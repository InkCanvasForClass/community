using Ink_Canvas.Models;
using System;
using System.Timers;

namespace Ink_Canvas.Services.Classroom
{
    /// <summary>
    /// 课堂倒计时状态机（寄生提取）。承载 CountdownTimerWindow / NewStyleTimerWindow
    /// 共有的纯计时逻辑：设定时间、开始/暂停/继续/重置、tick 递减、超时正计时模式。
    /// 纪律：禁止引用 UI 调度器 / 主窗体 / 任何 Visual 类型；设置项经 Func 注入；
    /// 定时器保持 System.Timers.Timer 原类型（回调在线程池），UI 触碰由订阅端自行调度。
    /// </summary>
    internal sealed class TimerService : IDisposable
    {
        private readonly Timer timer;
        private readonly Func<TimeSpan, double> _tickIntervalPolicy;
        private readonly Func<bool> _isOvertimeCountUpEnabled;
        private readonly Func<bool> _isProgressiveReminderEnabled;
        private readonly Func<DateTime> _now;
        private readonly bool _enableRealTimer;

        /// <summary>
        /// 状态变更事件。在定时器回调线程（线程池）上引发，订阅方需自行封送至 UI 线程。
        /// </summary>
        public event Action<TimerState> StateChanged;

        /// <param name="tickIntervalPolicy">根据设定总时长决定 tick 间隔（毫秒）的策略。</param>
        /// <param name="isOvertimeCountUpEnabled">是否启用超时正计时。</param>
        /// <param name="isProgressiveReminderEnabled">是否启用结束前渐进提醒。</param>
        /// <param name="nowProvider">时钟源，默认 DateTime.Now；测试可注入假时钟。</param>
        /// <param name="enableRealTimer">false 时真实定时器不启停，仅供测试以假时钟手动驱动 TickCore。</param>
        public TimerService(
            Func<TimeSpan, double> tickIntervalPolicy,
            Func<bool> isOvertimeCountUpEnabled,
            Func<bool> isProgressiveReminderEnabled,
            Func<DateTime> nowProvider = null,
            bool enableRealTimer = true)
        {
            _tickIntervalPolicy = tickIntervalPolicy ?? throw new ArgumentNullException(nameof(tickIntervalPolicy));
            _isOvertimeCountUpEnabled = isOvertimeCountUpEnabled ?? throw new ArgumentNullException(nameof(isOvertimeCountUpEnabled));
            _isProgressiveReminderEnabled = isProgressiveReminderEnabled ?? throw new ArgumentNullException(nameof(isProgressiveReminderEnabled));
            _now = nowProvider ?? (() => DateTime.Now);
            _enableRealTimer = enableRealTimer;

            timer = new Timer();
            timer.Elapsed += OnTimerElapsed;
            timer.Interval = 50;
        }

        /// <summary>
        /// 旧版倒计时窗口（CountdownTimerWindow）的 tick 间隔策略：按总时长自适应。
        /// </summary>
        public static double LegacyIntervalForTotal(TimeSpan total)
        {
            if (total.TotalSeconds <= 10) return 20;
            if (total.TotalSeconds <= 60) return 30;
            if (total.TotalSeconds <= 120) return 50;
            return 100;
        }

        // ---- 设定时间与状态字段（原样搬自两个计时器窗口）----

        private int hour = 0;
        private int minute = 0;
        private int second = 0;
        private int cachedStartHour = 0;
        private int cachedStartMinute = 0;
        private int cachedStartSecond = 0;

        private DateTime startTime = DateTime.Now;
        private DateTime pauseTime = DateTime.Now;

        private bool isTimerRunning = false;
        private bool isPaused = false;
        private bool isOvertimeMode = false;
        private bool hasPlayedProgressiveReminder = false;

        // 无调用方的历史遗留成员，随状态机原样搬入
        private TimeSpan remainingTime = TimeSpan.Zero;

        public int Hour { get => hour; set => hour = value; }
        public int Minute { get => minute; set => minute = value; }
        public int Second { get => second; set => second = value; }

        public bool IsRunning => isTimerRunning;
        public bool IsPaused => isPaused;
        public bool IsOvertimeMode => isOvertimeMode;

        /// <summary>当前计时的起始时刻（暂停补偿后）。供窗口推算预计结束时刻。</summary>
        public DateTime StartTime => startTime;

        // ---- 状态机操作 ----

        /// <summary>
        /// 从空闲状态开始计时：缓存起始设定时间、复位标志、按策略设定 tick 间隔并启动定时器。
        /// </summary>
        public void Start()
        {
            cachedStartHour = hour;
            cachedStartMinute = minute;
            cachedStartSecond = second;

            startTime = _now();
            isPaused = false;
            isTimerRunning = true;
            isOvertimeMode = false;
            hasPlayedProgressiveReminder = false;

            timer.Interval = _tickIntervalPolicy(GetTotalTimeSpan());
            StartTimer();
        }

