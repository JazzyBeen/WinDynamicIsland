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
                // One slow 18-second drift, no strobing or sharp highlights.
                float offset = (float)(Math.Sin(DateTime.UtcNow.TimeOfDay.TotalSeconds * Math.PI / 9) * width * .25);
                using (var brush = new LinearGradientBrush(new RectangleF(-width + offset, 0, width * 3, height), Color.FromArgb(25, 80, 220), Color.FromArgb(100, 38, 190), 0f))
                    g.FillRectangle(brush, 0, 0, width, height);
                using (var shade = new SolidBrush(Color.FromArgb((int)(110 + 95 * amount), Color.Black)))
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
