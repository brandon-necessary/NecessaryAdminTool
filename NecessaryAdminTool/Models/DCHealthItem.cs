// TAG: #DC_HEALTH #DASHBOARD #UI_MODEL #VERSION_2_1
using System.ComponentModel;

namespace NecessaryAdminTool.Models
{
    /// <summary>
    /// Represents a Domain Controller health status item for dashboard display
    /// </summary>
    public class DCHealthItem : INotifyPropertyChanged
    {
        private string _hostname;
        private int _avgLatency;
        private string _healthIcon;
        private string _latencyColor;
        private string _latencyBg;
        private string _favoriteIcon = "☆";

        public string Hostname
        {
            get => _hostname;
            set { _hostname = value; OnPropertyChanged(nameof(Hostname)); }
        }

        public int AvgLatency
        {
            get => _avgLatency;
            set { _avgLatency = value; OnPropertyChanged(nameof(AvgLatency)); }
        }

        public string HealthIcon
        {
            get => _healthIcon;
            set { _healthIcon = value; OnPropertyChanged(nameof(HealthIcon)); }
        }

        public string LatencyColor
        {
            get => _latencyColor;
            set { _latencyColor = value; OnPropertyChanged(nameof(LatencyColor)); }
        }

        public string LatencyBg
        {
            get => _latencyBg;
            set { _latencyBg = value; OnPropertyChanged(nameof(LatencyBg)); }
        }

        public string FavoriteIcon
        {
            get => _favoriteIcon;
            set { _favoriteIcon = value; OnPropertyChanged(nameof(FavoriteIcon)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
