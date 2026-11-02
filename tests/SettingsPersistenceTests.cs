using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using WinDynamicIsland.Core;
namespace WinDynamicIsland
{
    internal static class SettingsPersistenceTests
    {
        static void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS: " + message); }
        [STAThread] static void Main()
        {
            string directory = Path.Combine(Path.GetTempPath(), "island-persistence-" + Guid.NewGuid().ToString("N"));
            var original = Program.Settings;
            try
            {
                Directory.CreateDirectory(directory);
                var store = new SettingsStore(Path.Combine(directory, "settings.json"));
                var settings = new AppSettings { VpnEnabled = true, VpnProvider = "Sota", FocusMinutes = 90, BreakMinutes = 20, FocusVolume = 42, FocusPlaylist = "Lofi", Language = "en", NotificationApps = new[] { "test-app" } };
                store.Save(settings); var restored = store.Load();
                foreach (var property in typeof(AppSettings).GetProperties())
                    Check(JsonCodec.Serialize(property.GetValue(settings, null)) == JsonCodec.Serialize(property.GetValue(restored, null)), property.Name + " survives a new settings store");
                store.Save(settings); File.WriteAllText(Path.Combine(directory, "settings.json"), "broken");
                Check(store.Load().VpnEnabled && store.Load().FocusMinutes == 90, "damaged primary file recovers VPN and focus settings from backup");
                Program.Settings = new AppSettings();
                Application.EnableVisualStyles();
                using (var form = new SettingsForm(value => { Program.Settings = value; store.Save(value); }))
                {
                    form.Show(); Application.DoEvents();
                    var body = (FlowLayoutPanel)typeof(SettingsForm).GetField("body", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
                    var toggle = body.Controls.OfType<ToggleSwitch>().First(); toggle.Checked = false;
                    var wait = System.Diagnostics.Stopwatch.StartNew();
                    while (wait.ElapsedMilliseconds < 650) { Application.DoEvents(); System.Threading.Thread.Sleep(10); }
                    Check(!store.Load().StartWithWindows, "settings persist automatically while the window stays open");
                    Check(form.Font.FontFamily.Name.StartsWith("Inter", StringComparison.Ordinal), "bundled Inter loads without system font installation");
                    form.Close(); Application.DoEvents();
                    Check(!store.Load().StartWithWindows && !form.IsDisposed, "closing settings flushes edits without Save and keeps tray UI alive");
                    form.Show(); form.CloseForExit();
                }
                var reopened = new SettingsStore(Path.Combine(directory, "settings.json")).Load();
                Check(!reopened.StartWithWindows, "a completely new store restores the edit after application exit");
            }
            catch (Exception ex) { Console.WriteLine(ex); Environment.ExitCode = 1; }
            finally { Program.Settings = original; if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }
    }
}
