using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using WinDynamicIsland.Core;
namespace WinDynamicIsland
{
    internal sealed partial class SettingsForm
    {
        static readonly string[] SwatchColors = { "#6750A4", "#386AFA", "#006C4C", "#00687B", "#AC3274", "#9C4235", "#805600", "#6C5F53" };
        readonly List<ColorSwatch> swatches = new List<ColorSwatch>();
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
            swatches.Clear();
            if (draft.MaterialColorMode == "Custom")
            {
                var card = new GlassCard { Width = 620, Height = 76, Margin = new Padding(0, 4, 0, 4) };
                for (int i = 0; i < SwatchColors.Length; i++)
                {
                    string hex = SwatchColors[i];
                    var swatch = new ColorSwatch(MaterialPalette.Parse(hex)) { Location = new Point(14 + i * 52, 16), AccessibleName = hex, Tag = hex };
                    swatch.Click += delegate { ChangeColors("Custom", hex); };
                    swatches.Add(swatch);
                    card.Controls.Add(swatch);
                }
                MarkSelectedSwatch();
                body.Controls.Add(card);
            }
            body.Controls.Add(Button(L.T("PickColor"), () =>
            {
                using (var picker = new ColorPickerDialog(MaterialPalette.Parse(draft.MaterialSeedColor)))
                    if (picker.ShowDialog(this) == DialogResult.OK) ChangeColors("Custom", MaterialPalette.Hex(picker.SelectedColor));
            }));
        }
        void MarkSelectedSwatch()
        {
            foreach (var swatch in swatches)
                swatch.Selected = draft.MaterialColorMode == "Custom" && string.Equals(draft.MaterialSeedColor, (string)swatch.Tag, StringComparison.OrdinalIgnoreCase);
        }
        void ChangeColors(string mode, string seed)
        {
            if (rebuilding) return;
            if (mode == draft.MaterialColorMode && string.Equals(seed, draft.MaterialSeedColor, StringComparison.OrdinalIgnoreCase)) return;
            Collect();
            designTransition.Finish(); transition.Finish();
            Bitmap outgoing = Visible && draft.InterfaceAnimations && !exporting ? CaptureShownClient() : null;
            string previousMode = draft.MaterialColorMode, previousSeed = draft.MaterialSeedColor;
            // A new seed in the same mode only recolours: every control reads the
            // palette while painting, so the page is kept instead of rebuilt.
            bool sameLayout = mode == previousMode;
            draft.MaterialColorMode = mode; draft.MaterialSeedColor = seed;
            if (!PersistDraft()) { draft.MaterialColorMode = previousMode; draft.MaterialSeedColor = previousSeed; if (outgoing != null) outgoing.Dispose(); return; }
            if (apply == null) { Program.Settings.MaterialColorMode = mode; Program.Settings.MaterialSeedColor = seed; AppDesign.ConfigurePalette(); }
            ApplyVisualTheme();
            if (sameLayout) MarkSelectedSwatch();
            else RebuildQuietly();
            FinishPaletteTransition(outgoing);
        }
        internal void ApplyWallpaperPalette(Color seed)
        {
            if (AppDesign.Seed.ToArgb() == seed.ToArgb()) return;
            designTransition.Finish(); transition.Finish();
            Bitmap outgoing = Visible && draft.InterfaceAnimations && !exporting ? CaptureShownClient() : null;
            AppDesign.WallpaperSeed(seed);
            ApplyVisualTheme(); RebuildQuietly();
            FinishPaletteTransition(outgoing);
        }
        void FinishPaletteTransition(Bitmap outgoing)
        {
            if (outgoing == null) return;
            var incoming = CaptureClient(); designTransition.Bounds = ClientRectangle;
            designTransition.Cover(outgoing); designTransition.Begin(incoming, 0);
        }
    }
    // Paints its own card-coloured background: a transparent button would show
    // whatever another control left in the shared double buffer.
    internal sealed class ColorSwatch : Button
    {
        readonly Color color;
        bool selected, hover;
        internal ColorSwatch(Color color)
        {
            this.color = color; Size = new Size(44, 44); FlatStyle = FlatStyle.Flat; Cursor = Cursors.Hand;
            FlatAppearance.BorderSize = 0;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            AccessibleRole = AccessibleRole.RadioButton;
        }
        internal bool Selected
        {
            get { return selected; }
            set { if (selected == value) return; selected = value; AccessibleDescription = value ? "selected" : null; Invalidate(); }
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(AppDesign.Material ? AppDesign.Container : Color.FromArgb(18, 18, 20));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float size = Math.Min(Width, Height);
            float inset = selected ? 8 : hover ? 6 : 7;
            using (var fill = new SolidBrush(color)) g.FillEllipse(fill, inset, inset, size - inset * 2, size - inset * 2);
            if (selected)
                using (var pen = new Pen(AppDesign.Ink, 2)) g.DrawEllipse(pen, 2, 2, size - 4, size - 4);
            else if (Focused && ShowFocusCues)
                using (var pen = new Pen(AppDesign.Primary, 2)) g.DrawEllipse(pen, 2, 2, size - 4, size - 4);
        }
    }
}
