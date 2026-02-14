# Branding & Logo Management - ArtaznIT Suite

## Modular Logo System

The ArtaznIT Suite uses a **centralized logo configuration** that makes it easy to update branding across the entire application from a single location.

---

## Quick Logo Update

To change the logo/branding, edit **ONE file**:

**File:** `ArtaznIT/MainWindow.xaml.cs`
**Class:** `LogoConfig` (around line 8612)

### Configuration Options:

```csharp
public static class LogoConfig
{
    // BRANDING TEXT - Change these to update company name
    public const string COMPANY_NAME = "Artazn";
    public const string COMPANY_SUFFIX = " LLC";
    public const string TAGLINE = "I T   M A N A G E M E N T   S U I T E";
    public const string VERSION = "v5.2";

    // COLORS - Orange/Zinc Theme
    public static readonly Color ORANGE_PRIMARY = Color.FromRgb(255, 133, 51);  // #FFFF8533
    public static readonly Color ORANGE_DARK = Color.FromRgb(204, 107, 41);     // #FFCC6B29
    public static readonly Color ZINC_COLOR = Color.FromRgb(161, 161, 170);     // #FFA1A1AA
    public static readonly Color BG_DARK = Color.FromRgb(26, 26, 26);           // #FF1A1A1A

    // LOGO SIZES (for different contexts)
    public const double LARGE_ICON_SIZE = 50;   // Splash screens
    public const double MEDIUM_ICON_SIZE = 36;  // Default
    public const double SMALL_ICON_SIZE = 24;   // Compact views
}
```

---

## Logo Placements

All logo placements are tagged with `LOGO_PLACEMENT` comments for easy searching:

### 1. Main Window Top Bar
**File:** `MainWindow.xaml`
**Line:** ~511
**Tag:** `<!-- LOGO_PLACEMENT: Main Window Top Bar Logo -->`
**Type:** XAML (manual placement)
**Note:** This is still XAML-based for performance. Can be migrated to `LogoConfig` if needed.

### 2. Login Dialog
**File:** `MainWindow.xaml.cs`
**Line:** ~8905 (in LoginWindow constructor)
**Tag:** `// LOGO_PLACEMENT: Login Dialog Header Logo`
**Type:** Programmatic (uses `LogoConfig.CreateFullLogo()`)
**Features:** Auto-scales, uses centralized branding

### 3. Elevation Dialog
**File:** `MainWindow.xaml.cs`
**Line:** ~8794 (in ElevationDialog constructor)
**Type:** Text-only (no logo currently)
**Note:** Can add logo using `LogoConfig.CreateFullLogo()`

---

## How to Search for Logo Placements

### In Visual Studio / VS Code:
```
Search: LOGO_PLACEMENT
```

### In Command Line:
```bash
cd "C:\Users\brandon.necessary\source\repos\ArtaznIT"
grep -rn "LOGO_PLACEMENT" --include=*.xaml --include=*.cs
```

**Result:** All logo locations will be shown with line numbers.

---

## Changing the Logo Icon (the "A" symbol)

The logo icon is defined as an SVG path in `LogoConfig.CreateIconPath()`:

```csharp
public static System.Windows.Shapes.Path CreateIconPath()
{
    return new System.Windows.Shapes.Path
    {
        Fill = Brushes.White,
        Data = Geometry.Parse("M12,2 L20,22 L16,22 L14.5,18 L9.5,18 L8,22 L4,22 Z M10.5,14 L13.5,14 L12,9 Z")
        //                     ^ THIS IS THE SVG PATH DATA
    };
}
```

### To Change the Icon:

1. **Design your icon** in a vector editor (Inkscape, Figma, etc.)
2. **Export as SVG** (24x24 viewbox recommended)
3. **Extract the path data** from the SVG `<path d="...">` attribute
4. **Replace** the `Geometry.Parse()` string with your new path

