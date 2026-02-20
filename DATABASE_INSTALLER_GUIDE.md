# Database Setup Wizard - Quick Start Guide
<!-- TAG: #AUTO_UPDATE_DATABASE_INSTALLER #QUICK_START #USER_GUIDE -->
**Version:** 3.0 (3.2602.0.0)
**Last Updated:** February 20, 2026

---

## 🎯 Overview

The **DatabaseSetupWizard** is a comprehensive 3-step wizard that guides you through database configuration with automatic dependency checking and component installation.

---

## 🚀 Quick Start

### **Opening the Wizard**

**Option 1: From SuperAdmin Window**
1. Open SuperAdmin (Ctrl+Shift+Alt+S or 5 rapid clicks on version badge)
2. Navigate to Tools → Database Setup Wizard

**Option 2: From Main Window**
1. Go to Options → Configure Database
2. Click "Database Setup Wizard"

**Option 3: Programmatically**
```csharp
var wizard = new DatabaseSetupWizard();
if (wizard.ShowDialog() == true)
{
    string dbType = wizard.DatabaseType;
    string connString = wizard.ConnectionString;
    // Database is now configured
}
```

---

## 📋 Step-by-Step Process

### **Step 1: Choose Database Type**

Select your preferred database backend:

| Option | Best For | Requirements |
|--------|----------|--------------|
| **SQLite** ⭐ | Single-user, up to 100K computers | None (built-in) |
| **SQL Server** | Multi-user, enterprise, unlimited scale | SQL Server installed |
| **Access** | Office integration, up to 50K computers | ACE Database Engine |
| **CSV/JSON** | Portable, small environments (<1K computers) | None (built-in) |

**Recommended:** SQLite for most users

---

### **Step 2: Configuration**

Configure connection settings based on your selection:

#### **SQLite Configuration**
- **Database File:** Browse to choose location (default: `%AppData%\NecessaryAdminTool\NecessaryAdminTool.db`)
- **Encryption:** Optional SQLCipher AES-256 encryption
  - If enabled, enter a strong password
  - ⚠️ **WARNING:** You cannot recover data without this password!

#### **SQL Server Configuration**
- **Server Name:** Enter SQL Server instance (e.g., `SQLSERVER01` or `localhost\SQLEXPRESS`)
- **Database Name:** Database to create/use (default: `NecessaryAdminTool`)
- **Authentication:**
  - Windows Authentication (recommended)
  - SQL Server Authentication (username + password)
- **Test Connection:** Validates before proceeding

#### **Access Configuration**
- **Database File:** Browse to choose location (default: `%AppData%\NecessaryAdminTool\NecessaryAdminTool.accdb`)
- **Auto-Creation:** Will create .accdb file if it doesn't exist

#### **CSV/JSON Configuration**
- **Storage Directory:** Folder for data files
- **Format:** Choose CSV or JSON

---

### **Step 3: Dependency Check**

The wizard automatically checks for required components:

#### **SQLite**
- ✅ No dependencies required
- Works out of the box

#### **SQL Server**
- Tests server connectivity
- If fails: Provides download link for SQL Server Express (free)
- Installation guidance included

#### **Access**
- Checks for Microsoft Access Database Engine (ACE)
- Detection methods:
  - Registry check (CLSID `{3BE786A0-0366-4F5C-9434-25CF162E475E}`)
  - OleDb provider enumeration
- If missing: Provides download link for 64-bit ACE installer
- **Note:** Office 365 users may already have this installed

#### **CSV/JSON**
- ✅ No dependencies required
- Validates directory access

---

## 🔧 Automated Features

### **Connection Testing**
- Click "Test Connection" at any time during Step 2
- Validates configuration before finalizing
- Provides helpful error messages and hints

### **Dependency Installer**
- Detects missing components automatically
- Opens browser to official Microsoft download pages
- Provides step-by-step installation instructions

### **Auto-Configuration**
- Creates database files if they don't exist
- Creates directories automatically
- Saves configuration to `Settings.Default`

---

## 🐛 Troubleshooting

### **SQLite Issues**

**Error:** "SQLite not enabled"
- **Solution:** Ensure NuGet package `System.Data.SQLite.Core` is installed

**Error:** "Access denied"
- **Solution:** Run NecessaryAdminTool as Administrator

---

### **SQL Server Issues**

**Error:** "Cannot connect to server" (error -1 or 53)
- **Solution:** Check that SQL Server is running
- Verify server name is correct
- Check network connectivity and firewall rules

