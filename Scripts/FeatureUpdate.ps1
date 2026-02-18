# ==============================================================================
# NECESSARYADMINTOOL IT - FEATURE UPDATE SUITE (v1.0 - Bulletproof Edition)
# Includes: Windows Major OS Updates, HW Guard, ISO/Cloud Logic, ManageEngine Compatible
# Security Hardened: Admin checks, configurable patterns, resource cleanup, timeouts
# ==============================================================================

# --- PRE-FLIGHT GUARD: Admin Privileges ---
$CurrentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
$IsAdmin = (New-Object Security.Principal.WindowsPrincipal $CurrentUser).IsInRole([Security.Principal.WindowsBuiltinRole]::Administrator)

if (!$IsAdmin) {
    Write-Error "ERROR: This script requires administrator privileges"
    Write-Error "Right-click PowerShell and select 'Run as Administrator'"
    exit 1
}

# --- 1. CONFIGURABLE PATHS (Environment Variable Based) ---
# ALL paths are configured via environment variables or app settings
# NO hardcoded paths - configured through NecessaryAdminTool Options menu

$ISOPath         = $env:NECESSARYADMINTOOL_ISO_PATH
$LogDir          = $env:NECESSARYADMINTOOL_LOG_DIR
$HostnamePattern = if ($env:NECESSARYADMINTOOL_HOSTNAME_PATTERN) { $env:NECESSARYADMINTOOL_HOSTNAME_PATTERN } else { "*" }

# Log directory validation with automatic local fallback
if ([string]::IsNullOrEmpty($LogDir) -or !(Test-Path $LogDir -ErrorAction SilentlyContinue)) {
    $LogDir = "$env:TEMP\NecessaryAdminTool_Logs"
    if (!(Test-Path $LogDir)) {
        New-Item -ItemType Directory -Path $LogDir -Force | Out-Null
    }
    Write-Host "INFO: Using local fallback logging at $LogDir" -ForegroundColor Cyan
}

$PCLogDir   = "$LogDir\Individual_PC_Logs"
$PCLog      = "$PCLogDir\$($env:COMPUTERNAME)_Feature.txt"
$MasterLog  = "$LogDir\Master_Update_Log.csv"
$Comp       = $env:COMPUTERNAME

# Constants
$MIN_DISK_SPACE_GB = 20
$SETUP_TIMEOUT_SECONDS = 7200  # 2 hours
$MAX_LOG_LOCK_TIMEOUT = 50
$MIN_ISO_SIZE_GB = 1

# Create log directories if missing
if (!(Test-Path $PCLogDir)) {
    New-Item -ItemType Directory -Path $PCLogDir -Force | Out-Null
}

