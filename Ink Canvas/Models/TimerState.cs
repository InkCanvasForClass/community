using System;

namespace Ink_Canvas.Models
{
    /// <summary>
    /// 计时器模式（M14）。描述倒计时状态机的当前阶段。
    /// </summary>
    internal enum TimerMode
    {
        /// <summary>未启动或已结束。</summary>
        Idle,
        /// <summary>倒计时进行中。</summary>
        Running,
        /// <summary>已暂停。</summary>
        Paused,
        /// <summary>超时正计时（ overtime count-up ）模式。</summary>
        Overtime
    }

    /// <summary>
    /// 计时器状态快照（M14，POCO）。由 <see cref="Services.Classroom.TimerService"/> 在每次 tick
    /// 或状态变更时产生，经 StateChanged 事件推送给窗口壳；窗口只读渲染，不回写。
    /// </summary>
    internal sealed class TimerState
    {
        public TimerState(
            TimerMode mode,
            TimeSpan remaining,
            TimeSpan overtime,
            TimeSpan elapsed,
            TimeSpan total,
            double spentPercent,
            bool isRunning,
            bool isPaused,
            bool isOvertimeMode,
            bool justCompleted,
            bool justEnteredOvertime,
            bool progressiveReminderDue)
        {
            Mode = mode;
            Remaining = remaining;
            Overtime = overtime;
            Elapsed = elapsed;
            Total = total;
            SpentPercent = spentPercent;
            IsRunning = isRunning;
            IsPaused = isPaused;
            IsOvertimeMode = isOvertimeMode;
            JustCompleted = justCompleted;
            JustEnteredOvertime = justEnteredOvertime;
            ProgressiveReminderDue = progressiveReminderDue;
        }

        /// <summary>当前模式。</summary>
        public TimerMode Mode { get; }

        /// <summary>剩余时间（已按原窗口规则做 +1s 取整；超时模式下可能为负）。</summary>
        public TimeSpan Remaining { get; }

        /// <summary>超时正计时时长（仅 Overtime 模式有效）。</summary>
        public TimeSpan Overtime { get; }

        /// <summary>已流逝时间（暂停补偿后）。</summary>
        public TimeSpan Elapsed { get; }

        /// <summary>设定总时长。</summary>
        public TimeSpan Total { get; }

        /// <summary>已流逝比例（Elapsed / Total）。</summary>
        public double SpentPercent { get; }

        public bool IsRunning { get; }
        public bool IsPaused { get; }
        public bool IsOvertimeMode { get; }

        /// <summary>本帧刚自然结束（未启用超时正计时）。一次性边沿标志。</summary>
        public bool JustCompleted { get; }

        /// <summary>本帧刚进入超时正计时模式。一次性边沿标志。</summary>
        public bool JustEnteredOvertime { get; }

        /// <summary>本帧触发渐进提醒（剩余 ≤6s 且 &gt;0，每次启动仅一次）。一次性边沿标志。</summary>
        public bool ProgressiveReminderDue { get; }
    }
}
