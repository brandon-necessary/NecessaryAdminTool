# 🔧 MODULAR IMPLEMENTATION PLAN
**Making ArtaznIT Suite White-Label Ready**

---

## 📋 OVERVIEW

This plan converts all hard-coded branding/config into an external configuration system, enabling:
- ✅ Easy white-labeling for different customers
- ✅ Version updates without code changes
- ✅ Custom EULA per deployment
- ✅ Per-customer domain defaults

**Estimated Time:** 4-6 hours
**Difficulty:** Medium
**Risk:** Low (backward compatible)

---

## 🎯 PHASE 1: CREATE CONFIGURATION SYSTEM

### Step 1.1: Create AppConfig.xml

**File:** `C:\Users\brandon.necessary\source\repos\ArtaznIT\ArtaznIT\Config\AppConfig.xml`

```xml
<?xml version="1.0" encoding="utf-8"?>
<AppConfiguration>
  <!-- BRANDING - TAG: #BRANDING #CONFIG -->
  <Branding>
    <CompanyName>Artazn LLC</CompanyName>
    <ProductName>ArtaznIT Suite</ProductName>
    <ProductShortName>ArtaznIT</ProductShortName>
    <Version>5.0</Version>
    <Edition>Kerberos Edition</Edition>
    <CopyrightYear>2026</CopyrightYear>
  </Branding>

  <!-- CONTACT INFORMATION -->
  <Contact>
    <LegalEmail>legal@artazn.com</LegalEmail>
    <SupportEmail>support@artazn.com</SupportEmail>
    <SupportPhone>Contact your authorized representative</SupportPhone>
    <Website>https://artazn.com</Website>
  </Contact>

  <!-- DEFAULT CREDENTIALS - TAG: #AUTH_CONFIG -->
  <DefaultCredentials>
    <DomainPrefix>PROCESS</DomainPrefix>
    <RememberUsername>true</RememberUsername>
  </DefaultCredentials>

  <!-- ADMIN GROUPS - TAG: #AUTH_CONFIG -->
  <AdminGroups>
    <Group>Domain Admins</Group>
    <Group>Enterprise Admins</Group>
    <Group>IT-Admins</Group>
    <Group>Administrators</Group>
  </AdminGroups>

  <!-- THEME COLORS (Optional - for future use) - TAG: #THEME_CONFIG -->
  <Theme>
    <PrimaryColor>#FFFF8533</PrimaryColor>
    <SecondaryColor>#FFA1A1AA</SecondaryColor>
    <LogoPath>Assets/logo.png</LogoPath>
  </Theme>

  <!-- TIMERS & INTERVALS - TAG: #CONFIG -->
  <Timers>
    <DomainCheckInterval>300</DomainCheckInterval> <!-- 5 minutes in seconds -->
    <LoginDomainCheckInterval>5</LoginDomainCheckInterval> <!-- 5 seconds -->
    <DCHealthCheckInterval>60</DCHealthCheckInterval> <!-- 1 minute -->
  </Timers>
</AppConfiguration>
```

**Action:** Create `Config` folder in project, add this file, set "Copy to Output Directory" = "Copy if newer"

---

### Step 1.2: Create BrandingConfig.cs

**File:** `C:\Users\brandon.necessary\source\repos\ArtaznIT\ArtaznIT\BrandingConfig.cs`

