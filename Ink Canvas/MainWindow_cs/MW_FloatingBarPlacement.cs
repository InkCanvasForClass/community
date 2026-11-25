using Ink_Canvas.Helpers;
using System;
using System.Windows;
using System.Windows.Threading;

namespace Ink_Canvas
{
    /// <summary>
    /// 浮动栏的落位场景。调用方只声明目标形态，具体的边距、折叠否决和动画节奏由状态机决定。
    /// </summary>
    internal enum FloatingBarPlacement
    {
        /// <summary>收纳：工具栏移出屏幕，两侧只露出展开把手。</summary>
        Folded,

        /// <summary>桌面展开：按工具栏位置设置居中在桌面边缘。</summary>
        DesktopExpanded,

        /// <summary>PPT 放映展开：贴放映窗口底部，使用放映档边距。</summary>
        PptExpanded,

        /// <summary>全屏（白板/黑板）展开：贴边但不进任务栏，当前与放映档共用边距。</summary>
        FullScreenExpanded
    }

    public partial class MainWindow
    {
        /// <summary>
        /// 是否应按 PPT 放映档落位。放映开始事件里 <see cref="IsInPPTPresentationMode"/> 要等
        /// UpdateSlideShowStatus 排进 UI 队列执行后才置位；这段窗口期若按桌面档计算，
        /// Core 会用陈旧的桌面拖拽坐标（pointDesktop）覆盖算好的位置，表现为
        /// “进了 PPT，工具栏却停在任务栏附近、没有居中”。用短生命周期的
        /// <see cref="_pptSlideShowBeginning"/> 覆盖这段窗口期。
        /// </summary>
        private bool IsPptPlacementScene => IsInPPTPresentationMode || _pptSlideShowBeginning;

        /// <summary>
        /// 当前应有的落位场景。折叠优先于模式判断：退出放映的窗口期内
        /// <see cref="IsInPPTPresentationMode"/> 可能还是过期值，不能拿它反推折叠。
        /// </summary>
        internal FloatingBarPlacement CurrentFloatingBarPlacement
        {
            get
            {
                if (isFloatingBarFolded) return FloatingBarPlacement.Folded;
                return IsPptPlacementScene
                    ? FloatingBarPlacement.PptExpanded
                    : FloatingBarPlacement.DesktopExpanded;
            }
        }

        /// <summary>
        /// 把动作排到 UI 线程执行；已在线程上则直接执行。
        /// 浮动栏状态机（Margin、Visibility、侧边栏把手、折叠标志的观察）只在 UI 线程上合法：
        /// PPT 联动事件可能由 COM STA / 轮询线程 raise，处理器里第一个 await 之后的代码
        /// 还会丢掉 SynchronizationContext 落到线程池。在这些线程上访问浮动栏只会抛
        /// “另一个线程拥有该对象”，异常被吞掉后标志与外观就此脱节
        /// （日志里的“重设折叠位置失败”即由此而来）。
        /// </summary>
        /// <returns>是否已执行或已排入 UI 队列；Dispatcher 正在关闭时返回 false。</returns>
        internal bool PostToUiThread(Action action, string context = null)
        {
            if (action == null) return false;

            var dispatcher = Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                return false;

            if (dispatcher.CheckAccess())
            {
                action();
                return true;
            }

            if (!string.IsNullOrEmpty(context))
            {
                LogHelper.WriteLogToFile($"[Toolbar] {context} 不在 UI 线程，已封送", LogHelper.LogType.Trace);
            }

            _ = dispatcher.BeginInvoke(DispatcherPriority.Normal, action);
            return true;
        }

        /// <summary>
        /// 按场景落位。这是所有“把工具栏摆回某处”的常规入口：
        /// 折叠态下的展开请求会被拒绝，否则展开位会覆盖折叠位，
        /// 表现为“托盘显示收纳态、工具栏却停在屏幕里”。
        /// 请求 <see cref="FloatingBarPlacement.Folded"/> 时改为重建折叠外观。
        /// </summary>
        /// <param name="placement">目标场景。</param>
        /// <param name="skipAnimation">是否跳过动画直接落位（启动初始化、拖动预览）。</param>
        internal void ShowFloatingBar(FloatingBarPlacement placement, bool skipAnimation = false)
        {
            // 落位要写 Margin、跑动画，全是 UI 线程独占的状态；后台调用先排回 UI 线程再按原参数执行。
            if (!Dispatcher.CheckAccess())
            {
                PostToUiThread(() => ShowFloatingBar(placement, skipAnimation), "浮动栏落位");
                return;
            }

            if (placement == FloatingBarPlacement.Folded)
            {
                RestoreFoldedAppearance();
                return;
            }

            // 折叠中/已折叠：正向边距会覆盖折叠位，宁可不摆
            if (isFloatingBarFolded) return;

            switch (placement)
            {
                case FloatingBarPlacement.PptExpanded:
                case FloatingBarPlacement.FullScreenExpanded:
                    ViewboxFloatingBarMarginAnimation(FloatingBarEdgeExpandedPpt, false, skipAnimation);
                    break;
                default:
                    ViewboxFloatingBarMarginAnimation(FloatingBarEdgeExpandedDesktop, true, skipAnimation);
                    break;
            }
        }

        /// <summary>
        /// 按当前状态修复/恢复落位：折叠态重设折叠位与把手并返回 true，
        /// 展开态按实时场景摆回位置并返回 false。
        /// 用它替换“先判折叠、再自己挑 60/100”的写法：那些判断只读一次标志，
        /// 而折叠往往在随后的 await 期间才完成。
        /// </summary>
        internal bool ApplyFloatingBarPlacementForCurrentMode(bool skipAnimation = false)
        {
            if (ReapplyFoldPositionIfFolded()) return true;
            ShowFloatingBar(CurrentFloatingBarPlacement, skipAnimation);
            return false;
        }

        /// <summary>
        /// 重置到默认位置（用户拖动过之后的“恢复默认”）：清掉保存的拖动坐标，再按场景居中。
        /// 折叠态下只重建折叠外观，不会把工具栏拉回屏幕。
        /// </summary>
        internal void ResetFloatingBarToDefaultPlacement(FloatingBarPlacement placement)
        {
            if (!Dispatcher.CheckAccess())
            {
                PostToUiThread(() => ResetFloatingBarToDefaultPlacement(placement), "重置浮动栏位置");
                return;
            }

            if (isFloatingBarFolded)
            {
                RestoreFoldedAppearance();
                return;
            }

            _userHasDraggedFloatingBar = false;
            pointDesktop = new Point(-1, -1);
            pointPPT = new Point(-1, -1);
            // 恢复默认位置即回到居中模式
            _floatingBarCenteredMode = true;

            if (placement == FloatingBarPlacement.PptExpanded)
                PureViewboxFloatingBarMarginAnimationInPPTMode();
            else
                PureViewboxFloatingBarMarginAnimationInDesktopMode();
        }
    }
}
