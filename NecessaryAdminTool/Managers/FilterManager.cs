using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;
using System.Web.Script.Serialization;
using NecessaryAdminTool.Models;
using NecessaryAdminTool.Security;
using NecessaryAdminTool.Managers.UI;

namespace NecessaryAdminTool.Managers
{
    // TAG: #AUTO_UPDATE_UI_ENGINE #FILTER_SYSTEM #FLUENT_DESIGN #CORE_MANAGER
    /// <summary>
    /// Centralized filtering engine for computer inventory
    /// Handles filter presets, history, validation, and application logic
    /// TAG: #SECURITY_CRITICAL - All inputs validated via SecurityValidator
    /// </summary>
    public static class FilterManager
    {
        private static ObservableCollection<FilterPreset> _presets;
        private static List<FilterHistoryEntry> _history;
        private static FilterCriteria _currentFilter;
        private const int MAX_HISTORY = 10;
        private static readonly string PresetsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NecessaryAdminTool",
            "FilterPresets.json"
        );

        /// <summary>
        /// Current active filter
        /// </summary>
        public static FilterCriteria CurrentFilter
        {
            get => _currentFilter ?? (_currentFilter = new FilterCriteria());
            set => _currentFilter = value;
        }

        /// <summary>
        /// Initialize filter manager
        /// TAG: #INITIALIZATION
        /// </summary>
        public static void Initialize()
        {
            LogManager.LogInfo("[FilterManager] Initializing filter manager");

            try
            {
                _presets = new ObservableCollection<FilterPreset>();
                _history = new List<FilterHistoryEntry>();
                _currentFilter = new FilterCriteria();

                // Load built-in presets
                InitializeBuiltInPresets();

                // Load saved presets
                LoadPresets();

                LogManager.LogInfo($"[FilterManager] Initialized with {_presets.Count} presets");
            }
            catch (Exception ex)
            {
                LogManager.LogError("[FilterManager] Failed to initialize", ex);
                ToastManager.ShowError("Failed to initialize filter manager", category: "error");
            }
        }

        #region Built-In Presets

        /// <summary>
        /// Initialize built-in filter presets
        /// TAG: #BUILT_IN_PRESETS
        /// </summary>
        private static void InitializeBuiltInPresets()
        {
            _presets.Add(new FilterPreset(
                "Online Computers",
                new FilterCriteria { StatusFilter = "Online" },
                isBuiltIn: true
            )
            {
                Description = "Show only computers currently responding to ping"
            });

            _presets.Add(new FilterPreset(
                "Offline Computers",
                new FilterCriteria { StatusFilter = "Offline" },
                isBuiltIn: true
            )
            {
                Description = "Show only computers not responding to ping"
            });

            _presets.Add(new FilterPreset(
                "Windows 11",
                new FilterCriteria { OSFilter = "Windows 11" },
                isBuiltIn: true
            )
            {
                Description = "Show only Windows 11 computers"
            });

            _presets.Add(new FilterPreset(
                "Windows 10",
                new FilterCriteria { OSFilter = "Windows 10" },
                isBuiltIn: true
            )
            {
                Description = "Show only Windows 10 computers"
            });

            _presets.Add(new FilterPreset(
                "Windows 7 (EOL)",
                new FilterCriteria { OSFilter = "Windows 7" },
                isBuiltIn: true
            )
            {
                Description = "Show only Windows 7 computers (end-of-life)"
            });

            _presets.Add(new FilterPreset(
                "Windows Servers",
                new FilterCriteria { OSFilter = "Server" },
                isBuiltIn: true
            )
            {
                Description = "Show only Windows Server operating systems"
            });

            _presets.Add(new FilterPreset(
                "Workstations",
                new FilterCriteria { OSFilter = "Workstation" },
                isBuiltIn: true
            )
            {
                Description = "Show only workstation operating systems (non-server)"
            });

            _presets.Add(new FilterPreset(
                "High Memory (≥16GB)",
                new FilterCriteria { MinRamGB = 16 },
                isBuiltIn: true
            )
            {
                Description = "Show computers with 16GB RAM or more"
            });

            _presets.Add(new FilterPreset(
                "Low Memory (<8GB)",
                new FilterCriteria { MaxRamGB = 8 },
                isBuiltIn: true
            )
            {
                Description = "Show computers with less than 8GB RAM"
            });

            LogManager.LogInfo($"[FilterManager] Initialized {_presets.Count} built-in presets");
        }

        #endregion

        #region Preset Management

        /// <summary>
        /// Get all filter presets
        /// </summary>
        public static ObservableCollection<FilterPreset> GetPresets()
        {
            return _presets ?? (_presets = new ObservableCollection<FilterPreset>());
        }

