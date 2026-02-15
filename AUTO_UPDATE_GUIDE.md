# Auto-Update Tag System Guide
<!-- TAG: #AUTO_UPDATE_SYSTEM #FUTURE_CLAUDES #MAINTENANCE_GUIDE -->
<!-- This file documents all auto-update tags for future Claude instances -->
<!-- Last Updated: 2026-02-14 | Version: 1.0 (1.2602.0.0) -->

---

## 🎯 Purpose

This document serves as a **master index** of all auto-update tags in the NecessaryAdminTool codebase. Future Claude instances can reference this file to quickly identify which files need updating when:
- Releasing a new version
- Adding new features
- Updating performance benchmarks
- Changing documentation

---

## 🏷️ Auto-Update Tag Index

### **Version & Release Tags**

| Tag | Purpose | Files | Update Frequency |
|-----|---------|-------|------------------|
| **#AUTO_UPDATE_VERSION** | Version numbers and build dates | AssemblyInfo.cs, AboutWindow.xaml | Every release |
| **#VERSION_SYSTEM** | Version tracking system | AssemblyInfo.cs | Major versions |
| **#VERSION_DISPLAY** | UI-displayed version info | AboutWindow.xaml, README.md | Every release |
| **#CALVER** | CalVer versioning format | AssemblyInfo.cs | Every release |

### **Documentation Tags**

| Tag | Purpose | Files | Update Frequency |
|-----|---------|-------|------------------|
| **#AUTO_UPDATE_README** | Main project documentation | README_COMPREHENSIVE.md, README.md | Major features/versions |
| **#AUTO_UPDATE_FAQ** | Frequently asked questions | FAQ.md | When new common questions arise |
| **#AUTO_UPDATE_DATABASE** | Database system documentation | DATABASE_GUIDE.md | When providers/benchmarks change |
| **#AUTO_UPDATE_DATABASE_INSTALLER** | Database setup wizard code | DatabaseSetupWizard.xaml, DatabaseSetupWizard.xaml.cs | When database setup logic changes |
| **#COMPREHENSIVE_DOCS** | Master documentation identifier | README_COMPREHENSIVE.md, FAQ.md | Major updates |
| **#QUICK_README** | Short README | README.md | Every release |

### **Feature & Metrics Tags**

| Tag | Purpose | Files | Update Frequency |
|-----|---------|-------|------------------|
| **#AUTO_UPDATE_FEATURES** | Feature count and list | FEATURES.md | When features are added |
| **#FEATURE_COUNT** | Total feature count (currently 169) | FEATURES.md | When features are added |
| **#AUTO_UPDATE_BENCHMARKS** | Performance benchmarks | OPTIMIZATIONS.md | When optimizations are made |
| **#PERFORMANCE_METRICS** | Performance test results | OPTIMIZATIONS.md | Major performance changes |

### **UI & Theme Tags**

| Tag | Purpose | Files | Update Frequency |
|-----|---------|-------|------------------|
| **#ABOUT_WINDOW** | About dialog content | AboutWindow.xaml | Every release |
| **#UNIFIED_THEME** | Theme system documentation | THEME_SYSTEM.md | Theme changes |
| **#VERSION_1_0** | Version-specific content | Multiple files | Per version |

---

## 📁 Files with Auto-Update Tags

### **Critical Files (Update Every Release)**

1. **AssemblyInfo.cs**
   - Location: `NecessaryAdminTool\Properties\AssemblyInfo.cs`
   - Tags: `#AUTO_UPDATE_VERSION`, `#VERSION_SYSTEM`, `#CALVER`
   - What to update:
     ```csharp
     // Line 73-74: Version numbers
     [assembly: AssemblyVersion("1.2602.0.0")]  // ← Update this
     [assembly: AssemblyFileVersion("1.2602.0.0")]  // ← Update this

     // Lines 44-71: Version comments and release notes
     ```

