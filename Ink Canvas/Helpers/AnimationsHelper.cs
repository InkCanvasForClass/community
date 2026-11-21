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
        /// 每个菜单的动画状态：显式记录"当前处在缩放曲线的哪一档"，
        /// 不依赖 WPF 的 HasAnimatedProperties 判断是否被打断。
        /// </summary>
        /// <remarks>
        /// 为什么不用内省：动画播完后，属性上仍然挂着 Timeline（FillBehavior.Stop 也不移除对象），
        /// HasAnimatedProperties 依旧为 true，于是第二次打开会被误判成"打断"，
        /// 起点取成 1.0、终点也是 1.0 —— 动画就此消失。
        ///
        /// 这里自己维护状态，语义明确：
        /// 打开时从 CurrentScale 出发；收起时同样从 CurrentScale 出发。
        /// 动画结束把 CurrentScale 记为终值。
        /// </remarks>
        private sealed class AnimationToken
        {
            /// <summary>当前视觉缩放档位（0.72 ~ 1.0367）。</summary>
            public double CurrentScale = PopupPopInStartScale;

            /// <summary>当前视觉位移。</summary>
            public double CurrentSlide = 10;

            /// <summary>当前视觉透明度。</summary>
            public double CurrentOpacity = 0.5;

            /// <summary>是否有打开/收起流程正在进行（用于打断判断）。</summary>
            public bool IsTransitioning;

            /// <summary>收起流程是否在进行中（用于幂等守卫，防止重复播收起动画）。</summary>
            public bool IsClosing;

            /// <summary>代次：后发起的操作自增，旧动画回调据此作废。</summary>
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

        /// <summary>收场时长（秒）。比出场短一点，让关闭显得干脆。</summary>
        internal const double PopupPopOutDurationSeconds = 0.22;

        /// <summary>
        /// 打开动画中"涨到峰值"所占的时间比例。
        /// 取自观感序列：上升 8 段、回落 3 段，共 11 段 => 8/11 ≈ 0.727。
        /// </summary>
        internal const double PopupOpenPeakPercent = 8.0 / 11.0;

        /// <summary>
        /// 收起动画中"先涨到峰值"所占比例。与打开互逆：3/11 ≈ 0.273。
        /// </summary>
        internal const double PopupClosePeakPercent = 3.0 / 11.0;

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

                // 起点取自显式状态机，而不是内省 WPF 属性。
                //
                // 内省不可靠：动画播完后 Timeline 仍挂在属性上（Stop 也不移除对象），
                // HasAnimatedProperties 一直是 true，于是第二次打开会被误判成"打断"，
                // 起点取成 1.0、终点也是 1.0 —— 这就是"第二次打开动画没了"的原因。
                //
                // 自己记录档位则语义明确：
                // - 收起进行中被打断：从当时的档位继续升（例如 4 → 9 → 6）；
                // - 打开已完成后再开：从终态 6 开始，同样会有 6 → 9 → 6 的动画。
                var token = TokenFor(popup);
                double scaleStart = token.CurrentScale;
                double slideStart = token.CurrentSlide;
                double opacityStart = token.CurrentOpacity;

                // 打断检测：直接看变换上是否挂着动画，不依赖 IsTransitioning 标志。
                //
                // 用标志不可靠：它只在该动画自己的 Completed 里清除，
                // 若动画因窗口隐藏等原因不再出帧，Completed 可能迟迟不触发，标志就卡住；
                // 而 HasAnimatedProperties 反映的是"此刻属性是否被动画把持"，语义更准。
                //
                // 注意此前用它判断出过错（动画结束后标志仍在），
                // 那次的问题是**用它决定要不要播动画**；
                // 这里只用它决定**起点取实际值还是档位值**，
                // 即使误判为 true，也只是起点取当前视觉值——那本来就等于终态，无害。
                if (HasRunningAnimation(child))
                {
                    var (actualScale, actualSlide) = ReadActualVisual(child);
                    scaleStart = actualScale;
                    slideStart = actualSlide;
                    double actualOpacity = child.Opacity;
                    if (!double.IsNaN(actualOpacity) && actualOpacity > 0.02) opacityStart = actualOpacity;
                }

                AnimationLog.Log(popup, "SHOW",
                    $"start={scaleStart:F4} slide={slideStart:F1} op={opacityStart:F2}"
                    + $" transitioning={token.IsTransitioning}");

                // 起点已经等于终态时，仍要播一次"涨到峰值再回落"，
                // 否则用户会看到"第二次打开没有任何动画"。
                token.IsTransitioning = true;

                // 打开会接管收起流程：清掉守卫标志，
                // 否则收起被打断后标志会一直留着，导致后续再也收不起来。
                token.IsClosing = false;

                // 清掉旧动画（不再归位），目的是让下面的基值写入能生效。
                ClearTransformAnimations(child);
                child.BeginAnimation(UIElement.OpacityProperty, null);

                // 基值必须始终是**终态**（缩放 1、位移 0、目标透明度），不能写成当前状态。
                // 写成当前状态会导致两个 bug：
                //   1) 动画 To 省略时目标就是基值，From 又等于基值 => 缩放/位移完全没有变化；
                //   2) 若当前状态是收场残留的 0.72，菜单会永久停在 0.72（表现为"特别小"）。
                // 当前状态只用来决定 From（从哪儿开始），不影响终点。
                var scaleTransform = new ScaleTransform(1, 1);
                var slideTransform = new TranslateTransform(0, 0);
                var transformGroup = new TransformGroup();
                transformGroup.Children.Add(scaleTransform);
                transformGroup.Children.Add(slideTransform);

                child.RenderTransform = transformGroup;
                // 缩放基准点跟随触发按钮：从图标处放大、也缩回图标处。
                child.RenderTransformOrigin = PopupScaleOrigin.Resolve(popup, child);

                // 透明度基值同样是终态；From 会在动画里用 opacityStart 表示当前值
                child.Opacity = targetOpacity;

                popup.IsOpen = true;

                // 打开后内容才有真实尺寸：按它校正一次留白，
                // 避免打开前测不到尺寸导致留白偏小、峰值时四边被裁。
                PopupOverflowRoom.RefreshPadding(popup, PopupPopInPeakScale);

                // 注意：置顶由 PopupManagerHelper 统一管理，
                // 此处不再调用 BringPopupToFront，避免重复置顶导致闪烁

                var sb = new Storyboard();

                // To 全部省略：目标就是基值（终态）。
                // FillBehavior.Stop 让动画播完后释放属性、露出基值——基值已是终态，结果正确。
                // 时长：固定用完整时长，不按"剩余行程"压缩。
                //
                // 之前按 travelRatio 缩短会带来一个副作用：关键帧的百分比位置是固定的，
                // 时长一缩、行程又短，上升段速度就比正常打开快 1.2 倍以上，
                // 观感是"打断后突然变急"——正是用户说的"打断好奇怪"。
                //
                // 保持完整时长后，打断的只是"起点更靠近终点"，
                // 每单位时间的变化量与正常打开一致，速度感连续。
                double effectiveDuration = duration;

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

                // 缩放：显式三段关键帧 —— 起点 → 峰值 → 终值。
                //
                // 为什么不用单条 BackEase：
                // BackEase 的峰值由 Amplitude 固定决定，且只在"起点≠终点"时才有可观的行程。
                // 用户要求的是「从当前档位涨到峰值 9，再回落到 6」，起点可能就是 6，
                // 单条缓动在 From==To 时完全看不出动作。
                // 关键帧能把"先涨后落"写死成曲线本身，起点任意都成立。
                var scaleAnimation = new DoubleAnimationUsingKeyFrames
                {
                    Duration = AnimationSpeed.ScaleToTimeSpan(effectiveDuration),
                    FillBehavior = FillBehavior.Stop
                };

                // 起点：当前档位（打断时是中间值，正常打开时是 0.72 或 1.0）
                scaleAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(
                    scaleStart, KeyTime.FromPercent(0.0)));

                // 峰值：只在起点低于峰值时才需要"涨上去"；已高于峰值则不再上冲
                double peak = PopupPopInPeakScale;
                if (scaleStart < peak * 0.995)
                {
                    // 峰值时间点按实际行程比例调整，而不是固定 8/11。
                    //
                    // 固定比例的问题：起点越接近峰值，上升段行程越短，
                    // 却仍占用 72.7% 的时间 => 前半段极慢、后半段极快，观感失衡。
                    // 这里让"上升段/下降段"的时间比等于各自的行程比，
                    // 两段的速度就自然一致，打断后不会忽快忽慢。
                    double upDistance = Math.Abs(peak - scaleStart);
                    double downDistance = Math.Abs(1.0 - peak);
                    double totalDistance = upDistance + downDistance;
                    double peakPercent = totalDistance > 0.0001
                        ? upDistance / totalDistance
                        : PopupOpenPeakPercent;

                    // 留一点余量，避免峰值贴到 0% 或 100% 时看不出曲线
                    peakPercent = Math.Max(0.12, Math.Min(0.94, peakPercent));

                    scaleAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(
                        peak, KeyTime.FromPercent(peakPercent)));
                }

                // 终值：1.0
                scaleAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(
                    1.0, KeyTime.FromPercent(1.0)));

                Storyboard.SetTargetProperty(scaleAnimation,
                    new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleX)"));
                var scaleAnimationY = scaleAnimation.Clone();
                Storyboard.SetTargetProperty(scaleAnimationY,
                    new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleY)"));

                // 终值由基值保证（已是终态）；Completed 只负责同步状态机，
                // 让下一次打开能从"6"这样的正确档位继续。
                sb.Completed += (_, _) =>
                {
                    bool isCurrent = IsCurrent(popup, generation);
                    AnimationLog.Log(popup, isCurrent ? "SHOW-DONE" : "SHOW-DONE-STALE",
                        $"gen={generation} current={token.Generation} start={scaleStart:F4}");

                    // 只有**最新**那次操作才能写档位。
                    //
                    // 之前我改成"无条件同步终态"，结果制造了覆盖竞态：
                    // 被打断的旧 SHOW 的 Completed 会晚于新流程触发，
                    // 把新流程刚写入的正确档位又改回 1.0；
                    // 旧 HIDE 的 Completed 更会把档位写成 0.72，
                    // 而菜单其实已经重新打开了 —— 下次收起就从 0.72 起步。
                    if (!isCurrent) return;

                    token.CurrentScale = 1.0;
                    token.CurrentSlide = 0;
                    token.CurrentOpacity = targetOpacity;
                    token.IsTransitioning = false;
                };

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

                // 幂等守卫：收起动画播放期间 IsOpen 仍是 true，
                // 而 HideAllPopups 之类的批处理会被反复调用（日志实测同一次操作调了 11 次），
                // 每次都重播一遍收起动画，表现为"菜单在那儿抖"。
                // 已处于收起流程中就直接忽略后续请求。
                var guardToken = TokenFor(popup);
                if (guardToken.IsClosing)
                {
                    return;
                }
                guardToken.IsClosing = true;

                // 自增代次：若关闭动画期间用户又打开菜单，这次关闭就应当作废
                int generation = NextGeneration(popup);

                double targetOpacity = GetPopupTargetOpacity(popup);

                var sb = new Storyboard();

                // 起点取自同一个显式状态机（与出场共用），保证打断时接得上。
                var token = TokenFor(popup);
                double scaleStart = token.CurrentScale;
                double slideStart = token.CurrentSlide;

                // 打断检测：取屏幕上实际显示的值，避免从旧档位跳一下
                if (HasRunningAnimation(child))
                {
                    var (actualScale, actualSlide) = ReadActualVisual(child);
                    scaleStart = actualScale;
                    slideStart = actualSlide;
                }

                AnimationLog.Log(popup, "HIDE",
                    $"start={scaleStart:F4} slide={slideStart:F1} transitioning={token.IsTransitioning}");

                token.IsTransitioning = true;

                child.BeginAnimation(UIElement.OpacityProperty, null);

                // 基值必须是**收起的终态**（最小缩放、下滑 10px），不是 1.0。
                //
                // 原因：FillBehavior.Stop 在动画结束的瞬间就把属性回落到基值，
                // 而 Completed 要在这之后才触发。若基值是 1.0，
                // 那一帧会先画出 1.0 再被 Completed 改成 0.72 —— 肉眼看到"收起后闪一下变大"。
                // 实测逐帧采样里 0.78 → 1.00 的突跳就是它。
                // 把基值设成终态后，Stop 回落的位置与动画终点一致，不会闪。
                var exitScale = new ScaleTransform(PopupPopInStartScale, PopupPopInStartScale);
                var exitSlide = new TranslateTransform(0, 10);
                var exitGroup = new TransformGroup();
                exitGroup.Children.Add(exitScale);
                exitGroup.Children.Add(exitSlide);
                child.RenderTransform = exitGroup;
                // 与出场同一个基准点：缩回图标所在的方向
                child.RenderTransformOrigin = PopupScaleOrigin.Resolve(popup, child);
                child.Opacity = targetOpacity;

                // 收场曲线（按用户给的档位模型）：
                //   从当前档位出发 → 涨到峰值 → 缩到底并消失
                //   正常收起：6 → 7 → 8 → 9 → 8 → ... → 1
                //   打断收起：4 → 涨到 9 → 降到底
                // 用显式关键帧写死，起点任意都成立。
                double exitDuration = Math.Max(duration * 0.5, duration * 0.9);

                var exitScaleAnimation = new DoubleAnimationUsingKeyFrames
                {
                    Duration = AnimationSpeed.ScaleToTimeSpan(exitDuration),
                    FillBehavior = FillBehavior.Stop
                };
                exitScaleAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(
                    scaleStart, KeyTime.FromPercent(0.0)));
                // 先涨到峰值（起点已高于峰值时不再上冲）
                if (scaleStart < PopupPopInPeakScale * 0.995)
                {
                    // 峰值时间点必须按**行程比例**分配，不能固定 3/11。
                    //
                    // 固定值的问题：起点为 1.0 时，上升到峰值行程仅 0.0367，
                    // 而下降到最小态行程是 0.3167，相差 8.6 倍；
                    // 固定 27.3% 的时间给上升段，会让"上涨"只用极短时间，
                    // 看起来像"刚涨一点就立刻掉头"（实测 37ms 就走完峰值）。
                    //
                    // 让时间比 = 行程比后，两段速度一致，转折自然。
                    double upDistance = Math.Abs(PopupPopInPeakScale - scaleStart);
                    double downDistance = Math.Abs(PopupPopInPeakScale - PopupPopInStartScale);
                    double totalDistance = upDistance + downDistance;
                    double peakPercent = totalDistance > 0.0001
                        ? upDistance / totalDistance
                        : PopupClosePeakPercent;

                    peakPercent = Math.Max(0.08, Math.Min(0.92, peakPercent));

                    exitScaleAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(
                        PopupPopInPeakScale, KeyTime.FromPercent(peakPercent)));
                }
                // 再线性缩到底：终点与打开起点一致（0.72），做到"从哪来回哪去"
                exitScaleAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(
                    PopupPopInStartScale, KeyTime.FromPercent(1.0)));

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
                    // 无论代次是否过期，守卫标志都必须清零。
                    //
                    // 之前这里直接 return，导致"收起动画被打断后 IsClosing 永远是 true"，
                    // 之后所有收起请求都被幂等守卫挡掉——菜单再也收不起来。
                    token.IsClosing = false;

                    // 关闭动画期间被重新打开：本次关闭已过时，不能把菜单关掉
                    if (!IsCurrent(popup, generation))
                    {
                        AnimationLog.Log(popup, "HIDE-DONE-STALE",
                            $"gen={generation} current={token.Generation}");
                        return;
                    }

                    popup.IsOpen = false;
                    child.BeginAnimation(UIElement.OpacityProperty, null);
                    child.Opacity = targetOpacity;

                    // 归位到收起终态（最小缩放），并摘掉动画避免残留影响下次布局测量。
                    ClearTransformAnimations(child, PopupPopInStartScale, 10);

                    AnimationLog.Log(popup, "HIDE-DONE", $"from={scaleStart:F4}");

                    // 同步状态机：收起完成后的档位是"最小态"，
                    // 下次打开会从最小态重新长出来（0.72 → 峰值 → 1.0）。
                    token.CurrentScale = PopupPopInStartScale;
                    token.CurrentSlide = 10;
                    token.CurrentOpacity = targetOpacity * 0.5;
                    token.IsTransitioning = false;
                };

                // 收场动画一律 FillBehavior.Stop：结束后靠上面的清理归位
                fadeOutAnimation.FillBehavior = FillBehavior.Stop;
                slideAnimation.FillBehavior = FillBehavior.Stop;
                exitScaleAnimation.FillBehavior = FillBehavior.Stop;
                exitScaleAnimationY.FillBehavior = FillBehavior.Stop;

                sb.Begin(child);

                // 兜底：若 Completed 因任何原因未触发，守卫标志会永久卡住、
                // 菜单再也收不起来。这里定时强制清零（幂等，不影响正常流程）。
                var safetyTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = AnimationSpeed.ScaleMilliseconds(exitDuration * 1000) + TimeSpan.FromMilliseconds(400)
                };
                safetyTimer.Tick += (_, _) =>
                {
                    safetyTimer.Stop();
                    token.IsClosing = false;
                };
                safetyTimer.Start();
            }
            catch (Exception ex)
            {
                // 异常路径也必须清零，否则菜单无法再收起
                TokenFor(popup).IsClosing = false;
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        /// <summary>
        /// 读取菜单当前的视觉状态（缩放、纵向位移、透明度）。
        /// </summary>
        /// <remarks>
        /// 打断动画时要用它作为新动画的起点：从"现在看到的样子"继续，
        /// 而不是先跳回某个固定起点再重播。
        /// 读不到有效值时返回初始态（起始缩放、起始位移、半透明），
        /// 保证首次打开仍然是从小长出来。

        /// <summary>
        /// 读取菜单**当前真实显示**的缩放与位移。
        /// </summary>
        /// <remarks>
        /// 这是打断能否接上的关键：
        /// 状态机里的 CurrentScale 只在动画 Completed 时更新，
        /// 动画进行中它仍是**上一次结束时的档位**。
        /// 若打断时拿它当起点，画面会从当前显示值突然跳到旧档位（实测 0.9 → 0.72）。
        ///
        /// 动画进行中读取属性本身会返回动画的当前插值（WPF 属性值 = 动画覆盖基值），
        /// 所以直接读 ScaleTransform.ScaleX 就能拿到正确起点。
        /// </remarks>
        /// <summary>
        /// 判断变换上当前是否挂着正在跑的动画。
        /// </summary>
        /// <remarks>
        /// 仅用于决定"起点取实际视觉值还是状态机档位"，所以误判的代价很低：
        /// 误判为 true 时取当前视觉值，而它本就等于终态，结果无害。
        /// 这与"用它决定是否播动画"不同——那种用法会因动画结束后标志残留而丢动画。
        /// </remarks>
        private static bool HasRunningAnimation(FrameworkElement child)
        {
            try
            {
                if (child == null) return false;
                if (child.RenderTransform is not TransformGroup group) return false;

                foreach (var transform in group.Children)
                {
                    if (transform is ScaleTransform scale && scale.HasAnimatedProperties) return true;
                    if (transform is TranslateTransform translate && translate.HasAnimatedProperties) return true;
                }
            }
            catch
            {
                // 结构异常按"未在动画"处理
            }

            return false;
        }

        private static (double Scale, double Slide) ReadActualVisual(FrameworkElement child)
        {
            double scale = 1.0;
            double slide = 0.0;

            try
            {
                if (child?.RenderTransform is TransformGroup group && group.Children.Count >= 2)
                {
                    if (group.Children[0] is ScaleTransform st)
                    {
                        double v = st.ScaleX;
                        if (!double.IsNaN(v) && v > 0.05 && v < 1.5) scale = v;
                    }
                    if (group.Children[1] is TranslateTransform tt)
                    {
                        double v = tt.Y;
                        if (!double.IsNaN(v) && Math.Abs(v) < 80) slide = v;
                    }
                }
            }
            catch
            {
                // 结构异常时退回终态
            }

            return (scale, slide);
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
