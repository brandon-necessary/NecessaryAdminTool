# Code Quality & Standards Checklist

**Purpose:** Ensure all code meets NecessaryAdminTool quality and security standards
**Last Updated:** February 15, 2026
**Required For:** All new code, refactoring, and bug fixes

---

## 📋 Pre-Coding Checklist

Before writing any new code:

- [ ] Read existing code in the area you're modifying
- [ ] Understand the current architecture and patterns
- [ ] Check if similar functionality already exists
- [ ] Review related documentation (CLAUDE.md, guides)
- [ ] Understand security implications (SecurityValidator)

---

## 🛡️ Security Standards (MANDATORY)

### ✅ 1. Input Validation (ALWAYS validate user input)

**Required:** All user input MUST be validated using SecurityValidator

```csharp
// TAG: #SECURITY_CRITICAL #INPUT_VALIDATION
using NecessaryAdminTool.Security;

// PowerShell scripts
if (!SecurityValidator.ValidatePowerShellScript(scriptContent))
{
    ToastManager.ShowError("Script contains potentially dangerous commands");
    return;
}

// File paths
if (!SecurityValidator.ValidateFilePath(filePath, allowedDirectory))
{
    LogManager.LogWarning($"Blocked path traversal attempt: {filePath}");
    return;
}

// LDAP filters
if (!SecurityValidator.ValidateLDAPFilter(ldapFilter))
{
    throw new SecurityException("Invalid LDAP filter");
}

// Computer names
if (!SecurityValidator.ValidateComputerName(computerName))
{
    ToastManager.ShowError("Invalid computer name format");
    return;
}

// IP addresses
if (!SecurityValidator.ValidateIPAddress(ipAddress))
{
    ToastManager.ShowError("Invalid IP address format");
    return;
}

// Usernames
if (!SecurityValidator.ValidateUsername(username))
{
    ToastManager.ShowError("Invalid username format");
    return;
}
```

### ✅ 2. Output Sanitization

**Always sanitize before using in commands:**

```csharp
// TAG: #SECURITY_CRITICAL #OUTPUT_SANITIZATION
string safeComputerName = SecurityValidator.SanitizeForPowerShell(computerName);
string safeFilter = SecurityValidator.EscapeLDAPSearchFilter(userInput);
```

### ✅ 3. Security Tagging

**All security-critical code MUST be tagged:**

```csharp
// TAG: #SECURITY_CRITICAL #POWERSHELL_INJECTION_PREVENTION
// TAG: #SECURITY_CRITICAL #PATH_TRAVERSAL_PREVENTION
// TAG: #SECURITY_CRITICAL #LDAP_INJECTION_PREVENTION
// TAG: #SECURITY_CRITICAL #COMMAND_INJECTION_PREVENTION
// TAG: #SECURITY_CRITICAL #RATE_LIMITING
```

### ✅ 4. Security Checklist

Before committing code with user input:

- [ ] All user input validated with SecurityValidator
- [ ] All command parameters sanitized
- [ ] All file paths validated against traversal
- [ ] All LDAP filters escaped
- [ ] All PowerShell scripts scanned for malicious patterns
- [ ] All security code tagged #SECURITY_CRITICAL
- [ ] All security violations logged
- [ ] All errors shown via ToastManager (not MessageBox)

---

## 🏷️ Tagging Requirements (MANDATORY)

### ✅ 1. Standard Tags

**All code must include appropriate tags:**

```csharp
/// <summary>
/// Class description
/// TAG: #CATEGORY #FEATURE #SUBCATEGORY
/// </summary>
```

### ✅ 2. Tag Categories

**Security:**
- `#SECURITY_CRITICAL` - All security-sensitive code
- `#POWERSHELL_INJECTION_PREVENTION`
- `#PATH_TRAVERSAL_PREVENTION`
- `#LDAP_INJECTION_PREVENTION`
- `#COMMAND_INJECTION_PREVENTION`
- `#RATE_LIMITING`
- `#ENCRYPTION`
- `#AUTHENTICATION`

**UI/UX:**
- `#AUTO_UPDATE_UI_ENGINE` - All UI code
- `#FLUENT_DESIGN` - Fluent Design implementation
- `#TOAST_NOTIFICATIONS` - Toast notification usage
- `#COMMAND_PALETTE` - Command Palette integration
- `#KEYBOARD_SHORTCUTS` - Keyboard shortcut handling
- `#SKELETON_LOADERS` - Loading states
- `#CARD_VIEW` - Card-based layouts
- `#NATIVE_WPF` - Native WPF controls

**Architecture:**
- `#AD_INTEGRATION` - Active Directory operations
- `#DATABASE_ACCESS` - Database operations
- `#REMOTE_CONTROL` - Remote management
- `#SCRIPT_EXECUTION` - PowerShell execution
- `#FILE_OPERATIONS` - File I/O operations
- `#ASYNC_OPERATIONS` - Async/await code

