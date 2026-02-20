#Requires -Version 5.1
# TAG: #DEPLOYMENT #PREFLIGHT #MANAGEENGINE
<#
.SYNOPSIS
    Pre-flight reboot check for NecessaryAdminTool Feature Update deployment.
    Deploy this via ManageEngine BEFORE FeatureUpdate.ps1.

.DESCRIPTION
    Checks for pending reboot conditions (Windows Update, CBS, PendingFileRename).

    If a pending reboot is detected:
      - No user logged in  -> forced reboot after 60-second console countdown
      - User logged in     -> 20-minute WinForms warning dialog, then forced reboot

    Exit 0 in all cases so ManageEngine marks the task complete.
    Push FeatureUpdate.ps1 after machines are back online.
#>

# ============================================================
# CONFIGURATION (injected by NecessaryAdminTool on download)
# ============================================================
$LogDir              = $env:NECESSARYADMINTOOL_LOG_DIR

# ============================================================
# SETUP
# ============================================================
$Hostname       = $env:COMPUTERNAME
$StartTime      = Get-Date
$ScriptVer      = "1.1"
$WarningMinutes = 20

if ([string]::IsNullOrEmpty($LogDir)) {
    $LogDir = "$env:TEMP\NecessaryAdminTool_Logs\Individual_PC_Logs"
}
if (-not (Test-Path $LogDir -ErrorAction SilentlyContinue)) {
    New-Item -ItemType Directory -Path $LogDir -Force -ErrorAction SilentlyContinue | Out-Null
}
$LogFile = Join-Path $LogDir "${Hostname}_Preflight.txt"

function Write-Log {
    param([string]$Status, [string]$Detail = "")
    $Line = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') | $Hostname | PREFLIGHT v$ScriptVer | $Status"
    if ($Detail) { $Line += " | $Detail" }
    Write-Host $Line
    try { $Line | Out-File $LogFile -Append -Encoding UTF8 -ErrorAction SilentlyContinue } catch {}
}

# ============================================================
# HELPER FUNCTIONS
# ============================================================
function Test-PendingReboot {
    if (Test-Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired" `
            -ErrorAction SilentlyContinue) { return "WindowsUpdate" }
    if (Test-Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending" `
            -ErrorAction SilentlyContinue) { return "CBS" }
    $PFR = (Get-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager" `
                -Name PendingFileRenameOperations -ErrorAction SilentlyContinue).PendingFileRenameOperations
    if ($null -ne $PFR -and @($PFR).Count -gt 0) { return "PendingFileRename" }
    return $null
}

function Test-UserLoggedIn {
    $Sessions = Get-Process -Name explorer -ErrorAction SilentlyContinue |
        Where-Object { $_.SessionId -ne 0 }
    return ($null -ne $Sessions -and @($Sessions).Count -gt 0)
}

