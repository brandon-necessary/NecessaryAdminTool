# NecessaryAdminTool - Deployment Scripts

Two PowerShell scripts for deploying Windows updates across your fleet via ManageEngine or any RMM platform.

---

## Scripts

| Script | Purpose |
|--------|---------|
| `GeneralUpdate.ps1` | Cumulative updates, security patches, firmware |
| `FeatureUpdate.ps1` | Major OS version upgrades (Win10 → Win11, 22H2 → 24H2) |

---

## Requirements

- PowerShell 5.1 or higher (enforced via `#Requires` — script exits cleanly before any work if not met)
- Administrator privileges (enforced via `#Requires` — ManageEngine runs as SYSTEM, satisfying this automatically)
- Internet access to `*.powershellgallery.com` for PSWindowsUpdate module installation
- Internet access to Windows Update servers for the cloud upgrade path
- 10 GB free disk space (GeneralUpdate), 20 GB (FeatureUpdate)

---

## Configuration

### Recommended: Use NecessaryAdminTool to download pre-configured scripts

In NecessaryAdminTool, go to **Deployment Center → Download Scripts**. The app injects your configured paths (log directory, ISO path, hostname pattern, and SQL Server connection string if configured) automatically before saving the files. Upload those files to ManageEngine — no manual editing required.

### Manual: Hardcode at the top of each script

If editing manually, set the variables at the top of each script before deploying:

```powershell
# FeatureUpdate.ps1 / GeneralUpdate.ps1 — top of file
$ISOPath             = "\\yourserver\share\Win11.iso"   # FeatureUpdate only
$LogDir              = "\\yourserver\share\NAT_Logs"
$HostnamePattern     = "TN*"                             # FeatureUpdate only
$DatabaseType        = "SqlServer"                       # Optional — write results to DB
$SqlConnectionString = "Server=SQLSRV;Database=NAT;Integrated Security=True;"  # Optional
```

| Variable | Purpose | Default if blank |
|----------|---------|-----------------|
| `$LogDir` | Where log files are written | `%TEMP%\NecessaryAdminTool_Logs` |
| `$ISOPath` | Path to Windows ISO for feature updates | Skipped — uses Windows Update |
| `$HostnamePattern` | Which hostnames use the ISO path | `*` (all machines) |
| `$DatabaseType` | Set to `SqlServer` to write results to a database | Disabled |
| `$SqlConnectionString` | Full SQL Server connection string | — |

If `$LogDir` is a network share that is unreachable, the script automatically falls back to local temp logging without failing.

---

## GeneralUpdate.ps1

### What it does

1. Enforces PowerShell 5.1+ and admin privileges (`#Requires` — clean exit before any work if not met)
2. Starts a transcript alongside the structured log (`HOSTNAME_General_Transcript.txt`) for belt-and-suspenders capture
3. Checks uptime — prompts reboot if over 30 days (with 1-postpone grace period)
4. Checks disk space (minimum 10 GB)
5. Logs OS version, uptime, and free disk space
6. Installs PSWindowsUpdate module automatically if missing (NuGet + `-Scope AllUsers` for SYSTEM compatibility)
7. Creates a system restore point
8. Scans for all missing Microsoft updates
9. Logs every detected update: name, KB number, severity, size
10. Installs all updates silently
11. Reports exit code 0 (success) or 1 (failure) to ManageEngine

### Log output example

```
[2026-02-19 09:00:01] SCRIPT_START_Host=DESKTOP01_OS=Microsoft Windows 11 Pro_Uptime=4.2days
[2026-02-19 09:00:01] OS_VERSION_Microsoft Windows 11 Pro
[2026-02-19 09:00:01] UPTIME_4.2_DAYS
[2026-02-19 09:00:01] DISK_FREE_87.3GB
[2026-02-19 09:00:05] MODULE_LOADED_SUCCESSFULLY
[2026-02-19 09:00:08] RESTORE_POINT_CREATED
[2026-02-19 09:00:45] UPDATES_FOUND_3
[2026-02-19 09:00:45] DETECTED: [Critical] 2026-02 Cumulative Update for Windows 11 (KB5034765) - 512.4MB
[2026-02-19 09:00:45] DETECTED: [Important] 2026-02 .NET Framework Update (KB5034123) - 48.2MB
[2026-02-19 09:00:45] DETECTED: [Unspecified] Dell Firmware Update 1.12.0 - 24.1MB
[2026-02-19 09:00:45] INSTALLING_3_UPDATES
[2026-02-19 09:14:22] SUCCESS_PENDING_REBOOT
[2026-02-19 09:14:22] SCRIPT_COMPLETED_SUCCESS
```

