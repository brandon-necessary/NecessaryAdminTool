# ArtaznIT Suite - Version 8.0 Planning Document
**Target Release:** April 2026
**Version:** 8.0.0.0
**Codename:** "Service & Auto-Update"
**Status:** 🟡 PLANNING

---

## 🎯 **VISION**

Version 8.0 is a **MAJOR ARCHITECTURAL UPGRADE** that transforms ArtaznIT from a desktop-only application to a **hybrid service-based system** with automatic background scanning and **built-in auto-update capabilities**.

### **Core Goals:**
1. **Windows Service** - Background scanning without UI
2. **Auto-Update System** - Seamless updates across all installations
3. **Shared Data Layer** - UI and Service access same data
4. **Modular Database** - Multiple backend options (SQLite/SQL Server/Access/CSV)
5. **Settings Preservation** - Keep preferences during updates

---

## 🚀 **MAJOR FEATURES**

### **PRIORITY 1: AUTO-UPDATE SYSTEM** ⭐⭐⭐⭐⭐
**Why First:** Critical for deployment and maintenance
**Estimated LOC:** ~800 lines

#### Components:
1. **Update Manager**
   - Check for updates (GitHub Releases API or custom endpoint)
   - Download update packages
   - Verify digital signatures/checksums
   - Apply updates with rollback support
   - Preserve user settings during update

2. **Update UI**
   - "Check for Updates" button in Help menu
   - Update notification dialog
   - Progress bar during download/install
   - Release notes display
   - Auto-update on startup (optional, user preference)

3. **Settings Migration**
   - Backup current settings before update
   - Migrate settings to new version
   - Preserve:
     - Connection Profiles
     - Bookmarks
     - RMM configurations
     - Window position/size
     - Font preferences
     - Auto-save settings
     - Script library
     - Asset tags

4. **Branch Management**
   - Detect current version and branch
   - Support multiple update channels:
     - **Stable** - Production-ready releases
     - **Beta** - Testing releases
     - **Alpha** - Bleeding-edge features
   - Allow users to switch branches
   - Warn when switching to older branch

5. **Rollback Mechanism**
   - Backup previous version before update
   - One-click rollback if update fails
   - Automatic rollback on crash detection

#### Implementation:
- `UpdateManager.cs` - Core update logic
- `UpdateWindow.xaml` - Update UI dialog
- `VersionInfo.cs` - Version comparison utilities
- `SettingsMigrator.cs` - Settings backup/restore
- GitHub Releases or custom update server

#### Technical Details:
- **Update Check:** Poll GitHub Releases API or custom server
- **Download:** HTTPS with progress tracking
- **Verification:** SHA256 checksum validation
- **Installation:** Replace executable + restart
- **Settings:** Copy from `%AppData%\ArtaznIT` before update

---

### **PRIORITY 2: WINDOWS SERVICE** ⭐⭐⭐⭐⭐
**Estimated LOC:** ~1,500 lines

#### Components:
1. **Service Project**
   - New `ArtaznIT.Service` project (.NET Framework Windows Service)
   - Service installer with setup wizard
   - Service control (Start/Stop/Pause/Resume)
   - Service configuration UI

2. **Background Scanner**
   - Scheduled scans (hourly, daily, weekly, custom cron)
   - Uses existing `OptimizedADScanner` and `ActiveDirectoryManager`
   - Stores results in shared database
   - No UI overhead - headless operation

3. **Shared Data Layer**
   - Abstract `IDataProvider` interface
   - Multiple implementations:
     - **SQLite** (default) - `SqliteDataProvider.cs`
     - **SQL Server** - `SqlServerDataProvider.cs`
     - **Access** - `AccessDataProvider.cs`
     - **CSV/JSON** - `CsvDataProvider.cs` (fallback)
   - Factory pattern for provider selection

4. **Service Configuration**
   - Configurable scan schedule
   - Configurable data storage location
   - Database backend selection
   - Credentials for AD scanning
   - Email alerts (optional)

5. **UI ↔ Service Communication**
   - Service writes to database
   - UI reads from database (live refresh)
   - Named pipes or WCF for control commands
   - Service status display in UI

