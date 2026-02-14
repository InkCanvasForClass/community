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
    /// GesturesPanel.xaml 的交互逻辑
    /// </summary>
    public partial class GesturesPanel : UserControl
    {
        private bool _isLoaded = false;

        public GesturesPanel()
        {
            InitializeComponent();
            Loaded += GesturesPanel_Loaded;
        }

        private void GesturesPanel_Loaded(object sender, RoutedEventArgs e)
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
                System.Diagnostics.Debug.WriteLine($"GesturesPanel 启用触摸支持时出错: {ex.Message}");
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
            if (MainWindow.Settings == null) return;

            _isLoaded = false;

            try
            {
                // 进退白板模式自动开关双指移动功能
                if (MainWindow.Settings.Gesture != null)
                {
                    SetToggleSwitchState(FindToggleSwitch("ToggleSwitchAutoSwitchTwoFingerGesture"), MainWindow.Settings.Gesture.AutoSwitchTwoFingerGesture);
                    SetToggleSwitchState(FindToggleSwitch("ToggleSwitchEnableTwoFingerRotationOnSelection"), MainWindow.Settings.Gesture.IsEnableTwoFingerRotationOnSelection);
                }

                // 启用手掌擦
                if (MainWindow.Settings.Canvas != null)
                {
                    SetToggleSwitchState(FindToggleSwitch("ToggleSwitchEnablePalmEraser"), MainWindow.Settings.Canvas.EnablePalmEraser);
                    if (PalmEraserSensitivityPanel != null)
                    {
                        PalmEraserSensitivityPanel.Visibility = MainWindow.Settings.Canvas.EnablePalmEraser ? Visibility.Visible : Visibility.Collapsed;
                    }

                    // 手掌擦敏感度
                    SetOptionButtonState("PalmEraserSensitivity", MainWindow.Settings.Canvas.PalmEraserSensitivity);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载手势操作设置时出错: {ex.Message}");
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
                { "PalmEraserSensitivity", new[] { "Low", "Medium", "High" } }
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
            if (MainWindow.Settings == null) return false;

            try
            {
                switch (tag)
                {
                    case "AutoSwitchTwoFingerGesture":
                        return MainWindow.Settings.Gesture?.AutoSwitchTwoFingerGesture ?? false;
                    case "EnableTwoFingerRotationOnSelection":
                        return MainWindow.Settings.Gesture?.IsEnableTwoFingerRotationOnSelection ?? false;
                    case "EnablePalmEraser":
                        return MainWindow.Settings.Canvas?.EnablePalmEraser ?? false;
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
                case "AutoSwitchTwoFingerGesture":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchAutoSwitchTwoFingerGesture", newState);
                    break;

                case "EnableTwoFingerRotationOnSelection":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchEnableTwoFingerRotationOnSelection", newState);
                    break;

                case "EnablePalmEraser":
                    // 调用 MainWindow 中的方法
                    MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchEnablePalmEraser", newState);
                    // 更新UI状态
                    if (PalmEraserSensitivityPanel != null)
                    {
                        PalmEraserSensitivityPanel.Visibility = newState ? Visibility.Visible : Visibility.Collapsed;
                    }
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

            switch (group)
            {
                case "PalmEraserSensitivity":
                    int sensitivity;
                    switch (value)
                    {
                        case "Low":
                            sensitivity = 0;
                            break;
                        case "Medium":
                            sensitivity = 1;
                            break;
                        case "High":
                            sensitivity = 2;
                            break;
                        default:
                            sensitivity = 0;
                            break;
                    }
                    // 调用 MainWindow 中的方法（通过设置 ComboBox 的 SelectedIndex）
                    var mainWindow = Application.Current.MainWindow as MainWindow;
                    if (mainWindow != null)
                    {
                        var comboBox = mainWindow.FindName("ComboBoxPalmEraserSensitivity") as System.Windows.Controls.ComboBox;
                        if (comboBox != null && comboBox.Items.Count > sensitivity)
                        {
                            comboBox.SelectedIndex = sensitivity;
                            // 触发 SelectionChanged 事件
                            MainWindowSettingsHelper.InvokeComboBoxSelectionChanged("ComboBoxPalmEraserSensitivity", comboBox.Items[sensitivity]);
                        }
                        else
                        {
                            // 如果找不到控件，直接更新设置
                            MainWindowSettingsHelper.UpdateSettingDirectly(() =>
                            {
                                if (MainWindow.Settings.Canvas != null)
                                {
                                    MainWindow.Settings.Canvas.PalmEraserSensitivity = sensitivity;
                                }
                            }, "ComboBoxPalmEraserSensitivity");
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
                System.Diagnostics.Debug.WriteLine($"GesturesPanel 应用主题时出错: {ex.Message}");
            }
        }
    }
}

