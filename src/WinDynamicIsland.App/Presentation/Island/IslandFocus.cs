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
        void DrawFocusGlow(Graphics g, int width, int height, double amount)
        {
            double seconds = focusPreviewSeconds ?? focusTextureClock.Elapsed.TotalSeconds;
            using (var shape = RoundRect(new Rectangle(0, 0, width - 1, height - 1), 64))
            {
                var state = g.Save(); g.SetClip(shape);
                // A slow 24-second drift keeps blue and pink-violet visible together.
                float offset = (float)(Math.Sin(seconds * Math.PI / 12) * width * .35);
                using (var brush = new LinearGradientBrush(new RectangleF(-width * .4f + offset, 0, width * 1.8f, height), Color.Blue, Color.Blue, 0f))
                {
                    brush.WrapMode = WrapMode.TileFlipX;
                    brush.InterpolationColors = new ColorBlend
                    {
                        Positions = new[] { 0f, .25f, .5f, .75f, 1f },
                        Colors = new[] { Color.FromArgb(25, 88, 255), Color.FromArgb(83, 61, 241), Color.FromArgb(211, 62, 201), Color.FromArgb(99, 55, 232), Color.FromArgb(25, 88, 255) }
                    };
                    g.FillRectangle(brush, 0, 0, width, height);
                }
                using (var shade = new SolidBrush(Color.FromArgb((int)(90 + 115 * amount), Color.Black)))
                    g.FillRectangle(shade, 0, 0, width, height);
                // Blend the compact treatment away during the existing expansion animation.
                double compact = 1 - Ease(amount / .7);
                if (compact > 0)
                {
                    DrawFocusEdges(g, width, height, compact, seconds);
                }
                g.Restore(state);
            }
        }
        static double FocusCapsuleInset(double x, double y, int width, int height)
        {
            double radius = (height - 1) / 2d;
            double spineX = Math.Max(radius, Math.Min(width - 1 - radius, x));
            double dx = x - spineX, dy = y - radius;
            return radius - Math.Sqrt(dx * dx + dy * dy);
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
        static void DrawFocusEdges(Graphics g, int width, int height, double opacity, double seconds)
        {
            // Distance to the entire capsule perimeter gives equal falloff at the
            // top, bottom and rounded ends, with a genuinely black inner capsule.
            int depth = Math.Max(4, Math.Min(14, (int)(height * .32)));
            int alpha = (int)(255 * opacity);
            var pixels = new byte[width * height * 4];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    double inset = FocusCapsuleInset(x, y, width, height);
                    double edge = 1 - Ease(Math.Max(0, inset) / depth);
                    double angle = Math.Atan2((y - (height - 1) / 2d) / (height / 2d), (x - (width - 1) / 2d) / (width / 2d));
                    double flow = .5 + .5 * Math.Sin(angle - seconds * Math.PI / 16);
                    double glow = edge * (.68 + .08 * Math.Sin(angle * 2 + seconds * Math.PI / 21));
                    int at = (y * width + x) * 4;
                    pixels[at] = (byte)((188 - 15 * flow) * glow * alpha / 255);
                    pixels[at + 1] = (byte)((70 - 24 * flow) * glow * alpha / 255);
                    pixels[at + 2] = (byte)((30 + 77 * flow) * glow * alpha / 255);
                    pixels[at + 3] = (byte)alpha;
                }
            using (var field = new Bitmap(width, height, PixelFormat.Format32bppPArgb))
            {
                var data = field.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
                try { Marshal.Copy(pixels, 0, data.Scan0, pixels.Length); }
                finally { field.UnlockBits(data); }
                g.DrawImageUnscaled(field, 0, 0);
            }

            var state = g.Save();
            using (var ring = new Region(new Rectangle(0, 0, width, height)))
            using (var middle = RoundRect(new Rectangle(depth, depth, width - 1 - depth * 2, height - 1 - depth * 2), height - 1 - depth * 2))
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
                        double edge = 1 - Ease(Math.Max(0, FocusCapsuleInset(x, y, width, height)) / depth);
                        if (edge <= 0) continue;
                        // Stable random properties per dot: no per-frame randomness or flicker.
                        uint seed = FocusDotHash(row, column);
                        double phase = (seed & 1023) / 1024d * Math.PI * 2;
                        double period = 9 + ((seed >> 10) & 1023) / 1023d * 12;
                        double pulse = .5 + .5 * Math.Sin(seconds * Math.PI * 2 / period + phase);
                        double personality = ((seed >> 20) & 255) / 255d;
                        float size = (float)((.75 + pulse * (1.6 + personality * .8)) * (.65 + .35 * edge));
                        int dotAlpha = (int)((28 + pulse * 46) * edge * opacity);
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
                }
            }
        }
    }
}
