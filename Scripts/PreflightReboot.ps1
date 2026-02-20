#Requires -Version 5.1
# TAG: #DEPLOYMENT #PREFLIGHT #MANAGEENGINE
<#
.SYNOPSIS
    Pre-flight reboot check for NecessaryAdminTool Feature Update deployment.
    Deploy this via ManageEngine BEFORE FeatureUpdate.ps1.

.DESCRIPTION
    Checks three pending reboot conditions (Windows Update, Component Based Servicing,
    PendingFileRenameOperations). If any are found, the machine is rebooted automatically.
    Exit 0 in all cases so ManageEngine marks the task complete.

    Recommended ME deployment order:
        Step 1: NecessaryAdminTool_PreflightReboot.ps1  (this script)
        Step 2: NecessaryAdminTool_FeatureUpdate.ps1    (after machines are back online)
#>

# ============================================================
# CONFIGURATION (injected by NecessaryAdminTool on download)
# ============================================================
$LogDir              = $env:NECESSARYADMINTOOL_LOG_DIR

# ============================================================
# SETUP
# ============================================================
$Hostname   = $env:COMPUTERNAME
$StartTime  = Get-Date
$ScriptVer  = "1.0"

# Resolve log directory
if ([string]::IsNullOrEmpty($LogDir)) {
    $LogDir = "$env:TEMP\NecessaryAdminTool_Logs\Individual_PC_Logs"
}
if (-not (Test-Path $LogDir -ErrorAction SilentlyContinue)) {
    New-Item -ItemType Directory -Path $LogDir -Force -ErrorAction SilentlyContinue | Out-Null
}
$LogFile = Join-Path $LogDir "${Hostname}_Preflight.txt"

function Write-Log {
    param([string]$Status, [string]$Detail = "")
    $Line = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') | $Hostname | PREFLIGHT | $Status"
    if ($Detail) { $Line += " | $Detail" }
    Write-Host $Line
    try { $Line | Out-File $LogFile -Append -Encoding UTF8 -ErrorAction SilentlyContinue } catch {}
}

# ============================================================
# PENDING REBOOT DETECTION
# ============================================================
function Test-PendingReboot {
    # Windows Update reboot required
    if (Test-Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired" `
            -ErrorAction SilentlyContinue) { return "WindowsUpdate" }

    # Component Based Servicing (CBS) reboot pending
    if (Test-Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending" `
            -ErrorAction SilentlyContinue) { return "CBS" }

    # Pending file rename operations (installer cleanup)
    $PFR = (Get-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager" `
                -Name PendingFileRenameOperations -ErrorAction SilentlyContinue).PendingFileRenameOperations
    if ($null -ne $PFR -and @($PFR).Count -gt 0) { return "PendingFileRename" }

    return $null
}

# ============================================================
# MAIN
# ============================================================
Write-Host ""
Write-Host "================================================================================" -ForegroundColor DarkYellow
Write-Host "  NECESSARYADMINTOOL | Pre-flight Reboot Check v$ScriptVer" -ForegroundColor DarkYellow
Write-Host "  Host: $Hostname  |  Started: $($StartTime.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor Gray
Write-Host "================================================================================" -ForegroundColor DarkYellow
Write-Host ""

Write-Log -Status "STARTED"

$RebootReason = Test-PendingReboot

if ($null -ne $RebootReason) {
    Write-Host "  Pending reboot detected ($RebootReason) - rebooting to clear before upgrade." -ForegroundColor Yellow
    Write-Log -Status "PENDING_REBOOT_DETECTED" -Detail $RebootReason
    Write-Log -Status "REBOOTING" -Detail "shutdown /r /t 60 - ManageEngine task will show Success (exit 0)"

    Write-Host ""
    Write-Host "  Rebooting in 60 seconds to clear pending state." -ForegroundColor Cyan
    Write-Host "  After reboot, push NecessaryAdminTool_FeatureUpdate.ps1 to proceed with upgrade." -ForegroundColor Cyan
    Write-Host ""

    shutdown.exe /r /t 60 /c "NecessaryAdminTool: Applying pending updates before Windows 11 upgrade. System will restart in 60 seconds."
    exit 0
}

Write-Host "  No pending reboot detected - machine is ready for Feature Update." -ForegroundColor Green
Write-Log -Status "NO_PENDING_REBOOT_MACHINE_READY"
Write-Host ""
exit 0
