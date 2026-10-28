using System;

namespace Ink_Canvas.Ink
{
    internal static class PalmEraserProfiles
    {
        // Version 1 calibration stores DIPs. Convert both measurements and contact boxes
        // once at the adapter boundary; never mix DIP² with HID pixel² in the engine.
        internal static PalmEraserProfile Create(TouchCalibrationRecord calibration,
            TouchCalibrationEnvironment environment, PalmEraserOptions options, bool ellipse,
            double legacyThresholdDip = 0, double legacyFactor = 1)
        {
            options = (options ?? new()).Sanitize();
            double x = environment?.DpiX > 0 ? environment.DpiX : 1;
            double y = environment?.DpiY > 0 ? environment.DpiY : 1;
            if (calibration?.IsValid() == true && calibration.Environment.Matches(environment))
                return new()
                {
                    Options = options with { AreaTrim = Math.Clamp(options.AreaTrim * calibration.SizeAdjustment, 0.5, 2) },
                    DpiX = x, DpiY = y, Ellipse = ellipse,
                    PalmWidthPx = calibration.OutlineWidth * x,
                    PalmHeightPx = calibration.OutlineHeight * y,
                    PalmContactAreaPx2 = calibration.PalmArea * x * y,
                    FingerContactAreaPx2 = calibration.FingerArea * x * y
                };

            // Existing installations can still erase before recalibration. This fallback is
            // deliberately isolated from calibrated TouchSuite area/threshold calculations.
            double threshold = double.IsFinite(legacyThresholdDip) && legacyThresholdDip > 0 ? legacyThresholdDip : 0;
            double factor = double.IsFinite(legacyFactor) && legacyFactor > 0 ? legacyFactor : 1;
            double palm = threshold * threshold * x * y;
            return new()
            {
                Options = options with { AreaThresholdPx2 = options.AreaThresholdPx2 ?? palm },
                DpiX = x, DpiY = y, Ellipse = ellipse,
                PalmWidthPx = threshold * x * factor, PalmHeightPx = threshold * y * factor,
                PalmContactAreaPx2 = palm
            };
        }
    }
}
