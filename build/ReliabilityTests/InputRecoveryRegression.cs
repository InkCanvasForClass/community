using System.Collections.Concurrent;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Ink_Canvas.Helpers;
using Ink_Canvas.Ink;
using Ink_Canvas.Ink.WinRT;

internal static class InputRecoveryRegression
{
    internal static void Run(Action<bool, string> check)
    {
        // No visible test window, and no synthetic input sent to the user's applications.
        using (var source = new HwndSource(new HwndSourceParameters("ICC chrome regression")
        { PositionX = -32000, PositionY = -32000, Width = 400, Height = 300, WindowStyle = unchecked((int)0x80000000) }))
        {
            var root = new Canvas { Width = 400, Height = 300 };
            var parent = new Canvas { IsHitTestVisible = false };
            var toolbar = new Border { Width = 150, Height = 40, IsHitTestVisible = true };
            Canvas.SetLeft(toolbar, 20); Canvas.SetTop(toolbar, 30);
            parent.Children.Add(toolbar); root.Children.Add(parent); source.RootVisual = root;
            root.Measure(new Size(400, 300)); root.Arrange(new Rect(0, 0, 400, 300)); root.UpdateLayout();
            check(!toolbar.IsHitTestVisible, "Disabled ancestor did not reproduce the toolbar fault");
            check(CanvasChromeGeometry.TryGetBounds(toolbar, root, out var reserved)
                && reserved.Contains(new Point(25, 35)), "A disabled toolbar became writable canvas");
            toolbar.Visibility = Visibility.Collapsed;
            check(!CanvasChromeGeometry.TryGetBounds(toolbar, root, out _), "Collapsed toolbar reserved invisible canvas space");
            var detached = new Border { Width = 100, Height = 30 };
            check(!CanvasChromeGeometry.TryGetBounds(detached, root, out _), "Detached chrome produced an input region");
        }

        var stroke = new Stroke(new StylusPointCollection { new StylusPoint(1, 2), new StylusPoint(3, 4) });
        stroke.DrawingAttributes.Width = 7;
        var live = new StrokeCollection { stroke };
        using var baseline = new MemoryStream();
        live.Save(baseline); baseline.Position = 0;
        var expected = new StrokeCollection(baseline);
        var snapshot = StrokeSaveSnapshot.Capture(live);
        // Edits and page changes after capture cannot race ISF encoding on the worker.
        stroke.StylusPoints[0] = new StylusPoint(900, 901);
        stroke.DrawingAttributes.Width = 20; live.Clear();
        byte[] bytes = Task.Run(snapshot.Encode).GetAwaiter().GetResult();
        using (var stream = new MemoryStream(bytes))
        {
            var restored = new StrokeCollection(stream);
            check(restored.Count == 1 && restored[0].StylusPoints.SequenceEqual(expected[0].StylusPoints)
                && restored[0].DrawingAttributes.Width == expected[0].DrawingAttributes.Width,
                "Background save encoded a changing live canvas instead of its snapshot");
        }

        var dispatcher = Dispatcher.CurrentDispatcher;
        int touchFeed = 0, inkMoves = 0, ended = 0;
        var gate = new WinRTInkInputGate(_ => PointerGateResult.AllowInk, null, null, null,
            (_, _) => touchFeed++, (_, _) => touchFeed++, (_, _) => touchFeed++, null,
            (_, _, _) => inkMoves++, null, null, null, null, _ => ended++, () => { })
        { ForwardTouchPositions = false, ForwardInkMovement = false };
        Windows.UI.Core.PointerEventArgs Pointer() => new()
        {
            CurrentPoint = new() { PointerId = 42, Position = new Point(20, 30),
                PointerDevice = new() { PointerDeviceType = Windows.Devices.Input.PointerDeviceType.Touch } }
        };
        gate.OnPointerPressing(null, Pointer());
        for (int i = 0; i < 10000; i++) gate.OnPointerMoving(null, Pointer());
        gate.OnPointerReleasing(null, Pointer());
        gate.OnStrokeEnded(null, Pointer());
        check(touchFeed == 0 && inkMoves == 0 && ended == 1,
            "Disabled gesture/straighten features still flooded the UI queue or broke drying");
        var messages = new ConcurrentQueue<string>();
        using var warned = new ManualResetEventSlim();
        int inputProbes = 0;
        void Posted(object sender, DispatcherHookEventArgs args)
        { if (args.Operation.Priority == DispatcherPriority.Input) Interlocked.Increment(ref inputProbes); }
        dispatcher.Hooks.OperationPosted += Posted;
        try
        {
            using var monitor = new UiResponsivenessMonitor(dispatcher, message =>
            { messages.Enqueue(message); if (message.Contains("pending=")) warned.Set(); }, 10, 40);
            using (monitor.EnterOperation("regression-blocked-ui"))
                check(warned.Wait(TimeSpan.FromSeconds(3)), "UI stall went undetected while the dispatcher was blocked");
            check(inputProbes == 1, "Blocked UI accumulated unbounded responsiveness probes");
            var frame = new DispatcherFrame();
            dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
            check(messages.Any(message => message.Contains("recovered=")), "UI recovery was not recorded");
            check(messages.Any(message => message.Contains("operation=regression-blocked-ui")), "Stall lost its active operation context");
        }
        finally { dispatcher.Hooks.OperationPosted -= Posted; }
    }
}
