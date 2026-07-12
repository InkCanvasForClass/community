using Ink_Canvas.Helpers;
using Ink_Canvas.Properties;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

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
                ToggleSwitchRunAtStartup.IsOn = runAtStartup;

                ToggleSwitchExternalProtocol.IsOn = settings.Advanced.IsEnableUriScheme;

                if (settings.Startup != null)
                {
                    int crashAction = settings.Startup.CrashAction;
                    if (crashAction < 0 || crashAction > 2) crashAction = 0;
                    ComboBoxCrashAction.SelectedIndex = crashAction;

                    ToggleSwitchFoldAtStartup.IsOn = settings.Startup.IsFoldAtStartup;
                    ToggleSwitchFastStartup.IsOn = settings.Startup.EnableFastStartup;
                }

                ToggleSwitchEnableTrayIcon.IsOn = settings.Appearance.EnableTrayIcon;
                ComboBoxTrayLeftClickAction.SelectedIndex = (int)settings.Appearance.TrayLeftClickAction;
                ComboBoxTrayRightClickAction.SelectedIndex = (int)settings.Appearance.TrayRightClickAction;

                ToggleSwitchEnableSplashScreen.IsOn = settings.Appearance.EnableSplashScreen;
                ComboBoxSplashScreenStyle.SelectedIndex = settings.Appearance.SplashScreenStyle;
                UpdateCustomSplashImageVisibility();

                if (!string.IsNullOrEmpty(settings.Appearance.CustomSplashImagePath) &&
                    System.IO.File.Exists(settings.Appearance.CustomSplashImagePath))
                {
                    TextBlockCustomSplashPath.Text = System.IO.Path.GetFileName(settings.Appearance.CustomSplashImagePath);
                    TextBlockCustomSplashPath.ToolTip = settings.Appearance.CustomSplashImagePath;
                }
                else
                {
                    TextBlockCustomSplashPath.Text = ThemeStrings.Theme_CustomSplash_NotSelected;
                    TextBlockCustomSplashPath.ToolTip = null;
                }

                UpdateTextAlignButtonAppearance(settings.Appearance.CustomSplashTextPosition);
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
                bool newState = ToggleSwitchRunAtStartup.IsOn;

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
                SettingsManager.Settings.Startup.IsFoldAtStartup = ToggleSwitchFoldAtStartup.IsOn;
                SettingsManager.SaveSettingsToFile();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置启动时收纳出错: {ex.Message}");
            }
        }

        private void ToggleSwitchFastStartup_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            try
            {
                SettingsManager.Settings.Startup.EnableFastStartup = ToggleSwitchFastStartup.IsOn;
                SettingsManager.SaveSettingsToFile();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置快速启动模式时出错: {ex.Message}");
            }
        }

        private void ToggleSwitchExternalProtocol_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            try
            {
                bool newState = ToggleSwitchExternalProtocol.IsOn;
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
                    ToggleSwitchExternalProtocol.IsOn = !newState;
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
                    case "2":
                        App.CrashAction = App.CrashActionType.ShowCrashWindow;
                        SettingsManager.Settings.Startup.CrashAction = 2;
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

        #region 托盘图标

        private void ToggleSwitchEnableTrayIcon_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Appearance.EnableTrayIcon = ToggleSwitchEnableTrayIcon.IsOn;
            SettingsManager.SaveSettingsToFile();
            try
            {
                var _taskbar = Application.Current.Resources["TaskbarTrayIcon"];
                if (_taskbar is FrameworkElement fe)
                    fe.Visibility = ToggleSwitchEnableTrayIcon.IsOn ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                ExceptionHandler.HandleException(ex, "更新托盘图标可见性失败", LogHelper.LogType.Warning);
            }
        }

        private void ComboBoxTrayLeftClickAction_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Appearance.TrayLeftClickAction = (TrayClickAction)ComboBoxTrayLeftClickAction.SelectedIndex;
            SettingsManager.SaveSettingsToFile();
        }

        private void ComboBoxTrayRightClickAction_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Appearance.TrayRightClickAction = (TrayClickAction)ComboBoxTrayRightClickAction.SelectedIndex;
            SettingsManager.SaveSettingsToFile();
        }

        #endregion

        #region 启动画面

        private void ToggleSwitchEnableSplashScreen_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Appearance.EnableSplashScreen = ToggleSwitchEnableSplashScreen.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ComboBoxSplashScreenStyle_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Appearance.SplashScreenStyle = ComboBoxSplashScreenStyle.SelectedIndex;
            SettingsManager.SaveSettingsToFile();
            UpdateCustomSplashImageVisibility();
        }

        private void UpdateCustomSplashImageVisibility()
        {
            bool isCustomSelected = ComboBoxSplashScreenStyle.SelectedIndex == 7;
            CardCustomSplashImage.Visibility = isCustomSelected ? Visibility.Visible : Visibility.Collapsed;
            CardCustomSplashTextPosition.Visibility = isCustomSelected ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BorderTextAlign_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!_isLoaded) return;

            if (sender is Border border && border.Tag != null)
            {
                int selectedIndex = int.Parse(border.Tag.ToString());
                SettingsManager.Settings.Appearance.CustomSplashTextPosition = selectedIndex;
                SettingsManager.SaveSettingsToFile();
                UpdateTextAlignButtonAppearance(selectedIndex);
            }
        }

        private void UpdateTextAlignButtonAppearance(int selectedIndex)
        {
            AnimateIndicatorToPosition(selectedIndex);
        }

        private void AnimateIndicatorToPosition(int position)
        {
            double targetX = position * 36;

            var animation = new DoubleAnimation
            {
                To = targetX,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            IndicatorTranslateTransform.BeginAnimation(TranslateTransform.XProperty, animation);

            var isDarkTheme = SettingsManager.Settings.Appearance.Theme == 1;

            if (isDarkTheme)
            {
                SelectionIndicator.Background = new SolidColorBrush(Color.FromArgb(40, 0, 120, 215));
                SelectionIndicator.BorderBrush = new SolidColorBrush(Color.FromArgb(150, 0, 120, 215));
            }
            else
            {
                SelectionIndicator.Background = new SolidColorBrush(Color.FromArgb(25, 0, 120, 215));
                SelectionIndicator.BorderBrush = new SolidColorBrush(Color.FromArgb(120, 0, 120, 215));
            }
        }

        private void ButtonBrowseCustomSplash_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp|All Files|*.*",
                    Title = ThemeStrings.Theme_SelectCustomSplashImage
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    string selectedPath = openFileDialog.FileName;
                    if (!string.IsNullOrEmpty(selectedPath))
                    {
                        SettingsManager.Settings.Appearance.CustomSplashImagePath = selectedPath;
                        SettingsManager.SaveSettingsToFile();
                        TextBlockCustomSplashPath.Text = System.IO.Path.GetFileName(selectedPath);
                        TextBlockCustomSplashPath.ToolTip = selectedPath;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"选择自定义启动图片时出错: {ex.Message}");
            }
        }

        private void ButtonClearCustomSplash_Click(object sender, RoutedEventArgs e)
        {
            SettingsManager.Settings.Appearance.CustomSplashImagePath = string.Empty;
            SettingsManager.SaveSettingsToFile();
            TextBlockCustomSplashPath.Text = ThemeStrings.Theme_CustomSplash_NotSelected;
            TextBlockCustomSplashPath.ToolTip = null;
        }

        #endregion
    }
}
