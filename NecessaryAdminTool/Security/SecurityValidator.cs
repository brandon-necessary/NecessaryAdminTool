using System;
using System.IO;
using System.Linq;
// TAG: #SECURITY_CRITICAL #INPUT_VALIDATION #DEFENSE_IN_DEPTH #VERSION_2_0

namespace NecessaryAdminTool.Security
{
    /// <summary>
    /// Input validation and sanitization for security-critical operations
    /// Implements OWASP input validation best practices
    /// TAG: #SECURITY #INPUT_VALIDATION #OWASP_TOP_10
    /// </summary>
    public static class SecurityValidator
    {
        /// <summary>
        /// Validate hostname (DNS-safe characters only)
        /// Prevents injection attacks and ensures valid DNS names
        /// TAG: #SECURITY_CRITICAL #HOSTNAME_VALIDATION
        /// </summary>
        /// <param name="hostname">Hostname to validate</param>
        /// <returns>True if hostname is valid</returns>
        public static bool IsValidHostname(string hostname)
        {
            if (string.IsNullOrWhiteSpace(hostname))
            {
                LogManager.LogWarning("[SecurityValidator] Hostname validation failed: null or empty");
                return false;
            }

            if (hostname.Length > 255)
            {
                LogManager.LogWarning($"[SecurityValidator] Hostname validation failed: exceeds 255 characters ({hostname.Length})");
                return false;
            }

            // DNS-safe characters: letters, digits, hyphens, dots
            var validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-.";
            bool isValid = hostname.All(c => validChars.Contains(c));

            if (!isValid)
            {
                LogManager.LogWarning($"[SecurityValidator] Hostname validation failed: contains invalid characters");
            }

            return isValid;
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
                return IsValidDomainName(parts[0]) && IsValidUserPart(parts[1]);
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
                return IsValidUserPart(parts[0]) && IsValidDomainName(parts[1]);
            }
            // Format 3: user (no domain)
            else
            {
                return IsValidUserPart(username);
            }
        }

        /// <summary>
        /// Validate domain name (DNS-compatible)
        /// </summary>
        private static bool IsValidDomainName(string domain)
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
        /// Sanitize LDAP filter input to prevent LDAP injection
        /// TAG: #SECURITY_CRITICAL #LDAP_INJECTION
        /// </summary>
        /// <param name="input">LDAP filter input to sanitize</param>
        /// <returns>Sanitized string safe for LDAP queries</returns>
        public static string SanitizeLdapInput(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            // LDAP special characters that need escaping
            input = input.Replace("\\", "\\5c");
            input = input.Replace("*", "\\2a");
            input = input.Replace("(", "\\28");
            input = input.Replace(")", "\\29");
            input = input.Replace("\0", "\\00");

            return input;
        }

        /// <summary>
        /// Validate computer name (NetBIOS compatible)
        /// TAG: #SECURITY_CRITICAL #COMPUTER_NAME_VALIDATION
        /// </summary>
        /// <param name="computerName">Computer name to validate</param>
        /// <returns>True if computer name is valid</returns>
        public static bool IsValidComputerName(string computerName)
        {
            if (string.IsNullOrWhiteSpace(computerName)) return false;

            // NetBIOS name restrictions: 15 characters max, alphanumeric + hyphen
            if (computerName.Length > 15)
            {
                LogManager.LogWarning($"[SecurityValidator] Computer name validation failed: exceeds 15 characters");
                return false;
            }

            // Allow only alphanumeric and hyphens
            bool isValid = computerName.All(c => char.IsLetterOrDigit(c) || c == '-');

            if (!isValid)
            {
                LogManager.LogWarning($"[SecurityValidator] Computer name validation failed: contains invalid characters");
            }

            return isValid;
        }

        /// <summary>
        /// Rate limiter to prevent brute force attacks
        /// TAG: #SECURITY_CRITICAL #RATE_LIMITING #BRUTE_FORCE_PROTECTION
        /// </summary>
        public class RateLimiter
        {
            private readonly int _maxAttempts;
            private readonly TimeSpan _timeWindow;
            private readonly System.Collections.Concurrent.ConcurrentDictionary<string, AttemptInfo> _attempts;

            private class AttemptInfo
            {
                public int Count { get; set; }
                public DateTime FirstAttempt { get; set; }
                public DateTime? BlockedUntil { get; set; }
            }

            public RateLimiter(int maxAttempts = 5, TimeSpan? timeWindow = null)
            {
                _maxAttempts = maxAttempts;
                _timeWindow = timeWindow ?? TimeSpan.FromMinutes(5);
                _attempts = new System.Collections.Concurrent.ConcurrentDictionary<string, AttemptInfo>();
            }

            /// <summary>
            /// Check if operation is allowed for the given identifier
            /// </summary>
            public bool IsAllowed(string identifier)
            {
                var now = DateTime.UtcNow;
                var info = _attempts.GetOrAdd(identifier, _ => new AttemptInfo
                {
                    Count = 0,
                    FirstAttempt = now
                });

                // Check if currently blocked
                if (info.BlockedUntil.HasValue && now < info.BlockedUntil.Value)
                {
                    LogManager.LogWarning($"[RateLimiter] Operation blocked for {identifier} until {info.BlockedUntil.Value}");
                    return false;
                }

                // Reset if outside time window
                if (now - info.FirstAttempt > _timeWindow)
                {
                    info.Count = 0;
                    info.FirstAttempt = now;
                    info.BlockedUntil = null;
                }

                // Increment and check
                info.Count++;
                if (info.Count > _maxAttempts)
                {
                    // Block for exponential backoff
                    var blockDuration = TimeSpan.FromMinutes(Math.Pow(2, Math.Min(info.Count - _maxAttempts, 5)));
                    info.BlockedUntil = now.Add(blockDuration);
                    LogManager.LogWarning($"[RateLimiter] Rate limit exceeded for {identifier}. Blocked for {blockDuration.TotalMinutes:F1} minutes");
                    return false;
                }

                return true;
            }

            /// <summary>
            /// Reset rate limit for identifier (e.g., after successful auth)
            /// </summary>
            public void Reset(string identifier)
            {
                _attempts.TryRemove(identifier, out _);
            }
        }
    }
}
