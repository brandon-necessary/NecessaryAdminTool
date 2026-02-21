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
        private readonly string _tagsFile;
        private readonly string _settingsFile;
        private readonly JavaScriptSerializer _serializer;
        // Serialize write operations to prevent concurrent read-modify-write corruption
        private readonly System.Threading.SemaphoreSlim _fileLock = new System.Threading.SemaphoreSlim(1, 1);
        private bool _disposed = false;

        public CsvDataProvider(string dataDirectory)
        {
            _dataDirectory = dataDirectory ?? throw new ArgumentNullException(nameof(dataDirectory));
            _computersFile = Path.Combine(_dataDirectory, "computers.json");
            _scanHistoryFile = Path.Combine(_dataDirectory, "scan_history.json");
            _scriptsFile = Path.Combine(_dataDirectory, "scripts.json");
            _bookmarksFile = Path.Combine(_dataDirectory, "bookmarks.json");
            _tagsFile = Path.Combine(_dataDirectory, "tags.json");
            _settingsFile = Path.Combine(_dataDirectory, "settings.json");
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
                await CreateEmptyFileIfNotExistsAsync(_tagsFile, "{}");
                await CreateEmptyFileIfNotExistsAsync(_settingsFile, "{}");

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
                await Task.Run(() => WriteFileSafe(filePath, defaultContent));
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

                var json = await Task.Run(() => ReadFileSafe(_computersFile));
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
            await _fileLock.WaitAsync().ConfigureAwait(false);
            try
            {
                // Read current data inside the lock to prevent lost-write races
                var computers = File.Exists(_computersFile)
                    ? _serializer.Deserialize<List<ComputerInfo>>(
                        await Task.Run(() => ReadFileSafe(_computersFile)).ConfigureAwait(false))
                      ?? new List<ComputerInfo>()
                    : new List<ComputerInfo>();

                var existingIndex = computers.FindIndex(c =>
                    c.Hostname?.Equals(computer.Hostname, StringComparison.OrdinalIgnoreCase) == true);

                if (existingIndex >= 0) computers[existingIndex] = computer;
                else                   computers.Add(computer);

                var json = _serializer.Serialize(computers);
                await Task.Run(() => WriteFileSafe(_computersFile, json)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to save computer {computer.Hostname} to JSON file", ex);
                throw;
            }
            finally
            {
                _fileLock.Release();
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
            await _fileLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var computers = File.Exists(_computersFile)
                    ? _serializer.Deserialize<List<ComputerInfo>>(
                        await Task.Run(() => ReadFileSafe(_computersFile)).ConfigureAwait(false))
                      ?? new List<ComputerInfo>()
                    : new List<ComputerInfo>();

                var removed = computers.RemoveAll(c =>
                    c.Hostname?.Equals(hostname, StringComparison.OrdinalIgnoreCase) == true);

                if (removed > 0)
                {
                    var json = _serializer.Serialize(computers);
                    await Task.Run(() => WriteFileSafe(_computersFile, json)).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to delete computer {hostname} from JSON file", ex);
                throw;
            }
            finally
            {
                _fileLock.Release();
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

        // TAG MANAGEMENT — uses tags.json: Dictionary<hostname, List<tagName>>
        public async Task<List<string>> GetComputerTagsAsync(string hostname)
        {
            try
            {
                var allTags = await ReadTagsFileAsync().ConfigureAwait(false);
                if (allTags.ContainsKey(hostname))
                    return allTags[hostname];
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to get tags for {hostname}", ex);
            }
            return new List<string>();
        }

        public async Task AddTagAsync(string hostname, string tagName)
        {
            await _fileLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var allTags = await ReadTagsFileAsync().ConfigureAwait(false);
                if (!allTags.ContainsKey(hostname))
                    allTags[hostname] = new List<string>();
                if (!allTags[hostname].Contains(tagName))
                    allTags[hostname].Add(tagName);
                await Task.Run(() => WriteFileSafe(_tagsFile, _serializer.Serialize(allTags))).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to add tag '{tagName}' to {hostname}", ex);
            }
            finally { _fileLock.Release(); }
        }

        public async Task RemoveTagAsync(string hostname, string tagName)
        {
            await _fileLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var allTags = await ReadTagsFileAsync().ConfigureAwait(false);
                if (allTags.ContainsKey(hostname))
                {
                    allTags[hostname].Remove(tagName);
                    if (allTags[hostname].Count == 0)
                        allTags.Remove(hostname);
                    await Task.Run(() => WriteFileSafe(_tagsFile, _serializer.Serialize(allTags))).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to remove tag '{tagName}' from {hostname}", ex);
            }
            finally { _fileLock.Release(); }
        }

        public async Task<List<string>> GetAllTagsAsync()
        {
            try
            {
                var allTags = await ReadTagsFileAsync().ConfigureAwait(false);
                var distinct = new HashSet<string>();
                foreach (var tagList in allTags.Values)
                    foreach (var tag in tagList)
                        distinct.Add(tag);
                return distinct.OrderBy(t => t).ToList();
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to get all tags", ex);
                return new List<string>();
            }
        }

        private async Task<Dictionary<string, List<string>>> ReadTagsFileAsync()
        {
            if (!File.Exists(_tagsFile)) return new Dictionary<string, List<string>>();
            var json = await Task.Run(() => ReadFileSafe(_tagsFile)).ConfigureAwait(false);
            return _serializer.Deserialize<Dictionary<string, List<string>>>(json)
                   ?? new Dictionary<string, List<string>>();
        }

        // SCAN HISTORY — uses scan_history.json (already exists)
        public async Task<ScanHistory> GetLastScanAsync()
        {
            try
            {
                var history = await GetScanHistoryListAsync().ConfigureAwait(false);
                return history.Count > 0 ? history[0] : null;
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to get last scan", ex);
                return null;
            }
        }

        public async Task SaveScanHistoryAsync(ScanHistory scan)
        {
            await _fileLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var history = await GetScanHistoryListAsync().ConfigureAwait(false);
                // Auto-assign next ID
                scan.ScanId = history.Count > 0 ? history.Max(h => h.ScanId) + 1 : 1;
                history.Insert(0, scan);
                var json = _serializer.Serialize(history);
                await Task.Run(() => WriteFileSafe(_scanHistoryFile, json)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to save scan history", ex);
            }
            finally { _fileLock.Release(); }
        }

        public async Task<List<ScanHistory>> GetScanHistoryAsync(int limit = 10)
        {
            try
            {
                var history = await GetScanHistoryListAsync().ConfigureAwait(false);
                return history.Take(limit).ToList();
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to get scan history", ex);
                return new List<ScanHistory>();
            }
        }

        private async Task<List<ScanHistory>> GetScanHistoryListAsync()
        {
            if (!File.Exists(_scanHistoryFile)) return new List<ScanHistory>();
            var json = await Task.Run(() => ReadFileSafe(_scanHistoryFile)).ConfigureAwait(false);
            return _serializer.Deserialize<List<ScanHistory>>(json) ?? new List<ScanHistory>();
        }

        // SETTINGS — uses settings.json: Dictionary<key, value>
        public async Task<string> GetSettingAsync(string key, string defaultValue = null)
        {
            try
            {
                var settings = await ReadSettingsFileAsync().ConfigureAwait(false);
                if (settings.ContainsKey(key))
                    return settings[key];
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to get setting '{key}'", ex);
            }
            return defaultValue;
        }

        public async Task SaveSettingAsync(string key, string value)
        {
            await _fileLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var settings = await ReadSettingsFileAsync().ConfigureAwait(false);
                settings[key] = value;
                await Task.Run(() => WriteFileSafe(_settingsFile, _serializer.Serialize(settings))).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to save setting '{key}'", ex);
            }
            finally { _fileLock.Release(); }
        }

        private async Task<Dictionary<string, string>> ReadSettingsFileAsync()
        {
            if (!File.Exists(_settingsFile)) return new Dictionary<string, string>();
            var json = await Task.Run(() => ReadFileSafe(_settingsFile)).ConfigureAwait(false);
            return _serializer.Deserialize<Dictionary<string, string>>(json)
                   ?? new Dictionary<string, string>();
        }

        // SCRIPTS — uses scripts.json (already exists)
        public async Task<List<ScriptInfo>> GetAllScriptsAsync()
        {
            try
            {
                if (!File.Exists(_scriptsFile)) return new List<ScriptInfo>();
                var json = await Task.Run(() => ReadFileSafe(_scriptsFile)).ConfigureAwait(false);
                return _serializer.Deserialize<List<ScriptInfo>>(json) ?? new List<ScriptInfo>();
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to get scripts", ex);
                return new List<ScriptInfo>();
            }
        }

        public async Task SaveScriptAsync(ScriptInfo script)
        {
            await _fileLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var scripts = File.Exists(_scriptsFile)
                    ? _serializer.Deserialize<List<ScriptInfo>>(await Task.Run(() => ReadFileSafe(_scriptsFile)).ConfigureAwait(false))
                      ?? new List<ScriptInfo>()
                    : new List<ScriptInfo>();

                var existingIndex = scripts.FindIndex(s =>
                    s.Name?.Equals(script.Name, StringComparison.OrdinalIgnoreCase) == true);
                if (existingIndex >= 0) scripts[existingIndex] = script;
                else
                {
                    script.ScriptId = scripts.Count > 0 ? scripts.Max(s => s.ScriptId) + 1 : 1;
                    if (script.CreatedAt == DateTime.MinValue) script.CreatedAt = DateTime.Now;
                    scripts.Add(script);
                }
                await Task.Run(() => WriteFileSafe(_scriptsFile, _serializer.Serialize(scripts))).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to save script '{script.Name}'", ex);
            }
            finally { _fileLock.Release(); }
        }

        public async Task DeleteScriptAsync(int scriptId)
        {
            await _fileLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var scripts = File.Exists(_scriptsFile)
                    ? _serializer.Deserialize<List<ScriptInfo>>(await Task.Run(() => ReadFileSafe(_scriptsFile)).ConfigureAwait(false))
                      ?? new List<ScriptInfo>()
                    : new List<ScriptInfo>();
                var removed = scripts.RemoveAll(s => s.ScriptId == scriptId);
                if (removed > 0)
                    await Task.Run(() => WriteFileSafe(_scriptsFile, _serializer.Serialize(scripts))).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to delete script {scriptId}", ex);
            }
            finally { _fileLock.Release(); }
        }

        // BOOKMARKS — uses bookmarks.json (already exists)
        public async Task<List<BookmarkInfo>> GetAllBookmarksAsync()
        {
            try
            {
                if (!File.Exists(_bookmarksFile)) return new List<BookmarkInfo>();
                var json = await Task.Run(() => ReadFileSafe(_bookmarksFile)).ConfigureAwait(false);
                return _serializer.Deserialize<List<BookmarkInfo>>(json) ?? new List<BookmarkInfo>();
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to get bookmarks", ex);
                return new List<BookmarkInfo>();
            }
        }

        public async Task SaveBookmarkAsync(BookmarkInfo bookmark)
        {
            await _fileLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var bookmarks = File.Exists(_bookmarksFile)
                    ? _serializer.Deserialize<List<BookmarkInfo>>(await Task.Run(() => ReadFileSafe(_bookmarksFile)).ConfigureAwait(false))
                      ?? new List<BookmarkInfo>()
                    : new List<BookmarkInfo>();

                var existingIndex = bookmarks.FindIndex(b =>
                    b.Hostname?.Equals(bookmark.Hostname, StringComparison.OrdinalIgnoreCase) == true);
                if (existingIndex >= 0) bookmarks[existingIndex] = bookmark;
                else                    bookmarks.Add(bookmark);

                await Task.Run(() => WriteFileSafe(_bookmarksFile, _serializer.Serialize(bookmarks))).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to save bookmark for {bookmark.Hostname}", ex);
            }
            finally { _fileLock.Release(); }
        }

        public async Task DeleteBookmarkAsync(string hostname)
        {
            await _fileLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var bookmarks = File.Exists(_bookmarksFile)
                    ? _serializer.Deserialize<List<BookmarkInfo>>(await Task.Run(() => ReadFileSafe(_bookmarksFile)).ConfigureAwait(false))
                      ?? new List<BookmarkInfo>()
                    : new List<BookmarkInfo>();
                var removed = bookmarks.RemoveAll(b =>
                    b.Hostname?.Equals(hostname, StringComparison.OrdinalIgnoreCase) == true);
                if (removed > 0)
                    await Task.Run(() => WriteFileSafe(_bookmarksFile, _serializer.Serialize(bookmarks))).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Failed to delete bookmark for {hostname}", ex);
            }
            finally { _fileLock.Release(); }
        }

        public async Task OptimizeDatabaseAsync()
        {
            // JSON files don't need optimization — no-op success
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

                var json = await Task.Run(() => ReadFileSafe(filePath));
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

                await Task.Run(() => WriteFileSafe(outputPath, csv.ToString()));
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

                var lines = await Task.Run(() => ReadLinesSafe(csvPath));
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

                // Save imported data (lock for atomic write)
                await _fileLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    var json = _serializer.Serialize(computers);
                    await Task.Run(() => WriteFileSafe(_computersFile, json)).ConfigureAwait(false);
                }
                finally
                {
                    _fileLock.Release();
                }

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

        /// <summary>Reads a file with FileShare.ReadWrite to avoid locking conflicts on network shares.</summary>
        private static string ReadFileSafe(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs, Encoding.UTF8))
                return sr.ReadToEnd();
        }

        /// <summary>Writes a file with FileShare.Read to allow concurrent readers on network shares.</summary>
        private static void WriteFileSafe(string path, string content)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (var sw = new StreamWriter(fs, Encoding.UTF8))
                sw.Write(content);
        }

        /// <summary>Reads all lines with FileShare.ReadWrite for network share safety.</summary>
        private static string[] ReadLinesSafe(string path)
        {
            var lines = new List<string>();
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs, Encoding.UTF8))
            {
                string line;
                while ((line = sr.ReadLine()) != null) lines.Add(line);
            }
            return lines.ToArray();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _fileLock?.Dispose();
                _disposed = true;
                LogManager.LogInfo("CSV/JSON provider disposed");
            }
        }
    }
}
