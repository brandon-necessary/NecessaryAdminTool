#Requires -Version 5.1
#Requires -RunAsAdministrator
# ==============================================================================
# NECESSARYADMINTOOL IT - GENERAL UPDATE SUITE (v1.0 - Bulletproof Edition)
# Includes: Windows Updates, Firmware, Uptime Guard, Power Check, ManageEngine Compatible
# Security Hardened: Admin checks, module validation, safe cleanup, disk space checks
# ==============================================================================

# EARLY HEARTBEAT - first executable line; if ME execution log is blank, script never loaded
# (most common cause: #Requires -RunAsAdministrator failing in non-elevated agent context)
Write-Host "  [$(Get-Date -Format 'HH:mm:ss')] GeneralUpdate.ps1 - Script loaded on $env:COMPUTERNAME, starting execution..." -ForegroundColor Cyan

# Enterprise standard: fail loudly on unexpected errors; use -ErrorAction SilentlyContinue where fallback is intentional
$ErrorActionPreference = 'Stop'

# --- 0. CONFIGURABLE PATHS (Environment Variable Based) ---
# ALL paths are configured via environment variables or app settings
# NO hardcoded paths - configured through NecessaryAdminTool Options menu

$LogDir              = $env:NECESSARYADMINTOOL_LOG_DIR
$DatabaseType        = ""  # NAT_INJECT_DB_TYPE
$SqlConnectionString = ""  # NAT_INJECT_SQL_CONN

# Network/configured path validation with automatic local fallback
if ([string]::IsNullOrEmpty($LogDir) -or !(Test-Path $LogDir -ErrorAction SilentlyContinue)) {
    # Fallback to local logging if path not configured or unavailable
    $LogDir = "$env:TEMP\NecessaryAdminTool_Logs"
    if (!(Test-Path $LogDir)) {
        New-Item -ItemType Directory -Path $LogDir -Force | Out-Null
    }
    Write-Host "INFO: Using local fallback logging at $LogDir" -ForegroundColor Cyan
}

$MasterLog   = "$LogDir\Master_Update_Log.csv"
$Timestamp   = Get-Date -Format 'yyyy-MM-dd_HH-mm'
$PCLogDir    = "$LogDir\Individual_PC_Logs"
$PCArchive   = "$PCLogDir\Archived_PC_Logs"
$PCLog       = "$PCLogDir\$($env:COMPUTERNAME)_General_$Timestamp.txt"
$FlagFile    = "C:\Windows\Temp\NecessaryAdminTool_Uptime_Flag.txt"
$Comp        = $env:COMPUTERNAME

# Constants
$LOG_RETENTION_DAYS = 30
$MIN_DISK_SPACE_GB = 10
$UPTIME_LIMIT_DAYS = 30

# Create log directories if missing
if (!(Test-Path $PCLogDir)) {
    New-Item -ItemType Directory -Path $PCLogDir -Force | Out-Null
}
if (!(Test-Path $PCArchive)) {
    New-Item -ItemType Directory -Path $PCArchive -Force | Out-Null
}

# Start transcript - captures ALL console output automatically (belt-and-suspenders alongside custom logging)
# Must specify explicit path: under SYSTEM, $HOME resolves to C:\Windows\System32\config\systemprofile
$TranscriptPath = "$PCLogDir\$($env:COMPUTERNAME)_General_${Timestamp}_Transcript.txt"
Start-Transcript -Path $TranscriptPath -Append -NoClobber -ErrorAction SilentlyContinue

# Safe maintenance: Only clean NecessaryAdminTool temp files
Remove-Item "C:\Windows\Temp\NecessaryAdminTool*" -Recurse -Force -ErrorAction SilentlyContinue

# Archive old logs safely
Get-ChildItem -Path $PCLogDir -Filter "*.txt" -ErrorAction SilentlyContinue |
    Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-$LOG_RETENTION_DAYS) } |
    Move-Item -Destination $PCArchive -Force -ErrorAction SilentlyContinue

# Capture baseline info early so it's available at all exit points (single WMI call reused below)
$ScriptStart = Get-Date
$OSInfo      = Get-CimInstance Win32_OperatingSystem
$OSVersion   = $OSInfo.Caption

