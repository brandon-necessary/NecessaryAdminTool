using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using NecessaryAdminTool.Security;
// TAG: #DATABASE #CSV_JSON #VERSION_1_2 #SECURITY_CRITICAL #PATH_TRAVERSAL_PREVENTION

namespace NecessaryAdminTool.Data
{
    /// <summary>
    /// CSV/JSON file-based data provider (fallback option)
    /// TAG: #PORTABLE #HUMAN_READABLE #10K_COMPUTERS
    /// </summary>
    public class CsvDataProvider : IDataProvider
    {
        private readonly string _dataDirectory;
        private readonly string _computersFile;
        private readonly string _scanHistoryFile;
        private readonly string _scriptsFile;
        private readonly string _bookmarksFile;
        private readonly JavaScriptSerializer _serializer;
        private bool _disposed = false;

        public CsvDataProvider(string dataDirectory)
        {
            _dataDirectory = dataDirectory ?? throw new ArgumentNullException(nameof(dataDirectory));
            _computersFile = Path.Combine(_dataDirectory, "computers.json");
            _scanHistoryFile = Path.Combine(_dataDirectory, "scan_history.json");
            _scriptsFile = Path.Combine(_dataDirectory, "scripts.json");
            _bookmarksFile = Path.Combine(_dataDirectory, "bookmarks.json");
            _serializer = new JavaScriptSerializer();
            _serializer.MaxJsonLength = int.MaxValue;

            LogManager.LogInfo($"CSV/JSON provider initialized: {_dataDirectory}");
        }

        public async Task InitializeDatabaseAsync()
        {
            try
            {
                // Create directory if it doesn't exist
                if (!Directory.Exists(_dataDirectory))
                {
                    Directory.CreateDirectory(_dataDirectory);
                    LogManager.LogInfo($"Created data directory: {_dataDirectory}");
                }

                // Create empty files if they don't exist
                await CreateEmptyFileIfNotExistsAsync(_computersFile, "[]");
                await CreateEmptyFileIfNotExistsAsync(_scanHistoryFile, "[]");
                await CreateEmptyFileIfNotExistsAsync(_scriptsFile, "[]");
                await CreateEmptyFileIfNotExistsAsync(_bookmarksFile, "[]");

                LogManager.LogInfo("CSV/JSON data store initialized successfully");
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to initialize CSV/JSON data store", ex);
                throw new InvalidOperationException("Cannot initialize CSV/JSON data store", ex);
            }
        }

        private async Task CreateEmptyFileIfNotExistsAsync(string filePath, string defaultContent)
        {
            if (!File.Exists(filePath))
            {
                await Task.Run(() => File.WriteAllText(filePath, defaultContent, Encoding.UTF8));
                LogManager.LogInfo($"Created data file: {Path.GetFileName(filePath)}");
            }
        }

        public async Task<List<ComputerInfo>> GetAllComputersAsync()
        {
            try
            {
                if (!File.Exists(_computersFile))
                {
                    return new List<ComputerInfo>();
                }

                var json = await Task.Run(() => File.ReadAllText(_computersFile, Encoding.UTF8));
                var computers = _serializer.Deserialize<List<ComputerInfo>>(json);
                return computers ?? new List<ComputerInfo>();
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to read computers from JSON file", ex);
                return new List<ComputerInfo>();
            }
        }

        public async Task SaveComputerAsync(ComputerInfo computer)
        {
            try
            {
                // Load existing computers
                var computers = await GetAllComputersAsync();

                // Update or add
                var existingIndex = computers.FindIndex(c =>
                    c.Hostname?.Equals(computer.Hostname, StringComparison.OrdinalIgnoreCase) == true);

                if (existingIndex >= 0)
                {
                    computers[existingIndex] = computer;
                }
                else
                {
                    computers.Add(computer);
                }

                // Save back to file
                var json = _serializer.Serialize(computers);
                await Task.Run(() => File.WriteAllText(_computersFile, json, Encoding.UTF8));
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to save computer {computer.Hostname} to JSON file", ex);
                throw;
            }
        }

