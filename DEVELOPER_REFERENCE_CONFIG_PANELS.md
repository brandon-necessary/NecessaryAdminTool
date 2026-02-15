# Developer Reference: Toast & Keyboard Configuration Panels

**Tags:** #AUTO_UPDATE_UI_ENGINE #USER_CONFIG #SETTINGS #DEV_REFERENCE

---

## Quick Reference for Future Development

### Adding a New Toast Notification Type

1. **Update ToastType Enum** (`Models/UI/ToastNotification.cs`)
   ```csharp
   public enum ToastType
   {
       Success,
       Info,
       Warning,
       Error,
       Custom  // NEW
   }
   ```

2. **Update ToastNotificationSettings** (`SettingsManager.cs`)
   ```csharp
   public class ToastNotificationSettings
   {
       // ... existing properties ...
       public bool ShowCustomToasts { get; set; }
   }
   ```

3. **Update ToastManager.ShouldShowToast()** (`Managers/UI/ToastManager.cs`)
   ```csharp
   bool typeAllowed = type switch
   {
       ToastType.Success => _settings.ShowSuccessToasts,
       ToastType.Info => _settings.ShowInfoToasts,
       ToastType.Warning => _settings.ShowWarningToasts,
       ToastType.Error => _settings.ShowErrorToasts,
       ToastType.Custom => _settings.ShowCustomToasts,  // NEW
       _ => true
   };
   ```

4. **Update ToastManager.CreateToastElement()** for color/icon
   ```csharp
   Color borderColor = toast.Type switch
   {
       ToastType.Success => Color.FromRgb(16, 185, 129),
       ToastType.Warning => Color.FromRgb(245, 158, 11),
       ToastType.Error => Color.FromRgb(239, 68, 68),
       ToastType.Custom => Color.FromRgb(255, 0, 255),  // NEW
       _ => Color.FromRgb(59, 130, 246)
   };
   ```

5. **Add UI Checkbox** (`OptionsWindow.xaml`)
   ```xaml
   <CheckBox x:Name="ChkToastCustom" Content="Custom Toasts"
             Foreground="White" FontSize="11" IsChecked="True"/>
   ```

6. **Update Load/Save Methods** (`OptionsWindow.xaml.cs`)
   ```csharp
   // In LoadToastNotificationSettings()
   if (ChkToastCustom != null)
       ChkToastCustom.IsChecked = toastSettings.ShowCustomToasts;

   // In SaveToastNotificationSettings()
   ShowCustomToasts = ChkToastCustom?.IsChecked ?? true,
   ```

---

### Adding a New Keyboard Shortcut

1. **Add to Default Shortcuts** (`SettingsManager.cs`)
   ```csharp
   public static Dictionary<string, KeyboardShortcut> GetDefaultShortcuts()
   {
       return new Dictionary<string, KeyboardShortcut>
       {
           // ... existing shortcuts ...
           { "NewAction", new KeyboardShortcut
               { Command = "New Action", Key = "N", Modifiers = "Control+Shift" }
           }
       };
   }
   ```

2. **Implement Handler in MainWindow** (`MainWindow.xaml.cs`)
   ```csharp
   private void MainWindow_KeyDown(object sender, KeyEventArgs e)
   {
       var shortcuts = SettingsManager.LoadAllSettings().KeyboardShortcuts;

       // Check if current key combo matches "NewAction"
       var newActionShortcut = shortcuts.Shortcuts["NewAction"];
       if (IsKeyComboMatch(e, newActionShortcut))
       {
           PerformNewAction();
           e.Handled = true;
       }
   }

   private bool IsKeyComboMatch(KeyEventArgs e, KeyboardShortcut shortcut)
   {
       Key actualKey = e.Key == Key.System ? e.SystemKey : e.Key;
       if (actualKey.ToString() != shortcut.Key)
           return false;

       var modifiers = Keyboard.Modifiers;
       string currentModifiers = BuildModifierString(modifiers);

       return currentModifiers == shortcut.Modifiers;
   }

   private string BuildModifierString(ModifierKeys modifiers)
   {
       List<string> mods = new List<string>();
       if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control)
           mods.Add("Control");
       if ((modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
           mods.Add("Shift");
       if ((modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
           mods.Add("Alt");

       return mods.Count > 0 ? string.Join("+", mods) : "None";
   }
   ```

---

### Code Architecture

