<!-- TAG: #AUTO_UPDATE_README #QUICK_README #VERSION_DISPLAY -->
<!-- FUTURE CLAUDES: This is the QUICK README - update version here -->
<!-- For COMPREHENSIVE docs, see README_COMPREHENSIVE.md -->
# NecessaryAdminTool Suite

**Version 1.0** (1.2602.0.0) - Enterprise IT Management Suite by Brandon Necessary

**📘 For complete documentation, see [README_COMPREHENSIVE.md](README_COMPREHENSIVE.md)**

---

## Overview

NecessaryAdminTool is a comprehensive Windows-based IT management application designed for enterprise environments. It provides administrators with powerful tools for remote system management, Active Directory fleet inventory, automated deployment, and integrated RMM (Remote Monitoring and Management) tool control across domain-joined computers.

## Key Features

### 🎨 **Modern UI Features**
- **Toast Notifications** - 245+ non-blocking toast notifications throughout the app
- **Command Palette** - Press Ctrl+K for keyboard-driven command execution (30+ commands)
- **Advanced Filtering** - Multi-criteria filtering with save/load presets, AND/OR logic, history tracking
- **Fluent Design** - Windows 11 native look with Mica materials and rounded corners
- **Dual Views** - Switch between Grid and Card view layouts (Ctrl+T)
- **Skeleton Loaders** - Smooth loading animations for better perceived performance
- **15+ Keyboard Shortcuts** - Rapid workflow execution without mouse
- **Semantic Colors** - Success (green), Info (blue), Warning (amber), Error (red)

### 🖥️ **Single System Inspector**
- Deep system scanning with WMI queries
- Real-time hardware and network information
- Battery status, uptime, and storage monitoring
- Service tag lookup and warranty checking

### 📊 **AD Fleet Inventory**
- Parallel domain-wide computer scanning
- Real-time progress tracking with online/offline counters
- Windows version tracking with color-coded status indicators
- BitLocker and TPM status reporting
- **Advanced Filtering System** - Filter by status, OS, name pattern, OU, RAM, last seen date
- **Filter Presets** - Save and reuse custom filter configurations
- **Quick Filters** - One-click filters for Online, Offline, Win11, Win7, Servers, Workstations
- **Filter History** - Track last 10 filter operations
- CSV export capabilities

### 🛠️ **Remote Management Tools**
- **File & Software:** Browse C$ shares, software inventory, hotfix lists, disk cleanup
- **Process & Service Management:** Remote process/service control, startup programs, scheduled tasks
- **Diagnostics & Repair:** Event logs, DISM+SFC repair, Windows Defender scans, network diagnostics
- **Remote Access:** RDP, Remote Assist, Remote Registry, Remote CMD (PsExec)
- **System Actions:** Force GPUpdate, flush DNS, system reboot

### 🚀 **Deployment Center**
- Push Windows Updates (drivers, firmware, patches)
- Feature updates (major OS in-place upgrades)
- Script synchronization to deployment server

### 🏥 **Domain Controller Health**
- Real-time DC topology monitoring
- Automatic DC selection and failover
- Multi-DC ping health checks
- Short-name cards with domain-suffix badge overlay
- Uniform card widths auto-sized to widest hostname

### ⏱️ **Background Auto-Scan Service**
- Windows Task Scheduler integration — runs `NecessaryAdminTool.exe /autoscan` on a schedule
- Fully headless (no GUI) — suitable for overnight or off-hours fleet inventory
- **Works with all 4 database types:** SQLite, SQL Server, Microsoft Access, CSV
- Discovers domain and DC automatically via Kerberos (no stored passwords needed)
- Queries all AD computer accounts via paged LDAP (no size limit)
- Pings each computer (truly async `SendPingAsync`, 2s timeout) for online/offline status
- WMI-enriches online computers: OS, RAM, CPU, disk (C:), BIOS serial, last logged-on user
- Up to 20 parallel scans with per-query 5s WMI timeout to prevent stalls
- Saves all results via the configured `IDataProvider` (same DB the UI reads from)
- Records scan history (start/end time, online count, offline count, duration)
- Writes a plain-text summary report to the Deployment Log Directory

### 🖥️ **Remote Control Integration (Version 7.0)**
- Unified RMM tool management from a single interface
- Support for 6 major remote control platforms:
  - ✅ AnyDesk (CLI integration)
  - ✅ ScreenConnect/ConnectWise Control (API + URL)
  - ✅ TeamViewer (CLI + API)
  - ✅ RemotePC (REST API)
  - ✅ Dameware (REST API)
  - ✅ ManageEngine (API)
- Security-first design (all integrations disabled by default)
- Secure credential storage via Windows Credential Manager
- Quick-launch buttons in main window
- Context menu integration in device inventory
- Dedicated Remote Control tab for configuration
- Connection confirmation dialogs
- Session history and recent targets tracking
- Audit logging for all remote sessions