**Features:**
- `#OPTIMIZATION` - Performance improvements
- `#CACHING` - Caching implementation
- `#LOGGING` - Logging operations
- `#ERROR_HANDLING` - Error handling
- `#VALIDATION` - Input validation
- `#CONFIGURATION` - Settings/config

### ✅ 3. Tag Placement

```csharp
// Class level
/// <summary>
/// Manages Active Directory scanning operations
/// TAG: #AD_INTEGRATION #OPTIMIZATION #ASYNC_OPERATIONS
/// </summary>
public class OptimizedADScanner
{
    // Method level
    /// <summary>
    /// Scans AD computers with parallel processing
    /// TAG: #AD_INTEGRATION #ASYNC_OPERATIONS #CACHING
    /// </summary>
    public async Task<List<Computer>> ScanComputersAsync()
    {
        // Code block level
        // TAG: #SECURITY_CRITICAL #LDAP_INJECTION_PREVENTION
        if (!SecurityValidator.ValidateLDAPFilter(filter))
        {
            LogManager.LogWarning("Invalid LDAP filter blocked");
            return null;
        }

        // TAG: #ASYNC_OPERATIONS #ERROR_HANDLING
        try
        {
            return await Task.Run(() => PerformScan());
        }
        catch (Exception ex)
        {
            // TAG: #ERROR_HANDLING #LOGGING #TOAST_NOTIFICATIONS
            LogManager.LogError("ScanComputersAsync", ex);
            ToastManager.ShowError($"Scan failed: {ex.Message}");
            return null;
        }
    }
}
```

---

## 📝 Documentation Standards

### ✅ 1. XML Documentation (Required for public members)

```csharp
/// <summary>
/// Brief description of what this does (one sentence)
/// </summary>
/// <param name="paramName">Parameter description</param>
/// <returns>Return value description</returns>
/// <exception cref="ArgumentNullException">When parameter is null</exception>
/// <remarks>
/// Additional notes, usage examples, or important details
/// TAG: #CATEGORY #FEATURE
/// </remarks>
public string MethodName(string paramName)
{
    // Implementation
}
```

### ✅ 2. Inline Comments

**Use comments for:**
- Complex algorithms
- Business logic explanations
- Security considerations
- Performance optimizations
- Workarounds or limitations

**Don't comment:**
- Obvious code (`i++; // increment i`)
- Well-named methods that are self-explanatory
- Standard patterns everyone knows

```csharp
// GOOD - Explains WHY
// Retry 3 times with exponential backoff to handle transient AD errors
for (int i = 0; i < 3; i++)
{
    await Task.Delay(Math.Pow(2, i) * 1000);
}

// BAD - Explains WHAT (obvious from code)
// Loop from 0 to 3
for (int i = 0; i < 3; i++)
{
}
```

---

## 🎯 Error Handling Standards

### ✅ 1. Try-Catch Blocks (Required for all risky operations)

```csharp
// TAG: #ERROR_HANDLING #LOGGING #TOAST_NOTIFICATIONS
try
{
    // Risky operation
    await PerformOperationAsync();

    // Success notification
    ToastManager.ShowSuccess("Operation completed successfully");
}
catch (UnauthorizedAccessException ex)
{
    // Specific exception handling
    LogManager.LogError("MethodName", ex);
    ToastManager.ShowError("Access denied. Please check permissions.");
}
catch (Exception ex)
{
    // General exception handling
    LogManager.LogError("MethodName", ex);
    ToastManager.ShowError($"Operation failed: {ex.Message}");
}
```

### ✅ 2. Error Handling Checklist

- [ ] All exceptions logged with `LogManager.LogError()`
- [ ] User-friendly error messages via `ToastManager.ShowError()`
- [ ] No technical jargon in user-facing messages
- [ ] No raw exception messages shown to users
- [ ] Specific exceptions caught when possible (not just `Exception`)
- [ ] Resources cleaned up (using/Dispose patterns)
- [ ] Error state properly handled (UI reset, data cleanup)

### ✅ 3. Logging Standards

```csharp
// Success
LogManager.LogInfo($"Computer {computerName} scanned successfully");

// Warning
LogManager.LogWarning($"Computer {computerName} unreachable");

// Error
LogManager.LogError("MethodName", exception);

// Security
LogManager.LogWarning($"Blocked security violation: {details}");
```

---

## 🔄 Async/Await Standards

### ✅ 1. Async Method Naming

```csharp
// Async methods MUST end with "Async"
public async Task<List<Computer>> ScanComputersAsync()
public async Task SaveSettingsAsync()
public async Task<bool> ValidateCredentialsAsync()
```

