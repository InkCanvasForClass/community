using Ink_Canvas;
using iNKORE.UI.WPF.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Application = System.Windows.Application;

namespace Ink_Canvas.Windows.SettingsViews
{
    /// <summary>
    /// LuckyRandomPanel.xaml 的交互逻辑
    /// </summary>
    public partial class LuckyRandomPanel : UserControl
    {
        private bool _isLoaded = false;

        public LuckyRandomPanel()
        {
            InitializeComponent();
            Loaded += LuckyRandomPanel_Loaded;
        }

        private void LuckyRandomPanel_Loaded(object sender, RoutedEventArgs e)
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
                    MainWindowSettingsHelper.EnableTouchSupportForControls(this);
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LuckyRandomPanel 启用触摸支持时出错: {ex.Message}");
            }
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
            if (MainWindow.Settings == null || MainWindow.Settings.RandSettings == null) return;

            _isLoaded = false;

            try
            {
                var randSettings = MainWindow.Settings.RandSettings;

                // 显示修改随机点名名单的按钮
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchDisplayRandWindowNamesInputBtn"), randSettings.DisplayRandWindowNamesInputBtn);

                // 启用随机抽和单次抽按钮
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchShowRandomAndSingleDraw"), randSettings.ShowRandomAndSingleDraw);

                // 启用快抽悬浮按钮
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchEnableQuickDraw"), randSettings.EnableQuickDraw);

                // 直接调用外部点名
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchExternalCaller"), randSettings.DirectCallCiRand);

                // 点名类型
                SetOptionButtonState("ExternalCallerType", randSettings.ExternalCallerType);

                // 单次抽人窗口关闭延迟
                if (RandWindowOnceCloseLatencySlider != null)
                {
                    RandWindowOnceCloseLatencySlider.Value = randSettings.RandWindowOnceCloseLatency;
                    if (RandWindowOnceCloseLatencyText != null)
                    {
                        RandWindowOnceCloseLatencyText.Text = $"{randSettings.RandWindowOnceCloseLatency:F1}s";
                    }
                }

                // 单次随机点名人数上限
                if (RandWindowOnceMaxStudentsSlider != null)
                {
                    RandWindowOnceMaxStudentsSlider.Value = randSettings.RandWindowOnceMaxStudents;
                    if (RandWindowOnceMaxStudentsText != null)
                    {
                        RandWindowOnceMaxStudentsText.Text = randSettings.RandWindowOnceMaxStudents.ToString();
                    }
                }

                // 启用新点名UI
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchUseNewRollCallUI"), randSettings.UseNewRollCallUI);

                // 启用机器学习避免重复
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchEnableMLAvoidance"), randSettings.EnableMLAvoidance);

                // 避免重复历史记录数量
                if (MLAvoidanceHistorySlider != null)
                {
                    MLAvoidanceHistorySlider.Value = randSettings.MLAvoidanceHistoryCount;
                    if (MLAvoidanceHistoryText != null)
                    {
                        MLAvoidanceHistoryText.Text = randSettings.MLAvoidanceHistoryCount.ToString();
                    }
                }

                // 避免重复权重
                if (MLAvoidanceWeightSlider != null)
                {
                    MLAvoidanceWeightSlider.Value = randSettings.MLAvoidanceWeight;
                    if (MLAvoidanceWeightText != null)
                    {
                        MLAvoidanceWeightText.Text = $"{(randSettings.MLAvoidanceWeight * 100):F0}%";
                    }
                }

                // 背景选择
                SetOptionButtonState("PickNameBackground", randSettings.SelectedBackgroundIndex);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载幸运随机设置时出错: {ex.Message}");
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

        /// <summary>
        /// 设置选项按钮状态
        /// </summary>
        private void SetOptionButtonState(string group, int selectedIndex)
        {
            var buttons = new Dictionary<string, string[]>
            {
                { "ExternalCallerType", new[] { "ClassIsland", "SecRandom", "NamePicker" } },
                { "PickNameBackground", new[] { "Default" } }
            };

            if (!buttons.ContainsKey(group)) return;

            string[] buttonNames = buttons[group];

            bool isDarkTheme = ThemeHelper.IsDarkTheme;
            var selectedBrush = isDarkTheme ? new SolidColorBrush(Color.FromRgb(25, 25, 25)) : new SolidColorBrush(Color.FromRgb(225, 225, 225));
            var unselectedBrush = new SolidColorBrush(Colors.Transparent);

            for (int i = 0; i < buttonNames.Length; i++)
            {
                var button = this.FindDescendantByName($"{group}{buttonNames[i]}Border") as Border;
                if (button != null)
                {
                    if (i == selectedIndex)
                    {
                        button.Background = selectedBrush;
                        var textBlock = button.Child as TextBlock;
                        if (textBlock != null)
                        {
                            textBlock.FontWeight = FontWeights.Bold;
                            textBlock.Foreground = ThemeHelper.GetTextPrimaryBrush();
                        }
                    }
                    else
                    {
                        button.Background = unselectedBrush;
                        var textBlock = button.Child as TextBlock;
                        if (textBlock != null)
                        {
                            textBlock.FontWeight = FontWeights.Normal;
                            textBlock.Foreground = ThemeHelper.GetTextPrimaryBrush();
                        }
                    }
                }
            }
        }

        private bool GetCurrentSettingValue(string tag)
        {
            if (MainWindow.Settings?.RandSettings == null) return false;

            try
            {
                var randSettings = MainWindow.Settings.RandSettings;
                switch (tag)
                {
                    case "DisplayRandWindowNamesInputBtn":
                        return randSettings.DisplayRandWindowNamesInputBtn;
                    case "ShowRandomAndSingleDraw":
                        return randSettings.ShowRandomAndSingleDraw;
                    case "EnableQuickDraw":
                        return randSettings.EnableQuickDraw;
                    case "ExternalCaller":
                        return randSettings.DirectCallCiRand;
                    case "UseNewRollCallUI":
                        return randSettings.UseNewRollCallUI;
                    case "EnableMLAvoidance":
                        return randSettings.EnableMLAvoidance;
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

            var randSettings = MainWindow.Settings.RandSettings;
            if (randSettings == null) return;

            switch (tag)
            {
                case "DisplayRandWindowNamesInputBtn":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchDisplayRandWindowNamesInputBtn", newState);
                    break;

                case "ShowRandomAndSingleDraw":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchShowRandomAndSingleDraw", newState);
                    break;

                case "EnableQuickDraw":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchEnableQuickDraw", newState);
                    break;

                case "ExternalCaller":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchExternalCaller", newState);
                    break;

                case "UseNewRollCallUI":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchUseNewRollCallUI", newState);
                    break;

                case "EnableMLAvoidance":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchEnableMLAvoidance", newState);
                    break;
            }
        }

        /// <summary>
        /// 选项按钮点击事件处理
        /// </summary>
        private void OptionButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            var border = sender as Border;
            if (border == null) return;

            string tag = border.Tag?.ToString();
            if (string.IsNullOrEmpty(tag)) return;

            string[] parts = tag.Split('_');
            if (parts.Length < 2) return;

            string group = parts[0];
            string value = parts[1];

            bool isDarkTheme = ThemeHelper.IsDarkTheme;
            var selectedBrush = isDarkTheme ? new SolidColorBrush(Color.FromRgb(25, 25, 25)) : new SolidColorBrush(Color.FromRgb(225, 225, 225));
            var unselectedBrush = new SolidColorBrush(Colors.Transparent);

            var parent = border.Parent as Panel;
            if (parent != null)
            {
                foreach (var child in parent.Children)
                {
                    if (child is Border childBorder && childBorder != border)
                    {
                        string childTag = childBorder.Tag?.ToString();
                        if (!string.IsNullOrEmpty(childTag) && childTag.StartsWith(group + "_"))
                        {
                            childBorder.Background = unselectedBrush;
                            var textBlock = childBorder.Child as TextBlock;
                            if (textBlock != null)
                            {
                                textBlock.FontWeight = FontWeights.Normal;
                                textBlock.Foreground = ThemeHelper.GetTextPrimaryBrush();
                            }
                        }
                    }
                }
            }

            border.Background = selectedBrush;
            var currentTextBlock = border.Child as TextBlock;
            if (currentTextBlock != null)
            {
                currentTextBlock.FontWeight = FontWeights.Bold;
                currentTextBlock.Foreground = ThemeHelper.GetTextPrimaryBrush();
            }

            var randSettings = MainWindow.Settings.RandSettings;
            if (randSettings == null) return;

            switch (group)
            {
                case "ExternalCallerType":
                    int callerType;
                    switch (value)
                    {
                        case "ClassIsland":
                            callerType = 0;
                            break;
                        case "SecRandom":
                            callerType = 1;
                            break;
                        case "NamePicker":
                            callerType = 2;
                            break;
                        default:
                            callerType = 0;
                            break;
                    }
                    // 调用 MainWindow 中的方法
                    var mainWindow = Application.Current.MainWindow as MainWindow;
                    if (mainWindow != null)
                    {
                        var comboBox = mainWindow.FindName("ComboBoxExternalCallerType") as System.Windows.Controls.ComboBox;
                        if (comboBox != null && comboBox.Items.Count > callerType)
                        {
                            comboBox.SelectedIndex = callerType;
                            MainWindowSettingsHelper.InvokeComboBoxSelectionChanged("ComboBoxExternalCallerType", comboBox.Items[callerType]);
                        }
                        else
                        {
                            // 如果找不到控件，直接更新设置
                            MainWindowSettingsHelper.UpdateSettingDirectly(() =>
                            {
                                randSettings.ExternalCallerType = callerType;
                            }, "ComboBoxExternalCallerType");
                        }
                    }
                    break;

                case "PickNameBackground":
                    // 背景选择逻辑 - 这个设置可能没有对应的方法，直接更新
                    MainWindowSettingsHelper.UpdateSettingDirectly(() =>
                    {
                        randSettings.SelectedBackgroundIndex = 0; // 默认背景
                    }, "PickNameBackground");
                    break;
            }
        }

        /// <summary>
        /// Slider值变化事件处理
        /// </summary>
        private void RandWindowOnceCloseLatencySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isLoaded) return;
            if (RandWindowOnceCloseLatencySlider != null && RandWindowOnceCloseLatencyText != null)
            {
                double value = RandWindowOnceCloseLatencySlider.Value;
                RandWindowOnceCloseLatencyText.Text = $"{value:F1}s";
                // 调用 MainWindow 中的方法
                MainWindowSettingsHelper.InvokeSliderValueChanged("RandWindowOnceCloseLatencySlider", value);
            }
        }

        private void RandWindowOnceMaxStudentsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isLoaded) return;
            if (RandWindowOnceMaxStudentsSlider != null && RandWindowOnceMaxStudentsText != null)
            {
                double value = RandWindowOnceMaxStudentsSlider.Value;
                RandWindowOnceMaxStudentsText.Text = ((int)value).ToString();
                // 调用 MainWindow 中的方法
                MainWindowSettingsHelper.InvokeSliderValueChanged("RandWindowOnceMaxStudentsSlider", value);
            }
        }

        private void MLAvoidanceHistorySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isLoaded) return;
            if (MLAvoidanceHistorySlider != null && MLAvoidanceHistoryText != null)
            {
                double value = MLAvoidanceHistorySlider.Value;
                MLAvoidanceHistoryText.Text = ((int)value).ToString();
                // 调用 MainWindow 中的方法
                MainWindowSettingsHelper.InvokeSliderValueChanged("MLAvoidanceHistorySlider", value);
            }
        }

        private void MLAvoidanceWeightSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isLoaded) return;
            if (MLAvoidanceWeightSlider != null && MLAvoidanceWeightText != null)
            {
                double value = MLAvoidanceWeightSlider.Value;
                MLAvoidanceWeightText.Text = $"{(value * 100):F0}%";
                // 调用 MainWindow 中的方法
                MainWindowSettingsHelper.InvokeSliderValueChanged("MLAvoidanceWeightSlider", value);
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
                System.Diagnostics.Debug.WriteLine($"LuckyRandomPanel 应用主题时出错: {ex.Message}");
            }
        }
    }
}

