using Ink_Canvas.Controls.Toolbar.FloatingToolbar;
using Ink_Canvas.Helpers.Motion;
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
            // 居中跟随的补偿还挂在 RenderTransform 上：先原子提交进 Margin，
            // 下面读到的 currentMargin 才是视觉上的真实位置，弹簧起点不会跳
            FlushFloatingBarCenterFollow();

            var spring = FloatingBarSpring;

            if (!animate || !IsFloatingBarMotionEnabled)
            {
                spring.SnapToLayout();
                ViewboxFloatingBar.BeginAnimation(MarginProperty, null);
                ViewboxFloatingBar.Margin = targetMargin;
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
    }
}
