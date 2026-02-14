using Ink_Canvas;
using iNKORE.UI.WPF.Helpers;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Application = System.Windows.Application;

namespace Ink_Canvas.Windows.SettingsViews
{
    /// <summary>
    /// AdvancedPanel.xaml 的交互逻辑
    /// </summary>
    public partial class AdvancedPanel : UserControl
    {
        private bool _isLoaded = false;

        public AdvancedPanel()
        {
            InitializeComponent();
            Loaded += AdvancedPanel_Loaded;
        }

        private void AdvancedPanel_Loaded(object sender, RoutedEventArgs e)
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
                System.Diagnostics.Debug.WriteLine($"AdvancedPanel 启用触摸支持时出错: {ex.Message}");
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
            if (MainWindow.Settings == null || MainWindow.Settings.Advanced == null) return;

            _isLoaded = false;

            try
            {
                var advanced = MainWindow.Settings.Advanced;

                // 特殊屏幕模式
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchIsSpecialScreen"), advanced.IsSpecialScreen);

                // 触摸倍数
                if (TouchMultiplierSlider != null)
                {
                    TouchMultiplierSlider.Value = advanced.TouchMultiplier;
                    if (TouchMultiplierText != null)
                    {
                        TouchMultiplierText.Text = advanced.TouchMultiplier.ToString("F2");
                    }
                }

                // 橡皮擦绑定触摸大小倍数
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchEraserBindTouchMultiplier"), advanced.EraserBindTouchMultiplier);

                // 笔尖模式 BoundsWidth
                if (NibModeBoundsWidthSlider != null)
                {
                    NibModeBoundsWidthSlider.Value = advanced.NibModeBoundsWidth;
                    if (NibModeBoundsWidthText != null)
                    {
                        NibModeBoundsWidthText.Text = advanced.NibModeBoundsWidth.ToString();
                    }
                }

                // 手指模式 BoundsWidth
                if (FingerModeBoundsWidthSlider != null)
                {
                    FingerModeBoundsWidthSlider.Value = advanced.FingerModeBoundsWidth;
                    if (FingerModeBoundsWidthText != null)
                    {
                        FingerModeBoundsWidthText.Text = advanced.FingerModeBoundsWidth.ToString();
                    }
                }

                // 四边红外模式
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchIsQuadIR"), advanced.IsQuadIR);

                // 记录日志
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchIsLogEnabled"), advanced.IsLogEnabled);

                // 日志以日期保存
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchIsSaveLogByDate"), advanced.IsSaveLogByDate);

                // 关闭软件时二次弹窗确认
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchIsSecondConfimeWhenShutdownApp"), advanced.IsSecondConfirmWhenShutdownApp);

                // 实验性功能
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchIsEnableFullScreenHelper"), advanced.IsEnableFullScreenHelper);
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchIsEnableAvoidFullScreenHelper"), advanced.IsEnableAvoidFullScreenHelper);
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchIsEnableEdgeGestureUtil"), advanced.IsEnableEdgeGestureUtil);
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchIsEnableForceFullScreen"), advanced.IsEnableForceFullScreen);
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchIsEnableDPIChangeDetection"), advanced.IsEnableDPIChangeDetection);
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchIsEnableResolutionChangeDetection"), advanced.IsEnableResolutionChangeDetection);

                // 备份设置
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchIsAutoBackupBeforeUpdate"), advanced.IsAutoBackupBeforeUpdate);
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchIsAutoBackupEnabled"), advanced.IsAutoBackupEnabled);
                SetOptionButtonState("AutoBackupInterval", advanced.AutoBackupIntervalDays);

                // 外部协议
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchIsEnableUriScheme"), advanced.IsEnableUriScheme);

                // 悬浮窗拦截
                // 注意：IsEnableFloatingWindowInterception 可能不在 Advanced 类中，需要确认
                // 这里先假设它在 Advanced 类中，如果不在，需要调整
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载高级设置时出错: {ex.Message}");
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
        private void SetOptionButtonState(string group, int selectedValue)
        {
            var buttons = new Dictionary<string, Dictionary<int, string>>
            {
                { "AutoBackupInterval", new Dictionary<int, string> { { 1, "1Day" }, { 3, "3Days" }, { 7, "7Days" }, { 14, "14Days" }, { 30, "30Days" } } }
            };

            if (!buttons.ContainsKey(group)) return;

            var buttonNames = buttons[group];
            if (!buttonNames.ContainsKey(selectedValue)) return;

            string buttonName = buttonNames[selectedValue];
            var button = this.FindDescendantByName($"{group}{buttonName}Border") as Border;
            if (button != null)
            {
                bool isDarkTheme = ThemeHelper.IsDarkTheme;
                var selectedBrush = isDarkTheme ? new SolidColorBrush(Color.FromRgb(25, 25, 25)) : new SolidColorBrush(Color.FromRgb(225, 225, 225));
                var unselectedBrush = new SolidColorBrush(Colors.Transparent);

                var parent = button.Parent as Panel;
                if (parent != null)
                {
                    foreach (var child in parent.Children)
                    {
                        if (child is Border childBorder && childBorder != button)
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

                button.Background = selectedBrush;
                var currentTextBlock = button.Child as TextBlock;
                if (currentTextBlock != null)
                {
                    currentTextBlock.FontWeight = FontWeights.Bold;
                    currentTextBlock.Foreground = ThemeHelper.GetTextPrimaryBrush();
                }
            }
        }

        private bool GetCurrentSettingValue(string tag)
        {
            if (MainWindow.Settings?.Advanced == null) return false;

            try
            {
                var advanced = MainWindow.Settings.Advanced;
                switch (tag)
                {
                    case "IsSpecialScreen":
                        return advanced.IsSpecialScreen;
                    case "EraserBindTouchMultiplier":
                        return advanced.EraserBindTouchMultiplier;
                    case "IsQuadIR":
                        return advanced.IsQuadIR;
                    case "IsLogEnabled":
                        return advanced.IsLogEnabled;
                    case "IsSaveLogByDate":
                        return advanced.IsSaveLogByDate;
                    case "IsSecondConfirmWhenShutdownApp":
                        return advanced.IsSecondConfirmWhenShutdownApp;
                    case "IsEnableFullScreenHelper":
                        return advanced.IsEnableFullScreenHelper;
                    case "IsEnableAvoidFullScreenHelper":
                        return advanced.IsEnableAvoidFullScreenHelper;
                    case "IsEnableEdgeGestureUtil":
                        return advanced.IsEnableEdgeGestureUtil;
                    case "IsEnableForceFullScreen":
                        return advanced.IsEnableForceFullScreen;
                    case "IsEnableDPIChangeDetection":
                        return advanced.IsEnableDPIChangeDetection;
                    case "IsEnableResolutionChangeDetection":
                        return advanced.IsEnableResolutionChangeDetection;
                    case "IsAutoBackupBeforeUpdate":
                        return advanced.IsAutoBackupBeforeUpdate;
                    case "IsAutoBackupEnabled":
                        return advanced.IsAutoBackupEnabled;
                    case "IsEnableUriScheme":
                        return advanced.IsEnableUriScheme;
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

            var advanced = MainWindow.Settings.Advanced;
            if (advanced == null) return;

            switch (tag)
            {
                case "IsSpecialScreen":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchIsSpecialScreen", newState);
                    break;

                case "EraserBindTouchMultiplier":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchEraserBindTouchMultiplier", newState);
                    break;

                case "IsQuadIR":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchIsQuadIR", newState);
                    break;

                case "IsLogEnabled":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchIsLogEnabled", newState);
                    break;

                case "IsSaveLogByDate":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchIsSaveLogByDate", newState);
                    break;

                case "IsSecondConfirmWhenShutdownApp":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchIsSecondConfimeWhenShutdownApp", newState);
                    break;

                case "IsEnableFullScreenHelper":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchIsEnableFullScreenHelper", newState);
                    break;

                case "IsEnableAvoidFullScreenHelper":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchIsEnableAvoidFullScreenHelper", newState);
                    break;

                case "IsEnableEdgeGestureUtil":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchIsEnableEdgeGestureUtil", newState);
                    break;

                case "IsEnableForceFullScreen":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchIsEnableForceFullScreen", newState);
                    break;

                case "IsEnableDPIChangeDetection":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchIsEnableDPIChangeDetection", newState);
                    break;

                case "IsEnableResolutionChangeDetection":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchIsEnableResolutionChangeDetection", newState);
                    break;

                case "IsAutoBackupBeforeUpdate":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchIsAutoBackupBeforeUpdate", newState);
                    break;

                case "IsAutoBackupEnabled":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchIsAutoBackupEnabled", newState);
                    break;

                case "IsEnableUriScheme":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchIsEnableUriScheme", newState);
                    break;
            }
        }

        /// <summary>
        /// ToggleSwitch键盘事件处理
        /// </summary>
        private void ToggleSwitch_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Space || e.Key == System.Windows.Input.Key.Enter)
            {
                ToggleSwitch_Click(sender, new RoutedEventArgs());
                e.Handled = true;
            }
        }

        /// <summary>
        /// Slider值变化事件处理
        /// </summary>
        private void TouchMultiplierSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isLoaded) return;
            if (TouchMultiplierSlider != null && TouchMultiplierText != null)
            {
                double value = TouchMultiplierSlider.Value;
                TouchMultiplierText.Text = value.ToString("F2");
                // 调用 MainWindow 中的方法
                MainWindowSettingsHelper.InvokeSliderValueChanged("TouchMultiplierSlider", value);
            }
        }

        private void NibModeBoundsWidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isLoaded) return;
            if (NibModeBoundsWidthSlider != null && NibModeBoundsWidthText != null)
            {
                double value = NibModeBoundsWidthSlider.Value;
                NibModeBoundsWidthText.Text = ((int)value).ToString();
                // 调用 MainWindow 中的方法
                MainWindowSettingsHelper.InvokeSliderValueChanged("NibModeBoundsWidthSlider", value);
            }
        }

