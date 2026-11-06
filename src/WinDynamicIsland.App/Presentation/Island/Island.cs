using WinDynamicIsland.Core;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Threading;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Diagnostics;

// A small, non-activating notification. This window never controls the VPN.
namespace WinDynamicIsland
{
    internal sealed partial class Island : Form
    {
        private const int CompactWidth = 78, CompactHeight = 44;
        private static int TopGap
        {
            get
            {
                return Program.Settings.TopGap;
            }
        }
        private const double SlideMs = 220, ExpandMs = 280;
        private static double MotionExposureMs
        {
            get
            {
                return Program.Settings.MotionBlur;
            }
        }
        private const double SettleMs = 200, SettlePeak = 0.35;
        private const int MotionSamples = 9;
        // Invert the slide easing at 70% visible height (the final 8 px gap is extra travel).
        private static double ExpandStartMs
        {
            get
            {
                return SlideMs * (0.5 - Math.Sin(Math.Asin(1 - 2 * (CompactHeight * 0.70 / (CompactHeight + TopGap))) / 3));
            }
        }
        private static double OpeningMs
        {
            get
            {
                return ExpandStartMs + ExpandMs;
            }
        }
        private static readonly object Sync = new object();
        private static Island instance;
        private static bool starting;
        internal static void RefreshAppearance()
        {
            lock (Sync)
            {
                var current = instance;
                if (current == null || current.IsDisposed || !current.IsHandleCreated) return;
                try { current.BeginInvoke((Action)delegate { if (!current.IsDisposed) current.dirty = true; }); }
                catch (InvalidOperationException) { }
            }
        }
        public static bool Preview;
        private static readonly ManualResetEvent Ready = new ManualResetEvent(false);
        private readonly System.Windows.Forms.Timer timer = new FrameTimer();
        private readonly Icon logo;
        private readonly OutsideClickWatcher outsideClicks;
        private bool notification;
        private NotificationApp notificationApp;
        private readonly LinkedList<Notice> notices = new LinkedList<Notice>();
        private sealed class Notice
        {
            internal string Title, Body; internal Bitmap Avatar; internal Action Presented; internal NotificationApp App; internal CompactNoticeKind Kind; internal int Value; internal bool Active, Wide;
        }
        private CompactNoticeKind compactKind;
        private int compactValue;
        private Bitmap avatar;
        private Action presented;
        private int HoldMs
        {
            get
            {
                return IsControlHud ? (compactKind == CompactNoticeKind.Volume ? 2200 : 3000) : compactKind != CompactNoticeKind.None ? 4500 : notification ? (Preview ? 30000 : Program.Settings.NotificationSeconds * 1000) : 3000;
            }
        }
        private int ExpandedWidth
        {
            get
            {
                return compactKind != CompactNoticeKind.None ? CompactNoticeWidth : notification ? 460 : 310;
            }
        }
        private int ExpandedHeight
        {
            get
            {
                return compactKind != CompactNoticeKind.None ? 40 : notification ? 124 : 84;
            }
        }
        private string title = "Win Dynamic Island", status = "";
        private bool done, error;
        private DateTime changed;
        private double progress;
        private DateTime appeared;
        private bool dirty = true;
        private RectangleF previousFrame;
        private RectangleF previousTrail;

