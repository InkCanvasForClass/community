using System;
using System.Collections.Generic;

namespace Ink_Canvas.Ink.Native
{
    internal readonly struct WetInkVertex
    {
        public WetInkVertex(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; }
        public float Y { get; }
    }

    internal readonly struct WetInkTip
    {
        public WetInkTip(
            WetInkVertex center,
            float radiusX,
            float radiusY,
            InkStylusTipShape shape)
        {
            Center = center;
            RadiusX = radiusX;
            RadiusY = radiusY;
            Shape = shape;
        }

        public WetInkVertex Center { get; }
        public float RadiusX { get; }
        public float RadiusY { get; }
        public InkStylusTipShape Shape { get; }
    }

    internal sealed class WetInkRibbonGeometry
    {
        public WetInkRibbonGeometry(
            IReadOnlyList<WetInkVertex> outline,
            WetInkTip startTip,
            WetInkTip endTip)
        {
            Outline = outline ?? throw new ArgumentNullException(nameof(outline));
            StartTip = startTip;
            EndTip = endTip;
        }

        public IReadOnlyList<WetInkVertex> Outline { get; }
        public WetInkTip StartTip { get; }
        public WetInkTip EndTip { get; }
        public WetInkVertex StartCenter => StartTip.Center;
        public WetInkVertex EndCenter => EndTip.Center;
        public float StartRadius => Math.Max(StartTip.RadiusX, StartTip.RadiusY);
        public float EndRadius => Math.Max(EndTip.RadiusX, EndTip.RadiusY);
        public bool IsSinglePoint =>
            Outline.Count == 0
            && (StartTip.RadiusX > 0 || StartTip.RadiusY > 0);
    }

    internal sealed class WetInkGeometryBuilder
    {
        private const float MinimumRadius = 0.175f;
        private const float MinimumSegmentLengthSquared = 0.0001f;

        public WetInkRibbonGeometry Build(
            IReadOnlyList<RealInkPoint> realPoints,
            IReadOnlyList<PredictedInkPoint> predictedPoints,
            InkStrokeStyleSnapshot style) =>
            Build(realPoints, predictedPoints, style, int.MaxValue);

        /// <summary>Span 版本：零分配入口（渲染线程只读 snapshot 数组）。</summary>
        internal WetInkRibbonGeometry Build(
            ReadOnlySpan<RealInkPoint> realPoints,
            ReadOnlySpan<PredictedInkPoint> predictedPoints,
            InkStrokeStyleSnapshot style) =>
            Build(realPoints, predictedPoints, style, int.MaxValue);

        /// <summary>Span 版本 + outputPointCount 限制。</summary>
        internal WetInkRibbonGeometry Build(
            ReadOnlySpan<RealInkPoint> realPoints,
            ReadOnlySpan<PredictedInkPoint> predictedPoints,
            InkStrokeStyleSnapshot style,
            int outputPointCount)
        {
            var pointCount = realPoints.Length + predictedPoints.Length;
            if (pointCount == 0 || outputPointCount <= 0)
                return Empty();

            var centers = new List<WetInkVertex>(pointCount);
            var radiiX = new List<float>(pointCount);
            var radiiY = new List<float>(pointCount);
            AppendRealPoints(
                realPoints,
                style,
                centers,
                radiiX,
                radiiY);
            AppendPredictedPoints(
                predictedPoints,
                style,
                centers,
                radiiX,
                radiiY);

            return BuildFromCenters(
                style,
                centers,
                radiiX,
                radiiY,
                outputPointCount);
        }

        internal WetInkRibbonGeometry Build(
            IReadOnlyList<RealInkPoint> realPoints,
            IReadOnlyList<PredictedInkPoint> predictedPoints,
            InkStrokeStyleSnapshot style,
            int outputPointCount)
        {
            var pointCount =
                (realPoints?.Count ?? 0)
                + (predictedPoints?.Count ?? 0);
            if (pointCount == 0 || outputPointCount <= 0)
                return Empty();

            var centers = new List<WetInkVertex>(pointCount);
            var radiiX = new List<float>(pointCount);
            var radiiY = new List<float>(pointCount);
            AppendRealPoints(
                realPoints,
                style,
                centers,
                radiiX,
                radiiY);
            AppendPredictedPoints(
                predictedPoints,
                style,
                centers,
                radiiX,
                radiiY);

            return BuildFromCenters(
                style,
                centers,
                radiiX,
                radiiY,
                outputPointCount);
        }

