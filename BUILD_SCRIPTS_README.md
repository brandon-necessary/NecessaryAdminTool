# Build Scripts - Automated Installer Generation
<!-- TAG: #BUILD_AUTOMATION #POWERSHELL #README -->

## 📋 Overview

This document describes the **automated build scripts** that streamline installer creation for NecessaryAdminTool.

**Scripts:**
- ✅ `build-installer.ps1` - Main installer builder
- ✅ `install-wix.ps1` - WiX Toolset auto-installer

---

## 🎯 build-installer.ps1

### **Purpose:**
One-click MSI installer builder that automates the entire build process.

### **What It Does:**

```
1. Checks prerequisites (MSBuild, WiX, ACE)
   ↓
2. Builds app in Release mode
   ↓
3. Compiles WiX source (candle)
   ↓
4. Links WiX object (light)
   ↓
5. Outputs MSI to Installer\Output\
   ↓
6. Opens output folder
```

### **Usage:**

```powershell
# Basic usage
.\build-installer.ps1

# Specify version
.\build-installer.ps1 -Version "3.2602.0.0"

# Skip app rebuild (use existing binaries)
.\build-installer.ps1 -SkipBuild

# Verbose output
.\build-installer.ps1 -Verbose

# Combine parameters
.\build-installer.ps1 -Version "2.0.0.0" -Verbose
```

### **Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `-Version` | String | "3.2602.0.0" | Version number for MSI |
| `-SkipBuild` | Switch | False | Skip compiling app, use existing binaries |
| `-Verbose` | Switch | False | Show detailed WiX output |

### **Output:**

```
Installer\Output\NecessaryAdminTool-{Version}-Setup.msi
```

### **Example Output:**

```
========================================
  NecessaryAdminTool Installer Builder
  Version: 3.2602.0.0
========================================

[1/6] Checking prerequisites...
  ✓ MSBuild found
  ✓ WiX Toolset found
  ✓ ACE Database Engine found

[2/6] Building NecessaryAdminTool (Release)...
  ✓ Build successful

[3/6] Compiling WiX source (candle)...
  ✓ WiX source compiled

[4/6] Linking WiX object (light)...
  ✓ MSI linked successfully

========================================
  ✓ INSTALLER BUILD SUCCESSFUL!
========================================

Output:
  File: Installer\Output\NecessaryAdminTool-3.2602.0.0-Setup.msi
  Size: 48.73 MB

Next Steps:
  1. Test on clean VM
  2. Deploy: msiexec /i "..." /quiet
  3. Or double-click to install interactively
```

### **Error Handling:**

The script validates:
- ✅ MSBuild exists
- ✅ WiX Toolset installed
- ✅ ACE installer present (warns if missing)
- ✅ Release build succeeds
- ✅ WiX compilation succeeds

**Exit Codes:**
- `0` - Success
- `1` - Prerequisites missing
- Non-zero - Build failed (MSBuild/WiX error code)

---

## 🎯 install-wix.ps1

### **Purpose:**
Automatically downloads and installs WiX Toolset 3.11.

### **What It Does:**

```
1. Checks if WiX already installed
   ↓
2. Downloads WiX 3.11 from GitHub
   ↓
3. Runs silent installer
   ↓
4. Sets WIX environment variable
   ↓
5. Instructions to restart PowerShell
```

### **Usage:**

```powershell
# Run as Administrator
.\install-wix.ps1

# Silent install (no prompts)
.\install-wix.ps1 -Silent
```

### **Requirements:**
- **Administrator privileges** (needed to install software)
- **Internet connection** (downloads ~30MB installer)

### **Example Output:**

```
========================================
  WiX Toolset 3.11 Installer
========================================

[1/3] Downloading WiX Toolset 3.11...
  URL: https://github.com/wixtoolset/wix3/releases/...
  ✓ Downloaded

[2/3] Installing WiX Toolset...
  (This may take a few minutes)
  ✓ Installed

[3/3] Setting environment variable...
  ✓ WIX environment variable set

========================================
  ✓ WIX TOOLSET INSTALLED!
========================================

IMPORTANT: Restart PowerShell to use WiX commands

Next steps:
  1. Close and reopen PowerShell
  2. Run: .\build-installer.ps1
```

### **What Gets Installed:**

- **Location:** `C:\Program Files (x86)\WiX Toolset v3.11\`
- **Tools:**
  - `candle.exe` - WiX compiler
  - `light.exe` - WiX linker
  - `heat.exe` - Harvest tool
  - `torch.exe` - Patching tool
  - WiX Visual Studio extension files

- **Environment Variable:**
  - `%WIX%` = `C:\Program Files (x86)\WiX Toolset v3.11\`

---

## 🔄 Typical Workflow

### **First Time Setup:**

```powershell
# 1. Install WiX (one time)
.\install-wix.ps1

