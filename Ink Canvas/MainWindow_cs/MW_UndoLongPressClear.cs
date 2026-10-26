using Ink_Canvas.Controls;
using Ink_Canvas.Properties;
using System;
using System.Windows.Threading;

namespace Ink_Canvas
{
    /// <summary>
    /// 「长按撤销按钮清屏」功能的 partial 实现。
    /// 在撤销按钮（浮动栏 ToolbarImageButton / 白板工具栏 BoardToolbarButton）上按住约 0.8 秒
    /// 触发一次清屏（复用清屏按钮逻辑，行为一致）；快速点击仍是普通撤销。
    /// 清屏前显式提交可撤销历史，保证长按清屏后再次点击撤销即可恢复墨迹（两种模式一致）。
    /// 按住期间指针移出按钮即取消，按钮保持原有的普通按压反馈。
    /// </summary>
    public partial class MainWindow : Ink_Canvas.Helpers.PerformanceTransparentWin
    {
        /// <summary>
        /// 长按触发清屏的阈值（秒）
        /// </summary>
        private const double UndoLongPressThresholdSeconds = 0.8;

        /// <summary>
        /// 长按计时器，仅在一次按住期间运行
        /// </summary>
        private DispatcherTimer _undoLongPressTimer;

        /// <summary>
        /// 当前是否有一次有效的按住正在进行
        /// </summary>
        private bool _undoLongPressHolding;

        /// <summary>
        /// 本次按住已触发长按清屏（用于松开时吞掉普通撤销点击）
        /// </summary>
        private bool _undoLongPressClearFired;

        /// <summary>
        /// 抑制 StrokesChanged 事件路径的自动清屏历史提交（长按清屏已显式提交，见 <see cref="UndoLongPressTimer_Tick"/>）
        /// </summary>
        private bool _suppressClearHistoryCommit;

        private bool IsUndoLongPressClearEnabled => Settings.Canvas.EnableLongPressUndoClear;

        #region 按钮事件挂接

        /// <summary>
        /// 挂接浮动栏撤销按钮（ToolbarImageButton）的长按事件。
        /// 按钮重建时会传入新实例，这里先解除再订阅以防重复。
        /// </summary>
        internal void AttachUndoLongPressHandlers(ToolbarImageButton btn)
        {
            btn.ButtonMouseDown -= UndoButton_ButtonMouseDown;
            btn.ButtonMouseDown += UndoButton_ButtonMouseDown;
            btn.ButtonMouseLeave -= UndoButton_ButtonMouseLeave;
            btn.ButtonMouseLeave += UndoButton_ButtonMouseLeave;
        }

        /// <summary>
        /// 挂接白板工具栏撤销按钮（BoardToolbarButton）的长按事件。
        /// </summary>
        internal void AttachUndoLongPressHandlers(BoardToolbarButton btn)
        {
            btn.ButtonMouseDown -= UndoButton_ButtonMouseDown;
            btn.ButtonMouseDown += UndoButton_ButtonMouseDown;
            btn.ButtonMouseLeave -= UndoButton_ButtonMouseLeave;
            btn.ButtonMouseLeave += UndoButton_ButtonMouseLeave;
        }

        private void UndoButton_ButtonMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            OnUndoButtonPressed();
        }

        private void UndoButton_ButtonMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            CancelUndoLongPress();
        }

        #endregion

        #region 长按状态机

        /// <summary>
        /// 撤销按钮按下：启动长按计时（功能关闭时直接返回，零开销）。
        /// </summary>
        internal void OnUndoButtonPressed()
        {
            if (!IsUndoLongPressClearEnabled) return;

            _undoLongPressHolding = true;
            _undoLongPressClearFired = false;

            if (_undoLongPressTimer == null)
            {
                _undoLongPressTimer = new DispatcherTimer();
                _undoLongPressTimer.Tick += UndoLongPressTimer_Tick;
            }
            _undoLongPressTimer.Interval = TimeSpan.FromSeconds(UndoLongPressThresholdSeconds);
            _undoLongPressTimer.Start();
        }

        /// <summary>
        /// 撤销按钮点击（松开引发的 OnClick）前调用：
        /// 本次按住若已触发长按清屏则返回 false，调用方应吞掉这次普通撤销。
        /// </summary>
        internal bool ConsumeUndoLongPressClick()
        {
            var fired = _undoLongPressClearFired;
            CancelUndoLongPress();
            return !fired;
        }

        /// <summary>
        /// 取消本次长按（指针移出按钮或按住被中止），不撤销、不清屏，
        /// 并清除状态防止松开事件丢失时残留「已触发」标记误吞下一次点击。
        /// </summary>
        internal void CancelUndoLongPress()
        {
            if (_undoLongPressTimer != null) _undoLongPressTimer.Stop();
            _undoLongPressHolding = false;
            _undoLongPressClearFired = false;
        }

        private void UndoLongPressTimer_Tick(object sender, EventArgs e)
        {
            if (_undoLongPressTimer != null) _undoLongPressTimer.Stop();

            if (!_undoLongPressHolding) return;
            _undoLongPressHolding = false;

            // 冻结页面拦截（与普通清屏一致）；被拦截时松开仍走普通撤销（同样会被冻结拦截提示）
            if (TryBlockFrozenPageMutation("清空冻结页面内容")) return;

            _undoLongPressClearFired = true;

            // 显式提交清屏历史：保证长按清屏后可撤销恢复墨迹，不受当前工具模式影响。
            // 克隆一份，避免随后清空画布使历史引用失效。
            if (inkCanvas.Strokes.Count != 0)
                timeMachine.CommitStrokeEraseHistory(inkCanvas.Strokes.Clone());

            // 复用清屏按钮逻辑；同时抑制 StrokesChanged 事件路径的自动重复提交
            _suppressClearHistoryCommit = true;
            try
            {
                PerformCanvasClear(preserveClearHistory: true);
            }
            finally
            {
                _suppressClearHistoryCommit = false;
            }

            if (Settings.Canvas.NotifyAfterLongPressUndoClear)
                ShowNotification(CanvasStrings.Canvas_LongPressUndoClearNotification);
        }

        #endregion
    }
}
