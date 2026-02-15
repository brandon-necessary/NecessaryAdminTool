# SuperAdmin Configuration Panel - User Guide
<!-- TAG: #SUPERADMIN #WHITELABEL_GUI #ADVANCED_CONFIG #HIDDEN_FEATURE -->
<!-- Last Updated: 2026-02-14 | Version: 1.0 (1.2602.0.0) -->

---

## 🔒 Overview

The **SuperAdmin Configuration Panel** is a hidden administrative interface that provides GUI-based white-label customization and advanced system management. This powerful tool allows you to:

- Configure company branding via GUI (no manual file editing!)
- Manage advanced application settings
- Perform database maintenance
- Export/import configurations
- View detailed system information

**⚠️ Important:** SuperAdmin mode requires **Administrator privileges** and is password-protected to prevent unauthorized access.

---

## 🚀 Quick Start

### **Access Methods**

There are **TWO secret ways** to access the SuperAdmin panel:

#### **Method 1: Secret Keyboard Shortcut** ⌨️

1. **Ensure** the application is running with **Administrator privileges**
2. Press the secret key combination: **`Ctrl + Shift + Alt + S`**
3. Enter the SuperAdmin password when prompted
4. The SuperAdmin Configuration Panel will open

#### **Method 2: Invisible Button (Passcode)** 🖱️

1. **Ensure** the application is running with **Administrator privileges**
2. Locate the **version badge** in the top-right header (e.g., "v1.2602")
3. **Click the version badge 5 times rapidly** (within 2 seconds)
4. You'll see a subtle pulse animation on each click
5. After the 5th click, the password prompt will appear
6. Enter the SuperAdmin password
7. The SuperAdmin Configuration Panel will open

**💡 Tip:** The button is completely invisible (opacity: 0.01) and has no tooltip, making it extremely discrete!

### **Default Password**

```
Default Password: 08282021
```

**🔐 Security Recommendation:** Change this password in production by modifying line in `MainWindow.xaml.cs`:

```csharp
// Line ~2679 in MainWindow.xaml.cs
string correctPassword = "08282021"; // ← Change this!
```

For enhanced security, use a hashed password with salt:

```csharp
// Example: SHA256 hashed password
string correctPasswordHash = "YOUR_SHA256_HASH_HERE";
string enteredPasswordHash = ComputeSHA256Hash(enteredPassword);
if (enteredPasswordHash == correctPasswordHash) { ... }
```

---

## 📋 Features Overview

The SuperAdmin panel has **4 main tabs**:

| Tab | Purpose | Key Features |
|-----|---------|--------------|
| **🏷️ White-Label Branding** | Customize company information | Company name, domain, email, live preview |
| **⚙️ Advanced Settings** | System configuration | Debug mode, hidden features, config export/import |
| **💾 Database** | Database management | Optimize, backup, clear data |
| **ℹ️ System Info** | View system details | Application info, paths, runtime stats |

---

## 🏷️ Tab 1: White-Label Branding

### **Purpose**

Configure your organization's branding information without manually editing source files.

### **Fields**

#### **1. Legal Company Name**
- **What it is:** Your organization's full legal entity name
- **Examples:**
  - "Contoso Corporation"
  - "Acme LLC"
  - "TechCorp Inc."
- **Used in:**
  - License agreement warranty disclaimers
  - Liability limitation clauses
  - Legal contact information

#### **2. Company Email Domain**
- **What it is:** Your organization's email domain (without `support@` prefix)
- **Examples:**
  - "contoso.com"
  - "acme.org"
  - "techcorp.net"
- **Used in:**
  - Support email links (`support@yourdomain.com`)
  - Contact information in About window
  - HTML export functions

#### **3. Support Phone (Optional)**
- **What it is:** Contact phone or message for support
- **Examples:**
  - "1-800-SUPPORT"
  - "Contact your IT department"
  - "+1 (555) 123-4567"
- **Used in:**
  - About window contact section
  - HTML legal document exports

### **Live Preview Pane**

The right side of the screen shows a **real-time preview** of how your branding will appear:

- **Warranty Disclaimer Preview:** See how company name appears in legal text
- **Contact Information Preview:** View formatted contact details
- **Files Affected List:** Shows which files will be modified

### **Actions**

#### **✓ Apply Changes**

1. Fill in all required fields
2. Review the live preview
3. Click **"✓ Apply Changes"**
4. Confirm the changes in the dialog
5. Wait for success message

