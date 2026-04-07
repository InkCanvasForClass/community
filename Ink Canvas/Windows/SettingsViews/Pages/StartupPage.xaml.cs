using System;
using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    /// <summary>
    /// StartupPage.xaml 的交互逻辑
    /// </summary>
    public partial class StartupPage : Page
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

        /// <summary>
        /// 加载设置到UI
        /// </summary>
        private void LoadSettings()
        {
            if (MainWindow.Settings == null) return;

            _isLoaded = false;

            try
            {
                // 窗口无焦点模式
                if (MainWindow.Settings.Advanced != null)
                {
                    ToggleSwitchNoFocusMode.IsOn = MainWindow.Settings.Advanced.IsNoFocusMode;
                }

                // 窗口无边框模式
                if (MainWindow.Settings.Advanced != null)
                {
                    ToggleSwitchWindowMode.IsOn = MainWindow.Settings.Advanced.WindowMode;
                }

                // 窗口置顶
                if (MainWindow.Settings.Advanced != null)
                {
                    ToggleSwitchAlwaysOnTop.IsOn = MainWindow.Settings.Advanced.IsAlwaysOnTop;
                }

                // UIA置顶
                if (MainWindow.Settings.Advanced != null)
                {
                    ToggleSwitchUIAccessTopMost.IsOn = MainWindow.Settings.Advanced.EnableUIAccessTopMost;
                }

                // 开机时运行
                bool runAtStartup = System.IO.File.Exists(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.Startup) + "\\Ink Canvas Annotation.lnk");
                ToggleSwitchRunAtStartup.IsOn = runAtStartup;

                // 启动时折叠
                if (MainWindow.Settings.Startup != null)
                {
                    ToggleSwitchFoldAtStartup.IsOn = MainWindow.Settings.Startup.IsFoldAtStartup;
                }

                // 仅PPT模式
                if (MainWindow.Settings.ModeSettings != null)
                {
                    ToggleSwitchPPTOnlyMode.IsOn = MainWindow.Settings.ModeSettings.IsPPTOnlyMode;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载启动设置时出错: {ex.Message}");
            }

            _isLoaded = true;
        }

        #region 窗口设置事件处理

        /// <summary>
        /// 窗口无焦点模式开关事件
        /// </summary>
        private void ToggleSwitchNoFocusMode_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            try
            {
                bool newState = ToggleSwitchNoFocusMode.IsOn;

                // 使用Helper类更新设置并应用
                Windows.SettingsViews.MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchNoFocusMode", newState);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"设置窗口无焦点模式时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 窗口无边框模式开关事件
        /// </summary>
        private void ToggleSwitchWindowMode_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            try
            {
                bool newState = ToggleSwitchWindowMode.IsOn;

                // 使用Helper类更新设置并应用
                Windows.SettingsViews.MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchWindowMode", newState);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"设置窗口无边框模式时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 窗口置顶开关事件
        /// </summary>
        private void ToggleSwitchAlwaysOnTop_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            try
            {
                bool newState = ToggleSwitchAlwaysOnTop.IsOn;

                // 使用Helper类更新设置并应用
                Windows.SettingsViews.MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchAlwaysOnTop", newState);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"设置窗口置顶时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// UIA置顶开关事件
        /// </summary>
        private void ToggleSwitchUIAccessTopMost_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            try
            {
                bool newState = ToggleSwitchUIAccessTopMost.IsOn;

                // 更新Settings对象
                if (MainWindow.Settings.Advanced != null)
                {
                    MainWindow.Settings.Advanced.EnableUIAccessTopMost = newState;
                }

                // 保存设置
                MainWindow.SaveSettingsToFile();

                // 通知其他面板同步状态
                Windows.SettingsViews.MainWindowSettingsHelper.NotifySettingsPanelsSyncState("ToggleSwitchUIAccessTopMost");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"设置UIA置顶时出错: {ex.Message}");
            }
        }

        #endregion

        #region 启动设置事件处理

        /// <summary>
        /// 开机时运行开关事件
        /// </summary>
        private void ToggleSwitchRunAtStartup_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            try
            {
                bool newState = ToggleSwitchRunAtStartup.IsOn;

                // 使用Helper类更新设置并应用
                Windows.SettingsViews.MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchRunAtStartup", newState);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"设置开机启动时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 开机运行后收纳到侧边栏开关事件
        /// </summary>
        private void ToggleSwitchFoldAtStartup_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            try
            {
                bool newState = ToggleSwitchFoldAtStartup.IsOn;

                // 使用Helper类更新设置并应用
                Windows.SettingsViews.MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchFoldAtStartup", newState);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"设置开机折叠时出错: {ex.Message}");
            }
        }

        #endregion

        #region 模式设置事件处理

        /// <summary>
        /// 仅PPT模式开关事件
        /// </summary>
        private void ToggleSwitchPPTOnlyMode_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            try
            {
                bool newState = ToggleSwitchPPTOnlyMode.IsOn;

                // 使用Helper类更新设置并应用
                Windows.SettingsViews.MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchMode", newState);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"设置仅PPT模式时出错: {ex.Message}");
            }
        }

        #endregion
    }
}
