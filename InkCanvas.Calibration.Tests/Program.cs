using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Ink;
using System.Windows.Input;
using Ink_Canvas;
using Ink_Canvas.Helpers;
using Ink_Canvas.Ink;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using Ink_Canvas.Windows.SettingsViews.Pages;
using InkCanvasPPTAgent.Contracts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace InkCanvas.Calibration.Tests
{
    internal static class Program
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        [STAThread]
        private static int Main(string[] args)
        {
            var app = new Application();
            app.Resources[typeof(TextBlock)] = new Style(typeof(TextBlock));
            string testRoot = Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N"));
            App.RootPath = testRoot;
            SettingsManager.Settings = new Settings();
            int failed = 0;
            var tests = new Action[] { PageConstruction, PointRouteSaves, AreaRouteSaves, FailedSaveRetainsDraft,
                PalmGroupInputTests.RunAll, CalibrationPreviewSuppressesRemainingFingers, CalibrationPreviewOwnsTouchInput,
                TemporaryPalmModePreservesAnnotation, PendingPointGroupPreservesAnnotation, TemporaryPalmModeDoesNotNotifyPlugin,
                EraserSizeSelectionAppliesImmediately, PptEndUsesManagedSnapshot, PptEndRequestDoesNotBlockCaller,
                AutoStartMigrationPreservesOtherSettings, AutoStartMigrationKeepsExplicitValues,
                AutoStartMigrationRetriesAfterWriteFailure, AutoStartMigrationPreservesInvalidConfig,
                AutoStartMigrationCreatesMissingConfig, AutoStartMigrationReadsCurrentConfig,
                AutoStartMigrationHandlesMissingStartup, BoardRoamingTests.RunAll };
            if (Array.IndexOf(args, "--palm-only") >= 0) tests = new Action[] { PalmGroupInputTests.RunAll };
            if (Array.IndexOf(args, "--roaming-only") >= 0) tests = new Action[] { BoardRoamingTests.RunAll };
            if (Array.IndexOf(args, "--roaming-benchmark") >= 0) tests = new Action[] { BoardRoamingTests.Benchmark };
            if (Array.IndexOf(args, "--calibration-preview-only") >= 0)
                tests = new Action[] { CalibrationPreviewSuppressesRemainingFingers, CalibrationPreviewOwnsTouchInput };
            foreach (var test in tests)
            {
                try { test(); Console.WriteLine("PASS " + test.Method.Name); }
                catch (Exception ex) { failed++; Console.WriteLine("FAIL " + test.Method.Name + ": " + ex.GetBaseException()); }
            }
            DeleteDirectoryWithRetry(testRoot);
            Console.WriteLine($"Calibration UI: {failed} failures");
            return failed == 0 ? 0 : 1;
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void DeleteDirectoryWithRetry(string path)
        {
            if (!Directory.Exists(path)) return;

            for (int attempt = 0; attempt < 10; attempt++)
            {
                try
                {
                    Directory.Delete(path, true);
                    return;
                }
                catch (IOException) when (attempt < 9)
                {
                    Thread.Sleep(50);
                }
            }
        }

        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Members).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Members).SetValue(target, value);
        private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Members).Invoke(target, args);

        private static Button SaveButton(TouchCalibrationPage page) =>
            (page.FindName("SaveButton") ?? page.FindName("NextButton")) as Button;

        private static void WithAutoStartSettings(string json, Action<string> test)
        {
            string previousRoot = App.RootPath;
            var previousSettings = SettingsManager.Settings;
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            var cachedJsonProperty = typeof(App).GetProperty("CachedSettingsJson", flags);
            var parsedCacheField = typeof(App).GetField("_cachedParsedSettings", flags);
            object previousJson = cachedJsonProperty.GetValue(null);
            object previousParsed = parsedCacheField.GetValue(null);
            App.RootPath = Path.Combine(previousRoot, "auto-start", Guid.NewGuid().ToString("N"));
            SettingsManager.Settings = new Settings();
            string path = Path.Combine(App.RootPath, SettingsManager.SettingsFileName);
            try
            {
                if (json != null)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllText(path, json);
                }
                test(path);
            }
            finally
            {
                App.RootPath = previousRoot;
                SettingsManager.Settings = previousSettings;
                cachedJsonProperty.SetValue(null, previousJson);
                parsedCacheField.SetValue(null, previousParsed);
            }
        }

        private static JObject ParseRawSettings(string json) =>
            JsonConvert.DeserializeObject<JObject>(json, new JsonSerializerSettings
            {
                DateParseHandling = DateParseHandling.None
            });

        private static string CachedSettingsJson() => (string)typeof(App)
            .GetProperty("CachedSettingsJson", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

        private static void AutoStartMigrationPreservesOtherSettings()
        {
            const string json = "{\"startup\":{\"isAutoUpdate\":false,\"futureStartup\":{\"enabled\":true}},"
                + "\"appearance\":{\"language\":\"en-US\"},\"futureOptions\":{\"names\":[\"one\",\"two\"],"
                + "\"timestamp\":\"2026-11-03T07:55:38.0362429+08:00\"}}";
            WithAutoStartSettings(json, path =>
            {
                SettingsManager.Settings = JsonConvert.DeserializeObject<Settings>(json);
                bool expectedEnabled = AutoStartHelper.HasAnyRegistration();
                // 允许写入但不允许删除，确保迁移没有使用 File.Replace。
                using (var handle = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    Check(SettingsManager.MigrateAutoStartSetting(), "Auto-start migration could not write the existing file");
                }
                var expected = ParseRawSettings(json);
                expected["startup"]["runAtStartup"] = expectedEnabled;
                string saved = File.ReadAllText(path);
                Check(JToken.DeepEquals(expected, ParseRawSettings(saved)), "Auto-start migration changed unrelated settings");
                Check(SettingsManager.Settings.Startup.RunAtStartup == expectedEnabled, "Auto-start migration did not update live settings");
                Check(CachedSettingsJson() == saved, "Auto-start migration did not update the settings cache");
                Check(!File.Exists(path + ".tmp"), "Auto-start migration created a replacement file");
                using (var handle = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    Check(!SettingsManager.MigrateAutoStartSetting(), "Auto-start migration ran more than once");
                }
                Check(File.ReadAllText(path) == saved, "Repeated auto-start migration rewrote the file");
            });
        }

        private static void AutoStartMigrationKeepsExplicitValues()
        {
            foreach (bool enabled in new[] { false, true })
            {
                string json = JsonConvert.SerializeObject(new { startup = new { runAtStartup = enabled }, futureOption = 17 });
                WithAutoStartSettings(json, path =>
                {
                    SettingsManager.Settings = JsonConvert.DeserializeObject<Settings>(json);
                    using (var handle = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        Check(!SettingsManager.MigrateAutoStartSetting(), "Auto-start migration ignored an explicit setting");
                    }
                    Check(SettingsManager.Settings.Startup.RunAtStartup == enabled, "Auto-start migration changed an explicit value");
                    Check(File.ReadAllText(path) == json, "Auto-start migration rewrote an explicit setting");
                });
            }
        }

        private static void AutoStartMigrationRetriesAfterWriteFailure()
        {
            const string json = "{\"startup\":{\"isAutoUpdate\":false},\"futureOption\":17}";
            WithAutoStartSettings(json, path =>
            {
                SettingsManager.Settings = JsonConvert.DeserializeObject<Settings>(json);
                bool expectedEnabled = AutoStartHelper.HasAnyRegistration();
                string previousCache = CachedSettingsJson();
                using (var handle = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    Check(!SettingsManager.MigrateAutoStartSetting(), "Blocked auto-start migration reported success");
                    Check(File.ReadAllText(path) == json, "Blocked auto-start migration changed the original file");
                    Check(SettingsManager.Settings.Startup.RunAtStartup == expectedEnabled, "Failed migration lost the detected auto-start state");
                    Check(CachedSettingsJson() == previousCache, "Failed migration changed the settings cache");
                }
                Check(SettingsManager.MigrateAutoStartSetting(), "Auto-start migration could not retry after a write failure");
            });
        }

        private static void AutoStartMigrationPreservesInvalidConfig()
        {
            foreach (string json in new[] { "{", "null", "{\"startup\":[1,2],\"futureOption\":17}" })
            {
                WithAutoStartSettings(json, path =>
                {
                    Check(!SettingsManager.MigrateAutoStartSetting(), "Auto-start migration replaced an invalid config");
                    Check(File.ReadAllText(path) == json, "Auto-start migration discarded an invalid config");
                    File.WriteAllText(path, "{\"startup\":{}}");
                    Check(SettingsManager.MigrateAutoStartSetting(), "Auto-start migration could not retry after config repair");
                });
            }
        }

        private static void AutoStartMigrationCreatesMissingConfig()
        {
            WithAutoStartSettings(null, path =>
            {
                bool expectedEnabled = AutoStartHelper.HasAnyRegistration();
                Check(SettingsManager.MigrateAutoStartSetting(), "Auto-start migration did not create the missing config");
                var saved = ParseRawSettings(File.ReadAllText(path));
                Check(saved.Count == 1 && saved["startup"] is JObject startup && startup.Count == 1,
                    "Auto-start migration serialized unrelated defaults into the new config");
                Check(saved["startup"]["runAtStartup"].Value<bool>() == expectedEnabled, "The new config has the wrong auto-start state");
            });
        }

        private static void AutoStartMigrationReadsCurrentConfig()
        {
            const string original = "{\"startup\":{\"isAutoUpdate\":false},\"futureOption\":17}";
            const string latest = "{\"startup\":{\"isAutoUpdate\":true},\"futureOption\":42,\"addedOption\":\"keep\"}";
            WithAutoStartSettings(original, path =>
            {
                SettingsManager.Settings = JsonConvert.DeserializeObject<Settings>(original);
                typeof(App).GetMethod("UpdateCachedSettingsJson", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { original });
                File.WriteAllText(path, latest);
                bool expectedEnabled = AutoStartHelper.HasAnyRegistration();
                Check(SettingsManager.MigrateAutoStartSetting(), "Auto-start migration failed with a newer config");
                var expected = ParseRawSettings(latest);
                expected["startup"]["runAtStartup"] = expectedEnabled;
                Check(JToken.DeepEquals(expected, ParseRawSettings(File.ReadAllText(path))), "Auto-start migration overwrote newer file contents");
            });
        }

        private static void AutoStartMigrationHandlesMissingStartup()
        {
            foreach (string json in new[] { "{\"futureOption\":17}", "{\"Startup\":null,\"futureOption\":17}" })
            {
                WithAutoStartSettings(json, path =>
                {
                    SettingsManager.Settings = JsonConvert.DeserializeObject<Settings>(json);
                    Check(SettingsManager.MigrateAutoStartSetting(), "Auto-start migration could not add a missing startup section");
                    var saved = ParseRawSettings(File.ReadAllText(path));
                    Check(saved.Count == 2 && saved["futureOption"].Value<int>() == 17, "Auto-start migration changed unrelated root settings");
                    Check(saved.GetValue("startup", StringComparison.OrdinalIgnoreCase) is JObject startup && startup.Count == 1,
                        "Auto-start migration did not preserve the startup property name");
                });
            }
        }

        private static void PageConstruction() => _ = new TouchCalibrationPage();

        private static TouchCalibrationPage PointPreviewPage()
        {
            var page = new TouchCalibrationPage();
            Set(page, "_host", new Window());
            Field<TextBox>(page, "OutlineWidth").Text = "100";
            Field<TextBox>(page, "OutlineHeight").Text = "80";
            Field<Button>(page, "SimulateButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            var surface = Field<System.Windows.Controls.InkCanvas>(page, "Surface");
            surface.Measure(new Size(500, 500));
            surface.Arrange(new Rect(0, 0, 500, 500));
            return page;
        }

        private static void PreviewDown(TouchCalibrationPage page, long id, double x, double y) =>
            Call(page, "Begin", id, "WPF", new Point(x, y), 1d, 1d);

        private static void CalibrationPreviewSuppressesRemainingFingers()
        {
            foreach (bool pending in new[] { false, true })
            {
                var page = PointPreviewPage();
                var surface = Field<System.Windows.Controls.InkCanvas>(page, "Surface");
                var tracker = Field<object>(page, "_groupTracker");
                var existing = new Stroke(new StylusPointCollection { new StylusPoint(450, 450), new StylusPoint(480, 450) });
                surface.Strokes.Add(existing);
                try
                {
                    for (long id = 1; id <= 3; id++)
                    {
                        PreviewDown(page, id, id * 10, 20);
                        Call(tracker, "Up", id);
                        Call(tracker, "Down", id, id * 10d, 20d, long.MaxValue);
                    }
                    if (pending)
                    {
                        PreviewDown(page, 4, 200, 200);
                        Call(tracker, "Up", 4L);
                        Call(tracker, "Down", 4L, 200d, 200d, long.MaxValue);
                    }
                    long downTime = Environment.TickCount64 - 201;
                    for (long id = 1; id <= 3; id++)
                    {
                        Call(tracker, "Up", id);
                        Call(tracker, "Down", id, id * 10d, 20d, downTime);
                    }
                    Call(page, "EvaluatePointGroup");
                    Check((bool)Call(tracker, "IsPalm", 1L), "Calibration preview did not recognize the palm");
                    if (!pending) PreviewDown(page, 4, 200, 200);
                    Call(page, "Move", 4L, "WPF", new Point(220, 220), 1d, 1d);
                    Check(surface.Strokes.Count == 1 && surface.Strokes[0].StylusPoints.SequenceEqual(existing.StylusPoints),
                        $"Calibration preview leaked {surface.Strokes.Count - 1} finger stroke(s), pending={pending}");
                    for (long id = 1; id <= 3; id++) Call(page, "End", id, false);
                    Call(page, "Move", 4L, "WPF", new Point(240, 240), 1d, 1d);
                    PreviewDown(page, 5, 300, 300);
                    Check(surface.Strokes.Count == 1, "Calibration preview resumed writing before all palm-session contacts lifted");
                    Call(page, "End", 5L, false);
                    Call(page, "End", 4L, false);
                    Check(surface.Strokes.Count == 1 && surface.Strokes[0].StylusPoints.SequenceEqual(existing.StylusPoints), "Calibration preview left ink after lift or removed unrelated ink");
                    PreviewDown(page, 6, 200, 200);
                    Call(page, "Move", 6L, "WPF", new Point(220, 220), 1d, 1d);
                    Call(page, "End", 6L, false);
                    Check(surface.Strokes.Count == 2, "Calibration preview did not restore single-finger drawing after palm erasing");
                }
                finally { Call(page, "CancelPress"); }
            }
        }

        private static void CalibrationPreviewOwnsTouchInput()
        {
            var page = PointPreviewPage();
            var surface = Field<System.Windows.Controls.InkCanvas>(page, "Surface");
            try
            {
                Check(surface.EditingMode == InkCanvasEditingMode.Ink, "Idle calibration preview must allow drawing reference ink");
                PreviewDown(page, 1, 20, 20);
                Check(surface.EditingMode == InkCanvasEditingMode.None,
                    "Calibration preview leaves WPF built-in ink enabled while manually collecting touch strokes");
                Call(page, "End", 1L, false);
                Check(surface.EditingMode == InkCanvasEditingMode.Ink, "Lifting the final touch did not restore reference drawing");
                PreviewDown(page, 2, 20, 20);
                Call(page, "CancelPress");
                Check(surface.EditingMode == InkCanvasEditingMode.Ink, "Canceling calibration input did not restore reference drawing");
            }
            finally { Call(page, "CancelPress"); }
        }

        private static void PointRouteSaves()
        {
            var page = new TouchCalibrationPage();
            Set(page, "_host", new Window());
            Field<TextBox>(page, "OutlineWidth").Text = "100";
            Field<TextBox>(page, "OutlineHeight").Text = "80";
            Field<Button>(page, "SimulateButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            var save = SaveButton(page);
            Check(save.Visibility == Visibility.Visible && save.IsEnabled, "The position-only calibration route has no usable save button");
            Field<ComboBox>(page, "GroupCount").SelectedIndex = 2;
            Field<Slider>(page, "GroupWindow").Value = 300;
            Field<Slider>(page, "GroupTolerance").Value = 1.5;
            Field<Slider>(page, "SizeAdjustment").Value = 1.5;
            save.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            var record = SettingsManager.Settings.Advanced.TouchCalibration;
            Check(record?.IsValid() == true, "Position-only calibration was not saved");
            var restored = ReadSaved();
            Check(restored?.IsValid() == true && restored.OutlineWidth == 100 && restored.OutlineHeight == 80,
                "Position-only calibration was not persisted");
            Check(restored.SizeAdjustment == 1.5, "Size adjustment was not persisted");
            Check((int)restored.GetType().GetProperty("MinimumContacts").GetValue(restored) == 4, "Contact count was not persisted");
            Check((int)restored.GetType().GetProperty("WindowMilliseconds").GetValue(restored) == 300, "Time window was not persisted");
        }

        private static void AreaRouteSaves()
        {
            var page = new TouchCalibrationPage();
            Field<TextBox>(page, "OutlineWidth").Text = "60";
            Field<TextBox>(page, "OutlineHeight").Text = "60";
            Set(page, "_environment", new TouchCalibrationEnvironment
            {
                Source = "WPF", Display = "DISPLAY1", PixelWidth = 1920, PixelHeight = 1080, DpiX = 1, DpiY = 1
            });
            Field<Button>(page, "NextButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            AddPresses(Field<TouchCalibrationSampler>(page, "_palm"), 30);
            Field<Button>(page, "NextButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            AddPresses(Field<TouchCalibrationSampler>(page, "_finger"), 10);
            Field<Button>(page, "NextButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Field<Button>(page, "SkipButton").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            var save = SaveButton(page);
            Check(save.Visibility == Visibility.Visible && save.IsEnabled, "Area calibration has no usable save button");
            save.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Check(ReadSaved()?.PalmArea == 900, "Area calibration was not persisted");
        }

        private static void FailedSaveRetainsDraft()
        {
            var page = new TouchCalibrationPage();
            Set(page, "_step", 4);
            var draft = new TouchCalibrationRecord
            {
                FingerArea = 100, PalmArea = 900, OutlineWidth = 60, OutlineHeight = 60, AreaMultiplier = 4,
                Environment = new TouchCalibrationEnvironment
                { Source = "WPF", Display = "DISPLAY1", PixelWidth = 1920, PixelHeight = 1080, DpiX = 1, DpiY = 1 }
            };
            Set(page, "_draft", draft);
            Call(page, "ShowStep");
            var previous = SettingsManager.Settings.Advanced.TouchCalibration;
            string root = App.RootPath;
            string blocked = Path.Combine(root, "not-a-directory");
            File.WriteAllText(blocked, "test");
            App.RootPath = blocked;
            try
            {
                SaveButton(page).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Check(ReferenceEquals(SettingsManager.Settings.Advanced.TouchCalibration, previous), "Failed save changed the live calibration");
                Check(ReferenceEquals(Field<TouchCalibrationRecord>(page, "_draft"), draft), "Failed save discarded the draft");
                Check(SaveButton(page).Visibility == Visibility.Visible, "Failed save removed the retry button");
            }
            finally { App.RootPath = root; }
        }

        private static void AddPresses(TouchCalibrationSampler sampler, double side)
        {
            for (int i = 0; i < 3; i++)
            {
                sampler.BeginPress(true, 1, "WPF");
                sampler.AddSample(side, side, 0, 1, "WPF");
                sampler.EndPress();
            }
        }

        private static TouchCalibrationRecord ReadSaved() => JsonConvert.DeserializeObject<Settings>(
            File.ReadAllText(Path.Combine(App.RootPath, SettingsManager.SettingsFileName))).Advanced.TouchCalibration;

        // Avoid starting the full application's hardware, plugins and background services.
        // These methods only read the fields below and publish the pen-mode event.
        private static MainWindow PalmWindow()
        {
            var window = (MainWindow)FormatterServices.GetUninitializedObject(typeof(MainWindow));
            Set(window, "inkCanvas", new System.Windows.Controls.InkCanvas { EditingMode = InkCanvasEditingMode.None });
            Set(window, "isPalmEraserActive", true);
            Set(window, "palmEraserPreviousEditingMode", InkCanvasEditingMode.Ink);
            Set(window, "_changingPalmMode", true);
            return window;
        }

        private static void TemporaryPalmModePreservesAnnotation()
        {
            var window = PalmWindow();
            Check((bool)Call(window, "IsAnnotationModeActive"), "Palm activation falsely reports annotation exit");
            Set(window, "isPalmEraserActive", false);
            Set(window, "_changingPalmMode", false);
            Check(!(bool)Call(window, "IsAnnotationModeActive"), "Real annotation exit must still be reported");
        }

        private static void PendingPointGroupPreservesAnnotation()
        {
            var window = PalmWindow();
            Set(window, "isPalmEraserActive", false);
            Set(window, "_changingPalmMode", false);
            Set(window, "_palmGroupSavedMode", (InkCanvasEditingMode?)InkCanvasEditingMode.Ink);
            Check((bool)Call(window, "IsAnnotationModeActive"), "Waiting for the grouping window falsely reports annotation exit");
            Set(window, "_palmGroupSavedMode", null);
            Check(!(bool)Call(window, "IsAnnotationModeActive"), "Ending the group must not hide a real annotation exit");
        }

        private static void EraserSizeSelectionAppliesImmediately()
        {
            SettingsManager.Settings = new Settings();
            SettingsManager.Settings.Canvas.EraserShapeType = 1;
            var window = (MainWindow)FormatterServices.GetUninitializedObject(typeof(MainWindow));
            Set(window, "inkCanvas", new System.Windows.Controls.InkCanvas());
            Set(window, "isLoaded", true);
            window.eraserWidth = 64;

            var sizeSelector = new ComboBox { Name = "EraserSizeProbe" };
            for (int i = 0; i < 5; i++) sizeSelector.Items.Add(i);

            var eventWidths = new HashSet<double>();
            var savedIndices = new HashSet<int>();
            for (int i = 0; i < 5; i++)
            {
                sizeSelector.SelectedIndex = i;
                Call(window, "ComboBoxEraserSizeFloatingBar_SelectionChanged", sizeSelector, null);
                savedIndices.Add(SettingsManager.Settings.Canvas.EraserSize);
                eventWidths.Add(window.eraserWidth);
            }

            Check(savedIndices.Count == 5, $"Five eraser size choices saved only {savedIndices.Count} distinct index value(s)");
            Check(eventWidths.Count == 5,
                $"Five eraser size choices produced only {eventWidths.Count} actual hit-test width(s): {string.Join(", ", eventWidths)}");
        }

        private static void PptEndRequestDoesNotBlockCaller()
        {
            using var manager = new BlockingPptLinkManager();
            var window = (MainWindow)FormatterServices.GetUninitializedObject(typeof(MainWindow));
            Set(window, "_pptManager", manager);
            Set(window, "_pptEndSlideShowTaskLock", new object());

            var stopwatch = Stopwatch.StartNew();
            Call(window, "QueueEndSlideShow");
            stopwatch.Stop();

            Check(stopwatch.ElapsedMilliseconds < 500,
                $"QueueEndSlideShow blocked its caller for {stopwatch.ElapsedMilliseconds} ms");
            Check(manager.Started.Wait(TimeSpan.FromSeconds(2)),
                "The PPT end request never reached its worker");
            Check(manager.WorkerApartment == ApartmentState.STA,
                $"The PPT end request ran in {manager.WorkerApartment} instead of STA");

            var task = Field<Task<bool>>(window, "_pptEndSlideShowTask");
            manager.Release.Set();
            Check(task.Wait(TimeSpan.FromSeconds(2)) && task.Result,
                "The background PPT end request did not complete");
        }

        private sealed class BlockingPptLinkManager : IPPTLinkManager
        {
            public readonly ManualResetEventSlim Started = new ManualResetEventSlim();
            public readonly ManualResetEventSlim Release = new ManualResetEventSlim();
            public ApartmentState WorkerApartment { get; private set; }

            public event Action<object> SlideShowBegin { add { } remove { } }
            public event Action<object> SlideShowNextSlide { add { } remove { } }
            public event Action<object> SlideShowEnd { add { } remove { } }
            public event Action<object> PresentationOpen { add { } remove { } }
            public event Action<object> PresentationClose { add { } remove { } }
            public event Action<bool> PPTConnectionChanged { add { } remove { } }
            public event Action<bool> SlideShowStateChanged { add { } remove { } }

            public bool IsConnected => true;
            public bool IsInSlideShow => true;
            public bool IsSupportWPS { get; set; }
            public bool SkipAnimationsWhenNavigating { get; set; }
            public int SlidesCount => 1;
            public object PPTApplication => null;

            public void StartMonitoring() { }
            public void StopMonitoring(bool isShutdown = false) { }
            public void ReloadConnection() { }
            public bool TryStartSlideShow() => true;
            public bool TryEndSlideShow()
            {
                WorkerApartment = Thread.CurrentThread.GetApartmentState();
                Started.Set();
                return Release.Wait(TimeSpan.FromSeconds(5));
            }
            public bool TryNavigateToSlide(int slideNumber) => true;
            public bool TryNavigateNext() => true;
            public bool TryNavigatePrevious() => true;
            public int GetCurrentSlideNumber() => 1;
            public string GetPresentationName() => "Regression.pptx";
            public string GetPresentationPath() => @"C:\Presentations\Regression.pptx";
            public bool TryShowSlideNavigation() => true;
            public object GetCurrentActivePresentation() => null;
            public List<PPTSlideThumbnail> ExportSlideThumbnails(
                int width,
                int height,
                IProgress<double> progress = null,
                CancellationToken cancellationToken = default) => new List<PPTSlideThumbnail>();

            public void Dispose()
            {
                Release.Set();
                Started.Dispose();
                Release.Dispose();
            }
        }

        private static void PptEndUsesManagedSnapshot()
        {
            var linkManager = new ComPPTLinkManager();
            try
            {
                var manager = Field<PPTManager>(linkManager, "_inner");
                Set(manager, "_cachedPresentationName", "Regression.pptx");
                Set(manager, "_cachedPresentationPath", @"C:\Presentations\Regression.pptx");
                Set(manager, "_cachedCurrentSlideNumber", 7);
                manager.GetType().GetProperty(nameof(PPTManager.SlidesCount), Members).SetValue(manager, 12);

                object payload = null;
                linkManager.SlideShowEnd += value => payload = value;
                Call(manager, "OnSlideShowEnd", (object)null);

                Check(manager.GetPresentationName() == "Regression.pptx",
                    "Cached presentation name was lost before slideshow-end cleanup");
                Check(manager.GetPresentationPath() == @"C:\Presentations\Regression.pptx",
                    "Cached presentation path was lost before slideshow-end cleanup");
                Check(payload is PPTState state &&
                      state.PresentationName == "Regression.pptx" &&
                      state.PresentationFullName == @"C:\Presentations\Regression.pptx" &&
                      state.SlideIndex == 7 && state.TotalSlides == 12,
                    "COM slideshow-end leaked a live Presentation instead of a managed snapshot");
            }
            finally
            {
                linkManager.Dispose();
            }
        }

        private static void TemporaryPalmModeDoesNotNotifyPlugin()
        {
            var window = PalmWindow();
            var events = new List<bool>();
            typeof(MainWindow).GetEvent("PluginPenModeChanged", Members).GetAddMethod(true).Invoke(window, new object[] { new Action<bool>(events.Add) });
            Set(window, "_lastPluginPenMode", (bool?)true);
            Call(window, "NotifyPluginPenModeChanged", InkCanvasEditingMode.None);
            Check(events.Count == 0, "Temporary palm mode publishes a false pen-mode exit to plugins");
            Set(window, "_changingPalmMode", false);
            Set(window, "isPalmEraserActive", false);
            Call(window, "NotifyPluginPenModeChanged", InkCanvasEditingMode.None);
            Check(events.Count == 1 && !events[0], "Real pen-mode exit must still notify plugins");
        }
    }
}
