# White-Label Configuration Guide
<!-- TAG: #WHITELABEL #COMPANY_BRANDING #CONFIGURATION -->
<!-- Last Updated: 2026-02-14 | Version: 1.0 (1.2602.0.0) -->

---

## 🎯 Purpose

NecessaryAdminTool is designed as a **white-label application** that can be easily customized with your organization's branding. This guide explains how to configure company-specific information throughout the application.

---

## 📝 White-Label Placeholders

The following placeholder tokens are used throughout the codebase and must be replaced with your organization's information:

### **{{COMPANY_NAME}}**
- **Usage:** Legal entity name in license agreements, warranty disclaimers, and liability limitations
- **Example:** "Contoso Corporation", "Acme LLC", "TechCorp Inc."
- **Files:** AboutWindow.xaml, AboutWindow.xaml.cs

### **{{COMPANY_DOMAIN}}**
- **Usage:** Email domain for support and legal contact
- **Example:** "contoso.com", "acme.org", "techcorp.net"
- **Files:** AboutWindow.xaml, AboutWindow.xaml.cs

---

## 🔧 Configuration Steps

### **Step 1: Update Legal Information**

#### **File:** `NecessaryAdminTool\AboutWindow.xaml`

**Line 331:** Warranty Disclaimer
```xml
{{COMPANY_NAME}} DOES NOT WARRANT THAT THE SOFTWARE WILL MEET YOUR REQUIREMENTS...
```
→ Replace `{{COMPANY_NAME}}` with your legal entity name

**Line 336:** Limitation of Liability
```xml
IN NO EVENT SHALL {{COMPANY_NAME}} BE LIABLE FOR ANY SPECIAL, INCIDENTAL...
```
→ Replace `{{COMPANY_NAME}}` with your legal entity name

**Line 510-512:** Contact Information
```xml
<Run FontWeight="Bold" Foreground="#FF888888" FontSize="9">{{COMPANY_NAME}} - Legal Department</Run>
<LineBreak/>
<Run FontWeight="Bold" Foreground="#FF888888" FontSize="9">Email: support@{{COMPANY_DOMAIN}}</Run>
```
→ Replace both placeholders with your information

---

#### **File:** `NecessaryAdminTool\AboutWindow.xaml.cs`

**Lines 525-527:** HTML Export Contact Section
```csharp
<strong>{{COMPANY_NAME}} - Legal Department</strong><br>
Email: <a href="mailto:support@{{COMPANY_DOMAIN}}" style="color: #FF8533;">support@{{COMPANY_DOMAIN}}</a><br>
Phone: Contact your authorized {{COMPANY_NAME}} representative
```
→ Replace all three occurrences of placeholders

**Line 568:** HTML Export Warranty Disclaimer
```csharp
{{COMPANY_NAME}} DOES NOT WARRANT THAT THE SOFTWARE WILL MEET YOUR REQUIREMENTS...
```
→ Replace `{{COMPANY_NAME}}`

**Line 571:** HTML Export Limitation of Liability
```csharp
IN NO EVENT SHALL {{COMPANY_NAME}} BE LIABLE FOR ANY SPECIAL, INCIDENTAL...
```
→ Replace `{{COMPANY_NAME}}`

---

### **Step 2: Update Application Title and Branding**

#### **File:** `NecessaryAdminTool\MainWindow.xaml.cs`

**Lines 1997, 2055:** PowerShell Update Script Headers
```csharp
Write-Output '>>> GENERAL UPDATE ENGINE';
Write-Output '>>> FEATURE UPDATE ENGINE (IN-PLACE UPGRADE)';
```
These have been white-labeled and do not require changes. If you wish to add your company name:
```csharp
Write-Output '>>> CONTOSO GENERAL UPDATE ENGINE';
```

---

### **Step 3: Update Configuration File Names (Optional)**

The application uses the following configuration file prefix: `NecessaryAdmin_`

#### Current Files:
- `NecessaryAdmin_Config_v2.xml`
- `NecessaryAdmin_UserConfig.xml`
- `NecessaryAdmin_Debug.log`
- `NecessaryAdmin_Runtime.log`

If you want to rebrand the config files, update the prefix in:
- **File:** `NecessaryAdminTool\MainWindow.xaml.cs`
- **Line 217:** Main config path
- **Line 650:** DC configuration path
- **Line 1990:** User config path

**Example:**
```csharp
// Before
"NecessaryAdmin_Config_v2.xml"

// After
"Contoso_Config_v2.xml"
```

---

## 🔍 Quick Search Commands

