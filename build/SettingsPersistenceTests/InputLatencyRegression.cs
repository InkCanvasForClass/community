using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Ink_Canvas;
using Ink_Canvas.Helpers;
using Ink_Canvas.Windows.SettingsViews.Helpers;

internal static class InputLatencyRegression
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    internal static void Run(Action<bool, string> check)
    {
        var timing = typeof(MainWindow).Assembly.GetType("Ink_Canvas.Helpers.InkPacketTiming").GetMethod("SampleInterval", Hidden);
        float Dt(int now, int? before, int count, float rate = 120) => (float)timing.Invoke(null, new object[] { now, before, count, rate });
        check(Math.Abs(Dt(120, 100, 4) - .005f) < .000001, "Packet time was not shared across actual samples");
        check(Math.Abs(Dt(unchecked(int.MinValue + 9), int.MaxValue - 10, 4) - .005f) < .000001, "Timestamp rollover changed the input interval");
        check(Dt(100, 100, 4) > 0 && Dt(99, 100, 4) > 0, "Repeated/out-of-order timestamps produced invalid time");
        check(Math.Abs(Dt(100, null, 2) - 1f / 240) < .000001, "First packet did not use a bounded fallback interval");

        var canvas = new VisualCanvas();
        var visual = new StrokeVisual(new System.Windows.Ink.DrawingAttributes { Width = 6, Height = 6, Color = Colors.Black });
        visual.SetVisualCanvas(canvas); visual.Add(new StylusPoint(10, 20, .4f));
        var scheduler = typeof(StrokeVisual).Assembly.GetType("Ink_Canvas.Helpers.RealtimeInkFrameScheduler");
        void Schedule(string method) => scheduler.GetMethod(method, Hidden).Invoke(null, new object[] { visual });
        Schedule("RequestRedraw");
        check((bool)typeof(StrokeVisual).GetProperty("HasDrawnPreview", Hidden).GetValue(visual), "First input waited for a Rendering callback");
        var pending = (IDictionary)scheduler.GetField("PendingRedraws", Hidden).GetValue(null);
        check(!pending.Contains(visual), "Immediate first preview left duplicate pending work");
        visual.Add(new StylusPoint(20, 20, .5f)); Schedule("RequestRedraw");
        check(pending.Contains(visual), "Later input bypassed frame coalescing");
        Schedule("Cancel");
        typeof(StrokeVisual).GetMethod("SetInputPreviewTip", Hidden).Invoke(visual,
            new object[] { new StylusPoint(30, 20, .7f), new Point(100, 20) });
        var position = (Point)typeof(StrokeVisual).GetField("_previewPosition", Hidden).GetValue(visual);
        check(position.X > 30 && position.X <= 33, "Visual tip correction was missing or unbounded");
        check(visual.Stroke.StylusPoints.Count == 2, "Visual correction entered formal samples");
        typeof(StrokeVisual).GetMethod("CommitPreviewTip", Hidden).Invoke(visual, null);
        check(visual.Stroke.StylusPoints[^1].X == 30 && visual.Stroke.StylusPoints[^1].PressureFactor == .7f,
            "Visual tip correction changed the committed filtered endpoint/pressure");

        var owner = (MainWindow)RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
        typeof(MainWindow).GetField("inkCanvas", Hidden).SetValue(owner, new Ink_Canvas.Controls.HighlighterInkCanvas());
        typeof(MainWindow).GetField("RealtimeVelocityBrushTipAppliedGuid", Hidden).SetValue(owner, Guid.NewGuid());
        var stateType = typeof(MainWindow).GetNestedType("RealtimeBrushTipState", Hidden);
        var state = Activator.CreateInstance(stateType, true);
        var stateField = typeof(MainWindow).GetField("_realtimeBrushTipStates", Hidden);
        var states = (IDictionary)Activator.CreateInstance(stateField.FieldType);
        states[7] = state; stateField.SetValue(owner, states);
        stateType.GetProperty("LastPacketTimestamp").SetValue(state, (int?)100);
        var packet = new StylusPointCollection { new StylusPoint(10, 20, .2f), new StylusPoint(40, 30, .8f), new StylusPoint(70, 20, .4f) };
        var packetVisual = new StrokeVisual(new System.Windows.Ink.DrawingAttributes { Width = 5, Height = 5 });
        typeof(MainWindow).GetMethod("ProcessRealtimeStylusPacket", Hidden).Invoke(owner, new object[] { packetVisual, 7, packet, 124 });
        check(packetVisual.Stroke.StylusPoints.Count == 3, "Actual packet samples were lost or replaced with synthetic interpolation");
        check(packetVisual.Stroke.StylusPoints.Select(p => p.PressureFactor).Distinct().Count() > 1,
            "Actual device pressure was replaced by the default pressure");
        check((int?)stateType.GetProperty("LastPacketTimestamp").GetValue(state) == 124, "Packet input timestamp was not retained");
        var empty = new StylusPointCollection();
        typeof(MainWindow).GetMethod("ProcessRealtimeStylusPacket", Hidden).Invoke(owner, new object[] { packetVisual, 7, empty, 125 });
        check(packetVisual.Stroke.StylusPoints.Count == 3
            && (int?)stateType.GetProperty("LastPacketTimestamp").GetValue(state) == 124, "Empty packet changed samples or their timing");
        var contacts = new HashSet<int> { 8, 9 };
        typeof(MainWindow).GetField("_activeRealtimeTouchStrokeIds", Hidden).SetValue(owner, contacts);
        int style = SettingsManager.Settings.Canvas.InkStyle;
        double mix = SettingsManager.Settings.Canvas.VelocityBrushTipMix;
        bool disablePressure = SettingsManager.Settings.Canvas.DisablePressure;
        var results = new List<StylusPoint[]>();
        try
        {
            SettingsManager.Settings.Canvas.InkStyle = 3;
            SettingsManager.Settings.Canvas.VelocityBrushTipMix = .5;
            SettingsManager.Settings.Canvas.DisablePressure = false;
            foreach (int id in contacts)
            {
                var touchState = Activator.CreateInstance(stateType, true); states[id] = touchState;
                stateType.GetProperty("HasTouchPoint").SetValue(touchState, true);
                stateType.GetProperty("LastTouchPoint").SetValue(touchState, new Point(10, 20));
                stateType.GetProperty("LastTouchEventTimestamp").SetValue(touchState, (int?)100);
                var touchVisual = new StrokeVisual(new System.Windows.Ink.DrawingAttributes { Width = 5, Height = 5 });
                var append = typeof(MainWindow).GetMethod("TryAppendRealtimeVelocityBrushTipInterpolatedPoints", Hidden);
                append.Invoke(owner, new object[] { touchVisual, id, new Point(50, 30), 116, .5f });
                append.Invoke(owner, new object[] { touchVisual, id, new Point(90, 20), 144, .5f });
                results.Add(touchVisual.Stroke.StylusPoints.ToArray());
                check((int?)stateType.GetProperty("LastTouchEventTimestamp").GetValue(touchState) == 144,
                    "Touch interpolation lost the hardware event timestamp");
            }
            check(results[0].Length > 0 && results[0].SequenceEqual(results[1]),
                "Identical touch input timestamps produced different geometry/pressure when processed later");
        }
        finally
        {
            SettingsManager.Settings.Canvas.InkStyle = style;
            SettingsManager.Settings.Canvas.VelocityBrushTipMix = mix;
            SettingsManager.Settings.Canvas.DisablePressure = disablePressure;
        }
        foreach (var name in new[] { "_pauseStraightenTimers", "_pauseStraightenLastInput", "<StrokeVisualList>k__BackingField" })
        {
            var field = typeof(MainWindow).GetField(name, Hidden);
            field.SetValue(owner, Activator.CreateInstance(field.FieldType));
        }
        var timers = (IDictionary)typeof(MainWindow).GetField("_pauseStraightenTimers", Hidden).GetValue(owner);
        var lastInput = (IDictionary)typeof(MainWindow).GetField("_pauseStraightenLastInput", Hidden).GetValue(owner);
        var reset = typeof(MainWindow).GetMethod("ResetPauseStraightenTimer", Hidden);
        var cancel = typeof(MainWindow).GetMethod("CancelPauseStraightenTimer", Hidden);
        bool enabled = SettingsManager.Settings.Canvas.PauseStraightenLine;
        int delay = SettingsManager.Settings.Canvas.PauseStraightenDelay;
        try
        {
            SettingsManager.Settings.Canvas.PauseStraightenLine = true;
            SettingsManager.Settings.Canvas.PauseStraightenDelay = 500;
            reset.Invoke(owner, new object[] { 42 });
            var timer = (DispatcherTimer)timers[42];
            timer.Interval = TimeSpan.FromMilliseconds(321);
            for (int i = 0; i < 1000; i++) reset.Invoke(owner, new object[] { 42 });
            check(ReferenceEquals(timer, timers[42]) && timer.Interval.TotalMilliseconds == 321,
                "Move events restarted or reconfigured the pause timer");
            var tick = (EventHandler)typeof(DispatcherTimer).GetField("Tick", Hidden).GetValue(timer);
            tick(timer, EventArgs.Empty);
            check(timers.Contains(42) && timer.Interval.TotalMilliseconds > 0, "An active stroke was straightened before its deadline");
            lastInput[42] = Environment.TickCount64 - 1000;
            tick(timer, EventArgs.Empty);
            check(!timers.Contains(42) && !lastInput.Contains(42) && !timer.IsEnabled, "Expired pause timer retained its state");
            reset.Invoke(owner, new object[] { 43 }); cancel.Invoke(owner, new object[] { 43 });
            check(timers.Count == 0 && lastInput.Count == 0, "Canceled input retained its pause deadline");
        }
        finally
        {
            foreach (DispatcherTimer timer in timers.Values) timer.Stop();
            SettingsManager.Settings.Canvas.PauseStraightenLine = enabled;
            SettingsManager.Settings.Canvas.PauseStraightenDelay = delay;
            scheduler.GetMethod("Clear", Hidden).Invoke(null, null);
        }
    }
}
