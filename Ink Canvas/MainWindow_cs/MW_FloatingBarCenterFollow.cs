using Ink_Canvas.Controls.Toolbar.FloatingToolbar;
using Ink_Canvas.Helpers;
using System;
using System.Windows;
using System.Windows.Media;

namespace Ink_Canvas
{
    /// <summary>
    /// 浮动工具栏的"逐帧居中跟随"：胶囊跨度变化（含弹簧过冲）的整个过程里，
    /// 让整条工具栏在居中轴上不动，收尾时再原子提交成新的居中位置。
    /// </summary>
    /// <remarks>
    /// 为什么不直接算好 Margin 再动画：
    /// Margin 是布局属性，跨度每变一次就要重排整条栏；而同一时刻弹簧位移
    /// （<see cref="FloatingBarSpring"/> 独占 ViewboxFloatingBar 的 RenderTransform）也在改视觉位置。
    /// 早先的版本正是在逐帧回调里直接写 Margin、收尾再落位一次，"变宽"与"居中"两个来源
    /// 同时拽同一条栏，观感是被拽到中间再跳一下，整段被回退（2f71a285）。这里改成三步：
    ///   1. 补偿只写 GridFloatingBarContainer 的 TranslateTransform —— 不触发布局，只动合成；
    ///   2. 拖不到的部分才等到收尾，把补偿量一次性加进 Margin，同时把 TranslateTransform 归零，
    ///      两次写在同一帧里完成，视觉上没有任何跳变；
    ///   3. Margin 依然只由落位代码在"大动作"时改（换边、显示、拖动松手），这里只补那半个偏心量。
    /// 用 Grid 而不是 Viewbox 承接补偿：Viewbox 的 RenderTransform 被弹簧占用，
    /// 而 GridFloatingBarContainer 的 RenderTransform 全仓没有别的写入方（顺带也不会反馈进布局）。
    /// </remarks>
    public partial class MainWindow
    {
        /// <summary>补偿量小于这个值就不写（亚像素，看不出来，省掉一次失效）。</summary>
        private const double FloatingBarCenterFollowEpsilon = 0.01;

        /// <summary>判定"这条工具栏本来就在居中轴上居中"的容差（DIP）。</summary>
        /// <remarks>
        /// 用户可以把工具栏拖到任意位置。拖动过之后它多半不在屏幕上居中，
        /// 此时"变宽时保持中心不动"没有意义（它的中心本来就不是屏幕中心），
        /// 所以先算一次它当前的中心，离屏幕中心超过这个容差就整轮不介入。
        /// </remarks>
        private const double FloatingBarCenterFollowCenterTolerance = 24.0;

        /// <summary>补偿用的 TranslateTransform，独占挂在 GridFloatingBarContainer 上。</summary>
        private TranslateTransform _floatingBarCenterFollowTransform;

        /// <summary>当前 Margin 对应的"已对齐跨度"（局部坐标，未乘缩放）。NaN 表示还没量过。</summary>
        private double _floatingBarCenterFollowAlignedExtent = double.NaN;

        /// <summary>上一次认可的 Margin 分量（横排取 Left、竖排取 Top），用来识别外部落位。</summary>
        private double _floatingBarCenterFollowAlignedMargin = double.NaN;

        /// <summary>是否已经挂上逐帧回调。事件是静态的，重复挂会重复补偿。</summary>
        private bool _floatingBarCenterFollowHooked;

        /// <summary>重入守卫：这里面会强制一次布局，布局回调有可能绕回落位代码。</summary>
        private bool _floatingBarCenterFollowUpdating;

        /// <summary>逐帧强制布局的成本采样：定位"展开/收起掉帧"时用。</summary>
        private double _floatingBarCenterFollowLayoutWorstMs;
        private int _floatingBarCenterFollowLayoutSlowLogs;

        /// <summary>"量子树"与"实排"跨度不一致时最多记几行，避免刷日志。</summary>
        private int _floatingBarCenterFollowExtentMismatchLogs;

        /// <summary>当前真正写在补偿 Transform 上的值（局部 DIP），收尾提交时用来核对视觉连续性。</summary>
        private double _floatingBarCenterFollowAppliedShift;

