using Ink_Canvas.Helpers;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Point = System.Windows.Point;

namespace Ink_Canvas
{
    /// <summary>
    /// Issue #285 —— 更小批注栏。
    /// 当 <see cref="Settings.Appearance.EnableIdleMiniBar"/> 开启且处于闲置（光标/选择）状态时，
    /// 用一个可自动吸边的紧凑圆角矩形替代完整浮动工具栏，尽量减少对正常内容的遮挡。
    ///   • 默认半透明，不跟随浮动栏透明度设置。
    ///   • 页面无墨迹时按钮文字为"开始批注"，不显示清页按钮；有墨迹（或 PPT 模式）时改为"批注"并显示清页按钮。
    ///   • 提供白板、更多工具入口。
    ///   • 用户手动贴边过深时收起为仅展开按钮，一段时间后自动恢复。
    /// 桌面批注与 PPT 放映下均可工作（白板模式不显示，白板有独立工具栏）。
    /// </summary>
    public partial class MainWindow
    {
        // 拖动状态
        private bool _idleMiniBarDragging;
        private Point _idleMiniBarDragStart;
        private Thickness _idleMiniBarDragStartMargin;
        private bool _idleMiniBarDragMoved;

        // 是否已进入闲置迷你栏状态
        private bool _idleMiniBarActive;
        // 是否已深度贴边收起（仅剩展开按钮）
        private bool _idleMiniBarCollapsed;
        // 记忆的位置（窗口坐标系，相对左上角的 Margin.Left/Top）
        private Point _idleMiniBarPosition = new Point(-1, -1);
        // 深度贴边收起后自动恢复计时器
        private DispatcherTimer _idleMiniBarRestoreTimer;

        // 收起判定：屏幕内可见部分小于该值即视为"贴边过深"
        private const double IdleMiniBarDeepDockVisible = 24;
        // 收起后露出的可点击宽度（展开按钮）
        private const double IdleMiniBarCollapsedPeek = 40;
        // 距边缘的安全间隙
        private const double IdleMiniBarEdgeMargin = 8;

        /// <summary>当前是否满足显示迷你栏的条件（闲置 + 开启 + 非白板 + 未折叠）。</summary>
        private bool IsIdleMiniBarEligible()
        {
            if (!Settings.Appearance.EnableIdleMiniBar) return false;
            if (IdleMiniBar == null) return false;
            if (isFloatingBarFolded) return false;
            if (currentMode == 1) return false;              // 白板模式使用独立工具栏
            if (_currentToolMode != "cursor") return false;  // 仅闲置（光标）状态
            return true;
        }

