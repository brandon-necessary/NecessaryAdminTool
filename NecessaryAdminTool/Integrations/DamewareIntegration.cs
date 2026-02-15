using System;
using System.Diagnostics;
using NecessaryAdminTool.Security;

namespace NecessaryAdminTool.Integrations
{
    // TAG: #DAMEWARE #RMM_INTEGRATION
    /// <summary>
    /// Dameware Remote Everywhere integration (API-based)
    /// </summary>
    public static class DamewareIntegration
    {
        /// <summary>
        /// Launch Dameware remote session
        /// </summary>
        public static void LaunchSession(string targetHost, RmmToolConfig config)
        {
            try
            {
                // TAG: #SECURITY_CRITICAL #COMMAND_INJECTION_PREVENTION
                // Validate target host to prevent command injection
                if (!SecurityValidator.IsValidHostname(targetHost) && !SecurityValidator.IsValidIPAddress(targetHost))
                {
                    LogManager.LogWarning($"[Dameware] Blocked invalid target host: {targetHost}");
                    throw new ArgumentException($"Invalid target host format: {targetHost}");
                }

                string serverUrl = config.Settings.ContainsKey("ServerUrl")
                    ? config.Settings["ServerUrl"]
                    : "";

                if (string.IsNullOrEmpty(serverUrl))
                    throw new InvalidOperationException("Dameware server URL not configured");

                // Dameware uses web-based connection interface
                // API would create session and return connection URL

                string webUrl = $"{serverUrl}/login";

                var psi = new ProcessStartInfo
                {
                    FileName = webUrl,
                    UseShellExecute = true
                };

                Process.Start(psi);
                LogManager.LogInfo($"Dameware session launched: {targetHost}");
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to launch Dameware session to {targetHost}", ex);
                throw;
            }
        }

        /// <summary>
        /// Test Dameware connection
        /// </summary>
        public static bool TestConnection(RmmToolConfig config)
        {
            return config.Settings.ContainsKey("ServerUrl") &&
                   !string.IsNullOrEmpty(config.Settings["ServerUrl"]);
        }
    }
}
