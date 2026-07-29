using System;
using System.Collections.Generic;

namespace Ink_Canvas.Ink.Native
{
    internal enum NativeInkInputKind
    {
        Pen,
        Touch,
        Mouse
    }

    internal enum InkStylusTipShape
    {
        Ellipse,
        Rectangle
    }

    internal enum InkRenderMode
    {
        Standard,
        Laser
    }

    [Flags]
    internal enum NativeInkSampleFlags
    {
        None = 0,
        InContact = 1,
        Primary = 2,
        Inverted = 4,
        Eraser = 8,
        Canceled = 16
    }

    internal readonly struct RawInkSample
    {
        public RawInkSample(
            uint pointerId,
            NativeInkInputKind inputKind,
            double x,
            double y,
            float pressure,
            bool hasPressure,
            long timestampMicroseconds,
            uint frameId,
            NativeInkSampleFlags flags,
            float contactWidthPixels = 0,
            float contactHeightPixels = 0)
        {
            PointerId = pointerId;
            InputKind = inputKind;
            X = x;
            Y = y;
            Pressure = pressure;
            HasPressure = hasPressure;
            TimestampMicroseconds = timestampMicroseconds;
            FrameId = frameId;
            Flags = flags;
            ContactWidthPixels = contactWidthPixels;
            ContactHeightPixels = contactHeightPixels;
        }

        public uint PointerId { get; }
        public NativeInkInputKind InputKind { get; }
        public double X { get; }
        public double Y { get; }
        public float Pressure { get; }
        public bool HasPressure { get; }
        public long TimestampMicroseconds { get; }
        public uint FrameId { get; }
        public NativeInkSampleFlags Flags { get; }
        public float ContactWidthPixels { get; }
        public float ContactHeightPixels { get; }
    }

    internal readonly struct RealInkPoint
    {
        public RealInkPoint(double x, double y, float pressure, long timestampMicroseconds)
        {
            X = x;
            Y = y;
            Pressure = pressure;
            TimestampMicroseconds = timestampMicroseconds;
        }

        public double X { get; }
        public double Y { get; }
        public float Pressure { get; }
        public long TimestampMicroseconds { get; }
    }

    internal readonly struct PredictedInkPoint
    {
        public PredictedInkPoint(double x, double y, float pressure, long timestampMicroseconds)
        {
            X = x;
            Y = y;
            Pressure = pressure;
            TimestampMicroseconds = timestampMicroseconds;
        }

        public double X { get; }
        public double Y { get; }
        public float Pressure { get; }
        public long TimestampMicroseconds { get; }
    }

    internal readonly struct InkStrokeStyleSnapshot
    {
        public InkStrokeStyleSnapshot(
            uint colorArgb,
            double width,
            double height,
            bool ignorePressure,
            bool isHighlighter,
            bool useVelocityBrushTip,
            float velocityBrushTipMix,
            float minimumDistanceScale,
            long coordinateGeneration,
            long pageGeneration,
            InkStylusTipShape stylusTipShape = InkStylusTipShape.Ellipse,
            InkRenderMode renderMode = InkRenderMode.Standard)
        {
            ColorArgb = colorArgb;
            Width = width;
            Height = height;
            IgnorePressure = ignorePressure;
            IsHighlighter = isHighlighter;
            UseVelocityBrushTip = useVelocityBrushTip;
            VelocityBrushTipMix = velocityBrushTipMix;
            MinimumDistanceScale = minimumDistanceScale;
            CoordinateGeneration = coordinateGeneration;
            PageGeneration = pageGeneration;
            StylusTipShape = stylusTipShape;
            RenderMode = renderMode;
        }

        public uint ColorArgb { get; }
        public double Width { get; }
        public double Height { get; }
        public bool IgnorePressure { get; }
        public bool IsHighlighter { get; }
        public bool UseVelocityBrushTip { get; }
        public float VelocityBrushTipMix { get; }
        public float MinimumDistanceScale { get; }
        public long CoordinateGeneration { get; }
        public long PageGeneration { get; }
        public InkStylusTipShape StylusTipShape { get; }
        public InkRenderMode RenderMode { get; }
    }

    internal sealed class NativeStrokeCommitPayload
    {
        private readonly RealInkPoint[] _points;

        public NativeStrokeCommitPayload(
            long sessionId,
            uint pointerId,
            NativeInkInputKind inputKind,
            InkStrokeStyleSnapshot style,
            IReadOnlyList<RealInkPoint> points,
            long startedAtMicroseconds,
            long endedAtMicroseconds,
            bool velocityBrushTipApplied,
            bool finalBrushTipApplied = false)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (points.Count == 0) throw new ArgumentException("A committed stroke must contain at least one real point.", nameof(points));

            SessionId = sessionId;
            PointerId = pointerId;
            InputKind = inputKind;
            Style = style;
            StartedAtMicroseconds = startedAtMicroseconds;
            EndedAtMicroseconds = endedAtMicroseconds;
            VelocityBrushTipApplied = velocityBrushTipApplied;
            FinalBrushTipApplied = finalBrushTipApplied;
            _points = new RealInkPoint[points.Count];
            for (var i = 0; i < points.Count; i++)
                _points[i] = points[i];
        }

        public long SessionId { get; }
        public uint PointerId { get; }
        public NativeInkInputKind InputKind { get; }
        public InkStrokeStyleSnapshot Style { get; }
        public IReadOnlyList<RealInkPoint> Points => _points;
        public long StartedAtMicroseconds { get; }
        public long EndedAtMicroseconds { get; }
        public bool VelocityBrushTipApplied { get; }
        /// <summary>InkStyle 0/1 tip applied at pen-up; dry stroke must honor PressureFactor.</summary>
        public bool FinalBrushTipApplied { get; }
    }
}
