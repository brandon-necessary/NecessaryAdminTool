# NecessaryAdminTool - Claude Code Instructions
<!-- TAG: #PROJECT_INSTRUCTIONS #CLAUDE_AI #VERSION_3_0 #AUTO_UPDATE_VERSION -->
**Version:** 3.1.0 (3.2603.6.0)
**Last Updated:** March 6, 2026

---

## Project Overview

**NecessaryAdminTool** is an enterprise-grade Windows system administration tool built with WPF (.NET Framework 4.8.1).

**Core Purpose:** Single system inspection (WMI/CIM), Active Directory fleet inventory, remote management (6 RMM platforms), asset tagging/bookmarks, PowerShell deployment scripts, database persistence.

**Key Stats:**
- **Framework:** .NET 4.8.1 (WPF) | **Language:** C#
- **Architecture Score:** 89/100 | **Managers:** 13 | **Data Providers:** 5
- **UI Components:** 11 (Fluent theme, Toast, Command Palette, KPI Cards, Heatmap, etc.)
- **Logging:** 701+ LogManager calls | **Tooltips:** 93+
- **Installer:** WiX 3.11 (MSI + EXE bundle) | **Auto-Updates:** Squirrel.Windows

**Two Projects in Solution:**
- `NecessaryAdminTool/` — Main WPF application (admin console)
- `NecessaryAdminAgent/` — Lightweight TCP agent deployed to target machines (data collection fallback before WMI)

---

## Architecture

```
Layer 1: Data Access     → IDataProvider (26 methods), DataProviderFactory (SQLite/SQL Server/Access/CSV)
Layer 2: Business Logic  → 13 Managers (LogManager, UpdateManager, RemoteControlManager, etc.)
Layer 3: Security        → SecureCredentialManager, EncryptionKeyManager, SecurityValidator
Layer 4: Configuration   → Settings.Default, SettingsManager, SecureConfig
Layer 5: Logging         → LogManager (file + in-memory, thread-safe, 30-day retention)
Layer 6: Presentation    → Fluent theme (35+ resources), 5 windows, 11 UI components
```

**Architecture Docs:** See `CLAUDE.md` sections below + `MODULAR_UI_ENGINE.md` in `UI/`

---

## Version Engine (LogoConfig)
<!-- TAG: #VERSION_SYSTEM #VERSION_ENGINE #AUTO_UPDATE_VERSION #CALVER -->

**ALL version numbers derive from `AssemblyInfo.cs` via `LogoConfig` — NEVER hardcode versions.**

```
AssemblyInfo.cs → LogoConfig → MainWindow / SetupWizard / AboutWindow / All Displays
```

```csharp
LogoConfig.VERSION              // "v3.2603.6" (CalVer: Major.YYMM.DD)
LogoConfig.FULL_VERSION         // "v3.2603.6.0"
LogoConfig.USER_AGENT_VERSION   // "3.2603.6"
LogoConfig.COMPILED_DATE_SHORT  // from assembly timestamp
LogoConfig.COMPANY_NAME         // "NecessaryAdmin"
LogoConfig.PRODUCT_NAME         // "NecessaryAdminTool"
LogoConfig.COPYRIGHT            // Full copyright string with year
```

**To update version:** Edit `AssemblyInfo.cs` — that's it. All UI updates automatically.
**CalVer format:** `Major.YYMM.DD.Rev` — e.g., `3.2603.6.0` = v3, March 6 2026, revision 0
  - `Major` = product major version (3)
  - `YYMM` = two-digit year + two-digit month (2603 = March 2026)
  - `DD` = two-digit day of month (06 → stored as 6)
  - `Rev` = incrementing revision on same day (0, 1, 2...)
  - All components stay within .NET 16-bit limit (max 65535) ✓

**WiX version** is a separate simplified format (`Major.Minor.Patch`, each < 256) — increment Patch for fixes, Minor for features or new month. WiX cannot use CalVer directly due to the 255 Minor/Major limit.

**When bumping version, also update:** `Product.wxs` (WiX format: `3.1.0`), `build-msi-direct.ps1`, `build-installer.ps1`, `CLAUDE.md` (both header + footer), and any `*.md` docs with version headers.

---

## UI Engine (v3.0)
<!-- TAG: #AUTO_UPDATE_UI_ENGINE #MODULAR_DESIGN #FLUENT_SYSTEM -->

