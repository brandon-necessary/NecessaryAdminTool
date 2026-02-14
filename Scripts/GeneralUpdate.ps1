# ==============================================================================
# ARTAZN IT - GENERAL UPDATE SUITE (v1.0 - ArtaznIT v4.0 Compatible)
# Includes: Windows Updates, Firmware, Uptime Guard, Power Check, ManageEngine Compatible
# ==============================================================================

# --- PRE-FLIGHT GUARD ---
if ($PSVersionTable.PSVersion.Major -lt 5) {
    $MasterLog = "\\Jzppdm\sys\PUBLIC\BNIT\01_Software\04_Update Logs\Master_Update_Log.csv"
    $Line = "$($env:COMPUTERNAME),FAILED_VERSION_CHECK,$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    Add-Content $MasterLog $Line -ErrorAction SilentlyContinue
    Write-Error "CRITICAL: PowerShell 5.1 or higher is required."
    return
}

# --- 0. CONFIGURABLE PATHS (ManageEngine Compatible) ---
# Default network paths - can be overridden by ManageEngine custom fields
$LogDir      = if ($env:ARTAZN_LOG_DIR) { $env:ARTAZN_LOG_DIR } else { "\\Jzppdm\sys\PUBLIC\BNIT\01_Software\04_Update Logs" }
$MasterLog   = "$LogDir\Master_Update_Log.csv"
$PCLogDir    = "$LogDir\Individual_PC_Logs"
$PCArchive   = "$PCLogDir\Archived_PC_Logs"
$PCLog       = "$PCLogDir\$($env:COMPUTERNAME)_General.txt"
$FlagFile    = "C:\Windows\Temp\Artazn_Uptime_Flag.txt"
$Comp        = $env:COMPUTERNAME

# Maintenance
Remove-Item "C:\Windows\Temp\*" -Recurse -Force -ErrorAction SilentlyContinue
if (Test-Path $PCLogDir) {
    Get-ChildItem -Path $PCLogDir -Filter "*.txt" | Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-30) } | Move-Item -Destination $PCArchive -Force -ErrorAction SilentlyContinue
}

# --- 1. LOGGING (Locked Schema) ---
function Write-ArtaznLog {
    param([string]$Status, [bool]$ToMaster = $false)
    $Stamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    "[$Stamp] $Status" | Out-File $PCLog -Append -Encoding UTF8

    if ($ToMaster) {
        $LockFile = "$MasterLog.lock"
        $TimeOut  = 0
        while ((Test-Path $LockFile) -and ($TimeOut -lt 50)) { Start-Sleep -Milliseconds 200; $TimeOut++ }
        try {
            New-Item -ItemType File -Path $LockFile -Force -ErrorAction SilentlyContinue | Out-Null
            "$Comp,$Status,$Stamp" | Add-Content $MasterLog -Force
        }
        catch { "[$Stamp] ERROR: Master Log Write Failed" | Out-File $PCLog -Append }
        finally { Remove-Item $LockFile -ErrorAction SilentlyContinue }
    }
}