```csharp
using System;
using System.IO;
using System.Xml.Linq;

namespace ArtaznIT
{
    /// <summary>
    /// Centralized branding and configuration system
    /// TAG: #BRANDING #CONFIG
    /// </summary>
    public static class BrandingConfig
    {
        // === BRANDING ===
        public static string CompanyName { get; private set; } = "Artazn LLC";
        public static string ProductName { get; private set; } = "ArtaznIT Suite";
        public static string ProductShortName { get; private set; } = "ArtaznIT";
        public static string Version { get; private set; } = "5.0";
        public static string Edition { get; private set; } = "Kerberos Edition";
        public static string CopyrightYear { get; private set; } = "2026";

        // === CONTACT ===
        public static string LegalEmail { get; private set; } = "legal@artazn.com";
        public static string SupportEmail { get; private set; } = "support@artazn.com";
        public static string SupportPhone { get; private set; } = "Contact your authorized representative";
        public static string Website { get; private set; } = "https://artazn.com";

        // === DEFAULTS ===
        public static string DefaultDomainPrefix { get; private set; } = "PROCESS";
        public static bool RememberUsername { get; private set; } = true;
        public static string[] AdminGroups { get; private set; } = new[]
        {
            "Domain Admins",
            "Enterprise Admins",
            "IT-Admins",
            "Administrators"
        };

        // === TIMERS ===
        public static int DomainCheckIntervalSeconds { get; private set; } = 300; // 5 minutes
        public static int LoginDomainCheckIntervalSeconds { get; private set; } = 5; // 5 seconds

        // === DERIVED PROPERTIES ===
        public static string FullProductName => $"{ProductName} v{Version}";
        public static string FullProductNameWithEdition => $"{ProductName} v{Version} ({Edition})";
        public static string WindowTitle => ProductName;
        public static string AdminShortcutName => $"{ProductName} (Admin)";
        public static string CopyrightNotice => $"© {CopyrightYear} {CompanyName}. All Rights Reserved.";

        /// <summary>
        /// Load configuration from XML file
        /// </summary>
        public static void LoadConfiguration(string configPath = null)
        {
            try
            {
                // Default config path
                if (string.IsNullOrEmpty(configPath))
                {
                    string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                    configPath = Path.Combine(exeDir, "Config", "AppConfig.xml");
                }

                if (!File.Exists(configPath))
                {
                    LogManager.LogWarning($"Config file not found: {configPath} - using defaults");
                    return;
                }

                var doc = XDocument.Load(configPath);
                var root = doc.Root;

                // Load Branding
                var branding = root.Element("Branding");
                if (branding != null)
                {
                    CompanyName = branding.Element("CompanyName")?.Value ?? CompanyName;
                    ProductName = branding.Element("ProductName")?.Value ?? ProductName;
                    ProductShortName = branding.Element("ProductShortName")?.Value ?? ProductShortName;
                    Version = branding.Element("Version")?.Value ?? Version;
                    Edition = branding.Element("Edition")?.Value ?? Edition;
                    CopyrightYear = branding.Element("CopyrightYear")?.Value ?? CopyrightYear;
                }

                // Load Contact
                var contact = root.Element("Contact");
                if (contact != null)
                {
                    LegalEmail = contact.Element("LegalEmail")?.Value ?? LegalEmail;
                    SupportEmail = contact.Element("SupportEmail")?.Value ?? SupportEmail;
                    SupportPhone = contact.Element("SupportPhone")?.Value ?? SupportPhone;
                    Website = contact.Element("Website")?.Value ?? Website;
                }

                // Load Default Credentials
                var credentials = root.Element("DefaultCredentials");
                if (credentials != null)
                {
                    DefaultDomainPrefix = credentials.Element("DomainPrefix")?.Value ?? DefaultDomainPrefix;
                    RememberUsername = bool.Parse(credentials.Element("RememberUsername")?.Value ?? "true");
                }

                // Load Admin Groups
                var adminGroups = root.Element("AdminGroups");
                if (adminGroups != null)
                {
                    var groups = new System.Collections.Generic.List<string>();
                    foreach (var group in adminGroups.Elements("Group"))
                    {
                        groups.Add(group.Value);
                    }
                    if (groups.Count > 0)
                        AdminGroups = groups.ToArray();
                }

                // Load Timers
                var timers = root.Element("Timers");
                if (timers != null)
                {
                    DomainCheckIntervalSeconds = int.Parse(timers.Element("DomainCheckInterval")?.Value ?? "300");
                    LoginDomainCheckIntervalSeconds = int.Parse(timers.Element("LoginDomainCheckInterval")?.Value ?? "5");
                }

                LogManager.LogInfo($"Configuration loaded: {ProductName} v{Version} by {CompanyName}");
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to load configuration - using defaults", ex);
            }
        }
    }
}
```

**Action:** Add this file to project root

---

## 🎯 PHASE 2: EXTERNALIZE EULA

### Step 2.1: Create EULA Template

**File:** `C:\Users\brandon.necessary\source\repos\ArtaznIT\ArtaznIT\Config\EULA_TEMPLATE.txt`

