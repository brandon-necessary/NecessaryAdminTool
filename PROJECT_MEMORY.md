# NecessaryAdminTool Project Memory

## 🚨 CRITICAL: PROJECT REBRAND IN PROGRESS

### **OLD IDENTITY (v7.x - COMPLETE):**
- Name: ArtaznIT Suite
- GitHub: https://github.com/brandon-necessary/JadexIT2.git
- Final Version: 7.2603.5.0 (CalVer)
- Status: ✅ COMPLETE - All v7 features shipped
- Final Commit: d296b1c (Feb 14, 2026)

### **NEW IDENTITY (v1.x - STARTING NOW):**
- Name: **NecessaryAdminTool**
- GitHub: **https://github.com/brandon-necessary/NecessaryAdminTool** (NEW REPO - TO BE CREATED)
- Starting Version: **1.0.0.0** (SemVer)
- Status: 🚀 READY TO BEGIN - Week 0 Rebrand

## Project Overview
- **Current Location**: `C:\Users\brandon.necessary\source\repos\ArtaznIT` (WILL MOVE TO NecessaryAdminTool)
- **⭐ READ FIRST**: `VERSION_1.0_HANDOFF.md` (complete rebrand + v1.0 implementation guide)

## Theme & Branding
- Orange/Zinc gradient theme (keeping same colors)
- Primary: #FF6B35 (orange)
- Secondary: #71797E (zinc)
- Background: #0D0D0D (dark)
- Success: LimeGreen
- Use modularity tags: `TAG: #FEATURE_NAME #CATEGORY`

## User Preferences
- "GO FULL AUTO" - prefers autonomous implementation
- Values security: **all databases must be encrypted**
- Likes dedicated tabs for major features
- Values context menus and easy access
- Wants professional, enterprise-grade solution
- Appreciates detailed documentation

