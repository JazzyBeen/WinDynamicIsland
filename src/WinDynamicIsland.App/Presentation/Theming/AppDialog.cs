using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace WinDynamicIsland
{
    internal static class AppDialog
    {
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
        internal static void Show(IWin32Window owner, string text, string title)
        {
            if (!AppDesign.Material) { MessageBox.Show(owner, text, title, MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            using (var dialog = new Form { Text = title, BackColor = AppDesign.Surface, ForeColor = AppDesign.Ink, Font = AppTypography.Create(11), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false })
            {
                var message = new Label { Text = text, Location = new Point(28, 24), Width = 416, ForeColor = AppDesign.Ink };
                message.Height = Math.Min(360, TextRenderer.MeasureText(text, dialog.Font, new Size(416, 0), TextFormatFlags.WordBreak).Height + 12);
                var okay = new GlassButton { Text = "OK", Primary = true, TextAlign = ContentAlignment.MiddleCenter, DialogResult = DialogResult.OK, Size = new Size(100, 44), Location = new Point(344, message.Bottom + 20) };
                dialog.ClientSize = new Size(472, okay.Bottom + 24);
                dialog.Controls.Add(message); dialog.Controls.Add(okay); dialog.AcceptButton = okay; dialog.CancelButton = okay;
                dialog.HandleCreated += delegate { try { int dark = 1; DwmSetWindowAttribute(dialog.Handle, 20, ref dark, 4); int color = AppDesign.Surface.R | AppDesign.Surface.G << 8 | AppDesign.Surface.B << 16; DwmSetWindowAttribute(dialog.Handle, 35, ref color, 4); } catch { } };
                dialog.ShowDialog(owner);
            }
        }
    }
}
