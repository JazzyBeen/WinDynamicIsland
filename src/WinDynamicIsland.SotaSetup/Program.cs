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
            if (args.Length == 3 && args[0] == "--watch")
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
    internal sealed class CaptureProxy : IDisposable
    {
        readonly Action<string> report;
        readonly string endpointPath;
        readonly Action<Dictionary<string, object>> saveProfile;
        string recoveryId;
        TcpListener listener;
        string original;
        Dictionary<string, object> endpoint;
        int port;
        volatile bool stopped;
        int saved;
        readonly List<TcpClient> clients = new List<TcpClient>();
        readonly object sync = new object();
        internal CaptureProxy(Action<string> report, string testEndpoint = null, Action<Dictionary<string, object>> testSave = null)
        {
            this.report = report;
            endpointPath = testEndpoint ?? ApiProfile.Endpoint;
            saveProfile = testSave ?? ApiProfile.Save;
        }
        internal void Start()
        {
            if (endpointPath == ApiProfile.Endpoint)
                Recovery.Restore(null);
            original = ApiProfile.ReadShared(endpointPath);
            endpoint = ApiProfile.Json().Deserialize<Dictionary<string, object>>(original);
            listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var changed = new Dictionary<string, object>(endpoint);
            changed["port"] = port;
            if (endpointPath == ApiProfile.Endpoint)
            {
                recoveryId = Recovery.Prepare(original, port);
                Process.Start(new ProcessStartInfo(Application.ExecutablePath, "--watch " + Process.GetCurrentProcess().Id + " " + recoveryId) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden });
            }
            File.WriteAllText(endpointPath, ApiProfile.Json().Serialize(changed), new UTF8Encoding(false));
            new Thread(Accept) { IsBackground = true }.Start();
        }
        void Accept()
        {
            try
            {
                while (!stopped)
                {
                    var c = listener.AcceptTcpClient();
                    lock (sync)
                        clients.Add(c);
                    new Thread(delegate () { Relay(c); }) { IsBackground = true }.Start();
                }
            }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }
        }
        internal static Dictionary<string, object> StartParameters(string line, out string id)
        {
            id = null;
            try
            {
                var f = ApiProfile.Json().Deserialize<Dictionary<string, object>>(line);
                if (!f.ContainsKey("method") || !object.Equals(f["method"], "start") || !f.ContainsKey("id"))
                    return null;
                var p = (Dictionary<string, object>)f["params"];
                ApiProfile.Validate(p);
                id = Convert.ToString(f["id"]);
                return p;
            }
            catch { return null; }
        }
        internal static bool Success(string line, string id)
        {
            try
            {
                var f = ApiProfile.Json().Deserialize<Dictionary<string, object>>(line);
                return f.ContainsKey("id") && Convert.ToString(f["id"]) == id && f.ContainsKey("result") && (!f.ContainsKey("error") || f["error"] == null);
            }
            catch { return false; }
        }
        void Relay(TcpClient downstream)
        {
            try
            {
                using (downstream)
                using (var upstream = new TcpClient())
                {
                    upstream.Connect(IPAddress.Loopback, Convert.ToInt32(endpoint["port"]));
                    lock (sync)
                        clients.Add(upstream);
                    var pending = new Dictionary<string, Dictionary<string, object>>();
                    var dr = new StreamReader(downstream.GetStream(), Encoding.UTF8);
                    var dw = new StreamWriter(downstream.GetStream(), new UTF8Encoding(false)) { AutoFlush = true };
                    var ur = new StreamReader(upstream.GetStream(), Encoding.UTF8);
                    var uw = new StreamWriter(upstream.GetStream(), new UTF8Encoding(false)) { AutoFlush = true };
                    var forward = new Thread(delegate ()
                    {
                        try
                        {
                            string line;
                            while (!stopped && (line = dr.ReadLine()) != null)
                            {
                                string id;
                                var p = StartParameters(line, out id);
                                if (p != null)
                                    lock (pending)
                                        pending[id] = p;
                                uw.WriteLine(line);
                            }
                        }
                        catch { }
                        finally { try { upstream.Close(); } catch { } }
                    })
                    {
                        IsBackground = true
                    };
                    forward.Start();
                    string reply;
                    while (!stopped && (reply = ur.ReadLine()) != null)
                    {
                        dw.WriteLine(reply);
                        Dictionary<string, object> accepted = null;
                        lock (pending)
                        {
                            foreach (var p in pending)
                                if (Success(reply, p.Key))
                                {
                                    accepted = p.Value;
                                    break;
                                }
                        }
                        if (accepted != null && Interlocked.CompareExchange(ref saved, 1, 0) == 0)
                        {
                            try
                            {
                                saveProfile(accepted);
                                Restore();
                                report(L.T("SetupSaved"));
                            }
                            catch { Interlocked.Exchange(ref saved, 0); report(L.T("SetupSaveFailed")); }
                        }
                    }
                }
            }
            catch { }
            finally { lock (sync) clients.Remove(downstream); }
        }
        void Restore()
        {
            lock (sync)
            {
                if (original == null)
                    return;
                var current = ApiProfile.Json().Deserialize<Dictionary<string, object>>(ApiProfile.ReadShared(endpointPath));
                if (Convert.ToInt32(current["port"]) == port && object.Equals(current["token"], endpoint["token"]))
                    File.WriteAllText(endpointPath, original, new UTF8Encoding(false));
                if (recoveryId != null)
                    Recovery.Restore(recoveryId);
            }
        }
        public void Dispose()
        {
            stopped = true;
            try
            {
                Restore();
            }
            catch { report(L.T("SetupRestoreFailed")); }
            if (listener != null)
                listener.Stop();
            lock (sync)
                foreach (var c in clients)
                    try
                    {
                        c.Close();
                    }
                    catch { }
        }
    }
    internal static class Recovery
    {
        static string PathName
        {
            get
            {
                return Path.Combine(Path.GetDirectoryName(ApiProfile.PathName), "capture-recovery.bin");
            }
        }
        internal static string Prepare(string original, int port)
        {
            string id = Guid.NewGuid().ToString("N");
            var payload = new
            {
                id = id,
                original = original,
                port = port
            };
            byte[] plain = Encoding.UTF8.GetBytes(ApiProfile.Json().Serialize(payload));
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PathName));
                File.WriteAllBytes(PathName, ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser));
            }
            finally { Array.Clear(plain, 0, plain.Length); }
            return id;
        }
        internal static void Restore(string expectedId)
        {
            if (!File.Exists(PathName))
                return;
            byte[] plain = ProtectedData.Unprotect(File.ReadAllBytes(PathName), null, DataProtectionScope.CurrentUser);
            try
            {
                var data = ApiProfile.Json().Deserialize<Dictionary<string, object>>(Encoding.UTF8.GetString(plain));
                if (expectedId != null && !object.Equals(data["id"], expectedId))
                    return;
                var current = ApiProfile.Json().Deserialize<Dictionary<string, object>>(ApiProfile.ReadShared(ApiProfile.Endpoint));
                var old = ApiProfile.Json().Deserialize<Dictionary<string, object>>((string)data["original"]);
                if (Convert.ToInt32(current["port"]) == Convert.ToInt32(data["port"]) && object.Equals(current["token"], old["token"]))
                    File.WriteAllText(ApiProfile.Endpoint, (string)data["original"], new UTF8Encoding(false));
                File.Delete(PathName);
            }
            finally { Array.Clear(plain, 0, plain.Length); }
        }
    }

}