### File Structure
```
NecessaryAdminTool/
├── UI/
│   ├── Components/
│   │   ├── CommandPalette.xaml      # Ctrl+K command launcher
│   │   ├── SkeletonLoader.xaml      # Loading shimmer effect
│   │   ├── ComputerCard.xaml        # Card view layout (Ctrl+T)
│   │   ├── KpiCard.xaml             # Dashboard KPI with sparkline + animated transitions
│   │   ├── SparklineControl.xaml    # Reusable Polyline chart
│   │   ├── DeviceHeatmap.xaml       # Fleet health heatmap grid
│   │   ├── FilterBar.xaml           # Global filter strip (Ctrl+F)
│   │   ├── ActivityFeed.xaml        # Event timeline with severity toggles
│   │   └── BreadcrumbBar.xaml       # Breadcrumb navigation
│   ├── Themes/
│   │   └── Fluent.xaml              # Fluent Design resources (35+ keys, 3 density styles)
│   └── Converters/
│       ├── StatusToColorConverter.cs     # Status → color
│       ├── HealthToColorConverter.cs     # ONLINE/OFFLINE/WARNING → color
│       ├── StatusToTextConverter.cs      # Status → emoji + text
│       └── BoolToVisibilityConverter.cs  # Bool → visibility
├── Helpers/
│   └── GridLengthAnimation.cs       # AnimationTimeline for detail drawer slide
├── Managers/
│   └── UI/
│       └── ToastManager.cs          # Toast notification manager (245+ calls)
└── Models/
    └── UI/
        ├── ToastNotification.cs     # Toast data model
        ├── CommandItem.cs           # Command palette item model
        ├── KpiCardData.cs           # KPI card data model
        └── ActivityEvent.cs         # Activity feed event model
```

### Keyboard Shortcuts (12 Total)

| Shortcut | Action |
|----------|--------|
| **Ctrl+K** | Command Palette |
| **Ctrl+F** | Toggle Filter Bar |
| **Ctrl+Shift+F** | Scan Domain (Fleet) |
| **Ctrl+S** | Scan Single Computer |
| **Ctrl+L** | Load AD Objects |
| **Ctrl+Alt+A** | Authenticate |
| **Ctrl+R** | Remote Desktop |
| **Ctrl+P** | PowerShell Remote |
| **Ctrl+T** | Toggle Card/Grid View |
| **Ctrl+`** | Toggle Terminal |
| **Ctrl+,** | Open Settings |
| **Ctrl+Shift+Alt+S** | SuperAdmin Panel |

### Dashboard (v3.0)
- **7 KPI Cards** with sparklines: Total Devices, Online, Offline, Health Score, Compliance %, Last Scan, DB Size
- **Animated value transitions** (60fps ease-out cubic, 350ms)
- **Device Health Heatmap** (24x24 colored tiles, 1000 device cap)
- **Activity Feed** (chronological events with severity toggles, 200 cap)

### Fleet Enhancements (v3.0)
- **Status Bar** with live pulsing indicator + fleet counts (60s auto-refresh)
- **Global Filter Bar** (OS Version, Status, Scan Period) — Ctrl+F toggle
- **Detail Drawer** (slide-in panel, 0→350px GridLength animation)
- **Row Hover Actions** (RDP, PowerShell, C$, Pin — visible on DataGridRow hover)
- **Breadcrumb Navigation** (updates on filter/selection changes)
- **Display Density** toggle (Compact 22px / Normal 28px / Comfortable 36px)

### Key Patterns
```csharp
// Toast notifications (use instead of MessageBox)
ToastManager.ShowSuccess("Operation completed");
ToastManager.ShowError("Failed to connect", "Retry", () => { /* action */ });

// Fluent theme resources
<Border Background="{StaticResource MicaBrush}" CornerRadius="{StaticResource FluentCornerRadius}">
    <TextBlock Foreground="{StaticResource SuccessBrush}" FontSize="{StaticResource FontH2}"/>
