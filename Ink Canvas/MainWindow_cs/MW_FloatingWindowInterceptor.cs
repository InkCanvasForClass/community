using Ink_Canvas.Helpers;
using System;
using System.Windows;

namespace Ink_Canvas
{
    public partial class MainWindow : Ink_Canvas.Helpers.PerformanceTransparentWin
    {
        #region 悬浮窗拦截功能

        private void InitializeFloatingWindowInterceptor()
        {
            try
            {
                _floatingWindowInterceptorManager = new FloatingWindowInterceptorManager();
                _floatingWindowInterceptorManager.WindowIntercepted += OnFloatingWindowIntercepted;
                _floatingWindowInterceptorManager.WindowRestored += OnFloatingWindowRestored;
                _floatingWindowInterceptorManager.Initialize(Settings.Automation.FloatingWindowInterceptor);
                LogHelper.WriteLogToFile("悬浮窗拦截管理器初始化完成", LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"初始化悬浮窗拦截管理器失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void OnFloatingWindowIntercepted(object sender, FloatingWindowInterceptor.WindowInterceptedEventArgs e)
        {
            try
            {
                Dispatcher.BeginInvoke(new Action(() => { }));
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理窗口拦截事件失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void OnFloatingWindowRestored(object sender, FloatingWindowInterceptor.WindowRestoredEventArgs e)
        {
            try
            {
                Dispatcher.BeginInvoke(new Action(() => { }));
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理窗口恢复事件失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        #endregion

        #region 悬浮窗拦截事件处理

        private void ToggleSwitchFloatingWindowInterceptorEnabled_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            try
            {
                var toggle = sender as Wpf.Ui.Controls.ToggleSwitch;
                if (toggle != null)
                    Settings.Automation.FloatingWindowInterceptor.IsEnabled = ((toggle.IsChecked) == true);

                if (_floatingWindowInterceptorManager != null)
                {
                    if (Settings.Automation.FloatingWindowInterceptor.IsEnabled)
                        _floatingWindowInterceptorManager.Start();
                    else
                        _floatingWindowInterceptorManager.Stop();
                }

                SaveSettingsToFile();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"切换悬浮窗拦截主开关失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void ToggleSwitchSeewoWhiteboard3Floating_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            var toggle = sender as Wpf.Ui.Controls.ToggleSwitch;
            if (toggle != null) SetInterceptRule(FloatingWindowInterceptor.InterceptType.SeewoWhiteboard3Floating, ((toggle.IsChecked) == true));
        }

        private void ToggleSwitchSeewoWhiteboard5Floating_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            var toggle = sender as Wpf.Ui.Controls.ToggleSwitch;
            if (toggle != null) SetInterceptRule(FloatingWindowInterceptor.InterceptType.SeewoWhiteboard5Floating, ((toggle.IsChecked) == true));
        }

        private void ToggleSwitchSeewoWhiteboard5CFloating_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            var toggle = sender as Wpf.Ui.Controls.ToggleSwitch;
            if (toggle != null) SetInterceptRule(FloatingWindowInterceptor.InterceptType.SeewoWhiteboard5CFloating, ((toggle.IsChecked) == true));
        }

        private void ToggleSwitchSeewoPincoSideBarFloating_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            var toggle = sender as Wpf.Ui.Controls.ToggleSwitch;
            if (toggle != null) SetInterceptRule(FloatingWindowInterceptor.InterceptType.SeewoPincoSideBarFloating, ((toggle.IsChecked) == true));
        }

        private void ToggleSwitchSeewoPincoDrawingFloating_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            var toggle = sender as Wpf.Ui.Controls.ToggleSwitch;
            if (toggle != null) SetInterceptRule(FloatingWindowInterceptor.InterceptType.SeewoPincoDrawingFloating, ((toggle.IsChecked) == true));
        }

        private void ToggleSwitchSeewoPPTFloating_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            var toggle = sender as Wpf.Ui.Controls.ToggleSwitch;
            if (toggle != null) SetInterceptRule(FloatingWindowInterceptor.InterceptType.SeewoPPTFloating, ((toggle.IsChecked) == true));
        }

        private void ToggleSwitchAiClassFloating_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            var toggle = sender as Wpf.Ui.Controls.ToggleSwitch;
            if (toggle != null) SetInterceptRule(FloatingWindowInterceptor.InterceptType.AiClassFloating, ((toggle.IsChecked) == true));
        }

        private void ToggleSwitchHiteAnnotationFloating_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            var toggle = sender as Wpf.Ui.Controls.ToggleSwitch;
            if (toggle != null) SetInterceptRule(FloatingWindowInterceptor.InterceptType.HiteAnnotationFloating, ((toggle.IsChecked) == true));
        }

