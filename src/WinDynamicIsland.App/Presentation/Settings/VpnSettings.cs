using System;
using System.Linq;
using System.Net.NetworkInformation;
using WinDynamicIsland.Core;
namespace WinDynamicIsland
{
    internal sealed partial class SettingsForm
    {
        void AdapterChoice()
        {
            var adapters = NetworkInterface.GetAllNetworkInterfaces().Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback).ToArray();
            var names = new[] { L.T("ChooseAdapter") }.Concat(adapters.Select(n => n.Name)).ToArray();
            int index = Array.FindIndex(adapters, n => n.Id == draft.VpnAdapterId) + 1;
            Choice("VpnAdapter", names, index, i => draft.VpnAdapterId = i == 0 ? "" : adapters[i - 1].Id, false);
        }
    }
}