        /// <summary>暂停计时。</summary>
        public void Pause()
        {
            pauseTime = _now();
            isPaused = true;
            StopTimer();
        }

        /// <summary>继续计时：把暂停时长补偿进起始时刻。</summary>
        public void Resume()
        {
            startTime += _now() - pauseTime;
            isPaused = false;
            StartTimer();
        }

        /// <summary>
        /// 重置：停止定时器并复位运行/暂停/超时/提醒标志，保留当前设定时间。
        /// </summary>
        public void Reset()
        {
            if (isTimerRunning)
            {
                StopTimer();
            }
            isTimerRunning = false;
            isPaused = false;
            isOvertimeMode = false;
            hasPlayedProgressiveReminder = false;
        }

        /// <summary>
        /// 运行中重新开始：起始时刻重置为现在并立即触发一次 tick（旧版窗口重置按钮第三分支）。
        /// </summary>
        public void Restart()
        {
            startTime = _now();
            TickCore();
        }

        /// <summary>仅停止：定时器停止、运行标志复位（新版窗口 StopTimer 语义）。</summary>
        public void Stop()
        {
            StopTimer();
            isTimerRunning = false;
        }

        /// <summary>仅复位运行标志（旧版窗口 Closing 语义，定时器下一次 tick 自停）。</summary>
        public void MarkStopped()
        {
            isTimerRunning = false;
        }

        /// <summary>
        /// 复位到起始设定时间快照（新版窗口 ApplyResetTimerState 的状态部分）。
        /// </summary>
        public void ResetToStartSnapshot()
        {
            if (isTimerRunning)
            {
                StopTimer();
                isTimerRunning = false;
                isPaused = false;
            }

            hour = cachedStartHour;
            minute = cachedStartMinute;
            second = cachedStartSecond;

            isOvertimeMode = false;
            hasPlayedProgressiveReminder = false;
        }

        /// <summary>复位渐进提醒标志（新版窗口 ApplyResetStateAfterStop 的状态部分）。</summary>
        public void ResetProgressiveReminderFlag()
        {
            hasPlayedProgressiveReminder = false;
        }

        // ---- 查询（原 NewStyleTimerWindow 公开语义：暂停时返回 null，不做负值钳制）----

        public TimeSpan GetTotalTimeSpan()
        {
            return new TimeSpan(hour, minute, second);
        }

        public TimeSpan? GetElapsedTime()
        {
            if (isPaused) return null;

            return _now() - startTime;
        }

        public TimeSpan? GetRemainingTime()
        {
            if (isPaused) return null;

            var elapsed = _now() - startTime;
            var totalTimeSpan = new TimeSpan(hour, minute, second);
            var leftTimeSpan = totalTimeSpan - elapsed;

            if (leftTimeSpan.Milliseconds > 0) leftTimeSpan += new TimeSpan(0, 0, 1);

            return leftTimeSpan;
        }

        /// <summary>
        /// 当前状态的渲染快照（供主题切换等场景复现 tick 渲染分支）：
        /// 运行中按当前时钟、暂停中按暂停时刻推算；剩余/超时值做非负钳制，不含边沿标志。
        /// </summary>
        public TimerState Snapshot()
        {
            DateTime referenceTime = isPaused ? pauseTime : _now();
            TimeSpan elapsed = referenceTime - startTime;
            TimeSpan total = GetTotalTimeSpan();
            double spentPercent = elapsed.TotalMilliseconds / total.TotalMilliseconds;

            if (!isOvertimeMode)
            {
                TimeSpan left = total - elapsed;
                if (left.Milliseconds > 0) left += new TimeSpan(0, 0, 1);
                if (left < TimeSpan.Zero) left = TimeSpan.Zero;

                return new TimerState(CurrentMode(), left, TimeSpan.Zero, elapsed, total, spentPercent,
                    isTimerRunning, isPaused, isOvertimeMode, false, false, false);
            }
            else
            {
                TimeSpan overtime = elapsed - total;
                if (overtime < TimeSpan.Zero) overtime = TimeSpan.Zero;

                return new TimerState(CurrentMode(), TimeSpan.Zero, overtime, elapsed, total, spentPercent,
                    isTimerRunning, isPaused, isOvertimeMode, false, false, false);
            }
        }

        // ---- 历史遗留（无调用方，原样搬入，不改一行）----

