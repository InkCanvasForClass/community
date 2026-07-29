using System;
using System.Collections.Generic;

namespace Ink_Canvas.Ink.Native
{
    internal sealed class InkSampleProcessorSettings
    {
        public bool DisablePressure { get; init; }
        public bool EnablePressureForTouch { get; init; }
        public bool UseVelocityBrushTip { get; init; }
        public float VelocityBrushTipMix { get; init; }
        public float MinimumDistanceScale { get; init; } = 0.5f;
        public double BaseWidth { get; init; } = 2.5;
        /// <summary>
        /// Canvas.InkStyle: 0 点集笔锋, 1 速率笔锋, 2 关闭, 3 实时笔锋.
        /// 0/1 在抬笔时整笔改写 Pressure；3 在书写过程中实时写入.
        /// </summary>
        public int InkStyle { get; init; } = 2;
    }

    internal sealed class InkSampleProcessor
    {
        private sealed class OneEuroFilter
        {
            private readonly float _minimumCutoff;
            private readonly float _beta;
            private readonly float _derivativeCutoff;
            private bool _initialized;
            private float _previousValue;
            private float _previousDerivative;

            public OneEuroFilter(float minimumCutoff, float beta, float derivativeCutoff)
            {
                _minimumCutoff = minimumCutoff;
                _beta = beta;
                _derivativeCutoff = derivativeCutoff;
            }

            public float Filter(float value, float deltaSeconds, float speed)
            {
                if (!_initialized)
                {
                    _initialized = true;
                    _previousValue = value;
                    return value;
                }

                var derivative = (value - _previousValue) / Math.Max(0.000001f, deltaSeconds);
                var derivativeAlpha = Alpha(_derivativeCutoff, deltaSeconds);
                _previousDerivative = Lerp(_previousDerivative, derivative, derivativeAlpha);
                var valueAlpha = Alpha(_minimumCutoff + _beta * speed, deltaSeconds);
                _previousValue = Lerp(_previousValue, value, valueAlpha);
                return _previousValue;
            }

            private static float Alpha(float cutoff, float deltaSeconds)
            {
                var tau = 1f / (2f * (float)Math.PI * Math.Max(0.001f, cutoff));
                return 1f / (1f + tau / Math.Max(0.000001f, deltaSeconds));
            }

            private static float Lerp(float from, float to, float amount) => from + (to - from) * amount;
        }

        private readonly InkSampleProcessorSettings _settings;
        private readonly OneEuroFilter _filterX = new OneEuroFilter(1.2f, 0.015f, 1f);
        private readonly OneEuroFilter _filterY = new OneEuroFilter(1.2f, 0.015f, 1f);
        private readonly OneEuroFilter _filterPressure = new OneEuroFilter(1f, 0.02f, 1f);
        private bool _hasPrevious;
        private bool _sawPressureVariation;
        private float _lastRawX;
        private float _lastRawY;
        private float _lastSmoothX;
        private float _lastSmoothY;
        private float _lastSmoothPressure = 0.5f;
        private long _lastTimestampMicroseconds;
        private float _smoothedSampleRate = 120f;
        private RawInkSample _firstSample;
        private bool _hasFirstSample;

