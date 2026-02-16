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
        // TAG: #SECURITY_CRITICAL #RATE_LIMITING #BRUTE_FORCE_PREVENTION
        // Global rate limiter for authentication attempts
        private static readonly RateLimiter _authRateLimiter = new RateLimiter(
            maxAttempts: 5,
            timeWindow: TimeSpan.FromMinutes(5)
        );

        /// <summary>
        /// Check if authentication is allowed for the given username (rate limiting)
        /// TAG: #SECURITY_CRITICAL #RATE_LIMITING #BRUTE_FORCE_PREVENTION
        /// </summary>
        /// <param name="username">Username attempting authentication</param>
        /// <returns>True if authentication attempt is allowed, false if rate limited</returns>
        public static bool CheckRateLimit(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                LogManager.LogWarning("[SecurityValidator] Rate limit check failed: null or empty username");
                return false;
            }

            // Use username as rate limit identifier
            string identifier = $"auth:{username.ToLowerInvariant()}";
            bool allowed = _authRateLimiter.IsAllowed(identifier);

            if (!allowed)
            {
                LogManager.LogWarning($"[SecurityValidator] Authentication rate limit exceeded for user: {username}");
            }

            return allowed;
        }

        /// <summary>
        /// Reset rate limit for a user (call after successful authentication)
        /// TAG: #SECURITY_CRITICAL #RATE_LIMITING
        /// </summary>
        /// <param name="username">Username to reset rate limit for</param>
        public static void ResetRateLimit(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return;

            string identifier = $"auth:{username.ToLowerInvariant()}";
            _authRateLimiter.Reset(identifier);
            LogManager.LogInfo($"[SecurityValidator] Rate limit reset for user: {username}");
        }

        /// <summary>
        /// Validate username format (wrapper for IsValidUsername with clearer name)
        /// TAG: #SECURITY_CRITICAL #USERNAME_VALIDATION
        /// </summary>
        /// <param name="username">Username to validate</param>
        /// <returns>True if username format is valid</returns>
        public static bool ValidateUsername(string username)
        {
            return IsValidUsername(username);
        }

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
            var dangerousPatterns = new[]
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

            foreach (var pattern in dangerousPatterns)
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
        /// Escape LDAP search filter to prevent injection attacks
        /// More comprehensive escaping including all RFC 2254 special characters
        /// TAG: #SECURITY_CRITICAL #LDAP_INJECTION_PREVENTION
        /// </summary>
        /// <param name="input">User input to escape</param>
        /// <returns>Escaped string safe for LDAP search filters</returns>
        public static string EscapeLDAPSearchFilter(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            // RFC 2254 - escape special characters in LDAP search filters
            // Must escape in this order to prevent double-escaping
            var sb = new System.Text.StringBuilder();
            foreach (char c in input)
            {
                switch (c)
                {
                    case '\\':
                        sb.Append("\\5c");
                        break;
                    case '*':
                        sb.Append("\\2a");
                        break;
                    case '(':
                        sb.Append("\\28");
                        break;
                    case ')':
                        sb.Append("\\29");
                        break;
                    case '\0':
                        sb.Append("\\00");
                        break;
                    default:
                        // Also escape non-ASCII characters to prevent encoding attacks
                        if (c < 32 || c > 126)
                        {
                            sb.Append($"\\{((int)c).ToString("x2")}");
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Validate LDAP filter string to detect injection attempts
        /// TAG: #SECURITY_CRITICAL #LDAP_INJECTION_PREVENTION
        /// </summary>
        /// <param name="filter">LDAP filter to validate</param>
        /// <returns>True if filter appears safe</returns>
        public static bool ValidateLDAPFilter(string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                LogManager.LogWarning("[SecurityValidator] LDAP filter validation failed: null or empty");
                return false;
            }

            // Check for balanced parentheses (basic injection detection)
            int openCount = 0;
            int closeCount = 0;
            foreach (char c in filter)
            {
                if (c == '(') openCount++;
                if (c == ')') closeCount++;
            }

            if (openCount != closeCount)
            {
                LogManager.LogWarning($"[SecurityValidator] LDAP filter validation failed: unbalanced parentheses (open={openCount}, close={closeCount})");
                return false;
            }

            // Check for common injection patterns
            var suspiciousPatterns = new[]
            {
                "*)(",           // Wildcard injection to break out of filter
                "*)(|",          // OR injection attempt
                "*)(objectClass=*", // Object class enumeration attack
                "*)(&",          // AND injection attempt
                "*))%00",        // Null byte injection
                "admin*",        // Admin enumeration (context-dependent)
                "*)(uid=*",      // User enumeration attack
                "*)(cn=*"        // Common name enumeration attack
            };

            foreach (var pattern in suspiciousPatterns)
            {
                if (filter.Contains(pattern))
                {
                    LogManager.LogWarning($"[SecurityValidator] LDAP filter validation failed: suspicious pattern detected: {pattern}");
                    return false;
                }
            }

            // Check for null bytes
            if (filter.Contains("\0"))
            {
                LogManager.LogWarning("[SecurityValidator] LDAP filter validation failed: null byte detected");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validate and sanitize OU (Organizational Unit) filter input
        /// TAG: #SECURITY_CRITICAL #LDAP_INJECTION_PREVENTION
        /// </summary>
        /// <param name="ouFilter">OU filter string to validate</param>
        /// <returns>True if OU filter is valid</returns>
        public static bool ValidateOUFilter(string ouFilter)
        {
            if (string.IsNullOrWhiteSpace(ouFilter))
            {
                return true; // Empty/null is acceptable (means no filter)
            }

            // OU filters should only contain safe DN characters
            // Allow: alphanumeric, spaces, hyphens, underscores, equals, commas
            foreach (char c in ouFilter)
            {
                if (!char.IsLetterOrDigit(c) && c != ' ' && c != '-' && c != '_' &&
                    c != '=' && c != ',' && c != '.')
                {
                    LogManager.LogWarning($"[SecurityValidator] OU filter validation failed: invalid character '{c}'");
                    return false;
                }
            }

            // Check for LDAP injection patterns
            if (ouFilter.Contains(")(") || ouFilter.Contains("*)"))
            {
                LogManager.LogWarning("[SecurityValidator] OU filter validation failed: LDAP injection pattern detected");
                return false;
            }

            return true;
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
        /// Alias for IsValidComputerName for consistency with security audit recommendations
        /// TAG: #SECURITY_CRITICAL #COMPUTER_NAME_VALIDATION
        /// </summary>
        /// <param name="computerName">Computer name to validate</param>
        /// <returns>True if computer name is valid</returns>
        public static bool ValidateComputerName(string computerName)
        {
            return IsValidComputerName(computerName);
        }

        /// <summary>
        /// Validate file path to prevent path traversal attacks (main method)
        /// Ensures file path is within allowed base directory
        /// TAG: #SECURITY_CRITICAL #FILE_PATH_VALIDATION #PATH_TRAVERSAL
        /// </summary>
        /// <param name="filePath">File path to validate</param>
        /// <param name="allowedBasePath">Allowed base directory</param>
        /// <returns>True if file path is valid</returns>
        public static bool ValidateFilePath(string filePath, string allowedBasePath)
        {
            // TAG: #SECURITY_CRITICAL - Path traversal prevention
            return IsValidFilePath(filePath, allowedBasePath);
        }

        /// <summary>
        /// Validate filter pattern to prevent wildcard injection attacks
        /// TAG: #SECURITY_CRITICAL #FILTER_SYSTEM #WILDCARD_INJECTION_PREVENTION
        /// </summary>
        /// <param name="pattern">Filter pattern to validate (supports * and ?)</param>
        /// <returns>True if pattern is safe</returns>
        public static bool ValidateFilterPattern(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return true; // Empty pattern is allowed (no filter)
            }

            // Limit pattern length to prevent ReDoS attacks
            if (pattern.Length > 100)
            {
                LogManager.LogWarning($"[SecurityValidator] Filter pattern validation failed: exceeds 100 characters ({pattern.Length})");
                return false;
            }

            // Allow only safe characters: alphanumeric, wildcards (*, ?), hyphens, underscores, dots, spaces
            var allowedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789*?-_. ";
            bool isValid = pattern.All(c => allowedChars.Contains(c));

            if (!isValid)
            {
                LogManager.LogWarning($"[SecurityValidator] Filter pattern validation failed: contains invalid characters");
                return false;
            }

            // Prevent excessive wildcards (DoS prevention)
            int wildcardCount = pattern.Count(c => c == '*' || c == '?');
            if (wildcardCount > 10)
            {
                LogManager.LogWarning($"[SecurityValidator] Filter pattern validation failed: too many wildcards ({wildcardCount})");
                return false;
            }

            // Prevent patterns like ***** (catastrophic backtracking)
            if (pattern.Contains("***") || pattern.Contains("???"))
            {
                LogManager.LogWarning("[SecurityValidator] Filter pattern validation failed: excessive consecutive wildcards");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validate OU path to prevent LDAP injection attacks
        /// TAG: #SECURITY_CRITICAL #FILTER_SYSTEM #LDAP_INJECTION_PREVENTION
        /// </summary>
        /// <param name="ouPath">OU path to validate</param>
        /// <returns>True if OU path is safe</returns>
        public static bool ValidateOUPath(string ouPath)
        {
            if (string.IsNullOrWhiteSpace(ouPath))
            {
                return true; // Empty is acceptable (no filter)
            }

            // Limit length to prevent buffer overflow
            if (ouPath.Length > 500)
            {
                LogManager.LogWarning($"[SecurityValidator] OU path validation failed: exceeds 500 characters ({ouPath.Length})");
                return false;
            }

            // Allow only safe DN characters: alphanumeric, spaces, hyphens, underscores, equals, commas, dots
            foreach (char c in ouPath)
            {
                if (!char.IsLetterOrDigit(c) && c != ' ' && c != '-' && c != '_' &&
                    c != '=' && c != ',' && c != '.')
                {
                    LogManager.LogWarning($"[SecurityValidator] OU path validation failed: invalid character '{c}'");
                    return false;
                }
            }

            // Check for LDAP injection patterns
            var suspiciousPatterns = new[] { ")(", "*)", "(*", "|(", "&(" };
            foreach (var pattern in suspiciousPatterns)
            {
                if (ouPath.Contains(pattern))
                {
                    LogManager.LogWarning($"[SecurityValidator] OU path validation failed: LDAP injection pattern detected: {pattern}");
                    return false;
                }
            }

            // Check for null bytes
            if (ouPath.Contains("\0"))
            {
                LogManager.LogWarning("[SecurityValidator] OU path validation failed: null byte detected");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validate numeric filter value (range validation) - overload for int values
        /// TAG: #SECURITY_CRITICAL #FILTER_SYSTEM #NUMERIC_VALIDATION
        /// </summary>
        /// <param name="value">Numeric value to validate</param>
        /// <param name="min">Minimum allowed value</param>
        /// <param name="max">Maximum allowed value</param>
        /// <returns>True if value is within valid range</returns>
        public static bool ValidateNumericFilter(int value, int min, int max)
        {
            if (value < min || value > max)
            {
                LogManager.LogWarning($"[SecurityValidator] Numeric filter validation failed: value {value} outside range [{min}, {max}]");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validate numeric filter value (string to int with range validation)
        /// TAG: #SECURITY_CRITICAL #FILTER_SYSTEM #NUMERIC_VALIDATION #INPUT_PARSING
        /// </summary>
        /// <param name="value">String numeric value to validate</param>
        /// <param name="min">Minimum allowed value</param>
        /// <param name="max">Maximum allowed value</param>
        /// <returns>True if value is valid and within range</returns>
        public static bool ValidateNumericFilter(string value, int min, int max)
        {
            // TAG: #SECURITY_CRITICAL
            if (string.IsNullOrWhiteSpace(value))
            {
                LogManager.LogWarning("[SecurityValidator] Numeric filter validation failed: null or empty value");
                return false;
            }

            // TAG: #SECURITY_CRITICAL - Attempt to parse the string value to int
            if (!int.TryParse(value, out int intValue))
            {
                LogManager.LogWarning($"[SecurityValidator] Numeric filter validation failed: '{value}' is not a valid integer");
                return false;
            }

            // TAG: #SECURITY_CRITICAL - Validate the range
            if (intValue < min || intValue > max)
            {
                LogManager.LogWarning($"[SecurityValidator] Numeric filter validation failed: value {intValue} outside range [{min}, {max}]");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validate file path (alias for compatibility)
        /// TAG: #SECURITY_CRITICAL #PATH_VALIDATION
        /// </summary>
        public static bool IsValidPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            try
            {
                Path.GetFullPath(path);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Sanitize hostname for safe usage
        /// TAG: #SECURITY_CRITICAL #HOSTNAME_SANITIZATION
        /// </summary>
        public static string SanitizeHostname(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            return System.Text.RegularExpressions.Regex.Replace(input.Trim(), @"[^a-zA-Z0-9\-\.]", "");
        }

        /// <summary>
        /// Escape CSV value for safe output
        /// TAG: #SECURITY_CRITICAL #CSV_INJECTION_PREVENTION
        /// </summary>
        public static string EscapeCsv(string value)
        {
            return (value ?? "").Replace("\"", "\"\"");
        }

        /// <summary>
        /// Sanitize WMI query to remove dangerous patterns
        /// TAG: #SECURITY_CRITICAL #WMI_INJECTION_PREVENTION
        /// </summary>
        public static string SanitizeWmiQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return string.Empty;

            string[] dangerousPatterns = {
                ";", "|", "`", "$", "(", ")", "{", "}", "[", "]",
                "<", ">", "\n", "\r", "&&", "||", "powershell -enc", "invoke-expression"
            };

            foreach (var pattern in dangerousPatterns)
                query = query.Replace(pattern, "");

            return query.Trim();
        }

        /// <summary>
        /// Check if input contains dangerous command patterns
        /// TAG: #SECURITY_CRITICAL #COMMAND_INJECTION_PREVENTION
        /// </summary>
        public static bool ContainsDangerousPatterns(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;

            string[] whitelistedCommands = {
                "sfc /scannow", "dism /online", "gpupdate /force",
                "shutdown /r", "netsh advfirewall", "ipconfig /flushdns",
                "start-mpcompliance", "start-mpdefenderscan",
                "get-netipaddress", "get-netroute", "get-dnsclientcache",
                "ipconfig /release", "ipconfig /renew"
            };

            string lowerInput = input.ToLower();
            if (whitelistedCommands.Any(cmd => lowerInput.Contains(cmd.ToLower())))
                return false;

            string[] dangerousPatterns = {
                ";", "|", "`", "$", "(", ")", "{", "}", "[", "]",
                "<", ">", "\n", "\r", "&&", "||", "powershell -enc", "invoke-expression"
            };

            return dangerousPatterns.Any(p => input.Contains(p));
        }

        /// <summary>
        /// Validate domain user format (DOMAIN\user or user@domain.com)
        /// TAG: #SECURITY_CRITICAL #DOMAIN_USER_VALIDATION
        /// </summary>
        public static bool IsValidDomainUser(string username)
        {
            return IsValidUsername(username);
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