2. **AboutWindow.xaml**
   - Location: `NecessaryAdminTool\AboutWindow.xaml`
   - Tags: `#AUTO_UPDATE_VERSION`, `#ABOUT_WINDOW`, `#VERSION_DISPLAY`
   - What to update:
     ```xml
     <!-- Line 3: Comment with current version -->
     <!-- Current Version: 1.0 (1.2602.0.0) | Build Date: February 14, 2026 -->

     <!-- Line 199: Displayed version number -->
     <TextBlock Text="1.2602.0.0" ... />  // ← Update this

     <!-- Line 204: Build date -->
     <TextBlock Text="February 14, 2026" ... />  // ← Update this
     ```

3. **README_COMPREHENSIVE.md**
   - Location: Root directory
   - Tags: `#AUTO_UPDATE_README`, `#COMPREHENSIVE_DOCS`
   - What to update:
     - Line 3: Last Updated date
     - Line 8: Version badge
     - Version History section (add new version)
     - Feature counts if changed
     - Performance benchmarks if improved

4. **README.md**
   - Location: Root directory
   - Tags: `#AUTO_UPDATE_README`, `#QUICK_README`, `#VERSION_DISPLAY`
   - What to update:
     - Line 7: Version number in header
     - Version history section

---

### **Important Files (Update When Changed)**

5. **FAQ.md**
   - Location: Root directory
   - Tags: `#AUTO_UPDATE_FAQ`
   - What to update:
     - Line 3: Last Updated date
     - Add new Q&A as common questions arise
     - Update version-specific answers

6. **FEATURES.md**
   - Location: Root directory
   - Tags: `#AUTO_UPDATE_FEATURES`, `#FEATURE_COUNT`
   - What to update:
     - Line 2: Last Updated date
     - Line 3: Current Version
     - Line 11: Total feature count (currently 169)
     - Add new features to appropriate sections

7. **OPTIMIZATIONS.md**
   - Location: Root directory
   - Tags: `#AUTO_UPDATE_BENCHMARKS`, `#PERFORMANCE_METRICS`
   - What to update:
     - Line 2: Last Updated date
     - Performance benchmark tables (lines 389-400)
     - Add new optimization techniques

8. **THEME_SYSTEM.md**
   - Location: Root directory
   - Tags: `#UNIFIED_THEME`
   - What to update:
     - Color palette if theme colors change
     - New control styles added

9. **DATABASE_TESTING.md**
   - Location: Root directory
   - Tags: `#DATABASE_TESTING`
   - What to update:
     - Performance benchmarks (line 219-226)
     - New test categories if added

10. **DATABASE_GUIDE.md**
    - Location: Root directory
    - Tags: `#AUTO_UPDATE_DATABASE`
    - What to update:
      - Version numbers (line 3)
      - Database provider comparison table (when new features added)
      - Performance benchmarks (when optimization occurs)
      - Setup instructions (if process changes)

11. **DatabaseSetupWizard.xaml / .xaml.cs**
    - Location: `NecessaryAdminTool\DatabaseSetupWizard.xaml` and `.xaml.cs`
    - Tags: `#AUTO_UPDATE_DATABASE_INSTALLER`, `#DATABASE_SETUP`, `#VERSION_1_0`
    - What to update:
      - Database provider download URLs (when Microsoft updates links)
      - Dependency check logic (when new requirements emerge)
      - Connection string templates (when providers update)
      - ACE driver detection registry keys (if Microsoft changes installer)
      - SQL Server version recommendations (when new versions release)
    - Critical sections:
      - `BtnDownloadAce_Click` - ACE download URL (line ~670)
      - `BtnDownloadSqlExpress_Click` - SQL Server download URL (line ~690)
      - `CheckAceDriverInstalled` - Registry paths for ACE detection (line ~620)
      - `TestSqlServerConnectionAsync` - SQL Server connection logic (line ~350)
      - `TestAccessConnectionAsync` - Access/ACE testing logic (line ~400)

---

## 🔍 How to Find Auto-Update Tags

### **Method 1: Grep Search**
```bash
# Find all files with auto-update tags
grep -r "#AUTO_UPDATE" --include="*.md" --include="*.cs" --include="*.xaml"

# Find specific tag
grep -r "#AUTO_UPDATE_VERSION" --include="*.cs" --include="*.xaml"
```

### **Method 2: Use Glob Tool**
```
Pattern: **/*
Then grep for: #AUTO_UPDATE
```

