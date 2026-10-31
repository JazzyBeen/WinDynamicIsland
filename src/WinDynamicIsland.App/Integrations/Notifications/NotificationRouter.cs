using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Win32;
using WinDynamicIsland.Core;
namespace WinDynamicIsland
{
    internal sealed class NotificationSource
    {
        internal string Id, Name;
        public override string ToString()
        {
            return Name;
        }
    }
    internal static class NotificationCatalog
    {
        static readonly object gate = new object();
        static NotificationSource[] items = new NotificationSource[0];
        internal static NotificationSource[] Snapshot()
        {
            lock (gate)
                return items;
        }
        internal static void Refresh()
        {
            SenderNames.EnsureLoaded();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in NotificationStore.Query("SELECT PrimaryId FROM NotificationHandler"))
                if (NotificationRoutingPolicy.Valid(row["PrimaryId"]))
                    ids.Add(row["PrimaryId"]);
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings"))
                if (key != null)
                    foreach (string id in key.GetSubKeyNames())
                        if (NotificationRoutingPolicy.Valid(id))
                            ids.Add(id);
            var next = ids.Select(id => new NotificationSource { Id = id, Name = Friendly(id) }).OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
            lock (gate)
                items = next;
        }
        internal static string Friendly(string id)
        {
            if (id.IndexOf("Telegram", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Telegram";
            if (id.IndexOf("Codex", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Codex";
            if (id == "Windows.SystemToast.Power")
                return L.T("Battery");
            string known = SenderNames.Find(id);
            if (!string.IsNullOrWhiteSpace(known))
                return known;
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\AppUserModelId\" + id))
            {
                string name = key == null ? null : SenderNames.Resolve(key.GetValue("DisplayName") as string);
                if (!string.IsNullOrWhiteSpace(name))
                    return name;
            }
            Guid guid;
            long number;
            if (Guid.TryParse(id, out guid) || long.TryParse(id, out number) || id.StartsWith("Windows.", StringComparison.OrdinalIgnoreCase))
                return L.T("WindowsSystem");
            string stem = id.Split('!')[0].Split('_')[0];
            int split = stem.LastIndexOf('.');
            return split >= 0 && split < stem.Length - 1 ? stem.Substring(split + 1) : stem;
        }
    }
    internal sealed class NotificationRouter : IDisposable
    {
        readonly ManualResetEvent stop = new ManualResetEvent(false);
        readonly Thread worker;
        readonly HashSet<string> seen = new HashSet<string>();
        readonly Queue<string> order = new Queue<string>();
        readonly Dictionary<string, string> payloads = new Dictionary<string, string>();
        int presented;
        internal NotificationRouter()
        {
            worker = new Thread(Run) { IsBackground = true, Name = "Island notification router" };
            worker.Start();
        }
        internal static bool Routes(AppSettings settings, string id)
        {
            if (settings.NotificationMode == "All")
                return true;
            if ((settings.NotificationApps ?? new string[0]).Contains(id, StringComparer.OrdinalIgnoreCase))
                return true;
            if (settings.NotificationSelectionConfigured)
                return id == "Windows.SystemToast.Power" && settings.BatteryEnabled;
            if (id.IndexOf("Telegram", StringComparison.OrdinalIgnoreCase) >= 0)
                return settings.TelegramEnabled;
            if (id.IndexOf("Codex", StringComparison.OrdinalIgnoreCase) >= 0)
                return settings.CodexEnabled;
            return id == "Windows.SystemToast.Power" && settings.BatteryEnabled;
        }
        void Remember(string identity)
        {
            if (!seen.Add(identity))
                return;
            order.Enqueue(identity);
            while (order.Count > 4096)
                seen.Remove(order.Dequeue());
        }
        void Run()
        {
            bool initialized = false;
            int failures = 0;
            DateTime refresh = DateTime.MinValue;
            long previousStamp = -1;
            string previousFilter = null;
            NotificationRoutingPolicy.RestoreAll();
            while (!stop.WaitOne(350))
                try
                {
                    var settings = Program.Settings;
                    string filter = Filter(settings);
                    if (filter != previousFilter)
                    {
                        initialized = false;
                        previousFilter = filter;
                    }
                    if (DateTime.UtcNow >= refresh)
                    {
                        NotificationCatalog.Refresh();
                        refresh = DateTime.UtcNow.AddSeconds(5);
                    }
                    NotificationRoutingPolicy.Apply(settings.SuppressWindowsBanners || settings.NotificationMode == "All" ? NotificationCatalog.Snapshot().Where(s => Routes(settings, s.Id)).Select(s => s.Id) : Enumerable.Empty<string>());
                    long stamp = Stamp();
                    if (initialized && failures == 0 && stamp == previousStamp)
                        continue;
                    var rows = NotificationStore.Query("SELECT n.Id,n.Payload,n.ArrivalTime,h.PrimaryId FROM Notification n JOIN NotificationHandler h ON n.HandlerId=h.RecordId" + filter + " ORDER BY n.ArrivalTime DESC LIMIT 256");
                    previousStamp = stamp;
                    if (!initialized)
                    {
                        foreach (var row in rows)
                            Remember(DesktopNotifications.Identity(row));
                        initialized = true;
                        Program.Log("notification-router.txt", "Running; mode=" + settings.NotificationMode + "; senders=" + NotificationCatalog.Snapshot().Length);
                    }
                    foreach (var row in rows.AsEnumerable().Reverse())
                    {
                        string rowKey = row["Id"] + ":" + row["ArrivalTime"], payload;
                        if (payloads.TryGetValue(rowKey, out payload) && payload == row["Payload"])
                            continue;
                        if (payloads.Count > 1024)
                            payloads.Clear();
                        payloads[rowKey] = row["Payload"];
                        string identity = DesktopNotifications.Identity(row);
                        if (seen.Contains(identity))
                            continue;
                        Remember(identity);
                        string id = row["PrimaryId"];
                        if (!Routes(settings, id))
                            continue;
                        bool codex = id.IndexOf("Codex", StringComparison.OrdinalIgnoreCase) >= 0;
                        if (codex && settings.CodexBackgroundOnly && DesktopNotifications.IsCodexForeground())
                            continue;
                        if (id == "Windows.SystemToast.Power" && settings.BatteryEnabled)
                        {
                            if (BatteryAlerts.CurrentPercent <= settings.BatteryThreshold)
                                BatteryAlerts.ShowWarning(BatteryAlerts.CurrentPercent);
                            continue;
                        }
                        string title, body, path;
                        if (!DesktopNotifications.Parse(row["Payload"], out title, out body, out path))
                            continue;
                        var app = codex ? NotificationApp.Codex : id.IndexOf("Telegram", StringComparison.OrdinalIgnoreCase) >= 0 ? NotificationApp.Telegram : NotificationApp.Other;
                        Bitmap avatar = DesktopNotifications.LoadAvatar(codex ? Path.Combine(Program.DataPath, "codex-icon.png") : path);
                        // Never fetch notification images from the network.
                        bool hasAvatar = avatar != null;
                        Program.Presenter.Notify(title, body, app, avatar, delegate
                        {
                            int count = Interlocked.Increment(ref presented);
                            Program.Log("notification-router.txt", "Presented=" + count + "; Avatar=" + hasAvatar + "; UTC=" + DateTime.UtcNow.ToString("o"));
                        });
                    }
                    failures = 0;
                }
                catch (Exception ex)
                {
                    if (++failures == 3)
                    {
                        NotificationRoutingPolicy.RestoreAll();
                        Program.Log("notification-router.txt", "Windows banners restored; " + ex.GetType().Name);
                    }
                    stop.WaitOne(800);
                }
        }
        static long Stamp()
        {
            var file = new FileInfo(NotificationStore.PathName);
            var wal = new FileInfo(NotificationStore.PathName + "-wal");
            return unchecked(file.LastWriteTimeUtc.Ticks * 397 + (wal.Exists ? wal.LastWriteTimeUtc.Ticks + wal.Length : file.Length));
        }
        internal static string Filter(AppSettings settings)
        {
            if (settings.NotificationMode == "All")
                return "";
            var clauses = new List<string>();
            var ids = (settings.NotificationApps ?? new string[0]).Where(NotificationRoutingPolicy.Valid).ToArray();
            if (ids.Length > 0)
                clauses.Add("h.PrimaryId IN (" + string.Join(",", ids.Select(id => "'" + id.Replace("'", "''") + "'")) + ")");
            if (!settings.NotificationSelectionConfigured)
            {
                if (settings.TelegramEnabled)
                    clauses.Add("h.PrimaryId LIKE '%Telegram%'");
                if (settings.CodexEnabled)
                    clauses.Add("h.PrimaryId LIKE '%Codex%'");
            }
            if (settings.BatteryEnabled)
                clauses.Add("h.PrimaryId='Windows.SystemToast.Power'");
            return " WHERE (" + (clauses.Count == 0 ? "0" : string.Join(" OR ", clauses)) + ")";
        }
        public void Dispose()
        {
            stop.Set();
            worker.Join(2500);
            NotificationRoutingPolicy.RestoreAll();
        }
    }
}
