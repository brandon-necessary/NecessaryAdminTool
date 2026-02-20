using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using NecessaryAdminTool.Helpers;

namespace NecessaryAdminTool.Managers
{
    // TAG: #EXTERNAL_TOOLS #PROCESS_TRACKING #TOOL_MANAGEMENT #VERSION_2_4
    /// <summary>
    /// Manages external tool processes (MMC consoles, RMM tools, system utilities)
    /// Tracks running processes, provides launch/close capabilities, and manages credentials
    /// </summary>
    public static class ExternalToolManager
    {
        // TAG: #PROCESS_TRACKING
        private static readonly Dictionary<string, TrackedProcess> _runningTools = new Dictionary<string, TrackedProcess>();
        private static readonly object _lock = new object();

        /// <summary>
        /// Represents a tracked external tool process
        /// </summary>
        private class TrackedProcess
        {
            public Process Process { get; set; }
            public string ToolName { get; set; }
            public string ToolType { get; set; } // "MMC", "RMM", "System", "Remote"
            public DateTime LaunchTime { get; set; }
            public string TargetComputer { get; set; }
        }

        /// <summary>
        /// Event fired when a tool's running status changes
        /// </summary>
        public static event EventHandler<ToolStatusChangedEventArgs> ToolStatusChanged;

        /// <summary>
        /// Optional callback invoked on the UI thread when credential injection is blocked by EDR.
        /// Gives the user a chance to enter alternate credentials and try ProcessStartInfo path.
        /// Return (domain, username, password) tuple, or null to fall back to plain launch.
        /// TAG: #EDR_FALLBACK #CREDENTIALS
        /// </summary>
        public static Func<string, System.Threading.Tasks.Task<(string domain, string username, string password)?>> OnCredentialRequired { get; set; }

        public class ToolStatusChangedEventArgs : EventArgs
        {
            public string ToolName { get; set; }
            public bool IsRunning { get; set; }
            public int? ProcessId { get; set; }
        }

        // ════════════════════════════════════════════════════════════
        // LAUNCH TOOLS
        // TAG: #EXTERNAL_TOOLS #LAUNCH
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Launches an external tool with optional credentials (uses CreateProcessWithLogonW)
        /// TAG: #EXTERNAL_TOOLS #CREDENTIALS
        /// </summary>
        public static async Task<bool> LaunchToolAsync(
            string toolKey,
            string toolName,
            string toolType,
            string executablePath,
            string arguments = "",
            string targetComputer = null,
            string domain = null,
            string username = null,
            SecureString password = null)
        {
            LogManager.LogInfo($"[External Tool Manager] LaunchToolAsync() - START - Tool: {toolName} ({toolType})");
            var sw = Stopwatch.StartNew();

            try
            {
                // Check if already running
                if (IsToolRunning(toolKey))
                {
                    LogManager.LogWarning($"[External Tool Manager] Tool already running: {toolName} (Key: {toolKey})");
                    return false;
                }

                // Validate executable exists before attempting launch
                if (!System.IO.File.Exists(executablePath))
                {
                    LogManager.LogError($"[External Tool Manager] Executable not found: {executablePath}");
                    throw new System.IO.FileNotFoundException($"Tool executable not found: {executablePath}", executablePath);
                }

                Process process = null;

                // Launch with credentials if provided (uses CreateProcessWithLogonW)
                if (!string.IsNullOrEmpty(domain) && !string.IsNullOrEmpty(username) && password != null)
                {
                    LogManager.LogInfo($"[External Tool Manager] Launching with credentials: {domain}\\{username}");
                    try
                    {
                        process = await LaunchWithCredentialsAsync(executablePath, arguments, domain, username, password);
                    }
                    catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 740 || ex.NativeErrorCode == 5)
                    {
                        // TAG: #EDR_FALLBACK - Cortex XDR / EDR is blocking CreateProcessWithLogonW.
                        LogManager.LogWarning($"[External Tool Manager] Credential launch blocked (Error {ex.NativeErrorCode}: {ex.Message})");

                        // Ask user for alternate credentials via callback (runs on UI thread)
                        if (OnCredentialRequired != null)
                        {
                            LogManager.LogInfo("[External Tool Manager] Prompting user for alternate credentials...");
                            var altCreds = await OnCredentialRequired(toolName);
                            if (altCreds.HasValue && !string.IsNullOrEmpty(altCreds.Value.username))
                            {
                                LogManager.LogInfo($"[External Tool Manager] Got alternate credentials - trying ProcessStartInfo path: {altCreds.Value.domain}\\{altCreds.Value.username}");
                                process = await LaunchWithProcessStartInfoCredentialsAsync(
                                    executablePath, arguments,
                                    altCreds.Value.domain, altCreds.Value.username, altCreds.Value.password);
                            }
                        }

                        // If still null (no callback, user cancelled, or ProcessStartInfo also failed) - plain launch
                        if (process == null)
                        {
                            LogManager.LogWarning("[External Tool Manager] Falling back to plain launch (no credential passthrough)...");
                            process = await LaunchWithoutCredentialsAsync(executablePath, arguments);
                        }
                    }
                }
                else
                {
                    // Launch without credentials (current user context)
                    LogManager.LogInfo($"[External Tool Manager] Launching without credentials (current user)");
                    process = await LaunchWithoutCredentialsAsync(executablePath, arguments);
                }

                if (process == null)
                {
                    LogManager.LogError($"[External Tool Manager] Failed to launch tool: {toolName}");
                    return false;
                }

                // Track the process
                lock (_lock)
                {
                    _runningTools[toolKey] = new TrackedProcess
                    {
                        Process = process,
                        ToolName = toolName,
                        ToolType = toolType,
                        LaunchTime = DateTime.Now,
                        TargetComputer = targetComputer
                    };
                }

                // Monitor process exit
                process.EnableRaisingEvents = true;
                process.Exited += (s, e) => OnProcessExited(toolKey, toolName);

                LogManager.LogInfo($"[External Tool Manager] Tool launched successfully: {toolName} (PID: {process.Id}) - Elapsed: {sw.ElapsedMilliseconds}ms");

                // Raise status changed event
                ToolStatusChanged?.Invoke(null, new ToolStatusChangedEventArgs
                {
                    ToolName = toolKey,
                    IsRunning = true,
                    ProcessId = process.Id
                });

                return true;
            }
            catch (Exception ex)
            {
                LogManager.LogError($"[External Tool Manager] LaunchToolAsync() - FAILED - Tool: {toolName}", ex);
                return false;
            }
        }

