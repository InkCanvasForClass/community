using OSVersionExtension;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;

namespace Ink_Canvas.Helpers
{
    /// <summary>墨迹形状识别后端：自动 / IACore / WinRT。</summary>
    public enum ShapeRecognitionEngineMode
    {
        Auto = 0,
        IACore = 1,
        WinRT = 2,
    }

    public static class ShapeRecognitionRouter
    {
        /// <summary>
        /// 自动模式：在 Windows 10 及以上系统默认使用 WinRT，否则使用 IACore。
        /// </summary>
        public static bool ResolveUseWinRt(ShapeRecognitionEngineMode mode)
        {
            if (mode == ShapeRecognitionEngineMode.WinRT) return true;
            if (mode == ShapeRecognitionEngineMode.IACore) return false;
            return OSVersion.GetOperatingSystem() >= OSVersionExtension.OperatingSystem.Windows10;
        }

        public static bool ShouldRunShapeRecognition(bool inkToShapeEnabled, ShapeRecognitionEngineMode mode)
        {
            if (!inkToShapeEnabled) return false;
            if (ResolveUseWinRt(mode))
                return OSVersion.GetOperatingSystem() >= OSVersionExtension.OperatingSystem.Windows10;
            return true;
        }

        public static ShapeRecognitionEngineMode FromSettingsInt(int value)
        {
            if (value == (int)ShapeRecognitionEngineMode.IACore) return ShapeRecognitionEngineMode.IACore;
            if (value == (int)ShapeRecognitionEngineMode.WinRT) return ShapeRecognitionEngineMode.WinRT;
            return ShapeRecognitionEngineMode.Auto;
        }
    }

    /// <summary>与具体识别后端无关的形状识别结果，供统一纠正模块消费。</summary>
    public sealed class InkShapeRecognitionResult
    {
        public static readonly InkShapeRecognitionResult Empty = new InkShapeRecognitionResult();

        private InkShapeRecognitionResult()
        {
            IsSuccess = false;
            ShapeName = string.Empty;
            Centroid = new Point();
            HotPoints = new PointCollection();
            StrokesToRemove = new StrokeCollection();
        }

        public InkShapeRecognitionResult(
            string shapeName,
            Point centroid,
            PointCollection hotPoints,
            double shapeWidth,
            double shapeHeight,
            StrokeCollection strokesToRemove)
        {
            ShapeName = shapeName ?? string.Empty;
            Centroid = centroid;
            HotPoints = hotPoints ?? new PointCollection();
            ShapeWidth = shapeWidth;
            ShapeHeight = shapeHeight;
            StrokesToRemove = strokesToRemove ?? new StrokeCollection();
            IsSuccess = StrokesToRemove.Count > 0
                        && !string.IsNullOrEmpty(ShapeName)
                        && ShapeName != "Drawing";
        }

        public bool IsSuccess { get; }
        public string ShapeName { get; }
        public Point Centroid { get; set; }
        public PointCollection HotPoints { get; }
        public double ShapeWidth { get; }
        public double ShapeHeight { get; }
        public StrokeCollection StrokesToRemove { get; }
    }
}
