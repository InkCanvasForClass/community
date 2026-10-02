using Ink_Canvas.Helpers;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using OSVersionExtension;
using System;
using System.Diagnostics;
using System.Windows;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class ExperimentalPage : System.Windows.Controls.Page
    {
        private bool _isLoaded = false;

        public ExperimentalPage()
        {
            InitializeComponent();
            Loaded += ExperimentalPage_Loaded;
        }

        private void ExperimentalPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
            _isLoaded = true;
        }

        private void LoadSettings()
        {
            _isLoaded = false;

            try
            {
                var settings = SettingsManager.Settings;
                if (settings.Advanced != null)
                {
                    CardFullScreenHelper.IsChecked = settings.Advanced.IsEnableFullScreenHelper;
                    CardEdgeGestureUtil.IsChecked = settings.Advanced.IsEnableEdgeGestureUtil;
                    CardForceFullScreen.IsChecked = settings.Advanced.IsEnableForceFullScreen;
                    CardDPIChangeDetection.IsChecked = settings.Advanced.IsEnableDPIChangeDetection;
                    CardResolutionChangeDetection.IsChecked = settings.Advanced.IsEnableResolutionChangeDetection;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载实验性选项时出错: {ex.Message}");
            }

            _isLoaded = true;
        }

        private void ToggleSwitchFullScreenHelper_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            try
            {
                SettingsManager.Settings.Advanced.IsEnableFullScreenHelper = ((CardFullScreenHelper.IsChecked) == true);
                SettingsManager.SaveSettingsToFile();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置全屏助手时出错: {ex.Message}");
            }
        }

        private void ToggleSwitchEdgeGestureUtil_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            try
            {
                SettingsManager.Settings.Advanced.IsEnableEdgeGestureUtil = ((CardEdgeGestureUtil.IsChecked) == true);
                SettingsManager.SaveSettingsToFile();

                if (OSVersion.GetOperatingSystem() >= OSVersionExtension.OperatingSystem.Windows10)
                {
                    var window = Application.Current.MainWindow;
                    if (window != null)
                    {
                        var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
                        EdgeGestureUtil.DisableEdgeGestures(handle, ((CardEdgeGestureUtil.IsChecked) == true));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置边缘手势时出错: {ex.Message}");
            }
        }

        private void ToggleSwitchForceFullScreen_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            try
            {
                SettingsManager.Settings.Advanced.IsEnableForceFullScreen = ((CardForceFullScreen.IsChecked) == true);
                SettingsManager.SaveSettingsToFile();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置强制全屏时出错: {ex.Message}");
            }
        }

        private void ToggleSwitchDPIChangeDetection_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            try
            {
                SettingsManager.Settings.Advanced.IsEnableDPIChangeDetection = ((CardDPIChangeDetection.IsChecked) == true);
                SettingsManager.SaveSettingsToFile();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置DPI变化检测时出错: {ex.Message}");
            }
        }

        private void ToggleSwitchResolutionChangeDetection_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            try
            {
                SettingsManager.Settings.Advanced.IsEnableResolutionChangeDetection = ((CardResolutionChangeDetection.IsChecked) == true);
                SettingsManager.SaveSettingsToFile();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置分辨率变化检测时出错: {ex.Message}");
            }
        }
    }
}
