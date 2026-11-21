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

        // —— 菜单出场参数：对标 Inkeys 的实测值 ——
        // Inkeys 的弹出面板用自研 EaseOutBack(c=1.1)：峰值 1.0447 出现在 t≈0.651，
        // 面板实际放大比例 = compact + (1-compact)·f(t)，compact 是"紧凑态宽度比"。
        // 我们的菜单没有紧凑态概念，取一个视觉上等价的起始比例即可；
        // 过冲交给 BackEase 产生，不要手写峰值关键帧（那样曲线会两段拼接、手感发硬）。

        /// <summary>菜单出场起始缩放。越小越"从按钮里长出来"。</summary>
        private const double PopupPopInStartScale = 0.72;

        /// <summary>
        /// BackEase 过冲幅度，用于复刻 Inkeys 的 EaseOutBack(c = 1.1) 手感。
        /// </summary>
        /// <remarks>
        /// 注意 WPF 的 <c>Amplitude</c> 与业界 EaseOutBack 的 <c>c</c> **不是同一个量**，
        /// 直接照抄 c=1.1 会让过冲从 4.5% 变成 43%（实测 Amplitude=1.1 峰值 1.4319）。
        ///
        /// 换算方式：先按"目标峰值"反推。Inkeys 的 c=1.1 峰值 1.0447（t≈0.65），
        /// 实测 WPF Amplitude=0.28 时峰值 1.0446（t≈0.64），曲线形态一致。
        ///
        /// 但直接取 0.28 观感偏弱：菜单起始缩放是 0.72，过冲量被
        /// "start + (1-start)·peak" 压缩后视觉峰值只有 1.0125（过冲 1.25%），
        /// 肉眼几乎看不出"变大再回落"。
        /// 因此这里取 0.5，实测视觉峰值 1.0367（过冲 3.67%）——看得出有弹性但不夸张。
        /// 这个值是**按观感调的，不是 Inkeys 原始参数**；改它请重新实测视觉峰值。
        /// </remarks>
        private const double PopupPopInBackAmplitude = 0.5;

        /// <summary>菜单出场时长（秒），对齐 Inkeys 的 0.4s。</summary>
        internal const double PopupPopInDurationSeconds = 0.4;

        /// <summary>
        /// 给菜单（Popup.Child，即包含蓝色边框的整个面板）建立统一的变换结构。
        /// </summary>
        /// <remarks>
        /// 出场与收场必须用**同一个结构**，否则两者会互相破坏：
        /// 出场按 <c>TransformGroup.Children[i]</c> 绑定动画路径，如果上一次收场把
        /// <c>RenderTransform</c> 换成了裸 <see cref="TranslateTransform"/>，
        /// 下一次出场绑定时路径就对不上，位移残留会让菜单停偏。
        ///
        /// 缩放原点固定为中心：菜单通常在触发按钮上方，若把原点设在按钮中心，
        /// 那个点落在菜单坐标系之外，缩放会把整个菜单往菜单外收缩，看起来就是定位跑偏。
        /// </remarks>
        private static (ScaleTransform Scale, TranslateTransform Slide) PreparePopupTransform(
            FrameworkElement child, double startScale, double startSlide)
        {
            var scale = new ScaleTransform(startScale, startScale);
            var slide = new TranslateTransform(0, startSlide);
            var group = new TransformGroup();
            group.Children.Add(scale);
            group.Children.Add(slide);

            // 根元素若自带**有用的**变换，嵌入组内保留，不要直接覆盖——
            // 直接覆盖会悄悄丢掉布局代码设置的变换。
            //
            // 注意两个陷阱：
            // 1) RenderTransform 未设置时 WPF 返回的是默认的 MatrixTransform（Identity），
            //    它不是"用户设置的变换"，塞进组里会平白多一层；
            // 2) 若上次保留过外部变换，这次必须继续保留，不能因为组里已有缩放/位移就丢弃。
            var existing = child.RenderTransform as Transform;
            if (existing is TransformGroup previousGroup
                && IsOurPopupTransform(previousGroup)
                && previousGroup.Children.Count > 2)
            {
                // 我们上次建的组里，第 3 项起是保留下来的外部变换
                for (int i = 2; i < previousGroup.Children.Count; i++)
                {
                    group.Children.Add(previousGroup.Children[i]);
                }
            }
            else if (existing != null
                     && !IsOurPopupTransform(existing)
                     && !IsIdentityTransform(existing))
            {
                group.Children.Add(existing);
            }

            child.RenderTransform = group;
            child.RenderTransformOrigin = new Point(0.5, 0.5);

            return (scale, slide);
        }

        /// <summary>判断该变换是否属于本方法建立的结构（缩放 + 位移，可能附带原变换）。</summary>
        private static bool IsOurPopupTransform(Transform transform)
        {
            return transform is TransformGroup group
                && group.Children.Count >= 2
                && group.Children[0] is ScaleTransform
                && group.Children[1] is TranslateTransform;
        }

        /// <summary>
        /// 是否为"无实际效果"的变换。
        /// RenderTransform 未显式设置时 WPF 会返回一个 Identity 的 MatrixTransform，
        /// 不能把它当成用户设置的变换保留下来，否则每次都会多一层。
        /// </summary>
        private static bool IsIdentityTransform(Transform transform)
        {
            if (transform == null) return true;
            if (transform is MatrixTransform matrix) return matrix.Matrix.IsIdentity;
            return transform.Value.IsIdentity;
        }

        /// <summary>
        /// EaseOutSine：Inkeys 里透明度用的曲线，起步快、收尾平滑，不带过冲。
        /// </summary>
        private static QuadraticEase CreateEaseOutSine()
        {
            // WPF 没有内置 Sine 缓动；EaseOutQuad 在 0.4s 的观感上与之接近，
            // 且同样单调递增、无过冲，符合"透明度绝不能越过目标再回来"的约束。
            return new QuadraticEase { EasingMode = EasingMode.EaseOut };
        }

        public static void ShowPopupWithSlideAndFade(Popup popup, double duration = PopupPopInDurationSeconds)
        {
            try
            {
                if (popup == null)
                    throw new ArgumentNullException(nameof(popup));

                if (popup.IsOpen) return;

                var child = popup.Child as FrameworkElement;
                if (child == null)
                {
                    popup.IsOpen = true;
                    return;
                }

                // 获取目标透明度（菜单透明度设置）
                double targetOpacity = GetPopupTargetOpacity(popup);

                child.Visibility = Visibility.Visible;
                child.Opacity = targetOpacity * 0.5;

                // 位移动画用 Margin 之外的 RenderTransform，缩放原点固定在元素中心。
                //
                // 不要把原点设成"触发按钮中心"：那个点在菜单坐标系里通常落在菜单外面
                // （菜单显示在按钮上方），WPF 会以菜单外的一点为基准做缩放，
                // 结果是整个菜单朝外收缩——视觉上就是"定位跑偏"。
                // 菜单自身上下内缩的感觉交给位移补偿，而不是移动缩放原点。
                PreparePopupTransform(child, PopupPopInStartScale, 10);

                popup.IsOpen = true;

                // 注意：置顶由 PopupManagerHelper 统一管理，
                // 此处不再调用 BringPopupToFront，避免重复置顶导致闪烁

                var sb = new Storyboard();

                // 透明度单独用 EaseOutSine：它与缩放的 Back 曲线共用时间轴，
                // 但绝不能也带过冲——透明度越过 1 再回来会肉眼可见地闪一下。
                var fadeInAnimation = new DoubleAnimation
                {
                    From = targetOpacity * 0.5,
                    To = targetOpacity,
                    Duration = TimeSpan.FromSeconds(duration),
                    EasingFunction = CreateEaseOutSine()
                };
                Storyboard.SetTargetProperty(fadeInAnimation, new PropertyPath(UIElement.OpacityProperty));

                var slideAnimation = new DoubleAnimation
                {
                    From = 10,
                    To = 0,
                    Duration = TimeSpan.FromSeconds(duration)
                };
                slideAnimation.EasingFunction = new CubicEase();
                Storyboard.SetTargetProperty(slideAnimation,
                    new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[1].(TranslateTransform.Y)"));

                // 缩放入场：一条 BackEase 曲线即可完成"小 → 略大 → 回落"。
                // 不要拆成"升到峰值 + 回落"两个关键帧：那是两段缓动拼接，
                // 峰值处速度不连续，看起来会顿一下。EaseOutBack 本身就是先冲过头再回落。
                var scaleAnimation = new DoubleAnimation
                {
                    From = PopupPopInStartScale,
                    To = 1.0,
                    Duration = TimeSpan.FromSeconds(duration),
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

                sb.Children.Add(fadeInAnimation);
                sb.Children.Add(slideAnimation);
                sb.Children.Add(scaleAnimation);
                sb.Children.Add(scaleAnimationY);

                sb.Begin(child);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }

        public static void HidePopupWithSlideAndFade(Popup popup, double duration = 0.15)
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

                double targetOpacity = GetPopupTargetOpacity(popup);

                var sb = new Storyboard();

                var fadeOutAnimation = new DoubleAnimation
                {
                    From = targetOpacity,
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
                Storyboard.SetTargetProperty(slideAnimation,
                    new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[1].(TranslateTransform.Y)"));

                sb.Children.Add(fadeOutAnimation);
                sb.Children.Add(slideAnimation);

                // 与出场保持同一套变换结构（缩放 1、位移 0 起点），
                // 收场结束后把状态复位到"出场前的初值"，下次出场直接从初值开始。
                PreparePopupTransform(child, 1.0, 0);

                sb.Completed += (s, e) =>
                {
                    popup.IsOpen = false;
                    child.Opacity = targetOpacity;
                    PreparePopupTransform(child, 1.0, 0);
                };

                sb.Begin(child);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
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
