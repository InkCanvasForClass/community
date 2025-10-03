using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ink_Canvas
{
    /// <summary>
    /// 悬浮窗拦截管理器
    /// </summary>
    public class FloatingWindowInterceptorManager : IDisposable
    {
        #region 私有字段

        private FloatingWindowInterceptor _interceptor;
        private bool _isInitialized;
        private bool _disposed;
        private FloatingWindowInterceptorSettings _settings;

        #endregion

        #region 事件

        public event EventHandler<FloatingWindowInterceptor.WindowInterceptedEventArgs> WindowIntercepted;
        public event EventHandler<FloatingWindowInterceptor.WindowRestoredEventArgs> WindowRestored;

        #endregion

        #region 公共属性

        public bool IsEnabled => _interceptor != null && _settings != null && _settings.IsEnabled;
        public bool IsRunning => _interceptor != null && _interceptor.IsRunning;

        #endregion

        #region 公共方法

        /// <summary>
        /// 初始化拦截器
        /// </summary>
        public void Initialize(FloatingWindowInterceptorSettings settings)
        {
            if (_isInitialized) return;

            try
            {
                _settings = settings ?? new FloatingWindowInterceptorSettings();
                _interceptor = new FloatingWindowInterceptor();

                // 订阅事件
                _interceptor.WindowIntercepted += OnWindowIntercepted;
                _interceptor.WindowRestored += OnWindowRestored;

                // 应用配置
                ApplySettings();

                _isInitialized = true;

                // 如果设置了自动启动，则启动拦截器
                if (_settings.AutoStart && _settings.IsEnabled)
                {
                    Start();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"初始化悬浮窗拦截器失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 启动拦截器
        /// </summary>
        public void Start()
        {
            if (!_isInitialized || _settings == null) return;

            if (_interceptor == null) return;

            try
            {
                _interceptor.Start(_settings.ScanIntervalMs);
                LogHelper.WriteLogToFile("悬浮窗拦截器已启动", LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"启动悬浮窗拦截器失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 停止拦截器
        /// </summary>
        public void Stop()
        {
            if (_interceptor == null) return;

            try
            {
                _interceptor.Stop();
                LogHelper.WriteLogToFile("悬浮窗拦截器已停止", LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"停止悬浮窗拦截器失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 设置拦截规则
        /// </summary>
        public void SetInterceptRule(FloatingWindowInterceptor.InterceptType type, bool enabled)
        {
            if (_interceptor == null || _settings == null) return;

            try
            {
                _interceptor.SetInterceptRule(type, enabled);

                // 更新设置
                var ruleName = type.ToString();
                if (_settings.InterceptRules.ContainsKey(ruleName))
                {
                    _settings.InterceptRules[ruleName] = enabled;
                }

                // 获取规则信息以处理父子关系
                var rule = _interceptor.GetInterceptRule(type);
                if (rule != null)
                {
                    // 如果是父规则，更新所有子规则的设置
                    if (rule.ChildTypes.Count > 0)
                    {
                        foreach (var childType in rule.ChildTypes)
                        {
                            var childRuleName = childType.ToString();
                            if (_settings.InterceptRules.ContainsKey(childRuleName))
                            {
                                _settings.InterceptRules[childRuleName] = enabled;
                            }
                        }
                    }
                    // 如果是子规则，更新父规则的设置
                    else if (rule.ParentType.HasValue)
                    {
                        var parentRule = _interceptor.GetInterceptRule(rule.ParentType.Value);
                        if (parentRule != null)
                        {
                            var parentRuleName = rule.ParentType.Value.ToString();
                            if (_settings.InterceptRules.ContainsKey(parentRuleName))
                            {
                                _settings.InterceptRules[parentRuleName] = parentRule.IsEnabled;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"设置拦截规则失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 获取拦截规则
        /// </summary>
        public FloatingWindowInterceptor.InterceptRule GetInterceptRule(FloatingWindowInterceptor.InterceptType type)
        {
            return _interceptor?.GetInterceptRule(type);
        }

        /// <summary>
        /// 获取所有拦截规则
        /// </summary>
        public Dictionary<FloatingWindowInterceptor.InterceptType, FloatingWindowInterceptor.InterceptRule> GetAllRules()
        {
            return _interceptor?.GetAllRules() ?? new Dictionary<FloatingWindowInterceptor.InterceptType, FloatingWindowInterceptor.InterceptRule>();
        }

        /// <summary>
        /// 手动扫描一次
        /// </summary>
        public void ScanOnce()
        {
            if (_interceptor == null) return;

            try
            {
                _interceptor.ScanOnce();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"手动扫描失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }


        /// <summary>
        /// 恢复所有被拦截的窗口
        /// </summary>
        public void RestoreAllWindows()
        {
            if (_interceptor == null) return;

            try
            {
                _interceptor.RestoreAllWindows();
                LogHelper.WriteLogToFile("已恢复所有被拦截的窗口", LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"恢复窗口失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 应用设置
        /// </summary>
        public void ApplySettings()
        {
            if (_interceptor == null || _settings == null) return;

            try
            {
                // 应用拦截规则设置
                foreach (var kvp in _settings.InterceptRules)
                {
                    if (Enum.TryParse<FloatingWindowInterceptor.InterceptType>(kvp.Key, out var type))
                    {
                        _interceptor.SetInterceptRule(type, kvp.Value);
                    }
                }

                // 如果启用了拦截器，则启动
                if (_settings.IsEnabled && !IsRunning)
                {
                    Start();
                }
                // 如果禁用了拦截器，则停止
                else if (!_settings.IsEnabled && IsRunning)
                {
                    Stop();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用设置失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 更新扫描间隔
        /// </summary>
        public void UpdateScanInterval(int intervalMs)
        {
            if (_interceptor == null || _settings == null) return;

            try
            {
                _settings.ScanIntervalMs = intervalMs;

                // 如果正在运行，重启以应用新间隔
                if (IsRunning)
                {
                    Stop();
                    Start();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更新扫描间隔失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 获取拦截统计信息
        /// </summary>
        public InterceptStatistics GetStatistics()
        {
            if (_interceptor == null || _settings == null) return new InterceptStatistics();

            try
            {
                var rules = GetAllRules();
                var enabledRules = rules.Count(r => r.Value.IsEnabled);
                var totalRules = rules.Count;

                return new InterceptStatistics
                {
                    TotalRules = totalRules,
                    EnabledRules = enabledRules,
                    IsRunning = IsRunning,
                    ScanIntervalMs = _settings.ScanIntervalMs
                };
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"获取统计信息失败: {ex.Message}", LogHelper.LogType.Error);
                return new InterceptStatistics();
            }
        }

        #endregion

        #region 私有方法

        private void OnWindowIntercepted(object sender, FloatingWindowInterceptor.WindowInterceptedEventArgs e)
        {
            try
            {
                // 记录日志
                LogHelper.WriteLogToFile($"拦截窗口: {e.WindowTitle} ({e.InterceptType})", LogHelper.LogType.Event);

                // 显示通知（如果启用）
                if (_settings != null && _settings.ShowNotifications)
                {
                    ShowNotification($"已拦截悬浮窗: {e.Rule.Description}");
                }

                // 触发事件
                WindowIntercepted?.Invoke(this, e);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理窗口拦截事件失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void OnWindowRestored(object sender, FloatingWindowInterceptor.WindowRestoredEventArgs e)
        {
            try
            {
                // 记录日志
                LogHelper.WriteLogToFile($"恢复窗口: {e.InterceptType}", LogHelper.LogType.Event);

                // 触发事件
                WindowRestored?.Invoke(this, e);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理窗口恢复事件失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void ShowNotification(string message)
        {
            try
            {
                // 这里可以集成系统通知或自定义通知
                // 暂时使用调试输出
                System.Diagnostics.Debug.WriteLine($"通知: {message}");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"显示通知失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        #endregion

        #region 辅助类

        public class InterceptStatistics
        {
            public int TotalRules { get; set; }
            public int EnabledRules { get; set; }
            public bool IsRunning { get; set; }
            public int ScanIntervalMs { get; set; }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                Stop();
                _interceptor?.Dispose();
                _interceptor = null;
                _isInitialized = false;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"释放悬浮窗拦截器失败: {ex.Message}", LogHelper.LogType.Error);
            }
            finally
            {
                _disposed = true;
            }
        }

        #endregion
    }
}