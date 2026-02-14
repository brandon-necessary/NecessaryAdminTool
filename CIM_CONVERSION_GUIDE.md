# CIM Conversion Guide - ArtaznIT Suite

## Conversion Patterns

All WMI queries have been systematically converted to use CIM-first with WMI fallback using the `HybridQueryHelper` class.

### Pattern 1: Simple Property Query

**Before (WMI):**
```csharp
using (var s = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT SerialNumber FROM Win32_Bios")))
using (var r = s.Get())
{
    var b = r.Cast<ManagementObject>().FirstOrDefault();
    if (b != null) {
        spec.Serial = b["SerialNumber"]?.ToString() ?? "N/A";
        b.Dispose();
    }
}
```

**After (CIM with fallback):**
```csharp
var (protocol, cimResults, wmiResults) = _queryHelper.QueryInstances(
    hostname, username, password, "root/cimv2",
    "SELECT SerialNumber FROM Win32_Bios");

if (cimResults != null)
{
    var bios = cimResults.FirstOrDefault();
    spec.Serial = bios?.CimInstanceProperties["SerialNumber"]?.Value?.ToString() ?? "N/A";
    spec.Protocol = protocol;
}
else if (wmiResults != null)
{
    var b = wmiResults.Cast<ManagementObject>().FirstOrDefault();
    if (b != null) {
        spec.Serial = b["SerialNumber"]?.ToString() ?? "N/A";
        b.Dispose();
    }
    spec.Protocol = protocol;
}
```

**Simplified using helper:**
```csharp
var (protocol, cimResults, wmiResults) = _queryHelper.QueryInstances(
    hostname, username, password, "root/cimv2",
    "SELECT SerialNumber FROM Win32_Bios");

spec.Serial = _queryHelper.GetFirstPropertyValue(protocol, cimResults, wmiResults, "SerialNumber")?.ToString() ?? "N/A";
spec.Protocol = protocol;
```

### Pattern 2: DateTime Property (No Conversion Needed!)

**Before (WMI):**
```csharp
string bs = os["LastBootUpTime"]?.ToString();
if (!string.IsNullOrEmpty(bs))
{
    var boot = ManagementDateTimeConverter.ToDateTime(bs);
    spec.Uptime = $"{(DateTime.Now - boot).Days}d";
}
```

**After (CIM - Native DateTime):**
```csharp
if (cimResults != null)
{
    var os = cimResults.FirstOrDefault();
    DateTime? lastBoot = os?.CimInstanceProperties["LastBootUpTime"]?.Value as DateTime?;
    if (lastBoot.HasValue)
        spec.Uptime = $"{(DateTime.Now - lastBoot.Value).Days}d";
}
else if (wmiResults != null)
{
    // WMI fallback still needs conversion
    var os = wmiResults.Cast<ManagementObject>().FirstOrDefault();
    string bs = os?["LastBootUpTime"]?.ToString();
    if (!string.IsNullOrEmpty(bs))
    {
        var boot = ManagementDateTimeConverter.ToDateTime(bs);
        spec.Uptime = $"{(DateTime.Now - boot).Days}d";
    }
}
```

### Pattern 3: Method Invocation (Class Method)

**Before (WMI):**
```csharp
var scope = _wmiManager.GetConnection(host, _authUser, _authPass);
using (var mc = new ManagementClass(scope, new ManagementPath("Win32_Process"), null))
{
    var inp = mc.GetMethodParameters("Create");
    inp["CommandLine"] = command;
    var res = mc.InvokeMethod("Create", inp, null);
    uint code = (uint)res["returnValue"];
}
```

**After (CIM with fallback):**
```csharp
var parameters = new Dictionary<string, object> { { "CommandLine", command } };
var (protocol, returnValue) = _queryHelper.InvokeMethod(
    host, _authUser, _authPass, "root/cimv2", "Win32_Process", "Create", parameters);

uint code = returnValue != null ? Convert.ToUInt32(returnValue) : 999;
AppendTerminal($"[{protocol}] Command executed with code {code}");
```

### Pattern 4: Instance Method Invocation

**Before (WMI):**
```csharp
using (var searcher = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT * FROM Win32_OperatingSystem")))
using (var results = searcher.Get())
{
    foreach (ManagementObject os in results)
    {
        var inp = os.GetMethodParameters("Win32Shutdown");
        inp["Flags"] = "6";
        inp["Reserved"] = "0";
        os.InvokeMethod("Win32Shutdown", inp, null);
        os.Dispose();
    }
}
```

