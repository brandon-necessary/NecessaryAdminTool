# PowerShell Scripts - Security & Robustness Audit
<!-- TAG: #VERSION_1_0 #SCRIPTS #SECURITY_AUDIT #POWERSHELL -->
**Date:** February 14, 2026
**Version:** 1.0 (1.2602.0.0)
**Status:** ⚠️ **GOOD WITH RECOMMENDED IMPROVEMENTS**

---

## 🎯 Executive Summary

**Overall Assessment:** The PowerShell scripts are **well-designed with good error handling**, but there are **several improvements needed** for true "bulletproof" status.

**Scores:**
- GeneralUpdate.ps1: **7.5/10** (Good, needs improvements)
- FeatureUpdate.ps1: **7/10** (Good, needs improvements)

**Critical Issues:** ❌ 0
**High Priority:** ⚠️ 5
**Medium Priority:** ⚠️ 8
**Low Priority:** ℹ️ 6

---

## 📋 Script 1: GeneralUpdate.ps1

### ✅ **Strengths**

#### **1. Version Checking (Lines 6-13)**
✅ **Excellent** - Pre-flight guard prevents execution on old PowerShell
```powershell
if ($PSVersionTable.PSVersion.Major -lt 5) {
    # Logs failure and exits
}
```
**Why Good:** Prevents cryptic errors on incompatible systems

#### **2. Configurable Paths (Lines 17-23)**
✅ **Good** - Environment variable override support
```powershell
$LogDir = if ($env:NECESSARYADMINTOOL_LOG_DIR) {
    $env:NECESSARYADMINTOOL_LOG_DIR
} else {
    "\\Jzppdm\sys\PUBLIC\BNIT\01_Software\04_Update Logs"
}
```
**Why Good:** Flexible deployment, ManageEngine compatible

#### **3. Logging with Locking (Lines 32-48)**
✅ **Excellent** - File locking prevents log corruption
```powershell
while ((Test-Path $LockFile) -and ($TimeOut -lt 50)) {
    Start-Sleep -Milliseconds 200
    $TimeOut++
}
```
**Why Good:** Thread-safe logging in concurrent environments

#### **4. Power Safety (Lines 69-74, 93)**
✅ **Excellent** - Battery check prevents updates on low power
```powershell
function Check-Power {
    # Checks AC power or battery >20%
}
while (-not (Check-Power)) { /* Wait loop */ }
```
**Why Good:** Prevents interrupted updates causing corruption

#### **5. Uptime Guard (Lines 76-89)**
✅ **Good** - Forces reboots after 30 days with grace period
**Why Good:** Prevents Windows performance degradation

#### **6. System Restore Point (Line 95)**
✅ **Excellent** - Creates restore point before updates
```powershell
Checkpoint-Computer -Description "NecessaryAdminTool_General_Update"
```
**Why Good:** Rollback capability if updates fail

#### **7. Exit Code Reporting (Lines 121-125)**
✅ **Good** - ManageEngine compatible exit codes

---

### ⚠️ **Issues & Improvements**

#### **HIGH PRIORITY**

##### **H1. Missing Module Check (Line 96)**
⚠️ **Problem:** Script assumes PSWindowsUpdate module exists
```powershell
Import-Module PSWindowsUpdate -Force
```
**Risk:** Script fails silently if module not installed
**Fix:**
```powershell
if (!(Get-Module -ListAvailable PSWindowsUpdate)) {
    Write-NecessaryAdminToolLog -Status "ERROR_MODULE_MISSING" -ToMaster $true
    Write-Error "PSWindowsUpdate module not installed. Install with: Install-Module PSWindowsUpdate"
    exit 1
}
Import-Module PSWindowsUpdate -Force -ErrorAction Stop
```

##### **H2. Dangerous Temp Cleanup (Line 26)**
⚠️ **Problem:** Deletes ALL files in C:\Windows\Temp
```powershell
Remove-Item "C:\Windows\Temp\*" -Recurse -Force -ErrorAction SilentlyContinue
```
**Risk:** Could delete files needed by running processes
**Fix:**
```powershell
# Only clean NecessaryAdminTool temp files
Remove-Item "C:\Windows\Temp\NecessaryAdminTool*" -Recurse -Force -ErrorAction SilentlyContinue
```