### ⚙️ **Enhanced User Experience (Version 7.0)**
- **Connection Profiles**: Save and recall domain controller configurations
- **Bookmarks/Favorites**: Quick access to critical servers with folder organization
- **Font Size Control**: Adjustable UI scaling (0.8x - 2.0x)
- **Auto-Save System**: Automatic inventory backups every N minutes
- **Window Position Memory**: Remembers size and position between sessions
- **Recent Targets Dropdown**: Quick access to recently scanned systems
- **Export/Import All Settings**: Comprehensive backup and restore of all configurations

## System Requirements

- **OS:** Windows 10/11 (Administrator privileges required)
- **Framework:** .NET Framework 4.8.1
- **Domain:** Active Directory domain environment
- **Database:** Microsoft Access Database Engine (bundled with installer)
- **Minimum RAM:** 8GB (16GB+ recommended for large domain scans)
- **CPU:** 4+ cores (high-core systems benefit from parallel optimizations)

## Installation

### Quick Install (End Users)
1. Download `NecessaryAdminTool-Setup.msi`
2. Double-click to install
3. Follow the first-run setup wizard

### Enterprise Deployment
- **Silent Install:** `msiexec /i NecessaryAdminTool-Setup.msi /quiet /norestart`
- **Group Policy:** Deploy via GPO Software Installation
- **SCCM/Intune:** Use MSI as application package

📘 **For complete deployment documentation, see [INSTALLER_GUIDE.md](INSTALLER_GUIDE.md)**

### Building from Source
```powershell
# One-time setup (installs WiX Toolset)
.\install-wix.ps1

# Build installer
.\build-installer.ps1

# Output: Installer\Output\NecessaryAdminTool-{Version}-Setup.msi
```

📘 **For build automation details, see [BUILD_SCRIPTS_README.md](BUILD_SCRIPTS_README.md)**

## Performance Optimizations

NecessaryAdminTool v1.0 includes extensive multicore optimizations:

- **Parallel WMI Queries:** 11 independent queries run simultaneously per computer
- **Dynamic Scan Throttling:** Automatically scales parallel operations based on CPU cores and RAM
- **PLINQ Processing:** Process lists, service inventories, and event logs processed in parallel
- **Real-time Progress Tracking:** Live feedback with elapsed/estimated time

**Performance Gains:**
- Single computer scan: ~4x faster (8-10s → 2-3s)
- Domain scan (500 computers): ~3x faster (60-75min → 15-25min)
- CSV export (1000 computers): ~5x faster (1s → 0.2s)

See [OPTIMIZATIONS.md](OPTIMIZATIONS.md) for detailed technical information.

## Security Features

