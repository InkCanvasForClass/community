using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Ink_Canvas.Helpers.Motion;
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

        /// <summary>
        /// 菜单出场时长（秒）。
        /// </summary>
        /// <remarks>
        /// 时长是「progress 从 0 走到 1」的时间，曲线形状与它无关：
        /// 缩短只会整体加快，峰值位置(8/11)与过冲量都不变。
        ///
        /// 基准时长应当就是「手感最舒服的那个速度」，而不是一个偏慢的"出厂值"，
        /// 否则用户只能常驻 2x 以上，倍率滑杆的 1.0 就失去了意义。
        /// 用户实测结论：1x 要按 0.15s 这个速度才算对劲。
        /// </remarks>
        internal const double PopupPopInDurationSeconds = 0.15;

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
        ///   曲线：progress 0.0 → 1.0 映射 缩放 0.72 → 峰值(1.05) → 1.0
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

            /// <summary>是否正在逐帧驱动（false = 当前没有动画在跑）。</summary>
            public bool Running;

            /// <summary>收起动画结束后的回调（由 Hide 流程设置）。</summary>
            public Action OnClosed;

            /// <summary>逐帧回调，解绑时用。</summary>
            public EventHandler RenderingHandler;

            /// <summary>该菜单最后一次使用的时长基数（秒），用于算每帧步进。</summary>
            public double DurationSeconds = PopupPopInDurationSeconds;

            /// <summary>上一帧的时间，用于按真实帧间隔推进。</summary>
            public System.Diagnostics.Stopwatch Clock = new();
        }

        private static AnimationToken TokenFor(Popup popup)
        {
            return PopupTokens.GetValue(popup, _ => new AnimationToken());
        }

        /// <summary>菜单出场起始缩放：由小长到正常大小。</summary>
        /// <remarks>曲线本身在 <see cref="PopupCurve"/> 里，这里保留同义常量只为本类内部引用可读。</remarks>
        private const double PopupPopInStartScale = PopupCurve.StartScale;

        /// <summary>
        /// 缩放过冲峰值。>1 才会出现"变大一点再回落"。
        /// 必须与实际动画峰值一致：<see cref="PopupOverflowRoom"/> 按它计算留白，
        /// 低估会导致峰值时四边被裁。
        /// </summary>
        /// <remarks>
        /// 取 1.05（过冲 5%）而不是更小的值，是被时长逼出来的：
        /// 时长压到 0.15s 后，回落段只剩 3/11 × 150ms = 41ms，60fps 下约 2.5 帧。
        /// 采样点落不到峰值上时，实测最大缩放只有 ~1.02——肉眼分辨不出"先放大再回落"。
        /// 把峰值抬到 1.05，即使采样错过顶点，落点也在 1.02~1.04，过冲依然成立。
        /// 这同时解释了为什么峰值必须与留白使用同一个常量：留白按 1.05 算，
        /// 而曲线若只到 1.03，多出来的留白纯属浪费。
        /// </remarks>
        private const double PopupPopInPeakScale = PopupCurve.PeakScale;

        /// <summary>
        /// 收场时长（秒）。比出场短一点，让关闭显得干脆。
        /// </summary>
        /// <remarks>
        /// 收起是同一曲线的倒放，所以"先涨后缩"那段同样占 3/11。
        /// 0.10s 时该段约 27ms ≈ 1.6 帧，是这套曲线还能看出收起的最后一段余地；
        /// 能站得住是因为 <see cref="PopupPopInPeakScale"/> 提到了 1.05 ——
        /// 过冲量越大，越少的帧也足以传达"先涨后缩"。
        /// 收起刻意比打开快（0.10 vs 0.15）：关闭要显得干脆，打开要从容。
        /// </remarks>
        internal const double PopupPopOutDurationSeconds = 0.10;

        /// <summary>
        /// 菜单的目标状态：true = 正在（或已经）打开，false = 正在（或已经）收起。
        /// </summary>
        /// <remarks>
        /// 所有 toggle 判定都必须用它，而不是 <see cref="Popup.IsOpen"/>。
        ///
        /// 收起是"动画播完才真正 IsOpen = false"，这段时间里 IsOpen 仍为 true。
        /// 若调用方用 IsOpen 判开合，用户在收起途中再次点击时会再次走收起分支：
        /// 方向本来就是 -1，再设为 -1 等于什么都没发生，progress 只是一路退到 0。
        /// 观感就是"打断一次之后再点没反应"，而用户期望的是把方向翻回来
        /// （1-2-3-4 打断 4-3，再打断就该 3-4-5-6）。
        ///
        /// 动画进行中看方向、静止时看 IsOpen，两个阶段都不含糊，
        /// 于是每一次点击都必然翻转播放方向 —— 这正是"打断"本身。
        /// </remarks>
        internal static bool IsPopupTargetingOpen(Popup popup)
        {
            if (popup == null) return false;

            var token = TokenFor(popup);
            return token.Running ? token.Direction > 0 : popup.IsOpen;
        }

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

                // 记下次要推进的方向：打断排查时，"点了没反应"与"方向没翻转"是两回事，
                // 日志里能看到同一菜单连续出现 SHOW/HIDE/HIDE/SHOW 才说明方向真的翻转了。
                AnimationLog.Log(popup, "SHOW", $"progress={TokenFor(popup).Progress:F3} dir=+1");

                DrivePopup(popup, child, opening: true,
                    duration, onFullyClosed: null);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }

        /// <summary>
        /// 收起菜单：progress 从当前位置倒退到 0。
        /// </summary>
        public static void HidePopupWithSlideAndFade(Popup popup, double duration = PopupPopOutDurationSeconds)
        {
            try
            {
                if (popup == null) return;
                if (!popup.IsOpen) return;

                // 关键：必须与 Show 操作**同一个元素**。
                //
                // 包装后 popup.Child 是外层宿主 Border，而 Show 的动画写在内部内容上。
                // 若这里直接拿 popup.Child，时钟会写进宿主的变换 —— 两个方法各写一套，
                // 互相不可见：第一次正常（各自首次都"从零建"），第二次开始状态错乱，
                // 表现为定位异常 + 打开卡住。
                // 用 PopupOverflowRoom.Ensure 取回同一个内容元素；未包装时退回 popup.Child。
                var child = PopupOverflowRoom.Ensure(popup, PopupPopInPeakScale)
                            ?? popup.Child as FrameworkElement;
                if (child == null)
                {
                    popup.IsOpen = false;
                    return;
                }

                var token = TokenFor(popup);
                AnimationLog.Log(popup, "HIDE", $"progress={token.Progress:F3} dir=-1");

                // 收起的"完成动作"：真正关掉 Popup 并复位进度。
                DrivePopup(popup, child, opening: false,
                    duration, onFullyClosed: () =>
                    {
                        // 竞态防护：回调执行时方向若已被 Show 反转为打开，
                        // 说明用户在收起触底后立刻重新打开 —— 这次关闭作废，
                        // 否则会把刚打开的 Popup 关掉，表现为"打开后菜单消失/卡住"。
                        if (token.Direction > 0)
                        {
                            AnimationLog.Log(popup, "HIDE-DONE-SKIPPED", "reopened");
                            return;
                        }

                        // 关闭前先把 progress=0 的曲线值写进变换：
                        // 收起中断处(如 0.62)的缩放残留会让 PopupOverflowRoom 的
                        // 定位补偿按中途尺寸测量，下次打开位置就偏了。
                        SetPopupProgress(popup, child, token, 0.0);

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
        ///   缩放:    0.72 → 峰值(1.05) → 1.0
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
                group = g;

                // 初始进度写一次，保证"从未动过"的菜单有正确的起始视觉
                var initial = PopupCurve.Evaluate(token.Progress);
                scale.ScaleX = initial.Scale;
                scale.ScaleY = initial.Scale;
                slide.Y = initial.SlideY;
            }

            // 每次都重新解析缩放基准点：
            // 触发按钮可能不同（同一 Popup 可被多个入口打开），
            // 且 PointToScreen/TransformToAncestor 受 RenderTransform 影响（实测 0.72 缩放偏移 34px），
            // 残留的变换会让 Resolve 算错。先归位再解析。
            ClearTransformAnimations(child, 1.0, 0.0);
            child.RenderTransformOrigin = PopupScaleOrigin.Resolve(popup, child);
            SetPopupProgress(popup, child, token, token.Progress);

            // 方向与速率：收起走完更快（干脆），打开稍慢（从容）。
            //
            // 打断时（本来就在跑）沿用当前时长，只翻方向：用户描述打断是「1-2-3-4 打断 4-3-2-1」，
            // 每一步等长。若打断时按新方向换成另一个时长，反向的那一帧速度就突变了。
            double nominalDuration = durationSeconds > 0.001 ? durationSeconds
                : (opening ? PopupPopInDurationSeconds : PopupPopOutDurationSeconds);
            token.DurationSeconds = PopupTiming.ResolveDuration(
                nominalDuration, token.DurationSeconds, token.Running);
            token.Direction = opening ? 1 : -1;

            // 换目标：收起方向不再需要完成回调；打开方向挂上回调
            token.OnClosed = opening ? null : onFullyClosed;

            // 重启驱动（沿用当前 progress —— 这就是打断的全部实现）
            StopClock(token);
            token.Clock.Restart();

            // 用 CompositionTarget.Rendering 而不是 DispatcherTimer：
            // 后者按时间触发，UI 一忙就会"欠账"，下一次回调带来一个很大的 dt，
            // progress 一步跨过整个过冲区 —— 实测收起时 1.05 的峰值整段没被画出来。
            // Rendering 与合成帧同拍，每帧只推进一帧的量，曲线必定被完整采样。
            token.RenderingHandler = (_, _) => PopupTick(popup, child, token);
            token.Running = true;
            CompositionTarget.Rendering += token.RenderingHandler;

            // 防泄漏：Popup 若被外部直接关闭（不走本类流程），
            // 逐帧回调会一直挂在 CompositionTarget.Rendering 上，
            // 每帧继续持有 popup/child 引用并推进已不存在的动画。
            // 这里挂一次性监听，关闭时立刻解绑。
            void OnPopupClosed(object s, EventArgs e)
            {
                popup.Closed -= OnPopupClosed;
                StopClock(token);
            }
            popup.Closed -= OnPopupClosed;
            popup.Closed += OnPopupClosed;
        }

        /// <summary>每渲染帧推进 progress 并写入曲线值。</summary>
        private static void PopupTick(Popup popup, FrameworkElement child, AnimationToken token)
        {
            double dt = token.Clock.Elapsed.TotalSeconds;
            token.Clock.Restart();

            // 夹住单帧时长：窗口被拖动/系统卡顿时 dt 可能上百毫秒，
            // 不夹的话一帧就能跨过整个过冲区，曲线被"跳"过去。
            // 夹到 40ms 相当于最低 25fps 的表现，宁可短暂整体变慢也不丢曲线形状。
            if (dt > PopupTiming.MaxFrameSeconds) dt = PopupTiming.MaxFrameSeconds;

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

            // 缩放、位移、透明度同取一个曲线采样：
            // 三者若各走各的节奏（缩放先快后慢、位移匀速），会看出"两个东西在各自动"。
            var sample = PopupCurve.Evaluate(progress);

            if (g.Children[0] is ScaleTransform st)
            {
                st.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                st.ScaleX = sample.Scale;
                st.ScaleY = sample.Scale;
            }
            if (g.Children[1] is TranslateTransform tt)
            {
                tt.BeginAnimation(TranslateTransform.YProperty, null);
                tt.Y = sample.SlideY;
            }

            // 透明度跟随进度：收起时随缩小渐隐，打开时随放大渐显
            double targetOpacity = GetPopupTargetOpacity(popup);
            child.BeginAnimation(UIElement.OpacityProperty, null);
            child.Opacity = targetOpacity * sample.OpacityWeight;
        }

        private static void StopClock(AnimationToken token)
        {
            token.Running = false;
            if (token.RenderingHandler != null)
            {
                CompositionTarget.Rendering -= token.RenderingHandler;
                token.RenderingHandler = null;
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