        /// <summary>
        /// Save a new filter preset
        /// TAG: #SECURITY_CRITICAL - Validates preset name and criteria
        /// </summary>
        public static void SavePreset(string name, string description, FilterCriteria criteria)
        {
            LogManager.LogInfo($"[FilterManager] Saving preset: {name}");

            try
            {
                // TAG: #SECURITY_CRITICAL #INPUT_VALIDATION
                // Validate preset name (no path separators, limited length)
                if (string.IsNullOrWhiteSpace(name))
                {
                    ToastManager.ShowWarning("Preset name cannot be empty", category: "validation");
                    return;
                }

                if (name.Length > 100)
                {
                    ToastManager.ShowWarning("Preset name too long (max 100 characters)", category: "validation");
                    return;
                }

                if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    ToastManager.ShowWarning("Preset name contains invalid characters", category: "validation");
                    return;
                }

                // Validate filter criteria
                if (!ValidateCriteria(criteria))
                {
                    ToastManager.ShowWarning("Invalid filter criteria", category: "validation");
                    return;
                }

                // Check for duplicate names
                var existing = _presets.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    if (existing.IsBuiltIn)
                    {
                        ToastManager.ShowWarning("Cannot overwrite built-in preset", category: "validation");
                        return;
                    }

                    // Update existing preset
                    existing.Description = description;
                    existing.Criteria = criteria.Clone();
                    existing.ModifiedAt = DateTime.Now;
                    LogManager.LogInfo($"[FilterManager] Updated existing preset: {name}");
                }
                else
                {
                    // Create new preset
                    var preset = new FilterPreset(name, criteria.Clone())
                    {
                        Description = description
                    };
                    _presets.Add(preset);
                    LogManager.LogInfo($"[FilterManager] Created new preset: {name}");
                }

                // Save to disk
                SavePresets();

