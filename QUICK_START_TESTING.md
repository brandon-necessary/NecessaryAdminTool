# Quick Start: Testing Toast & Keyboard Configurations

**Tags:** #AUTO_UPDATE_UI_ENGINE #USER_CONFIG #TESTING #QUICK_START

---

## Build & Run Instructions

### Step 1: Build the Solution

1. Open Visual Studio 2022
2. Open `NecessaryAdminTool.sln`
3. Right-click solution in Solution Explorer
4. Select **"Rebuild Solution"**
5. Wait for XAML compilation to complete
6. Verify **"Build succeeded. 0 errors."**

**Expected Build Time:** 30-60 seconds

---

### Step 2: Run the Application

1. Press **F5** (Start Debugging)
2. Application launches
3. Authenticate with domain credentials
4. Main window appears

---

## Testing Toast Notifications

### Quick Test (30 seconds)

1. **Open Options Window**
   - Press **Ctrl+,**
   - Or click Settings button in toolbar

2. **Navigate to Toast Settings**
   - Scroll down to "🔔 TOAST NOTIFICATIONS" section
   - Click to expand

3. **Verify Default State**
   - ✅ All checkboxes should be checked (enabled)
   - Master toggle: **Enabled**
   - All 4 toast types: **Enabled**
   - All 4 toast categories: **Enabled**

4. **Test Preview**
   - Click **"🧪 TEST TOASTS"** button
   - Watch for 4 toasts to appear (1 per second):
     - Green success toast ✓
     - Blue info toast ℹ
     - Amber warning toast ⚠
     - Red error toast ✕

5. **Test Filtering**
   - Uncheck "Success Toasts"
   - Click **"🧪 TEST TOASTS"** again
   - Verify: Only 3 toasts appear (no green)

6. **Test Master Toggle**
   - Uncheck "Enable Toast Notifications" (master)
   - Click **"🧪 TEST TOASTS"** again
   - Verify: NO toasts appear

7. **Save Changes**
   - Click **"SAVE & CLOSE"**
   - Verify status message: "Settings saved successfully!"

### Persistence Test (60 seconds)

1. **Configure Custom Settings**
   - Open Options (Ctrl+,)
   - Expand Toast Notifications
   - Disable "Info Toasts"
   - Disable "Validation Toasts"
   - Click "SAVE & CLOSE"

2. **Close Application**
   - Alt+F4 or click X

3. **Reopen Application**
   - Launch from Start Menu or shortcut
   - Authenticate

4. **Verify Settings Persisted**
   - Open Options (Ctrl+,)
   - Expand Toast Notifications
   - Verify:
     - "Info Toasts" is still **unchecked**
     - "Validation Toasts" is still **unchecked**
     - All other settings remain as you left them

5. **Reset to Defaults**
   - Check all boxes again
   - Click "SAVE & CLOSE"

---

## Testing Keyboard Shortcuts

### Quick Test (30 seconds)

1. **Open Options Window**
   - Press **Ctrl+,**

2. **Navigate to Keyboard Shortcuts**
   - Scroll down to "⌨️ KEYBOARD SHORTCUTS" section
   - Click to expand

3. **Verify Default Shortcuts**
   - Look for table with 11 rows
   - Verify some defaults:
     - Command Palette: **Ctrl+K**
     - Scan Domain: **Ctrl+Shift+F**
     - Settings: **Ctrl+,**
     - Refresh: **F5**

4. **Test Edit Dialog**
   - Find "Settings" row (Ctrl+,)
   - Click **"✏ EDIT"** button
   - Dialog opens showing:
     - Command: "Settings"
     - Current: "Control+,"
     - Recording area: "Press keys..."

5. **Record New Shortcut**
   - Press **Ctrl+Alt+S**
   - Recording area updates to: "Control+Alt+S"
   - Click **"ACCEPT"**
   - Dialog closes
   - Table updates: Settings → **Control+Alt+S**

6. **Test Conflict Detection**
   - Find "PowerShell" row (Ctrl+P)
   - Click **"✏ EDIT"**
   - Press **Ctrl+R** (already used by RDP)
   - Verify warning appears:
     - "⚠ Warning: This shortcut is already assigned to 'RDP'"
   - Press **ESC** to cancel

7. **Test Reset Individual**
   - Find "Settings" row (now Ctrl+Alt+S)
   - Click **"↺ RESET"** button
   - Table updates: Settings → **Control+,** (back to default)

8. **Test Reset All**
   - Click **"↺ RESET ALL TO DEFAULTS"** button at bottom
   - Confirmation dialog appears
   - Click **"Yes"**
   - All shortcuts reset to defaults
   - Toast appears: "All keyboard shortcuts have been reset to defaults."

9. **Save Changes**
   - Click **"SAVE & CLOSE"**
   - Verify status message: "Settings saved successfully!"

### Functional Test (90 seconds)

**NOTE:** This test requires keyboard shortcut implementation in MainWindow (not yet completed)

