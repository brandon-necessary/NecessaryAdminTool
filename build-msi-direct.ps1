$ErrorActionPreference = "Stop"
$candle = 'C:\Program Files (x86)\WiX Toolset v3.11\bin\candle.exe'
$light  = 'C:\Program Files (x86)\WiX Toolset v3.11\bin\light.exe'
$version = '3.1.0'

Set-Location (Join-Path $PSScriptRoot 'Installer')

if (-not (Test-Path 'obj'))    { New-Item -ItemType Directory obj    | Out-Null }
if (-not (Test-Path 'Output')) { New-Item -ItemType Directory Output | Out-Null }

$msiOutput = "Output\NecessaryAdminTool-$version-Setup.msi"
$exeOutput = "Output\NecessaryAdminTool-$version-Setup.exe"

# ============================================================
# PHASE 1: Build MSI
# ============================================================

Write-Host "[1/4] Compiling MSI source (candle)..." -ForegroundColor Yellow
& $candle 'Product.wxs' `
    "-dVersion=$version" `
    -ext WixNetFxExtension `
    -ext WixUtilExtension `
    -out 'obj\Product.wixobj'
if ($LASTEXITCODE -ne 0) { Write-Host "Candle (MSI) FAILED: $LASTEXITCODE" -ForegroundColor Red; exit $LASTEXITCODE }

Write-Host "[2/4] Linking MSI (light)..." -ForegroundColor Yellow
& $light 'obj\Product.wixobj' `
    -ext WixUIExtension `
    -ext WixNetFxExtension `
    -ext WixUtilExtension `
    -out $msiOutput `
    -sval
if ($LASTEXITCODE -ne 0) { Write-Host "Light (MSI) FAILED: $LASTEXITCODE" -ForegroundColor Red; exit $LASTEXITCODE }

$msiPath = Join-Path $PSScriptRoot "Installer\$msiOutput"
$msiMB   = [math]::Round((Get-Item $msiPath).Length / 1MB, 2)
Write-Host "  MSI: $msiPath ($msiMB MB)" -ForegroundColor Cyan

# ============================================================
# PHASE 2: Build EXE Bundle (Burn bootstrapper with .NET check)
# ============================================================

Write-Host ""
Write-Host "[3/4] Compiling Bundle source (candle)..." -ForegroundColor Yellow
& $candle 'Bundle.wxs' `
    "-dBundleVersion=$version" `
    -ext WixNetFxExtension `
    -ext WixUtilExtension `
    -ext WixBalExtension `
    -out 'obj\Bundle.wixobj'
if ($LASTEXITCODE -ne 0) { Write-Host "Candle (Bundle) FAILED: $LASTEXITCODE" -ForegroundColor Red; exit $LASTEXITCODE }

Write-Host "[4/4] Linking EXE bundle (light)..." -ForegroundColor Yellow
& $light 'obj\Bundle.wixobj' `
    -ext WixNetFxExtension `
    -ext WixUtilExtension `
    -ext WixBalExtension `
    -out $exeOutput `
    -sval
if ($LASTEXITCODE -ne 0) { Write-Host "Light (Bundle) FAILED: $LASTEXITCODE" -ForegroundColor Red; exit $LASTEXITCODE }

$exePath = Join-Path $PSScriptRoot "Installer\$exeOutput"
$exeMB   = [math]::Round((Get-Item $exePath).Length / 1MB, 2)

Write-Host ""
Write-Host "====================================" -ForegroundColor Green
Write-Host "  BUILD SUCCESSFUL" -ForegroundColor Green
Write-Host "====================================" -ForegroundColor Green
Write-Host ""
Write-Host "  MSI (for IT/SCCM):      $msiPath ($msiMB MB)" -ForegroundColor Cyan
Write-Host "  EXE (with .NET check):  $exePath ($exeMB MB)" -ForegroundColor Cyan
Write-Host ""
Write-Host "  EXE install flags:" -ForegroundColor Gray
Write-Host "    /quiet   - fully silent" -ForegroundColor Gray
Write-Host "    /passive - progress bar only" -ForegroundColor Gray
Write-Host "    /install - install (default)" -ForegroundColor Gray
Write-Host "    /uninstall - remove app" -ForegroundColor Gray