#### Settings Flow
```
User Changes UI
    ↓
OptionsWindow.xaml.cs → SaveToastNotificationSettings()
    ↓
SettingsManager.SaveToastNotificationSettings()
    ↓
Settings.Default.ToastNotificationSettings = JSON
    ↓
Settings.Default.Save()
    ↓
Saved to user.config file
```

#### Loading Flow
```
Application Starts
    ↓
OptionsWindow.LoadAllSettings()
    ↓
LoadToastNotificationSettings() + LoadKeyboardShortcutSettings()
    ↓
SettingsManager.LoadAllSettings()
    ↓
Deserialize JSON from Settings.Default
    ↓
Populate UI Controls
```

#### Toast Display Flow
```
ToastManager.ShowSuccess("Message")
    ↓
ShouldShowToast(ToastType.Success, category)
    ↓
Check _settings.EnableToasts (master toggle)
    ↓
Check _settings.ShowSuccessToasts (type filter)
    ↓
Check _settings.ShowStatusUpdateToasts (category filter, if provided)
    ↓
If all pass → ShowToast(toast)
    ↓
If any fail → return (no toast shown)
```

---

## Class Diagram

```
SettingsManager (static class)
├── AppSettings
│   ├── GeneralSettings
│   ├── PerformanceSettings
│   ├── ToastNotificationSettings  ← NEW
│   └── KeyboardShortcutSettings   ← NEW
├── LoadAllSettings() → AppSettings
├── SaveToastNotificationSettings(ToastNotificationSettings)
└── SaveKeyboardShortcutSettings(KeyboardShortcutSettings)

ToastManager (static class)
├── _toastContainer : Panel
├── _settings : ToastNotificationSettings  ← NEW
├── Initialize(Panel)
├── LoadSettings()                          ← NEW
├── ReloadSettings()                        ← NEW
├── ShouldShowToast(type, category) → bool ← NEW
├── ShowSuccess(message, category)          ← MODIFIED
├── ShowInfo(message, category)             ← MODIFIED
├── ShowWarning(message, category)          ← MODIFIED
└── ShowError(message, category)            ← MODIFIED

OptionsWindow : Window
├── LoadToastNotificationSettings()         ← NEW
├── SaveToastNotificationSettings()         ← NEW
├── BtnTestToasts_Click()                   ← NEW
├── LoadKeyboardShortcutSettings()          ← NEW
├── SaveKeyboardShortcutSettings()          ← NEW
├── BtnEditShortcut_Click()                 ← NEW
├── BtnResetShortcut_Click()                ← NEW
└── BtnResetAllShortcuts_Click()            ← NEW

ShortcutRecorderDialog : Window             ← NEW
├── RecordedKey : string
├── RecordedModifiers : string
├── Window_KeyDown()
├── BtnAccept_Click()
└── BtnCancel_Click()
```

---

## Data Models

### ToastNotificationSettings
```csharp
{
    "EnableToasts": true,
    "ShowSuccessToasts": true,
    "ShowInfoToasts": true,
    "ShowWarningToasts": true,
    "ShowErrorToasts": true,
    "ShowStatusUpdateToasts": true,
    "ShowValidationToasts": true,
    "ShowWorkflowToasts": true,
    "ShowErrorHandlerToasts": true
}
```

### KeyboardShortcutSettings
```csharp
{
    "Shortcuts": {
        "CommandPalette": {
            "Command": "Open Command Palette",
            "Key": "K",
            "Modifiers": "Control"
        },
        "ScanDomain": {
            "Command": "Scan Domain (Fleet)",
            "Key": "F",
            "Modifiers": "Control+Shift"
        },
        // ... more shortcuts ...
    }
}
```

---

## Common Tasks

### How to Add a Toast Category Filter

**Scenario:** You want to add a "Network" category for network-related toasts

1. Add property to `ToastNotificationSettings`:
   ```csharp
   public bool ShowNetworkToasts { get; set; }
   ```

2. Initialize in constructor:
   ```csharp
   public ToastNotificationSettings()
   {
       // ... existing defaults ...
       ShowNetworkToasts = true;
   }
   ```

3. Update `ShouldShowToast()` category switch:
   ```csharp
   return category.ToLower() switch
   {
       "status" => _settings.ShowStatusUpdateToasts,
       "validation" => _settings.ShowValidationToasts,
       "workflow" => _settings.ShowWorkflowToasts,
       "error" => _settings.ShowErrorHandlerToasts,
       "network" => _settings.ShowNetworkToasts,  // NEW
       _ => true
   };
   ```

