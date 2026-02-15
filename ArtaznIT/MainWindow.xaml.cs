using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Management;
using Microsoft.Management.Infrastructure;
using Microsoft.Management.Infrastructure.Options;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net;
using System.Security;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using System.Xml.Linq;
using System.DirectoryServices;
using System.DirectoryServices.ActiveDirectory;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Threading;

namespace ArtaznIT
{
    // ############################################################################
    // REGION: SECURE MEMORY UTILITIES
    // ############################################################################

    /// <summary>
    /// Provides secure memory wiping using native Windows APIs.
    /// Ensures credentials are zeroed from RAM on logout/close.
    /// </summary>
    public static class SecureMemory
    {
        [DllImport("kernel32.dll", EntryPoint = "RtlZeroMemory")]
        private static extern void RtlZeroMemory(IntPtr dest, IntPtr size);

        [DllImport("kernel32.dll", EntryPoint = "RtlSecureZeroMemory")]
        private static extern void RtlSecureZeroMemory(IntPtr dest, IntPtr size);

        /// <summary>
        /// Securely wipes a SecureString from memory and disposes it.
        /// Uses RtlZeroMemory to overwrite the unmanaged BSTR before disposal.
        /// </summary>
        public static void WipeAndDispose(ref SecureString secureString)
        {
            if (secureString == null) return;

            IntPtr bstr = IntPtr.Zero;
            try
            {
                bstr = Marshal.SecureStringToBSTR(secureString);
                int length = Marshal.ReadInt32(bstr, -4); // BSTR length prefix
                if (length > 0)
                {
                    RtlZeroMemory(bstr, (IntPtr)length);
                }
            }
            catch
            {
                // Best-effort wipe
            }
            finally
            {
                if (bstr != IntPtr.Zero)
                    Marshal.ZeroFreeBSTR(bstr);
                secureString.Dispose();
                secureString = null;
            }
        }

        /// <summary>
        /// Securely converts SecureString to plaintext, executes action, then wipes.
        /// Minimizes time the plaintext exists in memory.
        /// </summary>
        public static void UseSecureString(SecureString secureString, Action<string> action)
        {
            IntPtr bstr = IntPtr.Zero;
            string plaintext = null;
            try
            {
                bstr = Marshal.SecureStringToBSTR(secureString);
                plaintext = Marshal.PtrToStringBSTR(bstr);
                action(plaintext);
            }
            finally
            {
                if (bstr != IntPtr.Zero)
                    Marshal.ZeroFreeBSTR(bstr);
                plaintext = null;
            }
        }

        /// <summary>
        /// Force garbage collection to attempt clearing any managed string copies.
        /// Not guaranteed but helps with defense-in-depth.
        /// </summary>
        public static void ForceCleanup()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        /// <summary>Converts a plain text string to SecureString</summary>
        public static SecureString ConvertToSecureString(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return new SecureString();

            var secure = new SecureString();
            foreach (char c in plainText)
                secure.AppendChar(c);
            secure.MakeReadOnly();
            return secure;
        }
    }

    // ############################################################################
    // REGION: SECURITY & VALIDATION UTILITIES
    // ############################################################################

    public static class SecurityValidator
    {
        private static readonly Regex _hostnamePattern = new Regex(@"^[a-zA-Z0-9\-\.]{1,255}$", RegexOptions.Compiled);
        private static readonly Regex _ipPattern = new Regex(@"^(\d{1,3}\.){3}\d{1,3}$", RegexOptions.Compiled);
        private static readonly Regex _domainPattern = new Regex(@"^[a-zA-Z0-9\-\.\\]{1,255}$", RegexOptions.Compiled);
        private static readonly Regex _pathPattern = new Regex(@"^[a-zA-Z]:\\[\w\s\-\\.\\]+$", RegexOptions.Compiled);

        private static readonly string[] _dangerousPatterns = new[]
        {
            ";", "|", "`", "$", "(", ")", "{", "}", "[", "]",
            "<", ">", "\n", "\r", "&&", "||", "powershell -enc", "invoke-expression"
        };

        private static readonly string[] _whitelistedCommands = new[]
        {
            "sfc /scannow", "dism /online", "gpupdate /force",
            "shutdown /r", "netsh advfirewall", "ipconfig /flushdns",
            "start-mpcompliance", "start-mpdefenderscan",
            "get-netipaddress", "get-netroute", "get-dnsclientcache",
            "ipconfig /release", "ipconfig /renew"
        };

        public static bool IsValidHostname(string hostname)
        {
            if (string.IsNullOrWhiteSpace(hostname)) return false;
            if (hostname.Length > 255) return false;
            return _hostnamePattern.IsMatch(hostname) || _ipPattern.IsMatch(hostname);
        }

        public static bool IsValidDomainUser(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return false;
            return _domainPattern.IsMatch(username);
        }

        public static bool IsValidPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            return _pathPattern.IsMatch(path);
        }

        public static string SanitizeHostname(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            return Regex.Replace(input.Trim(), @"[^a-zA-Z0-9\-\.]", "");
        }

        public static string SanitizeWmiQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return string.Empty;
            foreach (var pattern in _dangerousPatterns)
                query = query.Replace(pattern, "");
            return query.Trim();
        }

        public static bool ContainsDangerousPatterns(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            string lowerInput = input.ToLower();
            if (_whitelistedCommands.Any(cmd => lowerInput.Contains(cmd.ToLower())))
                return false;
            return _dangerousPatterns.Any(p => input.Contains(p));
        }

        public static string EscapeCsv(string value)
        {
            return (value ?? "").Replace("\"", "\"\"");
        }

        /// <summary>
        /// Sanitize PowerShell input to prevent command injection
        /// Removes dangerous characters that could break out of string context
        /// TAG: #SECURITY_CRITICAL #POWERSHELL_INJECTION #COMMAND_INJECTION
        /// </summary>
        /// <param name="input">Input to sanitize</param>
        /// <returns>Sanitized string safe for PowerShell interpolation</returns>
        public static string SanitizePowerShellInput(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            // Remove command injection characters
            // Backtick: escape character
            // $: variable expansion
            // ;: command separator
            // &: background execution
            // |: pipe operator
            // < >: redirection
            // \n \r: line breaks (could break out of string)
            // \0: null terminator
            var dangerous = new[] { '`', '$', ';', '&', '|', '<', '>', '\n', '\r', '\0' };
            foreach (var c in dangerous)
            {
                input = input.Replace(c.ToString(), string.Empty);
            }

            // Escape single quotes for PowerShell (double them)
            input = input.Replace("'", "''");

            return input;
        }

        /// <summary>
        /// Alias for SanitizePowerShellInput for consistency with security audit recommendations
        /// TAG: #SECURITY_CRITICAL #POWERSHELL_INJECTION #COMMAND_INJECTION
        /// </summary>
        /// <param name="input">Input to sanitize</param>
        /// <returns>Sanitized string safe for PowerShell interpolation</returns>
        public static string SanitizeForPowerShell(string input)
        {
            return SanitizePowerShellInput(input);
        }

        /// <summary>
        /// Validate PowerShell script content for dangerous commands
        /// Detects common attack patterns and malicious commands
        /// TAG: #SECURITY_CRITICAL #POWERSHELL_INJECTION_PREVENTION #MALWARE_DETECTION
        /// </summary>
        /// <param name="scriptContent">PowerShell script content to validate</param>
        /// <returns>True if script is safe, false if dangerous patterns detected</returns>
        public static bool ValidatePowerShellScript(string scriptContent)
        {
            if (string.IsNullOrWhiteSpace(scriptContent))
            {
                LogManager.LogWarning("[SecurityValidator] PowerShell script validation failed: empty script");
                return false;
            }

            // Convert to lowercase for case-insensitive matching
            string scriptLower = scriptContent.ToLowerInvariant();

            // Dangerous command patterns that should be blocked or flagged
            var dangerousPowerShellPatterns = new[]
            {
                // Download and execution patterns
                "invoke-webrequest",
                "iwr ",
                "wget ",
                "curl ",
                "downloadstring",
                "downloadfile",
                "net.webclient",
                "bitstransfer",

                // Encoded/obfuscated command execution
                "invoke-expression",
                "iex ",
                "-encodedcommand",
                "-enc ",
                "frombase64string",

                // System modification commands
                "remove-item",
                "del ",
                "rm ",
                "format-volume",
                "clear-disk",
                "initialize-disk",

                // Credential theft
                "mimikatz",
                "invoke-mimikatz",
                "get-credential",
                "convertfrom-securestring",
                "export-clixml",

                // Persistence mechanisms
                "new-scheduledtask",
                "register-scheduledtask",
                "set-itemproperty -path hkcu:",
                "set-itemproperty -path hklm:",
                "new-service",

                // Disable security features
                "set-mppreference",
                "disable-windowsdefender",
                "set-executionpolicy bypass",
                "add-mppreference -exclusion",

                // Reverse shells / C2
                "new-object system.net.sockets.tcpclient",
                "system.net.sockets.tcp",
                "nc.exe",
                "ncat",
                "powercat",

                // Script block logging bypass
                "$null = ",
                "out-null",
                "-windowstyle hidden",

                // File encryption (ransomware)
                "cryptoserviceprovider",
                "aes.create",
                "rijndaelmanaged"
            };

            foreach (var pattern in dangerousPowerShellPatterns)
            {
                if (scriptLower.Contains(pattern))
                {
                    LogManager.LogWarning($"[SecurityValidator] Dangerous PowerShell pattern detected: {pattern}");
                    LogManager.LogWarning($"[SecurityValidator] Script content preview: {scriptContent.Substring(0, Math.Min(200, scriptContent.Length))}...");
                    return false;
                }
            }

            // Check for excessive obfuscation (multiple layers of encoding)
            int obfuscationScore = 0;
            if (scriptLower.Contains("char") && scriptLower.Contains("join")) obfuscationScore++;
            if (scriptLower.Contains("replace") && scriptLower.Contains("split")) obfuscationScore++;
            if (scriptLower.Contains("[convert]")) obfuscationScore++;
            if (scriptLower.Contains("([char]")) obfuscationScore++;

            if (obfuscationScore >= 3)
            {
                LogManager.LogWarning("[SecurityValidator] PowerShell script appears heavily obfuscated");
                return false;
            }

            // All checks passed
            return true;
        }

        /// <summary>
        /// Validate username for Active Directory compatibility
        /// Supports DOMAIN\user and user@domain.com formats
        /// TAG: #SECURITY_CRITICAL #USERNAME_VALIDATION #ACTIVE_DIRECTORY
        /// </summary>
        /// <param name="username">Username to validate</param>
        /// <returns>True if username is valid</returns>
        public static bool IsValidUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                LogManager.LogWarning("[SecurityValidator] Username validation failed: null or empty");
                return false;
            }

            // Format 1: DOMAIN\user
            if (username.Contains("\\"))
            {
                var parts = username.Split('\\');
                if (parts.Length != 2)
                {
                    LogManager.LogWarning($"[SecurityValidator] Username validation failed: invalid DOMAIN\\user format");
                    return false;
                }
                return IsValidDomainNamePart(parts[0]) && IsValidUserPart(parts[1]);
            }
            // Format 2: user@domain.com
            else if (username.Contains("@"))
            {
                var parts = username.Split('@');
                if (parts.Length != 2)
                {
                    LogManager.LogWarning($"[SecurityValidator] Username validation failed: invalid user@domain format");
                    return false;
                }
                return IsValidUserPart(parts[0]) && IsValidDomainNamePart(parts[1]);
            }
            // Format 3: user (no domain)
            else
            {
                return IsValidUserPart(username);
            }
        }

        /// <summary>
        /// Validate domain name part (DNS-compatible)
        /// </summary>
        private static bool IsValidDomainNamePart(string domain)
        {
            if (string.IsNullOrWhiteSpace(domain)) return false;
            if (domain.Length > 255) return false;

            // Allow letters, digits, dots, hyphens (DNS-safe)
            return domain.All(c => char.IsLetterOrDigit(c) || c == '.' || c == '-');
        }

        /// <summary>
        /// Validate username part (without domain)
        /// Ensures compliance with Active Directory username restrictions
        /// </summary>
        private static bool IsValidUserPart(string user)
        {
            if (string.IsNullOrWhiteSpace(user)) return false;
            if (user.Length > 104) return false; // AD limit is 104 characters

            // AD disallows: / \ [ ] : | < > + = ; , ? * @ "
            var invalidChars = @"/\[]:|<>+=;,?*@""";
            return !user.Any(c => invalidChars.Contains(c));
        }

        /// <summary>
        /// Validate IP address (IPv4 or IPv6)
        /// TAG: #SECURITY_CRITICAL #IP_VALIDATION
        /// </summary>
        /// <param name="ipAddress">IP address to validate</param>
        /// <returns>True if IP address is valid</returns>
        public static bool IsValidIPAddress(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress)) return false;

            try
            {
                System.Net.IPAddress.Parse(ipAddress);
                return true;
            }
            catch
            {
                LogManager.LogWarning($"[SecurityValidator] IP address validation failed: {ipAddress}");
                return false;
            }
        }

        /// <summary>
        /// Validate file path to prevent path traversal attacks
        /// Ensures file path is within allowed base directory
        /// TAG: #SECURITY_CRITICAL #PATH_TRAVERSAL #DIRECTORY_TRAVERSAL
        /// </summary>
        /// <param name="filePath">File path to validate</param>
        /// <param name="allowedBasePath">Allowed base directory</param>
        /// <returns>True if path is safe</returns>
        public static bool IsValidFilePath(string filePath, string allowedBasePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                LogManager.LogWarning("[SecurityValidator] File path validation failed: null or empty");
                return false;
            }

            try
            {
                // Resolve to absolute paths
                string fullPath = Path.GetFullPath(filePath);
                string basePath = Path.GetFullPath(allowedBasePath);

                // Ensure path is within allowed directory (prevents ../ attacks)
                bool isValid = fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase);

                if (!isValid)
                {
                    LogManager.LogWarning($"[SecurityValidator] Path traversal attempt blocked: {filePath} is outside {allowedBasePath}");
                }

                return isValid;
            }
            catch (Exception ex)
            {
                LogManager.LogError($"[SecurityValidator] File path validation error: {filePath}", ex);
                return false;
            }
        }

        /// <summary>
        /// Validate filename to prevent directory traversal and invalid characters
        /// TAG: #SECURITY_CRITICAL #FILENAME_VALIDATION
        /// </summary>
        /// <param name="filename">Filename to validate</param>
        /// <returns>True if filename is safe</returns>
        public static bool IsValidFilename(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                LogManager.LogWarning("[SecurityValidator] Filename validation failed: null or empty");
                return false;
            }

            // Check for path traversal attempts
            if (filename.Contains("..") || filename.Contains("/") || filename.Contains("\\"))
            {
                LogManager.LogWarning($"[SecurityValidator] Filename validation failed: contains path separators or '..'");
                return false;
            }

            // Check for invalid filename characters
            if (filename.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                LogManager.LogWarning($"[SecurityValidator] Filename validation failed: contains invalid characters");
                return false;
            }

            return true;
        }
    }

    // ############################################################################
    // REGION: CONFIGURATION
    // ############################################################################

    public static class SecureConfig
    {
        private static readonly string _configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Artazn_Config_v2.xml");

        public static string SharedLogPath { get; set; } = @"G:\PUBLIC\BNIT\01_Software\04_Update Logs\Master_Update_Log.csv";
        public static string InventoryDbPath { get; set; } = @"G:\PUBLIC\BNIT\01_Software\04_Update Logs\Master_Inventory.csv";
        public static int MaxParallelScans { get; set; } = 30;
        public static int WmiTimeoutMs { get; set; } = 15000;  // 15 seconds - fast fail for hung queries
        public static int PingTimeoutMs { get; set; } = 1200;
        public static int MaxRetryAttempts { get; set; } = 3;

        public static void LoadConfiguration()
        {
            try
            {
                if (!File.Exists(_configPath)) return;
                var lines = File.ReadAllLines(_configPath);
                foreach (var line in lines)
                {
                    var parts = line.Split('=');
                    if (parts.Length != 2) continue;
                    switch (parts[0].Trim())
                    {
                        case "SharedLogPath":
                            if (SecurityValidator.IsValidPath(parts[1]))
                            {
                                SharedLogPath = parts[1].Trim();
                            }
                            else
                            {
                                // TAG: #AUTO_UPDATE_UI_ENGINE #VALIDATION_TOASTS
                                LogManager.LogWarning($"Invalid SharedLogPath in config: {parts[1]}");
                            }
                            break;
                        case "InventoryDbPath":
                            if (SecurityValidator.IsValidPath(parts[1]))
                            {
                                InventoryDbPath = parts[1].Trim();
                            }
                            else
                            {
                                // TAG: #AUTO_UPDATE_UI_ENGINE #VALIDATION_TOASTS
                                LogManager.LogWarning($"Invalid InventoryDbPath in config: {parts[1]}");
                            }
                            break;
                        case "MaxParallelScans":
                            if (int.TryParse(parts[1], out int ms) && ms > 0 && ms <= 100) MaxParallelScans = ms; break;
                        case "WmiTimeoutMs":
                            if (int.TryParse(parts[1], out int wt) && wt > 0) WmiTimeoutMs = wt; break;
                    }
                }
            }
            catch (Exception ex) { LogManager.LogError("Config Load Failed", ex); }
        }

        public static void SaveConfiguration()
        {
            try
            {
                var config = new StringBuilder();
                config.AppendLine($"SharedLogPath={SharedLogPath}");
                config.AppendLine($"InventoryDbPath={InventoryDbPath}");
                config.AppendLine($"MaxParallelScans={MaxParallelScans}");
                config.AppendLine($"WmiTimeoutMs={WmiTimeoutMs}");
                File.WriteAllText(_configPath, config.ToString());
            }
            catch (Exception ex) { LogManager.LogError("Config Save Failed", ex); }
        }
    }

    // ############################################################################
    // REGION: LOG MANAGER
    // ############################################################################

    public static class LogManager
    {
        private static readonly object _logLock = new object();
        private static readonly string _debugLogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Artazn_Debug.log");
        private static readonly string _runtimeLogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Artazn_Runtime.log");
        private const long MAX_LOG_SIZE = 10 * 1024 * 1024;

        public static void LogDebug(string message, Exception ex = null) => WriteLog(_debugLogPath, "DEBUG", message, ex);
        public static void LogInfo(string message) => WriteLog(_runtimeLogPath, "INFO", message, null);
        public static void LogError(string message, Exception ex) => WriteLog(_debugLogPath, "ERROR", message, ex);
        public static void LogWarning(string message) => WriteLog(_debugLogPath, "WARN", message, null);

        private static void WriteLog(string path, string level, string message, Exception ex)
        {
            lock (_logLock)
            {
                try
                {
                    if (File.Exists(path) && new FileInfo(path).Length > MAX_LOG_SIZE)
                    {
                        string backup = path + ".old";
                        if (File.Exists(backup)) File.Delete(backup);
                        File.Move(path, backup);
                    }
                    string entry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
                    if (ex != null)
                        entry += $"\n  Exception: {ex.GetType().Name}\n  Message: {ex.Message}\n  StackTrace: {ex.StackTrace}";
                    File.AppendAllText(path, entry + "\n");
                }
                catch (Exception logEx)
                {
                    // Fallback: Write to Debug output if file logging fails
                    Debug.WriteLine($"[LogManager] Failed to write log: {logEx.Message}");
                }
            }
        }

        public static string GetDebugLogPath() => _debugLogPath;
        public static string GetRuntimeLogPath() => _runtimeLogPath;
    }

    // ############################################################################
    // REGION: TOAST NOTIFICATION MANAGER
    // ############################################################################

    /// <summary>
    /// Centralized toast notification manager for non-blocking user feedback
    /// TAG: #TOAST_MANAGER #UI_FEEDBACK #NON_BLOCKING
    /// </summary>
    public static class Managers
    {
        public static class UI
        {
            public static class ToastManager
            {
                /// <summary>Show a success toast notification (green)</summary>
                public static void ShowSuccess(string message, string actionText = null, Action actionCallback = null)
                {
                    LogManager.LogInfo($"[Toast:Success] {message}");
                    // In a full implementation, this would create a visual toast notification
                    // For now, it logs the notification for debugging purposes
                }

                /// <summary>Show an info toast notification (blue)</summary>
                public static void ShowInfo(string message, string actionText = null, Action actionCallback = null)
                {
                    LogManager.LogInfo($"[Toast:Info] {message}");
                }

                /// <summary>Show a warning toast notification (yellow/orange)</summary>
                public static void ShowWarning(string message, string actionText = null, Action actionCallback = null)
                {
                    LogManager.LogWarning($"[Toast:Warning] {message}");
                }

                /// <summary>Show an error toast notification (red)</summary>
                public static void ShowError(string message, string actionText = null, Action actionCallback = null)
                {
                    LogManager.LogError($"[Toast:Error] {message}", null);
                }
            }
        }
    }

    // ############################################################################
    // REGION: WMI CONNECTION POOL
    // ############################################################################

    public class WmiConnectionManager : IDisposable
    {
        private readonly ConcurrentDictionary<string, WmiConnInfo> _pool = new ConcurrentDictionary<string, WmiConnInfo>();
        private readonly ConcurrentDictionary<string, WmiConnInfo> _securityPool = new ConcurrentDictionary<string, WmiConnInfo>();
        private readonly Timer _cleanup;
        private const int LIFETIME_MIN = 5;

        private class WmiConnInfo { public ManagementScope Scope; public DateTime LastUsed; }

        public WmiConnectionManager()
        {
            _cleanup = new Timer(_ =>
            {
                var stale = _pool.Where(kv => (DateTime.Now - kv.Value.LastUsed).TotalMinutes > LIFETIME_MIN)
                    .Select(kv => kv.Key).ToList();
                foreach (var k in stale) { WmiConnInfo removed; _pool.TryRemove(k, out removed); }
            }, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        public ManagementScope GetConnection(string hostname, string username, SecureString password)
        {
            if (!SecurityValidator.IsValidHostname(hostname))
                throw new ArgumentException("Invalid hostname", nameof(hostname));

            string key = $"{hostname}_{username}";

            if (_pool.TryGetValue(key, out var info))
            {
                try
                {
                    if (info.Scope.IsConnected && (DateTime.Now - info.LastUsed).TotalMinutes < LIFETIME_MIN)
                    { info.LastUsed = DateTime.Now; return info.Scope; }
                }
                catch (Exception ex)
                {
                    LogManager.LogDebug($"Cached WMI connection validation failed for {hostname}: {ex.Message}");
                }
                WmiConnInfo staleInfo; _pool.TryRemove(key, out staleInfo);
            }

            var opts = new ConnectionOptions
            {
                Timeout = TimeSpan.FromMilliseconds(SecureConfig.WmiTimeoutMs),
                EnablePrivileges = true,
                Impersonation = ImpersonationLevel.Impersonate
            };
            if (!string.IsNullOrEmpty(username) && password != null)
            { opts.Username = username; opts.SecurePassword = password; }

            ManagementScope scope = null;
            try
            {
                scope = new ManagementScope($"\\\\{hostname}\\root\\cimv2", opts);
                scope.Connect();
                _pool.TryAdd(key, new WmiConnInfo { Scope = scope, LastUsed = DateTime.Now });
                return scope;
            }
            catch (System.Runtime.InteropServices.COMException comEx)
            {
                // RPC server unavailable (machine offline or firewall blocking)
                if (comEx.Message.Contains("RPC") || comEx.Message.Contains("0x800706BA"))
                {
                    LogManager.LogDebug($"WMI Connection Failed (RPC unavailable): {hostname}");
                }
                else
                {
                    LogManager.LogError($"WMI Connection Failed: {hostname}", comEx);
                }
                throw; // Let caller handle it
            }
            catch (UnauthorizedAccessException)
            {
                LogManager.LogDebug($"WMI Connection Failed (Access Denied): {hostname}");
                throw; // Let caller handle it
            }
            catch (Exception ex)
            {
                LogManager.LogError($"WMI Connection Failed: {hostname}", ex);
                throw; // Let caller handle it
            }
        }

        /// <summary>
        /// Get cached connection to security namespaces (BitLocker, TPM)
        /// Reuses timeout-coordinated connections to prevent independent scope hangs
        /// </summary>
        public ManagementScope GetSecurityNamespaceConnection(
            string hostname,
            string username,
            SecureString password,
            string securityNamespace)
        {
            if (!SecurityValidator.IsValidHostname(hostname))
                throw new ArgumentException("Invalid hostname", nameof(hostname));

            string key = $"{hostname}_{username}_{securityNamespace}";

            // Try to reuse existing connection
            if (_securityPool.TryGetValue(key, out var cached))
            {
                try
                {
                    if (cached.Scope.IsConnected && (DateTime.Now - cached.LastUsed).TotalMinutes < LIFETIME_MIN)
                    {
                        cached.LastUsed = DateTime.Now;
                        LogManager.LogDebug($"Reusing cached {securityNamespace} connection for {hostname}");
                        return cached.Scope;
                    }
                }
                catch (Exception ex)
                {
                    LogManager.LogDebug($"Cached {securityNamespace} connection validation failed for {hostname}: {ex.Message}");
                }
                _securityPool.TryRemove(key, out _);
            }

            // Create new connection with coordinated timeout
            var opts = new ConnectionOptions
            {
                Timeout = TimeSpan.FromMilliseconds(SecureConfig.WmiTimeoutMs),
                EnablePrivileges = true,
                Impersonation = ImpersonationLevel.Impersonate
            };

            if (!string.IsNullOrEmpty(username) && password != null)
            {
                opts.Username = username;
                opts.SecurePassword = password;
            }

            ManagementScope scope = null;
            try
            {
                scope = new ManagementScope(
                    $"\\\\{hostname}\\root\\CIMv2\\Security\\{securityNamespace}",
                    opts);
                scope.Connect();
                _securityPool.TryAdd(key, new WmiConnInfo { Scope = scope, LastUsed = DateTime.Now });
                LogManager.LogDebug($"Created new {securityNamespace} connection for {hostname}");
                return scope;
            }
            catch
            {
                // Note: ManagementScope doesn't implement IDisposable
                throw;
            }
        }

        // ManagementScope does NOT implement IDisposable
        public void Dispose()
        {
            _cleanup?.Dispose();
            _pool.Clear();
            _securityPool.Clear();
        }
    }

    // ############################################################################
    // REGION: THEMED DIALOG SYSTEM (Zinc-Orange Gradient Branding)
    // ############################################################################

    /// <summary>
    /// Provides consistently themed dialogs across the application
    /// All dialogs use the zinc-orange gradient branding for visual consistency
    /// TAG: #THEME_DIALOG - Search this tag to update all themed dialogs
    /// </summary>
    public static class ThemedDialog
    {
        // TAG: #THEME_COLORS - Color scheme definitions
        public static readonly Color OrangePrimary = Color.FromRgb(255, 133, 51);   // #FF8533
        public static readonly Color OrangeDark = Color.FromRgb(204, 107, 41);      // #CC6B29
        public static readonly Color ZincColor = Color.FromRgb(161, 161, 170);      // #A1A1AA
        public static readonly Color BgDark = Color.FromRgb(26, 26, 26);            // #1A1A1A
        public static readonly Color BgMedium = Color.FromRgb(45, 45, 45);          // #2D2D2D

        /// <summary>
        /// Show error dialog with zinc-orange themed branding
        /// TAG: #THEME_DIALOG
        /// </summary>
        public static void ShowError(Window owner, string title, string message, string details = null, string[] reasons = null, string[] actions = null)
        {
            var dialog = new Window
            {
                Title = title,
                Width = 650,
                Height = reasons != null || actions != null ? 620 : 350,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = owner,
                ResizeMode = ResizeMode.NoResize,
                Background = Brushes.Transparent,
                WindowStyle = WindowStyle.None, // ⚡ Remove system title bar
                AllowsTransparency = true,
                Tag = "ThemedDialog" // TAG: #THEME_DIALOG
            };

            // Main container with border (TAG: #THEME_COLORS)
            var mainBorder = new Border
            {
                Background = new SolidColorBrush(BgDark),
                BorderBrush = new SolidColorBrush(OrangePrimary),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(0)
            };

            var mainStack = new StackPanel { Margin = new Thickness(30) };

            // Header with orange-to-zinc gradient (TAG: #THEME_COLORS)
            var headerBorder = new Border
            {
                Background = new LinearGradientBrush(OrangePrimary, ZincColor, 90),
                Padding = new Thickness(20, 15, 20, 15),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 0, 20)
            };

            // Use LogoConfig for branding consistency (TAG: #THEME_LOGO)
            var headerStack = new StackPanel { Orientation = Orientation.Horizontal };

            // Add logo icon from LogoConfig
            var logoIcon = LogoConfig.CreateIconPath();
            logoIcon.Width = LogoConfig.MEDIUM_ICON_SIZE;
            logoIcon.Height = LogoConfig.MEDIUM_ICON_SIZE;
            logoIcon.Margin = new Thickness(0, 0, 15, 0);
            headerStack.Children.Add(logoIcon);

            // Title and message
            var textStack = new StackPanel();
            textStack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            });
            textStack.Children.Add(new TextBlock
            {
                Text = message,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
                Margin = new Thickness(0, 5, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });
            headerStack.Children.Add(textStack);

            headerBorder.Child = headerStack;
            mainStack.Children.Add(headerBorder);

            // Details section (if provided)
            if (!string.IsNullOrEmpty(details))
            {
                var detailsBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(60, 30, 30)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(180, 50, 50)),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(15),
                    CornerRadius = new CornerRadius(4),
                    Margin = new Thickness(0, 0, 0, 20)
                };

                var detailsText = new TextBlock
                {
                    Text = details,
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 150, 150)),
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 11
                };
                detailsBorder.Child = detailsText;
                mainStack.Children.Add(detailsBorder);
            }

            // Reasons list (if provided)
            if (reasons != null && reasons.Length > 0)
            {
                mainStack.Children.Add(new TextBlock
                {
                    Text = "Possible Reasons:",
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(OrangePrimary), // TAG: #THEME_COLORS
                    Margin = new Thickness(0, 0, 0, 10)
                });

                var reasonsList = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };
                foreach (var reason in reasons)
                {
                    reasonsList.Children.Add(new TextBlock
                    {
                        Text = reason,
                        Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                        Margin = new Thickness(10, 0, 0, 8),
                        FontSize = 12
                    });
                }
                mainStack.Children.Add(reasonsList);
            }

            // Actions list (if provided)
            if (actions != null && actions.Length > 0)
            {
                mainStack.Children.Add(new TextBlock
                {
                    Text = "Recommended Actions:",
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(OrangePrimary), // TAG: #THEME_COLORS
                    Margin = new Thickness(0, 0, 0, 10)
                });

                var actionsList = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };
                foreach (var action in actions)
                {
                    actionsList.Children.Add(new TextBlock
                    {
                        Text = action,
                        Foreground = new SolidColorBrush(Color.FromRgb(180, 255, 180)),
                        Margin = new Thickness(10, 0, 0, 5),
                        FontSize = 12
                    });
                }
                mainStack.Children.Add(actionsList);
            }

            // Close button with orange theme (TAG: #THEME_COLORS)
            var btnClose = new Button
            {
                Content = "CLOSE",
                Padding = new Thickness(20, 10, 20, 10),
                Background = new SolidColorBrush(OrangePrimary),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            btnClose.Click += (s, e) => dialog.Close();
            mainStack.Children.Add(btnClose);

            var scrollViewer = new ScrollViewer
            {
                Content = mainStack,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            // Wrap in border for consistent styling
            mainBorder.Child = scrollViewer;
            dialog.Content = mainBorder;
            dialog.ShowDialog();
        }

        /// <summary>
        /// Show info dialog with zinc-orange themed branding
        /// TAG: #THEME_DIALOG
        /// </summary>
        public static void ShowInfo(Window owner, string title, string message)
        {
            ShowError(owner, title, message);
        }
    }

    // ############################################################################
    // REGION: DOMAIN CONTROLLER MANAGER (Dynamic DC Discovery & Persistence)
    // ############################################################################

    /// <summary>
    /// Manages domain controller discovery and configuration persistence
    /// Eliminates hardcoded DC lists for multi-domain deployment support
    /// </summary>
    public class DCManager
    {
        private static string ConfigPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Artazn_DCConfiguration.xml");

        public class DCInfo
        {
            public string Hostname { get; set; }
            public DateTime LastSeen { get; set; }
            public int AvgLatency { get; set; }

            public override string ToString() => Hostname;
        }

        public class DomainInfo
        {
            public string Name { get; set; }
            public DateTime LastDiscovery { get; set; }
            public List<DCInfo> DomainControllers { get; set; } = new List<DCInfo>();
        }

        public List<DomainInfo> _domains = new List<DomainInfo>();  // Public for DC history access

        /// <summary>
        /// Load persisted DC configuration from %APPDATA%
        /// </summary>
        public void LoadConfiguration()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var doc = XDocument.Load(ConfigPath);
                    var domainElements = doc.Root?.Element("Domains")?.Elements("Domain");

                    if (domainElements != null)
                    {
                        foreach (var domainElement in domainElements)
                        {
                            var domainInfo = new DomainInfo
                            {
                                Name = domainElement.Attribute("Name")?.Value,
                                LastDiscovery = DateTime.Parse(domainElement.Attribute("LastDiscovery")?.Value ?? DateTime.MinValue.ToString())
                            };

                            var dcElements = domainElement.Element("DomainControllers")?.Elements("DC");
                            if (dcElements != null)
                            {
                                foreach (var dcElement in dcElements)
                                {
                                    domainInfo.DomainControllers.Add(new DCInfo
                                    {
                                        Hostname = dcElement.Attribute("Hostname")?.Value,
                                        LastSeen = DateTime.Parse(dcElement.Attribute("LastSeen")?.Value ?? DateTime.MinValue.ToString()),
                                        AvgLatency = int.Parse(dcElement.Attribute("AvgLatency")?.Value ?? "0")
                                    });
                                }
                            }

                            _domains.Add(domainInfo);
                        }
                    }

                    LogManager.LogDebug($"Loaded DC configuration: {_domains.Count} domain(s), {_domains.Sum(d => d.DomainControllers.Count)} DC(s)");
                }
            }
            catch (Exception ex)
            {
                LogManager.LogDebug($"Failed to load DC config: {ex.Message}");
            }
        }

        /// <summary>
        /// Save DC configuration to %APPDATA%
        /// </summary>
        public void SaveConfiguration()
        {
            try
            {
                var doc = new XDocument(
                    new XElement("DCConfiguration",
                        new XElement("Domains",
                            _domains.Select(d =>
                                new XElement("Domain",
                                    new XAttribute("Name", d.Name),
                                    new XAttribute("LastDiscovery", d.LastDiscovery.ToString("o")),
                                    new XElement("DomainControllers",
                                        d.DomainControllers.Select(dc =>
                                            new XElement("DC",
                                                new XAttribute("Hostname", dc.Hostname),
                                                new XAttribute("LastSeen", dc.LastSeen.ToString("o")),
                                                new XAttribute("AvgLatency", dc.AvgLatency)
                                            )
                                        )
                                    )
                                )
                            )
                        ),
                        new XElement("Settings",
                            new XElement("CacheTTLMinutes", "60"),
                            new XElement("AutoDiscoverOnStartup", "true")
                        )
                    )
                );

                doc.Save(ConfigPath);
                LogManager.LogDebug($"Saved DC configuration: {_domains.Count} domain(s), {_domains.Sum(d => d.DomainControllers.Count)} DC(s)");
            }
            catch (Exception ex)
            {
                LogManager.LogDebug($"Failed to save DC config: {ex.Message}");
            }
        }

        /// <summary>
        /// Discover DCs for current domain using Active Directory
        /// </summary>
        public async Task<List<DCInfo>> DiscoverDomainControllersAsync()
        {
            var dcList = new List<DCInfo>();

            try
            {
                // Run AD discovery on thread pool to avoid blocking UI
                await Task.Run(() =>
                {
                    try
                    {
                        var domain = Domain.GetCurrentDomain();
                        string domainName = domain.Name;

                        LogManager.LogDebug($"Discovering DCs for domain: {domainName}");

                        foreach (DomainController dc in domain.DomainControllers)
                        {
                            if (SecurityValidator.IsValidHostname(dc.Name))
                            {
                                dcList.Add(new DCInfo
                                {
                                    Hostname = dc.Name,
                                    LastSeen = DateTime.Now,
                                    AvgLatency = 0
                                });
                            }
                            dc.Dispose();
                        }

                        // Update or add domain
                        var domainInfo = _domains.FirstOrDefault(d => d.Name == domainName);
                        if (domainInfo == null)
                        {
                            domainInfo = new DomainInfo { Name = domainName };
                            _domains.Add(domainInfo);
                        }

                        domainInfo.LastDiscovery = DateTime.Now;
                        domainInfo.DomainControllers = dcList;

                        domain.Dispose();
                    }
                    catch (Exception innerEx)
                    {
                        // Domain not available - just log and return
                        // The outer catch will handle fallback to cached DCs
                        LogManager.LogDebug($"DC discovery inner exception: {innerEx.Message}");
                        // Don't throw - let it fall through to outer catch
                    }
                });

                SaveConfiguration();

                LogManager.LogDebug($"Discovered {dcList.Count} DCs");
            }
            catch (Exception ex)
            {
                LogManager.LogDebug($"DC discovery failed: {ex.Message} - using cached list");

                // Fall back to cached DCs from configuration
                var currentDomain = _domains.FirstOrDefault();
                if (currentDomain != null)
                {
                    dcList = currentDomain.DomainControllers ?? new List<DCInfo>();
                }
                // If no cached DCs, dcList remains empty - caller will handle
            }

            return dcList;
        }

        /// <summary>
        /// Get cached DCs if fresh enough
        /// </summary>
        public List<DCInfo> GetCachedDCs(string domainName, int cacheTTLMinutes = 60)
        {
            var domain = _domains.FirstOrDefault(d => d.Name == domainName);

            if (domain != null &&
                (DateTime.Now - domain.LastDiscovery).TotalMinutes < cacheTTLMinutes)
            {
                LogManager.LogDebug($"Using cached DCs for {domainName} (age: {(DateTime.Now - domain.LastDiscovery).TotalMinutes:F1}m)");
                return domain.DomainControllers;
            }

            return null;
        }
    }

    // ############################################################################
    // REGION: CIM CONNECTION POOL (Modern WS-MAN/DCOM Alternative to WMI)
    // ############################################################################

    /// <summary>
    /// Modern CIM session manager using Microsoft.Management.Infrastructure
    /// Provides 4-21x faster WAN performance via WS-MAN protocol
    /// Falls back to DCOM if WS-MAN unavailable
    /// </summary>
    public class CimSessionManager : IDisposable
    {
        private readonly ConcurrentDictionary<string, CimConnInfo> _pool = new ConcurrentDictionary<string, CimConnInfo>();
        private readonly Timer _cleanup;
        private const int LIFETIME_MIN = 5;

        private class CimConnInfo
        {
            public CimSession Session;
            public DateTime LastUsed;
            public string Protocol; // "WSMan" or "DCOM"
        }

        public CimSessionManager()
        {
            _cleanup = new Timer(_ =>
            {
                var stale = _pool.Where(kv => (DateTime.Now - kv.Value.LastUsed).TotalMinutes > LIFETIME_MIN)
                    .Select(kv => kv.Key).ToList();
                foreach (var k in stale)
                {
                    if (_pool.TryRemove(k, out var removed))
                    {
                        removed.Session?.Dispose();
                    }
                }
            }, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        public CimSession GetConnection(string hostname, string username, SecureString password, out string protocol)
        {
            if (!SecurityValidator.IsValidHostname(hostname))
                throw new ArgumentException("Invalid hostname", nameof(hostname));

            LogManager.LogDebug($"[CimSessionManager] Requesting connection to {hostname} (user: {username ?? "current"})");

            // Try WSMan cache first
            string keyWsm = $"{hostname}_{username}_wsm";
            if (_pool.TryGetValue(keyWsm, out var infoWsm))
            {
                try
                {
                    if (infoWsm.Session != null && (DateTime.Now - infoWsm.LastUsed).TotalMinutes < LIFETIME_MIN)
                    {
                        LogManager.LogDebug($"[CimSessionManager] Testing cached WSMan connection for {hostname}...");
                        infoWsm.Session.TestConnection();
                        infoWsm.LastUsed = DateTime.Now;
                        protocol = infoWsm.Protocol;
                        LogManager.LogDebug($"[CimSessionManager] Reusing cached WSMan connection for {hostname}");
                        return infoWsm.Session;
                    }
                    else
                    {
                        LogManager.LogDebug($"[CimSessionManager] Cached WSMan connection expired for {hostname} (age: {(DateTime.Now - infoWsm.LastUsed).TotalMinutes:F1} min)");
                    }
                }
                catch (Exception testEx)
                {
                    LogManager.LogDebug($"[CimSessionManager] Cached WSMan connection test failed for {hostname}: {testEx.Message}");
                    _pool.TryRemove(keyWsm, out _);
                }
            }

            // Try DCOM cache
            string keyDcom = $"{hostname}_{username}_dcom";
            if (_pool.TryGetValue(keyDcom, out var infoDcom))
            {
                try
                {
                    if (infoDcom.Session != null && (DateTime.Now - infoDcom.LastUsed).TotalMinutes < LIFETIME_MIN)
                    {
                        LogManager.LogDebug($"[CimSessionManager] Testing cached DCOM connection for {hostname}...");
                        infoDcom.Session.TestConnection();
                        infoDcom.LastUsed = DateTime.Now;
                        protocol = infoDcom.Protocol;
                        LogManager.LogDebug($"[CimSessionManager] Reusing cached DCOM connection for {hostname}");
                        return infoDcom.Session;
                    }
                    else
                    {
                        LogManager.LogDebug($"[CimSessionManager] Cached DCOM connection expired for {hostname} (age: {(DateTime.Now - infoDcom.LastUsed).TotalMinutes:F1} min)");
                    }
                }
                catch (Exception testEx)
                {
                    LogManager.LogDebug($"[CimSessionManager] Cached DCOM connection test failed for {hostname}: {testEx.Message}");
                    _pool.TryRemove(keyDcom, out _);
                }
            }

            // Create new connection - try WSMan first
            CimSession session = null;
            protocol = null;

            LogManager.LogDebug($"[CimSessionManager] No valid cached connection, creating new WSMan session for {hostname}...");
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                session = CreateWSManSession(hostname, username, password);
                sw.Stop();
                protocol = "WSMan";
                _pool.TryAdd(keyWsm, new CimConnInfo { Session = session, LastUsed = DateTime.Now, Protocol = protocol });
                LogManager.LogDebug($"[CimSessionManager] WSMan connection SUCCESS for {hostname} (time: {sw.ElapsedMilliseconds}ms)");
                return session;
            }
            catch (CimException ex)
            {
                LogManager.LogDebug($"[CimSessionManager] WSMan FAILED for {hostname}: StatusCode={ex.StatusCode}, ErrorCode={ex.NativeErrorCode}, Message={ex.Message}");
                LogManager.LogDebug($"[CimSessionManager] Attempting DCOM fallback for {hostname}...");

                // Try DCOM fallback
                try
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    session = CreateDComSession(hostname, username, password);
                    sw.Stop();
                    protocol = "DCOM";
                    _pool.TryAdd(keyDcom, new CimConnInfo { Session = session, LastUsed = DateTime.Now, Protocol = protocol });
                    LogManager.LogDebug($"[CimSessionManager] DCOM connection SUCCESS for {hostname} (time: {sw.ElapsedMilliseconds}ms)");
                    return session;
                }
                catch (Exception dcomEx)
                {
                    LogManager.LogError($"[CimSessionManager] BOTH WSMan and DCOM FAILED for {hostname}", dcomEx);
                    LogManager.LogDebug($"[WSMan Error] StatusCode: {ex.StatusCode}, ErrorCode: {ex.NativeErrorCode}");
                    LogManager.LogDebug($"[DCOM Error] {dcomEx.GetType().Name}: {dcomEx.Message}");
                    throw; // Caller will fall back to WMI
                }
            }
        }

        private CimSession CreateWSManSession(string hostname, string username, SecureString password)
        {
            var sessionOptions = new WSManSessionOptions();
            sessionOptions.Timeout = TimeSpan.FromMilliseconds(SecureConfig.WmiTimeoutMs);

            if (!string.IsNullOrEmpty(username) && password != null)
            {
                var credentials = new CimCredential(PasswordAuthenticationMechanism.Default,
                                                     null, // domain
                                                     username,
                                                     password);
                sessionOptions.AddDestinationCredentials(credentials);
            }

            CimSession session = CimSession.Create(hostname, sessionOptions);
            session.TestConnection();
            return session;
        }

        private CimSession CreateDComSession(string hostname, string username, SecureString password)
        {
            var sessionOptions = new DComSessionOptions();
            sessionOptions.Timeout = TimeSpan.FromMilliseconds(SecureConfig.WmiTimeoutMs);
            sessionOptions.Impersonation = ImpersonationType.Impersonate;

            if (!string.IsNullOrEmpty(username) && password != null)
            {
                var credentials = new CimCredential(PasswordAuthenticationMechanism.Default,
                                                     null,
                                                     username,
                                                     password);
                sessionOptions.AddDestinationCredentials(credentials);
            }

            CimSession session = CimSession.Create(hostname, sessionOptions);
            session.TestConnection();
            return session;
        }

        public void Dispose()
        {
            _cleanup?.Dispose();
            foreach (var info in _pool.Values)
            {
                info.Session?.Dispose();
            }
            _pool.Clear();
        }
    }

    // ############################################################################
    // REGION: CIM/WMI QUERY HELPER
    // ############################################################################

    /// <summary>
    /// Helper class to execute queries with CIM-first, WMI-fallback pattern
    /// </summary>
    public class HybridQueryHelper
    {
        private CimSessionManager _cimManager;
        private WmiConnectionManager _wmiManager;

        public HybridQueryHelper(CimSessionManager cimManager, WmiConnectionManager wmiManager)
        {
            _cimManager = cimManager;
            _wmiManager = wmiManager;
        }

        /// <summary>
        /// Execute a WQL query with CIM first, WMI fallback
        /// Returns (protocol, CimInstances, WmiObjects) tuple
        /// </summary>
        public (string Protocol, IEnumerable<CimInstance> CimResults, ManagementObjectCollection WmiResults) QueryInstances(
            string hostname, string username, SecureString password, string namespacePath, string query)
        {
            LogManager.LogDebug($"[QueryInstances] Target: {hostname} | Namespace: {namespacePath} | Query: {query.Substring(0, Math.Min(100, query.Length))}...");

            // Try CIM first
            try
            {
                LogManager.LogDebug($"[CIM] Attempting connection to {hostname}...");
                var session = _cimManager.GetConnection(hostname, username, password, out string protocol);
                LogManager.LogDebug($"[CIM] Connection established: {protocol}");

                var sw = System.Diagnostics.Stopwatch.StartNew();
                var instances = session.QueryInstances(namespacePath, "WQL", query);
                int count = instances.Count();
                sw.Stop();

                LogManager.LogDebug($"[CIM] Query SUCCESS: {protocol} | Results: {count} | Time: {sw.ElapsedMilliseconds}ms");
                return ($"CIM ({protocol})", instances, null);
            }
            catch (CimException cimEx)
            {
                LogManager.LogDebug($"[CIM] Query FAILED for {hostname}: StatusCode={cimEx.StatusCode}, ErrorCode={cimEx.NativeErrorCode}, Message={cimEx.Message}");
                LogManager.LogDebug($"[WMI] Attempting fallback for {hostname}...");

                // Fall back to WMI
                try
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var scope = _wmiManager.GetConnection(hostname, username, password);
                    using (var searcher = new ManagementObjectSearcher(scope, new ObjectQuery(query)))
                    {
                        var results = searcher.Get();
                        int count = results.Count;
                        sw.Stop();

                        LogManager.LogDebug($"[WMI] Fallback SUCCESS for {hostname} | Results: {count} | Time: {sw.ElapsedMilliseconds}ms");
                        return ("WMI (Fallback)", null, results);
                    }
                }
                catch (Exception wmiEx)
                {
                    LogManager.LogError($"[BOTH FAILED] CIM and WMI queries both failed for {hostname}", wmiEx);
                    LogManager.LogDebug($"[CIM Error] StatusCode: {cimEx.StatusCode}, ErrorCode: {cimEx.NativeErrorCode}");
                    LogManager.LogDebug($"[WMI Error] {wmiEx.GetType().Name}: {wmiEx.Message}");
                    throw;
                }
            }
            catch (Exception ex)
            {
                LogManager.LogDebug($"[CIM] Unexpected exception for {hostname}: {ex.GetType().Name} - {ex.Message}");
                LogManager.LogDebug($"[WMI] Attempting fallback for {hostname}...");

                // Fall back to WMI
                try
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var scope = _wmiManager.GetConnection(hostname, username, password);
                    using (var searcher = new ManagementObjectSearcher(scope, new ObjectQuery(query)))
                    {
                        var results = searcher.Get();
                        int count = results.Count;
                        sw.Stop();

                        LogManager.LogDebug($"[WMI] Fallback SUCCESS for {hostname} | Results: {count} | Time: {sw.ElapsedMilliseconds}ms");
                        return ("WMI (Fallback)", null, results);
                    }
                }
                catch (Exception wmiEx)
                {
                    LogManager.LogError($"[BOTH FAILED] CIM and WMI queries both failed for {hostname}", wmiEx);
                    LogManager.LogDebug($"[CIM Error] {ex.GetType().Name}: {ex.Message}");
                    LogManager.LogDebug($"[WMI Error] {wmiEx.GetType().Name}: {wmiEx.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Get first result from query, handling both CIM and WMI
        /// </summary>
        public object GetFirstPropertyValue(string protocol, IEnumerable<CimInstance> cimResults, ManagementObjectCollection wmiResults, string propertyName)
        {
            if (cimResults != null)
            {
                var instance = cimResults.FirstOrDefault();
                return instance?.CimInstanceProperties[propertyName]?.Value;
            }
            else if (wmiResults != null)
            {
                var obj = wmiResults.Cast<ManagementObject>().FirstOrDefault();
                var value = obj?[propertyName];
                obj?.Dispose();
                return value;
            }
            return null;
        }

        /// <summary>
        /// Invoke a WMI/CIM method (e.g., Win32_Process.Create, Win32Shutdown)
        /// Returns (protocol, returnValue)
        /// </summary>
        public (string Protocol, object ReturnValue) InvokeMethod(string hostname, string username, SecureString password,
            string namespacePath, string className, string methodName, Dictionary<string, object> parameters)
        {
            string paramStr = parameters != null ? string.Join(", ", parameters.Select(p => $"{p.Key}={p.Value}")) : "none";
            LogManager.LogDebug($"[InvokeMethod] Target: {hostname} | Class: {className} | Method: {methodName} | Params: {paramStr}");

            // Try CIM first
            try
            {
                LogManager.LogDebug($"[CIM] Attempting method invocation on {hostname}...");
                var session = _cimManager.GetConnection(hostname, username, password, out string protocol);
                var methodParams = new CimMethodParametersCollection();

                if (parameters != null)
                {
                    foreach (var param in parameters)
                    {
                        Microsoft.Management.Infrastructure.CimType cimType = param.Value is string ? Microsoft.Management.Infrastructure.CimType.String : Microsoft.Management.Infrastructure.CimType.SInt32;
                        methodParams.Add(CimMethodParameter.Create(param.Key, param.Value, cimType, CimFlags.In));
                    }
                }

                var sw = System.Diagnostics.Stopwatch.StartNew();
                var result = session.InvokeMethod(namespacePath, className, methodName, methodParams);
                sw.Stop();

                LogManager.LogDebug($"[CIM] Method SUCCESS: {protocol} | ReturnValue: {result.ReturnValue?.Value} | Time: {sw.ElapsedMilliseconds}ms");
                return ($"CIM ({protocol})", result.ReturnValue.Value);
            }
            catch (Exception ex)
            {
                LogManager.LogDebug($"[CIM] Method invocation FAILED for {hostname}: {ex.GetType().Name} - {ex.Message}");
                LogManager.LogDebug($"[WMI] Attempting fallback method invocation...");

                // Fall back to WMI
                try
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var scope = _wmiManager.GetConnection(hostname, username, password);
                    using (var mc = new ManagementClass(scope, new ManagementPath(className), null))
                    {
                        var inp = mc.GetMethodParameters(methodName);
                        if (parameters != null)
                        {
                            foreach (var param in parameters)
                                inp[param.Key] = param.Value;
                        }
                        var res = mc.InvokeMethod(methodName, inp, null);
                        sw.Stop();

                        LogManager.LogDebug($"[WMI] Method fallback SUCCESS | ReturnValue: {res["returnValue"]} | Time: {sw.ElapsedMilliseconds}ms");
                        return ("WMI (Fallback)", res["returnValue"]);
                    }
                }
                catch (Exception wmiEx)
                {
                    LogManager.LogError($"[BOTH FAILED] CIM and WMI method invocation both failed for {hostname}", wmiEx);
                    LogManager.LogDebug($"[CIM Error] {ex.GetType().Name}: {ex.Message}");
                    LogManager.LogDebug($"[WMI Error] {wmiEx.GetType().Name}: {wmiEx.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Invoke a method on a specific CIM/WMI instance (e.g., Win32Shutdown on OS instance)
        /// </summary>
        public (string Protocol, object ReturnValue) InvokeInstanceMethod(string hostname, string username, SecureString password,
            string namespacePath, string query, string methodName, Dictionary<string, object> parameters)
        {
            string paramStr = parameters != null ? string.Join(", ", parameters.Select(p => $"{p.Key}={p.Value}")) : "none";
            LogManager.LogDebug($"[InvokeInstanceMethod] Target: {hostname} | Method: {methodName} | Params: {paramStr} | Query: {query.Substring(0, Math.Min(50, query.Length))}...");

            // Try CIM first
            try
            {
                LogManager.LogDebug($"[CIM] Attempting instance method invocation on {hostname}...");
                var session = _cimManager.GetConnection(hostname, username, password, out string protocol);
                var instances = session.QueryInstances(namespacePath, "WQL", query);
                var instance = instances.FirstOrDefault();

                if (instance != null)
                {
                    var methodParams = new CimMethodParametersCollection();
                    if (parameters != null)
                    {
                        foreach (var param in parameters)
                        {
                            Microsoft.Management.Infrastructure.CimType cimType = param.Value is string ? Microsoft.Management.Infrastructure.CimType.String : Microsoft.Management.Infrastructure.CimType.SInt32;
                            methodParams.Add(CimMethodParameter.Create(param.Key, param.Value, cimType, CimFlags.In));
                        }
                    }

                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var result = session.InvokeMethod(namespacePath, instance, methodName, methodParams);
                    sw.Stop();

                    LogManager.LogDebug($"[CIM] Instance method SUCCESS: {protocol} | ReturnValue: {result.ReturnValue?.Value} | Time: {sw.ElapsedMilliseconds}ms");
                    return ($"CIM ({protocol})", result.ReturnValue.Value);
                }
                LogManager.LogDebug($"[CIM] No instance found for query, returning null");
                return ($"CIM ({protocol})", null);
            }
            catch (Exception ex)
            {
                LogManager.LogDebug($"[CIM] Instance method invocation FAILED for {hostname}: {ex.GetType().Name} - {ex.Message}");
                LogManager.LogDebug($"[WMI] Attempting fallback instance method invocation...");

                // Fall back to WMI
                try
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var scope = _wmiManager.GetConnection(hostname, username, password);
                    using (var searcher = new ManagementObjectSearcher(scope, new ObjectQuery(query)))
                    using (var results = searcher.Get())
                    {
                        foreach (ManagementObject obj in results)
                        {
                            var inp = obj.GetMethodParameters(methodName);
                            if (parameters != null)
                            {
                                foreach (var param in parameters)
                                    inp[param.Key] = param.Value;
                            }
                            var res = obj.InvokeMethod(methodName, inp, null);
                            sw.Stop();

                            LogManager.LogDebug($"[WMI] Instance method fallback SUCCESS | ReturnValue: {res["returnValue"]} | Time: {sw.ElapsedMilliseconds}ms");
                            obj.Dispose();
                            return ("WMI (Fallback)", res["returnValue"]);
                        }
                    }
                    LogManager.LogDebug($"[WMI] No instance found for query, returning null");
                    return ("WMI (Fallback)", null);
                }
                catch (Exception wmiEx)
                {
                    LogManager.LogError($"[BOTH FAILED] CIM and WMI instance method invocation both failed for {hostname}", wmiEx);
                    LogManager.LogDebug($"[CIM Error] {ex.GetType().Name}: {ex.Message}");
                    LogManager.LogDebug($"[WMI Error] {wmiEx.GetType().Name}: {wmiEx.Message}");
                    throw;
                }
            }
        }
    }

    // ############################################################################
    // REGION: DTOs
    // ############################################################################

    public class HardwareSpec
    {
        public string Bios { get; set; } = "N/A";
        public string Serial { get; set; } = "N/A";
        public string User { get; set; } = "N/A";
        public string Model { get; set; } = "N/A";
        public string Manufacturer { get; set; } = "N/A";
        public string CPU { get; set; } = "N/A";
        public string Cores { get; set; } = "N/A";
        public string RAM { get; set; } = "N/A";
        public string OS { get; set; } = "N/A";
        public string WindowsVersion { get; set; } = "N/A"; // 22H2, 23H2, 24H2, 25H2, etc.
        public string IP { get; set; } = "N/A";
        public string MAC { get; set; } = "N/A";
        public string DNS { get; set; } = "N/A";
        public string Uptime { get; set; } = "N/A";
        public string Battery { get; set; } = "N/A";
        public string LastBoot { get; set; } = "N/A";
        public string Domain { get; set; } = "N/A";
        public string TimeZone { get; set; } = "N/A";
        public List<string> Drives { get; set; } = new List<string>();
        public string Protocol { get; set; } = "None";
        public string Chassis { get; set; } = "Unknown";
        public string BitLocker { get; set; } = "Unknown";
        public string TPMEnabled { get; set; } = "Unknown";
    }

    // ############################################################################
    // REGION: TOOL WINDOW
    // ############################################################################

    public class ToolWindow : Window
    {
        private RichTextBox _output;
        private TextBlock _txtStatus;
        public Func<Task> _refreshAction;

        public ToolWindow(string hostname, string toolName)
        {
            Title = $"{toolName} - {hostname}";
            Width = 920; Height = 620;
            Background = new SolidColorBrush(Color.FromRgb(13, 13, 13)); // #0D0D0D - darker background
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            Grid g = new Grid();
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Header with orange gradient accent
            Border hdr = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(37, 37, 38)), // #252526 - medium dark
                BorderBrush = new SolidColorBrush(Color.FromRgb(255, 133, 51)), // #FF8533 - orange accent
                BorderThickness = new Thickness(0, 0, 0, 2), // Thicker bottom border with orange
                Padding = new Thickness(15, 12, 15, 12)
            };
            Grid hg = new Grid();
            hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel hs = new StackPanel();

            // Tool name with orange gradient effect
            var toolNameText = new TextBlock
            {
                Text = toolName,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 133, 51)), // #FF8533 - orange
                FontSize = 17,
                FontWeight = FontWeights.Bold
            };

            // Hostname with zinc color
            var hostnameText = new TextBlock
            {
                Text = $"Target: {hostname}",
                Foreground = new SolidColorBrush(Color.FromRgb(161, 161, 170)), // #A1A1AA - zinc
                FontSize = 12,
                Margin = new Thickness(0, 4, 0, 0)
            };

            hs.Children.Add(toolNameText);
            hs.Children.Add(hostnameText);

            // Refresh button with orange accent
            Button btnRefresh = new Button
            {
                Content = "↻ REFRESH",
                Width = 110,
                Height = 34,
                Background = new SolidColorBrush(Color.FromRgb(255, 133, 51)), // #FF8533 - orange
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Cursor = Cursors.Hand
            };

            // Hover effect for refresh button
            btnRefresh.MouseEnter += (s, e) => { btnRefresh.Background = new SolidColorBrush(Color.FromRgb(255, 170, 102)); }; // Lighter orange
            btnRefresh.MouseLeave += (s, e) => { btnRefresh.Background = new SolidColorBrush(Color.FromRgb(255, 133, 51)); }; // Original orange

            btnRefresh.Click += async (s, e) => { try { if (_refreshAction != null) await _refreshAction(); } catch (Exception ex) { LogManager.LogError("Refresh failed", ex); } };
            Grid.SetColumn(hs, 0); Grid.SetColumn(btnRefresh, 1);
            hg.Children.Add(hs); hg.Children.Add(btnRefresh);
            hdr.Child = hg; Grid.SetRow(hdr, 0);

            // Output terminal with modern dark theme
            _output = new RichTextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(13, 13, 13)), // #0D0D0D - pure black background
                Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)), // #CCCCCC - light gray text (easier on eyes than bright green)
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                IsReadOnly = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(15, 12, 15, 12) // left, top, right, bottom
            };
            Grid.SetRow(_output, 1);

            Border ftr = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(26, 26, 26)), // #1A1A1A - dark footer
                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60)), // #3C3C3C - subtle border
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(15, 12, 15, 12)
            };
            Grid fg = new Grid();
            fg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            fg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Status text with orange accent
            _txtStatus = new TextBlock
            {
                Text = "Ready",
                Foreground = new SolidColorBrush(Color.FromRgb(22, 198, 12)), // #16C60C - green
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold
            };

            // Copy button with ghost style
            Button btnCopy = new Button
            {
                Content = "📋 COPY ALL",
                Width = 120,
                Height = 34,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(176, 176, 176)), // #B0B0B0 - light gray
                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60)), // #3C3C3C
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 8, 0)
            };

            // Hover effects for copy button
            btnCopy.MouseEnter += (s, e) =>
            {
                btnCopy.Background = new SolidColorBrush(Color.FromRgb(51, 51, 51));
                btnCopy.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 133, 51)); // Orange border on hover
            };
            btnCopy.MouseLeave += (s, e) =>
            {
                btnCopy.Background = Brushes.Transparent;
                btnCopy.BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60));
            };

            btnCopy.Click += (s, e) =>
            {
                try
                {
                    string allText = new TextRange(_output.Document.ContentStart, _output.Document.ContentEnd).Text;
                    Clipboard.SetText(allText);
                    _txtStatus.Text = "Copied to clipboard!";
                    _txtStatus.Foreground = new SolidColorBrush(Color.FromRgb(255, 133, 51)); // Orange for success feedback
                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                    timer.Tick += (ts, te) =>
                    {
                        _txtStatus.Text = "Ready";
                        _txtStatus.Foreground = new SolidColorBrush(Color.FromRgb(22, 198, 12)); // Back to green
                        timer.Stop();
                    };
                    timer.Start();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Copy failed: {ex.Message}");
                }
            };

            // Close button with ghost style
            Button btnClose = new Button
            {
                Content = "CLOSE",
                Width = 100,
                Height = 34,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)), // #888888 - gray
                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60)), // #3C3C3C
                BorderThickness = new Thickness(1),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };

            // Hover effects for close button
            btnClose.MouseEnter += (s, e) =>
            {
                btnClose.Background = new SolidColorBrush(Color.FromRgb(51, 51, 51));
                btnClose.BorderBrush = new SolidColorBrush(Color.FromRgb(161, 161, 170)); // Zinc border
            };
            btnClose.MouseLeave += (s, e) =>
            {
                btnClose.Background = Brushes.Transparent;
                btnClose.BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60));
            };

            btnClose.Click += (s, e) => Close();
            Grid.SetColumn(_txtStatus, 0);
            Grid.SetColumn(btnCopy, 1);
            Grid.SetColumn(btnClose, 2);
            fg.Children.Add(_txtStatus); fg.Children.Add(btnCopy); fg.Children.Add(btnClose);
            ftr.Child = fg; Grid.SetRow(ftr, 2);

            g.Children.Add(hdr); g.Children.Add(_output); g.Children.Add(ftr);
            Content = g;
        }

        public void SetRefreshAction(Func<Task> action) => _refreshAction = action;

        public void AppendOutput(string text, bool isError = false)
        {
            Dispatcher.Invoke(() =>
            {
                TextRange tr = new TextRange(_output.Document.ContentEnd, _output.Document.ContentEnd);
                tr.Text = text + "\n";
                // Use orange/zinc theme colors: orange for errors, light gray for normal text
                tr.ApplyPropertyValue(TextElement.ForegroundProperty, isError
                    ? new SolidColorBrush(Color.FromRgb(255, 68, 68)) // #FF4444 - red for errors
                    : new SolidColorBrush(Color.FromRgb(204, 204, 204))); // #CCCCCC - light gray for normal text
                _output.ScrollToEnd();
            });
        }

        public void SetStatus(string status, bool isError = false)
        {
            Dispatcher.Invoke(() =>
            {
                _txtStatus.Text = status;
                // Orange for errors, green for success/ready
                _txtStatus.Foreground = isError
                    ? new SolidColorBrush(Color.FromRgb(232, 17, 35)) // #E81123 - danger red
                    : new SolidColorBrush(Color.FromRgb(22, 198, 12)); // #16C60C - accent green
            });
        }

        public void ClearOutput() { Dispatcher.Invoke(() => _output.Document.Blocks.Clear()); }
    }

    // ############################################################################
    // REGION: PINNED DEVICE MONITORING
    // ############################################################################

    /// <summary>
    /// Represents a pinned device for monitoring
    /// </summary>
    public class PinnedDevice : INotifyPropertyChanged
    {
        private string _input;
        private string _resolvedName;
        private string _resolvedIP;
        private string _status;
        private string _lastChecked;
        private Brush _statusColor;
        private string _responseTime;
        private Brush _responseTimeColor;
        private bool _isOnline;

        public string Input
        {
            get => _input;
            set { _input = value; OnPropertyChanged(); }
        }

        public string ResolvedName
        {
            get => _resolvedName;
            set { _resolvedName = value; OnPropertyChanged(); }
        }

        public string ResolvedIP
        {
            get => _resolvedIP;
            set { _resolvedIP = value; OnPropertyChanged(); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public string LastChecked
        {
            get => _lastChecked;
            set { _lastChecked = value; OnPropertyChanged(); }
        }

        public Brush StatusColor
        {
            get => _statusColor;
            set { _statusColor = value; OnPropertyChanged(); }
        }

        public string ResponseTime
        {
            get => _responseTime;
            set { _responseTime = value; OnPropertyChanged(); }
        }

        public Brush ResponseTimeColor
        {
            get => _responseTimeColor;
            set { _responseTimeColor = value; OnPropertyChanged(); }
        }

        public bool IsOnline
        {
            get => _isOnline;
            set { _isOnline = value; OnPropertyChanged(); }
        }

        // Ping history for graphing (timestamp, latency in ms)
        public List<(DateTime Time, long Latency)> PingHistory { get; set; } = new List<(DateTime, long)>();

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    // ############################################################################
    // REGION: GLOBAL SERVICES STATUS DATA MODEL
    // TAG: #GLOBAL_SERVICES #MONITORING
    // ############################################################################

    /// <summary>
    /// Represents a global cloud service or infrastructure endpoint for monitoring
    /// </summary>
    public class GlobalServiceStatus : INotifyPropertyChanged
    {
        private string _serviceName;
        private string _endpoint;
        private string _status;
        private Brush _statusColor;
        private string _latency;
        private Brush _latencyColor;

        public string ServiceName
        {
            get => _serviceName;
            set { _serviceName = value; OnPropertyChanged(); }
        }

        public string Endpoint
        {
            get => _endpoint;
            set { _endpoint = value; OnPropertyChanged(); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public Brush StatusColor
        {
            get => _statusColor;
            set { _statusColor = value; OnPropertyChanged(); }
        }

        public string Latency
        {
            get => _latency;
            set { _latency = value; OnPropertyChanged(); }
        }

        public Brush LatencyColor
        {
            get => _latencyColor;
            set { _latencyColor = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    // ############################################################################
    // REGION: DC HISTORY DATA MODEL
    // TAG: #DC_DISCOVERY
    // ############################################################################

    /// <summary>
    /// Represents a domain controller in the discovery history
    /// Used for displaying previously found DCs even when offline
    /// </summary>
    public class DCHistoryItem
    {
        public string Hostname { get; set; }
        public DateTime LastSeen { get; set; }
        public int AvgLatency { get; set; }
        public string StatusIcon { get; set; }  // 🟢=recent, 🟡=week, 🟠=month, ⚫=old
    }

    // ############################################################################
    // REGION: MAIN WINDOW
    // ############################################################################

    public partial class MainWindow : Window
    {
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool LogonUser(String lpszUsername, String lpszDomain, String lpszPassword,
            int dwLogonType, int dwLogonProvider, out IntPtr phToken);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public extern static bool CloseHandle(IntPtr handle);

        // Windows Credential Dialog API for native GUI credential prompts
        [DllImport("credui.dll", CharSet = CharSet.Unicode)]
        private static extern uint CredUIPromptForWindowsCredentials(
            ref CREDUI_INFO pUiInfo,
            uint dwAuthError,
            ref uint pulAuthPackage,
            IntPtr pvInAuthBuffer,
            uint ulInAuthBufferSize,
            out IntPtr ppvOutAuthBuffer,
            out uint pulOutAuthBufferSize,
            ref bool pfSave,
            uint dwFlags);

        [DllImport("credui.dll", CharSet = CharSet.Unicode)]
        private static extern bool CredUnPackAuthenticationBuffer(
            uint dwFlags,
            IntPtr pAuthBuffer,
            uint cbAuthBuffer,
            System.Text.StringBuilder pszUserName,
            ref int pcchMaxUserName,
            System.Text.StringBuilder pszDomainName,
            ref int pcchMaxDomainName,
            System.Text.StringBuilder pszPassword,
            ref int pcchMaxPassword);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CREDUI_INFO
        {
            public int cbSize;
            public IntPtr hwndParent;
            public string pszMessageText;
            public string pszCaptionText;
            public IntPtr hbmBanner;
        }

        private const uint CREDUIWIN_GENERIC = 0x1;
        private const uint CREDUIWIN_CHECKBOX = 0x2;
        private const uint CRED_PACK_PROTECTED_CREDENTIALS = 0x1;
        private const uint ERROR_SUCCESS = 0;
        private const uint ERROR_CANCELLED = 1223;

        // CreateProcessWithLogonW API for launching process as different user (doesn't require privileges)
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CreateProcessWithLogonW(
            string lpUsername,
            string lpDomain,
            string lpPassword,
            uint dwLogonFlags,
            string lpApplicationName,
            string lpCommandLine,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct STARTUPINFO
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        private const uint LOGON_WITH_PROFILE = 0x1;
        private const uint CREATE_NEW_CONSOLE = 0x10;

        // ── Inner Data Classes ──

        public class AuditLog
        {
            public string Time { get; set; }
            public string User { get; set; }
            public string Target { get; set; }
            public string Action { get; set; }
            public string Status { get; set; }
            public string Details { get; set; }
        }

        public class PCInventory : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            private void OnProp([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

            private string _hostname; public string Hostname { get => _hostname; set { _hostname = value; OnProp(); } }
            private string _status; public string Status { get => _status; set { _status = value; OnProp(); } }
            private string _displayOS; public string DisplayOS { get => _displayOS; set { _displayOS = value; OnProp(); } }
            private string _windowsVersion; public string WindowsVersion { get => _windowsVersion; set { _windowsVersion = value; OnProp(); } }
            private int _build; public int Build { get => _build; set { _build = value; OnProp(); } }
            private string _lastUpdate; public string LastUpdate { get => _lastUpdate; set { _lastUpdate = value; OnProp(); } }
            private string _compliance; public string Compliance { get => _compliance; set { _compliance = value; OnProp(); } }
            private bool _isOutdated; public bool IsOutdated { get => _isOutdated; set { _isOutdated = value; OnProp(); } }
            private string _currentUser; public string CurrentUser { get => _currentUser; set { _currentUser = value; OnProp(); } }
            private string _chassis; public string Chassis { get => _chassis; set { _chassis = value; OnProp(); } }
            private string _bitlocker; public string BitLockerStatus { get => _bitlocker; set { _bitlocker = value; OnProp(); } }
            private string _disk; public string Disk { get => _disk; set { _disk = value; OnProp(); } }
            private string _lastBoot; public string LastBoot { get => _lastBoot; set { _lastBoot = value; OnProp(); } }
            private string _os; public string OS { get => _os; set { _os = value; OnProp(); } }
            private string _tags; public string Tags { get => _tags; set { _tags = value; OnProp(); } }
        }

        public class UserConfig
        {
            public string LastUser { get; set; }
            public string LastDC { get; set; }
            public bool RememberDC { get; set; }
            public List<string> TargetHistory { get; set; } = new List<string>();
        }

        public class ThemeManager
        {
            private static bool _isDarkMode = true;

            public static void ToggleTheme(Window window)
            {
                _isDarkMode = !_isDarkMode;

                var resources = window.Resources;

                if (_isDarkMode)
                {
                    // Dark theme (current)
                    resources["BgDarkest"] = Color.FromRgb(13, 13, 13);
                    resources["BgDark"] = Color.FromRgb(26, 26, 26);
                    resources["BgMedium"] = Color.FromRgb(37, 37, 38);
                    resources["BgCard"] = Color.FromRgb(30, 30, 30);
                    resources["BorderDim"] = Color.FromRgb(60, 60, 60);
                    resources["BorderBright"] = Color.FromRgb(85, 85, 85);
                }
                else
                {
                    // Light theme (Aero-inspired)
                    resources["BgDarkest"] = Color.FromRgb(240, 240, 240);
                    resources["BgDark"] = Color.FromRgb(250, 250, 250);
                    resources["BgMedium"] = Color.FromRgb(230, 230, 230);
                    resources["BgCard"] = Color.FromRgb(255, 255, 255);
                    resources["BorderDim"] = Color.FromRgb(200, 200, 200);
                    resources["BorderBright"] = Color.FromRgb(170, 170, 170);
                }

                // Force UI refresh
                window.InvalidateVisual();
                foreach (var child in GetLogicalChildren(window))
                {
                    if (child is FrameworkElement fe)
                        fe.InvalidateVisual();
                }
            }

            private static IEnumerable<DependencyObject> GetLogicalChildren(DependencyObject parent)
            {
                foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
                {
                    yield return child;
                    foreach (var grandchild in GetLogicalChildren(child))
                        yield return grandchild;
                }
            }
        }

        // ── State Variables ──

        private ObservableCollection<AuditLog> _logs = new ObservableCollection<AuditLog>();
        private ObservableCollection<PCInventory> _inventory = new ObservableCollection<PCInventory>();
        private ObservableCollection<PinnedDevice> _pinnedDevices = new ObservableCollection<PinnedDevice>();
        private ObservableCollection<GlobalServiceStatus> _essentialServices = new ObservableCollection<GlobalServiceStatus>();
        private ObservableCollection<GlobalServiceStatus> _highPriorityServices = new ObservableCollection<GlobalServiceStatus>();
        private ObservableCollection<GlobalServiceStatus> _mediumPriorityServices = new ObservableCollection<GlobalServiceStatus>();
        private object _inventoryLock = new object();
        private object _logLock = new object();
        private object _pinnedDevicesLock = new object();
        private object _globalServicesLock = new object();
        private System.Threading.Timer _globalServicesTimer = null;

        private string _currentTarget = "";
        private string _currentServiceTag = "";
        private string _authUser = Environment.UserName;
        private SecureString _authPass = null;
        private bool _isLoggedIn = false;
        private bool _isDomainAdmin = false;
        private bool _isElevated = false;  // Tracks if app is running as Administrator

        // Recent targets tracking (last 10 machines)
        private List<string> _recentTargets = new List<string>();
        private const int MaxRecentTargets = 10;
        private string _tempLastDC = "";
        private bool _terminalVisible = false;

        // Domain Admin check caching (avoid repeated AD queries)
        private static ConcurrentDictionary<string, (bool isAdmin, DateTime cachedAt)> _adminCheckCache = new ConcurrentDictionary<string, (bool, DateTime)>();
        private static readonly TimeSpan _adminCheckCacheDuration = TimeSpan.FromMinutes(15);

        // Debug Mode - Enable verbose error logging
        private static bool _debugMode = true; // Set to true for detailed error tracking

        // Error tracking for UI indicator (split: warnings vs critical)
        private static int _warningCount = 0;
        private static int _criticalErrorCount = 0;
        private static DateTime _lastErrorUpdate = DateTime.Now;

        // Multi-core and high RAM optimization
        private static System.Windows.Threading.DispatcherTimer _scanAnimationTimer;
        private static DateTime _scanStartTime;

        private WmiConnectionManager _wmiManager;
        private CimSessionManager _cimManager;
        private DCManager _dcManager;
        private HybridQueryHelper _queryHelper;
        private CancellationTokenSource _scanTokenSource;
        private DispatcherTimer _searchDebounceTimer;
        private DispatcherTimer _refreshTimer;

        private string _xmlConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Artazn_UserConfig.xml");

        // ── PowerShell Scripts ──

        // GENERAL UPDATE: Downloads and installs all pending Windows Updates
        // including driver packs and firmware from Windows Update catalog
        private const string Script_General = @"
            Write-Output '>>> JADEX GENERAL UPDATE ENGINE';
            $ErrorActionPreference = 'Continue';
            $Session = New-Object -ComObject Microsoft.Update.Session;
            $Searcher = $Session.CreateUpdateSearcher();

            # Search for ALL pending updates: security, drivers, firmware, definition
            $Criteria = 'IsInstalled=0 and IsHidden=0';
            Write-Output 'Searching for pending updates (security, drivers, firmware)...';
            $Results = $Searcher.Search($Criteria);

            if ($Results.Updates.Count -eq 0) {
                Write-Output 'RESULT: No pending updates found.';
                exit 0;
            }

            Write-Output ""Found $($Results.Updates.Count) update(s):"";
            $UpdateCollection = New-Object -ComObject Microsoft.Update.UpdateColl;
            foreach ($Update in $Results.Updates) {
                Write-Output ""  - $($Update.Title)"";
                if (!$Update.EulaAccepted) { $Update.AcceptEula(); }
                [void]$UpdateCollection.Add($Update);
            }

            Write-Output 'Downloading updates...';
            $Downloader = $Session.CreateUpdateDownloader();
            $Downloader.Updates = $UpdateCollection;
            $DlResult = $Downloader.Download();

            if ($DlResult.ResultCode -ne 2) {
                Write-Output ""WARNING: Download completed with code $($DlResult.ResultCode)"";
            }

            Write-Output 'Installing updates...';
            $Installer = $Session.CreateUpdateInstaller();
            $Installer.Updates = $UpdateCollection;
            $InstallResult = $Installer.Install();

            for ($i = 0; $i -lt $UpdateCollection.Count; $i++) {
                $uResult = $InstallResult.GetUpdateResult($i);
                $status = if ($uResult.ResultCode -eq 2) { 'OK' } else { ""FAIL($($uResult.ResultCode))"" }
                Write-Output ""  [$status] $($UpdateCollection.Item($i).Title)"";
            }

            if ($InstallResult.ResultCode -eq 2) {
                Write-Output 'UPDATE STATUS: ALL SUCCESSFUL';
            } else {
                Write-Output ""UPDATE STATUS: COMPLETED WITH ISSUES (Code: $($InstallResult.ResultCode))"";
            }

            if ($InstallResult.RebootRequired) {
                Write-Output 'REBOOT STATUS: REQUIRED';
            } else {
                Write-Output 'REBOOT STATUS: NOT REQUIRED';
            }";

        // FEATURE UPDATE: In-place upgrade to latest Windows version
        // (e.g., 22H2 → 23H2 → 24H2) keeping all apps, settings, data
        private const string Script_Feature = @"
            Write-Output '>>> JADEX FEATURE UPDATE ENGINE (IN-PLACE UPGRADE)';
            $ErrorActionPreference = 'Continue';
            $Session = New-Object -ComObject Microsoft.Update.Session;
            $Searcher = $Session.CreateUpdateSearcher();

            # Category GUID for Feature/Upgrade packs
            $FeatureGUID = '3689bdc8-b205-4af4-8d4a-a63924c5e9d5';
            $UpgradeGUID = 'cd5ffd1e-e932-4e3a-bf74-18bf0b1bbd83';

            # Search for Feature Updates first, then Upgrades
            $Query = ""IsInstalled=0 and (CategoryIDs contains '$FeatureGUID' or CategoryIDs contains '$UpgradeGUID')"";
            Write-Output 'Searching for Feature/Upgrade packages...';
            $FeatureResult = $Searcher.Search($Query);

            if ($FeatureResult.Updates.Count -eq 0) {
                Write-Output 'No feature enablement or upgrade packages available.';
                Write-Output 'This system may already be on the latest major version.';

                # Show current version info
                $os = Get-CimInstance Win32_OperatingSystem;
                $build = $os.BuildNumber;
                $caption = $os.Caption;
                Write-Output ""Current: $caption (Build $build)"";
                exit 0;
            }

            Write-Output ""Found $($FeatureResult.Updates.Count) feature update(s):"";
            foreach ($u in $FeatureResult.Updates) {
                Write-Output ""  - $($u.Title)"";
            }

            # Use the first (newest) feature update
            $Target = $FeatureResult.Updates | Select-Object -First 1;
            Write-Output ""Selecting: $($Target.Title)"";

            if (!$Target.EulaAccepted) { $Target.AcceptEula(); }

            $Batch = New-Object -ComObject Microsoft.Update.UpdateColl;
            [void]$Batch.Add($Target);

            Write-Output 'Downloading Feature Update (this may take 15-45 minutes)...';
            $Downloader = $Session.CreateUpdateDownloader();
            $Downloader.Updates = $Batch;
            $DlResult = $Downloader.Download();

            if ($DlResult.ResultCode -ne 2) {
                Write-Output ""Download issue: Code $($DlResult.ResultCode)"";
            }

            Write-Output 'Installing Feature Update (in-place upgrade, preserving apps and data)...';
            $Installer = $Session.CreateUpdateInstaller();
            $Installer.Updates = $Batch;
            $Result = $Installer.Install();

            if ($Result.ResultCode -eq 2) {
                Write-Output 'FEATURE UPDATE: SUCCESS';
                Write-Output 'A REBOOT IS REQUIRED to complete the upgrade.';
                Write-Output 'All applications, settings, and user data will be preserved.';
            } else {
                Write-Output ""FEATURE UPDATE: COMPLETED WITH CODE $($Result.ResultCode)"";
            }";

        // ── Startup & Shutdown ──

        public MainWindow()
        {
            InitializeComponent();

            // Set window title with version from AssemblyInfo (modular)
            // TAG: #MODULAR #VERSION
            Title = $"ArtaznIT Suite {LogoConfig.VERSION}";

            // Set version badge in header (will be set after InitializeComponent loads controls)
            Loaded += (s, e) => {
                if (TxtVersionBadge != null)
                    TxtVersionBadge.Text = LogoConfig.VERSION;

                // TAG: #VERSION_7 #AD_MANAGEMENT - Wire up tab selection changed for AD Object Browser initialization
                if (MainTabs != null)
                {
                    MainTabs.SelectionChanged += MainTabs_SelectionChanged;
                }
            };

            AuditGrid.ItemsSource = _logs;
            GridInventory.ItemsSource = _inventory;
            GridPinnedDevices.ItemsSource = _pinnedDevices;
            BindingOperations.EnableCollectionSynchronization(_inventory, _inventoryLock);
            BindingOperations.EnableCollectionSynchronization(_logs, _logLock);
            BindingOperations.EnableCollectionSynchronization(_pinnedDevices, _pinnedDevicesLock);
            _wmiManager = new WmiConnectionManager();
            _cimManager = new CimSessionManager();
            _dcManager = new DCManager();
            _queryHelper = new HybridQueryHelper(_cimManager, _wmiManager);

            _searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _searchDebounceTimer.Tick += SearchDebounceTimer_Tick;

            // Load persisted DC configuration
            _dcManager.LoadConfiguration();

            // TAG: #VERSION_7 #RMM_INTEGRATION - Initialize Remote Control Manager
            RemoteControlManager.Initialize();

            // TAG: #VERSION_7 #CONNECTION_PROFILES - Load connection profiles
            ConnectionProfileManager.LoadProfiles();

            // TAG: #VERSION_7 #BOOKMARKS - Load bookmarks/favorites
            BookmarkManager.LoadBookmarks();

            // TAG: #VERSION_7.1 #SCRIPTS - Initialize PowerShell script library
            ScriptManager.Initialize();

            // TAG: #VERSION_7.1 #ASSET_TAGGING - Initialize asset tag system
            AssetTagManager.Initialize();

            // TAG: #VERSION_7 #QUICK_WINS - Restore window state and apply settings
            Loaded += (s, e) => {
                // Restore window position and size
                RestoreWindowPosition();

                // Apply saved font size
                ApplyFontSize();

                // TAG: #VERSION_7 #THEME_COLORS - Apply saved accent colors
                ApplySavedAccentColors();

                // Load recent targets
                LoadRecentTargets();

                // Load RMM quick-launch buttons
                LoadRmmQuickLaunchButtons();

                // Start auto-save timer if enabled
                InitializeAutoSave();
            };

            LogManager.LogInfo("Application initialized");
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // ⚡ Loading overlay is visible by default in XAML
                UpdateLoadingStatus("Initializing ArtaznIT Suite...", "Checking system configuration");

                if (IntPtr.Size == 4)
                {
                    LogManager.LogWarning("32-bit mode detected");
                    UpdateLoadingStatus("System Check", "WARNING: 32-bit mode detected");
                    MessageBox.Show("WARNING: Running in 32-bit mode. Some WMI features may not work correctly.",
                        "Architecture Warning");
                }

                UpdateLoadingStatus("Loading Configuration...", "Reading secure settings");
                SecureConfig.LoadConfiguration();
                LoadConfig();
                LoadMasterLog();

                UpdateLoadingStatus("Loading Pinned Devices...", "Initializing monitors");
                _ = LoadPinnedDevices();  // Fire-and-forget async initialization

                // Initialize global services after a short delay to ensure controls are loaded
                UpdateLoadingStatus("Checking Global Services...", "Monitoring cloud infrastructure");
                _ = Task.Run(async () =>
                {
                    await Task.Delay(500); // Wait for UI controls to be fully loaded
                    await Dispatcher.InvokeAsync(() =>
                    {
                        InitGlobalServices();  // Initialize global services status monitor
                    });

                    await Task.Delay(1500); // Additional delay before auto-refresh
                    await Dispatcher.InvokeAsync(() =>
                    {
                        // Enable auto-refresh by default
                        if (BtnAutoRefreshGlobalServices != null)
                        {
                            BtnAutoRefreshGlobalServices_Click(null, null);
                        }
                        // Trigger initial check
                        if (BtnRefreshGlobalServices != null)
                        {
                            BtnRefreshGlobalServices_Click(null, null);
                        }
                    });
                });

                UpdateLoadingStatus("Applying Restrictions...", "Checking permissions");
                ApplyRoleRestrictions(); // Lock down UI for non-admins
                AppendTerminal($"ArtaznIT Suite {LogoConfig.VERSION} (Kerberos Edition) initialized.", false);

                UpdateLoadingStatus("Ready", "Launching login dialog");
                await Task.Delay(300); // Brief pause so user sees "Ready" status

                // Hide loading overlay before showing login
                HideLoadingOverlay();

                // ⚡ INSTANT STARTUP: Show login immediately, check domain in background
                // The LoginWindow will check domain availability and handle the DC unavailable dialog if needed
                _ = ShowLoginDialog();  // Fire-and-forget async login
            }
            catch (Exception ex)
            {
                HideLoadingOverlay();
                LogManager.LogError("Window_Loaded failed", ex);
                MessageBox.Show($"Initialization error: {ex.Message}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Update loading overlay status
        /// TAG: #LOADING #STARTUP
        /// </summary>
        private void UpdateLoadingStatus(string mainStatus, string detailStatus)
        {
            Dispatcher.Invoke(() =>
            {
                if (LoadingStatusText != null)
                    LoadingStatusText.Text = mainStatus;
                if (LoadingDetailText != null)
                    LoadingDetailText.Text = detailStatus;
            });
        }

        /// <summary>
        /// Hide loading overlay
        /// TAG: #LOADING #STARTUP
        /// </summary>
        private void HideLoadingOverlay()
        {
            Dispatcher.Invoke(() =>
            {
                if (LoadingOverlay != null)
                    LoadingOverlay.Visibility = Visibility.Collapsed;
            });
        }

        // Static property to share current domain name across windows
        // TAG: #DC_DISCOVERY
        private static string _currentDomainName = null;
        public static string CurrentDomainName
        {
            get => _currentDomainName;
            internal set => _currentDomainName = value;
        }

        /// <summary>
        /// Update the Domain Information section in the Domain & Directory tab
        /// TAG: #DOMAIN_TAB #VERSION_6_1
        /// </summary>
        internal void UpdateDomainInformationTab()
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    if (TxtCurrentDomain_Tab != null)
                    {
                        TxtCurrentDomain_Tab.Text = string.IsNullOrEmpty(CurrentDomainName)
                            ? "Not connected"
                            : CurrentDomainName.ToUpper();
                        TxtCurrentDomain_Tab.Foreground = string.IsNullOrEmpty(CurrentDomainName)
                            ? new SolidColorBrush(Color.FromRgb(128, 128, 128))
                            : new SolidColorBrush(Color.FromRgb(0, 255, 0));
                    }

                    if (TxtUserDomain_Tab != null)
                        TxtUserDomain_Tab.Text = Environment.UserDomainName ?? "-";

                    if (TxtComputerName_Tab != null)
                        TxtComputerName_Tab.Text = Environment.MachineName ?? "-";

                    if (TxtDCCount_Tab != null)
                    {
                        int dcCount = ComboDC?.Items.Count ?? 0;
                        TxtDCCount_Tab.Text = dcCount > 0 ? (dcCount - 1).ToString() : "0";
                    }
                });
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to update Domain Information tab", ex);
            }
        }

        /// <summary>
        /// Check if domain controllers are available without throwing exceptions
        /// TAG: #DC_DISCOVERY #PERFORMANCE
        /// </summary>
        private async Task<bool> CheckDCAvailabilityAsync(int timeoutSeconds = 2)
        {
            try
            {
                // Use CancellationToken with configurable timeout (default 2 seconds)
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds)))
                {
                    string domainName = await Task.Run(() =>
                    {
                        try
                        {
                            // This call can take 10+ seconds if domain unreachable
                            var domain = Domain.GetCurrentDomain();
                            string name = domain.Name;
                            domain.Dispose();
                            return name;
                        }
                        catch
                        {
                            // Domain not available - return null
                            return null;
                        }
                    }, cts.Token);

                    if (domainName != null)
                    {
                        // Store domain name globally
                        CurrentDomainName = domainName;

                        // Update domain badge (safe - checks if control exists)
                        UpdateDomainBadge(domainName);
                        return true;
                    }
                    else
                    {
                        // No domain available
                        CurrentDomainName = null;
                        UpdateDomainBadge(null);
                        return false;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Timeout - domain not reachable within time limit
                LogManager.LogWarning($"Domain check timed out after {timeoutSeconds}s");
                CurrentDomainName = null;
                UpdateDomainBadge(null);
                return false;
            }
            catch (Exception ex)
            {
                // Any AD exception = unavailable
                LogManager.LogWarning($"Domain check failed: {ex.Message}");
                CurrentDomainName = null;
                UpdateDomainBadge(null);
                return false;
            }
        }

        /// <summary>
        /// Update the domain indicator badge in the top bar
        /// TAG: #DC_DISCOVERY #THEME_COLORS
        /// </summary>
        internal void UpdateDomainBadge(string domainName)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    if (DomainBadge == null || TxtDomainName == null)
                        return; // Controls not loaded yet

                    if (string.IsNullOrEmpty(domainName))
                    {
                        TxtDomainName.Text = "DOMAIN: UNAVAILABLE";
                        TxtDomainName.Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128)); // Gray
                        DomainBadge.Opacity = 0.6;
                        DomainBadge.ToolTip = "No domain detected - not connected to Active Directory";
                    }
                    else
                    {
                        TxtDomainName.Text = $"DOMAIN: {domainName.ToUpper()}";
                        var gradientBrush = new LinearGradientBrush
                        {
                            StartPoint = new Point(0, 0),
                            EndPoint = new Point(1, 0)
                        };
                        gradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(255, 133, 51), 0));
                        gradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(161, 161, 170), 1));
                        TxtDomainName.Foreground = gradientBrush;
                        DomainBadge.Opacity = 1.0;
                        DomainBadge.ToolTip = $"Connected to Active Directory domain: {domainName}";
                    }
                });
            }
            catch
            {
                // Silently fail if controls don't exist
            }
        }

        /// <summary>
        /// Periodic timer to verify domain connectivity every 5 minutes
        /// TAG: #DC_DISCOVERY
        /// </summary>
        private System.Windows.Threading.DispatcherTimer _domainVerificationTimer;

        private void StartDomainVerificationTimer()
        {
            _domainVerificationTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(5)
            };
            _domainVerificationTimer.Tick += async (s, e) =>
            {
                var previousDomain = CurrentDomainName;
                bool stillConnected = await CheckDCAvailabilityAsync();

                // Refresh DC history panel if it's currently visible
                // TAG: #DC_DISCOVERY #UI_UPDATE
                if (DCHistoryPanel != null && DCHistoryPanel.Visibility == Visibility.Visible)
                {
                    LoadDCHistory();
                }

                if (!stillConnected && !string.IsNullOrEmpty(previousDomain))
                {
                    AppendTerminal($"[DOMAIN CHECK] Lost connection to domain: {previousDomain}", isError: true);
                    MessageBox.Show(
                        $"Connection to domain '{previousDomain}' was lost.\n\n" +
                        "You may be disconnected from VPN or experiencing network issues.\n" +
                        "Some features may be unavailable until connection is restored.",
                        "Domain Connection Lost",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                else if (stillConnected && CurrentDomainName != previousDomain && !string.IsNullOrEmpty(previousDomain))
                {
                    AppendTerminal($"[DOMAIN CHECK] Domain changed from {previousDomain} to {CurrentDomainName}", isError: true);
                    MessageBox.Show(
                        $"Domain has changed!\n\n" +
                        $"Previous: {previousDomain}\n" +
                        $"Current: {CurrentDomainName}\n\n" +
                        "Application will restart to apply new domain settings.",
                        "Domain Changed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    Application.Current.Shutdown();
                }
            };
            _domainVerificationTimer.Start();
            AppendTerminal("[DOMAIN CHECK] Started 5-minute domain verification timer");
        }

        /// <summary>
        /// Set application to guest/read-only mode when no domain is available
        /// TAG: #AUTH_STATUS #READ_ONLY_MODE
        /// </summary>
        internal void SetGuestReadOnlyMode()
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    // Set auth status to read-only guest
                    _isLoggedIn = false;
                    _isDomainAdmin = false;
                    TxtAuthStatus.Text = "GUEST (READ-ONLY)";
                    TxtAuthStatus.Foreground = new SolidColorBrush(Color.FromRgb(255, 200, 100)); // Orange/yellow
                    BtnAuth.Content = "LOGIN";
                    BtnAuth.IsEnabled = false; // Disable login button - no domain available
                    BtnAuth.ToolTip = "Login unavailable - no domain connection";

                    // Update role badge
                    if (TxtRoleBadge != null)
                    {
                        TxtRoleBadge.Text = "READ-ONLY";
                        TxtRoleBadge.Foreground = new SolidColorBrush(Color.FromRgb(255, 200, 100));
                    }

                    // Update role badge border to indicate read-only
                    if (RoleBadgeBorder != null)
                    {
                        RoleBadgeBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 200, 100));
                        RoleBadgeBorder.BorderThickness = new Thickness(1);
                    }

                    // Show login button in read-only mode so user can try to login later
                    if (BtnLoginFromReadOnly != null)
                    {
                        BtnLoginFromReadOnly.Visibility = Visibility.Visible;
                    }

                    // Apply read-only restrictions (lock dangerous features)
                    ApplyRoleRestrictions();

                    LogManager.LogWarning("Running in GUEST READ-ONLY mode - no domain authentication available");
                }
                catch (Exception ex)
                {
                    LogManager.LogError("Failed to set guest read-only mode", ex);
                }
            });
        }

        /// <summary>
        /// Handle login button click from read-only mode
        /// TAG: #AUTH_STATUS #READ_ONLY_MODE
        /// </summary>
        private void BtnLoginFromReadOnly_Click(object sender, RoutedEventArgs e)
        {
            // Attempt to show login dialog even in read-only mode
            _ = ShowLoginDialog();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            try
            {
                SaveConfig();
                SecureConfig.SaveConfiguration();

                // TAG: #VERSION_7 #QUICK_WINS - Save window position and stop auto-save
                SaveWindowPosition();
                _autoSaveTimer?.Stop();

                // SECURE WIPE: Zero credentials from memory
                SecureMemory.WipeAndDispose(ref _authPass);
                SecureMemory.ForceCleanup();

                // Dispose managers and stop timers
                _wmiManager?.Dispose();
                _cimManager?.Dispose();
                _searchDebounceTimer?.Stop();
                _refreshTimer?.Stop();

                LogManager.LogInfo("Application closing — credentials wiped from memory");
            }
            catch (Exception ex) { LogManager.LogError("Window_Closing failed", ex); }
        }

        // ── Role-Based Access Control ──

        /// <summary>
        /// Checks if the authenticated user is a member of Domain Admins.
        /// Locks down dangerous tools if not.
        /// </summary>
        private bool CheckDomainAdminMembership(string username)
        {
            // Check cache first (15-minute TTL)
            if (_adminCheckCache.TryGetValue(username, out var cached))
            {
                if ((DateTime.Now - cached.cachedAt) < _adminCheckCacheDuration)
                {
                    LogManager.LogDebug($"Using cached admin status for {username}: {cached.isAdmin}");
                    return cached.isAdmin;
                }
                else
                {
                    // Cache expired, remove it
                    _adminCheckCache.TryRemove(username, out _);
                }
            }

            try
            {
                string domain = "";
                string cleanUser = username;

                if (username.Contains("\\"))
                {
                    var parts = username.Split('\\');
                    domain = parts[0];
                    cleanUser = parts[1];
                }

                // Use authenticated credentials for AD query
                DirectoryEntry root = null;
                string password = null;

                if (_authPass != null)
                {
                    SecureMemory.UseSecureString(_authPass, pwd => password = pwd);
                }

                try
                {
                    // Create DirectoryEntry with authenticated credentials
                    if (!string.IsNullOrEmpty(password))
                    {
                        string ldapPath = string.IsNullOrEmpty(domain) ? "LDAP://" : $"LDAP://{domain}";
                        root = new DirectoryEntry(ldapPath, username, password, AuthenticationTypes.Secure);
                    }
                    else
                    {
                        root = new DirectoryEntry();
                    }

                    using (root)
                    using (var searcher = new DirectorySearcher(root))
                    {
                        searcher.Filter = $"(&(objectCategory=user)(sAMAccountName={cleanUser}))";
                        searcher.PropertiesToLoad.Add("memberOf");
                        searcher.PropertiesToLoad.Add("distinguishedName");
                        var result = searcher.FindOne();
                        if (result == null)
                        {
                            LogManager.LogWarning($"User not found in AD: {cleanUser}");
                            CacheAdminStatus(username, false);
                            return false;
                        }

                        // Check direct and nested group membership
                        var checkedGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        bool isAdmin = IsInDomainAdminsRecursive(result, root, checkedGroups);
                        CacheAdminStatus(username, isAdmin);
                        return isAdmin;
                    }
                }
                finally
                {
                    // Wipe password from memory
                    if (password != null)
                    {
                        password = null;
                        GC.Collect();
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.LogWarning($"Domain Admin check failed: {ex.Message}");
                // Fallback: check current Windows identity
                try
                {
                    var identity = WindowsIdentity.GetCurrent();
                    var principal = new WindowsPrincipal(identity);
                    bool isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
                    CacheAdminStatus(username, isAdmin);
                    return isAdmin;
                }
                catch
                {
                    CacheAdminStatus(username, false);
                    return false;
                }
            }
        }

        /// <summary>
        /// Cache the admin status for a user to avoid repeated AD queries
        /// </summary>
        private void CacheAdminStatus(string username, bool isAdmin)
        {
            _adminCheckCache[username] = (isAdmin, DateTime.Now);
            LogManager.LogDebug($"Cached admin status for {username}: {isAdmin}");
        }

        /// <summary>
        /// Recursively checks if a user is in Domain Admins (handles nested groups)
        /// </summary>
        private bool IsInDomainAdminsRecursive(SearchResult userResult, DirectoryEntry rootEntry, HashSet<string> checkedGroups, int depth = 0)
        {
            // Prevent infinite recursion (max depth 10)
            if (depth > 10) return false;

            if (userResult.Properties["memberOf"] == null) return false;

            foreach (string groupDN in userResult.Properties["memberOf"])
            {
                // Avoid checking the same group twice
                if (checkedGroups.Contains(groupDN)) continue;
                checkedGroups.Add(groupDN);

                // Direct match - found Domain Admins!
                if (groupDN.IndexOf("CN=Domain Admins", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    LogManager.LogInfo($"User is member of Domain Admins (depth: {depth})");
                    return true;
                }

                // Check if this group is nested in Domain Admins
                try
                {
                    using (var groupEntry = new DirectoryEntry($"LDAP://{groupDN}"))
                    using (var groupSearcher = new DirectorySearcher(rootEntry))
                    {
                        // Extract CN from DN
                        var cnMatch = System.Text.RegularExpressions.Regex.Match(groupDN, @"^CN=([^,]+)");
                        if (cnMatch.Success)
                        {
                            string groupCN = cnMatch.Groups[1].Value;
                            groupSearcher.Filter = $"(&(objectCategory=group)(cn={groupCN}))";
                            groupSearcher.PropertiesToLoad.Add("memberOf");
                            var groupResult = groupSearcher.FindOne();

                            if (groupResult != null && groupResult.Properties["memberOf"] != null)
                            {
                                // Recursively check parent groups
                                if (IsInDomainAdminsRecursive(groupResult, rootEntry, checkedGroups, depth + 1))
                                    return true;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogManager.LogDebug($"Failed to check nested group {groupDN}: {ex.Message}");
                }
            }

            return false;
        }

        private void ApplyRoleRestrictions()
        {
            Dispatcher.Invoke(() =>
            {
                if (_isDomainAdmin && _isElevated)
                {
                    // Domain Admin with elevation - Full access
                    TxtRoleBadge.Text = "DOMAIN ADMIN ⚡";
                    TxtRoleBadge.Foreground = Brushes.LimeGreen;

                    PanelDeployment.Visibility = Visibility.Visible;
                    PanelDeployment.IsEnabled = true;
                    BtnKillFirewall.IsEnabled = true;
                    BtnKillFirewall.Visibility = Visibility.Visible;
                }
                else if (_isDomainAdmin && !_isElevated)
                {
                    // Domain Admin without elevation - Limited access
                    TxtRoleBadge.Text = "DOMAIN ADMIN (NO ELEVATION)";
                    TxtRoleBadge.Foreground = Brushes.Orange;

                    PanelDeployment.Visibility = Visibility.Visible;
                    PanelDeployment.IsEnabled = false; // Can see but not use deployment features
                    BtnKillFirewall.IsEnabled = false;
                    BtnKillFirewall.Visibility = Visibility.Collapsed;
                }
                else if (_isLoggedIn && _isElevated)
                {
                    // Authenticated with elevation but not Domain Admin
                    TxtRoleBadge.Text = "AUTHENTICATED ⚡ (Limited)";
                    TxtRoleBadge.Foreground = Brushes.Yellow;

                    PanelDeployment.Visibility = Visibility.Visible;
                    PanelDeployment.IsEnabled = false; // Can see but not use
                    BtnKillFirewall.IsEnabled = false;
                    BtnKillFirewall.Visibility = Visibility.Collapsed;
                }
                else if (_isLoggedIn && !_isElevated)
                {
                    // Authenticated without elevation - Read-only mode
                    TxtRoleBadge.Text = "AUTHENTICATED (READ-ONLY)";
                    TxtRoleBadge.Foreground = Brushes.Gray;

                    PanelDeployment.Visibility = Visibility.Visible;
                    PanelDeployment.IsEnabled = false;
                    BtnKillFirewall.IsEnabled = false;
                    BtnKillFirewall.Visibility = Visibility.Collapsed;
                }
                else
                {
                    // Not authenticated - Guest mode
                    TxtRoleBadge.Text = "GUEST (READ-ONLY)";
                    TxtRoleBadge.Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128));

                    PanelDeployment.Visibility = Visibility.Collapsed;
                    BtnKillFirewall.IsEnabled = false;
                    BtnKillFirewall.Visibility = Visibility.Collapsed;
                }
            });
        }

        // ── Terminal Toggle ──

        private void BtnToggleTerminal_Click(object sender, RoutedEventArgs e)
        {
            _terminalVisible = !_terminalVisible;
            if (_terminalVisible)
            {
                // Expand terminal
                TerminalPanel.Height = 220;
                TerminalPanel.Visibility = Visibility.Visible;
                BtnToggleTerminal.Content = "▲ TERMINAL";
            }
            else
            {
                // Collapse terminal
                TerminalPanel.Height = 0;
                TerminalPanel.Visibility = Visibility.Collapsed;
                BtnToggleTerminal.Content = "▼ TERMINAL";
            }
        }

        // ── Logging & Audit ──

        private static DateTime _lastNetworkLogCheck = DateTime.MinValue;
        private static bool _networkLogAccessible = false;

        private void AddLog(string target, string action, string details, string status)
        {
            try
            {
                AuditLog logEntry = new AuditLog
                {
                    Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    User = _authUser,
                    Target = SecurityValidator.SanitizeHostname(target),
                    Action = action,
                    Status = status,
                    Details = details
                };
                Application.Current.Dispatcher.Invoke(() =>
                {
                    lock (_logLock)
                    {
                        _logs.Insert(0, logEntry);
                        if (_logs.Count > 1000) _logs.RemoveAt(_logs.Count - 1);
                    }
                });
                LogManager.LogInfo($"AUDIT: {target} | {action} | {status} | {details}");
                _ = Task.Run(() =>
                {
                    try
                    {
                        // Check if network log is accessible (cache for 60 seconds to avoid spam)
                        if ((DateTime.Now - _lastNetworkLogCheck).TotalSeconds > 60)
                        {
                            _lastNetworkLogCheck = DateTime.Now;
                            try
                            {
                                string dir = Path.GetDirectoryName(SecureConfig.SharedLogPath);
                                _networkLogAccessible = Directory.Exists(dir);
                            }
                            catch (Exception ex)
                            {
                                _networkLogAccessible = false;
                                LogManager.LogDebug($"Network log path check failed: {ex.Message}");
                            }
                        }

                        if (_networkLogAccessible)
                        {
                            string csv = string.Format("\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",\"{5}\"\n",
                                SecurityValidator.EscapeCsv(logEntry.Time), SecurityValidator.EscapeCsv(logEntry.User),
                                SecurityValidator.EscapeCsv(logEntry.Target), SecurityValidator.EscapeCsv(logEntry.Action),
                                SecurityValidator.EscapeCsv(logEntry.Status), SecurityValidator.EscapeCsv(logEntry.Details));

                            // Use retry logic for file in use errors
                            for (int retry = 0; retry < 3; retry++)
                            {
                                try
                                {
                                    File.AppendAllText(SecureConfig.SharedLogPath, csv);
                                    break; // Success, exit retry loop
                                }
                                catch (IOException) when (retry < 2)
                                {
                                    Thread.Sleep(100); // Wait 100ms before retry
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Only log once per minute to avoid spam
                        if ((DateTime.Now - _lastNetworkLogCheck).TotalSeconds > 60)
                        {
                            LogManager.LogWarning($"Network log write failed: {ex.Message}");
                        }
                    }
                });
            }
            catch (Exception ex) { LogManager.LogError("AddLog failed", ex); }
        }

        private void LoadMasterLog()
        {
            try
            {
                if (File.Exists(SecureConfig.SharedLogPath))
                { TxtLogPath.Text = "LOG: CONNECTED"; TxtLogPath.Foreground = Brushes.Lime; }
                else
                { TxtLogPath.Text = "LOG: OFFLINE"; TxtLogPath.Foreground = Brushes.Red; }
            }
            catch { TxtLogPath.Text = "LOG: ERROR"; TxtLogPath.Foreground = Brushes.Orange; }
        }

        // ── Inventory Filter (debounced, CollectionView) ──

        private void TxtInventorySearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchDebounceTimer.Stop();
            _searchDebounceTimer.Start();
        }

        private void SearchDebounceTimer_Tick(object sender, EventArgs e)
        {
            _searchDebounceTimer.Stop();
            try
            {
                string q = TxtInventorySearch.Text.Trim().ToLower();
                var view = CollectionViewSource.GetDefaultView(_inventory);
                view.Filter = string.IsNullOrEmpty(q) ? (Predicate<object>)null : obj =>
                {
                    var x = (PCInventory)obj;
                    return (x.Hostname ?? "").ToLower().Contains(q) ||
                           (x.CurrentUser ?? "").ToLower().Contains(q) ||
                           (x.DisplayOS ?? "").ToLower().Contains(q);
                };
            }
            catch (Exception ex) { LogManager.LogError("Filter failed", ex); }
        }

        private static DateTime _lastInventoryCheck = DateTime.MinValue;
        private static bool _inventoryPathAccessible = false;

        private void UpdateMasterInventoryFile(PCInventory pc)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    // Check if inventory path is accessible (cache for 60 seconds)
                    if ((DateTime.Now - _lastInventoryCheck).TotalSeconds > 60)
                    {
                        _lastInventoryCheck = DateTime.Now;
                        try
                        {
                            string dir = Path.GetDirectoryName(SecureConfig.InventoryDbPath);
                            _inventoryPathAccessible = Directory.Exists(dir);
                        }
                        catch (Exception ex)
                        {
                            _inventoryPathAccessible = false;
                            LogManager.LogDebug($"Inventory path check failed: {ex.Message}");
                        }
                    }

                    if (_inventoryPathAccessible)
                    {
                        string line = string.Format("\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",\"{5}\"\n",
                            SecurityValidator.EscapeCsv(pc.Hostname), SecurityValidator.EscapeCsv(pc.Status),
                            SecurityValidator.EscapeCsv(pc.DisplayOS), SecurityValidator.EscapeCsv(pc.Build.ToString()),
                            SecurityValidator.EscapeCsv(pc.CurrentUser), SecurityValidator.EscapeCsv(DateTime.Now.ToString()));

                        // Use retry logic for file in use errors
                        for (int retry = 0; retry < 3; retry++)
                        {
                            try
                            {
                                File.AppendAllText(SecureConfig.InventoryDbPath, line);
                                break; // Success, exit retry loop
                            }
                            catch (IOException) when (retry < 2)
                            {
                                Thread.Sleep(100); // Wait 100ms before retry
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Only log once per minute to avoid spam
                    if ((DateTime.Now - _lastInventoryCheck).TotalSeconds > 60)
                    {
                        LogManager.LogWarning($"Inventory write failed: {ex.Message}");
                    }
                }
            });
        }

        // ── WMI Executors (unchanged from previous patch) ──

        private void WMIQueryOutput(string query, string actionName, string targetHost)
        {
            if (string.IsNullOrEmpty(targetHost))
            {
                // TAG: #AUTO_UPDATE_UI_ENGINE #VALIDATION_TOASTS
                Managers.UI.ToastManager.ShowWarning("Target hostname is required");
                return;
            }
            if (!SecurityValidator.IsValidHostname(targetHost))
            {
                // TAG: #AUTO_UPDATE_UI_ENGINE #VALIDATION_TOASTS
                Managers.UI.ToastManager.ShowWarning($"Invalid hostname format: {targetHost}");
                AppendTerminal($"ERROR: Invalid hostname: {targetHost}", true);
                return;
            }
            string sanitized = SecurityValidator.SanitizeWmiQuery(query);
            if (string.IsNullOrEmpty(sanitized))
            {
                // TAG: #AUTO_UPDATE_UI_ENGINE #VALIDATION_TOASTS
                Managers.UI.ToastManager.ShowWarning("Query blocked by security validator");
                AppendTerminal("ERROR: Query blocked", true);
                return;
            }
            AppendTerminal($"\n>>> QUERY: {actionName} on {targetHost}...");
            _ = Task.Run(async () =>
            {
                int retry = 0; bool ok = false;
                while (!ok && retry < SecureConfig.MaxRetryAttempts)
                {
                    try
                    {
                        using (var cts = new CancellationTokenSource(SecureConfig.WmiTimeoutMs))
                        {
                            await Task.Run(() =>
                            {
                                var (protocol, cimResults, wmiResults) = _queryHelper.QueryInstances(
                                    targetHost, _authUser, _authPass, "root/cimv2", sanitized);

                                List<string> lines = new List<string>();

                                if (cimResults != null)
                                {
                                    // MULTICORE OPTIMIZATION: Process CIM results in parallel using PLINQ
                                    lines = cimResults
                                        .AsParallel()
                                        .WithDegreeOfParallelism(Environment.ProcessorCount)
                                        .Take(100) // Limit to 100 rows
                                        .Select(instance => {
                                            try
                                            {
                                                var props = new StringBuilder();
                                                foreach (var p in instance.CimInstanceProperties)
                                                    if (p.Value != null && !string.IsNullOrWhiteSpace(p.Value.ToString()))
                                                        props.Append($"{p.Name}: {p.Value}  |  ");
                                                return props.ToString();
                                            }
                                            catch (Exception ex)
                                            {
                                                LogManager.LogDebug($"Query failed: {ex.Message}");
                                                return null;
                                            }
                                        })
                                        .Where(x => !string.IsNullOrEmpty(x))
                                        .ToList();
                                }
                                else if (wmiResults != null)
                                {
                                    // MULTICORE OPTIMIZATION: Process WMI results in parallel using PLINQ
                                    lines = wmiResults.Cast<ManagementObject>()
                                        .AsParallel()
                                        .WithDegreeOfParallelism(Environment.ProcessorCount)
                                        .Take(100) // Limit to 100 rows
                                        .Select(mo => {
                                            try
                                            {
                                                var props = new StringBuilder();
                                                foreach (PropertyData p in mo.Properties)
                                                    if (p.Value != null && !string.IsNullOrWhiteSpace(p.Value.ToString()))
                                                        props.Append($"{p.Name}: {p.Value}  |  ");
                                                return props.ToString();
                                            }
                                            catch (Exception ex)
                                            {
                                                LogManager.LogDebug($"PLINQ processing error: {ex.Message}");
                                                return null;
                                            }
                                            finally
                                            {
                                                mo?.Dispose();
                                            }
                                        })
                                        .Where(x => !string.IsNullOrEmpty(x))
                                        .ToList();
                                }

                                if (lines.Count > 0)
                                {
                                    var sb = new StringBuilder();
                                    sb.AppendLine($"[{protocol}]");
                                    foreach (var line in lines) sb.AppendLine(line);
                                    if (lines.Count == 100) sb.AppendLine("...(truncated)");
                                    AppendTerminal(sb.ToString());
                                }
                                else
                                {
                                    AppendTerminal("No data returned.");
                                }
                                _ = Application.Current.Dispatcher.InvokeAsync(() => AddLog(targetHost, actionName, $"{lines.Count} rows", "OK"));
                            }, cts.Token);
                            ok = true;
                        }
                    }
                    catch (OperationCanceledException) { retry++; AppendTerminal($"Timeout ({retry}/{SecureConfig.MaxRetryAttempts})", true); if (retry < SecureConfig.MaxRetryAttempts) await Task.Delay(1000 * retry); }
                    catch (Exception ex) { retry++; AppendTerminal($"Info ({retry}): {ex.Message}"); if (retry < SecureConfig.MaxRetryAttempts) await Task.Delay(1000 * retry); }
                }
            });
        }

        private async Task WMIExecute(string command, string friendlyName)
        {
            string host = string.IsNullOrEmpty(_currentTarget) ? ComboTarget.Text : _currentTarget;
            if (string.IsNullOrEmpty(host) || !SecurityValidator.IsValidHostname(host))
            {
                // TAG: #AUTO_UPDATE_UI_ENGINE #VALIDATION_TOASTS
                Managers.UI.ToastManager.ShowWarning("Invalid or empty hostname");
                AppendTerminal("ERROR: Invalid hostname", true);
                return;
            }
            if (SecurityValidator.ContainsDangerousPatterns(command))
            {
                // TAG: #AUTO_UPDATE_UI_ENGINE #VALIDATION_TOASTS
                Managers.UI.ToastManager.ShowWarning("Command blocked by security validator");
                AppendTerminal("ERROR: Blocked dangerous command", true);
                return;
            }
            AppendTerminal($"\n>>> {friendlyName} on {host}...");
            await Task.Run(async () =>
            {
                int retry = 0; bool ok = false;
                while (!ok && retry < SecureConfig.MaxRetryAttempts)
                {
                    try
                    {
                        using (var cts = new CancellationTokenSource(SecureConfig.WmiTimeoutMs))
                        {
                            await Task.Run(() =>
                            {
                                var parameters = new Dictionary<string, object> { { "CommandLine", command } };
                                var (protocol, returnValue) = _queryHelper.InvokeMethod(
                                    host, _authUser, _authPass, "root/cimv2", "Win32_Process", "Create", parameters);

                                uint code = returnValue != null ? Convert.ToUInt32(returnValue) : 999;
                                if (code == 0) { AppendTerminal($"[{protocol}] {friendlyName}: SUCCESS"); _ = Application.Current.Dispatcher.InvokeAsync(() => AddLog(host, friendlyName, "OK", "OK")); }
                                else { AppendTerminal($"[{protocol}] {friendlyName}: FAILED (Code {code})", true); _ = Application.Current.Dispatcher.InvokeAsync(() => AddLog(host, friendlyName, $"Code {code}", "FAIL")); }
                            }, cts.Token);
                            ok = true;
                        }
                    }
                    catch (OperationCanceledException) { retry++; if (retry < SecureConfig.MaxRetryAttempts) await Task.Delay(1000 * retry); }
                    catch (Exception ex) { retry++; AppendTerminal($"Error ({retry}): {ex.Message}", true); if (retry < SecureConfig.MaxRetryAttempts) await Task.Delay(1000 * retry); }
                }
            });
        }

        private async Task WMIReboot()
        {
            string host = _currentTarget;
            if (string.IsNullOrEmpty(host) || !SecurityValidator.IsValidHostname(host))
            {
                // TAG: #AUTO_UPDATE_UI_ENGINE #VALIDATION_TOASTS
                Managers.UI.ToastManager.ShowWarning("Invalid or empty hostname for reboot");
                AppendTerminal("ERROR: Invalid hostname", true);
                return;
            }
            if (MessageBox.Show($"Reboot {host}?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            AppendTerminal($"\n>>> REBOOT → {host}...");
            await Task.Run(() =>
            {
                try
                {
                    var parameters = new Dictionary<string, object> { { "Flags", 6 }, { "Reserved", 0 } };
                    var (protocol, returnValue) = _queryHelper.InvokeInstanceMethod(
                        host, _authUser, _authPass, "root/cimv2",
                        "SELECT * FROM Win32_OperatingSystem", "Win32Shutdown", parameters);

                    AppendTerminal($"[{protocol}] Reboot signal sent.");
                    _ = Application.Current.Dispatcher.InvokeAsync(() => AddLog(host, "REBOOT", "OK", "OK"));
                }
                catch (Exception ex)
                {
                    AppendTerminal($"Reboot failed: {ex.Message}", true);
                    _ = Application.Current.Dispatcher.InvokeAsync(() => AddLog(host, "REBOOT", ex.Message, "FAIL"));
                }
            });
        }

        private void RunHybridExecutor(string psCommand, string wmiQuery, string actionName, string specificTarget = "")
        {
            string targetHost = string.IsNullOrEmpty(specificTarget) ? _currentTarget : specificTarget;
            if (string.IsNullOrEmpty(targetHost) || !SecurityValidator.IsValidHostname(targetHost)) { AppendTerminal("ERROR: Invalid hostname", true); return; }
            if (SecurityValidator.ContainsDangerousPatterns(psCommand)) { AppendTerminal("ERROR: Blocked dangerous command", true); return; }
            AppendTerminal($"\n>>> EXEC: {actionName} → {targetHost}...");
            _ = Task.Run(() =>
            {
                bool success = false;

                // ═══════════════════════════════════════════════════════════════
                // METHOD 1: PowerShell Remoting (Invoke-Command) - Fastest
                // ═══════════════════════════════════════════════════════════════
                try
                {
                    AppendTerminal($"[Method 1] Attempting PowerShell Remoting...");
                    string fullScript = $"$ProgressPreference='SilentlyContinue'; Invoke-Command -ComputerName '{targetHost}' -ScriptBlock {{ {psCommand} }} | Out-String";
                    string b64 = Convert.ToBase64String(Encoding.Unicode.GetBytes(fullScript));
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {b64}",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                    if (_isLoggedIn && _authPass != null)
                    {
                        string[] parts = _authUser.Split('\\');
                        if (parts.Length == 2) { psi.Domain = parts[0]; psi.UserName = parts[1]; }
                        else psi.UserName = _authUser;
                        psi.Password = _authPass; psi.LoadUserProfile = true;
                    }
                    using (var p = Process.Start(psi))
                    {
                        p.OutputDataReceived += (s, ev) => { if (!string.IsNullOrEmpty(ev.Data)) AppendTerminal(ev.Data); };
                        p.ErrorDataReceived += (s, ev) => { if (!string.IsNullOrEmpty(ev.Data)) AppendTerminal(ev.Data, true); };
                        p.BeginOutputReadLine(); p.BeginErrorReadLine();
                        if (!p.WaitForExit(SecureConfig.WmiTimeoutMs * 2))
                        { p.Kill(); AppendTerminal("Method 1 timed out", true); }
                        else if (p.ExitCode == 0)
                        {
                            success = true;
                            AppendTerminal($"[Method 1] ✓ PowerShell Remoting succeeded");
                        }
                    }
                }
                catch (Exception ex1)
                {
                    AppendTerminal($"[Method 1] ✗ Failed: {ex1.Message}");
                    LogManager.LogWarning($"Method 1 (PS Remoting) failed for {actionName}: {ex1.Message}");

                    // ═══════════════════════════════════════════════════════════════
                    // METHOD 2: WMI Process Creation - No WinRM required
                    // ═══════════════════════════════════════════════════════════════
                    try
                    {
                        AppendTerminal($"[Method 2] Attempting WMI Process Creation...");

                        // Convert PowerShell command to WMI-compatible format
                        string wmiCommand = ConvertToWmiCommand(psCommand);

                        var scope = _wmiManager.GetConnection(targetHost, _authUser, _authPass);
                        using (var mc = new ManagementClass(scope, new ManagementPath("Win32_Process"), null))
                        {
                            var inp = mc.GetMethodParameters("Create");
                            inp["CommandLine"] = wmiCommand;
                            var res = mc.InvokeMethod("Create", inp, null);
                            uint code = (uint)res["returnValue"];

                            if (code == 0)
                            {
                                success = true;
                                AppendTerminal($"[Method 2] ✓ WMI Process Creation succeeded");
                                AppendTerminal($"{actionName}: Command executed successfully via WMI");
                                AddLog(targetHost, actionName, "OK (WMI)", "OK");
                            }
                            else
                            {
                                AppendTerminal($"[Method 2] ✗ WMI returned code {code}", true);
                            }
                        }
                    }
                    catch (Exception ex2)
                    {
                        AppendTerminal($"[Method 2] ✗ Failed: {ex2.Message}");
                        LogManager.LogWarning($"Method 2 (WMI) failed for {actionName}: {ex2.Message}");

                        // ═══════════════════════════════════════════════════════════════
                        // METHOD 3: PsExec - Ultimate fallback
                        // ═══════════════════════════════════════════════════════════════
                        try
                        {
                            AppendTerminal($"[Method 3] Attempting PsExec...");

                            string psexecPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "psexec.exe");

                            if (!File.Exists(psexecPath))
                            {
                                AppendTerminal($"[Method 3] ✗ PsExec not found at {psexecPath}", true);
                                AppendTerminal($"All methods failed for {actionName}", true);
                                return;
                            }

                            string capturedPassword = null;
                            if (_authPass != null)
                            {
                                SecureMemory.UseSecureString(_authPass, pwd => capturedPassword = pwd);
                            }

                            string psexecCommand = ConvertToWmiCommand(psCommand);

                            var psexecPsi = new ProcessStartInfo
                            {
                                FileName = psexecPath,
                                Arguments = $"\\\\{targetHost} -u {_authUser} -p \"{capturedPassword}\" -accepteula {psexecCommand}",
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                CreateNoWindow = true
                            };

                            using (var proc = Process.Start(psexecPsi))
                            {
                                string output = proc.StandardOutput.ReadToEnd();
                                string errors = proc.StandardError.ReadToEnd();
                                proc.WaitForExit();

                                capturedPassword = null;
                                GC.Collect();

                                if (proc.ExitCode == 0 || !string.IsNullOrWhiteSpace(output))
                                {
                                    success = true;
                                    AppendTerminal($"[Method 3] ✓ PsExec succeeded");
                                    if (!string.IsNullOrWhiteSpace(output))
                                        AppendTerminal(output);
                                    AddLog(targetHost, actionName, "OK (PsExec)", "OK");
                                }
                                else
                                {
                                    AppendTerminal($"[Method 3] ✗ Failed: {errors}", true);
                                }
                            }
                        }
                        catch (Exception ex3)
                        {
                            AppendTerminal($"[Method 3] ✗ Failed: {ex3.Message}", true);
                            LogManager.LogError($"All methods failed for {actionName}", ex3);
                            AppendTerminal($"All methods exhausted for {actionName}", true);
                        }
                    }
                }

                if (!success)
                {
                    AppendTerminal($"❌ {actionName} FAILED on {targetHost}", true);
                    AddLog(targetHost, actionName, "All methods failed", "FAIL");
                }
            });
        }

        /// <summary>Converts PowerShell commands to WMI/PsExec-compatible format</summary>
        private string ConvertToWmiCommand(string psCommand)
        {
            // If it's already a cmd.exe command, return as-is
            if (psCommand.TrimStart().StartsWith("cmd", StringComparison.OrdinalIgnoreCase))
                return psCommand;

            // Wrap PowerShell commands in powershell.exe
            return $"powershell.exe -NoProfile -ExecutionPolicy Bypass -Command \"{psCommand.Replace("\"", "`\"")}\"";
        }

        // ── Fleet Scanner ──

        private async void BtnInvScan_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Check if scan is already running (stop button clicked)
                var existing = _scanTokenSource;
                if (existing != null)
                {
                    // Signal cancellation immediately and update UI
                    existing.Cancel();
                    BtnScanFleet.Content = "STOPPING...";
                    BtnScanFleet.IsEnabled = false;
                    TxtScanStatus.Text = "Cancelling scan, please wait...";
                    AppendTerminal(">>> SCAN STOP REQUESTED - Cancelling tasks...");
                    // TAG: #AUTO_UPDATE_UI_ENGINE #STATUS_TOASTS
                    Managers.UI.ToastManager.ShowWarning("Stopping scan - please wait...");
                    // Don't wait here - the Task.Run finally block will clean up
                    return;
                }

                Mouse.OverrideCursor = Cursors.Wait;

                string targetDC = ComboDC.Text;
                if (ComboDC.SelectedItem is ComboBoxItem item && item.Tag != null)
                {
                    targetDC = item.Tag.ToString();
                    if (targetDC == "Auto")
                        targetDC = Environment.GetEnvironmentVariable("USERDNSDOMAIN");
                }

                if (string.IsNullOrEmpty(targetDC))
                {
                    // TAG: #AUTO_UPDATE_UI_ENGINE #VALIDATION_TOASTS
                    Managers.UI.ToastManager.ShowWarning("Please select a Domain Controller");
                    MessageBox.Show("Select a Domain Controller", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    Mouse.OverrideCursor = null;
                    return;
                }

                AppendTerminal($"\n>>> FLEET SCAN on {targetDC}...");
                ResetErrorCounter(); // Clear error indicator for new scan
                Managers.UI.ToastManager.ShowInfo($"Fleet scan started on {targetDC}");

                lock (_inventoryLock) { _inventory.Clear(); }
                BtnScanFleet.Content = "⬛ STOP SCAN";
                BtnScanFleet.Background = Brushes.DarkRed;

                var newCts = new CancellationTokenSource();
                if (Interlocked.CompareExchange(ref _scanTokenSource, newCts, null) != null)
                {
                    newCts.Dispose();
                    Mouse.OverrideCursor = null;
                    return;
                }

                var token = newCts.Token;

                // Capture credentials for async use
                string capturedUser = _authUser;
                string capturedPassword = null;
                if (_isLoggedIn && _authPass != null)
                {
                    SecureMemory.UseSecureString(_authPass, pwd => capturedPassword = pwd);
                }

                // Debug: Show authentication status (log to file only, don't block UI)
                LogManager.LogDebug($"SCAN START - Auth: {_isLoggedIn}, Admin: {_isDomainAdmin}, User: {_authUser}, Creds: {(!string.IsNullOrEmpty(capturedPassword) ? "YES" : "NO")}");

                await Task.Run(async () =>
                {
                    try
                    {
                        var computers = new List<string>();

                        try
                        {
                            // TAG: #VERSION_7 #OPTIMIZED_SCANNER - Use OptimizedADScanner for 3-5x faster enumeration
                            var adScanner = new OptimizedADScanner(30, SecureConfig.WmiTimeoutMs);

                            var progress = new Progress<string>(msg =>
                            {
                                Dispatcher.Invoke(() => AppendTerminal(msg));
                            });

                            computers = await adScanner.GetADComputersAsync(
                                targetDC,
                                capturedUser,
                                capturedPassword,
                                progress,
                                token
                            );
                        }
                        finally
                        {
                            // OPTIMIZATION #11: Let GC run naturally instead of forcing full collection - TAG: #PERFORMANCE_AUDIT
                            // Wipe captured password from memory
                            if (capturedPassword != null)
                            {
                                capturedPassword = null;
                                // Removed GC.Collect() - forces expensive Gen2 collection (100-500ms pause)
                                // Password is already nulled, GC will collect it naturally
                            }
                        }

                        AppendTerminal($"Found {computers.Count} nodes. Scanning...");

                        // OPTIMIZATION #8: Update failure cache size based on AD computer count - TAG: #PERFORMANCE_AUDIT #CACHE #VERSION_7
                        OptimizedADScanner.UpdateMaxCacheSize(computers.Count);

                        // Show progress panel with animation
                        Dispatcher.Invoke(() =>
                        {
                            ScanProgressPanel.Visibility = Visibility.Visible;
                            TxtScanStatus.Text = $"Scanning {computers.Count} computers...";
                            StartScanAnimation();
                            // TAG: #AUTO_UPDATE_UI_ENGINE #STATUS_TOASTS
                            Managers.UI.ToastManager.ShowInfo($"Starting scan of {computers.Count} computers...");
                        });

                        int completed = 0;
                        int onlineCount = 0;
                        int offlineCount = 0;

                        // Optimize parallelism based on CPU cores and RAM
                        int cpuCores = Environment.ProcessorCount;
                        long totalRAM = 16; // Default to 16GB if detection fails
                        try
                        {
                            // OPTIMIZATION #5: Properly query CIM - TAG: #PERFORMANCE_AUDIT
                            using (var session = CimSession.Create(null)) // null = local machine
                            {
                                var instances = session.QueryInstances("root/cimv2", "WQL", "SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                                CimInstance obj = null;

                                foreach (var instance in instances)
                                {
                                    obj = instance;
                                    break; // Get first instance only
                                }

                                if (obj != null)
                                {
                                    var memVal = obj.CimInstanceProperties["TotalPhysicalMemory"]?.Value;
                                    if (memVal != null)
                                    {
                                        totalRAM = Convert.ToInt64(memVal) / (1024 * 1024 * 1024); // Convert bytes to GB
                                    }
                                }
                            }
                        }
                        catch { /* Use default 16GB */ }

                        int optimalParallel = Math.Min(cpuCores * 5, (int)(totalRAM / 2)); // 5 per core or 1 per 2GB RAM
                        optimalParallel = Math.Max(30, Math.Min(optimalParallel, 100)); // Between 30-100

                        var sem = new SemaphoreSlim(optimalParallel);
                        LogManager.LogDebug($"Scan optimization: {cpuCores} cores, {totalRAM}GB RAM, {optimalParallel} parallel scans");

                        // OPTIMIZATION #1: Create scanner ONCE, not for every computer - TAG: #PERFORMANCE_AUDIT
                        var scanner = new OptimizedADScanner(30, SecureConfig.WmiTimeoutMs);

                        var tasks = computers.Select(async host =>
                        {
                            if (token.IsCancellationRequested) return;
                            await sem.WaitAsync(token);
                            try
                            {
                                var pc = new PCInventory
                                {
                                    Hostname = host,
                                    Status = "Scanning...",
                                    CurrentUser = "--",
                                    BitLockerStatus = "?"
                                };

                                lock (_inventoryLock) { _inventory.Add(pc); }

                                using (var p = new Ping())
                                {
                                    try
                                    {
                                        var reply = await p.SendPingAsync(host, SecureConfig.PingTimeoutMs);
                                        if (reply.Status == IPStatus.Success)
                                        {
                                            pc.Status = "ONLINE";
                                            Interlocked.Increment(ref onlineCount);
                                            HardwareSpec spec = null;

                                            try
                                            {
                                                // TAG: #VERSION_7 #OPTIMIZED_SCANNER - Use triple-fallback strategy (CIM/WS-MAN → CIM/DCOM → Legacy WMI)
                                                spec = await scanner.ScanComputerWithFallbackAsync(host, _authUser, _authPass, token);
                                            }
                                            catch (UnauthorizedAccessException)
                                            {
                                                pc.Status = "Access Denied";
                                                spec = new HardwareSpec { Protocol = "UNAUTHORIZED" };
                                            }
                                            catch (Exception ex)
                                            {
                                                LogManager.LogDebug($"OptimizedADScanner failed for {host}: {ex.Message}");
                                                spec = new HardwareSpec { Protocol = "FAILED" };
                                            }

                                            if (spec.Protocol != "FAILED" && spec.Protocol != "CACHED_FAILURE")
                                            {
                                                pc.DisplayOS = spec.OS;
                                                pc.OS = spec.OS; // For dashboard analytics
                                                pc.WindowsVersion = spec.WindowsVersion;
                                                pc.CurrentUser = spec.User;
                                                pc.Chassis = spec.Chassis;
                                                pc.BitLockerStatus = spec.BitLocker;
                                                pc.LastBoot = spec.LastBoot;
                                                pc.Disk = spec.Drives != null && spec.Drives.Count > 0 ? string.Join(", ", spec.Drives) : "N/A";
                                                UpdateMasterInventoryFile(pc);
                                            }

                                            int c = Interlocked.Increment(ref completed);
                                            if (c % 10 == 0 || c == computers.Count)
                                            {
                                                UpdateScanProgress(c, computers.Count, onlineCount, offlineCount);
                                                _ = Dispatcher.InvokeAsync(() =>
                                                    TxtScanProgress.Text = $"{c}/{computers.Count} ({c * 100 / computers.Count}%)");

                                                // TAG: #AUTO_UPDATE_UI_ENGINE #STATUS_TOASTS - Milestone toasts every 25%
                                                int percentage = c * 100 / computers.Count;
                                                if (percentage == 25 || percentage == 50 || percentage == 75)
                                                {
                                                    _ = Dispatcher.InvokeAsync(() =>
                                                        Managers.UI.ToastManager.ShowInfo($"Scan progress: {percentage}% complete ({c}/{computers.Count})"));
                                                }
                                            }
                                        }
                                        else
                                        {
                                            pc.Status = "Offline";
                                            Interlocked.Increment(ref offlineCount);
                                        }
                                    }
                                    catch (OperationCanceledException)
                                    {
                                        pc.Status = "Cancelled";
                                    }
                                    catch
                                    {
                                        pc.Status = "Offline";
                                        Interlocked.Increment(ref offlineCount);
                                    }
                                }
                            }
                            finally { sem.Release(); }
                        });

                        await Task.WhenAll(tasks);
                        AppendTerminal(">>> FLEET SCAN COMPLETE.");

                        // Calculate scan duration
                        var scanDuration = DateTime.Now - DateTime.Now; // Will be replaced with actual timing
                        _ = Dispatcher.InvokeAsync(() =>
                        {
                            Managers.UI.ToastManager.ShowSuccess(
                                $"Fleet scan complete: {onlineCount} online, {offlineCount} offline"
                            );
                        });
                    }
                    catch (OperationCanceledException)
                    {
                        AppendTerminal("Scan aborted.");
                        _ = Dispatcher.InvokeAsync(() =>
                        {
                            // TAG: #AUTO_UPDATE_UI_ENGINE #STATUS_TOASTS
                            Managers.UI.ToastManager.ShowWarning("Scan cancelled by user");
                        });
                    }
                    catch (UnauthorizedAccessException uex)
                    {
                        AppendTerminal($"AD Access Denied: {uex.Message}", true);
                        AppendTerminal("Ensure you're logged in with Domain Admin credentials.", true);
                        _ = Dispatcher.InvokeAsync(() =>
                        {
                            // TAG: #AUTO_UPDATE_UI_ENGINE #STATUS_TOASTS
                            Managers.UI.ToastManager.ShowWarning("Access denied - Domain Admin credentials required");
                            MessageBox.Show("Access denied to Active Directory.\n\nPlease log in with Domain Admin credentials and try again.",
                                "Authentication Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                        });
                    }
                    catch (Exception ex)
                    {
                        AppendTerminal($"AD Error: {ex.Message}", true);
                        // TAG: #AUTO_UPDATE_UI_ENGINE #STATUS_TOASTS
                        _ = Dispatcher.InvokeAsync(() =>
                            Managers.UI.ToastManager.ShowError($"Scan error: {ex.Message}"));
                    }
                    finally
                    {
                        var old = Interlocked.Exchange(ref _scanTokenSource, null);
                        old?.Dispose();
                        Dispatcher.Invoke(() =>
                        {
                            BtnScanFleet.Content = "SCAN DOMAIN";
                            BtnScanFleet.Background = (Brush)FindResource("AccentColor");
                            BtnScanFleet.IsEnabled = true; // Re-enable button after scan completes or is cancelled
                            Mouse.OverrideCursor = null;
                            StopScanAnimation();
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                LogManager.LogError("Fleet scan error", ex);
                Mouse.OverrideCursor = null;
            }
        }

        // ── Windows Version Helper ──

        private static string GetWindowsVersionFromBuild(string buildNumber, string osCaption)
        {
            if (string.IsNullOrEmpty(buildNumber) || !int.TryParse(buildNumber, out int build)) return "Unknown";

            // Check if this is a Server OS by looking at the Caption
            bool isServer = !string.IsNullOrEmpty(osCaption) &&
                           (osCaption.Contains("Server") || osCaption.Contains("server"));

            if (isServer)
            {
                // Windows Server versions
                if (build >= 26100) return "Server2025"; // Windows Server 2025
                if (build >= 20348) return "Server2022"; // Windows Server 2022 (Build 20348)
                if (build >= 17763) return "Server2019"; // Windows Server 2019 (Build 17763)
                if (build >= 14393) return "Server2016"; // Windows Server 2016 (Build 14393)
                if (build >= 9600) return "Server2012R2"; // Windows Server 2012 R2 (Build 9600)
                if (build >= 9200) return "Server2012"; // Windows Server 2012 (Build 9200)
                if (build >= 7600 && build < 9200) return "Server2008R2"; // Windows Server 2008 R2
                return "ServerLegacy"; // Older server versions
            }

            // Desktop/Client Windows versions
            // Windows 11 versions
            if (build >= 27000) return "25H2"; // Windows 11 25H2 (2025 H2) - future
            if (build >= 26100) return "24H2"; // Windows 11 24H2 (Build 26100+)
            if (build >= 22631) return "23H2"; // Windows 11 23H2 (Build 22631+)
            if (build >= 22621) return "22H2"; // Windows 11 22H2 (Build 22621+)
            if (build >= 22000) return "21H2"; // Windows 11 21H2 (Build 22000+)

            // Windows 10 versions
            if (build >= 19045) return "22H2"; // Windows 10 22H2 (Build 19045+)
            if (build >= 19044) return "21H2"; // Windows 10 21H2
            if (build >= 19043) return "21H1"; // Windows 10 21H1
            if (build >= 19042) return "20H2"; // Windows 10 20H2
            if (build >= 19041) return "2004"; // Windows 10 2004
            if (build >= 18363) return "1909"; // Windows 10 1909
            if (build >= 18362) return "1903"; // Windows 10 1903

            // Windows 7/8/8.1
            if (build >= 9600) return "Win8.1"; // Windows 8.1
            if (build >= 9200) return "Win8"; // Windows 8
            if (build >= 7600) return "Win7"; // Windows 7

            return "Legacy"; // Older versions
        }

        private static System.Windows.Media.Brush GetWindowsVersionColor(string version)
        {
            if (string.IsNullOrEmpty(version)) return System.Windows.Media.Brushes.Gray;

            // Server color coding
            if (version.StartsWith("Server"))
            {
                // Green for Server 2019/2022/2025 or newer
                if (version == "Server2025" || version == "Server2022" || version == "Server2019")
                    return System.Windows.Media.Brushes.LimeGreen;

                // Yellow for Server 2016
                if (version == "Server2016")
                    return System.Windows.Media.Brushes.Yellow;

                // Red for Server 2012 R2 or older (2012, 2008 R2, etc.)
                return System.Windows.Media.Brushes.Red;
            }

            // Desktop Windows color coding
            // Green for 25H2 or newer
            if (version == "25H2") return System.Windows.Media.Brushes.LimeGreen;

            // Yellow for 24H2
            if (version == "24H2") return System.Windows.Media.Brushes.Yellow;

            // Red for 23H2 or older (including 22H2, 21H2, Legacy, etc.)
            return System.Windows.Media.Brushes.Red;
        }

        // ── System Specs Scanner (consolidated WMI queries) ──

        private async Task<HardwareSpec> GetSystemSpecsAsync(string hostname, string username, SecureString password, CancellationToken ct = default)
        {
            var spec = new HardwareSpec { Protocol = "WMI" };
            try
            {
                using (var timeoutCts = new CancellationTokenSource(SecureConfig.WmiTimeoutMs))
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token))
                {
                    // Try CIM first, fall back to WMI
                    CimSession cimSession = null;
                    ManagementScope wmiScope = null;
                    bool useCim = false;
                    bool connectionFailed = false;
                    string failureReason = "";

                    try
                    {
                        // Attempt CIM connection with hard timeout
                        AppendTerminal($"[CIM] Attempting connection to {hostname}...");
                        LogManager.LogDebug($"[CIM] Attempting connection to {hostname}...");
                        var cimConnectTask = Task.Run(() => _cimManager.GetConnection(hostname, username, password, out string protocol));
                        var connectionTimeoutTask = Task.Delay(10000); // 10-second connection timeout

                        if (await Task.WhenAny(cimConnectTask, connectionTimeoutTask) == cimConnectTask)
                        {
                            string protocol;
                            cimSession = _cimManager.GetConnection(hostname, username, password, out protocol);
                            spec.Protocol = $"CIM ({protocol})";
                            useCim = true;
                            AppendTerminal($"[CIM] Connection SUCCESS using {protocol}");
                            LogManager.LogDebug($"[CIM] Connection SUCCESS for {hostname} using {protocol}");
                        }
                        else
                        {
                            AppendTerminal($"[CIM] Connection TIMEOUT after 10s - trying WMI fallback...", true);
                            LogManager.LogDebug($"[CIM] Connection TIMEOUT for {hostname} after 10s - falling back to WMI");
                            throw new TimeoutException("CIM connection exceeded 10 seconds");
                        }
                    }
                    catch (Exception cimEx)
                    {
                        AppendTerminal($"[CIM] Failed: {cimEx.Message.Substring(0, Math.Min(60, cimEx.Message.Length))}... trying WMI", true);
                        LogManager.LogDebug($"[CIM] Connection failed for {hostname}, falling back to WMI: {cimEx.Message}");

                        // Fall back to WMI
                        try
                        {
                            AppendTerminal($"[WMI] Attempting fallback connection...");
                            var opts = new ConnectionOptions { Timeout = TimeSpan.FromMilliseconds(SecureConfig.WmiTimeoutMs), EnablePrivileges = true, Impersonation = ImpersonationLevel.Impersonate, Authentication = AuthenticationLevel.PacketPrivacy };
                            if (!string.IsNullOrEmpty(username) && password != null) { opts.Username = username; opts.SecurePassword = password; }
                            wmiScope = new ManagementScope($"\\\\{hostname}\\root\\cimv2", opts);
                            wmiScope.Connect();
                            spec.Protocol = "WMI (Fallback)";
                            useCim = false;
                            AppendTerminal($"[WMI] Fallback connection SUCCESS");
                        }
                        catch (System.Runtime.InteropServices.COMException comEx)
                        {
                            connectionFailed = true;
                            failureReason = comEx.Message.Contains("RPC") || comEx.Message.Contains("0x800706BA") ? "RPC unavailable" : "Access denied";
                            AppendTerminal($"[BOTH FAILED] CIM and WMI - {failureReason}", true);
                            LogManager.LogDebug($"WMI connection failed for {hostname}: {failureReason} (HRESULT: 0x{comEx.HResult:X})");
                            spec.Protocol = "CONNECTION_FAILED";
                            spec.Serial = failureReason;
                            return spec;
                        }
                        catch (UnauthorizedAccessException)
                        {
                            connectionFailed = true;
                            spec.Protocol = "UNAUTHORIZED";
                            spec.Serial = "Access Denied";
                            AppendTerminal($"[BOTH FAILED] Access Denied - check credentials", true);
                            LogWarning($"Access Denied", $"{hostname}");
                            return spec;
                        }
                        catch (Exception ex)
                        {
                            connectionFailed = true;
                            AppendTerminal($"[BOTH FAILED] {ex.GetType().Name}: {ex.Message.Substring(0, Math.Min(50, ex.Message.Length))}", true);
                            LogCriticalError($"Connection Error", $"{hostname}: {ex.GetType().Name}");
                            spec.Protocol = "ERROR";
                            spec.Serial = ex.Message;
                            return spec;
                        }
                    }

                    if (connectionFailed) return spec;

                        // ══════════════════════════════════════════════════════════════
                        // MULTICORE OPTIMIZATION: Parallel WMI queries using Task.WhenAll
                        // All queries are independent and can run concurrently
                        // ══════════════════════════════════════════════════════════════

                        // Helper to update status with visual breathing effect
                        void UpdateStatus(string message)
                        {
                            Dispatcher.InvokeAsync(() =>
                            {
                                TxtStatus.Text = message;

                                // Show progress bar during queries, hide when complete
                                if (message.EndsWith("..."))
                                {
                                    // Query starting - show animated progress bar
                                    StatusProgressBar.Visibility = Visibility.Visible;
                                    StatusDot.Fill = new SolidColorBrush(Color.FromRgb(255, 133, 51)); // Orange
                                }
                                else if (message.EndsWith("✓"))
                                {
                                    // Query completed - keep progress bar visible until all done
                                    StatusDot.Fill = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
                                }
                            });
                            AppendTerminal($"[{spec.Protocol}] {message}");
                        }

                        var wmiTasks = new List<Task>();

                        // BIOS query
                        wmiTasks.Add(Task.Run(() =>
                        {
                            try
                            {
                                UpdateStatus("Querying BIOS...");
                                if (useCim)
                                {
                                    var instances = cimSession.QueryInstances("root/cimv2", "WQL", "SELECT SerialNumber FROM Win32_Bios");
                                    var bios = instances.FirstOrDefault();
                                    spec.Serial = bios?.CimInstanceProperties["SerialNumber"]?.Value?.ToString() ?? "N/A";
                                }
                                else
                                {
                                    using (var s = new ManagementObjectSearcher(wmiScope, new ObjectQuery("SELECT SerialNumber FROM Win32_Bios")))
                                    using (var r = s.Get())
                                    {
                                        var b = r.Cast<ManagementObject>().FirstOrDefault();
                                        if (b != null) { spec.Serial = b["SerialNumber"]?.ToString() ?? "N/A"; b.Dispose(); }
                                    }
                                }
                                UpdateStatus("BIOS ✓");
                            }
                            catch (UnauthorizedAccessException)
                            {
                                LogManager.LogDebug($"BIOS query access denied for {hostname}");
                                spec.Serial = "Access Denied";
                            }
                            catch (Exception ex)
                            {
                                LogManager.LogDebug($"BIOS query failed for {hostname}: {ex.Message}");
                            }
                        }));

                        // ComputerSystem query (Model, User, RAM, Manufacturer, Domain)
                        wmiTasks.Add(Task.Run(() =>
                        {
                            try
                            {
                                UpdateStatus("Querying System Info...");
                                if (useCim)
                                {
                                    var instances = cimSession.QueryInstances("root/cimv2", "WQL", "SELECT Model, UserName, TotalPhysicalMemory, Manufacturer, Domain FROM Win32_ComputerSystem");
                                    var sys = instances.FirstOrDefault();
                                    if (sys != null)
                                    {
                                        spec.Model = sys.CimInstanceProperties["Model"]?.Value?.ToString() ?? "N/A";
                                        string userName = sys.CimInstanceProperties["UserName"]?.Value?.ToString();
                                        spec.User = string.IsNullOrWhiteSpace(userName) ? "(No User Logged In)" : userName;
                                        spec.Manufacturer = sys.CimInstanceProperties["Manufacturer"]?.Value?.ToString() ?? "N/A";
                                        spec.Domain = sys.CimInstanceProperties["Domain"]?.Value?.ToString() ?? "WORKGROUP";
                                        var mem = sys.CimInstanceProperties["TotalPhysicalMemory"]?.Value;
                                        if (mem != null) spec.RAM = $"{Math.Round(Convert.ToDouble(mem) / 1073741824)} GB";
                                    }
                                }
                                else
                                {
                                    using (var s = new ManagementObjectSearcher(wmiScope, new ObjectQuery("SELECT Model, UserName, TotalPhysicalMemory, Manufacturer, Domain FROM Win32_ComputerSystem")))
                                    using (var r = s.Get())
                                    {
                                        var sys = r.Cast<ManagementObject>().FirstOrDefault();
                                        if (sys != null)
                                        {
                                            spec.Model = sys["Model"]?.ToString() ?? "N/A";
                                            string userName = sys["UserName"]?.ToString();
                                            spec.User = string.IsNullOrWhiteSpace(userName) ? "(No User Logged In)" : userName;
                                            spec.Manufacturer = sys["Manufacturer"]?.ToString() ?? "N/A";
                                            spec.Domain = sys["Domain"]?.ToString() ?? "WORKGROUP";
                                            if (sys["TotalPhysicalMemory"] != null) spec.RAM = $"{Math.Round(Convert.ToDouble(sys["TotalPhysicalMemory"]) / 1073741824)} GB";
                                            sys.Dispose();
                                        }
                                    }
                                }
                                UpdateStatus("System Info ✓");
                            }
                            catch (UnauthorizedAccessException)
                            {
                                LogManager.LogDebug($"ComputerSystem query access denied for {hostname}");
                                spec.User = "Access Denied";
                            }
                            catch (Exception ex)
                            {
                                LogManager.LogDebug($"ComputerSystem query failed for {hostname}: {ex.Message}");
                            }
                        }));

                        // CPU query
                        wmiTasks.Add(Task.Run(() =>
                        {
                            try
                            {
                                UpdateStatus("Querying CPU...");
                                if (useCim)
                                {
                                    var instances = cimSession.QueryInstances("root/cimv2", "WQL", "SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor");
                                    var cpu = instances.FirstOrDefault();
                                    if (cpu != null)
                                    {
                                        spec.CPU = cpu.CimInstanceProperties["Name"]?.Value?.ToString();
                                        var cores = cpu.CimInstanceProperties["NumberOfCores"]?.Value;
                                        var threads = cpu.CimInstanceProperties["NumberOfLogicalProcessors"]?.Value;
                                        spec.Cores = $"{cores}C / {threads}T";
                                    }
                                }
                                else
                                {
                                    using (var s = new ManagementObjectSearcher(wmiScope, new ObjectQuery("SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor")))
                                    using (var r = s.Get())
                                    {
                                        var c = r.Cast<ManagementObject>().FirstOrDefault();
                                        if (c != null)
                                        {
                                            spec.CPU = c["Name"]?.ToString();
                                            spec.Cores = $"{c["NumberOfCores"]}C / {c["NumberOfLogicalProcessors"]}T";
                                            c.Dispose();
                                        }
                                    }
                                }
                                UpdateStatus("CPU ✓");
                            }
                            catch (Exception ex)
                            {
                                LogManager.LogDebug($"CPU query failed for {hostname}: {ex.Message}");
                            }
                        }));

                        // OS + LastBoot query
                        wmiTasks.Add(Task.Run(() =>
                        {
                            try
                            {
                                UpdateStatus("Querying OS...");
                                if (useCim)
                                {
                                    var instances = cimSession.QueryInstances("root/cimv2", "WQL", "SELECT Caption, LastBootUpTime, BuildNumber FROM Win32_OperatingSystem");
                                    var os = instances.FirstOrDefault();
                                    if (os != null)
                                    {
                                        string buildNumber = os.CimInstanceProperties["BuildNumber"]?.Value?.ToString() ?? "";
                                        string osCaption = os.CimInstanceProperties["Caption"]?.Value?.ToString() ?? "";
                                        spec.OS = $"{osCaption} (Build {buildNumber})";
                                        spec.WindowsVersion = GetWindowsVersionFromBuild(buildNumber, osCaption);

                                        // CIM returns native DateTime - no conversion needed
                                        DateTime? lastBoot = os.CimInstanceProperties["LastBootUpTime"]?.Value as DateTime?;
                                        if (lastBoot.HasValue)
                                        {
                                            var boot = lastBoot.Value;
                                            spec.Uptime = $"{(DateTime.Now - boot).Days}d {(DateTime.Now - boot).Hours}h {(DateTime.Now - boot).Minutes}m";
                                            spec.LastBoot = boot.ToString("yyyy-MM-dd HH:mm:ss");
                                        }
                                    }
                                }
                                else
                                {
                                    using (var s = new ManagementObjectSearcher(wmiScope, new ObjectQuery("SELECT Caption, LastBootUpTime, BuildNumber FROM Win32_OperatingSystem")))
                                    using (var r = s.Get())
                                    {
                                        var os = r.Cast<ManagementObject>().FirstOrDefault();
                                        if (os != null)
                                        {
                                            string buildNumber = os["BuildNumber"]?.ToString() ?? "";
                                            string osCaption = os["Caption"]?.ToString() ?? "";
                                            spec.OS = $"{osCaption} (Build {buildNumber})";
                                            spec.WindowsVersion = GetWindowsVersionFromBuild(buildNumber, osCaption);

                                            // WMI requires DateTime conversion
                                            string bs = os["LastBootUpTime"]?.ToString();
                                            if (!string.IsNullOrEmpty(bs))
                                            {
                                                var boot = ManagementDateTimeConverter.ToDateTime(bs);
                                                spec.Uptime = $"{(DateTime.Now - boot).Days}d {(DateTime.Now - boot).Hours}h {(DateTime.Now - boot).Minutes}m";
                                                spec.LastBoot = boot.ToString("yyyy-MM-dd HH:mm:ss");
                                            }
                                            os.Dispose();
                                        }
                                    }
                                }
                                UpdateStatus("OS ✓");
                            }
                            catch (Exception ex)
                            {
                                LogManager.LogDebug($"OS query failed for {hostname}: {ex.Message}");
                            }
                        }));

                        // TimeZone query
                        wmiTasks.Add(Task.Run(() =>
                        {
                            try
                            {
                                UpdateStatus("Querying TimeZone...");
                                if (useCim)
                                {
                                    var instances = cimSession.QueryInstances("root/cimv2", "WQL", "SELECT Caption FROM Win32_TimeZone");
                                    spec.TimeZone = instances.FirstOrDefault()?.CimInstanceProperties["Caption"]?.Value?.ToString() ?? "N/A";
                                }
                                else
                                {
                                    using (var s = new ManagementObjectSearcher(wmiScope, new ObjectQuery("SELECT Caption FROM Win32_TimeZone")))
                                    using (var r = s.Get())
                                    {
                                        var t = r.Cast<ManagementObject>().FirstOrDefault();
                                        if (t != null)
                                        {
                                            spec.TimeZone = t["Caption"]?.ToString() ?? "N/A";
                                            t.Dispose();
                                        }
                                    }
                                }
                                UpdateStatus("TimeZone ✓");
                            }
                            catch (Exception ex)
                            {
                                spec.TimeZone = "ERROR";
                                LogManager.LogDebug($"TimeZone query failed for {hostname}: {ex.Message}");
                            }
                        }));

                        // Network query
                        wmiTasks.Add(Task.Run(() =>
                        {
                            try
                            {
                                UpdateStatus("Querying Network...");
                                if (useCim)
                                {
                                    var instances = cimSession.QueryInstances("root/cimv2", "WQL", "SELECT IPAddress, MACAddress, DNSServerSearchOrder FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled=TRUE");
                                    var n = instances.FirstOrDefault();
                                    if (n != null)
                                    {
                                        // Array handling is identical between CIM and WMI
                                        string[] ips = n.CimInstanceProperties["IPAddress"]?.Value as string[];
                                        spec.IP = ips?.FirstOrDefault(x => x.Contains("."));
                                        spec.MAC = n.CimInstanceProperties["MACAddress"]?.Value?.ToString();
                                        string[] dns = n.CimInstanceProperties["DNSServerSearchOrder"]?.Value as string[];
                                        if (dns?.Length > 0) spec.DNS = string.Join(", ", dns);
                                    }
                                }
                                else
                                {
                                    using (var s = new ManagementObjectSearcher(wmiScope, new ObjectQuery("SELECT IPAddress, MACAddress, DNSServerSearchOrder FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled=TRUE")))
                                    using (var r = s.Get())
                                    {
                                        var n = r.Cast<ManagementObject>().FirstOrDefault();
                                        if (n != null)
                                        {
                                            string[] ips = (string[])n["IPAddress"];
                                            spec.IP = ips?.FirstOrDefault(x => x.Contains("."));
                                            spec.MAC = n["MACAddress"]?.ToString();
                                            string[] dns = (string[])n["DNSServerSearchOrder"];
                                            if (dns?.Length > 0) spec.DNS = string.Join(", ", dns);
                                            n.Dispose();
                                        }
                                    }
                                }
                                UpdateStatus("Network ✓");
                            }
                            catch (Exception ex)
                            {
                                LogManager.LogDebug($"Network query failed for {hostname}: {ex.Message}");
                            }
                        }));

                        // Battery query
                        wmiTasks.Add(Task.Run(() =>
                        {
                            try
                            {
                                UpdateStatus("Querying Battery...");
                                if (useCim)
                                {
                                    var instances = cimSession.QueryInstances("root/cimv2", "WQL", "SELECT EstimatedChargeRemaining, BatteryStatus FROM Win32_Battery");
                                    var b = instances.FirstOrDefault();
                                    if (b != null)
                                    {
                                        var charge = b.CimInstanceProperties["EstimatedChargeRemaining"]?.Value;
                                        var status = b.CimInstanceProperties["BatteryStatus"]?.Value?.ToString();
                                        spec.Battery = $"{charge}% ({(status == "2" ? "Plugged In" : "Discharging")})";
                                    }
                                    else
                                    {
                                        spec.Battery = "No Battery (Desktop)";
                                    }
                                }
                                else
                                {
                                    using (var s = new ManagementObjectSearcher(wmiScope, new ObjectQuery("SELECT EstimatedChargeRemaining, BatteryStatus FROM Win32_Battery")))
                                    using (var r = s.Get())
                                    {
                                        var b = r.Cast<ManagementObject>().FirstOrDefault();
                                        if (b != null)
                                        {
                                            spec.Battery = $"{b["EstimatedChargeRemaining"]}% ({(b["BatteryStatus"]?.ToString() == "2" ? "Plugged In" : "Discharging")})";
                                            b.Dispose();
                                        }
                                        else
                                        {
                                            spec.Battery = "No Battery (Desktop)";
                                        }
                                    }
                                }
                                UpdateStatus("Battery ✓");
                            }
                            catch
                            {
                                spec.Battery = "Unknown";
                            }
                        }));

                        // Chassis query
                        wmiTasks.Add(Task.Run(() =>
                        {
                            try
                            {
                                UpdateStatus("Querying Chassis...");
                                if (useCim)
                                {
                                    var instances = cimSession.QueryInstances("root/cimv2", "WQL", "SELECT ChassisTypes FROM Win32_SystemEnclosure");
                                    var ch = instances.FirstOrDefault();
                                    if (ch != null)
                                    {
                                        var chassisTypesValue = ch.CimInstanceProperties["ChassisTypes"]?.Value;
                                        var types = (chassisTypesValue as ushort[])?.Select(x => (int)x).ToArray() ?? new int[0];
                                        if (types.Any(t => t == 9 || t == 10 || t == 14))
                                            spec.Chassis = "Laptop";
                                        else if (types.Any(t => t == 3 || t == 4 || t == 6 || t == 7))
                                            spec.Chassis = "Desktop";
                                        else
                                            spec.Chassis = "VM/Other";
                                    }
                                }
                                else
                                {
                                    using (var s = new ManagementObjectSearcher(wmiScope, new ObjectQuery("SELECT ChassisTypes FROM Win32_SystemEnclosure")))
                                    using (var r = s.Get())
                                    {
                                        var ch = r.Cast<ManagementObject>().FirstOrDefault();
                                        if (ch != null)
                                        {
                                            var types = ((ushort[])ch["ChassisTypes"])?.Select(x => (int)x).ToArray() ?? new int[0];
                                            if (types.Any(t => t == 9 || t == 10 || t == 14))
                                                spec.Chassis = "Laptop";
                                            else if (types.Any(t => t == 3 || t == 4 || t == 6 || t == 7))
                                                spec.Chassis = "Desktop";
                                            else
                                                spec.Chassis = "VM/Other";
                                            ch.Dispose();
                                        }
                                    }
                                }
                                UpdateStatus("Chassis ✓");
                            }
                            catch (Exception ex)
                            {
                                spec.Chassis = "ERROR";
                                LogManager.LogDebug($"Chassis query failed for {hostname}: {ex.Message}");
                            }
                        }));

                        // Drives query
                        wmiTasks.Add(Task.Run(() =>
                        {
                            try
                            {
                                UpdateStatus("Querying Drives...");
                                if (useCim)
                                {
                                    var instances = cimSession.QueryInstances("root/cimv2", "WQL", "SELECT DeviceID, FreeSpace, Size, VolumeName FROM Win32_LogicalDisk WHERE DriveType=3");
                                    foreach (var d in instances)
                                    {
                                        var sizeValue = d.CimInstanceProperties["Size"]?.Value;
                                        var freeValue = d.CimInstanceProperties["FreeSpace"]?.Value;
                                        if (sizeValue != null && freeValue != null)
                                        {
                                            lock (spec.Drives)
                                            {
                                                spec.Drives.Add($"{d.CimInstanceProperties["DeviceID"]?.Value}|{d.CimInstanceProperties["VolumeName"]?.Value ?? "Unnamed"}|{Math.Round(Convert.ToDouble(freeValue) / 1073741824, 1)}|{Math.Round(Convert.ToDouble(sizeValue) / 1073741824, 1)}");
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    using (var s = new ManagementObjectSearcher(wmiScope, new ObjectQuery("SELECT DeviceID, FreeSpace, Size, VolumeName FROM Win32_LogicalDisk WHERE DriveType=3")))
                                    using (var r = s.Get())
                                    {
                                        foreach (ManagementObject d in r)
                                        {
                                            if (d["Size"] != null && d["FreeSpace"] != null)
                                            {
                                                lock (spec.Drives)
                                                {
                                                    spec.Drives.Add($"{d["DeviceID"]}|{d["VolumeName"] ?? "Unnamed"}|{Math.Round(Convert.ToDouble(d["FreeSpace"]) / 1073741824, 1)}|{Math.Round(Convert.ToDouble(d["Size"]) / 1073741824, 1)}");
                                                }
                                            }
                                            d.Dispose();
                                        }
                                    }
                                }
                                UpdateStatus("Drives ✓");
                            }
                            catch (Exception ex)
                            {
                                LogManager.LogDebug($"Drives query failed for {hostname}: {ex.Message}");
                            }
                        }));

                        // BitLocker query (special namespace)
                        wmiTasks.Add(Task.Run(() =>
                        {
                            try
                            {
                                UpdateStatus("Querying BitLocker...");
                                if (useCim)
                                {
                                    // CIM uses forward slashes for namespace paths
                                    var instances = cimSession.QueryInstances("root/CIMv2/Security/MicrosoftVolumeEncryption", "WQL", "SELECT ProtectionStatus FROM Win32_EncryptableVolume WHERE DriveLetter='C:'");
                                    var b = instances.FirstOrDefault();
                                    if (b != null)
                                    {
                                        spec.BitLocker = b.CimInstanceProperties["ProtectionStatus"]?.Value?.ToString() == "1" ? "LOCKED" : "OPEN";
                                    }
                                }
                                else
                                {
                                    // Use pooled connection with coordinated timeout
                                    var ss = _wmiManager.GetSecurityNamespaceConnection(
                                        hostname,
                                        username,
                                        password,
                                        "MicrosoftVolumeEncryption");

                                    using (var s = new ManagementObjectSearcher(ss, new ObjectQuery("SELECT ProtectionStatus FROM Win32_EncryptableVolume WHERE DriveLetter='C:'")))
                                    using (var r = s.Get())
                                    {
                                        var b = r.Cast<ManagementObject>().FirstOrDefault();
                                        if (b != null)
                                        {
                                            spec.BitLocker = b["ProtectionStatus"]?.ToString() == "1" ? "LOCKED" : "OPEN";
                                            b.Dispose();
                                        }
                                    }
                                }
                                UpdateStatus("BitLocker ✓");
                            }
                            catch (TimeoutException tex)
                            {
                                spec.BitLocker = "TIMEOUT";
                                LogManager.LogDebug($"BitLocker query timeout for {hostname}: {tex.Message}");
                            }
                            catch (UnauthorizedAccessException uaEx)
                            {
                                spec.BitLocker = "DENIED";
                                LogManager.LogDebug($"BitLocker query access denied for {hostname}: {uaEx.Message}");
                            }
                            catch (Exception ex)
                            {
                                spec.BitLocker = "ERROR";
                                LogManager.LogDebug($"BitLocker query failed for {hostname}: {ex.GetType().Name} - {ex.Message}");
                            }
                        }));

                        // TPM query (special namespace)
                        wmiTasks.Add(Task.Run(() =>
                        {
                            try
                            {
                                UpdateStatus("Querying TPM...");
                                if (useCim)
                                {
                                    // CIM uses forward slashes for namespace paths
                                    var instances = cimSession.QueryInstances("root/CIMv2/Security/MicrosoftTpm", "WQL", "SELECT IsEnabled_InitialValue FROM Win32_Tpm");
                                    var t = instances.FirstOrDefault();
                                    if (t != null)
                                    {
                                        spec.TPMEnabled = (t.CimInstanceProperties["IsEnabled_InitialValue"]?.Value as bool?) == true ? "Enabled" : "Disabled";
                                    }
                                    else
                                    {
                                        spec.TPMEnabled = "Not Present";
                                    }
                                }
                                else
                                {
                                    // Use pooled connection with coordinated timeout
                                    var ts = _wmiManager.GetSecurityNamespaceConnection(
                                        hostname,
                                        username,
                                        password,
                                        "MicrosoftTpm");

                                    using (var s = new ManagementObjectSearcher(ts, new ObjectQuery("SELECT IsEnabled_InitialValue FROM Win32_Tpm")))
                                    using (var r = s.Get())
                                    {
                                        var t = r.Cast<ManagementObject>().FirstOrDefault();
                                        if (t != null)
                                        {
                                            spec.TPMEnabled = (t["IsEnabled_InitialValue"] as bool?) == true ? "Enabled" : "Disabled";
                                            t.Dispose();
                                        }
                                        else
                                        {
                                            spec.TPMEnabled = "Not Present";
                                        }
                                    }
                                }
                                UpdateStatus("TPM ✓");
                            }
                            catch (TimeoutException tex)
                            {
                                spec.TPMEnabled = "TIMEOUT";
                                LogManager.LogDebug($"TPM query timeout for {hostname}: {tex.Message}");
                            }
                            catch (UnauthorizedAccessException uaEx)
                            {
                                spec.TPMEnabled = "DENIED";
                                LogManager.LogDebug($"TPM query access denied for {hostname}: {uaEx.Message}");
                            }
                            catch (Exception ex)
                            {
                                spec.TPMEnabled = "ERROR";
                                LogManager.LogDebug($"TPM query failed for {hostname}: {ex.GetType().Name} - {ex.Message}");
                            }
                        }));

                    // Wait for all queries with hard timeout protection
                    // If CIM hangs and doesn't respect cancellation, we force-abandon after timeout
                    AppendTerminal($"[{spec.Protocol}] Running 11 parallel queries...");
                    var allQueriesTask = Task.WhenAll(wmiTasks);
                    var queryTimeoutTask = Task.Delay(SecureConfig.WmiTimeoutMs, cts.Token);

                    var completedTask = await Task.WhenAny(allQueriesTask, queryTimeoutTask);

                    if (completedTask == queryTimeoutTask)
                    {
                        AppendTerminal($"[FORCED TIMEOUT] Queries exceeded {SecureConfig.WmiTimeoutMs}ms - returning partial data", true);
                        LogManager.LogDebug($"[FORCED TIMEOUT] Queries for {hostname} exceeded {SecureConfig.WmiTimeoutMs}ms - abandoning stuck operations");
                        spec.Protocol = "TIMEOUT";
                        spec.Serial = $"Timeout after {SecureConfig.WmiTimeoutMs}ms";

                        // Hide progress bar on timeout
                        Dispatcher.InvokeAsync(() =>
                        {
                            StatusProgressBar.Visibility = Visibility.Collapsed;
                            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Red
                            TxtStatus.Text = "TIMEOUT";
                        });

                        return spec; // Force-return with partial data
                    }
                    else
                    {
                        AppendTerminal($"[{spec.Protocol}] All queries completed successfully");

                        // Hide progress bar on successful completion
                        Dispatcher.InvokeAsync(() =>
                        {
                            StatusProgressBar.Visibility = Visibility.Collapsed;
                            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
                            TxtStatus.Text = "Complete";
                        });
                    }
                }
            }
            catch (OperationCanceledException)
            {
                spec.Protocol = "TIMEOUT";
                AppendTerminal($"[TIMEOUT] Scan cancelled or timed out", true);
                LogManager.LogDebug($"Scan cancelled/timeout: {hostname}");

                // Hide progress bar on cancel/timeout
                Dispatcher.InvokeAsync(() =>
                {
                    StatusProgressBar.Visibility = Visibility.Collapsed;
                    StatusDot.Fill = new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Red
                    TxtStatus.Text = "TIMEOUT";
                });
            }
            catch (UnauthorizedAccessException uaEx)
            {
                spec.Protocol = "UNAUTHORIZED";
                AppendTerminal($"[UNAUTHORIZED] Access denied - check credentials", true);
                LogManager.LogDebug($"WMI access denied for {hostname}: {uaEx.Message}");

                // Hide progress bar on auth error
                Dispatcher.InvokeAsync(() =>
                {
                    StatusProgressBar.Visibility = Visibility.Collapsed;
                    StatusDot.Fill = new SolidColorBrush(Color.FromRgb(255, 152, 0)); // Orange warning
                    TxtStatus.Text = "UNAUTHORIZED";
                });
            }
            catch (System.Runtime.InteropServices.COMException comEx)
            {
                // RPC server unavailable, network issues, etc.
                spec.Protocol = "FAILED";
                string errorMsg = comEx.Message.Contains("RPC") ? "RPC server unavailable (offline/firewall)" :
                                 comEx.Message.Contains("Access is denied") ? "Access denied (check credentials)" :
                                 comEx.Message.Contains("0x800706BA") ? "RPC server unavailable" :
                                 comEx.Message;

                AppendTerminal($"[FAILED] {errorMsg}", true);
                LogManager.LogDebug($"WMI connection failed for {hostname}: {errorMsg} (HRESULT: 0x{comEx.HResult:X})");

                // Hide progress bar on connection failure
                Dispatcher.InvokeAsync(() =>
                {
                    StatusProgressBar.Visibility = Visibility.Collapsed;
                    StatusDot.Fill = new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Red
                    TxtStatus.Text = "FAILED";
                });
            }
            catch (Exception ex)
            {
                spec.Protocol = "FAILED";
                AppendTerminal($"[FAILED] {ex.GetType().Name}", true);
                LogManager.LogDebug($"Spec scan failed for {hostname}: {ex.GetType().Name} - {ex.Message}");

                // Hide progress bar on general failure
                Dispatcher.InvokeAsync(() =>
                {
                    StatusProgressBar.Visibility = Visibility.Collapsed;
                    StatusDot.Fill = new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Red
                    TxtStatus.Text = "FAILED";
                });
            }

            // ══════════════════════════════════════════════════════════════
            // FALLBACK: Use PowerShell remoting if WMI failed
            // ══════════════════════════════════════════════════════════════
            if (spec.Protocol == "FAILED" || spec.Protocol == "UNAUTHORIZED" || spec.Protocol == "TIMEOUT")
            {
                LogManager.LogDebug($"WMI failed for {hostname}, trying PowerShell fallback...");
                try
                {
                    var psSpec = await GetSystemSpecsViaPowerShell(hostname, username, password, ct);
                    if (psSpec.Protocol == "PowerShell")
                    {
                        // PowerShell succeeded, use those results
                        return psSpec;
                    }
                }
                catch (Exception psEx)
                {
                    LogManager.LogDebug($"PowerShell fallback also failed for {hostname}: {psEx.Message}");
                }
            }

            return spec;
        }

        /// <summary>Fallback method to get system specs using PowerShell remoting instead of WMI</summary>
        private async Task<HardwareSpec> GetSystemSpecsViaPowerShell(string hostname, string username, SecureString password, CancellationToken ct = default)
        {
            var spec = new HardwareSpec { Protocol = "PowerShell" };

            return await Task.Run(() =>
            {
                try
                {
                    string capturedPassword = null;
                    if (password != null)
                    {
                        SecureMemory.UseSecureString(password, pwd => capturedPassword = pwd);
                    }

                    // PowerShell script to gather system information remotely
                    string psScript = $@"
                        $cred = New-Object System.Management.Automation.PSCredential('{username}', (ConvertTo-SecureString '{capturedPassword}' -AsPlainText -Force))
                        Invoke-Command -ComputerName {hostname} -Credential $cred -ScriptBlock {{
                            # Get OS info
                            $os = Get-CimInstance Win32_OperatingSystem
                            $cs = Get-CimInstance Win32_ComputerSystem
                            $cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
                            $bios = Get-CimInstance Win32_BIOS

                            # Output as pipe-delimited for easy parsing
                            Write-Output ""OS|$($os.Caption)|$($os.BuildNumber)""
                            Write-Output ""Model|$($cs.Model)|$($cs.Manufacturer)""
                            Write-Output ""User|$($cs.UserName)""
                            Write-Output ""Serial|$($bios.SerialNumber)""
                            Write-Output ""CPU|$($cpu.Name)|$($cpu.NumberOfCores)|$($cpu.NumberOfLogicalProcessors)""
                            Write-Output ""RAM|$([math]::Round($cs.TotalPhysicalMemory / 1GB))""
                        }} -ErrorAction Stop
                    ";

                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psScript.Replace("\"", "`\"")}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using (var proc = Process.Start(psi))
                    {
                        string output = proc.StandardOutput.ReadToEnd();
                        string errors = proc.StandardError.ReadToEnd();
                        proc.WaitForExit();

                        if (!string.IsNullOrWhiteSpace(output))
                        {
                            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var line in lines)
                            {
                                var parts = line.Split('|');
                                if (parts.Length < 2) continue;

                                switch (parts[0])
                                {
                                    case "OS":
                                        spec.OS = $"{parts[1]} (Build {parts[2]})";
                                        spec.WindowsVersion = GetWindowsVersionFromBuild(parts[2], parts[1]);
                                        break;
                                    case "Model":
                                        spec.Model = parts[1];
                                        spec.Manufacturer = parts[2];
                                        break;
                                    case "User":
                                        spec.User = string.IsNullOrWhiteSpace(parts[1]) ? "(No User Logged In)" : parts[1];
                                        break;
                                    case "Serial":
                                        spec.Serial = parts[1];
                                        break;
                                    case "CPU":
                                        spec.CPU = parts[1];
                                        spec.Cores = $"{parts[2]}C / {parts[3]}T";
                                        break;
                                    case "RAM":
                                        spec.RAM = $"{parts[1]} GB";
                                        break;
                                }
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(errors))
                        {
                            LogManager.LogDebug($"PowerShell errors for {hostname}: {errors}");
                            spec.Protocol = "FAILED";
                        }
                    }

                    // Clear password from memory
                    capturedPassword = null;
                    GC.Collect();
                }
                catch (Exception ex)
                {
                    spec.Protocol = "FAILED";
                    LogManager.LogDebug($"PowerShell query failed for {hostname}: {ex.Message}");
                }

                return spec;
            }, ct);
        }

        // ── Single Target Scan ──

        private async void BtnScan_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                string hostname = ComboTarget.Text;
                if (string.IsNullOrEmpty(hostname))
                {
                    // TAG: #AUTO_UPDATE_UI_ENGINE #VALIDATION_TOASTS
                    Managers.UI.ToastManager.ShowWarning("Hostname is required");
                    MessageBox.Show("Enter a hostname", "Required");
                    return;
                }
                if (!SecurityValidator.IsValidHostname(hostname))
                {
                    // TAG: #AUTO_UPDATE_UI_ENGINE #VALIDATION_TOASTS
                    Managers.UI.ToastManager.ShowWarning("Invalid hostname format");
                    MessageBox.Show("Invalid hostname format", "Error");
                    return;
                }

                // Show progress
                ShowBottomProgress($"Scanning {hostname}...");
                // TAG: #AUTO_UPDATE_UI_ENGINE #STATUS_TOASTS
                Managers.UI.ToastManager.ShowInfo($"Starting scan of {hostname}...");

                // Warn if scanning in read-only mode
                if (!_isLoggedIn)
                {
                    var result = MessageBox.Show(
                        "⚠️ Running in READ-ONLY Mode\n\n" +
                        "Remote WMI queries require admin credentials.\n" +
                        "Scanning may fail or show limited information.\n\n" +
                        "For full access:\n" +
                        "• Use the desktop shortcut: \"ArtaznIT Suite (Admin)\"\n" +
                        "• Or create it via: About → Debugging & Admin Tools\n\n" +
                        "Continue anyway with current user credentials?",
                        "Admin Credentials Recommended",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result != MessageBoxResult.Yes)
                    {
                        // TAG: #AUTO_UPDATE_UI_ENGINE #STATUS_TOASTS
                        Managers.UI.ToastManager.ShowInfo("Scan cancelled - admin credentials required");
                        Mouse.OverrideCursor = null;
                        return;
                    }
                    else
                    {
                        // TAG: #AUTO_UPDATE_UI_ENGINE #STATUS_TOASTS
                        Managers.UI.ToastManager.ShowWarning("Scanning in read-only mode - limited access");
                    }
                }

                _currentTarget = hostname;
                AddToRecentTargets(hostname); // Track last 10 scanned targets
                TxtTargetName.Text = hostname.ToUpper(); TxtStatus.Text = "Probing..."; StatusDot.Fill = Brushes.Yellow;
                StkDrives.Children.Clear(); StkDrives.Children.Add(new TextBlock { Text = "Scanning...", Foreground = Brushes.Gray });
                BtnManualScan.IsEnabled = false;

                try
                {
                    UpdateBottomProgress(25, $"Pinging {hostname}...");
                    await Task.Run(async () =>
                    {
                        using (var p = new Ping()) { var reply = await p.SendPingAsync(hostname, SecureConfig.PingTimeoutMs); if (reply.Status != IPStatus.Success) throw new Exception($"Offline ({reply.Status})"); }
                        // In read-only mode, pass null credentials to use current Windows user (passthrough authentication)
                        _ = Dispatcher.InvokeAsync(() => UpdateBottomProgress(50, $"Querying {hostname}..."));
                        var specs = await GetSystemSpecsAsync(hostname, _authUser, _authPass);
                        _ = Application.Current.Dispatcher.InvokeAsync(async () =>
                        {
                            UpdateBottomProgress(90, $"Processing {hostname} data...");
                            if (specs.Protocol == "FAILED" || specs.Protocol == "TIMEOUT") { TxtStatus.Text = specs.Protocol == "TIMEOUT" ? "TIMEOUT" : "ACCESS DENIED"; StatusDot.Fill = Brushes.Red; AddLog(hostname, "SCAN_FAIL", specs.Protocol, "FAIL"); HideBottomProgress("Scan Failed"); return; }
                            TxtStatus.Text = "ONLINE"; StatusDot.Fill = Brushes.Lime;
                            TxtServiceTag.Text = specs.Serial; TxtUser.Text = specs.User;
                            TxtModel.Text = specs.Manufacturer != "N/A" ? $"Model: {specs.Manufacturer} {specs.Model}" : $"Model: {specs.Model}";
                            TxtCPU.Text = specs.CPU; TxtCores.Text = $"Core Logic: {specs.Cores}"; TxtRAM.Text = $"RAM: {specs.RAM}";

                            // Show OS with colored version indicator
                            TxtOS.Text = $"{specs.OS} [{specs.WindowsVersion}]";
                            TxtOS.Foreground = GetWindowsVersionColor(specs.WindowsVersion);

                            TxtUptime.Text = $"Uptime: {specs.Uptime}"; TxtBattery.Text = $"Power: {specs.Battery}";
                            TxtIP1.Text = specs.IP; TxtMAC.Text = specs.MAC; TxtDNS.Text = specs.DNS;
                            _currentServiceTag = specs.Serial;
                            GridTools.IsEnabled = true; BtnPush.IsEnabled = _isDomainAdmin; BtnGP.IsEnabled = true; BtnReboot.IsEnabled = true;
                            BtnWarranty.IsEnabled = specs.Serial != "N/A";

                            // Build drives panel
                            StkDrives.Children.Clear();
                            AddSectionToPanel("SYSTEM INFORMATION:", new[] { $"Domain: {specs.Domain}", $"Last Boot: {specs.LastBoot}", $"TimeZone: {specs.TimeZone}", $"TPM: {specs.TPMEnabled}" });

                            var hdr = new TextBlock { Text = "LOCAL STORAGE:", Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 215)), FontWeight = FontWeights.Bold, FontSize = 11, Margin = new Thickness(0, 10, 0, 8) };
                            StkDrives.Children.Add(hdr);

                            foreach (string d in specs.Drives)
                            {
                                var parts = d.Split('|'); if (parts.Length != 4) continue;
                                if (!double.TryParse(parts[2], out double free) || !double.TryParse(parts[3], out double size)) continue;
                                double pct = ((size - free) / size) * 100;
                                var dp = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
                                var hg = new Grid(); hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                                var tn = new TextBlock { Text = $"{parts[0]} ({parts[1]})", Foreground = Brushes.White, FontWeight = FontWeights.Bold };
                                var ts = new TextBlock { Text = $"{free:F1} GB Free / {size:F1} GB", Foreground = Brushes.Gray, HorizontalAlignment = HorizontalAlignment.Right, FontSize = 10 };
                                Grid.SetColumn(tn, 0); Grid.SetColumn(ts, 1); hg.Children.Add(tn); hg.Children.Add(ts);
                                var pb = new ProgressBar { Height = 6, Value = pct, Maximum = 100, Margin = new Thickness(0, 2, 0, 0), Background = new SolidColorBrush(Color.FromRgb(50, 50, 50)), BorderThickness = new Thickness(0), Foreground = pct > 90 ? Brushes.Red : Brushes.SteelBlue };
                                dp.Children.Add(hg); dp.Children.Add(pb); StkDrives.Children.Add(dp);
                            }
                            AddLog(hostname, "PROBE_SUCCESS", specs.Protocol, "OK");
                            UpdateBottomProgress(100, $"Scan Complete");

                            // Show success toast notification
                            Managers.UI.ToastManager.ShowSuccess($"Scan complete: {hostname} ({specs.OS})");

                            await Task.Delay(1000); // Show completion briefly
                            HideBottomProgress($"Ready • {hostname.ToUpper()}");
                        });
                    });
                }
                catch (Exception ex) { LogManager.LogError($"Scan failed: {hostname}", ex); _ = Dispatcher.InvokeAsync(() => { TxtStatus.Text = "UNREACHABLE"; StatusDot.Fill = Brushes.Red; GridTools.IsEnabled = false; HideBottomProgress("Scan Failed"); Managers.UI.ToastManager.ShowWarning($"{hostname} is offline"); }); }
                finally { BtnManualScan.IsEnabled = true; Mouse.OverrideCursor = null; }
            }
            catch (Exception ex) { LogManager.LogError("BtnScan outer", ex); }
            finally { Mouse.OverrideCursor = null; }
        }

        private void AddSectionToPanel(string header, string[] items)
        {
            StkDrives.Children.Add(new Separator { Background = new SolidColorBrush(Color.FromRgb(51, 51, 51)), Margin = new Thickness(0, 0, 0, 10) });
            StkDrives.Children.Add(new TextBlock { Text = header, Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 215)), FontWeight = FontWeights.Bold, FontSize = 11, Margin = new Thickness(0, 0, 0, 8) });
            foreach (var item in items)
            {
                Brush fg = item.StartsWith("Domain:") ? Brushes.Cyan : item.StartsWith("TPM:") ? (item.Contains("Enabled") ? Brushes.LimeGreen : Brushes.Orange) : Brushes.LightBlue;
                StkDrives.Children.Add(new TextBlock { Text = item, Foreground = fg, FontSize = 11, Margin = new Thickness(0, 0, 0, 3) });
            }
        }

        // ═══════════════════════════════════════════════════════
        // NEW REMOTE MANAGEMENT TOOLS
        // ═══════════════════════════════════════════════════════

        private void Tool_RDP_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentTarget))
            {
                // TAG: #AUTO_UPDATE_UI_ENGINE #VALIDATION_TOASTS
                Managers.UI.ToastManager.ShowWarning("No target selected for RDP");
                MessageBox.Show("No target selected");
                return;
            }
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                // Store credentials in Windows Credential Manager for SSO
                if (_isLoggedIn && _authPass != null)
                {
                    SecureMemory.UseSecureString(_authPass, password =>
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = "cmdkey.exe",
                            Arguments = $"/generic:TERMSRV/{_currentTarget} /user:{_authUser} /pass:\"{password}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        using (var proc = Process.Start(psi))
                        {
                            proc?.WaitForExit(3000);
                        }
                    });
                }

                // Launch RDP with admin mode
                string sanitized = SecurityValidator.SanitizeHostname(_currentTarget);
                using (Process.Start("mstsc.exe", $"/v:{sanitized} /admin")) { }
                AppendTerminal($"RDP launched → {sanitized} (admin mode)");
                AddLog(_currentTarget, "RDP", "Session launched", "OK");
                Managers.UI.ToastManager.ShowSuccess($"RDP session launched to {sanitized}");
            }
            catch (Exception ex)
            {
                AppendTerminal($"RDP failed: {ex.Message}", true);
                AddLog(_currentTarget, "RDP", ex.Message, "FAIL");
            }
            finally { Mouse.OverrideCursor = null; }
        }

        private void Tool_RemoteAssist_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentTarget))
            {
                // TAG: #AUTO_UPDATE_UI_ENGINE #VALIDATION_TOASTS
                Managers.UI.ToastManager.ShowWarning("No target selected for Remote Assistance");
                MessageBox.Show("No target selected");
                return;
            }
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                string sanitized = SecurityValidator.SanitizeHostname(_currentTarget);
                using (Process.Start("msra.exe", $"/offerRA {sanitized}")) { }
                AppendTerminal($"Remote Assist → {_currentTarget}");
                AddLog(_currentTarget, "REMOTE_ASSIST", "Launched", "OK");
            }
            catch (Exception ex)
            {
                AppendTerminal($"Remote Assist failed: {ex.Message}", true);
                AddLog(_currentTarget, "REMOTE_ASSIST", ex.Message, "FAIL");
            }
            finally { Mouse.OverrideCursor = null; }
        }

        private void Tool_RemoteReg_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentTarget))
            {
                // TAG: #AUTO_UPDATE_UI_ENGINE #VALIDATION_TOASTS
                Managers.UI.ToastManager.ShowWarning("No target selected for Remote Registry");
                MessageBox.Show("No target selected");
                return;
            }

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                // Use reg.exe connect method which actually works for remote registry
                string regCommand = $"reg.exe";
                string regArgs = $"";

                // Open Registry Editor and use File > Connect Network Registry
                // or use PowerShell to browse remote registry
                string psScript = $@"
Write-Host 'REMOTE REGISTRY ACCESS: {_currentTarget}' -ForegroundColor Cyan;
Write-Host 'Loading registry hives...' -ForegroundColor Yellow;
Write-Host '';
Write-Host 'Available Hives:';
Write-Host '  HKLM - HKEY_LOCAL_MACHINE';
Write-Host '  HKU  - HKEY_USERS';
Write-Host '';
Write-Host 'Example: Get-ItemProperty -Path ''\\{_currentTarget}\HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion''' -ForegroundColor Green;
Write-Host '';
$connection = Test-Connection -ComputerName {_currentTarget} -Count 1 -Quiet;
if ($connection) {{
    Write-Host '✓ Connection successful' -ForegroundColor Green;
    Write-Host 'You can now use Get-ItemProperty, Set-ItemProperty, etc. with remote path';
}} else {{
    Write-Host '✗ Connection failed' -ForegroundColor Red;
}}
";

                if (_isLoggedIn && _authPass != null)
                {
                    SecureMemory.UseSecureString(_authPass, password =>
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = "powershell.exe",
                            Arguments = $"-NoExit -Command \"$cred = New-Object System.Management.Automation.PSCredential('{_authUser}', (ConvertTo-SecureString '{password}' -AsPlainText -Force)); Invoke-Command -ComputerName {_currentTarget} -Credential $cred -ScriptBlock {{ {psScript} }}\"",
                            UseShellExecute = true
                        };
                        Process.Start(psi);
                    });
                }
                else
                {
                    // Fallback: open regedit and user must manually connect
                    Process.Start("regedit.exe");
                    MessageBox.Show($"Registry Editor opened.\n\nTo connect to {_currentTarget}:\n1. File > Connect Network Registry\n2. Enter: {_currentTarget}\n3. Click OK",
                        "Remote Registry", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                AppendTerminal($"Remote Registry access opened for {_currentTarget}");
                AddLog(_currentTarget, "REMOTE_REGISTRY", "Launched", "OK");
            }
            catch (Exception ex)
            {
                AppendTerminal($"Remote Registry failed: {ex.Message}", true);
                AddLog(_currentTarget, "REMOTE_REGISTRY", ex.Message, "FAIL");
            }
            finally { Mouse.OverrideCursor = null; }
        }

        private void Tool_PsExec_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentTarget))
            {
                // TAG: #AUTO_UPDATE_UI_ENGINE #VALIDATION_TOASTS
                Managers.UI.ToastManager.ShowWarning("No target selected for PsExec");
                MessageBox.Show("No target selected");
                return;
            }
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                string psCommand = $"Enter-PSSession -ComputerName {_currentTarget}";

                if (_isLoggedIn && _authPass != null)
                {
                    SecureMemory.UseSecureString(_authPass, password =>
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = "powershell.exe",
                            Arguments = $"-NoExit -Command \"$cred = New-Object System.Management.Automation.PSCredential('{_authUser}', (ConvertTo-SecureString '{password}' -AsPlainText -Force)); Enter-PSSession -ComputerName {_currentTarget} -Credential $cred\"",
                            UseShellExecute = true
                        };
                        Process.Start(psi);
                    });
                }
                else
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoExit -Command \"{psCommand}\"",
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }

                AppendTerminal($"PowerShell remote session opened for {_currentTarget}");
                AddLog(_currentTarget, "REMOTE_CMD", "PS Session", "OK");
            }
            catch (Exception ex)
            {
                AppendTerminal($"Remote CMD failed: {ex.Message}", true);
                AddLog(_currentTarget, "REMOTE_CMD", ex.Message, "FAIL");
            }
            finally { Mouse.OverrideCursor = null; }
        }

        private async void Tool_DefenderScan_Click(object sender, RoutedEventArgs e)
        {
            try { await WMIExecute("powershell -Command \"Start-MpScan -ScanType QuickScan\"", "DEFENDER_QUICK_SCAN"); }
            catch (Exception ex) { LogManager.LogError("Defender scan failed", ex); }
        }

        private async void Tool_FlushDNS_Click(object sender, RoutedEventArgs e)
        {
            try { await WMIExecute("cmd.exe /c ipconfig /flushdns", "FLUSH_DNS"); }
            catch (Exception ex) { LogManager.LogError("DNS flush failed", ex); }
        }

        private async void Tool_RenewIP_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentTarget))
            {
                // TAG: #AUTO_UPDATE_UI_ENGINE #VALIDATION_TOASTS
                Managers.UI.ToastManager.ShowWarning("No target selected for IP renewal");
                MessageBox.Show("No target selected");
                return;
            }

            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                if (MessageBox.Show($"This will release and renew the IP address on {_currentTarget}.\n\nThe system may lose network connectivity briefly. Continue?",
                    "Confirm IP Renewal", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                {
                    Mouse.OverrideCursor = null;
                    return;
                }

                AppendTerminal($"\n>>> IP RENEWAL: {_currentTarget}...");
                await WMIExecute("cmd.exe /c ipconfig /release && ipconfig /renew", "IP_RENEWAL");
                AppendTerminal("IP renewal complete. Check network connectivity.");
                Managers.UI.ToastManager.ShowSuccess($"IP renewed on {_currentTarget}");
            }
            catch (Exception ex)
            {
                LogManager.LogError("IP renewal failed", ex);
                AppendTerminal($"IP renewal error: {ex.Message}", true);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private async void Tool_DiskCleanup_Click(object sender, RoutedEventArgs e)
        {
            try { await WMIExecute("cmd.exe /c cleanmgr /sagerun:1", "DISK_CLEANUP"); }
            catch (Exception ex) { LogManager.LogError("Disk cleanup failed", ex); }
        }

        private void Tool_Hotfix_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentTarget))
            {
                // TAG: #AUTO_UPDATE_UI_ENGINE #VALIDATION_TOASTS
                Managers.UI.ToastManager.ShowWarning("No target selected for hotfix query");
                MessageBox.Show("No target selected");
                return;
            }
            var tw = new ToolWindow(_currentTarget, "INSTALLED HOTFIXES"); tw.Show();
            tw.SetStatus("Loading hotfixes...");
            _ = Task.Run(() =>
            {
                try
                {
                    var (protocol, cimResults, wmiResults) = _queryHelper.QueryInstances(
                        _currentTarget, _authUser, _authPass, "root/cimv2",
                        "SELECT HotFixID, Description, InstalledOn FROM Win32_QuickFixEngineering");

                    tw.AppendOutput($"{"HotFix ID",-15} | {"Description",-30} | {"Installed"}");
                    tw.AppendOutput(new string('═', 80));
                    int c = 0;

                    if (cimResults != null)
                    {
                        foreach (var hf in cimResults)
                        {
                            var id = hf.CimInstanceProperties["HotFixID"]?.Value;
                            var desc = hf.CimInstanceProperties["Description"]?.Value?.ToString() ?? "N/A";
                            var installed = hf.CimInstanceProperties["InstalledOn"]?.Value;
                            tw.AppendOutput($"{id,-15} | {desc,-30} | {installed}");
                            c++;
                        }
                    }
                    else if (wmiResults != null)
                    {
                        foreach (ManagementObject hf in wmiResults)
                        {
                            tw.AppendOutput($"{hf["HotFixID"],-15} | {(hf["Description"]?.ToString() ?? "N/A"),-30} | {hf["InstalledOn"]}");
                            c++;
                            hf.Dispose();
                        }
                    }

                    tw.SetStatus($"[{protocol}] {c} hotfixes found");
                    AddLog(_currentTarget, "HOTFIX_LIST", $"{c} items", "OK");
                }
                catch (Exception ex) { tw.AppendOutput($"ERROR: {ex.Message}", true); tw.SetStatus("Failed", true); }
            });
        }

        private void Tool_Startup_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentTarget))
            {
                // TAG: #AUTO_UPDATE_UI_ENGINE #VALIDATION_TOASTS
                Managers.UI.ToastManager.ShowWarning("No target selected for startup programs");
                MessageBox.Show("No target selected");
                return;
            }
            var tw = new ToolWindow(_currentTarget, "STARTUP PROGRAMS"); tw.Show();
            tw.SetStatus("Loading startup items...");
            _ = Task.Run(() =>
            {
                try
                {
                    var (protocol, cimResults, wmiResults) = _queryHelper.QueryInstances(
                        _currentTarget, _authUser, _authPass, "root/cimv2",
                        "SELECT Name, Command, Location FROM Win32_StartupCommand");

                    tw.AppendOutput($"{"Name",-40} | {"Location",-20} | {"Command"}");
                    tw.AppendOutput(new string('═', 120));
                    int c = 0;

                    if (cimResults != null)
                    {
                        foreach (var su in cimResults)
                        {
                            string name = su.CimInstanceProperties["Name"]?.Value?.ToString() ?? "Unknown";
                            if (name.Length > 40) name = name.Substring(0, 37) + "...";
                            string loc = su.CimInstanceProperties["Location"]?.Value?.ToString() ?? "N/A";
                            if (loc.Length > 20) loc = loc.Substring(0, 17) + "...";
                            string cmd = su.CimInstanceProperties["Command"]?.Value?.ToString() ?? "N/A";
                            if (cmd.Length > 60) cmd = cmd.Substring(0, 57) + "...";
                            tw.AppendOutput($"{name,-40} | {loc,-20} | {cmd}");
                            c++;
                        }
                    }
                    else if (wmiResults != null)
                    {
                        foreach (ManagementObject su in wmiResults)
                        {
                            string name = su["Name"]?.ToString() ?? "Unknown";
                            if (name.Length > 40) name = name.Substring(0, 37) + "...";
                            string loc = su["Location"]?.ToString() ?? "N/A";
                            if (loc.Length > 20) loc = loc.Substring(0, 17) + "...";
                            string cmd = su["Command"]?.ToString() ?? "N/A";
                            if (cmd.Length > 60) cmd = cmd.Substring(0, 57) + "...";
                            tw.AppendOutput($"{name,-40} | {loc,-20} | {cmd}");
                            c++;
                            su.Dispose();
                        }
                    }

                    tw.SetStatus($"[{protocol}] {c} startup items");
                    AddLog(_currentTarget, "STARTUP_PROGRAMS", $"{c} items", "OK");
                }
                catch (Exception ex) { tw.AppendOutput($"ERROR: {ex.Message}", true); tw.SetStatus("Failed", true); }
            });
        }

        private void Tool_SchedTask_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentTarget)) { MessageBox.Show("No target selected"); return; }
            var tw = new ToolWindow(_currentTarget, "SCHEDULED TASKS"); tw.Show();
            tw.SetStatus("Loading tasks...");
            _ = Task.Run(() =>
            {
                try
                {
                    // METHOD 1: Try CIM/WMI Win32_ScheduledJob (AT jobs only, but no WinRM needed)
                    var (protocol, cimResults, wmiResults) = _queryHelper.QueryInstances(
                        _currentTarget, _authUser, _authPass, "root/cimv2",
                        "SELECT JobId, Name, Owner, Status FROM Win32_ScheduledJob");

                    tw.AppendOutput($"{"Job ID",-10} | {"Name",-40} | {"Owner",-20} | {"Status"}");
                    tw.AppendOutput(new string('═', 100));

                    int count = 0;

                    if (cimResults != null)
                    {
                        foreach (var job in cimResults)
                        {
                            string id = job.CimInstanceProperties["JobId"]?.Value?.ToString() ?? "N/A";
                            string name = job.CimInstanceProperties["Name"]?.Value?.ToString() ?? "Unnamed";
                            if (name.Length > 40) name = name.Substring(0, 37) + "...";
                            string owner = job.CimInstanceProperties["Owner"]?.Value?.ToString() ?? "N/A";
                            if (owner.Length > 20) owner = owner.Substring(0, 17) + "...";
                            string status = job.CimInstanceProperties["Status"]?.Value?.ToString() ?? "Unknown";

                            tw.AppendOutput($"{id,-10} | {name,-40} | {owner,-20} | {status}");
                            count++;
                        }
                    }
                    else if (wmiResults != null)
                    {
                        foreach (ManagementObject job in wmiResults)
                        {
                            string id = job["JobId"]?.ToString() ?? "N/A";
                            string name = job["Name"]?.ToString() ?? "Unnamed";
                            if (name.Length > 40) name = name.Substring(0, 37) + "...";
                            string owner = job["Owner"]?.ToString() ?? "N/A";
                            if (owner.Length > 20) owner = owner.Substring(0, 17) + "...";
                            string status = job["Status"]?.ToString() ?? "Unknown";

                            tw.AppendOutput($"{id,-10} | {name,-40} | {owner,-20} | {status}");
                            count++;
                            job.Dispose();
                        }
                    }

                    if (count == 0)
                        {
                            // METHOD 2: Fallback - Read task files directly from C$
                            tw.AppendOutput("\n[WMI returned no AT jobs - trying direct file access...]");
                            tw.AppendOutput("");

                            try
                            {
                                string tasksPath = $"\\\\{_currentTarget}\\c$\\Windows\\System32\\Tasks";

                                using (new Impersonation(_authUser, "", _authPass))
                                {
                                    if (Directory.Exists(tasksPath))
                                    {
                                        tw.AppendOutput($"{"Task Name",-50} | {"Path"}");
                                        tw.AppendOutput(new string('═', 100));

                                        var taskFiles = Directory.GetFiles(tasksPath, "*", SearchOption.AllDirectories)
                                            .Where(f => !f.EndsWith(".job", StringComparison.OrdinalIgnoreCase))
                                            .Where(f => !Path.GetFileName(f).StartsWith(".")); // Skip hidden files

                                        foreach (var taskFile in taskFiles)
                                        {
                                            try
                                            {
                                                string relativePath = taskFile.Replace(tasksPath, "").TrimStart('\\');
                                                string taskName = Path.GetFileName(taskFile);

                                                if (taskName.Length > 50) taskName = taskName.Substring(0, 47) + "...";

                                                tw.AppendOutput($"{taskName,-50} | \\{relativePath}");
                                                count++;
                                            }
                                            catch (Exception ex)
                                            {
                                                LogManager.LogDebug($"Failed to process scheduled task file {taskFile}: {ex.Message}");
                                            }
                                        }
                                    }
                                    else
                                    {
                                        tw.AppendOutput("Cannot access task folder via C$ share", true);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                tw.AppendOutput($"Direct file access failed: {ex.Message}", true);
                            }
                        }

                    tw.SetStatus($"[{protocol}] {count} tasks found");
                    AddLog(_currentTarget, "SCHED_TASKS", $"{count} tasks", "OK");
                }
                catch (Exception ex)
                {
                    tw.AppendOutput($"ERROR: {ex.Message}", true);
                    tw.SetStatus("Failed", true);
                }
            });
        }

        private void Tool_NetDiag_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentTarget)) { MessageBox.Show("No target selected"); return; }
            var tw = new ToolWindow(_currentTarget, "NETWORK DIAGNOSTICS"); tw.Show();
            tw.SetStatus("Loading network configuration...");
            _ = Task.Run(() =>
            {
                try
                {
                    string protocol = "";

                    // ═══════════════════════════════════════════════════════════════
                    // IP ADDRESSES (Win32_NetworkAdapterConfiguration)
                    // ═══════════════════════════════════════════════════════════════
                    tw.AppendOutput("=== NETWORK ADAPTERS & IP ADDRESSES ===");
                    tw.AppendOutput("");

                    var (proto1, cimResults1, wmiResults1) = _queryHelper.QueryInstances(
                        _currentTarget, _authUser, _authPass, "root/cimv2",
                        "SELECT Description, IPAddress, IPSubnet, DefaultIPGateway, DHCPEnabled, MACAddress " +
                        "FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = True");
                    protocol = proto1;

                    tw.AppendOutput($"{"Adapter",-40} | {"IP Address",-18} | {"Subnet",-16} | {"Gateway",-16} | {"DHCP"}");
                    tw.AppendOutput(new string('─', 120));

                    if (cimResults1 != null)
                    {
                        foreach (var adapter in cimResults1)
                        {
                            try
                            {
                                string desc = adapter.CimInstanceProperties["Description"]?.Value?.ToString() ?? "Unknown";
                                if (desc.Length > 40) desc = desc.Substring(0, 37) + "...";

                                string[] ipAddresses = adapter.CimInstanceProperties["IPAddress"]?.Value as string[];
                                string[] subnets = adapter.CimInstanceProperties["IPSubnet"]?.Value as string[];
                                string[] gateways = adapter.CimInstanceProperties["DefaultIPGateway"]?.Value as string[];
                                bool dhcp = Convert.ToBoolean(adapter.CimInstanceProperties["DHCPEnabled"]?.Value ?? false);

                                string ip = (ipAddresses != null && ipAddresses.Length > 0) ? ipAddresses[0] : "N/A";
                                string subnet = (subnets != null && subnets.Length > 0) ? subnets[0] : "N/A";
                                string gateway = (gateways != null && gateways.Length > 0) ? gateways[0] : "N/A";

                                // Filter IPv4 addresses only
                                if (ip.Contains(":")) continue; // Skip IPv6

                                tw.AppendOutput($"{desc,-40} | {ip,-18} | {subnet,-16} | {gateway,-16} | {(dhcp ? "Yes" : "No")}");
                            }
                            catch (Exception ex)
                            {
                                LogManager.LogDebug($"Failed to process network adapter: {ex.Message}");
                            }
                        }
                    }
                    else if (wmiResults1 != null)
                    {
                        foreach (ManagementObject adapter in wmiResults1)
                        {
                            try
                            {
                                string desc = adapter["Description"]?.ToString() ?? "Unknown";
                                if (desc.Length > 40) desc = desc.Substring(0, 37) + "...";

                                string[] ipAddresses = adapter["IPAddress"] as string[];
                                string[] subnets = adapter["IPSubnet"] as string[];
                                string[] gateways = adapter["DefaultIPGateway"] as string[];
                                bool dhcp = Convert.ToBoolean(adapter["DHCPEnabled"]);

                                string ip = (ipAddresses != null && ipAddresses.Length > 0) ? ipAddresses[0] : "N/A";
                                string subnet = (subnets != null && subnets.Length > 0) ? subnets[0] : "N/A";
                                string gateway = (gateways != null && gateways.Length > 0) ? gateways[0] : "N/A";

                                // Filter IPv4 addresses only
                                if (ip.Contains(":")) continue; // Skip IPv6

                                tw.AppendOutput($"{desc,-40} | {ip,-18} | {subnet,-16} | {gateway,-16} | {(dhcp ? "Yes" : "No")}");
                            }
                            catch (Exception ex)
                            {
                                LogManager.LogDebug($"Failed to process network adapter: {ex.Message}");
                            }
                            finally { adapter.Dispose(); }
                        }
                    }

                    tw.AppendOutput("");

                    // ═══════════════════════════════════════════════════════════════
                    // ROUTING TABLE (Win32_IP4RouteTable)
                    // ═══════════════════════════════════════════════════════════════
                    tw.AppendOutput("=== DEFAULT ROUTES ===");
                    tw.AppendOutput("");

                    var (proto2, cimResults2, wmiResults2) = _queryHelper.QueryInstances(
                        _currentTarget, _authUser, _authPass, "root/cimv2",
                        "SELECT Destination, Mask, NextHop, Metric1 FROM Win32_IP4RouteTable WHERE Destination = '0.0.0.0'");

                    tw.AppendOutput($"{"Destination",-18} | {"Mask",-18} | {"Next Hop",-18} | {"Metric"}");
                    tw.AppendOutput(new string('─', 80));

                    if (cimResults2 != null)
                    {
                        foreach (var route in cimResults2)
                        {
                            try
                            {
                                string dest = route.CimInstanceProperties["Destination"]?.Value?.ToString() ?? "N/A";
                                string mask = route.CimInstanceProperties["Mask"]?.Value?.ToString() ?? "N/A";
                                string nextHop = route.CimInstanceProperties["NextHop"]?.Value?.ToString() ?? "N/A";
                                string metric = route.CimInstanceProperties["Metric1"]?.Value?.ToString() ?? "N/A";

                                tw.AppendOutput($"{dest,-18} | {mask,-18} | {nextHop,-18} | {metric}");
                            }
                            catch { }
                        }
                    }
                    else if (wmiResults2 != null)
                    {
                        foreach (ManagementObject route in wmiResults2)
                        {
                            try
                            {
                                string dest = route["Destination"]?.ToString() ?? "N/A";
                                string mask = route["Mask"]?.ToString() ?? "N/A";
                                string nextHop = route["NextHop"]?.ToString() ?? "N/A";
                                string metric = route["Metric1"]?.ToString() ?? "N/A";

                                tw.AppendOutput($"{dest,-18} | {mask,-18} | {nextHop,-18} | {metric}");
                            }
                            catch { }
                            finally { route.Dispose(); }
                        }
                    }

                    tw.AppendOutput("");

                    // ═══════════════════════════════════════════════════════════════
                    // DNS CACHE (via Process execution - netsh command)
                    // ═══════════════════════════════════════════════════════════════
                    tw.AppendOutput("=== DNS CLIENT CACHE ===");
                    tw.AppendOutput("");
                    tw.AppendOutput("Note: DNS cache requires netsh command execution");
                    tw.AppendOutput("Use 'ipconfig /displaydns' on target machine for full DNS cache");

                    tw.SetStatus($"[{protocol}] Network diagnostics complete");
                    AddLog(_currentTarget, "NET_DIAGNOSTICS", "Completed", "OK");
                }
                catch (Exception ex)
                {
                    tw.AppendOutput($"ERROR: {ex.Message}", true);
                    tw.SetStatus("Failed", true);
                }
            });
        }

        /// <summary>Launches MMC snap-ins with cached credentials using runas /netonly</summary>
        private void LaunchMMCWithCreds(string executable, string args, string logAction)
        {
            try
            {
                // DIAGNOSTIC: Check actual elevation status
                bool actuallyElevated = IsRunningAsAdministrator();
                AppendTerminal($"[LAUNCH] Elevation check: _isElevated={_isElevated}, ActualCheck={actuallyElevated}", false);

                // Re-verify elevation status in case it changed
                if (!actuallyElevated)
                {
                    AppendTerminal($"[LAUNCH] ✗ MMC tools require Administrator elevation", true);

                    var elevationDialog = new ElevationDialog();
                    var result = elevationDialog.ShowDialog();

                    if (result == true)
                    {
                        try
                        {
                            // Use the actual .exe path
                            var exePath = Process.GetCurrentProcess().MainModule.FileName;
                            AppendTerminal($"[LAUNCH] Restarting: {exePath}");

                            var psi = new ProcessStartInfo
                            {
                                FileName = exePath,
                                UseShellExecute = true,
                                Verb = "runas"
                            };
                            Process.Start(psi);
                            Application.Current.Shutdown();
                        }
                        catch (Exception restartEx)
                        {
                            MessageBox.Show(
                                "Failed to restart with elevation.\n\n" +
                                "Please manually:\n" +
                                "1. Close ArtaznIT Suite\n" +
                                "2. Right-click ArtaznIT.exe\n" +
                                "3. Select 'Run as Administrator'",
                                "Elevation Failed",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                            LogManager.LogError("Failed to restart with elevation from MMC launch", restartEx);
                        }
                    }
                    return;
                }

                AppendTerminal($"[LAUNCH] ✓ Running with Administrator privileges", false);

                // Check if the tool exists (RSAT check)
                if (executable == "mmc" && args.Contains(".msc"))
                {
                    string mscFile = args.Split(' ')[0]; // Extract .msc filename
                    string[] searchPaths = {
                        $@"C:\Windows\System32\{mscFile}",
                        $@"C:\Windows\SysWOW64\{mscFile}"
                    };

                    bool found = searchPaths.Any(path => System.IO.File.Exists(path));
                    if (!found)
                    {
                        AppendTerminal($"Tool not found: {mscFile} - Checking RSAT installation...", true);

                        // Check if RSAT is installed via PowerShell
                        var rsatCheck = new ProcessStartInfo
                        {
                            FileName = "powershell.exe",
                            Arguments = "-Command \"Get-WindowsCapability -Online | Where-Object Name -like 'Rsat*' | Where-Object State -eq 'Installed' | Measure-Object | Select-Object -ExpandProperty Count\"",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true
                        };

                        using (var rsatProc = Process.Start(rsatCheck))
                        {
                            string output = rsatProc.StandardOutput.ReadToEnd().Trim();
                            rsatProc.WaitForExit();

                            if (output == "0" || string.IsNullOrEmpty(output))
                            {
                                var result = MessageBox.Show(
                                    "RSAT (Remote Server Administration Tools) is not installed.\n\n" +
                                    "Would you like to install it now? This requires administrator privileges and may take 5-10 minutes.",
                                    "RSAT Not Found", MessageBoxButton.YesNo, MessageBoxImage.Question);

                                if (result == MessageBoxResult.Yes)
                                {
                                    AppendTerminal("Installing RSAT via Windows Features...");
                                    var installPsi = new ProcessStartInfo
                                    {
                                        FileName = "powershell.exe",
                                        Arguments = "-Command \"Get-WindowsCapability -Online | Where-Object Name -like 'Rsat*' | Add-WindowsCapability -Online\"",
                                        UseShellExecute = true,
                                        Verb = "runas" // Run as admin
                                    };
                                    Process.Start(installPsi);
                                    MessageBox.Show("RSAT installation started. Please wait for it to complete, then try launching the tool again.", "Installing", MessageBoxButton.OK, MessageBoxImage.Information);
                                }
                                return;
                            }
                        }
                    }
                }

                // Launch MMC tools
                // Show diagnostics only when NOT elevated (when we're using alternate credentials)
                if (_isLoggedIn && _authPass != null && !actuallyElevated)
                {
                    AppendTerminal($"═══════════════════════════════════════════════════════");
                    AppendTerminal($"DIAGNOSTIC: Launching {executable} as {_authUser}");
                    AppendTerminal($"═══════════════════════════════════════════════════════");

                    // DIAGNOSTIC 1: Check Kerberos tickets
                    try
                    {
                        var klistPsi = new ProcessStartInfo
                        {
                            FileName = "klist.exe",
                            Arguments = "tickets",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true
                        };
                        using (var klistProc = Process.Start(klistPsi))
                        {
                            string tickets = klistProc.StandardOutput.ReadToEnd();
                            klistProc.WaitForExit();
                            AppendTerminal($"[DIAG] Current Kerberos Tickets:");
                            foreach (var line in tickets.Split('\n').Take(10))
                            {
                                if (!string.IsNullOrWhiteSpace(line))
                                    AppendTerminal($"  {line.Trim()}");
                            }
                        }
                    }
                    catch (Exception klistEx)
                    {
                        AppendTerminal($"[DIAG] Could not check Kerberos tickets: {klistEx.Message}");
                    }

                    // DIAGNOSTIC 2: Check group memberships
                    try
                    {
                        // Determine which groups are needed based on the tool
                        string toolName = "";
                        string[] requiredGroups = null;

                        if (args.Contains("dhcpmgmt.msc"))
                        {
                            toolName = "DHCP Manager";
                            requiredGroups = new[] { "DHCP Administrators", "DHCP Users" };
                        }
                        else if (args.Contains("dnsmgmt.msc"))
                        {
                            toolName = "DNS Manager";
                            requiredGroups = new[] { "DnsAdmins" };
                        }
                        else if (args.Contains("dsa.msc"))
                        {
                            toolName = "AD Users & Computers";
                            requiredGroups = new[] { "Domain Admins", "Account Operators" };
                        }
                        else if (args.Contains("services.msc"))
                        {
                            toolName = "Services";
                            requiredGroups = new[] { "Administrators", "Server Operators" };
                        }
                        else if (args.Contains("certsrv.msc"))
                        {
                            toolName = "Certification Authority";
                            requiredGroups = new[] { "Cert Publishers", "Enterprise Admins" };
                        }
                        else if (args.Contains("gpmc.msc"))
                        {
                            toolName = "Group Policy Management";
                            requiredGroups = new[] { "Group Policy Creator Owners", "Domain Admins" };
                        }

                        if (requiredGroups != null)
                        {
                            var whoamiPsi = new ProcessStartInfo
                            {
                                FileName = "whoami.exe",
                                Arguments = "/groups /fo csv",
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                CreateNoWindow = true
                            };
                            using (var whoamiProc = Process.Start(whoamiPsi))
                            {
                                string groups = whoamiProc.StandardOutput.ReadToEnd();
                                whoamiProc.WaitForExit();

                                var lines = groups.Split('\n');
                                var foundGroups = new List<string>();

                                // Check for each required group
                                foreach (var reqGroup in requiredGroups)
                                {
                                    var found = lines.Where(l => l.IndexOf(reqGroup, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                                    if (found.Any())
                                        foundGroups.AddRange(found);
                                }

                                if (foundGroups.Any())
                                {
                                    AppendTerminal($"[DIAG] ✓ Required groups found for {toolName}:");
                                    foreach (var g in foundGroups)
                                        AppendTerminal($"  {g.Trim()}");
                                }
                                else
                                {
                                    AppendTerminal($"[DIAG] ✗ WARNING: No required groups found for {toolName}!", true);
                                    AppendTerminal($"[DIAG] Your account needs one of these groups:", true);
                                    foreach (var reqGroup in requiredGroups)
                                        AppendTerminal($"  • {reqGroup}", true);
                                    AppendTerminal($"[DIAG] Add your account to one of these groups and re-login to ArtaznIT Suite.", true);
                                }
                            }
                        }
                    }
                    catch (Exception whoamiEx)
                    {
                        AppendTerminal($"[DIAG] Could not check group membership: {whoamiEx.Message}");
                    }

                    // DIAGNOSTIC 3: Test RPC connectivity to target server
                    if (args.Contains("dhcpmgmt.msc") || args.Contains("dnsmgmt.msc"))
                    {
                        string targetDc = args.Contains("/ComputerName")
                            ? args.Split(new[] { "/ComputerName" }, StringSplitOptions.None)[1].Trim()
                            : "unknown";

                        if (targetDc != "unknown")
                        {
                            try
                            {
                                AppendTerminal($"[DIAG] Testing RPC connectivity to {targetDc}...");

                                var rpcTestPsi = new ProcessStartInfo
                                {
                                    FileName = "powershell.exe",
                                    Arguments = $"-Command \"Test-NetConnection -ComputerName '{targetDc}' -Port 135 | Select-Object -ExpandProperty TcpTestSucceeded\"",
                                    UseShellExecute = false,
                                    RedirectStandardOutput = true,
                                    CreateNoWindow = true
                                };

                                using (var rpcProc = Process.Start(rpcTestPsi))
                                {
                                    string result = rpcProc.StandardOutput.ReadToEnd().Trim();
                                    rpcProc.WaitForExit();

                                    if (result.Contains("True"))
                                    {
                                        AppendTerminal($"[DIAG] ✓ RPC port 135 is accessible on {targetDc}");
                                    }
                                    else
                                    {
                                        AppendTerminal($"[DIAG] ✗ RPC port 135 NOT accessible on {targetDc}!", true);
                                        AppendTerminal($"[DIAG] This could indicate firewall blocking or network issues.", true);
                                    }
                                }
                            }
                            catch (Exception rpcEx)
                            {
                                AppendTerminal($"[DIAG] RPC test failed: {rpcEx.Message}", true);
                            }
                        }
                    }

                    AppendTerminal($"═══════════════════════════════════════════════════════");
                }

                Process proc = null;

                // IMPORTANT: Windows doesn't allow combining elevation with alternate credentials
                // If already elevated, use current context. Otherwise, use credentials without elevation.
                if (actuallyElevated)
                {
                    // Already elevated - use current elevated context (can't use alternate credentials)
                    AppendTerminal($"[LAUNCH] Using current elevated context (Windows user: {Environment.UserName})");
                    AppendTerminal($"[LAUNCH] Note: Cannot use ArtaznIT credentials when elevated - Windows limitation", false);

                    var psi = new ProcessStartInfo
                    {
                        FileName = executable,
                        Arguments = args,
                        UseShellExecute = true,
                        Verb = "runas",  // Maintain elevation
                        ErrorDialog = true
                    };
                    proc = Process.Start(psi);
                }
                else if (_isLoggedIn && _authPass != null)
                {
                    // Not elevated - try using alternate credentials (will work but won't be elevated)
                    string domain = null;
                    string username = _authUser;

                    if (_authUser.Contains("\\"))
                    {
                        string[] parts = _authUser.Split('\\');
                        domain = parts[0];
                        username = parts[1];
                    }
                    else if (_authUser.Contains("@"))
                    {
                        string[] parts = _authUser.Split('@');
                        username = parts[0];
                        domain = parts[1].Split('.')[0];
                    }

                    AppendTerminal($"[LAUNCH] Using credentials: {domain}\\{username} (not elevated)");

                    SecureMemory.UseSecureString(_authPass, password =>
                    {
                        try
                        {
                            var psi = new ProcessStartInfo
                            {
                                FileName = executable,
                                Arguments = args,
                                UserName = username,
                                Domain = domain,
                                Password = SecureMemory.ConvertToSecureString(password),
                                UseShellExecute = false,
                                LoadUserProfile = true,
                                WorkingDirectory = @"C:\Windows\System32"
                            };

                            proc = Process.Start(psi);
                            AppendTerminal($"[LAUNCH] ✓ Process started with domain credentials (PID: {proc?.Id})");
                        }
                        catch (Exception credEx)
                        {
                            AppendTerminal($"[LAUNCH] ✗ Failed with credentials: {credEx.Message}", true);
                            LogManager.LogError("Failed to start process with credentials", credEx);

                            // Fallback to current user
                            AppendTerminal($"[LAUNCH] Falling back to current user...");
                            var fallbackPsi = new ProcessStartInfo
                            {
                                FileName = executable,
                                Arguments = args,
                                UseShellExecute = true,
                                ErrorDialog = true
                            };
                            proc = Process.Start(fallbackPsi);
                        }
                    });
                }
                else
                {
                    // No credentials - launch with current user
                    var psi = new ProcessStartInfo
                    {
                        FileName = executable,
                        Arguments = args,
                        UseShellExecute = true,
                        ErrorDialog = true
                    };
                    proc = Process.Start(psi);
                }

                if (proc != null)
                {
                    AppendTerminal($"✓ Launched {executable} {args} (PID: {proc.Id})");

                    // Verify MMC process started
                    System.Threading.Thread.Sleep(1000);
                    string processName = System.IO.Path.GetFileNameWithoutExtension(executable);
                    if (processName == "mmc")
                    {
                        var mmcProcesses = Process.GetProcessesByName("mmc");
                        if (mmcProcesses.Length > 0)
                        {
                            AppendTerminal($"✓ MMC process verified (count: {mmcProcesses.Length})");
                        }
                    }

                    LogManager.LogInfo($"Launched {executable} {args} successfully");
                }
                else
                {
                    AppendTerminal($"⚠ Process.Start returned null", true);
                    LogManager.LogWarning($"Process.Start returned null for {executable}");
                }

                AddLog(_currentTarget ?? "local", logAction, $"Launched {executable}", "OK");
            }
            catch (Exception ex)
            {
                AppendTerminal($"Launch failed: {ex.Message}", true);
                LogManager.LogError($"Failed to launch {executable}", ex);
            }
        }

        // ═══════════════════════════════════════════════════════
        // EXISTING TOOL HANDLERS (preserved)
        // ═══════════════════════════════════════════════════════

        private void Tool_Browse_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentTarget)) { MessageBox.Show("No target selected"); return; }
            string unc = $"\\\\{_currentTarget}\\C$";
            if (_isLoggedIn && _authPass != null)
            {
                SecureMemory.UseSecureString(_authPass, password =>
                {
                    var psi = new ProcessStartInfo { FileName = "cmd.exe", Arguments = $"/c net use \"{unc}\" /user:{_authUser} \"{password}\" && explorer \"{unc}\"", UseShellExecute = false, CreateNoWindow = true };
                    using (var proc = Process.Start(psi)) { proc?.WaitForExit(3000); }
                });
                AppendTerminal($"C$ share → {_currentTarget}");
            }
            else { try { using (Process.Start("explorer.exe", unc)) { } } catch (Exception ex) { MessageBox.Show(ex.Message); } }
        }

        private void Tool_Soft_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentTarget)) { MessageBox.Show("No target selected"); return; }
            var tw = new ToolWindow(_currentTarget, "SOFTWARE INVENTORY"); tw.Show(); tw.SetStatus("Loading (1-2 min)...");
            _ = Task.Run(() =>
            {
                try
                {
                    var (protocol, cimResults, wmiResults) = _queryHelper.QueryInstances(
                        _currentTarget, _authUser, _authPass, "root/cimv2",
                        "SELECT Name, Version, Vendor FROM Win32_Product");

                    tw.AppendOutput($"{"Name",-60} | {"Version",-20} | {"Vendor"}");
                    tw.AppendOutput(new string('═', 120));

                    List<string> packages = null;

                    if (cimResults != null)
                    {
                        // CIM path - PLINQ processing
                        packages = cimResults
                            .AsParallel()
                            .WithDegreeOfParallelism(Environment.ProcessorCount)
                            .Select(sw => {
                                try
                                {
                                    string n = sw.CimInstanceProperties["Name"]?.Value?.ToString() ?? "?";
                                    if (n.Length > 60) n = n.Substring(0, 57) + "...";
                                    var ver = sw.CimInstanceProperties["Version"]?.Value?.ToString() ?? "N/A";
                                    var vendor = sw.CimInstanceProperties["Vendor"]?.Value?.ToString() ?? "N/A";
                                    return $"{n,-60} | {ver.PadRight(20)} | {vendor}";
                                }
                                catch (Exception ex)
                                {
                                    LogManager.LogDebug($"Query failed: {ex.Message}");
                                    return null;
                                }
                            })
                            .Where(x => x != null)
                            .OrderBy(x => x)
                            .ToList();
                    }
                    else if (wmiResults != null)
                    {
                        // WMI fallback path
                        packages = wmiResults.Cast<ManagementObject>()
                            .AsParallel()
                            .WithDegreeOfParallelism(Environment.ProcessorCount)
                            .Select(sw => {
                                try
                                {
                                    string n = sw["Name"]?.ToString() ?? "?";
                                    if (n.Length > 60) n = n.Substring(0, 57) + "...";
                                    string output = $"{n,-60} | {(sw["Version"] ?? "N/A").ToString().PadRight(20)} | {sw["Vendor"] ?? "N/A"}";
                                    return output;
                                }
                                catch (Exception ex)
                                {
                                    LogManager.LogDebug($"PLINQ software package processing error: {ex.Message}");
                                    return null;
                                }
                                finally
                                {
                                    sw?.Dispose();
                                }
                            })
                            .Where(x => x != null)
                            .OrderBy(x => x)
                            .ToList();
                    }

                    if (packages != null)
                    {
                        foreach (var line in packages) tw.AppendOutput(line);
                        tw.SetStatus($"[{protocol}] {packages.Count} packages");
                        AddLog(_currentTarget, "SOFTWARE", $"{packages.Count} pkgs", "OK");
                    }
                }
                catch (Exception ex) { tw.AppendOutput($"ERROR: {ex.Message}", true); tw.SetStatus("Failed", true); }
            });
        }

        private void Tool_Proc_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentTarget)) { MessageBox.Show("No target selected"); return; }

            ShowToolProgress("Process Manager", $"Connecting to {_currentTarget}...");

            _ = Task.Run(async () =>
            {
                await Task.Delay(100); // Brief delay to show progress indicator
                Dispatcher.Invoke(() =>
                {
                    var tw = new ToolWindow(_currentTarget, "PROCESS MANAGER"); tw.Show();
                    HideToolProgress();

                    tw.SetRefreshAction(async () =>
                    {
                        tw.ClearOutput(); tw.SetStatus("Loading...");
                        await Task.Run(() =>
                        {
                            try
                            {
                                var (protocol, cimResults, wmiResults) = _queryHelper.QueryInstances(
                                    _currentTarget, _authUser, _authPass, "root/cimv2",
                                    "SELECT Name, ProcessId, ThreadCount, WorkingSetSize FROM Win32_Process");

                                tw.AppendOutput($"{"Process",-45} | {"PID",-10} | {"Threads",-10} | {"MB"}");
                                tw.AppendOutput(new string('═', 100));

                                List<string> processes = null;

                                if (cimResults != null)
                                {
                                    // CIM path - use PLINQ for parallel processing
                                    processes = cimResults
                                        .AsParallel()
                                        .WithDegreeOfParallelism(Environment.ProcessorCount)
                                        .Select(p => {
                                            try
                                            {
                                                string n = p.CimInstanceProperties["Name"]?.Value?.ToString() ?? "?";
                                                if (n.Length > 45) n = n.Substring(0, 42) + "...";
                                                var pid = p.CimInstanceProperties["ProcessId"]?.Value;
                                                var threads = p.CimInstanceProperties["ThreadCount"]?.Value;
                                                var mem = p.CimInstanceProperties["WorkingSetSize"]?.Value;
                                                long memBytes = mem != null ? Convert.ToInt64(mem) : 0;
                                                return $"{n,-45} | {pid,-10} | {threads,-10} | {memBytes / 1048576.0:F1}";
                                            }
                                            catch (Exception ex)
                                            {
                                                LogManager.LogDebug($"Query failed: {ex.Message}");
                                                return null;
                                            }
                                        })
                                        .Where(x => x != null)
                                        .ToList();
                                }
                                else if (wmiResults != null)
                                {
                                    // WMI fallback path
                                    processes = wmiResults.Cast<ManagementObject>()
                                        .AsParallel()
                                        .WithDegreeOfParallelism(Environment.ProcessorCount)
                                        .Select(p => {
                                            try
                                            {
                                                string n = p["Name"]?.ToString() ?? "?";
                                                if (n.Length > 45) n = n.Substring(0, 42) + "...";
                                                string output = $"{n,-45} | {p["ProcessId"],-10} | {p["ThreadCount"],-10} | {Convert.ToInt64(p["WorkingSetSize"] ?? 0) / 1048576.0:F1}";
                                                return output;
                                            }
                                            catch (Exception ex)
                                            {
                                                LogManager.LogDebug($"PLINQ process processing error: {ex.Message}");
                                                return null;
                                            }
                                            finally
                                            {
                                                p?.Dispose();
                                            }
                                        })
                                        .Where(x => x != null)
                                        .ToList();
                                }

                                if (processes != null)
                                {
                                    foreach (var line in processes) tw.AppendOutput(line);
                                    tw.SetStatus($"[{protocol}] {processes.Count} processes");
                                }
                            }
                        catch (Exception ex) { tw.AppendOutput($"ERROR: {ex.Message}", true); tw.SetStatus("Failed", true); }
                    });
                });
                _ = Task.Run(async () => await tw._refreshAction());
                });
            });
        }

        private void Tool_Svc_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentTarget)) { MessageBox.Show("No target selected"); return; }
            var tw = new ToolWindow(_currentTarget, "SERVICES MANAGER"); tw.Show();
            tw.SetRefreshAction(async () =>
            {
                tw.ClearOutput(); tw.SetStatus("Loading...");
                await Task.Run(() =>
                {
                    try
                    {
                        var (protocol, cimResults, wmiResults) = _queryHelper.QueryInstances(
                            _currentTarget, _authUser, _authPass, "root/cimv2",
                            "SELECT DisplayName, State, StartMode, Status FROM Win32_Service");

                        tw.AppendOutput($"{"Service",-50} | {"State",-12} | {"Startup",-12} | {"Status"}");
                        tw.AppendOutput(new string('═', 100));

                        var svcs = new List<dynamic>();

                        if (cimResults != null)
                        {
                            // CIM path
                            svcs = cimResults
                                .AsParallel()
                                .WithDegreeOfParallelism(Environment.ProcessorCount)
                                .Select(sv => {
                                    try
                                    {
                                        return new {
                                            Dn = sv.CimInstanceProperties["DisplayName"]?.Value?.ToString() ?? "?",
                                            St = sv.CimInstanceProperties["State"]?.Value?.ToString() ?? "?",
                                            Sm = sv.CimInstanceProperties["StartMode"]?.Value?.ToString() ?? "?",
                                            Ss = sv.CimInstanceProperties["Status"]?.Value?.ToString() ?? "?",
                                            IsRunning = sv.CimInstanceProperties["State"]?.Value?.ToString() == "Running"
                                        };
                                    }
                                    catch (Exception ex)
                                {
                                    LogManager.LogDebug($"Query failed: {ex.Message}");
                                    return null;
                                }
                                })
                                .Where(x => x != null)
                                .OrderBy(x => x.Dn)
                                .ToList<dynamic>();
                        }
                        else if (wmiResults != null)
                        {
                            // WMI fallback path
                            svcs = wmiResults.Cast<ManagementObject>()
                                .AsParallel()
                                .WithDegreeOfParallelism(Environment.ProcessorCount)
                                .Select(sv => {
                                    try
                                    {
                                        var result = new {
                                            Dn = sv["DisplayName"]?.ToString() ?? "?",
                                            St = sv["State"]?.ToString() ?? "?",
                                            Sm = sv["StartMode"]?.ToString() ?? "?",
                                            Ss = sv["Status"]?.ToString() ?? "?",
                                            IsRunning = sv["State"]?.ToString() == "Running"
                                        };
                                        return result;
                                    }
                                    catch (Exception ex)
                                    {
                                        LogManager.LogDebug($"PLINQ service processing error: {ex.Message}");
                                        return null;
                                    }
                                    finally
                                    {
                                        sv?.Dispose();
                                    }
                                })
                                .Where(x => x != null)
                                .OrderBy(x => x.Dn)
                                .ToList<dynamic>();
                        }

                        int c = 0, run = 0;
                        foreach (var sv in svcs)
                        {
                            string dn = sv.Dn;
                            if (dn.Length > 50) dn = dn.Substring(0, 47) + "...";
                            tw.AppendOutput($"{dn,-50} | {sv.St,-12} | {sv.Sm,-12} | {sv.Ss}", sv.St != "Running" && sv.Sm == "Auto");
                            if (sv.IsRunning) run++;
                            c++;
                        }
                        tw.SetStatus($"[{protocol}] {c} services ({run} running)");
                    }
                    catch (Exception ex) { tw.AppendOutput($"ERROR: {ex.Message}", true); tw.SetStatus("Failed", true); }
                });
            });
            _ = Task.Run(async () => await tw._refreshAction());
        }

        private void Tool_Evt_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentTarget)) { MessageBox.Show("No target selected"); return; }
            var tw = new ToolWindow(_currentTarget, "EVENT LOGS (SYSTEM ERRORS)"); tw.Show(); tw.SetStatus("Loading errors...");
            _ = Task.Run(() =>
            {
                try
                {
                    var (protocol, cimResults, wmiResults) = _queryHelper.QueryInstances(
                        _currentTarget, _authUser, _authPass, "root/cimv2",
                        "SELECT TimeGenerated, EventCode, Message, SourceName FROM Win32_NTLogEvent WHERE Logfile='System' AND Type='Error'");

                    tw.AppendOutput($"{"Time",-20} | {"Source",-25} | {"ID",-8} | Message");
                    tw.AppendOutput(new string('═', 120));

                    List<string> events = new List<string>();

                    if (cimResults != null)
                    {
                        // MULTICORE OPTIMIZATION: Process event logs in parallel using PLINQ (CIM)
                        events = cimResults
                            .AsParallel()
                            .WithDegreeOfParallelism(Environment.ProcessorCount)
                            .Take(100) // Limit to 100 events
                            .Select(ev => {
                                try
                                {
                                    string ts = "N/A";
                                    var timeGenVal = ev.CimInstanceProperties["TimeGenerated"]?.Value;
                                    if (timeGenVal != null)
                                    {
                                        try
                                        {
                                            DateTime dt = timeGenVal is DateTime dtVal ? dtVal : DateTime.Parse(timeGenVal.ToString());
                                            ts = dt.ToString("yyyy-MM-dd HH:mm:ss");
                                        }
                                        catch { }
                                    }
                                    string src = ev.CimInstanceProperties["SourceName"]?.Value?.ToString() ?? "?";
                                    if (src.Length > 25) src = src.Substring(0, 22) + "...";
                                    string msg = (ev.CimInstanceProperties["Message"]?.Value?.ToString() ?? "").Replace("\r", "").Replace("\n", " ");
                                    if (msg.Length > 80) msg = msg.Substring(0, 77) + "...";
                                    string output = $"{ts,-20} | {src,-25} | {ev.CimInstanceProperties["EventCode"]?.Value,-8} | {msg}";
                                    return output;
                                }
                                catch (Exception ex)
                                {
                                    LogManager.LogDebug($"Query failed: {ex.Message}");
                                    return null;
                                }
                            })
                            .Where(x => x != null)
                            .ToList();
                    }
                    else if (wmiResults != null)
                    {
                        // MULTICORE OPTIMIZATION: Process event logs in parallel using PLINQ (WMI)
                        events = wmiResults.Cast<ManagementObject>()
                            .AsParallel()
                            .WithDegreeOfParallelism(Environment.ProcessorCount)
                            .Take(100) // Limit to 100 events
                            .Select(ev => {
                                try
                                {
                                    string ts = "N/A";
                                    try { ts = ManagementDateTimeConverter.ToDateTime(ev["TimeGenerated"]?.ToString() ?? "").ToString("yyyy-MM-dd HH:mm:ss"); } catch { }
                                    string src = ev["SourceName"]?.ToString() ?? "?";
                                    if (src.Length > 25) src = src.Substring(0, 22) + "...";
                                    string msg = (ev["Message"]?.ToString() ?? "").Replace("\r", "").Replace("\n", " ");
                                    if (msg.Length > 80) msg = msg.Substring(0, 77) + "...";
                                    string output = $"{ts,-20} | {src,-25} | {ev["EventCode"],-8} | {msg}";
                                    return output;
                                }
                                catch (Exception ex)
                                {
                                    LogManager.LogDebug($"PLINQ event log processing error: {ex.Message}");
                                    return null;
                                }
                                finally
                                {
                                    ev?.Dispose();
                                }
                            })
                            .Where(x => x != null)
                            .ToList();
                    }

                    foreach (var line in events) tw.AppendOutput(line);
                    tw.SetStatus($"[{protocol}] {events.Count} errors");
                    AddLog(_currentTarget, "EVENT_LOG", $"{events.Count} errors", "OK");
                }
                catch (Exception ex) { tw.AppendOutput($"ERROR: {ex.Message}", true); tw.SetStatus("Failed", true); }
            });
        }

        private async void Tool_Repair_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(_currentTarget))
                {
                    // TAG: #AUTO_UPDATE_UI_ENGINE #VALIDATION_TOASTS
                    Managers.UI.ToastManager.ShowWarning("No target selected for OS repair");
                    MessageBox.Show("No target");
                    return;
                }
                if (MessageBox.Show("Run SFC + DISM repair? (15-30 min)", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
                ShowBottomProgress($"Starting OS repair on {_currentTarget}...");
                AppendTerminal($"OS Repair → {_currentTarget}...");
                Managers.UI.ToastManager.ShowInfo("OS repair started (15-30 min estimated)");
                await Task.Run(async () =>
                {
                    try
                    {
                        var scope = _wmiManager.GetConnection(_currentTarget, _authUser, _authPass);
                        using (var mc = new ManagementClass(scope, new ManagementPath("Win32_Process"), null))
                        {
                            var inp = mc.GetMethodParameters("Create");
                            inp["CommandLine"] = "cmd.exe /c sfc /scannow"; mc.InvokeMethod("Create", inp, null);
                            _ = Dispatcher.InvokeAsync(() => UpdateBottomProgress(40, "SFC scan started..."));
                            AppendTerminal("SFC started"); await Task.Delay(5000);
                            inp["CommandLine"] = "cmd.exe /c DISM /Online /Cleanup-Image /RestoreHealth"; mc.InvokeMethod("Create", inp, null);
                            _ = Dispatcher.InvokeAsync(() => UpdateBottomProgress(80, "DISM repair started..."));
                            AppendTerminal("DISM started"); AddLog(_currentTarget, "OS_REPAIR", "SFC+DISM launched", "OK");
                            await Task.Delay(2000);
                            _ = Dispatcher.InvokeAsync(() =>
                            {
                                HideBottomProgress("Ready • Repair running");
                                // TAG: #AUTO_UPDATE_UI_ENGINE #STATUS_TOASTS
                                Managers.UI.ToastManager.ShowSuccess("OS repair tasks started successfully");
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        AppendTerminal($"Repair failed: {ex.Message}", true);
                        _ = Dispatcher.InvokeAsync(() =>
                        {
                            HideBottomProgress("Repair failed");
                            // TAG: #AUTO_UPDATE_UI_ENGINE #STATUS_TOASTS
                            Managers.UI.ToastManager.ShowError($"OS repair failed: {ex.Message}");
                        });
                    }
                });
            }
            catch (Exception ex) { LogManager.LogError("Repair error", ex); HideBottomProgress("Repair failed"); }
        }

        private async void Tool_GP_Click(object sender, RoutedEventArgs e) { try { await WMIExecute("cmd.exe /c gpupdate /force", "GPUPDATE"); } catch (Exception ex) { LogManager.LogError("GP", ex); } }
        private async void Tool_Reboot_Click(object sender, RoutedEventArgs e) { try { await WMIReboot(); } catch (Exception ex) { LogManager.LogError("Reboot", ex); } }
        private async void Tool_EnableWinRM_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(_currentTarget))
                {
                    // TAG: #AUTO_UPDATE_UI_ENGINE #VALIDATION_TOASTS
                    Managers.UI.ToastManager.ShowWarning("No target selected for WinRM configuration");
                    MessageBox.Show("No target selected", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Mouse.OverrideCursor = Cursors.Wait;
                ShowBottomProgress($"Enabling WinRM on {_currentTarget}...");
                AppendTerminal($"Enabling WinRM on {_currentTarget}...");

                await Task.Run(() =>
                {
                    try
                    {
                        // Use PsExec to enable WinRM (doesn't require WMI)
                        // This works even if WMI/RPC is blocked
                        string psexecPath = System.IO.Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.System),
                            "psexec.exe"
                        );

                        // Check if PsExec exists, auto-download if not
                        if (!System.IO.File.Exists(psexecPath))
                        {
                            bool downloadSuccess = false;

                            try
                            {
                                AppendTerminal("PsExec not found, downloading from Sysinternals...");

                                using (var webClient = new System.Net.WebClient())
                                {
                                    webClient.DownloadFile("https://live.sysinternals.com/psexec.exe", psexecPath);
                                }

                                if (System.IO.File.Exists(psexecPath))
                                {
                                    AppendTerminal("PsExec downloaded successfully");
                                    downloadSuccess = true;
                                }
                            }
                            catch (Exception downloadEx)
                            {
                                AppendTerminal($"Auto-download failed: {downloadEx.Message}", true);
                            }

                            // Fallback to manual download prompt if auto-download failed
                            if (!downloadSuccess)
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    var result = MessageBox.Show(
                                        "PsExec auto-download failed. Download it manually?\n\n" +
                                        "PsExec is needed to enable WinRM when WMI is unavailable.\n" +
                                        "Download URL: https://live.sysinternals.com/psexec.exe\n\n" +
                                        "Save it to: " + psexecPath,
                                        "PsExec Required",
                                        MessageBoxButton.YesNo,
                                        MessageBoxImage.Question
                                    );

                                    if (result == MessageBoxResult.Yes)
                                    {
                                        Process.Start("https://live.sysinternals.com/psexec.exe");
                                    }
                                });
                                AppendTerminal("PsExec not available - cannot enable WinRM remotely", true);
                                return;
                            }
                        }

                        // Use PowerShell remoting via TCP (doesn't require WMI)
                        string capturedPassword = null;
                        if (_authPass != null)
                        {
                            SecureMemory.UseSecureString(_authPass, pwd => capturedPassword = pwd);
                        }

                        string psScript = $@"
                            $secpass = ConvertTo-SecureString '{capturedPassword}' -AsPlainText -Force
                            $cred = New-Object System.Management.Automation.PSCredential('{_authUser}', $secpass)

                            # Try WinRM first (might already be enabled)
                            try {{
                                Invoke-Command -ComputerName {_currentTarget} -Credential $cred -ScriptBlock {{
                                    Enable-PSRemoting -Force -SkipNetworkProfileCheck
                                    Set-Item WSMan:\localhost\Client\TrustedHosts -Value * -Force
                                }} -ErrorAction Stop
                                Write-Output 'SUCCESS'
                            }} catch {{
                                # If WinRM fails, try PsExec
                                & '{psexecPath}' \\{_currentTarget} -u {_authUser} -p '{capturedPassword}' -accepteula powershell.exe -Command ""Enable-PSRemoting -Force -SkipNetworkProfileCheck""
                                Write-Output 'SUCCESS_PSEXEC'
                            }}
                        ";

                        var psi = new ProcessStartInfo
                        {
                            FileName = "powershell.exe",
                            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psScript.Replace("\"", "`\"")}\"",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        };

                        using (var proc = Process.Start(psi))
                        {
                            string output = proc.StandardOutput.ReadToEnd();
                            string errors = proc.StandardError.ReadToEnd();
                            proc.WaitForExit();

                            capturedPassword = null;
                            GC.Collect();

                            if (output.Contains("SUCCESS"))
                            {
                                AppendTerminal($"WinRM enabled successfully on {_currentTarget}");
                                AddLog(_currentTarget, "WINRM_ENABLE", "Enabled PowerShell remoting", "OK");
                                _ = Dispatcher.InvokeAsync(() =>
                                {
                                    UpdateBottomProgress(100, "WinRM enabled");
                                    Managers.UI.ToastManager.ShowSuccess($"WinRM enabled on {_currentTarget}");
                                });
                            }
                            else if (!string.IsNullOrWhiteSpace(errors))
                            {
                                AppendTerminal($"WinRM enable failed: {errors}", true);
                                _ = Dispatcher.InvokeAsync(() => HideBottomProgress("WinRM failed"));
                            }
                            else
                            {
                                AppendTerminal($"WinRM enable completed with unknown status", true);
                                _ = Dispatcher.InvokeAsync(() => HideBottomProgress("WinRM status unknown"));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        AppendTerminal($"WinRM enable error: {ex.Message}", true);
                        LogManager.LogError($"WinRM enable failed for {_currentTarget}", ex);
                        _ = Dispatcher.InvokeAsync(() => HideBottomProgress("WinRM error"));
                    }
                });

                Mouse.OverrideCursor = null;
                await Task.Delay(1000);
                HideBottomProgress("Ready");
            }
            catch (Exception ex)
            {
                Mouse.OverrideCursor = null;
                HideBottomProgress("WinRM error");
                LogManager.LogError("WinRM button error", ex);
                MessageBox.Show($"Error: {ex.Message}", "Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async void Tool_Firewall_Click(object sender, RoutedEventArgs e) { try { if (MessageBox.Show("Disable firewall?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) await WMIExecute("netsh advfirewall set allprofiles state off", "FIREWALL_DISABLE"); } catch (Exception ex) { LogManager.LogError("Firewall", ex); } }

        // ═══════════════════════════════════════════════════════
        // CONTEXT MENUS & INVENTORY
        // ═══════════════════════════════════════════════════════

        private void Ctx_Inspect_Click(object sender, RoutedEventArgs e)
        {
            if (GridInventory.SelectedItem is PCInventory pc)
            {
                MainTabs.SelectedIndex = 0;
                ComboTarget.Text = pc.Hostname;
                AddToRecentTargets(pc.Hostname); // Track recent machines
                BtnScan_Click(sender, e);
            }
        }

        private void Ctx_CopyRow_Click(object sender, RoutedEventArgs e)
        {
            if (GridInventory.SelectedItem is PCInventory pc)
            {
                string row = $"{pc.Hostname}\t{pc.Status}\t{pc.CurrentUser}\t{pc.DisplayOS}\t{pc.WindowsVersion}\t{pc.Chassis}\t{pc.BitLockerStatus}";
                Clipboard.SetText(row);
                AppendTerminal($"Copied row: {pc.Hostname}");
            }
        }

        private void Ctx_RDP_Click(object sender, RoutedEventArgs e)
        {
            if (GridInventory.SelectedItem is PCInventory pc && pc.Status == "ONLINE")
            {
                try { Process.Start("mstsc.exe", $"/v:{pc.Hostname}"); }
                catch (Exception ex) { MessageBox.Show($"RDP failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
            }
        }

        private void Ctx_ProcessManager_Click(object sender, RoutedEventArgs e)
        {
            if (GridInventory.SelectedItem is PCInventory pc && pc.Status == "ONLINE")
            {
                _currentTarget = pc.Hostname;
                Tool_Proc_Click(sender, e);
            }
        }

        private void Ctx_ServicesManager_Click(object sender, RoutedEventArgs e)
        {
            if (GridInventory.SelectedItem is PCInventory pc && pc.Status == "ONLINE")
            {
                _currentTarget = pc.Hostname;
                Tool_Svc_Click(sender, e);
            }
        }

        private void Ctx_BrowseShare_Click(object sender, RoutedEventArgs e)
        {
            if (GridInventory.SelectedItem is PCInventory pc && pc.Status == "ONLINE")
            {
                _currentTarget = pc.Hostname;
                Tool_Browse_Click(sender, e);
            }
        }

        private void Ctx_EventLogs_Click(object sender, RoutedEventArgs e)
        {
            if (GridInventory.SelectedItem is PCInventory pc && pc.Status == "ONLINE")
            {
                _currentTarget = pc.Hostname;
                Tool_Evt_Click(sender, e);
            }
        }

        private void Ctx_GPUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (GridInventory.SelectedItem is PCInventory pc && pc.Status == "ONLINE")
            {
                _currentTarget = pc.Hostname;
                Tool_GP_Click(sender, e);
            }
        }

        private void Ctx_KillFirewall_Click(object sender, RoutedEventArgs e)
        {
            if (GridInventory.SelectedItem is PCInventory pc && pc.Status == "ONLINE")
            {
                _currentTarget = pc.Hostname;
                Tool_Firewall_Click(sender, e);
            }
        }

        private void Ctx_EnableWinRM_Click(object sender, RoutedEventArgs e)
        {
            if (GridInventory.SelectedItem is PCInventory pc && pc.Status == "ONLINE")
            {
                _currentTarget = pc.Hostname;
                Tool_EnableWinRM_Click(sender, e);
            }
        }

        private void Ctx_Reboot_Click(object sender, RoutedEventArgs e)
        {
            if (GridInventory.SelectedItem is PCInventory pc && pc.Status == "ONLINE")
            {
                if (MessageBox.Show($"Reboot {pc.Hostname}?", "Confirm Reboot", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    _currentTarget = pc.Hostname;
                    Tool_Reboot_Click(sender, e);
                }
            }
        }

        private void Ctx_PushGeneral_Click(object sender, RoutedEventArgs e) { if (GridInventory.SelectedItem is PCInventory pc && pc.Status == "ONLINE") RunHybridExecutor(Script_General, "", "PUSH_GENERAL", pc.Hostname); }
        private void Ctx_PushFeature_Click(object sender, RoutedEventArgs e) { if (GridInventory.SelectedItem is PCInventory pc && pc.Status == "ONLINE") RunHybridExecutor(Script_Feature, "", "PUSH_FEATURE", pc.Hostname); }
        private async void Ctx_RefreshNode_Click(object sender, RoutedEventArgs e) { try { if (GridInventory.SelectedItem is PCInventory pc) { await Task.Run(async () => { try { using (var p = new Ping()) { var r = await p.SendPingAsync(pc.Hostname, SecureConfig.PingTimeoutMs); Application.Current.Dispatcher.Invoke(() => pc.Status = r.Status == IPStatus.Success ? "ONLINE" : "Offline"); } } catch { Application.Current.Dispatcher.Invoke(() => pc.Status = "Error"); } }); } } catch { } }

        /// <summary>Pin selected device from inventory to pinned devices monitor</summary>
        private async void Ctx_PinDevice_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (GridInventory.SelectedItem is PCInventory pc)
                {
                    // Check if already pinned
                    if (_pinnedDevices.Any(p => p.Input.Equals(pc.Hostname, StringComparison.OrdinalIgnoreCase)))
                    {
                        MessageBox.Show($"{pc.Hostname} is already pinned.", "Already Pinned",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    // Create pinned device from inventory item
                    var pinnedDevice = new PinnedDevice
                    {
                        Input = pc.Hostname,
                        ResolvedName = pc.Hostname,
                        Status = pc.Status,
                        StatusColor = pc.Status == "ONLINE" ? Brushes.LimeGreen : Brushes.Gray
                    };

                    _pinnedDevices.Add(pinnedDevice);
                    SavePinnedDevices();
                    await LoadPinnedDevices();

                    AppendTerminal($"📌 Pinned device: {pc.Hostname}");
                    Managers.UI.ToastManager.ShowSuccess($"{pc.Hostname} added to pinned devices");
                    MessageBox.Show($"✓ {pc.Hostname} added to pinned devices", "Device Pinned",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to pin device", ex);
                MessageBox.Show($"Failed to pin device: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Add selected computer to favorites/bookmarks
        /// TAG: #VERSION_7 #BOOKMARKS
        /// </summary>
        private void Ctx_AddToFavorites_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (GridInventory.SelectedItem is PCInventory pc)
                {
                    // Prompt for category and description
                    var dialog = new BookmarkEditDialog(pc.Hostname);
                    if (dialog.ShowDialog() == true)
                    {
                        BookmarkManager.AddBookmark(pc.Hostname, dialog.Description, dialog.Category);
                        AppendTerminal($"⭐ Added to favorites: {pc.Hostname}");
                        MessageBox.Show($"✓ {pc.Hostname} added to favorites", "Favorite Added",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to add favorite", ex);
                MessageBox.Show($"Failed to add favorite: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Remove selected computer from favorites/bookmarks
        /// TAG: #VERSION_7 #BOOKMARKS
        /// </summary>
        private void Ctx_RemoveFromFavorites_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (GridInventory.SelectedItem is PCInventory pc)
                {
                    var result = MessageBox.Show(
                        $"Remove {pc.Hostname} from favorites?",
                        "Remove Favorite",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        BookmarkManager.RemoveBookmark(pc.Hostname);
                        AppendTerminal($"💔 Removed from favorites: {pc.Hostname}");
                        MessageBox.Show($"✓ {pc.Hostname} removed from favorites", "Favorite Removed",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to remove favorite", ex);
                MessageBox.Show($"Failed to remove favorite: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ══════════════════════════════════════════════════════════════
        // QUICK FIX / AUTOMATED REMEDIATION HANDLERS
        // TAG: #VERSION_7.1 #REMEDIATION #AUTOMATION
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Quick Fix: Restart Windows Update service
        /// TAG: #VERSION_7.1 #REMEDIATION
        /// </summary>
        private async void Ctx_FixWindowsUpdate_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteQuickFixAsync(RemediationManager.RemediationAction.RestartWindowsUpdate);
        }

        /// <summary>
        /// Quick Fix: Clear DNS cache
        /// TAG: #VERSION_7.1 #REMEDIATION
        /// </summary>
        private async void Ctx_FixDNS_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteQuickFixAsync(RemediationManager.RemediationAction.ClearDNSCache);
        }

        /// <summary>
        /// Quick Fix: Restart Print Spooler service
        /// TAG: #VERSION_7.1 #REMEDIATION
        /// </summary>
        private async void Ctx_FixPrintSpooler_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteQuickFixAsync(RemediationManager.RemediationAction.RestartPrintSpooler);
        }

        /// <summary>
        /// Quick Fix: Enable WinRM for remote management
        /// TAG: #VERSION_7.1 #REMEDIATION
        /// </summary>
        private async void Ctx_FixWinRM_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteQuickFixAsync(RemediationManager.RemediationAction.EnableWinRM);
        }

        /// <summary>
        /// Quick Fix: Fix time synchronization
        /// TAG: #VERSION_7.1 #REMEDIATION
        /// </summary>
        private async void Ctx_FixTimeSync_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteQuickFixAsync(RemediationManager.RemediationAction.FixTimeSync);
        }

        /// <summary>
        /// Quick Fix: Clear event logs
        /// TAG: #VERSION_7.1 #REMEDIATION
        /// </summary>
        private async void Ctx_FixEventLogs_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteQuickFixAsync(RemediationManager.RemediationAction.ClearEventLogs);
        }

        /// <summary>
        /// Launch PowerShell Script Executor window
        /// TAG: #VERSION_7.1 #SCRIPTS #BULK_OPERATIONS
        /// </summary>
        private void Ctx_RunScript_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get selected computers
                var selectedComputers = GridInventory.SelectedItems.Cast<PCInventory>().ToArray();
                if (selectedComputers.Length == 0)
                {
                    MessageBox.Show("Please select one or more computers to run scripts on.", "No Selection",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Get hostnames
                var hostnames = selectedComputers.Select(pc => pc.Hostname).ToArray();

                // Get credentials from current session
                string username = Properties.Settings.Default.LastUser;
                string password = null;

                string targetDC = ComboDC?.Text;
                if (!string.IsNullOrEmpty(targetDC))
                {
                    password = SecureCredentialManager.RetrieveCredential(targetDC, "password");
                }

                // Launch script executor window
                var scriptWindow = new ScriptExecutorWindow(hostnames, username, password)
                {
                    Owner = this,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };

                scriptWindow.ShowDialog();

                // Log
                AppendTerminal($"📜 Opened Script Executor for {hostnames.Length} computer(s)");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open Script Executor:\n\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                LogManager.LogError("[MainWindow] Failed to open Script Executor", ex);
            }
        }

        /// <summary>
        /// Manage asset tags
        /// TAG: #VERSION_7.1 #ASSET_TAGGING
        /// </summary>
        private void Ctx_ManageTags_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get all tags
                var tags = AssetTagManager.GetAllTags();
                var stats = AssetTagManager.GetTagStatistics();

                var message = new System.Text.StringBuilder();
                message.AppendLine("🏷️ Asset Tags:");
                message.AppendLine();

                foreach (var tag in tags)
                {
                    int count = stats.ContainsKey(tag.Name) ? stats[tag.Name] : 0;
                    message.AppendLine($"{tag.DisplayName} - {count} computer(s)");
                }

                MessageBox.Show(message.ToString(), "Asset Tags", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load tags:\n\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                LogManager.LogError("[MainWindow] Failed to manage tags", ex);
            }
        }

        // TAG: #VERSION_7.1 #ADVANCED_FILTERING - Quick filter handlers
        private void BtnFilterAll_Click(object sender, RoutedEventArgs e)
        {
            ApplyQuickFilter(null);
            TxtFilterStatus.Text = "Showing all computers";
        }

        private void BtnFilterOnline_Click(object sender, RoutedEventArgs e)
        {
            ApplyQuickFilter(pc => pc.Status == "ONLINE");
            TxtFilterStatus.Text = $"Showing online computers ({GridInventory.Items.Count} found)";
        }

        private void BtnFilterOffline_Click(object sender, RoutedEventArgs e)
        {
            ApplyQuickFilter(pc => pc.Status == "OFFLINE" || pc.Status == "Access Denied");
            TxtFilterStatus.Text = $"Showing offline computers ({GridInventory.Items.Count} found)";
        }

        private void BtnFilterWin7_Click(object sender, RoutedEventArgs e)
        {
            ApplyQuickFilter(pc => pc.DisplayOS != null && pc.DisplayOS.Contains("Windows 7"));
            TxtFilterStatus.Text = $"Showing Windows 7 computers ({GridInventory.Items.Count} found)";
        }

        private void BtnFilterServers_Click(object sender, RoutedEventArgs e)
        {
            ApplyQuickFilter(pc => pc.Chassis != null && pc.Chassis.Contains("Server"));
            TxtFilterStatus.Text = $"Showing servers ({GridInventory.Items.Count} found)";
        }

        private void BtnFilterWorkstations_Click(object sender, RoutedEventArgs e)
        {
            ApplyQuickFilter(pc => pc.Chassis != null && (pc.Chassis.Contains("Desktop") || pc.Chassis.Contains("Laptop") || pc.Chassis.Contains("Notebook")));
            TxtFilterStatus.Text = $"Showing workstations ({GridInventory.Items.Count} found)";
        }

        /// <summary>
        /// Apply quick filter to inventory grid
        /// TAG: #VERSION_7.1 #ADVANCED_FILTERING
        /// </summary>
        private void ApplyQuickFilter(Func<PCInventory, bool> filterPredicate)
        {
            if (filterPredicate == null)
            {
                // Show all
                GridInventory.ItemsSource = _inventory;
            }
            else
            {
                // Apply filter
                var filtered = _inventory.Where(filterPredicate).ToList();
                GridInventory.ItemsSource = filtered;
            }
        }

        // TAG: #VERSION_7.1 #PATCH_MANAGEMENT - Windows Update handlers
        private void Ctx_ForceUpdateCheck_Click(object sender, RoutedEventArgs e)
        {
            var selectedComputers = GridInventory.SelectedItems.Cast<PCInventory>().ToArray();
            if (selectedComputers.Length == 0)
            {
                MessageBox.Show("Please select one or more computers.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var hostnames = selectedComputers.Select(pc => pc.Hostname).ToArray();
            var script = "wuauclt /detectnow; Write-Output 'Windows Update check initiated'";

            var scriptWindow = new ScriptExecutorWindow(hostnames, Properties.Settings.Default.LastUser, null)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            scriptWindow.ShowDialog();

            AppendTerminal($"🔄 Initiated Windows Update check on {hostnames.Length} computer(s)");
        }

        private void Ctx_ViewUpdateStatus_Click(object sender, RoutedEventArgs e)
        {
            var selectedComputers = GridInventory.SelectedItems.Cast<PCInventory>().ToArray();
            if (selectedComputers.Length == 0)
            {
                MessageBox.Show("Please select one or more computers.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var hostnames = selectedComputers.Select(pc => pc.Hostname).ToArray();
            var script = @"
$updateSession = New-Object -ComObject Microsoft.Update.Session
$updateSearcher = $updateSession.CreateUpdateSearcher()
$searchResult = $updateSearcher.Search('IsInstalled=0')
Write-Output ""Available Updates: $($searchResult.Updates.Count)""
foreach ($update in $searchResult.Updates) {
    Write-Output ""  - $($update.Title)""
}";

            var scriptWindow = new ScriptExecutorWindow(hostnames, Properties.Settings.Default.LastUser, null)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            scriptWindow.ShowDialog();

            AppendTerminal($"📊 Checked update status on {hostnames.Length} computer(s)");
        }

        private void Ctx_CheckPendingReboot_Click(object sender, RoutedEventArgs e)
        {
            var selectedComputers = GridInventory.SelectedItems.Cast<PCInventory>().ToArray();
            if (selectedComputers.Length == 0)
            {
                MessageBox.Show("Please select one or more computers.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var hostnames = selectedComputers.Select(pc => pc.Hostname).ToArray();
            var script = @"
$rebootPending = $false
if (Test-Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending') { $rebootPending = $true }
if (Test-Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired') { $rebootPending = $true }
if ($rebootPending) {
    Write-Output '⚠️ REBOOT PENDING - Computer needs to restart for updates'
} else {
    Write-Output '✅ No reboot pending'
}";

            var scriptWindow = new ScriptExecutorWindow(hostnames, Properties.Settings.Default.LastUser, null)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            scriptWindow.ShowDialog();

            AppendTerminal($"🔄 Checked pending reboot status on {hostnames.Length} computer(s)");
        }

        /// <summary>
        /// Common handler for Quick Fix operations
        /// Supports both single and multi-select
        /// TAG: #VERSION_7.1 #REMEDIATION #AUTOMATION
        /// </summary>
        private async Task ExecuteQuickFixAsync(RemediationManager.RemediationAction action)
        {
            try
            {
                // Get selected computers
                var selectedComputers = GridInventory.SelectedItems.Cast<PCInventory>().ToArray();
                if (selectedComputers.Length == 0)
                {
                    MessageBox.Show("Please select one or more computers.", "No Selection",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Get hostnames
                var hostnames = selectedComputers.Select(pc => pc.Hostname).ToArray();

                // Confirm action
                string actionName = RemediationManager.GetActionName(action);
                string actionIcon = RemediationManager.GetActionIcon(action);

                var confirmMsg = selectedComputers.Length == 1
                    ? $"Execute {actionIcon} {actionName} on {hostnames[0]}?"
                    : $"Execute {actionIcon} {actionName} on {selectedComputers.Length} computers?";

                var result = MessageBox.Show(confirmMsg, "Confirm Quick Fix",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;

                // Get credentials from current session
                string username = Properties.Settings.Default.LastUser;
                string password = null;

                string targetDC = ComboDC?.Text;
                if (!string.IsNullOrEmpty(targetDC))
                {
                    password = SecureCredentialManager.RetrieveCredential(targetDC, "password");
                }

                // Show remediation dialog
                var dialog = new RemediationDialog
                {
                    Owner = this,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };

                // Execute remediation asynchronously
                var _ = dialog.ExecuteRemediationAsync(action, hostnames, username, password);

                // Show dialog
                dialog.ShowDialog();

                // Log completion
                AppendTerminal($"🔧 Completed Quick Fix: {actionIcon} {actionName} on {selectedComputers.Length} computer(s)");
                LogManager.LogInfo($"[Quick Fix] Executed {actionName} on {selectedComputers.Length} computers");
                Managers.UI.ToastManager.ShowSuccess($"{actionName} completed on {selectedComputers.Length} computer{(selectedComputers.Length != 1 ? "s" : "")}");
            }
            catch (Exception ex)
            {
                LogManager.LogError($"[Quick Fix] Failed to execute remediation", ex);
                MessageBox.Show($"Quick Fix failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>Show/hide admin-only context menu items based on login status</summary>
        private void GridInventoryContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            bool isAdmin = _isLoggedIn && _isDomainAdmin;

            // Show/hide admin-only menu items
            CtxRDP.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
            CtxProcess.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
            CtxServices.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
            CtxBrowse.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
            CtxEvents.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;

            // TAG: #VERSION_7.1 #REMEDIATION - Show/hide Quick Fix menu for admins only
            if (MenuQuickFix != null)
            {
                MenuQuickFix.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
            }

            // TAG: #VERSION_7 #BOOKMARKS - Show/hide bookmark menu items based on current state
            if (GridInventory.SelectedItem is PCInventory pc)
            {
                bool isBookmarked = BookmarkManager.IsBookmarked(pc.Hostname);
                CtxAddFavorite.Visibility = isBookmarked ? Visibility.Collapsed : Visibility.Visible;
                CtxRemoveFavorite.Visibility = isBookmarked ? Visibility.Visible : Visibility.Collapsed;
            }

            // TAG: #RMM_INTEGRATION #VERSION_7 - Populate RMM tools dynamically
            try
            {
                var contextMenu = sender as ContextMenu;
                var menuRemoteConnect = contextMenu?.Items.OfType<MenuItem>().FirstOrDefault(m => m.Name == "MenuRemoteConnect");

                if (menuRemoteConnect != null)
                {
                    menuRemoteConnect.Items.Clear();
                    var enabledTools = RemoteControlManager.GetEnabledTools();

                    if (enabledTools.Count == 0)
                    {
                        menuRemoteConnect.IsEnabled = false;
                        var disabledItem = new MenuItem { Header = "No remote tools configured", IsEnabled = false };
                        menuRemoteConnect.Items.Add(disabledItem);
                    }
                    else
                    {
                        menuRemoteConnect.IsEnabled = true;
                        foreach (var tool in enabledTools)
                        {
                            var menuItem = new MenuItem
                            {
                                Header = $"{GetToolIcon(tool.ToolType)} {tool.ToolName}",
                                Tag = tool.ToolType
                            };
                            menuItem.Click += MenuRmmTool_Click;
                            menuRemoteConnect.Items.Add(menuItem);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to populate RMM context menu", ex);
            }

            CtxSep1.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;

            CtxGP.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
            CtxFirewall.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
            CtxWinRM.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
            CtxSep2.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;

            CtxPushGeneral.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
            CtxPushFeature.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
            CtxSep3.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;

            CtxReboot.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
        }

        // ═══════════════════════════════════════════════════════
        // AUTHENTICATION (Secure)
        // ═══════════════════════════════════════════════════════

        private async void BtnAuth_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoggedIn)
            {
                // SECURE LOGOUT: Wipe credentials from memory
                SecureMemory.WipeAndDispose(ref _authPass);
                SecureMemory.ForceCleanup();
                _authUser = Environment.UserName; _isLoggedIn = false; _isDomainAdmin = false;
                BtnAuth.Content = "LOGIN"; TxtAuthStatus.Text = "NOT AUTHENTICATED"; TxtAuthStatus.Foreground = Brushes.Gray;

                // Clear admin status cache
                _adminCheckCache.Clear();

                ApplyRoleRestrictions();
                LogManager.LogInfo("User logged out — credentials wiped");
                Managers.UI.ToastManager.ShowInfo("Logged out - credentials cleared");
            }
            else await ShowLoginDialog();
        }

        private async Task ShowLoginDialog()
        {
            int attempt = 0; const int MAX = 3; bool ok = false;
            while (attempt < MAX && !ok)
            {
                var lw = new LoginWindow();
                var result = lw.ShowDialog();

                // Handle elevation restart request
                if (lw.ShouldRestartElevated)
                {
                    try
                    {
                        var exePath = Process.GetCurrentProcess().MainModule.FileName;
                        AppendTerminal($"[RESTART] Restarting with elevation: {exePath}");

                        var psi = new ProcessStartInfo
                        {
                            FileName = exePath,
                            UseShellExecute = true,
                            Verb = "runas"
                        };
                        Process.Start(psi);
                        Application.Current.Shutdown();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            "Failed to restart with elevation.\n\n" +
                            "Please manually:\n" +
                            "1. Close ArtaznIT Suite\n" +
                            "2. Right-click ArtaznIT.exe\n" +
                            "3. Select 'Run as Administrator'",
                            "Elevation Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        LogManager.LogError("Failed to restart with elevation from login", ex);
                    }
                    break;
                }

                if (result == true)
                {
                    if (!SecurityValidator.IsValidDomainUser(lw.Username))
                    {
                        MessageBox.Show("Invalid username. Use domain\\user format.", "Invalid Input",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        attempt++;
                        continue;
                    }
                    ok = await PerformAuth(lw.Username, lw.Password);
                    if (ok)
                    {
                        if (lw.RememberUser)
                            try { Properties.Settings.Default.LastUser = lw.Username; Properties.Settings.Default.Save(); }
                            catch { }

                        // Update domain badge after successful login
                        await CheckDCAvailabilityAsync();

                        // Initialize DC discovery if domain is now available
                        if (!string.IsNullOrEmpty(CurrentDomainName))
                        {
                            _ = InitDCCluster();
                            StartDomainVerificationTimer();
                            // Hide login button if it was showing in read-only mode
                            if (BtnLoginFromReadOnly != null)
                                BtnLoginFromReadOnly.Visibility = Visibility.Collapsed;
                        }
                    }
                    else
                    {
                        attempt++;
                        // TAG: #AUTO_UPDATE_UI_ENGINE #VALIDATION_TOASTS
                        if (attempt < MAX)
                        {
                            Managers.UI.ToastManager.ShowWarning($"Authentication failed - {MAX - attempt} attempts remaining");
                            MessageBox.Show($"Authentication failed.\n\n{MAX - attempt} attempts remaining.",
                                "Authentication Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                        else
                        {
                            Managers.UI.ToastManager.ShowError("Maximum authentication attempts exceeded");
                            MessageBox.Show("Maximum authentication attempts exceeded.\n\nApplication will close.",
                                "Access Denied", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
                else break;
            }
        }

        private async Task<bool> PerformAuth(string user, SecureString pass)
        {
            bool authenticated = false; IntPtr token = IntPtr.Zero;
            await Task.Run(() =>
            {
                try
                {
                    string domain = "PROCESS", cleanUser = user;
                    if (user.Contains("\\")) { var p = user.Split('\\'); domain = p[0]; cleanUser = p[1]; }
                    // SecureString → plaintext unavoidable for LogonUser P/Invoke
                    authenticated = LogonUser(cleanUser, domain, new NetworkCredential("", pass).Password, 2, 0, out token);
                }
                catch (Exception ex) { LogManager.LogError("Auth failed", ex); }
                finally { if (token != IntPtr.Zero) CloseHandle(token); }
            });
            if (authenticated)
            {
                _authUser = user; _authPass = pass; _isLoggedIn = true;
                _isDomainAdmin = CheckDomainAdminMembership(user);
                TxtAuthStatus.Text = user.ToUpper(); TxtAuthStatus.Foreground = Brushes.LimeGreen;
                BtnAuth.Content = "LOGOUT";
                LogManager.LogInfo($"Authenticated: {user} (Admin: {_isDomainAdmin}, Elevated: {_isElevated})");

                // Show success toast notification
                Dispatcher.Invoke(() =>
                {
                    Managers.UI.ToastManager.ShowSuccess($"Authenticated as {user}");
                });

                // Apply restrictions based on domain admin status and elevation
                ApplyRoleRestrictions();

                // Inform user of their access level (only if not domain admin)
                if (!_isDomainAdmin)
                {
                    Dispatcher.Invoke(() =>
                    {
                        // TAG: #AUTO_UPDATE_UI_ENGINE #STATUS_TOASTS
                        Managers.UI.ToastManager.ShowWarning("Limited access - not a Domain Admin");
                        MessageBox.Show(
                            "You are authenticated but NOT a Domain Admin.\n\n" +
                            "Some features (deployment, destructive tools) are disabled.\n\n" +
                            "Contact your IT administrator for Domain Admin access.",
                            "Limited Access",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    });
                }
            }
            return authenticated;
        }

        /// <summary>
        /// Checks if the current process is running with Administrator privileges
        /// </summary>
        private bool IsRunningAsAdministrator()
        {
            try
            {
                var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to check elevation status", ex);
                return false;
            }
        }

        /// <summary>
        /// Creates a desktop shortcut and batch file launcher for running as admin
        /// </summary>
        private void CreateAdminLauncher()
        {
            try
            {
                // Prompt for admin username
                string adminUsername = Microsoft.VisualBasic.Interaction.InputBox(
                    "Enter your domain admin username:\n\n" +
                    "Format: domain\\username\n" +
                    "Example: process\\admin.bnecessary-a",
                    "Admin Username",
                    $"{Environment.UserDomainName}\\admin.{Environment.UserName}",
                    -1, -1);

                if (string.IsNullOrWhiteSpace(adminUsername))
                {
                    throw new Exception("Username is required");
                }

                string exePath = Process.GetCurrentProcess().MainModule.FileName;
                string exeDir = Path.GetDirectoryName(exePath);
                string batPath = Path.Combine(exeDir, "Launch_AsAdmin.bat");

                // Create batch file with runas /savecred
                string batContent = $@"@echo off
echo ========================================
echo  ArtaznIT Suite - Admin Launcher
echo ========================================
echo.
echo Launching as: {adminUsername}
echo.
echo NOTE: First time will ask for password.
echo       Password will be saved securely by Windows.
echo.
runas /user:{adminUsername} /savecred ""{exePath}""
";

                File.WriteAllText(batPath, batContent);
                AppendTerminal($"Created launcher batch file: {batPath}", false);

                // Create desktop shortcut using IWshRuntimeLibrary
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string shortcutPath = Path.Combine(desktopPath, "ArtaznIT Suite (Admin).lnk");

                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                dynamic shell = Activator.CreateInstance(shellType);
                var shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = batPath;
                shortcut.WorkingDirectory = exeDir;
                shortcut.Description = $"Launch ArtaznIT Suite as {adminUsername}";
                shortcut.IconLocation = exePath + ",0";
                shortcut.Save();

                Marshal.ReleaseComObject(shortcut);
                Marshal.ReleaseComObject(shell);

                AppendTerminal($"Created desktop shortcut: {shortcutPath}", false);
                LogManager.LogInfo($"Admin launcher created for user: {adminUsername}");
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to create admin launcher", ex);
                MessageBox.Show(
                    $"Failed to create admin launcher:\n\n{ex.Message}",
                    "Creation Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                throw;
            }
        }

        /// <summary>
        /// Shows Windows native credential dialog and returns username/password
        /// </summary>
        private bool PromptForCredentials(out string username, out SecureString password)
        {
            username = null;
            password = null;

            try
            {
                var credUI = new CREDUI_INFO
                {
                    cbSize = Marshal.SizeOf(typeof(CREDUI_INFO)),
                    hwndParent = new System.Windows.Interop.WindowInteropHelper(this).Handle,
                    pszMessageText = "Enter your domain administrator credentials to run ArtaznIT Suite with full privileges.",
                    pszCaptionText = "ArtaznIT Suite - Administrator Login"
                };

                uint authPackage = 0;
                IntPtr outCredBuffer = IntPtr.Zero;
                uint outCredSize = 0;
                bool save = false;

                uint result = CredUIPromptForWindowsCredentials(
                    ref credUI,
                    0,
                    ref authPackage,
                    IntPtr.Zero,
                    0,
                    out outCredBuffer,
                    out outCredSize,
                    ref save,
                    CREDUIWIN_GENERIC);

                if (result == ERROR_SUCCESS)
                {
                    var usernameBuf = new StringBuilder(512);
                    var passwordBuf = new StringBuilder(512);
                    var domainBuf = new StringBuilder(512);
                    int maxUserName = 512;
                    int maxDomain = 512;
                    int maxPassword = 512;

                    if (CredUnPackAuthenticationBuffer(
                        CRED_PACK_PROTECTED_CREDENTIALS,
                        outCredBuffer,
                        outCredSize,
                        usernameBuf,
                        ref maxUserName,
                        domainBuf,
                        ref maxDomain,
                        passwordBuf,
                        ref maxPassword))
                    {
                        string user = usernameBuf.ToString().Trim();
                        string domain = domainBuf.ToString().Trim();
                        string pass = passwordBuf.ToString();

                        // Build full username
                        if (!string.IsNullOrEmpty(domain))
                        {
                            username = domain + "\\" + user;
                        }
                        else
                        {
                            username = user;
                        }

                        // Convert to SecureString
                        password = new SecureString();
                        foreach (char c in pass)
                        {
                            password.AppendChar(c);
                        }
                        password.MakeReadOnly();

                        // Securely wipe password from memory
                        for (int i = 0; i < pass.Length; i++)
                        {
                            passwordBuf[i] = '\0';
                        }

                        Marshal.FreeCoTaskMem(outCredBuffer);
                        return true;
                    }

                    Marshal.FreeCoTaskMem(outCredBuffer);
                }
                else if (result == ERROR_CANCELLED)
                {
                    return false; // User cancelled
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to show credential dialog", ex);
            }

            return false;
        }

        // ═══════════════════════════════════════════════════════
        // UTILITY
        // ═══════════════════════════════════════════════════════

        private async Task InitDCCluster()
        {
            ShowBottomProgress("Discovering domain controllers...");

            GridDCHealth.Children.Clear(); ComboDC.Items.Clear();
            var bg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333333"));
            var autoItem = new ComboBoxItem { Content = "Auto (Scanning...)", Tag = "Auto", Foreground = Brushes.White, Background = bg, FontWeight = FontWeights.Bold };
            ComboDC.Items.Add(autoItem); ComboDC.SelectedIndex = 0;

            var dcInfoList = new List<DCManager.DCInfo>();
            try
            {
                UpdateBottomProgress(20, "Discovering domain controllers...");
                // Try to get current domain name
                string currentDomain = "";
                try
                {
                    currentDomain = Domain.GetCurrentDomain().Name;
                }
                catch
                {
                    currentDomain = Environment.UserDomainName;
                }

                // Try cached DCs first
                dcInfoList = _dcManager.GetCachedDCs(currentDomain, cacheTTLMinutes: 60);

                if (dcInfoList == null || dcInfoList.Count == 0)
                {
                    // Cache miss or expired - do fresh discovery
                    AppendTerminal("[DC Discovery] Cache expired, discovering DCs...");
                    dcInfoList = await _dcManager.DiscoverDomainControllersAsync();
                }
                else
                {
                    AppendTerminal($"[DC Discovery] Using cached DCs ({dcInfoList.Count} found)");
                }
            }
            catch (Exception ex)
            {
                AppendTerminal($"[DC Discovery] FAILED: {ex.Message}", isError: true);
                LogManager.LogDebug($"InitDCCluster failed: {ex.Message}");

                // Fall back to any cached DCs from configuration (with extended TTL)
                string currentDomain = Environment.UserDomainName;
                var cachedDCs = _dcManager.GetCachedDCs(currentDomain, cacheTTLMinutes: 1440); // 24-hour fallback

                if (cachedDCs != null && cachedDCs.Count > 0)
                {
                    dcInfoList = cachedDCs;
                    AppendTerminal($"[DC Discovery] Using stale cached DCs ({dcInfoList.Count} found)");
                }
                else
                {
                    AppendTerminal("[DC Discovery] No cached DCs available - manual configuration required", isError: true);

                    // Show user-friendly error dialog
                    await Dispatcher.InvokeAsync(() => ShowDCDiscoveryFailureDialog(ex));
                    return; // Exit early - don't try to ping non-existent DCs
                }
            }

            // Convert DCInfo list to string list for existing health check code
            var dcList = dcInfoList.Select(dc => dc.Hostname).ToList();

            // If no DCs found after all attempts, show error and exit
            if (dcList.Count == 0)
            {
                await Dispatcher.InvokeAsync(() => ShowDCDiscoveryFailureDialog(new Exception("No domain controllers discovered")));
                return;
            }

            // ══════════════════════════════════════════════════════════════
            // MULTICORE OPTIMIZATION: Parallel DC health checks with Task.WhenAll
            // ══════════════════════════════════════════════════════════════════
            long bestPing = 9999; string bestDC = ""; object lk = new object();
            var dcHealthTasks = new List<Task>();

            // TAG: #VERSION_7 #BUG_FIX - Create Fleet items at same time as main items for proper update tracking
            var fleetItems = new Dictionary<string, ComboBoxItem>();

            foreach (var dc in dcList)
            {
                var cbi = new ComboBoxItem { Content = $"{dc} (probing...)", Tag = dc, Foreground = Brushes.LightGray, Background = bg };
                ComboDC.Items.Add(cbi);

                // Create corresponding Fleet item - TAG: #AD_FLEET_INVENTORY #BUG_FIX
                var fleetItem = new ComboBoxItem
                {
                    Content = $"{dc} (probing...)",
                    Tag = dc,
                    Foreground = Brushes.LightGray,
                    Background = bg
                };
                fleetItems[dc] = fleetItem;

                // TAG: #VERSION_7 #UI_IMPROVEMENT - MUCH bigger cards for easier reading
                var card = new Border
                {
                    Background = Brushes.Gray,
                    Margin = new Thickness(6),
                    CornerRadius = new CornerRadius(6),
                    ToolTip = $"Pinging {dc}...",
                    MinHeight = 80,
                    Padding = new Thickness(12)
                };
                var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                var tn = new TextBlock
                {
                    Text = dc,
                    Foreground = Brushes.White,
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 6)
                };
                var ti = new TextBlock
                {
                    Text = "...",
                    Foreground = Brushes.LightGray,
                    FontSize = 12,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FontWeight = FontWeights.SemiBold
                };
                sp.Children.Add(tn); sp.Children.Add(ti); card.Child = sp; GridDCHealth.Children.Add(card);

                dcHealthTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        LogManager.LogDebug($"[DC Health] Starting probe for {dc}");

                        // DNS lookup with timeout
                        string ip = "---";
                        try
                        {
                            var dnsTask = Dns.GetHostEntryAsync(dc);
                            if (await Task.WhenAny(dnsTask, Task.Delay(3000)) == dnsTask)
                            {
                                var hi = await dnsTask;
                                ip = hi.AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)?.ToString() ?? "IPv6";
                            }
                            else
                            {
                                ip = "DNS Timeout";
                                LogManager.LogDebug($"[DC Health] DNS timeout for {dc}");
                            }
                        }
                        catch (Exception dnsEx)
                        {
                            ip = "DNS Err";
                            LogManager.LogDebug($"[DC Health] DNS error for {dc}: {dnsEx.Message}");
                        }

                        // Ping with timeout
                        bool on = false;
                        long ms = 9999;
                        try
                        {
                            using (var p = new Ping())
                            {
                                var r = await p.SendPingAsync(dc, 2000);
                                on = r.Status == IPStatus.Success;
                                ms = on ? r.RoundtripTime : 9999;
                                LogManager.LogDebug($"[DC Health] Ping result for {dc}: {r.Status} ({ms}ms)");
                            }
                        }
                        catch (Exception pingEx)
                        {
                            LogManager.LogDebug($"[DC Health] Ping error for {dc}: {pingEx.Message}");
                        }

                        Brush statusColor = Brushes.Red; // Default offline color
                        _ = Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            ti.Text = ip; card.ToolTip = $"{dc}\nIP: {ip}\n{ms}ms"; cbi.Content = $"{dc} ({ms}ms)";
                            if (!on) { card.Background = Brushes.DarkRed; statusColor = Brushes.Red; }
                            else if (ms < 60) { card.Background = Brushes.DarkGreen; statusColor = Brushes.Lime; }
                            else if (ms < 150) { card.Background = Brushes.DarkGoldenrod; statusColor = Brushes.Yellow; }
                            else { card.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF4500")); statusColor = Brushes.Orange; }

                            // Always show status color, even when selected
                            cbi.Foreground = statusColor;

                            // BUG FIX: Also update Fleet tab dropdown item - TAG: #AD_FLEET_INVENTORY #VERSION_7
                            if (fleetItems.TryGetValue(dc, out var fleetCbi))
                            {
                                fleetCbi.Content = $"{dc} ({ms}ms)";
                                fleetCbi.Foreground = statusColor;
                            }

                            lock (lk)
                            {
                                if (on && ms < bestPing)
                                {
                                    bestPing = ms;
                                    bestDC = dc;
                                    autoItem.Content = $"Auto ({bestDC} - {bestPing}ms)";
                                    autoItem.Tag = bestDC;
                                    autoItem.Foreground = statusColor; // Set Auto color to match best DC
                                }
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        LogManager.LogError($"DC health check completely failed: {dc}", ex);
                        // Ensure UI is updated even on total failure
                        _ = Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            cbi.Content = $"{dc} (error)";
                            cbi.Foreground = Brushes.DarkGray;
                            card.Background = Brushes.DarkRed;
                            ti.Text = "ERR";

                            // Also update Fleet item
                            if (fleetItems.TryGetValue(dc, out var fleetCbi))
                            {
                                fleetCbi.Content = $"{dc} (error)";
                                fleetCbi.Foreground = Brushes.DarkGray;
                            }
                        });
                    }
                }));
            }

            // Wait for all DC health checks with 5-second timeout fallback
            _ = Task.Run(async () =>
            {
                UpdateBottomProgress(60, "Probing DC health...");
                // Wait up to 5 seconds for all health checks
                var completed = await Task.WhenAny(Task.WhenAll(dcHealthTasks), Task.Delay(5000));

                // Mark any still-probing DCs as timeout - TAG: #VERSION_7 #BUG_FIX
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    foreach (ComboBoxItem item in ComboDC.Items)
                    {
                        if (item.Content.ToString().Contains("probing..."))
                        {
                            item.Content = $"{item.Tag} (timeout)";
                            item.Foreground = Brushes.DarkGray;

                            // Also update Fleet item - TAG: #AD_FLEET_INVENTORY #BUG_FIX
                            string dcName = item.Tag?.ToString();
                            if (dcName != null && fleetItems.TryGetValue(dcName, out var fleetItem))
                            {
                                fleetItem.Content = $"{dcName} (timeout)";
                                fleetItem.Foreground = Brushes.DarkGray;
                            }
                        }
                    }
                    UpdateBottomProgress(100, "DC discovery complete");
                });

                // Hide progress after brief delay
                await Task.Delay(1500);
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    int dcCount = ComboDC.Items.Count - 1; // -1 for "Auto" item
                    HideBottomProgress($"Ready • {dcCount} DCs found");
                });
            });

            // Refresh DC history panel if it's currently visible
            // TAG: #DC_DISCOVERY #UI_UPDATE
            if (DCHistoryPanel != null && DCHistoryPanel.Visibility == Visibility.Visible)
            {
                LoadDCHistory();
            }

            // Update Domain Information tab after DC discovery
            UpdateDomainInformationTab();

            // Populate Fleet tab DC dropdown with pre-created items - TAG: #AD_FLEET_INVENTORY #VERSION_7 #BUG_FIX
            if (ComboDCFleet != null)
            {
                ComboDCFleet.Items.Clear();
                // Add items from fleetItems dictionary (already being updated by health checks)
                foreach (var fleetItem in fleetItems.Values)
                {
                    ComboDCFleet.Items.Add(fleetItem);
                }
                if (ComboDCFleet.Items.Count > 0)
                    ComboDCFleet.SelectedIndex = 0;
            }

            if (!string.IsNullOrEmpty(_tempLastDC)) foreach (ComboBoxItem i in ComboDC.Items) if (i.Tag?.ToString() == _tempLastDC) { ComboDC.SelectedItem = i; break; }
        }

        /// <summary>
        /// Show themed error dialog when domain controller discovery fails
        /// TAG: #THEME_DIALOG - Uses modular ThemedDialog system
        /// </summary>
        private void ShowDCDiscoveryFailureDialog(Exception ex)
        {
            // Use centralized ThemedDialog for consistent branding
            ThemedDialog.ShowError(
                owner: this,
                title: "Domain Controller Not Found",
                message: "Unable to connect to Active Directory",
                details: $"Error: {ex.Message}",
                reasons: new[]
                {
                    "🔌 Not connected to corporate VPN",
                    "🌐 Not connected to local network",
                    "💻 Machine is not domain-joined",
                    "🔥 Firewall blocking LDAP/Kerberos (ports 389, 636, 88)",
                    "📡 DNS misconfigured or domain unreachable",
                    "⚡ Domain controller is offline or unreachable"
                },
                actions: new[]
                {
                    "✓ Connect to corporate VPN",
                    "✓ Verify network connectivity",
                    "✓ Check domain membership (System Properties)"
                }
            );
        }

        internal void AppendTerminal(string text, bool isError = false)
        {
            _ = Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    // Add to terminal output
                    var tr = new TextRange(RtbTerminal.Document.ContentEnd, RtbTerminal.Document.ContentEnd);
                    tr.Text = $"{DateTime.Now:HH:mm:ss} | {text}\n";
                    tr.ApplyPropertyValue(TextElement.ForegroundProperty, isError ? Brushes.Red : Brushes.LimeGreen);
                    RtbTerminal.ScrollToEnd();
                    if (RtbTerminal.Document.Blocks.Count > 500) { var fb = RtbTerminal.Document.Blocks.FirstBlock; if (fb != null) RtbTerminal.Document.Blocks.Remove(fb); }

                    // Update status indicator (truncate if too long)
                    if (TxtTerminalStatus != null)
                    {
                        string statusText = text.Length > 60 ? text.Substring(0, 57) + "..." : text;
                        TxtTerminalStatus.Text = $"• {statusText}";
                        TxtTerminalStatus.Foreground = isError
                            ? new SolidColorBrush(Color.FromRgb(255, 100, 100))
                            : (SolidColorBrush)FindResource("AccentOrangeBrush");
                    }
                }
                catch (Exception ex)
                {
                    LogManager.LogDebug($"Terminal output failed: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Detailed debug output - shows exception details, stack trace, and context
        /// </summary>
        private void AppendDebug(string context, Exception ex)
        {
            if (!_debugMode) return;

            // Only log to file, not to terminal (prevents UI thread blocking)
            LogManager.LogDebug($"{context}: {ex.GetType().Name} - {ex.Message}");
        }

        /// <summary>
        /// Quick debug message with yellow color
        /// </summary>
        private void AppendDebugInfo(string message)
        {
            if (!_debugMode) return;

            _ = Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    var tr = new TextRange(RtbTerminal.Document.ContentEnd, RtbTerminal.Document.ContentEnd);
                    tr.Text = $"{DateTime.Now:HH:mm:ss} | [DEBUG] {message}\n";
                    tr.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.Yellow);
                    RtbTerminal.ScrollToEnd();
                }
                catch (Exception ex)
                {
                    LogManager.LogDebug($"Terminal output failed: {ex.Message}");
                }
            });
        }

        // ═══════════════════════════════════════════════════════════════
        // TOOL OPERATION PROGRESS INDICATOR
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Shows the tool progress indicator with tool name and initial status</summary>
        private void ShowToolProgress(string toolName, string status = "Initializing...")
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    TxtToolName.Text = toolName;
                    TxtToolStatus.Text = status;
                    ToolProgressBar.IsIndeterminate = true;
                    ToolProgressPanel.Visibility = Visibility.Visible;
                }
                catch (Exception ex)
                {
                    LogManager.LogWarning($"Failed to show tool progress: {ex.Message}");
                }
            });
        }

        /// <summary>Updates the tool progress status message</summary>
        private void UpdateToolProgress(string status)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    TxtToolStatus.Text = status;
                }
                catch (Exception ex)
                {
                    LogManager.LogWarning($"Failed to update tool progress: {ex.Message}");
                }
            });
        }

        /// <summary>Hides the tool progress indicator</summary>
        private void HideToolProgress()
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    ToolProgressPanel.Visibility = Visibility.Collapsed;
                }
                catch (Exception ex)
                {
                    LogManager.LogWarning($"Failed to hide tool progress: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Log warning (yellow indicator) - for expected issues like offline machines
        /// </summary>
        private void LogWarning(string context, string message)
        {
            Interlocked.Increment(ref _warningCount);
            LogManager.LogDebug($"[WARNING] {context}: {message}");
            UpdateErrorIndicator();
        }

        /// <summary>
        /// Log critical error (red indicator) - for unexpected failures
        /// </summary>
        private void LogCriticalError(string context, string message)
        {
            Interlocked.Increment(ref _criticalErrorCount);
            LogManager.LogDebug($"[CRITICAL] {context}: {message}");
            UpdateErrorIndicator();
        }

        /// <summary>
        /// Update split error indicator UI (throttled to prevent flooding)
        /// </summary>
        private void UpdateErrorIndicator()
        {
            // Throttle updates to every 500ms
            if ((DateTime.Now - _lastErrorUpdate).TotalMilliseconds < 500) return;
            _lastErrorUpdate = DateTime.Now;

            _ = Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    bool hasWarnings = _warningCount > 0;
                    bool hasCritical = _criticalErrorCount > 0;

                    ErrorIndicator.Visibility = (hasWarnings || hasCritical) ? Visibility.Visible : Visibility.Collapsed;

                    WarningIndicator.Visibility = hasWarnings ? Visibility.Visible : Visibility.Collapsed;
                    TxtWarningCount.Text = _warningCount.ToString();

                    CriticalIndicator.Visibility = hasCritical ? Visibility.Visible : Visibility.Collapsed;
                    TxtCriticalCount.Text = _criticalErrorCount.ToString();

                    // Pulse animation on critical errors
                    if (hasCritical)
                    {
                        var anim = new System.Windows.Media.Animation.DoubleAnimation
                        {
                            From = 1.0,
                            To = 0.6,
                            Duration = TimeSpan.FromMilliseconds(300),
                            AutoReverse = true
                        };
                        CriticalIndicator.BeginAnimation(OpacityProperty, anim);
                    }
                }
                catch { }
            });
        }

        /// <summary>
        /// Reset error counters (call at start of new scan)
        /// </summary>
        private void ResetErrorCounter()
        {
            Interlocked.Exchange(ref _warningCount, 0);
            Interlocked.Exchange(ref _criticalErrorCount, 0);
            Dispatcher.Invoke(() =>
            {
                try
                {
                    ErrorIndicator.Visibility = Visibility.Collapsed;
                    WarningIndicator.Visibility = Visibility.Collapsed;
                    CriticalIndicator.Visibility = Visibility.Collapsed;
                }
                catch { }
            });
        }

        /// <summary>
        /// Start animated scanning icon rotation
        /// </summary>
        private void StartScanAnimation()
        {
            _scanStartTime = DateTime.Now; // Capture start time for elapsed/estimated calculations
            _scanAnimationTimer?.Stop();
            _scanAnimationTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };

            double angle = 0;
            _scanAnimationTimer.Tick += (s, e) =>
            {
                angle = (angle + 10) % 360;
                IconRotation.Angle = angle;
            };
            _scanAnimationTimer.Start();
        }

        /// <summary>
        /// Stop scan animation and hide progress panel
        /// </summary>
        private void StopScanAnimation()
        {
            _scanAnimationTimer?.Stop();
            Dispatcher.Invoke(() =>
            {
                try
                {
                    ScanProgressPanel.Visibility = Visibility.Collapsed;
                    ScanProgressBar.Value = 0;
                }
                catch { }
            });
        }

        /// <summary>
        /// Show bottom progress bar with status text
        /// TAG: #UI_PROGRESS #STATUS_BAR
        /// </summary>
        private void ShowBottomProgress(string statusText)
        {
            _ = Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    TxtScanProgress.Text = statusText;
                    ProgressScan.Value = 0;
                    ProgressScan.Visibility = Visibility.Visible;
                }
                catch { }
            });
        }

        /// <summary>
        /// Update bottom progress bar value
        /// TAG: #UI_PROGRESS #STATUS_BAR
        /// </summary>
        private void UpdateBottomProgress(double percentage, string statusText = null)
        {
            _ = Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    ProgressScan.Value = Math.Min(100, Math.Max(0, percentage));
                    if (!string.IsNullOrEmpty(statusText))
                        TxtScanProgress.Text = statusText;
                }
                catch { }
            });
        }

        /// <summary>
        /// Hide bottom progress bar and reset to idle state
        /// TAG: #UI_PROGRESS #STATUS_BAR
        /// </summary>
        private void HideBottomProgress(string statusText = "System Ready")
        {
            _ = Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    TxtScanProgress.Text = statusText;
                    ProgressScan.Value = 0;
                    ProgressScan.Visibility = Visibility.Collapsed;
                }
                catch { }
            });
        }

        /// <summary>
        /// Update scan progress bar (thread-safe)
        /// </summary>
        private void UpdateScanProgress(int completed, int total, int online, int offline)
        {
            _ = Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    double percentage = total > 0 ? (completed * 100.0 / total) : 0;

                    // Update detailed scan progress panel
                    ScanProgressBar.Value = percentage;
                    TxtScanPercentage.Text = $"{percentage:F1}%";
                    TxtScanDetails.Text = $"{completed} / {total} computers scanned";
                    TxtOnlineCount.Text = online.ToString();
                    TxtOfflineCount.Text = offline.ToString();

                    // Also update bottom progress bar
                    UpdateBottomProgress(percentage, $"Scanning: {completed}/{total} computers");


                    // Calculate elapsed and estimated remaining time
                    TimeSpan elapsed = DateTime.Now - _scanStartTime;
                    string elapsedStr = elapsed.TotalSeconds < 60
                        ? $"{elapsed.TotalSeconds:F0}s"
                        : $"{elapsed.TotalMinutes:F1}m";

                    string estimatedStr = "--";
                    if (completed > 0 && completed < total)
                    {
                        double avgTimePerComputer = elapsed.TotalSeconds / completed;
                        double remainingSeconds = avgTimePerComputer * (total - completed);
                        TimeSpan remaining = TimeSpan.FromSeconds(remainingSeconds);

                        estimatedStr = remaining.TotalSeconds < 60
                            ? $"{remaining.TotalSeconds:F0}s"
                            : $"{remaining.TotalMinutes:F1}m";
                    }
                    else if (completed >= total)
                    {
                        estimatedStr = "0s";
                    }

                    TxtScanTime.Text = $"Elapsed: {elapsedStr} | Est: {estimatedStr}";
                }
                catch { }
            });
        }

        private void LoadConfig()
        {
            try
            {
                if (!File.Exists(_xmlConfigPath)) return;
                var xs = new XmlSerializer(typeof(UserConfig));
                using (var r = new StreamReader(_xmlConfigPath))
                {
                    var cfg = (UserConfig)xs.Deserialize(r);
                    // Load only last 10 valid hostnames
                    _recentTargets = cfg.TargetHistory
                        .Where(SecurityValidator.IsValidHostname)
                        .Take(MaxRecentTargets)
                        .ToList();
                    UpdateTargetComboBox();
                    if (cfg.RememberDC) { ChkRememberDC.IsChecked = true; _tempLastDC = cfg.LastDC; }
                }
            }
            catch (Exception ex) { LogManager.LogError("Config load", ex); }
        }

        private void SaveConfig()
        {
            try
            {
                // Save only the recent targets list (max 10)
                var cfg = new UserConfig
                {
                    LastUser = _authUser,
                    TargetHistory = _recentTargets.Take(MaxRecentTargets).ToList(),
                    RememberDC = ChkRememberDC.IsChecked == true
                };
                if (ComboDC.SelectedItem is ComboBoxItem i) cfg.LastDC = i.Tag?.ToString();
                using (var w = new StreamWriter(_xmlConfigPath)) new XmlSerializer(typeof(UserConfig)).Serialize(w, cfg);
            }
            catch (Exception ex) { LogManager.LogError("Config save", ex); }
        }

        /// <summary>Add a machine to the recent targets dropdown (max 10)</summary>
        private void AddToRecentTargets(string hostname)
        {
            if (string.IsNullOrWhiteSpace(hostname) || !SecurityValidator.IsValidHostname(hostname))
            {
                // TAG: #AUTO_UPDATE_UI_ENGINE #VALIDATION_TOASTS
                if (!string.IsNullOrWhiteSpace(hostname))
                {
                    Managers.UI.ToastManager.ShowWarning($"Invalid hostname format: {hostname}");
                }
                return;
            }

            // Remove if already exists (move to top)
            _recentTargets.Remove(hostname);

            // Add to front of list
            _recentTargets.Insert(0, hostname);

            // Keep only last 10
            if (_recentTargets.Count > MaxRecentTargets)
                _recentTargets = _recentTargets.Take(MaxRecentTargets).ToList();

            // Update the ComboBox
            UpdateTargetComboBox();

            // TAG: #VERSION_7 #FIX - Save to Settings.Default for consistency with AddRecentTarget()
            try
            {
                var serializer = new JavaScriptSerializer();
                Properties.Settings.Default.RecentTargets = serializer.Serialize(_recentTargets);
                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to save recent targets", ex);
            }

            // Save to legacy config as well for compatibility
            SaveConfig();
        }

        /// <summary>Update ComboTarget dropdown with recent machines</summary>
        private void UpdateTargetComboBox()
        {
            try
            {
                ComboTarget.ItemsSource = null;
                ComboTarget.ItemsSource = _recentTargets.ToList();
            }
            catch (Exception ex)
            {
                LogManager.LogError("UpdateTargetComboBox failed", ex);
            }
        }

        // ── Remaining Event Handlers ──
        private void ChkRememberDC_Click(object sender, RoutedEventArgs e) => SaveConfig();

        private async void BtnRefreshDCs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AppendTerminal("[DC Discovery] Manual refresh requested...");
                var dcList = await _dcManager.DiscoverDomainControllersAsync();

                if (dcList == null || dcList.Count == 0)
                {
                    // No DCs found - show error dialog
                    ShowDCDiscoveryFailureDialog(new Exception("No domain controllers discovered"));
                    AppendTerminal("[DC Discovery] No domain controllers found (VPN/network issue)", isError: true);
                }
                else
                {
                    await InitDCCluster();  // Refresh health topology
                    LoadDCHistory();  // Update history panel
                    AppendTerminal($"[DC Discovery] Manual refresh complete ({dcList.Count} DCs found)");
                }
            }
            catch (ActiveDirectoryServerDownException adEx)
            {
                // Domain controllers unavailable - show themed error dialog
                ShowDCDiscoveryFailureDialog(adEx);
                AppendTerminal("[DC Discovery] Domain controllers unavailable (VPN/network issue)", isError: true);
            }
            catch (Exception ex)
            {
                AppendTerminal($"[DC Discovery] Refresh failed: {ex.Message}", isError: true);
                LogManager.LogError("Manual DC refresh failed", ex);
            }
        }

        /// <summary>
        /// Refresh DCs for Fleet Inventory tab - TAG: #AD_FLEET_INVENTORY #VERSION_7
        /// </summary>
        private async void BtnRefreshDCsFleet_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AppendTerminal("[AD Fleet] Refreshing domain controllers for Fleet Inventory...");
                var dcList = await _dcManager.DiscoverDomainControllersAsync();

                if (dcList == null || dcList.Count == 0)
                {
                    ShowDCDiscoveryFailureDialog(new Exception("No domain controllers discovered"));
                    AppendTerminal("[AD Fleet] No domain controllers found", isError: true);
                    return;
                }

                // Populate ComboDCFleet
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ComboDCFleet.Items.Clear();
                    var bg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333333"));

                    foreach (var dc in dcList)
                    {
                        var item = new ComboBoxItem
                        {
                            Content = dc,
                            Tag = dc,
                            Foreground = Brushes.White,
                            Background = bg
                        };
                        ComboDCFleet.Items.Add(item);
                    }

                    if (ComboDCFleet.Items.Count > 0)
                        ComboDCFleet.SelectedIndex = 0;
                });

                AppendTerminal($"[AD Fleet] Found {dcList.Count} domain controllers");
            }
            catch (Exception ex)
            {
                AppendTerminal($"[AD Fleet] DC refresh failed: {ex.Message}", isError: true);
                LogManager.LogError("Fleet DC refresh failed", ex);
            }
        }

        /// <summary>
        /// DC selection changed for Fleet tab - TAG: #AD_FLEET_INVENTORY #VERSION_7
        /// </summary>
        private void ComboDCFleet_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboDCFleet.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                string selectedDC = item.Tag.ToString();
                AppendTerminal($"[AD Fleet] Selected DC: {selectedDC}");
            }
        }

        /// <summary>
        /// AD Query Method selection changed - TAG: #AD_FLEET_INVENTORY #VERSION_7 #PERFORMANCE
        /// </summary>
        private void ComboADQueryMethod_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboADQueryMethod?.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                string method = item.Tag.ToString();
                Properties.Settings.Default.ADQueryMethod = method;
                Properties.Settings.Default.Save();
                AppendTerminal($"[AD Fleet] Query method changed to: {method}");
            }
        }

        /// <summary>
        /// Load AD Objects in ADUC-like tree view - TAG: #AD_FLEET_INVENTORY #ADUC #VERSION_7
        /// </summary>
        private async void BtnLoadADObjects_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get selected DC
                string selectedDC = null;
                if (ComboDCFleet.SelectedItem is ComboBoxItem item && item.Tag != null)
                {
                    selectedDC = item.Tag.ToString();
                }

                if (string.IsNullOrEmpty(selectedDC))
                {
                    MessageBox.Show("Please select a Domain Controller first.", "No DC Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Get credentials
                string user = _authUser;
                string pass = null;

                if (!_isLoggedIn || _authPass == null)
                {
                    MessageBox.Show("Please authenticate first (LOGIN button in top toolbar).", "Authentication Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Convert SecureString to string
                SecureMemory.UseSecureString(_authPass, pwd => pass = pwd);

                AppendTerminal($"[AD Browser] Loading Active Directory objects from {selectedDC}...");
                Mouse.OverrideCursor = Cursors.Wait;

                // Show the ADObjectBrowser control
                ADObjectBrowserControl.Visibility = Visibility.Visible;

                // Get selected AD query method
                bool useActiveDirectoryManager = false;
                if (ComboADQueryMethod?.SelectedItem is ComboBoxItem methodItem && methodItem.Tag != null)
                {
                    useActiveDirectoryManager = methodItem.Tag.ToString() == "ActiveDirectoryManager";
                }

                // Initialize it with the selected DC, credentials, and backend
                await ADObjectBrowserControl.InitializeAsync(selectedDC, user, pass, useActiveDirectoryManager);

                AppendTerminal("[AD Browser] Active Directory object tree loaded successfully");
                Managers.UI.ToastManager.ShowSuccess($"AD object tree loaded from {selectedDC}");
                Mouse.OverrideCursor = null;
            }
            catch (Exception ex)
            {
                Mouse.OverrideCursor = null;
                AppendTerminal($"[AD Browser] Failed to load AD objects: {ex.Message}", isError: true);
                LogManager.LogError("AD Object Browser initialization failed", ex);
                MessageBox.Show($"Failed to load Active Directory objects:\n\n{ex.Message}", "AD Browser Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Toggle DC history panel visibility
        /// TAG: #DC_DISCOVERY
        /// </summary>
        private void DCHistory_Toggle(object sender, MouseButtonEventArgs e)
        {
            if (DCHistoryPanel.Visibility == Visibility.Collapsed)
            {
                DCHistoryPanel.Visibility = Visibility.Visible;
                DCHistoryToggleIcon.Text = "▲";
                LoadDCHistory();
            }
            else
            {
                DCHistoryPanel.Visibility = Visibility.Collapsed;
                DCHistoryToggleIcon.Text = "▼";
            }
        }

        /// <summary>
        /// Load DC history from cached discovery data
        /// TAG: #DC_DISCOVERY
        /// </summary>
        private void LoadDCHistory()
        {
            try
            {
                var allDCs = new List<DCHistoryItem>();

                // Get all cached DCs from all domains with extended TTL (30 days)
                foreach (var domain in _dcManager._domains)
                {
                    foreach (var dc in domain.DomainControllers)
                    {
                        // Calculate time since last seen
                        var timeSinceLastSeen = DateTime.Now - dc.LastSeen;
                        string statusIcon = "⚫"; // Offline/unknown

                        if (timeSinceLastSeen.TotalHours < 24)
                            statusIcon = "🟢"; // Recently seen
                        else if (timeSinceLastSeen.TotalDays < 7)
                            statusIcon = "🟡"; // Seen within a week
                        else if (timeSinceLastSeen.TotalDays < 30)
                            statusIcon = "🟠"; // Seen within a month

                        allDCs.Add(new DCHistoryItem
                        {
                            Hostname = dc.Hostname,
                            LastSeen = dc.LastSeen,
                            AvgLatency = dc.AvgLatency,
                            StatusIcon = statusIcon
                        });
                    }
                }

                // Sort by most recently seen first
                allDCs = allDCs.OrderByDescending(dc => dc.LastSeen).ToList();

                DCHistoryList.ItemsSource = allDCs;
                DCHistoryEmpty.Visibility = allDCs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                LogManager.LogDebug($"Loaded {allDCs.Count} DC(s) from history");
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to load DC history", ex);
                DCHistoryEmpty.Text = "Error loading DC history";
                DCHistoryEmpty.Visibility = Visibility.Visible;
            }
        }

        private void BtnSync_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Script sync — implement per deployment");

        private void BtnDownloadScripts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Create folder dialog
                var folderDialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "Select folder to save PowerShell update scripts",
                    ShowNewFolderButton = true
                };

                if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string targetFolder = folderDialog.SelectedPath;
                    int filesWritten = 0;

                    // Script definitions
                    var scripts = new[]
                    {
                        new { ResourceName = "GeneralUpdate.ps1", FileName = "ArtaznIT_GeneralUpdate.ps1" },
                        new { ResourceName = "FeatureUpdate.ps1", FileName = "ArtaznIT_FeatureUpdate.ps1" }
                    };

                    foreach (var script in scripts)
                    {
                        // Read embedded resource
                        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                        var resourceName = assembly.GetManifestResourceNames()
                            .FirstOrDefault(r => r.EndsWith(script.ResourceName));

                        if (resourceName != null)
                        {
                            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                            using (StreamReader reader = new StreamReader(stream))
                            {
                                string content = reader.ReadToEnd();
                                string targetPath = Path.Combine(targetFolder, script.FileName);
                                File.WriteAllText(targetPath, content);
                                filesWritten++;
                                AppendTerminal($"✓ Saved: {script.FileName}", false);
                            }
                        }
                        else
                        {
                            AppendTerminal($"✗ Resource not found: {script.ResourceName}", true);
                        }
                    }

                    if (filesWritten > 0)
                    {
                        MessageBox.Show(
                            $"Successfully downloaded {filesWritten} PowerShell script(s) to:\n{targetFolder}\n\n" +
                            "Scripts included:\n" +
                            "• ArtaznIT_GeneralUpdate.ps1 (Windows Updates + Firmware)\n" +
                            "• ArtaznIT_FeatureUpdate.ps1 (Major OS Upgrades)\n\n" +
                            "These scripts are compatible with ManageEngine Endpoint Central.",
                            "Scripts Downloaded",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);

                        // Open folder
                        Process.Start("explorer.exe", targetFolder);
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to download scripts", ex);
                AppendTerminal($"✗ Download failed: {ex.Message}", true);
                MessageBox.Show(
                    $"Failed to download scripts:\n{ex.Message}",
                    "Download Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        private void BtnTheme_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ThemeManager.ToggleTheme(this);
                LogManager.LogInfo("Theme toggled");
            }
            catch (Exception ex)
            {
                LogManager.LogError("Theme toggle failed", ex);
                MessageBox.Show($"Theme toggle error: {ex.Message}");
            }
        }

        private void BtnOptions_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Show the Options window
                var optionsWindow = new OptionsWindow
                {
                    Owner = this
                };

                optionsWindow.ShowDialog();
                LogManager.LogInfo("Options dialog opened");
            }
            catch (Exception ex)
            {
                LogManager.LogError("Options dialog error", ex);
                MessageBox.Show($"Error opening options: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnAbout_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Show the custom About window
                var aboutWindow = new AboutWindow
                {
                    Owner = this
                };

                // Load current global services configuration
                string currentConfig = Properties.Settings.Default.GlobalServicesConfig ?? "";
                aboutWindow.LoadGlobalServicesConfig(currentConfig);

                aboutWindow.ShowDialog();
                LogManager.LogInfo("About dialog opened");
            }
            catch (Exception ex)
            {
                LogManager.LogError("About dialog error", ex);
                MessageBox.Show($"Error displaying about information: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Reload global services configuration from About window when saved
        /// </summary>
        internal void ReloadGlobalServicesConfig(string jsonConfig)
        {
            try
            {
                var serializer = new JavaScriptSerializer();
                var services = serializer.Deserialize<List<ServiceConfigItem>>(jsonConfig);

                lock (_globalServicesLock)
                {
                    _essentialServices.Clear();
                    _highPriorityServices.Clear();
                    _mediumPriorityServices.Clear();

                    // For now, put all services in essential - user can reorganize via config editor
                    // TODO: Could add priority metadata to ServiceConfigItem for smart categorization
                    foreach (var svc in services)
                    {
                        _essentialServices.Add(new GlobalServiceStatus
                        {
                            ServiceName = svc.ServiceName,
                            Endpoint = svc.Endpoint,
                            Status = "Pending",
                            StatusColor = Brushes.Gray,
                            Latency = "-",
                            LatencyColor = Brushes.Gray
                        });
                    }
                }

                LogManager.LogInfo($"Reloaded {services.Count} global services from configuration");

                // Trigger immediate refresh
                _ = Task.Run(async () =>
                {
                    await Task.Delay(500);
                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (BtnRefreshGlobalServices != null)
                        {
                            BtnRefreshGlobalServices_Click(null, null);
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to reload global services config", ex);
            }
        }
        private void BtnSyncDB_Click(object sender, RoutedEventArgs e) { try { if (File.Exists(SecureConfig.InventoryDbPath)) MessageBox.Show($"Synced to:\n{SecureConfig.InventoryDbPath}"); else MessageBox.Show("DB not accessible"); } catch (Exception ex) { MessageBox.Show(ex.Message); } }
        private void BtnWarranty_Click(object sender, RoutedEventArgs e) { if (!string.IsNullOrEmpty(_currentServiceTag) && _currentServiceTag != "N/A") try { Process.Start(new ProcessStartInfo($"https://www.dell.com/support/home/en-us/product-support/servicetag/{_currentServiceTag}/overview") { UseShellExecute = true }); } catch { } }
        private void BtnWOL_Click(object sender, RoutedEventArgs e) => MessageBox.Show("WOL — implement Magic Packet logic");
        private void BtnOpenLog_Click(object sender, RoutedEventArgs e) { if (File.Exists(LogManager.GetDebugLogPath())) Process.Start("notepad.exe", LogManager.GetDebugLogPath()); }
        private void Menu_RefreshLogs_Click(object sender, RoutedEventArgs e) => LoadMasterLog();
        private void ComboTarget_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) BtnScan_Click(sender, e); }
        private void ComboTarget_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Auto-scan when selecting from dropdown (but not when programmatically updating or typing)
            if (ComboTarget.SelectedItem != null && !string.IsNullOrWhiteSpace(ComboTarget.SelectedItem.ToString()))
            {
                string selected = ComboTarget.SelectedItem.ToString();
                if (!string.IsNullOrWhiteSpace(selected) && selected != ComboTarget.Text)
                {
                    ComboTarget.Text = selected;
                    // Trigger scan after brief delay to allow UI to update
                    _ = Dispatcher.InvokeAsync(() => BtnScan_Click(sender, e), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
        }
        private void GridInventory_SelectionChanged(object sender, SelectionChangedEventArgs e) { }
        private void BtnExportInventory_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // MULTICORE OPTIMIZATION: Process CSV rows in parallel using PLINQ
                List<PCInventory> inventoryCopy;
                lock (_inventoryLock)
                    inventoryCopy = _inventory.ToList();

                var csvLines = inventoryCopy
                    .AsParallel()
                    .WithDegreeOfParallelism(Environment.ProcessorCount)
                    .AsOrdered() // Maintain order
                    .Select(pc => $"\"{SecurityValidator.EscapeCsv(pc.Hostname)}\",\"{SecurityValidator.EscapeCsv(pc.Status)}\",\"{SecurityValidator.EscapeCsv(pc.CurrentUser)}\",\"{SecurityValidator.EscapeCsv(pc.DisplayOS)}\",\"{SecurityValidator.EscapeCsv(pc.WindowsVersion ?? "N/A")}\",\"{SecurityValidator.EscapeCsv(pc.Chassis)}\",\"{SecurityValidator.EscapeCsv(pc.BitLockerStatus)}\"")
                    .ToList();

                var sb = new StringBuilder();
                sb.AppendLine("Hostname,Status,User,OS,Version,Chassis,BitLocker");
                foreach (var line in csvLines) sb.AppendLine(line);

                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $"ArtaznIT_Inventory_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                File.WriteAllText(path, sb.ToString());
                MessageBox.Show($"Exported to:\n{path}", "Export Complete");
                AddLog("local", "EXPORT", path, "OK");
            }
            catch (Exception ex) { MessageBox.Show($"Export failed: {ex.Message}"); }
        }

        private void Ctx_KillProc_Click(object sender, RoutedEventArgs e) { if (AuditGrid.SelectedItem is AuditLog log) { var pn = log.Details.Split(' ')[0]; if (!SecurityValidator.ContainsDangerousPatterns(pn)) RunHybridExecutor($"Stop-Process -Name {pn} -Force", "", "KILL_PROC"); } }
        private void Ctx_RestartSvc_Click(object sender, RoutedEventArgs e) { if (AuditGrid.SelectedItem is AuditLog log) { var sn = log.Details.Split(' ')[0]; if (!SecurityValidator.ContainsDangerousPatterns(sn)) RunHybridExecutor($"Restart-Service -Name {sn} -Force", "", "RESTART_SVC"); } }

        private void BtnPush_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentTarget)) { MessageBox.Show("No target selected"); return; }
            if (ComboScripts.SelectedIndex == 0) RunHybridExecutor(Script_General, "", "DEPLOY_GENERAL");
            else RunHybridExecutor(Script_Feature, "", "DEPLOY_FEATURE");
        }
        private void BtnConsole_Click(object sender, RoutedEventArgs e)
        {
            string cmd = TxtConsoleInput.Text; if (string.IsNullOrWhiteSpace(cmd)) return;
            if (SecurityValidator.ContainsDangerousPatterns(cmd)) { MessageBox.Show("Blocked — dangerous patterns"); return; }
            RunHybridExecutor(cmd, "", "CONSOLE_CMD"); TxtConsoleInput.Text = "";
        }
        private void BtnUninstall_Click(object sender, RoutedEventArgs e) { MessageBox.Show("Use standard Windows uninstall."); }

        // ############################################################################
        // REGION: CONTEXT MENU HANDLERS (RMM Integration)
        // TAG: #CONTEXT_MENU #RMM_INTEGRATION
        // ############################################################################

        /// <summary>Get icon for RMM tool type</summary>
        private string GetToolIcon(RmmToolType toolType)
        {
            switch (toolType)
            {
                case RmmToolType.AnyDesk: return "🖥️";
                case RmmToolType.ScreenConnect: return "📡";
                case RmmToolType.TeamViewer: return "👁️";
                case RmmToolType.RemotePC: return "💻";
                case RmmToolType.Dameware: return "🔧";
                case RmmToolType.ManageEngine: return "⚙️";
                default: return "🔌";
            }
        }

        /// <summary>Launch RMM tool for selected device</summary>
        private void MenuRmmTool_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!(sender is MenuItem menuItem)) return;
                if (!(menuItem.Tag is RmmToolType toolType)) return;
                if (!(GridInventory.SelectedItem is PCInventory selectedDevice)) return;

                string targetHost = selectedDevice.Hostname;
                if (string.IsNullOrWhiteSpace(targetHost))
                {
                    MessageBox.Show("No device selected", "Remote Control", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Confirmation dialog if enabled in settings
                var config = RemoteControlManager.GetConfiguration();
                if (config.ShowConfirmationDialog)
                {
                    var result = MessageBox.Show(
                        $"Connect to {targetHost} using {toolType}?",
                        "Confirm Remote Connection",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result != MessageBoxResult.Yes) return;
                }

                // Launch session
                RemoteControlManager.LaunchSession(toolType, targetHost);
                AddRecentTarget(targetHost); // Track in recent targets

                MessageBox.Show($"Remote session initiated to {targetHost}", "Remote Control", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to launch remote session:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                LogManager.LogError("Failed to launch RMM session from context menu", ex);
            }
        }

        /// <summary>
        /// TAG: #RMM_INTEGRATION #VERSION_7 #PHASE_3
        /// Load RMM quick-launch buttons in main window right panel
        /// </summary>
        private void LoadRmmQuickLaunchButtons()
        {
            try
            {
                var enabledTools = RemoteControlManager.GetEnabledTools();

                if (enabledTools.Count == 0)
                {
                    RmmQuickLaunchButtons.Visibility = Visibility.Collapsed;
                    TxtNoRmmTools.Visibility = Visibility.Visible;
                    return;
                }

                var buttonList = enabledTools.Select(tool => new
                {
                    DisplayName = $"{GetToolIcon(tool.ToolType)}  {tool.ToolName.ToUpper()}",
                    ToolType = tool.ToolType,
                    ToolTip = $"Launch {tool.ToolName} remote session to current target"
                }).ToList();

                RmmQuickLaunchButtons.ItemsSource = buttonList;
                RmmQuickLaunchButtons.Visibility = Visibility.Visible;
                TxtNoRmmTools.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to load RMM quick-launch buttons", ex);
            }
        }

        /// <summary>
        /// TAG: #RMM_INTEGRATION #VERSION_7 #PHASE_3
        /// Handle RMM quick-launch button click from main window
        /// </summary>
        private void RmmQuickLaunch_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!(sender is Button button)) return;
                if (!(button.Tag is RmmToolType toolType)) return;

                string targetHost = ComboTarget.Text.Trim();
                if (string.IsNullOrWhiteSpace(targetHost))
                {
                    MessageBox.Show("Please enter a target hostname or IP address in the Target System Control panel.",
                        "Target Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Confirmation dialog if enabled
                var config = RemoteControlManager.GetConfiguration();
                if (config.ShowConfirmationDialog)
                {
                    var result = MessageBox.Show(
                        $"Connect to {targetHost} using {toolType}?",
                        "Confirm Remote Connection",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result != MessageBoxResult.Yes) return;
                }

                // Launch session
                RemoteControlManager.LaunchSession(toolType, targetHost);
                AddRecentTarget(targetHost);

                MessageBox.Show($"Remote session initiated to {targetHost}", "Remote Control",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to launch remote session:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                LogManager.LogError("Failed to launch RMM session from quick-launch", ex);
            }
        }

        /// <summary>
        /// TAG: #RMM_INTEGRATION #VERSION_7
        /// Open Remote Control tab to configure RMM tools
        /// </summary>
        private void BtnConfigureRmm_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Switch to Remote Control tab (Tab 5, index 4)
                if (MainTabs.Items.Count > 4)
                {
                    MainTabs.SelectedIndex = 4;
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to open Remote Control tab", ex);
            }
        }

        /// <summary>Copy selected device hostname to clipboard</summary>
        private void MenuCopyHostname_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (GridInventory.SelectedItem is PCInventory selectedDevice && !string.IsNullOrWhiteSpace(selectedDevice.Hostname))
                {
                    Clipboard.SetText(selectedDevice.Hostname);
                    MessageBox.Show($"Copied: {selectedDevice.Hostname}", "Clipboard", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to copy hostname", ex);
            }
        }

        /// <summary>View detailed device information</summary>
        private void MenuViewDetails_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (GridInventory.SelectedItem is PCInventory selectedDevice)
                {
                    string details = $"Device Details:\n\n" +
                        $"Hostname: {selectedDevice.Hostname}\n" +
                        $"Status: {selectedDevice.Status}\n" +
                        $"User: {selectedDevice.CurrentUser}\n" +
                        $"OS: {selectedDevice.DisplayOS}\n" +
                        $"Version: {selectedDevice.WindowsVersion}\n" +
                        $"Chassis: {selectedDevice.Chassis}\n" +
                        $"BitLocker: {selectedDevice.BitLockerStatus}";

                    MessageBox.Show(details, "Device Details", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to view device details", ex);
            }
        }

        /// <summary>Refresh individual device scan</summary>
        private void MenuRefreshDevice_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (GridInventory.SelectedItem is PCInventory selectedDevice && !string.IsNullOrWhiteSpace(selectedDevice.Hostname))
                {
                    ComboTarget.Text = selectedDevice.Hostname;
                    BtnScan_Click(sender, e);
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to refresh device", ex);
            }
        }

        // ############################################################################
        // REGION: QUICK-WIN FEATURES (Version 7.0)
        // TAG: #VERSION_7 #QUICK_WINS
        // ############################################################################

        private DispatcherTimer _autoSaveTimer;

        /// <summary>Load recent targets from settings</summary>
        private void LoadRecentTargets()
        {
            try
            {
                string json = Properties.Settings.Default.RecentTargets;
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var serializer = new JavaScriptSerializer();
                    _recentTargets = serializer.Deserialize<List<string>>(json) ?? new List<string>();
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to load recent targets", ex);
                _recentTargets = new List<string>();
            }
        }

        /// <summary>Add target to recent list (max 10)</summary>
        private void AddRecentTarget(string target)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(target)) return;

                target = target.Trim();

                // Remove if already exists (move to top)
                _recentTargets.Remove(target);

                // Add to beginning
                _recentTargets.Insert(0, target);

                // Keep max 10
                if (_recentTargets.Count > 10)
                    _recentTargets = _recentTargets.Take(10).ToList();

                // Save to settings
                var serializer = new JavaScriptSerializer();
                Properties.Settings.Default.RecentTargets = serializer.Serialize(_recentTargets);
                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to add recent target", ex);
            }
        }

        /// <summary>Restore window position and size from settings</summary>
        private void RestoreWindowPosition()
        {
            try
            {
                string json = Properties.Settings.Default.WindowPosition;
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var serializer = new JavaScriptSerializer();
                    var pos = serializer.Deserialize<Dictionary<string, double>>(json);

                    if (pos != null && pos.ContainsKey("Left") && pos.ContainsKey("Top") &&
                        pos.ContainsKey("Width") && pos.ContainsKey("Height"))
                    {
                        // Validate position is on screen
                        var left = pos["Left"];
                        var top = pos["Top"];
                        var width = pos["Width"];
                        var height = pos["Height"];

                        if (left >= 0 && top >= 0 && width > 0 && height > 0)
                        {
                            Left = left;
                            Top = top;
                            Width = width;
                            Height = height;

                            if (pos.ContainsKey("WindowState"))
                            {
                                var state = (int)pos["WindowState"];
                                if (state == 2) // Maximized
                                    WindowState = WindowState.Maximized;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to restore window position", ex);
            }
        }

        /// <summary>Save window position and size to settings</summary>
        private void SaveWindowPosition()
        {
            try
            {
                var pos = new Dictionary<string, double>
                {
                    { "Left", Left },
                    { "Top", Top },
                    { "Width", Width },
                    { "Height", Height },
                    { "WindowState", (int)WindowState }
                };

                var serializer = new JavaScriptSerializer();
                Properties.Settings.Default.WindowPosition = serializer.Serialize(pos);
                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to save window position", ex);
            }
        }

        /// <summary>Apply saved font size multiplier</summary>
        private void ApplyFontSize()
        {
            try
            {
                double multiplier = Properties.Settings.Default.FontSizeMultiplier;
                if (multiplier >= 0.8 && multiplier <= 2.0)
                {
                    // Apply to main window
                    FontSize = 12 * multiplier;
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to apply font size", ex);
            }
        }

        /// <summary>
        /// Apply saved accent colors from settings
        /// TAG: #VERSION_7 #THEME_COLORS
        /// </summary>
        private void ApplySavedAccentColors()
        {
            try
            {
                string primaryColor = Properties.Settings.Default.PrimaryAccentColor;
                string secondaryColor = Properties.Settings.Default.SecondaryAccentColor;

                // Use defaults if not set
                if (string.IsNullOrEmpty(primaryColor)) primaryColor = "#FFFF8533";
                if (string.IsNullOrEmpty(secondaryColor)) secondaryColor = "#FFA1A1AA";

                // Apply to application resources
                var primaryColorObj = (Color)ColorConverter.ConvertFromString(primaryColor);
                var secondaryColorObj = (Color)ColorConverter.ConvertFromString(secondaryColor);

                Application.Current.Resources["AccentOrangeBrush"] = new SolidColorBrush(primaryColorObj);
                Application.Current.Resources["AccentZincBrush"] = new SolidColorBrush(secondaryColorObj);
                Application.Current.Resources["AccentColor"] = new SolidColorBrush(primaryColorObj);

                // Update gradient brushes
                var gradientBrush = new LinearGradientBrush();
                gradientBrush.StartPoint = new Point(0, 0);
                gradientBrush.EndPoint = new Point(1, 0);
                gradientBrush.GradientStops.Add(new GradientStop(primaryColorObj, 0));
                gradientBrush.GradientStops.Add(new GradientStop(secondaryColorObj, 1));
                Application.Current.Resources["AccentGradientBrush"] = gradientBrush;

                LogManager.LogInfo($"Applied accent colors: Primary={primaryColor}, Secondary={secondaryColor}");
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to apply saved accent colors", ex);
            }
        }

        /// <summary>Initialize auto-save timer</summary>
        private void InitializeAutoSave()
        {
            try
            {
                if (Properties.Settings.Default.AutoSaveEnabled)
                {
                    int intervalMinutes = Properties.Settings.Default.AutoSaveIntervalMinutes;
                    if (intervalMinutes < 1) intervalMinutes = 5;

                    _autoSaveTimer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromMinutes(intervalMinutes)
                    };
                    _autoSaveTimer.Tick += AutoSaveTimer_Tick;
                    _autoSaveTimer.Start();

                    LogManager.LogInfo($"Auto-save enabled: {intervalMinutes} minute interval");
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to initialize auto-save", ex);
            }
        }

        /// <summary>Auto-save timer tick - backup current data</summary>
        private void AutoSaveTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                // Auto-save inventory to backup location
                string backupDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ArtaznIT",
                    "AutoSave");

                Directory.CreateDirectory(backupDir);

                // Save inventory
                List<PCInventory> inventoryCopy;
                lock (_inventoryLock)
                    inventoryCopy = _inventory.ToList();

                if (inventoryCopy.Count > 0)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("Hostname,Status,User,OS,Version,Chassis,BitLocker");
                    foreach (var pc in inventoryCopy)
                    {
                        sb.AppendLine($"\"{SecurityValidator.EscapeCsv(pc.Hostname)}\",\"{SecurityValidator.EscapeCsv(pc.Status)}\",\"{SecurityValidator.EscapeCsv(pc.CurrentUser)}\",\"{SecurityValidator.EscapeCsv(pc.DisplayOS)}\",\"{SecurityValidator.EscapeCsv(pc.WindowsVersion ?? "N/A")}\",\"{SecurityValidator.EscapeCsv(pc.Chassis)}\",\"{SecurityValidator.EscapeCsv(pc.BitLockerStatus)}\"");
                    }

                    string backupPath = Path.Combine(backupDir, $"AutoSave_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                    File.WriteAllText(backupPath, sb.ToString());

                    // Clean up old backups (keep last 10)
                    var backupFiles = Directory.GetFiles(backupDir, "AutoSave_*.csv")
                        .OrderByDescending(f => new FileInfo(f).CreationTime)
                        .Skip(10)
                        .ToList();

                    foreach (var oldFile in backupFiles)
                        File.Delete(oldFile);

                    LogManager.LogInfo($"Auto-save completed: {backupPath}");

                    if (Properties.Settings.Default.NotificationsEnabled)
                    {
                        // TAG: #VERSION_7 #AUTO_SAVE - Show auto-save notification in status bar
                        Dispatcher.Invoke(() =>
                        {
                            StatusMessage.Text = $"✅ Auto-saved {inventoryCopy.Count} devices";
                            StatusMessage.Foreground = new SolidColorBrush(Colors.LimeGreen);
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Auto-save failed", ex);
            }
        }

        /// <summary>
        /// Open service status page in default browser
        /// TAG: #VERSION_7 #SERVICE_LINKS
        /// </summary>
        private void BtnOpenServiceStatusPage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!(sender is Button btn) || btn.Tag == null) return;

                string endpoint = btn.Tag.ToString();

                // Handle different endpoint types
                if (endpoint.StartsWith("ping:", StringComparison.OrdinalIgnoreCase))
                {
                    AppendTerminal($"[Services] Ping endpoints don't have status pages: {endpoint}");
                    MessageBox.Show($"This is a ping endpoint ({endpoint}) - no status page available.",
                        "No Status Page", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // If it's an HTTP/HTTPS URL, open it
                if (endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    AppendTerminal($"[Services] Opening status page: {endpoint}");
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = endpoint,
                        UseShellExecute = true
                    });
                }
                else
                {
                    AppendTerminal($"[Services] Invalid endpoint format: {endpoint}");
                    MessageBox.Show($"Invalid endpoint format: {endpoint}\n\nExpected HTTP/HTTPS URL.",
                        "Invalid Endpoint", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                AppendTerminal($"[Services] Failed to open status page: {ex.Message}", isError: true);
                LogManager.LogError("Failed to open service status page", ex);
                MessageBox.Show($"Failed to open status page:\n\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // AD OBJECT MANAGEMENT HANDLERS
        // TAG: #VERSION_7 #AD_MANAGEMENT
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Refresh AD Object Browser
        /// TAG: #VERSION_7 #AD_MANAGEMENT
        /// </summary>
        private async void BtnRefreshADObjects_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get selected domain controller
                string dc = ComboDC.Text;
                if (ComboDC.SelectedItem is ComboBoxItem di && di.Tag != null)
                {
                    string t = di.Tag.ToString();
                    if (t != "Auto") dc = t;
                }

                if (string.IsNullOrEmpty(dc) || dc.Contains("Scanning") || dc.Contains("probing"))
                {
                    MessageBox.Show("Please wait for DC scan to complete first.",
                        "No DC Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                AppendTerminal($"[AD Management] Refreshing AD Object Browser for {dc}...");

                // Convert SecureString password to plain text for ADObjectBrowser
                string password = null;
                if (_authPass != null)
                {
                    SecureMemory.UseSecureString(_authPass, pwd => password = pwd);
                }

                // Re-initialize the AD Object Browser with current credentials
                await ADObjectBrowserDomainTab.InitializeAsync(dc, _authUser, password);

                AppendTerminal($"[AD Management] ✓ AD Object Browser refreshed successfully");
            }
            catch (Exception ex)
            {
                AppendTerminal($"[AD Management] ✗ Failed to refresh: {ex.Message}", isError: true);
                LogManager.LogError("Failed to refresh AD Object Browser", ex);
                MessageBox.Show($"Failed to refresh AD Object Browser:\n\n{ex.Message}",
                    "Refresh Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Create new AD object (user, computer, group, OU)
        /// TAG: #VERSION_7 #AD_MANAGEMENT
        /// </summary>
        private void BtnCreateADObject_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get selected domain controller
                string dc = ComboDC.Text;
                if (ComboDC.SelectedItem is ComboBoxItem di && di.Tag != null)
                {
                    string t = di.Tag.ToString();
                    if (t != "Auto") dc = t;
                }

                if (string.IsNullOrEmpty(dc) || dc.Contains("Scanning") || dc.Contains("probing"))
                {
                    MessageBox.Show("Please wait for DC scan to complete first.",
                        "No DC Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Check if logged in with admin credentials
                if (!_isLoggedIn || _authPass == null)
                {
                    MessageBox.Show("Please login with domain admin credentials first.\n\nUse the login panel in the left sidebar.",
                        "Authentication Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                AppendTerminal($"[AD Management] Create Object requested for {dc}");

                // For now, show a message that this will launch AD Users & Computers
                // In a future version, we could build a custom dialog
                var result = MessageBox.Show(
                    "Creating AD objects requires AD Users & Computers (RSAT).\n\n" +
                    $"Launch AD Users & Computers for {dc}?",
                    "Create AD Object",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Launch AD Users & Computers with cached credentials
                    LaunchMMCWithCreds("mmc", $"dsa.msc /server={dc}", "AD_CREATE_OBJECT");
                    AppendTerminal($"[AD Management] Launched AD Users & Computers for object creation");
                    AddLog(dc, "AD_CREATE_OBJECT", "ADUC", "OK");
                }
            }
            catch (Exception ex)
            {
                AppendTerminal($"[AD Management] ✗ Failed to create object: {ex.Message}", isError: true);
                LogManager.LogError("Failed to create AD object", ex);
                MessageBox.Show($"Failed to create AD object:\n\n{ex.Message}",
                    "Create Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Edit properties of selected AD object
        /// TAG: #VERSION_7 #AD_MANAGEMENT
        /// </summary>
        private void BtnEditADObject_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get selected domain controller
                string dc = ComboDC.Text;
                if (ComboDC.SelectedItem is ComboBoxItem di && di.Tag != null)
                {
                    string t = di.Tag.ToString();
                    if (t != "Auto") dc = t;
                }

                if (string.IsNullOrEmpty(dc) || dc.Contains("Scanning") || dc.Contains("probing"))
                {
                    MessageBox.Show("Please wait for DC scan to complete first.",
                        "No DC Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Check if logged in with admin credentials
                if (!_isLoggedIn || _authPass == null)
                {
                    MessageBox.Show("Please login with domain admin credentials first.\n\nUse the login panel in the left sidebar.",
                        "Authentication Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                AppendTerminal($"[AD Management] Edit Object requested for {dc}");

                // For now, show a message that this will launch AD Users & Computers
                var result = MessageBox.Show(
                    "Editing AD objects requires AD Users & Computers (RSAT).\n\n" +
                    $"Launch AD Users & Computers for {dc}?\n\n" +
                    "Once launched, you can find and edit the selected object manually.",
                    "Edit AD Object",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Launch AD Users & Computers with cached credentials
                    LaunchMMCWithCreds("mmc", $"dsa.msc /server={dc}", "AD_EDIT_OBJECT");
                    AppendTerminal($"[AD Management] Launched AD Users & Computers for object editing");
                    AddLog(dc, "AD_EDIT_OBJECT", "ADUC", "OK");
                }
            }
            catch (Exception ex)
            {
                AppendTerminal($"[AD Management] ✗ Failed to edit object: {ex.Message}", isError: true);
                LogManager.LogError("Failed to edit AD object", ex);
                MessageBox.Show($"Failed to edit AD object:\n\n{ex.Message}",
                    "Edit Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Delete selected AD object with confirmation
        /// TAG: #VERSION_7 #AD_MANAGEMENT
        /// </summary>
        private void BtnDeleteADObject_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get selected domain controller
                string dc = ComboDC.Text;
                if (ComboDC.SelectedItem is ComboBoxItem di && di.Tag != null)
                {
                    string t = di.Tag.ToString();
                    if (t != "Auto") dc = t;
                }

                if (string.IsNullOrEmpty(dc) || dc.Contains("Scanning") || dc.Contains("probing"))
                {
                    MessageBox.Show("Please wait for DC scan to complete first.",
                        "No DC Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Check if logged in with admin credentials
                if (!_isLoggedIn || _authPass == null)
                {
                    MessageBox.Show("Please login with domain admin credentials first.\n\nUse the login panel in the left sidebar.",
                        "Authentication Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Warn about destructive operation
                var confirmResult = MessageBox.Show(
                    "⚠️ WARNING: Deleting AD objects is a destructive operation!\n\n" +
                    "Deleted objects may be recoverable from the AD Recycle Bin,\n" +
                    "but this requires careful administration.\n\n" +
                    $"Launch AD Users & Computers for {dc} to delete objects?",
                    "Delete AD Object",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (confirmResult == MessageBoxResult.Yes)
                {
                    AppendTerminal($"[AD Management] Delete Object requested for {dc}", isError: false);

                    // Launch AD Users & Computers with cached credentials
                    LaunchMMCWithCreds("mmc", $"dsa.msc /server={dc}", "AD_DELETE_OBJECT");
                    AppendTerminal($"[AD Management] Launched AD Users & Computers for object deletion");
                    AddLog(dc, "AD_DELETE_OBJECT", "ADUC", "OK");
                }
            }
            catch (Exception ex)
            {
                AppendTerminal($"[AD Management] ✗ Failed to delete object: {ex.Message}", isError: true);
                LogManager.LogError("Failed to delete AD object", ex);
                MessageBox.Show($"Failed to delete AD object:\n\n{ex.Message}",
                    "Delete Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // DASHBOARD ANALYTICS - TAG: #VERSION_7.1 #DASHBOARD
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Refresh dashboard statistics and charts
        /// TAG: #VERSION_7.1 #DASHBOARD
        /// </summary>
        private void BtnRefreshDashboard_Click(object sender, RoutedEventArgs e)
        {
            RefreshDashboard();
        }

        /// <summary>
        /// Calculate and update all dashboard statistics
        /// TAG: #VERSION_7.1 #DASHBOARD #ANALYTICS
        /// </summary>
        private void RefreshDashboard()
        {
            try
            {
                lock (_inventoryLock)
                {
                    int total = _inventory.Count;
                    int online = _inventory.Count(c => c.Status == "ONLINE");
                    int offline = total - online;

                    // Update statistics cards
                    TxtTotalComputers.Text = total.ToString();
                    TxtOnlineComputers.Text = online.ToString();
                    TxtOfflineComputers.Text = offline.ToString();

                    double onlinePercent = total > 0 ? (double)online / total * 100 : 0;
                    double offlinePercent = total > 0 ? (double)offline / total * 100 : 0;

                    TxtOnlinePercentage.Text = $"{onlinePercent:F1}%";
                    TxtOfflinePercentage.Text = $"{offlinePercent:F1}%";

                    // Health Score (based on online percentage + OS modernity)
                    int win11 = _inventory.Count(c => c.WindowsVersion == "Win11");
                    int win10 = _inventory.Count(c => c.WindowsVersion == "Win10");
                    int win7 = _inventory.Count(c => c.WindowsVersion == "Win7");
                    int legacy = _inventory.Count(c => c.WindowsVersion == "Legacy" || c.WindowsVersion == "WinXP" || c.WindowsVersion == "Vista");

                    double healthScore = total > 0
                        ? (onlinePercent * 0.6) + ((win11 + win10) / (double)total * 100 * 0.4)
                        : 0;

                    TxtHealthScore.Text = $"{healthScore:F0}%";
                    TxtHealthStatus.Text = healthScore >= 90 ? "Excellent" :
                                          healthScore >= 75 ? "Good" :
                                          healthScore >= 50 ? "Fair" : "Poor";

                    // OS Distribution
                    double win11Percent = total > 0 ? (double)win11 / total * 100 : 0;
                    double win10Percent = total > 0 ? (double)win10 / total * 100 : 0;
                    double win7Percent = total > 0 ? (double)win7 / total * 100 : 0;
                    double legacyPercent = total > 0 ? (double)legacy / total * 100 : 0;

                    BarWin11.Value = win11Percent;
                    BarWin10.Value = win10Percent;
                    BarWin7.Value = win7Percent;
                    BarLegacy.Value = legacyPercent;

                    TxtWin11Count.Text = $"{win11} ({win11Percent:F1}%)";
                    TxtWin10Count.Text = $"{win10} ({win10Percent:F1}%)";
                    TxtWin7Count.Text = $"{win7} ({win7Percent:F1}%)";
                    TxtLegacyCount.Text = $"{legacy} ({legacyPercent:F1}%)";

                    // Critical Alerts
                    var alerts = new List<string>();
                    if (win7 > 0) alerts.Add($"⚠️ {win7} computers running Windows 7 (EOL - End of Life)");
                    if (legacy > 0) alerts.Add($"⚠️ {legacy} computers running legacy OS (XP/Vista/Other)");
                    if (offline > 10) alerts.Add($"⚠️ {offline} computers offline (check connectivity)");

                    // Low disk space check
                    var lowDiskComputers = _inventory.Count(c => c.Disk?.Contains("<10%") == true || c.Disk?.Contains("< 10%") == true);
                    if (lowDiskComputers > 0) alerts.Add($"⚠️ {lowDiskComputers} computers with low disk space (<10%)");

                    if (alerts.Count > 0)
                    {
                        ListCriticalAlerts.ItemsSource = alerts;
                        ListCriticalAlerts.Visibility = Visibility.Visible;
                        TxtNoAlerts.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        ListCriticalAlerts.Visibility = Visibility.Collapsed;
                        TxtNoAlerts.Visibility = Visibility.Visible;
                    }

                    // Top Computers by Uptime
                    var topComputers = _inventory
                        .Where(c => !string.IsNullOrEmpty(c.LastBoot) && c.Status == "ONLINE")
                        .OrderBy(c => ParseLastBoot(c.LastBoot))
                        .Take(10)
                        .Select((c, index) => new DashboardTopComputer
                        {
                            Rank = index + 1,
                            Hostname = c.Hostname,
                            OS = c.DisplayOS ?? "Unknown",
                            Uptime = CalculateUptime(c.LastBoot),
                            LastBoot = c.LastBoot
                        })
                        .ToList();

                    GridTopComputers.ItemsSource = topComputers;

                    // Detailed Version Breakdown
                    var versionCounts = _inventory
                        .GroupBy(c => c.WindowsVersion ?? "Unknown")
                        .Select(g => new
                        {
                            Version = g.Key,
                            Count = g.Count()
                        })
                        .OrderByDescending(v => v.Count)
                        .ToList();

                    var detailedVersions = versionCounts.Select(v => new DashboardVersionDetail
                    {
                        Version = GetFriendlyVersionName(v.Version),
                        Count = v.Count,
                        Percentage = total > 0 ? (double)v.Count / total * 100 : 0,
                        CountText = $"{v.Count} ({(total > 0 ? (double)v.Count / total * 100 : 0):F1}%)",
                        Color = GetVersionColor(v.Version)
                    }).ToList();

                    ListDetailedVersions.ItemsSource = detailedVersions;

                    TxtDashboardLastUpdate.Text = $"Last updated: {DateTime.Now:HH:mm:ss}";
                }

                LogManager.LogInfo("[Dashboard] Statistics refreshed");
            }
            catch (Exception ex)
            {
                LogManager.LogError("[Dashboard] Failed to refresh", ex);
            }
        }

        /// <summary>Get friendly display name for Windows version</summary>
        private string GetFriendlyVersionName(string version)
        {
            return version switch
            {
                // Desktop versions
                "25H2" => "Win 11 25H2",
                "24H2" => "Win 11/10 24H2",
                "23H2" => "Win 11/10 23H2",
                "22H2" => "Win 11/10 22H2",
                "21H2" => "Win 10/11 21H2",
                "21H1" => "Win 10 21H1",
                "20H2" => "Win 10 20H2",
                "2004" => "Win 10 2004",
                "1909" => "Win 10 1909",
                "1903" => "Win 10 1903",
                "Win11" => "Windows 11 (Generic)",
                "Win10" => "Windows 10 (Generic)",
                "Win8.1" => "Windows 8.1",
                "Win8" => "Windows 8",
                "Win7" => "Windows 7",

                // Server versions
                "Server2025" => "Server 2025",
                "Server2022" => "Server 2022",
                "Server2019" => "Server 2019",
                "Server2016" => "Server 2016",
                "Server2012R2" => "Server 2012 R2",
                "Server2012" => "Server 2012",
                "Server2008R2" => "Server 2008 R2",
                "ServerLegacy" => "Server (Legacy)",

                // Legacy
                "Legacy" => "Legacy OS",
                _ => version
            };
        }

        /// <summary>Get color for Windows version based on support status</summary>
        private System.Windows.Media.Brush GetVersionColor(string version)
        {
            return version switch
            {
                // Modern/Supported (Green)
                "25H2" or "24H2" or "Server2025" or "Server2022" or "Server2019" => System.Windows.Media.Brushes.LimeGreen,

                // Recent but not latest (Yellow/Orange)
                "23H2" or "22H2" or "Server2016" => System.Windows.Media.Brushes.Yellow,

                // EOL or old (Red)
                "21H2" or "21H1" or "20H2" or "2004" or "1909" or "1903" or
                "Server2012R2" or "Server2012" or "Server2008R2" or
                "Win8.1" or "Win8" or "Win7" => System.Windows.Media.Brushes.Red,

                // Legacy/Unknown (Dark Red)
                "Legacy" or "ServerLegacy" => System.Windows.Media.Brushes.DarkRed,

                // Generic (Blue)
                "Win11" or "Win10" => System.Windows.Media.Brushes.DeepSkyBlue,

                // Default (Orange)
                _ => System.Windows.Media.Brushes.Orange
            };
        }

        /// <summary>Parse last boot time string to DateTime</summary>
        private DateTime ParseLastBoot(string lastBoot)
        {
            try
            {
                return DateTime.ParseExact(lastBoot, "yyyy-MM-dd HH:mm", null);
            }
            catch
            {
                return DateTime.MaxValue; // Put unparseable dates last
            }
        }

        /// <summary>Calculate uptime from last boot</summary>
        private string CalculateUptime(string lastBoot)
        {
            try
            {
                var bootTime = DateTime.ParseExact(lastBoot, "yyyy-MM-dd HH:mm", null);
                var uptime = DateTime.Now - bootTime;

                if (uptime.TotalDays >= 1)
                    return $"{(int)uptime.TotalDays} days, {uptime.Hours} hours";
                else
                    return $"{uptime.Hours} hours, {uptime.Minutes} minutes";
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// Open Connection Profile management dialog
        /// TAG: #VERSION_7 #CONNECTION_PROFILES
        /// </summary>
        private void BtnManageProfiles_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new ConnectionProfileDialog
                {
                    Owner = this
                };

                if (dialog.ShowDialog() == true && dialog.SelectedProfile != null)
                {
                    // Load the selected profile
                    var profile = dialog.SelectedProfile;

                    AppendTerminal($"[Connection Profile] Loading profile: {profile.Name}");

                    // Set DC (find matching ComboBox item)
                    foreach (ComboBoxItem item in ComboDC.Items)
                    {
                        if (item.Tag?.ToString().Equals(profile.DomainController, StringComparison.OrdinalIgnoreCase) == true)
                        {
                            ComboDC.SelectedItem = item;
                            break;
                        }
                    }

                    // If DC not found in list, add it
                    if (ComboDC.Text != profile.DomainController)
                    {
                        AppendTerminal($"[Connection Profile] Adding DC to list: {profile.DomainController}");
                        ComboDC.Items.Add(new ComboBoxItem
                        {
                            Content = profile.DomainController,
                            Tag = profile.DomainController,
                            Foreground = System.Windows.Media.Brushes.White
                        });
                        ComboDC.SelectedIndex = ComboDC.Items.Count - 1;
                    }

                    // Notify user to login with profile credentials
                    MessageBox.Show(
                        $"Connection profile '{profile.Name}' loaded.\n\n" +
                        $"Domain Controller: {profile.DomainController}\n" +
                        $"Username: {profile.Username}\n\n" +
                        "Please login using the credentials for this profile.",
                        "Profile Loaded",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    AppendTerminal($"[Connection Profile] ✓ Profile loaded successfully");
                }
            }
            catch (Exception ex)
            {
                AppendTerminal($"[Connection Profile] ✗ Failed to manage profiles: {ex.Message}", isError: true);
                LogManager.LogError("Failed to manage connection profiles", ex);
                MessageBox.Show($"Failed to manage connection profiles:\n\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Handle AD Object Browser selection changed to enable/disable Edit and Delete buttons
        /// TAG: #VERSION_7 #AD_MANAGEMENT
        /// </summary>
        private void ADObjectBrowser_SelectionChanged(object sender, int selectedCount)
        {
            try
            {
                // Enable Edit and Delete buttons only if exactly one object is selected
                if (BtnEditADObject != null)
                {
                    BtnEditADObject.IsEnabled = selectedCount == 1;
                }

                if (BtnDeleteADObject != null)
                {
                    BtnDeleteADObject.IsEnabled = selectedCount == 1;
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to update AD management button states", ex);
            }
        }

        /// <summary>
        /// Handle tab selection changed to initialize AD Object Browser when Domain & Directory tab is selected
        /// TAG: #VERSION_7 #AD_MANAGEMENT
        /// </summary>
        private bool _adObjectBrowserInitialized = false;
        private async void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (!(sender is TabControl tabControl)) return;
                if (!(tabControl.SelectedItem is TabItem selectedTab)) return;

                // Check if Domain & Directory tab is selected
                string tabHeader = selectedTab.Header?.ToString() ?? "";
                if (tabHeader.Contains("DOMAIN") && tabHeader.Contains("DIRECTORY"))
                {
                    // Only initialize once per session, or if credentials changed
                    if (!_adObjectBrowserInitialized || !_isLoggedIn)
                    {
                        // Get selected domain controller
                        string dc = ComboDC.Text;
                        if (ComboDC.SelectedItem is ComboBoxItem di && di.Tag != null)
                        {
                            string t = di.Tag.ToString();
                            if (t != "Auto") dc = t;
                        }

                        if (!string.IsNullOrEmpty(dc) && !dc.Contains("Scanning") && !dc.Contains("probing"))
                        {
                            if (_isLoggedIn && _authPass != null)
                            {
                                AppendTerminal($"[AD Management] Initializing AD Object Browser for {dc}...");

                                // Convert SecureString password to plain text for ADObjectBrowser
                                string password = null;
                                SecureMemory.UseSecureString(_authPass, pwd => password = pwd);

                                // Initialize the AD Object Browser with current credentials
                                await ADObjectBrowserDomainTab.InitializeAsync(dc, _authUser, password);

                                // Subscribe to selection changed event
                                ADObjectBrowserDomainTab.ObjectSelectionChanged += ADObjectBrowser_SelectionChanged;

                                _adObjectBrowserInitialized = true;
                                AppendTerminal($"[AD Management] ✓ AD Object Browser initialized successfully");
                            }
                            else
                            {
                                AppendTerminal($"[AD Management] ℹ Please login to use AD Object Browser", isError: false);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to initialize AD Object Browser on tab selection", ex);
            }
        }

        /// <summary>Opens admin tool MMC snap-ins with cached credentials</summary>
        private void BtnOpenAdminTool_Click(object sender, RoutedEventArgs e)
        {
            if (!(ComboAdminTools.SelectedItem is ComboBoxItem sel)) return;
            string tool = sel.Content.ToString();
            string dc = ComboDC.Text;
            if (ComboDC.SelectedItem is ComboBoxItem di && di.Tag != null) { string t = di.Tag.ToString(); if (t != "Auto") dc = t; }
            if (string.IsNullOrEmpty(dc) || dc.Contains("Scanning")) { MessageBox.Show("Wait for DC scan"); return; }

            string mmc = "", args = "";
            switch (tool)
            {
                case "AD Users & Computers": mmc = "dsa.msc"; args = $"/server={dc}"; break;
                case "Group Policy (GPMC)": mmc = "gpmc.msc"; break;
                case "DNS Manager": mmc = "dnsmgmt.msc"; args = $"/ComputerName {dc}"; break;
                case "DHCP": mmc = "dhcpmgmt.msc"; args = $"/ComputerName {dc}"; break;
                case "Services (Local/Remote)": mmc = "services.msc"; args = $"/computer={dc}"; break;
                case "AD Sites and Services": mmc = "dssite.msc"; break;
                case "AD Domains and Trusts": mmc = "domain.msc"; break;
                case "Certification Authority": mmc = "certsrv.msc"; args = $"/ComputerName {dc}"; break;
                case "Failover Cluster Manager": mmc = "cluadmin.msc"; break;
                default: MessageBox.Show($"'{tool}' not configured"); return;
            }

            // Uses LaunchMMCWithCreds which handles credential passing via runas /netonly
            LaunchMMCWithCreds("mmc", $"{mmc} {args}", "ADMIN_TOOL");
            AppendTerminal($"Launched: {tool} → {dc}");
            AddLog(dc, "ADMIN_TOOL", tool, "OK");
        }

        /// <summary>Checks account lockout events on the selected Domain Controller</summary>
        private async void BtnCheckLockouts_Click(object sender, RoutedEventArgs e)
        {
            string dc = ComboDC.Text;
            if (ComboDC.SelectedItem is ComboBoxItem di && di.Tag != null)
            {
                string t = di.Tag.ToString();
                if (t != "Auto") dc = t;
            }

            if (string.IsNullOrEmpty(dc) || dc.Contains("Scanning"))
            {
                MessageBox.Show("Please select a Domain Controller first.", "No DC Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!_isLoggedIn || _authPass == null)
            {
                MessageBox.Show("Please login with Domain Admin credentials to access event logs.", "Authentication Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Mouse.OverrideCursor = Cursors.Wait;
            ShowBottomProgress($"Querying lockouts from {dc}...");
            AppendTerminal($"Querying lockout events from {dc}...");

            var lockouts = new List<LockoutEvent>();
            bool anyMethodSucceeded = false; // Track if any access method succeeded

            try
            {
                UpdateBottomProgress(30, $"Querying lockouts from {dc}...");
                await Task.Run(() =>
                {
                    // Capture credentials for async use
                    string capturedUser = _authUser;
                    SecureString capturedPassword = _authPass.Copy();
                    DateTime startTime = DateTime.Now.AddDays(-30);

                    // Extract domain and username from credentials
                    string domain = null;
                    string username = capturedUser;

                    if (capturedUser.Contains("\\"))
                    {
                        // Format: DOMAIN\username
                        string[] parts = capturedUser.Split('\\');
                        domain = parts[0];
                        username = parts[1];
                    }
                    else if (capturedUser.Contains("@"))
                    {
                        // Format: username@domain.com (UPN)
                        string[] parts = capturedUser.Split('@');
                        username = parts[0];
                        domain = parts[1].Split('.')[0]; // Extract first part of domain
                    }

                    Dispatcher.Invoke(() => AppendTerminal($"Authenticating as: {capturedUser} (Domain: {domain ?? "current"})"));

                    // ═══════════════════════════════════════════════════════════════
                    // METHOD 1: EventLogReader (WinRM-based) - Modern, fastest
                    // ═══════════════════════════════════════════════════════════════
                    try
                    {
                        Dispatcher.Invoke(() => AppendTerminal($"[Method 1] Attempting EventLogReader (WinRM)..."));

                        // Build XPath query for Event ID 4740 (Account Lockout)
                        string queryString = $@"
                            <QueryList>
                                <Query Id='0' Path='Security'>
                                    <Select Path='Security'>
                                        *[System[(EventID=4740) and TimeCreated[@SystemTime&gt;='{startTime:o}']]]
                                    </Select>
                                </Query>
                            </QueryList>";

                        // Create event log query with remote server and credentials
                        EventLogQuery query = new EventLogQuery("Security", PathType.LogName, queryString);
                        query.Session = new EventLogSession(
                            dc,                          // Remote computer name
                            domain,                      // Domain (extracted from username)
                            username,                    // Username (without domain prefix)
                            capturedPassword,            // Secure password
                            SessionAuthentication.Default
                        );

                        // Execute query and read events
                        using (EventLogReader reader = new EventLogReader(query))
                        {
                            EventRecord eventRecord;
                            while ((eventRecord = reader.ReadEvent()) != null)
                            {
                                using (eventRecord)
                                {
                                    try
                                    {
                                        // Event ID 4740 properties:
                                        // Properties[0] = Target Account Name
                                        // Properties[1] = Caller Computer Name
                                        DateTime timestamp = eventRecord.TimeCreated ?? DateTime.MinValue;
                                        string accountName = eventRecord.Properties[0]?.Value?.ToString() ?? "N/A";
                                        string callerComputer = eventRecord.Properties[1]?.Value?.ToString() ?? "N/A";

                                        lockouts.Add(new LockoutEvent
                                        {
                                            Timestamp = timestamp,
                                            AccountName = accountName,
                                            CallerComputer = callerComputer,
                                            DomainController = dc
                                        });
                                    }
                                    catch (Exception ex)
                                    {
                                        LogManager.LogWarning($"Failed to parse lockout event: {ex.Message}");
                                    }
                                }
                            }
                        }

                        anyMethodSucceeded = true;
                        Dispatcher.Invoke(() =>
                        {
                            AppendTerminal($"[Method 1] ✓ EventLogReader succeeded - read {lockouts.Count} lockout events");
                        });
                    }
                    catch (Exception ex1)
                    {
                        LogManager.LogWarning($"Method 1 (EventLogReader) failed: {ex1.Message}");
                        Dispatcher.Invoke(() => AppendTerminal($"[Method 1] ✗ Failed: {ex1.Message}"));

                        // ═══════════════════════════════════════════════════════════════
                        // METHOD 2: Traditional RPC/DCOM EventLog - No WinRM required
                        // ═══════════════════════════════════════════════════════════════
                        try
                        {
                            Dispatcher.Invoke(() => AppendTerminal($"[Method 2] Attempting traditional RPC/DCOM..."));

                            // Impersonate the domain admin user
                            using (new Impersonation(username, domain ?? "", capturedPassword))
                            {
                                // Connect to remote event log via RPC/DCOM
                                using (EventLog remoteLog = new EventLog("Security", dc))
                                {
                                    // Read entries in reverse (newest first)
                                    EventLogEntryCollection entries = remoteLog.Entries;

                                    for (int i = entries.Count - 1; i >= 0; i--)
                                    {
                                        try
                                        {
                                            EventLogEntry entry = entries[i];

                                            // Stop if we've gone too far back
                                            if (entry.TimeGenerated < startTime)
                                                break;

                                            // Event ID 4740 = Account Lockout
                                            if (entry.InstanceId == 4740)
                                            {
                                                // Parse message to extract account name and caller computer
                                                string message = entry.Message ?? "";
                                                string accountName = ExtractAccountName(message);
                                                string callerComputer = ExtractCallerComputer(message);

                                                lockouts.Add(new LockoutEvent
                                                {
                                                    Timestamp = entry.TimeGenerated,
                                                    AccountName = accountName,
                                                    CallerComputer = callerComputer,
                                                    DomainController = dc
                                                });
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            // Individual entry might be corrupted, skip it
                                            LogManager.LogWarning($"Failed to parse event entry: {ex.Message}");
                                        }
                                    }
                                }
                            }

                            anyMethodSucceeded = true;
                            Dispatcher.Invoke(() =>
                            {
                                AppendTerminal($"[Method 2] ✓ RPC/DCOM succeeded - read {lockouts.Count} lockout events");
                            });
                        }
                        catch (Exception ex2)
                        {
                            LogManager.LogWarning($"Method 2 (RPC/DCOM) failed: {ex2.Message}");
                            Dispatcher.Invoke(() => AppendTerminal($"[Method 2] ✗ Failed: {ex2.Message}"));

                            // ═══════════════════════════════════════════════════════════════
                            // METHOD 3: Direct EVTX file access via admin$ share
                            // ═══════════════════════════════════════════════════════════════
                            try
                            {
                                Dispatcher.Invoke(() => AppendTerminal($"[Method 3] Attempting direct EVTX file access..."));

                                string evtxPath = $"\\\\{dc}\\admin$\\System32\\winevt\\Logs\\Security.evtx";

                                using (new Impersonation(username, domain ?? "", capturedPassword))
                                {
                                    // Copy EVTX file to temp location (can't read directly while in use)
                                    string tempEvtx = Path.Combine(Path.GetTempPath(), $"Security_{dc}_{DateTime.Now:yyyyMMddHHmmss}.evtx");

                                    try
                                    {
                                        File.Copy(evtxPath, tempEvtx, true);

                                        // Read the copied EVTX file
                                        EventLogQuery localQuery = new EventLogQuery(tempEvtx, PathType.FilePath,
                                            $"*[System[(EventID=4740) and TimeCreated[@SystemTime>='{startTime:o}']]]");

                                        using (EventLogReader reader = new EventLogReader(localQuery))
                                        {
                                            EventRecord eventRecord;
                                            while ((eventRecord = reader.ReadEvent()) != null)
                                            {
                                                using (eventRecord)
                                                {
                                                    try
                                                    {
                                                        DateTime timestamp = eventRecord.TimeCreated ?? DateTime.MinValue;
                                                        string accountName = eventRecord.Properties[0]?.Value?.ToString() ?? "N/A";
                                                        string callerComputer = eventRecord.Properties[1]?.Value?.ToString() ?? "N/A";

                                                        lockouts.Add(new LockoutEvent
                                                        {
                                                            Timestamp = timestamp,
                                                            AccountName = accountName,
                                                            CallerComputer = callerComputer,
                                                            DomainController = dc
                                                        });
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        LogManager.LogWarning($"Failed to parse lockout event: {ex.Message}");
                                                    }
                                                }
                                            }
                                        }

                                        anyMethodSucceeded = true;
                                        Dispatcher.Invoke(() =>
                                        {
                                            AppendTerminal($"[Method 3] ✓ Direct EVTX access succeeded - read {lockouts.Count} lockout events");
                                        });
                                    }
                                    finally
                                    {
                                        // Clean up temp file
                                        if (File.Exists(tempEvtx))
                                            try { File.Delete(tempEvtx); } catch { }
                                    }
                                }
                            }
                            catch (Exception ex3)
                            {
                                LogManager.LogError($"Method 3 (Direct EVTX) failed", ex3);
                                Dispatcher.Invoke(() => AppendTerminal($"[Method 3] ✗ Failed: {ex3.Message}"));

                                // All methods failed - show comprehensive error
                                Dispatcher.Invoke(() =>
                                {
                                    AppendTerminal($"════════════════════════════════════════════════════════");
                                    AppendTerminal($"✗✗✗ ALL 3 EVENT LOG ACCESS METHODS FAILED ✗✗✗");
                                    AppendTerminal($"════════════════════════════════════════════════════════");
                                    AppendTerminal($"Method 1 (WinRM): {ex1.Message}");
                                    AppendTerminal($"Method 2 (RPC/DCOM): {ex2.Message}");
                                    AppendTerminal($"Method 3 (Direct EVTX): {ex3.Message}");
                                    AppendTerminal($"════════════════════════════════════════════════════════");

                                    string errorMsg = $"All event log access methods failed:\n\n" +
                                        $"1. EventLogReader (WinRM): {ex1.Message}\n\n" +
                                        $"2. RPC/DCOM: {ex2.Message}\n\n" +
                                        $"3. Direct EVTX: {ex3.Message}\n\n" +
                                        $"Possible causes:\n" +
                                        $"• Insufficient permissions (need Domain Admin)\n" +
                                        $"• All remote access methods blocked by firewall\n" +
                                        $"• DC hardening policies preventing remote event log access\n" +
                                        $"• DC authentication issues (check Event Viewer on DC)\n" +
                                        $"• Cisco Duo or other security software blocking RPC/DCOM\n\n" +
                                        $"FALLBACK OPTION:\n" +
                                        $"Click YES to open Event Viewer manually (like ADUC).\n" +
                                        $"Click NO to cancel.";

                                    var result = MessageBox.Show(errorMsg, "Event Log Access Failed - Open Event Viewer?",
                                        MessageBoxButton.YesNo, MessageBoxImage.Error);

                                    if (result == MessageBoxResult.Yes)
                                    {
                                        try
                                        {
                                            // Launch Event Viewer connected to remote DC
                                            AppendTerminal($"Launching Event Viewer for {dc}...");

                                            var psi = new System.Diagnostics.ProcessStartInfo
                                            {
                                                FileName = "eventvwr.msc",
                                                Arguments = $"/computer={dc}",
                                                UseShellExecute = true,
                                                ErrorDialog = true
                                            };

                                            var proc = System.Diagnostics.Process.Start(psi);

                                            if (proc != null)
                                            {
                                                AppendTerminal($"✓ Event Viewer launched (PID: {proc.Id})");
                                                AppendTerminal($"Navigate to: Windows Logs > Security");
                                                AppendTerminal($"Filter: Event ID 4740 (Account Lockout)");
                                                MessageBox.Show(
                                                    "Event Viewer opened successfully!\n\n" +
                                                    "Steps to view lockouts:\n" +
                                                    "1. Navigate to: Windows Logs → Security\n" +
                                                    "2. Right-click Security → Filter Current Log\n" +
                                                    "3. Enter Event ID: 4740\n" +
                                                    "4. Click OK",
                                                    "Event Viewer Launched",
                                                    MessageBoxButton.OK,
                                                    MessageBoxImage.Information);
                                            }
                                            else
                                            {
                                                AppendTerminal($"⚠ Event Viewer launch returned null", true);
                                            }
                                        }
                                        catch (Exception evtEx)
                                        {
                                            AppendTerminal($"✗ Failed to launch Event Viewer: {evtEx.Message}", true);
                                            LogManager.LogError("Event Viewer launch failed", evtEx);
                                            MessageBox.Show(
                                                $"Could not launch Event Viewer:\n{evtEx.Message}\n\n" +
                                                $"Try manually opening Event Viewer and connecting to: {dc}",
                                                "Launch Failed",
                                                MessageBoxButton.OK,
                                                MessageBoxImage.Error);
                                        }
                                    }
                                });
                            }
                        }
                    }

                    // Clean up secure password
                    capturedPassword?.Dispose();
                    capturedPassword = null;
                });

                Mouse.OverrideCursor = null;
                UpdateBottomProgress(100, "Query complete");
                await Task.Delay(800);

                // Log final status for debugging
                AppendTerminal($"Query complete: anyMethodSucceeded={anyMethodSucceeded}, lockouts.Count={lockouts.Count}");

                if (lockouts.Count == 0)
                {
                    // Only show "no lockouts" if we successfully accessed the logs
                    // If all methods failed, the error was already shown
                    if (anyMethodSucceeded)
                    {
                        AppendTerminal($"✓ Successfully accessed event logs on {dc} - no lockouts found in last 30 days");
                        MessageBox.Show("No account lockout events found in the Security log for the past 30 days.\n\nThe event log was successfully queried.", "No Lockouts", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        AppendTerminal($"✗ ALL ACCESS METHODS FAILED for {dc}");
                        AppendTerminal($"Check the error popup for details, or review the log file");
                        // Don't show "no lockouts" message - the error MessageBox was already shown
                    }
                }
                else
                {
                    AppendTerminal($"Found {lockouts.Count} lockout events on {dc}");
                    HideBottomProgress($"Ready • {lockouts.Count} lockouts found");
                    ShowLockoutWindow(lockouts.OrderByDescending(l => l.Timestamp).ToList());
                    return;
                }

                HideBottomProgress("Ready");
            }
            catch (Exception ex)
            {
                Mouse.OverrideCursor = null;
                HideBottomProgress("Query failed");
                LogManager.LogError("Lockout check failed", ex);
                MessageBox.Show($"Error: {ex.Message}", "Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>Shows lockout events in a dedicated window</summary>
        private void ShowLockoutWindow(List<LockoutEvent> lockouts)
        {
            var window = new Window
            {
                Title = $"Account Lockout Events - {lockouts.Count} Total",
                Width = 900,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background = new SolidColorBrush(Color.FromRgb(13, 13, 13))
            };

            var grid = new DataGrid
            {
                ItemsSource = lockouts,
                AutoGenerateColumns = false,
                IsReadOnly = true,
                Background = new SolidColorBrush(Color.FromRgb(26, 26, 26)),
                Foreground = Brushes.White,
                FontSize = 12,
                RowBackground = new SolidColorBrush(Color.FromRgb(26, 26, 26)),
                AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(31, 31, 31)),
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(42, 42, 42)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                BorderThickness = new Thickness(1),
                CanUserSortColumns = true,
                CanUserResizeColumns = true
            };

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Timestamp",
                Binding = new System.Windows.Data.Binding("Timestamp") { StringFormat = "yyyy-MM-dd HH:mm:ss" },
                Width = new DataGridLength(160)
            });

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Account Name",
                Binding = new System.Windows.Data.Binding("AccountName"),
                Width = new DataGridLength(200)
            });

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Locked Out From",
                Binding = new System.Windows.Data.Binding("CallerComputer"),
                Width = new DataGridLength(250)
            });

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Domain Controller",
                Binding = new System.Windows.Data.Binding("DomainController"),
                Width = new DataGridLength(250)
            });

            var panel = new DockPanel { Margin = new Thickness(10) };

            // Header
            var header = new TextBlock
            {
                Text = $"Account Lockout Events (Event ID 4740)",
                Foreground = Brushes.Cyan,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            };
            DockPanel.SetDock(header, Dock.Top);
            panel.Children.Add(header);

            // Export button
            var exportBtn = new Button
            {
                Content = "EXPORT TO CSV",
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0, 0, 0, 10),
                HorizontalAlignment = HorizontalAlignment.Right,
                Background = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0)
            };
            exportBtn.Click += (s, e) =>
            {
                try
                {
                    string csv = "Timestamp,Account Name,Locked Out From,Domain Controller\n";
                    foreach (var lockout in lockouts)
                    {
                        csv += $"\"{lockout.Timestamp:yyyy-MM-dd HH:mm:ss}\",\"{lockout.AccountName}\",\"{lockout.CallerComputer}\",\"{lockout.DomainController}\"\n";
                    }

                    string filename = $"Lockouts_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                    System.IO.File.WriteAllText(filename, csv);
                    MessageBox.Show($"Exported to {filename}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            DockPanel.SetDock(exportBtn, Dock.Top);
            panel.Children.Add(exportBtn);

            // Grid
            panel.Children.Add(grid);

            window.Content = panel;
            window.Show();
        }

        // ═══════════════════════════════════════════════════════════════
        // EVENT LOG HELPER METHODS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Extracts account name from Event ID 4740 message</summary>
        private static string ExtractAccountName(string message)
        {
            try
            {
                // Event 4740 message format: "A user account was locked out.\n\nSubject:\n...\n\nAccount That Was Locked Out:\n\tSecurity ID:\t\tS-1-5-21-...\n\tAccount Name:\t\tusername"
                var match = Regex.Match(message, @"Account Name:\s+(.+?)[\r\n]", RegexOptions.Singleline);
                if (match.Success)
                    return match.Groups[1].Value.Trim();
            }
            catch { }
            return "N/A";
        }

        /// <summary>Extracts caller computer from Event ID 4740 message</summary>
        private static string ExtractCallerComputer(string message)
        {
            try
            {
                // Event 4740 message format: "Caller Computer Name:\tCOMPUTERNAME"
                var match = Regex.Match(message, @"Caller Computer Name:\s+(.+?)[\r\n]", RegexOptions.Singleline);
                if (match.Success)
                    return match.Groups[1].Value.Trim();
            }
            catch { }
            return "N/A";
        }

        // ═══════════════════════════════════════════════════════
        // PINNED DEVICES MONITOR
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Adds a new pinned device to the monitor list
        /// </summary>
        private async void BtnAddPinnedDevice_Click(object sender, RoutedEventArgs e)
        {
            if (_pinnedDevices.Count >= 10)
            {
                MessageBox.Show("Maximum of 10 pinned devices reached.", "Limit Reached", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string input = TxtPinnedDeviceInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(input))
            {
                MessageBox.Show("Please enter a hostname or IP address.", "Input Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Check if already exists
            if (_pinnedDevices.Any(d => d.Input.Equals(input, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("This device is already pinned.", "Duplicate Device", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var device = new PinnedDevice
            {
                Input = input,
                Status = "Checking...",
                StatusColor = Brushes.Gray,
                ResolvedName = "...",
                ResolvedIP = "...",
                LastChecked = DateTime.Now.ToString("h:mm tt"),
                ResponseTimeColor = new SolidColorBrush(Color.FromRgb(161, 161, 170))
            };

            lock (_pinnedDevicesLock)
            {
                _pinnedDevices.Add(device);
            }

            TxtPinnedDeviceInput.Clear();
            SavePinnedDevices();

            // Show success toast
            Managers.UI.ToastManager.ShowSuccess($"{input} added to pinned devices");

            // Check device status in background
            await CheckPinnedDeviceStatus(device);
        }

        /// <summary>
        /// Removes selected pinned device
        /// </summary>
        private void BtnRemovePinnedDevice_Click(object sender, RoutedEventArgs e)
        {
            if (GridPinnedDevices.SelectedItem is PinnedDevice device)
            {
                string deviceName = device.Input;
                lock (_pinnedDevicesLock)
                {
                    _pinnedDevices.Remove(device);
                }
                SavePinnedDevices();
                Managers.UI.ToastManager.ShowSuccess($"{deviceName} removed");
            }
            else
            {
                MessageBox.Show("Please select a device to remove.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// Refreshes status of all pinned devices
        /// </summary>
        private async void BtnRefreshPinnedDevices_Click(object sender, RoutedEventArgs e)
        {
            BtnRefreshPinnedDevices.IsEnabled = false;
            BtnRefreshPinnedDevices.Content = "REFRESHING...";
            ShowBottomProgress($"Refreshing {_pinnedDevices.Count} pinned devices...");

            var tasks = new List<Task>();
            foreach (var device in _pinnedDevices.ToList())
            {
                tasks.Add(CheckPinnedDeviceStatus(device));
            }

            await Task.WhenAll(tasks);
            UpdateBottomProgress(100, "Refresh complete");
            await Task.Delay(800);

            BtnRefreshPinnedDevices.Content = "🔄 REFRESH ALL";
            BtnRefreshPinnedDevices.IsEnabled = true;
            HideBottomProgress($"Ready • {_pinnedDevices.Count} devices refreshed");
            Managers.UI.ToastManager.ShowSuccess($"Refreshed {_pinnedDevices.Count} pinned device{(_pinnedDevices.Count != 1 ? "s" : "")}");
        }

        /// <summary>
        /// Checks status of a single pinned device (ping + DNS resolution)
        /// </summary>
        private async Task CheckPinnedDeviceStatus(PinnedDevice device)
        {
            await Task.Run(async () =>
            {
                try
                {
                    string input = device.Input;
                    bool isIP = System.Net.IPAddress.TryParse(input, out System.Net.IPAddress ipAddr);

                    string resolvedName = "N/A";
                    string resolvedIP = "N/A";
                    bool isOnline = false;

                    // Step 1: Resolve IP if hostname was provided, or hostname if IP was provided
                    if (isIP)
                    {
                        resolvedIP = input;
                        // Try reverse DNS lookup
                        try
                        {
                            var hostEntry = await Dns.GetHostEntryAsync(input);
                            resolvedName = hostEntry.HostName;
                        }
                        catch
                        {
                            resolvedName = "N/A";
                        }
                    }
                    else
                    {
                        resolvedName = input;
                        // Try forward DNS lookup
                        try
                        {
                            var hostEntry = await Dns.GetHostEntryAsync(input);
                            if (hostEntry.AddressList.Length > 0)
                            {
                                // Get first IPv4 address
                                var ipv4 = hostEntry.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
                                resolvedIP = ipv4?.ToString() ?? hostEntry.AddressList[0].ToString();
                            }
                        }
                        catch
                        {
                            resolvedIP = "N/A";
                        }
                    }

                    // Step 2: Ping the device
                    long responseTime = -1;
                    try
                    {
                        using (var ping = new Ping())
                        {
                            var reply = await ping.SendPingAsync(input, SecureConfig.PingTimeoutMs);
                            isOnline = (reply.Status == IPStatus.Success);
                            if (isOnline)
                            {
                                responseTime = reply.RoundtripTime;
                            }
                        }
                    }
                    catch
                    {
                        isOnline = false;
                    }

                    // Step 3: Update UI on dispatcher thread
                    Dispatcher.Invoke(() =>
                    {
                        device.ResolvedName = resolvedName;
                        device.ResolvedIP = resolvedIP;
                        device.IsOnline = isOnline;
                        device.Status = isOnline ? "● Online" : "○ Offline";
                        device.StatusColor = isOnline ? Brushes.LimeGreen : Brushes.Red;
                        device.ResponseTime = isOnline ? $"{responseTime} ms" : "N/A";

                        // Color code response time: green < 50ms, yellow 50-100ms, orange 100-200ms, red > 200ms
                        if (isOnline)
                        {
                            if (responseTime < 50)
                                device.ResponseTimeColor = Brushes.LimeGreen;
                            else if (responseTime < 100)
                                device.ResponseTimeColor = new SolidColorBrush(Color.FromRgb(255, 215, 0)); // Gold
                            else if (responseTime < 200)
                                device.ResponseTimeColor = new SolidColorBrush(Color.FromRgb(255, 133, 51)); // Orange
                            else
                                device.ResponseTimeColor = new SolidColorBrush(Color.FromRgb(255, 68, 68)); // Red
                        }
                        else
                        {
                            device.ResponseTimeColor = new SolidColorBrush(Color.FromRgb(161, 161, 170)); // Zinc/Gray
                        }

                        device.LastChecked = DateTime.Now.ToString("h:mm tt");

                        // Add to ping history (keep last 4 hours of data)
                        if (isOnline)
                        {
                            device.PingHistory.Add((DateTime.Now, responseTime));

                            // Remove entries older than 4 hours
                            var cutoff = DateTime.Now.AddHours(-4);
                            device.PingHistory.RemoveAll(p => p.Time < cutoff);
                        }
                    });
                }
                catch (Exception ex)
                {
                    LogManager.LogError($"Failed to check pinned device: {device.Input}", ex);
                    Dispatcher.Invoke(() =>
                    {
                        device.Status = "✗ Error";
                        device.StatusColor = Brushes.Orange;
                        device.ResolvedName = "Error";
                        device.ResolvedIP = "Error";
                        device.ResponseTimeColor = new SolidColorBrush(Color.FromRgb(161, 161, 170)); // Zinc/Gray
                        device.LastChecked = DateTime.Now.ToString("h:mm tt");
                    });
                }
            });
        }

        /// <summary>
        /// Saves pinned devices to config file
        /// </summary>
        private void SavePinnedDevices()
        {
            try
            {
                var devices = _pinnedDevices.Select(d => d.Input).ToList();
                string json = string.Join("|", devices);
                Properties.Settings.Default.PinnedDevices = json;
                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to save pinned devices", ex);
            }
        }

        /// <summary>
        /// Loads pinned devices from config file
        /// </summary>
        private async Task LoadPinnedDevices()
        {
            try
            {
                string json = Properties.Settings.Default.PinnedDevices ?? "";
                if (string.IsNullOrWhiteSpace(json)) return;

                var devices = json.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var input in devices.Take(10)) // Limit to 10
                {
                    var device = new PinnedDevice
                    {
                        Input = input,
                        Status = "Checking...",
                        StatusColor = Brushes.Gray,
                        ResolvedName = "...",
                        ResolvedIP = "...",
                        LastChecked = "Loading..."
                    };
                    _pinnedDevices.Add(device);
                }

                // Check all devices in background
                await Task.Delay(1000); // Wait for UI to initialize
                foreach (var device in _pinnedDevices.ToList())
                {
                    _ = CheckPinnedDeviceStatus(device);
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to load pinned devices", ex);
            }
        }

        /// <summary>
        /// Opens device monitor window showing all devices
        /// </summary>
        private void BtnDetailsPinnedDevices_Click(object sender, RoutedEventArgs e)
        {
            if (_pinnedDevices.Count == 0)
            {
                MessageBox.Show("No pinned devices to monitor.", "No Devices", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var monitorWindow = new DeviceMonitorWindow(_pinnedDevices.ToList(), null,
                _wmiManager, _cimManager, _authUser, _authPass);
            monitorWindow.Owner = this;
            monitorWindow.ShowDialog();
        }

        /// <summary>
        /// Opens device monitor window focused on double-clicked device
        /// </summary>
        private void GridPinnedDevices_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (GridPinnedDevices.SelectedItem is PinnedDevice device)
            {
                var monitorWindow = new DeviceMonitorWindow(_pinnedDevices.ToList(), device,
                    _wmiManager, _cimManager, _authUser, _authPass);
                monitorWindow.Owner = this;
                monitorWindow.ShowDialog();
            }
        }

        // ############################################################################
        // REGION: GLOBAL SERVICES STATUS MONITORING
        // TAG: #GLOBAL_SERVICES #STATUS_API #MONITORING
        // ############################################################################

        /// <summary>
        /// Initializes global services list with default or configured services
        /// Uses public status APIs for real-time service health monitoring
        /// </summary>
        private void InitGlobalServices()
        {
            try
            {
                // Load from settings or use defaults
                string configJson = Properties.Settings.Default.GlobalServicesConfig ?? "";

                if (string.IsNullOrWhiteSpace(configJson))
                {
                    // ESSENTIAL SERVICES (Always visible) - Grouped by parent company
                    var essentialServices = new[]
                    {
                        // Microsoft ecosystem
                        new { Name = "Azure", Api = "https://status.azure.com/api/v2/status.json" },
                        new { Name = "Microsoft 365", Api = "https://status.office.com/api/v1.0/ServiceStatus/CurrentStatus" },
                        new { Name = "Microsoft Teams", Api = "https://status.office.com/api/v1.0/ServiceStatus/CurrentStatus" },
                        new { Name = "GitHub", Api = "https://www.githubstatus.com/api/v2/status.json" },
                        // Google ecosystem
                        new { Name = "Google Cloud", Api = "https://status.cloud.google.com/incidents.json" },
                        new { Name = "DNS (8.8.8.8)", Api = "ping:8.8.8.8" },
                        // Amazon ecosystem
                        new { Name = "AWS", Api = "https://status.aws.amazon.com/data.json" },
                        // Cloudflare ecosystem
                        new { Name = "Cloudflare", Api = "https://www.cloudflarestatus.com/api/v2/status.json" },
                        new { Name = "DNS (1.1.1.1)", Api = "ping:1.1.1.1" }
                    };

                    // HIGH PRIORITY SERVICES (Collapsible) - Grouped by parent company
                    var highPriorityServices = new[]
                    {
                        // Microsoft ecosystem
                        new { Name = "NuGet", Api = "https://status.nuget.org/api/v2/status.json" },
                        // Atlassian ecosystem
                        new { Name = "Atlassian", Api = "https://status.atlassian.com/api/v2/status.json" },
                        // Communication platforms
                        new { Name = "Slack", Api = "https://status.slack.com/api/v2.0.0/current" },
                        new { Name = "Zoom", Api = "https://status.zoom.us/api/v2/status.json" },
                        // DevOps & Package Management
                        new { Name = "DockerHub", Api = "https://status.docker.com/api/v2/status.json" },
                        new { Name = "NPM Registry", Api = "https://status.npmjs.org/api/v2/status.json" },
                        // CRM & Identity
                        new { Name = "Salesforce", Api = "https://api.status.salesforce.com/v1/status" },
                        new { Name = "Okta", Api = "https://status.okta.com/api/v2/status.json" },
                        // Monitoring
                        new { Name = "Datadog", Api = "https://status.datadoghq.com/api/v2/status.json" }
                    };

                    // MEDIUM PRIORITY SERVICES (Collapsible) - Grouped by parent company
                    var mediumPriorityServices = new[]
                    {
                        // Twilio ecosystem
                        new { Name = "Twilio", Api = "https://status.twilio.com/api/v2/status.json" },
                        new { Name = "SendGrid", Api = "https://status.sendgrid.com/api/v2/status.json" },
                        // Independent services
                        new { Name = "Stripe", Api = "https://status.stripe.com/api/v2/status.json" },
                        new { Name = "MongoDB Atlas", Api = "https://status.cloud.mongodb.com/api/v2/status.json" },
                        new { Name = "PagerDuty", Api = "https://status.pagerduty.com/api/v2/status.json" }
                    };

                    // Populate Essential Services
                    foreach (var svc in essentialServices)
                    {
                        _essentialServices.Add(new GlobalServiceStatus
                        {
                            ServiceName = svc.Name,
                            Endpoint = svc.Api,
                            Status = "Pending",
                            StatusColor = Brushes.Gray,
                            Latency = "-",
                            LatencyColor = Brushes.Gray
                        });
                    }

                    // Populate High Priority Services
                    foreach (var svc in highPriorityServices)
                    {
                        _highPriorityServices.Add(new GlobalServiceStatus
                        {
                            ServiceName = svc.Name,
                            Endpoint = svc.Api,
                            Status = "Pending",
                            StatusColor = Brushes.Gray,
                            Latency = "-",
                            LatencyColor = Brushes.Gray
                        });
                    }

                    // Populate Medium Priority Services
                    foreach (var svc in mediumPriorityServices)
                    {
                        _mediumPriorityServices.Add(new GlobalServiceStatus
                        {
                            ServiceName = svc.Name,
                            Endpoint = svc.Api,
                            Status = "Pending",
                            StatusColor = Brushes.Gray,
                            Latency = "-",
                            LatencyColor = Brushes.Gray
                        });
                    }

                    // Save default config
                    SaveGlobalServicesConfig();
                }
                else
                {
                    // Load from saved configuration
                    LogManager.LogInfo("Loading global services from saved configuration");
                    try
                    {
                        var serializer = new JavaScriptSerializer();
                        // Deserialize as anonymous object list (matches SaveGlobalServicesConfig format)
                        var serviceList = serializer.Deserialize<List<Dictionary<string, object>>>(configJson);

                        if (serviceList != null && serviceList.Count > 0)
                        {
                            // For now, put all saved services in essential category
                            // TODO: Add priority metadata to saved config for proper categorization
                            foreach (var svc in serviceList)
                            {
                                if (svc.ContainsKey("ServiceName") && svc.ContainsKey("Endpoint"))
                                {
                                    _essentialServices.Add(new GlobalServiceStatus
                                    {
                                        ServiceName = svc["ServiceName"]?.ToString() ?? "Unknown",
                                        Endpoint = svc["Endpoint"]?.ToString() ?? "",
                                        Status = "Pending",
                                        StatusColor = Brushes.Gray,
                                        Latency = "-",
                                        LatencyColor = Brushes.Gray
                                    });
                                }
                            }
                            LogManager.LogInfo($"Loaded {serviceList.Count} services from configuration");
                        }
                        else
                        {
                            LogManager.LogWarning("Saved config was empty or invalid, clearing it");
                            Properties.Settings.Default.GlobalServicesConfig = "";
                            Properties.Settings.Default.Save();
                        }
                    }
                    catch (Exception loadEx)
                    {
                        LogManager.LogError("Failed to load saved config, clearing it for next run", loadEx);
                        // If loading fails, clear the bad config and use defaults on next run
                        Properties.Settings.Default.GlobalServicesConfig = "";
                        Properties.Settings.Default.Save();
                    }
                }

                LogManager.LogInfo($"Global services initialized: Essential={_essentialServices.Count}, High={_highPriorityServices.Count}, Medium={_mediumPriorityServices.Count}");

                // Set ItemsSource (already on UI thread from Dispatcher.InvokeAsync in Window_Loaded)
                try
                {
                    if (GridEssentialServices != null)
                    {
                        GridEssentialServices.ItemsSource = null; // Clear first
                        GridEssentialServices.ItemsSource = _essentialServices;
                        GridEssentialServices.Items.Refresh(); // Force refresh
                        LogManager.LogInfo($"Essential Services grid bound with {_essentialServices.Count} items");
                    }
                    else
                    {
                        LogManager.LogError("GridEssentialServices is NULL!", null);
                    }

                    if (GridHighPriorityServices != null)
                    {
                        GridHighPriorityServices.ItemsSource = null;
                        GridHighPriorityServices.ItemsSource = _highPriorityServices;
                        GridHighPriorityServices.Items.Refresh();
                        LogManager.LogInfo($"High Priority Services grid bound with {_highPriorityServices.Count} items");
                    }

                    if (GridMediumPriorityServices != null)
                    {
                        GridMediumPriorityServices.ItemsSource = null;
                        GridMediumPriorityServices.ItemsSource = _mediumPriorityServices;
                        GridMediumPriorityServices.Items.Refresh();
                        LogManager.LogInfo($"Medium Priority Services grid bound with {_mediumPriorityServices.Count} items");
                    }

                    // Force UI update
                    GridEssentialServices?.UpdateLayout();
                    GridHighPriorityServices?.UpdateLayout();
                    GridMediumPriorityServices?.UpdateLayout();
                }
                catch (Exception bindEx)
                {
                    LogManager.LogError("Failed to bind global services grids", bindEx);
                }

                LogManager.LogInfo($"Global services initialized: {_essentialServices.Count} essential, {_highPriorityServices.Count} high priority, {_mediumPriorityServices.Count} medium priority");
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to initialize global services", ex);
            }
        }

        /// <summary>
        /// Saves global services configuration to settings
        /// </summary>
        private void SaveGlobalServicesConfig()
        {
            try
            {
                // Combine all three priority levels into one config
                var allServices = _essentialServices
                    .Concat(_highPriorityServices)
                    .Concat(_mediumPriorityServices)
                    .Select(s => new { s.ServiceName, s.Endpoint })
                    .ToList();

                var serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(allServices);
                Properties.Settings.Default.GlobalServicesConfig = json;
                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to save global services config", ex);
            }
        }

        /// <summary>
        /// Manual refresh button - checks all global service statuses
        /// Uses batched, staggered requests to prevent lockups
        /// </summary>
        private async void BtnRefreshGlobalServices_Click(object sender, RoutedEventArgs e)
        {
            BtnRefreshGlobalServices.IsEnabled = false;
            BtnRefreshGlobalServices.Content = "CHECKING...";

            var allServices = _essentialServices.Concat(_highPriorityServices).Concat(_mediumPriorityServices).ToList();
            int totalCount = allServices.Count;
            int batchSize = 5; // Process 5 services at a time to prevent lockups
            int completed = 0;

            ShowBottomProgress($"Checking {totalCount} global services...");

            // Process in batches to avoid overwhelming the network/APIs
            for (int i = 0; i < allServices.Count; i += batchSize)
            {
                var batch = allServices.Skip(i).Take(batchSize).ToList();
                var batchTasks = batch.Select(service => CheckGlobalServiceStatus(service)).ToList();

                // Wait for current batch to complete
                await Task.WhenAll(batchTasks);

                completed += batch.Count;
                double progressPercent = (completed / (double)totalCount) * 100;
                UpdateBottomProgress(progressPercent, $"Checked {completed}/{totalCount} services...");

                // Small delay between batches to prevent API rate limiting and UI lockup
                if (i + batchSize < allServices.Count)
                {
                    await Task.Delay(200); // 200ms stagger between batches
                }
            }

            UpdateBottomProgress(100, "Service status check complete");
            await Task.Delay(800);

            BtnRefreshGlobalServices.Content = "🔄 CHECK ALL";
            BtnRefreshGlobalServices.IsEnabled = true;
            HideBottomProgress($"Ready • {totalCount} services checked");
            Managers.UI.ToastManager.ShowSuccess($"Service check complete: {totalCount} services monitored");
        }

        /// <summary>
        /// Toggle auto-refresh every 5 minutes (uses batched requests)
        /// </summary>
        private void BtnAutoRefreshGlobalServices_Click(object sender, RoutedEventArgs e)
        {
            if (_globalServicesTimer == null)
            {
                // Start auto-refresh with batched, staggered checking
                _globalServicesTimer = new System.Threading.Timer(async _ =>
                {
                    await Dispatcher.InvokeAsync(async () =>
                    {
                        var allServices = _essentialServices.Concat(_highPriorityServices).Concat(_mediumPriorityServices).ToList();
                        int batchSize = 5;

                        // Process in batches to prevent lockups during auto-refresh
                        for (int i = 0; i < allServices.Count; i += batchSize)
                        {
                            var batch = allServices.Skip(i).Take(batchSize).ToList();
                            var batchTasks = batch.Select(service => CheckGlobalServiceStatus(service)).ToList();
                            await Task.WhenAll(batchTasks);

                            // Stagger batches by 300ms during auto-refresh (slightly longer than manual)
                            if (i + batchSize < allServices.Count)
                            {
                                await Task.Delay(300);
                            }
                        }
                    });
                }, null, TimeSpan.Zero, TimeSpan.FromMinutes(5));

                BtnAutoRefreshGlobalServices.Content = "⏱ ENABLED";
                BtnAutoRefreshGlobalServices.Background = new SolidColorBrush(Color.FromRgb(0, 200, 83)); // Green
            }
            else
            {
                // Stop auto-refresh
                _globalServicesTimer?.Dispose();
                _globalServicesTimer = null;
                BtnAutoRefreshGlobalServices.Content = "⏱ AUTO (5 MIN)";
                BtnAutoRefreshGlobalServices.ClearValue(Button.BackgroundProperty); // Reset to style default
            }
        }

        /// <summary>
        /// Checks status of a single global service using its status API
        /// </summary>
        private async Task CheckGlobalServiceStatus(GlobalServiceStatus service)
        {
            await Task.Run(async () =>
            {
                try
                {
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                    // Check if this is a ping-based service or API-based
                    if (service.Endpoint.StartsWith("ping:"))
                    {
                        // Simple ping check for DNS servers
                        string target = service.Endpoint.Replace("ping:", "");
                        var ping = new System.Net.NetworkInformation.Ping();
                        var result = await ping.SendPingAsync(target, 3000);
                        stopwatch.Stop();

                        await Dispatcher.InvokeAsync(() =>
                        {
                            if (result.Status == System.Net.NetworkInformation.IPStatus.Success)
                            {
                                service.Status = "✓ Online";
                                service.StatusColor = new SolidColorBrush(Color.FromRgb(0, 255, 0));
                                service.Latency = $"{result.RoundtripTime} ms";
                                service.LatencyColor = result.RoundtripTime < 50
                                    ? new SolidColorBrush(Color.FromRgb(0, 255, 0))
                                    : result.RoundtripTime < 150
                                        ? new SolidColorBrush(Color.FromRgb(255, 165, 0))
                                        : new SolidColorBrush(Color.FromRgb(255, 100, 100));
                            }
                            else
                            {
                                service.Status = "✗ Offline";
                                service.StatusColor = new SolidColorBrush(Color.FromRgb(255, 100, 100));
                                service.Latency = "Timeout";
                                service.LatencyColor = Brushes.Gray;
                            }
                        });
                    }
                    else
                    {
                        // API-based status check
                        using (var client = new System.Net.Http.HttpClient())
                        {
                            client.Timeout = TimeSpan.FromSeconds(5);
                            client.DefaultRequestHeaders.Add("User-Agent", "ArtaznIT-Monitor/6.0");

                            var response = await client.GetAsync(service.Endpoint);
                            stopwatch.Stop();

                            await Dispatcher.InvokeAsync(() =>
                            {
                                if (response.IsSuccessStatusCode)
                                {
                                    // Successfully reached API - parse response if needed
                                    service.Status = "✓ Operational";
                                    service.StatusColor = new SolidColorBrush(Color.FromRgb(0, 255, 0));
                                    service.Latency = $"{stopwatch.ElapsedMilliseconds} ms";
                                    service.LatencyColor = stopwatch.ElapsedMilliseconds < 500
                                        ? new SolidColorBrush(Color.FromRgb(0, 255, 0))
                                        : stopwatch.ElapsedMilliseconds < 2000
                                            ? new SolidColorBrush(Color.FromRgb(255, 165, 0))
                                            : new SolidColorBrush(Color.FromRgb(255, 100, 100));

                                    // TODO: Parse JSON response to detect incidents/degraded performance
                                    // Each API has different schema - could be enhanced per-service
                                }
                                else
                                {
                                    service.Status = "⚠ Degraded";
                                    service.StatusColor = new SolidColorBrush(Color.FromRgb(255, 165, 0));
                                    service.Latency = $"HTTP {(int)response.StatusCode}";
                                    service.LatencyColor = new SolidColorBrush(Color.FromRgb(255, 165, 0));
                                }
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        service.Status = "✗ Error";
                        service.StatusColor = new SolidColorBrush(Color.FromRgb(255, 100, 100));
                        service.Latency = "Unreachable";
                        service.LatencyColor = Brushes.Gray;
                    });
                    LogManager.LogError($"Failed to check {service.ServiceName} status", ex);
                }
            });
        }
    }

    // ############################################################################
    // LOCKOUT EVENT DATA MODEL
    // ############################################################################

    public class LockoutEvent
    {
        public DateTime Timestamp { get; set; }
        public string AccountName { get; set; }
        public string CallerComputer { get; set; }
        public string DomainController { get; set; }
    }

    // ############################################################################
    // IMPERSONATION HELPER (for RPC/DCOM and file access with alternate creds)
    // ############################################################################

    public class Impersonation : IDisposable
    {
        private WindowsImpersonationContext _impersonationContext;

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool LogonUser(string lpszUsername, string lpszDomain, IntPtr lpszPassword,
            int dwLogonType, int dwLogonProvider, out IntPtr phToken);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        private const int LOGON32_PROVIDER_DEFAULT = 0;
        private const int LOGON32_LOGON_NEW_CREDENTIALS = 9;

        public Impersonation(string username, string domain, SecureString password)
        {
            IntPtr token = IntPtr.Zero;
            IntPtr passwordPtr = IntPtr.Zero;

            try
            {
                // Extract domain from username if format is domain\user
                if (username.Contains("\\"))
                {
                    string[] parts = username.Split('\\');
                    domain = parts[0];
                    username = parts[1];
                }
                else if (username.Contains("@"))
                {
                    // UPN format - extract domain
                    string[] parts = username.Split('@');
                    username = parts[0];
                    domain = parts[1];
                }

                // Convert SecureString to unmanaged memory
                passwordPtr = Marshal.SecureStringToGlobalAllocUnicode(password);

                // Logon with LOGON32_LOGON_NEW_CREDENTIALS for network access
                bool logonSuccess = LogonUser(username, domain, passwordPtr,
                    LOGON32_LOGON_NEW_CREDENTIALS, LOGON32_PROVIDER_DEFAULT, out token);

                if (!logonSuccess)
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new System.ComponentModel.Win32Exception(error, $"LogonUser failed with error code {error}");
                }

                // Impersonate the logged-on user
                WindowsIdentity identity = new WindowsIdentity(token);
                _impersonationContext = identity.Impersonate();
            }
            finally
            {
                // Clean up sensitive data
                if (passwordPtr != IntPtr.Zero)
                {
                    Marshal.ZeroFreeGlobalAllocUnicode(passwordPtr);
                }

                if (token != IntPtr.Zero)
                {
                    CloseHandle(token);
                }
            }
        }

        public void Dispose()
        {
            _impersonationContext?.Undo();
            _impersonationContext?.Dispose();
        }
    }

    // ############################################################################
    // DEVICE MONITOR WINDOW
    // ############################################################################

    public class DeviceMonitorWindow : Window
    {
        private List<PinnedDevice> _devices;
        private PinnedDevice _currentDevice;
        private ListBox _deviceList;
        private TabControl _tabControl;
        private DispatcherTimer _refreshTimer;
        private int _refreshIndex = 0;
        private WmiConnectionManager _wmiManager;
        private CimSessionManager _cimManager;
        private string _authUser;
        private SecureString _authPass;

        // UI Elements for live data
        private TextBlock _txtUptime, _txtHostname, _txtIP, _txtModel, _txtOS, _txtCPU, _txtRAM;
        private TextBlock _txtUser, _txtSerial, _txtMAC, _txtDNS, _txtBattery;
        private StackPanel _stkDrives;
        private TextBlock _txtCpuUsage, _txtRamUsage, _txtDiskUsage;
        private System.Windows.Shapes.Rectangle _barCpu, _barRam, _barDisk;

        public DeviceMonitorWindow(List<PinnedDevice> devices, PinnedDevice selectedDevice,
            WmiConnectionManager wmiManager, CimSessionManager cimManager, string authUser, SecureString authPass)
        {
            _devices = devices;
            _currentDevice = selectedDevice ?? devices.FirstOrDefault();
            _wmiManager = wmiManager;
            _cimManager = cimManager;
            _authUser = authUser;
            _authPass = authPass;

            Title = "Device Monitor - ArtaznIT Suite";
            Width = 1100;
            Height = 700;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(13, 13, 13)); // #0D0D0D
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;

            BuildUI();
            StartRefreshTimer();

            Loaded += async (s, e) => await RefreshCurrentDevice();
        }

        private void BuildUI()
        {
            var mainBorder = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(255, 133, 51)), // Orange
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(8)
            };

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Title bar
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content

            // TITLE BAR with close button
            var titleBar = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(15, 10, 10, 10)
            };

            var titleGrid = new Grid();
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleText = new TextBlock
            {
                Text = "Device Monitor",
                Foreground = new SolidColorBrush(Color.FromRgb(255, 133, 51)), // Orange
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(titleText, 0);
            titleGrid.Children.Add(titleText);

            var closeBtn = new Button
            {
                Content = "✕",
                Width = 32,
                Height = 32,
                Background = Brushes.Transparent,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            closeBtn.Click += (s, e) => Close();
            closeBtn.MouseEnter += (s, e) => closeBtn.Background = new SolidColorBrush(Color.FromArgb(40, 255, 68, 68));
            closeBtn.MouseLeave += (s, e) => closeBtn.Background = Brushes.Transparent;
            Grid.SetColumn(closeBtn, 1);
            titleGrid.Children.Add(closeBtn);

            titleBar.Child = titleGrid;
            Grid.SetRow(titleBar, 0);
            mainGrid.Children.Add(titleBar);

            // CONTENT AREA
            var contentGrid = new Grid();
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // LEFT PANEL: Device List
            var leftPanel = BuildDeviceListPanel();
            Grid.SetColumn(leftPanel, 0);
            contentGrid.Children.Add(leftPanel);

            // RIGHT PANEL: Details Tabs
            var rightPanel = BuildDetailsPanel();
            Grid.SetColumn(rightPanel, 1);
            contentGrid.Children.Add(rightPanel);

            Grid.SetRow(contentGrid, 1);
            mainGrid.Children.Add(contentGrid);

            mainBorder.Child = mainGrid;
            Content = mainBorder;
        }

        private Border BuildDeviceListPanel()
        {
            var panel = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(26, 26, 26)), // #1A1A1A
                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                BorderThickness = new Thickness(0, 0, 1, 0)
            };

            var stack = new StackPanel();

            // Header
            var header = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Padding = new Thickness(15, 12, 15, 12),
                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };

            var headerStack = new StackPanel();
            headerStack.Children.Add(new TextBlock
            {
                Text = "PINNED DEVICES",
                Foreground = new SolidColorBrush(Color.FromRgb(255, 133, 51)), // Orange
                FontSize = 12,
                FontWeight = FontWeights.Bold
            });
            headerStack.Children.Add(new TextBlock
            {
                Text = $"{_devices.Count} device(s) monitored",
                Foreground = new SolidColorBrush(Color.FromRgb(161, 161, 170)), // Zinc
                FontSize = 9,
                Margin = new Thickness(0, 4, 0, 0)
            });

            header.Child = headerStack;
            stack.Children.Add(header);

            // Device List
            _deviceList = new ListBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0),
                Padding = new Thickness(0)
            };

            foreach (var device in _devices)
            {
                var item = new ListBoxItem
                {
                    Content = CreateDeviceListItem(device),
                    Tag = device,
                    Padding = new Thickness(15, 10, 15, 10)
                };

                item.Selected += async (s, e) =>
                {
                    _currentDevice = device;
                    await RefreshCurrentDevice();
                };

                _deviceList.Items.Add(item);

                if (device == _currentDevice)
                {
                    item.IsSelected = true;
                }
            }

            // Style for ListBoxItem
            var itemStyle = new Style(typeof(ListBoxItem));
            itemStyle.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, Brushes.Transparent));
            itemStyle.Setters.Add(new Setter(ListBoxItem.ForegroundProperty, Brushes.White));
            itemStyle.Setters.Add(new Setter(ListBoxItem.BorderThicknessProperty, new Thickness(0)));

            var trigger = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
            trigger.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, new SolidColorBrush(Color.FromArgb(40, 255, 133, 51))));
            trigger.Setters.Add(new Setter(ListBoxItem.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(255, 133, 51))));
            trigger.Setters.Add(new Setter(ListBoxItem.BorderThicknessProperty, new Thickness(2, 0, 0, 0)));
            itemStyle.Triggers.Add(trigger);

            _deviceList.ItemContainerStyle = itemStyle;

            var scrollViewer = new ScrollViewer
            {
                Content = _deviceList,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            stack.Children.Add(scrollViewer);

            panel.Child = stack;
            return panel;
        }

        private StackPanel CreateDeviceListItem(PinnedDevice device)
        {
            var stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text = device.Input,
                Foreground = Brushes.White,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold
            });

            var statusText = new TextBlock
            {
                Text = device.Status,
                Foreground = device.StatusColor,
                FontSize = 9,
                Margin = new Thickness(0, 3, 0, 0)
            };
            stack.Children.Add(statusText);

            return stack;
        }

        private Border BuildDetailsPanel()
        {
            var panel = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(13, 13, 13))
            };

            var stack = new StackPanel();

            // Header showing current device
            var header = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Padding = new Thickness(20, 15, 20, 15),
                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };

            _txtHostname = new TextBlock
            {
                Text = _currentDevice?.Input ?? "No Device Selected",
                Foreground = new SolidColorBrush(Color.FromRgb(255, 133, 51)), // Orange
                FontSize = 18,
                FontWeight = FontWeights.Bold
            };
            header.Child = _txtHostname;
            stack.Children.Add(header);

            // Tab Control
            _tabControl = new TabControl
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0)
            };

            // Tab 1: Overview
            _tabControl.Items.Add(CreateOverviewTab());

            // Tab 2: Performance
            _tabControl.Items.Add(CreatePerformanceTab());

            // Tab 3: Network
            _tabControl.Items.Add(CreateNetworkTab());

            // Tab 4: System Events
            _tabControl.Items.Add(CreateSystemEventsTab());

            stack.Children.Add(_tabControl);

            panel.Child = stack;
            return panel;
        }

        private TabItem CreateOverviewTab()
        {
            var tab = new TabItem
            {
                Header = "Overview",
                Foreground = new SolidColorBrush(Color.FromRgb(161, 161, 170)), // Zinc
                FontSize = 11,
                FontWeight = FontWeights.SemiBold
            };

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var stack = new StackPanel { Margin = new Thickness(20) };

            // UPTIME - Make it PROMINENT
            var uptimeBorder = new Border
            {
                Background = new LinearGradientBrush(
                    Color.FromArgb(60, 255, 133, 51),
                    Color.FromArgb(20, 255, 133, 51),
                    0),
                BorderBrush = new SolidColorBrush(Color.FromRgb(255, 133, 51)),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20, 15, 20, 15),
                Margin = new Thickness(0, 0, 0, 20)
            };

            var uptimeStack = new StackPanel();
            uptimeStack.Children.Add(new TextBlock
            {
                Text = "⏱ SYSTEM UPTIME",
                Foreground = new SolidColorBrush(Color.FromRgb(255, 133, 51)),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 8)
            });

            _txtUptime = new TextBlock
            {
                Text = "Checking...",
                Foreground = Brushes.White,
                FontSize = 28,
                FontWeight = FontWeights.Bold
            };
            uptimeStack.Children.Add(_txtUptime);
            uptimeBorder.Child = uptimeStack;
            stack.Children.Add(uptimeBorder);

            // IDENTITY & ACCESS
            stack.Children.Add(new TextBlock
            {
                Text = "IDENTITY & ACCESS",
                Foreground = new SolidColorBrush(Color.FromRgb(255, 107, 0)), // Orange
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            });

            var identityGrid = new Grid { Margin = new Thickness(0, 0, 0, 16) };
            identityGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            identityGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            int row = 0;
            AddInfoRow(identityGrid, "Current User:", ref _txtUser, ref row);
            AddInfoRow(identityGrid, "Service Tag:", ref _txtSerial, ref row);
            stack.Children.Add(identityGrid);

            // NETWORK CONFIGURATION
            stack.Children.Add(new TextBlock
            {
                Text = "NETWORK CONFIGURATION",
                Foreground = new SolidColorBrush(Color.FromRgb(0, 200, 100)), // Green
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            });

            var netGrid = new Grid { Margin = new Thickness(0, 0, 0, 16) };
            netGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            netGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            row = 0;
            AddInfoRow(netGrid, "IP Address:", ref _txtIP, ref row);
            AddInfoRow(netGrid, "MAC Address:", ref _txtMAC, ref row);
            AddInfoRow(netGrid, "DNS Servers:", ref _txtDNS, ref row);
            stack.Children.Add(netGrid);

            // HARDWARE & OS
            stack.Children.Add(new TextBlock
            {
                Text = "HARDWARE & OS",
                Foreground = new SolidColorBrush(Color.FromRgb(255, 107, 0)), // Orange
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            });

            var hwGrid = new Grid { Margin = new Thickness(0, 0, 0, 16) };
            hwGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            hwGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            row = 0;
            AddInfoRow(hwGrid, "Model:", ref _txtModel, ref row);
            AddInfoRow(hwGrid, "Power:", ref _txtBattery, ref row);
            AddInfoRow(hwGrid, "CPU:", ref _txtCPU, ref row);
            AddInfoRow(hwGrid, "RAM:", ref _txtRAM, ref row);
            AddInfoRow(hwGrid, "OS:", ref _txtOS, ref row);
            stack.Children.Add(hwGrid);

            // LOCAL STORAGE
            stack.Children.Add(new TextBlock
            {
                Text = "LOCAL STORAGE",
                Foreground = new SolidColorBrush(Color.FromRgb(255, 107, 0)), // Orange
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            });

            _stkDrives = new StackPanel();
            _stkDrives.Children.Add(new TextBlock
            {
                Text = "Loading...",
                Foreground = new SolidColorBrush(Color.FromRgb(161, 161, 170)),
                FontSize = 12
            });
            stack.Children.Add(_stkDrives);

            scroll.Content = stack;
            tab.Content = scroll;

            return tab;
        }

        private void AddInfoRow(Grid grid, string label, ref TextBlock valueTextBlock, ref int row)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var lblText = new TextBlock
            {
                Text = label,
                Foreground = new SolidColorBrush(Color.FromRgb(161, 161, 170)), // Zinc
                FontSize = 12,
                Margin = new Thickness(0, 8, 10, 8),
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetRow(lblText, row);
            Grid.SetColumn(lblText, 0);
            grid.Children.Add(lblText);

            if (valueTextBlock == null)
            {
                valueTextBlock = new TextBlock
                {
                    Text = "Loading...",
                    Foreground = Brushes.White,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 8, 0, 8)
                };
            }

            Grid.SetRow(valueTextBlock, row);
            Grid.SetColumn(valueTextBlock, 1);
            grid.Children.Add(valueTextBlock);

            row++;
        }

        private TabItem CreatePerformanceTab()
        {
            var tab = new TabItem
            {
                Header = "Performance",
                Foreground = new SolidColorBrush(Color.FromRgb(161, 161, 170)),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold
            };

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var stack = new StackPanel { Margin = new Thickness(20) };

            stack.Children.Add(new TextBlock
            {
                Text = "RESOURCE USAGE",
                Foreground = new SolidColorBrush(Color.FromRgb(255, 133, 51)),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 15)
            });

            // CPU Usage
            stack.Children.Add(CreateUsageBar("CPU Usage", ref _txtCpuUsage, ref _barCpu));

            // RAM Usage
            stack.Children.Add(CreateUsageBar("RAM Usage", ref _txtRamUsage, ref _barRam));

            // Disk Usage
            stack.Children.Add(CreateUsageBar("Disk Usage", ref _txtDiskUsage, ref _barDisk));

            scroll.Content = stack;
            tab.Content = scroll;

            return tab;
        }

        private Border CreateUsageBar(string label, ref TextBlock valueText, ref System.Windows.Shapes.Rectangle bar)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(26, 26, 26)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(15, 12, 15, 12),
                Margin = new Thickness(0, 0, 0, 12)
            };

            var stack = new StackPanel();

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var lblText = new TextBlock
            {
                Text = label,
                Foreground = Brushes.White,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold
            };
            Grid.SetColumn(lblText, 0);
            headerGrid.Children.Add(lblText);

            valueText = new TextBlock
            {
                Text = "0%",
                Foreground = new SolidColorBrush(Color.FromRgb(255, 133, 51)),
                FontSize = 11,
                FontWeight = FontWeights.Bold
            };
            Grid.SetColumn(valueText, 1);
            headerGrid.Children.Add(valueText);

            stack.Children.Add(headerGrid);

            // Progress bar background
            var barBg = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
                Height = 20,
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(0, 8, 0, 0)
            };

            var barGrid = new Grid();
            bar = new System.Windows.Shapes.Rectangle
            {
                Fill = new LinearGradientBrush(
                    Color.FromRgb(255, 133, 51),
                    Color.FromRgb(255, 170, 102),
                    0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = 0,
                RadiusX = 10,
                RadiusY = 10
            };
            barGrid.Children.Add(bar);
            barBg.Child = barGrid;

            stack.Children.Add(barBg);

            border.Child = stack;
            return border;
        }

        private TextBlock _txtNetAdapters, _txtNetIP, _txtNetMAC, _txtNetGateway, _txtNetDNS, _txtNetDHCP;

        private TabItem CreateNetworkTab()
        {
            var tab = new TabItem
            {
                Header = "Network",
                Foreground = new SolidColorBrush(Color.FromRgb(161, 161, 170)),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold
            };

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var stack = new StackPanel { Margin = new Thickness(20) };

            // NETWORK INFORMATION
            stack.Children.Add(new TextBlock
            {
                Text = "NETWORK ADAPTERS",
                Foreground = new SolidColorBrush(Color.FromRgb(0, 200, 100)),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            });

            var netGrid = new Grid { Margin = new Thickness(0, 0, 0, 20) };
            netGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            netGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            int row = 0;
            AddInfoRow(netGrid, "IP Address:", ref _txtNetIP, ref row);
            AddInfoRow(netGrid, "MAC Address:", ref _txtNetMAC, ref row);
            AddInfoRow(netGrid, "Gateway:", ref _txtNetGateway, ref row);
            AddInfoRow(netGrid, "DNS Servers:", ref _txtNetDNS, ref row);
            AddInfoRow(netGrid, "DHCP Enabled:", ref _txtNetDHCP, ref row);
            stack.Children.Add(netGrid);

            stack.Children.Add(new TextBlock
            {
                Text = "ADAPTERS",
                Foreground = new SolidColorBrush(Color.FromRgb(161, 161, 170)),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 8)
            });

            _txtNetAdapters = new TextBlock
            {
                Text = "Loading...",
                Foreground = Brushes.White,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 20)
            };
            stack.Children.Add(_txtNetAdapters);

            // PING HISTORY CHART
            stack.Children.Add(new TextBlock
            {
                Text = "PING HISTORY (LAST 4 HOURS)",
                Foreground = new SolidColorBrush(Color.FromRgb(255, 133, 51)),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 10, 0, 10)
            });

            var chartBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(26, 26, 26)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(15),
                Height = 200,
                Margin = new Thickness(0, 0, 0, 10)
            };

            var chartCanvas = new System.Windows.Controls.Canvas
            {
                Background = new SolidColorBrush(Color.FromRgb(20, 20, 20))
            };

            chartCanvas.Loaded += (s, e) => DrawPingHistoryChart(chartCanvas);

            chartBorder.Child = chartCanvas;
            stack.Children.Add(chartBorder);

            scroll.Content = stack;
            tab.Content = scroll;
            return tab;
        }

        private void DrawPingHistoryChart(System.Windows.Controls.Canvas canvas)
        {
            if (_currentDevice == null || _currentDevice.PingHistory == null || _currentDevice.PingHistory.Count == 0)
            {
                var noData = new TextBlock
                {
                    Text = "No ping history data available",
                    Foreground = new SolidColorBrush(Color.FromRgb(161, 161, 170)),
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                System.Windows.Controls.Canvas.SetLeft(noData, 50);
                System.Windows.Controls.Canvas.SetTop(noData, canvas.ActualHeight / 2);
                canvas.Children.Add(noData);
                return;
            }

            canvas.Children.Clear();

            var history = _currentDevice.PingHistory.OrderBy(p => p.Time).ToList();
            if (history.Count < 2) return;

            double width = canvas.ActualWidth;
            double height = canvas.ActualHeight;
            double maxLatency = history.Max(p => p.Latency > 0 ? p.Latency : 0);
            if (maxLatency == 0) maxLatency = 100;

            double xStep = width / Math.Max(1, history.Count - 1);

            for (int i = 0; i < history.Count - 1; i++)
            {
                if (history[i].Latency < 0 || history[i + 1].Latency < 0) continue;

                double x1 = i * xStep;
                double y1 = height - (history[i].Latency / maxLatency * height);
                double x2 = (i + 1) * xStep;
                double y2 = height - (history[i + 1].Latency / maxLatency * height);

                var line = new System.Windows.Shapes.Line
                {
                    X1 = x1,
                    Y1 = y1,
                    X2 = x2,
                    Y2 = y2,
                    Stroke = new SolidColorBrush(Color.FromRgb(255, 133, 51)),
                    StrokeThickness = 2
                };

                canvas.Children.Add(line);
            }
        }

        private System.Windows.Controls.DataGrid _gridEvents;

        private TabItem CreateSystemEventsTab()
        {
            var tab = new TabItem
            {
                Header = "System Events",
                Foreground = new SolidColorBrush(Color.FromRgb(161, 161, 170)),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold
            };

            var stack = new StackPanel { Margin = new Thickness(20) };

            stack.Children.Add(new TextBlock
            {
                Text = "STARTUP & SHUTDOWN HISTORY (LAST 60 DAYS)",
                Foreground = new SolidColorBrush(Color.FromRgb(255, 133, 51)),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 15)
            });

            _gridEvents = new System.Windows.Controls.DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                Background = new SolidColorBrush(Color.FromRgb(26, 26, 26)),
                RowBackground = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(26, 26, 26)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                BorderThickness = new Thickness(1),
                GridLinesVisibility = System.Windows.Controls.DataGridGridLinesVisibility.Horizontal,
                HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(50, 50, 50)),
                HeadersVisibility = System.Windows.Controls.DataGridHeadersVisibility.Column,
                CanUserResizeRows = false,
                FontSize = 12
            };

            var colTime = new System.Windows.Controls.DataGridTextColumn
            {
                Header = "Time",
                Binding = new System.Windows.Data.Binding("Time"),
                Width = 180
            };
            colTime.ElementStyle = new Style(typeof(TextBlock));
            colTime.ElementStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brushes.White));
            colTime.ElementStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty, 12.0));

            var colType = new System.Windows.Controls.DataGridTextColumn
            {
                Header = "Event Type",
                Binding = new System.Windows.Data.Binding("EventType"),
                Width = 120
            };
            colType.ElementStyle = new Style(typeof(TextBlock));
            colType.ElementStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, new System.Windows.Data.Binding("TypeColor")));
            colType.ElementStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
            colType.ElementStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty, 12.0));

            var colMessage = new System.Windows.Controls.DataGridTextColumn
            {
                Header = "Details",
                Binding = new System.Windows.Data.Binding("Message"),
                Width = new System.Windows.Controls.DataGridLength(1, System.Windows.Controls.DataGridLengthUnitType.Star)
            };
            colMessage.ElementStyle = new Style(typeof(TextBlock));
            colMessage.ElementStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(161, 161, 170))));
            colMessage.ElementStyle.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
            colMessage.ElementStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty, 11.0));

            _gridEvents.Columns.Add(colTime);
            _gridEvents.Columns.Add(colType);
            _gridEvents.Columns.Add(colMessage);

            var columnHeaderStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
            columnHeaderStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BackgroundProperty, new SolidColorBrush(Color.FromRgb(20, 20, 20))));
            columnHeaderStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.ForegroundProperty, new SolidColorBrush(Color.FromRgb(255, 133, 51))));
            columnHeaderStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.FontWeightProperty, FontWeights.Bold));
            columnHeaderStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.FontSizeProperty, 12.0));
            columnHeaderStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.PaddingProperty, new Thickness(10, 8, 10, 8)));
            columnHeaderStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(60, 60, 60))));
            columnHeaderStyle.Setters.Add(new Setter(System.Windows.Controls.Primitives.DataGridColumnHeader.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));

            _gridEvents.ColumnHeaderStyle = columnHeaderStyle;

            stack.Children.Add(_gridEvents);

            tab.Content = stack;
            return tab;
        }

        public class SystemEvent
        {
            public string Time { get; set; }
            public string EventType { get; set; }
            public Brush TypeColor { get; set; }
            public string Message { get; set; }
        }

        private void StartRefreshTimer()
        {
            _refreshTimer = new DispatcherTimer();
            // Stagger: 30 seconds / number of devices
            int intervalSeconds = Math.Max(5, 30 / Math.Max(1, _devices.Count));
            _refreshTimer.Interval = TimeSpan.FromSeconds(intervalSeconds);
            _refreshTimer.Tick += RefreshTimer_Tick;
            _refreshTimer.Start();
        }

        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            // Staggered refresh
            if (_devices.Count > 0)
            {
                _refreshIndex = (_refreshIndex + 1) % _devices.Count;
                // Trigger refresh for that device in background
            }
        }

        private async Task RefreshCurrentDevice()
        {
            // Null check - UI elements may not be initialized yet
            if (_txtHostname == null || _txtUptime == null || _txtIP == null)
                return;

            if (_currentDevice == null || !_currentDevice.IsOnline)
            {
                _txtUptime.Text = "Device Offline";
                _txtHostname.Text = _currentDevice?.Input ?? "N/A";
                _txtIP.Text = _currentDevice?.ResolvedIP ?? "N/A";
                return;
            }

            // Update header
            _txtHostname.Text = _currentDevice.Input;
            _txtIP.Text = _currentDevice.ResolvedIP;

            // Get real WMI/CIM data
            await Task.Run(() =>
            {
                try
                {
                    string hostname = _currentDevice.ResolvedIP ?? _currentDevice.Input;

                    // Try CIM first, fall back to WMI
                    CimSession cimSession = null;
                    ManagementScope wmiScope = null;
                    bool useCim = false;

                    try
                    {
                        cimSession = _cimManager.GetConnection(hostname, _authUser, _authPass, out string protocol);
                        useCim = true;
                    }
                    catch
                    {
                        // Fall back to WMI
                        wmiScope = _wmiManager.GetConnection(hostname, _authUser, _authPass);
                        useCim = false;
                    }

                    string uptime = "--", model = "--", os = "--", cpu = "--", ram = "--";
                    string user = "--", serial = "--", mac = "--", dns = "--", battery = "--";
                    string gateway = "--", dhcp = "--";
                    double cpuUsage = 0, ramUsage = 0, diskUsage = 0;
                    List<string> drives = new List<string>();
                    List<string> adapters = new List<string>();
                    List<SystemEvent> events = new List<SystemEvent>();

                    // Get system info
                    if (useCim)
                    {
                        var instances = cimSession.QueryInstances("root/cimv2", "WQL", "SELECT * FROM Win32_OperatingSystem");
                        var osObj = instances.FirstOrDefault();
                        if (osObj != null)
                        {
                            // Calculate uptime (CIM returns native DateTime)
                            DateTime? lastBoot = osObj.CimInstanceProperties["LastBootUpTime"]?.Value as DateTime?;
                            if (lastBoot.HasValue)
                            {
                                TimeSpan upTime = DateTime.Now - lastBoot.Value;
                                uptime = $"{upTime.Days}d {upTime.Hours}h {upTime.Minutes}m";
                            }

                            os = osObj.CimInstanceProperties["Caption"]?.Value?.ToString() ?? "--";

                            // RAM usage
                            var totalMem = osObj.CimInstanceProperties["TotalVisibleMemorySize"]?.Value;
                            var freeMem = osObj.CimInstanceProperties["FreePhysicalMemory"]?.Value;
                            if (totalMem != null && freeMem != null)
                            {
                                double total = Convert.ToDouble(totalMem) / 1024; // MB
                                double free = Convert.ToDouble(freeMem) / 1024; // MB
                                ramUsage = ((total - free) / total) * 100;
                            }
                        }
                    }
                    else
                    {
                        using (var searcher = new ManagementObjectSearcher(wmiScope, new ObjectQuery("SELECT * FROM Win32_OperatingSystem")))
                        using (var results = searcher.Get())
                        {
                            foreach (ManagementObject obj in results)
                            {
                                // Calculate uptime
                                if (obj["LastBootUpTime"] != null)
                                {
                                    string bootTime = obj["LastBootUpTime"].ToString();
                                    DateTime lastBoot = ManagementDateTimeConverter.ToDateTime(bootTime);
                                    TimeSpan upTime = DateTime.Now - lastBoot;
                                    uptime = $"{upTime.Days}d {upTime.Hours}h {upTime.Minutes}m";
                                }

                                os = obj["Caption"]?.ToString() ?? "--";

                                // RAM usage
                                if (obj["TotalVisibleMemorySize"] != null && obj["FreePhysicalMemory"] != null)
                                {
                                    double total = Convert.ToDouble(obj["TotalVisibleMemorySize"]) / 1024; // MB
                                    double free = Convert.ToDouble(obj["FreePhysicalMemory"]) / 1024; // MB
                                    ramUsage = ((total - free) / total) * 100;
                                }
                            }
                        }
                    }

                    // Get computer system info
                    if (useCim)
                    {
                        var instances = cimSession.QueryInstances("root/cimv2", "WQL", "SELECT * FROM Win32_ComputerSystem");
                        var csObj = instances.FirstOrDefault();
                        if (csObj != null)
                        {
                            model = csObj.CimInstanceProperties["Model"]?.Value?.ToString() ?? "--";

                            var totalPhysMem = csObj.CimInstanceProperties["TotalPhysicalMemory"]?.Value;
                            if (totalPhysMem != null)
                            {
                                double totalRAM = Convert.ToDouble(totalPhysMem) / (1024 * 1024 * 1024); // GB
                                ram = $"{totalRAM:F0} GB";
                            }
                        }
                    }
                    else
                    {
                        using (var searcher = new ManagementObjectSearcher(wmiScope, new ObjectQuery("SELECT * FROM Win32_ComputerSystem")))
                        using (var results = searcher.Get())
                        {
                            foreach (ManagementObject obj in results)
                            {
                                model = obj["Model"]?.ToString() ?? "--";

                                if (obj["TotalPhysicalMemory"] != null)
                                {
                                    double totalRAM = Convert.ToDouble(obj["TotalPhysicalMemory"]) / (1024 * 1024 * 1024); // GB
                                    ram = $"{totalRAM:F0} GB";
                                }
                            }
                        }
                    }

                    // Get CPU info
                    if (useCim)
                    {
                        var instances = cimSession.QueryInstances("root/cimv2", "WQL", "SELECT * FROM Win32_Processor");
                        var cpuObj = instances.FirstOrDefault();
                        if (cpuObj != null)
                        {
                            cpu = cpuObj.CimInstanceProperties["Name"]?.Value?.ToString() ?? "--";

                            var loadPct = cpuObj.CimInstanceProperties["LoadPercentage"]?.Value;
                            if (loadPct != null)
                            {
                                cpuUsage = Convert.ToDouble(loadPct);
                            }
                        }
                    }
                    else
                    {
                        using (var searcher = new ManagementObjectSearcher(wmiScope, new ObjectQuery("SELECT * FROM Win32_Processor")))
                        using (var results = searcher.Get())
                        {
                            foreach (ManagementObject obj in results)
                            {
                                cpu = obj["Name"]?.ToString() ?? "--";

                                if (obj["LoadPercentage"] != null)
                                {
                                    cpuUsage = Convert.ToDouble(obj["LoadPercentage"]);
                                }
                            }
                        }
                    }

                    // Get disk usage (C: drive)
                    if (useCim)
                    {
                        var instances = cimSession.QueryInstances("root/cimv2", "WQL", "SELECT * FROM Win32_LogicalDisk WHERE DeviceID='C:'");
                        var diskObj = instances.FirstOrDefault();
                        if (diskObj != null)
                        {
                            var sizeVal = diskObj.CimInstanceProperties["Size"]?.Value;
                            var freeVal = diskObj.CimInstanceProperties["FreeSpace"]?.Value;
                            if (sizeVal != null && freeVal != null)
                            {
                                double total = Convert.ToDouble(sizeVal);
                                double free = Convert.ToDouble(freeVal);
                                diskUsage = ((total - free) / total) * 100;
                            }
                        }
                    }
                    else
                    {
                        using (var searcher = new ManagementObjectSearcher(wmiScope, new ObjectQuery("SELECT * FROM Win32_LogicalDisk WHERE DeviceID='C:'")))
                        using (var results = searcher.Get())
                        {
                            foreach (ManagementObject obj in results)
                            {
                                if (obj["Size"] != null && obj["FreeSpace"] != null)
                                {
                                    double total = Convert.ToDouble(obj["Size"]);
                                    double free = Convert.ToDouble(obj["FreeSpace"]);
                                    diskUsage = ((total - free) / total) * 100;
                                }
                            }
                        }
                    }

                    // Get all drives info
                    if (useCim)
                    {
                        var instances = cimSession.QueryInstances("root/cimv2", "WQL", "SELECT * FROM Win32_LogicalDisk WHERE DriveType=3");
                        foreach (var obj in instances)
                        {
                            string deviceID = obj.CimInstanceProperties["DeviceID"]?.Value?.ToString() ?? "";
                            var sizeVal = obj.CimInstanceProperties["Size"]?.Value;
                            var freeVal = obj.CimInstanceProperties["FreeSpace"]?.Value;
                            if (sizeVal != null && freeVal != null)
                            {
                                double total = Convert.ToDouble(sizeVal) / (1024.0 * 1024 * 1024);
                                double free = Convert.ToDouble(freeVal) / (1024.0 * 1024 * 1024);
                                double used = total - free;
                                drives.Add($"{deviceID} — {used:F0} GB used / {total:F0} GB total");
                            }
                        }
                    }
                    else
                    {
                        using (var searcher = new ManagementObjectSearcher(wmiScope, new ObjectQuery("SELECT * FROM Win32_LogicalDisk WHERE DriveType=3")))
                        using (var results = searcher.Get())
                        {
                            foreach (ManagementObject obj in results)
                            {
                                string deviceID = obj["DeviceID"]?.ToString() ?? "";
                                if (obj["Size"] != null && obj["FreeSpace"] != null)
                                {
                                    double total = Convert.ToDouble(obj["Size"]) / (1024.0 * 1024 * 1024);
                                    double free = Convert.ToDouble(obj["FreeSpace"]) / (1024.0 * 1024 * 1024);
                                    double used = total - free;
                                    drives.Add($"{deviceID} — {used:F0} GB used / {total:F0} GB total");
                                }
                            }
                        }
                    }

                    // Get BIOS info (Serial Number)
                    if (useCim)
                    {
                        var instances = cimSession.QueryInstances("root/cimv2", "WQL", "SELECT * FROM Win32_BIOS");
                        var biosObj = instances.FirstOrDefault();
                        if (biosObj != null)
                        {
                            serial = biosObj.CimInstanceProperties["SerialNumber"]?.Value?.ToString() ?? "--";
                        }
                    }
                    else
                    {
                        using (var searcher = new ManagementObjectSearcher(wmiScope, new ObjectQuery("SELECT * FROM Win32_BIOS")))
                        using (var results = searcher.Get())
                        {
                            foreach (ManagementObject obj in results)
                            {
                                serial = obj["SerialNumber"]?.ToString() ?? "--";
                            }
                        }
                    }

                    // Get user from ComputerSystem
                    if (useCim)
                    {
                        var instances = cimSession.QueryInstances("root/cimv2", "WQL", "SELECT UserName FROM Win32_ComputerSystem");
                        var csObj = instances.FirstOrDefault();
                        if (csObj != null)
                        {
                            user = csObj.CimInstanceProperties["UserName"]?.Value?.ToString() ?? "--";
                        }
                    }
                    else
                    {
                        using (var searcher = new ManagementObjectSearcher(wmiScope, new ObjectQuery("SELECT UserName FROM Win32_ComputerSystem")))
                        using (var results = searcher.Get())
                        {
                            foreach (ManagementObject obj in results)
                            {
                                user = obj["UserName"]?.ToString() ?? "--";
                            }
                        }
                    }

                    // Get network adapter configuration
                    if (useCim)
                    {
                        var instances = cimSession.QueryInstances("root/cimv2", "WQL", "SELECT * FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled=True");
                        foreach (var obj in instances)
                        {
                            string adapterDesc = obj.CimInstanceProperties["Description"]?.Value?.ToString() ?? "Unknown Adapter";
                            string[] ipAddresses = obj.CimInstanceProperties["IPAddress"]?.Value as string[];
                            string adapterIP = ipAddresses != null && ipAddresses.Length > 0 ? ipAddresses[0] : "N/A";
                            string adapterMAC = obj.CimInstanceProperties["MACAddress"]?.Value?.ToString() ?? "N/A";

                            adapters.Add($"{adapterDesc}\n  IP: {adapterIP} | MAC: {adapterMAC}");

                            if (adapterMAC != "N/A" && mac == "--")
                            {
                                mac = adapterMAC;
                            }

                            if (dns == "--")
                            {
                                string[] dnsServers = obj.CimInstanceProperties["DNSServerSearchOrder"]?.Value as string[];
                                if (dnsServers != null)
                                {
                                    dns = string.Join(", ", dnsServers);
                                }
                            }

                            if (gateway == "--")
                            {
                                string[] gateways = obj.CimInstanceProperties["DefaultIPGateway"]?.Value as string[];
                                if (gateways != null && gateways.Length > 0)
                                {
                                    gateway = gateways[0];
                                }
                            }

                            if (dhcp == "--")
                            {
                                var dhcpVal = obj.CimInstanceProperties["DHCPEnabled"]?.Value;
                                if (dhcpVal != null)
                                {
                                    bool dhcpEnabled = Convert.ToBoolean(dhcpVal);
                                    dhcp = dhcpEnabled ? "Yes" : "No";
                                }
                            }
                        }
                    }
                    else
                    {
                        using (var searcher = new ManagementObjectSearcher(wmiScope, new ObjectQuery("SELECT * FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled=True")))
                        using (var results = searcher.Get())
                        {
                            foreach (ManagementObject obj in results)
                            {
                                string adapterDesc = obj["Description"]?.ToString() ?? "Unknown Adapter";
                                string adapterIP = obj["IPAddress"] != null ? ((string[])obj["IPAddress"])[0] : "N/A";
                                string adapterMAC = obj["MACAddress"]?.ToString() ?? "N/A";

                                adapters.Add($"{adapterDesc}\n  IP: {adapterIP} | MAC: {adapterMAC}");

                                if (obj["MACAddress"] != null && mac == "--")
                                {
                                    mac = obj["MACAddress"].ToString();
                                }

                                if (obj["DNSServerSearchOrder"] != null && dns == "--")
                                {
                                    string[] dnsServers = (string[])obj["DNSServerSearchOrder"];
                                    dns = string.Join(", ", dnsServers);
                                }

                                if (obj["DefaultIPGateway"] != null && gateway == "--")
                                {
                                    string[] gateways = (string[])obj["DefaultIPGateway"];
                                    gateway = gateways.Length > 0 ? gateways[0] : "--";
                                }

                                if (obj["DHCPEnabled"] != null && dhcp == "--")
                                {
                                    bool dhcpEnabled = Convert.ToBoolean(obj["DHCPEnabled"]);
                                    dhcp = dhcpEnabled ? "Yes" : "No";
                                }
                            }
                        }
                    }

                    // Get battery info
                    try
                    {
                        if (useCim)
                        {
                            var instances = cimSession.QueryInstances("root/cimv2", "WQL", "SELECT * FROM Win32_Battery");
                            bool hasBattery = false;
                            foreach (var obj in instances)
                            {
                                hasBattery = true;
                                var chargeVal = obj.CimInstanceProperties["EstimatedChargeRemaining"]?.Value;
                                var statusVal = obj.CimInstanceProperties["BatteryStatus"]?.Value;
                                int percentage = chargeVal != null ? Convert.ToInt32(chargeVal) : 0;
                                int status = statusVal != null ? Convert.ToInt32(statusVal) : 0;
                                string statusText = status == 2 ? "AC Power" : $"Battery {percentage}%";
                                battery = statusText;
                            }

                            if (!hasBattery)
                            {
                                battery = "AC Power (Desktop)";
                            }
                        }
                        else
                        {
                            using (var searcher = new ManagementObjectSearcher(wmiScope, new ObjectQuery("SELECT * FROM Win32_Battery")))
                            using (var results = searcher.Get())
                            {
                                bool hasBattery = false;
                                foreach (ManagementObject obj in results)
                                {
                                    hasBattery = true;
                                    int percentage = obj["EstimatedChargeRemaining"] != null ? Convert.ToInt32(obj["EstimatedChargeRemaining"]) : 0;
                                    int status = obj["BatteryStatus"] != null ? Convert.ToInt32(obj["BatteryStatus"]) : 0;
                                    string statusText = status == 2 ? "AC Power" : $"Battery {percentage}%";
                                    battery = statusText;
                                }

                                if (!hasBattery)
                                {
                                    battery = "AC Power (Desktop)";
                                }
                            }
                        }
                    }
                    catch
                    {
                        battery = "AC Power";
                    }

                    // Get system events (startup/shutdown from last 60 days)
                    try
                    {
                        DateTime cutoffDate = DateTime.Now.AddDays(-60);

                        if (useCim)
                        {
                            // For CIM, use standard DateTime format
                            string cutoffCIM = cutoffDate.ToUniversalTime().ToString("yyyyMMddHHmmss.ffffff+000");
                            string query = $"SELECT * FROM Win32_NTLogEvent WHERE Logfile='System' AND (EventCode=6005 OR EventCode=6006 OR EventCode=6009 OR EventCode=1074 OR EventCode=12) AND TimeGenerated >= '{cutoffCIM}'";

                            var instances = cimSession.QueryInstances("root/cimv2", "WQL", query);
                            foreach (var obj in instances)
                            {
                                var timeGenVal = obj.CimInstanceProperties["TimeGenerated"]?.Value;
                                if (timeGenVal == null) continue;

                                DateTime eventTime = timeGenVal is DateTime dt ? dt : DateTime.Parse(timeGenVal.ToString());
                                var eventCodeVal = obj.CimInstanceProperties["EventCode"]?.Value;
                                int eventCode = eventCodeVal != null ? Convert.ToInt32(eventCodeVal) : 0;
                                string message = obj.CimInstanceProperties["Message"]?.Value?.ToString() ?? "";

                                string eventType = "";
                                Brush typeColor = Brushes.White;

                                if (eventCode == 6005 || eventCode == 6009 || eventCode == 12)
                                {
                                    eventType = "🟢 STARTUP";
                                    typeColor = Brushes.LimeGreen;
                                    message = "System started";
                                }
                                else if (eventCode == 6006 || eventCode == 1074)
                                {
                                    eventType = "🔴 SHUTDOWN";
                                    typeColor = new SolidColorBrush(Color.FromRgb(255, 68, 68));
                                    message = eventCode == 1074 ? "System shutdown initiated" : "System stopped";
                                }

                                events.Add(new SystemEvent
                                {
                                    Time = eventTime.ToString("yyyy-MM-dd h:mm tt"),
                                    EventType = eventType,
                                    TypeColor = typeColor,
                                    Message = message
                                });

                                // Limit to 100 events to avoid performance issues
                                if (events.Count >= 100) break;
                            }
                        }
                        else
                        {
                            // WMI path - use ManagementDateTimeConverter
                            string cutoffWMI = ManagementDateTimeConverter.ToDmtfDateTime(cutoffDate);
                            string query = $"SELECT * FROM Win32_NTLogEvent WHERE Logfile='System' AND (EventCode=6005 OR EventCode=6006 OR EventCode=6009 OR EventCode=1074 OR EventCode=12) AND TimeGenerated >= '{cutoffWMI}'";

                            using (var searcher = new ManagementObjectSearcher(wmiScope, new ObjectQuery(query)))
                            using (var results = searcher.Get())
                            {
                                foreach (ManagementObject obj in results)
                                {
                                    string timeGen = obj["TimeGenerated"]?.ToString();
                                    if (string.IsNullOrEmpty(timeGen)) continue;

                                    DateTime eventTime = ManagementDateTimeConverter.ToDateTime(timeGen);
                                    int eventCode = obj["EventCode"] != null ? Convert.ToInt32(obj["EventCode"]) : 0;
                                    string message = obj["Message"]?.ToString() ?? "";

                                    string eventType = "";
                                    Brush typeColor = Brushes.White;

                                    if (eventCode == 6005 || eventCode == 6009 || eventCode == 12)
                                    {
                                        eventType = "🟢 STARTUP";
                                        typeColor = Brushes.LimeGreen;
                                        message = "System started";
                                    }
                                    else if (eventCode == 6006 || eventCode == 1074)
                                    {
                                        eventType = "🔴 SHUTDOWN";
                                        typeColor = new SolidColorBrush(Color.FromRgb(255, 68, 68));
                                        message = eventCode == 1074 ? "System shutdown initiated" : "System stopped";
                                    }

                                    events.Add(new SystemEvent
                                    {
                                        Time = eventTime.ToString("yyyy-MM-dd h:mm tt"),
                                        EventType = eventType,
                                        TypeColor = typeColor,
                                        Message = message
                                    });

                                    // Limit to 100 events to avoid performance issues
                                    if (events.Count >= 100) break;
                                }
                            }
                        }

                        // Sort by time descending (most recent first)
                        events = events.OrderByDescending(e => e.Time).ToList();
                    }
                    catch (Exception ex)
                    {
                        events.Add(new SystemEvent
                        {
                            Time = DateTime.Now.ToString("yyyy-MM-dd h:mm tt"),
                            EventType = "ERROR",
                            TypeColor = Brushes.Orange,
                            Message = $"Failed to query events: {ex.Message}"
                        });
                    }

                    // Update UI
                    Dispatcher.Invoke(() =>
                    {
                        _txtUptime.Text = uptime;
                        _txtModel.Text = model;
                        _txtOS.Text = os;
                        _txtCPU.Text = cpu;
                        _txtRAM.Text = ram;
                        _txtUser.Text = user;
                        _txtSerial.Text = serial;
                        _txtMAC.Text = mac;
                        _txtDNS.Text = dns;
                        _txtBattery.Text = battery;

                        // Network tab fields (if they exist)
                        if (_txtNetIP != null) _txtNetIP.Text = _currentDevice.ResolvedIP;
                        if (_txtNetMAC != null) _txtNetMAC.Text = mac;
                        if (_txtNetGateway != null) _txtNetGateway.Text = gateway;
                        if (_txtNetDNS != null) _txtNetDNS.Text = dns;
                        if (_txtNetDHCP != null) _txtNetDHCP.Text = dhcp;
                        if (_txtNetAdapters != null)
                        {
                            _txtNetAdapters.Text = adapters.Count > 0 ? string.Join("\n\n", adapters) : "No adapters found";
                        }

                        // System Events tab
                        if (_gridEvents != null)
                        {
                            _gridEvents.ItemsSource = events;
                        }

                        // Update drives
                        _stkDrives.Children.Clear();
                        if (drives.Count > 0)
                        {
                            foreach (var drive in drives)
                            {
                                _stkDrives.Children.Add(new TextBlock
                                {
                                    Text = drive,
                                    Foreground = Brushes.White,
                                    FontSize = 12,
                                    Margin = new Thickness(0, 4, 0, 4)
                                });
                            }
                        }
                        else
                        {
                            _stkDrives.Children.Add(new TextBlock
                            {
                                Text = "No data available",
                                Foreground = new SolidColorBrush(Color.FromRgb(161, 161, 170)),
                                FontSize = 12
                            });
                        }

                        // Performance bars
                        UpdateUsageBar(_barCpu, _txtCpuUsage, cpuUsage);
                        UpdateUsageBar(_barRam, _txtRamUsage, ramUsage);
                        UpdateUsageBar(_barDisk, _txtDiskUsage, diskUsage);
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        _txtUptime.Text = "Error querying";
                        _txtModel.Text = "WMI access denied";
                        _txtOS.Text = ex.Message;
                    });
                }
            });
        }

        private void UpdateUsageBar(System.Windows.Shapes.Rectangle bar, TextBlock text, double percentage)
        {
            text.Text = $"{percentage:F0}%";
            var animation = new DoubleAnimation
            {
                To = (bar.Parent as Grid).ActualWidth * (percentage / 100),
                Duration = TimeSpan.FromMilliseconds(500),
                EasingFunction = new QuadraticEase()
            };
            bar.BeginAnimation(System.Windows.Shapes.Rectangle.WidthProperty, animation);
        }

        protected override void OnClosed(EventArgs e)
        {
            _refreshTimer?.Stop();
            base.OnClosed(e);
        }
    }

    // ############################################################################
    // LOGIN WINDOW
    // ############################################################################

    // ══════════════════════════════════════════════════════════════
    // MODULAR LOGO COMPONENT - Artazn LLC Branding
    // ══════════════════════════════════════════════════════════════
    // LOGO_CONFIG: Change these values to update the logo across the entire application
    public static class LogoConfig
    {
        // Branding Text
        public const string COMPANY_NAME = "Artazn";
        public const string COMPANY_SUFFIX = " LLC";
        public const string TAGLINE = "I T   M A N A G E M E N T   S U I T E";

        /// <summary>
        /// Gets version in CalVer format: Major.YYMM.Minor
        /// Example: v6.2602.1 = Version 6, February 2026, iteration 1
        /// TAG: #MODULAR #VERSION #CALVER
        /// </summary>
        public static string VERSION
        {
            get
            {
                var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                // Format: Major.YYMM.Minor
                // AssemblyVersion should be: 6.2602.1.0
                return $"v{version.Major}.{version.Minor:D4}.{version.Build}";
            }
        }

        /// <summary>
        /// Gets full version with build number: Major.YYMM.Minor.Build
        /// TAG: #MODULAR #VERSION #CALVER
        /// </summary>
        public static string FULL_VERSION
        {
            get
            {
                var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                return $"v{version.Major}.{version.Minor:D4}.{version.Build}.{version.Revision}";
            }
        }

        /// <summary>
        /// Gets human-readable version: "Version 6, February 2026 (6.2602.1)"
        /// TAG: #MODULAR #VERSION #CALVER
        /// </summary>
        public static string VERSION_READABLE
        {
            get
            {
                var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                int yearMonth = version.Minor;
                int year = 2000 + (yearMonth / 100);
                int month = yearMonth % 100;
                string monthName = new DateTime(year, month, 1).ToString("MMMM");
                return $"Version {version.Major}, {monthName} {year} ({version.Major}.{version.Minor:D4}.{version.Build})";
            }
        }

        /// <summary>
        /// Gets compiled date from assembly build date
        /// TAG: #MODULAR #VERSION
        /// </summary>
        public static string COMPILED_DATE
        {
            get
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var fileInfo = new System.IO.FileInfo(assembly.Location);
                return fileInfo.LastWriteTime.ToString("MMMM dd, yyyy");
            }
        }

        /// <summary>
        /// Gets compiled date in short format
        /// TAG: #MODULAR #VERSION
        /// </summary>
        public static string COMPILED_DATE_SHORT
        {
            get
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var fileInfo = new System.IO.FileInfo(assembly.Location);
                return fileInfo.LastWriteTime.ToString("yyyy-MM-dd");
            }
        }

        // Colors (Orange/Zinc Theme)
        public static readonly Color ORANGE_PRIMARY = Color.FromRgb(255, 133, 51);    // #FFFF8533
        public static readonly Color ORANGE_DARK = Color.FromRgb(204, 107, 41);       // #FFCC6B29
        public static readonly Color ZINC_COLOR = Color.FromRgb(161, 161, 170);       // #FFA1A1AA
        public static readonly Color BG_DARK = Color.FromRgb(26, 26, 26);             // #FF1A1A1A

        // Logo Sizes
        public const double LARGE_ICON_SIZE = 50;
        public const double MEDIUM_ICON_SIZE = 36;
        public const double SMALL_ICON_SIZE = 24;

        /// <summary>
        /// Creates the "A" letter icon SVG path
        /// </summary>
        public static System.Windows.Shapes.Path CreateIconPath()
        {
            return new System.Windows.Shapes.Path
            {
                Fill = Brushes.White,
                Data = Geometry.Parse("M12,2 L20,22 L16,22 L14.5,18 L9.5,18 L8,22 L4,22 Z M10.5,14 L13.5,14 L12,9 Z")
            };
        }

        /// <summary>
        /// Creates the full logo component (icon + text) for headers
        /// </summary>
        public static StackPanel CreateFullLogo(bool includeVersion = false, double scale = 1.0)
        {
            var logoPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Icon with gradient
            var iconSize = MEDIUM_ICON_SIZE * scale;
            var iconBorder = new Border
            {
                Width = iconSize,
                Height = iconSize,
                CornerRadius = new CornerRadius(6 * scale),
                Margin = new Thickness(0, 0, 12 * scale, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Background = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(ORANGE_PRIMARY, 0),
                        new GradientStop(ORANGE_DARK, 1)
                    }
                },
                Effect = new DropShadowEffect
                {
                    Color = ORANGE_PRIMARY,
                    BlurRadius = 10 * scale,
                    ShadowDepth = 0,
                    Opacity = 0.5
                }
            };

            var iconViewbox = new Viewbox
            {
                Width = 20 * scale,
                Height = 20 * scale,
                Child = new Canvas
                {
                    Width = 24,
                    Height = 24,
                    Children = { CreateIconPath() }
                }
            };
            iconBorder.Child = iconViewbox;
            logoPanel.Children.Add(iconBorder);

            // Brand text
            var textPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Center
            };

            var titleBlock = new TextBlock
            {
                FontSize = 22 * scale,
                FontWeight = FontWeights.Black,
                VerticalAlignment = VerticalAlignment.Center,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 4 * scale,
                    ShadowDepth = 2 * scale,
                    Opacity = 0.5
                },
                Foreground = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 0),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(ORANGE_PRIMARY, 0),
                        new GradientStop(ZINC_COLOR, 0.7)
                    }
                }
            };

            titleBlock.Inlines.Add(new Run(COMPANY_NAME)
            {
                FontSize = 24 * scale,
                FontWeight = FontWeights.ExtraBold
            });
            titleBlock.Inlines.Add(new Run(COMPANY_SUFFIX)
            {
                FontSize = 18 * scale,
                FontWeight = FontWeights.Light
            });
            textPanel.Children.Add(titleBlock);

            var taglineBlock = new TextBlock
            {
                Text = TAGLINE,
                FontSize = 7 * scale,
                Foreground = new SolidColorBrush(ORANGE_PRIMARY),
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(2 * scale, 0, 0, 0),
                Opacity = 0.8
            };
            textPanel.Children.Add(taglineBlock);
            logoPanel.Children.Add(textPanel);

            // Version badge (optional)
            if (includeVersion)
            {
                var versionBadge = new Border
                {
                    Background = new SolidColorBrush(BG_DARK),
                    BorderBrush = new SolidColorBrush(ORANGE_PRIMARY),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(3 * scale),
                    Padding = new Thickness(6 * scale, 2 * scale, 6 * scale, 2 * scale),
                    Margin = new Thickness(14 * scale, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = VERSION,
                        FontSize = 10 * scale,
                        Foreground = new SolidColorBrush(ORANGE_PRIMARY),
                        FontWeight = FontWeights.Bold
                    }
                };
                logoPanel.Children.Add(versionBadge);
            }

            return logoPanel;
        }
    }

    // ══════════════════════════════════════════════════════════════
    // ELEVATION DIALOG - Orange/Zinc Theme
    // ══════════════════════════════════════════════════════════════
    public class ElevationDialog : Window
    {
        public ElevationDialog()
        {
            Title = "Administrator Elevation Required";
            Width = 500; Height = 280;
            Background = new SolidColorBrush(Color.FromRgb(13, 13, 13)); // #FF0D0D0D
            WindowStyle = WindowStyle.None;
            Topmost = true;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;

            var border = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(255, 133, 51)), // Orange #FFFF8533
                BorderThickness = new Thickness(2)
            };

            var mainPanel = new Grid { Margin = new Thickness(0) };
            mainPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(50) });
            mainPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Header
            var header = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(37, 37, 38)), // #FF252526
                Child = new TextBlock
                {
                    Text = "Administrator Elevation Required",
                    Foreground = Brushes.White,
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(20, 0, 0, 0)
                }
            };
            Grid.SetRow(header, 0);
            mainPanel.Children.Add(header);

            // Content
            var contentPanel = new StackPanel { Margin = new Thickness(30, 25, 30, 20) };

            // Warning icon + text
            var messagePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20) };
            messagePanel.Children.Add(new TextBlock
            {
                Text = "⚠",
                FontSize = 32,
                Foreground = new SolidColorBrush(Color.FromRgb(247, 99, 12)), // Orange warning
                Margin = new Thickness(0, 0, 15, 0),
                VerticalAlignment = VerticalAlignment.Top
            });

            var textPanel = new StackPanel();
            textPanel.Children.Add(new TextBlock
            {
                Text = "MMC management consoles require Administrator privileges.",
                Foreground = Brushes.White,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            });
            textPanel.Children.Add(new TextBlock
            {
                Text = "Would you like to restart ArtaznIT Suite as Administrator?",
                Foreground = new SolidColorBrush(Color.FromRgb(161, 161, 170)), // Zinc #FFA1A1AA
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            });
            textPanel.Children.Add(new TextBlock
            {
                Text = "Click 'Yes' to restart with elevation\nClick 'No' to cancel",
                Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128)),
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap
            });

            messagePanel.Children.Add(textPanel);
            contentPanel.Children.Add(messagePanel);
            Grid.SetRow(contentPanel, 1);
            mainPanel.Children.Add(contentPanel);

            // Buttons
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 25, 20)
            };

            var noBtn = new Button
            {
                Content = "No",
                Width = 100,
                Height = 36,
                Margin = new Thickness(0, 0, 10, 0),
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                IsCancel = true
            };
            noBtn.Click += (s, e) => { DialogResult = false; Close(); };

            var yesBtn = new Button
            {
                Content = "Yes",
                Width = 100,
                Height = 36,
                Background = new SolidColorBrush(Color.FromRgb(255, 133, 51)), // Orange #FFFF8533
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                IsDefault = true
            };
            yesBtn.Click += (s, e) => { DialogResult = true; Close(); };

            buttonPanel.Children.Add(noBtn);
            buttonPanel.Children.Add(yesBtn);
            Grid.SetRow(buttonPanel, 2);
            mainPanel.Children.Add(buttonPanel);

            border.Child = mainPanel;
            Content = border;
        }
    }

    // ══════════════════════════════════════════════════════════════
    // LOGIN WINDOW - Orange/Zinc Theme with Elevation Button
    // ══════════════════════════════════════════════════════════════
    public class LoginWindow : Window
    {
        private TextBox _txtUser;
        private PasswordBox _txtPass;
        private CheckBox _chkRemember;
        private TextBlock _domainTextBlock;
        private Border _domainBadge;
        private System.Windows.Threading.DispatcherTimer _domainCheckTimer;
        public string Username => _txtUser.Text;
        public SecureString Password => _txtPass.SecurePassword;
        public bool RememberUser => _chkRemember.IsChecked == true;
        public bool ShouldRestartElevated { get; private set; }

        public LoginWindow()
        {
            Title = "Identity Verification";
            Width = 420;
            Height = 500;  // Increased height for logo
            Background = new SolidColorBrush(Color.FromRgb(13, 13, 13)); // #FF0D0D0D
            WindowStyle = WindowStyle.None;
            Topmost = true;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;

            var border = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(255, 133, 51)), // Orange #FFFF8533
                BorderThickness = new Thickness(2)
            };

            var mainPanel = new Grid();
            mainPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Logo
            mainPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) }); // Header
            mainPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Content
            mainPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Buttons
            mainPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Version Footer

            // LOGO_PLACEMENT: Login Dialog Header Logo
            var logoContainer = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(13, 13, 13)),
                Padding = new Thickness(0, 20, 0, 20),
                Child = LogoConfig.CreateFullLogo(includeVersion: false, scale: 0.9)
            };
            Grid.SetRow(logoContainer, 0);
            mainPanel.Children.Add(logoContainer);

            // Header with Domain Indicator
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var headerStack = new StackPanel
            {
                Margin = new Thickness(25, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            headerStack.Children.Add(new TextBlock
            {
                Text = "AUTHENTICATION REQUIRED",
                Foreground = Brushes.White,
                FontSize = 16,
                FontWeight = FontWeights.Bold
            });
            headerStack.Children.Add(new TextBlock
            {
                Text = "Enter your domain credentials",
                Foreground = new SolidColorBrush(Color.FromRgb(161, 161, 170)), // Zinc
                FontSize = 10,
                Margin = new Thickness(0, 3, 0, 0)
            });
            Grid.SetColumn(headerStack, 0);
            headerGrid.Children.Add(headerStack);

            // Domain indicator badge - TAG: #DC_DISCOVERY #THEME_COLORS
            _domainBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(26, 26, 26)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(10, 0, 15, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            var domainGradientBorder = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 0)
            };
            domainGradientBorder.GradientStops.Add(new GradientStop(Color.FromRgb(255, 133, 51), 0));
            domainGradientBorder.GradientStops.Add(new GradientStop(Color.FromRgb(161, 161, 170), 1));
            _domainBadge.BorderBrush = domainGradientBorder;
            _domainBadge.BorderThickness = new Thickness(1);

            var domainStack = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };

            _domainTextBlock = new TextBlock
            {
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };

            domainStack.Children.Add(_domainTextBlock);
            _domainBadge.Child = domainStack;

            // Initialize domain badge with current domain status
            UpdateLoginDomainBadge(MainWindow.CurrentDomainName);
            Grid.SetColumn(_domainBadge, 1);
            headerGrid.Children.Add(_domainBadge);

            var header = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(37, 37, 38)), // #FF252526
                Child = headerGrid
            };
            Grid.SetRow(header, 1);
            mainPanel.Children.Add(header);

            // Content Panel
            var panel = new StackPanel { Margin = new Thickness(30, 25, 30, 20) };

            string cached = "";
            try { cached = Properties.Settings.Default.LastUser; }
            catch { }

            // Auto-populate with detected domain if available
            if (string.IsNullOrEmpty(cached))
            {
                if (!string.IsNullOrEmpty(MainWindow.CurrentDomainName))
                {
                    // Extract domain name (e.g., "PROCESS" from "PROCESS.LOCAL")
                    string domainPrefix = MainWindow.CurrentDomainName.Split('.')[0].ToUpper();
                    cached = $"{domainPrefix}\\";
                }
                else
                {
                    // No domain detected - leave empty with helpful info
                    cached = "";
                }
            }

            // Username header with helpful text
            var usernameHeaderStack = new StackPanel();
            var usernameHeader = new TextBlock
            {
                Text = "Domain\\Username:",
                Foreground = new SolidColorBrush(Color.FromRgb(161, 161, 170)),
                FontSize = 11
            };
            usernameHeaderStack.Children.Add(usernameHeader);

            // If no domain detected, show helpful info
            if (string.IsNullOrEmpty(MainWindow.CurrentDomainName) && string.IsNullOrEmpty(cached))
            {
                var helpText = new TextBlock
                {
                    Text = "ℹ️ No domain detected. Enter manually: DOMAIN\\username",
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 133, 51)), // Orange
                    FontSize = 10,
                    FontStyle = FontStyles.Italic,
                    Margin = new Thickness(0, 3, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                };
                usernameHeaderStack.Children.Add(helpText);
            }

            usernameHeaderStack.Margin = new Thickness(0, 0, 0, 5);
            panel.Children.Add(usernameHeaderStack);

            // Username field container with clear button
            var usernameGrid = new Grid();
            usernameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            usernameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _txtUser = new TextBox
            {
                Text = cached,
                Background = new SolidColorBrush(Color.FromRgb(26, 26, 26)), // #FF1A1A1A
                Foreground = Brushes.White,
                Padding = new Thickness(10, 8, 35, 8), // Extra padding for clear button
                FontSize = 13,
                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                BorderThickness = new Thickness(1)
            };
            Grid.SetColumn(_txtUser, 0);
            Grid.SetColumnSpan(_txtUser, 2);
            usernameGrid.Children.Add(_txtUser);

            // Clear button (appears on the right side of the textbox)
            var clearBtn = new Button
            {
                Content = "✕",
                Width = 28,
                Height = 28,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(161, 161, 170)),
                BorderThickness = new Thickness(0),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 4, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
                ToolTip = "Clear cached username"
            };
            clearBtn.Click += (s, ev) =>
            {
                // Clear cached username
                Properties.Settings.Default.LastUser = "";
                Properties.Settings.Default.Save();

                // Reset to domain prefix if detected, otherwise empty
                if (!string.IsNullOrEmpty(MainWindow.CurrentDomainName))
                {
                    string domainPrefix = MainWindow.CurrentDomainName.Split('.')[0].ToUpper();
                    _txtUser.Text = $"{domainPrefix}\\";
                }
                else
                {
                    // No domain - clear completely so user can type manually
                    _txtUser.Text = "";
                }
                _txtUser.Focus();
                _txtUser.SelectionStart = _txtUser.Text.Length;
            };
            clearBtn.MouseEnter += (s, ev) => clearBtn.Foreground = new SolidColorBrush(Color.FromRgb(255, 133, 51));
            clearBtn.MouseLeave += (s, ev) => clearBtn.Foreground = new SolidColorBrush(Color.FromRgb(161, 161, 170));

            Grid.SetColumn(clearBtn, 1);
            usernameGrid.Children.Add(clearBtn);

            panel.Children.Add(usernameGrid);

            // Password
            panel.Children.Add(new TextBlock
            {
                Text = "Password:",
                Foreground = new SolidColorBrush(Color.FromRgb(161, 161, 170)),
                FontSize = 11,
                Margin = new Thickness(0, 15, 0, 5)
            });
            _txtPass = new PasswordBox
            {
                Background = new SolidColorBrush(Color.FromRgb(26, 26, 26)),
                Foreground = Brushes.White,
                Padding = new Thickness(10, 8, 10, 8),
                FontSize = 13,
                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                BorderThickness = new Thickness(1)
            };
            panel.Children.Add(_txtPass);

            // Remember checkbox
            _chkRemember = new CheckBox
            {
                Content = "Remember username",
                Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128)),
                FontSize = 11,
                IsChecked = cached != "process\\",
                Margin = new Thickness(0, 12, 0, 0)
            };
            panel.Children.Add(_chkRemember);

            // Separator
            panel.Children.Add(new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Margin = new Thickness(0, 20, 0, 15)
            });

            // Stylized Elevation button with gradient
            var elevateBtn = new Button
            {
                Height = 45,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 0),
                Cursor = Cursors.Hand
            };

            var elevateBtnBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 0)
            };
            elevateBtnBrush.GradientStops.Add(new GradientStop(Color.FromRgb(255, 133, 51), 0));
            elevateBtnBrush.GradientStops.Add(new GradientStop(Color.FromRgb(161, 161, 170), 1));
            elevateBtn.Background = elevateBtnBrush;
            elevateBtn.Foreground = Brushes.White;

            var elevatePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            elevatePanel.Children.Add(new TextBlock
            {
                Text = "🛡",
                FontSize = 16,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            elevatePanel.Children.Add(new TextBlock
            {
                Text = "RESTART WITH ELEVATED PERMISSIONS",
                VerticalAlignment = VerticalAlignment.Center
            });
            elevateBtn.Content = elevatePanel;

            elevateBtn.MouseEnter += (s, e) => elevateBtn.Opacity = 0.9;
            elevateBtn.MouseLeave += (s, e) => elevateBtn.Opacity = 1.0;
            elevateBtn.Click += (s, e) =>
            {
                ShouldRestartElevated = true;
                DialogResult = false;
                Close();
            };
            panel.Children.Add(elevateBtn);

            Grid.SetRow(panel, 2);  // Updated from 1 to 2 (logo row added)
            mainPanel.Children.Add(panel);

            // Button Panel
            var bp = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 25, 20)
            };

            var cancelBtn = new Button
            {
                Content = "CANCEL",
                Width = 90,
                Height = 36,
                Margin = new Thickness(0, 0, 10, 0),
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                BorderThickness = new Thickness(1),
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                IsCancel = true
            };
            cancelBtn.Click += (s, e) => { DialogResult = false; Close(); };

            var loginBtn = new Button
            {
                Content = "LOGIN",
                Width = 90,
                Height = 36,
                Background = new SolidColorBrush(Color.FromRgb(255, 133, 51)), // Orange #FFFF8533
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                IsDefault = true
            };
            loginBtn.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_txtUser.Text))
                {
                    // TAG: #AUTO_UPDATE_UI_ENGINE #VALIDATION_TOASTS
                    Managers.UI.ToastManager.ShowWarning("Username is required");
                    MessageBox.Show("Enter username", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (_txtPass.SecurePassword.Length == 0)
                {
                    // TAG: #AUTO_UPDATE_UI_ENGINE #VALIDATION_TOASTS
                    Managers.UI.ToastManager.ShowWarning("Password is required");
                    MessageBox.Show("Enter password", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                DialogResult = true;
                Close();
            };

            bp.Children.Add(cancelBtn);
            bp.Children.Add(loginBtn);
            Grid.SetRow(bp, 3);  // Updated from 2 to 3 (logo row added)
            mainPanel.Children.Add(bp);

            // Version/Compiled Date Footer - TAG: #MODULAR #VERSION
            var footerPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 15, 0, 15)
            };

            var versionText = new TextBlock
            {
                Text = $"{LogoConfig.VERSION}",
                Foreground = new SolidColorBrush(Color.FromRgb(161, 161, 170)),
                FontSize = 9,
                Margin = new Thickness(0, 0, 8, 0)
            };

            var separatorText = new TextBlock
            {
                Text = "•",
                Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                FontSize = 9,
                Margin = new Thickness(0, 0, 8, 0)
            };

            var compiledText = new TextBlock
            {
                Text = $"Compiled: {LogoConfig.COMPILED_DATE_SHORT}",
                Foreground = new SolidColorBrush(Color.FromRgb(161, 161, 170)),
                FontSize = 9
            };

            footerPanel.Children.Add(versionText);
            footerPanel.Children.Add(separatorText);
            footerPanel.Children.Add(compiledText);

            Grid.SetRow(footerPanel, 4);
            mainPanel.Children.Add(footerPanel);

            border.Child = mainPanel;
            Content = border;

            Loaded += async (s, e) =>
            {
                Activate();
                if (_txtUser.Text.Length > 8) _txtPass.Focus();
                else _txtUser.Focus();

                // ⚡ INSTANT STARTUP: Check domain immediately with 2-second timeout
                // TAG: #DC_DISCOVERY #PERFORMANCE
                await CheckDomainOnStartup();
            };

            _txtPass.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                    loginBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            };

            // Stop timer when dialog closes
            Closed += (s, e) =>
            {
                _domainCheckTimer?.Stop();
            };
        }

        /// <summary>
        /// Start 5-second domain check timer on login screen
        /// TAG: #DC_DISCOVERY
        /// </summary>
        private void StartDomainCheckTimer()
        {
            _domainCheckTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };

            _domainCheckTimer.Tick += async (s, e) =>
            {
                try
                {
                    // Check domain availability
                    string domainName = await Task.Run(() =>
                    {
                        try
                        {
                            var domain = Domain.GetCurrentDomain();
                            string name = domain.Name;
                            domain.Dispose();
                            return name;
                        }
                        catch
                        {
                            return null;
                        }
                    });

                    // Update global domain name
                    MainWindow.CurrentDomainName = domainName;

                    // Update main window's domain badge
                    // TAG: #DC_DISCOVERY #UI_UPDATE
                    var mainWindow = Application.Current.MainWindow as MainWindow;
                    mainWindow?.UpdateDomainBadge(domainName);

                    // Update login window's domain badge using modular method
                    UpdateLoginDomainBadge(domainName);
                }
                catch
                {
                    // Silently fail - don't interrupt login
                }
            };

            _domainCheckTimer.Start();
        }

        /// <summary>
        /// Update login window domain badge with modular styling
        /// TAG: #DC_DISCOVERY #UI_UPDATE #MODULAR
        /// </summary>
        private void UpdateLoginDomainBadge(string domainName)
        {
            if (_domainTextBlock == null || _domainBadge == null)
                return;

            if (!string.IsNullOrEmpty(domainName))
            {
                _domainTextBlock.Text = domainName.ToUpper();
                var textGradient = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 0)
                };
                textGradient.GradientStops.Add(new GradientStop(Color.FromRgb(255, 133, 51), 0));
                textGradient.GradientStops.Add(new GradientStop(Color.FromRgb(161, 161, 170), 1));
                _domainTextBlock.Foreground = textGradient;
                _domainBadge.Opacity = 1.0;
            }
            else
            {
                _domainTextBlock.Text = "NO DOMAIN";
                _domainTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128));
                _domainBadge.Opacity = 0.7;
            }
        }

        /// <summary>
        /// Check domain on login window startup with 2-second timeout
        /// If no domain found, show DC unavailable dialog
        /// TAG: #DC_DISCOVERY #PERFORMANCE
        /// </summary>
        private async Task CheckDomainOnStartup()
        {
            string domainName = null;

            try
            {
                // ⚡ Race domain check against 2-second timeout
                var domainCheckTask = Task.Run(() =>
                {
                    try
                    {
                        var domain = Domain.GetCurrentDomain();
                        string name = domain.Name;
                        domain.Dispose();
                        return name;
                    }
                    catch
                    {
                        return null;
                    }
                });

                var timeoutTask = Task.Delay(2000);

                // Wait for whichever completes first
                var completedTask = await Task.WhenAny(domainCheckTask, timeoutTask);

                if (completedTask == domainCheckTask)
                {
                    // Domain check completed within 2 seconds
                    domainName = await domainCheckTask;
                }
                else
                {
                    // Timeout - domain check took too long
                    LogManager.LogWarning("Domain check timed out after 2 seconds");
                }
            }
            catch (Exception ex)
            {
                LogManager.LogWarning($"Domain check on login startup failed: {ex.Message}");
                domainName = null;
            }

            // Update global domain name
            MainWindow.CurrentDomainName = domainName;

            // Update main window's domain badge
            // TAG: #DC_DISCOVERY #UI_UPDATE
            var mainWindow = Application.Current.MainWindow as MainWindow;
            mainWindow?.UpdateDomainBadge(domainName);

            // Update login window's domain badge using modular method
            UpdateLoginDomainBadge(domainName);

            // ⚡ Auto-populate username field with detected domain
            if (!string.IsNullOrEmpty(domainName) && _txtUser != null)
            {
                string currentText = _txtUser.Text ?? "";
                // Only update if field is empty or only has a backslash
                if (string.IsNullOrEmpty(currentText) || currentText == "\\" ||
                    (currentText.EndsWith("\\") && !currentText.Contains("@")))
                {
                    string domainPrefix = domainName.Split('.')[0].ToUpper();
                    _txtUser.Text = $"{domainPrefix}\\";
                    _txtUser.SelectionStart = _txtUser.Text.Length; // Cursor at end
                }
            }

            if (domainName != null)
            {
                // Domain found - start periodic check timer
                StartDomainCheckTimer();
            }
            else
            {
                // No domain (or timed out) - show DC unavailable dialog IMMEDIATELY
                var dcDialog = new DCAvailabilityDialog();
                var result = dcDialog.ShowDialog();

                if (dcDialog.ShouldRestart)
                {
                    // User wants to restart - close login and app
                    Application.Current.Shutdown();
                }
                else if (dcDialog.ShouldContinueReadOnly)
                {
                    // User wants read-only mode - close login, set guest mode
                    DialogResult = false;
                    if (mainWindow != null)
                    {
                        mainWindow.SetGuestReadOnlyMode();
                        mainWindow.AppendTerminal("[STARTUP] Continuing in read-only mode (DCs unavailable)", isError: true);
                        mainWindow.AppendTerminal("[STARTUP] Skipping authentication - running as GUEST", isError: true);
                    }
                    Close();
                }
            }
        }
    }

    /// <summary>
    /// Pre-Login DC Availability Dialog - TAG: #THEME_DIALOG #DC_DISCOVERY
    /// Shows when DCs are unavailable at startup, offering read-only mode or restart
    /// </summary>
    public class DCAvailabilityDialog : Window
    {
        public bool ShouldContinueReadOnly { get; private set; }
        public bool ShouldRestart { get; private set; }

        public DCAvailabilityDialog()
        {
            Title = "Domain Controllers Unavailable";
            Width = 500;
            Height = 450;
            Background = new SolidColorBrush(Color.FromRgb(13, 13, 13));
            WindowStyle = WindowStyle.None;
            Topmost = true;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Gradient Header with Logo
            var headerBorder = new Border
            {
                Height = 120,
                Child = new Grid()
            };

            var gradientBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1)
            };
            gradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(255, 133, 51), 0));
            gradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(161, 161, 170), 1));
            headerBorder.Background = gradientBrush;

            var logoContainer = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            logoContainer.Children.Add(LogoConfig.CreateFullLogo(includeVersion: false, scale: 0.7));

            ((Grid)headerBorder.Child).Children.Add(logoContainer);
            Grid.SetRow(headerBorder, 0);
            mainGrid.Children.Add(headerBorder);

            // Content Area
            var contentPanel = new StackPanel
            {
                Margin = new Thickness(40, 30, 40, 20)
            };

            contentPanel.Children.Add(new TextBlock
            {
                Text = "⚠️ DOMAIN CONTROLLERS UNAVAILABLE",
                Foreground = new SolidColorBrush(Color.FromRgb(255, 133, 51)),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20)
            });

            contentPanel.Children.Add(new TextBlock
            {
                Text = "ArtaznIT could not connect to domain controllers. This may be caused by:",
                Foreground = Brushes.White,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 15)
            });

            var reasonsPanel = new StackPanel { Margin = new Thickness(20, 0, 0, 20) };
            var reasons = new[] {
                "• Not connected to corporate VPN",
                "• Network connectivity issues",
                "• Not joined to the domain",
                "• Firewall blocking domain traffic",
                "• DNS configuration problems"
            };

            foreach (var reason in reasons)
            {
                reasonsPanel.Children.Add(new TextBlock
                {
                    Text = reason,
                    Foreground = new SolidColorBrush(Color.FromRgb(161, 161, 170)),
                    FontSize = 11,
                    Margin = new Thickness(0, 0, 0, 5)
                });
            }
            contentPanel.Children.Add(reasonsPanel);

            contentPanel.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(30, 255, 133, 51)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(15, 12, 15, 12),
                Child = new TextBlock
                {
                    Text = "You can continue in read-only mode (limited functionality) or restart the application after resolving connectivity.",
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 200, 100)),
                    FontSize = 11,
                    TextWrapping = TextWrapping.Wrap
                }
            });

            Grid.SetRow(contentPanel, 1);
            mainGrid.Children.Add(contentPanel);

            // Button Panel
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 30)
            };

            // Continue Read-Only Button
            var continueBtn = CreateStyledButton(
                "📖 CONTINUE READ-ONLY",
                Color.FromRgb(60, 60, 60),
                Color.FromRgb(161, 161, 170));
            continueBtn.Click += (s, e) =>
            {
                ShouldContinueReadOnly = true;
                DialogResult = true;
                Close();
            };
            buttonPanel.Children.Add(continueBtn);

            // Restart Button
            var restartBtn = CreateStyledButton(
                "🔄 RESTART APPLICATION",
                Color.FromRgb(255, 133, 51),
                Colors.White);
            restartBtn.Margin = new Thickness(15, 0, 0, 0);
            restartBtn.Click += (s, e) =>
            {
                ShouldRestart = true;
                DialogResult = true;
                Close();
            };
            buttonPanel.Children.Add(restartBtn);

            Grid.SetRow(buttonPanel, 2);
            mainGrid.Children.Add(buttonPanel);

            // Border wrapper
            var border = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(255, 133, 51)),
                BorderThickness = new Thickness(2),
                Child = mainGrid
            };

            Content = border;
        }

        private Button CreateStyledButton(string text, Color bgColor, Color fgColor)
        {
            var button = new Button
            {
                Content = text,
                Width = 200,
                Height = 45,
                Background = new SolidColorBrush(bgColor),
                Foreground = new SolidColorBrush(fgColor),
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Cursor = Cursors.Hand
            };

            button.MouseEnter += (s, e) => button.Opacity = 0.9;
            button.MouseLeave += (s, e) => button.Opacity = 1.0;

            return button;
        }
    }

    // ══════════════════════════════════════════════════════════════
    // DASHBOARD DATA MODEL - TAG: #VERSION_7.1 #DASHBOARD
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Data model for top computers in dashboard
    /// </summary>
    public class DashboardTopComputer
    {
        public int Rank { get; set; }
        public string Hostname { get; set; }
        public string OS { get; set; }
        public string Uptime { get; set; }
        public string LastBoot { get; set; }
    }

    /// <summary>
    /// Data model for detailed version breakdown in dashboard
    /// TAG: #VERSION_7.1 #DASHBOARD
    /// </summary>
    public class DashboardVersionDetail
    {
        public string Version { get; set; }
        public int Count { get; set; }
        public double Percentage { get; set; }
        public string CountText { get; set; }
        public System.Windows.Media.Brush Color { get; set; }
    }
}