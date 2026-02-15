# User Guide: Toast Notifications & Keyboard Shortcuts Configuration

**Tags:** #AUTO_UPDATE_UI_ENGINE #USER_CONFIG #SETTINGS #USER_GUIDE

---

## Quick Start

### Accessing the Configuration Panels

1. Open the application
2. Press **Ctrl+,** (or click the Settings button)
3. The Options Window will open
4. Scroll down to find the new sections:
   - 🔔 **TOAST NOTIFICATIONS**
   - ⌨️ **KEYBOARD SHORTCUTS**

---

## Toast Notifications Configuration

### What Are Toast Notifications?

Toast notifications are small popup messages that appear in the top-right corner of the application to provide feedback about actions and events. They automatically disappear after a few seconds.

**Example:** When you successfully scan a computer, you see a green toast saying "Scan completed successfully!"

### Why Configure Them?

Some users prefer minimal distractions and want to disable certain notifications, while others want to see everything. This panel lets you choose exactly which toasts you want to see.

### Configuration Options

#### Master Toggle
- **Enable Toast Notifications** - Turn ALL toasts on or off with one click
  - When disabled: No toasts will appear, regardless of other settings
  - When enabled: Individual toggles below control which toasts appear

#### Toast Types (Color-Based)
Control which types of toasts appear based on their severity:

- **✓ Success Toasts (Green)** - Positive confirmations
  - Example: "Computer scan completed successfully"
  - Recommendation: Keep enabled

- **ℹ Info Toasts (Blue)** - Informational messages
  - Example: "Loading Active Directory objects..."
  - Recommendation: Keep enabled for important info, disable if too distracting

- **⚠ Warning Toasts (Amber)** - Caution messages
  - Example: "Weak password detected - consider changing"
  - Recommendation: Keep enabled

- **✕ Error Toasts (Red)** - Error messages
  - Example: "Failed to connect to remote computer"
  - Recommendation: ALWAYS keep enabled to catch issues

#### Toast Categories (Function-Based)
Control which categories of toasts appear based on what they're about:

- **Status Update Toasts** - Progress and status messages
  - Example: "Scanning 150 computers..."
  - Disable if: You find status updates distracting

- **Validation Message Toasts** - Input validation results
  - Example: "Invalid hostname format"
  - Disable if: You prefer silent validation

- **Workflow Completion Toasts** - Task completion notifications
  - Example: "Export completed - 500 computers saved to CSV"
  - Disable if: You only want errors, not success confirmations

- **Error Handler Toasts** - System error notifications
  - Example: "WMI query failed - timeout after 15 seconds"
  - Recommendation: ALWAYS keep enabled

### Testing Your Settings

Click the **🧪 TEST TOASTS** button to see a preview of all 4 toast types with your current settings:
1. Green success toast appears
2. (1 second delay)
3. Blue info toast appears
4. (1 second delay)
5. Amber warning toast appears
6. (1 second delay)
7. Red error toast appears

Only toasts that are enabled in your settings will appear during the test.

### Recommended Configurations

**Default (Show Everything):**
- All toggles enabled
- Best for: Users who want maximum awareness

**Minimal (Errors Only):**
- Master toggle: Enabled
- Success: Disabled
- Info: Disabled
- Warning: Enabled
- Error: Enabled
- All categories: Enabled
- Best for: Users who want minimal distractions

**Balanced (Important Only):**
- Master toggle: Enabled
- Success: Disabled
- Info: Disabled
- Warning: Enabled
- Error: Enabled
- Status: Disabled
- Validation: Enabled
- Workflow: Enabled
- Errors: Enabled
- Best for: Power users who know what they're doing

---

## Keyboard Shortcuts Configuration

### What Are Keyboard Shortcuts?

Keyboard shortcuts let you perform actions quickly without using the mouse. For example, pressing **Ctrl+K** opens the Command Palette.

### Why Customize Them?

- **Conflict Resolution:** Your shortcuts might conflict with other software
- **Personal Preference:** You might prefer different key combinations
- **Accessibility:** You might need shortcuts that work better with your keyboard layout
- **Efficiency:** Customize shortcuts to match your workflow

### Default Shortcuts

