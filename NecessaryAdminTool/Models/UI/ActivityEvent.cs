using System;

namespace NecessaryAdminTool.Models.UI
{
    // TAG: #AUTO_UPDATE_UI_ENGINE #ACTIVITY_FEED #DASHBOARD
    /// <summary>
    /// Represents a single event in the activity feed timeline.
    /// </summary>
    public class ActivityEvent
    {
        public DateTime Timestamp { get; set; }
        public ActivitySeverity Severity { get; set; }
        public string Message { get; set; }
        public string Source { get; set; }

        public string TimestampDisplay => Timestamp.ToString("HH:mm:ss");
        public string DateDisplay => Timestamp.ToString("MMM dd");

        public string SeverityIcon => Severity switch
        {
            ActivitySeverity.Success => "\u2713",
            ActivitySeverity.Info => "\u2139",
            ActivitySeverity.Warning => "\u26A0",
            ActivitySeverity.Error => "\u2717",
            _ => "\u2022"
        };

        public string SeverityColor => Severity switch
        {
            ActivitySeverity.Success => "#FF10B981",
            ActivitySeverity.Info => "#FF3B82F6",
            ActivitySeverity.Warning => "#FFF59E0B",
            ActivitySeverity.Error => "#FFEF4444",
            _ => "#FF6B7280"
        };
    }

    public enum ActivitySeverity
    {
        Success,
        Info,
        Warning,
        Error
    }
}
