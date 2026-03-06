using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using NecessaryAdminTool.Models;
using NecessaryAdminTool.Security;
// TAG: #FEATURE_BULK_OPERATIONS #PARALLEL_EXECUTION #ASYNC_OPERATIONS #VERSION_2_0

namespace NecessaryAdminTool.Managers
{
    /// <summary>
    /// Parallel execution engine for bulk operations
    /// Handles threading, retries, timeouts, and progress reporting
    /// TAG: #BULK_OPERATIONS #ASYNC_OPERATIONS #PARALLEL_PROCESSING
    /// </summary>
    public class BulkOperationExecutor
    {
        /// <summary>
        /// Execute operation on multiple targets in parallel
        /// TAG: #ASYNC_OPERATIONS #PARALLEL_PROCESSING
        /// </summary>
        public async Task<List<ComputerOperationResult>> ExecuteParallelAsync(
            BulkOperation operation,
            List<string> targets,
            IProgress<BulkOperationProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            var results = new ConcurrentBag<ComputerOperationResult>();
            var completedCount = 0;
            var successCount = 0;
            var failureCount = 0;

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = operation.MaxDegreeOfParallelism,
                CancellationToken = cancellationToken
            };

            try
            {
                await Task.Run(() =>
                {
                    Parallel.ForEach(targets, parallelOptions, target =>
                    {
                        if (cancellationToken.IsCancellationRequested)
                            return;

                        try
                        {
                            // Report progress - current target
                            progress?.Report(new BulkOperationProgress
                            {
                                TotalTargets = targets.Count,
                                CompletedTargets = completedCount,
                                SuccessCount = successCount,
                                FailureCount = failureCount,
                                CurrentTarget = target
                            });

                            // Execute operation with retry logic
                            var result = ExecuteWithRetry(operation, target, cancellationToken);
                            results.Add(result);

                            // Update counts
                            Interlocked.Increment(ref completedCount);
                            if (result.Success)
                                Interlocked.Increment(ref successCount);
                            else if (!result.Skipped)
                                Interlocked.Increment(ref failureCount);

                            // Report progress - completion
                            progress?.Report(new BulkOperationProgress
                            {
                                TotalTargets = targets.Count,
                                CompletedTargets = completedCount,
                                SuccessCount = successCount,
                                FailureCount = failureCount,
                                CurrentTarget = null
                            });
                        }
                        catch (Exception ex)
                        {
                            LogManager.LogError($"[BulkOperationExecutor] Error executing on {target}", ex);
                            results.Add(ComputerOperationResult.Failed(target, ex.Message));
                            Interlocked.Increment(ref completedCount);
                            Interlocked.Increment(ref failureCount);
                        }
                    });
                }, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                LogManager.LogWarning("[BulkOperationExecutor] Parallel execution cancelled");
            }

            return results.OrderBy(r => r.ComputerName).ToList();
        }

        /// <summary>
        /// Execute operation with retry logic
        /// TAG: #RETRY_LOGIC #FAULT_TOLERANCE
        /// </summary>
        private ComputerOperationResult ExecuteWithRetry(
            BulkOperation operation,
            string target,
            CancellationToken cancellationToken)
        {
            var startTime = DateTime.Now;
            Exception lastException = null;

            for (int attempt = 0; attempt < operation.MaxRetryAttempts; attempt++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return ComputerOperationResult.CreateSkipped(target, "Operation cancelled");
                }

                try
                {
                    // Execute with timeout
                    using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                    {
                        cts.CancelAfter(operation.TimeoutPerComputerMs);

                        var task = Task.Run(() => ExecuteOnComputer(operation, target), cts.Token);
                        task.Wait(cts.Token);

                        var result = task.Result;
                        result.RetryCount = attempt;
                        result.StartTime = startTime;
                        return result;
                    }
                }
                catch (OperationCanceledException)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return ComputerOperationResult.CreateSkipped(target, "Operation cancelled");
                    }
                    else
                    {
                        lastException = new TimeoutException($"Operation timed out after {operation.TimeoutPerComputerMs}ms");
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                }

                // Wait before retry (exponential backoff, cancellation-aware)
                if (attempt < operation.MaxRetryAttempts - 1)
                {
                    int delayMs = Math.Min(1000 * (int)Math.Pow(2, attempt), 10000);
                    cancellationToken.WaitHandle.WaitOne(delayMs);
                    if (cancellationToken.IsCancellationRequested)
                        break;
                }
            }

