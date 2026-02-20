#Requires -Version 5.1
#Requires -RunAsAdministrator
# ==============================================================================
# NECESSARYADMINTOOL IT - FEATURE UPDATE SUITE (v1.0 - Bulletproof Edition)
# Includes: Windows Major OS Updates, HW Guard, ISO/Cloud Logic, ManageEngine Compatible
# Security Hardened: Admin checks, configurable patterns, resource cleanup, timeouts
# ==============================================================================

# EARLY HEARTBEAT - first executable line; if ME execution log is blank, script never loaded
# (most common cause: #Requires -RunAsAdministrator failing in non-elevated agent context)
Write-Host "  [$(Get-Date -Format 'HH:mm:ss')] FeatureUpdate.ps1 - Script loaded on $env:COMPUTERNAME, starting execution..." -ForegroundColor Cyan

# Enterprise standard: fail loudly on unexpected errors; use -ErrorAction SilentlyContinue where fallback is intentional
$ErrorActionPreference = 'Stop'

# --- 1. CONFIGURABLE PATHS (Environment Variable Based) ---
# ALL paths are configured via environment variables or app settings
# NO hardcoded paths - configured through NecessaryAdminTool Options menu

$ISOPath             = $env:NECESSARYADMINTOOL_ISO_PATH
$LogDir              = $env:NECESSARYADMINTOOL_LOG_DIR
$HostnamePattern     = if ($env:NECESSARYADMINTOOL_HOSTNAME_PATTERN) { $env:NECESSARYADMINTOOL_HOSTNAME_PATTERN } else { "*" }
$DatabaseType        = ""  # NAT_INJECT_DB_TYPE
$SqlConnectionString = ""  # NAT_INJECT_SQL_CONN

# Log directory validation with automatic local fallback
if ([string]::IsNullOrEmpty($LogDir) -or !(Test-Path $LogDir -ErrorAction SilentlyContinue)) {
    $LogDir = "$env:TEMP\NecessaryAdminTool_Logs"
    if (!(Test-Path $LogDir)) {
        New-Item -ItemType Directory -Path $LogDir -Force | Out-Null
    }
    Write-Host "INFO: Using local fallback logging at $LogDir" -ForegroundColor Cyan
}

$Timestamp  = Get-Date -Format 'yyyy-MM-dd_HH-mm'
$PCLogDir   = "$LogDir\Individual_PC_Logs"
$PCLog      = "$PCLogDir\$($env:COMPUTERNAME)_Feature_$Timestamp.txt"
$MasterLog  = "$LogDir\Master_Update_Log.csv"
$Comp       = $env:COMPUTERNAME

# Constants
$MIN_DISK_SPACE_GB = 20
$SETUP_TIMEOUT_SECONDS = 7200  # 2 hours
$MIN_ISO_SIZE_GB = 1

# Create log directories if missing
if (!(Test-Path $PCLogDir)) {
    New-Item -ItemType Directory -Path $PCLogDir -Force | Out-Null
}

# Start transcript - captures ALL console output automatically (belt-and-suspenders alongside custom logging)
# Must specify explicit path: under SYSTEM, $HOME resolves to C:\Windows\System32\config\systemprofile
$TranscriptPath = "$PCLogDir\$($env:COMPUTERNAME)_Feature_${Timestamp}_Transcript.txt"
Start-Transcript -Path $TranscriptPath -Append -NoClobber -ErrorAction SilentlyContinue

# Capture baseline info early so it's available at all exit points
$ScriptStart = Get-Date
$OSInfo      = Get-CimInstance Win32_OperatingSystem
$OSVersion   = $OSInfo.Caption
$UptimeDays  = [math]::Round(((Get-Date) - $OSInfo.LastBootUpTime).TotalDays, 2)
$FreeGB      = 0   # initialized early so Write-MasterSummary is safe on pre-section-4 exits

# Collect machine context for the start banner (single Win32_ComputerSystem query reused below)
$PSVersion   = "$($PSVersionTable.PSVersion.Major).$($PSVersionTable.PSVersion.Minor)"
$RunningAs   = [Security.Principal.WindowsIdentity]::GetCurrent().Name
$SysInfo     = Get-CimInstance Win32_ComputerSystem -ErrorAction SilentlyContinue
$DomainName  = if ($SysInfo) { $SysInfo.Domain } else { "Unknown" }
$TotalRAMGB  = if ($SysInfo) { [math]::Round($SysInfo.TotalPhysicalMemory / 1GB, 2) } else { 0 }

# Additional system info for fleet master log (single BIOS + network query at startup)
$SerialNumber = try { (Get-CimInstance Win32_BIOS -ErrorAction SilentlyContinue).SerialNumber.Trim() } catch { "Unknown" }
$Manufacturer = if ($SysInfo) { $SysInfo.Manufacturer.Trim() } else { "Unknown" }
$Model        = if ($SysInfo) { $SysInfo.Model.Trim() } else { "Unknown" }
$LoggedInUser = if ($SysInfo -and $SysInfo.UserName) { $SysInfo.UserName } else { "None" }
$IPAddress    = try {
    $IP = $null
    foreach ($Adapter in @(Get-CimInstance Win32_NetworkAdapterConfiguration -Filter "IPEnabled=True" -ErrorAction SilentlyContinue)) {
        $ValidIP = @($Adapter.IPAddress) | Where-Object { $_ -match '^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$' -and $_ -notmatch '^(127\.|169\.254\.)' }
        if ($ValidIP) { $IP = $ValidIP[0]; break }
    }
    if ($IP) { $IP } else { "Unknown" }
} catch { "Unknown" }
# Pre-init compat vars as "Unchecked" - overwritten in Section 4 (safe for pre-Section-4 CSV writes)
$CurrentBuild     = try { [int]$OSInfo.BuildNumber } catch { 0 }
$TPMStatus        = "Unchecked"
$SecureBootStatus = "Unchecked"

# --- 2. LOGGING (Thread-Safe with File Locking) ---
function Write-NecessaryAdminToolLog {
    param([string]$Status, [bool]$ToMaster = $false)
    $Stamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"

    try {
        "[$Stamp] $Status" | Out-File $PCLog -Append -Encoding UTF8 -ErrorAction Stop
    } catch {
        Write-Host "[$Stamp] $Status" -ForegroundColor Yellow
    }

    if ($ToMaster) {
        # Named system mutex - OS auto-releases on process crash; no stale lock risk
        $Mtx = $null; $Acquired = $false
        try {
            $Mtx = [System.Threading.Mutex]::new($false, "Global\NecessaryAdminTool_MasterLog")
            $Acquired = $Mtx.WaitOne(10000)   # wait up to 10s
        } catch [System.Threading.AbandonedMutexException] {
            $Acquired = $true   # previous process died holding it - we now own it
        } catch {}
        try {
            "$Comp,$Status,$Stamp" | Add-Content $MasterLog -Force -ErrorAction Stop
        } catch {
            "[$Stamp] ERROR: Master Log Write Failed - $($_.Exception.Message)" | Out-File $PCLog -Append -ErrorAction SilentlyContinue
        } finally {
            if ($Acquired -and $Mtx) { try { $Mtx.ReleaseMutex() } catch {} }
            if ($Mtx) { $Mtx.Dispose() }
        }
    }
}

# --- 2b. MASTER CSV SUMMARY (Rich 20-column fleet reporting schema) ---
function Write-MasterSummary {
    param(
        [string]$Status,
        [string]$Method      = "N/A",
        [string]$UpdateCount = "N/A",
        [string]$Details     = ""
    )
    $Stamp    = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $Duration = [math]::Round(((Get-Date) - $ScriptStart).TotalSeconds, 0)
    $Header   = "Hostname,Script,Timestamp,OSVersion,BuildNumber,UptimeDays,TotalRAMGB,DiskFreeGB,SerialNumber,Manufacturer,Model,IPAddress,LoggedInUser,TPMPresent,SecureBoot,Status,Method,UpdateCount,Details,DurationSeconds"
    $Row      = "`"$Comp`",`"Feature`",`"$Stamp`",`"$OSVersion`",`"$CurrentBuild`",`"$UptimeDays`",`"$TotalRAMGB`",`"$FreeGB`",`"$SerialNumber`",`"$Manufacturer`",`"$Model`",`"$IPAddress`",`"$LoggedInUser`",`"$TPMStatus`",`"$SecureBootStatus`",`"$Status`",`"$Method`",`"$UpdateCount`",`"$Details`",`"$Duration`""

    # Named system mutex - OS auto-releases on process crash; no stale lock risk
    $Mtx = $null; $Acquired = $false
    try {
        $Mtx = [System.Threading.Mutex]::new($false, "Global\NecessaryAdminTool_MasterLog")
        $Acquired = $Mtx.WaitOne(10000)
    } catch [System.Threading.AbandonedMutexException] {
        $Acquired = $true
    } catch {}
    try {
        if (!(Test-Path $MasterLog)) { $Header | Out-File $MasterLog -Encoding UTF8 -ErrorAction SilentlyContinue }
        $Row | Add-Content $MasterLog -Force -ErrorAction Stop
    } catch {
        "[$Stamp] ERROR: Master Summary Write Failed - $($_.Exception.Message)" | Out-File $PCLog -Append -ErrorAction SilentlyContinue
    } finally {
        if ($Acquired -and $Mtx) { try { $Mtx.ReleaseMutex() } catch {} }
        if ($Mtx) { $Mtx.Dispose() }
    }

    # Write duration footer to individual PC log (reuse $Duration already computed above)
    $DurMin   = [math]::Round($Duration / 60, 1)
    $Footer   = "================ SCRIPT END: Status=$Status | Method=$Method | Duration=${DurMin}min | $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') ================"
    try { $Footer | Out-File $PCLog -Append -Encoding UTF8 -ErrorAction SilentlyContinue } catch {}

    # Write to SQL Server if configured
    Write-ToDatabase -Status $Status -Method $Method -Details $Details
}

