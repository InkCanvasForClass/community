using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Media = System.Windows.Media;

namespace Ink_Canvas.Windows.SettingsViews.Helpers
{
    public static class MainWindowSettingsHelper
    {
        private static MainWindow GetMainWindow()
        {
            return Application.Current.MainWindow as MainWindow;
        }

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

        public static void InvokeToggleSwitchToggled(string toggleSwitchName, bool isOn)
        {
            try
            {
                var mainWindow = GetMainWindow();
                if (mainWindow == null) return;

                var toggleSwitch = mainWindow.FindName(toggleSwitchName);
                if (toggleSwitch == null)
                {
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
                    var toggledMethodName = toggleSwitchName + "_Toggled";
                    InvokeMainWindowMethod(toggledMethodName, null, new RoutedEventArgs());

                    NotifySettingsPanelsSyncState(toggleSwitchName);
                    return;
                }

                var toggleSwitchType = toggleSwitch.GetType();
                var isOnProp = toggleSwitchType.GetProperty("IsOn");
                if (isOnProp != null)
                {
                    isOnProp.SetValue(toggleSwitch, isOn);
                }

                var toggledMethodName2 = toggleSwitchName + "_Toggled";
                InvokeMainWindowMethod(toggledMethodName2, toggleSwitch, new RoutedEventArgs());

                NotifySettingsPanelsSyncState(toggleSwitchName);

                NotifyThemeUpdateIfNeeded(toggleSwitchName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"调用 ToggleSwitch {toggleSwitchName} 失败: {ex.Message}");
            }
        }

        public static void InvokeComboBoxSelectionChanged(string comboBoxName, object selectedItem)
        {
            try
            {
                var mainWindow = GetMainWindow();
                if (mainWindow == null) return;

                var comboBox = mainWindow.FindName(comboBoxName) as System.Windows.Controls.ComboBox;
                if (comboBox != null)
                {
                    comboBox.SelectedItem = selectedItem;

                    var selectionChangedMethodName = comboBoxName + "_SelectionChanged";
                    InvokeMainWindowMethod(selectionChangedMethodName, comboBox, new System.Windows.Controls.SelectionChangedEventArgs(
                        System.Windows.Controls.Primitives.Selector.SelectionChangedEvent,
                        new System.Collections.IList[0],
                        new System.Collections.IList[0]));
                }

                NotifySettingsPanelsSyncState(comboBoxName);

                NotifyThemeUpdateIfNeeded(comboBoxName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"调用 ComboBox {comboBoxName} 失败: {ex.Message}");
            }
        }

        public static void InvokeSliderValueChanged(string sliderName, double value)
        {
            try
            {
                var mainWindow = GetMainWindow();
                if (mainWindow == null) return;

                var slider = mainWindow.FindName(sliderName) as System.Windows.Controls.Slider;
                if (slider != null)
                {
                    var oldValue = slider.Value;
                    slider.Value = value;

                    var valueChangedMethodName = sliderName + "_ValueChanged";
                    InvokeMainWindowMethod(valueChangedMethodName, slider,
                        new System.Windows.RoutedPropertyChangedEventArgs<double>(oldValue, value));
                }

                NotifySettingsPanelsSyncState(sliderName);

                NotifyThemeUpdateIfNeeded(sliderName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"调用 Slider {sliderName} 失败: {ex.Message}");
            }
        }

