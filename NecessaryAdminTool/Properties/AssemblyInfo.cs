using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("NecessaryAdminTool")]
[assembly: AssemblyDescription("Enterprise Active Directory and Remote Management Tool")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Brandon Necessary")]
[assembly: AssemblyProduct("NecessaryAdminTool")]
[assembly: AssemblyCopyright("Copyright © Brandon Necessary 2026")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

//In order to begin building localizable applications, set
//<UICulture>CultureYouAreCodingWith</UICulture> in your .csproj file
//inside a <PropertyGroup>.  For example, if you are using US english
//in your source files, set the <UICulture> to en-US.  Then uncomment
//the NeutralResourceLanguage attribute below.  Update the "en-US" in
//the line below to match the UICulture setting in the project file.

//[assembly: NeutralResourcesLanguage("en-US", UltimateResourceFallbackLocation.Satellite)]


[assembly: ThemeInfo(
    ResourceDictionaryLocation.None, //where theme specific resource dictionaries are located
                                     //(used if a resource is not found in the page,
                                     // or application resource dictionaries)
    ResourceDictionaryLocation.SourceAssembly //where the generic resource dictionary is located
                                              //(used if a resource is not found in the page,
                                              // app, or any theme specific resource dictionaries)
)]


// TAG: #AUTO_UPDATE_VERSION #VERSION_SYSTEM #CALVER
// FUTURE CLAUDES: Update version numbers here with each release
// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version
//      Build Number
//      Revision
//
// CalVer Format: Major.YYMM.Minor.Build
// 2.2602.0.0 = Version 2.0, February 2026, UI Engine release
//
// CURRENT VERSION: 2.0 (2.2602.0.0) - "Modern UI" (February 15, 2026)
//
// VERSION 2.0 - MAJOR UI/UX MODERNIZATION
// - Toast Notification System (303+ non-blocking notifications)
//   * 4 types: Success, Info, Warning, Error with semantic colors
//   * Auto-duration calculation based on message length
//   * Max 5 concurrent toasts with animations
// - Command Palette (Ctrl+K)
//   * 25+ commands with fuzzy search
//   * Keyboard navigation (arrows, Enter, ESC)
//   * 6 categories: Scanning, Auth, Remote Tools, Quick Fixes, View, Filters
// - Fluent Design System Integration
//   * Windows 11 Mica materials and rounded corners
//   * Semantic color palette (Success/Warning/Error/Info)
//   * Segoe UI Variable typography
//   * Elevation shadows (2dp, 4dp, 8dp)
// - Card View + Grid View Toggle (Ctrl+T)
//   * 300x180 card layout for visual browsing
//   * Toggle between grid and card layouts
// - Skeleton Loaders (40-60% perceived performance improvement)
// - 11 Keyboard Shortcuts for rapid workflows
// - User Configuration Panels
//   * Toast notification preferences (enable/disable by type/category)
//   * Keyboard shortcut customization with conflict detection
// - Complete Tag System (#AUTO_UPDATE_UI_ENGINE)
//   * 186+ tags across 27 files for easy maintenance
// - Comprehensive Documentation (CLAUDE.md, README.md, FAQ.md updated)
//
// VERSION 1.0 (1.2602.0.0) - "Foundation" (February 14, 2026)
// - Complete rebrand from ArtaznIT to NecessaryAdminTool
// - All v7.x features (169 total) - see docs/archive for details
//
// VERSION 3.0 (3.2602.0.0) - "Enhanced Dashboard" (February 20, 2026)
// - KPI Cards with sparklines + animated value transitions
// - Device health heatmap (fleet overview)
// - Status bar with live fleet summary
// - Global filter bar (Ctrl+F)
// - Contextual detail drawer (slide-in panel)
// - Row hover actions (RDP, PS, C$, Pin)
// - Breadcrumb navigation
// - Activity feed / event timeline
// - Display density toggle (Compact/Normal/Comfortable)
// - Deployment results auto-load on login
// - GeneralUpdate.ps1: partial-success handling + WU scan retry
//
// VERSION 3.0.1 (3.2602.1.0) - Patch (February 20, 2026)
// - Credential prompt forced to foreground on EDR fallback
// - Unified database/deployment path fallbacks (fixes CSV not detected)
// - Script download injects configured DeploymentLogDirectory
//
// VERSION 3.0.2 (3.2602.2.0) - Audit Fixes (February 20, 2026)
// - Fix 5 missing XAML resources in DatabaseSetupWizard (runtime crash)
// - Fix missing AccentButton style in SuperAdminWindow
// - Wire GridInventory_SelectionChanged to detail drawer
// - BtnSyncDB now saves inventory to database (was fake stub)
// - Fix build-installer.ps1 MSBuild path + WiX extensions
// - Fix duplicate ComponentRef in Product.wxs
// - Card view RDP/PS buttons wired with click handlers
//
[assembly: AssemblyVersion("3.2602.2.0")]
[assembly: AssemblyFileVersion("3.2602.2.0")]