### Exit codes

| Code | Meaning |
|------|---------|
| `0` | All updates installed successfully |
| `1` | Failed — disk space, module install, or update installation error |

---

## FeatureUpdate.ps1

### What it does

1. Enforces PowerShell 5.1+ and admin privileges (`#Requires` — clean exit before any work if not met)
2. Starts a transcript alongside the structured log (`HOSTNAME_Feature_Transcript.txt`)
3. Falls back to local temp logging if log share unavailable
4. **Power check** — exits 1 if running on battery below 40% and not on AC power
5. **Pending reboot check** — exits 1 if a reboot is already pending (WU, CBS, or PendingFileRenameOps)
6. **Hardware compatibility** — TPM **2.0** (not just present), Secure Boot, 64-bit OS, 4 GB RAM, 20 GB free disk
   - If disk is low (10–20 GB): attempts WU cache + Delivery Optimization cache + temp + DISM cleanup before failing
7. **Already-compliant check** — exits 0 with `COMPLIANT` status if machine is already on Windows 11 25H2 (Build 26200+)
8. Logs OS version for before/after comparison
9. **User warning** (attended machines only):
   - Detects if an interactive user is logged in (`explorer.exe` session check)
   - **Unattended**: skips dialog and countdown, upgrades immediately
   - **Attended**: fires `msg.exe` cross-session alert, then shows timed WinForms dialog listing any open Office/browser/Teams apps
   - Up to 2 postpones; dialog auto-proceeds after 20 minutes if no response
   - Boot-persistent: registers a startup task so a reboot during the countdown re-runs the upgrade automatically
10. **VPN check** — if VPN is active and the ISO path is a UNC network share, skips to Windows Update (share is unreachable over VPN); local ISO paths are unaffected by VPN
11. **Chooses upgrade method:**
    - **ISO path** — if `$ISOPath` is set, file exists, and hostname matches `$HostnamePattern`; uses `setup.exe` with enterprise-hardened flags
    - **Windows Update** — uses PSWindowsUpdate (`-Category "Upgrades"`); works through proper WU channels under SYSTEM; installs module automatically if missing
12. **ISO-with-WU-fallback**: if ISO setup exits with an error code, Windows Update is attempted before the script gives up
13. Reports progress every 60 seconds to ME execution log during ISO setup
14. Translates `setup.exe` exit codes to human-readable HRESULT descriptions in the log
15. Reports exit code 0 (success) or 1 (failure)

### Upgrade method decision tree

```
VPN active AND ISO path is a UNC share (\\server\...)?
  YES → Windows Update (PSWindowsUpdate -Category "Upgrades")

  NO  → Hostname matches pattern + ISO accessible?
          YES → ISO  (setup.exe /auto upgrade /quiet /compat ignorewarning ...)
                  setup.exe success (0 / 3010)?  → exit 0
                  setup.exe error code?          → Windows Update fallback
                  setup.exe timeout (2 h)?       → exit 1  (state uncertain — no WU fallback)
                  Mount / launch exception?      → Windows Update fallback

          NO  → Windows Update (PSWindowsUpdate -Category "Upgrades")
```

**Windows Update path behaviour:**
- If the feature upgrade is **not currently offered** to this machine (common when WSUS/WUfB deferral is active or Win11 targeting is not approved), the script exits 1 immediately with a clear message and three remediation options.
- If the upgrade **is offered**, PSWindowsUpdate downloads and stages it; the machine reboots on its own schedule and finishes installation.

### Log output example — attended machine, ISO path

