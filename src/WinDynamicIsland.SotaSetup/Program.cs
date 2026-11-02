using WinDynamicIsland.Core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Diagnostics;
using System.Security.Cryptography;

namespace WinDynamicIsland
{
    internal static class CaptureProgram
    {
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--language")
                L.Language = args[1];
            if (args.Length == 3 && (args[0] == "--watch" || args[0] == "--capture-watch"))
            {
                try
                {
                    Process.GetProcessById(int.Parse(args[1])).WaitForExit();
                }
                catch { }
                try
                {
                    Recovery.Restore(args[2]);
                }
                catch { }
                return;
            }
            Application.EnableVisualStyles();
            if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
            {
                MessageBox.Show(L.T("SetupAdmin"), L.T("SetupTitle"));
                return;
            }
            bool created;
            using (var mutex = new Mutex(true, "Local\\SotaToggle.Capture", out created))
            {
                if (!created)
                {
                    MessageBox.Show(L.T("SetupBusy"), L.T("SetupTitle"));
                    return;
                }
                Application.Run(new CaptureForm());
            }
        }
    }
    internal sealed class CaptureForm : Form
    {
        readonly Label info = new Label();
        readonly Button begin = new Button();
        CaptureProxy proxy;
        readonly System.Windows.Forms.Timer timeout = new System.Windows.Forms.Timer();
        public CaptureForm()
        {
            Text = L.T("SetupTitle");
            Width = 570;
            Height = 310;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            Font = new Font("Segoe UI", 10);
            info.SetBounds(22, 20, 510, 180);
            info.Text = L.T("SetupIntro");
            begin.Text = L.T("SetupBegin");
            begin.SetBounds(350, 215, 180, 34);
            begin.Click += Start;
            Controls.Add(info);
            Controls.Add(begin);
            timeout.Interval = 10 * 60 * 1000;
            timeout.Tick += delegate
            {
                if (proxy != null)
                    proxy.Dispose();
                proxy = null;
                timeout.Stop();
                info.Text = L.T("SetupTimeout");
            };
            FormClosed += delegate
            {
                if (proxy != null)
                    proxy.Dispose();
            };
        }
        void Start(object sender, EventArgs e)
        {
            try
            {
                proxy = new CaptureProxy(delegate (string message)
                {
                    if (!IsDisposed)
                        BeginInvoke((Action)delegate
                        {
                            info.Text = message;
                        });
                });
                proxy.Start();
                begin.Enabled = false;
                timeout.Start();
                info.Text = L.T("SetupWaiting");
            }
            catch (Exception) { if (proxy != null) proxy.Dispose(); proxy = null; info.Text = L.T("SetupFailed"); }
        }
    }
}
