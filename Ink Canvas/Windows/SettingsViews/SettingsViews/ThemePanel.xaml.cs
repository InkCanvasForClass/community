using Ink_Canvas;
using iNKORE.UI.WPF.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Hardcodet.Wpf.TaskbarNotification;

namespace Ink_Canvas.Windows.SettingsViews
{
    /// <summary>
    /// ThemePanel.xaml 的交互逻辑
    /// </summary>
    public partial class ThemePanel : UserControl
    {
        private bool _isLoaded = false;

        public ThemePanel()
        {
            InitializeComponent();
            Loaded += ThemePanel_Loaded;
        }

        private void ThemePanel_Loaded(object sender, RoutedEventArgs e)
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
                System.Diagnostics.Debug.WriteLine($"ThemePanel 启用触摸支持时出错: {ex.Message}");
            }
        }

        public event EventHandler<RoutedEventArgs> IsTopBarNeedShadowEffect;
        public event EventHandler<RoutedEventArgs> IsTopBarNeedNoShadowEffect;
        public event EventHandler<RoutedEventArgs> ThemeChanged;

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
            if (MainWindow.Settings == null || MainWindow.Settings.Appearance == null) return;

            _isLoaded = false;

            try
            {
                var appearance = MainWindow.Settings.Appearance;

                // 主题设置
                SetOptionButtonState("Theme", appearance.Theme);

                // 启用启动动画
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchEnableSplashScreen"), appearance.EnableSplashScreen);
                if (SplashScreenStylePanel != null)
                {
                    SplashScreenStylePanel.Visibility = appearance.EnableSplashScreen ? Visibility.Visible : Visibility.Collapsed;
                }

                // 启动动画样式
                if (ComboBoxSplashScreenStyle != null)
                {
                    ComboBoxSplashScreenStyle.SelectedIndex = Math.Min(appearance.SplashScreenStyle, ComboBoxSplashScreenStyle.Items.Count - 1);
                }

                // 浮动工具栏图标
                if (ComboBoxFloatingBarImg != null)
                {
                    // 更新自定义图标列表（如果需要）
                    // UpdateCustomIconsInComboBox();
                    
                    int selectedIndex = Math.Min(appearance.FloatingBarImg, ComboBoxFloatingBarImg.Items.Count - 1);
                    ComboBoxFloatingBarImg.SelectedIndex = selectedIndex;
                }

                // 浮动工具栏缩放
                if (ViewboxFloatingBarScaleTransformValueSlider != null)
                {
                    double val = appearance.ViewboxFloatingBarScaleTransformValue;
                    if (val == 0) val = 1.0;
                    ViewboxFloatingBarScaleTransformValueSlider.Value = val;
                    if (ViewboxFloatingBarScaleTransformValueText != null)
                    {
                        ViewboxFloatingBarScaleTransformValueText.Text = val.ToString("F2");
                    }
                }

                // 浮动工具栏透明度
                if (ViewboxFloatingBarOpacityValueSlider != null)
                {
                    ViewboxFloatingBarOpacityValueSlider.Value = appearance.ViewboxFloatingBarOpacityValue;
                    if (ViewboxFloatingBarOpacityValueText != null)
                    {
                        ViewboxFloatingBarOpacityValueText.Text = appearance.ViewboxFloatingBarOpacityValue.ToString("F2");
                    }
                }

                // 浮栏在PPT下透明度
                if (ViewboxFloatingBarOpacityInPPTValueSlider != null)
                {
                    ViewboxFloatingBarOpacityInPPTValueSlider.Value = appearance.ViewboxFloatingBarOpacityInPPTValue;
                    if (ViewboxFloatingBarOpacityInPPTValueText != null)
                    {
                        ViewboxFloatingBarOpacityInPPTValueText.Text = appearance.ViewboxFloatingBarOpacityInPPTValue.ToString("F2");
                    }
                }

                // 在调色盘窗口中显示笔尖模式按钮
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchEnableDisPlayNibModeToggle"), appearance.IsEnableDisPlayNibModeToggler);

                // 使用老版浮动栏按钮UI
                if (CheckBoxUseLegacyFloatingBarUI != null)
                {
                    CheckBoxUseLegacyFloatingBarUI.IsChecked = appearance.UseLegacyFloatingBarUI;
                }

                // 浮动栏按钮显示控制
                if (CheckBoxShowShapeButton != null) CheckBoxShowShapeButton.IsChecked = appearance.IsShowShapeButton;
                if (CheckBoxShowUndoButton != null) CheckBoxShowUndoButton.IsChecked = appearance.IsShowUndoButton;
                if (CheckBoxShowRedoButton != null) CheckBoxShowRedoButton.IsChecked = appearance.IsShowRedoButton;
                if (CheckBoxShowClearButton != null) CheckBoxShowClearButton.IsChecked = appearance.IsShowClearButton;
                if (CheckBoxShowWhiteboardButton != null) CheckBoxShowWhiteboardButton.IsChecked = appearance.IsShowWhiteboardButton;
                if (CheckBoxShowHideButton != null) CheckBoxShowHideButton.IsChecked = appearance.IsShowHideButton;
                if (CheckBoxShowQuickColorPalette != null) CheckBoxShowQuickColorPalette.IsChecked = appearance.IsShowQuickColorPalette;
                if (CheckBoxShowLassoSelectButton != null) CheckBoxShowLassoSelectButton.IsChecked = appearance.IsShowLassoSelectButton;
                if (CheckBoxShowClearAndMouseButton != null) CheckBoxShowClearAndMouseButton.IsChecked = appearance.IsShowClearAndMouseButton;

                // 启用系统托盘图标
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchEnableTrayIcon"), appearance.EnableTrayIcon);

                // 画板UI缩放
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchEnableViewboxBlackBoardScaleTransform"), appearance.EnableViewboxBlackBoardScaleTransform);

                // 白板模式时间显示
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchEnableTimeDisplayInWhiteboardMode"), appearance.EnableTimeDisplayInWhiteboardMode);

                // 白板模式鸡汤文
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchEnableChickenSoupInWhiteboardMode"), appearance.EnableChickenSoupInWhiteboardMode);

                // 启用快捷面板
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchEnableQuickPanel"), appearance.IsShowQuickPanel);

                // 退出折叠模式后自动进入批注模式
                if (MainWindow.Settings.Automation != null)
                {
                    SetToggleSwitchState(FindToggleSwitch("ToggleSwitchAutoEnterAnnotationModeWhenExitFoldMode"), MainWindow.Settings.Automation.IsAutoEnterAnnotationModeWhenExitFoldMode);
                }

                // PPT放映结束后自动折叠
                if (MainWindow.Settings.Automation != null)
                {
                    SetToggleSwitchState(FindToggleSwitch("ToggleSwitchAutoFoldAfterPPTSlideShow"), MainWindow.Settings.Automation.IsAutoFoldAfterPPTSlideShow);
                }

                // 退出白板模式后自动折叠
                if (MainWindow.Settings.Automation != null)
                {
                    SetToggleSwitchState(FindToggleSwitch("ToggleSwitchAutoFoldWhenExitWhiteboard"), MainWindow.Settings.Automation.IsAutoFoldWhenExitWhiteboard);
                }

                // 信仰の源出自Where？
                SetOptionButtonState("ChickenSoupSource", appearance.ChickenSoupSource);

                // 取消收纳按钮图标
                SetOptionButtonState("UnFoldBtnImg", appearance.UnFoldButtonImageType);

                // 快捷调色盘显示模式
                SetOptionButtonState("QuickColorPaletteDisplayMode", appearance.QuickColorPaletteDisplayMode);

                // 橡皮按钮显示
                SetOptionButtonState("EraserDisplayOption", appearance.EraserDisplayOption);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载个性化设置时出错: {ex.Message}");
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
                    case "EnableSplashScreen":
                        return MainWindow.Settings.Appearance?.EnableSplashScreen ?? false;
                    case "EnableDisPlayNibModeToggle":
                        return MainWindow.Settings.Appearance?.IsEnableDisPlayNibModeToggler ?? false;
                    case "EnableTrayIcon":
                        return MainWindow.Settings.Appearance?.EnableTrayIcon ?? false;
                    case "EnableViewboxBlackBoardScaleTransform":
                        return MainWindow.Settings.Appearance?.EnableViewboxBlackBoardScaleTransform ?? false;
                    case "EnableTimeDisplayInWhiteboardMode":
                        return MainWindow.Settings.Appearance?.EnableTimeDisplayInWhiteboardMode ?? false;
                    case "EnableChickenSoupInWhiteboardMode":
                        return MainWindow.Settings.Appearance?.EnableChickenSoupInWhiteboardMode ?? false;
                    case "EnableQuickPanel":
                        return MainWindow.Settings.Appearance?.IsShowQuickPanel ?? false;
                    case "AutoEnterAnnotationModeWhenExitFoldMode":
                        return MainWindow.Settings.Automation?.IsAutoEnterAnnotationModeWhenExitFoldMode ?? false;
                    case "AutoFoldAfterPPTSlideShow":
                        return MainWindow.Settings.Automation?.IsAutoFoldAfterPPTSlideShow ?? false;
                    case "AutoFoldWhenExitWhiteboard":
                        return MainWindow.Settings.Automation?.IsAutoFoldWhenExitWhiteboard ?? false;
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

            var appearance = MainWindow.Settings.Appearance;
            if (appearance == null) return;

            switch (tag)
            {
                case "EnableSplashScreen":
                    // 调用 MainWindow 中的方法（带主题检查）
                    MainWindowSettingsHelper.InvokeToggleSwitchToggledWithThemeCheck("ToggleSwitchEnableSplashScreen", newState);
                    // 更新UI状态
                    if (SplashScreenStylePanel != null)
                    {
                        SplashScreenStylePanel.Visibility = newState ? Visibility.Visible : Visibility.Collapsed;
                    }
                    break;

                case "EnableDisPlayNibModeToggle":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchEnableDisPlayNibModeToggle", newState);
                    break;

                case "EnableTrayIcon":
                    // 调用 MainWindow 中的方法（带主题检查）
                    MainWindowSettingsHelper.InvokeToggleSwitchToggledWithThemeCheck("ToggleSwitchEnableTrayIcon", newState);
                    // 更新系统托盘图标可见性
                    var taskbar = Application.Current.Resources["TaskbarTrayIcon"] as TaskbarIcon;
                    if (taskbar != null)
                    {
                        taskbar.Visibility = newState ? Visibility.Visible : Visibility.Collapsed;
                    }
                    break;

                case "EnableViewboxBlackBoardScaleTransform":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchEnableViewboxBlackBoardScaleTransform", newState);
                    break;

                case "EnableTimeDisplayInWhiteboardMode":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchEnableTimeDisplayInWhiteboardMode", newState);
                    break;

                case "EnableChickenSoupInWhiteboardMode":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchEnableChickenSoupInWhiteboardMode", newState);
                    break;

                case "EnableQuickPanel":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchEnableQuickPanel", newState);
                    break;

                case "AutoEnterAnnotationModeWhenExitFoldMode":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchAutoEnterAnnotationModeWhenExitFoldMode", newState);
                    break;

                case "AutoFoldAfterPPTSlideShow":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchAutoFoldAfterPPTSlideShow", newState);
                    break;

                case "AutoFoldWhenExitWhiteboard":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchAutoFoldWhenExitWhiteboard", newState);
                    break;
            }
        }

        /// <summary>
        /// ComboBox选择变化事件处理
        /// </summary>
        private void ComboBoxSplashScreenStyle_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            if (ComboBoxSplashScreenStyle?.SelectedIndex >= 0)
            {
                // 调用 MainWindow 中的方法（带主题检查）
                MainWindowSettingsHelper.InvokeComboBoxSelectionChangedWithThemeCheck("ComboBoxSplashScreenStyle", ComboBoxSplashScreenStyle.SelectedItem);
            }
        }

        private void ComboBoxFloatingBarImg_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            if (ComboBoxFloatingBarImg?.SelectedIndex >= 0)
            {
                // 调用 MainWindow 中的方法（带主题检查）
                MainWindowSettingsHelper.InvokeComboBoxSelectionChangedWithThemeCheck("ComboBoxFloatingBarImg", ComboBoxFloatingBarImg.SelectedItem);
            }
        }

        /// <summary>
        /// Slider值变化事件处理
        /// </summary>
        private void ViewboxFloatingBarScaleTransformValueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isLoaded) return;
            if (ViewboxFloatingBarScaleTransformValueSlider != null && ViewboxFloatingBarScaleTransformValueText != null)
            {
                double val = ViewboxFloatingBarScaleTransformValueSlider.Value;
                ViewboxFloatingBarScaleTransformValueText.Text = val.ToString("F2");
                // 调用 MainWindow 中的方法（会自动检查主题更新）
                MainWindowSettingsHelper.InvokeSliderValueChanged("ViewboxFloatingBarScaleTransformValueSlider", val);
            }
        }

        private void ViewboxFloatingBarOpacityValueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isLoaded) return;
            if (ViewboxFloatingBarOpacityValueSlider != null && ViewboxFloatingBarOpacityValueText != null)
            {
                double val = ViewboxFloatingBarOpacityValueSlider.Value;
                ViewboxFloatingBarOpacityValueText.Text = val.ToString("F2");
                // 调用 MainWindow 中的方法（会自动检查主题更新）
                MainWindowSettingsHelper.InvokeSliderValueChanged("ViewboxFloatingBarOpacityValueSlider", val);
            }
        }

        private void ViewboxFloatingBarOpacityInPPTValueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isLoaded) return;
            if (ViewboxFloatingBarOpacityInPPTValueSlider != null && ViewboxFloatingBarOpacityInPPTValueText != null)
            {
                double val = ViewboxFloatingBarOpacityInPPTValueSlider.Value;
                ViewboxFloatingBarOpacityInPPTValueText.Text = val.ToString("F2");
                // 调用 MainWindow 中的方法（会自动检查主题更新）
                MainWindowSettingsHelper.InvokeSliderValueChanged("ViewboxFloatingBarOpacityInPPTValueSlider", val);
            }
        }

        /// <summary>
        /// CheckBox变化事件处理
        /// </summary>
        private void CheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            var checkBox = sender as CheckBox;
            if (checkBox == null) return;

            string name = checkBox.Name;
            var appearance = MainWindow.Settings.Appearance;
            if (appearance == null) return;

            switch (name)
            {
                case "CheckBoxUseLegacyFloatingBarUI":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeCheckBoxCheckedChanged("CheckBoxUseLegacyFloatingBarUI", checkBox.IsChecked ?? false);
                    break;

                case "CheckBoxShowShapeButton":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeCheckBoxCheckedChanged("CheckBoxShowShapeButton", checkBox.IsChecked ?? false);
                    break;

                case "CheckBoxShowUndoButton":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeCheckBoxCheckedChanged("CheckBoxShowUndoButton", checkBox.IsChecked ?? false);
                    break;

                case "CheckBoxShowRedoButton":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeCheckBoxCheckedChanged("CheckBoxShowRedoButton", checkBox.IsChecked ?? false);
                    break;

                case "CheckBoxShowClearButton":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeCheckBoxCheckedChanged("CheckBoxShowClearButton", checkBox.IsChecked ?? false);
                    break;

                case "CheckBoxShowWhiteboardButton":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeCheckBoxCheckedChanged("CheckBoxShowWhiteboardButton", checkBox.IsChecked ?? false);
                    break;

                case "CheckBoxShowHideButton":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeCheckBoxCheckedChanged("CheckBoxShowHideButton", checkBox.IsChecked ?? false);
                    break;

                case "CheckBoxShowQuickColorPalette":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeCheckBoxCheckedChanged("CheckBoxShowQuickColorPalette", checkBox.IsChecked ?? false);
                    break;

                case "CheckBoxShowLassoSelectButton":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeCheckBoxCheckedChanged("CheckBoxShowLassoSelectButton", checkBox.IsChecked ?? false);
                    break;

                case "CheckBoxShowClearAndMouseButton":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeCheckBoxCheckedChanged("CheckBoxShowClearAndMouseButton", checkBox.IsChecked ?? false);
                    break;
            }
        }

        private void SetOptionButtonState(string group, int selectedIndex)
        {
            var buttons = new Dictionary<string, string[]>
            {
                { "Theme", new[] { "Light", "Dark", "System" } },
                { "ChickenSoupSource", new[] { "Osu", "Motivational", "Gaokao", "Hitokoto" } },
                { "UnFoldBtnImg", new[] { "Arrow", "Pen" } },
                { "QuickColorPaletteDisplayMode", new[] { "Single", "Double" } },
                { "EraserDisplayOption", new[] { "Both", "Area", "Line", "None" } }
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

            var appearance = MainWindow.Settings.Appearance;
            if (appearance == null) return;

            switch (group)
            {
                case "Theme":
                    // 通过 MainWindowSettingsHelper 调用 ComboBoxTheme 的 SelectionChanged 事件处理器
                    try
                    {
                        var mainWindow = Application.Current.MainWindow as MainWindow;
                        if (mainWindow != null)
                        {
                            var comboBox = mainWindow.FindName("ComboBoxTheme") as System.Windows.Controls.ComboBox;
                            if (comboBox != null)
                            {
                                // 根据 value 找到对应的 ComboBoxItem
                                int themeIndex;
                                switch (value)
                                {
                                    case "Light":
                                        themeIndex = 0;
                                        break;
                                    case "Dark":
                                        themeIndex = 1;
                                        break;
                                    case "System":
                                        themeIndex = 2;
                                        break;
                                    default:
                                        themeIndex = 2;
                                        break;
                                }
                                
                                if (comboBox.Items.Count > themeIndex)
                                {
                                    var selectedItem = comboBox.Items[themeIndex];
                                    MainWindowSettingsHelper.InvokeComboBoxSelectionChangedWithThemeCheck("ComboBoxTheme", selectedItem);
                                }
                                else
                                {
                                    // 如果找不到控件，直接更新设置并通知主题更新
                                    MainWindowSettingsHelper.UpdateSettingSafely(() =>
                                    {
                                        appearance.Theme = themeIndex;
                                    }, "ComboBoxTheme_SelectionChanged", "ComboBoxTheme");
                                    MainWindowSettingsHelper.NotifyThemeUpdateIfNeeded("ComboBoxTheme");
                                    
                                    // 触发主题变化事件，通知设置窗口更新主题
                                    ThemeChanged?.Invoke(this, new RoutedEventArgs());
                                }
                            }
                            else
                            {
                                // 如果找不到控件，直接更新设置并通知主题更新
                                int themeIndex;
                                switch (value)
                                {
                                    case "Light":
                                        themeIndex = 0;
                                        break;
                                    case "Dark":
                                        themeIndex = 1;
                                        break;
                                    case "System":
                                        themeIndex = 2;
                                        break;
                                    default:
                                        themeIndex = 2;
                                        break;
                                }
                                MainWindowSettingsHelper.UpdateSettingSafely(() =>
                                {
                                    appearance.Theme = themeIndex;
                                }, "ComboBoxTheme_SelectionChanged", "ComboBoxTheme");
                                MainWindowSettingsHelper.NotifyThemeUpdateIfNeeded("ComboBoxTheme");
                                
                                // 触发主题变化事件，通知设置窗口更新主题
                                ThemeChanged?.Invoke(this, new RoutedEventArgs());
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"切换主题时出错: {ex.Message}");
                    }
                    break;

                case "ChickenSoupSource":
                    int sourceIndex;
                    switch (value)
                    {
                        case "Osu":
                            sourceIndex = 0;
                            break;
                        case "Motivational":
                            sourceIndex = 1;
                            break;
                        case "Gaokao":
                            sourceIndex = 2;
                            break;
                        case "Hitokoto":
                            sourceIndex = 3;
                            break;
                        default:
                            sourceIndex = 3;
                            break;
                    }
                    // 调用 MainWindow 中的方法
                    var mainWindow6 = Application.Current.MainWindow as MainWindow;
                    if (mainWindow6 != null)
                    {
                        var comboBox = mainWindow6.FindName("ComboBoxChickenSoupSource") as System.Windows.Controls.ComboBox;
                        if (comboBox != null && comboBox.Items.Count > sourceIndex)
                        {
                            comboBox.SelectedIndex = sourceIndex;
                            MainWindowSettingsHelper.InvokeComboBoxSelectionChanged("ComboBoxChickenSoupSource", comboBox.Items[sourceIndex]);
                        }
                        else
                        {
                            // 如果找不到控件，直接更新设置
                            MainWindowSettingsHelper.UpdateSettingDirectly(() =>
                            {
                                appearance.ChickenSoupSource = sourceIndex;
                            }, "ComboBoxChickenSoupSource");
                        }
                    }
                    break;

                case "UnFoldBtnImg":
                    int imgType;
                    switch (value)
                    {
                        case "Arrow":
                            imgType = 0;
                            break;
                        case "Pen":
                            imgType = 1;
                            break;
                        default:
                            imgType = 0;
                            break;
                    }
                    // 调用 MainWindow 中的方法（带主题检查）
                    var mainWindow3 = Application.Current.MainWindow as MainWindow;
                    if (mainWindow3 != null)
                    {
                        var comboBox = mainWindow3.FindName("ComboBoxUnFoldBtnImg") as System.Windows.Controls.ComboBox;
                        if (comboBox != null && comboBox.Items.Count > imgType)
                        {
                            comboBox.SelectedIndex = imgType;
                            MainWindowSettingsHelper.InvokeComboBoxSelectionChangedWithThemeCheck("ComboBoxUnFoldBtnImg", comboBox.Items[imgType]);
                        }
                        else
                        {
                            // 如果找不到控件，直接更新设置并通知主题更新
                            MainWindowSettingsHelper.UpdateSettingSafely(() =>
                            {
                                appearance.UnFoldButtonImageType = imgType;
                            }, "ComboBoxUnFoldBtnImg_SelectionChanged", "ComboBoxUnFoldBtnImg");
                            MainWindowSettingsHelper.NotifyThemeUpdateIfNeeded("ComboBoxUnFoldBtnImg");
                        }
                    }
                    break;

                case "QuickColorPaletteDisplayMode":
                    int displayMode;
                    switch (value)
                    {
                        case "Single":
                            displayMode = 0;
                            break;
                        case "Double":
                            displayMode = 1;
                            break;
                        default:
                            displayMode = 1;
                            break;
                    }
                    // 调用 MainWindow 中的方法
                    var mainWindow4 = Application.Current.MainWindow as MainWindow;
                    if (mainWindow4 != null)
                    {
                        var comboBox = mainWindow4.FindName("ComboBoxQuickColorPaletteDisplayMode") as System.Windows.Controls.ComboBox;
                        if (comboBox != null && comboBox.Items.Count > displayMode)
                        {
                            comboBox.SelectedIndex = displayMode;
                            MainWindowSettingsHelper.InvokeComboBoxSelectionChanged("ComboBoxQuickColorPaletteDisplayMode", comboBox.Items[displayMode]);
                        }
                        else
                        {
                            // 如果找不到控件，直接更新设置
                            MainWindowSettingsHelper.UpdateSettingDirectly(() =>
                            {
                                appearance.QuickColorPaletteDisplayMode = displayMode;
                            }, "ComboBoxQuickColorPaletteDisplayMode");
                        }
                    }
                    break;

                case "EraserDisplayOption":
                    int eraserOption;
                    switch (value)
                    {
                        case "Both":
                            eraserOption = 0;
                            break;
                        case "Area":
                            eraserOption = 1;
                            break;
                        case "Line":
                            eraserOption = 2;
                            break;
                        case "None":
                            eraserOption = 3;
                            break;
                        default:
                            eraserOption = 0;
                            break;
                    }
                    // 调用 MainWindow 中的方法
                    var mainWindow5 = Application.Current.MainWindow as MainWindow;
                    if (mainWindow5 != null)
                    {
                        var comboBox = mainWindow5.FindName("ComboBoxEraserDisplayOption") as System.Windows.Controls.ComboBox;
                        if (comboBox != null && comboBox.Items.Count > eraserOption)
                        {
                            comboBox.SelectedIndex = eraserOption;
                            MainWindowSettingsHelper.InvokeComboBoxSelectionChanged("ComboBoxEraserDisplayOption", comboBox.Items[eraserOption]);
                        }
                        else
                        {
                            // 如果找不到控件，直接更新设置
                            MainWindowSettingsHelper.UpdateSettingDirectly(() =>
                            {
                                appearance.EraserDisplayOption = eraserOption;
                            }, "ComboBoxEraserDisplayOption");
                        }
                    }
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
                System.Diagnostics.Debug.WriteLine($"ThemePanel 应用主题时出错: {ex.Message}");
            }
        }
    }
}