```
END USER LICENSE AGREEMENT (EULA)

IMPORTANT – READ CAREFULLY: This End User License Agreement ("Agreement") is a legal agreement between you (either an individual or a single entity, "Licensee") and {CompanyName} ("Licensor") for the software product identified above, which includes computer software and may include associated media, printed materials, and "online" or electronic documentation ("{ProductName}").

BY INSTALLING, COPYING, OR OTHERWISE USING THE SOFTWARE, YOU AGREE TO BE BOUND BY THE TERMS OF THIS AGREEMENT. IF YOU DO NOT AGREE TO THE TERMS OF THIS AGREEMENT, DO NOT INSTALL OR USE THE SOFTWARE.

1. LICENSE GRANT
This software product, {ProductName} ("Software"), is licensed, not sold, to you by {CompanyName} ("Licensor") for use strictly in accordance with the terms of this Agreement. This license grants you the right to install and use the Software solely for internal business operations within your organization's authorized IT infrastructure management activities.

2. RESTRICTIONS
You may not:
• Transfer, sublicense, or assign your rights under this license to any third party without prior written consent from {CompanyName};
• Reverse engineer, decompile, or disassemble the Software, except to the extent that such activity is expressly permitted by applicable law;
• Remove, alter, or obscure any proprietary notices (including copyright and trademark notices) on the Software;
• Use the Software for any unlawful purpose or in violation of any applicable laws or regulations;
• Distribute, rent, lease, or lend the Software to third parties;

... [REST OF EULA TEXT WITH {PLACEHOLDERS}] ...

CONTACT INFORMATION
For questions about this Agreement or the Software, contact:
{CompanyName} - Legal Department
Email: {LegalEmail}
Phone: {SupportPhone}

{ProductName} v{Version} | {CompanyName} - All Rights Reserved | Confidential & Proprietary
```

**Action:** Create this file with ALL text from AboutWindow.xaml, replace names with `{CompanyName}`, `{ProductName}`, etc.

---

### Step 2.2: Create EulaManager.cs

**File:** `C:\Users\brandon.necessary\source\repos\ArtaznIT\ArtaznIT\EulaManager.cs`

```csharp
using System;
using System.IO;

namespace ArtaznIT
{
    /// <summary>
    /// Manages EULA text with templating support
    /// TAG: #BRANDING #CONFIG #EULA
    /// </summary>
    public static class EulaManager
    {
        private static string _cachedEula = null;

        /// <summary>
        /// Get EULA text with all placeholders replaced
        /// </summary>
        public static string GetEulaText()
        {
            if (_cachedEula != null)
                return _cachedEula;

            try
            {
                // Load EULA template
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                string templatePath = Path.Combine(exeDir, "Config", "EULA_TEMPLATE.txt");

                if (!File.Exists(templatePath))
                {
                    LogManager.LogWarning("EULA template not found - using default");
                    return GetDefaultEula();
                }

                string template = File.ReadAllText(templatePath);

                // Replace placeholders
                _cachedEula = template
                    .Replace("{CompanyName}", BrandingConfig.CompanyName)
                    .Replace("{ProductName}", BrandingConfig.ProductName)
                    .Replace("{Version}", BrandingConfig.Version)
                    .Replace("{LegalEmail}", BrandingConfig.LegalEmail)
                    .Replace("{SupportEmail}", BrandingConfig.SupportEmail)
                    .Replace("{SupportPhone}", BrandingConfig.SupportPhone)
                    .Replace("{Website}", BrandingConfig.Website)
                    .Replace("{CopyrightYear}", BrandingConfig.CopyrightYear);

                return _cachedEula;
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to load EULA template", ex);
                return GetDefaultEula();
            }
        }

        /// <summary>
        /// Fallback EULA if template not found
        /// </summary>
        private static string GetDefaultEula()
        {
            return $@"END USER LICENSE AGREEMENT

This software ({BrandingConfig.ProductName}) is licensed by {BrandingConfig.CompanyName}.

[Default EULA text...]

Contact: {BrandingConfig.LegalEmail}";
        }
    }
}
```

**Action:** Add this file to project

---

## 🎯 PHASE 3: UPDATE CODE TO USE BrandingConfig

### Step 3.1: Update MainWindow.xaml.cs Initialization

**Location:** `MainWindow.xaml.cs` - Constructor or Window_Loaded

**BEFORE:**
```csharp
private void Window_Loaded(object sender, RoutedEventArgs e)
{
    try
    {
        SecureConfig.LoadConfiguration();
        LoadConfig();
        // ...
```