#### Architecture:
```
ArtaznIT (WPF UI)
├── Reads from: IDataProvider
├── Writes settings to: IDataProvider
└── Controls service via: WCF/Named Pipes

ArtaznIT.Service (Windows Service)
├── Reads settings from: IDataProvider
├── Writes scan results to: IDataProvider
└── Runs on schedule (no UI)

IDataProvider (Interface)
├── SqliteDataProvider
├── SqlServerDataProvider
├── AccessDataProvider
└── CsvDataProvider
```

#### Benefits:
- ✅ Scans run even when no user is logged in
- ✅ Always-on monitoring for servers
- ✅ Lower resource usage (no UI)
- ✅ Centralized data (multiple UIs can read same data)
- ✅ Perfect for enterprise deployments

---

### **PRIORITY 3: MODULAR DATABASE LAYER** ⭐⭐⭐⭐
**Estimated LOC:** ~600 lines

#### Database Providers:

**1. SQLite (Default)**
- **Pros:** No installation, lightweight, fast, cross-platform
- **Cons:** Not ideal for high-concurrency (but fine for this use case)
- **Use Case:** Small to medium deployments (1-5000 computers)
- **File:** `%AppData%\ArtaznIT\ArtaznIT.db`

**2. SQL Server**
- **Pros:** Enterprise-grade, scales to millions of computers, supports multiple users
- **Cons:** Requires SQL Server installation
- **Use Case:** Large enterprises (5000+ computers)
- **Connection:** User-configured connection string

**3. Microsoft Access**
- **Pros:** Familiar to users, easy Excel integration
- **Cons:** Legacy, size limits (2GB), slower performance
- **Use Case:** Legacy compatibility
- **File:** `%AppData%\ArtaznIT\ArtaznIT.mdb`

**4. CSV/JSON (Fallback)**
- **Pros:** Human-readable, no dependencies, easy debugging
- **Cons:** Slow, no querying, file locking issues
- **Use Case:** Emergency fallback, testing, export
- **File:** `%AppData%\ArtaznIT\inventory.csv`

#### IDataProvider Interface:
```csharp
public interface IDataProvider
{
    // Computer inventory
    Task<List<ComputerInventory>> GetAllComputersAsync();
    Task<ComputerInventory> GetComputerAsync(string hostname);
    Task SaveComputerAsync(ComputerInventory computer);
    Task DeleteComputerAsync(string hostname);

    // Tags
    Task<List<AssetTag>> GetAllTagsAsync();
    Task SaveTagAsync(AssetTag tag);
    Task<List<string>> GetComputerTagsAsync(string hostname);

    // Settings
    Task<T> GetSettingAsync<T>(string key);
    Task SaveSettingAsync<T>(string key, T value);

    // Service
    Task<ScanHistory> GetLastScanAsync();
    Task SaveScanHistoryAsync(ScanHistory scan);
}
```

---

## 📋 **IMPLEMENTATION ROADMAP**

### **Phase 1: Auto-Update Foundation (Week 1-2)**
**Deliverable:** v8.0.0.0-alpha1

Tasks:
- [ ] Create `UpdateManager.cs`
- [ ] Implement GitHub Releases API integration
- [ ] Create `UpdateWindow.xaml` UI
- [ ] Implement settings backup/restore
- [ ] Add "Check for Updates" to Help menu
- [ ] Test update flow (download → install → restart)
- [ ] Implement version comparison logic
- [ ] Add update channels (Stable/Beta/Alpha)

**Success Criteria:**
- ✅ App can check for updates
- ✅ App can download and install updates
- ✅ Settings are preserved after update
- ✅ Rollback works if update fails

---

### **Phase 2: Shared Data Layer (Week 3-4)**
**Deliverable:** v8.0.0.0-alpha2

Tasks:
- [ ] Design `IDataProvider` interface
- [ ] Implement `SqliteDataProvider` (default)
- [ ] Implement `CsvDataProvider` (fallback)
- [ ] Create data models (ComputerInventory, ScanHistory, etc.)
- [ ] Migrate existing code to use IDataProvider
- [ ] Add database selection in OptionsWindow
- [ ] Add configurable data directory
- [ ] Test data persistence and retrieval