        /// <summary>
        /// 根据当前工具模式刷新迷你栏显示/隐藏。由 <c>UpdateCurrentToolMode</c> 调用。
        /// </summary>
        internal void RefreshIdleMiniBarState()
        {
            try
            {
                if (IsIdleMiniBarEligible())
                    TransitionToIdleBar();
                else
                    TransitionFromIdleBar();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"迷你批注栏刷新失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        /// <summary>进入闲置迷你栏状态：隐藏完整浮动栏，显示并定位迷你栏。</summary>
        private void TransitionToIdleBar()
        {
            if (IdleMiniBar == null) return;

            // 隐藏完整浮动栏
            if (ViewboxFloatingBar != null)
                ViewboxFloatingBar.Visibility = Visibility.Hidden;

            _idleMiniBarActive = true;
            IdleMiniBar.Opacity = ClampIdleMiniBarOpacity(Settings.Appearance.IdleMiniBarOpacity);
            UpdateIdleMiniBarContent();
            SetIdleMiniBarCollapsed(false);
            IdleMiniBar.Visibility = Visibility.Visible;

            // 布局完成后再定位（需要 ActualWidth/Height）
            Dispatcher.BeginInvoke(new Action(() =>
            {
                IdleMiniBar.UpdateLayout();
                PositionIdleMiniBar();
            }), DispatcherPriority.Loaded);
        }

        /// <summary>退出闲置迷你栏状态：隐藏迷你栏，恢复完整浮动栏。</summary>
        private void TransitionFromIdleBar()
        {
            StopIdleMiniBarRestoreTimer();

            if (IdleMiniBar != null && IdleMiniBar.Visibility != Visibility.Collapsed)
                IdleMiniBar.Visibility = Visibility.Collapsed;

            // 若浮动栏应显示（Topmost 且未折叠），恢复其可见性
            if (_idleMiniBarActive && ViewboxFloatingBar != null && Topmost && !isFloatingBarFolded)
                ViewboxFloatingBar.Visibility = Visibility.Visible;

            _idleMiniBarActive = false;
            _idleMiniBarCollapsed = false;
        }

        /// <summary>刷新按钮文字与清页按钮可见性（基于是否有墨迹 / PPT 模式）。</summary>
        private void UpdateIdleMiniBarContent()
        {
            bool hasStrokes = inkCanvas != null && inkCanvas.Strokes.Count > 0;

            if (IdleMiniBarAnnotateText != null)
            {
                IdleMiniBarAnnotateText.Text = hasStrokes
                    ? Properties.FloatingBarStrings.IdleMiniBar_Annotate
                    : Properties.FloatingBarStrings.IdleMiniBar_StartAnnotate;
            }

            if (IdleMiniBarClearButton != null)
            {
                bool showClear = hasStrokes || IsInPPTPresentationMode;
                IdleMiniBarClearButton.Visibility = showClear ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private static double ClampIdleMiniBarOpacity(double value)
        {
            if (value < 0.1) return 0.1;
            if (value > 1.0) return 1.0;
            return value;
        }

        /// <summary>计算窗口可用尺寸（DIP）。</summary>
        private Size GetIdleMiniBarHostSize()
        {
            double w = ActualWidth > 0 ? ActualWidth : Width;
            double h = ActualHeight > 0 ? ActualHeight : Height;
            if (w <= 0) w = 1920;
            if (h <= 0) h = 1080;
            return new Size(w, h);
        }

        /// <summary>将迷你栏定位到记忆位置或默认底部居中，并夹取到屏幕内。</summary>
        private void PositionIdleMiniBar()
        {
            if (IdleMiniBar == null) return;

            var host = GetIdleMiniBarHostSize();
            double barW = IdleMiniBar.ActualWidth > 0 ? IdleMiniBar.ActualWidth : IdleMiniBar.MinWidth;
            double barH = IdleMiniBar.ActualHeight > 0 ? IdleMiniBar.ActualHeight : IdleMiniBar.Height;
            if (barW <= 0) barW = 160;
            if (barH <= 0) barH = 44;

            double left, top;
            if (_idleMiniBarPosition.X >= 0 && _idleMiniBarPosition.Y >= 0)
            {
                left = _idleMiniBarPosition.X;
                top = _idleMiniBarPosition.Y;
            }
            else
            {
                // 默认：底部居中，全屏时置底
                left = (host.Width - barW) / 2;
                top = host.Height - barH - GetIdleMiniBarBottomOffset();
            }

            left = ClampValue(left, IdleMiniBarEdgeMargin, host.Width - barW - IdleMiniBarEdgeMargin);
            top = ClampValue(top, IdleMiniBarEdgeMargin, host.Height - barH - IdleMiniBarEdgeMargin);

            IdleMiniBar.Margin = new Thickness(left, top, 0, 0);
            _idleMiniBarPosition = new Point(left, top);
        }

        /// <summary>底部默认偏移：非全屏时考虑任务栏高度。</summary>
        private double GetIdleMiniBarBottomOffset()
        {
            try
            {
                // 全屏（置底/Topmost false）时贴到最底部，否则留出任务栏空间
                if (!Topmost) return 12;
                var screen = GetFloatingBarTargetScreen();
                double dpiScaleY = 1;
                var source = PresentationSource.FromVisual(this);
                if (source != null) dpiScaleY = source.CompositionTarget.TransformToDevice.M22;
                double taskbar = ForegroundWindowInfo.GetTaskbarHeight(screen, dpiScaleY);
                return Math.Max(12, taskbar + 6);
            }
            catch
            {
                return 48;
            }
        }

        // —— 拖动 & 吸边 ——

        private void IdleMiniBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (IdleMiniBar == null) return;
            if (_idleMiniBarCollapsed) return; // 收起时仅展开按钮响应

            _idleMiniBarDragging = true;
            _idleMiniBarDragMoved = false;
            _idleMiniBarDragStart = e.GetPosition(this);
            _idleMiniBarDragStartMargin = IdleMiniBar.Margin;
            IdleMiniBar.CaptureMouse();
        }

        private void IdleMiniBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_idleMiniBarDragging || IdleMiniBar == null) return;

            var cur = e.GetPosition(this);
            double dx = cur.X - _idleMiniBarDragStart.X;
            double dy = cur.Y - _idleMiniBarDragStart.Y;
            if (Math.Abs(dx) > 3 || Math.Abs(dy) > 3) _idleMiniBarDragMoved = true;

            double left = _idleMiniBarDragStartMargin.Left + dx;
            double top = _idleMiniBarDragStartMargin.Top + dy;

            var host = GetIdleMiniBarHostSize();
            double barW = IdleMiniBar.ActualWidth > 0 ? IdleMiniBar.ActualWidth : 160;
            double barH = IdleMiniBar.ActualHeight > 0 ? IdleMiniBar.ActualHeight : 44;

            // 允许拖出屏幕一部分（用于深度贴边收起），但限制不完全飞出
            left = ClampValue(left, -barW + IdleMiniBarDeepDockVisible, host.Width - IdleMiniBarDeepDockVisible);
            top = ClampValue(top, -barH + IdleMiniBarDeepDockVisible, host.Height - IdleMiniBarDeepDockVisible);

            IdleMiniBar.Margin = new Thickness(left, top, 0, 0);
        }

        private void IdleMiniBar_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (IdleMiniBar == null) return;
            if (_idleMiniBarDragging)
            {
                _idleMiniBarDragging = false;
                IdleMiniBar.ReleaseMouseCapture();

                if (_idleMiniBarDragMoved)
                {
                    SnapIdleMiniBarToEdge();
                    return;
                }
            }
        }

