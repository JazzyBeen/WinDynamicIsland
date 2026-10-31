using WinDynamicIsland.Core;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Web.Script.Serialization;
using System.Collections.Generic;
using Microsoft.Win32;
using System.Runtime.InteropServices;

// Independent of the island process so Windows warnings return if it exits unexpectedly.
namespace WinDynamicIsland
{
    internal static class Program
    {
        internal static readonly string DataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinDynamicIsland");
        internal static void Log(string file, string text)
        {
            try
            {
                File.WriteAllText(Path.Combine(DataPath, file), text);
            }
            catch { }
        }
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string className, string title);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint id);
        static bool MatchesOwner(int id, long started)
        {
            try
            {
                using (var process = Process.GetProcessById(id))
                    return process.StartTime.ToUniversalTime().Ticks == started && string.Equals(process.MainModule.FileName, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WinDynamicIsland.exe"), StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }
        static bool ReplacementRunning(int oldId)
        {
            try
            {
                uint id;
                var window = FindWindow(null, "WinDynamicIsland.Host");
                if (window == IntPtr.Zero)
                    return false;
                GetWindowThreadProcessId(window, out id);
                using (var process = Process.GetProcessById((int)id))
                    return process.Id != oldId && string.Equals(process.MainModule.FileName, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WinDynamicIsland.exe"), StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }
        static void Main(string[] args)
        {
            int owner;
            long started;
            if (args.Length != 2 || !int.TryParse(args[0], out owner) || !long.TryParse(args[1], out started))
                return;
            while (MatchesOwner(owner, started))
                Thread.Sleep(1000);
            Thread.Sleep(5000);
            if (ReplacementRunning(owner))
                return;
            try
            {
                BatteryBannerPolicy.Restore();
                RestoreBanners();
                NotificationRoutingPolicy.RestoreAll();
                Log("battery-guard.txt", "Island stopped; Windows notification banners restored");
            }
            catch (Exception ex) { Log("battery-guard.txt", ex.GetType().Name); }
        }
        static void RestoreBanners()
        {
            foreach (string path in Directory.GetFiles(DataPath, "*-banner-backup.json"))
            {
                string file = Path.GetFileName(path), id = null;
                if (file == "telegram-banner-backup.json")
                    id = "TelegramMessengerLLP.TelegramDesktop_t4vj0pshhgkwm!Telegram.TelegramDesktop.Store";
                else if (file.StartsWith("codex-"))
                    id = file.Substring(6, file.Length - 6 - "-banner-backup.json".Length);
                else if (file.StartsWith("system-"))
                    id = file.Substring(7, file.Length - 7 - "-banner-backup.json".Length);
                else if (file.StartsWith("telegram-"))
                    id = file.Substring(9, file.Length - 9 - "-banner-backup.json".Length);
                if (id == null || id.IndexOf('\\') >= 0 || id.IndexOf('/') >= 0)
                    continue;
                var saved = JsonCodec.Create().Deserialize<Dictionary<string, object>>(File.ReadAllText(path));
                using (var key = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Notifications\\Settings\\" + id))
                {
                    foreach (var entry in new[] { new[] { "showBanner", "ShowBanner" }, new[] { "showInActionCenter", "ShowInActionCenter" } })
                    {
                        if (!saved.ContainsKey(entry[0]))
                            continue;
                        if (saved[entry[0]] == null)
                            key.DeleteValue(entry[1], false);
                        else
                            key.SetValue(entry[1], Convert.ToInt32(saved[entry[0]]), RegistryValueKind.DWord);
                    }
                }
            }
        }
    }


}
