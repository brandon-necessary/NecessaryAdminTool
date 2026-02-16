using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;

namespace NecessaryAdminTool
{
    // TAG: #VERSION_7.1 #REMEDIATION #AUTOMATION
    /// <summary>
    /// Manages automated remediation actions for common IT issues
    /// Provides one-click fixes for Windows Update, DNS, Print Spooler, WinRM, Time Sync, Event Logs
    /// </summary>
    public class RemediationManager
    {
        /// <summary>
        /// Available remediation actions
        /// </summary>
        public enum RemediationAction
        {
            RestartWindowsUpdate,
            ClearDNSCache,
            RestartPrintSpooler,
            EnableWinRM,
            FixTimeSync,
            ClearEventLogs
        }

        /// <summary>
        /// Result of a remediation action on a single computer
        /// </summary>
        public class RemediationResult
        {
            public string Hostname { get; set; }
            public RemediationAction Action { get; set; }
            public bool Success { get; set; }
            public string Message { get; set; }
            public DateTime Timestamp { get; set; }
            public TimeSpan Duration { get; set; }
        }

        /// <summary>
        /// Execute remediation action on a single computer
        /// TAG: #VERSION_7.1 #REMEDIATION
        /// </summary>
        public static async Task<RemediationResult> ExecuteRemediationAsync(
            string hostname,
            RemediationAction action,
            string username = null,
            string password = null,
            CancellationToken ct = default)
        {
            var startTime = DateTime.Now;
            var result = new RemediationResult
            {
                Hostname = hostname,
                Action = action,
                Timestamp = startTime
            };

            try
            {
                await Task.Run(async () =>
                {
                    switch (action)
                    {
                        case RemediationAction.RestartWindowsUpdate:
                            await RestartWindowsUpdateServiceAsync(hostname, username, password);
                            result.Success = true;
                            result.Message = "Windows Update service restarted and cache cleared";
                            break;

                        case RemediationAction.ClearDNSCache:
                            await ClearDNSCacheAsync(hostname, username, password);
                            result.Success = true;
                            result.Message = "DNS cache flushed successfully";
                            break;

                        case RemediationAction.RestartPrintSpooler:
                            await RestartPrintSpoolerAsync(hostname, username, password);
                            result.Success = true;
                            result.Message = "Print Spooler service restarted";
                            break;

                        case RemediationAction.EnableWinRM:
                            await EnableWinRMAsync(hostname, username, password);
                            result.Success = true;
                            result.Message = "WinRM enabled and configured";
                            break;

                        case RemediationAction.FixTimeSync:
                            await FixTimeSyncAsync(hostname, username, password);
                            result.Success = true;
                            result.Message = "Time synchronized with domain";
                            break;

                        case RemediationAction.ClearEventLogs:
                            await ClearEventLogsAsync(hostname, username, password);
                            result.Success = true;
                            result.Message = "Event logs cleared (Application, System, Security)";
                            break;
                    }
                }, ct);

                result.Duration = DateTime.Now - startTime;
                LogManager.LogInfo($"[Remediation] {action} completed on {hostname} in {result.Duration.TotalSeconds:F1}s");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Error: {ex.Message}";
                result.Duration = DateTime.Now - startTime;
                LogManager.LogError($"[Remediation] {action} failed on {hostname}", ex);
            }

            return result;
        }

        /// <summary>
        /// Restart Windows Update service and clear cache
        /// TAG: #VERSION_7.1 #REMEDIATION #WINDOWS_UPDATE #LOGGING
        /// </summary>
        private static async Task RestartWindowsUpdateServiceAsync(string hostname, string username, string password)
        {
            LogManager.LogInfo($"RemediationManager.RestartWindowsUpdateService() - START - Target: {hostname}");

            var scope = GetWmiScope(hostname, username, password);

            // Stop Windows Update service
            LogManager.LogInfo($"Stopping Windows Update service (wuauserv) on {hostname}");
            StopService(scope, "wuauserv");
            await Task.Delay(2000); // Wait for service to stop

            // Clear SoftwareDistribution folder (optional - requires file access)
            try
            {
                LogManager.LogInfo($"Attempting to clear SoftwareDistribution cache on {hostname}");
                var deleteCmd = new ManagementClass(scope, new ManagementPath("Win32_Process"), null);
                var inParams = deleteCmd.GetMethodParameters("Create");
                inParams["CommandLine"] = "cmd.exe /c rd /s /q C:\\Windows\\SoftwareDistribution\\Download";
                deleteCmd.InvokeMethod("Create", inParams, null);
                await Task.Delay(1000);
                LogManager.LogInfo($"SoftwareDistribution cache clear command executed on {hostname}");
            }
            catch (Exception ex)
            {
                LogManager.LogWarning($"[Remediation] Could not clear SoftwareDistribution cache on {hostname}: {ex.Message}");
            }

            // Start Windows Update service
            LogManager.LogInfo($"Starting Windows Update service (wuauserv) on {hostname}");
            StartService(scope, "wuauserv");
            LogManager.LogInfo($"RemediationManager.RestartWindowsUpdateService() - SUCCESS - Windows Update restarted on {hostname}");
        }

