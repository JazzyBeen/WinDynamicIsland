using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Windows.Forms;
using WinDynamicIsland.Core;
namespace WinDynamicIsland
{
    internal sealed partial class Island
    {
        Bitmap DrawMaterialMediaFace(int width, int height, double amount)
        {
            var face = NewFace(width, height);
            using (var g = Graphics.FromImage(face))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                AppDesign.Fill(g, new RectangleF(0, 0, width - 1, height - 1), Math.Min(32, height / 2f), AppDesign.Pick(Color.Black, amount < .25 ? AppDesign.PrimaryContainer : AppDesign.Container));
                if (mediaData == null) return face;
                if (amount < .4)
                {
                    if (!IsFocus && mediaData.Icon != null)
                    {
                        var saved = g.Save();
                        using (var clip = IslandTheme.Round(new RectangleF(16, (height - 26) / 2f, 26, 26), 8)) g.SetClip(clip);
                        g.DrawImage(mediaData.Icon, new RectangleF(16, (height - 26) / 2f, 26, 26)); g.Restore(saved);
                    }
                    else AppDesign.Symbol(g, IsFocus ? "\ue425" : "\ue310", new RectangleF(14, (height - 26) / 2f, 26, 26), AppDesign.OnContainer, 24);
                    if (IsFocus)
                        using (var font = AppTypography.Create(11, FontStyle.Bold))
                        using (var brush = new SolidBrush(AppDesign.OnContainer))
                            TextLine(g, Math.Max(0, (int)Math.Ceiling((mediaData.End - mediaPresentation.PositionAt(PreciseClock.UtcNow)) / 60)) + (L.Language == "en" ? " min" : " мин"), font, brush, new RectangleF(49, (height - 22) / 2f, width - 54, 24));
                    else
                        using (var pen = new Pen(AppDesign.OnContainer, 3))
                        {
                            pen.StartCap = pen.EndCap = LineCap.Round;
                            for (int i = 0; i < 4; i++)
                            {
                                float bar = mediaData.Playing ? (float)(6 + 10 * (.5 + .5 * Math.Sin(mediaPaintTick * .5 + i * 1.8))) : 5;
                                g.DrawLine(pen, width - 35 + i * 5, (height - bar) / 2, width - 35 + i * 5, (height + bar) / 2);
                            }
                        }
                    return face;
                }
                int alpha = (int)(255 * Ease((amount - .35) / .65));
                int textX = IsFocus ? 24 : 84;
                if (!IsFocus)
                {
                    var image = mediaData.Artwork ?? mediaData.Icon;
                    if (image != null)
                    {
                        var saved = g.Save();
                        using (var clip = IslandTheme.Round(new RectangleF(24, 20, 44, 44), 14)) g.SetClip(clip);
                        g.DrawImage(image, new Rectangle(24, 20, 44, 44)); g.Restore(saved);
                    }
                    else
                    {
                        AppDesign.Fill(g, new RectangleF(24, 20, 44, 44), 16, AppDesign.PrimaryContainer);
                        AppDesign.Symbol(g, "\ue405", new RectangleF(24, 20, 44, 44), AppDesign.OnContainer, 28);
                    }
                }
                using (var titleFont = AppTypography.Create(IsFocus ? 16 : 13, FontStyle.Bold))
                using (var detailFont = AppTypography.Create(9))
                using (var ink = new SolidBrush(Color.FromArgb(alpha, AppDesign.Ink)))
                using (var muted = new SolidBrush(Color.FromArgb(alpha, AppDesign.Muted)))
                {
                    string track = IsFocus ? (string.IsNullOrWhiteSpace(mediaData.TrackTitle) ? L.T("FocusSilent") : mediaData.TrackTitle) : mediaData.Title;
                    string detail = IsFocus ? mediaData.PlaylistName : string.IsNullOrWhiteSpace(mediaData.Artist) ? mediaData.AppName : mediaData.Artist;
                    TextLine(g, track, titleFont, ink, new RectangleF(textX, 19, Math.Max(1, width - textX - 24), 30));
                    TextLine(g, detail, detailFont, muted, new RectangleF(textX, 49, Math.Max(1, width - textX - 24), 22));
                }
                double position = mediaDragging ? mediaDragPosition : mediaPresentation.PositionAt(PreciseClock.UtcNow), range = mediaData.End - mediaData.Start;
                float fraction = range > 0 ? (float)Math.Max(0, Math.Min(1, (position - mediaData.Start) / range)) : 0;
                float end = 22 + (width - 44) * fraction;
                using (var trackPen = new Pen(Color.FromArgb(alpha, AppDesign.Highest), 4))
                using (var fill = new Pen(Color.FromArgb(alpha, AppDesign.Primary), 4))
                {
                    trackPen.StartCap = trackPen.EndCap = fill.StartCap = fill.EndCap = LineCap.Round;
                    if (end + 9 < width - 22) g.DrawLine(trackPen, end + 9, 98, width - 22, 98);
                    if (fraction > 0)
                    {
                        var wave = new PointF[Math.Max(2, (int)(end - 22) / 2 + 1)];
                        double phase = Program.Settings.InterfaceAnimations && mediaData.Playing ? PreciseClock.UtcNow.Ticks / (double)TimeSpan.TicksPerMillisecond / 350 : 0;
                        for (int i = 0; i < wave.Length; i++) { float x = 22 + (end - 22) * i / (wave.Length - 1); wave[i] = new PointF(x, 98 + (float)Math.Sin(x / 7 - phase) * 2); }
                        g.DrawLines(fill, wave);
                    }
                }
                using (var thumb = new SolidBrush(Color.FromArgb(alpha, AppDesign.Primary))) g.FillEllipse(thumb, end - 4, 94, 8, 8);
                if (!IsFocus)
                    using (var small = AppTypography.Create(8))
                    using (var muted = new SolidBrush(Color.FromArgb(alpha, AppDesign.Muted)))
                    {
                        TextLine(g, TimeLabel(position - mediaData.Start), small, muted, new RectangleF(22, 109, 90, 18));
                        using (var format = new StringFormat { Alignment = StringAlignment.Far })
                            g.DrawString(range > 0 ? "−" + TimeLabel(mediaData.End - position) : "LIVE", small, muted, new RectangleF(width - 112, 109, 90, 18), format);
                    }
                for (int i = 0; i < 3; i++)
                {
                    RectangleF box = MaterialMediaButton(i, width);
                    AppDesign.Fill(g, box, i == 1 ? (mediaData.Playing ? 16 : 24) : 20, Color.FromArgb(alpha, i == 1 ? AppDesign.Primary : AppDesign.Secondary));
                    string symbol = i == 1 ? (mediaData.Playing ? "\ue034" : "\ue037") : i == 0 ? "\ue059" : "\ue056";
                    bool enabled = i == 1 ? mediaData.CanToggle : mediaData.CanSeek;
                    AppDesign.Symbol(g, symbol, box, Color.FromArgb(enabled ? alpha : alpha / 3, i == 1 ? AppDesign.OnPrimary : AppDesign.OnSecondary), i == 1 ? 30 : 25);
                }
            }
            return face;
        }
        static RectangleF MaterialMediaButton(int index, int width)
        {
            const int skipWidth = 64;
            return index == 0 ? new RectangleF(22, 136, skipWidth, 48) : index == 2 ? new RectangleF(width - 22 - skipWidth, 136, skipWidth, 48) : new RectangleF(90, 136, width - 180, 48);
        }
        void MaterialMediaMouseDown(PointF point)
        {
            if (point.Y >= 82 && point.Y <= 126 && mediaData.CanSeek)
            {
                mediaDragging = true; Capture = true; UpdateMediaDrag(point.X); return;
            }
            for (int i = 0; i < 3; i++)
                if (MaterialMediaButton(i, MediaWidth).Contains(point))
                {
                    if (i == 1 && mediaData.CanToggle) SendMediaCommand("toggle", 0);
                    else if (i != 1 && mediaData.CanSeek) SendMediaCommand("seek", mediaPresentation.PositionAt(PreciseClock.UtcNow) + (i == 0 ? -10 : 10));
                    return;
                }
        }
        void DrawMaterialNotice(Graphics g, int width, int height)
        {
            if (IsControlHud) { DrawMaterialHud(g, width, height); return; }
            if (compactKind != CompactNoticeKind.None) { DrawMaterialDevice(g, width, height); return; }
            int size = notification ? 44 : 40;
            int x = width < 170 ? (width - size) / 2 : 20, y = (height - size) / 2;
            var rect = new Rectangle(x, y, size, size);
            AppDesign.Fill(g, rect, notification ? 16 : 14, AppDesign.PrimaryContainer);
            if (notification && avatar != null)
            {
                var saved = g.Save(); using (var clip = IslandTheme.Round(rect, 16)) g.SetClip(clip);
                g.DrawImage(avatar, rect); g.Restore(saved);
            }
            else AppDesign.Symbol(g, notification ? "\ue7f4" : error ? "\ue001" : done ? "\ue876" : "\ue0da", rect, error ? AppDesign.Error : AppDesign.OnContainer, 25);
            if (width < 170) return;
            using (var titleFont = AppTypography.Create(notification ? 12 : 11, FontStyle.Bold))
            using (var bodyFont = AppTypography.Create(10))
            using (var ink = new SolidBrush(AppDesign.Ink))
            using (var muted = new SolidBrush(error ? AppDesign.Error : AppDesign.Muted))
            {
                TextLine(g, title, titleFont, ink, new RectangleF(80, notification ? 20 : 17, width - 100, 28));
                using (var format = new StringFormat { Trimming = StringTrimming.EllipsisWord, FormatFlags = StringFormatFlags.LineLimit })
                    g.DrawString(status, bodyFont, muted, new RectangleF(80, notification ? 50 : 46, Math.Max(1, width - 100), notification ? 58 : 24), format);
            }
        }
        void DrawMaterialDevice(Graphics g, int width, int height)
        {
            bool battery = compactKind == CompactNoticeKind.Battery || compactKind == CompactNoticeKind.Charging;
            Color accent = compactKind == CompactNoticeKind.Battery ? AppDesign.Error : AppDesign.Primary;
            if (battery)
            {
                string label = compactKind == CompactNoticeKind.Charging ? L.T(title == L.T("ChargingOff") ? "OnBatteryShort" : "ChargingShort") : title;
                using (var font = AppTypography.Create(10, FontStyle.Bold))
                using (var ink = new SolidBrush(AppDesign.Ink))
                using (var color = new SolidBrush(accent))
                {
                    TextLine(g, label, font, ink, new RectangleF(16, 11, width - 122, 24));
                    TextLine(g, compactValue < 0 ? "—" : Math.Max(0, Math.Min(100, compactValue)) + "%", font, color, new RectangleF(width - 105, 11, 55, 24));
                }
                AppDesign.Fill(g, new RectangleF(width - 44, 11, 29, 18), 5, AppDesign.Highest);
                AppDesign.Fill(g, new RectangleF(width - 44, 11, 29 * Math.Max(0, Math.Min(100, compactValue)) / 100f, 18), 5, accent);
                AppDesign.Fill(g, new RectangleF(width - 12, 16, 2, 8), 1, accent);
                return;
            }
            AppDesign.Symbol(g, "\ue310", new RectangleF(14, 7, 28, 28), AppDesign.Primary, 25);
            using (var font = AppTypography.Create(10, FontStyle.Bold))
            using (var ink = new SolidBrush(AppDesign.Ink)) TextLine(g, title, font, ink, new RectangleF(52, 10, width - 100, 25));
            AppDesign.Fill(g, new RectangleF(width - 38, 8, 24, 24), 12, AppDesign.PrimaryContainer);
            AppDesign.Symbol(g, "\ue876", new RectangleF(width - 38, 8, 24, 24), AppDesign.OnContainer, 18);
        }
        void DrawMaterialHud(Graphics g, int width, int height)
        {
            float center = height / 2f;
            Color accent = compactKind == CompactNoticeKind.Microphone && hudActive ? AppDesign.Error : AppDesign.Primary;
            if (compactKind == CompactNoticeKind.Volume)
            {
                double wide = hudWide ? Ease((PreciseClock.UtcNow - hudShapeAt).TotalMilliseconds / 170) : 0;
                AppDesign.Symbol(g, hudActive ? "\ue04f" : "\ue050", new RectangleF(13, center - 16, 32, 32), Color.FromArgb((int)(255 * (1 - wide)), AppDesign.Primary), 24);
                float x = (float)(54 - 36 * wide), length = width - x - (float)(50 - 32 * wide), bar = hudWide ? 7 : 10;
                AppDesign.Fill(g, new RectangleF(x, center - bar / 2, length, bar), bar / 2, AppDesign.Secondary);
                float fraction = (float)Math.Max(0, Math.Min(1, HudValueAt(PreciseClock.UtcNow) / 100));
                AppDesign.Fill(g, new RectangleF(x, center - bar / 2, length * fraction, bar), bar / 2, AppDesign.Primary);
                using (var font = AppTypography.Create(9, FontStyle.Bold))
                using (var brush = new SolidBrush(Color.FromArgb((int)(255 * (1 - wide)), AppDesign.Ink))) TextLine(g, hudActive ? "—" : compactValue + "%", font, brush, new RectangleF(width - 44, center - 10, 42, 22));
                return;
            }
            string symbol = compactKind == CompactNoticeKind.Microphone ? (hudActive ? "\ue02b" : "\ue029") : compactKind == CompactNoticeKind.CapsLock ? "\ue318" : "\ue312";
            AppDesign.Symbol(g, symbol, new RectangleF(14, center - 15, 30, 30), accent, 24);
            using (var font = AppTypography.Create(10, FontStyle.Bold))
            using (var ink = new SolidBrush(AppDesign.Ink)) TextLine(g, title, font, ink, new RectangleF(54, center - 11, width - 70, 25));
        }
    }
}
