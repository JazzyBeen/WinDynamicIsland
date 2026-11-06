using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using WinDynamicIsland.Core;
namespace WinDynamicIsland
{
    internal sealed partial class SettingsForm
    {
        void PaletteSettings()
        {
            var row = Row("PaletteSource");
            string[] modes = { "Default", "Custom", "Wallpaper" };
            var choice = new SelectionButton(new[] { L.T("PaletteDefault"), L.T("PaletteCustom"), L.T("PaletteWallpaper") }, Math.Max(0, Array.IndexOf(modes, draft.MaterialColorMode))) { Height = 44, Width = 265, Top = 16, Font = Font, AccessibleName = L.T("PaletteSource") };
            row.Controls.Add(choice);
            Action layout = delegate { choice.Left = row.Width - choice.Width - 20; row.Controls[0].Width = Math.Max(80, choice.Left - 24); };
            row.Resize += delegate { layout(); }; layout();
            choice.SelectionChanged += delegate { ChangeColors(modes[choice.SelectedIndex], draft.MaterialSeedColor); };
            if (draft.MaterialColorMode == "Wallpaper") Help("PaletteWallpaperHelp");
            if (draft.MaterialColorMode == "Custom")
            {
                var swatches = new GlassCard { Width = 620, Height = 76, Margin = new Padding(0, 4, 0, 4) };
                var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), WrapContents = false, BackColor = Color.Transparent };
                foreach (string hex in new[] { "#6750A4", "#386AFA", "#006C4C", "#00687B", "#AC3274", "#9C4235", "#805600", "#6C5F53" })
                {
                    string selected = hex;
                    var button = new ColorSwatch(MaterialPalette.Parse(hex), string.Equals(draft.MaterialSeedColor, hex, StringComparison.OrdinalIgnoreCase)) { Margin = new Padding(2), AccessibleName = hex };
                    button.Click += delegate { ChangeColors("Custom", selected); };
                    flow.Controls.Add(button);
                }
                swatches.Controls.Add(flow); body.Controls.Add(swatches);
            }
            body.Controls.Add(Button(L.T("PickColor"), () =>
            {
                using (var picker = new ColorPickerDialog(MaterialPalette.Parse(draft.MaterialSeedColor)))
                    if (picker.ShowDialog(this) == DialogResult.OK) ChangeColors("Custom", MaterialPalette.Hex(picker.SelectedColor));
            }));
        }
        void ChangeColors(string mode, string seed)
        {
            if (rebuilding) return;
            Collect();
            designTransition.Finish(); transition.Finish();
            Bitmap outgoing = Visible && draft.InterfaceAnimations && !exporting ? CaptureClient() : null;
            string previousMode = draft.MaterialColorMode, previousSeed = draft.MaterialSeedColor;
            draft.MaterialColorMode = mode; draft.MaterialSeedColor = seed;
            if (!PersistDraft()) { draft.MaterialColorMode = previousMode; draft.MaterialSeedColor = previousSeed; if (outgoing != null) outgoing.Dispose(); return; }
            if (apply == null) { Program.Settings.MaterialColorMode = mode; Program.Settings.MaterialSeedColor = seed; AppDesign.ConfigurePalette(); }
            ApplyVisualTheme(); ShowSection(section); transition.Finish();
            FinishPaletteTransition(outgoing);
        }
        internal void ApplyWallpaperPalette(Color seed)
        {
            if (AppDesign.Seed.ToArgb() == seed.ToArgb()) return;
            designTransition.Finish(); transition.Finish();
            Bitmap outgoing = Visible && draft.InterfaceAnimations && !exporting ? CaptureClient() : null;
            AppDesign.WallpaperSeed(seed);
            ApplyVisualTheme(); ShowSection(section); transition.Finish();
            FinishPaletteTransition(outgoing);
        }
        void FinishPaletteTransition(Bitmap outgoing)
        {
            if (outgoing == null) return;
            var incoming = CaptureClient(); designTransition.Bounds = ClientRectangle;
            designTransition.Cover(outgoing); designTransition.Begin(incoming, 0);
        }
    }
    internal sealed class ColorSwatch : Button
    {
        readonly Color color; readonly bool selected;
        internal ColorSwatch(Color color, bool selected)
        {
            this.color = color; this.selected = selected; Size = new Size(44, 44); FlatStyle = FlatStyle.Flat; Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var fill = new SolidBrush(color)) e.Graphics.FillEllipse(fill, 7, 7, 30, 30);
            if (selected || Focused && ShowFocusCues)
                using (var pen = new Pen(AppDesign.Ink, 2)) e.Graphics.DrawEllipse(pen, 3, 3, 38, 38);
        }
    }
}