        /// <summary>
        /// Launches a process with credentials using CreateProcessWithLogonW (EDR bypass)
        /// TAG: #CREDENTIALS #EDR_BYPASS
        /// </summary>
        private static async Task<Process> LaunchWithCredentialsAsync(
            string executablePath,
            string arguments,
            string domain,
            string username,
            SecureString password)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Use CreateProcessWithLogonW with LOGON_NETCREDENTIALS_ONLY (runas /netonly)
                    var si = new Win32Helper.STARTUPINFO();
                    si.cb = Marshal.SizeOf(si);
                    si.dwFlags = Win32Helper.STARTF_USESHOWWINDOW;
                    si.wShowWindow = (short)ProcessWindowStyle.Normal; // Normal window (not embedded)

                    Win32Helper.PROCESS_INFORMATION pi;

                    string commandLine = string.IsNullOrEmpty(arguments)
                        ? executablePath
                        : $"\"{executablePath}\" {arguments}";

                    // TAG: #DIAGNOSTICS - Run pre-launch diagnostic before Win32 call
                    DiagnosePreLaunch(executablePath, arguments, domain, username);

                    LogManager.LogInfo($"[External Tool Manager] CreateProcessWithLogonW - Command: {commandLine}");

                    bool success = Win32Helper.CreateProcessWithLogonW(
                        username,
                        domain,
                        ConvertSecureStringToString(password),
                        Win32Helper.LOGON_NETCREDENTIALS_ONLY, // Use network credentials only (runas /netonly)
                        executablePath,
                        commandLine,
                        0, // No special creation flags
                        IntPtr.Zero,
                        null, // No working directory
                        ref si,
                        out pi
                    );

                    if (!success)
                    {
                        int errorCode = Marshal.GetLastWin32Error();
                        LogManager.LogError($"[External Tool Manager] CreateProcessWithLogonW FAILED - Error Code: {errorCode} (0x{errorCode:X8})");

                        // TAG: #DIAGNOSTICS - Run comprehensive post-failure diagnostics
                        DiagnosePostFailure(errorCode, executablePath);

                        throw new System.ComponentModel.Win32Exception(errorCode,
                            $"CreateProcessWithLogonW failed (Error {errorCode}: {GetWin32ErrorDescription(errorCode)}). " +
                            $"Check the DEBUG LOG for a full diagnostic report including which security software is blocking this.");
                    }

