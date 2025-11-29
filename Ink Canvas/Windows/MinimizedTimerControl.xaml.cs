using System;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using iNKORE.UI.WPF.Modern;

namespace Ink_Canvas.Windows
{
    /// <summary>
    /// 最小化计时器窗口
    /// </summary>
    public partial class MinimizedTimerControl : UserControl
    {
        private TimerControl parentControl;
        private System.Timers.Timer updateTimer;

        public MinimizedTimerControl()
        {
            InitializeComponent();
            
            updateTimer = new System.Timers.Timer(100);
            updateTimer.Elapsed += UpdateTimer_Elapsed;
            updateTimer.Start();
            
            ApplyTheme();
            
            // 监听主题变化事件
            SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
            
            Unloaded += MinimizedTimerControl_Unloaded;
        }
        
        private void MinimizedTimerControl_Unloaded(object sender, RoutedEventArgs e)
        {
            // 取消订阅主题变化事件
            SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
            
            if (parentControl != null)
            {
                parentControl.TimerCompleted -= ParentControl_TimerCompleted;
            }
            
            if (updateTimer != null)
            {
                updateTimer.Stop();
                updateTimer.Dispose();
            }
        }
        
        private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            // 当主题变化时，重新应用主题
            Application.Current.Dispatcher.Invoke(() =>
            {
                RefreshTheme();
            });
        }
        