**AFTER:**
```csharp
private void Window_Loaded(object sender, RoutedEventArgs e)
{
    try
    {
        // TAG: #CONFIG - Load branding first
        BrandingConfig.LoadConfiguration();

        SecureConfig.LoadConfiguration();
        LoadConfig();
        // ...
```

---

### Step 3.2: Update Terminal Initialization Message

**Location:** `MainWindow.xaml.cs` - Line ~2103

**BEFORE:**
```csharp
AppendTerminal("ArtaznIT Suite v5.0 (Kerberos Edition) initialized.", false);
```

**AFTER:**
```csharp
AppendTerminal($"{BrandingConfig.FullProductNameWithEdition} initialized.", false);
```

---

### Step 3.3: Update All Product Name References

**Use Find/Replace (Ctrl+H) in Visual Studio:**

| Find | Replace With | Files |
|------|-------------|-------|
| `"ArtaznIT Suite"` | `BrandingConfig.ProductName` | `*.cs` |
| `"ArtaznIT Suite (Admin)"` | `BrandingConfig.AdminShortcutName` | `*.cs` |
| `"Artazn LLC"` | `BrandingConfig.CompanyName` | `*.cs` |
| `"v5.0"` | `$"v{BrandingConfig.Version}"` | `*.cs` (be careful with XAML) |
| `"Kerberos Edition"` | `BrandingConfig.Edition` | `*.cs` |

**⚠️ IMPORTANT:** Review each replacement! Some may need string interpolation adjustments.

**Example Replacements:**

**BEFORE:**
```csharp
Title = "Device Monitor - ArtaznIT Suite";
```

**AFTER:**
```csharp
Title = $"Device Monitor - {BrandingConfig.ProductName}";
```

**BEFORE:**
```csharp
string shortcutPath = Path.Combine(desktopPath, "ArtaznIT Suite (Admin).lnk");
```

**AFTER:**
```csharp
string shortcutPath = Path.Combine(desktopPath, $"{BrandingConfig.AdminShortcutName}.lnk");
```

---

### Step 3.4: Update LoginWindow Default Domain

**Location:** `MainWindow.xaml.cs` - LoginWindow constructor (~line 10140)

**BEFORE:**
```csharp
string cached = "";
try { cached = Properties.Settings.Default.LastUser; }
catch { }
if (string.IsNullOrEmpty(cached)) cached = "process\\";
```

**AFTER:**
```csharp
string cached = "";
try { cached = Properties.Settings.Default.LastUser; }
catch { }
if (string.IsNullOrEmpty(cached))
    cached = $"{BrandingConfig.DefaultDomainPrefix.ToLower()}\\";
```

---

### Step 3.5: Update Admin Group Checking

**Location:** `MainWindow.xaml.cs` - CheckDomainAdminMembership method

**BEFORE:**
```csharp
string[] adminGroups = {
    "Domain Admins",
    "Enterprise Admins",
    "IT-Admins",
    "Administrators"
};

foreach (var groupName in adminGroups)
{
    // ...
}
```

**AFTER:**
```csharp
// Use configurable admin groups - TAG: #AUTH_CONFIG
foreach (var groupName in BrandingConfig.AdminGroups)
{
    // ...
}
```

---

### Step 3.6: Update Domain Check Timers

**Location:** `MainWindow.xaml.cs` - StartDomainVerificationTimer method

**BEFORE:**
```csharp
_domainVerificationTimer = new System.Windows.Threading.DispatcherTimer
{
    Interval = TimeSpan.FromMinutes(5)
};
```

**AFTER:**
```csharp
_domainVerificationTimer = new System.Windows.Threading.DispatcherTimer
{
    Interval = TimeSpan.FromSeconds(BrandingConfig.DomainCheckIntervalSeconds)
};
```

**Location:** `MainWindow.xaml.cs` - LoginWindow.StartDomainCheckTimer

**BEFORE:**
```csharp
_domainCheckTimer = new System.Windows.Threading.DispatcherTimer
{
    Interval = TimeSpan.FromSeconds(5)
};
```

**AFTER:**
```csharp
_domainCheckTimer = new System.Windows.Threading.DispatcherTimer
{
    Interval = TimeSpan.FromSeconds(BrandingConfig.LoginDomainCheckIntervalSeconds)
};
```