**Success Criteria:**
- ✅ App stores all data in SQLite by default
- ✅ User can switch to CSV/JSON fallback
- ✅ User can configure data directory location
- ✅ All features work with new data layer

---

### **Phase 3: Windows Service (Week 5-7)**
**Deliverable:** v8.0.0.0-beta1

Tasks:
- [ ] Create `ArtaznIT.Service` Windows Service project
- [ ] Implement service installer
- [ ] Move scanning logic to shared library
- [ ] Implement scheduled scanning
- [ ] Implement service configuration
- [ ] Add service control UI in main app
- [ ] Implement service ↔ UI communication (WCF/Named Pipes)
- [ ] Add service status indicator
- [ ] Test service installation/uninstallation
- [ ] Test background scanning

**Success Criteria:**
- ✅ Service installs correctly
- ✅ Service runs scans on schedule
- ✅ Service writes to database
- ✅ UI reads from database
- ✅ Service can be controlled from UI

---

### **Phase 4: Advanced Database Providers (Week 8)**
**Deliverable:** v8.0.0.0-beta2

Tasks:
- [ ] Implement `SqlServerDataProvider`
- [ ] Implement `AccessDataProvider`
- [ ] Add database migration utilities
- [ ] Add database connection testing
- [ ] Add database performance monitoring
- [ ] Test all providers under load

**Success Criteria:**
- ✅ User can select SQL Server backend
- ✅ User can select Access backend
- ✅ Migration between providers works
- ✅ All providers pass test suite

---

### **Phase 5: Polish & Testing (Week 9-10)**
**Deliverable:** v8.0.0.0 (Release)

Tasks:
- [ ] Comprehensive testing (UI + Service)
- [ ] Performance testing with large datasets
- [ ] Documentation (README, INSTALL, UPGRADE)
- [ ] Create installer (MSI/EXE with service option)
- [ ] Create upgrade guide for v7 users
- [ ] Fix bugs
- [ ] Release notes
- [ ] GitHub release

**Success Criteria:**
- ✅ All features work flawlessly
- ✅ No critical bugs
- ✅ Documentation complete
- ✅ Installer works
- ✅ Upgrade from v7 is smooth

---

## 🔧 **SETTINGS PRESERVATION STRATEGY**

### **What to Preserve:**
✅ All user preferences:
- Connection Profiles (`%AppData%\ArtaznIT\ConnectionProfiles.json`)
- Bookmarks (`%AppData%\ArtaznIT\Bookmarks.json`)
- RMM Configurations (`Settings.Default.RemoteControlConfigJson`)
- Window position/size (`Settings.Default.WindowPosition`)
- Font preferences (`Settings.Default.FontSizeMultiplier`)
- Auto-save settings (`Settings.Default.AutoSaveEnabled`, `Settings.Default.AutoSaveIntervalMinutes`)
- Script library (`%AppData%\ArtaznIT\ScriptLibrary\*.json`)
- Asset tags (`%AppData%\ArtaznIT\AssetTags.json`)
- Inventory data (migrate to database)

### **Migration Process:**
1. **Before Update:**
   - Create backup: `%AppData%\ArtaznIT\Backups\v7_backup_YYYYMMDD.zip`
   - Include all settings files
   - Include database file

2. **During Update:**
   - Install new version
   - Detect v7 settings
   - Prompt user: "Migrate settings from v7?"

3. **After Update:**
   - Copy settings files
   - Migrate database if schema changed
   - Validate migration
   - Log any issues

4. **Rollback (if needed):**
   - Restore from backup
   - Revert to previous version

---

## 🎨 **UI MOCKUPS**

