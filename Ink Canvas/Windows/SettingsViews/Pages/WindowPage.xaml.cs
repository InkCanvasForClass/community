using Ink_Canvas.Helpers;
using Ink_Canvas.Properties;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class WindowPage : iNKORE.UI.WPF.Modern.Controls.Page
    {
        private bool _isLoaded = false;
        private bool _isAdmin = false;
        private bool _hasUIAccess = false;
        private bool CanConfigureUIAccessTopMost => true;
        private RadioButton _radioNormal;
        private RadioButton _radioUIA;
        private readonly ObservableCollection<object> _topMostModeItems = new();

        public WindowPage()
        {
            InitializeComponent();
            Loaded += WindowPage_Loaded;
        }

        private void WindowPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
            _isLoaded = true;
        }

        private void LoadSettings()
        {
            _isLoaded = false;
            _isAdmin = AppRestartHelper.IsRunningAsAdmin();
            _hasUIAccess = UIAccessHelper.HasUIAccess();

            try
            {
                var settings = SettingsManager.Settings;
                if (settings.Advanced != null)
                {
                    CardNoFocusMode.IsOn = settings.Advanced.IsNoFocusMode;
                    CardWindowMode.IsOn = settings.Advanced.WindowMode;
                    CardWindowChromeRendering.IsOn = settings.Startup?.EnableWindowChromeRendering ?? false;
                    CardAvoidFullScreen.IsOn = settings.Advanced.IsEnableAvoidFullScreenHelper;
                    CardMultiScreenSupport.IsOn = settings.Advanced.EnableMultiScreenSupport;
                    CardFollowMouseScreen.IsOn = settings.Advanced.FollowMouseForScreenSelection;
                    ToggleSwitchAlwaysOnTop.IsOn = settings.Advanced.IsAlwaysOnTop;

                    _topMostModeItems.Clear();
                    _topMostModeItems.Add(new TopMostModeSelectionItem());

                    var btnItem = new TopMostModeButtonItem
                    {
                        ButtonHeader = _hasUIAccess && !_isAdmin
                            ? StartupStrings.TopMostMode_CurrentUIAccessNormal
                            : StartupStrings.TopMostMode_RestartAsNormal,
                        ButtonContent = StartupStrings.TopMostMode_RestartAsNormal,
                        RestartAsAdmin = false
                    };
                    _topMostModeItems.Add(btnItem);

                    ExpanderAlwaysOnTop.ItemsSource = _topMostModeItems;

                    // 初始化 UIA 方案下拉框
                    if (ComboBoxUIAMode != null)
                    {
                        ComboBoxUIAMode.Items.Clear();
                        ComboBoxUIAMode.Items.Add(new ComboBoxItem { Content = StartupStrings.UIAMode_UserToken, Tag = UIAMode.UserToken });
                        ComboBoxUIAMode.Items.Add(new ComboBoxItem { Content = StartupStrings.UIAMode_ProcessToken, Tag = UIAMode.ProcessToken });
                        ComboBoxUIAMode.SelectedIndex = settings.Advanced.UIAMode == UIAMode.ProcessToken ? 1 : 0;
                    }

                    UpdateUIAModeVisibility();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载窗口设置时出错: {ex.Message}");
            }

            _isLoaded = true;
        }

        private void UpdateRadioButtons()
        {
            if (_radioNormal == null || _radioUIA == null) return;

            bool wasLoaded = _isLoaded;
            _isLoaded = false;

            _radioNormal.IsEnabled = CanConfigureUIAccessTopMost;
            _radioUIA.IsEnabled = CanConfigureUIAccessTopMost;

            if (CanConfigureUIAccessTopMost && SettingsManager.Settings.Advanced.EnableUIAccessTopMost)
                _radioUIA.IsChecked = true;
            else
                _radioNormal.IsChecked = true;

            UpdateUIAModeVisibility();

            _isLoaded = wasLoaded;
        }

        private void UpdateUIAModeVisibility()
        {
            if (CardUIAMode == null) return;

            bool isUIAEnabled = SettingsManager.Settings.Advanced.EnableUIAccessTopMost;
            CardUIAMode.Visibility = isUIAEnabled ? Visibility.Visible : Visibility.Collapsed;

            if (isUIAEnabled && ComboBoxUIAMode != null)
            {
                var currentMode = SettingsManager.Settings.Advanced.UIAMode;
                ComboBoxUIAMode.SelectedIndex = currentMode == UIAMode.ProcessToken ? 1 : 0;
            }
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

        private void ToggleSwitchAvoidFullScreen_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            try
            {
                bool newState = CardAvoidFullScreen.IsOn;
                SettingsManager.Settings.Advanced.IsEnableAvoidFullScreenHelper = newState;
                SettingsManager.SaveSettingsToFile();

                var window = Application.Current.MainWindow;
                if (window != null)
                {
                    if (newState)
                    {
                        AvoidFullScreenHelper.StartAvoidFullScreen(window);
                    }
                    else
                    {
                        AvoidFullScreenHelper.StopAvoidFullScreen(window);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置避免全屏时出错: {ex.Message}");
            }
        }

        private void ToggleSwitchWindowChromeRendering_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            try
            {
                bool newState = CardWindowChromeRendering.IsOn;
                if (SettingsManager.Settings.Startup == null)
                    SettingsManager.Settings.Startup = new Startup();

                SettingsManager.Settings.Startup.EnableWindowChromeRendering = newState;
                SettingsManager.SaveSettingsToFile();

                var msg = WindowStrings.Window_WindowChromeRendering_RestartRequired;
                var result = MessageBox.Show(msg, "Ink Canvas", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    AppRestartHelper.RestartWithCurrentPrivileges();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置 WindowChrome 渲染时出错: {ex.Message}");
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

        private void ToggleSwitchMultiScreenSupport_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            try
            {
                bool newState = CardMultiScreenSupport.IsOn;
                SettingsManager.Settings.Advanced.EnableMultiScreenSupport = newState;
                SettingsManager.SaveSettingsToFile();

                if (Application.Current.MainWindow is MainWindow mainWindow)
                {
                    mainWindow.ApplyMultiScreenSettings();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置多屏支持时出错: {ex.Message}");
            }
        }

        private void ToggleSwitchFollowMouseScreen_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            try
            {
                bool newState = CardFollowMouseScreen.IsOn;
                SettingsManager.Settings.Advanced.FollowMouseForScreenSelection = newState;
                SettingsManager.SaveSettingsToFile();

                if (Application.Current.MainWindow is MainWindow mainWindow)
                {
                    mainWindow.ApplyMultiScreenSettings();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置自动跟随鼠标选择显示屏时出错: {ex.Message}");
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

                UpdateUIAModeVisibility();

                var msg = StartupStrings.TopMostMode_Normal_RestartRequired;
                var result = MessageBox.Show(msg, "Ink Canvas", MessageBoxButton.YesNo, MessageBoxImage.Question);

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

                UpdateUIAModeVisibility();

                var msg = StartupStrings.TopMostMode_UIA_RestartRequired;
                var result = MessageBox.Show(msg, "Ink Canvas", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    App.IsUIAccessTopMostEnabled = true;
                    AppRestartHelper.SwitchToUIATopMostAndRestart();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置UIA置顶模式时出错: {ex.Message}");
            }
        }

        private void ComboBoxUIAMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            if (ComboBoxUIAMode?.SelectedItem is not ComboBoxItem item) return;

            try
            {
                var newMode = (UIAMode)item.Tag;
                if (SettingsManager.Settings.Advanced.UIAMode == newMode) return;

                SettingsManager.Settings.Advanced.UIAMode = newMode;
                SettingsManager.SaveSettingsToFile();

                // 切换方案需要重启才能生效
                var msg = StartupStrings.TopMostMode_UIA_RestartRequired;
                var result = MessageBox.Show(msg, "Ink Canvas", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    App.IsUIAccessTopMostEnabled = true;
                    AppRestartHelper.SwitchToUIATopMostAndRestart();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置UIA方案时出错: {ex.Message}");
            }
        }

        private void BtnRestart_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is bool asAdmin)
            {
                AppRestartHelper.RestartApp(asAdmin);
            }
        }
    }
}
