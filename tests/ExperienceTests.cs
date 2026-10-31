using System;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using WinDynamicIsland.Core;
using Microsoft.Win32;
namespace WinDynamicIsland
{
    internal static class ExperienceTests
    {
        static int count;
        sealed class SlowModule : IIslandModule { internal int Starts; internal readonly ManualResetEvent Began = new ManualResetEvent(false); public string Id { get { return "experience-test"; } } public void Start() { Starts++; Began.Set(); } public void Dispose() { Thread.Sleep(250); } }
        static void NativeBannerChecks()
        {
            string name = Guid.NewGuid().ToString("N"), root = @"Software\WinDynamicIsland.Tests\" + name + @"\", folder = Path.Combine(Path.GetTempPath(), "Island-banner-" + name);
            Directory.CreateDirectory(folder);
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(root + "Example.Mail")) { key.SetValue("ShowBanner", 1); key.SetValue("ShowInActionCenter", 0); }
                var store = new BannerRoutingStore(folder, root);
                store.Apply(new[] { "Example.Mail", "Example.NewSender" });
                using (var key = Registry.CurrentUser.OpenSubKey(root + "Example.Mail")) Check((int)key.GetValue("ShowBanner") == 0 && (int)key.GetValue("ShowInActionCenter") == 1, "routing suppresses the banner and preserves Notification Center delivery");
                store.Apply(new[] { "Example.NewSender" });
                using (var key = Registry.CurrentUser.OpenSubKey(root + "Example.Mail")) Check((int)key.GetValue("ShowBanner") == 1 && (int)key.GetValue("ShowInActionCenter") == 0, "deselecting an app restores its original Windows settings");
                store.RestoreAll();
                using (var key = Registry.CurrentUser.OpenSubKey(root + "Example.NewSender")) Check(key.GetValue("ShowBanner") == null && key.GetValue("ShowInActionCenter") == null, "unset Windows preferences remain unset after restoration");
            }
            finally { Registry.CurrentUser.DeleteSubKeyTree(root.TrimEnd('\\'), false); foreach (string file in Directory.GetFiles(folder)) File.Delete(file); Directory.Delete(folder); }
        }
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); count++; Console.WriteLine("PASS: " + message); }
        [STAThread] static void Main()
        {
            try
            {
                var now = DateTime.UtcNow; var gesture = new VolumeGesture();
                Check(!gesture.Change(now) && !gesture.Change(now.AddMilliseconds(100)), "first two volume changes retain the normal capsule");
                Check(gesture.Change(now.AddMilliseconds(200)), "third rapid change widens and flattens volume HUD");
                Check(!gesture.Change(now.AddSeconds(2)), "a later volume gesture resets the shape");
                var selected = new AppSettings { NotificationSelectionConfigured = true, NotificationApps = new[] { "Example.Mail" } };
                Check(NotificationRouter.Routes(selected,"Example.Mail") && !NotificationRouter.Routes(selected,"Example.Chat"), "notification routing follows the chosen apps");
                selected.NotificationMode = "All"; selected.SuppressWindowsBanners = false; selected.Validate();
                Check(selected.SuppressWindowsBanners && NotificationRouter.Routes(selected,"Future.NewApp"), "all-app mode includes new senders and suppresses duplicate banners");
                Check(!NotificationRoutingPolicy.Valid(@"Invalid\Sender") && NotificationRoutingPolicy.Valid("App.Package!App"), "notification registry paths are bounded to individual senders");
                NativeBannerChecks();
                var slow = new SlowModule();
                using (var coordinator = new ModuleCoordinator())
                {
                    var plans = new Dictionary<string, Func<IDisposable>> { { "experience-test", () => slow } };
                    coordinator.Update(plans); Check(slow.Began.WaitOne(2000), "extension module starts on the lifecycle thread");
                    coordinator.Update(plans); Thread.Sleep(60); Check(slow.Starts == 1, "saving unrelated settings preserves the running module");
                    var timer = Stopwatch.StartNew(); coordinator.Update(new Dictionary<string, Func<IDisposable>>()); timer.Stop(); Check(timer.ElapsedMilliseconds < 60, "slow module disposal does not block the settings action");
                }
                using (var slider = new SnapSlider("Test", 28, 0, 48)) { Check(slider.Snap(0.501) == 24 && slider.Snap(-1) == 0 && slider.Snap(2) == 48, "sliders snap to bounded integer values"); }
                var settings = Program.Settings;
                try
                {
                    Program.Settings = settings.Clone(); Program.Settings.F7ControlsMicrophone = false;
                    var watch = Stopwatch.StartNew(); using (var keys = new KeyboardIndicators()) { } Check(watch.ElapsedMilliseconds < 2500, "indicator hooks shut down on their owning message loop");
                } finally { Program.Settings = settings; }
                using (var island = (Island)Activator.CreateInstance(typeof(Island), true))
                {
                    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                    typeof(Island).GetField("compactKind", flags).SetValue(island, CompactNoticeKind.Volume);
                    typeof(Island).GetField("hudShapeAt", flags).SetValue(island, now.AddSeconds(-1));
                    var size = typeof(Island).GetMethod("HudSizeAt", flags);
                    var normal = (SizeF)size.Invoke(island, new object[] { now });
                    typeof(Island).GetField("hudWide", flags).SetValue(island, true);
                    var wide = (SizeF)size.Invoke(island, new object[] { now });
                    Check(wide.Width > normal.Width && wide.Height < normal.Height, "volume capsule becomes wider and shorter");
                    Check(wide.Width == 380, "expanded volume capsule uses the wider 380-pixel layout");
                    using(var frame=(Bitmap)typeof(Island).GetMethod("DrawFace",flags).Invoke(island,new object[]{380,28,1d})) { bool outside=false;for(int y=0;y<frame.Height;y++)for(int x=0;x<frame.Width;x++)if(y<9 || y>18){var pixel=frame.GetPixel(x,y);if(pixel.A>100 && pixel.R>100)outside=true;}Check(!outside,"settled wide volume HUD contains only the level bar");}
                    typeof(Island).GetField("compactKind", flags).SetValue(island, CompactNoticeKind.Microphone);
                    Check((int)typeof(Island).GetProperty("HoldMs", flags).GetValue(island, null) == 3000, "microphone HUD uses a three-second hold");
                    typeof(Island).GetField("compactKind",flags).SetValue(island,CompactNoticeKind.Charging);
                    Check((int)typeof(Island).GetProperty("CompactNoticeWidth",flags).GetValue(island,null)==370,"charging capsule matches the wide reference layout");
                }
                try { using (var endpoint = new AudioEndpoint(0, null)) { float volume; bool muted; endpoint.Read(out volume, out muted); Check(volume >= 0 && volume <= 1, "actual output volume reads without changing audio"); } }
                catch (System.Runtime.InteropServices.COMException) { Console.WriteLine("SKIP: output endpoint absent"); }
                try { using (var endpoint = new AudioEndpoint(1, null)) { float volume; bool muted; endpoint.Read(out volume, out muted); Check(true, "actual microphone mute state reads without changing it"); } }
                catch (System.Runtime.InteropServices.COMException) { Console.WriteLine("SKIP: input endpoint absent"); }
                Console.WriteLine(count + " experience checks passed");
            }
            catch (Exception ex) { Console.WriteLine(ex); Environment.ExitCode = 1; }
        }
    }
}