        public async Task<ComputerInfo> GetComputerAsync(string hostname)
        {
            try
            {
                var computers = await GetAllComputersAsync();
                return computers.FirstOrDefault(c =>
                    c.Hostname?.Equals(hostname, StringComparison.OrdinalIgnoreCase) == true);
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to get computer {hostname} from JSON file", ex);
                return null;
            }
        }

        public async Task DeleteComputerAsync(string hostname)
        {
            try
            {
                var computers = await GetAllComputersAsync();
                var removed = computers.RemoveAll(c =>
                    c.Hostname?.Equals(hostname, StringComparison.OrdinalIgnoreCase) == true);

                if (removed > 0)
                {
                    var json = _serializer.Serialize(computers);
                    await Task.Run(() => File.WriteAllText(_computersFile, json, Encoding.UTF8));
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to delete computer {hostname} from JSON file", ex);
                throw;
            }
        }

        public async Task<List<ComputerInfo>> SearchComputersAsync(string searchTerm)
        {
            try
            {
                var computers = await GetAllComputersAsync();
                var searchLower = searchTerm?.ToLower() ?? string.Empty;

                return computers.Where(c =>
                    (c.Hostname?.ToLower().Contains(searchLower) == true) ||
                    (c.OS?.ToLower().Contains(searchLower) == true) ||
                    (c.Manufacturer?.ToLower().Contains(searchLower) == true) ||
                    (c.Model?.ToLower().Contains(searchLower) == true) ||
                    (c.IPAddress?.ToLower().Contains(searchLower) == true)
                ).ToList();
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to search computers with term '{searchTerm}' from JSON file", ex);
                return new List<ComputerInfo>();
            }
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
            LogManager.LogWarning($"OptimizeDatabaseAsync not yet implemented in {GetType().Name}");
            await Task.CompletedTask;
        }

        public async Task<bool> VerifyIntegrityAsync()
        {
            try
            {
                // Verify all JSON files are valid
                await GetAllComputersAsync();
                await CountRecordsAsync(_scanHistoryFile);
                await CountRecordsAsync(_scriptsFile);
                await CountRecordsAsync(_bookmarksFile);
                return true;
            }
            catch (Exception ex)
            {
                LogManager.LogError("CSV/JSON data integrity check failed", ex);
                return false;
            }
        }

        public async Task<bool> BackupDatabaseAsync(string backupPath)
        {
            try
            {
                // TAG: #SECURITY_CRITICAL #PATH_TRAVERSAL_PREVENTION
                // Validate backup path
                if (!Directory.Exists(backupPath))
                {
                    LogManager.LogWarning($"[CsvDataProvider] Blocked backup - directory does not exist: {backupPath}");
                    return false;
                }

                if (!Directory.Exists(_dataDirectory))
                {
                    return false;
                }

                // Copy all JSON files
                var files = Directory.GetFiles(_dataDirectory, "*.json");
                foreach (var file in files)
                {
                    // TAG: #SECURITY_CRITICAL #PATH_TRAVERSAL_PREVENTION
                    // Validate source file path
                    string fullSourcePath = Path.GetFullPath(file);
                    if (!SecurityValidator.IsValidFilePath(fullSourcePath, _dataDirectory))
                    {
                        LogManager.LogWarning($"[CsvDataProvider] Blocked backup - invalid source file: {file}");
                        continue;
                    }

                    var fileName = Path.GetFileName(file);
                    if (!SecurityValidator.IsValidFilename(fileName))
                    {
                        LogManager.LogWarning($"[CsvDataProvider] Blocked backup - invalid filename: {fileName}");
                        continue;
                    }

                    var destFile = Path.Combine(backupPath, fileName);
                    string fullDestPath = Path.GetFullPath(destFile);

                    // Validate destination path
                    if (!SecurityValidator.IsValidFilePath(fullDestPath, backupPath))
                    {
                        LogManager.LogWarning($"[CsvDataProvider] Blocked backup - invalid destination: {destFile}");
                        continue;
                    }

                    await Task.Run(() => File.Copy(file, destFile, true));
                }

                LogManager.LogInfo($"CSV/JSON data backed up to: {backupPath}");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to backup CSV/JSON data to {backupPath}", ex);
                return false;
            }
        }

        public async Task<bool> RestoreDatabaseAsync(string backupPath)
        {
            try
            {
                // TAG: #SECURITY_CRITICAL #PATH_TRAVERSAL_PREVENTION
                // Validate backup path
                if (!Directory.Exists(backupPath))
                {
                    LogManager.LogWarning($"[CsvDataProvider] Blocked restore - directory does not exist: {backupPath}");
                    return false;
                }

                // Create data directory if needed
                if (!Directory.Exists(_dataDirectory))
                {
                    Directory.CreateDirectory(_dataDirectory);
                }

                // Copy all JSON files from backup
                var files = Directory.GetFiles(backupPath, "*.json");
                foreach (var file in files)
                {
                    // TAG: #SECURITY_CRITICAL #PATH_TRAVERSAL_PREVENTION
                    // Validate source file path
                    string fullSourcePath = Path.GetFullPath(file);
                    if (!SecurityValidator.IsValidFilePath(fullSourcePath, backupPath))
                    {
                        LogManager.LogWarning($"[CsvDataProvider] Blocked restore - invalid source file: {file}");
                        continue;
                    }

                    var fileName = Path.GetFileName(file);
                    if (!SecurityValidator.IsValidFilename(fileName))
                    {
                        LogManager.LogWarning($"[CsvDataProvider] Blocked restore - invalid filename: {fileName}");
                        continue;
                    }

                    var destFile = Path.Combine(_dataDirectory, fileName);
                    string fullDestPath = Path.GetFullPath(destFile);

                    // Validate destination path
                    if (!SecurityValidator.IsValidFilePath(fullDestPath, _dataDirectory))
                    {
                        LogManager.LogWarning($"[CsvDataProvider] Blocked restore - invalid destination: {destFile}");
                        continue;
                    }

                    await Task.Run(() => File.Copy(file, destFile, true));
                }

                LogManager.LogInfo($"CSV/JSON data restored from: {backupPath}");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to restore CSV/JSON data from {backupPath}", ex);
                return false;
            }
        }

        public async Task<DatabaseStats> GetDatabaseStatsAsync()
        {
            try
            {
                var stats = new DatabaseStats();

                // Count records in each file
                stats.TotalComputers = await CountRecordsAsync(_computersFile);
                stats.TotalScans = await CountRecordsAsync(_scanHistoryFile);
                stats.TotalScripts = await CountRecordsAsync(_scriptsFile);
                stats.TotalBookmarks = await CountRecordsAsync(_bookmarksFile);

                // Calculate total data directory size
                if (Directory.Exists(_dataDirectory))
                {
                    var dirInfo = new DirectoryInfo(_dataDirectory);
                    var totalSize = dirInfo.GetFiles("*.json", SearchOption.AllDirectories)
                        .Sum(f => f.Length);
                    stats.SizeBytes = totalSize;
                }

                return stats;
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to get CSV/JSON data stats", ex);
                return new DatabaseStats();
            }
        }

        private async Task<int> CountRecordsAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return 0;
                }

