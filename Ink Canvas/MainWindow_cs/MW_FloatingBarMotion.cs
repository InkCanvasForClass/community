using Ink_Canvas.Controls.Toolbar.FloatingToolbar;
using Ink_Canvas.Helpers;
using Ink_Canvas.Helpers.Motion;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;

namespace Ink_Canvas
{
    public partial class MainWindow
    {
        private SpringTranslateAnimator _floatingBarSpring;
        private SpringTranslateAnimator _leftSidePanelSpring;
        private SpringTranslateAnimator _rightSidePanelSpring;
        private SpringTranslateAnimator _idleMiniBarSpring;

        /// <summary>浮动栏的弹簧驱动器（每窗口一个，复用同一个 TranslateTransform）。</summary>
        private SpringTranslateAnimator FloatingBarSpring =>
            _floatingBarSpring ??= SpringTranslateAnimator.For(ViewboxFloatingBar);

        private SpringTranslateAnimator LeftSidePanelSpring =>
            _leftSidePanelSpring ??= SpringTranslateAnimator.For(LeftSidePanel);

        private SpringTranslateAnimator RightSidePanelSpring =>
            _rightSidePanelSpring ??= SpringTranslateAnimator.For(RightSidePanel);

        /// <summary>当前手感预设对应的弹簧参数。</summary>
        private static SpringSpec ToolbarSpringSpec =>
            SpringSpec.FromPreset(Settings.Appearance.ToolbarMotionPreset);

        /// <summary>是否应该播放位移动画（设置里关掉动画时一律直接到位）。</summary>
        private static bool IsFloatingBarMotionEnabled => !Settings.Appearance.DisableToolbarAnimation;

        /// <summary>
        /// 在"直接改写布局位置"之前调用：清掉浮动栏及两侧把手的弹簧偏移。
        /// 拖动把手、加载设置、启动首帧这类操作以 Margin 为真值来源，
        /// 若弹簧还留着视觉偏移，两者会叠加（拖动发飘、松手回跳）。
        /// </summary>
        internal void SnapFloatingBarMotionToLayout()
        {
            _floatingBarSpring?.SnapToLayout();
            _leftSidePanelSpring?.SnapToLayout();
            _rightSidePanelSpring?.SnapToLayout();
            _idleMiniBarSpring?.SnapToLayout();
        }

