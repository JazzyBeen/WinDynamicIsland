using System;
using System.Diagnostics;
using System.Drawing;
namespace WinDynamicIsland
{
    // Product-wide semantic roles. Classic values stay at the call site so the
    // original design can be restored without rebuilding controls or services.
    internal static class AppDesign
    {
        static MaterialPalette palette = MaterialPalette.Default;
        static string paletteKey = "Default";
        internal static Color Surface { get { return palette.Surface; } } internal static Color Container { get { return palette.Container; } }
        internal static Color High { get { return palette.High; } } internal static Color Highest { get { return palette.Highest; } }
        internal static Color Ink { get { return palette.Ink; } } internal static Color Muted { get { return palette.Muted; } }
        internal static Color Primary { get { return palette.Primary; } } internal static Color OnPrimary { get { return palette.OnPrimary; } }
        internal static Color Secondary { get { return palette.Secondary; } } internal static Color OnSecondary { get { return palette.OnSecondary; } }
        internal static Color PrimaryContainer { get { return palette.PrimaryContainer; } } internal static Color OnContainer { get { return palette.OnContainer; } }
        internal static Color Error { get { return palette.Error; } } internal static Color Success { get { return palette.Success; } }
        internal static Color Outline { get { return palette.Outline; } }
        internal static Color Seed { get { return Color.FromArgb(palette.Seed); } }
        internal static void ConfigurePalette()
        {
            string mode = Program.Settings.MaterialColorMode;
            string seed = mode == "Wallpaper" ? WallpaperPaletteMonitor.CachedSeed : Program.Settings.MaterialSeedColor;
            string key = mode == "Default" ? "Default" : mode + seed;
            if (key == paletteKey) return;
            if (mode == "Default") palette = MaterialPalette.Default;
            else palette = MaterialPalette.FromSeed(MaterialPalette.Parse(seed));
            paletteKey = key;
        }
        internal static void WallpaperSeed(Color seed) { palette = MaterialPalette.FromSeed(seed); paletteKey = "Wallpaper" + MaterialPalette.Hex(seed); }
        static readonly Stopwatch clock = Stopwatch.StartNew();
        static double from, target, changed;
        static bool initialized;
        internal static bool Material { get { return Program.Settings.InterfaceDesign == "Material"; } }
        internal static bool Transitioning { get { return initialized && from != target && clock.Elapsed.TotalMilliseconds - changed < 300; } }
        internal static double Amount
        {
            get
            {
                if (!initialized) return Material ? 1 : 0;
                double t = Math.Max(0, Math.Min(1, (clock.Elapsed.TotalMilliseconds - changed) / 300));
                t = t * t * (3 - 2 * t);
                return from + (target - from) * t;
            }
        }
        internal static void Change(bool material, bool animate)
        {
            double next = material ? 1 : 0;
            if (initialized && next == target) return;
            double current = Amount;
            initialized = true; target = next;
            from = animate && Program.Settings.InterfaceAnimations ? current : next;
            changed = clock.Elapsed.TotalMilliseconds;
        }
        internal static Color Blend(Color a, Color b, double amount)
        {
            amount = Math.Max(0, Math.Min(1, amount));
            return Color.FromArgb((int)Math.Round(a.A + (b.A - a.A) * amount), (int)Math.Round(a.R + (b.R - a.R) * amount), (int)Math.Round(a.G + (b.G - a.G) * amount), (int)Math.Round(a.B + (b.B - a.B) * amount));
        }
        internal static Color Pick(Color classic, Color material) { return Blend(classic, material, Amount); }
        internal static Color Background { get { return Pick(Color.Black, Surface); } }
        internal static Color Foreground { get { return Pick(Color.White, Ink); } }
        internal static Color SecondaryText { get { return Pick(Color.FromArgb(152, 152, 160), Muted); } }
        internal static Color IslandBackground { get { return Pick(Color.Black, Container); } }
        internal static double Spatial(double t)
        {
            if (t <= 0) return 0;
            if (t >= 1) return 1;
            if (!Material) return t * t * (3 - 2 * t);
            // Expressive spatial spring: slight overshoot, continuous endpoints.
            double value = 1 - Math.Exp(-7 * t) * (Math.Cos(9 * t) + 7d / 9 * Math.Sin(9 * t));
            double end = 1 - Math.Exp(-7) * (Math.Cos(9) + 7d / 9 * Math.Sin(9));
            return value / end;
        }
        internal static void Fill(Graphics g, RectangleF bounds, float radius, Color color)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0) return;
            using (var path = IslandTheme.Round(bounds, radius))
            using (var brush = new SolidBrush(color)) g.FillPath(brush, path);
        }
        internal static string NavigationIcon(string key)
        {
            switch (key)
            {
                case "General": return "\ue8b8"; case "Appearance": return "\ue40a"; case "Focus": return "\ue425"; case "Media": return "\ue039";
                case "Notifications": return "\ue7f4"; case "Devices": return "\ue1b1"; case "VPN": return "\ue0da"; default: return "\ue88e";
            }
        }
        internal static void Symbol(Graphics g, string symbol, RectangleF box, Color color, float size = 20)
        {
            using (var font = AppTypography.Symbol(size))
            using (var brush = new SolidBrush(color))
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(symbol, font, brush, box, format);
        }
    }
}
