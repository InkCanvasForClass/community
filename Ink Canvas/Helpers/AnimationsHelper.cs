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

                // 打断支持：若上一段动画（通常是收场）还在播，从它**当前**的视觉状态接续，
                // 而不是先归位再重来——否则用户会看到"缩回去一半又跳回来"。
                var (scaleStart, slideStart, opacityStart) = CaptureCurrentVisualState(child);

                // 清掉旧动画（不再归位），目的是让下面的基值写入能生效。
                ClearTransformAnimations(child);
                child.BeginAnimation(UIElement.OpacityProperty, null);

                var scaleTransform = new ScaleTransform(scaleStart, scaleStart);
                var slideTransform = new TranslateTransform(0, slideStart);
                var transformGroup = new TransformGroup();
                transformGroup.Children.Add(scaleTransform);
                transformGroup.Children.Add(slideTransform);

                child.RenderTransform = transformGroup;
                // 缩放基准点跟随触发按钮：从图标处放大、也缩回图标处。
                child.RenderTransformOrigin = PopupScaleOrigin.Resolve(popup, child);

                // 从当前视觉状态出发：透明度也要接续，避免中途重开时突然闪一下
                child.Opacity = opacityStart;

                popup.IsOpen = true;

                // 打开后内容才有真实尺寸：按它校正一次留白，
                // 避免打开前测不到尺寸导致留白偏小、峰值时四边被裁。
                PopupOverflowRoom.RefreshPadding(popup, PopupPopInPeakScale);

                // 注意：置顶由 PopupManagerHelper 统一管理，
                // 此处不再调用 BringPopupToFront，避免重复置顶导致闪烁

                var sb = new Storyboard();

                // To 全部省略：目标就是基值（终态）。
                // FillBehavior.Stop 让动画播完后释放属性、露出基值——基值已是终态，结果正确。
                // 时间按"剩余行程"缩短：从 0.9 接续到 1.0 只需走 1/10 的路程，
                // 仍用完整时长会显得拖沓。打断越快、剩余越少、时长越短。
                double remaining = Math.Abs(1.0 - scaleStart);
                double fullDistance = Math.Abs(1.0 - PopupPopInStartScale);
                double effectiveDuration = Math.Max(0.08,
                    duration * (fullDistance > 0.001 ? Math.Min(1.0, remaining / fullDistance) : 1.0));

                var fadeInAnimation = new DoubleAnimation
                {
                    From = opacityStart,
                    Duration = AnimationSpeed.ScaleToTimeSpan(effectiveDuration),
                    FillBehavior = FillBehavior.Stop
                };
                fadeInAnimation.EasingFunction = new CubicEase();
                Storyboard.SetTargetProperty(fadeInAnimation, new PropertyPath(UIElement.OpacityProperty));

                var slideAnimation = new DoubleAnimation
                {
                    From = slideStart,
                    Duration = AnimationSpeed.ScaleToTimeSpan(effectiveDuration),
                    FillBehavior = FillBehavior.Stop
                };
                slideAnimation.EasingFunction = new CubicEase();
                Storyboard.SetTargetProperty(slideAnimation,
                    new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[1].(TranslateTransform.Y)"));

                // 缩放入场：一条 BackEase 曲线完成"小 → 略大 → 回落"。
                // 不要拆成两段关键帧：峰值处速度不连续会顿一下，EaseOutBack 本身就先冲过头再回落。
                // From 用当前缩放值：中途被打断时从这里继续长到 1.0。
                var scaleAnimation = new DoubleAnimation
                {
                    From = scaleStart,
                    Duration = AnimationSpeed.ScaleToTimeSpan(effectiveDuration),
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

                // 打断支持：从当前视觉状态接续（通常是出场动画播到一半又点了关闭）
                var (scaleStart, slideStart, opacityStart) = CaptureCurrentVisualState(child);

                child.BeginAnimation(UIElement.OpacityProperty, null);

                var exitScale = new ScaleTransform(scaleStart, scaleStart);
                var exitSlide = new TranslateTransform(0, slideStart);
                var exitGroup = new TransformGroup();
                exitGroup.Children.Add(exitScale);
                exitGroup.Children.Add(exitSlide);
                child.RenderTransform = exitGroup;
                // 与出场同一个基准点：缩回图标所在的方向
                child.RenderTransformOrigin = PopupScaleOrigin.Resolve(popup, child);
                child.Opacity = opacityStart;

                // 收场 = 出场动画的逆过程：
                //   出场 起始0.72 →(BackEase 冲到1.037)→ 回落 1.0
                //   收场 1.0 →(BackEase 先涨到1.037)→ 缩到 0.72 并消失
                // 「先放大一点再缩小」正是 Back 曲线前半段的效果，
                // 用一条 EaseInBack 就能同时表达这两种感觉，不必拼两段。
                double exitDuration = Math.Max(0.10,
                    duration * Math.Min(1.0, Math.Abs(scaleStart - PopupPopInStartScale)
                                              / Math.Max(0.001, Math.Abs(1.0 - PopupPopInStartScale))));

                // 缩放：EaseInBack 起手先向外涨一点，随后加速缩到底
                var exitScaleAnimation = new DoubleAnimation
                {
                    To = PopupPopInStartScale,
                    Duration = AnimationSpeed.ScaleToTimeSpan(exitDuration),
                    EasingFunction = new BackEase
                    {
                        EasingMode = EasingMode.EaseIn,
                        Amplitude = PopupPopInBackAmplitude
                    }
                };
                Storyboard.SetTargetProperty(exitScaleAnimation,
                    new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleX)"));
                var exitScaleAnimationY = exitScaleAnimation.Clone();
                Storyboard.SetTargetProperty(exitScaleAnimationY,
                    new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleY)"));

                // 淡出：与缩放同一条时间轴，缩到最小的时候正好透明
                var fadeOutAnimation = new DoubleAnimation
                {
                    To = 0,
                    Duration = AnimationSpeed.ScaleToTimeSpan(exitDuration)
                };
                fadeOutAnimation.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn };
                Storyboard.SetTargetProperty(fadeOutAnimation, new PropertyPath(UIElement.OpacityProperty));

                // 位移：下滑 10px，与出场的上滑镜像
                var slideAnimation = new DoubleAnimation
                {
                    To = 10,
                    Duration = AnimationSpeed.ScaleToTimeSpan(exitDuration)
                };
                slideAnimation.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn };
                Storyboard.SetTargetProperty(slideAnimation,
                    new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[1].(TranslateTransform.Y)"));

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

                    // 把变换上的动画彻底摘掉并归位。若不清理，缩放会停在 0.9，
                    // 下次打开时布局按带残留的尺寸测量，popupSize 变小、定位偏下。
                    ClearTransformAnimations(child);
                };

                // 收场动画一律 FillBehavior.Stop：结束后靠上面的清理归位
                fadeOutAnimation.FillBehavior = FillBehavior.Stop;
                slideAnimation.FillBehavior = FillBehavior.Stop;
                exitScaleAnimation.FillBehavior = FillBehavior.Stop;
                exitScaleAnimationY.FillBehavior = FillBehavior.Stop;

                sb.Begin(child);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }

        /// <summary>
        /// 读取菜单当前的视觉状态（缩放、纵向位移、透明度）。
        /// </summary>
        /// <remarks>
        /// 打断动画时要用它作为新动画的起点：从"现在看到的样子"继续，
        /// 而不是先跳回某个固定起点再重播。
        /// 读不到有效值时返回初始态（起始缩放、起始位移、半透明），
        /// 保证首次打开仍然是从小长出来。
        /// </remarks>
        private static (double Scale, double Slide, double Opacity) CaptureCurrentVisualState(FrameworkElement child)
        {
            double scale = PopupPopInStartScale;
            double slide = 10;
            double opacity = child?.Opacity ?? 1.0;

            if (double.IsNaN(opacity) || opacity < 0.05) opacity = 1.0;

            try
            {
                if (child?.RenderTransform is TransformGroup group && group.Children.Count >= 2
                    && group.Children[0] is ScaleTransform st
                    && group.Children[1] is TranslateTransform tt)
                {
                    // 动画进行中时，属性上挂着动画，直接读会拿到动画当前值
                    double currentScale = st.ScaleX;
                    double currentSlide = tt.Y;

                    if (!double.IsNaN(currentScale) && currentScale > 0.05 && currentScale < 1.2)
                    {
                        scale = currentScale;
                    }
                    if (!double.IsNaN(currentSlide) && Math.Abs(currentSlide) < 60)
                    {
                        slide = currentSlide;
                    }
                }
            }
            catch
            {
                // 结构异常时用默认起点
            }

            return (scale, slide, opacity);
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
