using OSVersionExtension;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using SysPoint = System.Windows.Point;
using WinRtInkAnalyzer = global::Windows.UI.Input.Inking.Analysis.InkAnalyzer;

namespace Ink_Canvas.Helpers
{
    /// <summary>基于 Windows.UI.Input.Inking.Analysis 的形状识别（适用于 64 位进程等场景）。</summary>
    internal static class WinRtInkShapeRecognizer
    {
        public static bool IsApiAvailable =>
            OSVersion.GetOperatingSystem() >= OSVersionExtension.OperatingSystem.Windows10;

        public static void Warmup()
        {
            if (!IsApiAvailable) return;
            try
            {
                var d = Application.Current?.Dispatcher;
                if (d == null) return;
                d.BeginInvoke(new Action(async () =>
                {
                    try
                    {
                        // 空 StrokeCollection 在 RecognizeShapeAsync 入口会直接返回，无法预热 WinRT InkAnalyzer。
                        await RecognizeShapeAsync(CreateMinimalWarmupStrokeCollection()).ConfigureAwait(true);
                    }
                    catch
                    {
                        // ignore
                    }
                }));
            }
            catch
            {
                // ignore
            }
        }

        /// <summary>由 <see cref="ModernInkProcessor"/> / <see cref="InkRecognitionManager"/> 在 UI 上 await（勿在收笔回调中同步阻塞）。</summary>
        internal static async Task<InkShapeRecognitionResult> RecognizeShapeAsync(StrokeCollection strokes)
        {
            if (!IsApiAvailable || strokes == null || strokes.Count == 0)
                return InkShapeRecognitionResult.Empty;

            try
            {
                var analyzer = new WinRtInkAnalyzer();
                var added = 0;
                foreach (Stroke s in strokes)
                {
                    var inkStroke = CreateInkStrokeFromWpf(s);
                    if (inkStroke == null)
                        continue;

                    analyzer.AddDataForStroke(inkStroke);
                    analyzer.SetStrokeDataKind(
                        inkStroke.Id,
                        global::Windows.UI.Input.Inking.Analysis.InkAnalysisStrokeKind.Drawing);
                    added++;
                }

                if (added == 0)
                    return InkShapeRecognitionResult.Empty;

                await analyzer.AnalyzeAsync().AsTask().ConfigureAwait(true);

                var drawing = FindPrimaryDrawing(analyzer);
                if (drawing == null)
                    return InkShapeRecognitionResult.Empty;

                if (drawing.DrawingKind == global::Windows.UI.Input.Inking.Analysis.InkAnalysisDrawingKind.Drawing)
                    return InkShapeRecognitionResult.Empty;

                var name = MapDrawingKindToShapeName(drawing.DrawingKind);
                if (string.IsNullOrEmpty(name) || name == "Drawing")
                    return InkShapeRecognitionResult.Empty;

                var winPts = CopyWinRtPoints(drawing);
                var hot = ToWpfPointCollection(winPts);
                var c = drawing.Center;
                var centroid = new SysPoint(c.X, c.Y);
                BoundsFromPoints(winPts, out double w, out double h);

                var toRemove = new StrokeCollection();
                foreach (Stroke s in strokes)
                    toRemove.Add(s);

                return new InkShapeRecognitionResult(name, centroid, hot, w, h, toRemove);
            }
            catch (Exception)
            {
                return InkShapeRecognitionResult.Empty;
            }
        }

        /// <summary>
        /// 极短合成笔画，供 <see cref="Warmup"/> 等场景走完整 WinRT 转换与分析管线（空集合在入口处会被直接返回）。
        /// </summary>
        internal static StrokeCollection CreateMinimalWarmupStrokeCollection()
        {
            var da = new DrawingAttributes { Color = Colors.Black, Width = 2, Height = 2 };
            var pts = new StylusPointCollection
            {
                new StylusPoint(8, 8),
                new StylusPoint(14, 10),
                new StylusPoint(20, 8),
            };
            var col = new StrokeCollection();
            col.Add(new Stroke(pts, da));
            return col;
        }

        /// <summary>供 WinRT 手写等模块复用：将 WPF <see cref="Stroke"/> 转为 WinRT <see cref="global::Windows.UI.Input.Inking.InkStroke"/>。</summary>
        internal static global::Windows.UI.Input.Inking.InkStroke CreateInkStrokeFromWpf(Stroke stroke)
        {
            if (stroke?.StylusPoints == null || stroke.StylusPoints.Count == 0)
                return null;

            var da = stroke.DrawingAttributes;
            var wda = new global::Windows.UI.Input.Inking.InkDrawingAttributes
            {
                PenTip = global::Windows.UI.Input.Inking.PenTipShape.Circle,
                Color = global::Windows.UI.Color.FromArgb(da.Color.A, da.Color.R, da.Color.G, da.Color.B),
                Size = new global::Windows.Foundation.Size((float)da.Width, (float)da.Height)
            };

            var builder = new global::Windows.UI.Input.Inking.InkStrokeBuilder();
            builder.SetDefaultDrawingAttributes(wda);

            var points = new List<global::Windows.Foundation.Point>(stroke.StylusPoints.Count);
            foreach (StylusPoint sp in stroke.StylusPoints)
            {
                var pi = sp.ToPoint();
                points.Add(new global::Windows.Foundation.Point((float)pi.X, (float)pi.Y));
            }

            if (points.Count == 0)
                return null;

            return builder.CreateStroke(points);
        }

