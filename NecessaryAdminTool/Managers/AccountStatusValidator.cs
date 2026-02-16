// TAG: #SECURITY #AUTHENTICATION #PASSWORD_EXPIRATION #ACCOUNT_LOCKOUT #KERBEROS
using System;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;

namespace NecessaryAdminTool.Managers
{
    /// <summary>
    /// Validates Active Directory account status including password expiration,
    /// account lockout, and disabled accounts.
    /// Provides friendly error messages and actionable guidance.
    /// </summary>
    public static class AccountStatusValidator
    {
        // Windows API for credential validation
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool LogonUser(
            string lpszUsername,
            string lpszDomain,
            string lpszPassword,
            int dwLogonType,
            int dwLogonProvider,
            out IntPtr phToken);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern bool CloseHandle(IntPtr handle);

        // Logon types
        private const int LOGON32_LOGON_NETWORK = 3;
        private const int LOGON32_PROVIDER_DEFAULT = 0;

        // Common error codes
        private const int ERROR_PASSWORD_EXPIRED = 1330;
        private const int ERROR_ACCOUNT_LOCKED_OUT = 1909;
        private const int ERROR_ACCOUNT_DISABLED = 1331;
        private const int ERROR_LOGON_FAILURE = 1326;
        private const int ERROR_PASSWORD_MUST_CHANGE = 1907;
        private const int ERROR_ACCOUNT_RESTRICTION = 1327;

        /// <summary>
        /// Account status result
        /// </summary>
        public class AccountStatus
        {
            public bool IsValid { get; set; }
            public bool PasswordExpired { get; set; }
            public bool AccountLocked { get; set; }
            public bool AccountDisabled { get; set; }
            public bool PasswordMustChange { get; set; }
            public DateTime? PasswordExpiryDate { get; set; }
            public int? DaysUntilExpiry { get; set; }
            public string ErrorMessage { get; set; }
            public string FriendlyMessage { get; set; }
            public string ActionableGuidance { get; set; }
        }