**Error:** "Login failed" (error 18456)
- **Solution:** Verify credentials
- Check SQL Server authentication mode (Windows vs SQL)
- Ensure user has `db_owner` permissions

**Error:** "Database does not exist"
- **Solution:** Wizard will create it automatically
- Ensure user has `CREATE DATABASE` permission

---

### **Access Issues**

**Error:** "Provider not registered"
- **Solution:** Install ACE Database Engine from Step 3
- Download: https://www.microsoft.com/en-us/download/details.aspx?id=54920
- **Important:** Install 64-bit version for NecessaryAdminTool (x64)

**Error:** "Syntax error in field definition"
- **Solution:** This is a known issue with empty databases
- Fixed in v1.0 with improved error handling
- Database will be created automatically on first use

**Error:** "Could not find installable ISAM"
- **Solution:** File path too long or invalid characters
- Move database to shorter path (e.g., `C:\Data\NAT.accdb`)

---

### **CSV/JSON Issues**

**Error:** "Access denied to directory"
- **Solution:** Choose a different directory
- Ensure user has write permissions
- Run as Administrator if needed

---

## 📝 Configuration Files

After setup, configuration is saved to:

**Location:** `%AppData%\NecessaryAdminTool\user.config`

**Properties Updated:**
- `DatabaseType` - Selected database type (SQLite, SqlServer, Access, CSV, JSON)
- `DatabasePath` - Connection string or file path

**To Reset Configuration:**
1. Delete `user.config`
2. Restart NecessaryAdminTool
3. Wizard will prompt for reconfiguration

---

## 🔒 Security Notes

### **SQLite Encryption**
- If encryption is enabled, password is stored in Windows Credential Manager
- Credential name: `NecessaryAdminTool_SQLite_Encryption`
- Uses `SecureCredentialManager` for secure storage

### **SQL Server Authentication**
- Windows Authentication uses Kerberos (most secure)
- SQL Authentication credentials are stored encrypted
- Connection strings are stored in user.config (encrypted by .NET)

### **Access Databases**
- No built-in encryption in .accdb format
- Store in secured directory with NTFS permissions
- Consider using Access 2007+ encryption features

---

## 📊 Database Provider Comparison

| Feature | SQLite | SQL Server | Access | CSV/JSON |
|---------|--------|------------|--------|----------|
| **Setup Difficulty** | ⭐ Easy | ⭐⭐⭐ Moderate | ⭐⭐ Medium | ⭐ Easy |
| **Capacity** | 100K computers | Unlimited | 50K computers | <1K computers |
| **Performance** | ⭐⭐⭐⭐ Fast | ⭐⭐⭐⭐⭐ Fastest | ⭐⭐⭐ Good | ⭐⭐ Slow |
| **Multi-User** | No | ✅ Yes | Limited | No |
| **Encryption** | ✅ SQLCipher | ✅ TDE | ❌ Manual | ❌ No |
| **Backup** | File copy | SQL Backup | File copy | File copy |
| **Cost** | Free | Free (Express) / Paid (Enterprise) | Free (Runtime) / Paid (Full) | Free |

---

## 🤖 Auto-Update Tags

This installer includes the following auto-update tags for future maintenance:

- `#AUTO_UPDATE_DATABASE_INSTALLER` - Main installer tag
- `#DATABASE_SETUP` - Setup wizard code
- `#ACE_DRIVER_DETECTION` - ACE driver registry detection
- `#SQL_SERVER_TEST` - SQL Server connectivity testing
- `#CONNECTION_TESTING` - Connection validation logic
- `#AUTO_INSTALLER` - Component download helpers

**For Future Claudes:**
- Update download URLs if Microsoft changes them
- Update registry paths if ACE installer changes
- Add new database providers by implementing `IDataProvider`
- Update dependency detection logic as needed

---

## 📞 Support

**Database Setup Issues:**
1. Check this guide's Troubleshooting section
2. See `DATABASE_GUIDE.md` for detailed technical information
3. Enable debug logging (SuperAdmin → Advanced Settings → Logging)
4. Check `%AppData%\NecessaryAdminTool\NecessaryAdmin_Debug.log`

**Provider-Specific Documentation:**
- SQLite: `DATABASE_GUIDE.md` → SQLite Provider section
- SQL Server: `DATABASE_GUIDE.md` → SQL Server Provider section
- Access: `DATABASE_GUIDE.md` → Access Provider section
- CSV/JSON: `DATABASE_GUIDE.md` → CSV/JSON Provider section

---

**Built with Claude Code** 🤖
