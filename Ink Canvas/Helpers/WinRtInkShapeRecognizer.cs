using OSVersionExtension;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using SysPoint = System.Windows.Point;
using WinRtInkAnalyzer = global::Windows.UI.Input.Inking.Analysis.InkAnalyzer;

namespace Ink_Canvas.Helpers
{
    internal class ModernInkAnalyzer : IDisposable
    {
        public static readonly Guid ShapeStrokePropertyGuid = new Guid("11111111-2222-3333-4444-555555555555");

        private global::Windows.UI.Input.Inking.Analysis.InkAnalyzer _internalAnalyzer;
        private readonly Dictionary<Stroke, uint> _strokeIdMap = new Dictionary<Stroke, uint>();
        private readonly Dictionary<uint, Stroke> _reverseIdMap = new Dictionary<uint, Stroke>();
        private readonly object _syncLock = new object();
        private readonly SemaphoreSlim _analysisGate = new SemaphoreSlim(1, 1);

        public ModernInkAnalyzer()
        {
            if (!WinRtInkShapeRecognizer.IsApiAvailable)
                return;

            _internalAnalyzer = new global::Windows.UI.Input.Inking.Analysis.InkAnalyzer();
        }

        private void AddStrokeInternal(Stroke stroke)
        {
            if (stroke.ContainsPropertyData(ShapeStrokePropertyGuid))
                return;

            var inkStroke = WinRtInkShapeRecognizer.CreateInkStrokeFromWpf(stroke);
            if (inkStroke == null) return;

            _internalAnalyzer.AddDataForStroke(inkStroke);
            _internalAnalyzer.SetStrokeDataKind(
                inkStroke.Id,
                global::Windows.UI.Input.Inking.Analysis.InkAnalysisStrokeKind.Drawing);

            _strokeIdMap[stroke] = inkStroke.Id;
            _reverseIdMap[inkStroke.Id] = stroke;
        }

        private CancellationTokenSource _cts;

        public async Task<InkShapeRecognitionResult> AnalyzeAsync(StrokeCollection strokes)
        {
            if (_internalAnalyzer == null || strokes == null || strokes.Count == 0)
                return InkShapeRecognitionResult.Empty;

            var currentCts = new CancellationTokenSource();
            CancellationTokenSource previousCts;
            lock (_syncLock)
            {
                previousCts = _cts;
                _cts = currentCts;
                previousCts?.Cancel();
            }

            await _analysisGate.WaitAsync().ConfigureAwait(true);
            try
            {
                var token = currentCts.Token;
                if (token.IsCancellationRequested || _internalAnalyzer == null)
                    return InkShapeRecognitionResult.Empty;

                lock (_syncLock)
                {
                    _internalAnalyzer.ClearDataForAllStrokes();
                    _strokeIdMap.Clear();
                    _reverseIdMap.Clear();

                    foreach (var stroke in strokes)
                        AddStrokeInternal(stroke);
                }

                if (_strokeIdMap.Count == 0 || token.IsCancellationRequested)
                    return InkShapeRecognitionResult.Empty;

                var analysisResult = await _internalAnalyzer.AnalyzeAsync().AsTask(token).ConfigureAwait(true);
                if (analysisResult == null ||
                    analysisResult.Status != global::Windows.UI.Input.Inking.Analysis.InkAnalysisStatus.Updated ||
                    token.IsCancellationRequested)
                    return InkShapeRecognitionResult.Empty;

                var drawing = WinRtInkShapeRecognizer.FindPrimaryDrawing(_internalAnalyzer);
                if (drawing == null)
                    return InkShapeRecognitionResult.Empty;

                Dictionary<uint, Stroke> strokeMap;
                lock (_syncLock)
                    strokeMap = new Dictionary<uint, Stroke>(_reverseIdMap);

                return WinRtInkShapeRecognizer.CreateRecognitionResult(drawing, strokeMap);
            }
            catch (OperationCanceledException) when (currentCts.IsCancellationRequested)
            {
                return InkShapeRecognitionResult.Empty;
            }
            catch (Exception)
            {
                return InkShapeRecognitionResult.Empty;
            }
            finally
            {
                lock (_syncLock)
                {
                    if (ReferenceEquals(_cts, currentCts))
                        _cts = null;
                }

                currentCts.Dispose();
                _analysisGate.Release();
            }
        }

        public Task<StrokeCollection> AnalyzeAndCorrectAsync(
            StrokeCollection strokes,
            string handwritingFontFamilyList)
        {
            return WinRtHandwritingRecognizer.ConvertRecognizedTextToHandwritingInkAsync(
                strokes,
                handwritingFontFamilyList);
        }

