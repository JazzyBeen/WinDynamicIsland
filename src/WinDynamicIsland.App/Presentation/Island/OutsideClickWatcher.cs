using WinDynamicIsland.Core;
using System;
using System.Drawing;
using System.Runtime.InteropServices;

// Observes button-down locations only to dismiss an open island. No input is
// swallowed, generated or recorded; the hook is removed with the window.
namespace WinDynamicIsland
{
    internal sealed class OutsideClickWatcher : IDisposable
    {
        delegate IntPtr MouseProcedure(int code, IntPtr message, IntPtr data);
        [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int hook, MouseProcedure procedure, IntPtr module, uint thread);
        [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);
        readonly MouseProcedure procedure;
        readonly Action<Point> clicked;
        IntPtr hook;
        internal OutsideClickWatcher(Action<Point> clicked)
        {
            this.clicked = clicked;
            procedure = Observe;
            hook = SetWindowsHookEx(14, procedure, GetModuleHandle(null), 0);
        }
        IntPtr Observe(int code, IntPtr message, IntPtr data)
        {
            if (code >= 0 && (message.ToInt64() == 0x201 || message.ToInt64() == 0x204 || message.ToInt64() == 0x207 || message.ToInt64() == 0x20b))
            {
                var point = new Point(Marshal.ReadInt32(data), Marshal.ReadInt32(data, 4));
                try
                {
                    clicked(point);
                }
                catch { }
            }
            return CallNextHookEx(hook, code, message, data);
        }
        public void Dispose()
        {
            if (hook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(hook);
                hook = IntPtr.Zero;
            }
            GC.KeepAlive(procedure);
        }
    }

}
