# NecessaryAdminTool - Database Setup Guide
<!-- TAG: #DATABASE_SETUP #INITIAL_SETUP #USER_GUIDE #VERSION_1_0 -->
**Version:** 1.0 (1.2602.0.0)
**Last Updated:** February 15, 2026

---

## 🎯 Quick Start - Which Database Should I Use?

**Choose based on your environment:**

| Database Type | Best For | Max Capacity | Setup Time | Skill Level |
|--------------|----------|--------------|------------|-------------|
| **SQLite** ⭐ | Single user, small-medium teams | 100,000+ computers | 30 seconds | Beginner |
| **SQL Server** | Enterprise, multi-user, unlimited scale | Unlimited | 5-10 minutes | Advanced |
| **Microsoft Access** | Excel users, familiar Office tools | ~50,000 computers (2GB limit) | 2 minutes | Intermediate |
| **CSV/JSON** | Portable, human-readable fallback | ~10,000 computers | 0 seconds (automatic) | Beginner |

---

## 📊 Detailed Database Comparison

### **1. SQLite (Recommended for Most Users) ⭐**

**What is SQLite?**
- Self-contained file-based database (no server required)
- Industry-standard used by Chrome, Firefox, iOS, Android
- AES-256 encryption with SQLCipher

**Pros:**
- ✅ Zero configuration - just works
- ✅ No installation required - included with NecessaryAdminTool
- ✅ Encrypted by default (AES-256)
- ✅ Fast performance (100,000+ computers easily)
- ✅ Single file = easy backup/restore
- ✅ Portable - copy file to USB/network share

**Cons:**
- ❌ Single user only (file locking prevents concurrent access)
- ❌ Not ideal for network shares (file corruption risk)

**File Location:**
```
Default: C:\ProgramData\NecessaryAdminTool\NecessaryAdminTool.db
Encrypted: Uses SQLCipher with password from Windows Credential Manager
```

**When to Use SQLite:**
- ✅ Single administrator managing fleet
- ✅ Local workstation installation
- ✅ Up to 100,000 computers
- ✅ Need quick setup without complexity
- ✅ Security concerns (encryption built-in)

**Setup Steps:**
1. Select "SQLite (Recommended)" in Setup Wizard
2. Choose database folder (default: `C:\ProgramData\NecessaryAdminTool`)
3. Click "Test Database" to verify (optional)
4. Click "Finish Setup"
5. ✅ Done! Database created automatically on first use

---

### **2. SQL Server (Enterprise-Grade)**

**What is SQL Server?**
- Microsoft's enterprise database platform
- Supports unlimited computers and concurrent users
- Full ACID compliance, transaction support

**Pros:**
- ✅ Unlimited capacity (millions of computers)
- ✅ Multi-user concurrent access
- ✅ Enterprise backup/restore tools
- ✅ High availability (clustering, AlwaysOn)
- ✅ Auditing and compliance features
- ✅ Centralized management (SSMS)

**Cons:**
- ❌ Requires SQL Server installation (Express is free)
- ❌ More complex configuration
- ❌ Network connectivity required
- ❌ Licensing costs (except SQL Server Express)

**Prerequisites:**
- SQL Server 2012 or newer (Express, Standard, or Enterprise)
- Network access to SQL Server instance
- Database credentials (Windows Auth or SQL Auth)

**Connection String Examples:**

**Windows Authentication (Recommended):**
```
Server=SERVER-NAME\SQLEXPRESS;Database=NecessaryAdminTool;Integrated Security=true;
```

**SQL Server Authentication:**
```
Server=192.168.1.100;Database=NecessaryAdminTool;User Id=admin;Password=SecurePass123!;
```

**When to Use SQL Server:**
- ✅ Multiple administrators accessing simultaneously
- ✅ Enterprise environment (100+ computers)
- ✅ Existing SQL Server infrastructure
- ✅ Need high availability/disaster recovery
- ✅ Centralized database team managing backups

**Setup Steps:**

**Option A: Automated Setup (NecessaryAdminTool creates database)**
1. Install SQL Server (Express is free)
2. Note server name: `Computer Name\SQLEXPRESS`
3. In Setup Wizard:
   - Select "SQL Server"
   - Enter connection string
   - Click "Test Database"
   - Click "Finish Setup"
4. ✅ NecessaryAdminTool creates schema automatically

**Option B: Manual SQL Server Setup**
1. Open SQL Server Management Studio (SSMS)
2. Create new database:
   ```sql
   CREATE DATABASE NecessaryAdminTool
   GO

   USE NecessaryAdminTool
   GO

   -- Schema will be created by NecessaryAdminTool on first run
   ```
