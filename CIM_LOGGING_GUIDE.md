# CIM Migration - Logging Guide

## Understanding the New Log Output

With verbose logging enabled, every CIM/WMI operation now produces detailed diagnostic information.

### Log Prefixes

All CIM-related logs use prefixes for easy filtering:

- `[CimSessionManager]` - Connection pooling and session management
- `[CIM]` - CIM operations (queries, methods)
- `[WMI]` - WMI fallback operations
- `[QueryInstances]` - Query execution details
- `[InvokeMethod]` - Method invocation details
- `[InvokeInstanceMethod]` - Instance method details
- `[BOTH FAILED]` - Critical errors where both CIM and WMI failed

### Typical Successful Query Flow

```
[QueryInstances] Target: PC-SALES-01 | Namespace: root/cimv2 | Query: SELECT * FROM Win32_OperatingSystem...
[CimSessionManager] Requesting connection to PC-SALES-01 (user: admin)
[CimSessionManager] No valid cached connection, creating new WSMan session for PC-SALES-01...
[CimSessionManager] WSMan connection SUCCESS for PC-SALES-01 (time: 156ms)
[CIM] Connection established: WSMan
[CIM] Query SUCCESS: WSMan | Results: 1 | Time: 89ms
```

**Total Time**: ~245ms on WAN (vs 3-5 seconds with old WMI-only)

### With Connection Reuse

```
[QueryInstances] Target: PC-SALES-01 | Namespace: root/cimv2 | Query: SELECT * FROM Win32_ComputerSystem...
[CimSessionManager] Requesting connection to PC-SALES-01 (user: admin)
[CimSessionManager] Testing cached WSMan connection for PC-SALES-01...
[CimSessionManager] Reusing cached WSMan connection for PC-SALES-01
[CIM] Connection established: WSMan
[CIM] Query SUCCESS: WSMan | Results: 1 | Time: 34ms
```

**Total Time**: ~34ms (cached connection eliminates handshake overhead)

### Fallback to DCOM (Legacy System)

```
[QueryInstances] Target: SRV-2008-DB | Namespace: root/cimv2 | Query: SELECT * FROM Win32_Service...
[CimSessionManager] Requesting connection to SRV-2008-DB (user: admin)
[CimSessionManager] No valid cached connection, creating new WSMan session for SRV-2008-DB...
[CimSessionManager] WSMan FAILED for SRV-2008-DB: StatusCode=InvalidClass, ErrorCode=2150858778, Message=...
[CimSessionManager] Attempting DCOM fallback for SRV-2008-DB...
[CimSessionManager] DCOM connection SUCCESS for SRV-2008-DB (time: 234ms)
[CIM] Connection established: DCOM
[CIM] Query SUCCESS: DCOM | Results: 145 | Time: 456ms
```

**Result**: Still using CIM, but via DCOM instead of WSMan

### Full Fallback to WMI (Very Old System)

```
[QueryInstances] Target: OLD-XP-KIOSK | Namespace: root/cimv2 | Query: SELECT * FROM Win32_Process...
[CimSessionManager] Requesting connection to OLD-XP-KIOSK (user: admin)
[CimSessionManager] No valid cached connection, creating new WSMan session for OLD-XP-KIOSK...
[CimSessionManager] WSMan FAILED for OLD-XP-KIOSK: StatusCode=AccessDenied, ErrorCode=5, Message=...
[CimSessionManager] Attempting DCOM fallback for OLD-XP-KIOSK...
[CimSessionManager] BOTH WSMan and DCOM FAILED for OLD-XP-KIOSK
[WSMan Error] StatusCode: AccessDenied, ErrorCode: 5
[DCOM Error] CimException: Access denied
[CIM] Query FAILED for OLD-XP-KIOSK: CimException - Access denied
[WMI] Attempting fallback for OLD-XP-KIOSK...
[WMI] Fallback SUCCESS for OLD-XP-KIOSK | Results: 67 | Time: 1234ms
```

**Result**: WMI fallback ensures functionality even on unsupported systems

### Method Invocation (Process Creation)

```
[InvokeMethod] Target: PC-IT-05 | Class: Win32_Process | Method: Create | Params: CommandLine=notepad.exe
[CIM] Attempting method invocation on PC-IT-05...
[CimSessionManager] Reusing cached WSMan connection for PC-IT-05
[CIM] Connection established: WSMan
[CIM] Method SUCCESS: WSMan | ReturnValue: 0 | Time: 67ms
```

**ReturnValue 0** = Success (process created)

