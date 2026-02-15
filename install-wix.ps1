# Install WiX Toolset Automatically
# TAG: #AUTO_UPDATE_INSTALLER #WIX_INSTALLER #BUILD_AUTOMATION #POWERSHELL
# FUTURE CLAUDES: Update WiX download URL if newer version released
# Run as Administrator

param([switch]$Silent)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  WiX Toolset 3.11 Installer" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if already installed
if (Test-Path "${env:WIX}bin\candle.exe") {
    Write-Host "✓ WiX Toolset is already installed!" -ForegroundColor Green
    Write-Host "  Location: ${env:WIX}" -ForegroundColor White
    exit 0
}

# Download URL
$wixUrl = "https://github.com/wixtoolset/wix3/releases/download/wix3112rtm/wix311.exe"
$downloadPath = Join-Path $env:TEMP "wix311.exe"

Write-Host "[1/3] Downloading WiX Toolset 3.11..." -ForegroundColor Yellow
Write-Host "  URL: $wixUrl" -ForegroundColor Gray

try {
    Invoke-WebRequest -Uri $wixUrl -OutFile $downloadPath -UseBasicParsing
    Write-Host "  ✓ Downloaded" -ForegroundColor Green
}
catch {
    Write-Host "ERROR: Download failed!" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}

# Install
Write-Host ""
Write-Host "[2/3] Installing WiX Toolset..." -ForegroundColor Yellow
Write-Host "  (This may take a few minutes)" -ForegroundColor Gray

$installArgs = @("/quiet", "/norestart")
if ($Silent) {
    $installArgs += "/passive"
}

$process = Start-Process -FilePath $downloadPath -ArgumentList $installArgs -Wait -PassThru

if ($process.ExitCode -ne 0) {
    Write-Host "ERROR: Installation failed with code $($process.ExitCode)" -ForegroundColor Red
    exit $process.ExitCode
}

Write-Host "  ✓ Installed" -ForegroundColor Green

# Set environment variable
Write-Host ""
Write-Host "[3/3] Setting environment variable..." -ForegroundColor Yellow

$wixPath = "C:\Program Files (x86)\WiX Toolset v3.11\"
[Environment]::SetEnvironmentVariable("WIX", $wixPath, "Machine")
$env:WIX = $wixPath

Write-Host "  ✓ WIX environment variable set" -ForegroundColor Green

# Cleanup
Remove-Item $downloadPath -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  ✓ WIX TOOLSET INSTALLED!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "IMPORTANT: Restart PowerShell to use WiX commands" -ForegroundColor Yellow
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Close and reopen PowerShell" -ForegroundColor White
Write-Host "  2. Run: .\build-installer.ps1" -ForegroundColor White
Write-Host ""