# --- 2. LOGGING (Thread-Safe with File Locking) ---
function Write-NecessaryAdminToolLog {
    param([string]$Status, [bool]$ToMaster = $false)
    $Stamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"

    try {
        "[$Stamp] $Status" | Out-File $PCLog -Append -Encoding UTF8 -ErrorAction Stop
    } catch {
        Write-Host "[$Stamp] $Status" -ForegroundColor Yellow
    }

    if ($ToMaster) {
        $LockFile = "$MasterLog.lock"
        $TimeOut = 0
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

# --- 3. UI LOGO (Theme Engine: Orange #FF8533 + Zinc #A1A1AA) ---
function Show-NecessaryAdminToolLogo {
    param([string]$Msg, [string]$Color = "Cyan")
    Clear-Host
    Write-Host ""
    Write-Host " -----------------------------------------------------------" -ForegroundColor DarkYellow
    Write-Host "  " -NoNewline
    Write-Host "NECESSARYADMINTOOL" -ForegroundColor DarkYellow -NoNewline
    Write-Host " | " -ForegroundColor Gray -NoNewline
    Write-Host "Feature Update Suite v1.0" -ForegroundColor Gray
    Write-Host " -----------------------------------------------------------" -ForegroundColor DarkYellow
    Write-Host ""
    if ($Msg) {
        Write-Host "  STATUS: " -ForegroundColor Gray -NoNewline
        Write-Host $Msg -ForegroundColor $Color
    }
    Write-Host ""
}

# --- 4. HARDWARE COMPATIBILITY CHECK ---
Show-NecessaryAdminToolLogo -Msg "Hardware Compatibility Check..."

# Check TPM (try-catch for older systems)
$TPM = try {
    (Get-Tpm -ErrorAction Stop).TpmPresent
} catch {
    Write-NecessaryAdminToolLog -Status "TPM_CHECK_FAILED_ASSUMING_FALSE" -ToMaster $false
    $false
}

# Check Secure Boot (gracefully handle legacy BIOS)
$SecureBoot = try {
    Confirm-SecureBootUEFI -ErrorAction Stop
} catch {
    Write-NecessaryAdminToolLog -Status "SECUREBOOT_CHECK_FAILED_ASSUMING_FALSE" -ToMaster $false
    $false
}

# Check disk space
$FreeGB = [math]::Round(((Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='C:'").FreeSpace / 1GB), 2)

# Hardware compatibility validation
if (!$TPM -or !$SecureBoot -or $FreeGB -lt $MIN_DISK_SPACE_GB) {
    $Reasons = @()
    if (!$TPM) { $Reasons += "No TPM" }
    if (!$SecureBoot) { $Reasons += "Secure Boot disabled" }
    if ($FreeGB -lt $MIN_DISK_SPACE_GB) { $Reasons += "${FreeGB}GB free (need ${MIN_DISK_SPACE_GB}GB)" }

    $ReasonText = $Reasons -join ", "
    Write-NecessaryAdminToolLog -Status "FAILED_HW_COMPATIBILITY_$ReasonText" -ToMaster $true
    Show-NecessaryAdminToolLogo -Msg "INCOMPATIBLE HARDWARE: $ReasonText" "Red"
    Write-Host "`nUpgrade cannot proceed due to hardware requirements." -ForegroundColor Red
    Start-Sleep -Seconds 5
    exit 1  # ManageEngine/RMM exit code for failure
}

Write-NecessaryAdminToolLog -Status "HW_COMPAT_CHECK_PASSED_TPM_SECUREBOOT_DISK_OK" -ToMaster $false

# --- 5. CLOUD UPDATE FALLBACK FUNCTION ---
function Run-CloudUpdate {
    Show-NecessaryAdminToolLogo -Msg "Using Cloud-Based Windows Update..." "Yellow"
    Write-NecessaryAdminToolLog -Status "METHOD_CLOUD_START" -ToMaster $true

    # Check for PSWindowsUpdate module
    if (!(Get-Module -ListAvailable -Name PSWindowsUpdate)) {
        Write-Host "`nInstalling PSWindowsUpdate module..." -ForegroundColor Cyan
        try {
            [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
            Install-Module PSWindowsUpdate -Force -Confirm:$false -SkipPublisherCheck -ErrorAction Stop
            Write-NecessaryAdminToolLog -Status "MODULE_INSTALLED_SUCCESSFULLY" -ToMaster $false
        } catch {
            Write-NecessaryAdminToolLog -Status "ERROR_MODULE_INSTALL_FAILED" -ToMaster $true
            Show-NecessaryAdminToolLogo -Msg "Failed to install PSWindowsUpdate module" "Red"
            Write-Error "Module installation failed: $($_.Exception.Message)"
            exit 1
        }
    }

    # Import module
    try {
        Import-Module PSWindowsUpdate -Force -ErrorAction Stop
    } catch {
        Write-NecessaryAdminToolLog -Status "ERROR_MODULE_IMPORT_FAILED" -ToMaster $true
        exit 1
    }

    # Run cloud update
    try {
        Get-WindowsUpdate -MicrosoftUpdate -Title "Feature update" -AcceptAll -Install -IgnoreReboot -ErrorAction Stop
        Write-NecessaryAdminToolLog -Status "CLOUD_SUCCESS_REBOOT_REQ" -ToMaster $true
        Show-NecessaryAdminToolLogo -Msg "Cloud update completed successfully" "Green"
        exit 0  # Success
    } catch {
        Write-NecessaryAdminToolLog -Status "CLOUD_FAILED_$($_.Exception.Message)" -ToMaster $true
        Show-NecessaryAdminToolLogo -Msg "Cloud update failed: $($_.Exception.Message)" "Red"
        exit 1  # Failure
    }
}

# --- 6. MAIN EXECUTION LOGIC ---

# Check if hostname matches pattern and ISO is available
if ($Comp -like $HostnamePattern -and -not [string]::IsNullOrEmpty($ISOPath) -and (Test-Path $ISOPath -ErrorAction SilentlyContinue)) {

    # Verify ISO is valid size
    $ISOSize = (Get-Item $ISOPath -ErrorAction SilentlyContinue).Length / 1GB
    if ($ISOSize -lt $MIN_ISO_SIZE_GB) {
        Write-NecessaryAdminToolLog -Status "ERROR_ISO_TOO_SMALL_${ISOSize}GB" -ToMaster $true
        Run-CloudUpdate
        exit
    }

    Write-NecessaryAdminToolLog -Status "METHOD_ISO_START_PATTERN_${HostnamePattern}" -ToMaster $true

    $MountedISO = $null
    try {
        # Mount ISO
        Show-NecessaryAdminToolLogo -Msg "Mounting Local ISO..." "Cyan"
        $MountedISO = Mount-DiskImage -ImagePath $ISOPath -PassThru -ErrorAction Stop
        $Drive = ($MountedISO | Get-Volume).DriveLetter
        Write-NecessaryAdminToolLog -Status "ISO_MOUNTED_DRIVE_${Drive}" -ToMaster $false

        # Verify setup.exe exists
        $SetupPath = "${Drive}:\setup.exe"
        if (!(Test-Path $SetupPath)) {
            throw "setup.exe not found on mounted ISO"
        }

        # Run setup with timeout protection
        Write-NecessaryAdminToolLog -Status "ISO_RUNNING" -ToMaster $true
        Show-NecessaryAdminToolLogo -Msg "Upgrading... (Do not turn off - may take up to 2 hours)" "Yellow"

        $Proc = Start-Process $SetupPath -ArgumentList "/auto upgrade /quiet /showoobe none /eula accept /dynamicupdate disable" -PassThru -ErrorAction Stop

        # Wait with timeout (2 hours)
        $Proc | Wait-Process -Timeout $SETUP_TIMEOUT_SECONDS -ErrorAction SilentlyContinue

        if (!$Proc.HasExited) {
            Write-NecessaryAdminToolLog -Status "TIMEOUT_SETUP_KILLED" -ToMaster $true
            $Proc.Kill()
            throw "Setup.exe timed out after $($SETUP_TIMEOUT_SECONDS / 3600) hours"
        }

        # Check exit code
        $ExitCode = $Proc.ExitCode
        Write-NecessaryAdminToolLog -Status "ISO_COMPLETE_CODE_${ExitCode}" -ToMaster $true

        if ($ExitCode -eq 0) {
            Show-NecessaryAdminToolLogo -Msg "ISO upgrade completed successfully" "Green"
            exit 0  # Success
        } else {
            Show-NecessaryAdminToolLogo -Msg "Setup exited with code ${ExitCode}" "Red"
            exit 1  # Failure
        }
    }
    catch {
        Write-NecessaryAdminToolLog -Status "ISO_FAILED_FALLBACK_CLOUD_$($_.Exception.Message)" -ToMaster $true
        Show-NecessaryAdminToolLogo -Msg "ISO method failed, trying cloud update..." "Yellow"
        Run-CloudUpdate
    }
    finally {
        # Always dismount ISO, even if script crashes
        if ($MountedISO) {
            try {
                Dismount-DiskImage -ImagePath $ISOPath -ErrorAction Stop
                Write-NecessaryAdminToolLog -Status "ISO_DISMOUNTED" -ToMaster $false
            } catch {
                Write-NecessaryAdminToolLog -Status "WARNING_ISO_DISMOUNT_FAILED" -ToMaster $false
                # Try forceful dismount
                try {
                    Get-DiskImage -ImagePath $ISOPath | Dismount-DiskImage -ErrorAction SilentlyContinue
                } catch {
                    # Log but don't fail
                }
            }
        }
    }
}
else {
    # Hostname doesn't match pattern or ISO not available - use cloud update
    if ($Comp -notlike $HostnamePattern) {
        Write-NecessaryAdminToolLog -Status "HOSTNAME_PATTERN_MISMATCH_USING_CLOUD" -ToMaster $false
    } else {
        Write-NecessaryAdminToolLog -Status "ISO_NOT_FOUND_USING_CLOUD" -ToMaster $false
    }
    Run-CloudUpdate
}
