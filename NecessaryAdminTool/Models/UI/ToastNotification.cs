using System;

namespace NecessaryAdminTool.Models.UI
{
    /// <summary>
    /// Model for toast notifications
    /// TAG: #TOAST_NOTIFICATIONS #MODULAR #UI_MODELS
    /// Research: https://blog.logrocket.com/ux-design/toast-notifications/
    /// </summary>
    public class ToastNotification
    {
        public ToastType Type { get; set; }
        public string Message { get; set; }
        public string ActionText { get; set; }
        public Action ActionCallback { get; set; }
        public int Duration { get; set; }

        public ToastNotification()
        {
            Duration = 4000; // Default 4 seconds
        }

        public ToastNotification(string message, ToastType type = ToastType.Info)
        {
            Message = message;
            Type = type;
            Duration = CalculateDuration(message);
        }

        public bool HasAction => !string.IsNullOrEmpty(ActionText) && ActionCallback != null;

        /// <summary>
        /// Calculate auto-dismiss duration based on message length
        /// Rule: 500ms per word + 1000ms buffer (max 10s)
        /// TAG: #UX_BEST_PRACTICE
        /// </summary>
        private int CalculateDuration(string message)
        {
            if (string.IsNullOrEmpty(message))
                return 4000;

            int wordCount = message.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
            int duration = (wordCount * 500) + 1000;

            // Cap at 10 seconds max
            return Math.Min(duration, 10000);
        }
    }

    /// <summary>
    /// Toast notification types with semantic colors
    /// TAG: #SEMANTIC_COLORS
    /// </summary>
    public enum ToastType
    {
        Success,  // Green #10B981
        Info,     // Blue #3B82F6
        Warning,  // Amber #F59E0B
        Error     // Red #EF4444
    }
}
