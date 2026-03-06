using System;
using System.Runtime.InteropServices;
using System.Text;

namespace NecessaryAdminTool
{
    // TAG: #SECURITY #CREDENTIALS #WINDOWS_CREDENTIAL_MANAGER
    /// <summary>
    /// Secure credential storage using Windows Credential Manager
    /// Uses P/Invoke to native Windows API (no external dependencies)
    /// </summary>
    public static class SecureCredentialManager
    {
        private const string CredentialPrefix = "NecessaryAdminTool_RMM_";

        #region Windows API P/Invoke

        [DllImport("Advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredWrite([In] ref CREDENTIAL userCredential, [In] uint flags);

        [DllImport("Advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredRead(string target, CRED_TYPE type, int reservedFlag, out IntPtr credentialPtr);

        [DllImport("Advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredDelete(string target, CRED_TYPE type, int reservedFlag);

        [DllImport("Advapi32.dll", EntryPoint = "CredFree", SetLastError = true)]
        private static extern void CredFree(IntPtr cred);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CREDENTIAL
        {
            public uint Flags;
            public CRED_TYPE Type;
            public string TargetName;
            public string Comment;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
            public uint CredentialBlobSize;
            public IntPtr CredentialBlob;
            public uint Persist;
            public uint AttributeCount;
            public IntPtr Attributes;
            public string TargetAlias;
            public string UserName;
        }

        private enum CRED_TYPE : uint
        {
            GENERIC = 1,
            DOMAIN_PASSWORD = 2,
            DOMAIN_CERTIFICATE = 3,
            DOMAIN_VISIBLE_PASSWORD = 4,
            GENERIC_CERTIFICATE = 5,
            DOMAIN_EXTENDED = 6,
            MAXIMUM = 7,
            MAXIMUM_EX = 1007
        }

        #endregion

        /// <summary>
        /// Store credential securely in Windows Credential Manager
        /// </summary>
        public static bool StoreCredential(string toolName, string credentialType, string value)
        {
            try
            {
                string targetName = $"{CredentialPrefix}{toolName}_{credentialType}";
                byte[] byteArray = Encoding.Unicode.GetBytes(value);

                if (byteArray.Length > 512 * 5)
                    throw new ArgumentOutOfRangeException("Credential value is too long (max 2560 bytes)");

                IntPtr unmanagedBlob = Marshal.AllocHGlobal(byteArray.Length);
                try
                {
                    Marshal.Copy(byteArray, 0, unmanagedBlob, byteArray.Length);
                    // TAG: #SECURITY_CRITICAL — wipe managed byte array immediately after copy to unmanaged
                    Array.Clear(byteArray, 0, byteArray.Length);

                    CREDENTIAL credential = new CREDENTIAL
                    {
                        Type = CRED_TYPE.GENERIC,
                        TargetName = targetName,
                        UserName = Environment.UserName,
                        CredentialBlob = unmanagedBlob,
                        CredentialBlobSize = (uint)byteArray.Length,
                        Persist = 2, // CRED_PERSIST_LOCAL_MACHINE
                        Comment = $"NecessaryAdminTool RMM Integration: {toolName}"
                    };

                    bool result = CredWrite(ref credential, 0);
                    if (!result)
                    {
                        int error = Marshal.GetLastWin32Error();
                        LogManager.LogWarning($"Failed to store credential for {toolName}: Win32Error={error}");
                        return false;
                    }

                    LogManager.LogInfo($"Credential stored securely: {toolName}/{credentialType}");
                    return true;
                }
                finally
                {
                    Marshal.FreeHGlobal(unmanagedBlob);
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to store credential for {toolName}", ex);
                return false;
            }
        }

        /// <summary>
        /// Retrieve credential from Windows Credential Manager
        /// </summary>
        public static string RetrieveCredential(string toolName, string credentialType)
        {
            try
            {
                string targetName = $"{CredentialPrefix}{toolName}_{credentialType}";
                IntPtr credPtr;

                bool result = CredRead(targetName, CRED_TYPE.GENERIC, 0, out credPtr);
                if (!result)
                {
                    int error = Marshal.GetLastWin32Error();
                    if (error != 1168) // ERROR_NOT_FOUND
                        LogManager.LogWarning($"Credential not found for {toolName}/{credentialType}");
                    return null;
                }

                try
                {
                    CREDENTIAL credential = (CREDENTIAL)Marshal.PtrToStructure(credPtr, typeof(CREDENTIAL));
                    byte[] byteArray = new byte[credential.CredentialBlobSize];
                    Marshal.Copy(credential.CredentialBlob, byteArray, 0, (int)credential.CredentialBlobSize);
                    try
                    {
                        return Encoding.Unicode.GetString(byteArray);
                    }
                    finally
                    {
                        // TAG: #SECURITY_CRITICAL — wipe raw credential bytes from managed heap
                        // immediately after string conversion to minimise exposure window
                        Array.Clear(byteArray, 0, byteArray.Length);
                    }
                }
                finally
                {
                    CredFree(credPtr);
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to retrieve credential for {toolName}", ex);
                return null;
            }
        }

        /// <summary>
        /// Delete credential from Windows Credential Manager
        /// </summary>
        public static bool DeleteCredential(string toolName, string credentialType)
        {
            try
            {
                string targetName = $"{CredentialPrefix}{toolName}_{credentialType}";
                bool result = CredDelete(targetName, CRED_TYPE.GENERIC, 0);

                if (result)
                    LogManager.LogInfo($"Credential deleted: {toolName}/{credentialType}");

                return result;
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to delete credential for {toolName}", ex);
                return false;
            }
        }

        /// <summary>
        /// Delete all RMM credentials
        /// </summary>
        public static void DeleteAllCredentials()
        {
            foreach (RmmToolType tool in Enum.GetValues(typeof(RmmToolType)))
            {
                try
                {
                    DeleteCredential(tool.ToString(), "ApiToken");
                    DeleteCredential(tool.ToString(), "ApiSecret");
                    DeleteCredential(tool.ToString(), "Password");
                    DeleteCredential(tool.ToString(), "AccessToken");
                }
                catch (Exception ex)
                {
                    // Continue deleting others even if one fails
                    LogManager.LogWarning($"DeleteAllCredentials: Failed to delete credentials for {tool}: {ex.Message}");
                }
            }

            LogManager.LogInfo("All RMM credentials deleted");
        }

        /// <summary>
        /// Check if credential exists
        /// </summary>
        public static bool CredentialExists(string toolName, string credentialType)
        {
            string value = RetrieveCredential(toolName, credentialType);
            return !string.IsNullOrEmpty(value);
        }
    }
}