        /// <summary>
        /// Validates account credentials and checks for common issues.
        /// Returns detailed status including password expiration and account lockout.
        /// TAG: #AUTHENTICATION #VALIDATION #ERROR_DETECTION
        /// </summary>
        public static AccountStatus ValidateAccount(string username, string domain, SecureString password)
        {
            var result = new AccountStatus
            {
                IsValid = false
            };

            try
            {
                LogManager.LogInfo($"[AccountValidator] Validating account: {domain}\\{username}");

                // Convert SecureString to plaintext for LogonUser API
                string plainPassword = ConvertToUnsecureString(password);

                // Attempt Windows authentication
                IntPtr token = IntPtr.Zero;
                bool success = LogonUser(username, domain, plainPassword,
                    LOGON32_LOGON_NETWORK, LOGON32_PROVIDER_DEFAULT, out token);

                if (success)
                {
                    // Authentication succeeded - check password expiration
                    CloseHandle(token);
                    result.IsValid = true;

                    // Check password expiration using AD
                    CheckPasswordExpiration(username, domain, result);

                    if (result.DaysUntilExpiry.HasValue && result.DaysUntilExpiry.Value <= 30)
                    {
                        result.FriendlyMessage = $"⚠️ Password expires in {result.DaysUntilExpiry.Value} days";
                        result.ActionableGuidance = "Consider changing your password soon to avoid service interruption.";
                        LogManager.LogWarning($"[AccountValidator] Password for {username} expires in {result.DaysUntilExpiry.Value} days");
                    }
                    else
                    {
                        result.FriendlyMessage = "✅ Account validated successfully";
                    }

                    LogManager.LogInfo($"[AccountValidator] Account {username} validated successfully");
                    return result;
                }

                // Authentication failed - determine why
                int errorCode = Marshal.GetLastWin32Error();
                LogManager.LogWarning($"[AccountValidator] Authentication failed for {username}, error code: {errorCode}");

                switch (errorCode)
                {
                    case ERROR_PASSWORD_EXPIRED:
                    case ERROR_PASSWORD_MUST_CHANGE:
                        result.PasswordExpired = true;
                        result.ErrorMessage = "Password has expired";
                        result.FriendlyMessage = "❌ Your password has expired";
                        result.ActionableGuidance =
                            "You must change your password before you can log in.\n\n" +
                            "Steps to fix:\n" +
                            "1. Log into a Windows computer with your current password\n" +
                            "2. Press Ctrl+Alt+Delete → Change Password\n" +
                            "3. Enter your old password and create a new one\n" +
                            "4. Return to this application and use your new password";
                        LogManager.LogError($"[AccountValidator] Password expired for {username}", null);
                        break;

                    case ERROR_ACCOUNT_LOCKED_OUT:
                        result.AccountLocked = true;
                        result.ErrorMessage = "Account is locked out";
                        result.FriendlyMessage = "🔒 Your account has been locked";
                        result.ActionableGuidance =
                            "Your account was locked due to too many failed login attempts.\n\n" +
                            "Steps to fix:\n" +
                            "1. Contact your IT Help Desk or System Administrator\n" +
                            "2. Request account unlock (they can do this in Active Directory)\n" +
                            "3. Wait 30 minutes for automatic unlock (if enabled)\n" +
                            "4. Try logging in again\n\n" +
                            "⚠️ Do NOT attempt to log in repeatedly - this may extend the lockout period.";
                        LogManager.LogError($"[AccountValidator] Account locked: {username}", null);
                        break;

                    case ERROR_ACCOUNT_DISABLED:
                        result.AccountDisabled = true;
                        result.ErrorMessage = "Account is disabled";
                        result.FriendlyMessage = "⛔ Your account has been disabled";
                        result.ActionableGuidance =
                            "Your account has been disabled by an administrator.\n\n" +
                            "Possible reasons:\n" +
                            "• Employee separation/termination\n" +
                            "• Security policy violation\n" +
                            "• Inactive account cleanup\n\n" +
                            "Steps to fix:\n" +
                            "1. Contact your IT Help Desk or System Administrator\n" +
                            "2. Verify your employment status\n" +
                            "3. Request account reactivation if appropriate";
                        LogManager.LogError($"[AccountValidator] Account disabled: {username}", null);
                        break;

                    case ERROR_LOGON_FAILURE:
                        result.ErrorMessage = "Invalid username or password";
                        result.FriendlyMessage = "❌ Invalid username or password";
                        result.ActionableGuidance =
                            "The username or password you entered is incorrect.\n\n" +
                            "Common issues:\n" +
                            "• Caps Lock is ON (passwords are case-sensitive)\n" +
                            "• Typing error in username or password\n" +
                            "• Using old password after recent change\n" +
                            "• Domain name is incorrect\n\n" +
                            "⚠️ After 3-5 failed attempts, your account may be locked.";
                        LogManager.LogWarning($"[AccountValidator] Invalid credentials for {username}");
                        break;

                    case ERROR_ACCOUNT_RESTRICTION:
                        result.ErrorMessage = "Account restrictions prevent login";
                        result.FriendlyMessage = "⚠️ Account has login restrictions";
                        result.ActionableGuidance =
                            "Your account has restrictions that prevent login at this time.\n\n" +
                            "Possible restrictions:\n" +
                            "• Login hours (time-based restrictions)\n" +
                            "• Workstation restrictions (computer-based restrictions)\n" +
                            "• Network location restrictions\n\n" +
                            "Contact your IT administrator to review account restrictions.";
                        LogManager.LogWarning($"[AccountValidator] Account restrictions for {username}");
                        break;

                    default:
                        result.ErrorMessage = $"Authentication failed (Error {errorCode})";
                        result.FriendlyMessage = "❌ Authentication failed";
                        result.ActionableGuidance =
                            $"An unexpected error occurred during authentication.\n\n" +
                            $"Error Code: {errorCode}\n\n" +
                            "Please contact your IT administrator with this error code.";
                        LogManager.LogError($"[AccountValidator] Unknown error {errorCode} for {username}", null);
                        break;
                }

                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Validation error: {ex.Message}";
                result.FriendlyMessage = "❌ Unable to validate account";
                result.ActionableGuidance =
                    "An error occurred while validating your credentials.\n\n" +
                    $"Error: {ex.Message}\n\n" +
                    "Please check:\n" +
                    "• Network connectivity to domain controller\n" +
                    "• Domain name is correct\n" +
                    "• Firewall is not blocking authentication";
                LogManager.LogError("[AccountValidator] Exception during validation", ex);
                return result;
            }
        }

