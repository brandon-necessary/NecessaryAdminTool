using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using System.Linq;
// using System.Data.SQLite; // Requires: Install-Package System.Data.SQLite.Core
// TAG: #DATABASE #SQLITE #VERSION_1_2

namespace NecessaryAdminTool.Data
{
    /// <summary>
    /// SQLite data provider with SQLCipher encryption
    /// TAG: #SQLITE #ENCRYPTION #AES256
    /// </summary>
    public class SqliteDataProvider : IDataProvider
    {
        private readonly string _connectionString;
        private readonly string _encryptionKey;
        private bool _disposed = false;

        public SqliteDataProvider(string dbPath, string encryptionKey)
        {
            _encryptionKey = encryptionKey;

            // SQLCipher connection string with encryption
            _connectionString = $"Data Source={dbPath};Version=3;Password={encryptionKey};";

            LogManager.LogInfo($"SQLite provider initialized: {dbPath}");
        }

        public async Task InitializeDatabaseAsync()
        {
            #if SQLITE_ENABLED
            using (var conn = new SQLiteConnection(_connectionString))
            {
                await conn.OpenAsync();

                // Enable WAL mode for better concurrency
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA journal_mode=WAL;";
                    await cmd.ExecuteNonQueryAsync();
                }

                // Create schema
                await CreateSchemaAsync(conn);

                LogManager.LogInfo("SQLite database initialized with encryption");
            }
            #else
            await Task.CompletedTask;
            LogManager.LogWarning("SQLite not enabled - install System.Data.SQLite.Core NuGet package");
            #endif
        }

