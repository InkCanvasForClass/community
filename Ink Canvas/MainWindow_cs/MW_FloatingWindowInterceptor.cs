using Ink_Canvas.Helpers;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas
{
    public partial class MainWindow : Window
    {
        #region 悬浮窗拦截功能

        /// <summary>
        /// 初始化悬浮窗拦截管理器
        /// </summary>
        private void InitializeFloatingWindowInterceptor()
        {
            try
            {
                _floatingWindowInterceptorManager = new FloatingWindowInterceptorManager();
                
                // 订阅事件
                _floatingWindowInterceptorManager.WindowIntercepted += OnFloatingWindowIntercepted;
                _floatingWindowInterceptorManager.WindowRestored += OnFloatingWindowRestored;
                
                // 初始化拦截器
                _floatingWindowInterceptorManager.Initialize(Settings.Automation.FloatingWindowInterceptor);
                
                // 加载UI状态
                LoadFloatingWindowInterceptorUI();
                
                LogHelper.WriteLogToFile("悬浮窗拦截管理器初始化完成", LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"初始化悬浮窗拦截管理器失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 加载悬浮窗拦截UI状态
        /// </summary>
        private void LoadFloatingWindowInterceptorUI()
        {
            try
            {
                if (!isLoaded) return;

                // 设置主开关状态
                ToggleSwitchFloatingWindowInterceptorEnabled.IsOn = Settings.Automation.FloatingWindowInterceptor.IsEnabled;
                
                // 设置各个拦截规则的状态
                foreach (var kvp in Settings.Automation.FloatingWindowInterceptor.InterceptRules)
                {
                    var toggleName = $"ToggleSwitch{kvp.Key}";
                    var toggle = FindName(toggleName) as ToggleSwitch;
                    if (toggle != null)
                    {
                        toggle.IsOn = kvp.Value;
                    }
                }
                
                // 更新UI可见性
                UpdateFloatingWindowInterceptorUI();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"加载悬浮窗拦截UI状态失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 更新悬浮窗拦截UI
        /// </summary>
        private void UpdateFloatingWindowInterceptorUI()
        {
            try
            {
                var isEnabled = Settings.Automation.FloatingWindowInterceptor.IsEnabled;
                FloatingWindowInterceptorGrid.Visibility = isEnabled ? Visibility.Visible : Visibility.Collapsed;
                
                // 更新状态文本
                if (_floatingWindowInterceptorManager != null)
                {
                    var stats = _floatingWindowInterceptorManager.GetStatistics();
                    TextBlockFloatingWindowInterceptorStatus.Text = stats.IsRunning 
                        ? $"拦截器运行中 - 已启用 {stats.EnabledRules}/{stats.TotalRules} 个规则"
                        : "拦截器未启动";
                }
                else
                {
                    TextBlockFloatingWindowInterceptorStatus.Text = "拦截器未初始化";
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更新悬浮窗拦截UI失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 窗口被拦截事件处理
        /// </summary>
        private void OnFloatingWindowIntercepted(object sender, FloatingWindowInterceptor.WindowInterceptedEventArgs e)
        {
            try
            {
                // 在UI线程中更新状态
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdateFloatingWindowInterceptorUI();
                }));
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理窗口拦截事件失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 窗口被恢复事件处理
        /// </summary>
        private void OnFloatingWindowRestored(object sender, FloatingWindowInterceptor.WindowRestoredEventArgs e)
        {
            try
            {
                // 在UI线程中更新状态
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdateFloatingWindowInterceptorUI();
                }));
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理窗口恢复事件失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        #endregion

        #region 悬浮窗拦截事件处理

        /// <summary>
        /// 主开关切换事件
        /// </summary>
        private void ToggleSwitchFloatingWindowInterceptorEnabled_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            
            try
            {
                Settings.Automation.FloatingWindowInterceptor.IsEnabled = ToggleSwitchFloatingWindowInterceptorEnabled.IsOn;
                
                if (_floatingWindowInterceptorManager != null)
                {
                    if (Settings.Automation.FloatingWindowInterceptor.IsEnabled)
                    {
                        _floatingWindowInterceptorManager.Start();
                    }
                    else
                    {
                        _floatingWindowInterceptorManager.Stop();
                    }
                }
                
                UpdateFloatingWindowInterceptorUI();
                SaveSettingsToFile();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"切换悬浮窗拦截主开关失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 希沃白板3拦截开关
        /// </summary>
        private void ToggleSwitchSeewoWhiteboard3Floating_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            SetInterceptRule(FloatingWindowInterceptor.InterceptType.SeewoWhiteboard3Floating, ToggleSwitchSeewoWhiteboard3Floating.IsOn);
        }

        /// <summary>
        /// 希沃白板5拦截开关
        /// </summary>
        private void ToggleSwitchSeewoWhiteboard5Floating_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            SetInterceptRule(FloatingWindowInterceptor.InterceptType.SeewoWhiteboard5Floating, ToggleSwitchSeewoWhiteboard5Floating.IsOn);
        }

        /// <summary>
        /// 希沃白板5C拦截开关
        /// </summary>
        private void ToggleSwitchSeewoWhiteboard5CFloating_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            SetInterceptRule(FloatingWindowInterceptor.InterceptType.SeewoWhiteboard5CFloating, ToggleSwitchSeewoWhiteboard5CFloating.IsOn);
        }

        /// <summary>
        /// 希沃品课侧栏拦截开关
        /// </summary>
        private void ToggleSwitchSeewoPincoSideBarFloating_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            SetInterceptRule(FloatingWindowInterceptor.InterceptType.SeewoPincoSideBarFloating, ToggleSwitchSeewoPincoSideBarFloating.IsOn);
        }

        /// <summary>
        /// 希沃品课画笔拦截开关
        /// </summary>
        private void ToggleSwitchSeewoPincoDrawingFloating_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            SetInterceptRule(FloatingWindowInterceptor.InterceptType.SeewoPincoDrawingFloating, ToggleSwitchSeewoPincoDrawingFloating.IsOn);
        }

        /// <summary>
        /// 希沃PPT小工具拦截开关
        /// </summary>
        private void ToggleSwitchSeewoPPTFloating_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            SetInterceptRule(FloatingWindowInterceptor.InterceptType.SeewoPPTFloating, ToggleSwitchSeewoPPTFloating.IsOn);
        }

        /// <summary>
        /// AiClass拦截开关
        /// </summary>
        private void ToggleSwitchAiClassFloating_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            SetInterceptRule(FloatingWindowInterceptor.InterceptType.AiClassFloating, ToggleSwitchAiClassFloating.IsOn);
        }

        /// <summary>
        /// 鸿合屏幕书写拦截开关
        /// </summary>
        private void ToggleSwitchHiteAnnotationFloating_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            SetInterceptRule(FloatingWindowInterceptor.InterceptType.HiteAnnotationFloating, ToggleSwitchHiteAnnotationFloating.IsOn);
        }

        /// <summary>
        /// 畅言智慧课堂拦截开关
        /// </summary>
        private void ToggleSwitchChangYanFloating_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            SetInterceptRule(FloatingWindowInterceptor.InterceptType.ChangYanFloating, ToggleSwitchChangYanFloating.IsOn);
        }

        /// <summary>
        /// 畅言PPT拦截开关
        /// </summary>
        private void ToggleSwitchChangYanPptFloating_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            SetInterceptRule(FloatingWindowInterceptor.InterceptType.ChangYanPptFloating, ToggleSwitchChangYanPptFloating.IsOn);
        }

        /// <summary>
        /// 天喻教育云拦截开关
        /// </summary>
        private void ToggleSwitchIntelligentClassFloating_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            SetInterceptRule(FloatingWindowInterceptor.InterceptType.IntelligentClassFloating, ToggleSwitchIntelligentClassFloating.IsOn);
        }

        /// <summary>
        /// 希沃桌面画笔拦截开关
        /// </summary>
        private void ToggleSwitchSeewoDesktopAnnotationFloating_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            SetInterceptRule(FloatingWindowInterceptor.InterceptType.SeewoDesktopAnnotationFloating, ToggleSwitchSeewoDesktopAnnotationFloating.IsOn);
        }

        /// <summary>
        /// 希沃桌面侧栏拦截开关
        /// </summary>
        private void ToggleSwitchSeewoDesktopSideBarFloating_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            SetInterceptRule(FloatingWindowInterceptor.InterceptType.SeewoDesktopSideBarFloating, ToggleSwitchSeewoDesktopSideBarFloating.IsOn);
        }

        /// <summary>
        /// 设置拦截规则
        /// </summary>
        private void SetInterceptRule(FloatingWindowInterceptor.InterceptType type, bool enabled)
        {
            try
            {
                if (_floatingWindowInterceptorManager != null)
                {
                    _floatingWindowInterceptorManager.SetInterceptRule(type, enabled);
                }
                
                // 更新设置
                var ruleName = type.ToString();
                if (Settings.Automation.FloatingWindowInterceptor.InterceptRules.ContainsKey(ruleName))
                {
                    Settings.Automation.FloatingWindowInterceptor.InterceptRules[ruleName] = enabled;
                }
                
                SaveSettingsToFile();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"设置拦截规则失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 启动拦截按钮点击
        /// </summary>
        private void BtnFloatingWindowInterceptorStart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_floatingWindowInterceptorManager != null)
                {
                    _floatingWindowInterceptorManager.Start();
                    UpdateFloatingWindowInterceptorUI();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"启动悬浮窗拦截失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 停止拦截按钮点击
        /// </summary>
        private void BtnFloatingWindowInterceptorStop_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_floatingWindowInterceptorManager != null)
                {
                    _floatingWindowInterceptorManager.Stop();
                    UpdateFloatingWindowInterceptorUI();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"停止悬浮窗拦截失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 手动扫描按钮点击
        /// </summary>
        private void BtnFloatingWindowInterceptorScan_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_floatingWindowInterceptorManager != null)
                {
                    _floatingWindowInterceptorManager.ScanOnce();
                    UpdateFloatingWindowInterceptorUI();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"手动扫描失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 恢复所有窗口按钮点击
        /// </summary>
        private void BtnFloatingWindowInterceptorRestore_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_floatingWindowInterceptorManager != null)
                {
                    _floatingWindowInterceptorManager.RestoreAllWindows();
                    UpdateFloatingWindowInterceptorUI();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"恢复所有窗口失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 调试列出窗口按钮点击
        /// </summary>
        private void BtnFloatingWindowInterceptorDebug_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_floatingWindowInterceptorManager != null)
                {
                    // 更新状态显示
                    TextBlockFloatingWindowInterceptorStatus.Text = "正在调试扫描窗口...";
                    
                    _floatingWindowInterceptorManager.DebugListAllWindows();
                    
                    // 更新状态显示
                    TextBlockFloatingWindowInterceptorStatus.Text = "调试扫描完成，请查看日志文件";
                    
                    LogHelper.WriteLogToFile("用户点击了调试列出窗口按钮", LogHelper.LogType.Event);
                }
                else
                {
                    LogHelper.WriteLogToFile("拦截器管理器未初始化", LogHelper.LogType.Warning);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"调试列出窗口失败: {ex.Message}", LogHelper.LogType.Error);
                TextBlockFloatingWindowInterceptorStatus.Text = "调试扫描失败";
            }
        }

        /// <summary>
        /// 测试拦截按钮点击
        /// </summary>
        private void BtnFloatingWindowInterceptorTest_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_floatingWindowInterceptorManager != null)
                {
                    // 更新状态显示
                    TextBlockFloatingWindowInterceptorStatus.Text = "正在测试拦截功能...";
                    
                    // 启用测试规则
                    _floatingWindowInterceptorManager.SetInterceptRule(FloatingWindowInterceptor.InterceptType.SeewoWhiteboard3Floating, true);
                    
                    // 执行一次扫描
                    _floatingWindowInterceptorManager.ScanOnce();
                    
                    // 更新状态显示
                    TextBlockFloatingWindowInterceptorStatus.Text = "测试拦截完成，请查看日志文件";
                    
                    LogHelper.WriteLogToFile("用户点击了测试拦截按钮", LogHelper.LogType.Event);
                }
                else
                {
                    LogHelper.WriteLogToFile("拦截器管理器未初始化", LogHelper.LogType.Warning);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"测试拦截失败: {ex.Message}", LogHelper.LogType.Error);
                TextBlockFloatingWindowInterceptorStatus.Text = "测试拦截失败";
            }
        }

        #endregion
    }
}