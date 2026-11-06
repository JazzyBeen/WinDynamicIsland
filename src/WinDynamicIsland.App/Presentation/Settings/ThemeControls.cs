using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace WinDynamicIsland
{
    internal sealed class IslandScrollPanel : FlowLayoutPanel
    {
        readonly ScrollViewport viewport;
        readonly ScrollBar scrollbar;
        int offset, contentHeight; bool arranging;
        double wheelRemainder;
        internal Panel Viewport { get { return viewport; } }
        internal int ScrollOffset
        {
            get
            {
                return offset;
            }
        }
        internal int MaximumOffset
        {
            get
            {
                return Math.Max(0, contentHeight - viewport.ClientSize.Height);
            }
        }
        internal IslandScrollPanel()
        {
            DoubleBuffered = true;
            BackColor = IslandTheme.Background;
            AutoScroll = false;
            ResizeRedraw = true;
            viewport = new ScrollViewport(this) { Dock = DockStyle.Fill, BackColor = BackColor, Size = Size };
            scrollbar = new ScrollBar(this) { BackColor = BackColor };
            viewport.Controls.Add(this);
            viewport.Controls.Add(scrollbar);
            scrollbar.BringToFront();
            viewport.SizeChanged += delegate { ResizeViewport(); };
            ResizeViewport();
        }
        void ResizeViewport()
        {
            if (arranging || IsDisposed) return;
            int width = Math.Max(1, viewport.ClientSize.Width - 12);
            if (Width != width) Width = width;
            else PerformLayout();
        }
        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            TrackFocus(e.Control);
        }
        void TrackFocus(Control control)
        {
            control.Enter += delegate
            {
                if (!control.IsHandleCreated || !IsHandleCreated)
                    return;
                var top = viewport.PointToClient(control.PointToScreen(Point.Empty)).Y;
                if (top < 0)
                    ScrollTo(offset + top - 8);
                else if (top + control.Height > viewport.ClientSize.Height)
                    ScrollTo(offset + top + control.Height - viewport.ClientSize.Height + 8);
            };
            control.ControlAdded += delegate (object sender, ControlEventArgs e)
            {
                TrackFocus(e.Control);
            };
            foreach (Control child in control.Controls)
                TrackFocus(child);
        }
        protected override void OnLayout(LayoutEventArgs e)
        {
            if (arranging || viewport == null || scrollbar == null)
                return;
            arranging = true;
            try
            {
                int width = Math.Max(80, Math.Min(620, ClientSize.Width - Padding.Horizontal - 8));
                foreach (Control child in Controls)
                    if (child.Width != width)
                        child.Width = width;
                base.OnLayout(e);
                contentHeight = Padding.Top + Padding.Bottom;
                foreach (Control child in Controls)
                    contentHeight = Math.Max(contentHeight, child.Bottom + child.Margin.Bottom + Padding.Bottom);
                Height = Math.Max(viewport.ClientSize.Height, contentHeight);
                offset = Math.Max(0, Math.Min(MaximumOffset, offset));
                Top = -offset;
                scrollbar.SetBounds(viewport.ClientSize.Width - 12, 0, 12, viewport.ClientSize.Height);
                scrollbar.Visible = MaximumOffset > 0;
                scrollbar.Invalidate();
            }
            finally { arranging = false; }
            Invalidate();
        }
        internal void ScrollTo(int value)
        {
            int next = Math.Max(0, Math.Min(MaximumOffset, value));
            if (next == offset)
                return;
            offset = next;
            // Move one clipped surface; card positions and their layout stay unchanged.
            Top = -offset;
            scrollbar.Invalidate();
        }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            Wheel(e);
            base.OnMouseWheel(e);
        }
        void Wheel(MouseEventArgs e)
        {
            wheelRemainder -= e.Delta * 54d / 120;
            int pixels = (int)wheelRemainder;
            wheelRemainder -= pixels;
            ScrollTo(offset + pixels);
            var handled = e as HandledMouseEventArgs;
            if (handled != null)
                handled.Handled = true;
        }
        sealed class ScrollViewport : Panel
        {
            readonly IslandScrollPanel owner;
            internal ScrollViewport(IslandScrollPanel owner)
            {
                this.owner = owner;
                DoubleBuffered = true;
                AutoScroll = false;
            }
            protected override void OnMouseWheel(MouseEventArgs e)
            {
                owner.Wheel(e);
                base.OnMouseWheel(e);
            }
        }
        sealed class ScrollBar : Control
        {
            readonly IslandScrollPanel owner;
            bool dragging; float dragOffset;
            internal ScrollBar(IslandScrollPanel owner)
            {
                this.owner = owner;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                TabStop = false;
            }
            RectangleF Thumb
            {
                get
                {
                    float h = Math.Min(Height, Math.Max(32, Height * (float)Height / Math.Max(1, owner.contentHeight)));
                    return new RectangleF(5, owner.MaximumOffset == 0 ? 0 : owner.offset * (Height - h) / owner.MaximumOffset, 3, h);
                }
            }
            protected override void OnMouseDown(MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left && owner.MaximumOffset > 0)
                {
                    var thumb = Thumb;
                    dragOffset = e.Y >= thumb.Top && e.Y <= thumb.Bottom ? e.Y - thumb.Y : thumb.Height / 2;
                    dragging = true;
                    Capture = true;
                    Drag(e.Y);
                }
                base.OnMouseDown(e);
            }
            void Drag(int y)
            {
                owner.ScrollTo((int)Math.Round((y - dragOffset) * owner.MaximumOffset / Math.Max(1, Height - Thumb.Height)));
            }
            protected override void OnMouseMove(MouseEventArgs e)
            {
                if (dragging) Drag(e.Y);
                base.OnMouseMove(e);
            }
            protected override void OnMouseUp(MouseEventArgs e)
            {
                if (dragging) Drag(e.Y);
                dragging = false;
                Capture = false;
                base.OnMouseUp(e);
            }
            protected override void OnMouseCaptureChanged(EventArgs e)
            {
                if (!Capture) dragging = false;
                base.OnMouseCaptureChanged(e);
            }
            protected override void OnMouseWheel(MouseEventArgs e)
            {
                owner.Wheel(e);
                base.OnMouseWheel(e);
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                if (owner.MaximumOffset <= 0) return;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = IslandTheme.Round(Thumb, 2))
                using (var brush = new SolidBrush(IslandTheme.Secondary)) e.Graphics.FillPath(brush, path);
            }
        }
    }
    internal static class IslandTheme
    {
        internal static Color Accent { get { return AppDesign.Material ? AppDesign.Primary : Color.FromArgb(10, 132, 255); } }
        internal static Color Secondary { get { return AppDesign.Material ? AppDesign.Muted : Color.FromArgb(152, 152, 160); } }
        internal static Color Foreground { get { return AppDesign.Material ? AppDesign.Ink : Color.White; } }
        internal static Color Background { get { return AppDesign.Material ? AppDesign.Surface : Color.Black; } }
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
            if (AppDesign.Material)
            {
                AppDesign.Fill(g, box, selected ? bounds.Height / 2f : 20, selected ? AppDesign.Secondary : AppDesign.Container);
                return;
            }
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
            BackColor = IslandTheme.Background;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            IslandTheme.Card(e.Graphics, ClientRectangle);
            base.OnPaint(e);
        }
    }
    internal class GlassButton : Button
    {
        bool hover, pressed, visualDisposed;
        float hoverAmount, pressAmount;
        readonly Timer interactionTimer = new FrameTimer();
        DateTime interactionAt;
        internal bool Primary, Selected, Navigation;
        internal GlassButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = IslandTheme.Background;
            ForeColor = IslandTheme.Foreground;
            Cursor = Cursors.Hand;
            interactionTimer.Tick += delegate
            {
                float dt = (float)(PreciseClock.UtcNow - interactionAt).TotalMilliseconds;
                interactionAt = PreciseClock.UtcNow;
                hoverAmount = Approach(hoverAmount, hover ? 1 : 0, dt / 120);
                pressAmount = Approach(pressAmount, pressed ? 1 : 0, dt / 85);
                if (!Program.Settings.InterfaceAnimations)
                {
                    hoverAmount = hover ? 1 : 0;
                    pressAmount = pressed ? 1 : 0;
                }
                if (hoverAmount == (hover ? 1 : 0) && pressAmount == (pressed ? 1 : 0))
                    interactionTimer.Stop();
                Invalidate();
            };
        }
        static float Approach(float value, float target, float step)
        {
            return value < target ? Math.Min(target, value + step) : Math.Max(target, value - step);
        }
        void AnimateInteraction()
        {
            if (visualDisposed)
                return;
            interactionAt = PreciseClock.UtcNow;
            interactionTimer.Start();
            Invalidate();
        }
        protected override void OnMouseEnter(EventArgs e)
        {
            hover = true;
            AnimateInteraction();
            base.OnMouseEnter(e);
        }
        protected override void OnMouseLeave(EventArgs e)
        {
            hover = false;
            pressed = false;
            AnimateInteraction();
            base.OnMouseLeave(e);
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                pressed = true;
                AnimateInteraction();
            }
            base.OnMouseDown(e);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            pressed = false;
            AnimateInteraction();
            base.OnMouseUp(e);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (Parent is GlassCard || Parent is AnimatedNavigationPanel)
            {
                var state = e.Graphics.Save();
                e.Graphics.TranslateTransform(-Left, -Top);
                InvokePaintBackground(Parent, new PaintEventArgs(e.Graphics, Parent.ClientRectangle));
                InvokePaint(Parent, new PaintEventArgs(e.Graphics, Parent.ClientRectangle));
                e.Graphics.Restore(state);
            }
            else
                e.Graphics.Clear(Parent == null ? Color.Black : Parent.BackColor);
            var drawing = e.Graphics.Save();
            if (pressAmount > 0)
            {
                float scale = 1 - .018f * pressAmount;
                e.Graphics.TranslateTransform(Width * (1 - scale) / 2, Height * (1 - scale) / 2);
                e.Graphics.ScaleTransform(scale, scale);
            }
            if (AppDesign.Material)
            {
                PaintMaterial(e.Graphics);
                e.Graphics.Restore(drawing);
                return;
            }
            if (Primary)
                using (var path = IslandTheme.Round(new RectangleF(1, 1, Width - 2, Height - 2), Height / 2f))
                using (var brush = new SolidBrush(Color.FromArgb((int)(255 - 30 * hoverAmount), (int)(255 - 30 * hoverAmount), (int)(255 - 25 * hoverAmount))))
                    e.Graphics.FillPath(brush, path);
            else if (!Navigation)
            {
                IslandTheme.Card(e.Graphics, ClientRectangle, false, 18);
                using (var path = IslandTheme.Round(new RectangleF(1, 1, Width - 2, Height - 2), 18))
                using (var fill = new SolidBrush(Color.FromArgb((int)(hoverAmount * 12), Color.White)))
                    e.Graphics.FillPath(fill, path);
            }
            else if (hoverAmount > 0 && !Selected)
            {
                using (var path = IslandTheme.Round(new RectangleF(1, 1, Width - 2, Height - 2), 15))
                using (var fill = new SolidBrush(Color.FromArgb((int)(hoverAmount * 12), Color.White)))
                    e.Graphics.FillPath(fill, path);
            }
            Color color = Primary ? Color.Black : Enabled ? Color.White : IslandTheme.Secondary;
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(Padding.Left + 12, 0, Width - Padding.Left - 24, Height), color, (TextAlign == ContentAlignment.MiddleCenter ? TextFormatFlags.HorizontalCenter : TextFormatFlags.Left) | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (Focused && ShowFocusCues)
                using (var path = IslandTheme.Round(new RectangleF(2, 2, Width - 4, Height - 4), 17))
                using (var pen = new Pen(IslandTheme.Accent, 1))
                    e.Graphics.DrawPath(pen, path);
            e.Graphics.Restore(drawing);
        }
        void PaintMaterial(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new RectangleF(1, 1, Width - 2, Height - 2);
            float radius = Primary || Navigation ? Height / 2f : 16 + 6 * pressAmount;
            if (Primary || Selected && !Navigation)
                AppDesign.Fill(g, bounds, radius, Primary ? AppDesign.Primary : AppDesign.PrimaryContainer);
            else if (!Navigation)
                AppDesign.Fill(g, bounds, radius, AppDesign.High);
            if (hoverAmount > 0 || pressAmount > 0)
                AppDesign.Fill(g, bounds, radius, Color.FromArgb((int)(hoverAmount * 18 + pressAmount * 12), Primary ? AppDesign.OnPrimary : AppDesign.Ink));
            Color color = !Enabled ? AppDesign.Outline : Primary ? AppDesign.OnPrimary : Selected ? AppDesign.OnContainer : AppDesign.Ink;
            int inset = Padding.Left + 12;
            if (Navigation)
            {
                AppDesign.Symbol(g, AppDesign.NavigationIcon(Tag as string), new RectangleF(16, 0, 24, Height), color, 22);
                inset = 56;
            }
            TextRenderer.DrawText(g, Text, Font, new Rectangle(inset, 0, Math.Max(1, Width - inset - 16), Height), color, (TextAlign == ContentAlignment.MiddleCenter ? TextFormatFlags.HorizontalCenter : TextFormatFlags.Left) | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (Focused && ShowFocusCues)
                using (var path = IslandTheme.Round(new RectangleF(3, 3, Width - 6, Height - 6), radius))
                using (var pen = new Pen(AppDesign.Primary, 2)) g.DrawPath(pen, path);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                visualDisposed = true;
                interactionTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
    internal sealed class IslandMenuRenderer : ToolStripProfessionalRenderer
    {
        internal static void Style(ContextMenuStrip menu)
        {
            menu.Renderer = new IslandMenuRenderer();
            menu.Font = AppTypography.Create(10);
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = false;
            menu.Padding = new Padding(6);
            menu.BackColor = AppDesign.Material ? AppDesign.High : Color.FromArgb(24, 24, 27);
            menu.ForeColor = IslandTheme.Foreground;
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
            e.Graphics.Clear(AppDesign.Material ? AppDesign.High : Color.FromArgb(24, 24, 27));
        }
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            if (AppDesign.Material) return;
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
            using (var brush = new SolidBrush(AppDesign.Material ? AppDesign.Secondary : IslandTheme.Accent))
                e.Graphics.FillPath(brush, path);
        }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = AppDesign.Material ? (e.Item.Selected ? AppDesign.OnSecondary : AppDesign.Ink) : Color.White;
            base.OnRenderItemText(e);
        }
    }
    internal sealed class ToggleSwitch : CheckBox
    {
        readonly Timer timer = new FrameTimer(); float amount; DateTime previous;
        internal ToggleSwitch()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = IslandTheme.Background;
            ForeColor = IslandTheme.Foreground;
            Cursor = Cursors.Hand;
            timer.Tick += Animate;
        }
        protected override void OnCheckedChanged(EventArgs e)
        {
            previous = PreciseClock.UtcNow;
            timer.Start();
            base.OnCheckedChanged(e);
        }
        void Animate(object sender, EventArgs e)
        {
            float step = (float)(PreciseClock.UtcNow - previous).TotalMilliseconds / 150;
            previous = PreciseClock.UtcNow;
            amount = Checked ? Math.Min(1, amount + step) : Math.Max(0, amount - step);
            if (!Program.Settings.InterfaceAnimations) amount = Checked ? 1 : 0;
            if (amount == 0 || amount == 1)
                timer.Stop();
            Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(IslandTheme.Background);
            IslandTheme.Card(g, ClientRectangle);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(20, 0, Width - 112, Height), IslandTheme.Foreground, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            if (AppDesign.Material)
            {
                var materialTrack = new RectangleF(Width - 76, (Height - 32) / 2f, 52, 32);
                AppDesign.Fill(g, materialTrack, 16, AppDesign.Blend(AppDesign.Highest, AppDesign.Primary, amount));
                if (amount < 1)
                    using (var path = IslandTheme.Round(materialTrack, 16))
                    using (var pen = new Pen(Color.FromArgb((int)(255 * (1 - amount)), AppDesign.Outline), 2)) g.DrawPath(pen, path);
                float diameter = 16 + 8 * amount;
                using (var thumb = new SolidBrush(AppDesign.Blend(AppDesign.Outline, AppDesign.OnPrimary, amount)))
                    g.FillEllipse(thumb, materialTrack.X + 16 + 20 * amount - diameter / 2, materialTrack.Y + 16 - diameter / 2, diameter, diameter);
                if (Focused && ShowFocusCues)
                    using (var path = IslandTheme.Round(new RectangleF(Width - 82, (Height - 44) / 2f, 64, 44), 22))
                    using (var pen = new Pen(AppDesign.Primary, 2)) g.DrawPath(pen, path);
                return;
            }
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
        // displayValue is the drawn thumb position; while dragging it follows the
        // pointer continuously instead of jumping between integer values.
        int value; bool dragging; float displayValue, displayTarget, dragAmount;
        bool magnetic = true;
        // Stops that catch the thumb (min, quarter, half, start value, max).
        // Continuous ranges such as colour channels turn them off.
        internal bool Magnetic { get { return magnetic; } set { magnetic = value; Invalidate(); } }
        readonly Timer motionTimer = new FrameTimer(); DateTime motionAt;
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
                if (!dragging)
                {
                    displayTarget = next;
                    if (!Program.Settings.InterfaceAnimations) displayValue = next;
                }
                AnimateThumb();
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
            displayValue = displayTarget = value;
            magnets = new[] { min, min + (max - min) / 4, min + (max - min) / 2, value, max };
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            Size = new Size(620, 91);
            BackColor = IslandTheme.Background;
            ForeColor = IslandTheme.Foreground;
            TabStop = true;
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.Slider;
            AccessibleName = label;
            motionTimer.Tick += delegate
            {
                float t = (float)(PreciseClock.UtcNow - motionAt).TotalMilliseconds;
                motionAt = PreciseClock.UtcNow;
                if (!dragging)
                    displayValue += (displayTarget - displayValue) * Math.Min(1, t / 55);
                dragAmount += ((dragging ? 1 : 0) - dragAmount) * Math.Min(1, t / 70);
                if (!Program.Settings.InterfaceAnimations)
                {
                    if (!dragging) displayValue = displayTarget;
                    dragAmount = dragging ? 1 : 0;
                }
                if ((dragging || Math.Abs(displayValue - displayTarget) < .01f) && Math.Abs(dragAmount - (dragging ? 1 : 0)) < .01f)
                {
                    if (!dragging) displayValue = displayTarget;
                    dragAmount = dragging ? 1 : 0;
                    motionTimer.Stop();
                }
                Invalidate();
            };
        }
        void AnimateThumb()
        {
            if (!motionTimer.Enabled)
            {
                motionAt = PreciseClock.UtcNow;
                motionTimer.Start();
            }
            Invalidate();
        }
        internal int Snap(double fraction)
        {
            return Math.Max(min, Math.Min(max, (int)Math.Round(min + Math.Max(0, Math.Min(1, fraction)) * (max - min))));
        }
        void At(int x)
        {
            double width = Math.Max(1, Width - 52), position = Math.Max(0, Math.Min(width, x - 26));
            float pointer = (float)(min + position / width * (max - min));
            int chosen = Snap(position / width);
            if (magnetic)
                foreach (int stop in magnets)
                    if (Math.Abs(position - (stop - min) * width / Math.Max(1, max - min)) <= 6)
                    {
                        pointer = chosen = stop;
                        break;
                    }
            // Move the thumb first so ValueChanged observers see a consistent state.
            if (dragging)
            {
                displayValue = pointer;
                Invalidate();
            }
            Value = chosen;
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && e.Y >= 40)
            {
                Focus();
                dragging = true;
                Capture = true;
                At(e.X);
                AnimateThumb();
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
            if (dragging && e.Button == MouseButtons.Left) At(e.X);
            EndDrag();
            Capture = false;
            base.OnMouseUp(e);
        }
        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            if (!Capture && dragging) EndDrag();
            base.OnMouseCaptureChanged(e);
        }
        // Settle the thumb from where the pointer left it onto the chosen value.
        void EndDrag()
        {
            if (!dragging) return;
            dragging = false;
            displayTarget = value;
            if (!Program.Settings.InterfaceAnimations) displayValue = value;
            AnimateThumb();
        }
        protected override bool IsInputKey(Keys key)
        {
            return key == Keys.Left || key == Keys.Right || key == Keys.Up || key == Keys.Down || key == Keys.Home || key == Keys.End || base.IsInputKey(key);
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
            { Value = min; e.Handled = true; }
            if (e.KeyCode == Keys.End)
            { Value = max; e.Handled = true; }
            base.OnKeyDown(e);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(IslandTheme.Background);
            IslandTheme.Card(g, ClientRectangle);
            if (AppDesign.Material) { PaintMaterialSlider(g); return; }
            TextRenderer.DrawText(g, label, Font, new Rectangle(20, 13, Width - 95, 24), Color.White, TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, Value.ToString(), Font, new Rectangle(Width - 70, 13, 48, 24), IslandTheme.Secondary, TextFormatFlags.Right);
            float length = Width - 52, y = 63, x = 26 + length * (displayValue - min) / Math.Max(1, max - min);
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
            if (magnetic)
            using (var brush = new SolidBrush(Color.FromArgb(110, 110, 120)))
                foreach (int stop in magnets)
                {
                    float at = 26 + length * (stop - min) / Math.Max(1, max - min);
                    g.FillEllipse(brush, at - 1, y + 10, 2, 2);
                }
            using (var shadow = new SolidBrush(Color.FromArgb(50, Color.White)))
                g.FillEllipse(shadow, x - 12, y - 12, 24, 24);
            float radius = 9 + dragAmount * 1.5f;
            g.FillEllipse(Brushes.White, x - radius, y - radius, radius * 2, radius * 2);
        }
        void PaintMaterialSlider(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            TextRenderer.DrawText(g, label, Font, new Rectangle(20, 12, Width - 105, 28), AppDesign.Ink, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            AppDesign.Fill(g, new RectangleF(Width - 75, 12, 52, 28), 14, AppDesign.PrimaryContainer);
            TextRenderer.DrawText(g, Value.ToString(), Font, new Rectangle(Width - 75, 12, 52, 28), AppDesign.OnContainer, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            float x = 26 + (Width - 52) * (displayValue - min) / Math.Max(1, max - min);
            AppDesign.Fill(g, new RectangleF(26, 55, Math.Max(0, x - 32), 16), 8, AppDesign.Primary);
            AppDesign.Fill(g, new RectangleF(x + 6, 55, Math.Max(0, Width - 32 - x), 16), 8, AppDesign.Secondary);
            AppDesign.Fill(g, new RectangleF(x - 2, 47 - 2 * dragAmount, 4, 32 + 4 * dragAmount), 2, AppDesign.Primary);
            if (magnetic)
            foreach (int stop in magnets)
            {
                float at = 26 + (Width - 52) * (stop - min) / Math.Max(1, max - min);
                using (var brush = new SolidBrush(at < x ? AppDesign.OnPrimary : AppDesign.OnSecondary)) g.FillEllipse(brush, at - 1, 62, 2, 2);
            }
            if (Focused && ShowFocusCues)
                using (var path = IslandTheme.Round(new RectangleF(3, 3, Width - 6, Height - 6), 18))
                using (var pen = new Pen(AppDesign.Primary, 2)) g.DrawPath(pen, path);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                motionTimer.Dispose();
            base.Dispose(disposing);
        }
    }
}
