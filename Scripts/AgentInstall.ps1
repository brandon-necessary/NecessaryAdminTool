#Requires -Version 5.1
#Requires -RunAsAdministrator
# ==============================================================================
# NECESSARYADMINTOOL IT - AGENT INSTALL SCRIPT (v1.0)
# Copies NecessaryAdminAgent.exe from UNC share + installs as Windows service
# ManageEngine Compatible - run under SYSTEM context via ME deployment
# ==============================================================================
# Parameters (configure in ME before deployment):
#   $AgentExeUNCPath - UNC path to NecessaryAdminAgent.exe (e.g. \\SERVER\Share\NecessaryAdminAgent.exe)
#   $AgentToken      - Pre-shared auth token (must match NAT Options → Agent Token)
#   $AgentPort       - TCP port for agent listener (default: 443)
# ==============================================================================

param(
    [string]$AgentExeUNCPath = "",
    [string]$AgentToken      = "",
    [int]   $AgentPort       = 443
)

# EARLY HEARTBEAT
Write-Host "  [$(Get-Date -Format 'HH:mm:ss')] AgentInstall.ps1 - Script loaded on $env:COMPUTERNAME, starting execution..." -ForegroundColor Cyan

# --- 0. CONFIGURABLE PATHS ---
$LogDir = $env:NECESSARYADMINTOOL_LOG_DIR

if ([string]::IsNullOrEmpty($LogDir) -or !(Test-Path $LogDir -ErrorAction SilentlyContinue)) {
    $LogDir = "$env:TEMP\NecessaryAdminTool_Logs"
    if (!(Test-Path $LogDir)) { New-Item -ItemType Directory -Path $LogDir -Force | Out-Null }
    Write-Host "INFO: Using local fallback logging at $LogDir" -ForegroundColor Cyan
}

$MasterLog  = "$LogDir\Master_Update_Log.csv"
$Timestamp  = Get-Date -Format 'yyyy-MM-dd_HH-mm'
$PCLogDir   = "$LogDir\Individual_PC_Logs"
$PCLog      = "$PCLogDir\$($env:COMPUTERNAME)_AgentInstall_$Timestamp.txt"
$Comp       = $env:COMPUTERNAME
$ScriptVer  = "1.0"
$InstallDir = "$env:ProgramFiles\NecessaryAdminTool\Agent"
$DestExe    = "$InstallDir\NecessaryAdminAgent.exe"

if (!(Test-Path $PCLogDir)) { New-Item -ItemType Directory -Path $PCLogDir -Force | Out-Null }

$TranscriptPath = "$PCLogDir\$($env:COMPUTERNAME)_AgentInstall_${Timestamp}_Transcript.txt"
Start-Transcript -Path $TranscriptPath -Append -NoClobber -ErrorAction SilentlyContinue

$ScriptStart  = Get-Date
$OSInfo       = Get-CimInstance Win32_OperatingSystem -ErrorAction SilentlyContinue
$OSVersion    = if ($OSInfo) { $OSInfo.Caption } else { "Unknown" }
$CurrentBuild = if ($OSInfo) { [int]$OSInfo.BuildNumber } else { 0 }
$SysInfo      = Get-CimInstance Win32_ComputerSystem -ErrorAction SilentlyContinue
$UptimeDays   = if ($OSInfo) { [math]::Round(((Get-Date) - $OSInfo.LastBootUpTime).TotalDays, 2) } else { 0 }
$FreeGB       = try { [math]::Round((Get-PSDrive C -ErrorAction Stop).Free / 1GB, 2) } catch { 0 }
$RunningAs    = [Security.Principal.WindowsIdentity]::GetCurrent().Name
$PSVersion    = "$($PSVersionTable.PSVersion.Major).$($PSVersionTable.PSVersion.Minor)"
$DomainName   = if ($SysInfo) { $SysInfo.Domain } else { "Unknown" }
$TotalRAMGB   = if ($SysInfo) { [math]::Round($SysInfo.TotalPhysicalMemory / 1GB, 2) } else { 0 }
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

# --- 1. LOGGING ---
function Write-Log {
    param([string]$Status)
    $Stamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    try { "[$Stamp] $Status" | Out-File $PCLog -Append -Encoding UTF8 -ErrorAction Stop } catch { Write-Host "[$Stamp] $Status" -ForegroundColor Yellow }
}

