# LDAP Injection Prevention Integration

## Objective
Integrate SecurityValidator into Active Directory operations to prevent LDAP injection attacks across all AD query points.

## Completed Changes

### 1. SecurityValidator.cs Enhancements
**File:** `NecessaryAdminTool/Security/SecurityValidator.cs`

Added comprehensive LDAP security methods:

#### New Methods Added:
- `EscapeLDAPSearchFilter(string input)` - RFC 2254 compliant LDAP filter escaping
  - Escapes special characters: `\ * ( ) \0`
  - Escapes non-ASCII characters to prevent encoding attacks
  - Returns properly escaped string safe for LDAP queries

- `ValidateLDAPFilter(string filter)` - LDAP filter validation
  - Checks for balanced parentheses
  - Detects common injection patterns:
    - `*)(` - Wildcard injection
    - `*)(|` - OR injection
    - `*)(objectClass=*` - Object enumeration
    - `*)(&` - AND injection
    - `*))%00` - Null byte injection
    - Admin/user/cn enumeration patterns
  - Validates no null bytes present
  - Logs all blocked attempts

- `ValidateOUFilter(string ouFilter)` - OU filter validation
  - Validates safe DN characters only
  - Detects injection patterns like `)(` and `*)`
  - Allows null/empty (no filter)

**TAG:** `#SECURITY_CRITICAL #LDAP_INJECTION_PREVENTION`

### 2. ActiveDirectoryManager.cs Integration
**File:** `NecessaryAdminTool/ActiveDirectoryManager.cs`

**Changes:**
- Added `using NecessaryAdminTool.Security;`
- Updated class TAG: `#SECURITY_CRITICAL #LDAP_INJECTION_PREVENTION`

#### GetComputersAsync() - Lines ~95-125
```csharp
// Validate OU filter before use
if (!string.IsNullOrEmpty(ouFilter) && !SecurityValidator.ValidateOUFilter(ouFilter))
{
    LogManager.LogWarning($"[AD] Blocked invalid OU filter: {ouFilter}");
    throw new ArgumentException("Invalid OU filter detected. Possible LDAP injection attempt.");
}

// Sanitize OU filter before building query
string sanitizedOU = SecurityValidator.EscapeLDAPSearchFilter(ouFilter);
filter = $"(&(objectCategory=computer)(distinguishedName=*{sanitizedOU}*))";

// Validate final LDAP filter
if (!SecurityValidator.ValidateLDAPFilter(filter))
{
    LogManager.LogWarning($"[AD] Blocked invalid LDAP filter: {filter}");
    throw new InvalidOperationException("Generated LDAP filter failed security validation.");
}
```

#### GetUsersAsync() - Lines ~220-245
- Same validation pattern applied to user queries
- OU filter validation + sanitization
- Final LDAP filter validation

#### GetGroupsAsync() - Lines ~325-350
- Same validation pattern applied to group queries
- OU filter validation + sanitization
- Final LDAP filter validation

**TAG:** `#SECURITY_CRITICAL #LDAP_INJECTION_PREVENTION`

### 3. OptimizedADScanner.cs Integration
**File:** `NecessaryAdminTool/OptimizedADScanner.cs`

**Changes:**
- Added `using NecessaryAdminTool.Security;`
- Updated class TAG: `#SECURITY_CRITICAL #LDAP_INJECTION_PREVENTION`

#### GetADComputersAsync() - Lines ~54-95
```csharp
// Validate domain controller hostname to prevent injection
if (!SecurityValidator.IsValidHostname(domainController))
{
    LogManager.LogWarning($"[AD Scanner] Invalid domain controller hostname: {domainController}");
    throw new ArgumentException("Invalid domain controller hostname. Possible injection attempt.");
}

// Validate static LDAP filter before use
string filter = "(&(objectCategory=computer)(!(userAccountControl:1.2.840.113556.1.4.803:=2)))";
if (!SecurityValidator.ValidateLDAPFilter(filter))
{
    LogManager.LogWarning($"[AD Scanner] LDAP filter validation failed: {filter}");
    throw new InvalidOperationException("LDAP filter failed security validation.");
}

searcher.Filter = filter;
LogManager.LogDebug($"[AD Scanner] Using validated LDAP filter: {filter}");
```

