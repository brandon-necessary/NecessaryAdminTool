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

        public SqlServerDataProvider(string connectionString)
        {
            if (connectionString == null) throw new ArgumentNullException(nameof(connectionString));
            // Ensure a connection timeout is set (default 15s) to avoid indefinite hangs
            var builder = new SqlConnectionStringBuilder(connectionString);
            if (builder.ConnectTimeout == 0)
                builder.ConnectTimeout = 15;
            _connectionString = builder.ConnectionString;
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

        // Returns a fresh open connection; caller must dispose via using()
        private async Task<SqlConnection> OpenConnectionAsync()
        {
            var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync().ConfigureAwait(false);
            return conn;
        }

        public async Task InitializeDatabaseAsync()
        {
            try
            {
                using (var conn = await OpenConnectionAsync().ConfigureAwait(false))
                {
                    await CreateSchemaAsync(conn).ConfigureAwait(false);
                }
                LogManager.LogInfo("SQL Server database initialized successfully");
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to initialize SQL Server database", ex);
                throw new InvalidOperationException("Cannot initialize SQL Server database", ex);
            }
        }

        private async Task CreateSchemaAsync(SqlConnection conn)
        {
            var schema = @"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Computers' AND xtype='U')
                CREATE TABLE Computers (
                    Hostname NVARCHAR(255) PRIMARY KEY,
                    OS NVARCHAR(100),
                    OSVersion NVARCHAR(50),
                    Status NVARCHAR(50),
                    Manufacturer NVARCHAR(100),
                    Model NVARCHAR(100),
                    SerialNumber NVARCHAR(100),
                    AssetTag NVARCHAR(100),
                    ChassisType NVARCHAR(100),
                    IPAddress NVARCHAR(50),
                    MACAddress NVARCHAR(50),
                    Domain NVARCHAR(100),
                    DomainController NVARCHAR(255),
                    LastLoggedOnUser NVARCHAR(100),
                    RAM_GB INT,
                    CPU NVARCHAR(200),
                    DiskSize_GB INT,
                    DiskFree_GB INT,
                    LastSeen DATETIME,
                    LastBootTime DATETIME,
                    InstallDate DATETIME,
                    Uptime BIGINT,
                    BitLockerStatus NVARCHAR(50),
                    TPMVersion NVARCHAR(50),
                    AntivirusProduct NVARCHAR(100),
                    AntivirusStatus NVARCHAR(50),
                    FirewallStatus NVARCHAR(50),
                    PendingRebootCount INT,
                    LastPatchDate DATETIME,
                    Notes NVARCHAR(MAX),
                    RawDataJson NVARCHAR(MAX),
                    CreatedDate DATETIME DEFAULT GETDATE(),
                    ModifiedDate DATETIME DEFAULT GETDATE()
                );

                -- Migration: add columns to existing databases
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Computers') AND name='Status')
                    ALTER TABLE Computers ADD Status NVARCHAR(50);
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Computers') AND name='ChassisType')
                    ALTER TABLE Computers ADD ChassisType NVARCHAR(100);
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Computers') AND name='DomainController')
                    ALTER TABLE Computers ADD DomainController NVARCHAR(255);
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Computers') AND name='Uptime')
                    ALTER TABLE Computers ADD Uptime BIGINT;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Computers') AND name='RawDataJson')
                    ALTER TABLE Computers ADD RawDataJson NVARCHAR(MAX);

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
                );

                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ComputerTags' AND xtype='U')
                CREATE TABLE ComputerTags (
                    Hostname NVARCHAR(255) NOT NULL,
                    TagName NVARCHAR(100) NOT NULL,
                    CONSTRAINT PK_ComputerTags PRIMARY KEY (Hostname, TagName),
                    CONSTRAINT FK_ComputerTags_Computers FOREIGN KEY (Hostname) REFERENCES Computers(Hostname) ON DELETE CASCADE
                );

                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Settings' AND xtype='U')
                CREATE TABLE Settings (
                    SettingKey NVARCHAR(255) PRIMARY KEY,
                    SettingValue NVARCHAR(MAX),
                    UpdatedAt DATETIME DEFAULT GETDATE()
                );";

            using (var cmd = new SqlCommand(schema, conn))
            {
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task<List<ComputerInfo>> GetAllComputersAsync()
        {
            var computers = new List<ComputerInfo>();

            try
            {
                using (var conn = await OpenConnectionAsync().ConfigureAwait(false))
                {
                    var query = "SELECT * FROM Computers ORDER BY Hostname";
                    using (var cmd = new SqlCommand(query, conn))
                    using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync().ConfigureAwait(false))
                        {
                            computers.Add(MapReaderToComputer(reader));
                        }
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
                using (var conn = await OpenConnectionAsync().ConfigureAwait(false))
                {
                    var query = @"
                        MERGE Computers AS target
                        USING (SELECT @Hostname AS Hostname) AS source
                        ON target.Hostname = source.Hostname
                        WHEN MATCHED THEN
                            UPDATE SET OS=@OS, OSVersion=@OSVersion, Status=@Status,
                                       Manufacturer=@Manufacturer, Model=@Model, SerialNumber=@SerialNumber,
                                       AssetTag=@AssetTag, ChassisType=@ChassisType,
                                       IPAddress=@IPAddress, MACAddress=@MACAddress, Domain=@Domain,
                                       DomainController=@DomainController, LastLoggedOnUser=@LastLoggedOnUser,
                                       RAM_GB=@RAM_GB, CPU=@CPU, DiskSize_GB=@DiskSize_GB, DiskFree_GB=@DiskFree_GB,
                                       LastSeen=@LastSeen, LastBootTime=@LastBootTime,
                                       InstallDate=@InstallDate, Uptime=@Uptime,
                                       BitLockerStatus=@BitLockerStatus, TPMVersion=@TPMVersion,
                                       AntivirusProduct=@AntivirusProduct, AntivirusStatus=@AntivirusStatus,
                                       FirewallStatus=@FirewallStatus, PendingRebootCount=@PendingRebootCount,
                                       LastPatchDate=@LastPatchDate, Notes=@Notes, RawDataJson=@RawDataJson,
                                       ModifiedDate=GETDATE()
                        WHEN NOT MATCHED THEN
                            INSERT (Hostname, OS, OSVersion, Status, Manufacturer, Model, SerialNumber, AssetTag,
                                    ChassisType, IPAddress, MACAddress, Domain, DomainController,
                                    LastLoggedOnUser, RAM_GB, CPU, DiskSize_GB, DiskFree_GB,
                                    LastSeen, LastBootTime, InstallDate, Uptime,
                                    BitLockerStatus, TPMVersion, AntivirusProduct, AntivirusStatus,
                                    FirewallStatus, PendingRebootCount, LastPatchDate, Notes, RawDataJson)
                            VALUES (@Hostname, @OS, @OSVersion, @Status, @Manufacturer, @Model, @SerialNumber, @AssetTag,
                                    @ChassisType, @IPAddress, @MACAddress, @Domain, @DomainController,
                                    @LastLoggedOnUser, @RAM_GB, @CPU, @DiskSize_GB, @DiskFree_GB,
                                    @LastSeen, @LastBootTime, @InstallDate, @Uptime,
                                    @BitLockerStatus, @TPMVersion, @AntivirusProduct, @AntivirusStatus,
                                    @FirewallStatus, @PendingRebootCount, @LastPatchDate, @Notes, @RawDataJson);";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        AddComputerParameters(cmd, computer);
                        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }
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
                using (var conn = await OpenConnectionAsync().ConfigureAwait(false))
                {
                    var query = "SELECT * FROM Computers WHERE Hostname = @Hostname";
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Hostname", hostname);
                        using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                        {
                            if (await reader.ReadAsync().ConfigureAwait(false))
                            {
                                return MapReaderToComputer(reader);
                            }
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
                using (var conn = await OpenConnectionAsync().ConfigureAwait(false))
                {
                    var query = "DELETE FROM Computers WHERE Hostname = @Hostname";
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Hostname", hostname);
                        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }
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
                using (var conn = await OpenConnectionAsync().ConfigureAwait(false))
                {
                    var query = @"SELECT * FROM Computers
                        WHERE Hostname LIKE @Search OR OS LIKE @Search OR Manufacturer LIKE @Search
                            OR Model LIKE @Search OR IPAddress LIKE @Search
                        ORDER BY Hostname";

                    var searchPattern = $"%{searchTerm}%";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Search", searchPattern);
                        using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                        {
                            while (await reader.ReadAsync().ConfigureAwait(false))
                            {
                                computers.Add(MapReaderToComputer(reader));
                            }
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

        // TAG MANAGEMENT
        public async Task<List<string>> GetComputerTagsAsync(string hostname)
        {
            var tags = new List<string>();
            try
            {
                using (var conn = await OpenConnectionAsync().ConfigureAwait(false))
                {
                    using (var cmd = new SqlCommand("SELECT TagName FROM ComputerTags WHERE Hostname = @Hostname ORDER BY TagName", conn))
                    {
                        cmd.Parameters.AddWithValue("@Hostname", hostname);
                        using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                        {
                            while (await reader.ReadAsync().ConfigureAwait(false))
                                tags.Add(reader.GetString(0));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to get tags for {hostname}", ex);
            }
            return tags;
        }

        public async Task AddTagAsync(string hostname, string tagName)
        {
            try
            {
                using (var conn = await OpenConnectionAsync().ConfigureAwait(false))
                {
                    var query = @"IF NOT EXISTS (SELECT 1 FROM ComputerTags WHERE Hostname=@Hostname AND TagName=@Tag)
                        INSERT INTO ComputerTags (Hostname, TagName) VALUES (@Hostname, @Tag)";
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Hostname", hostname);
                        cmd.Parameters.AddWithValue("@Tag", tagName);
                        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to add tag '{tagName}' to {hostname}", ex);
            }
        }

        public async Task RemoveTagAsync(string hostname, string tagName)
        {
            try
            {
                using (var conn = await OpenConnectionAsync().ConfigureAwait(false))
                {
                    using (var cmd = new SqlCommand("DELETE FROM ComputerTags WHERE Hostname=@Hostname AND TagName=@Tag", conn))
                    {
                        cmd.Parameters.AddWithValue("@Hostname", hostname);
                        cmd.Parameters.AddWithValue("@Tag", tagName);
                        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to remove tag '{tagName}' from {hostname}", ex);
            }
        }

        public async Task<List<string>> GetAllTagsAsync()
        {
            var tags = new List<string>();
            try
            {
                using (var conn = await OpenConnectionAsync().ConfigureAwait(false))
                {
                    using (var cmd = new SqlCommand("SELECT DISTINCT TagName FROM ComputerTags ORDER BY TagName", conn))
                    using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync().ConfigureAwait(false))
                            tags.Add(reader.GetString(0));
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to get all tags", ex);
            }
            return tags;
        }

        // SCAN HISTORY
        public async Task<ScanHistory> GetLastScanAsync()
        {
            try
            {
                using (var conn = await OpenConnectionAsync().ConfigureAwait(false))
                {
                    using (var cmd = new SqlCommand("SELECT TOP 1 Id, ScanDate, ComputersFound, ErrorCount, DurationSeconds FROM ScanHistory ORDER BY Id DESC", conn))
                    using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                    {
                        if (await reader.ReadAsync().ConfigureAwait(false))
                        {
                            return new ScanHistory
                            {
                                ScanId = reader.GetInt32(0),
                                StartTime = reader["ScanDate"] != System.DBNull.Value ? (DateTime)reader["ScanDate"] : DateTime.MinValue,
                                ComputersScanned = reader["ComputersFound"] != System.DBNull.Value ? (int)reader["ComputersFound"] : 0,
                                FailureCount = reader["ErrorCount"] != System.DBNull.Value ? (int)reader["ErrorCount"] : 0,
                                DurationSeconds = reader["DurationSeconds"] != System.DBNull.Value ? Convert.ToDouble(reader["DurationSeconds"]) : 0
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to get last scan", ex);
            }
            return null;
        }

        public async Task SaveScanHistoryAsync(ScanHistory scan)
        {
            try
            {
                using (var conn = await OpenConnectionAsync().ConfigureAwait(false))
                {
                    var query = @"INSERT INTO ScanHistory (ScanDate, ScanType, ComputersFound, ErrorCount, DurationSeconds, Notes)
                        VALUES (@ScanDate, @ScanType, @Found, @Errors, @Duration, @Notes)";
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@ScanDate", scan.StartTime);
                        cmd.Parameters.AddWithValue("@ScanType", "Fleet Scan");
                        cmd.Parameters.AddWithValue("@Found", scan.ComputersScanned);
                        cmd.Parameters.AddWithValue("@Errors", scan.FailureCount);
                        cmd.Parameters.AddWithValue("@Duration", (int)scan.DurationSeconds);
                        cmd.Parameters.AddWithValue("@Notes", $"Success: {scan.SuccessCount}, Failed: {scan.FailureCount}");
                        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to save scan history", ex);
            }
        }

        public async Task<List<ScanHistory>> GetScanHistoryAsync(int limit = 10)
        {
            var history = new List<ScanHistory>();
            try
            {
                using (var conn = await OpenConnectionAsync().ConfigureAwait(false))
                {
                    using (var cmd = new SqlCommand($"SELECT TOP (@Limit) Id, ScanDate, ComputersFound, ErrorCount, DurationSeconds FROM ScanHistory ORDER BY Id DESC", conn))
                    {
                        cmd.Parameters.AddWithValue("@Limit", limit);
                        using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                        {
                            while (await reader.ReadAsync().ConfigureAwait(false))
                            {
                                history.Add(new ScanHistory
                                {
                                    ScanId = reader.GetInt32(0),
                                    StartTime = reader["ScanDate"] != System.DBNull.Value ? (DateTime)reader["ScanDate"] : DateTime.MinValue,
                                    ComputersScanned = reader["ComputersFound"] != System.DBNull.Value ? (int)reader["ComputersFound"] : 0,
                                    FailureCount = reader["ErrorCount"] != System.DBNull.Value ? (int)reader["ErrorCount"] : 0,
                                    DurationSeconds = reader["DurationSeconds"] != System.DBNull.Value ? Convert.ToDouble(reader["DurationSeconds"]) : 0
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to get scan history", ex);
            }
            return history;
        }

        // SETTINGS
        public async Task<string> GetSettingAsync(string key, string defaultValue = null)
        {
            try
            {
                using (var conn = await OpenConnectionAsync().ConfigureAwait(false))
                {
                    using (var cmd = new SqlCommand("SELECT SettingValue FROM Settings WHERE SettingKey = @Key", conn))
                    {
                        cmd.Parameters.AddWithValue("@Key", key);
                        var result = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
                        if (result != null && result != System.DBNull.Value)
                            return result.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to get setting '{key}'", ex);
            }
            return defaultValue;
        }

        public async Task SaveSettingAsync(string key, string value)
        {
            try
            {
                using (var conn = await OpenConnectionAsync().ConfigureAwait(false))
                {
                    var query = @"MERGE Settings AS target
                        USING (SELECT @Key AS SettingKey) AS source ON target.SettingKey = source.SettingKey
                        WHEN MATCHED THEN UPDATE SET SettingValue=@Value, UpdatedAt=GETDATE()
                        WHEN NOT MATCHED THEN INSERT (SettingKey, SettingValue) VALUES (@Key, @Value);";
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Key", key);
                        cmd.Parameters.AddWithValue("@Value", (object)value ?? System.DBNull.Value);
                        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to save setting '{key}'", ex);
            }
        }

        // SCRIPTS
        public async Task<List<ScriptInfo>> GetAllScriptsAsync()
        {
            var scripts = new List<ScriptInfo>();
            try
            {
                using (var conn = await OpenConnectionAsync().ConfigureAwait(false))
                {
                    using (var cmd = new SqlCommand("SELECT Id, Name, Description, ScriptContent, Category, CreatedDate FROM Scripts ORDER BY Name", conn))
                    using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync().ConfigureAwait(false))
                        {
                            scripts.Add(new ScriptInfo
                            {
                                ScriptId = reader.GetInt32(0),
                                Name = reader["Name"]?.ToString(),
                                Description = reader["Description"]?.ToString(),
                                Content = reader["ScriptContent"]?.ToString(),
                                Category = reader["Category"]?.ToString(),
                                CreatedAt = reader["CreatedDate"] != System.DBNull.Value ? (DateTime)reader["CreatedDate"] : DateTime.MinValue
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to get scripts", ex);
            }
            return scripts;
        }

        public async Task SaveScriptAsync(ScriptInfo script)
        {
            try
            {
                using (var conn = await OpenConnectionAsync().ConfigureAwait(false))
                {
                    var query = @"MERGE Scripts AS target
                        USING (SELECT @Name AS Name) AS source ON target.Name = source.Name
                        WHEN MATCHED THEN UPDATE SET Description=@Desc, ScriptContent=@Content, Category=@Category, ModifiedDate=GETDATE()
                        WHEN NOT MATCHED THEN INSERT (Name, Description, ScriptContent, Category) VALUES (@Name, @Desc, @Content, @Category);";
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Name", script.Name ?? string.Empty);
                        cmd.Parameters.AddWithValue("@Desc", (object)script.Description ?? System.DBNull.Value);
                        cmd.Parameters.AddWithValue("@Content", (object)script.Content ?? System.DBNull.Value);
                        cmd.Parameters.AddWithValue("@Category", (object)script.Category ?? System.DBNull.Value);
                        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to save script '{script.Name}'", ex);
            }
        }

        public async Task DeleteScriptAsync(int scriptId)
        {
            try
            {
                using (var conn = await OpenConnectionAsync().ConfigureAwait(false))
                {
                    using (var cmd = new SqlCommand("DELETE FROM Scripts WHERE Id = @Id", conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", scriptId);
                        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to delete script {scriptId}", ex);
            }
        }

        // BOOKMARKS
        public async Task<List<BookmarkInfo>> GetAllBookmarksAsync()
        {
            var bookmarks = new List<BookmarkInfo>();
            try
            {
                using (var conn = await OpenConnectionAsync().ConfigureAwait(false))
                {
                    using (var cmd = new SqlCommand("SELECT Hostname, Category, Notes FROM Bookmarks ORDER BY Hostname", conn))
                    using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync().ConfigureAwait(false))
                        {
                            bookmarks.Add(new BookmarkInfo
                            {
                                Hostname = reader["Hostname"]?.ToString(),
                                Category = reader["Category"]?.ToString(),
                                Notes = reader["Notes"]?.ToString()
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to get bookmarks", ex);
            }
            return bookmarks;
        }

        public async Task SaveBookmarkAsync(BookmarkInfo bookmark)
        {
            try
            {
                using (var conn = await OpenConnectionAsync().ConfigureAwait(false))
                {
                    var query = @"MERGE Bookmarks AS target
                        USING (SELECT @Hostname AS Hostname) AS source ON target.Hostname = source.Hostname
                        WHEN MATCHED THEN UPDATE SET Category=@Category, Notes=@Notes
                        WHEN NOT MATCHED THEN INSERT (Name, Hostname, Category, Notes) VALUES (@Hostname, @Hostname, @Category, @Notes);";
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Hostname", bookmark.Hostname ?? string.Empty);
                        cmd.Parameters.AddWithValue("@Category", (object)bookmark.Category ?? System.DBNull.Value);
                        cmd.Parameters.AddWithValue("@Notes", (object)bookmark.Notes ?? System.DBNull.Value);
                        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to save bookmark for {bookmark.Hostname}", ex);
            }
        }

        public async Task DeleteBookmarkAsync(string hostname)
        {
            try
            {
                using (var conn = await OpenConnectionAsync().ConfigureAwait(false))
                {
                    using (var cmd = new SqlCommand("DELETE FROM Bookmarks WHERE Hostname = @Hostname", conn))
                    {
                        cmd.Parameters.AddWithValue("@Hostname", hostname);
                        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to delete bookmark for {hostname}", ex);
            }
        }

        public async Task OptimizeDatabaseAsync()
        {
            try
            {
                using (var conn = await OpenConnectionAsync().ConfigureAwait(false))
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

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.CommandTimeout = 300; // 5 minutes
                        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }
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
                using (var conn = await OpenConnectionAsync().ConfigureAwait(false))
                {
                    var query = "DBCC CHECKDB WITH NO_INFOMSGS";
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.CommandTimeout = 300; // 5 minutes
                        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }
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
                using (var conn = await OpenConnectionAsync().ConfigureAwait(false))
                {
                    var builder = new SqlConnectionStringBuilder(_connectionString);
                    var databaseName = builder.InitialCatalog;

                    var query = $"BACKUP DATABASE [{databaseName}] TO DISK = @BackupPath WITH FORMAT, INIT";
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@BackupPath", backupPath);
                        cmd.CommandTimeout = 600; // 10 minutes
                        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }
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
                using (var conn = await OpenConnectionAsync().ConfigureAwait(false))
                {
                    var builder = new SqlConnectionStringBuilder(_connectionString);
                    var databaseName = builder.InitialCatalog;

                    var query = $@"
                        USE master;
                        ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                        RESTORE DATABASE [{databaseName}] FROM DISK = @BackupPath WITH REPLACE;
                        ALTER DATABASE [{databaseName}] SET MULTI_USER;";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@BackupPath", backupPath);
                        cmd.CommandTimeout = 600; // 10 minutes
                        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }
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
                using (var conn = await OpenConnectionAsync().ConfigureAwait(false))
                {
                    var query = @"
                        SELECT
                            (SELECT COUNT(*) FROM Computers) AS TotalComputers,
                            (SELECT COUNT(*) FROM ScanHistory) AS TotalScans,
                            (SELECT COUNT(*) FROM Scripts) AS TotalScripts,
                            (SELECT COUNT(*) FROM Bookmarks) AS TotalBookmarks";

                    using (var cmd = new SqlCommand(query, conn))
                    using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                    {
                        if (await reader.ReadAsync().ConfigureAwait(false))
                        {
                            return new DatabaseStats
                            {
                                TotalComputers = reader.GetInt32(0),
                                TotalScans = reader.GetInt32(1),
                                TotalScripts = reader.GetInt32(2),
                                TotalBookmarks = reader.GetInt32(3),
                                SizeBytes = await GetDatabaseSizeAsync().ConfigureAwait(false)
                            };
                        }
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
                using (var conn = await OpenConnectionAsync().ConfigureAwait(false))
                {
                    var query = @"
                        SELECT SUM(size) * 8 * 1024 AS SizeBytes
                        FROM sys.database_files";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        var result = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
                        return result != null ? Convert.ToInt64(result) : 0;
                    }
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
                RAM_GB = reader["RAM_GB"] != System.DBNull.Value ? (int)reader["RAM_GB"] : 0,
                CPU = reader["CPU"]?.ToString(),
                DiskSize_GB = reader["DiskSize_GB"] != System.DBNull.Value ? (int)reader["DiskSize_GB"] : 0,
                DiskFree_GB = reader["DiskFree_GB"] != System.DBNull.Value ? (int)reader["DiskFree_GB"] : 0,
                LastSeen = reader["LastSeen"] != System.DBNull.Value ? (DateTime)reader["LastSeen"] : DateTime.MinValue,
                LastBootTime = reader["LastBootTime"] != System.DBNull.Value ? (DateTime)reader["LastBootTime"] : DateTime.MinValue,
                InstallDate = reader["InstallDate"] != System.DBNull.Value ? (DateTime)reader["InstallDate"] : DateTime.MinValue,
                BitLockerStatus = reader["BitLockerStatus"]?.ToString(),
                TPMVersion = reader["TPMVersion"]?.ToString(),
                AntivirusProduct = reader["AntivirusProduct"]?.ToString(),
                AntivirusStatus = reader["AntivirusStatus"]?.ToString(),
                FirewallStatus = reader["FirewallStatus"]?.ToString(),
                PendingRebootCount = reader["PendingRebootCount"] != System.DBNull.Value ? (int)reader["PendingRebootCount"] : 0,
                LastPatchDate = reader["LastPatchDate"] != System.DBNull.Value ? (DateTime)reader["LastPatchDate"] : DateTime.MinValue,
                Notes = reader["Notes"]?.ToString()
            };
        }

        private void AddComputerParameters(SqlCommand cmd, ComputerInfo computer)
        {
            cmd.Parameters.AddWithValue("@Hostname",          computer.Hostname ?? string.Empty);
            cmd.Parameters.AddWithValue("@OS",                computer.OS ?? string.Empty);
            cmd.Parameters.AddWithValue("@OSVersion",         computer.OSVersion ?? string.Empty);
            cmd.Parameters.AddWithValue("@Status",            computer.Status ?? string.Empty);
            cmd.Parameters.AddWithValue("@Manufacturer",      computer.Manufacturer ?? string.Empty);
            cmd.Parameters.AddWithValue("@Model",             computer.Model ?? string.Empty);
            cmd.Parameters.AddWithValue("@SerialNumber",      computer.SerialNumber ?? string.Empty);
            cmd.Parameters.AddWithValue("@AssetTag",          computer.AssetTag ?? string.Empty);
            cmd.Parameters.AddWithValue("@ChassisType",       computer.ChassisType ?? string.Empty);
            cmd.Parameters.AddWithValue("@IPAddress",         computer.IPAddress ?? string.Empty);
            cmd.Parameters.AddWithValue("@MACAddress",        computer.MACAddress ?? string.Empty);
            cmd.Parameters.AddWithValue("@Domain",            computer.Domain ?? string.Empty);
            cmd.Parameters.AddWithValue("@DomainController",  computer.DomainController ?? string.Empty);
            cmd.Parameters.AddWithValue("@LastLoggedOnUser",  computer.LastLoggedOnUser ?? string.Empty);
            cmd.Parameters.AddWithValue("@RAM_GB",            computer.RAM_GB);
            cmd.Parameters.AddWithValue("@CPU",               computer.CPU ?? string.Empty);
            cmd.Parameters.AddWithValue("@DiskSize_GB",       computer.DiskSize_GB);
            cmd.Parameters.AddWithValue("@DiskFree_GB",       computer.DiskFree_GB);
            cmd.Parameters.AddWithValue("@LastSeen",          computer.LastSeen);
            cmd.Parameters.AddWithValue("@LastBootTime",      (object)computer.LastBootTime ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@InstallDate",       computer.InstallDate != DateTime.MinValue ? (object)computer.InstallDate : DBNull.Value);
            cmd.Parameters.AddWithValue("@Uptime",            (object)computer.Uptime ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@BitLockerStatus",   computer.BitLockerStatus ?? string.Empty);
            cmd.Parameters.AddWithValue("@TPMVersion",        computer.TPMVersion ?? string.Empty);
            cmd.Parameters.AddWithValue("@AntivirusProduct",  computer.AntivirusProduct ?? string.Empty);
            cmd.Parameters.AddWithValue("@AntivirusStatus",   computer.AntivirusStatus ?? string.Empty);
            cmd.Parameters.AddWithValue("@FirewallStatus",    computer.FirewallStatus ?? string.Empty);
            cmd.Parameters.AddWithValue("@PendingRebootCount", computer.PendingRebootCount);
            cmd.Parameters.AddWithValue("@LastPatchDate",     computer.LastPatchDate != DateTime.MinValue ? (object)computer.LastPatchDate : DBNull.Value);
            cmd.Parameters.AddWithValue("@Notes",             computer.Notes ?? string.Empty);
            cmd.Parameters.AddWithValue("@RawDataJson",       computer.RawDataJson ?? string.Empty);
        }

        public void Dispose()
        {
            LogManager.LogInfo("SQL Server provider disposed");
        }
    }
}