1. **Configure Test Shortcut**
   - Open Options (Ctrl+,)
   - Expand Keyboard Shortcuts
   - Find "Command Palette"
   - Edit to **Ctrl+Space**
   - Click "SAVE & CLOSE"

2. **Test New Shortcut**
   - Press **Ctrl+Space**
   - Expected: Command Palette should open
   - **Current Status:** This will NOT work yet (MainWindow integration pending)

3. **Test Old Shortcut Still Works**
   - Reset "Command Palette" back to **Ctrl+K**
   - Click "SAVE & CLOSE"
   - Press **Ctrl+K**
   - Expected: Command Palette opens

### Persistence Test (60 seconds)

1. **Configure Custom Shortcut**
   - Open Options (Ctrl+,)
   - Expand Keyboard Shortcuts
   - Edit "Scan Domain" to **Ctrl+Shift+D**
   - Click "SAVE & CLOSE"

2. **Close Application**
   - Alt+F4

3. **Reopen Application**
   - Launch and authenticate

4. **Verify Settings Persisted**
   - Open Options (Ctrl+,)
   - Expand Keyboard Shortcuts
   - Find "Scan Domain" row
   - Verify shortcut shows: **Control+Shift+D**

5. **Reset to Default**
   - Click "↺ RESET" for "Scan Domain"
   - Verify: **Control+Shift+F** restored
   - Click "SAVE & CLOSE"

---

## Integration Testing

### Toast Manager Integration (2 minutes)

1. **Trigger Real Toasts**
   - Perform actions that generate toasts:
     - Scan a computer (Success toast)
     - Load AD objects (Info toast)
     - Enter invalid hostname (Warning toast)
     - Try to connect to offline PC (Error toast)

2. **Configure Filters**
   - Open Options (Ctrl+,)
   - Disable "Success Toasts"
   - Disable "Info Toasts"
   - Click "SAVE & CLOSE"

3. **Verify Filtering**
   - Scan a computer again
   - Verify: NO success toast appears
   - Load AD objects again
   - Verify: NO info toast appears
   - Try invalid action
   - Verify: Warning/Error toasts STILL appear

4. **Re-enable All**
   - Open Options
   - Enable all toasts
   - Save

### Settings Persistence Integration (3 minutes)

1. **Configure Both Panels**
   - Open Options (Ctrl+,)
   - Toast Notifications:
     - Disable "Info Toasts"
     - Disable "Validation Toasts"
   - Keyboard Shortcuts:
     - Edit "Settings" to Ctrl+Alt+,
   - Click "SAVE & CLOSE"

2. **Verify Saved**
   - Look for status: "Settings saved successfully!"

3. **Close & Reopen**
   - Close app completely
   - Relaunch

4. **Verify Both Persisted**
   - Open Options (Ctrl+,)
   - Check Toast Notifications:
     - ✅ Info and Validation should be unchecked
   - Check Keyboard Shortcuts:
     - ✅ Settings should show Ctrl+Alt+,

5. **Reset Everything**
   - Toast Notifications: Check all boxes
   - Keyboard Shortcuts: Click "Reset All"
   - Save & Close

---

## Regression Testing

### Ensure No Breaking Changes (5 minutes)

1. **Test Existing Features**
   - [ ] Scan single computer (should work normally)
   - [ ] Scan domain fleet (should work normally)
   - [ ] Load AD objects (should work normally)
   - [ ] Open Command Palette with Ctrl+K (should work)
   - [ ] Open Settings with Ctrl+, (should work)
   - [ ] RDP to computer with Ctrl+R (should work)

2. **Test Existing Settings**
   - Open Options (Ctrl+,)
   - [ ] General Settings section loads
   - [ ] Performance & Threading section loads
   - [ ] Pinned Devices section loads
   - [ ] Database Management section loads
   - [ ] All existing functionality works

3. **Test Settings Saving**
   - Change a performance setting (e.g., Max Parallel Scans)
   - Click "SAVE & CLOSE"
   - [ ] No errors appear
   - [ ] Setting persists correctly

---

## Error Handling Testing

### Test Corrupted Settings (2 minutes)

