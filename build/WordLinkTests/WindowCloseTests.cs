using System.IO;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Interop;
using Ink_Canvas.WordLink;

internal static partial class Program
{
    private static void WindowCloseTests(string root)
    {
        var canvas = new InkCanvas();
        using var surface = new HwndSource(new HwndSourceParameters("Word close fixture") { Width = 1200, Height = 1000 });
        surface.RootVisual = canvas;
        var desktop = Stroke(10, 10); canvas.Strokes.Add(desktop);
        var source = new FrameSource { Frame = Frame() };
        using var controller = new WordLectureController(canvas, new Ink_Canvas.Controls.WordLectureBar(),
            Path.Combine(root, "close-pending"), () => true, () => false, _ => { }, action => action(), (_, _) => { },
            source, _ => 10, automaticPolling: false);
        controller.Configure(true, true); Wait(controller.PollAsync());
        canvas.Strokes.Add(Stroke(150, 180)); Pump();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        source.BeforeReply = () => release.Task;
        var pending = controller.PollAsync();
        Check(!pending.IsCompleted, "Close fixture did not hold an in-flight view query");
        controller.NotifyWindowClosed(99);
        Check(controller.IsActive, "A different window's close detached the active document");
        controller.NotifyWindowClosed(10);
        Check(!controller.IsActive && !controller.BlocksInput && controller.WindowHandle == 0
            && canvas.Strokes.Count == 1 && ReferenceEquals(canvas.Strokes[0], desktop),
            "Closing Word waited for its pending query or lost desktop ink");
        release.SetResult(); Wait(pending);
        Check(!controller.IsActive && canvas.Strokes.Count == 1 && ReferenceEquals(canvas.Strokes[0], desktop),
            "A late reply restored a closed Word session over the desktop");
        controller.NotifyWindowClosed(10);
        Check(canvas.Strokes.Count == 1, "Repeated close recovery changed desktop ink");
    }
}