# 2. Restart PowerShell
exit  # Close and reopen

# 3. Download ACE installer (one time)
# https://www.microsoft.com/en-us/download/details.aspx?id=54920
# Save to: Installer\Dependencies\AccessDatabaseEngine_X64.exe

# 4. Build installer
.\build-installer.ps1
```

### **Subsequent Builds:**

```powershell
# Just run this every time you want to rebuild
.\build-installer.ps1
```

### **Version Updates:**

```powershell
# Increment version and rebuild
.\build-installer.ps1 -Version "3.2602.1.0"

# Quick rebuild without recompiling app
.\build-installer.ps1 -SkipBuild
```

---

## 🧪 Testing

### **Test Build Script:**

```powershell
# Dry run - check prerequisites only
.\build-installer.ps1 -SkipBuild -Verbose
```

### **Test MSI Installer:**

```powershell
# Test on clean Windows VM
# 1. Copy MSI to VM
# 2. Double-click to install
# 3. Verify:
#    - App installed to C:\Program Files\NecessaryAdminTool\
#    - Start Menu shortcut created
#    - App launches successfully
#    - Database setup wizard appears (first run)
```

### **Test Silent Install:**

```powershell
# Install silently with logging
msiexec /i NecessaryAdminTool-Setup.msi /quiet /l*v test-install.log

# Check log for errors
Get-Content test-install.log | Select-String "error|fail"

# Verify installation
Test-Path "C:\Program Files\NecessaryAdminTool\NecessaryAdminTool.exe"
```

---

## 🐛 Troubleshooting

### **build-installer.ps1 Errors:**

| Error | Cause | Solution |
|-------|-------|----------|
| "MSBuild not found" | VS not installed | Install Visual Studio 2022 |
| "WiX not found" | WiX not installed | Run `.\install-wix.ps1` |
| "ACE not found" | ACE installer missing | Download and save to Dependencies\ |
| "Build failed" | Code errors | Fix errors and retry |
| "Candle failed" | WiX XML syntax | Check Product.wxs syntax |
| "Light failed" | WiX linking | Check component GUIDs |

### **install-wix.ps1 Errors:**

| Error | Cause | Solution |
|-------|-------|----------|
| "Access denied" | Not admin | Run PowerShell as Administrator |
| "Download failed" | No internet | Connect and retry |
| "Install failed" | Installer error | Check Windows Event Log |
| "WiX still not found" | Env var not set | Restart PowerShell |

---

## 📊 Build Performance

| Step | Duration | Notes |
|------|----------|-------|
| Prerequisites check | ~1s | Fast validation |
| App build (Release) | 30-60s | Depends on CPU |
| WiX compile (candle) | ~5s | Fast XML processing |
| WiX link (light) | ~10s | Slower due to ICE validation |
| **Total** | **~45-75s** | **Full clean build** |

**Optimization:**
- Use `-SkipBuild` if app hasn't changed: **~15s total**
- Use `-sval` in light.exe to skip ICE validation: **~5s faster**

---

## 🎨 Customization

### **Add Custom Build Step:**

Edit `build-installer.ps1`:

```powershell
# After app build, before WiX compile
Write-Host "[2.5/6] Running custom step..." -ForegroundColor Yellow

# Your custom code here
& mycustomtool.exe

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Custom step failed" -ForegroundColor Red
    exit $LASTEXITCODE
}
```

### **Change Output Directory:**

Edit `build-installer.ps1`:

```powershell
# Change this line:
$OutputDir = Join-Path $InstallerDir "Release"  # Custom location
```

### **Add Version to MSI Filename:**

Already included! Output format:
```
NecessaryAdminTool-{Version}-Setup.msi
```

---

## 📚 Advanced Usage

### **Build Multiple Versions:**

```powershell
# Build for different channels
.\build-installer.ps1 -Version "1.0.0.0"  # Stable
.\build-installer.ps1 -Version "1.0.1-beta.1"  # Beta
.\build-installer.ps1 -Version "2.0.0-rc.1"  # Release Candidate
```

### **Automated CI/CD Build:**

```powershell
# GitHub Actions / Azure DevOps script
$version = $env:BUILD_VERSION
.\build-installer.ps1 -Version $version -Verbose

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed"
    exit $LASTEXITCODE
}

# Upload artifact
$msi = "Installer\Output\NecessaryAdminTool-$version-Setup.msi"
# ... upload to release/artifact storage
```

---

## 🏷️ Auto-Update Tags

- `#BUILD_AUTOMATION` - Build script code
- `#POWERSHELL` - PowerShell scripts
- `#WIX_BUILD` - WiX build process

---

**See also:**
- `Installer/README.md` - WiX installer details
- `INSTALLER_GUIDE.md` - Deployment guide
- `Product.wxs` - WiX installer definition
