using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;
using MaterialColorUtilities.Utils;
namespace WinDynamicIsland
{
    // Read Windows wallpaper files only; never capture the desktop or change it.
    internal sealed class WallpaperPaletteMonitor : IDisposable
    {
        readonly ManualResetEvent stop = new ManualResetEvent(false);
        readonly Thread worker;
        static string CachePath { get { return Path.Combine(Program.DataPath, "wallpaper-palette.txt"); } }
        internal static string CachedSeed { get { try { return File.ReadAllText(CachePath).Trim(); } catch { return "#6750A4"; } } }
        internal WallpaperPaletteMonitor(Action<Color> changed)
        {
            worker = new Thread(() => Run(changed)) { IsBackground = true, Name = "Island wallpaper palette" };
            worker.SetApartmentState(ApartmentState.STA); worker.Start();
        }
        void Run(Action<Color> changed)
        {
            string previous = null;
            while (!stop.WaitOne(0))
            {
                try
                {
                    string path = CurrentWallpaper();
                    string signature = path + "|" + Program.Settings.MonitorIndex + "|" + (File.Exists(path) ? File.GetLastWriteTimeUtc(path).Ticks.ToString() : MaterialPalette.Hex(SolidColor()));
                    if (signature != previous)
                    {
                        Color seed = File.Exists(path) ? Extract(path) : SolidColor();
                        if (stop.WaitOne(0)) break;
                        Directory.CreateDirectory(Program.DataPath);
                        File.WriteAllText(CachePath, MaterialPalette.Hex(seed));
                        previous = signature; changed(seed);
                    }
                }
                catch { /* Keep the last valid palette while Windows replaces an image. */ }
                if (stop.WaitOne(4000)) break;
            }
        }
        internal static Color Extract(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var image = Image.FromStream(stream))
            using (var sample = new Bitmap(64, 64))
            {
                using (var g = Graphics.FromImage(sample))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    g.DrawImage(image, new Rectangle(0, 0, 64, 64));
                }
                var pixels = new uint[64 * 64]; int index = 0;
                for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) pixels[index++] = unchecked((uint)sample.GetPixel(x, y).ToArgb());
                var colors = ImageUtils.ColorsFromImage(pixels);
                return colors.Count == 0 ? MaterialPalette.Parse("#6750A4") : Color.FromArgb(unchecked((int)colors[0]));
            }
        }
        internal static string CurrentWallpaper()
        {
            IDesktopWallpaper wallpaper = null;
            try
            {
                wallpaper = (IDesktopWallpaper)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("C2CF3110-460E-4FC1-B9D0-8A1C0C9CC4BD")));
                uint count = wallpaper.GetMonitorDevicePathCount();
                var target = Program.TargetScreen.Bounds;
                for (uint i = 0; i < count; i++)
                {
                    string id = wallpaper.GetMonitorDevicePathAt(i); Rect rect; wallpaper.GetMonitorRECT(id, out rect);
                    if (rect.Left == target.Left && rect.Top == target.Top)
                    {
                        string file = wallpaper.GetWallpaper(id);
                        if (string.IsNullOrEmpty(file)) return "";
                        if (File.Exists(file)) return file;
                    }
                }
            }
            catch { }
            finally { if (wallpaper != null) Marshal.FinalReleaseComObject(wallpaper); }
            using (var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop"))
            {
                string file = key == null ? null : key.GetValue("WallPaper") as string;
                if (!string.IsNullOrWhiteSpace(file) && File.Exists(file)) return file;
            }
            string cached = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Themes\TranscodedWallpaper");
            return File.Exists(cached) ? cached : "";
        }
        static Color SolidColor()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Colors"))
            {
                string value = key == null ? "" : Convert.ToString(key.GetValue("Background"));
                var parts = value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                int r, g, b;
                if (parts.Length == 3 && int.TryParse(parts[0], out r) && int.TryParse(parts[1], out g) && int.TryParse(parts[2], out b)) return Color.FromArgb(Math.Max(0,Math.Min(255,r)),Math.Max(0,Math.Min(255,g)),Math.Max(0,Math.Min(255,b)));
            }
            return MaterialPalette.Parse("#6750A4");
        }
        public void Dispose() { stop.Set(); if (worker.Join(1000)) stop.Dispose(); }
        [StructLayout(LayoutKind.Sequential)] struct Rect { internal int Left, Top, Right, Bottom; }
        [ComImport, Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IDesktopWallpaper
        {
            void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitor, [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);
            [return: MarshalAs(UnmanagedType.LPWStr)] string GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitor);
            [return: MarshalAs(UnmanagedType.LPWStr)] string GetMonitorDevicePathAt(uint index);
            uint GetMonitorDevicePathCount();
            void GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitor, out Rect rect);
        }
    }
}