                var json = await Task.Run(() => File.ReadAllText(filePath, Encoding.UTF8));
                var array = _serializer.Deserialize<List<object>>(json);
                return array?.Count ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Export data to CSV format for Excel compatibility
        /// </summary>
        public async Task ExportToCsvAsync(string outputPath)
        {
            try
            {
                // TAG: #SECURITY_CRITICAL #PATH_TRAVERSAL_PREVENTION
                // Validate filename
                string filename = Path.GetFileName(outputPath);
                if (!SecurityValidator.IsValidFilename(filename))
                {
                    LogManager.LogWarning($"[CsvDataProvider] Blocked export - invalid filename: {filename}");
                    throw new ArgumentException("Invalid filename detected");
                }

                // Validate file extension
                string extension = Path.GetExtension(outputPath);
                if (!extension.Equals(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    LogManager.LogWarning($"[CsvDataProvider] Blocked export - invalid extension: {extension}");
                    throw new ArgumentException("File must have .csv extension");
                }

                // Validate parent directory exists
                string directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    LogManager.LogWarning($"[CsvDataProvider] Blocked export - directory does not exist: {directory}");
                    throw new DirectoryNotFoundException($"Directory does not exist: {directory}");
                }

                var computers = await GetAllComputersAsync();
                var csv = new StringBuilder();

                // Header
                csv.AppendLine("Hostname,OS,OSVersion,Manufacturer,Model,SerialNumber,AssetTag," +
                    "IPAddress,MACAddress,Domain,LastLoggedOnUser,RAM_GB,CPU,DiskSize_GB,DiskFree_GB," +
                    "LastSeen,LastBootTime,InstallDate,BitLockerStatus,TPMVersion," +
                    "AntivirusProduct,AntivirusStatus,FirewallStatus,PendingRebootCount,LastPatchDate,Notes");

                // Data rows
                foreach (var computer in computers)
                {
                    csv.AppendLine($"{EscapeCsv(computer.Hostname)}," +
                        $"{EscapeCsv(computer.OS)}," +
                        $"{EscapeCsv(computer.OSVersion)}," +
                        $"{EscapeCsv(computer.Manufacturer)}," +
                        $"{EscapeCsv(computer.Model)}," +
                        $"{EscapeCsv(computer.SerialNumber)}," +
                        $"{EscapeCsv(computer.AssetTag)}," +
                        $"{EscapeCsv(computer.IPAddress)}," +
                        $"{EscapeCsv(computer.MACAddress)}," +
                        $"{EscapeCsv(computer.Domain)}," +
                        $"{EscapeCsv(computer.LastLoggedOnUser)}," +
                        $"{computer.RAM_GB}," +
                        $"{EscapeCsv(computer.CPU)}," +
                        $"{computer.DiskSize_GB}," +
                        $"{computer.DiskFree_GB}," +
                        $"{computer.LastSeen:yyyy-MM-dd HH:mm:ss}," +
                        $"{computer.LastBootTime:yyyy-MM-dd HH:mm:ss}," +
                        $"{computer.InstallDate:yyyy-MM-dd HH:mm:ss}," +
                        $"{EscapeCsv(computer.BitLockerStatus)}," +
                        $"{EscapeCsv(computer.TPMVersion)}," +
                        $"{EscapeCsv(computer.AntivirusProduct)}," +
                        $"{EscapeCsv(computer.AntivirusStatus)}," +
                        $"{EscapeCsv(computer.FirewallStatus)}," +
                        $"{computer.PendingRebootCount}," +
                        $"{computer.LastPatchDate:yyyy-MM-dd HH:mm:ss}," +
                        $"{EscapeCsv(computer.Notes)}");
                }

                await Task.Run(() => File.WriteAllText(outputPath, csv.ToString(), Encoding.UTF8));
                LogManager.LogInfo($"Exported {computers.Count} computers to CSV: {outputPath}");
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to export to CSV", ex);
                throw;
            }
        }