4. Add UI checkbox in `OptionsWindow.xaml`:
   ```xaml
   <CheckBox x:Name="ChkToastNetwork" Content="Network Toasts"
             Foreground="White" FontSize="11" IsChecked="True"/>
   ```

5. Update load/save methods in `OptionsWindow.xaml.cs`:
   ```csharp
   // Load
   if (ChkToastNetwork != null)
       ChkToastNetwork.IsChecked = toastSettings.ShowNetworkToasts;

   // Save
   ShowNetworkToasts = ChkToastNetwork?.IsChecked ?? true,
   ```

6. Use in code:
   ```csharp
   ToastManager.ShowInfo("Connected to network", category: "network");
   ```

---

### How to Debug Settings Not Saving

**Check These Points:**

1. **Settings.Default.Save() called?**
   - Look for `Settings.Default.Save()` in save methods
   - Add breakpoint and verify it's reached

2. **JSON serialization successful?**
   ```csharp
   try
   {
       var serializer = new JavaScriptSerializer();
       string json = serializer.Serialize(settings);
       Console.WriteLine($"JSON: {json}");  // Debug output
       Settings.Default.ToastNotificationSettings = json;
       Settings.Default.Save();
   }
   catch (Exception ex)
   {
       Console.WriteLine($"Serialization failed: {ex.Message}");
   }
   ```

3. **Settings property exists in Settings.settings?**
   - Open `Properties/Settings.settings` in VS
   - Verify property name matches exactly
   - Verify type is "String" and scope is "User"

4. **Settings.Designer.cs property generated?**
   - Open `Properties/Settings.Designer.cs`
   - Search for property name
   - If missing, rebuild solution or regenerate

5. **File permissions on user.config?**
   - Navigate to `%LocalAppData%\NecessaryAdminTool\`
   - Check if `user.config` exists and is writable
   - Delete `user.config` and restart app to regenerate

6. **AppData folder accessible?**
   ```csharp
   string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
   Console.WriteLine($"AppData: {path}");
   Console.WriteLine($"Writable: {Directory.Exists(path)}");
   ```

---

### How to Add Settings Validation

**Example:** Validate that at least one toast type is enabled

1. Add validation method:
   ```csharp
   public static bool ValidateToastSettings(ToastNotificationSettings settings)
   {
       if (!settings.EnableToasts)
           return true;  // Master disabled, no further validation needed

       // At least one type must be enabled
       if (!settings.ShowSuccessToasts &&
           !settings.ShowInfoToasts &&
           !settings.ShowWarningToasts &&
           !settings.ShowErrorToasts)
       {
           return false;
       }

       return true;
   }
   ```

2. Call before saving:
   ```csharp
   private void SaveToastNotificationSettings()
   {
       var toastSettings = new ToastNotificationSettings
       {
           // ... populate from UI ...
       };

       if (!SettingsManager.ValidateToastSettings(toastSettings))
       {
           MessageBox.Show(
               "At least one toast type must be enabled.",
               "Validation Error",
               MessageBoxButton.OK,
               MessageBoxImage.Warning);
           return;
       }

       SettingsManager.SaveToastNotificationSettings(toastSettings);
   }
   ```

---

## Performance Considerations

### Toast Settings
- **Loading:** Settings loaded once on startup, cached in `ToastManager._settings`
- **Checking:** `ShouldShowToast()` is called before every toast (fast, just boolean checks)
- **Reloading:** Only called when user changes settings (infrequent)

### Keyboard Shortcuts
- **Loading:** Settings loaded once when OptionsWindow opens
- **Checking:** Need to implement efficient shortcut checking in MainWindow
- **Recommendation:** Cache shortcuts in MainWindow, reload on settings change

**Optimization Tip:**
```csharp
// Cache shortcuts in MainWindow for fast lookup
private Dictionary<string, KeyboardShortcut> _shortcuts;

private void LoadShortcuts()
{
    var appSettings = SettingsManager.LoadAllSettings();
    _shortcuts = appSettings.KeyboardShortcuts.Shortcuts;
}