Use these commands to find all white-label placeholders:

### **Find all company name placeholders:**
```bash
grep -r "{{COMPANY_NAME}}" --include="*.xaml" --include="*.cs"
```

### **Find all domain placeholders:**
```bash
grep -r "{{COMPANY_DOMAIN}}" --include="*.xaml" --include="*.cs"
```

### **Find all config file references:**
```bash
grep -r "NecessaryAdmin_" --include="*.cs"
```

---

## ✅ White-Label Verification Checklist

Before deploying to your organization, verify all placeholders are replaced:

- [ ] **AboutWindow.xaml** - All 4 occurrences of `{{COMPANY_NAME}}` and `{{COMPANY_DOMAIN}}` replaced
- [ ] **AboutWindow.xaml.cs** - All 5 occurrences in HTML export functions replaced
- [ ] **README.md** - Configuration file names updated (if rebranded)
- [ ] **Application tested** - About window displays correctly
- [ ] **HTML export tested** - Legal document exports with correct company info
- [ ] **Email links tested** - Support email links work correctly

---

## 📋 Example: Complete White-Label for "Contoso Corporation"

### Replace Tokens:
```
{{COMPANY_NAME}}   → Contoso Corporation
{{COMPANY_DOMAIN}} → contoso.com
```

### Result in AboutWindow.xaml:
```xml
<!-- Before -->
<Run>Email: support@{{COMPANY_DOMAIN}}</Run>

<!-- After -->
<Run>Email: support@contoso.com</Run>
```

### Result in License Agreement:
```
Before: "IN NO EVENT SHALL {{COMPANY_NAME}} BE LIABLE..."
After:  "IN NO EVENT SHALL Contoso Corporation BE LIABLE..."
```

---

## 🚨 Important Notes

1. **Legal Review Required:** Have your legal department review all license agreement changes
2. **Consistent Naming:** Use the same company name format everywhere (e.g., "LLC" vs "L.L.C.")
3. **Email Validation:** Ensure support email domain is valid and monitored
4. **Backup Original:** Keep a copy of the original white-label template files
5. **Version Control:** Commit white-label changes to a separate branch

---

## 📞 White-Label Support

For assistance with white-labeling NecessaryAdminTool:
- **Documentation:** This guide and AUTO_UPDATE_GUIDE.md
- **Search Tags:** `#WHITELABEL`, `#COMPANY_BRANDING`
- **Files Modified:** AboutWindow.xaml, AboutWindow.xaml.cs, MainWindow.xaml.cs

---

## 📊 Files Affected by White-Labeling

| File | Placeholders | Lines | Purpose |
|------|--------------|-------|---------|
| **AboutWindow.xaml** | {{COMPANY_NAME}} (×3)<br>{{COMPANY_DOMAIN}} (×1) | 331, 336, 510-512 | Legal disclaimers, contact info |
| **AboutWindow.xaml.cs** | {{COMPANY_NAME}} (×3)<br>{{COMPANY_DOMAIN}} (×2) | 525-527, 568, 571 | HTML export legal text |
| **MainWindow.xaml.cs** | None (white-labeled) | 1997, 2055 | Update script headers |
| **README.md** | Config file names | 129-132 | Documentation |

**Total Placeholders:** 9 occurrences across 2 files

---

## 🔄 Automation Script (PowerShell)

Use this PowerShell script to automatically replace all placeholders:

```powershell
# White-Label Configuration Script
$CompanyName = "Contoso Corporation"
$CompanyDomain = "contoso.com"

$files = @(
    "NecessaryAdminTool\AboutWindow.xaml",
    "NecessaryAdminTool\AboutWindow.xaml.cs"
)

foreach ($file in $files) {
    $content = Get-Content $file -Raw
    $content = $content -replace '\{\{COMPANY_NAME\}\}', $CompanyName
    $content = $content -replace '\{\{COMPANY_DOMAIN\}\}', $CompanyDomain
    Set-Content $file $content -NoNewline
    Write-Host "Updated: $file"
}

Write-Host "`nWhite-labeling complete for $CompanyName!"
Write-Host "Support email: support@$CompanyDomain"
```

**Usage:**
1. Save as `WhiteLabel-Configure.ps1`
2. Edit `$CompanyName` and `$CompanyDomain` variables
3. Run: `.\WhiteLabel-Configure.ps1`

---

**Last Updated:** February 14, 2026
**Version:** 1.0 (1.2602.0.0)
**Maintained by:** Brandon Necessary
**Built with Claude Code** 🤖
