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

- PowerShell 5.1 or higher
- Administrator privileges (ManageEngine runs as SYSTEM — this is satisfied automatically)
- Internet access for cloud-based paths
- 10 GB free disk space (General), 20 GB (Feature)

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
| `$LogDir` | Where log files are written | `C:\WINDOWS\TEMP\NecessaryAdminTool_Logs` |
| `$ISOPath` | Path to Windows ISO for feature updates | Skipped — uses cloud |
| `$HostnamePattern` | Which hostnames use the ISO path | `*` (all machines) |
| `$DatabaseType` | Set to `SqlServer` to write results to a database | Disabled |
| `$SqlConnectionString` | Full SQL Server connection string | — |

If `$LogDir` is a network share that is unreachable, the script automatically falls back to local temp logging without failing.

---

## GeneralUpdate.ps1

### What it does

1. Verifies PowerShell 5.1+ and admin privileges
2. Checks uptime — prompts reboot if over 30 days (with 1-postpone grace period)
3. Checks disk space (minimum 10 GB)
4. Logs OS version, uptime, and free disk space
5. Installs PSWindowsUpdate module if missing (NuGet + Scope AllUsers for SYSTEM compatibility)
6. Creates a system restore point
7. Scans for all missing Microsoft updates
8. Logs every detected update: name, KB number, severity, size
9. Installs all updates silently
10. Reports exit code 0 (success) or 1 (failure) to ManageEngine

### Log output example

```
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

1. Verifies admin privileges
2. Falls back to local temp logging if log share unavailable
3. **Power check** — exits 1 if running on battery below 40% and not on AC power
4. **Pending reboot check** — exits 1 if a reboot is already pending (WU, CBS, or PendingFileRenameOps)
5. **Hardware compatibility** — TPM **2.0** (not just present), Secure Boot, 4 GB RAM, 20 GB free disk
   - If disk is low (10–20 GB): attempts WU cache + temp + DISM cleanup before failing
6. **Already-compliant check** — exits 0 with `COMPLIANT` status if machine is already on Windows 11 25H2 (Build 26200+)
7. Logs OS version for before/after comparison
8. **User warning** (attended machines only):
   - Detects if an interactive user is logged in (`explorer.exe` session check)
   - **Unattended**: skips dialog and countdown, upgrades immediately
   - **Attended**: fires `msg.exe` cross-session alert, then shows timed WinForms dialog listing any open Office/browser/Teams apps
   - Up to 2 postpones; dialog auto-proceeds after 20 minutes if no response
   - Boot-persistent: registers a startup task so a reboot during the countdown re-runs the upgrade automatically
9. **VPN check** — if any VPN is active, skips ISO and goes directly to cloud
10. **Chooses upgrade method:**
    - **ISO path** — if `$ISOPath` is set, file exists, and hostname matches `$HostnamePattern`
    - **Cloud path** — Windows 11 Installation Assistant (always gets latest version from Microsoft)
      - 3 download attempts with 30-second backoff between each
11. Reports progress every 60 seconds to ME execution log
12. Translates exit codes to human-readable descriptions in the ME log (HRESULT decoder)
13. Reports exit code 0 (success) or 1 (failure)

### Upgrade method decision tree

```
VPN detected?
  YES → Cloud (Installation Assistant)
  NO  → ISO path configured + hostname matches + file exists?
          YES → ISO (setup.exe /auto upgrade)
                 ISO fails mid-run? → Cloud fallback
          NO  → Cloud (Installation Assistant)
```

### Log output example — attended machine, cloud path

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
[2026-02-19 10:20:02] UPGRADE_COUNTDOWN_20MIN_REMAINING
...
[2026-02-19 10:40:02] UPGRADE_COUNTDOWN_COMPLETE_PROCEEDING
[2026-02-19 10:40:03] VPN_DETECTED_SKIPPING_ISO  (or skipped if no VPN)
[2026-02-19 10:40:03] METHOD_CLOUD_START
[2026-02-19 10:40:03] CLOUD_DOWNLOAD_ATTEMPT_1_OF_3
[2026-02-19 10:40:21] ASSISTANT_DOWNLOADED_ATTEMPT_1
[2026-02-19 10:40:21] ASSISTANT_RUNNING
[2026-02-19 10:41:21] PROGRESS_1min - Initializing...
[2026-02-19 10:46:21] PROGRESS_6min - Downloading / Preparing...
[2026-02-19 10:56:21] PROGRESS_16min - Upgrading (18% complete)
[2026-02-19 11:04:33] CLOUD_COMPLETE_CODE_0_0x00000000 — Success — reboot pending
```

### Log output example — unattended machine (no user)