        /// <summary>
        /// Clear DNS cache via ipconfig /flushdns
        /// TAG: #VERSION_7.1 #REMEDIATION #DNS #LOGGING
        /// </summary>
        private static Task ClearDNSCacheAsync(string hostname, string username, string password)
        {
            LogManager.LogInfo($"RemediationManager.ClearDNSCache() - START - Target: {hostname}");

            var scope = GetWmiScope(hostname, username, password);

            // Execute ipconfig /flushdns
            LogManager.LogInfo($"Executing ipconfig /flushdns on {hostname}");
            var processClass = new ManagementClass(scope, new ManagementPath("Win32_Process"), null);
            var inParams = processClass.GetMethodParameters("Create");
            inParams["CommandLine"] = "ipconfig /flushdns";

            var outParams = processClass.InvokeMethod("Create", inParams, null);
            var returnCode = outParams["ReturnValue"].ToString();

            if (returnCode != "0")
            {
                LogManager.LogError($"RemediationManager.ClearDNSCache() - FAILED - Return code: {returnCode} - Target: {hostname}");
                throw new Exception($"Failed to execute ipconfig /flushdns (Return code: {returnCode})");
            }

            LogManager.LogInfo($"RemediationManager.ClearDNSCache() - SUCCESS - DNS cache cleared on {hostname}");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Restart Print Spooler service
        /// TAG: #VERSION_7.1 #REMEDIATION #PRINT_SPOOLER #LOGGING
        /// </summary>
        private static async Task RestartPrintSpoolerAsync(string hostname, string username, string password)
        {
            LogManager.LogInfo($"RemediationManager.RestartPrintSpooler() - START - Target: {hostname}");

            var scope = GetWmiScope(hostname, username, password);

            LogManager.LogInfo($"Stopping Print Spooler service on {hostname}");
            StopService(scope, "spooler");
            await Task.Delay(2000);

            LogManager.LogInfo($"Starting Print Spooler service on {hostname}");
            StartService(scope, "spooler");

            LogManager.LogInfo($"RemediationManager.RestartPrintSpooler() - SUCCESS - Print Spooler restarted on {hostname}");
        }

        /// <summary>
        /// Enable WinRM for remote management
        /// TAG: #VERSION_7.1 #REMEDIATION #WINRM #LOGGING
        /// </summary>
        private static Task EnableWinRMAsync(string hostname, string username, string password)
        {
            LogManager.LogInfo($"RemediationManager.EnableWinRM() - START - Target: {hostname}");

            var scope = GetWmiScope(hostname, username, password);

            // Execute winrm quickconfig -force
            LogManager.LogInfo($"Executing 'winrm quickconfig -force -quiet' on {hostname}");
            var processClass = new ManagementClass(scope, new ManagementPath("Win32_Process"), null);
            var inParams = processClass.GetMethodParameters("Create");
            inParams["CommandLine"] = "winrm quickconfig -force -quiet";

            var outParams = processClass.InvokeMethod("Create", inParams, null);
            var returnCode = outParams["ReturnValue"].ToString();

            if (returnCode != "0")
            {
                LogManager.LogError($"RemediationManager.EnableWinRM() - FAILED - Return code: {returnCode} - Target: {hostname}");
                throw new Exception($"Failed to enable WinRM (Return code: {returnCode})");
            }

            LogManager.LogInfo($"RemediationManager.EnableWinRM() - SUCCESS - WinRM enabled on {hostname}");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Fix time synchronization with domain controller
        /// TAG: #VERSION_7.1 #REMEDIATION #TIME_SYNC
        /// </summary>
        private static async Task FixTimeSyncAsync(string hostname, string username, string password)
        {
            var scope = GetWmiScope(hostname, username, password);

            // Stop Windows Time service
            StopService(scope, "w32time");
            await Task.Delay(1000);

            // Resync time
            var processClass = new ManagementClass(scope, new ManagementPath("Win32_Process"), null);
            var inParams = processClass.GetMethodParameters("Create");
            inParams["CommandLine"] = "w32tm /resync /force";

            processClass.InvokeMethod("Create", inParams, null);
            await Task.Delay(1000);

            // Start Windows Time service
            StartService(scope, "w32time");
        }

        /// <summary>
        /// Clear Windows Event Logs (Application, System, Security)
        /// TAG: #VERSION_7.1 #REMEDIATION #EVENT_LOGS
        /// </summary>
        private static Task ClearEventLogsAsync(string hostname, string username, string password)
        {
            var scope = GetWmiScope(hostname, username, password);

            string[] logNames = { "Application", "System", "Security" };

            foreach (var logName in logNames)
            {
                try
                {
                    var query = $"SELECT * FROM Win32_NTEventLogFile WHERE LogfileName='{logName}'";
                    using (var searcher = new ManagementObjectSearcher(scope, new ObjectQuery(query)))
                    using (var results = searcher.Get())
                    {
                        foreach (ManagementObject log in results)
                        {
                            using (log)
                            {
                                log.InvokeMethod("ClearEventLog", null);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogManager.LogWarning($"[Remediation] Could not clear {logName} log: {ex.Message}");
                }
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Stop a Windows service via WMI
        /// </summary>
        private static void StopService(ManagementScope scope, string serviceName)
        {
            var query = $"SELECT * FROM Win32_Service WHERE Name='{serviceName}'";
            using (var searcher = new ManagementObjectSearcher(scope, new ObjectQuery(query)))
            using (var results = searcher.Get())
            {
                foreach (ManagementObject service in results)
                {
                    using (service)
                    {
                        if (service["State"].ToString() == "Running")
                        {
                            service.InvokeMethod("StopService", null);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Start a Windows service via WMI
        /// </summary>
        private static void StartService(ManagementScope scope, string serviceName)
        {
            var query = $"SELECT * FROM Win32_Service WHERE Name='{serviceName}'";
            using (var searcher = new ManagementObjectSearcher(scope, new ObjectQuery(query)))
            using (var results = searcher.Get())
            {
                foreach (ManagementObject service in results)
                {
                    using (service)
                    {
                        if (service["State"].ToString() != "Running")
                        {
                            service.InvokeMethod("StartService", null);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Create WMI scope with credentials
        /// </summary>
        private static ManagementScope GetWmiScope(string hostname, string username, string password)
        {
            var options = new ConnectionOptions
            {
                Timeout = TimeSpan.FromSeconds(30),
                EnablePrivileges = true,
                Authentication = AuthenticationLevel.PacketPrivacy,
                Impersonation = ImpersonationLevel.Impersonate
            };

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                options.Username = username;
                options.Password = password;
            }

            var scope = new ManagementScope($"\\\\{hostname}\\root\\cimv2", options);
            scope.Connect();

            return scope;
        }

        /// <summary>
        /// Get friendly name for remediation action
        /// </summary>
        public static string GetActionName(RemediationAction action)
        {
            return action switch
            {
                RemediationAction.RestartWindowsUpdate => "Restart Windows Update",
                RemediationAction.ClearDNSCache => "Clear DNS Cache",
                RemediationAction.RestartPrintSpooler => "Restart Print Spooler",
                RemediationAction.EnableWinRM => "Enable WinRM",
                RemediationAction.FixTimeSync => "Fix Time Sync",
                RemediationAction.ClearEventLogs => "Clear Event Logs",
                _ => action.ToString()
            };
        }

        /// <summary>
        /// Get icon for remediation action
        /// </summary>
        public static string GetActionIcon(RemediationAction action)
        {
            return action switch
            {
                RemediationAction.RestartWindowsUpdate => "🔄",
                RemediationAction.ClearDNSCache => "🌐",
                RemediationAction.RestartPrintSpooler => "🖨️",
                RemediationAction.EnableWinRM => "⚡",
                RemediationAction.FixTimeSync => "🕐",
                RemediationAction.ClearEventLogs => "🗑️",
                _ => "🔧"
            };
        }
    }
}
