using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;

namespace Ink_Canvas.Helpers
{
    public class InkRecognizeHelper
    {
        /// <summary>IACore / IAWinFX 形状识别（典型用于 32 位进程）。</summary>
        public static ShapeRecognizeResult RecognizeShapeIACore(StrokeCollection strokes)
        {
            if (strokes == null || strokes.Count == 0)
                return default;

            var analyzer = new InkAnalyzer();
            analyzer.AddStrokes(strokes);
            analyzer.SetStrokesType(strokes, StrokeType.Drawing);

            AnalysisAlternate analysisAlternate = null;
            int strokesCount = strokes.Count;
            var sfsaf = analyzer.Analyze();
            if (sfsaf.Successful)
            {
                var alternates = analyzer.GetAlternates();
                if (alternates.Count > 0)
                {
                    while (strokesCount >= 2)
                    {
                        var alt0 = alternates[0];
                        if (alt0?.AlternateNodes == null || alt0.AlternateNodes.Count == 0)
                            break;
                        var drawNode = alt0.AlternateNodes[0] as InkDrawingNode;
                        if (drawNode == null)
                            break;
                        var shapeOk = IsContainShapeType(drawNode.GetShapeName());
                        if (alt0.Strokes.Contains(strokes.Last()) && shapeOk)
                            break;
                        analyzer.RemoveStroke(strokes[strokes.Count - strokesCount]);
                        strokesCount--;
                        sfsaf = analyzer.Analyze();
                        if (sfsaf.Successful)
                            alternates = analyzer.GetAlternates();
                        else
                            break;
                        if (alternates.Count == 0)
                            break;
                    }
                    if (alternates.Count > 0)
                    {
                        var altFinal = alternates[0];
                        if (altFinal?.AlternateNodes != null && altFinal.AlternateNodes.Count > 0)
                            analysisAlternate = altFinal;
                    }
                }
            }

            analyzer.Dispose();

            if (analysisAlternate != null && analysisAlternate.AlternateNodes != null && analysisAlternate.AlternateNodes.Count > 0)
            {
                var node = analysisAlternate.AlternateNodes[0] as InkDrawingNode;
                if (node == null)
                    return default;
                return new ShapeRecognizeResult(node.Centroid, node.HotPoints, analysisAlternate, node);
            }

            return default;
        }

        /// <summary>兼容旧调用：等价于 <see cref="RecognizeShapeIACore"/>。</summary>
        public static ShapeRecognizeResult RecognizeShape(StrokeCollection strokes) =>
            RecognizeShapeIACore(strokes);

        /// <summary>按设置选择 WinRT（<see cref="InkRecognitionManager"/>）或 IACore；WinRT 请用 <see cref="RecognizeShapeUnifiedAsync"/>。</summary>
        public static InkShapeRecognitionResult RecognizeShapeUnified(
            StrokeCollection strokes,
            ShapeRecognitionEngineMode mode)
        {
            if (strokes == null || strokes.Count == 0)
                return InkShapeRecognitionResult.Empty;

            if (ShapeRecognitionRouter.ResolveUseWinRt(mode))
                return InkShapeRecognitionResult.Empty;

            var legacy = RecognizeShapeIACore(strokes);
            return FromIACoreOrEmpty(legacy);
        }

        /// <summary>与 CE 反编译版 <c>InkRecognitionManager.RecognizeShapeAsync</c> 对齐的统一入口。</summary>
        public static Task<InkShapeRecognitionResult> RecognizeShapeUnifiedAsync(
            StrokeCollection strokes,
            ShapeRecognitionEngineMode mode)
        {
            if (strokes == null || strokes.Count == 0)
                return Task.FromResult(InkShapeRecognitionResult.Empty);

            return InkRecognitionManager.Instance.RecognizeShapeAsync(strokes, mode);
        }

        public static void WarmupShapeRecognition(ShapeRecognitionEngineMode mode)
        {
            try
            {
                _ = InkRecognitionManager.Instance;
                if (ShapeRecognitionRouter.ResolveUseWinRt(mode))
                {
                    WinRtInkShapeRecognizer.Warmup();
                    WinRtHandwritingRecognizer.Warmup();
                }
                else
                    RecognizeShapeIACore(new StrokeCollection());
            }
            catch
            {
                // 预热失败不影响启动
            }
        }

        /// <summary>WinRT 手写识别（64 位 + Windows 10+）。</summary>
        public static Task<HandwritingRecognitionResult> RecognizeHandwritingUnifiedAsync(
            StrokeCollection strokes,
            ShapeRecognitionEngineMode mode) =>
            InkRecognitionManager.Instance.RecognizeHandwritingAsync(strokes, mode);

        /// <summary>WinRT 下将识别成功的词替换为手写体字形墨迹；是否应用由设置「WinRT 识别转手写体字形」控制。</summary>
        public static Task<StrokeCollection> CorrectHandwritingStrokesUnifiedAsync(
            StrokeCollection strokes,
            ShapeRecognitionEngineMode mode) =>
            InkRecognitionManager.Instance.CorrectInkAsync(
                strokes,
                mode,
                MainWindow.Settings?.InkToShape?.EnableWinRtHandwritingStrokeBeautify ?? false,
                MainWindow.Settings?.InkToShape?.HandwritingCorrectionFontFamily);

