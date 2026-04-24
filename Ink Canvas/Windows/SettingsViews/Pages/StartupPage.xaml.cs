using Ink_Canvas.Helpers;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Diagnostics;
using System.Windows;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class StartupPage : iNKORE.UI.WPF.Modern.Controls.Page
    {
        private bool _isLoaded = false;

        public StartupPage()
        {
            InitializeComponent();
            Loaded += StartupPage_Loaded;
        }

        private void StartupPage_Loaded(object sender, RoutedEventArgs e)
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

                bool runAtStartup = AutoStartHelper.IsAutoStartEnabled("Ink Canvas Annotation");
                CardRunAtStartup.IsOn = runAtStartup;

                if (settings.Startup != null)
                {
                    CardFoldAtStartup.IsOn = settings.Startup.IsFoldAtStartup;
                }

                if (settings.ModeSettings != null)
                {
                    CardPPTOnlyMode.IsOn = settings.ModeSettings.IsPPTOnlyMode;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载启动设置时出错: {ex.Message}");
            }

            _isLoaded = true;
        }

        #region 启动设置事件处理

        private void ToggleSwitchRunAtStartup_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            try
            {
                bool newState = CardRunAtStartup.IsOn;

                if (newState)
                {
                    AutoStartHelper.StartAutomaticallyDel("InkCanvas");
                    AutoStartHelper.StartAutomaticallyCreate("Ink Canvas Annotation");
                }
                else
                {
                    AutoStartHelper.StartAutomaticallyDel("InkCanvas");
                    AutoStartHelper.StartAutomaticallyDel("Ink Canvas Annotation");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置开机启动时出错: {ex.Message}");
            }
        }

        private void ToggleSwitchFoldAtStartup_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            try
            {
                bool newState = CardFoldAtStartup.IsOn;

                SettingsManager.Settings.Startup.IsFoldAtStartup = newState;
                SettingsManager.SaveSettingsToFile();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置开机折叠时出错: {ex.Message}");
            }
        }

        #endregion

        #region 模式设置事件处理

        private void ToggleSwitchPPTOnlyMode_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            try
            {
                bool newState = CardPPTOnlyMode.IsOn;

                var window = Application.Current.MainWindow;
                if (window != null)
                {
                    WindowSettingsHelper.ApplyPptOnlyMode(window, newState);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置仅PPT模式时出错: {ex.Message}");
            }
        }

        #endregion
    }
}
