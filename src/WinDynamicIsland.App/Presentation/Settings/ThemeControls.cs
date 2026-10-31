using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace WinDynamicIsland
{
    internal sealed class IslandScrollPanel : FlowLayoutPanel
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool ShowScrollBar(IntPtr window, int bar, bool show);
        bool hiding;
        internal IslandScrollPanel()
        {
            DoubleBuffered = true;
            BackColor = Color.Black;
        }
        void HideNative()
        {
            if (!IsHandleCreated || hiding)
                return;
            hiding = true;
            ShowScrollBar(Handle, 1, false);
            hiding = false;
        }
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            HideNative();
        }
        protected override void OnScroll(ScrollEventArgs e)
        {
            base.OnScroll(e);
            HideNative();
            Invalidate();
        }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            HideNative();
            Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (DisplayRectangle.Height <= ClientSize.Height)
                return;
            float height = Math.Max(32, ClientSize.Height * ClientSize.Height / (float)DisplayRectangle.Height);
            float top = -AutoScrollPosition.Y * (ClientSize.Height - height) / Math.Max(1, DisplayRectangle.Height - ClientSize.Height);
            using (var path = IslandTheme.Round(new RectangleF(Width - 5, top, 3, height), 2))
            using (var fill = new SolidBrush(Color.FromArgb(85, 85, 92)))
                e.Graphics.FillPath(fill, path);
        }
    }
    internal static class IslandTheme
    {
        internal static readonly Color Accent = Color.FromArgb(10, 132, 255), Secondary = Color.FromArgb(152, 152, 160);
        internal static GraphicsPath Round(RectangleF r, float radius)
        {
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            var path = new GraphicsPath();
            if (d < 1)
            {
                path.AddRectangle(r);
                return path;
            }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
        internal static void Card(Graphics g, Rectangle bounds, bool selected = false, int radius = 19)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var box = new RectangleF(bounds.X + .7f, bounds.Y + .7f, bounds.Width - 1.4f, bounds.Height - 1.4f);
            using (var path = Round(box, radius))
            {
                using (var fill = new SolidBrush(selected ? Color.FromArgb(35, 35, 39) : Color.FromArgb(18, 18, 20)))
                    g.FillPath(fill, path);
                if (Program.Settings.GlassStyle)
                {
                    using (var sheen = new LinearGradientBrush(box, Color.FromArgb(selected ? 26 : 16, Color.White), Color.FromArgb(0, Color.White), 105))
                        g.FillPath(sheen, path);
                    using (var outline = new Pen(Color.FromArgb(selected ? 56 : 25, Color.White), .8f))
                        g.DrawPath(outline, path);
                }
            }
        }
    }
    internal sealed class GlassCard : Panel
    {
        internal GlassCard()
        {
            DoubleBuffered = true;
            BackColor = Color.Black;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            IslandTheme.Card(e.Graphics, ClientRectangle);
            base.OnPaint(e);
        }
    }
    internal class GlassButton : Button
    {
        bool hover;
        internal bool Primary, Selected, Navigation;
        internal GlassButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = Color.Black;
            ForeColor = Color.White;
            Cursor = Cursors.Hand;
        }
        protected override void OnMouseEnter(EventArgs e)
        {
            hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }
        protected override void OnMouseLeave(EventArgs e)
        {
            hover = false;
            Invalidate();
            base.OnMouseLeave(e);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (Parent is GlassCard)
            {
                var state = e.Graphics.Save();
                e.Graphics.TranslateTransform(-Left, -Top);
                InvokePaintBackground(Parent, new PaintEventArgs(e.Graphics, Parent.ClientRectangle));
                InvokePaint(Parent, new PaintEventArgs(e.Graphics, Parent.ClientRectangle));
                e.Graphics.Restore(state);
            }
            else
                e.Graphics.Clear(Parent == null ? Color.Black : Parent.BackColor);
            if (Primary)
                using (var path = IslandTheme.Round(new RectangleF(1, 1, Width - 2, Height - 2), Height / 2f))
                using (var brush = new SolidBrush(hover ? Color.FromArgb(225, 225, 230) : Color.White))
                    e.Graphics.FillPath(brush, path);
            else if (!Navigation || Selected || hover)
                IslandTheme.Card(e.Graphics, ClientRectangle, Selected || hover, Navigation ? 15 : 18);
            Color color = Primary ? Color.Black : Enabled ? Color.White : IslandTheme.Secondary;
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(Padding.Left + 12, 0, Width - Padding.Left - 24, Height), color, (TextAlign == ContentAlignment.MiddleCenter ? TextFormatFlags.HorizontalCenter : TextFormatFlags.Left) | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (Focused)
                using (var path = IslandTheme.Round(new RectangleF(2, 2, Width - 4, Height - 4), 17))
                using (var pen = new Pen(IslandTheme.Accent, 1))
                    e.Graphics.DrawPath(pen, path);
        }
    }
    internal sealed class IslandMenuRenderer : ToolStripProfessionalRenderer
    {
        internal static void Style(ContextMenuStrip menu)
        {
            menu.Renderer = new IslandMenuRenderer();
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = false;
            menu.Padding = new Padding(6);
            menu.BackColor = Color.FromArgb(24, 24, 27);
            menu.ForeColor = Color.White;
            menu.SizeChanged += delegate
            {
                if (menu.Width < 2 || menu.Height < 2)
                    return;
                using (var path = IslandTheme.Round(new RectangleF(0, 0, menu.Width, menu.Height), 13))
                {
                    var previous = menu.Region;
                    menu.Region = new Region(path);
                    if (previous != null)
                        previous.Dispose();
                }
            };
        }
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            e.Graphics.Clear(Color.FromArgb(24, 24, 27));
        }
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = IslandTheme.Round(new RectangleF(.5f, .5f, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1), 13))
            using (var pen = new Pen(Color.FromArgb(65, 65, 70), 1))
                e.Graphics.DrawPath(pen, path);
        }
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected)
                return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = IslandTheme.Round(new RectangleF(0, 0, e.Item.Width, e.Item.Height), 8))
            using (var brush = new SolidBrush(IslandTheme.Accent))
                e.Graphics.FillPath(brush, path);
        }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = Color.White;
            base.OnRenderItemText(e);
        }
    }
    internal sealed class ToggleSwitch : CheckBox
    {
        readonly Timer timer = new Timer { Interval = 16 }; float amount; DateTime previous;
        internal ToggleSwitch()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Color.Black;
            ForeColor = Color.White;
            Cursor = Cursors.Hand;
            timer.Tick += Animate;
        }
        protected override void OnCheckedChanged(EventArgs e)
        {
            previous = DateTime.UtcNow;
            timer.Start();
            base.OnCheckedChanged(e);
        }
        void Animate(object sender, EventArgs e)
        {
            float step = (float)(DateTime.UtcNow - previous).TotalMilliseconds / 150;
            previous = DateTime.UtcNow;
            amount = Checked ? Math.Min(1, amount + step) : Math.Max(0, amount - step);
            if (amount == 0 || amount == 1)
                timer.Stop();
            Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Color.Black);
            IslandTheme.Card(g, ClientRectangle);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(20, 0, Width - 112, Height), Color.White, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            var track = new RectangleF(Width - 73, (Height - 28) / 2f, 50, 28);
            using (var path = IslandTheme.Round(track, 14))
            using (var brush = new SolidBrush(Color.FromArgb((int)(58 + (10 - 58) * amount), (int)(58 + (132 - 58) * amount), (int)(64 + (255 - 64) * amount))))
                g.FillPath(brush, path);
            using (var shadow = new SolidBrush(Color.FromArgb(55, Color.Black)))
                g.FillEllipse(shadow, track.X + 2 + 22 * amount, track.Y + 3, 25, 25);
            g.FillEllipse(Brushes.White, track.X + 2 + 22 * amount, track.Y + 2, 24, 24);
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            amount = Checked ? 1 : 0;
            base.OnHandleCreated(e);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                timer.Dispose();
            base.Dispose(disposing);
        }
    }
    internal sealed class SnapSlider : Control
    {
        readonly string label; readonly int min, max; readonly int[] magnets;
        int value; bool dragging;
        internal int Value
        {
            get
            {
                return value;
            }
            set
            {
                int next = Math.Max(min, Math.Min(max, value));
                if (this.value == next)
                    return;
                this.value = next;
                Invalidate();
                if (ValueChanged != null)
                    ValueChanged(this, EventArgs.Empty);
            }
        }
        internal event EventHandler ValueChanged;
        internal SnapSlider(string label, int value, int min, int max)
        {
            this.label = label;
            this.min = min;
            this.max = max;
            this.value = value;
            magnets = new[] { min, min + (max - min) / 4, min + (max - min) / 2, value, max };
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            Size = new Size(620, 91);
            BackColor = Color.Black;
            ForeColor = Color.White;
            TabStop = true;
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.Slider;
            AccessibleName = label;
        }
        internal int Snap(double fraction)
        {
            return Math.Max(min, Math.Min(max, (int)Math.Round(min + Math.Max(0, Math.Min(1, fraction)) * (max - min))));
        }
        void At(int x)
        {
            double width = Math.Max(1, Width - 52), position = Math.Max(0, Math.Min(width, x - 26));
            foreach (int stop in magnets)
            {
                double at = (stop - min) * width / Math.Max(1, max - min);
                if (Math.Abs(position - at) <= 6)
                {
                    Value = stop;
                    return;
                }
            }
            Value = Snap(position / width);
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && e.Y >= 40)
            {
                Focus();
                dragging = true;
                Capture = true;
                At(e.X);
            }
            base.OnMouseDown(e);
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (dragging)
                At(e.X);
            base.OnMouseMove(e);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            dragging = false;
            Capture = false;
            base.OnMouseUp(e);
        }
        protected override bool IsInputKey(Keys key)
        {
            return key == Keys.Left || key == Keys.Right || base.IsInputKey(key);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Down)
            {
                Value--;
                e.Handled = true;
            }
            if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Up)
            {
                Value++;
                e.Handled = true;
            }
            if (e.KeyCode == Keys.Home)
                Value = min;
            if (e.KeyCode == Keys.End)
                Value = max;
            base.OnKeyDown(e);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Color.Black);
            IslandTheme.Card(g, ClientRectangle);
            TextRenderer.DrawText(g, label, Font, new Rectangle(20, 13, Width - 95, 24), Color.White, TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, Value.ToString(), Font, new Rectangle(Width - 70, 13, 48, 24), IslandTheme.Secondary, TextFormatFlags.Right);
            float length = Width - 52, y = 63, x = 26 + length * (Value - min) / Math.Max(1, max - min);
            using (var pen = new Pen(Color.FromArgb(53, 53, 58), 5))
            {
                pen.StartCap = pen.EndCap = LineCap.Round;
                g.DrawLine(pen, 26, y, Width - 26, y);
            }
            using (var pen = new Pen(Color.White, 5))
            {
                pen.StartCap = pen.EndCap = LineCap.Round;
                if (x > 26)
                    g.DrawLine(pen, 26, y, x, y);
            }
            using (var brush = new SolidBrush(Color.FromArgb(110, 110, 120)))
                foreach (int stop in magnets)
                {
                    float at = 26 + length * (stop - min) / Math.Max(1, max - min);
                    g.FillEllipse(brush, at - 1, y + 10, 2, 2);
                }
            using (var shadow = new SolidBrush(Color.FromArgb(50, Color.White)))
                g.FillEllipse(shadow, x - 12, y - 12, 24, 24);
            g.FillEllipse(Brushes.White, x - 9, y - 9, 18, 18);
        }
    }
}