        /// <summary>拖动结束后吸附到最近的屏幕边缘；若贴边过深则收起为展开按钮。</summary>
        private void SnapIdleMiniBarToEdge()
        {
            if (IdleMiniBar == null) return;

            var host = GetIdleMiniBarHostSize();
            double barW = IdleMiniBar.ActualWidth > 0 ? IdleMiniBar.ActualWidth : 160;
            double barH = IdleMiniBar.ActualHeight > 0 ? IdleMiniBar.ActualHeight : 44;

            double left = IdleMiniBar.Margin.Left;
            double top = IdleMiniBar.Margin.Top;

            // 判断是否贴边过深：任一方向露出部分不足
            double visibleLeft = left + barW;                 // 从左侧算，露出右部
            double visibleRight = host.Width - left;          // 从右侧算，露出左部
            double visibleTop = top + barH;
            double visibleBottom = host.Height - top;

            bool deepLeft = left < 0 && visibleLeft < IdleMiniBarDeepDockVisible + 4;
            bool deepRight = (left + barW) > host.Width && visibleRight < IdleMiniBarDeepDockVisible + 4;
            bool deepTop = top < 0 && visibleTop < IdleMiniBarDeepDockVisible + 4;
            bool deepBottom = (top + barH) > host.Height && visibleBottom < IdleMiniBarDeepDockVisible + 4;

            if (deepLeft || deepRight || deepTop || deepBottom)
            {
                CollapseIdleMiniBarToEdge(deepLeft, deepRight, deepTop, deepBottom);
                return;
            }

            // 常规吸边：找最近的一条边
            double distLeft = left;
            double distRight = host.Width - (left + barW);
            double distTop = top;
            double distBottom = host.Height - (top + barH);
            double min = Math.Min(Math.Min(distLeft, distRight), Math.Min(distTop, distBottom));

            if (min == distLeft) left = IdleMiniBarEdgeMargin;
            else if (min == distRight) left = host.Width - barW - IdleMiniBarEdgeMargin;
            else if (min == distTop) top = IdleMiniBarEdgeMargin;
            else top = host.Height - barH - IdleMiniBarEdgeMargin;

            left = ClampValue(left, IdleMiniBarEdgeMargin, host.Width - barW - IdleMiniBarEdgeMargin);
            top = ClampValue(top, IdleMiniBarEdgeMargin, host.Height - barH - IdleMiniBarEdgeMargin);

            AnimateIdleMiniBarTo(left, top);
            _idleMiniBarPosition = new Point(left, top);
        }