### **Method 3: Read This File**
You're reading it now! This AUTO_UPDATE_GUIDE.md is the master index.

---

## 📝 Update Checklist for New Releases

When releasing a new version (e.g., v1.1.0), follow this checklist:

### **1. Version Numbers** ✅
- [ ] Update `AssemblyInfo.cs` lines 73-74 with new version
- [ ] Update `AssemblyInfo.cs` comments (lines 44-71) with release notes
- [ ] Update `AboutWindow.xaml` line 3 comment with new version/date
- [ ] Update `AboutWindow.xaml` line 199 (displayed version)
- [ ] Update `AboutWindow.xaml` line 204 (build date)

### **2. Documentation** ✅
- [ ] Update `README_COMPREHENSIVE.md` version badge and last updated date
- [ ] Add new version to Version History section in README_COMPREHENSIVE.md
- [ ] Update `README.md` version in header
- [ ] Update `FAQ.md` last updated date
- [ ] Update version-specific FAQ answers

### **3. Features & Metrics** ✅
- [ ] Update `FEATURES.md` total feature count if changed
- [ ] Add new features to FEATURES.md
- [ ] Update `OPTIMIZATIONS.md` benchmarks if performance improved
- [ ] Update DATABASE_TESTING.md if tests changed

### **4. Commit Message** ✅
- [ ] Include all changed files
- [ ] Reference auto-update tags in commit message
- [ ] Tag commit with version (e.g., `git tag v1.1.0`)

---

## 🤖 Instructions for Future Claude Instances

**When a user says "Update to version X.X":**

1. **Search for auto-update tags:**
   ```
   Use Grep tool with pattern: #AUTO_UPDATE
   ```

2. **Read this file (AUTO_UPDATE_GUIDE.md)** to see what needs updating

3. **Follow the checklist above** systematically

4. **Update all tagged files** with new version/date/features

5. **Commit with message referencing tags:**
   ```
   "Update to version X.X (#AUTO_UPDATE_VERSION #AUTO_UPDATE_README)"
   ```

6. **Create git tag:**
   ```bash
   git tag -a vX.X.X -m "Version X.X.X - [Release Name]"
   git push origin vX.X.X
   ```

---

## 📊 Current Auto-Update Tag Statistics

| Category | Tags | Files | Total Locations |
|----------|------|-------|-----------------|
| **Version/Release** | 4 | 3 | 6 |
| **Documentation** | 5 | 4 | 6 |
| **Features/Metrics** | 4 | 3 | 4 |
| **UI/Theme** | 3 | 2 | 3 |
| **Total** | **16 unique tags** | **10 files** | **19 locations** |

---

## 🔄 Tag Naming Convention

All auto-update tags follow this pattern:
```
#AUTO_UPDATE_<CATEGORY>
```

Where `<CATEGORY>` is:
- **VERSION** - Version numbers and dates
- **README** - Main documentation
- **FAQ** - Frequently asked questions
- **FEATURES** - Feature list and count
- **BENCHMARKS** - Performance metrics

**Supporting tags:**
- `#VERSION_SYSTEM` - Overall version tracking
- `#FEATURE_COUNT` - Total feature count
- `#PERFORMANCE_METRICS` - Performance data
- `#COMPREHENSIVE_DOCS` - Master documentation

---

## 🎯 Quick Reference

**Need to update version?** → Search `#AUTO_UPDATE_VERSION`

**Need to add features?** → Search `#AUTO_UPDATE_FEATURES`

**Need to update docs?** → Search `#AUTO_UPDATE_README`

**Need to add FAQ?** → Search `#AUTO_UPDATE_FAQ`

**Need to update benchmarks?** → Search `#AUTO_UPDATE_BENCHMARKS`

**Need to see everything?** → Read this file (AUTO_UPDATE_GUIDE.md)

---

## 📞 Maintenance Notes

- **Created:** February 14, 2026
- **Last Updated:** February 14, 2026
- **Version:** 1.0 (1.2602.0.0)
- **Maintainer:** Claude Sonnet 4.5 (future instances)
- **Purpose:** Ensure consistent updates across all version-dependent files

---

<div align="center">

**This file itself should be updated when new auto-update tags are added!**

**Built with Claude Code** 🤖

</div>
