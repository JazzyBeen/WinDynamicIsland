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
        static readonly string[][] SwatchColors =
        {
            new[] { "#EC5464", "Малиновый", "Crimson" },
            new[] { "#D81B60", "Роза", "Rose" },
            new[] { "#8E24AA", "Пурпурный", "Purple" },
            new[] { "#5E35B1", "Глубокий фиолетовый", "Deep purple" },
            new[] { "#3949AB", "Индиго", "Indigo" },
            new[] { "#1E88E5", "Синий", "Blue" },
            new[] { "#039BE5", "Небесный", "Sky blue" },
            new[] { "#00ACC1", "Бирюзовый", "Cyan" },
            new[] { "#00897B", "Морская волна", "Teal" },
            new[] { "#43A047", "Зелёный", "Green" },
            new[] { "#7CB342", "Светло-зелёный", "Light green" },
            new[] { "#C0CA33", "Лайм", "Lime" },
            new[] { "#FDD835", "Жёлтый", "Yellow" },
            new[] { "#FFB300", "Янтарный", "Amber" },
            new[] { "#FB8C00", "Оранжевый", "Orange" },
            new[] { "#F4511E", "Тёмно-оранжевый", "Deep orange" },
            new[] { "#6D4C41", "Коричневый", "Brown" },
            new[] { "#757575", "Серый", "Grey" },
            new[] { "#546E7A", "Серо-синий", "Blue grey" },
            new[] { "#880E4F", "Тёмно-розовый", "Dark pink" },
            new[] { "#7B1F3A", "Вино", "Wine" },
            new[] { "#FF7043", "Коралловый", "Coral" },
            new[] { "#9575CD", "Лавандовый", "Lavender" },
            new[] { "#1A237E", "Тёмно-синий", "Navy" },
            new[] { "#26A69A", "Мятный", "Mint" },
            new[] { "#2E7D32", "Лесной", "Forest" },
            new[] { "#C9A227", "Золотой", "Gold" },
            new[] { "#F48FB1", "Сакура", "Sakura" },
            new[] { "#FFAB91", "Персиковый", "Peach" },
            new[] { "#CE93D8", "Сиреневый", "Lilac" },
            new[] { "#E040FB", "Фуксия", "Fuchsia" },
            new[] { "#C2185B", "Рубин", "Ruby" },
            new[] { "#AD1457", "Вишня", "Cherry" },
            new[] { "#0277BD", "Океан", "Ocean" },
            new[] { "#4DD0E1", "Аква", "Aqua" },
            new[] { "#00C853", "Изумруд", "Emerald" },
            new[] { "#827717", "Оливковый", "Olive" },
            new[] { "#D7B98E", "Песок", "Sand" },
            new[] { "#BF5B3A", "Терракота", "Terracotta" },
            new[] { "#455A64", "Графит", "Graphite" },
            new[] { "#283593", "Полночь", "Midnight" },
            new[] { "#6750A4", "Фирменный", "Brand" }
        };
        const int SwatchColumns = 11;
        readonly List<ColorSwatch> swatches = new List<ColorSwatch>();
        void PaletteSettings()
        {
            var row = Row("PaletteSource");
            string[] modes = { "Default", "Custom", "Wallpaper", "Ori" };
            var choice = new SelectionButton(new[] { L.T("PaletteDefault"), L.T("PaletteCustom"), L.T("PaletteWallpaper"), L.T("PaletteOri") }, Math.Max(0, Array.IndexOf(modes, draft.MaterialColorMode))) { Height = 44, Width = 265, Top = 16, Font = Font, AccessibleName = L.T("PaletteSource") };
            row.Controls.Add(choice);
            Action layout = delegate { choice.Left = row.Width - choice.Width - 20; row.Controls[0].Width = Math.Max(80, choice.Left - 24); };
            row.Resize += delegate { layout(); }; layout();
            choice.SelectionChanged += delegate { ChangeColors(modes[choice.SelectedIndex], draft.MaterialSeedColor); };
            if (draft.MaterialColorMode == "Wallpaper") Help("PaletteWallpaperHelp");
            if (draft.MaterialColorMode == "Ori") Help("PaletteOriHelp");
            swatches.Clear();
            if (draft.MaterialColorMode == "Custom" || draft.MaterialColorMode == "Wallpaper")
            {
                var styleRow = Row("PaletteStyle");
                string[] styles = MaterialPalette.Styles;
                var names = new string[styles.Length];
                for (int i = 0; i < styles.Length; i++) names[i] = L.T("Style" + styles[i]);
                var style = new SelectionButton(names, Math.Max(0, Array.IndexOf(styles, draft.MaterialPaletteStyle))) { Height = 44, Width = 265, Top = 16, Font = Font, AccessibleName = L.T("PaletteStyle") };
                styleRow.Controls.Add(style);
                Action styleLayout = delegate { style.Left = styleRow.Width - style.Width - 20; styleRow.Controls[0].Width = Math.Max(80, style.Left - 24); };
                styleRow.Resize += delegate { styleLayout(); }; styleLayout();
                style.SelectionChanged += delegate { ChangeLook(draft.MaterialColorMode, draft.MaterialSeedColor, styles[style.SelectedIndex], draft.MaterialPureBlack); };
            }
            if (draft.MaterialColorMode == "Custom")
            {
                int rows = (SwatchColors.Length + SwatchColumns - 1) / SwatchColumns;
                var card = new GlassCard { Width = 620, Height = 20 + rows * 52, Margin = new Padding(0, 4, 0, 4) };
                var tips = new ToolTip();
                for (int i = 0; i < SwatchColors.Length; i++)
                {
                    string hex = SwatchColors[i][0];
                    var swatch = new ColorSwatch(MaterialPalette.Parse(hex)) { Location = new Point(14 + i % SwatchColumns * 52, 10 + i / SwatchColumns * 52), AccessibleName = L.Language == "ru" ? SwatchColors[i][1] : SwatchColors[i][2], Tag = hex };
                    swatch.Click += delegate { ChangeColors("Custom", hex); };
                    tips.SetToolTip(swatch, swatch.AccessibleName);
                    swatches.Add(swatch);
                    card.Controls.Add(swatch);
                }
                card.Disposed += delegate { tips.Dispose(); };
                MarkSelectedSwatch();
                body.Controls.Add(card);
                body.Controls.Add(Button(L.T("PickColor"), () =>
                {
                    using (var picker = new ColorPickerDialog(MaterialPalette.Parse(draft.MaterialSeedColor)))
                        if (picker.ShowDialog(this) == DialogResult.OK) ChangeColors("Custom", MaterialPalette.Hex(picker.SelectedColor));
                }));
            }
            var black = new ToggleSwitch { Text = L.T("PalettePureBlack"), Checked = draft.MaterialPureBlack, Width = 620, Height = 60, Padding = new Padding(14, 0, 12, 0), Margin = new Padding(0, 4, 0, 4), BackColor = surface, FlatStyle = FlatStyle.Standard };
            black.CheckedChanged += delegate { ChangeLook(draft.MaterialColorMode, draft.MaterialSeedColor, draft.MaterialPaletteStyle, black.Checked); };
            body.Controls.Add(black);
            Help("PalettePureBlackHelp");
        }
        void MarkSelectedSwatch()
        {
            foreach (var swatch in swatches)
                swatch.Selected = draft.MaterialColorMode == "Custom" && string.Equals(draft.MaterialSeedColor, (string)swatch.Tag, StringComparison.OrdinalIgnoreCase);
        }
        void ChangeColors(string mode, string seed) { ChangeLook(mode, seed, draft.MaterialPaletteStyle, draft.MaterialPureBlack); }
        void ChangeLook(string mode, string seed, string style, bool pureBlack)
        {
            if (rebuilding) return;
            if (mode == draft.MaterialColorMode && string.Equals(seed, draft.MaterialSeedColor, StringComparison.OrdinalIgnoreCase) && style == draft.MaterialPaletteStyle && pureBlack == draft.MaterialPureBlack) return;
            Collect();
            designTransition.Finish(); transition.Finish();
            Bitmap outgoing = Visible && draft.InterfaceAnimations && !exporting ? CaptureShownClient() : null;
            string previousMode = draft.MaterialColorMode, previousSeed = draft.MaterialSeedColor, previousStyle = draft.MaterialPaletteStyle; bool previousBlack = draft.MaterialPureBlack;
            // A new seed in the same mode only recolours: every control reads the
            // palette while painting, so the page is kept instead of rebuilt.
            bool sameLayout = mode == previousMode;
            draft.MaterialColorMode = mode; draft.MaterialSeedColor = seed; draft.MaterialPaletteStyle = style; draft.MaterialPureBlack = pureBlack;
            if (!PersistDraft()) { draft.MaterialColorMode = previousMode; draft.MaterialSeedColor = previousSeed; draft.MaterialPaletteStyle = previousStyle; draft.MaterialPureBlack = previousBlack; if (outgoing != null) outgoing.Dispose(); return; }
            if (apply == null) { Program.Settings.MaterialColorMode = mode; Program.Settings.MaterialSeedColor = seed; Program.Settings.MaterialPaletteStyle = style; Program.Settings.MaterialPureBlack = pureBlack; AppDesign.ConfigurePalette(); }
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
