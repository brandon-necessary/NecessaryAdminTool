using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
// TAG: #DATABASE #SQL_SERVER #VERSION_1_2

namespace NecessaryAdminTool.Data
{
    /// <summary>
    /// SQL Server data provider implementation
    /// TAG: #ENTERPRISE #MULTI_USER #UNLIMITED_CAPACITY
    /// </summary>
    public class SqlServerDataProvider : IDataProvider
    {
        private readonly string _connectionString;
        private SqlConnection _connection;
        private bool _disposed = false;

        public SqlServerDataProvider(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            LogManager.LogInfo($"SQL Server provider initialized: {GetServerName(connectionString)}");
        }

        private static string GetServerName(string connectionString)
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(connectionString);
                return builder.DataSource;
            }
            catch
            {
                return "unknown";
            }
        }

        public async Task InitializeDatabaseAsync()
        {
            try
            {
                _connection = new SqlConnection(_connectionString);
                await _connection.OpenAsync();

                // Create schema
                await CreateSchemaAsync();

                LogManager.LogInfo("SQL Server database initialized successfully");
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to initialize SQL Server database", ex);
                throw new InvalidOperationException("Cannot initialize SQL Server database", ex);
            }
        }

        private async Task CreateSchemaAsync()
        {
            var schema = @"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Computers' AND xtype='U')
                CREATE TABLE Computers (
                    Hostname NVARCHAR(255) PRIMARY KEY,
                    OS NVARCHAR(100),
                    OSVersion NVARCHAR(50),
                    Manufacturer NVARCHAR(100),
                    Model NVARCHAR(100),
                    SerialNumber NVARCHAR(100),
                    AssetTag NVARCHAR(100),
                    IPAddress NVARCHAR(50),
                    MACAddress NVARCHAR(50),
                    Domain NVARCHAR(100),
                    LastLoggedOnUser NVARCHAR(100),
                    RAM_GB INT,
                    CPU NVARCHAR(200),
                    DiskSize_GB INT,
                    DiskFree_GB INT,
                    LastSeen DATETIME,
                    LastBootTime DATETIME,
                    InstallDate DATETIME,
                    BitLockerStatus NVARCHAR(50),
                    TPMVersion NVARCHAR(50),
                    AntivirusProduct NVARCHAR(100),
                    AntivirusStatus NVARCHAR(50),
                    FirewallStatus NVARCHAR(50),
                    PendingRebootCount INT,
                    LastPatchDate DATETIME,
                    Notes NVARCHAR(MAX),
                    CreatedDate DATETIME DEFAULT GETDATE(),
                    ModifiedDate DATETIME DEFAULT GETDATE()
                );

                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ScanHistory' AND xtype='U')
                CREATE TABLE ScanHistory (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Hostname NVARCHAR(255),
                    ScanDate DATETIME DEFAULT GETDATE(),
                    ScanType NVARCHAR(50),
                    ComputersFound INT,
                    ErrorCount INT,
                    DurationSeconds INT,
                    Notes NVARCHAR(MAX)
                );

                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Scripts' AND xtype='U')
                CREATE TABLE Scripts (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Name NVARCHAR(200) NOT NULL,
                    Description NVARCHAR(MAX),
                    ScriptContent NVARCHAR(MAX),
                    ScriptType NVARCHAR(50),
                    Category NVARCHAR(100),
                    CreatedDate DATETIME DEFAULT GETDATE(),
                    ModifiedDate DATETIME DEFAULT GETDATE(),
                    ExecutionCount INT DEFAULT 0,
                    LastExecuted DATETIME
                );

                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Bookmarks' AND xtype='U')
                CREATE TABLE Bookmarks (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Name NVARCHAR(200) NOT NULL,
                    Hostname NVARCHAR(255),
                    Category NVARCHAR(100),
                    Notes NVARCHAR(MAX),
                    CreatedDate DATETIME DEFAULT GETDATE()
                );";

            using (var cmd = new SqlCommand(schema, _connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }
        }

        public async Task<List<ComputerInfo>> GetAllComputersAsync()
        {
            var computers = new List<ComputerInfo>();

            try
            {
                var query = "SELECT * FROM Computers ORDER BY Hostname";
                using (var cmd = new SqlCommand(query, _connection))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        computers.Add(MapReaderToComputer(reader));
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to retrieve computers from SQL Server", ex);
            }

            return computers;
        }

        public async Task SaveComputerAsync(ComputerInfo computer)
        {
            try
            {
                var query = @"
                    MERGE Computers AS target
                    USING (SELECT @Hostname AS Hostname) AS source
                    ON target.Hostname = source.Hostname
                    WHEN MATCHED THEN
                        UPDATE SET OS=@OS, OSVersion=@OSVersion, Manufacturer=@Manufacturer,
                                   Model=@Model, SerialNumber=@SerialNumber, AssetTag=@AssetTag,
                                   IPAddress=@IPAddress, MACAddress=@MACAddress, Domain=@Domain,
                                   LastLoggedOnUser=@LastLoggedOnUser, RAM_GB=@RAM_GB, CPU=@CPU,
                                   DiskSize_GB=@DiskSize_GB, DiskFree_GB=@DiskFree_GB,
                                   LastSeen=@LastSeen, LastBootTime=@LastBootTime,
                                   InstallDate=@InstallDate, BitLockerStatus=@BitLockerStatus,
                                   TPMVersion=@TPMVersion, AntivirusProduct=@AntivirusProduct,
                                   AntivirusStatus=@AntivirusStatus, FirewallStatus=@FirewallStatus,
                                   PendingRebootCount=@PendingRebootCount, LastPatchDate=@LastPatchDate,
                                   Notes=@Notes, ModifiedDate=GETDATE()
                    WHEN NOT MATCHED THEN
                        INSERT (Hostname, OS, OSVersion, Manufacturer, Model, SerialNumber, AssetTag,
                                IPAddress, MACAddress, Domain, LastLoggedOnUser, RAM_GB, CPU,
                                DiskSize_GB, DiskFree_GB, LastSeen, LastBootTime, InstallDate,
                                BitLockerStatus, TPMVersion, AntivirusProduct, AntivirusStatus,
                                FirewallStatus, PendingRebootCount, LastPatchDate, Notes)
                        VALUES (@Hostname, @OS, @OSVersion, @Manufacturer, @Model, @SerialNumber, @AssetTag,
                                @IPAddress, @MACAddress, @Domain, @LastLoggedOnUser, @RAM_GB, @CPU,
                                @DiskSize_GB, @DiskFree_GB, @LastSeen, @LastBootTime, @InstallDate,
                                @BitLockerStatus, @TPMVersion, @AntivirusProduct, @AntivirusStatus,
                                @FirewallStatus, @PendingRebootCount, @LastPatchDate, @Notes);";

                using (var cmd = new SqlCommand(query, _connection))
                {
                    AddComputerParameters(cmd, computer);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to save computer {computer.Hostname}", ex);
                throw;
            }
        }

        public async Task<ComputerInfo> GetComputerAsync(string hostname)
        {
            try
            {
                var query = "SELECT * FROM Computers WHERE Hostname = @Hostname";
                using (var cmd = new SqlCommand(query, _connection))
                {
                    cmd.Parameters.AddWithValue("@Hostname", hostname);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return MapReaderToComputer(reader);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to get computer {hostname} from SQL Server", ex);
            }

            return null;
        }

        public async Task DeleteComputerAsync(string hostname)
        {
            try
            {
                var query = "DELETE FROM Computers WHERE Hostname = @Hostname";
                using (var cmd = new SqlCommand(query, _connection))
                {
                    cmd.Parameters.AddWithValue("@Hostname", hostname);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to delete computer {hostname} from SQL Server", ex);
                throw;
            }
        }

        public async Task<List<ComputerInfo>> SearchComputersAsync(string searchTerm)
        {
            var computers = new List<ComputerInfo>();

            try
            {
                var query = @"SELECT * FROM Computers
                    WHERE Hostname LIKE @Search OR OS LIKE @Search OR Manufacturer LIKE @Search
                        OR Model LIKE @Search OR IPAddress LIKE @Search
                    ORDER BY Hostname";

                var searchPattern = $"%{searchTerm}%";

                using (var cmd = new SqlCommand(query, _connection))
                {
                    cmd.Parameters.AddWithValue("@Search", searchPattern);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            computers.Add(MapReaderToComputer(reader));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to search computers with term '{searchTerm}' from SQL Server", ex);
            }

            return computers;
        }

        public async Task<List<string>> GetComputerTagsAsync(string hostname)
        {
            LogManager.LogWarning($"GetComputerTagsAsync not yet implemented in {GetType().Name}");
            return await Task.FromResult(new List<string>());
        }

        public async Task AddTagAsync(string hostname, string tagName)
        {
            LogManager.LogWarning($"AddTagAsync not yet implemented in {GetType().Name}");
            await Task.CompletedTask;
        }

        public async Task RemoveTagAsync(string hostname, string tagName)
        {
            LogManager.LogWarning($"RemoveTagAsync not yet implemented in {GetType().Name}");
            await Task.CompletedTask;
        }

        public async Task<List<string>> GetAllTagsAsync()
        {
            LogManager.LogWarning($"GetAllTagsAsync not yet implemented in {GetType().Name}");
            return await Task.FromResult(new List<string>());
        }

        public async Task<ScanHistory> GetLastScanAsync()
        {
            LogManager.LogWarning($"GetLastScanAsync not yet implemented in {GetType().Name}");
            return await Task.FromResult<ScanHistory>(null);
        }

        public async Task SaveScanHistoryAsync(ScanHistory scan)
        {
            LogManager.LogWarning($"SaveScanHistoryAsync not yet implemented in {GetType().Name}");
            await Task.CompletedTask;
        }

        public async Task<List<ScanHistory>> GetScanHistoryAsync(int limit = 10)
        {
            LogManager.LogWarning($"GetScanHistoryAsync not yet implemented in {GetType().Name}");
            return await Task.FromResult(new List<ScanHistory>());
        }

        public async Task<string> GetSettingAsync(string key, string defaultValue = null)
        {
            LogManager.LogWarning($"GetSettingAsync not yet implemented in {GetType().Name}");
            return await Task.FromResult(defaultValue);
        }

        public async Task SaveSettingAsync(string key, string value)
        {
            LogManager.LogWarning($"SaveSettingAsync not yet implemented in {GetType().Name}");
            await Task.CompletedTask;
        }

        public async Task<List<ScriptInfo>> GetAllScriptsAsync()
        {
            LogManager.LogWarning($"GetAllScriptsAsync not yet implemented in {GetType().Name}");
            return await Task.FromResult(new List<ScriptInfo>());
        }

        public async Task SaveScriptAsync(ScriptInfo script)
        {
            LogManager.LogWarning($"SaveScriptAsync not yet implemented in {GetType().Name}");
            await Task.CompletedTask;
        }

        public async Task DeleteScriptAsync(int scriptId)
        {
            LogManager.LogWarning($"DeleteScriptAsync not yet implemented in {GetType().Name}");
            await Task.CompletedTask;
        }

        public async Task<List<BookmarkInfo>> GetAllBookmarksAsync()
        {
            LogManager.LogWarning($"GetAllBookmarksAsync not yet implemented in {GetType().Name}");
            return await Task.FromResult(new List<BookmarkInfo>());
        }

        public async Task SaveBookmarkAsync(BookmarkInfo bookmark)
        {
            LogManager.LogWarning($"SaveBookmarkAsync not yet implemented in {GetType().Name}");
            await Task.CompletedTask;
        }

        public async Task DeleteBookmarkAsync(string hostname)
        {
            LogManager.LogWarning($"DeleteBookmarkAsync not yet implemented in {GetType().Name}");
            await Task.CompletedTask;
        }

        public async Task OptimizeDatabaseAsync()
        {
            try
            {
                var query = @"
                    -- Update statistics
                    EXEC sp_updatestats;

                    -- Rebuild indexes
                    DECLARE @TableName NVARCHAR(255)
                    DECLARE TableCursor CURSOR FOR
                    SELECT table_name FROM information_schema.tables WHERE table_type = 'BASE TABLE'

                    OPEN TableCursor
                    FETCH NEXT FROM TableCursor INTO @TableName

                    WHILE @@FETCH_STATUS = 0
                    BEGIN
                        EXEC('ALTER INDEX ALL ON ' + @TableName + ' REBUILD')
                        FETCH NEXT FROM TableCursor INTO @TableName
                    END

                    CLOSE TableCursor
                    DEALLOCATE TableCursor";

                using (var cmd = new SqlCommand(query, _connection))
                {
                    cmd.CommandTimeout = 300; // 5 minutes
                    await cmd.ExecuteNonQueryAsync();
                }

                LogManager.LogInfo("SQL Server database optimized successfully");
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to optimize SQL Server database", ex);
            }
        }

        public async Task<bool> VerifyIntegrityAsync()
        {
            try
            {
                var query = "DBCC CHECKDB WITH NO_INFOMSGS";
                using (var cmd = new SqlCommand(query, _connection))
                {
                    cmd.CommandTimeout = 300; // 5 minutes
                    await cmd.ExecuteNonQueryAsync();
                }

                LogManager.LogInfo("SQL Server database integrity verified");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.LogError("SQL Server database integrity check failed", ex);
                return false;
            }
        }

        public async Task<bool> BackupDatabaseAsync(string backupPath)
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(_connectionString);
                var databaseName = builder.InitialCatalog;

                var query = $"BACKUP DATABASE [{databaseName}] TO DISK = @BackupPath WITH FORMAT, INIT";
                using (var cmd = new SqlCommand(query, _connection))
                {
                    cmd.Parameters.AddWithValue("@BackupPath", backupPath);
                    cmd.CommandTimeout = 600; // 10 minutes
                    await cmd.ExecuteNonQueryAsync();
                }

                LogManager.LogInfo($"SQL Server database backed up to: {backupPath}");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to backup SQL Server database to {backupPath}", ex);
                return false;
            }
        }

        public async Task<bool> RestoreDatabaseAsync(string backupPath)
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(_connectionString);
                var databaseName = builder.InitialCatalog;

                var query = $@"
                    USE master;
                    ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    RESTORE DATABASE [{databaseName}] FROM DISK = @BackupPath WITH REPLACE;
                    ALTER DATABASE [{databaseName}] SET MULTI_USER;";

                using (var cmd = new SqlCommand(query, _connection))
                {
                    cmd.Parameters.AddWithValue("@BackupPath", backupPath);
                    cmd.CommandTimeout = 600; // 10 minutes
                    await cmd.ExecuteNonQueryAsync();
                }

                LogManager.LogInfo($"SQL Server database restored from: {backupPath}");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to restore SQL Server database from {backupPath}", ex);
                return false;
            }
        }

        public async Task<DatabaseStats> GetDatabaseStatsAsync()
        {
            try
            {
                var query = @"
                    SELECT
                        (SELECT COUNT(*) FROM Computers) AS TotalComputers,
                        (SELECT COUNT(*) FROM ScanHistory) AS TotalScans,
                        (SELECT COUNT(*) FROM Scripts) AS TotalScripts,
                        (SELECT COUNT(*) FROM Bookmarks) AS TotalBookmarks";

                using (var cmd = new SqlCommand(query, _connection))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return new DatabaseStats
                        {
                            TotalComputers = reader.GetInt32(0),
                            TotalScans = reader.GetInt32(1),
                            TotalScripts = reader.GetInt32(2),
                            TotalBookmarks = reader.GetInt32(3),
                            SizeBytes = await GetDatabaseSizeAsync()
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to get database stats", ex);
            }

            return new DatabaseStats();
        }

        private async Task<long> GetDatabaseSizeAsync()
        {
            try
            {
                var query = @"
                    SELECT SUM(size) * 8 * 1024 AS SizeBytes
                    FROM sys.database_files";

                using (var cmd = new SqlCommand(query, _connection))
                {
                    var result = await cmd.ExecuteScalarAsync();
                    return result != null ? Convert.ToInt64(result) : 0;
                }
            }
            catch
            {
                return 0;
            }
        }

        private ComputerInfo MapReaderToComputer(SqlDataReader reader)
        {
            return new ComputerInfo
            {
                Hostname = reader["Hostname"]?.ToString(),
                OS = reader["OS"]?.ToString(),
                OSVersion = reader["OSVersion"]?.ToString(),
                Manufacturer = reader["Manufacturer"]?.ToString(),
                Model = reader["Model"]?.ToString(),
                SerialNumber = reader["SerialNumber"]?.ToString(),
                AssetTag = reader["AssetTag"]?.ToString(),
                IPAddress = reader["IPAddress"]?.ToString(),
                MACAddress = reader["MACAddress"]?.ToString(),
                Domain = reader["Domain"]?.ToString(),
                LastLoggedOnUser = reader["LastLoggedOnUser"]?.ToString(),
                RAM_GB = reader["RAM_GB"] != DBNull.Value ? (int)reader["RAM_GB"] : 0,
                CPU = reader["CPU"]?.ToString(),
                DiskSize_GB = reader["DiskSize_GB"] != DBNull.Value ? (int)reader["DiskSize_GB"] : 0,
                DiskFree_GB = reader["DiskFree_GB"] != DBNull.Value ? (int)reader["DiskFree_GB"] : 0,
                LastSeen = reader["LastSeen"] != DBNull.Value ? (DateTime)reader["LastSeen"] : DateTime.MinValue,
                LastBootTime = reader["LastBootTime"] != DBNull.Value ? (DateTime)reader["LastBootTime"] : DateTime.MinValue,
                InstallDate = reader["InstallDate"] != DBNull.Value ? (DateTime)reader["InstallDate"] : DateTime.MinValue,
                BitLockerStatus = reader["BitLockerStatus"]?.ToString(),
                TPMVersion = reader["TPMVersion"]?.ToString(),
                AntivirusProduct = reader["AntivirusProduct"]?.ToString(),
                AntivirusStatus = reader["AntivirusStatus"]?.ToString(),
                FirewallStatus = reader["FirewallStatus"]?.ToString(),
                PendingRebootCount = reader["PendingRebootCount"] != DBNull.Value ? (int)reader["PendingRebootCount"] : 0,
                LastPatchDate = reader["LastPatchDate"] != DBNull.Value ? (DateTime)reader["LastPatchDate"] : DateTime.MinValue,
                Notes = reader["Notes"]?.ToString()
            };
        }

        private void AddComputerParameters(SqlCommand cmd, ComputerInfo computer)
        {
            cmd.Parameters.AddWithValue("@Hostname", computer.Hostname ?? string.Empty);
            cmd.Parameters.AddWithValue("@OS", computer.OS ?? string.Empty);
            cmd.Parameters.AddWithValue("@OSVersion", computer.OSVersion ?? string.Empty);
            cmd.Parameters.AddWithValue("@Manufacturer", computer.Manufacturer ?? string.Empty);
            cmd.Parameters.AddWithValue("@Model", computer.Model ?? string.Empty);
            cmd.Parameters.AddWithValue("@SerialNumber", computer.SerialNumber ?? string.Empty);
            cmd.Parameters.AddWithValue("@AssetTag", computer.AssetTag ?? string.Empty);
            cmd.Parameters.AddWithValue("@IPAddress", computer.IPAddress ?? string.Empty);
            cmd.Parameters.AddWithValue("@MACAddress", computer.MACAddress ?? string.Empty);
            cmd.Parameters.AddWithValue("@Domain", computer.Domain ?? string.Empty);
            cmd.Parameters.AddWithValue("@LastLoggedOnUser", computer.LastLoggedOnUser ?? string.Empty);
            cmd.Parameters.AddWithValue("@RAM_GB", computer.RAM_GB);
            cmd.Parameters.AddWithValue("@CPU", computer.CPU ?? string.Empty);
            cmd.Parameters.AddWithValue("@DiskSize_GB", computer.DiskSize_GB);
            cmd.Parameters.AddWithValue("@DiskFree_GB", computer.DiskFree_GB);
            cmd.Parameters.AddWithValue("@LastSeen", computer.LastSeen);
            cmd.Parameters.AddWithValue("@LastBootTime", computer.LastBootTime);
            cmd.Parameters.AddWithValue("@InstallDate", computer.InstallDate);
            cmd.Parameters.AddWithValue("@BitLockerStatus", computer.BitLockerStatus ?? string.Empty);
            cmd.Parameters.AddWithValue("@TPMVersion", computer.TPMVersion ?? string.Empty);
            cmd.Parameters.AddWithValue("@AntivirusProduct", computer.AntivirusProduct ?? string.Empty);
            cmd.Parameters.AddWithValue("@AntivirusStatus", computer.AntivirusStatus ?? string.Empty);
            cmd.Parameters.AddWithValue("@FirewallStatus", computer.FirewallStatus ?? string.Empty);
            cmd.Parameters.AddWithValue("@PendingRebootCount", computer.PendingRebootCount);
            cmd.Parameters.AddWithValue("@LastPatchDate", computer.LastPatchDate);
            cmd.Parameters.AddWithValue("@Notes", computer.Notes ?? string.Empty);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _connection?.Close();
                _connection?.Dispose();
                _disposed = true;
                LogManager.LogInfo("SQL Server provider disposed");
            }
        }
    }
}