- **Role-Based Access Control:** Admin/standard user privilege separation
- **Secure Credential Storage:** Encrypted credential management with SecureString
- **Audit Logging:** All administrative actions logged to master log
- **Memory Wiping:** Credentials zeroed from RAM on logout using RtlSecureZeroMemory

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| **Ctrl+K** | Open Command Palette |
| **Ctrl+Shift+F** | Scan Domain (Fleet) |
| **Ctrl+S** | Scan Single Computer |
| **Ctrl+L** | Load AD Objects |
| **Ctrl+Alt+A** | Authenticate |
| **Ctrl+R** | Remote Desktop |
| **Ctrl+P** | PowerShell Remote |
| **Ctrl+T** | Toggle Card/Grid View |
| **Ctrl+`** | Toggle Terminal |
| **Ctrl+,** | Open Settings |

**Pro Tip:** Press **Ctrl+K** to open the Command Palette and search all available commands with fuzzy matching.

## Usage

### Initial Setup
1. Launch NecessaryAdminTool with administrator privileges
2. Authenticate with domain credentials
3. Select target Domain Controller from the dropdown
4. Begin scanning individual systems or entire domain

### Using the Command Palette
1. Press **Ctrl+K** to open the Command Palette
2. Type to search for commands using fuzzy matching
3. Use **↑↓** arrow keys to navigate results
4. Press **Enter** to execute, **ESC** to close
5. Access 25+ commands without leaving the keyboard

### Scanning Individual Systems
1. Enter hostname or IP in the "Target System Control" panel
2. Click **SCAN** to perform deep system inspection (or press **Ctrl+S**)
3. View results in the "Single System Inspector" tab
4. Use remote management tools from the right panel

### Domain Fleet Inventory
1. Navigate to the "AD Fleet Inventory" tab
2. Click **SCAN DOMAIN** to begin parallel scan (or press **Ctrl+Shift+F**)
3. Monitor real-time progress with online/offline counters
4. Export results to CSV for reporting

### Background Auto-Scan (Scheduled Task)
1. Open **Options → Background Service**
2. Click **ENABLE** to register the Windows Scheduled Task
3. Set the scan interval (default: every 2 hours)
4. Click **RUN NOW** to trigger an immediate headless scan
5. Check **Options → Database** to view updated inventory after the scan runs
6. Scan summary reports are saved to the configured **Deployment Log Directory**

**How it works headlessly:**
- The scheduled task calls `NecessaryAdminTool.exe /autoscan`
- The process discovers the domain controller via Kerberos (no credentials needed)
- All AD computers are queried, pinged, and WMI-scanned in up to 20 parallel threads
- Results are written to the same database the main UI reads from
- The process exits cleanly when the scan completes

### Viewing Modes
- **Grid View** - Traditional table layout (default)
- **Card View** - Visual cards with quick actions (toggle with **Ctrl+T**)
- Each mode has advantages: Grid for dense data, Cards for visual scanning

## Configuration Files

The following configuration files are stored in `%APPDATA%`:
- `NecessaryAdmin_Config_v2.xml` - Application configuration
- `NecessaryAdmin_UserConfig.xml` - User preferences
- `NecessaryAdmin_Debug.log` - Debug logging (warning-level events)
- `NecessaryAdmin_Runtime.log` - Runtime logging (info-level events)

## Architecture

- **Language:** C# (WPF)
- **UI Framework:** XAML with unified dark theme (see [THEME_SYSTEM.md](THEME_SYSTEM.md))
- **Query Technology:** WMI (Windows Management Instrumentation)
- **Parallelization:** Task.WhenAll, PLINQ, SemaphoreSlim throttling
- **Networking:** WinRM, PsExec, Remote PowerShell
- **Database:** Abstracted provider system (SQLite, SQL Server, Access, CSV)
- **Testing:** Comprehensive database testing system (see [DATABASE_TESTING.md](DATABASE_TESTING.md))

## Version History

### Version 1.0 (1.2602.0.0) - "Foundation" 🎉 **CURRENT**
**Release Date:** February 14, 2026
**CalVer Format:** Major.YYMM.Minor.Build

**Complete Rebrand:**
- Rebranded from ArtaznIT Suite to NecessaryAdminTool
- New identity, clean v1.0 start with professional CalVer versioning
- All legacy references removed

**All v7.x Features Included:**
- ✅ **RMM Integration Suite** - 6 platforms (AnyDesk, ScreenConnect, TeamViewer, RemotePC, Dameware, ManageEngine)
- ✅ **Dashboard Analytics** - Fleet health metrics, OS distribution charts, uptime rankings
- ✅ **Automated Remediation** - One-click fixes for common issues
- ✅ **Custom Script Executor** - PowerShell script library with categorization
- ✅ **Asset Tagging System** - Manual and auto-tagging with rules engine
- ✅ **Connection Profiles** - Save and recall DC configurations
- ✅ **Bookmarks/Favorites** - Organized server quick-access with folders
- ✅ **Enhanced UX** - Font scaling, window memory, auto-save, export/import settings
- ✅ **Performance Optimized** - Multicore parallel processing (3-4x faster scanning)
- ✅ **Security First** - Windows Credential Manager, SecureString, encrypted storage

**Background Auto-Scan (v2.x):**
- ✅ Windows Task Scheduler integration (enable via Options → Background Service)
- ✅ Headless fleet scan: AD query → ping → WMI → database, no UI required
- ✅ All 4 database types supported (SQLite, SQL Server, Access, CSV)
- ✅ Integrated Kerberos auth — no passwords stored for scan service
- ✅ Per-query 5s WMI timeout, `ManagementScope` + `ManagementObject` properly disposed
- ✅ Truly async `SendPingAsync` — no ThreadPool starvation on large fleets
- ✅ Scan summary written to Deployment Log Directory after each run

**Prepared for v1.1+ Enhancements:**
- Auto-update system (Squirrel.Windows)
- Encrypted database layer (SQLCipher with AES-256)

**Documentation:**
- 📘 [Unified Theme System](THEME_SYSTEM.md) - Complete theme architecture and customization guide
- 📘 [Database Testing System](DATABASE_TESTING.md) - Comprehensive database validation and testing
- 📘 [Performance Optimizations](OPTIMIZATIONS.md) - Multicore parallelization details

---

### Previous Versions (Legacy - ArtaznIT Suite)

**Version 7.2603.5.0** (Feb 2026) - Final ArtaznIT release
**Version 4.0** (Feb 2026) - Major performance optimizations
**Version 3.x** - Initial multicore support

## License

Copyright © 2026 Brandon Necessary. All rights reserved.

## Support

For issues, questions, or feature requests:
- GitHub Issues: https://github.com/brandon-necessary/NecessaryAdminTool/issues
- Contact: Brandon Necessary

---

**Built with Claude Code** 🤖
