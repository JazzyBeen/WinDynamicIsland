using WinDynamicIsland.Core;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Windows.Forms;



namespace WinDynamicIsland
{
    internal sealed partial class Island
    {
        const int MediaWidth = 400, MediaHeight = 176, MediaSmallWidth = 124, MediaSmallHeight = 40;
        bool mediaMode, mediaAvailable, mediaEntering, mediaExiting, mediaDragging;
        MediaSnapshot mediaData;
        string pinnedMediaId;
        DateTime mediaAppeared, mediaMorphAt, mediaCollapseAt, mediaExitAt, mediaPulseAt;
        double mediaFrom, mediaTo, mediaExitFrom, mediaExitSlide, mediaExitDuration, mediaDragPosition;
        double mediaFromWidth, mediaFromHeight, mediaExitWidth, mediaExitHeight;
        readonly MediaPresentation mediaPresentation = new MediaPresentation();
        IntPtr mediaForegroundAtOpen;
        string mediaDebugState;
        DateTime mediaDebugAt;
        long mediaPaintTick = -1;

        public static void SetMedia(MediaSnapshot snapshot)
        {
            lock (Sync)
            {
                if (instance == null && snapshot == null)
                    return;
            }
            try
            {
                Dispatch(current => { try { current.AcceptMedia(snapshot); current.LogMediaState(); } catch (Exception ex) { Program.Log("island-error.txt", ex.ToString()); } });
            }
            catch (Exception ex) { if (snapshot != null) snapshot.Dispose(); Program.Log("island-error.txt", ex.ToString()); }
        }
        void LogMediaState()
        {
            string state = "Visible=" + Visible + "; mediaMode=" + mediaMode + "; eligible=" + MediaEligible + "; exiting=" + mediaExiting + "; notification=" + notification + "; statusLength=" + status.Length + "; queued=" + notices.Count;
            if (state != mediaDebugState)
            {
                mediaDebugState = state;
                Program.Log("island-state.txt", state);
            }
        }
        bool MediaEligible
        {
            get
            {
                return mediaAvailable && mediaData != null && (!Program.Settings.MediaBackgroundOnly || !mediaData.Foreground) && (mediaData.Playing || pinnedMediaId == mediaData.Id);
            }
        }
        void AcceptMedia(MediaSnapshot snapshot)
        {
            bool wasForeground = mediaData != null && mediaData.Foreground;
            if (mediaMode && snapshot != null && snapshot.Playing && !snapshot.Foreground)
                pinnedMediaId = snapshot.Id;
            mediaAvailable = snapshot != null && !snapshot.Stopped;
            if (snapshot != null)
            {
                mediaPresentation.Accept(snapshot, DateTime.UtcNow);
                var old = mediaData;
                mediaData = snapshot;
                if (old != null)
                    old.Dispose();
            }
            dirty = true;
            if (mediaMode)
            {
                if (!MediaEligible)
                    BeginMediaExit();
            }
            else if (!Visible || status.Length == 0 && !notification)
            {
                if (MediaEligible)
                    StartMedia(pinnedMediaId != mediaData.Id || wasForeground && mediaData.Playing);
                else
                    Hide();
            }
        }
        void StartMedia(bool expanded)
        {
            if (!MediaEligible)
                return;
            mediaMode = true;
            notification = false;
            compactKind = CompactNoticeKind.None;
            status = "";
            if (avatar != null)
            {
                avatar.Dispose();
                avatar = null;
            }
            presented = null;
            mediaEntering = true;
            mediaExiting = false;
            mediaDragging = false;
            mediaAppeared = mediaMorphAt = DateTime.UtcNow;
            mediaFrom = 0;
            mediaTo = expanded ? 1 : 0;
            mediaPulseAt = mediaAppeared.AddMilliseconds(OpeningMs);
            mediaForegroundAtOpen = Program.GetForegroundWindow();
            mediaCollapseAt = mediaAppeared.AddMilliseconds(OpeningMs + (Program.Settings.MediaCollapseSeconds * 1000));
            pinnedMediaId = mediaData.Id;
            Cursor = Cursors.Hand;
            dirty = true;
            if (!Visible)
                base.Show();
            AnimateMedia();
        }
        void StopMediaForNotice()
        {
            if (!mediaMode)
                return;
            Hide();
            mediaMode = false;
            mediaExiting = false;
            mediaDragging = false;
            Capture = false;
        }
        void EnqueueNotice(Notice notice)
        {
            if (mediaMode || Visible && (notification || status.Length > 0))
            {
                if (notices.Count >= 20)
                {
                    if (notices.First.Value.Avatar != null)
                        notices.First.Value.Avatar.Dispose();
                    notices.RemoveFirst();
                }
                notices.AddLast(notice);
                if (mediaMode)
                    BeginMediaExit();
            }
            else
                StartNotification(notice);
        }
        void NextNoticeOrMedia()
        {
            if (notices.Count > 0)
            {
                var next = notices.First.Value;
                notices.RemoveFirst();
                StartNotification(next);
            }
            else if (MediaEligible)
                StartMedia(pinnedMediaId != mediaData.Id);
        }
        public static void ShowCompact(CompactNoticeKind kind, string text, int value)
        {
            Dispatch(current => current.EnqueueNotice(new Notice { Title = text, Body = "", App = NotificationApp.System, Kind = kind, Value = value }));
        }
        double MediaAmount(DateTime time)
        {
            if (mediaExiting)
                return mediaExitFrom * Ease((mediaExitDuration - (time - mediaExitAt).TotalMilliseconds - ExpandStartMs) / ExpandMs);
            if (mediaEntering)
                return mediaTo * Ease(((time - mediaAppeared).TotalMilliseconds - ExpandStartMs) / ExpandMs);
            return mediaFrom + (mediaTo - mediaFrom) * Ease((time - mediaMorphAt).TotalMilliseconds / ExpandMs);
        }
        double MediaSlide(DateTime time)
        {
            if (mediaExiting)
                return mediaExitSlide + (1 - mediaExitSlide) * Ease(((time - mediaExitAt).TotalMilliseconds - (mediaExitDuration - SlideMs)) / SlideMs);
            return mediaEntering ? 1 - Ease((time - mediaAppeared).TotalMilliseconds / SlideMs) : 0;
        }
        RectangleF MediaMotionAt(DateTime time)
        {
            double amount = MediaAmount(time), slide = MediaSlide(time);
            double pulse = mediaExiting ? 0 : SettleAt((time - mediaPulseAt).TotalMilliseconds);
            if (mediaExiting)
                return RevealMotion(mediaExitWidth, mediaExitHeight, mediaExitDuration - (time - mediaExitAt).TotalMilliseconds, 0, mediaExitSlide);
            if (mediaEntering)
                return RevealMotion(mediaTo == 1 ? MediaWidth : MediaSmallWidth, mediaTo == 1 ? MediaHeight : MediaSmallHeight, (time - mediaAppeared).TotalMilliseconds, pulse, 0);
            double morph = Ease((time - mediaMorphAt).TotalMilliseconds / ExpandMs);
            float width = (float)((mediaFromWidth + ((mediaTo == 1 ? MediaWidth : MediaSmallWidth) - mediaFromWidth) * morph) * (1 - 0.02 * pulse));
            float height = (float)((mediaFromHeight + ((mediaTo == 1 ? MediaHeight : MediaSmallHeight) - mediaFromHeight) * morph) * (1 + 0.05 * pulse));
            return new RectangleF((IslandSurface.CanvasWidth - width) / 2, TopGap - IslandSurface.OriginY, width, height);
        }
        void MorphMedia(bool expanded)
        {
            var now = DateTime.UtcNow;
            var frame = MediaMotionAt(now);
            mediaFromWidth = frame.Width;
            mediaFromHeight = frame.Height;
            mediaFrom = MediaAmount(now);
            mediaTo = expanded ? 1 : 0;
            mediaMorphAt = now;
            mediaEntering = false;
            mediaPulseAt = now.AddMilliseconds(ExpandMs);
            if (expanded)
                mediaForegroundAtOpen = Program.GetForegroundWindow();
            mediaCollapseAt = now.AddMilliseconds(ExpandMs + (Program.Settings.MediaCollapseSeconds * 1000));
            dirty = true;
        }
        void BeginMediaExit()
        {
            if (!mediaMode || mediaExiting)
                return;
            var now = DateTime.UtcNow;
            var frame = MediaMotionAt(now);
            mediaExitWidth = frame.Width;
            mediaExitHeight = frame.Height;
            mediaExitFrom = MediaAmount(now);
            mediaExitSlide = MediaSlide(now);
            mediaExitAt = now;
            mediaExitDuration = OpeningMs;
            mediaExiting = true;
            mediaDragging = false;
            Capture = false;
            dirty = true;
        }
        void AnimateMedia()
        {
            var now = DateTime.UtcNow;
            if (mediaExiting && (now - mediaExitAt).TotalMilliseconds >= mediaExitDuration + MotionExposureMs)
            {
                Hide();
                mediaMode = false;
                mediaExiting = false;
                dirty = true;
                NextNoticeOrMedia();
                return;
            }
            if (!mediaExiting && mediaTo == 1)
            {
                var mouse = PointToClient(Cursor.Position);
                if (Program.GetForegroundWindow() != mediaForegroundAtOpen)
                    MorphMedia(false);
                else if (MediaMotionAt(now).Contains(mouse.X, mouse.Y))
                    mediaCollapseAt = now.AddMilliseconds((Program.Settings.MediaCollapseSeconds * 1000));
                else if (now >= mediaCollapseAt && !mediaDragging)
                    MorphMedia(false);
            }
            double amount = MediaAmount(now);
            var current = MediaMotionAt(now);
            var trail = MediaMotionAt(now.AddMilliseconds(-MotionExposureMs));
            long tick = now.Ticks / TimeSpan.TicksPerMillisecond / (amount < 0.1 ? 50 : 33);
            bool movingBars = mediaData != null && mediaData.Playing && (amount < 0.1 || amount > 0.85 && mediaData.CanSeek);
            if (!dirty && current == previousFrame && trail == previousTrail && (!movingBars || tick == mediaPaintTick))
                return;
            previousFrame = current;
            previousTrail = trail;
            mediaPaintTick = tick;
            dirty = false;
            var samples = new RectangleF[current == trail ? 1 : MotionSamples];
            for (int i = 0; i < samples.Length; i++)
                samples[i] = MediaMotionAt(now.AddMilliseconds(-MotionExposureMs * i / Math.Max(1, samples.Length - 1)));
            using (var face = DrawMediaFace((int)Math.Round(MediaSmallWidth + (MediaWidth - MediaSmallWidth) * amount), (int)Math.Round(MediaSmallHeight + (MediaHeight - MediaSmallHeight) * amount), amount))
            using (var frame = IslandSurface.Render(face, samples))
            {
                var area = Program.TargetScreen.Bounds;
                IslandSurface.Present(Handle, frame, area.Left + (area.Width - IslandSurface.CanvasWidth) / 2, area.Top + (Preview ? 16 : IslandSurface.OriginY));
                if (now >= mediaDebugAt)
                {
                    mediaDebugAt = now.AddSeconds(2);
                    Program.Log("island-frame.txt", "Visible=" + Visible + "; window=" + Bounds + "; body=" + current + "; amount=" + amount.ToString("F2") + "; screen=" + area);
                }
            }
        }
        void MediaOutsideClick(Point point)
        {
            if (!mediaMode || mediaExiting || mediaTo != 1)
                return;
            var local = PointToClient(point);
            if (!MediaMotionAt(DateTime.UtcNow).Contains(local.X, local.Y))
                MorphMedia(false);
        }
        static void TextLine(Graphics g, string text, Font font, Brush brush, RectangleF box)
        {
            using (var format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                g.DrawString(text ?? "", font, brush, box, format);
        }
        Bitmap DrawMediaFace(int width, int height, double amount)
        {
            var face = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(face))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                using (var path = RoundRect(new Rectangle(0, 0, width - 1, height - 1), 64))
                    g.FillPath(Brushes.Black, path);
                if (mediaData == null)
                    return face;
                int iconSize = (int)(26 + 26 * amount), iconY = (int)((height - iconSize) / 2 * (1 - amount) + 18 * amount);
                var iconRect = new Rectangle(18, iconY, iconSize, iconSize);
                var image = amount > 0.6 ? (mediaData.Artwork ?? mediaData.Icon) : mediaData.Icon;
                if (image != null)
                {
                    using (var path = RoundRect(iconRect, 12))
                    {
                        var saved = g.Save();
                        g.SetClip(path);
                        g.DrawImage(image, iconRect);
                        g.Restore(saved);
                    }
                }
                else
                {
                    using (var font = new Font("Segoe UI Symbol", iconSize * 0.72f, FontStyle.Bold))
                        g.DrawString("♫", font, Brushes.White, iconRect.X - 3, iconRect.Y - 5);
                }
                if (amount < 0.4)
                {
                    using (var pen = new Pen(Color.FromArgb((int)(255 * (1 - Ease(amount / 0.4))), 111, 235, 166), 3))
                    {
                        pen.StartCap = pen.EndCap = LineCap.Round;
                        for (int i = 0; i < 4; i++)
                        {
                            float bar = mediaData.Playing ? (float)(6 + 11 * (0.5 + 0.5 * Math.Sin(mediaPaintTick * 0.7 + i * 1.8))) : 5;
                            float x = width - 38 + i * 5;
                            g.DrawLine(pen, x, (height - bar) / 2, x, (height + bar) / 2);
                        }
                    }
                }
                if (amount <= 0.35)
                    return face;
                int alpha = (int)(255 * Ease((amount - 0.35) / 0.65));
                using (var white = new SolidBrush(Color.FromArgb(alpha, Color.White)))
                {
                    using (var titleFont = new Font("Segoe UI", 11, FontStyle.Bold))
                    using (var subtitleFont = new Font("Segoe UI", 9, FontStyle.Regular))
                    using (var grey = new SolidBrush(Color.FromArgb(alpha, 170, 174, 184)))
                    {
                        TextLine(g, mediaData.Title, titleFont, white, new RectangleF(88, 20, width - 110, 26));
                        TextLine(g, string.IsNullOrWhiteSpace(mediaData.Artist) ? mediaData.AppName : mediaData.Artist, subtitleFont, grey, new RectangleF(88, 49, width - 110, 22));
                    }
                    if (mediaData.Artwork != null && mediaData.Icon != null)
                    {
                        g.FillEllipse(Brushes.Black, 57, 55, 20, 20);
                        g.DrawImage(mediaData.Icon, 59, 57, 16, 16);
                    }
                    float trackWidth = width - 44;
                    double range = mediaData.End - mediaData.Start;
                    double position = mediaDragging ? mediaDragPosition : mediaPresentation.PositionAt(DateTime.UtcNow);
                    float fraction = range > 0 ? (float)Math.Max(0, Math.Min(1, (position - mediaData.Start) / range)) : 0;
                    using (var background = new Pen(Color.FromArgb(alpha, 55, 55, 60), 4))
                    using (var filled = new Pen(Color.FromArgb(alpha, 205, 208, 214), 4))
                    {
                        background.StartCap = background.EndCap = filled.StartCap = filled.EndCap = LineCap.Round;
                        g.DrawLine(background, 22, 98, width - 22, 98);
                        if (fraction > 0)
                            g.DrawLine(filled, 22, 98, 22 + trackWidth * fraction, 98);
                    }
                    using (var small = new Font("Segoe UI", 8))
                    using (var grey = new SolidBrush(Color.FromArgb(alpha, 143, 147, 157)))
                    {
                        g.DrawString(TimeLabel(position - mediaData.Start), small, grey, 20, 106);
                        string end = range > 0 ? "−" + TimeLabel(mediaData.End - position) : "LIVE";
                        var size = g.MeasureString(end, small);
                        g.DrawString(end, small, grey, width - 22 - size.Width, 106);
                    }
                    MediaIcons.Draw(g, width / 2 - 78, 148, false, mediaData.CanSeek, alpha);
                    MediaIcons.Draw(g, width / 2 + 78, 148, true, mediaData.CanSeek, alpha);
                    using (var brush = new SolidBrush(Color.FromArgb(alpha, mediaData.CanToggle ? Color.White : Color.FromArgb(80, 80, 85))))
                    {
                        if (mediaData.Playing)
                        {
                            g.FillRectangle(brush, width / 2 - 9, 137, 6, 22);
                            g.FillRectangle(brush, width / 2 + 3, 137, 6, 22);
                        }
                        else
                            g.FillPolygon(brush, new[] { new Point(width / 2 - 7, 136), new Point(width / 2 - 7, 160), new Point(width / 2 + 13, 148) });
                    }
                }
            }
            return face;
        }
        static string TimeLabel(double seconds)
        {
            var span = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return span.TotalHours >= 1 ? ((int)span.TotalHours) + ":" + span.Minutes.ToString("00") + ":" + span.Seconds.ToString("00") : ((int)span.TotalMinutes) + ":" + span.Seconds.ToString("00");
        }
        PointF MediaPoint(MouseEventArgs e)
        {
            var rect = MediaMotionAt(DateTime.UtcNow);
            return new PointF((e.X - rect.X) * MediaWidth / rect.Width, (e.Y - rect.Y) * MediaHeight / rect.Height);
        }
        void MediaMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || mediaExiting || mediaData == null)
                return;
            if (MediaAmount(DateTime.UtcNow) < 0.5)
            {
                MorphMedia(true);
                return;
            }
            var point = MediaPoint(e);
            mediaCollapseAt = DateTime.UtcNow.AddMilliseconds((Program.Settings.MediaCollapseSeconds * 1000));
            if (point.Y >= 84 && point.Y <= 124 && mediaData.CanSeek)
            {
                mediaDragging = true;
                Capture = true;
                UpdateMediaDrag(point.X);
                return;
            }
            if (point.Y >= 128 && point.Y <= 173)
            {
                if (Math.Abs(point.X - MediaWidth / 2) < 25 && mediaData.CanToggle)
                    SendMediaCommand("toggle", 0);
                else if (mediaData.CanSeek && Math.Abs(point.X - (MediaWidth / 2 - 78)) < 25)
                    SendMediaCommand("seek", mediaPresentation.PositionAt(DateTime.UtcNow) - 10);
                else if (mediaData.CanSeek && Math.Abs(point.X - (MediaWidth / 2 + 78)) < 25)
                    SendMediaCommand("seek", mediaPresentation.PositionAt(DateTime.UtcNow) + 10);
            }
        }
        void MediaMouseMove(MouseEventArgs e)
        {
            if (mediaDragging)
                UpdateMediaDrag(MediaPoint(e).X);
            if (mediaTo == 1)
                mediaCollapseAt = DateTime.UtcNow.AddMilliseconds((Program.Settings.MediaCollapseSeconds * 1000));
        }
        void MediaMouseUp(MouseEventArgs e)
        {
            if (!mediaDragging)
                return;
            UpdateMediaDrag(MediaPoint(e).X);
            mediaDragging = false;
            Capture = false;
            SendMediaCommand("seek", mediaDragPosition);
        }
        void UpdateMediaDrag(float x)
        {
            if (mediaData == null)
                return;
            mediaDragPosition = mediaData.Start + (mediaData.End - mediaData.Start) * Math.Max(0, Math.Min(1, (x - 22) / (MediaWidth - 44)));
            dirty = true;
        }
        void SendMediaCommand(string name, double value)
        {
            if (mediaData != null && mediaData.Command != null)
            {
                if (name == "seek")
                    mediaPresentation.Seek(value, DateTime.UtcNow);
                mediaData.Command(name, value);
                mediaCollapseAt = DateTime.UtcNow.AddMilliseconds((Program.Settings.MediaCollapseSeconds * 1000));
                dirty = true;
            }
        }

        void DrawCompactNotice(Graphics g, int width, int height)
        {
            if (compactKind == CompactNoticeKind.Charging)
            {
                DrawChargingNotice(g, width, height);
                return;
            }
            if (compactKind == CompactNoticeKind.Headphones)
            {
                using (var pen = new Pen(Color.FromArgb(225, 229, 234), 3))
                {
                    pen.StartCap = pen.EndCap = LineCap.Round;
                    g.DrawArc(pen, 19, 10, 19, 19, 180, 180);
                    g.DrawLine(pen, 19, 20, 19, 29);
                    g.DrawLine(pen, 38, 20, 38, 29);
                }
                using (var font = new Font("Segoe UI", 9, FontStyle.Bold))
                    TextLine(g, title, font, Brushes.White, new RectangleF(51, 12, width - 95, 20));
                using (var pen = new Pen(Color.FromArgb(65, 219, 133), 2))
                {
                    g.DrawEllipse(pen, width - 36, 9, 22, 22);
                    g.DrawLines(pen, new[] { new Point(width - 31, 20), new Point(width - 26, 24), new Point(width - 19, 16) });
                }
            }
            else
            {
                using (var font = new Font("Segoe UI", 9, FontStyle.Bold))
                    TextLine(g, title, font, Brushes.White, new RectangleF(17, 12, 135, 20));
                using (var red = new SolidBrush(compactKind == CompactNoticeKind.Charging ? Color.FromArgb(85, 223, 150) : Color.FromArgb(255, 83, 99)))
                using (var font = new Font("Segoe UI", 10, FontStyle.Bold))
                {
                    string value = compactValue >= 0 ? compactValue + "%" : "!";
                    g.DrawString(value, font, red, width - 91, 10);
                    using (var outline = new Pen(Color.FromArgb(110, 42, 48), 1.5f))
                        g.DrawRectangle(outline, width - 48, 12, 29, 16);
                    g.FillRectangle(red, width - 46, 14, Math.Max(3, 25 * Math.Max(0, compactValue) / 100), 12);
                    g.FillRectangle(red, width - 17, 17, 3, 6);
                }
            }
        }
        internal static void ExportFeaturePreview(string folder)
        {
            Directory.CreateDirectory(folder);
            using (var island = new Island())
            {
                island.timer.Stop();
                string appName;
                island.mediaData = new MediaSnapshot { Id = "Yandex.H3PIY6KEH3URTYXLWJYZZW5KEM", Title = "Название трека", Artist = "Исполнитель", AppName = "Яндекс Браузер", Playing = true, CanToggle = true, CanSeek = true, Position = 73, End = 218, Icon = AppIdentity.IconFor("Yandex.H3PIY6KEH3URTYXLWJYZZW5KEM", out appName) };
                island.mediaPresentation.Accept(island.mediaData, DateTime.UtcNow);
                using (var face = island.DrawMediaFace(MediaWidth, MediaHeight, 1))
                    face.Save(Path.Combine(folder, "media-expanded.png"));
                using (var face = island.DrawMediaFace(MediaSmallWidth, MediaSmallHeight, 0))
                    face.Save(Path.Combine(folder, "media-compact.png"));
                island.compactKind = CompactNoticeKind.Headphones;
                island.title = "CMF Buds Pro 2";
                using (var face = island.DrawFace(260, 40, 1))
                    face.Save(Path.Combine(folder, "headphones.png"));
                island.compactKind = CompactNoticeKind.Battery;
                island.compactValue = 20;
                using (var face = island.DrawFace(260, 40, 1))
                    face.Save(Path.Combine(folder, "battery.png"));
                island.compactKind = CompactNoticeKind.Charging;
                island.compactValue = 100;
                island.title = L.T("ChargingOn");
                using (var face = island.DrawFace(island.CompactNoticeWidth, 40, 1))
                    face.Save(Path.Combine(folder, "charging.png"));
            }
        }
    }

}