function Show-RebootWarningDialog {
    param([int]$Minutes = 20)

    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing

    $TotalSeconds   = $Minutes * 60
    $script:Elapsed = 0
    $script:Restart = $false

    $Form                  = New-Object System.Windows.Forms.Form
    $Form.Text             = "NecessaryAdminTool IT - Restart Required"
    $Form.Size             = New-Object System.Drawing.Size(520, 280)
    $Form.StartPosition    = "CenterScreen"
    $Form.BackColor        = [System.Drawing.Color]::FromArgb(26, 26, 26)
    $Form.ForeColor        = [System.Drawing.Color]::White
    $Form.FormBorderStyle  = "FixedDialog"
    $Form.MaximizeBox      = $false
    $Form.MinimizeBox      = $false
    $Form.ControlBox       = $false
    $Form.TopMost          = $true

    # Header label
    $LblTitle              = New-Object System.Windows.Forms.Label
    $LblTitle.Text         = "  IT Department - System Restart Required"
    $LblTitle.Font         = New-Object System.Drawing.Font("Segoe UI", 11, [System.Drawing.FontStyle]::Bold)
    $LblTitle.ForeColor    = [System.Drawing.Color]::FromArgb(255, 133, 51)
    $LblTitle.BackColor    = [System.Drawing.Color]::FromArgb(40, 40, 40)
    $LblTitle.Dock         = "Top"
    $LblTitle.Height       = 36
    $LblTitle.TextAlign    = "MiddleLeft"
    $Form.Controls.Add($LblTitle)

    # Body text
    $LblBody               = New-Object System.Windows.Forms.Label
    $LblBody.Text          = "Your IT team needs to restart this computer to apply pending`n" +
                             "Windows updates before a scheduled OS upgrade.`n`n" +
                             "PLEASE SAVE YOUR WORK NOW.`n`n" +
                             "Your computer will restart automatically when the timer reaches zero."
    $LblBody.Font          = New-Object System.Drawing.Font("Segoe UI", 9)
    $LblBody.ForeColor     = [System.Drawing.Color]::FromArgb(200, 200, 200)
    $LblBody.Location      = New-Object System.Drawing.Point(20, 52)
    $LblBody.Size          = New-Object System.Drawing.Size(480, 110)
    $Form.Controls.Add($LblBody)

    # Countdown label
    $LblTimer              = New-Object System.Windows.Forms.Label
    $LblTimer.Text         = "Restarting in  $Minutes:00"
    $LblTimer.Font         = New-Object System.Drawing.Font("Segoe UI", 13, [System.Drawing.FontStyle]::Bold)
    $LblTimer.ForeColor    = [System.Drawing.Color]::FromArgb(255, 210, 80)
    $LblTimer.Location     = New-Object System.Drawing.Point(20, 168)
    $LblTimer.Size         = New-Object System.Drawing.Size(300, 30)
    $Form.Controls.Add($LblTimer)

    # Restart Now button
    $BtnNow                = New-Object System.Windows.Forms.Button
    $BtnNow.Text           = "Restart Now"
    $BtnNow.Font           = New-Object System.Drawing.Font("Segoe UI", 10, [System.Drawing.FontStyle]::Bold)
    $BtnNow.BackColor      = [System.Drawing.Color]::FromArgb(255, 133, 51)
    $BtnNow.ForeColor      = [System.Drawing.Color]::White
    $BtnNow.FlatStyle      = "Flat"
    $BtnNow.Size           = New-Object System.Drawing.Size(140, 34)
    $BtnNow.Location       = New-Object System.Drawing.Point(350, 162)
    $BtnNow.Add_Click({
        $script:Restart = $true
        $Form.Close()
    })
    $Form.Controls.Add($BtnNow)

    # Timer - fires every second
    $Timer          = New-Object System.Windows.Forms.Timer
    $Timer.Interval = 1000
    $Timer.Add_Tick({
        $script:Elapsed++
        $Remaining = $TotalSeconds - $script:Elapsed
        if ($Remaining -le 0) {
            $script:Restart = $true
            $Timer.Stop()
            $Form.Close()
            return
        }
        $Mins = [math]::Floor($Remaining / 60)
        $Secs = $Remaining % 60
        $LblTimer.Text = "Restarting in  $($Mins):$($Secs.ToString('00'))"
        # Turn label red in final 2 minutes
        if ($Remaining -le 120) {
            $LblTimer.ForeColor = [System.Drawing.Color]::FromArgb(255, 80, 80)
        }
    })
    $Timer.Start()

    try { $Form.ShowDialog() | Out-Null }
    catch { <# Session 0 / no desktop - form invisible, timer expires, restart proceeds #> }
    finally { $Timer.Stop(); $Timer.Dispose(); $Form.Dispose() }
}

# ============================================================
# MAIN
# ============================================================
Write-Host ""
Write-Host "--------------------------------------------------------------------------------" -ForegroundColor DarkYellow
Write-Host "  NECESSARYADMINTOOL | Pre-flight Reboot Check v$ScriptVer" -ForegroundColor DarkYellow
Write-Host "  Host    : $Hostname" -ForegroundColor Gray
Write-Host "  Started : $($StartTime.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor Gray
Write-Host "  RunAs   : $([System.Security.Principal.WindowsIdentity]::GetCurrent().Name)" -ForegroundColor Gray
Write-Host "--------------------------------------------------------------------------------" -ForegroundColor DarkYellow
Write-Host ""

Write-Log -Status "STARTED"

$RebootReason = Test-PendingReboot

if ($null -eq $RebootReason) {
    Write-Host "  No pending reboot detected - machine is ready for Feature Update." -ForegroundColor Green
    Write-Log -Status "NO_PENDING_REBOOT_MACHINE_READY"
    Write-Host ""
    exit 0
}

Write-Host "  Pending reboot detected ($RebootReason) - restart required before upgrade." -ForegroundColor Yellow
Write-Log -Status "PENDING_REBOOT_DETECTED" -Detail $RebootReason

$UserPresent = Test-UserLoggedIn
Write-Host "  Interactive user logged in: $UserPresent" -ForegroundColor Cyan
Write-Log -Status "USER_SESSION_PRESENT_$UserPresent"

if (!$UserPresent) {
    # ---- UNATTENDED: reboot immediately with a short console countdown ----
    Write-Host ""
    Write-Host "  No user logged in - rebooting in 60 seconds to clear pending state." -ForegroundColor Yellow
    Write-Host "  Push FeatureUpdate.ps1 after this machine comes back online." -ForegroundColor Cyan
    Write-Log -Status "UNATTENDED_REBOOTING_IN_60S"

    shutdown.exe /r /t 60 /c "NecessaryAdminTool: Applying pending updates before Windows 11 upgrade. System restarts in 60 seconds."
    exit 0

} else {
    # ---- USER PRESENT: WinForms dialog + per-minute console countdown ----
    Write-Host ""
    Write-Host "  User is logged in - showing $WarningMinutes-minute restart warning dialog..." -ForegroundColor Yellow
    Write-Log -Status "USER_PRESENT_SHOWING_${WarningMinutes}MIN_REBOOT_DIALOG"

    Show-RebootWarningDialog -Minutes $WarningMinutes

    Write-Host "  Dialog closed. Starting $WarningMinutes-minute work-save countdown..." -ForegroundColor Yellow
    Write-Log -Status "REBOOT_COUNTDOWN_STARTED_${WarningMinutes}MIN"

    # Per-minute console countdown - keeps ME execution log alive and gives visible progress
    for ($Min = $WarningMinutes; $Min -gt 0; $Min--) {
        $Elapsed = $WarningMinutes - $Min
        Write-Host "  [$Min min remaining | $Elapsed min elapsed | $(Get-Date -Format 'HH:mm:ss')] Save your work - restart incoming." -ForegroundColor Yellow
        Write-Log -Status "REBOOT_COUNTDOWN_${Min}MIN_REMAINING"
        Start-Sleep -Seconds 60
    }

    Write-Host ""
    Write-Host "  [COUNTDOWN COMPLETE] $WarningMinutes minutes elapsed. Issuing restart now." -ForegroundColor Green
    Write-Log -Status "REBOOT_COUNTDOWN_COMPLETE_ISSUING_RESTART"

    shutdown.exe /r /t 30 /c "NecessaryAdminTool: Restarting to apply pending updates before Windows 11 upgrade."
    exit 0
}
