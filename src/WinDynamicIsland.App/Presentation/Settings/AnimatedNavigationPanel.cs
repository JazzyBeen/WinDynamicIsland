using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace WinDynamicIsland
{
    internal sealed class AnimatedNavigationPanel : FlowLayoutPanel
    {
        readonly Timer timer = new FrameTimer(); readonly Stopwatch elapsed = new Stopwatch();
        RectangleF from, target, shown; bool initialized;
        Rectangle painted;
        internal AnimatedNavigationPanel()
        {
            DoubleBuffered = true;
            BackColor = AppDesign.Material ? AppDesign.Container : Color.Black;
            timer.Tick += delegate
            {
                bool finished = elapsed.ElapsedMilliseconds >= (AppDesign.Material ? 300 : 230);
                if (finished)
                    timer.Stop();
                MoveHighlight(Current);
            };
        }
        RectangleF Current
        {
            get
            {
                double t = timer.Enabled ? (AppDesign.Material ? AppDesign.Spatial(elapsed.Elapsed.TotalMilliseconds / 300) : PageTransitionSurface.Ease(elapsed.Elapsed.TotalMilliseconds / 230)) : 1;
                return new RectangleF((float)(from.X + (target.X - from.X) * t), (float)(from.Y + (target.Y - from.Y) * t), (float)(from.Width + (target.Width - from.Width) * t), (float)(from.Height + (target.Height - from.Height) * t));
            }
        }
        internal void Select(GlassButton button, bool animate)
        {
            if (button == null)
                return;
            var next = (RectangleF)button.Bounds;
            if (!initialized || !animate)
            {
                from = target = shown = next;
                initialized = true;
                timer.Stop();
                painted = Rectangle.Empty;
                Invalidate(true);
                return;
            }
            if (target == next)
                return;
            from = Current;
            target = next;
            elapsed.Restart();
            timer.Start();
            MoveHighlight(from);
        }
        internal void Freeze()
        {
            if (!timer.Enabled)
                return;
            from = target = Current;
            timer.Stop();
            MoveHighlight(target);
        }
        // The highlight is fixed when a frame is scheduled, so the painted area and
        // the invalidated area always agree; only the band it travelled is redrawn.
        void MoveHighlight(RectangleF highlight)
        {
            shown = highlight;
            var area = Rectangle.Round(highlight);
            area.Inflate(3, 3);
            Invalidate(painted.Width > 0 && painted.Height > 0 ? Rectangle.Union(painted, area) : area, true);
            painted = area;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (initialized)
                IslandTheme.Card(e.Graphics, Rectangle.Round(shown), true, 15);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                timer.Dispose();
            base.Dispose(disposing);
        }
    }
}
