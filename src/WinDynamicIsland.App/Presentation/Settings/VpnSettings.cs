using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using WinDynamicIsland.Core;
namespace WinDynamicIsland
{
    internal sealed partial class SettingsForm
    {
        void SotaProfileStatus()
        {
            Help("CheckingProfile");
            var label = (Label)body.Controls[body.Controls.Count - 1];
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool valid = true;
                try
                {
                    ApiProfile.Load();
                }
                catch { valid = false; }
                try
                {
                    if (!IsDisposed && IsHandleCreated)
                        BeginInvoke((Action)delegate
                        {
                            if (label.IsDisposed)
                                return;
                            label.Text = L.T(valid ? "ProfileReady" : "ProfileNeedsSetup");
                            label.Height = TextRenderer.MeasureText(label.Text, label.Font, new Size(label.Width, 0), TextFormatFlags.WordBreak).Height + 12;
                        });
                }
                catch (InvalidOperationException) { }
            });
        }
        void AdapterChoice()
        {
            var adapters = NetworkInterface.GetAllNetworkInterfaces().Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback).ToArray();
            var names = new[] { L.T("ChooseAdapter") }.Concat(adapters.Select(n => n.Name)).ToArray();
            int index = Array.FindIndex(adapters, n => n.Id == draft.VpnAdapterId) + 1;
            Choice("VpnAdapter", names, index, i => draft.VpnAdapterId = i == 0 ? "" : adapters[i - 1].Id, false);
        }
    }
}
