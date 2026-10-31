using WinDynamicIsland.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using Microsoft.Win32;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Runtime.InteropServices;



namespace WinDynamicIsland
{
    internal sealed class DesktopNotifications : IDisposable
    {
        internal const string AppId = "TelegramMessengerLLP.TelegramDesktop_t4vj0pshhgkwm!Telegram.TelegramDesktop.Store";
        internal static readonly string[] CodexAppIds = { "com.openai.codex", "OpenAI.Codex_2p2nqsd0c76g0!App" };
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        internal static bool IsCodexForeground()
        {
            try
            {
                uint id;
                GetWindowThreadProcessId(GetForegroundWindow(), out id);
                using (var process = Process.GetProcessById((int)id))
                    return string.Equals(process.ProcessName, "Codex", StringComparison.OrdinalIgnoreCase) || string.Equals(process.ProcessName, "ChatGPT", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }
        readonly NotificationApp app;
        readonly string logName;
        volatile bool stopping;
        readonly Thread worker;
        readonly HashSet<string> seen = new HashSet<string>();
        readonly Queue<string> seenOrder = new Queue<string>();
        int shown;
        internal static string DisabledPath(NotificationApp app)
        {
            return Path.Combine(Program.DataPath, app.ToString().ToLowerInvariant() + "-disabled.flag");
        }
        static string BackupPath(NotificationApp app, string id)
        {
            return Path.Combine(Program.DataPath, app == NotificationApp.Telegram && id == AppId ? "telegram-banner-backup.json" : app.ToString().ToLowerInvariant() + "-" + id + "-banner-backup.json");
        }
        static string[] AppIds(NotificationApp app)
        {
            var ids = new List<string>(app == NotificationApp.Telegram ? new[] { AppId, "TelegramDesktop", "Telegram" } : app == NotificationApp.Codex ? CodexAppIds : new[] { "Windows.SystemToast.Power" });
            if (app != NotificationApp.System)
                try
                {
                    using (var key = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Notifications\\Settings"))
                    {
                        if (key != null)
                            ids.AddRange(key.GetSubKeyNames().Where(id => id.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && (app == NotificationApp.Telegram ? id.IndexOf("Telegram", StringComparison.OrdinalIgnoreCase) >= 0 : id.StartsWith("OpenAI.Codex_", StringComparison.OrdinalIgnoreCase))));
                    }
                }
                catch { }
            return ids.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }
        internal DesktopNotifications(NotificationApp app)
        {
            this.app = app;
            logName = app.ToString().ToLowerInvariant() + "-listener.txt";
            worker = new Thread(Run) { IsBackground = true, Name = app + " notification bridge" };
            worker.Start();
        }
        void Run()
        {
            int failures = 0;
            bool initialized = false;
            while (!stopping)
            {
                if (File.Exists(DisabledPath(app)))
                {
                    initialized = false;
                    Thread.Sleep(350);
                    continue;
                }
                try
                {
                    if (!initialized)
                    {
                        foreach (var row in Snapshot())
                            Remember(Identity(row));
                        initialized = true;
                        if (Program.Settings.SuppressWindowsBanners)
                            EnableIslandBanners(app);
                        else
                            RestoreBanners(app);
                        Program.Log(logName, "running; new " + app + " notifications only; displayed=" + shown);
                    }
                    var rows = Snapshot();
                    foreach (var row in rows)
                    {
                        string identity = Identity(row);
                        if (seen.Contains(identity))
                            continue;
                        if (app == NotificationApp.Codex && Program.Settings.CodexBackgroundOnly && IsCodexForeground())
                        {
                            Remember(identity);
                            Program.Log(logName, "running; foreground Codex notification skipped; displayed=" + shown);
                            continue;
                        }
                        string title, body, avatarPath;
                        if (Parse(row["Payload"], out title, out body, out avatarPath))
                        {
                            if (app == NotificationApp.System && BatteryAlerts.CurrentPercent >= 0 && BatteryAlerts.CurrentPercent <= 20)
                            {
                                BatteryAlerts.ShowWarning(BatteryAlerts.CurrentPercent);
                                Remember(identity);
                                continue;
                            }
                            Bitmap avatar = LoadAvatar(app == NotificationApp.Codex ? Path.Combine(Program.DataPath, "codex-icon.png") : avatarPath);
                            bool hasAvatar = avatar != null;
                            Program.Presenter.Notify(app == NotificationApp.Codex ? "Codex · " + title : title, body, app, avatar, delegate
                            {
                                int count = Interlocked.Increment(ref shown);
                                Program.Log(logName, "running; presented=" + count + "; avatar=" + hasAvatar + "; last presentation=" + DateTime.Now.ToString("s"));
                            });
                        }
                        else
                            Program.Log(logName, "running; unsupported notification payload; displayed=" + shown);
                        Remember(identity);
                    }
                    if (failures >= 3)
                        EnableIslandBanners(app);
                    failures = 0;
                }
                catch
                {
                    failures++;
                    if (failures == 3)
                    {
                        Program.Log(logName, "notification store unavailable; normal Windows banners restored");
                        RestoreBanners(app);
                    }
                }
                for (int i = 0; i < 3 && !stopping; i++)
                    Thread.Sleep(50);
            }
        }
        List<Dictionary<string, string>> Snapshot()
        {
            string filter = " FROM Notification n JOIN NotificationHandler h ON n.HandlerId=h.RecordId WHERE h.PrimaryId IN (" + string.Join(",", AppIds(app).Select(id => "'" + id.Replace("'", "''") + "'")) + ")";
            return NotificationStore.Query("SELECT n.Id,n.Payload,n.ArrivalTime" + filter + " ORDER BY n.ArrivalTime DESC LIMIT 100").AsEnumerable().Reverse().ToList();
        }
        void Remember(string id)
        {
            if (!seen.Add(id))
                return;
            seenOrder.Enqueue(id);
            while (seenOrder.Count > 4096)
                seen.Remove(seenOrder.Dequeue());
        }
        internal static string Identity(Dictionary<string, string> row)
        {
            using (var sha = SHA256.Create())
                return row["Id"] + ":" + row["ArrivalTime"] + ":" + Convert.ToBase64String(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(row["Payload"])));
        }
        internal static bool Parse(string payload, out string title, out string body)
        {
            string avatar;
            return Parse(payload, out title, out body, out avatar);
        }
        internal static bool Parse(string payload, out string title, out string body, out string avatar)
        {
            title = body = avatar = "";
            if (string.IsNullOrEmpty(payload) || payload.Length > 1024 * 1024)
                return false;
            try
            {
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
                var xml = new XmlDocument { XmlResolver = null };
                using (var input = new StringReader(payload.TrimStart('\uFEFF')))
                using (var reader = XmlReader.Create(input, settings))
                    xml.Load(reader);
                if (xml.DocumentElement == null || xml.DocumentElement.Name != "toast")
                    return false;
                var texts = xml.SelectNodes("/toast/visual/binding/text").Cast<XmlNode>().Select(n => Clean(n.InnerText, 600)).Where(s => s.Length > 0).ToArray();
                if (texts.Length == 0)
                    return false;
                title = Clean(texts[0], 120);
                body = texts.Length > 1 ? Clean(string.Join("\n", texts.Skip(1)), 600) : "Новое уведомление";
                var image = xml.SelectSingleNode("/toast/visual/binding/image[@placement='appLogoOverride']/@src");
                if (image != null)
                    avatar = image.Value;
                return true;
            }
            catch { return false; }
        }
        internal static Bitmap LoadAvatar(string source)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(source))
                    return null;
                string path = source;
                Uri uri;
                if (Uri.TryCreate(source, UriKind.Absolute, out uri))
                {
                    if (!uri.IsFile || uri.IsUnc)
                        return null;
                    path = uri.LocalPath;
                }
                path = Path.GetFullPath(path);
                if (path.StartsWith(@"\\"))
                    return null;
                string userRoot = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd('\\') + "\\";
                if (!path.StartsWith(userRoot, StringComparison.OrdinalIgnoreCase))
                    return null;
                var file = new FileInfo(path);
                if (!file.Exists || file.Length > 5 * 1024 * 1024)
                    return null;
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var image = Image.FromStream(stream))
                {
                    if (image.Width > 4096 || image.Height > 4096)
                        return null;
                    var avatar = new Bitmap(64, 64, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                    using (var g = Graphics.FromImage(avatar))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        int side = Math.Min(image.Width, image.Height);
                        g.DrawImage(image, new Rectangle(0, 0, 64, 64), (image.Width - side) / 2, (image.Height - side) / 2, side, side, GraphicsUnit.Pixel);
                    }
                    return avatar;
                }
            }
            catch { return null; }
        }
        static string Clean(string text, int max)
        {
            string clean = new string(text.Where(c => !char.IsControl(c) || c == '\n').ToArray()).Trim();
            return clean.Length > max ? clean.Substring(0, max) + "…" : clean;
        }
        internal static void RestoreBanners(NotificationApp app)
        {
            foreach (string id in AppIds(app))
            {
                try
                {
                    string backupPath = BackupPath(app, id);
                    if (!File.Exists(backupPath))
                        continue;
                    var saved = JsonCodec.Create().Deserialize<Dictionary<string, object>>(File.ReadAllText(backupPath));
                    using (var key = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Notifications\\Settings\\" + id))
                    {
                        if (saved["showBanner"] == null)
                            key.DeleteValue("ShowBanner", false);
                        else
                            key.SetValue("ShowBanner", Convert.ToInt32(saved["showBanner"]), RegistryValueKind.DWord);
                        if (saved.ContainsKey("showInActionCenter"))
                        {
                            if (saved["showInActionCenter"] == null)
                                key.DeleteValue("ShowInActionCenter", false);
                            else
                                key.SetValue("ShowInActionCenter", Convert.ToInt32(saved["showInActionCenter"]), RegistryValueKind.DWord);
                        }
                    }
                }
                catch { }
            }
        }
        internal static void EnableIslandBanners(NotificationApp app)
        {
            if (File.Exists(DisabledPath(app)))
                return;
            foreach (string id in AppIds(app))
            {
                string backupPath = BackupPath(app, id);
                using (var key = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Notifications\\Settings\\" + id))
                {
                    var backup = File.Exists(backupPath) ? JsonCodec.Create().Deserialize<Dictionary<string, object>>(File.ReadAllText(backupPath)) : new Dictionary<string, object> { { "showBanner", key.GetValue("ShowBanner") } };
                    if (!backup.ContainsKey("showInActionCenter"))
                        backup["showInActionCenter"] = key.GetValue("ShowInActionCenter");
                    File.WriteAllText(backupPath, JsonCodec.Create().Serialize(backup));
                    key.SetValue("ShowBanner", 0, RegistryValueKind.DWord);
                    key.SetValue("ShowInActionCenter", 1, RegistryValueKind.DWord);
                }
            }
        }
        public void Dispose()
        {
            stopping = true;
            worker.Join(1500);
            RestoreBanners(app);
        }
    }

}
