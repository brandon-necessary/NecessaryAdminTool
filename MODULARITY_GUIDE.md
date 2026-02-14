# 🏢 MODULARITY & COMMERCIAL VIABILITY GUIDE

**Last Updated:** 2026-02-13
**Purpose:** Document all elements requiring externalization for white-labeling and commercial distribution

---

## 📋 TABLE OF CONTENTS

1. [Branding Elements](#branding-elements)
2. [Configuration System](#configuration-system)
3. [Hard-Coded Values](#hard-coded-values)
4. [Theme System](#theme-system)
5. [Implementation Checklist](#implementation-checklist)

---

## 🎨 BRANDING ELEMENTS

### Company Names (Currently: "Artazn LLC")
**Files to Update:**
- `AboutWindow.xaml` - Lines 167, 291, 308, 317, 322, 337, 342, 347, 352, 376, 422, 494, 506, 510, 515
- `MainWindow.xaml` - Logo text (line ~550)
- `MainWindow.xaml.cs` - LogoConfig class

**Recommended Solution:**
```xml
<!-- Create AppConfig.xml -->
<AppConfig>
  <Branding>
    <CompanyName>Artazn LLC</CompanyName>
    <ProductName>ArtaznIT Suite</ProductName>
    <ProductVersion>v5.0</ProductVersion>
    <ProductEdition>Kerberos Edition</ProductEdition>
    <LegalContact>legal@artazn.com</LegalContact>
    <SupportContact>support@artazn.com</SupportContact>
  </Branding>
</AppConfig>
```

**Tags to Search:**
- `#BRANDING` - All company/product name references
- `#THEME_LOGO` - Logo placements

---

## ⚙️ CONFIGURATION SYSTEM

### Default Domain Credentials
**Current Hard-Coded Values:**
- Default username: `"process\\"` (Lines: LoginWindow constructor, Settings)
- Domain name: Discovered from `Domain.GetCurrentDomain()`

**Recommended Solution:**
```xml
<DefaultCredentials>
  <DomainPrefix>YOURDOMAIN</DomainPrefix>
  <AllowRememberUsername>true</AllowRememberUsername>
  <DefaultAdminGroups>
    <Group>Domain Admins</Group>
    <Group>Enterprise Admins</Group>
    <Group>IT-Admins</Group>
  </DefaultAdminGroups>
</DefaultCredentials>
```

**Files:**
- `MainWindow.xaml.cs` - Line ~10140 (LoginWindow default user)
- `MainWindow.xaml.cs` - CheckDomainAdminMembership method

---

## 🔧 HARD-CODED VALUES

### Application Strings
**Location:** `MainWindow.xaml.cs`

| Line | Current Value | Should Be Configurable |
|------|---------------|------------------------|
| 2103 | "ArtaznIT Suite v5.0 (Kerberos Edition) initialized." | `{ProductName} {Version} ({Edition}) initialized.` |
| 4310 | "ArtaznIT Suite (Admin)" | `{ProductName} (Admin)` |
| 6322 | "ArtaznIT Suite - Admin Launcher" | `{ProductName} - Admin Launcher` |
| 6338 | "ArtaznIT Suite (Admin).lnk" | `{ProductName} (Admin).lnk` |
| 8304 | "Device Monitor - ArtaznIT Suite" | `Device Monitor - {ProductName}` |

**Recommended Implementation:**
```csharp
// Create BrandingConfig.cs
public static class BrandingConfig
{
    public static string CompanyName { get; set; } = "Artazn LLC";
    public static string ProductName { get; set; } = "ArtaznIT Suite";
    public static string ProductVersion { get; set; } = "v5.0";
    public static string ProductEdition { get; set; } = "Kerberos Edition";
    public static string LegalEmail { get; set; } = "legal@artazn.com";
    public static string SupportEmail { get; set; } = "support@artazn.com";
    public static string SupportPhone { get; set; } = "Contact your authorized representative";

    // Load from external XML/JSON config file
    public static void LoadFromConfig(string configPath) { /* ... */ }
}
```

---

## 🎨 THEME SYSTEM

### Color Scheme (Currently: Orange #FF8533 → Zinc #A1A1AA)
**Already Well-Modularized!** ✅

**Color Resources in `MainWindow.xaml`:**
```xml
<!-- Lines 33-38 - TAG: #THEME_COLORS -->
<Color x:Key="AccentOrange">#FFFF8533</Color>
<Color x:Key="AccentZinc">#FFA1A1AA</Color>
<SolidColorBrush x:Key="AccentOrangeBrush" Color="{StaticResource AccentOrange}"/>
<SolidColorBrush x:Key="AccentCyanBrush" Color="{StaticResource AccentOrange}"/>
```

**For Commercial Distribution:**
```xml
<!-- Allow theme configuration via external file -->
<ThemeConfig>
  <PrimaryColor>#FFFF8533</PrimaryColor>  <!-- Orange -->
  <SecondaryColor>#FFA1A1AA</SecondaryColor>  <!-- Zinc -->
  <AccentColor>#FFFF8533</AccentColor>
  <LogoPath>Assets/logo.png</LogoPath>
</ThemeConfig>
```

**Tagged Elements for Easy Theme Updates:**
- `#THEME_COLORS` - All color definitions and gradients
- `#THEME_LOGO` - Logo placements (3 locations)
- `#THEME_DIALOG` - Themed dialog windows
- `#SECTION_HEADERS` - Section headers using accent colors

---

## 📄 LEGAL & LICENSING

### EULA Text
**File:** `AboutWindow.xaml` (Lines 280-515)

**Current:** Hard-coded EULA with "Artazn LLC" throughout
**Recommended:** Load EULA from external file or embed as resource with templating

```csharp
// EulaManager.cs
public static class EulaManager
{
    public static string GetEulaText()
    {
        string template = LoadEulaTemplate();
        return template
            .Replace("{CompanyName}", BrandingConfig.CompanyName)
            .Replace("{ProductName}", BrandingConfig.ProductName)
            .Replace("{LegalEmail}", BrandingConfig.LegalEmail);
    }
}
```

---

## 🏷️ TAG REFERENCE

### All Tags for Modular Elements

| Tag | Purpose | Files |
|-----|---------|-------|
| `#BRANDING` | Company/product names | AboutWindow.xaml, MainWindow.xaml |
| `#THEME_COLORS` | Color scheme (orange→zinc gradient) | MainWindow.xaml (all gradients) |
| `#THEME_LOGO` | Logo placements | MainWindow.xaml, LoginWindow, AboutWindow |
| `#THEME_DIALOG` | Themed dialogs | MainWindow.xaml.cs (all dialogs) |
| `#SECTION_HEADERS` | Section headers with accent colors | MainWindow.xaml |
| `#STATUS_INDICATORS` | Status badges, progress bars | MainWindow.xaml |
| `#ERROR_TRACKING` | Error/warning indicators | MainWindow.xaml |
| `#AUTH_STATUS` | Authentication status display | MainWindow.xaml |
| `#DC_DISCOVERY` | Domain controller features | MainWindow.xaml, MainWindow.xaml.cs |
| `#LOGGING` | Master log status | MainWindow.xaml |
| `#ANIMATION` | Animated elements (pulse, spin) | MainWindow.xaml |
| `#LOADING` | Loading indicators/progress bars | MainWindow.xaml |

---

## ✅ IMPLEMENTATION CHECKLIST

### Phase 1: Configuration System
- [ ] Create `BrandingConfig.cs` class
- [ ] Create `AppConfig.xml` schema
- [ ] Create `ConfigurationManager` to load external config
- [ ] Replace all hard-coded company names with `BrandingConfig.CompanyName`
- [ ] Replace all product names with `BrandingConfig.ProductName`
- [ ] Replace version strings with `BrandingConfig.ProductVersion`

### Phase 2: EULA & Legal
- [ ] Create EULA template file (`EULA_TEMPLATE.txt`)
- [ ] Create `EulaManager.cs` for template replacement
- [ ] Update About window to use dynamic EULA
- [ ] Add license key validation system (optional)

### Phase 3: Theme System Enhancement
- [ ] Create `ThemeConfig.xml` for external theme definition
- [ ] Load theme colors from config at startup
- [ ] Create theme preview tool for testing color schemes
- [ ] Document custom theme creation process

### Phase 4: Logo System
- [ ] Externalize logo to `Assets/` folder
- [ ] Support multiple logo formats (SVG, PNG)
- [ ] Allow logo customization via config
- [ ] Update `LogoConfig` class to load from external source

### Phase 5: Default Settings
- [ ] Externalize default domain prefix
- [ ] Configure default admin groups via config
- [ ] Make DC refresh intervals configurable
- [ ] Export all hard-coded timeouts to config

### Phase 6: Testing & Validation
- [ ] Create test config for "Generic IT Suite"
- [ ] Verify all branding elements update correctly
- [ ] Test with alternate color scheme
- [ ] Validate EULA replacement
- [ ] Document white-labeling process

---

## 🚀 QUICK WHITE-LABEL GUIDE

### To Rebrand This Product:

1. **Update `AppConfig.xml`:**
   ```xml
   <CompanyName>Your Company Inc.</CompanyName>
   <ProductName>Your Product Suite</ProductName>
   ```

2. **Replace Logo:**
   - Update `Assets/logo.png`
   - Modify `LogoConfig` class if needed

3. **Update Colors:**
   ```xml
   <PrimaryColor>#YourColor</PrimaryColor>
   <SecondaryColor>#YourAccent</SecondaryColor>
   ```

4. **Replace EULA:**
   - Update `EULA_TEMPLATE.txt` with your terms

5. **Rebuild & Test:**
   ```bash
   msbuild /t:Rebuild /p:Configuration=Release
   ```

---

## 📝 NOTES

- **Version Badge:** Currently shows "v5.0" - TAG: `#BRANDING` (Line ~568)
- **Domain Badge:** Shows detected domain - Already dynamic ✅
- **Role Badge:** Shows user role - Already dynamic ✅
- **AccentCyanBrush:** Currently maps to AccentOrange - Consider renaming for clarity

---

## 🔍 SEARCH COMMANDS

To find all elements requiring updates:

```bash
# Find all branding references
grep -r "Artazn" --include="*.xaml" --include="*.cs"

# Find all product name references
grep -r "ArtaznIT Suite" --include="*.xaml" --include="*.cs"

# Find all hard-coded version strings
grep -r "v5\\.0\\|v4\\.0" --include="*.xaml" --include="*.cs"

# Find all theme color definitions
grep -r "#THEME_COLORS" --include="*.xaml"

# Find all logo placements
grep -r "#THEME_LOGO" --include="*.xaml" --include="*.cs"
```

---

**End of Modularity Guide**
*For questions or updates, contact the development team*
