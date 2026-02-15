using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace NecessaryAdminTool
{
    // TAG: #AUTO_UPDATE_UI_ENGINE #USER_CONFIG #SETTINGS
    /// <summary>
    /// Dialog for recording keyboard shortcuts
    /// </summary>
    public partial class ShortcutRecorderDialog : Window
    {
        private string _commandName;
        private string _currentShortcut;
        private List<KeyValuePair<string, KeyboardShortcut>> _existingShortcuts;

        public string RecordedKey { get; private set; }
        public string RecordedModifiers { get; private set; }

        public ShortcutRecorderDialog(string commandName, string currentShortcut, List<KeyValuePair<string, KeyboardShortcut>> existingShortcuts)
        {
            InitializeComponent();
            _commandName = commandName;
            _currentShortcut = currentShortcut;
            _existingShortcuts = existingShortcuts;

            TxtCommand.Text = commandName;
            TxtCurrentShortcut.Text = currentShortcut;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Focus the window so it can capture key presses
            this.Focus();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;

            // ESC to cancel
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
                return;
            }

            // Get the actual key (not modifier keys)
            Key actualKey = e.Key == Key.System ? e.SystemKey : e.Key;

            // Ignore pure modifier keys
            if (actualKey == Key.LeftCtrl || actualKey == Key.RightCtrl ||
                actualKey == Key.LeftShift || actualKey == Key.RightShift ||
                actualKey == Key.LeftAlt || actualKey == Key.RightAlt ||
                actualKey == Key.LWin || actualKey == Key.RWin)
            {
                return;
            }

            // Build modifier string
            List<string> modifiers = new List<string>();
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                modifiers.Add("Control");
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                modifiers.Add("Shift");
            if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
                modifiers.Add("Alt");

            string modifierString = modifiers.Count > 0 ? string.Join("+", modifiers) : "None";
            string keyString = actualKey.ToString();

            // Display the recorded shortcut
            RecordedKey = keyString;
            RecordedModifiers = modifierString;

            string displayShortcut = modifierString == "None" ? FormatKey(keyString) : $"{modifierString}+{FormatKey(keyString)}";
            TxtRecordedKeys.Text = displayShortcut;

            // Check for conflicts
            bool hasConflict = false;
            foreach (var kvp in _existingShortcuts)
            {
                if (kvp.Value.Key == keyString && kvp.Value.Modifiers == modifierString && kvp.Value.Command != _commandName)
                {
                    hasConflict = true;
                    TxtConflictWarning.Text = $"⚠ Warning: This shortcut is already assigned to '{kvp.Value.Command}'";
                    TxtConflictWarning.Visibility = Visibility.Visible;
                    break;
                }
            }

            if (!hasConflict)
            {
                TxtConflictWarning.Visibility = Visibility.Collapsed;
            }

            // Enable accept button
            BtnAccept.IsEnabled = true;
        }

        private void BtnAccept_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private string FormatKey(string key)
        {
            // Format special keys
            return key switch
            {
                "OemTilde" => "`",
                "OemComma" => ",",
                _ => key
            };
        }
    }
}
