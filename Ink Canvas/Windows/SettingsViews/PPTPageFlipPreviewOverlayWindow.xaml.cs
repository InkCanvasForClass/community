using Ink_Canvas.Helpers;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Ink_Canvas.Windows.SettingsViews
{
    /// <summary>
    /// 全屏预览的顶层浮层窗口：仅承载 4 个翻页按钮控件，浮在 SettingsWindow 之上。
    /// 点击穿透（WS_EX_TRANSPARENT）+ 不抢焦点（WS_EX_NOACTIVATE），不影响设置窗口操作。
    /// 不注册到 WindowTopmostManager（SettingsWindow 激活会超越其 ZOrder），
    /// 改用 DispatcherTimer 周期性 SetWindowPos 确保始终位于 SettingsWindow 之上但低于 Popup。
    /// </summary>
    public partial class PPTPageFlipPreviewOverlayWindow : Window
    {
        public static PPTPageFlipPreviewOverlayWindow ActiveInstance { get; private set; }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        private readonly Window _settingsWindow;
        private DispatcherTimer _zOrderTimer;

        //[DllImport("user32.dll")]
        //private static extern int GetWindowLong(IntPtr hwnd, int index);

        //[DllImport("user32.dll")]
        //private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        //[DllImport("user32.dll")]
        //private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        public PPTPageFlipPreviewOverlayWindow(Window settingsWindow)
        {
            InitializeComponent();
            ActiveInstance = this;
            _settingsWindow = settingsWindow;

            // Set dummy page numbers for preview bars
            LeftSidePanelForPPTNavigation.CurrentSlide = 2;
            LeftSidePanelForPPTNavigation.TotalSlides = 5;
            RightSidePanelForPPTNavigation.CurrentSlide = 2;
            RightSidePanelForPPTNavigation.TotalSlides = 5;
            LeftBottomPanelForPPTNavigation.CurrentSlide = 2;
            LeftBottomPanelForPPTNavigation.TotalSlides = 5;
            RightBottomPanelForPPTNavigation.CurrentSlide = 2;
            RightBottomPanelForPPTNavigation.TotalSlides = 5;

            Closed += PPTPageFlipPreviewOverlayWindow_Closed;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                int extendedStyle = PInvoke.GetWindowLong(new HWND(hwnd), WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
                PInvoke.SetWindowLong(new HWND(hwnd), WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE);

                // 精确定位到主屏幕整个边界（与 MainWindow / PreviewWindow 一致）
                var screen = System.Windows.Forms.Screen.PrimaryScreen;
                Left = screen.Bounds.X;
                Top = screen.Bounds.Y;
                Width = screen.Bounds.Width;
                Height = screen.Bounds.Height;
                PInvoke.MoveWindow(new HWND(hwnd), screen.Bounds.X, screen.Bounds.Y, screen.Bounds.Width, screen.Bounds.Height, true);

                // 启动 Z 序维持定时器：周期性将 Overlay 置于 SettingsWindow 之上。
                // WindowTopmostManager 重排 SettingsWindow（SetTopmost）会把它提到 TOPMOST 顶，
                // 盖住 Overlay；此处用 SetWindowPos(overlay, settingsHWND) 把 Overlay 重新提到
                // SettingsWindow 之上，但低于被 BoostPopupWindowsAboveChildren 提升的 Popup。
                StartZOrderTimer();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set window styles: {ex}");
            }
        }

        private void StartZOrderTimer()
        {
            if (_zOrderTimer != null) return;

            _zOrderTimer = new DispatcherTimer(DispatcherPriority.Send)
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _zOrderTimer.Tick += (s, e) => MaintainZOrderAboveSettings();
            _zOrderTimer.Start();
        }

        private void MaintainZOrderAboveSettings()
        {
            var overlayHandle = new WindowInteropHelper(this).Handle;
            if (overlayHandle == IntPtr.Zero) return;
            if (!NativeWindowHelper.IsWindowReady(overlayHandle)) return;

            // 用 HWND_TOPMOST 绝对置顶 Overlay 到 TOPMOST 队列最顶。
            // SettingsWindow 的 WPF Topmost=true 会在激活/失焦等事件时自动把它重新提到 TOPMOST 顶，
            // 相对位置插入（SetWindowPos(overlay, settingsHWND)）会被它盖住；
            // 改用 HWND_TOPMOST 绝对置顶 + 100ms 高频刷新，确保 Overlay 始终压在 SettingsWindow 之上。
            // Overlay 为 WS_EX_TRANSPARENT 点击穿透，不会阻挡 SettingsWindow 的鼠标操作，
            // 且 4 个按钮位于屏幕边缘，对设置区中央的 Popup 影响极小。
            PInvoke.SetWindowPos(new HWND(overlayHandle), new HWND(NativeWindowHelper.HWND_TOPMOST), 0, 0, 0, 0,
                SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER);
        }

        private void PPTPageFlipPreviewOverlayWindow_Closed(object sender, EventArgs e)
        {
            _zOrderTimer?.Stop();
            _zOrderTimer = null;

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
