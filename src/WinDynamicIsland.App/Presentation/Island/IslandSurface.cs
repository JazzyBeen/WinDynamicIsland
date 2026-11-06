using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

// All samples use premultiplied alpha. Averaging them simulates a short exposure
// without making the stationary black background translucent.
namespace WinDynamicIsland
{
    internal static class IslandSurface
    {
        internal const int CanvasWidth = 520, CanvasHeight = 280, OriginY = -64;
        internal static Bitmap Render(Bitmap face, RectangleF[] samples)
        {
            using (var canvas = new IslandCanvas())
            {
                canvas.Compose(face, samples);
                return canvas.Snapshot();
            }
        }
    }

    // A persistent layered-window surface. Frames are composed straight into the
    // DIB section that UpdateLayeredWindow reads, so no per-frame bitmaps, HBITMAP
    // conversions or large managed buffers are created. Only the area the island
    // covers now (plus whatever it covered last frame) is touched.
    internal sealed class IslandCanvas : IDisposable
    {
        const int Width = IslandSurface.CanvasWidth, Height = IslandSurface.CanvasHeight, Stride = Width * 4;
        // Bicubic sampling and antialiased edges never spill further than this.
        const int Bleed = 4;
        static readonly byte[] Zeros = new byte[Stride];
        IntPtr dc, dib, previousObject, bits;
        Bitmap surface;
        ImageAttributes tiling;
        SampleLane lane;
        Rectangle drawn;
        bool disposed;

        internal IslandCanvas()
        {
            var header = new BitmapInfoHeader { Size = Marshal.SizeOf(typeof(BitmapInfoHeader)), Width = Width, Height = -Height, Planes = 1, BitCount = 32 };
            dc = CreateCompatibleDC(IntPtr.Zero);
            if (dc == IntPtr.Zero)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            dib = CreateDIBSection(dc, ref header, 0, out bits, IntPtr.Zero, 0);
            if (dib == IntPtr.Zero || bits == IntPtr.Zero)
            {
                DeleteDC(dc);
                dc = IntPtr.Zero;
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }
            previousObject = SelectObject(dc, dib);
            // A fresh DIB section is zero-filled: fully transparent premultiplied pixels.
            surface = new Bitmap(Width, Height, Stride, PixelFormat.Format32bppPArgb, bits);
        }

        internal void Compose(Bitmap face, RectangleF[] samples)
        {
            if (samples.Length == 0) return;
            var first = samples[0];
            bool exact = samples.Length == 1 && first.Width == face.Width && first.Height == face.Height;
            Rectangle region = exact ? Clip(new Rectangle((int)first.Left, (int)first.Top, face.Width, face.Height)) : Bounds(samples);
            if (samples.Length == 1)
            {
                Clear(bits, Union(drawn, region));
                using (var g = Graphics.FromImage(surface))
                {
                    Prepare(g);
                    if (exact)
                        g.DrawImageUnscaled(face, (int)first.Left, (int)first.Top);
                    else
                    {
                        if (tiling == null)
                            tiling = Tiling();
                        DrawScaled(g, face, first, tiling);
                    }
                }
                drawn = region;
                return;
            }
            var lane = Lane();
            Accumulate(lane, face, samples, region);
            int rowBytes = region.Width * 4, count = rowBytes * region.Height;
            int n = samples.Length, half = n / 2;
            var sum = lane.Sum;
            var pixels = lane.Pixels;
            for (int p = 0; p < count; p++)
                pixels[p] = (byte)((sum[p] + half) / n);
            Clear(bits, drawn);
            for (int y = 0; y < region.Height; y++)
                Marshal.Copy(pixels, y * rowBytes, Offset(bits, region.X, region.Y + y), rowBytes);
            drawn = region;
        }

        internal void Present(IntPtr window, int x, int y)
        {
            var position = new Point { X = x, Y = y };
            var origin = new Point();
            var size = new Size { Width = Width, Height = Height };
            var blend = new Blend { Alpha = 255, Format = 1 };
            if (!UpdateLayeredWindow(window, IntPtr.Zero, ref position, ref size, dc, ref origin, 0, ref blend, 2))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            // Reassert native visibility and z-order after the layered surface
            // changes, without taking keyboard focus from the current app.
            SetWindowPos(window, new IntPtr(-1), 0, 0, 0, 0, 0x53);
        }

        internal Bitmap Snapshot()
        {
            var output = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
            var target = output.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            try
            {
                var row = new byte[Stride];
                for (int y = 0; y < Height; y++)
                {
                    Marshal.Copy(Offset(bits, 0, y), row, 0, Stride);
                    Marshal.Copy(row, 0, new IntPtr(target.Scan0.ToInt64() + (long)y * target.Stride), Stride);
                }
            }
            finally { output.UnlockBits(target); }
            return output;
        }

        // Scratch surface and integer accumulators for motion-blurred frames.
        // GDI+ serialises DrawImage internally, so the exposures stay sequential.
        sealed class SampleLane : IDisposable
        {
            internal readonly IntPtr Bits;
            internal readonly Bitmap Scratch;
            internal readonly ImageAttributes Attributes = Tiling();
            internal readonly int[] Sum = new int[Stride * Height];
            internal readonly byte[] Pixels = new byte[Stride * Height];
            internal SampleLane()
            {
                Bits = Marshal.AllocHGlobal(Stride * Height);
                Clear(Bits, new Rectangle(0, 0, Width, Height));
                Scratch = new Bitmap(Width, Height, Stride, PixelFormat.Format32bppPArgb, Bits);
            }
            public void Dispose()
            {
                Scratch.Dispose();
                Attributes.Dispose();
                Marshal.FreeHGlobal(Bits);
            }
        }