</Border>
```

---

## NecessaryAdminAgent
<!-- TAG: #NAT_AGENT #FLEET_SCAN -->

Lightweight TCP service deployed to target machines. Falls back to WMI if agent unavailable.

- **Protocol:** Line-delimited JSON over raw TCP (internal LAN trust boundary)
- **Commands:** PING, GET_SYSTEM_INFO, GET_PROCESSES, GET_SERVICES, GET_NETWORK, GET_INSTALLED
- **Registry config:** `HKLM\SOFTWARE\NecessaryAdminTool\Agent` (Token + Port)
- **Log:** `C:\ProgramData\NecessaryAdminTool\Agent\agent.log`
- **Security:** Constant-time token comparison, base64 auth, no secrets in responses
- **Usage pattern:** `await NatAgentClient.GetSystemInfoAsync(host) ?? await WmiScanner...`
- **Settings:** `AgentToken` (string) + `AgentPort` (int, default 443) in `Properties/Settings`

---

## Logging
<!-- TAG: #LOGGING #VERBOSE_LOGGING -->

**Location:** `%APPDATA%\NecessaryAdminTool\Logs\NAT_YYYY-MM-DD.log` | **Retention:** 30 days

```csharp
LogManager.LogInfo(string message)
LogManager.LogWarning(string message)
LogManager.LogError(string message, Exception ex)
LogManager.LogDebug(string message)  // DEBUG builds only
```

**Required pattern for all new methods:**
```csharp
LogManager.LogInfo($"Method() - START");
var sw = Stopwatch.StartNew();
try {
    // work
    LogManager.LogInfo($"Method() - SUCCESS - {sw.ElapsedMilliseconds}ms");
} catch (Exception ex) {
    LogManager.LogError("Method() - FAILED", ex);
    throw;
}
```

**NEVER log:** passwords, encryption keys, full connection strings, API keys.

---

## Tag System
<!-- TAG: #AUTO_UPDATE_UI_ENGINE #TAG_SYSTEM -->

All code is tagged for discoverability. **Always search tags before modifying tagged systems.**

| Tag Category | Tag | Purpose |
|--------------|-----|---------|
| Version | `#AUTO_UPDATE_VERSION` | Version numbers, CalVer tracking |
| Installer | `#AUTO_UPDATE_INSTALLER` | WiX, build scripts, update control |
| Database | `#AUTO_UPDATE_DATABASE` | Providers, schema, setup wizard |
| UI Engine | `#AUTO_UPDATE_UI_ENGINE` | All UI components and theme |
| Docs | `#AUTO_UPDATE_README`, `#AUTO_UPDATE_FAQ` | Documentation files |
| Security | `#SECURITY_CRITICAL` | Auth, credentials, encryption |
| Config | `#CONFIGURABLE_OPTIONS` | User settings |
| DPI | `#DPI_REQUIRED_PROPERTIES` | High-DPI rendering |

**Search tags:** `Grep pattern="#AUTO_UPDATE_VERSION" glob="*.{cs,xaml,md}"`
**Find FUTURE CLAUDES notes:** `Grep pattern="FUTURE CLAUDES" glob="*.{cs,xaml,md,ps1,wxs}"`

---

## High-DPI Support
<!-- TAG: #DPI_REQUIRED_PROPERTIES -->

**ALL new windows MUST include these properties:**

```xml
<!-- XAML windows -->
<Window UseLayoutRounding="True" SnapsToDevicePixels="True"
        TextOptions.TextFormattingMode="Display" TextOptions.TextRenderingMode="ClearType">
```

```csharp
// C#-created windows
UseLayoutRounding = true;
SnapsToDevicePixels = true;
TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
```

**Manifest:** `<dpiAware>true</dpiAware>` + `<dpiAwareness>system</dpiAwareness>` (System DPI, not PerMonitorV2)

---

## PowerShell Scripts

| Script | Purpose | Key Features |
|--------|---------|--------------|
| `GeneralUpdate.ps1` | Windows Updates + Firmware | Partial-success handling (exit 0 for ME), WU scan retry, disk cleanup, restart prompt |
| `FeatureUpdate.ps1` | Major OS upgrades | Win10 22H2 fallback, NuGet auto-install, ISO support |
| `PreflightReboot.ps1` | Pre-deployment reboot | DB write at all exit paths, user notification |
| `WMIEnable.ps1` | Enable WMI/WinRM | Firewall rules, DCOM, ME logging |
| `AgentInstall.ps1` | Deploy NecessaryAdminAgent | UNC copy, service install, port verify |

**All scripts:** `$ErrorActionPreference = 'Stop'`, 20-column Master CSV schema, `shutdown.exe` full path, ME-compatible exit codes.

**Embedded in assembly:** GeneralUpdate + FeatureUpdate via `<EmbeddedResource>` in csproj.