        private Island()
        {
            FormBorderStyle = FormBorderStyle.None;
            Text = "Win Dynamic Island";
            ShowInTaskbar = Preview;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.Black;
            DoubleBuffered = true;
            appeared = changed = PreciseClock.UtcNow;
            Size = new Size(IslandSurface.CanvasWidth, IslandSurface.CanvasHeight);
            var screen = Program.TargetScreen.Bounds;
            Location = new Point(screen.Left + (screen.Width - Width) / 2, screen.Top + (Preview ? 16 : IslandSurface.OriginY));
            try
            {
                logo = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }
            timer.Tick += delegate (object sender, EventArgs args)
            {
                try
                {
                    Animate(sender, args);
                }
                catch (Exception ex) { Program.Log("island-error.txt", ex.ToString()); }
            };
            timer.Start();
            outsideClicks = new OutsideClickWatcher(MediaOutsideClick);
        }
        protected override bool ShowWithoutActivation
        {
            get
            {
                return true;
            }
        }
        protected override CreateParams CreateParams
        {
            get
            {
                var p = base.CreateParams;
                p.ExStyle |= 0x08000000 | 0x00080000;
                if (!Preview)
                    p.ExStyle |= 0x00000080;
                return p;
            }
        }
        public static void Show(string title, string status, bool done, bool error)
        {
            Dispatch(delegate (Island current)
            {
                current.StopMediaForNotice();
                if (current.Visible && current.notification)
                    current.notices.AddFirst(new Notice { Title = current.title, Body = current.status, Avatar = current.avatar, Presented = current.presented, App = current.notificationApp, Kind = current.compactKind, Value = current.compactValue });
                else if (current.avatar != null)
                    current.avatar.Dispose();
                current.avatar = null;
                current.presented = null;
                current.notification = false;
                current.compactKind = CompactNoticeKind.None;
                current.Cursor = Cursors.Default;
                current.UpdateText(title, status, done, error);
            });
        }
        public static void ShowTelegram(string title, string body, Bitmap avatar = null, Action presented = null)
        {
            ShowNotification(title, body, NotificationApp.Telegram, avatar, presented);
        }
        public static void ShowNotification(string title, string body, NotificationApp app, Bitmap avatar = null, Action presented = null)
        {
            if (app == NotificationApp.Codex && Program.Settings.CodexBackgroundOnly && DesktopNotifications.IsCodexForeground())
            {
                if (avatar != null)
                    avatar.Dispose();
                return;
            }
            try
            {
                Dispatch(delegate (Island current)
                {
                    var notice = new Notice { Title = title, Body = body, Avatar = avatar, Presented = presented, App = app };
                    current.EnqueueNotice(notice);
                });
            }
            catch { if (avatar != null) avatar.Dispose(); throw; }
        }
        private void StartNotification(Notice notice)
        {
            if (notice.App == NotificationApp.Codex && Program.Settings.CodexBackgroundOnly && DesktopNotifications.IsCodexForeground())
            {
                if (notice.Avatar != null)
                    notice.Avatar.Dispose();
                Hide();
                NextNoticeOrMedia();
                return;
            }
            if (avatar != null)
                avatar.Dispose();
            avatar = notice.Avatar;
            presented = notice.Presented;
            notification = true;
            notificationApp = notice.App;
            compactKind = notice.Kind;
            compactValue = notice.Value;
            if (HudKind(notice.Kind))
                BeginHud(notice);
            Cursor = compactKind == CompactNoticeKind.None && notificationApp == NotificationApp.Telegram ? Cursors.Hand : Cursors.Default;
            UpdateText(notice.Title, notice.Body, true, false);
        }
        private static void Dispatch(Action<Island> action)
        {
            lock (Sync)
            {
                if (instance == null && !starting)
                {
                    starting = true;
                    Ready.Reset();
                    var thread = new Thread(delegate ()
                    {
                        var form = new Island();
                        lock (Sync)
                        {
                            instance = form;
                        }
                        form.Shown += delegate
                        {
                            Ready.Set();
                        };
                        Application.Run(form);
                    });
                    thread.IsBackground = true;
                    thread.SetApartmentState(ApartmentState.STA);
                    thread.Start();
                }
            }
            if (!Ready.WaitOne(3000))
                throw new TimeoutException("Остров не готов к показу уведомления.");
            Island current;
            lock (Sync)
                current = instance;
            if (current != null && !current.IsDisposed)
                current.BeginInvoke((Action)delegate
                {
                    action(current);
                });
            else
                throw new InvalidOperationException("Окно острова недоступно.");
        }
        private void UpdateText(string newTitle, string newStatus, bool isDone, bool isError)
        {
            title = newTitle;
            status = newStatus;
            done = isDone;
            error = isError;
            changed = PreciseClock.UtcNow;
            if (!Visible || appeared == default(DateTime))
            {
                progress = 0;
                appeared = PreciseClock.UtcNow;
            }
            dirty = true;
            if (!Visible)
                base.Show();
            Animate(null, EventArgs.Empty);
        }
        private void Animate(object sender, EventArgs e)
        {
            if (!Visible)
                return;
            if (mediaMode)
            {
                AnimateMedia();
                return;
            }
            var now = PreciseClock.UtcNow;
            double elapsed = (now - changed).TotalMilliseconds;
            if (done && elapsed >= HoldMs + OpeningMs + MotionExposureMs)
            {
                Hide();
                progress = 0;
                dirty = true;
                if (avatar != null)
                {
                    avatar.Dispose();
                    avatar = null;
                }
                presented = null;
                status = "";
                NextNoticeOrMedia();
                return;
            }
            if (IsControlHud && ((now - hudValueAt).TotalMilliseconds < 160 || (now - hudShapeAt).TotalMilliseconds < 230))
                dirty = true;
            var current = MotionAt(now);
            var trail = MotionAt(now.AddMilliseconds(-MotionExposureMs));
            if (!dirty && !AppDesign.Transitioning && current == previousFrame && trail == previousTrail)
                return;
            previousFrame = current;
            previousTrail = trail;
            dirty = false;
            progress = ExpansionAt(now);
            var samples = new RectangleF[current == trail ? 1 : MotionSamples];
            for (int i = 0; i < samples.Length; i++)
                samples[i] = MotionAt(now.AddMilliseconds(-MotionExposureMs * i / Math.Max(1, samples.Length - 1)));
            PresentFrame(LiveFace(() => DrawFaceAt(now)), samples);
            if (notification && progress >= 0.999 && presented != null)
            {
                var callback = presented;
                presented = null;
                callback();
            }
        }
        // Live frames reuse one face bitmap and one layered surface; exports and
        // tests still receive bitmaps they own and dispose.
        IslandCanvas canvas;
        Bitmap liveFace;
        bool drawingLiveFace;
        Bitmap LiveFace(Func<Bitmap> draw)
        {
            drawingLiveFace = true;
            try { return draw(); }
            finally { drawingLiveFace = false; }
        }
        Bitmap NewFace(int width, int height)
        {
            if (!drawingLiveFace)
                return new Bitmap(width, height, PixelFormat.Format32bppPArgb);
            if (liveFace == null || liveFace.Width != width || liveFace.Height != height)
            {
                if (liveFace != null)
                    liveFace.Dispose();
                liveFace = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
            }
            return liveFace;
        }
        void PresentFrame(Bitmap face, RectangleF[] samples)
        {
            if (canvas == null)
                canvas = new IslandCanvas();
            canvas.Compose(face, samples);
            var area = Program.TargetScreen.Bounds;
            canvas.Present(Handle, area.Left + (area.Width - IslandSurface.CanvasWidth) / 2, area.Top + (Preview ? 16 : IslandSurface.OriginY));
        }
        private RectangleF MotionAt(DateTime time)
        {
            double elapsed = (time - changed).TotalMilliseconds;
            if (compactKind != CompactNoticeKind.None)
            {
                double phase = OpeningPhaseAt(time);
                double pulse = done && elapsed >= HoldMs ? 0 : SettleAt((time - appeared).TotalMilliseconds - OpeningMs);
                var size = IsControlHud ? HudSizeAt(time) : new SizeF(CompactNoticeWidth, 40);
                return RevealMotion(size.Width, size.Height, phase, pulse, 0);
            }
            double opening = OpeningPhaseAt(time);
            double slide = 1 - Ease(opening / SlideMs), amount = ExpansionAt(time);
            // A single soft squeeze after opening: 5% taller, 2% narrower, then settle.
            double settle = done && elapsed >= HoldMs ? 0 : SettleAt((time - appeared).TotalMilliseconds - OpeningMs);
            float width = (float)((CompactWidth + (ExpandedWidth - CompactWidth) * amount) * (1 - 0.02 * settle));
            float height = (float)((CompactHeight + (ExpandedHeight - CompactHeight) * amount) * (1 + 0.05 * settle));
            // The window emerges from, and retreats behind, the screen's top edge.
            float top = (float)(TopGap - slide * (height + TopGap) - IslandSurface.OriginY);
            return new RectangleF((IslandSurface.CanvasWidth - width) / 2, top, width, height);
        }
        private static RectangleF RevealMotion(double width, double height, double phase, double pulse, double initialSlide)
        {
            double reveal = Ease((phase - ExpandStartMs) / ExpandMs);
            double slide = initialSlide + (1 - initialSlide) * (1 - Ease(phase / SlideMs));
            float w = (float)((64 + (width - 64) * reveal) * (1 - 0.02 * pulse));
            float h = (float)((24 + (height - 24) * reveal) * (1 + 0.05 * pulse));
            return new RectangleF((IslandSurface.CanvasWidth - w) / 2, (float)(TopGap - slide * (h + TopGap) - IslandSurface.OriginY), w, h);
        }
        private double OpeningPhaseAt(DateTime time)
        {
            double elapsed = (time - changed).TotalMilliseconds;
            // Reverse the same path on dismissal, including the overlap of shrinking and sliding.
            return done && elapsed >= HoldMs ? OpeningMs - (elapsed - HoldMs) : (time - appeared).TotalMilliseconds;
        }
        private double ExpansionAt(DateTime time)
        {
            return Ease((OpeningPhaseAt(time) - ExpandStartMs) / ExpandMs);
        }
        private static double SettleAt(double elapsed)
        {
            if (!Program.Settings.SettleAnimation)
                return 0;
            double t = elapsed / SettleMs;
            if (t <= 0 || t >= 1)
                return 0;
            return t < SettlePeak ? Ease(t / SettlePeak) : 1 - Ease((t - SettlePeak) / (1 - SettlePeak));
        }
        private Bitmap DrawFaceAt(DateTime time)
        {
            if (compactKind != CompactNoticeKind.None)
            {
                var size = IsControlHud ? HudSizeAt(time) : new SizeF(CompactNoticeWidth, 40);
                return DrawFace((int)Math.Round(size.Width), (int)Math.Round(size.Height), 1);
            }
            double amount = ExpansionAt(time);
            // Keep the text layout steady; the compositor squeezes the whole face together.
            return DrawFace((int)Math.Round(CompactWidth + (ExpandedWidth - CompactWidth) * amount),
                (int)Math.Round(CompactHeight + (ExpandedHeight - CompactHeight) * amount), amount);
        }
        private static double Ease(double t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return t * t * (3 - 2 * t);
        }
        private static GraphicsPath RoundRect(Rectangle r, int diameter)
        {
            var p = new GraphicsPath();
            diameter = Math.Min(diameter, Math.Min(r.Width, r.Height));
            p.AddArc(r.X, r.Y, diameter, diameter, 180, 90);
            p.AddArc(r.Right - diameter, r.Y, diameter, diameter, 270, 90);
            p.AddArc(r.Right - diameter, r.Bottom - diameter, diameter, diameter, 0, 90);
            p.AddArc(r.X, r.Bottom - diameter, diameter, diameter, 90, 90);
            p.CloseFigure();
            return p;
        }
        private Bitmap DrawFace(int width, int height, double amount)
        {
            var face = NewFace(width, height);
            using (var g = Graphics.FromImage(face))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                var bounds = new Rectangle(0, 0, width - 1, height - 1);
                using (var path = RoundRect(bounds, 64))
                using (var fill = new SolidBrush(AppDesign.IslandBackground))
                {
                    g.FillPath(fill, path);
                }
                if (AppDesign.Material)
                {
                    DrawMaterialNotice(g, width, height);
                    return face;
                }
                if (compactKind != CompactNoticeKind.None)
                {
                    if (IsControlHud)
                        DrawControlHud(g, width, height);
                    else
                        DrawCompactNotice(g, width, height);
                    return face;
                }
                int iconSize = (int)(26 + (notification ? 22 : 12) * amount);
                int iconX = width < 170 ? (width - iconSize) / 2 : 20;
                var iconRect = new Rectangle(iconX, (height - iconSize) / 2, iconSize, iconSize);
                if (notification)
                {
                    using (var circle = new GraphicsPath())
                    {
                        circle.AddEllipse(iconRect);
                        var old = g.Save();
                        g.SetClip(circle);
                        if (avatar != null)
                            g.DrawImage(avatar, iconRect);
                        else
                        {
                            using (var fill = new SolidBrush(Color.FromArgb(48, 147, 191)))
                                g.FillEllipse(fill, iconRect);
                            using (var font = new Font("Segoe UI", Math.Max(9, iconSize * 0.32f), FontStyle.Bold))
                            using (var brush = new SolidBrush(Color.White))
                            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                                g.DrawString(string.IsNullOrWhiteSpace(title) ? "T" : System.Globalization.StringInfo.GetNextTextElement(title.Trim()), font, brush, iconRect, format);
                        }
                        g.Restore(old);
                    }
                }
                else if (logo != null)
                    g.DrawIcon(logo, iconRect);
                if (width >= 170)
                {
                    if (notification)
                    {
                        using (var nameFont = new Font("Segoe UI", 11, FontStyle.Bold))
                        using (var bodyFont = new Font("Segoe UI", 10, FontStyle.Regular))
                        using (var white = new SolidBrush(Color.White))
                        using (var grey = new SolidBrush(Color.FromArgb(200, 203, 210)))
                        using (var oneLine = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                        using (var lines = new StringFormat { Trimming = StringTrimming.EllipsisWord, FormatFlags = StringFormatFlags.LineLimit })
                        {
                            g.DrawString(title, nameFont, white, new RectangleF(84, 19, Math.Max(0, width - 104), 25), oneLine);
                            g.DrawString(status, bodyFont, grey, new RectangleF(84, 48, Math.Max(0, width - 104), 57), lines);
                        }
                    }
                    else
                    {
                        using (var titleFont = new Font("Segoe UI", 10, FontStyle.Bold))
                        using (var statusFont = new Font("Segoe UI", 9, FontStyle.Regular))
                        using (var white = new SolidBrush(Color.White))
                        using (var secondary = new SolidBrush(error ? Color.FromArgb(255, 115, 115) : Color.FromArgb(189, 192, 198)))
                        using (var indicator = new SolidBrush(error ? Color.FromArgb(255, 91, 91) : done && status == L.T("Connected") ? Color.FromArgb(85, 223, 150) : Color.FromArgb(245, 206, 67)))
                        {
                            g.DrawString(title, titleFont, white, 72, 21);
                            g.FillEllipse(indicator, 74, 51, 7, 7);
                            g.DrawString(status, statusFont, secondary, 86, 44);
                        }
                    }
                }
            }
            return face;
        }
        internal static void ExportMotionPreview(string folder)
        {
            System.IO.Directory.CreateDirectory(folder);
            using (var island = new Island())
            {
                island.timer.Stop();
                island.done = true;
                island.status = "Включён";
                var basis = PreciseClock.UtcNow;
                island.changed = basis;
                island.appeared = basis;
                double[] times = { ExpandStartMs, 170, 220, OpeningMs, OpeningMs + SettleMs * SettlePeak, OpeningMs + SettleMs, 2000, 3140, 3000 + OpeningMs - 170, 3380 };
                string[] names = { "emerged-70-percent", "emerge-expand", "slide-complete", "fully-open", "settle-peak", "settle-complete", "idle", "contract", "contract-depart", "depart" };
                var watch = System.Diagnostics.Stopwatch.StartNew();
                for (int k = 0; k < times.Length; k++)
                {
                    var now = basis.AddMilliseconds(times[k]);
                    var current = island.MotionAt(now);
                    var trail = island.MotionAt(now.AddMilliseconds(-MotionExposureMs));
                    var samples = new RectangleF[current == trail ? 1 : MotionSamples];
                    for (int i = 0; i < samples.Length; i++)
                        samples[i] = island.MotionAt(now.AddMilliseconds(-MotionExposureMs * i / Math.Max(1, samples.Length - 1)));
                    using (var face = island.DrawFaceAt(now))
                    using (var bitmap = IslandSurface.Render(face, samples))
                    {
                        bitmap.Save(System.IO.Path.Combine(folder, names[k] + ".png"), ImageFormat.Png);
                    }
                }
                string report = "Rendered " + times.Length + " animation phases in " + watch.ElapsedMilliseconds + " ms; VPN untouched. Expansion starts at " + ExpandStartMs.ToString("F1") + " ms (70% visible); closing reverses the same path; settle " + SettleMs + " ms (+5% height, -2% width); motion exposure " + MotionExposureMs + " ms.";
                var motionTime = basis.AddMilliseconds(3140);
                var motionSamples = new RectangleF[MotionSamples];
                for (int i = 0; i < motionSamples.Length; i++)
                    motionSamples[i] = island.MotionAt(motionTime.AddMilliseconds(-MotionExposureMs * i / (motionSamples.Length - 1)));
                using (var face = island.DrawFaceAt(motionTime))
                {
                    watch.Restart();
                    for (int i = 0; i < 12; i++)
                        using (var frame = IslandSurface.Render(face, motionSamples))
                        {
                        }
                    report += " Average motion frame: " + (watch.Elapsed.TotalMilliseconds / 12).ToString("F1") + " ms.";
                }
                System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "render.txt"), report);
            }
        }
        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(value);
            if (timer != null)
            {
                if (value)
                    timer.Start();
                else
                    timer.Stop();
            }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
        }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (mediaMode)
            {
                MediaMouseDown(e);
                return;
            }
            if (notification && compactKind == CompactNoticeKind.None && notificationApp == NotificationApp.Telegram && e.Button == MouseButtons.Left)
            {
                try
                {
                    Process.Start(new ProcessStartInfo("tg://") { UseShellExecute = true });
                }
                catch { }
                changed = PreciseClock.UtcNow.AddMilliseconds(-HoldMs);
            }
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (mediaMode)
                MediaMouseMove(e);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (mediaMode)
                MediaMouseUp(e);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                timer.Dispose();
                if (focusField != null) focusField.Dispose();
                if (canvas != null) canvas.Dispose();
                if (liveFace != null) liveFace.Dispose();
                outsideClicks.Dispose();
                if (logo != null)
                    logo.Dispose();
                if (avatar != null)
                    avatar.Dispose();
                if (mediaData != null)
                    mediaData.Dispose();
                foreach (var notice in notices)
                    if (notice.Avatar != null)
                        notice.Avatar.Dispose();
            }
            base.Dispose(disposing);
        }
    }

}
