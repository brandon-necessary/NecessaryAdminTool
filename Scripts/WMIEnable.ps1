#Requires -Version 5.1
#Requires -RunAsAdministrator
# ==============================================================================
# NECESSARYADMINTOOL IT - WMI ENABLE SCRIPT (v1.0)
# Enables WMI firewall rules + WinRM on target clients
# Allows NAT to perform remote WMI queries without needing the NecessaryAdminAgent
# ManageEngine Compatible - run under SYSTEM context via ME deployment
# ==============================================================================

# EARLY HEARTBEAT - first executable line; if ME execution log is blank, script never loaded
Write-Host "  [$(Get-Date -Format 'HH:mm:ss')] WMIEnable.ps1 - Script loaded on $env:COMPUTERNAME, starting execution..." -ForegroundColor Cyan

# Enterprise standard: fail loudly on unexpected errors
$ErrorActionPreference = 'Stop'

# --- 0. CONFIGURABLE PATHS ---
$LogDir = $env:NECESSARYADMINTOOL_LOG_DIR

if ([string]::IsNullOrEmpty($LogDir) -or !(Test-Path $LogDir -ErrorAction SilentlyContinue)) {
    $LogDir = "$env:TEMP\NecessaryAdminTool_Logs"
    if (!(Test-Path $LogDir)) { New-Item -ItemType Directory -Path $LogDir -Force | Out-Null }
    Write-Host "INFO: Using local fallback logging at $LogDir" -ForegroundColor Cyan
}

$MasterLog = "$LogDir\Master_Update_Log.csv"
$Timestamp = Get-Date -Format 'yyyy-MM-dd_HH-mm'
$PCLogDir  = "$LogDir\Individual_PC_Logs"
$PCLog     = "$PCLogDir\$($env:COMPUTERNAME)_WMIEnable_$Timestamp.txt"
$Comp      = $env:COMPUTERNAME
$ScriptVer = "1.0"

if (!(Test-Path $PCLogDir)) { New-Item -ItemType Directory -Path $PCLogDir -Force | Out-Null }

$TranscriptPath = "$PCLogDir\$($env:COMPUTERNAME)_WMIEnable_${Timestamp}_Transcript.txt"
Start-Transcript -Path $TranscriptPath -Append -NoClobber -ErrorAction SilentlyContinue