```
[2026-02-19 02:00:01] POWER_CHECK_PASSED
[2026-02-19 02:00:01] PENDING_REBOOT_CHECK_PASSED
[2026-02-19 02:00:01] HW_COMPAT_CHECK_PASSED_TPM2_SECUREBOOT_RAM_16.0GB_DISK_87.3GB
[2026-02-19 02:00:01] UPGRADE_REQUIRED_Windows 10 -> Windows 11 25H2 upgrade (current Build 19045)
[2026-02-19 02:00:02] USER_SESSION_PRESENT_False
[2026-02-19 02:00:02] UNATTENDED_MODE_SKIPPING_DIALOG_AND_COUNTDOWN
[2026-02-19 02:00:03] METHOD_CLOUD_START
...
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

### Progress phases (visible in ME execution log)

| Phase shown | What is happening |
|-------------|------------------|
| `Initializing...` | Assistant just launched |
| `Downloading / Preparing...` | Downloading upgrade files |
| `Upgrading (XX% complete)` | Windows Setup actively upgrading |

### Task Manager indicators (for manual checks)

| Process visible | Stage |
|----------------|-------|
| `Windows11InstallationAssistant.exe` | Downloading |
| `DismHost.exe` (high disk) | Preparing image |
| `Modern Setup Host` | Upgrade executing |
| Machine reboots | Final install stages (1-3 reboots) |

### Exit codes

| Code | Meaning |
|------|---------|
| `0` | Upgrade completed — reboot pending |
| `1` | Failed — hardware incompatible, download error, setup error, or timeout |

---

## Deploying via ManageEngine

1. Open the script in a text editor
2. Set `$LogDir`, `$ISOPath`, `$HostnamePattern` at the top of the file
3. Upload to ManageEngine → Scripts
4. Create a task targeting your device group
5. Set script timeout to at least **3 hours** for FeatureUpdate, **1 hour** for GeneralUpdate
6. Run — monitor progress in the ME script execution output window

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
| `Status` | NVARCHAR(100) | `COMPLIANT`, `SUCCESS`, `FAILED`, `HW_INCOMPATIBLE` |
| `UpdatesFound` | NVARCHAR(500) | (GeneralUpdate) Number of updates found |
| `Method` | NVARCHAR(200) | (FeatureUpdate) `ISO`, `Cloud-VPN`, `Cloud-NoISO`, etc. |
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

### Per-machine log (most detail)
```
$LogDir\Individual_PC_Logs\HOSTNAME_General.txt
$LogDir\Individual_PC_Logs\HOSTNAME_Feature.txt
```

### Fleet summary CSV
```
$LogDir\Master_Update_Log.csv
```
Format: `HOSTNAME, STATUS, TIMESTAMP`

### Fallback (when log share unavailable)
```
C:\WINDOWS\TEMP\NecessaryAdminTool_Logs\Individual_PC_Logs\
```

---

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| `INFO: Using local fallback logging` | `$LogDir` not set or share unreachable | Set `$LogDir` at top of script |
| `FAILED_HW_COMPATIBILITY_No TPM` | Machine doesn't meet Win11 requirements | Check TPM in BIOS, enable if present |
| `FAILED_HW_COMPATIBILITY_Secure Boot disabled` | Secure Boot off in BIOS | Enable Secure Boot |
| `ERROR_MODULE_INSTALL_FAILED` | No internet / PowerShell Gallery blocked | Check firewall allows `*.powershellgallery.com` |
| `CLOUD_DOWNLOAD_FAILED` | No internet access | Check connectivity from SYSTEM context |
| `CLOUD_COMPLETE_CODE_<n>` (non-zero) | Installation Assistant error | See Microsoft docs for exit code |
| Script shows `COMPLIANT` but machine needs updates | Updates already installed | Machine was up to date — no action needed |
| ME shows script as timed out | ME timeout too short | Set ME script timeout to 3+ hours for FeatureUpdate |
| `FAILED_POWER_CHECK` in log | Laptop on battery below 40% | Plug in AC power, then re-push the task |
| `FAILED_PENDING_REBOOT` in log | Machine has a pending reboot from WU or CBS | Reboot the machine, then re-push the task |
| `TPM_VERSION_BELOW_2` in log | Machine has TPM 1.2 instead of 2.0 | Enable TPM 2.0 in BIOS or replace hardware |
| `HW_INCOMPATIBLE` — RAM reason | Machine has < 4 GB RAM | Add RAM to meet Win11 minimum requirement |
| `DISK_CLEANUP_*` entries in log | Disk was low (10–20 GB) — cleanup was attempted | Check `DISK_CLEANUP_COMPLETE_FREE_XXX` to see freed space; may need manual cleanup if still short |
| `UNATTENDED_MODE_SKIPPING_DIALOG_AND_COUNTDOWN` | No interactive user logged in | Expected for overnight pushes — upgrade runs immediately without delay |
| `MSG_EXE_FAILED` in log | `msg.exe` unavailable (some editions) | Non-fatal — WinForms dialog is still shown as the primary notification |
| Upgrade prompt not visible to user | Session 0 isolation (SYSTEM context) | Expected on some systems — the upgrade still proceeds after the 20-minute countdown |
| Upgrade keeps running every boot | Boot task not cleaned up (upgrade may have failed mid-way) | Delete task `NecessaryAdminTool_FeatureUpgrade_Pending` in Task Scheduler and delete `C:\Windows\Temp\NecessaryAdminTool_Feature_Pending.txt` |
| User postponed but upgrade never re-ran | ME task was one-time, not recurring | Re-push the task in ME, or configure it as a recurring task until success |
| `CLOUD_DOWNLOAD_FAILED_ALL_3_ATTEMPTS` | No internet / firewall blocking download | Check connectivity from SYSTEM context: `Test-NetConnection go.microsoft.com -Port 443` |
| `CLOUD_COMPLETE_CODE_0xC1900208` | Incompatible app blocking upgrade | Uninstall the blocking app (check CBS.log for name), then re-push |
| `CLOUD_COMPLETE_CODE_0xC1900101` | Driver compatibility error | Run SetupDiag to identify the driver, update or remove it |
| `DB_WRITE_FAILED` in log | SQL Server unreachable or permission denied | Check firewall, verify SYSTEM has INSERT permission on `UpdateHistory` |
| No rows appearing in `UpdateHistory` | `$DatabaseType` not set to `SqlServer` | Use NecessaryAdminTool Download Scripts button (injects settings automatically) |