        private void ToggleSwitchChangYanFloating_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            var toggle = sender as Wpf.Ui.Controls.ToggleSwitch;
            if (toggle != null) SetInterceptRule(FloatingWindowInterceptor.InterceptType.ChangYanFloating, ((toggle.IsChecked) == true));
        }

        private void ToggleSwitchChangYanPPTFloating_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            var toggle = sender as Wpf.Ui.Controls.ToggleSwitch;
            if (toggle != null) SetInterceptRule(FloatingWindowInterceptor.InterceptType.ChangYanPPTFloating, ((toggle.IsChecked) == true));
        }

        private void ToggleSwitchIntelligentClassFloating_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            var toggle = sender as Wpf.Ui.Controls.ToggleSwitch;
            if (toggle != null) SetInterceptRule(FloatingWindowInterceptor.InterceptType.IntelligentClassFloating, ((toggle.IsChecked) == true));
        }

        private void ToggleSwitchSeewoDesktopAnnotationFloating_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            var toggle = sender as Wpf.Ui.Controls.ToggleSwitch;
            if (toggle != null) SetInterceptRule(FloatingWindowInterceptor.InterceptType.SeewoDesktopAnnotationFloating, ((toggle.IsChecked) == true));
        }

        private void ToggleSwitchSeewoDesktopSideBarFloating_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            var toggle = sender as Wpf.Ui.Controls.ToggleSwitch;
            if (toggle != null) SetInterceptRule(FloatingWindowInterceptor.InterceptType.SeewoDesktopSideBarFloating, ((toggle.IsChecked) == true));
        }

        public void SetInterceptRule(FloatingWindowInterceptor.InterceptType type, bool enabled)
        {
            try
            {
                if (_floatingWindowInterceptorManager != null)
                {
                    _floatingWindowInterceptorManager.SetInterceptRule(type, enabled);
                }

                var ruleName = type.ToString();
                if (Settings.Automation.FloatingWindowInterceptor.InterceptRules.ContainsKey(ruleName))
                {
                    Settings.Automation.FloatingWindowInterceptor.InterceptRules[ruleName] = enabled;
                }

                var rule = _floatingWindowInterceptorManager?.GetInterceptRule(type);
                if (rule != null)
                {
                    if (rule.ChildTypes.Count > 0)
                    {
                        foreach (var childType in rule.ChildTypes)
                        {
                            var childRuleName = childType.ToString();
                            if (Settings.Automation.FloatingWindowInterceptor.InterceptRules.ContainsKey(childRuleName))
                            {
                                Settings.Automation.FloatingWindowInterceptor.InterceptRules[childRuleName] = enabled;
                            }
                        }
                    }
                    else if (rule.ParentType.HasValue)
                    {
                        var parentRule = _floatingWindowInterceptorManager?.GetInterceptRule(rule.ParentType.Value);
                        if (parentRule != null)
                        {
                            var parentRuleName = rule.ParentType.Value.ToString();
                            if (Settings.Automation.FloatingWindowInterceptor.InterceptRules.ContainsKey(parentRuleName))
                            {
                                Settings.Automation.FloatingWindowInterceptor.InterceptRules[parentRuleName] = parentRule.IsEnabled;
                            }
                        }
                    }
                }

                SaveSettingsToFile();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"设置拦截规则失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }
        #endregion
    }
}
