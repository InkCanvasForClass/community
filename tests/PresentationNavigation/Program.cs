using System.Windows;
using System.Windows.Threading;
using Ink_Canvas;
using Ink_Canvas.Helpers;

internal static class Program
{
    private static int _checks;

    [STAThread]
    private static void Main()
    {
        var host = new MainWindow
        {
            IsExternalPresentationActive = true,
            ExternalPresentationPageCount = 12,
            IsInPPTPresentationMode = true
        };
        var manager = new PPTUIManager(host);
        Check(host.PPTManager == null, "external presentation has no PowerPoint session");
        Refresh(manager);
        CheckBars(host, "2222", "external presentation shows navigation");

        // Reproduce the ordering in ImageBlackboard_MouseUp: unfold queues work,
        // the synchronous whiteboard transition completes, then the queue runs.
        host.IsWhiteboardMode = true;
        Refresh(manager);
        CheckBars(host, "1111", "queued unfold update stays hidden inside whiteboard");
        host.Dispatcher.BeginInvoke(new Action(manager.UpdateNavigationPanelsVisibility));
        host.IsWhiteboardMode = false;
        manager.UpdateNavigationPanelsVisibility();
        Pump();
        CheckBars(host, "2222", "whiteboard exit survives the delayed unfold update");

        MainWindow.Settings.PowerPointSettings.PPTButtonsDisplayOption = 2112;
        Refresh(manager);
        CheckBars(host, "2112", "individual button choices survive whiteboard exit");
        MainWindow.Settings.PowerPointSettings.PPTLBUseGlobalSettings = true;
        MainWindow.Settings.PowerPointSettings.PPTGlobalButtonEnabled = false;
        Refresh(manager);
        CheckBars(host, "1112", "global button overrides are respected");
        MainWindow.Settings.PowerPointSettings.PPTLBUseGlobalSettings = false;
        MainWindow.Settings.PowerPointSettings.PPTButtonsDisplayOption = 2222;

        MainWindow.Settings.PowerPointSettings.ShowPPTButton = false;
        Refresh(manager);
        CheckBars(host, "1111", "disabled navigation remains hidden");
        MainWindow.Settings.PowerPointSettings.ShowPPTButton = true;
        MainWindow.Settings.Automation.IsAutoFoldInPPTSlideShow = true;
        Refresh(manager);
        CheckBars(host, "1111", "automatic slideshow fold keeps navigation hidden");
        MainWindow.Settings.Automation.IsAutoFoldInPPTSlideShow = false;

        host.ExternalPresentationPageCount = 0;
        Refresh(manager);
        CheckBars(host, "1111", "invalid external page count stays hidden");
        host.ExternalPresentationPageCount = 12;
        host.IsInPPTPresentationMode = false;
        Refresh(manager);
        CheckBars(host, "1111", "ended presentation stays hidden");

        host.IsExternalPresentationActive = false;
        host.IsInPPTPresentationMode = true;
        host.PPTManager = new TestPptSession { IsInSlideShow = true, SlidesCount = 8 };
        Refresh(manager);
        CheckBars(host, "2222", "PowerPoint navigation still shows");
        host.IsWhiteboardMode = true;
        Refresh(manager);
        CheckBars(host, "1111", "PowerPoint navigation is hidden in whiteboard");
        host.IsWhiteboardMode = false;
        Refresh(manager);
        CheckBars(host, "2222", "PowerPoint navigation restores after whiteboard");
        host.PPTManager.IsInSlideShow = false;
        Refresh(manager);
        CheckBars(host, "1111", "inactive PowerPoint session stays hidden");

        Check(LogHelper.Errors.Count == 0, "no errors swallowed by UI manager");
        Console.WriteLine($"PASS: {_checks} checks; external and PowerPoint navigation, queued whiteboard transitions and display settings");
    }

    private static void Refresh(PPTUIManager manager)
    {
        manager.UpdateNavigationPanelsVisibility();
        Pump();
    }

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static void CheckBars(MainWindow host, string options, string message)
    {
        var bars = new[] { host.LeftBottomPanelForPPTNavigation, host.RightBottomPanelForPPTNavigation,
            host.LeftSidePanelForPPTNavigation, host.RightSidePanelForPPTNavigation };
        for (var i = 0; i < bars.Length; i++)
            Check(bars[i].Visibility == (options[i] == '2' ? Visibility.Visible : Visibility.Collapsed), $"{message} (bar {i})");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        _checks++;
    }
}
