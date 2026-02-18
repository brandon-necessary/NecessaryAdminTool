using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
// TAG: #SCHEDULED_TASKS #AUTOMATION #VERSION_1_2

namespace NecessaryAdminTool
{
    /// <summary>
    /// Manages Windows Task Scheduler tasks for automatic scanning
    /// TAG: #BACKGROUND_SCANNING #TASK_SCHEDULER
    /// </summary>
    public static class ScheduledTaskManager
    {
        private const string TASK_NAME = "NecessaryAdminTool_AutoScan";
        private const string TASK_FOLDER = "\\NecessaryAdminTool\\";

        /// <summary>
        /// Check if the scheduled task exists
        /// </summary>
        public static bool TaskExists()
        {
            try
            {
                var result = RunSchtasks($"/Query /TN \"{TASK_FOLDER}{TASK_NAME}\"");
                return result.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Create scheduled task for automatic scanning
        /// </summary>
        public static bool CreateTask(int intervalHours, bool runAsAdmin = true)
        {
            try
            {
                if (TaskExists())
                {
                    LogManager.LogInfo("Scheduled task already exists, deleting before recreating");
                    DeleteTask();
                }

                // Get current executable path (MainModule can be null in rare cases)
                var mainModule = Process.GetCurrentProcess().MainModule;
                if (mainModule == null)
                    throw new InvalidOperationException("Could not determine the executable path (Process.MainModule is null).");
                var exePath = mainModule.FileName;
                var arguments = "/autoscan"; // Command-line argument for auto-scan mode

                // Build schtasks command
                var runLevel = runAsAdmin ? "HIGHEST" : "LIMITED";
                var username = WindowsIdentity.GetCurrent().Name;

                // Create the task with specified interval
                string triggerArgs;
                if (intervalHours == 0)
                {
                    // Manual only - create task but disabled
                    triggerArgs = "";
                }
                else if (intervalHours >= 24)
                {
                    // Daily
                    triggerArgs = $"/SC DAILY /ST 00:00";
                }
                else
                {
                    // Hourly intervals (1, 2, 4, etc.)
                    triggerArgs = $"/SC HOURLY /MO {intervalHours}";
                }

                var createCommand = $"/Create /TN \"{TASK_FOLDER}{TASK_NAME}\" " +
                    $"/TR \"\\\"{exePath}\\\" {arguments}\" " +
                    $"/SC MINUTE /MO 1 " + // Will be changed by trigger args
                    $"/RL {runLevel} " +
                    $"/F"; // Force create (overwrites if exists)

                // If we have actual trigger args, add them
                if (!string.IsNullOrEmpty(triggerArgs))
                {
                    createCommand = $"/Create /TN \"{TASK_FOLDER}{TASK_NAME}\" " +
                        $"/TR \"\\\"{exePath}\\\" {arguments}\" " +
                        $"{triggerArgs} " +
                        $"/RL {runLevel} " +
                        $"/F";
                }

                var result = RunSchtasks(createCommand);

                if (result.ExitCode == 0)
                {
                    LogManager.LogInfo($"Scheduled task created: {TASK_NAME} (interval: {intervalHours}h, runAsAdmin: {runAsAdmin})");

                    // If manual only, disable the task
                    if (intervalHours == 0)
                    {
                        RunSchtasks($"/Change /TN \"{TASK_FOLDER}{TASK_NAME}\" /DISABLE");
                        LogManager.LogInfo("Task created but disabled (manual mode)");
                    }

                    return true;
                }
                else
                {
                    LogManager.LogError($"Failed to create scheduled task. Exit code: {result.ExitCode}\n{result.Output}", null);
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to create scheduled task", ex);
                return false;
            }
        }

        /// <summary>
        /// Delete the scheduled task
        /// </summary>
        public static bool DeleteTask()
        {
            try
            {
                if (!TaskExists())
                {
                    LogManager.LogInfo("Scheduled task does not exist, nothing to delete");
                    return true;
                }

                var result = RunSchtasks($"/Delete /TN \"{TASK_FOLDER}{TASK_NAME}\" /F");

                if (result.ExitCode == 0)
                {
                    LogManager.LogInfo($"Scheduled task deleted: {TASK_NAME}");
                    return true;
                }
                else
                {
                    LogManager.LogError($"Failed to delete scheduled task. Exit code: {result.ExitCode}", null);
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to delete scheduled task", ex);
                return false;
            }
        }

        /// <summary>
        /// Start the scheduled task immediately (run now)
        /// </summary>
        public static bool RunTask()
        {
            try
            {
                if (!TaskExists())
                {
                    LogManager.LogWarning("Cannot run task - it does not exist");
                    return false;
                }

                var result = RunSchtasks($"/Run /TN \"{TASK_FOLDER}{TASK_NAME}\"");

                if (result.ExitCode == 0)
                {
                    LogManager.LogInfo($"Scheduled task started: {TASK_NAME}");
                    return true;
                }
                else
                {
                    LogManager.LogError($"Failed to run scheduled task. Exit code: {result.ExitCode}", null);
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to run scheduled task", ex);
                return false;
            }
        }

        /// <summary>
        /// Enable the scheduled task
        /// </summary>
        public static bool EnableTask()
        {
            try
            {
                if (!TaskExists())
                {
                    LogManager.LogWarning("Cannot enable task - it does not exist");
                    return false;
                }

                var result = RunSchtasks($"/Change /TN \"{TASK_FOLDER}{TASK_NAME}\" /ENABLE");

                if (result.ExitCode == 0)
                {
                    LogManager.LogInfo($"Scheduled task enabled: {TASK_NAME}");
                    return true;
                }
                else
                {
                    LogManager.LogError($"Failed to enable scheduled task. Exit code: {result.ExitCode}", null);
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to enable scheduled task", ex);
                return false;
            }
        }

        /// <summary>
        /// Disable the scheduled task
        /// </summary>
        public static bool DisableTask()
        {
            try
            {
                if (!TaskExists())
                {
                    LogManager.LogWarning("Cannot disable task - it does not exist");
                    return false;
                }

                var result = RunSchtasks($"/Change /TN \"{TASK_FOLDER}{TASK_NAME}\" /DISABLE");

                if (result.ExitCode == 0)
                {
                    LogManager.LogInfo($"Scheduled task disabled: {TASK_NAME}");
                    return true;
                }
                else
                {
                    LogManager.LogError($"Failed to disable scheduled task. Exit code: {result.ExitCode}", null);
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to disable scheduled task", ex);
                return false;
            }
        }

        /// <summary>
        /// Get the status of the scheduled task
        /// </summary>
        public static TaskStatus GetTaskStatus()
        {
            try
            {
                if (!TaskExists())
                {
                    return new TaskStatus
                    {
                        Exists = false,
                        IsEnabled = false,
                        LastRunTime = null,
                        NextRunTime = null,
                        State = "Not Installed"
                    };
                }

                var result = RunSchtasks($"/Query /TN \"{TASK_FOLDER}{TASK_NAME}\" /FO LIST /V");

                if (result.ExitCode == 0)
                {
                    return ParseTaskStatus(result.Output);
                }

                return new TaskStatus { Exists = true, State = "Unknown" };
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to get task status", ex);
                return new TaskStatus { Exists = false, State = "Error" };
            }
        }

        private static TaskStatus ParseTaskStatus(string output)
        {
            var status = new TaskStatus { Exists = true };

            try
            {
                foreach (var line in output.Split('\n'))
                {
                    var cleanLine = line.Trim();

                    if (cleanLine.StartsWith("Status:"))
                    {
                        status.State = cleanLine.Substring(7).Trim();
                        status.IsEnabled = !status.State.Equals("Disabled", StringComparison.OrdinalIgnoreCase);
                    }
                    else if (cleanLine.StartsWith("Last Run Time:"))
                    {
                        var timeStr = cleanLine.Substring(14).Trim();
                        if (DateTime.TryParse(timeStr, out var lastRun))
                        {
                            status.LastRunTime = lastRun;
                        }
                    }
                    else if (cleanLine.StartsWith("Next Run Time:"))
                    {
                        var timeStr = cleanLine.Substring(14).Trim();
                        if (DateTime.TryParse(timeStr, out var nextRun))
                        {
                            status.NextRunTime = nextRun;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to parse task status", ex);
            }

            return status;
        }

        private static (int ExitCode, string Output) RunSchtasks(string arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = Process.Start(startInfo))
            {
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                var combinedOutput = string.IsNullOrEmpty(error) ? output : $"{output}\n{error}";
                return (process.ExitCode, combinedOutput);
            }
        }

        /// <summary>
        /// Check if current user has admin privileges
        /// Uses Win32 TokenElevation API for accurate UAC elevation detection
        /// TAG: #UAC_DETECTION #ELEVATION_CHECK
        /// </summary>
        public static bool IsAdministrator()
        {
            try
            {
                // Use Win32 TokenElevation API for accurate detection (Session 9b fix)
                return Helpers.Win32Helper.IsProcessElevated();
            }
            catch (Exception ex)
            {
                LogManager.LogError("[ScheduledTaskManager] Error checking elevation status", ex);
                return false;
            }
        }
    }

    /// <summary>
    /// Represents the status of a scheduled task
    /// </summary>
    public class TaskStatus
    {
        public bool Exists { get; set; }
        public bool IsEnabled { get; set; }
        public string State { get; set; }
        public DateTime? LastRunTime { get; set; }
        public DateTime? NextRunTime { get; set; }

        public string GetDisplayText()
        {
            if (!Exists)
                return "Not Installed";

            if (!IsEnabled)
                return "Disabled";

            return $"{State} (Next: {NextRunTime?.ToString("g") ?? "N/A"})";
        }
    }
}