        /// <summary>
        /// 把浮动栏落到 <paramref name="targetMargin"/>：
        /// 布局属性直接写成目标值（只触发一次布局），视觉位移交给弹簧在合成线程上收敛。
        /// </summary>
        /// <remarks>
        /// 旧实现用 <see cref="ThicknessAnimation"/> 直接动 Margin。Margin 是布局属性，
        /// 每帧变化都要重新测量与排列，动画期间 UI 线程被布局吃满，长距离滑动就掉帧；
        /// 而缓动是从零速度开始的固定曲线，被下一次操作打断时只能硬切，观感更差。
        /// 弹簧方案下布局只发生一次，逐帧只更新 TranslateTransform（合成线程插值），
        /// 并且把当前偏移与速度交给下一次弹簧，中断即重定向，不会出现顿挫。
        /// </remarks>
        /// <param name="targetMargin">目标 Margin。</param>
        /// <param name="animate">是否播放动画；为 false 时立刻到位。</param>
        /// <returns>弹簧静止后完成的任务；不播放动画时返回已完成任务。</returns>
        private Task MoveFloatingBarTo(Thickness targetMargin, bool animate)
        {
            // 还没提交的居中补偿先并进 Margin：下面拿 currentMargin 当视觉起点，
            // 少算这段会让弹簧一起步就跳。
            FlushFloatingBarCenterFollow();

            var spring = FloatingBarSpring;

            if (!animate || !IsFloatingBarMotionEnabled)
            {
                spring.SnapToLayout();
                ViewboxFloatingBar.BeginAnimation(MarginProperty, null);
                ViewboxFloatingBar.Margin = targetMargin;
                // 落位即真值：把跟随基准一起挪过来，否则下一次跨度变化会按旧位置补错量
                SyncFloatingBarCenterFollowBaseline();
                return Task.CompletedTask;
            }

            // 视觉连续：起点 = 当前布局位置 + 当前弹簧偏移。
            // 这样即使上一次动画还没跑完，接手时也不会先跳回布局位置。
            var currentMargin = ViewboxFloatingBar.Margin;
            var offset = spring.CurrentOffset;
            var velocity = spring.CurrentVelocity;
            double visualLeft = currentMargin.Left + offset.X;
            double visualTop = currentMargin.Top + offset.Y;

            // 跨度补间（收起/展开的胶囊宽度动画）还没跑完时，调用方按「当前实排宽度」算出的
            // 横向居中位置是中间值：独立方形刚让出 58px 排布、胶囊还停在 718，用 779 重新居中
            // 会把整条栏横着推 30.5px（日志实测 Margin.Left 420 → 450.5，就是收起时往右闪那一下）。
            // 跨度动画期间横向位置归居中跟随所有：它按逐帧跨度托住中心，收尾再提交终值。
            // 折叠/换边这类大幅位移（>64px）仍照常落位，不受影响。
            if (ToolbarRegistry.IsExtentAnimating &&
                System.Math.Abs(targetMargin.Left - currentMargin.Left) <= 64.0)
            {
                ToolbarRegistry.MotionTrace(
                    $"落位跳过横向居中 跨度补间中 目标Left={targetMargin.Left:F1} 保持={currentMargin.Left:F1} 实排宽={ViewboxFloatingBar.ActualWidth:F1}");
                targetMargin = new Thickness(currentMargin.Left, targetMargin.Top, targetMargin.Right, targetMargin.Bottom);
            }

            // 布局一次落到终点；之后所有逐帧变化都只发生在 RenderTransform 上。
            ViewboxFloatingBar.BeginAnimation(MarginProperty, null);
            ViewboxFloatingBar.Margin = targetMargin;

            // 跨度补间期间横向位置归居中跟随所有（上面那条守卫），基准不能在这里改写，
            // 否则会把它的托底补偿打断；其余情况落位即真值，基准跟着走。
            if (!ToolbarRegistry.IsExtentAnimating) SyncFloatingBarCenterFollowBaseline();

            var start = new Vector(visualLeft - targetMargin.Left, visualTop - targetMargin.Top);

            ToolbarRegistry.MotionTrace(
                $"落位 target=({targetMargin.Left:F1},{targetMargin.Top:F1}) 视觉起点=({visualLeft:F1},{visualTop:F1}) 偏移=({offset.X:F1},{offset.Y:F1}) 弹簧起点=({start.X:F1},{start.Y:F1})");

            return spring.RunAsync(start, default, velocity, ToolbarSpringSpec);
        }

        /// <summary>
        /// 把手（左/右）位移：同一套"布局一次 + 弹簧收敛"的做法。
        /// 左右共用一个实现，避免两条几乎一样的代码各自漂移。
        /// </summary>
        private Task MoveSidePanelTo(FrameworkElement panel, SpringTranslateAnimator spring,
            double edge, bool isRightSide, double top, bool animate)
        {
            var target = isRightSide ? new Thickness(0, 0, edge, top) : new Thickness(edge, 0, 0, top);

            if (!animate || !IsFloatingBarMotionEnabled)
            {
                spring.SnapToLayout();
                panel.BeginAnimation(MarginProperty, null);
                panel.Margin = target;
                return Task.CompletedTask;
            }

            var currentMargin = panel.Margin;
            var offset = spring.CurrentOffset;
            var velocity = spring.CurrentVelocity;

            // 把边距换算成"元素距屏幕边缘的距离"，两侧就能用同一套位移计算。
            // 右侧用 Right 边距：元素会随边距变小而右移，与左侧 Left 的符号相反，
            // 所以位移方向要取反，否则右侧动画会朝反方向跑。
            double currentEdgeDistance = isRightSide ? currentMargin.Right : currentMargin.Left;

            panel.BeginAnimation(MarginProperty, null);
            panel.Margin = target;

            double startHorizontal = currentEdgeDistance - edge;
            if (isRightSide) startHorizontal = -startHorizontal;

            return spring.RunAsync(
                new Vector(startHorizontal, 0), default, velocity, ToolbarSpringSpec);
        }

