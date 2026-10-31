using WinDynamicIsland.Core;
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
            int length = CanvasWidth * CanvasHeight * 4;
            var sum = new int[length];
            var bytes = new byte[length];
            using (var sample = new Bitmap(CanvasWidth, CanvasHeight, PixelFormat.Format32bppPArgb))
            {
                foreach (var rect in samples)
                {
                    using (var g = Graphics.FromImage(sample))
                    {
                        g.Clear(Color.Transparent);
                        g.CompositingMode = CompositingMode.SourceCopy;
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        using (var attributes = new ImageAttributes())
                        {
                            attributes.SetWrapMode(WrapMode.TileFlipXY);
                            var points = new[] { new PointF(rect.Left, rect.Top), new PointF(rect.Right, rect.Top), new PointF(rect.Left, rect.Bottom) };
                            if (samples.Length == 1 && rect.Width == face.Width && rect.Height == face.Height)
                                g.DrawImageUnscaled(face, (int)rect.Left, (int)rect.Top);
                            else
                                g.DrawImage(face, points, new RectangleF(0, 0, face.Width, face.Height), GraphicsUnit.Pixel, attributes);
                        }
                    }
                    var data = sample.LockBits(new Rectangle(0, 0, CanvasWidth, CanvasHeight), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
                    try
                    {
                        Marshal.Copy(data.Scan0, bytes, 0, length);
                    }
                    finally { sample.UnlockBits(data); }
                    for (int p = 0; p < length; p++)
                        sum[p] += bytes[p];
                }
            }
            for (int p = 0; p < length; p++)
                bytes[p] = (byte)((sum[p] + samples.Length / 2) / samples.Length);
            var output = new Bitmap(CanvasWidth, CanvasHeight, PixelFormat.Format32bppPArgb);
            var target = output.LockBits(new Rectangle(0, 0, CanvasWidth, CanvasHeight), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            try
            {
                Marshal.Copy(bytes, 0, target.Scan0, length);
            }
            finally { output.UnlockBits(target); }
            return output;
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
        [DllImport("user32.dll", SetLastError = true)] static extern bool UpdateLayeredWindow(IntPtr window, IntPtr screen, ref Point position, ref Size size, IntPtr source, ref Point origin, uint key, ref Blend blend, uint flags);
        [DllImport("user32.dll", SetLastError = true)] static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr value);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr value);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
        internal static void Present(IntPtr window, Bitmap frame, int x, int y)
        {
            IntPtr dc = CreateCompatibleDC(IntPtr.Zero), bitmap = IntPtr.Zero, previous = IntPtr.Zero;
            try
            {
                bitmap = frame.GetHbitmap(Color.FromArgb(0));
                previous = SelectObject(dc, bitmap);
                var position = new Point { X = x, Y = y };
                var origin = new Point();
                var size = new Size { Width = frame.Width, Height = frame.Height };
                var blend = new Blend { Alpha = 255, Format = 1 };
                if (!UpdateLayeredWindow(window, IntPtr.Zero, ref position, ref size, dc, ref origin, 0, ref blend, 2))
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                // Reassert native visibility and z-order after the layered surface
                // changes, without taking keyboard focus from the current app.
                SetWindowPos(window, new IntPtr(-1), 0, 0, 0, 0, 0x53);
            }
            finally
            {
                if (previous != IntPtr.Zero)
                    SelectObject(dc, previous);
                if (bitmap != IntPtr.Zero)
                    DeleteObject(bitmap);
                if (dc != IntPtr.Zero)
                    DeleteDC(dc);
            }
        }
    }

}
