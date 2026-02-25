using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.IO;
using System.Threading.Tasks;
// TAG: #DATABASE #MS_ACCESS #VERSION_1_2

namespace NecessaryAdminTool.Data
{
    /// <summary>
    /// Microsoft Access data provider implementation
    /// TAG: #EXCEL_INTEGRATION #FAMILIAR_INTERFACE #50K_COMPUTERS
    /// </summary>
    public class AccessDataProvider : IDataProvider
    {
        private readonly string _databasePath;
        private OleDbConnection _connection;
        private bool _disposed = false;
        // Serialise all DB operations — OleDb/Access is not thread-safe for concurrent writers
        private readonly System.Threading.SemaphoreSlim _dbLock = new System.Threading.SemaphoreSlim(1, 1);

        public AccessDataProvider(string databasePath)
        {
            _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
            LogManager.LogInfo($"Access provider initialized: {_databasePath}");
        }

        public async Task InitializeDatabaseAsync()
        {
            try
            {
                // Create database file if it doesn't exist
                if (!File.Exists(_databasePath))
                {
                    CreateAccessDatabase();
                }

                // Open connection
                var connectionString = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={_databasePath};Persist Security Info=False;";
                _connection = new OleDbConnection(connectionString);
                await Task.Run(() => _connection.Open());

                // Create schema
                await CreateSchemaAsync();

                LogManager.LogInfo("Access database initialized successfully");
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to initialize Access database", ex);
                throw new InvalidOperationException("Cannot initialize Access database. Ensure Microsoft Access Database Engine is installed.", ex);
            }
        }