##### **H3. No Admin Privilege Check**
⚠️ **Problem:** Script modifies HKLM registry without checking admin rights
**Risk:** Silent failure or partial execution
**Fix:** Add to top of script:
```powershell
$CurrentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
$IsAdmin = (New-Object Security.Principal.WindowsPrincipal $CurrentUser).IsInRole([Security.Principal.WindowsBuiltinRole]::Administrator)
if (!$IsAdmin) {
    Write-Error "This script requires administrator privileges"
    exit 1
}
```

##### **H4. Network Path Availability**
⚠️ **Problem:** No check if network log paths are accessible
**Risk:** Silent logging failures
**Fix:**
```powershell
# Test network path before using
if (!(Test-Path $LogDir -ErrorAction SilentlyContinue)) {
    # Fallback to local logging
    $LogDir = "$env:TEMP\NecessaryAdminTool_Logs"
    if (!(Test-Path $LogDir)) {
        New-Item -ItemType Directory -Path $LogDir -Force | Out-Null
    }
}
```

##### **H5. Exit Code Logic Flaw (Lines 121-125)**
⚠️ **Problem:** Checks $LASTEXITCODE from wrong command
```powershell
if ($LASTEXITCODE -eq 0 -or $null -eq $LASTEXITCODE) {
    exit 0
}
```
**Risk:** $LASTEXITCODE is from Get-WindowsUpdate, not overall script status
**Fix:**
```powershell
# Track success/failure explicitly
$ScriptSuccess = $true
try {
    Get-WindowsUpdate -MicrosoftUpdate -AcceptAll -Install -IgnoreReboot -Verbose
} catch {
    $ScriptSuccess = $false
}
exit ($ScriptSuccess ? 0 : 1)
```

---

#### **MEDIUM PRIORITY**

##### **M1. Hardcoded Network Path**
⚠️ **Issue:** Default network path is environment-specific
```powershell
"\\Jzppdm\sys\PUBLIC\BNIT\01_Software\04_Update Logs"
```
**Recommendation:** Document requirement to set environment variable

##### **M2. No Logging Directory Creation**
⚠️ **Issue:** Script assumes log directories exist
**Fix:**
```powershell
if (!(Test-Path $PCLogDir)) {
    New-Item -ItemType Directory -Path $PCLogDir -Force | Out-Null
}
```

##### **M3. Restore Point Failure Not Checked**
⚠️ **Issue:** Restore point creation might fail silently
**Fix:**
```powershell
try {
    Checkpoint-Computer -Description "NecessaryAdminTool_General_Update" -RestorePointType "MODIFY_SETTINGS" -ErrorAction Stop
    Write-NecessaryAdminToolLog -Status "RESTORE_POINT_CREATED" -ToMaster $true
} catch {
    Write-NecessaryAdminToolLog -Status "RESTORE_POINT_FAILED" -ToMaster $true
    # Continue anyway, but logged
}
```

##### **M4. No Disk Space Check**
⚠️ **Issue:** Updates might fail if disk full
**Fix:**
```powershell
$FreeGB = [math]::Round((Get-PSDrive C).Free / 1GB, 2)
if ($FreeGB -lt 10) {
    Write-NecessaryAdminToolLog -Status "ERROR_DISK_SPACE_LOW_$($FreeGB)GB" -ToMaster $true
    Show-NecessaryAdminToolLogo -Msg "Low disk space: ${FreeGB}GB free" "Red"
    exit 1
}
```

##### **M5. Shutdown Command Without Confirmation**
⚠️ **Issue:** Forced shutdown might lose user work (Line 83)
**Current:** 60 second warning
**Better:** Longer warning with countdown display

---

#### **LOW PRIORITY**

##### **L1. Magic Numbers**
ℹ️ **Issue:** Hardcoded values throughout
- Line 28: `-30` days for log retention
- Line 40: `50` timeout iterations
- Line 77: `30` days uptime limit
- Line 83: `60` seconds shutdown timer
**Recommendation:** Use named constants at top of script

##### **L2. PSWindowsUpdate Error Handling**
ℹ️ **Issue:** Get-WindowsUpdate failures could be more detailed
**Enhancement:** Parse and log specific error types