### Auto-Update Dialog:
```
┌───────────────────────────────────────────┐
│ 🔄 Update Available                       │
├───────────────────────────────────────────┤
│                                           │
│  New Version: v8.0.1.0 (Stable)          │
│  Current:     v8.0.0.0                    │
│  Size:        12.5 MB                     │
│                                           │
│  Release Notes:                           │
│  ┌─────────────────────────────────────┐ │
│  │ - New feature: XYZ                  │ │
│  │ - Bug fix: ABC                      │ │
│  │ - Performance improvements          │ │
│  └─────────────────────────────────────┘ │
│                                           │
│  [✅] Backup settings before update      │
│  [✅] Auto-restart after update          │
│                                           │
│  [Download & Install]  [Skip This Version│
└───────────────────────────────────────────┘
```

### Service Configuration:
```
┌───────────────────────────────────────────┐
│ ⚙️ Service Configuration                  │
├───────────────────────────────────────────┤
│                                           │
│  Status: ✅ Running                       │
│  Last Scan: 2 minutes ago                 │
│                                           │
│  Scan Schedule:                           │
│  ⚪ Hourly   ⚪ Daily   ⚫ Every 4 hours   │
│  ⚪ Weekly   ⚪ Custom: [________]         │
│                                           │
│  Data Storage:                            │
│  Backend: [SQLite          ▼]             │
│  Location: C:\ProgramData\ArtaznIT\       │
│  [Browse...]                              │
│                                           │
│  [Start Service]  [Stop Service]          │
│  [Apply Changes]  [Close]                 │
└───────────────────────────────────────────┘
```

---

## 📊 **ESTIMATED EFFORT**

| Phase | Feature | LOC | Time | Priority |
|-------|---------|-----|------|----------|
| 1 | Auto-Update System | ~800 | 2 weeks | P0 |
| 2 | Shared Data Layer | ~600 | 2 weeks | P0 |
| 3 | Windows Service | ~1,500 | 3 weeks | P0 |
| 4 | Advanced DB Providers | ~400 | 1 week | P1 |
| 5 | Polish & Testing | - | 2 weeks | P0 |
| **Total** | | **~3,300** | **10 weeks** | |

---

## 🚨 **RISKS & MITIGATION**

### Risk 1: Service Installation Failures
**Mitigation:**
- Comprehensive installer testing
- Fallback to desktop-only mode if service fails
- Clear error messages with troubleshooting steps

### Risk 2: Data Migration Issues
**Mitigation:**
- Extensive backup before migration
- Rollback capability
- Migration validation tests
- User notification of any issues

### Risk 3: Update Failures
**Mitigation:**
- Automatic rollback on crash
- Settings backup before update
- Checksum verification
- Staged rollout (Alpha → Beta → Stable)

### Risk 4: Performance with SQLite
**Mitigation:**
- Performance testing with large datasets
- Optimization (indexes, caching)
- Option to upgrade to SQL Server

---

## 📝 **OPEN QUESTIONS**

1. **Update Server:** GitHub Releases API or custom server?
   - **Recommendation:** GitHub Releases (free, reliable, built-in versioning)

2. **Service vs Desktop:** Should service be optional or required?
   - **Recommendation:** Optional (users can choose desktop-only or service mode)

3. **Database Schema:** Design schema for SQLite/SQL Server?
   - **Tables:** Computers, Tags, ScanHistory, Settings, Scripts

4. **Installer:** MSI or EXE? WiX or InstallShield?
   - **Recommendation:** WiX MSI (professional, customizable)

5. **Auto-Update Security:** How to verify updates are authentic?
   - **Recommendation:** SHA256 checksums + optional code signing

---

## 🎯 **SUCCESS METRICS**

Version 8.0 is successful if:

✅ **Auto-Update:**
- Users can update with 1 click
- Settings preserved 100% of the time
- Rollback works if needed

✅ **Windows Service:**
- Service installs without errors
- Service runs 24/7 without crashes
- Scheduled scans execute reliably

✅ **Data Layer:**
- All data persists correctly
- Database switching works
- Performance is acceptable (scans < 5 seconds per 1000 computers)

✅ **User Experience:**
- No breaking changes for v7 users
- Migration is seamless
- Clear documentation

---

**Ready to revolutionize ArtaznIT with v8.0! 🚀**
