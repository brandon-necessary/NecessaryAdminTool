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

# Enterprise standard: fail loudly on unexpected errors; use -ErrorAction SilentlyContinue where fallback is intentional
$ErrorActionPreference = 'Stop'

# ============================================================
# CONFIGURATION (injected by NecessaryAdminTool on download)
# ============================================================
$LogDir              = $env:NECESSARYADMINTOOL_LOG_DIR
$DatabaseType        = ""  # NAT_INJECT_DB_TYPE
$SqlConnectionString = ""  # NAT_INJECT_SQL_CONN

# ============================================================
# SETUP
# ============================================================
$Hostname       = $env:COMPUTERNAME
$StartTime      = Get-Date
$Timestamp      = Get-Date -Format 'yyyy-MM-dd_HH-mm'
$ScriptVer      = "1.2"
$WarningMinutes = 20

# Gather system context early - used in startup banner and all log entries
$PSVersion   = "$($PSVersionTable.PSVersion.Major).$($PSVersionTable.PSVersion.Minor)"
$RunningAs   = [Security.Principal.WindowsIdentity]::GetCurrent().Name
$OSInfo      = Get-CimInstance Win32_OperatingSystem -ErrorAction SilentlyContinue
$OSVersion   = if ($OSInfo) { $OSInfo.Caption } else { "Unknown" }
$UptimeDays  = if ($OSInfo) { [math]::Round(((Get-Date) - $OSInfo.LastBootUpTime).TotalDays, 2) } else { 0 }
$SysInfo     = Get-CimInstance Win32_ComputerSystem -ErrorAction SilentlyContinue
$DomainName  = if ($SysInfo) { $SysInfo.Domain } else { "Unknown" }
$TotalRAMGB  = if ($SysInfo) { [math]::Round($SysInfo.TotalPhysicalMemory / 1GB, 2) } else { 0 }
$FreeGB      = try { [math]::Round((Get-PSDrive C -ErrorAction Stop).Free / 1GB, 2) } catch { 0 }

if ([string]::IsNullOrEmpty($LogDir)) {
    $LogDir = "$env:TEMP\NecessaryAdminTool_Logs"
}
$LogDirSrc = if ($env:NECESSARYADMINTOOL_LOG_DIR) { "Configured ($env:NECESSARYADMINTOOL_LOG_DIR)" } else { "LOCAL FALLBACK ($LogDir)" }

$PCLogDir = "$LogDir\Individual_PC_Logs"
if (-not (Test-Path $PCLogDir -ErrorAction SilentlyContinue)) {
    New-Item -ItemType Directory -Path $PCLogDir -Force -ErrorAction SilentlyContinue | Out-Null
}
$LogFile        = "$PCLogDir\${Hostname}_Preflight_${Timestamp}.txt"
$TranscriptPath = "$PCLogDir\${Hostname}_Preflight_${Timestamp}_Transcript.txt"

# Start transcript - captures all console output (same as FeatureUpdate/GeneralUpdate)
Start-Transcript -Path $TranscriptPath -Append -NoClobber -ErrorAction SilentlyContinue

function Write-Log {
    param([string]$Status, [string]$Detail = "")
    $Line = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') | $Hostname | PREFLIGHT v$ScriptVer | $Status"
    if ($Detail) { $Line += " | $Detail" }
    Write-Host $Line
    try { $Line | Out-File $LogFile -Append -Encoding UTF8 -ErrorAction SilentlyContinue } catch {}
}

