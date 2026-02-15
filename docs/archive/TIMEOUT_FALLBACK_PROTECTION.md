# Timeout & Fallback Protection - ArtaznIT Suite

## Problem Solved
**Issue**: Application would freeze/lockup when CIM queries hung or timed out
**Solution**: Multi-layer timeout and fallback protection at every level

---

## Protection Layers

### Layer 1: DC Health Check (5-second timeout)
**Location**: Domain Controller probing
**Protection**:
- DNS lookup: 3-second timeout
- Ping: 2-second timeout
- Overall health check: 5-second timeout
- **Fallback**: Mark as "(timeout)" but still selectable

**Code**:
```csharp
// DNS with timeout
var dnsTask = Dns.GetHostEntryAsync(dc);
if (await Task.WhenAny(dnsTask, Task.Delay(3000)) == dnsTask)
    // DNS succeeded
else
    ip = "DNS Timeout";

// Overall timeout - mark unresponsive DCs
var completed = await Task.WhenAny(Task.WhenAll(dcHealthTasks), Task.Delay(5000));
// Mark any still-probing DCs as timeout
```

**User Impact**: DCs that don't respond to ping in 5 seconds are marked "(timeout)" but remain selectable for querying

---

### Layer 2: CIM Connection Timeout (10 seconds)
**Location**: GetSystemSpecsAsync - CIM connection phase
**Protection**:
- CIM connection attempt: 10-second hard timeout
- **Fallback**: Automatically switch to WMI if CIM times out

**Code**:
```csharp
var cimConnectTask = Task.Run(() => _cimManager.GetConnection(...));
var timeoutTask = Task.Delay(10000);

if (await Task.WhenAny(cimConnectTask, timeoutTask) == timeoutTask)
{
    LogManager.LogDebug($"[CIM] Connection TIMEOUT - falling back to WMI");
    throw new TimeoutException("CIM connection exceeded 10 seconds");
}
```

**User Impact**: If CIM takes >10 seconds to connect, automatically falls back to WMI. No UI freeze.

---

### Layer 3: Query Execution Timeout (ConfigTimeout)
**Location**: GetSystemSpecsAsync - parallel query execution
**Protection**:
- All queries run with `SecureConfig.WmiTimeoutMs` timeout
- Hard timeout abandons stuck queries
- **Fallback**: Returns partial data with "TIMEOUT" status

**Code**:
```csharp
var allQueriesTask = Task.WhenAll(wmiTasks);
var timeoutTask = Task.Delay(SecureConfig.WmiTimeoutMs, cts.Token);

var completedTask = await Task.WhenAny(allQueriesTask, timeoutTask);

if (completedTask == timeoutTask)
{
    LogManager.LogDebug($"[FORCED TIMEOUT] Abandoning stuck operations");
    spec.Protocol = "TIMEOUT";
    return spec; // Force-return with partial data
}
```

**User Impact**: If queries hang beyond timeout, method returns with whatever data was collected. System shows as "TIMEOUT" but doesn't freeze UI.

---

### Layer 4: Protocol Fallback (CIM → WMI)
**Location**: HybridQueryHelper
**Protection**:
- Try CIM (WSMan) first
- Fall back to CIM (DCOM) on WSMan failure
- Fall back to WMI (DCOM) if both CIM protocols fail
- **Fallback**: Always returns data via some protocol

**Code**:
```csharp
try {
    // Try CIM (WSMan)
    var session = _cimManager.GetConnection(..., out protocol);
    return ($"CIM ({protocol})", cimResults, null);
}
catch (CimException ex) {
    LogManager.LogDebug($"CIM failed, using WMI fallback");
    // Try WMI
    var scope = _wmiManager.GetConnection(...);
    return ("WMI (Fallback)", null, wmiResults);
}
```

**User Impact**: Even if CIM completely fails, WMI provides data. Zero functionality loss.

---

## Timeout Values

