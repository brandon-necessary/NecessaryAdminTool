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

## Usage

### Initial Setup
1. Launch NecessaryAdminTool with administrator privileges
2. Authenticate with domain credentials
3. Select target Domain Controller from the dropdown
4. Begin scanning individual systems or entire domain

### Scanning Individual Systems
1. Enter hostname or IP in the "Target System Control" panel
2. Click **SCAN** to perform deep system inspection
3. View results in the "Single System Inspector" tab
4. Use remote management tools from the right panel

### Domain Fleet Inventory
1. Navigate to the "AD Fleet Inventory" tab
2. Click **SCAN DOMAIN** to begin parallel scan
3. Monitor real-time progress with online/offline counters
4. Export results to CSV for reporting

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

**Prepared for v1.1+ Enhancements:**
- Auto-update system (Squirrel.Windows)
- Encrypted database layer (SQLCipher with AES-256)
- Windows Service for background scanning

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
