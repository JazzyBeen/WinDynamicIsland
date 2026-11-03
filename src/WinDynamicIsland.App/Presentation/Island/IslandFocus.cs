using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using WinDynamicIsland.Core;
namespace WinDynamicIsland
{
    internal sealed partial class Island
    {
        bool IsFocus { get { return mediaData != null && mediaData.Id != null && mediaData.Id.StartsWith("WinDynamicIsland.Focus.", StringComparison.Ordinal); } }
        double? focusPreviewSeconds;
        readonly Stopwatch focusTextureClock = Stopwatch.StartNew();
        readonly FocusMotionClock focusMotion = new FocusMotionClock();
        FocusEdgeField focusField;
        void BoostFocusTexture(double openingMilliseconds)
        {
            focusMotion.Pulse(focusTextureClock.Elapsed.TotalSeconds, openingMilliseconds / 1000);
        }
        void DrawFocusGlow(Graphics g, int width, int height)
        {
            double seconds = focusPreviewSeconds ?? focusMotion.Read(focusTextureClock.Elapsed.TotalSeconds);
            using (var shape = RoundRect(new Rectangle(0, 0, width - 1, height - 1), 64))
            {
                var state = g.Save();
                g.SetClip(shape);
                DrawFocusEdges(g, width, height, seconds);
                g.Restore(state);
            }
        }
        static double FocusShapeInset(double x, double y, int width, int height)
        {
            double cx = (width - 1) / 2d, cy = (height - 1) / 2d;
            double radius = Math.Min(32, Math.Min(cx, cy));
            double qx = Math.Abs(x - cx) - (cx - radius), qy = Math.Abs(y - cy) - (cy - radius);
            double dx = Math.Max(0, qx), dy = Math.Max(0, qy);
            return radius - Math.Sqrt(dx * dx + dy * dy) - Math.Min(Math.Max(qx, qy), 0);
        }
        // Reuse the geometry and pixel buffer while the island rests at one size.
        // The per-frame pass uses angle addition, avoiding per-pixel trig calls.
        sealed class FocusEdgeField : IDisposable
        {
            internal readonly int Width, Height, Depth;
            readonly float[] edges, sin, cos, sin2, cos2;
            readonly byte[] pixels;
            readonly Bitmap bitmap;
            internal FocusEdgeField(int width, int height)
            {
                Width = width; Height = height;
                Depth = Math.Max(4, Math.Min(30, (int)(height * .32)));
                int count = width * height;
                edges = new float[count]; sin = new float[count]; cos = new float[count];
                sin2 = new float[count]; cos2 = new float[count]; pixels = new byte[count * 4];
                bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        int i = y * width + x;
                        edges[i] = (float)(1 - Ease(Math.Max(0, FocusShapeInset(x, y, width, height)) / Depth));
                        double angle = Math.Atan2((y - (height - 1) / 2d) / (height / 2d), (x - (width - 1) / 2d) / (width / 2d));
                        sin[i] = (float)Math.Sin(angle); cos[i] = (float)Math.Cos(angle);
                        sin2[i] = (float)Math.Sin(angle * 2); cos2[i] = (float)Math.Cos(angle * 2);
                    }
            }
            internal Bitmap Render(double seconds)
            {
                double sm = Math.Sin(seconds * Math.PI / 16), cm = Math.Cos(seconds * Math.PI / 16);
                double sp = Math.Sin(seconds * Math.PI / 21), cp = Math.Cos(seconds * Math.PI / 21);
                for (int i = 0; i < edges.Length; i++)
                {
                    double flow = .5 + .5 * (sin[i] * cm - cos[i] * sm);
                    double glow = edges[i] * (.68 + .08 * (sin2[i] * cp + cos2[i] * sp));
                    int at = i * 4;
                    pixels[at] = (byte)((188 - 15 * flow) * glow);
                    pixels[at + 1] = (byte)((70 - 24 * flow) * glow);
                    pixels[at + 2] = (byte)((30 + 77 * flow) * glow);
                    pixels[at + 3] = 255;
                }
                var data = bitmap.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
                try { Marshal.Copy(pixels, 0, data.Scan0, pixels.Length); }
                finally { bitmap.UnlockBits(data); }
                return bitmap;
            }
            public void Dispose() { bitmap.Dispose(); }
        }
        static uint FocusDotHash(int row, int column)
        {
            unchecked
            {
                uint value = (uint)row * 374761393u + (uint)column * 668265263u;
                value = (value ^ (value >> 13)) * 1274126177u;
                return value ^ (value >> 16);
            }
        }
        void DrawFocusEdges(Graphics g, int width, int height, double seconds)
        {
            if (focusField == null || focusField.Width != width || focusField.Height != height)
            {
                if (focusField != null) focusField.Dispose();
                focusField = new FocusEdgeField(width, height);
            }
            int depth = focusField.Depth;
            g.DrawImageUnscaled(focusField.Render(seconds), 0, 0);
            var state = g.Save();
            using (var ring = new Region(new Rectangle(0, 0, width, height)))
            using (var middle = RoundRect(new Rectangle(depth, depth, width - 1 - depth * 2, height - 1 - depth * 2), Math.Max(2, Math.Min(64, Math.Min(width - 1, height - 1)) - depth * 2)))
            using (var dot = new SolidBrush(Color.White))
            {
                // Clipping also keeps enlarged dots out of the black central region.
                ring.Exclude(middle);
                g.SetClip(ring, CombineMode.Intersect);
                const double pitch = 7;
                double driftX = seconds * .42, driftY = seconds * .19;
                int firstRow = (int)Math.Floor(-driftY / pitch) - 1;
                int lastRow = (int)Math.Ceiling((height - driftY) / pitch) + 1;
                int firstColumn = (int)Math.Floor(-driftX / pitch) - 1;
                int lastColumn = (int)Math.Ceiling((width - driftX) / pitch) + 1;
                for (int row = firstRow; row <= lastRow; row++)
                    for (int column = firstColumn; column <= lastColumn; column++)
                    {
                        double x = column * pitch + ((row & 1) == 0 ? 0 : pitch / 2) + driftX;
                        double y = row * pitch + driftY;
                        double edge = 1 - Ease(Math.Max(0, FocusShapeInset(x, y, width, height)) / depth);
                        if (edge <= 0) continue;
                        // Stable random properties per dot: no per-frame randomness or flicker.
                        uint seed = FocusDotHash(row, column);
                        double phase = (seed & 1023) / 1024d * Math.PI * 2;
                        double period = 9 + ((seed >> 10) & 1023) / 1023d * 12;
                        double pulse = .5 + .5 * Math.Sin(seconds * Math.PI * 2 / period + phase);
                        double personality = ((seed >> 20) & 255) / 255d;
                        float size = (float)((.75 + pulse * (1.6 + personality * .8)) * (.65 + .35 * edge));
                        int dotAlpha = (int)((28 + pulse * 46) * edge);
                        if (dotAlpha < 2) continue;
                        dot.Color = Color.FromArgb(dotAlpha, 167, 153, 222);
                        g.FillEllipse(dot, (float)x - size / 2, (float)y - size / 2, size, size);
                    }
            }
            g.Restore(state);
        }
        internal static void ExportFocusPreview(string folder)
        {
            System.IO.Directory.CreateDirectory(folder);
            using (var island = new Island())
            {
                island.timer.Stop();
                island.focusPreviewSeconds = 0;
                island.mediaData = new MediaSnapshot { Id = "WinDynamicIsland.Focus.work0", Title = L.T("FocusWork"), Artist = "Almost Floating · 50 " + (L.Language == "en" ? "min" : "мин"), AppName = "Pomodoro", Playing = true, CanToggle = true, CanSeek = true, Position = 1320, End = 3000 };
                island.mediaPresentation.Accept(island.mediaData, DateTime.UtcNow);
                using (var face = island.DrawMediaFace(MediaWidth, MediaHeight, 1)) face.Save(System.IO.Path.Combine(folder, "focus-expanded.png"));
                using (var face = island.DrawMediaFace(MediaSmallWidth, MediaSmallHeight, 0)) face.Save(System.IO.Path.Combine(folder, "focus-compact.png"));
                for (int i = 0; i < 4; i++)
                {
                    island.focusPreviewSeconds = i * 8;
                    using (var face = island.DrawMediaFace(MediaSmallWidth, MediaSmallHeight, 0))
                        face.Save(System.IO.Path.Combine(folder, "focus-compact-" + i + ".png"));
                    using (var face = island.DrawMediaFace(MediaWidth, MediaHeight, 1))
                        face.Save(System.IO.Path.Combine(folder, "focus-expanded-" + i + ".png"));
                }
                var motion = new FocusMotionClock();
                motion.Pulse(0, ExpandMs / 1000);
                double[] moments = { 0, .08, .14, .28, .36, .5, .8, 1.2 };
                for (int i = 0; i < moments.Length; i++)
                {
                    double amount = Ease(moments[i] * 1000 / ExpandMs);
                    island.focusPreviewSeconds = motion.Read(moments[i]);
                    using (var face = island.DrawMediaFace((int)(MediaSmallWidth + (MediaWidth - MediaSmallWidth) * amount), (int)(MediaSmallHeight + (MediaHeight - MediaSmallHeight) * amount), amount))
                        face.Save(System.IO.Path.Combine(folder, "focus-opening-" + i + ".png"));
                }
                using (var warmup = island.DrawMediaFace(MediaWidth, MediaHeight, 1)) { }
                var watch = Stopwatch.StartNew();
                for (int i = 0; i < 30; i++)
                {
                    island.focusPreviewSeconds = i / 30d;
                    using (var frame = island.DrawMediaFace(MediaWidth, MediaHeight, 1)) { }
                }
                System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "focus-render.txt"), "Expanded focus face average: " + (watch.Elapsed.TotalMilliseconds / 30).ToString("F2") + " ms");
            }
        }
    }
}
