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
    /// RSAT Active Directory Users and Computers-like interface.
    /// Provides tree-based navigation of AD objects with scanning capabilities.
    /// </summary>
    public partial class ADObjectBrowser : UserControl
    {
        private ObservableCollection<ADObjectItem> _currentObjects;
        private List<ADObjectItem> _allObjects = new List<ADObjectItem>();
        private string _domainController;
        private string _username;
        private string _password;
        private bool _useActiveDirectoryManager;
        private ActiveDirectoryManager _adManager;

        // ──────────────────────────────────────────────
        // Dependency Properties
        // ──────────────────────────────────────────────

        /// <summary>
        /// Controls visibility of the SCAN SELECTED button.
        /// True in Fleet Inventory scope mode; False in AD Management browse mode.
        /// </summary>
        public static readonly DependencyProperty ShowScanButtonProperty =
            DependencyProperty.Register("ShowScanButton", typeof(bool), typeof(ADObjectBrowser),
                new PropertyMetadata(true, OnShowScanButtonChanged));

        public bool ShowScanButton
        {
            get => (bool)GetValue(ShowScanButtonProperty);
            set => SetValue(ShowScanButtonProperty, value);
        }

        private static void OnShowScanButtonChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ADObjectBrowser browser && browser.BtnScanSelected != null)
                browser.BtnScanSelected.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
        }

        // ──────────────────────────────────────────────
        // Events
        // ──────────────────────────────────────────────

        /// <summary>
        /// Fired when user selects computers and clicks SCAN SELECTED.
        /// Parameter is the list of hostnames to scan.
        /// Parent window (Fleet Inventory) subscribes to trigger real WMI scan.
        /// TAG: #VERSION_7 #AD_MANAGEMENT
        /// </summary>
        public event EventHandler<List<string>> ScanRequested;

        /// <summary>
        /// Fired when AD object selection changes in the grid.
        /// Parameter is count of selected items.
        /// TAG: #VERSION_7 #AD_MANAGEMENT
        /// </summary>
        public event EventHandler<int> ObjectSelectionChanged;

        // ──────────────────────────────────────────────
        // Constructor
        // ──────────────────────────────────────────────

        public ADObjectBrowser()
        {
            _currentObjects = new ObservableCollection<ADObjectItem>();
            InitializeComponent();
            GridADObjects.ItemsSource = _currentObjects;
            GridADObjects.SelectionChanged += GridADObjects_SelectionChanged;
        }

        private void GridADObjects_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ObjectSelectionChanged?.Invoke(this, GridADObjects.SelectedItems.Count);
        }

        // ──────────────────────────────────────────────
        // Initialization
        // ──────────────────────────────────────────────

        /// <summary>
        /// Initialize the AD object browser with domain connection.
        /// TAG: #AD_QUERY_BACKEND_SELECTION
        /// </summary>
        public async Task InitializeAsync(string domainController, string username = null,
            string password = null, bool useActiveDirectoryManager = false)
        {
            _domainController = domainController;
            _username = username;
            _password = password;
            _useActiveDirectoryManager = useActiveDirectoryManager;

            TxtStatusMessage.Text = "Connecting to Active Directory...";

            try
            {
                // TAG: #SECURITY_CRITICAL #LDAP_INJECTION_PREVENTION
                if (string.IsNullOrWhiteSpace(domainController))
                {
                    LogManager.LogWarning("[AD Browser] Empty or null domain controller hostname");
                    throw new ArgumentException("No domain controller selected. Please select a DC from the dropdown.");
                }
                if (!SecurityValidator.IsValidHostname(domainController))
                {
                    LogManager.LogWarning($"[AD Browser] Invalid domain controller hostname: '{domainController}'");
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
                        rootEntry = GetDirectoryEntry($"LDAP://{domainController}");
                        domainName = rootEntry.Properties["name"]?.Value?.ToString() ?? domainController;
                    }
                    catch (System.Runtime.InteropServices.COMException comEx)
                    {
                        throw new Exception(
                            $"Cannot connect to domain controller '{domainController}'.\n\n" +
                            $"Possible causes:\n• DC is unreachable or offline\n• LDAP service is not running\n" +
                            $"• Network/firewall is blocking connection\n• Invalid credentials\n\n" +
                            $"Technical details: {comEx.Message}", comEx);
                    }
                }).ConfigureAwait(false);

                await Dispatcher.InvokeAsync(() =>
                {
                    if (rootEntry != null && !string.IsNullOrEmpty(domainName))
                    {
                        TxtDomainName.Text = domainName;
                        BuildTreeView(rootEntry, domainName);
                    }
                    TxtStatusMessage.Text = "Connected to Active Directory";
                });
            }
            catch (Exception ex)
            {
                TxtStatusMessage.Text = $"Failed to connect: {ex.Message}";
                MessageBox.Show($"Failed to connect to Active Directory:\n\n{ex.Message}",
                    "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ──────────────────────────────────────────────
        // Tree View
        // ──────────────────────────────────────────────

        private void BuildTreeView(DirectoryEntry rootEntry, string domainName)
        {
            ADTreeView.Items.Clear();

            string rootDn = rootEntry.Properties["distinguishedName"][0]?.ToString() ?? string.Empty;

            var rootNode = new TreeViewItem
            {
                Header = $"🌐 {domainName}",
                Tag = new ADTreeNode
                {
                    DistinguishedName = rootDn,
                    ObjectClass = "domain",
                    ContainerType = ADContainerType.Domain,
                    Filter = "(objectClass=*)"
                }
            };

            // TAG: #SECURITY_CRITICAL — all filters are pre-validated static literals
            rootNode.Items.Add(MakeContainer("🖥️ Computers", ADContainerType.Computers,
                "(objectCategory=computer)"));
            rootNode.Items.Add(MakeContainer("👤 Users", ADContainerType.Users,
                "(&(objectCategory=person)(objectClass=user)(!(objectClass=computer)))"));
            rootNode.Items.Add(MakeContainer("👥 Groups", ADContainerType.Groups,
                "(objectCategory=group)"));
            rootNode.Items.Add(MakeContainer("📁 Organizational Units", ADContainerType.OrganizationalUnits,
                "(objectCategory=organizationalUnit)"));

            rootNode.IsExpanded = true;
            ADTreeView.Items.Add(rootNode);
        }

        private static TreeViewItem MakeContainer(string header, ADContainerType type, string filter)
        {
            var node = new TreeViewItem
            {
                Header = header,
                Tag = new ADTreeNode { ObjectClass = "container", ContainerType = type, Filter = filter }
            };
            node.Items.Add(new TreeViewItem { Header = "Loading..." });
            return node;
        }

        private async void ADTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeViewItem selectedItem && selectedItem.Tag is ADTreeNode node)
            {
                TxtSelectedContainer.Text = selectedItem.Header.ToString();
                TxtStatusMessage.Text = $"Loading objects from {selectedItem.Header}...";
                await LoadObjectsAsync(node);
            }
        }

        // ──────────────────────────────────────────────
        // Object Loading — core query engine
        // ──────────────────────────────────────────────

        private async Task LoadObjectsAsync(ADTreeNode node)
        {
            _currentObjects.Clear();
            List<ADObjectItem> objects = null;

            try
            {
                string queryMethod = Properties.Settings.Default.ADQueryMethod ?? "DirectorySearcher";

                if (queryMethod == "ActiveDirectoryManager")
                {
                    try
                    {
                        TxtStatusMessage.Text = "Loading objects (ActiveDirectoryManager)...";
                        if (_adManager == null)
                        {
                            _adManager = new ActiveDirectoryManager(_domainController, _username, _password);
                            if (!_adManager.Initialize(out string initError))
                                throw new Exception(initError);
                        }

                        var ct = System.Threading.CancellationToken.None;
                        switch (node.ContainerType)
                        {
                            case ADContainerType.Computers:
                                objects = ConvertToADObjectItems(await _adManager.GetComputersAsync(null, ct, node.DistinguishedName));
                                break;
                            case ADContainerType.Users:
                                objects = ConvertToADObjectItems(await _adManager.GetUsersAsync(null, ct, node.DistinguishedName));
                                break;
                            case ADContainerType.Groups:
                                objects = ConvertToADObjectItems(await _adManager.GetGroupsAsync(null, ct, node.DistinguishedName));
                                break;
                            case ADContainerType.OrganizationalUnits:
                                objects = ConvertToADObjectItems(await _adManager.GetOUsAsync(null, ct));
                                break;
                        }
                        LogManager.LogDebug($"[AD Browser] Query succeeded via ActiveDirectoryManager ({objects?.Count ?? 0} objects)");
                    }
                    catch (Exception ex)
                    {
                        LogManager.LogWarning($"[AD Browser] ActiveDirectoryManager failed, falling back: {ex.Message}");
                        objects = null;
                    }
                }

                if (objects == null)
                {
                    TxtStatusMessage.Text = queryMethod == "DirectorySearcher"
                        ? "Loading objects (DirectorySearcher)..."
                        : "Loading objects (DirectorySearcher — fallback)...";

                    objects = await Task.Run(() => QueryViaDirectorySearcher(node)).ConfigureAwait(false);

                    LogManager.LogDebug($"[AD Browser] Query succeeded via DirectorySearcher ({objects.Count} objects)");
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    _allObjects = objects;
                    if (TxtSearch != null) TxtSearch.Text = "";
                    ApplyFilter();
                    TxtStatusMessage.Text = $"Loaded {objects.Count} objects";
                });
            }
            catch (Exception ex)
            {
                TxtStatusMessage.Text = $"Error loading objects: {ex.Message}";
                LogManager.LogError("[AD Browser] Failed to load objects", ex);
            }
        }

        /// <summary>
        /// DirectorySearcher query path — loads rich AD attributes for each object.
        /// Runs on a background thread.
        /// </summary>
        private List<ADObjectItem> QueryViaDirectorySearcher(ADTreeNode node)
        {
            var objectList = new List<ADObjectItem>();

            using (var entry = GetDirectoryEntry($"LDAP://{_domainController}"))
            using (var searcher = new DirectorySearcher(entry))
            {
                // TAG: #SECURITY_CRITICAL #LDAP_INJECTION_PREVENTION
                string filter = node.Filter ?? "(objectClass=*)";
                if (!SecurityValidator.ValidateLDAPFilter(filter))
                {
                    LogManager.LogWarning($"[AD Browser] Invalid LDAP filter blocked: {filter}");
                    throw new InvalidOperationException("LDAP filter failed security validation.");
                }

                searcher.Filter = filter;
                searcher.PageSize = 1000;

                // Core identity
                searcher.PropertiesToLoad.Add("name");
                searcher.PropertiesToLoad.Add("cn");
                searcher.PropertiesToLoad.Add("objectClass");
                searcher.PropertiesToLoad.Add("description");
                searcher.PropertiesToLoad.Add("distinguishedName");

                // Accounts
                searcher.PropertiesToLoad.Add("sAMAccountName");
                searcher.PropertiesToLoad.Add("displayName");
                searcher.PropertiesToLoad.Add("userAccountControl");

                // Computers
                searcher.PropertiesToLoad.Add("dNSHostName");
                searcher.PropertiesToLoad.Add("operatingSystem");
                searcher.PropertiesToLoad.Add("operatingSystemVersion");

                // Users
                searcher.PropertiesToLoad.Add("mail");
                searcher.PropertiesToLoad.Add("givenName");
                searcher.PropertiesToLoad.Add("sn");

                // Groups
                searcher.PropertiesToLoad.Add("groupType");

                using (var results = searcher.FindAll())
                {
                    foreach (SearchResult result in results)
                    {
                        // CRITICAL FIX: objectClass is multi-valued (top → ... → specificClass).
                        // GetProperty() only returns [0] which is always "top".
                        // Concatenate all values so Contains("computer") works correctly.
                        string objectClass = GetAllPropertyValues(result, "objectClass");

                        var obj = new ADObjectItem
                        {
                            Name         = GetProperty(result, "name"),
                            Description  = GetProperty(result, "description"),
                            DistinguishedName = GetProperty(result, "distinguishedName"),
                        };

                        // Order matters: check "computer" before "user" since computers inherit user class
                        if (objectClass.Contains("computer"))
                        {
                            obj.ObjectType = "Computer";
                            obj.Icon       = "🖥️";
                            var dns = GetProperty(result, "dNSHostName");
                            obj.Account    = dns.Length > 0 ? dns : obj.Name;
                            var os  = GetProperty(result, "operatingSystem");
                            var osv = GetProperty(result, "operatingSystemVersion");
                            obj.Info       = os.Length > 0 ? (osv.Length > 0 ? $"{os} ({osv})" : os) : "";
                            obj.IsDisabled = IsAccountDisabled(result);
                        }
                        else if (objectClass.Contains("user") || objectClass.Contains("person"))
                        {
                            obj.ObjectType = "User";
                            obj.Icon       = "👤";
                            obj.Account    = GetProperty(result, "sAMAccountName");
                            var email   = GetProperty(result, "mail");
                            var display = GetProperty(result, "displayName");
                            obj.Info       = email.Length > 0 ? email : display;
                            obj.IsDisabled = IsAccountDisabled(result);
                        }
                        else if (objectClass.Contains("group"))
                        {
                            obj.ObjectType = "Group";
                            obj.Icon       = "👥";
                            obj.Account    = GetProperty(result, "sAMAccountName");
                            obj.Info       = GetGroupTypeDescription(result);
                        }
                        else if (objectClass.Contains("organizationalUnit"))
                        {
                            obj.ObjectType = "OU";
                            obj.Icon       = "📁";
                            obj.Info       = obj.Description;
                        }
                        else
                        {
                            obj.ObjectType = "Other";
                            obj.Icon       = "📄";
                            obj.Info       = obj.Description;
                        }

                        objectList.Add(obj);
                    }
                }
            }

            return objectList;
        }

        // ──────────────────────────────────────────────
        // Button Handlers
        // ──────────────────────────────────────────────

        /// <summary>
        /// Fire ScanRequested event so parent (Fleet Inventory) can trigger real WMI scan.
        /// </summary>
        private void BtnScanSelected_Click(object sender, RoutedEventArgs e)
        {
            var hostnames = GridADObjects.SelectedItems
                .Cast<ADObjectItem>()
                .Where(item => item.ObjectType == "Computer")
                .Select(item => item.Name)
                .ToList();

            if (hostnames.Count == 0)
            {
                MessageBox.Show("Please select one or more computers to scan.",
                    "No Computers Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ScanRequested?.Invoke(this, hostnames);
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (ADTreeView.SelectedItem is TreeViewItem selectedItem &&
                selectedItem.Tag is ADTreeNode node)
            {
                await LoadObjectsAsync(node);
            }
        }

        // ──────────────────────────────────────────────
        // Count helpers
        // ──────────────────────────────────────────────

        private void UpdateObjectCounts()
        {
            TxtComputerCount.Text = _currentObjects.Count(o => o.ObjectType == "Computer").ToString();
            TxtUserCount.Text     = _currentObjects.Count(o => o.ObjectType == "User").ToString();
            TxtGroupCount.Text    = _currentObjects.Count(o => o.ObjectType == "Group").ToString();
        }

        // ──────────────────────────────────────────────
        // Search and filter
        // ──────────────────────────────────────────────

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

        private void CboTypeFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyFilter();

        private void ApplyFilter()
        {
            // Guard: XAML-named elements are null until InitializeComponent() completes
            if (_currentObjects == null || TxtObjectCount == null) return;

            var search = TxtSearch?.Text?.Trim() ?? "";
            var typeLabel = (CboTypeFilter?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All Types";

            // Show/hide placeholder text
            if (SearchPlaceholder != null)
                SearchPlaceholder.Visibility = string.IsNullOrEmpty(search)
                    ? Visibility.Visible : Visibility.Collapsed;

            IEnumerable<ADObjectItem> filtered = _allObjects;

            // Type filter
            if (typeLabel != "All Types")
            {
                string type =
                    typeLabel.Contains("Computer") ? "Computer" :
                    typeLabel.Contains("User") ? "User" :
                    typeLabel.Contains("Group") ? "Group" :
                    typeLabel.Contains("OU") ? "OU" : null;
                if (type != null)
                    filtered = filtered.Where(o => o.ObjectType == type);
            }

            // Text search across Name, Account, and Info columns
            if (!string.IsNullOrEmpty(search))
            {
                filtered = filtered.Where(o =>
                    (o.Name?.IndexOf(search, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                    (o.Account?.IndexOf(search, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                    (o.Info?.IndexOf(search, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0);
            }

            var results = filtered.ToList();
            _currentObjects.Clear();
            foreach (var obj in results)
                _currentObjects.Add(obj);

            bool isFiltered = !string.IsNullOrEmpty(search) || typeLabel != "All Types";
            TxtObjectCount.Text = isFiltered
                ? $"({results.Count} of {_allObjects.Count} objects)"
                : $"({results.Count} objects)";

            UpdateObjectCounts();
        }

        // ──────────────────────────────────────────────
        // ActiveDirectoryManager conversion helpers
        // ──────────────────────────────────────────────

        private List<ADObjectItem> ConvertToADObjectItems(List<ADComputer> computers) =>
            (computers ?? new List<ADComputer>()).Select(c => new ADObjectItem
            {
                Icon = "🖥️", Name = c.Name, ObjectType = "Computer",
                Account = c.Name, Info = c.Description, Description = c.Description,
                DistinguishedName = c.DistinguishedName
            }).ToList();

        private List<ADObjectItem> ConvertToADObjectItems(List<ADUser> users) =>
            (users ?? new List<ADUser>()).Select(u => new ADObjectItem
            {
                Icon = "👤", ObjectType = "User",
                Name = u.DisplayName?.Length > 0 ? u.DisplayName : u.SamAccountName,
                Account = u.SamAccountName, Info = u.Description, Description = u.Description,
                DistinguishedName = u.DistinguishedName
            }).ToList();

        private List<ADObjectItem> ConvertToADObjectItems(List<ADGroup> groups) =>
            (groups ?? new List<ADGroup>()).Select(g => new ADObjectItem
            {
                Icon = "👥", Name = g.Name, ObjectType = "Group",
                Account = g.Name, Info = g.Description, Description = g.Description,
                DistinguishedName = g.DistinguishedName
            }).ToList();

        private List<ADObjectItem> ConvertToADObjectItems(List<ADOrganizationalUnit> ous) =>
            (ous ?? new List<ADOrganizationalUnit>()).Select(o => new ADObjectItem
            {
                Icon = "📁", Name = o.Name, ObjectType = "OU",
                Info = o.Description, Description = o.Description,
                DistinguishedName = o.DistinguishedName
            }).ToList();

        // ──────────────────────────────────────────────
        // LDAP / DirectoryEntry helpers
        // ──────────────────────────────────────────────

        /// <summary>
        /// Get DirectoryEntry with Kerberos authentication.
        /// TAG: #VERSION_7 #AD_MANAGEMENT #KERBEROS
        /// </summary>
        private DirectoryEntry GetDirectoryEntry(string path)
        {
            if (!string.IsNullOrEmpty(_username) && !string.IsNullOrEmpty(_password))
                return new DirectoryEntry(path, _username, _password,
                    AuthenticationTypes.Secure | AuthenticationTypes.Sealing | AuthenticationTypes.Signing);
            return new DirectoryEntry(path);
        }

        /// <summary>
        /// Safely get the first value of a single-valued LDAP property.
        /// </summary>
        private string GetProperty(SearchResult result, string propertyName)
        {
            try
            {
                if (result.Properties.Contains(propertyName) && result.Properties[propertyName].Count > 0)
                    return result.Properties[propertyName][0]?.ToString() ?? string.Empty;
            }
            catch { }
            return string.Empty;
        }

        /// <summary>
        /// Concatenate ALL values of a multi-valued property (e.g. objectClass).
        /// objectClass is ordered top→...→specificClass — we need all values to type-detect correctly.
        /// CRITICAL: using only [0] always returns "top" which breaks type detection.
        /// </summary>
        private string GetAllPropertyValues(SearchResult result, string propertyName)
        {
            try
            {
                if (!result.Properties.Contains(propertyName)) return string.Empty;
                var props = result.Properties[propertyName];
                return string.Join(",",
                    Enumerable.Range(0, props.Count).Select(i => props[i]?.ToString() ?? string.Empty));
            }
            catch { }
            return string.Empty;
        }

        /// <summary>
        /// Returns true if the AD account is disabled (userAccountControl bit 1 = ACCOUNTDISABLE).
        /// </summary>
        private bool IsAccountDisabled(SearchResult result)
        {
            try
            {
                var uac = GetProperty(result, "userAccountControl");
                if (int.TryParse(uac, out int flags))
                    return (flags & 0x2) != 0; // ACCOUNTDISABLE flag
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Convert groupType integer to human-readable scope + security/distribution string.
        /// Bit 31 (0x80000000) = Security group; scope bits: 0x2 = Global, 0x4 = Domain Local, 0x8 = Universal.
        /// </summary>
        private string GetGroupTypeDescription(SearchResult result)
        {
            try
            {
                var gtStr = GetProperty(result, "groupType");
                if (!int.TryParse(gtStr, out int groupType)) return string.Empty;

                bool isSecurity = (groupType & unchecked((int)0x80000000)) != 0;
                string scope =
                    (groupType & 0x8) != 0 ? "Universal" :
                    (groupType & 0x4) != 0 ? "Domain Local" :
                    (groupType & 0x2) != 0 ? "Global" : "Unknown scope";

                return $"{(isSecurity ? "Security" : "Distribution")} — {scope}";
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
        Domain, Computers, Users, Groups, OrganizationalUnits, Custom
    }

    public class ADObjectItem : INotifyPropertyChanged
    {
        private string _status;
        private bool _isDisabled;

        public string Icon             { get; set; }
        public string Name             { get; set; }
        public string ObjectType       { get; set; }

        /// <summary>sAMAccountName for users/groups; dNSHostName for computers.</summary>
        public string Account          { get; set; }

        /// <summary>Contextual detail: OS for computers, email for users, group type for groups.</summary>
        public string Info             { get; set; }

        /// <summary>Raw AD description field (kept for ADManager compatibility).</summary>
        public string Description      { get; set; }

        public string DistinguishedName { get; set; }

        /// <summary>True when userAccountControl ACCOUNTDISABLE bit is set.</summary>
        public bool IsDisabled
        {
            get => _isDisabled;
            set
            {
                _isDisabled = value;
                OnPropertyChanged(nameof(IsDisabled));
                OnPropertyChanged(nameof(EnabledDisplay));
            }
        }

        /// <summary>Human-readable enabled state. Returns empty string for OUs and Other objects.</summary>
        public string EnabledDisplay
        {
            get
            {
                if (ObjectType == "OU" || ObjectType == "Other") return "";
                return IsDisabled ? "✗ Disabled" : "✓ Active";
            }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(nameof(Status)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
