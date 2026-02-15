# WiX Installer System
<!-- TAG: #AUTO_UPDATE_INSTALLER #WIX #MSI #README -->

## 📋 Overview

This directory contains the **WiX Toolset installer** definition for NecessaryAdminTool. The installer creates a professional MSI package that:

- ✅ Installs to `C:\Program Files\NecessaryAdminTool\`
- ✅ Bundles ACE Database Engine installer
- ✅ Creates Start Menu shortcuts
- ✅ Sets up registry keys for update control
- ✅ Supports silent deployment
- ✅ Provides proper uninstaller

---

## 📁 Directory Structure

```
Installer/
├── Product.wxs              # WiX installer definition (XML)
├── Dependencies/            # Bundled installers
│   └── AccessDatabaseEngine_X64.exe  # ACE driver (download separately)
├── Output/                  # Built MSI files
│   └── NecessaryAdminTool-{Version}-Setup.msi
├── obj/                     # Build artifacts (auto-generated)
└── README.md               # This file
```

---

## 🚀 Quick Start

### **Prerequisites:**

1. **WiX Toolset 3.11+**
   ```powershell
   # Run from project root:
   .\install-wix.ps1
   ```

2. **ACE Database Engine**
   - Download: https://www.microsoft.com/en-us/download/details.aspx?id=54920
   - Save as: `Installer\Dependencies\AccessDatabaseEngine_X64.exe`

3. **Visual Studio 2022** with MSBuild

### **Build Installer:**

```powershell
# From project root, run:
.\build-installer.ps1

# Output: Installer\Output\NecessaryAdminTool-{Version}-Setup.msi
```

---

## 🔧 Product.wxs Explained

### **Key Sections:**

#### **1. Prerequisites Check**
```xml
<!-- Requires .NET Framework 4.8.1 -->
<PropertyRef Id="NETFRAMEWORK481"/>
<Condition Message="Requires .NET Framework 4.8.1">
  <![CDATA[Installed OR NETFRAMEWORK481]]>
</Condition>
```

#### **2. Installation Directory**
```xml
<Directory Id="ProgramFiles64Folder">
  <Directory Id="INSTALLFOLDER" Name="NecessaryAdminTool">
    <!-- Application files installed here -->
  </Directory>
</Directory>
```

#### **3. Registry Keys for Update Control**
```xml
<RegistryKey Root="HKLM" Key="Software\NecessaryAdminTool\Updates">
  <RegistryValue Name="EnableAutoUpdates" Value="1" />
  <RegistryValue Name="CheckFrequencyHours" Value="24" />
  <RegistryValue Name="UpdateChannel" Value="stable" />
</RegistryKey>
```

#### **4. ACE Database Engine Bundling**
```xml
<Binary Id="ACEInstaller" SourceFile="Dependencies\AccessDatabaseEngine_X64.exe" />
<CustomAction Id="InstallACE" BinaryKey="ACEInstaller" Execute="deferred" />
```

---

## 🎛️ MSI Properties

You can customize installation via command-line properties:

```powershell
# Change install directory
msiexec /i Setup.msi INSTALLFOLDER="D:\Tools\NecessaryAdminTool"

# Disable auto-updates during install
msiexec /i Setup.msi ENABLE_AUTO_UPDATES=0

# Set update check frequency (hours)
msiexec /i Setup.msi UPDATE_CHECK_FREQUENCY=168

# Set update channel
msiexec /i Setup.msi UPDATE_CHANNEL=beta
```

---

## 📦 Silent Deployment

### **Install Silently:**
```powershell
msiexec /i NecessaryAdminTool-Setup.msi /quiet /norestart
```

### **Install with Logging:**
```powershell
msiexec /i NecessaryAdminTool-Setup.msi /quiet /l*v install.log
```

### **Uninstall Silently:**
```powershell
msiexec /x NecessaryAdminTool-Setup.msi /quiet /norestart
```

---

## 🏢 Enterprise Deployment

### **Group Policy Software Installation:**

1. Copy MSI to network share: `\\server\share\NecessaryAdminTool-Setup.msi`
2. Open **Group Policy Management**
3. Create/Edit GPO → **Computer Configuration** → Software Settings → **Software Installation**
4. Right-click → **New** → **Package**
5. Browse to MSI on network share
6. Deploy as: **Assigned** (installs on next reboot)

### **SCCM / Intune Deployment:**

Use the MSI as an application package with these settings:
- **Install command:** `msiexec /i NecessaryAdminTool-Setup.msi /quiet /norestart`
- **Uninstall command:** `msiexec /x {PRODUCT-CODE-GUID} /quiet /norestart`
- **Detection method:** File exists: `C:\Program Files\NecessaryAdminTool\NecessaryAdminTool.exe`

---

## 🔄 Updating the Installer

### **Change Version Number:**

Edit `Product.wxs`:
```xml
<?define ProductVersion = "1.2602.1.0" ?>  <!-- Update this -->
```

Then rebuild:
```powershell
.\build-installer.ps1 -Version "1.2602.1.0"
```

### **Add New Files:**

Edit `Product.wxs` in the `<ComponentGroup Id="ProductComponents">` section:
```xml
<Component Id="NewComponent" Guid="NEW-GUID-HERE" Win64="yes">
  <File Source="..\NecessaryAdminTool\bin\Release\NewFile.dll" />
</Component>
```

### **Modify Registry Keys:**

Edit the `<RegistryKey>` section in `Product.wxs`:
```xml
<RegistryValue Name="NewSetting" Value="DefaultValue" Type="string" />
```

---

## 🐛 Troubleshooting

### **"WiX not found"**
- Install WiX: `.\install-wix.ps1`
- Verify: `$env:WIX` should point to `C:\Program Files (x86)\WiX Toolset v3.11\`
- Restart PowerShell after install

### **"ACE installer not found"**
- Download from: https://www.microsoft.com/en-us/download/details.aspx?id=54920
- Place in: `Installer\Dependencies\AccessDatabaseEngine_X64.exe`
- Or skip bundling: Edit `Product.wxs` and remove ACE custom action

### **"Build failed" - Candle errors**
- Check XML syntax in `Product.wxs`
- Ensure all `<Component>` have unique `Id` attributes
- Verify file paths in `Source` attributes exist

### **"Build failed" - Light errors**
- ICE validation errors can usually be ignored: Add `-sval` flag
- Missing files: Check that Release build succeeded
- Duplicate GUIDs: Ensure all GUIDs are unique

---

## 📚 References

- **WiX Documentation:** https://wixtoolset.org/documentation/
- **WiX Tutorial:** https://www.firegiant.com/wix/tutorial/
- **MSI Best Practices:** https://docs.microsoft.com/en-us/windows/win32/msi/

---

## 🏷️ Auto-Update Tags

- `#AUTO_UPDATE_INSTALLER` - Installer system code
- `#WIX` - WiX Toolset configuration
- `#MSI` - MSI package configuration
- `#ENTERPRISE_DEPLOYMENT` - Enterprise deployment settings

---

**For complete deployment documentation, see:** `../INSTALLER_GUIDE.md`