        /// <summary>收起为仅展开按钮，贴到对应边缘，并启动自动恢复计时器。</summary>
        private void CollapseIdleMiniBarToEdge(bool left, bool right, bool top, bool bottom)
        {
            var host = GetIdleMiniBarHostSize();
            SetIdleMiniBarCollapsed(true);
            IdleMiniBar.UpdateLayout();

            double barW = IdleMiniBar.ActualWidth > 0 ? IdleMiniBar.ActualWidth : IdleMiniBarCollapsedPeek;
            double barH = IdleMiniBar.ActualHeight > 0 ? IdleMiniBar.ActualHeight : 44;

            double newLeft = IdleMiniBar.Margin.Left;
            double newTop = IdleMiniBar.Margin.Top;

            if (left) newLeft = -(barW - IdleMiniBarCollapsedPeek);
            else if (right) newLeft = host.Width - IdleMiniBarCollapsedPeek;
            else newLeft = ClampValue(newLeft, 0, host.Width - barW);

            if (top) newTop = -(barH - IdleMiniBarCollapsedPeek);
            else if (bottom) newTop = host.Height - IdleMiniBarCollapsedPeek;
            else newTop = ClampValue(newTop, 0, host.Height - barH);

            // 展开按钮的箭头指向展开方向（远离贴附的边缘）
            UpdateIdleMiniBarExpandArrow(left, right, top, bottom);

            AnimateIdleMiniBarTo(newLeft, newTop);
            _idleMiniBarPosition = new Point(newLeft, newTop);
            StartIdleMiniBarRestoreTimer();
        }

        /// <summary>切换收起/展开状态下的按钮显示。</summary>
        private void SetIdleMiniBarCollapsed(bool collapsed)
        {
            _idleMiniBarCollapsed = collapsed;
            if (IdleMiniBarExpandButton != null)
                IdleMiniBarExpandButton.Visibility = collapsed ? Visibility.Visible : Visibility.Collapsed;
            if (IdleMiniBarButtons != null)
                IdleMiniBarButtons.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        }

        /// <summary>根据贴附的边缘设置展开箭头方向（指向展开方向，即远离该边缘）。</summary>
        private void UpdateIdleMiniBarExpandArrow(bool left, bool right, bool top, bool bottom)
        {
            if (IdleMiniBarExpandIcon == null) return;
            if (left) IdleMiniBarExpandIcon.Icon = iNKORE.UI.WPF.Modern.Common.IconKeys.SegoeFluentIcons.ChevronRight;
            else if (right) IdleMiniBarExpandIcon.Icon = iNKORE.UI.WPF.Modern.Common.IconKeys.SegoeFluentIcons.ChevronLeft;
            else if (top) IdleMiniBarExpandIcon.Icon = iNKORE.UI.WPF.Modern.Common.IconKeys.SegoeFluentIcons.ChevronDown;
            else if (bottom) IdleMiniBarExpandIcon.Icon = iNKORE.UI.WPF.Modern.Common.IconKeys.SegoeFluentIcons.ChevronUp;
        }

        private void AnimateIdleMiniBarTo(double left, double top)
        {
            if (IdleMiniBar == null) return;
            if (Settings.Appearance.DisableToolbarAnimation)
            {
                IdleMiniBar.BeginAnimation(MarginProperty, null);
                IdleMiniBar.Margin = new Thickness(left, top, 0, 0);
                return;
            }

            var anim = new ThicknessAnimation
            {
                To = new Thickness(left, top, 0, 0),
                Duration = TimeSpan.FromMilliseconds(180),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            anim.Completed += (_, _) =>
            {
                IdleMiniBar.BeginAnimation(MarginProperty, null);
                IdleMiniBar.Margin = new Thickness(left, top, 0, 0);
            };
            IdleMiniBar.BeginAnimation(MarginProperty, anim);
        }

        private void StartIdleMiniBarRestoreTimer()
        {
            StopIdleMiniBarRestoreTimer();
            double seconds = Settings.Appearance.IdleMiniBarAutoRestoreSeconds;
            if (seconds <= 0) return;

            _idleMiniBarRestoreTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(seconds)
            };
            _idleMiniBarRestoreTimer.Tick += (_, _) =>
            {
                StopIdleMiniBarRestoreTimer();
                RestoreIdleMiniBarFromCollapse();
            };
            _idleMiniBarRestoreTimer.Start();
        }

