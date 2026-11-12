using System;
using System.Drawing;
using System.Globalization;
using MaterialColorUtilities.Palettes;
namespace WinDynamicIsland
{
    internal sealed class MaterialPalette
    {
        internal Color Surface, Container, High, Highest, Ink, Muted, Primary, OnPrimary, Secondary, OnSecondary, PrimaryContainer, OnContainer, Error, Success, Outline;
        internal int Seed;
        internal static readonly MaterialPalette Default = new MaterialPalette
        {
            Seed = Color.FromArgb(103, 80, 164).ToArgb(), Surface = Color.FromArgb(20,18,24), Container = Color.FromArgb(33,31,38), High = Color.FromArgb(43,41,48), Highest = Color.FromArgb(54,52,59),
            Ink = Color.FromArgb(230,224,233), Muted = Color.FromArgb(202,196,208), Primary = Color.FromArgb(208,188,255), OnPrimary = Color.FromArgb(56,30,114), Secondary = Color.FromArgb(74,68,88), OnSecondary = Color.FromArgb(232,222,248),
            PrimaryContainer = Color.FromArgb(79,55,139), OnContainer = Color.FromArgb(234,221,255), Error = Color.FromArgb(255,180,171), Success = Color.FromArgb(174,211,178), Outline = Color.FromArgb(147,143,153)
        };
        static Color Tone(TonalPalette palette, uint tone) { return Color.FromArgb(unchecked((int)palette[tone])); }
        internal static readonly string[] Styles = { "TonalSpot", "Vibrant", "Expressive", "Content", "Rainbow", "Monochrome" };
        internal static string NormalizeStyle(string style) { return Array.IndexOf(Styles, style) >= 0 ? style : "TonalSpot"; }
        // Deep black-blue night with pale spirit-light blue accents.
        internal static readonly MaterialPalette Ori = new MaterialPalette
        {
            Seed = Color.FromArgb(63, 182, 240).ToArgb(), Surface = Color.FromArgb(2,7,12), Container = Color.FromArgb(7,19,29), High = Color.FromArgb(12,29,43), Highest = Color.FromArgb(17,39,57),
            Ink = Color.FromArgb(221,243,255), Muted = Color.FromArgb(156,196,218), Primary = Color.FromArgb(143,220,255), OnPrimary = Color.FromArgb(0,48,74), Secondary = Color.FromArgb(18,51,71), OnSecondary = Color.FromArgb(191,230,250),
            PrimaryContainer = Color.FromArgb(14,74,107), OnContainer = Color.FromArgb(198,238,255), Error = Color.FromArgb(255,180,171), Success = Color.FromArgb(150,226,214), Outline = Color.FromArgb(79,124,149)
        };
        internal static MaterialPalette FromSeed(Color seed) { return FromSeed(seed, "TonalSpot"); }
        internal static MaterialPalette FromSeed(Color seed, string style)
        {
            style = NormalizeStyle(style);
            uint argb = unchecked((uint)seed.ToArgb());
            Style kind = Style.TonalSpot;
            switch (style)
            {
                case "Vibrant": kind = Style.Vibrant; break;
                case "Expressive": kind = Style.Expressive; break;
                case "Content": kind = Style.Content; break;
                case "Rainbow": kind = Style.Rainbow; break;
                case "Monochrome": kind = Style.Spritz; break;
            }
            var core = CorePalette.Of(argb, kind);
            return new MaterialPalette
            {
                Seed = seed.ToArgb(), Surface = Tone(core.Neutral, 6), Container = Tone(core.Neutral, 12), High = Tone(core.Neutral, 17), Highest = Tone(core.Neutral, 22),
                Ink = Tone(core.Neutral, 90), Muted = Tone(core.NeutralVariant, 80), Outline = Tone(core.NeutralVariant, 60), Primary = Tone(core.Primary, 80), OnPrimary = Tone(core.Primary, 20),
                PrimaryContainer = Tone(core.Primary, 30), OnContainer = Tone(core.Primary, 90), Secondary = Tone(core.Secondary, 30), OnSecondary = Tone(core.Secondary, 90), Error = Tone(core.Error, 80), Success = Default.Success
            };
        }
        // True black surfaces for OLED screens; accents stay untouched.
        internal MaterialPalette PureBlack()
        {
            var copy = (MaterialPalette)MemberwiseClone();
            copy.Surface = Color.Black;
            copy.Container = Mix(Container, Color.Black, 0.55f); copy.High = Mix(High, Color.Black, 0.35f); copy.Highest = Mix(Highest, Color.Black, 0.2f);
            return copy;
        }
        static Color Mix(Color a, Color b, float t) { return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t)); }
        internal static string Hex(Color color) { return "#" + (color.ToArgb() & 0xffffff).ToString("X6", CultureInfo.InvariantCulture); }
        internal static Color Parse(string value)
        {
            int rgb;
            return value != null && value.Length == 7 && value[0] == '#' && int.TryParse(value.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out rgb) ? Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255) : Color.FromArgb(103,80,164);
        }
    }
}