        /// <summary>逐帧跳变日志配额：单帧动超过 3px 就记一行。</summary>
        private int _floatingBarCenterFollowJumpLogs;

        /// <summary>提交日志配额。</summary>
        private int _floatingBarCenterFollowCommitLogs;

        /// <summary>外部落位日志配额。</summary>
        private int _floatingBarCenterFollowResetLogs;

        /// <summary>挂上逐帧跟随（幂等）。工具栏装配完成后调用。</summary>
        internal void HookFloatingBarCenterFollow()
        {
            if (_floatingBarCenterFollowHooked) return;
            _floatingBarCenterFollowHooked = true;
            ToolbarRegistry.ExtentFrame += OnFloatingBarExtentFrame;
            ToolbarRegistry.ExtentSettled += OnFloatingBarExtentSettled;
        }

        internal void UnhookFloatingBarCenterFollow()
        {
            if (!_floatingBarCenterFollowHooked) return;
            _floatingBarCenterFollowHooked = false;
            ToolbarRegistry.ExtentFrame -= OnFloatingBarExtentFrame;
            ToolbarRegistry.ExtentSettled -= OnFloatingBarExtentSettled;
            ResetFloatingBarCenterFollow();
        }

        private void OnFloatingBarExtentFrame()
        {
            // 逐帧只写 RenderTransform。这里绝不能写 Margin ——
            // 那正是 2f71a285 回退掉的"三方同时改位置"。
            UpdateFloatingBarCenterFollow(commit: false);
        }

        private void OnFloatingBarExtentSettled()
        {
            // 还有别的分段在跑就先不动，等最后一段收尾时一起提交
            if (ToolbarRegistry.IsExtentAnimating) return;
            UpdateFloatingBarCenterFollow(commit: true);

            if (_floatingBarCenterFollowLayoutWorstMs > 0)
            {
                ToolbarRegistry.MotionTrace($"居中跟随逐帧统计 最差强制布局={_floatingBarCenterFollowLayoutWorstMs:F1}ms");
                _floatingBarCenterFollowLayoutWorstMs = 0;
                _floatingBarCenterFollowLayoutSlowLogs = 0;
            }
        }

