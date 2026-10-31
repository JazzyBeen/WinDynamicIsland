using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using WinDynamicIsland.Core;
namespace WinDynamicIsland
{
    internal sealed partial class Island
    {
        int CompactNoticeWidth
        {
            get
            {
                return compactKind == CompactNoticeKind.Charging ? 370 : 260;
            }
        }
        void DrawChargingNotice(Graphics g, int width, int height)
        {
            bool plugged = title != L.T("ChargingOff");
            var green = Color.FromArgb(86, 255, 158);
            using (var font = new Font("Segoe UI", 10, FontStyle.Regular))
                TextLine(g, L.T(plugged ? "ChargingShort" : "OnBatteryShort"), font, Brushes.White, new RectangleF(15, 10, width - 125, 25));
            using (var font = new Font("Segoe UI", 10, FontStyle.Bold))
            using (var brush = new SolidBrush(plugged ? green : Color.White))
            {
                using (var format = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
                    g.DrawString(compactValue < 0 ? "—" : Math.Max(0, Math.Min(100, compactValue)) + "%", font, brush, new RectangleF(width - 111, 8, 58, 25), format);
                var battery = new Rectangle(width - 45, 10, 32, 20);
                using (var path = RoundRect(battery, 12))
                using (var outline = new SolidBrush(Color.FromArgb(45, 75, 53)))
                    g.FillPath(outline, path);
                int fillWidth = (int)Math.Round(32 * Math.Max(0, Math.Min(100, compactValue)) / 100d);
                if (fillWidth > 0)
                {
                    var state = g.Save();
                    using (var clip = RoundRect(battery, 12))
                        g.SetClip(clip);
                    g.FillRectangle(brush, battery.X, battery.Y, fillWidth, battery.Height);
                    g.Restore(state);
                }
                using (var cap = new Pen(Color.FromArgb(120, 150, 130), 2))
                {
                    cap.StartCap = cap.EndCap = LineCap.Round;
                    g.DrawLine(cap, width - 10, 17, width - 10, 23);
                }
            }
        }
    }
}