        public InkSampleProcessor(InkSampleProcessorSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public bool VelocityBrushTipApplied { get; private set; }

        /// <summary>
        /// True after a final (point-set / rate) brush-tip pass rewrote pressures at pen-up.
        /// Dry stroke must honor PressureFactor when this is set.
        /// </summary>
        public bool FinalBrushTipApplied { get; private set; }

        public void Append(IReadOnlyList<RawInkSample> samples, List<RealInkPoint> destination)
        {
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            if (destination == null) throw new ArgumentNullException(nameof(destination));

            for (var i = 0; i < samples.Count; i++)
                Append(samples[i], destination);
        }

        /// <summary>
        /// Applies InkStyle 0 (point-set tip) / 1 (rate tip) to the full point list at pen-up.
        /// Realtime velocity tip (InkStyle 3) is already applied during Append.
        /// Mirrors the legacy dry-ink post-process so native freehand keeps the same brush tip.
        /// </summary>
        public void ApplyFinalBrushTip(List<RealInkPoint> points)
        {
            if (points == null || points.Count == 0)
                return;
            if (_settings.DisablePressure)
                return;
            // 实时笔锋已在 Append 中完成；关闭(2)不处理。
            if (_settings.InkStyle != 0 && _settings.InkStyle != 1)
                return;
            if (points.Count < 2)
                return;

            if (_settings.InkStyle == 1)
            {
                ApplyRateBasedBrushTip(points);
                FinalBrushTipApplied = true;
                return;
            }

            ApplyPointSetBrushTip(points);
            FinalBrushTipApplied = true;
        }

        private static void ApplyRateBasedBrushTip(List<RealInkPoint> points)
        {
            var n = points.Count - 1;
            for (var i = 0; i <= n; i++)
            {
                var prev = points[Math.Max(i - 1, 0)];
                var cur = points[i];
                var next = points[Math.Min(i + 1, n)];
                var speed = GetLegacyPointSpeed(prev, cur, next);
                var pressure = RateBasedPressureFactorFromPointSpeed(speed);
                points[i] = new RealInkPoint(cur.X, cur.Y, pressure, cur.TimestampMicroseconds);
            }
        }

        private static void ApplyPointSetBrushTip(List<RealInkPoint> points)
        {
            var n = points.Count - 1;
            if (n == 1)
                return;

            const double taperPressure = 0.1;
            const int taperCount = 10;
            var rewritten = new List<RealInkPoint>(points.Count);

            if (n >= taperCount)
            {
                for (var i = 0; i < n - taperCount; i++)
                {
                    var p = points[i];
                    rewritten.Add(new RealInkPoint(p.X, p.Y, 0.5f, p.TimestampMicroseconds));
                }

                for (var i = n - taperCount; i <= n; i++)
                {
                    var p = points[i];
                    var pressure = (float)((0.5 - taperPressure) * (n - i) / taperCount + taperPressure);
                    rewritten.Add(new RealInkPoint(p.X, p.Y, pressure, p.TimestampMicroseconds));
                }
            }
            else
            {
                for (var i = 0; i <= n; i++)
                {
                    var p = points[i];
                    var pressure = (float)(0.4 * (n - i) / n + taperPressure);
                    rewritten.Add(new RealInkPoint(p.X, p.Y, pressure, p.TimestampMicroseconds));
                }
            }

            points.Clear();
            points.AddRange(rewritten);
        }

        /// <summary>
        /// Legacy GetPointSpeed used by InkStyle 0/1 dry post-process
        /// (sum of segment lengths / 20, not time-based).
        /// </summary>
        private static double GetLegacyPointSpeed(
            RealInkPoint point1,
            RealInkPoint point2,
            RealInkPoint point3)
        {
            var d12 = Math.Sqrt(
                (point1.X - point2.X) * (point1.X - point2.X)
                + (point1.Y - point2.Y) * (point1.Y - point2.Y));
            var d32 = Math.Sqrt(
                (point3.X - point2.X) * (point3.X - point2.X)
                + (point3.Y - point2.Y) * (point3.Y - point2.Y));
            return (d12 + d32) / 20.0;
        }

        private static float RateBasedPressureFactorFromPointSpeed(double speed)
        {
            if (speed >= 0.25)
                return (float)(0.5 - 0.3 * (Math.Min(speed, 1.5) - 0.3) / 1.2);
            if (speed >= 0.05)
                return 0.5f;
            return (float)(0.5 + 0.4 * (0.05 - speed) / 0.05);
        }

        private void Append(RawInkSample sample, List<RealInkPoint> destination)
        {
            var rawX = (float)sample.X;
            var rawY = (float)sample.Y;
            if (!_hasPrevious)
            {
                _firstSample = sample;
                _hasFirstSample = true;
                // 落笔首点：暂用硬件压感/默认 0.5。待第二点到达后，按首段速度回修首点压感，
                // 使落笔点与笔画主体宽度一致，避免起笔闪变。
                var initialPressure = ResolvePressure(sample, 0, 1f / 120f, applyVelocityModulation: false);
                destination.Add(new RealInkPoint(sample.X, sample.Y, initialPressure, sample.TimestampMicroseconds));
                _hasPrevious = true;
                _lastRawX = rawX;
                _lastRawY = rawY;
                _lastSmoothX = rawX;
                _lastSmoothY = rawY;
                _lastSmoothPressure = initialPressure;
                _lastTimestampMicroseconds = sample.TimestampMicroseconds;
                return;
            }

            var deltaSeconds = Math.Max(0.0001f, (sample.TimestampMicroseconds - _lastTimestampMicroseconds) / 1_000_000f);
            var deltaX = rawX - _lastRawX;
            var deltaY = rawY - _lastRawY;
            var distance = (float)Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            var speed = distance / deltaSeconds;
            var sampleRate = 1f / deltaSeconds;
            _smoothedSampleRate = _smoothedSampleRate * 0.85f + sampleRate * 0.15f;

            // 首点回修：第二点到达时，按首段真实速度重算首点压感，使落笔点融入笔画。
            if (_hasFirstSample && destination.Count == 1)
            {
                var firstPressure = ResolvePressure(_firstSample, speed, deltaSeconds, applyVelocityModulation: true);
                destination[0] = new RealInkPoint(
                    destination[0].X,
                    destination[0].Y,
                    firstPressure,
                    destination[0].TimestampMicroseconds);
                _lastSmoothPressure = firstPressure;
                _hasFirstSample = false;
            }

            var filteredX = _filterX.Filter(rawX, deltaSeconds, speed);
            var filteredY = _filterY.Filter(rawY, deltaSeconds, speed);
            var pressure = ResolvePressure(sample, speed, deltaSeconds);
            var minimumDistance = GetMinimumDistance();

            _lastRawX = rawX;
            _lastRawY = rawY;
            _lastTimestampMicroseconds = sample.TimestampMicroseconds;
            if (distance < minimumDistance)
                return;

            var pointX = (_lastSmoothX + filteredX) * 0.5f;
            var pointY = (_lastSmoothY + filteredY) * 0.5f;
            var pointPressure = (_lastSmoothPressure + pressure) * 0.5f;
            destination.Add(new RealInkPoint(pointX, pointY, pointPressure, sample.TimestampMicroseconds));
            _lastSmoothX = filteredX;
            _lastSmoothY = filteredY;
            _lastSmoothPressure = pressure;
        }

        private float ResolvePressure(RawInkSample sample, float speed, float deltaSeconds, bool applyVelocityModulation = true)
        {
            if (_settings.DisablePressure)
                return 0.5f;

            var rawPressure = sample.HasPressure ? Clamp(sample.Pressure, 0f, 1f) : 0.5f;
            if (sample.HasPressure && Math.Abs(rawPressure - 0.5f) > 0.02f)
                _sawPressureVariation = true;

            var useHardwarePressure = sample.InputKind == NativeInkInputKind.Pen
                                      ? _sawPressureVariation && rawPressure > 0
                                      : _settings.EnablePressureForTouch && _sawPressureVariation && rawPressure > 0;
            if (!_settings.UseVelocityBrushTip)
                return useHardwarePressure ? rawPressure : 0.5f;

            var width = (float)Math.Max(0.35, _settings.BaseWidth);
            if (useHardwarePressure)
                width *= 0.25f + 0.75f * rawPressure;
            var speedNormalization = 1800f + _smoothedSampleRate * 3.5f;
            if (applyVelocityModulation)
                width *= Clamp(1.15f - speed / speedNormalization, 0.45f, 1.25f);
            var speedPressure = WidthToPressure(width, (float)Math.Max(0.35, _settings.BaseWidth));
            var mix = Clamp(_settings.VelocityBrushTipMix, 0f, 1f);
            var pressure = useHardwarePressure
                ? (1f - mix) * rawPressure + mix * speedPressure
                : speedPressure;
            VelocityBrushTipApplied = true;
            return _filterPressure.Filter(Clamp(pressure, 0.08f, 1f), deltaSeconds, speed);
        }

        private float GetMinimumDistance()
        {
            var baseDistance = _smoothedSampleRate > 160f ? 0.35f : _smoothedSampleRate > 90f ? 0.25f : 0.15f;
            return baseDistance * Clamp(_settings.MinimumDistanceScale, 0f, 2f);
        }

        private static float WidthToPressure(float width, float baseWidth)
        {
            return Clamp((width / baseWidth - 0.42f) / 1.16f, 0.08f, 1f);
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            if (value < minimum) return minimum;
            return value > maximum ? maximum : value;
        }
    }
}
