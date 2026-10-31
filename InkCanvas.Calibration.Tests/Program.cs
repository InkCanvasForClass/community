using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Ink_Canvas;
using Ink_Canvas.Ink;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using Ink_Canvas.Windows.SettingsViews.Pages;
using Newtonsoft.Json;

namespace InkCanvas.Calibration.Tests
{
    internal static class Program
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        [STAThread]
        private static int Main()
        {
            var app = new Application();
            app.Resources[typeof(TextBlock)] = new Style(typeof(TextBlock));
            string testRoot = Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N"));
            App.RootPath = testRoot;
            SettingsManager.Settings = new Settings();
            int failed = 0;
            foreach (var test in new Action[] { PageConstruction, PointRouteSaves, AreaRouteSaves, FailedSaveRetainsDraft,
                TemporaryPalmModePreservesAnnotation, PendingPointGroupPreservesAnnotation, TemporaryPalmModeDoesNotNotifyPlugin })
            {
                try { test(); Console.WriteLine("PASS " + test.Method.Name); }
                catch (Exception ex) { failed++; Console.WriteLine("FAIL " + test.Method.Name + ": " + ex.GetBaseException()); }
            }
            if (Directory.Exists(testRoot)) Directory.Delete(testRoot, true);
            Console.WriteLine($"Calibration UI: {failed} failures");
            return failed == 0 ? 0 : 1;
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Members).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Members).SetValue(target, value);
        private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Members).Invoke(target, args);

        private static Button SaveButton(TouchCalibrationPage page) =>
            (page.FindName("SaveButton") ?? page.FindName("NextButton")) as Button;

        private static void PageConstruction() => _ = new TouchCalibrationPage();

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
