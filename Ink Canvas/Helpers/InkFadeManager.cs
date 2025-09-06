using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

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
        public bool IsEnabled { get; set; }

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
        private readonly Dictionary<Stroke, Point> _strokeStartPoints;
        private readonly Dictionary<Stroke, Point> _strokeEndPoints;
        #endregion

        #region Constructor
        public InkFadeManager(MainWindow mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _dispatcher = _mainWindow.Dispatcher;
            _fadeTimers = new Dictionary<Stroke, DispatcherTimer>();
            _strokeVisuals = new Dictionary<Stroke, UIElement>();
            _strokeStartPoints = new Dictionary<Stroke, Point>();
            _strokeEndPoints = new Dictionary<Stroke, Point>();
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// 添加需要渐隐的墨迹
        /// </summary>
        /// <param name="stroke">墨迹对象</param>
        /// <param name="startPoint">落笔点</param>
        /// <param name="endPoint">抬笔点</param>
        public void AddFadingStroke(Stroke stroke, Point startPoint, Point endPoint)
        {
            if (!IsEnabled || stroke == null)
            {
                return;
            }

            try
            {

                // 记录墨迹的起点和终点
                _strokeStartPoints[stroke] = startPoint;
                _strokeEndPoints[stroke] = endPoint;

                // 创建墨迹的视觉元素（湿墨迹状态）
                var strokeVisual = CreateStrokeVisual(stroke);
                if (strokeVisual == null) return;

                _strokeVisuals[stroke] = strokeVisual;

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
                            // 将墨迹添加到 inkCanvas 的父容器中，而不是 inkCanvas.Children
                            // 这样可以避免坐标系统问题
                            var parent = _mainWindow.inkCanvas.Parent as Panel;
                            if (parent != null)
                            {
                                parent.Children.Add(strokeVisual);
                            }
                            else
                            {
                                // 如果无法获取父容器，则添加到 inkCanvas.Children
                                _mainWindow.inkCanvas.Children.Add(strokeVisual);
                            }
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
                            // 从父容器中移除墨迹
                            var parent = _mainWindow.inkCanvas?.Parent as Panel;
                            if (parent != null && parent.Children.Contains(visual))
                            {
                                parent.Children.Remove(visual);
                            }
                            else if (_mainWindow.inkCanvas != null && _mainWindow.inkCanvas.Children.Contains(visual))
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

                _strokeStartPoints.Remove(stroke);
                _strokeEndPoints.Remove(stroke);
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
                            var parent = _mainWindow.inkCanvas.Parent as Panel;
                            foreach (var visual in _strokeVisuals.Values)
                            {
                                if (parent != null && parent.Children.Contains(visual))
                                {
                                    parent.Children.Remove(visual);
                                }
                                else if (_mainWindow.inkCanvas.Children.Contains(visual))
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
                _strokeStartPoints.Clear();
                _strokeEndPoints.Clear();
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
        /// 启用墨迹渐隐功能
        /// </summary>
        public void Enable()
        {
            IsEnabled = true;
            LogHelper.WriteLogToFile("墨迹渐隐功能已启用");
        }

        /// <summary>
        /// 禁用墨迹渐隐功能
        /// </summary>
        public void Disable()
        {
            IsEnabled = false;
            LogHelper.WriteLogToFile("墨迹渐隐功能已禁用");
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// 创建墨迹的视觉元素
        /// </summary>
        /// <param name="stroke">墨迹对象</param>
        /// <returns>视觉元素</returns>
        private UIElement CreateStrokeVisual(Stroke stroke)
        {
            try
            {
                // 创建路径几何，使用墨迹的实际位置
                var geometry = stroke.GetGeometry();
                if (geometry == null)
                {
                    return null;
                }

                // 获取绘画属性
                var drawingAttribs = stroke.DrawingAttributes;

                // 创建路径元素，确保使用正确的绘画属性
                var path = new Path
                {
                    Data = geometry,
                    Stroke = new SolidColorBrush(drawingAttribs.Color),
                    StrokeThickness = drawingAttribs.Width, // 使用原始墨迹的粗细
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    StrokeLineJoin = PenLineJoin.Round,
                    Fill = drawingAttribs.IsHighlighter ? new SolidColorBrush(drawingAttribs.Color) : null, // 高亮笔需要填充
                    Opacity = 0.95, // 初始透明度更高，显得更自然

                    // 优化渲染质量
                    UseLayoutRounding = false,
                    SnapsToDevicePixels = false
                };

                // 如果是高亮笔，调整透明度和混合模式
                if (drawingAttribs.IsHighlighter)
                {
                    path.Opacity = 0.4; // 高亮笔初始透明度更低，更符合荧光笔特性

                    // 为高亮笔添加特殊的混合效果
                    // 使用更柔和的笔触样式
                    path.StrokeStartLineCap = PenLineCap.Flat;
                    path.StrokeEndLineCap = PenLineCap.Flat;
                    path.StrokeLineJoin = PenLineJoin.Miter;

                    // 高亮笔通常需要更宽的笔触来覆盖下面的内容
                    if (drawingAttribs.Width < 20)
                    {
                        path.StrokeThickness = Math.Max(drawingAttribs.Width * 1.5, 20);
                    }
                }

                // 不设置任何变换，保持墨迹原有粗细
                var bounds = geometry.Bounds;

                // 设置墨迹的初始位置
                System.Windows.Controls.Canvas.SetLeft(path, bounds.Left);
                System.Windows.Controls.Canvas.SetTop(path, bounds.Top);

                return path;
            }
            catch (Exception)
            {
                return null;
            }
        }

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
                    // 获取当前透明度和判断是否为高亮笔
                    var currentOpacity = visual.Opacity;
                    var isHighlighter = stroke.DrawingAttributes.IsHighlighter;

                    // 根据墨迹类型选择不同的动画效果
                    if (isHighlighter)
                    {
                        StartHighlighterFadeAnimation(visual, stroke, currentOpacity);
                    }
                    else
                    {
                        StartNormalStrokeFadeAnimation(visual, stroke, currentOpacity);
                    }
                });
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"开始渐隐动画失败: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 开始普通墨迹的渐隐动画
        /// </summary>
        private void StartNormalStrokeFadeAnimation(UIElement visual, Stroke stroke, double currentOpacity)
        {
            try
            {
                // 对于普通墨迹也使用连续渐隐动画
                StartContinuousFadeAnimation(visual, stroke, currentOpacity, AnimationDuration);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"开始普通墨迹渐隐动画失败: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 开始高亮笔的渐隐动画
        /// </summary>
        private void StartHighlighterFadeAnimation(UIElement visual, Stroke stroke, double currentOpacity)
        {
            try
            {
                // 使用连续渐隐动画而不是分段动画
                StartContinuousFadeAnimation(visual, stroke, currentOpacity, (int)(AnimationDuration * 1.5));
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"开始高亮笔渐隐动画失败: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 渐进式渐隐动画 - 从起点到终点逐渐消失
        /// </summary>
        private void StartProgressiveFadeAnimation(UIElement visual, Stroke stroke, double currentOpacity, int duration)
        {
            try
            {
                // 确保所有墨迹都能显示动画，包括短墨迹
                if (stroke.StylusPoints.Count < 2)
                {
                    // 只有1个点的墨迹也使用分段动画，确保视觉效果
                    CreateSegmentedStroke(visual, stroke, currentOpacity, duration);
                    return;
                }

                // 将墨迹分段并创建多个 Path
                CreateSegmentedStroke(visual, stroke, currentOpacity, duration);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"渐进式渐隐动画失败: {ex}", LogHelper.LogType.Error);
                // 失败时回退到简单动画
                StartSimpleFadeAnimation(visual, stroke, currentOpacity, duration);
            }
        }

        /// <summary>
        /// 创建分段墨迹并开始渐进消失
        /// </summary>
        private void CreateSegmentedStroke(UIElement originalVisual, Stroke stroke, double opacity, int duration)
        {
            try
            {
                var stylusPoints = stroke.StylusPoints;
                var totalPoints = stylusPoints.Count;

                // 分段算法 - 确保所有墨迹都有足够的动画效果
                var strokeLength = CalculateStrokeLength(stylusPoints);
                var segmentCount = CalculateOptimalSegmentCount(totalPoints, strokeLength);

                // 强制最小分段数量，确保短墨迹也有动画效果
                segmentCount = Math.Max(segmentCount, 4);

                var pointsPerSegment = Math.Max(1, totalPoints / segmentCount);

                // 隐藏原始视觉元素
                originalVisual.Visibility = Visibility.Hidden;

                var segments = new List<UIElement>();
                var parent = _mainWindow.inkCanvas?.Parent as Panel;
                if (parent == null)
                {
                    // 如果父容器不是Panel，直接使用InkCanvas
                    parent = null; // 稍后会检查并使用InkCanvas.Children
                }

                // 创建各个分段 - 确保短墨迹也能正确分段
                for (int i = 0; i < segmentCount; i++)
                {
                    var startIndex = i * pointsPerSegment;
                    var endIndex = (i == segmentCount - 1) ? totalPoints - 1 : (i + 1) * pointsPerSegment;

                    // 确保有足够的点来创建分段，对于短墨迹特殊处理
                    if (endIndex <= startIndex && totalPoints > 1)
                    {
                        // 短墨迹：每个点作为一个分段
                        startIndex = i;
                        endIndex = Math.Min(i + 1, totalPoints - 1);
                    }

                    // 为每个分段添加重叠，确保连接处平滑
                    var overlap = Math.Max(1, pointsPerSegment / 6); // 15%的重叠，平衡平滑与速度
                    var actualStartIndex = Math.Max(0, startIndex - overlap);
                    var actualEndIndex = Math.Min(totalPoints - 1, endIndex + overlap);

                    var segment = CreateStrokeSegment(stroke, actualStartIndex, actualEndIndex, opacity);
                    if (segment != null)
                    {
                        segments.Add(segment);
                        if (parent != null)
                        {
                            parent.Children.Add(segment);
                        }
                        else if (_mainWindow.inkCanvas != null)
                        {
                            _mainWindow.inkCanvas.Children.Add(segment);
                        }
                    }
                }

                // 开始分段渐隐动画
                StartSegmentedFadeAnimation(segments, stroke, originalVisual, duration);
            }
            catch (Exception)
            {
                StartSimpleFadeAnimation(originalVisual, stroke, opacity, duration);
            }
        }

        /// <summary>
        /// 创建墨迹分段
        /// </summary>
        private UIElement CreateStrokeSegment(Stroke originalStroke, int startIndex, int endIndex, double opacity)
        {
            try
            {
                // 创建分段的 StylusPoint 集合
                var segmentPoints = new StylusPointCollection();
                for (int i = startIndex; i <= endIndex && i < originalStroke.StylusPoints.Count; i++)
                {
                    segmentPoints.Add(originalStroke.StylusPoints[i]);
                }

                if (segmentPoints.Count < 2) return null;

                // 创建分段墨迹
                var segmentStroke = new Stroke(segmentPoints)
                {
                    DrawingAttributes = originalStroke.DrawingAttributes.Clone()
                };

                // 创建分段的视觉元素
                var geometry = segmentStroke.GetGeometry();
                if (geometry == null) return null;

                var drawingAttribs = segmentStroke.DrawingAttributes;
                var path = new Path
                {
                    Data = geometry,
                    Stroke = new SolidColorBrush(drawingAttribs.Color),
                    StrokeThickness = drawingAttribs.Width,
                    StrokeStartLineCap = drawingAttribs.IsHighlighter ? PenLineCap.Flat : PenLineCap.Round,
                    StrokeEndLineCap = drawingAttribs.IsHighlighter ? PenLineCap.Flat : PenLineCap.Round,
                    StrokeLineJoin = drawingAttribs.IsHighlighter ? PenLineJoin.Miter : PenLineJoin.Round,
                    Fill = drawingAttribs.IsHighlighter ? new SolidColorBrush(drawingAttribs.Color) : null,
                    Opacity = opacity,
                    UseLayoutRounding = false,
                    SnapsToDevicePixels = false
                };

                // 设置位置
                var bounds = geometry.Bounds;
                System.Windows.Controls.Canvas.SetLeft(path, bounds.Left);
                System.Windows.Controls.Canvas.SetTop(path, bounds.Top);

                return path;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// 开始分段渐隐动画
        /// </summary>
        private void StartSegmentedFadeAnimation(List<UIElement> segments, Stroke originalStroke, UIElement originalVisual, int totalDuration)
        {
            try
            {
                // 动画时序算法
                var segmentDuration = CalculateOptimalSegmentDuration(totalDuration, segments.Count);
                var animationCurve = CreateAppleStyleAnimationCurve(segments.Count, totalDuration);

                // 跟踪动画完成状态
                var completedSegments = new HashSet<UIElement>();
                var totalSegments = segments.Count;

                // 渐隐效果 - 使用自然的动画曲线
                for (int i = 0; i < segments.Count; i++)
                {
                    var segment = segments[i];

                    // 使用预计算的动画曲线获取延迟时间
                    var delay = animationCurve[i];

                    // 使用定时器延迟启动每个分段的动画
                    var timer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(delay)
                    };

                    int segmentIndex = i; // 捕获当前索引
                    timer.Tick += (sender, e) =>
                    {
                        StartSingleSegmentFadeAnimation(segment, segmentDuration, () =>
                        {
                            // 动画完成回调
                            lock (completedSegments)
                            {
                                completedSegments.Add(segment);

                                // 检查是否所有分段都完成了
                                if (completedSegments.Count >= totalSegments)
                                {
                                    CleanupSegmentedAnimation(segments, originalStroke, originalVisual);
                                }
                            }
                        });
                        timer.Stop();
                    };

                    timer.Start();
                }

                // 设置一个安全超时定时器，防止无限等待
                var safetyTimeout = totalDuration + (segments.Count * segmentDuration) + 1200; // 额外1.2秒缓冲，确保动画完整
                var safetyTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(safetyTimeout)
                };

                safetyTimer.Tick += (sender, e) =>
                {
                    CleanupSegmentedAnimation(segments, originalStroke, originalVisual);
                    safetyTimer.Stop();
                };

                safetyTimer.Start();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"分段渐隐动画失败: {ex}", LogHelper.LogType.Error);
                CleanupSegmentedAnimation(segments, originalStroke, originalVisual);
            }
        }

        /// <summary>
        /// 单个分段的渐隐动画
        /// </summary>
        private void StartSingleSegmentFadeAnimation(UIElement segment, int duration, Action onCompleted = null)
        {
            try
            {
                // 只使用透明度动画，保持墨迹原有粗细
                var fadeAnimation = new DoubleAnimation
                {
                    From = segment.Opacity,
                    To = 0.0,
                    Duration = TimeSpan.FromMilliseconds(duration),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } // 更平滑的缓动
                };

                // 添加动画完成事件
                if (onCompleted != null)
                {
                    fadeAnimation.Completed += (sender, e) =>
                    {
                        onCompleted?.Invoke();
                    };
                }

                // 只应用透明度动画，不改变墨迹大小
                segment.BeginAnimation(UIElement.OpacityProperty, fadeAnimation);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"单个分段渐隐动画失败: {ex}", LogHelper.LogType.Error);
                // 即使失败也要调用完成回调
                onCompleted?.Invoke();
            }
        }

        /// <summary>
        /// 清理分段动画
        /// </summary>
        private void CleanupSegmentedAnimation(List<UIElement> segments, Stroke originalStroke, UIElement originalVisual)
        {
            try
            {
                // 移除所有分段
                var parent = _mainWindow.inkCanvas?.Parent as Panel;

                foreach (var segment in segments)
                {
                    if (parent != null && parent.Children.Contains(segment))
                    {
                        parent.Children.Remove(segment);
                    }
                    else if (_mainWindow.inkCanvas != null && _mainWindow.inkCanvas.Children.Contains(segment))
                    {
                        _mainWindow.inkCanvas.Children.Remove(segment);
                    }
                }

                // 清理原始墨迹
                OnAnimationCompleted(originalVisual, originalStroke);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"清理分段动画失败: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 简单渐隐动画（备用方案）
        /// </summary>
        private void StartSimpleFadeAnimation(UIElement visual, Stroke stroke, double currentOpacity, int duration)
        {
            try
            {
                var fadeAnimation = new DoubleAnimation
                {
                    From = currentOpacity,
                    To = 0.0,
                    Duration = TimeSpan.FromMilliseconds(duration),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                };

                fadeAnimation.Completed += (sender, e) => OnAnimationCompleted(visual, stroke);
                visual.BeginAnimation(UIElement.OpacityProperty, fadeAnimation);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"简单渐隐动画失败: {ex}", LogHelper.LogType.Error);
                OnAnimationCompleted(visual, stroke);
            }
        }

        /// <summary>
        /// 计算墨迹的实际长度
        /// </summary>
        private double CalculateStrokeLength(StylusPointCollection points)
        {
            if (points.Count < 2) return 0;

            double totalLength = 0;
            for (int i = 1; i < points.Count; i++)
            {
                var p1 = points[i - 1].ToPoint();
                var p2 = points[i].ToPoint();
                totalLength += Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
            }
            return totalLength;
        }

        /// <summary>
        /// 根据墨迹特性计算最优分段数量 - 平衡速度与完整性
        /// </summary>
        private int CalculateOptimalSegmentCount(int pointCount, double strokeLength)
        {
            // 平衡速度与完整性，确保动画效果的同时提高速度
            const double PIXELS_PER_SEGMENT = 12.0; // 每段适中长度，平衡效果与速度
            const int MIN_SEGMENTS = 5; // 适当的最小分段数，确保动画效果
            const int MAX_SEGMENTS = 100; // 适中的最大分段数，平衡性能与效果

            // 根据长度计算基础分段数
            var lengthBasedSegments = Math.Max(MIN_SEGMENTS, (int)(strokeLength / PIXELS_PER_SEGMENT));

            // 根据点密度调整，平衡效果与速度
            var density = pointCount > 0 ? strokeLength / pointCount : 1;
            var densityFactor = Math.Max(0.4, Math.Min(2.5, density / 1.8));

            var finalSegments = (int)(lengthBasedSegments * densityFactor);

            // 对于短墨迹，确保至少有4个分段
            if (pointCount <= 5)
            {
                finalSegments = Math.Max(finalSegments, 4);
            }

            // 限制在合理范围内
            return Math.Min(MAX_SEGMENTS, Math.Max(MIN_SEGMENTS, finalSegments));
        }

        /// <summary>
        /// 计算最优的单段动画持续时间 - 平衡速度与完整性
        /// </summary>
        private int CalculateOptimalSegmentDuration(int totalDuration, int segmentCount)
        {
            // 平衡速度与动画完整性
            var baseDuration = totalDuration / Math.Max(segmentCount, 1);
            var minDuration = 150; // 每段最少150ms，确保动画完整显示
            var maxDuration = 500; // 每段最多500ms，平衡速度与完整性

            return Math.Max(minDuration, Math.Min(maxDuration, baseDuration));
        }

        /// <summary>
        /// 创建优化的动画时间曲线 - 平衡速度与完整性
        /// </summary>
        private int[] CreateAppleStyleAnimationCurve(int segmentCount, int totalDuration)
        {
            var curve = new int[segmentCount];

            // 平衡速度与完整性，确保动画有足够时间播放
            var availableTime = totalDuration * 0.6; // 使用60%的总时间，给动画留足够缓冲
            var delayBetweenSegments = Math.Max(60, availableTime / Math.Max(segmentCount, 1));

            for (int i = 0; i < segmentCount; i++)
            {
                // 线性延迟，确保每个分段都有足够时间
                curve[i] = (int)(i * delayBetweenSegments);
            }

            return curve;
        }

        /// <summary>
        /// 连续渐隐动画 - 模拟橡皮擦擦除效果
        /// </summary>
        private void StartContinuousFadeAnimation(UIElement visual, Stroke stroke, double currentOpacity, int duration)
        {
            try
            {
                // 获取墨迹的边界
                var geometry = stroke.GetGeometry();
                if (geometry == null) return;

                var bounds = geometry.Bounds;
                var strokeStart = stroke.StylusPoints[0].ToPoint();
                var strokeEnd = stroke.StylusPoints[stroke.StylusPoints.Count - 1].ToPoint();

                // 计算墨迹的方向向量
                var direction = new Vector(strokeEnd.X - strokeStart.X, strokeEnd.Y - strokeStart.Y);
                var length = direction.Length;
                
                if (length == 0)
                {
                    // 如果墨迹没有方向（单点），使用简单渐隐
                    StartSimpleFadeAnimation(visual, stroke, currentOpacity, duration);
                    return;
                }

                // 归一化方向向量
                direction.Normalize();

                // 创建精确的擦除动画 - 使用PathGeometry来避免变形
                CreatePreciseEraseAnimation(visual, stroke, bounds, strokeStart, strokeEnd, direction, length, duration);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"连续渐隐动画失败: {ex}", LogHelper.LogType.Error);
                // 失败时回退到简单动画
                StartSimpleFadeAnimation(visual, stroke, currentOpacity, duration);
            }
        }

        /// <summary>
        /// 创建精确的擦除动画 - 使用动态裁剪区域避免墨迹移动
        /// </summary>
        private void CreatePreciseEraseAnimation(UIElement visual, Stroke stroke, Rect bounds, Point strokeStart, Point strokeEnd, Vector direction, double length, int duration)
        {
            try
            {
                // 计算擦除方向上的移动距离
                var totalDistance = Math.Sqrt(Math.Pow(strokeEnd.X - strokeStart.X, 2) + Math.Pow(strokeEnd.Y - strokeStart.Y, 2));
                
                if (totalDistance == 0)
                {
                    // 如果墨迹没有长度，使用简单渐隐
                    StartSimpleFadeAnimation(visual, stroke, visual.Opacity, duration);
                    return;
                }

                // 创建动态裁剪区域 - 从完整墨迹开始，逐渐缩小
                var clipGeometry = new RectangleGeometry
                {
                    Rect = bounds
                };

                visual.Clip = clipGeometry;

                // 创建擦除动画 - 裁剪区域从起点向终点移动并缩小
                // 使用更自然的擦除效果：从起点开始，逐渐向终点移动
                var eraseAnimation = new RectAnimation
                {
                    From = bounds, // 从完整边界开始
                    To = new Rect(strokeEnd.X, strokeEnd.Y, 0, bounds.Height), // 到终点位置，宽度为0
                    Duration = TimeSpan.FromMilliseconds(duration),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
                };

                // 添加动画完成事件
                eraseAnimation.Completed += (sender, e) =>
                {
                    OnAnimationCompleted(visual, stroke);
                };

                // 开始动画
                clipGeometry.BeginAnimation(RectangleGeometry.RectProperty, eraseAnimation);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"精确擦除动画失败: {ex}", LogHelper.LogType.Error);
                // 失败时回退到简单动画
                StartSimpleFadeAnimation(visual, stroke, visual.Opacity, duration);
            }
        }

        /// <summary>
        /// 动画完成后的统一处理
        /// </summary>
        private void OnAnimationCompleted(UIElement visual, Stroke stroke)
        {
            try
            {
                // 从父容器中移除墨迹
                var parent = _mainWindow.inkCanvas?.Parent as Panel;
                if (parent != null && parent.Children.Contains(visual))
                {
                    parent.Children.Remove(visual);
                }
                else if (_mainWindow.inkCanvas != null && _mainWindow.inkCanvas.Children.Contains(visual))
                {
                    _mainWindow.inkCanvas.Children.Remove(visual);
                }

                RemoveStroke(stroke);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"渐隐动画完成后清理墨迹失败: {ex}", LogHelper.LogType.Error);
            }
        }
        #endregion
    }
}