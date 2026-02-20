using System.Collections.Generic;
using System.Windows.Media;

namespace NecessaryAdminTool.Models.UI
{
    // TAG: #AUTO_UPDATE_UI_ENGINE #KPI_CARD #DASHBOARD
    /// <summary>
    /// Data model for KPI dashboard cards with sparkline trend data.
    /// </summary>
    public class KpiCardData
    {
        public string Label { get; set; }
        public string Icon { get; set; }
        public double Value { get; set; }
        public double PreviousValue { get; set; }
        public string DeltaText { get; set; }
        public bool DeltaIsPositive { get; set; }
        public string Subtitle { get; set; }
        public Brush AccentBrush { get; set; }
        public List<double> SparklineData { get; set; } = new List<double>();
        public string FormatString { get; set; } = "N0";
    }
}
