using System;

namespace Ink_Canvas.Ink
{
    internal static class PalmEraserProfiles
    {
        // Version 1 calibration stores DIPs. Convert both measurements and contact boxes
        // once at the adapter boundary; never mix DIP² with HID pixel² in the engine.
        //
        // This build only accepts a valid, environment-matching calibration. The legacy
        // heuristic fallback (sensitivity * bounds width) is intentionally not ported yet;
        // when it comes back it belongs here, isolated from the calibrated area math.
        internal static bool TryCreate(TouchCalibrationRecord calibration,
            TouchCalibrationEnvironment environment, PalmEraserOptions options, bool ellipse,
            out PalmEraserProfile profile, double? fingerAreaDip2 = null, double? palmAreaDip2 = null)
        {
            profile = null;
            if (calibration?.IsValid() != true) return false;
            if (calibration.Environment.Matches(environment) is false) return false;

            double x = environment?.DpiX > 0 ? environment.DpiX : 1;
            double y = environment?.DpiY > 0 ? environment.DpiY : 1;
            if (!(calibration.OutlineWidth > 0) || !(calibration.OutlineHeight > 0)) return false;

            double fingerArea = TouchCalibrationRecord.Positive(fingerAreaDip2 ?? 0)
                ? fingerAreaDip2.Value : calibration.FingerArea;
            double palmArea = TouchCalibrationRecord.Positive(palmAreaDip2 ?? 0)
                ? palmAreaDip2.Value : calibration.PalmArea;
            if (!TouchCalibrationRecord.Positive(fingerArea) || !TouchCalibrationRecord.Positive(palmArea)
                || palmArea <= fingerArea) return false;

            options = (options ?? new PalmEraserOptions()).Sanitize();
            profile = new PalmEraserProfile
            {
                Options = options with
                {
                    AreaTrim = Math.Clamp(options.AreaTrim * calibration.SizeAdjustment, 0.5, 2)
                },
                DpiX = x,
                DpiY = y,
                Ellipse = ellipse,
                PalmWidthPx = calibration.OutlineWidth * x,
                PalmHeightPx = calibration.OutlineHeight * y,
                PalmContactAreaPx2 = palmArea * x * y,
                FingerContactAreaPx2 = fingerArea * x * y
            };
            return true;
        }
    }
}
