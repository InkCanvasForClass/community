using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Ink_Canvas.Controls;
using Ink_Canvas.WordLink;

internal static partial class Program
{
    private static void PlacementTests(string root)
    {
        var leftSize = new Size(136, 46); var rightSize = new Size(269, 46);
        var corners = WordToolbarPlacement.Calculate(new Rect(100, 80, 900, 600), leftSize, rightSize);
        Check(corners.Left == new Point(106, 628) && corners.Right == new Point(725, 628),
            "Word controls were not anchored inside the document window corners");
        var narrow = WordToolbarPlacement.Calculate(new Rect(100, 80, 320, 400), leftSize, rightSize);
        Check(narrow.Left.Y + leftSize.Height < narrow.Right.Y && narrow.Right.X >= 100,
            "Narrow Word windows overlapped the two navigation controls");
        var tiny = WordToolbarPlacement.Calculate(new Rect(0, 0, 320, 60), leftSize, rightSize);
        Check(tiny.Left.X < 0 && tiny.Right.X >= 0, "Tiny window failed to preserve the right-side controls without overlap");
        var outside = WordToolbarPlacement.Calculate(Rect.Empty, leftSize, rightSize);
        Check(outside.Left.X < 0 && outside.Right.X < 0, "Offscreen Word window retained on-screen controls");
        var offset = WordToolbarPlacement.Calculate(new Rect(100, 80, 900, 600), leftSize, rightSize, 40, 30);
        Check(offset.Left == new Point(140, 604) && offset.Right == new Point(691, 604),
            "Configured navigation margins were not applied");
        var constrained = WordToolbarPlacement.Calculate(new Rect(100, 80, 320, 400), leftSize, rightSize, 300, 200);
        Check(constrained.Right.X >= 100 && constrained.Right.X + rightSize.Width <= 420
            && constrained.Left.Y >= 80, "Large configured margins pushed navigation outside a narrow window");

        using var surface = new HwndSource(new HwndSourceParameters("Word window tracking fixture")
            { Width = 1200, Height = 800, PositionX = 100, PositionY = 100 });
        var overlay = new Canvas(); var ink = new InkCanvas { Width = 1200, Height = 800 };
        var left = new Border { Width = leftSize.Width, Height = leftSize.Height };
        var right = new Border { Width = rightSize.Width, Height = rightSize.Height };
        overlay.Children.Add(ink); overlay.Children.Add(left); overlay.Children.Add(right);
        surface.RootVisual = overlay; overlay.Measure(new Size(1200, 800)); overlay.Arrange(new Rect(0, 0, 1200, 800));
        overlay.UpdateLayout();
        Rect? screen = null;
        void Move(Rect local) => screen = new Rect(overlay.PointToScreen(local.TopLeft), overlay.PointToScreen(local.BottomRight));
        using var placement = new WordToolbarPlacement(overlay, left, right, _ => screen);
        var transport = new FrameSource { Frame = Frame() };
        var bar = new WordLectureBar();
        using var controller = new WordLectureController(ink, bar, Path.Combine(root, "placement"), () => true,
            () => false, _ => { }, action => action(), (_, _) => { }, transport, _ => 10,
            automaticPolling: false, placeWindow: placement.Update);
        Move(new Rect(100, 80, 900, 600));
        controller.Configure(true, true); Wait(controller.PollAsync()); Pump(30);
        Check(Math.Abs(Canvas.GetLeft(left) - 106) < .5 && Math.Abs(Canvas.GetTop(right) - 628) < .5,
            "Physical screen bounds were not converted into overlay coordinates");

        var release = new TaskCompletionSource();
        transport.BeforeReply = () => release.Task;
        var pending = controller.PollAsync();
        try
        {
            int calls = transport.Calls;
            Move(new Rect(300, 140, 600, 500)); controller.NotifyViewChanged(); Pump(30);
            Check(!pending.IsCompleted && Math.Abs(Canvas.GetLeft(left) - 306) < .5
                && Math.Abs(Canvas.GetLeft(right) - 625) < .5 && transport.Calls == calls,
                "Window tracking waited for Office or launched another document query");
            Move(new Rect(-200, 100, 600, 400)); controller.NotifyViewChanged(); Pump(30);
            Check(Canvas.GetLeft(left) >= 0 && Canvas.GetLeft(right) >= 0,
                "A partially offscreen Word window placed controls outside the overlay");
            screen = null; controller.NotifyViewChanged(); Pump(30);
            Check(Canvas.GetLeft(left) < 0 && Canvas.GetLeft(right) < 0,
                "Minimized/unavailable window left stale controls visible");
        }
        finally { release.TrySetResult(); Wait(pending); }
        controller.Suspend(); Pump(30);
        Check(Canvas.GetLeft(left) < 0 && Canvas.GetLeft(right) < 0, "Suspending Word retained toolbar placement");
        Move(new Rect(100, 80, 900, 600)); placement.Configure(false, 20, 30); placement.Update(10); Pump(30);
        Check(Math.Abs(Canvas.GetLeft(left) - 20) < .5 && Math.Abs(Canvas.GetLeft(right) - 911) < .5
            && Math.Abs(Canvas.GetTop(right) - 724) < .5, "Fixed navigation did not honor the canvas corners and configured margins");
    }
}
