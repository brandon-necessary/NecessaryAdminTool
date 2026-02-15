using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Globalization;
using NecessaryAdminTool.Models;

namespace NecessaryAdminTool.UI.Components
{
    /// <summary>
    /// Active Directory Tree View control for hierarchical OU/Computer navigation
    /// TAG: #AUTO_UPDATE_UI_ENGINE #AD_TREE_VIEW #NATIVE_WPF #USER_CONTROL
    /// </summary>
    public partial class ADTreeView : UserControl
    {
        private ADTreeNode _rootNode;
        private List<ADTreeNode> _allNodes;

        // Event fired when a node is selected
        public event EventHandler<NodeSelectedEventArgs> NodeSelected;

        public ADTreeView()
        {
            InitializeComponent();
            _allNodes = new List<ADTreeNode>();

            Loaded += ADTreeView_Loaded;
        }

        /// <summary>
        /// Initialize tree when control loads
        /// TAG: #AUTO_UPDATE_UI_ENGINE #INITIALIZATION
        /// </summary>
        private void ADTreeView_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // TAG: #AUTO_UPDATE_UI_ENGINE #TOAST_NOTIFICATIONS
                Managers.UI.ToastManager.ShowInfo("Loading Active Directory tree...");
                LoadADTree();
            }
            catch (Exception ex)
            {
                // TAG: #AUTO_UPDATE_UI_ENGINE #ERROR_HANDLING #TOAST_NOTIFICATIONS
                Managers.UI.ToastManager.ShowError($"Failed to load AD tree: {ex.Message}");
                LogManager.LogError("ADTreeView.Loaded", ex);
            }
        }

        /// <summary>
        /// Load Active Directory tree structure
        /// TAG: #AUTO_UPDATE_UI_ENGINE #AD_INTEGRATION
        /// </summary>
        private void LoadADTree()
        {
            try
            {
                TxtStatus.Text = "Loading Active Directory...";

                // Create root node
                _rootNode = new ADTreeNode("Active Directory", ADTreeNode.NodeType.Root);

                // TAG: #AUTO_UPDATE_UI_ENGINE #AD_INTEGRATION
                // Load top-level OUs from Active Directory
                // This will be implemented to use ActiveDirectoryManager

                // For now, create a demo structure (will be replaced with real AD calls)
                LoadDemoStructure();

                // Set tree data source
                TreeAD.Items.Clear();
                TreeAD.Items.Add(_rootNode);

                // Expand root by default
                _rootNode.IsExpanded = true;

                UpdateStatusCounts();
                TxtStatus.Text = "Active Directory loaded successfully";

                // TAG: #AUTO_UPDATE_UI_ENGINE #TOAST_NOTIFICATIONS
                Managers.UI.ToastManager.ShowSuccess("AD tree loaded successfully");
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "Failed to load Active Directory";

                // TAG: #AUTO_UPDATE_UI_ENGINE #ERROR_HANDLING #TOAST_NOTIFICATIONS
                Managers.UI.ToastManager.ShowError($"AD load failed: {ex.Message}");
                LogManager.LogError("ADTreeView.LoadADTree", ex);
            }
        }

        /// <summary>
        /// Load demo structure for testing (will be replaced with real AD integration)
        /// TAG: #AUTO_UPDATE_UI_ENGINE #DEMO_DATA #TODO_REPLACE_WITH_AD
        /// </summary>
        private void LoadDemoStructure()
        {
            // Create sample OU structure
            var corp = new ADTreeNode("Corporate", ADTreeNode.NodeType.OrganizationalUnit, "OU=Corporate,DC=company,DC=com");
            var it = new ADTreeNode("IT Department", ADTreeNode.NodeType.OrganizationalUnit, "OU=IT,OU=Corporate,DC=company,DC=com");
            var hr = new ADTreeNode("HR Department", ADTreeNode.NodeType.OrganizationalUnit, "OU=HR,OU=Corporate,DC=company,DC=com");

            // Add sample computers
            var pc1 = new ADTreeNode("IT-PC-001", ADTreeNode.NodeType.Computer, "CN=IT-PC-001,OU=IT,OU=Corporate,DC=company,DC=com")
            {
                ComputerStatus = "Online",
                IPAddress = "192.168.1.100",
                OSVersion = "Windows 11 Pro"
            };

            var pc2 = new ADTreeNode("IT-PC-002", ADTreeNode.NodeType.Computer, "CN=IT-PC-002,OU=IT,OU=Corporate,DC=company,DC=com")
            {
                ComputerStatus = "Offline",
                IPAddress = "192.168.1.101",
                OSVersion = "Windows 10 Pro"
            };

            var pc3 = new ADTreeNode("HR-PC-001", ADTreeNode.NodeType.Computer, "CN=HR-PC-001,OU=HR,OU=Corporate,DC=company,DC=com")
            {
                ComputerStatus = "Online",
                IPAddress = "192.168.1.150",
                OSVersion = "Windows 11 Pro"
            };

            // Build hierarchy
            it.Children.Add(pc1);
            it.Children.Add(pc2);
            hr.Children.Add(pc3);

            corp.Children.Add(it);
            corp.Children.Add(hr);

            _rootNode.Children.Add(corp);

            // Update status counts
            it.OnlineCount = 1;
            it.OfflineCount = 1;
            hr.OnlineCount = 1;
            hr.OfflineCount = 0;
            corp.OnlineCount = 2;
            corp.OfflineCount = 1;

            // Track all nodes for searching
            _allNodes.Add(_rootNode);
            _allNodes.Add(corp);
            _allNodes.Add(it);
            _allNodes.Add(hr);
            _allNodes.Add(pc1);
            _allNodes.Add(pc2);
            _allNodes.Add(pc3);
        }

        /// <summary>
        /// Search/filter tree based on text input
        /// TAG: #AUTO_UPDATE_UI_ENGINE #SEARCH_FILTER
        /// </summary>
        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                string searchText = TxtSearch.Text?.Trim().ToLower();

                if (string.IsNullOrWhiteSpace(searchText))
                {
                    // Show all nodes
                    RestoreTreeVisibility(_rootNode);
                    TxtStatus.Text = "Ready";
                    return;
                }

                // Hide all nodes first
                CollapseAll(_rootNode);

                // Find and show matching nodes
                int matchCount = 0;
                foreach (var node in _allNodes)
                {
                    if (node.Name.ToLower().Contains(searchText))
                    {
                        // Show this node and all its parents
                        ShowNodeAndParents(node);
                        matchCount++;
                    }
                }

                TxtStatus.Text = $"Found {matchCount} matching item(s)";
            }
            catch (Exception ex)
            {
                LogManager.LogError("ADTreeView.TxtSearch_TextChanged", ex);
            }
        }

        /// <summary>
        /// Show a node and all its parent nodes
        /// </summary>
        private void ShowNodeAndParents(ADTreeNode node)
        {
            // Find parent chain and expand all
            var current = FindParentNode(_rootNode, node);
            while (current != null)
            {
                current.IsExpanded = true;
                current = FindParentNode(_rootNode, current);
            }

            node.IsExpanded = true;
        }

        /// <summary>
        /// Find parent of a given node
        /// </summary>
        private ADTreeNode FindParentNode(ADTreeNode root, ADTreeNode target)
        {
            if (root.Children.Contains(target))
                return root;

            foreach (var child in root.Children)
            {
                var parent = FindParentNode(child, target);
                if (parent != null)
                    return parent;
            }

            return null;
        }

        /// <summary>
        /// Restore tree visibility after search
        /// </summary>
        private void RestoreTreeVisibility(ADTreeNode node)
        {
            node.IsExpanded = false;
            foreach (var child in node.Children)
            {
                RestoreTreeVisibility(child);
            }
        }

        /// <summary>
        /// Expand all tree nodes
        /// TAG: #AUTO_UPDATE_UI_ENGINE #TREE_OPERATIONS
        /// </summary>
        private void BtnExpandAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ExpandAll(_rootNode);
                TxtStatus.Text = "All nodes expanded";

                // TAG: #AUTO_UPDATE_UI_ENGINE #TOAST_NOTIFICATIONS
                Managers.UI.ToastManager.ShowInfo("All nodes expanded");
            }
            catch (Exception ex)
            {
                LogManager.LogError("ADTreeView.BtnExpandAll_Click", ex);
            }
        }

        /// <summary>
        /// Collapse all tree nodes
        /// TAG: #AUTO_UPDATE_UI_ENGINE #TREE_OPERATIONS
        /// </summary>
        private void BtnCollapseAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CollapseAll(_rootNode);
                _rootNode.IsExpanded = true; // Keep root expanded
                TxtStatus.Text = "All nodes collapsed";

                // TAG: #AUTO_UPDATE_UI_ENGINE #TOAST_NOTIFICATIONS
                Managers.UI.ToastManager.ShowInfo("All nodes collapsed");
            }
            catch (Exception ex)
            {
                LogManager.LogError("ADTreeView.BtnCollapseAll_Click", ex);
            }
        }

        /// <summary>
        /// Refresh tree from Active Directory
        /// TAG: #AUTO_UPDATE_UI_ENGINE #REFRESH
        /// </summary>
        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TxtStatus.Text = "Refreshing...";

                // TAG: #AUTO_UPDATE_UI_ENGINE #TOAST_NOTIFICATIONS
                Managers.UI.ToastManager.ShowInfo("Refreshing AD tree...");

                LoadADTree();
            }
            catch (Exception ex)
            {
                // TAG: #AUTO_UPDATE_UI_ENGINE #ERROR_HANDLING #TOAST_NOTIFICATIONS
                Managers.UI.ToastManager.ShowError($"Refresh failed: {ex.Message}");
                LogManager.LogError("ADTreeView.BtnRefresh_Click", ex);
            }
        }

        /// <summary>
        /// Handle context menu actions
        /// TAG: #AUTO_UPDATE_UI_ENGINE #CONTEXT_MENU
        /// </summary>
        private void ContextMenu_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuItem menuItem && TreeAD.SelectedItem is ADTreeNode selectedNode)
                {
                    string action = menuItem.Tag?.ToString();

                    switch (action)
                    {
                        case "RDP":
                            // TAG: #AUTO_UPDATE_UI_ENGINE #TOAST_NOTIFICATIONS
                            Managers.UI.ToastManager.ShowInfo($"Launching RDP to {selectedNode.Name}...");
                            // TODO: Implement RDP launch
                            break;

                        case "PowerShell":
                            Managers.UI.ToastManager.ShowInfo($"Opening PowerShell to {selectedNode.Name}...");
                            // TODO: Implement PowerShell remote
                            break;

                        case "Services":
                            Managers.UI.ToastManager.ShowInfo($"Opening Services for {selectedNode.Name}...");
                            // TODO: Implement services manager
                            break;

                        case "Scan":
                            Managers.UI.ToastManager.ShowInfo($"Scanning {selectedNode.Name}...");
                            // TODO: Implement computer scan
                            break;

                        case "Refresh":
                            selectedNode.RefreshStatus();
                            UpdateStatusCounts();
                            Managers.UI.ToastManager.ShowSuccess("Status refreshed");
                            break;

                        case "ScanOU":
                            Managers.UI.ToastManager.ShowInfo($"Scanning all computers in {selectedNode.Name}...");
                            // TODO: Implement OU scan
                            break;

                        case "Export":
                            Managers.UI.ToastManager.ShowInfo($"Exporting computer list from {selectedNode.Name}...");
                            // TODO: Implement export
                            break;
                    }

                    // Fire NodeSelected event
                    NodeSelected?.Invoke(this, new NodeSelectedEventArgs { SelectedNode = selectedNode, Action = action });
                }
            }
            catch (Exception ex)
            {
                // TAG: #AUTO_UPDATE_UI_ENGINE #ERROR_HANDLING #TOAST_NOTIFICATIONS
                Managers.UI.ToastManager.ShowError($"Action failed: {ex.Message}");
                LogManager.LogError("ADTreeView.ContextMenu_Click", ex);
            }
        }

        /// <summary>
        /// Recursively expand all nodes
        /// </summary>
        private void ExpandAll(ADTreeNode node)
        {
            node.IsExpanded = true;
            foreach (var child in node.Children)
            {
                ExpandAll(child);
            }
        }

        /// <summary>
        /// Recursively collapse all nodes
        /// </summary>
        private void CollapseAll(ADTreeNode node)
        {
            node.IsExpanded = false;
            foreach (var child in node.Children)
            {
                CollapseAll(child);
            }
        }

        /// <summary>
        /// Update online/offline status counts
        /// TAG: #AUTO_UPDATE_UI_ENGINE #STATUS_UPDATE
        /// </summary>
        private void UpdateStatusCounts()
        {
            int totalOnline = _rootNode.OnlineCount;
            int totalOffline = _rootNode.OfflineCount;

            TxtOnlineCount.Text = totalOnline.ToString();
            TxtOfflineCount.Text = totalOffline.ToString();
        }

        /// <summary>
        /// Get currently selected node
        /// </summary>
        public ADTreeNode GetSelectedNode()
        {
            return TreeAD.SelectedItem as ADTreeNode;
        }

        /// <summary>
        /// Set selected node programmatically
        /// </summary>
        public void SetSelectedNode(ADTreeNode node)
        {
            if (node != null)
            {
                node.IsSelected = true;
            }
        }
    }

    /// <summary>
    /// Event args for node selection
    /// TAG: #AUTO_UPDATE_UI_ENGINE #EVENTS
    /// </summary>
    public class NodeSelectedEventArgs : EventArgs
    {
        public ADTreeNode SelectedNode { get; set; }
        public string Action { get; set; }
    }

    /// <summary>
    /// String to Visibility converter for XAML binding
    /// TAG: #AUTO_UPDATE_UI_ENGINE #CONVERTERS
    /// </summary>
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.IsNullOrWhiteSpace(value?.ToString()) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
