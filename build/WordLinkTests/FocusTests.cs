using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Ink_Canvas.Controls;
using Ink_Canvas.WordLink;

internal static partial class Program
{
    private static void FocusTests(string root)
    {
        var original = new Thickness(120, 210, 0, -20);
        var toolbar = new Border { Width = 300, Height = 60, Margin = original, Opacity = .9,
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        toolbar.Measure(new Size(1200, 800)); toolbar.Arrange(new Rect(0, 0, 1200, 800));
        Rect bounds = new(0, 0, 1000, 800); bool folded = false; int cancelled = 0;
        using (var presentation = new WordFocusToolbarPresentation(toolbar, () => bounds, () => .5, () => cancelled++, () => !folded))
        {
            presentation.Update(true);
            Check(toolbar.Margin.Left == 350 && toolbar.Margin.Top == 742 && toolbar.Opacity == .5,
                "Word focus did not use a bottom-centered, translucent presentation toolbar");
            presentation.Update(true);
            bounds = new Rect(0, 0, 1200, 900); presentation.Refresh();
            Check(toolbar.Margin.Left == 450 && toolbar.Margin.Top == 842, "Focus toolbar did not follow screen geometry changes");
            presentation.Update(false);
            Check(toolbar.Margin == original && toolbar.Opacity == .9, "Exiting focus lost the original toolbar position or opacity");
            presentation.Update(true); folded = true;
            var foldMargin = new Thickness(0, 1200, 0, -20); toolbar.Margin = foldMargin;
            int beforeFoldExit = cancelled;
            presentation.Refresh(); presentation.Update(false);
            Check(toolbar.Margin == foldMargin && toolbar.Opacity == .9 && cancelled == beforeFoldExit,
                "Leaving focus unexpectedly cancelled a fold animation or unfolded a hidden toolbar");
            folded = false; toolbar.Margin = original; presentation.Update(true);
        }
        Check(toolbar.Margin == original && toolbar.Opacity == .9 && cancelled > 0,
            "Disposing the focus presentation failed to restore the desktop toolbar");
        var opacityLayer = new Grid { Opacity = 1 };
        opacityLayer.Children.Add(toolbar);
        using (var layered = new WordFocusToolbarPresentation(toolbar, () => bounds, () => .5, () => { }, opacitySurface: opacityLayer))
        {
            layered.Update(true);
            toolbar.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, TimeSpan.Zero) { FillBehavior = FillBehavior.HoldEnd });
            toolbar.Opacity = 1; // Reproduce the theme/show animation overwriting the inner value.
            Check(opacityLayer.Opacity == .5 && toolbar.Opacity * opacityLayer.Opacity <= .5,
                "A theme or retained opacity animation made the focus toolbar opaque again");
        }
        Check(opacityLayer.Opacity == 1 && toolbar.Opacity == .9, "Leaving focus failed to remove animation and restore both opacity layers");

        var canvas = new InkCanvas(); var source = new FrameSource { Frame = Frame() };
        using var surface = new HwndSource(new HwndSourceParameters("Word focus state fixture") { Width = 1200, Height = 1000 });
        surface.RootVisual = canvas; canvas.Measure(new Size(1200, 1000)); canvas.Arrange(new Rect(0, 0, 1200, 1000));
        var changes = new List<bool>();
        using var controller = new WordLectureController(canvas, new WordLectureBar(), Path.Combine(root, "focus"),
            () => true, () => false, _ => { }, action => action(), (_, _) => { }, source, _ => 10,
            automaticPolling: false, focusChanged: changes.Add);
        controller.Configure(true, true); Wait(controller.PollAsync());
        source.Frame.IsFocusMode = true; Wait(controller.PollAsync()); Wait(controller.PollAsync());
        Check(changes.SequenceEqual(new[] { true }), "Repeated focus frames overwrote the presentation's saved state");
        source.Frame.Status = "Busy"; Wait(controller.PollAsync());
        Check(changes.Count == 1, "Transient Office busy state reset the focused toolbar");
        source.Frame.Status = "Ready"; source.Frame.IsFocusMode = false; Wait(controller.PollAsync());
        Check(changes.SequenceEqual(new[] { true, false }), "Leaving Word focus was not reported to the toolbar");
        source.Frame.IsFocusMode = true; Wait(controller.PollAsync()); controller.Suspend();
        Check(changes.SequenceEqual(new[] { true, false, true, false }), "Leaving Word failed to restore its focused toolbar");
        changes.Clear(); source.Frame.IsFocusMode = false;
        controller.Configure(true, true, autoEnterFocus: true); Wait(controller.PollAsync());
        Check(controller.BlocksInput, "Auto Focus did not queue a command after document connection");
        source.Frame.Status = "Locating"; Wait(controller.PollAsync());
        Check(source.LastRequest.TargetFocusMode == true && controller.BlocksInput, "Auto Focus did not use a retryable absolute target");
        source.Frame.Status = "Ready"; source.Frame.IsFocusMode = true; Wait(controller.PollAsync());
        Check(!controller.BlocksInput && changes.SequenceEqual(new[] { true }), "Auto Focus never completed its mode/geometry confirmation");
        controller.RequestFocusMode(false); source.Frame.IsFocusMode = false; Wait(controller.PollAsync());
        Check(source.LastRequest.TargetFocusMode == false && !controller.BlocksInput,
            "Exit Focus did not send a command or immediately auto-entered again");
        controller.Suspend(); Wait(controller.PollAsync()); Wait(controller.PollAsync());
        Check(source.LastRequest.TargetFocusMode == null && !controller.BlocksInput,
            "Returning to a manually exited document retriggered Auto Focus");
        source.Frame.IsFocusMode = true; Wait(controller.PollAsync());
        source.Frame.Revision = new string('c', 64); Wait(controller.PollAsync());
        controller.RequestFocusMode(false); source.Frame.IsFocusMode = false; Wait(controller.PollAsync());
        Check(source.LastRequest.TargetFocusMode == false && changes.Last() == false,
            "A paused annotation layout prevented Exit Focus from restoring the toolbar");
    }
}
