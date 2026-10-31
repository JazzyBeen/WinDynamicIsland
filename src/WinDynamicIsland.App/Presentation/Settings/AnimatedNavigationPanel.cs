using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace WinDynamicIsland
{
    internal sealed class AnimatedNavigationPanel : FlowLayoutPanel
    {
        readonly Timer timer = new Timer { Interval = 16 }; readonly Stopwatch elapsed = new Stopwatch();
        RectangleF from, target; bool initialized;
        internal AnimatedNavigationPanel()
        {
            DoubleBuffered = true;
            BackColor = Color.Black;
            timer.Tick += delegate
            {
                Invalidate(true);
                if (elapsed.ElapsedMilliseconds >= 230)
                    timer.Stop();
            };
        }
        RectangleF Current
        {
            get
            {
                double t = timer.Enabled ? PageTransitionSurface.Ease(elapsed.Elapsed.TotalMilliseconds / 230) : 1;
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
                from = target = next;
                initialized = true;
                timer.Stop();
                Invalidate(true);
                return;
            }
            if (target == next)
                return;
            from = Current;
            target = next;
            elapsed.Restart();
            timer.Start();
            Invalidate(true);
        }
        internal void Freeze()
        {
            if (!timer.Enabled)
                return;
            from = target = Current;
            timer.Stop();
            Invalidate(true);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (initialized)
                IslandTheme.Card(e.Graphics, Rectangle.Round(Current), true, 15);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                timer.Dispose();
            base.Dispose(disposing);
        }
    }
}
