using System.Windows;
using Ink_Canvas.Ink;

internal static class PalmEngineRegression
{
    internal static void Run(Action<bool, string> check)
    {
        long time = 10000;
        var options = new PalmEraserOptions { Source = PalmSourceMode.Software, Smooth = false };
        var profile = new PalmEraserProfile { Options = options, PalmWidthPx = 100, PalmHeightPx = 80,
            PalmContactAreaPx2 = 800, FingerContactAreaPx2 = 40 };
        var engine = new PalmEraserEngine(() => time);
        engine.Configure(profile);
        void Sample(int id, double x, double y, double width = 5, double height = 5) =>
            engine.Submit(PalmContactSource.Software, id, new Rect(x - width / 2, y - height / 2, width, height));
        Sample(1, 10, 10); time += 30; Sample(2, 20, 20); time += 30; Sample(3, 30, 30);
        time = 10199; Sample(1, 11, 10);
        check(!engine.TryGetEraser(1, out _), "Palm group decided before anchor's observation window");
        time++; Sample(1, 12, 10);
        check(engine.TryGetEraser(1, out var group) && group.Group, "Three nearby contacts never formed a palm");
        check(Math.Abs(group.CenterPx.X - 62.0 / 3) < 1e-9 && group.WidthPx * group.HeightPx == 8000,
            "Group eraser did not use one calibrated palm at the raw centroid");
        int revision = group.Revision;
        engine.Remove(PalmContactSource.Software, 2);
        check(engine.TryGetEraser(1, out group) && group.Revision != revision,
            "Lifting a group member did not break centroid interpolation");
        Sample(1, 1000, 1000);
        check(engine.TryGetEraser(1, out _), "Decided palm group changed classification during the same press");

        engine.Configure(profile with { Options = options with { MultiTouch = false } });
        Sample(1, 10, 10, 40, 40);
        check(engine.TryGetEraser(1, out _), "Contact area did not classify a palm");
        for (int i = 0; i < 5; i++) Sample(1, 11 + i, 10, 5, 5);
        check(engine.IsWriting(1), "Unlocked classification stayed latched after median contact area shrank");
        engine.Configure(profile with { Options = options with { MultiTouch = false, LockSize = true } });
        Sample(1, 10, 10, 40, 40);
        engine.TryGetEraser(1, out var large);
        for (int i = 0; i < 5; i++) Sample(1, 11 + i, 10, 5, 5);
        check(engine.TryGetEraser(1, out var locked) && locked.WidthPx * locked.HeightPx >= large.WidthPx * large.HeightPx,
            "Locked palm lost its classification or peak area");
        engine.Remove(PalmContactSource.Software, 1); Sample(1, 10, 10);
        check(engine.IsWriting(1), "Reused contact ID inherited an old palm latch");

        engine.Configure(profile with { Options = options with { Source = PalmSourceMode.Auto } });
        engine.Submit(PalmContactSource.RawHid, 1, new Rect(0, 0, 30, 30), false, "screen-a");
        time += 300;
        engine.Submit(PalmContactSource.RawHid, 1, new Rect(1, 0, 30, 30), false, "screen-a");
        check(engine.LockedSource == PalmContactSource.RawHid, "Stable HID source did not lock");
        check(!engine.Submit(PalmContactSource.RawHid, 2, new Rect(0, 0, 500, 500), false, "screen-b"),
            "A mirrored second touchscreen injected contacts into the first device");
        time += 301; engine.Tick();
        check(engine.Count == 0 && engine.LockedSource == PalmContactSource.RawHid,
            "Idle expiry retained stale contacts or unlocked the selected source");
        check(!engine.Submit(PalmContactSource.Software, 9, new Rect(0, 0, 30, 30)),
            "Software input replaced a locked HID source after idle");

        foreach (double dpi in new[] { 1.0, 1.5, 2.0 })
        {
            var environment = new TouchCalibrationEnvironment { Source = "WPF", Display = "A", PixelWidth = 1920,
                PixelHeight = 1080, DpiX = dpi, DpiY = dpi };
            var record = new TouchCalibrationRecord { FingerArea = 40, PalmArea = 800, OutlineWidth = 100,
                OutlineHeight = 80, AreaMultiplier = 10, SizeAdjustment = 1, Environment = environment };
            check(record.IsValid(), "Existing version 1 calibration became unreadable");
            var converted = PalmEraserProfiles.Create(record, environment, options, true);
            check(converted.ThresholdPx2 == 420 * dpi * dpi && converted.PalmWidthPx == 100 * dpi,
                "DIP calibration was mixed with physical contact pixels");
            engine.Configure(converted); Sample(1, 10, 10, 40 * dpi, 40 * dpi);
            engine.TryGetEraser(1, out var frame);
            double areaDip = frame.WidthPx / dpi * frame.HeightPx / dpi * Math.PI / 4;
            check(Math.Abs(areaDip - 16000) < 1e-7, "Ellipse area changed with Windows display scaling");
        }
        check(new PalmEraserOptions { AreaTrim = double.NaN, MinimumContacts = -1, PressureMaximum = double.PositiveInfinity }
            .Sanitize() is { AreaTrim: 1, MinimumContacts: 2, PressureMaximum: 2 }, "Malformed persisted options escaped normalization");

        var scheduled = new Queue<Action>(); var delivered = new List<int>(); int resets = 0;
        using var mailbox = new PalmInputMailbox<int>(scheduled.Enqueue, delivered.Add, () => resets++);
        for (int i = 0; i < 1000; i++) mailbox.Post(i);
        check(scheduled.Count == 1, "Raw HID posted an unbounded number of ink-thread work items");
        scheduled.Dequeue()();
        check(resets == 1 && delivered.Count <= 256 && delivered[^1] == 999, "HID overflow lost reset or latest contact");
        mailbox.Post(1000); mailbox.Dispose(); scheduled.Dequeue()();
        check(delivered[^1] == 999, "Disposed HID mailbox invoked a retired ink host");
    }
}
