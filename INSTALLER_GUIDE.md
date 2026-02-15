# WiX Installer & Update Control Guide
<!-- TAG: #AUTO_UPDATE_INSTALLER #WIX #ENTERPRISE #VERSION_1_0 -->
**Version:** 1.0 (1.2602.0.0)
**Last Updated:** February 14, 2026

---

## 🎯 Overview

This guide covers the **WiX installer** and **enterprise update control** features for NecessaryAdminTool.

### **Key Features:**
- ✅ Professional MSI installer with dependency bundling
- ✅ Granular update control via Registry/GPO
- ✅ Air-gapped environment support
- ✅ Silent deployment options
- ✅ Auto-update with Squirrel.Windows
- ✅ Update channels (Stable/Beta/Disabled)

---

## 📦 Building the Installer

### **Prerequisites:**

1. **WiX Toolset v3.11+**
   ```powershell
   # Download from: https://wixtoolset.org/
   # Or use Chocolatey:
   choco install wixtoolset
   ```

2. **WiX Visual Studio Extension**
   - Visual Studio → Extensions → Manage Extensions
   - Search: "WiX Toolset Visual Studio Extension"
   - Install and restart VS

3. **Dependencies:**
   - Download ACE Database Engine: https://www.microsoft.com/en-us/download/details.aspx?id=54920
   - Place `AccessDatabaseEngine_X64.exe` in `Installer\Dependencies\`

### **Build Steps:**

1. **Build main application (Release mode):**
   ```powershell
   cd C:\Users\brandon.necessary\source\repos\NecessaryAdminTool
   msbuild NecessaryAdminTool.sln /t:Build /p:Configuration=Release
   ```

2. **Build installer:**
   ```powershell
   cd Installer
   candle Product.wxs
   light -ext WixUIExtension -out NecessaryAdminTool-Setup.msi Product.wixobj
   ```

3. **Output:**
   - `NecessaryAdminTool-Setup.msi` (~50MB)

---

## 🚀 Installation

### **Interactive Install:**

```powershell
# Double-click the MSI or run:
msiexec /i NecessaryAdminTool-Setup.msi
```

**Installer will:**
1. Check .NET Framework 4.8.1
2. Install to `C:\Program Files\NecessaryAdminTool\`
3. Install ACE Database Engine (if not present)
4. Create Start Menu shortcut
5. Optionally create Desktop shortcut
6. Launch application on completion

### **Silent Install (Unattended):**

```powershell
# Install silently
msiexec /i NecessaryAdminTool-Setup.msi /quiet /norestart

# Install silently with logging
msiexec /i NecessaryAdminTool-Setup.msi /quiet /norestart /l*v install.log

# Install with custom directory
msiexec /i NecessaryAdminTool-Setup.msi /quiet INSTALLFOLDER="D:\Tools\NecessaryAdminTool"
```

### **Uninstall:**

```powershell
# Interactive uninstall
msiexec /x NecessaryAdminTool-Setup.msi

# Silent uninstall
msiexec /x NecessaryAdminTool-Setup.msi /quiet /norestart
```

---

## 🔒 Update Control - Enterprise Configuration

### **Method 1: Group Policy (Recommended)**

**Create GPO to control updates:**

1. **Open Group Policy Management**
2. **Create new GPO:** "NecessaryAdminTool - Update Policy"
3. **Edit GPO → Computer Configuration → Preferences → Windows Settings → Registry**
4. **Add new Registry Item:**

   ```
   Key Path:  HKLM\Software\NecessaryAdminTool\Updates

   Value Name: EnableAutoUpdates
   Value Type: REG_DWORD
   Value Data: 0 (Disabled) or 1 (Enabled)

   Value Name: CheckFrequencyHours
   Value Type: REG_DWORD
   Value Data: 24 (hours between checks)

   Value Name: UpdateChannel
   Value Type: REG_SZ
   Value Data: stable | beta | disabled
   ```

5. **Link GPO to OU** containing target computers
6. **Run `gpupdate /force`** on target machines

**Example GPO Settings:**

| Setting | Value | Purpose |
|---------|-------|---------|
| `EnableAutoUpdates` | `0` | Disable all auto-updates |
| `CheckFrequencyHours` | `168` | Check weekly (7 days) |
| `UpdateChannel` | `stable` | Only production releases |

---

### **Method 2: Registry (Manual)**

**Set via PowerShell:**

```powershell
# Disable auto-updates
Set-ItemProperty -Path "HKLM:\Software\NecessaryAdminTool\Updates" `
                 -Name "EnableAutoUpdates" -Value 0 -Type DWord

# Set check frequency to weekly
Set-ItemProperty -Path "HKLM:\Software\NecessaryAdminTool\Updates" `
                 -Name "CheckFrequencyHours" -Value 168 -Type DWord