        public void Dispose()
        {
            lock (_syncLock)
            {
                _cts?.Cancel();
                _cts?.Dispose();
                _cts = null;
                _internalAnalyzer = null;
            }

            _analysisGate.Dispose();
        }
    }

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
                var idToWpf = new Dictionary<uint, Stroke>();
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
                    idToWpf[inkStroke.Id] = s;
                    added++;
                }

                if (added == 0)
                    return InkShapeRecognitionResult.Empty;

                var analysisResult = await analyzer.AnalyzeAsync().AsTask().ConfigureAwait(true);
                if (analysisResult == null ||
                    analysisResult.Status != global::Windows.UI.Input.Inking.Analysis.InkAnalysisStatus.Updated)
                    return InkShapeRecognitionResult.Empty;

                var drawing = FindPrimaryDrawing(analyzer);
                return CreateRecognitionResult(drawing, idToWpf);
            }
            catch (Exception)
            {
                return InkShapeRecognitionResult.Empty;
            }
        }

        internal static InkShapeRecognitionResult CreateRecognitionResult(
            global::Windows.UI.Input.Inking.Analysis.InkAnalysisInkDrawing drawing,
            IReadOnlyDictionary<uint, Stroke> idToWpf)
        {
            if (drawing == null ||
                drawing.DrawingKind == global::Windows.UI.Input.Inking.Analysis.InkAnalysisDrawingKind.Drawing)
                return InkShapeRecognitionResult.Empty;

            var name = MapDrawingKindToShapeName(drawing.DrawingKind);
            if (string.IsNullOrEmpty(name) || name == "Drawing")
                return InkShapeRecognitionResult.Empty;

            var winPts = CopyWinRtPoints(drawing);
            if (!HasValidGeometry(drawing, winPts))
                return InkShapeRecognitionResult.Empty;

            var hot = ToWpfPointCollection(winPts);
            var c = drawing.Center;
            var centroid = new SysPoint(c.X, c.Y);
            BoundsFromPoints(winPts, out double w, out double h);

            var toRemove = new StrokeCollection();
            var strokeIds = drawing.GetStrokeIds();
            if (strokeIds == null || idToWpf == null)
                return InkShapeRecognitionResult.Empty;

            foreach (var id in strokeIds)
            {
                if (idToWpf.TryGetValue(id, out var stroke))
                    toRemove.Add(stroke);
            }

            if (toRemove.Count == 0)
                return InkShapeRecognitionResult.Empty;

            return new InkShapeRecognitionResult(name, centroid, hot, w, h, toRemove);
        }

        private static bool HasValidGeometry(
            global::Windows.UI.Input.Inking.Analysis.InkAnalysisInkDrawing drawing,
            IReadOnlyList<global::Windows.Foundation.Point> points)
        {
            if (points == null || points.Count == 0 || drawing == null)
                return false;

            var requiredPointCount = drawing.DrawingKind ==
                global::Windows.UI.Input.Inking.Analysis.InkAnalysisDrawingKind.Circle ||
                drawing.DrawingKind == global::Windows.UI.Input.Inking.Analysis.InkAnalysisDrawingKind.Ellipse
                ? 4
                : 3;

            BoundsFromPoints(points, out double width, out double height);
            return points.Count >= requiredPointCount && width > 0 && height > 0;
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

        /// <summary>供 WinRT 手写等模块复用：将 WPF <see cref="Stroke"/> 转为 WinRT <see cref="global::Windows.UI.Input.Inking.InkStroke"/>。
        /// 显式保留 InkAnalysis 手写笔触的推荐配置（FitToCurve=true、IgnorePressure=false），与官方 Convert-ink-to-text 示例一致，
        /// 不附加 PencilProperties（普通笔杆场景；铅笔纹理/软边/透明不应继承到手写识别输入）。</summary>
        internal static global::Windows.UI.Input.Inking.InkStroke CreateInkStrokeFromWpf(Stroke stroke)
        {
            if (stroke?.StylusPoints == null || stroke.StylusPoints.Count == 0)
                return null;

            var da = stroke.DrawingAttributes;
            if (da == null)
                return null;

            var wda = CreateRecognizerDrawingAttributes(da);
            if (wda == null)
                return null;

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

        /// <summary>
        /// 直接构造一份标准的 WinRT <see cref="global::Windows.UI.Input.Inking.InkDrawingAttributes"/> 而不调用
        /// <c>CreateForPencil</c>。返回的对象同时供形状与手写识别共用，必须保证：
        /// <list type="bullet">
        ///   <item><description><c>FitToCurve = true</c>：让 WinRT InkAnalysis 自己进行曲线平滑。</description></item>
        ///   <item><description><c>IgnorePressure = false</c>：明确不忽略压力，与官方示例一致。</description></item>
        ///   <item><description><c>PenTip = Circle</c>：保持与既有行为兼容，普通笔画不必切到矩形笔尖。</description></item>
        /// </list>
        /// </summary>
        internal static global::Windows.UI.Input.Inking.InkDrawingAttributes CreateRecognizerDrawingAttributes(DrawingAttributes wpfDa)
        {
            if (wpfDa == null)
                return null;

            return new global::Windows.UI.Input.Inking.InkDrawingAttributes
            {
                PenTip = global::Windows.UI.Input.Inking.PenTipShape.Circle,
                Color = global::Windows.UI.Color.FromArgb(wpfDa.Color.A, wpfDa.Color.R, wpfDa.Color.G, wpfDa.Color.B),
                Size = new global::Windows.Foundation.Size((float)wpfDa.Width, (float)wpfDa.Height),
                FitToCurve = true,
                IgnorePressure = false
            };
        }

        internal static global::Windows.UI.Input.Inking.Analysis.InkAnalysisInkDrawing FindPrimaryDrawing(
            global::Windows.UI.Input.Inking.Analysis.InkAnalyzer analyzer)
        {
            if (analyzer?.AnalysisRoot == null)
                return null;

            // 收集所有非 Drawing 的图形候选（含面积、笔画数）。
            // 仅按最大包围盒面积选主图形会把"用很多笔画凑出的大包围盒"误判为形状，
            // 因此在面积 ≥ 最大面积 60% 的候选里，优先选笔画数最少（最紧凑、最像单笔形状）的那个。
            var candidates = new List<global::Windows.UI.Input.Inking.Analysis.InkAnalysisInkDrawing>();
            var areaByCandidate = new Dictionary<global::Windows.UI.Input.Inking.Analysis.InkAnalysisInkDrawing, double>();
            Collect(analyzer.AnalysisRoot);
            if (candidates.Count == 0)
                return null;

            double maxArea = -1;
            foreach (var c in candidates)
            {
                var area = areaByCandidate[c];
                if (area > maxArea) maxArea = area;
            }

            global::Windows.UI.Input.Inking.Analysis.InkAnalysisInkDrawing best = null;
            int bestStrokeCount = int.MaxValue;
            double areaThreshold = maxArea * 0.6;
            foreach (var c in candidates)
            {
                if (areaByCandidate[c] < areaThreshold) continue;
                var ids = c.GetStrokeIds();
                int sc = ids?.Count ?? 0;
                if (sc == 0) sc = int.MaxValue; // 无笔画信息时不作为紧凑度优势
                if (sc < bestStrokeCount)
                {
                    bestStrokeCount = sc;
                    best = c;
                }
            }

            // 退化兜底：没有候选达到 60% 阈值（不应发生），回退最大面积
            if (best == null)
            {
                double bestArea = -1;
                foreach (var c in candidates)
                {
                    if (areaByCandidate[c] > bestArea)
                    {
                        bestArea = areaByCandidate[c];
                        best = c;
                    }
                }
            }

            return best;

            void Collect(global::Windows.UI.Input.Inking.Analysis.IInkAnalysisNode node)
            {
                if (node == null) return;

                if (node is global::Windows.UI.Input.Inking.Analysis.InkAnalysisInkDrawing d &&
                    d.DrawingKind != global::Windows.UI.Input.Inking.Analysis.InkAnalysisDrawingKind.Drawing)
                {
                    candidates.Add(d);
                    areaByCandidate[d] = EstimateDrawingArea(d);
                }

                // WinRT IInkAnalysisNode.Children 可能为 null，不可直接 foreach。
                var children = node.Children;
                if (children == null) return;

                foreach (var child in children)
                    Collect(child);
            }
        }

        private static double EstimateDrawingArea(global::Windows.UI.Input.Inking.Analysis.InkAnalysisInkDrawing drawing)
        {
            var pts = CopyWinRtPoints(drawing);
            BoundsFromPoints(pts, out double w, out double h);
            return w * h;
        }

        internal static global::Windows.Foundation.Point[] CopyWinRtPoints(
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

        internal static void BoundsFromPoints(
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

        internal static PointCollection ToWpfPointCollection(
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

        internal static string MapDrawingKindToShapeName(
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