        private void FingerModeBoundsWidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isLoaded) return;
            if (FingerModeBoundsWidthSlider != null && FingerModeBoundsWidthText != null)
            {
                double value = FingerModeBoundsWidthSlider.Value;
                FingerModeBoundsWidthText.Text = ((int)value).ToString();
                // 调用 MainWindow 中的方法
                MainWindowSettingsHelper.InvokeSliderValueChanged("FingerModeBoundsWidthSlider", value);
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
            var selectedBrush = isDarkTheme ? ThemeHelper.GetButtonBackgroundBrush() : new SolidColorBrush(Color.FromRgb(225, 225, 225));
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

            if (MainWindow.Settings.Advanced == null) return;

            switch (group)
            {
                case "AutoBackupInterval":
                    int days;
                    if (int.TryParse(value, out days))
                    {
                        // 尝试调用 MainWindow 中的方法
                        var mainWindow = Application.Current.MainWindow as MainWindow;
                        if (mainWindow != null)
                        {
                            var comboBox = mainWindow.FindName("ComboBoxAutoBackupInterval") as System.Windows.Controls.ComboBox;
                            if (comboBox != null)
                            {
                                // 找到对应的选项并设置
                                foreach (ComboBoxItem item in comboBox.Items)
                                {
                                    if (item.Tag != null && int.TryParse(item.Tag.ToString(), out int tagValue) && tagValue == days)
                                    {
                                        comboBox.SelectedItem = item;
                                        MainWindowSettingsHelper.InvokeComboBoxSelectionChanged("ComboBoxAutoBackupInterval", item);
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                // 如果找不到控件，直接更新设置
                                MainWindowSettingsHelper.UpdateSettingDirectly(() =>
                                {
                                    MainWindow.Settings.Advanced.AutoBackupIntervalDays = days;
                                }, "ComboBoxAutoBackupInterval");
                            }
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// 按钮点击事件处理（备份还原、文件关联等）
        /// </summary>
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            string action = button.Tag?.ToString();
            if (string.IsNullOrEmpty(action)) action = button.Name;

            // 这些按钮的功能可能需要调用 MainWindow 中的方法
            // 暂时先留空，后续可以根据需要实现
            switch (action)
            {
                case "manual_backup":
                case "BtnManualBackup":
                    // TODO: 调用 MainWindow 的备份方法
                    break;

                case "restore_backup":
                case "BtnRestoreBackup":
                    // TODO: 调用 MainWindow 的还原方法
                    break;

                case "unregister":
                case "BtnUnregisterFileAssociation":
                    // TODO: 调用 MainWindow 的取消文件关联方法
                    break;

                case "check":
                case "BtnCheckFileAssociation":
                    // TODO: 调用 MainWindow 的检查文件关联状态方法
                    break;

                case "register":
                case "BtnRegisterFileAssociation":
                    // TODO: 调用 MainWindow 的注册文件关联方法
                    break;

                case "dlass_settings":
                case "BtnDlassSettingsManage":
                    // TODO: 调用 MainWindow 的 Dlass 设置管理方法
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
                System.Diagnostics.Debug.WriteLine($"AdvancedPanel 应用主题时出错: {ex.Message}");
            }
        }
    }
}
