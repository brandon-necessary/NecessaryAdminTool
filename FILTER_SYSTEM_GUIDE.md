# Advanced Filtering System - User Guide

**TAG: #AUTO_UPDATE_UI_ENGINE #FILTER_SYSTEM #DOCUMENTATION**

## Overview

The Advanced Filtering System provides powerful multi-criteria filtering capabilities for the Fleet Inventory, allowing you to quickly find specific computers based on multiple attributes.

## Features

### 🔍 **Multi-Criteria Filtering**
Filter computers by:
- **Computer Name Pattern** - Wildcards supported (* = any characters, ? = single character)
- **Status** - Online or Offline
- **Operating System** - Windows 7, 10, 11, Server, etc.
- **Organizational Unit (OU)** - LDAP path filtering
- **RAM (Memory)** - Min/max range in GB
- **Last Seen Date** - Date range filtering

### 💾 **Filter Presets**
- Save frequently used filters as reusable presets
- Load saved presets with one click
- Built-in presets included (Online, Offline, Win11, Win7, Servers, etc.)
- Export/import presets for sharing across teams

### ⚡ **Quick Filters**
One-click filter buttons:
- 📋 **All** - Clear all filters
- ✅ **Online** - Show only computers responding to ping
- ❌ **Offline** - Show only non-responsive computers
- 🪟 **Win11** - Windows 11 only
- 🔟 **Win10** - Windows 10 only
- ⚠️ **Win7** - Windows 7 (end-of-life warning)
- 🖥️ **Servers** - Windows Server OS only
- 💻 **Workstations** - Non-server OS only

### 🕐 **Filter History**
- Automatically tracks last 10 filter operations
- View number of results for each filter
- Re-apply previous filters quickly

### 🎯 **AND/OR Logic**
- **AND Logic** (default) - All criteria must match
- **OR Logic** - Any criteria can match
- Toggle logic operator per filter

## Quick Start

### Basic Filtering

1. **Click a Quick Filter Button**
   - Click "Online" to see only online computers
   - Click "Win11" to see only Windows 11 systems
   - Click "All" to clear filters

2. **View Filter Status**
   - Status bar shows active filter description
   - Result count displays matched computers
   - Filter icon indicates active filtering (🔍) or all computers (ℹ️)

### Saving Filter Presets

1. **Apply Your Filters**
   - Use quick filters or manual criteria
   - Verify results match your needs

2. **Click "Save Preset" (💾)**
   - Enter a descriptive name (max 100 characters)
   - Add optional description (max 500 characters)
   - Review filter criteria summary
   - Click "Save"

3. **Manage Presets**
   - View existing presets in the save dialog
   - Built-in presets (🔒) cannot be deleted
   - Custom presets (📌) can be edited/deleted

### Loading Filter Presets

1. **Click "Load Preset" (📂)**
2. **Select from List**
   - Built-in presets: 🔒 icon
   - Custom presets: 📌 icon
3. **Click "Load"**
   - Filter applies immediately
   - Results update in grid/card view

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Ctrl+Shift+F` | Open Advanced Filters (coming soon) |
| `Ctrl+Shift+S` | Save current filter as preset |
| `Esc` | Clear all filters |
| `Ctrl+K` | Open Command Palette (filter commands available) |

## Command Palette Integration

Press `Ctrl+K` and type:
- `filter online` - Apply online filter
- `filter offline` - Apply offline filter
- `filter win11` - Filter Windows 11
- `filter win10` - Filter Windows 10
- `filter win7` - Filter Windows 7
- `filter servers` - Filter servers
- `filter workstations` - Filter workstations
- `filter save` - Save current filter preset
- `filter load` - Load saved preset
- `filter clear` - Clear all filters

## Advanced Examples

### Example 1: Find High-Memory Windows 11 Workstations
**Criteria:**
- OS Filter: "Windows 11"
- Min RAM: 16 GB
- Logic: AND

**Result:** All Windows 11 computers with 16GB+ RAM

### Example 2: Find End-of-Life Systems
**Preset:** "Windows 7 (EOL)"
- OS Filter: "Windows 7"
- Shows systems requiring upgrade

### Example 3: Offline Servers Needing Attention
**Criteria:**
- Status: Offline
- OS Filter: "Server"
- Logic: AND

**Result:** All offline Windows Server systems

### Example 4: Recently Active Computers
**Criteria:**
- Last Seen After: [7 days ago]
- Status: Online
- Logic: AND

**Result:** Computers active in last week

## Security Features

### Input Validation
All filter inputs are validated to prevent:
- **Wildcard Injection** - Pattern length limits, character restrictions
- **LDAP Injection** - OU path sanitization, special character filtering
- **Numeric Range Attacks** - RAM values validated (1-1024 GB range)

### Security Tags
Critical validation points tagged with:
- `#SECURITY_CRITICAL` - Input validation checkpoints
- `#WILDCARD_INJECTION_PREVENTION` - Pattern validation
- `#LDAP_INJECTION_PREVENTION` - OU path validation
- `#NUMERIC_VALIDATION` - Range checking

## Performance Optimization

### Efficient Filtering
- Filters applied in-memory (no database re-queries)
- Parallel filtering for large datasets
- Results cached until inventory refresh

### Best Practices
1. Use Quick Filters for common scenarios
2. Save complex filters as presets for reuse
3. Combine filters progressively (start broad, narrow down)
4. Clear filters when done to see full inventory

## Troubleshooting

### No Results Found
- **Check filter criteria** - Are they too restrictive?
- **Verify data exists** - Run inventory scan first
- **Try broader filters** - Use wildcards in name patterns

### Filter Not Saving
- **Check preset name** - Must be unique, no special characters
- **Verify criteria** - At least one criterion must be set
- **Check file permissions** - AppData folder must be writable

### Slow Filtering
- **Reduce dataset** - Filter in stages (status → OS → name)
- **Simplify patterns** - Avoid excessive wildcards
- **Refresh inventory** - Clear old cached data

## File Locations

### Filter Presets
Stored at: `%APPDATA%\NecessaryAdminTool\FilterPresets.json`

### Settings
Filter settings: `%APPDATA%\NecessaryAdminTool\UserConfig.xml`

## Built-In Presets

| Preset Name | Description |
|-------------|-------------|
| Online Computers | All computers responding to ping |
| Offline Computers | All non-responsive computers |
| Windows 11 | All Windows 11 systems |
| Windows 10 | All Windows 10 systems |
| Windows 7 (EOL) | End-of-life Windows 7 systems |
| Windows Servers | All Windows Server OS |
| Workstations | All non-server OS |
| High Memory (≥16GB) | Computers with 16GB+ RAM |
| Low Memory (<8GB) | Computers with <8GB RAM |

## Future Enhancements

Coming in future versions:
- Advanced filter dialog with all criteria
- Filter combinations (save multiple filters as groups)
- Scheduled filter reports
- Filter-based automation triggers
- Export filtered results directly

## Support

For issues or feature requests:
1. Check audit logs for error details
2. Verify security validator is not blocking input
3. Review `FilterManager` logs in debug terminal
4. Contact IT Support with filter criteria used

---

**Version:** 1.0 (February 2026)
**TAG:** #FILTER_SYSTEM #USER_GUIDE #DOCUMENTATION
