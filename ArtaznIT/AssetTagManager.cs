using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace ArtaznIT
{
    // TAG: #VERSION_7.1 #ASSET_TAGGING #CATEGORIZATION
    /// <summary>
    /// Manages asset tags and categories for computers
    /// Allows custom tagging for organization, filtering, and tracking
    /// </summary>
    public class AssetTagManager
    {
        private static readonly string TagStoragePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ArtaznIT", "AssetTags.json");

        private static List<AssetTag> _tags = new List<AssetTag>();
        private static Dictionary<string, List<string>> _computerTags = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Asset tag definition
        /// </summary>
        public class AssetTag
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public string Color { get; set; } // Hex color (e.g., "#FF6B35")
            public string Icon { get; set; } // Emoji icon
            public DateTime CreatedDate { get; set; }
            public bool IsSystemTag { get; set; } // Built-in tags can't be deleted

            public string DisplayName => $"{Icon} {Name}";
        }

        /// <summary>
        /// Auto-tagging rule
        /// </summary>
        public class AutoTagRule
        {
            public string RuleName { get; set; }
            public string TagName { get; set; }
            public string Condition { get; set; } // "OS", "Status", "Chassis", etc.
            public string Value { get; set; } // e.g., "Windows 7", "OFFLINE", "Laptop"
            public bool IsEnabled { get; set; }
        }

        private static List<AutoTagRule> _autoTagRules = new List<AutoTagRule>();

        /// <summary>
        /// Initialize asset tag system
        /// </summary>
        public static void Initialize()
        {
            try
            {
                LoadTags();
                CreateSystemTags();
                LogManager.LogInfo("[AssetTagManager] Initialized");
            }
            catch (Exception ex)
            {
                LogManager.LogError("[AssetTagManager] Failed to initialize", ex);
            }
        }

        /// <summary>
        /// Create built-in system tags
        /// </summary>
        private static void CreateSystemTags()
        {
            var systemTags = new List<AssetTag>
            {
                new AssetTag { Name = "VIP", Description = "High-priority / executive computers", Color = "#FFD700", Icon = "⭐", IsSystemTag = true, CreatedDate = DateTime.Now },
                new AssetTag { Name = "Critical", Description = "Mission-critical systems", Color = "#FF0000", Icon = "🔴", IsSystemTag = true, CreatedDate = DateTime.Now },
                new AssetTag { Name = "Quarantine", Description = "Isolated/infected systems", Color = "#FF6B00", Icon = "⚠️", IsSystemTag = true, CreatedDate = DateTime.Now },
                new AssetTag { Name = "Maintenance", Description = "Under maintenance", Color = "#FFA500", Icon = "🔧", IsSystemTag = true, CreatedDate = DateTime.Now },
                new AssetTag { Name = "Legacy", Description = "Old/unsupported systems", Color = "#808080", Icon = "📟", IsSystemTag = true, CreatedDate = DateTime.Now },
                new AssetTag { Name = "New", Description = "Recently deployed", Color = "#00FF00", Icon = "🆕", IsSystemTag = true, CreatedDate = DateTime.Now },
                new AssetTag { Name = "Decommission", Description = "Pending removal", Color = "#A9A9A9", Icon = "♻️", IsSystemTag = true, CreatedDate = DateTime.Now }
            };

            foreach (var tag in systemTags)
            {
                if (!_tags.Any(t => t.Name.Equals(tag.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    _tags.Add(tag);
                }
            }

            // Create default auto-tag rules
            if (_autoTagRules.Count == 0)
            {
                _autoTagRules.Add(new AutoTagRule
                {
                    RuleName = "Tag Windows 7 as Legacy",
                    TagName = "Legacy",
                    Condition = "OS",
                    Value = "Windows 7",
                    IsEnabled = true
                });

                _autoTagRules.Add(new AutoTagRule
                {
                    RuleName = "Tag Offline as Critical",
                    TagName = "Critical",
                    Condition = "Status",
                    Value = "OFFLINE",
                    IsEnabled = false // Disabled by default
                });
            }
        }

        /// <summary>
        /// Get all tags
        /// </summary>
        public static List<AssetTag> GetAllTags()
        {
            return _tags.ToList();
        }

        /// <summary>
        /// Create new tag
        /// </summary>
        public static bool CreateTag(AssetTag tag)
        {
            try
            {
                if (_tags.Any(t => t.Name.Equals(tag.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    LogManager.LogWarning($"[AssetTagManager] Tag '{tag.Name}' already exists");
                    return false;
                }

                tag.CreatedDate = DateTime.Now;
                tag.IsSystemTag = false;
                _tags.Add(tag);

                SaveTags();
                LogManager.LogInfo($"[AssetTagManager] Created tag: {tag.Name}");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.LogError($"[AssetTagManager] Failed to create tag: {tag.Name}", ex);
                return false;
            }
        }

        /// <summary>
        /// Delete tag
        /// </summary>
        public static bool DeleteTag(string tagName)
        {
            try
            {
                var tag = _tags.FirstOrDefault(t => t.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase));
                if (tag == null)
                    return false;

                if (tag.IsSystemTag)
                {
                    LogManager.LogWarning($"[AssetTagManager] Cannot delete system tag: {tagName}");
                    return false;
                }

                _tags.Remove(tag);

                // Remove from all computers
                foreach (var computerTags in _computerTags.Values)
                {
                    computerTags.RemoveAll(t => t.Equals(tagName, StringComparison.OrdinalIgnoreCase));
                }

                SaveTags();
                LogManager.LogInfo($"[AssetTagManager] Deleted tag: {tagName}");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.LogError($"[AssetTagManager] Failed to delete tag: {tagName}", ex);
                return false;
            }
        }

        /// <summary>
        /// Add tag to computer
        /// </summary>
        public static bool AddTagToComputer(string hostname, string tagName)
        {
            try
            {
                // Verify tag exists
                if (!_tags.Any(t => t.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase)))
                {
                    LogManager.LogWarning($"[AssetTagManager] Tag '{tagName}' does not exist");
                    return false;
                }

                if (!_computerTags.ContainsKey(hostname))
                    _computerTags[hostname] = new List<string>();

                if (!_computerTags[hostname].Contains(tagName, StringComparer.OrdinalIgnoreCase))
                {
                    _computerTags[hostname].Add(tagName);
                    SaveTags();
                    LogManager.LogInfo($"[AssetTagManager] Added tag '{tagName}' to {hostname}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                LogManager.LogError($"[AssetTagManager] Failed to add tag to {hostname}", ex);
                return false;
            }
        }

        /// <summary>
        /// Remove tag from computer
        /// </summary>
        public static bool RemoveTagFromComputer(string hostname, string tagName)
        {
            try
            {
                if (!_computerTags.ContainsKey(hostname))
                    return false;

                bool removed = _computerTags[hostname].RemoveAll(t => t.Equals(tagName, StringComparison.OrdinalIgnoreCase)) > 0;
                if (removed)
                {
                    SaveTags();
                    LogManager.LogInfo($"[AssetTagManager] Removed tag '{tagName}' from {hostname}");
                }
                return removed;
            }
            catch (Exception ex)
            {
                LogManager.LogError($"[AssetTagManager] Failed to remove tag from {hostname}", ex);
                return false;
            }
        }

        /// <summary>
        /// Get all tags for a computer
        /// </summary>
        public static List<string> GetComputerTags(string hostname)
        {
            if (_computerTags.ContainsKey(hostname))
                return _computerTags[hostname].ToList();
            return new List<string>();
        }

        /// <summary>
        /// Get tag details
        /// </summary>
        public static AssetTag GetTag(string tagName)
        {
            return _tags.FirstOrDefault(t => t.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Get all computers with a specific tag
        /// </summary>
        public static List<string> GetComputersWithTag(string tagName)
        {
            return _computerTags
                .Where(kvp => kvp.Value.Contains(tagName, StringComparer.OrdinalIgnoreCase))
                .Select(kvp => kvp.Key)
                .ToList();
        }

        /// <summary>
        /// Apply auto-tagging rules to a computer
        /// </summary>
        public static void ApplyAutoTagRules(string hostname, string os, string status, string chassis)
        {
            try
            {
                foreach (var rule in _autoTagRules.Where(r => r.IsEnabled))
                {
                    bool shouldTag = false;

                    switch (rule.Condition.ToUpper())
                    {
                        case "OS":
                            shouldTag = os != null && os.IndexOf(rule.Value, StringComparison.OrdinalIgnoreCase) >= 0;
                            break;
                        case "STATUS":
                            shouldTag = status != null && status.Equals(rule.Value, StringComparison.OrdinalIgnoreCase);
                            break;
                        case "CHASSIS":
                            shouldTag = chassis != null && chassis.IndexOf(rule.Value, StringComparison.OrdinalIgnoreCase) >= 0;
                            break;
                    }

                    if (shouldTag)
                    {
                        AddTagToComputer(hostname, rule.TagName);
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"[AssetTagManager] Failed to apply auto-tag rules to {hostname}", ex);
            }
        }

        /// <summary>
        /// Get auto-tag rules
        /// </summary>
        public static List<AutoTagRule> GetAutoTagRules()
        {
            return _autoTagRules.ToList();
        }

        /// <summary>
        /// Add auto-tag rule
        /// </summary>
        public static bool AddAutoTagRule(AutoTagRule rule)
        {
            try
            {
                _autoTagRules.Add(rule);
                SaveTags();
                LogManager.LogInfo($"[AssetTagManager] Added auto-tag rule: {rule.RuleName}");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.LogError($"[AssetTagManager] Failed to add auto-tag rule", ex);
                return false;
            }
        }

        /// <summary>
        /// Save tags and mappings to disk
        /// </summary>
        private static void SaveTags()
        {
            try
            {
                var data = new
                {
                    Tags = _tags,
                    ComputerTags = _computerTags,
                    AutoTagRules = _autoTagRules
                };

                var serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(data);

                string directory = Path.GetDirectoryName(TagStoragePath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(TagStoragePath, json);
            }
            catch (Exception ex)
            {
                LogManager.LogError("[AssetTagManager] Failed to save tags", ex);
            }
        }

        /// <summary>
        /// Load tags and mappings from disk
        /// </summary>
        private static void LoadTags()
        {
            try
            {
                if (!File.Exists(TagStoragePath))
                    return;

                string json = File.ReadAllText(TagStoragePath);
                var serializer = new JavaScriptSerializer();
                var data = serializer.Deserialize<Dictionary<string, object>>(json);

                if (data.ContainsKey("Tags"))
                {
                    var tagsJson = serializer.Serialize(data["Tags"]);
                    _tags = serializer.Deserialize<List<AssetTag>>(tagsJson) ?? new List<AssetTag>();
                }

                if (data.ContainsKey("ComputerTags"))
                {
                    var computerTagsJson = serializer.Serialize(data["ComputerTags"]);
                    _computerTags = serializer.Deserialize<Dictionary<string, List<string>>>(computerTagsJson) ?? new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                }

                if (data.ContainsKey("AutoTagRules"))
                {
                    var rulesJson = serializer.Serialize(data["AutoTagRules"]);
                    _autoTagRules = serializer.Deserialize<List<AutoTagRule>>(rulesJson) ?? new List<AutoTagRule>();
                }

                LogManager.LogInfo($"[AssetTagManager] Loaded {_tags.Count} tags, {_computerTags.Count} computer mappings");
            }
            catch (Exception ex)
            {
                LogManager.LogError("[AssetTagManager] Failed to load tags", ex);
            }
        }

        /// <summary>
        /// Get tag statistics
        /// </summary>
        public static Dictionary<string, int> GetTagStatistics()
        {
            var stats = new Dictionary<string, int>();

            foreach (var tag in _tags)
            {
                stats[tag.Name] = GetComputersWithTag(tag.Name).Count;
            }

            return stats;
        }

        /// <summary>
        /// Clear all tags from a computer
        /// </summary>
        public static bool ClearComputerTags(string hostname)
        {
            try
            {
                if (_computerTags.ContainsKey(hostname))
                {
                    _computerTags[hostname].Clear();
                    SaveTags();
                    LogManager.LogInfo($"[AssetTagManager] Cleared all tags from {hostname}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                LogManager.LogError($"[AssetTagManager] Failed to clear tags from {hostname}", ex);
                return false;
            }
        }
    }
}
