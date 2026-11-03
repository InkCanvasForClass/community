using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Ink_Canvas;
using Ink_Canvas.Windows.SettingsViews.Helpers;

internal static class Program
{
    private static int _checks;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++;
    }

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--interceptor-fixture")) return FloatingWindowNativeRegression.RunFixture();
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        string root = Path.Combine(Path.GetTempPath(), "ICC_SettingsRegression_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string previousRoot = Ink_Canvas.App.RootPath;
        var previousSettings = SettingsManager.Settings;
        try
        {
            Ink_Canvas.App.RootPath = root + Path.DirectorySeparatorChar;
            SettingsManager.Settings = new Settings();
            if (args.Contains("--native-caption-only"))
            {
                NativeCaptionRegression.Run(Check);
                Console.WriteLine($"PASS: {_checks} native caption checks"); return 0;
            }
            if (args.Contains("--drawing-segment-only"))
            {
                DrawingSystemRegression.SegmentFingerprint(Check);
                Console.WriteLine($"PASS: {_checks} fade geometry checks"); return 0;
            }
            if (args.Contains("--drawing-system-only"))
            {
                DrawingSystemRegression.Run(Check);
                Console.WriteLine($"PASS: {_checks} drawing-system behavior checks"); return 0;
            }
            if (args.Contains("--ink-resampling-only"))
            {
                InkBurstRegression.Resampling(Check);
                Console.WriteLine($"PASS: {_checks} resampling bounds and quality checks"); return 0;
            }
            if (args.Contains("--ink-burst-only"))
            {
                InkBurstRegression.Run(Check);
                Console.WriteLine($"PASS: {_checks} input-priority and smoothing-burst checks"); return 0;
            }
            if (args.Contains("--ink-focus-only"))
            {
                InkRenderingRegression.Run(Check);
                Console.WriteLine($"PASS: {_checks} production wet-ink rendering and history checks"); return 0;
            }
            if (args.Contains("--performance-only"))
            {
                PerformanceRegression.Run(Check);
                Console.WriteLine($"PASS: {_checks} performance behavior checks"); return 0;
            }
            if (args.Contains("--board-pen-icon-only"))
            {
                var button = new Ink_Canvas.Controls.BoardToolbarButton { IconGeometry = "M4,4 L20,4 20,20 4,20Z" };
                var apply = typeof(Ink_Canvas.MainWindow).Assembly.GetType("Ink_Canvas.Helpers.BoardPenIconPresentation")
                    .GetMethod("Apply", BindingFlags.NonPublic | BindingFlags.Static);
                void Apply(System.Windows.Media.Brush color, System.Windows.Media.Brush outline, bool needed) => apply.Invoke(null, new object[] { button, color, outline, needed });
                Apply(System.Windows.Media.Brushes.White, System.Windows.Media.Brushes.Black, true);
                Check(button.IconGeometryDrawing.Pen != null && button.IconBrush == System.Windows.Media.Brushes.White,
                    "White pen lost its silhouette or changed the ink indicator color");
                button.Measure(new Size(60, 50)); button.Arrange(new Rect(0, 0, 60, 50)); button.UpdateLayout();
                var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(60, 50, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                bitmap.Render(button);
                var pixels = new byte[60 * 50 * 4]; bitmap.CopyPixels(pixels, 60 * 4, 0);
                Check(Enumerable.Range(0, 60 * 50).Any(i => pixels[i * 4 + 3] > 128 && pixels[i * 4] < 60), "Rendered white pen had no visible contrasting pixels");
                Apply(System.Windows.Media.Brushes.Red, System.Windows.Media.Brushes.Black, false);
                Check(button.IconGeometryDrawing.Pen == null, "Vivid pen retained a stale contrast outline");
                Apply(System.Windows.Media.Brushes.Transparent, System.Windows.Media.Brushes.Black, true);
                Check(button.IconGeometryDrawing.Pen != null, "Transparent pen lost its silhouette");
                Apply(System.Windows.Media.Brushes.Black, System.Windows.Media.Brushes.White, true);
                Check(button.IconGeometryDrawing.Pen.Brush == System.Windows.Media.Brushes.White, "Dark toolbar contrast outline did not follow foreground");
                var retainedPen = button.IconGeometryDrawing.Pen;
                int invalidations = 0;
                button.IconGeometryDrawing.Changed += (_, _) => invalidations++;
                for (int i = 0; i < 1000; i++)
                    Apply(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Black),
                        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White), true);
                Check(invalidations == 0 && ReferenceEquals(retainedPen, button.IconGeometryDrawing.Pen),
                    "Repeated contrast updates rebuilt or invalidated an unchanged icon");
                Apply(System.Windows.Media.Brushes.Black, System.Windows.Media.Brushes.Red, true);
                Check(button.IconGeometryDrawing.Pen.Brush == System.Windows.Media.Brushes.Red && invalidations > 0,
                    "Contrast cache ignored an actual foreground/theme change");
                Console.WriteLine($"PASS: {_checks} board pen icon rendering checks"); return 0;
            }
            if (args.Contains("--booth-history-only"))
            {
                var history = new Ink_Canvas.Helpers.TimeMachine();
                var strokes = new System.Windows.Ink.StrokeCollection {
                    new System.Windows.Ink.Stroke(new System.Windows.Input.StylusPointCollection {
                        new System.Windows.Input.StylusPoint(10, 20), new System.Windows.Input.StylusPoint(30, 40) }) };
                history.CommitStrokeUserInputHistory(strokes);
                history.CommitStrokeUserInputHistory(new System.Windows.Ink.StrokeCollection());
                history.Undo();
                var type = history.GetType();
                var snapshot = type.GetMethod("CaptureContext", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(history, null);
                history.ClearStrokeHistory();
                Check(!history.CanUndo && !history.CanRedo, "Booth inherited board undo history");
                history.CommitStrokeUserInputHistory(new System.Windows.Ink.StrokeCollection());
                type.GetMethod("RestoreContext", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(history, new[] { snapshot });
                Check(history.CanUndo && history.CanRedo, "Board undo/redo position was lost across booth session");
                Check(ReferenceEquals(history.Undo().CurrentStroke, strokes), "Restored history lost original board stroke references");
                Check(!history.CanUndo && history.CanRedo, "Undo after returning from booth was inconsistent");
                Console.WriteLine($"PASS: {_checks} booth history isolation checks"); return 0;
            }
            if (args.Contains("--presentation-only"))
            {
                PresentationNavigationRegression.Run(Check);
                Console.WriteLine($"PASS: {_checks} production presentation navigation checks");
                return 0;
            }
            if (args.Contains("--word-navigation-only"))
            {
                WordNavigationRegression.Run(Check);
                Check(Wait(SettingsManager.TrySaveSettingsToFileAsync()), "Word navigation preferences could not be saved");
                var savedNavigation = Newtonsoft.Json.JsonConvert.DeserializeObject<Settings>(File.ReadAllText(Path.Combine(root, SettingsManager.SettingsFileName))).WordLink;
                Check(savedNavigation.NavigationScale == 1.1 && savedNavigation.HorizontalInset == 40 && savedNavigation.BottomInset == 30
                    && !savedNavigation.ShowZoom && !savedNavigation.UsePptAppearance && savedNavigation.AutoEnterFocusMode
                    && savedNavigation.FocusToolbarOpacity == .35, "Word navigation preferences did not survive a restart");
                Console.WriteLine($"PASS: {_checks} Word navigation and settings checks");
                return 0;
            }
            FloatingWindowNativeRegression.Run(Check);
            Check(Wait(SettingsManager.TrySaveSettingsToFileAsync()), "Initial settings could not be written");
            string path = Path.Combine(root, SettingsManager.SettingsFileName);
            Check(!ReadCompleted(path), "New install should start with incomplete setup");

            // Reproduce completing immediately after requesting a coalesced save.
            SettingsManager.SaveSettingsToFile();
            SettingsManager.Settings.Startup.HasShownOobe = true;
            SettingsManager.Settings.Canvas.HighlighterAlpha = 72;
            Check(Wait(SettingsManager.TrySaveSettingsToFileAsync()), "OOBE commit failed");
            Check(ReadCompleted(path), "OOBE commit returned before its completion flag reached disk");
            using (var saved = JsonDocument.Parse(File.ReadAllText(path)))
                Check(saved.RootElement.GetProperty("canvas").GetProperty("highlighterAlpha").GetDouble() == 72,
                    "Selected settings were lost during completion");
            Pump(250);
            Check(ReadCompleted(path), "An older delayed save reset the completed flag");
            var reloaded = Newtonsoft.Json.JsonConvert.DeserializeObject<Settings>(File.ReadAllText(path));
            Check(reloaded.Startup.HasShownOobe && reloaded.Canvas.HighlighterAlpha == 72,
                "Restart deserialization lost the completed wizard and selections");
            var cached = typeof(Ink_Canvas.App).GetProperty("CachedSettingsJson", BindingFlags.Static | BindingFlags.NonPublic);
            var cachedSettings = Newtonsoft.Json.JsonConvert.DeserializeObject<Settings>((string)cached.GetValue(null));
            Check(cachedSettings.Startup.HasShownOobe, "Startup cache retained incomplete OOBE state");

            // A genuinely unwritable destination must report failure instead of
            // letting the wizard close and pretending the settings were persisted.
            string blocked = Path.Combine(root, "blocked"); File.WriteAllText(blocked, "file, not directory");
            Ink_Canvas.App.RootPath = blocked + Path.DirectorySeparatorChar;
            Check(!Wait(SettingsManager.TrySaveSettingsToFileAsync()), "Write failure reported success");
            Console.WriteLine($"PASS: {_checks} settings persistence and native-window checks (production services and WPF dispatcher)");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally
        {
            Ink_Canvas.App.RootPath = previousRoot;
            SettingsManager.Settings = previousSettings;
            app.Shutdown();
        }
    }

    private static bool ReadCompleted(string path)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        return json.RootElement.GetProperty("startup").GetProperty("hasShownOobe").GetBoolean();
    }

    private static bool Wait(Task<bool> task)
    {
        var limit = DateTime.UtcNow.AddSeconds(15);
        while (!task.IsCompleted && DateTime.UtcNow < limit) Pump(5);
        if (!task.IsCompleted) throw new TimeoutException("Settings writer did not complete");
        return task.GetAwaiter().GetResult();
    }

    private static void Pump(int milliseconds)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle) { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
    }
}