        private void StopIdleMiniBarRestoreTimer()
        {
            _idleMiniBarRestoreTimer?.Stop();
            _idleMiniBarRestoreTimer = null;
        }

        /// <summary>从收起状态恢复为完整迷你栏并重新贴边定位。</summary>
        private void RestoreIdleMiniBarFromCollapse()
        {
            if (IdleMiniBar == null || !_idleMiniBarActive) return;
            SetIdleMiniBarCollapsed(false);
            IdleMiniBar.UpdateLayout();
            _idleMiniBarPosition = new Point(-1, -1); // 触发默认吸边定位
            PositionIdleMiniBar();
        }

        // —— 悬停时临时提升不透明度，便于操作 ——

        private void IdleMiniBar_MouseEnter(object sender, MouseEventArgs e)
        {
            if (IdleMiniBar != null && _idleMiniBarActive)
                IdleMiniBar.Opacity = 1.0;
        }

        private void IdleMiniBar_MouseLeave(object sender, MouseEventArgs e)
        {
            if (IdleMiniBar != null && _idleMiniBarActive && !_idleMiniBarDragging)
                IdleMiniBar.Opacity = ClampIdleMiniBarOpacity(Settings.Appearance.IdleMiniBarOpacity);
        }

        // —— 按钮事件 ——

        private void IdleMiniBarExpand_Click(object sender, RoutedEventArgs e)
        {
            RestoreIdleMiniBarFromCollapse();
        }

        private void IdleMiniBarAnnotate_Click(object sender, RoutedEventArgs e)
        {
            // 切换到画笔即离开闲置状态，迷你栏会自动隐藏
            PenIcon_Click(sender, new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left));
        }

        private void IdleMiniBarClear_Click(object sender, RoutedEventArgs e)
        {
            BtnClear_Click(sender, e);
            UpdateIdleMiniBarContent();
        }

        private void IdleMiniBarWhiteboard_Click(object sender, RoutedEventArgs e)
        {
            ImageBlackboard_MouseUp(sender, new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left));
        }

        private void IdleMiniBarMore_Click(object sender, RoutedEventArgs e)
        {
            // 工具弹窗默认锚定在完整浮动栏的"更多"按钮上（此时该栏已隐藏且移出屏幕），
            // 需临时改为锚定迷你栏的按钮，弹窗才会跟随迷你栏显示；关闭后还原。
            RetargetToolsPopupToIdleMiniBar();
            SymbolIconTools_MouseUp(IdleMiniBarMoreButton, new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left));
        }

        /// <summary>把"更多工具"弹窗临时锚定到迷你栏按钮，弹窗关闭后自动还原到浮动栏按钮。</summary>
        private void RetargetToolsPopupToIdleMiniBar()
        {
            if (BorderTools == null || IdleMiniBarMoreButton == null) return;

            var original = BorderTools.PlacementTarget;
            BorderTools.PlacementTarget = IdleMiniBarMoreButton;

            void OnClosed(object s, EventArgs args)
            {
                BorderTools.Closed -= OnClosed;
                // 仅当仍指向迷你栏按钮时才还原，避免覆盖其他路径的重设
                if (ReferenceEquals(BorderTools.PlacementTarget, IdleMiniBarMoreButton))
                    BorderTools.PlacementTarget = original ?? ToolsFloatingBarBtn;
            }

            BorderTools.Closed += OnClosed;
        }

        // —— 设置变更响应 ——

        internal void ApplyIdleMiniBarEnabled(bool enabled)
        {
            if (!enabled)
            {
                TransitionFromIdleBar();
            }
            RefreshIdleMiniBarState();
        }

        internal void ApplyIdleMiniBarOpacity(double opacity)
        {
            if (IdleMiniBar != null && _idleMiniBarActive)
                IdleMiniBar.Opacity = ClampIdleMiniBarOpacity(opacity);
        }

        private static double ClampValue(double value, double min, double max)
        {
            if (max < min) return min;
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