# 20-column master CSV — matches GeneralUpdate.ps1 + FeatureUpdate.ps1 schema exactly.
function Write-MasterSummary {
    param([string]$Status, [string]$Details = "")
    $Stamp    = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $Duration = [math]::Round(((Get-Date) - $ScriptStart).TotalSeconds, 0)
    $Header   = "Hostname,Script,Timestamp,OSVersion,BuildNumber,UptimeDays,TotalRAMGB,DiskFreeGB,SerialNumber,Manufacturer,Model,IPAddress,LoggedInUser,TPMPresent,SecureBoot,Status,Method,UpdateCount,Details,DurationSeconds"
    # All fields quoted to handle commas, special characters, and formula injection in field values
    $Row      = "`"$Comp`",`"AgentInstall`",`"$Stamp`",`"$OSVersion`",`"$CurrentBuild`",`"$UptimeDays`",`"$TotalRAMGB`",`"$FreeGB`",`"$SerialNumber`",`"$Manufacturer`",`"$Model`",`"$IPAddress`",`"$LoggedInUser`",`"N/A`",`"N/A`",`"$Status`",`"AgentInstall`",`"N/A`",`"$Details`",`"$Duration`""

    $Mtx = $null; $Acquired = $false
    try {
        $Mtx = [System.Threading.Mutex]::new($false, "Global\NecessaryAdminTool_MasterLog")
        $Acquired = $Mtx.WaitOne(10000)
    } catch [System.Threading.AbandonedMutexException] { $Acquired = $true } catch {}
    try {
        if (!(Test-Path $MasterLog)) { $Header | Out-File $MasterLog -Encoding UTF8 -ErrorAction SilentlyContinue }
        $Row | Add-Content $MasterLog -Force -ErrorAction Stop
    } catch {
        Write-Log "ERROR: Master Summary Write Failed - $($_.Exception.Message)"
    } finally {
        if ($Acquired -and $Mtx) { try { $Mtx.ReleaseMutex() } catch {} }
        if ($Mtx) { try { $Mtx.Dispose() } catch {} }
    }
}

# --- 2. STARTUP BANNER ---
Write-Log "--- NecessaryAdminTool Agent Install v$ScriptVer ---"
Write-Log "Host: $Comp | Domain: $DomainName | OS: $OSVersion (Build $CurrentBuild)"
Write-Log "Uptime: ${UptimeDays}d | RAM: ${TotalRAMGB}GB | Disk C: Free: ${FreeGB}GB"
Write-Log "RunAs: $RunningAs | PS: $PSVersion"
Write-Log "Parameters: UNCPath=$AgentExeUNCPath | Port=$AgentPort | Token=(set=$(![string]::IsNullOrEmpty($AgentToken)))"
Write-Log "InstallDir: $InstallDir"
Write-Log "Script START: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"

# --- 3. VALIDATE PARAMETERS ---
if ([string]::IsNullOrEmpty($AgentToken)) {
    Write-Log "ERROR: AgentToken parameter is empty - cannot install without a token"
    Write-Host "  [ERROR] AgentToken is required. Set it in ME script parameters." -ForegroundColor Red
    Write-MasterSummary -Status "AGENT_INSTALL_FAILED_NO_TOKEN" -Details "AgentToken parameter empty"
    Stop-Transcript -ErrorAction SilentlyContinue
    exit 2
}

if ([string]::IsNullOrEmpty($AgentExeUNCPath)) {
    Write-Log "ERROR: AgentExeUNCPath parameter is empty - no source EXE specified"
    Write-Host "  [ERROR] AgentExeUNCPath is required. Set it in ME script parameters." -ForegroundColor Red
    Write-MasterSummary -Status "AGENT_INSTALL_FAILED_NO_PATH" -Details "AgentExeUNCPath parameter empty"
    Stop-Transcript -ErrorAction SilentlyContinue
    exit 2
}

if (!(Test-Path $AgentExeUNCPath -ErrorAction SilentlyContinue)) {
    Write-Log "ERROR: Agent EXE not accessible at: $AgentExeUNCPath"
    Write-Host "  [ERROR] Cannot reach: $AgentExeUNCPath" -ForegroundColor Red
    Write-MasterSummary -Status "AGENT_INSTALL_FAILED_NO_SOURCE" -Details "Cannot reach UNC path"
    Stop-Transcript -ErrorAction SilentlyContinue
    exit 2
}

# Validate port range
if ($AgentPort -lt 1 -or $AgentPort -gt 65535) {
    Write-Log "ERROR: Invalid port $AgentPort - must be 1-65535"
    Write-Host "  [ERROR] AgentPort $AgentPort is out of range (1-65535)." -ForegroundColor Red
    Write-MasterSummary -Status "AGENT_INSTALL_FAILED_BAD_PORT" -Details "Port $AgentPort out of range"
    Stop-Transcript -ErrorAction SilentlyContinue
    exit 2
}

# --- 4. COPY EXE TO INSTALL DIRECTORY ---
Write-Host "  [$(Get-Date -Format 'HH:mm:ss')] Copying agent to $InstallDir..." -ForegroundColor Cyan
Write-Log "STEP: Copying agent EXE"

