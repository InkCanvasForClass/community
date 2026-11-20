using Ink_Canvas.Helpers;
using iNKORE.UI.WPF.Modern;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Ink_Canvas
{
    public partial class MainWindow : Ink_Canvas.Helpers.PerformanceTransparentWin
    {
        /// <summary>
        /// 浮动栏是否折叠的标志。
        /// </summary>
        public bool isFloatingBarFolded;

        /// <summary>
        /// 浮动栏正在改变隐藏模式的标志，用于防止重复操作。
        /// </summary>
        private bool isFloatingBarChangingHideMode;

        /// <summary>
        /// 折叠/展开过渡的目标状态：-1 无请求，0 展开，1 折叠。
        /// 过渡进行中再次请求只更新目标，由正在执行的循环读到最后一次意图后再跑一轮，
        /// 避免两个动画并发写 Margin 互相覆盖（历史上表现为"折叠了但工具栏还在屏幕上"）。
        /// </summary>
        private int _pendingFoldTarget = -1;
        private bool _pendingFoldInstant;
        private object _pendingFoldTrigger;
        private bool _foldTransitionRunning;

        // 热键 / 托盘菜单 / URI 触发时没有真实控件，用它表明"这是用户在操作"。
        private static readonly object FloatingBarUserTrigger = new object();

        // 浮动栏贴边位置（像素）。折叠位在这个基础上再减去任务栏高度。
        private const int FloatingBarEdgeExpandedDesktop = 100;
        private const int FloatingBarEdgeExpandedPpt = 60;
        private const int FloatingBarEdgeHiddenBase = -60;

        // 侧边栏位置：-50 完全收起，-10 折叠态露出展开按钮。
        private const int SidePanelEdgeHidden = -50;
        private const int SidePanelEdgePeek = -10;
        private static readonly TimeSpan SidePanelAnimationDuration = TimeSpan.FromSeconds(0.175);

        private int _sidePanelAnimVersion = 0;

        /// <summary>
        /// 自动收起侧栏计时器
        /// </summary>
        private DispatcherTimer _autoCollapseTimer;

        /// <summary>
        /// 立即关闭白板模式，恢复到批注模式。
        /// </summary>
        /// <remarks>
        /// 操作包括：
        /// 1. 检查是否正在显示或隐藏黑板，如果是则直接返回
        /// 2. 设置显示/隐藏黑板的标志为true
        /// 3. 立即隐藏所有子面板
        /// 4. 如果启用了自动切换多指手势，则关闭多指平移
        /// 5. 隐藏所有水印
        /// 6. 切换到批注模式
        /// 7. 设置退出按钮前景色为白色
        /// 8. 保持用户配置的应用主题
        /// 9. 200毫秒后重置显示/隐藏黑板的标志为false
        /// </remarks>
        private void CloseWhiteboardImmediately()
        {
            if (isDisplayingOrHidingBlackboard) return;
            isDisplayingOrHidingBlackboard = true;
            HideSubPanelsImmediately();
            WaterMarkTime.Visibility = Visibility.Collapsed;
            WaterMarkDate.Visibility = Visibility.Collapsed;
            BlackBoardWaterMark.Visibility = Visibility.Collapsed;
            ICCWaterMarkDark.Visibility = Visibility.Collapsed;
            ICCWaterMarkWhite.Visibility = Visibility.Collapsed;
            SwitchBackground(null, null);
            new Thread(() =>
            {
                Thread.Sleep(200);
                Application.Current.Dispatcher.Invoke(() => { isDisplayingOrHidingBlackboard = false; });
            }).Start();
        }

        /// <summary>
        /// 处理折叠浮动栏的鼠标点击事件。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">路由事件参数。</param>
        public async void FoldFloatingBar_MouseUp(object sender, MouseButtonEventArgs e)
        {
            await FoldFloatingBar(sender);
        }

        /// <summary>
        /// 折叠浮动栏，将其收纳到侧边栏。
        /// </summary>
        /// <param name="sender">事件发送者；null 表示由代码触发而非用户点击。</param>
        /// <param name="isAutoFoldCommand">是否为自动折叠命令。</param>
        /// <param name="instant">是否跳过动画直接落位（启动初始化用）。</param>
        public async Task FoldFloatingBar(object sender, bool isAutoFoldCommand = false, bool instant = false)
        {
            foldFloatingBarByUser = sender != null;
            unfoldFloatingBarByUser = false;
            await TransitionFloatingBarAsync(folded: true, instant: instant, trigger: sender);
        }

        /// <summary>
        /// 由用户操作触发折叠（热键、托盘菜单、URI），但没有真实控件可以当 sender。
        /// </summary>
        internal Task FoldFloatingBarByUser(bool instant = false)
            => FoldFloatingBar(FloatingBarUserTrigger, false, instant);

        /// <summary>
        /// 由程序决策触发折叠（白板/PPT 退出、启动初始化），不算用户操作。
        /// </summary>
        internal Task FoldFloatingBarByCode(bool instant = false)
            => FoldFloatingBar(null, false, instant);

        /// <summary>
        /// 折叠态迁移的唯一入口。过渡进行中再次请求时只更新目标，
        /// 由正在执行的循环读到最后一次意图后再跑一轮，避免并发动画互相覆盖。
        /// </summary>
        private async Task TransitionFloatingBarAsync(bool folded, bool instant, object trigger)
        {
            _pendingFoldTarget = folded ? 1 : 0;
            _pendingFoldInstant = instant;
            _pendingFoldTrigger = trigger;

            if (_foldTransitionRunning) return;

            _foldTransitionRunning = true;
            isFloatingBarChangingHideMode = true;
            try
            {
                while (_pendingFoldTarget >= 0)
                {
                    bool target = _pendingFoldTarget == 1;
                    bool targetInstant = _pendingFoldInstant;
                    object targetTrigger = _pendingFoldTrigger;
                    _pendingFoldTarget = -1;

                    if (target)
                        await ApplyFoldedAsync(targetInstant, targetTrigger);
                    else
                        await ApplyUnfoldedAsync();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"浮动栏折叠/展开失败: {ex}", LogHelper.LogType.Error);
            }
            finally
            {
                _foldTransitionRunning = false;
                isFloatingBarChangingHideMode = false;
            }
        }

        /// <summary>
        /// 执行折叠：先同步置位折叠标志，再统一处理可见性、位置与侧边栏。
        /// 标志必须在第一个 await 之前落下——调用方返回后紧接着的切工具/隐藏子面板
        /// 逻辑会用 !isFloatingBarFolded 判断是否居中，状态滞后会把折叠位覆盖成展开位。
        /// </summary>
        private async Task ApplyFoldedAsync(bool instant, object trigger)
        {
            if (isFloatingBarFolded && !instant) return;

            isFloatingBarFolded = true;

            await Dispatcher.InvokeAsync(() =>
            {
                InkCanvasForInkReplay.Visibility = Visibility.Collapsed;
                InkCanvasGridForInkReplay.Visibility = Visibility.Visible;
                InkCanvasGridForInkReplay.IsHitTestVisible = true;
                FloatingbarUIForInkReplay.Visibility = Visibility.Visible;
                FloatingbarUIForInkReplay.IsHitTestVisible = true;
                BlackboardUIGridForInkReplay.Visibility = Visibility.Visible;
                BlackboardUIGridForInkReplay.IsHitTestVisible = true;
                AnimationsHelper.HideWithFadeOut(BorderInkReplayToolBox);
                isStopInkReplay = true;
            });

            await Dispatcher.InvokeAsync(() =>
            {
                if (currentMode != 0) CloseWhiteboardImmediately();
                if (IsAnnotating && foldFloatingBarByUser && inkCanvas.Strokes.Count > 2)
                    ShowNotification(Properties.MainWindowStrings.Main_AutoFold_ClearingAndFolding);
                lastBorderMouseDownObject = trigger;
                CursorWithDelIcon_Click(trigger, null);
            });

            // 让上面这批 UI 状态变更在渲染队列里落地，而不是固定睡一段时间
            await Dispatcher.Yield(DispatcherPriority.Background);

            int foldEdge = ComputeFoldEdge();

            await Dispatcher.InvokeAsync(() =>
            {
                LeftBottomPanelForPPTNavigation.Visibility = Visibility.Collapsed;
                RightBottomPanelForPPTNavigation.Visibility = Visibility.Collapsed;
                LeftSidePanelForPPTNavigation.Visibility = Visibility.Collapsed;
                RightSidePanelForPPTNavigation.Visibility = Visibility.Collapsed;
                GridForFloatingBarDraging.Visibility = Visibility.Collapsed;
                HideSubPanels("cursor");
            });

            await ViewboxFloatingBarMarginAnimationAsync(foldEdge, skipAnimation: instant);
            await AnimateSidePanelsAsync(SidePanelEdgePeek, instant);

            // 自检：启动早期布局未稳定时首次位置计算可能落空，当场重设（用户不可见）
            await Dispatcher.InvokeAsync(() =>
            {
                if (!isFloatingBarFolded || currentMode != 0) return;

                ViewboxFloatingBar.UpdateLayout();
                if (IsFloatingBarUiAbsentFromScreens()) return;

                LogHelper.WriteLogToFile("[Toolbar] 折叠位置未生效，立即重设折叠位置", LogHelper.LogType.Warning);
                ViewboxFloatingBarMarginAnimationCore(foldEdge);
            });

            if (Settings.Automation.ThoroughlyHideWhenFolded)
            {
                await Dispatcher.InvokeAsync(() => { this.Visibility = Visibility.Hidden; });
            }
        }

        /// <summary>
        /// 计算折叠位：屏幕内坐标之外的隐藏位置（还要减去任务栏高度，避开任务栏遮挡）。
        /// </summary>
        private int ComputeFoldEdge()
        {
            double dpiScaleY = 1;
            var source = PresentationSource.FromVisual(this);
            if (source != null) dpiScaleY = source.CompositionTarget.TransformToDevice.M22;
            double taskbarHeight = ForegroundWindowInfo.GetTaskbarHeight(GetFloatingBarTargetScreen(), dpiScaleY);
            return FloatingBarEdgeHiddenBase - (int)Math.Round(taskbarHeight);
        }

        /// <summary>
        /// 侧边栏动画并等待落地。等待的是动画时长本身，不是凭经验拍的固定延时。
        /// </summary>
        private async Task AnimateSidePanelsAsync(int edge, bool skipAnimation)
        {
            int thisVersion = ++_sidePanelAnimVersion;

            await Dispatcher.InvokeAsync(() =>
            {
                if (edge == SidePanelEdgePeek)
                    LeftSidePanel.Visibility = isFloatingBarFolded ? Visibility.Visible : Visibility.Collapsed;

                var duration = skipAnimation ? TimeSpan.Zero : SidePanelAnimationDuration;
                var leftAnimation = new ThicknessAnimation
                {
                    Duration = duration,
                    From = LeftSidePanel.Margin,
                    To = new Thickness(edge, 0, 0, Settings.Appearance.QuickPanelBottomOffset),
                    EasingFunction = new CubicEase()
                };
                var rightAnimation = new ThicknessAnimation
                {
                    Duration = duration,
                    From = RightSidePanel.Margin,
                    To = new Thickness(0, 0, edge, Settings.Appearance.QuickPanelBottomOffset),
                    EasingFunction = new CubicEase()
                };
                LeftSidePanel.BeginAnimation(MarginProperty, leftAnimation);
                RightSidePanel.BeginAnimation(MarginProperty, rightAnimation);
            });

            if (!skipAnimation) await Task.Delay(SidePanelAnimationDuration);

            if (_sidePanelAnimVersion != thisVersion) return;

            await Dispatcher.InvokeAsync(() =>
            {
                LeftSidePanel.Margin = new Thickness(edge, 0, 0, Settings.Appearance.QuickPanelBottomOffset);
                RightSidePanel.Margin = new Thickness(0, 0, edge, Settings.Appearance.QuickPanelBottomOffset);
                if (edge == SidePanelEdgeHidden) LeftSidePanel.Visibility = Visibility.Collapsed;
            });
        }

        /// <summary>
        /// 处理左侧展开按钮显示快捷面板的鼠标点击事件。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">鼠标按钮事件参数。</param>
        /// <remarks>
        /// 操作包括：
        /// 1. 检查是否显示快捷面板
        /// 2. 如果显示快捷面板，则隐藏右侧快捷面板，显示左侧快捷面板并执行动画
        /// 3. 否则，调用展开浮动栏的方法
        /// </remarks>
        private async void LeftUnFoldButtonDisplayQuickPanel_MouseUp(object sender, RoutedEventArgs e)
        {
            if (Settings.Appearance.IsShowQuickPanel)
            {
                SidePannelMarginAnimation(-50);
                HideRightQuickPanel();
                LeftUnFoldButtonQuickPanel.Visibility = Visibility.Visible;
                await Dispatcher.InvokeAsync(() =>
                {
                    var marginAnimation = new ThicknessAnimation
                    {
                        Duration = TimeSpan.FromSeconds(0.1),
                        From = new Thickness(QuickPanelFoldedMargin, 0, 0, Settings.Appearance.QuickPanelBottomOffset),
                        To = new Thickness(QuickPanelUnfoldedMargin, 0, 0, Settings.Appearance.QuickPanelBottomOffset)
                    };
                    marginAnimation.EasingFunction = new CubicEase();
                    LeftUnFoldButtonQuickPanel.BeginAnimation(MarginProperty, marginAnimation);
                });
                await Task.Delay(100);

                await Dispatcher.InvokeAsync(() =>
                {
                    LeftUnFoldButtonQuickPanel.Margin = new Thickness(QuickPanelUnfoldedMargin, 0, 0, Settings.Appearance.QuickPanelBottomOffset);
                });

                StartAutoCollapseQuickPanelTimer();
            }
            else
            {
                _ = UnFoldFloatingBarByUser();
            }
        }

        /// <summary>
        /// 处理右侧展开按钮显示快捷面板的鼠标点击事件。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">路由事件参数。</param>
        /// <remarks>
        /// 操作包括：
        /// 1. 检查是否显示快捷面板
        /// 2. 如果显示快捷面板，则隐藏左侧快捷面板，显示右侧快捷面板并执行动画
        /// 3. 否则，调用展开浮动栏的方法
        /// </remarks>
        private async void RightUnFoldButtonDisplayQuickPanel_MouseUp(object sender, RoutedEventArgs e)
        {
            if (Settings.Appearance.IsShowQuickPanel)
            {
                SidePannelMarginAnimation(-50);
                HideLeftQuickPanel();
                RightUnFoldButtonQuickPanel.Visibility = Visibility.Visible;
                await Dispatcher.InvokeAsync(() =>
                {
                    var marginAnimation = new ThicknessAnimation
                    {
                        Duration = TimeSpan.FromSeconds(0.1),
                        From = new Thickness(0, 0, QuickPanelFoldedMargin, Settings.Appearance.QuickPanelBottomOffset),
                        To = new Thickness(0, 0, QuickPanelUnfoldedMargin, Settings.Appearance.QuickPanelBottomOffset)
                    };
                    marginAnimation.EasingFunction = new CubicEase();
                    RightUnFoldButtonQuickPanel.BeginAnimation(MarginProperty, marginAnimation);
                });
                await Task.Delay(100);

                await Dispatcher.InvokeAsync(() =>
                {
                    RightUnFoldButtonQuickPanel.Margin = new Thickness(0, 0, QuickPanelUnfoldedMargin, Settings.Appearance.QuickPanelBottomOffset);
                });

                StartAutoCollapseQuickPanelTimer();
            }
            else
            {
                _ = UnFoldFloatingBarByUser();
            }
        }

        /// <summary>
        /// 隐藏左侧快捷面板。
        /// </summary>
        /// <remarks>
        /// 操作包括：
        /// 1. 检查左侧快捷面板是否可见，如果不可见则直接返回
        /// 2. 执行左侧快捷面板的隐藏动画
        /// 3. 等待动画完成后，设置左侧快捷面板的边距并将其折叠
        /// </remarks>
        private async void HideLeftQuickPanel()
        {
            if (LeftUnFoldButtonQuickPanel.Visibility == Visibility.Visible)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    var marginAnimation = new ThicknessAnimation
                    {
                        Duration = TimeSpan.FromSeconds(0.1),
                        From = new Thickness(QuickPanelUnfoldedMargin, 0, 0, Settings.Appearance.QuickPanelBottomOffset),
                        To = new Thickness(QuickPanelFoldedMargin, 0, 0, Settings.Appearance.QuickPanelBottomOffset)
                    };
                    marginAnimation.EasingFunction = new CubicEase();
                    LeftUnFoldButtonQuickPanel.BeginAnimation(MarginProperty, marginAnimation);
                });
                await Task.Delay(100);

                await Dispatcher.InvokeAsync(() =>
                {
                    LeftUnFoldButtonQuickPanel.Margin = new Thickness(QuickPanelFoldedMargin, 0, 0, Settings.Appearance.QuickPanelBottomOffset);
                    LeftUnFoldButtonQuickPanel.Visibility = Visibility.Collapsed;
                });
            }
        }

        /// <summary>
        /// 隐藏右侧快捷面板。
        /// </summary>
        /// <remarks>
        /// 操作包括：
        /// 1. 检查右侧快捷面板是否可见，如果不可见则直接返回
        /// 2. 执行右侧快捷面板的隐藏动画
        /// 3. 等待动画完成后，设置右侧快捷面板的边距并将其折叠
        /// </remarks>
        private async void HideRightQuickPanel()
        {
            if (RightUnFoldButtonQuickPanel.Visibility == Visibility.Visible)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    var marginAnimation = new ThicknessAnimation
                    {
                        Duration = TimeSpan.FromSeconds(0.1),
                        From = new Thickness(0, 0, QuickPanelUnfoldedMargin, Settings.Appearance.QuickPanelBottomOffset),
                        To = new Thickness(0, 0, QuickPanelFoldedMargin, Settings.Appearance.QuickPanelBottomOffset)
                    };
                    marginAnimation.EasingFunction = new CubicEase();
                    RightUnFoldButtonQuickPanel.BeginAnimation(MarginProperty, marginAnimation);
                });
                await Task.Delay(100);

                await Dispatcher.InvokeAsync(() =>
                {
                    RightUnFoldButtonQuickPanel.Margin = new Thickness(0, 0, QuickPanelFoldedMargin, Settings.Appearance.QuickPanelBottomOffset);
                    RightUnFoldButtonQuickPanel.Visibility = Visibility.Collapsed;
                });
            }
        }

        /// <summary>
        /// 处理隐藏快捷面板的鼠标点击事件。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">鼠标按钮事件参数。</param>
        /// <remarks>
        /// 操作包括：
        /// 1. 隐藏左侧快捷面板
        /// 2. 隐藏右侧快捷面板
        /// </remarks>
        private void HideQuickPanel_MouseUp(object sender, MouseButtonEventArgs e)
        {
            StopAutoCollapseQuickPanelTimer();
            HideLeftQuickPanel();
            HideRightQuickPanel();
            SidePannelMarginAnimation(-10);
        }

        /// <summary>
        /// 处理展开浮动栏的鼠标点击事件。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">路由事件参数。</param>
        public async void UnFoldFloatingBar_MouseUp(object sender, MouseButtonEventArgs e)
        {
            await UnFoldFloatingBar(sender);
        }

        /// <summary>
        /// 展开浮动栏，将其从侧边栏恢复到正常状态。
        /// </summary>
        /// <param name="sender">事件发送者；null 表示由代码触发而非用户点击。</param>
        public async Task UnFoldFloatingBar(object sender)
        {
            unfoldFloatingBarByUser = sender != null && !ArePPTControlsVisible;
            foldFloatingBarByUser = false;
            await TransitionFloatingBarAsync(folded: false, instant: false, trigger: sender);
        }

        /// <summary>
        /// 由用户操作触发展开（热键、托盘菜单、URI），但没有真实控件可以当 sender。
        /// </summary>
        internal Task UnFoldFloatingBarByUser()
            => UnFoldFloatingBar(FloatingBarUserTrigger);

        /// <summary>
        /// 由程序决策触发展开（PPT 开始放映、退出恢复等），不算用户操作。
        /// </summary>
        internal Task UnFoldFloatingBarByCode()
            => UnFoldFloatingBar(null);

        /// <summary>
        /// 执行展开：恢复窗口与浮动栏可见性，按桌面/PPT 场景复位位置并收走侧边栏。
        /// </summary>
        private async Task ApplyUnfoldedAsync()
        {
            // 与折叠对称：标志必须在第一个 await 之前落下，
            // 否则调用方返回后读到的还是"已折叠"，会跳过一次居中/高光复位。
            isFloatingBarFolded = false;

            if (this.Visibility != Visibility.Visible)
            {
                await Dispatcher.InvokeAsync(() => { this.Visibility = Visibility.Visible; });
            }

            await Dispatcher.InvokeAsync(() =>
            {
                LeftUnFoldButtonQuickPanel.Visibility = Visibility.Collapsed;
                RightUnFoldButtonQuickPanel.Visibility = Visibility.Collapsed;
            });

            await Dispatcher.InvokeAsync(() =>
            {
                // 根据设置决定是否自动切换至批注模式
                if (Settings.Automation.IsAutoEnterAnnotationModeWhenExitFoldMode && currentMode == 0)
                {
                    PenIcon_Click(null, null);
                }

                // 只有在PPT放映模式下且页数有效时才显示翻页按钮
                if (ArePPTControlsVisible &&
                    IsInPPTPresentationMode &&
                    PPTManager?.IsInSlideShow == true &&
                    PPTManager?.SlidesCount > 0)
                {
                    var dops = Settings.PowerPointSettings.PPTButtonsDisplayOption.ToString();
                    var dopsc = dops.ToCharArray();
                    if (dopsc[0] == '2' && !isDisplayingOrHidingBlackboard) AnimationsHelper.ShowWithFadeIn(LeftBottomPanelForPPTNavigation);
                    if (dopsc[1] == '2' && !isDisplayingOrHidingBlackboard) AnimationsHelper.ShowWithFadeIn(RightBottomPanelForPPTNavigation);
                    if (dopsc[2] == '2' && !isDisplayingOrHidingBlackboard) AnimationsHelper.ShowWithFadeIn(LeftSidePanelForPPTNavigation);
                    if (dopsc[3] == '2' && !isDisplayingOrHidingBlackboard) AnimationsHelper.ShowWithFadeIn(RightSidePanelForPPTNavigation);
                }
                else
                {
                    LeftBottomPanelForPPTNavigation.Visibility = Visibility.Collapsed;
                    RightBottomPanelForPPTNavigation.Visibility = Visibility.Collapsed;
                    LeftSidePanelForPPTNavigation.Visibility = Visibility.Collapsed;
                    RightSidePanelForPPTNavigation.Visibility = Visibility.Collapsed;
                }

                if (currentMode == 0)
                {
                    // 布局没刷新时 ActualWidth 还是 0，按 0 尺寸算出来的位置会偏
                    ViewboxFloatingBar.UpdateLayout();
                }
            });

            if (currentMode == 0)
            {
                if (IsInPPTPresentationMode)
                    await ViewboxFloatingBarMarginAnimationAsync(FloatingBarEdgeExpandedPpt);
                else
                    PureViewboxFloatingBarMarginAnimationInDesktopMode();
            }

            await AnimateSidePanelsAsync(SidePanelEdgeHidden, !unfoldFloatingBarByUser);

            await Dispatcher.InvokeAsync(() =>
            {
                string selectedToolMode = GetCurrentSelectedMode();
                if (!string.IsNullOrEmpty(selectedToolMode))
                {
                    SetFloatingBarHighlightPosition(selectedToolMode);
                }
            });
        }

        /// <summary>
        /// 侧边栏边距动画（即发即忘）。需要等待动画落地的调用方请用 AnimateSidePanelsAsync。
        /// </summary>
        /// <param name="MarginFromEdge">侧边栏距边缘的边距值：-50 完全收起，-10 折叠态露出展开按钮。</param>
        /// <param name="isNoAnimation">是否跳过动画直接落位。</param>
        private void SidePannelMarginAnimation(int MarginFromEdge, bool isNoAnimation = false)
            => _ = AnimateSidePanelsAsync(MarginFromEdge, isNoAnimation);

        /// <summary>
        /// 启动自动收起侧栏计时器
        /// </summary>
        internal void StartAutoCollapseQuickPanelTimer()
        {
            if (!Settings.Appearance.IsAutoCollapseQuickPanel) return;

            StopAutoCollapseQuickPanelTimer();

            _autoCollapseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(Settings.Appearance.AutoCollapseQuickPanelDelay)
            };
            _autoCollapseTimer.Tick += AutoCollapseTimer_Tick;
            _autoCollapseTimer.Start();
        }

        /// <summary>
        /// 停止自动收起侧栏计时器
        /// </summary>
        internal void StopAutoCollapseQuickPanelTimer()
        {
            if (_autoCollapseTimer != null)
            {
                _autoCollapseTimer.Stop();
                _autoCollapseTimer.Tick -= AutoCollapseTimer_Tick;
                _autoCollapseTimer = null;
            }
        }

        /// <summary>
        /// 自动收起侧栏计时器到期时执行
        /// </summary>
        private void AutoCollapseTimer_Tick(object sender, EventArgs e)
        {
            StopAutoCollapseQuickPanelTimer();
            HideQuickPanelAndResetMargin();
        }

        /// <summary>
        /// 更新自动收起侧栏计时器（根据当前设置）
        /// </summary>
        internal void UpdateAutoCollapseQuickPanelTimer()
        {
            if (!Settings.Appearance.IsAutoCollapseQuickPanel)
            {
                StopAutoCollapseQuickPanelTimer();
            }
        }

        /// <summary>
        /// 隐藏快捷面板并重置侧边栏边距
        /// </summary>
        private void HideQuickPanelAndResetMargin()
        {
            if (LeftUnFoldButtonQuickPanel.Visibility == Visibility.Visible ||
                RightUnFoldButtonQuickPanel.Visibility == Visibility.Visible)
            {
                HideLeftQuickPanel();
                HideRightQuickPanel();
                SidePannelMarginAnimation(-10);
            }
        }

        /// <summary>
        /// 处理主窗口上的鼠标按下事件，用于检测点击其他区域时自动收起侧栏
        /// </summary>
        internal void HandleAutoCollapseOnMouseDown(MouseButtonEventArgs e)
        {
            if (!Settings.Appearance.IsAutoCollapseQuickPanel) return;
            if (!isFloatingBarFolded) return;

            bool isQuickPanelVisible = LeftUnFoldButtonQuickPanel.Visibility == Visibility.Visible ||
                                       RightUnFoldButtonQuickPanel.Visibility == Visibility.Visible;

            if (!isQuickPanelVisible) return;

            // 检查点击目标是否在快捷面板或侧边栏按钮内
            var hitTarget = e.OriginalSource as DependencyObject;
            if (hitTarget != null)
            {
                // 如果点击在快捷面板或侧边栏按钮内，不收起
                if (IsDescendantOf(hitTarget, LeftUnFoldButtonQuickPanel) ||
                    IsDescendantOf(hitTarget, RightUnFoldButtonQuickPanel) ||
                    IsDescendantOf(hitTarget, LeftSidePanel) ||
                    IsDescendantOf(hitTarget, RightSidePanel))
                {
                    // 重置计时器
                    StartAutoCollapseQuickPanelTimer();
                    return;
                }
            }

            // 点击了其他区域，立即收起
            StopAutoCollapseQuickPanelTimer();
            HideQuickPanelAndResetMargin();
        }

        /// <summary>
        /// 检查一个元素是否是另一个元素的子元素
        /// </summary>
        private static bool IsDescendantOf(DependencyObject child, FrameworkElement parent)
        {
            if (parent == null) return false;
            var current = child;
            while (current != null)
            {
                if (current == parent) return true;
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            return false;
        }

        /// <summary>
        /// 主窗口鼠标按下预览事件，用于检测点击其他区域时自动收起侧栏
        /// </summary>
        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            HandleAutoCollapseOnMouseDown(e);
        }

        private bool IsFloatingBarUiAbsentFromScreens()
        {
            try
            {
                if (ViewboxFloatingBar == null) return true;
                if (Settings?.Automation?.ThoroughlyHideWhenFolded == true &&
                    (!IsVisible || Visibility != Visibility.Visible))
                    return true;
                if (ViewboxFloatingBar.Visibility != Visibility.Visible)
                    return true;
                return IsOutsideOfScreenHelper.IsOutsideOfScreen(ViewboxFloatingBar);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"检测浮动栏可见性时出错: {ex.Message}", LogHelper.LogType.Warning);
                return true;
            }
        }

        /// <summary>
        /// 若当前处于折叠状态，则按当前缩放/任务栏高度重新应用折叠位置并返回 true；
        /// 未折叠（或非批注模式）返回 false，由调用方继续执行正常的展开居中逻辑。
        /// 用于"应用缩放""加载设置""重建工具栏"等会顺带重算位置的动作：
        /// 这些动作发生在启动折叠之后，不做保护会把工具栏拉回屏幕内
        /// （折叠标志仍为 true，但工具栏实际可见——用户看到"自动收纳失效"）。
        /// </summary>
        internal bool ReapplyFoldPositionIfFolded()
        {
            if (!isFloatingBarFolded || currentMode != 0) return false;

            try
            {
                double dpiScaleYForFold = 1;
                var srcForFold = PresentationSource.FromVisual(this);
                if (srcForFold != null) dpiScaleYForFold = srcForFold.CompositionTarget.TransformToDevice.M22;
                double taskbarHeightForFold = ForegroundWindowInfo.GetTaskbarHeight(GetFloatingBarTargetScreen(), dpiScaleYForFold);

                // 同步写入折叠位置（无动画、不展开），并走一次布局让可见性判定读到最终值
                ViewboxFloatingBarMarginAnimationCore(-60 - (int)Math.Round(taskbarHeightForFold));
                ViewboxFloatingBar.UpdateLayout();
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"重设折叠位置失败: {ex.Message}", LogHelper.LogType.Warning);
                return false;
            }
        }

    }
}
