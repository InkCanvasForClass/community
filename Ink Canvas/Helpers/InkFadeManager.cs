using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Ink;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 墨迹渐隐管理器 - 管理墨迹的渐隐动画和状态
    /// </summary>
    public class InkFadeManager
    {
        #region Properties
        /// <summary>
        /// 是否启用墨迹渐隐功能
        /// </summary>
        public bool IsEnabled { get; set; } = false;

        /// <summary>
        /// 墨迹渐隐时间（毫秒）
        /// </summary>
        public int FadeTime { get; set; } = 3000;

        /// <summary>
        /// 渐隐动画持续时间（毫秒）
        /// </summary>
        public int AnimationDuration { get; set; } = 1000;
        #endregion

        #region Private Fields
        private readonly MainWindow _mainWindow;
        private readonly Dispatcher _dispatcher;
        private readonly Dictionary<Stroke, DispatcherTimer> _fadeTimers;
        private readonly Dictionary<Stroke, UIElement> _strokeVisuals;
        #endregion

        #region Constructor
        public InkFadeManager(MainWindow mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _dispatcher = _mainWindow.Dispatcher;
            _fadeTimers = new Dictionary<Stroke, DispatcherTimer>();
            _strokeVisuals = new Dictionary<Stroke, UIElement>();
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// 添加需要渐隐的墨迹
        /// </summary>
        /// <param name="stroke">墨迹对象</param>
        /// <param name="visual">墨迹的视觉元素</param>
        public void AddFadingStroke(Stroke stroke, UIElement visual)
        {
            if (!IsEnabled || stroke == null || visual == null) return;

            try
            {
                // 记录墨迹和视觉元素的对应关系
                _strokeVisuals[stroke] = visual;

                // 创建定时器，在指定时间后开始渐隐动画
                var timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(FadeTime)
                };

                timer.Tick += (sender, e) =>
                {
                    StartFadeAnimation(stroke);
                    timer.Stop();
                    _fadeTimers.Remove(stroke);
                };

                _fadeTimers[stroke] = timer;
                timer.Start();

                // 将视觉元素添加到画布上
                _dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        if (_mainWindow.inkCanvas != null)
                        {
                            _mainWindow.inkCanvas.Children.Add(visual);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"添加墨迹视觉元素到画布失败: {ex}", LogHelper.LogType.Error);
                    }
                });
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"添加渐隐墨迹失败: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 移除墨迹
        /// </summary>
        /// <param name="stroke">要移除的墨迹</param>
        public void RemoveStroke(Stroke stroke)
        {
            if (stroke == null) return;

            try
            {
                if (_fadeTimers.TryGetValue(stroke, out var timer))
                {
                    timer.Stop();
                    _fadeTimers.Remove(stroke);
                }

                if (_strokeVisuals.TryGetValue(stroke, out var visual))
                {
                    _dispatcher.InvokeAsync(() =>
                    {
                        try
                        {
                            if (_mainWindow.inkCanvas != null && _mainWindow.inkCanvas.Children.Contains(visual))
                            {
                                _mainWindow.inkCanvas.Children.Remove(visual);
                            }
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"从画布移除墨迹视觉元素失败: {ex}", LogHelper.LogType.Error);
                        }
                    });

                    _strokeVisuals.Remove(stroke);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"移除渐隐墨迹失败: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 清除所有渐隐墨迹
        /// </summary>
        public void ClearAllFadingStrokes()
        {
            try
            {
                foreach (var timer in _fadeTimers.Values)
                {
                    timer.Stop();
                }

                _fadeTimers.Clear();

                _dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        if (_mainWindow.inkCanvas != null)
                        {
                            foreach (var visual in _strokeVisuals.Values)
                            {
                                if (_mainWindow.inkCanvas.Children.Contains(visual))
                                {
                                    _mainWindow.inkCanvas.Children.Remove(visual);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"清除所有墨迹视觉元素失败: {ex}", LogHelper.LogType.Error);
                    }
                });

                _strokeVisuals.Clear();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"清除所有渐隐墨迹失败: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 更新渐隐时间设置
        /// </summary>
        /// <param name="fadeTime">新的渐隐时间（毫秒）</param>
        public void UpdateFadeTime(int fadeTime)
        {
            FadeTime = fadeTime;
            
            foreach (var kvp in _fadeTimers)
            {
                var stroke = kvp.Key;
                var timer = kvp.Value;
                
                timer.Stop();
                timer.Interval = TimeSpan.FromMilliseconds(FadeTime);
                timer.Start();
            }
        }

        /// <summary>
        /// 更新动画持续时间设置
        /// </summary>
        /// <param name="animationDuration">新的动画持续时间（毫秒）</param>
        public void UpdateAnimationDuration(int animationDuration)
        {
            AnimationDuration = animationDuration;
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// 开始渐隐动画
        /// </summary>
        /// <param name="stroke">要渐隐的墨迹</param>
        private void StartFadeAnimation(Stroke stroke)
        {
            if (!_strokeVisuals.TryGetValue(stroke, out var visual)) return;

            try
            {
                _dispatcher.InvokeAsync(() =>
                {
                    var fadeAnimation = new DoubleAnimation
                    {
                        From = 1.0,
                        To = 0.0,
                        Duration = TimeSpan.FromMilliseconds(AnimationDuration),
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
                    };

                    fadeAnimation.Completed += (sender, e) =>
                    {
                        try
                        {
                            if (_mainWindow.inkCanvas != null && _mainWindow.inkCanvas.Children.Contains(visual))
                            {
                                _mainWindow.inkCanvas.Children.Remove(visual);
                            }

                            RemoveStroke(stroke);
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"渐隐动画完成后清理墨迹失败: {ex}", LogHelper.LogType.Error);
                        }
                    };

                    visual.BeginAnimation(UIElement.OpacityProperty, fadeAnimation);
                });
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"开始渐隐动画失败: {ex}", LogHelper.LogType.Error);
            }
        }
        #endregion
    }
} 