# Tag Verification Script
# TAG: #AUTO_UPDATE_SYSTEM #VERIFICATION #MAINTENANCE
# Verifies all auto-update and version tags are present

param(
    [switch]$Detailed,
    [switch]$ShowMissing
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Tag Verification Script" -ForegroundColor Cyan
Write-Host "  NecessaryAdminTool v1.0" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$categories = @{
    "FUTURE CLAUDES Notes" = @{
        Pattern = "FUTURE CLAUDES"
        Files = @(
            "NecessaryAdminTool\Properties\AssemblyInfo.cs",
            "NecessaryAdminTool\AboutWindow.xaml",
            "Installer\Product.wxs",
            "install-wix.ps1",
            "README.md",
            "README_COMPREHENSIVE.md",
            "FEATURES.md",
            "FAQ.md",
            "OPTIMIZATIONS.md",
            "DATABASE_GUIDE.md",
            "NecessaryAdminTool\DatabaseSetupWizard.xaml",
            "AUTO_UPDATE_GUIDE.md"
        )
    }
    "Installer System" = @{
        Pattern = "TAG:.*#AUTO_UPDATE_INSTALLER|TAG:.*#WIX_INSTALLER|TAG:.*#BUILD"
        Files = @(
            "Installer\Product.wxs",
            "build-installer.ps1",
            "install-wix.ps1",
            "NecessaryAdminTool\UpdateManager.cs",
            "INSTALLER_GUIDE.md",
            "Installer\README.md",
            "BUILD_SCRIPTS_README.md"
        )
    }
    "Version Management" = @{
        Pattern = "TAG:.*#AUTO_UPDATE_VERSION|TAG:.*#VERSION_DISPLAY"
        Files = @(
            "NecessaryAdminTool\AboutWindow.xaml",
            "NecessaryAdminTool\Properties\AssemblyInfo.cs",
            "README.md"
        )
    }
    "Database System" = @{
        Pattern = "TAG:.*#AUTO_UPDATE_DATABASE|TAG:.*#DATABASE"
        Files = @(
            "DATABASE_GUIDE.md",
            "DATABASE_INSTALLER_GUIDE.md",
            "NecessaryAdminTool\DatabaseSetupWizard.xaml",
            "NecessaryAdminTool\Data\AccessDataProvider.cs",
            "NecessaryAdminTool\Data\CsvDataProvider.cs",
            "NecessaryAdminTool\Data\SqlServerDataProvider.cs"
        )
    }
    "Documentation" = @{
        Pattern = "TAG:.*#AUTO_UPDATE_README|TAG:.*#AUTO_UPDATE_FEATURES|TAG:.*#AUTO_UPDATE_FAQ"
        Files = @(
            "README.md",
            "README_COMPREHENSIVE.md",
            "FEATURES.md",
            "FAQ.md",
            "OPTIMIZATIONS.md"
        )
    }
    "Setup System" = @{
        Pattern = "TAG:.*#SETUP_WIZARD|TAG:.*#FIRST_RUN"
        Files = @(
            "NecessaryAdminTool\SetupWizardWindow.xaml.cs",
            "NecessaryAdminTool\OptionsWindow.xaml.cs",
            "NecessaryAdminTool\DatabaseSetupWizard.xaml"
        )
    }
}

$totalFiles = 0
$totalTagged = 0
$totalMissing = 0

foreach ($category in $categories.Keys) {
    Write-Host "[$category]" -ForegroundColor Yellow

    $files = $categories[$category].Files
    $pattern = $categories[$category].Pattern
    $tagged = 0
    $missing = 0

    foreach ($file in $files) {
        $fullPath = Join-Path $PSScriptRoot $file
        $totalFiles++

        if (Test-Path $fullPath) {
            $content = Get-Content $fullPath -First 15 -Raw -ErrorAction SilentlyContinue

            if ($content -match $pattern) {
                $tagged++
                $totalTagged++
                if ($Detailed) {
                    Write-Host "  ✓ $file" -ForegroundColor Green
                }
            } else {
                $missing++
                $totalMissing++
                if ($ShowMissing) {
                    Write-Host "  ✗ $file - MISSING TAG" -ForegroundColor Red
                }
            }
        } else {
            $missing++
            $totalMissing++
            if ($ShowMissing) {
                Write-Host "  ? $file - FILE NOT FOUND" -ForegroundColor Yellow
            }
        }
    }

    $percent = if ($files.Count -gt 0) { [math]::Round(($tagged / $files.Count) * 100) } else { 0 }
    $percentText = "$percent%"
    Write-Host "  Tagged: $tagged/$($files.Count) ($percentText)" -ForegroundColor $(if ($percent -eq 100) { "Green" } else { "Yellow" })
    Write-Host ""
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Total Files Checked: $totalFiles"
Write-Host "Tagged Files:        $totalTagged" -ForegroundColor Green
Write-Host "Missing Tags:        $totalMissing" -ForegroundColor $(if ($totalMissing -eq 0) { "Green" } else { "Red" })
Write-Host ""

$overallPercent = [math]::Round(($totalTagged / $totalFiles) * 100)
$overallPercentText = "$overallPercent%"
Write-Host "Overall Coverage: $overallPercentText" -ForegroundColor $(if ($overallPercent -eq 100) { "Green" } elseif ($overallPercent -ge 90) { "Yellow" } else { "Red" })
Write-Host ""

if ($totalMissing -eq 0) {
    Write-Host "✓ All critical files are properly tagged!" -ForegroundColor Green
} else {
    Write-Host "⚠ Some files are missing tags. Run with -ShowMissing to see details." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Usage:" -ForegroundColor Cyan
Write-Host "  .\verify-tags.ps1               # Quick summary"
Write-Host "  .\verify-tags.ps1 -Detailed     # Show all files"
Write-Host "  .\verify-tags.ps1 -ShowMissing  # Show only missing tags"
Write-Host ""
