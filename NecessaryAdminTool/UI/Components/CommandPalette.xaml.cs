using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NecessaryAdminTool.UI.Components
{
    /// <summary>
    /// Command Palette - Keyboard-driven command launcher
    /// TAG: #AUTO_UPDATE_UI_ENGINE #COMMAND_PALETTE #PHASE_2
    /// </summary>
    public partial class CommandPalette : UserControl
    {
        private ObservableCollection<CommandItem> _allCommands;
        private ObservableCollection<CommandItem> _filteredCommands;
        private int _selectedIndex = 0;

        public event EventHandler<CommandExecutedEventArgs> CommandExecuted;

        public CommandPalette()
        {
            InitializeComponent();
            InitializeCommands();
            _filteredCommands = new ObservableCollection<CommandItem>(_allCommands);
            ResultsList.ItemsSource = _filteredCommands;
            UpdateResultCount();

            // Focus search box when loaded
            Loaded += (s, e) => TxtSearch.Focus();
        }

        /// <summary>
        /// Initialize all available commands
        /// TAG: #COMMAND_REGISTRY
        /// </summary>
        private void InitializeCommands()
        {
            _allCommands = new ObservableCollection<CommandItem>
            {
                // Scanning Commands
                new CommandItem
                {
                    Id = "scan_fleet",
                    Title = "Scan Domain (Fleet)",
                    Description = "Scan all AD computers with WMI queries",
                    Icon = "🔍",
                    Shortcut = "Ctrl+Shift+F",
                    Category = "Scanning",
                    Keywords = new[] { "scan", "fleet", "domain", "ad", "wmi" }
                },
                new CommandItem
                {
                    Id = "scan_single",
                    Title = "Scan Single Computer",
                    Description = "Deep scan individual computer",
                    Icon = "🖥",
                    Shortcut = "Ctrl+S",
                    Category = "Scanning",
                    Keywords = new[] { "scan", "single", "computer", "device" }
                },
                new CommandItem
                {
                    Id = "load_ad_objects",
                    Title = "Load AD Objects",
                    Description = "Browse Active Directory tree structure",
                    Icon = "🌳",
                    Shortcut = "Ctrl+L",
                    Category = "Active Directory",
                    Keywords = new[] { "ad", "active directory", "tree", "browse", "ou" }
                },

                // Authentication
                new CommandItem
                {
                    Id = "auth_login",
                    Title = "Authenticate",
                    Description = "Login with domain credentials",
                    Icon = "🔑",
                    Shortcut = "Ctrl+Alt+A",
                    Category = "Authentication",
                    Keywords = new[] { "auth", "login", "credentials", "domain" }
                },
                new CommandItem
                {
                    Id = "auth_logout",
                    Title = "Logout",
                    Description = "Clear credentials and logout",
                    Icon = "🚪",
                    Category = "Authentication",
                    Keywords = new[] { "logout", "sign out", "clear credentials" }
                },

                // Remote Management
                new CommandItem
                {
                    Id = "tool_rdp",
                    Title = "Remote Desktop (RDP)",
                    Description = "Launch RDP to selected computer",
                    Icon = "🖥",
                    Shortcut = "Ctrl+R",
                    Category = "Remote Tools",
                    Keywords = new[] { "rdp", "remote desktop", "connect" }
                },
                new CommandItem
                {
                    Id = "tool_powershell",
                    Title = "PowerShell Remote",
                    Description = "Open PowerShell session to computer",
                    Icon = "💻",
                    Shortcut = "Ctrl+P",
                    Category = "Remote Tools",
                    Keywords = new[] { "powershell", "ps", "remote", "shell" }
                },
                new CommandItem
                {
                    Id = "tool_services",
                    Title = "Services Manager",
                    Description = "Manage services on remote computer",
                    Icon = "⚙",
                    Category = "Remote Tools",
                    Keywords = new[] { "services", "manage", "remote" }
                },
                new CommandItem
                {
                    Id = "tool_processes",
                    Title = "Process Manager",
                    Description = "View and manage processes",
                    Icon = "⚡",
                    Category = "Remote Tools",
                    Keywords = new[] { "process", "task manager", "kill" }
                },
                new CommandItem
                {
                    Id = "tool_eventlogs",
                    Title = "Event Logs",
                    Description = "View event logs",
                    Icon = "📄",
                    Category = "Remote Tools",
                    Keywords = new[] { "event logs", "events", "errors" }
                },
                new CommandItem
                {
                    Id = "bulk_operations",
                    Title = "Bulk Operations",
                    Description = "Execute commands on multiple computers",
                    Icon = "⚡",
                    Shortcut = "Ctrl+Shift+B",
                    Category = "Remote Tools",
                    Keywords = new[] { "bulk", "multi", "mass", "parallel", "batch" }
                },

                // Quick Fixes
                new CommandItem
                {
                    Id = "fix_windows_update",
                    Title = "Fix Windows Update",
                    Description = "Reset Windows Update components",
                    Icon = "🔧",
                    Category = "Quick Fixes",
                    Keywords = new[] { "fix", "windows update", "wuauserv", "repair" }
                },
                new CommandItem
                {
                    Id = "fix_dns",
                    Title = "Flush DNS Cache",
                    Description = "Clear DNS resolver cache",
                    Icon = "🌐",
                    Category = "Quick Fixes",
                    Keywords = new[] { "dns", "flush", "cache", "ipconfig" }
                },
                new CommandItem
                {
                    Id = "fix_print_spooler",
                    Title = "Restart Print Spooler",
                    Description = "Restart print spooler service",
                    Icon = "🖨",
                    Category = "Quick Fixes",
                    Keywords = new[] { "print", "spooler", "restart", "printer" }
                },

                // Views
                new CommandItem
                {
                    Id = "toggle_view",
                    Title = "Toggle Card/Grid View",
                    Description = "Switch between card and grid layouts",
                    Icon = "📇",
                    Shortcut = "Ctrl+T",
                    Category = "View",
                    Keywords = new[] { "view", "card", "grid", "layout", "toggle" }
                },
                new CommandItem
                {
                    Id = "toggle_terminal",
                    Title = "Toggle Terminal",
                    Description = "Show/hide debug terminal",
                    Icon = "⌨",
                    Shortcut = "Ctrl+`",
                    Category = "View",
                    Keywords = new[] { "terminal", "console", "debug", "output" }
                },

                // Filters - TAG: #AUTO_UPDATE_UI_ENGINE #FILTER_SYSTEM #COMMAND_PALETTE
                new CommandItem
                {
                    Id = "filter_online",
                    Title = "Filter: Online Only",
                    Description = "Show only online computers",
                    Icon = "✅",
                    Category = "Filters",
                    Keywords = new[] { "filter", "online", "active" }
                },
                new CommandItem
                {
                    Id = "filter_offline",
                    Title = "Filter: Offline Only",
                    Description = "Show only offline computers",
                    Icon = "❌",
                    Category = "Filters",
                    Keywords = new[] { "filter", "offline", "inactive" }
                },
                new CommandItem
                {
                    Id = "filter_win11",
                    Title = "Filter: Windows 11",
                    Description = "Show only Windows 11 computers",
                    Icon = "🪟",
                    Category = "Filters",
                    Keywords = new[] { "filter", "windows 11", "win11", "os" }
                },
                new CommandItem
                {
                    Id = "filter_win10",
                    Title = "Filter: Windows 10",
                    Description = "Show only Windows 10 computers",
                    Icon = "🔟",
                    Category = "Filters",
                    Keywords = new[] { "filter", "windows 10", "win10", "os" }
                },
                new CommandItem
                {
                    Id = "filter_win7",
                    Title = "Filter: Windows 7 (EOL)",
                    Description = "Show only Windows 7 computers",
                    Icon = "⚠️",
                    Category = "Filters",
                    Keywords = new[] { "filter", "windows 7", "win7", "eol", "legacy" }
                },
                new CommandItem
                {
                    Id = "filter_servers",
                    Title = "Filter: Servers Only",
                    Description = "Show only Windows Server OS",
                    Icon = "🖥",
                    Category = "Filters",
                    Keywords = new[] { "filter", "server", "windows server" }
                },
                new CommandItem
                {
                    Id = "filter_workstations",
                    Title = "Filter: Workstations Only",
                    Description = "Show only workstation OS",
                    Icon = "💻",
                    Category = "Filters",
                    Keywords = new[] { "filter", "workstation", "desktop", "client" }
                },
                new CommandItem
                {
                    Id = "filter_save_preset",
                    Title = "Save Filter Preset",
                    Description = "Save current filter as reusable preset",
                    Icon = "💾",
                    Shortcut = "Ctrl+Shift+S",
                    Category = "Filters",
                    Keywords = new[] { "filter", "save", "preset", "bookmark" }
                },
                new CommandItem
                {
                    Id = "filter_load_preset",
                    Title = "Load Filter Preset",
                    Description = "Load a saved filter preset",
                    Icon = "📂",
                    Category = "Filters",
                    Keywords = new[] { "filter", "load", "preset", "bookmark" }
                },
                new CommandItem
                {
                    Id = "filter_advanced",
                    Title = "Advanced Filters",
                    Description = "Open advanced filter dialog",
                    Icon = "🔍",
                    Shortcut = "Ctrl+Shift+F",
                    Category = "Filters",
                    Keywords = new[] { "filter", "advanced", "search", "criteria" }
                },
                new CommandItem
                {
                    Id = "filter_clear",
                    Title = "Clear All Filters",
                    Description = "Remove all active filters",
                    Icon = "🗑️",
                    Shortcut = "Esc",
                    Category = "Filters",
                    Keywords = new[] { "filter", "clear", "all", "reset", "remove" }
                },

                // Settings
                new CommandItem
                {
                    Id = "settings",
                    Title = "Settings",
                    Description = "Open application settings",
                    Icon = "⚙",
                    Shortcut = "Ctrl+,",
                    Category = "Settings",
                    Keywords = new[] { "settings", "preferences", "options", "config" }
                },
                new CommandItem
                {
                    Id = "about",
                    Title = "About",
                    Description = "View application information",
                    Icon = "ℹ",
                    Category = "Settings",
                    Keywords = new[] { "about", "version", "info" }
                }
            };
        }

        /// <summary>
        /// Filter commands based on search text (fuzzy matching)
        /// TAG: #FUZZY_SEARCH
        /// </summary>
        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = TxtSearch.Text?.ToLower() ?? "";

            _filteredCommands.Clear();

            if (string.IsNullOrWhiteSpace(query))
            {
                // Show all commands
                foreach (var cmd in _allCommands)
                    _filteredCommands.Add(cmd);
            }
            else
            {
                // Fuzzy search: match title, description, or keywords
                var matches = _allCommands.Where(cmd =>
                    cmd.Title.ToLower().Contains(query) ||
                    cmd.Description.ToLower().Contains(query) ||
                    cmd.Keywords.Any(k => k.Contains(query)) ||
                    FuzzyMatch(cmd.Title.ToLower(), query)
                ).ToList();

                foreach (var match in matches)
                    _filteredCommands.Add(match);
            }

            _selectedIndex = 0;
            UpdateResultCount();
        }

        /// <summary>
        /// Simple fuzzy matching algorithm
        /// </summary>
        private bool FuzzyMatch(string text, string pattern)
        {
            int patternIdx = 0;
            foreach (char c in text)
            {
                if (patternIdx < pattern.Length && c == pattern[patternIdx])
                    patternIdx++;
            }
            return patternIdx == pattern.Length;
        }

        /// <summary>
        /// Handle keyboard navigation (Up/Down/Enter/Escape)
        /// TAG: #KEYBOARD_NAVIGATION
        /// </summary>
        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down && _filteredCommands.Count > 0)
            {
                _selectedIndex = Math.Min(_selectedIndex + 1, _filteredCommands.Count - 1);
                HighlightSelected();
                e.Handled = true;
            }
            else if (e.Key == Key.Up && _filteredCommands.Count > 0)
            {
                _selectedIndex = Math.Max(_selectedIndex - 1, 0);
                HighlightSelected();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && _filteredCommands.Count > 0)
            {
                ExecuteCommand(_filteredCommands[_selectedIndex]);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CloseCommandPalette();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Highlight selected command
        /// </summary>
        private void HighlightSelected()
        {
            for (int i = 0; i < _filteredCommands.Count; i++)
            {
                var container = ResultsList.ItemContainerGenerator.ContainerFromIndex(i) as System.Windows.Controls.ContentPresenter;
                if (container == null) continue;
                var border = System.Windows.Media.VisualTreeHelper.GetChild(container, 0) as System.Windows.Controls.Border;
                if (border == null) continue;
                border.Background = i == _selectedIndex
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 255, 255, 255))
                    : System.Windows.Media.Brushes.Transparent;
            }
        }

        /// <summary>
        /// Execute selected command
        /// </summary>
        private void ExecuteCommand(CommandItem command)
        {
            CommandExecuted?.Invoke(this, new CommandExecutedEventArgs { Command = command });
            CloseCommandPalette();
        }

        /// <summary>
        /// Close the command palette
        /// </summary>
        private void CloseCommandPalette()
        {
            Visibility = Visibility.Collapsed;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            CloseCommandPalette();
        }

        private void ResultItem_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Border border)
            {
                border.Background = new SolidColorBrush(Color.FromRgb(40, 40, 40));
                border.BorderBrush = Helpers.ThemeHelper.PrimaryBrush; // Accent primary
            }
        }

        private void ResultItem_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Border border)
            {
                border.Background = Brushes.Transparent;
                border.BorderBrush = Brushes.Transparent;
            }
        }

        private void ResultItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is CommandItem command)
            {
                ExecuteCommand(command);
            }
        }

        private void UpdateResultCount()
        {
            TxtResultCount.Text = $"{_filteredCommands.Count} command{(_filteredCommands.Count != 1 ? "s" : "")}";
        }
    }

    /// <summary>
    /// Command item model
    /// TAG: #COMMAND_MODEL
    /// </summary>
    public class CommandItem
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
        public string Shortcut { get; set; }
        public string Category { get; set; }
        public string[] Keywords { get; set; }
    }

    /// <summary>
    /// Command executed event args
    /// </summary>
    public class CommandExecutedEventArgs : EventArgs
    {
        public CommandItem Command { get; set; }
    }
}