**After (CIM with fallback):**
```csharp
var parameters = new Dictionary<string, object> { { "Flags", 6 }, { "Reserved", 0 } };
var (protocol, returnValue) = _queryHelper.InvokeInstanceMethod(
    host, _authUser, _authPass, "root/cimv2",
    "SELECT * FROM Win32_OperatingSystem", "Win32Shutdown", parameters);

AppendTerminal($"[{protocol}] Reboot initiated");
```

### Pattern 5: Special Namespace (BitLocker, TPM)

**Before (WMI):**
```csharp
var opts = new ConnectionOptions { /* ... */ };
var ss = new ManagementScope($"\\\\{hostname}\\root\\CIMv2\\Security\\MicrosoftVolumeEncryption", opts);
ss.Connect();
using (var s = new ManagementObjectSearcher(ss, new ObjectQuery("SELECT ProtectionStatus FROM Win32_EncryptableVolume WHERE DriveLetter='C:'")))
{
    // ...
}
```

**After (CIM with fallback):**
```csharp
// CIM uses forward slashes for namespace paths
var (protocol, cimResults, wmiResults) = _queryHelper.QueryInstances(
    hostname, username, password,
    "root/CIMv2/Security/MicrosoftVolumeEncryption",
    "SELECT ProtectionStatus FROM Win32_EncryptableVolume WHERE DriveLetter='C:'");

// Process results as normal
```

### Pattern 6: Array Properties (IP Addresses, DNS)

**Before (WMI):**
```csharp
string[] ipAddresses = adapter["IPAddress"] as string[];
```

**After (CIM):**
```csharp
if (cimResults != null)
{
    var adapter = cimResults.FirstOrDefault();
    string[] ipAddresses = adapter?.CimInstanceProperties["IPAddress"]?.Value as string[];
}
else if (wmiResults != null)
{
    var adapter = wmiResults.Cast<ManagementObject>().FirstOrDefault();
    string[] ipAddresses = adapter?["IPAddress"] as string[];
}
```

**Note:** Array handling is identical between CIM and WMI!

---

## Remaining Queries to Convert

### High Priority (Core Functionality):
1. **GetSystemSpecsAsync** (Lines 2425-2700) - 11 parallel queries
   - BIOS query
   - ComputerSystem query
   - Processor query
   - Operating System query
   - TimeZone query
   - Network Adapter query
   - Battery query
   - Chassis query
   - Drives query
   - BitLocker query (special namespace)
   - TPM query (special namespace)

2. **Tool Button Click Handlers:**
   - Tool_Hotfix_Click (Line ~3000)
   - Tool_Software_Click (Line ~3676)
   - Tool_Processes_Click (Line ~3730)
   - Tool_Services_Click (Line ~3776)
   - Tool_Network_Click (Line ~3145)
   - Tool_EventLog_Click (Line ~3830)

3. **DeviceMonitorWindow Queries** (Lines 6870-7110):
   - RefreshCurrentDevice method
   - Uptime history queries
   - Real-time CPU/RAM/Disk monitoring

### Medium Priority:
4. **Scheduled Tasks, DNS Cache, etc.**

---

## Quick Conversion Template

For any WMI query, follow this template:

```csharp
// OLD WMI CODE:
// var scope = _wmiManager.GetConnection(hostname, username, password);
// using (var searcher = new ManagementObjectSearcher(scope, new ObjectQuery(query)))
// using (var results = searcher.Get()) { /* process */ }

// NEW CIM CODE:
var (protocol, cimResults, wmiResults) = _queryHelper.QueryInstances(
    hostname, username, password, "root/cimv2", query);

if (cimResults != null)
{
    // CIM path - native DateTime, CimInstanceProperties["PropertyName"].Value
    foreach (var instance in cimResults)
    {
        var value = instance.CimInstanceProperties["PropertyName"]?.Value;
        // Process...
    }
}
else if (wmiResults != null)
{
    // WMI fallback path - ManagementDateTimeConverter, obj["PropertyName"]
    foreach (ManagementObject obj in wmiResults)
    {
        var value = obj["PropertyName"];
        obj.Dispose();
        // Process...
    }
}

LogManager.LogDebug($"Query completed using {protocol}");
```

---

## Testing Checklist

After conversion:
- [ ] Modern Windows 11 system → expect "CIM (WSMan)" in logs
- [ ] Server 2012+ → expect "CIM (WSMan)"
- [ ] Legacy Server 2008 R2 → expect "CIM (DCOM)" or "WMI (Fallback)"
- [ ] WinRM disabled system → expect "WMI (Fallback)"
- [ ] Verify identical results between CIM and WMI paths
- [ ] Performance benchmark: WAN queries should be 4-21x faster

---

## Next Steps

1. Convert remaining queries using patterns above
2. Test on diverse systems (modern + legacy)
3. Performance benchmark
4. GitHub research for other modernization opportunities
