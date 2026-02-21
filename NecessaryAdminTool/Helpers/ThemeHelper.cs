using System;
using System.Windows;
using System.Windows.Media;

namespace NecessaryAdminTool.Helpers
{
    // TAG: #THEME_COLORS #AUTO_UPDATE_UI_ENGINE
    /// <summary>
    /// Centralized accent color manager. All accent color changes flow through here.
    /// Replaces scattered Color.FromRgb() calls and duplicate constants.
    /// </summary>
    public static class ThemeHelper
    {
        // Factory-default colors (orange/zinc theme)
        public static readonly Color DefaultPrimary = Color.FromRgb(255, 133, 51);    // #FF8533
        public static readonly Color DefaultSecondary = Color.FromRgb(161, 161, 170);  // #A1A1AA

        /// <summary>Current primary accent color (updated by ApplyAccentColors)</summary>
        public static Color PrimaryColor { get; private set; } = DefaultPrimary;

        /// <summary>Current secondary accent color (updated by ApplyAccentColors)</summary>
        public static Color SecondaryColor { get; private set; } = DefaultSecondary;

        /// <summary>Live primary brush from app resources</summary>
        public static SolidColorBrush PrimaryBrush =>
            (SolidColorBrush)Application.Current.Resources["AccentOrangeBrush"];

        /// <summary>Live secondary brush from app resources</summary>
        public static SolidColorBrush SecondaryBrush =>
            (SolidColorBrush)Application.Current.Resources["AccentZincBrush"];

        // Derivative colors computed from primary
        public static Color PrimaryHover => LightenColor(PrimaryColor, 0.15);
        public static Color PrimaryPressed => DarkenColor(PrimaryColor, 0.15);
        public static Color PrimarySemi => Color.FromArgb(128, PrimaryColor.R, PrimaryColor.G, PrimaryColor.B);
        public static Color PrimaryLight => Color.FromArgb(64, PrimaryColor.R, PrimaryColor.G, PrimaryColor.B);

        #region Named Colors & Presets

        public struct NamedColor
        {
            public string Name;
            public Color Color;
            public NamedColor(string name, Color color) { Name = name; Color = color; }
        }

        public struct ThemePreset
        {
            public string Name;
            public Color Primary;
            public Color Secondary;
            public ThemePreset(string name, Color primary, Color secondary) { Name = name; Primary = primary; Secondary = secondary; }
        }

        public static readonly NamedColor[] PrimaryColors = new[]
        {
            new NamedColor("Orange",  Color.FromRgb(255, 133, 51)),
            new NamedColor("Blue",    Color.FromRgb(0, 120, 215)),
            new NamedColor("Teal",    Color.FromRgb(0, 180, 160)),
            new NamedColor("Purple",  Color.FromRgb(139, 92, 246)),
            new NamedColor("Red",     Color.FromRgb(229, 62, 62)),
            new NamedColor("Green",   Color.FromRgb(56, 161, 105)),
            new NamedColor("Cobalt",  Color.FromRgb(59, 130, 246)),
            new NamedColor("Magenta", Color.FromRgb(217, 70, 239)),
            new NamedColor("Gold",    Color.FromRgb(234, 179, 8)),
            new NamedColor("Crimson", Color.FromRgb(220, 38, 38)),
        };

        public static readonly NamedColor[] SecondaryColors = new[]
        {
            new NamedColor("Zinc",     Color.FromRgb(161, 161, 170)),
            new NamedColor("Steel",    Color.FromRgb(100, 116, 139)),
            new NamedColor("Slate",    Color.FromRgb(71, 85, 105)),
            new NamedColor("Silver",   Color.FromRgb(192, 192, 192)),
            new NamedColor("Charcoal", Color.FromRgb(55, 65, 81)),
            new NamedColor("Ash",      Color.FromRgb(107, 114, 128)),
            new NamedColor("Pearl",    Color.FromRgb(226, 232, 240)),
            new NamedColor("Smoke",    Color.FromRgb(113, 113, 122)),
            new NamedColor("Graphite", Color.FromRgb(75, 85, 99)),
            new NamedColor("Ivory",    Color.FromRgb(245, 245, 220)),
        };

        public static readonly ThemePreset[] Presets = new[]
        {
            new ThemePreset("Default",  PrimaryColors[0].Color, SecondaryColors[0].Color),
            new ThemePreset("Ocean",    PrimaryColors[1].Color, SecondaryColors[1].Color),
            new ThemePreset("Forest",   PrimaryColors[2].Color, SecondaryColors[2].Color),
            new ThemePreset("Royal",    PrimaryColors[3].Color, SecondaryColors[3].Color),
            new ThemePreset("Ember",    PrimaryColors[4].Color, SecondaryColors[4].Color),
            new ThemePreset("Nature",   PrimaryColors[5].Color, SecondaryColors[5].Color),
            new ThemePreset("Arctic",   PrimaryColors[6].Color, SecondaryColors[6].Color),
            new ThemePreset("Neon",     PrimaryColors[7].Color, SecondaryColors[7].Color),
            new ThemePreset("Sunset",   PrimaryColors[8].Color, SecondaryColors[8].Color),
            new ThemePreset("Midnight", PrimaryColors[9].Color, SecondaryColors[9].Color),
        };

