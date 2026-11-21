using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Ink_Canvas.Helpers
{
    internal class AnimationsHelper
    {
        private static readonly IntPtr HWND_TOP = new IntPtr(0);

        /// <summary>
        /// 强制刷新 Popup 的实际窗口位置（终极方案）
        /// 通过 Win32 API 直接操作窗口句柄
        /// </summary>
        public static void ForceRefreshPopupPosition(Popup popup)
        {
            if (popup?.Child == null || !popup.IsOpen) return;

            try
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        var source = PresentationSource.FromVisual(popup.Child) as HwndSource;
                        if (source?.Handle == null) return;

                        var hwnd = source.Handle;

                        // 获取当前窗口位置
                        if (PInvoke.GetWindowRect(new HWND(hwnd), out RECT rect))
                        {
                            // 使用相同的参数调用 SetWindowPos，但加上 SWP_SHOWWINDOW
                            // 这会强制窗口管理器重新评估并更新窗口位置
                            PInvoke.SetWindowPos(
                                new HWND(hwnd),
                                new HWND(HWND_TOP),
                                rect.left, rect.top,
                                rect.right - rect.left,
                                rect.bottom - rect.top,
                                SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW);

                            System.Diagnostics.Debug.WriteLine($"[PopupZOrder] Force refreshed position: ({rect.left}, {rect.top})");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[PopupZOrder] ForceRefreshPopupPosition failed: {ex.Message}");
                    }
                }), System.Windows.Threading.DispatcherPriority.Render);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PopupZOrder] ForceRefreshPopupPosition error: {ex.Message}");
            }
        }

        /// <summary>
        /// 将 Popup 窗口提升到最顶层，确保不被其他控件遮挡
        /// 采用多重策略确保置顶生效
        /// </summary>
        private static void BringPopupToFront(Popup popup)
        {
            try
            {
                if (popup?.Child == null) return;

                Action bringToTopAction = () =>
                {
                    try
                    {
                        var source = PresentationSource.FromVisual(popup.Child) as HwndSource;
                        if (source?.Handle == null) return;

                        var hwnd = source.Handle;

                        // 策略1：直接设置为 TOPMOST（最高优先级）
                        PInvoke.SetWindowPos(new HWND(hwnd), new HWND(NativeWindowHelper.HWND_TOPMOST),
                            0, 0, 0, 0,
                            SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW);

                        System.Diagnostics.Debug.WriteLine($"[PopupZOrder] Set TOPMOST for popup");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[PopupZOrder] BringPopupToFront failed: {ex.Message}");
                    }
                };

                // 立即执行第一次
                Application.Current.Dispatcher.BeginInvoke(bringToTopAction,
                    System.Windows.Threading.DispatcherPriority.Render);

                // 延迟 50ms 后再次执行（确保在其他窗口操作之后）
                Application.Current.Dispatcher.BeginInvoke(bringToTopAction,
                    System.Windows.Threading.DispatcherPriority.Normal);

                // 延迟 100ms 后第三次执行（最终确认）
                Application.Current.Dispatcher.BeginInvoke(bringToTopAction,
                    System.Windows.Threading.DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PopupZOrder] BringPopupToFront error: {ex.Message}");
            }
        }

        /// <summary>
        /// 轻量级置顶方法（用于拖动时的实时跟随）
        /// 仅执行一次置顶，避免性能问题
        /// </summary>
        public static void BringPopupToFrontLight(Popup popup)
        {
            try
            {
                if (popup?.Child == null) return;

                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        var source = PresentationSource.FromVisual(popup.Child) as HwndSource;
                        if (source?.Handle == null) return;

                        var hwnd = source.Handle;

                        PInvoke.SetWindowPos(new HWND(hwnd), new HWND(NativeWindowHelper.HWND_TOPMOST),
                            0, 0, 0, 0,
                            SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[PopupZOrder] BringPopupToFrontLight failed: {ex.Message}");
                    }
                }), System.Windows.Threading.DispatcherPriority.Render);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PopupZOrder] BringPopupToFrontLight error: {ex.Message}");
            }
        }

        private static UIElement ResolveAnimationTarget(UIElement element)
        {
            return element;
        }

        public static void ShowWithFadeIn(UIElement element, double duration = 0.15)
        {
            if (element.Visibility == Visibility.Visible) return;

            if (element == null)
                throw new ArgumentNullException(nameof(element));

            var sb = new Storyboard();

            // 渐变动画
            var fadeInAnimation = new DoubleAnimation
            {
                From = 0.5,
                To = 1,
                Duration = AnimationSpeed.ScaleToTimeSpan(duration)
            };
            Storyboard.SetTargetProperty(fadeInAnimation, new PropertyPath(UIElement.OpacityProperty));

            sb.Children.Add(fadeInAnimation);

            element.Visibility = Visibility.Visible;

            sb.Begin((FrameworkElement)element);
        }

        public static void ShowWithSlideFromBottomAndFade(UIElement element, double duration = 0.25)
        {
            try
            {
                if (element == null)
                    throw new ArgumentNullException(nameof(element));

                if (element.Visibility == Visibility.Visible) return;

                var target = ResolveAnimationTarget(element);

                // 初始化变换：设置起始位置（从下方20像素开始）
                var initialTransform = new TranslateTransform(0, 20);
                target.RenderTransform = initialTransform;
                target.Opacity = 0;

                element.Visibility = Visibility.Visible;

                var sb = new Storyboard();

                var fadeInAnimation = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = AnimationSpeed.ScaleToTimeSpan(duration)
                };
                fadeInAnimation.EasingFunction = new CubicEase();
                Storyboard.SetTarget(fadeInAnimation, target);
                Storyboard.SetTargetProperty(fadeInAnimation, new PropertyPath(UIElement.OpacityProperty));

                var slideAnimation = new DoubleAnimation
                {
                    From = 20,
                    To = 0,
                    Duration = AnimationSpeed.ScaleToTimeSpan(duration)
                };
                slideAnimation.EasingFunction = new CubicEase();
                Storyboard.SetTarget(slideAnimation, target);
                Storyboard.SetTargetProperty(slideAnimation, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));

                sb.Children.Add(fadeInAnimation);
                sb.Children.Add(slideAnimation);

                sb.Begin((FrameworkElement)target);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }

        public static void ShowWithSlideFromLeftAndFade(UIElement element, double duration = 0.25)
        {
            try
            {
                if (element.Visibility == Visibility.Visible) return;

                if (element == null)
                    throw new ArgumentNullException(nameof(element));

                var sb = new Storyboard();

                // 渐变动画
                var fadeInAnimation = new DoubleAnimation
                {
                    From = 0.5,
                    To = 1,
                    Duration = AnimationSpeed.ScaleToTimeSpan(duration)
                };
                Storyboard.SetTargetProperty(fadeInAnimation, new PropertyPath(UIElement.OpacityProperty));

                // 滑动动画
                var slideAnimation = new DoubleAnimation
                {
                    From = element.RenderTransform.Value.OffsetX - 20, // 滑动距离
                    To = 0,
                    Duration = AnimationSpeed.ScaleToTimeSpan(duration)
                };
                Storyboard.SetTargetProperty(slideAnimation, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));

                sb.Children.Add(fadeInAnimation);
                sb.Children.Add(slideAnimation);

                element.Visibility = Visibility.Visible;
                element.RenderTransform = new TranslateTransform();

                sb.Begin((FrameworkElement)element);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }

        public static void ShowWithScaleFromLeft(UIElement element, double duration = 0.2)
        {
            try
            {
                if (element.Visibility == Visibility.Visible) return;

                if (element == null)
                    throw new ArgumentNullException(nameof(element));

                var sb = new Storyboard();

                // 水平方向的缩放动画
                var scaleXAnimation = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = AnimationSpeed.ScaleToTimeSpan(duration)
                };
                Storyboard.SetTargetProperty(scaleXAnimation, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));

                // 垂直方向的缩放动画
                var scaleYAnimation = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = AnimationSpeed.ScaleToTimeSpan(duration)
                };
                scaleYAnimation.EasingFunction = new CubicEase();
                scaleXAnimation.EasingFunction = new CubicEase();
                Storyboard.SetTargetProperty(scaleYAnimation, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));

                sb.Children.Add(scaleXAnimation);
                sb.Children.Add(scaleYAnimation);

                element.Visibility = Visibility.Visible;
                element.RenderTransformOrigin = new Point(0, 0.5); // 左侧中心点为基准
                element.RenderTransform = new ScaleTransform(0, 0);

                sb.Begin((FrameworkElement)element);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }

        public static void ShowWithScaleFromRight(UIElement element, double duration = 0.2)
        {
            try
            {
                if (element.Visibility == Visibility.Visible) return;

                if (element == null)
                    throw new ArgumentNullException(nameof(element));

                var sb = new Storyboard();

                // 水平方向的缩放动画
                var scaleXAnimation = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = AnimationSpeed.ScaleToTimeSpan(duration)
                };
                Storyboard.SetTargetProperty(scaleXAnimation, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));

                // 垂直方向的缩放动画
                var scaleYAnimation = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = AnimationSpeed.ScaleToTimeSpan(duration)
                };
                Storyboard.SetTargetProperty(scaleYAnimation, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));

                scaleYAnimation.EasingFunction = new CubicEase();
                scaleXAnimation.EasingFunction = new CubicEase();

                sb.Children.Add(scaleXAnimation);
                sb.Children.Add(scaleYAnimation);

                element.Visibility = Visibility.Visible;
                element.RenderTransformOrigin = new Point(1, 0.5); // 右侧中心点为基准
                element.RenderTransform = new ScaleTransform(0, 0);

                sb.Begin((FrameworkElement)element);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }

        public static void HideWithSlideAndFade(UIElement element, double duration = 0.15)
        {
            try
            {
                if (element == null)
                    throw new ArgumentNullException(nameof(element));

                if (element.Visibility == Visibility.Collapsed) return;

                var target = ResolveAnimationTarget(element);

                var sb = new Storyboard();

                var fadeOutAnimation = new DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = AnimationSpeed.ScaleToTimeSpan(duration)
                };
                fadeOutAnimation.EasingFunction = new CubicEase();
                Storyboard.SetTargetProperty(fadeOutAnimation, new PropertyPath(UIElement.OpacityProperty));

                var slideAnimation = new DoubleAnimation
                {
                    From = 0,
                    To = 10,
                    Duration = AnimationSpeed.ScaleToTimeSpan(duration)
                };
                slideAnimation.EasingFunction = new CubicEase();

                Storyboard.SetTargetProperty(slideAnimation, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));

                sb.Children.Add(fadeOutAnimation);
                sb.Children.Add(slideAnimation);

                sb.Completed += (s, e) =>
                {
                    element.Visibility = Visibility.Collapsed;
                };

                target.RenderTransform = new TranslateTransform();
                sb.Begin((FrameworkElement)target);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }

        public static void HideWithFadeOut(UIElement element, double duration = 0.15)
        {
            if (element.Visibility == Visibility.Collapsed) return;

            if (element == null)
                throw new ArgumentNullException(nameof(element));

            var sb = new Storyboard();

            var fadeOutAnimation = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = AnimationSpeed.ScaleToTimeSpan(duration)
            };
            Storyboard.SetTargetProperty(fadeOutAnimation, new PropertyPath(UIElement.OpacityProperty));

            sb.Children.Add(fadeOutAnimation);

            sb.Completed += (s, e) =>
            {
                element.Visibility = Visibility.Collapsed;
            };

            sb.Begin((FrameworkElement)element);
        }

        /// <summary>菜单出场时长（秒）。</summary>
        internal const double PopupPopInDurationSeconds = 0.4;

        /// <summary>
        /// 每个 Popup 当前的动画代次。
        /// </summary>
        /// <remarks>
        /// 菜单的关闭是"动画播完才真正 IsOpen = false"，这段时间里 Popup 仍算开着。
        /// 用户在这个窗口期内再次点开菜单时会出现两个问题：
        /// 1) Show 看到 IsOpen 为 true 就直接返回，动画根本不播；
        /// 2) 上一段关闭动画的 Completed 随后触发，把刚打开的菜单又关掉。
        /// 结果就是"闪一下"。
        ///
        /// 用代次号解决：每次 Show/Hide 都自增，动画回调里比对代次，
        /// 只允许最新一次操作生效；同时 Show 不再因为 IsOpen 为 true 而跳过动画。
        /// </remarks>
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Popup, AnimationToken> PopupTokens = new();

        /// <summary>
        /// 菜单动画的「进度时钟」：打开与收起共用同一条曲线，方向相反。
        /// </summary>
        /// <remarks>
        /// 这是整个动画的核心模型，此前所有打断 bug 都源于违背它：
        ///
        ///   曲线：progress 0.0 → 1.0 映射 缩放 0.72 → 峰值(1.0367) → 1.0
        ///   打开 = progress 从当前值推进到 1.0
        ///   收起 = progress 从当前值倒退到 0.0
        ///   打断 = 不重置 progress，只改变推进方向
        ///
        /// 为什么这能彻底解决打断：
        /// progress 是唯一的状态。打断前后读的是同一个数字，
        /// 「从哪继续」不再需要任何检测（HasAnimatedProperties、Completed、状态机同步），
        /// 也就不存在"起点读错、跳档、竞态覆盖"这一整类问题。
        ///
        /// 进度由 DispatcherTimer 驱动而不是 Storyboard：
        /// Storyboard 无法"从中间反向"，且 Completed/FillBehavior 的时序坑
        /// （Stop 先于 Completed 把值弹回基值）在前几轮反复引发回归。
        /// 时钟每帧自己算值并直接写入 Transform，方向切换天然平滑。
        /// </remarks>
        private sealed class AnimationToken
        {
            /// <summary>当前进度。0 = 完全收起（缩放最小），1 = 完全打开。</summary>
            public double Progress;

            /// <summary>每帧推进的进度增量（正=打开，负=收起）。</summary>
            public double Direction;

            /// <summary>动画时钟；null 表示当前没有动画在跑。</summary>
            public System.Windows.Threading.DispatcherTimer Timer;

            /// <summary>收起动画结束后的回调（由 Hide 流程设置）。</summary>
            public Action OnClosed;

            /// <summary>时钟触发的处理器，解绑时用。</summary>
            public EventHandler TickHandler;

            /// <summary>该菜单最后一次使用的时长基数（秒），用于算每帧步进。</summary>
            public double DurationSeconds = PopupPopInDurationSeconds;

            /// <summary>上次 tick 的时间戳，用于按真实帧间隔推进。</summary>
            public System.Diagnostics.Stopwatch Clock = new();
        }

        private static AnimationToken TokenFor(Popup popup)
        {
            return PopupTokens.GetValue(popup, _ => new AnimationToken());
        }

        /// <summary>菜单出场起始缩放：由小长到正常大小。</summary>
        private const double PopupPopInStartScale = 0.72;

        /// <summary>
        /// 缩放过冲峰值。>1 才会出现"变大一点再回落"。
        /// 必须与实际动画峰值一致：<see cref="PopupOverflowRoom"/> 按它计算留白，
        /// 低估会导致峰值时四边被裁。BackEase(Amplitude=0.5) 从 0.72 起跳的实测峰值为 1.0367。
        /// </summary>
        private const double PopupPopInPeakScale = 1.0367;

        /// <summary>
        /// BackEase 过冲幅度。
        /// 注意 WPF 的 Amplitude 与业界 EaseOutBack 的 c 不是同一个量（Amplitude=1.1 过冲达 43%），
        /// 0.5 对应实测峰值 1.0367（过冲 3.67%），与 <see cref="PopupPopInPeakScale"/> 一致。
        /// </summary>
        private const double PopupPopInBackAmplitude = 0.5;

        /// <summary>收场时长（秒）。比出场短一点，让关闭显得干脆。</summary>
        internal const double PopupPopOutDurationSeconds = 0.22;

        /// <summary>
        /// 打开菜单：progress 从当前位置推进到 1。
        /// </summary>
        public static void ShowPopupWithSlideAndFade(Popup popup, double duration = PopupPopInDurationSeconds)
        {
            try
            {
                if (popup == null) return;

                // 先预留溢出空间：Popup 窗口尺寸等于内容布局尺寸，
                // 不留白的话缩放一旦超过 1.0，四边就会被窗口边界裁掉。
                var child = PopupOverflowRoom.Ensure(popup, PopupPopInPeakScale)
                            ?? popup.Child as FrameworkElement;
                if (child == null)
                {
                    popup.IsOpen = true;
                    return;
                }

                child.Visibility = Visibility.Visible;
                popup.IsOpen = true;

                // 打开后内容才有真实尺寸：按它校正一次留白
                PopupOverflowRoom.RefreshPadding(popup, PopupPopInPeakScale);

                AnimationLog.Log(popup, "SHOW", $"progress={TokenFor(popup).Progress:F3}");

                DrivePopup(popup, child, opening: true,
                    duration, onFullyClosed: null);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }

        /// <summary>
        /// 收起菜单：progress 从当前位置倒退到 0。
        /// </summary>
        public static void HidePopupWithSlideAndFade(Popup popup, double duration = PopupPopInDurationSeconds)
        {
            try
            {
                if (popup == null) return;
                if (!popup.IsOpen) return;

                var child = popup.Child as FrameworkElement;
                if (child == null)
                {
                    popup.IsOpen = false;
                    return;
                }

                var token = TokenFor(popup);
                AnimationLog.Log(popup, "HIDE", $"progress={token.Progress:F3}");

                // 收起的"完成动作"：真正关掉 Popup 并复位进度。
                // 中途被打开打断时这个回调不会执行（方向已反转，时钟不会到达 0）。
                DrivePopup(popup, child, opening: false,
                    duration, onFullyClosed: () =>
                    {
                        popup.IsOpen = false;
                        // 收起终态直接写进变换，避免下一次打开测量到带动画残留的尺寸
                        ClearTransformAnimations(child, PopupPopInStartScale, 10);
                        AnimationLog.Log(popup, "HIDE-DONE", "progress=0");
                    });
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }

        /// <summary>
        /// 唯一的动画驱动器：管理进度时钟，按方向推进 progress，并把曲线值写入变换。
        /// </summary>
        /// <remarks>
        /// 打开与收起不是两段动画，而是同一条曲线的两个播放方向：
        ///
        ///   progress: 0.0 ──────────────▶ 1.0
        ///   缩放:    0.72 → 峰值(1.0367) → 1.0
        ///   打开 = 向右推进；收起 = 向左倒退。
        ///
        /// 打断 = 再次调用本方法且方向相反：时钟沿用同一个 progress，
        /// 只是翻转步进符号，画面从当前大小直接反向，没有任何检测与拼接。
        ///
        /// 为什么用 DispatcherTimer 而不是 Storyboard：
        /// - Storyboard 无法从中间反向，也无法方便地读出"当前播到哪"；
        /// - 前几轮所有回归（Completed 时序、FillBehavior 弹回、起点检测）都源于它。
        /// 时钟模型下这些概念直接不存在。
        ///
        /// 帧率说明：DispatcherTimer(Background) 大约按渲染节拍触发，
        /// 逐帧写 RenderTransform 属合成层属性，与 Storyboard 的每帧开销同级。
        /// </remarks>
        private static void DrivePopup(Popup popup, FrameworkElement child, bool opening,
            double durationSeconds, Action onFullyClosed)
        {
            var token = TokenFor(popup);

            // 首次调用：建立变换结构（此后一直复用，不重建——重建会丢失当前帧状态）
            if (child.RenderTransform is not TransformGroup group
                || group.Children.Count < 2
                || group.Children[0] is not ScaleTransform
                || group.Children[1] is not TranslateTransform)
            {
                var scale = new ScaleTransform(1, 1);
                var slide = new TranslateTransform(0, 0);
                var g = new TransformGroup();
                g.Children.Add(scale);
                g.Children.Add(slide);
                child.RenderTransform = g;
                child.RenderTransformOrigin = PopupScaleOrigin.Resolve(popup, child);
                group = g;

                // 初始进度写一次，保证"从未动过"的菜单有正确的起始视觉
                var (s0, y0) = EvaluateCurve(token.Progress);
                scale.ScaleX = s0;
                scale.ScaleY = s0;
                slide.Y = y0;
            }

            // 方向与速率：收起走完更快（干脆），打开稍慢（从容）
            token.DurationSeconds = durationSeconds > 0.001 ? durationSeconds
                : (opening ? PopupPopInDurationSeconds : PopupPopOutDurationSeconds);
            token.Direction = opening ? 1 : -1;

            // 换目标：收起方向不再需要完成回调；打开方向挂上回调
            token.OnClosed = opening ? null : onFullyClosed;

            // 重启时钟（沿用当前 progress —— 这就是打断的全部实现）
            StopClock(token);
            token.Clock.Restart();

            token.TickHandler = (_, _) => PopupTick(popup, child, token);
            token.Timer = new System.Windows.Threading.DispatcherTimer(
                System.Windows.Threading.DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(8)
            };
            token.Timer.Tick += token.TickHandler;
            token.Timer.Start();
        }

        /// <summary>时钟每帧推进 progress 并写入曲线值。</summary>
        private static void PopupTick(Popup popup, FrameworkElement child, AnimationToken token)
        {
            double dt = token.Clock.Elapsed.TotalSeconds;
            token.Clock.Restart();

            // 每秒推进量：1 / 时长。速度倍率在这里生效。
            double step = token.Direction * dt / AnimationSpeed.Scale(token.DurationSeconds);
            double next = token.Progress + step;

            if (token.Direction > 0)
            {
                if (next >= 1.0)
                {
                    // 打开完成：钉在 1.0
                    SetPopupProgress(popup, child, token, 1.0);
                    StopClock(token);
                    AnimationLog.Log(popup, "SHOW-DONE", "progress=1.000");
                    return;
                }
            }
            else
            {
                if (next <= 0.0)
                {
                    // 收起完成：钉在 0.0，执行关闭动作
                    SetPopupProgress(popup, child, token, 0.0);
                    StopClock(token);
                    token.OnClosed?.Invoke();
                    return;
                }
            }

            token.Progress = next;
            SetPopupProgress(popup, child, token, next);
        }

        /// <summary>把 progress 映射为缩放/位移并写入变换。</summary>
        private static void SetPopupProgress(Popup popup, FrameworkElement child, AnimationToken token, double progress)
        {
            token.Progress = progress;

            if (child?.RenderTransform is not TransformGroup g || g.Children.Count < 2) return;

            var (scale, slideY) = EvaluateCurve(progress);

            if (g.Children[0] is ScaleTransform st)
            {
                st.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                st.ScaleX = scale;
                st.ScaleY = scale;
            }
            if (g.Children[1] is TranslateTransform tt)
            {
                tt.BeginAnimation(TranslateTransform.YProperty, null);
                tt.Y = slideY;
            }

            // 透明度跟随进度：收起时随缩小渐隐，打开时随放大渐显
            double targetOpacity = GetPopupTargetOpacity(popup);
            child.BeginAnimation(UIElement.OpacityProperty, null);
            child.Opacity = targetOpacity * (0.35 + 0.65 * progress);
        }

        /// <summary>
        /// 曲线定义：progress ∈ [0,1] 映射 (缩放, 纵向位移)。
        /// </summary>
        /// <remarks>
        /// 分段：
        ///   [0, P]   0.72 → 峰值   （上升段）
        ///   [P, 1]   峰值 → 1.0    （回落段）
        /// 段内线性——用户给的观感序列（1-2-3-...-9-8-7-6）是每步等量的。
        /// 正放倒放经过同一组值，因此打开与收起天然互为镜像。
        ///
        /// 位移：progress 0（收起）= 下滑 10px；1（打开）= 0。
        /// </remarks>
        private static (double Scale, double SlideY) EvaluateCurve(double progress)
        {
            progress = Math.Max(0.0, Math.Min(1.0, progress));

            const double peak = PopupPopInPeakScale;
            const double start = PopupPopInStartScale;
            // 峰值时间点 = 上升行程 / 总行程（速度一致，转折自然）
            double up = peak - start;
            double down = 1.0 - peak;
            double peakAt = up + down > 0.0001 ? up / (up + down) : 0.8;

            double scale;
            if (progress <= peakAt)
            {
                double local = peakAt > 0.0001 ? progress / peakAt : 1.0;
                scale = start + (peak - start) * local;
            }
            else
            {
                double local = (progress - peakAt) / Math.Max(0.0001, 1.0 - peakAt);
                scale = peak + (1.0 - peak) * local;
            }

            double slideY = 10.0 * (1.0 - progress);
            return (scale, slideY);
        }

        private static void StopClock(AnimationToken token)
        {
            if (token.Timer != null)
            {
                token.Timer.Stop();
                if (token.TickHandler != null) token.Timer.Tick -= token.TickHandler;
                token.Timer = null;
                token.TickHandler = null;
            }
        }

        /// <summary>
        /// 清掉内容变换上的所有动画，并把缩放/位移恢复到单位值。
        /// </summary>
        /// <remarks>
        /// Storyboard 以属性路径动画到 <see cref="TransformGroup"/> 的子变换时，
        /// 动画对象挂在该 Transform 实例上；即使 Storyboard 结束，动画值仍可能把持属性。
        /// 这一步是幂等的，可以在每次打开前无脑调用。
        /// </remarks>
        private static void ClearTransformAnimations(FrameworkElement element,
            double scale = 1.0, double slideY = 0.0)
        {
            try
            {
                if (element?.RenderTransform is not TransformGroup group)
                {
                    // 不是我们的结构就不动它，避免破坏别处设置的变换
                    return;
                }

                foreach (var transform in group.Children)
                {
                    if (transform is ScaleTransform scaleTransform)
                    {
                        scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                        scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                        scaleTransform.ScaleX = scale;
                        scaleTransform.ScaleY = scale;
                    }
                    else if (transform is TranslateTransform translate)
                    {
                        translate.BeginAnimation(TranslateTransform.XProperty, null);
                        translate.BeginAnimation(TranslateTransform.YProperty, null);
                        translate.X = 0;
                        translate.Y = slideY;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AnimationsHelper] ClearTransformAnimations: {ex.Message}");
            }
        }

        /// <summary>
        /// 根据 Popup 名称获取其目标透明度（菜单透明度设置）
        /// </summary>
        private static double GetPopupTargetOpacity(Popup popup)
        {
            var settings = Ink_Canvas.Windows.SettingsViews.Helpers.SettingsManager.Settings;
            var mw = Application.Current.MainWindow as Ink_Canvas.MainWindow;
            bool isPPTMode = mw != null && mw.currentMode == 2;

            // 浮动栏菜单
            string name = popup.Name;
            if (name == "PenPalette" || name == "EraserSizePanel" || name == "BorderTools"
                || name == "BorderDrawShape" || name == "TwoFingerGestureBorder")
            {
                return isPPTMode
                    ? settings.Appearance.FloatingBarMenuOpacityInPPT
                    : settings.Appearance.FloatingBarMenuOpacity;
            }

            // 白板菜单
            if (name == "BoardTwoFingerGestureBorder" || name == "BoardRoamingPopup" || name == "BackgroundPalette"
                || name == "BoardPenPalette" || name == "BoardEraserSizePanel"
                || name == "BoardBorderDrawShape" || name == "BoardImageOptionsPanel"
                || name == "BoardBorderToolsPopup")
            {
                return settings.Appearance.BoardMenuOpacity;
            }

            // 其他 Popup 默认不透明
            return 1.0;
        }

    }
}