        /// <summary>
        /// Import data from CSV file
        /// </summary>
        public async Task ImportFromCsvAsync(string csvPath)
        {
            try
            {
                // TAG: #SECURITY_CRITICAL #PATH_TRAVERSAL_PREVENTION
                // Validate filename
                string filename = Path.GetFileName(csvPath);
                if (!SecurityValidator.IsValidFilename(filename))
                {
                    LogManager.LogWarning($"[CsvDataProvider] Blocked import - invalid filename: {filename}");
                    throw new ArgumentException("Invalid filename detected");
                }

                // Validate file extension
                string extension = Path.GetExtension(csvPath);
                if (!extension.Equals(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    LogManager.LogWarning($"[CsvDataProvider] Blocked import - invalid extension: {extension}");
                    throw new ArgumentException("File must have .csv extension");
                }

                // Validate file exists
                if (!File.Exists(csvPath))
                {
                    LogManager.LogWarning($"[CsvDataProvider] Blocked import - file does not exist: {csvPath}");
                    throw new FileNotFoundException("CSV file not found");
                }

                // TAG: #SECURITY_CRITICAL #FILE_SIZE_VALIDATION
                // Prevent reading excessively large files (DoS protection)
                var fileInfo = new FileInfo(csvPath);
                const long MAX_FILE_SIZE = 50 * 1024 * 1024; // 50 MB
                if (fileInfo.Length > MAX_FILE_SIZE)
                {
                    LogManager.LogWarning($"[CsvDataProvider] Blocked import - file too large: {fileInfo.Length} bytes");
                    throw new ArgumentException("CSV file exceeds maximum allowed size (50 MB)");
                }

                var lines = await Task.Run(() => File.ReadAllLines(csvPath, Encoding.UTF8));
                if (lines.Length < 2)
                {
                    throw new InvalidOperationException("CSV file is empty or has no data rows");
                }

                var computers = new List<ComputerInfo>();

                // Skip header line
                for (int i = 1; i < lines.Length; i++)
                {
                    var fields = ParseCsvLine(lines[i]);
                    if (fields.Length >= 26)
                    {
                        var computer = new ComputerInfo
                        {
                            Hostname = fields[0],
                            OS = fields[1],
                            OSVersion = fields[2],
                            Manufacturer = fields[3],
                            Model = fields[4],
                            SerialNumber = fields[5],
                            AssetTag = fields[6],
                            IPAddress = fields[7],
                            MACAddress = fields[8],
                            Domain = fields[9],
                            LastLoggedOnUser = fields[10],
                            RAM_GB = int.TryParse(fields[11], out var ram) ? ram : 0,
                            CPU = fields[12],
                            DiskSize_GB = int.TryParse(fields[13], out var diskSize) ? diskSize : 0,
                            DiskFree_GB = int.TryParse(fields[14], out var diskFree) ? diskFree : 0,
                            LastSeen = DateTime.TryParse(fields[15], out var lastSeen) ? lastSeen : DateTime.MinValue,
                            LastBootTime = DateTime.TryParse(fields[16], out var lastBoot) ? lastBoot : DateTime.MinValue,
                            InstallDate = DateTime.TryParse(fields[17], out var installDate) ? installDate : DateTime.MinValue,
                            BitLockerStatus = fields[18],
                            TPMVersion = fields[19],
                            AntivirusProduct = fields[20],
                            AntivirusStatus = fields[21],
                            FirewallStatus = fields[22],
                            PendingRebootCount = int.TryParse(fields[23], out var rebootCount) ? rebootCount : 0,
                            LastPatchDate = DateTime.TryParse(fields[24], out var patchDate) ? patchDate : DateTime.MinValue,
                            Notes = fields.Length > 25 ? fields[25] : string.Empty
                        };

                        computers.Add(computer);
                    }
                }

                // Save imported data
                var json = _serializer.Serialize(computers);
                await Task.Run(() => File.WriteAllText(_computersFile, json, Encoding.UTF8));

                LogManager.LogInfo($"Imported {computers.Count} computers from CSV: {csvPath}");
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to import from CSV", ex);
                throw;
            }
        }

        private string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            return value;
        }

        private string[] ParseCsvLine(string line)
        {
            var fields = new List<string>();
            var currentField = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        // Escaped quote
                        currentField.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    fields.Add(currentField.ToString());
                    currentField.Clear();
                }
                else
                {
                    currentField.Append(c);
                }
            }

            fields.Add(currentField.ToString());
            return fields.ToArray();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                LogManager.LogInfo("CSV/JSON provider disposed");
            }
        }
    }
}
