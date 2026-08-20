using Ink_Canvas.Controls;
using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Point = System.Windows.Point;

namespace Ink_Canvas
{
    public partial class MainWindow : Ink_Canvas.Helpers.PerformanceTransparentWin
    {
        #region Multi-Touch

        /// <summary>
        /// 是否处于多点触控模式
        /// </summary>
        private bool isInMultiTouchMode;
        /// <summary>
        /// 存储触摸设备ID的列表
        /// </summary>
        private List<int> dec = new List<int>();
        /// <summary>
        /// 是否处于单指拖动模式
        /// </summary>
        private bool isSingleFingerDragMode;
        /// <summary>
        /// 中心点坐标
        /// </summary>
        private Point centerPoint = new Point(0, 0);
        /// <summary>
        /// 上次的InkCanvas编辑模式
        /// </summary>
        private InkCanvasEditingMode lastInkCanvasEditingMode = InkCanvasEditingMode.Ink;
        /// <summary>
        /// 上次触摸按下的时间
        /// </summary>
        private DateTime lastTouchDownTime = DateTime.MinValue;
        /// <summary>
        /// 多点触控延迟时间（毫秒）
        /// </summary>
        private const double MULTI_TOUCH_DELAY_MS = 100;
        private bool isMultiTouchTimerActive;
        private bool isPalmEraserActive;
        private bool palmEraserWasEnabledBeforeMultiTouch;
        private InkCanvasEditingMode palmEraserPreviousEditingMode = InkCanvasEditingMode.Ink;
        private readonly Dictionary<int, RealtimeBrushTipState> _realtimeBrushTipStates = new Dictionary<int, RealtimeBrushTipState>();
        private readonly Guid RealtimeVelocityBrushTipAppliedGuid = new Guid("74E57D95-945F-4A8C-B52A-7D3EF2D4FD5B");
        internal const int MouseRealtimeStrokeId = -100001;
        private readonly HashSet<int> _activeRealtimeTouchStrokeIds = new HashSet<int>();
        private readonly HashSet<int> _activeRealtimeStylusStrokeIds = new HashSet<int>();
        private readonly HashSet<int> _activeTouchStrokeIds = new HashSet<int>();
        private bool _isInkCanvasManipulationEnabledBeforeTouchInk;
        private bool _hasStoredInkCanvasManipulationStateForTouchInk;

        private readonly Dictionary<int, DispatcherTimer> _pauseStraightenTimers = new Dictionary<int, DispatcherTimer>();
        private const int PauseStraightenDelayMs = 300;

        private sealed class OneEuroFilter
        {
            private readonly float _minCutoff;
            private readonly float _beta;
            private readonly float _dCutoff;
            private bool _initialized;
            private float _xPrev;
            private float _dxPrev;

            public OneEuroFilter(float minCutoff, float beta, float dCutoff)
            {
                _minCutoff = minCutoff;
                _beta = beta;
                _dCutoff = dCutoff;
            }

            public float Filter(float value, float dt, float speed)
            {
                if (!_initialized)
                {
                    _initialized = true;
                    _xPrev = value;
                    _dxPrev = 0f;
                    return value;
                }

                var dx = (value - _xPrev) / Math.Max(1e-6f, dt);
                var aD = Alpha(_dCutoff, dt);
                var dxHat = Lerp(_dxPrev, dx, aD);
                var a = Alpha(_minCutoff + _beta * speed, dt);
                var xHat = Lerp(_xPrev, value, a);
                _xPrev = xHat;
                _dxPrev = dxHat;
                return xHat;
            }

            private static float Alpha(float cutoff, float dt)
            {
                var tau = 1f / (2f * (float)Math.PI * Math.Max(1e-3f, cutoff));
                return 1f / (1f + tau / Math.Max(1e-6f, dt));
            }

            private static float Lerp(float a, float b, float t) => a + (b - a) * t;
        }

        private sealed class RealtimeBrushTipState
        {
            public float LastRawX { get; set; }
            public float LastRawY { get; set; }
            public long LastTimestampMs { get; set; }
            public long LastTouchInputTimestampTicks { get; set; }
            public float SmoothedSampleRateHz { get; set; } = 120f;
            public bool SawPressureVariation { get; set; }
            public bool HasSeed { get; set; }
            public bool HasTouchPoint { get; set; }
            public Point PreviousTouchPoint { get; set; }
            public Point LastTouchPoint { get; set; }
            public bool HasTouchDirection { get; set; }
            public Vector LastTouchDirection { get; set; }
            public float LastSmoothX { get; set; }
            public float LastSmoothY { get; set; }
            public float LastSmoothPressure { get; set; } = 0.5f;
            public OneEuroFilter FilterX { get; } = new OneEuroFilter(1.2f, 0.015f, 1f);
            public OneEuroFilter FilterY { get; } = new OneEuroFilter(1.2f, 0.015f, 1f);
            public OneEuroFilter FilterPressure { get; } = new OneEuroFilter(1f, 0.02f, 1f);
        }

        private static long RealtimeNowMs() => Environment.TickCount64;

        private static float RealtimeClamp(float x, float min, float max)
        {
            if (x < min) return min;
            if (x > max) return max;
            return x;
        }

        private static float WidthToPressure(float width, float baseWidth)
        {
            if (baseWidth <= 1e-4f) return 0.5f;
            var scale = width / baseWidth;
            return RealtimeClamp((scale - 0.42f) / 1.16f, 0.08f, 1f);
        }

        private bool ShouldUseRealtimeVelocityBrushTip()
        {
            return Settings.Canvas.InkStyle == 3
                && Settings.Canvas.VelocityBrushTipMix > 0
                && !Settings.Canvas.DisablePressure;
        }

        private bool ShouldUseRealtimeVelocityBrushTipForTouch()
        {
            return Settings.Canvas.InkStyle == 3
                && Settings.Canvas.VelocityBrushTipMix > 0
                && !Settings.Canvas.DisablePressure
                && drawingShapeMode == 0
                && !isPalmEraserActive;
        }

        internal bool ShouldUseRealtimeVelocityBrushTipForMouse()
        {
            return ShouldUseRealtimeVelocityBrushTip()
                   && drawingShapeMode == 0
                   && !isPalmEraserActive;
        }

        private static bool IsTouchStylusDevice(StylusDevice stylusDevice)
        {
            return stylusDevice?.TabletDevice?.Type == TabletDeviceType.Touch;
        }

        private void BeginTouchInkInput()
        {
            if (!_hasStoredInkCanvasManipulationStateForTouchInk)
            {
                _isInkCanvasManipulationEnabledBeforeTouchInk = inkCanvas.IsManipulationEnabled;
                _hasStoredInkCanvasManipulationStateForTouchInk = true;
            }
            inkCanvas.IsManipulationEnabled = false;
        }

        private void EndTouchInkInputIfIdle()
        {
            if (_activeRealtimeTouchStrokeIds.Count != 0 || _activeRealtimeStylusStrokeIds.Count != 0 || _activeTouchStrokeIds.Count != 0)
                return;
            if (!_hasStoredInkCanvasManipulationStateForTouchInk)
                return;
            inkCanvas.IsManipulationEnabled = _isInkCanvasManipulationEnabledBeforeTouchInk;
            _hasStoredInkCanvasManipulationStateForTouchInk = false;
        }

        /// <summary>
        /// 强制清空触摸活动集合，作为异常失活路径（TouchLeave / PointerCaptureLost /
        /// LostFocus / Deactivated 等没有对应 TouchUp 的场景）的兜底。
        /// 不修改其他触摸状态，避免与既有 dec.Clear / dec.Remove 兜底产生不一致。
        /// </summary>
        internal void AbortAllActiveTouchInputs()
        {
            if (_activeRealtimeTouchStrokeIds.Count > 0)
            {
                foreach (var strokeId in _activeRealtimeTouchStrokeIds)
                    RealtimeInkFrameScheduler.Cancel(StrokeVisualList.TryGetValue(strokeId, out var sv) ? sv : null);
                _activeRealtimeTouchStrokeIds.Clear();
            }
            if (_activeRealtimeStylusStrokeIds.Count > 0)
                _activeRealtimeStylusStrokeIds.Clear();
            if (_activeTouchStrokeIds.Count > 0)
                _activeTouchStrokeIds.Clear();
            if (_realtimeBrushTipStates.Count > 0)
                _realtimeBrushTipStates.Clear();
            // 同步清理水印自动隐藏的触摸跟踪，避免丢失 TouchUp 时陈旧状态阻塞恢复
            if (_whiteboardTipsAreaTouchIds.Count > 0)
                _whiteboardTipsAreaTouchIds.Clear();
            foreach (var timerEntry in _pauseStraightenTimers)
            {
                timerEntry.Value.Stop();
            }
            _pauseStraightenTimers.Clear();
            EndTouchInkInputIfIdle();
        }

        internal void EnsureRealtimeStylusPipelineBinding()
        {
            if (inkCanvas == null) return;

            inkCanvas.StylusDown -= MainWindow_StylusDown;
            inkCanvas.StylusMove -= MainWindow_StylusMove;
            inkCanvas.StylusUp -= MainWindow_StylusUp;

            inkCanvas.StylusDown += MainWindow_StylusDown;
            inkCanvas.StylusMove += MainWindow_StylusMove;
            inkCanvas.StylusUp += MainWindow_StylusUp;

            // 手动 StrokeVisual 采集（实时笔锋 / 多指书写）要求 InkCanvas 自身不收集笔画；
            // 其余情况保持 Ink，由 InkCanvas 内建采集，两者不可同时生效。
            var isManualStylusCollection = ShouldUseRealtimeVelocityBrushTip() || isInMultiTouchMode;
            if (isManualStylusCollection
                && inkCanvas.EditingMode != InkCanvasEditingMode.EraseByPoint
                && inkCanvas.EditingMode != InkCanvasEditingMode.EraseByStroke
                && inkCanvas.EditingMode != InkCanvasEditingMode.Select)
            {
                inkCanvas.EditingMode = InkCanvasEditingMode.None;
            }
            else if (!isManualStylusCollection
                     && inkCanvas.EditingMode == InkCanvasEditingMode.None)
            {
                inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
            }
        }

        private void InitializeRealtimeBrushTipState(int stylusId, StylusDownEventArgs e)
        {
            if (!ShouldUseRealtimeVelocityBrushTip())
            {
                _realtimeBrushTipStates.Remove(stylusId);
                return;
            }

            var startPoint = e.GetPosition(inkCanvas);
            _realtimeBrushTipStates[stylusId] = new RealtimeBrushTipState
            {
                LastRawX = (float)startPoint.X,
                LastRawY = (float)startPoint.Y,
                LastTimestampMs = RealtimeNowMs(),
                LastTouchInputTimestampTicks = Stopwatch.GetTimestamp(),
                HasTouchPoint = true,
                LastTouchPoint = startPoint
            };
        }

        private void InitializeRealtimeBrushTipStateFromPoint(int strokeId, Point startPoint)
        {
            if (!ShouldUseRealtimeVelocityBrushTipForTouch() && strokeId != MouseRealtimeStrokeId)
            {
                _realtimeBrushTipStates.Remove(strokeId);
                return;
            }
            if (!ShouldUseRealtimeVelocityBrushTipForMouse() && strokeId == MouseRealtimeStrokeId)
            {
                _realtimeBrushTipStates.Remove(strokeId);
                return;
            }

            _realtimeBrushTipStates[strokeId] = new RealtimeBrushTipState
            {
                LastRawX = (float)startPoint.X,
                LastRawY = (float)startPoint.Y,
                LastTimestampMs = RealtimeNowMs(),
                LastTouchInputTimestampTicks = Stopwatch.GetTimestamp(),
                HasTouchPoint = true,
                LastTouchPoint = startPoint
            };
        }

        private void CleanupRealtimeBrushTipState(int stylusId)
        {
            _realtimeBrushTipStates.Remove(stylusId);
        }

        private float GetRealtimeBrushTipMinDistance(float smoothedSampleRateHz)
        {
            var baseMinDist = smoothedSampleRateHz > 160f ? 0.35f
                : smoothedSampleRateHz > 90f ? 0.25f
                : 0.15f;
            var scale = RealtimeClamp((float)Settings.Canvas.RealtimeBrushTipMinDistanceScale, 0f, 2f);
            return baseMinDist * scale;
        }

        private static IEnumerable<Point> InterpolateTouchPoints(Point from, Point to, double spacing = 1.2)
        {
            var dx = to.X - from.X;
            var dy = to.Y - from.Y;
            var distance = Math.Sqrt(dx * dx + dy * dy);
            var safeSpacing = Math.Max(0.1, spacing);
            var steps = Math.Min(24, Math.Max(1, (int)Math.Ceiling(distance / safeSpacing)));
            for (var i = 1; i <= steps; i++)
            {
                var t = (double)i / steps;
                yield return new Point(from.X + dx * t, from.Y + dy * t);
            }
        }

        private static IEnumerable<Point> InterpolateTouchPoints(
            RealtimeBrushTipState state,
            Point to,
            double spacing = 1.2)
        {
            if (!state.HasTouchDirection)
            {
                foreach (var p in InterpolateTouchPoints(state.LastTouchPoint, to, spacing))
                    yield return p;
                yield break;
            }

            var from = state.LastTouchPoint;
            var chord = to - from;
            var distance = chord.Length;
            if (distance < 0.1)
                yield break;

            var incoming = state.LastTouchDirection;
            if (incoming.LengthSquared > 0.0001)
            {
                incoming.Normalize();
                var current = chord;
                current.Normalize();
                var dot = Math.Max(-1, Math.Min(1, incoming.X * current.X + incoming.Y * current.Y));
                var angle = Math.Acos(dot);
                if (angle < Math.PI * 0.72)
                {
                    var tangentLength = Math.Min(distance * 0.45, 18);
                    var c1 = from + incoming * tangentLength;
                    var c2 = to - current * tangentLength;
                    var safeSpacing = Math.Max(0.1, spacing);
                    var steps = Math.Min(24, Math.Max(1, (int)Math.Ceiling(distance / safeSpacing)));
                    for (var i = 1; i <= steps; i++)
                    {
                        var t = (double)i / steps;
                        var u = 1 - t;
                        yield return new Point(
                            u * u * u * from.X + 3 * u * u * t * c1.X + 3 * u * t * t * c2.X + t * t * t * to.X,
                            u * u * u * from.Y + 3 * u * u * t * c1.Y + 3 * u * t * t * c2.Y + t * t * t * to.Y);
                    }
                    yield break;
                }
            }

            foreach (var p in InterpolateTouchPoints(from, to, spacing))
                yield return p;
        }

