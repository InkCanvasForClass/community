using Ink_Canvas;
using iNKORE.UI.WPF.Helpers;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.Windows.SettingsViews
{
    /// <summary>
    /// 设置面板基类，提供通用的辅助方法
    /// </summary>
    public abstract class SettingsPanelBase : UserControl
    {
        protected bool _isLoaded = false;

        public SettingsPanelBase()
        {
            Loaded += SettingsPanelBase_Loaded;
        }

        private void SettingsPanelBase_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
            // 添加触摸支持
            EnableTouchSupport();
            // 应用主题（如果面板有 ApplyTheme 方法）
            try
            {
                var applyThemeMethod = this.GetType().GetMethod("ApplyTheme",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (applyThemeMethod != null)
                {
                    applyThemeMethod.Invoke(this, null);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SettingsPanelBase 应用主题时出错: {ex.Message}");
            }
            _isLoaded = true;
        }

        /// <summary>
        /// 为面板中的所有交互控件启用触摸支持
        /// </summary>
        protected virtual void EnableTouchSupport()
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
                System.Diagnostics.Debug.WriteLine($"启用触摸支持时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 为控件树中的所有交互控件启用触摸支持
        /// </summary>
        private void EnableTouchSupportForControls(DependencyObject parent)
        {
            if (parent == null) return;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                // 为 Border 控件（ToggleSwitch、选项按钮等）启用触摸支持
                if (child is Border border)
                {
                    // 检查是否是交互控件（有 Tag 或 Cursor 为 Hand）
                    if (border.Tag != null || border.Cursor == Cursors.Hand)
                    {
                        border.IsManipulationEnabled = true;
                        
                        // 添加触摸事件支持，将触摸事件转换为鼠标事件
                        border.TouchDown += Border_TouchDown;
                        border.PreviewTouchDown += Border_PreviewTouchDown;
                    }
                }
                // 为 Button 控件启用触摸支持
                else if (child is Button button)
                {
                    button.IsManipulationEnabled = true;
                }
                // 为 ComboBox 启用触摸支持
                else if (child is ComboBox comboBox)
                {
                    comboBox.IsManipulationEnabled = true;
                }
                // 为 Slider 启用触摸支持
                else if (child is Slider slider)
                {
                    slider.IsManipulationEnabled = true;
                }
                // 为 TextBox 启用触摸支持
                else if (child is TextBox textBox)
                {
                    textBox.IsManipulationEnabled = true;
                }
                
                // 递归处理子元素
                EnableTouchSupportForControls(child);
            }
        }

        /// <summary>
        /// Border 触摸按下事件处理
        /// </summary>
        private void Border_TouchDown(object sender, TouchEventArgs e)
        {
            var border = sender as Border;
            if (border == null) return;

            // 获取触摸点位置
            var touchPoint = e.GetTouchPoint(border);
            
            // 创建模拟的鼠标事件
            var mouseButtonEventArgs = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            {
                RoutedEvent = UIElement.MouseLeftButtonDownEvent,
                Source = border
            };
            
            // 触发鼠标按下事件
            border.RaiseEvent(mouseButtonEventArgs);
            
            // 捕获触摸设备
            border.CaptureTouch(e.TouchDevice);
            e.Handled = true;
        }

        /// <summary>
        /// Border 预览触摸按下事件处理
        /// </summary>
        private void Border_PreviewTouchDown(object sender, TouchEventArgs e)
        {
            var border = sender as Border;
            if (border == null) return;

            // 获取触摸点位置
            var touchPoint = e.GetTouchPoint(border);
            
            // 创建模拟的鼠标事件
            var mouseButtonEventArgs = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            {
                RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent,
                Source = border
            };
            
            // 触发预览鼠标按下事件
            border.RaiseEvent(mouseButtonEventArgs);
            
            e.Handled = true;
        }

        /// <summary>
        /// 加载设置到UI - 子类需要实现
        /// </summary>
        public abstract void LoadSettings();

        /// <summary>
        /// 查找ToggleSwitch控件
        /// </summary>
        protected Border FindToggleSwitch(string name)
        {
            return this.FindDescendantByName(name) as Border;
        }

        /// <summary>
        /// 设置ToggleSwitch状态
        /// </summary>
        protected void SetToggleSwitchState(Border toggleSwitch, bool isOn)
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
        /// 通用的ToggleSwitch点击事件处理
        /// </summary>
        protected virtual void ToggleSwitch_Click(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            var border = sender as Border;
            if (border == null) return;

            bool isOn = border.Background.ToString() == "#FF3584E4";
            bool newState = !isOn;
            SetToggleSwitchState(border, newState);

            string tag = border.Tag?.ToString();
            if (string.IsNullOrEmpty(tag)) return;

            HandleToggleSwitchChange(tag, newState);
        }

        /// <summary>
        /// 处理ToggleSwitch变化 - 子类需要实现
        /// </summary>
        protected abstract void HandleToggleSwitchChange(string tag, bool newState);

        /// <summary>
        /// 处理选项按钮变化 - 子类可以重写
        /// </summary>
        protected virtual void HandleOptionButtonChange(object sender, string tag)
        {
            // 默认实现：清除同组其他按钮的选中状态
            var border = sender as Border;
            if (border == null) return;

            string[] parts = tag.Split('_');
            if (parts.Length >= 2)
            {
                string group = parts[0];
                string value = parts[1];

                // 清除同组其他按钮的选中状态
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
                                childBorder.Background = new SolidColorBrush(Colors.Transparent);
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

                bool isDarkTheme = ThemeHelper.IsDarkTheme;
                var selectedBrush = isDarkTheme ? new SolidColorBrush(Color.FromRgb(25, 25, 25)) : new SolidColorBrush(Color.FromRgb(225, 225, 225));
                border.Background = selectedBrush;
                var currentTextBlock = border.Child as TextBlock;
                if (currentTextBlock != null)
                {
                    currentTextBlock.FontWeight = FontWeights.Bold;
                    currentTextBlock.Foreground = ThemeHelper.GetTextPrimaryBrush();
                }

                HandleOptionChange(group, value);
            }
        }

        protected abstract void HandleOptionChange(string group, string value);
    }
}

