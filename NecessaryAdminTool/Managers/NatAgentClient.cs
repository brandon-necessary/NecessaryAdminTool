// TAG: #NAT_AGENT #VERSION_1_0 #FLEET_SCAN
// NatAgentClient.cs - NAT-side async TCP client for NecessaryAdminAgent
//
// Usage pattern (caller falls back to WMI when agent not reachable):
//   var info = await NatAgentClient.GetSystemInfoAsync(hostname)
//              ?? await WmiScanner.GetSystemInfoAsync(hostname);
//
// Settings read at call time (no caching):
//   Properties.Settings.Default.AgentToken  -- shared token set by admin
//   Properties.Settings.Default.AgentPort   -- TCP port (default 443)

using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NecessaryAdminTool.Managers
{
    /// <summary>
    /// Async TCP client for NecessaryAdminAgent.
    /// Returns null on any error so the caller can fall back to WMI.
    /// </summary>
    public static class NatAgentClient
    {
        private const int ConnectTimeoutMs = 3000;  // 3s connect timeout
        private const int ReadTimeoutMs    = 15000; // 15s read timeout

        // ---- Public API ----

        /// <summary>
        /// Send PING to the agent. Returns true if agent responded correctly.
        /// </summary>
        public static async Task<bool> PingAsync(string hostname)
        {
            try
            {
                string response = await SendCommandAsync(hostname, "PING").ConfigureAwait(false);
                return response != null && response.Contains("\"success\":true");
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Query GET_SYSTEM_INFO. Returns parsed AgentSystemInfo, or null if agent not reachable.
        /// </summary>
        public static async Task<AgentSystemInfo> GetSystemInfoAsync(string hostname)
        {
            try
            {
                string response = await SendCommandAsync(hostname, "GET_SYSTEM_INFO").ConfigureAwait(false);
                if (response == null || !response.Contains("\"success\":true"))
                    return null;

                return AgentSystemInfo.Parse(response);
            }
            catch (Exception ex)
            {
                LogManager.LogDebug($"NatAgentClient.GetSystemInfoAsync({hostname}) - {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Query GET_PROCESSES. Returns raw JSON string, or null if agent not reachable.
        /// </summary>
        public static async Task<string> GetProcessesAsync(string hostname)
            => await SendCommandAsync(hostname, "GET_PROCESSES").ConfigureAwait(false);

        /// <summary>
        /// Query GET_SERVICES. Returns raw JSON string, or null if agent not reachable.
        /// </summary>
        public static async Task<string> GetServicesAsync(string hostname)
            => await SendCommandAsync(hostname, "GET_SERVICES").ConfigureAwait(false);

        /// <summary>
        /// Query GET_NETWORK. Returns raw JSON string, or null if agent not reachable.
        /// </summary>
        public static async Task<string> GetNetworkAsync(string hostname)
            => await SendCommandAsync(hostname, "GET_NETWORK").ConfigureAwait(false);

        /// <summary>
        /// Query GET_INSTALLED. Returns raw JSON string, or null if agent not reachable.
        /// </summary>
        public static async Task<string> GetInstalledAsync(string hostname)
            => await SendCommandAsync(hostname, "GET_INSTALLED").ConfigureAwait(false);

        // ---- Core send/receive ----

        private static async Task<string> SendCommandAsync(string hostname, string command)
        {
            string token = NecessaryAdminTool.Properties.Settings.Default.AgentToken ?? "";
            int port = NecessaryAdminTool.Properties.Settings.Default.AgentPort;
            if (port <= 0) port = 443;

            if (string.IsNullOrEmpty(token))
            {
                LogManager.LogDebug($"NatAgentClient.SendCommandAsync() - AgentToken not configured, skipping agent for {hostname}");
                return null;
            }

            string request = $"{{\"token\":{JsonEscape(token)},\"command\":{JsonEscape(command)}}}\n";
            LogManager.LogDebug($"NatAgentClient.SendCommandAsync() - {command} → {hostname}:{port}");

            try
            {
                using (var tcp = new TcpClient())
                {
                    // Connect with async timeout — avoids blocking a thread-pool thread via .Wait()
                    var connectTask = tcp.ConnectAsync(hostname, port);
                    var completed = await Task.WhenAny(connectTask, Task.Delay(ConnectTimeoutMs)).ConfigureAwait(false);
                    if (completed != connectTask || connectTask.IsFaulted)
                    {
                        LogManager.LogDebug($"NatAgentClient - connect timeout to {hostname}:{port}");
                        return null;
                    }
                    if (!tcp.Connected) return null;

                    tcp.ReceiveTimeout = ReadTimeoutMs;
                    tcp.SendTimeout = 10000;

                    using (var stream = tcp.GetStream())
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true })
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        await writer.WriteAsync(request).ConfigureAwait(false);
                        string response = await reader.ReadLineAsync().ConfigureAwait(false);
                        LogManager.LogDebug($"NatAgentClient - {command} response from {hostname}: {(response?.Length > 200 ? response.Substring(0, 200) + "..." : response)}");
                        return response;
                    }
                }
            }
            catch (SocketException ex)
            {
                LogManager.LogDebug($"NatAgentClient - SocketException connecting to {hostname}:{port}: {ex.Message}");
                return null;
            }
            catch (IOException ex)
            {
                LogManager.LogDebug($"NatAgentClient - IOException with {hostname}: {ex.Message}");
                return null;
            }
        }

        // ---- Helpers ----

        private static string JsonEscape(string s)
        {
            if (s == null) return "null";
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
    }

    // -----------------------------------------------------------------------
    // AgentSystemInfo — parsed response from GET_SYSTEM_INFO
    // Matches Master_Update_Log.csv schema (20 fields)
    // -----------------------------------------------------------------------
    public class AgentSystemInfo
    {
        public string Hostname      { get; set; }
        public string OS            { get; set; }
        public string Build         { get; set; }
        public string Arch          { get; set; }
        public string Manufacturer  { get; set; }
        public string Model         { get; set; }
        public string Serial        { get; set; }
        public string Processor     { get; set; }
        public string TotalRamGB    { get; set; }
        public string FreeRamGB     { get; set; }
        public string DiskTotalGB   { get; set; }
        public string DiskFreeGB    { get; set; }
        public string IPAddress     { get; set; }
        public string MACAddress    { get; set; }
        public string LoggedInUser  { get; set; }
        public string LastBoot      { get; set; }
        public string TPMStatus     { get; set; }
        public string SecureBoot    { get; set; }
        public string AgentVersion  { get; set; }

        /// <summary>
        /// Parse the flat JSON response from GET_SYSTEM_INFO.
        /// Uses a simple field extractor to avoid pulling in Newtonsoft.
        /// </summary>
        public static AgentSystemInfo Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            return new AgentSystemInfo
            {
                Hostname     = Extract(json, "hostname"),
                OS           = Extract(json, "os"),
                Build        = Extract(json, "build"),
                Arch         = Extract(json, "arch"),
                Manufacturer = Extract(json, "manufacturer"),
                Model        = Extract(json, "model"),
                Serial       = Extract(json, "serial"),
                Processor    = Extract(json, "processor"),
                TotalRamGB   = Extract(json, "total_ram_gb"),
                FreeRamGB    = Extract(json, "free_ram_gb"),
                DiskTotalGB  = Extract(json, "disk_total_gb"),
                DiskFreeGB   = Extract(json, "disk_free_gb"),
                IPAddress    = Extract(json, "ip_address"),
                MACAddress   = Extract(json, "mac_address"),
                LoggedInUser = Extract(json, "logged_in_user"),
                LastBoot     = Extract(json, "last_boot"),
                TPMStatus    = Extract(json, "tpm_status"),
                SecureBoot   = Extract(json, "secure_boot"),
                AgentVersion = Extract(json, "agent_version"),
            };
        }

        // Minimal JSON string field extractor
        private static string Extract(string json, string field)
        {
            string key = "\"" + field + "\"";
            int idx = json.IndexOf(key, StringComparison.Ordinal);
            if (idx < 0) return "";
            idx += key.Length;
            while (idx < json.Length && (json[idx] == ' ' || json[idx] == ':')) idx++;
            if (idx >= json.Length) return "";
            if (json[idx] != '"') return ""; // non-string — skip
            idx++;
            var sb = new StringBuilder();
            while (idx < json.Length && json[idx] != '"')
            {
                if (json[idx] == '\\' && idx + 1 < json.Length)
                {
                    idx++;
                    switch (json[idx])
                    {
                        case '"':  sb.Append('"');  break;
                        case '\\': sb.Append('\\'); break;
                        case 'n':  sb.Append('\n'); break;
                        case 'r':  sb.Append('\r'); break;
                        case 't':  sb.Append('\t'); break;
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
    }
}
