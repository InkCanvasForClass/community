using Ink_Canvas.Helpers;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class StartupPage : iNKORE.UI.WPF.Modern.Controls.Page
    {
        private bool _isLoaded = false;
        private bool _isAdmin = false;
        private RadioButton _radioNormal;
        private RadioButton _radioUIA;
        private readonly ObservableCollection<object> _topMostModeItems = new();

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
            _isAdmin = AppRestartHelper.IsRunningAsAdmin();

            try
            {
                var settings = SettingsManager.Settings;
                if (settings.Advanced != null)
                {
                    CardNoFocusMode.IsOn = settings.Advanced.IsNoFocusMode;
                    CardWindowMode.IsOn = settings.Advanced.WindowMode;
                    ToggleSwitchAlwaysOnTop.IsOn = settings.Advanced.IsAlwaysOnTop;

                    _topMostModeItems.Clear();
                    _topMostModeItems.Add(new TopMostModeSelectionItem());

                    var btnItem = _isAdmin
                        ? new TopMostModeButtonItem
                        {
                            ButtonHeader = Properties.Strings.GetString("Startup_TopMostMode_RestartAsNormal"),
                            ButtonContent = Properties.Strings.GetString("Startup_TopMostMode_RestartAsNormal"),
                            RestartAsAdmin = false
                        }
                        : new TopMostModeButtonItem
                        {
                            ButtonHeader = Properties.Strings.GetString("Startup_TopMostMode_RestartAsAdmin"),
                            ButtonContent = Properties.Strings.GetString("Startup_TopMostMode_RestartAsAdmin"),
                            RestartAsAdmin = true
                        };
                    _topMostModeItems.Add(btnItem);

                    ExpanderAlwaysOnTop.ItemsSource = _topMostModeItems;
                }

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

        private void UpdateRadioButtons()
        {
            if (_radioNormal == null || _radioUIA == null) return;

            bool wasLoaded = _isLoaded;
            _isLoaded = false;

            _radioNormal.IsEnabled = _isAdmin;
            _radioUIA.IsEnabled = _isAdmin;

            if (_isAdmin && SettingsManager.Settings.Advanced.EnableUIAccessTopMost)
                _radioUIA.IsChecked = true;
            else
                _radioNormal.IsChecked = true;

            _isLoaded = wasLoaded;
        }

        private void RadioTopMostNormal_Loaded(object sender, RoutedEventArgs e)
        {
            _radioNormal = sender as RadioButton;
            UpdateRadioButtons();
        }

        private void RadioTopMostUIA_Loaded(object sender, RoutedEventArgs e)
        {
            _radioUIA = sender as RadioButton;
            UpdateRadioButtons();
        }

        #region 窗口设置事件处理

        private void ToggleSwitchNoFocusMode_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            try
            {
                bool newState = CardNoFocusMode.IsOn;

                SettingsManager.Settings.Advanced.IsNoFocusMode = newState;
                SettingsManager.SaveSettingsToFile();

                var window = Application.Current.MainWindow;
                if (window != null)
                {
                    WindowSettingsHelper.ApplyNoFocusMode(window);

                    if (SettingsManager.Settings.Advanced.IsAlwaysOnTop)
                    {
                        WindowSettingsHelper.ApplyAlwaysOnTop(window);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置窗口无焦点模式时出错: {ex.Message}");
            }
        }

        private void ToggleSwitchWindowMode_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            try
            {
                bool newState = CardWindowMode.IsOn;

                SettingsManager.Settings.Advanced.WindowMode = newState;
                SettingsManager.SaveSettingsToFile();

                var window = Application.Current.MainWindow;
                if (window != null)
                {
                    WindowSettingsHelper.SetWindowMode(window);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置窗口无边框模式时出错: {ex.Message}");
            }
        }

        private void ToggleSwitchAlwaysOnTop_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            try
            {
                bool newState = ToggleSwitchAlwaysOnTop.IsOn;

                SettingsManager.Settings.Advanced.IsAlwaysOnTop = newState;
                SettingsManager.SaveSettingsToFile();

                var window = Application.Current.MainWindow;
                if (window != null)
                {
                    WindowSettingsHelper.ApplyAlwaysOnTop(window);

                    if (!newState && SettingsManager.Settings.Advanced.EnableUIAccessTopMost)
                    {
                        SettingsManager.Settings.Advanced.EnableUIAccessTopMost = false;
                        App.IsUIAccessTopMostEnabled = false;
                        WindowSettingsHelper.ApplyUIAccessTopMost(window);
                        SettingsManager.SaveSettingsToFile();
                        if (_radioNormal != null) _radioNormal.IsChecked = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置窗口置顶时出错: {ex.Message}");
            }
        }

        private void RadioTopMostNormal_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            try
            {
                SettingsManager.Settings.Advanced.EnableUIAccessTopMost = false;
                SettingsManager.SaveSettingsToFile();

                App.IsUIAccessTopMostEnabled = false;

                var msg = Properties.Strings.GetString("Startup_TopMostMode_Normal_RestartRequired");
                var result = System.Windows.MessageBox.Show(msg, "Ink Canvas", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    AppRestartHelper.RestartWithCurrentPrivileges();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置普通置顶模式时出错: {ex.Message}");
            }
        }

        private void RadioTopMostUIA_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            try
            {
                SettingsManager.Settings.Advanced.EnableUIAccessTopMost = true;

                if (!SettingsManager.Settings.Advanced.IsAlwaysOnTop)
                {
                    SettingsManager.Settings.Advanced.IsAlwaysOnTop = true;
                    ToggleSwitchAlwaysOnTop.IsOn = true;
                }

                SettingsManager.SaveSettingsToFile();

                var msg = Properties.Strings.GetString("Startup_TopMostMode_UIA_RestartRequired");
                var result = System.Windows.MessageBox.Show(msg, "Ink Canvas", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    App.IsUIAccessTopMostEnabled = true;
                    AppRestartHelper.RestartAsAdmin();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置UIA置顶模式时出错: {ex.Message}");
            }
        }

        private void BtnRestart_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is bool asAdmin)
            {
                AppRestartHelper.RestartApp(asAdmin);
            }
        }

        #endregion

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
