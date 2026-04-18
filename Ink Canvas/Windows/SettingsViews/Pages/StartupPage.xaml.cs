using Ink_Canvas.Controls;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Security.Principal;
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

        private static bool IsRunningAsAdmin()
        {
            try
            {
                var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private void RestartApp(bool asAdmin)
        {
            try
            {
                App.IsAppExitByUser = true;

                (Application.Current as App)?.ReleaseMutexForRestart();

                string exePath = Process.GetCurrentProcess().MainModule.FileName;

                if (asAdmin)
                {
                    var psi = new ProcessStartInfo(exePath) { UseShellExecute = true, Verb = "runas" };
                    Process.Start(psi);
                }
                else
                {
                    Process.Start("explorer.exe", "\"" + exePath + "\"");
                }

                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"重启应用时出错: {ex.Message}");
            }
        }

        private void LoadSettings()
        {
            if (MainWindow.Settings == null) return;

            _isLoaded = false;
            _isAdmin = IsRunningAsAdmin();

            try
            {
                if (MainWindow.Settings.Advanced != null)
                {
                    CardNoFocusMode.IsOn = MainWindow.Settings.Advanced.IsNoFocusMode;
                    CardWindowMode.IsOn = MainWindow.Settings.Advanced.WindowMode;
                    ToggleSwitchAlwaysOnTop.IsOn = MainWindow.Settings.Advanced.IsAlwaysOnTop;

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

                bool runAtStartup = System.IO.File.Exists(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.Startup) + "\\Ink Canvas Annotation.lnk");
                CardRunAtStartup.IsOn = runAtStartup;

                if (MainWindow.Settings.Startup != null)
                {
                    CardFoldAtStartup.IsOn = MainWindow.Settings.Startup.IsFoldAtStartup;
                }

                if (MainWindow.Settings.ModeSettings != null)
                {
                    ToggleSwitchPPTOnlyMode.IsOn = MainWindow.Settings.ModeSettings.IsPPTOnlyMode;
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

            if (_isAdmin && MainWindow.Settings.Advanced.EnableUIAccessTopMost)
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

                if (MainWindow.Settings.Advanced != null)
                {
                    MainWindow.Settings.Advanced.IsNoFocusMode = newState;
                    MainWindow.SaveSettingsToFile();
                }

                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    mainWindow.ApplyNoFocusMode();

                    if (MainWindow.Settings.Advanced.IsAlwaysOnTop)
                    {
                        mainWindow.ApplyAlwaysOnTop();
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

                if (MainWindow.Settings.Advanced != null)
                {
                    MainWindow.Settings.Advanced.WindowMode = newState;
                    MainWindow.SaveSettingsToFile();
                }

                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    mainWindow.SetWindowMode();
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

                if (MainWindow.Settings.Advanced != null)
                {
                    MainWindow.Settings.Advanced.IsAlwaysOnTop = newState;
                    MainWindow.SaveSettingsToFile();
                }

                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    mainWindow.ApplyAlwaysOnTop();

                    if (!newState && MainWindow.Settings.Advanced.EnableUIAccessTopMost)
                    {
                        MainWindow.Settings.Advanced.EnableUIAccessTopMost = false;
                        App.IsUIAccessTopMostEnabled = false;
                        mainWindow.ApplyUIAccessTopMost();
                        MainWindow.SaveSettingsToFile();
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
                if (MainWindow.Settings.Advanced != null)
                {
                    MainWindow.Settings.Advanced.EnableUIAccessTopMost = false;
                    MainWindow.SaveSettingsToFile();
                }

                App.IsUIAccessTopMostEnabled = false;

                var msg = Properties.Strings.GetString("Startup_TopMostMode_Normal_RestartRequired");
                var result = System.Windows.MessageBox.Show(msg, "Ink Canvas", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    RestartApp(_isAdmin);
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
                if (MainWindow.Settings.Advanced != null)
                {
                    MainWindow.Settings.Advanced.EnableUIAccessTopMost = true;

                    if (!MainWindow.Settings.Advanced.IsAlwaysOnTop)
                    {
                        MainWindow.Settings.Advanced.IsAlwaysOnTop = true;
                        ToggleSwitchAlwaysOnTop.IsOn = true;
                    }

                    MainWindow.SaveSettingsToFile();
                }

                var msg = Properties.Strings.GetString("Startup_TopMostMode_UIA_RestartRequired");
                var result = System.Windows.MessageBox.Show(msg, "Ink Canvas", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    App.IsUIAccessTopMostEnabled = true;
                    RestartApp(true);
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
                RestartApp(asAdmin);
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
                    MainWindow.StartAutomaticallyDel("InkCanvas");
                    MainWindow.StartAutomaticallyCreate("Ink Canvas Annotation");
                }
                else
                {
                    MainWindow.StartAutomaticallyDel("InkCanvas");
                    MainWindow.StartAutomaticallyDel("Ink Canvas Annotation");
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

                if (MainWindow.Settings.Startup != null)
                {
                    MainWindow.Settings.Startup.IsFoldAtStartup = newState;
                    MainWindow.SaveSettingsToFile();
                }
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
                bool newState = ToggleSwitchPPTOnlyMode.IsOn;

                Windows.SettingsViews.MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchMode", newState);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置仅PPT模式时出错: {ex.Message}");
            }
        }

        #endregion
    }
}
