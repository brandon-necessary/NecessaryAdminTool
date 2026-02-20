# NecessaryAdminTool Code Tags Reference

This document serves as a centralized repository for all code tags used throughout the NecessaryAdminTool project. Tags make it easy to find and update related code across the entire codebase.

## How to Use Tags

Search for any tag (e.g., `#THEME_DIALOG`) in your IDE to find all related code locations. This ensures consistency when making changes to themes, branding, or other cross-cutting concerns.

---

## 🎨 THEME & BRANDING TAGS

### #THEME_DIALOG
**Purpose:** Identifies all themed dialog windows
**Location:** `MainWindow.xaml.cs` - `ThemedDialog` class and usage
**Description:** All error/info dialogs that use the zinc-orange gradient branding. Update here to change dialog appearance globally.

**Files:**
- `MainWindow.xaml.cs` (lines ~474-650) - ThemedDialog class definition
- `MainWindow.xaml.cs` (line ~6702) - ShowDCDiscoveryFailureDialog usage

**Usage Example:**
```csharp
// TAG: #THEME_DIALOG
ThemedDialog.ShowError(owner, title, message, details, reasons, actions);
```

---

### #THEME_COLORS
**Purpose:** Color scheme definitions for consistent branding
**Location:** `MainWindow.xaml.cs` - `ThemedDialog` class
**Description:** Defines the zinc-orange gradient color palette used throughout the application.

**Color Definitions:**
```csharp
// TAG: #THEME_COLORS
OrangePrimary = #FF8533 (RGB: 255, 133, 51)
OrangeDark    = #CC6B29 (RGB: 204, 107, 41)
ZincColor     = #A1A1AA (RGB: 161, 161, 170)
BgDark        = #1A1A1A (RGB: 26, 26, 26)
BgMedium      = #2D2D2D (RGB: 45, 45, 45)
```

**Files:**
- `MainWindow.xaml.cs` (lines ~488-492) - Color constant definitions
- `MainWindow.xaml.cs` (multiple locations) - Color usage in themed dialogs

---

### #THEME_LOGO
**Purpose:** Logo placements using LogoConfig system
**Location:** Various UI elements
**Description:** All locations where the NecessaryAdminTool logo/icon is displayed. Updates to LogoConfig automatically propagate here.

**Files:**
- `MainWindow.xaml.cs` (line ~543) - ThemedDialog header logo
- `MainWindow.xaml` (line ~508) - Main window top bar logo

**Usage Example:**
```csharp
// TAG: #THEME_LOGO
var logoIcon = LogoConfig.CreateIconPath();
logoIcon.Width = LogoConfig.MEDIUM_ICON_SIZE;
```

---

## 🔧 FUNCTIONAL TAGS

### #DC_DISCOVERY
**Purpose:** Domain Controller discovery, health monitoring, and history
**Location:** `MainWindow.xaml.cs` - DCManager class and DC history components
**Description:** All code related to discovering, caching, monitoring domain controllers, and viewing historical DC data.

**Features:**
- Automatic DC discovery with caching
- Real-time health monitoring with ping/latency
- Expandable DC history panel showing previously discovered DCs
- Color-coded status indicators (🟢 recent, 🟡 week old, 🟠 month old, ⚫ offline)

**Files:**
- `MainWindow.xaml.cs` (lines ~668-876) - DCManager class
- `MainWindow.xaml.cs` (lines ~6261-6500) - InitDCCluster method
- `MainWindow.xaml.cs` (line ~6702) - DC failure dialog
- `MainWindow.xaml.cs` (lines ~6890-6970) - DC history toggle and load methods
- `MainWindow.xaml.cs` (lines ~1678-1693) - DCHistoryItem class
- `MainWindow.xaml` (lines ~980-1045) - DC history expandable panel UI

**Usage Example:**
```csharp
// TAG: #DC_DISCOVERY
LoadDCHistory();  // Populate history panel with cached DCs
```