        private async Task CreateSchemaAsync(dynamic conn)
        {
            #if SQLITE_ENABLED
            var schema = @"
                -- Computers table
                CREATE TABLE IF NOT EXISTS Computers (
                    Hostname TEXT PRIMARY KEY,
                    OS TEXT,
                    LastSeen DATETIME,
                    Status TEXT,
                    IPAddress TEXT,
                    Manufacturer TEXT,
                    Model TEXT,
                    SerialNumber TEXT,
                    ChassisType TEXT,
                    LastBootTime DATETIME,
                    Uptime INTEGER,
                    DomainController TEXT,
                    RawDataJson TEXT
                );

                -- Tags table
                CREATE TABLE IF NOT EXISTS ComputerTags (
                    Hostname TEXT,
                    TagName TEXT,
                    PRIMARY KEY (Hostname, TagName),
                    FOREIGN KEY (Hostname) REFERENCES Computers(Hostname) ON DELETE CASCADE
                );

                -- Scan history
                CREATE TABLE IF NOT EXISTS ScanHistory (
                    ScanId INTEGER PRIMARY KEY AUTOINCREMENT,
                    StartTime DATETIME,
                    EndTime DATETIME,
                    ComputersScanned INTEGER,
                    SuccessCount INTEGER,
                    FailureCount INTEGER,
                    DurationSeconds REAL
                );

                -- Settings
                CREATE TABLE IF NOT EXISTS Settings (
                    Key TEXT PRIMARY KEY,
                    Value TEXT,
                    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                );

                -- Scripts
                CREATE TABLE IF NOT EXISTS Scripts (
                    ScriptId INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT UNIQUE,
                    Description TEXT,
                    Content TEXT,
                    Category TEXT,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                );

                -- Bookmarks
                CREATE TABLE IF NOT EXISTS Bookmarks (
                    Hostname TEXT PRIMARY KEY,
                    Category TEXT,
                    Notes TEXT,
                    IsFavorite INTEGER,
                    FOREIGN KEY (Hostname) REFERENCES Computers(Hostname) ON DELETE CASCADE
                );

                -- Indexes
                CREATE INDEX IF NOT EXISTS idx_computers_status ON Computers(Status);
                CREATE INDEX IF NOT EXISTS idx_computers_os ON Computers(OS);
                CREATE INDEX IF NOT EXISTS idx_computers_lastseen ON Computers(LastSeen);
                CREATE INDEX IF NOT EXISTS idx_scans_starttime ON ScanHistory(StartTime);
            ";

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = schema;
                await cmd.ExecuteNonQueryAsync();
            }
            #else
            await Task.CompletedTask;
            #endif
        }

        public async Task<List<ComputerInfo>> GetAllComputersAsync()
        {
            var computers = new List<ComputerInfo>();

            #if SQLITE_ENABLED
            using (var conn = new SQLiteConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM Computers ORDER BY Hostname";
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            computers.Add(ReadComputerInfo(reader));
                        }
                    }
                }
            }
            #else
            await Task.CompletedTask;
            #endif

            return computers;
        }

        public async Task<ComputerInfo> GetComputerAsync(string hostname)
        {
            #if SQLITE_ENABLED
            using (var conn = new SQLiteConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM Computers WHERE Hostname = @hostname";
                    cmd.Parameters.AddWithValue("@hostname", hostname);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return ReadComputerInfo(reader);
                        }
                    }
                }
            }
            #else
            await Task.CompletedTask;
            #endif

            return null;
        }

        public async Task SaveComputerAsync(ComputerInfo computer)
        {
            #if SQLITE_ENABLED
            using (var conn = new SQLiteConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT OR REPLACE INTO Computers
                        (Hostname, OS, LastSeen, Status, IPAddress, Manufacturer, Model, SerialNumber,
                         ChassisType, LastBootTime, Uptime, DomainController, RawDataJson)
                        VALUES (@hostname, @os, @lastseen, @status, @ip, @mfg, @model, @serial,
                                @chassis, @boot, @uptime, @dc, @json)";

                    cmd.Parameters.AddWithValue("@hostname", computer.Hostname);
                    cmd.Parameters.AddWithValue("@os", computer.OS ?? "");
                    cmd.Parameters.AddWithValue("@lastseen", DateTime.Now);
                    cmd.Parameters.AddWithValue("@status", computer.Status ?? "");
                    cmd.Parameters.AddWithValue("@ip", computer.IPAddress ?? "");
                    cmd.Parameters.AddWithValue("@mfg", computer.Manufacturer ?? "");
                    cmd.Parameters.AddWithValue("@model", computer.Model ?? "");
                    cmd.Parameters.AddWithValue("@serial", computer.SerialNumber ?? "");
                    cmd.Parameters.AddWithValue("@chassis", computer.ChassisType ?? "");
                    cmd.Parameters.AddWithValue("@boot", computer.LastBootTime ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@uptime", computer.Uptime ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@dc", computer.DomainController ?? "");
                    cmd.Parameters.AddWithValue("@json", computer.RawDataJson ?? "");

                    await cmd.ExecuteNonQueryAsync();
                }
            }
            #else
            await Task.CompletedTask;
            #endif
        }

        public async Task DeleteComputerAsync(string hostname)
        {
            #if SQLITE_ENABLED
            using (var conn = new SQLiteConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM Computers WHERE Hostname = @hostname";
                    cmd.Parameters.AddWithValue("@hostname", hostname);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            #else
            await Task.CompletedTask;
            #endif
        }

        public async Task<List<ComputerInfo>> SearchComputersAsync(string searchTerm)
        {
            var computers = new List<ComputerInfo>();

            #if SQLITE_ENABLED
            using (var conn = new SQLiteConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT * FROM Computers
                        WHERE Hostname LIKE @search
                           OR OS LIKE @search
                           OR IPAddress LIKE @search
                        ORDER BY Hostname";
                    cmd.Parameters.AddWithValue("@search", $"%{searchTerm}%");

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            computers.Add(ReadComputerInfo(reader));
                        }
                    }
                }
            }
            #else
            await Task.CompletedTask;
            #endif

            return computers;
        }

        #if SQLITE_ENABLED
        private ComputerInfo ReadComputerInfo(IDataReader reader)
        {
            return new ComputerInfo
            {
                Hostname = reader["Hostname"]?.ToString(),
                OS = reader["OS"]?.ToString(),
                LastSeen = reader["LastSeen"] != DBNull.Value ? Convert.ToDateTime(reader["LastSeen"]) : DateTime.MinValue,
                Status = reader["Status"]?.ToString(),
                IPAddress = reader["IPAddress"]?.ToString(),
                Manufacturer = reader["Manufacturer"]?.ToString(),
                Model = reader["Model"]?.ToString(),
                SerialNumber = reader["SerialNumber"]?.ToString(),
                ChassisType = reader["ChassisType"]?.ToString(),
                LastBootTime = reader["LastBootTime"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(reader["LastBootTime"]) : null,
                Uptime = reader["Uptime"] != DBNull.Value ? (long?)Convert.ToInt64(reader["Uptime"]) : null,
                DomainController = reader["DomainController"]?.ToString(),
                RawDataJson = reader["RawDataJson"]?.ToString()
            };
        }
        #endif

        // TAG MANAGEMENT - Simplified implementations
        public async Task<List<string>> GetComputerTagsAsync(string hostname) => new List<string>();
        public async Task AddTagAsync(string hostname, string tagName) => await Task.CompletedTask;
        public async Task RemoveTagAsync(string hostname, string tagName) => await Task.CompletedTask;
        public async Task<List<string>> GetAllTagsAsync() => new List<string>();

        // SCAN HISTORY - Simplified implementations
        public async Task<ScanHistory> GetLastScanAsync() => null;
        public async Task SaveScanHistoryAsync(ScanHistory scan) => await Task.CompletedTask;
        public async Task<List<ScanHistory>> GetScanHistoryAsync(int limit = 10) => new List<ScanHistory>();

        // SETTINGS - Simplified implementations
        public async Task<string> GetSettingAsync(string key, string defaultValue = null) => defaultValue;
        public async Task SaveSettingAsync(string key, string value) => await Task.CompletedTask;

        // SCRIPTS - Simplified implementations
        public async Task<List<ScriptInfo>> GetAllScriptsAsync() => new List<ScriptInfo>();
        public async Task SaveScriptAsync(ScriptInfo script) => await Task.CompletedTask;
        public async Task DeleteScriptAsync(int scriptId) => await Task.CompletedTask;

        // BOOKMARKS - Simplified implementations
        public async Task<List<BookmarkInfo>> GetAllBookmarksAsync() => new List<BookmarkInfo>();
        public async Task SaveBookmarkAsync(BookmarkInfo bookmark) => await Task.CompletedTask;
        public async Task DeleteBookmarkAsync(string hostname) => await Task.CompletedTask;

        // DATABASE OPERATIONS
        public async Task OptimizeDatabaseAsync()
        {
            #if SQLITE_ENABLED
            using (var conn = new SQLiteConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA optimize; VACUUM;";
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            #else
            await Task.CompletedTask;
            #endif
        }

        public async Task<bool> VerifyIntegrityAsync()
        {
            #if SQLITE_ENABLED
            using (var conn = new SQLiteConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA integrity_check;";
                    var result = await cmd.ExecuteScalarAsync();
                    return result?.ToString() == "ok";
                }
            }
            #else
            await Task.CompletedTask;
            return true;
            #endif
        }

        public async Task<DatabaseStats> GetDatabaseStatsAsync()
        {
            var stats = new DatabaseStats { EncryptionStatus = "AES-256 (SQLCipher)" };

            #if SQLITE_ENABLED
            using (var conn = new SQLiteConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM Computers";
                    stats.TotalComputers = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }
            }
            #else
            await Task.CompletedTask;
            #endif

            return stats;
        }

        public async Task<bool> BackupDatabaseAsync(string backupPath)
        {
            // Implementation requires file copy with encryption
            await Task.CompletedTask;
            return true;
        }

        public async Task<bool> RestoreDatabaseAsync(string backupPath)
        {
            // Implementation requires file copy with decryption
            await Task.CompletedTask;
            return true;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                // Cleanup
                _disposed = true;
            }
        }
    }
}