        private static int GetTouchInterpolationStepCount(double distance, double spacing, int maxSteps = 24)
        {
            var safeSpacing = Math.Max(0.1, spacing);
            var cappedMaxSteps = Math.Max(1, maxSteps);
            return Math.Min(cappedMaxSteps, Math.Max(1, (int)Math.Ceiling(distance / safeSpacing)));
        }

        private static Point GetTouchInterpolationPoint(
            RealtimeBrushTipState state,
            Point to,
            int stepIndex,
            int stepCount)
        {
            var from = state.LastTouchPoint;
            var chord = to - from;
            var t = (double)stepIndex / stepCount;

            if (state.HasTouchDirection && state.LastTouchDirection.LengthSquared > 0.0001)
            {
                var incoming = state.LastTouchDirection;
                incoming.Normalize();
                var current = chord;
                current.Normalize();
                var dot = Math.Max(-1, Math.Min(1, incoming.X * current.X + incoming.Y * current.Y));
                if (Math.Acos(dot) < Math.PI * 0.72)
                {
                    var tangentLength = Math.Min(chord.Length * 0.45, 18);
                    var c1 = from + incoming * tangentLength;
                    var c2 = to - current * tangentLength;
                    var u = 1 - t;
                    return new Point(
                        u * u * u * from.X + 3 * u * u * t * c1.X + 3 * u * t * t * c2.X + t * t * t * to.X,
                        u * u * u * from.Y + 3 * u * u * t * c1.Y + 3 * u * t * t * c2.Y + t * t * t * to.Y);
                }
            }

            return new Point(from.X + chord.X * t, from.Y + chord.Y * t);
        }

        // Target Added/Raw about 2x-4x for TouchVelocity: hard-cap steps and use larger spacing.
        private static void GetTouchVelocityInterpolationParams(
            RealtimeBrushTipState state,
            Point to,
            double speed,
            out double spacing,
            out int maxSteps)
        {
            var chord = to - state.LastTouchPoint;
            var distance = chord.Length;
            if (distance < 0.1)
            {
                spacing = 2.0;
                maxSteps = 1;
                return;
            }

            var turnAngleDegrees = 0.0;
            if (state.HasTouchDirection && state.LastTouchDirection.LengthSquared > 0.0001)
            {
                var previous = state.LastTouchDirection;
                previous.Normalize();
                var current = chord;
                current.Normalize();
                var dot = Math.Max(-1, Math.Min(1, previous.X * current.X + previous.Y * current.Y));
                turnAngleDegrees = Math.Acos(dot) * 180.0 / Math.PI;
            }

            // Straight motion: sparse samples. Curves keep denser samples but still hard-capped.
            if (turnAngleDegrees >= 35.0)
            {
                spacing = 1.6;
                maxSteps = 5;
                return;
            }

            if (speed < 300.0)
                spacing = 2.0;
            else if (speed < 900.0)
                spacing = 3.2;
            else
                spacing = 4.5;

            if (turnAngleDegrees > 18.0)
            {
                var curvatureFactor = Math.Min(1.0, (turnAngleDegrees - 18.0) / 17.0);
                spacing = spacing + (1.6 - spacing) * curvatureFactor;
                maxSteps = 5;
            }
            else
            {
                maxSteps = 3;
            }
        }

        private static void UpdateTouchInterpolationState(RealtimeBrushTipState state, Point point)
        {
            var delta = point - state.LastTouchPoint;
            if (delta.LengthSquared > 0.0001)
            {
                state.PreviousTouchPoint = state.LastTouchPoint;
                state.LastTouchDirection = delta;
                state.HasTouchDirection = true;
            }
            state.LastTouchPoint = point;
        }

        private void AppendInterpolatedTouchPoints(StrokeVisual strokeVisual, int strokeId, Point point)
        {
            if (strokeVisual == null) return;
            var startedAt = RealtimeInkPerformanceMonitor.IsDebugLoggingEnabled ? Stopwatch.GetTimestamp() : 0L;
            var initialPointCount = strokeVisual.Stroke?.StylusPoints.Count ?? 0;
            try
            {
                if (!_realtimeBrushTipStates.TryGetValue(strokeId, out var state))
                {
                    state = new RealtimeBrushTipState { HasTouchPoint = true, LastTouchPoint = point };
                    _realtimeBrushTipStates[strokeId] = state;
                    strokeVisual.Add(new StylusPoint(point.X, point.Y, 0.5f));
                    return;
                }

                if (!state.HasTouchPoint)
                {
                    state.HasTouchPoint = true;
                    state.LastTouchPoint = point;
                    strokeVisual.Add(new StylusPoint(point.X, point.Y, 0.5f));
                    return;
                }

                foreach (var p in InterpolateTouchPoints(state, point))
                {
                    strokeVisual.Add(new StylusPoint(p.X, p.Y, 0.5f));
                }
                UpdateTouchInterpolationState(state, point);
            }
            finally
            {
                if (startedAt != 0L)
                {
                    var finalPointCount = strokeVisual.Stroke?.StylusPoints.Count ?? initialPointCount;
                    RealtimeInkPerformanceMonitor.RecordInputEvent(
                        strokeVisual,
                        1,
                        Math.Max(0, finalPointCount - initialPointCount),
                        Stopwatch.GetTimestamp() - startedAt);
                }
            }
        }
        private bool TryAppendRealtimeVelocityBrushTipPoints(StrokeVisual strokeVisual, StylusEventArgs e)
        {
            if (!ShouldUseRealtimeVelocityBrushTip() || strokeVisual == null || e?.StylusDevice == null)
                return false;

            if (!_realtimeBrushTipStates.TryGetValue(e.StylusDevice.Id, out var state))
                return false;

            var stylusPointCollection = e.GetStylusPoints(this);
            if (stylusPointCollection == null || stylusPointCollection.Count == 0)
                return true;

            var startedAt = RealtimeInkPerformanceMonitor.IsDebugLoggingEnabled ? Stopwatch.GetTimestamp() : 0L;
            var addedPointCount = 0L;
            try
            {
                var mix = RealtimeClamp((float)Settings.Canvas.VelocityBrushTipMix, 0f, 1f);
                var appended = false;
                var baseWidth = (float)Math.Max(0.35,
                    strokeVisual.Stroke?.DrawingAttributes?.Width ?? inkCanvas.DefaultDrawingAttributes.Width);

                foreach (StylusPoint rawPoint in stylusPointCollection)
                {
                    var nowMs = RealtimeNowMs();
                    var dtMs = Math.Max(1L, nowMs - state.LastTimestampMs);
                    var dt = dtMs / 1000f;
                    var sampleRate = 1f / Math.Max(1e-4f, dt);
                    state.SmoothedSampleRateHz = state.SmoothedSampleRateHz * 0.85f + sampleRate * 0.15f;

                    var rawX = (float)rawPoint.X;
                    var rawY = (float)rawPoint.Y;
                    var dx = rawX - state.LastRawX;
                    var dy = rawY - state.LastRawY;
                    var dist = (float)Math.Sqrt(dx * dx + dy * dy);
                    var speed = dist / dt;

                    var filteredX = state.FilterX.Filter(rawX, dt, speed);
                    var filteredY = state.FilterY.Filter(rawY, dt, speed);

                    var hwPressure = RealtimeClamp((float)rawPoint.PressureFactor, 0f, 1f);
                    if (Math.Abs(hwPressure - 0.5f) > 0.02f)
                        state.SawPressureVariation = true;
                    var usePressure = state.SawPressureVariation && hwPressure > 0f;

                    var width = baseWidth;
                    if (usePressure)
                        width *= 0.25f + 0.75f * hwPressure;
                    var speedNormalization = 1800f + state.SmoothedSampleRateHz * 3.5f;
                    width *= RealtimeClamp(1.15f - (speed / speedNormalization), 0.45f, 1.25f);
                    var speedPressure = WidthToPressure(width, baseWidth);

                    var pressure = usePressure
                        ? ((1f - mix) * hwPressure + mix * speedPressure)
                        : speedPressure;
                    pressure = RealtimeClamp(pressure, 0.08f, 1f);
                    pressure = state.FilterPressure.Filter(pressure, dt, speed);

                    var minDist = GetRealtimeBrushTipMinDistance(state.SmoothedSampleRateHz);
                    if (dist < minDist && state.HasSeed)
                    {
                        state.LastRawX = rawX;
                        state.LastRawY = rawY;
                        state.LastTimestampMs = nowMs;
                        continue;
                    }

                    if (!state.HasSeed)
                    {
                        state.HasSeed = true;
                        state.LastSmoothX = filteredX;
                        state.LastSmoothY = filteredY;
                        state.LastSmoothPressure = pressure;
                        strokeVisual.Add(new StylusPoint(filteredX, filteredY, pressure));
                        addedPointCount++;
                    }
                    else
                    {
                        // 采用中点链减抖：保持实时笔锋同时降低折线锯齿
                        var midX = (state.LastSmoothX + filteredX) * 0.5f;
                        var midY = (state.LastSmoothY + filteredY) * 0.5f;
                        var midPressure = (state.LastSmoothPressure + pressure) * 0.5f;
                        strokeVisual.Add(new StylusPoint(midX, midY, midPressure));
                        addedPointCount++;
                        state.LastSmoothX = filteredX;
                        state.LastSmoothY = filteredY;
                        state.LastSmoothPressure = pressure;
                    }

                    state.LastRawX = rawX;
                    state.LastRawY = rawY;
                    state.LastTimestampMs = nowMs;
                    appended = true;
                }

                var committedStroke = strokeVisual.Stroke;
                if (appended && committedStroke != null)
                {
                    if (committedStroke.DrawingAttributes != null)
                        committedStroke.DrawingAttributes.IgnorePressure = false;
                    if (!committedStroke.ContainsPropertyData(RealtimeVelocityBrushTipAppliedGuid))
                        committedStroke.AddPropertyData(RealtimeVelocityBrushTipAppliedGuid, true);
                }

                return true;
            }
            finally
            {
                if (startedAt != 0L)
                {
                    RealtimeInkPerformanceMonitor.RecordInputEvent(
                        strokeVisual,
                        stylusPointCollection.Count,
                        addedPointCount,
                        Stopwatch.GetTimestamp() - startedAt);
                }
            }
        }

        private bool TryAppendRealtimeVelocityBrushTipPoint(StrokeVisual strokeVisual, int strokeId, Point point, float rawPressure = 0.5f)
        {
            var startedAt = RealtimeInkPerformanceMonitor.IsDebugLoggingEnabled ? Stopwatch.GetTimestamp() : 0L;
            var initialPointCount = strokeVisual?.Stroke?.StylusPoints.Count ?? 0;
            try
            {
                return TryAppendRealtimeVelocityBrushTipPointCore(strokeVisual, strokeId, point, rawPressure);
            }
            finally
            {
                if (startedAt != 0L)
                {
                    var finalPointCount = strokeVisual?.Stroke?.StylusPoints.Count ?? initialPointCount;
                    RealtimeInkPerformanceMonitor.RecordInputEvent(
                        strokeVisual,
                        1,
                        Math.Max(0, finalPointCount - initialPointCount),
                        Stopwatch.GetTimestamp() - startedAt);
                }
            }
        }