---

### Step 3.7: Update AboutWindow to Use EulaManager

**Location:** `AboutWindow.xaml.cs` (if it exists) or AboutWindow.xaml

**Option A: If AboutWindow.xaml.cs exists:**
```csharp
public AboutWindow()
{
    InitializeComponent();

    // Set EULA text from template - TAG: #EULA #CONFIG
    TxtEula.Text = EulaManager.GetEulaText();

    // Update version display
    TxtVersion.Text = BrandingConfig.FullProductName;
}
```

**Option B: If EULA is in XAML directly:**
Move EULA text to code-behind and set it dynamically.

---

### Step 3.8: Update MainWindow.xaml Version Badge

**Location:** `MainWindow.xaml` - Line ~568-578

**BEFORE:**
```xml
<TextBlock Text="v5.0" FontSize="10" FontWeight="Bold">
```

**AFTER:**
You have two options:

**Option A - Set in Code-Behind (Recommended):**
```xml
<TextBlock x:Name="TxtVersionBadge" FontSize="10" FontWeight="Bold">
```

Then in `MainWindow.xaml.cs` Window_Loaded:
```csharp
TxtVersionBadge.Text = $"v{BrandingConfig.Version}";
```

**Option B - Use Binding (Advanced):**
Create a property in MainWindow and bind to it.

---

## 🎯 PHASE 4: UPDATE ABOUTWINDOW.XAML

### Step 4.1: Make About Window Dynamic

**Location:** `AboutWindow.xaml`

Add x:Name to elements that need dynamic text:

```xml
<!-- Company Name -->
<Run x:Name="TxtCompanyName" Text="Artazn" FontSize="36" FontWeight="ExtraBold"/>

<!-- Version -->
<TextBlock x:Name="TxtVersion" Text="v5.0" .../>

<!-- EULA -->
<TextBlock x:Name="TxtEula" TextWrapping="Wrap" .../>

<!-- Copyright -->
<Run x:Name="TxtCopyright" Text="..." />
```

**Then in AboutWindow.xaml.cs:**
```csharp
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        LoadDynamicContent();
    }

    private void LoadDynamicContent()
    {
        // TAG: #BRANDING #CONFIG
        TxtCompanyName.Text = BrandingConfig.CompanyName.Replace(" LLC", "");
        TxtVersion.Text = BrandingConfig.FullProductName;
        TxtEula.Text = EulaManager.GetEulaText();
        TxtCopyright.Text = BrandingConfig.CopyrightNotice;
    }
}
```

---

## 🎯 PHASE 5: TESTING CHECKLIST

### Test 1: Default Configuration
- [ ] Build and run without AppConfig.xml
- [ ] Verify defaults work ("Artazn LLC", "ArtaznIT Suite", "v5.0")
- [ ] Check terminal shows correct initialization message
- [ ] Verify About window shows correct company name

### Test 2: Custom Configuration
- [ ] Create AppConfig.xml with test values:
  ```xml
  <CompanyName>Test Company Inc.</CompanyName>
  <ProductName>Test IT Suite</ProductName>
  <Version>1.0</Version>
  ```
- [ ] Build and run
- [ ] Verify all UI shows "Test Company Inc."
- [ ] Check login shows "Test IT Suite"
- [ ] Verify version badge shows "v1.0"

### Test 3: EULA Template
- [ ] Create EULA_TEMPLATE.txt with placeholders
- [ ] Open About window
- [ ] Verify EULA shows replaced values
- [ ] Check company name, product name correct

### Test 4: Admin Groups
- [ ] Modify AdminGroups in AppConfig.xml
- [ ] Add custom group name
- [ ] Login as user in custom group
- [ ] Verify admin access granted

### Test 5: Timers
- [ ] Set DomainCheckInterval to 10 seconds (for testing)
- [ ] Set LoginDomainCheckInterval to 2 seconds
- [ ] Verify domain check runs every 10 seconds after login
- [ ] Verify login screen checks every 2 seconds

---

## 📝 IMPLEMENTATION ORDER

### Week 1: Core System
1. ✅ Create `Config` folder in project
2. ✅ Create `AppConfig.xml` (Step 1.1)
3. ✅ Create `BrandingConfig.cs` (Step 1.2)
4. ✅ Add config loading to Window_Loaded (Step 3.1)
5. ✅ Test basic loading