        private Task MoveLeftSidePanelTo(double edge, double top, bool animate)
            => MoveSidePanelTo(LeftSidePanel, LeftSidePanelSpring, edge, false, top, animate);

        private Task MoveRightSidePanelTo(double edge, double top, bool animate)
            => MoveSidePanelTo(RightSidePanel, RightSidePanelSpring, edge, true, top, animate);

        /// <summary>
        /// 迷你栏（Issue #285） flies 位移：与主工具栏同一套弹簧，三个浮动部件手感一致。
        /// </summary>
        private void MoveIdleMiniBarTo(double left, double top)
        {
            if (IdleMiniBar == null) return;

            var spring = _idleMiniBarSpring ??= SpringTranslateAnimator.For(IdleMiniBar);
            var target = new Thickness(left, top, 0, 0);

            if (!IsFloatingBarMotionEnabled)
            {
                spring.SnapToLayout();
                IdleMiniBar.BeginAnimation(MarginProperty, null);
                IdleMiniBar.Margin = target;
                return;
            }

            var offset = spring.CurrentOffset;
            var velocity = spring.CurrentVelocity;
            double visualLeft = IdleMiniBar.Margin.Left + offset.X;
            double visualTop = IdleMiniBar.Margin.Top + offset.Y;

            IdleMiniBar.BeginAnimation(MarginProperty, null);
            IdleMiniBar.Margin = target;

            _ = spring.RunAsync(
                new Vector(visualLeft - left, visualTop - top), default, velocity, ToolbarSpringSpec);
        }

        /// <summary>
        /// 快捷面板（把手展开后弹出的那一条）位移。和主工具栏同一套弹簧，
        /// 保证"展开工具栏"与"展开快捷面板"的手感一致，不再是两种不同的时长与曲线。
        /// </summary>
        private Task MoveQuickPanelTo(FrameworkElement panel, double edge, bool isRightSide)
        {
            var spring = SpringTranslateAnimator.For(panel);
            var top = Settings.Appearance.QuickPanelBottomOffset;
            var target = isRightSide ? new Thickness(0, 0, edge, top) : new Thickness(edge, 0, 0, top);

            if (!IsFloatingBarMotionEnabled)
            {
                spring.SnapToLayout();
                panel.BeginAnimation(MarginProperty, null);
                panel.Margin = target;
                return Task.CompletedTask;
            }

            var offset = spring.CurrentOffset;
            var velocity = spring.CurrentVelocity;
            double currentEdgeDistance = isRightSide ? panel.Margin.Right : panel.Margin.Left;

            panel.BeginAnimation(MarginProperty, null);
            panel.Margin = target;

            // 与把手同一套换算：右侧 Right 边距的方向与左侧相反，位移要取反。
            double startHorizontal = currentEdgeDistance - edge;
            if (isRightSide) startHorizontal = -startHorizontal;

            return spring.RunAsync(new Vector(startHorizontal, 0), default, velocity, ToolbarSpringSpec);
        }

        // ─────────────────── 点击"打开菜单/窗口"时给动画让路 ───────────────────

        /// <summary>等待动画落定的上限（毫秒）：手感倍率调得很慢时，不能让菜单跟着等两秒。</summary>
        private const double ToolbarUiActionMaxWaitMs = 500.0;

        /// <summary>当前等待中的打开动作；再次点击时取消上一次，避免排队打开两次。</summary>
        private CancellationTokenSource _toolbarUiActionWait;