        private void CreateAccessDatabase()
        {
            try
            {
                // Ensure directory exists
                var directory = Path.GetDirectoryName(_databasePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Create empty Access database using ADOX
                var catalog = Activator.CreateInstance(Type.GetTypeFromProgID("ADOX.Catalog"));
                var connectionString = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={_databasePath};";
                catalog.GetType().InvokeMember("Create",
                    System.Reflection.BindingFlags.InvokeMethod,
                    null, catalog, new object[] { connectionString });

                LogManager.LogInfo($"Created new Access database: {_databasePath}");
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to create Access database file", ex);
                throw;
            }
        }

        private async Task CreateSchemaAsync()
        {
            try
            {
                // Check if tables exist
                var tables = await GetExistingTablesAsync();

                if (!tables.Contains("Computers"))
                {
                    var createComputersTable = @"
                        CREATE TABLE Computers (
                            Hostname TEXT(255) PRIMARY KEY,
                            OS TEXT(100),
                            OSVersion TEXT(50),
                            Status TEXT(50),
                            Manufacturer TEXT(100),
                            Model TEXT(100),
                            SerialNumber TEXT(100),
                            AssetTag TEXT(100),
                            ChassisType TEXT(100),
                            IPAddress TEXT(50),
                            MACAddress TEXT(50),
                            Domain TEXT(100),
                            DomainController TEXT(255),
                            LastLoggedOnUser TEXT(100),
                            RAM_GB INTEGER,
                            CPU TEXT(200),
                            DiskSize_GB INTEGER,
                            DiskFree_GB INTEGER,
                            LastSeen DATETIME,
                            LastBootTime DATETIME,
                            InstallDate DATETIME,
                            Uptime LONG,
                            BitLockerStatus TEXT(50),
                            TPMVersion TEXT(50),
                            AntivirusProduct TEXT(100),
                            AntivirusStatus TEXT(50),
                            FirewallStatus TEXT(50),
                            PendingRebootCount INTEGER,
                            LastPatchDate DATETIME,
                            Notes MEMO,
                            RawDataJson MEMO,
                            CreatedDate DATETIME,
                            ModifiedDate DATETIME
                        )";
                    await ExecuteNonQueryAsync(createComputersTable);
                }

                if (!tables.Contains("ScanHistory"))
                {
                    var createScanHistoryTable = @"
                        CREATE TABLE ScanHistory (
                            Id AUTOINCREMENT PRIMARY KEY,
                            Hostname TEXT(255),
                            ScanDate DATETIME,
                            ScanType TEXT(50),
                            ComputersFound INTEGER,
                            ErrorCount INTEGER,
                            DurationSeconds INTEGER,
                            Notes MEMO
                        )";
                    await ExecuteNonQueryAsync(createScanHistoryTable);
                }

                if (!tables.Contains("Scripts"))
                {
                    var createScriptsTable = @"
                        CREATE TABLE Scripts (
                            Id AUTOINCREMENT PRIMARY KEY,
                            Name TEXT(200),
                            Description MEMO,
                            ScriptContent MEMO,
                            ScriptType TEXT(50),
                            Category TEXT(100),
                            CreatedDate DATETIME,
                            ModifiedDate DATETIME,
                            ExecutionCount INTEGER,
                            LastExecuted DATETIME
                        )";
                    await ExecuteNonQueryAsync(createScriptsTable);
                }

                if (!tables.Contains("Bookmarks"))
                {
                    var createBookmarksTable = @"
                        CREATE TABLE Bookmarks (
                            Id AUTOINCREMENT PRIMARY KEY,
                            Name TEXT(200),
                            Hostname TEXT(255),
                            Category TEXT(100),
                            Notes MEMO,
                            CreatedDate DATETIME
                        )";
                    await ExecuteNonQueryAsync(createBookmarksTable);
                }

                if (!tables.Contains("ComputerTags"))
                {
                    var createTagsTable = @"
                        CREATE TABLE ComputerTags (
                            Hostname TEXT(255),
                            TagName TEXT(100),
                            CONSTRAINT PK_ComputerTags PRIMARY KEY (Hostname, TagName)
                        )";
                    await ExecuteNonQueryAsync(createTagsTable);
                }

                if (!tables.Contains("Settings"))
                {
                    var createSettingsTable = @"
                        CREATE TABLE Settings (
                            SettingKey TEXT(255) PRIMARY KEY,
                            SettingValue MEMO,
                            UpdatedAt DATETIME
                        )";
                    await ExecuteNonQueryAsync(createSettingsTable);
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to create Access database schema", ex);
                throw;
            }
        }

        private async Task<List<string>> GetExistingTablesAsync()
        {
            var tables = new List<string>();
            try
            {
                // Ensure connection is open
                if (_connection == null || _connection.State != System.Data.ConnectionState.Open)
                {
                    LogManager.LogWarning("Connection not open in GetExistingTablesAsync, returning empty list");
                    return tables;
                }

                await Task.Run(() =>
                {
                    try
                    {
                        var schema = _connection.GetOleDbSchemaTable(OleDbSchemaGuid.Tables,
                            new object[] { null, null, null, "TABLE" });

                        if (schema != null)
                        {
                            foreach (System.Data.DataRow row in schema.Rows)
                            {
                                if (row["TABLE_NAME"] != null && row["TABLE_NAME"] != DBNull.Value)
                                {
                                    tables.Add(row["TABLE_NAME"].ToString());
                                }
                            }
                        }
                    }
                    catch (OleDbException ex)
                    {
                        // Log but don't throw - database might be new
                        LogManager.LogWarning($"Could not retrieve Access schema (database might be new): {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                LogManager.LogError("Error in GetExistingTablesAsync", ex);
            }
            return tables;
        }

        private async Task ExecuteNonQueryAsync(string query)
        {
            using (var cmd = new OleDbCommand(query, _connection))
            {
                await Task.Run(() => cmd.ExecuteNonQuery());
            }
        }

        public async Task<List<ComputerInfo>> GetAllComputersAsync()
        {
            var computers = new List<ComputerInfo>();
            await _dbLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var query = "SELECT * FROM Computers ORDER BY Hostname";
                using (var cmd = new OleDbCommand(query, _connection))
                {
                    await Task.Run(() =>
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                computers.Add(MapReaderToComputer(reader));
                            }
                        }
                    }).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to retrieve computers from Access database", ex);
            }
            finally
            {
                _dbLock.Release();
            }
            return computers;
        }

        public async Task SaveComputerAsync(ComputerInfo computer)
        {
            await _dbLock.WaitAsync().ConfigureAwait(false);
            try
            {
                // Check if computer exists
                var existsQuery = "SELECT COUNT(*) FROM Computers WHERE Hostname = ?";
                bool exists = false;

                using (var cmd = new OleDbCommand(existsQuery, _connection))
                {
                    cmd.Parameters.AddWithValue("?", computer.Hostname);
                    var count = await Task.Run(() => (int)cmd.ExecuteScalar()).ConfigureAwait(false);
                    exists = count > 0;
                }

                string query;
                if (exists)
                {
                    query = @"UPDATE Computers SET
                        OS=?, OSVersion=?, Status=?, Manufacturer=?, Model=?, SerialNumber=?, AssetTag=?,
                        ChassisType=?, IPAddress=?, MACAddress=?, Domain=?, DomainController=?,
                        LastLoggedOnUser=?, RAM_GB=?, CPU=?, DiskSize_GB=?, DiskFree_GB=?,
                        LastSeen=?, LastBootTime=?, InstallDate=?, Uptime=?,
                        BitLockerStatus=?, TPMVersion=?, AntivirusProduct=?, AntivirusStatus=?,
                        FirewallStatus=?, PendingRebootCount=?, LastPatchDate=?, Notes=?, RawDataJson=?,
                        ModifiedDate=?
                        WHERE Hostname=?";
                }
                else
                {
                    query = @"INSERT INTO Computers (
                        Hostname, OS, OSVersion, Status, Manufacturer, Model, SerialNumber, AssetTag,
                        ChassisType, IPAddress, MACAddress, Domain, DomainController,
                        LastLoggedOnUser, RAM_GB, CPU, DiskSize_GB, DiskFree_GB,
                        LastSeen, LastBootTime, InstallDate, Uptime,
                        BitLockerStatus, TPMVersion, AntivirusProduct, AntivirusStatus,
                        FirewallStatus, PendingRebootCount, LastPatchDate, Notes, RawDataJson,
                        CreatedDate, ModifiedDate
                    ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";
                }

                using (var cmd = new OleDbCommand(query, _connection))
                {
                    if (exists) AddUpdateParameters(cmd, computer);
                    else        AddInsertParameters(cmd, computer);
                    await Task.Run(() => cmd.ExecuteNonQuery()).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to save computer {computer.Hostname} to Access database", ex);
                throw;
            }
            finally
            {
                _dbLock.Release();
            }
        }

        public async Task<ComputerInfo> GetComputerAsync(string hostname)
        {
            await _dbLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var query = "SELECT * FROM Computers WHERE Hostname = ?";
                using (var cmd = new OleDbCommand(query, _connection))
                {
                    cmd.Parameters.AddWithValue("?", hostname);
                    return await Task.Run(() =>
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read()) return MapReaderToComputer(reader);
                        }
                        return null;
                    }).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to get computer {hostname} from Access database", ex);
                return null;
            }
            finally
            {
                _dbLock.Release();
            }
        }

        public async Task DeleteComputerAsync(string hostname)
        {
            await _dbLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var query = "DELETE FROM Computers WHERE Hostname = ?";
                using (var cmd = new OleDbCommand(query, _connection))
                {
                    cmd.Parameters.AddWithValue("?", hostname);
                    await Task.Run(() => cmd.ExecuteNonQuery()).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to delete computer {hostname} from Access database", ex);
                throw;
            }
            finally
            {
                _dbLock.Release();
            }
        }

        public async Task<List<ComputerInfo>> SearchComputersAsync(string searchTerm)
        {
            var computers = new List<ComputerInfo>();
            await _dbLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var query = @"SELECT * FROM Computers
                    WHERE Hostname LIKE ? OR OS LIKE ? OR Manufacturer LIKE ? OR Model LIKE ? OR IPAddress LIKE ?
                    ORDER BY Hostname";
                var searchPattern = $"%{searchTerm}%";
                using (var cmd = new OleDbCommand(query, _connection))
                {
                    cmd.Parameters.AddWithValue("?", searchPattern);
                    cmd.Parameters.AddWithValue("?", searchPattern);
                    cmd.Parameters.AddWithValue("?", searchPattern);
                    cmd.Parameters.AddWithValue("?", searchPattern);
                    cmd.Parameters.AddWithValue("?", searchPattern);
                    await Task.Run(() =>
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read()) computers.Add(MapReaderToComputer(reader));
                        }
                    }).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to search computers with term '{searchTerm}' from Access database", ex);
            }
            finally
            {
                _dbLock.Release();
            }
            return computers;
        }

        // TAG MANAGEMENT
        public async Task<List<string>> GetComputerTagsAsync(string hostname)
        {
            var tags = new List<string>();
            await _dbLock.WaitAsync().ConfigureAwait(false);
            try
            {
                using (var cmd = new OleDbCommand("SELECT TagName FROM ComputerTags WHERE Hostname = ? ORDER BY TagName", _connection))
                {
                    cmd.Parameters.AddWithValue("?", hostname);
                    await Task.Run(() =>
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                                tags.Add(reader.GetString(0));
                        }
                    }).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to get tags for {hostname}", ex);
            }
            finally { _dbLock.Release(); }
            return tags;
        }

        public async Task AddTagAsync(string hostname, string tagName)
        {
            await _dbLock.WaitAsync().ConfigureAwait(false);
            try
            {
                // Check if tag exists first (Access doesn't support INSERT OR IGNORE)
                using (var checkCmd = new OleDbCommand("SELECT COUNT(*) FROM ComputerTags WHERE Hostname = ? AND TagName = ?", _connection))
                {
                    checkCmd.Parameters.AddWithValue("?", hostname);
                    checkCmd.Parameters.AddWithValue("?", tagName);
                    var count = await Task.Run(() => (int)checkCmd.ExecuteScalar()).ConfigureAwait(false);
                    if (count > 0) return;
                }
                using (var cmd = new OleDbCommand("INSERT INTO ComputerTags (Hostname, TagName) VALUES (?, ?)", _connection))
                {
                    cmd.Parameters.AddWithValue("?", hostname);
                    cmd.Parameters.AddWithValue("?", tagName);
                    await Task.Run(() => cmd.ExecuteNonQuery()).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to add tag '{tagName}' to {hostname}", ex);
            }
            finally { _dbLock.Release(); }
        }

        public async Task RemoveTagAsync(string hostname, string tagName)
        {
            await _dbLock.WaitAsync().ConfigureAwait(false);
            try
            {
                using (var cmd = new OleDbCommand("DELETE FROM ComputerTags WHERE Hostname = ? AND TagName = ?", _connection))
                {
                    cmd.Parameters.AddWithValue("?", hostname);
                    cmd.Parameters.AddWithValue("?", tagName);
                    await Task.Run(() => cmd.ExecuteNonQuery()).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to remove tag '{tagName}' from {hostname}", ex);
            }
            finally { _dbLock.Release(); }
        }

        public async Task<List<string>> GetAllTagsAsync()
        {
            var tags = new List<string>();
            await _dbLock.WaitAsync().ConfigureAwait(false);
            try
            {
                using (var cmd = new OleDbCommand("SELECT DISTINCT TagName FROM ComputerTags ORDER BY TagName", _connection))
                {
                    await Task.Run(() =>
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                                tags.Add(reader.GetString(0));
                        }
                    }).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to get all tags", ex);
            }
            finally { _dbLock.Release(); }
            return tags;
        }

        // SCAN HISTORY
        public async Task<ScanHistory> GetLastScanAsync()
        {
            await _dbLock.WaitAsync().ConfigureAwait(false);
            try
            {
                // Access doesn't support LIMIT — use TOP 1 + ORDER BY DESC
                using (var cmd = new OleDbCommand("SELECT TOP 1 Id, ScanDate, ComputersFound, ErrorCount, DurationSeconds FROM ScanHistory ORDER BY Id DESC", _connection))
                {
                    return await Task.Run(() =>
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new ScanHistory
                                {
                                    ScanId = (int)reader["Id"],
                                    StartTime = reader["ScanDate"] != DBNull.Value ? (DateTime)reader["ScanDate"] : DateTime.MinValue,
                                    ComputersScanned = reader["ComputersFound"] != DBNull.Value ? (int)reader["ComputersFound"] : 0,
                                    FailureCount = reader["ErrorCount"] != DBNull.Value ? (int)reader["ErrorCount"] : 0,
                                    DurationSeconds = reader["DurationSeconds"] != DBNull.Value ? Convert.ToDouble(reader["DurationSeconds"]) : 0
                                };
                            }
                            return null;
                        }
                    }).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to get last scan", ex);
                return null;
            }
            finally { _dbLock.Release(); }
        }

        public async Task SaveScanHistoryAsync(ScanHistory scan)
        {
            await _dbLock.WaitAsync().ConfigureAwait(false);
            try
            {
                using (var cmd = new OleDbCommand("INSERT INTO ScanHistory (ScanDate, ScanType, ComputersFound, ErrorCount, DurationSeconds, Notes) VALUES (?, ?, ?, ?, ?, ?)", _connection))
                {
                    cmd.Parameters.AddWithValue("?", scan.StartTime);
                    cmd.Parameters.AddWithValue("?", "Fleet Scan");
                    cmd.Parameters.AddWithValue("?", scan.ComputersScanned);
                    cmd.Parameters.AddWithValue("?", scan.FailureCount);
                    cmd.Parameters.AddWithValue("?", (int)scan.DurationSeconds);
                    cmd.Parameters.AddWithValue("?", $"Success: {scan.SuccessCount}, Failed: {scan.FailureCount}");
                    await Task.Run(() => cmd.ExecuteNonQuery()).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to save scan history", ex);
            }
            finally { _dbLock.Release(); }
        }

        public async Task<List<ScanHistory>> GetScanHistoryAsync(int limit = 10)
        {
            var history = new List<ScanHistory>();
            await _dbLock.WaitAsync().ConfigureAwait(false);
            try
            {
                // Access TOP requires literal, but parameterised TOP doesn't work — use TOP 100 as safe upper bound
                var topN = Math.Min(limit, 100);
                using (var cmd = new OleDbCommand($"SELECT TOP {topN} Id, ScanDate, ComputersFound, ErrorCount, DurationSeconds FROM ScanHistory ORDER BY Id DESC", _connection))
                {
                    await Task.Run(() =>
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                history.Add(new ScanHistory
                                {
                                    ScanId = (int)reader["Id"],
                                    StartTime = reader["ScanDate"] != DBNull.Value ? (DateTime)reader["ScanDate"] : DateTime.MinValue,
                                    ComputersScanned = reader["ComputersFound"] != DBNull.Value ? (int)reader["ComputersFound"] : 0,
                                    FailureCount = reader["ErrorCount"] != DBNull.Value ? (int)reader["ErrorCount"] : 0,
                                    DurationSeconds = reader["DurationSeconds"] != DBNull.Value ? Convert.ToDouble(reader["DurationSeconds"]) : 0
                                });
                            }
                        }
                    }).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to get scan history", ex);
            }
            finally { _dbLock.Release(); }
            return history;
        }

        // SETTINGS
        public async Task<string> GetSettingAsync(string key, string defaultValue = null)
        {
            await _dbLock.WaitAsync().ConfigureAwait(false);
            try
            {
                using (var cmd = new OleDbCommand("SELECT SettingValue FROM Settings WHERE SettingKey = ?", _connection))
                {
                    cmd.Parameters.AddWithValue("?", key);
                    var result = await Task.Run(() => cmd.ExecuteScalar()).ConfigureAwait(false);
                    if (result != null && result != DBNull.Value)
                        return result.ToString();
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to get setting '{key}'", ex);
            }
            finally { _dbLock.Release(); }
            return defaultValue;
        }

        public async Task SaveSettingAsync(string key, string value)
        {
            await _dbLock.WaitAsync().ConfigureAwait(false);
            try
            {
                // Check if key exists (Access doesn't support MERGE)
                using (var checkCmd = new OleDbCommand("SELECT COUNT(*) FROM Settings WHERE SettingKey = ?", _connection))
                {
                    checkCmd.Parameters.AddWithValue("?", key);
                    var count = await Task.Run(() => (int)checkCmd.ExecuteScalar()).ConfigureAwait(false);
                    if (count > 0)
                    {
                        using (var cmd = new OleDbCommand("UPDATE Settings SET SettingValue = ?, UpdatedAt = ? WHERE SettingKey = ?", _connection))
                        {
                            cmd.Parameters.AddWithValue("?", value ?? string.Empty);
                            cmd.Parameters.AddWithValue("?", DateTime.Now);
                            cmd.Parameters.AddWithValue("?", key);
                            await Task.Run(() => cmd.ExecuteNonQuery()).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        using (var cmd = new OleDbCommand("INSERT INTO Settings (SettingKey, SettingValue, UpdatedAt) VALUES (?, ?, ?)", _connection))
                        {
                            cmd.Parameters.AddWithValue("?", key);
                            cmd.Parameters.AddWithValue("?", value ?? string.Empty);
                            cmd.Parameters.AddWithValue("?", DateTime.Now);
                            await Task.Run(() => cmd.ExecuteNonQuery()).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to save setting '{key}'", ex);
            }
            finally { _dbLock.Release(); }
        }

        // SCRIPTS
        public async Task<List<ScriptInfo>> GetAllScriptsAsync()
        {
            var scripts = new List<ScriptInfo>();
            await _dbLock.WaitAsync().ConfigureAwait(false);
            try
            {
                using (var cmd = new OleDbCommand("SELECT Id, Name, Description, ScriptContent, Category, CreatedDate FROM Scripts ORDER BY Name", _connection))
                {
                    await Task.Run(() =>
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                scripts.Add(new ScriptInfo
                                {
                                    ScriptId = (int)reader["Id"],
                                    Name = reader["Name"]?.ToString(),
                                    Description = reader["Description"]?.ToString(),
                                    Content = reader["ScriptContent"]?.ToString(),
                                    Category = reader["Category"]?.ToString(),
                                    CreatedAt = reader["CreatedDate"] != DBNull.Value ? (DateTime)reader["CreatedDate"] : DateTime.MinValue
                                });
                            }
                        }
                    }).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to get scripts", ex);
            }
            finally { _dbLock.Release(); }
            return scripts;
        }

        public async Task SaveScriptAsync(ScriptInfo script)
        {
            await _dbLock.WaitAsync().ConfigureAwait(false);
            try
            {
                // Check if script with same name exists
                using (var checkCmd = new OleDbCommand("SELECT COUNT(*) FROM Scripts WHERE Name = ?", _connection))
                {
                    checkCmd.Parameters.AddWithValue("?", script.Name ?? string.Empty);
                    var count = await Task.Run(() => (int)checkCmd.ExecuteScalar()).ConfigureAwait(false);
                    if (count > 0)
                    {
                        using (var cmd = new OleDbCommand("UPDATE Scripts SET Description = ?, ScriptContent = ?, Category = ?, ModifiedDate = ? WHERE Name = ?", _connection))
                        {
                            cmd.Parameters.AddWithValue("?", script.Description ?? string.Empty);
                            cmd.Parameters.AddWithValue("?", script.Content ?? string.Empty);
                            cmd.Parameters.AddWithValue("?", script.Category ?? string.Empty);
                            cmd.Parameters.AddWithValue("?", DateTime.Now);
                            cmd.Parameters.AddWithValue("?", script.Name ?? string.Empty);
                            await Task.Run(() => cmd.ExecuteNonQuery()).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        using (var cmd = new OleDbCommand("INSERT INTO Scripts (Name, Description, ScriptContent, Category, CreatedDate, ModifiedDate) VALUES (?, ?, ?, ?, ?, ?)", _connection))
                        {
                            cmd.Parameters.AddWithValue("?", script.Name ?? string.Empty);
                            cmd.Parameters.AddWithValue("?", script.Description ?? string.Empty);
                            cmd.Parameters.AddWithValue("?", script.Content ?? string.Empty);
                            cmd.Parameters.AddWithValue("?", script.Category ?? string.Empty);
                            cmd.Parameters.AddWithValue("?", DateTime.Now);
                            cmd.Parameters.AddWithValue("?", DateTime.Now);
                            await Task.Run(() => cmd.ExecuteNonQuery()).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to save script '{script.Name}'", ex);
            }
            finally { _dbLock.Release(); }
        }

        public async Task DeleteScriptAsync(int scriptId)
        {
            await _dbLock.WaitAsync().ConfigureAwait(false);
            try
            {
                using (var cmd = new OleDbCommand("DELETE FROM Scripts WHERE Id = ?", _connection))
                {
                    cmd.Parameters.AddWithValue("?", scriptId);
                    await Task.Run(() => cmd.ExecuteNonQuery()).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to delete script {scriptId}", ex);
            }
            finally { _dbLock.Release(); }
        }

        // BOOKMARKS
        public async Task<List<BookmarkInfo>> GetAllBookmarksAsync()
        {
            var bookmarks = new List<BookmarkInfo>();
            await _dbLock.WaitAsync().ConfigureAwait(false);
            try
            {
                using (var cmd = new OleDbCommand("SELECT Hostname, Category, Notes FROM Bookmarks ORDER BY Hostname", _connection))
                {
                    await Task.Run(() =>
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                bookmarks.Add(new BookmarkInfo
                                {
                                    Hostname = reader["Hostname"]?.ToString(),
                                    Category = reader["Category"]?.ToString(),
                                    Notes = reader["Notes"]?.ToString()
                                });
                            }
                        }
                    }).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to get bookmarks", ex);
            }
            finally { _dbLock.Release(); }
            return bookmarks;
        }

        public async Task SaveBookmarkAsync(BookmarkInfo bookmark)
        {
            await _dbLock.WaitAsync().ConfigureAwait(false);
            try
            {
                using (var checkCmd = new OleDbCommand("SELECT COUNT(*) FROM Bookmarks WHERE Hostname = ?", _connection))
                {
                    checkCmd.Parameters.AddWithValue("?", bookmark.Hostname ?? string.Empty);
                    var count = await Task.Run(() => (int)checkCmd.ExecuteScalar()).ConfigureAwait(false);
                    if (count > 0)
                    {
                        using (var cmd = new OleDbCommand("UPDATE Bookmarks SET Category = ?, Notes = ? WHERE Hostname = ?", _connection))
                        {
                            cmd.Parameters.AddWithValue("?", bookmark.Category ?? string.Empty);
                            cmd.Parameters.AddWithValue("?", bookmark.Notes ?? string.Empty);
                            cmd.Parameters.AddWithValue("?", bookmark.Hostname ?? string.Empty);
                            await Task.Run(() => cmd.ExecuteNonQuery()).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        using (var cmd = new OleDbCommand("INSERT INTO Bookmarks (Name, Hostname, Category, Notes, CreatedDate) VALUES (?, ?, ?, ?, ?)", _connection))
                        {
                            cmd.Parameters.AddWithValue("?", bookmark.Hostname ?? string.Empty);
                            cmd.Parameters.AddWithValue("?", bookmark.Hostname ?? string.Empty);
                            cmd.Parameters.AddWithValue("?", bookmark.Category ?? string.Empty);
                            cmd.Parameters.AddWithValue("?", bookmark.Notes ?? string.Empty);
                            cmd.Parameters.AddWithValue("?", DateTime.Now);
                            await Task.Run(() => cmd.ExecuteNonQuery()).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to save bookmark for {bookmark.Hostname}", ex);
            }
            finally { _dbLock.Release(); }
        }

        public async Task DeleteBookmarkAsync(string hostname)
        {
            await _dbLock.WaitAsync().ConfigureAwait(false);
            try
            {
                using (var cmd = new OleDbCommand("DELETE FROM Bookmarks WHERE Hostname = ?", _connection))
                {
                    cmd.Parameters.AddWithValue("?", hostname);
                    await Task.Run(() => cmd.ExecuteNonQuery()).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to delete bookmark for {hostname}", ex);
            }
            finally { _dbLock.Release(); }
        }

        // DATABASE OPERATIONS
        public async Task OptimizeDatabaseAsync()
        {
            // Access compact/repair requires the DB to be closed, then re-opened
            // Use JRO (Jet Replication Objects) for compacting
            try
            {
                LogManager.LogInfo("AccessDataProvider.OptimizeDatabaseAsync() - START");
                _connection?.Close();

                var tempPath = _databasePath + ".compact.tmp";
                var connStr = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={_databasePath};";
                var tempConnStr = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={tempPath};";

                await Task.Run(() =>
                {
                    var jro = Activator.CreateInstance(Type.GetTypeFromProgID("JRO.JetEngine"));
                    jro.GetType().InvokeMember("CompactDatabase",
                        System.Reflection.BindingFlags.InvokeMethod, null, jro,
                        new object[] { connStr, tempConnStr });

                    // Replace original with compacted
                    File.Delete(_databasePath);
                    File.Move(tempPath, _databasePath);
                }).ConfigureAwait(false);

                // Re-open connection
                var connectionString = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={_databasePath};Persist Security Info=False;";
                _connection = new OleDbConnection(connectionString);
                await Task.Run(() => _connection.Open());

                LogManager.LogInfo("AccessDataProvider.OptimizeDatabaseAsync() - SUCCESS");
            }
            catch (Exception ex)
            {
                LogManager.LogError("AccessDataProvider.OptimizeDatabaseAsync() - FAILED", ex);
                // Try to re-open connection even on failure
                try
                {
                    var connectionString = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={_databasePath};Persist Security Info=False;";
                    _connection = new OleDbConnection(connectionString);
                    await Task.Run(() => _connection.Open());
                }
                catch (Exception reopenEx)
                {
                    LogManager.LogError("Failed to re-open Access database after optimize failure", reopenEx);
                }
            }
        }

        public async Task<bool> VerifyIntegrityAsync()
        {
            await _dbLock.WaitAsync().ConfigureAwait(false);
            try
            {
                // Verify we can read from each table
                var tables = new[] { "Computers", "ScanHistory", "Scripts", "Bookmarks" };
                foreach (var table in tables)
                {
                    using (var cmd = new OleDbCommand($"SELECT COUNT(*) FROM {table}", _connection))
                    {
                        await Task.Run(() => cmd.ExecuteScalar()).ConfigureAwait(false);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                LogManager.LogError("Access database integrity check failed", ex);
                return false;
            }
            finally { _dbLock.Release(); }
        }

        public async Task<bool> BackupDatabaseAsync(string backupPath)
        {
            try
            {
                if (File.Exists(_databasePath))
                {
                    await Task.Run(() => File.Copy(_databasePath, backupPath, true));
                    LogManager.LogInfo($"Access database backed up to: {backupPath}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to backup Access database to {backupPath}", ex);
                return false;
            }
        }

        public async Task<bool> RestoreDatabaseAsync(string backupPath)
        {
            try
            {
                if (File.Exists(backupPath))
                {
                    _connection?.Close();
                    await Task.Run(() => File.Copy(backupPath, _databasePath, true));
                    LogManager.LogInfo($"Access database restored from: {backupPath}");

                    // Reconnect
                    var connectionString = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={_databasePath};Persist Security Info=False;";
                    _connection = new OleDbConnection(connectionString);
                    await Task.Run(() => _connection.Open());

                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to restore Access database from {backupPath}", ex);
                return false;
            }
        }

        public async Task<DatabaseStats> GetDatabaseStatsAsync()
        {
            await _dbLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var stats = new DatabaseStats();
                // GetTableCountAsync called directly here (under the lock already held)
                stats.TotalComputers = await GetTableCountLockedAsync("Computers").ConfigureAwait(false);
                stats.TotalScans     = await GetTableCountLockedAsync("ScanHistory").ConfigureAwait(false);
                stats.TotalScripts   = await GetTableCountLockedAsync("Scripts").ConfigureAwait(false);
                stats.TotalBookmarks = await GetTableCountLockedAsync("Bookmarks").ConfigureAwait(false);
                if (File.Exists(_databasePath))
                {
                    stats.SizeBytes = new FileInfo(_databasePath).Length;
                }
                return stats;
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to get Access database stats", ex);
                return new DatabaseStats();
            }
            finally
            {
                _dbLock.Release();
            }
        }

        // Called only from within a held _dbLock — do NOT acquire lock here.
        // tableName is validated against a whitelist to prevent SQL injection
        // (OleDb does not support parameterized table names).
        private static readonly HashSet<string> _allowedTables =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "Computers", "ScanHistory", "Scripts", "Bookmarks", "ComputerTags", "Settings" };

        private async Task<int> GetTableCountLockedAsync(string tableName)
        {
            if (string.IsNullOrEmpty(tableName) || !_allowedTables.Contains(tableName))
            {
                LogManager.LogWarning($"AccessDataProvider.GetTableCountLockedAsync - rejected non-whitelisted tableName '{tableName}'");
                return 0;
            }
            try
            {
                // Safe: tableName validated against whitelist above — not user-supplied
                var query = $"SELECT COUNT(*) FROM {tableName}";
                using (var cmd = new OleDbCommand(query, _connection))
                {
                    return await Task.Run(() => (int)cmd.ExecuteScalar()).ConfigureAwait(false);
                }
            }
            catch
            {
                return 0;
            }
        }

        private ComputerInfo MapReaderToComputer(OleDbDataReader reader)
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

        private void AddUpdateParameters(OleDbCommand cmd, ComputerInfo computer)
        {
            // ORDER must match UPDATE SET column list exactly
            cmd.Parameters.AddWithValue("?", computer.OS ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.OSVersion ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.Status ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.Manufacturer ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.Model ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.SerialNumber ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.AssetTag ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.ChassisType ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.IPAddress ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.MACAddress ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.Domain ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.DomainController ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.LastLoggedOnUser ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.RAM_GB);
            cmd.Parameters.AddWithValue("?", computer.CPU ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.DiskSize_GB);
            cmd.Parameters.AddWithValue("?", computer.DiskFree_GB);
            cmd.Parameters.AddWithValue("?", computer.LastSeen);
            cmd.Parameters.AddWithValue("?", (object)computer.LastBootTime ?? DBNull.Value);
            cmd.Parameters.AddWithValue("?", computer.InstallDate != DateTime.MinValue ? (object)computer.InstallDate : DBNull.Value);
            cmd.Parameters.AddWithValue("?", (object)computer.Uptime ?? DBNull.Value);
            cmd.Parameters.AddWithValue("?", computer.BitLockerStatus ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.TPMVersion ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.AntivirusProduct ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.AntivirusStatus ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.FirewallStatus ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.PendingRebootCount);
            cmd.Parameters.AddWithValue("?", computer.LastPatchDate != DateTime.MinValue ? (object)computer.LastPatchDate : DBNull.Value);
            cmd.Parameters.AddWithValue("?", computer.Notes ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.RawDataJson ?? string.Empty);
            cmd.Parameters.AddWithValue("?", DateTime.Now);           // ModifiedDate
            cmd.Parameters.AddWithValue("?", computer.Hostname ?? string.Empty); // WHERE
        }

        private void AddInsertParameters(OleDbCommand cmd, ComputerInfo computer)
        {
            // ORDER must match INSERT column list exactly
            cmd.Parameters.AddWithValue("?", computer.Hostname ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.OS ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.OSVersion ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.Status ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.Manufacturer ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.Model ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.SerialNumber ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.AssetTag ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.ChassisType ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.IPAddress ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.MACAddress ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.Domain ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.DomainController ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.LastLoggedOnUser ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.RAM_GB);
            cmd.Parameters.AddWithValue("?", computer.CPU ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.DiskSize_GB);
            cmd.Parameters.AddWithValue("?", computer.DiskFree_GB);
            cmd.Parameters.AddWithValue("?", computer.LastSeen);
            cmd.Parameters.AddWithValue("?", (object)computer.LastBootTime ?? DBNull.Value);
            cmd.Parameters.AddWithValue("?", computer.InstallDate != DateTime.MinValue ? (object)computer.InstallDate : DBNull.Value);
            cmd.Parameters.AddWithValue("?", (object)computer.Uptime ?? DBNull.Value);
            cmd.Parameters.AddWithValue("?", computer.BitLockerStatus ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.TPMVersion ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.AntivirusProduct ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.AntivirusStatus ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.FirewallStatus ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.PendingRebootCount);
            cmd.Parameters.AddWithValue("?", computer.LastPatchDate != DateTime.MinValue ? (object)computer.LastPatchDate : DBNull.Value);
            cmd.Parameters.AddWithValue("?", computer.Notes ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.RawDataJson ?? string.Empty);
            cmd.Parameters.AddWithValue("?", DateTime.Now);           // CreatedDate
            cmd.Parameters.AddWithValue("?", DateTime.Now);           // ModifiedDate
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _connection?.Close();
                _connection?.Dispose();
                _dbLock?.Dispose();
                _disposed = true;
                LogManager.LogInfo("Access provider disposed");
            }
        }
    }
}