# --- 2c. DATABASE WRITE (SQL Server direct write when configured) ---
function Write-ToDatabase {
    param(
        [string]$Status,
        [string]$Method  = "",
        [string]$Details = ""
    )
    # Silently skip unless SQL Server is configured in NecessaryAdminTool
    if ([string]::IsNullOrEmpty($DatabaseType) -or
        $DatabaseType -ne "SqlServer" -or
        [string]::IsNullOrEmpty($SqlConnectionString)) {
        return
    }

    $Duration = [math]::Round(((Get-Date) - $ScriptStart).TotalSeconds, 0)
    $Conn = $null
    try {
        $Conn = New-Object System.Data.SqlClient.SqlConnection($SqlConnectionString)
        $Conn.Open()

        # Create table if it does not already exist
        $CreateCmd = $Conn.CreateCommand()
        $CreateCmd.CommandText = @"
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'UpdateHistory')
CREATE TABLE UpdateHistory (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    Hostname        NVARCHAR(255)   NOT NULL,
    Script          NVARCHAR(50)    NOT NULL,
    Timestamp       DATETIME        NOT NULL,
    OSVersion       NVARCHAR(500)   NULL,
    UptimeDays      DECIMAL(10,2)   NULL,
    DiskFreeGB      DECIMAL(10,2)   NULL,
    Status          NVARCHAR(100)   NOT NULL,
    UpdatesFound    NVARCHAR(500)   NULL,
    Method          NVARCHAR(200)   NULL,
    Details         NVARCHAR(MAX)   NULL,
    DurationSeconds INT             NULL
)
"@
        $CreateCmd.ExecuteNonQuery() | Out-Null

        # Parameterized INSERT - no SQL injection risk
        $InsertCmd = $Conn.CreateCommand()
        $InsertCmd.CommandText = @"
INSERT INTO UpdateHistory
    (Hostname, Script, Timestamp, OSVersion, UptimeDays, DiskFreeGB,
     Status, UpdatesFound, Method, Details, DurationSeconds)
VALUES
    (@Hostname, @Script, @Timestamp, @OSVersion, @UptimeDays, @DiskFreeGB,
     @Status, NULL, @Method, @Details, @Duration)
"@
        $InsertCmd.Parameters.AddWithValue("@Hostname",   $Comp)           | Out-Null
        $InsertCmd.Parameters.AddWithValue("@Script",     "Feature")       | Out-Null
        $InsertCmd.Parameters.AddWithValue("@Timestamp",  [DateTime]::Now) | Out-Null
        $InsertCmd.Parameters.AddWithValue("@OSVersion",  $OSVersion)      | Out-Null
        $InsertCmd.Parameters.AddWithValue("@UptimeDays", $UptimeDays)     | Out-Null
        $InsertCmd.Parameters.AddWithValue("@DiskFreeGB", $FreeGB)         | Out-Null
        $InsertCmd.Parameters.AddWithValue("@Status",     $Status)         | Out-Null
        $InsertCmd.Parameters.AddWithValue("@Method",     $Method)         | Out-Null
        $InsertCmd.Parameters.AddWithValue("@Details",    $Details)        | Out-Null
        $InsertCmd.Parameters.AddWithValue("@Duration",   $Duration)       | Out-Null
        $InsertCmd.ExecuteNonQuery() | Out-Null

        Write-NecessaryAdminToolLog -Status "DB_WRITE_SUCCESS_Feature_$Status" -ToMaster $false
    } catch {
        Write-NecessaryAdminToolLog -Status "DB_WRITE_FAILED_$($_.Exception.Message)" -ToMaster $false
    } finally {
        if ($null -ne $Conn) { $Conn.Close() }
    }
}

# --- 3. UI LOGO (Theme Engine: Orange #FF8533 + Zinc #A1A1AA) ---
function Show-NecessaryAdminToolLogo {
    param([string]$Msg, [string]$Color = "Cyan")
    # Note: Clear-Host intentionally omitted - clearing console removes ME execution log history
    Write-Host ""
    Write-Host " -----------------------------------------------------------" -ForegroundColor DarkYellow
    Write-Host "  " -NoNewline
    Write-Host "NECESSARYADMINTOOL" -ForegroundColor DarkYellow -NoNewline
    Write-Host " | " -ForegroundColor Gray -NoNewline
    Write-Host "Feature Update Suite v1.0" -ForegroundColor Gray
    Write-Host " -----------------------------------------------------------" -ForegroundColor DarkYellow
    Write-Host ""
    if ($Msg) {
        Write-Host "  STATUS: " -ForegroundColor Gray -NoNewline
        Write-Host $Msg -ForegroundColor $Color
    }
    Write-Host ""
}

# --- 3b. ENTERPRISE HELPER FUNCTIONS ---

# Power/AC check - prevents upgrade on low-battery laptops not plugged in
function Test-PowerOK {
    $Battery = Get-CimInstance -ClassName Win32_Battery -ErrorAction SilentlyContinue
    if ($null -eq $Battery) { return $true }   # No battery = desktop/docked - always OK
    # BatteryStatus: 1=Discharging, 2=OnAC, 3=FullyCharged, 4=Low, 5=Critical, 6-9=Charging variants
    # Check NOT discharging rather than exact equality to 2 - catches all "on AC" states
    $OnAC         = $Battery.BatteryStatus -notin @(1, 4, 5)
    $PctRemaining = $Battery.EstimatedChargeRemaining
    Write-Host "  Battery: $PctRemaining% | AC Power: $OnAC" -ForegroundColor Cyan
    Write-NecessaryAdminToolLog -Status "POWER_Battery_${PctRemaining}pct_AC_${OnAC}" -ToMaster $false
    return ($OnAC -or $PctRemaining -ge 40)
}

