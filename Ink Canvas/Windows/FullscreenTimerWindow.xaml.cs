using System;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Ink_Canvas.Windows
{
    /// <summary>
    /// 全屏计时器窗口
    /// </summary>
    public partial class FullscreenTimerWindow : Window
    {
        private NewStyleTimerWindow parentControl;
        private System.Timers.Timer updateTimer;

        public FullscreenTimerWindow(NewStyleTimerWindow parent)
        {
            InitializeComponent();
            parentControl = parent;

            this.Left = 0;
            this.Top = 0;
            this.Width = SystemParameters.PrimaryScreenWidth;
            this.Height = SystemParameters.PrimaryScreenHeight;

            updateTimer = new System.Timers.Timer(100);
            updateTimer.Elapsed += UpdateTimer_Elapsed;
            updateTimer.Start();

            parentControl.TimerCompleted += ParentWindow_TimerCompleted;

            var mainWindow = Application.Current.MainWindow as MainWindow;
            mainWindow?.PauseTopmostMaintenance();

            if (parentControl != null)
            {
                parentControl.Hide();
            }

            // 设置窗口置顶（WPF 属性，WindowTopmostManager 会通过 Win32 强制执行）
            Topmost = true;

            // 注册到中央置顶管理器，确保立即生效
            SourceInitialized += (s, e) => Helpers.WindowTopmostManager.RegisterWindow(this);
        }

        private void UpdateTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (parentControl != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (ShouldCloseWindow())
                    {
                        this.Close();
                        return;
                    }

                    UpdateTimeDisplay();
                });
            }
        }

        private bool ShouldCloseWindow()
        {
            if (parentControl == null) return true;

            if (MainWindow.Settings.RandSettings?.EnableOvertimeCountUp == true)
            {
                if (parentControl.IsTimerRunning)
                {
                    return false;
                }

                var remainingTime = parentControl.GetRemainingTime();
                if (remainingTime.HasValue && remainingTime.Value.TotalSeconds < 0)
                {
                    return false;
                }

                return true;
            }
            else
            {
                return !parentControl.IsTimerRunning;
            }
        }

        private void UpdateTimeDisplay()
        {
            if (parentControl == null) return;

            var remainingTime = parentControl.GetRemainingTime();
            if (remainingTime.HasValue)
            {
                var timeSpan = remainingTime.Value;
                bool isOvertimeMode = timeSpan.TotalSeconds < 0;
                bool shouldShowRed = isOvertimeMode && MainWindow.Settings.RandSettings?.EnableOvertimeRedText == true;

                int hours, minutes, seconds;

                if (isOvertimeMode)
                {
                    var totalTimeSpan = parentControl.GetTotalTimeSpan();
                    if (totalTimeSpan.HasValue)
                    {
                        var elapsedTime = parentControl.GetElapsedTime();
                        if (elapsedTime.HasValue)
                        {
                            var overtimeSpan = elapsedTime.Value - totalTimeSpan.Value;
                            hours = (int)overtimeSpan.TotalHours;
                            minutes = overtimeSpan.Minutes;
                            seconds = overtimeSpan.Seconds;
                        }
                        else
                        {
                            hours = 0;
                            minutes = 0;
                            seconds = 0;
                        }
                    }
                    else
                    {
                        hours = 0;
                        minutes = 0;
                        seconds = 0;
                    }
                }
                else
                {
                    hours = (int)timeSpan.TotalHours;
                    minutes = timeSpan.Minutes;
                    seconds = timeSpan.Seconds;
                }

                SetDigitDisplay("FullHour1Display", Math.Abs(hours / 10) % 10, shouldShowRed);
                SetDigitDisplay("FullHour2Display", (hours % 10 + 10) % 10, shouldShowRed);

                SetDigitDisplay("FullMinute1Display", minutes / 10, shouldShowRed);
                SetDigitDisplay("FullMinute2Display", minutes % 10, shouldShowRed);

                SetDigitDisplay("FullSecond1Display", seconds / 10, shouldShowRed);
                SetDigitDisplay("FullSecond2Display", seconds % 10, shouldShowRed);

                SetColonDisplay(shouldShowRed);
            }
        }

        private void ParentWindow_TimerCompleted(object sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                this.Close();
            });
        }

        private void SetDigitDisplay(string pathName, int digit, bool isRed = false)
        {
            var path = this.FindName(pathName) as Path;
            if (path != null)
            {
                string resourceKey = $"Digit{digit}";
                var geometry = this.FindResource(resourceKey) as Geometry;
                if (geometry != null)
                {
                    path.Data = geometry;
                }

                // 设置颜色
                if (isRed)
                {
                    path.Fill = Brushes.Red;
                }
                else
                {
                    path.Fill = Brushes.White;
                }
            }
        }

        /// <summary>
        /// 设置全屏窗口冒号显示颜色
        /// </summary>
        /// <param name="isRed">是否显示为红色</param>
        private void SetColonDisplay(bool isRed = false)
        {
            var colon1 = this.FindName("FullColon1Display") as TextBlock;
            var colon2 = this.FindName("FullColon2Display") as TextBlock;

            if (colon1 != null)
            {
                if (isRed)
                {
                    colon1.Foreground = Brushes.Red;
                }
                else
                {
                    colon1.Foreground = Brushes.White;
                }
            }

            if (colon2 != null)
            {
                if (isRed)
                {
                    colon2.Foreground = Brushes.Red;
                }
                else
                {
                    colon2.Foreground = Brushes.White;
                }
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 点击屏幕退出全屏
            ExitFullscreen();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // 按ESC键退出全屏
            if (e.Key == Key.Escape)
            {
                ExitFullscreen();
            }
        }

        private void ExitFullscreen()
        {
            this.Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            mainWindow?.ResumeTopmostMaintenance();

            if (parentControl != null)
            {
                parentControl.TimerCompleted -= ParentWindow_TimerCompleted;
                parentControl.Show();
                parentControl.Activate();
                parentControl.UpdateActivityTime();
            }

            // 清理资源
            if (updateTimer != null)
            {
                updateTimer.Stop();
                updateTimer.Dispose();
            }
            base.OnClosed(e);
        }
    }
}