        private bool TryAppendRealtimeVelocityBrushTipPointCore(
            StrokeVisual strokeVisual,
            int strokeId,
            Point point,
            float rawPressure = 0.5f,
            float? motionSpeed = null,
            float? motionDt = null,
            bool updateSampleRate = true,
            bool useMidPointChain = true)
        {
            var allow = strokeId == MouseRealtimeStrokeId
                ? ShouldUseRealtimeVelocityBrushTipForMouse()
                : ShouldUseRealtimeVelocityBrushTipForTouch();
            if (!allow || strokeVisual == null)
                return false;
            if (!_realtimeBrushTipStates.TryGetValue(strokeId, out var state))
                return false;

            var mix = RealtimeClamp((float)Settings.Canvas.VelocityBrushTipMix, 0f, 1f);
            var nowMs = RealtimeNowMs();
            var measuredDt = Math.Max(1L, nowMs - state.LastTimestampMs) / 1000f;
            var dt = Math.Max(1e-4f, motionDt ?? measuredDt);
            if (updateSampleRate)
            {
                var sampleRate = 1f / dt;
                state.SmoothedSampleRateHz = state.SmoothedSampleRateHz * 0.85f + sampleRate * 0.15f;
            }
            var baseWidth = (float)Math.Max(0.35,
                strokeVisual.Stroke?.DrawingAttributes?.Width ?? inkCanvas.DefaultDrawingAttributes.Width);

            var rawX = (float)point.X;
            var rawY = (float)point.Y;
            var dx = rawX - state.LastRawX;
            var dy = rawY - state.LastRawY;
            var dist = (float)Math.Sqrt(dx * dx + dy * dy);
            var speed = Math.Max(0f, motionSpeed ?? dist / dt);

            var filteredX = state.FilterX.Filter(rawX, dt, speed);
            var filteredY = state.FilterY.Filter(rawY, dt, speed);

            rawPressure = RealtimeClamp(rawPressure, 0f, 1f);
            if (Math.Abs(rawPressure - 0.5f) > 0.02f)
                state.SawPressureVariation = true;
            var usePressure = state.SawPressureVariation && rawPressure > 0f;

            var width = baseWidth;
            if (usePressure)
                width *= 0.25f + 0.75f * rawPressure;
            var speedNormalization = 1800f + state.SmoothedSampleRateHz * 3.5f;
            width *= RealtimeClamp(1.15f - (speed / speedNormalization), 0.45f, 1.25f);
            var speedPressure = WidthToPressure(width, baseWidth);

            var pressure = usePressure
                ? ((1f - mix) * rawPressure + mix * speedPressure)
                : speedPressure;
            pressure = RealtimeClamp(pressure, 0.08f, 1f);
            pressure = state.FilterPressure.Filter(pressure, dt, speed);

            var minDist = GetRealtimeBrushTipMinDistance(state.SmoothedSampleRateHz);
            if (dist < minDist && state.HasSeed && strokeId == MouseRealtimeStrokeId)
            {
                state.LastRawX = rawX;
                state.LastRawY = rawY;
                state.LastTimestampMs = nowMs;
                return true;
            }

            if (!state.HasSeed)
            {
                state.HasSeed = true;
                state.LastSmoothX = filteredX;
                state.LastSmoothY = filteredY;
                state.LastSmoothPressure = pressure;
                strokeVisual.Add(new StylusPoint(filteredX, filteredY, pressure));
            }
            else if (useMidPointChain)
            {
                // Stylus/mouse keep midpoint chain to reduce jaggedness.
                var midX = (state.LastSmoothX + filteredX) * 0.5f;
                var midY = (state.LastSmoothY + filteredY) * 0.5f;
                var midPressure = (state.LastSmoothPressure + pressure) * 0.5f;
                strokeVisual.Add(new StylusPoint(midX, midY, midPressure));
                state.LastSmoothX = filteredX;
                state.LastSmoothY = filteredY;
                state.LastSmoothPressure = pressure;
            }
            else
            {
                // TouchVelocity already pre-interpolates geometrically; avoid another midpoint expansion.
                strokeVisual.Add(new StylusPoint(filteredX, filteredY, pressure));
                state.LastSmoothX = filteredX;
                state.LastSmoothY = filteredY;
                state.LastSmoothPressure = pressure;
            }

            state.LastRawX = rawX;
            state.LastRawY = rawY;
            state.LastTimestampMs = nowMs;
            return true;
        }

        private bool TryAppendRealtimeVelocityBrushTipInterpolatedPoints(StrokeVisual strokeVisual, int strokeId, Point point, float rawPressure = 0.5f)
        {
            if (!_realtimeBrushTipStates.TryGetValue(strokeId, out var state))
                return TryAppendRealtimeVelocityBrushTipPoint(strokeVisual, strokeId, point, rawPressure);

            var startedAt = RealtimeInkPerformanceMonitor.IsDebugLoggingEnabled ? Stopwatch.GetTimestamp() : 0L;
            var initialPointCount = strokeVisual?.Stroke?.StylusPoints.Count ?? 0;
            try
            {
                var appended = false;
                if (!state.HasTouchPoint)
                {
                    state.HasTouchPoint = true;
                    state.LastTouchPoint = point;
                    state.LastTouchInputTimestampTicks = Stopwatch.GetTimestamp();
                    return TryAppendRealtimeVelocityBrushTipPointCore(strokeVisual, strokeId, point, rawPressure);
                }

                var isTouchVelocity = _activeRealtimeTouchStrokeIds.Contains(strokeId);
                if (isTouchVelocity)
                {
                    var touchInputTimestampTicks = Stopwatch.GetTimestamp();
                    var eventDt = state.LastTouchInputTimestampTicks > 0
                        ? Math.Max(
                            0.001f,
                            (float)((touchInputTimestampTicks - state.LastTouchInputTimestampTicks)
                                    / (double)Stopwatch.Frequency))
                        : 1f / 60f;
                    var eventChord = point - state.LastTouchPoint;
                    if (eventChord.Length < 0.1)
                    {
                        UpdateTouchInterpolationState(state, point);
                        state.LastTouchInputTimestampTicks = touchInputTimestampTicks;
                        return false;
                    }

                    var eventSpeed = (float)(eventChord.Length / eventDt);
                    GetTouchVelocityInterpolationParams(
                        state,
                        point,
                        eventSpeed,
                        out var interpolationSpacing,
                        out var maxSteps);
                    var stepCount = GetTouchInterpolationStepCount(
                        eventChord.Length,
                        interpolationSpacing,
                        maxSteps);
                    var pointDt = eventDt / stepCount;
                    var sampleRate = 1f / eventDt;
                    state.SmoothedSampleRateHz = state.SmoothedSampleRateHz * 0.85f + sampleRate * 0.15f;
                    for (var stepIndex = 1; stepIndex <= stepCount; stepIndex++)
                    {
                        var interpolatedPoint = GetTouchInterpolationPoint(
                            state,
                            point,
                            stepIndex,
                            stepCount);
                        appended |= TryAppendRealtimeVelocityBrushTipPointCore(
                            strokeVisual,
                            strokeId,
                            interpolatedPoint,
                            rawPressure,
                            eventSpeed,
                            pointDt,
                            false,
                            false);
                    }
                    UpdateTouchInterpolationState(state, point);
                    state.LastTouchInputTimestampTicks = touchInputTimestampTicks;
                }
                else
                {
                    foreach (var p in InterpolateTouchPoints(state, point))
                    {
                        appended |= TryAppendRealtimeVelocityBrushTipPointCore(
                            strokeVisual,
                            strokeId,
                            p,
                            rawPressure);
                    }
                    UpdateTouchInterpolationState(state, point);
                }
                return appended;
            }
            finally
            {
                if (startedAt != 0L)
                {
                    var finalPointCount = strokeVisual?.Stroke?.StylusPoints.Count ?? initialPointCount;
                    RealtimeInkPerformanceMonitor.RecordInputEvent(
                        strokeVisual,
                        1,
                        Math.Max(0, finalPointCount - initialPointCount),
                        Stopwatch.GetTimestamp() - startedAt);
                }
            }
        }

        /// <summary>
        /// 保存画布上的非笔画元素（如图片、媒体元素等）
        /// </summary>
        /// <returns>返回保存的非笔画元素列表</returns>
        private List<UIElement> PreserveNonStrokeElements()
        {
            var preservedElements = new List<UIElement>();

            // 遍历inkCanvas的所有子元素，创建副本而不是直接引用
            for (int i = inkCanvas.Children.Count - 1; i >= 0; i--)
            {
                var child = inkCanvas.Children[i];
                if (child is FrameworkElement sceneChild
                    && (IsSecAgentEditableSceneElement(sceneChild) || IsSecAgentEditableSceneGroup(sceneChild)))
                    continue;

                // 保存图片、媒体元素等非笔画相关的UI元素
                if (child is Image || child is MediaElement || child is CanvasMediaControl ||
                    (child is Border border && border.Name != "EraserOverlayCanvas" &&
                     !string.Equals(child.GetType().FullName, "Ink_Canvas.SecAgent.Plugin.SvgSceneElement", StringComparison.Ordinal) &&
                     !string.Equals(child.GetType().FullName, "Ink_Canvas.SecAgent.Plugin.SvgSceneGroup", StringComparison.Ordinal)))
                {
                    // CanvasMediaControl 直接保留原始引用，避免克隆导致播放状态丢失
                    if (child is CanvasMediaControl)
                    {
                        preservedElements.Add(child);
                        continue;
                    }

                    // 创建元素的深拷贝，避免直接引用导致的问题
                    var clonedElement = CloneUIElement(child);
                    if (clonedElement != null)
                    {
                        preservedElements.Add(clonedElement);
                    }
                }
            }

            return preservedElements;
        }

        /// <summary>
        /// 克隆UI元素，创建深拷贝
        /// </summary>
        private UIElement CloneUIElement(UIElement originalElement)
        {
            try
            {
                if (originalElement is Image originalImage)
                {
                    var clonedImage = new Image();

                    // 复制图片源
                    if (originalImage.Source is BitmapSource bitmapSource)
                    {
                        clonedImage.Source = bitmapSource;
                    }

                    // 复制属性
                    clonedImage.Width = originalImage.Width;
                    clonedImage.Height = originalImage.Height;
                    clonedImage.Stretch = originalImage.Stretch;
                    clonedImage.StretchDirection = originalImage.StretchDirection;
                    clonedImage.Name = originalImage.Name;
                    clonedImage.IsHitTestVisible = originalImage.IsHitTestVisible;
                    clonedImage.Focusable = originalImage.Focusable;
                    clonedImage.Cursor = originalImage.Cursor;
                    clonedImage.IsManipulationEnabled = originalImage.IsManipulationEnabled;

                    // 复制位置
                    InkCanvas.SetLeft(clonedImage, InkCanvas.GetLeft(originalImage));
                    InkCanvas.SetTop(clonedImage, InkCanvas.GetTop(originalImage));

                    // 复制变换
                    if (originalImage.RenderTransform != null)
                    {
                        clonedImage.RenderTransform = originalImage.RenderTransform.Clone();
                    }

                    return clonedImage;
                }
                else if (originalElement is CanvasMediaControl originalMediaControl)
                {
                    var clonedMediaControl = new CanvasMediaControl
                    {
                        Width = originalMediaControl.Width,
                        Height = originalMediaControl.Height,
                        Name = originalMediaControl.Name,
                        IsHitTestVisible = originalMediaControl.IsHitTestVisible,
                        Focusable = originalMediaControl.Focusable,
                        RenderTransform = originalMediaControl.RenderTransform?.Clone()
                    };

                    if (!string.IsNullOrWhiteSpace(originalMediaControl.SourcePath))
                    {
                        clonedMediaControl.Initialize(originalMediaControl.SourcePath, originalMediaControl.DisplayName);
                    }
                    clonedMediaControl.SetPlaybackRate(originalMediaControl.PlaybackRate);
                    clonedMediaControl.SetVolumeLevel(originalMediaControl.VolumeLevel);
                    var playbackPosition = originalMediaControl.GetPlaybackPositionOrNull();
                    if (playbackPosition.HasValue)
                    {
                        clonedMediaControl.SetPlaybackPosition(playbackPosition.Value);
                    }

                    InkCanvas.SetLeft(clonedMediaControl, InkCanvas.GetLeft(originalMediaControl));
                    InkCanvas.SetTop(clonedMediaControl, InkCanvas.GetTop(originalMediaControl));

                    return clonedMediaControl;
                }
                else if (originalElement is MediaElement originalMedia)
                {
                    var clonedMedia = new MediaElement
                    {
                        Source = originalMedia.Source,
                        Width = originalMedia.Width,
                        Height = originalMedia.Height,
                        Name = originalMedia.Name,
                        IsHitTestVisible = originalMedia.IsHitTestVisible,
                        Focusable = originalMedia.Focusable,
                        RenderTransform = originalMedia.RenderTransform?.Clone()
                    };

                    // 复制位置
                    InkCanvas.SetLeft(clonedMedia, InkCanvas.GetLeft(originalMedia));
                    InkCanvas.SetTop(clonedMedia, InkCanvas.GetTop(originalMedia));

                    return clonedMedia;
                }
                else if (originalElement is Border originalBorder)
                {
                    var clonedBorder = new Border
                    {
                        Width = originalBorder.Width,
                        Height = originalBorder.Height,
                        Name = originalBorder.Name,
                        IsHitTestVisible = originalBorder.IsHitTestVisible,
                        Focusable = originalBorder.Focusable,
                        Background = originalBorder.Background,
                        BorderBrush = originalBorder.BorderBrush,
                        BorderThickness = originalBorder.BorderThickness,
                        CornerRadius = originalBorder.CornerRadius,
                        RenderTransform = originalBorder.RenderTransform?.Clone()
                    };

                    // 复制位置
                    InkCanvas.SetLeft(clonedBorder, InkCanvas.GetLeft(originalBorder));
                    InkCanvas.SetTop(clonedBorder, InkCanvas.GetTop(originalBorder));

                    return clonedBorder;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"克隆UI元素失败: {ex.Message}", LogHelper.LogType.Error);
            }

            return null;
        }

        /// <summary>
        /// 恢复之前保存的非笔画元素到画布
        /// </summary>
        private void RestoreNonStrokeElements(List<UIElement> preservedElements)
        {
            if (preservedElements == null) return;

            foreach (var element in preservedElements)
            {
                try
                {
                    // 由于现在使用的是克隆的元素，不需要检查Parent属性
                    inkCanvas.Children.Add(element);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"恢复非笔画元素失败: {ex.Message}", LogHelper.LogType.Error);
                }
            }
        }

