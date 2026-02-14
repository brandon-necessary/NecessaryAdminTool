using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("ArtaznIT Suite")]
[assembly: AssemblyDescription("Enterprise IT Management Suite")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Artazn LLC")]
[assembly: AssemblyProduct("ArtaznIT Suite")]
[assembly: AssemblyCopyright("Copyright © Artazn LLC 2026")]
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


// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version
//      Build Number
//      Revision
//
// CalVer Format: Major.YYMM.Minor.Build
// 7.2603.2.0 = Version 7.1, March 2026, iteration 2, alpha 2
// Version 7.1 - Automation & Analytics (Alpha 2)
// - Dashboard Analytics: Visual fleet health overview with statistics cards
//   * Total/Online/Offline computer counts with percentages
//   * Health score calculation (online % + OS modernity)
//   * OS distribution charts (Win11/Win10/Win7/Legacy)
//   * Detailed Windows version breakdown (24H2, 23H2, Server 2022, etc.)
//   * Critical alerts (Win7, offline, low disk space)
//   * Top 10 computers by uptime ranking
// - Automated Remediation: One-click fixes for common IT issues
//   * Restart Windows Update (clear cache + restart service)
//   * Clear DNS Cache (ipconfig /flushdns)
//   * Restart Print Spooler service
//   * Enable WinRM for remote management
//   * Fix Time Sync with domain controller
//   * Clear Event Logs (Application/System/Security)
//   * Beautiful progress dialog with real-time statistics
//   * Multi-select support (execute on multiple computers)
//   * Admin-only Quick Fix context menu
//
// Version 7.0 Features (Complete):
// - Complete ActiveDirectoryManager integration with fallback
// - Parallel WMI query execution (3x faster fallback)
// - Failure cache cleanup (dynamic size limit based on AD computer count)
// - AD Object Browser with Kerberos auth
// - Connection Profiles, Bookmarks/Favorites, Export/Import Settings
[assembly: AssemblyVersion("7.2603.5.0")]
[assembly: AssemblyFileVersion("7.2603.5.0")]
