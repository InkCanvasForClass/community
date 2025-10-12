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
        private NewStyleTimerWindow parentWindow;
        private System.Timers.Timer updateTimer;
        private bool isMouseOver = false;
        private bool isDragging = false;
        private Point lastMousePosition;

        public MinimizedTimerWindow(NewStyleTimerWindow parent)
        {
            InitializeComponent();
            parentWindow = parent;
            
            // 设置窗口位置
            this.Left = parent.Left;
            this.Top = parent.Top;
            
            // 启动更新定时器
            updateTimer = new System.Timers.Timer(100); // 100ms更新一次
            updateTimer.Elapsed += UpdateTimer_Elapsed;
            updateTimer.Start();
            
            parentWindow.TimerCompleted += ParentWindow_TimerCompleted;
            
            // 应用主题
            ApplyTheme();
        }

        private void UpdateTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
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
                SetDigitDisplay("MinHour1Display", hours / 10, shouldShowRed);
                SetDigitDisplay("MinHour2Display", hours % 10, shouldShowRed);
                
                // 更新分钟显示
                SetDigitDisplay("MinMinute1Display", minutes / 10, shouldShowRed);
                SetDigitDisplay("MinMinute2Display", minutes % 10, shouldShowRed);
                
                // 更新秒显示
                SetDigitDisplay("MinSecond1Display", seconds / 10, shouldShowRed);
                SetDigitDisplay("MinSecond2Display", seconds % 10, shouldShowRed);
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
                    var defaultBrush = this.FindResource("NewTimerWindowDigitForeground") as Brush;
                    if (defaultBrush != null)
                    {
                        path.Fill = defaultBrush;
                    }
                    else
                    {
                        bool isLightTheme = IsLightTheme();
                        path.Fill = isLightTheme ? Brushes.Black : Brushes.White;
                    }
                }
            }
        }

        private void ApplyTheme()
        {
            try
            {

                bool isLightTheme = IsLightTheme();
                if (!isLightTheme)
                {
                    SetDarkThemeBorder();
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

        // 设置深色主题下的灰色边框
        private void SetDarkThemeBorder()
        {
            try
            {
                // 找到Border元素并设置灰色边框
                var border = this.FindName("MainBorder") as Border;
                if (border != null)
                {
                    border.BorderBrush = new SolidColorBrush(Color.FromRgb(64, 64, 64));
                }
            }
            catch
            {
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 记录点击时间
            lastClickTime = DateTime.Now;
            // 开始拖动
            isDragging = true;
            lastMousePosition = e.GetPosition(this);
            this.CaptureMouse();
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                var currentPosition = e.GetPosition(this);
                var deltaX = currentPosition.X - lastMousePosition.X;
                var deltaY = currentPosition.Y - lastMousePosition.Y;
                
                this.Left += deltaX;
                this.Top += deltaY;
            }
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (isDragging)
            {
                isDragging = false;
                this.ReleaseMouseCapture();
                
                // 如果点击时间很短，认为是单击，恢复主窗口
                var clickDuration = DateTime.Now - lastClickTime;
                if (clickDuration.TotalMilliseconds < 200) // 200ms内认为是单击
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
            }
        }

        private DateTime lastClickTime = DateTime.Now;

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