---

### #WMI_CONNECTION
**Purpose:** WMI connection pooling and management
**Location:** `MainWindow.xaml.cs` - WmiConnectionManager class
**Description:** Manages reusable WMI connections with timeout coordination.

**Files:**
- `MainWindow.xaml.cs` (lines ~312-472) - WmiConnectionManager class
- `MainWindow.xaml.cs` (line ~1866) - Initialization

---

### #CIM_CONNECTION
**Purpose:** CIM/WS-MAN connection pooling
**Location:** `MainWindow.xaml.cs` - CimSessionManager class
**Description:** Modern CIM session manager for faster remote management.

**Files:**
- `MainWindow.xaml.cs` (lines ~880-1050) - CimSessionManager class
- `MainWindow.xaml.cs` (line ~1867) - Initialization

---

### #SECURITY_VALIDATION
**Purpose:** Input validation and security checks
**Location:** `MainWindow.xaml.cs` - SecurityValidator class
**Description:** Validates hostnames, domain users, and prevents injection attacks.

**Files:**
- `MainWindow.xaml.cs` (lines ~180-280) - SecurityValidator class
- Used throughout codebase for input validation

---

## 📊 DATA MANAGEMENT TAGS

### #PINNED_DEVICES
**Purpose:** Pinned device monitoring system
**Location:** `MainWindow.xaml.cs` - PinnedDevice class and related methods
**Description:** Manages the list of devices pinned for continuous monitoring.

**Files:**
- `MainWindow.xaml.cs` (lines ~1602-1670) - PinnedDevice class
- `MainWindow.xaml.cs` (line ~5910) - Ctx_PinDevice_Click (add from inventory)
- `MainWindow.xaml.cs` (lines ~7780+) - LoadPinnedDevices, SavePinnedDevices

---

### #INVENTORY_SCAN
**Purpose:** AD computer inventory scanning
**Location:** `MainWindow.xaml.cs` - Inventory scan methods
**Description:** Scans Active Directory for computers and gathers system information.

**Files:**
- `MainWindow.xaml.cs` (lines ~2860-3050) - BtnInvScan_Click method
- `MainWindow.xaml.cs` (lines ~3150+) - GetSystemSpecsAsync method

---

## 🔒 SECURITY TAGS

### #SECURE_MEMORY
**Purpose:** Secure credential handling
**Location:** `MainWindow.xaml.cs` - SecureMemory class
**Description:** Zero-wipes credentials from memory using Windows APIs.

**Files:**
- `MainWindow.xaml.cs` (lines ~43-95) - SecureMemory class
- `MainWindow.xaml.cs` (lines ~1901-1903, 5743-5745) - Usage in logout/cleanup

---

### #AUTH_SYSTEM
**Purpose:** Authentication and authorization
**Location:** `MainWindow.xaml.cs` - Authentication methods
**Description:** Login dialog, credential validation, domain admin checks.

**Files:**
- `MainWindow.xaml.cs` (lines ~5738-5828) - ShowLoginDialog, PerformAuth
- `MainWindow.xaml.cs` (lines ~1947-2040) - CheckDomainAdminMembership

---

## 🎯 UI COMPONENT TAGS

### #CONTEXT_MENU
**Purpose:** Right-click context menus
**Location:** XAML and event handlers
**Description:** Context menu items for inventory grid, pinned devices, etc.

**Files:**
- `MainWindow.xaml` (lines ~1390-1423) - GridInventory context menu
- `MainWindow.xaml.cs` (lines ~5707-5735) - Context menu handlers

---

### #TERMINAL_OUTPUT
**Purpose:** Terminal/console output panel
**Location:** `MainWindow.xaml.cs` - AppendTerminal methods
**Description:** Debug and status output to the terminal panel.

**Files:**
- `MainWindow.xaml.cs` (lines ~6655-6730) - AppendTerminal, AppendDebugInfo
- Used throughout for logging visible to user

