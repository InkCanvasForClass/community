using System.Dynamic;
using System.Windows;
using System.Windows.Controls;

// Only the host boundary is replaced. Tests compile the production PPTUIManager
// and execute its queued updates on a real WPF dispatcher.
namespace Ink_Canvas
{
    public sealed class MainWindow : Window
    {
        public static dynamic Settings { get; } = CreateSettings();
        public bool IsExternalPresentationActive { get; set; }
        public int ExternalPresentationPageCount { get; set; }
        public bool IsInPPTPresentationMode { get; set; }
        public bool IsWhiteboardMode { get; set; }
        public bool ArePPTControlsVisible { get; set; }
        public bool isFullScreenApplied;
        public TestPptSession? PPTManager { get; set; }
        public TestBar LeftBottomPanelForPPTNavigation { get; } = new();
        public TestBar RightBottomPanelForPPTNavigation { get; } = new();
        public TestBar LeftSidePanelForPPTNavigation { get; } = new();
        public TestBar RightSidePanelForPPTNavigation { get; } = new();
        public FrameworkElement BtnExitPPTFromSidebarLeft { get; } = new Border();
        public FrameworkElement BtnExitPPTFromSidebarRight { get; } = new Border();
        public FrameworkElement ViewboxFloatingBar { get; } = new Border();
        public TextBlock PPTBtnPageNow { get; } = new();
        public TextBlock PPTBtnPageTotal { get; } = new();
        public void RefreshPPTPageKeyHook() { }
        public void UpdateToolbarComponentVisibility() { }
        public void UpdatePPTUIManagerSettings() { }
        public void UpdatePPTTimeCapsuleVisibility() { }
        public void UpdatePPTQuickPanelVisibility() { }
        public void ShowPPTModePromptNotification() { }
        public static void MoveWindow(IntPtr handle, int x, int y, int width, int height, bool repaint) { }

        private static dynamic CreateSettings()
        {
            dynamic settings = new ExpandoObject();
            dynamic ppt = new ExpandoObject();
            settings.PowerPointSettings = ppt;
            ppt.ShowPPTButton = true;
            ppt.PPTButtonsDisplayOption = 2222;
            ppt.PPTSButtonsOption = 221;
            ppt.PPTBButtonsOption = 121;
            ppt.EnablePPTButtonPageClickable = true;
            ppt.EnablePPTButtonLongPressPageTurn = true;
            ppt.PPTNavBarScale = 1.0;
            ppt.PPTGlobalButtonEnabled = true;
            ppt.PPTGlobalShowPageNumber = true;
            ppt.PPTGlobalBlackBackground = false;
            ppt.PPTGlobalSideButtonPosition = 0;
            ppt.PPTGlobalBottomButtonPosition = 0;
            ppt.PPTGlobalButtonOpacity = 0.5;
            ppt.PPTGlobalButtonScale = 1.0;
            var values = (IDictionary<string, object>)ppt;
            foreach (var side in new[] { "LS", "RS", "LB", "RB" })
            {
                values[$"PPT{side}UseGlobalSettings"] = false;
                values[$"PPT{side}ButtonPosition"] = 0;
                values[$"PPT{side}ButtonOpacity"] = 0.5;
                values[$"PPT{side}ButtonScale"] = 1.0;
                values[$"PPT{side}ShowPageNumber"] = true;
                values[$"PPT{side}BlackBackground"] = false;
            }
            settings.Automation = new ExpandoObject();
            settings.Automation.IsAutoFoldInPPTSlideShow = false;
            settings.Advanced = new ExpandoObject();
            settings.Advanced.IsEnableAvoidFullScreenHelper = false;
            return settings;
        }
    }

    public sealed class TestPptSession
    {
        public bool IsInSlideShow { get; set; }
        public int SlidesCount { get; set; }
    }

    public sealed class TestBar : Border
    {
        public int CurrentSlide { get; set; }
        public int TotalSlides { get; set; }
        public void SetPageButtonVisibility(Visibility visibility) { }
        public void SetBarOpacity(double opacity) { }
        public void ApplyTheme(bool black) { }
        public void SetBarScale(double scale) { }
    }
}

namespace Ink_Canvas.Helpers
{
    internal static class AnimationsHelper
    {
        public static void ShowWithFadeIn(UIElement element) => element.Visibility = Visibility.Visible;
    }

    internal static class LogHelper
    {
        public enum LogType { Error }
        public static List<string> Errors { get; } = new();
        public static void WriteLogToFile(string message, LogType type) => Errors.Add(message);
    }

    internal static class AvoidFullScreenHelper
    {
        public static void SetBoardMode(bool enabled) { }
    }
}

namespace System.Windows.Forms
{
    internal static class Screen
    {
        public static TestScreen PrimaryScreen { get; } = new();
        internal sealed class TestScreen
        {
            public TestRect Bounds => new();
            public TestRect WorkingArea => new();
        }
        internal sealed class TestRect
        {
            public int X => 0;
            public int Y => 0;
            public int Width => 1920;
            public int Height => 1080;
        }
    }
}
