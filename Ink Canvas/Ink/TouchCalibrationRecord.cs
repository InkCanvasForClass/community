using System;

namespace Ink_Canvas.Ink
{
    public sealed class TouchCalibrationEnvironment
    {
        public string Source { get; set; }
        public string Display { get; set; }
        public int PixelWidth { get; set; }
        public int PixelHeight { get; set; }
        public double DpiX { get; set; }
        public double DpiY { get; set; }

        public bool IsValid() => (Source == "Pointer" || Source == "WPF")
            && !string.IsNullOrWhiteSpace(Display) && PixelWidth > 0 && PixelHeight > 0
            && TouchCalibrationRecord.Positive(DpiX) && TouchCalibrationRecord.Positive(DpiY);

        public bool Matches(TouchCalibrationEnvironment other) => IsValid() && other != null && other.IsValid()
            && Source == other.Source && Display == other.Display
            && PixelWidth == other.PixelWidth && PixelHeight == other.PixelHeight
            && Math.Abs(DpiX - other.DpiX) < 0.001 && Math.Abs(DpiY - other.DpiY) < 0.001;
    }

    public sealed class TouchCalibrationRecord
    {
        public int Version { get; set; } = 1;
        public double FingerArea { get; set; }
        public double PalmArea { get; set; }
        public double OutlineWidth { get; set; }
        public double OutlineHeight { get; set; }
        public double AreaMultiplier { get; set; }
        public double SizeAdjustment { get; set; } = 1;
        // Missing fields in existing version-1 records retain the original area-based policy.
        public bool UsePointGrouping { get; set; }
        public int MinimumContacts { get; set; } = 3;
        public int WindowMilliseconds { get; set; } = 200;
        public double GroupTolerance { get; set; } = 1;
        public TouchCalibrationEnvironment Environment { get; set; }

        public static bool Positive(double value) => double.IsFinite(value) && value > 0;
        public bool IsValid() => Version == 1 && Environment?.IsValid() == true
            && Positive(OutlineWidth) && OutlineWidth <= 4096 && Positive(OutlineHeight) && OutlineHeight <= 4096
            && SizeAdjustment >= 0.5 && SizeAdjustment <= 2
            && MinimumContacts >= 2 && MinimumContacts <= 5
            && WindowMilliseconds >= 0 && WindowMilliseconds <= 1000
            && GroupTolerance >= 1 && GroupTolerance <= 2
            && (UsePointGrouping || (Positive(FingerArea) && Positive(PalmArea) && PalmArea > FingerArea
                && Positive(AreaMultiplier)
                && Math.Abs(AreaMultiplier - OutlineWidth * OutlineHeight / PalmArea) <= AreaMultiplier * 0.000001));
    }

    internal static class TouchCalibrationPolicy
    {
        public static bool TryResolve(TouchCalibrationRecord record, TouchCalibrationEnvironment environment,
            int sensitivity, out double thresholdWidth, out double sizeFactor)
        {
            thresholdWidth = sizeFactor = 0;
            if (record?.IsValid() != true || record.UsePointGrouping || !record.Environment.Matches(environment)) return false;
            double fraction = sensitivity == 0 ? 0.65 : sensitivity == 1 ? 0.5 : 0.35;
            thresholdWidth = Math.Sqrt(record.FingerArea + (record.PalmArea - record.FingerArea) * fraction);
            sizeFactor = Math.Sqrt(record.AreaMultiplier * record.SizeAdjustment);
            return true;
        }

        public static double PixelsToDip(double pixels, double dpiScale) =>
            TouchCalibrationRecord.Positive(pixels) && TouchCalibrationRecord.Positive(dpiScale) ? pixels / dpiScale : 0;
    }
}
