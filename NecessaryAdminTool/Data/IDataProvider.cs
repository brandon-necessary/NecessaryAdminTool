using System;
using System.Collections.Generic;
using System.Threading.Tasks;
// TAG: #DATABASE #VERSION_1_2

namespace NecessaryAdminTool.Data
{
    /// <summary>
    /// Data provider interface for database abstraction
    /// Supports SQLite, SQL Server, Access, and CSV/JSON
    /// TAG: #DATABASE_ABSTRACTION #VERSION_1_2
    /// </summary>
    public interface IDataProvider : IDisposable
    {
        // ═══════════════════════════════════════════════════
        // COMPUTER MANAGEMENT
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// Get all computers from database
        /// </summary>
        Task<List<ComputerInfo>> GetAllComputersAsync();

        /// <summary>
        /// Get specific computer by hostname
        /// </summary>
        Task<ComputerInfo> GetComputerAsync(string hostname);

        /// <summary>
        /// Save or update computer information
        /// </summary>
        Task SaveComputerAsync(ComputerInfo computer);

        /// <summary>
        /// Delete computer from database
        /// </summary>
        Task DeleteComputerAsync(string hostname);

        /// <summary>
        /// Search computers by criteria
        /// </summary>
        Task<List<ComputerInfo>> SearchComputersAsync(string searchTerm);

        // ═══════════════════════════════════════════════════
        // TAG MANAGEMENT
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// Get all tags for a computer
        /// </summary>
        Task<List<string>> GetComputerTagsAsync(string hostname);

        /// <summary>
        /// Add tag to computer
        /// </summary>
        Task AddTagAsync(string hostname, string tagName);

        /// <summary>
        /// Remove tag from computer
        /// </summary>
        Task RemoveTagAsync(string hostname, string tagName);

        /// <summary>
        /// Get all unique tags in database
        /// </summary>
        Task<List<string>> GetAllTagsAsync();

        // ═══════════════════════════════════════════════════
        // SCAN HISTORY
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// Get last scan information
        /// </summary>
        Task<ScanHistory> GetLastScanAsync();

        /// <summary>
        /// Save scan history
        /// </summary>
        Task SaveScanHistoryAsync(ScanHistory scan);

        /// <summary>
        /// Get scan history (most recent first)
        /// </summary>
        Task<List<ScanHistory>> GetScanHistoryAsync(int limit = 10);

        // ═══════════════════════════════════════════════════
        // SETTINGS
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// Get setting value
        /// </summary>
        Task<string> GetSettingAsync(string key, string defaultValue = null);

        /// <summary>
        /// Save setting value
        /// </summary>
        Task SaveSettingAsync(string key, string value);

        // ═══════════════════════════════════════════════════
        // SCRIPTS
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// Get all scripts
        /// </summary>
        Task<List<ScriptInfo>> GetAllScriptsAsync();

        /// <summary>
        /// Save script
        /// </summary>
        Task SaveScriptAsync(ScriptInfo script);

        /// <summary>
        /// Delete script
        /// </summary>
        Task DeleteScriptAsync(int scriptId);

        // ═══════════════════════════════════════════════════
        // BOOKMARKS
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// Get all bookmarks
        /// </summary>
        Task<List<BookmarkInfo>> GetAllBookmarksAsync();

        /// <summary>
        /// Save bookmark
        /// </summary>
        Task SaveBookmarkAsync(BookmarkInfo bookmark);

        /// <summary>
        /// Delete bookmark
        /// </summary>
        Task DeleteBookmarkAsync(string hostname);

        // ═══════════════════════════════════════════════════
        // DATABASE OPERATIONS
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// Initialize database schema
        /// </summary>
        Task InitializeDatabaseAsync();

        /// <summary>
        /// Optimize database (VACUUM, compact, etc.)
        /// </summary>
        Task OptimizeDatabaseAsync();

        /// <summary>
        /// Verify database integrity
        /// </summary>
        Task<bool> VerifyIntegrityAsync();

        /// <summary>
        /// Get database statistics
        /// </summary>
        Task<DatabaseStats> GetDatabaseStatsAsync();

        /// <summary>
        /// Backup database to file
        /// </summary>
        Task<bool> BackupDatabaseAsync(string backupPath);

        /// <summary>
        /// Restore database from backup
        /// </summary>
        Task<bool> RestoreDatabaseAsync(string backupPath);
    }

    // ═══════════════════════════════════════════════════
    // DATA MODELS
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Computer information model
    /// </summary>
    public class ComputerInfo
    {
        public string Hostname { get; set; }
        public string OS { get; set; }
        public DateTime LastSeen { get; set; }
        public string Status { get; set; } // ONLINE, OFFLINE, UNKNOWN
        public string IPAddress { get; set; }
        public string Manufacturer { get; set; }
        public string Model { get; set; }
        public string SerialNumber { get; set; }
        public string ChassisType { get; set; }
        public DateTime? LastBootTime { get; set; }
        public long? Uptime { get; set; } // Seconds
        public string DomainController { get; set; }
        public string RawDataJson { get; set; } // Full WMI data as JSON
    }

    /// <summary>
    /// Scan history model
    /// </summary>
    public class ScanHistory
    {
        public int ScanId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int ComputersScanned { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public double DurationSeconds { get; set; }
    }

    /// <summary>
    /// Script information model
    /// </summary>
    public class ScriptInfo
    {
        public int ScriptId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Content { get; set; }
        public string Category { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Bookmark information model
    /// </summary>
    public class BookmarkInfo
    {
        public string Hostname { get; set; }
        public string Category { get; set; }
        public string Notes { get; set; }
        public bool IsFavorite { get; set; }
    }

    /// <summary>
    /// Database statistics model
    /// </summary>
    public class DatabaseStats
    {
        public long SizeBytes { get; set; }
        public int TotalComputers { get; set; }
        public int TotalTags { get; set; }
        public int TotalScans { get; set; }
        public int TotalScripts { get; set; }
        public int TotalBookmarks { get; set; }
        public DateTime LastBackup { get; set; }
        public string EncryptionStatus { get; set; }
    }
}
