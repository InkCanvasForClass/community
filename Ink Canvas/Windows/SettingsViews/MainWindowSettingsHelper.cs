using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Media = System.Windows.Media;

namespace Ink_Canvas.Windows.SettingsViews
{
    public static class MainWindowSettingsHelper
    {
        private static MainWindow GetMainWindow()
        {
            return Application.Current.MainWindow as MainWindow;
        }

        /// <summary>
        /// 调用 MainWindow 中的方法
        /// </summary>
        public static void InvokeMainWindowMethod(string methodName, params object[] parameters)
        {
            try
            {
                var mainWindow = GetMainWindow();
                if (mainWindow == null) return;

                var method = mainWindow.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (method != null)
                {
                    method.Invoke(mainWindow, parameters);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"调用 MainWindow 方法 {methodName} 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 调用 MainWindow 中的 ToggleSwitch 事件处理方法
        /// </summary>
        public static void InvokeToggleSwitchToggled(string toggleSwitchName, bool isOn)
        {
            try
            {
                var mainWindow = GetMainWindow();
                if (mainWindow == null) return;

                // 获取 MainWindow 中的 ToggleSwitch 控件
                var toggleSwitch = mainWindow.FindName(toggleSwitchName);
                if (toggleSwitch == null)
                {
                    // 如果找不到控件，尝试通过反射设置属性
                    var property = mainWindow.GetType().GetProperty(toggleSwitchName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                    if (property != null && property.PropertyType.Name.Contains("ToggleSwitch"))
                    {
                        var isOnProperty = property.PropertyType.GetProperty("IsOn");
                        if (isOnProperty != null)
                        {
                            var toggleSwitchInstance = property.GetValue(mainWindow);
                            if (toggleSwitchInstance != null)
                            {
                                isOnProperty.SetValue(toggleSwitchInstance, isOn);
                            }
                        }
                    }
                    // 即使找不到控件，也尝试触发事件（可能通过反射调用）
                    var toggledMethodName = toggleSwitchName + "_Toggled";
                    InvokeMainWindowMethod(toggledMethodName, null, new RoutedEventArgs());

                    // 通知新设置面板同步状态
                    NotifySettingsPanelsSyncState(toggleSwitchName);
                    return;
                }

                // 设置 ToggleSwitch 的 IsOn 属性
                var toggleSwitchType = toggleSwitch.GetType();
                var isOnProp = toggleSwitchType.GetProperty("IsOn");
                if (isOnProp != null)
                {
                    isOnProp.SetValue(toggleSwitch, isOn);
                }

                // 触发 Toggled 事件
                var toggledMethodName2 = toggleSwitchName + "_Toggled";
                InvokeMainWindowMethod(toggledMethodName2, toggleSwitch, new RoutedEventArgs());

                // 通知新设置面板同步状态
                NotifySettingsPanelsSyncState(toggleSwitchName);

                // 检查是否需要更新主题
                NotifyThemeUpdateIfNeeded(toggleSwitchName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"调用 ToggleSwitch {toggleSwitchName} 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 调用 MainWindow 中的 ComboBox 事件处理方法
        /// </summary>
        public static void InvokeComboBoxSelectionChanged(string comboBoxName, object selectedItem)
        {
            try
            {
                var mainWindow = GetMainWindow();
                if (mainWindow == null) return;

                // 获取 MainWindow 中的 ComboBox 控件
                var comboBox = mainWindow.FindName(comboBoxName) as System.Windows.Controls.ComboBox;
                if (comboBox != null)
                {
                    comboBox.SelectedItem = selectedItem;

                    // 触发 SelectionChanged 事件
                    var selectionChangedMethodName = comboBoxName + "_SelectionChanged";
                    InvokeMainWindowMethod(selectionChangedMethodName, comboBox, new System.Windows.Controls.SelectionChangedEventArgs(
                        System.Windows.Controls.Primitives.Selector.SelectionChangedEvent,
                        new System.Collections.IList[0],
                        new System.Collections.IList[0]));
                }

                // 通知新设置面板同步状态
                NotifySettingsPanelsSyncState(comboBoxName);

                // 检查是否需要更新主题
                NotifyThemeUpdateIfNeeded(comboBoxName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"调用 ComboBox {comboBoxName} 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 调用 MainWindow 中的 Slider 事件处理方法
        /// </summary>
        public static void InvokeSliderValueChanged(string sliderName, double value)
        {
            try
            {
                var mainWindow = GetMainWindow();
                if (mainWindow == null) return;

                // 获取 MainWindow 中的 Slider 控件
                var slider = mainWindow.FindName(sliderName) as System.Windows.Controls.Slider;
                if (slider != null)
                {
                    var oldValue = slider.Value;
                    slider.Value = value;

                    // 触发 ValueChanged 事件
                    var valueChangedMethodName = sliderName + "_ValueChanged";
                    InvokeMainWindowMethod(valueChangedMethodName, slider,
                        new System.Windows.RoutedPropertyChangedEventArgs<double>(oldValue, value));
                }

                // 通知新设置面板同步状态
                NotifySettingsPanelsSyncState(sliderName);

                // 检查是否需要更新主题（某些Slider可能影响UI外观）
                NotifyThemeUpdateIfNeeded(sliderName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"调用 Slider {sliderName} 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 调用 MainWindow 中的 CheckBox 事件处理方法
        /// </summary>
        public static void InvokeCheckBoxCheckedChanged(string checkBoxName, bool isChecked)
        {
            try
            {
                var mainWindow = GetMainWindow();
                if (mainWindow == null) return;

                // 获取 MainWindow 中的 CheckBox 控件
                var checkBox = mainWindow.FindName(checkBoxName) as System.Windows.Controls.CheckBox;
                if (checkBox != null)
                {
                    checkBox.IsChecked = isChecked;

                    // 尝试多种可能的方法名
                    var methodNames = new[]
                    {
                        checkBoxName + "_IsCheckChanged",
                        checkBoxName + "_IsCheckChange",
                        checkBoxName + "_Checked",
                        checkBoxName + "_Unchecked"
                    };

                    foreach (var methodName in methodNames)
                    {
                        var method = mainWindow.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                        if (method != null)
                        {
                            InvokeMainWindowMethod(methodName, checkBox, new RoutedEventArgs());
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"调用 CheckBox {checkBoxName} 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 安全地修改设置并保存，优先调用 MainWindow 中的事件处理方法
        /// 如果找不到对应的事件处理方法，则直接修改设置并保存
        /// </summary>
        /// <param name="action">设置修改的 Action，例如：() => MainWindow.Settings.Startup.IsAutoUpdate = true</param>
        /// <param name="eventHandlerName">可选：要调用的 MainWindow 事件处理方法名（如 "ToggleSwitchIsAutoUpdate_Toggled"）</param>
        /// <param name="controlName">可选：控件名称，用于状态同步（如 "ToggleSwitchIsAutoUpdate"）</param>
        /// <param name="eventHandlerParams">可选：事件处理方法的参数</param>
        public static void UpdateSettingSafely(Action action, string eventHandlerName = null, string controlName = null, params object[] eventHandlerParams)
        {
            try
            {
                // 如果提供了事件处理方法名，优先调用
                if (!string.IsNullOrEmpty(eventHandlerName))
                {
                    var mainWindow = GetMainWindow();
                    if (mainWindow != null)
                    {
                        var method = mainWindow.GetType().GetMethod(eventHandlerName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                        if (method != null)
                        {
                            // 调用事件处理方法（它会自动保存设置并触发状态同步）
                            method.Invoke(mainWindow, eventHandlerParams);

                            // 如果提供了控件名称，确保状态同步
                            if (!string.IsNullOrEmpty(controlName))
                            {
                                NotifySettingsPanelsSyncState(controlName);
                            }
                            return;
                        }
                    }
                }

                // 如果没有事件处理方法或调用失败，直接修改设置并保存
                action?.Invoke();
                MainWindow.SaveSettingsToFile();

                // 如果提供了控件名称，通知面板同步状态
                if (!string.IsNullOrEmpty(controlName))
                {
                    NotifySettingsPanelsSyncState(controlName);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新设置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 直接修改设置属性并保存（用于没有对应事件处理方法的设置项）
        /// </summary>
        /// <param name="action">设置修改的 Action</param>
        /// <param name="controlName">可选：控件名称，用于状态同步（如 "ToggleSwitchIsAutoUpdate"）</param>
        public static void UpdateSettingDirectly(Action action, string controlName = null)
        {
            try
            {
                action?.Invoke();
                MainWindow.SaveSettingsToFile();

                // 如果提供了控件名称，通知面板同步状态
                if (!string.IsNullOrEmpty(controlName))
                {
                    NotifySettingsPanelsSyncState(controlName);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"直接更新设置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 调用 MainWindow 中的 TextBox 事件处理方法
        /// </summary>
        public static void InvokeTextBoxTextChanged(string textBoxName, string text)
        {
            try
            {
                var mainWindow = GetMainWindow();
                if (mainWindow == null) return;

                // 获取 MainWindow 中的 TextBox 控件
                var textBox = mainWindow.FindName(textBoxName) as System.Windows.Controls.TextBox;
                if (textBox != null)
                {
                    textBox.Text = text;

                    // 触发 TextChanged 事件
                    var textChangedMethodName = textBoxName + "_TextChanged";
                    InvokeMainWindowMethod(textChangedMethodName, textBox, new System.Windows.Controls.TextChangedEventArgs(
                        System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
                        System.Windows.Controls.UndoAction.None));
                }

                // 通知新设置面板同步状态
                NotifySettingsPanelsSyncState(textBoxName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"调用 TextBox {textBoxName} 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 通知所有新设置面板同步指定控件的状态
        /// </summary>
        public static void NotifySettingsPanelsSyncState(string controlName)
        {
            try
            {
                // 延迟执行，确保设置已经保存
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    // 查找所有打开的设置窗口
                    foreach (Window window in Application.Current.Windows)
                    {
                        if (window.GetType().Name == "SettingsWindow")
                        {
                            // 同步所有面板（保守策略）
                            SyncAllPanels(window);
                            break; // 通常只有一个设置窗口
                        }
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"通知设置面板同步状态失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 同步所有面板的状态（保守策略）
        /// </summary>
        private static void SyncAllPanels(Window settingsWindow)
        {
            try
            {
                // 获取所有页面属性
                var pageProperties = settingsWindow.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.PropertyType.Name.EndsWith("Page") &&
                               p.PropertyType.IsSubclassOf(typeof(System.Windows.Controls.Page)));

                foreach (var pageProp in pageProperties)
                {
                    try
                    {
                        var page = pageProp.GetValue(settingsWindow) as System.Windows.Controls.Page;
                        if (page != null)
                        {
                            // 调用 LoadSettings 方法
                            var loadMethod = page.GetType().GetMethod("LoadSettings",
                                BindingFlags.Public | BindingFlags.Instance);
                            if (loadMethod != null)
                            {
                                loadMethod.Invoke(page, null);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"同步页面 {pageProp.Name} 状态失败: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"同步所有页面状态失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查并通知设置窗口更新主题（如果设置变化可能影响主题）
        /// </summary>
        /// <param name="controlName">控件名称，用于判断是否是主题相关的设置</param>
        public static void NotifyThemeUpdateIfNeeded(string controlName)
        {
            try
            {
                if (string.IsNullOrEmpty(controlName)) return;

                // 定义可能影响主题的控件名称列表（扩展版）
                var themeRelatedControls = new[]
                {
                    "ComboBoxTheme",           // 主题选择
                    "ToggleSwitchEnableTrayIcon", // 托盘图标（可能影响图标颜色）
                    "ComboBoxFloatingBarImg",   // 浮动栏图标
                    "ComboBoxUnFoldBtnImg",     // 展开按钮图标
                    "ComboBoxSplashScreenStyle", // 启动画面样式
                    "ToggleSwitchEnableSplashScreen", // 启动画面开关
                    "ViewboxFloatingBarScaleTransformValueSlider", // 浮动栏缩放（可能影响UI）
                    "ViewboxFloatingBarOpacityValueSlider", // 浮动栏透明度
                    "ViewboxFloatingBarOpacityInPPTValueSlider", // PPT中浮动栏透明度
                    "UnFoldBtnImg",             // 展开按钮图标（选项按钮）
                    "FloatingBarImg",            // 浮动栏图标（选项按钮）
                    "Theme"                      // 主题（选项按钮）
                };

                // 检查是否是主题相关的设置（使用更灵活的匹配）
                bool isThemeRelated = false;
                string controlNameLower = controlName.ToLower();

                foreach (var themeControl in themeRelatedControls)
                {
                    string themeControlLower = themeControl.ToLower();
                    // 检查是否包含或匹配
                    if (controlNameLower.Contains(themeControlLower) ||
                        themeControlLower.Contains(controlNameLower) ||
                        controlNameLower == themeControlLower)
                    {
                        isThemeRelated = true;
                        break;
                    }
                }

                if (isThemeRelated)
                {
                    // 延迟通知，确保设置已保存
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        // 通知设置窗口更新主题
                        NotifySettingsWindowThemeUpdate();
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"通知主题更新失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 通知设置窗口更新所有页面的主题
        /// </summary>
        private static void NotifySettingsWindowThemeUpdate()
        {
            try
            {
                // 查找所有打开的设置窗口
                foreach (Window window in Application.Current.Windows)
                {
                    // 使用类型名称匹配，因为 SettingsWindow 在不同的命名空间中
                    if (window.GetType().Name == "SettingsWindow")
                    {
                        // 同时调用 ApplyTheme 方法更新窗口本身
                        var applyThemeMethod = window.GetType().GetMethod("ApplyTheme",
                            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                        if (applyThemeMethod != null)
                        {
                            applyThemeMethod.Invoke(window, null);
                        }

                        break; // 通常只有一个设置窗口
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"通知设置窗口主题更新失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 强制更新所有设置页面的主题（公共方法，可在外部调用）
        /// </summary>
        public static void ForceUpdateAllPanelsTheme()
        {
            NotifySettingsWindowThemeUpdate();
        }

        /// <summary>
        /// 调用 MainWindow 中的 ComboBox 事件处理方法（增强版，支持主题更新通知）
        /// </summary>
        public static void InvokeComboBoxSelectionChangedWithThemeCheck(string comboBoxName, object selectedItem)
        {
            InvokeComboBoxSelectionChanged(comboBoxName, selectedItem);
            NotifyThemeUpdateIfNeeded(comboBoxName);
        }

        /// <summary>
        /// 调用 MainWindow 中的 ToggleSwitch 事件处理方法（增强版，支持主题更新通知）
        /// </summary>
        public static void InvokeToggleSwitchToggledWithThemeCheck(string toggleSwitchName, bool isOn)
        {
            InvokeToggleSwitchToggled(toggleSwitchName, isOn);
            NotifyThemeUpdateIfNeeded(toggleSwitchName);
        }

        /// <summary>
        /// 为控件树中的所有交互控件启用触摸支持（公共方法，可在外部调用）
        /// </summary>
        /// <param name="parent">父控件</param>
        public static void EnableTouchSupportForControls(DependencyObject parent)
        {
            if (parent == null) return;

            for (int i = 0; i < Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = Media.VisualTreeHelper.GetChild(parent, i);

                // 为 Border 控件（ToggleSwitch、选项按钮等）启用触摸支持
                if (child is Border border)
                {
                    // 检查是否是交互控件（有 Tag 或 Cursor 为 Hand）
                    if (border.Tag != null || border.Cursor == Cursors.Hand)
                    {
                        border.IsManipulationEnabled = true;

                        // 添加触摸事件支持，将触摸事件转换为鼠标事件
                        border.TouchDown += (s, e) =>
                        {
                            var touchPoint = e.GetTouchPoint(border);
                            var mouseEvent = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                            {
                                RoutedEvent = UIElement.MouseLeftButtonDownEvent,
                                Source = border
                            };
                            border.RaiseEvent(mouseEvent);
                            border.CaptureTouch(e.TouchDevice);
                            e.Handled = true;
                        };

                        // 添加触摸释放事件
                        border.TouchUp += (s, e) =>
                        {
                            border.ReleaseTouchCapture(e.TouchDevice);
                            e.Handled = true;
                        };
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
                // 为 CheckBox 启用触摸支持
                else if (child is CheckBox checkBox)
                {
                    checkBox.IsManipulationEnabled = true;
                }
                // 为 RadioButton 启用触摸支持
                else if (child is RadioButton radioButton)
                {
                    radioButton.IsManipulationEnabled = true;
                }

                // 递归处理子元素
                EnableTouchSupportForControls(child);
            }
        }
    }
}
