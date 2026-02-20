// TAG: #NAT_AGENT #VERSION_1_0 #WINDOWS_SERVICE
// AgentService.cs - Windows ServiceBase + TcpListener loop
// Max 20 concurrent connections via SemaphoreSlim.
// Auth: validates JSON "token" field against registry value.

using System;
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
            _listenerThread = new Thread(ListenerLoop) { IsBackground = true, Name = "AgentListener" };
            _listenerThread.Start();
        }

        protected override void OnStop()
        {
            AgentLog.Write("AgentService.OnStop() - stopping");
            _running = false;
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
            try
            {
                remoteEp = client.Client.RemoteEndPoint?.ToString() ?? "(unknown)";
                client.ReceiveTimeout = 10000;  // 10s to send request
                client.SendTimeout = 30000;     // 30s to receive response

                using (client)
                using (var stream = client.GetStream())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" })
                {
                    string line = reader.ReadLine();
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
                        AgentLog.Write($"HandleClient() - UNAUTHORIZED from {remoteEp}");
                        writer.WriteLine("{\"success\":false,\"error\":\"Unauthorized\"}");
                        return;
                    }

                    AgentLog.Write($"HandleClient() - {command} from {remoteEp}");
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
                _connSemaphore?.Dispose();
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
                    File.AppendAllText(Program.LOG_PATH, line + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch { /* log failures are non-fatal */ }
        }
    }
}
