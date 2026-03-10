using iNKORE.UI.WPF.Helpers;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Ink_Canvas.Windows.SettingsViews
{
    /// <summary>
    /// StartupPanel.xaml 的交互逻辑
    /// </summary>
    public partial class StartupPanel : UserControl
    {
        private bool _isLoaded = false;

        public StartupPanel()
        {
            InitializeComponent();
            Loaded += StartupPanel_Loaded;
        }

        private void StartupPanel_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
            // 添加触摸支持
            EnableTouchSupport();
            // 应用主题
            ApplyTheme();
            _isLoaded = true;
        }

        /// <summary>
        /// 为面板中的所有交互控件启用触摸支持
        /// </summary>
        private void EnableTouchSupport()
        {
            try
            {
                // 延迟执行，确保所有控件都已加载
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    EnableTouchSupportForControls(this);
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"StartupPanel 启用触摸支持时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 为控件树中的所有交互控件启用触摸支持
        /// </summary>
        private void EnableTouchSupportForControls(System.Windows.DependencyObject parent)
        {
            // 使用 MainWindowSettingsHelper 的通用方法
            MainWindowSettingsHelper.EnableTouchSupportForControls(parent);
        }

        public event EventHandler<RoutedEventArgs> IsTopBarNeedShadowEffect;
        public event EventHandler<RoutedEventArgs> IsTopBarNeedNoShadowEffect;

        private void ScrollViewerEx_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            var scrollViewer = (ScrollViewer)sender;
            if (scrollViewer.VerticalOffset >= 10)
            {
                IsTopBarNeedShadowEffect?.Invoke(this, new RoutedEventArgs());
            }
            else
            {
                IsTopBarNeedNoShadowEffect?.Invoke(this, new RoutedEventArgs());
            }
        }

        /// <summary>
        /// 加载设置到UI
        /// </summary>
        public void LoadSettings()
        {
            if (MainWindow.Settings == null) return;

            _isLoaded = false;

            try
            {
                // 开机时运行
                var toggleSwitchRunAtStartup = FindToggleSwitch("ToggleSwitchRunAtStartup");
                if (toggleSwitchRunAtStartup != null)
                {
                    // 检查启动项是否存在
                    bool runAtStartup = System.IO.File.Exists(
                        Environment.GetFolderPath(Environment.SpecialFolder.Startup) + "\\Ink Canvas Annotation.lnk");
                    SetToggleSwitchState(toggleSwitchRunAtStartup, runAtStartup);
                }

                // 启动时折叠
                var toggleSwitchFoldAtStartup = FindToggleSwitch("ToggleSwitchFoldAtStartup");
                if (toggleSwitchFoldAtStartup != null)
                {
                    SetToggleSwitchState(toggleSwitchFoldAtStartup, MainWindow.Settings.Startup.IsFoldAtStartup);
                }

                // 窗口无焦点模式
                var toggleSwitchNoFocusMode = FindToggleSwitch("ToggleSwitchNoFocusMode");
                if (toggleSwitchNoFocusMode != null && MainWindow.Settings.Advanced != null)
                {
                    SetToggleSwitchState(toggleSwitchNoFocusMode, MainWindow.Settings.Advanced.IsNoFocusMode);
                }

                // 窗口无边框模式
                var toggleSwitchWindowMode = FindToggleSwitch("ToggleSwitchWindowMode");
                if (toggleSwitchWindowMode != null && MainWindow.Settings.Advanced != null)
                {
                    SetToggleSwitchState(toggleSwitchWindowMode, MainWindow.Settings.Advanced.WindowMode);
                }

                // 窗口置顶
                var toggleSwitchAlwaysOnTop = FindToggleSwitch("ToggleSwitchAlwaysOnTop");
                if (toggleSwitchAlwaysOnTop != null && MainWindow.Settings.Advanced != null)
                {
                    SetToggleSwitchState(toggleSwitchAlwaysOnTop, MainWindow.Settings.Advanced.IsAlwaysOnTop);
                }

                // UIA置顶
                var toggleSwitchUIAccessTopMost = FindToggleSwitch("ToggleSwitchUIAccessTopMost");
                if (toggleSwitchUIAccessTopMost != null && MainWindow.Settings.Advanced != null)
                {
                    SetToggleSwitchState(toggleSwitchUIAccessTopMost, MainWindow.Settings.Advanced.EnableUIAccessTopMost);
                }

                // 仅PPT模式
                var toggleSwitchMode = FindToggleSwitch("ToggleSwitchMode");
                if (toggleSwitchMode != null && MainWindow.Settings.ModeSettings != null)
                {
                    SetToggleSwitchState(toggleSwitchMode, MainWindow.Settings.ModeSettings.IsPPTOnlyMode);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载启动设置时出错: {ex.Message}");
            }

            _isLoaded = true;
        }

        /// <summary>
        /// 查找ToggleSwitch控件
        /// </summary>
        private Border FindToggleSwitch(string name)
        {
            return this.FindDescendantByName(name) as Border;
        }

        /// <summary>
        /// 设置ToggleSwitch状态
        /// </summary>
        private void SetToggleSwitchState(Border toggleSwitch, bool isOn)
        {
            if (toggleSwitch == null) return;
            toggleSwitch.Background = isOn
                ? new SolidColorBrush(Color.FromRgb(53, 132, 228))
                : (ThemeHelper.IsDarkTheme ? ThemeHelper.GetButtonBackgroundBrush() : new SolidColorBrush(Color.FromRgb(225, 225, 225)));
            var innerBorder = toggleSwitch.Child as Border;
            if (innerBorder != null)
            {
                innerBorder.HorizontalAlignment = isOn ? HorizontalAlignment.Right : HorizontalAlignment.Left;
                innerBorder.Background = new SolidColorBrush(Colors.White);
            }
        }

        private bool GetCurrentSettingValue(string tag)
        {
            if (MainWindow.Settings == null) return false;

            try
            {
                switch (tag)
                {
                    case "RunAtStartup":
                        // 检查启动项是否存在
                        return System.IO.File.Exists(
                            Environment.GetFolderPath(Environment.SpecialFolder.Startup) + "\\Ink Canvas Annotation.lnk");
                    case "FoldAtStartup":
                        return MainWindow.Settings.Startup?.IsFoldAtStartup ?? false;
                    case "NoFocusMode":
                        return MainWindow.Settings.Advanced?.IsNoFocusMode ?? false;
                    case "WindowMode":
                        return MainWindow.Settings.Advanced?.WindowMode ?? false;
                    case "AlwaysOnTop":
                        return MainWindow.Settings.Advanced?.IsAlwaysOnTop ?? false;
                    case "UIAccessTopMost":
                        return MainWindow.Settings.Advanced?.EnableUIAccessTopMost ?? false;
                    case "Mode":
                        return MainWindow.Settings.ModeSettings?.IsPPTOnlyMode ?? false;
                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// ToggleSwitch点击事件处理
        /// </summary>
        private void ToggleSwitch_Click(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            var border = sender as Border;
            if (border == null) return;

            string tag = border.Tag?.ToString();
            if (string.IsNullOrEmpty(tag)) return;

            bool currentState = GetCurrentSettingValue(tag);
            bool newState = !currentState;
            SetToggleSwitchState(border, newState);

            switch (tag)
            {
                case "RunAtStartup":
                    // 直接调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchRunAtStartup", newState);
                    break;

                case "FoldAtStartup":
                    // 直接调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchFoldAtStartup", newState);
                    break;

                case "NoFocusMode":
                    // 窗口无焦点模式
                    MainWindowSettingsHelper.UpdateSettingDirectly(() =>
                    {
                        if (MainWindow.Settings.Advanced != null)
                        {
                            MainWindow.Settings.Advanced.IsNoFocusMode = newState;
                        }
                    }, "ToggleSwitchNoFocusMode");
                    // 调用 ApplyNoFocusMode 方法
                    MainWindowSettingsHelper.InvokeMainWindowMethod("ApplyNoFocusMode");
                    break;

                case "WindowMode":
                    // 窗口无边框模式
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchWindowMode", newState);
                    break;

                case "AlwaysOnTop":
                    // 窗口置顶
                    MainWindowSettingsHelper.UpdateSettingDirectly(() =>
                    {
                        if (MainWindow.Settings.Advanced != null)
                        {
                            MainWindow.Settings.Advanced.IsAlwaysOnTop = newState;
                        }
                    }, "ToggleSwitchAlwaysOnTop");
                    // 调用 SetAlwaysOnTop 方法（如果存在）
                    MainWindowSettingsHelper.InvokeMainWindowMethod("SetAlwaysOnTop", newState);
                    break;

                case "UIAccessTopMost":
                    // UIA置顶
                    MainWindowSettingsHelper.UpdateSettingDirectly(() =>
                    {
                        if (MainWindow.Settings.Advanced != null)
                        {
                            MainWindow.Settings.Advanced.EnableUIAccessTopMost = newState;
                        }
                    }, "ToggleSwitchUIAccessTopMost");
                    break;

                case "Mode":
                    // 仅PPT模式
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchMode", newState);
                    break;
            }
        }


        /// <summary>
        /// 应用主题
        /// </summary>
        public void ApplyTheme()
        {
            try
            {
                ThemeHelper.ApplyThemeToControl(this);
                if (_isLoaded)
                {
                    LoadSettings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"StartupPanel 应用主题时出错: {ex.Message}");
            }
        }
    }
}

