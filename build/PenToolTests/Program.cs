using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Ink_Canvas.Helpers;
using Ink_Canvas.Ink;
using Ink_Canvas.Ink.WinRT;

internal static class Program
{
    private static int _checks;
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); _checks++; }
    private static void Pump(int milliseconds = 1)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle) { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
    }
    [STAThread]
    private static int Main()
    {
        var attributes = new DrawingAttributes { Color = Color.FromArgb(120, 255, 220, 0), Width = 5, Height = 5 };
        foreach (bool overlap in new[] { false, true })
            foreach (double width in new[] { 4.0, 20.0, 50.0 })
                foreach (double dpi in new[] { 1.0, 1.5, 2.0 })
                {
                    var style = PenToolStylePolicy.Capture(attributes, 1, width, overlap, false, true);
                    Check(style.WidthDip == width / 2 && style.HeightDip == width && style.TipRectangle && !style.UseVelocityBrushTip,
                        "Highlighter thickness or tip was overwritten");
                    var wet = new WinRTInkConfig(1920, 1080, style, dpi, dpi, new Point()).ToInkDrawingAttributes();
                    Check(wet.DrawAsHighlighter == !overlap, "Wet highlighter ignored overlap setting");
                    Check(Math.Abs(wet.Size.Width - width / 2 * dpi) < 0.001 && Math.Abs(wet.Size.Height - width * dpi) < 0.001,
                        "Native highlighter size did not follow the selected width");
                    var points = new[] { new Windows.UI.Input.Inking.InkPoint(new Windows.Foundation.Point(10, 20), 0.5f) };
                    var dry = WinRTStrokeConverter.CreateStroke(points, style, dpi, dpi, new Point());
                    Check(dry.DrawingAttributes.IsHighlighter == !overlap && dry.DrawingAttributes.Width == width / 2
                        && dry.DrawingAttributes.Height == width, "Dry highlighter disagreed with wet style");
                }
        var laserStyle = PenToolStylePolicy.Capture(attributes, 2, 20, false, true, false);
        Check(laserStyle.IsLaser && !laserStyle.IsHighlighter && !laserStyle.TipRectangle, "Laser inherited highlighter blending");
        long clock = 100;
        var sessions = new StrokeSessionTracker<WinRTInkConfig>(() => clock);
        var original = new WinRTInkConfig(100, 100, laserStyle, 1, 1, new Point());
        sessions.Begin(1, original); clock += 125;
        var ended = sessions.End(1);
        Check(ReferenceEquals(ended.State, original) && ended.DurationMs == 125, "Delayed drying lost the pointer-down laser style");
        sessions.Begin(1, original); sessions.Cancel(1);
        Check(sessions.End(1).State == null, "Canceled pointer retained its dry style");
        var tagged = WinRTStrokeConverter.CreateStroke(new[] { new Windows.UI.Input.Inking.InkPoint(new Windows.Foundation.Point(1, 1), 0.5f) },
            laserStyle, 1, 1, new Point(), durationMs: 125);
        Check(InkFadeManager.IsLaserStroke(tagged) && (long)tagged.GetPropertyData(WinRTStrokeConverter.StrokeDurationGuid) == 125,
            "Dry laser lost fade identity or writing duration");

        var window = new Ink_Canvas.MainWindow();
        window.inkCanvas.EditingMode = InkCanvasEditingMode.None;
        var fade = new InkFadeManager(window) { IsEnabled = false, FadeTime = 20, AnimationDuration = 20 };
        Stroke Laser()
        {
            var stroke = new Stroke(new StylusPointCollection { new StylusPoint(1, 1), new StylusPoint(20, 20) });
            stroke.AddPropertyData(InkFadeManager.LaserRenderModeGuid, true); return stroke;
        }
        var first = Laser();
        var normal = new Stroke(new StylusPointCollection { new StylusPoint(1, 1) });
        var persistent = PersistentStrokePolicy.Filter(new StrokeCollection { first, normal });
        Check(persistent.Count == 1 && ReferenceEquals(persistent[0], normal), "Undo history retained a transient laser stroke");
        Check(fade.TryAddFadingStroke(first, new Point(), new Point(20, 20)), "Switching tools disabled an already captured laser fade");
        Pump(); Check(window.Surface.Children.Count == 2, "Fade visual was never attached");
        Check(window.inkCanvas.EditingMode == InkCanvasEditingMode.None, "Fade manager changed the active editing mode");
        fade.ClearAllFadingStrokes();
        Check(window.Surface.Children.Count == 1, "Clear left a laser visual behind");
        fade.AddFadingStroke(first, new Point(), new Point(20, 20));
        fade.ClearAllFadingStrokes(); Pump();
        Check(window.Surface.Children.Count == 1, "Queued attachment resurrected a cleared laser");
        fade.AddFadingStroke(first, new Point(), new Point(20, 20)); Pump();
        fade.ClearAllFadingStrokes();
        fade.AddFadingStroke(Laser(), new Point(), new Point(20, 20)); Pump();
        Check(window.Surface.Children.Count == 2, "Stale clear removed a subsequently registered stroke");
        fade.ClearAllFadingStrokes();
        fade.AddFadingStroke(first, new Point(), new Point(20, 20));
        fade.AddFadingStroke(first, new Point(), new Point(20, 20)); Pump();
        Check(window.Surface.Children.Count == 2, "Duplicate fade registration orphaned an old visual");
        Pump(250);
        Check(window.Surface.Children.Count == 1, "Laser did not expire without a rendered animation completion");
        fade.ClearAllFadingStrokes(); window.Close();
        Console.WriteLine($"PASS: {_checks} pen style and fade checks (real Windows Ink attributes and WPF dispatcher)");
        return 0;
    }
}