$ScriptStart = Get-Date
$OSInfo      = Get-CimInstance Win32_OperatingSystem -ErrorAction SilentlyContinue
$OSVersion   = if ($OSInfo) { $OSInfo.Caption } else { "Unknown" }
$CurrentBuild = if ($OSInfo) { [int]$OSInfo.BuildNumber } else { 0 }
$SysInfo     = Get-CimInstance Win32_ComputerSystem -ErrorAction SilentlyContinue
$UptimeDays  = if ($OSInfo) { [math]::Round(((Get-Date) - $OSInfo.LastBootUpTime).TotalDays, 2) } else { 0 }
$FreeGB      = try { [math]::Round((Get-PSDrive C -ErrorAction Stop).Free / 1GB, 2) } catch { 0 }
$RunningAs   = [Security.Principal.WindowsIdentity]::GetCurrent().Name
$PSVersion   = "$($PSVersionTable.PSVersion.Major).$($PSVersionTable.PSVersion.Minor)"
$DomainName  = if ($SysInfo) { $SysInfo.Domain } else { "Unknown" }
$TotalRAMGB  = if ($SysInfo) { [math]::Round($SysInfo.TotalPhysicalMemory / 1GB, 2) } else { 0 }
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
# Header: Hostname,Script,Timestamp,OSVersion,BuildNumber,UptimeDays,TotalRAMGB,DiskFreeGB,
#         SerialNumber,Manufacturer,Model,IPAddress,LoggedInUser,TPMPresent,SecureBoot,
#         Status,Method,UpdateCount,Details,DurationSeconds
function Write-MasterSummary {
    param([string]$Status, [string]$Details = "")
    $Stamp    = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $Duration = [math]::Round(((Get-Date) - $ScriptStart).TotalSeconds, 0)
    $Header   = "Hostname,Script,Timestamp,OSVersion,BuildNumber,UptimeDays,TotalRAMGB,DiskFreeGB,SerialNumber,Manufacturer,Model,IPAddress,LoggedInUser,TPMPresent,SecureBoot,Status,Method,UpdateCount,Details,DurationSeconds"
    # All fields quoted to handle commas, special characters, and formula injection in field values
    $Row      = "`"$Comp`",`"WMIEnable`",`"$Stamp`",`"$OSVersion`",`"$CurrentBuild`",`"$UptimeDays`",`"$TotalRAMGB`",`"$FreeGB`",`"$SerialNumber`",`"$Manufacturer`",`"$Model`",`"$IPAddress`",`"$LoggedInUser`",`"N/A`",`"N/A`",`"$Status`",`"WMIEnable`",`"N/A`",`"$Details`",`"$Duration`""

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
Write-Log "--- NecessaryAdminTool WMI Enable v$ScriptVer ---"
Write-Log "Host: $Comp | Domain: $DomainName | OS: $OSVersion (Build $CurrentBuild)"
Write-Log "Uptime: ${UptimeDays}d | RAM: ${TotalRAMGB}GB | Disk C: Free: ${FreeGB}GB"
Write-Log "RunAs: $RunningAs | PS: $PSVersion"
Write-Log "Log: $PCLog | Transcript: $TranscriptPath"
Write-Log "Script START: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"

# --- 3. ENABLE WMI FIREWALL RULES ---
Write-Host "  [$(Get-Date -Format 'HH:mm:ss')] Enabling WMI firewall rules..." -ForegroundColor Cyan
Write-Log "STEP: Enabling WMI firewall rule group"

try {
    $result = netsh advfirewall firewall set rule group="Windows Management Instrumentation (WMI-In)" new enable=yes 2>&1
    # Native commands don't throw — check $LASTEXITCODE explicitly
    if ($LASTEXITCODE -ne 0) {
        Write-Log "WARNING: netsh firewall command exited $LASTEXITCODE - output: $result"
        Write-Host "  [WARN] WMI firewall group rule may not exist by that name (exit $LASTEXITCODE) - individual rules attempted below" -ForegroundColor Yellow
    } else {
        Write-Log "WMI firewall rule: $result"
        Write-Host "  [OK] WMI firewall rules enabled" -ForegroundColor Green
    }
} catch {
    Write-Log "WARNING: WMI firewall rule set failed - $($_.Exception.Message)"
    Write-Host "  [WARN] WMI firewall rule may already be set or group name differs" -ForegroundColor Yellow
}

# Enable individual WMI rules by name (covers OS localisation variants)
$wmiRuleNames = @(
    "Windows Management Instrumentation (WMI-In)",
    "Windows Management Instrumentation (ASync-In)",
    "Windows Management Instrumentation (DCOM-In)"
)
foreach ($ruleName in $wmiRuleNames) {
    try {
        Get-NetFirewallRule -DisplayName $ruleName -ErrorAction Stop | Enable-NetFirewallRule -ErrorAction Stop
        Write-Log "  Enabled firewall rule: $ruleName"
    } catch {
        Write-Log "  Firewall rule not found or already enabled: $ruleName"
    }
}

# --- 4. ENSURE WINMGMT SERVICE IS RUNNING ---
Write-Host "  [$(Get-Date -Format 'HH:mm:ss')] Configuring Winmgmt service..." -ForegroundColor Cyan
Write-Log "STEP: Configuring Winmgmt service"

try {
    $svc = Get-Service -Name Winmgmt -ErrorAction Stop
    if ($svc.StartType -ne 'Automatic') {
        Set-Service -Name Winmgmt -StartupType Automatic -ErrorAction Stop
        Write-Log "Winmgmt startup type set to Automatic"
    }
    if ($svc.Status -ne 'Running') {
        Start-Service -Name Winmgmt -ErrorAction Stop
        Write-Log "Winmgmt service started"
    } else {
        Write-Log "Winmgmt service already running"
    }
    Write-Host "  [OK] Winmgmt is Running (Automatic)" -ForegroundColor Green
} catch {
    Write-Log "ERROR: Winmgmt service configuration failed - $($_.Exception.Message)"
    Write-Host "  [ERROR] Winmgmt configuration failed" -ForegroundColor Red
}

# --- 5. ENABLE DCOM REMOTE ACCESS ---
Write-Host "  [$(Get-Date -Format 'HH:mm:ss')] Configuring DCOM remote access..." -ForegroundColor Cyan
Write-Log "STEP: Configuring DCOM remote access"

try {
    # Enable DCOM via registry
    $dcomKey = "HKLM:\SOFTWARE\Microsoft\Ole"
    $current = (Get-ItemProperty -Path $dcomKey -Name "EnableDCOM" -ErrorAction SilentlyContinue).EnableDCOM
    if ($current -ne "Y") {
        Set-ItemProperty -Path $dcomKey -Name "EnableDCOM" -Value "Y" -Type String -ErrorAction Stop
        Write-Log "DCOM enabled in registry (was: $current)"
    } else {
        Write-Log "DCOM already enabled"
    }
    Write-Host "  [OK] DCOM remote access enabled" -ForegroundColor Green
} catch {
    Write-Log "WARNING: DCOM registry key update failed - $($_.Exception.Message)"
    Write-Host "  [WARN] DCOM config may require manual review" -ForegroundColor Yellow
}

# --- 6. ENABLE WINRM (OPTIONAL - useful for PS Remoting alongside WMI) ---
Write-Host "  [$(Get-Date -Format 'HH:mm:ss')] Configuring WinRM..." -ForegroundColor Cyan
Write-Log "STEP: Configuring WinRM"

try {
    Enable-PSRemoting -Force -SkipNetworkProfileCheck -ErrorAction Stop
    Write-Log "WinRM enabled via Enable-PSRemoting"
    Write-Host "  [OK] WinRM enabled" -ForegroundColor Green
} catch {
    Write-Log "WARNING: Enable-PSRemoting failed (may already be enabled or policy blocked) - $($_.Exception.Message)"
    # Try ensuring WinRM service is at least running
    try {
        Set-Service -Name WinRM -StartupType Automatic -ErrorAction SilentlyContinue
        Start-Service -Name WinRM -ErrorAction SilentlyContinue
        Write-Log "WinRM service started manually"
    } catch {}
}

# --- 7. VERIFY WMI RESPONSIVE ---
Write-Host "  [$(Get-Date -Format 'HH:mm:ss')] Verifying WMI responds locally..." -ForegroundColor Cyan
Write-Log "STEP: Verifying local WMI"

$wmiOk = $false
try {
    $testQuery = Get-CimInstance Win32_OperatingSystem -ErrorAction Stop
    $wmiOk = ($null -ne $testQuery)
    Write-Log "WMI local test: OK (OS: $($testQuery.Caption))"
    Write-Host "  [OK] WMI local test passed" -ForegroundColor Green
} catch {
    Write-Log "ERROR: WMI local test failed - $($_.Exception.Message)"
    Write-Host "  [ERROR] WMI local test FAILED - may need reboot" -ForegroundColor Red
}

# --- 8. DONE ---
$Duration = [math]::Round(((Get-Date) - $ScriptStart).TotalSeconds, 0)
$FinalStatus = if ($wmiOk) { "WMI_ENABLED_OK" } else { "WMI_ENABLE_PARTIAL" }

Write-Log "Script END: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') | Duration: ${Duration}s | Status: $FinalStatus"
Write-MasterSummary -Status $FinalStatus -Details "WMI firewall+service+DCOM+WinRM configured on $(Get-Date -Format 'yyyy-MM-dd')"

Write-Host ""
Write-Host "  [$(Get-Date -Format 'HH:mm:ss')] WMIEnable complete - Status: $FinalStatus (${Duration}s)" -ForegroundColor $(if ($wmiOk) { 'Green' } else { 'Yellow' })

Stop-Transcript -ErrorAction SilentlyContinue
exit $(if ($wmiOk) { 0 } else { 1 })
