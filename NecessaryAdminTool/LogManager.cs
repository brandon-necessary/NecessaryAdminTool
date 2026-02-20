using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NecessaryAdminTool.Security;
// TAG: #LOGGING #DIAGNOSTICS #VERSION_1_2 #SECURITY_CRITICAL #PATH_TRAVERSAL_PREVENTION #ASYNC_OPTIMIZATION

namespace NecessaryAdminTool
{
    /// <summary>
    /// Centralized logging manager for application-wide diagnostics
    /// TAG: #ERROR_TRACKING #DEBUGGING
    /// </summary>
    public static class LogManager
    {
        private static readonly string LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NecessaryAdminTool", "Logs");

        private static readonly string LogFile = Path.Combine(LogDirectory,
            $"NAT_{DateTime.Now:yyyy-MM-dd}.log");

        private static readonly object _lockObject = new object();
        private static bool _initialized = false;

        static LogManager()
        {
            InitializeLogging();
        }

        private static void InitializeLogging()
        {
            try
            {
                if (!Directory.Exists(LogDirectory))
                {
                    Directory.CreateDirectory(LogDirectory);
                }

                _initialized = true;

                // TAG: #ASYNC_OPTIMIZATION - Clean up old log files asynchronously (fire-and-forget)
                _ = CleanOldLogsAsync();
            }
            catch
            {
                // Silently fail if logging initialization fails
                _initialized = false;
            }
        }