```
[2026-02-19 10:00:01] POWER_CHECK_PASSED
[2026-02-19 10:00:01] PENDING_REBOOT_CHECK_PASSED
[2026-02-19 10:00:01] HW_COMPAT_CHECK_PASSED_TPM2_SECUREBOOT_RAM_16.0GB_DISK_87.3GB
[2026-02-19 10:00:01] OS_VERSION_Microsoft Windows 10 Pro_BUILD_19045_TARGET_25H2
[2026-02-19 10:00:01] UPGRADE_REQUIRED_Windows 10 -> Windows 11 25H2 upgrade (current Build 19045)
[2026-02-19 10:00:02] USER_SESSION_PRESENT_True
[2026-02-19 10:00:02] MSG_EXE_SENT_TO_ALL_SESSIONS
[2026-02-19 10:00:02] WARNING_DIALOG_SHOWN_20MIN_POSTPONES_LEFT_2
[2026-02-19 10:20:02] WARNING_DIALOG_RESULT_Proceed
[2026-02-19 10:20:02] BOOT_TASK_REGISTERED_UPGRADE_RESUMES_ON_RESTART
[2026-02-19 10:20:02] UPGRADE_COUNTDOWN_STARTED_20MIN
...
[2026-02-19 10:40:02] UPGRADE_COUNTDOWN_COMPLETE_PROCEEDING
[2026-02-19 10:40:03] VPN_NOT_DETECTED_EVALUATING_ISO_PATH
[2026-02-19 10:40:03] METHOD_ISO_START_Host=DESKTOP01_Pattern=TN*_ISO=\\srv\share\Win11.iso_Size=5.2GB
[2026-02-19 10:41:03] ISO_PROGRESS_1min_Initializing...
[2026-02-19 10:47:03] ISO_PROGRESS_7min_Downloading / Preparing...
[2026-02-19 10:57:03] ISO_PROGRESS_17min_Upgrading (18% complete)
[2026-02-19 11:05:44] ISO_COMPLETE_CODE_0_0x00000000 — Success — reboot pending
[2026-02-19 11:05:44] ISO_DISMOUNTED
```

### Log output example — unattended machine, Windows Update path

```
[2026-02-19 02:00:01] POWER_CHECK_PASSED
[2026-02-19 02:00:01] PENDING_REBOOT_CHECK_PASSED
[2026-02-19 02:00:01] HW_COMPAT_CHECK_PASSED_TPM2_SECUREBOOT_RAM_16.0GB_DISK_87.3GB
[2026-02-19 02:00:01] UPGRADE_REQUIRED_Windows 10 -> Windows 11 25H2 upgrade (current Build 19045)
[2026-02-19 02:00:02] USER_SESSION_PRESENT_False
[2026-02-19 02:00:02] UNATTENDED_MODE_SKIPPING_DIALOG_AND_COUNTDOWN
[2026-02-19 02:00:03] ISO_PATH_NOT_CONFIGURED_USING_CLOUD
[2026-02-19 02:00:03] METHOD_CLOUD_START
[2026-02-19 02:00:04] CLOUD_WU_SCAN_STARTED
[2026-02-19 02:00:14] CLOUD_WU_UPGRADE_FOUND_Windows 11, version 25H2 Feature Update
[2026-02-19 02:00:14] CLOUD_WU_INSTALL_STARTED
... (PSWindowsUpdate outputs download and install progress directly to ME execution log)
[2026-02-19 02:58:31] CLOUD_WU_COMPLETE_REBOOT_PENDING
```

### Log output example — hardware failure

```
[2026-02-19 10:00:01] FAILED_HW_COMPATIBILITY_TPM 2.0 required (found: 1.2)|Secure Boot disabled
[2026-02-19 10:00:01] (script exits 1)
```

### Log output example — already compliant

```
[2026-02-19 10:00:01] HW_COMPAT_CHECK_PASSED_TPM2_SECUREBOOT_RAM_16.0GB_DISK_87.3GB
[2026-02-19 10:00:01] OS_VERSION_Microsoft Windows 11 Pro_BUILD_26200_TARGET_25H2
[2026-02-19 10:00:01] ALREADY_COMPLIANT_WIN11_25H2_BUILD_26200
[2026-02-19 10:00:01] (script exits 0, Status=COMPLIANT)
```

### Log output example — WU path, no upgrade offered (WSUS/WUfB blocking)

```
[2026-02-19 10:00:03] METHOD_CLOUD_START
[2026-02-19 10:00:04] CLOUD_WU_SCAN_STARTED
[2026-02-19 10:00:14] CLOUD_WU_NO_UPGRADE_OFFERED
[2026-02-19 10:00:14] (script exits 1 — see log for three remediation options)
```

### ISO setup progress phases (visible in ME execution log)

| Phase shown | What is happening |
|-------------|------------------|
| `Initializing...` | setup.exe just launched |
| `Downloading / Preparing...` | Windows Setup staging files |
| `Upgrading (XX% complete)` | Windows Setup actively upgrading |