        private static WetInkRibbonGeometry BuildFromCenters(
            InkStrokeStyleSnapshot style,
            List<WetInkVertex> centers,
            List<float> radiiX,
            List<float> radiiY,
            int outputPointCount)
        {
            var outputCount = Math.Min(outputPointCount, centers.Count);
            if (outputCount == 0)
                return Empty();
            if (outputCount == 1)
            {
                var tip = CreateTip(
                    centers[0],
                    radiiX[0],
                    radiiY[0],
                    style.StylusTipShape);
                return new WetInkRibbonGeometry(
                    Array.Empty<WetInkVertex>(),
                    tip,
                    tip);
            }

            var left = new WetInkVertex[outputCount];
            var right = new WetInkVertex[outputCount];
            for (var i = 0; i < outputCount; i++)
            {
                GetTangent(
                    centers,
                    i,
                    outputCount,
                    out var tangentX,
                    out var tangentY);
                var normalX = -tangentY;
                var normalY = tangentX;
                var support = ResolveNormalSupport(
                    style.StylusTipShape,
                    radiiX[i],
                    radiiY[i],
                    normalX,
                    normalY);
                left[i] = new WetInkVertex(
                    centers[i].X + normalX * support,
                    centers[i].Y + normalY * support);
                right[i] = new WetInkVertex(
                    centers[i].X - normalX * support,
                    centers[i].Y - normalY * support);
            }

            var outline = new WetInkVertex[outputCount * 2];
            for (var i = 0; i < outputCount; i++)
                outline[i] = left[i];
            for (var i = 0; i < outputCount; i++)
                outline[outputCount + i] = right[outputCount - 1 - i];

            return new WetInkRibbonGeometry(
                outline,
                CreateTip(
                    centers[0],
                    radiiX[0],
                    radiiY[0],
                    style.StylusTipShape),
                CreateTip(
                    centers[outputCount - 1],
                    radiiX[outputCount - 1],
                    radiiY[outputCount - 1],
                    style.StylusTipShape));
        }

        private static WetInkRibbonGeometry Empty() =>
            new WetInkRibbonGeometry(
                Array.Empty<WetInkVertex>(),
                default,
                default);

        private static WetInkTip CreateTip(
            WetInkVertex center,
            float radiusX,
            float radiusY,
            InkStylusTipShape shape) =>
            new WetInkTip(center, radiusX, radiusY, shape);

        private static void AppendRealPoints(
            IReadOnlyList<RealInkPoint> points,
            InkStrokeStyleSnapshot style,
            List<WetInkVertex> centers,
            List<float> radiiX,
            List<float> radiiY)
        {
            if (points == null)
                return;
            for (var i = 0; i < points.Count; i++)
            {
                AppendPoint(
                    points[i].X,
                    points[i].Y,
                    points[i].Pressure,
                    style,
                    centers,
                    radiiX,
                    radiiY);
            }
        }

        private static void AppendPredictedPoints(
            IReadOnlyList<PredictedInkPoint> points,
            InkStrokeStyleSnapshot style,
            List<WetInkVertex> centers,
            List<float> radiiX,
            List<float> radiiY)
        {
            if (points == null)
                return;
            for (var i = 0; i < points.Count; i++)
            {
                AppendPoint(
                    points[i].X,
                    points[i].Y,
                    points[i].Pressure,
                    style,
                    centers,
                    radiiX,
                    radiiY);
            }
        }

        private static void AppendRealPoints(
            ReadOnlySpan<RealInkPoint> points,
            InkStrokeStyleSnapshot style,
            List<WetInkVertex> centers,
            List<float> radiiX,
            List<float> radiiY)
        {
            for (var i = 0; i < points.Length; i++)
            {
                AppendPoint(
                    points[i].X,
                    points[i].Y,
                    points[i].Pressure,
                    style,
                    centers,
                    radiiX,
                    radiiY);
            }
        }

