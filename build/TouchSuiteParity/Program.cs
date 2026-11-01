using System.Windows;
using Ink_Canvas.Ink;
using Reference = TouchSuite.App.Eraser;

internal static class Program
{
    private static int _checks;
    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); _checks++; }
    private static void Equal(double a, double b, string message) =>
        Require(Math.Abs(a - b) <= Math.Max(1, Math.Abs(b)) * 1e-10, $"{message}: {a:R} != {b:R}");

    private static Reference.EraserEngine Oracle(PalmEraserProfile profile)
    {
        var o = profile.Options;
        var engine = new Reference.EraserEngine
        {
            PxPerDiuX = profile.DpiX, PxPerDiuY = profile.DpiY,
            PalmWidthPx = profile.PalmWidthPx, PalmHeightPx = profile.PalmHeightPx,
            PalmContactAreaPx2 = profile.PalmContactAreaPx2, FingerContactAreaPx2 = profile.FingerContactAreaPx2,
            FollowSize = o.FollowSize, LockPalmSize = o.LockSize, SmoothJitter = o.Smooth,
            SizeLimit = (Reference.PalmLimit)o.Limit, PalmPressureEnabled = o.PressureEnabled,
            PalmPressureThreshold = o.PressureThreshold, PalmNormMax = o.PressureMaximum,
            KTrim = o.AreaTrim, AreaThresholdEnabled = o.AreaEnabled, AreaThresholdPx2 = profile.ThresholdPx2,
            Aspect = (Reference.AspectSource)o.Aspect, CustomAspect = o.CustomAspect,
            Shape = profile.Ellipse ? Reference.EraserShape.Circle : Reference.EraserShape.Rectangle,
            MultiTouchAsPalm = o.MultiTouch, MultiTouchPalmMin = o.MinimumContacts,
            PalmGroupWindowMs = o.GroupWindowMs, PalmGroupTol = o.GroupTolerance
        };
        engine.SetMode((Reference.SourceMode)o.Source);
        engine.SetRawHidDevice(o.HidDevice);
        return engine;
    }

    private static void Compare(PalmEraserEngine actual, Reference.EraserEngine expected, string context)
    {
        Require((int)actual.ActiveSource == (int)expected.ActiveSource, context + " active source");
        Require((int)actual.LockedSource == (int)expected.LockedSource, context + " locked source");
        Require(actual.Count == expected.LastContacts.Count, context + " tracks");
        Equal(actual.EffectiveK, expected.EffectiveK(), context + " K");
        Equal(actual.PressureGain, expected.PalmGain, context + " pressure gain");
        foreach (var (id, rect) in expected.LastContacts)
        {
            Require(actual.IsWriting(id) == expected.IsWriting(id), context + $" classification #{id}");
            var size = actual.Size(id);
            var reference = expected.SizePx(id, rect);
            Equal(size.Width, reference.w, context + " width");
            Equal(size.Height, reference.h, context + " height");
            if (actual.TryGetEraser(id, out var frame) && !frame.Group)
            {
                Equal(frame.CenterPx.X, rect.X + rect.Width / 2, context + " raw center X");
                Equal(frame.CenterPx.Y, rect.Y + rect.Height / 2, context + " raw center Y");
            }
        }
        var group = actual.GroupSize();
        var expectedGroup = expected.PalmSizeDiu();
        Equal(group.Width, expectedGroup.w * actual.Profile.DpiX, context + " group width");
        Equal(group.Height, expectedGroup.h * actual.Profile.DpiY, context + " group height");
    }

    private static int Main()
    {
        var random = new Random(71906);
        for (int scenario = 0; scenario < 576; scenario++)
        {
            ReferenceClock.Now = 10000;
            var options = new PalmEraserOptions
            {
                FollowSize = (scenario & 1) == 0, LockSize = (scenario & 2) != 0,
                Smooth = (scenario & 4) != 0, PressureEnabled = (scenario & 8) != 0,
                MultiTouch = (scenario & 16) != 0, AreaEnabled = (scenario & 32) == 0,
                Limit = (PalmSizeLimit)((scenario / 64) % 3), Aspect = (PalmAspectSource)((scenario / 192) % 3),
                CustomAspect = 1.7, AreaTrim = 0.5 + random.NextDouble() * 1.5,
                Source = (PalmSourceMode)(scenario % 3),
                PressureThreshold = random.NextDouble(), PressureMaximum = 1 + random.NextDouble() * 2,
                MinimumContacts = 2 + scenario % 4, GroupWindowMs = scenario % 2 == 0 ? 200 : 0,
                GroupTolerance = 1 + random.NextDouble()
            };
            var profile = new PalmEraserProfile
            {
                DpiX = 1 + (scenario % 3) * 0.5, DpiY = 1 + (scenario % 3) * 0.5,
                PalmWidthPx = scenario % 7 == 0 ? 0 : 400, PalmHeightPx = 250,
                PalmContactAreaPx2 = scenario % 5 == 0 ? 0 : 2000, FingerContactAreaPx2 = 100,
                Ellipse = (scenario & 1) != 0, Options = options
            };
            var actual = new PalmEraserEngine(() => ReferenceClock.Now);
            actual.Configure(profile);
            var expected = Oracle(profile);
            for (int step = 0; step < 120; step++)
            {
                ReferenceClock.Now += step % 19 == 0 ? 350 : random.Next(0, 40);
                if (step % 11 == 0)
                {
                    double pressure = random.NextDouble();
                    actual.NotePressure(pressure); expected.NotePressure(pressure);
                }
                int id = step % 5;
                var source = step % 3 == 0 ? PalmContactSource.RawHid : PalmContactSource.Software;
                string device = step % 17 == 0 ? "screen-b" : "screen-a";
                var rect = new Rect(step % 13 == 0 ? 700 : id * 30 + step % 4,
                    id * 15 + step % 3, step % 9 == 0 ? 0 : 5 + random.NextDouble() * 120, 5 + random.NextDouble() * 90);
                actual.Submit(source, id, rect, source == PalmContactSource.Software, device);
                expected.Submit((Reference.ContactSource)source, id, rect, source == PalmContactSource.Software, "", device);
                if (step % 7 == 0) { actual.Tick(); expected.Tick(); }
                Compare(actual, expected, $"scenario {scenario}, step {step}");
            }
        }
        // HID stretch/isotropic conversions must conserve area exactly across display shapes.
        foreach (int width in new[] { 1920, 2560, 3840 })
            foreach (var scale in Enum.GetValues<PalmHidScale>())
            {
                var size = PalmEraserEngine.HidDimensions(20, 30, 32767, 65535, width, 1080, scale);
                var expected = Reference.HidScale.Factors(32767, 65535, width, 1080, (Reference.HidSizeScale)scale);
                Equal(size.Width, expected.fw * 20, "HID width"); Equal(size.Height, expected.fh * 30, "HID height");
            }
        Console.WriteLine($"PASS: {_checks} comparisons against TouchSuite 719f06e0 (576 deterministic scenarios)");
        return 0;
    }
}