### Task Manager indicators during ISO upgrade (for manual checks)

| Process visible | Stage |
|----------------|-------|
| `setup.exe` (high disk I/O) | Copying / preparing upgrade files |
| `DismHost.exe` (high disk) | Applying image |
| `Modern Setup Host` | Upgrade executing |
| Machine reboots | Final install stages (1–3 reboots) |

### Exit codes

| Code | Meaning |
|------|---------|
| `0` | Upgrade completed — reboot pending (or already compliant — no upgrade needed) |
| `1` | Failed — hardware incompatible, 32-bit OS, setup error, WU not offering upgrade, or timeout |

---

## Deploying via ManageEngine

1. In NecessaryAdminTool, click **Deployment Center → Download Scripts** — settings are injected automatically
2. Upload both scripts to ManageEngine → Scripts
3. Create a task targeting your device group
4. Set script timeout to at least **3 hours** for FeatureUpdate, **1 hour** for GeneralUpdate
5. Run — monitor progress in the ME script execution output window

If configuring manually: set `$LogDir`, `$ISOPath`, and `$HostnamePattern` at the top of FeatureUpdate.ps1 before uploading.

---

## SQL Server database write (optional)

When `$DatabaseType = "SqlServer"` and `$SqlConnectionString` is set, both scripts write their results directly to a SQL Server table called **`UpdateHistory`**.

The table is created automatically if it does not exist. The schema is shared by both scripts:

| Column | Type | Description |
|--------|------|-------------|
| `Id` | INT IDENTITY | Auto-increment primary key |
| `Hostname` | NVARCHAR(255) | Computer name |
| `Script` | NVARCHAR(50) | `General` or `Feature` |
| `Timestamp` | DATETIME | When the script finished |
| `OSVersion` | NVARCHAR(500) | Windows edition (e.g. "Microsoft Windows 11 Pro") |
| `UptimeDays` | DECIMAL(10,2) | Days since last reboot |
| `DiskFreeGB` | DECIMAL(10,2) | C: drive free space at script start |
| `Status` | NVARCHAR(100) | `COMPLIANT`, `SUCCESS`, `FAILED`, `HW_INCOMPATIBLE`, `POSTPONED` |
| `UpdatesFound` | NVARCHAR(500) | (GeneralUpdate) Number of updates found |
| `Method` | NVARCHAR(200) | (FeatureUpdate) `ISO`, `Cloud-VPN`, `Cloud-NoISO`, `Cloud-ISOFailed`, etc. |
| `Details` | NVARCHAR(MAX) | KB list (General) or exit code / error (Feature) |
| `DurationSeconds` | INT | Total script run time in seconds |

**Connection string examples:**

```powershell
# Windows Authentication (recommended for domain-joined machines):
$SqlConnectionString = "Server=SQLSRV01;Database=NecessaryAdminTool;Integrated Security=True;"

# SQL Server login:
$SqlConnectionString = "Server=SQLSRV01;Database=NecessaryAdminTool;User Id=nat_writer;Password=secret;"
```

**Service account requirements:** The SYSTEM account (ManageEngine) needs INSERT permission on the `UpdateHistory` table, and CREATE TABLE permission if the table doesn't exist yet. Alternatively, pre-create the table and grant only INSERT.

**Query example — recent fleet status:**
```sql
SELECT Hostname, Script, Status, UpdatesFound, Method, Details, Timestamp
FROM UpdateHistory
WHERE Timestamp >= DATEADD(DAY, -7, GETDATE())
ORDER BY Timestamp DESC;
```

---

## Log file locations

### Per-machine structured log (most detail)
```
$LogDir\Individual_PC_Logs\HOSTNAME_General.txt
$LogDir\Individual_PC_Logs\HOSTNAME_Feature.txt
```

### Per-machine transcript (belt-and-suspenders — all console output)
```
$LogDir\Individual_PC_Logs\HOSTNAME_General_Transcript.txt
$LogDir\Individual_PC_Logs\HOSTNAME_Feature_Transcript.txt
```

### Fleet summary CSV
```
$LogDir\Master_Update_Log.csv
```
Rich columns: `Hostname, Script, Timestamp, OSVersion, UptimeDays, DiskFreeGB, Status, Method, Details, DurationSeconds`

### Fallback (when log share unavailable)
```
%TEMP%\NecessaryAdminTool_Logs\Individual_PC_Logs\
```

