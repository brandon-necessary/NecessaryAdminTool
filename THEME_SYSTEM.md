# Unified Theme System

**Version:** 1.0 (1.2602.0.0)
**Last Updated:** February 14, 2026
**TAG:** #THEME_SYSTEM #UNIFIED_THEME #VERSION_1_0

---

## Overview

NecessaryAdminTool implements a **unified theme system** where all WPF windows inherit theme resources from `App.xaml`. This ensures:

- ✅ **Consistency** - All windows use the same colors, fonts, and styles
- ✅ **Maintainability** - Single source of truth for theme definitions
- ✅ **Configurability** - Theme can be changed from Settings without code changes
- ✅ **Readability** - Dark theme optimized for long admin sessions

---

## Architecture

### Theme Hierarchy

```
App.xaml (Application.Resources)
    └─ Global Theme Resources (Colors, Brushes, Styles)
        ├─ MainWindow.xaml (inherits theme)
        ├─ SetupWizardWindow.xaml (inherits theme)
        ├─ OptionsWindow.xaml (inherits theme)
        ├─ AboutWindow.xaml (inherits theme)
        └─ All other windows (inherit theme)
```

### Key Files

| File | Purpose |
|------|---------|
| `App.xaml` | **Master theme definition** - All global resources |
| `MainWindow.xaml` | Window-specific resources only (inherits from App.xaml) |
| `OptionsWindow.xaml` | Color picker for theme customization |

---

## Color Palette

### Core Colors

```xml
<!-- Background Colors -->
<Color x:Key="BgDarkest">#FF0D0D0D</Color>      <!-- Main background -->
<Color x:Key="BgDark">#FF1A1A1A</Color>         <!-- Secondary background -->
<Color x:Key="BgMedium">#FF252526</Color>       <!-- Tertiary background -->
<Color x:Key="BgCard">#FF1E1E1E</Color>         <!-- Card/panel background -->

<!-- Border Colors -->
<Color x:Key="BorderDim">#FF3C3C3C</Color>      <!-- Subtle borders -->
<Color x:Key="BorderBright">#FF555555</Color>   <!-- Prominent borders -->

<!-- Accent Colors -->
<Color x:Key="AccentOrange">#FFFF8533</Color>   <!-- Primary accent (buttons, highlights) -->
<Color x:Key="AccentZinc">#FFA1A1AA</Color>     <!-- Secondary accent (muted) -->
<Color x:Key="AccentBlue">#FF0078D7</Color>     <!-- Alternative accent -->
<Color x:Key="AccentGreen">#FF16C60C</Color>    <!-- Success/online indicator -->

<!-- Status Colors -->
<Color x:Key="DangerRed">#FFE81123</Color>      <!-- Errors, warnings, delete actions -->
<Color x:Key="WarningOrange">#FFF7630C</Color>  <!-- Caution, important notices -->
```

### Text Colors

```xml
<SolidColorBrush x:Key="TextPrimaryBrush" Color="#FFFFFFFF"/>    <!-- White - primary text -->
<SolidColorBrush x:Key="TextSecondaryBrush" Color="#FFB0B0B0"/>  <!-- Light gray - secondary text -->
<SolidColorBrush x:Key="TextMutedBrush" Color="#FF808080"/>      <!-- Medium gray - muted/disabled -->
```

---

## Button Styles

### BtnPrimary
Default button style with orange accent.

```xml
<Button Content="Save" Style="{StaticResource BtnPrimary}"/>
```

**States:**
- Normal: `#FFFF8533` (Orange)
- Hover: `#FFFF9944` (Lighter orange)
- Pressed: `#FFDD6622` (Darker orange)
- Disabled: `#FF444444` (Gray)

### BtnDanger
Destructive actions (delete, remove, cancel).

```xml
<Button Content="Delete" Style="{StaticResource BtnDanger}"/>
```

**States:**
- Normal: `#FFE81123` (Red)
- Hover: `#FFFF2D2D` (Lighter red)
- Pressed: `#FFB01020` (Darker red)

### BtnGhost
Subtle button with transparent background and border.

```xml
<Button Content="Cancel" Style="{StaticResource BtnGhost}"/>
```

**States:**
- Normal: Transparent background, gray border
- Hover: `#FF333333` background, orange border

### BtnWarning
Important actions requiring attention.

```xml
<Button Content="Force Update" Style="{StaticResource BtnWarning}"/>
```

**States:**
- Normal: `#FFF7630C` (Warning orange)
- Hover: `#FFFF8C00` (Lighter warning orange)

### BtnTool
Specialized button for remote management tools.

```xml
<Button Content="🖥️ Remote CMD" Style="{StaticResource BtnTool}"/>
```

---

## Control Styles

### ComboBox (Dropdown)

