using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using WinDynamicIsland.Core;
namespace WinDynamicIsland
{
    internal sealed class ColorPickerDialog : Form
    {
        internal Color SelectedColor { get; private set; }
        readonly SnapSlider red, green, blue;
        readonly TextBox hex;
        readonly Label validation;
        readonly Panel preview;
        bool updating;
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
        internal ColorPickerDialog(Color initial)
        {
            SelectedColor = initial;
            Text = L.T("ColorPickerTitle"); Font = AppTypography.Create(10.5f);
            BackColor = AppDesign.Surface; ForeColor = AppDesign.Ink;
            StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog; DoubleBuffered = true;
            MaximizeBox = MinimizeBox = ShowInTaskbar = false; ClientSize = new Size(500, 474);
            preview = new BufferedPanel { Location = new Point(24, 24), Size = new Size(58, 48) };
            preview.Paint += delegate(object sender, PaintEventArgs e) { e.Graphics.Clear(AppDesign.Surface); e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias; AppDesign.Fill(e.Graphics, new RectangleF(0, 0, 57, 47), 16, SelectedColor); };
            // A borderless TextBox over a rounded field painted by the dialog.
            hex = new TextBox { Text = MaterialPalette.Hex(initial), Font = AppTypography.Create(15, FontStyle.Bold), Width = 170, BackColor = AppDesign.Container, ForeColor = AppDesign.Ink, BorderStyle = BorderStyle.None, CharacterCasing = CharacterCasing.Upper, MaxLength = 7, AccessibleName = "HEX #RRGGBB" };
            hex.Location = new Point(HexField.X + 15, HexField.Y + Math.Max(0, (HexField.Height - hex.PreferredHeight) / 2));
            hex.GotFocus += delegate { Invalidate(HexField); };
            hex.LostFocus += delegate { Invalidate(HexField); };
            validation = new Label { Text = "", Location = new Point(24, 82), Size = new Size(452, 22), ForeColor = AppDesign.Error };
            // Colour channels are continuous: no magnetic stops pulling the thumb.
            red = new SnapSlider(L.T("RedChannel"), initial.R, 0, 255) { Location = new Point(24, 112), Width = 452, Font = Font, Magnetic = false };
            green = new SnapSlider(L.T("GreenChannel"), initial.G, 0, 255) { Location = new Point(24, 210), Width = 452, Font = Font, Magnetic = false };
            blue = new SnapSlider(L.T("BlueChannel"), initial.B, 0, 255) { Location = new Point(24, 308), Width = 452, Font = Font, Magnetic = false };
            red.ValueChanged += ChannelChanged; green.ValueChanged += ChannelChanged; blue.ValueChanged += ChannelChanged;
            hex.TextChanged += delegate
            {
                if (updating) return;
                int value;
                if (hex.Text.Length != 7 || hex.Text[0] != '#' || !int.TryParse(hex.Text.Substring(1), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out value)) { validation.Text = L.T("InvalidColor"); return; }
                updating = true;
                try { SelectedColor = MaterialPalette.Parse(hex.Text); red.Value = SelectedColor.R; green.Value = SelectedColor.G; blue.Value = SelectedColor.B; validation.Text = ""; preview.Invalidate(); }
                finally { updating = false; }
            };
            var cancel = new GlassButton { Text = L.T("CancelColor"), Location = new Point(162, 414), Size = new Size(112, 44), TextAlign = ContentAlignment.MiddleCenter, DialogResult = DialogResult.Cancel };
            var apply = new GlassButton { Text = L.T("ApplyColor"), Location = new Point(286, 414), Size = new Size(190, 44), TextAlign = ContentAlignment.MiddleCenter, Primary = true };
            apply.Click += delegate { if (validation.Text.Length != 0) { hex.Focus(); return; } DialogResult = DialogResult.OK; Close(); };
            Controls.AddRange(new Control[] { preview, hex, validation, red, green, blue, cancel, apply }); AcceptButton = apply; CancelButton = cancel;
            ActiveControl = red;
        }
        static readonly Rectangle HexField = new Rectangle(98, 24, 200, 48);
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            AppDesign.Fill(e.Graphics, new RectangleF(HexField.X, HexField.Y, HexField.Width - 1, HexField.Height - 1), 16, AppDesign.Container);
            if (hex.Focused)
                using (var path = IslandTheme.Round(new RectangleF(HexField.X + 1, HexField.Y + 1, HexField.Width - 3, HexField.Height - 3), 15))
                using (var pen = new Pen(AppDesign.Primary, 2)) e.Graphics.DrawPath(pen, path);
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (HexField.Contains(e.Location)) hex.Focus();
        }
        void ChannelChanged(object sender, EventArgs e)
        {
            if (updating) return;
            updating = true;
            try { SelectedColor = Color.FromArgb(red.Value, green.Value, blue.Value); hex.Text = MaterialPalette.Hex(SelectedColor); validation.Text = ""; preview.Invalidate(); }
            finally { updating = false; }
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { int dark = 1; DwmSetWindowAttribute(Handle, 20, ref dark, 4); int color = AppDesign.Surface.R | AppDesign.Surface.G << 8 | AppDesign.Surface.B << 16; DwmSetWindowAttribute(Handle, 35, ref color, 4); } catch { }
        }
    }
}