3. Create login (SQL Auth):
   ```sql
   CREATE LOGIN nat_admin WITH PASSWORD = 'YourSecurePassword!'
   GO

   USE NecessaryAdminTool
   GO

   CREATE USER nat_admin FOR LOGIN nat_admin
   GO

   ALTER ROLE db_owner ADD MEMBER nat_admin
   GO
   ```
4. In NecessaryAdminTool Setup Wizard:
   - Select "SQL Server"
   - Enter connection string
   - Click "Test Database"
   - Click "Finish Setup"

**Troubleshooting:**

| Error | Solution |
|-------|----------|
| "SQL Server not found" | Enable TCP/IP in SQL Server Configuration Manager |
| "Login failed for user" | Check credentials, grant db_owner role |
| "Network-related error" | Check firewall (port 1433), enable SQL Server Browser service |
| "Syntax error" | Normal for new database - schema created on first use |

---

### **3. Microsoft Access**

**What is Microsoft Access?**
- Desktop database from Microsoft Office suite
- `.accdb` file format
- Excel-like interface for direct editing

**Pros:**
- ✅ Familiar interface for Office users
- ✅ Can open database in Access for manual editing
- ✅ Export to Excel easily
- ✅ No server required
- ✅ Good performance for small-medium fleets

**Cons:**
- ❌ 2GB file size limit (~50,000 computers)
- ❌ Single user only (file locking)
- ❌ Requires Microsoft Access Database Engine
- ❌ Not cross-platform (Windows only)
- ❌ No encryption by default

**Prerequisites:**
- Microsoft Access Database Engine 2016 (64-bit)
- Download: https://www.microsoft.com/en-us/download/details.aspx?id=54920

**File Location:**
```
Default: C:\ProgramData\NecessaryAdminTool\NecessaryAdminTool.accdb
```

**When to Use Access:**
- ✅ You have Microsoft Office/Access installed
- ✅ Need to manually edit data in Access UI
- ✅ Export to Excel frequently
- ✅ Up to 50,000 computers
- ✅ Comfortable with Access tools

**Setup Steps:**
1. Install Microsoft Access Database Engine 2016:
   - Download 64-bit version from Microsoft
   - Run `accessdatabaseengine_X64.exe`
   - Close all Office apps first
2. In Setup Wizard:
   - Select "Microsoft Access"
   - Choose database folder
   - Click "Test Database"
   - Click "Finish Setup"
3. ✅ Database file created automatically

**Troubleshooting:**

| Error | Solution |
|-------|----------|
| "ACE not registered" | Install Access Database Engine 2016 (64-bit) |
| "Could not use ''; file already in use" | Close Access if open, check for `.laccdb` lock file |
| "Syntax error in CREATE TABLE" | Normal for new DB - NecessaryAdminTool creates schema |
| "Database exceeds 2GB" | Compact database or migrate to SQL Server |

---

### **4. CSV/JSON (Fallback/Portable)**

**What is CSV/JSON?**
- Human-readable text files
- `Computers.csv`, `Inventory.json`, etc.
- No special software required

**Pros:**
- ✅ Works immediately - no installation
- ✅ Human-readable with Notepad/Excel
- ✅ Portable - copy to USB drive
- ✅ Easy to edit manually if needed
- ✅ Version control friendly (Git)
- ✅ No dependencies or drivers

**Cons:**
- ❌ Slowest performance (linear search)
- ❌ Limited capacity (~10,000 computers recommended)
- ❌ No transactions or ACID compliance
- ❌ File corruption risk if edited while running
- ❌ No query optimization

**File Structure:**
```
C:\ProgramData\NecessaryAdminTool\
├── Computers.csv          (Computer inventory)
├── Inventory.json         (Detailed scans)
├── Bookmarks.csv          (Favorite computers)
└── ConnectionProfiles.csv (Saved DC connections)
```

**When to Use CSV/JSON:**
- ✅ Testing/evaluation
- ✅ Portable deployment (USB drive)
- ✅ Very small fleet (<1,000 computers)
- ✅ No database server available
- ✅ Temporary/quick scans
- ✅ You need to script/automate data import

**Setup Steps:**
1. In Setup Wizard:
   - Select "CSV/JSON (Fallback)"
   - Choose folder location
   - Click "Finish Setup"
2. ✅ Files created automatically on first scan

**Manual CSV Editing:**
```csv
Hostname,IPAddress,OS,LastSeen,Status
SERVER-DC01,192.168.1.10,Windows Server 2022,2026-02-15,Online
DESKTOP-001,192.168.1.100,Windows 11 Pro,2026-02-15,Online
```

---

## 🔧 Initial Setup Wizard Walkthrough

### **Step 1: Select Database Type**

When you launch NecessaryAdminTool for the first time, the Setup Wizard appears.

**Quick Decision Tree:**

