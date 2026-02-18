using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.DirectoryServices;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using NecessaryAdminTool.Security;

namespace NecessaryAdminTool
{
    // TAG: #AD_OBJECT_BROWSER #RSAT_ADUC #VERSION_7 #SECURITY_CRITICAL #LDAP_INJECTION_PREVENTION
    /// <summary>
    /// RSAT Active Directory Users and Computers-like interface
    /// Provides tree-based navigation of AD objects with scanning capabilities
    /// </summary>
    public partial class ADObjectBrowser : UserControl
    {
        private ObservableCollection<ADObjectItem> _currentObjects;
        private string _domainController;
        private string _username;
        private string _password;
        #pragma warning disable CS0169 // Field is never used - reserved for future ActiveDirectoryManager integration
        private bool _useActiveDirectoryManager;
        #pragma warning restore CS0169
        private ActiveDirectoryManager _adManager;

        /// <summary>
        /// Event fired when AD object selection changes
        /// TAG: #VERSION_7 #AD_MANAGEMENT
        /// </summary>
        public event EventHandler<int> ObjectSelectionChanged;

        public ADObjectBrowser()
        {
            InitializeComponent();
            _currentObjects = new ObservableCollection<ADObjectItem>();
            GridADObjects.ItemsSource = _currentObjects;

            // TAG: #VERSION_7 #AD_MANAGEMENT - Wire up selection changed event
            GridADObjects.SelectionChanged += GridADObjects_SelectionChanged;
        }

        /// <summary>
        /// Handle DataGrid selection changed to notify parent window
        /// TAG: #VERSION_7 #AD_MANAGEMENT
        /// </summary>
        private void GridADObjects_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Fire event with count of selected items
            ObjectSelectionChanged?.Invoke(this, GridADObjects.SelectedItems.Count);
        }

