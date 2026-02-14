using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;

namespace NecessaryAdminTool
{
    // TAG: #VERSION_7 #BOOKMARKS #FAVORITES #FEATURE
    /// <summary>
    /// Manages bookmarked/favorite computers for quick access
    /// Allows users to star critical servers and access them quickly
    /// </summary>
    public class BookmarkManager
    {
        private static List<ComputerBookmark> _bookmarks = new List<ComputerBookmark>();

        /// <summary>
        /// Load all bookmarks from settings
        /// </summary>
        public static void LoadBookmarks()
        {
            try
            {
                string json = Properties.Settings.Default.BookmarksJson ?? "";
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var serializer = new JavaScriptSerializer();
                    _bookmarks = serializer.Deserialize<List<ComputerBookmark>>(json) ?? new List<ComputerBookmark>();
                    LogManager.LogInfo($"[Bookmarks] Loaded {_bookmarks.Count} bookmarked computers");
                }
                else
                {
                    _bookmarks = new List<ComputerBookmark>();
                    LogManager.LogInfo("[Bookmarks] No saved bookmarks found");
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError("[Bookmarks] Failed to load bookmarks", ex);
                _bookmarks = new List<ComputerBookmark>();
            }
        }

        /// <summary>
        /// Save all bookmarks to settings
        /// </summary>
        public static void SaveBookmarks()
        {
            try
            {
                var serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(_bookmarks);
                Properties.Settings.Default.BookmarksJson = json;
                Properties.Settings.Default.Save();
                LogManager.LogInfo($"[Bookmarks] Saved {_bookmarks.Count} bookmarks");
            }
            catch (Exception ex)
            {
                LogManager.LogError("[Bookmarks] Failed to save bookmarks", ex);
            }
        }

        /// <summary>
        /// Get all bookmarks
        /// </summary>
        public static List<ComputerBookmark> GetBookmarks()
        {
            return new List<ComputerBookmark>(_bookmarks);
        }

        /// <summary>
        /// Add a computer to bookmarks
        /// </summary>
        public static void AddBookmark(string hostname, string description = null, string category = null)
        {
            // Check if already bookmarked
            if (IsBookmarked(hostname))
            {
                LogManager.LogInfo($"[Bookmarks] Computer '{hostname}' is already bookmarked");
                return;
            }

            var bookmark = new ComputerBookmark
            {
                Hostname = hostname,
                Description = description ?? "",
                Category = category ?? "General",
                BookmarkedDate = DateTime.Now
            };

            _bookmarks.Add(bookmark);
            SaveBookmarks();

            LogManager.LogInfo($"[Bookmarks] Added bookmark: {hostname}");
        }

        /// <summary>
        /// Remove a computer from bookmarks
        /// </summary>
        public static bool RemoveBookmark(string hostname)
        {
            int removed = _bookmarks.RemoveAll(b => b.Hostname.Equals(hostname, StringComparison.OrdinalIgnoreCase));

            if (removed > 0)
            {
                SaveBookmarks();
                LogManager.LogInfo($"[Bookmarks] Removed bookmark: {hostname}");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Check if a computer is bookmarked
        /// </summary>
        public static bool IsBookmarked(string hostname)
        {
            return _bookmarks.Any(b => b.Hostname.Equals(hostname, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Get bookmark for a specific computer
        /// </summary>
        public static ComputerBookmark GetBookmark(string hostname)
        {
            return _bookmarks.FirstOrDefault(b => b.Hostname.Equals(hostname, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Update bookmark description or category
        /// </summary>
        public static void UpdateBookmark(string hostname, string description = null, string category = null)
        {
            var bookmark = GetBookmark(hostname);
            if (bookmark != null)
            {
                if (description != null) bookmark.Description = description;
                if (category != null) bookmark.Category = category;
                SaveBookmarks();
                LogManager.LogInfo($"[Bookmarks] Updated bookmark: {hostname}");
            }
        }

        /// <summary>
        /// Get bookmarks grouped by category
        /// </summary>
        public static Dictionary<string, List<ComputerBookmark>> GetBookmarksByCategory()
        {
            return _bookmarks.GroupBy(b => b.Category ?? "General")
                            .ToDictionary(g => g.Key, g => g.ToList());
        }
    }

    // ══════════════════════════════════════════════════════════════
    // BOOKMARK DATA MODEL
    // TAG: #VERSION_7 #BOOKMARKS
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Represents a bookmarked/favorite computer
    /// </summary>
    public class ComputerBookmark
    {
        /// <summary>Computer hostname</summary>
        public string Hostname { get; set; }

        /// <summary>User description/notes</summary>
        public string Description { get; set; }

        /// <summary>Category (Domain Controllers, SQL Servers, Web Servers, etc.)</summary>
        public string Category { get; set; }

        /// <summary>Date/time when bookmarked</summary>
        public DateTime BookmarkedDate { get; set; }

        /// <summary>Display name for UI bindings</summary>
        public string DisplayName => string.IsNullOrWhiteSpace(Description)
            ? Hostname
            : $"{Hostname} - {Description}";

        /// <summary>Icon for category</summary>
        public string CategoryIcon
        {
            get
            {
                return Category?.ToLower() switch
                {
                    "domain controllers" => "🌐",
                    "sql servers" => "🗄️",
                    "web servers" => "🌍",
                    "file servers" => "📁",
                    "exchange servers" => "📧",
                    "critical" => "⭐",
                    _ => "💾"
                };
            }
        }
    }
}
