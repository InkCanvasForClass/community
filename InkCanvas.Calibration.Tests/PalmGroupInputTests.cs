using System;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Ink_Canvas;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using Ink_Canvas.Ink;

namespace InkCanvas.Calibration.Tests
{
    internal static class PalmGroupInputTests
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static FieldInfo Field(string name) => typeof(MainWindow).GetField(name, Members);
        private static object Call(object target, string name, params object[] args) =>
            target.GetType().GetMethod(name, Members).Invoke(target, args);
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class Contact : TouchDevice
        {
            public Point Position { get; set; }
            public Contact(int id, double x, double y) : base(id) => Position = new Point(x, y);
            public override TouchPoint GetTouchPoint(IInputElement relativeTo) =>
                new TouchPoint(this, Position, new Rect(Position, new Size(1, 1)), TouchAction.Move);
            public override TouchPointCollection GetIntermediateTouchPoints(IInputElement relativeTo) =>
                new TouchPointCollection { GetTouchPoint(relativeTo) };
        }

        public static void RunAll()
        {
            var previous = SettingsManager.Settings;
            SettingsManager.Settings = new Settings();
            SettingsManager.Settings.Canvas.EnablePalmEraser = true;
            SettingsManager.Settings.Canvas.IsShowCursor = false;
            SettingsManager.Settings.Gesture.IsEnableMultiTouchMode = false;
            var window = (MainWindow)FormatterServices.GetUninitializedObject(typeof(MainWindow));
            var canvas = new System.Windows.Controls.InkCanvas { EditingMode = InkCanvasEditingMode.Ink };
            Field("inkCanvas").SetValue(window, canvas);
            Field("_currentToolMode").SetValue(window, "pen");
            Field("frozenPages").SetValue(window, new bool[101]);
            Field("_palmGroupTimer").SetValue(window, new DispatcherTimer());
            foreach (string name in new[] { "_palmGroupTracker", "_palmGroupStrokes", "_palmGroupDevices",
                "_palmContacts", "_palmSuppressedTouches", "_activeRealtimeTouchStrokeIds", "_activeRealtimeStylusStrokeIds",
                "_activeTouchStrokeIds", "_realtimeBrushTipStates", "_pauseStraightenTimers", "dec",
                "<StrokeVisualList>k__BackingField", "<VisualCanvasList>k__BackingField", "<TouchDownPointsList>k__BackingField" })
            {
                var field = Field(name);
                field.SetValue(window, Activator.CreateInstance(field.FieldType));
            }
            var tracker = Field("_palmGroupTracker").GetValue(window);
            SettingsManager.Settings.Advanced.TouchCalibration = new TouchCalibrationRecord
            {
                UsePointGrouping = true, OutlineWidth = 100, OutlineHeight = 80,
                MinimumContacts = 3, WindowMilliseconds = 200,
                Environment = (TouchCalibrationEnvironment)typeof(MainWindow).Assembly
                    .GetType("Ink_Canvas.Ink.TouchCalibrationDisplay").GetMethod("Capture")
                    .Invoke(null, new object[] { window, "WPF" })
            };
            try
            {
                for (int id = 1; id <= 3; id++)
                {
                    var contact = new Contact(id, id * 10, 20);
                    Check((bool)Call(window, "TryHandlePalmTouch", new TouchEventArgs(contact, 0) { RoutedEvent = UIElement.PreviewTouchDownEvent }, true), "Grouping did not take the contact");
                    // Keep the group pending without sleeping; advance all contact timestamps together below.
                    Call(tracker, "Up", (long)id);
                    Call(tracker, "Down", (long)id, contact.Position.X, contact.Position.Y, long.MaxValue);
                }
                for (int id = 1; id <= 3; id++)
                {
                    Call(tracker, "Up", (long)id);
                    Call(tracker, "Down", (long)id, id * 10d, 20d, Environment.TickCount64 - 201);
                }
                Call(window, "EvaluatePalmGroup");
                Check((bool)Field("isPalmEraserActive").GetValue(window), "Three nearby contacts did not activate palm erasing");
                Check(canvas.Children.Count == 0, "Recognized palm contacts retained ink previews");

                var remainingFinger = new Contact(4, 40, 20);
                Call(window, "TryHandlePalmTouch", new TouchEventArgs(remainingFinger, 0) { RoutedEvent = UIElement.PreviewTouchDownEvent }, true);
                remainingFinger.Position = new Point(60, 30);
                Call(window, "TryHandlePalmTouch", new TouchEventArgs(remainingFinger, 0) { RoutedEvent = UIElement.PreviewTouchMoveEvent }, false);
                Check(canvas.Children.Count == 0,
                    $"Palm erasing leaked {canvas.Children.Count} finger ink preview(s) after another contact arrived");
                Call(window, "FinishPalmTouch", new TouchEventArgs(remainingFinger, 0) { RoutedEvent = UIElement.PreviewTouchUpEvent });
                Check(canvas.Strokes.Count == 0, "The remaining finger committed ink on lift");
                Console.WriteLine("PASS PalmGroupInput (late finger has no preview or committed ink)");
            }
            finally
            {
                Call(window, "CancelPalmGroup", true);
                SettingsManager.Settings = previous;
            }
        }
    }
}
