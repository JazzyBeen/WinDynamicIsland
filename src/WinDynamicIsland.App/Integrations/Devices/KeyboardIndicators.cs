using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using WinDynamicIsland.Core;
namespace WinDynamicIsland
{
    // Only the configured indicator keys are observed; typed text is never collected.
    internal sealed class KeyboardIndicators : IDisposable
    {
        delegate IntPtr Hook(int code, IntPtr message, IntPtr data);
        [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int id, Hook callback, IntPtr module, uint thread);
        [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);
        [DllImport("user32.dll")] static extern short GetKeyState(int key);
        [DllImport("user32.dll")] static extern IntPtr GetKeyboardLayout(uint thread);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        readonly Thread thread; readonly ManualResetEvent ready = new ManualResetEvent(false);
        Hook callback; IntPtr hook; ApplicationContext context; SynchronizationContext ui; bool f7Down; long layout;
        internal KeyboardIndicators()
        {
            thread = new Thread(Run) { IsBackground = true, Name = "Island indicator keys" };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            ready.WaitOne(1500);
        }
        void Run()
        {
            callback = Handle;
            hook = SetWindowsHookEx(13, callback, GetModuleHandle(null), 0);
            context = new ApplicationContext();
            ui = new WindowsFormsSynchronizationContext();
            var timer = new System.Windows.Forms.Timer { Interval = 700 };
            timer.Tick += delegate
            {
                Layout();
            };
            timer.Start();
            ready.Set();
            Application.Run(context);
            timer.Dispose();
            if (hook != IntPtr.Zero)
                UnhookWindowsHookEx(hook);
        }
        IntPtr Handle(int code, IntPtr message, IntPtr pointer)
        {
            if (code >= 0)
            {
                int key = Marshal.ReadInt32(pointer);
                long kind = message.ToInt64();
                bool down = kind == 0x100 || kind == 0x104, up = kind == 0x101 || kind == 0x105;
                if ((key == 0xAD || key == 0xAE || key == 0xAF) && Program.Settings.VolumeEnabled)
                {
                    var audio = AudioMonitor.Current;
                    bool control = audio != null && audio.CanControlVolume && Program.Settings.ReplaceSystemVolumeFlyout;
                    if (down && audio != null)
                        audio.VolumeKey(key, control);
                    if (control)
                        return new IntPtr(1);
                }
                if (key == 0x76 && Program.Settings.F7MicrophoneIndicator && Program.Settings.MicrophoneEnabled)
                {
                    if (down && !f7Down)
                    {
                        f7Down = true;
                        var monitor = AudioMonitor.Current;
                        if (monitor != null)
                            monitor.F7();
                    }
                    if (up)
                        f7Down = false;
                    if (Program.Settings.F7ControlsMicrophone)
                        return new IntPtr(1);
                }
                if (down && key == 0x14 && Program.Settings.CapsLockEnabled)
                {
                    bool enabled = (GetKeyState(0x14) & 1) == 0;
                    Island.ShowHud(CompactNoticeKind.CapsLock, "Caps Lock", enabled ? 1 : 0, enabled, false);
                }
            }
            return CallNextHookEx(hook, code, message, pointer);
        }
        void Layout()
        {
            if (!Program.Settings.KeyboardLayoutEnabled)
                return;
            uint process;
            uint thread = GetWindowThreadProcessId(Program.GetForegroundWindow(), out process);
            long next = GetKeyboardLayout(thread).ToInt64() & 0xffff;
            if (next != layout && layout != 0)
            {
                string name;
                try
                {
                    name = System.Globalization.CultureInfo.GetCultureInfo((int)next).TwoLetterISOLanguageName.ToUpperInvariant();
                }
                catch { name = "⌨"; }
                Island.ShowHud(CompactNoticeKind.KeyboardLayout, name, 0, false, false);
            }
            layout = next;
        }
        public void Dispose()
        {
            var current = ui;
            if (current != null)
                current.Post(_ => context.ExitThread(), null);
            thread.Join(2000);
        }
    }
}
