using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace WinDynamicIsland
{
    // Composes whole-page snapshots (fade, slight scale, slide) in managed code.
    // GDI+ needs ~13 ms for one scaled, alpha-matrixed page and serialises all
    // drawing, so a two-layer transition ran at 30-40 fps. Here every row is
    // independent, runs across cores, and the finished frame is one unscaled blit.
    internal sealed class PageCompositor : IDisposable
    {
        int[] canvas;
        GCHandle pin;
        Bitmap frame;
        int width, height;

        internal Bitmap Frame { get { return frame; } }

        internal void Begin(int frameWidth, int frameHeight, Color background)
        {
            if (frame == null || width != frameWidth || height != frameHeight)
            {
                Release();
                width = Math.Max(1, frameWidth);
                height = Math.Max(1, frameHeight);
                canvas = new int[width * height];
                pin = GCHandle.Alloc(canvas, GCHandleType.Pinned);
                frame = new Bitmap(width, height, width * 4, PixelFormat.Format32bppPArgb, pin.AddrOfPinnedObject());
            }
            int fill = background.ToArgb() | unchecked((int)0xFF000000);
            canvas[0] = fill;
            // Doubling copies fill the frame far faster than a per-pixel loop.
            for (int filled = 1; filled < canvas.Length; filled *= 2)
                Array.Copy(canvas, 0, canvas, filled, Math.Min(filled, canvas.Length - filled));
        }

        // Draws the source into box with bilinear sampling, multiplied by alpha,
        // over the frame (premultiplied "source over").
        internal void Layer(PagePixels source, Rectangle box, double alpha)
        {
            if (source == null || alpha <= 0 || box.Width <= 0 || box.Height <= 0) return;
            int a256 = Math.Min(256, (int)Math.Round(alpha * 256));
            if (a256 <= 0) return;
            int x0 = Math.Max(0, box.X), x1 = Math.Min(width, box.Right), y0 = Math.Max(0, box.Y), y1 = Math.Min(height, box.Bottom);
            if (x0 >= x1 || y0 >= y1) return;
            int columns = x1 - x0;
            int[] src = source.Data, dst = canvas;
            int sw = source.Width, sh = source.Height, dw = width;
            bool exact = box.Width == sw && box.Height == sh;
            int[] left = null, right = null, weight = null;
            double scaleY = (double)sh / box.Height;
            if (!exact)
            {
                left = new int[columns]; right = new int[columns]; weight = new int[columns];
                double scaleX = (double)sw / box.Width;
                for (int k = 0; k < columns; k++)
                {
                    double u = Math.Max(0, Math.Min(sw - 1, (x0 + k - box.X + .5) * scaleX - .5));
                    int i = (int)u;
                    left[k] = i;
                    right[k] = Math.Min(i + 1, sw - 1);
                    weight[k] = (int)((u - i) * 256);
                }
            }
            Parallel.ForEach(Partitioner.Create(y0, y1, 24), range =>
            {
                for (int y = range.Item1; y < range.Item2; y++)
                {
                    int target = y * dw + x0;
                    if (exact)
                    {
                        int row = (y - box.Y) * sw + (x0 - box.X);
                        if (a256 == 256 && Opaque(source))
                        {
                            Array.Copy(src, row, dst, target, columns);
                            continue;
                        }
                        for (int k = 0; k < columns; k++)
                            dst[target + k] = Over(Scale(src[row + k], a256), dst[target + k]);
                        continue;
                    }
                    double v = Math.Max(0, Math.Min(sh - 1, (y - box.Y + .5) * scaleY - .5));
                    int j = (int)v, fy = (int)((v - j) * 256);
                    int top = j * sw, bottom = Math.Min(j + 1, sh - 1) * sw;
                    for (int k = 0; k < columns; k++)
                    {
                        int fx = weight[k];
                        int w00 = (256 - fx) * (256 - fy), w10 = fx * (256 - fy), w01 = (256 - fx) * fy, w11 = fx * fy;
                        int c00 = src[top + left[k]], c10 = src[top + right[k]], c01 = src[bottom + left[k]], c11 = src[bottom + right[k]];
                        int b = ((c00 & 255) * w00 + (c10 & 255) * w10 + (c01 & 255) * w01 + (c11 & 255) * w11 + 32768) >> 16;
                        int g = ((c00 >> 8 & 255) * w00 + (c10 >> 8 & 255) * w10 + (c01 >> 8 & 255) * w01 + (c11 >> 8 & 255) * w11 + 32768) >> 16;
                        int r = ((c00 >> 16 & 255) * w00 + (c10 >> 16 & 255) * w10 + (c01 >> 16 & 255) * w01 + (c11 >> 16 & 255) * w11 + 32768) >> 16;
                        int a = ((c00 >> 24 & 255) * w00 + (c10 >> 24 & 255) * w10 + (c01 >> 24 & 255) * w01 + (c11 >> 24 & 255) * w11 + 32768) >> 16;
                        dst[target + k] = Over(Scale(a << 24 | r << 16 | g << 8 | b, a256), dst[target + k]);
                    }
                }
            });
        }

        static bool Opaque(PagePixels source) { return source.Opaque; }

        static int Scale(int color, int a256)
        {
            if (a256 >= 256) return color;
            int a = ((color >> 24 & 255) * a256 + 128) >> 8, r = ((color >> 16 & 255) * a256 + 128) >> 8;
            int g = ((color >> 8 & 255) * a256 + 128) >> 8, b = ((color & 255) * a256 + 128) >> 8;
            return a << 24 | r << 16 | g << 8 | b;
        }

        static int Over(int source, int target)
        {
            int inverse = 255 - (source >> 24 & 255);
            if (inverse == 0) return source;
            int a = (source >> 24 & 255) + Div255((target >> 24 & 255) * inverse);
            int r = (source >> 16 & 255) + Div255((target >> 16 & 255) * inverse);
            int g = (source >> 8 & 255) + Div255((target >> 8 & 255) * inverse);
            int b = (source & 255) + Div255((target & 255) * inverse);
            return Math.Min(255, a) << 24 | Math.Min(255, r) << 16 | Math.Min(255, g) << 8 | Math.Min(255, b);
        }

        static int Div255(int value) { value += 128; return (value + (value >> 8)) >> 8; }

        void Release()
        {
            if (frame != null) frame.Dispose();
            if (pin.IsAllocated) pin.Free();
            frame = null;
            canvas = null;
        }

        public void Dispose() { Release(); }
    }

    // Premultiplied pixels of a captured page, read once per transition.
    internal sealed class PagePixels
    {
        internal readonly int[] Data;
        internal readonly int Width, Height;
        internal readonly bool Opaque;
        internal PagePixels(Bitmap image)
        {
            Width = image.Width;
            Height = image.Height;
            Data = new int[Width * Height];
            var bits = image.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try
            {
                for (int y = 0; y < Height; y++)
                    Marshal.Copy(new IntPtr(bits.Scan0.ToInt64() + (long)y * bits.Stride), Data, y * Width, Width);
            }
            finally { image.UnlockBits(bits); }
            bool opaque = true;
            for (int i = 0; i < Data.Length && opaque; i++)
                opaque = (Data[i] >> 24 & 255) == 255;
            Opaque = opaque;
        }
    }
}
