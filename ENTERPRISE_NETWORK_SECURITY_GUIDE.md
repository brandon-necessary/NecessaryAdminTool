# Enterprise Network & Security Configuration Guide
<!-- TAG: #ENTERPRISE_GUIDE #NETWORK_REQUIREMENTS #EDR_CONFIGURATION #FIREWALL #VERSION_2_4 -->

**Version:** 2.5 (2.2602.5.0)
**Last Updated:** February 17, 2026
**Audience:** IT Security, Network Engineers, System Administrators

---

## Table of Contents

1. [Overview](#overview)
2. [Required Network Ports](#required-network-ports)
   - [Active Directory & LDAP](#active-directory--ldap)
   - [WMI & DCOM (Remote System Scanning)](#wmi--dcom-remote-system-scanning)
   - [SMB & Admin Shares](#smb--admin-shares)
   - [PowerShell Remoting (WinRM)](#powershell-remoting-winrm)
   - [Remote Desktop Protocol (RDP)](#remote-desktop-protocol-rdp)
   - [Database Connectivity](#database-connectivity)
   - [RMM Tool Ports](#rmm-tool-ports)
   - [Auto-Update Service](#auto-update-service)
3. [Windows Firewall Rules](#windows-firewall-rules)
4. [Windows Account & Privilege Requirements](#windows-account--privilege-requirements)
5. [EDR & Antivirus Exception Configuration](#edr--antivirus-exception-configuration)
   - [Palo Alto Cortex XDR](#palo-alto-cortex-xdr-detailed)
   - [CrowdStrike Falcon](#crowdstrike-falcon)
   - [Microsoft Defender / MDE](#microsoft-defender--mde)
   - [SentinelOne](#sentinelone)
   - [Other EDR Solutions](#other-edr-solutions)
6. [Group Policy Requirements](#group-policy-requirements)
7. [Common Symptoms & Troubleshooting](#common-symptoms--troubleshooting)
8. [Port Summary Table](#port-summary-table)

---

## Overview

NecessaryAdminTool is an enterprise Windows system administration tool that performs:
- **Remote WMI/CIM queries** against target workstations and servers
- **Active Directory LDAP queries** for fleet inventory
- **MMC snap-in hosting** (ADUC, GPMC, DNS, DHCP, etc.) with credential passthrough
- **Remote management** via RDP, PowerShell remoting, and 6 RMM platforms
- **Admin share access** (C$, ADMIN$, IPC$) for file/process operations

Because it performs privileged remote operations with **alternate domain credentials**, it is frequently flagged by EDR solutions as suspicious. This guide covers every exception and network rule required for correct operation.

---

## Required Network Ports

All ports listed are **outbound from the IT admin workstation** running NecessaryAdminTool to the **target computers** or **domain infrastructure**, unless otherwise noted.

---

### Active Directory & LDAP

Required for: Login authentication, AD fleet scan, ADUC snap-in, domain lookups

| Port | Protocol | Service | Direction | Required? |
|------|----------|---------|-----------|-----------|
| **53** | TCP + UDP | DNS | Outbound to DNS/DC | ✅ Required |
| **88** | TCP + UDP | Kerberos Authentication | Outbound to DCs | ✅ Required |
| **135** | TCP | RPC Endpoint Mapper | Outbound to DCs | ✅ Required |
| **389** | TCP + UDP | LDAP | Outbound to DCs | ✅ Required |
| **445** | TCP | SMB (SYSVOL, NETLOGON) | Outbound to DCs | ✅ Required |
| **464** | TCP + UDP | Kerberos Password Change | Outbound to DCs | Recommended |
| **636** | TCP | LDAPS (Secure LDAP) | Outbound to DCs | Optional (if LDAPS forced) |
| **3268** | TCP | Global Catalog | Outbound to DCs | Recommended |
| **3269** | TCP | Global Catalog SSL | Outbound to DCs | Optional (if GC SSL forced) |
| **49152–65535** | TCP | Dynamic RPC | Outbound to DCs | ✅ Required |

> **Note on Dynamic RPC:** Windows uses ports 49152–65535 for WMI, DCOM, and RPC traffic after the initial connection on TCP 135. These cannot be avoided without restricting the RPC dynamic port range via GPO. See [Group Policy Requirements](#group-policy-requirements) for how to restrict this range.

**LDAP Query Method:**
NecessaryAdminTool uses `DirectorySearcher` (LDAP, port 389) by default. Configurable in Options → Active Directory → Query Method. If your environment enforces LDAP signing or channel binding, ensure:
- Domain Controllers have the LDAP policy set to allow unsigned queries, **or**
- Configure the app to use LDAPS (port 636) in Options → Active Directory

---

### WMI & DCOM (Remote System Scanning)

Required for: Single system scan, Hardware tab, Software tab, Services tab, Event Logs tab, fleet scanning

| Port | Protocol | Service | Direction | Required? |
|------|----------|---------|-----------|-----------|
| **135** | TCP | RPC Endpoint Mapper (DCOM) | Outbound to targets | ✅ Required |
| **49152–65535** | TCP | Dynamic RPC (WMI data) | Outbound to targets | ✅ Required |

**What WMI is used for:**
- Hardware inventory (CPU, RAM, Disk, BIOS, Motherboard)
- Software inventory (installed applications)
- Running processes and services
- Network adapter configuration
- Event log queries
- Remote service control (start/stop/restart)
- Windows Update status

**On target computers**, the following must be enabled:
- Windows Management Instrumentation (WMI) service running
- Remote Administration firewall exception enabled
- COM Security permissions allowing remote access for the admin account

**Enable WMI access on targets (GPO or manual):**
```powershell
# Enable WMI firewall exception
netsh advfirewall firewall set rule group="Windows Management Instrumentation (WMI)" new enable=yes

# Or via PowerShell
Enable-NetFirewallRule -DisplayGroup "Windows Management Instrumentation (WMI)"
```

---

### SMB & Admin Shares

Required for: Browse C$ Share, file copy operations, IPC$ authentication, script deployment

| Port | Protocol | Service | Direction | Required? |
|------|----------|---------|-----------|-----------|
| **445** | TCP | SMB (Direct) | Outbound to targets | ✅ Required |
| **139** | TCP | NetBIOS Session Service | Outbound to targets | Legacy only |
| **137** | UDP | NetBIOS Name Service | Outbound to targets | Legacy only |
| **138** | UDP | NetBIOS Datagram Service | Outbound to targets | Legacy only |

> **Recommendation:** Port 445 (Direct SMB) is all that is needed on modern Windows. Ports 137–139 are only required if target computers are running Windows XP/2003 or have NetBIOS over TCP/IP explicitly required.

**Required admin shares on targets:**
- `\\COMPUTER\C$` — File browsing, script deployment
- `\\COMPUTER\ADMIN$` — Remote administration
- `\\COMPUTER\IPC$` — Named pipe communication (required for WMI, RPC)

**Enable admin shares (if disabled by policy):**
```powershell
# Re-enable admin shares
Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Services\LanmanServer\Parameters" `
    -Name "AutoShareWks" -Value 1 -Type DWord
Restart-Service LanmanServer
```

---

### PowerShell Remoting (WinRM)

Required for: PowerShell Remote tool, WinRM-based management, script execution

| Port | Protocol | Service | Direction | Required? |
|------|----------|---------|-----------|-----------|
| **5985** | TCP | WinRM over HTTP | Outbound to targets | ✅ Required (default) |
| **5986** | TCP | WinRM over HTTPS | Outbound to targets | Optional (if HTTPS enforced) |

**Enable WinRM on target computers:**
```powershell
# Run on target computers (or via GPO)
Enable-PSRemoting -Force

# Or via winrm command
winrm quickconfig -q

# Add trusted hosts (on the admin workstation, if not domain-joined targets)
Set-Item WSMan:\localhost\Client\TrustedHosts -Value "*" -Force
```

**WinRM via Group Policy:**
- Computer Configuration → Administrative Templates → Windows Components → Windows Remote Management (WinRM) → WinRM Service → Allow remote server management through WinRM

---

### Remote Desktop Protocol (RDP)

Required for: RDP quick launch button, remote desktop sessions

| Port | Protocol | Service | Direction | Required? |
|------|----------|---------|-----------|-----------|
| **3389** | TCP | RDP | Outbound to targets | ✅ Required |
| **3389** | UDP | RDP (UDP acceleration) | Outbound to targets | Recommended |

> NecessaryAdminTool launches `mstsc.exe` with the target hostname and optionally passes credentials via RDP credential files. Port 3389 must be open on target computers and any intermediate firewalls.

---

### Database Connectivity

Required for: Data persistence, fleet inventory storage, bookmark/profile storage

| Database | Port | Protocol | Notes |
|----------|------|----------|-------|
| **SQLite** (default) | None | Local file | No network ports — local `%ProgramData%\NecessaryAdminTool\` |
| **SQL Server** | **1433** TCP | SQL Server | Default instance. Named instances use dynamic ports. |
| **SQL Server Browser** | **1434** UDP | SQL Server Browser | Required for named instances |
| **SQL Server (named instance)** | Dynamic | TCP | Check SQL Server Configuration Manager for assigned port |
| **Microsoft Access** | None | Local file | No network ports — local file access only |
| **CSV** | None | Local file | No network ports — local file access only |

**SQL Server named instance port discovery:**
```sql
-- Run on SQL Server to find the port for a named instance
SELECT name, port FROM sys.dm_exec_connections WHERE session_id = @@SPID
```

**SQL Server firewall rule:**
```powershell
New-NetFirewallRule -DisplayName "SQL Server" -Direction Inbound `
    -Protocol TCP -LocalPort 1433 -Action Allow
```

---

### RMM Tool Ports

Required for: RMM Quick Launch buttons (TeamViewer, ScreenConnect, Datto, Kaseya, N-able, Atera)

#### TeamViewer

| Port | Protocol | Direction | Purpose |
|------|----------|-----------|---------|
| **5938** | TCP + UDP | Outbound | Primary TeamViewer connection |
| **443** | TCP | Outbound | Fallback HTTPS tunnel |
| **80** | TCP | Outbound | Fallback HTTP tunnel |

#### ConnectWise ScreenConnect (Control)

| Port | Protocol | Direction | Purpose |
|------|----------|-----------|---------|
| **443** | TCP | Outbound | HTTPS to ScreenConnect relay |
| **8040** | TCP | Outbound | ScreenConnect relay port (configurable) |
| **443** | TCP | Inbound (on SC server) | Client connections |

> Port 8040 may vary depending on your ScreenConnect server configuration. Check ScreenConnect Admin → Server Info.

#### Datto RMM (formerly Autotask Endpoint Management)

| Port | Protocol | Direction | Purpose |
|------|----------|-----------|---------|
| **443** | TCP | Outbound | HTTPS to Datto cloud (pinotage.centrastage.net) |
| **8443** | TCP | Outbound | Alternate HTTPS |

#### Kaseya VSA

| Port | Protocol | Direction | Purpose |
|------|----------|-----------|---------|
| **5721** | TCP | Outbound | Kaseya agent communication |
| **443** | TCP | Outbound | HTTPS to VSA server |

#### N-able N-sight / N-central

| Port | Protocol | Direction | Purpose |
|------|----------|-----------|---------|
| **443** | TCP | Outbound | HTTPS to N-able cloud |
| **80** | TCP | Outbound | HTTP fallback |
| **10000** | TCP | Outbound (to N-central server) | N-central agent communication |

#### NinjaRMM / NinjaOne

| Port | Protocol | Direction | Purpose |
|------|----------|-----------|---------|
| **443** | TCP | Outbound | HTTPS to NinjaRMM cloud |

#### Atera

| Port | Protocol | Direction | Purpose |
|------|----------|-----------|---------|
| **443** | TCP | Outbound | HTTPS to Atera cloud |

---

### Auto-Update Service

Required for: Squirrel.Windows auto-update checks and downloads

| Port | Protocol | Destination | Purpose |
|------|----------|-------------|---------|
| **443** | TCP | `api.github.com` | Check for new releases |
| **443** | TCP | `github.com` | Download release metadata |
| **443** | TCP | `objects.githubusercontent.com` | Download release binary/installer |

**Disable auto-updates (air-gapped environments):**

Option 1 — Registry (GPO-deployable, highest priority):
```reg
[HKEY_LOCAL_MACHINE\SOFTWARE\NecessaryAdminTool]
"DisableAutoUpdates"=dword:00000001
```

Option 2 — Marker file (for air-gapped/offline environments):
```
Place a file named ".no-updates" in the application directory
```

Option 3 — Options Window → "Disable Auto-Updates" checkbox

---

## Windows Firewall Rules

### On the Admin Workstation (outbound rules)

The following outbound rules are required on the machine running NecessaryAdminTool:

```powershell
# Allow outbound to domain controllers (AD/LDAP/Kerberos)
New-NetFirewallRule -DisplayName "NAT - Active Directory" `
    -Direction Outbound -Protocol TCP `
    -RemotePort 53,88,135,389,445,464,636,3268,3269 -Action Allow

New-NetFirewallRule -DisplayName "NAT - Kerberos UDP" `
    -Direction Outbound -Protocol UDP `
    -RemotePort 53,88,389,464 -Action Allow

# Allow outbound WMI / Dynamic RPC
New-NetFirewallRule -DisplayName "NAT - WMI Dynamic RPC" `
    -Direction Outbound -Protocol TCP `
    -RemotePort 49152-65535 -Action Allow

# Allow outbound SMB
New-NetFirewallRule -DisplayName "NAT - SMB" `
    -Direction Outbound -Protocol TCP `
    -RemotePort 445 -Action Allow

# Allow outbound WinRM
New-NetFirewallRule -DisplayName "NAT - WinRM" `
    -Direction Outbound -Protocol TCP `
    -RemotePort 5985,5986 -Action Allow

# Allow outbound RDP
New-NetFirewallRule -DisplayName "NAT - RDP" `
    -Direction Outbound -Protocol TCP `
    -RemotePort 3389 -Action Allow

# Allow outbound SQL Server
New-NetFirewallRule -DisplayName "NAT - SQL Server" `
    -Direction Outbound -Protocol TCP `
    -RemotePort 1433 -Action Allow

# Allow outbound HTTPS (auto-update, RMM tools)
New-NetFirewallRule -DisplayName "NAT - HTTPS" `
    -Direction Outbound -Protocol TCP `
    -RemotePort 443 -Action Allow
```

### On Target Computers (inbound rules)

Target computers being scanned/managed need these inbound rules:

```powershell
# Enable built-in WMI exception
Enable-NetFirewallRule -DisplayGroup "Windows Management Instrumentation (WMI)"

# Enable built-in Remote Administration exception (covers RPC, WMI, IPC$)
Enable-NetFirewallRule -DisplayGroup "Remote Administration"

# Enable file and printer sharing (for admin shares C$, ADMIN$)
Enable-NetFirewallRule -DisplayGroup "File and Printer Sharing"

# Enable WinRM
Enable-NetFirewallRule -DisplayGroup "Windows Remote Management"

# Enable RDP (if used)
Enable-NetFirewallRule -DisplayGroup "Remote Desktop"
```

**Deploy via GPO:**
- Computer Configuration → Windows Settings → Security Settings → Windows Firewall with Advanced Security → Inbound Rules
- Or use the "Windows Firewall: Define inbound program exceptions" GPO setting

---

## Windows Account & Privilege Requirements

### The Admin Account (used for credential passthrough)

NecessaryAdminTool authenticates with domain admin credentials via Windows Credential Manager and passes them to remote tools using `CreateProcessWithLogonW` with `LOGON_NETCREDENTIALS_ONLY`.

**Required permissions:**
| Permission | Where | Purpose |
|-----------|-------|---------|
| Domain Users | Active Directory | Base membership |
| Domain Admins (or delegated OU admin) | Active Directory | WMI, remote management |
| Remote Management Users | Target computers | WinRM access |
| Distributed COM Users | Target computers | WMI/DCOM access |
| Read access to `C$`, `ADMIN$` | Target computers | Admin share browsing |
| LDAP read permissions | Active Directory | Fleet inventory scan |

**Minimum delegation (non-Domain Admin):**
If you do not want to use a full Domain Admin account, delegate these specific permissions:
- Read all user/computer objects in target OUs (Active Directory Delegation)
- Remote Enable permission in WMI namespace (WMI Control → Security)
- Add to `Remote Management Users` group on target computers
- Add to `Performance Monitor Users` group (for performance data)

### The Running User Account (NecessaryAdminTool.exe process)

> **CRITICAL: Do NOT run NecessaryAdminTool.exe as Administrator (elevated/UAC).**

Running elevated breaks `CreateProcessWithLogonW` credential passthrough. The process must run as a standard domain user. Admin credentials are passed at the network layer only (`LOGON_NETCREDENTIALS_ONLY`).

| Condition | Result |
|-----------|--------|
| Running as standard user ✅ | Credentials pass through to MMC/tools correctly |
| Running as Administrator (elevated) ❌ | CreateProcessWithLogonW fails — child processes can't receive alternate network credentials |

**Credential storage:**
- Credentials are stored in Windows Credential Manager (`cmdkey` / CredMan API)
- Never stored in plaintext files or registry
- Encrypted by Windows DPAPI tied to the logged-on user's profile

---

## EDR & Antivirus Exception Configuration

NecessaryAdminTool.exe performs operations that behavioral EDR engines classify as suspicious:

| Behavior | Why flagged | Reality |
|---------|-------------|---------|
| `CreateProcessWithLogonW` with alternate creds | Credential injection / credential theft TTP | Legitimate admin tool launching MMC with domain credentials |
| Spawning `mmc.exe`, `cmd.exe`, `powershell.exe` | LOLBAS (Living Off the Land) | Standard admin console and scripting tools |
| WMI remote queries | WMI-based lateral movement TTP | Remote system scanning |
| Registry Run key writes | Persistence mechanism | Scheduled task / auto-start feature |
| Unsigned binary (development builds) | Unknown/untrusted executable | Development build without code signing |

---

### Palo Alto Cortex XDR (Detailed)

#### Agent Version Compatibility

| Cortex XDR Version | Exception Method |
|-------------------|-----------------|
| < 7.9 | Not supported (upgrade required) |
| 7.9 – 8.6 | **Disable Prevention Rules** (policy-level) ← Use this |
| 8.7+ | Disable Prevention Rules + Operational Agent Exceptions |

> ⚠️ Cortex XDR v8.3 (common enterprise version): Operational Agent Exceptions are greyed out in the console. Use Disable Prevention Rules instead — it works on v8.3 and covers the same behaviors.

#### Step 1: Create the Disable Prevention Rule

**Navigate to:** Security Management → Policy Rules → Prevention → Disable Prevention

1. Click **+ New Rule**
2. **General tab:**
   - Name: `NecessaryAdminTool`
   - Description: `IT admin tool - credential passthrough to MMC and remote management tools`
   - Status: **Enabled**

3. **Rule Conditions tab:**

   | Field | Value | Notes |
   |-------|-------|-------|
   | Platform | Windows | |
   | Files / Folders | `\*NecessaryAdminTool.exe` | Wildcard — works for all build paths |
   | Hash | *(leave blank)* | SHA256 changes on every Debug rebuild |
   | Command Line | *(leave blank)* | |
   | Signer Name | *(leave blank)* | Unsigned during development |
   | Module | Credential Gathering Protection | ✅ Primary — blocks CreateProcessWithLogonW |
   | Module | Behavioral Threat Protection | ✅ Secondary — blocks LOLBIN spawning |
   | Scope | Exception Profile → "IT Tools" | Do NOT use Global |

4. **Summary tab:** Check "I understand the risk" → Save

#### Step 2: Create the "IT Tools" Exception Profile

When setting Scope, create a new profile:
- Profile Name: `IT Tools`
- This profile can be reused for other internal tools

#### Step 3: Create and Assign a Policy

1. Navigate to **Endpoint Security → Policies**
2. Click **+ Create New Policy**
3. **General tab:**
   - Name: `JDX IT Tools` (or your org naming convention)
   - Description: `Policy for IT admin endpoints with tool exceptions`
4. **Target tab:** Add specific endpoints (by hostname or endpoint group)
5. **Windows section:**
   - Exploit: Default
   - Malware: Default
   - Restrictions: Default
   - Agent Settings: Default
   - **Exceptions: IT Tools** ← select your profile
6. Save and push

#### Step 4: Verify Policy Sync

After saving the policy:
1. Navigate to **Endpoints** → find your endpoint
2. Check **Policy Status** — should show "Applied" within 5–15 minutes
3. No restart required for policy-level exceptions

#### What Each Module Covers

| Module | What it protects | Why we disable it for NecessaryAdminTool |
|--------|-----------------|------------------------------------------|
| Credential Gathering Protection | Blocks processes using `CreateProcessWithLogonW`, LSASS access, token manipulation | Our app legitimately uses CreateProcessWithLogonW to pass domain admin credentials to MMC |
| Behavioral Threat Protection | Blocks unsigned processes spawning cmd.exe, powershell.exe, msbuild.exe, WMI operations | Our app legitimately launches admin shells and consoles |
| Kernel Privilege Escalation Protection | Blocks kernel exploits, driver abuse | ❌ NOT needed — our issue is user-space, not kernel |

#### Diagnostic: What Cortex XDR Logs Show

If still blocked after exception, check **Cortex XDR Alerts** for your endpoint. Common alert descriptions to look for:

| Alert Description | Module | Fix |
|------------------|--------|-----|
| "Credential Injection" or "Credential Gathering" | Credential Gathering Protection | Add Credential Gathering to exception |
| "Rare unsigned process execution" | Behavioral Threat Protection | Add Behavioral Threat to exception |
| "Process execution by unsigned initiator" | Behavioral Threat Protection | Add Behavioral Threat to exception |
| "Registry key modification by unsigned process" | Behavioral Threat Protection | Add Behavioral Threat to exception |
| "LOLBAS execution" (msbuild, cmd, powershell) | Behavioral Threat Protection | Add Behavioral Threat to exception |

#### Long-term: Code Signing

Obtaining a code signing certificate eliminates most Cortex XDR flags permanently:
- Provider: DigiCert, Sectigo, or GlobalSign (~$200–400/year)
- Type: Standard Code Signing (EV Code Signing is more trusted but costs more)
- Once signed: "Unsigned process" and "Unknown SHA256" alerts disappear
- Cortex and all other EDR solutions treat signed processes with significantly higher trust

---

### CrowdStrike Falcon

#### Create a Process Exclusion (Path-based)

1. **Falcon Console** → Prevention Policies → Windows → Machine Learning or Behavior settings
2. Navigate to **Prevention Hash Management** or **Exclusions → Process Exclusions**
3. Add: `*\NecessaryAdminTool.exe`
4. Apply to the **Policy Group** containing IT admin endpoints

#### IOA Exclusions (for behavioral alerts)

1. **Falcon Console** → Configuration → IOA Exclusions
2. Add exclusion:
   - Image Filename: `NecessaryAdminTool.exe`
   - Command Line: `.*` (wildcard)
   - Groups: IT admin endpoints

#### Specific detections to suppress

| CrowdStrike Detection | Tactic | Relevant? |
|----------------------|--------|-----------|
| `CreateProcessWithLogonW` credential injection | T1134.002 | ✅ Yes — suppress for this exe |
| LOLBAS execution chain | T1218 | ✅ Yes — cmd/powershell from unsigned |
| WMI lateral movement | T1047 | ✅ Yes — remote WMI scans |

---

### Microsoft Defender / Microsoft Defender for Endpoint (MDE)

#### Windows Defender Exclusion (local, per-machine)

```powershell
# Exclude the process from real-time protection
Add-MpPreference -ExclusionProcess "NecessaryAdminTool.exe"

# Or exclude by path
Add-MpPreference -ExclusionPath "C:\Users\brandon.necessary\source\repos\NecessaryAdminTool\NecessaryAdminTool\bin"
```

#### MDE (Defender for Endpoint) — Attack Surface Reduction exclusion

Via Intune or GPO:
```
Computer Configuration → Administrative Templates →
  Windows Components → Microsoft Defender Antivirus →
  Microsoft Defender Exploit Guard → Attack Surface Reduction →
  Exclude files and paths from Attack Surface Reduction Rules
```

Value: `C:\path\to\NecessaryAdminTool.exe`

#### MDE Indicator (allow list)

1. **Microsoft 365 Defender portal** (security.microsoft.com)
2. Settings → Endpoints → Indicators → File hashes
3. Add indicator:
   - Hash Type: SHA-256 (get from `Get-FileHash NecessaryAdminTool.exe`)
   - Action: Allow
   - Title: NecessaryAdminTool IT Tool

> Note: For development builds the SHA256 changes on every rebuild. Use path exclusions for dev builds; hash indicators for production/release builds.

---

### SentinelOne

#### Process Exclusion (path-based)

1. **SentinelOne Console** → Sentinels → Policies → Exclusions
2. Add Exclusion:
   - Type: Path
   - Value: `*\NecessaryAdminTool.exe`
   - Mode: Interoperability-Extended (allows all operations)
3. Assign to endpoint group containing IT admin machines

#### Blocklist / Alert suppression

1. Navigate to **Incidents → Threats**
2. Find alerts related to NecessaryAdminTool.exe
3. Select → Add to Exclusions → by Path

---

### Other EDR Solutions

#### ESET (Endpoint Security)

HIPS Exclusion:
1. ESET Remote Administrator → Policies → ESET Endpoint Security → HIPS → Rules
2. Add rule: Source Application = `NecessaryAdminTool.exe` → Action = Allow

#### Symantec Endpoint Protection / Broadcom

Application Control:
1. SEPM Console → Policies → Application and Device Control
2. Add exception for `NecessaryAdminTool.exe` with allowed operations:
   - Process creation with alternate credentials
   - WMI access

#### Trend Micro Apex One

Exception list:
1. Apex One Console → Policies → Behavior Monitoring
2. Exception List → Add: `NecessaryAdminTool.exe`
3. Program Type: Approved

#### Cylance / BlackBerry Protect

Safe List:
1. Cylance Console → Policy → Safe List
2. Add by file path: `*\NecessaryAdminTool.exe`
3. Category: Admin Tools

---

## Group Policy Requirements

### Restrict WMI Dynamic RPC Port Range (Optional — Reduces Firewall Exposure)

Instead of opening ports 49152–65535 wide, restrict Windows RPC to a smaller range:

1. **Group Policy Object:** Computer Configuration → Windows Settings → Security Settings → Windows Firewall → Advanced
2. Or via Registry:

```reg
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Rpc\Internet]
"Ports"="5000-5500"
"PortsInternetAvailable"="Y"
"UseInternetPorts"="Y"
```

Then open only TCP 5000–5500 in your firewall instead of 49152–65535.

### Enable Remote Registry (Required for Some Features)

```
Computer Configuration → Windows Settings → Security Settings →
  System Services → Remote Registry → Automatic
```

### WinRM via GPO

```
Computer Configuration → Administrative Templates →
  Windows Components → Windows Remote Management (WinRM) →
  WinRM Service → Allow remote server management through WinRM
  IPv4 filter: * (or specific subnet)
```

### WMI Permissions via GPO

Rather than adding accounts to local Administrators, delegate WMI:

1. Run `wmimgmt.msc` on target → WMI Control → Properties → Security
2. Navigate to `Root\CIMV2`
3. Add the admin account with: Execute Methods, Enable Account, Remote Enable, Read Security

Or deploy via GPO startup script.

---

## Common Symptoms & Troubleshooting

### MMC consoles fail to open (ADUC, GPMC, DNS, etc.)

| Symptom | Likely Cause | Fix |
|---------|-------------|-----|
| Error 740 in debug log | EDR blocking `CreateProcessWithLogonW` | Add Credential Gathering Protection exception |
| MMC opens but shows "Access Denied" | Running as Administrator (UAC elevated) | Run as normal domain user, NOT as admin |
| MMC opens but AD is empty | LDAP port 389 blocked | Open TCP 389 outbound to DCs |
| Credential prompt appears repeatedly | Kerberos ticket not cached | Authenticate via login dialog first |

### WMI scan returns no data

| Symptom | Likely Cause | Fix |
|---------|-------------|-----|
| All fields empty after scan | WMI service stopped on target | `Start-Service winmgmt` on target |
| Partial data (some tabs empty) | Dynamic RPC blocked (49152-65535) | Open dynamic RPC ports, or restrict range via GPO |
| "RPC server unavailable" | TCP 135 blocked | Open TCP 135 on target firewall |
| "Access denied" on scan | WMI permissions missing | Add admin account to WMI security namespace |

### RDP does not launch

| Symptom | Likely Cause | Fix |
|---------|-------------|-----|
| No response | TCP 3389 blocked | Open TCP 3389 on target and intermediate firewalls |
| "Remote Desktop Services not enabled" | RDP not enabled on target | `Enable-NetFirewallRule -DisplayGroup "Remote Desktop"` on target |

### Auto-update fails silently

| Symptom | Likely Cause | Fix |
|---------|-------------|-----|
| No updates found (when available) | TCP 443 to github.com blocked | Allow HTTPS to `api.github.com`, `github.com`, `objects.githubusercontent.com` |
| Update check times out | Proxy not configured | Set system proxy or disable auto-update via registry |

### Scheduled task scan does not run

| Symptom | Likely Cause | Fix |
|---------|-------------|-----|
| Task shows "Unsigned process execution" in Cortex | EDR blocking scheduled task launch | Behavioral Threat Protection exception covers this |
| Task runs but no results | Database path inaccessible from SYSTEM context | Verify database path is accessible by the task's run-as account |

---

## Port Summary Table

Complete reference — all ports NecessaryAdminTool may use:

| Port | Protocol | Direction | Service | Feature |
|------|----------|-----------|---------|---------|
| 53 | TCP+UDP | Out | DNS | All features requiring name resolution |
| 80 | TCP | Out | HTTP | TeamViewer fallback, some RMM tools |
| 88 | TCP+UDP | Out | Kerberos | AD authentication |
| 135 | TCP | Out | RPC/DCOM | WMI, MMC snap-ins |
| 137 | UDP | Out | NetBIOS NS | Legacy SMB |
| 138 | UDP | Out | NetBIOS DG | Legacy SMB |
| 139 | TCP | Out | NetBIOS SS | Legacy SMB |
| 389 | TCP+UDP | Out | LDAP | AD fleet scan, ADUC |
| 443 | TCP | Out | HTTPS | RMM tools, auto-update, GitHub |
| 445 | TCP | Out | SMB | Admin shares, AD SYSVOL |
| 464 | TCP+UDP | Out | Kerberos PW | Kerberos password operations |
| 636 | TCP | Out | LDAPS | Secure LDAP (optional) |
| 1433 | TCP | Out | SQL Server | SQL Server database provider |
| 1434 | UDP | Out | SQL Browser | SQL Server named instances |
| 3268 | TCP | Out | Global Catalog | Cross-domain AD queries |
| 3269 | TCP | Out | GC SSL | Secure Global Catalog (optional) |
| 3389 | TCP+UDP | Out | RDP | Remote Desktop quick launch |
| 5721 | TCP | Out | Kaseya | Kaseya VSA RMM |
| 5938 | TCP+UDP | Out | TeamViewer | TeamViewer RMM |
| 5985 | TCP | Out | WinRM HTTP | PowerShell remoting |
| 5986 | TCP | Out | WinRM HTTPS | Secure PowerShell remoting |
| 8040 | TCP | Out | ScreenConnect | ConnectWise Control relay |
| 10000 | TCP | Out | N-central | N-able N-central agent |
| 49152–65535 | TCP | Out | Dynamic RPC | WMI data, DCOM, MMC snap-ins |

---

*This document should be reviewed when:*
- *New RMM platforms are added to the tool*
- *Active Directory infrastructure changes (new DCs, new domains)*
- *EDR platform is upgraded or changed*
- *New EDR blocking behaviors are observed*

*For questions, check the debug log (DEBUG LOG button in app) or open an issue on GitHub.*