# Set to stable channel
Set-ItemProperty -Path "HKLM:\Software\NecessaryAdminTool\Updates" `
                 -Name "UpdateChannel" -Value "stable" -Type String
```

**Set via Registry Editor (regedit):**

1. Navigate to: `HKEY_LOCAL_MACHINE\SOFTWARE\NecessaryAdminTool\Updates`
2. Create key if it doesn't exist
3. Add DWORDs/Strings as needed

---

### **Method 3: Marker File (Air-Gapped)**

**Create `.no-updates` file in installation directory:**

```powershell
# Disable updates via file marker
New-Item -Path "C:\Program Files\NecessaryAdminTool\.no-updates" -ItemType File -Force

# Content (optional):
Set-Content -Path "C:\Program Files\NecessaryAdminTool\.no-updates" `
            -Value "Auto-updates disabled for air-gapped deployment - $(Get-Date)"
```

**App will check for this file on startup and skip all update checks if present.**

---

### **Method 4: User Preference (In-App)**

**Users can disable updates from SuperAdmin window:**

1. Open SuperAdmin (Ctrl+Shift+Alt+S)
2. Navigate to: Advanced Settings → Updates
3. Uncheck "Enable automatic updates"
4. Click "Save Settings"

**Note:** User setting can be overridden by Registry/GPO.

---

## 🎛️ Update Control Priority (Hierarchy)

Updates are checked in this order:

```
1. Registry (HKLM) - Highest priority (GPO)
   ↓
2. Registry (HKCU) - User override
   ↓
3. Marker File (.no-updates) - Deployment flag
   ↓
4. App Settings - User preference
   ↓
5. Default: ENABLED
```

**Example:**
- GPO sets `HKLM\EnableAutoUpdates = 0` → Updates **DISABLED** (cannot be overridden)
- User sets app setting to enabled → Updates **STILL DISABLED** (GPO wins)
- Remove GPO setting → User setting applies

---

## 📊 Update Channels

| Channel | Description | Use Case |
|---------|-------------|----------|
| **stable** | Production releases only | Production environments |
| **beta** | Pre-release builds | Testing environments |
| **disabled** | No updates | Air-gapped networks |

**Set via Registry:**

```powershell
Set-ItemProperty -Path "HKLM:\Software\NecessaryAdminTool\Updates" `
                 -Name "UpdateChannel" -Value "stable"
```

---

## 🔄 How Auto-Updates Work

### **Update Flow:**

```
APP LAUNCH
   ↓
Check Registry/Settings
   ↓
Updates Enabled? ──NO──→ SKIP
   ↓ YES
Check Last Update Time
   ↓
Frequency Elapsed? ──NO──→ SKIP
   ↓ YES
Query GitHub Releases
   ↓
New Version Available? ──NO──→ SKIP
   ↓ YES
Download in Background (Delta)
   ↓
Show Notification
   ↓
User Clicks "Update Now"
   ↓
Apply Update & Restart
```

### **Update Frequency:**

Default: **24 hours**

Configure via Registry:
```powershell
# Daily
Set-ItemProperty -Path "HKLM:\Software\NecessaryAdminTool\Updates" `
                 -Name "CheckFrequencyHours" -Value 24

# Weekly
Set-ItemProperty -Path "HKLM:\Software\NecessaryAdminTool\Updates" `
                 -Name "CheckFrequencyHours" -Value 168

# Monthly
Set-ItemProperty -Path "HKLM:\Software\NecessaryAdminTool\Updates" `
                 -Name "CheckFrequencyHours" -Value 720
