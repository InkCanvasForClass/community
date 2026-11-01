using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using Ink_Canvas;
using Ink_Canvas.Controls;
using Ink_Canvas.Controls.Toolbar.BoardToolbar;
using Ink_Canvas.Helpers;
using Ink_Canvas.Windows.SettingsViews.Helpers;

internal static class PerformanceRegression
{
    internal static void Run(Action<bool, string> check)
    {
        var assembly = typeof(MainWindow).Assembly;
        var reader = assembly.GetType("Ink_Canvas.Windows.SettingsViews.Helpers.SettingsSearchMetadata");
        var read = reader.GetMethod("Read", BindingFlags.NonPublic | BindingFlags.Static);
        var culture = CultureInfo.CurrentUICulture;
        try
        {
            foreach (string language in new[] { "zh-CN", "en-US" })
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
                var searchWatch = System.Diagnostics.Stopwatch.StartNew();
                var entries = ((System.Collections.IEnumerable)read.Invoke(null, new object[] { assembly, CancellationToken.None })).Cast<object>().ToArray();
                check(entries.Length > 150, "Declarative search omitted settings pages");
                check(entries.All(entry => !string.IsNullOrWhiteSpace((string)entry.GetType().GetProperty("SettingKey").GetValue(entry))),
                    "Unvisited search results cannot locate a setting without a key or header fallback");
                check(entries.Any(entry => (string)entry.GetType().GetProperty("PageType").GetValue(entry) == "Ink_Canvas.Windows.SettingsViews.Pages.WordPage"
                    && (string)entry.GetType().GetProperty("Text").GetValue(entry) == Ink_Canvas.Properties.WordStrings.Enable),
                    "Search metadata lost localized Word settings");
                Console.WriteLine($"Search metadata: {language}, {entries.Length} headers, {searchWatch.ElapsedMilliseconds}ms, no page constructors");
            }
        }
        finally { CultureInfo.CurrentUICulture = culture; }