        public static void InvokeCheckBoxCheckedChanged(string checkBoxName, bool isChecked)
        {
            try
            {
                var mainWindow = GetMainWindow();
                if (mainWindow == null) return;

                var checkBox = mainWindow.FindName(checkBoxName) as System.Windows.Controls.CheckBox;
                if (checkBox != null)
                {
                    checkBox.IsChecked = isChecked;

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

        public static void UpdateSettingSafely(Action action, string eventHandlerName = null, string controlName = null, params object[] eventHandlerParams)
        {
            try
            {
                if (!string.IsNullOrEmpty(eventHandlerName))
                {
                    var mainWindow = GetMainWindow();
                    if (mainWindow != null)
                    {
                        var method = mainWindow.GetType().GetMethod(eventHandlerName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                        if (method != null)
                        {
                            method.Invoke(mainWindow, eventHandlerParams);

                            if (!string.IsNullOrEmpty(controlName))
                            {
                                NotifySettingsPanelsSyncState(controlName);
                            }
                            return;
                        }
                    }
                }

                action?.Invoke();
                MainWindow.SaveSettingsToFile();

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

        public static void UpdateSettingDirectly(Action action, string controlName = null)
        {
            try
            {
                action?.Invoke();
                MainWindow.SaveSettingsToFile();

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

        public static void InvokeTextBoxTextChanged(string textBoxName, string text)
        {
            try
            {
                var mainWindow = GetMainWindow();
                if (mainWindow == null) return;

                var textBox = mainWindow.FindName(textBoxName) as System.Windows.Controls.TextBox;
                if (textBox != null)
                {
                    textBox.Text = text;

                    var textChangedMethodName = textBoxName + "_TextChanged";
                    InvokeMainWindowMethod(textChangedMethodName, textBox, new System.Windows.Controls.TextChangedEventArgs(
                        System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
                        System.Windows.Controls.UndoAction.None));
                }

                NotifySettingsPanelsSyncState(textBoxName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"调用 TextBox {textBoxName} 失败: {ex.Message}");
            }
        }

        public static void NotifySettingsPanelsSyncState(string controlName)
        {
            try
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    foreach (Window window in Application.Current.Windows)
                    {
                        if (window.GetType().Name == "SettingsWindow")
                        {
                            SyncAllPanels(window);
                            break;
                        }
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"通知设置面板同步状态失败: {ex.Message}");
            }
        }

        private static void SyncAllPanels(Window settingsWindow)
        {
            try
            {
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

        public static void NotifyThemeUpdateIfNeeded(string controlName)
        {
            try
            {
                if (string.IsNullOrEmpty(controlName)) return;

                var themeRelatedControls = new[]
                {
                    "ComboBoxTheme",
                    "ToggleSwitchEnableTrayIcon",
                    "ComboBoxFloatingBarImg",
                    "ComboBoxUnFoldBtnImg",
                    "ComboBoxSplashScreenStyle",
                    "ToggleSwitchEnableSplashScreen",
                    "ViewboxFloatingBarScaleTransformValueSlider",
                    "ViewboxFloatingBarOpacityValueSlider",
                    "ViewboxFloatingBarOpacityInPPTValueSlider",
                    "UnFoldBtnImg",
                    "FloatingBarImg",
                    "Theme"
                };

                bool isThemeRelated = false;

                foreach (var themeControl in themeRelatedControls)
                {
                    // OrdinalIgnoreCase 避免在循环里反复 ToLower() 生成中间字符串。
                    if (controlName.IndexOf(themeControl, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        themeControl.IndexOf(controlName, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        isThemeRelated = true;
                        break;
                    }
                }

                if (isThemeRelated)
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        NotifySettingsWindowThemeUpdate();
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"通知主题更新失败: {ex.Message}");
            }
        }

        private static void NotifySettingsWindowThemeUpdate()
        {
            try
            {
                foreach (Window window in Application.Current.Windows)
                {
                    if (window.GetType().Name == "SettingsWindow")
                    {
                        var applyThemeMethod = window.GetType().GetMethod("ApplyTheme",
                            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                        if (applyThemeMethod != null)
                        {
                            applyThemeMethod.Invoke(window, null);
                        }

                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"通知设置窗口主题更新失败: {ex.Message}");
            }
        }

        public static void ForceUpdateAllPanelsTheme()
        {
            NotifySettingsWindowThemeUpdate();
        }

        public static void InvokeComboBoxSelectionChangedWithThemeCheck(string comboBoxName, object selectedItem)
        {
            InvokeComboBoxSelectionChanged(comboBoxName, selectedItem);
            NotifyThemeUpdateIfNeeded(comboBoxName);
        }

        public static void InvokeToggleSwitchToggledWithThemeCheck(string toggleSwitchName, bool isOn)
        {
            InvokeToggleSwitchToggled(toggleSwitchName, isOn);
            NotifyThemeUpdateIfNeeded(toggleSwitchName);
        }

        public static void EnableTouchSupportForControls(DependencyObject parent)
        {
            if (parent == null) return;

            for (int i = 0; i < Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = Media.VisualTreeHelper.GetChild(parent, i);

                if (child is Border border)
                {
                    if (border.Tag != null || border.Cursor == Cursors.Hand)
                    {
                        border.IsManipulationEnabled = true;

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

                        border.TouchUp += (s, e) =>
                        {
                            border.ReleaseTouchCapture(e.TouchDevice);
                            e.Handled = true;
                        };
                    }
                }
                else if (child is Button button)
                {
                    button.IsManipulationEnabled = true;
                }
                else if (child is ComboBox comboBox)
                {
                    comboBox.IsManipulationEnabled = true;
                }
                else if (child is Slider slider)
                {
                    slider.IsManipulationEnabled = true;
                }
                else if (child is TextBox textBox)
                {
                    textBox.IsManipulationEnabled = true;
                }
                else if (child is CheckBox checkBox)
                {
                    checkBox.IsManipulationEnabled = true;
                }
                else if (child is RadioButton radioButton)
                {
                    radioButton.IsManipulationEnabled = true;
                }

                EnableTouchSupportForControls(child);
            }
        }
    }
}
