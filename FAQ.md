# Frequently Asked Questions (FAQ)
<!-- TAG: #AUTO_UPDATE_FAQ #VERSION_1_0 #HELP_DOCUMENTATION -->
<!-- FUTURE CLAUDES: Update this FAQ with new common questions and solutions -->
<!-- Last Updated: 2026-02-14 | Version: 1.0 (1.2602.0.0) -->

---

## 📋 Table of Contents

- [Getting Started](#-getting-started)
- [Installation & Setup](#-installation--setup)
- [Database Questions](#-database-questions)
- [Performance & Optimization](#-performance--optimization)
- [Security & Credentials](#-security--credentials)
- [Modern UI Features](#-modern-ui-features)
- [Features & Functionality](#-features--functionality)
- [Troubleshooting](#-troubleshooting)
- [Advanced Topics](#-advanced-topics)
- [Licensing & Support](#-licensing--support)

---

## 🚀 Getting Started

### Q: What is NecessaryAdminTool?
**A:** NecessaryAdminTool is an enterprise-grade Windows IT management suite designed for Active Directory environments. It provides comprehensive tools for remote system management, fleet inventory, automated remediation, and integrated RMM control across domain-joined computers.

### Q: What are the system requirements?
**A:**
- **OS:** Windows 10/11 (Administrator privileges required)
- **Framework:** .NET Framework 4.8.1
- **Domain:** Active Directory domain environment
- **RAM:** 8GB minimum (16GB+ recommended for large domain scans)
- **CPU:** 4+ cores (high-core systems benefit from parallel optimizations)

### Q: Do I need to be a Domain Admin?
**A:** No, but it's recommended for full functionality:
- **Standard Domain User:** Can scan computers, view inventory (read-only features)
- **Local Admin:** Can use remote management tools on authorized computers
- **Domain Admin:** Full access to all features across the entire domain

### Q: How much does it cost?
**A:** See [Licensing & Support](#-licensing--support) section.

---

## 💾 Installation & Setup

### Q: How do I install NecessaryAdminTool?
**A:**
1. Download the latest release from [GitHub Releases](https://github.com/brandon-necessary/NecessaryAdminTool/releases)
2. Extract ZIP to `C:\Program Files\NecessaryAdminTool\`
3. Run `NecessaryAdminTool.exe` as Administrator
4. Complete the Setup Wizard on first run
5. Authenticate with your domain credentials

### Q: What happens on first run?
**A:** The Setup Wizard appears and guides you through:
1. **Database Type Selection** - Choose SQLite (recommended), SQL Server, Access, or CSV
2. **Database Location** - Default is `C:\ProgramData\NecessaryAdminTool\`
3. **Service Installation** - Optional background service for automatic scanning
4. **Scan Interval** - How often to run automatic scans (default: 2 hours)
5. **Database Testing** - Optional validation with "🧪 Test Database" button

### Q: Can I skip the Setup Wizard?
**A:** No, the Setup Wizard is required on first run to configure the database. However, all settings can be changed later from Options → Database Management.

### Q: Where are my settings stored?
**A:**
- **Application Settings:** `%AppData%\NecessaryAdminTool\`
- **Database:** `%ProgramData%\NecessaryAdminTool\` (default, user-configurable)
- **Encryption Keys:** Windows Credential Manager (secure OS storage)
- **Logs:** `%AppData%\NecessaryAdminTool\Logs\`

### Q: Can I use NecessaryAdminTool on a USB drive?
**A:** Yes! It's a portable application:
- Extract to USB drive
- Database stored wherever you configure (can be on the USB drive)
- Settings stored in `%AppData%` on the current machine
- No installation required (xcopy deployment)

---

## 🗄️ Database Questions

### Q: Which database should I choose?
**A:**

| If you need... | Choose |
|----------------|--------|
| **Zero configuration, fast, secure** | **SQLite** (Recommended) |
| **Multi-user, enterprise-scale, unlimited capacity** | **SQL Server** |
| **Excel integration, familiar UI** | **Microsoft Access** |
| **Portable, human-readable, fallback** | **CSV/JSON** |

**Recommendation:** Start with **SQLite**. It's encrypted (AES-256), fast, supports 100,000+ computers, and requires zero configuration.

### Q: Is my database encrypted?
**A:** Yes, all database options support encryption:
- **SQLite:** AES-256 via SQLCipher (industry-standard encryption)
- **SQL Server:** Transparent Data Encryption (TDE) on the server
- **Microsoft Access:** JET encryption + password protection
- **CSV/JSON:** AES-256 file encryption using .NET Cryptography

### Q: Where is the encryption key stored?
**A:** Encryption keys are stored securely in **Windows Credential Manager**, which:
- Uses DPAPI (Data Protection API) with AES-256
- Encrypted with your Windows password
- Only accessible by your user account
- Kernel-level protection (cannot be extracted without admin + physical access)

### Q: How do I test my database?
**A:** In the Setup Wizard or anytime from Options → Database Management:
1. Click **"🧪 Test Database"** button
2. Wait 10-30 seconds for 25+ automated tests
3. View detailed results in the popup window
4. Tests validate: connectivity, CRUD operations, encryption, performance

See [DATABASE_TESTING.md](DATABASE_TESTING.md) for details.

### Q: Can I switch database types later?
**A:** Yes:
1. Go to **Options → Database Management**
2. Click **"Reconfigure Database"**
3. Select new database type
4. **IMPORTANT:** Export your current data first (File → Export Inventory)
5. Import into new database after reconfiguration

### Q: How do I backup my database?
**A:**
- **SQLite/Access:** Options → Database Management → **"Backup Database"**
  - Creates `.bak` file with timestamp
  - Can restore anytime with **"Restore Database"**
- **SQL Server:** Use SQL Server Management Studio backup
- **CSV/JSON:** Files are plain text, just copy the directory

### Q: Where do I export database templates?
**A:** In the Setup Wizard:
1. Click **"💾 Export Template"** button
2. Choose save location
3. Get an empty database file (.db or .accdb) with full schema
4. Copy template to other machines or use as backup

**Use Cases:**
- Share pre-configured databases with team
- Create backups of empty schema
- Deploy to multiple machines

### Q: Can multiple people use the same database?
**A:** Depends on database type:
- **SQLite:** Single-user (file locking) - one writer at a time
- **SQL Server:** Multi-user (fully concurrent, enterprise-grade)
- **Access:** Limited multi-user (5-10 users max, network share)
- **CSV/JSON:** Single-user (file locking)

**For multi-user environments:** Use SQL Server.

---

## ⚡ Performance & Optimization

### Q: How fast is NecessaryAdminTool?
**A:** With v4.0 multicore optimizations:

| Operation | Before | After | Speedup |
|-----------|--------|-------|---------|
| Single scan | 8-10s | 2-3s | **4x faster** |
| 100 computers | 12-15min | 3-5min | **3x faster** |
| 500 computers | 60-75min | 15-25min | **3x faster** |
| Process list | 2-5s | 0.5-1.5s | **3x faster** |
| CSV export (1000) | 1s | 0.2s | **5x faster** |

See [OPTIMIZATIONS.md](OPTIMIZATIONS.md) for technical details.

### Q: How does NecessaryAdminTool scale with CPU cores?
**A:** Automatically! The **Dynamic Parallel Optimization** system adjusts based on your hardware:

| System | Parallel Scans | Speed |
|--------|----------------|-------|
| 4-core, 8GB RAM | 30 scans | Baseline |
| 8-core, 16GB RAM | 40 scans | 1.3x faster |
| 16-core, 32GB RAM | 80 scans | 2.6x faster |
| 32-core, 128GB RAM | 100 scans (max) | 3.3x faster |

**Result:** Low-end systems stay stable, high-end systems run at maximum speed.

### Q: My scans are slow. How can I speed them up?
**A:**
1. **Check your hardware:**
   - More CPU cores = faster scanning
   - More RAM = more parallel scans
   - SSD storage = faster database access

2. **Optimize database:**
   - Options → Database Management → **"Optimize Database"** (VACUUM for SQLite)
   - Consider switching to SQLite if using CSV

3. **Reduce scope:**
   - Filter by OU instead of scanning entire domain
   - Use bookmarks for frequently scanned systems

4. **Network optimization:**
   - Ensure fast network to Domain Controller
   - Reduce network latency (use DC in same datacenter)

### Q: How many computers can NecessaryAdminTool handle?
**A:**

| Database | Practical Limit | Notes |
|----------|----------------|-------|
| **SQLite** | 100,000+ | Fast, encrypted, zero-config |
| **SQL Server** | Unlimited | Enterprise-scale |
| **Access** | ~50,000 | 2GB database limit |
| **CSV/JSON** | ~10,000 | Performance degrades with size |

**Tested:** Successfully scanned domains with 5,000+ computers on 16-core system (25 minutes).

### Q: Does NecessaryAdminTool cache results?
**A:** Yes:
- **Failure Cache:** Remembers offline computers for 5 minutes (avoids re-scanning immediately)
- **Recent Targets:** Last 20 scanned devices (instant recall)
- **WMI Connection Pooling:** Reuses established connections

---

## 🔒 Security & Credentials

### Q: Are my credentials safe?
**A:** Yes. NecessaryAdminTool follows enterprise security best practices:
- **Windows Credential Manager** - Secure OS-level storage
- **SecureString** - No plaintext passwords in memory
- **Kernel-level Memory Wiping** - RtlSecureZeroMemory after use
- **Kerberos Authentication** - Encrypted + signed network traffic
- **No Disk Storage** - Credentials never written to disk in plaintext

### Q: Can someone steal my database encryption key?
**A:** Very difficult:
- Stored in **Windows Credential Manager** (DPAPI encrypted)
- Encrypted with your Windows password
- Requires admin access + physical access + knowledge of target
- Even then, DPAPI protection is industry-standard

**Recommended:** Use a strong Windows password and enable BitLocker.

### Q: Does NecessaryAdminTool support multi-factor authentication (MFA)?
**A:** NecessaryAdminTool uses Windows Authentication (Kerberos/NTLM), which honors your domain's authentication policy:
- If your domain requires MFA (e.g., smartcard logon), NecessaryAdminTool inherits that
- RMM tool integrations support individual tool authentication (TeamViewer password, AnyDesk key, etc.)

### Q: Are remote sessions audited?
**A:** Yes:
- All administrative actions logged to `%AppData%\NecessaryAdminTool\Logs\`
- RMM tool connections logged with:
  - Timestamp
  - User
  - Target computer
  - Tool used
  - Success/failure status

### Q: Can I disable certain features for security?
**A:** Yes:
- **RMM Tools:** Options → Remote Control → Disable individual tools
- **Remote Commands:** Disable PsExec, DISM, or other risky tools (code-level configuration)
- **Read-Only Mode:** Run as non-admin user for view-only access

### Q: Is there a role-based access control (RBAC) system?
**A:** Currently limited:
- **Admin Mode:** Full access (requires Administrator privileges)
- **Read-Only Mode:** View inventory only (standard domain user)

**Planned for v1.3+:** Customizable role-based templates.

---

## 🎨 Modern UI Features

### Q: How do I use the Command Palette?
**A:** Press **Ctrl+K** to open the Command Palette. Type to search for commands using fuzzy matching (searches command titles, descriptions, and keywords). Use **↑↓** arrow keys to navigate, **Enter** to execute the selected command, **ESC** to close.

**Features:**
- 25+ registered commands covering all major operations
- Fuzzy search (type "scan" to find "Scan Domain", "Scan Single", etc.)
- Categories: Scanning, Authentication, Remote Tools, Quick Fixes, Filters, Settings
- Visual indicators: Command icon, title, description, keyboard shortcut
- Non-blocking: Doesn't interrupt your current workflow

**Examples:**
- Type "scan" → Shows all scan-related commands
- Type "rdp" → Shows Remote Desktop command
- Type "auth" → Shows authentication commands

### Q: Can I disable toast notifications?
**A:** Toast notification settings are planned for a future release (v1.1+). Currently, all toast notifications are enabled by default.

**Current Behavior:**
- 245+ toast notifications throughout the app
- 4 types: Success (green), Info (blue), Warning (amber), Error (red)
- Auto-dismiss based on message length (500ms per word + 1 second, max 10 seconds)
- Max 5 concurrent toasts (auto-queues additional notifications)
- Non-blocking (doesn't stop your work)

**Planned Features (v1.1+):**
- Options → Notifications → Customize which toast types you want to see
- Per-category filtering (Scanning, Authentication, Remote Tools, etc.)
- Duration customization
- Position customization (top-right, bottom-right, etc.)

### Q: How do I customize keyboard shortcuts?
**A:** Keyboard shortcut customization is planned for a future release (v1.2+).

**Current Shortcuts (11 total, fixed):**
- **Ctrl+K** - Open Command Palette
- **Ctrl+Shift+F** - Scan Domain (Fleet)
- **Ctrl+S** - Scan Single Computer
- **Ctrl+L** - Load AD Objects
- **Ctrl+Alt+A** - Authenticate
- **Ctrl+R** - Remote Desktop
- **Ctrl+P** - PowerShell Remote
- **Ctrl+T** - Toggle Card/Grid View
- **Ctrl+`** - Toggle Terminal
- **Ctrl+,** - Open Settings

**Planned Features (v1.2+):**
- Options → Keyboard Shortcuts → View and customize all shortcuts
- Click "Edit" next to any shortcut to record a new key combination
- Conflict detection (warns if shortcut is already in use)
- Reset to defaults button
- Import/export shortcut profiles

**Workaround:** Use the Command Palette (Ctrl+K) to search and execute commands without remembering shortcuts.

### Q: What's the difference between Card View and Grid View?
**A:** Both views display the same computer inventory data but in different layouts.

**Grid View (Default):**
- Traditional table format with columns
- Dense data display (more computers visible at once)
- Easy sorting by clicking column headers
- Best for: Comparing specific properties across many computers
- Toggle: Press **Ctrl+T** or click toolbar button

**Card View:**
- Visual cards (300x180 pixels each) in a grid layout
- Status badges, CPU/RAM progress bars, quick action buttons
- Better visual hierarchy (easier to scan at a glance)
- Best for: Visual scanning, quick actions, status overview
- Toggle: Press **Ctrl+T** or click toolbar button

**Pro Tip:** Use Grid View for data analysis, Card View for quick status checks and actions.

### Q: What are the toast notification types?
**A:** There are 4 toast notification types, each with a distinct color and icon:

| Type | Color | Icon | Usage |
|------|-------|------|-------|
| **Success** | Green (#10B981) | ✓ | Successful operations ("Scan completed", "Settings saved") |
| **Info** | Blue (#3B82F6) | ℹ | Status updates and information ("Loading 500 computers...", "Connected to DC") |
| **Warning** | Amber (#F59E0B) | ⚠ | Validation failures and cautions ("Authentication expires in 5 min", "Database optimization recommended") |
| **Error** | Red (#EF4444) | ✕ | Errors and failures ("Failed to connect", "Access denied") |

**Visual Design:**
- Dark background (#1E1E1E) with colored left border
- 320-480px wide, auto-height
- Slide-in animation from right
- Fade-out animation on dismiss
- Optional action button (orange, "View", "Retry", "Undo", etc.)

**Usage Stats:** 245+ toast notifications throughout the application.

### Q: How long do toast notifications stay visible?
**A:** Toast duration is automatically calculated based on message length:

**Formula:** `(word count × 500ms) + 1000ms`

**Examples:**
- "Scan completed" (2 words) = 2 seconds
- "Loading 500 computers from Active Directory" (6 words) = 4 seconds
- Long error messages (15+ words) = 8.5 seconds
- **Maximum:** 10 seconds (regardless of message length)

**Interaction:**
- Toasts auto-dismiss after their calculated duration
- Click action button (if present) to execute action and dismiss immediately
- Max 5 concurrent toasts (oldest auto-removed if limit reached)

**Pro Tip:** You don't need to manually dismiss toasts - they auto-clear based on message complexity.

### Q: What commands are available in the Command Palette?
**A:** 25+ commands across 6 categories:

**Scanning (3 commands):**
- Scan Domain (Fleet) - Ctrl+Shift+F
- Scan Single Computer - Ctrl+S
- Load AD Objects - Ctrl+L

**Authentication (2 commands):**
- Authenticate - Ctrl+Alt+A
- Logout

**Remote Tools (6 commands):**
- Remote Desktop - Ctrl+R
- PowerShell Remote - Ctrl+P
- Services Manager
- Process Manager
- Event Logs
- Browse C$ Share

**Quick Fixes (6 commands):**
- Restart Windows Update
- Clear DNS Cache
- Restart Print Spooler
- Enable WinRM
- Fix Time Sync
- Clear Event Logs

**Filters (4 commands):**
- Show Online Only
- Show Offline Only
- Show Servers Only
- Clear Filters

**Settings (4 commands):**
- Open Settings - Ctrl+,
- Toggle Card/Grid View - Ctrl+T
- Toggle Terminal - Ctrl+`
- About

**How to Access:** Press **Ctrl+K** and start typing. The palette uses fuzzy search across command titles, descriptions, and keywords.

### Q: What is Fluent Design?
**A:** Fluent Design is Microsoft's design language for Windows 11. NecessaryAdminTool v1.0 uses Fluent Design principles for a native Windows 11 look and feel.

**Fluent Design Elements in NecessaryAdminTool:**
- **Mica Materials** - Opaque, wallpaper-tinted backgrounds (#1A1A1A, #1E1E1E)
- **Acrylic Materials** - Semi-transparent surfaces for transient UI (90% opacity)
- **Rounded Corners** - 8px standard, 4px small, 12px large
- **Elevation Shadows** - 2dp, 4dp, 8dp drop shadows for depth
- **Typography** - Segoe UI Variable font (10px-32px scale)
- **Spacing Scale** - 4px base unit (8px, 12px, 16px, 24px, 32px, 48px, 64px)
- **Semantic Colors** - Success (#10B981), Warning (#F59E0B), Error (#EF4444), Info (#3B82F6)

**Implementation:** `UI/Themes/Fluent.xaml` - Auto-merged into App.xaml resources

**References:**
- Fluent 2 Design Specs: https://fluent2.microsoft.design/
- TAG: `#FLUENT_THEME` in codebase

### Q: Are there any new tags in the code?
**A:** Yes! All UI Engine code is tagged with `#AUTO_UPDATE_UI_ENGINE` and specific feature tags:

**Primary Tag:**
- `#AUTO_UPDATE_UI_ENGINE` - Core UI engine code (119+ occurrences across 7 files)

**Feature-Specific Tags:**
- `#TOAST_NOTIFICATIONS` - Toast notification system (245+ calls across 5 files)
- `#COMMAND_PALETTE` - Command palette features
- `#FLUENT_THEME` - Fluent Design resources
- `#SKELETON_LOADERS` - Loading screen animations
- `#CARD_VIEW` - Card view components
- `#VALUE_CONVERTERS` - Data binding converters
- `#KEYBOARD_SHORTCUTS` - Keyboard shortcut handlers

**How to Use Tags:**
```bash
# Find all UI Engine code
Grep pattern="#AUTO_UPDATE_UI_ENGINE" glob="*.{cs,xaml}"

# Find all toast notifications
Grep pattern="ToastManager\.(ShowSuccess|ShowInfo|ShowWarning|ShowError)" glob="*.cs"

# Find keyboard shortcuts
Grep pattern="#KEYBOARD_SHORTCUTS" glob="MainWindow.xaml.cs"
```

**Pro Tip:** Use tags to quickly locate and update related code across the entire codebase.

### Q: How do I toggle between Card View and Grid View?
**A:** There are two ways to toggle between viewing modes:

**Method 1: Keyboard Shortcut**
- Press **Ctrl+T** to instantly toggle between Grid and Card view

**Method 2: Toolbar Button**
- Click the view toggle button in the main toolbar

**Current View Indicator:**
- The toolbar button shows which view is currently active
- View preference persists between application sessions

**When to Use Each View:**
- **Grid View** - Best for data analysis, sorting, comparing specific properties
- **Card View** - Best for visual scanning, quick actions, status overview

**Performance:** Both views render the same data with minimal performance difference.

---

## 🛠️ Features & Functionality

### Q: Can I scan computers outside my domain?
**A:** Limited support:
- **Workgroup Computers:** Enter IP address directly, provide credentials manually
- **Cross-Domain:** Use connection profiles for different domain credentials
- **DMZ/Isolated Networks:** Requires network connectivity and firewall rules

**Best Use Case:** Domain-joined computers in the same Active Directory forest.

### Q: How do I use bookmarks?
**A:**
1. **Right-click** any computer in inventory
2. Select **"Add to Bookmarks"**
3. Choose category (Domain Controllers, SQL Servers, etc.)
4. Add description/notes (optional)
5. Access anytime from **Bookmarks panel**

**Power Tip:** Export bookmarks (File → Export Bookmarks) to share with team.

### Q: What is a connection profile?
**A:** A saved configuration for connecting to different domain controllers:
- **Use Case:** Switch between Production, Staging, Test environments
- **Stores:** Domain controller hostname, credentials (encrypted), last used date
- **Access:** Click profile dropdown in main window to switch instantly

### Q: How do I create custom PowerShell scripts?
**A:**
1. Go to **Scripts tab**
2. Click **"New Script"**
3. Enter name, description, category
4. Paste your PowerShell code
5. Click **"Save"**
6. Execute from **Scripts tab** or right-click context menu

**Features:** Multi-computer execution, real-time output streaming, parameter support.

### Q: Can I schedule automatic scans?
**A:** Yes (v1.0):
1. During Setup Wizard, enable **"Install Windows Service"**
2. Choose scan interval (1-24 hours, default: 2 hours)
3. Service runs in background, writes to database
4. UI shows results immediately

**Fallback:** If not admin, creates scheduled task instead.

### Q: How do I enable RMM tool integrations?
**A:**
1. Go to **Options → Remote Control**
2. Enable desired tool (TeamViewer, AnyDesk, etc.)
3. Configure connection string (e.g., TeamViewer ID format)
4. Save credentials to Windows Credential Manager
5. Right-click computer → Remote Control → [Tool]

**Security:** All integrations disabled by default.

### Q: What remediation actions are available?
**A:** 6 automated fixes:
1. **Restart Windows Update** - Clear cache + restart service
2. **Clear DNS Cache** - Flush DNS resolver
3. **Restart Print Spooler** - Fix print queue issues
4. **Enable WinRM** - Configure remote management
5. **Fix Time Synchronization** - Sync with domain time source
6. **Clear Event Logs** - Free up disk space

**Access:** Right-click computer → Remediation → [Action]

### Q: Can I customize the colors/theme?
**A:** Currently limited:
- **Unified Dark Theme:** Orange (#FFFF8533) / Zinc (#FFA1A1AA)
- **Fluent Design:** Windows 11 native look with Mica materials
- **Semantic Colors:** Success (green), Info (blue), Warning (amber), Error (red)
- **Font Scaling:** Options → UI → Font Size (0.8x - 2.0x)

**Current Theming:**
- Dark mode only (optimized for extended use, reduces eye strain)
- Fluent Design resources (see `UI/Themes/Fluent.xaml`)
- Automatic color application via StaticResource bindings

**Planned for v1.1+:**
- Color picker for accent colors
- Light mode toggle
- Custom theme profiles
- Import/export theme settings

See [THEME_SYSTEM.md](THEME_SYSTEM.md) for technical details.

---

## 🐛 Troubleshooting

### Q: NecessaryAdminTool won't start. What do I do?
**A:**
1. **Check .NET Framework:** Ensure .NET Framework 4.8.1 is installed
   - Download: https://dotnet.microsoft.com/download/dotnet-framework/net481
2. **Run as Administrator:** Right-click → Run as Administrator
3. **Check logs:** `%AppData%\NecessaryAdminTool\Logs\` for error details
4. **Delete config:** Rename `%AppData%\NecessaryAdminTool\` to reset settings
5. **Reinstall:** Extract fresh ZIP, try again

### Q: "Database connection failed" error
**A:**
- **SQLite:** Check if file is locked (another process using it)
- **SQL Server:** Verify connection string, network connectivity, credentials
- **Access:** Ensure Access Database Engine installed
- **All:** Try **"🧪 Test Database"** to diagnose issue

### Q: "Access Denied" when scanning computers
**A:** Common causes:
- **Firewall:** Ensure WMI/RPC ports open (TCP 135, 445, dynamic RPC)
- **Credentials:** Must be domain admin or local admin on target
- **WMI Permissions:** Target computer must allow WMI queries
- **Antivirus:** May block remote management (whitelist NecessaryAdminTool.exe)

### Q: Scans are timing out
**A:**
1. **Increase timeout:** Options → Advanced → Query Timeout Multiplier
2. **Check network:** Slow network = slow scans (use DC in same site)
3. **Reduce parallelism:** Lower concurrent scans if network is saturated
4. **Check target:** Offline or unresponsive computers will timeout (expected)

### Q: Domain Controller auto-discovery fails
**A:**
1. **DNS Resolution:** Ensure `_ldap._tcp.dc._msdcs.[domain]` SRV record exists
2. **Network Connectivity:** Ping domain controller by hostname
3. **Firewall:** LDAP ports (TCP 389, 636) must be open
4. **Manual Entry:** Enter DC hostname manually in Options → Active Directory

### Q: RMM tool integration doesn't work
**A:**
- **Tool Installed:** Ensure RMM tool is installed on target computer
- **Credentials:** Verify credentials stored in Windows Credential Manager
- **Connection String:** Check format (e.g., TeamViewer ID is 9-10 digits)
- **Firewall:** RMM tool firewall rules must allow connections
- **Logs:** Check `%AppData%\NecessaryAdminTool\Logs\` for details

### Q: CSV export fails with large datasets
**A:**
- **Memory:** Large exports (10,000+ computers) require sufficient RAM
- **Timeout:** May take several seconds for very large datasets
- **Format:** Try exporting to JSON instead (faster for large datasets)
- **Filter:** Export subsets instead of entire inventory

### Q: "Setup Wizard cancelled by user" on startup
**A:** If Setup Wizard was cancelled, app exits:
1. **Re-run app** and complete Setup Wizard
2. **Can't skip:** First-run configuration is required
3. **Change later:** All settings can be modified in Options menu

### Q: Update check fails
**A:** (v1.1+ when auto-update is released)
- **Internet Connection:** Ensure internet access
- **GitHub Access:** Firewall must allow access to GitHub
- **Proxy Settings:** Configure proxy in Options → Network
- **Manual Download:** Download from GitHub Releases directly

---

## 🎓 Advanced Topics

### Q: Can I automate NecessaryAdminTool with scripts?
**A:** Not yet (planned for v1.2+):
- **Current:** GUI-only application
- **Planned:** Command-line interface (CLI) for automation
- **Workaround:** Use PowerShell to call WMI/CIM directly

### Q: Can I integrate with ServiceNow/SCCM/Intune?
**A:** Not yet (planned for v1.4+):
- **Current:** Standalone application
- **Planned:** API endpoints for integration
- **Workaround:** Export inventory to CSV, import into other tools

### Q: Does NecessaryAdminTool support Linux or Mac?
**A:** No. NecessaryAdminTool is Windows-only:
- Built with .NET Framework 4.8.1 (Windows-specific)
- Uses WMI/CIM (Windows Management Instrumentation)
- Integrates with Active Directory (Windows domain)

**Alternative:** Use RSAT tools on Linux with wine (limited functionality).

### Q: Can I contribute to NecessaryAdminTool?
**A:** Currently proprietary software. See [Licensing & Support](#-licensing--support).

### Q: How do I migrate from ArtaznIT Suite to NecessaryAdminTool?
**A:** Simple:
1. **Export data** from ArtaznIT Suite (File → Export All Settings, Export Inventory)
2. **Install NecessaryAdminTool v1.0**
3. **Import data** (File → Import All Settings, Import Inventory)
4. **Done!** All bookmarks, tags, profiles preserved

**Note:** Database schema is compatible (v7.x → v1.0).

### Q: Can I run NecessaryAdminTool on Windows Server?
**A:** Yes:
- **Tested on:** Windows Server 2019, 2022
- **Requirements:** .NET Framework 4.8.1, Desktop Experience (GUI)
- **Use Case:** Run on jump box or admin workstation

**Not Recommended:** Running on production servers (use admin workstation instead).

### Q: How do I create a custom report?
**A:** (v1.3+ planned)
- **Current:** Export to CSV, process in Excel
- **Planned:** Custom report templates with PDF/Excel generation

### Q: Can I use NecessaryAdminTool offline?
**A:** Limited:
- **Offline Mode:** Disable Global Services health checks
- **Local Data:** View previously scanned inventory
- **No Scanning:** Active Directory queries require network
- **No RMM:** Remote control tools require network

---

## 📜 Licensing & Support

### Q: Is NecessaryAdminTool free?
**A:** Contact Brandon Necessary for licensing information.
- **Source:** GitHub repository (https://github.com/brandon-necessary/NecessaryAdminTool)

### Q: Can I use NecessaryAdminTool commercially?
**A:** See LICENSE file in repository. Contact author for commercial licensing.

### Q: How do I get support?
**A:**
1. **Documentation:** Read [README_COMPREHENSIVE.md](README_COMPREHENSIVE.md), this FAQ, and linked docs
2. **GitHub Issues:** Report bugs at https://github.com/brandon-necessary/NecessaryAdminTool/issues
3. **GitHub Discussions:** Ask questions at https://github.com/brandon-necessary/NecessaryAdminTool/discussions
4. **Email:** Contact via GitHub profile

### Q: Where can I report bugs?
**A:** [GitHub Issues](https://github.com/brandon-necessary/NecessaryAdminTool/issues)

**Include:**
- NecessaryAdminTool version (Help → About)
- Windows version
- Error message (full text or screenshot)
- Steps to reproduce
- Logs from `%AppData%\NecessaryAdminTool\Logs\`

### Q: Can I request features?
**A:** Yes! [GitHub Discussions](https://github.com/brandon-necessary/NecessaryAdminTool/discussions)

**Popular Requests:**
- Email alerts for critical events
- Custom dashboard widgets
- PowerBI integration
- Multi-language support
- Theme customization

See [SUGGESTED_FEATURES.md](SUGGESTED_FEATURES.md) for planned enhancements.

### Q: When is the next update?
**A:** See [Roadmap](#-roadmap) in README:
- **v1.1** - Auto-Update Enhancement (Q2 2026)
- **v1.2** - Windows Service (Q3 2026)
- **v1.3** - Enhanced Reporting (Q4 2026)

**Subscribe:** Watch GitHub repository for release notifications.

---

## 🔄 Version-Specific Questions

### Q: What's new in v1.0?
**A:** Complete rebrand from ArtaznIT Suite + new features:
- ✅ Unified theme system
- ✅ Database layer (4 providers with encryption)
- ✅ Database testing system (25+ automated tests)
- ✅ Setup Wizard
- ✅ Template export
- ✅ CalVer versioning (1.2602.0.0)
- ✅ All 169 features from v7.x included

### Q: Why the version jump from v7.x to v1.0?
**A:** Complete rebrand deserved a fresh start:
- **New Name:** ArtaznIT Suite → NecessaryAdminTool
- **New Repository:** GitHub (fresh commit history)
- **New Versioning:** CalVer (Major.YYMM.Minor.Build)
- **Foundation Release:** First official release of NecessaryAdminTool

### Q: Is v1.0 stable for production?
**A:** Yes! v1.0 includes all tested features from v7.2603.5.0 (ArtaznIT):
- 169 implemented features
- Tested in enterprise environments
- No breaking changes from v7.x (data compatible)

### Q: How do I upgrade from v7.x to v1.0?
**A:** See "How do I migrate from ArtaznIT Suite to NecessaryAdminTool?" above.

---

## 💡 Pro Tips

### Tip #1: Use Bookmarks for Critical Servers
Right-click → Add to Bookmarks → Category: "Domain Controllers" for instant access.

### Tip #2: Export Settings Before Major Changes
Options → Export All Settings → Save timestamped backup before database changes.

### Tip #3: Use Recent Targets Dropdown
Click dropdown in Target System Control for last 20 scanned systems (instant recall).

### Tip #4: Run Database Testing After Setup
Setup Wizard → "🧪 Test Database" to validate configuration before production use.

### Tip #5: Enable Auto-Save
Options → Auto-Save → Enable → Interval: 10 minutes (prevents data loss).

### Tip #6: Optimize Database Monthly
Options → Database Management → "Optimize Database" (VACUUM for SQLite).

### Tip #7: Use Connection Profiles for Multi-Environment
Create profiles for Production, Staging, Test (quick switching).

### Tip #8: Filter by OU for Large Domains
Instead of scanning entire domain, filter by OU for faster results.

### Tip #9: Use Dark Theme for Long Sessions
Unified dark theme reduces eye strain during extended use.

### Tip #10: Check Release Notes Before Updating
Read release notes for breaking changes, new features, bug fixes.

---

## 🔗 Additional Resources

- **README:** [README_COMPREHENSIVE.md](README_COMPREHENSIVE.md)
- **Theme System:** [THEME_SYSTEM.md](THEME_SYSTEM.md)
- **Database Testing:** [DATABASE_TESTING.md](DATABASE_TESTING.md)
- **Performance:** [OPTIMIZATIONS.md](OPTIMIZATIONS.md)
- **Feature List:** [FEATURES.md](FEATURES.md)
- **GitHub Repository:** https://github.com/brandon-necessary/NecessaryAdminTool

---

<div align="center">

**Can't find your question? Ask on [GitHub Discussions](https://github.com/brandon-necessary/NecessaryAdminTool/discussions)**

**Built with Claude Code** 🤖

[⬆ Back to Top](#frequently-asked-questions-faq)

</div>
