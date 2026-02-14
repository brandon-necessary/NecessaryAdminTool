using System;
using System.Security.Cryptography;
// TAG: #SECURITY #ENCRYPTION #VERSION_1_2

namespace NecessaryAdminTool.Security
{
    /// <summary>
    /// Manages encryption keys using Windows Credential Manager
    /// TAG: #ENCRYPTION_KEY #CREDENTIAL_MANAGER
    /// </summary>
    public static class EncryptionKeyManager
    {
        private const string DATABASE_KEY_NAME = "NecessaryAdminTool_DatabaseKey";
        private const string CSV_KEY_NAME = "NecessaryAdminTool_CSVKey";

        /// <summary>
        /// Get or create database encryption key (256-bit AES)
        /// Stored securely in Windows Credential Manager
        /// </summary>
        public static string GetDatabaseKey()
        {
            try
            {
                // Try to retrieve existing key
                var existingKey = SecureCredentialManager.RetrieveCredential(DATABASE_KEY_NAME);
                if (!string.IsNullOrEmpty(existingKey))
                {
                    LogManager.LogInfo("Retrieved existing database encryption key");
                    return existingKey;
                }

                // Generate new 256-bit key
                using (var rng = new RNGCryptoServiceProvider())
                {
                    byte[] keyBytes = new byte[32]; // 256 bits
                    rng.GetBytes(keyBytes);
                    var key = Convert.ToBase64String(keyBytes);

                    // Store in Windows Credential Manager
                    SecureCredentialManager.StoreCredential(DATABASE_KEY_NAME, key);

                    LogManager.LogInfo("Generated new database encryption key (256-bit AES)");
                    return key;
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to get database encryption key", ex);
                throw new InvalidOperationException("Cannot initialize database encryption", ex);
            }
        }

        /// <summary>
        /// Get or create CSV/JSON file encryption key
        /// </summary>
        public static string GetCSVEncryptionKey()
        {
            try
            {
                // Try to retrieve existing key
                var existingKey = SecureCredentialManager.RetrieveCredential(CSV_KEY_NAME);
                if (!string.IsNullOrEmpty(existingKey))
                {
                    return existingKey;
                }

                // Generate new 256-bit key
                using (var rng = new RNGCryptoServiceProvider())
                {
                    byte[] keyBytes = new byte[32];
                    rng.GetBytes(keyBytes);
                    var key = Convert.ToBase64String(keyBytes);

                    // Store in Windows Credential Manager
                    SecureCredentialManager.StoreCredential(CSV_KEY_NAME, key);

                    LogManager.LogInfo("Generated new CSV encryption key");
                    return key;
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to get CSV encryption key", ex);
                throw new InvalidOperationException("Cannot initialize CSV encryption", ex);
            }
        }

        /// <summary>
        /// Rotate encryption key (regenerate and migrate data)
        /// </summary>
        public static string RotateDatabaseKey()
        {
            try
            {
                // Delete old key
                SecureCredentialManager.DeleteCredential(DATABASE_KEY_NAME);

                // Generate new key
                return GetDatabaseKey();
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to rotate database key", ex);
                throw;
            }
        }

        /// <summary>
        /// Delete all encryption keys (use with caution!)
        /// </summary>
        public static void DeleteAllEncryptionKeys()
        {
            try
            {
                SecureCredentialManager.DeleteCredential(DATABASE_KEY_NAME);
                SecureCredentialManager.DeleteCredential(CSV_KEY_NAME);
                LogManager.LogWarning("All encryption keys deleted");
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to delete encryption keys", ex);
            }
        }

        /// <summary>
        /// Verify encryption key exists and is valid
        /// </summary>
        public static bool VerifyDatabaseKey()
        {
            try
            {
                var key = GetDatabaseKey();
                return !string.IsNullOrEmpty(key) && key.Length >= 32;
            }
            catch
            {
                return false;
            }
        }
    }
}
