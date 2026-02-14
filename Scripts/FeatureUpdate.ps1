# ==============================================================================
# ARTAZN IT - FEATURE UPDATE SUITE (v1.0 - ArtaznIT v4.0 Compatible)
# Includes: Windows Major OS Updates, HW Guard, ISO/Cloud Logic, ManageEngine Compatible
# ==============================================================================

# --- 1. CONFIGURABLE PATHS (ManageEngine Compatible) ---
# Default network paths - can be overridden by ManageEngine custom fields
$ISOPath    = if ($env:ARTAZN_ISO_PATH) { $env:ARTAZN_ISO_PATH } else { "\\Jzppdm\sys\PUBLIC\BNIT\01_Software\02_ISOs\Windows\Win11_25H2_English_x64.iso" }
$LogDir     = if ($env:ARTAZN_LOG_DIR) { $env:ARTAZN_LOG_DIR } else { "\\Jzppdm\sys\PUBLIC\BNIT\01_Software\04_Update Logs" }
$PCLog      = "$LogDir\Individual_PC_Logs\$($env:COMPUTERNAME)_Feature.txt"
$MasterLog  = "$LogDir\Master_Update_Log.csv"
$Comp       = $env:COMPUTERNAME

# --- 2. LOGGING ---
function Write-ArtaznLog {
    param([string]$Status, [bool]$ToMaster = $false)
    $Stamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    "[$Stamp] $Status" | Out-File $PCLog -Append -Encoding UTF8

    if ($ToMaster) {
        $LockFile = "$MasterLog.lock"
        $TimeOut = 0
        while ((Test-Path $LockFile) -and ($TimeOut -lt 50)) { Start-Sleep -Milliseconds 200; $TimeOut++ }
        try {
            New-Item -ItemType File -Path $LockFile -Force -ErrorAction SilentlyContinue | Out-Null
            "$Comp,$Status,$Stamp" | Add-Content $MasterLog -Force
        }
        catch { "Logging Error" | Out-File $PCLog -Append }
        finally { Remove-Item $LockFile -ErrorAction SilentlyContinue }
    }
}

# --- 3. UI LOGO (Artazn Branded) ---
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
    Write-Host " ║                  🚀 FEATURE UPDATE SUITE v1.0                       ║" -ForegroundColor DarkYellow
    Write-Host " ╚══════════════════════════════════════════════════════════════════════╝" -ForegroundColor DarkYellow
    Write-Host ""
    if ($Msg) { Write-Host " STATUS: $Msg" -ForegroundColor $Color }
    Write-Host ""
}

# --- 4. EXECUTION ---
Show-ArtaznLogo -Msg "Hardware Compatibility Check..."
$TPM = (Get-Tpm).TpmPresent
$SecureBoot = Confirm-SecureBootUEFI -ErrorAction SilentlyContinue
$FreeGB = [math]::round(((Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='C:'").FreeSpace / 1GB), 2)

if (!$TPM -or !$SecureBoot -or $FreeGB -lt 20) {
    Write-ArtaznLog -Status "FAILED_HW_COMPATIBILITY" -ToMaster $true
    Show-ArtaznLogo -Msg "INCOMPATIBLE HARDWARE. Upgrade Stopped." "Red"
    Start-Sleep -Seconds 5
    exit 1  # ManageEngine exit code for failure
}

function Run-CloudUpdate {
    Show-ArtaznLogo -Msg "ISO Unavailable. Using Cloud Fallback..." "Yellow"
    Write-ArtaznLog -Status "METHOD_CLOUD_START" -ToMaster $true
    if (!(Get-Module -ListAvailable PSWindowsUpdate)) { Install-Module PSWindowsUpdate -Force -Confirm:$false -ErrorAction SilentlyContinue }
    try {
        Get-WindowsUpdate -MicrosoftUpdate -Title "Feature update" -AcceptAll -Install -IgnoreReboot
        Write-ArtaznLog -Status "CLOUD_SUCCESS_REBOOT_REQ" -ToMaster $true
        exit 0  # ManageEngine exit code for success
    } catch {
        Write-ArtaznLog -Status "CLOUD_FAILED" -ToMaster $true
        exit 1  # ManageEngine exit code for failure
    }
}

if ($Comp -like "TN*" -and (Test-Path $ISOPath)) {
    try {
        Show-ArtaznLogo -Msg "Mounting Local ISO..." "Cyan"
        $Mount = Mount-DiskImage -ImagePath $ISOPath -PassThru -ErrorAction Stop
        $Drive = ($Mount | Get-Volume).DriveLetter

        Write-ArtaznLog -Status "ISO_RUNNING" -ToMaster $true
        Show-ArtaznLogo -Msg "Upgrading... (Do not turn off)" "Yellow"

        $Proc = Start-Process "$($Drive):\setup.exe" -ArgumentList "/auto upgrade /quiet /showoobe none /eula accept /dynamicupdate disable" -Wait -PassThru

        Dismount-DiskImage -ImagePath $ISOPath
        Write-ArtaznLog -Status "ISO_COMPLETE_CODE_$($Proc.ExitCode)" -ToMaster $true

        # ManageEngine exit code reporting
        if ($Proc.ExitCode -eq 0) {
            exit 0  # Success
        } else {
            exit 1  # Failure
        }
    }
    catch {
        Run-CloudUpdate
    }
}
else {
    Run-CloudUpdate
}