        private static global::Windows.UI.Input.Inking.Analysis.InkAnalysisInkDrawing FindPrimaryDrawing(
            WinRtInkAnalyzer analyzer)
        {
            global::Windows.UI.Input.Inking.Analysis.InkAnalysisInkDrawing best = null;
            double bestArea = -1;
            if (analyzer?.AnalysisRoot != null)
                Visit(analyzer.AnalysisRoot);
            return best;

            void Visit(global::Windows.UI.Input.Inking.Analysis.IInkAnalysisNode node)
            {
                if (node == null) return;

                if (node is global::Windows.UI.Input.Inking.Analysis.InkAnalysisInkDrawing d &&
                    d.DrawingKind != global::Windows.UI.Input.Inking.Analysis.InkAnalysisDrawingKind.Drawing)
                {
                    double area = EstimateDrawingArea(d);
                    if (area > bestArea)
                    {
                        bestArea = area;
                        best = d;
                    }
                }

                // WinRT IInkAnalysisNode.Children 可能为 null，不可直接 foreach。
                var children = node.Children;
                if (children == null) return;

                foreach (var child in children)
                    Visit(child);
            }
        }

        private static double EstimateDrawingArea(global::Windows.UI.Input.Inking.Analysis.InkAnalysisInkDrawing drawing)
        {
            var pts = CopyWinRtPoints(drawing);
            BoundsFromPoints(pts, out double w, out double h);
            return w * h;
        }

        private static global::Windows.Foundation.Point[] CopyWinRtPoints(
            global::Windows.UI.Input.Inking.Analysis.InkAnalysisInkDrawing drawing)
        {
            var src = drawing?.Points;
            if (src == null)
                return Array.Empty<global::Windows.Foundation.Point>();

            var n = src.Count;
            if (n == 0)
                return Array.Empty<global::Windows.Foundation.Point>();

            var arr = new global::Windows.Foundation.Point[n];
            for (var i = 0; i < n; i++)
                arr[i] = src[i];
            return arr;
        }

        private static void BoundsFromPoints(
            System.Collections.Generic.IReadOnlyList<global::Windows.Foundation.Point> points,
            out double w,
            out double h)
        {
            if (points == null || points.Count == 0)
            {
                w = h = 0;
                return;
            }

            double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
            for (int i = 0; i < points.Count; i++)
            {
                var pt = points[i];
                minX = Math.Min(minX, pt.X);
                maxX = Math.Max(maxX, pt.X);
                minY = Math.Min(minY, pt.Y);
                maxY = Math.Max(maxY, pt.Y);
            }

            w = Math.Max(0, maxX - minX);
            h = Math.Max(0, maxY - minY);
        }

        private static PointCollection ToWpfPointCollection(
            System.Collections.Generic.IReadOnlyList<global::Windows.Foundation.Point> points)
        {
            var hot = new PointCollection();
            if (points == null) return hot;
            for (int i = 0; i < points.Count; i++)
            {
                var pt = points[i];
                hot.Add(new SysPoint(pt.X, pt.Y));
            }

            return hot;
        }

        private static string MapDrawingKindToShapeName(
            global::Windows.UI.Input.Inking.Analysis.InkAnalysisDrawingKind kind)
        {
            switch (kind)
            {
                case global::Windows.UI.Input.Inking.Analysis.InkAnalysisDrawingKind.Circle:
                    return "Circle";
                case global::Windows.UI.Input.Inking.Analysis.InkAnalysisDrawingKind.Ellipse:
                    return "Ellipse";
                case global::Windows.UI.Input.Inking.Analysis.InkAnalysisDrawingKind.Triangle:
                case global::Windows.UI.Input.Inking.Analysis.InkAnalysisDrawingKind.IsoscelesTriangle:
                case global::Windows.UI.Input.Inking.Analysis.InkAnalysisDrawingKind.EquilateralTriangle:
                case global::Windows.UI.Input.Inking.Analysis.InkAnalysisDrawingKind.RightTriangle:
                    return "Triangle";
                case global::Windows.UI.Input.Inking.Analysis.InkAnalysisDrawingKind.Rectangle:
                    return "Rectangle";
                case global::Windows.UI.Input.Inking.Analysis.InkAnalysisDrawingKind.Square:
                    return "Square";
                case global::Windows.UI.Input.Inking.Analysis.InkAnalysisDrawingKind.Diamond:
                    return "Diamond";
                case global::Windows.UI.Input.Inking.Analysis.InkAnalysisDrawingKind.Trapezoid:
                    return "Trapezoid";
                case global::Windows.UI.Input.Inking.Analysis.InkAnalysisDrawingKind.Parallelogram:
                    return "Parallelogram";
                case global::Windows.UI.Input.Inking.Analysis.InkAnalysisDrawingKind.Quadrilateral:
                    return "Quadrilateral";
                default:
                    return kind == global::Windows.UI.Input.Inking.Analysis.InkAnalysisDrawingKind.Drawing
                        ? "Drawing"
                        : kind.ToString();
            }
        }
    }
}
