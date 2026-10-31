using System;
using Ink_Canvas.Ink;
using Newtonsoft.Json;

namespace InkCanvas.Calibration.Tests
{
    internal static class TouchCalibrationTests
    {
        private static void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }
        private static void Near(double actual, double expected) =>
            Check(Math.Abs(actual - expected) < 0.000001, $"Expected {expected}, got {actual}");

        private static TouchCalibrationEnvironment Environment(string source = "WPF") => new()
        {
            Source = source, Display = "DISPLAY1", PixelWidth = 1920, PixelHeight = 1080,
            DpiX = 1.5, DpiY = 1.5
        };

        private sealed class Profile
        {
            public int Preserved { get; set; }
            [JsonConverter(typeof(TouchCalibrationRecordConverter))]
            public TouchCalibrationRecord Calibration { get; set; }
        }

        private static TouchCalibrationSampler Samples(params double[] sides)
        {
            var sampler = new TouchCalibrationSampler(TouchCalibrationMeasure.Area);
            foreach (double side in sides)
            {
                sampler.BeginPress(true, 1, "WPF");
                sampler.AddSample(side, side, 0, 1, "WPF");
                sampler.EndPress();
            }
            return sampler;
        }

        public static void RunAll()
        {
            var sampler = new TouchCalibrationSampler(TouchCalibrationMeasure.Area);
            foreach (double side in new[] { 10d, 30d, 20d })
            {
                Check(sampler.BeginPress(true, 1, "WPF"), "Begin press");
                for (int i = 0; i < 12; i++) sampler.AddSample(side, side, 0, 1, "WPF");
                Check(sampler.EndPress(), "Accept press");
            }
            Check(sampler.AcceptedAreas.Count == 3 && sampler.IsComplete, "Frames must not count as presses");
            Near(sampler.MedianPeak(), 400);
            Near(sampler.MinPeak(), 100);
            Near(sampler.MaxPeak(), 900);
            Check(!sampler.BeginPress(true, 1, "Pointer"), "Source must stay locked");
            Check(!sampler.BeginPress(false, 1, "WPF"), "Outside press");
            sampler.Reset();
            Check(!sampler.IsComplete && sampler.AcceptedAreas.Count == 0, "Reset");
            Check(sampler.BeginPress(true, 1, "Pointer"), "Reset source lock");
            sampler.AddSample(20, 20, 0, 1, "Pointer");
            sampler.CancelPress();
            Check(!sampler.EndPress(), "Canceled press must not count");
            sampler.BeginPress(true, 1, "Pointer");
            sampler.AddSample(20, 20, 0, 2, "Pointer");
            Check(!sampler.EndPress(), "Multiple contacts must not count");
            sampler.BeginPress(true, 1, "Pointer");
            sampler.AddSample(double.NaN, 20, 0, 1, "Pointer");
            sampler.AddSample(double.PositiveInfinity, 20, 0, 1, "Pointer");
            sampler.AddSample(0.1, 20, 0, 1, "Pointer");
            Check(!sampler.EndPress(), "Invalid sizes must not count");
            sampler.BeginPress(true, 1, "Pointer");
            foreach (double side in new[] { 10d, 10d, 100d, 10d, 10d })
                sampler.AddSample(side, side, 0, 1, "Pointer");
            sampler.EndPress();
            Near(sampler.MedianPeak(), 100);

            var record = new TouchCalibrationRecord
            {
                FingerArea = 100, PalmArea = 900, OutlineWidth = 60, OutlineHeight = 60,
                AreaMultiplier = 4, Environment = Environment()
            };
            Check(record.IsValid(), "Valid record");
            Check(TouchCalibrationPolicy.TryResolve(record, Environment(), 1, out var threshold, out var scale), "Resolve");
            Near(threshold * threshold, 500);
            Near(scale, 2);
            TouchCalibrationPolicy.TryResolve(record, Environment(), 0, out var low, out _);
            TouchCalibrationPolicy.TryResolve(record, Environment(), 2, out var high, out _);
            Check(low > threshold && threshold > high, "Sensitivity ordering");
            Check(!TouchCalibrationPolicy.TryResolve(record, Environment("Pointer"), 1, out _, out _), "Source isolation");
            var different = Environment(); different.DpiX = 2;
            Check(!TouchCalibrationPolicy.TryResolve(record, different, 1, out _, out _), "DPI isolation");
            different = Environment(); different.Display = "DISPLAY2";
            Check(!TouchCalibrationPolicy.TryResolve(record, different, 1, out _, out _), "Display isolation");
            Near(TouchCalibrationPolicy.PixelsToDip(60, 1.5), 40);
            record.Version = 2; Check(!record.IsValid(), "Unknown version"); record.Version = 1;
            record.AreaMultiplier = double.NaN; Check(!record.IsValid(), "Invalid multiplier"); record.AreaMultiplier = 4;
            record.PalmArea = 50; Check(!record.IsValid(), "Overlapping representative areas"); record.PalmArea = 900;

            global::PalmEngineRegression.Run(Check);
            var validJson = JsonConvert.SerializeObject(new Profile { Preserved = 42, Calibration = record });
            Check(JsonConvert.DeserializeObject<Profile>(validJson).Calibration.IsValid(), "Calibration round trip");
            foreach (string broken in new[] { "null", "\"bad\"", "[]", "{\"PalmArea\":\"bad\"}", "{\"Version\":999}" })
            {
                var profile = JsonConvert.DeserializeObject<Profile>("{\"Preserved\":42,\"Calibration\":" + broken + "}");
                Check(profile.Preserved == 42 && profile.Calibration == null, "Malformed calibration preserves other settings");
            }
            Check(TouchCalibrationCalculator.TryBuild(Samples(10, 11, 9), Samples(30, 31, 29),
                60, 60, Environment(), out var built) == TouchCalibrationBuildError.None, "Build record");
            Near(built.AreaMultiplier, 4);
            Check(TouchCalibrationCalculator.TryBuild(Samples(10, 20, 10), Samples(15, 30, 30),
                60, 60, Environment(), out _) == TouchCalibrationBuildError.DistributionsOverlap, "Reject overlapping distributions");
            Check(TouchCalibrationCalculator.TryBuild(Samples(10), Samples(30, 30, 30),
                60, 60, Environment(), out _) == TouchCalibrationBuildError.MissingFinger, "Require three presses");
            var legacy = JsonConvert.DeserializeObject<TouchCalibrationRecord>(
                "{\"Version\":1,\"FingerArea\":100,\"PalmArea\":900,\"OutlineWidth\":60,\"OutlineHeight\":60,\"AreaMultiplier\":4,"
                + "\"Environment\":{\"Source\":\"WPF\",\"Display\":\"DISPLAY1\",\"PixelWidth\":1920,\"PixelHeight\":1080,\"DpiX\":1.5,\"DpiY\":1.5}}");
            Check(legacy.IsValid() && !legacy.UsePointGrouping && legacy.MinimumContacts == 3
                && legacy.WindowMilliseconds == 200 && legacy.GroupTolerance == 1, "Existing profiles retain the area policy and defaults");
            var pointRecord = new TouchCalibrationRecord
            {
                UsePointGrouping = true, OutlineWidth = 100, OutlineHeight = 80,
                MinimumContacts = 4, WindowMilliseconds = 300, GroupTolerance = 1.5,
                SizeAdjustment = 1.5, Environment = Environment()
            };
            Check(pointRecord.IsValid(), "Position-only calibration does not require area samples");
            Check(!TouchCalibrationPolicy.TryResolve(pointRecord, Environment(), 1, out _, out _), "Position-only policy must not use an area threshold");
            var pointRestored = JsonConvert.DeserializeObject<Profile>(JsonConvert.SerializeObject(
                new Profile { Preserved = 42, Calibration = pointRecord })).Calibration;
            Check(pointRestored.IsValid() && pointRestored.UsePointGrouping && pointRestored.MinimumContacts == 4
                && pointRestored.WindowMilliseconds == 300 && pointRestored.GroupTolerance == 1.5, "Position-only settings survive the settings converter");
            var restoredProfile = PalmEraserProfiles.Create(pointRestored, Environment(), new PalmEraserOptions(), false);
            Check(restoredProfile.Options.MultiTouch && !restoredProfile.Options.AreaEnabled
                && restoredProfile.Options.MinimumContacts == 4 && restoredProfile.Options.GroupWindowMs == 300
                && restoredProfile.Options.GroupTolerance == 1.5, "Saved point settings feed the TouchSuite engine");
            Near(restoredProfile.PalmWidthPx, 150);
            pointRestored.MinimumContacts = 1; Check(!pointRestored.IsValid(), "Reject invalid contact count"); pointRestored.MinimumContacts = 4;
            pointRestored.WindowMilliseconds = -1; Check(!pointRestored.IsValid(), "Reject invalid time window"); pointRestored.WindowMilliseconds = 300;
            pointRestored.GroupTolerance = double.NaN; Check(!pointRestored.IsValid(), "Reject invalid group tolerance");
            Console.WriteLine("PASS CalibrationRoutes (area, position-only, migration, persistence, runtime policy)");
            Console.WriteLine("PASS TouchSuitePalmEngine (area, grouping, source locking, migration)");
            Console.WriteLine("PASS TouchCalibrationTests (sampling, policy, environment, tracker, persistence)");
        }
    }
}
