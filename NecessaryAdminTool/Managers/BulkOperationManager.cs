using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NecessaryAdminTool.Models;
using NecessaryAdminTool.Security;
// TAG: #FEATURE_BULK_OPERATIONS #MANAGER #SECURITY_CRITICAL #VERSION_2_0

namespace NecessaryAdminTool.Managers
{
    /// <summary>
    /// Core bulk operation manager for multi-computer management
    /// Implements security validations, parallel execution, and result tracking
    /// TAG: #BULK_OPERATIONS #ASYNC_OPERATIONS #SECURITY_CRITICAL
    /// </summary>
    public class BulkOperationManager
    {
        private const int MAX_TARGETS_PER_OPERATION = 1000; // Rate limiting
        private const int DEFAULT_THREAD_COUNT = 10;
        private const int DEFAULT_TIMEOUT_MS = 300000; // 5 minutes
        private const int DEFAULT_RETRY_ATTEMPTS = 3;

        private BulkOperationExecutor _executor;

        public BulkOperationManager()
        {
            _executor = new BulkOperationExecutor();
        }

        /// <summary>
        /// Execute bulk operation with full security validation
        /// TAG: #SECURITY_CRITICAL #BULK_OPERATIONS #ASYNC_OPERATIONS
        /// </summary>
        public async Task<BulkOperationResult> ExecuteBulkOperationAsync(
            BulkOperation operation,
            IProgress<BulkOperationProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new BulkOperationResult
            {
                OperationId = operation.OperationId,
                OperationType = operation.OperationType,
                StartTime = DateTime.Now
            };

            try
            {
                // TAG: #SECURITY_CRITICAL #INPUT_VALIDATION
                // Phase 1: Validate all targets
                LogManager.LogInfo($"[BulkOperationManager] Starting bulk operation - Type: {operation.OperationType}, Targets: {operation.Targets.Count}");

                var validTargets = ValidateTargets(operation.Targets);
                if (validTargets.Count == 0)
                {
                    LogManager.LogWarning("[BulkOperationManager] No valid targets after validation");
                    return BulkOperationResult.Failed("No valid targets provided");
                }

                // TAG: #SECURITY_CRITICAL #RATE_LIMITING
                // Phase 2: Rate limiting check
                if (validTargets.Count > MAX_TARGETS_PER_OPERATION)
                {
                    LogManager.LogWarning($"[BulkOperationManager] Target count exceeds maximum ({validTargets.Count} > {MAX_TARGETS_PER_OPERATION})");
                    return BulkOperationResult.Failed($"Target count exceeds maximum allowed ({MAX_TARGETS_PER_OPERATION})");
                }

                // TAG: #SECURITY_CRITICAL #PARAMETER_VALIDATION
                // Phase 3: Validate operation-specific parameters
                if (!ValidateOperationParameters(operation))
                {
                    LogManager.LogWarning($"[BulkOperationManager] Invalid operation parameters for {operation.OperationType}");
                    return BulkOperationResult.Failed("Invalid operation parameters");
                }

                // Phase 4: Audit logging
                AuditBulkOperation(operation, validTargets);

                // Phase 5: Execute with parallel processing
                result.FinalStatus = BulkOperationStatus.Running;
                var computerResults = await _executor.ExecuteParallelAsync(
                    operation,
                    validTargets,
                    progress,
                    cancellationToken
                );

                result.ComputerResults = computerResults;
                result.EndTime = DateTime.Now;

                // Phase 6: Determine final status
                if (cancellationToken.IsCancellationRequested)
                {
                    result.FinalStatus = BulkOperationStatus.Cancelled;
                }
                else if (result.SuccessCount == result.TotalTargets)
                {
                    result.FinalStatus = BulkOperationStatus.Completed;
                }
                else if (result.SuccessCount > 0)
                {
                    result.FinalStatus = BulkOperationStatus.PartiallyCompleted;
                }
                else
                {
                    result.FinalStatus = BulkOperationStatus.Failed;
                }

                LogManager.LogInfo($"[BulkOperationManager] Bulk operation completed - Status: {result.FinalStatus}, Success: {result.SuccessCount}/{result.TotalTargets}, Duration: {result.Duration.TotalSeconds:F1}s");

                return result;
            }
            catch (Exception ex)
            {
                LogManager.LogError($"[BulkOperationManager] Bulk operation failed: {operation.OperationType}", ex);
                result.FinalStatus = BulkOperationStatus.Failed;
                result.ErrorMessage = ex.Message;
                result.EndTime = DateTime.Now;
                return result;
            }
        }

        /// <summary>
        /// Validate all target computer names
        /// TAG: #SECURITY_CRITICAL #INPUT_VALIDATION
        /// </summary>
        private List<string> ValidateTargets(List<string> targets)
        {
            var validTargets = new List<string>();

            foreach (var target in targets)
            {
                // TAG: #SECURITY_CRITICAL #COMPUTER_NAME_VALIDATION
                if (SecurityValidator.ValidateComputerName(target))
                {
                    validTargets.Add(target);
                }
                else
                {
                    LogManager.LogWarning($"[BulkOperationManager] Invalid target blocked: {target}");
                }
            }

            LogManager.LogInfo($"[BulkOperationManager] Target validation - Valid: {validTargets.Count}, Invalid: {targets.Count - validTargets.Count}");
            return validTargets;
        }

