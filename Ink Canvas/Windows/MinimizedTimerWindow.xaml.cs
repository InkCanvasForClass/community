using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Media.Animation;

namespace Ink_Canvas
{
    /// <summary>
    /// 最小化计时器窗口
    /// </summary>
    public partial class MinimizedTimerWindow : Window
    {
        private SeewoStyleTimerWindow parentWindow;
        private System.Timers.Timer updateTimer;
        private bool isMouseOver = false;

        public MinimizedTimerWindow(SeewoStyleTimerWindow parent)
        {
            InitializeComponent();
            parentWindow = parent;
            
            // 设置窗口位置（在父窗口右下角）
            this.Left = parent.Left + parent.Width - this.Width - 20;
            this.Top = parent.Top + parent.Height - this.Height - 20;
            
            // 启动更新定时器
            updateTimer = new System.Timers.Timer(100); // 100ms更新一次
            updateTimer.Elapsed += UpdateTimer_Elapsed;
            updateTimer.Start();
            
            // 应用主题
            ApplyTheme();
        }

        private void UpdateTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
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
                SetDigitDisplay("MinHour1Display", hours / 10);
                SetDigitDisplay("MinHour2Display", hours % 10);
                
                // 更新分钟显示
                SetDigitDisplay("MinMinute1Display", minutes / 10);
                SetDigitDisplay("MinMinute2Display", minutes % 10);
                
                // 更新秒显示
                SetDigitDisplay("MinSecond1Display", seconds / 10);
                SetDigitDisplay("MinSecond2Display", seconds % 10);
            }
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

        private void ApplyTheme()
        {
            try
            {
                // 应用主题设置
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    bool isLightTheme = IsLightTheme();
                    if (isLightTheme)
                    {
                        // 应用浅色主题
                        this.Background = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                    }
                    else
                    {
                        // 应用深色主题
                        this.Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"应用主题时出错: {ex.Message}");
            }
        }

        private bool IsLightTheme()
        {
            try
            {
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    var currentModeField = mainWindow.GetType().GetField("currentMode",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (currentModeField != null)
                    {
                        var currentMode = currentModeField.GetValue(mainWindow);
                        return currentMode?.ToString() == "Light";
                    }
                }
            }
            catch
            {
                // 如果获取主题失败，默认使用浅色主题
            }
            return true;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 恢复主窗口
            if (parentWindow != null)
            {
                parentWindow.Show();
                parentWindow.Activate();
                parentWindow.WindowState = WindowState.Normal;
                this.Close();
            }
        }

        private void Window_MouseEnter(object sender, MouseEventArgs e)
        {
            isMouseOver = true;
            // 鼠标进入时显示关闭按钮
            if (CloseButton != null)
            {
                CloseButton.Opacity = 1.0;
            }
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            isMouseOver = false;
            // 鼠标离开时隐藏关闭按钮
            if (CloseButton != null)
            {
                CloseButton.Opacity = 0.7;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // 停止计时器并关闭窗口
            if (parentWindow != null)
            {
                parentWindow.StopTimer();
            }
            this.Close();
        }

        protected override void OnClosed(EventArgs e)
        {
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