## Key Architecture (v7.x Legacy)
- WPF application (.NET Framework 4.8.1)
- Uses Windows Credential Manager for secure storage
- Settings stored in Properties/Settings.settings
- Auto-save backups: `%AppData%\ArtaznIT\AutoSave\`
- 5 main tabs (5th is RemoteControlTab)

## Version 7.x - ✅ COMPLETE (Legacy Codebase)

**All v7.1 Features Complete:**
✅ Dashboard Analytics (v7.2603.1.0)
✅ Automated Remediation (v7.2603.2.0)
✅ Custom Script Executor (v7.2603.3.0)
✅ Asset Tagging System (v7.2603.4.0)
✅ Advanced Filtering (v7.2603.4.0)
✅ Patch Management (v7.2603.4.0)
✅ UI Improvements (v7.2603.5.0)

**Final Version:** 7.2603.5.0
**Total LOC in v7:** ~15,000 lines
**Repo:** https://github.com/brandon-necessary/JadexIT2.git (branch: version-7.0)

---

## Version 1.0 Planning (New NecessaryAdminTool)

### **USER-APPROVED REQUIREMENTS:**

#### 1. **Auto-Update System** ✅ APPROVED
- Use Squirrel.Windows (battle-tested, reduces dev time by 50%)
- Weekly automatic update checks
- Manual "Check for Updates" button in Help menu
- Preserve ALL settings during updates
- Rollback on failure

#### 2. **Database Layer** ✅ APPROVED
- **ALL 4 providers required:**
  1. SQLite (default) - encrypted with SQLCipher
  2. SQL Server - enterprise option
  3. Microsoft Access - legacy compatibility
  4. CSV/JSON - fallback, human-readable
- **Setup wizard on first startup** - user selects database type
- **Options menu** - change database type, move location, backup/restore
- **Default location:** `%ProgramData%\NecessaryAdminTool\`
- **User-configurable location** - can move database anywhere
- **MANDATORY ENCRYPTION** - all database types must be encrypted (AES-256)
- Encryption keys stored in Windows Credential Manager

#### 3. **Windows Service** ✅ APPROVED
- Optional installation (user choice during setup)
- Background scanning every **2 hours** (user confirmed)
- Service writes to encrypted database
- UI reads from database in real-time
- Fallback to scheduled task if not admin
- Service status visible in UI

#### 4. **Versioning** ✅ APPROVED
- **Start fresh at v1.0.0.0** (SemVer: Major.Minor.Patch.Build)
- No more CalVer (old: 7.YYMM.Minor.Build)
- Clean slate for professional release

### **IMPLEMENTATION TIMELINE:**

**Week 0: REBRAND (CRITICAL - DO THIS FIRST)**
1. Create new GitHub repo: `NecessaryAdminTool`
2. Copy codebase to new location
3. Rename all files, projects, namespaces
4. Update AssemblyInfo.cs to v1.0.0.0
5. Change all "ArtaznIT" → "NecessaryAdminTool"
6. Update AppData paths: `%AppData%\NecessaryAdminTool\`
7. Initial commit to new repo

**Week 1-2: Auto-Update**
- Install Squirrel.Windows NuGet
- Create UpdateManager.cs
- Add "Check for Updates" to Help menu
- Implement weekly auto-check
- Test update flow

**Week 3-4: Encrypted Database**
- Install SQLCipher for encrypted SQLite
- Create IDataProvider interface
- Implement all 4 providers (SQLite, SQL Server, Access, CSV)
- Create EncryptionKeyManager
- Database schema design
- Migration from v7 in-memory to v1 database

**Week 5: Setup Wizard**
- SetupWizardWindow.xaml
- Database type selection
- Location picker
- Service installation option
- Scan interval config (default: 2 hours)

**Week 6: Windows Service**
- Create NecessaryAdminTool.Service project
- Background scanning with encrypted DB
- Service installer
- Scheduled task fallback
- Service control from UI

**Week 7: Options Menu**
- Add Database tab to OptionsWindow
- Database type switcher
- Move database functionality
- Backup/restore
- Optimization tools

**Week 8: Polish & Release**
- Comprehensive testing
- Security audit (encryption)
- Performance testing
- Documentation
- Release v1.0.0.0

### **ESTIMATED EFFORT:**
- **Original Plan:** 10 weeks, ~3,300 LOC
- **Optimized Plan:** 8 weeks, ~2,500 LOC (using Squirrel.Windows saves time)

---

## Critical Files for Next Instance

**⭐ MUST READ FIRST:**
1. `C:\Users\brandon.necessary\source\repos\ArtaznIT\VERSION_1.0_HANDOFF.md` (complete implementation guide)
2. `C:\Users\brandon.necessary\source\repos\ArtaznIT\VERSION_8.0_TECHNICAL_ANALYSIS.md` (technical deep-dive)
3. `C:\Users\brandon.necessary\source\repos\ArtaznIT\VERSION_8.0_PLAN.md` (original v8 plan)

**Current Codebase:**
- Location: `C:\Users\brandon.necessary\source\repos\ArtaznIT`
- Branch: version-7.0
- Status: v7.2603.5.0 - COMPLETE and pushed to GitHub

---

## Key Technical Details

### **Encryption Strategy:**
- SQLite: SQLCipher (AES-256)
- SQL Server: Transparent Data Encryption (TDE)
- Access: JET encryption + database password
- CSV/JSON: AES-256 file encryption
- Keys stored in Windows Credential Manager

### **Database Schema:**
```sql
Tables:
- Computers (hostname, OS, status, IP, manufacturer, model, etc.)
- ComputerTags (many-to-many relationship)
- ScanHistory (track background scans)
- Settings (key-value store)
- Scripts (script library)
- Bookmarks (favorited computers)
```

### **Auto-Update Architecture:**
- Squirrel.Windows handles download, install, rollback
- GitHub Releases API integration
- Delta updates (only download changes)
- Automatic preservation of `%AppData%\NecessaryAdminTool\*`

### **Service Architecture:**
```
[NecessaryAdminTool.exe (UI)] ←→ [Encrypted SQLite DB] ←→ [NecessaryAdminTool.Service (Background)]
```

---

## Next Steps (For Next Instance)

**IMMEDIATE ACTION REQUIRED:**
1. **READ** `VERSION_1.0_HANDOFF.md` (has all implementation details)
2. **CREATE** new GitHub repo: https://github.com/brandon-necessary/NecessaryAdminTool
3. **REBRAND** all code (Week 0 checklist in handoff doc)
4. **COMMIT** initial v1.0.0.0 to new repo
5. **BEGIN** auto-update implementation (Week 1)

**DO NOT:**
- ❌ Start coding before rebrand is complete
- ❌ Skip reading VERSION_1.0_HANDOFF.md
- ❌ Forget to create new GitHub repo first
- ❌ Use old "ArtaznIT" naming anywhere in v1.0

**User Preference:**
- GO FULL AUTO mode - proceed autonomously after reading handoff doc
- Values detailed progress updates
- Appreciates security-first approach

---

## Success Criteria for v1.0

✅ **Rebrand Complete:**
- All "ArtaznIT" → "NecessaryAdminTool"
- New repo created
- v1.0.0.0 version number

✅ **Auto-Update Works:**
- 1-click updates
- Weekly auto-check
- Settings preserved
- Rollback on failure

✅ **Database Encrypted:**
- All 4 providers working
- AES-256 encryption
- Keys in Credential Manager
- < 5% performance overhead

✅ **Service Operational:**
- Installs successfully
- 2-hour scan interval
- Results in UI immediately
- Scheduled task fallback

✅ **Professional UX:**
- Setup wizard
- Options menu
- Zero data loss
- Enterprise-grade

---

**PROJECT STATUS: READY FOR v1.0 REBRAND 🚀**