private void MainWindow_KeyDown(object sender, KeyEventArgs e)
{
    // Fast dictionary lookup instead of loading settings each time
    foreach (var kvp in _shortcuts)
    {
        if (IsMatch(e, kvp.Value))
        {
            ExecuteCommand(kvp.Key);
            e.Handled = true;
            return;
        }
    }
}
```

---

## Testing Checklist

### Unit Tests to Write

**ToastNotificationSettings:**
- [ ] Default constructor initializes all values to true
- [ ] JSON serialization preserves all properties
- [ ] JSON deserialization handles missing properties (defaults)
- [ ] JSON deserialization handles null/empty string

**KeyboardShortcutSettings:**
- [ ] GetDefaultShortcuts() returns 11 shortcuts
- [ ] All default shortcuts have unique key combinations
- [ ] DisplayShortcut formats correctly (Ctrl+K, F5, etc.)
- [ ] FormatKey() handles special keys (OemTilde, OemComma)

**ToastManager.ShouldShowToast():**
- [ ] Returns false when EnableToasts is false (master toggle)
- [ ] Returns false when specific toast type is disabled
- [ ] Returns false when specific category is disabled
- [ ] Returns true when all filters pass
- [ ] Returns true for unknown categories (permissive)

**ShortcutRecorderDialog:**
- [ ] Ignores pure modifier keys (Ctrl, Shift, Alt alone)
- [ ] Captures Ctrl+K correctly
- [ ] Captures Ctrl+Shift+F correctly
- [ ] Captures F5 correctly
- [ ] Detects conflicts with existing shortcuts
- [ ] ESC cancels recording (DialogResult = false)

### Integration Tests

**Settings Persistence:**
- [ ] Save toast settings, restart app, verify loaded correctly
- [ ] Save keyboard shortcuts, restart app, verify loaded correctly
- [ ] Change settings multiple times, verify last change persists
- [ ] Delete user.config, verify app creates new one with defaults

**UI Interactions:**
- [ ] Master toggle disables all toast checkboxes (visual feedback)
- [ ] Test button shows toasts according to current settings
- [ ] Edit shortcut dialog opens and captures keys
- [ ] Reset shortcut button restores default
- [ ] Reset all shortcuts button shows confirmation and works
- [ ] Apply button saves without closing window
- [ ] Save & Close button saves and closes window
- [ ] Cancel button discards changes (with confirmation if dirty)

---

## Known Issues & Limitations

### Current Limitations

1. **Keyboard Shortcuts Not Yet Integrated**
   - Shortcuts are configurable but not yet dynamically bound in MainWindow
   - MainWindow still uses hardcoded KeyDown events
   - Future work: Create KeyboardShortcutManager and dynamic binding

2. **No Import/Export**
   - Can't export settings to share with team
   - Can't import settings from file
   - Manual workaround: Copy user.config file

3. **No Preset Profiles**
   - Can't save/load named configurations (e.g., "Minimal", "Power User")
   - Each setting must be configured individually

4. **No Global Hotkeys**
   - Shortcuts only work when app window has focus
   - Can't trigger actions when app is minimized

5. **No Conflict Prevention**
   - Dialog warns about conflicts but doesn't prevent them
   - Two commands can have the same shortcut (both will trigger)

### Future Enhancements

See `TASK_11_12_IMPLEMENTATION_SUMMARY.md` for detailed list of planned features.

---

## Migration Notes

### Upgrading from Pre-Config Panel Version

**If user has no existing settings:**
- All settings default to enabled (permissive)
- No migration needed

**If user.config exists but missing new properties:**
- JSON deserialization handles gracefully
- Missing properties default to enabled
- No error thrown, no data loss

**Schema Changes:**
- Current version stores JSON strings in Settings.Default
- Future version might use XML or SQLite
- Migration path: Read old JSON, convert to new format, save

---

## Support & Maintenance

### Logging
All settings operations are logged via `LogManager`:
```csharp
LogManager.LogInfo("SettingsManager.SaveToastNotificationSettings() - Saving...");
LogManager.LogWarning("Failed to load toast settings, using defaults");
LogManager.LogError("Failed to save keyboard shortcuts", ex);
```

### Error Handling
All methods include try-catch blocks:
```csharp
try
{
    // Operation
}
catch (Exception ex)
{
    LogManager.LogError("Context", ex);
    MessageBox.Show($"User-friendly message\n\n{ex.Message}");
    throw;  // Re-throw if caller needs to handle
}
```

### Debugging
Enable verbose logging in `LogManager` configuration:
```csharp
LogManager.SetLogLevel(LogLevel.Debug);
```

Check logs at:
`C:\Users\<Username>\AppData\Local\NecessaryAdminTool\Logs\`

---

**Last Updated:** 2026-02-15
**Maintainer:** Development Team
**Tags:** #DEVELOPER_REFERENCE #SETTINGS #ARCHITECTURE
