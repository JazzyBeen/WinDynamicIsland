using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using WinDynamicIsland.Core;
namespace WinDynamicIsland
{
    internal sealed class BannerSnapshot
    {
        public object Banner
        {
            get; set;
        }
        public object Center
        {
            get; set;
        }
    }
    internal sealed class BannerRoutingStore
    {
        readonly string RegistryRoot; readonly string directory;
        internal BannerRoutingStore(string directory, string registryRoot)
        {
            this.directory = directory;
            RegistryRoot = registryRoot;
            Directory.CreateDirectory(directory);
        }
        readonly object gate = new object();
        readonly HashSet<string> applied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string Backup
        {
            get
            {
                return Path.Combine(directory, "notification-routing-backup.json");
            }
        }
        Dictionary<string, BannerSnapshot> Load()
        {
            return File.Exists(Backup) ? JsonCodec.Create().Deserialize<Dictionary<string, BannerSnapshot>>(File.ReadAllText(Backup)) : new Dictionary<string, BannerSnapshot>();
        }
        internal void Apply(IEnumerable<string> sources)
        {
            lock (gate)
            {
                var next = new HashSet<string>(sources.Where(Valid), StringComparer.OrdinalIgnoreCase);
                if (next.SetEquals(applied))
                    return;
                var backup = Load();
                foreach (string id in applied.Except(next).ToArray())
                    Restore(id, backup);
                foreach (string id in next.Except(applied).ToArray())
                    using (var key = Registry.CurrentUser.CreateSubKey(RegistryRoot + id))
                    {
                        backup[id] = new BannerSnapshot { Banner = key.GetValue("ShowBanner"), Center = key.GetValue("ShowInActionCenter") };
                    }
                string temporary = Backup + ".tmp";
                File.WriteAllText(temporary, JsonCodec.Create().Serialize(backup));
                if (File.Exists(Backup))
                    File.Replace(temporary, Backup, null);
                else
                    File.Move(temporary, Backup);
                foreach (string id in next)
                    using (var key = Registry.CurrentUser.CreateSubKey(RegistryRoot + id))
                    {
                        key.SetValue("ShowBanner", 0, RegistryValueKind.DWord);
                        key.SetValue("ShowInActionCenter", 1, RegistryValueKind.DWord);
                    }
                applied.Clear();
                applied.UnionWith(next);
            }
        }
        internal static bool Valid(string id)
        {
            return !string.IsNullOrWhiteSpace(id) && id.Length < 500 && id.IndexOfAny(new[] { '\\', '/', '\0' }) < 0;
        }
        void Restore(string id, Dictionary<string, BannerSnapshot> backup)
        {
            BannerSnapshot saved;
            if (!backup.TryGetValue(id, out saved))
                return;
            using (var key = Registry.CurrentUser.CreateSubKey(RegistryRoot + id))
            {
                if (saved.Banner == null)
                    key.DeleteValue("ShowBanner", false);
                else
                    key.SetValue("ShowBanner", Convert.ToInt32(saved.Banner), RegistryValueKind.DWord);
                if (saved.Center == null)
                    key.DeleteValue("ShowInActionCenter", false);
                else
                    key.SetValue("ShowInActionCenter", Convert.ToInt32(saved.Center), RegistryValueKind.DWord);
            }
        }
        internal void RestoreAll()
        {
            lock (gate)
            {
                if (!File.Exists(Backup))
                    return;
                Dictionary<string, BannerSnapshot> saved;
                try
                {
                    saved = Load();
                }
                catch { Program.Log("notification-policy.txt", "Saved banner settings could not be read"); return; }
                foreach (string id in saved.Keys.Where(Valid))
                    try
                    {
                        Restore(id, saved);
                    }
                    catch { }
                applied.Clear();
            }
        }
    }
    internal static class NotificationRoutingPolicy
    {
        static readonly BannerRoutingStore current = new BannerRoutingStore(Program.DataPath, @"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\");
        internal static bool Valid(string id)
        {
            return BannerRoutingStore.Valid(id);
        }
        internal static void Apply(IEnumerable<string> sources)
        {
            current.Apply(sources);
        }
        internal static void RestoreAll()
        {
            current.RestoreAll();
        }
    }
}
