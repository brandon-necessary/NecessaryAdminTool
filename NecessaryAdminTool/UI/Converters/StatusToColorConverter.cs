using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace NecessaryAdminTool.UI.Converters
{
    /// <summary>
    /// Converts computer status to semantic color
    /// TAG: #AUTO_UPDATE_UI_ENGINE #VALUE_CONVERTER #SEMANTIC_COLORS #MODULAR
    /// </summary>
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return new SolidColorBrush(Color.FromRgb(107, 114, 128)); // Muted gray

            string status = value.ToString().ToLower();

            return status switch
            {
                "online" => new SolidColorBrush(Color.FromRgb(16, 185, 129)),    // Success green
                "offline" => new SolidColorBrush(Color.FromRgb(239, 68, 68)),     // Error red
                "warning" => new SolidColorBrush(Color.FromRgb(245, 158, 11)),    // Warning amber
                "unknown" => new SolidColorBrush(Color.FromRgb(107, 114, 128)),   // Muted gray
                _ => new SolidColorBrush(Color.FromRgb(59, 130, 246))             // Info blue
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts status to status text with emoji
    /// TAG: #VALUE_CONVERTER #STATUS_DISPLAY
    /// </summary>
    public class StatusToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return "⚪ Unknown";

            string status = value.ToString().ToLower();

            return status switch
            {
                "online" => "🟢 Online",
                "offline" => "🔴 Offline",
                "warning" => "🟡 Warning",
                "unknown" => "⚪ Unknown",
                _ => $"⚪ {value}"
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts boolean to Visibility
    /// TAG: #VALUE_CONVERTER #VISIBILITY
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return boolValue ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

            return System.Windows.Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is System.Windows.Visibility visibility)
                return visibility == System.Windows.Visibility.Visible;

            return false;
        }
    }

    /// <summary>
    /// Inverts boolean to Visibility
    /// TAG: #VALUE_CONVERTER #VISIBILITY #INVERTED
    /// </summary>
    public class InvertedBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return boolValue ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;

            return System.Windows.Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is System.Windows.Visibility visibility)
                return visibility == System.Windows.Visibility.Collapsed;

            return true;
        }
    }
}