# Pending reboot check - upgrading over a pending reboot causes setup failures
function Test-PendingReboot {
    if (Test-Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired" -ErrorAction SilentlyContinue) { return $true }
    if (Test-Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending"  -ErrorAction SilentlyContinue) { return $true }
    $PFR = (Get-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager" `
                -Name PendingFileRenameOperations -ErrorAction SilentlyContinue).PendingFileRenameOperations
    return ($null -ne $PFR -and @($PFR).Count -gt 0)
}

# Interactive user detection - skip dialog/countdown if machine is unattended
function Test-UserLoggedIn {
    $UserSessions = Get-Process -Name explorer -ErrorAction SilentlyContinue |
        Where-Object { $_.SessionId -ne 0 }
    return ($null -ne $UserSessions -and @($UserSessions).Count -gt 0)
}

# Open application detection - finds visible user apps to list in the warning dialog
function Get-OpenApplications {
    $AppPatterns = @(
        'WINWORD','EXCEL','POWERPNT','OUTLOOK','ONENOTE','ACCESS','MSPUB',  # Microsoft Office
        'Teams','msteams','ms-teams','slack','zoom','webexmta',               # Collaboration (ms-teams = Teams 2.0)
        'chrome','msedge','firefox','iexplore','brave',                      # Browsers
        'notepad\+\+','Code','devenv',                                       # Dev tools
        'acrobat','acrord32',                                                # PDF
        'mstsc','vmconnect'                                                  # Remote/VM
    )
    $Running = @()
    foreach ($Pattern in $AppPatterns) {
        $Procs = Get-Process -ErrorAction SilentlyContinue | Where-Object {
            $_.Name -match "^$Pattern$" -and $_.SessionId -ne 0 -and $_.MainWindowTitle -ne ''
        }
        foreach ($P in $Procs) {
            $Name = if ($P.MainWindowTitle) { $P.MainWindowTitle } else { $P.Name }
            $Running += $Name
        }
    }
    return ($Running | Select-Object -Unique | Select-Object -First 8)
}

# Exit code decoder - translates installer HRESULTs to readable descriptions for ME log
function Get-InstallExitDescription {
    param([int]$ExitCode)
    $Known = @{
        0            = "Success - reboot pending"
        3010         = "Success - reboot required (same outcome as 0)"
        -1056931699  = "0xC1900101 - Driver compatibility error (DRIVER_UNLOAD_WITHOUT_CANCEL)"
        -1056931700  = "0xC1900200 - General hardware compatibility failure"
        -1056931702  = "0xC1900202 - Minimum hardware requirements not met"
        -1056931703  = "0xC1900203 - BIOS/UEFI firmware compatibility error"
        -1056931593  = "0xC1900107 - Cleanup pending - reboot machine then retry"
        -1056931120  = "0xC1900208 - Incompatible app is blocking the upgrade"
        -1056931578  = "0xC190020E - Insufficient free space after gathering upgrade files"
        -2146233088  = "0x80070070 - Insufficient disk space"
        -2145107904  = "0x80240020 - Upgrade not offered via Windows Update for this device"
        -1            = "Process did not exit cleanly (killed or crashed)"
    }
    $Hex = "0x{0:X8}" -f [uint32]$ExitCode
    if ($Known.ContainsKey($ExitCode)) { return "$Hex - $($Known[$ExitCode])" }
    return "$Hex - Unknown exit code (check %TEMP%\`$WINDOWS.~BT\Sources\Panther\setupact.log or run SetupDiag)"
}

# User-visible notification dialog (ServiceNotification flag reaches session 0 -> active user desktop)
# Only shown when a user is logged in; silently skipped for unattended machines.
function Show-UserNotification {
    param(
        [string]$Title   = "NecessaryAdminTool IT",
        [string]$Message = "",
        [string]$Icon    = "Information"
    )
    if ([string]::IsNullOrEmpty($Message)) { return }
    if (!(Test-UserLoggedIn)) {
        Write-NecessaryAdminToolLog -Status "USER_NOTIFY_SKIPPED_NO_USER - $Title" -ToMaster $false
        return
    }
    try {
        Add-Type -AssemblyName System.Windows.Forms
        $IconEnum = [System.Windows.Forms.MessageBoxIcon]::$Icon
        Write-NecessaryAdminToolLog -Status "USER_NOTIFY_SHOWING - $Title" -ToMaster $false
        [System.Windows.Forms.MessageBox]::Show(
            $Message, $Title,
            [System.Windows.Forms.MessageBoxButtons]::OK,
            $IconEnum,
            [System.Windows.Forms.MessageBoxDefaultButton]::Button1,
            [System.Windows.Forms.MessageBoxOptions]::ServiceNotification
        ) | Out-Null
        Write-NecessaryAdminToolLog -Status "USER_NOTIFY_DISMISSED - $Title" -ToMaster $false
    } catch {
        Write-NecessaryAdminToolLog -Status "USER_NOTIFY_FAILED - $($_.Exception.Message)" -ToMaster $false
    }
}

# Pre-upgrade disk cleanup - frees WU cache + temp files and runs DISM component cleanup
function Invoke-PreUpgradeDiskCleanup {
    Write-Host "  Attempting pre-upgrade disk cleanup..." -ForegroundColor Cyan
    Write-NecessaryAdminToolLog -Status "DISK_CLEANUP_STARTED" -ToMaster $false

    # Clear Windows Update download cache
    try {
        Stop-Service -Name wuauserv -Force -ErrorAction SilentlyContinue
        Remove-Item "C:\Windows\SoftwareDistribution\Download\*" -Recurse -Force -ErrorAction SilentlyContinue
        Start-Service -Name wuauserv -ErrorAction SilentlyContinue
        Write-NecessaryAdminToolLog -Status "DISK_CLEANUP_WU_CACHE_CLEARED" -ToMaster $false
    } catch {
        Write-NecessaryAdminToolLog -Status "DISK_CLEANUP_WU_WARN_$($_.Exception.Message)" -ToMaster $false
    }

    # Clear system temp folder
    try {
        Remove-Item "$env:SystemRoot\Temp\*" -Recurse -Force -ErrorAction SilentlyContinue
        Write-NecessaryAdminToolLog -Status "DISK_CLEANUP_TEMP_CLEARED" -ToMaster $false
    } catch {
        Write-NecessaryAdminToolLog -Status "DISK_CLEANUP_TEMP_WARN_$($_.Exception.Message)" -ToMaster $false
    }

    # Clear Delivery Optimization cache - often 2-8 GB of temporary download data, safe to remove
    try {
        Delete-DeliveryOptimizationCache -Force -ErrorAction Stop
        Write-NecessaryAdminToolLog -Status "DISK_CLEANUP_DO_CACHE_CLEARED" -ToMaster $false
    } catch {
        Write-NecessaryAdminToolLog -Status "DISK_CLEANUP_DO_CACHE_WARN_$($_.Exception.Message)" -ToMaster $false
    }

    # DISM component cleanup (5-minute cap - does not block indefinitely)
    try {
        $DismProc = Start-Process dism.exe `
            -ArgumentList "/Online /Cleanup-Image /StartComponentCleanup /ResetBase" `
            -PassThru -WindowStyle Hidden -ErrorAction Stop
        $DismProc | Wait-Process -Timeout 300 -ErrorAction SilentlyContinue
        if (!$DismProc.HasExited) { $DismProc.Kill() }
        Write-NecessaryAdminToolLog -Status "DISK_CLEANUP_DISM_COMPLETE" -ToMaster $false
    } catch {
        Write-NecessaryAdminToolLog -Status "DISK_CLEANUP_DISM_WARN_$($_.Exception.Message)" -ToMaster $false
    }

    $NewFreeGB = [math]::Round(((Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='C:'").FreeSpace / 1GB), 2)
    Write-Host "  Disk cleanup done. Free space now: $NewFreeGB GB" -ForegroundColor Cyan
    Write-NecessaryAdminToolLog -Status "DISK_CLEANUP_COMPLETE_FREE_${NewFreeGB}GB" -ToMaster $false
    return $NewFreeGB
}

# --- 3c. SCRIPT START BANNER ---
# Written as the very first log entry so every run has full context at the top of the file.
$StartBanner = @"
================================================================================
  FEATURE UPDATE SUITE - SCRIPT START
  Host      : $Comp
  Domain    : $DomainName
  OS        : $OSVersion
  Uptime    : $UptimeDays days
  RAM       : $TotalRAMGB GB
  RunningAs : $RunningAs
  PS Ver    : $PSVersion
  LogDir    : $PCLog
  ISOPath   : $(if ($ISOPath) { $ISOPath } else { '(not set - cloud path will be used)' })
  LogDirSrc : $(if ($LogDir -eq "$env:TEMP\NecessaryAdminTool_Logs") { 'LOCAL FALLBACK' } else { 'Configured share' })
  Started   : $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
================================================================================
"@
Write-NecessaryAdminToolLog -Status "SCRIPT_START_Host=${Comp}_OS=${OSVersion}_Uptime=${UptimeDays}days_RAM=${TotalRAMGB}GB_RunAs=${RunningAs}_PS=${PSVersion}" -ToMaster $false
# Write the banner block directly to the PC log for human readability
try { $StartBanner | Out-File $PCLog -Append -Encoding UTF8 -ErrorAction SilentlyContinue } catch {}
Write-Host $StartBanner -ForegroundColor DarkYellow

# --- 3e. POWER CHECK ---
Show-NecessaryAdminToolLogo -Msg "Power & Reboot Pre-flight Check..."

if (!(Test-PowerOK)) {
    $Battery = Get-CimInstance -ClassName Win32_Battery -ErrorAction SilentlyContinue
    $Pct     = if ($Battery) { $Battery.EstimatedChargeRemaining } else { "?" }
    $Reason  = "Battery at ${Pct}% and not connected to AC power. Plug in charger and retry."
    Write-Host "  FAILED: $Reason" -ForegroundColor Red
    Write-NecessaryAdminToolLog -Status "FAILED_POWER_CHECK_$Reason" -ToMaster $false
    Write-MasterSummary -Status "FAILED" -Method "None" -Details $Reason
    Show-NecessaryAdminToolLogo -Msg "Power check failed - not safe to upgrade on battery" "Red"
    exit 1
}
Write-Host "  Power check: OK (AC or sufficient battery)" -ForegroundColor Green
Write-NecessaryAdminToolLog -Status "POWER_CHECK_PASSED" -ToMaster $false

# --- 3f. PENDING REBOOT CHECK ---
# Deploy PreflightReboot.ps1 via ManageEngine BEFORE this script to handle pending reboots.
# This script fails fast so ME can track the issue; the preflight handles the reboot cleanly.
if (Test-PendingReboot) {
    $Reason = "Pending reboot detected. Run NecessaryAdminTool_PreflightReboot.ps1 first, then re-push this task."
    Write-Host "  FAILED: $Reason" -ForegroundColor Yellow
    Write-NecessaryAdminToolLog -Status "FAILED_PENDING_REBOOT" -ToMaster $false
    Write-MasterSummary -Status "FAILED" -Method "None" -Details $Reason
    Show-NecessaryAdminToolLogo -Msg "Pending reboot - run Preflight script first, then re-push task" "Yellow"
    exit 1
}
Write-Host "  Pending reboot: None detected - OK to proceed with upgrade." -ForegroundColor Green
Write-Host "  [PREFLIGHT PASS] No pending reboot (WU/CBS/PFR keys all clear)." -ForegroundColor Green
Write-NecessaryAdminToolLog -Status "PENDING_REBOOT_CHECK_PASSED - WU_Key=CLEAR CBS_Key=CLEAR PFR_Key=CLEAR" -ToMaster $false

# --- 4. HARDWARE COMPATIBILITY CHECK ---
Show-NecessaryAdminToolLogo -Msg "Hardware Compatibility Check..."

# Check TPM 2.0 - Win11 requires TPM 2.0 (presence alone is not sufficient)
$TPM       = $false
$TPMVersion = "None"
try {
    $TpmInfo = Get-Tpm -ErrorAction Stop
    if ($TpmInfo.TpmPresent) {
        $TpmWmi    = Get-CimInstance -Namespace "root\cimv2\Security\MicrosoftTpm" `
                        -ClassName Win32_Tpm -ErrorAction SilentlyContinue
        $TPMVersion = if ($TpmWmi) { $TpmWmi.SpecVersion } else { "Unknown" }
        $TPM        = $TPMVersion -match '^2\.'   # SpecVersion "2.0, 0, 1.38" starts with "2."
        if (!$TPM) { Write-NecessaryAdminToolLog -Status "TPM_VERSION_BELOW_2_${TPMVersion}" -ToMaster $false }
    }
} catch {
    Write-NecessaryAdminToolLog -Status "TPM_CHECK_FAILED_$($_.Exception.Message)" -ToMaster $false
}
$TPMStatus = "$TPM (SpecVersion: $TPMVersion)"
Write-Host "  TPM 2.0: $TPM (SpecVersion: $TPMVersion)" -ForegroundColor Cyan

# Check Secure Boot (gracefully handle legacy BIOS)
$SecureBoot = try {
    Confirm-SecureBootUEFI -ErrorAction Stop
} catch {
    Write-NecessaryAdminToolLog -Status "SECUREBOOT_CHECK_FAILED_ASSUMING_FALSE" -ToMaster $false
    $false
}
$SecureBootStatus = "$SecureBoot"
Write-Host "  Secure Boot: $SecureBoot" -ForegroundColor Cyan

# Check OS architecture - Windows 11 is 64-bit only; 32-bit systems can never be upgraded
$OSArch  = $OSInfo.OSArchitecture   # reuse $OSInfo from startup
$Is64Bit = $OSArch -like "*64*"
Write-Host "  Architecture: $OSArch" -ForegroundColor Cyan
if (!$Is64Bit) {
    Write-NecessaryAdminToolLog -Status "FAILED_HW_COMPATIBILITY_32-bit OS cannot be upgraded to Windows 11" -ToMaster $false
    Write-MasterSummary -Status "HW_INCOMPATIBLE" -Method "None" -Details "32-bit OS ($OSArch) cannot be upgraded to Windows 11"
    Show-NecessaryAdminToolLogo -Msg "INCOMPATIBLE: 32-bit OS - Windows 11 is 64-bit only" "Red"
    exit 1
}

# Check RAM - Win11 requires 4 GB minimum (reuse $TotalRAMGB captured at startup)
$MIN_RAM_GB = 4
$RAMGB      = $TotalRAMGB   # Win32_ComputerSystem already queried at startup - no extra WMI call
$RAMOk      = $RAMGB -ge $MIN_RAM_GB
Write-Host "  RAM: $RAMGB GB (minimum $MIN_RAM_GB GB)" -ForegroundColor Cyan

# Check disk space
$FreeGB = [math]::Round(((Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='C:'").FreeSpace / 1GB), 2)
Write-Host "  Disk free: $FreeGB GB (minimum $MIN_DISK_SPACE_GB GB)" -ForegroundColor Cyan

# If disk is low but not critical (between 10 GB and threshold), attempt cleanup first
if ($FreeGB -lt $MIN_DISK_SPACE_GB -and $FreeGB -ge 10) {
    Write-Host "  Disk space low - attempting pre-upgrade cleanup to free space..." -ForegroundColor Yellow
    Write-NecessaryAdminToolLog -Status "DISK_LOW_${FreeGB}GB_ATTEMPTING_CLEANUP" -ToMaster $false
    $FreeGB = Invoke-PreUpgradeDiskCleanup
}

# Hardware compatibility validation
if (!$TPM -or !$SecureBoot -or !$RAMOk -or $FreeGB -lt $MIN_DISK_SPACE_GB) {
    $Reasons = @()
    if (!$TPM)                          { $Reasons += "TPM 2.0 required (found: $TPMVersion)" }
    if (!$SecureBoot)                   { $Reasons += "Secure Boot disabled" }
    if (!$RAMOk)                        { $Reasons += "${RAMGB}GB RAM (need ${MIN_RAM_GB}GB)" }
    if ($FreeGB -lt $MIN_DISK_SPACE_GB) { $Reasons += "${FreeGB}GB free (need ${MIN_DISK_SPACE_GB}GB)" }

    $ReasonText = $Reasons -join "|"
    Write-NecessaryAdminToolLog -Status "FAILED_HW_COMPATIBILITY_$ReasonText" -ToMaster $false
    Write-MasterSummary -Status "HW_INCOMPATIBLE" -Method "None" -Details $ReasonText
    Show-NecessaryAdminToolLogo -Msg "INCOMPATIBLE HARDWARE: $ReasonText" "Red"
    Write-Host "`nUpgrade cannot proceed due to hardware requirements." -ForegroundColor Red
    Start-Sleep -Seconds 5
    exit 1
}

Write-NecessaryAdminToolLog -Status "HW_COMPAT_CHECK_PASSED_TPM2_SECUREBOOT_RAM_${RAMGB}GB_DISK_${FreeGB}GB" -ToMaster $false

# --- 4b. ALREADY COMPLIANT CHECK ---
# Detect current Windows build and determine if an upgrade is actually needed.
# Windows 11 build numbers: 21H2=22000, 22H2=22621, 23H2=22631, 24H2=26100, 25H2=26200
# Update $WIN11_TARGET_BUILD when targeting a newer Windows release.
$CurrentBuild        = [int]$OSInfo.BuildNumber   # reuse $OSInfo captured at startup - no extra WMI call
$WIN11_TARGET_BUILD  = 26200   # Windows 11 25H2 (released Sept 2025) - update when new version ships
$WIN11_TARGET_NAME   = "25H2"

Write-Host ""
Write-Host "  --- System State ---" -ForegroundColor DarkYellow
Write-Host "  OS        : $OSVersion" -ForegroundColor Cyan
Write-Host "  Build     : $CurrentBuild" -ForegroundColor Cyan
Write-Host "  Target    : Windows 11 $WIN11_TARGET_NAME (Build $WIN11_TARGET_BUILD+)" -ForegroundColor Cyan
Write-Host "  Uptime    : $UptimeDays days" -ForegroundColor Cyan
Write-Host "  Disk (C:) : $FreeGB GB free" -ForegroundColor Cyan
Write-Host ""
Write-NecessaryAdminToolLog -Status "OS_VERSION_${OSVersion}_BUILD_${CurrentBuild}_TARGET_${WIN11_TARGET_NAME}" -ToMaster $false

if ($CurrentBuild -ge $WIN11_TARGET_BUILD) {
    Write-Host "  RESULT: Already on Windows 11 $WIN11_TARGET_NAME or later (Build $CurrentBuild). No upgrade needed." -ForegroundColor Green
    Write-NecessaryAdminToolLog -Status "ALREADY_COMPLIANT_WIN11_${WIN11_TARGET_NAME}_BUILD_${CurrentBuild}" -ToMaster $false
    Write-MasterSummary -Status "COMPLIANT" -Method "AlreadyUpToDate" -Details "Already on Windows 11 $WIN11_TARGET_NAME (Build $CurrentBuild) - no upgrade required"
    Show-NecessaryAdminToolLogo -Msg "Already up to date: $OSVersion (Build $CurrentBuild)" "Green"
    Show-UserNotification -Title "Windows is Up to Date" -Message "Your PC is already running Windows 11 $WIN11_TARGET_NAME (Build $CurrentBuild). No upgrade is needed. This notification was generated by your IT team." -Icon "Information"
    exit 0
}

# Log what upgrade path is needed
if ($OSVersion -match "Windows 11") {
    $UpgradePath = "Windows 11 feature update (Build $CurrentBuild -> $WIN11_TARGET_NAME Build $WIN11_TARGET_BUILD+)"
} else {
    $UpgradePath = "Windows 10 -> Windows 11 $WIN11_TARGET_NAME upgrade (current Build $CurrentBuild)"
}
Write-Host "  RESULT: Upgrade required - $UpgradePath" -ForegroundColor Yellow
Write-NecessaryAdminToolLog -Status "UPGRADE_REQUIRED_$UpgradePath" -ToMaster $false

# --- 5. CLOUD UPDATE FALLBACK FUNCTION ---
# Uses PSWindowsUpdate to trigger the feature upgrade through Windows Update channels.
# This is the same module used by GeneralUpdate.ps1 and works reliably under SYSTEM context.
#
# IMPORTANT: This path only works if the Windows 11 upgrade is currently OFFERED to this machine
# via Windows Update. If your organisation uses WUfB or WSUS to control feature update targeting,
# the upgrade must be approved there before this path will find anything to install.
#
# For maximum reliability in RMM deployments: configure an ISO path in NecessaryAdminTool Options.
# The ISO path (section 7) uses setup.exe with documented enterprise flags and is always preferred.
function Run-CloudUpdate {
    param([string]$Method = "Cloud")
    Show-NecessaryAdminToolLogo -Msg "Initiating Windows 11 upgrade via Windows Update channels..." "Yellow"
    Write-NecessaryAdminToolLog -Status "METHOD_CLOUD_START" -ToMaster $false

    # Ensure PSWindowsUpdate module is available - installs the same way as GeneralUpdate.ps1
    if (!(Get-Module -ListAvailable -Name PSWindowsUpdate)) {
        Write-Host "  Installing PSWindowsUpdate module..." -ForegroundColor Cyan
        Write-NecessaryAdminToolLog -Status "CLOUD_MODULE_INSTALL_STARTED" -ToMaster $false
        try {
            if (!(Get-PackageProvider -Name NuGet -ErrorAction SilentlyContinue)) {
                Install-PackageProvider -Name NuGet -Force -Scope AllUsers -ErrorAction Stop | Out-Null
            }
            Install-Module PSWindowsUpdate -Force -Scope AllUsers -ErrorAction Stop | Out-Null
            Write-NecessaryAdminToolLog -Status "CLOUD_MODULE_INSTALLED_SUCCESSFULLY" -ToMaster $false
        } catch {
            Write-NecessaryAdminToolLog -Status "CLOUD_MODULE_INSTALL_FAILED_$($_.Exception.Message)" -ToMaster $false
            Write-MasterSummary -Status "FAILED" -Method $Method -Details "PSWindowsUpdate install failed - configure ISO path for reliable deployment: $($_.Exception.Message)"
            Show-NecessaryAdminToolLogo -Msg "Module install failed. Configure an ISO path in NecessaryAdminTool Options." "Red"
            exit 1
        }
    }

    try {
        Import-Module PSWindowsUpdate -ErrorAction Stop

        # Scan Windows Update for feature upgrades (Category "Upgrades" = feature updates / OS upgrades)
        Write-Host "  Scanning Windows Update for feature upgrades..." -ForegroundColor Cyan
        Write-NecessaryAdminToolLog -Status "CLOUD_WU_SCAN_STARTED" -ToMaster $false

        $FeatureUpdates = @(Get-WindowsUpdate -MicrosoftUpdate -Category "Upgrades" -AcceptAll -ErrorAction Stop)

        if ($FeatureUpdates.Count -eq 0) {
            # Feature update not offered to this machine via Windows Update.
            # Common causes: WUfB/WSUS deferral policy, machine not yet in rollout ring,
            # or the organisation has not approved Win11 targeting in their WU policy.
            $Advice = "No feature upgrade currently offered via Windows Update for this machine. " +
                      "To fix: (1) configure an ISO path in NecessaryAdminTool Options, " +
                      "(2) approve Win11 targeting in WUfB/WSUS policy, or " +
                      "(3) temporarily remove WU deferral registry keys."
            Write-Host "  $Advice" -ForegroundColor Yellow
            Write-NecessaryAdminToolLog -Status "CLOUD_WU_NO_UPGRADE_OFFERED" -ToMaster $false
            Write-MasterSummary -Status "FAILED" -Method $Method -Details $Advice
            Show-NecessaryAdminToolLogo -Msg "No feature upgrade offered via Windows Update - see log for options" "Yellow"
            exit 1
        }

        $UpdateTitle = $FeatureUpdates[0].Title
        Write-Host "  Feature upgrade available: $UpdateTitle" -ForegroundColor Green
        Write-NecessaryAdminToolLog -Status "CLOUD_WU_UPGRADE_FOUND_$UpdateTitle" -ToMaster $false
        Write-NecessaryAdminToolLog -Status "CLOUD_WU_INSTALL_STARTED" -ToMaster $true
        Show-NecessaryAdminToolLogo -Msg "Installing via Windows Update... (Do not turn off - may take 1-2 hours)" "Yellow"
        Write-Host "  PSWindowsUpdate will report download and install progress below." -ForegroundColor Gray
        Write-Host "  The machine will reboot automatically when installation is complete." -ForegroundColor Gray

        # Install synchronously - PSWindowsUpdate outputs progress natively so ME log stays alive.
        # -IgnoreReboot defers the physical reboot so the script can exit cleanly and log results;
        # Windows will reboot on its own schedule (or ME can trigger it after task completion).
        $Results = Install-WindowsUpdate -MicrosoftUpdate -Category "Upgrades" -AcceptAll -IgnoreReboot -ErrorAction Stop

        $Failed = @($Results | Where-Object { $_.Result -eq 'Failed' })
        if ($Failed.Count -gt 0) {
            $FailDetail = ($Failed | ForEach-Object { "$($_.Title): $($_.Result)" }) -join "; "
            throw "Install reported failures: $FailDetail"
        }

        Write-NecessaryAdminToolLog -Status "CLOUD_WU_COMPLETE_ISSUING_FORCED_REBOOT" -ToMaster $false
        Write-MasterSummary -Status "SUCCESS" -Method $Method -UpdateCount "1" -Details "Windows Update feature upgrade complete - forced reboot issued to finish installation"
        Show-NecessaryAdminToolLogo -Msg "Windows Update upgrade complete - restarting to finish installation..." "Green"
        Show-UserNotification -Title "Windows 11 Upgrade Complete" -Message "Windows 11 $WIN11_TARGET_NAME has been installed successfully. Your computer will restart in 5 minutes to complete the upgrade. PLEASE SAVE YOUR WORK NOW." -Icon "Warning"

        # Always force reboot - feature upgrades require it and WU policies may defer auto-restart
        # indefinitely (WUfB active hours, WU restart policies, etc.).
        $RebootDelay = if (Test-UserLoggedIn) { 300 } else { 60 }
        Write-Host "  Issuing restart in $RebootDelay seconds (user present: $(Test-UserLoggedIn))..." -ForegroundColor Yellow
        Write-NecessaryAdminToolLog -Status "CLOUD_WU_REBOOT_DELAY_${RebootDelay}s" -ToMaster $false
        shutdown.exe /r /t $RebootDelay /c "NecessaryAdminTool: Windows 11 upgrade installed. Restarting to complete installation."
        exit 0

    } catch {
        Write-NecessaryAdminToolLog -Status "CLOUD_WU_FAILED_$($_.Exception.Message)" -ToMaster $false
        Write-MasterSummary -Status "FAILED" -Method $Method -Details $_.Exception.Message
        Show-NecessaryAdminToolLogo -Msg "Cloud upgrade failed: $($_.Exception.Message)" "Red"
        exit 1
    }
}

# --- 5b. VPN DETECTION ---
function Test-VpnConnected {
    # Check adapter description for known VPN clients
    $VpnPatterns = 'Cisco AnyConnect|Cisco Secure Client|GlobalProtect|PANGP|Palo Alto|Pulse Secure|Ivanti|FortiClient|OpenVPN|TAP-Windows|WireGuard|SonicWALL|Juniper|Zscaler|Prisma Access'
    $VpnByName = Get-NetAdapter -ErrorAction SilentlyContinue | Where-Object {
        $_.Status -eq 'Up' -and $_.InterfaceDescription -match $VpnPatterns
    }
    # Check for PPP interface type (Windows built-in PPTP/L2TP/SSTP/IKEv2 VPN)
    $VpnByType = Get-NetAdapter -ErrorAction SilentlyContinue | Where-Object {
        $_.Status -eq 'Up' -and $_.InterfaceType -eq 23
    }
    # Use Count - Get-NetAdapter returns an empty array (not $null) when nothing matches
    return (@($VpnByName).Count -gt 0 -or @($VpnByType).Count -gt 0)
}

# --- 6. USER WARNING (timed dialog - auto-proceeds, up to 2 postpones, boot-persistent) ---
#
# Flow:
#   First run  -> timed dialog: auto-proceeds after 20 min if no one clicks anything
#   Postpone   -> increment count, exit 0 (re-push ME task to retry)
#   PC restart  -> startup task re-runs this script; pending flag skips dialog, upgrades immediately
#

$PostponeFlag = "C:\Windows\Temp\NecessaryAdminTool_Feature_Postpone.txt"
$PendingFlag  = "C:\Windows\Temp\NecessaryAdminTool_Feature_Pending.txt"
$BootTaskName = "NecessaryAdminTool_FeatureUpgrade_Pending"
$MaxPostpones = 2
$WarningMinutes = 20   # minutes the user has to save work / postpone before upgrade starts

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

# --- Timed warning dialog (replaces blocking MessageBox) ---
# Auto-proceeds when the countdown hits 0 so ME never hangs waiting for a click.
# If running as SYSTEM in Session 0 (typical for ME), the form may not be visible to
# the user - in that case ShowDialog() returns immediately and the script proceeds,
# which is the correct behaviour for overnight / unattended deployments.
function Show-UpgradeWarning {
    param(
        [string]$Title,
        [string]$BodyText,
        [int]$CountdownSeconds,
        [bool]$AllowPostpone,
        [int]$PostponesLeft
    )

    $script:UpgradeChoice  = "Proceed"           # default: proceed when timer expires
    $script:SecondsLeft    = $CountdownSeconds

    $Form                  = New-Object System.Windows.Forms.Form
    $Form.Text             = $Title
    $Form.Width            = 560
    $Form.Height           = 370
    $Form.TopMost          = $true
    $Form.StartPosition    = [System.Windows.Forms.FormStartPosition]::CenterScreen
    $Form.FormBorderStyle  = [System.Windows.Forms.FormBorderStyle]::FixedDialog
    $Form.MaximizeBox      = $false
    $Form.MinimizeBox      = $false
    $Form.ControlBox       = $false   # no X - must use a button

    # Body text
    $LblBody               = New-Object System.Windows.Forms.Label
    $LblBody.Text          = $BodyText
    $LblBody.Location      = New-Object System.Drawing.Point(20, 16)
    $LblBody.Size          = New-Object System.Drawing.Size(510, 215)
    $LblBody.Font          = New-Object System.Drawing.Font("Segoe UI", 10)
    $Form.Controls.Add($LblBody)

    # Countdown label
    $LblCountdown          = New-Object System.Windows.Forms.Label
    $Mins0                 = [math]::Floor($CountdownSeconds / 60)
    $Secs0                 = $CountdownSeconds % 60
    $LblCountdown.Text     = "Proceeding automatically in  $Mins0`:$($Secs0.ToString('00'))"
    $LblCountdown.Location = New-Object System.Drawing.Point(20, 238)
    $LblCountdown.Size     = New-Object System.Drawing.Size(510, 28)
    $LblCountdown.Font     = New-Object System.Drawing.Font("Segoe UI", 10, [System.Drawing.FontStyle]::Bold)
    $LblCountdown.ForeColor = [System.Drawing.Color]::DarkOrange
    $Form.Controls.Add($LblCountdown)

    # "Start Now" button - proceed immediately
    $BtnNow                = New-Object System.Windows.Forms.Button
    $BtnNow.Text           = "Start Now"
    $BtnNow.Location       = New-Object System.Drawing.Point(20, 282)
    $BtnNow.Size           = New-Object System.Drawing.Size(110, 34)
    $BtnNow.Font           = New-Object System.Drawing.Font("Segoe UI", 10)
    $BtnNow.Add_Click({ $script:UpgradeChoice = "Proceed"; $Form.Close() })
    $Form.Controls.Add($BtnNow)

    # "Postpone" button - only shown when postpones are available
    if ($AllowPostpone) {
        $BtnPostpone           = New-Object System.Windows.Forms.Button
        $BtnPostpone.Text      = "Postpone  ($PostponesLeft left)"
        $BtnPostpone.Location  = New-Object System.Drawing.Point(148, 282)
        $BtnPostpone.Size      = New-Object System.Drawing.Size(175, 34)
        $BtnPostpone.Font      = New-Object System.Drawing.Font("Segoe UI", 10)
        $BtnPostpone.Add_Click({ $script:UpgradeChoice = "Postpone"; $Form.Close() })
        $Form.Controls.Add($BtnPostpone)
    }

    # Timer fires every second - updates countdown label, closes form at zero
    $Timer                 = New-Object System.Windows.Forms.Timer
    $Timer.Interval        = 1000
    $Timer.Add_Tick({
        $script:SecondsLeft--
        $m = [math]::Floor($script:SecondsLeft / 60)
        $s = $script:SecondsLeft % 60
        $LblCountdown.Text = "Proceeding automatically in  $m`:$($s.ToString('00'))"
        if ($script:SecondsLeft -le 0) {
            $script:UpgradeChoice = "Proceed"
            $Timer.Stop()
            $Form.Close()
        }
    })
    $Timer.Start()

    try   { [void]$Form.ShowDialog() }
    catch { <# Session 0 / no desktop - form invisible, default "Proceed" applies #> }
    finally { $Timer.Stop(); $Timer.Dispose(); $Form.Dispose() }

    return $script:UpgradeChoice
}

Write-Host ""
Write-Host "  --- User Warning Check ---" -ForegroundColor DarkYellow
Write-Host "  Checking for existing postpone / pending-restart state..." -ForegroundColor Cyan
Write-NecessaryAdminToolLog -Status "USER_WARNING_CHECK_STARTED" -ToMaster $false

if (Test-Path $PendingFlag) {
    # Machine restarted during the 20-min countdown - skip dialog, go straight to upgrade
    Write-Host "  [BOOT RESUME] Pending flag found - machine restarted during countdown." -ForegroundColor Yellow
    Write-Host "  User previously acknowledged the upgrade. Skipping dialog, starting upgrade now." -ForegroundColor Yellow
    Write-NecessaryAdminToolLog -Status "PENDING_FLAG_FOUND_RESUMING_AFTER_RESTART" -ToMaster $false
    Show-NecessaryAdminToolLogo -Msg "Resuming upgrade after restart (user previously acknowledged)..." "Yellow"
    Unregister-ScheduledTask -TaskName $BootTaskName -Confirm:$false -ErrorAction SilentlyContinue
    Remove-Item $PendingFlag  -Force -ErrorAction SilentlyContinue
    Remove-Item $PostponeFlag -Force -ErrorAction SilentlyContinue
    Write-Host "  Boot task and flags cleared. Proceeding to upgrade." -ForegroundColor Green
    # Fall through to main execution logic below

} else {
    # Read how many times the user has already postponed
    $PostponeCount = 0
    if (Test-Path $PostponeFlag) {
        try { $PostponeCount = [int](Get-Content $PostponeFlag -Raw -ErrorAction Stop).Trim() } catch { $PostponeCount = 0 }
    }
    $PostponesLeft = $MaxPostpones - $PostponeCount

    Write-Host "  Postpone history: $PostponeCount of $MaxPostpones used ($PostponesLeft remaining)" -ForegroundColor Cyan
    Write-NecessaryAdminToolLog -Status "POSTPONE_STATE_${PostponeCount}_OF_${MaxPostpones}_USED" -ToMaster $false

    # === IDLE MACHINE CHECK ===
    # Skip dialog and countdown entirely if no interactive user session is present.
    # Common scenario: overnight ME push to a powered-on but unattended workstation.
    $UserPresent = Test-UserLoggedIn
    Write-Host "  Interactive user session detected: $UserPresent" -ForegroundColor Cyan
    Write-NecessaryAdminToolLog -Status "USER_SESSION_PRESENT_${UserPresent}" -ToMaster $false

    if (!$UserPresent) {
        # Unattended machine - proceed immediately without dialog or countdown
        Write-Host "  No interactive user detected - running in unattended mode (no dialog, no countdown)." -ForegroundColor Yellow
        Write-NecessaryAdminToolLog -Status "UNATTENDED_MODE_SKIPPING_DIALOG_AND_COUNTDOWN" -ToMaster $false
        Show-NecessaryAdminToolLogo -Msg "Unattended mode - no user present, upgrading immediately..." "Yellow"

    } else {
        # === USER IS PRESENT - SHOW WARNING DIALOG ===

        # Detect open applications to include in dialog body
        $OpenApps    = Get-OpenApplications
        $AppsWarning = if ($OpenApps -and @($OpenApps).Count -gt 0) {
            "`n`nDETECTED OPEN APPLICATIONS: $($OpenApps -join ', ')`nPlease save and close them before the upgrade begins."
        } else { "" }

        $BaseMsg = "Your IT team is pushing a mandatory Windows OS upgrade to this computer." +
            "`n`n  >>> SAVE ALL YOUR WORK AND CLOSE YOUR APPLICATIONS NOW. <<<" +
            $AppsWarning +
            "`n`nThe upgrade will begin in $WarningMinutes minutes and your computer will restart." +
            "`nThe process takes 1-2 hours. You will be unable to use your computer during this time."

        if ($PostponeCount -lt $MaxPostpones) {
            $BodyText = $BaseMsg +
                "`n`nPostpones remaining: $PostponesLeft of $MaxPostpones" +
                "`n`n  Start Now  - Begin the $WarningMinutes-minute countdown immediately." +
                "`n  Postpone   - Delay this upgrade ($PostponesLeft postpone(s) remaining)." +
                "`n`nIf you do not respond, the upgrade starts automatically when the timer reaches zero."
            $DialogTitle = "NecessaryAdminTool IT - Windows 11 $WIN11_TARGET_NAME Upgrade in $WarningMinutes Minutes"
            Write-Host ""
            Write-Host "  [WARNING DIALOG] Showing timed upgrade notice ($WarningMinutes-min countdown)..." -ForegroundColor Yellow
            Write-Host "  Postpones remaining: $PostponesLeft of $MaxPostpones" -ForegroundColor Gray
            if ($OpenApps -and @($OpenApps).Count -gt 0) {
                Write-Host "  Detected open apps: $($OpenApps -join ', ')" -ForegroundColor Gray
            }
            Write-Host "  If no response, upgrade proceeds automatically when timer reaches zero." -ForegroundColor Gray
            Write-NecessaryAdminToolLog -Status "WARNING_DIALOG_SHOWN_${WarningMinutes}MIN_POSTPONES_LEFT_${PostponesLeft}" -ToMaster $false
        } else {
            $BodyText = $BaseMsg +
                "`n`n  *** NO FURTHER POSTPONES ARE AVAILABLE ***" +
                "`n`nIf you do not respond, the upgrade starts automatically when the timer reaches zero."
            $DialogTitle = "NecessaryAdminTool IT - Windows 11 $WIN11_TARGET_NAME Upgrade (Final Notice)"
            Write-Host ""
            Write-Host "  [FINAL NOTICE] No postpones remaining. Showing final timed warning..." -ForegroundColor Yellow
            Write-Host "  If no response, upgrade proceeds automatically when timer reaches zero." -ForegroundColor Gray
            Write-NecessaryAdminToolLog -Status "FINAL_NOTICE_SHOWN_NO_POSTPONES_REMAINING" -ToMaster $false
        }

        # === msg.exe belt-and-suspenders ===
        # Fires a cross-session popup BEFORE the WinForms dialog so users in all sessions
        # (including Remote Desktop) see the alert even if the form doesn't render in Session 0.
        $MsgText = "Windows 11 $WIN11_TARGET_NAME upgrade starts in $WarningMinutes minutes. SAVE YOUR WORK AND CLOSE APPLICATIONS NOW. Managed by IT."
        try {
            $MsgProc = Start-Process "msg.exe" `
                -ArgumentList "* /TIME:1200 `"$MsgText`"" `
                -WindowStyle Hidden -PassThru -ErrorAction Stop
            $MsgProc | Wait-Process -Timeout 10 -ErrorAction SilentlyContinue
            Write-NecessaryAdminToolLog -Status "MSG_EXE_SENT_TO_ALL_SESSIONS" -ToMaster $false
        } catch {
            Write-NecessaryAdminToolLog -Status "MSG_EXE_FAILED_$($_.Exception.Message)" -ToMaster $false
            # Non-fatal - WinForms dialog is the primary notification path
        }

        $Choice = Show-UpgradeWarning `
            -Title            $DialogTitle `
            -BodyText         $BodyText `
            -CountdownSeconds ($WarningMinutes * 60) `
            -AllowPostpone    ($PostponeCount -lt $MaxPostpones) `
            -PostponesLeft    $PostponesLeft

        Write-Host "  [DIALOG CLOSED] Result: $Choice" -ForegroundColor Cyan
        Write-NecessaryAdminToolLog -Status "WARNING_DIALOG_RESULT_${Choice}" -ToMaster $false

        if ($Choice -eq "Postpone") {
            $NewCount = $PostponeCount + 1
            $NewCount | Out-File $PostponeFlag -Encoding ASCII -Force
            Write-Host "  [USER POSTPONED] Postpones used: $NewCount of $MaxPostpones." -ForegroundColor Yellow
            Write-Host "  Exiting with code 0. Re-push the ME task to attempt again." -ForegroundColor Yellow
            Write-NecessaryAdminToolLog -Status "UPGRADE_POSTPONED_${NewCount}_OF_${MaxPostpones}" -ToMaster $false
            Write-MasterSummary -Status "POSTPONED" -Method "UserPostpone" -Details "User postponed ($NewCount of $MaxPostpones used)"
            Show-NecessaryAdminToolLogo -Msg "Upgrade postponed ($NewCount of $MaxPostpones postpones used)." "Yellow"
            exit 0
        }

        Write-Host "  [PROCEEDING] User acknowledged (or timer expired). Starting $WarningMinutes-minute work-save window." -ForegroundColor Green

        # Write pending flag + register boot task before the countdown sleep,
        # so if the PC restarts during countdown the upgrade auto-resumes on next boot
        "Acknowledged" | Out-File $PendingFlag -Encoding ASCII -Force
        Write-Host "  Pending flag written - upgrade auto-resumes if PC restarts." -ForegroundColor Cyan

        $ScriptPath = $MyInvocation.MyCommand.Path
        if (-not [string]::IsNullOrEmpty($ScriptPath) -and (Test-Path $ScriptPath -ErrorAction SilentlyContinue)) {
            try {
                $Action   = New-ScheduledTaskAction -Execute "powershell.exe" `
                                -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$ScriptPath`""
                $Trigger  = New-ScheduledTaskTrigger -AtStartup
                $Settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Hours 4) `
                                -MultipleInstances IgnoreNew -StartWhenAvailable $true
                Register-ScheduledTask -TaskName $BootTaskName -Action $Action -Trigger $Trigger `
                    -Settings $Settings -RunLevel Highest -User "SYSTEM" -Force -ErrorAction Stop | Out-Null
                Write-Host "  [BOOT TASK] '$BootTaskName' registered - upgrade survives a reboot." -ForegroundColor Green
                Write-NecessaryAdminToolLog -Status "BOOT_TASK_REGISTERED_UPGRADE_RESUMES_ON_RESTART" -ToMaster $false
            } catch {
                Write-Host "  [BOOT TASK] WARNING: Could not register startup task: $($_.Exception.Message)" -ForegroundColor Yellow
                Write-Host "  Upgrade still proceeds after countdown - boot persistence unavailable." -ForegroundColor Yellow
                Write-NecessaryAdminToolLog -Status "BOOT_TASK_REGISTER_FAILED_$($_.Exception.Message)" -ToMaster $false
            }
        } else {
            Write-Host "  [BOOT TASK] Script path unavailable - boot persistence skipped." -ForegroundColor Gray
            Write-NecessaryAdminToolLog -Status "BOOT_TASK_SKIPPED_NO_SCRIPT_PATH" -ToMaster $false
        }

        # 20-minute work-save countdown - fully visible in ME execution log
        Write-Host ""
        Write-Host "  --- $WarningMinutes-Minute Work-Save Countdown ---" -ForegroundColor DarkYellow
        Write-NecessaryAdminToolLog -Status "UPGRADE_COUNTDOWN_STARTED_${WarningMinutes}MIN" -ToMaster $false
        Show-NecessaryAdminToolLogo -Msg "Upgrade begins in $WarningMinutes minutes. Save your work now..." "Yellow"

        for ($Min = $WarningMinutes; $Min -gt 0; $Min--) {
            $Elapsed = $WarningMinutes - $Min
            Write-Host "  [$Min min remaining | $Elapsed min elapsed | $(Get-Date -Format 'HH:mm:ss')]" -ForegroundColor Yellow
            Write-NecessaryAdminToolLog -Status "UPGRADE_COUNTDOWN_${Min}MIN_REMAINING" -ToMaster $false
            Start-Sleep -Seconds 60
        }

        # Countdown complete - remove boot persistence, proceed to upgrade
        Write-Host ""
        Write-Host "  [COUNTDOWN COMPLETE] $WarningMinutes minutes elapsed. Proceeding to upgrade now." -ForegroundColor Green
        Write-NecessaryAdminToolLog -Status "UPGRADE_COUNTDOWN_COMPLETE_PROCEEDING" -ToMaster $false
        Unregister-ScheduledTask -TaskName $BootTaskName -Confirm:$false -ErrorAction SilentlyContinue
        Remove-Item $PendingFlag  -Force -ErrorAction SilentlyContinue
        Remove-Item $PostponeFlag -Force -ErrorAction SilentlyContinue
        Write-Host "  Boot task and flags cleared." -ForegroundColor Cyan
        Show-NecessaryAdminToolLogo -Msg "Countdown complete. Starting upgrade now..." "Yellow"

    }   # end if ($UserPresent)
}

# --- 7. MAIN EXECUTION LOGIC ---
Write-Host ""
Write-Host "  --- Upgrade Method Selection ---" -ForegroundColor DarkYellow
Write-NecessaryAdminToolLog -Status "UPGRADE_METHOD_SELECTION_STARTED_Hostname=${Comp}_Pattern=${HostnamePattern}" -ToMaster $false

# VPN check - only skip ISO if the ISO path is a UNC network share (\\server\share).
# Local ISO paths (C:\, D:\, etc.) are not affected by VPN and should proceed normally.
$VpnActive    = Test-VpnConnected
$IsNetworkISO = (-not [string]::IsNullOrEmpty($ISOPath)) -and $ISOPath.TrimStart().StartsWith("\\")
Write-Host "  VPN active: $VpnActive | Network ISO: $IsNetworkISO" -ForegroundColor Cyan

if ($VpnActive -and $IsNetworkISO) {
    # UNC share is likely unreachable over VPN - go straight to Windows Update
    Write-NecessaryAdminToolLog -Status "VPN_DETECTED_NETWORK_ISO_UNREACHABLE_USING_CLOUD" -ToMaster $false
    Show-NecessaryAdminToolLogo -Msg "VPN detected (UNC share unreachable) - using Windows Update" "Yellow"
    Run-CloudUpdate -Method "Cloud-VPN"
} elseif ($VpnActive) {
    # VPN active but ISO is a local path - VPN does not affect local access, proceed normally
    Write-NecessaryAdminToolLog -Status "VPN_DETECTED_LOCAL_ISO_PROCEEDING_NORMALLY" -ToMaster $false
    Write-Host "  VPN active but ISO is a local path - evaluating ISO/WU normally." -ForegroundColor Cyan
} else {
    Write-NecessaryAdminToolLog -Status "VPN_NOT_DETECTED_EVALUATING_ISO_PATH" -ToMaster $false
}

# Check if hostname matches pattern and ISO is available
$HostnameMatch = $Comp -like $HostnamePattern
$ISOConfigured = -not [string]::IsNullOrEmpty($ISOPath)
$ISOExists     = $ISOConfigured -and (Test-Path $ISOPath -ErrorAction SilentlyContinue)
Write-Host "  Hostname match ($HostnamePattern): $HostnameMatch" -ForegroundColor Cyan
Write-Host "  ISO path configured: $ISOConfigured$(if ($ISOConfigured) { " ($ISOPath)" })" -ForegroundColor Cyan
Write-Host "  ISO file accessible: $ISOExists" -ForegroundColor Cyan
Write-NecessaryAdminToolLog -Status "ISO_EVAL_HostMatch=${HostnameMatch}_ISOConfigured=${ISOConfigured}_ISOExists=${ISOExists}" -ToMaster $false

if ($HostnameMatch -and $ISOExists) {

    # Verify ISO is valid size
    $ISOSizeBytes = (Get-Item $ISOPath -ErrorAction SilentlyContinue).Length
    $ISOSize      = [math]::Round($ISOSizeBytes / 1GB, 2)
    Write-Host "  ISO size: $ISOSize GB" -ForegroundColor Cyan
    Write-NecessaryAdminToolLog -Status "ISO_SIZE_CHECK_${ISOSize}GB_Path=${ISOPath}" -ToMaster $false

    if ($ISOSize -lt $MIN_ISO_SIZE_GB) {
        Write-NecessaryAdminToolLog -Status "ERROR_ISO_TOO_SMALL_${ISOSize}GB_EXPECTED_${MIN_ISO_SIZE_GB}GB" -ToMaster $false
        Run-CloudUpdate -Method "Cloud-ISOTooSmall"
        exit 1   # Run-CloudUpdate exits internally; this is a safety backstop
    }

    Write-Host "  Selected method: ISO ($ISOPath)" -ForegroundColor Green
    Write-NecessaryAdminToolLog -Status "METHOD_ISO_START_Host=${Comp}_Pattern=${HostnamePattern}_ISO=${ISOPath}_Size=${ISOSize}GB" -ToMaster $false

    $MountedISO = $null
    try {
        # Mount ISO
        Show-NecessaryAdminToolLogo -Msg "Mounting Local ISO..." "Cyan"
        $MountedISO = Mount-DiskImage -ImagePath $ISOPath -PassThru -ErrorAction Stop
        $Drive = ($MountedISO | Get-Volume).DriveLetter
        if ([string]::IsNullOrEmpty($Drive)) {
            throw "ISO mounted but no drive letter was assigned"
        }
        Write-NecessaryAdminToolLog -Status "ISO_MOUNTED_DRIVE_${Drive}" -ToMaster $false

        # Verify setup.exe exists
        $SetupPath = "${Drive}:\setup.exe"
        if (!(Test-Path $SetupPath)) {
            throw "setup.exe not found on mounted ISO"
        }

        # Run setup with timeout protection and per-minute progress reporting
        # /compat ignorewarning - CRITICAL: without this, dismissible compat warnings silently block the upgrade in /quiet mode
        # /copylogs - copies Panther logs to our log directory for post-failure diagnostics
        # /migratedrivers all - explicit driver migration (default is non-deterministic)
        # /telemetry disable - suppress setup telemetry per enterprise policy
        $SetupArgs = "/auto upgrade /quiet /eula accept /showoobe none /dynamicupdate disable /compat ignorewarning /migratedrivers all /telemetry disable /copylogs `"$PCLogDir`""
        Write-NecessaryAdminToolLog -Status "ISO_RUNNING_Args=$SetupArgs" -ToMaster $true
        Show-NecessaryAdminToolLogo -Msg "Upgrading via ISO... (Do not turn off - may take up to 2 hours)" "Yellow"
        Write-Host "  Progress will be reported every 60 seconds in this log." -ForegroundColor Gray

        $Proc = Start-Process $SetupPath -ArgumentList $SetupArgs -PassThru -ErrorAction Stop

        # Poll every 60s - keeps ME execution log alive during the 2-hour install
        $ISOCheckInterval = 60
        $ISOElapsed       = 0
        $PantherLog       = "C:\`$WINDOWS.~BT\Sources\Panther\setupact.log"

        while (!$Proc.HasExited) {
            if ($ISOElapsed -ge $SETUP_TIMEOUT_SECONDS) {
                Write-NecessaryAdminToolLog -Status "TIMEOUT_ISO_SETUP_KILLED_AFTER_$($SETUP_TIMEOUT_SECONDS / 3600)h" -ToMaster $false
                Write-MasterSummary -Status "FAILED" -Method "ISO" -Details "Timed out after $($SETUP_TIMEOUT_SECONDS / 3600) hours"
                $Proc.Kill()
                Show-NecessaryAdminToolLogo -Msg "ISO setup timed out after $($SETUP_TIMEOUT_SECONDS / 3600) hours" "Red"
                exit 1
            }

            Start-Sleep -Seconds $ISOCheckInterval
            $ISOElapsed    += $ISOCheckInterval
            $ISOElapsedMin  = [math]::Round($ISOElapsed / 60, 0)

            # Detect phase from Windows Setup Panther log (same as cloud branch)
            $ISOPhase = "Upgrading..."
            if (Test-Path $PantherLog -ErrorAction SilentlyContinue) {
                $Recent  = Get-Content $PantherLog -Tail 50 -ErrorAction SilentlyContinue
                $PctLine = $Recent | Select-String 'Percentage complete: (\d+)' | Select-Object -Last 1
                if ($PctLine) {
                    $ISOPhase = "Upgrading ($($PctLine.Matches.Groups[1].Value)% complete)"
                }
            } elseif (Test-Path "C:\`$WINDOWS.~BT" -ErrorAction SilentlyContinue) {
                $ISOPhase = "Downloading / Preparing..."
            }

            Write-NecessaryAdminToolLog -Status "ISO_PROGRESS_${ISOElapsedMin}min_$ISOPhase" -ToMaster $false
            Write-Host "  [${ISOElapsedMin}m elapsed] $ISOPhase" -ForegroundColor Cyan
        }

        # Check exit code
        $ExitCode = $Proc.ExitCode
        $ExitDesc = Get-InstallExitDescription -ExitCode $ExitCode
        Write-Host "  ISO setup exit: $ExitDesc" -ForegroundColor Cyan
        Write-NecessaryAdminToolLog -Status "ISO_COMPLETE_CODE_${ExitCode}_$ExitDesc" -ToMaster $false

        if ($ExitCode -eq 0 -or $ExitCode -eq 3010) {
            Write-MasterSummary -Status "SUCCESS" -Method "ISO" -UpdateCount "1" -Details $ExitDesc
            Show-NecessaryAdminToolLogo -Msg "ISO upgrade completed successfully" "Green"
            Show-UserNotification -Title "Windows 11 Upgrade Complete" -Message "Windows 11 $WIN11_TARGET_NAME has been installed successfully. Your computer will restart shortly to complete the upgrade. PLEASE SAVE YOUR WORK NOW." -Icon "Warning"
            exit 0
        } else {
            # setup.exe exited with an error - attempt Windows Update as fallback before giving up.
            # Note: hardware/driver/app compat failures (0xC190xxxx) will likely fail WU too,
            # but we try anyway because some errors (e.g. ISO-specific format issues) may not apply.
            Write-Host "  ISO setup failed ($ExitDesc) - trying Windows Update fallback..." -ForegroundColor Yellow
            Write-NecessaryAdminToolLog -Status "ISO_FAILED_CODE_${ExitCode}_TRYING_WU_FALLBACK" -ToMaster $false
            Run-CloudUpdate -Method "Cloud-ISOFailed"
        }
    }
    catch {
        Write-NecessaryAdminToolLog -Status "ISO_FAILED_FALLBACK_CLOUD_$($_.Exception.Message)" -ToMaster $false
        Show-NecessaryAdminToolLogo -Msg "ISO method failed, trying cloud update..." "Yellow"
        Run-CloudUpdate -Method "Cloud-ISOFallback"
    }
    finally {
        # Always dismount ISO, even if script crashes
        if ($MountedISO) {
            try {
                Dismount-DiskImage -ImagePath $ISOPath -ErrorAction Stop
                Write-NecessaryAdminToolLog -Status "ISO_DISMOUNTED" -ToMaster $false
            } catch {
                Write-NecessaryAdminToolLog -Status "WARNING_ISO_DISMOUNT_FAILED" -ToMaster $false
                # Try forceful dismount
                try {
                    Get-DiskImage -ImagePath $ISOPath | Dismount-DiskImage -ErrorAction SilentlyContinue
                } catch {
                    Write-NecessaryAdminToolLog -Status "WARNING_ISO_FORCE_DISMOUNT_FAILED_$($_.Exception.Message)" -ToMaster $false
                }
            }
        }
    }
}
else {
    # ISO path not used - determine specific reason and log it
    if (!$HostnameMatch) {
        Write-Host "  Selected method: Cloud (hostname '$Comp' does not match pattern '$HostnamePattern')" -ForegroundColor Cyan
        Write-NecessaryAdminToolLog -Status "HOSTNAME_PATTERN_MISMATCH_Host=${Comp}_Pattern=${HostnamePattern}_USING_CLOUD" -ToMaster $false
        Run-CloudUpdate -Method "Cloud-NoPatternMatch"
    } elseif (!$ISOConfigured) {
        Write-Host "  Selected method: Cloud (no ISO path configured)" -ForegroundColor Cyan
        Write-NecessaryAdminToolLog -Status "ISO_PATH_NOT_CONFIGURED_USING_CLOUD" -ToMaster $false
        Run-CloudUpdate -Method "Cloud-NoISO"
    } else {
        Write-Host "  Selected method: Cloud (ISO path configured but file not accessible: $ISOPath)" -ForegroundColor Yellow
        Write-NecessaryAdminToolLog -Status "ISO_NOT_ACCESSIBLE_Path=${ISOPath}_USING_CLOUD" -ToMaster $false
        Run-CloudUpdate -Method "Cloud-ISONotFound"
    }
}
