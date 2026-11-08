using Ink_Canvas.Helpers;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas
{
    public partial class MainWindow
    {
        #region 浮动工具栏 UI 重叠检测与自动适配

        /// <summary>
        /// 浮动工具栏自适应适配的结果模式。
        /// </summary>
        private enum FloatingBarFitMode
        {
            /// <summary>单行且未额外缩放（按用户设置正常显示）。</summary>
            Normal,
            /// <summary>单行，但因空间不足被自动缩小。</summary>
            AutoScaled,
            /// <summary>两行布局，未额外缩小。</summary>
            TwoRow,
            /// <summary>两行布局，且被自动缩小。</summary>
            TwoRowAutoScaled,
            /// <summary>两行 + DPI 允许的最小缩放仍放不下（提示用户减少工具）。</summary>
            Overflow
        }

        /// <summary>单行布局的基准高度/宽度（拖动柄 58 逻辑像素）。</summary>
        private const double FloatingBarBaseRowExtent = 58.0;

        /// <summary>触控/可点的最小物理像素尺寸，用于按 DPI 推导最小缩放。</summary>
        private const double FloatingBarMinUsablePhysicalPx = 42.0;

        /// <summary>自动缩放的绝对下限，防止极端 DPI 下缩到不可见。</summary>
        private const double FloatingBarAbsoluteMinScale = 0.35;

        /// <summary>浮动栏距屏幕边缘预留的逻辑像素（两侧）。</summary>
        private const double FloatingBarFitScreenMargin = 16.0;

        /// <summary>用户缩放滑块允许的范围（与设置页/加载逻辑一致）。</summary>
        private const double FloatingBarUserScaleMin = 0.5;
        private const double FloatingBarUserScaleMax = 1.25;

        /// <summary>自动适配附加缩放系数（叠加在用户设置与紧凑模式之上）。</summary>
        private double _autoFitScaleFactor = 1.0;

        /// <summary>当前是否处于两行布局。</summary>
        private bool _isFloatingBarTwoRow;

        /// <summary>防止 EvaluateFloatingBarFit 重入。</summary>
        private bool _floatingBarFitEvaluating;

        private FloatingBarFitMode _floatingBarFitMode = FloatingBarFitMode.Normal;

        /// <summary>本次会话内是否已提示过"已切换为两行"，避免刷屏。</summary>
        private bool _hasNotifiedTwoRowThisSession;

        /// <summary>上次提示"减少工具数量"的时间，用于冷却。</summary>
        private DateTime _lastReduceToolsPromptTime = DateTime.MinValue;

        /// <summary>
        /// 读取当前窗口的 DPI 缩放（TransformToDevice），失败时回退为 1。
        /// </summary>
        private void GetFloatingBarDpiScale(out double dpiScaleX, out double dpiScaleY)
        {
            dpiScaleX = 1.0;
            dpiScaleY = 1.0;
            try
            {
                var source = PresentationSource.FromVisual(this);
                if (source?.CompositionTarget != null)
                {
                    dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
                    dpiScaleY = source.CompositionTarget.TransformToDevice.M22;
                }
            }
            catch { /* 窗口尚未呈现时忽略，使用 1.0 */ }

            if (dpiScaleX <= 0 || double.IsNaN(dpiScaleX) || double.IsInfinity(dpiScaleX)) dpiScaleX = 1.0;
            if (dpiScaleY <= 0 || double.IsNaN(dpiScaleY) || double.IsInfinity(dpiScaleY)) dpiScaleY = 1.0;
        }

        /// <summary>
        /// 根据 DPI 计算浮动工具栏允许的最小缩放：保证 58 逻辑像素的行高在缩放后
        /// 仍不小于 <see cref="FloatingBarMinUsablePhysicalPx"/> 物理像素（可点/可触），
        /// 并不低于绝对下限 <see cref="FloatingBarAbsoluteMinScale"/>。
        /// DPI 越高，允许缩得越小（物理尺寸不变）。
        /// </summary>
        internal double GetDpiAwareMinFloatingBarScale()
        {
            GetFloatingBarDpiScale(out var dpiScaleX, out var dpiScaleY);
            double dpi = IsVerticalToolbar ? dpiScaleY : dpiScaleX;
            double min = FloatingBarMinUsablePhysicalPx / (FloatingBarBaseRowExtent * dpi);
            return Math.Max(FloatingBarAbsoluteMinScale, Math.Min(1.0, min));
        }

        /// <summary>
        /// 获取浮动栏目标屏幕的工作区可用尺寸（逻辑像素），四周预留 <see cref="FloatingBarFitScreenMargin"/>。
        /// 使用工作区而非全屏 Bounds，避免与任务栏重叠。
        /// </summary>
        private Size GetFloatingBarAvailableLogicalSize()
        {
            GetFloatingBarDpiScale(out var dpiScaleX, out var dpiScaleY);
            System.Windows.Forms.Screen screen;
            try
            {
                screen = GetFloatingBarTargetScreen() ?? System.Windows.Forms.Screen.PrimaryScreen;
            }
            catch
            {
                screen = System.Windows.Forms.Screen.PrimaryScreen;
            }

            var area = screen.WorkingArea;
            return new Size(
                Math.Max(0, area.Width / dpiScaleX - FloatingBarFitScreenMargin * 2),
                Math.Max(0, area.Height / dpiScaleY - FloatingBarFitScreenMargin * 2));
        }

        /// <summary>
        /// 在当前换行约束下测量浮动栏根面板的自然尺寸（无需可见，直接 Measure）。
        /// </summary>
        private Size MeasureFloatingBarNaturalSize()
        {
            var panel = StackPanelFloatingBarRoot;
            if (panel == null) return new Size(0, 0);

            panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var size = panel.DesiredSize;
            if (size.Width <= 0 || double.IsNaN(size.Width)) size.Width = 284;
            if (size.Height <= 0 || double.IsNaN(size.Height)) size.Height = FloatingBarBaseRowExtent;
            return size;
        }

        /// <summary>
        /// 切换两行布局：开启时通过 MaxWidth（水平工具栏）或 MaxHeight（垂直工具栏）
        /// 约束根 WrapPanel 触发换行；关闭时恢复 UpdateToolbarDimensions 的单行约束。
        /// </summary>
        private void SetFloatingBarWrap(bool enable, double wrapExtent)
        {
            var panel = StackPanelFloatingBarRoot;
            if (panel == null) return;

            bool vertical = IsVerticalToolbar;
            if (enable)
            {
                double extent = Math.Max(FloatingBarBaseRowExtent * 2, wrapExtent);
                if (vertical)
                {
                    panel.ClearValue(MaxWidthProperty);
                    panel.MaxHeight = extent;
                }
                else
                {
                    panel.ClearValue(MaxHeightProperty);
                    panel.MaxWidth = extent;
                }
            }
            else
            {
                // 恢复单行约束（MaxHeight=58 / MaxWidth=58）
                UpdateToolbarDimensions(vertical ? Orientation.Vertical : Orientation.Horizontal);
            }
        }

        /// <summary>
        /// 用户设置缩放（与设置页相同的 0.5~1.25 范围限制）。
        /// </summary>
        private double GetUserFloatingBarScale()
        {
            double userScale = Settings.Appearance.ViewboxFloatingBarScaleTransformValue;
            if (userScale <= 0 || double.IsNaN(userScale) || double.IsInfinity(userScale)) userScale = 1.0;
            return Math.Max(FloatingBarUserScaleMin, Math.Min(FloatingBarUserScaleMax, userScale));
        }

        /// <summary>
        /// 用户缩放 × 紧凑模式系数，即未做屏幕适配前的基准缩放。
        /// </summary>
        private double GetBaseFloatingBarScale()
        {
            double baseScale = GetUserFloatingBarScale();
            if (Settings.Appearance.CompactFloatingBar)
                baseScale *= CompactFloatingBarScaleFactor;
            return baseScale;
        }

        /// <summary>
        /// 提前做 UI 重叠检测：在显示前/状态变化后测量浮动工具栏所需尺寸，
        /// 与目标屏幕工作区比较，按以下级联策略自动调整，避免超出屏幕产生重叠/裁切：
        /// 1) 单行 + 用户缩放能放下 → 不处理；
        /// 2) 单行放不下 → 自动缩小（不低于 DPI 推导的最小缩放）；
        /// 3) 缩到最小仍放不下 → 切换为两行布局，必要时再缩放；
        /// 4) 两行 + 最小缩放仍放不下 → 保持两行最小缩放，并提示用户减少工具数量。
        /// 调用时机：工具栏初始化/重建、工具可见性变化、紧凑模式切换、
        /// 用户缩放变化、DPI 变化、工具栏位置变化。
        /// </summary>
        internal void EvaluateFloatingBarFit()
        {
            if (_floatingBarFitEvaluating) return;
            if (StackPanelFloatingBarRoot == null || ViewboxFloatingBarScaleTransform == null) return;
            if (!IsLoaded) return;

            _floatingBarFitEvaluating = true;
            try
            {
                bool vertical = IsVerticalToolbar;
                var available = GetFloatingBarAvailableLogicalSize();
                double availMain = vertical ? available.Height : available.Width;
                if (availMain <= 0) return;

                double baseScale = GetBaseFloatingBarScale();
                double minScale = GetDpiAwareMinFloatingBarScale();

                // 1) 单行检测
                SetFloatingBarWrap(false, 0);
                var single = MeasureFloatingBarNaturalSize();
                double singleMain = vertical ? single.Height : single.Width;
                double singleCross = vertical ? single.Width : single.Height;
                double fitScale = singleMain > 0 ? availMain / singleMain : 1.0;

                if (baseScale <= fitScale)
                {
                    ApplyFloatingBarFit(baseScale, false, FloatingBarFitMode.Normal);
                    return;
                }

                // 2) 仅缩放即可放下（不低于 DPI 最小缩放）
                if (fitScale >= minScale)
                {
                    ApplyFloatingBarFit(fitScale, false, FloatingBarFitMode.AutoScaled);
                    return;
                }

                // 3) 两行布局：换行宽度按"最小缩放后必须放得下"设置
                double wrapExtent = availMain / minScale;
                SetFloatingBarWrap(true, wrapExtent);
                var twoRow = MeasureFloatingBarNaturalSize();
                double twoRowMain = vertical ? twoRow.Width : twoRow.Height;
                double twoRowCross = vertical ? twoRow.Height : twoRow.Width;
                int rows = singleCross > 0 ? Math.Max(1, (int)Math.Round(twoRowCross / singleCross)) : 1;
                double twoFitScale = twoRowMain > 0 ? availMain / twoRowMain : 1.0;

                if (rows <= 2 && twoFitScale >= minScale)
                {
                    double target = Math.Min(baseScale, twoFitScale);
                    ApplyFloatingBarFit(target, true,
                        target < baseScale - 0.001 ? FloatingBarFitMode.TwoRowAutoScaled : FloatingBarFitMode.TwoRow);
                    return;
                }

                // 4) 两行也放不下：保持两行 + 最小可用缩放，提示用户减少工具数量
                double overflowScale = Math.Max(minScale, Math.Min(baseScale, twoFitScale));
                ApplyFloatingBarFit(overflowScale, true, FloatingBarFitMode.Overflow);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"EvaluateFloatingBarFit 异常: {ex.Message}", LogHelper.LogType.Warning);
            }
            finally
            {
                _floatingBarFitEvaluating = false;
            }
        }

        /// <summary>
        /// 应用适配结果：更新缩放与两行状态，必要时重新居中浮动栏，并按模式发出提示。
        /// </summary>
        private void ApplyFloatingBarFit(double effectiveScale, bool twoRow, FloatingBarFitMode mode)
        {
            var prevMode = _floatingBarFitMode;

            double baseScale = GetBaseFloatingBarScale();
            _autoFitScaleFactor = baseScale > 0 ? effectiveScale / baseScale : 1.0;
            _isFloatingBarTwoRow = twoRow;
            _floatingBarFitMode = mode;

            bool scaleChanged = Math.Abs(ViewboxFloatingBarScaleTransform.ScaleX - effectiveScale) > 0.001;
            if (scaleChanged)
            {
                _userHasDraggedFloatingBar = false;
                pointDesktop = new Point(-1, -1);
                pointPPT = new Point(-1, -1);
                ViewboxFloatingBarScaleTransform.ScaleX = effectiveScale;
                ViewboxFloatingBarScaleTransform.ScaleY = effectiveScale;
                if (IsInPPTPresentationMode)
                    ViewboxFloatingBarMarginAnimation(60, skipAnimation: true);
                else
                    ViewboxFloatingBarMarginAnimation(100, true, skipAnimation: true);
            }
            else if (twoRow != (prevMode == FloatingBarFitMode.TwoRow ||
                                prevMode == FloatingBarFitMode.TwoRowAutoScaled ||
                                prevMode == FloatingBarFitMode.Overflow))
            {
                // 仅布局切换（缩放行高变化）时也重新定位一次
                if (IsInPPTPresentationMode)
                    ViewboxFloatingBarMarginAnimation(60, skipAnimation: true);
                else
                    ViewboxFloatingBarMarginAnimation(100, true, skipAnimation: true);
            }

            if (mode == FloatingBarFitMode.Overflow)
            {
                NotifyReduceFloatingBarTools();
            }
            else if ((mode == FloatingBarFitMode.TwoRow || mode == FloatingBarFitMode.TwoRowAutoScaled) &&
                     prevMode != FloatingBarFitMode.TwoRow &&
                     prevMode != FloatingBarFitMode.TwoRowAutoScaled &&
                     prevMode != FloatingBarFitMode.Overflow)
            {
                NotifyFloatingBarTwoRowActivated();
            }
        }

        /// <summary>
        /// 提示用户浮动栏已自动切换为两行（每次会话只提示一次）。
        /// </summary>
        private void NotifyFloatingBarTwoRowActivated()
        {
            if (_hasNotifiedTwoRowThisSession) return;
            _hasNotifiedTwoRowThisSession = true;
            ShowNewMessage("屏幕空间不足，浮动工具栏已自动切换为两行显示；如需恢复单行，可在设置中减少浮动工具栏上的工具数量。");
        }

        /// <summary>
        /// 提示用户浮动栏工具过多（带 10 分钟冷却，避免重复打扰）。
        /// </summary>
        private void NotifyReduceFloatingBarTools()
        {
            if ((DateTime.Now - _lastReduceToolsPromptTime).TotalMinutes < 10) return;
            _lastReduceToolsPromptTime = DateTime.Now;
            ShowNewMessage("浮动工具栏上的工具过多，已缩放到当前 DPI 允许的最小比例仍无法完整显示，建议在设置中减少浮动工具栏上的工具数量。");
        }

        #endregion
    }
}