**Example SVG Path Conversion:**
```xml
<!-- Your SVG file -->
<svg viewBox="0 0 24 24">
  <path d="M12,2 L20,22 L16,22 ..."/>
</svg>

<!-- Extract this and put in CreateIconPath() -->
Data = Geometry.Parse("M12,2 L20,22 L16,22 ...")
```

---

## Changing Colors

All colors are centralized in `LogoConfig`:

```csharp
// Change these to update the color scheme
public static readonly Color ORANGE_PRIMARY = Color.FromRgb(255, 133, 51);
public static readonly Color ORANGE_DARK = Color.FromRgb(204, 107, 41);
public static readonly Color ZINC_COLOR = Color.FromRgb(161, 161, 170);
```

**Effect:** Changes colors everywhere the logo appears (login, dialogs, headers, etc.)

---

## Adding Logo to New Windows

To add the logo to a new dialog or window:

```csharp
// Inside your Window constructor
var logo = LogoConfig.CreateFullLogo(
    includeVersion: true,   // Show "v5.2" badge
    scale: 0.9              // 90% size
);

// Add to your layout
myPanel.Children.Add(logo);
```

**Example:**
```csharp
public class MyCustomDialog : Window
{
    public MyCustomDialog()
    {
        Title = "My Dialog";

        var panel = new StackPanel();

        // Add logo at top
        panel.Children.Add(LogoConfig.CreateFullLogo(includeVersion: false, scale: 0.8));

        // Add your content below
        panel.Children.Add(new TextBlock { Text = "Dialog content..." });

        Content = panel;
    }
}
```

---

## Version Number Management

Version displayed in the logo badge is controlled by:

```csharp
public const string VERSION = "v5.2";
```

**Also update these locations:**
1. `MainWindow.xaml` - Window Title (line ~4)
2. `AboutWindow.xaml.cs` - About dialog version text
3. `AssemblyInfo.cs` - Assembly version attributes

**Tip:** Search for the old version number (e.g., "5.2") to find all occurrences.

---

## Color Theme Reference

| Element | Color | Hex | RGB |
|---------|-------|-----|-----|
| **Orange Primary** | Brand orange, buttons | `#FFFF8533` | 255, 133, 51 |
| **Orange Dark** | Gradients, shadows | `#FFCC6B29` | 204, 107, 41 |
| **Zinc** | Secondary text, subtle UI | `#FFA1A1AA` | 161, 161, 170 |
| **Background Dark** | Cards, inputs | `#FF1A1A1A` | 26, 26, 26 |
| **Background Darkest** | Main background | `#FF0D0D0D` | 13, 13, 13 |
| **Background Medium** | Headers, panels | `#FF252526` | 37, 37, 38 |

---

## Migrating XAML Logo to Programmatic (Optional)

The main window top bar logo is still in XAML for performance. To migrate it to `LogoConfig`:

**Before (XAML):**
```xml
<!-- MainWindow.xaml, line ~511 -->
<StackPanel Grid.Column="0" Orientation="Horizontal">
    <!-- Complex XAML logo definition -->
</StackPanel>
```

**After (Code):**
```csharp
// In MainWindow.xaml.cs constructor, after InitializeComponent()
var topBarLogo = LogoConfig.CreateFullLogo(includeVersion: true, scale: 1.0);
// Find the Grid in XAML and replace the StackPanel with this
```

**Trade-off:**
- **XAML:** Slightly faster initial load, harder to maintain
- **Programmatic:** Centralized, easier to update, negligible performance difference

---

## Summary

✅ **Single source of truth**: `LogoConfig` class
✅ **Easy to find**: Search for `LOGO_PLACEMENT`
✅ **Color consistency**: All colors centralized
✅ **Scalable**: Use same logo at any size
✅ **Modular**: Add to new windows with one line

**Next time you rebrand:**
1. Update `LogoConfig` constants
2. Replace SVG path if changing icon
3. Build and test
4. Done! 🎉