```

---

## 📝 Deployment Scenarios

### **Scenario 1: Enterprise with Controlled Updates**

**Requirements:**
- IT controls all updates
- Users cannot enable updates
- Check monthly for updates

**Configuration:**

```powershell
# Via GPO
HKLM\Software\NecessaryAdminTool\Updates
   EnableAutoUpdates = 0
   CheckFrequencyHours = 720
   UpdateChannel = "stable"
```

**Result:** Updates disabled, IT must manually deploy updates.

---

### **Scenario 2: Air-Gapped Network**

**Requirements:**
- No internet access
- Updates via manual file transfer

**Configuration:**

```powershell
# Create marker file
New-Item -Path "C:\Program Files\NecessaryAdminTool\.no-updates" -ItemType File

# Or via registry
Set-ItemProperty -Path "HKLM:\Software\NecessaryAdminTool\Updates" `
                 -Name "EnableAutoUpdates" -Value 0
```

**Update Process:**
1. Download MSI on internet-connected machine
2. Transfer to air-gapped network
3. Run: `msiexec /i NecessaryAdminTool-v1.1-Setup.msi /quiet`

---

### **Scenario 3: SMB with Auto-Updates**

**Requirements:**
- Small business, limited IT staff
- Auto-update weekly
- Stable releases only

**Configuration:**

```powershell
# Enable updates, weekly checks
HKLM\Software\NecessaryAdminTool\Updates
   EnableAutoUpdates = 1
   CheckFrequencyHours = 168
   UpdateChannel = "stable"
```

**Result:** App checks weekly, downloads in background, notifies users.

---

### **Scenario 4: Testing Environment**

**Requirements:**
- Test beta releases
- Auto-update daily

**Configuration:**

```powershell
HKLM\Software\NecessaryAdminTool\Updates
   EnableAutoUpdates = 1
   CheckFrequencyHours = 24
   UpdateChannel = "beta"
```

**Result:** Gets pre-release builds for testing.

---

## 🛠️ Troubleshooting

### **Updates Not Working:**

1. **Check if updates are enabled:**
   ```powershell
   Get-ItemProperty -Path "HKLM:\Software\NecessaryAdminTool\Updates" -Name "EnableAutoUpdates"
   ```

2. **Check marker file:**
   ```powershell
   Test-Path "C:\Program Files\NecessaryAdminTool\.no-updates"
   ```

3. **Check last update time:**
   ```powershell
   # Open app, go to Help → About
   # Look for "Last Update Check" timestamp
   ```

4. **Force update check:**
   - Open app
   - Help → Check for Updates
   - Bypasses frequency throttling

---

### **Installer Fails:**

**Error: ".NET Framework 4.8.1 required"**
- Download: https://dotnet.microsoft.com/download/dotnet-framework/net481
- Install, then retry

**Error: "Access denied"**
- Run installer as Administrator
- Right-click → "Run as administrator"

**Error: "ACE Database Engine install failed"**
- Download manually: https://www.microsoft.com/en-us/download/details.aspx?id=54920
- Install AccessDatabaseEngine_X64.exe
- Retry installer

---

## 📚 Reference

### **Registry Keys:**

| Key | Type | Values | Default |
|-----|------|--------|---------|
| `EnableAutoUpdates` | DWORD | 0=Disabled, 1=Enabled | 1 |
| `CheckFrequencyHours` | DWORD | Hours (1-720) | 24 |
| `UpdateChannel` | String | stable, beta, disabled | stable |
| `InstalledVersion` | String | X.X.X.X | (current) |

### **Marker Files:**

| File | Location | Purpose |
|------|----------|---------|
| `.no-updates` | Installation directory | Disable all updates |

### **Application Settings:**

| Setting | Type | Purpose |
|---------|------|---------|
| `DisableAutoUpdates` | Boolean | User preference to disable updates |
| `LastUpdateCheck` | String (ISO8601) | Timestamp of last check |

---

## 🤖 Auto-Update Tags

For future maintenance, the following tags are used:

- `#AUTO_UPDATE_INSTALLER` - Installer configuration
- `#WIX_INSTALLER` - WiX XML files
- `#UPDATE_CONTROL` - Update control logic
- `#ENTERPRISE_POLICY` - GPO/Registry settings
- `#SQUIRREL` - Squirrel.Windows integration

---

**Built with Claude Code** 🤖
