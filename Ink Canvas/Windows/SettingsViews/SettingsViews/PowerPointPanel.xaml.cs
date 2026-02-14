using Ink_Canvas;
using iNKORE.UI.WPF.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Ink_Canvas.Windows.SettingsViews
{
    /// <summary>
    /// PowerPointPanel.xaml 的交互逻辑
    /// </summary>
    public partial class PowerPointPanel : UserControl
    {
        private bool _isLoaded = false;

        public PowerPointPanel()
        {
            InitializeComponent();
            Loaded += PowerPointPanel_Loaded;
        }

        private void PowerPointPanel_Loaded(object sender, RoutedEventArgs e)
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
                System.Diagnostics.Debug.WriteLine($"PowerPointPanel 启用触摸支持时出错: {ex.Message}");
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
            if (MainWindow.Settings == null || MainWindow.Settings.PowerPointSettings == null) return;

            _isLoaded = false;

            try
            {
                var pptSettings = MainWindow.Settings.PowerPointSettings;

                // Microsoft PowerPoint 支持
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchSupportPowerPoint"), pptSettings.PowerPointSupport);

                // PowerPoint 联动增强
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchPowerPointEnhancement"), pptSettings.EnablePowerPointEnhancement);

                // WPS 支持
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchSupportWPS"), pptSettings.IsSupportWPS);

                // WPP进程查杀
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchEnableWppProcessKill"), pptSettings.EnableWppProcessKill);

                // 在 PPT 模式下显示翻页按钮
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchShowPPTButton"), pptSettings.ShowPPTButton);
                if (PPTButtonSettingsPanel != null)
                {
                    PPTButtonSettingsPanel.Visibility = pptSettings.ShowPPTButton ? Visibility.Visible : Visibility.Collapsed;
                }

                // PPT按钮显示选项
                var dops = pptSettings.PPTButtonsDisplayOption.ToString();
                var dopsc = dops.ToCharArray();
                if (dopsc.Length >= 4)
                {
                    if (CheckboxEnableLBPPTButton != null) CheckboxEnableLBPPTButton.IsChecked = dopsc[0] == '2';
                    if (CheckboxEnableRBPPTButton != null) CheckboxEnableRBPPTButton.IsChecked = dopsc[1] == '2';
                    if (CheckboxEnableLSPPTButton != null) CheckboxEnableLSPPTButton.IsChecked = dopsc[2] == '2';
                    if (CheckboxEnableRSPPTButton != null) CheckboxEnableRSPPTButton.IsChecked = dopsc[3] == '2';
                }

                // 按钮位置
                if (PPTButtonLeftPositionValueSlider != null)
                {
                    PPTButtonLeftPositionValueSlider.Value = pptSettings.PPTLSButtonPosition;
                    if (PPTButtonLeftPositionValueText != null)
                    {
                        PPTButtonLeftPositionValueText.Text = pptSettings.PPTLSButtonPosition.ToString();
                    }
                }
                if (PPTButtonRightPositionValueSlider != null)
                {
                    PPTButtonRightPositionValueSlider.Value = pptSettings.PPTRSButtonPosition;
                    if (PPTButtonRightPositionValueText != null)
                    {
                        PPTButtonRightPositionValueText.Text = pptSettings.PPTRSButtonPosition.ToString();
                    }
                }
                if (PPTButtonLBPositionValueSlider != null)
                {
                    PPTButtonLBPositionValueSlider.Value = pptSettings.PPTLBButtonPosition;
                    if (PPTButtonLBPositionValueText != null)
                    {
                        PPTButtonLBPositionValueText.Text = pptSettings.PPTLBButtonPosition.ToString();
                    }
                }
                if (PPTButtonRBPositionValueSlider != null)
                {
                    PPTButtonRBPositionValueSlider.Value = pptSettings.PPTRBButtonPosition;
                    if (PPTButtonRBPositionValueText != null)
                    {
                        PPTButtonRBPositionValueText.Text = pptSettings.PPTRBButtonPosition.ToString();
                    }
                }

                // 两侧按钮选项
                var sops = pptSettings.PPTSButtonsOption.ToString();
                var sopsc = sops.ToCharArray();
                if (sopsc.Length >= 3)
                {
                    if (CheckboxSPPTDisplayPage != null) CheckboxSPPTDisplayPage.IsChecked = sopsc[0] == '2';
                    if (CheckboxSPPTHalfOpacity != null) CheckboxSPPTHalfOpacity.IsChecked = sopsc[1] == '2';
                    if (CheckboxSPPTBlackBackground != null) CheckboxSPPTBlackBackground.IsChecked = sopsc[2] == '2';
                }

                // 左下右下按钮选项
                var bops = pptSettings.PPTBButtonsOption.ToString();
                var bopsc = bops.ToCharArray();
                if (bopsc.Length >= 3)
                {
                    if (CheckboxBPPTDisplayPage != null) CheckboxBPPTDisplayPage.IsChecked = bopsc[0] == '2';
                    if (CheckboxBPPTHalfOpacity != null) CheckboxBPPTHalfOpacity.IsChecked = bopsc[1] == '2';
                    if (CheckboxBPPTBlackBackground != null) CheckboxBPPTBlackBackground.IsChecked = bopsc[2] == '2';
                }

                // 按钮透明度
                if (PPTLSButtonOpacityValueSlider != null)
                {
                    PPTLSButtonOpacityValueSlider.Value = pptSettings.PPTLSButtonOpacity > 0 ? pptSettings.PPTLSButtonOpacity : 0.5;
                    if (PPTLSButtonOpacityValueText != null)
                    {
                        PPTLSButtonOpacityValueText.Text = PPTLSButtonOpacityValueSlider.Value.ToString("F1");
                    }
                }
                if (PPTRSButtonOpacityValueSlider != null)
                {
                    PPTRSButtonOpacityValueSlider.Value = pptSettings.PPTRSButtonOpacity > 0 ? pptSettings.PPTRSButtonOpacity : 0.5;
                    if (PPTRSButtonOpacityValueText != null)
                    {
                        PPTRSButtonOpacityValueText.Text = PPTRSButtonOpacityValueSlider.Value.ToString("F1");
                    }
                }
                if (PPTLBButtonOpacityValueSlider != null)
                {
                    PPTLBButtonOpacityValueSlider.Value = pptSettings.PPTLBButtonOpacity > 0 ? pptSettings.PPTLBButtonOpacity : 0.5;
                    if (PPTLBButtonOpacityValueText != null)
                    {
                        PPTLBButtonOpacityValueText.Text = PPTLBButtonOpacityValueSlider.Value.ToString("F1");
                    }
                }
                if (PPTRBButtonOpacityValueSlider != null)
                {
                    PPTRBButtonOpacityValueSlider.Value = pptSettings.PPTRBButtonOpacity > 0 ? pptSettings.PPTRBButtonOpacity : 0.5;
                    if (PPTRBButtonOpacityValueText != null)
                    {
                        PPTRBButtonOpacityValueText.Text = PPTRBButtonOpacityValueSlider.Value.ToString("F1");
                    }
                }

                // PPT 页码按钮可点击
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchEnablePPTButtonPageClickable"), pptSettings.EnablePPTButtonPageClickable);

                // PPT 翻页按钮长按翻页
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchEnablePPTButtonLongPressPageTurn"), pptSettings.EnablePPTButtonLongPressPageTurn);

                // 点击翻页时跳过转场动画
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchSkipAnimationsWhenGoNext"), pptSettings.SkipAnimationsWhenGoNext);

                // 进入 PPT 放映时自动进入批注模式
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchShowCanvasAtNewSlideShow"), pptSettings.IsShowCanvasAtNewSlideShow);

                // 允许幻灯片模式下的双指手势
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchEnableTwoFingerGestureInPresentationMode"), pptSettings.IsEnableTwoFingerGestureInPresentationMode);

                // 允许使用手指手势进行幻灯片翻页
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchEnableFingerGestureSlideShowControl"), pptSettings.IsEnableFingerGestureSlideShowControl);

                // PPT 放映模式显示手势按钮
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchShowGestureButtonInSlideShow"), pptSettings.ShowGestureButtonInSlideShow);

                // PPT时间显示胶囊
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchEnablePPTTimeCapsule"), pptSettings.EnablePPTTimeCapsule);

                // 时间胶囊位置
                SetOptionButtonState("PPTTimeCapsulePosition", pptSettings.PPTTimeCapsulePosition);

                // 记忆并提示上次播放位置
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchNotifyPreviousPage"), pptSettings.IsNotifyPreviousPage);

                // 进入放映时回到首页
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchAlwaysGoToFirstPageOnReenter"), pptSettings.IsAlwaysGoToFirstPageOnReenter);

                // 提示隐藏幻灯片
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchNotifyHiddenPage"), pptSettings.IsNotifyHiddenPage);

                // 提示是否已启用自动播放
                SetToggleSwitchState(FindToggleSwitch("ToggleSwitchNotifyAutoPlayPresentation"), pptSettings.IsNotifyAutoPlayPresentation);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载PowerPoint设置时出错: {ex.Message}");
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
                { "PPTTimeCapsulePosition", new[] { "TopLeft", "TopRight", "TopCenter" } }
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
            if (MainWindow.Settings?.PowerPointSettings == null) return false;

            try
            {
                var pptSettings = MainWindow.Settings.PowerPointSettings;
                switch (tag)
                {
                    case "SupportPowerPoint":
                        return pptSettings.PowerPointSupport;
                    case "PowerPointEnhancement":
                        return pptSettings.EnablePowerPointEnhancement;
                    case "SupportWPS":
                        return pptSettings.IsSupportWPS;
                    case "EnableWppProcessKill":
                        return pptSettings.EnableWppProcessKill;
                    case "ShowPPTButton":
                        return pptSettings.ShowPPTButton;
                    case "EnablePPTButtonPageClickable":
                        return pptSettings.EnablePPTButtonPageClickable;
                    case "EnablePPTButtonLongPressPageTurn":
                        return pptSettings.EnablePPTButtonLongPressPageTurn;
                    case "SkipAnimationsWhenGoNext":
                        return pptSettings.SkipAnimationsWhenGoNext;
                    case "ShowCanvasAtNewSlideShow":
                        return pptSettings.IsShowCanvasAtNewSlideShow;
                    case "EnableTwoFingerGestureInPresentationMode":
                        return pptSettings.IsEnableTwoFingerGestureInPresentationMode;
                    case "EnableFingerGestureSlideShowControl":
                        return pptSettings.IsEnableFingerGestureSlideShowControl;
                    case "ShowGestureButtonInSlideShow":
                        return pptSettings.ShowGestureButtonInSlideShow;
                    case "EnablePPTTimeCapsule":
                        return pptSettings.EnablePPTTimeCapsule;
                    case "NotifyPreviousPage":
                        return pptSettings.IsNotifyPreviousPage;
                    case "AlwaysGoToFirstPageOnReenter":
                        return pptSettings.IsAlwaysGoToFirstPageOnReenter;
                    case "NotifyHiddenPage":
                        return pptSettings.IsNotifyHiddenPage;
                    case "NotifyAutoPlayPresentation":
                        return pptSettings.IsNotifyAutoPlayPresentation;
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

            var pptSettings = MainWindow.Settings.PowerPointSettings;
            if (pptSettings == null) return;

            switch (tag)
            {
                case "SupportPowerPoint":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchSupportPowerPoint", newState);
                    break;

                case "PowerPointEnhancement":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchPowerPointEnhancement", newState);
                    break;

                case "SupportWPS":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchSupportWPS", newState);
                    break;

                case "EnableWppProcessKill":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchEnableWppProcessKill", newState);
                    break;

                case "ShowPPTButton":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchShowPPTButton", newState);
                    // 更新UI状态
                    if (PPTButtonSettingsPanel != null)
                    {
                        PPTButtonSettingsPanel.Visibility = newState ? Visibility.Visible : Visibility.Collapsed;
                    }
                    break;

                case "EnablePPTButtonPageClickable":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchEnablePPTButtonPageClickable", newState);
                    break;

                case "EnablePPTButtonLongPressPageTurn":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchEnablePPTButtonLongPressPageTurn", newState);
                    break;

                case "SkipAnimationsWhenGoNext":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchSkipAnimationsWhenGoNext", newState);
                    break;

                case "ShowCanvasAtNewSlideShow":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchShowCanvasAtNewSlideShow", newState);
                    break;

                case "EnableTwoFingerGestureInPresentationMode":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchEnableTwoFingerGestureInPresentationMode", newState);
                    break;

                case "EnableFingerGestureSlideShowControl":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchEnableFingerGestureSlideShowControl", newState);
                    break;

                case "ShowGestureButtonInSlideShow":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchShowGestureButtonInSlideShow", newState);
                    break;

                case "EnablePPTTimeCapsule":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchEnablePPTTimeCapsule", newState);
                    break;

                case "NotifyPreviousPage":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchNotifyPreviousPage", newState);
                    break;

                case "AlwaysGoToFirstPageOnReenter":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchAlwaysGoToFirstPageOnReenter", newState);
                    break;

                case "NotifyHiddenPage":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchNotifyHiddenPage", newState);
                    break;

                case "NotifyAutoPlayPresentation":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchNotifyAutoPlayPresentation", newState);
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

            var pptSettings = MainWindow.Settings.PowerPointSettings;
            if (pptSettings == null) return;

            switch (group)
            {
                case "PPTTimeCapsulePosition":
                    int position;
                    switch (value)
                    {
                        case "TopLeft":
                            position = 0;
                            break;
                        case "TopRight":
                            position = 1;
                            break;
                        case "TopCenter":
                            position = 2;
                            break;
                        default:
                            position = 1;
                            break;
                    }
                    MainWindowSettingsHelper.UpdateSettingDirectly(() =>
                    {
                        pptSettings.PPTTimeCapsulePosition = position;
                    }, "PPTTimeCapsulePosition");
                    break;
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
            var pptSettings = MainWindow.Settings.PowerPointSettings;
            if (pptSettings == null) return;

            // 调用 MainWindow 中的方法
            MainWindowSettingsHelper.InvokeCheckBoxCheckedChanged(name, checkBox.IsChecked ?? false);
        }


        /// <summary>
        /// Slider值变化事件处理
        /// </summary>
        private void PPTButtonPositionSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isLoaded) return;

            var slider = sender as Slider;
            if (slider == null) return;

            string name = slider.Name;
            double value = slider.Value;

            // 更新文本显示
            switch (name)
            {
                case "PPTButtonLeftPositionValueSlider":
                    if (PPTButtonLeftPositionValueText != null)
                    {
                        PPTButtonLeftPositionValueText.Text = ((int)value).ToString();
                    }
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeSliderValueChanged("PPTButtonLeftPositionValueSlider", value);
                    break;

                case "PPTButtonRightPositionValueSlider":
                    if (PPTButtonRightPositionValueText != null)
                    {
                        PPTButtonRightPositionValueText.Text = ((int)value).ToString();
                    }
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeSliderValueChanged("PPTButtonRightPositionValueSlider", value);
                    break;

                case "PPTButtonLBPositionValueSlider":
                    if (PPTButtonLBPositionValueText != null)
                    {
                        PPTButtonLBPositionValueText.Text = ((int)value).ToString();
                    }
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeSliderValueChanged("PPTButtonLBPositionValueSlider", value);
                    break;

                case "PPTButtonRBPositionValueSlider":
                    if (PPTButtonRBPositionValueText != null)
                    {
                        PPTButtonRBPositionValueText.Text = ((int)value).ToString();
                    }
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeSliderValueChanged("PPTButtonRBPositionValueSlider", value);
                    break;
            }
        }

        /// <summary>
        /// 按钮透明度Slider值变化事件处理
        /// </summary>
        private void PPTButtonOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isLoaded) return;

            var slider = sender as Slider;
            if (slider == null) return;

            string name = slider.Name;
            double value = slider.Value;

            // 更新文本显示
            switch (name)
            {
                case "PPTLSButtonOpacityValueSlider":
                    if (PPTLSButtonOpacityValueText != null)
                    {
                        PPTLSButtonOpacityValueText.Text = value.ToString("F1");
                    }
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeSliderValueChanged("PPTLSButtonOpacityValueSlider", value);
                    break;

                case "PPTRSButtonOpacityValueSlider":
                    if (PPTRSButtonOpacityValueText != null)
                    {
                        PPTRSButtonOpacityValueText.Text = value.ToString("F1");
                    }
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeSliderValueChanged("PPTRSButtonOpacityValueSlider", value);
                    break;

                case "PPTLBButtonOpacityValueSlider":
                    if (PPTLBButtonOpacityValueText != null)
                    {
                        PPTLBButtonOpacityValueText.Text = value.ToString("F1");
                    }
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeSliderValueChanged("PPTLBButtonOpacityValueSlider", value);
                    break;

                case "PPTRBButtonOpacityValueSlider":
                    if (PPTRBButtonOpacityValueText != null)
                    {
                        PPTRBButtonOpacityValueText.Text = value.ToString("F1");
                    }
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeSliderValueChanged("PPTRBButtonOpacityValueSlider", value);
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
                System.Diagnostics.Debug.WriteLine($"PowerPointPanel 应用主题时出错: {ex.Message}");
            }
        }
    }
}