##### **L3. No Update Size Warning**
ℹ️ **Enhancement:** Warn if updates are very large (>2GB)

---

## 📋 Script 2: FeatureUpdate.ps1

### ✅ **Strengths**

#### **1. Hardware Compatibility Checks (Lines 53-63)**
✅ **Excellent** - Verifies TPM, Secure Boot, disk space
```powershell
$TPM = (Get-Tpm).TpmPresent
$SecureBoot = Confirm-SecureBootUEFI
$FreeGB = [math]::round(...FreeSpace / 1GB, 2)
if (!$TPM -or !$SecureBoot -or $FreeGB -lt 20) { exit 1 }
```
**Why Good:** Prevents upgrade on incompatible hardware

#### **2. Fallback Strategy (Lines 65-77, 79-106)**
✅ **Excellent** - ISO→Cloud fallback logic
**Why Good:** Graceful degradation if ISO unavailable

#### **3. ISO Mount/Unmount (Lines 82-90)**
✅ **Good** - Proper resource cleanup
```powershell
$Mount = Mount-DiskImage -ImagePath $ISOPath -PassThru -ErrorAction Stop
# ... use ...
Dismount-DiskImage -ImagePath $ISOPath
```

#### **4. Exit Code Propagation (Lines 94-98)**
✅ **Good** - Passes setup.exe exit code to ManageEngine

---

### ⚠️ **Issues & Improvements**

#### **HIGH PRIORITY**

##### **H1. No Admin Privilege Check**
⚠️ **Problem:** Feature updates require admin, no check
**Fix:** Add admin check (same as GeneralUpdate.ps1)

##### **H2. Hardcoded Hostname Pattern (Line 79)**
⚠️ **Problem:** `if ($Comp -like "TN*")` is hardcoded
**Risk:** Must edit script for different naming conventions
**Fix:**
```powershell
$HostnamePattern = if ($env:NECESSARYADMINTOOL_HOSTNAME_PATTERN) {
    $env:NECESSARYADMINTOOL_HOSTNAME_PATTERN
} else {
    "TN*"
}
if ($Comp -like $HostnamePattern -and (Test-Path $ISOPath)) { ... }
```
**Note:** This should read from app settings if possible

##### **H3. ISO Dismount Not in Finally Block**
⚠️ **Problem:** ISO stays mounted if script crashes
```powershell
try {
    $Mount = Mount-DiskImage ...
    # ... process ...
    Dismount-DiskImage ...  # This might not run if error
} catch { ... }
```
**Fix:**
```powershell
$Mount = $null
try {
    $Mount = Mount-DiskImage -ImagePath $ISOPath -PassThru -ErrorAction Stop
    # ... process ...
} catch {
    Run-CloudUpdate
} finally {
    if ($Mount) {
        Dismount-DiskImage -ImagePath $ISOPath -ErrorAction SilentlyContinue
    }
}
```

##### **H4. No Logging Directory Creation**
⚠️ **Same as GeneralUpdate.ps1** - Assumes log paths exist

##### **H5. Setup.exe Process Timeout**
⚠️ **Problem:** No timeout on setup.exe (could hang forever)
**Fix:**
```powershell
$Timeout = 7200 # 2 hours
$Proc = Start-Process ... -PassThru
$Proc | Wait-Process -Timeout $Timeout -ErrorAction SilentlyContinue
if (!$Proc.HasExited) {
    $Proc.Kill()
    Write-NecessaryAdminToolLog -Status "TIMEOUT_SETUP_KILLED" -ToMaster $true
    exit 1
}
```

---

#### **MEDIUM PRIORITY**

##### **M1. TPM Check Error Handling**
⚠️ **Issue:** `Get-Tpm` might fail on older systems
**Fix:**
```powershell
$TPM = try { (Get-Tpm).TpmPresent } catch { $false }
```

##### **M2. Confirm-SecureBootUEFI on Legacy BIOS**
⚠️ **Issue:** Throws error on legacy BIOS systems
**Current:** Uses -ErrorAction SilentlyContinue (good)
**Better:** Explicit null check

