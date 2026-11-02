using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Windows.Forms;
using WinDynamicIsland.Core;
namespace WinDynamicIsland
{
    // A headless elevated worker is required to write Sota's protected endpoint.
    // The wizard itself lives in SettingsForm. No connection is stopped here.
    internal sealed class CaptureWorker : ApplicationContext
    {
        internal static string StatusPath { get { return Path.Combine(Program.DataPath, "capture-status.txt"); } }
        internal static string CancelPath { get { return Path.Combine(Program.DataPath, "capture-cancel.flag"); } }
        readonly Timer poll = new Timer { Interval = 500 };
        readonly DateTime started = DateTime.UtcNow;
        CaptureProxy capture;
        volatile bool saved;
        internal static void Run(string language)
        {
            L.Language = language;
            if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)) { File.WriteAllText(StatusPath, L.T("SetupAdmin")); return; }
            bool created;
            using (var mutex = new System.Threading.Mutex(true, "Local\\WinDynamicIsland.Capture", out created))
            {
                if (!created) return;
                using (var worker = new CaptureWorker()) { if (worker.capture != null) Application.Run(worker); }
            }
        }
        CaptureWorker()
        {
            try
            {
                capture = new CaptureProxy(message => { File.WriteAllText(StatusPath, message); if (message == L.T("SetupSaved")) saved = true; });
                capture.Start();
                File.WriteAllText(StatusPath, L.T("SetupWaiting"));
                poll.Tick += delegate
                {
                    if (!saved && !File.Exists(CancelPath) && DateTime.UtcNow - started < TimeSpan.FromMinutes(10)) return;
                    if (!saved) File.WriteAllText(StatusPath, L.T("SetupTimeout"));
                    ExitThread();
                };
                poll.Start();
            }
            catch (Exception) { if (capture != null) capture.Dispose(); capture = null; File.WriteAllText(StatusPath, L.T("SetupFailed")); }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { poll.Dispose(); if (capture != null) capture.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
