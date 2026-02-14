using System;
using System.IO;
using System.Threading.Tasks;
using NecessaryAdminTool.Security;
// TAG: #DATABASE #FACTORY_PATTERN #VERSION_1_2

namespace NecessaryAdminTool.Data
{
    /// <summary>
    /// Factory for creating appropriate data provider instances
    /// TAG: #ABSTRACTION #DEPENDENCY_INJECTION
    /// </summary>
    public static class DataProviderFactory
    {
        /// <summary>
        /// Create and initialize data provider based on application settings
        /// </summary>
        public static async Task<IDataProvider> CreateProviderAsync()
        {
            var databaseType = Properties.Settings.Default.DatabaseType;
            var databasePath = Properties.Settings.Default.DatabasePath;

            LogManager.LogInfo($"Creating data provider: {databaseType} at {databasePath}");

            IDataProvider provider = null;

            try
            {
                switch (databaseType?.ToUpperInvariant())
                {
                    case "SQLITE":
                        provider = CreateSqliteProvider(databasePath);
                        break;

                    case "SQLSERVER":
                        provider = CreateSqlServerProvider(databasePath);
                        break;

                    case "ACCESS":
                        provider = CreateAccessProvider(databasePath);
                        break;

                    case "CSV":
                    case "JSON":
                        provider = CreateCsvProvider(databasePath);
                        break;

                    default:
                        LogManager.LogWarning($"Unknown database type '{databaseType}', falling back to SQLite");
                        provider = CreateSqliteProvider(databasePath);
                        break;
                }

                // Initialize the provider
                await provider.InitializeDatabaseAsync();

                LogManager.LogInfo($"Data provider initialized successfully: {databaseType}");
                return provider;
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to create {databaseType} provider", ex);
                throw;
            }
        }

        private static IDataProvider CreateSqliteProvider(string databasePath)
        {
            #if SQLITE_ENABLED
            // Ensure directory exists
            var directory = Path.GetDirectoryName(databasePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Get encryption key from secure storage
            var encryptionKey = EncryptionKeyManager.GetDatabaseKey();

            // Build SQLite file path
            var dbFile = Path.Combine(databasePath, "NecessaryAdminTool.db");

            return new SqliteDataProvider(dbFile, encryptionKey);
            #else
            throw new NotImplementedException(
                "SQLite support requires System.Data.SQLite NuGet package.\n" +
                "Install via: Install-Package System.Data.SQLite.Core\n" +
                "Then define SQLITE_ENABLED in project properties.");
            #endif
        }

        private static IDataProvider CreateSqlServerProvider(string connectionString)
        {
            // connectionString should be a full SQL Server connection string
            // e.g., "Server=myServerAddress;Database=myDataBase;User Id=myUsername;Password=myPassword;"

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("SQL Server connection string is required");
            }

            return new SqlServerDataProvider(connectionString);
        }

        private static IDataProvider CreateAccessProvider(string databasePath)
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(databasePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Build Access database file path
            var dbFile = Path.Combine(databasePath, "NecessaryAdminTool.accdb");

            return new AccessDataProvider(dbFile);
        }

        private static IDataProvider CreateCsvProvider(string databasePath)
        {
            // Ensure directory exists
            if (!Directory.Exists(databasePath))
            {
                Directory.CreateDirectory(databasePath);
            }

            return new CsvDataProvider(databasePath);
        }

        /// <summary>
        /// Verify that the current database configuration is valid
        /// </summary>
        public static bool VerifyConfiguration()
        {
            try
            {
                var databaseType = Properties.Settings.Default.DatabaseType;
                var databasePath = Properties.Settings.Default.DatabasePath;

                if (string.IsNullOrWhiteSpace(databaseType))
                {
                    LogManager.LogWarning("Database type not configured");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(databasePath))
                {
                    LogManager.LogWarning("Database path not configured");
                    return false;
                }

                // Type-specific validation
                switch (databaseType.ToUpperInvariant())
                {
                    case "SQLITE":
                        #if !SQLITE_ENABLED
                        LogManager.LogWarning("SQLite selected but not enabled in build");
                        return false;
                        #endif
                        break;

                    case "SQLSERVER":
                        // Validate connection string format
                        if (!databasePath.Contains("Server=") && !databasePath.Contains("Data Source="))
                        {
                            LogManager.LogWarning("SQL Server connection string appears invalid");
                            return false;
                        }
                        break;

                    case "ACCESS":
                        // Check if Microsoft Access Database Engine is available
                        try
                        {
                            Type.GetTypeFromProgID("ADOX.Catalog");
                        }
                        catch
                        {
                            LogManager.LogWarning("Microsoft Access Database Engine not found");
                            return false;
                        }
                        break;

                    case "CSV":
                    case "JSON":
                        // CSV/JSON always works
                        break;

                    default:
                        LogManager.LogWarning($"Unknown database type: {databaseType}");
                        return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to verify database configuration", ex);
                return false;
            }
        }

        /// <summary>
        /// Get user-friendly database type description
        /// </summary>
        public static string GetDatabaseTypeDescription(string databaseType)
        {
            return databaseType?.ToUpperInvariant() switch
            {
                "SQLITE" => "SQLite (AES-256 Encrypted)",
                "SQLSERVER" => "SQL Server (Enterprise)",
                "ACCESS" => "Microsoft Access",
                "CSV" => "CSV/JSON Files",
                "JSON" => "CSV/JSON Files",
                _ => "Unknown"
            };
        }
    }
}
