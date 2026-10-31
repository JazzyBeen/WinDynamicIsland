using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using WinDynamicIsland.Core;

namespace WinDynamicIsland
{
    internal sealed class SettingsForm : Form
    {
        readonly Action<AppSettings> apply;
        AppSettings draft;
        readonly Panel content = new Panel();
        readonly FlowLayoutPanel navigation = new FlowLayoutPanel();
        readonly List<Action> collect = new List<Action>();
        readonly Label heading = new Label(), subtitle = new Label();
        readonly Button save = new Button();
        FlowLayoutPanel body; string section = "General"; bool rebuilding;
        readonly Color background = Color.FromArgb(15, 16, 19), surface = Color.FromArgb(27, 29, 34), muted = Color.FromArgb(155, 161, 174), accent = Color.FromArgb(127, 226, 184);
        [System.Runtime.InteropServices.DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int dark = 1;
                DwmSetWindowAttribute(Handle, 20, ref dark, 4);
            }
            catch { }
        }
        internal SettingsForm(Action<AppSettings> apply)
        {
            this.apply = apply;
            draft = Program.Settings.Clone();
            Text = "Win Dynamic Island";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(980, 740);
            MinimumSize = new Size(860, 680);
            BackColor = background;
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 10);
            AutoScaleMode = AutoScaleMode.Dpi;
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }
            navigation.Dock = DockStyle.Left;
            navigation.Width = 218;
            navigation.Padding = new Padding(18, 22, 14, 16);
            navigation.FlowDirection = FlowDirection.TopDown;
            navigation.WrapContents = false;
            navigation.BackColor = Color.FromArgb(20, 21, 25);
            Controls.Add(content);
            Controls.Add(navigation);
            content.Dock = DockStyle.Fill;
            content.Padding = new Padding(28, 24, 28, 22);
            BuildNavigation();
            ShowSection("General");
        }
        Button Button(string text, Action click)
        {
            var button = new Button { Text = text, FlatStyle = FlatStyle.Flat, BackColor = surface, ForeColor = Color.White, Height = 38, Width = 550, Cursor = Cursors.Hand, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(12, 0, 0, 0), Margin = new Padding(0, 7, 0, 10) };
            button.FlatAppearance.BorderSize = 0;
            button.Click += delegate
            {
                try
                {
                    click();
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, L.T("Error"), MessageBoxButtons.OK, MessageBoxIcon.Information); }
            };
            return button;
        }
        void BuildNavigation()
        {
            navigation.Controls.Clear();
            var mark = new Label { Text = "●  WIN DYNAMIC\n     ISLAND", Font = new Font("Segoe UI", 12, FontStyle.Bold), ForeColor = accent, Width = 184, Height = 78, Margin = new Padding(0, 0, 0, 20) };
            navigation.Controls.Add(mark);
            foreach (string key in new[] { "General", "Appearance", "Media", "Notifications", "Devices", "VPN", "About" })
            {
                string target = key;
                var button = Button(L.T(key), () => ShowSection(target));
                button.Width = 184;
                button.Height = 44;
                button.Margin = new Padding(0, 2, 0, 2);
                button.BackColor = key == section ? surface : navigation.BackColor;
                button.Tag = key;
                navigation.Controls.Add(button);
            }
            navigation.Controls.Add(new Label { Text = "2.0  ·  Windows", ForeColor = muted, Width = 180, Height = 32, Margin = new Padding(12, 32, 0, 0) });
        }
        void Collect()
        {
            foreach (var action in collect)
                action();
        }
        void ShowSection(string key)
        {
            if (rebuilding)
                return;
            Collect();
            collect.Clear();
            section = key;
            rebuilding = true;
            content.SuspendLayout();
            if (save.Parent != null)
                save.Parent.Controls.Remove(save);
            foreach (Control previous in content.Controls)
            {
                if (previous != heading && previous != subtitle)
                    previous.Dispose();
            }
            content.Controls.Clear();
            heading.Text = L.T(key);
            heading.Font = new Font("Segoe UI", 22, FontStyle.Bold);
            heading.AutoSize = false;
            heading.Dock = DockStyle.Top;
            heading.Height = 48;
            subtitle.Text = "Win Dynamic Island";
            subtitle.ForeColor = muted;
            subtitle.Dock = DockStyle.Top;
            subtitle.Height = 32;
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 60 };
            save.Text = L.T("Save");
            save.FlatStyle = FlatStyle.Flat;
            save.FlatAppearance.BorderSize = 0;
            save.BackColor = accent;
            save.ForeColor = Color.FromArgb(10, 28, 21);
            save.Font = new Font(Font, FontStyle.Bold);
            save.Size = new Size(190, 38);
            save.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            save.Location = new Point(footer.Width - 190, 18);
            footer.Resize += delegate
            {
                save.Left = footer.Width - save.Width;
            };
            save.Click -= Save;
            save.Click += Save;
            footer.Controls.Add(save);
            body = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(0, 12, 8, 12) };
            content.Controls.Add(body);
            content.Controls.Add(footer);
            content.Controls.Add(subtitle);
            content.Controls.Add(heading);
            if (key == "General")
            {
                Help("GeneralHelp");
                Choice("Language", new[] { "Русский", "English" }, draft.Language == "en" ? 1 : 0, i => draft.Language = i == 1 ? "en" : "ru", true);
                Check("Startup", draft.StartWithWindows, v => draft.StartWithWindows = v);
                Check("StartupNotice", draft.ShowStartupIsland, v => draft.ShowStartupIsland = v);
                body.Controls.Add(Button(L.T("Preview"), () => Program.Presenter.Status("Win Dynamic Island", L.T("Ready"), true, false)));
            }
            else if (key == "Appearance")
            {
                Number("TopGap", draft.TopGap, 2, 48, v => draft.TopGap = v);
                Number("Blur", draft.MotionBlur, 0, 48, v => draft.MotionBlur = v);
                Check("Settle", draft.SettleAnimation, v => draft.SettleAnimation = v);
                var names = new List<string>();
                for (int i = 0; i < Screen.AllScreens.Length; i++)
                    names.Add((i + 1) + " · " + Screen.AllScreens[i].Bounds.Width + " × " + Screen.AllScreens[i].Bounds.Height);
                Choice("Monitor", names.ToArray(), Math.Min(draft.MonitorIndex, names.Count - 1), v => draft.MonitorIndex = v, false);
                Number("Duration", draft.NotificationSeconds, 2, 20, v => draft.NotificationSeconds = v);
                body.Controls.Add(Button(L.T("Preview"), () => Program.Presenter.Status("Win Dynamic Island", L.T("Ready"), true, false)));
            }
            else if (key == "Media")
            {
                Check("MediaEnabled", draft.MediaEnabled, v => draft.MediaEnabled = v);
                Check("BackgroundOnly", draft.MediaBackgroundOnly, v => draft.MediaBackgroundOnly = v);
                Number("Collapse", draft.MediaCollapseSeconds, 2, 15, v => draft.MediaCollapseSeconds = v);
                Check("BrowserEnabled", draft.BrowserEnabled, v => draft.BrowserEnabled = v);
                Help("BrowserHelp");
                body.Controls.Add(Button(L.T("PrepareBrowser"), () => BrowserExtensionSetup.Prepare(true)));
            }
            else if (key == "Notifications")
            {
                Check("Telegram", draft.TelegramEnabled, v => draft.TelegramEnabled = v);
                Check("Codex", draft.CodexEnabled, v => draft.CodexEnabled = v);
                Check("CodexBackground", draft.CodexBackgroundOnly, v => draft.CodexBackgroundOnly = v);
                Check("Suppress", draft.SuppressWindowsBanners, v => draft.SuppressWindowsBanners = v);
            }
            else if (key == "Devices")
            {
                Check("Headphones", draft.HeadphonesEnabled, v => draft.HeadphonesEnabled = v);
                Check("Battery", draft.BatteryEnabled, v => draft.BatteryEnabled = v);
                Number("Threshold", draft.BatteryThreshold, 5, 50, v => draft.BatteryThreshold = v);
                Check("Charging", draft.ChargingEnabled, v => draft.ChargingEnabled = v);
            }
            else if (key == "VPN")
            {
                Check("VpnEnabled", draft.VpnEnabled, v => draft.VpnEnabled = v);
                Choice("Provider", new[] { L.T("Nothing"), "Sota VPN", L.T("WindowsVpn"), L.T("CustomCli") }, Array.IndexOf(new[] { "None", "Sota", "Windows", "Command" }, draft.VpnProvider), i => draft.VpnProvider = new[] { "None", "Sota", "Windows", "Command" }[i], false);
                TextBox("VpnName", draft.VpnName, v => draft.VpnName = v);
                Check("Hotkey", draft.VpnHotkeyEnabled, v => draft.VpnHotkeyEnabled = v);
                if (draft.VpnProvider == "Windows")
                    TextBox("Profile", draft.WindowsVpnProfile, v => draft.WindowsVpnProfile = v);
                else if (draft.VpnProvider == "Sota")
                {
                    Help("SotaHelp");
                    bool valid = true;
                    try
                    {
                        ApiProfile.Load();
                    }
                    catch { valid = false; }
                    Help(valid ? "ProfileReady" : "ProfileNeedsSetup");
                    body.Controls.Add(Button(L.T("SotaSetup"), () => { string exe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SotaProfileSetup.exe"); Process.Start(new ProcessStartInfo(exe, "--language " + draft.Language) { UseShellExecute = true, Verb = "runas" }); }));
                }
                else if (draft.VpnProvider == "Command")
                {
                    Help("CommandHelp");
                    TextBox("ConnectExe", draft.ConnectExecutable, v => draft.ConnectExecutable = v);
                    TextBox("ConnectArgs", draft.ConnectArguments, v => draft.ConnectArguments = v);
                    TextBox("DisconnectExe", draft.DisconnectExecutable, v => draft.DisconnectExecutable = v);
                    TextBox("DisconnectArgs", draft.DisconnectArguments, v => draft.DisconnectArguments = v);
                    TextBox("StatusExe", draft.StatusExecutable, v => draft.StatusExecutable = v);
                    TextBox("StatusArgs", draft.StatusArguments, v => draft.StatusArguments = v);
                }
                else
                    Help("VpnNotConfigured");
            }
            else
            {
                Help("AboutText");
                body.Controls.Add(Button(L.T("DataFolder"), () => Process.Start("explorer.exe", Program.DataPath)));
                body.Controls.Add(Button("GitHub · WinDynamicIsland", () => Process.Start("https://github.com/JazzyBeen/WinDynamicIsland")));
                body.Controls.Add(Button(L.T("Preview"), () => Program.Presenter.Status("Win Dynamic Island", L.T("Ready"), true, false)));
                string status = File.Exists(Path.Combine(Program.DataPath, "host.txt")) ? File.ReadAllText(Path.Combine(Program.DataPath, "host.txt")) : "";
                body.Controls.Add(new Label { Text = status, ForeColor = muted, Width = 550, Height = 70, Margin = new Padding(0, 15, 0, 0) });
            }
            BuildNavigation();
            content.ResumeLayout();
            rebuilding = false;
        }
        void Help(string key)
        {
            var label = new Label { Text = L.T(key), ForeColor = muted, Width = 550, AutoSize = false, Margin = new Padding(0, 8, 0, 14) };
            label.Height = TextRenderer.MeasureText(label.Text, Font, new Size(550, 0), TextFormatFlags.WordBreak).Height + 12;
            body.Controls.Add(label);
        }
        void Check(string key, bool value, Action<bool> set)
        {
            var box = new CheckBox { Text = L.T(key), Checked = value, Width = 550, Height = 50, Padding = new Padding(14, 0, 12, 0), Margin = new Padding(0, 4, 0, 4), BackColor = surface, FlatStyle = FlatStyle.Standard };
            body.Controls.Add(box);
            collect.Add(() => set(box.Checked));
        }
        Panel Row(string key)
        {
            var panel = new Panel { Width = 550, Height = 65, BackColor = surface, Margin = new Padding(0, 4, 0, 4) };
            panel.Controls.Add(new Label { Text = L.T(key), AutoSize = false, Width = 350, Height = 42, Location = new Point(14, 17) });
            body.Controls.Add(panel);
            return panel;
        }
        void Number(string key, int value, int min, int max, Action<int> set)
        {
            var row = Row(key);
            var input = new NumericUpDown { Minimum = min, Maximum = max, Value = Math.Max(min, Math.Min(max, value)), Location = new Point(415, 18), Width = 115, BackColor = background, ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            row.Controls.Add(input);
            collect.Add(() => set((int)input.Value));
        }
        void Choice(string key, string[] values, int index, Action<int> set, bool language)
        {
            var row = Row(key);
            row.Controls[0].Width = 275;
            var input = new SelectionButton(values, Math.Max(0, index)) { Location = new Point(305, 16), Width = 225, Height = 34, BackColor = background, ForeColor = Color.White, AccessibleName = L.T(key) };
            row.Controls.Add(input);
            collect.Add(() => set(input.SelectedIndex));
            if (language || key == "Provider")
                input.SelectionChanged += delegate
                {
                    if (rebuilding)
                        return;
                    Collect();
                    if (language)
                        L.Language = draft.Language;
                    ShowSection(section);
                };
        }
        void TextBox(string key, string value, Action<string> set)
        {
            var row = Row(key);
            row.Height = 84;
            row.Controls[0].Height = 25;
            row.Controls[0].Top = 8;
            var input = new TextBox { Text = value ?? "", Location = new Point(14, 41), Width = 516, BackColor = background, ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            row.Controls.Add(input);
            collect.Add(() => set(input.Text));
        }
        void Save(object sender, EventArgs args)
        {
            try
            {
                Collect();
                draft.Validate();
                if (apply != null)
                    apply(draft.Clone());
                subtitle.Text = L.T("Saved");
                ShowSection(section);
                subtitle.Text = L.T("Saved");
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, L.T("Error")); }
        }
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            L.Language = Program.Settings.Language;
            base.OnFormClosed(e);
        }
        internal void Export(string folder)
        {
            Directory.CreateDirectory(folder);
            Show();
            Application.DoEvents();
            foreach (string page in new[] { "General", "VPN" })
            {
                ShowSection(page);
                Application.DoEvents();
                using (var bitmap = new Bitmap(Width, Height))
                {
                    DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height));
                    bitmap.Save(Path.Combine(folder, page + "-" + L.Language + ".png"));
                }
            }
            Close();
        }
    }
    internal sealed class SelectionButton : Button
    {
        readonly string[] values; internal int SelectedIndex
        {
            get; private set;
        }
        internal event EventHandler SelectionChanged;
        internal SelectionButton(string[] values, int selected)
        {
            this.values = values;
            SelectedIndex = selected;
            Text = values[selected] + "   ▾";
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            TextAlign = ContentAlignment.MiddleLeft;
            Padding = new Padding(10, 0, 0, 0);
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.ComboBox;
        }
        internal void SelectIndex(int index)
        {
            if (index < 0 || index >= values.Length)
                throw new ArgumentOutOfRangeException("index");
            SelectedIndex = index;
            Text = values[index] + "   ▾";
            if (SelectionChanged != null)
                SelectionChanged(this, EventArgs.Empty);
        }
        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            var menu = new ContextMenuStrip { BackColor = BackColor, ForeColor = ForeColor };
            for (int i = 0; i < values.Length; i++)
            {
                int item = i;
                var option = new ToolStripMenuItem(values[i]) { Checked = i == SelectedIndex };
                option.Click += delegate
                {
                    SelectIndex(item);
                };
                menu.Items.Add(option);
            }
            menu.Closed += delegate
            {
                menu.Dispose();
            };
            menu.Show(this, new Point(0, Height));
        }
    }
}