| Action | Default Shortcut |
|--------|------------------|
| Open Command Palette | Ctrl+K |
| Scan Domain (Fleet) | Ctrl+Shift+F |
| Scan Single Computer | Ctrl+S |
| Load AD Objects | Ctrl+L |
| Authenticate | Ctrl+Alt+A |
| RDP | Ctrl+R |
| PowerShell | Ctrl+P |
| Toggle View | Ctrl+T |
| Toggle Terminal | Ctrl+` |
| Settings | Ctrl+, |
| Refresh | F5 |

### How to Change a Shortcut

1. Find the shortcut you want to change in the table
2. Click the **✏ EDIT** button next to it
3. A dialog will appear showing:
   - The command name (e.g., "Open Command Palette")
   - The current shortcut (e.g., "Ctrl+K")
   - A recording area saying "Press keys..."
4. Press your desired key combination (e.g., Ctrl+Alt+P)
5. The new shortcut will appear in the recording area
6. If there's a conflict, you'll see a warning in red:
   - **"⚠ Warning: This shortcut is already assigned to 'PowerShell'"**
7. Click **ACCEPT** to save the new shortcut
8. Or click **CANCEL** (or press ESC) to abort

### How to Reset a Shortcut

**Reset One Shortcut:**
1. Find the shortcut in the table
2. Click the **↺ RESET** button next to it
3. The shortcut immediately returns to its default value

**Reset All Shortcuts:**
1. Click the **↺ RESET ALL TO DEFAULTS** button at the bottom
2. A confirmation dialog appears:
   - "Are you sure you want to reset ALL keyboard shortcuts to their default values? This cannot be undone."
3. Click **Yes** to reset all shortcuts
4. Click **No** to cancel

### Recording Tips

**Valid Key Combinations:**
- Must include at least one modifier key (Ctrl, Shift, Alt) OR be a function key (F1-F12)
- Examples: Ctrl+K, Ctrl+Shift+F, Alt+A, F5

**Invalid Combinations:**
- Pure letter keys without modifiers (e.g., just "K")
- Pure modifier keys (e.g., just "Ctrl")

**Special Keys:**
- **Backtick/Tilde (`):** The key below ESC - displayed as "`"
- **Comma (,):** Displayed as ","
- **Function Keys:** F1, F2, F3, ..., F12
- **Escape:** Cancels recording - cannot be bound

**Conflict Handling:**
- The dialog warns you if the shortcut is already used
- You can still accept the shortcut (both commands will use it)
- Recommendation: Avoid conflicts by choosing unique combinations

### Example Workflow

**Scenario:** You want to change "Open Command Palette" from Ctrl+K to Ctrl+Space

1. Open Options Window (Ctrl+,)
2. Expand "⌨️ KEYBOARD SHORTCUTS" section
3. Find "Open Command Palette" in the table (shows "Ctrl+K")
4. Click the "✏ EDIT" button
5. Dialog opens showing current shortcut "Ctrl+K"
6. Press Ctrl+Space
7. Recording area shows "Control+Space"
8. No conflict warning appears (Space is unique)
9. Click "ACCEPT"
10. Table updates to show "Control+Space"
11. Click "SAVE & CLOSE" at bottom of Options Window
12. New shortcut is saved and active immediately

---

## Applying Your Changes

After configuring either panel:

1. Click **APPLY** to save without closing
   - Settings are saved and applied immediately
   - Options Window remains open for more changes

2. Click **SAVE & CLOSE** to save and exit
   - Settings are saved and applied immediately
   - Options Window closes
   - You return to the main application

3. Click **CANCEL** to discard changes
   - If you made changes, a confirmation dialog appears
   - "You have unsaved changes. Discard them and close?"
   - Click "Yes" to discard changes
   - Click "No" to return to Options Window

### Persistence

All settings are automatically saved to your user profile and will persist:
- Across application restarts
- Across Windows reboots
- Even after updates (unless settings format changes)

**Settings Location:**
`C:\Users\<YourUsername>\AppData\Local\NecessaryAdminTool\user.config`

---

## Troubleshooting

### Toast Notifications Not Appearing

**Problem:** I disabled all toasts and now I can't see any feedback

