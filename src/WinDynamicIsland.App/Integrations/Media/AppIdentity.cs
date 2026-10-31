using WinDynamicIsland.Core;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Windows.Foundation;
using Windows.Media.Control;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace WinDynamicIsland
{
    internal static class AppIdentity
    {
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint id);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern int GetApplicationUserModelId(IntPtr process, ref uint length, StringBuilder value);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)] static extern void SHCreateItemFromParsingName(string path, IntPtr context, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory item);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr handle);
        [DllImport("shell32.dll", PreserveSig = false)] static extern void SHGetPropertyStoreForWindow(IntPtr window, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IPropertyStore store);
        [DllImport("ole32.dll")] static extern int PropVariantClear(ref PropVariant value);
        [StructLayout(LayoutKind.Sequential)]
        struct PropertyKey
        {
            internal Guid Format; internal uint Id;
        }
        [StructLayout(LayoutKind.Explicit, Size = 24)]
        struct PropVariant
        {
            [FieldOffset(0)] internal ushort Type; [FieldOffset(8)] internal IntPtr Pointer;
        }
        [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IPropertyStore
        {
            void GetCount(out uint count); void GetAt(uint index, out PropertyKey key); void GetValue(ref PropertyKey key, out PropVariant value);
            void SetValue(ref PropertyKey key, ref PropVariant value); void Commit();
        }
        [StructLayout(LayoutKind.Sequential)]
        struct Size
        {
            internal int X, Y;
        }
        [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IShellItemImageFactory
        {
            void GetImage(Size size, uint flags, out IntPtr bitmap);
        }
        static bool Matches(Process process, string id)
        {
            try
            {
                if (id.StartsWith("Chrome.", StringComparison.OrdinalIgnoreCase) && process.ProcessName.Equals("chrome", StringComparison.OrdinalIgnoreCase))
                    return true;
                if (id.StartsWith("Edge.", StringComparison.OrdinalIgnoreCase) && process.ProcessName.Equals("msedge", StringComparison.OrdinalIgnoreCase))
                    return true;
                if (id.StartsWith("Yandex.", StringComparison.OrdinalIgnoreCase) && process.ProcessName.Equals("browser", StringComparison.OrdinalIgnoreCase))
                    return true;
                uint length = 512;
                var value = new StringBuilder(512);
                if (GetApplicationUserModelId(process.Handle, ref length, value) == 0 && string.Equals(value.ToString(), id, StringComparison.OrdinalIgnoreCase))
                    return true;
                string stem = Path.GetFileNameWithoutExtension(id);
                return string.Equals(process.ProcessName, stem, StringComparison.OrdinalIgnoreCase) || string.Equals(process.ProcessName, id, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }
        internal static bool IsForeground(string id)
        {
            IPropertyStore store = null;
            var value = new PropVariant();
            try
            {
                var window = GetForegroundWindow();
                var iid = typeof(IPropertyStore).GUID;
                try
                {
                    SHGetPropertyStoreForWindow(window, ref iid, out store);
                    var key = new PropertyKey { Format = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), Id = 5 };
                    store.GetValue(ref key, out value);
                    if (value.Type == 31 && string.Equals(Marshal.PtrToStringUni(value.Pointer), id, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                catch { }
                uint pid;
                GetWindowThreadProcessId(window, out pid);
                using (var process = Process.GetProcessById((int)pid))
                    return Matches(process, id);
            }
            catch { return false; }
            finally { PropVariantClear(ref value); if (store != null) Marshal.ReleaseComObject(store); }
        }
        internal static Bitmap IconFor(string id, out string name)
        {
            name = Path.GetFileNameWithoutExtension(id);
            IShellItemImageFactory item = null;
            IntPtr bitmap = IntPtr.Zero;
            try
            {
                var iid = typeof(IShellItemImageFactory).GUID;
                SHCreateItemFromParsingName("shell:AppsFolder\\" + id, IntPtr.Zero, ref iid, out item);
                item.GetImage(new Size { X = 64, Y = 64 }, 0x4 | 0x1, out bitmap);
                using (var image = Image.FromHbitmap(bitmap))
                    return new Bitmap(image);
            }
            catch { }
            finally { if (bitmap != IntPtr.Zero) DeleteObject(bitmap); if (item != null) Marshal.ReleaseComObject(item); }
            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    if (Matches(process, id))
                    {
                        name = id.StartsWith("Yandex.", StringComparison.OrdinalIgnoreCase) ? "Яндекс Браузер" : process.ProcessName;
                        using (var icon = Icon.ExtractAssociatedIcon(process.MainModule.FileName))
                            return icon.ToBitmap();
                    }
                }
                catch { }
                finally { process.Dispose(); }
            }
            return null;
        }
    }


}
