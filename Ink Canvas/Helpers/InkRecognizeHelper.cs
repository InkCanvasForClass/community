using System.Threading.Tasks;
using System.Windows.Ink;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 墨迹形状/手写识别的对外门面。
    /// IACore 路径通过 IPC 调用 x86 辅助进程；WinRT 路径在主进程内直接调用。
    /// 主进程 (.NET 6 x64) 不再直接引用 IAWinFX 类型。
    /// </summary>
    public class InkRecognizeHelper
    {
        public static InkShapeRecognitionResult RecognizeShapeUnified(
            StrokeCollection strokes,
            ShapeRecognitionEngineMode mode)
        {
            if (strokes == null || strokes.Count == 0)
                return InkShapeRecognitionResult.Empty;

            if (ShapeRecognitionRouter.ResolveUseWinRt(mode))
                return InkShapeRecognitionResult.Empty;

            return IpcIACoreClient.Instance.Recognize(strokes);
        }

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
                if (ShapeRecognitionRouter.ResolveUseWinRt(mode))
                {
                    WinRtInkShapeRecognizer.Warmup();
                    WinRtHandwritingRecognizer.Warmup();
                }
                else
                {
                    IpcIACoreClient.Instance.Start();
                }
            }
            catch
            {
                // 预热失败不影响启动
            }
        }

        public static Task<HandwritingRecognitionResult> RecognizeHandwritingUnifiedAsync(
            StrokeCollection strokes,
            ShapeRecognitionEngineMode mode) =>
            InkRecognitionManager.Instance.RecognizeHandwritingAsync(strokes, mode);

        public static Task<StrokeCollection> CorrectHandwritingStrokesUnifiedAsync(
            StrokeCollection strokes,
            ShapeRecognitionEngineMode mode) =>
            InkRecognitionManager.Instance.CorrectInkAsync(
                strokes,
                mode,
                MainWindow.Settings?.InkToShape?.EnableWinRtHandwritingStrokeBeautify ?? false,
                MainWindow.Settings?.InkToShape?.HandwritingCorrectionFontFamily);

        public static Task<StrokeCollection> CorrectHandwritingStrokesUnifiedAsync(
            StrokeCollection strokes,
            ShapeRecognitionEngineMode mode,
            bool applyHandwritingBeautify) =>
            InkRecognitionManager.Instance.CorrectInkAsync(
                strokes,
                mode,
                applyHandwritingBeautify,
                MainWindow.Settings?.InkToShape?.HandwritingCorrectionFontFamily);

        public static bool IsContainShapeType(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            return name.Contains("Triangle") || name.Contains("Circle") ||
                   name.Contains("Rectangle") || name.Contains("Diamond") ||
                   name.Contains("Parallelogram") || name.Contains("Square") ||
                   name.Contains("Ellipse");
        }
    }

    public enum RecognizeLanguage
    {
        SimplifiedChinese = 0x0804,
        TraditionalChinese = 0x7c03,
        English = 0x0809
    }

    public class Circle
    {
        public Circle(System.Windows.Point centroid, double r, Stroke stroke)
        {
            Centroid = centroid;
            R = r;
            Stroke = stroke;
        }

        public System.Windows.Point Centroid { get; set; }
        public double R { get; set; }
        public Stroke Stroke { get; set; }
    }
}