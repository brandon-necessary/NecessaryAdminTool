# TAG: #SECURITY_CRITICAL #AUTOMATED_TESTING #CI_CD #VERSION_2_0
# Automated Security Test Runner for NecessaryAdminTool
# Executes comprehensive security test suite and generates reports

param(
    [switch]$FailOnError = $true,
    [switch]$GenerateReport = $true,
    [string]$OutputPath = ".\TestResults",
    [double]$MinimumScore = 90.0,
    [switch]$Verbose = $false
)

# Script configuration
$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$TestProject = Join-Path $ScriptDir "NecessaryAdminTool.SecurityTests\NecessaryAdminTool.SecurityTests.csproj"
$NUnitConsole = Join-Path $ScriptDir "packages\NUnit.ConsoleRunner.3.16.3\tools\nunit3-console.exe"

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Security Test Suite Runner" -ForegroundColor Cyan
Write-Host "  NecessaryAdminTool v2.0" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Check if test project exists
if (-not (Test-Path $TestProject)) {
    Write-Host "[ERROR] Test project not found: $TestProject" -ForegroundColor Red
    exit 1
}

# Create output directory
if (-not (Test-Path $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
    Write-Host "[INFO] Created output directory: $OutputPath" -ForegroundColor Green
}

# Step 1: Restore NuGet packages
Write-Host "[STEP 1/5] Restoring NuGet packages..." -ForegroundColor Yellow
try {
    $nugetPath = Join-Path $ScriptDir ".nuget\nuget.exe"
    if (-not (Test-Path $nugetPath)) {
        Write-Host "[INFO] Downloading NuGet.exe..." -ForegroundColor Gray
        $nugetDir = Join-Path $ScriptDir ".nuget"
        if (-not (Test-Path $nugetDir)) {
            New-Item -ItemType Directory -Path $nugetDir -Force | Out-Null
        }
        Invoke-WebRequest -Uri "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe" -OutFile $nugetPath
    }

    & $nugetPath restore (Join-Path $ScriptDir "NecessaryAdminTool.sln") -NonInteractive
    if ($LASTEXITCODE -ne 0) {
        throw "NuGet restore failed with exit code $LASTEXITCODE"
    }
    Write-Host "[SUCCESS] NuGet packages restored" -ForegroundColor Green
} catch {
    Write-Host "[ERROR] Failed to restore NuGet packages: $_" -ForegroundColor Red
    if ($FailOnError) { exit 1 }
}

# Step 2: Build test project
Write-Host "[STEP 2/5] Building test project..." -ForegroundColor Yellow
try {
    $msbuildPath = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" `
        -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe `
        -prerelease | Select-Object -First 1

    if (-not $msbuildPath) {
        throw "MSBuild not found. Please install Visual Studio 2019 or later."
    }

    & $msbuildPath $TestProject /p:Configuration=Release /v:minimal /nologo
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE"
    }
    Write-Host "[SUCCESS] Test project built successfully" -ForegroundColor Green
} catch {
    Write-Host "[ERROR] Failed to build test project: $_" -ForegroundColor Red
    if ($FailOnError) { exit 1 }
}

# Step 3: Run security tests
Write-Host "[STEP 3/5] Executing security tests..." -ForegroundColor Yellow
$testStartTime = Get-Date
$testAssembly = Join-Path $ScriptDir "NecessaryAdminTool.SecurityTests\bin\Release\NecessaryAdminTool.SecurityTests.dll"
$testResultFile = Join-Path $OutputPath "TestResults.xml"

try {
    if (-not (Test-Path $testAssembly)) {
        throw "Test assembly not found: $testAssembly"
    }

    # Check if NUnit Console Runner is available
    if (-not (Test-Path $NUnitConsole)) {
        Write-Host "[INFO] NUnit Console Runner not found, using dotnet test instead..." -ForegroundColor Gray

        # Alternative: use dotnet test
        dotnet test $TestProject --configuration Release --no-build --logger "trx;LogFileName=TestResults.trx" --results-directory $OutputPath
        if ($LASTEXITCODE -ne 0) {
            throw "Tests failed with exit code $LASTEXITCODE"
        }
    } else {
        # Run with NUnit Console
        $nunitArgs = @(
            $testAssembly,
            "--result=$testResultFile",
            "--out=$OutputPath\TestOutput.txt",
            "--err=$OutputPath\TestErrors.txt"
        )

        if ($Verbose) {
            $nunitArgs += "--trace=Verbose"
        }

        & $NUnitConsole $nunitArgs
        $testExitCode = $LASTEXITCODE
    }

    $testEndTime = Get-Date
    $testDuration = ($testEndTime - $testStartTime).TotalSeconds

    Write-Host "[SUCCESS] Security tests completed in $($testDuration.ToString('F2')) seconds" -ForegroundColor Green
} catch {
    Write-Host "[ERROR] Test execution failed: $_" -ForegroundColor Red
    if ($FailOnError) { exit 1 }
}

# Step 4: Parse test results
Write-Host "[STEP 4/5] Analyzing test results..." -ForegroundColor Yellow
try {
    $resultsFound = $false
    $totalTests = 0
    $passedTests = 0
    $failedTests = 0
    $skippedTests = 0

    # Try to parse NUnit XML results
    if (Test-Path $testResultFile) {
        [xml]$testResults = Get-Content $testResultFile
        $testRun = $testResults.'test-run'

        $totalTests = [int]$testRun.total
        $passedTests = [int]$testRun.passed
        $failedTests = [int]$testRun.failed
        $skippedTests = [int]$testRun.skipped
        $resultsFound = $true
    }
    # Try to parse TRX results
    elseif (Test-Path (Join-Path $OutputPath "TestResults.trx")) {
        [xml]$trxResults = Get-Content (Join-Path $OutputPath "TestResults.trx")
        $counters = $trxResults.TestRun.ResultSummary.Counters

        $totalTests = [int]$counters.total
        $passedTests = [int]$counters.passed
        $failedTests = [int]$counters.failed
        $skippedTests = [int]$counters.notExecuted
        $resultsFound = $true
    }

    if (-not $resultsFound) {
        throw "Could not find test results file"
    }

    # Calculate security score
    $securityScore = if ($totalTests -gt 0) { ($passedTests / $totalTests) * 100 } else { 0 }

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  SECURITY TEST RESULTS" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Total Tests:    $totalTests" -ForegroundColor White
    Write-Host "Passed:         $passedTests" -ForegroundColor Green
    Write-Host "Failed:         $failedTests" -ForegroundColor $(if ($failedTests -gt 0) { "Red" } else { "Green" })
    Write-Host "Skipped:        $skippedTests" -ForegroundColor Yellow
    Write-Host "Duration:       $($testDuration.ToString('F2'))s" -ForegroundColor White
    Write-Host ""
    Write-Host "Security Score: $($securityScore.ToString('F2'))%" -ForegroundColor $(
        if ($securityScore -ge 95) { "Green" }
        elseif ($securityScore -ge $MinimumScore) { "Yellow" }
        else { "Red" }
    )
    Write-Host "Minimum Score:  $MinimumScore%" -ForegroundColor White
    Write-Host "Status:         $(if ($securityScore -ge $MinimumScore) { 'PASS' } else { 'FAIL' })" -ForegroundColor $(
        if ($securityScore -ge $MinimumScore) { "Green" } else { "Red" }
    )
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""

} catch {
    Write-Host "[ERROR] Failed to analyze test results: $_" -ForegroundColor Red
    if ($FailOnError) { exit 1 }
}

# Step 5: Generate report
if ($GenerateReport) {
    Write-Host "[STEP 5/5] Generating security report..." -ForegroundColor Yellow
    try {
        $reportFile = Join-Path $OutputPath "SecurityReport.txt"
        $jsonReportFile = Join-Path $OutputPath "SecurityReport.json"

        $report = @"
================================================================================
  SECURITY TEST REPORT
  NecessaryAdminTool v2.0
================================================================================

Generated: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss") UTC
Test Duration: $($testDuration.ToString('F2')) seconds

OVERALL SECURITY SCORE
--------------------------------------------------------------------------------
Score: $($securityScore.ToString('F2'))%
Status: $(if ($securityScore -ge $MinimumScore) { 'PASS' } else { 'FAIL' }) (Minimum: $MinimumScore%)

TEST SUMMARY
--------------------------------------------------------------------------------
Total Tests:   $totalTests
Passed:        $passedTests ($([math]::Round(($passedTests / $totalTests) * 100, 1))%)
Failed:        $failedTests ($([math]::Round(($failedTests / $totalTests) * 100, 1))%)
Skipped:       $skippedTests

RECOMMENDATIONS
--------------------------------------------------------------------------------
"@

        if ($securityScore -ge 95) {
            $report += "`nEXCELLENT: Security posture is strong (95%+ pass rate)`n"
        } elseif ($securityScore -ge $MinimumScore) {
            $report += "`nGOOD: Security score meets minimum requirements ($($securityScore.ToString('F2'))% >= $MinimumScore%)`n"
        } else {
            $report += "`nCRITICAL: Overall security score ($($securityScore.ToString('F2'))%) is below minimum threshold ($MinimumScore%)`n"
            $report += "Action Required: Review and fix all failed security tests before deployment`n"
        }

        if ($failedTests -gt 0) {
            $report += "`nFailed Tests: $failedTests security tests failed`n"
            $report += "Review test results for details on specific vulnerabilities`n"
        }

        if ($testDuration -gt 5.0) {
            $report += "`nPERFORMANCE: Test execution time ($($testDuration.ToString('F2'))s) exceeds target (5s)`n"
            $report += "Consider optimizing slow tests for CI/CD pipeline`n"
        }

        $report += "`n" + "=" * 80 + "`n"

        # Save text report
        $report | Out-File -FilePath $reportFile -Encoding UTF8
        Write-Host "[SUCCESS] Security report saved to: $reportFile" -ForegroundColor Green

        # Generate JSON report
        $jsonReport = @{
            generatedAt = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ssZ")
            overallScore = [math]::Round($securityScore, 2)
            meetsMinimumScore = $securityScore -ge $MinimumScore
            minimumRequiredScore = $MinimumScore
            totalTests = $totalTests
            passedTests = $passedTests
            failedTests = $failedTests
            skippedTests = $skippedTests
            executionTimeSeconds = [math]::Round($testDuration, 2)
        } | ConvertTo-Json -Depth 10

        $jsonReport | Out-File -FilePath $jsonReportFile -Encoding UTF8
        Write-Host "[SUCCESS] JSON report saved to: $jsonReportFile" -ForegroundColor Green

    } catch {
        Write-Host "[WARNING] Failed to generate report: $_" -ForegroundColor Yellow
    }
} else {
    Write-Host "[STEP 5/5] Skipping report generation" -ForegroundColor Gray
}

# Exit with appropriate code
Write-Host ""
if ($securityScore -lt $MinimumScore -and $FailOnError) {
    Write-Host "[FAILED] Security tests did not meet minimum score threshold" -ForegroundColor Red
    exit 1
} elseif ($failedTests -gt 0 -and $FailOnError) {
    Write-Host "[FAILED] Some security tests failed" -ForegroundColor Red
    exit 1
} else {
    Write-Host "[SUCCESS] All security checks passed!" -ForegroundColor Green
    exit 0
}
