using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ArtaznIT.Integrations
{
    // TAG: #TEAMVIEWER #RMM_INTEGRATION
    /// <summary>
    /// TeamViewer remote control integration
    /// Supports CLI and API methods
    /// </summary>
    public static class TeamViewerIntegration
    {
        /// <summary>
        /// Launch TeamViewer remote session
        /// </summary>
        public static void LaunchSession(string targetHost, RmmToolConfig config)
        {
            try
            {
                string exePath = config.Settings.ContainsKey("ExePath")
                    ? config.Settings["ExePath"]
                    : @"C:\Program Files\TeamViewer\TeamViewer.exe";

                if (!File.Exists(exePath))
                    throw new FileNotFoundException($"TeamViewer not found at: {exePath}");

                string authMethod = config.Settings.ContainsKey("AuthMethod")
                    ? config.Settings["AuthMethod"]
                    : "cli";

                if (authMethod == "cli")
                {
                    LaunchViaCli(exePath, targetHost, config);
                }
                else
                {
                    // API method not fully implemented - fallback to CLI
                    LaunchViaCli(exePath, targetHost, config);
                }

                LogManager.LogInfo($"TeamViewer session launched: {targetHost}");
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to launch TeamViewer session to {targetHost}", ex);
                throw;
            }
        }

        private static void LaunchViaCli(string exePath, string targetHost, RmmToolConfig config)
        {
            string arguments = $"-i {targetHost}";

            // Add password if configured
            string password = SecureCredentialManager.RetrieveCredential("TeamViewer", "Password");
            if (!string.IsNullOrEmpty(password))
            {
                string base64Password = Convert.ToBase64String(Encoding.UTF8.GetBytes(password));
                arguments += $" -p {base64Password}";
            }

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = false
            };

            Process.Start(psi);
        }

        /// <summary>
        /// Test TeamViewer connection
        /// </summary>
        public static bool TestConnection(RmmToolConfig config)
        {
            try
            {
                string exePath = config.Settings.ContainsKey("ExePath")
                    ? config.Settings["ExePath"]
                    : @"C:\Program Files\TeamViewer\TeamViewer.exe";

                return File.Exists(exePath);
            }
            catch
            {
                return false;
            }
        }
    }
}
