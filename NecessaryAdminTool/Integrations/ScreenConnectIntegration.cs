using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;

namespace NecessaryAdminTool.Integrations
{
    // TAG: #SCREENCONNECT #CONNECTWISE #RMM_INTEGRATION
    /// <summary>
    /// ScreenConnect/ConnectWise Control integration
    /// Supports both URL scheme and API methods
    /// </summary>
    public static class ScreenConnectIntegration
    {
        /// <summary>
        /// Launch ScreenConnect remote session
        /// </summary>
        public static void LaunchSession(string targetHost, RmmToolConfig config)
        {
            try
            {
                string serverUrl = config.Settings.ContainsKey("ServerUrl")
                    ? config.Settings["ServerUrl"]
                    : "";

                if (string.IsNullOrEmpty(serverUrl))
                    throw new InvalidOperationException("ScreenConnect server URL not configured");

                string port = config.Settings.ContainsKey("Port")
                    ? config.Settings["Port"]
                    : "443";

                string authMethod = config.Settings.ContainsKey("AuthMethod")
                    ? config.Settings["AuthMethod"]
                    : "url";

                if (authMethod == "url")
                {
                    // Simple URL launch (no API credentials needed)
                    LaunchViaUrl(serverUrl, port, targetHost, config);
                }
                else if (authMethod == "api")
                {
                    // API-based session creation
                    LaunchViaApi(serverUrl, port, targetHost, config);
                }

                LogManager.LogInfo($"ScreenConnect session launched: {targetHost} via {authMethod}");
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to launch ScreenConnect session to {targetHost}", ex);
                throw;
            }
        }

        private static void LaunchViaUrl(string serverUrl, string port, string targetHost, RmmToolConfig config)
        {
            // Construct session URL
            string sessionUrl = $"https://{serverUrl}:{port}/Host#Access/All Machines//{targetHost}/Join";

            // Launch in default browser
            var psi = new ProcessStartInfo
            {
                FileName = sessionUrl,
                UseShellExecute = true
            };

            Process.Start(psi);
        }

        private static void LaunchViaApi(string serverUrl, string port, string targetHost, RmmToolConfig config)
        {
            // API-based launch (requires API token)
            string apiToken = SecureCredentialManager.RetrieveCredential("ScreenConnect", "ApiToken");

            if (string.IsNullOrEmpty(apiToken))
                throw new InvalidOperationException("ScreenConnect API token not configured");

            // For now, fall back to URL method
            // Full API implementation would require async HTTP calls
            LaunchViaUrl(serverUrl, port, targetHost, config);
        }

        /// <summary>
        /// Test ScreenConnect connection
        /// </summary>
        public static bool TestConnection(RmmToolConfig config)
        {
            try
            {
                string serverUrl = config.Settings.ContainsKey("ServerUrl")
                    ? config.Settings["ServerUrl"]
                    : "";

                if (string.IsNullOrEmpty(serverUrl))
                    return false;

                string port = config.Settings.ContainsKey("Port")
                    ? config.Settings["Port"]
                    : "443";

                // Test basic connectivity
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(10);
                    var task = client.GetAsync($"https://{serverUrl}:{port}/");
                    task.Wait(10000);

                    if (task.IsCompleted && task.Result != null)
                        return task.Result.IsSuccessStatusCode || task.Result.StatusCode == System.Net.HttpStatusCode.Redirect;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
