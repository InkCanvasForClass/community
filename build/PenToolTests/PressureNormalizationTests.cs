using System.Windows.Ink;
using System.Windows.Input;
using Ink_Canvas.Ink;

internal static class PressureNormalizationTests
{
    internal static void Run(Action<bool, string> check)
    {
        var uniform = new Stroke(new StylusPointCollection { new StylusPoint(10, 20), new StylusPoint(30, 40) });
        var original = uniform.StylusPoints;
        int replaced = 0;
        uniform.StylusPointsReplaced += (_, _) => replaced++;
        StrokePressure.Normalize(uniform);
        check(ReferenceEquals(original, uniform.StylusPoints) && replaced == 0,
            "Already normalized ink allocated a replacement or invalidated history");

        var description = new StylusPointDescription(new[] {
            new StylusPointPropertyInfo(StylusPointProperties.X), new StylusPointPropertyInfo(StylusPointProperties.Y),
            new StylusPointPropertyInfo(StylusPointProperties.NormalPressure),
            new StylusPointPropertyInfo(StylusPointProperties.XTiltOrientation),
            new StylusPointPropertyInfo(StylusPointProperties.YTiltOrientation) });
        var points = new StylusPointCollection(description) {
            new StylusPoint(10, 20, .1f, description, new[] { 30, -20 }),
            new StylusPoint(30, 40, .5f, description, new[] { 15, 25 }),
            new StylusPoint(50, 60, .9f, description, new[] { -30, 10 }) };
        var stroke = new Stroke(points);
        var attributes = stroke.DrawingAttributes;
        var marker = Guid.NewGuid(); stroke.AddPropertyData(marker, "persistent ink");
        int updates = 0;
        stroke.StylusPointsReplaced += (_, _) => updates++;
        StrokePressure.Normalize(stroke);
        check(updates == 1 && !ReferenceEquals(stroke.StylusPoints, points), "Pressure normalization did not publish one complete update");
        check(stroke.StylusPoints.All(p => p.PressureFactor == .5f), "Pressure normalization left varying pressure");
        check(points[0].PressureFactor == .1f && points[2].PressureFactor == .9f, "Pressure normalization mutated a shared input collection");
        check(stroke.StylusPoints.Select(p => (p.X, p.Y)).SequenceEqual(points.Select(p => (p.X, p.Y))), "Normalization changed ink coordinates");
        check(stroke.StylusPoints.Select(p => p.GetPropertyValue(StylusPointProperties.XTiltOrientation))
            .SequenceEqual(points.Select(p => p.GetPropertyValue(StylusPointProperties.XTiltOrientation)))
            && stroke.StylusPoints.Select(p => p.GetPropertyValue(StylusPointProperties.YTiltOrientation))
            .SequenceEqual(points.Select(p => p.GetPropertyValue(StylusPointProperties.YTiltOrientation))), "Normalization lost pen tilt");
        check(ReferenceEquals(stroke.DrawingAttributes, attributes) && (string)stroke.GetPropertyData(marker) == "persistent ink",
            "Normalization lost ink style or persistence tags");
        StrokePressure.Normalize(stroke);
        check(updates == 1, "Repeated normalization rebuilt unchanged ink");
        using (var guard = new StrokeProcessingGuard(stroke))
        {
            StrokePressure.Normalize(stroke);
            check(!guard.HasChanged, "Reading or normalizing unchanged pressure invalidated a worker result");
            stroke.Transform(new System.Windows.Media.Matrix(1, 0, 0, 1, 30, 40), false);
            check(guard.HasChanged, "Moving ink did not reject a stale smoothing result");
        }
        using (var guard = new StrokeProcessingGuard(stroke))
        {
            stroke.DrawingAttributes.Color = System.Windows.Media.Colors.Blue;
            check(guard.HasChanged, "Changing pen color did not reject stale worker style");
        }
        using (var guard = new StrokeProcessingGuard(stroke))
        {
            stroke.StylusPoints = stroke.StylusPoints.Clone();
            check(guard.HasChanged, "Replacing geometry did not invalidate a pending result");
        }
        var disposed = new StrokeProcessingGuard(stroke);
        disposed.Dispose(); disposed.Dispose();
        stroke.DrawingAttributes.Width += 1;
        check(!disposed.HasChanged, "Completed smoothing left an invalidation handler attached");
        using var stream = new System.IO.MemoryStream();
        new StrokeCollection { stroke }.Save(stream); stream.Position = 0;
        var loaded = new StrokeCollection(stream)[0];
        check(loaded.StylusPoints.Count == points.Count && loaded.StylusPoints.All(p => Math.Abs(p.PressureFactor - .5f) < .001f)
            && (string)loaded.GetPropertyData(marker) == "persistent ink", "Normalized ink failed ISF roundtrip");

        var page = new StrokeCollection(Enumerable.Range(0, 300).Select(s =>
            new Stroke(new StylusPointCollection(Enumerable.Range(0, 200).Select(i => new StylusPoint(i, s, .1f + .8f * i / 199))))));
        page.Clone();
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 20; i++) GC.KeepAlive(page.Clone());
        watch.Stop();
        Console.WriteLine($"Removed unused page-backup cost (300 strokes / 60,000 points): {watch.Elapsed.TotalMilliseconds / 20:F2}ms and {(GC.GetAllocatedBytesForCurrentThread() - allocated) / 20:N0}B per clone");
    }
}
