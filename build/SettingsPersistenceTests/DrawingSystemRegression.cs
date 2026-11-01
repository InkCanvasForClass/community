using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Ink_Canvas;
using Ink_Canvas.Controls;
using Ink_Canvas.Helpers;
using Ink_Canvas.Windows.SettingsViews.Helpers;

internal static class DrawingSystemRegression
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static Stroke Make(double x = 10) => new(new StylusPointCollection { new StylusPoint(x, 20, .2f), new StylusPoint(x + 30, 20, .8f) })
        { DrawingAttributes = new DrawingAttributes { Width = 5, Height = 5, Color = Colors.Black } };
    private static byte[] Render(Visual visual)
    {
        var bitmap = new RenderTargetBitmap(200, 120, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual); var pixels = new byte[200 * 120 * 4]; bitmap.CopyPixels(pixels, 800, 0); return pixels;
    }
    private static bool Ink(byte[] data, int x, int y) => data[(y * 200 + x) * 4 + 3] > 128;
    private static void Layout(FrameworkElement view)
    {
        view.Measure(new Size(200, 120)); view.Arrange(new Rect(0, 0, 200, 120)); view.UpdateLayout();
        var frame = new System.Windows.Threading.DispatcherFrame();
        var timer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.ApplicationIdle)
            { Interval = TimeSpan.FromMilliseconds(5) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); System.Windows.Threading.Dispatcher.PushFrame(frame);
    }
    private sealed class DrawingHost(Drawing drawing) : FrameworkElement
    {
        protected override void OnRender(DrawingContext dc) => dc.DrawDrawing(drawing);
    }
    private sealed class Scene : FrameworkElement
    {
        public bool HitTestLocalPoint(Point point, double tolerance) => point.X >= -tolerance && point.Y >= -tolerance;
        public bool IntersectsLocalRect(Rect rectangle, double tolerance) => rectangle.IntersectsWith(new Rect(-tolerance, -tolerance, 20, 20));
    }

    internal static void SegmentFingerprint(Action<bool, string> check)
    {
        var manager = (InkFadeManager)RuntimeHelpers.GetUninitializedObject(typeof(InkFadeManager));
        var method = typeof(InkFadeManager).GetMethod("CreateStrokeSegment", Hidden);
        using var stream = new System.IO.MemoryStream();
        foreach (bool highlighter in new[] { false, true })
        foreach (bool rectangular in new[] { false, true })
        foreach (bool endCaps in new[] { false, true })
        {
            var stroke = new Stroke(new StylusPointCollection(Enumerable.Range(0, 30).Select(i =>
                new StylusPoint(20 + i * 3, 40 + Math.Sin(i * .3) * 15, .1f + i * .025f))));
            stroke.DrawingAttributes = new DrawingAttributes { Color = Colors.Red, Width = 5, Height = 8, IsHighlighter = highlighter,
                StylusTip = rectangular ? StylusTip.Rectangle : StylusTip.Ellipse };
            object segment = method.Invoke(manager, new object[] { stroke, 0, 29, endCaps, endCaps });
            FrameworkElement view = segment as FrameworkElement ?? new DrawingHost((Drawing)segment);
            Layout(view); var pixels = Render(view); stream.Write(pixels);
            check(pixels.Any(b => b != 0), "Fade segment rendered no pixels");
        }
        Console.WriteLine("FADE_SEGMENT_FINGERPRINT: " + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream.ToArray())));
    }

    internal static void Run(Action<bool, string> check)
    {
        var assembly = typeof(MainWindow).Assembly;
        var owner = new HighlighterInkCanvas { Width = 200, Height = 120, Background = Brushes.Transparent };
        Layout(owner);
        int changes = 0; owner.Strokes.StrokesChanged += (_, _) => changes++;
        var type = assembly.GetType("Ink_Canvas.Controls.ShapePreviewSession");
        var preview = Activator.CreateInstance(type, Hidden, null, new object[] { owner }, null);
        var strokes = (StrokeCollection)type.GetProperty("Strokes", Hidden).GetValue(preview);
        void Call(string name) => type.GetMethod(name, Hidden).Invoke(preview, null);
        Call("Attach");
        Stroke final = null;
        for (int i = 0; i < 500; i++) { strokes.Clear(); strokes.Add(final = Make(i % 50)); }
        check(owner.Strokes.Count == 0 && changes == 0 && owner.Children.Count == 1, "Shape preview polluted formal ink/history or leaked controls");
        Call("Commit");
        check(owner.Strokes.Count == 1 && changes == 1 && ReferenceEquals(owner.Strokes[0], final), "Shape commit changed identity or sent multiple formal updates");
        check(owner.Children.Count == 0 && strokes.Count == 0, "Committed shape retained its preview layer");
        Call("Attach"); strokes.Add(Make()); Call("Clear");
        check(owner.Strokes.Count == 1 && changes == 1 && owner.Children.Count == 0, "Canceling a preview changed the document");

        // Exercise the production geometry generator without constructing an app
        // window or starting Office/camera services. These branches use only the
        // injected canvas, preview session and primitive geometry state.
        var generator = (MainWindow)RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
        void Field(string name, object value) => typeof(MainWindow).GetField(name, Hidden).SetValue(generator, value);
        Field("inkCanvas", owner); Field("_shapePreview", preview); Field("_buildingTransientShape", true);
        Field("iniP", new Point(10, 10));
        SettingsManager.Settings.Canvas.FitToCurve = false;
        SettingsManager.Settings.Canvas.ShowCircleCenter = false;
        owner.Strokes.Clear(); int formalChanges = changes;
        foreach (int mode in new[] { 1, 2, 3, 4, 5, 8, 10 })
        {
            Call("Attach"); Field("drawingShapeMode", mode); Field("lastTempStroke", null);
            Field("lastTempStrokeCollection", new StrokeCollection());
            for (int i = 0; i < 20; i++)
                typeof(MainWindow).GetMethod("DrawShapePreview", Hidden).Invoke(generator, new object[] { new Point(70 + i, 60 + i) });
            check(strokes.Count > 0, $"Shape mode {mode} produced no preview");
            check(owner.Strokes.Count == 0 && changes == formalChanges, $"Shape mode {mode} wrote transient points into formal ink");
            Call("Clear");
        }

        owner.Strokes.Clear(); var original = Make(); owner.Strokes.Add(original);
        var image = new System.Windows.Shapes.Rectangle { Width = 10, Height = 10, Fill = Brushes.Blue };
        InkCanvas.SetLeft(image, 70); InkCanvas.SetTop(image, 10); owner.Children.Add(image); Layout(owner);
        var setView = typeof(HighlighterInkCanvas).GetMethod("SetPresentationTransform", Hidden, null, new[] { typeof(Matrix?), typeof(bool) }, null);
        setView.Invoke(owner, new object[] { (Matrix?)new Matrix(1, 0, 0, 1, 30, 15), false });
        Layout(owner);
        var pixels = Render(owner);
        Console.WriteLine($"View probe: sourceX={original.StylusPoints[0].X}, baseInk={Ink(pixels, 20, 20)}, movedInk={Ink(pixels, 50, 35)}, baseImage={Ink(pixels,75,15)}, movedImage={Ink(pixels,105,30)}, visibleBytes={pixels.Count(v => v > 0)}");
        check(original.StylusPoints[0].X == 10 && Ink(pixels, 50, 35), "View preview mutated source points or failed to move ink");
        check(Ink(pixels, 105, 30) && !Ink(pixels, 75, 15), "View preview did not keep child images aligned with ink");
        setView.Invoke(owner, new object[] { null, false });
        Layout(owner);
        check(Ink(Render(owner), 75, 15), "Ending view preview did not restore the original image transform");

        // A capture/tool interruption must flush an outstanding presentation
        // matrix even if the pointer flag was already cleared by a two-finger
        // transition or a popup drag. No app window/services are started here.
        foreach (string listField in new[] { "circles", "_boardRoamingContacts" })
        {
            var field = typeof(MainWindow).GetField(listField, Hidden);
            field.SetValue(generator, Activator.CreateInstance(field.FieldType));
        }
        Field("_boardRoamingPendingTransform", new Matrix(1, 0, 0, 1, 30, 15));
        Field("_boardRoamingPendingScale", 1d);
        Field("_boardRoamingViewPending", true);
        Field("_isBoardRoamingPopupDragActive", true);
        setView.Invoke(owner, new object[] { (Matrix?)new Matrix(1, 0, 0, 1, 30, 15), false });
        typeof(MainWindow).GetMethod("CancelBoardRoamingInteraction", Hidden).Invoke(generator, null);
        check(original.StylusPoints[0].X == 40 && original.StylusPoints[0].Y == 35,
            "Interrupted roaming dropped its pending source transform");
        check(!(bool)typeof(MainWindow).GetField("_boardRoamingViewPending", Hidden).GetValue(generator)
            && !(bool)typeof(MainWindow).GetField("_isBoardRoamingPopupDragActive", Hidden).GetValue(generator),
            "Interrupted roaming retained pending input/view state");
        typeof(MainWindow).GetMethod("CancelBoardRoamingInteraction", Hidden).Invoke(generator, null);
        check(original.StylusPoints[0].X == 40, "Roaming recovery applied the same transform twice");

        var canvas = new VisualCanvas { Width = 200, Height = 120 }; Layout(canvas);
        var visual = new StrokeVisual(new DrawingAttributes { Color = Colors.Black, Width = 5, Height = 5 }); visual.SetVisualCanvas(canvas);
        visual.Add(new StylusPoint(10, 20, .2f)); visual.Add(new StylusPoint(40, 20, .8f));
        var setTip = typeof(StrokeVisual).GetMethod("SetPreviewTip", Hidden);
        var commitTip = typeof(StrokeVisual).GetMethod("CommitPreviewTip", Hidden);
        setTip.Invoke(visual, new object[] { (StylusPoint?)new StylusPoint(60, 20, .9f) }); visual.Redraw();
        check(visual.Stroke.StylusPoints.Count == 2 && Ink(Render(canvas), 59, 20), "Tip preview added permanent points or lagged behind the latest filtered sample");
        commitTip.Invoke(visual, null); commitTip.Invoke(visual, null); visual.Redraw();
        check(visual.Stroke.StylusPoints.Count == 3 && visual.Stroke.StylusPoints[^1].X == 60, "Pen-up lost or duplicated the preview endpoint");
        setTip.Invoke(visual, new object[] { (StylusPoint?)new StylusPoint(90, 20, .9f) }); visual.ForceRedraw();
        check(!Ink(Render(canvas), 88, 20), "Force redraw resurrected a canceled tip");

        original = Make();
        var history = new TimeMachine(); var before = original.StylusPoints.Clone(); var after = original.StylusPoints;
        var mutation = new Dictionary<Stroke, Tuple<StylusPointCollection, StylusPointCollection>> { [original] = Tuple.Create(before, after) };
        history.CommitStrokeManipulationHistory(mutation);
        before[0] = new StylusPoint(88, 20); after[0] = new StylusPoint(99, 20);
        var recorded = history.Undo().StylusPointDictionary[original];
        check(recorded.Item1[0].X == 10 && recorded.Item2[0].X == 10, "Gesture history retained live endpoints after commit");
        check(!ReferenceEquals(mutation[original].Item2, after), "History callers cannot safely reuse the committed endpoint as a baseline");
        var historyCanvas = new InkCanvas(); historyCanvas.Strokes.Add(original);
        var applyHistory = typeof(MainWindow).GetMethod("ApplyHistoryToCanvas", Hidden);
        var applied = new TimeMachineHistory(new Dictionary<Stroke, Tuple<StylusPointCollection, StylusPointCollection>>
            { [original] = recorded }, TimeMachineHistoryType.Manipulation);
        applyHistory.Invoke(generator, new object[] { applied, historyCanvas, null });
        original.StylusPoints[0] = new StylusPoint(123, 20);
        check(recorded.Item2[0].X == 10, "Replaying manipulation history shared a mutable historical point collection");

        var cacheType = assembly.GetType("Ink_Canvas.Ink.InactiveStrokeSnapshotCache`1").MakeGenericType(typeof(object));
        object cache = Activator.CreateInstance(cacheType, true); object version = new(); int captured = 0;
        var capture = cacheType.GetMethod("Capture", Hidden);
        Func<StrokeCollection> source = () => { captured++; return new StrokeCollection { Make() }; };
        object first = capture.Invoke(cache, new object[] { version, source });
        object second = capture.Invoke(cache, new object[] { version, source });
        check(ReferenceEquals(first, second) && captured == 1, "Unchanged inactive-page version was reconstructed twice");
        cacheType.GetMethod("Invalidate", Hidden).Invoke(cache, new[] { version });
        check(!ReferenceEquals(first, capture.Invoke(cache, new object[] { version, source })) && captured == 2, "Active-page invalidation reused stale save ink");
        check(!ReferenceEquals(first, capture.Invoke(cache, new object[] { new object(), source })), "Different page versions shared a snapshot");
        var activity = assembly.GetType("Ink_Canvas.Helpers.DrawingActivity");
        activity.GetMethod("Record", Hidden).Invoke(null, null);
        check(!(bool)activity.GetMethod("IsQuiet", Hidden).Invoke(null, new object[] { 1000 }), "Drawing did not suppress expensive periodic work");
        var scene = new Scene();
        var pointHit = typeof(MainWindow).GetMethod("InvokeSceneHitTest", Hidden).CreateDelegate<Func<FrameworkElement, Point, double, bool>>();
        var rectangleHit = typeof(MainWindow).GetMethod("InvokeSceneRectTest", Hidden).CreateDelegate<Func<FrameworkElement, Rect, double, bool>>();
        check(pointHit(scene, new Point(5, 5), 4) && !pointHit(scene, new Point(-50, -50), 4), "Scene point adapter changed hit results");
        check(rectangleHit(scene, new Rect(5, 5, 10, 10), 4) && !rectangleHit(scene, new Rect(100, 100, 10, 10), 4), "Scene rectangle adapter changed hit results");
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) pointHit(scene, new Point(5, 5), 4);
        check(GC.GetAllocatedBytesForCurrentThread() - allocated < 1024, "Scene erasing still allocates reflection arguments for every hit");
    }
}
