using System;
using System.Drawing;
using System.Windows.Forms;
using WinDynamicIsland.Core;
namespace WinDynamicIsland
{
    internal sealed partial class SettingsForm
    {
        readonly PageTransitionSurface designTransition = new PageTransitionSurface();
        void DesignChoice()
        {
            var card = new GlassCard { Width = 620, Height = 108, Margin = new Padding(0, 4, 0, 12) };
            card.Controls.Add(new Label { Text = L.T("InterfaceDesign"), AutoSize = false, BackColor = Color.Transparent, Location = new Point(20, 12), Size = new Size(560, 24) });
            var classic = new GlassButton { Text = L.T("ClassicDesign"), Selected = draft.InterfaceDesign != "Material", Primary = draft.InterfaceDesign != "Material", TextAlign = ContentAlignment.MiddleCenter, Location = new Point(12, 48), Height = 44 };
            var material = new GlassButton { Text = "Material 3 Expressive", Selected = draft.InterfaceDesign == "Material", TextAlign = ContentAlignment.MiddleCenter, Top = 48, Height = 44 };
            classic.AccessibleRole = material.AccessibleRole = AccessibleRole.RadioButton;
            card.Controls.Add(classic); card.Controls.Add(material);
            Action layout = delegate { int width = Math.Max(80, (card.Width - 28) / 2); classic.Width = width; material.SetBounds(16 + width, 48, width, 44); };
            card.Resize += delegate { layout(); };
            layout();
            classic.Click += delegate { SwitchDesign("Classic"); };
            material.Click += delegate { SwitchDesign("Material"); };
            body.Controls.Add(card);
            Help("DesignHelp");
        }
        internal void SwitchDesign(string design)
        {
            if (rebuilding || draft.InterfaceDesign == design) return;
            Collect();
            designTransition.Finish(); transition.Finish();
            Bitmap outgoing = Visible && !exporting && draft.InterfaceAnimations ? CaptureShownClient() : null;
            string previous = draft.InterfaceDesign;
            draft.InterfaceDesign = design;
            if (!PersistDraft()) { draft.InterfaceDesign = previous; if (outgoing != null) outgoing.Dispose(); return; }
            if (apply == null) { Program.Settings.InterfaceDesign = design; AppDesign.Change(design == "Material", false); }
            ApplyVisualTheme();
            RebuildQuietly();
            if (outgoing != null)
            {
                var incoming = CaptureClient();
                designTransition.Bounds = ClientRectangle;
                designTransition.Cover(outgoing); designTransition.Begin(incoming, 0);
            }
        }
        void RebuildQuietly()
        {
            quietRebuild = true;
            try { ShowSection(section); }
            finally { quietRebuild = false; }
            transition.Finish();
        }
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool PrintWindow(IntPtr window, IntPtr dc, uint flags);
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool RedrawWindow(IntPtr window, IntPtr rect, IntPtr region, uint flags);
        // The outgoing frame is what is on screen now, so it comes straight from the
        // compositor (a few ms) instead of re-rendering every control (~40 ms).
        // Incoming frames must stay off screen: use CaptureClient for those.
        Bitmap CaptureShownClient()
        {
            if (IsHandleCreated && Visible && WindowState != FormWindowState.Minimized && !designTransition.Visible)
            {
                var frame = new Bitmap(Math.Max(1, ClientSize.Width), Math.Max(1, ClientSize.Height), System.Drawing.Imaging.PixelFormat.Format32bppRgb);
                bool captured = false;
                try
                {
                    // Paint pending invalidations first so the copy is current.
                    RedrawWindow(Handle, IntPtr.Zero, IntPtr.Zero, 0x0100 | 0x0080);
                    using (var g = Graphics.FromImage(frame))
                    {
                        IntPtr dc = g.GetHdc();
                        try { captured = PrintWindow(Handle, dc, 1 | 2); }
                        finally { g.ReleaseHdc(dc); }
                    }
                }
                catch (Exception) { captured = false; }
                if (captured) return frame;
                frame.Dispose();
            }
            return CaptureClient();
        }
        Bitmap CaptureClient()
        {
            using (var full = PageTransitionSurface.CapturePage(this))
            {
                var origin = PointToScreen(Point.Empty); origin.Offset(-Left, -Top);
                return full.Clone(new Rectangle(origin, ClientSize), System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            }
        }
        void ApplyVisualTheme()
        {
            SuspendLayout();
            try
            {
                Font = AppTypography.Create(AppDesign.Material ? 10.5f : 10);
                heading.Font = AppTypography.Create(AppDesign.Material ? 26 : 25, FontStyle.Bold);
                save.Font = AppTypography.Create(10, FontStyle.Bold);
                navigation.Width = AppDesign.Material ? 234 : 218;
                foreach (Control child in navigation.Controls)
                {
                    var button = child as GlassButton;
                    if (button != null) { button.Width = AppDesign.Material ? 200 : 184; button.Height = AppDesign.Material ? 48 : 44; }
                }
                if (navigation.Controls.Count > 0) navigation.Controls[0].Font = AppTypography.Create(12, FontStyle.Bold);
                UpdateThemeColors(this);
                navigation.BackColor = AppDesign.Material ? AppDesign.Container : Color.Black;
                UpdateTitleBar();
            }
            finally { ResumeLayout(true); }
            Invalidate(true);
        }
        static void UpdateThemeColors(Control root)
        {
            root.BackColor = root is TextBox || root is AppSelectionList ? (AppDesign.Material ? AppDesign.High : Color.FromArgb(18,18,20)) : IslandTheme.Background;
            root.ForeColor = IslandTheme.Foreground;
            foreach (Control child in root.Controls)
            {
                if (child is Label)
                {
                    child.BackColor = Color.Transparent;
                    child.ForeColor = child.Tag as string == "muted" ? IslandTheme.Secondary : IslandTheme.Foreground;
                }
                else UpdateThemeColors(child);
            }
        }
        void UpdateTitleBar()
        {
            if (!IsHandleCreated) return;
            try
            {
                int dark = 1; DwmSetWindowAttribute(Handle, 20, ref dark, 4);
                Color color = IslandTheme.Background;
                int value = color.R | color.G << 8 | color.B << 16;
                DwmSetWindowAttribute(Handle, 35, ref value, 4);
            }
            catch { }
        }
    }
}