# ============================================================
# DATABASE WRITE (SQL Server - optional, same table as Feature/General scripts)
# ============================================================
function Write-ToDatabase {
    param(
        [string]$Status,
        [string]$Details = ""
    )
    # Silently skip unless SQL Server is configured in NecessaryAdminTool
    if ([string]::IsNullOrEmpty($DatabaseType) -or
        $DatabaseType -ne "SqlServer" -or
        [string]::IsNullOrEmpty($SqlConnectionString)) {
        return
    }

    $Duration = [math]::Round(((Get-Date) - $StartTime).TotalSeconds, 0)
    $Conn = $null
    try {
        $Conn = New-Object System.Data.SqlClient.SqlConnection($SqlConnectionString)
        $Conn.Open()

        # Create table if it does not already exist (shared with Feature/General scripts)
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
     @Status, NULL, NULL, @Details, @Duration)
"@
        $InsertCmd.Parameters.AddWithValue("@Hostname",   $Hostname)         | Out-Null
        $InsertCmd.Parameters.AddWithValue("@Script",     "Preflight")       | Out-Null
        $InsertCmd.Parameters.AddWithValue("@Timestamp",  [DateTime]::Now)   | Out-Null
        $InsertCmd.Parameters.AddWithValue("@OSVersion",  $OSVersion)        | Out-Null
        $InsertCmd.Parameters.AddWithValue("@UptimeDays", $UptimeDays)       | Out-Null
        $InsertCmd.Parameters.AddWithValue("@DiskFreeGB", $FreeGB)           | Out-Null
        $InsertCmd.Parameters.AddWithValue("@Status",     $Status)           | Out-Null
        $InsertCmd.Parameters.AddWithValue("@Details",    $Details)          | Out-Null
        $InsertCmd.Parameters.AddWithValue("@Duration",   $Duration)         | Out-Null
        $InsertCmd.ExecuteNonQuery() | Out-Null

        Write-Log -Status "DB_WRITE_SUCCESS" -Detail "Status=$Status"
    } catch {
        Write-Log -Status "DB_WRITE_FAILED" -Detail $_.Exception.Message
    } finally {
        if ($null -ne $Conn) { $Conn.Close() }
    }
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

# --- Comprehensive startup banner (matches FeatureUpdate/GeneralUpdate format) ---
$StartBanner = @"
================================================================================
  PREFLIGHT REBOOT CHECK v$ScriptVer - SCRIPT START
  Host      : $Hostname
  Domain    : $DomainName
  OS        : $OSVersion
  Uptime    : $UptimeDays days
  RAM       : $TotalRAMGB GB
  Disk (C:) : $FreeGB GB free
  RunningAs : $RunningAs
  PS Ver    : $PSVersion
  LogFile   : $LogFile
  Transcript: $TranscriptPath
  LogDirSrc : $LogDirSrc
  Started   : $($StartTime.ToString('yyyy-MM-dd HH:mm:ss'))
================================================================================
"@
Write-Log -Status "SCRIPT_START" -Detail "Host=$Hostname|Domain=$DomainName|OS=$OSVersion|Uptime=${UptimeDays}days|RAM=${TotalRAMGB}GB|Disk=${FreeGB}GB|PS=$PSVersion|RunAs=$RunningAs"
try { $StartBanner | Out-File $LogFile -Append -Encoding UTF8 -ErrorAction SilentlyContinue } catch {}
Write-Host $StartBanner -ForegroundColor DarkYellow

# --- Check all three pending reboot registry keys ---
Write-Log -Status "CHECKING_PENDING_REBOOT_KEYS" -Detail "WU_Key=HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired|CBS_Key=HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending|PFR_Key=HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager"
$RebootReason = Test-PendingReboot

if ($null -eq $RebootReason) {
    $Duration = [math]::Round(((Get-Date) - $StartTime).TotalSeconds, 0)
    Write-Host "  No pending reboot detected - machine is ready for Feature Update." -ForegroundColor Green
    Write-Log -Status "NO_PENDING_REBOOT_MACHINE_READY" -Detail "OS=$OSVersion|Uptime=${UptimeDays}days|RAM=${TotalRAMGB}GB|Disk=${FreeGB}GB|Duration=${Duration}s"
    Write-ToDatabase -Status "NO_REBOOT_NEEDED" -Details "OS=$OSVersion|Uptime=${UptimeDays}days|RAM=${TotalRAMGB}GB|Disk=${FreeGB}GB"
    Write-Host ""
    Stop-Transcript -ErrorAction SilentlyContinue
    exit 0
}

Write-Host "  Pending reboot detected ($RebootReason) - restart required before upgrade." -ForegroundColor Yellow
Write-Log -Status "PENDING_REBOOT_DETECTED" -Detail "Reason=$RebootReason|OS=$OSVersion|Uptime=${UptimeDays}days|RAM=${TotalRAMGB}GB|Disk=${FreeGB}GB"

# --- Detect active user session ---
Write-Log -Status "CHECKING_USER_SESSIONS" -Detail "Method=explorer_process_SessionId"
$UserPresent   = Test-UserLoggedIn
$LoggedInUser  = try { (Get-CimInstance Win32_ComputerSystem -ErrorAction SilentlyContinue).UserName } catch { "Unknown" }
$ExplorerCount = @(Get-Process explorer -ErrorAction SilentlyContinue | Where-Object { $_.SessionId -ne 0 }).Count

