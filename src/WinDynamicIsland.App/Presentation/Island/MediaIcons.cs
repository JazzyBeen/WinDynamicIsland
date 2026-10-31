using WinDynamicIsland.Core;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Xml;
using System.Windows.Media;
using Color = System.Drawing.Color;

namespace WinDynamicIsland
{
    internal static class MediaIcons
    {
        static readonly GraphicsPath back = Load("RewindSvg"), forward = Load("ForwardSvg");
        // Bootstrap Icons, MIT. Original SVGs and license are in assets/ and embedded.
        static GraphicsPath Load(string name)
        {
            var path = new GraphicsPath();
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            using (var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
            {
                var document = new XmlDocument { XmlResolver = null };
                document.Load(reader);
                foreach (XmlElement element in document.GetElementsByTagName("path"))
                {
                    var shape = PathGeometry.CreateFromGeometry(Geometry.Parse(element.GetAttribute("d"))).GetFlattenedPathGeometry(0.01, ToleranceType.Absolute);
                    foreach (var figure in shape.Figures)
                    {
                        path.StartFigure();
                        var previous = figure.StartPoint;
                        foreach (var segment in figure.Segments)
                        {
                            var line = segment as LineSegment;
                            var poly = segment as PolyLineSegment;
                            if (line != null)
                            {
                                path.AddLine((float)previous.X, (float)previous.Y, (float)line.Point.X, (float)line.Point.Y);
                                previous = line.Point;
                            }
                            else if (poly != null)
                                foreach (var point in poly.Points)
                                {
                                    path.AddLine((float)previous.X, (float)previous.Y, (float)point.X, (float)point.Y);
                                    previous = point;
                                }
                        }
                        if (figure.IsClosed)
                            path.CloseFigure();
                    }
                }
            }
            return path;
        }
        internal static void Draw(Graphics graphics, int x, int y, bool next, bool enabled, int alpha = 255)
        {
            var state = graphics.Save();
            graphics.TranslateTransform(x - 16, y - 16);
            graphics.ScaleTransform(2, 2);
            using (var brush = new SolidBrush(Color.FromArgb(alpha, enabled ? Color.White : Color.FromArgb(75, 75, 80))))
                graphics.FillPath(brush, next ? forward : back);
            graphics.Restore(state);
        }
    }

}
