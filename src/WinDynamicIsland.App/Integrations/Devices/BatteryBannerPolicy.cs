using WinDynamicIsland.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

// Only UI-warning flags are changed. Sleep/hibernate actions and thresholds are untouched.
namespace WinDynamicIsland
{
    internal static class BatteryBannerPolicy
    {
        internal sealed class SavedSetting
        {
            public string Scheme, Setting; public uint AC, DC;
        }
        static readonly Guid Battery = new Guid("e73a048d-bf27-4f12-9731-8b2076e8891f");
        static readonly Guid[] Warnings = { new Guid("bcded951-187b-4d05-bccc-f7e51960c258"), new Guid("5dbb7c9f-38e9-40d2-9749-4f8a0e9f640f") };
        static string applied;
        static readonly object Gate = new object();
        static string Backup
        {
            get
            {
                return Path.Combine(Program.DataPath, "battery-warning-backup.json");
            }
        }
        [DllImport("powrprof.dll")] static extern uint PowerGetActiveScheme(IntPtr root, out IntPtr scheme);
        [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr memory);
        [DllImport("powrprof.dll")] static extern uint PowerReadACValueIndex(IntPtr root, ref Guid scheme, ref Guid group, ref Guid setting, out uint value);
        [DllImport("powrprof.dll")] static extern uint PowerReadDCValueIndex(IntPtr root, ref Guid scheme, ref Guid group, ref Guid setting, out uint value);
        [DllImport("powrprof.dll")] static extern uint PowerWriteACValueIndex(IntPtr root, ref Guid scheme, ref Guid group, ref Guid setting, uint value);
        [DllImport("powrprof.dll")] static extern uint PowerWriteDCValueIndex(IntPtr root, ref Guid scheme, ref Guid group, ref Guid setting, uint value);
        [DllImport("powrprof.dll")] static extern uint PowerSetActiveScheme(IntPtr root, ref Guid scheme);
        static Guid Active()
        {
            IntPtr p;
            if (PowerGetActiveScheme(IntPtr.Zero, out p) != 0)
                throw new IOException("Power scheme unavailable");
            try
            {
                return (Guid)Marshal.PtrToStructure(p, typeof(Guid));
            }
            finally { LocalFree(p); }
        }
        static List<SavedSetting> Saved()
        {
            return File.Exists(Backup) ? JsonCodec.Create().Deserialize<List<SavedSetting>>(File.ReadAllText(Backup)) : new List<SavedSetting>();
        }
        internal static void Ensure()
        {
            lock (Gate)
            {
                Guid scheme = Active();
                if (applied == scheme.ToString())
                    return;
                var saved = Saved();
                Guid group = Battery;
                foreach (var candidate in Warnings)
                {
                    Guid setting = candidate;
                    uint ac, dc;
                    if (PowerReadACValueIndex(IntPtr.Zero, ref scheme, ref group, ref setting, out ac) != 0 || PowerReadDCValueIndex(IntPtr.Zero, ref scheme, ref group, ref setting, out dc) != 0)
                        throw new IOException("Cannot read battery warning settings");
                    if (!saved.Exists(s => s.Scheme == scheme.ToString() && s.Setting == setting.ToString()))
                        saved.Add(new SavedSetting { Scheme = scheme.ToString(), Setting = setting.ToString(), AC = ac, DC = dc });
                }
                File.WriteAllText(Backup, JsonCodec.Create().Serialize(saved));
                try
                {
                    foreach (var candidate in Warnings)
                    {
                        Guid setting = candidate;
                        if (PowerWriteACValueIndex(IntPtr.Zero, ref scheme, ref group, ref setting, 0) != 0 || PowerWriteDCValueIndex(IntPtr.Zero, ref scheme, ref group, ref setting, 0) != 0)
                            throw new IOException("Cannot disable duplicate battery warning");
                    }
                    if (PowerSetActiveScheme(IntPtr.Zero, ref scheme) != 0)
                        throw new IOException("Cannot apply battery warning setting");
                    applied = scheme.ToString();
                    Program.Log("battery-policy.txt", "Windows low/critical warning banners off; original settings saved; power actions unchanged");
                }
                catch { Restore(); throw; }
            }
        }
        internal static void Restore()
        {
            lock (Gate)
            {
                if (!File.Exists(Backup))
                    return;
                Guid group = Battery;
                foreach (var saved in Saved())
                {
                    Guid scheme = new Guid(saved.Scheme), setting = new Guid(saved.Setting);
                    PowerWriteACValueIndex(IntPtr.Zero, ref scheme, ref group, ref setting, saved.AC);
                    PowerWriteDCValueIndex(IntPtr.Zero, ref scheme, ref group, ref setting, saved.DC);
                }
                Guid active = Active();
                PowerSetActiveScheme(IntPtr.Zero, ref active);
                applied = null;
            }
        }
    }

}
