using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace NecessaryAdminTool.Models
{
    // TAG: #AUTO_UPDATE_UI_ENGINE #FILTER_SYSTEM #FLUENT_DESIGN #MODELS
    /// <summary>
    /// Filter criteria for computer inventory filtering
    /// Supports multi-criteria filtering with AND/OR logic
    /// </summary>
    public class FilterCriteria : INotifyPropertyChanged
    {
        private string _namePattern;
        private string _statusFilter;
        private string _osFilter;
        private string _ouFilter;
        private int? _minRamGB;
        private int? _maxRamGB;
        private DateTime? _lastSeenAfter;
        private DateTime? _lastSeenBefore;
        private string _logicOperator;

        public event PropertyChangedEventHandler PropertyChanged;

        public FilterCriteria()
        {
            LogicOperator = "AND"; // Default to AND logic
        }

        #region Properties

        /// <summary>
        /// Computer name pattern (supports wildcards: *, ?)
        /// TAG: #SECURITY_CRITICAL - Validated by SecurityValidator.ValidateFilterPattern
        /// </summary>
        public string NamePattern
        {
            get => _namePattern;
            set
            {
                _namePattern = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Status filter: "Online", "Offline", or null for all
        /// </summary>
        public string StatusFilter
        {
            get => _statusFilter;
            set
            {
                _statusFilter = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// OS filter: "Windows 11", "Windows 10", "Windows Server", etc.
        /// </summary>
        public string OSFilter
        {
            get => _osFilter;
            set
            {
                _osFilter = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Organizational Unit filter (partial match)
        /// TAG: #SECURITY_CRITICAL - Validated by SecurityValidator.ValidateOUPath
        /// </summary>
        public string OUFilter
        {
            get => _ouFilter;
            set
            {
                _ouFilter = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Minimum RAM in GB
        /// TAG: #SECURITY_CRITICAL - Validated by SecurityValidator.ValidateNumericFilter
        /// </summary>
        public int? MinRamGB
        {
            get => _minRamGB;
            set
            {
                _minRamGB = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Maximum RAM in GB
        /// TAG: #SECURITY_CRITICAL - Validated by SecurityValidator.ValidateNumericFilter
        /// </summary>
        public int? MaxRamGB
        {
            get => _maxRamGB;
            set
            {
                _maxRamGB = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Last seen date range (start)
        /// </summary>
        public DateTime? LastSeenAfter
        {
            get => _lastSeenAfter;
            set
            {
                _lastSeenAfter = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Last seen date range (end)
        /// </summary>
        public DateTime? LastSeenBefore
        {
            get => _lastSeenBefore;
            set
            {
                _lastSeenBefore = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Logic operator for combining criteria: "AND" or "OR"
        /// </summary>
        public string LogicOperator
        {
            get => _logicOperator;
            set
            {
                _logicOperator = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Check if filter has any active criteria
        /// </summary>
        public bool IsEmpty()
        {
            return string.IsNullOrWhiteSpace(NamePattern) &&
                   string.IsNullOrWhiteSpace(StatusFilter) &&
                   string.IsNullOrWhiteSpace(OSFilter) &&
                   string.IsNullOrWhiteSpace(OUFilter) &&
                   !MinRamGB.HasValue &&
                   !MaxRamGB.HasValue &&
                   !LastSeenAfter.HasValue &&
                   !LastSeenBefore.HasValue;
        }

        /// <summary>
        /// Get human-readable description of active filters
        /// </summary>
        public string GetDescription()
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(NamePattern))
                parts.Add($"Name: {NamePattern}");

            if (!string.IsNullOrWhiteSpace(StatusFilter))
                parts.Add($"Status: {StatusFilter}");

            if (!string.IsNullOrWhiteSpace(OSFilter))
                parts.Add($"OS: {OSFilter}");

            if (!string.IsNullOrWhiteSpace(OUFilter))
                parts.Add($"OU: {OUFilter}");

            if (MinRamGB.HasValue || MaxRamGB.HasValue)
            {
                if (MinRamGB.HasValue && MaxRamGB.HasValue)
                    parts.Add($"RAM: {MinRamGB}-{MaxRamGB} GB");
                else if (MinRamGB.HasValue)
                    parts.Add($"RAM: ≥{MinRamGB} GB");
                else
                    parts.Add($"RAM: ≤{MaxRamGB} GB");
            }

            if (LastSeenAfter.HasValue || LastSeenBefore.HasValue)
            {
                if (LastSeenAfter.HasValue && LastSeenBefore.HasValue)
                    parts.Add($"Last seen: {LastSeenAfter:yyyy-MM-dd} to {LastSeenBefore:yyyy-MM-dd}");
                else if (LastSeenAfter.HasValue)
                    parts.Add($"Last seen: after {LastSeenAfter:yyyy-MM-dd}");
                else
                    parts.Add($"Last seen: before {LastSeenBefore:yyyy-MM-dd}");
            }

            return parts.Count > 0 ? string.Join($" {LogicOperator} ", parts) : "No filters";
        }

        /// <summary>
        /// Clone this filter criteria
        /// </summary>
        public FilterCriteria Clone()
        {
            return new FilterCriteria
            {
                NamePattern = this.NamePattern,
                StatusFilter = this.StatusFilter,
                OSFilter = this.OSFilter,
                OUFilter = this.OUFilter,
                MinRamGB = this.MinRamGB,
                MaxRamGB = this.MaxRamGB,
                LastSeenAfter = this.LastSeenAfter,
                LastSeenBefore = this.LastSeenBefore,
                LogicOperator = this.LogicOperator
            };
        }

        #endregion

        #region INotifyPropertyChanged

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    // TAG: #AUTO_UPDATE_UI_ENGINE #FILTER_SYSTEM #PRESET_MANAGEMENT
    /// <summary>
    /// Saved filter preset for quick reuse
    /// </summary>
    public class FilterPreset
    {
        /// <summary>
        /// Unique identifier
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// User-friendly preset name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Optional description
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Filter criteria
        /// </summary>
        public FilterCriteria Criteria { get; set; }

        /// <summary>
        /// Creation timestamp
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Last modified timestamp
        /// </summary>
        public DateTime ModifiedAt { get; set; }

        /// <summary>
        /// Is this a built-in preset (cannot be deleted)
        /// </summary>
        public bool IsBuiltIn { get; set; }

        public FilterPreset()
        {
            Id = Guid.NewGuid().ToString();
            CreatedAt = DateTime.Now;
            ModifiedAt = DateTime.Now;
            Criteria = new FilterCriteria();
        }

        public FilterPreset(string name, FilterCriteria criteria, bool isBuiltIn = false)
        {
            Id = Guid.NewGuid().ToString();
            Name = name;
            Criteria = criteria;
            CreatedAt = DateTime.Now;
            ModifiedAt = DateTime.Now;
            IsBuiltIn = isBuiltIn;
        }
    }

    // TAG: #AUTO_UPDATE_UI_ENGINE #FILTER_SYSTEM #HISTORY_TRACKING
    /// <summary>
    /// Filter history entry for recently used filters
    /// </summary>
    public class FilterHistoryEntry
    {
        /// <summary>
        /// Filter criteria used
        /// </summary>
        public FilterCriteria Criteria { get; set; }

        /// <summary>
        /// Timestamp when filter was applied
        /// </summary>
        public DateTime AppliedAt { get; set; }

        /// <summary>
        /// Number of results returned
        /// </summary>
        public int ResultCount { get; set; }

        public FilterHistoryEntry(FilterCriteria criteria, int resultCount)
        {
            Criteria = criteria.Clone();
            AppliedAt = DateTime.Now;
            ResultCount = resultCount;
        }
    }
}
