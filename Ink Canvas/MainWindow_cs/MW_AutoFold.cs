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

        /// <summary>
        /// 本次收纳是否要挡住"自动展开"定时器（tick 500ms）。
        /// 白板/PPT 退出、启动收纳由系统决定，定时器不该把它们当成"自动收纳软件已退出"而弹回来；
        /// 定时器自己触发的收纳则为 false，软件退出后才能自动展开。
        /// </summary>
        private bool _holdFoldAgainstAutoUnfold;

        // 热键 / 托盘菜单 / URI 触发时没有真实控件，用它表明"这是用户在操作"。
        private static readonly object FloatingBarUserTrigger = new object();

        // 浮动栏贴边位置（像素）。折叠位在这个基础上再减去任务栏高度。
        private const int FloatingBarEdgeExpandedDesktop = 100;
        private const int FloatingBarEdgeExpandedPpt = 60;
        private const int FloatingBarEdgeHiddenBase = -60;

        // 侧边栏位置：-50 完全收起，-10 折叠态露出展开按钮。
        private const int SidePanelEdgeHidden = -50;
        private const int SidePanelEdgePeek = -10;
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
        /// <param name="hold">
        /// 是否挡住"自动展开"定时器。白板/PPT 退出时的收纳必须为 true：
        /// 定时器看到"已收纳且不是用户收的"就会自动展开，把刚收好的工具栏又弹回来。
        /// </param>
        public async Task FoldFloatingBar(object sender, bool isAutoFoldCommand = false, bool instant = false,
            bool hold = false)
        {
            // 自动折叠可能由 System.Timers.Timer 在线程池触发；先切回窗口 Dispatcher，
            // 确保后续状态变更及整个异步过渡都在 UI 线程上执行。
            if (!Dispatcher.CheckAccess())
            {
                await Dispatcher.InvokeAsync(() => FoldFloatingBar(sender, isAutoFoldCommand, instant, hold)).Task.Unwrap();
                return;
            }

            foldFloatingBarByUser = sender != null;
            _holdFoldAgainstAutoUnfold = sender != null || hold;
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
        /// <param name="instant">是否跳过动画直接落位。</param>
        /// <param name="hold">是否挡住自动展开定时器；白板/PPT 退出要传 true。</param>
        internal Task FoldFloatingBarByCode(bool instant = false, bool hold = false)
            => FoldFloatingBar(null, false, instant, hold);

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
            if (isFloatingBarFolded && !instant)
            {
                // 标志已置位不代表外观已经落地：退出放映的展开流程可能随后把工具栏
                // 拉回屏幕内、把侧边栏把手收起（标志仍是折叠）。此时直接返回，用户看到的
                // 就是"托盘显示收纳态、工具栏却停在屏幕里、又找不到展开把手"的半折叠外观。
                if (!IsFoldedAppearanceIntact()) RestoreFoldedAppearance();
                return;
            }

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

            // 位置动画与侧边栏动画同时开始。串行（先等工具栏滑走 0.35s 再滑侧边栏）
            // 看起来就是"工具栏先闪一下，侧边栏才出来"。
            var marginTask = ViewboxFloatingBarMarginAnimationAsync(foldEdge, skipAnimation: instant);
            var sidePanelTask = AnimateSidePanelsAsync(SidePanelEdgePeek, instant);
            await Task.WhenAll(marginTask, sidePanelTask);

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

            Task leftMotion = Task.CompletedTask;
            await Dispatcher.InvokeAsync(() =>
            {
                if (edge == SidePanelEdgePeek)
                    LeftSidePanel.Visibility = isFloatingBarFolded ? Visibility.Visible : Visibility.Collapsed;

                // 两个把手都只布局一次到目标位置，位移交给弹簧收敛。
                // 旧实现逐帧动 Margin（布局属性）并固定睡 0.175s，既掉帧又把时长写死。
                bool animate = !skipAnimation;
                leftMotion = MoveLeftSidePanelTo(edge, Settings.Appearance.QuickPanelBottomOffset, animate);
                _ = MoveRightSidePanelTo(edge, Settings.Appearance.QuickPanelBottomOffset, animate);
            });

            // 等弹簧自然静止，而不是睡一个写死的动画时长
            await leftMotion;

            if (_sidePanelAnimVersion != thisVersion) return;

            await Dispatcher.InvokeAsync(() =>
            {
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
                await MoveQuickPanelTo(LeftUnFoldButtonQuickPanel, QuickPanelUnfoldedMargin, false);

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
                await MoveQuickPanelTo(RightUnFoldButtonQuickPanel, QuickPanelUnfoldedMargin, true);

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
                await MoveQuickPanelTo(LeftUnFoldButtonQuickPanel, QuickPanelFoldedMargin, false);
                LeftUnFoldButtonQuickPanel.Visibility = Visibility.Collapsed;
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
                await MoveQuickPanelTo(RightUnFoldButtonQuickPanel, QuickPanelFoldedMargin, true);
                RightUnFoldButtonQuickPanel.Visibility = Visibility.Collapsed;
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
            if (!Dispatcher.CheckAccess())
            {
                await Dispatcher.InvokeAsync(() => UnFoldFloatingBar(sender)).Task.Unwrap();
                return;
            }

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

            Task marginTask = Task.CompletedTask;
            if (currentMode == 0)
            {
                // 放映开始事件的应用段（UpdateSlideShowStatus、退出按钮与 PPT 专用项的可见性）
                // 还没跑：此刻工具栏还比最终形态窄，按它居中会偏右。如果在这里先摆一次、
                // 应用段结束再按最终宽度摆第二次，用户看到的就是"工具栏先居中、随后左移"。
                // 所以窗口期内不落位，由 OnPPTSlideShowBegin 在可见性应用完后统一落位一次。
                bool isPptBeginWindow = _pptSlideShowBeginning && !IsInPPTPresentationMode;
                if (!isPptBeginWindow)
                {
                    // 其余放映开始窗口期（例如外部演示源）仍走放映档：
                    // 只认 IsInPPTPresentationMode 会按桌面档算出任务栏上方的位置。
                    if (IsPptPlacementScene)
                        marginTask = ViewboxFloatingBarMarginAnimationAsync(FloatingBarEdgeExpandedPpt);
                    else
                        PureViewboxFloatingBarMarginAnimationInDesktopMode();
                }
            }

            // 侧边栏回收与工具栏回位同时进行，不要等位置动画播完再收
            await AnimateSidePanelsAsync(SidePanelEdgeHidden, !unfoldFloatingBarByUser);
            await marginTask;

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

                // 作废仍在等待落值的"展开/居中"写值：它们延时结束后只校验版本号、
                // 不重新判断折叠标志，晚到的正值会把这里刚写好的折叠位覆盖回屏幕内。
                Interlocked.Increment(ref _marginAnimationVersion);

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

        /// <summary>
        /// 折叠态下只允许写折叠边（负值，见 ComputeFoldEdge）。正向边距代表“展开/居中”，
        /// 折叠期间写入会把折叠位置覆盖成屏幕内的位置，而 isFloatingBarFolded 仍是 true——
        /// 用户看到的就是“托盘显示收纳态、工具栏却停在屏幕里”。
        /// </summary>
        private bool IsExpandedEdgeDuringFold(int marginFromEdge)
        {
            return isFloatingBarFolded && marginFromEdge > FloatingBarEdgeHiddenBase;
        }

        /// <summary>
        /// 折叠态外观是否完整：工具栏已离开屏幕、侧边栏展开把手可见。
        /// 两个条件缺一，都说明折叠流程被后来的展开/居中写值覆盖过——
        /// 只看 isFloatingBarFolded 会把这种半折叠外观当成"已经折叠"而放过。
        /// </summary>
        private bool IsFoldedAppearanceIntact()
        {
            if (LeftSidePanel.Visibility != Visibility.Visible) return false;
            return IsFloatingBarUiAbsentFromScreens();
        }

        /// <summary>
        /// 只重建折叠态外观（工具栏折叠位 + 侧边栏把手），不重放折叠过渡的其它副作用
        /// （切回放 UI、清墨迹、关白板等），避免"已经折叠、只是外观跑偏"时误伤用户现场。
        /// </summary>
        internal void RestoreFoldedAppearance()
        {
            if (!isFloatingBarFolded || currentMode != 0) return;

            if (!Dispatcher.CheckAccess())
            {
                // 折叠外观全是 UI 线程独占状态。PPT 联动事件过去在线程池线程上走到这里，
                // 异常被 ReapplyFoldPositionIfFolded 的 catch 吞掉，标志与外观就此脱节。
                // 与其静默失败，不如排回 UI 队列补一次（重复执行安全）。
                PostToUiThread(RestoreFoldedAppearance, "重建折叠外观");
                return;
            }

            ReapplyFoldPositionIfFolded();

            // 快捷面板正被用户打开时，把手本来就该收起（-50），不要把它拉回来
            bool quickPanelOpen = LeftUnFoldButtonQuickPanel.Visibility == Visibility.Visible
                || RightUnFoldButtonQuickPanel.Visibility == Visibility.Visible;
            if (!quickPanelOpen)
            {
                _ = AnimateSidePanelsAsync(SidePanelEdgePeek, true);
            }
        }

        /// <summary>
        /// 按当前折叠标志把浮动栏落到最终位置：折叠态落折叠位（屏幕外），展开态按桌面位置居中。
        /// 只用于流程收尾（退出白板/PPT、放映结束事件），不做过渡动画。
        /// 目标位置固定为桌面位置：退出放映的窗口期内 IsInPPTPresentationMode 可能仍是过期值，
        /// 用它选位置正是“退出后停在任务栏附近、没有重新居中”的来源之一。
        /// </summary>
        internal void ApplyFloatingBarFinalPosition()
        {
            if (!Dispatcher.CheckAccess())
            {
                PostToUiThread(ApplyFloatingBarFinalPosition, "浮动栏最终落位");
                return;
            }

            if (isFloatingBarFolded)
            {
                RestoreFoldedAppearance();
                return;
            }

            PureViewboxFloatingBarMarginAnimationInDesktopMode();
        }

        /// <summary>
        /// 按调用方给出的策略（白板 / PPT 的 ExitFoldMode）收敛浮动栏形态，可选落地最终位置。
        /// 已处于目标形态时入口会自行拦下，重复调用不会重播动画，因此可以幂等调用。
        /// </summary>
        /// <param name="shouldFold">调用方按各自策略计算出的目标形态。</param>
        /// <param name="applyFinalPosition">收敛后是否调用 <see cref="ApplyFloatingBarFinalPosition"/>。</param>
        /// <param name="context">日志上下文，例如“PPT 放映退出”。</param>
        internal async Task SettleFloatingBarAfterExitAsync(bool shouldFold, bool applyFinalPosition, string context)
        {
            // 收敛要跑动画与 UI 写值。调用方（放映结束事件）过去可能落在线程池线程上，
            // 这里先封送回 UI 线程，await 的调用方也就等到了真正的完成。
            if (!Dispatcher.CheckAccess())
            {
                await Dispatcher.InvokeAsync(() => SettleFloatingBarAfterExitAsync(shouldFold, applyFinalPosition, context))
                    .Task.Unwrap();
                return;
            }

            try
            {
                if (shouldFold)
                {
                    // hold: 由系统决定的收纳，不要被“自动展开”定时器随后弹回来
                    if (!isFloatingBarFolded) await FoldFloatingBarByCode(hold: true);
                }
                else if (isFloatingBarFolded)
                {
                    await UnFoldFloatingBarByCode();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"{context}：恢复浮动栏收纳状态失败: {ex.Message}", LogHelper.LogType.Warning);
            }

            if (applyFinalPosition) ApplyFloatingBarFinalPosition();
        }

        /// <summary>
        /// 退出白板后的落位策略：要求恢复收纳时走折叠入口，否则原地展开。
        /// </summary>
        /// <remarks>
        /// “原地展开”刻意不调用展开入口：展开入口会按当前场景重新居中，
        /// 而白板退出时用户希望工具栏留在他自己放的位置上。这里只同步
        /// 折叠标志、可见性和侧边栏把手，位置一个像素都不动。
        /// </remarks>
        /// <param name="restoreFolded">白板退出策略要求恢复收纳时为 true。</param>
        internal async Task RestoreFloatingBarAfterBoardExitAsync(bool restoreFolded)
        {
            if (restoreFolded)
            {
                // hold：系统决定的收纳，不要被“自动展开”定时器随后弹回来
                await FoldFloatingBarByCode(instant: true, hold: true);
                return;
            }

            if (isFloatingBarFolded)
            {
                // 策略要求展开、当前却处于折叠态：必须走展开入口把工具栏搬回屏幕内。
                // 只翻标志再改可见性会留下“标志=展开、工具栏仍在屏幕外”的半折叠外观，
                // 正是托盘与工具栏互相矛盾的那类 bug。
                await UnFoldFloatingBarByCode();
                return;
            }

            // 已是展开态：只做原地恢复。白板退出刻意不重新居中，位置留给用户。
            foldFloatingBarByUser = false;
            ViewboxFloatingBar.Visibility = Visibility.Visible;
            await AnimateSidePanelsAsync(SidePanelEdgeHidden, true);
        }

    }
}