# --- 2. UI & LOGO (Artazn Branded) ---
function Show-ArtaznLogo {
    param([string]$Msg, [string]$Color = "DarkYellow")
    Clear-Host
    # Artazn ASCII Art in Orange Theme
    Write-Host " ╔══════════════════════════════════════════════════════════════════════╗" -ForegroundColor DarkYellow
    Write-Host " ║                                                                      ║" -ForegroundColor DarkYellow
    Write-Host " ║     █████╗ ██████╗ ████████╗ █████╗ ███████╗███╗   ██╗            ║" -ForegroundColor DarkYellow
    Write-Host " ║    ██╔══██╗██╔══██╗╚══██╔══╝██╔══██╗╚══███╔╝████╗  ██║            ║" -ForegroundColor DarkYellow
    Write-Host " ║    ███████║██████╔╝   ██║   ███████║  ███╔╝ ██╔██╗ ██║            ║" -ForegroundColor DarkYellow
    Write-Host " ║    ██╔══██║██╔══██╗   ██║   ██╔══██║ ███╔╝  ██║╚██╗██║            ║" -ForegroundColor DarkYellow
    Write-Host " ║    ██║  ██║██║  ██║   ██║   ██║  ██║███████╗██║ ╚████║            ║" -ForegroundColor DarkYellow
    Write-Host " ║    ╚═╝  ╚═╝╚═╝  ╚═╝   ╚═╝   ╚═╝  ╚═╝╚══════╝╚═╝  ╚═══╝            ║" -ForegroundColor DarkYellow
    Write-Host " ║                                                                      ║" -ForegroundColor DarkYellow
    Write-Host " ║                   🔧 GENERAL UPDATE SUITE v1.0                      ║" -ForegroundColor DarkYellow
    Write-Host " ╚══════════════════════════════════════════════════════════════════════╝" -ForegroundColor DarkYellow
    Write-Host ""
    if ($Msg) { Write-Host " STATUS: $Msg" -ForegroundColor $Color }
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
        Write-ArtaznLog -Status "REBOOT_FORCED_UPTIME_LIMIT" -ToMaster $true
        [System.Windows.Forms.MessageBox]::Show("Grace period expired. Restarting...", "Artazn IT", "OK", "Stop", "Button1", "ServiceNotification")
        Remove-Item $FlagFile -Force; shutdown /r /t 60 /f /c "Artazn IT: Mandatory Maintenance."
        exit
    } else {
        $Choice = [System.Windows.Forms.MessageBox]::Show("Uptime: $UptimeDays days. Reboot now or postpone?", "Artazn IT", "YesNo", "Warning", "Button1", "ServiceNotification")
        if ($Choice -eq "No") { "Postponed" | Out-File $FlagFile; Write-ArtaznLog -Status "REBOOT_POSTPONED" -ToMaster $true; exit }
    }
}

# --- 4. EXECUTION ---
Show-ArtaznLogo -Msg "Checking Power Status..."
while (-not (Check-Power)) { Show-ArtaznLogo -Msg "BATTERY LOW. Plug in AC." -Color "Red"; Start-Sleep -Seconds 30 }

Checkpoint-Computer -Description "Artazn_General_Update" -RestorePointType "MODIFY_SETTINGS" -ErrorAction SilentlyContinue
Import-Module PSWindowsUpdate -Force
$Updates = Get-WindowsUpdate -MicrosoftUpdate -Criteria "IsInstalled=0" -ErrorAction SilentlyContinue

if ($null -eq $Updates -or $Updates.Count -eq 0) {
    Write-ArtaznLog -Status "COMPLIANT" -ToMaster $true
    Show-ArtaznLogo -Msg "System is Up-to-Date." "Green"
    Start-Sleep -Seconds 3; exit
}

Write-ArtaznLog -Status "INSTALLING_UPDATES" -ToMaster $true
Show-ArtaznLogo -Msg "Installing Updates... DO NOT TURN OFF." "Yellow"
Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Power" -Name HiberbootEnabled -Value 0 -Force

try {
    Get-WindowsUpdate -MicrosoftUpdate -AcceptAll -Install -IgnoreReboot -Verbose
    Write-ArtaznLog -Status "SUCCESS_PENDING_REBOOT" -ToMaster $true
    Show-ArtaznLogo -Msg "Installation Complete." "Green"
} catch {
    Write-ArtaznLog -Status "FAILED_INSTALLATION" -ToMaster $true
    Show-ArtaznLogo -Msg "Installation Failed." "Red"
}
Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Power" -Name HiberbootEnabled -Value 1 -Force

# --- 5. MANAGEENGINE INTEGRATION (Exit Code Reporting) ---
# ManageEngine reads exit codes to determine success/failure
if ($LASTEXITCODE -eq 0 -or $null -eq $LASTEXITCODE) {
    exit 0  # Success
} else {
    exit 1  # Failure
}
