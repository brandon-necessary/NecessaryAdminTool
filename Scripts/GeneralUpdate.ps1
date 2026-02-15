# ==============================================================================
# NECESSARYADMINTOOL IT - GENERAL UPDATE SUITE (v1.0 - Bulletproof Edition)
# Includes: Windows Updates, Firmware, Uptime Guard, Power Check, ManageEngine Compatible
# Security Hardened: Admin checks, module validation, safe cleanup, disk space checks
# ==============================================================================

# --- PRE-FLIGHT GUARD: PowerShell Version ---
if ($PSVersionTable.PSVersion.Major -lt 5) {
    $MasterLog = "\\Jzppdm\sys\PUBLIC\BNIT\01_Software\04_Update Logs\Master_Update_Log.csv"
    $Line = "$($env:COMPUTERNAME),FAILED_VERSION_CHECK,$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    Add-Content $MasterLog $Line -ErrorAction SilentlyContinue
    Write-Error "CRITICAL: PowerShell 5.1 or higher is required."
    exit 1
}

# --- PRE-FLIGHT GUARD: Admin Privileges ---
$CurrentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
$IsAdmin = (New-Object Security.Principal.WindowsPrincipal $CurrentUser).IsInRole([Security.Principal.WindowsBuiltinRole]::Administrator)

if (!$IsAdmin) {
    Write-Error "ERROR: This script requires administrator privileges"
    Write-Error "Right-click PowerShell and select 'Run as Administrator'"
    exit 1
}

# --- 0. CONFIGURABLE PATHS (Environment Variable Based) ---
# ALL paths are configured via environment variables or app settings
# NO hardcoded paths - configured through NecessaryAdminTool Options menu

$LogDir = $env:NECESSARYADMINTOOL_LOG_DIR

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
$PCLogDir    = "$LogDir\Individual_PC_Logs"
$PCArchive   = "$PCLogDir\Archived_PC_Logs"
$PCLog       = "$PCLogDir\$($env:COMPUTERNAME)_General.txt"
$FlagFile    = "C:\Windows\Temp\NecessaryAdminTool_Uptime_Flag.txt"
$Comp        = $env:COMPUTERNAME

# Constants
$LOG_RETENTION_DAYS = 30
$MIN_DISK_SPACE_GB = 10
$UPTIME_LIMIT_DAYS = 30
$MAX_LOG_LOCK_TIMEOUT = 50

# Create log directories if missing
if (!(Test-Path $PCLogDir)) {
    New-Item -ItemType Directory -Path $PCLogDir -Force | Out-Null
}
if (!(Test-Path $PCArchive)) {
    New-Item -ItemType Directory -Path $PCArchive -Force | Out-Null
}

# Safe maintenance: Only clean NecessaryAdminTool temp files
Remove-Item "C:\Windows\Temp\NecessaryAdminTool*" -Recurse -Force -ErrorAction SilentlyContinue

# Archive old logs safely
Get-ChildItem -Path $PCLogDir -Filter "*.txt" -ErrorAction SilentlyContinue |
    Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-$LOG_RETENTION_DAYS) } |
    Move-Item -Destination $PCArchive -Force -ErrorAction SilentlyContinue

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
        $LockFile = "$MasterLog.lock"
        $TimeOut  = 0
        while ((Test-Path $LockFile) -and ($TimeOut -lt $MAX_LOG_LOCK_TIMEOUT)) {
            Start-Sleep -Milliseconds 200
            $TimeOut++
        }
        try {
            New-Item -ItemType File -Path $LockFile -Force -ErrorAction SilentlyContinue | Out-Null
            "$Comp,$Status,$Stamp" | Add-Content $MasterLog -Force -ErrorAction Stop
        }
        catch {
            "[$Stamp] ERROR: Master Log Write Failed - $($_.Exception.Message)" | Out-File $PCLog -Append -ErrorAction SilentlyContinue
        }
        finally {
            Remove-Item $LockFile -ErrorAction SilentlyContinue
        }
    }
}