Write-Host "  Interactive user logged in: $UserPresent" -ForegroundColor Cyan
Write-Log -Status "USER_SESSION_CHECK" -Detail "Present=$UserPresent|ActiveUser=$LoggedInUser|ExplorerCount=$ExplorerCount"

if (!$UserPresent) {
    # ---- UNATTENDED: reboot immediately with a short console countdown ----
    Write-Host ""
    Write-Host "  No user logged in - rebooting in 60 seconds to clear pending state." -ForegroundColor Yellow
    Write-Host "  Push FeatureUpdate.ps1 after this machine comes back online." -ForegroundColor Cyan
    Write-Log -Status "UNATTENDED_REBOOTING_IN_60S" -Detail "RebootReason=$RebootReason|OS=$OSVersion|Uptime=${UptimeDays}days|Disk=${FreeGB}GB"

    shutdown.exe /r /t 60 /c "NecessaryAdminTool: Applying pending updates before Windows 11 upgrade. System restarts in 60 seconds."

    $Duration = [math]::Round(((Get-Date) - $StartTime).TotalSeconds, 0)
    Write-Log -Status "SHUTDOWN_COMMAND_ISSUED" -Detail "Delay=60s|Duration=${Duration}s|Result=REBOOTING_UNATTENDED"
    Write-ToDatabase -Status "REBOOTING_UNATTENDED" -Details "RebootReason=$RebootReason|OS=$OSVersion|Uptime=${UptimeDays}days"
    Stop-Transcript -ErrorAction SilentlyContinue
    exit 0

} else {
    # ---- USER PRESENT: WinForms dialog + per-minute console countdown ----
    Write-Host ""
    Write-Host "  User is logged in - showing $WarningMinutes-minute restart warning dialog..." -ForegroundColor Yellow
    Write-Log -Status "USER_PRESENT_SHOWING_DIALOG" -Detail "User=$LoggedInUser|WarningMinutes=$WarningMinutes|RebootReason=$RebootReason"

    $DialogStarted = Get-Date
    Show-RebootWarningDialog -Minutes $WarningMinutes
    $DialogSeconds = [math]::Round(((Get-Date) - $DialogStarted).TotalSeconds, 0)

    Write-Host "  Dialog closed. Starting $WarningMinutes-minute work-save countdown..." -ForegroundColor Yellow
    Write-Log -Status "DIALOG_CLOSED" -Detail "DialogOpenDuration=${DialogSeconds}s|UserClickedRestart=$($script:Restart)"
    Write-Log -Status "REBOOT_COUNTDOWN_STARTED" -Detail "TotalMinutes=$WarningMinutes|RebootReason=$RebootReason|User=$LoggedInUser"

    # Per-minute console countdown - keeps ME execution log alive and gives visible progress
    for ($Min = $WarningMinutes; $Min -gt 0; $Min--) {
        $ElapsedMin = $WarningMinutes - $Min
        Write-Host "  [$Min min remaining | $ElapsedMin min elapsed | $(Get-Date -Format 'HH:mm:ss')] Save your work - restart incoming." -ForegroundColor Yellow
        Write-Log -Status "REBOOT_COUNTDOWN_${Min}MIN_REMAINING" -Detail "ElapsedMin=$ElapsedMin|RebootReason=$RebootReason"
        Start-Sleep -Seconds 60
    }

    Write-Host ""
    Write-Host "  [COUNTDOWN COMPLETE] $WarningMinutes minutes elapsed. Issuing restart now." -ForegroundColor Green

    $Duration = [math]::Round(((Get-Date) - $StartTime).TotalSeconds, 0)
    Write-Log -Status "REBOOT_COUNTDOWN_COMPLETE" -Detail "TotalMinutes=$WarningMinutes|RebootReason=$RebootReason|Duration=${Duration}s"

    shutdown.exe /r /t 30 /c "NecessaryAdminTool: Restarting to apply pending updates before Windows 11 upgrade."

    Write-Log -Status "SHUTDOWN_COMMAND_ISSUED" -Detail "Delay=30s|Duration=${Duration}s|Result=REBOOTING_AFTER_USER_WARNING"
    Write-ToDatabase -Status "REBOOTING_AFTER_USER_WARNING" -Details "RebootReason=$RebootReason|User=$LoggedInUser|OS=$OSVersion|Uptime=${UptimeDays}days|WarningMinutes=$WarningMinutes"
    Stop-Transcript -ErrorAction SilentlyContinue
    exit 0
}