##### **M3. No Network Check for ISO Path**
⚠️ **Issue:** Doesn't verify ISO is accessible before mounting
**Fix:**
```powershell
if ($Comp -like "TN*" -and (Test-Path $ISOPath) -and (Test-Path (Split-Path $ISOPath))) {
    # Verify ISO size > 0
    $ISOSize = (Get-Item $ISOPath).Length
    if ($ISOSize -lt 1GB) {
        Write-NecessaryAdminToolLog -Status "ERROR_ISO_TOO_SMALL" -ToMaster $true
        Run-CloudUpdate
        return
    }
    # Continue with mount...
}
```

##### **M4. PSWindowsUpdate Auto-Install**
⚠️ **Issue:** Auto-installs module without verification (Line 68)
```powershell
Install-Module PSWindowsUpdate -Force -Confirm:$false
```
**Better:** Check if installation succeeded before using

---

#### **LOW PRIORITY**

##### **L1. Magic Numbers**
ℹ️ **Same as GeneralUpdate.ps1**
- Line 56: `20GB` minimum disk space
- Line 22: `50` timeout iterations

##### **L2. Setup.exe Arguments Not Documented**
ℹ️ **Enhancement:** Add comments explaining setup.exe flags

##### **L3. No Progress Indication**
ℹ️ **Enhancement:** Show progress during long-running setup.exe

---

## 🔒 Security Analysis

### **Security Issues Found:**

#### **1. Command Injection: NONE** ✅
- No user input concatenated into commands
- All paths use environment variables (safe)
- No `Invoke-Expression` or similar

#### **2. Privilege Escalation: LOW RISK** ⚠️
- Scripts assume admin privileges but don't check
- Should validate elevation before modifying system

#### **3. Network Path Injection: NONE** ✅
- Paths controlled by environment variables (admin-set)

#### **4. Resource Leaks: MEDIUM RISK** ⚠️
- ISO might stay mounted if script crashes (FeatureUpdate)
- Lock files might persist if process killed

#### **5. Denial of Service: LOW RISK** ⚠️
- Forced reboots could interrupt critical work
- 60-second warning is reasonable

---

## 🛡️ Recommended Improvements

### **Priority 1: Critical Safety**
1. ✅ **Add admin privilege checks** to both scripts
2. ✅ **Fix ISO dismount** to use finally block
3. ✅ **Add module existence check** before importing
4. ✅ **Fix exit code logic** in GeneralUpdate.ps1

### **Priority 2: Robustness**
5. ✅ **Add disk space checks** before updates
6. ✅ **Create log directories** if missing
7. ✅ **Add network path fallback** to local logging
8. ✅ **Add timeout** to setup.exe process

### **Priority 3: Configurability**
9. ✅ **Make hostname pattern** configurable via environment variable
10. ✅ **Remove dangerous temp cleanup** or make it safer
11. ✅ **Add restore point verification**

### **Priority 4: User Experience**
12. ✅ **Better error messages** with troubleshooting hints
13. ✅ **Progress indication** for long operations
14. ✅ **Longer shutdown warning** with countdown

---

## 📝 Improved Script Snippets

### **Admin Check (Add to both scripts)**
```powershell
# --- ADMIN PRIVILEGE CHECK ---
$CurrentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
$IsAdmin = (New-Object Security.Principal.WindowsPrincipal $CurrentUser).IsInRole([Security.Principal.WindowsBuiltinRole]::Administrator)

if (!$IsAdmin) {
    Write-Error "ERROR: This script requires administrator privileges"
    Write-Error "Right-click PowerShell and select 'Run as Administrator'"
    exit 1
}
```

### **Module Check (GeneralUpdate.ps1)**
```powershell
# --- MODULE VERIFICATION ---
if (!(Get-Module -ListAvailable -Name PSWindowsUpdate)) {
    Write-NecessaryAdminToolLog -Status "ERROR_MODULE_NOT_INSTALLED" -ToMaster $true
    Write-Error "PSWindowsUpdate module is not installed"
    Write-Host "Install it with: Install-Module PSWindowsUpdate -Force" -ForegroundColor Yellow
    exit 1
}

try {
    Import-Module PSWindowsUpdate -Force -ErrorAction Stop
} catch {
    Write-NecessaryAdminToolLog -Status "ERROR_MODULE_IMPORT_FAILED" -ToMaster $true
    Write-Error "Failed to import PSWindowsUpdate: $($_.Exception.Message)"
    exit 1
}
```

