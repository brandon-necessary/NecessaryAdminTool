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
// 1.2602.0.0 = Version 1.0, February 2026, initial release
//
// CURRENT VERSION: 1.0 (1.2602.0.0) - "Foundation" (February 14, 2026)
// - Complete rebrand from ArtaznIT to NecessaryAdminTool
// - Unified theme system across all windows
// - Database layer (4 providers with AES-256 encryption)
// - Database testing system (25+ automated tests)
// - All v7.x features included (169 total):
//   * Dashboard Analytics with fleet health metrics
//   * Automated Remediation (one-click fixes)
//   * Custom Script Executor with PowerShell library
//   * Asset Tagging System (manual + auto-tagging)
//   * Advanced Filtering & Search
//   * Patch Management Integration
//   * RMM Tool Integration (6 platforms)
//   * AD Object Browser with Kerberos auth
//   * Connection Profiles & Bookmarks/Favorites
//   * Export/Import All Settings
//   * Performance Optimizations (3-4x faster scanning)
// - Prepared for v1.1+ enhancements:
//   * Auto-update system (Squirrel.Windows)
//   * Windows Service for background scanning
//   * Enhanced reporting (PDF/Excel)
//
// Previous codebase (ArtaznIT v7.2603.5.0) - fully migrated
[assembly: AssemblyVersion("1.2602.0.0")]
[assembly: AssemblyFileVersion("1.2602.0.0")]