### Error Scenario

```
[QueryInstances] Target: PC-OFFLINE-99 | Namespace: root/cimv2 | Query: SELECT * FROM Win32_BIOS...
[CimSessionManager] Requesting connection to PC-OFFLINE-99 (user: admin)
[CimSessionManager] No valid cached connection, creating new WSMan session for PC-OFFLINE-99...
[CimSessionManager] WSMan FAILED for PC-OFFLINE-99: StatusCode=ServerNotReachable, ErrorCode=2150858770, Message=...
[CimSessionManager] Attempting DCOM fallback for PC-OFFLINE-99...
[CimSessionManager] BOTH WSMan and DCOM FAILED for PC-OFFLINE-99
[WSMan Error] StatusCode: ServerNotReachable, ErrorCode: 2150858770
[DCOM Error] COMException: The RPC server is unavailable
[CIM] Query FAILED for PC-OFFLINE-99: CimException - Server not reachable
[WMI] Attempting fallback for PC-OFFLINE-99...
[BOTH FAILED] CIM and WMI queries both failed for PC-OFFLINE-99
[CIM Error] CimException: Server not reachable
[WMI Error] COMException: The RPC server is unavailable
```

**Result**: System is offline - both protocols failed (expected behavior)

---

## Performance Monitoring

Look for these timing patterns in your logs:

| Scenario | Expected Time | Protocol |
|----------|--------------|----------|
| **Modern Windows (LAN)** | 50-200ms | CIM (WSMan) |
| **Modern Windows (WAN)** | 200-800ms | CIM (WSMan) |
| **Legacy Windows (LAN)** | 100-400ms | CIM (DCOM) or WMI |
| **Legacy Windows (WAN)** | 800-3000ms | WMI (Fallback) |
| **Cached connection** | 20-100ms | CIM (reused) |

**Key Indicator**: If you see most queries using "CIM (WSMan)" with times under 500ms on WAN, the migration is working perfectly.

---

## Common CIM Error Codes

| StatusCode | ErrorCode | Meaning | Action |
|------------|-----------|---------|--------|
| AccessDenied | 5 | Invalid credentials or permissions | Check username/password |
| InvalidNamespace | 2150858779 | Namespace doesn't exist | Verify namespace path |
| InvalidClass | 2150858778 | WMI class not found | Check query syntax |
| ServerNotReachable | 2150858770 | Target offline or firewall | Check network/firewall |
| TimedOut | 2150858793 | Query took too long | Increase timeout or optimize query |

---

## Filtering Logs

To focus on specific areas, filter by prefix:

**Connection issues only:**
```
Filter: [CimSessionManager]
```

**Query performance:**
```
Filter: [CIM] Query SUCCESS
Look for: Time: XXXms
```

**Fallback events:**
```
Filter: [WMI] Attempting fallback
```

**Critical errors:**
```
Filter: [BOTH FAILED]
```

---

## What to Monitor

### Week 1: Validate CIM Adoption
- Count: How many queries use "CIM (WSMan)" vs "WMI (Fallback)"
- Target: >80% on modern networks

### Week 2: Performance Baseline
- Compare: Average query times before/after
- Expected: 4-10x improvement on WAN queries

### Week 3: Identify Optimization Opportunities
- Find: Systems always falling back to WMI
- Action: Investigate firewall/WinRM configuration

---

## Troubleshooting

**Problem**: All queries use WMI (Fallback)
- **Check**: Windows Firewall allows WinRM (port 5985/5986)
- **Check**: WinRM service is running: `winrm quickconfig`
- **Check**: Credentials have remote access permissions

**Problem**: DCOM works but WSMan doesn't
- **Likely**: WinRM not configured on target
- **Solution**: Enable WinRM: `Enable-PSRemoting -Force`

**Problem**: Slow queries even with CIM
- **Check**: Network latency to target
- **Check**: Query complexity (SELECT * is slower than specific columns)
- **Check**: Result count (100+ results take longer)

---

## Success Criteria

✅ **Migration Successful** if you see:
1. Most queries report "CIM (WSMan)" on modern systems
2. Query times <500ms on WAN for simple queries
3. Zero functionality loss (WMI fallback catches legacy systems)
4. Connection cache hit rate >60% (cached connections reused)

🎯 **Optimization Opportunity** if you see:
- Many "WSMan FAILED" → Configure WinRM on targets
- High "DCOM" usage → Check if WSMan can be enabled
- Frequent "WMI (Fallback)" → Legacy systems may need upgrades