#### Hostname Validation - Lines ~106-132
```csharp
// Validate hostname before adding to collection
if (!string.IsNullOrEmpty(hostname) && SecurityValidator.IsValidHostname(hostname))
{
    computers.Add(hostname);
}
else if (!string.IsNullOrEmpty(hostname))
{
    LogManager.LogWarning($"[AD Scanner] Rejected invalid hostname from AD: {hostname}");
}
```

**TAG:** `#SECURITY_CRITICAL #LDAP_INJECTION_PREVENTION`

### 4. ADObjectBrowser.xaml.cs Integration
**File:** `NecessaryAdminTool/ADObjectBrowser.xaml.cs`

**Changes:**
- Added `using NecessaryAdminTool.Security;`
- Updated class TAG: `#SECURITY_CRITICAL #LDAP_INJECTION_PREVENTION`

#### InitializeAsync() - Lines ~65-93
```csharp
// Validate domain controller hostname before use
if (!SecurityValidator.IsValidHostname(domainController))
{
    LogManager.LogWarning($"[AD Browser] Invalid domain controller hostname: {domainController}");
    throw new ArgumentException("Invalid domain controller hostname. Possible injection attempt.");
}
```

#### BuildTreeView() - Lines ~130-182
- Added security tags to all pre-built LDAP filters (static, validated)
- Filters remain unchanged as they are hardcoded and safe:
  - `(objectCategory=computer)`
  - `(&(objectCategory=person)(objectClass=user))`
  - `(objectCategory=group)`
  - `(objectCategory=organizationalUnit)`

#### LoadObjectsAsync() - DirectorySearcher Path - Lines ~264-285
```csharp
// Validate LDAP filter before use
string filter = node.Filter ?? "(objectClass=*)";
if (!SecurityValidator.ValidateLDAPFilter(filter))
{
    LogManager.LogWarning($"[AD Browser] Invalid LDAP filter blocked: {filter}");
    throw new InvalidOperationException("LDAP filter failed security validation.");
}

searcher.Filter = filter;
LogManager.LogDebug($"[AD Browser] Using validated LDAP filter: {filter}");
```

**TAG:** `#SECURITY_CRITICAL #LDAP_INJECTION_PREVENTION`

## Remaining Work (MainWindow.xaml.cs)

### File: NecessaryAdminTool/MainWindow.xaml.cs

**Note:** This file has an active linter that prevents edits. The following changes need to be applied manually or after linter is disabled.

#### Location 1: Line ~2924-2943 - User Admin Check
**Current Code:**
```csharp
string ldapPath = string.IsNullOrEmpty(domain) ? "LDAP://" : $"LDAP://{domain}";
root = new DirectoryEntry(ldapPath, username, password, AuthenticationTypes.Secure);

using (var searcher = new DirectorySearcher(root))
{
    searcher.Filter = $"(&(objectCategory=user)(sAMAccountName={cleanUser}))";
    searcher.PropertiesToLoad.Add("memberOf");
    searcher.PropertiesToLoad.Add("distinguishedName");
    var result = searcher.FindOne();
```

**Required Changes:**
```csharp
// TAG: #SECURITY_CRITICAL #LDAP_INJECTION_PREVENTION
// Validate domain before use in LDAP path
if (!string.IsNullOrEmpty(domain) && !SecurityValidator.IsValidHostname(domain))
{
    LogManager.LogWarning($"[AD Auth] Invalid domain name: {domain}");
    return false;
}

string ldapPath = string.IsNullOrEmpty(domain) ? "LDAP://" : $"LDAP://{domain}";
root = new DirectoryEntry(ldapPath, username, password, AuthenticationTypes.Secure);

using (var searcher = new DirectorySearcher(root))
{
    // TAG: #SECURITY_CRITICAL #LDAP_INJECTION_PREVENTION
    // Sanitize username for LDAP filter to prevent injection
    string sanitizedUser = SecurityValidator.EscapeLDAPSearchFilter(cleanUser);
    string filter = $"(&(objectCategory=user)(sAMAccountName={sanitizedUser}))";

    // Validate LDAP filter before use
    if (!SecurityValidator.ValidateLDAPFilter(filter))
    {
        LogManager.LogWarning($"[AD Auth] LDAP filter validation failed: {filter}");
        return false;
    }

    searcher.Filter = filter;
    LogManager.LogDebug($"[AD Auth] Using validated LDAP filter for user lookup");

    searcher.PropertiesToLoad.Add("memberOf");
    searcher.PropertiesToLoad.Add("distinguishedName");
    var result = searcher.FindOne();
```

