using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using WinDynamicIsland.Core;
namespace WinDynamicIsland
{
    internal static class VpnProviderFactory
    {
        internal static IVpnProvider Create(AppSettings settings)
        {
            if (settings.VpnProvider == "Sota")
                return new SotaVpnProvider();
            if (settings.VpnProvider == "Windows")
                return new WindowsVpnProvider(settings.WindowsVpnProfile, settings.VpnName);
            if (settings.VpnProvider == "Command")
                return new CommandVpnProvider(settings);
            throw new InvalidOperationException(L.T("VpnNotConfigured"));
        }
    }
    internal static class VpnCommand
    {
        internal static int Run(string executable, string arguments, int timeout)
        {
            if (string.IsNullOrWhiteSpace(executable))
                throw new InvalidOperationException(L.T("VpnNotConfigured"));
            using (var process = Process.Start(new ProcessStartInfo(executable, arguments ?? "") { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden }))
            {
                if (!process.WaitForExit(timeout))
                    throw new TimeoutException(L.T("Unavailable"));
                return process.ExitCode;
            }
        }
    }
    internal sealed class CommandVpnProvider : IVpnProvider
    {
        readonly AppSettings config; internal CommandVpnProvider(AppSettings config)
        {
            this.config = config;
        }
        public string Name
        {
            get
            {
                return config.VpnName;
            }
        }
        public VpnState GetState()
        {
            int code = VpnCommand.Run(config.StatusExecutable, config.StatusArguments, 5000);
            return code == 0 ? VpnState.Connected : code == 1 ? VpnState.Disconnected : VpnState.Unknown;
        }
        public void Connect()
        {
            if (VpnCommand.Run(config.ConnectExecutable, config.ConnectArguments, 45000) != 0)
                throw new IOException(L.T("Error"));
            Wait(VpnState.Connected);
        }
        public void Disconnect()
        {
            if (VpnCommand.Run(config.DisconnectExecutable, config.DisconnectArguments, 45000) != 0)
                throw new IOException(L.T("Error"));
            Wait(VpnState.Disconnected);
        }
        void Wait(VpnState wanted)
        {
            for (int i = 0; i < 30; i++)
            {
                if (GetState() == wanted)
                    return;
                System.Threading.Thread.Sleep(500);
            }
            throw new TimeoutException(L.T("Unavailable"));
        }
    }
    internal sealed class WindowsVpnProvider : IVpnProvider
    {
        readonly string profile, name; internal WindowsVpnProvider(string profile, string name)
        {
            this.profile = profile;
            this.name = name;
        }
        public string Name
        {
            get
            {
                return name;
            }
        }
        // ras.h uses pshpack4.h, including on x64.
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 4)]
        struct RasConnection
        {
            internal int Size; internal IntPtr Handle;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)] internal string Entry;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 17)] internal string DeviceType;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 129)] internal string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] internal string Phonebook;
            internal int SubEntry; internal Guid EntryId; internal int Flags; internal long Luid; internal Guid CorrelationId;
        }
        [DllImport("rasapi32.dll", CharSet = CharSet.Unicode)] static extern int RasEnumConnections([In, Out] RasConnection[] connections, ref int bytes, out int count);
        public VpnState GetState()
        {
            var connections = new RasConnection[64];
            connections[0].Size = Marshal.SizeOf(typeof(RasConnection));
            int bytes = connections[0].Size * connections.Length, count;
            int code = RasEnumConnections(connections, ref bytes, out count);
            if (code != 0)
            {
                Program.Log("ras-status.txt", "Code=" + code + "; Size=" + connections[0].Size + "; Count=" + count);
                return VpnState.Unknown;
            }
            return connections.Take(count).Any(c => string.Equals(c.Entry, profile, StringComparison.OrdinalIgnoreCase)) ? VpnState.Connected : VpnState.Disconnected;
        }
        string Arguments
        {
            get
            {
                if (string.IsNullOrWhiteSpace(profile) || profile.Contains("\"") || profile.Contains("\r") || profile.Contains("\n"))
                    throw new InvalidOperationException(L.T("VpnNotConfigured"));
                return "\"" + profile + "\"";
            }
        }
        public void Connect()
        {
            if (VpnCommand.Run(Path.Combine(Environment.SystemDirectory, "rasdial.exe"), Arguments, 65000) != 0)
                throw new IOException(L.T("Error"));
        }
        public void Disconnect()
        {
            if (VpnCommand.Run(Path.Combine(Environment.SystemDirectory, "rasdial.exe"), Arguments + " /disconnect", 15000) != 0)
                throw new IOException(L.T("Error"));
        }
    }
}