        #endregion

        /// <summary>
        /// Apply accent colors to ALL app resources at once.
        /// Called from MainWindow.ApplySavedAccentColors(), OptionsWindow, and ResetToDefaults().
        /// </summary>
        public static void ApplyAccentColors(Color primary, Color secondary)
        {
            PrimaryColor = primary;
            SecondaryColor = secondary;

            var res = Application.Current.Resources;

            // Core brushes
            res["AccentOrangeBrush"] = new SolidColorBrush(primary);
            res["AccentZincBrush"] = new SolidColorBrush(secondary);
            res["AccentColor"] = new SolidColorBrush(primary);

            // Color keys (can be replaced at runtime for ControlTemplate.Triggers that reference Color keys)
            res["AccentOrange"] = primary;
            res["AccentZinc"] = secondary;

            // Horizontal gradient (tabs, headers)
            var hGradient = new LinearGradientBrush();
            hGradient.StartPoint = new Point(0, 0);
            hGradient.EndPoint = new Point(1, 0);
            hGradient.GradientStops.Add(new GradientStop(primary, 0));
            hGradient.GradientStops.Add(new GradientStop(secondary, 1));
            res["AccentGradientBrush"] = hGradient;

            // Vertical gradient (borders, headers)
            var vGradient = new LinearGradientBrush();
            vGradient.StartPoint = new Point(0, 0);
            vGradient.EndPoint = new Point(0, 1);
            vGradient.GradientStops.Add(new GradientStop(primary, 0));
            vGradient.GradientStops.Add(new GradientStop(secondary, 1));
            res["AccentGradientVerticalBrush"] = vGradient;

            // Tab hover (25% opacity tint of primary)
            res["AccentHoverBrush"] = new SolidColorBrush(PrimaryLight);

            // Derivative brushes for button states
            res["AccentPrimaryHoverBrush"] = new SolidColorBrush(PrimaryHover);
            res["AccentPrimaryPressedBrush"] = new SolidColorBrush(PrimaryPressed);
            res["AccentPrimarySemiBrush"] = new SolidColorBrush(PrimarySemi);

            // Semi-transparent variants for DataGrid selection, ComboBox highlight
            res["AccentPrimarySelectionBrush"] = new SolidColorBrush(Color.FromArgb(102, primary.R, primary.G, primary.B)); // ~40%
            res["AccentPrimaryHighlightBrush"] = new SolidColorBrush(Color.FromArgb(77, primary.R, primary.G, primary.B));  // ~30%

            // Legacy brush alias
            res["AccentCyanBrush"] = new SolidColorBrush(primary);
        }

        /// <summary>Reset to factory default orange/zinc theme</summary>
        public static void ResetToDefaults()
        {
            ApplyAccentColors(DefaultPrimary, DefaultSecondary);

            Properties.Settings.Default.PrimaryAccentColor = $"#FF{DefaultPrimary.R:X2}{DefaultPrimary.G:X2}{DefaultPrimary.B:X2}";
            Properties.Settings.Default.SecondaryAccentColor = $"#FF{DefaultSecondary.R:X2}{DefaultSecondary.G:X2}{DefaultSecondary.B:X2}";
            Properties.Settings.Default.Save();
        }

        /// <summary>Convert a Color to hex string (#FFRRGGBB)</summary>
        public static string ColorToHex(Color c)
        {
            return $"#FF{c.R:X2}{c.G:X2}{c.B:X2}";
        }

        /// <summary>Try parse hex color string, returns false if invalid</summary>
        public static bool TryParseColor(string hex, out Color color)
        {
            color = default;
            try
            {
                if (!string.IsNullOrWhiteSpace(hex) && hex.StartsWith("#") && (hex.Length == 7 || hex.Length == 9))
                {
                    color = (Color)ColorConverter.ConvertFromString(hex);
                    return true;
                }
            }
            catch { }
            return false;
        }

        #region Color Utilities

        private static Color LightenColor(Color c, double amount)
        {
            byte r = (byte)Math.Min(255, c.R + (255 - c.R) * amount);
            byte g = (byte)Math.Min(255, c.G + (255 - c.G) * amount);
            byte b = (byte)Math.Min(255, c.B + (255 - c.B) * amount);
            return Color.FromRgb(r, g, b);
        }

        private static Color DarkenColor(Color c, double amount)
        {
            byte r = (byte)Math.Max(0, c.R * (1 - amount));
            byte g = (byte)Math.Max(0, c.G * (1 - amount));
            byte b = (byte)Math.Max(0, c.B * (1 - amount));
            return Color.FromRgb(r, g, b);
        }

        #endregion
    }
}
