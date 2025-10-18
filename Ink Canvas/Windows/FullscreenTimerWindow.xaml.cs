using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Timers;

namespace Ink_Canvas
{
    /// <summary>
    /// 全屏计时器窗口
    /// </summary>
    public partial class FullscreenTimerWindow : Window
    {
        private NewStyleTimerWindow parentWindow;
        private System.Timers.Timer updateTimer;

        public FullscreenTimerWindow(NewStyleTimerWindow parent)
        {
            InitializeComponent();
            parentWindow = parent;
            
            // 设置窗口位置和大小
            this.Left = 0;
            this.Top = 0;
            this.Width = SystemParameters.PrimaryScreenWidth;
            this.Height = SystemParameters.PrimaryScreenHeight;
            
            // 启动更新定时器
            updateTimer = new System.Timers.Timer(100); 
            updateTimer.Elapsed += UpdateTimer_Elapsed;
            updateTimer.Start();
            
            parentWindow.TimerCompleted += ParentWindow_TimerCompleted;
        }

        private void UpdateTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (parentWindow != null)
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
            if (parentWindow == null) return true;
            
            if (MainWindow.Settings.RandSettings?.EnableOvertimeCountUp == true)
            {
                if (parentWindow.IsTimerRunning)
                {
                    return false;
                }
                
                var remainingTime = parentWindow.GetRemainingTime();
                if (remainingTime.HasValue && remainingTime.Value.TotalSeconds < 0)
                {
                    return false;
                }
                
                return true;
            }
            else
            {
                return !parentWindow.IsTimerRunning;
            }
        }

        private void UpdateTimeDisplay()
        {
            if (parentWindow == null) return;

            // 获取剩余时间
            var remainingTime = parentWindow.GetRemainingTime();
            if (remainingTime.HasValue)
            {
                var timeSpan = remainingTime.Value;
                bool isOvertimeMode = timeSpan.TotalSeconds < 0;
                bool shouldShowRed = isOvertimeMode && MainWindow.Settings.RandSettings?.EnableOvertimeRedText == true;

                int hours, minutes, seconds;

                if (isOvertimeMode)
                {
                    var totalTimeSpan = parentWindow.GetTotalTimeSpan();
                    if (totalTimeSpan.HasValue)
                    {
                        var elapsedTime = parentWindow.GetElapsedTime();
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

                // 更新小时显示
                SetDigitDisplay("FullHour1Display", hours / 10, shouldShowRed);
                SetDigitDisplay("FullHour2Display", hours % 10, shouldShowRed);
                
                // 更新分钟显示
                SetDigitDisplay("FullMinute1Display", minutes / 10, shouldShowRed);
                SetDigitDisplay("FullMinute2Display", minutes % 10, shouldShowRed);
                
                // 更新秒显示
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
            // 恢复主窗口
            if (parentWindow != null)
            {
                // 清除全屏模式标志
                parentWindow.SetFullscreenMode(false);
                parentWindow.Show();
                parentWindow.Activate();
                parentWindow.WindowState = WindowState.Normal;
            }
            this.Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            if (parentWindow != null)
            {
                parentWindow.TimerCompleted -= ParentWindow_TimerCompleted;
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