```
Do you have SQL Server available?
├─ YES → Use SQL Server (unlimited capacity)
└─ NO
   ├─ Do you need to share data with multiple users?
   │  ├─ YES → Install SQL Server Express (free)
   │  └─ NO
   │     ├─ Do you have Microsoft Access?
   │     │  ├─ YES → Use Access (<50k computers)
   │     │  └─ NO
   │     │     ├─ Fleet size?
   │     │     │  ├─ <10k computers → CSV/JSON (simplest)
   │     │     │  └─ >10k computers → SQLite (recommended)
```

### **Step 2: Database Location**

**Local Storage (Recommended):**
```
C:\ProgramData\NecessaryAdminTool
```
- ✅ Persistent across reboots
- ✅ Protected from accidental deletion
- ✅ All users can access (if needed)

**Network Share (Advanced):**
```
\\SERVER\Share\NecessaryAdminTool
```
- ⚠️ **Warning:** SQLite/Access on network shares can corrupt
- ✅ Only use with SQL Server
- ✅ Requires proper SMB permissions

**Custom Location:**
- Click "📁 Browse..." to select folder
- Folder must exist or be creatable
- Must have write permissions

### **Step 3: Background Service (Optional)**

**What does it do?**
- Runs automatic scans every 2 hours (configurable)
- Updates computer status in background
- No user interaction required

**Options:**
- ✅ **Install Windows Service** (Recommended if admin)
  - Runs as SYSTEM account
  - Survives logoffs
  - Requires admin rights

- ⚠️ **Scheduled Task** (Fallback)
  - Runs as your user account
  - Only works when logged in
  - Used if not admin

- ❌ **Manual Only**
  - You run scans manually
  - No automatic updates

**Recommendation:**
- Enable for production environments
- Disable for testing/evaluation
- Set to "Manual Only" for low-change environments

### **Step 4: Scan Interval**

Choose how often automatic scans run:

| Interval | Best For | Notes |
|----------|----------|-------|
| Every hour | Dynamic environments, workstations joining/leaving | High CPU usage |
| **Every 2 hours** ⭐ | Most environments (default) | Balanced performance |
| Every 4 hours | Stable servers, low-change | Lower resource usage |
| Daily | Very stable, monitoring only | Minimal overhead |
| Manual only | Testing, on-demand scanning | No automatic scans |

### **Step 5: Test Database (Recommended)**

**What does it test?**
- ✅ Database connectivity
- ✅ Table creation
- ✅ CRUD operations (Create, Read, Update, Delete)
- ✅ Transaction support
- ✅ Schema validation
- ✅ Performance benchmarks

**Test Duration:**
- SQLite: 5-10 seconds
- SQL Server: 10-30 seconds
- Access: 10-20 seconds
- CSV: 2-5 seconds

**Sample Test Output:**
```
✓ Connection Test: PASSED
✓ Create Table: PASSED
✓ Insert Record: PASSED (10ms)
✓ Select Query: PASSED (5ms)
✓ Update Record: PASSED (8ms)
✓ Delete Record: PASSED (6ms)
✓ Transaction Support: PASSED

Tests: 15/15 passed
Duration: 8.3 seconds

The SQLite provider is working correctly.
```

