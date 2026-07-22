using System;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Ink_Canvas.Windows
{
    /// <summary>
    /// 最小化计时器独立窗口
    /// </summary>
    public partial class NewStyleMinimizedTimerWindow : Window
    {
        private readonly System.Timers.Timer updateTimer;
        private readonly Func<TimeSpan?> _getRemainingTime;
        private readonly Func<bool> _shouldHide;
        private readonly Action _restoreCallback;
        private readonly Action _stopTimerCallback;

        private double _clickCheckLeft;
        private double _clickCheckTop;
        private readonly DispatcherTimer _clickCheckTimer;

        public NewStyleMinimizedTimerWindow(
            Func<TimeSpan?> remainingTime,
            Func<bool> shouldHide,
            Action restoreCallback,
            Action stopTimerCallback)
        {
            InitializeComponent();
            _getRemainingTime = remainingTime;
            _shouldHide = shouldHide;
            _restoreCallback = restoreCallback;
            _stopTimerCallback = stopTimerCallback;

            updateTimer = new System.Timers.Timer(100);
            updateTimer.Elapsed += UpdateTimer_Elapsed;
            updateTimer.Start();

            _clickCheckTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _clickCheckTimer.Tick += ClickCheckTimer_Tick;

            Closed += (s, e) =>
            {
                // 解除订阅，避免窗口反复开关时委托累积
                updateTimer.Elapsed -= UpdateTimer_Elapsed;
                updateTimer.Stop();
                updateTimer.Dispose();
                _clickCheckTimer.Tick -= ClickCheckTimer_Tick;
                _clickCheckTimer.Stop();
            };
        }

        private void UpdateTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (!IsVisible) return;
                if (_shouldHide != null && _shouldHide())
                {
                    Close();
                    return;
                }
                UpdateTimeDisplay();
            });
        }

        private void UpdateTimeDisplay()
        {
            var remaining = _getRemainingTime?.Invoke();
            if (!remaining.HasValue) return;

            var ts = remaining.Value;
            bool isOvertime = ts.TotalSeconds < 0;
            bool shouldShowRed = isOvertime && MainWindow.Settings?.RandSettings?.EnableOvertimeRedText == true;

            int hours, minutes, seconds;
            if (isOvertime)
            {
                hours = Math.Abs((int)ts.TotalHours);
                minutes = Math.Abs(ts.Minutes);
                seconds = Math.Abs(ts.Seconds);
            }
            else
            {
                hours = (int)ts.TotalHours;
                minutes = ts.Minutes;
                seconds = ts.Seconds;
            }

            var fill = shouldShowRed ? Brushes.Red : (Brush)FindResource("NewTimerWindowDigitForeground");

            SetDigit("MinHour1Display", hours / 10, fill);
            SetDigit("MinHour2Display", hours % 10, fill);
            SetDigit("MinMinute1Display", minutes / 10, fill);
            SetDigit("MinMinute2Display", minutes % 10, fill);
            SetDigit("MinSecond1Display", seconds / 10, fill);
            SetDigit("MinSecond2Display", seconds % 10, fill);

            var colon1 = FindName("MinColon1Display") as TextBlock;
            var colon2 = FindName("MinColon2Display") as TextBlock;
            if (colon1 != null) colon1.Foreground = fill;
            if (colon2 != null) colon2.Foreground = fill;
        }

        private void SetDigit(string name, int digit, Brush fill)
        {
            var path = FindName(name) as System.Windows.Shapes.Path;
            if (path == null) return;
            path.Data = FindResource($"Digit{digit}") as Geometry;
            path.Fill = fill;
        }

        // === 拖动/点击逻辑 ===

        private void MainBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 记录当前位置，200ms 后检查是否移动了
            _clickCheckLeft = Left;
            _clickCheckTop = Top;
            _clickCheckTimer.Start();

            try
            {
                DragMove();
            }
            catch
            {
            }
        }

        private void ClickCheckTimer_Tick(object sender, EventArgs e)
        {
            _clickCheckTimer.Stop();

            // 如果窗口位置没变，说明是点击而不是拖动
            if (Math.Abs(Left - _clickCheckLeft) < 2 && Math.Abs(Top - _clickCheckTop) < 2)
            {
                _restoreCallback?.Invoke();
                Close();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            _stopTimerCallback?.Invoke();
            Close();
        }
    }
}
