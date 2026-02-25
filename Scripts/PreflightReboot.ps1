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
      - User logged in     -> 20-minute WinForms warning dialog with up to 3 postpones
                             Postpone: increments flag file, exits 0 (re-push ME task)
                             Final notice (no postpones left): forced countdown, no button
                             Timer expires or "Restart Now": reboot issued

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
$ScriptVer      = "1.3"
$WarningMinutes = 20
$MaxPostpones   = 3
$PostponeFlag   = "C:\Windows\Temp\NecessaryAdminTool_Preflight_Postpone.txt"

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
$LogDirSrc  = if ($env:NECESSARYADMINTOOL_LOG_DIR) { "Configured ($env:NECESSARYADMINTOOL_LOG_DIR)" } else { "LOCAL FALLBACK ($LogDir)" }
$MasterLog  = "$LogDir\Master_Update_Log.csv"

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
        $SafeMsg = $_.Exception.Message -replace '(?i)(password|pwd|user id|uid|data source|server)[^;]*(=)[^;]*', '$1$2***'
        Write-Log -Status "DB_WRITE_FAILED" -Detail $SafeMsg
    } finally {
        if ($null -ne $Conn) { $Conn.Close() }
    }
}

# 20-column master CSV -- matches GeneralUpdate/FeatureUpdate/WMIEnable/AgentInstall schema.
function Write-MasterSummary {
    param([string]$Status, [string]$Details = "")
    $Stamp    = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $Duration = [math]::Round(((Get-Date) - $StartTime).TotalSeconds, 0)
    $Build    = if ($OSInfo) { $OSInfo.BuildNumber } else { "N/A" }
    $Header   = "Hostname,Script,Timestamp,OSVersion,BuildNumber,UptimeDays,TotalRAMGB,DiskFreeGB,SerialNumber,Manufacturer,Model,IPAddress,LoggedInUser,TPMPresent,SecureBoot,Status,Method,UpdateCount,Details,DurationSeconds"
    $Row      = "`"$Hostname`",`"Preflight`",`"$Stamp`",`"$OSVersion`",`"$Build`",`"$UptimeDays`",`"$TotalRAMGB`",`"$FreeGB`",`"N/A`",`"N/A`",`"N/A`",`"N/A`",`"N/A`",`"N/A`",`"N/A`",`"$Status`",`"Preflight`",`"N/A`",`"$Details`",`"$Duration`""

    $Mtx = $null; $Acquired = $false
    try {
        $Mtx = [System.Threading.Mutex]::new($false, "Global\NecessaryAdminTool_MasterLog")
        $Acquired = $Mtx.WaitOne(10000)
    } catch [System.Threading.AbandonedMutexException] { $Acquired = $true } catch {}
    try {
        if (!(Test-Path $MasterLog)) { $Header | Out-File $MasterLog -Encoding UTF8 -ErrorAction SilentlyContinue }
        $Row | Add-Content $MasterLog -Force -ErrorAction Stop
    } catch {
        Write-Log -Status "ERROR: Master Summary Write Failed - $($_.Exception.Message)"
    } finally {
        if ($Acquired -and $Mtx) { try { $Mtx.ReleaseMutex() } catch {} }
        if ($Mtx) { try { $Mtx.Dispose() } catch {} }
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
    param(
        [int]$Minutes       = 20,
        [bool]$AllowPostpone = $false,
        [int]$PostponesLeft  = 0
    )

    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing

    $TotalSeconds      = $Minutes * 60
    $script:Elapsed    = 0
    $script:Restart    = $false
    $script:Postponed  = $false

    $FormHeight = if ($AllowPostpone) { 310 } else { 280 }

    $Form                  = New-Object System.Windows.Forms.Form
    $Form.Text             = "NecessaryAdminTool IT - Restart Required"
    $Form.Size             = New-Object System.Drawing.Size(520, $FormHeight)
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

    # Body text — adjusted based on whether postpones are available
    $BodyLines = "Your IT team needs to restart this computer to apply pending`n" +
                 "Windows updates before a scheduled OS upgrade.`n`n" +
                 "PLEASE SAVE YOUR WORK NOW.`n`n"
    if ($AllowPostpone) {
        $BodyLines += "Postpones remaining: $PostponesLeft of $MaxPostpones`n" +
                      "  Restart Now  - Restart immediately.`n" +
                      "  Wait 20 min  - Delay this restart ($PostponesLeft remaining).`n`n" +
                      "Your computer will restart automatically when the timer reaches zero."
    } else {
        $BodyLines += "*** NO FURTHER POSTPONES AVAILABLE ***`n`n" +
                      "Your computer will restart automatically when the timer reaches zero."
    }

    $LblBody               = New-Object System.Windows.Forms.Label
    $LblBody.Text          = $BodyLines
    $LblBody.Font          = New-Object System.Drawing.Font("Segoe UI", 9)
    $LblBody.ForeColor     = [System.Drawing.Color]::FromArgb(200, 200, 200)
    $LblBody.Location      = New-Object System.Drawing.Point(20, 52)
    $LblBody.Size          = New-Object System.Drawing.Size(480, 130)
    $Form.Controls.Add($LblBody)

    # Countdown label
    $LblTimer              = New-Object System.Windows.Forms.Label
    $LblTimer.Text         = "Restarting in  $Minutes:00"
    $LblTimer.Font         = New-Object System.Drawing.Font("Segoe UI", 13, [System.Drawing.FontStyle]::Bold)
    $LblTimer.ForeColor    = [System.Drawing.Color]::FromArgb(255, 210, 80)
    $LblTimer.Location     = New-Object System.Drawing.Point(20, 192)
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
    $BtnNow.Location       = New-Object System.Drawing.Point(350, 186)
    $BtnNow.Add_Click({
        $script:Restart = $true
        $Form.Close()
    })
    $Form.Controls.Add($BtnNow)

    # "Wait 20 min" (postpone) button — only shown when postpones remain
    if ($AllowPostpone) {
        $BtnWait               = New-Object System.Windows.Forms.Button
        $BtnWait.Text          = "Wait 20 min  ($PostponesLeft left)"
        $BtnWait.Font          = New-Object System.Drawing.Font("Segoe UI", 10)
        $BtnWait.BackColor     = [System.Drawing.Color]::FromArgb(60, 60, 60)
        $BtnWait.ForeColor     = [System.Drawing.Color]::White
        $BtnWait.FlatStyle     = "Flat"
        $BtnWait.Size          = New-Object System.Drawing.Size(185, 34)
        $BtnWait.Location      = New-Object System.Drawing.Point(148, 232)
        $BtnWait.Add_Click({
            $script:Postponed = $true
            $Form.Close()
        })
        $Form.Controls.Add($BtnWait)
    }

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
    catch { Write-Log -Status "DIALOG_DISPLAY_FAILED_SESSION0_OR_NO_DESKTOP - timer will expire and reboot will proceed automatically" }
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
    Write-MasterSummary -Status "NO_REBOOT_NEEDED" -Details "OS=$OSVersion|Uptime=${UptimeDays}days"
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

    Remove-Item $PostponeFlag -Force -ErrorAction SilentlyContinue
    & "$env:SystemRoot\System32\shutdown.exe" /r /t 60 /c "NecessaryAdminTool: Applying pending updates. System restarts in 60 seconds."

    $Duration = [math]::Round(((Get-Date) - $StartTime).TotalSeconds, 0)
    Write-Log -Status "SHUTDOWN_COMMAND_ISSUED" -Detail "Delay=60s|Duration=${Duration}s|Result=REBOOTING_UNATTENDED"
    Write-ToDatabase -Status "REBOOTING_UNATTENDED" -Details "RebootReason=$RebootReason|OS=$OSVersion|Uptime=${UptimeDays}days"
    Write-MasterSummary -Status "REBOOTING_UNATTENDED" -Details "RebootReason=$RebootReason|OS=$OSVersion|Uptime=${UptimeDays}days"
    Stop-Transcript -ErrorAction SilentlyContinue
    exit 0

} else {
    # ---- USER PRESENT: WinForms dialog with postpone support (up to $MaxPostpones times) ----

    # Read postpone history from flag file (JSON with count+timestamp, or legacy plain-number format)
    $PostponeCount = 0
    if (Test-Path $PostponeFlag) {
        try {
            $Raw = (Get-Content $PostponeFlag -Raw -ErrorAction Stop).Trim()
            if ($Raw -match '^\d+$') {
                # Legacy plain-number format
                $PostponeCount = [int]$Raw
            } else {
                $PostponeData = $Raw | ConvertFrom-Json -ErrorAction Stop
                $PostponeCount = [int]$PostponeData.Count
                # Reset stale postpones: if last postpone was > 7 days ago, treat as fresh start
                $LastPostpone = [DateTime]::Parse($PostponeData.LastPostpone)
                if (((Get-Date) - $LastPostpone).TotalDays -gt 7) {
                    Write-Log -Status "POSTPONE_RESET - last postpone was $([math]::Round(((Get-Date) - $LastPostpone).TotalDays, 1)) days ago (>7 day threshold)"
                    $PostponeCount = 0
                    Remove-Item $PostponeFlag -Force -ErrorAction SilentlyContinue
                }
            }
        } catch { $PostponeCount = 0 }
    }
    $PostponesLeft = $MaxPostpones - $PostponeCount

    Write-Host ""
    Write-Host "  User is logged in - showing $WarningMinutes-minute restart warning dialog..." -ForegroundColor Yellow
    Write-Host "  Postpone history: $PostponeCount of $MaxPostpones used ($PostponesLeft remaining)" -ForegroundColor Cyan
    Write-Log -Status "USER_PRESENT_SHOWING_DIALOG" -Detail "User=$LoggedInUser|WarningMinutes=$WarningMinutes|RebootReason=$RebootReason|PostponeCount=$PostponeCount|PostponesLeft=$PostponesLeft"

    $DialogStarted = Get-Date
    Show-RebootWarningDialog -Minutes $WarningMinutes `
        -AllowPostpone ($PostponeCount -lt $MaxPostpones) `
        -PostponesLeft $PostponesLeft
    $DialogSeconds = [math]::Round(((Get-Date) - $DialogStarted).TotalSeconds, 0)

    Write-Log -Status "DIALOG_CLOSED" -Detail "DialogOpenDuration=${DialogSeconds}s|UserClickedRestart=$($script:Restart)|UserPostponed=$($script:Postponed)"

    # --- Postpone path: save count, exit 0, re-push via ME ---
    if ($script:Postponed) {
        $NewCount = $PostponeCount + 1
        # Write JSON with count + timestamp so stale postpones auto-expire after 7 days
        @{ Count = $NewCount; LastPostpone = (Get-Date -Format 'yyyy-MM-dd HH:mm:ss') } |
            ConvertTo-Json -Compress | Out-File $PostponeFlag -Encoding UTF8 -Force
        Write-Host ""
        Write-Host "  [USER POSTPONED] Postpones used: $NewCount of $MaxPostpones." -ForegroundColor Yellow
        Write-Host "  Exiting with code 0. Re-push the ME task to attempt again." -ForegroundColor Yellow
        Write-Log -Status "PREFLIGHT_POSTPONED_${NewCount}_OF_${MaxPostpones}" -Detail "User=$LoggedInUser|RebootReason=$RebootReason"
        Write-ToDatabase -Status "POSTPONED_${NewCount}_OF_${MaxPostpones}" -Details "RebootReason=$RebootReason|User=$LoggedInUser|PostponeCount=$NewCount"
        Write-MasterSummary -Status "POSTPONED_${NewCount}_OF_${MaxPostpones}" -Details "RebootReason=$RebootReason|User=$LoggedInUser"
        Stop-Transcript -ErrorAction SilentlyContinue
        exit 0
    }

    # --- Proceed path: clear postpone flag, then reboot ---
    Remove-Item $PostponeFlag -Force -ErrorAction SilentlyContinue
    Write-Host ""
    Write-Host "  [PROCEEDING] Countdown complete or user clicked Restart Now. Issuing restart." -ForegroundColor Green
    Write-Log -Status "REBOOT_COUNTDOWN_COMPLETE" -Detail "TotalMinutes=$WarningMinutes|RebootReason=$RebootReason|User=$LoggedInUser|PostponesUsed=$PostponeCount"

    $Duration = [math]::Round(((Get-Date) - $StartTime).TotalSeconds, 0)
    & "$env:SystemRoot\System32\shutdown.exe" /r /t 30 /c "NecessaryAdminTool: Restarting to apply pending updates."

    Write-Log -Status "SHUTDOWN_COMMAND_ISSUED" -Detail "Delay=30s|Duration=${Duration}s|Result=REBOOTING_AFTER_USER_WARNING"
    Write-ToDatabase -Status "REBOOTING_AFTER_USER_WARNING" -Details "RebootReason=$RebootReason|User=$LoggedInUser|OS=$OSVersion|Uptime=${UptimeDays}days|WarningMinutes=$WarningMinutes|PostponesUsed=$PostponeCount"
    Write-MasterSummary -Status "REBOOTING_AFTER_USER_WARNING" -Details "RebootReason=$RebootReason|User=$LoggedInUser|PostponesUsed=$PostponeCount"
    Stop-Transcript -ErrorAction SilentlyContinue
    exit 0
}