try {
    if (!(Test-Path $InstallDir)) {
        New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
        Write-Log "Created install directory: $InstallDir"
    }

    # Stop existing service before overwriting EXE
    $existingSvc = Get-Service -Name NecessaryAdminAgent -ErrorAction SilentlyContinue
    if ($existingSvc -and $existingSvc.Status -eq 'Running') {
        Stop-Service -Name NecessaryAdminAgent -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
        Write-Log "Stopped existing NecessaryAdminAgent service before update"
    }

    Copy-Item -Path $AgentExeUNCPath -Destination $DestExe -Force -ErrorAction Stop
    $exeInfo = Get-Item $DestExe
    Write-Log "Agent EXE copied: $DestExe ($([math]::Round($exeInfo.Length / 1KB, 1)) KB)"
    Write-Host "  [OK] Agent EXE copied ($([math]::Round($exeInfo.Length / 1KB, 1)) KB)" -ForegroundColor Green
} catch {
    Write-Log "ERROR: Copy failed - $($_.Exception.Message)"
    Write-Host "  [ERROR] Copy failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-MasterSummary -Status "AGENT_INSTALL_FAILED_COPY" -Details "Copy from UNC failed"
    Stop-Transcript -ErrorAction SilentlyContinue
    exit 1
}

# --- 5. RUN --INSTALL ---
Write-Host "  [$(Get-Date -Format 'HH:mm:ss')] Running agent --install (port $AgentPort)..." -ForegroundColor Cyan
Write-Log "STEP: Running --install"

try {
    $installArgs = "--install --token `"$AgentToken`" --port $AgentPort"
    $proc = Start-Process -FilePath $DestExe -ArgumentList $installArgs -Wait -PassThru -NoNewWindow -ErrorAction Stop
    Write-Log "Agent --install exited with code: $($proc.ExitCode)"
    if ($proc.ExitCode -ne 0) {
        throw "Agent --install returned exit code $($proc.ExitCode)"
    }
    Write-Host "  [OK] Agent --install completed (exit $($proc.ExitCode))" -ForegroundColor Green
} catch {
    Write-Log "ERROR: --install failed - $($_.Exception.Message)"
    Write-Host "  [ERROR] --install failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-MasterSummary -Status "AGENT_INSTALL_FAILED_INSTALL" -Details "--install exited non-zero"
    Stop-Transcript -ErrorAction SilentlyContinue
    exit 1
}

# --- 6. VERIFY SERVICE IS RUNNING ---
Write-Host "  [$(Get-Date -Format 'HH:mm:ss')] Verifying NecessaryAdminAgent service..." -ForegroundColor Cyan
Write-Log "STEP: Verifying service status"

Start-Sleep -Seconds 3  # Give SCM time to start the service

$svcOk = $false
try {
    $svc = Get-Service -Name NecessaryAdminAgent -ErrorAction Stop
    $svcOk = ($svc.Status -eq 'Running')
    Write-Log "Service status: $($svc.Status) | StartType: $($svc.StartType)"
    if ($svcOk) {
        Write-Host "  [OK] NecessaryAdminAgent is Running" -ForegroundColor Green
    } else {
        Write-Host "  [WARN] NecessaryAdminAgent status: $($svc.Status)" -ForegroundColor Yellow
    }
} catch {
    Write-Log "ERROR: Get-Service failed - $($_.Exception.Message)"
    Write-Host "  [ERROR] Could not verify service status" -ForegroundColor Red
}

# --- 7. VERIFY TCP PORT LISTENING ---
Write-Host "  [$(Get-Date -Format 'HH:mm:ss')] Verifying port $AgentPort is listening..." -ForegroundColor Cyan
Write-Log "STEP: Verifying TCP port $AgentPort"

$portOk = $false
try {
    $conn = [System.Net.Sockets.TcpClient]::new()
    $conn.Connect("127.0.0.1", $AgentPort)
    $conn.Close()
    $portOk = $true
    Write-Log "TCP port $AgentPort is listening - connection test passed"
    Write-Host "  [OK] Port $AgentPort is listening" -ForegroundColor Green
} catch {
    Write-Log "WARNING: Port $AgentPort not yet accepting connections - $($_.Exception.Message)"
    Write-Host "  [WARN] Port $AgentPort not yet accepting (service may still be starting)" -ForegroundColor Yellow
}

# --- 8. DONE ---
$Duration = [math]::Round(((Get-Date) - $ScriptStart).TotalSeconds, 0)
$FinalStatus = if ($svcOk) { "AGENT_INSTALLED_OK" } else { "AGENT_INSTALL_PARTIAL" }

Write-Log "Script END: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') | Duration: ${Duration}s | Status: $FinalStatus"
Write-MasterSummary -Status $FinalStatus -Details "Agent installed on port $AgentPort; service running=$svcOk; port test=$portOk"

Write-Host ""
Write-Host "  [$(Get-Date -Format 'HH:mm:ss')] AgentInstall complete - Status: $FinalStatus (${Duration}s)" -ForegroundColor $(if ($svcOk) { 'Green' } else { 'Yellow' })

Stop-Transcript -ErrorAction SilentlyContinue
exit $(if ($svcOk) { 0 } else { 1 })