                ToastManager.ShowSuccess($"Filter preset '{name}' saved", category: "workflow");
            }
            catch (Exception ex)
            {
                LogManager.LogError($"[FilterManager] Failed to save preset: {name}", ex);
                ToastManager.ShowError($"Failed to save preset: {ex.Message}", category: "error");
            }
        }

        /// <summary>
        /// Delete a filter preset
        /// TAG: #SECURITY_CRITICAL - Prevents deletion of built-in presets
        /// </summary>
        public static void DeletePreset(string presetId)
        {
            LogManager.LogInfo($"[FilterManager] Deleting preset: {presetId}");

            try
            {
                var preset = _presets.FirstOrDefault(p => p.Id == presetId);
                if (preset == null)
                {
                    ToastManager.ShowWarning("Preset not found", category: "validation");
                    return;
                }

                // TAG: #SECURITY_CRITICAL - Prevent deletion of built-in presets
                if (preset.IsBuiltIn)
                {
                    ToastManager.ShowWarning("Cannot delete built-in preset", category: "validation");
                    return;
                }

                _presets.Remove(preset);
                SavePresets();

                LogManager.LogInfo($"[FilterManager] Deleted preset: {preset.Name}");
                ToastManager.ShowSuccess($"Filter preset '{preset.Name}' deleted", category: "workflow");
            }
            catch (Exception ex)
            {
                LogManager.LogError($"[FilterManager] Failed to delete preset: {presetId}", ex);
                ToastManager.ShowError($"Failed to delete preset: {ex.Message}", category: "error");
            }
        }

        /// <summary>
        /// Load a filter preset
        /// </summary>
        public static void LoadPreset(string presetId)
        {
            LogManager.LogInfo($"[FilterManager] Loading preset: {presetId}");

            try
            {
                var preset = _presets.FirstOrDefault(p => p.Id == presetId);
                if (preset == null)
                {
                    ToastManager.ShowWarning("Preset not found", category: "validation");
                    return;
                }

                CurrentFilter = preset.Criteria.Clone();
                LogManager.LogInfo($"[FilterManager] Loaded preset: {preset.Name}");
                ToastManager.ShowInfo($"Filter preset '{preset.Name}' loaded", category: "workflow");
            }
            catch (Exception ex)
            {
                LogManager.LogError($"[FilterManager] Failed to load preset: {presetId}", ex);
                ToastManager.ShowError($"Failed to load preset: {ex.Message}", category: "error");
            }
        }

        #endregion

        #region Persistence

        /// <summary>
        /// Save presets to disk
        /// TAG: #SECURITY_CRITICAL - Validates file path before writing
        /// </summary>
        private static void SavePresets()
        {
            try
            {
                // TAG: #SECURITY_CRITICAL #PATH_TRAVERSAL_PREVENTION
                // Validate file path
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string allowedBasePath = Path.Combine(appDataPath, "NecessaryAdminTool");

                if (!SecurityValidator.IsValidFilePath(PresetsPath, allowedBasePath))
                {
                    LogManager.LogWarning("[FilterManager] Blocked save to invalid path");
                    return;
                }

                // Ensure directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(PresetsPath));

                // Filter out built-in presets (don't save them)
                var userPresets = _presets.Where(p => !p.IsBuiltIn).ToList();

                var serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(userPresets);
                File.WriteAllText(PresetsPath, json);

                LogManager.LogInfo($"[FilterManager] Saved {userPresets.Count} presets to disk");
            }
            catch (Exception ex)
            {
                LogManager.LogError("[FilterManager] Failed to save presets", ex);
            }
        }

        /// <summary>
        /// Load presets from disk
        /// TAG: #SECURITY_CRITICAL - Validates file path and content
        /// </summary>
        private static void LoadPresets()
        {
            try
            {
                if (!File.Exists(PresetsPath))
                {
                    LogManager.LogInfo("[FilterManager] No saved presets file found");
                    return;
                }

                // TAG: #SECURITY_CRITICAL #PATH_TRAVERSAL_PREVENTION
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string allowedBasePath = Path.Combine(appDataPath, "NecessaryAdminTool");

                if (!SecurityValidator.IsValidFilePath(PresetsPath, allowedBasePath))
                {
                    LogManager.LogWarning("[FilterManager] Blocked load from invalid path");
                    return;
                }

                // TAG: #SECURITY_CRITICAL #FILE_SIZE_VALIDATION
                var fileInfo = new FileInfo(PresetsPath);
                const long MAX_FILE_SIZE = 5 * 1024 * 1024; // 5 MB
                if (fileInfo.Length > MAX_FILE_SIZE)
                {
                    LogManager.LogWarning($"[FilterManager] Presets file too large: {fileInfo.Length} bytes");
                    return;
                }

                string json = File.ReadAllText(PresetsPath);
                var serializer = new JavaScriptSerializer();
                var userPresets = serializer.Deserialize<List<FilterPreset>>(json);

                if (userPresets != null)
                {
                    foreach (var preset in userPresets)
                    {
                        // Validate loaded preset
                        if (ValidateCriteria(preset.Criteria))
                        {
                            _presets.Add(preset);
                        }
                        else
                        {
                            LogManager.LogWarning($"[FilterManager] Skipped invalid preset: {preset.Name}");
                        }
                    }
                }

                LogManager.LogInfo($"[FilterManager] Loaded {userPresets?.Count ?? 0} user presets from disk");
            }
            catch (Exception ex)
            {
                LogManager.LogError("[FilterManager] Failed to load presets", ex);
            }
        }

        #endregion

        #region Filter Application

        /// <summary>
        /// Apply filter criteria to a collection of computer records
        /// TAG: #CORE_FILTERING_LOGIC
        /// </summary>
        public static List<T> ApplyFilter<T>(List<T> computers, FilterCriteria criteria, Func<T, string> getName,
            Func<T, string> getStatus, Func<T, string> getOS, Func<T, string> getOU,
            Func<T, int?> getRamGB, Func<T, DateTime?> getLastSeen)
        {
            if (computers == null || computers.Count == 0)
                return new List<T>();

            if (criteria == null || criteria.IsEmpty())
                return computers;

            LogManager.LogInfo($"[FilterManager] Applying filter: {criteria.GetDescription()}");

            try
            {
                var filtered = computers.Where(computer =>
                {
                    var results = new List<bool>();

                    // Name pattern filter
                    if (!string.IsNullOrWhiteSpace(criteria.NamePattern))
                    {
                        string name = getName(computer) ?? "";
                        results.Add(MatchesPattern(name, criteria.NamePattern));
                    }

                    // Status filter
                    if (!string.IsNullOrWhiteSpace(criteria.StatusFilter))
                    {
                        string status = getStatus(computer) ?? "";
                        results.Add(status.Equals(criteria.StatusFilter, StringComparison.OrdinalIgnoreCase));
                    }

                    // OS filter
                    if (!string.IsNullOrWhiteSpace(criteria.OSFilter))
                    {
                        string os = getOS(computer) ?? "";
                        results.Add(MatchesOSFilter(os, criteria.OSFilter));
                    }

                    // OU filter
                    if (!string.IsNullOrWhiteSpace(criteria.OUFilter))
                    {
                        string ou = getOU(computer) ?? "";
                        results.Add(ou.IndexOf(criteria.OUFilter, StringComparison.OrdinalIgnoreCase) >= 0);
                    }

                    // RAM filter
                    if (criteria.MinRamGB.HasValue || criteria.MaxRamGB.HasValue)
                    {
                        int? ram = getRamGB(computer);
                        if (ram.HasValue)
                        {
                            bool ramMatch = true;
                            if (criteria.MinRamGB.HasValue)
                                ramMatch &= ram.Value >= criteria.MinRamGB.Value;
                            if (criteria.MaxRamGB.HasValue)
                                ramMatch &= ram.Value <= criteria.MaxRamGB.Value;
                            results.Add(ramMatch);
                        }
                        else
                        {
                            results.Add(false); // No RAM data = doesn't match
                        }
                    }

                    // Last seen date filter
                    if (criteria.LastSeenAfter.HasValue || criteria.LastSeenBefore.HasValue)
                    {
                        DateTime? lastSeen = getLastSeen(computer);
                        if (lastSeen.HasValue)
                        {
                            bool dateMatch = true;
                            if (criteria.LastSeenAfter.HasValue)
                                dateMatch &= lastSeen.Value >= criteria.LastSeenAfter.Value;
                            if (criteria.LastSeenBefore.HasValue)
                                dateMatch &= lastSeen.Value <= criteria.LastSeenBefore.Value;
                            results.Add(dateMatch);
                        }
                        else
                        {
                            results.Add(false); // No last seen data = doesn't match
                        }
                    }

                    // Apply logic operator (AND or OR)
                    if (results.Count == 0)
                        return true; // No filters = include

                    return criteria.LogicOperator.Equals("OR", StringComparison.OrdinalIgnoreCase)
                        ? results.Any(r => r) // OR: at least one match
                        : results.All(r => r); // AND: all must match
                }).ToList();

                LogManager.LogInfo($"[FilterManager] Filter applied: {filtered.Count}/{computers.Count} computers matched");

                // Add to history
                AddToHistory(criteria, filtered.Count);

                return filtered;
            }
            catch (Exception ex)
            {
                LogManager.LogError("[FilterManager] Failed to apply filter", ex);
                ToastManager.ShowError($"Failed to apply filter: {ex.Message}", category: "error");
                return computers; // Return unfiltered on error
            }
        }

        /// <summary>
        /// Match computer name against pattern (supports wildcards)
        /// TAG: #SECURITY_CRITICAL - Pattern validated before use
        /// </summary>
        private static bool MatchesPattern(string name, string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                return true;

            // TAG: #SECURITY_CRITICAL #WILDCARD_INJECTION_PREVENTION
            // Validate pattern before converting to regex
            if (!SecurityValidator.ValidateFilterPattern(pattern))
            {
                LogManager.LogWarning($"[FilterManager] Invalid filter pattern blocked: {pattern}");
                return false;
            }

            try
            {
                // Convert wildcard pattern to regex
                // * = any characters, ? = single character
                string regexPattern = "^" + Regex.Escape(pattern)
                    .Replace("\\*", ".*")
                    .Replace("\\?", ".") + "$";

                return Regex.IsMatch(name, regexPattern, RegexOptions.IgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Match OS against filter (handles "Server" and "Workstation" special cases)
        /// </summary>
        private static bool MatchesOSFilter(string os, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
                return true;

            // Special case: "Server" matches any Windows Server
            if (filter.Equals("Server", StringComparison.OrdinalIgnoreCase))
            {
                return os.IndexOf("Server", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            // Special case: "Workstation" matches non-server Windows
            if (filter.Equals("Workstation", StringComparison.OrdinalIgnoreCase))
            {
                return os.IndexOf("Server", StringComparison.OrdinalIgnoreCase) < 0 &&
                       os.IndexOf("Windows", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            // Normal case: partial match
            return os.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        #endregion

        #region Validation

        /// <summary>
        /// Validate filter criteria
        /// TAG: #SECURITY_CRITICAL - Comprehensive input validation
        /// </summary>
        public static bool ValidateCriteria(FilterCriteria criteria)
        {
            if (criteria == null)
                return false;

            try
            {
                // TAG: #SECURITY_CRITICAL #INPUT_VALIDATION
                // Validate name pattern (wildcard injection prevention)
                if (!string.IsNullOrWhiteSpace(criteria.NamePattern))
                {
                    if (!SecurityValidator.ValidateFilterPattern(criteria.NamePattern))
                    {
                        LogManager.LogWarning($"[FilterManager] Invalid name pattern: {criteria.NamePattern}");
                        return false;
                    }
                }

                // Validate OU path (LDAP injection prevention)
                if (!string.IsNullOrWhiteSpace(criteria.OUFilter))
                {
                    if (!SecurityValidator.ValidateOUPath(criteria.OUFilter))
                    {
                        LogManager.LogWarning($"[FilterManager] Invalid OU path: {criteria.OUFilter}");
                        return false;
                    }
                }

                // Validate RAM range (numeric validation)
                if (criteria.MinRamGB.HasValue)
                {
                    if (!SecurityValidator.ValidateNumericFilter(criteria.MinRamGB.Value, 1, 1024))
                    {
                        LogManager.LogWarning($"[FilterManager] Invalid MinRamGB: {criteria.MinRamGB}");
                        return false;
                    }
                }

                if (criteria.MaxRamGB.HasValue)
                {
                    if (!SecurityValidator.ValidateNumericFilter(criteria.MaxRamGB.Value, 1, 1024))
                    {
                        LogManager.LogWarning($"[FilterManager] Invalid MaxRamGB: {criteria.MaxRamGB}");
                        return false;
                    }
                }

                // Ensure min < max for RAM
                if (criteria.MinRamGB.HasValue && criteria.MaxRamGB.HasValue)
                {
                    if (criteria.MinRamGB.Value > criteria.MaxRamGB.Value)
                    {
                        LogManager.LogWarning($"[FilterManager] MinRamGB > MaxRamGB: {criteria.MinRamGB} > {criteria.MaxRamGB}");
                        return false;
                    }
                }

                // Ensure start < end for dates
                if (criteria.LastSeenAfter.HasValue && criteria.LastSeenBefore.HasValue)
                {
                    if (criteria.LastSeenAfter.Value > criteria.LastSeenBefore.Value)
                    {
                        LogManager.LogWarning($"[FilterManager] LastSeenAfter > LastSeenBefore");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                LogManager.LogError("[FilterManager] Validation error", ex);
                return false;
            }
        }

        #endregion

        #region History

        /// <summary>
        /// Add filter to history
        /// TAG: #HISTORY_TRACKING
        /// </summary>
        private static void AddToHistory(FilterCriteria criteria, int resultCount)
        {
            try
            {
                if (_history == null)
                    _history = new List<FilterHistoryEntry>();

                _history.Insert(0, new FilterHistoryEntry(criteria, resultCount));

                // Limit history size
                if (_history.Count > MAX_HISTORY)
                    _history.RemoveAt(_history.Count - 1);

                LogManager.LogInfo($"[FilterManager] Added to history (total: {_history.Count})");
            }
            catch (Exception ex)
            {
                LogManager.LogError("[FilterManager] Failed to add to history", ex);
            }
        }

        /// <summary>
        /// Get filter history
        /// </summary>
        public static List<FilterHistoryEntry> GetHistory()
        {
            return _history ?? (_history = new List<FilterHistoryEntry>());
        }

        /// <summary>
        /// Clear filter history
        /// </summary>
        public static void ClearHistory()
        {
            _history?.Clear();
            LogManager.LogInfo("[FilterManager] Filter history cleared");
        }

        #endregion

        #region Quick Filters

        /// <summary>
        /// Get quick filter for online computers
        /// </summary>
        public static FilterCriteria GetOnlineFilter()
        {
            return new FilterCriteria { StatusFilter = "Online" };
        }

        /// <summary>
        /// Get quick filter for offline computers
        /// </summary>
        public static FilterCriteria GetOfflineFilter()
        {
            return new FilterCriteria { StatusFilter = "Offline" };
        }

        /// <summary>
        /// Get quick filter for Windows 11
        /// </summary>
        public static FilterCriteria GetWindows11Filter()
        {
            return new FilterCriteria { OSFilter = "Windows 11" };
        }

        /// <summary>
        /// Get quick filter for Windows 7
        /// </summary>
        public static FilterCriteria GetWindows7Filter()
        {
            return new FilterCriteria { OSFilter = "Windows 7" };
        }

        /// <summary>
        /// Get quick filter for servers
        /// </summary>
        public static FilterCriteria GetServersFilter()
        {
            return new FilterCriteria { OSFilter = "Server" };
        }

        /// <summary>
        /// Get quick filter for workstations
        /// </summary>
        public static FilterCriteria GetWorkstationsFilter()
        {
            return new FilterCriteria { OSFilter = "Workstation" };
        }

        /// <summary>
        /// Clear all filters
        /// </summary>
        public static void ClearFilter()
        {
            CurrentFilter = new FilterCriteria();
            LogManager.LogInfo("[FilterManager] Filter cleared");
        }

        #endregion
    }
}
