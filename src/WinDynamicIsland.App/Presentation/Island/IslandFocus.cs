using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using WinDynamicIsland.Core;
namespace WinDynamicIsland
{
    internal sealed partial class Island
    {
        bool IsFocus { get { return mediaData != null && mediaData.Id != null && mediaData.Id.StartsWith("WinDynamicIsland.Focus.", StringComparison.Ordinal); } }
        double? focusPreviewSeconds;
        void DrawFocusGlow(Graphics g, int width, int height, double amount)
        {
            double seconds = focusPreviewSeconds ?? DateTime.UtcNow.TimeOfDay.TotalSeconds;
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
                    using (var black = new SolidBrush(Color.FromArgb((int)(255 * compact), Color.Black)))
                        g.FillRectangle(black, 0, 0, width, height);
                    DrawFocusEdges(g, width, height, compact, seconds);
                }
                g.Restore(state);
            }
        }
        static Color FocusEdgeColor(Color first, Color second, double mix, double light, int alpha)
        {
            return Color.FromArgb(alpha,
                (int)((first.R + (second.R - first.R) * mix) * light),
                (int)((first.G + (second.G - first.G) * mix) * light),
                (int)((first.B + (second.B - first.B) * mix) * light));
        }
        static void DrawFocusEdges(Graphics g, int width, int height, double opacity, double seconds)
        {
            double drift = .5 + .5 * Math.Sin(seconds * Math.PI / 12);
            int alpha = (int)(255 * opacity);
            Color blue = Color.FromArgb(30, 64, 157), violet = Color.FromArgb(84, 42, 139);
            using (var glow = new LinearGradientBrush(new RectangleF(0, 0, width, height), Color.Black, Color.Black, 0f))
            {
                glow.InterpolationColors = new ColorBlend
                {
                    Positions = new[] { 0f, .10f, .22f, .34f, .66f, .78f, .90f, 1f },
                    Colors = new[]
                    {
                        FocusEdgeColor(blue, violet, drift * .45, .90, alpha),
                        FocusEdgeColor(blue, violet, drift * .45, .58, alpha),
                        FocusEdgeColor(blue, violet, drift * .45, .16, alpha),
                        Color.FromArgb(alpha, Color.Black), Color.FromArgb(alpha, Color.Black),
                        FocusEdgeColor(violet, blue, drift * .45, .16, alpha),
                        FocusEdgeColor(violet, blue, drift * .45, .58, alpha),
                        FocusEdgeColor(violet, blue, drift * .45, .90, alpha)
                    }
                };
                g.FillRectangle(glow, 0, 0, width, height);
            }
            // Halftone dots drift as a single soft field and fade toward the middle.
            // The clip explicitly excludes the black central band, even during motion.
            var state = g.Save();
            using (var edges = new Region(new RectangleF(0, 0, width * .34f, height)))
            {
                edges.Union(new RectangleF(width * .66f, 0, width * .34f, height));
                g.SetClip(edges, CombineMode.Intersect);
                const float pitch = 7;
                float dx = (float)(Math.Sin(seconds * Math.PI / 16) * 2.8);
                float dy = (float)(Math.Cos(seconds * Math.PI / 20) * 2.2);
                for (int row = -1; row <= height / pitch + 1; row++)
                    for (int column = -1; column <= width / pitch + 1; column++)
                    {
                        float x = column * pitch + ((row & 1) == 0 ? 0 : pitch / 2) + dx;
                        float y = row * pitch + dy;
                        double edge = 1 - Ease(Math.Max(0, Math.Min(x, width - x)) / (width * .34));
                        double softness = .72 + .28 * Math.Sin(row * .83 + column * .61 + seconds * .12);
                        int dotAlpha = (int)(64 * edge * softness * opacity);
                        if (dotAlpha < 2) continue;
                        float size = (float)(1.5 + 1.3 * edge);
                        using (var dot = new SolidBrush(Color.FromArgb(dotAlpha, 157, 145, 212)))
                            g.FillEllipse(dot, x - size / 2, y - size / 2, size, size);
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
