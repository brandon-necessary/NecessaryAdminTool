using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using SecValidator = NecessaryAdminTool.Security.SecurityValidator;

namespace NecessaryAdminTool.Integrations
{
    // TAG: #ANYDESK #RMM_INTEGRATION
    /// <summary>
    /// AnyDesk remote control integration (CLI-based)
    /// Easiest integration - simple command-line execution
    /// </summary>
    public static class AnyDeskIntegration
    {
        /// <summary>
        /// Launch AnyDesk remote session to target host
        /// </summary>
        public static void LaunchSession(string targetHost, RmmToolConfig config)
        {
            try
            {
                // TAG: #SECURITY_CRITICAL #COMMAND_INJECTION_PREVENTION
                // Validate target host to prevent command injection
                if (!SecurityValidator.IsValidHostname(targetHost) && !SecurityValidator.IsValidIPAddress(targetHost))
                {
                    LogManager.LogWarning($"[AnyDesk] Blocked invalid target host: {targetHost}");
                    throw new ArgumentException($"Invalid target host format: {targetHost}");
                }

                // Get AnyDesk executable path
                string exePath = config.Settings.ContainsKey("ExePath")
                    ? config.Settings["ExePath"]
                    : @"C:\Program Files (x86)\AnyDesk\AnyDesk.exe";

                if (!File.Exists(exePath))
                    throw new FileNotFoundException($"AnyDesk not found at: {exePath}. Please configure the correct path in Options.");

                // Get connection mode
                string connectionMode = config.Settings.ContainsKey("ConnectionMode")
                    ? config.Settings["ConnectionMode"]
                    : "attended";

                // TAG: #SECURITY_CRITICAL #COMMAND_INJECTION_PREVENTION
                // Sanitize target host for command line usage
                string safeTargetHost = SecurityValidator.SanitizePowerShellInput(targetHost);

                // Build arguments
                string arguments = $"{safeTargetHost} --plain";

                // Add password for unattended access
                if (connectionMode == "unattended")
                {
                    string password = SecureCredentialManager.RetrieveCredential("AnyDesk", "Password");
                    if (!string.IsNullOrEmpty(password))
                    {
                        // TAG: #SECURITY_CRITICAL #COMMAND_INJECTION_PREVENTION
                        string safePassword = SecurityValidator.SanitizePowerShellInput(password);
                        arguments += $" --with-password \"{safePassword}\"";
                    }
                }

                // Launch AnyDesk
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                Process.Start(psi);
                LogManager.LogInfo($"AnyDesk session launched: {targetHost} (mode: {connectionMode})");
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to launch AnyDesk session to {targetHost}", ex);
                throw;
            }
        }

        /// <summary>
        /// Test AnyDesk connection
        /// </summary>
        public static bool TestConnection(RmmToolConfig config)
        {
            try
            {
                string exePath = config.Settings.ContainsKey("ExePath")
                    ? config.Settings["ExePath"]
                    : @"C:\Program Files (x86)\AnyDesk\AnyDesk.exe";

                if (!File.Exists(exePath))
                    return false;

                // Test by getting version
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(psi))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit(5000);

                    return process.HasExited && process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output);
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
