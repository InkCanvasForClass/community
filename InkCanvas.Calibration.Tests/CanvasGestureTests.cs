using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Ink_Canvas;
using Ink_Canvas.Helpers;
using Ink_Canvas.Ink;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using Newtonsoft.Json;
using WpfInkCanvas = System.Windows.Controls.InkCanvas;

namespace InkCanvas.Calibration.Tests
{
    internal static class CanvasGestureTests
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static T Field<T>(object target, string name) =>
            (T)target.GetType().GetField(name, Members).GetValue(target);
        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, Members).SetValue(target, value);
        private static object Call(object target, string name, params object[] args) =>
            target.GetType().GetMethods(Members).Single(method => method.Name == name &&
                method.GetParameters().Length == args.Length).Invoke(target, args);
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        private static void Near(Point actual, Point expected, string message, double tolerance = 0.1)
        {
            Check((actual - expected).Length <= tolerance, $"{message}: expected {expected}, got {actual}");
        }

        public static void RunAll()
        {
            var previousSettings = SettingsManager.Settings;
            var shutdownMode = Application.Current.ShutdownMode;
            Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            int failed = 0;
            try
            {
                foreach (var test in new Action[] { TranslationWithAllPenStyles, TranslationWithoutManipulation,
                    TranslationWithMultiTouchWriting, TranslationInEditingModes, IndependentSceneSwitches,
                    PinchUsesFingerCenter, RotationUsesFingerCenter, SelectedInkDoesNotMoveOtherContent,
                    GestureDiscardsTemporaryInk, RemainingFingerDoesNotResumeWriting,
                    LateManipulationDoesNotTransformAgain, CaptureLossAllowsNextGesture,
                    PointGroupingAllowsNormalTwoFingerGesture, GestureHistoryIncludesInkAndImages,
                    PointGroupingRetainsRealPalmInput, DeactivationCancelsTheGesture,
                    AdditionalContactsDoNotJump, DisabledGesturesPreserveMultiTouchWriting,
                    FrozenAndPresentationPoliciesAreRespected, NextRealtimeStrokeCanStart })
                {
                    try { test(); Console.WriteLine("PASS Gesture " + test.Method.Name); }
                    catch (Exception ex)
                    {
                        failed++;
                        Console.WriteLine("FAIL Gesture " + test.Method.Name + ": " + ex.GetBaseException());
                    }
                }
            }
            finally
            {
                SettingsManager.Settings = previousSettings;
                Application.Current.ShutdownMode = shutdownMode;
            }
            Check(failed == 0, $"Canvas gestures: {failed} failures");
        }

        public static void RunConfiguration(string path)
        {
            var previousSettings = SettingsManager.Settings;
            var shutdownMode = Application.Current.ShutdownMode;
            Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            try
            {
                var settings = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(path));
                using var fixture = new Fixture(settings);
                fixture.Translate();
                Near(fixture.Ink.StylusPoints[0].ToPoint(), new Point(240, 170), "Current configuration cannot translate ink", 1);
                Console.WriteLine("PASS Gesture CurrentConfiguration");
            }
            finally
            {
                SettingsManager.Settings = previousSettings;
                Application.Current.ShutdownMode = shutdownMode;
            }
        }

        private static Settings TranslationSettings()
        {
            var settings = new Settings();
            settings.Gesture.IsEnableTwoFingerZoom = false;
            settings.Gesture.IsEnableTwoFingerZoomBoard = false;
            return settings;
        }

        private static void TranslationWithAllPenStyles()
        {
            foreach (int style in new[] { 0, 1, 2, 3 })
            foreach (bool disablePressure in new[] { false, true })
            foreach (bool nibMode in new[] { false, true })
            {
                var settings = TranslationSettings();
                settings.Canvas.InkStyle = style;
                settings.Canvas.DisablePressure = disablePressure;
                settings.Startup.IsEnableNibMode = nibMode;
                using var fixture = new Fixture(settings);
                fixture.Translate();
                Near(fixture.Ink.StylusPoints[0].ToPoint(), new Point(240, 170),
                    $"Translation failed for style={style}, pressureDisabled={disablePressure}, nib={nibMode}");
            }
        }

        private static void TranslationWithoutManipulation()
        {
            using var fixture = new Fixture(TranslationSettings());
            fixture.Canvas.IsManipulationEnabled = false;
            fixture.Translate();
            Near(fixture.Ink.StylusPoints[0].ToPoint(), new Point(240, 170),
                "Raw two-finger input depended on IsManipulationEnabled");
        }

        private static void TranslationWithMultiTouchWriting()
        {
            foreach (bool runtimeMultiTouch in new[] { false, true })
            {
                var settings = TranslationSettings();
                settings.Gesture.IsEnableMultiTouchModeBoard = true;
                using var fixture = new Fixture(settings);
                Set(fixture.Window, "isInMultiTouchMode", runtimeMultiTouch);
                fixture.Translate();
                Near(fixture.Ink.StylusPoints[0].ToPoint(), new Point(240, 170),
                    $"Multi-touch writing blocked an enabled gesture (runtime={runtimeMultiTouch})");
                Check(fixture.Canvas.Strokes.Count == 1, "Two-finger gesture committed handwriting strokes");
            }
        }

        private static void TranslationInEditingModes()
        {
            foreach (var mode in new[] { InkCanvasEditingMode.Ink, InkCanvasEditingMode.None,
                InkCanvasEditingMode.EraseByPoint, InkCanvasEditingMode.EraseByStroke, InkCanvasEditingMode.Select })
            {
                using var fixture = new Fixture(TranslationSettings(), mode);
                fixture.Translate();
                Near(fixture.Ink.StylusPoints[0].ToPoint(), new Point(240, 170), $"Editing mode {mode} blocked translation");
                Check(fixture.Canvas.EditingMode == mode, $"Gesture did not restore {mode}");
            }
        }

        private static void IndependentSceneSwitches()
        {
            foreach (int mode in new[] { 0, 1 })
            foreach (bool enabled in new[] { false, true })
            {
                var settings = TranslationSettings();
                settings.Gesture.IsEnableTwoFingerTranslate = mode == 0 ? enabled : !enabled;
                settings.Gesture.IsEnableTwoFingerTranslateBoard = mode == 1 ? enabled : !enabled;
                using var fixture = new Fixture(settings);
                Set(fixture.Window, "_currentMode", mode);
                fixture.Translate();
                Near(fixture.Ink.StylusPoints[0].ToPoint(), enabled ? new Point(240, 170) : new Point(200, 150),
                    $"Gesture switches leaked between scenes (mode={mode}, enabled={enabled})");
            }
        }

        private static void PinchUsesFingerCenter()
        {
            var settings = TranslationSettings();
            settings.Gesture.IsEnableTwoFingerTranslateBoard = false;
            settings.Gesture.IsEnableTwoFingerZoomBoard = true;
            using var fixture = new Fixture(settings);
            fixture.Begin(new Point(300, 200), new Point(500, 200));
            fixture.MovePair(new Point(250, 200), new Point(550, 200));
            Near(fixture.Ink.StylusPoints[0].ToPoint(), new Point(100, 125), "Pinch did not use the fingers' center");
            Check(Math.Abs(fixture.Ink.DrawingAttributes.Width - 6) < 0.01, "Pinch did not scale ink thickness");
        }

        private static void RotationUsesFingerCenter()
        {
            var settings = TranslationSettings();
            settings.Gesture.IsEnableTwoFingerTranslateBoard = false;
            settings.Gesture.IsEnableTwoFingerRotationBoard = true;
            using var fixture = new Fixture(settings);
            fixture.Begin(new Point(300, 200), new Point(500, 200));
            fixture.MovePair(new Point(400, 100), new Point(400, 300));
            Near(fixture.Ink.StylusPoints[0].ToPoint(), new Point(450, 0), "Rotation did not use the fingers' center");
        }

        private static void SelectedInkDoesNotMoveOtherContent()
        {
            using var fixture = new Fixture(TranslationSettings(), InkCanvasEditingMode.Select);
            var other = new Stroke(new StylusPointCollection { new StylusPoint(100, 100), new StylusPoint(120, 120) });
            fixture.Canvas.Strokes.Add(other);
            fixture.Canvas.Select(new StrokeCollection { fixture.Ink });
            fixture.Translate();
            Near(fixture.Ink.StylusPoints[0].ToPoint(), new Point(240, 170), "Selected ink did not move");
            Near(other.StylusPoints[0].ToPoint(), new Point(100, 100), "Gesture moved unselected ink");
            Near(fixture.Image.RenderTransform.Value.Transform(new Point(200, 150)), new Point(200, 150),
                "Selection gesture moved an unrelated image");
        }

        private static void GestureDiscardsTemporaryInk()
        {
            var settings = TranslationSettings();
            settings.Canvas.InkStyle = 3;
            using var fixture = new Fixture(settings);
            fixture.Begin(new Point(300, 220), new Point(500, 220));
            fixture.MovePair(new Point(340, 240), new Point(540, 240));
            fixture.End();
            Check(fixture.Canvas.Strokes.Count == 1, "Gesture left extra strokes on the canvas");
            Check(Field<HashSet<int>>(fixture.Window, "_activeRealtimeTouchStrokeIds").Count == 0,
                "Gesture retained active realtime touch strokes");
            Check(Field<Dictionary<int, StrokeVisual>>(fixture.Window, "<StrokeVisualList>k__BackingField").Count == 0,
                "Gesture retained drawing previews");
        }

        private static void RemainingFingerDoesNotResumeWriting()
        {
            using var fixture = new Fixture(TranslationSettings());
            fixture.Begin(new Point(300, 220), new Point(500, 220));
            fixture.MovePair(new Point(340, 240), new Point(540, 240));
            fixture.Second.Up();
            Pump();
            var before = fixture.Ink.StylusPoints[0].ToPoint();
            fixture.First.Move(new Point(380, 260));
            Pump();
            Near(fixture.Ink.StylusPoints[0].ToPoint(), before, "Remaining finger continued moving content");
            Check(fixture.Canvas.EditingMode == InkCanvasEditingMode.None, "Remaining finger resumed writing");
            fixture.First.Up();
            Pump();
            Check(fixture.Canvas.EditingMode == InkCanvasEditingMode.Ink, "Lifting all fingers did not restore writing");
        }

        private static void LateManipulationDoesNotTransformAgain()
        {
            using var fixture = new Fixture(TranslationSettings());
            fixture.Begin(new Point(300, 220), new Point(500, 220));
            fixture.MovePair(new Point(340, 240), new Point(540, 240));
            var before = fixture.Ink.StylusPoints[0].ToPoint();
            var delta = new ManipulationDelta(new Vector(30, 20), 0, new Vector(1, 1), new Vector());
            var args = (ManipulationDeltaEventArgs)typeof(ManipulationDeltaEventArgs)
                .GetConstructors(Members).Single().Invoke(new object[]
                { null, 0, fixture.Canvas, new Point(), delta, delta,
                    new ManipulationVelocities(new Vector(), 0, new Vector()), false });
            args.Source = fixture.Canvas;
            Set(args, "_manipulators", new IManipulator[] { fixture.First, fixture.Second });
            Call(fixture.Window, "Main_Grid_ManipulationDelta", fixture.Canvas, args);
            Near(fixture.Ink.StylusPoints[0].ToPoint(), before, "Legacy Manipulation applied the gesture twice");
        }

        private static void CaptureLossAllowsNextGesture()
        {
            using var fixture = new Fixture(TranslationSettings());
            fixture.Begin(new Point(300, 220), new Point(500, 220));
            fixture.MovePair(new Point(320, 230), new Point(520, 230));
            fixture.First.Capture(null);
            fixture.End();
            fixture.Begin(new Point(300, 220), new Point(500, 220));
            fixture.MovePair(new Point(320, 230), new Point(520, 230));
            fixture.End();
            Near(fixture.Ink.StylusPoints[0].ToPoint(), new Point(240, 170), "Capture loss blocked the next gesture");
        }

        private static void PointGroupingAllowsNormalTwoFingerGesture()
        {
            using var fixture = new Fixture(TranslationSettings());
            var record = new TouchCalibrationRecord
            {
                UsePointGrouping = true, OutlineWidth = 100, OutlineHeight = 80,
                MinimumContacts = 3, WindowMilliseconds = 200,
                Environment = (TouchCalibrationEnvironment)typeof(MainWindow).Assembly
                    .GetType("Ink_Canvas.Ink.TouchCalibrationDisplay").GetMethod("Capture")
                    .Invoke(null, new object[] { fixture.Window, "WPF" })
            };
            SettingsManager.Settings.Advanced.TouchCalibration = record;
            fixture.Translate();
            Near(fixture.Ink.StylusPoints[0].ToPoint(), new Point(240, 170), "Pending palm grouping blocked normal two-finger input");
            Check(fixture.Canvas.Strokes.Count == 1, "Grouping committed two-finger gesture previews");
        }

        private static void GestureHistoryIncludesInkAndImages()
        {
            var settings = TranslationSettings();
            settings.Gesture.IsEnableTwoFingerZoomBoard = true;
            using var fixture = new Fixture(settings);
            fixture.TrackHistory();
            fixture.Begin(new Point(300, 200), new Point(500, 200));
            fixture.MovePair(new Point(250, 200), new Point(550, 200));
            fixture.End();
            var machine = Field<TimeMachine>(fixture.Window, "timeMachine");
            var after = fixture.Ink.StylusPoints[0].ToPoint();
            var history = machine.Undo();
            Check(history != null, "Gesture did not commit undo history");
            Call(fixture.Window, "ApplyHistoryToCanvas", history, null, null);
            Near(fixture.Ink.StylusPoints[0].ToPoint(), new Point(200, 150), "Undo did not restore ink points");
            Check(Math.Abs(fixture.Ink.DrawingAttributes.Width - 4) < 0.01, "Undo did not restore ink thickness");
            Near(fixture.Image.RenderTransform.Value.Transform(new Point(200, 150)), new Point(200, 150),
                "Undo did not restore the image with the ink");
            Check(!machine.CanUndo, "A single gesture created more than one undo step");
            Call(fixture.Window, "ApplyHistoryToCanvas", machine.Redo(), null, null);
            Near(fixture.Ink.StylusPoints[0].ToPoint(), after, "Redo did not restore the gesture");
            Near(fixture.Image.RenderTransform.Value.Transform(new Point(200, 150)), after,
                "Redo did not keep the image aligned with the ink");
        }

        private static void PointGroupingRetainsRealPalmInput()
        {
            using var fixture = new Fixture(TranslationSettings());
            SettingsManager.Settings.Advanced.TouchCalibration = new TouchCalibrationRecord
            {
                UsePointGrouping = true, OutlineWidth = 100, OutlineHeight = 80,
                MinimumContacts = 3, WindowMilliseconds = 200,
                Environment = (TouchCalibrationEnvironment)typeof(MainWindow).Assembly
                    .GetType("Ink_Canvas.Ink.TouchCalibrationDisplay").GetMethod("Capture")
                    .Invoke(null, new object[] { fixture.Window, "WPF" })
            };
            var contacts = new[] { new Contact(201, fixture.Canvas, new Point(340, 220)),
                new Contact(202, fixture.Canvas, new Point(350, 220)), new Contact(203, fixture.Canvas, new Point(360, 220)) };
            try
            {
                foreach (var contact in contacts) { contact.Down(); Pump(); }
                var tracker = typeof(MainWindow).GetField("_palmGroupTracker", Members).GetValue(fixture.Window);
                foreach (var contact in contacts)
                {
                    Call(tracker, "Up", (long)contact.Id);
                    Call(tracker, "Down", (long)contact.Id, contact.Position.X, contact.Position.Y, Environment.TickCount64 - 201);
                }
                Call(fixture.Window, "EvaluatePalmGroup");
                Check(Field<bool>(fixture.Window, "isPalmEraserActive"), "Raw gesture tracking stole a calibrated palm group");
                Check(!Field<bool>(fixture.Window, "_canvasGestureOwnsTouchInput"), "Palm contacts became a canvas gesture");
                Near(fixture.Ink.StylusPoints[0].ToPoint(), new Point(200, 150), "Palm recognition moved unrelated ink");
            }
            finally
            {
                foreach (var contact in contacts) contact.Up();
                Pump();
            }
        }

        private static void DeactivationCancelsTheGesture()
        {
            using var fixture = new Fixture(TranslationSettings());
            fixture.Begin(new Point(300, 220), new Point(500, 220));
            fixture.MovePair(new Point(320, 230), new Point(520, 230));
            Call(fixture.Window, "Window_Deactivated", fixture.Window, EventArgs.Empty);
            var before = fixture.Ink.StylusPoints[0].ToPoint();
            fixture.First.Move(new Point(380, 260));
            Pump();
            Near(fixture.Ink.StylusPoints[0].ToPoint(), before, "Canceled input continued transforming ink");
            Check(fixture.Canvas.EditingMode == InkCanvasEditingMode.Ink, "Deactivation left the canvas unable to write");
            fixture.End();
            fixture.Begin(new Point(300, 220), new Point(500, 220));
            fixture.MovePair(new Point(320, 230), new Point(520, 230));
            fixture.End();
            Near(fixture.Ink.StylusPoints[0].ToPoint(), new Point(240, 170), "Deactivation blocked the next gesture");
        }

        private static void AdditionalContactsDoNotJump()
        {
            using var fixture = new Fixture(TranslationSettings());
            fixture.Begin(new Point(300, 220), new Point(500, 220));
            fixture.MovePair(new Point(320, 230), new Point(520, 230));
            var before = fixture.Ink.StylusPoints[0].ToPoint();
            var third = new Contact(300, fixture.Canvas, new Point(600, 300));
            try
            {
                third.Down();
                Pump();
                third.Move(new Point(610, 310));
                Pump();
                Near(fixture.Ink.StylusPoints[0].ToPoint(), before, "A third contact changed the gesture baseline");
                fixture.First.Up();
                Pump();
                Near(fixture.Ink.StylusPoints[0].ToPoint(), before, "Replacing the primary contact caused a jump");
                fixture.Second.Position = new Point(540, 240);
                third.Position = new Point(630, 320);
                fixture.Second.ReportPosition();
                third.ReportPosition();
                Pump();
                Near(fixture.Ink.StylusPoints[0].ToPoint(), new Point(240, 170), "Replacement pair did not resume from a stable baseline");
            }
            finally { third.Up(); fixture.End(); }
        }

        private static void DisabledGesturesPreserveMultiTouchWriting()
        {
            var settings = TranslationSettings();
            settings.Gesture.IsEnableTwoFingerTranslateBoard = false;
            settings.Gesture.IsEnableMultiTouchModeBoard = true;
            using var fixture = new Fixture(settings);
            fixture.Begin(new Point(300, 220), new Point(500, 220));
            fixture.MovePair(new Point(340, 240), new Point(540, 240));
            Near(fixture.Ink.StylusPoints[0].ToPoint(), new Point(200, 150), "Disabled gestures moved existing ink");
            Check(Field<HashSet<int>>(fixture.Window, "_activeTouchStrokeIds").Count == 2,
                "Disabling gestures did not preserve two independent handwriting contacts");
        }

        private static void FrozenAndPresentationPoliciesAreRespected()
        {
            foreach (bool frozen in new[] { false, true })
            {
                using var fixture = new Fixture(TranslationSettings(), frozen ? InkCanvasEditingMode.None : InkCanvasEditingMode.Ink);
                if (frozen)
                {
                    Field<bool[]>(fixture.Window, "frozenPages")[1] = true;
                    Set(fixture.Window, "lastFreezeBlockNotificationUtc", DateTime.UtcNow);
                }
                else
                    typeof(MainWindow).GetProperty("ArePPTControlsVisible", Members).SetValue(fixture.Window, true);
                fixture.Translate();
                Near(fixture.Ink.StylusPoints[0].ToPoint(), new Point(200, 150),
                    frozen ? "Gesture bypassed frozen-page protection" : "Gesture bypassed presentation permission");
            }
        }

        private static void NextRealtimeStrokeCanStart()
        {
            var settings = TranslationSettings();
            settings.Canvas.InkStyle = 3;
            using var fixture = new Fixture(settings);
            fixture.Translate();
            var next = new Contact(400, fixture.Canvas, new Point(300, 200));
            try
            {
                next.Down();
                Pump();
                Check(Field<HashSet<int>>(fixture.Window, "_activeRealtimeTouchStrokeIds").Contains(next.Id),
                    "A completed gesture left the next realtime stroke suppressed");
            }
            finally { next.Up(); Pump(); }
        }

        // Initialize the WPF base only, without starting the app's hardware, plugins or background services.
        private sealed class Fixture : IDisposable
        {
            public MainWindow Window { get; }
            public WpfInkCanvas Canvas { get; }
            public Stroke Ink { get; }
            public Image Image { get; }
            public Contact First { get; private set; }
            public Contact Second { get; private set; }
            private int _nextContactId = 100;

            public Fixture(Settings settings, InkCanvasEditingMode editingMode = InkCanvasEditingMode.Ink)
            {
                SettingsManager.Settings = settings;
                Window = (MainWindow)FormatterServices.GetUninitializedObject(typeof(MainWindow));
                var initialize = new DynamicMethod("InitializeGestureWindow", typeof(void), new[] { typeof(MainWindow) },
                    typeof(CanvasGestureTests).Module, true);
                var il = initialize.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Call, typeof(Window).GetConstructor(Type.EmptyTypes));
                il.Emit(OpCodes.Ret);
                ((Action<MainWindow>)initialize.CreateDelegate(typeof(Action<MainWindow>)))(Window);
                foreach (var field in typeof(MainWindow).GetFields(Members))
                {
                    var type = field.FieldType;
                    if (field.GetValue(Window) == null && type.IsGenericType &&
                        new[] { typeof(Dictionary<,>), typeof(List<>), typeof(HashSet<>) }.Contains(type.GetGenericTypeDefinition()))
                        field.SetValue(Window, Activator.CreateInstance(type));
                }
                var tracker = typeof(MainWindow).GetField("_palmGroupTracker", Members);
                tracker.SetValue(Window, Activator.CreateInstance(tracker.FieldType, true));
                var root = new Grid();
                Canvas = new WpfInkCanvas { Width = 800, Height = 450, Background = Brushes.Transparent,
                    EditingMode = editingMode, IsManipulationEnabled = true, ClipToBounds = false };
                var bar = new Viewbox { Width = 20, Height = 20, HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Bottom };
                root.Children.Add(Canvas);
                root.Children.Add(bar);
                Window.Width = 800;
                Window.Height = 450;
                Window.Left = -10000;
                Window.Top = -10000;
                Window.WindowStyle = WindowStyle.None;
                Window.ShowInTaskbar = false;
                Window.ShowActivated = false;
                Window.Content = root;
                Set(Window, "inkCanvas", Canvas);
                Set(Window, "ViewboxFloatingBar", bar);
                Set(Window, "BlackboardUIGridForInkReplay", new Grid());
                Set(Window, "InkCanvasGridForInkReplay", root);
                Set(Window, "GridInkCanvasSelectionCover", new Grid { Visibility = Visibility.Collapsed });
                Set(Window, "GridBackgroundCover", new Grid());
                Set(Window, "_currentMode", 1);
                Set(Window, "_currentToolMode", editingMode == InkCanvasEditingMode.Select ? "select" : "pen");
                Set(Window, "_currentWhiteboardIndex", 1);
                Set(Window, "frozenPages", new bool[101]);
                Set(Window, "pageLastUserInkMutationUtc", new DateTime[101]);
                Set(Window, "lastTouchDownStrokeCollection", new StrokeCollection());
                Set(Window, "lastInkCanvasEditingMode", editingMode);
                Set(Window, "BoundsWidth", settings.Startup.IsEnableNibMode ? 10 : 30);
                Set(Window, "timeMachine", new TimeMachine());
                foreach (var entry in new[]
                {
                    (UIElement.PreviewTouchDownEvent, "InkCanvas_PreviewTouchDown"),
                    (UIElement.PreviewTouchMoveEvent, "InkCanvas_PreviewTouchMove"),
                    (UIElement.PreviewTouchUpEvent, "InkCanvas_PreviewTouchUp"),
                    (UIElement.TouchDownEvent, "Main_Grid_TouchDown"),
                    (UIElement.TouchMoveEvent, "inkCanvas_TouchMove")
                }) Bind(entry.Item1, entry.Item2, typeof(EventHandler<TouchEventArgs>));
                Bind(UIElement.ManipulationStartingEvent, "InkCanvas_ManipulationStarting", typeof(EventHandler<ManipulationStartingEventArgs>));
                Bind(UIElement.ManipulationDeltaEvent, "Main_Grid_ManipulationDelta", typeof(EventHandler<ManipulationDeltaEventArgs>));
                Bind(UIElement.ManipulationCompletedEvent, "Main_Grid_ManipulationCompleted", typeof(EventHandler<ManipulationCompletedEventArgs>));
                Ink = new Stroke(new StylusPointCollection { new StylusPoint(200, 150), new StylusPoint(240, 170) },
                    new DrawingAttributes { Width = 4, Height = 4 });
                Canvas.Strokes.Add(Ink);
                Image = new Image { Width = 20, Height = 20, IsHitTestVisible = false };
                Canvas.Children.Add(Image);
                Window.Show();
                root.UpdateLayout();
                Pump();
            }

            private void Bind(RoutedEvent routedEvent, string method, Type handlerType) =>
                Canvas.AddHandler(routedEvent, Delegate.CreateDelegate(handlerType, Window,
                    typeof(MainWindow).GetMethod(method, Members)));

            public void Begin(Point first, Point second)
            {
                First = new Contact(++_nextContactId, Canvas, first);
                Second = new Contact(++_nextContactId, Canvas, second);
                First.Down();
                Pump();
                Second.Down();
                Pump();
            }

            public void MovePair(Point first, Point second)
            {
                First.Position = first;
                Second.Position = second;
                First.ReportPosition();
                Second.ReportPosition();
                Pump();
            }

            public void Translate()
            {
                Begin(new Point(300, 220), new Point(500, 220));
                for (int i = 1; i <= 4; i++)
                    MovePair(new Point(300 + 10 * i, 220 + 5 * i), new Point(500 + 10 * i, 220 + 5 * i));
                End();
            }

            public void End()
            {
                First?.Up();
                Second?.Up();
                Pump();
            }

            public void TrackHistory()
            {
                Field<Dictionary<Stroke, StylusPointCollection>>(Window, "StrokeInitialHistory")[Ink] = Ink.StylusPoints.Clone();
                var flags = Field<Dictionary<Guid, List<Stroke>>>(Window, "DrawingAttributesHistoryFlag");
                foreach (var id in new[] { DrawingAttributeIds.Color, DrawingAttributeIds.DrawingFlags,
                    DrawingAttributeIds.IsHighlighter, DrawingAttributeIds.StylusHeight, DrawingAttributeIds.StylusTip,
                    DrawingAttributeIds.StylusTipTransform, DrawingAttributeIds.StylusWidth })
                    flags[id] = new List<Stroke>();
                Ink.StylusPointsChanged += (EventHandler)Delegate.CreateDelegate(typeof(EventHandler), Window,
                    typeof(MainWindow).GetMethod("Stroke_StylusPointsChanged", Members));
                Ink.DrawingAttributesChanged += (PropertyDataChangedEventHandler)Delegate.CreateDelegate(
                    typeof(PropertyDataChangedEventHandler), Window,
                    typeof(MainWindow).GetMethod("Stroke_DrawingAttributesChanged", Members));
            }

            public void Dispose()
            {
                End();
                Call(Window, "CancelPalmGroup", true);
                Window.Close();
            }
        }

        private static void Pump()
        {
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(20) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start();
            Dispatcher.PushFrame(frame);
        }

        private sealed class Contact : TouchDevice
        {
            private readonly WpfInkCanvas _canvas;
            private TouchAction _action;
            private bool _active;
            public Point Position { get; set; }
            public Contact(int id, WpfInkCanvas canvas, Point point) : base(id) { _canvas = canvas; Position = point; }
            public void Down()
            {
                SetActiveSource(PresentationSource.FromVisual(_canvas));
                Activate();
                _active = true;
                _action = TouchAction.Down;
                ReportDown();
            }
            public void Move(Point point) { Position = point; ReportPosition(); }
            public void ReportPosition() { _action = TouchAction.Move; ReportMove(); }
            public void Up()
            {
                if (!_active) return;
                _action = TouchAction.Up;
                ReportUp();
                Deactivate();
                _active = false;
            }
            public override TouchPoint GetTouchPoint(IInputElement relativeTo)
            {
                var point = relativeTo is UIElement element ? _canvas.TranslatePoint(Position, element) : Position;
                return new TouchPoint(this, point, new Rect(point, new Size(8, 8)), _action);
            }
            public override TouchPointCollection GetIntermediateTouchPoints(IInputElement relativeTo) =>
                new TouchPointCollection { GetTouchPoint(relativeTo) };
        }
    }
}