### **Safe Temp Cleanup (GeneralUpdate.ps1)**
```powershell
# --- MAINTENANCE (Safe cleanup) ---
# Only clean our own temp files, not system-wide
Remove-Item "C:\Windows\Temp\NecessaryAdminTool*" -Recurse -Force -ErrorAction SilentlyContinue

# Archive old logs safely
if (Test-Path $PCLogDir) {
    if (!(Test-Path $PCArchive)) {
        New-Item -ItemType Directory -Path $PCArchive -Force | Out-Null
    }
    Get-ChildItem -Path $PCLogDir -Filter "*.txt" |
        Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-30) } |
        Move-Item -Destination $PCArchive -Force -ErrorAction SilentlyContinue
}
```

### **ISO Dismount Fix (FeatureUpdate.ps1)**
```powershell
$MountedISO = $null
try {
    Show-NecessaryAdminToolLogo -Msg "Mounting Local ISO..." "Cyan"
    $MountedISO = Mount-DiskImage -ImagePath $ISOPath -PassThru -ErrorAction Stop
    $Drive = ($MountedISO | Get-Volume).DriveLetter

    Write-NecessaryAdminToolLog -Status "ISO_RUNNING" -ToMaster $true
    Show-NecessaryAdminToolLogo -Msg "Upgrading... (Do not turn off)" "Yellow"

    $Proc = Start-Process "$($Drive):\setup.exe" -ArgumentList "/auto upgrade /quiet /showoobe none /eula accept /dynamicupdate disable" -Wait -PassThru

    Write-NecessaryAdminToolLog -Status "ISO_COMPLETE_CODE_$($Proc.ExitCode)" -ToMaster $true

    if ($Proc.ExitCode -eq 0) {
        exit 0
    } else {
        exit 1
    }
}
catch {
    Write-NecessaryAdminToolLog -Status "ISO_FAILED_FALLBACK_CLOUD" -ToMaster $true
    Run-CloudUpdate
}
finally {
    # Always dismount, even if script crashes
    if ($MountedISO) {
        try {
            Dismount-DiskImage -ImagePath $ISOPath -ErrorAction SilentlyContinue
            Write-NecessaryAdminToolLog -Status "ISO_DISMOUNTED" -ToMaster $false
        } catch {
            # Log but don't fail
            Write-NecessaryAdminToolLog -Status "ISO_DISMOUNT_FAILED" -ToMaster $false
        }
    }
}
```

---

## ✅ Final Verdict

### **Current Status:**
- ⚠️ **GeneralUpdate.ps1:** 7.5/10 - **GOOD** but not bulletproof
- ⚠️ **FeatureUpdate.ps1:** 7/10 - **GOOD** but needs improvements

### **To Achieve "Bulletproof" (9/10+):**
1. ✅ Fix all HIGH PRIORITY issues (5 items)
2. ✅ Fix MEDIUM PRIORITY issues (8 items)
3. ✅ Consider LOW PRIORITY enhancements

### **Strengths:**
✅ Good error logging
✅ ManageEngine integration
✅ Power safety checks
✅ Hardware compatibility checks
✅ Fallback strategies
✅ System restore points

### **Weaknesses:**
⚠️ No admin privilege verification
⚠️ Missing module checks
⚠️ Resource leak risk (ISO mount)
⚠️ Hardcoded configurations
⚠️ Limited error recovery

---

## 📋 Implementation Checklist

To make scripts truly bulletproof:

- [ ] Add admin privilege check to both scripts
- [ ] Add PSWindowsUpdate module verification (GeneralUpdate.ps1)
- [ ] Fix ISO dismount to use finally block (FeatureUpdate.ps1)
- [ ] Create log directories if missing
- [ ] Add disk space checks
- [ ] Make hostname pattern configurable
- [ ] Add network path fallback to local logging
- [ ] Fix exit code logic (GeneralUpdate.ps1)
- [ ] Remove dangerous temp cleanup
- [ ] Add setup.exe timeout
- [ ] Add restore point verification
- [ ] Improve error messages

---

**Audit Completed:** February 14, 2026
**Reviewed By:** Claude Sonnet 4.5
**Result:** ⚠️ **GOOD SCRIPTS - IMPROVEMENTS RECOMMENDED**

**Built with Claude Code** 🤖