        /// <summary>
        /// Validate operation-specific parameters
        /// TAG: #SECURITY_CRITICAL #PARAMETER_VALIDATION
        /// </summary>
        private bool ValidateOperationParameters(BulkOperation operation)
        {
            switch (operation.OperationType)
            {
                case BulkOperationType.RunPowerShellScript:
                    // TAG: #SECURITY_CRITICAL #POWERSHELL_VALIDATION
                    if (!operation.Parameters.ContainsKey("ScriptContent"))
                    {
                        LogManager.LogWarning("[BulkOperationManager] Missing ScriptContent parameter for PowerShell operation");
                        return false;
                    }

                    string scriptContent = operation.Parameters["ScriptContent"]?.ToString();
                    if (!SecurityValidator.ValidatePowerShellScript(scriptContent))
                    {
                        LogManager.LogWarning("[BulkOperationManager] PowerShell script validation failed - dangerous patterns detected");
                        return false;
                    }
                    break;

                case BulkOperationType.DeploySoftware:
                    // TAG: #SECURITY_CRITICAL #FILE_PATH_VALIDATION
                    if (!operation.Parameters.ContainsKey("InstallerPath"))
                    {
                        LogManager.LogWarning("[BulkOperationManager] Missing InstallerPath parameter for software deployment");
                        return false;
                    }

                    string installerPath = operation.Parameters["InstallerPath"]?.ToString();
                    if (!SecurityValidator.IsValidPath(installerPath))
                    {
                        LogManager.LogWarning("[BulkOperationManager] Installer path validation failed");
                        return false;
                    }
                    break;

                case BulkOperationType.ExecuteRemoteCommand:
                    // TAG: #SECURITY_CRITICAL #COMMAND_VALIDATION
                    if (!operation.Parameters.ContainsKey("Command"))
                    {
                        LogManager.LogWarning("[BulkOperationManager] Missing Command parameter for remote execution");
                        return false;
                    }

                    string command = operation.Parameters["Command"]?.ToString();
                    if (string.IsNullOrWhiteSpace(command))
                    {
                        LogManager.LogWarning("[BulkOperationManager] Empty command not allowed");
                        return false;
                    }

                    // Sanitize command for PowerShell execution
                    operation.Parameters["Command"] = SecurityValidator.SanitizeForPowerShell(command);
                    break;
            }

            return true;
        }

        /// <summary>
        /// Audit log for bulk operations
        /// TAG: #SECURITY_CRITICAL #AUDIT_LOGGING
        /// </summary>
        private void AuditBulkOperation(BulkOperation operation, List<string> validTargets)
        {
            string targetSummary = validTargets.Count <= 5
                ? string.Join(", ", validTargets)
                : $"{string.Join(", ", validTargets.Take(3))}... ({validTargets.Count} total)";

            LogManager.LogInfo($"[AUDIT] Bulk Operation Started - ID: {operation.OperationId}, Type: {operation.OperationType}, User: {operation.CreatedBy ?? Environment.UserName}, Targets: {targetSummary}, Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        }

        /// <summary>
        /// Export bulk operation results to CSV
        /// TAG: #BULK_OPERATIONS #EXPORT
        /// </summary>
        public string ExportResultsToCsv(BulkOperationResult result)
        {
            try
            {
                var csv = new System.Text.StringBuilder();
                csv.AppendLine("ComputerName,Status,Message,ExecutionTime(ms),RetryCount");

                foreach (var computerResult in result.ComputerResults)
                {
                    string status = computerResult.Success ? "Success" : (computerResult.Skipped ? "Skipped" : "Failed");
                    string message = computerResult.Message?.Replace(",", ";") ?? "";
                    string executionTime = computerResult.ExecutionTime.TotalMilliseconds.ToString("F0");

                    csv.AppendLine($"{computerResult.ComputerName},{status},{message},{executionTime},{computerResult.RetryCount}");
                }

                LogManager.LogInfo($"[BulkOperationManager] Results exported to CSV - {result.ComputerResults.Count} rows");
                return csv.ToString();
            }
            catch (Exception ex)
            {
                LogManager.LogError("[BulkOperationManager] Failed to export results to CSV", ex);
                return string.Empty;
            }
        }

        /// <summary>
        /// Get summary statistics for a bulk operation result
        /// TAG: #BULK_OPERATIONS #REPORTING
        /// </summary>
        public string GetResultSummary(BulkOperationResult result)
        {
            var summary = new System.Text.StringBuilder();
            summary.AppendLine($"Operation Type: {result.OperationType}");
            summary.AppendLine($"Status: {result.FinalStatus}");
            summary.AppendLine($"Duration: {result.Duration.TotalSeconds:F1} seconds");
            summary.AppendLine($"Total Targets: {result.TotalTargets}");
            summary.AppendLine($"Success: {result.SuccessCount} ({result.SuccessRate:F1}%)");
            summary.AppendLine($"Failed: {result.FailureCount}");
            summary.AppendLine($"Skipped: {result.SkippedCount}");

            if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                summary.AppendLine($"Error: {result.ErrorMessage}");
            }

            return summary.ToString();
        }
    }

    /// <summary>
    /// Progress report for bulk operations
    /// TAG: #BULK_OPERATIONS #PROGRESS_TRACKING
    /// </summary>
    public class BulkOperationProgress
    {
        public int TotalTargets { get; set; }
        public int CompletedTargets { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public string CurrentTarget { get; set; }
        public double PercentComplete => TotalTargets > 0 ? (double)CompletedTargets / TotalTargets * 100 : 0;
    }
}
