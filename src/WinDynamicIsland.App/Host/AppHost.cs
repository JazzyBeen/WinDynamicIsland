using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using WinDynamicIsland.Core;

namespace WinDynamicIsland
{
    internal sealed class FeatureModule : IIslandModule
    {
        readonly Func<IDisposable> create; IDisposable service;
        public string Id
        {
            get; private set;
        }
        internal FeatureModule(string id, Func<IDisposable> create)
        {
            Id = id;
            this.create = create;
        }
        public void Start()
        {
            service = create();
        }
        public void Dispose()
        {
            if (service != null)
                service.Dispose();
            service = null;
        }
    }
    internal sealed class AppHost : ApplicationContext
    {
        readonly NotifyIcon tray = new NotifyIcon();
        readonly SynchronizationContext ui = new WindowsFormsSynchronizationContext();
        readonly HostWindow commands;
        ModuleCoordinator modules = new ModuleCoordinator(); SettingsForm settings; bool busy, quitting;
        internal AppHost()
        {
            commands = new HostWindow(Dispatch);
            tray.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            tray.Text = "Win Dynamic Island";
            tray.MouseClick += delegate (object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                    OpenSettings();
            };
        }
        internal void Start(string action)
        {
            StartupRegistration.SetEnabled(Program.Settings.StartWithWindows);
            ConfigureModules();
            ConfigureTray();
            tray.Visible = true;
            string guard = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "IslandGuard.exe");
            if (File.Exists(guard))
                using (var owner = Process.GetCurrentProcess())
                    Process.Start(new ProcessStartInfo(guard, owner.Id + " " + owner.StartTime.ToUniversalTime().Ticks) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden });
            Program.Log("host.txt", "Running; version=2.1.1; PID=" + Process.GetCurrentProcess().Id);
            if (Program.Settings.ShowStartupIsland)
                Program.Presenter.Status("Win Dynamic Island", L.T("Ready"), true, false);
            if (action == "--settings")
                OpenSettings();
            else if (action == "--toggle-vpn")
                ToggleVpn();
        }
        void ConfigureModules()
        {
            var next = new Dictionary<string, Func<IDisposable>>();
            if (Program.Settings.BrowserEnabled && Program.Settings.MediaEnabled)
                next["browser"] = () => new BrowserLoopback();
            if (Program.Settings.MediaEnabled)
                next["media"] = () => new MediaSessionMonitor();
            if (Program.Settings.BatteryEnabled || Program.Settings.HeadphonesEnabled || Program.Settings.ChargingEnabled)
                next["devices"] = () => new DeviceMonitor();
            if (Program.Settings.NotificationMode == "All" || Program.Settings.NotificationApps.Length > 0 || !Program.Settings.NotificationSelectionConfigured && (Program.Settings.TelegramEnabled || Program.Settings.CodexEnabled))
                next["notifications"] = () => new NotificationRouter();
            if (Program.Settings.VolumeEnabled || Program.Settings.MicrophoneEnabled)
                next["audio"] = () => new AudioMonitor();
            if (Program.Settings.VolumeEnabled || Program.Settings.F7MicrophoneIndicator && Program.Settings.MicrophoneEnabled || Program.Settings.CapsLockEnabled || Program.Settings.KeyboardLayoutEnabled)
                next["keys"] = () => new KeyboardIndicators();
            if (Program.Settings.VpnStatusEnabled)
                next["vpn-status"] = () => new VpnStatusMonitor();
            modules.Update(next);
            commands.SetHotkey(Program.Settings.VpnEnabled && Program.Settings.VpnHotkeyEnabled);
        }
        void ConfigureTray()
        {
            var menu = new ContextMenuStrip();
            IslandMenuRenderer.Style(menu);
            menu.Items.Add(L.T("OpenSettings"), null, delegate
            {
                OpenSettings();
            });
            menu.Items.Add(L.T("ToggleVpn"), null, delegate
            {
                ToggleVpn();
            });
            menu.Items.Add(L.T("Preview"), null, delegate
            {
                Program.Presenter.Status("Win Dynamic Island", L.T("Ready"), true, false);
            });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(L.T("Exit"), null, delegate
            {
                Quit();
            });
            var old = tray.ContextMenuStrip;
            tray.ContextMenuStrip = menu;
            if (old != null)
                old.Dispose();
        }
        void Dispatch(int command)
        {
            if (command == 1)
                OpenSettings();
            else if (command == 2)
                ToggleVpn();
            else if (command == 3)
                Quit();
        }
        internal void OpenSettings()
        {
            if (settings == null || settings.IsDisposed)
                settings = new SettingsForm(Apply);
            else if (!settings.Visible)
                settings.ReloadSaved();
            settings.RevealFromTray();
            if (settings.WindowState == FormWindowState.Minimized)
                settings.WindowState = FormWindowState.Normal;
            settings.Activate();
            Program.ShowWindow(settings.Handle, 9);
            Program.SetForegroundWindow(settings.Handle);
            Program.Log("settings-window.txt", "Opened; Visible=" + settings.Visible);
        }
        void Apply(AppSettings next)
        {
            if (busy)
                throw new InvalidOperationException(L.T("Connecting"));
            Program.Store.Save(next);
            StartupRegistration.SetEnabled(next.StartWithWindows);
            Program.Settings = next;
            L.Language = next.Language;
            ConfigureModules();
            ConfigureTray();
        }
        void ToggleVpn()
        {
            if (busy)
                return;
            if (!Program.Settings.VpnEnabled || Program.Settings.VpnProvider == "None")
            {
                OpenSettings();
                return;
            }
            var snapshot = Program.Settings.Clone();
            busy = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    var provider = VpnProviderFactory.Create(snapshot);
                    var state = provider.GetState();
                    if (state == VpnState.Unknown)
                        throw new InvalidOperationException(L.T("Unavailable"));
                    bool connect = state != VpnState.Connected;
                    Program.Presenter.Status(provider.Name, L.T(connect ? "Connecting" : "Disconnecting"), false, false);
                    if (connect)
                        provider.Connect();
                    else
                        provider.Disconnect();
                    if (provider.GetState() != (connect ? VpnState.Connected : VpnState.Disconnected))
                        throw new InvalidOperationException(L.T("Unavailable"));
                    Program.Presenter.Status(provider.Name, L.T(connect ? "Connected" : "Disconnected"), true, false);
                }
                catch (Exception ex) { Program.Log("vpn-error.txt", ex.GetType().Name); Program.Presenter.Status(snapshot.VpnName, L.T("Error") + ": " + ex.Message, true, true); }
                finally { ui.Post(_ => { busy = false; }, null); }
            });
        }
        void Quit()
        {
            if (quitting)
                return;
            quitting = true;
            if (settings != null)
                settings.CloseForExit();
            tray.Visible = false;
            if (modules != null)
                modules.Dispose();
            modules = null;
            commands.Dispose();
            BatteryBannerPolicy.Restore();
            NotificationRoutingPolicy.RestoreAll();
            foreach (NotificationApp app in Enum.GetValues(typeof(NotificationApp)))
                DesktopNotifications.RestoreBanners(app);
            ExitThread();
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!quitting)
                    Quit();
                tray.Dispose();
            }
            base.Dispose(disposing);
        }
    }
    internal sealed class HostWindow : NativeWindow, IDisposable
    {
        internal const uint CommandMessage = 0x8002; readonly Action<int> dispatch; bool registered;
        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)] static extern bool RegisterHotKey(IntPtr h, int id, uint modifiers, uint key);
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h, int id);
        internal HostWindow(Action<int> dispatch)
        {
            this.dispatch = dispatch;
            CreateHandle(new CreateParams { Caption = "WinDynamicIsland.Host" });
        }
        internal void SetHotkey(bool enabled)
        {
            if (registered)
            {
                UnregisterHotKey(Handle, 7821);
                registered = false;
            }
            if (enabled)
            {
                registered = RegisterHotKey(Handle, 7821, 0x4001, 0x4e);
                Program.Log("hotkey.txt", registered ? "Alt+N registered" : "Alt+N unavailable; tray remains usable");
            }
        }
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == CommandMessage)
                dispatch(message.WParam.ToInt32());
            else if (message.Msg == 0x312 && message.WParam.ToInt32() == 7821)
                dispatch(2);
            base.WndProc(ref message);
        }
        public void Dispose()
        {
            if (registered)
                UnregisterHotKey(Handle, 7821);
            DestroyHandle();
        }
    }
}
