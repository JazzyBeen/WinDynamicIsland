using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using WinDynamicIsland.Core;
namespace WinDynamicIsland
{
    internal sealed class AdapterVpnProvider : IVpnProvider
    {
        readonly AppSettings settings;
        internal AdapterVpnProvider(AppSettings settings)
        {
            this.settings = settings;
        }
        public string Name
        {
            get
            {
                return settings.VpnName;
            }
        }
        public VpnState GetState()
        {
            return Read(settings.VpnAdapterId);
        }
        internal static VpnState Read(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return VpnState.Unknown;
            var adapter = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(n => n.Id == id);
            return adapter != null && adapter.OperationalStatus == OperationalStatus.Up ? VpnState.Connected : VpnState.Disconnected;
        }
        void Execute(string executable, string arguments, VpnState wanted)
        {
            if (string.IsNullOrWhiteSpace(executable))
                throw new InvalidOperationException(L.T("VpnNeedsCommands"));
            if (VpnCommand.Run(executable, arguments, 45000) != 0)
                throw new InvalidOperationException(L.T("Error"));
            for (int i = 0; i < 40; i++)
            {
                if (GetState() == wanted)
                    return;
                Thread.Sleep(350);
            }
            throw new TimeoutException(L.T("Unavailable"));
        }
        public void Connect()
        {
            Execute(settings.ConnectExecutable, settings.ConnectArguments, VpnState.Connected);
        }
        public void Disconnect()
        {
            Execute(settings.DisconnectExecutable, settings.DisconnectArguments, VpnState.Disconnected);
        }
    }
    internal sealed class VpnStatusMonitor : IDisposable
    {
        readonly ManualResetEvent stop = new ManualResetEvent(false); readonly Thread worker;
        internal VpnStatusMonitor()
        {
            worker = new Thread(Run) { IsBackground = true, Name = "Island VPN status" };
            worker.Start();
        }
        void Run()
        {
            VpnState previous = VpnState.Unknown;
            string source = null;
            while (!stop.WaitOne(1200))
                try
                {
                    var config = Program.Settings;
                    string nextSource = config.VpnProvider + config.VpnAdapterId + config.WindowsVpnProfile;
                    if (source != nextSource)
                    {
                        previous = VpnState.Unknown;
                        source = nextSource;
                    }
                    if (!config.VpnStatusEnabled)
                        continue;
                    if (config.VpnProvider != "Adapter" && config.VpnProvider != "Sota" && config.VpnProvider != "Windows")
                        continue;
                    var state = VpnProviderFactory.Create(config).GetState();
                    if (state != VpnState.Unknown && previous != VpnState.Unknown && state != previous)
                        Program.Presenter.Status(config.VpnName, L.T(state == VpnState.Connected ? "Connected" : "Disconnected"), true, false);
                    previous = state;
                }
                catch { }
        }
        public void Dispose()
        {
            stop.Set();
            worker.Join(4000);
        }
    }
}
