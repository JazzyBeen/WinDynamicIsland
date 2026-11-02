using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using WinDynamicIsland.Core;
namespace WinDynamicIsland
{
    internal sealed partial class Island
    {
        bool IsFocus { get { return mediaData != null && mediaData.Id != null && mediaData.Id.StartsWith("WinDynamicIsland.Focus.", StringComparison.Ordinal); } }
        void DrawFocusGlow(Graphics g, int width, int height, double amount)
        {
            using (var shape = RoundRect(new Rectangle(0, 0, width - 1, height - 1), 64))
            {
                var state = g.Save(); g.SetClip(shape);
                // A slow 24-second drift keeps blue and pink-violet visible together.
                float offset = (float)(Math.Sin(DateTime.UtcNow.TimeOfDay.TotalSeconds * Math.PI / 12) * width * .35);
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
                g.Restore(state);
            }
        }
        internal static void ExportFocusPreview(string folder)
        {
            System.IO.Directory.CreateDirectory(folder);
            using (var island = new Island())
            {
                island.timer.Stop();
                island.mediaData = new MediaSnapshot { Id = "WinDynamicIsland.Focus.work0", Title = L.T("FocusWork"), Artist = "Almost Floating · 50 " + (L.Language == "en" ? "min" : "мин"), AppName = "Pomodoro", Playing = true, CanToggle = true, CanSeek = true, Position = 1320, End = 3000 };
                island.mediaPresentation.Accept(island.mediaData, DateTime.UtcNow);
                using (var face = island.DrawMediaFace(MediaWidth, MediaHeight, 1)) face.Save(System.IO.Path.Combine(folder, "focus-expanded.png"));
                using (var face = island.DrawMediaFace(MediaSmallWidth, MediaSmallHeight, 0)) face.Save(System.IO.Path.Combine(folder, "focus-compact.png"));
            }
        }
    }
}