                    LogManager.LogInfo($"[External Tool Manager] Process created successfully (PID: {pi.dwProcessId})");

                    // Get Process object from PID - close handles in finally to prevent leaks
                    Process process = null;
                    try
                    {
                        process = Process.GetProcessById(pi.dwProcessId);
                    }
                    finally
                    {
                        // Always close handles regardless of whether GetProcessById succeeds
                        if (pi.hProcess != IntPtr.Zero) CloseHandle(pi.hProcess);
                        if (pi.hThread != IntPtr.Zero) CloseHandle(pi.hThread);
                    }

                    return process;
                }
                catch (Exception ex)
                {
                    LogManager.LogError($"[External Tool Manager] LaunchWithCredentialsAsync() - FAILED", ex);
                    return null;
                }
            });
        }

        /// <summary>
        /// Launches a process with credentials via ProcessStartInfo (alternate path when CreateProcessWithLogonW is blocked)
        /// TAG: #CREDENTIALS #EDR_FALLBACK
        /// </summary>
        private static async Task<Process> LaunchWithProcessStartInfoCredentialsAsync(
            string executablePath,
            string arguments,
            string domain,
            string username,
            string password)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = executablePath,
                        Arguments = arguments,
                        UseShellExecute = false,
                        Domain = domain,
                        UserName = username,
                        Password = ConvertToSecureString(password),
                        WindowStyle = ProcessWindowStyle.Normal
                    };

                    Process process = Process.Start(startInfo);
                    LogManager.LogInfo($"[External Tool Manager] ProcessStartInfo credential launch succeeded (PID: {process?.Id})");
                    return process;
                }
                catch (Exception ex)
                {
                    LogManager.LogError("[External Tool Manager] LaunchWithProcessStartInfoCredentialsAsync() - FAILED", ex);
                    return null;
                }
            });
        }

        /// <summary>
        /// Launches a process without credentials (current user context)
        /// TAG: #PROCESS_LAUNCH
        /// </summary>
        private static async Task<Process> LaunchWithoutCredentialsAsync(string executablePath, string arguments)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = executablePath,
                        Arguments = arguments,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Normal
                    };

                    Process process = Process.Start(startInfo);
                    LogManager.LogInfo($"[External Tool Manager] Process started (PID: {process.Id})");
                    return process;
                }
                catch (Exception ex)
                {
                    LogManager.LogError($"[External Tool Manager] LaunchWithoutCredentialsAsync() - FAILED", ex);
                    return null;
                }
            });
        }

        // ════════════════════════════════════════════════════════════
        // PROCESS TRACKING
        // TAG: #PROCESS_TRACKING #STATUS_CHECK
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Checks if a tool is currently running
        /// TAG: #STATUS_CHECK
        /// </summary>
        public static bool IsToolRunning(string toolKey)
        {
            lock (_lock)
            {
                if (!_runningTools.ContainsKey(toolKey))
                    return false;

                var tracked = _runningTools[toolKey];

                // Check if process is still alive
                try
                {
                    if (tracked.Process.HasExited)
                    {
                        // Process exited, remove from tracking
                        _runningTools.Remove(toolKey);
                        return false;
                    }
                    return true;
                }
                catch
                {
                    // Process no longer accessible, remove from tracking
                    _runningTools.Remove(toolKey);
                    return false;
                }
            }
        }

        /// <summary>
        /// Gets the Process object for a running tool
        /// TAG: #PROCESS_TRACKING
        /// </summary>
        public static Process GetToolProcess(string toolKey)
        {
            lock (_lock)
            {
                if (_runningTools.ContainsKey(toolKey))
                    return _runningTools[toolKey].Process;
                return null;
            }
        }

        /// <summary>
        /// Gets information about a running tool
        /// TAG: #PROCESS_TRACKING
        /// </summary>
        public static (string toolName, int processId, DateTime launchTime, string targetComputer) GetToolInfo(string toolKey)
        {
            lock (_lock)
            {
                if (_runningTools.ContainsKey(toolKey))
                {
                    var tracked = _runningTools[toolKey];
                    return (tracked.ToolName, tracked.Process.Id, tracked.LaunchTime, tracked.TargetComputer);
                }
                return (null, 0, DateTime.MinValue, null);
            }
        }

        /// <summary>
        /// Gets all running tools of a specific type
        /// TAG: #PROCESS_TRACKING
        /// </summary>
        public static List<string> GetRunningToolsByType(string toolType)
        {
            lock (_lock)
            {
                return _runningTools
                    .Where(kvp => kvp.Value.ToolType == toolType && !kvp.Value.Process.HasExited)
                    .Select(kvp => kvp.Key)
                    .ToList();
            }
        }

        /// <summary>
        /// Gets all running tools
        /// TAG: #PROCESS_TRACKING
        /// </summary>
        public static List<string> GetAllRunningTools()
        {
            lock (_lock)
            {
                return _runningTools
                    .Where(kvp => !kvp.Value.Process.HasExited)
                    .Select(kvp => kvp.Key)
                    .ToList();
            }
        }

        // ════════════════════════════════════════════════════════════
        // FORCE CLOSE
        // TAG: #FORCE_CLOSE #PROCESS_TERMINATION
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Force closes a running tool
        /// TAG: #FORCE_CLOSE
        /// </summary>
        public static bool ForceCloseTool(string toolKey)
        {
            LogManager.LogInfo($"[External Tool Manager] ForceCloseTool() - Tool: {toolKey}");

            lock (_lock)
            {
                if (!_runningTools.ContainsKey(toolKey))
                {
                    LogManager.LogWarning($"[External Tool Manager] Tool not found in running tools: {toolKey}");
                    return false;
                }

                var tracked = _runningTools[toolKey];

                try
                {
                    if (!tracked.Process.HasExited)
                    {
                        LogManager.LogInfo($"[External Tool Manager] Killing process: {tracked.ToolName} (PID: {tracked.Process.Id})");
                        tracked.Process.Kill();
                        tracked.Process.WaitForExit(5000); // Wait up to 5 seconds
                    }

                    _runningTools.Remove(toolKey);
                    LogManager.LogInfo($"[External Tool Manager] Tool closed successfully: {toolKey}");

                    // Raise status changed event
                    ToolStatusChanged?.Invoke(null, new ToolStatusChangedEventArgs
                    {
                        ToolName = toolKey,
                        IsRunning = false,
                        ProcessId = null
                    });

                    return true;
                }
                catch (Exception ex)
                {
                    LogManager.LogError($"[External Tool Manager] ForceCloseTool() - FAILED - Tool: {toolKey}", ex);
                    return false;
                }
            }
        }

        /// <summary>
        /// Force closes all running tools
        /// TAG: #FORCE_CLOSE
        /// </summary>
        public static void ForceCloseAllTools()
        {
            LogManager.LogInfo($"[External Tool Manager] ForceCloseAllTools() - Closing {_runningTools.Count} tools");

            lock (_lock)
            {
                var toolKeys = _runningTools.Keys.ToList();
                foreach (var toolKey in toolKeys)
                {
                    ForceCloseTool(toolKey);
                }
            }
        }

        /// <summary>
        /// Force closes all tools of a specific type
        /// TAG: #FORCE_CLOSE
        /// </summary>
        public static void ForceCloseToolsByType(string toolType)
        {
            LogManager.LogInfo($"[External Tool Manager] ForceCloseToolsByType() - Type: {toolType}");

            lock (_lock)
            {
                var toolKeys = _runningTools
                    .Where(kvp => kvp.Value.ToolType == toolType)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var toolKey in toolKeys)
                {
                    ForceCloseTool(toolKey);
                }
            }
        }

        // ════════════════════════════════════════════════════════════
        // EVENT HANDLERS
        // TAG: #EVENT_HANDLERS
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Handles process exit event
        /// TAG: #EVENT_HANDLERS
        /// </summary>
        private static void OnProcessExited(string toolKey, string toolName)
        {
            LogManager.LogInfo($"[External Tool Manager] Process exited: {toolName} (Key: {toolKey})");

            lock (_lock)
            {
                if (_runningTools.TryGetValue(toolKey, out var tracked))
                {
                    try { tracked.Process?.Dispose(); } catch { }
                    _runningTools.Remove(toolKey);
                }
            }

            // Raise status changed event
            ToolStatusChanged?.Invoke(null, new ToolStatusChangedEventArgs
            {
                ToolName = toolKey,
                IsRunning = false,
                ProcessId = null
            });
        }

        // ════════════════════════════════════════════════════════════
        // DIAGNOSTICS - Blocking Process Detection & Launch Analysis
        // TAG: #DIAGNOSTICS #EDR_DETECTION #TROUBLESHOOTING
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Known EDR/AV process names for blocking detection.
        /// Add to this list when new security software is encountered.
        /// TAG: #DIAGNOSTICS #EDR_DETECTION
        /// </summary>
        private static readonly (string ProcessName, string Vendor)[] KnownSecurityProcesses = new[]
        {
            // Palo Alto Cortex XDR
            ("CortexXDRAgent",       "Palo Alto Cortex XDR"),
            ("cytray",               "Palo Alto Cortex XDR"),
            ("cyserver",             "Palo Alto Cortex XDR"),
            ("pmdaemon",             "Palo Alto Cortex XDR"),
            // CrowdStrike Falcon
            ("CSFalconService",      "CrowdStrike Falcon"),
            ("CSFalconContainer",    "CrowdStrike Falcon"),
            ("CsFalconLocalService", "CrowdStrike Falcon"),
            // VMware Carbon Black
            ("CbDefense",            "VMware Carbon Black"),
            ("RepMgr",               "VMware Carbon Black"),
            ("CarbonBlackCS",        "VMware Carbon Black"),
            // SentinelOne
            ("SentinelAgent",        "SentinelOne"),
            ("SentinelServiceHost",  "SentinelOne"),
            ("SentinelStaticEngine", "SentinelOne"),
            // Cylance / BlackBerry
            ("CylanceSvc",           "Cylance (BlackBerry)"),
            ("CylanceUI",            "Cylance (BlackBerry)"),
            // Sophos
            ("SophosSafestore64",    "Sophos"),
            ("SophosNtpService",     "Sophos"),
            ("SophosAgent",          "Sophos"),
            ("SophosUI",             "Sophos"),
            // Microsoft Defender / MDE
            ("MsMpEng",              "Microsoft Defender / MDE"),
            ("MpCmdRun",             "Microsoft Defender / MDE"),
            ("SecurityHealthService","Microsoft Defender / MDE"),
            ("WdNisSvc",             "Microsoft Defender / MDE"),
            // McAfee / Trellix
            ("MfeAVSvc",             "McAfee / Trellix"),
            ("McAPExe",              "McAfee / Trellix"),
            ("McShield",             "McAfee / Trellix"),
            // Symantec / Broadcom
            ("ccSvcHst",             "Symantec / Broadcom"),
            ("SEPMasterService",     "Symantec / Broadcom"),
            // Trend Micro
            ("PccNTMon",             "Trend Micro"),
            ("TmListen",             "Trend Micro"),
            ("coreServiceShell",     "Trend Micro"),
            // ESET
            ("ekrn",                 "ESET"),
            ("egui",                 "ESET"),
            // Bitdefender
            ("bdagent",              "Bitdefender"),
            ("vsserv",               "Bitdefender"),
            // Kaspersky
            ("avp",                  "Kaspersky"),
            // Webroot
            ("WRSA",                 "Webroot"),
            // Malwarebytes
            ("MBAMService",          "Malwarebytes"),
            // Deep Instinct
            ("DISService",           "Deep Instinct"),
        };

        /// <summary>
        /// Logs a comprehensive pre-launch diagnostic report: file check, current user context,
        /// UAC elevation state, and all running security/EDR software.
        /// TAG: #DIAGNOSTICS #PRE_LAUNCH
        /// </summary>
        private static void DiagnosePreLaunch(string executablePath, string arguments, string domain, string username)
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("╔══════════════════════════════════════════════════════════╗");
            sb.AppendLine("║         PRE-LAUNCH DIAGNOSTIC REPORT                    ║");
            sb.AppendLine("╚══════════════════════════════════════════════════════════╝");

            // 1. Launch target
            sb.AppendLine();
            sb.AppendLine("▶ LAUNCH TARGET:");
            sb.AppendLine($"   Executable  : {executablePath}");
            sb.AppendLine($"   Arguments   : {(string.IsNullOrEmpty(arguments) ? "(none)" : arguments)}");
            sb.AppendLine($"   Domain      : {(string.IsNullOrEmpty(domain) ? "(none - current user)" : domain)}");
            sb.AppendLine($"   Username    : {(string.IsNullOrEmpty(username) ? "(current user)" : username)}");
            sb.AppendLine($"   Win32 API   : CreateProcessWithLogonW (advapi32.dll) with LOGON_NETCREDENTIALS_ONLY");
            sb.AppendLine($"   Our EXE     : {System.Reflection.Assembly.GetExecutingAssembly().Location}");

            // 2. Executable file check
            sb.AppendLine();
            sb.AppendLine("▶ EXECUTABLE FILE CHECK:");
            try
            {
                bool exists = File.Exists(executablePath);
                sb.AppendLine($"   File exists    : {(exists ? "✅ YES" : "❌ NO - file not found!")}");
                if (exists)
                {
                    var fi = new FileInfo(executablePath);
                    sb.AppendLine($"   File size      : {fi.Length:N0} bytes");
                    sb.AppendLine($"   Last modified  : {fi.LastWriteTime:yyyy-MM-dd HH:mm:ss}");
                    try
                    {
                        using (File.OpenRead(executablePath)) { }
                        sb.AppendLine("   Read access    : ✅ Yes");
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine($"   Read access    : ❌ DENIED ({ex.Message})");
                    }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"   ⚠️ File check error: {ex.Message}");
            }

            // 3. Current process context
            sb.AppendLine();
            sb.AppendLine("▶ CURRENT PROCESS CONTEXT:");
            try
            {
                var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                bool isElevated = Win32Helper.IsProcessElevated();
                bool isAdmin = principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
                var proc = Process.GetCurrentProcess();

                sb.AppendLine($"   Running as     : {identity.Name}");
                sb.AppendLine($"   Auth type      : {identity.AuthenticationType}");
                sb.AppendLine($"   In Admin group : {(isAdmin ? "Yes" : "No")}");
                sb.AppendLine($"   UAC elevated   : {(isElevated ? "⚠️ YES - elevation BREAKS credential passthrough to child processes" : "✅ NO (normal user - credentials should pass through)")}");
                sb.AppendLine($"   Our PID        : {proc.Id}");
                sb.AppendLine($"   Our process    : {proc.ProcessName}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"   ⚠️ Context check error: {ex.Message}");
            }

            // 4. Running security software
            sb.AppendLine();
            sb.Append(ScanForSecuritySoftware());

            LogManager.LogInfo(sb.ToString());
        }

        /// <summary>
        /// Scans running processes for known EDR/AV software.
        /// Returns a formatted diagnostic block.
        /// TAG: #DIAGNOSTICS #EDR_DETECTION
        /// </summary>
        private static string ScanForSecuritySoftware()
        {
            var sb = new StringBuilder();
            sb.AppendLine("▶ SECURITY SOFTWARE SCAN:");

            var found = new List<(string processName, int pid, string vendor)>();

            try
            {
                var allProcesses = Process.GetProcesses();
                foreach (var proc in allProcesses)
                {
                    try
                    {
                        string procName = proc.ProcessName;
                        foreach (var (knownName, vendor) in KnownSecurityProcesses)
                        {
                            if (string.Equals(procName, knownName, StringComparison.OrdinalIgnoreCase))
                            {
                                found.Add((procName, proc.Id, vendor));
                                break;
                            }
                        }
                    }
                    catch { /* process may have exited */ }
                }

                if (found.Count == 0)
                {
                    sb.AppendLine("   ✅ No known EDR/AV processes detected");
                }
                else
                {
                    sb.AppendLine($"   ⚠️ {found.Count} security process(es) found - one of these is likely blocking the launch:");
                    foreach (var (procName, pid, vendor) in found)
                        sb.AppendLine($"      🛡️ [{vendor}] {procName}.exe (PID: {pid})");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"   ⚠️ Could not enumerate processes: {ex.Message}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Logs a comprehensive post-failure diagnostic: error analysis, likely cause,
        /// fix recommendations, and recent Windows Event Log entries.
        /// TAG: #DIAGNOSTICS #POST_FAILURE
        /// </summary>
        private static void DiagnosePostFailure(int errorCode, string executablePath)
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("╔══════════════════════════════════════════════════════════╗");
            sb.AppendLine("║         POST-FAILURE DIAGNOSTIC REPORT                  ║");
            sb.AppendLine("╚══════════════════════════════════════════════════════════╝");

            // Error analysis
            sb.AppendLine();
            sb.AppendLine("▶ ERROR ANALYSIS:");
            sb.AppendLine($"   Error Code       : {errorCode} (0x{errorCode:X8})");
            sb.AppendLine($"   Description      : {GetWin32ErrorDescription(errorCode)}");
            sb.AppendLine($"   Likely Cause     : {GetErrorLikelyCause(errorCode)}");
            sb.AppendLine($"   Recommended Fix  :");
            foreach (var line in GetErrorRecommendedFix(errorCode).Split('\n'))
                sb.AppendLine($"      {line.TrimStart()}");

            // Security software at time of failure
            sb.AppendLine();
            sb.Append(ScanForSecuritySoftware());

            // Recent Application event log errors (last 90 seconds)
            sb.AppendLine();
            sb.AppendLine("▶ RECENT APPLICATION EVENT LOG (last 90 sec, errors only):");
            ReadRecentEventLogErrors("Application", 90, sb);

            // Recent System event log errors (last 90 seconds)
            sb.AppendLine();
            sb.AppendLine("▶ RECENT SYSTEM EVENT LOG (last 90 sec, errors only):");
            ReadRecentEventLogErrors("System", 90, sb);

            sb.AppendLine();
            sb.AppendLine("   ℹ️  Full logs: %APPDATA%\\NecessaryAdminTool\\Logs\\");
            sb.AppendLine("   ℹ️  Event Viewer: eventvwr.msc → Windows Logs → Application / Security");
            sb.AppendLine("════════════════════════════════════════════════════════════");

            LogManager.LogError(sb.ToString());
        }

        /// <summary>
        /// Reads recent errors from a Windows Event Log and appends them to the StringBuilder.
        /// TAG: #DIAGNOSTICS #EVENT_LOG
        /// </summary>
        private static void ReadRecentEventLogErrors(string logName, int seconds, StringBuilder sb)
        {
            try
            {
                var cutoff = DateTime.Now.AddSeconds(-seconds);
                var log = new EventLog(logName);
                int total = log.Entries.Count;
                int startIdx = Math.Max(0, total - 200); // scan last 200 entries
                int found = 0;

                for (int i = startIdx; i < total; i++)
                {
                    try
                    {
                        var entry = log.Entries[i];
                        if (entry.TimeGenerated < cutoff) continue;
                        if (entry.EntryType != EventLogEntryType.Error &&
                            entry.EntryType != EventLogEntryType.Warning) continue;

                        string msg = entry.Message ?? string.Empty;
                        if (msg.Length > 250) msg = msg.Substring(0, 250) + "...";
                        msg = msg.Replace("\r\n", " ").Replace("\n", " ");

                        sb.AppendLine($"   [{entry.TimeGenerated:HH:mm:ss}] [{entry.EntryType,-7}] " +
                                      $"Source={entry.Source}, EventID={entry.InstanceId}");
                        sb.AppendLine($"      {msg}");
                        found++;
                    }
                    catch { /* individual entry may be unreadable */ }
                }

                if (found == 0)
                    sb.AppendLine($"   (no errors/warnings in the last {seconds} seconds)");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"   ⚠️ Could not read {logName} event log: {ex.Message}");
                sb.AppendLine("      (reading Security log requires admin privileges)");
            }
        }

        /// <summary>
        /// Returns a human-readable description of a Win32 error code.
        /// TAG: #DIAGNOSTICS #ERROR_CODES
        /// </summary>
        private static string GetWin32ErrorDescription(int errorCode)
        {
            switch (errorCode)
            {
                case 5:    return "ERROR_ACCESS_DENIED - The OS or security software denied the request";
                case 87:   return "ERROR_INVALID_PARAMETER - One or more parameters are invalid";
                case 740:  return "ERROR_ELEVATION_REQUIRED - UAC elevation required OR blocked by EDR/AV";
                case 1314: return "ERROR_PRIVILEGE_NOT_HELD - Required privilege not held (SeAssignPrimaryTokenPrivilege)";
                case 1326: return "ERROR_LOGON_FAILURE - Unknown username or bad password";
                case 1327: return "ERROR_ACCOUNT_RESTRICTION - Account restrictions prevent this logon type";
                case 1328: return "ERROR_INVALID_LOGON_HOURS - Account restricted to specific logon hours";
                case 1329: return "ERROR_INVALID_WORKSTATION - User not allowed to log on to this computer";
                case 1330: return "ERROR_PASSWORD_EXPIRED - The user's password has expired";
                case 1331: return "ERROR_ACCOUNT_DISABLED - The account is currently disabled";
                case 1385: return "ERROR_LOGON_TYPE_NOT_GRANTED - Logon type not granted (network-only logon may be restricted)";
                case 1793: return "ERROR_ACCOUNT_EXPIRED - The user's account has expired";
                case 2202: return "ERROR_BAD_USERNAME - The specified username is invalid";
                default:   return $"Unknown error - see https://docs.microsoft.com/windows/win32/debug/system-error-codes (code {errorCode})";
            }
        }

        /// <summary>
        /// Returns the likely cause for a Win32 error during CreateProcessWithLogonW.
        /// TAG: #DIAGNOSTICS #ERROR_CODES
        /// </summary>
        private static string GetErrorLikelyCause(int errorCode)
        {
            switch (errorCode)
            {
                case 5:
                case 740:
                    return "EDR/AV software (Cortex XDR, CrowdStrike, SentinelOne, etc.) is intercepting " +
                           "CreateProcessWithLogonW and blocking it. This is the most common cause in enterprise environments.";
                case 1326:
                    return "Wrong username or password, or the domain name is incorrect. " +
                           "Verify credentials by logging into a domain machine manually.";
                case 1314:
                    return "The current process token is missing SeAssignPrimaryTokenPrivilege or SeIncreaseQuotaPrivilege. " +
                           "This can happen when running elevated (Run as Administrator).";
                case 1385:
                    return "LOGON_NETCREDENTIALS_ONLY (runas /netonly) is not permitted for this account or this workstation. " +
                           "May be restricted by Group Policy.";
                default:
                    return "See Windows documentation for this error code.";
            }
        }

        /// <summary>
        /// Returns recommended fix steps for a Win32 error during CreateProcessWithLogonW.
        /// TAG: #DIAGNOSTICS #ERROR_CODES
        /// </summary>
        private static string GetErrorRecommendedFix(int errorCode)
        {
            switch (errorCode)
            {
                case 5:
                case 740:
                    return "1) Add a Disable Prevention Rule in Cortex XDR portal:\n" +
                           "      Path: \\*NecessaryAdminTool.exe\n" +
                           "      Modules: Credential Gathering Protection + Behavioral Threat Protection\n" +
                           "   2) Assign the exception to a policy targeting this endpoint\n" +
                           "   3) Force policy sync: Cortex console → right-click endpoint → Check In\n" +
                           "      OR: Restart-Service CyServer (no full PC restart needed)\n" +
                           "   4) Verify in Cortex console: endpoint Policy Status = Applied\n" +
                           "   5) If policy shows Applied but still blocked: check for a higher-priority\n" +
                           "      conflicting policy on this endpoint that overrides the exception\n" +
                           "   6) Cortex XDR v8.3: Use Disable Prevention Rules (NOT Operational Agent\n" +
                           "      Exceptions - those require v8.7+)";
                case 1326:
                    return "1) Verify username format: type ONLY 'username' (no domain\\ prefix)\n" +
                           "   2) Verify password hasn't expired\n" +
                           "   3) Try authenticating at a domain machine to confirm credentials work";
                case 1314:
                    return "1) Do NOT run NecessaryAdminTool.exe as Administrator\n" +
                           "   2) Run as your normal domain user and authenticate via the login dialog";
                default:
                    return "Check Windows Event Viewer for more details: eventvwr.msc → Windows Logs";
            }
        }

        // ════════════════════════════════════════════════════════════
        // HELPER METHODS
        // TAG: #HELPER_METHODS
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Converts a plain string to SecureString
        /// TAG: #CREDENTIALS #HELPER_METHODS
        /// </summary>
        private static SecureString ConvertToSecureString(string plain)
        {
            var ss = new SecureString();
            if (!string.IsNullOrEmpty(plain))
                foreach (char c in plain)
                    ss.AppendChar(c);
            ss.MakeReadOnly();
            return ss;
        }

        /// <summary>
        /// Converts SecureString to plain string (for Win32 API calls)
        /// TAG: #CREDENTIALS #HELPER_METHODS
        /// </summary>
        private static string ConvertSecureStringToString(SecureString secureString)
        {
            if (secureString == null)
                return string.Empty;

            IntPtr ptr = IntPtr.Zero;
            try
            {
                ptr = Marshal.SecureStringToGlobalAllocUnicode(secureString);
                return Marshal.PtrToStringUni(ptr);
            }
            finally
            {
                if (ptr != IntPtr.Zero)
                    Marshal.ZeroFreeGlobalAllocUnicode(ptr);
            }
        }

        /// <summary>
        /// Closes a Win32 handle
        /// TAG: #WIN32_API
        /// </summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);
    }
}
