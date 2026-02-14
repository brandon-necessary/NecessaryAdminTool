using System;
using System.Diagnostics;
using System.Net.Http;

namespace ArtaznIT.Integrations
{
    // TAG: #MANAGEENGINE #ENDPOINT_CENTRAL #RMM_INTEGRATION
    /// <summary>
    /// ManageEngine Endpoint Central integration (API-based)
    /// PRIORITY for testing
    /// </summary>
    public static class ManageEngineIntegration
    {
        /// <summary>
        /// Launch ManageEngine Endpoint Central remote session
        /// </summary>
        public static void LaunchSession(string targetHost, RmmToolConfig config)
        {
            try
            {
                string serverUrl = config.Settings.ContainsKey("ServerUrl")
                    ? config.Settings["ServerUrl"]
                    : "";

                if (string.IsNullOrEmpty(serverUrl))
                    throw new InvalidOperationException("ManageEngine server URL not configured");

                string port = config.Settings.ContainsKey("Port")
                    ? config.Settings["Port"]
                    : "8383";

                // ManageEngine Endpoint Central remote control URL pattern
                // https://server:port/dcapi/remotecontrol/launchremote?computerId=xxx

                // For now, open the web console (full API implementation would require device ID lookup)
                string webUrl = $"{serverUrl}:{port}/";

                var psi = new ProcessStartInfo
                {
                    FileName = webUrl,
                    UseShellExecute = true
                };

                Process.Start(psi);
                LogManager.LogInfo($"ManageEngine Endpoint Central session launched: {targetHost}");
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to launch ManageEngine session to {targetHost}", ex);
                throw;
            }
        }

        /// <summary>
        /// Test ManageEngine connection
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
                    : "8383";

                // Test connectivity to ManageEngine API
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(10);
                    var task = client.GetAsync($"{serverUrl}:{port}/api/1.3/som/status");
                    task.Wait(10000);

                    if (task.IsCompleted && task.Result != null)
                        return task.Result.IsSuccessStatusCode || task.Result.StatusCode == System.Net.HttpStatusCode.Unauthorized;
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