        var config = new InkSmoothingConfig();
        foreach (var quality in Enum.GetValues<InkSmoothingConfig.SmoothingQuality>())
        {
            config.Quality = quality;
            config.ApplyQualitySettings();
            check(config.MaxConcurrentTasks <= 4 && config.MaxConcurrentTasks <= Math.Max(1, Environment.ProcessorCount - 1),
                "Automatic smoothing concurrency monopolizes CPU cores");
        }
        config.MaxConcurrentTasks = 12;
        typeof(InkSmoothingConfig).GetProperty("HasExplicitMaxConcurrentTasks", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(config, true);
        config.ApplyQualitySettings();
        check(config.MaxConcurrentTasks == 12, "Automatic concurrency overwrote manual preference");
        using var processor = new HardwareAcceleratedInkProcessor(80, 80);
        var stroke = new Stroke(new StylusPointCollection(Enumerable.Range(0, 40).Select(i => new StylusPoint(i, Math.Sin(i * .1) * 10, .1f + i * .02f))));
        var inline = (Stroke)typeof(HardwareAcceleratedInkProcessor).GetMethod("SmoothStrokeInline", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(processor, new object[] { stroke });
        check(!ReferenceEquals(inline, stroke) && inline.StylusPoints.Count > 2, "Synchronous smoothing fell back instead of computing");
        check(inline.StylusPoints.Select(p => p.PressureFactor).Distinct().Count() > 1, "Inline smoothing lost pressure variation");
        var smoothing = new AsyncAdvancedBezierSmoothing(System.Windows.Threading.Dispatcher.CurrentDispatcher);
        try
        {
            bool callbackOnUi = false;
            double originalWidth = stroke.DrawingAttributes.Width;
            var processing = smoothing.SmoothStrokeAsync(stroke, (source, output) =>
                callbackOnUi = ReferenceEquals(source, stroke) && Application.Current.Dispatcher.CheckAccess());
            stroke.StylusPoints[0] = new StylusPoint(10000, 10000);
            stroke.DrawingAttributes.Width = 99;
            var deadline = System.Diagnostics.Stopwatch.StartNew();
            while (!processing.IsCompleted && deadline.Elapsed.TotalSeconds < 10)
            {
                var frame = new System.Windows.Threading.DispatcherFrame();
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(5) };
                timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
                timer.Start(); System.Windows.Threading.Dispatcher.PushFrame(frame);
            }
            check(processing.IsCompleted && callbackOnUi, "Immutable smoothing did not complete on the UI dispatcher");
            var smoothed = processing.GetAwaiter().GetResult();
            check(smoothed.GetBounds().Right < 1000 && smoothed.DrawingAttributes.Width == originalWidth,
                "Editing a source stroke changed the worker's point/style snapshot");
        }
        finally { smoothing.Dispose(); }

        System.Windows.Media.Imaging.BitmapSource analysis;
        using (var frame = new System.Drawing.Bitmap(1920, 1080))
        {
            using (var graphics = System.Drawing.Graphics.FromImage(frame)) graphics.Clear(System.Drawing.Color.Red);
            analysis = (System.Windows.Media.Imaging.BitmapSource)typeof(CapturableVideoCaptureElement)
                .GetMethod("CreateAnalysisFrame", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { frame });
        }
        check(analysis.IsFrozen && analysis.PixelWidth == 500 && analysis.PixelHeight == 281,
            "Camera analysis did not bound resolution, preserve aspect ratio or detach from its producer");
        var pixel = new byte[3]; analysis.CopyPixels(new Int32Rect(250, 140, 1, 1), pixel, 3, 0);
        check(pixel[0] == 0 && pixel[1] == 0 && pixel[2] == 255, "Camera analysis lost pixel data after the capture frame was disposed");

        var host = new Host(); var left = new StackPanel(); var center = new StackPanel(); var right = new StackPanel();
        var component = new BoardToolbarComponentEntry { Id = "board.eraser" };
        component.SetSetting(BoardToolbarRegistry.ShowInBoothOnlySettingKey, true);
        var layout = new BoardToolbarLayoutSettings { Areas = new() { new BoardToolbarAreaEntry { Id = "center",
            Groups = new() { new BoardToolbarGroupEntry { Components = new() { component } } } } } };
        BoardToolbarRegistry.RebuildToolbar(host, left, center, right, layout, false);
        var original = host.FindView("board.eraser");
        check(original.Visibility == Visibility.Collapsed, "Booth-only button was visible on the board");
        BoardToolbarRegistry.RebuildToolbar(host, left, center, right, layout, true);
        check(ReferenceEquals(original, host.FindView("board.eraser")) && original.Visibility == Visibility.Visible,
            "Mode switch rebuilt a button or retained stale visibility");
        for (int i = 0; i < 20; i++) BoardToolbarRegistry.RebuildToolbar(host, left, center, right, layout, i % 2 == 0);
        ((BoardToolbarButton)original).ButtonBorderControl.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseUpEvent });
        check(host.EraseClicks == 1, "Reusing a toolbar button duplicated its click handlers");
        component.SetSetting("fixedWidth", 70.0);
        BoardToolbarRegistry.RebuildToolbar(host, left, center, right, layout, true);
        check(!ReferenceEquals(original, host.FindView("board.eraser")) && host.FindView("board.eraser").Width == 70,
            "Changed toolbar configuration reused stale presentation");
        layout.Areas[0].Groups[0].Components = Enumerable.Range(0, 12)
            .Select(_ => new BoardToolbarComponentEntry { Id = "board.eraser" }).ToList();
        BoardToolbarRegistry.RebuildToolbar(host, left, center, right, layout, true);
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 50; i++) BoardToolbarRegistry.RebuildToolbar(host, left, center, right, layout, true);
        watch.Stop();
        long reusedBytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        double reusedMs = watch.Elapsed.TotalMilliseconds;
        allocated = GC.GetAllocatedBytesForCurrentThread(); watch.Restart();
        for (int i = 0; i < 50; i++)
        {
            foreach (var entry in layout.Areas[0].Groups[0].Components) entry.SetSetting("fixedWidth", 60 + i);
            BoardToolbarRegistry.RebuildToolbar(host, left, center, right, layout, true);
        }
        watch.Stop();
        Console.WriteLine($"Toolbar 12 buttons / 50 rebuilds: reuse {reusedMs:F1}ms / {reusedBytes:N0}B; forced construction {watch.Elapsed.TotalMilliseconds:F1}ms / {GC.GetAllocatedBytesForCurrentThread() - allocated:N0}B");
    }

    private sealed class Host : IBoardToolbarHost
    {
        private readonly Dictionary<string, FrameworkElement> _views = new();
        internal int EraseClicks;
        public MainWindow Window => null;
        public void RegisterView(string id, FrameworkElement view) => _views[id] = view;
        public FrameworkElement FindView(string id) => _views.GetValueOrDefault(id);
        public void SelectEraser() => EraseClicks++;
        public void SwitchToPreviousPage() { } public void SwitchToNextPage() { } public void AddWhiteboardPage() { }
        public void DeleteWhiteboardPage() { } public void ToggleGesture() { } public void ChangeBackgroundColor() { }
        public void SelectTool() { } public void SelectRoaming() { } public void SelectPen() { }
        public void SelectStrokeEraser() { } public void SelectShape() { } public void InsertImage() { }
        public void Undo() { } public void Redo() { } public void ToggleInkFreeze() { } public void OpenTools() { }
        public void ExitWhiteboard() { } public void UpdatePageInfo() { }
        public bool CanUndo => false; public bool CanRedo => false; public bool CanSwitchToPreviousPage => false;
        public bool CanSwitchToNextPage => false; public bool CanAddNewPage => true; public bool CanDeletePage => false;
        public string CurrentPageInfo => "1/1";
    }
}
