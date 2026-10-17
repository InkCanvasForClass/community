using System;
using System.Collections.Generic;
using Ink_Canvas.Models;
using Ink_Canvas.Services.Classroom;
using InkCanvasForClass.Tests.TestDoubles;
using Xunit;

namespace InkCanvasForClass.Tests
{
    /// <summary>
    /// <see cref="TimerService"/> 状态机的 characterization 测试。
    /// 全部用例以假时钟 + <c>enableRealTimer: false</c> 手动驱动 <c>TickCore</c>，
    /// 不触碰真实定时器与 UI 线程；事件经 <see cref="FakeSyncService"/> 断言同步语义。
    /// </summary>
    public class TimerServiceTests : IDisposable
    {
        // 固定基准时刻，假时钟只进不退
        private static readonly DateTime Epoch = new DateTime(2026, 9, 30, 12, 0, 0);

        private DateTime _clock;
        private bool _overtimeEnabled;
        private bool _progressiveReminderEnabled;
        private TimerService _service;

        public TimerServiceTests()
        {
            _clock = Epoch;
            _service = CreateService();
        }

        public void Dispose()
        {
            _service?.Dispose();
        }

        private TimerService CreateService()
        {
            return new TimerService(
                tickIntervalPolicy: _ => 50,
                isOvertimeCountUpEnabled: () => _overtimeEnabled,
                isProgressiveReminderEnabled: () => _progressiveReminderEnabled,
                nowProvider: () => _clock,
                enableRealTimer: false);
        }

        /// <summary>设定总时长并开始计时（以秒为单位，取整秒避免 +1s 取整分支干扰断言）。</summary>
        private void StartWithTotalSeconds(int totalSeconds)
        {
            _service.Hour = 0;
            _service.Minute = 0;
            _service.Second = totalSeconds;
            _service.Start();
        }

        private void AdvanceSeconds(int seconds)
        {
            _clock = _clock.AddSeconds(seconds);
        }

        [Fact]
        public void Start_SetsRunningState()
        {
            // Arrange（构造器已备好服务）

            // Act
            StartWithTotalSeconds(60);

            // Assert
            Assert.True(_service.IsRunning);
            Assert.False(_service.IsPaused);
            Assert.Equal(TimerMode.Running, _service.Snapshot().Mode);
        }

        [Fact]
        public void TickCore_AfterPartialElapsed_RemainingDecreases()
        {
            // Arrange：总时长 60s，整秒流逝不触发 +1s 取整
            StartWithTotalSeconds(60);
            TimerState observed = null;
            _service.StateChanged += s => observed = s;

            // Act
            AdvanceSeconds(10);
            _service.TickCore();

            // Assert
            Assert.NotNull(observed);
            Assert.Equal(TimeSpan.FromSeconds(50), observed.Remaining);
        }

        [Fact]
        public void Pause_FreezesTicksAndQueries()
        {
            // Arrange
            StartWithTotalSeconds(60);
            var eventCount = 0;
            _service.StateChanged += _ => eventCount++;
            AdvanceSeconds(10);
            _service.TickCore();

            // Act
            _service.Pause();
            AdvanceSeconds(10);
            _service.TickCore();

            // Assert：暂停期间 tick 早退，不推事件；查询按原窗口语义返回 null
            Assert.Equal(1, eventCount);
            Assert.Null(_service.GetRemainingTime());
            Assert.Null(_service.GetElapsedTime());
        }

        [Fact]
        public void Resume_CompensatesPauseDuration_ElapsedContinuesFromPausePoint()
        {
            // Arrange：跑 10s → 暂停 → 暂停中流逝 1000s（应被补偿）→ 继续 → 再跑 5s
            StartWithTotalSeconds(3600);
            AdvanceSeconds(10);
            _service.Pause();
            AdvanceSeconds(1000);

            // Act
            _service.Resume();
            AdvanceSeconds(5);

            // Assert：流逝 = 10 + 5，暂停的 1000s 不计入
            Assert.Equal(TimeSpan.FromSeconds(15), _service.GetElapsedTime());
        }

