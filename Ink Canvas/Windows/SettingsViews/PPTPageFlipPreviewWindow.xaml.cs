using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Ink_Canvas.Windows.SettingsViews
{
    public partial class PPTPageFlipPreviewWindow : Window
    {
        public static PPTPageFlipPreviewWindow ActiveInstance { get; private set; }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        [DllImport("user32.dll")]
        private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        public PPTPageFlipPreviewWindow()
        {
            InitializeComponent();
            ActiveInstance = this;
            
            // Set dummy page numbers for preview bars
            LeftSidePanelForPPTNavigation.CurrentSlide = 2;
            LeftSidePanelForPPTNavigation.TotalSlides = 5;
            RightSidePanelForPPTNavigation.CurrentSlide = 2;
            RightSidePanelForPPTNavigation.TotalSlides = 5;
            LeftBottomPanelForPPTNavigation.CurrentSlide = 2;
            LeftBottomPanelForPPTNavigation.TotalSlides = 5;
            RightBottomPanelForPPTNavigation.CurrentSlide = 2;
            RightBottomPanelForPPTNavigation.TotalSlides = 5;

            Closed += PPTPageFlipPreviewWindow_Closed;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE);

                // 精确定位到主屏幕整个边界（与 MainWindow 一致，避免 Maximized 导致的几像素溢出）
                var screen = System.Windows.Forms.Screen.PrimaryScreen;
                Left = screen.Bounds.X;
                Top = screen.Bounds.Y;
                Width = screen.Bounds.Width;
                Height = screen.Bounds.Height;
                MoveWindow(hwnd, screen.Bounds.X, screen.Bounds.Y, screen.Bounds.Width, screen.Bounds.Height, true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set window styles: {ex}");
            }
        }

        private void PPTPageFlipPreviewWindow_Closed(object sender, EventArgs e)
        {
            if (ActiveInstance == this)
            {
                ActiveInstance = null;
            }
        }

        public void UpdatePreview()
        {
            var ppt = SettingsManager.Settings.PowerPointSettings;

            // 有效值：位置 i 若 UseGlobalSettings=true，则采用全局字段值，否则采用位置自身字段值
            double lsScale = ppt.PPTLSUseGlobalSettings ? ppt.PPTNavBarScale : ppt.PPTLSButtonScale;
            double rsScale = ppt.PPTRSUseGlobalSettings ? ppt.PPTNavBarScale : ppt.PPTRSButtonScale;
            double lbScale = ppt.PPTLBUseGlobalSettings ? ppt.PPTNavBarScale : ppt.PPTLBButtonScale;
            double rbScale = ppt.PPTRBUseGlobalSettings ? ppt.PPTNavBarScale : ppt.PPTRBButtonScale;

            int lsOffset = ppt.PPTLSUseGlobalSettings ? ppt.PPTGlobalSideButtonPosition : ppt.PPTLSButtonPosition;
            int rsOffset = ppt.PPTRSUseGlobalSettings ? ppt.PPTGlobalSideButtonPosition : ppt.PPTRSButtonPosition;
            int lbOffset = ppt.PPTLBUseGlobalSettings ? ppt.PPTGlobalBottomButtonPosition : ppt.PPTLBButtonPosition;
            int rbOffset = ppt.PPTRBUseGlobalSettings ? ppt.PPTGlobalBottomButtonPosition : ppt.PPTRBButtonPosition;

            double lsOpacity = ppt.PPTLSUseGlobalSettings ? ppt.PPTGlobalButtonOpacity : ppt.PPTLSButtonOpacity;
            double rsOpacity = ppt.PPTRSUseGlobalSettings ? ppt.PPTGlobalButtonOpacity : ppt.PPTRSButtonOpacity;
            double lbOpacity = ppt.PPTLBUseGlobalSettings ? ppt.PPTGlobalButtonOpacity : ppt.PPTLBButtonOpacity;
            double rbOpacity = ppt.PPTRBUseGlobalSettings ? ppt.PPTGlobalButtonOpacity : ppt.PPTRBButtonOpacity;

            bool lsShowPage = ppt.PPTLSUseGlobalSettings ? ppt.PPTGlobalShowPageNumber : ppt.PPTLSShowPageNumber;
            bool rsShowPage = ppt.PPTRSUseGlobalSettings ? ppt.PPTGlobalShowPageNumber : ppt.PPTRSShowPageNumber;
            bool lbShowPage = ppt.PPTLBUseGlobalSettings ? ppt.PPTGlobalShowPageNumber : ppt.PPTLBShowPageNumber;
            bool rbShowPage = ppt.PPTRBUseGlobalSettings ? ppt.PPTGlobalShowPageNumber : ppt.PPTRBShowPageNumber;

            bool lsBlackBg = ppt.PPTLSUseGlobalSettings ? ppt.PPTGlobalBlackBackground : ppt.PPTLSBlackBackground;
            bool rsBlackBg = ppt.PPTRSUseGlobalSettings ? ppt.PPTGlobalBlackBackground : ppt.PPTRSBlackBackground;
            bool lbBlackBg = ppt.PPTLBUseGlobalSettings ? ppt.PPTGlobalBlackBackground : ppt.PPTLBBlackBackground;
            bool rbBlackBg = ppt.PPTRBUseGlobalSettings ? ppt.PPTGlobalBlackBackground : ppt.PPTRBBlackBackground;

            // 1. Update scale for all 4 bars
            LeftSidePanelForPPTNavigation.SetBarScale(lsScale);
            RightSidePanelForPPTNavigation.SetBarScale(rsScale);
            LeftBottomPanelForPPTNavigation.SetBarScale(lbScale);
            RightBottomPanelForPPTNavigation.SetBarScale(rbScale);

            // 2. Set margins (offsets)
            LeftSidePanelForPPTNavigation.Margin = new Thickness(6, 0, 0, lsOffset * 2);
            RightSidePanelForPPTNavigation.Margin = new Thickness(0, 0, 6, rsOffset * 2);
            LeftBottomPanelForPPTNavigation.Margin = new Thickness(6 + lbOffset, 0, 0, 6);
            RightBottomPanelForPPTNavigation.Margin = new Thickness(0, 0, 6 + rbOffset, 6);

            // 3. Set enabled/disabled visibility (UseGlobalSettings 的位由 PPTGlobalButtonEnabled 决定)
            string displayOption = ppt.PPTButtonsDisplayOption.ToString("D4");
            if (displayOption.Length < 4) displayOption = "2222";
            char[] c = displayOption.ToCharArray();
            // LeftBottom = [0], RightBottom = [1], LeftSide = [2], RightSide = [3]
            if (ppt.PPTLBUseGlobalSettings) c[0] = ppt.PPTGlobalButtonEnabled ? '2' : '1';
            if (ppt.PPTRBUseGlobalSettings) c[1] = ppt.PPTGlobalButtonEnabled ? '2' : '1';
            if (ppt.PPTLSUseGlobalSettings) c[2] = ppt.PPTGlobalButtonEnabled ? '2' : '1';
            if (ppt.PPTRSUseGlobalSettings) c[3] = ppt.PPTGlobalButtonEnabled ? '2' : '1';
            LeftBottomPanelForPPTNavigation.Visibility = c[0] == '2' ? Visibility.Visible : Visibility.Collapsed;
            RightBottomPanelForPPTNavigation.Visibility = c[1] == '2' ? Visibility.Visible : Visibility.Collapsed;
            LeftSidePanelForPPTNavigation.Visibility = c[2] == '2' ? Visibility.Visible : Visibility.Collapsed;
            RightSidePanelForPPTNavigation.Visibility = c[3] == '2' ? Visibility.Visible : Visibility.Collapsed;

            // 4. Set page button visibility (Show Page Number)
            LeftSidePanelForPPTNavigation.SetPageButtonVisibility(lsShowPage ? Visibility.Visible : Visibility.Collapsed);
            RightSidePanelForPPTNavigation.SetPageButtonVisibility(rsShowPage ? Visibility.Visible : Visibility.Collapsed);
            LeftBottomPanelForPPTNavigation.SetPageButtonVisibility(lbShowPage ? Visibility.Visible : Visibility.Collapsed);
            RightBottomPanelForPPTNavigation.SetPageButtonVisibility(rbShowPage ? Visibility.Visible : Visibility.Collapsed);

            // 5. Set opacity
            LeftSidePanelForPPTNavigation.SetBarOpacity(lsOpacity);
            RightSidePanelForPPTNavigation.SetBarOpacity(rsOpacity);
            LeftBottomPanelForPPTNavigation.SetBarOpacity(lbOpacity);
            RightBottomPanelForPPTNavigation.SetBarOpacity(rbOpacity);

            // 6. Set theme (Black Background)
            LeftSidePanelForPPTNavigation.ApplyTheme(lsBlackBg);
            RightSidePanelForPPTNavigation.ApplyTheme(rsBlackBg);
            LeftBottomPanelForPPTNavigation.ApplyTheme(lbBlackBg);
            RightBottomPanelForPPTNavigation.ApplyTheme(rbBlackBg);
        }
    }
}
