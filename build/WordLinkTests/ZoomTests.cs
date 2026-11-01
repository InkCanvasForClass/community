using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Ink_Canvas.Controls;
using Ink_Canvas.WordLink;

internal static partial class Program
{
    private static void ZoomTests(string root)
    {
        var canvas = new InkCanvas();
        using var surface = new HwndSource(new HwndSourceParameters("Word zoom fixture") { Width = 1200, Height = 1000 });
        surface.RootVisual = canvas; canvas.Measure(new Size(1200, 1000)); canvas.Arrange(new Rect(0, 0, 1200, 1000));
        var bar = new WordLectureBar();
        var transport = new FrameSource { Frame = Frame() };
        transport.Frame.ZoomPercentage = 100;
        long targetWindow = 10;
        using var controller = new WordLectureController(canvas, bar, Path.Combine(root, "zoom"), () => true,
            () => false, _ => { }, action => action(), (_, _) => { }, transport, _ => targetWindow, automaticPolling: false);
        controller.Configure(true, true); Wait(controller.PollAsync());
        bar.ChangeZoom(10);
        Check(controller.BlocksInput && !canvas.IsHitTestVisible, "Ink remained writable against geometry awaiting a zoom change");
        transport.Frame.ZoomPercentage = 110;
        transport.BeforeReply = async () => { bar.ChangeZoom(10); bar.ChangeZoom(10); await Task.Delay(10); };
        Wait(controller.PollAsync());
        Check(transport.LastRequest.TargetZoomPercentage == 110 && controller.BlocksInput,
            "An in-flight zoom reply overwrote newer clicks");
        transport.BeforeReply = null;
        transport.Frame.Status = "Busy"; Wait(controller.PollAsync());
        Check(transport.LastRequest.TargetZoomPercentage == 130 && controller.BlocksInput,
            "Rapid zoom clicks did not merge into one absolute target");
        transport.Frame.Status = "Ready"; transport.Frame.ZoomPercentage = 130;
        Wait(controller.PollAsync());
        Check(transport.LastRequest.TargetZoomPercentage == 130 && !controller.BlocksInput,
            "Busy recovery lost the zoom target or failed to restore ink input");
        bar.ChangeZoom(10); bar.ChangeZoom(-10);
        Wait(controller.PollAsync());
        Check(transport.LastRequest.TargetZoomPercentage == 130 && !controller.BlocksInput,
            "Reversing zoom while pending accumulated an incorrect target");
        bar.ChangeZoom(1000); transport.Frame.ZoomPercentage = 500; Wait(controller.PollAsync());
        Check(transport.LastRequest.TargetZoomPercentage == 500, "Zoom exceeded Word's maximum");
        bar.ChangeZoom(-1000); transport.Frame.ZoomPercentage = 10; Wait(controller.PollAsync());
        Check(transport.LastRequest.TargetZoomPercentage == 10, "Zoom fell below Word's minimum");
        bar.ChangeZoom(10); targetWindow = transport.Frame.WindowHandle = 20;
        Wait(controller.PollAsync());
        Check(transport.LastRequest.TargetZoomPercentage == 0 && !controller.BlocksInput,
            "A queued zoom was redirected to a different Word window");
        bar.ChangeZoom(10);
        transport.Frame.DocumentKey = new string('c', 64);
        Wait(controller.PollAsync()); Wait(controller.PollAsync());
        Check(transport.LastRequest.TargetZoomPercentage == 0 && !controller.BlocksInput,
            "A changed document inherited a pending zoom command");
        bar.ChangeZoom(10); transport.Frame.ZoomPercentage = 15;
        Wait(controller.PollAsync()); Wait(controller.PollAsync());
        Check(transport.LastRequest.TargetZoomPercentage == 0 && !controller.BlocksInput,
            "Word's constrained zoom read-back caused an endless retry loop");
        bar.ChangeZoom(10); transport.Frame.Status = "PrintLayoutRequired";
        Wait(controller.PollAsync());
        transport.Frame.Status = "Ready"; Wait(controller.PollAsync());
        Check(transport.LastRequest.TargetZoomPercentage == 0 && !controller.BlocksInput,
            "Unsupported Word view retained a command that repeatedly woke Office");
    }
}