### ✅ 2. Async Best Practices

```csharp
// TAG: #ASYNC_OPERATIONS #ERROR_HANDLING

// GOOD - ConfigureAwait(false) for library code
await SomeOperationAsync().ConfigureAwait(false);

// GOOD - Await properly (don't block)
var result = await GetDataAsync();

// BAD - Blocking on async (NEVER DO THIS)
var result = GetDataAsync().Result; ❌
var result = GetDataAsync().GetAwaiter().GetResult(); ❌

// GOOD - Parallel operations
var task1 = Operation1Async();
var task2 = Operation2Async();
await Task.WhenAll(task1, task2);

// GOOD - Task.Run for CPU-bound work
var result = await Task.Run(() => ExpensiveComputation());
```

### ✅ 3. Async Checklist

- [ ] Method name ends with "Async"
- [ ] Returns `Task` or `Task<T>`
- [ ] Uses `await` (not `.Result` or `.Wait()`)
- [ ] Error handling with try-catch
- [ ] UI updates on UI thread (Dispatcher)
- [ ] Long operations show loading indicator
- [ ] Cancellation token support for long operations

---

## 🗄️ Database Access Standards

### ✅ 1. Parameterized Queries (ALWAYS)

```csharp
// TAG: #DATABASE_ACCESS #SECURITY_CRITICAL

// CORRECT - Parameterized
string sql = "SELECT * FROM Computers WHERE Name = @name";
command.Parameters.AddWithValue("@name", computerName);

// WRONG - String concatenation (SQL INJECTION!)
string sql = $"SELECT * FROM Computers WHERE Name = '{computerName}'"; ❌
```

### ✅ 2. Using Statements (Resource cleanup)

```csharp
// TAG: #DATABASE_ACCESS #RESOURCE_MANAGEMENT
using (var connection = new SqlConnection(connectionString))
using (var command = connection.CreateCommand())
{
    connection.Open();
    command.CommandText = sql;
    command.Parameters.AddWithValue("@param", value);

    using (var reader = command.ExecuteReader())
    {
        while (reader.Read())
        {
            // Process results
        }
    }
}
// Connection automatically closed and disposed
```

### ✅ 3. Database Checklist

- [ ] All queries parameterized (NO string concatenation)
- [ ] Using statements for all IDisposable resources
- [ ] Connection strings from config (not hardcoded)
- [ ] Transactions for multi-step operations
- [ ] Error handling with logging
- [ ] No sensitive data in logs

---

## 🎨 Naming Conventions

### ✅ 1. Standard Conventions

```csharp
// Classes - PascalCase
public class ComputerScanner { }

// Methods - PascalCase
public void ScanComputers() { }

// Properties - PascalCase
public string ComputerName { get; set; }

// Private fields - camelCase with underscore
private string _connectionString;
private int _retryCount;

// Parameters - camelCase
public void Method(string computerName, int timeout) { }

// Local variables - camelCase
string userName = "admin";
int count = 0;

// Constants - PascalCase
private const int DefaultTimeout = 30;

// Enums - PascalCase
public enum Status { Online, Offline, Unknown }

// Events - PascalCase
public event EventHandler ComputerScanned;
```

### ✅ 2. Meaningful Names

```csharp
// GOOD - Self-explanatory
string computerName = "PC-001";
int retryCount = 3;
bool isOnline = CheckStatus();
List<Computer> activeComputers = GetActiveComputers();

// BAD - Unclear, abbreviated
string cn = "PC-001";  ❌
int rc = 3;  ❌
bool b = CheckStatus();  ❌
List<Computer> list1 = GetActiveComputers();  ❌
```

---

## 🧪 Code Organization

### ✅ 1. File Organization

```csharp
// 1. Using statements (sorted)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NecessaryAdminTool.Models;
using NecessaryAdminTool.Security;

// 2. Namespace
namespace NecessaryAdminTool.Managers
{
    // 3. XML documentation
    /// <summary>
    /// Class description
    /// TAG: #TAGS
    /// </summary>

    // 4. Class declaration
    public class ClassName
    {
        // 5. Constants
        private const int DefaultTimeout = 30;

        // 6. Private fields
        private string _connectionString;
        private int _retryCount;

        // 7. Events
        public event EventHandler DataChanged;

        // 8. Properties
        public string Name { get; set; }
        public int Count { get; private set; }

        // 9. Constructor(s)
        public ClassName()
        {
            InitializeComponent();
        }

        // 10. Public methods
        public void PublicMethod() { }

        // 11. Private methods
        private void PrivateMethod() { }

        // 12. Event handlers
        private void Button_Click(object sender, EventArgs e) { }
    }
}
```

