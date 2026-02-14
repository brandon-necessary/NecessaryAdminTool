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
                            Manufacturer TEXT(100),
                            Model TEXT(100),
                            SerialNumber TEXT(100),
                            AssetTag TEXT(100),
                            IPAddress TEXT(50),
                            MACAddress TEXT(50),
                            Domain TEXT(100),
                            LastLoggedOnUser TEXT(100),
                            RAM_GB INTEGER,
                            CPU TEXT(200),
                            DiskSize_GB INTEGER,
                            DiskFree_GB INTEGER,
                            LastSeen DATETIME,
                            LastBootTime DATETIME,
                            InstallDate DATETIME,
                            BitLockerStatus TEXT(50),
                            TPMVersion TEXT(50),
                            AntivirusProduct TEXT(100),
                            AntivirusStatus TEXT(50),
                            FirewallStatus TEXT(50),
                            PendingRebootCount INTEGER,
                            LastPatchDate DATETIME,
                            Notes MEMO,
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
            await Task.Run(() =>
            {
                var schema = _connection.GetOleDbSchemaTable(OleDbSchemaGuid.Tables,
                    new object[] { null, null, null, "TABLE" });
                foreach (System.Data.DataRow row in schema.Rows)
                {
                    tables.Add(row["TABLE_NAME"].ToString());
                }
            });
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
                    });
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to retrieve computers from Access database", ex);
            }

            return computers;
        }

        public async Task SaveComputerAsync(ComputerInfo computer)
        {
            try
            {
                // Check if computer exists
                var existsQuery = "SELECT COUNT(*) FROM Computers WHERE Hostname = ?";
                bool exists = false;

                using (var cmd = new OleDbCommand(existsQuery, _connection))
                {
                    cmd.Parameters.AddWithValue("?", computer.Hostname);
                    var count = await Task.Run(() => (int)cmd.ExecuteScalar());
                    exists = count > 0;
                }

                string query;
                if (exists)
                {
                    // Update existing
                    query = @"UPDATE Computers SET
                        OS=?, OSVersion=?, Manufacturer=?, Model=?, SerialNumber=?, AssetTag=?,
                        IPAddress=?, MACAddress=?, Domain=?, LastLoggedOnUser=?, RAM_GB=?, CPU=?,
                        DiskSize_GB=?, DiskFree_GB=?, LastSeen=?, LastBootTime=?, InstallDate=?,
                        BitLockerStatus=?, TPMVersion=?, AntivirusProduct=?, AntivirusStatus=?,
                        FirewallStatus=?, PendingRebootCount=?, LastPatchDate=?, Notes=?, ModifiedDate=?
                        WHERE Hostname=?";
                }
                else
                {
                    // Insert new
                    query = @"INSERT INTO Computers (
                        Hostname, OS, OSVersion, Manufacturer, Model, SerialNumber, AssetTag,
                        IPAddress, MACAddress, Domain, LastLoggedOnUser, RAM_GB, CPU,
                        DiskSize_GB, DiskFree_GB, LastSeen, LastBootTime, InstallDate,
                        BitLockerStatus, TPMVersion, AntivirusProduct, AntivirusStatus,
                        FirewallStatus, PendingRebootCount, LastPatchDate, Notes, CreatedDate, ModifiedDate
                    ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";
                }

                using (var cmd = new OleDbCommand(query, _connection))
                {
                    if (exists)
                    {
                        AddUpdateParameters(cmd, computer);
                    }
                    else
                    {
                        AddInsertParameters(cmd, computer);
                    }

                    await Task.Run(() => cmd.ExecuteNonQuery());
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to save computer {computer.Hostname} to Access database", ex);
                throw;
            }
        }

        public async Task<DatabaseStats> GetDatabaseStatsAsync()
        {
            try
            {
                var stats = new DatabaseStats { DatabaseType = "Microsoft Access" };

                // Get counts from each table
                stats.TotalComputers = await GetTableCountAsync("Computers");
                stats.TotalScans = await GetTableCountAsync("ScanHistory");
                stats.TotalScripts = await GetTableCountAsync("Scripts");
                stats.TotalBookmarks = await GetTableCountAsync("Bookmarks");

                // Get database file size
                if (File.Exists(_databasePath))
                {
                    var fileInfo = new FileInfo(_databasePath);
                    stats.DatabaseSizeMB = fileInfo.Length / (1024 * 1024);
                }

                return stats;
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to get Access database stats", ex);
                return new DatabaseStats { DatabaseType = "Microsoft Access" };
            }
        }

        private async Task<int> GetTableCountAsync(string tableName)
        {
            try
            {
                var query = $"SELECT COUNT(*) FROM {tableName}";
                using (var cmd = new OleDbCommand(query, _connection))
                {
                    return await Task.Run(() => (int)cmd.ExecuteScalar());
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
            cmd.Parameters.AddWithValue("?", computer.OS ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.OSVersion ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.Manufacturer ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.Model ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.SerialNumber ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.AssetTag ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.IPAddress ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.MACAddress ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.Domain ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.LastLoggedOnUser ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.RAM_GB);
            cmd.Parameters.AddWithValue("?", computer.CPU ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.DiskSize_GB);
            cmd.Parameters.AddWithValue("?", computer.DiskFree_GB);
            cmd.Parameters.AddWithValue("?", computer.LastSeen);
            cmd.Parameters.AddWithValue("?", computer.LastBootTime);
            cmd.Parameters.AddWithValue("?", computer.InstallDate);
            cmd.Parameters.AddWithValue("?", computer.BitLockerStatus ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.TPMVersion ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.AntivirusProduct ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.AntivirusStatus ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.FirewallStatus ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.PendingRebootCount);
            cmd.Parameters.AddWithValue("?", computer.LastPatchDate);
            cmd.Parameters.AddWithValue("?", computer.Notes ?? string.Empty);
            cmd.Parameters.AddWithValue("?", DateTime.Now);
            cmd.Parameters.AddWithValue("?", computer.Hostname ?? string.Empty);
        }

        private void AddInsertParameters(OleDbCommand cmd, ComputerInfo computer)
        {
            cmd.Parameters.AddWithValue("?", computer.Hostname ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.OS ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.OSVersion ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.Manufacturer ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.Model ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.SerialNumber ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.AssetTag ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.IPAddress ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.MACAddress ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.Domain ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.LastLoggedOnUser ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.RAM_GB);
            cmd.Parameters.AddWithValue("?", computer.CPU ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.DiskSize_GB);
            cmd.Parameters.AddWithValue("?", computer.DiskFree_GB);
            cmd.Parameters.AddWithValue("?", computer.LastSeen);
            cmd.Parameters.AddWithValue("?", computer.LastBootTime);
            cmd.Parameters.AddWithValue("?", computer.InstallDate);
            cmd.Parameters.AddWithValue("?", computer.BitLockerStatus ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.TPMVersion ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.AntivirusProduct ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.AntivirusStatus ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.FirewallStatus ?? string.Empty);
            cmd.Parameters.AddWithValue("?", computer.PendingRebootCount);
            cmd.Parameters.AddWithValue("?", computer.LastPatchDate);
            cmd.Parameters.AddWithValue("?", computer.Notes ?? string.Empty);
            cmd.Parameters.AddWithValue("?", DateTime.Now);
            cmd.Parameters.AddWithValue("?", DateTime.Now);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _connection?.Close();
                _connection?.Dispose();
                _disposed = true;
                LogManager.LogInfo("Access provider disposed");
            }
        }
    }
}