| Operation | Timeout | Fallback Action |
|-----------|---------|-----------------|
| **DC DNS Lookup** | 3 seconds | Mark as "DNS Timeout", still queryable |
| **DC Ping** | 2 seconds | Mark as offline, still queryable |
| **DC Health Overall** | 5 seconds | Mark as "(timeout)", still queryable |
| **CIM Connection** | 10 seconds | Fall back to WMI |
| **Individual Query** | ConfigTimeout | Skip to next query |
| **All Queries** | ConfigTimeout | Return partial data, mark TIMEOUT |

**Default ConfigTimeout**: Check `SecureConfig.WmiTimeoutMs` (typically 30-60 seconds)

---

## Logging Output

### Successful CIM Query
```
[CIM] Attempting connection to PC-SALES-01...
[CIM] Connection SUCCESS for PC-SALES-01 using WSMan
[QueryInstances] Target: PC-SALES-01 | Namespace: root/cimv2 | Query: SELECT...
[CIM] Query SUCCESS: WSMan | Results: 1 | Time: 89ms
```

### CIM Connection Timeout → WMI Fallback
```
[CIM] Attempting connection to OLD-SERVER...
[CIM] Connection TIMEOUT for OLD-SERVER after 10s - falling back to WMI
[CIM] Connection failed for OLD-SERVER, falling back to WMI: TimeoutException
[WMI] Fallback SUCCESS for OLD-SERVER | Results: 1 | Time: 1234ms
```

### Complete Timeout (Stuck Queries)
```
[CIM] Attempting connection to DEAD-PC...
[CIM] Connection TIMEOUT for DEAD-PC after 10s - falling back to WMI
[WMI] Attempting fallback for DEAD-PC...
[FORCED TIMEOUT] Queries for DEAD-PC exceeded 30000ms - abandoning stuck operations
Scan cancelled/timeout: DEAD-PC
```

---

## Prevented Issues

### Before (Problems):
❌ App freezes when CIM hangs
❌ UI locks up waiting for response
❌ No way to cancel stuck operations
❌ Users must force-kill application

### After (Solutions):
✅ Hard timeouts prevent indefinite waits
✅ Automatic fallback to WMI
✅ Returns partial data instead of hanging
✅ Verbose logging shows exactly what happened
✅ UI remains responsive during all operations

---

## Testing Checklist

Test these scenarios to verify protection:

- [ ] **Offline System** → Should timeout and mark as TIMEOUT within configured time
- [ ] **Firewall Blocking WinRM** → Should timeout CIM, fall back to WMI successfully
- [ ] **Firewall Blocking WMI** → Should timeout both protocols, mark as TIMEOUT
- [ ] **Very Slow Network** → Should complete within timeout or abandon gracefully
- [ ] **DC Not Responding to Ping** → Should mark as timeout but remain queryable
- [ ] **Mid-Query Hang** → Should abandon after timeout, return partial data
- [ ] **Connection Phase Hang** → Should timeout at 10s, attempt WMI fallback

All scenarios should:
1. **Not freeze the UI**
2. **Log detailed information** about what happened
3. **Return gracefully** with status indicator
4. **Allow user to continue** using the application

---

## Configuration

To adjust timeout values, modify `SecureConfig` class:

```csharp
public static class SecureConfig
{
    public static int WmiTimeoutMs = 30000;  // Main query timeout (30 seconds)
    // CIM connection timeout is hardcoded to 10 seconds
    // DC health timeout is hardcoded to 5 seconds
}
```

**Recommendations**:
- **LAN environments**: 15-30 seconds (faster networks)
- **WAN environments**: 30-60 seconds (slower networks, VPN)
- **Mixed environments**: 30 seconds (balanced)

---

## Key Takeaway

**Every CIM/WMI operation now has multiple layers of protection:**
1. Timeout at connection phase (10s)
2. Timeout at query execution phase (configurable)
3. Timeout for overall operation (configurable)
4. Automatic protocol fallback (CIM → WMI)
5. Graceful degradation (partial data vs total failure)

**Result**: The application will NEVER freeze waiting for remote systems, and will always provide the best data possible given the circumstances.
