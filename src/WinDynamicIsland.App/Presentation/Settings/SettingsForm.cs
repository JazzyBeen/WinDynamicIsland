using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using WinDynamicIsland.Core;

namespace WinDynamicIsland
{
    internal sealed partial class SettingsForm : Form
    {
        readonly Action<AppSettings> apply;
        AppSettings draft;
        readonly Panel content = new BufferedPanel();
        readonly AnimatedNavigationPanel navigation = new AnimatedNavigationPanel();
        readonly List<Action> collect = new List<Action>();
        readonly Label heading = new Label(), subtitle = new Label();
        readonly GlassButton save = new GlassButton { Primary = true };
        FlowLayoutPanel body; string section = "General"; bool rebuilding;
        bool closingForExit;
        readonly Color background = Color.Black, surface = Color.Black, muted = IslandTheme.Secondary, accent = Color.White;
        [System.Runtime.InteropServices.DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int dark = 1;
                DwmSetWindowAttribute(Handle, 20, ref dark, 4);
                int black = 0;
                DwmSetWindowAttribute(Handle, 35, ref black, 4);
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
            Font = new Font("Segoe UI Variable Text", 10);
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
            navigation.BackColor = Color.Black;
            Controls.Add(content);
            Controls.Add(navigation);
            content.Dock = DockStyle.Fill;
            content.Padding = new Padding(28, 24, 28, 22);
            InitializePageHost();
            BuildNavigation();
            ShowSection("General");
        }
        Button Button(string text, Action click)
        {
            var button = new GlassButton { Text = text, FlatStyle = FlatStyle.Flat, BackColor = surface, ForeColor = Color.White, Height = 38, Width = 620, Cursor = Cursors.Hand, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(12, 0, 0, 0), Margin = new Padding(0, 7, 0, 10) };
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
            if (navigation.Controls.Count > 0)
            {
                foreach (Control control in navigation.Controls)
                {
                    var button = control as GlassButton;
                    if (button == null || button.Tag == null)
                        continue;
                    button.Text = L.T((string)button.Tag);
                    button.Selected = (string)button.Tag == section;
                    button.Invalidate();
                    if (button.Selected && !rebuilding)
                        navigation.Select(button, Program.Settings.InterfaceAnimations && Visible && !exporting);
                }
                return;
            }
            var mark = new Label { Text = "Dynamic Island", Font = new Font("Segoe UI Variable Display", 12, FontStyle.Bold), ForeColor = accent, Width = 184, Height = 58, Margin = new Padding(0, 0, 0, 24) };
            navigation.Controls.Add(mark);
            foreach (string key in new[] { "General", "Appearance", "Media", "Notifications", "Devices", "VPN", "About" })
            {
                string target = key;
                var button = Button(L.T(key), () => ShowSection(target));
                button.Width = 184;
                button.Height = 44;
                button.Margin = new Padding(0, 2, 0, 2);
                ((GlassButton)button).Navigation = true;
                ((GlassButton)button).Selected = key == section;
                button.Tag = key;
                navigation.Controls.Add(button);
            }
            navigation.Controls.Add(new Label { Text = "2.1.1  ·  Windows", ForeColor = muted, Width = 180, Height = 32, Margin = new Padding(12, 32, 0, 0) });
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
            var previousBody = body;
            var previousSection = section;
            var previousCollectors = new List<Action>(collect);
            string previousHeading = heading.Text;
            bool animate = Visible && body != null && !exporting && Program.Settings.InterfaceAnimations;
            rebuilding = true;
            try
            {
                Collect();
                collect.Clear();
                navigation.Freeze();
                if (previousBody != null)
                    scrollPositions[previousSection] = ((IslandScrollPanel)previousBody).ScrollOffset;
                transition.Bounds = pageHost.Bounds;
                if (animate)
                    transition.Cover(transition.CaptureVisible(pageHost));
                else
                    transition.Finish();
                pageHost.SuspendLayout();
                if (previousBody != null)
                {
                    previousBody.Visible = false;
                    previousBody.SuspendLayout();
                }
                section = key;
                BuildNavigation();
                heading.Text = L.T(key);
                subtitle.Text = "Win Dynamic Island";
                save.Text = L.T("Save");
                body = new IslandScrollPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = false, Padding = new Padding(0, 12, 8, 12), Font = Font, Visible = false, Size = new Size(Math.Max(1, pageHost.ClientSize.Width), Math.Max(1, pageHost.ClientSize.Height - 80)) };
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
                    Check("GlassStyle", draft.GlassStyle, v => draft.GlassStyle = v);
                    Check("InterfaceMotion", draft.InterfaceAnimations, v => draft.InterfaceAnimations = v);
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
                    Choice("NotificationRoute", new[] { L.T("SelectedApps"), L.T("AllApps") }, draft.NotificationMode == "All" ? 1 : 0, i => draft.NotificationMode = i == 1 ? "All" : "Selected", false);
                    Help("RoutingHelp");
                    NotificationApps();
                    Check("CodexBackground", draft.CodexBackgroundOnly, v => draft.CodexBackgroundOnly = v);
                    if (draft.NotificationMode != "All")
                        Check("Suppress", draft.SuppressWindowsBanners, v => draft.SuppressWindowsBanners = v);
                }
                else if (key == "Devices")
                {
                    Check("VolumeHud", draft.VolumeEnabled, v => draft.VolumeEnabled = v);
                    Check("ReplaceVolume", draft.ReplaceSystemVolumeFlyout, v => draft.ReplaceSystemVolumeFlyout = v);
                    Check("MicrophoneHud", draft.MicrophoneEnabled, v => draft.MicrophoneEnabled = v);
                    Check("F7Indicator", draft.F7MicrophoneIndicator, v => draft.F7MicrophoneIndicator = v);
                    Check("F7Control", draft.F7ControlsMicrophone, v => draft.F7ControlsMicrophone = v);
                    Help("F7Help");
                    Check("CapsIndicator", draft.CapsLockEnabled, v => draft.CapsLockEnabled = v);
                    Check("LayoutIndicator", draft.KeyboardLayoutEnabled, v => draft.KeyboardLayoutEnabled = v);
                    Check("Headphones", draft.HeadphonesEnabled, v => draft.HeadphonesEnabled = v);
                    Check("Battery", draft.BatteryEnabled, v => draft.BatteryEnabled = v);
                    Number("Threshold", draft.BatteryThreshold, 5, 50, v => draft.BatteryThreshold = v);
                    Check("Charging", draft.ChargingEnabled, v => draft.ChargingEnabled = v);
                }
                else if (key == "VPN")
                {
                    Check("VpnEnabled", draft.VpnEnabled, v => draft.VpnEnabled = v);
                    Choice("Provider", new[] { L.T("Nothing"), "Sota VPN", L.T("WindowsVpn"), L.T("CustomCli"), L.T("AdapterVpn") }, Array.IndexOf(new[] { "None", "Sota", "Windows", "Command", "Adapter" }, draft.VpnProvider), i => draft.VpnProvider = new[] { "None", "Sota", "Windows", "Command", "Adapter" }[i], false);
                    TextBox("VpnName", draft.VpnName, v => draft.VpnName = v);
                    Check("Hotkey", draft.VpnHotkeyEnabled, v => draft.VpnHotkeyEnabled = v);
                    if (draft.VpnProvider == "Windows")
                        TextBox("Profile", draft.WindowsVpnProfile, v => draft.WindowsVpnProfile = v);
                    else if (draft.VpnProvider == "Sota")
                    {
                        Help("SotaHelp");
                        SotaProfileStatus();
                        body.Controls.Add(Button(L.T("SotaSetup"), () => { string exe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SotaProfileSetup.exe"); Process.Start(new ProcessStartInfo(exe, "--language " + draft.Language) { UseShellExecute = true, Verb = "runas" }); }));
                    }
                    else if (draft.VpnProvider == "Command" || draft.VpnProvider == "Adapter")
                    {
                        Help("UniversalVpnHelp");
                        if (draft.VpnProvider == "Adapter")
                            AdapterChoice();
                        TextBox("ConnectExe", draft.ConnectExecutable, v => draft.ConnectExecutable = v);
                        TextBox("ConnectArgs", draft.ConnectArguments, v => draft.ConnectArguments = v);
                        TextBox("DisconnectExe", draft.DisconnectExecutable, v => draft.DisconnectExecutable = v);
                        TextBox("DisconnectArgs", draft.DisconnectArguments, v => draft.DisconnectArguments = v);
                        TextBox("StatusExe", draft.StatusExecutable, v => draft.StatusExecutable = v);
                        TextBox("StatusArgs", draft.StatusArguments, v => draft.StatusArguments = v);
                    }
                    else
                        Help("VpnNotConfigured");
                    Check("VpnStatusHud", draft.VpnStatusEnabled, v => draft.VpnStatusEnabled = v);
                }
                else
                {
                    Help("AboutText");
                    body.Controls.Add(Button(L.T("DataFolder"), () => Process.Start("explorer.exe", Program.DataPath)));
                    body.Controls.Add(Button("GitHub · WinDynamicIsland", () => Process.Start("https://github.com/JazzyBeen/WinDynamicIsland")));
                    body.Controls.Add(Button(L.T("Preview"), () => Program.Presenter.Status("Win Dynamic Island", L.T("Ready"), true, false)));
                    body.Controls.Add(new Label { Text = L.T("Running"), ForeColor = IslandTheme.Accent, Width = 620, Height = 44, Margin = new Padding(0, 12, 0, 0) });
                }
                pageHost.Controls.Add(body);
                pageHost.Controls.SetChildIndex(body, 0);
                pageHost.ResumeLayout(true);
                body.Visible = true;
                body.PerformLayout();
                int scroll;
                ((IslandScrollPanel)body).ScrollTo(scrollPositions.TryGetValue(key, out scroll) ? scroll : 0);
                if (animate)
                {
                    int direction = Array.IndexOf(new[] { "General", "Appearance", "Media", "Notifications", "Devices", "VPN", "About" }, key) >= Array.IndexOf(new[] { "General", "Appearance", "Media", "Notifications", "Devices", "VPN", "About" }, previousSection) ? 1 : -1;
                    transition.Begin(PageTransitionSurface.CapturePage(pageHost), direction);
                }
                else
                    pageHost.Invalidate(true);
                SelectNavigation(animate);
                if (previousBody != null)
                    previousBody.Dispose();
            }
            catch
            {
                transition.Finish();
                if (body != previousBody && body != null)
                    body.Dispose();
                body = previousBody;
                collect.Clear();
                collect.AddRange(previousCollectors);
                section = previousSection;
                heading.Text = previousHeading;
                if (body != null && !body.IsDisposed)
                {
                    body.ResumeLayout(false);
                    body.Visible = true;
                }
                BuildNavigation();
                SelectNavigation(false);
                throw;
            }
            finally { pageHost.ResumeLayout(true); rebuilding = false; }
        }
        void Help(string key)
        {
            var label = new Label { Text = L.T(key), ForeColor = muted, Width = 620, AutoSize = false, Margin = new Padding(0, 8, 0, 14) };
            label.Height = TextRenderer.MeasureText(label.Text, Font, new Size(620, 0), TextFormatFlags.WordBreak).Height + 12;
            label.SizeChanged += delegate
            {
                if (label.Width > 0)
                {
                    int h = TextRenderer.MeasureText(label.Text, label.Font, new Size(label.Width, 0), TextFormatFlags.WordBreak).Height + 12;
                    if (label.Height != h)
                        label.Height = h;
                }
            };
            body.Controls.Add(label);
        }
        void Check(string key, bool value, Action<bool> set)
        {
            var box = new ToggleSwitch { Text = L.T(key), Checked = value, Width = 620, Height = 60, Padding = new Padding(14, 0, 12, 0), Margin = new Padding(0, 4, 0, 4), BackColor = surface, FlatStyle = FlatStyle.Standard };
            body.Controls.Add(box);
            collect.Add(() => set(box.Checked));
        }
        Panel Row(string key)
        {
            var panel = new GlassCard { Width = 620, Height = 65, BackColor = surface, Margin = new Padding(0, 4, 0, 4) };
            panel.Controls.Add(new Label { Text = L.T(key), BackColor = Color.Transparent, AutoEllipsis = true, AutoSize = false, Width = 350, Height = 42, Location = new Point(14, 17) });
            body.Controls.Add(panel);
            return panel;
        }
        void Number(string key, int value, int min, int max, Action<int> set)
        {
            var input = new SnapSlider(L.T(key), value, min, max) { Font = Font, Margin = new Padding(0, 5, 0, 5) };
            body.Controls.Add(input);
            collect.Add(() => set(input.Value));
        }
        void Choice(string key, string[] values, int index, Action<int> set, bool language)
        {
            var row = Row(key);
            row.Controls[0].Width = 275;
            var input = new SelectionButton(values, Math.Max(0, index)) { Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(335, 16), Width = 265, Height = 34, BackColor = background, ForeColor = Color.White, AccessibleName = L.T(key) };
            row.Controls.Add(input);
            row.Resize += delegate
            {
                row.Controls[0].Width = Math.Max(80, input.Left - 24);
            };
            collect.Add(() => set(input.SelectedIndex));
            if (language || key == "Provider" || key == "NotificationRoute")
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
            var input = new TextBox { Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Text = value ?? "", Location = new Point(14, 41), Width = 584, BackColor = background, ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
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
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!closingForExit && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                L.Language = Program.Settings.Language;
                return;
            }
            base.OnFormClosing(e);
        }
        internal void CloseForExit()
        {
            closingForExit = true;
            Close();
        }
        internal void ReloadSaved()
        {
            collect.Clear();
            draft = Program.Settings.Clone();
            L.Language = draft.Language;
            ShowSection(section);
        }
        internal void Export(string folder)
        {
            Directory.CreateDirectory(folder);
            exporting = true;
            Show();
            CompleteReveal();
            Application.DoEvents();
            foreach (string page in new[] { "General", "Appearance", "Notifications", "Devices", "VPN" })
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
    internal sealed class SelectionButton : GlassButton
    {
        readonly ContextMenuStrip menu;
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
            menu = new ContextMenuStrip { BackColor = Color.FromArgb(22, 22, 24), ForeColor = Color.White };
            IslandMenuRenderer.Style(menu);
            for (int i = 0; i < values.Length; i++)
            {
                int item = i;
                var option = new ToolStripMenuItem(values[i]) { Padding = new Padding(12, 7, 18, 7) };
                option.Click += delegate
                {
                    var owner = FindForm();
                    if (owner != null && !owner.IsDisposed && owner.IsHandleCreated)
                        owner.BeginInvoke((Action)delegate
                        {
                            if (!IsDisposed)
                                SelectIndex(item);
                        });
                };
                menu.Items.Add(option);
            }
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
            menu.Font = Font;
            for (int i = 0; i < menu.Items.Count; i++)
            {
                var item = (ToolStripMenuItem)menu.Items[i];
                item.Checked = false;
                item.Text = (i == SelectedIndex ? "✓  " : "    ") + values[i];
            }
            menu.Show(this, new Point(0, Height));
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                menu.Dispose();
            base.Dispose(disposing);
        }
    }
}