            // All retries exhausted
            var failedResult = ComputerOperationResult.Failed(target, lastException?.Message ?? "Unknown error");
            failedResult.RetryCount = operation.MaxRetryAttempts;
            failedResult.StartTime = startTime;
            return failedResult;
        }

        /// <summary>
        /// Execute operation on a single computer
        /// TAG: #BULK_OPERATIONS #REMOTE_EXECUTION
        /// </summary>
        private ComputerOperationResult ExecuteOnComputer(BulkOperation operation, string computerName)
        {
            try
            {
                switch (operation.OperationType)
                {
                    case BulkOperationType.PingTest:
                        return ExecutePingTest(computerName);

                    case BulkOperationType.RestartComputers:
                        return ExecuteRestart(computerName, operation.Parameters);

                    case BulkOperationType.RunPowerShellScript:
                        return ExecutePowerShellScript(computerName, operation.Parameters);

                    case BulkOperationType.CollectSystemInventory:
                        return ExecuteInventoryCollection(computerName);

                    case BulkOperationType.EnableService:
                        return ExecuteServiceControl(computerName, operation.Parameters, true);

                    case BulkOperationType.DisableService:
                        return ExecuteServiceControl(computerName, operation.Parameters, false);

                    case BulkOperationType.WMIScan:
                        return ExecuteWMIScan(computerName);

                    default:
                        return ComputerOperationResult.Failed(computerName, $"Operation type {operation.OperationType} not implemented");
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"[BulkOperationExecutor] Failed to execute {operation.OperationType} on {computerName}", ex);
                return ComputerOperationResult.Failed(computerName, ex.Message);
            }
        }

        /// <summary>
        /// Execute ping test
        /// TAG: #BULK_OPERATIONS #PING_TEST
        /// </summary>
        private ComputerOperationResult ExecutePingTest(string computerName)
        {
            try
            {
                var ping = new System.Net.NetworkInformation.Ping();
                var reply = ping.Send(computerName, 1200);

                if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                {
                    var result = ComputerOperationResult.Succeeded(computerName, $"Online - {reply.RoundtripTime}ms");
                    result.ResultData["RoundTripTime"] = reply.RoundtripTime;
                    result.ResultData["IPAddress"] = reply.Address.ToString();
                    return result;
                }
                else
                {
                    return ComputerOperationResult.Failed(computerName, $"Ping failed: {reply.Status}");
                }
            }
            catch (Exception ex)
            {
                return ComputerOperationResult.Failed(computerName, $"Ping error: {ex.Message}");
            }
        }

        /// <summary>
        /// Execute computer restart
        /// TAG: #BULK_OPERATIONS #RESTART_COMPUTER #WMI
        /// </summary>
        private ComputerOperationResult ExecuteRestart(string computerName, Dictionary<string, object> parameters)
        {
            try
            {
                bool forced = parameters.ContainsKey("Forced") && (bool)parameters["Forced"];
                int flags = forced ? 6 : 2; // 6 = forced reboot, 2 = graceful reboot

                // TAG: #PERFORMANCE_AUDIT #WMI_TIMEOUT - ConnectionOptions.Timeout prevents indefinite hang on unreachable hosts
                var connOpts = new ConnectionOptions { Timeout = TimeSpan.FromSeconds(30), EnablePrivileges = true, Impersonation = ImpersonationLevel.Impersonate };
                var scope = new ManagementScope($"\\\\{computerName}\\root\\cimv2", connOpts);
                scope.Connect();

                var query = new ObjectQuery("SELECT * FROM Win32_OperatingSystem");
                using (var searcher = new ManagementObjectSearcher(scope, query))
                {
                    foreach (ManagementObject os in searcher.Get())
                    {
                        var result = os.InvokeMethod("Win32Shutdown", new object[] { flags });
                        int returnValue = Convert.ToInt32(result);

                        if (returnValue == 0)
                        {
                            string restartType = forced ? "forced" : "graceful";
                            return ComputerOperationResult.Succeeded(computerName, $"Restart initiated ({restartType})");
                        }
                        else
                        {
                            return ComputerOperationResult.Failed(computerName, $"Restart failed with code: {returnValue}");
                        }
                    }
                }

                return ComputerOperationResult.Failed(computerName, "No operating system found");
            }
            catch (Exception ex)
            {
                return ComputerOperationResult.Failed(computerName, $"Restart error: {ex.Message}");
            }
        }

        /// <summary>
        /// Execute PowerShell script remotely
        /// TAG: #BULK_OPERATIONS #POWERSHELL_REMOTE #SECURITY_CRITICAL
        /// </summary>
        private ComputerOperationResult ExecutePowerShellScript(string computerName, Dictionary<string, object> parameters)
        {
            try
            {
                // TAG: #SECURITY_CRITICAL - Script already validated in BulkOperationManager
                string scriptContent = parameters["ScriptContent"]?.ToString();

                if (string.IsNullOrWhiteSpace(scriptContent))
                {
                    return ComputerOperationResult.Failed(computerName, "Script content is empty");
                }

                // Execute using WMI Win32_Process
                // TAG: #PERFORMANCE_AUDIT #WMI_TIMEOUT
                var connOpts = new ConnectionOptions { Timeout = TimeSpan.FromSeconds(30), EnablePrivileges = true, Impersonation = ImpersonationLevel.Impersonate };
                var scope = new ManagementScope($"\\\\{computerName}\\root\\cimv2", connOpts);
                scope.Connect();

                var processClass = new ManagementClass(scope, new ManagementPath("Win32_Process"), null);
                var inParams = processClass.GetMethodParameters("Create");

                // TAG: #SECURITY_CRITICAL - Sanitize computer name in script context
                string sanitizedComputerName = SecurityValidator.SanitizeForPowerShell(computerName);
                string command = $"powershell.exe -ExecutionPolicy Bypass -Command \"{scriptContent}\"";

                inParams["CommandLine"] = command;

                var outParams = processClass.InvokeMethod("Create", inParams, null);
                int returnValue = Convert.ToInt32(outParams["returnValue"]);

                if (returnValue == 0)
                {
                    int processId = Convert.ToInt32(outParams["processId"]);
                    var result = ComputerOperationResult.Succeeded(computerName, $"Script executed - PID: {processId}");
                    result.ResultData["ProcessId"] = processId;
                    return result;
                }
                else
                {
                    return ComputerOperationResult.Failed(computerName, $"Script execution failed with code: {returnValue}");
                }
            }
            catch (Exception ex)
            {
                return ComputerOperationResult.Failed(computerName, $"Script error: {ex.Message}");
            }
        }

        /// <summary>
        /// Execute system inventory collection
        /// TAG: #BULK_OPERATIONS #INVENTORY #WMI
        /// </summary>
        private ComputerOperationResult ExecuteInventoryCollection(string computerName)
        {
            // Strategy 0: NecessaryAdminAgent — try before WMI.
            // .GetAwaiter().GetResult() is safe here: called on thread pool (no SynchronizationContext).
            // TAG: #NAT_AGENT #BULK_OPERATIONS
            try
            {
                var agentInfo = NatAgentClient.GetSystemInfoAsync(computerName).GetAwaiter().GetResult();
                if (agentInfo != null)
                {
                    LogManager.LogDebug($"[BulkInventory] {computerName}: Agent hit");
                    var agentResult = ComputerOperationResult.Succeeded(computerName, "Inventory collected via Agent");
                    agentResult.ResultData["OS"]              = agentInfo.OS;
                    agentResult.ResultData["Version"]         = agentInfo.Build;
                    agentResult.ResultData["Manufacturer"]    = agentInfo.Manufacturer;
                    agentResult.ResultData["Model"]           = agentInfo.Model;
                    agentResult.ResultData["Serial"]          = agentInfo.Serial;
                    agentResult.ResultData["CPU"]             = agentInfo.Processor;
                    agentResult.ResultData["RAM_GB"]          = agentInfo.TotalRamGB;
                    agentResult.ResultData["LoggedInUser"]    = agentInfo.LoggedInUser;
                    agentResult.ResultData["CollectionMethod"] = "Agent";
                    return agentResult;
                }
                LogManager.LogDebug($"[BulkInventory] {computerName}: Agent null — falling back to WMI");
            }
            catch (Exception agentEx)
            {
                LogManager.LogDebug($"[BulkInventory] {computerName}: Agent exception — falling back to WMI: {agentEx.Message}");
            }

            // Strategy 1: WMI fallback
            try
            {
                // TAG: #PERFORMANCE_AUDIT #WMI_TIMEOUT
                var connOpts = new ConnectionOptions { Timeout = TimeSpan.FromSeconds(30), EnablePrivileges = true, Impersonation = ImpersonationLevel.Impersonate };
                var scope = new ManagementScope($"\\\\{computerName}\\root\\cimv2", connOpts);
                scope.Connect();

                var result = ComputerOperationResult.Succeeded(computerName, "Inventory collected");

                // Collect OS info
                var osQuery = new ObjectQuery("SELECT * FROM Win32_OperatingSystem");
                using (var osSearcher = new ManagementObjectSearcher(scope, osQuery))
                {
                    foreach (ManagementObject os in osSearcher.Get())
                    {
                        result.ResultData["OS"] = os["Caption"]?.ToString();
                        result.ResultData["Version"] = os["Version"]?.ToString();
                        result.ResultData["ServicePack"] = os["ServicePackMajorVersion"]?.ToString();
                        break;
                    }
                }

                // Collect computer system info
                var csQuery = new ObjectQuery("SELECT * FROM Win32_ComputerSystem");
                using (var csSearcher = new ManagementObjectSearcher(scope, csQuery))
                {
                    foreach (ManagementObject cs in csSearcher.Get())
                    {
                        result.ResultData["Manufacturer"] = cs["Manufacturer"]?.ToString();
                        result.ResultData["Model"] = cs["Model"]?.ToString();
                        result.ResultData["Domain"] = cs["Domain"]?.ToString();
                        break;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                return ComputerOperationResult.Failed(computerName, $"Inventory error: {ex.Message}");
            }
        }

        /// <summary>
        /// Execute service control (enable/disable)
        /// TAG: #BULK_OPERATIONS #SERVICE_CONTROL #WMI
        /// </summary>
        private ComputerOperationResult ExecuteServiceControl(string computerName, Dictionary<string, object> parameters, bool enable)
        {
            try
            {
                if (!parameters.ContainsKey("ServiceName"))
                {
                    return ComputerOperationResult.Failed(computerName, "ServiceName parameter missing");
                }

                string serviceName = parameters["ServiceName"]?.ToString();

                // TAG: #SECURITY_CRITICAL - Sanitize service name
                serviceName = SecurityValidator.SanitizeForPowerShell(serviceName);

                // TAG: #PERFORMANCE_AUDIT #WMI_TIMEOUT
                var connOpts = new ConnectionOptions { Timeout = TimeSpan.FromSeconds(30), EnablePrivileges = true, Impersonation = ImpersonationLevel.Impersonate };
                var scope = new ManagementScope($"\\\\{computerName}\\root\\cimv2", connOpts);
                scope.Connect();

                var query = new ObjectQuery($"SELECT * FROM Win32_Service WHERE Name = '{serviceName}'");
                using (var searcher = new ManagementObjectSearcher(scope, query))
                {
                    foreach (ManagementObject service in searcher.Get())
                    {
                        string action = enable ? "StartService" : "StopService";
                        var result = service.InvokeMethod(action, null);
                        int returnValue = Convert.ToInt32(result);

                        if (returnValue == 0)
                        {
                            string status = enable ? "started" : "stopped";
                            return ComputerOperationResult.Succeeded(computerName, $"Service {serviceName} {status}");
                        }
                        else
                        {
                            return ComputerOperationResult.Failed(computerName, $"Service control failed with code: {returnValue}");
                        }
                    }
                }

                return ComputerOperationResult.Failed(computerName, $"Service {serviceName} not found");
            }
            catch (Exception ex)
            {
                return ComputerOperationResult.Failed(computerName, $"Service control error: {ex.Message}");
            }
        }

        /// <summary>
        /// Execute WMI scan
        /// TAG: #BULK_OPERATIONS #WMI_SCAN
        /// </summary>
        private ComputerOperationResult ExecuteWMIScan(string computerName)
        {
            try
            {
                // TAG: #PERFORMANCE_AUDIT #WMI_TIMEOUT
                var connOpts = new ConnectionOptions { Timeout = TimeSpan.FromSeconds(30), EnablePrivileges = true, Impersonation = ImpersonationLevel.Impersonate };
                var scope = new ManagementScope($"\\\\{computerName}\\root\\cimv2", connOpts);
                scope.Connect();

                var result = ComputerOperationResult.Succeeded(computerName, "WMI scan completed");
                result.ResultData["WMIAccessible"] = true;

                return result;
            }
            catch (Exception ex)
            {
                return ComputerOperationResult.Failed(computerName, $"WMI scan error: {ex.Message}");
            }
        }
    }
}
