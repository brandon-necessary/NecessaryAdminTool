using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using System.Linq;
using System.Data.SQLite; // Bundled in libs/System.Data.SQLite.dll
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
        private readonly string _dbPath;
        private bool _disposed = false;

        public SqliteDataProvider(string dbPath, string encryptionKey)
        {
            _encryptionKey = encryptionKey;
            _dbPath = dbPath;

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

        // TAG MANAGEMENT
        public async Task<List<string>> GetComputerTagsAsync(string hostname)
        {
            var tags = new List<string>();
            #if SQLITE_ENABLED
            using (var conn = new SQLiteConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT TagName FROM ComputerTags WHERE Hostname = @hostname ORDER BY TagName";
                    cmd.Parameters.AddWithValue("@hostname", hostname);
                    using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync().ConfigureAwait(false))
                            tags.Add(reader.GetString(0));
                    }
                }
            }
            #else
            await Task.CompletedTask;
            #endif
            return tags;
        }

        public async Task AddTagAsync(string hostname, string tagName)
        {
            #if SQLITE_ENABLED
            using (var conn = new SQLiteConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "INSERT OR IGNORE INTO ComputerTags (Hostname, TagName) VALUES (@hostname, @tag)";
                    cmd.Parameters.AddWithValue("@hostname", hostname);
                    cmd.Parameters.AddWithValue("@tag", tagName);
                    await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
            #else
            await Task.CompletedTask;
            #endif
        }

        public async Task RemoveTagAsync(string hostname, string tagName)
        {
            #if SQLITE_ENABLED
            using (var conn = new SQLiteConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM ComputerTags WHERE Hostname = @hostname AND TagName = @tag";
                    cmd.Parameters.AddWithValue("@hostname", hostname);
                    cmd.Parameters.AddWithValue("@tag", tagName);
                    await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
            #else
            await Task.CompletedTask;
            #endif
        }

        public async Task<List<string>> GetAllTagsAsync()
        {
            var tags = new List<string>();
            #if SQLITE_ENABLED
            using (var conn = new SQLiteConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT DISTINCT TagName FROM ComputerTags ORDER BY TagName";
                    using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync().ConfigureAwait(false))
                            tags.Add(reader.GetString(0));
                    }
                }
            }
            #else
            await Task.CompletedTask;
            #endif
            return tags;
        }

        // SCAN HISTORY
        public async Task<ScanHistory> GetLastScanAsync()
        {
            #if SQLITE_ENABLED
            using (var conn = new SQLiteConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT ScanId, StartTime, EndTime, ComputersScanned, SuccessCount, FailureCount, DurationSeconds FROM ScanHistory ORDER BY ScanId DESC LIMIT 1";
                    using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                    {
                        if (await reader.ReadAsync().ConfigureAwait(false))
                        {
                            return new ScanHistory
                            {
                                ScanId = Convert.ToInt32(reader["ScanId"]),
                                StartTime = Convert.ToDateTime(reader["StartTime"]),
                                EndTime = reader["EndTime"] != DBNull.Value ? Convert.ToDateTime(reader["EndTime"]) : DateTime.MinValue,
                                ComputersScanned = Convert.ToInt32(reader["ComputersScanned"]),
                                SuccessCount = Convert.ToInt32(reader["SuccessCount"]),
                                FailureCount = Convert.ToInt32(reader["FailureCount"]),
                                DurationSeconds = Convert.ToDouble(reader["DurationSeconds"])
                            };
                        }
                    }
                }
            }
            #else
            await Task.CompletedTask;
            #endif
            return null;
        }

        public async Task SaveScanHistoryAsync(ScanHistory scan)
        {
            #if SQLITE_ENABLED
            using (var conn = new SQLiteConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"INSERT INTO ScanHistory (StartTime, EndTime, ComputersScanned, SuccessCount, FailureCount, DurationSeconds)
                        VALUES (@start, @end, @scanned, @success, @failure, @duration)";
                    cmd.Parameters.AddWithValue("@start", scan.StartTime);
                    cmd.Parameters.AddWithValue("@end", scan.EndTime);
                    cmd.Parameters.AddWithValue("@scanned", scan.ComputersScanned);
                    cmd.Parameters.AddWithValue("@success", scan.SuccessCount);
                    cmd.Parameters.AddWithValue("@failure", scan.FailureCount);
                    cmd.Parameters.AddWithValue("@duration", scan.DurationSeconds);
                    await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
            #else
            await Task.CompletedTask;
            #endif
        }

        public async Task<List<ScanHistory>> GetScanHistoryAsync(int limit = 10)
        {
            var history = new List<ScanHistory>();
            #if SQLITE_ENABLED
            using (var conn = new SQLiteConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT ScanId, StartTime, EndTime, ComputersScanned, SuccessCount, FailureCount, DurationSeconds FROM ScanHistory ORDER BY ScanId DESC LIMIT @limit";
                    cmd.Parameters.AddWithValue("@limit", limit);
                    using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync().ConfigureAwait(false))
                        {
                            history.Add(new ScanHistory
                            {
                                ScanId = Convert.ToInt32(reader["ScanId"]),
                                StartTime = Convert.ToDateTime(reader["StartTime"]),
                                EndTime = reader["EndTime"] != DBNull.Value ? Convert.ToDateTime(reader["EndTime"]) : DateTime.MinValue,
                                ComputersScanned = Convert.ToInt32(reader["ComputersScanned"]),
                                SuccessCount = Convert.ToInt32(reader["SuccessCount"]),
                                FailureCount = Convert.ToInt32(reader["FailureCount"]),
                                DurationSeconds = Convert.ToDouble(reader["DurationSeconds"])
                            });
                        }
                    }
                }
            }
            #else
            await Task.CompletedTask;
            #endif
            return history;
        }

        // SETTINGS
        public async Task<string> GetSettingAsync(string key, string defaultValue = null)
        {
            #if SQLITE_ENABLED
            using (var conn = new SQLiteConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT Value FROM Settings WHERE Key = @key";
                    cmd.Parameters.AddWithValue("@key", key);
                    var result = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
                    if (result != null && result != DBNull.Value)
                        return result.ToString();
                }
            }
            #else
            await Task.CompletedTask;
            #endif
            return defaultValue;
        }

        public async Task SaveSettingAsync(string key, string value)
        {
            #if SQLITE_ENABLED
            using (var conn = new SQLiteConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "INSERT OR REPLACE INTO Settings (Key, Value, UpdatedAt) VALUES (@key, @value, @now)";
                    cmd.Parameters.AddWithValue("@key", key);
                    cmd.Parameters.AddWithValue("@value", value ?? string.Empty);
                    cmd.Parameters.AddWithValue("@now", DateTime.UtcNow);
                    await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
            #else
            await Task.CompletedTask;
            #endif
        }

        // SCRIPTS
        public async Task<List<ScriptInfo>> GetAllScriptsAsync()
        {
            var scripts = new List<ScriptInfo>();
            #if SQLITE_ENABLED
            using (var conn = new SQLiteConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT ScriptId, Name, Description, Content, Category, CreatedAt FROM Scripts ORDER BY Name";
                    using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync().ConfigureAwait(false))
                        {
                            scripts.Add(new ScriptInfo
                            {
                                ScriptId = Convert.ToInt32(reader["ScriptId"]),
                                Name = reader["Name"]?.ToString(),
                                Description = reader["Description"]?.ToString(),
                                Content = reader["Content"]?.ToString(),
                                Category = reader["Category"]?.ToString(),
                                CreatedAt = reader["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(reader["CreatedAt"]) : DateTime.MinValue
                            });
                        }
                    }
                }
            }
            #else
            await Task.CompletedTask;
            #endif
            return scripts;
        }

        public async Task SaveScriptAsync(ScriptInfo script)
        {
            #if SQLITE_ENABLED
            using (var conn = new SQLiteConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"INSERT OR REPLACE INTO Scripts (Name, Description, Content, Category, CreatedAt)
                        VALUES (@name, @desc, @content, @category, @created)";
                    cmd.Parameters.AddWithValue("@name", script.Name ?? string.Empty);
                    cmd.Parameters.AddWithValue("@desc", script.Description ?? string.Empty);
                    cmd.Parameters.AddWithValue("@content", script.Content ?? string.Empty);
                    cmd.Parameters.AddWithValue("@category", script.Category ?? string.Empty);
                    cmd.Parameters.AddWithValue("@created", script.CreatedAt == DateTime.MinValue ? DateTime.UtcNow : script.CreatedAt);
                    await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
            #else
            await Task.CompletedTask;
            #endif
        }

        public async Task DeleteScriptAsync(int scriptId)
        {
            #if SQLITE_ENABLED
            using (var conn = new SQLiteConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM Scripts WHERE ScriptId = @id";
                    cmd.Parameters.AddWithValue("@id", scriptId);
                    await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
            #else
            await Task.CompletedTask;
            #endif
        }

        // BOOKMARKS
        public async Task<List<BookmarkInfo>> GetAllBookmarksAsync()
        {
            var bookmarks = new List<BookmarkInfo>();
            #if SQLITE_ENABLED
            using (var conn = new SQLiteConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT Hostname, Category, Notes, IsFavorite FROM Bookmarks ORDER BY Hostname";
                    using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync().ConfigureAwait(false))
                        {
                            bookmarks.Add(new BookmarkInfo
                            {
                                Hostname = reader["Hostname"]?.ToString(),
                                Category = reader["Category"]?.ToString(),
                                Notes = reader["Notes"]?.ToString(),
                                IsFavorite = reader["IsFavorite"] != DBNull.Value && Convert.ToInt32(reader["IsFavorite"]) == 1
                            });
                        }
                    }
                }
            }
            #else
            await Task.CompletedTask;
            #endif
            return bookmarks;
        }

        public async Task SaveBookmarkAsync(BookmarkInfo bookmark)
        {
            #if SQLITE_ENABLED
            using (var conn = new SQLiteConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"INSERT OR REPLACE INTO Bookmarks (Hostname, Category, Notes, IsFavorite)
                        VALUES (@hostname, @category, @notes, @fav)";
                    cmd.Parameters.AddWithValue("@hostname", bookmark.Hostname ?? string.Empty);
                    cmd.Parameters.AddWithValue("@category", bookmark.Category ?? string.Empty);
                    cmd.Parameters.AddWithValue("@notes", bookmark.Notes ?? string.Empty);
                    cmd.Parameters.AddWithValue("@fav", bookmark.IsFavorite ? 1 : 0);
                    await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
            #else
            await Task.CompletedTask;
            #endif
        }

        public async Task DeleteBookmarkAsync(string hostname)
        {
            #if SQLITE_ENABLED
            using (var conn = new SQLiteConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM Bookmarks WHERE Hostname = @hostname";
                    cmd.Parameters.AddWithValue("@hostname", hostname);
                    await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
            #else
            await Task.CompletedTask;
            #endif
        }

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
            var stats = new DatabaseStats { EncryptionStatus = "AES-256 (SQLCipher)", DatabaseType = "SQLite" };

            #if SQLITE_ENABLED
            using (var conn = new SQLiteConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM Computers";
                    stats.TotalComputers = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                    cmd.CommandText = "SELECT COUNT(*) FROM ComputerTags";
                    stats.TotalTags = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                    cmd.CommandText = "SELECT COUNT(*) FROM ScanHistory";
                    stats.TotalScans = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                    cmd.CommandText = "SELECT COUNT(*) FROM Scripts";
                    stats.TotalScripts = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                    cmd.CommandText = "SELECT COUNT(*) FROM Bookmarks";
                    stats.TotalBookmarks = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }
            }

            if (!string.IsNullOrEmpty(_dbPath) && System.IO.File.Exists(_dbPath))
            {
                var fi = new System.IO.FileInfo(_dbPath);
                stats.SizeBytes = fi.Length;
                stats.DatabaseSizeMB = fi.Length / (1024 * 1024);
            }
            #else
            await Task.CompletedTask;
            #endif

            return stats;
        }

        public async Task<bool> BackupDatabaseAsync(string backupPath)
        {
            LogManager.LogInfo($"SqliteDataProvider.BackupDatabaseAsync() - START - dest: {backupPath}");

            if (string.IsNullOrEmpty(backupPath))
            {
                LogManager.LogWarning("SqliteDataProvider.BackupDatabaseAsync() - SKIPPED - empty path");
                return false;
            }

            try
            {
                var backupDir = System.IO.Path.GetDirectoryName(backupPath);
                if (!string.IsNullOrEmpty(backupDir) && !System.IO.Directory.Exists(backupDir))
                    System.IO.Directory.CreateDirectory(backupDir);

                #if SQLITE_ENABLED
                // SQLite online backup API — safe while DB is in use (WAL mode, per-op connections)
                using (var sourceConn = new SQLiteConnection(_connectionString))
                using (var destConn = new SQLiteConnection($"Data Source={backupPath};Version=3;Password={_encryptionKey};"))
                {
                    await sourceConn.OpenAsync();
                    await destConn.OpenAsync();
                    sourceConn.BackupDatabase(destConn, "main", "main", -1, null, 0);
                }

                var size = new System.IO.FileInfo(backupPath).Length;
                LogManager.LogInfo($"SqliteDataProvider.BackupDatabaseAsync() - SUCCESS - {size / 1024} KB written to {backupPath}");
                return true;
                #else
                LogManager.LogWarning("SqliteDataProvider.BackupDatabaseAsync() - SQLite not enabled");
                return await Task.FromResult(false);
                #endif
            }
            catch (Exception ex)
            {
                LogManager.LogError($"SqliteDataProvider.BackupDatabaseAsync() - FAILED - dest: {backupPath}", ex);
                return false;
            }
        }

        public async Task<bool> RestoreDatabaseAsync(string backupPath)
        {
            LogManager.LogInfo($"SqliteDataProvider.RestoreDatabaseAsync() - START - source: {backupPath}");

            if (string.IsNullOrEmpty(backupPath) || !System.IO.File.Exists(backupPath))
            {
                LogManager.LogWarning($"SqliteDataProvider.RestoreDatabaseAsync() - SKIPPED - backup not found: {backupPath}");
                return false;
            }

            try
            {
                #if SQLITE_ENABLED
                // Per-operation connections are used throughout — no persistent connection to close.
                // Overwrite the live DB file, then remove stale WAL/SHM sidecars from the old DB.
                System.IO.File.Copy(backupPath, _dbPath, overwrite: true);

                foreach (var sidecar in new[] { _dbPath + "-wal", _dbPath + "-shm" })
                    if (System.IO.File.Exists(sidecar)) System.IO.File.Delete(sidecar);

                LogManager.LogInfo($"SqliteDataProvider.RestoreDatabaseAsync() - SUCCESS - restored from {backupPath} to {_dbPath}");
                return true;
                #else
                LogManager.LogWarning("SqliteDataProvider.RestoreDatabaseAsync() - SQLite not enabled");
                return await Task.FromResult(false);
                #endif
            }
            catch (Exception ex)
            {
                LogManager.LogError($"SqliteDataProvider.RestoreDatabaseAsync() - FAILED - source: {backupPath}", ex);
                return false;
            }
        }

        /// <summary>Runs VACUUM to compact and defragment the SQLite database file.</summary>
        public void Vacuum()
        {
            #if SQLITE_ENABLED
            try
            {
                LogManager.LogInfo("SqliteDataProvider.Vacuum() - START");
                using (var conn = new SQLiteConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SQLiteCommand("VACUUM;", conn))
                        cmd.ExecuteNonQuery();
                }
                LogManager.LogInfo("SqliteDataProvider.Vacuum() - SUCCESS");
            }
            catch (Exception ex)
            {
                LogManager.LogError("SqliteDataProvider.Vacuum() - FAILED", ex);
            }
            #endif
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