        [Fact]
        public void Completion_WhenOvertimeDisabled_JustCompletedFiresExactlyOnce()
        {
            // Arrange：总时长 3s，未启用超时正计时
            _overtimeEnabled = false;
            StartWithTotalSeconds(3);
            var states = new List<TimerState>();
            _service.StateChanged += states.Add;

            // Act：越过终点后连打两 tick——第二 tick 应因 !isRunning 早退
            AdvanceSeconds(5);
            _service.TickCore();
            _service.TickCore();

            // Assert：事件恰好一次，JustCompleted 恰好一次，状态回到 Idle
            Assert.Single(states);
            Assert.True(states[0].JustCompleted);
            Assert.False(_service.IsRunning);
            Assert.Equal(TimerMode.Idle, _service.Snapshot().Mode);
        }

        [Fact]
        public void Completion_WhenOvertimeEnabled_EntersOvertimeInsteadOfCompleting()
        {
            // Arrange：总时长 3s，启用超时正计时
            _overtimeEnabled = true;
            StartWithTotalSeconds(3);
            var states = new List<TimerState>();
            _service.StateChanged += states.Add;

            // Act：越过终点 2s 打第一 tick，再越 2s 打第二 tick
            AdvanceSeconds(5);
            _service.TickCore();
            AdvanceSeconds(2);
            _service.TickCore();

            // Assert：第一帧进超时（边沿标志恰好一次），第二帧为正计时帧、时长 4s
            Assert.Equal(2, states.Count);
            Assert.True(states[0].JustEnteredOvertime);
            Assert.False(states[0].JustCompleted);
            Assert.False(states[1].JustEnteredOvertime);
            Assert.Equal(TimerMode.Overtime, states[1].Mode);
            Assert.Equal(TimeSpan.FromSeconds(4), states[1].Overtime);
        }

        [Fact]
        public void ProgressiveReminder_WhenEnabled_FiresOncePerStart()
        {
            // Arrange：总时长 60s，启用渐进提醒；剩余 ≤6s 且 >0 触发
            _progressiveReminderEnabled = true;
            StartWithTotalSeconds(60);
            var states = new List<TimerState>();
            _service.StateChanged += states.Add;

            // Act：整秒流逝——55s 时剩余 5s（应触发），56s 时剩余 4s（不应再触发）
            AdvanceSeconds(55);
            _service.TickCore();
            AdvanceSeconds(1);
            _service.TickCore();

            // Assert
            Assert.Equal(2, states.Count);
            Assert.True(states[0].ProgressiveReminderDue);
            Assert.False(states[1].ProgressiveReminderDue);
        }

        [Fact]
        public void StateChanged_SubscriberMarshalsThroughFakeSyncService_SemanticsAssertable()
        {
            // Arrange：订阅方自行封送——事件计数即 BeginInvoke 计数，排空前回调不落地
            StartWithTotalSeconds(60);
            var fake = new FakeSyncService();
            var received = new List<TimerState>();
            _service.StateChanged += s => fake.BeginInvoke(() => received.Add(s));

            // Act
            AdvanceSeconds(10);
            _service.TickCore();
            AdvanceSeconds(10);
            _service.TickCore();

            // Assert：发起了 2 次异步调用；RunPending 前未执行，排空后按序落地
            Assert.Equal(2, fake.BeginInvokeCount);
            Assert.Equal(2, fake.PendingCount);
            Assert.Empty(received);
            Assert.Equal(2, fake.RunPending());
            Assert.Equal(2, received.Count);
            Assert.Equal(TimeSpan.FromSeconds(50), received[0].Remaining);
            Assert.Equal(TimeSpan.FromSeconds(40), received[1].Remaining);
        }

        [Fact]
        public void Reset_AfterStart_ClearsRunningAndOvertimeFlags()
        {
            // Arrange：启用超时并越过终点，使服务进入超时态
            _overtimeEnabled = true;
            StartWithTotalSeconds(3);
            AdvanceSeconds(5);
            _service.TickCore();

            // Act
            _service.Reset();

            // Assert
            Assert.False(_service.IsRunning);
            Assert.False(_service.IsPaused);
            Assert.False(_service.IsOvertimeMode);
        }
    }
}
