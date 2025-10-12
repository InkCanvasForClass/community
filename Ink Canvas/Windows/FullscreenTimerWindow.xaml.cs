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
        private SeewoStyleTimerWindow parentWindow;
        private System.Timers.Timer updateTimer;

        public FullscreenTimerWindow(SeewoStyleTimerWindow parent)
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
            if (parentWindow != null && parentWindow.IsTimerRunning)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    UpdateTimeDisplay();
                });
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
                int hours = (int)timeSpan.TotalHours;
                int minutes = timeSpan.Minutes;
                int seconds = timeSpan.Seconds;

                // 更新小时显示
                SetDigitDisplay("FullHour1Display", hours / 10);
                SetDigitDisplay("FullHour2Display", hours % 10);
                
                // 更新分钟显示
                SetDigitDisplay("FullMinute1Display", minutes / 10);
                SetDigitDisplay("FullMinute2Display", minutes % 10);
                
                // 更新秒显示
                SetDigitDisplay("FullSecond1Display", seconds / 10);
                SetDigitDisplay("FullSecond2Display", seconds % 10);
            }
        }
        
        private void ParentWindow_TimerCompleted(object sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                this.Close();
            });
        }

        private void SetDigitDisplay(string pathName, int digit)
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