        // 更新剩余时间
        private void UpdateRemainingTime()
        {
            if (isTimerRunning && !isPaused)
            {
                // 获取当前剩余时间
                TimeSpan? currentRemaining = GetRemainingTime();
                if (currentRemaining.HasValue)
                {
                    // 计算已经过去的时间
                    TimeSpan elapsedTime = _now() - startTime;

                    // 计算新的总时间
                    TimeSpan newTotalTime = new TimeSpan(hour, minute, second);

                    // 如果新设置的时间小于已经过去的时间，则设置为0
                    if (newTotalTime <= elapsedTime)
                    {
                        remainingTime = TimeSpan.Zero;
                    }
                    else
                    {
                        // 否则，剩余时间 = 新总时间 - 已经过去的时间
                        remainingTime = newTotalTime - elapsedTime;
                    }
                }
                else
                {
                    // 如果没有剩余时间信息，直接设置新的剩余时间
                    remainingTime = new TimeSpan(hour, minute, second);
                }
            }
        }

        // 更新特定时间单位的剩余时间
        private void UpdateSpecificTimeUnit(int newHour, int newMinute, int newSecond)
        {
            if (isTimerRunning && !isPaused)
            {
                // 获取当前剩余时间
                TimeSpan? currentRemaining = GetRemainingTime();
                if (currentRemaining.HasValue)
                {
                    // 计算已经过去的时间
                    TimeSpan elapsedTime = _now() - startTime;

                    // 计算新的总时间
                    TimeSpan newTotalTime = new TimeSpan(newHour, newMinute, newSecond);

                    // 如果新设置的时间小于已经过去的时间，则设置为0
                    if (newTotalTime <= elapsedTime)
                    {
                        remainingTime = TimeSpan.Zero;
                    }
                    else
                    {
                        // 否则，剩余时间 = 新总时间 - 已经过去的时间
                        remainingTime = newTotalTime - elapsedTime;
                    }
                }
                else
                {
                    // 如果没有剩余时间信息，直接设置新的剩余时间
                    remainingTime = new TimeSpan(newHour, newMinute, newSecond);
                }
            }
        }

        // ---- tick 核心 ----

        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            TickCore();
        }

        /// <summary>
        /// tick 核心逻辑。internal 以便等价性验证与后续单元测试在假时钟下直接驱动。
        /// </summary>
        internal void TickCore()
        {
            if (!isTimerRunning || isPaused)
            {
                StopTimer();
                return;
            }

            DateTime now = _now();
            TimeSpan elapsed = now - startTime;
            TimeSpan total = GetTotalTimeSpan();
            double spentPercent = elapsed.TotalMilliseconds / total.TotalMilliseconds;

            if (!isOvertimeMode)
            {
                TimeSpan left = total - elapsed;
                if (left.Milliseconds > 0) left += new TimeSpan(0, 0, 1);

                // 渐进提醒：剩余 ≤6s 且 >0，每次启动仅触发一次（仅新版窗口启用）
                bool progressiveDue = left.TotalSeconds <= 6 && left.TotalSeconds > 0 &&
                    _isProgressiveReminderEnabled() && !hasPlayedProgressiveReminder;
                if (progressiveDue) hasPlayedProgressiveReminder = true;

                // 结束判定：旧版窗口谓词 spentTimePercent >= 1 与新版窗口谓词 left.TotalSeconds <= 0
                // 经推导均恰好等价于「未取整剩余时间 <= 0」，此处统一，行为不变。
                bool finished = (total - elapsed) <= TimeSpan.Zero;

                bool justCompleted = false;
                bool justEnteredOvertime = false;
                if (finished)
                {
                    if (_isOvertimeCountUpEnabled())
                    {
                        isOvertimeMode = true;
                        justEnteredOvertime = true;
                    }
                    else
                    {
                        StopTimer();
                        isTimerRunning = false;
                        isPaused = false;
                        justCompleted = true;
                    }
                }

                RaiseStateChanged(new TimerState(CurrentMode(), left, TimeSpan.Zero, elapsed, total, spentPercent,
                    isTimerRunning, isPaused, isOvertimeMode, justCompleted, justEnteredOvertime, progressiveDue));
            }
            else
            {
                TimeSpan overtime = elapsed - total;

                RaiseStateChanged(new TimerState(CurrentMode(), total - elapsed, overtime, elapsed, total, spentPercent,
                    isTimerRunning, isPaused, isOvertimeMode, false, false, false));
            }
        }

        private TimerMode CurrentMode()
        {
            if (isOvertimeMode) return TimerMode.Overtime;
            if (isTimerRunning) return isPaused ? TimerMode.Paused : TimerMode.Running;
            return TimerMode.Idle;
        }

        private void RaiseStateChanged(TimerState state)
        {
            StateChanged?.Invoke(state);
        }

        private void StartTimer()
        {
            if (_enableRealTimer) timer.Start();
        }

        private void StopTimer()
        {
            if (_enableRealTimer) timer.Stop();
        }

        public void Dispose()
        {
            timer.Stop();
            timer.Elapsed -= OnTimerElapsed;
            timer.Dispose();
        }
    }
}