        /// <summary>显式指定是否应用手写体字形替换（忽略开关）；字体仍从设置读取。</summary>
        public static Task<StrokeCollection> CorrectHandwritingStrokesUnifiedAsync(
            StrokeCollection strokes,
            ShapeRecognitionEngineMode mode,
            bool applyHandwritingBeautify) =>
            InkRecognitionManager.Instance.CorrectInkAsync(
                strokes,
                mode,
                applyHandwritingBeautify,
                MainWindow.Settings?.InkToShape?.HandwritingCorrectionFontFamily);

        internal static InkShapeRecognitionResult FromIACoreOrEmpty(ShapeRecognizeResult legacy)
        {
            if (legacy?.InkDrawingNode == null)
                return InkShapeRecognitionResult.Empty;

            var node = legacy.InkDrawingNode;
            var shape = node.GetShape();
            var hot = ClonePointCollection(node.HotPoints);
            return new InkShapeRecognitionResult(
                node.GetShapeName(),
                legacy.Centroid,
                hot,
                shape.Width,
                shape.Height,
                node.Strokes);
        }

        private static PointCollection ClonePointCollection(PointCollection src)
        {
            var dst = new PointCollection();
            if (src == null) return dst;
            foreach (System.Windows.Point p in src)
                dst.Add(p);
            return dst;
        }

        public static bool IsContainShapeType(string name)
        {
            if (name.Contains("Triangle") || name.Contains("Circle") ||
                name.Contains("Rectangle") || name.Contains("Diamond") ||
                name.Contains("Parallelogram") || name.Contains("Square")
                || name.Contains("Ellipse"))
            {
                return true;
            }
            return false;
        }
    }

    //Recognizer 的实现

    public enum RecognizeLanguage
    {
        SimplifiedChinese = 0x0804,
        TraditionalChinese = 0x7c03,
        English = 0x0809
    }

    public class ShapeRecognizeResult
    {
        public ShapeRecognizeResult(Point centroid, PointCollection hotPoints, AnalysisAlternate analysisAlternate, InkDrawingNode node)
        {
            Centroid = centroid;
            HotPoints = hotPoints;
            AnalysisAlternate = analysisAlternate;
            InkDrawingNode = node;
        }

        public AnalysisAlternate AnalysisAlternate { get; }

        public Point Centroid { get; set; }

        public PointCollection HotPoints { get; }

        public InkDrawingNode InkDrawingNode { get; }
    }

    /// <summary>
    /// 图形识别类
    /// </summary>
    //public class ShapeRecogniser
    //{
    //    public InkAnalyzer _inkAnalyzer = null;

    //    private ShapeRecogniser()
    //    {
    //        this._inkAnalyzer = new InkAnalyzer
    //        {
    //            AnalysisModes = AnalysisModes.AutomaticReconciliationEnabled
    //        };
    //    }

    //    /// <summary>
    //    /// 根据笔迹集合返回图形名称字符串
    //    /// </summary>
    //    /// <param name="strokeCollection"></param>
    //    /// <returns></returns>
    //    public InkDrawingNode Recognition(StrokeCollection strokeCollection)
    //    {
    //        if (strokeCollection == null)
    //        {
    //            //MessageBox.Show("dddddd");
    //            return null;
    //        }

    //        InkDrawingNode result = null;
    //        try
    //        {
    //            this._inkAnalyzer.AddStrokes(strokeCollection);
    //            if (this._inkAnalyzer.Analyze().Successful)
    //            {
    //                result = _internalAnalyzer(this._inkAnalyzer);
    //                this._inkAnalyzer.RemoveStrokes(strokeCollection);
    //            }
    //        }
    //        catch (System.Exception ex)
    //        {
    //            //result = ex.Message;
    //            System.Diagnostics.Debug.WriteLine(ex.Message);
    //        }

    //        return result;
    //    }

    //    /// <summary>
    //    /// 实现笔迹的分析，返回图形对应的字符串
    //    /// 你在实际的应用中根据返回的字符串来生成对应的Shape
    //    /// </summary>
    //    /// <param name="ink"></param>
    //    /// <returns></returns>
    //    private InkDrawingNode _internalAnalyzer(InkAnalyzer ink)
    //    {
    //        try
    //        {
    //            ContextNodeCollection nodecollections = ink.FindNodesOfType(ContextNodeType.InkDrawing);
    //            foreach (ContextNode node in nodecollections)
    //            {
    //                InkDrawingNode drawingNode = node as InkDrawingNode;
    //                if (drawingNode != null)
    //                {
    //                    return drawingNode;//.GetShapeName();
    //                }
    //            }
    //        }
    //        catch (System.Exception ex)
    //        {
    //            System.Diagnostics.Debug.WriteLine(ex.Message);
    //        }

    //        return null;
    //    }


    //    private static ShapeRecogniser instance = null;
    //    public static ShapeRecogniser Instance
    //    {
    //        get
    //        {
    //            return instance == null ? (instance = new ShapeRecogniser()) : instance;
    //        }
    //    }
    //}


    //用于自动控制其他形状相对于圆的位置

    public class Circle
    {
        public Circle(Point centroid, double r, Stroke stroke)
        {
            Centroid = centroid;
            R = r;
            Stroke = stroke;
        }

        public Point Centroid { get; set; }

        public double R { get; set; }

        public Stroke Stroke { get; set; }
    }
}