---

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| `INFO: Using local fallback logging` | `$LogDir` not set or share unreachable | Set `$LogDir` at top of script, or use Download Scripts button |
| `FAILED_HW_COMPATIBILITY_TPM 2.0 required` | Machine has TPM 1.2 or none | Enable TPM 2.0 in BIOS, or replace hardware |
| `FAILED_HW_COMPATIBILITY_Secure Boot disabled` | Secure Boot off in BIOS | Enable Secure Boot |
| `FAILED_HW_COMPATIBILITY_32-bit OS` | Machine is running 32-bit Windows | Windows 11 is 64-bit only — hardware replacement required |
| `HW_INCOMPATIBLE` — RAM reason | Machine has < 4 GB RAM | Add RAM to meet Win11 minimum requirement |
| `CLOUD_MODULE_INSTALL_FAILED` | No internet / PowerShell Gallery blocked | Allow outbound HTTPS to `*.powershellgallery.com` from SYSTEM context |
| `CLOUD_WU_NO_UPGRADE_OFFERED` | Feature update not currently offered via WU | (1) Configure ISO path, (2) approve Win11 targeting in WUfB/WSUS, or (3) remove WU deferral policies |
| `CLOUD_WU_FAILED` | PSWindowsUpdate encountered an error | Check network connectivity from SYSTEM; check WU service is running |
| `ISO_COMPLETE_CODE_0xC1900208` | Incompatible app blocking upgrade | Uninstall the blocking app (check `%TEMP%\$WINDOWS.~BT\Sources\Panther\` or run SetupDiag), then re-push |
| `ISO_COMPLETE_CODE_0xC1900101` | Driver compatibility error | Run SetupDiag to identify the driver, update or remove it, then re-push |
| `ISO_COMPLETE_CODE_0xC1900107` | Cleanup pending from a previous attempt | Reboot the machine, then re-push |
| `ISO_NOT_ACCESSIBLE` in log | ISO file path configured but unreachable | Verify UNC share is online and SYSTEM has read access |
| `VPN_DETECTED_NETWORK_ISO_UNREACHABLE` | VPN active, ISO is a UNC share | Expected — script uses Windows Update path; or switch to a local ISO path |
| `TIMEOUT_ISO_SETUP_KILLED` | setup.exe ran for 2 hours without completing | Check Panther logs at `%TEMP%\$WINDOWS.~BT\Sources\Panther\`; reboot and re-push |
| Script shows `COMPLIANT` but machine needs updates | Machine is already on target build | No action needed — machine is up to date |
| ME shows script as timed out | ME timeout too short | Set ME script timeout to 3+ hours for FeatureUpdate, 1+ hour for GeneralUpdate |
| `FAILED_POWER_CHECK` | Laptop on battery below 40% | Plug in AC power, then re-push the task |
| `FAILED_PENDING_REBOOT` | Machine has a pending reboot | Reboot the machine, then re-push the task |
| `DISK_CLEANUP_*` entries in log | Disk was low (10–20 GB) — cleanup was attempted | Check `DISK_CLEANUP_COMPLETE_FREE_XXX` to see freed space; may need manual cleanup if still short |
| `UNATTENDED_MODE_SKIPPING_DIALOG_AND_COUNTDOWN` | No interactive user logged in | Expected for overnight pushes — upgrade runs immediately without delay |
| `MSG_EXE_FAILED` in log | `msg.exe` unavailable (some editions) | Non-fatal — WinForms dialog is the primary notification |
| Upgrade prompt not visible to user | Session 0 isolation (SYSTEM context) | Expected — the upgrade still proceeds after the 20-minute countdown |
| Upgrade keeps running every boot | Boot task not cleaned up (upgrade failed mid-way) | Delete task `NecessaryAdminTool_FeatureUpgrade_Pending` in Task Scheduler and delete `C:\Windows\Temp\NecessaryAdminTool_Feature_Pending.txt` |
| User postponed but upgrade never re-ran | ME task was one-time, not recurring | Re-push the task in ME, or configure it as recurring until `Status=SUCCESS` |
| `DB_WRITE_FAILED` in log | SQL Server unreachable or permission denied | Check firewall; verify SYSTEM has INSERT permission on `UpdateHistory` |
| No rows appearing in `UpdateHistory` | `$DatabaseType` not set to `SqlServer` | Use NecessaryAdminTool Download Scripts button (injects settings automatically) |