**What happens:**
- Automatic backups are created (in `%APPDATA%\NecessaryAdminTool\Backups\WhiteLabel_TIMESTAMP\`)
- Replaces `{{COMPANY_NAME}}` and `{{COMPANY_DOMAIN}}` placeholders in:
  - `AboutWindow.xaml`
  - `AboutWindow.xaml.cs`
- Logs the change to `WhiteLabel_Changes.log`
- Displays success confirmation

**⚠️ Important:** Restart the application to see changes take effect.

#### **↺ Reset to Defaults**

- Resets all fields to placeholder values (`{{COMPANY_NAME}}`, `{{COMPANY_DOMAIN}}`)
- Does **not** modify files until you click "Apply Changes"
- Useful for testing or starting over

### **Backup System**

Every time you apply changes, the system **automatically creates backups**:

- **Location:** `%APPDATA%\NecessaryAdminTool\Backups\WhiteLabel_YYYYMMDD_HHMMSS\`
- **Files backed up:**
  - `AboutWindow.xaml.bak`
  - `AboutWindow.xaml.cs.bak`
  - `BACKUP_INFO.txt` (metadata)

**Restore from Backup:**

If something goes wrong, backups are automatically restored. Manual restore:

1. Navigate to backup folder
2. Copy `.bak` files
3. Rename to original filenames (remove `.bak`)
4. Replace the files in `NecessaryAdminTool\` directory

---

## ⚙️ Tab 2: Advanced Settings

### **🐛 Debug Mode**

**What it does:**
- Enables verbose logging throughout the application
- Adds diagnostic output to console and log files
- Useful for troubleshooting issues

**How to enable:**
1. Check the "Debug Mode" checkbox
2. Restart the application

**Saved to:** `SuperAdmin_Config.txt` in `%APPDATA%\NecessaryAdminTool\`

### **🔓 Unlock Hidden Features**

**What it does:**
- Reveals experimental and beta features in the UI
- Shows advanced options not visible in production mode

**How to enable:**
1. Check the "Unlock Hidden Features" checkbox
2. Restart the application

**Use cases:**
- Testing new features before release
- Accessing developer tools
- Early access to v1.1+ functionality

### **⚠️ Reset All Application Settings (DANGER)**

**What it does:**
- **Permanently deletes** all configuration files:
  - `NecessaryAdmin_Config_v2.xml`
  - `NecessaryAdmin_UserConfig.xml`
  - `NecessaryAdmin_DCConfiguration.xml`
- Resets app to factory defaults

**What is NOT affected:**
- Database and inventory data
- Backups
- SuperAdmin logs

**How to use:**
1. Click "Reset Settings"
2. Confirm first warning
3. Confirm second "This cannot be undone" warning
4. Application will close automatically
5. All settings reset on next launch

**⚠️ WARNING:** This action is **irreversible**! Create a backup first using "Export Config".

### **📤 Export Config**

**What it does:**
- Exports current SuperAdmin configuration to JSON file
- Includes:
  - White-label settings (company name, domain)
  - Advanced settings (debug mode, hidden features)
  - Timestamp and version info

**How to use:**
1. Click "📤 Export Config"
2. Choose save location
3. Default filename: `NecessaryAdmin_SuperAdmin_YYYYMMDD.json`

**Example JSON:**

```json
{
  "ExportDate": "2026-02-14 15:30:00",
  "Version": "1.0",
  "WhiteLabel": {
    "CompanyName": "Contoso Corporation",
    "CompanyDomain": "contoso.com",
    "SupportPhone": "1-800-SUPPORT"
  },
  "AdvancedSettings": {
    "DebugMode": true,
    "HiddenFeatures": false
  }
}
```

### **📥 Import Config**

**What it does:**
- Imports SuperAdmin configuration from JSON file
- Populates white-label fields automatically
- Applies advanced settings

**How to use:**
1. Click "📥 Import Config"
2. Select previously exported JSON file
3. Review imported values
4. Click "Apply Changes" to save

**Use cases:**
- Restore configuration from backup
- Deploy same settings across multiple installations
- Share white-label configuration between environments (Dev, Staging, Prod)

---

## 💾 Tab 3: Database Management

### **📊 Database Statistics**

Displays real-time database information:

- **Database Type:** SQLite, SQL Server, Access, or CSV
- **Total Computers:** Number of computer records
- **Database Size:** Size in MB

### **🔧 Optimize Database**

**What it does:**
- **SQLite:** Runs `VACUUM` to compact and defragment
- **SQL Server:** Rebuilds indexes
- **Access:** Performs `COMPACT & REPAIR`

**Benefits:**
- Reduces database file size
- Improves query performance
- Removes fragmentation

**When to use:**
- After large data imports
- Database feels slow
- After deleting many records

### **💾 Backup Database**

**What it does:**
- Creates a complete backup of the database file
- Allows you to choose save location

**Recommended schedule:**
- Before major operations (bulk delete, import)
- Weekly for active environments
- Before upgrading to new version

**File format:**
- SQLite: `.db` file copy
- SQL Server: `.bak` file
- Access: `.mdb` or `.accdb` copy

### **🗑️ Clear All Data (Dangerous!)**

**What it does:**
- **Permanently deletes ALL inventory data:**
  - All computer records
  - All scan history
  - All asset tags

**⚠️ WARNING:**
- **This CANNOT be undone!**
- **Create a backup first!**
- Use only when you want to start completely fresh

**Requires:**
- Two confirmation dialogs
- Not recommended for production environments

---

## ℹ️ Tab 4: System Information

### **Displays:**

```
=== APPLICATION INFORMATION ===
Application: NecessaryAdminTool
Version: 1.2602.0.0
Build Date: 2026-02-14 10:30:00
Installation Path: C:\...\NecessaryAdminTool\

=== SYSTEM INFORMATION ===
OS: Microsoft Windows NT 10.0.26200.0
Platform: Win32NT
64-Bit OS: True
64-Bit Process: True
Processor Count: 16
Machine Name: DESKTOP-ABC123
User Name: CONTOSO\john.doe
CLR Version: 4.0.30319.42000

=== CONFIGURATION PATHS ===
AppData: C:\Users\...\AppData\Roaming\NecessaryAdminTool
Backups: C:\Users\...\AppData\Roaming\NecessaryAdminTool\Backups
Temp: C:\Users\...\AppData\Local\Temp\

=== RUNTIME INFORMATION ===
Current Time: 2026-02-14 15:45:30
Uptime: 02:15:43
Working Set: 245 MB
```

**Use cases:**
- Troubleshooting
- Verify installation paths
- Check system requirements
- Support ticket information

---

## 🔐 Security Features

### **Access Control**

1. **Administrator Privileges Required**
   - SuperAdmin mode only opens if app is "Run as Administrator"
   - Prevents unauthorized access

2. **Password Protection**
   - Default: `08282021`
   - Change in production!
   - Failed attempts are logged

3. **Audit Logging**

All SuperAdmin actions are logged to:

- **`SuperAdmin_Access.log`**
  ```
  [2026-02-14 15:30:00] User: CONTOSO\john.doe | Machine: DESKTOP-ABC123
  ```

- **`WhiteLabel_Changes.log`**
  ```
  [2026-02-14 15:32:45] User: CONTOSO\john.doe | Company: Contoso Corporation | Domain: contoso.com
  ```

### **Automatic Backups**

- Created before every file modification
- Timestamped folders
- Includes metadata (user, machine, timestamp)
- Restored automatically on error

---

## 🎯 Common Use Cases

### **Use Case 1: White-Label for New Customer**

**Scenario:** You need to deploy NecessaryAdminTool to "Acme Corporation".

**Steps:**

1. Press `Ctrl+Shift+Alt+S` → Enter password
2. Navigate to **🏷️ White-Label Branding** tab
3. Fill in:
   - Company Name: `Acme Corporation`
   - Domain: `acme.com`
   - Phone: `1-800-ACME-HELP`
4. Review live preview
5. Click **"✓ Apply Changes"** → Confirm
6. Close SuperAdmin window
7. Restart application
8. Verify About window shows "Acme Corporation"

**Time:** ~2 minutes

---

### **Use Case 2: Enable Debug Mode for Troubleshooting**

**Scenario:** User reports scanning issues, you need detailed logs.

**Steps:**

1. Press `Ctrl+Shift+Alt+S` → Enter password
2. Navigate to **⚙️ Advanced Settings** tab
3. Check **"🐛 Debug Mode"**
4. Restart application
5. Reproduce the issue
6. Check logs for detailed diagnostic output
7. Uncheck Debug Mode when done

---

### **Use Case 3: Backup Configuration Before Major Change**

**Scenario:** About to upgrade to v1.1, want to save current white-label settings.

**Steps:**

1. Press `Ctrl+Shift+Alt+S` → Enter password
2. Navigate to **⚙️ Advanced Settings** tab
3. Click **"📤 Export Config"**
4. Save as `Config_Backup_v1.0.json`
5. Perform upgrade
6. If needed, click **"📥 Import Config"** to restore

---

## ⚠️ Important Warnings

### **DO NOT:**

- ❌ Share the SuperAdmin password with unauthorized users
- ❌ Use "Clear All Data" without backing up first
- ❌ Modify files manually while SuperAdmin changes are pending
- ❌ Apply white-label changes without reviewing live preview

### **ALWAYS:**

- ✅ Run as Administrator
- ✅ Review live preview before applying
- ✅ Test changes in dev/staging before production
- ✅ Keep backup of configuration JSON files
- ✅ Change default password in production

---

## 🔧 Customization

### **Change SuperAdmin Password**

**File:** `MainWindow.xaml.cs`
**Line:** ~2679 (search for `correctPassword`)

```csharp
// Current (insecure - plain text)
string correctPassword = "08282021";

// Better (hashed with salt)
private const string CORRECT_PASSWORD_HASH = "YOUR_SHA256_HASH";

private string ComputeSHA256Hash(string input)
{
    using (var sha256 = System.Security.Cryptography.SHA256.Create())
    {
        byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input + "SALT"));
        return BitConverter.ToString(bytes).Replace("-", "").ToLower();
    }
}

