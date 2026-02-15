# NecessaryAdminTool - Automated Installer Builder
# TAG: #AUTO_UPDATE_INSTALLER #BUILD_SCRIPT #AUTOMATION
# Run this script to build the MSI installer

param(
    [string]$Version = "1.2602.0.0",
    [switch]$SkipBuild,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  NecessaryAdminTool Installer Builder" -ForegroundColor Cyan
Write-Host "  Version: $Version" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Paths
$RootDir = $PSScriptRoot
$SolutionFile = Join-Path $RootDir "NecessaryAdminTool.sln"
$InstallerDir = Join-Path $RootDir "Installer"
$DepsDir = Join-Path $InstallerDir "Dependencies"
$OutputDir = Join-Path $InstallerDir "Output"
$ReleaseDir = Join-Path $RootDir "NecessaryAdminTool\bin\Release"

# Check prerequisites
Write-Host "[1/6] Checking prerequisites..." -ForegroundColor Yellow

# Check for MSBuild
$MSBuildPath = "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"
if (-not (Test-Path $MSBuildPath)) {
    Write-Host "ERROR: MSBuild not found at: $MSBuildPath" -ForegroundColor Red
    Write-Host "Please install Visual Studio 2022" -ForegroundColor Red
    exit 1
}
Write-Host "  ✓ MSBuild found" -ForegroundColor Green

# Check for WiX
$CandlePath = "${env:WIX}bin\candle.exe"
$LightPath = "${env:WIX}bin\light.exe"

if (-not (Test-Path $CandlePath)) {
    Write-Host "ERROR: WiX Toolset not found!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please install WiX Toolset v3.11+" -ForegroundColor Yellow
    Write-Host "Download: https://github.com/wixtoolset/wix3/releases" -ForegroundColor Yellow
    Write-Host "Or run: choco install wixtoolset" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "After installing, set WIX environment variable and restart PowerShell" -ForegroundColor Yellow
    exit 1
}
Write-Host "  ✓ WiX Toolset found" -ForegroundColor Green

# Create directories
if (-not (Test-Path $DepsDir)) {
    New-Item -ItemType Directory -Path $DepsDir -Force | Out-Null
}
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

# Check for ACE installer
$ACEInstaller = Join-Path $DepsDir "AccessDatabaseEngine_X64.exe"
if (-not (Test-Path $ACEInstaller)) {
    Write-Host "WARNING: ACE Database Engine not found!" -ForegroundColor Yellow
    Write-Host "  Expected: $ACEInstaller" -ForegroundColor Yellow
    Write-Host "  Download from: https://www.microsoft.com/en-us/download/details.aspx?id=54920" -ForegroundColor Yellow
    Write-Host ""
    $response = Read-Host "Continue without ACE bundling? (y/N)"
    if ($response -ne 'y') {
        exit 1
    }
}
else {
    Write-Host "  ✓ ACE Database Engine found" -ForegroundColor Green
}

# Build application
if (-not $SkipBuild) {
    Write-Host ""
    Write-Host "[2/6] Building NecessaryAdminTool (Release)..." -ForegroundColor Yellow

    $buildArgs = @(
        $SolutionFile,
        "/t:Clean,Build",
        "/p:Configuration=Release",
        "/p:Platform=`"Any CPU`"",
        "/v:minimal",
        "/nologo"
    )

    & $MSBuildPath $buildArgs

    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Build failed with exit code $LASTEXITCODE" -ForegroundColor Red
        exit $LASTEXITCODE
    }

    Write-Host "  ✓ Build successful" -ForegroundColor Green
}
else {
    Write-Host ""
    Write-Host "[2/6] Skipping build (using existing binaries)..." -ForegroundColor Yellow
}

# Verify Release binaries exist
if (-not (Test-Path (Join-Path $ReleaseDir "NecessaryAdminTool.exe"))) {
    Write-Host "ERROR: NecessaryAdminTool.exe not found in Release folder!" -ForegroundColor Red
    Write-Host "  Expected: $ReleaseDir\NecessaryAdminTool.exe" -ForegroundColor Red
    exit 1
}

# Compile WiX source
Write-Host ""
Write-Host "[3/6] Compiling WiX source (candle)..." -ForegroundColor Yellow

Push-Location $InstallerDir

$candleArgs = @(
    "Product.wxs",
    "-dVersion=$Version",
    "-out", "obj\Product.wixobj"
)

if ($Verbose) {
    $candleArgs += "-v"
}

& $CandlePath $candleArgs

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Candle failed with exit code $LASTEXITCODE" -ForegroundColor Red
    Pop-Location
    exit $LASTEXITCODE
}

Write-Host "  ✓ WiX source compiled" -ForegroundColor Green

# Link WiX object
Write-Host ""
Write-Host "[4/6] Linking WiX object (light)..." -ForegroundColor Yellow

$msiOutput = Join-Path $OutputDir "NecessaryAdminTool-$Version-Setup.msi"

$lightArgs = @(
    "obj\Product.wixobj",
    "-ext", "WixUIExtension",
    "-out", $msiOutput,
    "-sval"  # Suppress ICE validation for faster builds
)

if ($Verbose) {
    $lightArgs += "-v"
}

& $LightPath $lightArgs

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Light failed with exit code $LASTEXITCODE" -ForegroundColor Red
    Pop-Location
    exit $LASTEXITCODE
}

Pop-Location

Write-Host "  ✓ MSI linked successfully" -ForegroundColor Green

# Get file size
$msiSize = (Get-Item $msiOutput).Length / 1MB

# Success!
Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  ✓ INSTALLER BUILD SUCCESSFUL!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Output:" -ForegroundColor Cyan
Write-Host "  File: $msiOutput" -ForegroundColor White
Write-Host "  Size: $([math]::Round($msiSize, 2)) MB" -ForegroundColor White
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Cyan
Write-Host "  1. Test on clean VM" -ForegroundColor White
Write-Host "  2. Deploy: msiexec /i `"$msiOutput`" /quiet" -ForegroundColor White
Write-Host "  3. Or double-click to install interactively" -ForegroundColor White
Write-Host ""

# Open output folder
$response = Read-Host "Open output folder? (Y/n)"
if ($response -ne 'n') {
    Start-Process "explorer.exe" -ArgumentList "/select,`"$msiOutput`""
}
