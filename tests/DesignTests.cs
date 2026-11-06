using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using WinDynamicIsland.Core;
namespace WinDynamicIsland
{
    internal static class DesignTests
    {
        static readonly BindingFlags Instance = BindingFlags.NonPublic | BindingFlags.Instance;
        static int checks;
        static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; Console.WriteLine("PASS: " + message); }
        static T Field<T>(object owner, string name) { return (T)owner.GetType().GetField(name, Instance).GetValue(owner); }
        static void Page(SettingsForm form, string key) { typeof(SettingsForm).GetMethod("ShowSection", Instance).Invoke(form, new object[] { key }); Application.DoEvents(); }
        static void Set(object owner, string name, object value) { owner.GetType().GetField(name, Instance).SetValue(owner, value); }
        static void Settle() { var clock = Stopwatch.StartNew(); while (clock.ElapsedMilliseconds < 370) { Application.DoEvents(); Thread.Sleep(8); } }
        [STAThread] static void Main()
        {
            var original = Program.Settings;
            string language = L.Language;
            string folder = Path.Combine(Path.GetTempPath(), "island-design-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(folder);
                var store = new SettingsStore(Path.Combine(folder, "settings.json"));
                File.WriteAllText(Path.Combine(folder, "settings.json"), "{\"Language\":\"ru\",\"VpnEnabled\":true,\"VpnProvider\":\"Sota\"}");
                var legacy = store.Load();
                Check(legacy.InterfaceDesign == "Classic" && legacy.VpnEnabled, "older settings retain the original design and VPN configuration");
                legacy.InterfaceDesign = "Material"; store.Save(legacy);
                Check(store.Load().InterfaceDesign == "Material" && legacy.Clone().InterfaceDesign == "Material", "design survives a fresh store and settings cloning");
                legacy.InterfaceDesign = "unrecognized"; legacy.Validate(); Check(legacy.InterfaceDesign == "Classic", "unknown design safely falls back to the original");
                Application.EnableVisualStyles();
                Program.Settings = new AppSettings { VpnEnabled = true, VpnProvider = "Sota", VpnName = "Fixture VPN", InterfaceAnimations = true };
                AppDesign.Change(false, false);
                using (var form = new SettingsForm(next => { Program.Settings = next; store.Save(next); AppDesign.Change(next.InterfaceDesign == "Material", true); }))
                {
                    form.Show(); Application.DoEvents();
                    var navigation = Field<AnimatedNavigationPanel>(form, "navigation");
                    var buttons = navigation.Controls.OfType<GlassButton>().ToArray();
                    Page(form, "VPN");
                    var body = Field<IslandScrollPanel>(form, "body");
                    body.Controls.OfType<Panel>().SelectMany(p => p.Controls.OfType<TextBox>()).First().Text = "Unsaved VPN name";
                    form.SwitchDesign("Material"); Settle();
                    Check(Program.Settings.InterfaceDesign == "Material" && store.Load().InterfaceDesign == "Material", "theme switch applies and persists immediately");
                    Check(Program.Settings.VpnEnabled && Program.Settings.VpnProvider == "Sota" && Program.Settings.VpnName == "Unsaved VPN name", "theme switch preserves active provider and pending form edits");
                    Check(form.Font.Name == "Google Sans", "Material settings use embedded Google Sans instead of a substitute");
                    using (var font = AppTypography.Symbol(24)) Check(font.Name == "Material Icons", "Material glyphs use the embedded Google icon font");
                    Check(buttons.SequenceEqual(navigation.Controls.OfType<GlassButton>()) && buttons.All(b => !b.IsDisposed), "switching design preserves live navigation controls");
                    Check(!Field<PageTransitionSurface>(form, "designTransition").Visible, "seamless theme overlay releases the interface after settling");
                    form.ClientSize = new Size(860, 660);
                    foreach (string page in new[] { "General", "Appearance", "Focus", "Media", "Notifications", "Devices", "VPN", "About" })
                    {
                        Page(form, page); body = Field<IslandScrollPanel>(form, "body");
                        Check(body.Controls.Count > 0 && body.Controls.Cast<Control>().All(c => c.Right <= body.ClientSize.Width), "Material " + page + " fits the minimum supported window");
                        Check(body.BackColor == AppDesign.Surface, "Material " + page + " uses a consistent surface");
                        Check(body.Controls.OfType<Panel>().SelectMany(p => p.Controls.OfType<SelectionButton>()).All(c => c.Left >= 0 && c.Right < c.Parent.Width), page + " dropdowns do not overflow");
                    }
                    Page(form, "Appearance");
                    for (int i = 0; i < 6; i++) form.SwitchDesign(i % 2 == 0 ? "Classic" : "Material");
                    Settle(); Check(Program.Settings.InterfaceDesign == "Material", "rapid repeated switching settles on the last design");
                    form.SwitchDesign("Classic"); Settle();
                    Check(form.Font.Name.StartsWith("Inter", StringComparison.Ordinal) && form.BackColor == Color.Black, "original typography and black surfaces are restored");
                    Field<IslandScrollPanel>(form, "body").Controls.OfType<ToggleSwitch>().Single(c => c.Text == L.T("InterfaceMotion")).Checked = false;
                    form.SwitchDesign("Material");
                    Check(!Field<PageTransitionSurface>(form, "designTransition").Visible, "disabled animations switch design immediately");
                    form.Close(); Application.DoEvents(); Check(!form.IsDisposed && store.Load().InterfaceDesign == "Material", "closing keeps the tray application and chosen design");
                    form.CloseForExit();
                }
                Program.Settings = new SettingsStore(Path.Combine(folder, "settings.json")).Load();
                AppDesign.Change(Program.Settings.InterfaceDesign == "Material", false);
                using (var reopened = new SettingsForm(null))
                {
                    reopened.Show(); Application.DoEvents(); Page(reopened, "Appearance");
                    Check(Program.Settings.InterfaceDesign == "Material" && AppDesign.Material && reopened.Font.Name == "Google Sans", "a fresh application lifetime restores Material and its typography from disk");
                    Check(Field<IslandScrollPanel>(reopened, "body").Controls.OfType<GlassCard>().First().Controls.OfType<GlassButton>().Single(c => c.Text == "Material 3 Expressive").Selected, "reopened settings mark the saved Material design as selected");
                    reopened.CloseForExit();
                }
                Program.Settings = new AppSettings { InterfaceDesign = "Material" }; AppDesign.Change(true, false);
                using (var island = (Island)Activator.CreateInstance(typeof(Island), true))
                {
                    Field<System.Windows.Forms.Timer>(island, "timer").Stop();
                    string command = null; double position = -1;
                    var data = new MediaSnapshot { Id = "fixture", Title = "Test track", Artist = "Test playlist", Playing = true, CanToggle = true, CanSeek = true, End = 200, Position = 50, Command = (name, value) => { command = name; position = value; } };
                    Set(island, "mediaData", data); Field<MediaPresentation>(island, "mediaPresentation").Accept(data, DateTime.UtcNow);
                    var mouse = typeof(Island).GetMethod("MaterialMediaMouseDown", Instance);
                    mouse.Invoke(island, new object[] { new PointF(208, 158) }); Check(command == "toggle", "Material pause button invokes the actual media command");
                    mouse.Invoke(island, new object[] { new PointF(54, 158) }); Check(command == "seek" && position < 45, "Material rewind button seeks backwards");
                    mouse.Invoke(island, new object[] { new PointF(362, 158) }); Check(command == "seek" && position > 45, "Material forward button seeks forwards");
                    mouse.Invoke(island, new object[] { new PointF(300, 98) }); Check(Field<bool>(island, "mediaDragging") && Field<double>(island, "mediaDragPosition") > 140, "Material timeline begins a real manual seek");
                    Set(island, "mediaDragging", false); island.Capture = false;
                    using (var frame = (Bitmap)typeof(Island).GetMethod("DrawMediaFace", Instance).Invoke(island, new object[] { 416, 200, 1d }))
                        Check(frame.GetPixel(208, 15).ToArgb() == AppDesign.Container.ToArgb(), "Material media renderer uses the shared tonal surface");
                }
                double peak = Enumerable.Range(0, 101).Max(i => AppDesign.Spatial(i / 100d));
                Check(AppDesign.Spatial(0) == 0 && AppDesign.Spatial(1) == 1 && peak > 1 && peak < 1.1, "expressive spring has stable endpoints and a bounded overshoot");
                Console.WriteLine(checks + " design checks passed; no network or VPN commands were run.");
            }
            catch (Exception ex) { Console.WriteLine(ex); Environment.ExitCode = 1; }
            finally { Program.Settings = original; AppDesign.Change(original.InterfaceDesign == "Material", false); L.Language = language; if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        }
    }
}
