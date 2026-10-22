using System;
using System.Collections.Generic;

namespace Ink_Canvas.Ink
{
    /// <summary>
    /// One calibration measurement stage. WPF-free so the console tests can drive
    /// the exact same code the wizard uses. Areas are DIP², pressure is 0..1.
    /// A press is one Down..Up sequence; only completed presses count, each
    /// contributing a short-window filtered peak, never individual frames.
    /// </summary>
    public sealed class TouchCalibrationSampler
    {
        private const int FilterWindow = 3;
        private readonly Queue<double> _window = new Queue<double>(FilterWindow);
        private bool _pressActive;
        private bool _pressMultiTouch;
        private double _pressPeakArea;
        private double _pressPeakPressure;
        private double _liveArea;
        private double _livePressure;
        private bool _liveValid;
        private string _lockedSource;

        public TouchCalibrationSampler(TouchCalibrationMeasure measure, int requiredPresses = 3)
        {
            Measure = measure;
            RequiredPresses = Math.Max(1, requiredPresses);
            AcceptedAreas = new List<double>();
            AcceptedPressures = new List<double>();
        }

        public TouchCalibrationMeasure Measure { get; }
        public int RequiredPresses { get; }
        public List<double> AcceptedAreas { get; }
        public List<double> AcceptedPressures { get; }
        public int CompletedPresses => AcceptedAreas.Count;
        public bool IsComplete => CompletedPresses >= RequiredPresses;
        public double LiveArea => _liveArea;
        public double LivePressure => _livePressure;
        public bool LiveValid => _liveValid;
        public string LockedSource => _lockedSource;
        public bool IsPressActive => _pressActive;
        public double PressPeakArea => _pressPeakArea;
        public double PressPeakPressure => _pressPeakPressure;

        public void Reset()
        {
            _window.Clear();
            _pressActive = false;
            _pressMultiTouch = false;
            _pressPeakArea = 0;
            _pressPeakPressure = 0;
            _liveArea = _livePressure = 0;
            _liveValid = false;
            _lockedSource = null;
            AcceptedAreas.Clear();
            AcceptedPressures.Clear();
        }

        public void ClearResults()
        {
            AcceptedAreas.Clear();
            AcceptedPressures.Clear();
        }

        /// <summary>Down. Returns false when the press cannot start (outside capture area or invalid).</summary>
        public bool BeginPress(bool insideCaptureArea, int contactCount, string source)
        {
            if (_pressActive) return false;
            _window.Clear();
            _pressActive = false;
            _pressMultiTouch = contactCount > 1;
            _pressPeakArea = 0;
            _pressPeakPressure = 0;
            if (!insideCaptureArea) return false;
            if (!SourceMatches(source)) return false;
            _pressActive = true;
            if (_pressMultiTouch) _pressActive = false; // multi-touch never becomes a valid single-contact press
            return _pressActive;
        }

        /// <summary>
        /// Adds one contact frame during the active press. Frames from an unlocked
        /// alternate source are ignored so two backends never mix in one window.
        /// </summary>
        public void AddSample(double width, double height, double pressure, int contactCount, string source)
        {
            if (!_pressActive) return;
            if (!SourceMatches(source)) return;
            if (contactCount > 1)
            {
                _pressMultiTouch = true;
                _pressActive = false;
                return;
            }

            bool geometryValid = double.IsFinite(width) && width >= 0.5 && width <= 4096
                && double.IsFinite(height) && height >= 0.5 && height <= 4096;
            if (geometryValid)
            {
                _window.Enqueue(width * height);
                while (_window.Count > FilterWindow) _window.Dequeue();
                double filtered = Median(_window);
                _liveArea = filtered;
                _liveValid = true;
                if (filtered > _pressPeakArea) _pressPeakArea = filtered;
            }
            if (double.IsFinite(pressure) && pressure > 0)
            {
                _livePressure = pressure;
                if (pressure > _pressPeakPressure) _pressPeakPressure = pressure;
            }
        }

        public void CancelPress()
        {
            _pressActive = false;
            _pressMultiTouch = false;
            _window.Clear();
        }

        /// <summary>Up. Returns true when this press contributed a measurement.</summary>
        public bool EndPress()
        {
            bool wasMulti = _pressMultiTouch;
            bool record = _pressActive && !wasMulti;
            _pressActive = false;
            _pressMultiTouch = false;
            _window.Clear();
            if (!record) return false;

            if (Measure == TouchCalibrationMeasure.Pressure)
            {
                if (_pressPeakPressure <= 0) return false;
                AcceptedPressures.Add(_pressPeakPressure);
                return true;
            }
            if (_pressPeakArea <= 0) return false;
            AcceptedAreas.Add(_pressPeakArea);
            return true;
        }

