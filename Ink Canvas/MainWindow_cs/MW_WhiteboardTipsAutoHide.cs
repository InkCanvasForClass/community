using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Ink_Canvas
{
    /// <summary>
    /// 白板模式「在时间与鸡汤区域操作时自动隐藏」功能的 partial 实现。
    /// 白板中当触控、手写笔或鼠标操作（含悬停）落在时间/日期与鸡汤水印的显示区域上时，
    /// 水印淡出让位；恢复时机支持两种模式：
    /// 1. 延迟恢复：距最后一次区域操作超过设定延迟后自动淡入恢复；
    /// 2. 即时恢复：手指/笔抬起、松开鼠标或指针移出区域后立即淡入恢复；
    ///    若操作结束时指针仍停留在区域内（如抬笔后笔仍悬停在文字上），保持隐藏，
    ///    待其移出时恢复；延迟设置作为丢失结束事件时的兜底，保证不会一直隐藏。
    /// </summary>
    public partial class MainWindow : Ink_Canvas.Helpers.PerformanceTransparentWin
    {
        /// <summary>
        /// 水印淡入/淡出动画时长（秒）
        /// </summary>
        private const double WhiteboardTipsFadeDurationSeconds = 0.25;

        /// <summary>
        /// 恢复显示轮询间隔（毫秒）。仅在白板模式且功能启用时运行，开销可忽略。
        /// </summary>
        private const int WhiteboardTipsAutoHideCheckIntervalMs = 500;

        /// <summary>
        /// 命中区域外扩像素，便于手指/笔尖落在文字边缘附近时也能触发隐藏
        /// </summary>
        private const double WhiteboardTipsHitPadding = 10;

        /// <summary>
        /// 恢复显示轮询计时器（非空且 Running 时表示功能正在白板模式中生效）
        /// </summary>
        private DispatcherTimer _whiteboardTipsAutoHideCheckTimer;

        /// <summary>
        /// 水印当前是否因用户操作而处于隐藏状态
        /// </summary>
        private bool _whiteboardTipsOverlayHidden;

        /// <summary>
        /// 最后一次落在水印区域上的操作时间戳（TickCount64）
        /// </summary>
        private long _lastWhiteboardInputActivityTicks;

        /// <summary>
        /// 真实鼠标指针当前是否位于水印区域内（含悬停；触控/手写笔提升的鼠标事件不参与）
        /// </summary>
        private bool _whiteboardTipsAreaMouseHovering;

        /// <summary>
        /// 手写笔当前是否位于水印区域内（含接触与悬停）
        /// </summary>
        private bool _whiteboardTipsAreaStylusOver;

        /// <summary>
        /// 当前位于水印区域内的触摸设备 ID 集合
        /// </summary>
        private readonly HashSet<int> _whiteboardTipsAreaTouchIds = new HashSet<int>();

        private bool IsWhiteboardTipsAutoHideActive =>
            currentMode == 1 && Settings.Appearance.EnableWhiteboardTipsAutoHideOnInteraction;

        private bool IsWhiteboardTipsInstantRestoreEnabled =>
            Settings.Appearance.EnableWhiteboardTipsInstantRestore;

        /// <summary>
        /// 订阅窗口级 Preview 输入事件（由构造函数调用一次）。
        /// Preview 事件先于各控件处理且不受 Handled 影响，能覆盖白板内的全部
        /// 触控/手写笔/鼠标操作；其他窗口（设置窗口、计时器等）的输入不会路由
        /// 到本窗口，天然不会误触发。
        /// </summary>
        private void InitializeWhiteboardTipsAutoHide()
        {
            PreviewMouseDown += WhiteboardTipsAutoHide_PreviewMouseDown;
            PreviewMouseMove += WhiteboardTipsAutoHide_PreviewMouseMove;
            PreviewMouseUp += WhiteboardTipsAutoHide_PreviewMouseUp;
            PreviewTouchDown += WhiteboardTipsAutoHide_PreviewTouchDown;
            PreviewTouchMove += WhiteboardTipsAutoHide_PreviewTouchMove;
            PreviewTouchUp += WhiteboardTipsAutoHide_PreviewTouchUp;
            PreviewStylusDown += WhiteboardTipsAutoHide_PreviewStylusDown;
            PreviewStylusMove += WhiteboardTipsAutoHide_PreviewStylusMove;
            PreviewStylusUp += WhiteboardTipsAutoHide_PreviewStylusUp;
            PreviewStylusInRange += WhiteboardTipsAutoHide_PreviewStylusInRange;
            PreviewStylusOutOfRange += WhiteboardTipsAutoHide_PreviewStylusOutOfRange;
            MouseLeave += WhiteboardTipsAutoHide_MouseLeave;
            Deactivated += WhiteboardTipsAutoHide_WindowDeactivated;
        }

        #region 输入事件

        private void WhiteboardTipsAutoHide_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // 触控/手写笔提升而来的鼠标事件不参与鼠标状态，避免与触摸状态重复计数
            if (e.StylusDevice != null) return;

            var position = e.GetPosition(this);
            if (!IsInputWithinWhiteboardTipsArea(position)) return;
            _whiteboardTipsAreaMouseHovering = true;
            NotifyWhiteboardInputActivity();
        }

        private void WhiteboardTipsAutoHide_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.StylusDevice != null) return;

            // 悬停与按住拖拽均触发
            var position = e.GetPosition(this);
            _whiteboardTipsAreaMouseHovering = IsInputWithinWhiteboardTipsArea(position);
            if (_whiteboardTipsAreaMouseHovering) NotifyWhiteboardInputActivity();
            else TryInstantRestoreWhiteboardTips();
        }

        private void WhiteboardTipsAutoHide_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.StylusDevice != null) return;

            // 松开后若鼠标仍悬停在区域内，由悬停状态保持隐藏，待其移出时恢复
            TryInstantRestoreWhiteboardTips();
        }

        private void WhiteboardTipsAutoHide_PreviewTouchDown(object sender, TouchEventArgs e)
        {
            var position = e.GetTouchPoint(this).Position;
            if (!IsInputWithinWhiteboardTipsArea(position)) return;
            _whiteboardTipsAreaTouchIds.Add(e.TouchDevice.Id);
            NotifyWhiteboardInputActivity();
        }

        private void WhiteboardTipsAutoHide_PreviewTouchMove(object sender, TouchEventArgs e)
        {
            var position = e.GetTouchPoint(this).Position;
            if (IsInputWithinWhiteboardTipsArea(position))
            {
                _whiteboardTipsAreaTouchIds.Add(e.TouchDevice.Id);
                NotifyWhiteboardInputActivity();
            }
            else
            {
                _whiteboardTipsAreaTouchIds.Remove(e.TouchDevice.Id);
                TryInstantRestoreWhiteboardTips();
            }
        }

        private void WhiteboardTipsAutoHide_PreviewTouchUp(object sender, TouchEventArgs e)
        {
            _whiteboardTipsAreaTouchIds.Remove(e.TouchDevice.Id);
            TryInstantRestoreWhiteboardTips();
        }

        private void WhiteboardTipsAutoHide_PreviewStylusDown(object sender, StylusDownEventArgs e)
        {
            var position = e.GetPosition(this);
            if (!IsInputWithinWhiteboardTipsArea(position)) return;
            _whiteboardTipsAreaStylusOver = true;
            NotifyWhiteboardInputActivity();
        }

        private void WhiteboardTipsAutoHide_PreviewStylusMove(object sender, StylusEventArgs e)
        {
            // 笔接触与悬停移动均触发
            var position = e.GetPosition(this);
            _whiteboardTipsAreaStylusOver = IsInputWithinWhiteboardTipsArea(position);
            if (_whiteboardTipsAreaStylusOver) NotifyWhiteboardInputActivity();
            else TryInstantRestoreWhiteboardTips();
        }

        private void WhiteboardTipsAutoHide_PreviewStylusUp(object sender, StylusEventArgs e)
        {
            // 抬笔后笔可能仍悬停在区域上方，交由后续 StylusMove/OutOfRange 更新状态
            TryInstantRestoreWhiteboardTips();
        }

        private void WhiteboardTipsAutoHide_PreviewStylusInRange(object sender, StylusEventArgs e)
        {
            var position = e.GetPosition(this);
            _whiteboardTipsAreaStylusOver = IsInputWithinWhiteboardTipsArea(position);
            if (_whiteboardTipsAreaStylusOver) NotifyWhiteboardInputActivity();
        }

        private void WhiteboardTipsAutoHide_PreviewStylusOutOfRange(object sender, StylusEventArgs e)
        {
            _whiteboardTipsAreaStylusOver = false;
            TryInstantRestoreWhiteboardTips();
        }

        private void WhiteboardTipsAutoHide_MouseLeave(object sender, MouseEventArgs e)
        {
            if (e.StylusDevice != null) return;
            _whiteboardTipsAreaMouseHovering = false;
            TryInstantRestoreWhiteboardTips();
        }

        /// <summary>
        /// 窗口失活（如打开设置窗口、Alt+Tab）时重置隐藏状态。
        /// 同时兜底触摸丢失 TouchUp 等异常场景，避免水印卡在隐藏状态。
        /// </summary>
        private void WhiteboardTipsAutoHide_WindowDeactivated(object sender, EventArgs e)
        {
            RestoreWhiteboardTipsOverlay();
        }

        #endregion

        /// <summary>
        /// 判断输入点是否落在时间/鸡汤水印的显示区域内。
        /// 取容器中所有可见子元素的边界并集，并向外扩若干像素以提高容错。
        /// </summary>
        private bool IsInputWithinWhiteboardTipsArea(Point position)
        {
            if (WhiteboardTipsOverlayCanvas == null) return false;

            foreach (UIElement child in WhiteboardTipsOverlayCanvas.Children)
            {
                if (child.Visibility != Visibility.Visible) continue;
                if (!(child is FrameworkElement element)) continue;
                try
                {
                    var bounds = element.TransformToAncestor(this).TransformBounds(
                        new Rect(0, 0, element.ActualWidth, element.ActualHeight));
                    if (double.IsNaN(bounds.Width) || double.IsNaN(bounds.Height)) continue;
                    bounds.Inflate(WhiteboardTipsHitPadding, WhiteboardTipsHitPadding);
                    if (bounds.Contains(position)) return true;
                }
                catch (InvalidOperationException)
                {
                    // 元素尚未连接到可视化树时忽略
                }
            }
            return false;
        }

        /// <summary>
        /// 记录一次落在水印区域上的用户操作：刷新活动时间戳；若水印当前可见则立即淡出。
        /// </summary>
        private void NotifyWhiteboardInputActivity()
        {
            if (!IsWhiteboardTipsAutoHideActive) return;

            _lastWhiteboardInputActivityTicks = Environment.TickCount64;

            if (_whiteboardTipsOverlayHidden) return;
            _whiteboardTipsOverlayHidden = true;
            AnimateWhiteboardTipsOverlayOpacity(0);
        }

        /// <summary>
        /// 即时恢复模式：操作结束（手指/笔抬起、松开鼠标或指针移出区域）后立即淡入。
        /// 若指针仍停留在区域内（如抬笔后笔仍悬停在文字上、鼠标仍停在该处），
        /// 保持隐藏，待其移出区域时再恢复。
        /// </summary>
        private void TryInstantRestoreWhiteboardTips()
        {
            if (!IsWhiteboardTipsAutoHideActive) return;
            if (!IsWhiteboardTipsInstantRestoreEnabled) return;
            if (!_whiteboardTipsOverlayHidden) return;
            if (IsAnyPointerOverWhiteboardTipsArea()) return;

            FadeInWhiteboardTipsOverlay();
        }

        /// <summary>
        /// 是否存在仍停留在水印区域内的指针（鼠标悬停/笔悬停或接触/区域内触摸）。
        /// </summary>
        private bool IsAnyPointerOverWhiteboardTipsArea()
        {
            return _whiteboardTipsAreaMouseHovering
                   || _whiteboardTipsAreaStylusOver
                   || _whiteboardTipsAreaTouchIds.Count > 0;
        }

        /// <summary>
        /// 将时间与鸡汤水印容器动画到目标不透明度（淡入/淡出）。
        /// </summary>
        private void AnimateWhiteboardTipsOverlayOpacity(double targetOpacity)
        {
            if (WhiteboardTipsOverlayCanvas == null) return;

            WhiteboardTipsOverlayCanvas.BeginAnimation(OpacityProperty, new DoubleAnimation
            {
                To = targetOpacity,
                Duration = TimeSpan.FromSeconds(WhiteboardTipsFadeDurationSeconds),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
        }

        /// <summary>
        /// 淡入恢复水印显示（用于延迟到点与即时恢复场景）。
        /// </summary>
        private void FadeInWhiteboardTipsOverlay()
        {
            _whiteboardTipsOverlayHidden = false;
            AnimateWhiteboardTipsOverlayOpacity(1);
        }

        /// <summary>
        /// 立即恢复水印显示并清空跟踪状态（清除进行中的动画并把容器不透明度还原为 1）。
        /// 子元素各自的可见性/不透明度由既有显示逻辑管理，此处只操作容器。
        /// </summary>
        private void RestoreWhiteboardTipsOverlay()
        {
            _whiteboardTipsOverlayHidden = false;
            _whiteboardTipsAreaMouseHovering = false;
            _whiteboardTipsAreaStylusOver = false;
            _whiteboardTipsAreaTouchIds.Clear();
            if (WhiteboardTipsOverlayCanvas == null) return;
            WhiteboardTipsOverlayCanvas.BeginAnimation(OpacityProperty, null);
            WhiteboardTipsOverlayCanvas.Opacity = 1;
        }

        /// <summary>
        /// 进入白板模式时调用：还原水印显示状态并启动恢复显示轮询（若功能启用）。
        /// 注意：调用时 currentMode 可能尚未置 1（置位发生在随后的 SwitchBackground 中），
        /// 因此这里只看功能开关；轮询 Tick 会按 currentMode 自行校验并收敛。
        /// </summary>
        internal void StartWhiteboardTipsAutoHide()
        {
            RestoreWhiteboardTipsOverlay();
            _lastWhiteboardInputActivityTicks = Environment.TickCount64;
            if (!Settings.Appearance.EnableWhiteboardTipsAutoHideOnInteraction) return;

            EnsureWhiteboardTipsAutoHideCheckTimer();
            _whiteboardTipsAutoHideCheckTimer.Start();
        }

        /// <summary>
        /// 退出白板模式或关闭功能时调用：停止轮询并还原水印显示状态。
        /// </summary>
        internal void StopWhiteboardTipsAutoHide()
        {
            if (_whiteboardTipsAutoHideCheckTimer != null) _whiteboardTipsAutoHideCheckTimer.Stop();
            RestoreWhiteboardTipsOverlay();
        }

        /// <summary>
        /// 设置变更/热重载时调用：按当前模式与开关状态同步轮询与显示状态。
        /// </summary>
        internal void SyncWhiteboardTipsAutoHide()
        {
            if (IsWhiteboardTipsAutoHideActive) StartWhiteboardTipsAutoHide();
            else StopWhiteboardTipsAutoHide();
        }

        private void EnsureWhiteboardTipsAutoHideCheckTimer()
        {
            if (_whiteboardTipsAutoHideCheckTimer != null) return;
            _whiteboardTipsAutoHideCheckTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(WhiteboardTipsAutoHideCheckIntervalMs)
            };
            _whiteboardTipsAutoHideCheckTimer.Tick += WhiteboardTipsAutoHideCheckTimer_Tick;
        }

        private void WhiteboardTipsAutoHideCheckTimer_Tick(object sender, EventArgs e)
        {
            // 兜底自愈：模式/开关变化后未走显式同步路径时在此收敛
            if (!IsWhiteboardTipsAutoHideActive)
            {
                StopWhiteboardTipsAutoHide();
                return;
            }

            if (!_whiteboardTipsOverlayHidden) return;

            // 距最后一次落在区域上的操作超过设定延迟后恢复。
            // 延迟模式：正常恢复路径；即时恢复模式：丢失结束事件（如触摸被取消、
            // 指针静止悬停在区域内）时的兜底，保证水印不会一直停留在隐藏状态。
            var delaySeconds = Math.Max(1, Settings.Appearance.WhiteboardTipsAutoHideRestoreDelay);
            if (Environment.TickCount64 - _lastWhiteboardInputActivityTicks >= delaySeconds * 1000L)
            {
                // 兜底恢复时清理可能残留的触摸跟踪，避免陈旧状态持续阻塞后续即时恢复
                _whiteboardTipsAreaTouchIds.Clear();
                FadeInWhiteboardTipsOverlay();
            }
        }
    }
}
