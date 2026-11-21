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
                Duration = TimeSpan.FromSeconds(duration)
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
                    Duration = TimeSpan.FromSeconds(duration)
                };
                fadeInAnimation.EasingFunction = new CubicEase();
                Storyboard.SetTarget(fadeInAnimation, target);
                Storyboard.SetTargetProperty(fadeInAnimation, new PropertyPath(UIElement.OpacityProperty));

                var slideAnimation = new DoubleAnimation
                {
                    From = 20,
                    To = 0,
                    Duration = TimeSpan.FromSeconds(duration)
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
                    Duration = TimeSpan.FromSeconds(duration)
                };
                Storyboard.SetTargetProperty(fadeInAnimation, new PropertyPath(UIElement.OpacityProperty));

                // 滑动动画
                var slideAnimation = new DoubleAnimation
                {
                    From = element.RenderTransform.Value.OffsetX - 20, // 滑动距离
                    To = 0,
                    Duration = TimeSpan.FromSeconds(duration)
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
                    Duration = TimeSpan.FromSeconds(duration)
                };
                Storyboard.SetTargetProperty(scaleXAnimation, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));

                // 垂直方向的缩放动画
                var scaleYAnimation = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromSeconds(duration)
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
                    Duration = TimeSpan.FromSeconds(duration)
                };
                Storyboard.SetTargetProperty(scaleXAnimation, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));

                // 垂直方向的缩放动画
                var scaleYAnimation = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromSeconds(duration)
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
                    Duration = TimeSpan.FromSeconds(duration)
                };
                fadeOutAnimation.EasingFunction = new CubicEase();
                Storyboard.SetTargetProperty(fadeOutAnimation, new PropertyPath(UIElement.OpacityProperty));

                var slideAnimation = new DoubleAnimation
                {
                    From = 0,
                    To = 10,
                    Duration = TimeSpan.FromSeconds(duration)
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
                Duration = TimeSpan.FromSeconds(duration)
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

        private sealed class AnimationToken
        {
            public int Generation;
        }

        private static AnimationToken TokenFor(Popup popup)
        {
            return PopupTokens.GetValue(popup, _ => new AnimationToken());
        }

        /// <summary>当前代次（用于回调里判断是否已被更新的操作取代）。</summary>
        private static int NextGeneration(Popup popup)
        {
            var token = TokenFor(popup);
            return ++token.Generation;
        }

        private static bool IsCurrent(Popup popup, int generation)
        {
            return PopupTokens.TryGetValue(popup, out var token) && token.Generation == generation;
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

        /// <summary>
        /// 收场结束时的缩放：与出场的起始缩放一致，观感上"从哪来回哪去"。
        /// 消失不需要过冲，所以直接单调缩到该值。
        /// </summary>
        private const double PopupPopOutEndScale = 0.9;

        /// <summary>收场时长（秒）。比出场短一点，让关闭显得干脆。</summary>
        internal const double PopupPopOutDurationSeconds = 0.22;

        public static void ShowPopupWithSlideAndFade(Popup popup, double duration = PopupPopInDurationSeconds)
        {
            try
            {
                if (popup == null)
                    throw new ArgumentNullException(nameof(popup));

                // 注意：这里**不能**因为 IsOpen 为 true 就返回。
                // 关闭动画播放期间 Popup 仍是打开的（动画结束才置 false），
                // 用户在此时重新点开菜单，若直接返回就只剩上一段关闭动画在跑——表现为"闪一下"。
                // 因此允许重入：自增代次，作废仍在等待的关闭回调，然后重新播一遍出场动画。
                int generation = NextGeneration(popup);

                // 先预留溢出空间：Popup 窗口尺寸等于内容布局尺寸，
                // 不留白的话缩放一旦超过 1.0，四边就会被窗口边界裁掉。
                // Ensure 会同时补偿定位（见 PopupOverflowRoom 的说明），视觉位置不变。
                var child = PopupOverflowRoom.Ensure(popup, PopupPopInPeakScale)
                            ?? popup.Child as FrameworkElement;
                if (child == null)
                {
                    popup.IsOpen = true;
                    return;
                }

                // 获取目标透明度（菜单透明度设置）
                double targetOpacity = GetPopupTargetOpacity(popup);

                child.Visibility = Visibility.Visible;

                // 关键设计：**基值就是终值**，动画只负责"从 From 播到基值"。
                //
                // WPF 里属性值有三层：动画 > 基值。用 FillBehavior.Stop 时动画一结束
                // 就回落到基值；如果把基值设成起始值（0.72），菜单播完就会停在小尺寸上，
                // 表现为"第二次打开变小、位置偏"——靠 Completed 回调补终值非常脆弱，
                // 回调一旦不执行（被后续操作打断、Storyboard 被替换）就永久停在错误状态。
                //
                // 因此这里把基线设成最终态（缩放 1、位移 0、目标透明度），
                // From 只描述起点。播完后无论是否触发 Completed，属性都已经是终值。
                child.BeginAnimation(UIElement.OpacityProperty, null);
                child.Opacity = targetOpacity;

                // 自愈：先把上一次残留的变换动画彻底摘掉。
                // 若上次收场被打断（Completed 没跑），缩放/位移会停在中间值，
                // 而 Popup 打开时会按带残留的尺寸测量布局，导致 popupSize 变小、
                // 定位计算偏下。这里强制归零，保证每次打开都从干净状态开始。
                ClearTransformAnimations(child);

                var scaleTransform = new ScaleTransform(1, 1);
                var slideTransform = new TranslateTransform(0, 0);
                var transformGroup = new TransformGroup();
                transformGroup.Children.Add(scaleTransform);
                transformGroup.Children.Add(slideTransform);

                child.RenderTransform = transformGroup;
                // 原点必须是内容自身中心：留白是四周对称的，以中心缩放才不会被单边裁到。
                child.RenderTransformOrigin = new Point(0.5, 0.5);

                popup.IsOpen = true;

                // 打开后内容才有真实尺寸：按它校正一次留白，
                // 避免打开前测不到尺寸导致留白偏小、峰值时四边被裁。
                PopupOverflowRoom.RefreshPadding(popup, PopupPopInPeakScale);

                // 诊断：记录本次打开后的关键状态，便于对比第一次与后续打开的差异。
                // 用 BeginInvoke 排到布局之后，才能取到真实的实际尺寸与屏幕位置。
                child.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        var hostEl = popup.Child as FrameworkElement;
                        var screen = child.PointToScreen(new Point(0, 0));
                        PopupOverflowRoom.Diag(
                            $"[Open] popup={PopupOverflowRoom.Name(popup)}"
                            + $" childActual={child.ActualWidth:F1}x{child.ActualHeight:F1}"
                            + $" hostActual={(hostEl != null ? hostEl.ActualWidth.ToString("F1") : "-")}x{(hostEl != null ? hostEl.ActualHeight.ToString("F1") : "-")}"
                            + $" padding={(hostEl is Border hb ? hb.Padding.Left.ToString("F1") + "," + hb.Padding.Top.ToString("F1") : "-")}"
                            + $" screen=({screen.X:F1},{screen.Y:F1})"
                            + $" transform={DescribeTransform(child)}");
                    }
                    catch (Exception ex)
                    {
                        PopupOverflowRoom.Diag("[Open] 记录失败: " + ex.Message);
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);

                // 注意：置顶由 PopupManagerHelper 统一管理，
                // 此处不再调用 BringPopupToFront，避免重复置顶导致闪烁

                var sb = new Storyboard();

                // To 全部省略：目标就是基值（终态）。
                // FillBehavior.Stop 让动画播完后释放属性、露出基值——基值已是终态，结果正确。
                var fadeInAnimation = new DoubleAnimation
                {
                    From = targetOpacity * 0.5,
                    Duration = TimeSpan.FromSeconds(duration),
                    FillBehavior = FillBehavior.Stop
                };
                fadeInAnimation.EasingFunction = new CubicEase();
                Storyboard.SetTargetProperty(fadeInAnimation, new PropertyPath(UIElement.OpacityProperty));

                var slideAnimation = new DoubleAnimation
                {
                    From = 10,
                    Duration = TimeSpan.FromSeconds(duration),
                    FillBehavior = FillBehavior.Stop
                };
                slideAnimation.EasingFunction = new CubicEase();
                Storyboard.SetTargetProperty(slideAnimation,
                    new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[1].(TranslateTransform.Y)"));

                // 缩放入场：一条 BackEase 曲线完成"小 → 略大 → 回落"。
                // 不要拆成两段关键帧：峰值处速度不连续会顿一下，EaseOutBack 本身就先冲过头再回落。
                var scaleAnimation = new DoubleAnimation
                {
                    From = PopupPopInStartScale,
                    Duration = TimeSpan.FromSeconds(duration),
                    FillBehavior = FillBehavior.Stop,
                    EasingFunction = new BackEase
                    {
                        EasingMode = EasingMode.EaseOut,
                        Amplitude = PopupPopInBackAmplitude
                    }
                };
                Storyboard.SetTargetProperty(scaleAnimation,
                    new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleX)"));
                var scaleAnimationY = scaleAnimation.Clone();
                Storyboard.SetTargetProperty(scaleAnimationY,
                    new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleY)"));

                // 不需要 Completed 打补丁：基值已经是终态。
                // Stop 让动画结束后露出基值（缩放 1、位移 0、目标透明度），天然就是正确结果。

                sb.Children.Add(fadeInAnimation);
                sb.Children.Add(slideAnimation);
                sb.Children.Add(scaleAnimation);
                sb.Children.Add(scaleAnimationY);

                sb.Begin(child);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }

        public static void HidePopupWithSlideAndFade(Popup popup, double duration = PopupPopOutDurationSeconds)
        {
            try
            {
                if (popup == null)
                    throw new ArgumentNullException(nameof(popup));

                if (!popup.IsOpen) return;

                var child = popup.Child as FrameworkElement;
                if (child == null)
                {
                    popup.IsOpen = false;
                    return;
                }

                // 自增代次：若关闭动画期间用户又打开菜单，这次关闭就应当作废
                int generation = NextGeneration(popup);

                double targetOpacity = GetPopupTargetOpacity(popup);

                var sb = new Storyboard();

                // 收场与出场对称：缩放 + 位移 + 淡出，同一条时间轴。
                // 原来只有淡出和 10px 下移，没有缩放，和出场（缩放+位移+淡入）不对称，
                // 观感就会"生硬、像被抽走"。
                //
                // 基值仍保持终态（缩放 1、位移 0、目标透明度），
                // 动画只描述"从现在播到 To"，播完由 Closed 重置，
                // 这样中途被重新打开也不会留下错误状态。
                child.BeginAnimation(UIElement.OpacityProperty, null);
                child.Opacity = targetOpacity;

                var exitScale = new ScaleTransform(1, 1);
                var exitSlide = new TranslateTransform(0, 0);
                var exitGroup = new TransformGroup();
                exitGroup.Children.Add(exitScale);
                exitGroup.Children.Add(exitSlide);
                child.RenderTransform = exitGroup;
                child.RenderTransformOrigin = new Point(0.5, 0.5);

                // 淡出：用 EaseInCubic 起步慢、收尾快，符合"消失"的观感
                var fadeOutAnimation = new DoubleAnimation
                {
                    To = 0,
                    Duration = TimeSpan.FromSeconds(duration)
                };
                fadeOutAnimation.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn };
                Storyboard.SetTargetProperty(fadeOutAnimation, new PropertyPath(UIElement.OpacityProperty));

                // 位移：向下滑出，与出场的向上滑入互为镜像，用 EaseIn 让加速感自然
                var slideAnimation = new DoubleAnimation
                {
                    To = 10,
                    Duration = TimeSpan.FromSeconds(duration)
                };
                slideAnimation.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn };
                Storyboard.SetTargetProperty(slideAnimation,
                    new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[1].(TranslateTransform.Y)"));

                // 缩小：与出场放大镜像。用 EaseIn 而非 Back，消失不需要回弹
                var exitScaleAnimation = new DoubleAnimation
                {
                    To = PopupPopOutEndScale,
                    Duration = TimeSpan.FromSeconds(duration)
                };
                exitScaleAnimation.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn };
                Storyboard.SetTargetProperty(exitScaleAnimation,
                    new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleX)"));
                var exitScaleAnimationY = exitScaleAnimation.Clone();
                Storyboard.SetTargetProperty(exitScaleAnimationY,
                    new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleY)"));

                sb.Children.Add(fadeOutAnimation);
                sb.Children.Add(slideAnimation);
                sb.Children.Add(exitScaleAnimation);
                sb.Children.Add(exitScaleAnimationY);

                sb.Completed += (s, e) =>
                {
                    // 关闭动画期间被重新打开：本次关闭已过时，不能把菜单关掉
                    if (!IsCurrent(popup, generation)) return;

                    popup.IsOpen = false;
                    child.BeginAnimation(UIElement.OpacityProperty, null);
                    child.Opacity = targetOpacity;

                    // 关键：把变换上的动画彻底摘掉并归位。
                    //
                    // Storyboard 通过属性路径动画到 TransformGroup 的子里时，
                    // 动画对象挂在 Transform 上，Storyboard 结束后不会自动释放
                    // （日志实测 [Close] 后缩放仍停在 0.905）。
                    // 这个残留会让下次打开时布局按 0.9 倍测量，popupSize 变小、定位偏下，
                    // 必须在这里显式清除。
                    exitScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                    exitScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                    exitSlide.BeginAnimation(TranslateTransform.YProperty, null);
                    exitScale.ScaleX = 1.0;
                    exitScale.ScaleY = 1.0;
                    exitSlide.Y = 0;

                    PopupOverflowRoom.Diag(
                        $"[Close] popup={PopupOverflowRoom.Name(popup)} transform={DescribeTransform(child)}");
                };

                // 收场动画一律 FillBehavior.Stop：结束后靠上面的清理归位，
                // 不让动画值继续把持属性。
                fadeOutAnimation.FillBehavior = FillBehavior.Stop;
                slideAnimation.FillBehavior = FillBehavior.Stop;
                exitScaleAnimation.FillBehavior = FillBehavior.Stop;
                exitScaleAnimationY.FillBehavior = FillBehavior.Stop;

                PopupOverflowRoom.Diag(
                    $"[CloseStart] popup={PopupOverflowRoom.Name(popup)} transform={DescribeTransform(child)}");

                sb.Begin(child);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }

        /// <summary>
        /// 清掉内容变换上的所有动画，并把缩放/位移恢复到单位值。
        /// </summary>
        /// <remarks>
        /// Storyboard 以属性路径动画到 <see cref="TransformGroup"/> 的子变换时，
        /// 动画对象挂在该 Transform 实例上；即使 Storyboard 结束，动画值仍可能把持属性。
        /// 这一步是幂等的，可以在每次打开前无脑调用。
        /// </remarks>
        private static void ClearTransformAnimations(FrameworkElement element)
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
                    if (transform is ScaleTransform scale)
                    {
                        scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                        scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                        scale.ScaleX = 1.0;
                        scale.ScaleY = 1.0;
                    }
                    else if (transform is TranslateTransform translate)
                    {
                        translate.BeginAnimation(TranslateTransform.XProperty, null);
                        translate.BeginAnimation(TranslateTransform.YProperty, null);
                        translate.X = 0;
                        translate.Y = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AnimationsHelper] ClearTransformAnimations: {ex.Message}");
            }
        }

        /// <summary>诊断用：把内容的变换状态描述成一行，便于比对。</summary>
        private static string DescribeTransform(FrameworkElement element)
        {
            if (element?.RenderTransform is not TransformGroup group)
            {
                return element?.RenderTransform == null ? "none" : element.RenderTransform.GetType().Name;
            }

            var parts = new System.Text.StringBuilder();
            foreach (var t in group.Children)
            {
                if (t is ScaleTransform st) parts.Append($"S({st.ScaleX:F3})");
                else if (t is TranslateTransform tt) parts.Append($"T({tt.X:F1},{tt.Y:F1})");
                else parts.Append(t.GetType().Name);
                parts.Append(' ');
            }
            return parts.ToString().Trim();
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