### Week 2: Code Updates
6. ✅ Update terminal message (Step 3.2)
7. ✅ Update LoginWindow default domain (Step 3.4)
8. ✅ Update admin groups (Step 3.5)
9. ✅ Update timers (Step 3.6)
10. ✅ Test all updated code

### Week 3: Product Name Migration
11. ✅ Search and replace "ArtaznIT Suite" → BrandingConfig.ProductName
12. ✅ Search and replace "Artazn LLC" → BrandingConfig.CompanyName
13. ✅ Update version references
14. ✅ Test all replaced strings

### Week 4: EULA System
15. ✅ Create EULA_TEMPLATE.txt (Step 2.1)
16. ✅ Create EulaManager.cs (Step 2.2)
17. ✅ Update AboutWindow (Step 4.1)
18. ✅ Test EULA loading

### Week 5: Final Testing & Documentation
19. ✅ Run all tests from Phase 5
20. ✅ Create white-label test config
21. ✅ Document configuration options
22. ✅ Create deployment guide

---

## 🚀 DEPLOYMENT GUIDE

### For Each Customer Deployment:

1. **Copy template config:**
   ```bash
   cp AppConfig.xml AppConfig_Customer.xml
   ```

2. **Edit customer values:**
   ```xml
   <CompanyName>Customer Name Inc.</CompanyName>
   <ProductName>Custom IT Management Suite</ProductName>
   <DefaultDomainPrefix>CUSTOMERDOMAIN</DefaultDomainPrefix>
   ```

3. **Copy EULA if customized:**
   ```bash
   cp EULA_TEMPLATE.txt EULA_Customer.txt
   ```

4. **Build with customer config:**
   - Replace `Config/AppConfig.xml` with customer version
   - Replace `Config/EULA_TEMPLATE.txt` if customized
   - Build Release

5. **Test deployment:**
   - Verify branding shows correctly
   - Check EULA text
   - Test domain defaults
   - Verify admin groups

---

## 📊 BACKWARD COMPATIBILITY

### Handling Missing Config:
- ✅ All properties have defaults in BrandingConfig.cs
- ✅ LoadConfiguration silently fails to defaults
- ✅ Existing installations continue working
- ✅ No breaking changes

### Migration Path:
1. Deploy new version with BrandingConfig.cs
2. App runs with defaults (current values)
3. Add AppConfig.xml when ready to customize
4. No user disruption

---

## 🎓 BEST PRACTICES

### DO:
- ✅ Keep defaults matching current values
- ✅ Test without config file
- ✅ Log when config loads
- ✅ Use try/catch around config loading
- ✅ Validate config values

### DON'T:
- ❌ Remove defaults from code
- ❌ Make config mandatory
- ❌ Break existing installations
- ❌ Forget to copy config to output

---

## 📁 FILE STRUCTURE (AFTER IMPLEMENTATION)

```
ArtaznIT/
├── Config/
│   ├── AppConfig.xml           ← New: Main configuration
│   └── EULA_TEMPLATE.txt       ← New: EULA template
├── BrandingConfig.cs           ← New: Config class
├── EulaManager.cs              ← New: EULA manager
├── MainWindow.xaml.cs          ← Modified: Uses BrandingConfig
├── MainWindow.xaml             ← Modified: Version badge dynamic
├── AboutWindow.xaml            ← Modified: Dynamic text
├── AboutWindow.xaml.cs         ← Modified: Loads EULA
└── [other files...]
```

---

## 🎯 SUCCESS CRITERIA

✅ **Configuration loaded:** App loads AppConfig.xml on startup
✅ **Branding works:** All UI shows configured company/product name
✅ **Version dynamic:** Version number comes from config
✅ **EULA templated:** EULA shows customized text
✅ **Domain defaults:** Login shows configured domain prefix
✅ **Admin groups:** Custom groups recognized
✅ **Timers configurable:** Check intervals adjustable
✅ **Backward compatible:** Works without config file
✅ **White-label ready:** Can deploy for different customers
✅ **Zero code changes:** Rebrand via config only

---

**READY TO IMPLEMENT!** 🚀

Start with Phase 1 (Steps 1.1-1.2), test, then proceed to Phase 2.