// In password check:
if (ComputeSHA256Hash(enteredPassword) == CORRECT_PASSWORD_HASH) { ... }
```

### **Add Custom Settings to Export/Import**

**File:** `SuperAdminWindow.xaml.cs`

**Export (line ~1280):**

```csharp
string json = $@"{{
  ...existing fields...,
  ""CustomSettings"": {{
    ""MySetting"": ""{MyCustomValue}""
  }}
}}";
```

**Import (line ~1310):**

```csharp
var mySettingMatch = Regex.Match(json, @"""MySetting"":\s*""([^""]+)""");
if (mySettingMatch.Success) MyCustomValue = mySettingMatch.Groups[1].Value;
```

---

## 📞 Support

### **Access Logs**

All SuperAdmin activity is logged to:

```
C:\Users\[USERNAME]\AppData\Roaming\NecessaryAdminTool\
├── SuperAdmin_Access.log         (Who accessed SuperAdmin and when)
├── WhiteLabel_Changes.log         (What white-label changes were made)
└── SuperAdmin_Config.txt          (Current advanced settings)
```

### **Backup Locations**

```
C:\Users\[USERNAME]\AppData\Roaming\NecessaryAdminTool\Backups\
├── WhiteLabel_20260214_153000\    (Auto-created before changes)
│   ├── AboutWindow.xaml.bak
│   ├── AboutWindow.xaml.cs.bak
│   └── BACKUP_INFO.txt
```

### **Troubleshooting**

| Issue | Solution |
|-------|----------|
| "Access Denied" when pressing Ctrl+Shift+Alt+S | Run as Administrator |
| Password dialog doesn't appear | Check keyboard is working, try combination again |
| "File not found" error when applying changes | Files may have been moved - check installation path |
| Changes don't appear after applying | Restart the application |
| Backup restore failed | Manually copy `.bak` files from Backups folder |

---

## 📚 Related Documentation

- **[WHITELABEL_GUIDE.md](WHITELABEL_GUIDE.md)** - Manual white-label configuration (file editing)
- **[AUTO_UPDATE_GUIDE.md](AUTO_UPDATE_GUIDE.md)** - Auto-update tag system for version bumps
- **[README_COMPREHENSIVE.md](README_COMPREHENSIVE.md)** - Complete project documentation

---

## 🎓 Training Checklist

Before using SuperAdmin in production, ensure you:

- [ ] Understand the secret access method (Ctrl+Shift+Alt+S)
- [ ] Know the default password (08282021)
- [ ] Can navigate all 4 tabs
- [ ] Have tested white-label changes in dev environment
- [ ] Know where backups are stored
- [ ] Understand the danger of "Clear All Data" and "Reset Settings"
- [ ] Have changed the default password
- [ ] Have created a backup JSON export
- [ ] Know how to restore from backup if needed
- [ ] Understand audit logging and where logs are stored

---

**Document Version:** 1.0
**Last Updated:** February 14, 2026
**Maintained by:** Brandon Necessary
**Built with Claude Code** 🤖