        /// <summary>
        /// 多点触控模式切换按钮的鼠标抬起事件处理方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 切换多点触控模式和单点触控模式，包括以下步骤：
        /// 1. 如果当前处于多点触控模式，则切换到单点触控模式
        ///    - 移除手写笔和触摸事件处理程序
        ///    - 添加触摸事件处理程序
        ///    - 设置InkCanvas编辑模式为Ink（如果当前不是橡皮擦模式）
        ///    - 保存并恢复非笔画元素
        ///    - 设置isInMultiTouchMode为false
        /// 2. 如果当前处于单点触控模式，则切换到多点触控模式
        ///    - 添加手写笔事件处理程序
        ///    - 添加触摸事件处理程序
        ///    - 移除触摸事件处理程序
        ///    - 设置InkCanvas编辑模式为None（如果当前不是橡皮擦模式）
        ///    - 保存并恢复非笔画元素
        ///    - 设置isInMultiTouchMode为true
        /// </remarks>
        private void BorderMultiTouchMode_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isInMultiTouchMode)
            {
                inkCanvas.StylusDown -= MainWindow_StylusDown;
                inkCanvas.StylusMove -= MainWindow_StylusMove;
                inkCanvas.StylusUp -= MainWindow_StylusUp;
                inkCanvas.TouchDown -= MainWindow_TouchDown;
                inkCanvas.TouchDown += Main_Grid_TouchDown;
                if (inkCanvas.EditingMode != InkCanvasEditingMode.EraseByPoint
                    && inkCanvas.EditingMode != InkCanvasEditingMode.EraseByStroke)
                {
                    inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                }
                // 保存非笔画元素（如图片）
                var preservedElements = PreserveNonStrokeElements();
                inkCanvas.Children.Clear();
                // 恢复非笔画元素
                RestoreNonStrokeElements(preservedElements);
                isInMultiTouchMode = false;

                if (palmEraserWasEnabledBeforeMultiTouch)
                {
                    Settings.Canvas.EnablePalmEraser = true;
                    SaveSettingsToFile();
                }
            }
            else
            {

                inkCanvas.StylusDown += MainWindow_StylusDown;
                inkCanvas.StylusMove += MainWindow_StylusMove;
                inkCanvas.StylusUp += MainWindow_StylusUp;
                inkCanvas.TouchDown += MainWindow_TouchDown;
                inkCanvas.TouchDown -= Main_Grid_TouchDown;
                if (inkCanvas.EditingMode != InkCanvasEditingMode.EraseByPoint
                    && inkCanvas.EditingMode != InkCanvasEditingMode.EraseByStroke)
                {
                    inkCanvas.EditingMode = InkCanvasEditingMode.None;
                }
                // 保存非笔画元素（如图片）
                var preservedElements = PreserveNonStrokeElements();
                inkCanvas.Children.Clear();
                // 恢复非笔画元素
                RestoreNonStrokeElements(preservedElements);
                isInMultiTouchMode = true;

                palmEraserWasEnabledBeforeMultiTouch = Settings.Canvas.EnablePalmEraser;
                Settings.Canvas.EnablePalmEraser = false;
                SaveSettingsToFile();
            }
        }

        /// <summary>
        /// 主窗口的触摸按下事件处理方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">触摸事件参数</param>
        /// <remarks>
        /// 处理触摸按下事件，包括以下逻辑：
        /// 1. 如果当前处于橡皮擦模式或选择模式，则直接返回
        /// 2. 如果当前没有隐藏子面板，则隐藏子面板
        /// 3. 如果当前处于图形绘制模式，则：
        ///    - 设置InkCanvas编辑模式为None
        ///    - 设置触摸状态为按下
        ///    - 禁用浮动栏和黑板UI网格的命中测试
        ///    - 设置起始点坐标
        ///    - 直接返回
        /// 4. 否则，设置触摸按下点的编辑模式为None
        /// 5. 如果当前不是橡皮擦模式，则设置InkCanvas编辑模式为None
        /// </remarks>
        private void MainWindow_TouchDown(object sender, TouchEventArgs e)
        {
            // 视频展台特殊模式：所有触摸交给 VideoPresenterSpecialModeContainer 的 Manipulation 处理，
            // 不进入下面的 EditingMode 切换逻辑（避免把 Ink 切到 None 干扰预览绘制）。
            // 图形绘制模式例外：需要走正常绘制流程
            if (_isVideoPresenterSpecialMode && drawingShapeMode == 0) return;

            if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint
                || inkCanvas.EditingMode == InkCanvasEditingMode.EraseByStroke
                || inkCanvas.EditingMode == InkCanvasEditingMode.Select) return;

            if (!isHidingSubPanelsWhenInking)
            {
                isHidingSubPanelsWhenInking = true;
                HideSubPanels(); // 书写时自动隐藏二级菜单
            }

            if (drawingShapeMode != 0)
            {
                inkCanvas.EditingMode = InkCanvasEditingMode.None;

                isTouchDown = true;
                ViewboxFloatingBar.IsHitTestVisible = false;
                BlackboardUIGridForInkReplay.IsHitTestVisible = false;

                // 设置起始点
                if (NeedUpdateIniP()) iniP = e.GetTouchPoint(inkCanvas).Position;

                return;
            }

            // 只保留普通橡皮逻辑
            TouchDownPointsList[e.TouchDevice.Id] = InkCanvasEditingMode.None;
            if (inkCanvas.EditingMode != InkCanvasEditingMode.EraseByPoint
                && inkCanvas.EditingMode != InkCanvasEditingMode.EraseByStroke)
            {
                inkCanvas.EditingMode = InkCanvasEditingMode.None;
            }
        }

        /// <summary>
        /// 主窗口的手写笔按下事件处理方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">手写笔按下事件参数</param>
        /// <remarks>
        /// 处理手写笔按下事件，包括以下逻辑：
        /// 1. 检查手写笔点击是否发生在浮动栏区域，如果是则允许事件传播到浮动栏按钮并返回
        /// 2. 根据手写笔是否倒置自动切换橡皮擦/画笔模式：
        ///    - 如果手写笔倒置，设置编辑模式为EraseByPoint
        ///    - 如果手写笔正常：
        ///       - 如果当前处于图形绘制模式，设置编辑模式为None，设置触摸状态为按下，禁用浮动栏和黑板UI网格的命中测试，设置起始点坐标并返回
        ///       - 如果当前不是线擦模式，设置编辑模式为Ink
        ///       - 否则，保持当前线擦模式
        /// 3. 捕获手写笔输入
        /// 4. 禁用浮动栏和黑板UI网格的命中测试
        /// 5. 根据编辑模式设置光标
        /// 6. 如果当前处于橡皮擦模式或选择模式，则直接返回
        /// 7. 设置触摸按下点的编辑模式为None
        /// </remarks>
        private void MainWindow_StylusDown(object sender, StylusDownEventArgs e)
        {
            if (IsTouchStylusDevice(e.StylusDevice))
                return;

            // 检查手写笔点击是否发生在浮动栏区域，如果是则允许事件传播到浮动栏按钮
            var stylusPoint = e.GetPosition(this);
            var floatingBarBounds = ViewboxFloatingBar.TransformToAncestor(this).TransformBounds(
                new Rect(0, 0, ViewboxFloatingBar.ActualWidth, ViewboxFloatingBar.ActualHeight));

            // 如果手写笔点击发生在浮动栏区域，不阻止事件传播，让浮动栏按钮能够接收手写笔事件
            if (floatingBarBounds.Contains(stylusPoint))
            {
                // 不设置 ViewboxFloatingBar.IsHitTestVisible = false，让浮动栏按钮能够接收手写笔事件
                return;
            }

            if (IsCurrentPageFrozen)
            {
                TryBlockFrozenPageMutation("书写或擦除");
                e.Handled = true;
                return;
            }


            // 根据是否为笔尾自动切换橡皮擦/画笔模式
            if (e.StylusDevice.Inverted)
            {
                inkCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
            }
            else
            {
                if (drawingShapeMode != 0)
                {
                    inkCanvas.EditingMode = InkCanvasEditingMode.None;

                    isTouchDown = true;
                    ViewboxFloatingBar.IsHitTestVisible = false;
                    BlackboardUIGridForInkReplay.IsHitTestVisible = false;

                    // 设置起始点
                    if (NeedUpdateIniP()) iniP = e.GetPosition(inkCanvas);

                    return;
                }
                if (inkCanvas.EditingMode != InkCanvasEditingMode.EraseByStroke)
                {
                    // Ink 模式下 InkCanvas 内建采集笔画（TouchDownPointsList[stylusId]=None 标记归属 InkCanvas），
                    // 实时笔锋模式走手动采集，两者必须同步。
                    inkCanvas.EditingMode = ShouldUseRealtimeVelocityBrushTip()
                        ? InkCanvasEditingMode.None
                        : InkCanvasEditingMode.Ink;
                }
                else
                {
                    LogHelper.WriteLogToFile("保持当前线擦模式");
                }
            }
            inkCanvas.CaptureStylus();
            ViewboxFloatingBar.IsHitTestVisible = false;
            BlackboardUIGridForInkReplay.IsHitTestVisible = false;

            SetCursorBasedOnEditingMode(inkCanvas);

            if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint
                || inkCanvas.EditingMode == InkCanvasEditingMode.EraseByStroke
                || inkCanvas.EditingMode == InkCanvasEditingMode.Select) return;

            var stylusId = e.StylusDevice.Id;
            if (ShouldUseRealtimeVelocityBrushTip()
                && inkCanvas.EditingMode != InkCanvasEditingMode.EraseByPoint
                && inkCanvas.EditingMode != InkCanvasEditingMode.EraseByStroke
                && inkCanvas.EditingMode != InkCanvasEditingMode.Select)
            {
                if (StrokeVisualList.TryGetValue(stylusId, out var staleVisual))
                    RealtimeInkFrameScheduler.Cancel(staleVisual);
                if (VisualCanvasList.TryGetValue(stylusId, out var staleCanvas) && inkCanvas.Children.Contains(staleCanvas))
                    inkCanvas.Children.Remove(staleCanvas);
                StrokeVisualList.Remove(stylusId);
                VisualCanvasList.Remove(stylusId);
                TouchDownPointsList.Remove(stylusId);
                CleanupRealtimeBrushTipState(stylusId);

                inkCanvas.EditingMode = InkCanvasEditingMode.None;
                inkCanvas.CaptureStylus();
                ViewboxFloatingBar.IsHitTestVisible = false;
                BlackboardUIGridForInkReplay.IsHitTestVisible = false;
                SetCursorBasedOnEditingMode(inkCanvas);

                var p = e.GetPosition(inkCanvas);
                _activeRealtimeStylusStrokeIds.Add(stylusId);
                BeginTouchInkInput();
                CancelPauseStraightenTimer(stylusId);
                InitializeRealtimeBrushTipState(stylusId, e);
                var sv = GetStrokeVisual(stylusId);
                RealtimeInkPerformanceMonitor.BeginStroke(sv, RealtimeInkInputKind.Stylus);
                TryAppendRealtimeVelocityBrushTipInterpolatedPoints(sv, stylusId, p);
                RealtimeInkFrameScheduler.RequestRedraw(sv);
                _pauseStraightenInkModeStartPos = p;
                _pauseStraightenInkModeTracking = true;
                TouchDownPointsList[stylusId] = InkCanvasEditingMode.None;
                e.Handled = true;
                return;
            }

            InitializeRealtimeBrushTipState(e.StylusDevice.Id, e);
            CancelPauseStraightenTimer(e.StylusDevice.Id);
            _pauseStraightenInkModeStartPos = e.GetPosition(inkCanvas);
            _pauseStraightenInkModeTracking = true;
            TouchDownPointsList[e.StylusDevice.Id] = InkCanvasEditingMode.None;
        }

        /// <summary>
        /// 主窗口的手写笔抬起事件处理方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">手写笔事件参数</param>
        /// <remarks>
        /// 处理手写笔抬起事件，包括以下逻辑：
        /// 1. 如果当前处于图形绘制模式：
        ///    - 重置触摸状态
        ///    - 启用浮动栏和黑板UI网格的命中测试
        ///    - 对于双曲线等需要多步绘制的图形，根据当前步骤决定是进入下一步还是完成绘制
        ///    - 对于其他单步绘制的图形，直接完成绘制
        ///    - 直接返回
        /// 2. 否则，尝试获取并处理笔画：
        ///    - 获取笔画视觉对象的笔画
        ///    - 如果笔画不为空，将其添加到InkCanvas，移除视觉画布，并触发笔画收集事件
        ///    - 如果笔画为空，仅移除视觉画布
        /// 3. 清理相关资源：
        ///    - 从StrokeVisualList、VisualCanvasList和TouchDownPointsList中移除当前手写笔设备ID
        ///    - 如果列表为空，清除所有手写笔预览相关的Canvas并清空列表
        /// 4. 释放手写笔捕获
        /// 5. 启用浮动栏和黑板UI网格的命中测试
        /// 6. 根据编辑模式设置光标
        /// </remarks>
        private async void MainWindow_StylusUp(object sender, StylusEventArgs e)
        {
            if (IsTouchStylusDevice(e.StylusDevice))
                return;

            var stylusId = e.StylusDevice.Id;
            if (_activeRealtimeStylusStrokeIds.Contains(stylusId))
            {
                StrokeVisual sv = null;
                try
                {
                    sv = GetStrokeVisual(stylusId);
                    RealtimeInkFrameScheduler.Flush(sv);
                    var stroke = sv?.Stroke;
                    if (stroke != null)
                    {
                        if (!stroke.ContainsPropertyData(RealtimeVelocityBrushTipAppliedGuid))
                            stroke.AddPropertyData(RealtimeVelocityBrushTipAppliedGuid, true);
                        inkCanvas.Strokes.Add(stroke);
                        inkCanvas_StrokeCollected(inkCanvas, new InkCanvasStrokeCollectedEventArgs(stroke));
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"MainWindow_StylusUp 实时笔锋出错: {ex}", LogHelper.LogType.Error);
                    Label.Content = ex.ToString();
                }
                finally
                {
                    RealtimeInkFrameScheduler.Cancel(sv);
                    if (VisualCanvasList.TryGetValue(stylusId, out var visualCanvas) && inkCanvas.Children.Contains(visualCanvas))
                        inkCanvas.Children.Remove(visualCanvas);
                    StrokeVisualList.Remove(stylusId);
                    VisualCanvasList.Remove(stylusId);
                    TouchDownPointsList.Remove(stylusId);
                    CleanupRealtimeBrushTipState(stylusId);
                    CancelPauseStraightenTimer(stylusId);
                    CancelPauseStraightenTimer(-200001);
                    _pauseStraightenInkModeTracking = false;
                    _activeRealtimeStylusStrokeIds.Remove(stylusId);
                    RealtimeInkPerformanceMonitor.EndStroke(sv);
                    EndTouchInkInputIfIdle();
                    inkCanvas.ReleaseStylusCapture();
                    ViewboxFloatingBar.IsHitTestVisible = true;
                    BlackboardUIGridForInkReplay.IsHitTestVisible = true;
                    SetCursorBasedOnEditingMode(inkCanvas);
                    e.Handled = true;
                }
                return;
            }

            if (drawingShapeMode != 0)
            {
                // 重置触摸状态
                isTouchDown = false;
                ViewboxFloatingBar.IsHitTestVisible = true;
                BlackboardUIGridForInkReplay.IsHitTestVisible = true;

                // 对于双曲线等需要多步绘制的图形，手写笔抬起时应该进入下一步
                if (drawingShapeMode == 24 || drawingShapeMode == 25)
                {
                    if (drawMultiStepShapeCurrentStep == 0)
                    {
                        // 第一笔完成，进入第二笔
                        drawMultiStepShapeCurrentStep = 1;
                    }
                    else
                    {
                        // 第二笔完成，完成绘制
                        var mouseArgs = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                        {
                            RoutedEvent = MouseLeftButtonUpEvent,
                            Source = inkCanvas
                        };
                        inkCanvas_MouseUp(inkCanvas, mouseArgs);
                    }
                }
                else
                {
                    // 其他单步绘制的图形，手写笔抬起时完成绘制
                    var mouseArgs = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                    {
                        RoutedEvent = MouseLeftButtonUpEvent,
                        Source = inkCanvas
                    };
                    inkCanvas_MouseUp(inkCanvas, mouseArgs);
                }

                return;
            }

            try
            {
                var strokeVisual = GetStrokeVisual(e.StylusDevice.Id);
                RealtimeInkFrameScheduler.Flush(strokeVisual);
                var stroke = strokeVisual.Stroke;

                if (stroke != null)
                {
                    inkCanvas.Strokes.Add(stroke);
                    await Task.Delay(5);
                    inkCanvas.Children.Remove(GetVisualCanvas(e.StylusDevice.Id));

                    inkCanvas_StrokeCollected(inkCanvas,
                    new InkCanvasStrokeCollectedEventArgs(stroke));
                }
                else
                {
                    await Task.Delay(5);
                    inkCanvas.Children.Remove(GetVisualCanvas(e.StylusDevice.Id));
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"MainWindow_StylusUp 出错: {ex}", LogHelper.LogType.Error);
                Label.Content = ex.ToString();
            }

            try
            {
                if (StrokeVisualList.TryGetValue(e.StylusDevice.Id, out var strokeVisual))
                    RealtimeInkFrameScheduler.Cancel(strokeVisual);
                StrokeVisualList.Remove(e.StylusDevice.Id);
                VisualCanvasList.Remove(e.StylusDevice.Id);
                TouchDownPointsList.Remove(e.StylusDevice.Id);
                CleanupRealtimeBrushTipState(e.StylusDevice.Id);
                CancelPauseStraightenTimer(e.StylusDevice.Id);
                CancelPauseStraightenTimer(-200001);
                _pauseStraightenInkModeTracking = false;
                if (StrokeVisualList.Count == 0 || VisualCanvasList.Count == 0 || TouchDownPointsList.Count == 0)
                {
                    // 只清除手写笔预览相关的Canvas，不清除所有子元素
                    foreach (var canvas in VisualCanvasList.Values.ToList())
                    {
                        if (inkCanvas.Children.Contains(canvas))
                        {
                            inkCanvas.Children.Remove(canvas);
                        }
                    }
                    foreach (var pendingStrokeVisual in StrokeVisualList.Values.ToList())
                        RealtimeInkFrameScheduler.Cancel(pendingStrokeVisual);
                    StrokeVisualList.Clear();
                    VisualCanvasList.Clear();
                    TouchDownPointsList.Clear();
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            inkCanvas.ReleaseStylusCapture();
            ViewboxFloatingBar.IsHitTestVisible = true;
            BlackboardUIGridForInkReplay.IsHitTestVisible = true;
            SetCursorBasedOnEditingMode(inkCanvas);
        }

        /// <summary>
        /// 主窗口的手写笔移动事件处理方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">手写笔事件参数</param>
        /// <remarks>
        /// 处理手写笔移动事件，包括以下逻辑：
        /// 1. 如果当前处于图形绘制模式且触摸状态为按下：
        ///    - 获取手写笔在InkCanvas上的位置
        ///    - 调用MouseTouchMove方法处理移动
        ///    - 直接返回
        /// 2. 如果触摸按下点的编辑模式不是None，则直接返回
        /// 3. 尝试检查手写笔按钮状态，如果第二个按钮被按下，则直接返回
        /// 4. 否则，获取笔画视觉对象，添加手写笔点，并重新绘制
        /// 5. 捕获并忽略所有异常
        /// </remarks>
        private void MainWindow_StylusMove(object sender, StylusEventArgs e)
        {
            try
            {
                if (IsTouchStylusDevice(e.StylusDevice))
                    return;

                if (drawingShapeMode != 0)
                {
                    if (isTouchDown)
                    {
                        Point stylusPoint = e.GetPosition(inkCanvas);
                        MouseTouchMove(stylusPoint);
                    }
                    return;
                }

                var stylusId = e.StylusDevice.Id;
                if (_activeRealtimeStylusStrokeIds.Contains(stylusId))
                {
                    try
                    {
                        var p = e.GetPosition(inkCanvas);
                        var sv = GetStrokeVisual(stylusId);
                        if (TryAppendRealtimeVelocityBrushTipInterpolatedPoints(sv, stylusId, p))
                            RealtimeInkFrameScheduler.RequestRedraw(sv);
                        ResetPauseStraightenTimer(stylusId);
                        e.Handled = true;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(ex);
                    }
                    return;
                }

                if (GetTouchDownPointsList(e.StylusDevice.Id) != InkCanvasEditingMode.None)
                {
                    // Regular Ink mode — InkCanvas builds the stroke internally.
                    // Track position for pause-straighten.
                    if (inkCanvas.EditingMode == InkCanvasEditingMode.Ink && drawingShapeMode == 0)
                        ResetPauseStraightenTimerInkMode(e.GetPosition(inkCanvas));
                    return;
                }
                try
                {
                    if (e.StylusDevice.StylusButtons[1].StylusButtonState == StylusButtonState.Down) return;
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }


                var strokeVisual = GetStrokeVisual(e.StylusDevice.Id);
                var isHandledByRealtime = TryAppendRealtimeVelocityBrushTipPoints(strokeVisual, e);
                if (!isHandledByRealtime)
                {
                    var stylusPointCollection = e.GetStylusPoints(this);
                    foreach (var stylusPoint in stylusPointCollection)
                        strokeVisual.Add(new StylusPoint(stylusPoint.X, stylusPoint.Y, stylusPoint.PressureFactor));
                }

                ResetPauseStraightenTimer(e.StylusDevice.Id);

                RealtimeInkFrameScheduler.RequestRedraw(strokeVisual);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }

        /// <summary>
        /// 获取笔画视觉对象方法
        /// </summary>
        /// <param name="id">设备ID</param>
        /// <returns>返回笔画视觉对象</returns>
        /// <remarks>
        /// 根据设备ID获取笔画视觉对象，如果不存在则创建新的：
        /// 1. 尝试从StrokeVisualList中获取笔画视觉对象
        /// 2. 如果不存在，创建新的StrokeVisual实例，使用InkCanvas的默认绘制属性的克隆
        /// 3. 将新的笔画视觉对象添加到StrokeVisualList
        /// 4. 创建新的VisualCanvas实例，将其设置为笔画视觉对象的视觉画布
        /// 5. 将新的视觉画布添加到VisualCanvasList和InkCanvas的子元素中
        /// 6. 返回笔画视觉对象
        /// </remarks>
        private StrokeVisual GetStrokeVisual(int id)
        {
            if (StrokeVisualList.TryGetValue(id, out var visual)) return visual;

            var strokeVisual = new StrokeVisual(inkCanvas.DefaultDrawingAttributes.Clone());
            StrokeVisualList[id] = strokeVisual;
            var visualCanvas = new VisualCanvas();
            strokeVisual.SetVisualCanvas(visualCanvas);
            VisualCanvasList[id] = visualCanvas;
            inkCanvas.Children.Add(visualCanvas);

            return strokeVisual;
        }

        /// <summary>
        /// 获取视觉画布方法
        /// </summary>
        /// <param name="id">设备ID</param>
        /// <returns>返回视觉画布对象，如果不存在则返回null</returns>
        /// <remarks>
        /// 根据设备ID从VisualCanvasList中获取视觉画布对象
        /// </remarks>
        private VisualCanvas GetVisualCanvas(int id)
        {
            return VisualCanvasList.TryGetValue(id, out var visualCanvas) ? visualCanvas : null;
        }

        private void ResetPauseStraightenTimer(int stylusId)
        {
            if (!Settings.Canvas.PauseStraightenLine) return;
            Debug.WriteLine($"ResetPauseStraightenTimer: id={stylusId}");
            if (_pauseStraightenTimers.TryGetValue(stylusId, out var existing))
            {
                existing.Stop();
                existing.Interval = TimeSpan.FromMilliseconds(Settings.Canvas.PauseStraightenDelay);
                existing.Start();
                return;
            }
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Settings.Canvas.PauseStraightenDelay) };
            var capturedId = stylusId;
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                _pauseStraightenTimers.Remove(capturedId);
                Debug.WriteLine($"PauseStraightenTimer fired: id={capturedId}");
                TryPauseStraighten(capturedId);
            };
            _pauseStraightenTimers[stylusId] = timer;
            timer.Start();
        }

        private void ResetPauseStraightenTimerInkMode(Point currentPos)
        {
            if (!Settings.Canvas.PauseStraightenLine) return;
            const int inkModeId = -200001;
            _pauseStraightenInkModeLastPos = currentPos;
            if (_pauseStraightenTimers.TryGetValue(inkModeId, out var existing))
            {
                existing.Stop();
                existing.Interval = TimeSpan.FromMilliseconds(Settings.Canvas.PauseStraightenDelay);
                existing.Start();
                return;
            }
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Settings.Canvas.PauseStraightenDelay) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                _pauseStraightenTimers.Remove(inkModeId);
                TryPauseStraightenInkMode();
            };
            _pauseStraightenTimers[inkModeId] = timer;
            timer.Start();
        }

        private Point _pauseStraightenInkModeLastPos;
        private Point _pauseStraightenInkModeStartPos;
        private bool _pauseStraightenInkModeTracking;

        private void TryPauseStraightenInkMode()
        {
            if (!Settings.Canvas.PauseStraightenLine) return;
            if (!_pauseStraightenInkModeTracking) return;
            if (inkCanvas.EditingMode != InkCanvasEditingMode.Ink) return;
            if (drawingShapeMode != 0) return;

            var start = _pauseStraightenInkModeStartPos;
            var end = _pauseStraightenInkModeLastPos;
            double lineLength = GetDistance(start, end);
            if (lineLength < 2) return;

            // Commit current stroke by briefly switching mode
            inkCanvas.EditingMode = InkCanvasEditingMode.None;
            inkCanvas.EditingMode = InkCanvasEditingMode.Ink;

            // The just-committed stroke should now be last in inkCanvas.Strokes
            if (inkCanvas.Strokes.Count == 0) return;
            var stroke = inkCanvas.Strokes[inkCanvas.Strokes.Count - 1];
            if (stroke.StylusPoints.Count < 2) return;

            var newPoints = new StylusPointCollection();
            newPoints.Add(new StylusPoint(start.X, start.Y, 0.5f));
            if (lineLength > 100)
            {
                newPoints.Add(new StylusPoint(start.X + (end.X - start.X) / 3.0, start.Y + (end.Y - start.Y) / 3.0, 0.5f));
                newPoints.Add(new StylusPoint(start.X + (end.X - start.X) * 2.0 / 3.0, start.Y + (end.Y - start.Y) * 2.0 / 3.0, 0.5f));
            }
            newPoints.Add(new StylusPoint(end.X, end.Y, 0.5f));
            stroke.StylusPoints = newPoints;

            _pauseStraightenInkModeTracking = false;
        }

        private void CancelPauseStraightenTimer(int stylusId)
        {
            if (_pauseStraightenTimers.TryGetValue(stylusId, out var timer))
            {
                timer.Stop();
                _pauseStraightenTimers.Remove(stylusId);
            }
        }

        private void TryPauseStraighten(int stylusId)
        {
            if (!Settings.Canvas.PauseStraightenLine) { Debug.WriteLine("PauseStraighten: disabled"); return; }
            var strokeVisual = StrokeVisualList.TryGetValue(stylusId, out var sv) ? sv : null;
            if (strokeVisual?.Stroke == null) { Debug.WriteLine($"PauseStraighten: no stroke for id={stylusId}"); return; }
            var stroke = strokeVisual.Stroke;
            Debug.WriteLine($"PauseStraighten: points={stroke.StylusPoints.Count}");
            if (stroke.StylusPoints.Count < 2) return;

            var start = stroke.StylusPoints[0].ToPoint();
            var end = stroke.StylusPoints[stroke.StylusPoints.Count - 1].ToPoint();
            double lineLength = GetDistance(start, end);
            Debug.WriteLine($"PauseStraighten: length={lineLength:F1}, STRAIGHTENING!");

            var newPoints = new StylusPointCollection();
            newPoints.Add(new StylusPoint(start.X, start.Y, 0.5f));
            if (lineLength > 100)
            {
                newPoints.Add(new StylusPoint(start.X + (end.X - start.X) / 3.0, start.Y + (end.Y - start.Y) / 3.0, 0.5f));
                newPoints.Add(new StylusPoint(start.X + (end.X - start.X) * 2.0 / 3.0, start.Y + (end.Y - start.Y) * 2.0 / 3.0, 0.5f));
            }
            newPoints.Add(new StylusPoint(end.X, end.Y, 0.5f));
            stroke.StylusPoints = newPoints;
            RealtimeInkFrameScheduler.Flush(strokeVisual, true);
        }

        /// <summary>
        /// 获取触摸按下点的编辑模式方法
        /// </summary>
        /// <param name="id">设备ID</param>
        /// <returns>返回触摸按下点的编辑模式，如果不存在则返回InkCanvas的当前编辑模式</returns>
        /// <remarks>
        /// 根据设备ID从TouchDownPointsList中获取触摸按下点的编辑模式
        /// </remarks>
        private InkCanvasEditingMode GetTouchDownPointsList(int id)
        {
            return TouchDownPointsList.TryGetValue(id, out var inkCanvasEditingMode) ? inkCanvasEditingMode : inkCanvas.EditingMode;
        }

        /// <summary>
        /// 触摸按下点的编辑模式字典，键为设备ID，值为编辑模式
        /// </summary>
        private Dictionary<int, InkCanvasEditingMode> TouchDownPointsList { get; } =
            new Dictionary<int, InkCanvasEditingMode>();

        /// <summary>
        /// 笔画视觉对象字典，键为设备ID，值为笔画视觉对象
        /// </summary>
        private Dictionary<int, StrokeVisual> StrokeVisualList { get; } = new Dictionary<int, StrokeVisual>();
        /// <summary>
        /// 视觉画布字典，键为设备ID，值为视觉画布对象
        /// </summary>
        private Dictionary<int, VisualCanvas> VisualCanvasList { get; } = new Dictionary<int, VisualCanvas>();

        #endregion




        private Point iniP = new Point(0, 0);

        /// <summary>
        /// 主网格的触摸按下事件处理方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">触摸事件参数</param>
        /// <remarks>
        /// 处理主网格的触摸按下事件，包括以下逻辑：
        /// 1. 检查触摸是否发生在浮动栏区域，如果是则允许事件传播到浮动栏按钮并返回
        /// 2. 根据编辑模式设置光标
        /// 3. 捕获触摸输入
        /// 4. 如果当前处于点擦模式，则直接返回
        /// 5. 如果当前处于图形绘制模式：
        ///    - 设置编辑模式为None
        ///    - 设置触摸状态为按下
        ///    - 禁用浮动栏和黑板UI网格的命中测试
        ///    - 设置起始点坐标
        ///    - 直接返回
        /// 6. 如果当前处于选择模式、墨水模式或线擦模式，则直接返回
        /// 7. 如果当前不是橡皮擦模式，则设置编辑模式为Ink
        /// </remarks>
        private void Main_Grid_TouchDown(object sender, TouchEventArgs e)
        {
            // 视频展台特殊模式：不在此处切换 EditingMode，
            // PreviewTouchDown 已临时切到 None 抑制 InkCanvas 框选/绘制；
            // 这里再切会覆盖 None → Ink，导致特殊模式下仍画出墨迹（Q7 真正根因）。
            // 图形绘制模式例外：需要走正常绘制流程
            if (_isVideoPresenterSpecialMode && drawingShapeMode == 0)
            {
                return;
            }

            // 检查触摸是否发生在浮动栏区域，如果是则允许事件传播到浮动栏按钮
            var touchPoint = e.GetTouchPoint(this);
            var floatingBarBounds = ViewboxFloatingBar.TransformToAncestor(this).TransformBounds(
                new Rect(0, 0, ViewboxFloatingBar.ActualWidth, ViewboxFloatingBar.ActualHeight));

            // 如果触摸发生在浮动栏区域，不阻止事件传播，让浮动栏按钮能够接收触摸事件
            if (floatingBarBounds.Contains(touchPoint.Position))
            {
                // 不设置 ViewboxFloatingBar.IsHitTestVisible = false，让浮动栏按钮能够接收触摸事件
                return;
            }

            if (IsCurrentPageFrozen)
            {
                TryBlockFrozenPageMutation("修改冻结页面");
                e.Handled = true;
                return;
            }

            SetCursorBasedOnEditingMode(inkCanvas);
            inkCanvas.CaptureTouch(e.TouchDevice);

            if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint)
            {
                return;
            }
            if (drawingShapeMode != 0)
            {
                inkCanvas.EditingMode = InkCanvasEditingMode.None;

                // 设置触摸状态，类似鼠标事件处理
                isTouchDown = true;
                ViewboxFloatingBar.IsHitTestVisible = false;
                BlackboardUIGridForInkReplay.IsHitTestVisible = false;

                // 设置起始点
                if (NeedUpdateIniP()) iniP = e.GetTouchPoint(inkCanvas).Position;

                return;
            }
            if (inkCanvas.EditingMode == InkCanvasEditingMode.Select)
            {
                return;
            }
            if (inkCanvas.EditingMode == InkCanvasEditingMode.Ink)
            {
                return;
            }
            if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByStroke)
            {
                return;
            }
            if (inkCanvas.EditingMode != InkCanvasEditingMode.EraseByPoint
                && inkCanvas.EditingMode != InkCanvasEditingMode.EraseByStroke)
            {
                inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
            }
        }

        /// <summary>
        /// 获取触摸边界宽度方法
        /// </summary>
        /// <param name="e">触摸事件参数</param>
        /// <returns>返回触摸边界宽度</returns>
        /// <remarks>
        /// 手掌擦阈值与特殊屏 <c>TouchMultiplier</c> 在激活逻辑中单独参与计算，此处仅返回几何接触尺寸。
        /// </remarks>
        public double GetTouchBoundWidth(TouchEventArgs e)
        {
            var args = e.GetTouchPoint(null).Bounds;
            return Ink_Canvas.Ink.PalmEraserGeometry.GetEffectiveContactWidthDip(
                args.Width,
                args.Height,
                Settings.Advanced.IsQuadIR);
        }

        /// <summary>
        /// InkCanvas的预览触摸按下事件处理方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">触摸事件参数</param>
        /// <remarks>
        /// 处理InkCanvas的预览触摸按下事件，包括以下逻辑：
        /// 1. 捕获触摸输入
        /// 2. 禁用浮动栏和黑板UI网格的命中测试
        /// 3. 将触摸设备ID添加到dec列表中
        /// 4. 当只有一个触摸设备时：
        ///    - 记录中心点坐标
        ///    - 记录第一根手指点击时的StrokeCollection
        /// 5. 当有两个或以上触摸设备，或者处于单指拖动模式，或者禁用了双指手势时：
        ///    - 如果处于多点触控模式或禁用了双指手势，则直接返回
        ///    - 如果当前编辑模式为None或Select，则直接返回
        ///    - 记录当前的编辑模式
        ///    - 设置编辑模式为None，关闭画笔功能
        /// </remarks>
        private void InkCanvas_PreviewTouchDown(object sender, TouchEventArgs e)
        {
            var touchPointForBar = e.GetTouchPoint(this);
            var floatingBarBounds = ViewboxFloatingBar.TransformToAncestor(this).TransformBounds(
                new Rect(0, 0, ViewboxFloatingBar.ActualWidth, ViewboxFloatingBar.ActualHeight));
            if (floatingBarBounds.Contains(touchPointForBar.Position))
                return;

            // 视频展台特殊模式：
            //   - 单指：笔模式让 InkCanvas/原生湿墨正常画墨迹；选择/橡皮擦模式临时切 None 抑制框选
            //   - 双指（第二指落下）：双指手势用于缩放/平移预览画面（画面与墨迹同步），
            //     不应产生墨迹。此时取消第一指正在画的未提交墨迹（原生湿墨 CancelAll +
            //     临时切 None 抑制 WPF Stylus 墨迹），避免双指捏合留下"多余的一条墨迹"。
            // 注意：不能用 e.Handled = true —— 这样会同时阻断 Manipulation 事件的提升，
            //      导致 VideoPresenterSpecialMode_ManipulationDelta 永远收不到事件（Q7 根因）。
            // 仍维护 dec，保证 InkCanvas_PreviewTouchUp 中的 dec.Remove 配对。
            if (_isVideoPresenterSpecialMode && drawingShapeMode == 0)
            {
                bool isSecondFinger = dec.Count >= 1;
                dec.Add(e.TouchDevice.Id);

                if (isSecondFinger)
                {
                    // 第二指落下 = 双指手势开始：临时切 None，让 WPF 内置 Ink（legacy 湿墨 EditingMode==Ink）
                    // 不再把第二指当新墨迹；仅在第一次进双指时保存用户模式，PreviewTouchUp 在所有手指抬起后恢复
                    if (inkCanvas != null
                        && inkCanvas.EditingMode != InkCanvasEditingMode.None
                        && !_boothTouchSavedInkEditingMode.HasValue)
                    {
                        _boothTouchSavedInkEditingMode = inkCanvas.EditingMode;
                    }
                    if (inkCanvas != null && inkCanvas.EditingMode != InkCanvasEditingMode.None)
                    {
                        inkCanvas.EditingMode = InkCanvasEditingMode.None;
                    }
                }
                else if (inkCanvas != null
                    && inkCanvas.EditingMode != InkCanvasEditingMode.Ink
                    && inkCanvas.EditingMode != InkCanvasEditingMode.None
                    && inkCanvas.EditingMode != InkCanvasEditingMode.EraseByPoint
                    && inkCanvas.EditingMode != InkCanvasEditingMode.EraseByStroke)
                {
                    // 第一指 + 选择模式：临时切 None 抑制 InkCanvas 内部框选。
                    // 橡皮擦模式除外——让 InkCanvas 正常执行擦除（否则线擦/点擦在展台会被当作拖动预览）
                    if (!_boothTouchSavedInkEditingMode.HasValue)
                    {
                        _boothTouchSavedInkEditingMode = inkCanvas.EditingMode;
                    }
                    inkCanvas.EditingMode = InkCanvasEditingMode.None;
                }
                // 不设置 e.Handled = true，让 WPF 把触摸提升为 Manipulation 事件
                return;
            }

            if (IsCurrentPageFrozen)
            {
                TryBlockFrozenPageMutation("修改冻结页面");
                e.Handled = true;
                return;
            }

            if ((inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint
                 || inkCanvas.EditingMode == InkCanvasEditingMode.EraseByStroke)
                && !isPalmEraserActive)
            {
                return;
            }

            if (drawingShapeMode != 0)
            {
                inkCanvas.EditingMode = InkCanvasEditingMode.None;
                SetCursorBasedOnEditingMode(inkCanvas);
                inkCanvas.CaptureTouch(e.TouchDevice);
                ViewboxFloatingBar.IsHitTestVisible = false;
                BlackboardUIGridForInkReplay.IsHitTestVisible = false;

                isTouchDown = true;

                if (dec.Count == 0)
                {
                    var inkTouchPoint = e.GetTouchPoint(inkCanvas);
                    if (drawingShapeMode == 24 || drawingShapeMode == 25)
                    {
                        if (drawMultiStepShapeCurrentStep == 0)
                            iniP = inkTouchPoint.Position;
                    }
                    else
                    {
                        iniP = inkTouchPoint.Position;
                    }
                    lastTouchDownStrokeCollection = inkCanvas.Strokes.Clone();
                }
                dec.Add(e.TouchDevice.Id);
                return;
            }

            SetCursorBasedOnEditingMode(inkCanvas);
            inkCanvas.CaptureTouch(e.TouchDevice);
            ViewboxFloatingBar.IsHitTestVisible = false;
            BlackboardUIGridForInkReplay.IsHitTestVisible = false;
            lastTouchDownTime = DateTime.Now;
            dec.Add(e.TouchDevice.Id);

            // 插件画布手势（如 PDF 阅读器双指缩放/平移）：第二指落下 = 双指手势开始，
            // 立即取消第一指正在画的墨迹并临时切 None，避免双指手势留下残留墨迹。
            // 通用双指分支（dec.Count > 1）有 100ms 去抖窗口，那 100ms 内第一指会继续画，
            // 因此必须在此立即处理。不能设 e.Handled = true —— 会阻断 Manipulation 提升，
            // 插件手势（OnCanvasGestureStarting/Delta）收不到增量事件。
            if (_pluginCanvasGestureHandler != null && dec.Count > 1)
            {
                AbortAllActiveTouchInputs();
                if (inkCanvas != null && inkCanvas.EditingMode != InkCanvasEditingMode.None)
                {
                    lastInkCanvasEditingMode = inkCanvas.EditingMode;
                    inkCanvas.EditingMode = InkCanvasEditingMode.None;
                }
                return;
            }

            if (ShouldUseRealtimeVelocityBrushTipForTouch()
                && inkCanvas.EditingMode != InkCanvasEditingMode.EraseByPoint
                && inkCanvas.EditingMode != InkCanvasEditingMode.EraseByStroke
                && inkCanvas.EditingMode != InkCanvasEditingMode.Select)
            {
                try
                {
                    inkCanvas.EditingMode = InkCanvasEditingMode.None;
                    var touchId = e.TouchDevice.Id;
                    var p = e.GetTouchPoint(inkCanvas).Position;
                    _activeRealtimeTouchStrokeIds.Add(touchId);
                    BeginTouchInkInput();
                    CancelPauseStraightenTimer(touchId);
                    InitializeRealtimeBrushTipStateFromPoint(touchId, p);
                    var sv = GetStrokeVisual(touchId);
                    RealtimeInkPerformanceMonitor.BeginStroke(sv, RealtimeInkInputKind.TouchVelocity);
                    TryAppendRealtimeVelocityBrushTipInterpolatedPoints(sv, touchId, p);
                    RealtimeInkFrameScheduler.RequestRedraw(sv);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex);
                }
                return;
            }

            if ((isInMultiTouchMode || (currentMode == 1 ? Settings.Gesture.IsEnableMultiTouchModeBoard : Settings.Gesture.IsEnableMultiTouchMode))
                && inkCanvas.EditingMode != InkCanvasEditingMode.EraseByPoint
                && inkCanvas.EditingMode != InkCanvasEditingMode.EraseByStroke
                && inkCanvas.EditingMode != InkCanvasEditingMode.Select)
            {
                try
                {
                    inkCanvas.EditingMode = InkCanvasEditingMode.None;
                    var touchId = e.TouchDevice.Id;
                    var p = e.GetTouchPoint(inkCanvas).Position;
                    _activeTouchStrokeIds.Add(touchId);
                    BeginTouchInkInput();
                    CancelPauseStraightenTimer(touchId);
                    var sv = GetStrokeVisual(touchId);
                    RealtimeInkPerformanceMonitor.BeginStroke(sv, RealtimeInkInputKind.TouchInterpolated);
                    AppendInterpolatedTouchPoints(sv, touchId, p);
                    RealtimeInkFrameScheduler.RequestRedraw(sv);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex);
                }
                return;
            }

            if (Settings.Canvas.EnablePalmEraser && !isPalmEraserActive && drawingShapeMode == 0)
            {
                var touchPoint = e.GetTouchPoint(inkCanvas);
                double boundWidth = GetTouchBoundWidth(e);

                if ((Settings.Advanced.TouchMultiplier != 0 || !Settings.Advanced.IsSpecialScreen)
                    && (boundWidth > BoundsWidth))
                {
                    double thresholdMultiplier;
                    switch (Settings.Canvas.PalmEraserSensitivity)
                    {
                        case 0:
                            thresholdMultiplier = 3.0;
                            break;
                        case 1:
                            thresholdMultiplier = 2.5;
                            break;
                        case 2:
                        default:
                            thresholdMultiplier = 2.0;
                            break;
                    }

                    double EraserThresholdValue = Settings.Startup.IsEnableNibMode
                        ? Settings.Advanced.NibModeBoundsWidthThresholdValue
                        : Settings.Advanced.FingerModeBoundsWidthThresholdValue;

                    if (boundWidth > BoundsWidth * EraserThresholdValue * thresholdMultiplier)
                    {
                        var eraserSizeFactor = Settings.Startup.IsEnableNibMode
                            ? Settings.Advanced.NibModeBoundsWidthEraserSize
                            : Settings.Advanced.FingerModeBoundsWidthEraserSize;
                        var palmEraserWidth = Ink_Canvas.Ink.PalmEraserGeometry.ApplyPalmEraserSize(
                            boundWidth,
                            eraserSizeFactor,
                            Settings.Advanced.IsSpecialScreen,
                            Settings.Advanced.TouchMultiplier);

                        palmEraserPreviousEditingMode = inkCanvas.EditingMode;
                        inkCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
                        isPalmEraserActive = true;

                        EnableEraserOverlay();
                        eraserWidth = palmEraserWidth;
                        UpdateEraserStyle();
                        touchPoint = e.GetTouchPoint(inkCanvas);
                        EraserOverlay_PointerDown(sender);
                        EraserOverlay_PointerMove(sender, touchPoint.Position);
                        if (Settings.Canvas.IsShowCursor)
                        {
                            inkCanvas.ForceCursor = false;
                            inkCanvas.UseCustomCursor = false;
                        }
                    }
                }
            }

            if (dec.Count == 1)
            {
                var touchPoint = e.GetTouchPoint(inkCanvas);
                centerPoint = touchPoint.Position;
                lastTouchDownStrokeCollection = inkCanvas.Strokes.Clone();
            }

            if (dec.Count > 1 || isSingleFingerDragMode || !Settings.Gesture.IsEnableTwoFingerGesture)
            {
                if (isInMultiTouchMode || !Settings.Gesture.IsEnableTwoFingerGesture) return;
                if (inkCanvas.EditingMode == InkCanvasEditingMode.None ||
                    inkCanvas.EditingMode == InkCanvasEditingMode.Select) return;
                var timeSinceLastTouch = (DateTime.Now - lastTouchDownTime).TotalMilliseconds;
                if (timeSinceLastTouch < MULTI_TOUCH_DELAY_MS && inkCanvas.EditingMode == InkCanvasEditingMode.Ink)
                {
                    if (!isMultiTouchTimerActive)
                    {
                        isMultiTouchTimerActive = true;
                        var remainingTime = MULTI_TOUCH_DELAY_MS - timeSinceLastTouch;
                        Task.Delay((int)remainingTime).ContinueWith(_ =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                if (dec.Count > 1 && inkCanvas.EditingMode == InkCanvasEditingMode.Ink)
                                    inkCanvas.EditingMode = InkCanvasEditingMode.None;
                                isMultiTouchTimerActive = false;
                            });
                        });
                    }
                    return;
                }

                lastInkCanvasEditingMode = inkCanvas.EditingMode;
                if (inkCanvas.EditingMode != InkCanvasEditingMode.EraseByPoint
                    && inkCanvas.EditingMode != InkCanvasEditingMode.EraseByStroke
                    && drawingShapeMode == 0)
                {
                    inkCanvas.EditingMode = InkCanvasEditingMode.None;
                }
            }
        }

        /// <summary>
        /// InkCanvas的预览触摸移动事件处理方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">触摸事件参数</param>
        /// <remarks>
        /// 空方法，预留用于处理InkCanvas的预览触摸移动事件
        /// </remarks>
        private void InkCanvas_PreviewTouchMove(object sender, TouchEventArgs e)
        {
            if (isPalmEraserActive)
            {
                var touchPoint = e.GetTouchPoint(inkCanvas);
                EraserOverlay_PointerMove(sender, touchPoint.Position);
                return;
            }

            var touchId = e.TouchDevice.Id;
            if (_activeRealtimeTouchStrokeIds.Contains(touchId))
            {
                try
                {
                    var p = e.GetTouchPoint(inkCanvas).Position;
                    var sv = GetStrokeVisual(touchId);
                    if (TryAppendRealtimeVelocityBrushTipInterpolatedPoints(sv, touchId, p))
                        RealtimeInkFrameScheduler.RequestRedraw(sv);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex);
                }
                return;
            }

            if (_activeTouchStrokeIds.Contains(touchId))
            {
                StrokeVisual sv = null;
                try
                {
                    var p = e.GetTouchPoint(inkCanvas).Position;
                    sv = GetStrokeVisual(touchId);
                    AppendInterpolatedTouchPoints(sv, touchId, p);
                    RealtimeInkFrameScheduler.RequestRedraw(sv);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex);
                }
            }
        }

        /// <summary>
        /// InkCanvas的预览触摸抬起事件处理方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">触摸事件参数</param>
        /// <remarks>
        /// 处理InkCanvas的预览触摸抬起事件，包括以下逻辑：
        /// 1. 释放所有触摸捕获
        /// 2. 启用浮动栏和黑板UI网格的命中测试
        /// 3. 如果有多个触摸设备且当前编辑模式为None，则切回之前的编辑模式
        /// 4. 从dec列表中移除当前触摸设备ID
        /// 5. 当没有触摸设备时：
        ///    - 重置单指拖动模式和等待下一次触摸按下的标志
        ///    - 如果当前不是图形绘制模式且编辑模式不是橡皮擦或选择模式，则切回之前的编辑模式
        /// 6. 如果当前处于图形绘制模式：
        ///    - 重置触摸状态
        ///    - 启用浮动栏和黑板UI网格的命中测试
        ///    - 对于双曲线等需要多步绘制的图形，根据当前步骤决定是进入下一步还是完成绘制
        ///    - 对于其他单步绘制的图形，直接完成绘制
        /// 7. 设置InkCanvas的透明度为1
        /// 8. 当没有触摸设备且笔画数量发生变化，且不是绘制长方体的第一次触摸时，保存笔画集合
        /// </remarks>
        private void InkCanvas_PreviewTouchUp(object sender, TouchEventArgs e)
        {
            // 视频展台特殊模式：所有手指抬起后恢复用户原本的 EditingMode
            // （PreviewTouchDown 中为了抑制 InkCanvas 内部框选临时切到了 None）
            // 图形绘制模式例外：需要走正常绘制流程完成图形
            if (_isVideoPresenterSpecialMode && drawingShapeMode == 0)
            {
                dec.Remove(e.TouchDevice.Id);
                if (dec.Count == 0)
                {
                    if (_boothTouchSavedInkEditingMode.HasValue && inkCanvas != null)
                    {
                        try
                        {
                            inkCanvas.EditingMode = _boothTouchSavedInkEditingMode.Value;
                        }
                        catch { }
                        _boothTouchSavedInkEditingMode = null;
                    }
                }
                // 仍然执行常规清理（释放触摸捕获、恢复浮动栏可见性等）
                inkCanvas?.ReleaseAllTouchCaptures();
                if (ViewboxFloatingBar != null) ViewboxFloatingBar.IsHitTestVisible = true;
                if (BlackboardUIGridForInkReplay != null) BlackboardUIGridForInkReplay.IsHitTestVisible = true;
                return;
            }

            var touchId = e.TouchDevice.Id;
            if (_activeRealtimeTouchStrokeIds.Contains(touchId))
            {
                StrokeVisual sv = null;
                try
                {
                    sv = GetStrokeVisual(touchId);
                    RealtimeInkFrameScheduler.Flush(sv);
                    var stroke = sv?.Stroke;
                    if (stroke != null)
                    {
                        if (!stroke.ContainsPropertyData(RealtimeVelocityBrushTipAppliedGuid))
                            stroke.AddPropertyData(RealtimeVelocityBrushTipAppliedGuid, true);
                        inkCanvas.Strokes.Add(stroke);
                        inkCanvas_StrokeCollected(inkCanvas, new InkCanvasStrokeCollectedEventArgs(stroke));
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex);
                }
                finally
                {
                    RealtimeInkFrameScheduler.Cancel(sv);
                    if (VisualCanvasList.TryGetValue(touchId, out var visualCanvas) && inkCanvas.Children.Contains(visualCanvas))
                        inkCanvas.Children.Remove(visualCanvas);
                    StrokeVisualList.Remove(touchId);
                    VisualCanvasList.Remove(touchId);
                    TouchDownPointsList.Remove(touchId);
                    CleanupRealtimeBrushTipState(touchId);
                    CancelPauseStraightenTimer(touchId);
                    _activeRealtimeTouchStrokeIds.Remove(touchId);
                    RealtimeInkPerformanceMonitor.EndStroke(sv);
                    EndTouchInkInputIfIdle();
                }
            }
            else if (_activeTouchStrokeIds.Contains(touchId))
            {
                StrokeVisual sv = null;
                try
                {
                    sv = GetStrokeVisual(touchId);
                    RealtimeInkFrameScheduler.Flush(sv);
                    var stroke = sv?.Stroke;
                    if (stroke != null)
                    {
                        inkCanvas.Strokes.Add(stroke);
                        inkCanvas_StrokeCollected(inkCanvas, new InkCanvasStrokeCollectedEventArgs(stroke));
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex);
                }
                finally
                {
                    RealtimeInkFrameScheduler.Cancel(sv);
                    if (VisualCanvasList.TryGetValue(touchId, out var visualCanvas) && inkCanvas.Children.Contains(visualCanvas))
                        inkCanvas.Children.Remove(visualCanvas);
                    StrokeVisualList.Remove(touchId);
                    VisualCanvasList.Remove(touchId);
                    TouchDownPointsList.Remove(touchId);
                    CleanupRealtimeBrushTipState(touchId);
                    CancelPauseStraightenTimer(touchId);
                    _activeTouchStrokeIds.Remove(touchId);
                    RealtimeInkPerformanceMonitor.EndStroke(sv);
                    EndTouchInkInputIfIdle();
                }
            }

            if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint && !isPalmEraserActive)
            {
                return;
            }
            inkCanvas.ReleaseAllTouchCaptures();
            ViewboxFloatingBar.IsHitTestVisible = true;
            BlackboardUIGridForInkReplay.IsHitTestVisible = true;

            dec.Remove(e.TouchDevice.Id);

            if (dec.Count <= 1)
                isMultiTouchTimerActive = false;

            if (drawingShapeMode != 0)
            {
                isTouchDown = false;
                ViewboxFloatingBar.IsHitTestVisible = true;
                BlackboardUIGridForInkReplay.IsHitTestVisible = true;

                if (drawingShapeMode == 24 || drawingShapeMode == 25)
                {
                    if (drawMultiStepShapeCurrentStep == 0)
                    {
                        drawMultiStepShapeCurrentStep = 1;
                    }
                    else
                    {
                        var mouseArgs = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                        {
                            RoutedEvent = MouseLeftButtonUpEvent,
                            Source = inkCanvas
                        };
                        inkCanvas_MouseUp(inkCanvas, mouseArgs);
                    }
                }
                else
                {
                    var mouseArgs = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                    {
                        RoutedEvent = MouseLeftButtonUpEvent,
                        Source = inkCanvas
                    };
                    inkCanvas_MouseUp(inkCanvas, mouseArgs);
                }
            }

            if (drawingShapeMode == 0)
            {
                if (dec.Count > 1)
                {
                    if (inkCanvas.EditingMode == InkCanvasEditingMode.None)
                    {
                        if (lastInkCanvasEditingMode != InkCanvasEditingMode.EraseByPoint)
                            inkCanvas.EditingMode = lastInkCanvasEditingMode;
                    }
                }
                else if (dec.Count == 0)
                {
                    isWaitUntilNextTouchDown = false;

                    if (!IsBoardRoamingMode)
                    {
                        isSingleFingerDragMode = false;
                        if (inkCanvas.EditingMode == InkCanvasEditingMode.None &&
                            lastInkCanvasEditingMode != InkCanvasEditingMode.None &&
                            lastInkCanvasEditingMode != InkCanvasEditingMode.EraseByPoint)
                        {
                            inkCanvas.EditingMode = lastInkCanvasEditingMode;
                        }
                    }

                    if (isPalmEraserActive)
                    {
                        isPalmEraserActive = false;
                        DisableEraserOverlay();
                        if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint)
                        {
                            inkCanvas.EditingMode = palmEraserPreviousEditingMode;
                            SetCursorBasedOnEditingMode(inkCanvas);
                        }
                    }
                }
            }

            inkCanvas.Opacity = 1;

            if (dec.Count == 0)
                if (lastTouchDownStrokeCollection.Count() != inkCanvas.Strokes.Count() &&
                    !(drawingShapeMode == 9 && !isFirstTouchCuboid))
                {
                    // 笔画数量已变化（书写/擦除操作完成）
                }
        }

        /// <summary>
        /// InkCanvas的操作开始事件处理方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">操作开始事件参数</param>
        /// <remarks>
        /// 设置操作模式为所有模式
        /// </remarks>
        private void InkCanvas_ManipulationStarting(object sender, ManipulationStartingEventArgs e)
        {
            // 插件画布手势（如 PDF 阅读器双指缩放/平移）：优先让插件声明手势模式。
            if (_pluginCanvasGestureHandler != null && (e.Manipulators?.Count() ?? 0) >= 2)
            {
                if (_pluginCanvasGestureHandler.OnCanvasGestureStarting(e)) return;
            }

            e.Mode = ManipulationModes.All;
        }

        /// <summary>
        /// InkCanvas的操作惯性开始事件处理方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">操作惯性开始事件参数</param>
        /// <remarks>
        /// 空方法，预留用于处理InkCanvas的操作惯性开始事件
        /// </remarks>
        private void InkCanvas_ManipulationInertiaStarting(object sender, ManipulationInertiaStartingEventArgs e) { }

        /// <summary>
        /// 主网格的操作完成事件处理方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">操作完成事件参数</param>
        /// <remarks>
        /// 处理主网格的操作完成事件，包括以下逻辑：
        /// 1. 当没有操作器时：
        ///    - 清除dec列表
        ///    - 重置单指拖动模式标志
        ///    - 如果当前不是图形绘制模式且编辑模式不是橡皮擦或选择模式，则设置编辑模式为Ink
        /// </remarks>
        private void Main_Grid_ManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
        {
            // 视频展台特殊模式：不在此处恢复 EditingMode，
            // PreviewTouchUp 已经在所有手指抬起后恢复用户原本的模式
            // 图形绘制模式例外：需要走正常绘制流程
            if (_isVideoPresenterSpecialMode && drawingShapeMode == 0)
            {
                return;
            }

            // 插件画布手势结束通知（插件内部用 _gestureActive 自保护，非手势时是无操作）。
            try { _pluginCanvasGestureHandler?.OnCanvasGestureCompleted(e); }
            catch (Exception ex) { LogHelper.WriteLogToFile($"插件画布手势结束通知失败: {ex.Message}", LogHelper.LogType.Warning); }

            if (e.Manipulators.Count() == 0)
            {
                if (dec.Count > 0)
                {
                    dec.Clear();
                }
                if (!IsBoardRoamingMode)
                {
                    isSingleFingerDragMode = false;

                    if (drawingShapeMode == 0
                        && inkCanvas.EditingMode != InkCanvasEditingMode.EraseByPoint
                        && inkCanvas.EditingMode != InkCanvasEditingMode.EraseByStroke
                        && inkCanvas.EditingMode != InkCanvasEditingMode.Select)
                    {
                        // 旧墨迹系统：触摸/手写由 WPF 内置 Ink 收集，操作结束后必须恢复
                        // EditingMode.Ink。否则下一笔落下时 EditingMode 仍为 None，
                        // InkPresenter 在落笔时刻不会启动笔画（第一笔正常、后续均无法书写）。
                        // 恢复 b9b5d38 之前的既有行为。
                        inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                        lastInkCanvasEditingMode = InkCanvasEditingMode.Ink;
                    }
                }
            }
        }

        /// <summary>
        /// 主网格的操作增量事件处理方法
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">操作增量事件参数</param>
        /// <remarks>
        /// 处理主网格的操作增量事件，包括以下逻辑：
        /// 1. 如果当前处于多点触控模式或禁用了双指手势，则直接返回
        /// 2. 检查是否有多个操作器
        /// 3. 检查是否应该使用双指手势
        /// 4. 如果应该使用双指手势：
        ///    - 获取位移矢量
        ///    - 创建矩阵变换
        ///    - 如果启用了双指平移，则应用平移变换
        ///    - 计算中心点（用于缩放和旋转）
        ///    - 如果启用了双指平移或旋转，则应用旋转变换
        ///    - 如果启用了双指缩放，则应用缩放变换
        ///    - 处理选中的笔画：
        ///       - 对每个选中的笔画应用变换
        ///       - 对圆形笔画更新半径和中心点
        ///       - 如果启用了双指缩放，更新笔画的宽度和高度
        ///    - 处理未选中的笔画：
        ///       - 对所有笔画应用变换
        ///       - 如果启用了双指缩放，更新笔画的宽度和高度
        ///       - 同时变换画布上的图片元素
        ///       - 对所有圆形笔画更新半径和中心点
        /// </remarks>
        private void Main_Grid_ManipulationDelta(object sender, ManipulationDeltaEventArgs e)
        {
            // 视频展台特殊模式：
            //   - 单指（笔模式）：在预览画面上绘制墨迹批注，走正常墨迹绘制路径，不拦截
            //   - 双指（任意模式）：用于拖动/缩放预览画面（画面与墨迹同步变换）
            //   - 单指（选择/橡皮擦模式）：用于拖动预览画面
            // 双指时必须拦截并转发到 VideoPresenterSpecialMode_ManipulationDelta，
            // 否则笔模式（legacy WPF 湿墨 EditingMode==Ink）会落入下方通用双指路径，
            // 只缩放墨迹不缩放预览画面（画面不同步），并留下第一指的残留墨迹。
            // VideoPresenterSpecialModeContainer 在 Z 顺序最底层，触摸事件被 inkCanvas 拦截，
            // 根本到不了 Container 上的处理器，必须在此转发。
            if (_isVideoPresenterSpecialMode && inkCanvas != null && drawingShapeMode == 0)
            {
                int manipulatorCount = e.Manipulators?.Count() ?? 0;
                bool penInkSingleFinger = inkCanvas.EditingMode == InkCanvasEditingMode.Ink && manipulatorCount < 2;
                // 橡皮擦单指：让 InkCanvas 正常执行擦除，不转发到预览拖动/缩放
                bool eraserSingleFinger = (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint
                    || inkCanvas.EditingMode == InkCanvasEditingMode.EraseByStroke)
                    && manipulatorCount < 2;
                if (!penInkSingleFinger && !eraserSingleFinger)
                {
                    VideoPresenterSpecialMode_ManipulationDelta(sender, e);
                    return;
                }
            }

            if (IsCurrentPageFrozen)
            {
                TryBlockFrozenPageMutation("移动或缩放内容");
                e.Handled = true;
                return;
            }

            // 插件画布手势（如 PDF 阅读器双指缩放/平移）：双指一律优先转发。
            // 插件返回 true 表示已接管，宿主跳过默认的墨迹/画布变换。
            if (_pluginCanvasGestureHandler != null && (e.Manipulators?.Count() ?? 0) >= 2)
            {
                if (_pluginCanvasGestureHandler.OnCanvasGestureDelta(e))
                {
                    e.Handled = true;
                    return;
                }
            }

            if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint)
                return;

            if (isInMultiTouchMode || (!Settings.Gesture.IsEnableTwoFingerGesture && !IsBoardRoamingMode)) return;

            bool hasMultipleManipulators = e.Manipulators.Count() >= 2;
            bool shouldUseTwoFingerGesture = (dec.Count >= 2 && hasMultipleManipulators &&
                                             (Settings.PowerPointSettings.IsEnableTwoFingerGestureInPresentationMode ||
                                              !ArePPTControlsVisible)) ||
                                            IsBoardRoamingMode ||
                                            isSingleFingerDragMode;

            if (shouldUseTwoFingerGesture)
            {
                var md = e.DeltaManipulation;
                var trans = md.Translation; // 获得位移矢量

                var m = new Matrix();

                bool isBoardMode = currentMode == 1;
                bool enableTranslate = IsBoardRoamingMode || (isBoardMode ? Settings.Gesture.IsEnableTwoFingerTranslateBoard : Settings.Gesture.IsEnableTwoFingerTranslate);
                bool enableRotate = !IsBoardRoamingMode && (isBoardMode ? Settings.Gesture.IsEnableTwoFingerRotationBoard : Settings.Gesture.IsEnableTwoFingerRotation);
                bool enableZoom = !IsBoardRoamingMode && (isBoardMode ? Settings.Gesture.IsEnableTwoFingerZoomBoard : Settings.Gesture.IsEnableTwoFingerZoom);
                bool enableGestureTranslateOrRotate = IsBoardRoamingMode || (isBoardMode
                    ? (Settings.Gesture.IsEnableTwoFingerTranslateBoard || Settings.Gesture.IsEnableTwoFingerRotationBoard)
                    : (Settings.Gesture.IsEnableTwoFingerTranslate || Settings.Gesture.IsEnableTwoFingerRotation));

                if (enableTranslate)
                    m.Translate(trans.X, trans.Y); // 移动

                // 计算中心点（用于缩放和旋转）
                var fe = e.Source as FrameworkElement;
                var center = new Point(fe.ActualWidth / 2, fe.ActualHeight / 2);
                center = m.Transform(center); // 转换为矩阵缩放和旋转的中心点

                if (enableGestureTranslateOrRotate)
                {
                    var rotate = md.Rotation; // 获得旋转角度

                    if (enableRotate)
                        m.RotateAt(rotate, center.X, center.Y); // 旋转
                }

                if (enableZoom)
                {
                    var scale = md.Scale; // 获得缩放倍数
                    m.ScaleAt(scale.X, scale.Y, center.X, center.Y); // 缩放
                }

                var strokes = inkCanvas.GetSelectedStrokes();
                if (strokes.Count != 0)
                {
                    foreach (var stroke in strokes)
                    {
                        stroke.Transform(m, false);

                        foreach (var circle in circles)
                            if (stroke == circle.Stroke)
                            {
                                circle.R = GetDistance(circle.Stroke.StylusPoints[0].ToPoint(),
                                    circle.Stroke.StylusPoints[circle.Stroke.StylusPoints.Count / 2].ToPoint()) / 2;
                                circle.Centroid = new Point(
                                    (circle.Stroke.StylusPoints[0].X +
                                     circle.Stroke.StylusPoints[circle.Stroke.StylusPoints.Count / 2].X) / 2,
                                    (circle.Stroke.StylusPoints[0].Y +
                                     circle.Stroke.StylusPoints[circle.Stroke.StylusPoints.Count / 2].Y) / 2);
                                break;
                            }

                        if (!enableZoom) continue;
                        try
                        {
                            stroke.DrawingAttributes.Width *= md.Scale.X;
                            stroke.DrawingAttributes.Height *= md.Scale.Y;
                        }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
                    }
                }
                else
                {
                    if (enableZoom)
                    {
                        foreach (var stroke in inkCanvas.Strokes)
                        {
                            stroke.Transform(m, false);
                            try
                            {
                                stroke.DrawingAttributes.Width *= md.Scale.X;
                                stroke.DrawingAttributes.Height *= md.Scale.Y;
                            }
                            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
                        }

                        // 同时变换画布上的图片元素
                        TransformCanvasImages(m);
                    }
                    else
                    {
                        foreach (var stroke in inkCanvas.Strokes) stroke.Transform(m, false);

                        // 同时变换画布上的图片元素
                        TransformCanvasImages(m);
                    }

                    foreach (var circle in circles)
                    {
                        circle.R = GetDistance(circle.Stroke.StylusPoints[0].ToPoint(),
                            circle.Stroke.StylusPoints[circle.Stroke.StylusPoints.Count / 2].ToPoint()) / 2;
                        circle.Centroid = new Point(
                            (circle.Stroke.StylusPoints[0].X +
                             circle.Stroke.StylusPoints[circle.Stroke.StylusPoints.Count / 2].X) / 2,
                            (circle.Stroke.StylusPoints[0].Y +
                             circle.Stroke.StylusPoints[circle.Stroke.StylusPoints.Count / 2].Y) / 2
                        );
                    }
                }
            }
        }

        /// <summary>
        /// 变换画布上的图片元素，使其与墨迹同步移动
        /// </summary>
        private void TransformCanvasImages(Matrix matrix)
        {
            try
            {
                // 遍历inkCanvas的所有子元素，找到图片元素
                for (int i = inkCanvas.Children.Count - 1; i >= 0; i--)
                {
                    var child = inkCanvas.Children[i];

                    if (child is Image image)
                    {
                        // 应用矩阵变换到图片
                        ApplyMatrixTransformToImage(image, matrix);
                    }
                    else if (child is MediaElement mediaElement)
                    {
                        // 对媒体元素也应用变换
                        ApplyMatrixTransformToMediaElement(mediaElement, matrix);
                    }
                    else if (child is CanvasMediaControl mediaControl)
                    {
                        ApplyMatrixTransformToCanvasMediaControl(mediaControl, matrix);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"变换画布图片失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 对图片应用矩阵变换
        /// </summary>
        private void ApplyMatrixTransformToImage(Image image, Matrix matrix)
        {
            try
            {
                // 获取图片的RenderTransform，如果不存在则创建新的TransformGroup
                if (!(image.RenderTransform is TransformGroup transformGroup))
                {
                    transformGroup = new TransformGroup();
                    image.RenderTransform = transformGroup;
                }

                // 创建新的MatrixTransform并添加到变换组
                var matrixTransform = new MatrixTransform(matrix);
                transformGroup.Children.Add(matrixTransform);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用图片变换失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 对媒体元素应用矩阵变换
        /// </summary>
        private void ApplyMatrixTransformToMediaElement(MediaElement mediaElement, Matrix matrix)
        {
            try
            {
                // 获取媒体元素的RenderTransform，如果不存在则创建新的TransformGroup
                if (!(mediaElement.RenderTransform is TransformGroup transformGroup))
                {
                    transformGroup = new TransformGroup();
                    mediaElement.RenderTransform = transformGroup;
                }

                // 创建新的MatrixTransform并添加到变换组
                var matrixTransform = new MatrixTransform(matrix);
                transformGroup.Children.Add(matrixTransform);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用媒体元素变换失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void ApplyMatrixTransformToCanvasMediaControl(CanvasMediaControl mediaControl, Matrix matrix)
        {
            try
            {
                if (!(mediaControl.RenderTransform is TransformGroup transformGroup))
                {
                    transformGroup = new TransformGroup();
                    mediaControl.RenderTransform = transformGroup;
                }

                var matrixTransform = new MatrixTransform(matrix);
                transformGroup.Children.Add(matrixTransform);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用媒体控件变换失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }
    }
}