        /// <summary>Median of the recorded press peaks; 0 when nothing was accepted.</summary>
        public double MedianPeak()
        {
            var values = Measure == TouchCalibrationMeasure.Pressure ? AcceptedPressures : AcceptedAreas;
            return values.Count == 0 ? 0 : Median(values);
        }

        public double MinPeak()
        {
            var values = Measure == TouchCalibrationMeasure.Pressure ? AcceptedPressures : AcceptedAreas;
            if (values.Count == 0) return 0;
            double min = values[0];
            for (int i = 1; i < values.Count; i++) if (values[i] < min) min = values[i];
            return min;
        }

        public double MaxPeak()
        {
            var values = Measure == TouchCalibrationMeasure.Pressure ? AcceptedPressures : AcceptedAreas;
            if (values.Count == 0) return 0;
            double max = values[0];
            for (int i = 1; i < values.Count; i++) if (values[i] > max) max = values[i];
            return max;
        }

        private bool SourceMatches(string source)
        {
            if (string.IsNullOrEmpty(source)) return false;
            if (_lockedSource == null)
            {
                _lockedSource = source;
                return true;
            }
            return _lockedSource == source;
        }

        internal static double Median(IReadOnlyCollection<double> values)
        {
            if (values == null || values.Count == 0) return 0;
            var sorted = new List<double>(values);
            sorted.Sort();
            int middle = sorted.Count / 2;
            return sorted.Count % 2 == 0
                ? sorted[middle - 1] + (sorted[middle] - sorted[middle - 1]) / 2
                : sorted[middle];
        }
    }

    public enum TouchCalibrationMeasure
    {
        Area,
        Pressure
    }

    public enum TouchCalibrationBuildError
    {
        None,
        MissingFinger,
        MissingPalm,
        MissingOutline,
        PalmNotAboveFinger,
        DistributionsOverlap,
        InvalidArea
    }

    /// <summary>Combines the recorded finger/palm presses and the traced outline into a record.</summary>
    public static class TouchCalibrationCalculator
    {
        public static TouchCalibrationBuildError TryBuild(
            TouchCalibrationSampler finger,
            TouchCalibrationSampler palm,
            double outlineWidth, double outlineHeight,
            TouchCalibrationEnvironment environment,
            out TouchCalibrationRecord record)
        {
            record = null;
            if (finger == null || palm == null) return TouchCalibrationBuildError.MissingFinger;
            if (!finger.IsComplete || finger.CompletedPresses < finger.RequiredPresses)
                return TouchCalibrationBuildError.MissingFinger;
            if (!palm.IsComplete || palm.CompletedPresses < palm.RequiredPresses)
                return TouchCalibrationBuildError.MissingPalm;
            if (!TouchCalibrationRecord.Positive(outlineWidth) || !TouchCalibrationRecord.Positive(outlineHeight))
                return TouchCalibrationBuildError.MissingOutline;
            if (environment?.IsValid() != true) return TouchCalibrationBuildError.InvalidArea;

            double fingerMedian = finger.MedianPeak();
            double palmMedian = palm.MedianPeak();
            if (!TouchCalibrationRecord.Positive(fingerMedian) || !TouchCalibrationRecord.Positive(palmMedian))
                return TouchCalibrationBuildError.InvalidArea;
            if (palmMedian <= fingerMedian) return TouchCalibrationBuildError.PalmNotAboveFinger;
            // 分布重叠意味着这台设备的尺寸数据无法区分手掌和手指，不能宣称校准可用。
            if (palm.MinPeak() <= finger.MaxPeak()) return TouchCalibrationBuildError.DistributionsOverlap;

            record = new TouchCalibrationRecord
            {
                Version = 1,
                FingerArea = fingerMedian,
                PalmArea = palmMedian,
                OutlineWidth = outlineWidth,
                OutlineHeight = outlineHeight,
                AreaMultiplier = outlineWidth * outlineHeight / palmMedian,
                SizeAdjustment = 1,
                Environment = environment
            };
            return record.IsValid() ? TouchCalibrationBuildError.None : TouchCalibrationBuildError.InvalidArea;
        }
    }
}
