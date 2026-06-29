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
            
            // 1. Update scale for all 4 bars
            double scale = ppt.PPTNavBarScale;
            LeftSidePanelForPPTNavigation.SetBarScale(scale);
            RightSidePanelForPPTNavigation.SetBarScale(scale);
            LeftBottomPanelForPPTNavigation.SetBarScale(scale);
            RightBottomPanelForPPTNavigation.SetBarScale(scale);

            // 2. Set margins (offsets)
            LeftSidePanelForPPTNavigation.Margin = new Thickness(6, 0, 0, ppt.PPTLSButtonPosition * 2);
            RightSidePanelForPPTNavigation.Margin = new Thickness(0, 0, 6, ppt.PPTRSButtonPosition * 2);
            LeftBottomPanelForPPTNavigation.Margin = new Thickness(6 + ppt.PPTLBButtonPosition, 0, 0, 6);
            RightBottomPanelForPPTNavigation.Margin = new Thickness(0, 0, 6 + ppt.PPTRBButtonPosition, 6);

            // 3. Set enabled/disabled visibility
            string displayOption = ppt.PPTButtonsDisplayOption.ToString("D4");
            if (displayOption.Length >= 4)
            {
                // LeftBottom = [0], RightBottom = [1], LeftSide = [2], RightSide = [3]
                LeftBottomPanelForPPTNavigation.Visibility = displayOption[0] == '2' ? Visibility.Visible : Visibility.Collapsed;
                RightBottomPanelForPPTNavigation.Visibility = displayOption[1] == '2' ? Visibility.Visible : Visibility.Collapsed;
                LeftSidePanelForPPTNavigation.Visibility = displayOption[2] == '2' ? Visibility.Visible : Visibility.Collapsed;
                RightSidePanelForPPTNavigation.Visibility = displayOption[3] == '2' ? Visibility.Visible : Visibility.Collapsed;
            }

            // 4. Set page button visibility (Show Page Number)
            LeftSidePanelForPPTNavigation.SetPageButtonVisibility(ppt.PPTLSShowPageNumber ? Visibility.Visible : Visibility.Collapsed);
            RightSidePanelForPPTNavigation.SetPageButtonVisibility(ppt.PPTRSShowPageNumber ? Visibility.Visible : Visibility.Collapsed);
            LeftBottomPanelForPPTNavigation.SetPageButtonVisibility(ppt.PPTLBShowPageNumber ? Visibility.Visible : Visibility.Collapsed);
            RightBottomPanelForPPTNavigation.SetPageButtonVisibility(ppt.PPTRBShowPageNumber ? Visibility.Visible : Visibility.Collapsed);

            // 5. Set opacity
            LeftSidePanelForPPTNavigation.SetBarOpacity(ppt.PPTLSButtonOpacity);
            RightSidePanelForPPTNavigation.SetBarOpacity(ppt.PPTRSButtonOpacity);
            LeftBottomPanelForPPTNavigation.SetBarOpacity(ppt.PPTLBButtonOpacity);
            RightBottomPanelForPPTNavigation.SetBarOpacity(ppt.PPTRBButtonOpacity);

            // 6. Set theme (Black Background)
            LeftSidePanelForPPTNavigation.ApplyTheme(ppt.PPTLSBlackBackground);
            RightSidePanelForPPTNavigation.ApplyTheme(ppt.PPTRSBlackBackground);
            LeftBottomPanelForPPTNavigation.ApplyTheme(ppt.PPTLBBlackBackground);
            RightBottomPanelForPPTNavigation.ApplyTheme(ppt.PPTRBBlackBackground);
        }
    }
}