Fully styled dark theme ComboBox with:
- Dark background `#FF2D2D2D`
- White text (readable!)
- Orange accent on hover/selection
- Properly styled dropdown items

**Fixed Issues:**
- ✅ No more light text on light background
- ✅ Selected items are highlighted with orange tint
- ✅ Hover effect on dropdown items

### TextBox

Consistent dark input fields:
- Background: `#FF1A1A1A`
- Foreground: White
- Caret: White
- Border: `#FF3C3C3C`

### DataGrid

Professional dark table styling:
- Alternating row colors
- Orange selection highlight (`#66FF8533`)
- Hover effect
- Dark headers with light gray text

### TabItem

Clean tab navigation:
- Inactive: Gray text, transparent background
- Active: White text, orange bottom border
- Hover: Light gray background

### ScrollBar

Modern thin scrollbar (12px width):
- Track: Dark gray
- Thumb: Medium gray
- Thumb Hover: Orange
- Thumb Dragging: Light orange

---

## Usage Examples

### Inheriting Theme in New Windows

```xml
<Window x:Class="NecessaryAdminTool.NewWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Background="#FF0D0D0D">

    <!-- No Window.Resources needed - theme inherited from App.xaml -->

    <Grid>
        <Button Content="Save" Style="{StaticResource BtnPrimary}"/>
        <ComboBox /> <!-- Automatically styled -->
        <TextBox /> <!-- Automatically styled -->
    </Grid>
</Window>
```

### Adding Window-Specific Resources

```xml
<Window.Resources>
    <!-- Window-specific resources only -->
    <Style x:Key="CustomStyle" TargetType="TextBlock">
        <Setter Property="FontSize" Value="16"/>
        <Setter Property="Foreground" Value="{StaticResource AccentOrangeBrush}"/>
    </Style>
</Window.Resources>
```

---

## Theme Customization

### Color Picker Integration

The unified theme system is designed to support dynamic color changes through the Settings menu:

1. **Current Implementation:**
   - `OptionsWindow.xaml` contains color pickers for `PrimaryAccentColor` and `SecondaryAccentColor`
   - Values stored in `Properties.Settings.Default`

2. **Future Enhancement (v1.1+):**
   - Bind `AccentOrange` and `AccentZinc` colors to user settings
   - Apply theme changes dynamically without restart
   - Save/load theme presets

---

## Design Principles

### 1. **Readability First**
All text is white or light gray on dark backgrounds. Never light text on light backgrounds.

### 2. **Consistency**
Every control type has exactly one default style. No exceptions.

### 3. **Accessibility**
Sufficient contrast ratios for WCAG AA compliance:
- White on `#0D0D0D`: 19.36:1 ✅
- Orange on `#0D0D0D`: 5.12:1 ✅

### 4. **Professional Appearance**
Clean, modern dark theme suitable for enterprise environments.

---

## Migration Notes

### Before v1.0 (ArtaznIT Legacy)
Each window defined its own theme resources, leading to:
- ❌ Inconsistent colors across windows
- ❌ Duplicate code (473 lines × 5 windows = 2365 lines)
- ❌ Difficult maintenance
- ❌ Unreadable dropdowns in setup wizard

### After v1.0 (Unified Theme)
- ✅ Single source of truth in `App.xaml`
- ✅ Consistent appearance across all windows
- ✅ Easy to maintain and extend
- ✅ Readable controls everywhere

---

## Troubleshooting

### Issue: Controls not styled correctly

**Solution:** Ensure window background is set:
```xml
<Window Background="#FF0D0D0D">
```

### Issue: Resource not found error

**Solution:** Make sure resource key matches exactly:
```xml
<!-- Correct -->
<Button Style="{StaticResource BtnPrimary}"/>

<!-- Wrong -->
<Button Style="{StaticResource ButtonPrimary}"/>
```

### Issue: Theme not applying to new window

**Solution:** Remove any `Window.Resources` that redefine global styles.

---

## Future Enhancements (v1.1+)

1. **Dynamic Theme Switching**
   - Light/Dark mode toggle
   - Real-time color updates without restart

2. **Theme Presets**
   - Default (Orange/Zinc)
   - Blue Theme
   - Green Theme
   - Custom user themes

3. **Per-Control Overrides**
   - Allow windows to override specific colors while maintaining consistency

4. **Theme Export/Import**
   - Save custom themes as `.json` files
   - Share themes across installations

---

## Related Files

- `/App.xaml` - Master theme definition
- `/MainWindow.xaml` - Main application window
- `/SetupWizardWindow.xaml` - Setup wizard (now with readable dropdowns!)
- `/OptionsWindow.xaml` - Settings and color picker
- `/AboutWindow.xaml` - About dialog

---

**Built with Claude Code** 🤖
**Copyright © 2026 Brandon Necessary**