        /// <summary>
        /// 工具栏（含两侧把手、迷你栏）此刻是否还有动画在跑。
        /// </summary>
        /// <remarks>
        /// 折叠过渡、跨度补间、组件显隐让位、弹簧位移都要算进来：
        /// 这几种运动都由 UI 线程逐帧推进，任何一项没跑完，UI 线程被占住就会掉帧。
        /// </remarks>
        internal bool IsFloatingBarMotionActive =>
            _foldTransitionRunning
            || ToolbarRegistry.IsMotionActive
            || (_floatingBarSpring?.IsRunning ?? false)
            || (_leftSidePanelSpring?.IsRunning ?? false)
            || (_rightSidePanelSpring?.IsRunning ?? false)
            || (_idleMiniBarSpring?.IsRunning ?? false);

        /// <summary>
        /// 执行"点击后要打开菜单/窗口"的动作：工具栏自己在动画时不抢 UI 线程。
        /// </summary>
        /// <remarks>
        /// 打开窗口要解析整棵 XAML、创建 HWND，打开菜单要重建菜单内容再把 Popup 挂上去，
        /// 都是几十到几百毫秒的同步工作。这类工作落在动画中间时，UI 线程被占住，
        /// 动画就整段停住或连掉几帧——用户看到的"点一下按钮动画就卡住"就是它。
        ///
        /// 托盘菜单项那次的做法是"先等关闭动画播完，再做重活"，思路可以借，但实现不能照抄：
        /// 那边用嵌套消息循环等 Closed 事件，放在这里会让点击事件重入（等待期间再点一下会套第二层），
        /// 而且嵌套循环期间输入还会派发到工具栏上。这里改成不阻塞的等待——
        /// 点击事件立刻返回，动画照常逐帧推进，落定后再执行打开动作。
        /// 没有动画在跑时行为完全不变，立刻执行，不多等一帧。
        /// </remarks>
        /// <param name="label">组件 Id，仅用于轨迹日志。</param>
        /// <param name="action">真正打开菜单/窗口的动作。</param>
        internal void RunToolbarUiAction(string label, Action action)
        {
            if (action == null) return;

            if (!IsFloatingBarMotionActive)
            {
                RunToolbarUiActionNow(label, action);
                return;
            }

            // 上一次的等待还没兑现就又被点了一下：后一次点击代表用户最新意图，取消旧的，
            // 否则会先开再关（菜单类组件按当前状态取反，两次排队等于什么都没做）。
            _toolbarUiActionWait?.Cancel();
            var cts = new CancellationTokenSource();
            _toolbarUiActionWait = cts;

            ToolbarRegistry.MotionTrace($"点击延后 等动画落定 id={label}");
            _ = WaitThenRunToolbarUiActionAsync(label, action, cts);
        }

        private async Task WaitThenRunToolbarUiActionAsync(string label, Action action, CancellationTokenSource cts)
        {
            try
            {
                var wait = Stopwatch.StartNew();
                while (!cts.IsCancellationRequested
                       && wait.Elapsed.TotalMilliseconds < ToolbarUiActionMaxWaitMs
                       && IsFloatingBarMotionActive)
                {
                    await Task.Delay(16);
                }

                if (cts.IsCancellationRequested) return;

                ToolbarRegistry.MotionTrace(
                    $"点击放行 id={label} 等待={wait.Elapsed.TotalMilliseconds:F0}ms"
                    + (IsFloatingBarMotionActive ? "（已达等待上限，动画仍在跑）" : ""));

                RunToolbarUiActionNow(label, action);
            }
            catch (Exception ex)
            {
                // 这里脱离了点事件，异常不会再有人接；不接住会变成未观察任务异常
                LogHelper.WriteLogToFile($"工具栏点击动作失败（{label}）: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>执行打开动作，并把它在 UI 线程上的耗时写进轨迹日志（用于确认重活到底有多重）。</summary>
        private static void RunToolbarUiActionNow(string label, Action action)
        {
            var cost = Stopwatch.StartNew();
            action();
            ToolbarRegistry.MotionTrace($"点击完成 id={label} 处理={cost.Elapsed.TotalMilliseconds:F1}ms");
        }
    }
}