---

## Build & Release

**Build both projects:**
```powershell
.\do_build.ps1  # Builds NecessaryAdminTool + NecessaryAdminAgent in Release
```

**Build MSI + EXE installer:**
```powershell
.\build-msi-direct.ps1  # WiX 3.11: MSI (IT/SCCM) + EXE (with .NET check)
```

**Output:** `Installer\Output\NecessaryAdminTool-3.0.0-Setup.msi` + `.exe`

**MSBuild path:** `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe`

**WiX version constraint:** Major < 256, Minor < 256, Build < 65535. Use `3.0.0` not CalVer.

---

## Common Tasks

### Add New Setting
1. `Properties/Settings.settings` — add `<Setting>` with `#CONFIGURABLE_OPTIONS` tag
2. `Properties/Settings.Designer.cs` — add property
3. `OptionsWindow.xaml` — add UI control with tooltip
4. `OptionsWindow.xaml.cs` — add load/save logic

### Add New Manager
1. Create `YourManager.cs` with `// TAG: #VERSION_3_0`
2. Static class for singletons, instance for stateful (`IDisposable`)
3. Add logging (entry/exit/error pattern)
4. Use `ConfigureAwait(false)` on all awaits

### Add New Window
1. Apply DPI properties (see High-DPI section above)
2. Use Fluent theme resources (`StaticResource MicaBrush`, etc.)
3. Add tooltips to all buttons
4. Add to csproj

---

## Security

- **Credentials:** Windows Credential Manager (native API), SecureString, RtlSecureZeroMemory
- **Database:** SQLite with SQLCipher (optional), machine-specific keys via EncryptionKeyManager
- **PowerShell:** `SecurityValidator.SanitizePowerShellInput()` + `-EncodedCommand` for user input
- **WMI:** `WmiConnectionPool` with pooled `ManagementScope` (NOT IDisposable)
- **Update control:** HKLM GPO > `.no-updates` file > HKCU > App settings
- **Agent:** Constant-time token comparison, no secrets in responses, port validation 1-65535

**NEVER commit:** credentials, API keys, user.config, bin/obj

---

## Key Patterns & Gotchas

- **`ManagementScope`** does NOT implement `IDisposable` — no `using()` around it
- **`SearchResult`** (DirectoryServices) is NOT IDisposable — `SearchResultCollection.Dispose()` covers it
- **`Application.Properties` namespace conflict** in `App.xaml.cs` — always use `NecessaryAdminTool.Properties.Settings.Default`
- **`out _` discard in lambda** fails in VS 18 compiler — use named typed variable instead
- **`SemaphoreSlim`** on OleDb (Access) and JSON file (CSV) providers for thread safety
- **`ConfigureAwait(false)`** on all awaits in manager/library code to avoid STA deadlock
- **Static class cleanup** — use `Shutdown()` method called from `Window_Closing` (not IDisposable)
- **PowerShell native commands** (netsh, shutdown.exe) don't throw on non-zero exit — check `$LASTEXITCODE`

---

## Quick Reference

| Item | Path |
|------|------|
| Source | `C:\Users\brandon.necessary\source\repos\NecessaryAdminTool\` |
| Logs | `%APPDATA%\NecessaryAdminTool\Logs\` |
| Config | `%APPDATA%\NecessaryAdminTool\user.config` |
| Database | `C:\ProgramData\NecessaryAdminTool\` (default) |
| Agent Log | `C:\ProgramData\NecessaryAdminTool\Agent\agent.log` |
| MSI Output | `Installer\Output\NecessaryAdminTool-3.0.0-Setup.msi` |

| Key Class | Purpose |
|-----------|---------|
| `LogManager` | Centralized logging |
| `DataProviderFactory` | Database abstraction (SQLite/SQL Server/Access/CSV) |
| `NatAgentClient` | TCP client for NecessaryAdminAgent |
| `WmiConnectionPool` | Pooled WMI scopes with timer cleanup |
| `SecurityValidator` | Input sanitization, rate limiting, script validation |
| `ToastManager` | Non-blocking notifications |
| `LogoConfig` | Version/branding constants (derives from AssemblyInfo) |

---

**Project Status:** Production (v3.1.0 / 3.2603.6.0) | **Architecture Score:** 89/100
**Last Updated:** March 6, 2026

**Built with Claude Code**