### ✅ 2. Method Size

**Keep methods focused and small:**
- Ideal: < 50 lines
- Maximum: 100 lines
- If longer, refactor into smaller methods

```csharp
// GOOD - Single responsibility
private void SaveData()
{
    ValidateData();
    UpdateDatabase();
    NotifyUser();
}

// BAD - Doing too much
private void SaveData()
{
    // 200 lines of validation, database, UI updates... ❌
}
```

---

## ✅ Performance Best Practices

### ✅ 1. LINQ Optimization

```csharp
// GOOD - Single enumeration
var activeComputers = computers
    .Where(c => c.Status == "Online")
    .ToList();

// BAD - Multiple enumerations
var online = computers.Where(c => c.Status == "Online");
var count = online.Count();  // Enumeration 1
var list = online.ToList();  // Enumeration 2 ❌

// GOOD - Use Any() instead of Count() > 0
if (computers.Any(c => c.Status == "Online"))

// BAD - Unnecessary enumeration
if (computers.Count(c => c.Status == "Online") > 0) ❌
```

### ✅ 2. String Operations

```csharp
// GOOD - StringBuilder for multiple concatenations
var sb = new StringBuilder();
for (int i = 0; i < 1000; i++)
{
    sb.AppendLine($"Line {i}");
}
string result = sb.ToString();

// BAD - String concatenation in loop
string result = "";
for (int i = 0; i < 1000; i++)
{
    result += $"Line {i}\n"; ❌
}
```

### ✅ 3. Collection Initialization

```csharp
// GOOD - Specify capacity for large lists
var computers = new List<Computer>(1000);

// GOOD - Use Dictionary for lookups
var computerLookup = new Dictionary<string, Computer>();

// BAD - Using List.Contains() in loop
if (computers.Any(c => c.Name == name)) ❌
// Use Dictionary instead
```

---

## ✅ Final Code Quality Checklist

Before committing any code:

**Security:**
- [ ] All user input validated with SecurityValidator
- [ ] All commands/queries sanitized
- [ ] All security code tagged #SECURITY_CRITICAL
- [ ] All security violations logged
- [ ] No hardcoded credentials

**Code Standards:**
- [ ] All code properly tagged
- [ ] XML documentation on public members
- [ ] Meaningful variable/method names
- [ ] Follow naming conventions
- [ ] No compiler warnings

**Error Handling:**
- [ ] Try-catch on all risky operations
- [ ] All exceptions logged
- [ ] User-friendly error messages via ToastManager
- [ ] No MessageBox.Show() usage
- [ ] Resources properly disposed

**Async/Await:**
- [ ] Async methods end with "Async"
- [ ] Proper await usage (no .Result or .Wait())
- [ ] Error handling in async methods
- [ ] Loading indicators for long operations

**Database:**
- [ ] All queries parameterized
- [ ] Using statements for connections
- [ ] No SQL injection vulnerabilities
- [ ] Connection strings from config

**Performance:**
- [ ] No unnecessary enumerations
- [ ] StringBuilder for string concatenations
- [ ] Appropriate collection types
- [ ] Async for I/O operations

**Testing:**
- [ ] Code compiles without errors
- [ ] No warnings
- [ ] Functionality tested manually
- [ ] Error cases tested

---

## 🚫 Common Anti-Patterns to Avoid

**DON'T:**
- ❌ Use `MessageBox.Show()`
- ❌ Hardcode credentials, API keys, or secrets
- ❌ Use string concatenation for SQL/commands
- ❌ Block on async methods (`.Result`, `.Wait()`)
- ❌ Swallow exceptions (empty catch blocks)
- ❌ Use `goto` statements
- ❌ Write methods > 100 lines
- ❌ Use magic numbers (use constants)
- ❌ Ignore compiler warnings
- ❌ Skip error handling

**DO:**
- ✅ Use `ToastManager` for notifications
- ✅ Use `SecureCredentialManager` for credentials
- ✅ Use parameterized queries
- ✅ Await async methods properly
- ✅ Log and handle all exceptions
- ✅ Use early returns for validation
- ✅ Refactor large methods
- ✅ Define constants for magic numbers
- ✅ Fix all warnings
- ✅ Handle all error cases

---

## 📚 Reference Files

- `SECURITY_RELEASE_CHECKLIST.md` - Security validation
- `UI_DEVELOPMENT_CHECKLIST.md` - UI standards
- `Security/SecurityValidator.cs` - Security validation methods
- `Managers/UI/ToastManager.cs` - Toast notifications
- `LogManager.cs` - Logging standards
- `CLAUDE.md` - Project overview

---

**REMEMBER:** Code quality is not optional. Write code you'd be proud to show other developers.

**"Quality means doing it right when no one is looking."** - Henry Ford
