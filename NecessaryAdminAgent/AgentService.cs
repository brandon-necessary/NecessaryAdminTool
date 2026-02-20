// TAG: #NAT_AGENT #VERSION_1_1 #WINDOWS_SERVICE #SECURITY_HARDENED
// AgentService.cs - Windows ServiceBase + TcpListener loop
// Max 20 concurrent connections via SemaphoreSlim.
// Auth: validates JSON "token" field against registry value.
// Security: message size limit (1MB), per-IP rate limiting (5 failures/min), log ACLs.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace NecessaryAdminAgent
{
    public class AgentService : ServiceBase
    {
        private Thread _listenerThread;
        private volatile bool _running;
        private TcpListener _listener;
        private readonly SemaphoreSlim _connSemaphore = new SemaphoreSlim(20, 20);

        // TAG: #SECURITY_HARDENED - Max request size to prevent DoS via unbounded ReadLine
        private const int MAX_REQUEST_BYTES = 1 * 1024 * 1024; // 1 MB

        // TAG: #SECURITY_HARDENED - Per-IP rate limiter for failed auth attempts
        private static readonly ConcurrentDictionary<string, AuthFailureTracker> _authFailures
            = new ConcurrentDictionary<string, AuthFailureTracker>();
        private const int MAX_AUTH_FAILURES = 5;          // max failures per window
        private static readonly TimeSpan AUTH_WINDOW = TimeSpan.FromMinutes(1);
        private Timer _rateLimitCleanupTimer;

        public AgentService()
        {
            ServiceName = Program.SERVICE_NAME;
            CanStop = true;
            CanPauseAndContinue = false;
            AutoLog = true;
        }

        protected override void OnStart(string[] args)
        {
            AgentLog.Write("AgentService.OnStart() - starting TCP listener thread");
            _running = true;

            // Periodic cleanup of expired rate-limit entries (every 2 minutes)
            _rateLimitCleanupTimer = new Timer(_ => CleanExpiredAuthEntries(), null, AUTH_WINDOW, TimeSpan.FromMinutes(2));

            _listenerThread = new Thread(ListenerLoop) { IsBackground = true, Name = "AgentListener" };
            _listenerThread.Start();
        }

        protected override void OnStop()
        {
            AgentLog.Write("AgentService.OnStop() - stopping");
            _running = false;
            try { _rateLimitCleanupTimer?.Dispose(); } catch { }
            try { _listener?.Stop(); } catch { }
            _listenerThread?.Join(5000);
            AgentLog.Write("AgentService.OnStop() - done");
        }

        // Called from --console mode
        public void StartInConsole() { OnStart(null); }
        public void StopInConsole() { OnStop(); }

        private void ListenerLoop()
        {
            int port = ReadPortFromRegistry();
            AgentLog.Write($"ListenerLoop() - binding TCP on port {port}");

            try
            {
                _listener = new TcpListener(IPAddress.Any, port);
                _listener.Start();
                AgentLog.Write($"ListenerLoop() - listening on :{port}");

                while (_running)
                {
                    TcpClient client = null;
                    try
                    {
                        client = _listener.AcceptTcpClient();
                    }
                    catch (SocketException)
                    {
                        // Thrown when _listener.Stop() is called — expected during shutdown
                        break;
                    }

                    if (!_connSemaphore.Wait(0))
                    {
                        // Too many concurrent connections
                        AgentLog.Write("ListenerLoop() - max connections reached, dropping client");
                        try { client.Close(); } catch { }
                        continue;
                    }

                    // Handle on background thread
                    var capturedClient = client;
                    var t = new Thread(() => HandleClient(capturedClient)) { IsBackground = true };
                    t.Start();
                }
            }
            catch (Exception ex)
            {
                AgentLog.Write($"ListenerLoop() - FATAL: {ex.Message}");
            }
            finally
            {
                try { _listener?.Stop(); } catch { }
                AgentLog.Write("ListenerLoop() - exited");
            }
        }

        private void HandleClient(TcpClient client)
        {
            string remoteEp = "(unknown)";
            string remoteIp = "(unknown)";
            try
            {
                remoteEp = client.Client.RemoteEndPoint?.ToString() ?? "(unknown)";
                // Extract IP without port for rate-limiting key
                var remoteEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
                remoteIp = remoteEndPoint?.Address.ToString() ?? "(unknown)";

                client.ReceiveTimeout = 10000;  // 10s to send request
                client.SendTimeout = 30000;     // 30s to receive response

                // TAG: #SECURITY_HARDENED - Check per-IP rate limit before processing
                if (IsRateLimited(remoteIp))
                {
                    AgentLog.Write($"HandleClient() - RATE LIMITED {remoteEp} (too many auth failures)");
                    try
                    {
                        using (client)
                        using (var stream = client.GetStream())
                        using (var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" })
                            writer.WriteLine("{\"success\":false,\"error\":\"Rate limited\"}");
                    }
                    catch { }
                    return;
                }

                using (client)
                using (var stream = client.GetStream())
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" })
                {
                    // TAG: #SECURITY_HARDENED - Bounded read to prevent DoS via unbounded ReadLine
                    string line = ReadLineBounded(stream, MAX_REQUEST_BYTES);
                    if (line == null)
                    {
                        AgentLog.Write($"HandleClient() - Request from {remoteEp} exceeded {MAX_REQUEST_BYTES} bytes or was empty");
                        writer.WriteLine("{\"success\":false,\"error\":\"Request too large\"}");
                        return;
                    }
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        writer.WriteLine("{\"success\":false,\"error\":\"Empty request\"}");
                        return;
                    }

                    // Parse minimal JSON to extract token + command
                    string token = JsonExtract(line, "token");
                    string command = JsonExtract(line, "command");

                    string expectedToken = ReadTokenFromRegistry();
                    if (string.IsNullOrEmpty(expectedToken) || !ConstantTimeEquals(token, expectedToken))
                    {
                        RecordAuthFailure(remoteIp);
                        AgentLog.Write($"HandleClient() - UNAUTHORIZED from {remoteEp}");
                        writer.WriteLine("{\"success\":false,\"error\":\"Unauthorized\"}");
                        return;
                    }

                    // Strip newlines from command before logging (prevent log injection)
                    string safeCmd = command?.Replace("\n", "").Replace("\r", "") ?? "(null)";
                    AgentLog.Write($"HandleClient() - {safeCmd} from {remoteEp}");
                    string response = QueryHandler.Execute(command);
                    writer.WriteLine(response);
                }
            }
            catch (Exception ex)
            {
                AgentLog.Write($"HandleClient() - ERROR from {remoteEp}: {ex.Message}");
            }
            finally
            {
                _connSemaphore.Release();
                try { client?.Close(); } catch { }
            }
        }

        /// <summary>
        /// Reads a single line from the stream with a maximum byte limit.
        /// Returns null if the limit is exceeded (DoS protection).
        /// Returns empty string if nothing was read before newline/EOF.
        /// </summary>
        private static string ReadLineBounded(NetworkStream stream, int maxBytes)
        {
            var sb = new StringBuilder();
            int totalRead = 0;
            int b;
            while ((b = stream.ReadByte()) != -1)
            {
                totalRead++;
                if (totalRead > maxBytes) return null; // exceeded limit
                if (b == '\n') break;
                if (b == '\r') continue; // skip CR in CRLF
                sb.Append((char)b);
            }
            return sb.ToString();
        }

        // ── Rate Limiting ────────────────────────────────────────────────

        private static bool IsRateLimited(string ip)
        {
            if (!_authFailures.TryGetValue(ip, out var tracker)) return false;
            lock (tracker)
            {
                // Expire old entries
                if (DateTime.UtcNow - tracker.WindowStart > AUTH_WINDOW)
                {
                    tracker.Count = 0;
                    tracker.WindowStart = DateTime.UtcNow;
                    return false;
                }
                return tracker.Count >= MAX_AUTH_FAILURES;
            }
        }

        private static void RecordAuthFailure(string ip)
        {
            var tracker = _authFailures.GetOrAdd(ip, _ => new AuthFailureTracker());
            lock (tracker)
            {
                if (DateTime.UtcNow - tracker.WindowStart > AUTH_WINDOW)
                {
                    // Start new window
                    tracker.Count = 1;
                    tracker.WindowStart = DateTime.UtcNow;
                }
                else
                {
                    tracker.Count++;
                }

                if (tracker.Count == MAX_AUTH_FAILURES)
                    AgentLog.Write($"RateLimit - IP {ip} blocked after {MAX_AUTH_FAILURES} auth failures in {AUTH_WINDOW.TotalSeconds}s");
            }
        }

        private static void CleanExpiredAuthEntries()
        {
            var now = DateTime.UtcNow;
            foreach (var kvp in _authFailures)
            {
                lock (kvp.Value)
                {
                    if (now - kvp.Value.WindowStart > AUTH_WINDOW)
                        _authFailures.TryRemove(kvp.Key, out _);
                }
            }
        }

        private class AuthFailureTracker
        {
            public int Count;
            public DateTime WindowStart = DateTime.UtcNow;
        }

        // ── JSON / Auth helpers ──────────────────────────────────────────

        // Minimal JSON field extractor — avoids pulling in Newtonsoft for a lightweight service.
        // Handles escaped characters inside string values (e.g. \", \\) to prevent auth bypass.
        internal static string JsonExtract(string json, string field)
        {
            string pattern = "\"" + field + "\"";
            int idx = json.IndexOf(pattern, StringComparison.Ordinal);
            if (idx < 0) return "";

            idx += pattern.Length;
            // Skip optional whitespace + colon
            while (idx < json.Length && (json[idx] == ' ' || json[idx] == ':' || json[idx] == '\t')) idx++;
            if (idx >= json.Length) return "";

            if (json[idx] == '"')
            {
                // Build string value respecting JSON escape sequences
                idx++; // skip opening quote
                var sb = new StringBuilder();
                while (idx < json.Length && json[idx] != '"')
                {
                    if (json[idx] == '\\' && idx + 1 < json.Length)
                    {
                        idx++; // skip backslash
                        switch (json[idx])
                        {
                            case '"':  sb.Append('"');  break;
                            case '\\': sb.Append('\\'); break;
                            case '/':  sb.Append('/');  break;
                            case 'n':  sb.Append('\n'); break;
                            case 'r':  sb.Append('\r'); break;
                            case 't':  sb.Append('\t'); break;
                            case 'b':  sb.Append('\b'); break;
                            case 'f':  sb.Append('\f'); break;
                            default:   sb.Append(json[idx]); break;
                        }
                    }
                    else
                    {
                        sb.Append(json[idx]);
                    }
                    idx++;
                }
                return sb.ToString();
            }

            // Non-string value (number, bool) — take until comma or closing brace
            int valEnd = idx;
            while (valEnd < json.Length && json[valEnd] != ',' && json[valEnd] != '}') valEnd++;
            return json.Substring(idx, valEnd - idx).Trim();
        }

        // Constant-time string comparison — prevents timing attacks on the auth token.
        // Returns false immediately on length mismatch (length is not secret).
        private static bool ConstantTimeEquals(string a, string b)
        {
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;
            int result = 0;
            for (int i = 0; i < a.Length; i++)
                result |= a[i] ^ b[i];
            return result == 0;
        }

        private static int ReadPortFromRegistry()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(Program.REGISTRY_KEY))
                    return key != null ? (int)(key.GetValue("Port") ?? 443) : 443;
            }
            catch { return 443; }
        }

        private static string ReadTokenFromRegistry()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(Program.REGISTRY_KEY))
                    return key?.GetValue("Token")?.ToString() ?? "";
            }
            catch { return ""; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _rateLimitCleanupTimer?.Dispose();
                _connSemaphore?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // Minimal thread-safe file logger (no dependency on NAT's LogManager)
    internal static class AgentLog
    {
        private static readonly object _lock = new object();

        public static void Write(string message)
        {
            try
            {
                string dir = System.IO.Path.GetDirectoryName(Program.LOG_PATH);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
                lock (_lock)
                {
                    // FileShare.ReadWrite: allows concurrent readers (e.g. admin viewing log) while agent writes
                    using (var fs = new FileStream(Program.LOG_PATH, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                    using (var sw = new StreamWriter(fs, Encoding.UTF8))
                        sw.WriteLine(line);
                }
            }
            catch { /* log failures are non-fatal */ }
        }
    }
}