        /// <summary>
        /// Clean old log files asynchronously
        /// TAG: #ASYNC_OPTIMIZATION - Made async to prevent UI blocking during initialization
        /// </summary>
        private static async Task CleanOldLogsAsync()
        {
            try
            {
                // TAG: #SECURITY_CRITICAL #PATH_TRAVERSAL_PREVENTION
                // Validate log directory is within allowed base path
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string allowedBasePath = Path.Combine(appDataPath, "NecessaryAdminTool");

                if (!SecurityValidator.IsValidFilePath(LogDirectory, allowedBasePath))
                {
                    LogWarning("[LogManager] CleanOldLogs blocked - invalid log directory path");
                    return;
                }

                // TAG: #ASYNC_OPTIMIZATION - Run file system operations on background thread
                await Task.Run(() =>
                {
                    var cutoffDate = DateTime.Now.AddDays(-30);
                    var logFiles = Directory.GetFiles(LogDirectory, "NAT_*.log");

                    foreach (var logFile in logFiles)
                    {
                        // TAG: #SECURITY_CRITICAL #PATH_TRAVERSAL_PREVENTION
                        // Validate each log file path before deletion
                        string fullLogPath = Path.GetFullPath(logFile);
                        if (!SecurityValidator.IsValidFilePath(fullLogPath, LogDirectory))
                        {
                            LogWarning($"[LogManager] Blocked deletion of file outside log directory: {logFile}");
                            continue;
                        }

                        var fileInfo = new FileInfo(logFile);
                        if (fileInfo.LastWriteTime < cutoffDate)
                        {
                            File.Delete(logFile);
                        }
                    }
                });
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }

        /// <summary>
        /// Log informational message
        /// </summary>
        public static void LogInfo(string message)
        {
            WriteLog("INFO", message);
        }

        /// <summary>
        /// Log warning message
        /// </summary>
        public static void LogWarning(string message)
        {
            WriteLog("WARN", message);
        }

        /// <summary>
        /// Log error message
        /// </summary>
        public static void LogError(string message, Exception ex = null)
        {
            var fullMessage = ex != null
                ? $"{message}\nException: {ex.GetType().Name}\nMessage: {ex.Message}\nStack: {ex.StackTrace}"
                : message;

            WriteLog("ERROR", fullMessage);
        }

        /// <summary>
        /// Log debug message (only in DEBUG builds)
        /// </summary>
        public static void LogDebug(string message)
        {
            #if DEBUG
            WriteLog("DEBUG", message);
            #endif
        }

        private static void WriteLog(string level, string message)
        {
            if (!_initialized)
            {
                return;
            }

            // TAG: #ASYNC_OPTIMIZATION #PERFORMANCE - Fire-and-forget async to prevent UI blocking
            _ = WriteLogAsync(level, message);
        }

        /// <summary>
        /// Async log writer to prevent UI blocking on file I/O
        /// TAG: #ASYNC_OPTIMIZATION - All file writes run on background thread
        /// </summary>
        private static async Task WriteLogAsync(string level, string message)
        {
            try
            {
                // TAG: #SECURITY_CRITICAL #PATH_TRAVERSAL_PREVENTION
                // Validate log file path before writing
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string allowedBasePath = Path.Combine(appDataPath, "NecessaryAdminTool");
                string fullLogPath = Path.GetFullPath(LogFile);

                if (!SecurityValidator.IsValidFilePath(fullLogPath, allowedBasePath))
                {
                    // Cannot log warning since we're in the logging function
                    return;
                }

                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var logEntry = $"[{timestamp}] [{level}] {message}\n";

                // TAG: #ASYNC_OPTIMIZATION - Run file I/O on background thread
                await Task.Run(() =>
                {
                    lock (_lockObject)
                    {
                        File.AppendAllText(LogFile, logEntry, Encoding.UTF8);
                    }
                });
            }
            catch (Exception fileEx)
            {
                // File write failed — emit a best-effort entry to Windows Event Log
                // so there is SOME audit trail of the failure
                try
                {
                    System.Diagnostics.EventLog.WriteEntry(
                        "NecessaryAdminTool",
                        $"LogManager file write failed: {fileEx.Message} | Level: {level} | Msg: {message}",
                        System.Diagnostics.EventLogEntryType.Warning);
                }
                catch { /* Event log also unavailable — best effort */ }
            }
        }

        /// <summary>
        /// Get path to current log file
        /// </summary>
        public static string GetCurrentLogFile()
        {
            return LogFile;
        }

        /// <summary>
        /// Get path to log directory
        /// </summary>
        public static string GetLogDirectory()
        {
            return LogDirectory;
        }

        /// <summary>
        /// Read recent log entries
        /// TAG: #ASYNC_OPTIMIZATION - Made async to prevent UI blocking on file read
        /// </summary>
        public static async Task<string> GetRecentLogsAsync(int lines = 100)
        {
            try
            {
                // TAG: #SECURITY_CRITICAL #PATH_TRAVERSAL_PREVENTION
                // Validate log file path before reading
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string allowedBasePath = Path.Combine(appDataPath, "NecessaryAdminTool");
                string fullLogPath = Path.GetFullPath(LogFile);

                if (!SecurityValidator.IsValidFilePath(fullLogPath, allowedBasePath))
                {
                    return "Error: Invalid log file path";
                }

                if (!File.Exists(LogFile))
                {
                    return "No log file found.";
                }

                // TAG: #ASYNC_OPTIMIZATION - Run file I/O on background thread
                return await Task.Run(() =>
                {
                    var allLines = File.ReadAllLines(LogFile, Encoding.UTF8);
                    var recentLines = allLines.Length > lines
                        ? allLines.Skip(allLines.Length - lines).ToArray()
                        : allLines;

                    return string.Join(Environment.NewLine, recentLines);
                });
            }
            catch (Exception ex)
            {
                return $"Error reading log file: {ex.Message}";
            }
        }

        /// <summary>
        /// Read recent log entries (synchronous wrapper for backward compatibility)
        /// </summary>
        public static string GetRecentLogs(int lines = 100)
        {
            return GetRecentLogsAsync(lines).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Clear all log files asynchronously
        /// TAG: #ASYNC_OPTIMIZATION - Made async to prevent UI blocking
        /// </summary>
        public static async Task ClearAllLogsAsync()
        {
            try
            {
                // TAG: #SECURITY_CRITICAL #PATH_TRAVERSAL_PREVENTION
                // Validate log directory before clearing
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string allowedBasePath = Path.Combine(appDataPath, "NecessaryAdminTool");

                if (!SecurityValidator.IsValidFilePath(LogDirectory, allowedBasePath))
                {
                    LogWarning("[LogManager] ClearAllLogs blocked - invalid log directory path");
                    return;
                }

                // TAG: #ASYNC_OPTIMIZATION - Run file system operations on background thread
                await Task.Run(() =>
                {
                    var logFiles = Directory.GetFiles(LogDirectory, "NAT_*.log");
                    foreach (var logFile in logFiles)
                    {
                        // TAG: #SECURITY_CRITICAL #PATH_TRAVERSAL_PREVENTION
                        // Validate each file path before deletion
                        string fullLogPath = Path.GetFullPath(logFile);
                        if (!SecurityValidator.IsValidFilePath(fullLogPath, LogDirectory))
                        {
                            LogWarning($"[LogManager] Blocked deletion of file outside log directory: {logFile}");
                            continue;
                        }

                        File.Delete(logFile);
                    }
                });

                LogInfo("All log files cleared");
            }
            catch (Exception ex)
            {
                LogError("Failed to clear log files", ex);
            }
        }

        /// <summary>
        /// Clear all log files (synchronous wrapper for backward compatibility)
        /// </summary>
        public static void ClearAllLogs()
        {
            _ = ClearAllLogsAsync();
        }

        /// <summary>
        /// Get the current log file path
        /// </summary>
        public static string GetDebugLogPath()
        {
            return LogFile;
        }

        /// <summary>
        /// Get the runtime log path (alias for GetDebugLogPath for compatibility)
        /// </summary>
        public static string GetRuntimeLogPath()
        {
            return LogFile;
        }
    }
}
