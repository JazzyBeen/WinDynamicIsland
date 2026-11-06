using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using WinDynamicIsland.Core;
namespace WinDynamicIsland
{
    internal static class PaletteTests
    {
        static void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS: " + message); }
        static double Linear(byte c) { double v = c / 255d; return v <= .04045 ? v / 12.92 : Math.Pow((v + .055) / 1.055, 2.4); }
        static double Luminance(Color c) { return .2126 * Linear(c.R) + .7152 * Linear(c.G) + .0722 * Linear(c.B); }
        static double Contrast(Color a, Color b) { double x = Luminance(a), y = Luminance(b); return (Math.Max(x,y)+.05)/(Math.Min(x,y)+.05); }
        [STAThread] static void Main()
        {
            var previous = Program.Settings;
            string folder = Path.Combine(Path.GetTempPath(), "island-palette-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(folder);
                foreach (string hex in new[] { "#000000", "#FFFFFF", "#777777", "#FF0000", "#00FF00", "#0000FF", "#00FFFF", "#FF00FF", "#FFFF00", "#6750A4", "#006C4C", "#805600" })
                {
                    var palette = MaterialPalette.FromSeed(MaterialPalette.Parse(hex));
                    Check(Contrast(palette.Primary,palette.OnPrimary) >= 4.5 && Contrast(palette.PrimaryContainer,palette.OnContainer) >= 4.5 && Contrast(palette.Container,palette.Ink) >= 4.5 && Contrast(palette.Secondary,palette.OnSecondary) >= 4.5 && Contrast(palette.Highest,palette.Muted) >= 4.5, "HCT roles retain readable contrast for " + hex);
                }
                var store = new SettingsStore(Path.Combine(folder, "settings.json"));
                var value = new AppSettings { InterfaceDesign = "Material", MaterialColorMode = "Custom", MaterialSeedColor = "#006c4c" };
                store.Save(value); Check(store.Load().MaterialSeedColor == "#006C4C" && store.Load().MaterialColorMode == "Custom", "custom color persists and normalizes correctly");
                value.MaterialColorMode = "Wallpaper"; store.Save(value); Check(store.Load().MaterialColorMode == "Wallpaper" && store.Load().MaterialSeedColor == "#006C4C", "wallpaper mode keeps the custom color for later");
                value.MaterialSeedColor = "invalid"; value.MaterialColorMode = "invalid"; value.Validate(); Check(value.MaterialSeedColor == "#6750A4" && value.MaterialColorMode == "Default", "invalid color preferences recover safely");
                string image = Path.Combine(folder,"wallpaper.png");
                using (var bitmap = new Bitmap(100,100)) { using (var g = Graphics.FromImage(bitmap)) g.Clear(Color.FromArgb(0,160,100)); bitmap.Save(image,ImageFormat.Png); }
                var hash = File.ReadAllBytes(image);
                Color seed = WallpaperPaletteMonitor.Extract(image);
                Check(seed.G > seed.R && seed.G > seed.B && hash.SequenceEqual(File.ReadAllBytes(image)), "wallpaper extraction finds the image color without changing the file");
                string wallpaper = WallpaperPaletteMonitor.CurrentWallpaper();
                Check(wallpaper == "" || File.Exists(wallpaper), "Windows wallpaper lookup returns a readable image or solid-color fallback");
                using (var ready = new System.Threading.ManualResetEvent(false))
                using (var monitor = new WallpaperPaletteMonitor(color => ready.Set()))
                    Check(ready.WaitOne(6000), "automatic wallpaper monitoring publishes a real Windows palette");
                Program.Settings = new AppSettings { InterfaceDesign = "Material", MaterialColorMode = "Custom", MaterialSeedColor = "#006C4C", InterfaceAnimations = false }; AppDesign.Change(true,false); AppDesign.ConfigurePalette();
                Color primary = AppDesign.Primary;
                Program.Settings.MaterialSeedColor = "#AC3274"; AppDesign.ConfigurePalette(); Check(primary != AppDesign.Primary, "changing the source updates the shared app and island palette");
                Application.EnableVisualStyles();
                using (var picker = new ColorPickerDialog(Color.Blue))
                {
                    picker.Show(); Application.DoEvents();
                    var field = picker.Controls.OfType<TextBox>().Single(); field.Text = "#FF0080"; Application.DoEvents();
                    Check(picker.SelectedColor.R == 255 && picker.SelectedColor.G == 0 && picker.SelectedColor.B == 128, "HEX input updates the color and RGB sliders");
                    picker.Controls.OfType<SnapSlider>().First().Value = 120; Check(picker.SelectedColor.R == 120 && field.Text == "#780080", "color sliders update the preview and HEX input");
                    field.Text = "#ZZZZZZ"; Check(picker.SelectedColor.R == 120, "invalid input cannot corrupt the chosen color"); picker.Close();
                }
                using (var form = new SettingsForm(next => { Program.Settings = next; store.Save(next); AppDesign.ConfigurePalette(); }))
                {
                    form.Show(); Application.DoEvents();
                    typeof(SettingsForm).GetMethod("ShowSection",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(form,new object[]{"Appearance"});
                    var change = typeof(SettingsForm).GetMethod("ChangeColors",BindingFlags.NonPublic|BindingFlags.Instance);
                    change.Invoke(form,new object[]{"Custom","#386AFA"}); Check(store.Load().MaterialSeedColor == "#386AFA", "custom palette is applied and saved directly from settings");
                    Application.DoEvents();
                    using (var frame = new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(frame,new Rectangle(0,0,form.Width,form.Height)); frame.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"palette-settings-preview.png")); }
                    change.Invoke(form,new object[]{"Wallpaper","#386AFA"}); Check(store.Load().MaterialColorMode == "Wallpaper" && store.Load().MaterialSeedColor == "#386AFA", "automatic mode can be selected without losing the manual choice"); form.CloseForExit();
                }
                Console.WriteLine("Palette checks passed; wallpaper and VPN were not modified.");
            }
            catch (Exception ex) { Console.WriteLine(ex); Environment.ExitCode = 1; }
            finally { Program.Settings = previous; AppDesign.ConfigurePalette(); AppDesign.Change(previous.InterfaceDesign == "Material",false); if (Directory.Exists(folder)) Directory.Delete(folder,true); }
        }
    }
}