1. **Locate user.config File**
   - Navigate to: `C:\Users\<YourUsername>\AppData\Local\NecessaryAdminTool\`
   - Find `user.config`

2. **Backup Original**
   - Copy `user.config` to `user.config.backup`

3. **Corrupt Toast Settings**
   - Open `user.config` in Notepad
   - Find `ToastNotificationSettings` entry
   - Change value to: `{invalid json!!!}`
   - Save file

4. **Test Graceful Fallback**
   - Launch application
   - Open Options (Ctrl+,)
   - Expand Toast Notifications
   - Verify: All checkboxes are checked (defaults loaded)
   - Verify: No crash, no error dialog

5. **Save Corrects Corruption**
   - Click "SAVE & CLOSE"
   - Close app
   - Reopen app
   - Open Options
   - Verify: Settings load correctly now

6. **Restore Backup**
   - Close app
   - Copy `user.config.backup` back to `user.config`

---

## Performance Testing

### Measure Settings Load Time (1 minute)

1. **Enable Stopwatch Logging** (optional)
   - Modify `SettingsManager.LoadAllSettings()` to output timing
   - Or use external profiler

2. **Cold Start**
   - Close application completely
   - Launch application
   - Note startup time

3. **Settings Load**
   - Open Options window (Ctrl+,)
   - Note time to display (should be instant)

4. **Expected Performance**
   - Settings load: < 100ms
   - Options window display: < 200ms
   - No noticeable delay

### Measure Toast Filter Performance (1 minute)

1. **Generate Many Toasts**
   - Run a fleet scan (generates many toasts)
   - Note application responsiveness

2. **With Filtering Enabled**
   - Disable most toast types
   - Run fleet scan again
   - Note application responsiveness

3. **Expected Performance**
   - `ShouldShowToast()` call: < 1ms
   - No performance degradation
   - UI remains responsive

---

## Automated Test Script (PowerShell)

```powershell
# Quick automated test script
Write-Host "Starting NecessaryAdminTool Configuration Tests..." -ForegroundColor Cyan

# Test 1: Check user.config exists
$configPath = "$env:LOCALAPPDATA\NecessaryAdminTool\user.config"
if (Test-Path $configPath) {
    Write-Host "✓ user.config found" -ForegroundColor Green
} else {
    Write-Host "✗ user.config not found (app not run yet)" -ForegroundColor Yellow
}

# Test 2: Check settings structure
if (Test-Path $configPath) {
    $content = Get-Content $configPath -Raw
    if ($content -match "ToastNotificationSettings") {
        Write-Host "✓ Toast settings found in config" -ForegroundColor Green
    } else {
        Write-Host "✗ Toast settings NOT found" -ForegroundColor Red
    }

    if ($content -match "KeyboardShortcutSettings") {
        Write-Host "✓ Keyboard shortcuts found in config" -ForegroundColor Green
    } else {
        Write-Host "✗ Keyboard shortcuts NOT found" -ForegroundColor Red
    }
}

# Test 3: Verify JSON structure
# (Add more sophisticated JSON parsing here)

Write-Host "`nTest complete!" -ForegroundColor Cyan
```

Save as `TestConfiguration.ps1` and run:
```powershell
.\TestConfiguration.ps1
```

---

## Common Issues & Solutions

### Issue: Checkboxes Not Responding
**Symptom:** Clicking checkboxes doesn't change their state
**Cause:** XAML not compiled or InitializeComponent() failed
**Solution:** Rebuild solution in Visual Studio

### Issue: Settings Not Saving
**Symptom:** Changes lost after restart
**Cause:** Save method not called or Settings.Default.Save() missing
**Solution:** Verify `SaveAllSettings()` is called, check logs

### Issue: Dialog Doesn't Capture Keys
**Symptom:** ShortcutRecorderDialog shows "Press keys..." but doesn't update
**Cause:** Window not focused or KeyDown event not wired
**Solution:** Click inside dialog window, verify Window_KeyDown in XAML

### Issue: Toasts Still Appear When Disabled
**Symptom:** Toasts show even with master toggle off
**Cause:** ToastManager.ReloadSettings() not called after save
**Solution:** Verify `SaveToastNotificationSettings()` calls `ToastManager.ReloadSettings()`

---

## Test Results Template

```
==============================================
TOAST & KEYBOARD SHORTCUTS - TEST REPORT
==============================================

Date: _______________________
Tester: _____________________
Build: ______________________

TOAST NOTIFICATIONS
  [ ] Default state correct (all enabled)
  [ ] Test button shows all 4 toasts
  [ ] Master toggle disables all toasts
  [ ] Individual type filters work
  [ ] Individual category filters work
  [ ] Settings persist after restart
  [ ] Real toasts respect filters
  [ ] Graceful handling of corrupted settings

KEYBOARD SHORTCUTS
  [ ] Default shortcuts load (11 total)
  [ ] Edit dialog opens correctly
  [ ] Key recording works
  [ ] Conflict detection works
  [ ] ESC cancels recording
  [ ] Reset individual works
  [ ] Reset all works (with confirmation)
  [ ] Settings persist after restart

INTEGRATION
  [ ] Both save together correctly
  [ ] Both load together correctly
  [ ] No conflicts with existing settings
  [ ] No performance degradation
  [ ] No crashes or errors

REGRESSION
  [ ] Existing features still work
  [ ] Existing settings still save/load
  [ ] No breaking changes

OVERALL RESULT: [ PASS / FAIL ]

Notes:
_______________________________________
_______________________________________
_______________________________________
```

---

**Last Updated:** 2026-02-15
**Tags:** #TESTING #QUICK_START #QA #VALIDATION
