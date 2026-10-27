using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;

namespace Ink_Canvas.Helpers
{
    /// <summary>Legacy XML format, shared by background save and import paths.</summary>
    internal static class LegacyInkXmlCodec
    {
        internal static string Serialize(StrokeCollection strokes, DateTime savedAt)
            => new XDocument(new XElement("InkCanvasStrokes",
                new XAttribute("Version", "1.0"), new XAttribute("StrokeCount", strokes.Count),
                new XAttribute("SaveTime", savedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                strokes.Select(stroke => new XElement("Stroke",
                    new XAttribute("DrawingAttributes", SerializeAttributes(stroke.DrawingAttributes)),
                    new XElement("StylusPoints", stroke.StylusPoints.Select(point => new XElement("StylusPoint",
                        new XAttribute("X", point.X), new XAttribute("Y", point.Y),
                        new XAttribute("PressureFactor", point.PressureFactor)))))))).ToString();

        internal static string SerializeAttributes(DrawingAttributes attributes)
            => FormattableString.Invariant($"Color={attributes.Color};Width={attributes.Width};Height={attributes.Height};FitToCurve={attributes.FitToCurve};IsHighlighter={attributes.IsHighlighter};IgnorePressure={attributes.IgnorePressure};StylusTip={attributes.StylusTip};");

        internal static StrokeCollection Load(string path)
        {
            using var stream = File.OpenRead(path);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
            var root = XDocument.Load(reader).Root;
            if (root?.Name != "InkCanvasStrokes") throw new InvalidDataException("Invalid legacy ink XML root.");
            var strokes = new StrokeCollection();
            foreach (var element in root.Elements("Stroke"))
            {
                var points = new StylusPointCollection();
                foreach (var point in element.Element("StylusPoints")?.Elements("StylusPoint") ?? Enumerable.Empty<XElement>())
                    points.Add(new StylusPoint(XmlConvert.ToDouble((string)point.Attribute("X") ?? "0"),
                        XmlConvert.ToDouble((string)point.Attribute("Y") ?? "0"),
                        XmlConvert.ToSingle((string)point.Attribute("PressureFactor") ?? "0.5")));
                if (points.Count > 0)
                    strokes.Add(new Stroke(points) { DrawingAttributes = ParseAttributes((string)element.Attribute("DrawingAttributes") ?? "") });
            }
            return strokes;
        }

        private static DrawingAttributes ParseAttributes(string text)
        {
            var attributes = new DrawingAttributes();
            foreach (var part in text.Split(';'))
            {
                var pair = part.Split('=', 2);
                if (pair.Length != 2) continue;
                string value = pair[1].Trim();
                switch (pair[0].Trim())
                {
                    case "Color": attributes.Color = (Color)ColorConverter.ConvertFromString(value); break;
                    case "Width": attributes.Width = ParseLegacySize(value); break;
                    case "Height": attributes.Height = ParseLegacySize(value); break;
                    case "FitToCurve": attributes.FitToCurve = bool.Parse(value); break;
                    case "IsHighlighter": attributes.IsHighlighter = bool.Parse(value); break;
                    case "IgnorePressure": attributes.IgnorePressure = bool.Parse(value); break;
                    case "StylusTip": attributes.StylusTip = Enum.TryParse<StylusTip>(value, out var tip) ? tip : StylusTip.Ellipse; break;
                }
            }
            return attributes;
        }

        private static double ParseLegacySize(string text)
        {
            // Older writers used the machine's decimal separator for this string.
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double size)) return size;
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out size)) return size;
            return double.Parse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture);
        }
    }
}
