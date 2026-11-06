using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
namespace WinDynamicIsland
{
    internal sealed class BufferedPanel : Panel
    {
        internal BufferedPanel()
        {
            DoubleBuffered = true;
            BackColor = IslandTheme.Background;
            ResizeRedraw = true;
        }
    }
    // Animate two finished pages. Native child controls are never resized or faded per frame.
    internal sealed class PageTransitionSurface : Control
    {
        readonly Timer timer = new FrameTimer();
        readonly Stopwatch elapsed = new Stopwatch();
        Bitmap previous, next; int direction = 1;
        PagePixels previousPixels, nextPixels;
        readonly PageCompositor compositor = new PageCompositor();
        internal bool Running
        {
            get
            {
                return timer.Enabled;
            }
        }
        internal PageTransitionSurface()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = IslandTheme.Background;
            Visible = false;
            TabStop = false;
            timer.Tick += delegate
            {
                if (elapsed.ElapsedMilliseconds >= 240)
                    Finish();
                else
                    Invalidate();
            };
        }
        internal static Bitmap CapturePage(Control page)
        {
            var bitmap = new Bitmap(Math.Max(1, page.Width), Math.Max(1, page.Height), PixelFormat.Format32bppPArgb);
            try
            {
                page.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                return bitmap;
            }
            catch { bitmap.Dispose(); throw; }
        }
        internal Bitmap CaptureVisible(Control page)
        {
            if (!Visible || previous == null)
                return CapturePage(page);
            var bitmap = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
            using (var graphics = Graphics.FromImage(bitmap))
                PaintFrame(graphics, Progress);
            return bitmap;
        }
        internal void Cover(Bitmap frame)
        {
            timer.Stop();
            ClearFrames();
            previous = frame;
            next = null;
            Visible = true;
            BringToFront();
            Invalidate();
            Update();
        }
        internal void Begin(Bitmap frame, int direction)
        {
            next = frame;
            nextPixels = null;
            this.direction = direction;
            elapsed.Restart();
            timer.Start();
            Invalidate();
        }
        double Progress
        {
            get
            {
                return Math.Min(1, elapsed.Elapsed.TotalMilliseconds / 240);
            }
        }
        internal static double Ease(double value)
        {
            value = Math.Max(0, Math.Min(1, value));
            return 1 - Math.Pow(1 - value, 3);
        }
        void Layer(Bitmap image, ref PagePixels pixels, double alpha, double scale, double offset)
        {
            if (image == null || alpha <= 0)
                return;
            var box = new Rectangle((int)Math.Round((Width - Width * scale) / 2), (int)Math.Round(offset), Math.Max(1, (int)Math.Round(Width * scale)), Math.Max(1, (int)Math.Round(Height * scale)));
            if (pixels == null)
                pixels = new PagePixels(image);
            compositor.Layer(pixels, box, alpha);
        }
        void PaintFrame(Graphics graphics, double progress)
        {
            if (next == null)
            {
                graphics.Clear(IslandTheme.Background);
                if (previous != null)
                    graphics.DrawImageUnscaled(previous, 0, 0);
                return;
            }
            compositor.Begin(Width, Height, IslandTheme.Background);
            double amount = Ease(progress);
            if (direction == 0)
            {
                Layer(previous, ref previousPixels, 1, 1, 0);
                Layer(next, ref nextPixels, amount, 1, 0);
            }
            else if (AppDesign.Material)
            {
                double spatial = AppDesign.Spatial(progress);
                Layer(previous, ref previousPixels, 1 - Ease(progress / .6), 1 - .015 * spatial, -direction * 16 * spatial);
                Layer(next, ref nextPixels, Ease((progress - .08) / .92), .97 + .03 * spatial, direction * 24 * (1 - spatial));
            }
            else
            {
                double outgoing = 1 - Ease(progress / .45), incoming = Ease((progress - .12) / .88);
                Layer(previous, ref previousPixels, outgoing, 1 - .008 * amount, -direction * 8 * amount);
                Layer(next, ref nextPixels, incoming, .988 + .012 * amount, direction * 14 * (1 - amount));
            }
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.DrawImageUnscaled(compositor.Frame, 0, 0);
        }
        internal Bitmap RenderPreview(double progress)
        {
            var frame = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
            using (var graphics = Graphics.FromImage(frame))
                PaintFrame(graphics, progress);
            return frame;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            PaintFrame(e.Graphics, Progress);
        }
        internal void Finish()
        {
            timer.Stop();
            elapsed.Stop();
            Visible = false;
            ClearFrames();
        }
        void ClearFrames()
        {
            if (previous != null)
                previous.Dispose();
            if (next != null)
                next.Dispose();
            previous = next = null;
            previousPixels = nextPixels = null;
        }
        protected override void OnSizeChanged(EventArgs e)
        {
            if (previous != null && previous.Size != Size)
                Finish();
            base.OnSizeChanged(e);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                timer.Dispose();
                ClearFrames();
                compositor.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