        private static void AppendPredictedPoints(
            ReadOnlySpan<PredictedInkPoint> points,
            InkStrokeStyleSnapshot style,
            List<WetInkVertex> centers,
            List<float> radiiX,
            List<float> radiiY)
        {
            for (var i = 0; i < points.Length; i++)
            {
                AppendPoint(
                    points[i].X,
                    points[i].Y,
                    points[i].Pressure,
                    style,
                    centers,
                    radiiX,
                    radiiY);
            }
        }

        private static void AppendPoint(
            double x,
            double y,
            float pressure,
            InkStrokeStyleSnapshot style,
            List<WetInkVertex> centers,
            List<float> radiiX,
            List<float> radiiY)
        {
            if (!IsFinite(x) || !IsFinite(y) || !IsFinite(pressure))
                throw new ArgumentOutOfRangeException(nameof(x), "Wet ink geometry points must be finite.");

            var center = new WetInkVertex((float)x, (float)y);
            ResolveRadii(style, pressure, out var radiusX, out var radiusY);
            if (centers.Count != 0)
            {
                var previous = centers[centers.Count - 1];
                var deltaX = center.X - previous.X;
                var deltaY = center.Y - previous.Y;
                if (deltaX * deltaX + deltaY * deltaY
                    < MinimumSegmentLengthSquared)
                {
                    centers[centers.Count - 1] = center;
                    radiiX[radiiX.Count - 1] = radiusX;
                    radiiY[radiiY.Count - 1] = radiusY;
                    return;
                }
            }

            centers.Add(center);
            radiiX.Add(radiusX);
            radiiY.Add(radiusY);
        }

        private static void ResolveRadii(
            InkStrokeStyleSnapshot style,
            float pressure,
            out float radiusX,
            out float radiusY)
        {
            // 原版 ICC 湿墨 PressureToVisualScale：压感 0.5 对应标称宽度（scale=1.0）。
            var pressureScale = style.IgnorePressure
                ? 1f
                : Math.Max(0.22f, Math.Min(2.1f, 0.42f + 1.16f * Clamp(pressure, 0f, 1f)));
            radiusX = Math.Max(
                MinimumRadius,
                (float)Math.Max(0.35, style.Width)
                * pressureScale
                * 0.5f);
            radiusY = Math.Max(
                MinimumRadius,
                (float)Math.Max(0.35, style.Height)
                * pressureScale
                * 0.5f);
        }

        private static float ResolveNormalSupport(
            InkStylusTipShape shape,
            float radiusX,
            float radiusY,
            float normalX,
            float normalY)
        {
            if (shape == InkStylusTipShape.Rectangle)
            {
                return Math.Abs(normalX) * radiusX
                    + Math.Abs(normalY) * radiusY;
            }

            var x = radiusX * normalX;
            var y = radiusY * normalY;
            return (float)Math.Sqrt(x * x + y * y);
        }

        private static void GetTangent(
            IReadOnlyList<WetInkVertex> centers,
            int index,
            int outputCount,
            out float tangentX,
            out float tangentY)
        {
            WetInkVertex from;
            WetInkVertex to;
            if (index == 0)
            {
                from = centers[0];
                to = centers[1];
            }
            else if (index == outputCount - 1)
            {
                if (outputCount < centers.Count)
                {
                    from = centers[index];
                    to = centers[index + 1];
                }
                else
                {
                    from = centers[index - 1];
                    to = centers[index];
                }
            }
            else
            {
                from = centers[index - 1];
                to = centers[index + 1];
            }

            tangentX = to.X - from.X;
            tangentY = to.Y - from.Y;
            var length = (float)Math.Sqrt(
                tangentX * tangentX
                + tangentY * tangentY);
            if (length <= 0.0001f)
            {
                tangentX = 1;
                tangentY = 0;
                return;
            }

            tangentX /= length;
            tangentY /= length;
        }

        private static float Clamp(
            float value,
            float minimum,
            float maximum)
        {
            if (value < minimum)
                return minimum;
            return value > maximum ? maximum : value;
        }

        private static bool IsFinite(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
