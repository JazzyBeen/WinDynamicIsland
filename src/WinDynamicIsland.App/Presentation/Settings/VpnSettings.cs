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
        readonly System.Windows.Forms.Timer captureUi = new System.Windows.Forms.Timer { Interval = 700 };
        Label captureStatusLabel;
        void UpdateCaptureStatus(object sender, EventArgs e)
        {
            if (captureStatusLabel == null || captureStatusLabel.IsDisposed) return;
            try { if (System.IO.File.Exists(CaptureWorker.StatusPath)) captureStatusLabel.Text = System.IO.File.ReadAllText(CaptureWorker.StatusPath); } catch (System.IO.IOException) { }
        }
        void EmbeddedVpnSetup()
        {
            Help("CaptureHint");
            var label = new Label { Text = "", ForeColor = IslandTheme.Secondary, Width = 620, Height = 90, AutoEllipsis = true };
            captureStatusLabel = label;
            body.Controls.Add(label);
            body.Controls.Add(Button(L.T("CaptureInline"), () =>
            {
                if (!PersistDraft()) return;
                System.IO.File.Delete(CaptureWorker.CancelPath);
                System.IO.File.WriteAllText(CaptureWorker.StatusPath, L.T("CheckingProfile"));
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Application.ExecutablePath, "--capture-profile " + draft.Language) { UseShellExecute = true, Verb = "runas", WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden });
                captureUi.Start();
            }));
            body.Controls.Add(Button(L.T("CaptureCancel"), () => System.IO.File.WriteAllText(CaptureWorker.CancelPath, "cancel")));
            captureUi.Stop();
            captureUi.Start();
        }
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