**Solution:**
1. Open Options Window (Ctrl+,)
2. Expand "🔔 TOAST NOTIFICATIONS"
3. Enable the master toggle "Enable Toast Notifications"
4. Enable at least the error toasts
5. Click "SAVE & CLOSE"

### Keyboard Shortcuts Not Working

**Problem:** My custom shortcut doesn't work

**Possible Causes:**
1. **Not Applied:** Did you click "SAVE & CLOSE" or "APPLY"?
   - Solution: Re-open Options, verify shortcut, click "SAVE & CLOSE"

2. **Conflict with System:** Windows or another app is using that shortcut
   - Solution: Choose a different key combination
   - Examples to avoid: Ctrl+C, Ctrl+V, Alt+Tab, Win+D

3. **Invalid Combination:** The shortcut doesn't include a modifier
   - Solution: Use Ctrl, Shift, or Alt with your key
   - Example: Change "K" to "Ctrl+K"

### Reset Everything to Defaults

**Problem:** I messed up my settings and want to start over

**Solution for Toast Notifications:**
1. Open Options Window (Ctrl+,)
2. Expand "🔔 TOAST NOTIFICATIONS"
3. Enable master toggle
4. Enable all toast types
5. Enable all toast categories
6. Click "SAVE & CLOSE"

**Solution for Keyboard Shortcuts:**
1. Open Options Window (Ctrl+,)
2. Expand "⌨️ KEYBOARD SHORTCUTS"
3. Click "↺ RESET ALL TO DEFAULTS" button
4. Click "Yes" in confirmation dialog
5. Click "SAVE & CLOSE"

### Settings Not Persisting

**Problem:** My settings reset every time I restart the app

**Possible Causes:**
1. **Permissions Issue:** User profile folder is read-only
   - Solution: Check folder permissions on `%LocalAppData%\NecessaryAdminTool`

2. **Roaming Profile:** Your organization uses roaming profiles
   - Solution: Contact your IT administrator

3. **Portable Installation:** App is running from USB drive
   - Solution: Install to local hard drive

---

## Frequently Asked Questions

**Q: Can I import/export my settings?**
A: Not yet, but this is planned for a future update. You can manually backup the `user.config` file.

**Q: Can I disable the test toast button?**
A: No, the test button always works regardless of your settings. It's meant for testing.

**Q: Will my shortcuts work in other windows (RDP, PowerShell)?**
A: No, shortcuts only work in the main application window. System-wide hotkeys are not supported yet.

**Q: Can I use mouse buttons as shortcuts?**
A: Not currently supported. Only keyboard keys and modifiers are supported.

**Q: What happens if two commands have the same shortcut?**
A: Both commands will trigger when you press the shortcut. This is why conflict warnings exist.

**Q: Can I bind shortcuts without modifiers (like just "K")?**
A: No, shortcuts require at least one modifier (Ctrl, Shift, Alt) unless it's a function key (F1-F12).

**Q: Can I export my toast settings to share with my team?**
A: Not yet, but you can manually copy the settings from the `user.config` file and share the JSON.

---

## Tips & Best Practices

### Toast Notifications
1. **Keep error toasts enabled** - Always know when something goes wrong
2. **Disable status toasts** if you find them distracting during long scans
3. **Use the test button** before committing to major changes
4. **Start with defaults** and only disable what you find annoying

### Keyboard Shortcuts
1. **Avoid system shortcuts** - Don't rebind Ctrl+C, Ctrl+V, etc.
2. **Use Ctrl+Shift combos** for advanced features (less likely to conflict)
3. **Keep F5 as Refresh** - It's a universal standard
4. **Test immediately** after changing a shortcut to ensure it works
5. **Document your custom shortcuts** if you share workstations

---

## Support

If you encounter issues or have questions:
1. Check this guide first
2. Check the Troubleshooting section
3. Review the application logs (if enabled)
4. Contact your IT administrator or support team

**Log Location:**
`C:\Users\<YourUsername>\AppData\Local\NecessaryAdminTool\Logs`

---

**Last Updated:** 2026-02-15
**Version:** 1.2
**Tags:** #USER_GUIDE #TOAST_NOTIFICATIONS #KEYBOARD_SHORTCUTS #HELP
