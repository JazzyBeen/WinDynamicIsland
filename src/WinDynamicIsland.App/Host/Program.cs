using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using WinDynamicIsland.Core;

[assembly: System.Reflection.AssemblyTitle("Win Dynamic Island")]
[assembly: System.Reflection.AssemblyVersion("2.3.1.0")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("WinDynamicIsland.Tests")]
namespace WinDynamicIsland
{
    internal static class Program
    {
        internal const string Version = "2.3.1";
        internal static readonly string DataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinDynamicIsland");
        internal static readonly SettingsStore Store = new SettingsStore(Path.Combine(DataPath, "settings.json"));
        internal static AppSettings Settings = new AppSettings();
        internal static IIslandPresenter Presenter = new IslandPresenter();
        internal static Screen TargetScreen
        {
            get
            {
                var screens = Screen.AllScreens;
                return Settings.MonitorIndex < screens.Length ? screens[Settings.MonitorIndex] : Screen.PrimaryScreen;
            }
        }
        internal static void Log(string name, string message)
        {
            try
            {
                Directory.CreateDirectory(DataPath);
                File.WriteAllText(Path.Combine(DataPath, name), message);
            }
            catch { }
        }
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr handle);
        [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr handle, int command);
        [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr context);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern int SetCurrentProcessExplicitAppUserModelID(string id);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string className, string title);
        [DllImport("user32.dll")] static extern bool PostMessage(IntPtr handle, uint message, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr handle, out uint id);
        [DllImport("user32.dll")] static extern bool AllowSetForegroundWindow(uint id);
        [STAThread]
        static void Main(string[] args)
        {
            Directory.CreateDirectory(DataPath);
            Settings = Store.Load();
            if (args.Length > 0 && args[0].StartsWith("--render-", StringComparison.Ordinal)) Settings = new AppSettings();
            if (Array.IndexOf(args, "--material") >= 0) Settings.InterfaceDesign = "Material";
            AppDesign.Change(Settings.InterfaceDesign == "Material", false);
            AppDesign.ConfigurePalette();
            L.Language = Settings.Language;
            try
            {
                SetProcessDpiAwarenessContext(new IntPtr(-4));
            }
            catch { }
            SetCurrentProcessExplicitAppUserModelID("WinDynamicIsland.Desktop");
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length >= 2 && args[0] == "--render-focus") { Island.ExportFocusPreview(args[1]); return; }
            if (args.Length > 0 && args[0] == "--capture-profile") { CaptureWorker.Run(args.Length > 1 ? args[1] : "ru"); return; }
            if (args.Length == 3 && args[0] == "--capture-watch") { try { Process.GetProcessById(int.Parse(args[1])).WaitForExit(); } catch { } try { Recovery.Restore(args[2]); } catch { } return; }
            if (args.Length > 0 && args[0] == "--check")
            {
                Log("health.txt", "Version=" + Version + "; Design=" + Settings.InterfaceDesign + "; Startup=" + StartupRegistration.IsEnabled + "; SotaProfile=" + File.Exists(ApiProfile.PathName));
                return;
            }
            if (args.Length > 0 && args[0] == "--check-api")
            {
                try
                {
                    bool profile = false;
                    string issue = "none";
                    try
                    {
                        ApiProfile.Load();
                        profile = true;
                    }
                    catch (Exception ex) { issue = ex.GetType().Name; }
                    Log("api-check.txt", "Backend=" + new SotaVpnProvider().GetState() + "; ProfileValid=" + profile + "; ProfileIssue=" + issue);
                }
                catch { Environment.ExitCode = 1; }
                return;
            }
            if (args.Length >= 2 && args[0] == "--render-features")
            {
                Island.ExportFeaturePreview(args[1]);
                return;
            }
            if (args.Length >= 2 && args[0] == "--render-huds")
            {
                Island.ExportHudPreview(args[1]);
                return;
            }
            if (args.Length >= 2 && args[0] == "--render-motion-frames")
            {
                Island.ExportMotionPreview(args[1]);
                return;
            }
            if (args.Length >= 2 && args[0] == "--render-settings")
            {
                try
                {
                    NotificationCatalog.Refresh();
                }
                catch { }
                if (args.Length >= 3 && (args[2] == "ru" || args[2] == "en"))
                {
                    Settings.Language = args[2];
                    L.Language = args[2];
                }
                using (var form = new SettingsForm(null))
                {
                    form.Export(args[1]);
                }
                return;
            }
            if (args.Length >= 2 && args[0] == "--render-navigation")
            {
                using (var form = new SettingsForm(null))
                    form.ExportNavigationPreview(args[1]);
                return;
            }
            string action = args.Length == 0 ? "--settings" : args[0];
            if (action != "--settings" && action != "--background" && action != "--toggle-vpn" && action != "--exit")
            {
                MessageBox.Show("Unknown argument: " + action, "Win Dynamic Island");
                Environment.ExitCode = 2;
                return;
            }
            bool created;
            using (var mutex = new Mutex(true, "Local\\WinDynamicIsland.Host", out created))
            {
                if (!created)
                {
                    var handle = FindWindow(null, "WinDynamicIsland.Host");
                    Log("command.txt", "Action=" + action + "; ListenerFound=" + (handle != IntPtr.Zero));
                    if (handle != IntPtr.Zero)
                    {
                        uint id;
                        GetWindowThreadProcessId(handle, out id);
                        AllowSetForegroundWindow(id);
                        PostMessage(handle, HostWindow.CommandMessage, new IntPtr(action == "--toggle-vpn" ? 2 : action == "--exit" ? 3 : action == "--background" ? 0 : 1), IntPtr.Zero);
                    }
                    return;
                }
                if (action == "--exit")
                    return;
                try
                {
                    using (var host = new AppHost())
                    {
                        host.Start(action);
                        Application.Run(host);
                    }
                }
                catch (Exception ex) { Log("startup-error.txt", ex.ToString()); try { BatteryBannerPolicy.Restore(); foreach (NotificationApp app in Enum.GetValues(typeof(NotificationApp))) DesktopNotifications.RestoreBanners(app); } catch { } MessageBox.Show(ex.Message, "Win Dynamic Island"); Environment.ExitCode = 1; }
            }
        }
    }
    internal sealed class IslandPresenter : IIslandPresenter
    {
        public void Status(string title, string message, bool complete, bool error)
        {
            Island.Show(title, message, complete, error);
        }
        public void Notify(string title, string message, NotificationApp app, System.Drawing.Bitmap avatar, Action presented)
        {
            Island.ShowNotification(title, message, app, avatar, presented);
        }
        public void Compact(CompactNoticeKind kind, string message, int value)
        {
            Island.ShowCompact(kind, message, value);
        }
        public void Media(MediaSnapshot snapshot)
        {
            Island.SetMedia(snapshot);
        }
    }
}