        /// <summary>
        /// 刷新主题
        /// </summary>
        public void RefreshTheme()
        {
            try
            {
                // 重新应用主题
                ApplyTheme();
                
                // 强制刷新UI
                InvalidateVisual();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"刷新最小化计时器窗口主题出错: {ex.Message}");
            }
        }

        public void SetParentControl(TimerControl parent)
        {
            if (parentControl != null)
            {
                parentControl.TimerCompleted -= ParentControl_TimerCompleted;
            }
            
            parentControl = parent;
            
            if (parentControl != null)
            {
                parentControl.TimerCompleted += ParentControl_TimerCompleted;
                UpdateTimeDisplay();
            }
        }

        private void UpdateTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (parentControl != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (this.Visibility != Visibility.Visible)
                    {
                        return;
                    }
                    
                    if (ShouldHide())
                    {
                        this.Visibility = Visibility.Collapsed;
                        var parent = this.Parent as FrameworkElement;
                        if (parent != null)
                        {
                            parent.Visibility = Visibility.Collapsed;
                        }
                        return;
                    }
                    
                    UpdateTimeDisplay();
                });
            }
        }
        
        private bool ShouldHide()
        {
            if (parentControl == null) return true;
            
            if (parentControl.IsFullscreenWindowOpen)
            {
                return true;
            }
            
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

                SetDigitDisplay("MinHour1Display", Math.Abs(hours / 10) % 10, shouldShowRed);
                SetDigitDisplay("MinHour2Display", (hours % 10 + 10) % 10, shouldShowRed);
                
                SetDigitDisplay("MinMinute1Display", minutes / 10, shouldShowRed);
                SetDigitDisplay("MinMinute2Display", minutes % 10, shouldShowRed);
                
                SetDigitDisplay("MinSecond1Display", seconds / 10, shouldShowRed);
                SetDigitDisplay("MinSecond2Display", seconds % 10, shouldShowRed);
                
                SetColonDisplay(shouldShowRed);
            }
        }
        
        private void ParentControl_TimerCompleted(object sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Visibility = Visibility.Collapsed;
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
                
                if (isRed)
                {
                    path.Fill = Brushes.Red;
                }
                else
                {
                    var defaultBrush = this.TryFindResource("NewTimerWindowDigitForeground") as Brush;
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

        private void SetColonDisplay(bool isRed = false)
        {
            var colon1 = this.FindName("MinColon1Display") as TextBlock;
            var colon2 = this.FindName("MinColon2Display") as TextBlock;
            
            if (colon1 != null)
            {
                if (isRed)
                {
                    colon1.Foreground = Brushes.Red;
                }
                else
                {
                    var defaultBrush = this.TryFindResource("NewTimerWindowDigitForeground") as Brush;
                    if (defaultBrush != null)
                    {
                        colon1.Foreground = defaultBrush;
                    }
                    else
                    {
                        bool isLightTheme = IsLightTheme();
                        colon1.Foreground = isLightTheme ? Brushes.Black : Brushes.White;
                    }
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
                    var defaultBrush = this.TryFindResource("NewTimerWindowDigitForeground") as Brush;
                    if (defaultBrush != null)
                    {
                        colon2.Foreground = defaultBrush;
                    }
                    else
                    {
                        bool isLightTheme = IsLightTheme();
                        colon2.Foreground = isLightTheme ? Brushes.Black : Brushes.White;
                    }
                }
            }
        }

        private void ApplyTheme()
        {
            try
            {
                if (MainWindow.Settings != null)
                {
                    ApplyTheme(MainWindow.Settings);
                }
                else
                {
                    bool isLightTheme = IsLightTheme();
                    if (!isLightTheme)
                    {
                        SetDarkThemeBorder();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"应用主题时出错: {ex.Message}");
            }
        }
        
        private void ApplyTheme(Settings settings)
        {
            try
            {
                if (settings.Appearance.Theme == 0) // 浅色主题
                {
                    ThemeManager.SetRequestedTheme(this, ElementTheme.Light);
                }
                else if (settings.Appearance.Theme == 1) // 深色主题
                {
                    ThemeManager.SetRequestedTheme(this, ElementTheme.Dark);
                    SetDarkThemeBorder();
                }
                else // 跟随系统主题
                {
                    bool isSystemLight = IsSystemThemeLight();
                    if (isSystemLight)
                    {
                        ThemeManager.SetRequestedTheme(this, ElementTheme.Light);
                    }
                    else
                    {
                        ThemeManager.SetRequestedTheme(this, ElementTheme.Dark);
                        SetDarkThemeBorder();
                    }
                }
                
                // 刷新数字和冒号显示的颜色
                if (parentControl != null)
                {
                    UpdateTimeDisplay();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"应用最小化计时器窗口主题出错: {ex.Message}");
            }
        }
        
        private bool IsSystemThemeLight()
        {
            var light = false;
            try
            {
                var registryKey = Microsoft.Win32.Registry.CurrentUser;
                var themeKey = registryKey.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (themeKey != null)
                {
                    var value = themeKey.GetValue("AppsUseLightTheme");
                    if (value != null)
                    {
                        light = (int)value == 1;
                    }
                    themeKey.Close();
                }
            }
            catch
            {
                // 如果读取注册表失败，默认为浅色主题
                light = true;
            }
            return light;
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
            }
            return true;
        }

        private void SetDarkThemeBorder()
        {
            try
            {
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

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (parentControl != null)
            {
                parentControl.StopTimer();
            }
            Visibility = Visibility.Collapsed;
        }
        
        private bool isDragging = false;
        private bool isDragStarted = false;
        private Point dragStartPoint;
        private Point containerStartPosition;
        private const double DragThreshold = 5.0; // 拖动阈值，像素
        
        private void MainBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                // 双击：恢复主窗口
                if (parentControl != null)
                {
                    parentControl.UpdateActivityTime();
                    
                    var mainWindow = Application.Current.MainWindow as MainWindow;
                    if (mainWindow != null)
                    {
                        var timerContainer = mainWindow.FindName("TimerContainer") as FrameworkElement;
                        var minimizedContainer = mainWindow.FindName("MinimizedTimerContainer") as FrameworkElement;
                        
                        if (timerContainer != null && minimizedContainer != null)
                        {
                            timerContainer.Visibility = Visibility.Visible;
                            minimizedContainer.Visibility = Visibility.Collapsed;
                        }
                    }
                }
                e.Handled = true;
            }
            else if (e.ClickCount == 1)
            {
                // 单击：准备拖动或点击
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    var minimizedContainer = mainWindow.FindName("MinimizedTimerContainer") as FrameworkElement;
                    if (minimizedContainer != null)
                    {
                        var point = e.GetPosition(minimizedContainer);
                        var mainWindowPoint = minimizedContainer.TransformToAncestor(mainWindow).Transform(point);
                        
                        // 初始化拖动状态，但不立即开始拖动
                        isDragging = false;
                        isDragStarted = false;
                        dragStartPoint = mainWindowPoint;
                        
                        var margin = minimizedContainer.Margin;
                        containerStartPosition = new Point(margin.Left, margin.Top);
                        
                        if (double.IsNaN(containerStartPosition.X) || containerStartPosition.X < 0) containerStartPosition.X = 0;
                        if (double.IsNaN(containerStartPosition.Y) || containerStartPosition.Y < 0) containerStartPosition.Y = 0;
                        
                        // 捕获鼠标并订阅事件，等待判断是拖动还是点击
                        minimizedContainer.CaptureMouse();
                        minimizedContainer.MouseMove += MinimizedContainer_MouseMove;
                        minimizedContainer.MouseLeftButtonUp += MinimizedContainer_MouseLeftButtonUp;
                        e.Handled = true;
                    }
                }
            }
        }
        
        private void MinimizedContainer_MouseMove(object sender, MouseEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow == null) return;
            
            var minimizedContainer = mainWindow.FindName("MinimizedTimerContainer") as FrameworkElement;
            if (minimizedContainer == null) return;
            
            var currentPoint = e.GetPosition(mainWindow);
            var deltaX = currentPoint.X - dragStartPoint.X;
            var deltaY = currentPoint.Y - dragStartPoint.Y;
            var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            
            // 如果移动距离超过阈值，开始拖动
            if (!isDragStarted && distance > DragThreshold)
            {
                isDragStarted = true;
                isDragging = true;
            }
            
            // 如果已经开始拖动，更新位置
            if (isDragging)
            {
                var timerContainer = mainWindow.FindName("TimerContainer") as FrameworkElement;
                
                var newX = containerStartPosition.X + deltaX;
                var newY = containerStartPosition.Y + deltaY;
                
                if (newX < 0) newX = 0;
                if (newY < 0) newY = 0;
                
                minimizedContainer.Margin = new Thickness(newX, newY, 0, 0);
                minimizedContainer.HorizontalAlignment = HorizontalAlignment.Left;
                minimizedContainer.VerticalAlignment = VerticalAlignment.Top;
                
                if (timerContainer != null)
                {
                    timerContainer.Margin = new Thickness(newX, newY, 0, 0);
                    timerContainer.HorizontalAlignment = HorizontalAlignment.Left;
                    timerContainer.VerticalAlignment = VerticalAlignment.Top;
                }
            }
        }
        
        private void MinimizedContainer_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow == null) return;
            
            var minimizedContainer = mainWindow.FindName("MinimizedTimerContainer") as FrameworkElement;
            if (minimizedContainer != null)
            {
                minimizedContainer.ReleaseMouseCapture();
                minimizedContainer.MouseMove -= MinimizedContainer_MouseMove;
                minimizedContainer.MouseLeftButtonUp -= MinimizedContainer_MouseLeftButtonUp;
            }
            
            // 如果没有开始拖动（移动距离小于阈值），则视为单击，恢复主窗口
            if (!isDragStarted)
            {
                if (parentControl != null)
                {
                    parentControl.UpdateActivityTime();
                    
                    var timerContainer = mainWindow.FindName("TimerContainer") as FrameworkElement;
                    if (timerContainer != null && minimizedContainer != null)
                    {
                        timerContainer.Visibility = Visibility.Visible;
                        minimizedContainer.Visibility = Visibility.Collapsed;
                    }
                }
            }
            
            isDragging = false;
            isDragStarted = false;
        }

    }
}


