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
            StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = ShowInTaskbar = false; ClientSize = new Size(500, 474);
            preview = new BufferedPanel { Location = new Point(24, 24), Size = new Size(58, 48) };
            preview.Paint += delegate(object sender, PaintEventArgs e) { e.Graphics.Clear(AppDesign.Surface); AppDesign.Fill(e.Graphics, new RectangleF(0, 0, 58, 48), 18, SelectedColor); };
            hex = new TextBox { Text = MaterialPalette.Hex(initial), Font = AppTypography.Create(15, FontStyle.Bold), Location = new Point(100, 30), Width = 190, BackColor = AppDesign.Container, ForeColor = AppDesign.Ink, BorderStyle = BorderStyle.None, CharacterCasing = CharacterCasing.Upper, MaxLength = 7, AccessibleName = "HEX #RRGGBB" };
            validation = new Label { Text = "", Location = new Point(24, 82), Size = new Size(452, 22), ForeColor = AppDesign.Error };
            red = new SnapSlider(L.T("RedChannel"), initial.R, 0, 255) { Location = new Point(24, 112), Width = 452, Font = Font };
            green = new SnapSlider(L.T("GreenChannel"), initial.G, 0, 255) { Location = new Point(24, 210), Width = 452, Font = Font };
            blue = new SnapSlider(L.T("BlueChannel"), initial.B, 0, 255) { Location = new Point(24, 308), Width = 452, Font = Font };
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