        /// <summary>
        /// Checks password expiration date using Active Directory.
        /// TAG: #PASSWORD_EXPIRATION #ACTIVE_DIRECTORY
        /// </summary>
        private static void CheckPasswordExpiration(string username, string domain, AccountStatus result)
        {
            try
            {
                using (var context = new PrincipalContext(ContextType.Domain, domain))
                using (var user = UserPrincipal.FindByIdentity(context, username))
                {
                    if (user != null)
                    {
                        // Check if password never expires
                        if (user.PasswordNeverExpires)
                        {
                            result.DaysUntilExpiry = null;
                            result.PasswordExpiryDate = null;
                            LogManager.LogInfo($"[AccountValidator] Password for {username} is set to never expire");
                            return;
                        }

                        // Get password last set date
                        DateTime? lastSet = user.LastPasswordSet;
                        if (lastSet.HasValue)
                        {
                            // Get domain password policy (max password age)
                            using (var domainEntry = new DirectoryEntry($"LDAP://{domain}"))
                            {
                                // Default to 42 days if unable to retrieve policy
                                int maxPasswordAgeDays = 42;

                                try
                                {
                                    var maxPwdAge = domainEntry.Properties["maxPwdAge"].Value;
                                    if (maxPwdAge != null)
                                    {
                                        long maxPwdAgeValue = (long)maxPwdAge;
                                        // Convert from 100-nanosecond intervals to days
                                        maxPasswordAgeDays = (int)(Math.Abs(maxPwdAgeValue) / 864000000000);
                                    }
                                }
                                catch
                                {
                                    LogManager.LogWarning("[AccountValidator] Unable to retrieve domain password policy, using default 42 days");
                                }

                                // Calculate expiry
                                result.PasswordExpiryDate = lastSet.Value.AddDays(maxPasswordAgeDays);
                                result.DaysUntilExpiry = (int)(result.PasswordExpiryDate.Value - DateTime.Now).TotalDays;

                                LogManager.LogInfo($"[AccountValidator] Password for {username} expires on {result.PasswordExpiryDate:yyyy-MM-dd} ({result.DaysUntilExpiry} days)");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.LogWarning($"[AccountValidator] Unable to check password expiration for {username}: {ex.Message}");
                // Don't fail validation if we can't check expiration
            }
        }

        /// <summary>
        /// Converts SecureString to plaintext string.
        /// ⚠️ WARNING: Use only when absolutely necessary (e.g., Win32 APIs).
        /// TAG: #SECURITY #SECURESTRING
        /// </summary>
        private static string ConvertToUnsecureString(SecureString securePassword)
        {
            if (securePassword == null)
                return string.Empty;

            IntPtr unmanagedString = IntPtr.Zero;
            try
            {
                unmanagedString = Marshal.SecureStringToGlobalAllocUnicode(securePassword);
                return Marshal.PtrToStringUni(unmanagedString);
            }
            finally
            {
                if (unmanagedString != IntPtr.Zero)
                    Marshal.ZeroFreeGlobalAllocUnicode(unmanagedString);
            }
        }

        /// <summary>
        /// Quick check if account is currently valid (cached check, no network call).
        /// Use this for fast validation without hitting domain controller.
        /// TAG: #PERFORMANCE #CACHE
        /// </summary>
        public static bool IsCurrentUserDomainAuthenticated()
        {
            try
            {
                var identity = WindowsIdentity.GetCurrent();
                return identity.IsAuthenticated &&
                       identity.AuthenticationType.Equals("Kerberos", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the current user's domain credentials if authenticated via Kerberos.
        /// Returns null if not domain authenticated.
        /// TAG: #KERBEROS #CURRENT_USER
        /// </summary>
        public static (string username, string domain)? GetCurrentDomainCredentials()
        {
            try
            {
                var identity = WindowsIdentity.GetCurrent();
                if (identity.IsAuthenticated && identity.AuthenticationType.Equals("Kerberos", StringComparison.OrdinalIgnoreCase))
                {
                    // Format: DOMAIN\username
                    string fullName = identity.Name;
                    int backslashIndex = fullName.IndexOf('\\');
                    if (backslashIndex > 0)
                    {
                        string domain = fullName.Substring(0, backslashIndex);
                        string username = fullName.Substring(backslashIndex + 1);
                        return (username, domain);
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.LogWarning($"[AccountValidator] Unable to get current domain credentials: {ex.Message}");
            }

            return null;
        }
    }
}