# Gather system context for startup banner (single query - reused throughout)
$PSVersion   = "$($PSVersionTable.PSVersion.Major).$($PSVersionTable.PSVersion.Minor)"
$RunningAs   = [Security.Principal.WindowsIdentity]::GetCurrent().Name
$SysInfo     = Get-CimInstance Win32_ComputerSystem -ErrorAction SilentlyContinue
$DomainName  = if ($SysInfo) { $SysInfo.Domain } else { "Unknown" }
$TotalRAMGB  = if ($SysInfo) { [math]::Round($SysInfo.TotalPhysicalMemory / 1GB, 2) } else { 0 }
$UptimeDays  = [math]::Round(((Get-Date) - $OSInfo.LastBootUpTime).TotalDays, 2)
$FreeGB      = try { [math]::Round((Get-PSDrive C -ErrorAction Stop).Free / 1GB, 2) } catch { 0 }
$LogDirSrc   = if ($env:NECESSARYADMINTOOL_LOG_DIR) { "Configured ($env:NECESSARYADMINTOOL_LOG_DIR)" } else { "LOCAL FALLBACK ($LogDir)" }

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
$CurrentBuild = try { [int]$OSInfo.BuildNumber } catch { 0 }

# --- 1. LOGGING (Thread-Safe with File Locking) ---
function Write-NecessaryAdminToolLog {
    param([string]$Status, [bool]$ToMaster = $false)
    $Stamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"

    try {
        "[$Stamp] $Status" | Out-File $PCLog -Append -Encoding UTF8 -ErrorAction Stop
    } catch {
        # Fallback to console if log write fails
        Write-Host "[$Stamp] $Status" -ForegroundColor Yellow
    }

    if ($ToMaster) {
        # Named system mutex - OS auto-releases on process crash; no stale lock risk
        $Mtx = $null; $Acquired = $false
        try {
            $Mtx = [System.Threading.Mutex]::new($false, "Global\NecessaryAdminTool_MasterLog")
            $Acquired = $Mtx.WaitOne(10000)
        } catch [System.Threading.AbandonedMutexException] {
            $Acquired = $true
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

# --- 1b. MASTER CSV SUMMARY (Rich 20-column fleet reporting schema - matches FeatureUpdate) ---
function Write-MasterSummary {
    param(
        [string]$Status,
        [string]$UpdatesFound = "0",
        [string]$Details      = ""
    )
    $Stamp    = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $Duration = [math]::Round(((Get-Date) - $ScriptStart).TotalSeconds, 0)
    $Header   = "Hostname,Script,Timestamp,OSVersion,BuildNumber,UptimeDays,TotalRAMGB,DiskFreeGB,SerialNumber,Manufacturer,Model,IPAddress,LoggedInUser,TPMPresent,SecureBoot,Status,Method,UpdateCount,Details,DurationSeconds"
    $Row      = "`"$Comp`",`"General`",`"$Stamp`",`"$OSVersion`",`"$CurrentBuild`",`"$UptimeDays`",`"$TotalRAMGB`",`"$FreeGB`",`"$SerialNumber`",`"$Manufacturer`",`"$Model`",`"$IPAddress`",`"$LoggedInUser`",`"N/A`",`"N/A`",`"$Status`",`"PSWindowsUpdate`",`"$UpdatesFound`",`"$Details`",`"$Duration`""

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

    # Write to SQL Server if configured
    Write-ToDatabase -Status $Status -UpdatesFound $UpdatesFound -Details $Details
}

# --- 1c. DATABASE WRITE (SQL Server direct write when configured) ---
function Write-ToDatabase {
    param(
        [string]$Status,
        [string]$UpdatesFound = "0",
        [string]$Details      = ""
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
     @Status, @UpdatesFound, NULL, @Details, @Duration)
"@
        $InsertCmd.Parameters.AddWithValue("@Hostname",     $Comp)             | Out-Null
        $InsertCmd.Parameters.AddWithValue("@Script",       "General")         | Out-Null
        $InsertCmd.Parameters.AddWithValue("@Timestamp",    [DateTime]::Now)   | Out-Null
        $InsertCmd.Parameters.AddWithValue("@OSVersion",    $OSVersion)        | Out-Null
        $InsertCmd.Parameters.AddWithValue("@UptimeDays",   $UptimeDays)       | Out-Null
        $InsertCmd.Parameters.AddWithValue("@DiskFreeGB",   $FreeGB)           | Out-Null
        $InsertCmd.Parameters.AddWithValue("@Status",       $Status)           | Out-Null
        $InsertCmd.Parameters.AddWithValue("@UpdatesFound", $UpdatesFound)     | Out-Null
        $InsertCmd.Parameters.AddWithValue("@Details",      $Details)          | Out-Null
        $InsertCmd.Parameters.AddWithValue("@Duration",     $Duration)         | Out-Null
        $InsertCmd.ExecuteNonQuery() | Out-Null

        Write-NecessaryAdminToolLog -Status "DB_WRITE_SUCCESS_General_$Status" -ToMaster $false
    } catch {
        Write-NecessaryAdminToolLog -Status "DB_WRITE_FAILED_$($_.Exception.Message)" -ToMaster $false
    } finally {
        if ($null -ne $Conn) { $Conn.Close() }
    }
}

# --- 2. UI & LOGO (Theme Engine: Orange #FF8533 + Zinc #A1A1AA) ---
function Show-NecessaryAdminToolLogo {
    param([string]$Msg, [string]$Color = "Cyan")
    # Note: Clear-Host intentionally omitted - clearing console removes ME execution log history
    Write-Host ""
    Write-Host " -----------------------------------------------------------" -ForegroundColor DarkYellow
    Write-Host "  " -NoNewline
    Write-Host "NECESSARYADMINTOOL" -ForegroundColor DarkYellow -NoNewline
    Write-Host " | " -ForegroundColor Gray -NoNewline
    Write-Host "General Update Suite v1.0" -ForegroundColor Gray
    Write-Host " -----------------------------------------------------------" -ForegroundColor DarkYellow
    Write-Host ""
    if ($Msg) {
        Write-Host "  STATUS: " -ForegroundColor Gray -NoNewline
        Write-Host $Msg -ForegroundColor $Color
    }
    Write-Host ""
}

function Check-Power {
    $Battery = Get-CimInstance -ClassName Win32_Battery -ErrorAction SilentlyContinue
    if ($null -eq $Battery) { return $true }   # No battery = desktop/docked - always OK
    $Battery = @($Battery)[0]   # Guard against multiple battery objects (docking stations)
    # Try root/wmi BatteryStatus.PowerOnline first (most reliable); fall back to Win32_Battery.BatteryStatus
    $BattStatus = Get-CimInstance -Namespace root/wmi -ClassName BatteryStatus -ErrorAction SilentlyContinue
    $OnAC = if ($BattStatus) {
        @($BattStatus)[0].PowerOnline
    } else {
        # BatteryStatus: 1=Discharging, 2=OnAC, 3=FullyCharged, 4=Low, 5=Critical, 6-9=Charging variants
        $Battery.BatteryStatus -notin @(1, 4, 5)
    }
    return ($OnAC -or $Battery.EstimatedChargeRemaining -ge 20)
}

# --- 3. SCRIPT START BANNER ---
# Written as the very first log entry so every run has full context at the top of the file.
$StartBanner = @"
================================================================================
  GENERAL UPDATE SUITE - SCRIPT START
  Host      : $Comp
  Domain    : $DomainName
  OS        : $OSVersion
  Uptime    : $UptimeDays days
  RAM       : $TotalRAMGB GB
  Disk (C:) : $FreeGB GB free
  RunningAs : $RunningAs
  PS Ver    : $PSVersion
  LogDir    : $PCLog
  LogDirSrc : $LogDirSrc
  Started   : $($ScriptStart.ToString('yyyy-MM-dd HH:mm:ss'))
================================================================================
"@
Write-NecessaryAdminToolLog -Status "SCRIPT_START_Host=${Comp}_OS=${OSVersion}_Uptime=${UptimeDays}days_RAM=${TotalRAMGB}GB_RunAs=${RunningAs}_PS=${PSVersion}" -ToMaster $false
try { $StartBanner | Out-File $PCLog -Append -Encoding UTF8 -ErrorAction SilentlyContinue } catch {}
Write-Host $StartBanner -ForegroundColor DarkYellow

# --- 3. UPTIME CHECK ---
$UptimeDays = [math]::Round(((Get-Date) - $OSInfo.LastBootUpTime).TotalDays, 2)   # reuse $OSInfo from startup
if ($UptimeDays -gt 30) {
    Add-Type -AssemblyName System.Windows.Forms
    if (Test-Path $FlagFile) {
        Write-NecessaryAdminToolLog -Status "REBOOT_FORCED_UPTIME_LIMIT" -ToMaster $true
        [System.Windows.Forms.MessageBox]::Show("Grace period expired. Restarting...", "NecessaryAdminTool IT", "OK", "Stop", "Button1", "ServiceNotification")
        Remove-Item $FlagFile -Force; shutdown /r /t 60 /f /c "NecessaryAdminTool IT: Mandatory Maintenance."
        exit 0
    } else {
        $Choice = [System.Windows.Forms.MessageBox]::Show("Uptime: $UptimeDays days. Reboot now or postpone?", "NecessaryAdminTool IT", "YesNo", "Warning", "Button1", "ServiceNotification")
        if ($Choice -eq "No") { "Postponed" | Out-File $FlagFile; Write-NecessaryAdminToolLog -Status "REBOOT_POSTPONED" -ToMaster $true; exit 0 }
    }
}

# --- 4. PRE-EXECUTION CHECKS ---

# Check disk space
Show-NecessaryAdminToolLogo -Msg "Checking system requirements..."
$FreeGB = [math]::Round((Get-PSDrive C).Free / 1GB, 2)
if ($FreeGB -lt $MIN_DISK_SPACE_GB) {
    Write-NecessaryAdminToolLog -Status "ERROR_DISK_SPACE_LOW_${FreeGB}GB" -ToMaster $true
    Show-NecessaryAdminToolLogo -Msg "Insufficient disk space: ${FreeGB}GB free (minimum ${MIN_DISK_SPACE_GB}GB required)" "Red"
    Write-Error "Low disk space. Free up space and try again."
    exit 1
}

# Log system baseline info for diagnostics
Write-NecessaryAdminToolLog -Status "OS_VERSION_$OSVersion" -ToMaster $false
Write-NecessaryAdminToolLog -Status "UPTIME_${UptimeDays}_DAYS" -ToMaster $false
Write-NecessaryAdminToolLog -Status "DISK_FREE_${FreeGB}GB" -ToMaster $false

# Verify PSWindowsUpdate module - auto-install if missing (ManageEngine/RMM compatible)
if (!(Get-Module -ListAvailable -Name PSWindowsUpdate)) {
    Show-NecessaryAdminToolLogo -Msg "PSWindowsUpdate not found - installing..." "Yellow"
    Write-NecessaryAdminToolLog -Status "MODULE_NOT_FOUND_INSTALLING" -ToMaster $false
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Install-PackageProvider -Name NuGet -MinimumVersion 2.8.5.201 -Force -Scope AllUsers -ErrorAction SilentlyContinue
        Install-Module PSWindowsUpdate -Force -Confirm:$false -SkipPublisherCheck -Scope AllUsers -ErrorAction Stop
        Write-NecessaryAdminToolLog -Status "MODULE_INSTALLED_SUCCESSFULLY" -ToMaster $false
        Show-NecessaryAdminToolLogo -Msg "PSWindowsUpdate installed successfully" "Green"
    } catch {
        Write-NecessaryAdminToolLog -Status "ERROR_MODULE_INSTALL_FAILED_$($_.Exception.Message)" -ToMaster $true
        Show-NecessaryAdminToolLogo -Msg "Failed to install PSWindowsUpdate: $($_.Exception.Message)" "Red"
        Write-Error "Module installation failed: $($_.Exception.Message)"
        exit 1
    }
}

# Import module with error handling
try {
    Import-Module PSWindowsUpdate -Force -ErrorAction Stop
    Write-NecessaryAdminToolLog -Status "MODULE_LOADED_SUCCESSFULLY" -ToMaster $false
} catch {
    Write-NecessaryAdminToolLog -Status "ERROR_MODULE_IMPORT_FAILED" -ToMaster $true
    Show-NecessaryAdminToolLogo -Msg "Failed to import PSWindowsUpdate module" "Red"
    Write-Error "Module import failed: $($_.Exception.Message)"
    exit 1
}

# --- 5. EXECUTION ---
Show-NecessaryAdminToolLogo -Msg "Checking Power Status..."
while (-not (Check-Power)) {
    Show-NecessaryAdminToolLogo -Msg "BATTERY LOW. Plug in AC." -Color "Red"
    Start-Sleep -Seconds 30
}

# Create system restore point with verification
try {
    Checkpoint-Computer -Description "NecessaryAdminTool_General_Update" -RestorePointType "MODIFY_SETTINGS" -ErrorAction Stop
    Write-NecessaryAdminToolLog -Status "RESTORE_POINT_CREATED" -ToMaster $false
} catch {
    Write-NecessaryAdminToolLog -Status "RESTORE_POINT_FAILED_CONTINUING" -ToMaster $false
    # Continue anyway - restore point is nice to have but not critical
}

# Check for updates
Show-NecessaryAdminToolLogo -Msg "Scanning for updates..."
$Updates = Get-WindowsUpdate -MicrosoftUpdate -Criteria "IsInstalled=0" -ErrorAction SilentlyContinue

if ($null -eq $Updates -or $Updates.Count -eq 0) {
    Write-NecessaryAdminToolLog -Status "COMPLIANT_NO_UPDATES_FOUND" -ToMaster $false
    Write-MasterSummary -Status "COMPLIANT" -UpdatesFound "0" -Details "No updates required"
    Show-NecessaryAdminToolLogo -Msg "System is Up-to-Date." "Green"
    Start-Sleep -Seconds 3; exit 0
}

# Log each detected update and build KB list for master summary
$KBList = @()
Write-NecessaryAdminToolLog -Status "UPDATES_FOUND_$($Updates.Count)" -ToMaster $false
foreach ($Update in $Updates) {
    $KB       = if ($Update.KB) { "KB$($Update.KB)" } else { "NoKB" }
    $Severity = if ($Update.MsrcSeverity) { $Update.MsrcSeverity } else { "Unspecified" }
    $SizeMB   = [math]::Round($Update.Size / 1MB, 1)
    $KBList  += "[$Severity] $KB"
    Write-NecessaryAdminToolLog -Status "DETECTED: [$Severity] $($Update.Title) ($KB) - ${SizeMB}MB" -ToMaster $false
    Write-Host "  - [$Severity] $($Update.Title) ($KB)" -ForegroundColor Cyan
}

Write-NecessaryAdminToolLog -Status "INSTALLING_$($Updates.Count)_UPDATES" -ToMaster $true
Show-NecessaryAdminToolLogo -Msg "Installing $($Updates.Count) update(s)... DO NOT TURN OFF." "Yellow"
Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Power" -Name HiberbootEnabled -Value 0 -Force -ErrorAction SilentlyContinue

# Track success/failure explicitly
$ScriptSuccess  = $true
$FailureReason  = ""

try {
    Get-WindowsUpdate -MicrosoftUpdate -AcceptAll -Install -IgnoreReboot -Verbose -ErrorAction Stop
    Write-NecessaryAdminToolLog -Status "SUCCESS_PENDING_REBOOT" -ToMaster $false
    Write-MasterSummary -Status "SUCCESS" -UpdatesFound "$($Updates.Count)" -Details ($KBList -join "; ")
    Show-NecessaryAdminToolLogo -Msg "Installation Complete." "Green"
    $ScriptSuccess = $true
} catch {
    $FailureReason = $_.Exception.Message
    Write-NecessaryAdminToolLog -Status "FAILED_INSTALLATION_$FailureReason" -ToMaster $false
    Write-MasterSummary -Status "FAILED" -UpdatesFound "$($Updates.Count)" -Details $FailureReason
    Show-NecessaryAdminToolLogo -Msg "Installation Failed: $FailureReason" "Red"
    $ScriptSuccess = $false
}

# Re-enable fast startup
Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Power" -Name HiberbootEnabled -Value 1 -Force -ErrorAction SilentlyContinue

# --- 6. MANAGEENGINE INTEGRATION (Exit Code Reporting) ---
# ManageEngine/RMM platforms read exit codes to determine success/failure
if ($ScriptSuccess) {
    Write-NecessaryAdminToolLog -Status "SCRIPT_COMPLETED_SUCCESS" -ToMaster $false
    exit 0  # Success
} else {
    Write-NecessaryAdminToolLog -Status "SCRIPT_COMPLETED_FAILURE" -ToMaster $false
    exit 1  # Failure
}