---

## 📝 LOGGING TAGS

### #LOG_MANAGER
**Purpose:** Centralized logging system
**Location:** `MainWindow.xaml.cs` - LogManager class
**Description:** File-based logging with debug/info/warning/error levels.

**Files:**
- `MainWindow.xaml.cs` (lines ~240-308) - LogManager class
- Used throughout entire codebase

---

## 🔄 ASYNC PATTERNS

### #ASYNC_VOID_FIX
**Purpose:** Locations where async void was converted to async Task
**Location:** Various async methods
**Description:** Methods that were fixed from async void to async Task for proper exception handling.

**Fixed Methods:**
- ShowLoginDialog() → async Task (line ~5959)
- InitDCCluster() → async Task (line ~6261)
- LoadPinnedDevices() → async Task (line ~7782)
- RefreshCurrentDevice() → async Task (line ~8787)

---

## 📦 CONFIGURATION TAGS

### #CONFIG_PERSISTENCE
**Purpose:** Configuration file save/load
**Location:** Various manager classes
**Description:** Persists settings to AppData XML files.

**Files:**
- `MainWindow.xaml.cs` (lines ~575-656) - DCManager LoadConfiguration/SaveConfiguration
- `MainWindow.xaml.cs` (lines ~2174-2200+) - User config load/save

---

## 🚀 ADDING NEW TAGS

When creating a new tag, follow this format:

1. **Choose a descriptive name:** Use UPPERCASE with underscores (e.g., `#NEW_FEATURE`)
2. **Add the tag in code:** Place as a comment near relevant code
   ```csharp
   // TAG: #NEW_FEATURE - Description of what this does
   public void MyNewFeature() { }
   ```
3. **Document it here:** Add a section to this file with:
   - Purpose
   - Location
   - Description
   - Files
   - Usage example (if applicable)

---

## 🔍 QUICK SEARCH GUIDE

| What to Update | Tag to Search |
|---------------|---------------|
| Dialog themes | `#THEME_DIALOG` |
| Color scheme | `#THEME_COLORS` |
| Logo/branding | `#THEME_LOGO` |
| DC discovery | `#DC_DISCOVERY` |
| Error handling | `#LOG_MANAGER` |
| Security validation | `#SECURITY_VALIDATION` |
| Async patterns | `#ASYNC_VOID_FIX` |
| Rebranding support | `#REBRANDING` |
| Deployment scripts | `#DEPLOYMENT_SCRIPT` |
| AppData paths | `#APPDATA_PATHS` |

---

### #REBRANDING
**Purpose:** Marks code that references product names, paths, or branding via LogoConfig
**Location:** LogoConfig class, LogManager, window titles
**Description:** All code centralized for easy white-label rebranding via LogoConfig constants.

### #DEPLOYMENT_SCRIPT
**Purpose:** PowerShell scripts deployed via ManageEngine or similar RMM platforms
**Location:** `Scripts/*.ps1`
**Files:**
- `Scripts/GeneralUpdate.ps1` - Windows Updates + Firmware
- `Scripts/FeatureUpdate.ps1` - Major OS upgrades (Feature Updates)
- `Scripts/PreflightReboot.ps1` - Pre-upgrade reboot handler
- `Scripts/WMIEnable.ps1` - WMI/WinRM firewall enablement
- `Scripts/AgentInstall.ps1` - NecessaryAdminAgent deployment

### #APPDATA_PATHS
**Purpose:** Application data directory references centralized in LogoConfig
**Location:** `LogoConfig.APPDATA_FOLDER`, `LogoConfig.AppDataPath`, `LogoConfig.LogDirectory`

---

**Last Updated:** 2026-02-20
**Maintained by:** NecessaryAdminTool Development Team
**File Location:** `C:\Users\brandon.necessary\source\repos\NecessaryAdminTool\TAGS_REFERENCE.md`
