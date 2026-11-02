using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
namespace WinDynamicIsland
{
    internal sealed class AppSelectionList : Control
    {
        internal event EventHandler SelectionChanged;
        readonly NotificationChoice[] choices; readonly HashSet<string> selected; readonly bool all;
        NotificationChoice[] visible; int offset, hover = -1; const int RowHeight = 34;
        internal AppSelectionList(NotificationChoice[] choices, HashSet<string> selected, bool all)
        {
            this.choices = choices;
            this.selected = selected;
            this.all = all;
            visible = choices;
            BackColor = Color.FromArgb(18, 18, 20);
            ForeColor = Color.White;
            Cursor = Cursors.Hand;
            TabStop = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            AccessibleRole = AccessibleRole.List;
        }
        internal void Filter(string text)
        {
            visible = choices.Where(c => c.Name.IndexOf(text.Trim(), StringComparison.CurrentCultureIgnoreCase) >= 0).ToArray();
            offset = 0;
            hover = -1;
            Invalidate();
        }
        void Toggle(int index)
        {
            if (all || index < 0 || index >= visible.Length)
                return;
            var choice = visible[index];
            bool was = choice.Ids.Any(id => selected.Contains(id));
            foreach (string id in choice.Ids)
                if (was)
                    selected.Remove(id);
                else
                    selected.Add(id);
            if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
            Invalidate();
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();
            if (e.Button == MouseButtons.Left)
                Toggle((e.Y + offset) / RowHeight);
            base.OnMouseDown(e);
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            int index = (e.Y + offset) / RowHeight;
            if (hover != index)
            {
                hover = index;
                Invalidate();
            }
            base.OnMouseMove(e);
        }
        protected override void OnMouseLeave(EventArgs e)
        {
            hover = -1;
            Invalidate();
            base.OnMouseLeave(e);
        }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            offset = Math.Max(0, Math.Min(Math.Max(0, visible.Length * RowHeight - Height), offset - e.Delta / 120 * RowHeight * 3));
            Invalidate();
            var handled = e as HandledMouseEventArgs;
            if (handled != null)
                handled.Handled = true;
            base.OnMouseWheel(e);
        }
        protected override bool IsInputKey(Keys key)
        {
            return key == Keys.Up || key == Keys.Down || base.IsInputKey(key);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Up)
            {
                hover = Math.Max(0, Math.Min(visible.Length - 1, hover + (e.KeyCode == Keys.Down ? 1 : -1)));
                if (hover * RowHeight < offset)
                    offset = hover * RowHeight;
                if ((hover + 1) * RowHeight > offset + Height)
                    offset = (hover + 1) * RowHeight - Height;
                Invalidate();
                e.Handled = true;
            }
            if (e.KeyCode == Keys.Space)
            {
                Toggle(hover);
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            for (int i = Math.Max(0, offset / RowHeight); i < visible.Length && i * RowHeight - offset < Height; i++)
            {
                int y = i * RowHeight - offset;
                bool check = all || visible[i].Ids.Any(id => selected.Contains(id));
                if (hover == i && !all)
                    using (var path = IslandTheme.Round(new RectangleF(0, y, Width - 9, RowHeight), 10))
                    using (var fill = new SolidBrush(Color.FromArgb(37, 37, 41)))
                        g.FillPath(fill, path);
                if (check)
                    using (var brush = new SolidBrush(IslandTheme.Accent))
                        g.FillEllipse(brush, 8, y + 8, 18, 18);
                using (var pen = new Pen(check ? Color.White : Color.FromArgb(85, 85, 93), 1.6f))
                {
                    if (!check)
                        g.DrawEllipse(pen, 8, y + 8, 18, 18);
                    else
                    {
                        pen.StartCap = pen.EndCap = LineCap.Round;
                        g.DrawLines(pen, new[] { new PointF(12, y + 17), new PointF(16, y + 21), new PointF(22, y + 13) });
                    }
                }
                TextRenderer.DrawText(g, visible[i].Name, Font, new Rectangle(38, y, Width - 55, RowHeight), Color.White, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
            if (visible.Length * RowHeight > Height)
            {
                float h = Math.Max(26, Height * Height / (float)(visible.Length * RowHeight)), top = offset * (Height - h) / Math.Max(1, visible.Length * RowHeight - Height);
                using (var brush = new SolidBrush(Color.FromArgb(85, 85, 93)))
                    g.FillRectangle(brush, Width - 4, top, 3, h);
            }
        }
    }
    internal sealed class SearchInput : TextBox
    {
        internal string Hint;
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr window, int message, IntPtr parameter, string value);
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            SendMessage(Handle, 0x1501, new IntPtr(1), Hint ?? "");
        }
    }
}
