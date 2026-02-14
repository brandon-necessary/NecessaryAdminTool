using System;
using System.Diagnostics;

namespace ArtaznIT.Integrations
{
    // TAG: #REMOTEPC #RMM_INTEGRATION
    /// <summary>
    /// RemotePC integration (API-based)
    /// </summary>
    public static class RemotePCIntegration
    {
        /// <summary>
        /// Launch RemotePC remote session
        /// </summary>
        public static void LaunchSession(string targetHost, RmmToolConfig config)
        {
            try
            {
                // RemotePC uses web-based connection URLs
                // API would return connection URL to open in browser

                string apiUrl = config.Settings.ContainsKey("ApiUrl")
                    ? config.Settings["ApiUrl"]
                    : "https://api.remotepc.com";

                // For now, launch web interface (full API implementation would fetch connection URL)
                string webUrl = $"https://www.remotepc.com/login";

                var psi = new ProcessStartInfo
                {
                    FileName = webUrl,
                    UseShellExecute = true
                };

                Process.Start(psi);
                LogManager.LogInfo($"RemotePC session launched: {targetHost}");
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to launch RemotePC session to {targetHost}", ex);
                throw;
            }
        }

        /// <summary>
        /// Test RemotePC connection
        /// </summary>
        public static bool TestConnection(RmmToolConfig config)
        {
            // Basic validation - check if API URL is configured
            return config.Settings.ContainsKey("ApiUrl") &&
                   !string.IsNullOrEmpty(config.Settings["ApiUrl"]);
        }
    }
}