**If Tests Fail:**
- Review error log: `%AppData%\NecessaryAdminTool\Logs\`
- Check database permissions
- Verify prerequisites installed
- See troubleshooting section below

### **Step 6: Finish Setup**

Click **"✓ Finish Setup"** to save configuration and launch NecessaryAdminTool.

**What Happens:**
1. Settings saved to `%AppData%\NecessaryAdminTool\user.config`
2. Database schema created (if needed)
3. Scheduled task installed (if enabled)
4. Main window opens

---

## 🔍 Post-Setup Verification

**Verify Database is Working:**

1. Open NecessaryAdminTool
2. Click **Login** (authenticate with AD credentials)
3. Go to **Fleet Management** tab
4. Click **"🔍 SCAN DOMAIN"** or **"🌳 LOAD AD OBJECTS"**
5. Wait for scan to complete
6. Check **Fleet Grid** - should show computers

**Check Database File:**

**SQLite:**
```powershell
Test-Path "C:\ProgramData\NecessaryAdminTool\NecessaryAdminTool.db"
# Should return: True
```

**Access:**
```powershell
Test-Path "C:\ProgramData\NecessaryAdminTool\NecessaryAdminTool.accdb"
# Should return: True
```

**CSV:**
```powershell
Test-Path "C:\ProgramData\NecessaryAdminTool\Computers.csv"
# Should return: True (after first scan)
```

---

## 🛠️ Troubleshooting

### **Common Issues:**

#### **"Setup wizard not appearing on first run"**

**Cause:** Setup already completed
**Solution:**
```powershell
# Reset first-run flag (DEBUG builds only)
# Hold CTRL+SHIFT during startup to bypass setup
```

Or manually edit config:
```
%AppData%\NecessaryAdminTool\user.config
Find: <setting name="SetupCompleted" serializeAs="String"><value>True</value></setting>
Change to: <value>False</value>
```

#### **"Database file not found" after setup**

**Cause:** Folder doesn't exist or no write permissions
**Solution:**
1. Check folder exists: `C:\ProgramData\NecessaryAdminTool`
2. Verify write permissions (run as admin if needed)
3. Try different location (e.g., `%AppData%\NecessaryAdminTool`)

#### **"SQLite encryption password not found"**

**Cause:** Windows Credential Manager entry missing
**Solution:**
- NecessaryAdminTool creates password automatically on first use
- Check: Control Panel → Credential Manager → Windows Credentials
- Look for: `NecessaryAdminTool_DBPassword`

#### **"Access database locked"**

**Cause:** Another process has database open
**Solution:**
1. Close Microsoft Access if open
2. Delete lock file: `NecessaryAdminTool.laccdb`
3. Reopen NecessaryAdminTool

#### **"SQL Server connection timeout"**

**Cause:** Network/firewall issue or SQL Server not running
**Solution:**
1. Verify SQL Server running:
   ```powershell
   Get-Service MSSQL*
   ```
2. Enable TCP/IP:
   - SQL Server Configuration Manager
   - SQL Server Network Configuration
   - Protocols for SQLEXPRESS
   - Right-click TCP/IP → Enable
3. Restart SQL Server
4. Check firewall (port 1433)

---

## 🔄 Changing Database After Setup

**Can I switch databases later?**
Yes! But current data won't be migrated automatically.

**Steps to Switch:**
1. Open NecessaryAdminTool
2. Go to **Options** (⚙️) → **Database & Configuration**
3. Click **"RECONFIGURE"** button
4. Choose new database type
5. Click **"Apply"** and restart

**Migrating Data:**
- Export from old database: **Options → EXPORT ALL**
- Configure new database
- Import to new database: **Options → IMPORT ALL**

---

## 📊 Database Maintenance

### **SQLite Maintenance:**

**Optimize Database (Vacuum):**
- Options → Database Operations → **"OPTIMIZE"**
- Reclaims unused space
- Rebuilds indexes
- Recommended: Monthly

**Backup:**
```powershell
# Simple file copy
Copy-Item "C:\ProgramData\NecessaryAdminTool\NecessaryAdminTool.db" `
          "C:\Backups\NAT_Backup_$(Get-Date -Format 'yyyy-MM-dd').db"
```

**Restore:**
```powershell
# Replace current database
Copy-Item "C:\Backups\NAT_Backup_2026-02-15.db" `
          "C:\ProgramData\NecessaryAdminTool\NecessaryAdminTool.db" -Force
```

### **SQL Server Maintenance:**

**Backup:**
```sql
BACKUP DATABASE NecessaryAdminTool
TO DISK = 'C:\Backups\NAT_Backup.bak'
WITH FORMAT, COMPRESSION
```

**Restore:**
```sql
RESTORE DATABASE NecessaryAdminTool
FROM DISK = 'C:\Backups\NAT_Backup.bak'
WITH REPLACE
```

---

## 📈 Performance Tuning

### **SQLite:**
- Keep database file on SSD
- Don't use network shares
- Run OPTIMIZE monthly
- Limit to 100,000 computers

### **SQL Server:**
- Increase memory allocation
- Add indexes to frequently queried columns
- Enable SQL Server compression
- Use SSD for data files

### **Access:**
- Compact & Repair monthly
- Split database if >1GB
- Don't exceed 50,000 computers

### **CSV:**
- Keep under 10,000 computers
- Use SQLite for better performance

---

## 🔐 Security Best Practices

1. ✅ Use **SQLite encryption** (AES-256) for sensitive data
2. ✅ Store database on **encrypted drive** (BitLocker)
3. ✅ Use **SQL Server Windows Auth** instead of SQL auth
4. ✅ **Restrict file permissions** on database folder
5. ✅ **Backup regularly** (daily for production)
6. ✅ Test **restore procedure** quarterly
7. ❌ **Never** store database on network share (SQLite/Access)
8. ❌ **Never** commit database files to Git

---

## 📞 Getting Help

**Database Issues:**
- Check logs: `%AppData%\NecessaryAdminTool\Logs\NAT_YYYY-MM-DD.log`
- Click **"DEBUG LOG"** button in main window
- Report issues: https://github.com/anthropics/claude-code/issues

**Documentation:**
- `DATABASE_GUIDE.md` - Detailed database architecture
- `DATABASE_INSTALLER_GUIDE.md` - Advanced deployment
- `CLAUDE.md` - Developer documentation

---

**Built with Claude Code** 🤖
