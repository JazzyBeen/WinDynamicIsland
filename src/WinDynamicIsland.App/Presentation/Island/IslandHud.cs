using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using WinDynamicIsland.Core;
namespace WinDynamicIsland
{
    internal sealed partial class Island
    {
        bool hudWide, hudActive;
        double hudFromWidth = 280, hudFromHeight = 44, hudFromValue;
        DateTime hudShapeAt, hudValueAt;
        static bool HudKind(CompactNoticeKind kind)
        {
            return kind == CompactNoticeKind.Volume || kind == CompactNoticeKind.Microphone || kind == CompactNoticeKind.CapsLock || kind == CompactNoticeKind.KeyboardLayout;
        }
        bool IsControlHud
        {
            get
            {
                return HudKind(compactKind);
            }
        }
        SizeF HudSizeAt(DateTime now)
        {
            double width = compactKind == CompactNoticeKind.Volume ? (hudWide ? 380 : 280) : 260;
            double height = compactKind == CompactNoticeKind.Volume && hudWide ? 28 : 44;
            double amount = Ease((now - hudShapeAt).TotalMilliseconds / 210);
            return new SizeF((float)(hudFromWidth + (width - hudFromWidth) * amount), (float)(hudFromHeight + (height - hudFromHeight) * amount));
        }
        double HudValueAt(DateTime now)
        {
            return hudFromValue + (compactValue - hudFromValue) * Ease((now - hudValueAt).TotalMilliseconds / 130);
        }
        public static void ShowHud(CompactNoticeKind kind, string text, int value, bool active, bool wide)
        {
            Dispatch(current =>
            {
                var notice = new Notice { Kind = kind, Title = text, Body = "", App = NotificationApp.System, Value = value, Active = active, Wide = wide };
                if (current.Visible && current.IsControlHud)
                {
                    current.UpdateHud(notice);
                    return;
                }
                if (current.Visible)
                {
                    var now = DateTime.UtcNow;
                    var old = current.mediaMode ? current.MediaMotionAt(now) : current.MotionAt(now);
                    if (current.notification)
                    {
                        current.notices.AddFirst(new Notice { Kind = current.compactKind, Value = current.compactValue, Title = current.title, Body = current.status, App = current.notificationApp, Avatar = current.avatar, Presented = current.presented });
                        current.avatar = null;
                        current.presented = null;
                    }
                    current.StopMediaForNotice();
                    current.StartNotification(notice);
                    current.appeared = now.AddMilliseconds(-OpeningMs);
                    current.hudFromWidth = old.Width;
                    current.hudFromHeight = old.Height;
                    current.hudShapeAt = now;
                    current.dirty = true;
                }
                else
                    current.StartNotification(notice);
            });
        }
        void UpdateHud(Notice notice)
        {
            var now = DateTime.UtcNow;
            var size = HudSizeAt(now);
            if (compactKind != notice.Kind || hudWide != notice.Wide)
            {
                hudFromWidth = size.Width;
                hudFromHeight = size.Height;
                hudShapeAt = now;
            }
            hudFromValue = HudValueAt(now);
            hudValueAt = now;
            compactKind = notice.Kind;
            compactValue = notice.Value;
            hudActive = notice.Active;
            hudWide = notice.Wide;
            title = notice.Title;
            changed = now;
            done = true;
            dirty = true;
        }
        internal static void ExportHudPreview(string folder)
        {
            Directory.CreateDirectory(folder);
            using (var island = new Island())
            {
                island.timer.Stop();
                var examples = new[] {
                    new Notice { Kind=CompactNoticeKind.Volume, Title=L.T("Volume"), Value=62 },
                    new Notice { Kind=CompactNoticeKind.Volume, Title=L.T("Volume"), Value=78, Wide=true },
                    new Notice { Kind=CompactNoticeKind.Microphone, Title=L.T("MicOff"), Active=true },
                    new Notice { Kind=CompactNoticeKind.Microphone, Title=L.T("MicOn") }
                };
                for (int i = 0; i < examples.Length; i++)
                {
                    var example = examples[i];
                    island.compactKind = example.Kind;
                    island.compactValue = example.Value;
                    island.title = example.Title;
                    island.BeginHud(example);
                    island.hudShapeAt = DateTime.UtcNow.AddSeconds(-1);
                    var size = island.HudSizeAt(DateTime.UtcNow.AddSeconds(1));
                    using (var frame = island.DrawFace((int)size.Width, (int)size.Height, 1))
                        frame.Save(Path.Combine(folder, "hud-" + i + ".png"));
                }
            }
        }
        void BeginHud(Notice notice)
        {
            hudWide = notice.Wide;
            hudActive = notice.Active;
            hudFromWidth = notice.Kind == CompactNoticeKind.Volume ? (notice.Wide ? 380 : 280) : 260;
            hudFromHeight = notice.Wide && notice.Kind == CompactNoticeKind.Volume ? 28 : 44;
            hudShapeAt = DateTime.UtcNow;
            hudFromValue = notice.Value;
            hudValueAt = DateTime.UtcNow;
        }
        void DrawControlHud(Graphics g, int width, int height)
        {
            var color = hudActive && compactKind == CompactNoticeKind.Microphone ? Color.FromArgb(255, 98, 115) : Color.White;
            float center = height / 2f;
            if (compactKind == CompactNoticeKind.Volume)
            {
                double wideAmount = hudWide ? Ease((DateTime.UtcNow - hudShapeAt).TotalMilliseconds / 170) : 0;
                var foreground = Color.FromArgb((int)Math.Round(255 * (1 - wideAmount)), Color.White);
                using (var brush = new SolidBrush(foreground))
                {
                    g.FillPolygon(brush, new[] { new PointF(17, center - 4), new PointF(22, center - 4), new PointF(29, center - 10), new PointF(29, center + 10), new PointF(22, center + 4), new PointF(17, center + 4) });
                    using (var pen = new Pen(foreground, 1.8f))
                    {
                        if (hudActive)
                        {
                            g.DrawLine(pen, 34, center - 4, 41, center + 4);
                            g.DrawLine(pen, 41, center - 4, 34, center + 4);
                        }
                        else
                            g.DrawArc(pen, 25, center - 7, 15, 14, -55, 110);
                    }
                    float x = (float)(54 - 36 * wideAmount), trackWidth = width - x - (float)(50 - 32 * wideAmount), y = center - 2.5f;
                    using (var path = RoundRect(new Rectangle((int)x, (int)y, (int)trackWidth, 5), 5))
                    using (var track = new SolidBrush(Color.FromArgb(55, 55, 59)))
                        g.FillPath(track, path);
                    float amount = (float)Math.Max(0, Math.Min(1, HudValueAt(DateTime.UtcNow) / 100));
                    if (amount > 0)
                        using (var path = RoundRect(new Rectangle((int)x, (int)y, Math.Max(5, (int)(trackWidth * amount)), 5), 5))
                            g.FillPath(Brushes.White, path);
                    using (var font = new Font("Segoe UI", hudWide ? 8 : 9, FontStyle.Bold))
                        TextLine(g, hudActive ? "—" : compactValue + "%", font, brush, new RectangleF(width - 44, center - 9, 40, 20));
                }
                return;
            }
            using (var pen = new Pen(color, 2))
            {
                pen.StartCap = pen.EndCap = LineCap.Round;
                if (compactKind == CompactNoticeKind.Microphone)
                {
                    using (var path = RoundRect(new Rectangle(22, (int)center - 11, 8, 16), 8))
                        g.DrawPath(pen, path);
                    g.DrawArc(pen, 18, center - 8, 16, 17, 0, 180);
                    g.DrawLine(pen, 26, center + 9, 26, center + 13);
                    g.DrawLine(pen, 21, center + 13, 31, center + 13);
                    if (hudActive)
                        g.DrawLine(pen, 17, center - 12, 36, center + 13);
                }
                else if (compactKind == CompactNoticeKind.CapsLock)
                {
                    g.DrawLines(pen, new[] { new PointF(18, center), new PointF(26, center - 9), new PointF(34, center), new PointF(30, center), new PointF(30, center + 5), new PointF(22, center + 5), new PointF(22, center), new PointF(18, center) });
                    g.DrawLine(pen, 22, center + 10, 30, center + 10);
                }
                else
                {
                    g.DrawRectangle(pen, 17, center - 8, 24, 16);
                    g.DrawLine(pen, 22, center + 3, 35, center + 3);
                }
            }
            using (var font = new Font("Segoe UI", 9, FontStyle.Bold))
            using (var brush = new SolidBrush(color))
                TextLine(g, title, font, brush, new RectangleF(52, center - 10, width - 66, 24));
        }
    }
}
