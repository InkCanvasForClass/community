using Ink_Canvas.Helpers;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

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

                CardExternalProtocol.IsOn = settings.Advanced.IsEnableUriScheme;

                if (settings.Startup != null)
                {
                    CardEnableNibMode.IsOn = settings.Startup.IsEnableNibMode;

                    ComboBoxCrashAction.SelectedIndex = settings.Startup.CrashAction;
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

        private void ToggleSwitchExternalProtocol_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            try
            {
                bool newState = CardExternalProtocol.IsOn;
                bool success = false;

                if (newState)
                {
                    if (!UriSchemeHelper.IsUriSchemeRegistered())
                    {
                        success = UriSchemeHelper.RegisterUriScheme();
                    }
                    else
                    {
                        success = true;
                    }
                }
                else
                {
                    if (UriSchemeHelper.IsUriSchemeRegistered())
                    {
                        success = UriSchemeHelper.UnregisterUriScheme();
                    }
                    else
                    {
                        success = true;
                    }
                }

                if (success)
                {
                    SettingsManager.Settings.Advanced.IsEnableUriScheme = newState;
                    SettingsManager.SaveSettingsToFile();
                }
                else
                {
                    _isLoaded = false;
                    CardExternalProtocol.IsOn = !newState;
                    _isLoaded = true;

                    LogHelper.WriteLogToFile("设置外部协议失败，请检查权限或日志", LogHelper.LogType.Error);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置外部协议时出错: {ex.Message}");
            }
        }

        #endregion

        #region 笔尖模式事件处理

        private void ToggleSwitchEnableNibMode_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            try
            {
                bool newState = CardEnableNibMode.IsOn;
                SettingsManager.Settings.Startup.IsEnableNibMode = newState;

                var window = Application.Current.MainWindow;
                if (window is MainWindow mw)
                {
                    if (mw.ToggleSwitchEnableNibMode != null)
                        mw.ToggleSwitchEnableNibMode.IsOn = newState;
                    if (mw.BoardToggleSwitchEnableNibMode != null)
                        mw.BoardToggleSwitchEnableNibMode.IsOn = newState;

                    if (newState)
                        mw.BoundsWidth = SettingsManager.Settings.Advanced.NibModeBoundsWidth;
                    else
                        mw.BoundsWidth = SettingsManager.Settings.Advanced.FingerModeBoundsWidth;
                }

                SettingsManager.SaveSettingsToFile();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置笔尖模式时出错: {ex.Message}");
            }
        }

        #endregion

        #region 崩溃后操作事件处理

        private void ComboBoxCrashAction_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;

            try
            {
                var item = ComboBoxCrashAction?.SelectedItem as ComboBoxItem;
                if (item == null) return;
                var tag = item.Tag?.ToString() ?? "0";
                switch (tag)
                {
                    case "0":
                        App.CrashAction = App.CrashActionType.SilentRestart;
                        SettingsManager.Settings.Startup.CrashAction = 0;
                        break;
                    case "1":
                        App.CrashAction = App.CrashActionType.NoAction;
                        SettingsManager.Settings.Startup.CrashAction = 1;
                        break;
                }
                SettingsManager.SaveSettingsToFile();
                App.SyncCrashActionFromSettings();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置崩溃操作时出错: {ex.Message}");
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