        SampleLane Lane()
        {
            if (lane == null)
                lane = new SampleLane();
            return lane;
        }

        static void Accumulate(SampleLane lane, Bitmap face, RectangleF[] samples, Rectangle region)
        {
            int rowBytes = region.Width * 4, count = rowBytes * region.Height;
            var sum = lane.Sum;
            var pixels = lane.Pixels;
            using (var g = Graphics.FromImage(lane.Scratch))
            {
                Prepare(g);
                for (int s = 0; s < samples.Length; s++)
                {
                    // Coinciding exposures (the trail catching up) reuse the last sample.
                    if (s == 0 || samples[s] != samples[s - 1])
                    {
                        Clear(lane.Bits, region);
                        DrawScaled(g, face, samples[s], lane.Attributes);
                        g.Flush(FlushIntention.Sync);
                        for (int y = 0; y < region.Height; y++)
                            Marshal.Copy(Offset(lane.Bits, region.X, region.Y + y), pixels, y * rowBytes, rowBytes);
                    }
                    if (s == 0)
                        for (int p = 0; p < count; p++)
                            sum[p] = pixels[p];
                    else
                        for (int p = 0; p < count; p++)
                            sum[p] += pixels[p];
                }
            }
        }

        static ImageAttributes Tiling()
        {
            var attributes = new ImageAttributes();
            attributes.SetWrapMode(WrapMode.TileFlipXY);
            return attributes;
        }

        static void Prepare(Graphics g)
        {
            g.CompositingMode = CompositingMode.SourceCopy;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }

        static void DrawScaled(Graphics g, Bitmap face, RectangleF rect, ImageAttributes attributes)
        {
            var points = new[] { new PointF(rect.Left, rect.Top), new PointF(rect.Right, rect.Top), new PointF(rect.Left, rect.Bottom) };
            g.DrawImage(face, points, new RectangleF(0, 0, face.Width, face.Height), GraphicsUnit.Pixel, attributes);
        }

        static Rectangle Bounds(RectangleF[] samples)
        {
            float left = float.MaxValue, top = float.MaxValue, right = float.MinValue, bottom = float.MinValue;
            foreach (var rect in samples)
            {
                left = Math.Min(left, Math.Min(rect.Left, rect.Right));
                top = Math.Min(top, Math.Min(rect.Top, rect.Bottom));
                right = Math.Max(right, Math.Max(rect.Left, rect.Right));
                bottom = Math.Max(bottom, Math.Max(rect.Top, rect.Bottom));
            }
            return Clip(Rectangle.FromLTRB((int)Math.Floor(left) - Bleed, (int)Math.Floor(top) - Bleed, (int)Math.Ceiling(right) + Bleed, (int)Math.Ceiling(bottom) + Bleed));
        }

        static Rectangle Clip(Rectangle rect)
        {
            return Rectangle.Intersect(rect, new Rectangle(0, 0, Width, Height));
        }

        static Rectangle Union(Rectangle a, Rectangle b)
        {
            if (a.Width <= 0 || a.Height <= 0) return b;
            if (b.Width <= 0 || b.Height <= 0) return a;
            return Rectangle.Union(a, b);
        }

        static IntPtr Offset(IntPtr start, int x, int y)
        {
            return new IntPtr(start.ToInt64() + (long)y * Stride + x * 4L);
        }

        static void Clear(IntPtr start, Rectangle rect)
        {
            if (rect.Width <= 0 || rect.Height <= 0) return;
            int bytes = rect.Width * 4;
            for (int y = rect.Top; y < rect.Bottom; y++)
                Marshal.Copy(Zeros, 0, Offset(start, rect.X, y), bytes);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            surface.Dispose();
            if (tiling != null) tiling.Dispose();
            if (lane != null) lane.Dispose();
            if (previousObject != IntPtr.Zero) SelectObject(dc, previousObject);
            DeleteDC(dc);
            DeleteObject(dib);
        }

        [StructLayout(LayoutKind.Sequential)]
        struct Point
        {
            public int X, Y;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct Size
        {
            public int Width, Height;
        }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct Blend
        {
            public byte Op, Flags, Alpha, Format;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct BitmapInfoHeader
        {
            public int Size, Width, Height;
            public short Planes, BitCount;
            public int Compression, SizeImage, XPelsPerMeter, YPelsPerMeter, ClrUsed, ClrImportant;
        }
        [DllImport("user32.dll", SetLastError = true)] static extern bool UpdateLayeredWindow(IntPtr window, IntPtr screen, ref Point position, ref Size size, IntPtr source, ref Point origin, uint key, ref Blend blend, uint flags);
        [DllImport("user32.dll", SetLastError = true)] static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("gdi32.dll", SetLastError = true)] static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll", SetLastError = true)] static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfoHeader header, uint usage, out IntPtr bits, IntPtr section, uint offset);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr value);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr value);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
    }
}
