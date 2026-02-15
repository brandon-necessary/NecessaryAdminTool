using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace NecessaryAdminTool.Models
{
    /// <summary>
    /// Hierarchical tree node model for Active Directory objects (OUs, Computers, Groups)
    /// TAG: #AUTO_UPDATE_UI_ENGINE #AD_TREE_VIEW #NATIVE_WPF #MODELS
    /// </summary>
    public class ADTreeNode : INotifyPropertyChanged
    {
        private bool _isExpanded;
        private bool _isSelected;
        private ObservableCollection<ADTreeNode> _children;
        private bool _isLoaded;
        private int _onlineCount;
        private int _offlineCount;

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Node type enumeration
        /// </summary>
        public enum NodeType
        {
            Root,
            OrganizationalUnit,
            Computer,
            Group,
            User
        }

        public ADTreeNode(string name, NodeType type, string distinguishedName = null)
        {
            Name = name;
            Type = type;
            DistinguishedName = distinguishedName ?? name;
            Children = new ObservableCollection<ADTreeNode>();
            _isLoaded = false;

            // Set icon based on type
            Icon = GetIconForType(type);
        }

        #region Properties

        /// <summary>
        /// Display name of the node
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Active Directory Distinguished Name
        /// </summary>
        public string DistinguishedName { get; set; }

        /// <summary>
        /// Node type (OU, Computer, Group, User)
        /// </summary>
        public NodeType Type { get; set; }

        /// <summary>
        /// Icon character from Segoe MDL2 Assets font
        /// </summary>
        public string Icon { get; set; }

        /// <summary>
        /// Child nodes collection
        /// </summary>
        public ObservableCollection<ADTreeNode> Children
        {
            get => _children;
            set
            {
                _children = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Is node expanded in tree view
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();

                    // TAG: #AUTO_UPDATE_UI_ENGINE #LAZY_LOADING
                    // Lazy load children when expanded for first time
                    if (value && !_isLoaded)
                    {
                        LoadChildren();
                    }
                }
            }
        }

        /// <summary>
        /// Is node selected in tree view
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Number of online computers under this node
        /// </summary>
        public int OnlineCount
        {
            get => _onlineCount;
            set
            {
                _onlineCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusColor));
            }
        }

        /// <summary>
        /// Number of offline computers under this node
        /// </summary>
        public int OfflineCount
        {
            get => _offlineCount;
            set
            {
                _offlineCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusColor));
            }
        }

        /// <summary>
        /// Status text for display (e.g., "5 online, 2 offline")
        /// </summary>
        public string StatusText
        {
            get
            {
                if (Type == NodeType.Computer)
                    return string.Empty;

                int total = OnlineCount + OfflineCount;
                if (total == 0)
                    return string.Empty;

                return $"{OnlineCount} online, {OfflineCount} offline";
            }
        }

        /// <summary>
        /// Status color based on online/offline ratio
        /// </summary>
        public SolidColorBrush StatusColor
        {
            get
            {
                int total = OnlineCount + OfflineCount;
                if (total == 0)
                    return new SolidColorBrush(Colors.Gray);

                double onlinePercent = (double)OnlineCount / total;

                if (onlinePercent >= 0.9)
                    return new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Success green
                else if (onlinePercent >= 0.5)
                    return new SolidColorBrush(Color.FromRgb(245, 158, 11)); // Warning amber
                else
                    return new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Error red
            }
        }

        /// <summary>
        /// Computer status (Online/Offline) - only for Computer nodes
        /// </summary>
        public string ComputerStatus { get; set; }

        /// <summary>
        /// Computer IP address - only for Computer nodes
        /// </summary>
        public string IPAddress { get; set; }

        /// <summary>
        /// Computer OS version - only for Computer nodes
        /// </summary>
        public string OSVersion { get; set; }

        #endregion

        #region Methods

        /// <summary>
        /// Get icon based on node type
        /// TAG: #AUTO_UPDATE_UI_ENGINE #ICONS
        /// </summary>
        private string GetIconForType(NodeType type)
        {
            return type switch
            {
                NodeType.Root => "\uE8F4",                    // Home icon
                NodeType.OrganizationalUnit => "\uE8B7",     // Folder icon
                NodeType.Computer => "\uE977",                // Computer icon
                NodeType.Group => "\uE902",                   // Group icon
                NodeType.User => "\uE77B",                    // Person icon
                _ => "\uE8F4"
            };
        }

        /// <summary>
        /// Load child nodes from Active Directory
        /// TAG: #AUTO_UPDATE_UI_ENGINE #AD_INTEGRATION #LAZY_LOADING
        /// </summary>
        public async void LoadChildren()
        {
            if (_isLoaded)
                return;

            try
            {
                // Only load children for OUs and Root
                if (Type != NodeType.OrganizationalUnit && Type != NodeType.Root)
                    return;

                // TAG: #AUTO_UPDATE_UI_ENGINE #ASYNC_LOADING
                // Load in background to avoid UI freeze
                await System.Threading.Tasks.Task.Run(() =>
                {
                    // This will be implemented to call ActiveDirectoryManager
                    // For now, just mark as loaded
                    _isLoaded = true;
                });

                // TODO: Implement actual AD loading
                // var childOUs = ActiveDirectoryManager.GetChildOUs(this.DistinguishedName);
                // var computers = ActiveDirectoryManager.GetComputersInOU(this.DistinguishedName);
                //
                // foreach (var ou in childOUs)
                //     Children.Add(new ADTreeNode(ou.Name, NodeType.OrganizationalUnit, ou.DistinguishedName));
                //
                // foreach (var computer in computers)
                //     Children.Add(new ADTreeNode(computer.Name, NodeType.Computer, computer.DistinguishedName));
            }
            catch (Exception ex)
            {
                // TAG: #AUTO_UPDATE_UI_ENGINE #ERROR_HANDLING #TOAST_NOTIFICATIONS
                Managers.UI.ToastManager.ShowError($"Failed to load children: {ex.Message}");
                LogManager.LogError("ADTreeNode.LoadChildren", ex);
            }
        }

        /// <summary>
        /// Refresh node status (online/offline counts)
        /// TAG: #AUTO_UPDATE_UI_ENGINE #STATUS_UPDATE
        /// </summary>
        public void RefreshStatus()
        {
            // Recalculate online/offline counts from children
            OnlineCount = 0;
            OfflineCount = 0;

            foreach (var child in Children)
            {
                if (child.Type == NodeType.Computer)
                {
                    if (child.ComputerStatus == "Online")
                        OnlineCount++;
                    else if (child.ComputerStatus == "Offline")
                        OfflineCount++;
                }
                else
                {
                    OnlineCount += child.OnlineCount;
                    OfflineCount += child.OfflineCount;
                }
            }
        }

        #endregion

        #region INotifyPropertyChanged

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