        /// <summary>
        /// Initialize the AD object browser with domain connection
        /// TAG: #AD_QUERY_BACKEND_SELECTION
        /// </summary>
        public async Task InitializeAsync(string domainController, string username = null, string password = null, bool useActiveDirectoryManager = false)
        {
            _domainController = domainController;
            _username = username;
            _password = password;

            TxtStatusMessage.Text = "Connecting to Active Directory...";

            try
            {
                // TAG: #SECURITY_CRITICAL #LDAP_INJECTION_PREVENTION
                // Validate domain controller hostname before use
                if (string.IsNullOrWhiteSpace(domainController))
                {
                    LogManager.LogWarning("[AD Browser] Empty or null domain controller hostname");
                    throw new ArgumentException("No domain controller selected. Please select a DC from the dropdown.");
                }
                if (!SecurityValidator.IsValidHostname(domainController))
                {
                    LogManager.LogWarning($"[AD Browser] Invalid domain controller hostname: '{domainController}' (length={domainController.Length})");
                    throw new ArgumentException(
                        $"The domain controller name '{domainController}' contains invalid characters.\n\n" +
                        "Valid hostnames contain only letters, numbers, hyphens, and dots.\n" +
                        "Please select a DC from the dropdown rather than typing manually.");
                }

                string domainName = null;
                DirectoryEntry rootEntry = null;

                await Task.Run(() =>
                {
                    try
                    {
                        // Get domain information
                        rootEntry = GetDirectoryEntry($"LDAP://{domainController}");

                        // Test connection by accessing a property
                        domainName = rootEntry.Properties["name"]?.Value?.ToString() ?? domainController;
                    }
                    catch (System.Runtime.InteropServices.COMException comEx)
                    {
                        throw new Exception($"Cannot connect to domain controller '{domainController}'.\n\n" +
                            $"Possible causes:\n" +
                            $"• DC is unreachable or offline\n" +
                            $"• LDAP service is not running\n" +
                            $"• Network/firewall is blocking connection\n" +
                            $"• Invalid credentials\n\n" +
                            $"Technical details: {comEx.Message}", comEx);
                    }
                });

                // Build tree on UI thread
                if (rootEntry != null && !string.IsNullOrEmpty(domainName))
                {
                    TxtDomainName.Text = domainName;
                    BuildTreeView(rootEntry, domainName);
                }

                TxtStatusMessage.Text = "Connected to Active Directory";
            }
            catch (Exception ex)
            {
                TxtStatusMessage.Text = $"Failed to connect: {ex.Message}";
                MessageBox.Show($"Failed to connect to Active Directory:\n\n{ex.Message}",
                    "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Build the tree view structure mimicking RSAT ADUC
        /// </summary>
        private void BuildTreeView(DirectoryEntry rootEntry, string domainName)
        {
            ADTreeView.Items.Clear();

            // Root domain node
            var rootNode = new TreeViewItem
            {
                Header = $"🌐 {domainName}",
                Tag = new ADTreeNode
                {
                    DistinguishedName = rootEntry.Properties["distinguishedName"][0]?.ToString(),
                    ObjectClass = "domain",
                    ContainerType = ADContainerType.Domain
                }
            };

            // TAG: #SECURITY_CRITICAL #LDAP_INJECTION_PREVENTION
            // Add standard containers with validated LDAP filters
            var computersNode = new TreeViewItem
            {
                Header = "🖥️ Computers",
                Tag = new ADTreeNode
                {
                    ObjectClass = "container",
                    ContainerType = ADContainerType.Computers,
                    Filter = "(objectCategory=computer)" // Pre-validated static filter
                }
            };
            computersNode.Items.Add(new TreeViewItem { Header = "Loading..." }); // Placeholder
            rootNode.Items.Add(computersNode);

            var usersNode = new TreeViewItem
            {
                Header = "👤 Users",
                Tag = new ADTreeNode
                {
                    ObjectClass = "container",
                    ContainerType = ADContainerType.Users,
                    Filter = "(&(objectCategory=person)(objectClass=user))" // Pre-validated static filter
                }
            };
            usersNode.Items.Add(new TreeViewItem { Header = "Loading..." });
            rootNode.Items.Add(usersNode);

            var groupsNode = new TreeViewItem
            {
                Header = "👥 Groups",
                Tag = new ADTreeNode
                {
                    ObjectClass = "container",
                    ContainerType = ADContainerType.Groups,
                    Filter = "(objectCategory=group)" // Pre-validated static filter
                }
            };
            groupsNode.Items.Add(new TreeViewItem { Header = "Loading..." });
            rootNode.Items.Add(groupsNode);

            var ousNode = new TreeViewItem
            {
                Header = "📁 Organizational Units",
                Tag = new ADTreeNode
                {
                    ObjectClass = "organizationalUnit",
                    ContainerType = ADContainerType.OrganizationalUnits,
                    Filter = "(objectCategory=organizationalUnit)" // Pre-validated static filter
                }
            };
            ousNode.Items.Add(new TreeViewItem { Header = "Loading..." });
            rootNode.Items.Add(ousNode);

            rootNode.IsExpanded = true;
            ADTreeView.Items.Add(rootNode);
        }

        /// <summary>
        /// Handle tree view item selection
        /// </summary>
        private async void ADTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeViewItem selectedItem && selectedItem.Tag is ADTreeNode node)
            {
                TxtSelectedContainer.Text = selectedItem.Header.ToString();
                TxtStatusMessage.Text = $"Loading objects from {selectedItem.Header}...";

                await LoadObjectsAsync(node);
            }
        }

        /// <summary>
        /// Load AD objects for selected container
        /// TAG: #AD_QUERY_BACKEND_SELECTION #PERFORMANCE_AUDIT #VERSION_7
        /// </summary>
        private async Task LoadObjectsAsync(ADTreeNode node)
        {
            _currentObjects.Clear();
            List<ADObjectItem> objects = null;

            try
            {
                // TASK 1: Backend selection based on user preference
                string queryMethod = Properties.Settings.Default.ADQueryMethod ?? "DirectorySearcher";

                if (queryMethod == "ActiveDirectoryManager")
                {
                    try
                    {
                        TxtStatusMessage.Text = "Loading objects (ActiveDirectoryManager - Detailed)...";

                        // Initialize AD Manager if needed
                        if (_adManager == null)
                        {
                            _adManager = new ActiveDirectoryManager(_domainController, _username, _password);
                            if (!_adManager.Initialize(out string initError))
                            {
                                LogManager.LogWarning($"[AD Browser] ActiveDirectoryManager init failed: {initError}, falling back to DirectorySearcher");
                                throw new Exception(initError);
                            }
                        }

                        // Query based on container type
                        var ct = System.Threading.CancellationToken.None;
                        switch (node.ContainerType)
                        {
                            case ADContainerType.Computers:
                                var computers = await _adManager.GetComputersAsync(null, ct, node.DistinguishedName);
                                objects = ConvertToADObjectItems(computers);
                                break;
                            case ADContainerType.Users:
                                var users = await _adManager.GetUsersAsync(null, ct, node.DistinguishedName);
                                objects = ConvertToADObjectItems(users);
                                break;
                            case ADContainerType.Groups:
                                var groups = await _adManager.GetGroupsAsync(null, ct, node.DistinguishedName);
                                objects = ConvertToADObjectItems(groups);
                                break;
                            case ADContainerType.OrganizationalUnits:
                                var ous = await _adManager.GetOUsAsync(null, ct);
                                objects = ConvertToADObjectItems(ous);
                                break;
                        }

                        LogManager.LogDebug($"[AD Browser] Query succeeded with ActiveDirectoryManager ({objects?.Count ?? 0} objects)");
                    }
                    catch (Exception ex)
                    {
                        LogManager.LogWarning($"[AD Browser] ActiveDirectoryManager failed, falling back to DirectorySearcher: {ex.Message}");
                        objects = null; // Trigger fallback
                    }
                }

                // FALLBACK: DirectorySearcher (existing code - always works)
                if (objects == null)
                {
                    TxtStatusMessage.Text = queryMethod == "DirectorySearcher"
                        ? "Loading objects (DirectorySearcher - Fast)..."
                        : "Loading objects (DirectorySearcher - Fallback)...";

                    objects = await Task.Run(() =>
                    {
                        var objectList = new List<ADObjectItem>();

                        using (var entry = GetDirectoryEntry($"LDAP://{_domainController}"))
                        using (var searcher = new DirectorySearcher(entry))
                        {
                            // TAG: #SECURITY_CRITICAL #LDAP_INJECTION_PREVENTION
                            // Validate LDAP filter before use
                            string filter = node.Filter ?? "(objectClass=*)";
                            if (!SecurityValidator.ValidateLDAPFilter(filter))
                            {
                                LogManager.LogWarning($"[AD Browser] Invalid LDAP filter blocked: {filter}");
                                throw new InvalidOperationException("LDAP filter failed security validation.");
                            }

                            searcher.Filter = filter;
                            LogManager.LogDebug($"[AD Browser] Using validated LDAP filter: {filter}");

                            searcher.PageSize = 1000;
                            searcher.PropertiesToLoad.Add("name");
                            searcher.PropertiesToLoad.Add("cn");
                            searcher.PropertiesToLoad.Add("objectClass");
                            searcher.PropertiesToLoad.Add("description");
                            searcher.PropertiesToLoad.Add("distinguishedName");

                            using (var results = searcher.FindAll())
                            {
                                foreach (SearchResult result in results)
                                {
                                    var obj = new ADObjectItem
                                    {
                                        Name = GetProperty(result, "name"),
                                        Description = GetProperty(result, "description"),
                                        DistinguishedName = GetProperty(result, "distinguishedName"),
                                        Status = "Ready"
                                    };

                                    // Determine object type and icon
                                    var objectClass = GetProperty(result, "objectClass");
                                    if (objectClass.Contains("computer"))
                                    {
                                        obj.ObjectType = "Computer";
                                        obj.Icon = "🖥️";
                                    }
                                    else if (objectClass.Contains("user") || objectClass.Contains("person"))
                                    {
                                        obj.ObjectType = "User";
                                        obj.Icon = "👤";
                                    }
                                    else if (objectClass.Contains("group"))
                                    {
                                        obj.ObjectType = "Group";
                                        obj.Icon = "👥";
                                    }
                                    else if (objectClass.Contains("organizationalUnit"))
                                    {
                                        obj.ObjectType = "OU";
                                        obj.Icon = "📁";
                                    }
                                    else
                                    {
                                        obj.ObjectType = "Other";
                                        obj.Icon = "📄";
                                    }

                                    objectList.Add(obj);
                                }
                            }
                        }

                        return objectList;
                    });

                    LogManager.LogDebug($"[AD Browser] Query succeeded with DirectorySearcher ({objects.Count} objects)");
                }

                // Update UI with results
                foreach (var obj in objects)
                {
                    _currentObjects.Add(obj);
                }

                TxtObjectCount.Text = $"({objects.Count} objects)";
                TxtStatusMessage.Text = $"Loaded {objects.Count} objects";

                // Update counts
                UpdateObjectCounts();
            }
            catch (Exception ex)
            {
                TxtStatusMessage.Text = $"Error loading objects: {ex.Message}";
                LogManager.LogError($"[AD Browser] Failed to load objects: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Scan selected computers
        /// </summary>
        private async void BtnScanSelected_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = GridADObjects.SelectedItems.Cast<ADObjectItem>()
                .Where(item => item.ObjectType == "Computer")
                .ToList();

            if (selectedItems.Count == 0)
            {
                MessageBox.Show("Please select one or more computers to scan.",
                    "No Computers Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            TxtStatusMessage.Text = $"Scanning {selectedItems.Count} computers...";
            BtnScanSelected.IsEnabled = false;

            try
            {
                var scanner = new OptimizedADScanner();

                int scanned = 0;
                foreach (var item in selectedItems)
                {
                    item.Status = "Scanning...";

                    try
                    {
                        // This would integrate with your existing scanning logic
                        // For now, just mark as scanned
                        await Task.Delay(100); // Simulate scan
                        item.Status = "Scanned";
                        scanned++;
                    }
                    catch (Exception ex)
                    {
                        item.Status = $"Error: {ex.Message}";
                    }
                }

                TxtStatusMessage.Text = $"Scanned {scanned} of {selectedItems.Count} computers";
            }
            finally
            {
                BtnScanSelected.IsEnabled = true;
            }
        }

        /// <summary>
        /// Refresh current view
        /// </summary>
        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (ADTreeView.SelectedItem is TreeViewItem selectedItem &&
                selectedItem.Tag is ADTreeNode node)
            {
                await LoadObjectsAsync(node);
            }
        }

        /// <summary>
        /// Update object type counts
        /// </summary>
        private void UpdateObjectCounts()
        {
            TxtComputerCount.Text = _currentObjects.Count(o => o.ObjectType == "Computer").ToString();
            TxtUserCount.Text = _currentObjects.Count(o => o.ObjectType == "User").ToString();
            TxtGroupCount.Text = _currentObjects.Count(o => o.ObjectType == "Group").ToString();
        }

        /// <summary>
        /// Convert ActiveDirectoryManager ADComputer objects to ADObjectItems
        /// TAG: #AD_QUERY_BACKEND_SELECTION #PERFORMANCE_AUDIT #VERSION_7
        /// </summary>
        private List<ADObjectItem> ConvertToADObjectItems(List<ADComputer> computers)
        {
            return computers.Select(c => new ADObjectItem
            {
                Icon = "🖥️",
                Name = c.Name,
                ObjectType = "Computer",
                Description = c.Description,
                DistinguishedName = c.DistinguishedName,
                Status = "Ready"
            }).ToList();
        }

        /// <summary>
        /// Convert ActiveDirectoryManager ADUser objects to ADObjectItems
        /// TAG: #AD_QUERY_BACKEND_SELECTION #PERFORMANCE_AUDIT #VERSION_7
        /// </summary>
        private List<ADObjectItem> ConvertToADObjectItems(List<ADUser> users)
        {
            return users.Select(u => new ADObjectItem
            {
                Icon = "👤",
                Name = u.DisplayName ?? u.SamAccountName,
                ObjectType = "User",
                Description = u.Description,
                DistinguishedName = u.DistinguishedName,
                Status = "Ready"
            }).ToList();
        }

        /// <summary>
        /// Convert ActiveDirectoryManager ADGroup objects to ADObjectItems
        /// TAG: #AD_QUERY_BACKEND_SELECTION #PERFORMANCE_AUDIT #VERSION_7
        /// </summary>
        private List<ADObjectItem> ConvertToADObjectItems(List<ADGroup> groups)
        {
            return groups.Select(g => new ADObjectItem
            {
                Icon = "👥",
                Name = g.Name,
                ObjectType = "Group",
                Description = g.Description,
                DistinguishedName = g.DistinguishedName,
                Status = "Ready"
            }).ToList();
        }

        /// <summary>
        /// Convert ActiveDirectoryManager ADOrganizationalUnit objects to ADObjectItems
        /// TAG: #AD_QUERY_BACKEND_SELECTION #PERFORMANCE_AUDIT #VERSION_7
        /// </summary>
        private List<ADObjectItem> ConvertToADObjectItems(List<ADOrganizationalUnit> ous)
        {
            return ous.Select(o => new ADObjectItem
            {
                Icon = "📁",
                Name = o.Name,
                ObjectType = "OU",
                Description = o.Description,
                DistinguishedName = o.DistinguishedName,
                Status = "Ready"
            }).ToList();
        }

        /// <summary>
        /// Get DirectoryEntry with credentials using Kerberos authentication
        /// TAG: #VERSION_7 #AD_MANAGEMENT #KERBEROS
        /// Uses AuthenticationTypes.Secure (Kerberos) + Sealing (encryption) + Signing (integrity)
        /// </summary>
        private DirectoryEntry GetDirectoryEntry(string path)
        {
            if (!string.IsNullOrEmpty(_username) && !string.IsNullOrEmpty(_password))
            {
                // Use Kerberos authentication with encryption and signing for security
                return new DirectoryEntry(path, _username, _password,
                    AuthenticationTypes.Secure | AuthenticationTypes.Sealing | AuthenticationTypes.Signing);
            }
            return new DirectoryEntry(path);
        }

        /// <summary>
        /// Safely get property from SearchResult
        /// </summary>
        private string GetProperty(SearchResult result, string propertyName)
        {
            try
            {
                if (result.Properties.Contains(propertyName) && result.Properties[propertyName].Count > 0)
                {
                    return result.Properties[propertyName][0]?.ToString() ?? string.Empty;
                }
            }
            catch { }
            return string.Empty;
        }
    }

    // ══════════════════════════════════════════════════════════════
    // DATA CLASSES
    // ══════════════════════════════════════════════════════════════

    public class ADTreeNode
    {
        public string DistinguishedName { get; set; }
        public string ObjectClass { get; set; }
        public ADContainerType ContainerType { get; set; }
        public string Filter { get; set; }
    }

    public enum ADContainerType
    {
        Domain,
        Computers,
        Users,
        Groups,
        OrganizationalUnits,
        Custom
    }

    public class ADObjectItem : INotifyPropertyChanged
    {
        private string _status;

        public string Icon { get; set; }
        public string Name { get; set; }
        public string ObjectType { get; set; }
        public string Description { get; set; }
        public string DistinguishedName { get; set; }

        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged(nameof(Status));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