#### Location 2: Line ~3023-3031 - Nested Group Check
**Current Code:**
```csharp
using (var groupEntry = new DirectoryEntry($"LDAP://{groupDN}"))
using (var groupSearcher = new DirectorySearcher(rootEntry))
{
    // Extract CN from DN
    var cnMatch = System.Text.RegularExpressions.Regex.Match(groupDN, @"^CN=([^,]+)");
    if (cnMatch.Success)
    {
        string groupCN = cnMatch.Groups[1].Value;
        groupSearcher.Filter = $"(&(objectCategory=group)(cn={groupCN}))";
        groupSearcher.PropertiesToLoad.Add("memberOf");
        var groupResult = groupSearcher.FindOne();
```

**Required Changes:**
```csharp
using (var groupEntry = new DirectoryEntry($"LDAP://{groupDN}"))
using (var groupSearcher = new DirectorySearcher(rootEntry))
{
    // Extract CN from DN
    var cnMatch = System.Text.RegularExpressions.Regex.Match(groupDN, @"^CN=([^,]+)");
    if (cnMatch.Success)
    {
        string groupCN = cnMatch.Groups[1].Value;

        // TAG: #SECURITY_CRITICAL #LDAP_INJECTION_PREVENTION
        // Sanitize group CN before using in LDAP filter
        string sanitizedCN = SecurityValidator.EscapeLDAPSearchFilter(groupCN);
        string filter = $"(&(objectCategory=group)(cn={sanitizedCN}))";

        // Validate LDAP filter before use
        if (!SecurityValidator.ValidateLDAPFilter(filter))
        {
            LogManager.LogWarning($"[AD Auth] Group filter validation failed: {filter}");
            continue; // Skip this group
        }

        groupSearcher.Filter = filter;
        LogManager.LogDebug($"[AD Auth] Using validated group filter");

        groupSearcher.PropertiesToLoad.Add("memberOf");
        var groupResult = groupSearcher.FindOne();
```

## Security Benefits

### Defense in Depth
1. **Input Validation** - Validates OU filters, hostnames, and user inputs
2. **Output Encoding** - RFC 2254 compliant LDAP escaping
3. **Pattern Detection** - Blocks known injection patterns
4. **Logging** - All blocked attempts are logged for audit

### Attack Vectors Prevented
1. **Wildcard Injection** - `*)(cn=admin)` → Blocked by parenthesis validation
2. **OR Injection** - `*)(|(uid=*))` → Blocked by pattern detection
3. **Object Enumeration** - `*)(objectClass=*)` → Blocked by pattern detection
4. **Null Byte Attacks** - `admin%00` → Blocked by null byte detection
5. **Encoding Attacks** - Unicode/non-ASCII → Escaped as hex codes

### OWASP Compliance
- OWASP LDAP Injection Prevention Cheat Sheet compliant
- RFC 2254 LDAP filter escaping
- Principle of least privilege (deny-by-default)
- Defense in depth with multiple validation layers

## Testing Recommendations

### Test Cases to Verify
1. Normal AD queries with valid filters - should work
2. OU filter with special characters - should be escaped
3. User input with LDAP metacharacters - should be escaped
4. Malicious injection attempts - should be blocked and logged
5. Edge cases: empty strings, null values, Unicode

### Security Audit
- Review logs for blocked injection attempts
- Verify all LDAP filter constructions use SecurityValidator
- Confirm no direct string interpolation in LDAP filters
- Test with penetration testing tools (LDAP injection scanners)

## Implementation Status

### ✅ Completed
- SecurityValidator.cs - LDAP security methods
- ActiveDirectoryManager.cs - All 3 query methods secured
- OptimizedADScanner.cs - GetADComputersAsync secured
- ADObjectBrowser.xaml.cs - Initialize and LoadObjects secured

### ⚠️ Pending (Manual Edit Required)
- MainWindow.xaml.cs - 2 locations (linter conflict)
  - User admin check LDAP query
  - Nested group check LDAP query

### 📋 Follow-up Tasks
1. Complete MainWindow.xaml.cs edits when linter allows
2. Run security test suite
3. Review logs for any blocked legitimate queries
4. Update security documentation
5. Train team on LDAP injection prevention

## Tags Applied
All changes are tagged with:
- `#SECURITY_CRITICAL`
- `#LDAP_INJECTION_PREVENTION`

For easy searching and audit tracking.
