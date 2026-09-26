using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 使用 WPF 字体轮廓生成可撤销墨迹的宿主实现。
    /// </summary>
    internal sealed class InkTextService : IInkTextService
    {
        private readonly MainWindow _mainWindow;

        public InkTextService(MainWindow mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        }

        private T RunOnUi<T>(Func<T> func)
            => _mainWindow.Dispatcher.CheckAccess() ? func() : _mainWindow.Dispatcher.Invoke(func);

        public StrokeCollection CreateTextStrokes(string text, PluginInkTextOptions options = null)
            => RunOnUi(() => RenderText(text, options ?? new PluginInkTextOptions()));

        public bool TryInsertTextAsInk(string text, Point? center = null,
            PluginInkTextOptions options = null)
        {
            return RunOnUi(() =>
            {
                var strokes = RenderText(text, options ?? new PluginInkTextOptions());
                if (strokes.Count == 0) return false;

                var target = center;
                if (!target.HasValue)
                {
                    var size = _mainWindow.GetPluginCanvasSize();
                    if (size.Width <= 0 || size.Height <= 0) return false;
                    target = new Point(size.Width / 2, size.Height / 2);
                }

                return _mainWindow.TryAddPluginStrokes(strokes, target);
            });
        }

        private static StrokeCollection RenderText(string text, PluginInkTextOptions options)
        {
            var result = new StrokeCollection();
            if (string.IsNullOrWhiteSpace(text)) return result;

            var fontSize = Clamp(options.FontSize, 4, 256);
            var strokeWidth = Clamp(options.StrokeWidth, 0.25, 32);
            var familyName = string.IsNullOrWhiteSpace(options.FontFamily)
                ? "Microsoft YaHei UI" : options.FontFamily;

            FormattedText formatted;
            try
            {
                var typeface = new Typeface(new FontFamily(familyName),
                    FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
                formatted = new FormattedText(
                    text,
                    CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    fontSize,
                    Brushes.Black,
                    1.0);
            }
            catch
            {
                formatted = new FormattedText(
                    text,
                    CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    fontSize,
                    Brushes.Black,
                    1.0);
            }

            if (options.MaxWidth > 0)
                formatted.MaxTextWidth = Math.Max(fontSize, options.MaxWidth);
            if (options.LineHeight > 0)
                formatted.LineHeight = Math.Max(fontSize, options.LineHeight);

            Geometry geometry;
            try
            {
                // BuildGeometry 使用基线坐标，向下平移字号使输出包围盒从接近原点开始。
                geometry = formatted.BuildGeometry(new Point(0, fontSize));
                geometry = geometry.GetFlattenedPathGeometry(0.6,
                    ToleranceType.Absolute);
            }
            catch
            {
                return result;
            }

            var attributes = new DrawingAttributes
            {
                Color = options.Color,
                Width = strokeWidth,
                Height = strokeWidth,
                FitToCurve = options.FitToCurve
            };

            if (geometry is not PathGeometry pathGeometry)
                pathGeometry = PathGeometry.CreateFromGeometry(geometry);

            foreach (var figure in pathGeometry.Figures)
            {
                var points = new StylusPointCollection
                {
                    new StylusPoint(figure.StartPoint.X, figure.StartPoint.Y)
                };

                foreach (var segment in figure.Segments)
                {
                    if (segment is PolyLineSegment polyLine)
                    {
                        foreach (var point in polyLine.Points)
                            points.Add(new StylusPoint(point.X, point.Y));
                    }
                    else if (segment is LineSegment line)
                    {
                        points.Add(new StylusPoint(line.Point.X, line.Point.Y));
                    }
                }

                if (figure.IsClosed && points.Count > 1)
                {
                    var first = points[0];
                    var last = points[points.Count - 1];
                    if (Math.Abs(first.X - last.X) > 0.01 || Math.Abs(first.Y - last.Y) > 0.01)
                        points.Add(new StylusPoint(first.X, first.Y));
                }

                if (points.Count >= 2)
                    result.Add(new Stroke(points, attributes.Clone()));
            }

            return result;
        }

        private static double Clamp(double value, double min, double max)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return min;
            return Math.Max(min, Math.Min(max, value));
        }
    }
}