        /// <summary>
        /// 不强制整窗布局，只量工具栏容器子树，得到整条栏当前的跨度。
        /// </summary>
        /// <remarks>
        /// 分段跨度是逐帧写的布局属性；每帧 Viewbox.UpdateLayout() 等于每帧让整窗
        /// （含墨迹画布）重排一次，展开/收起那半秒的掉帧主要花在这里。
        /// Viewbox 是 Stretch=None，容器排多大它就多大，所以容器 DesiredSize 就是栏跨度。
        /// </remarks>
        private double MeasureFloatingBarExtent(bool vertical)
        {
            if (GridFloatingBarContainer == null) return 0;
            try
            {
                GridFloatingBarContainer.Measure(
                    new Size(double.PositiveInfinity, double.PositiveInfinity));
                return vertical
                    ? GridFloatingBarContainer.DesiredSize.Height
                    : GridFloatingBarContainer.DesiredSize.Width;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 落位代码要改写 Margin 之前调用：把还挂着的补偿先原子提交掉。
        /// </summary>
        /// <remarks>
        /// MoveFloatingBarTo 用 "currentMargin + 弹簧偏移" 当视觉起点，
        /// 若补偿还挂在 RenderTransform 上没提交，这个起点就会少算一段，弹簧一起步就跳。
        /// </remarks>
        private void FlushFloatingBarCenterFollow()
        {
            // 不受 IsExtentAnimating 限制：落位是大动作，位置以它为准
            UpdateFloatingBarCenterFollow(commit: true);
        }

        /// <summary>居中跟随的主体：按当前跨度算偏心量并补偿（或收尾时提交）。</summary>
        private void UpdateFloatingBarCenterFollow(bool commit)
        {
            if (_floatingBarCenterFollowUpdating) return;
            _floatingBarCenterFollowUpdating = true;
            try
            {
                if (!TryGetFloatingBarCenterFollowMetrics(
                        out var vertical, out var scale, out var extent, out var marginNow,
                        ensureLayout: commit))
                {
                    // 折叠、隐藏、未加载：位置交给落位代码，这里只把补偿撤掉（不提交）
                    ResetFloatingBarCenterFollow();
                    return;
                }

                // 外部代码改了落位（换边、显示、拖动松手）：以它为准，基准重新起算
                if (!double.IsNaN(_floatingBarCenterFollowAlignedMargin) &&
                    Math.Abs(marginNow - _floatingBarCenterFollowAlignedMargin) > 0.5)
                {
                    if (_floatingBarCenterFollowResetLogs < 24)
                    {
                        _floatingBarCenterFollowResetLogs++;
                        ToolbarRegistry.MotionTrace(
                            $"居中跟随 外部落位 基准margin={_floatingBarCenterFollowAlignedMargin:F1}→{marginNow:F1} 补偿归零（原={_floatingBarCenterFollowAppliedShift:F1}）跨度={extent:F1}");
                    }
                    SetFloatingBarCenterFollowShift(0);
                    _floatingBarCenterFollowAlignedExtent = extent;
                    _floatingBarCenterFollowAlignedMargin = marginNow;
                }

                if (double.IsNaN(_floatingBarCenterFollowAlignedExtent) ||
                    double.IsNaN(_floatingBarCenterFollowAlignedMargin))
                {
                    _floatingBarCenterFollowAlignedExtent = extent;
                    _floatingBarCenterFollowAlignedMargin = marginNow;
                }

                if (!IsFloatingBarCenteredOnFollowAxis(
                        vertical, scale, marginNow, _floatingBarCenterFollowAlignedExtent))
                {
                    // 不在居中位置（用户拖走了、或正贴边）：不介入，基准跟着当前跨度走
                    if (_floatingBarCenterFollowResetLogs < 24)
                    {
                        _floatingBarCenterFollowResetLogs++;
                        ToolbarRegistry.MotionTrace(
                            $"居中跟随 不介入 margin={marginNow:F1} 基准跨度={_floatingBarCenterFollowAlignedExtent:F1} 当前跨度={extent:F1} 补偿归零（原={_floatingBarCenterFollowAppliedShift:F1}）");
                    }
                    SetFloatingBarCenterFollowShift(0);
                    _floatingBarCenterFollowAlignedExtent = extent;
                    return;
                }

                // 跨度相对"当前 Margin 对应的跨度"增长了多少，中心就偏了多少的一半
                double shift = -(extent - _floatingBarCenterFollowAlignedExtent) / 2.0;

                if (!commit)
                {
                    double applied = Math.Abs(shift) < FloatingBarCenterFollowEpsilon ? 0 : shift;

                    // 正常收缩一帧只该动零点几像素；单帧 >3px 说明跨度表被某一帧改了
                    // （退场元素让出布局、分段折掉……），这一行就是"居中位置被拽过去"的凭据。
                    if (Math.Abs(applied - _floatingBarCenterFollowAppliedShift) > 3.0 &&
                        _floatingBarCenterFollowJumpLogs < 60)
                    {
                        _floatingBarCenterFollowJumpLogs++;
                        ToolbarRegistry.MotionTrace(
                            $"居中跟随 单帧跳变 跨度={extent:F1}（基准={_floatingBarCenterFollowAlignedExtent:F1}）补偿={_floatingBarCenterFollowAppliedShift:F1}→{applied:F1} 单帧差={applied - _floatingBarCenterFollowAppliedShift:F1}");
                    }

                    SetFloatingBarCenterFollowShift(applied);
                    return;
                }

                // 收尾：补偿量一次性加进 Margin，再把 RenderTransform 归零。
                // 顺序不能反：RenderTransform 当场生效，Margin 是布局属性、要等下一次布局
                // 才落到 Arrange 上。先归零再等布局就会多画一帧"未补偿"的位置——而收尾时
                // 补偿量正好是整段跨度量的一半（718→326 时是 196px），屏幕上是整条栏横着闪一下。
                // 所以写完 Margin 立刻强制一次布局，让两件事在同一帧里一起生效。
                if (Math.Abs(shift) < FloatingBarCenterFollowEpsilon)
                {
                    SetFloatingBarCenterFollowShift(0);
                    _floatingBarCenterFollowAlignedExtent = extent;
                    return;
                }

                var margin = ViewboxFloatingBar.Margin;
                double newMargin = marginNow + shift * scale;
                double appliedShift = _floatingBarCenterFollowAppliedShift;
                var target = vertical
                    ? new Thickness(margin.Left, newMargin, margin.Right, margin.Bottom)
                    : new Thickness(newMargin, margin.Top, margin.Right, margin.Bottom);

                ViewboxFloatingBar.BeginAnimation(FrameworkElement.MarginProperty, null);
                ViewboxFloatingBar.Margin = target;
                ViewboxFloatingBar.UpdateLayout();
                SetFloatingBarCenterFollowShift(0);

                if (_floatingBarCenterFollowCommitLogs < 80)
                {
                    _floatingBarCenterFollowCommitLogs++;
                    ToolbarRegistry.MotionTrace(
                        $"居中跟随 提交 跨度={extent:F1} 补偿={appliedShift:F1}→0 margin={marginNow:F1}→{newMargin:F1} 缩放={scale:F2}");
                }

                _floatingBarCenterFollowAlignedExtent = extent;
                _floatingBarCenterFollowAlignedMargin = newMargin;

                // 跨度已稳定：让落位代码下次量到的是这个终值
                RefreshFloatingBarSizeCache(true);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"居中跟随失败: {ex.Message}", LogHelper.LogType.Warning);
                ResetFloatingBarCenterFollow();
            }
            finally
            {
                _floatingBarCenterFollowUpdating = false;
            }
        }

        /// <summary>取当前这帧要用的量：朝向、缩放、跨度、Margin 分量。返回 false 表示不该介入。</summary>
        private bool TryGetFloatingBarCenterFollowMetrics(
            out bool vertical, out double scale, out double extent, out double marginNow,
            bool ensureLayout = false)
        {
            vertical = IsVerticalToolbar;
            scale = vertical ? GetFloatingBarScaleY() : GetFloatingBarScaleX();
            extent = 0;
            marginNow = 0;

            if (!IsLoaded) return false;
            if (ViewboxFloatingBar == null || GridFloatingBarContainer == null) return false;
            if (ViewboxFloatingBar.Visibility != Visibility.Visible) return false;
            if (!ViewboxFloatingBar.IsVisible) return false;
            // 折叠（迷你条）时整条栏的落位由另一套逻辑管，别插手
            if (isFloatingBarFolded) return false;
            if (!(scale > 0) || double.IsNaN(scale) || double.IsInfinity(scale)) return false;

            if (ensureLayout)
            {
                // 收尾这一帧要拿到"真正的"跨度：这里付一次整窗布局，位置才落得准。
                var layoutWatch = System.Diagnostics.Stopwatch.StartNew();
                ViewboxFloatingBar.UpdateLayout();
                double layoutMs = layoutWatch.Elapsed.TotalMilliseconds;
                if (layoutMs > _floatingBarCenterFollowLayoutWorstMs)
                {
                    _floatingBarCenterFollowLayoutWorstMs = layoutMs;
                }
                if (layoutMs > 6.0 && _floatingBarCenterFollowLayoutSlowLogs < 12)
                {
                    _floatingBarCenterFollowLayoutSlowLogs++;
                    ToolbarRegistry.MotionTrace($"居中跟随收尾布局 {layoutMs:F1}ms");
                }

                extent = vertical ? ViewboxFloatingBar.ActualHeight : ViewboxFloatingBar.ActualWidth;
            }
            else
            {
                // 逐帧不强制整窗布局：直接量工具栏容器这一棵子树。写进分段的跨度
                // 在 Measure 里就能反映出来，没必要让整窗（含墨迹画布）跟着重排。
                extent = MeasureFloatingBarExtent(vertical);

                // 量出来和实排不一致时记一行：这条日志就是"能不能信任子树量"的凭据
                double actualExtent = vertical ? ViewboxFloatingBar.ActualHeight : ViewboxFloatingBar.ActualWidth;
                if (Math.Abs(actualExtent - extent) > 8.0 &&
                    _floatingBarCenterFollowExtentMismatchLogs < 8)
                {
                    _floatingBarCenterFollowExtentMismatchLogs++;
                    ToolbarRegistry.MotionTrace($"居中跟随跨度量不符 子树={extent:F1} 实排={actualExtent:F1}");
                }
            }
            // 退场中的独立方形仍然画着，只是排布被让出去了：按不透明度折算回来，
            // 跨度才是眼睛看到的那个宽度——收起第一帧就不会少掉半个方形（见 ToolbarRegistry.GetExitingVisualExtent）
            extent += ToolbarRegistry.GetExitingVisualExtent(vertical);
        extent -= ToolbarRegistry.GetEnteringVisualExtent(vertical);

            if (extent <= 0.5 || double.IsNaN(extent) || double.IsInfinity(extent)) return false;

            var margin = ViewboxFloatingBar.Margin;
            marginNow = vertical ? margin.Top : margin.Left;
            if (double.IsNaN(marginNow) || double.IsInfinity(marginNow)) return false;

            return true;
        }

        /// <summary>这条工具栏当前是否就停在居中轴上（按"Margin 对应的跨度"算，避开动画中间态）。</summary>
        private bool IsFloatingBarCenteredOnFollowAxis(
            bool vertical, double scale, double marginNow, double alignedExtent)
        {
            double offset = vertical ? FloatingBarSpring.CurrentOffset.Y : FloatingBarSpring.CurrentOffset.X;
            double barCenter = marginNow + offset + alignedExtent * scale / 2.0;
            if (double.IsNaN(barCenter) || double.IsInfinity(barCenter)) return false;

            bool useWorkingArea = Settings.Advanced.IsEnableAvoidFullScreenHelper;
            double screenExtent = vertical
                ? GetFloatingBarScreenHeight(useWorkingArea)
                : GetFloatingBarScreenWidth(useWorkingArea);
            if (!(screenExtent > 0) || double.IsNaN(screenExtent) || double.IsInfinity(screenExtent)) return false;

            return Math.Abs(barCenter - screenExtent / 2.0) <= FloatingBarCenterFollowCenterTolerance;
        }

        private void SetFloatingBarCenterFollowShift(double shift)
        {
            var host = GridFloatingBarContainer;
            if (host == null) return;

            if (_floatingBarCenterFollowTransform == null)
                _floatingBarCenterFollowTransform = new TranslateTransform();

            if (!ReferenceEquals(host.RenderTransform, _floatingBarCenterFollowTransform))
            {
                // 别人把 RenderTransform 换掉了：按位置算回 0，再重新挂上自己的
                host.RenderTransform = _floatingBarCenterFollowTransform;
            }

            var transform = _floatingBarCenterFollowTransform;
            if (IsVerticalToolbar)
            {
                if (Math.Abs(transform.X) > FloatingBarCenterFollowEpsilon) transform.X = 0;
                if (Math.Abs(transform.Y - shift) > FloatingBarCenterFollowEpsilon) transform.Y = shift;
            }
            else
            {
                if (Math.Abs(transform.X - shift) > FloatingBarCenterFollowEpsilon) transform.X = shift;
                if (Math.Abs(transform.Y) > FloatingBarCenterFollowEpsilon) transform.Y = 0;
            }

            _floatingBarCenterFollowAppliedShift = shift;
        }

        /// <summary>撤掉补偿并丢掉基准（不提交）。</summary>
        private void ResetFloatingBarCenterFollow()
        {
            if (_floatingBarCenterFollowTransform != null)
            {
                if (Math.Abs(_floatingBarCenterFollowTransform.X) > FloatingBarCenterFollowEpsilon)
                    _floatingBarCenterFollowTransform.X = 0;
                if (Math.Abs(_floatingBarCenterFollowTransform.Y) > FloatingBarCenterFollowEpsilon)
                    _floatingBarCenterFollowTransform.Y = 0;
            }

            _floatingBarCenterFollowAppliedShift = 0;
            _floatingBarCenterFollowAlignedExtent = double.NaN;
            _floatingBarCenterFollowAlignedMargin = double.NaN;
        }

        /// <summary>纵向缩放。仓库里只有 GetFloatingBarScaleX，竖排工具栏要用到 Y。</summary>
        private double GetFloatingBarScaleY()
        {
            var scale = ViewboxFloatingBarScaleTransform?.ScaleY ?? 1;
            return scale > 0 && !double.IsNaN(scale) && !double.IsInfinity(scale) ? scale : 1;
        }
    }
}