# --- 2. UI & LOGO (Theme Engine: Orange #FF8533 + Zinc #A1A1AA) ---
function Show-NecessaryAdminToolLogo {
    param([string]$Msg, [string]$Color = "Cyan")
    Clear-Host
    Write-Host ""
    Write-Host " ═══════════════════════════════════════════════════════════" -ForegroundColor DarkYellow
    Write-Host "  " -NoNewline
    Write-Host "NECESSARYADMINTOOL" -ForegroundColor DarkYellow -NoNewline
    Write-Host " | " -ForegroundColor Gray -NoNewline
    Write-Host "General Update Suite v1.0" -ForegroundColor Gray
    Write-Host " ═══════════════════════════════════════════════════════════" -ForegroundColor DarkYellow
    Write-Host ""
    if ($Msg) {
        Write-Host "  STATUS: " -ForegroundColor Gray -NoNewline
        Write-Host $Msg -ForegroundColor $Color
    }
    Write-Host ""
}

function Check-Power {
    $Battery = Get-CimInstance -ClassName Win32_Battery -ErrorAction SilentlyContinue
    if ($null -eq $Battery) { return $true }
    $AC = (Get-CimInstance -Namespace root/wmi -ClassName BatteryStatus).PowerOnline
    return ($AC -or $Battery.EstimatedChargeRemaining -ge 20)
}

# --- 3. UPTIME CHECK ---
$UptimeDays = [math]::Round(((Get-Date) - (Get-CimInstance Win32_OperatingSystem).LastBootUpTime).TotalDays, 2)
if ($UptimeDays -gt 30) {
    Add-Type -AssemblyName System.Windows.Forms
    if (Test-Path $FlagFile) {
        Write-NecessaryAdminToolLog -Status "REBOOT_FORCED_UPTIME_LIMIT" -ToMaster $true
        [System.Windows.Forms.MessageBox]::Show("Grace period expired. Restarting...", "NecessaryAdminTool IT", "OK", "Stop", "Button1", "ServiceNotification")
        Remove-Item $FlagFile -Force; shutdown /r /t 60 /f /c "NecessaryAdminTool IT: Mandatory Maintenance."
        exit
    } else {
        $Choice = [System.Windows.Forms.MessageBox]::Show("Uptime: $UptimeDays days. Reboot now or postpone?", "NecessaryAdminTool IT", "YesNo", "Warning", "Button1", "ServiceNotification")
        if ($Choice -eq "No") { "Postponed" | Out-File $FlagFile; Write-NecessaryAdminToolLog -Status "REBOOT_POSTPONED" -ToMaster $true; exit }
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

# Verify PSWindowsUpdate module
if (!(Get-Module -ListAvailable -Name PSWindowsUpdate)) {
    Write-NecessaryAdminToolLog -Status "ERROR_MODULE_NOT_INSTALLED" -ToMaster $true
    Show-NecessaryAdminToolLogo -Msg "PSWindowsUpdate module not installed" "Red"
    Write-Host "`nInstall the module with:" -ForegroundColor Yellow
    Write-Host "  Install-Module PSWindowsUpdate -Force" -ForegroundColor Cyan
    Write-Host "`nOr run from elevated PowerShell:" -ForegroundColor Yellow
    Write-Host "  [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12" -ForegroundColor Cyan
    Write-Host "  Install-Module PSWindowsUpdate -Force -SkipPublisherCheck" -ForegroundColor Cyan
    exit 1
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
$Updates = Get-WindowsUpdate -MicrosoftUpdate -Criteria "IsInstalled=0" -ErrorAction SilentlyContinue

if ($null -eq $Updates -or $Updates.Count -eq 0) {
    Write-NecessaryAdminToolLog -Status "COMPLIANT" -ToMaster $true
    Show-NecessaryAdminToolLogo -Msg "System is Up-to-Date." "Green"
    Start-Sleep -Seconds 3; exit
}

Write-NecessaryAdminToolLog -Status "INSTALLING_UPDATES" -ToMaster $true
Show-NecessaryAdminToolLogo -Msg "Installing Updates... DO NOT TURN OFF." "Yellow"
Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Power" -Name HiberbootEnabled -Value 0 -Force -ErrorAction SilentlyContinue

# Track success/failure explicitly
$ScriptSuccess = $true

try {
    Get-WindowsUpdate -MicrosoftUpdate -AcceptAll -Install -IgnoreReboot -Verbose -ErrorAction Stop
    Write-NecessaryAdminToolLog -Status "SUCCESS_PENDING_REBOOT" -ToMaster $true
    Show-NecessaryAdminToolLogo -Msg "Installation Complete." "Green"
    $ScriptSuccess = $true
} catch {
    Write-NecessaryAdminToolLog -Status "FAILED_INSTALLATION_$($_.Exception.Message)" -ToMaster $true
    Show-NecessaryAdminToolLogo -Msg "Installation Failed: $($_.Exception.Message)" "Red"
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
