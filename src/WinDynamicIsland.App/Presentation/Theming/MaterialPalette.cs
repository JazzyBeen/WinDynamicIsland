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
        internal static MaterialPalette FromSeed(Color seed)
        {
            var core = CorePalette.Of(unchecked((uint)seed.ToArgb()), Style.TonalSpot);
            return new MaterialPalette
            {
                Seed = seed.ToArgb(), Surface = Tone(core.Neutral, 6), Container = Tone(core.Neutral, 12), High = Tone(core.Neutral, 17), Highest = Tone(core.Neutral, 22),
                Ink = Tone(core.Neutral, 90), Muted = Tone(core.NeutralVariant, 80), Outline = Tone(core.NeutralVariant, 60), Primary = Tone(core.Primary, 80), OnPrimary = Tone(core.Primary, 20),
                PrimaryContainer = Tone(core.Primary, 30), OnContainer = Tone(core.Primary, 90), Secondary = Tone(core.Secondary, 30), OnSecondary = Tone(core.Secondary, 90), Error = Tone(core.Error, 80), Success = Default.Success
            };
        }
        internal static string Hex(Color color) { return "#" + (color.ToArgb() & 0xffffff).ToString("X6", CultureInfo.InvariantCulture); }
        internal static Color Parse(string value)
        {
            int rgb;
            return value != null && value.Length == 7 && value[0] == '#' && int.TryParse(value.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out rgb) ? Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255) : Color.FromArgb(103,80,164);
        }
    }
}
