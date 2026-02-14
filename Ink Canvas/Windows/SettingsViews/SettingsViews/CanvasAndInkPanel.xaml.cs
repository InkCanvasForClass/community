using Ink_Canvas;
using iNKORE.UI.WPF.Helpers;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.Windows.SettingsViews
{
    /// <summary>
    /// CanvasAndInkPanel.xaml 的交互逻辑
    /// </summary>
    public partial class CanvasAndInkPanel : UserControl
    {
        private bool _isLoaded = false;

        public CanvasAndInkPanel()
        {
            InitializeComponent();
            Loaded += CanvasAndInkPanel_Loaded;
        }

        private void CanvasAndInkPanel_Loaded(object sender, RoutedEventArgs e)
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
                System.Diagnostics.Debug.WriteLine($"CanvasAndInkPanel 启用触摸支持时出错: {ex.Message}");
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
            if (MainWindow.Settings == null || MainWindow.Settings.Canvas == null) return;

            _isLoaded = false;

            try
            {
                var canvas = MainWindow.Settings.Canvas;

                // 显示画笔光标
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchShowCursor"), canvas.IsShowCursor);

                // 启用压感触屏模式
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchEnablePressureTouchMode"), canvas.EnablePressureTouchMode);

                // 屏蔽压感
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchDisablePressure"), canvas.DisablePressure);

                // 板擦橡皮大小
                SetOptionButtonState("EraserSize", canvas.EraserSize);

                // 退出画板模式后隐藏墨迹
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchHideStrokeWhenSelecting"), canvas.HideStrokeWhenSelecting);

                // 清空墨迹时删除墨迹历史记录
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchClearCanvasAndClearTimeMachine"), canvas.ClearCanvasAndClearTimeMachine);

                // 清空画布时同时清空图片
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchClearCanvasAlsoClearImages"), canvas.ClearCanvasAlsoClearImages);

                // 插入图片时自动压缩
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchCompressPicturesUploaded"), canvas.IsCompressPicturesUploaded);

                // 保留双曲线渐近线
                SetOptionButtonState("HyperbolaAsymptote", (int)canvas.HyperbolaAsymptoteOption);

                // 绘制圆时显示圆心位置
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchShowCircleCenter"), canvas.ShowCircleCenter);

                // 使用WPF默认贝塞尔曲线平滑
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchFitToCurve"), canvas.FitToCurve && !canvas.UseAdvancedBezierSmoothing);

                // 使用高级贝塞尔曲线平滑
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchAdvancedBezierSmoothing"), canvas.UseAdvancedBezierSmoothing);

                // 启用异步墨迹平滑
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchUseAsyncInkSmoothing"), canvas.UseAsyncInkSmoothing);

                // 启用硬件加速
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchUseHardwareAcceleration"), canvas.UseHardwareAcceleration);

                // 启用直线自动拉直
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchAutoStraightenLine"), canvas.AutoStraightenLine);

                // 启用高精度直线拉直
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchHighPrecisionLineStraighten"), canvas.HighPrecisionLineStraighten);

                // 启用直线端点吸附
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchLineEndpointSnapping"), canvas.LineEndpointSnapping);

                // 启用墨迹渐隐功能
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchEnableInkFade"), canvas.EnableInkFade);
                if (InkFadeTimePanel != null)
                {
                    InkFadeTimePanel.Visibility = canvas.EnableInkFade ? Visibility.Visible : Visibility.Collapsed;
                }
                if (InkFadeTimeSlider != null)
                {
                    InkFadeTimeSlider.Value = canvas.InkFadeTime;
                }

                // 定时自动保存墨迹
                // 注意：这个设置可能在 Automation 或 Canvas 中，需要根据实际情况调整
                // SetToggleSwitchState(FindToggleSwitch("ToggleSwitchEnableAutoSaveStrokes"), ...);

                // 墨迹全页面保存
                // SetToggleSwitchState(FindToggleSwitch("ToggleSwitchSaveFullPageStrokes"), ...);

                // 保存为XML格式
                // SetToggleSwitchState(FindToggleSwitch("ToggleSwitchSaveStrokesAsXML"), ...);

                // 自动保存幻灯片墨迹
                if (MainWindow.Settings.PowerPointSettings != null)
                {
                    SetToggleSwitchState(FindToggleSwitch("ToggleSwitchAutoSaveStrokesInPowerPoint"), 
                        MainWindow.Settings.PowerPointSettings.IsAutoSaveStrokesInPowerPoint);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载画板和墨迹设置时出错: {ex.Message}");
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
            var buttons = new[] { "VerySmall", "Small", "Medium", "Large", "VeryLarge" };
            var hyperbolaButtons = new[] { "Yes", "No", "Ask" };
            
            string[] buttonNames = group == "EraserSize" ? buttons : hyperbolaButtons;
            
            bool isDarkTheme = ThemeHelper.IsDarkTheme;
            var selectedBrush = isDarkTheme ? new SolidColorBrush(Color.FromRgb(25, 25, 25)) : new SolidColorBrush(Color.FromRgb(225, 225, 225));
            var unselectedBrush = new SolidColorBrush(Colors.Transparent);

            for (int i = 0; i < buttonNames.Length && i <= selectedIndex; i++)
            {
                var button = this.FindDescendantByName($"{group}{buttonNames[i]}") as Border;
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
            if (MainWindow.Settings?.Canvas == null) return false;

            try
            {
                var canvas = MainWindow.Settings.Canvas;
                switch (tag)
                {
                    case "ShowCursor":
                        return canvas.IsShowCursor;
                    case "EnablePressureTouchMode":
                        return canvas.EnablePressureTouchMode;
                    case "DisablePressure":
                        return canvas.DisablePressure;
                    case "HideStrokeWhenSelecting":
                        return canvas.HideStrokeWhenSelecting;
                    case "ClearCanvasAndClearTimeMachine":
                        return canvas.ClearCanvasAndClearTimeMachine;
                    case "ClearCanvasAlsoClearImages":
                        return canvas.ClearCanvasAlsoClearImages;
                    case "CompressPicturesUploaded":
                        return canvas.IsCompressPicturesUploaded;
                    case "ShowCircleCenter":
                        return canvas.ShowCircleCenter;
                    case "FitToCurve":
                        return canvas.FitToCurve && !canvas.UseAdvancedBezierSmoothing;
                    case "AdvancedBezierSmoothing":
                        return canvas.UseAdvancedBezierSmoothing;
                    case "UseAsyncInkSmoothing":
                        return canvas.UseAsyncInkSmoothing;
                    case "UseHardwareAcceleration":
                        return canvas.UseHardwareAcceleration;
                    case "AutoStraightenLine":
                        return canvas.AutoStraightenLine;
                    case "HighPrecisionLineStraighten":
                        return canvas.HighPrecisionLineStraighten;
                    case "LineEndpointSnapping":
                        return canvas.LineEndpointSnapping;
                    case "EnableInkFade":
                        return canvas.EnableInkFade;
                    case "HideInkFadeControlInPenMenu":
                        return canvas.HideInkFadeControlInPenMenu;
                    case "EnableAutoSaveStrokes":
                        return MainWindow.Settings.Automation?.IsEnableAutoSaveStrokes ?? false;
                    case "SaveFullPageStrokes":
                        return MainWindow.Settings.Automation?.IsSaveFullPageStrokes ?? false;
                    case "SaveStrokesAsXML":
                        return MainWindow.Settings.Automation?.IsSaveStrokesAsXML ?? false;
                    case "AutoSaveStrokesInPowerPoint":
                        return MainWindow.Settings.PowerPointSettings?.IsAutoSaveStrokesInPowerPoint ?? false;
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

            var canvas = MainWindow.Settings.Canvas;
            if (canvas == null) return;

            switch (tag)
            {
                case "ShowCursor":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchShowCursor", newState);
                    break;

                case "EnablePressureTouchMode":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchEnablePressureTouchMode", newState);
                    // 处理互斥逻辑
                    if (newState && canvas.DisablePressure)
                    {
                        MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchDisablePressure", false);
                        SetToggleSwitchState(FindToggleSwitch("ToggleSwitchDisablePressure"), false);
                    }
                    break;

                case "DisablePressure":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchDisablePressure", newState);
                    // 处理互斥逻辑
                    if (newState && canvas.EnablePressureTouchMode)
                    {
                        MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchEnablePressureTouchMode", false);
                        SetToggleSwitchState(FindToggleSwitch("ToggleSwitchEnablePressureTouchMode"), false);
                    }
                    break;

                case "HideStrokeWhenSelecting":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchHideStrokeWhenSelecting", newState);
                    break;

                case "ClearCanvasAndClearTimeMachine":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchClearCanvasAndClearTimeMachine", newState);
                    break;

                case "ClearCanvasAlsoClearImages":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchClearCanvasAlsoClearImages", newState);
                    break;

                case "CompressPicturesUploaded":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchCompressPicturesUploaded", newState);
                    break;

                case "ShowCircleCenter":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchShowCircleCenter", newState);
                    break;

                case "FitToCurve":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchFitToCurve", newState);
                    // 处理互斥逻辑
                    if (newState)
                    {
                        MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchAdvancedBezierSmoothing", false);
                        SetToggleSwitchState(FindToggleSwitch("ToggleSwitchAdvancedBezierSmoothing"), false);
                    }
                    break;

                case "AdvancedBezierSmoothing":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchAdvancedBezierSmoothing", newState);
                    // 处理互斥逻辑
                    if (newState)
                    {
                        MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchFitToCurve", false);
                        SetToggleSwitchState(FindToggleSwitch("ToggleSwitchFitToCurve"), false);
                    }
                    break;

                case "UseAsyncInkSmoothing":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchAsyncInkSmoothing", newState);
                    break;

                case "UseHardwareAcceleration":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchHardwareAcceleration", newState);
                    break;

                case "AutoStraightenLine":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchAutoStraightenLine", newState);
                    break;

                case "HighPrecisionLineStraighten":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchHighPrecisionLineStraighten", newState);
                    break;

                case "LineEndpointSnapping":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchLineEndpointSnapping", newState);
                    break;

                case "EnableInkFade":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchEnableInkFade", newState);
                    // 更新UI状态
                    if (InkFadeTimePanel != null)
                    {
                        InkFadeTimePanel.Visibility = newState ? Visibility.Visible : Visibility.Collapsed;
                    }
                    break;

                case "HideInkFadeControlInPenMenu":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchHideInkFadeControlInPenMenu", newState);
                    break;

                case "EnableAutoSaveStrokes":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchEnableAutoSaveStrokes", newState);
                    break;

                case "SaveFullPageStrokes":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchSaveFullPageStrokes", newState);
                    break;

                case "SaveStrokesAsXML":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchSaveStrokesAsXML", newState);
                    break;

                case "AutoSaveStrokesInPowerPoint":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchAutoSaveStrokesInPowerPoint", newState);
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

            var canvas = MainWindow.Settings.Canvas;
            if (canvas == null) return;

            switch (group)
            {
                case "EraserSize":
                    int eraserSize;
                    switch (value)
                    {
                        case "VerySmall":
                            eraserSize = 0;
                            break;
                        case "Small":
                            eraserSize = 1;
                            break;
                        case "Medium":
                            eraserSize = 2;
                            break;
                        case "Large":
                            eraserSize = 3;
                            break;
                        case "VeryLarge":
                            eraserSize = 4;
                            break;
                        default:
                            eraserSize = 2;
                            break;
                    }
                    // 调用 MainWindow 中的方法
                    var mainWindow = Application.Current.MainWindow as MainWindow;
                    if (mainWindow != null)
                    {
                        var comboBox = mainWindow.FindName("ComboBoxEraserSize") as System.Windows.Controls.ComboBox;
                        if (comboBox != null && comboBox.Items.Count > eraserSize)
                        {
                            comboBox.SelectedIndex = eraserSize;
                            MainWindowSettingsHelper.InvokeComboBoxSelectionChanged("ComboBoxEraserSize", comboBox.Items[eraserSize]);
                        }
                        else
                        {
                            // 如果找不到控件，直接更新设置
                            MainWindowSettingsHelper.UpdateSettingDirectly(() =>
                            {
                                canvas.EraserSize = eraserSize;
                            }, "ComboBoxEraserSize");
                        }
                    }
                    break;

                case "HyperbolaAsymptote":
                    OptionalOperation option;
                    switch (value)
                    {
                        case "Yes":
                            option = OptionalOperation.Yes;
                            break;
                        case "No":
                            option = OptionalOperation.No;
                            break;
                        case "Ask":
                            option = OptionalOperation.Ask;
                            break;
                        default:
                            option = OptionalOperation.Ask;
                            break;
                    }
                    // 调用 MainWindow 中的方法
                    var mainWindow2 = Application.Current.MainWindow as MainWindow;
                    if (mainWindow2 != null)
                    {
                        var comboBox = mainWindow2.FindName("ComboBoxHyperbolaAsymptoteOption") as System.Windows.Controls.ComboBox;
                        if (comboBox != null)
                        {
                            int optionIndex = (int)option;
                            if (comboBox.Items.Count > optionIndex)
                            {
                                comboBox.SelectedIndex = optionIndex;
                                MainWindowSettingsHelper.InvokeComboBoxSelectionChanged("ComboBoxHyperbolaAsymptoteOption", comboBox.Items[optionIndex]);
                            }
                        }
                        else
                        {
                            // 如果找不到控件，直接更新设置
                            MainWindowSettingsHelper.UpdateSettingDirectly(() =>
                            {
                                canvas.HyperbolaAsymptoteOption = option;
                            }, "ComboBoxHyperbolaAsymptoteOption");
                        }
                    }
                    break;

                case "AutoSaveStrokesInterval":
                    // 调用 MainWindow 中的方法
                    int interval = int.Parse(value);
                    var mainWindow3 = Application.Current.MainWindow as MainWindow;
                    if (mainWindow3 != null)
                    {
                        var comboBox = mainWindow3.FindName("ComboBoxAutoSaveStrokesInterval") as System.Windows.Controls.ComboBox;
                        if (comboBox != null)
                        {
                            // 查找对应的选项（根据 Tag 或 Content 匹配）
                            foreach (System.Windows.Controls.ComboBoxItem item in comboBox.Items)
                            {
                                if (item.Tag != null && int.TryParse(item.Tag.ToString(), out int tagValue) && tagValue == interval)
                                {
                                    comboBox.SelectedItem = item;
                                    MainWindowSettingsHelper.InvokeComboBoxSelectionChanged("ComboBoxAutoSaveStrokesInterval", item);
                                    break;
                                }
                                else if (item.Content != null && item.Content.ToString().Contains(interval.ToString()))
                                {
                                    comboBox.SelectedItem = item;
                                    MainWindowSettingsHelper.InvokeComboBoxSelectionChanged("ComboBoxAutoSaveStrokesInterval", item);
                                    break;
                                }
                            }
                        }
                        else
                        {
                            // 如果找不到控件，直接更新设置
                            MainWindowSettingsHelper.UpdateSettingDirectly(() =>
                            {
                                if (MainWindow.Settings.Automation != null)
                                {
                                    MainWindow.Settings.Automation.AutoSaveStrokesIntervalMinutes = interval;
                                }
                            }, "ComboBoxAutoSaveStrokesInterval");
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// 自动保存间隔选项按钮点击事件处理
        /// </summary>
        private void AutoSaveIntervalButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            OptionButton_Click(sender, e);
        }

        /// <summary>
        /// Slider值变化事件处理
        /// </summary>
        private void InkFadeTimeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isLoaded) return;
            if (InkFadeTimeSlider != null && InkFadeTimeText != null)
            {
                double value = InkFadeTimeSlider.Value;
                InkFadeTimeText.Text = $"{(int)value}ms";
                // 调用 MainWindow 中的方法
                MainWindowSettingsHelper.InvokeSliderValueChanged("InkFadeTimeSlider", value);
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
                System.Diagnostics.Debug.WriteLine($"CanvasAndInkPanel 应用主题时出错: {ex.Message}");
            }
        }
    }
}

