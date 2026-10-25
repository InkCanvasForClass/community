using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Ink_Canvas.Helpers;

internal static class Program
{
    private static int _checks;

    [STAThread]
    private static void Main()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var overlay = new Canvas { Width = 100, Height = 100 };
        var host = new Window { Width = 800, Height = 600, Topmost = true, Content = overlay };
        var button = new Button { Content = "Test", Width = 160, Height = 60 };
        var cloud = new Window { Width = 450, Height = 400, Topmost = true, Content = button };
        var touch = new TestTouchDevice();
        try
        {
            host.Show();
            cloud.Owner = host;
            cloud.Show();
            cloud.Activate();
            Pump();

            Check(Mouse.Capture(button), "button capture acquired");
            overlay.ReleaseStylusCapture();
            Pump();
            Check(Mouse.Captured == null, "baseline: inactive overlay clears another window's mouse capture");

            overlay.IsHitTestVisible = false;
            overlay.Visibility = Visibility.Collapsed;
            Check(Mouse.Capture(button), "button recapture acquired");
            for (var i = 0; i < 20; i++)
            {
                InputCaptureHelper.ReleaseOwnedCaptures(overlay);
                Pump();
                Check(ReferenceEquals(Mouse.Captured, button), "cleanup preserves another window's button capture");
            }
            Mouse.Capture(null);

            var menu = new ContextMenu { PlacementTarget = button };
            menu.Items.Add(new MenuItem { Header = "Account" });
            menu.IsOpen = true;
            Pump();
            Check(ReferenceEquals(Mouse.Captured, menu), "context menu owns capture");
            InputCaptureHelper.ReleaseOwnedCaptures(overlay);
            Pump();
            Check(menu.IsOpen && ReferenceEquals(Mouse.Captured, menu), "cleanup preserves context menu capture");
            menu.IsOpen = false;
            Pump();

            overlay.Visibility = Visibility.Visible;
            overlay.IsHitTestVisible = true;
            Check(Mouse.Capture(overlay), "eraser capture acquired");
            InputCaptureHelper.ReleaseOwnedCaptures(overlay);
            Check(Mouse.Captured == null, "cleanup still releases the overlay's own mouse capture");
            InputCaptureHelper.ReleaseOwnedCaptures(overlay);
            Check(Mouse.Captured == null, "repeated cleanup with no capture is harmless");

            touch.Start(PresentationSource.FromVisual(host));
            Check(touch.Capture(overlay), "eraser touch capture acquired");
            Check(Mouse.Capture(button), "button mouse capture acquired alongside eraser touch");
            InputCaptureHelper.ReleaseOwnedCaptures(overlay);
            Check(touch.Captured == null, "cleanup still releases the overlay's own touch capture");
            Check(ReferenceEquals(Mouse.Captured, button), "releasing owned touch preserves another window's mouse capture");
            Mouse.Capture(null);

            touch.Stop();
            touch.Start(PresentationSource.FromVisual(cloud));
            Check(touch.Capture(button), "button touch capture acquired");
            Check(Mouse.Capture(button), "button mouse capture acquired alongside touch");
            InputCaptureHelper.ReleaseOwnedCaptures(overlay);
            Check(ReferenceEquals(touch.Captured, button), "cleanup preserves another window's touch capture");
            Check(ReferenceEquals(Mouse.Captured, button), "touch cleanup preserves another window's mouse capture");

            Console.WriteLine($"PASS: {_checks} checks; baseline reproduced; unrelated button, popup and touch captures preserved; owned captures released");
        }
        finally
        {
            touch.Stop();
            Mouse.Capture(null);
            cloud.Close();
            host.Close();
            app.Shutdown();
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        _checks++;
    }

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private sealed class TestTouchDevice : TouchDevice
    {
        internal TestTouchDevice() : base(10001) { }

        internal void Start(PresentationSource source)
        {
            SetActiveSource(source);
            Activate();
        }

        internal void Stop()
        {
            if (!IsActive) return;
            Capture(null);
            Deactivate();
        }

        public override TouchPoint GetTouchPoint(IInputElement relativeTo)
            => new(this, new Point(), Rect.Empty, TouchAction.Move);

        public override TouchPointCollection GetIntermediateTouchPoints(IInputElement relativeTo)
            => new() { GetTouchPoint(relativeTo) };
    }
}
