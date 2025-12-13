using Ink_Canvas.Helpers;
using System;
using System.IO;
using System.Media;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Newtonsoft.Json;
using System.Windows.Threading;
using Microsoft.Win32;
namespace Ink_Canvas.Windows
{
    /// <summary>
    /// 最近计时记录数据模型
    /// </summary>
    public class RecentTimersData
    {
        public string RecentTimer1 { get; set; } = "--:--";
        public string RecentTimer2 { get; set; } = "--:--";
        public string RecentTimer3 { get; set; } = "--:--";
        public string RecentTimer4 { get; set; } = "--:--";
        public string RecentTimer5 { get; set; } = "--:--";
        public string RecentTimer6 { get; set; } = "--:--";
    }

    /// <summary>
    /// 新计时器UI风格的倒计时器窗口
    /// </summary>
    public partial class TimerControl : UserControl
    {
        public TimerControl()
        {
            InitializeComponent();

            timer.Elapsed += Timer_Elapsed;
            timer.Interval = 50;
            InitializeUI();

            // 应用主题
            ApplyTheme();
            
            // 初始化隐藏定时器
            hideTimer = new Timer(1000); // 每秒检查一次
            hideTimer.Elapsed += HideTimer_Elapsed;
            lastActivityTime = DateTime.Now;
            
            // 监听主题变化事件
            SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
            
            // 监听卸载事件，清理资源
            Unloaded += TimerControl_Unloaded;
        }
        
        private void TimerControl_Unloaded(object sender, RoutedEventArgs e)
        {
            // 取消订阅主题变化事件
            SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
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
                LogHelper.WriteLogToFile($"刷新计时器窗口主题出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }
        
        #region 事件定义
        /// <summary>
        /// 计时器完成事件
        /// </summary>
        public event EventHandler TimerCompleted;
        
        /// <summary>
        /// 关闭事件 - 通知主窗口隐藏容器
        /// </summary>
        public event EventHandler CloseRequested;
        
        /// <summary>
        /// 显示最小化视图事件
        /// </summary>
        public event EventHandler ShowMinimizedRequested;
        
        /// <summary>
        /// 隐藏最小化视图事件
        /// </summary>
        public event EventHandler HideMinimizedRequested;
        #endregion


        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (!isTimerRunning || isPaused)
            {
                timer.Stop();
                return;
            }

            TimeSpan timeSpan = DateTime.Now - startTime;
            TimeSpan totalTimeSpan = new TimeSpan(hour, minute, second);
            double spentTimePercent = timeSpan.TotalMilliseconds / (totalTimeSpan.TotalMilliseconds);

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (!isOvertimeMode)
                {
                    TimeSpan leftTimeSpan = totalTimeSpan - timeSpan;
                    if (leftTimeSpan.Milliseconds > 0) leftTimeSpan += new TimeSpan(0, 0, 1);
                    
                    int totalHours = (int)leftTimeSpan.TotalHours;
                    int displayHours = totalHours;

                    if (displayHours > 99) displayHours = 99;

                    SetDigitDisplay("Digit1Display", displayHours / 10);
                    SetDigitDisplay("Digit2Display", displayHours % 10);
                    SetDigitDisplay("Digit3Display", leftTimeSpan.Minutes / 10);
                    SetDigitDisplay("Digit4Display", leftTimeSpan.Minutes % 10);
                    SetDigitDisplay("Digit5Display", leftTimeSpan.Seconds / 10);
                    SetDigitDisplay("Digit6Display", leftTimeSpan.Seconds % 10);
                    
                    SetColonDisplay(false);
                    
                    if (leftTimeSpan.TotalSeconds <= 6 && leftTimeSpan.TotalSeconds > 0 && 
                        MainWindow.Settings.RandSettings?.EnableProgressiveReminder == true &&
                        !hasPlayedProgressiveReminder)
                    {
                        PlayProgressiveReminderSound();
                        hasPlayedProgressiveReminder = true;
                    }

                    if (leftTimeSpan.TotalSeconds <= 0 && MainWindow.Settings.RandSettings?.EnableOvertimeCountUp == true)
                    {
                        isOvertimeMode = true;
                        PlayTimerSound();
                    }
                    else if (leftTimeSpan.TotalSeconds <= 0)
                    {
                        SetDigitDisplay("Digit1Display", 0);
                        SetDigitDisplay("Digit2Display", 0);
                        SetDigitDisplay("Digit3Display", 0);
                        SetDigitDisplay("Digit4Display", 0);
                        SetDigitDisplay("Digit5Display", 0);
                        SetDigitDisplay("Digit6Display", 0);
                        
                        SetColonDisplay(false);
                        timer.Stop();
                        isTimerRunning = false;
                        StartPauseIcon.Data = Geometry.Parse(PlayIconData);
                        PlayTimerSound();
                        
                        // 禁用全屏按钮
                        if (FullscreenBtn != null)
                        {
                            FullscreenBtn.IsEnabled = false;
                        }
                        
                        TimerCompleted?.Invoke(this, EventArgs.Empty);
                        HandleTimerCompletion();
                    }
                }
                else
                {
                    TimeSpan overtimeSpan = timeSpan - totalTimeSpan;
                    int totalHours = (int)overtimeSpan.TotalHours;
                    int displayHours = totalHours;

                    if (displayHours > 99) displayHours = 99;
                    if (displayHours < 0) displayHours = 0;

                    bool shouldShowRed = MainWindow.Settings.RandSettings?.EnableOvertimeRedText == true;

                    int hoursTens = Math.Max(0, Math.Min(9, Math.Abs(displayHours / 10) % 10));
                    int hoursOnes = Math.Max(0, Math.Min(9, (displayHours % 10 + 10) % 10));
                    int minutesTens = Math.Max(0, Math.Min(9, Math.Abs(overtimeSpan.Minutes) / 10));
                    int minutesOnes = Math.Max(0, Math.Min(9, Math.Abs(overtimeSpan.Minutes) % 10));
                    int secondsTens = Math.Max(0, Math.Min(9, Math.Abs(overtimeSpan.Seconds) / 10));
                    int secondsOnes = Math.Max(0, Math.Min(9, Math.Abs(overtimeSpan.Seconds) % 10));

                    SetDigitDisplay("Digit1Display", hoursTens, shouldShowRed);
                    SetDigitDisplay("Digit2Display", hoursOnes, shouldShowRed);
                    SetDigitDisplay("Digit3Display", minutesTens, shouldShowRed);
                    SetDigitDisplay("Digit4Display", minutesOnes, shouldShowRed);
                    SetDigitDisplay("Digit5Display", secondsTens, shouldShowRed);
                    SetDigitDisplay("Digit6Display", secondsOnes, shouldShowRed);
                    
                    SetColonDisplay(shouldShowRed);
                }
            });
        }

        SoundPlayer player = new SoundPlayer();
        MediaPlayer mediaPlayer = new MediaPlayer();

        int hour = 0;
        int minute = 5;
        int second = 0;

        DateTime startTime = DateTime.Now;
        DateTime pauseTime = DateTime.Now;

        bool isTimerRunning = false;
        bool isPaused = false;
        bool isOvertimeMode = false; 
        TimeSpan remainingTime = TimeSpan.Zero;
        bool hasPlayedProgressiveReminder = false; 
        
        Timer timer = new Timer();
        private Timer hideTimer;
        private DateTime lastActivityTime;        
        public TimeSpan? GetTotalTimeSpan()
        {
            return new TimeSpan(hour, minute, second);
        }
        
        public TimeSpan? GetElapsedTime()
        {
            if (isPaused) return null;
            
            return DateTime.Now - startTime;
        }

        // 最近计时记录
        private string recentTimer1 = "--:--";
        private string recentTimer2 = "--:--";
        private string recentTimer3 = "--:--";
        private string recentTimer4 = "--:--";
        private string recentTimer5 = "--:--";
        private string recentTimer6 = "--:--";

        // JSON文件路径
        private static readonly string ConfigsFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs");
        private static readonly string RecentTimersJsonPath = System.IO.Path.Combine(ConfigsFolder, "RecentTimers.json");

        private void InitializeUI()
        {
            UpdateDigitDisplays();
            LoadRecentTimers();
            UpdateRecentTimerDisplays();
            InitializeTabState();
        }

        private void InitializeTabState()
        {
            // 设置默认选中CommonTab
            CommonTimersGrid.Visibility = Visibility.Visible;
            RecentTimersGrid.Visibility = Visibility.Collapsed;

            // 设置tab文字颜色和样式
            var commonText = this.FindName("CommonTabText") as TextBlock;
            var recentText = this.FindName("RecentTabText") as TextBlock;
            if (commonText != null)
            {
                commonText.FontWeight = FontWeights.Bold;
                commonText.Opacity = 1.0;
                commonText.Foreground = new SolidColorBrush(Colors.White);
            }
            if (recentText != null)
            {
                recentText.FontWeight = FontWeights.Normal;
                recentText.Opacity = 0.8;
                recentText.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102));
            }

            // 设置指示器位置
            var indicator = this.FindName("SegmentedIndicator") as Border;
            if (indicator != null)
            {
                indicator.CornerRadius = new CornerRadius(7.5, 0, 0, 7.5);
                indicator.Margin = new Thickness(0, 0, 0, 0);
            }
        }

        private void ApplyTheme()
        {
            try
            {
                // 应用主题设置
                if (MainWindow.Settings != null)
                {
                    ApplyTheme(MainWindow.Settings);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用新计时器UI倒计时窗口主题出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void ApplyTheme(Settings settings)
        {
            try
            {
                if (settings.Appearance.Theme == 0) // 浅色主题
                {
                    iNKORE.UI.WPF.Modern.ThemeManager.SetRequestedTheme(this, iNKORE.UI.WPF.Modern.ElementTheme.Light);
                }
                else if (settings.Appearance.Theme == 1) // 深色主题
                {
                    iNKORE.UI.WPF.Modern.ThemeManager.SetRequestedTheme(this, iNKORE.UI.WPF.Modern.ElementTheme.Dark);
                    SetDarkThemeBorder();
                }
                else // 跟随系统主题
                {
                    bool isSystemLight = IsSystemThemeLight();
                    if (isSystemLight)
                    {
                        iNKORE.UI.WPF.Modern.ThemeManager.SetRequestedTheme(this, iNKORE.UI.WPF.Modern.ElementTheme.Light);
                    }
                    else
                    {
                        iNKORE.UI.WPF.Modern.ThemeManager.SetRequestedTheme(this, iNKORE.UI.WPF.Modern.ElementTheme.Dark);
                        SetDarkThemeBorder();
                    }
                }
                
                // 刷新数字和冒号显示的颜色
                UpdateDigitDisplays();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用新计时器UI倒计时窗口主题出错: {ex.Message}", LogHelper.LogType.Error);
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

        private void UpdateDigitDisplays()
        {
            SetDigitDisplay("Digit1Display", hour / 10);
            SetDigitDisplay("Digit2Display", hour % 10);
            SetDigitDisplay("Digit3Display", minute / 10);
            SetDigitDisplay("Digit4Display", minute % 10);
            SetDigitDisplay("Digit5Display", second / 10);
            SetDigitDisplay("Digit6Display", second % 10);
            
            SetColonDisplay(false);
        }

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
                    TimeSpan elapsedTime = DateTime.Now - startTime;
                    
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
                    TimeSpan elapsedTime = DateTime.Now - startTime;
                    
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

        public bool IsTimerRunning => isTimerRunning;

        public TimeSpan? GetRemainingTime()
        {
            if (isPaused) return null;
            
            var elapsed = DateTime.Now - startTime;
            var totalTimeSpan = new TimeSpan(hour, minute, second);
            var leftTimeSpan = totalTimeSpan - elapsed;
            
            if (leftTimeSpan.Milliseconds > 0) leftTimeSpan += new TimeSpan(0, 0, 1);
            
            return leftTimeSpan;
        }

        public void StopTimer()
        {
            timer.Stop();
            isTimerRunning = false;
            StartPauseIcon.Data = Geometry.Parse(PlayIconData);
        }


        /// <summary>
        /// 根据数字值设置SVG数字显示
        /// </summary>
        /// <param name="pathName">Path控件的名称</param>
        /// <param name="digit">要显示的数字(0-9)</param>
        /// <param name="isRed">是否显示为红色</param>
        private void SetDigitDisplay(string pathName, int digit, bool isRed = false)
        {
            var path = this.FindName(pathName) as System.Windows.Shapes.Path;
            if (path != null)
            {
                digit = Math.Max(0, Math.Min(9, digit));
                
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
                        path.Fill = Brushes.White;
                    }
                }
            }
        }

        /// <summary>
        /// 设置冒号显示颜色
        /// </summary>
        /// <param name="isRed">是否显示为红色</param>
        private void SetColonDisplay(bool isRed = false)
        {
            var colon1 = this.FindName("Colon1Display") as TextBlock;
            var colon2 = this.FindName("Colon2Display") as TextBlock;
            
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
                        colon1.Foreground = Brushes.White;
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
                        colon2.Foreground = Brushes.White;
                    }
                }
            }
        }

        // 第1位数字（小时十位）
        private void Digit1Plus_Click(object sender, RoutedEventArgs e)
        {
            if (isTimerRunning) return;
            UpdateActivityTime();
            int currentHour = hour;
            int hourTens = currentHour / 10;
            int hourOnes = currentHour % 10;

            hourTens++;
            if (hourTens >= 10) hourTens = 0;

            hour = hourTens * 10 + hourOnes;
            UpdateDigitDisplays();
        }

        private void Digit1Minus_Click(object sender, RoutedEventArgs e)
        {
            if (isTimerRunning) return;
            UpdateActivityTime();
            int currentHour = hour;
            int hourTens = currentHour / 10;
            int hourOnes = currentHour % 10;

            hourTens--;
            if (hourTens < 0) hourTens = 9;

            hour = hourTens * 10 + hourOnes;
            UpdateDigitDisplays();
        }

        // 第2位数字（小时个位）
        private void Digit2Plus_Click(object sender, RoutedEventArgs e)
        {
            if (isTimerRunning) return;
            UpdateActivityTime();
            int currentHour = hour;
            int hourTens = currentHour / 10;
            int hourOnes = currentHour % 10;

            hourOnes++;
            if (hourOnes >= 10)
            {
                hourOnes = 0;
                hourTens++;
                if (hourTens >= 10) hourTens = 0;
            }

            hour = hourTens * 10 + hourOnes;
            UpdateDigitDisplays();
        }

        private void Digit2Minus_Click(object sender, RoutedEventArgs e)
        {
            if (isTimerRunning) return;
            UpdateActivityTime();
            int currentHour = hour;
            int hourTens = currentHour / 10;
            int hourOnes = currentHour % 10;

            hourOnes--;
            if (hourOnes < 0)
            {
                hourOnes = 9;
                hourTens--;
                if (hourTens < 0) hourTens = 9;
            }

            hour = hourTens * 10 + hourOnes;
            UpdateDigitDisplays();
        }

        // 第3位数字（分钟十位）
        private void Digit3Plus_Click(object sender, RoutedEventArgs e)
        {
            if (isTimerRunning) return;
            UpdateActivityTime();
            int currentMinute = minute;
            int minuteTens = currentMinute / 10;
            int minuteOnes = currentMinute % 10;

            minuteTens++;
            if (minuteTens >= 6) minuteTens = 0;

            minute = minuteTens * 10 + minuteOnes;
            UpdateDigitDisplays();
        }

        private void Digit3Minus_Click(object sender, RoutedEventArgs e)
        {
            if (isTimerRunning) return;
            UpdateActivityTime();
            int currentMinute = minute;
            int minuteTens = currentMinute / 10;
            int minuteOnes = currentMinute % 10;

            minuteTens--;
            if (minuteTens < 0) minuteTens = 5;

            minute = minuteTens * 10 + minuteOnes;
            UpdateDigitDisplays();
        }

        // 第4位数字（分钟个位）
        private void Digit4Plus_Click(object sender, RoutedEventArgs e)
        {
            if (isTimerRunning) return;
            UpdateActivityTime();
            int currentMinute = minute;
            int minuteTens = currentMinute / 10;
            int minuteOnes = currentMinute % 10;

            minuteOnes++;
            if (minuteOnes >= 10)
            {
                minuteOnes = 0;
                minuteTens++;
                if (minuteTens >= 6) minuteTens = 0;
            }

            minute = minuteTens * 10 + minuteOnes;
            UpdateDigitDisplays();
        }

        private void Digit4Minus_Click(object sender, RoutedEventArgs e)
        {
            if (isTimerRunning) return;
            UpdateActivityTime();
            int currentMinute = minute;
            int minuteTens = currentMinute / 10;
            int minuteOnes = currentMinute % 10;

            minuteOnes--;
            if (minuteOnes < 0)
            {
                minuteOnes = 9;
                minuteTens--;
                if (minuteTens < 0) minuteTens = 5;
            }

            minute = minuteTens * 10 + minuteOnes;
            UpdateDigitDisplays();
        }

        // 第5位数字（秒十位）
        private void Digit5Plus_Click(object sender, RoutedEventArgs e)
        {
            if (isTimerRunning) return;
            UpdateActivityTime();
            int currentSecond = second;
            int secondTens = currentSecond / 10;
            int secondOnes = currentSecond % 10;

            secondTens++;
            if (secondTens >= 6) secondTens = 0;

            second = secondTens * 10 + secondOnes;
            UpdateDigitDisplays();
        }

        private void Digit5Minus_Click(object sender, RoutedEventArgs e)
        {
            if (isTimerRunning) return;
            UpdateActivityTime();
            int currentSecond = second;
            int secondTens = currentSecond / 10;
            int secondOnes = currentSecond % 10;

            secondTens--;
            if (secondTens < 0) secondTens = 5;

            second = secondTens * 10 + secondOnes;
            UpdateDigitDisplays();
        }

        // 第6位数字（秒个位）
        private void Digit6Plus_Click(object sender, RoutedEventArgs e)
        {
            if (isTimerRunning) return;
            UpdateActivityTime();
            int currentSecond = second;
            int secondTens = currentSecond / 10;
            int secondOnes = currentSecond % 10;

            secondOnes++;
            if (secondOnes >= 10)
            {
                secondOnes = 0;
                secondTens++;
                if (secondTens >= 6) secondTens = 0;
            }

            second = secondTens * 10 + secondOnes;
            UpdateDigitDisplays();
        }

        private void Digit6Minus_Click(object sender, RoutedEventArgs e)
        {
            if (isTimerRunning) return;
            UpdateActivityTime();
            int currentSecond = second;
            int secondTens = currentSecond / 10;
            int secondOnes = currentSecond % 10;

            secondOnes--;
            if (secondOnes < 0)
            {
                secondOnes = 9;
                secondTens--;
                if (secondTens < 0) secondTens = 5;
            }

            second = secondTens * 10 + secondOnes;
            UpdateDigitDisplays();
        }

        // 图标数据常量
        private const string PlayIconData = "M6.5 4.00004V20C6.49995 20.178 6.54737 20.3527 6.63738 20.5062C6.72739 20.6597 6.85672 20.7864 7.01202 20.8732C7.16733 20.96 7.34299 21.0038 7.52088 21.0001C7.69878 20.9964 7.87245 20.9453 8.024 20.852L21.024 12.852C21.1696 12.7626 21.2898 12.6373 21.3733 12.4881C21.4567 12.339 21.5005 12.1709 21.5005 12C21.5005 11.8291 21.4567 11.6611 21.3733 11.512C21.2898 11.3628 21.1696 11.2375 21.024 11.148L8.024 3.14804C7.87245 3.0548 7.69878 3.00369 7.52088 2.99997C7.34299 2.99626 7.16733 3.04007 7.01202 3.1269C6.85672 3.21372 6.72739 3.34042 6.63738 3.4939C6.54737 3.64739 6.49995 3.82211 6.5 4.00004Z";
        private const string PauseIconData = "M9.5 4H7.5C6.96957 4 6.46086 4.21071 6.08579 4.58579C5.71071 4.96086 5.5 5.46957 5.5 6V18C5.5 18.5304 5.71071 19.0391 6.08579 19.4142C6.46086 19.7893 6.96957 20 7.5 20H9.5C10.0304 20 10.5391 19.7893 10.9142 19.4142C11.2893 19.0391 11.5 18.5304 11.5 18V6C11.5 5.46957 11.2893 4.96086 10.9142 4.58579C10.5391 4.21071 10.0304 4 9.5 4Z M17.5 4H15.5C14.9696 4 14.4609 4.21071 14.0858 4.58579C13.7107 4.96086 13.5 5.46957 13.5 6V18C13.5 18.5304 13.7107 19.0391 14.0858 19.4142C14.4609 19.7893 14.9696 20 15.5 20H17.5C18.0304 20 18.5391 19.7893 18.9142 19.4142C19.2893 19.0391 19.5 18.5304 19.5 18V6C19.5 5.46957 19.2893 4.96086 18.9142 4.58579C18.5391 4.21071 18.0304 4 17.5 4Z";

        private void StartPause_Click(object sender, RoutedEventArgs e)
        {
            UpdateActivityTime();
            if (isPaused && isTimerRunning)
            {
                // 继续计时
                startTime += DateTime.Now - pauseTime;
                StartPauseIcon.Data = Geometry.Parse(PauseIconData);
                isPaused = false;
                timer.Start();
            }
            else if (isTimerRunning)
            {
                // 暂停计时
                pauseTime = DateTime.Now;
                StartPauseIcon.Data = Geometry.Parse(PlayIconData);
                isPaused = true;
                timer.Stop();
            }
            else
            {
                // 开始计时
                if (hour == 0 && minute == 0 && second == 0)
                {
                    second = 1;
                    UpdateDigitDisplays();
                }

                startTime = DateTime.Now;
                StartPauseIcon.Data = Geometry.Parse(PauseIconData);
                isPaused = false;
                isTimerRunning = true;
                isOvertimeMode = false;
                hasPlayedProgressiveReminder = false; 
                timer.Start();
                
                // 启动隐藏定时器
                hideTimer.Start();

                // 保存到最近计时记录
                SaveRecentTimer();
                
                // 启用全屏按钮
                if (FullscreenBtn != null)
                {
                    FullscreenBtn.IsEnabled = true;
                }
            }
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            UpdateActivityTime();
            
            if (isTimerRunning)
            {
                // 停止计时器
                timer.Stop();
                isTimerRunning = false;
                isPaused = false;
                
                if (hideTimer != null)
                {
                    hideTimer.Stop();
                }
            }
            
            UpdateDigitDisplays();
            SetColonDisplay(false);
            
            if (StartPauseIcon != null)
            {
                StartPauseIcon.Data = Geometry.Parse(PlayIconData);
            }
            
            isOvertimeMode = false;
            hasPlayedProgressiveReminder = false;
            
            // 禁用全屏按钮
            if (FullscreenBtn != null)
            {
                FullscreenBtn.IsEnabled = false;
            }
        }

        private void PlayTimerSound()
        {
            try
            {
                double volume = MainWindow.Settings.RandSettings?.TimerVolume ?? 1.0;
                mediaPlayer.Volume = volume;

                if (!string.IsNullOrEmpty(MainWindow.Settings.RandSettings?.CustomTimerSoundPath) &&
                    System.IO.File.Exists(MainWindow.Settings.RandSettings.CustomTimerSoundPath))
                {
                    mediaPlayer.Open(new Uri(MainWindow.Settings.RandSettings.CustomTimerSoundPath));
                }
                else
                {
                    string tempPath = System.IO.Path.GetTempFileName() + ".wav";
                    using (var stream = Properties.Resources.TimerDownNotice)
                    {
                        using (var fileStream = new System.IO.FileStream(tempPath, System.IO.FileMode.Create))
                        {
                            stream.CopyTo(fileStream);
                        }
                    }
                    mediaPlayer.Open(new Uri(tempPath));
                }

                mediaPlayer.Play();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"播放计时器铃声失败: {ex.Message}");
            }
        }

        private void PlayProgressiveReminderSound()
        {
            try
            {
                double volume = MainWindow.Settings.RandSettings?.ProgressiveReminderVolume ?? 1.0;
                mediaPlayer.Volume = volume;

                if (!string.IsNullOrEmpty(MainWindow.Settings.RandSettings?.ProgressiveReminderSoundPath) &&
                    System.IO.File.Exists(MainWindow.Settings.RandSettings.ProgressiveReminderSoundPath))
                {
                    mediaPlayer.Open(new Uri(MainWindow.Settings.RandSettings.ProgressiveReminderSoundPath));
                }
                else
                {
                    string tempPath = System.IO.Path.GetTempFileName() + ".wav";
                    using (var stream = Properties.Resources.ProgressiveAudio)
                    {
                        using (var fileStream = new System.IO.FileStream(tempPath, System.IO.FileMode.Create))
                        {
                            stream.CopyTo(fileStream);
                        }
                    }
                    mediaPlayer.Open(new Uri(tempPath));
                }

                mediaPlayer.Play();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"播放渐进提醒音频失败: {ex.Message}");
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            StopTimer();
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void CommonTab_Click(object sender, RoutedEventArgs e)
        {
            UpdateActivityTime();
            CommonTimersGrid.Visibility = Visibility.Visible;
            RecentTimersGrid.Visibility = Visibility.Collapsed;

            // 更新字体粗细、透明度和颜色
            var commonText = this.FindName("CommonTabText") as TextBlock;
            var recentText = this.FindName("RecentTabText") as TextBlock;
            if (commonText != null)
            {
                commonText.FontWeight = FontWeights.Bold;
                commonText.Opacity = 1.0;
                commonText.Foreground = new SolidColorBrush(Colors.White);
            }
            if (recentText != null)
            {
                recentText.FontWeight = FontWeights.Normal;
                recentText.Opacity = 0.8;
                recentText.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102));
            }

            // 移动指示器到左侧
            var indicator = this.FindName("SegmentedIndicator") as Border;
            if (indicator != null)
            {
                // 设置左侧圆角
                indicator.CornerRadius = new CornerRadius(7.5, 0, 0, 7.5);
                var animation = new System.Windows.Media.Animation.ThicknessAnimation(
                    new Thickness(0, 0, 0, 0),
                    TimeSpan.FromMilliseconds(200));
                indicator.BeginAnimation(Border.MarginProperty, animation);
            }
        }

        private void RecentTab_Click(object sender, RoutedEventArgs e)
        {
            UpdateActivityTime();
            CommonTimersGrid.Visibility = Visibility.Collapsed;
            RecentTimersGrid.Visibility = Visibility.Visible;

            // 更新字体粗细、透明度和颜色
            var commonText = this.FindName("CommonTabText") as TextBlock;
            var recentText = this.FindName("RecentTabText") as TextBlock;
            if (commonText != null)
            {
                commonText.FontWeight = FontWeights.Normal;
                commonText.Opacity = 0.8;
                commonText.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102));
            }
            if (recentText != null)
            {
                recentText.FontWeight = FontWeights.Bold;
                recentText.Opacity = 1.0;
                recentText.Foreground = new SolidColorBrush(Colors.White);
            }

            // 移动指示器到右侧
            var indicator = this.FindName("SegmentedIndicator") as Border;
            if (indicator != null)
            {
                // 设置右侧圆角
                indicator.CornerRadius = new CornerRadius(0, 7.5, 7.5, 0);
                var animation = new System.Windows.Media.Animation.ThicknessAnimation(
                    new Thickness(118, 0, 0, 0),
                    TimeSpan.FromMilliseconds(200));
                indicator.BeginAnimation(Border.MarginProperty, animation);
            }
        }

        // 常用计时事件处理
        private void Common5Min_Click(object sender, RoutedEventArgs e)
        {
            if (isTimerRunning && !isPaused) return;
            UpdateActivityTime();
            SetQuickTime(0, 5, 0);
        }

        private void Common10Min_Click(object sender, RoutedEventArgs e)
        {
            if (isTimerRunning && !isPaused) return;
            UpdateActivityTime();
            SetQuickTime(0, 10, 0);
        }

        private void Common15Min_Click(object sender, RoutedEventArgs e)
        {
            if (isTimerRunning && !isPaused) return;
            UpdateActivityTime();
            SetQuickTime(0, 15, 0);
        }

        private void Common30Min_Click(object sender, RoutedEventArgs e)
        {
            if (isTimerRunning && !isPaused) return;
            UpdateActivityTime();
            SetQuickTime(0, 30, 0);
        }

        private void Common45Min_Click(object sender, RoutedEventArgs e)
        {
            if (isTimerRunning && !isPaused) return;
            UpdateActivityTime();
            SetQuickTime(0, 45, 0);
        }

        private void Common60Min_Click(object sender, RoutedEventArgs e)
        {
            if (isTimerRunning && !isPaused) return;
            UpdateActivityTime();
            SetQuickTime(1, 0, 0);
        }

        // 最近计时事件处理
        private void RecentTimer1_Click(object sender, RoutedEventArgs e)
        {
            if ((isTimerRunning && !isPaused) || recentTimer1 == "--:--") return;
            UpdateActivityTime();
            ApplyRecentTimer(recentTimer1);
        }

        private void RecentTimer2_Click(object sender, RoutedEventArgs e)
        {
            if ((isTimerRunning && !isPaused) || recentTimer2 == "--:--") return;
            UpdateActivityTime();
            ApplyRecentTimer(recentTimer2);
        }

        private void RecentTimer3_Click(object sender, RoutedEventArgs e)
        {
            if ((isTimerRunning && !isPaused) || recentTimer3 == "--:--") return;
            UpdateActivityTime();
            ApplyRecentTimer(recentTimer3);
        }

        private void RecentTimer4_Click(object sender, RoutedEventArgs e)
        {
            if ((isTimerRunning && !isPaused) || recentTimer4 == "--:--") return;
            UpdateActivityTime();
            ApplyRecentTimer(recentTimer4);
        }

        private void RecentTimer5_Click(object sender, RoutedEventArgs e)
        {
            if ((isTimerRunning && !isPaused) || recentTimer5 == "--:--") return;
            UpdateActivityTime();
            ApplyRecentTimer(recentTimer5);
        }

        private void RecentTimer6_Click(object sender, RoutedEventArgs e)
        {
            if ((isTimerRunning && !isPaused) || recentTimer6 == "--:--") return;
            UpdateActivityTime();
            ApplyRecentTimer(recentTimer6);
        }

        // 设置快捷时间
        private void SetQuickTime(int h, int m, int s)
        {
            hour = h;
            minute = m;
            second = s;
            UpdateDigitDisplays();
        }

        // 应用最近计时
        private void ApplyRecentTimer(string timeString)
        {
            if (timeString == "--:--") return;

            try
            {
                var parts = timeString.Split(':');
                if (parts.Length == 2)
                {
                    int minutes = int.Parse(parts[0]);
                    int seconds = int.Parse(parts[1]);
                    SetQuickTime(0, minutes, seconds);
                }
            }
            catch
            {
                // 如果解析失败，忽略
            }
        }

        // 保存最近计时记录
        private void SaveRecentTimer()
        {
            if (hour == 0 && minute == 0 && second == 0) return;

            string currentTime = $"{minute:D2}:{second:D2}";

            // 检查是否已存在相同的时间
            var existingIndex = -1;
            if (recentTimer1 == currentTime) existingIndex = 0;
            else if (recentTimer2 == currentTime) existingIndex = 1;
            else if (recentTimer3 == currentTime) existingIndex = 2;
            else if (recentTimer4 == currentTime) existingIndex = 3;
            else if (recentTimer5 == currentTime) existingIndex = 4;
            else if (recentTimer6 == currentTime) existingIndex = 5;

            if (existingIndex >= 0)
            {
                // 如果存在重复，将其移到最前面
                string duplicateTimer = GetRecentTimerByIndex(existingIndex);
                
                // 移除重复项
                RemoveRecentTimerByIndex(existingIndex);
                
                // 将重复项添加到最前面
                recentTimer6 = recentTimer5;
                recentTimer5 = recentTimer4;
                recentTimer4 = recentTimer3;
                recentTimer3 = recentTimer2;
                recentTimer2 = recentTimer1;
                recentTimer1 = duplicateTimer;
            }
            else
            {
                // 如果不存在重复，正常添加新记录
                recentTimer6 = recentTimer5;
                recentTimer5 = recentTimer4;
                recentTimer4 = recentTimer3;
                recentTimer3 = recentTimer2;
                recentTimer2 = recentTimer1;
                recentTimer1 = currentTime;
            }

            UpdateRecentTimerDisplays();
            SaveRecentTimersToRegistry();
        }

        private string GetRecentTimerByIndex(int index)
        {
            switch (index)
            {
                case 0: return recentTimer1;
                case 1: return recentTimer2;
                case 2: return recentTimer3;
                case 3: return recentTimer4;
                case 4: return recentTimer5;
                case 5: return recentTimer6;
                default: return "";
            }
        }

        private void RemoveRecentTimerByIndex(int index)
        {
            switch (index)
            {
                case 0:
                    recentTimer1 = recentTimer2;
                    recentTimer2 = recentTimer3;
                    recentTimer3 = recentTimer4;
                    recentTimer4 = recentTimer5;
                    recentTimer5 = recentTimer6;
                    recentTimer6 = "--:--";
                    break;
                case 1:
                    recentTimer2 = recentTimer3;
                    recentTimer3 = recentTimer4;
                    recentTimer4 = recentTimer5;
                    recentTimer5 = recentTimer6;
                    recentTimer6 = "--:--";
                    break;
                case 2:
                    recentTimer3 = recentTimer4;
                    recentTimer4 = recentTimer5;
                    recentTimer5 = recentTimer6;
                    recentTimer6 = "--:--";
                    break;
                case 3:
                    recentTimer4 = recentTimer5;
                    recentTimer5 = recentTimer6;
                    recentTimer6 = "--:--";
                    break;
                case 4:
                    recentTimer5 = recentTimer6;
                    recentTimer6 = "--:--";
                    break;
                case 5:
                    recentTimer6 = "--:--";
                    break;
            }
        }

        // 更新最近计时显示
        private void UpdateRecentTimerDisplays()
        {
            try
            {
                var timer1Text = this.FindName("RecentTimer1Text") as TextBlock;
                var timer2Text = this.FindName("RecentTimer2Text") as TextBlock;
                var timer3Text = this.FindName("RecentTimer3Text") as TextBlock;
                var timer4Text = this.FindName("RecentTimer4Text") as TextBlock;
                var timer5Text = this.FindName("RecentTimer5Text") as TextBlock;
                var timer6Text = this.FindName("RecentTimer6Text") as TextBlock;

                if (timer1Text != null) timer1Text.Text = recentTimer1;
                if (timer2Text != null) timer2Text.Text = recentTimer2;
                if (timer3Text != null) timer3Text.Text = recentTimer3;
                if (timer4Text != null) timer4Text.Text = recentTimer4;
                if (timer5Text != null) timer5Text.Text = recentTimer5;
                if (timer6Text != null) timer6Text.Text = recentTimer6;
            }
            catch
            {
            }
        }

        // 从JSON文件加载最近计时记录
        private void LoadRecentTimers()
        {
            try
            {
                // 确保Configs文件夹存在
                if (!Directory.Exists(ConfigsFolder))
                {
                    Directory.CreateDirectory(ConfigsFolder);
                }

                if (!File.Exists(RecentTimersJsonPath))
                {
                    recentTimer1 = "--:--";
                    recentTimer2 = "--:--";
                    recentTimer3 = "--:--";
                    recentTimer4 = "--:--";
                    recentTimer5 = "--:--";
                    recentTimer6 = "--:--";
                    return;
                }

                // 读取JSON文件
                string jsonContent = File.ReadAllText(RecentTimersJsonPath);
                var data = JsonConvert.DeserializeObject<RecentTimersData>(jsonContent);

                if (data != null)
                {
                    recentTimer1 = data.RecentTimer1 ?? "--:--";
                    recentTimer2 = data.RecentTimer2 ?? "--:--";
                    recentTimer3 = data.RecentTimer3 ?? "--:--";
                    recentTimer4 = data.RecentTimer4 ?? "--:--";
                    recentTimer5 = data.RecentTimer5 ?? "--:--";
                    recentTimer6 = data.RecentTimer6 ?? "--:--";
                }
                else
                {
                    recentTimer1 = "--:--";
                    recentTimer2 = "--:--";
                    recentTimer3 = "--:--";
                    recentTimer4 = "--:--";
                    recentTimer5 = "--:--";
                    recentTimer6 = "--:--";
                }
            }
            catch (Exception ex)
            {
                recentTimer1 = "--:--";
                recentTimer2 = "--:--";
                recentTimer3 = "--:--";
                recentTimer4 = "--:--";
                recentTimer5 = "--:--";
                recentTimer6 = "--:--";
            }
        }

        // 保存最近计时记录到JSON文件
        private void SaveRecentTimersToRegistry()
        {
            try
            {
                // 确保Configs文件夹存在
                if (!Directory.Exists(ConfigsFolder))
                {
                    Directory.CreateDirectory(ConfigsFolder);
                }

                // 创建数据对象
                var data = new RecentTimersData
                {
                    RecentTimer1 = recentTimer1,
                    RecentTimer2 = recentTimer2,
                    RecentTimer3 = recentTimer3,
                    RecentTimer4 = recentTimer4,
                    RecentTimer5 = recentTimer5,
                    RecentTimer6 = recentTimer6
                };

                // 序列化为JSON并保存到文件
                string jsonContent = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(RecentTimersJsonPath, jsonContent);
            }
            catch (Exception)
            {
            }
        }

        // 设置深色主题下的灰色边框
        private void SetDarkThemeBorder()
        {
            try
            {
                if (MainBorder != null)
                {
                    MainBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(64, 64, 64));
                }
            }
            catch
            {
            }
        }

        private FullscreenTimerWindow fullscreenWindow;
        
        public bool IsFullscreenWindowOpen => fullscreenWindow != null && fullscreenWindow.IsVisible;
        
        private void Fullscreen_Click(object sender, RoutedEventArgs e)
        {
            if (fullscreenWindow != null && fullscreenWindow.IsVisible)
            {
                fullscreenWindow.Close();
                fullscreenWindow = null;
                return;
            }
            
            if (isTimerRunning && !isPaused)
            {
                fullscreenWindow = new FullscreenTimerWindow(this);
                fullscreenWindow.Closed += (s, args) => { fullscreenWindow = null; };
                fullscreenWindow.Show();
                HideMinimizedRequested?.Invoke(this, EventArgs.Empty);
            }
        }
        
        private void MainBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            UpdateActivityTime();
            if (e.ClickCount == 1)
            {
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    var timerContainer = mainWindow.FindName("TimerContainer") as FrameworkElement;
                    if (timerContainer != null)
                    {
                        var point = e.GetPosition(timerContainer);
                        var mainWindowPoint = timerContainer.TransformToAncestor(mainWindow).Transform(point);
                        DragTimerContainer(mainWindow, mainWindowPoint, e);
                    }
                }
            }
        }
        
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            UpdateActivityTime();
            if (e.ClickCount == 1)
            {
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    var timerContainer = mainWindow.FindName("TimerContainer") as FrameworkElement;
                    if (timerContainer != null)
                    {
                        var point = e.GetPosition(timerContainer);
                        var mainWindowPoint = timerContainer.TransformToAncestor(mainWindow).Transform(point);
                        DragTimerContainer(mainWindow, mainWindowPoint, e);
                    }
                }
            }
        }
        
        private bool isDragging = false;
        private Point dragStartPoint;
        private Point containerStartPosition;
        
        private void DragTimerContainer(MainWindow mainWindow, Point startPoint, MouseButtonEventArgs e)
        {
            var timerContainer = mainWindow.FindName("TimerContainer") as FrameworkElement;
            if (timerContainer == null) return;
            
            isDragging = true;
            dragStartPoint = startPoint;
            
            if (timerContainer.HorizontalAlignment == HorizontalAlignment.Center || 
                timerContainer.VerticalAlignment == VerticalAlignment.Center)
            {
                var timerPoint = timerContainer.TransformToAncestor(mainWindow).Transform(new Point(0, 0));
                containerStartPosition = new Point(timerPoint.X, timerPoint.Y);
                
                timerContainer.Margin = new Thickness(containerStartPosition.X, containerStartPosition.Y, 0, 0);
                timerContainer.HorizontalAlignment = HorizontalAlignment.Left;
                timerContainer.VerticalAlignment = VerticalAlignment.Top;
            }
            else
            {
                var margin = timerContainer.Margin;
                containerStartPosition = new Point(margin.Left, margin.Top);
                
                if (double.IsNaN(containerStartPosition.X) || containerStartPosition.X < 0) containerStartPosition.X = 0;
                if (double.IsNaN(containerStartPosition.Y) || containerStartPosition.Y < 0) containerStartPosition.Y = 0;
            }
            
            timerContainer.CaptureMouse();
            timerContainer.MouseMove += TimerContainer_MouseMove;
            timerContainer.MouseLeftButtonUp += TimerContainer_MouseLeftButtonUp;
            e.Handled = true;
        }
        
        private void TimerContainer_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isDragging) return;
            
            UpdateActivityTime();
            
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow == null) return;
            
            var timerContainer = mainWindow.FindName("TimerContainer") as FrameworkElement;
            var minimizedContainer = mainWindow.FindName("MinimizedTimerContainer") as FrameworkElement;
            if (timerContainer == null) return;
            
            var currentPoint = e.GetPosition(mainWindow);
            var deltaX = currentPoint.X - dragStartPoint.X;
            var deltaY = currentPoint.Y - dragStartPoint.Y;
            
            var newX = containerStartPosition.X + deltaX;
            var newY = containerStartPosition.Y + deltaY;
            
            if (newX < 0) newX = 0;
            if (newY < 0) newY = 0;
            
            timerContainer.Margin = new Thickness(newX, newY, 0, 0);
            timerContainer.HorizontalAlignment = HorizontalAlignment.Left;
            timerContainer.VerticalAlignment = VerticalAlignment.Top;
            
            if (minimizedContainer != null && minimizedContainer.Visibility == Visibility.Visible)
            {
                minimizedContainer.Margin = new Thickness(newX, newY, 0, 0);
                minimizedContainer.HorizontalAlignment = HorizontalAlignment.Left;
                minimizedContainer.VerticalAlignment = VerticalAlignment.Top;
            }
        }
        
        private void TimerContainer_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!isDragging) return;
            
            isDragging = false;
            
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow == null) return;
            
            var timerContainer = mainWindow.FindName("TimerContainer") as FrameworkElement;
            if (timerContainer != null)
            {
                timerContainer.ReleaseMouseCapture();
                timerContainer.MouseMove -= TimerContainer_MouseMove;
                timerContainer.MouseLeftButtonUp -= TimerContainer_MouseLeftButtonUp;
            }
        }
        
        private void HandleTimerCompletion()
        {
            // 计时器结束时，如果显示的是最小化视图，恢复到主窗口视图
            Application.Current.Dispatcher.Invoke(() =>
            {
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    var timerContainer = mainWindow.FindName("TimerContainer") as FrameworkElement;
                    var minimizedContainer = mainWindow.FindName("MinimizedTimerContainer") as FrameworkElement;
                    
                    // 如果最小化视图可见，恢复到主窗口视图
                    if (minimizedContainer != null && minimizedContainer.Visibility == Visibility.Visible)
                    {
                        HideMinimizedRequested?.Invoke(this, EventArgs.Empty);
                    }
                }
            });
            
            // 重置计时器状态
            ResetTimerState();
        }
        
        /// <summary>
        /// 重置计时器状态
        /// </summary>
        public void ResetTimerState()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 停止计时器
                if (isTimerRunning)
                {
                    timer.Stop();
                    isTimerRunning = false;
                    isPaused = false;
                    
                    if (hideTimer != null)
                    {
                        hideTimer.Stop();
                    }
                }
                
                // 重置时间到默认值
                hour = 0;
                minute = 5;
                second = 0;
                
                // 更新显示
                UpdateDigitDisplays();
                SetColonDisplay(false);
                
                // 重置图标
                if (StartPauseIcon != null)
                {
                    StartPauseIcon.Data = Geometry.Parse(PlayIconData);
                }
                
                // 重置状态标志
                isOvertimeMode = false;
                hasPlayedProgressiveReminder = false;
                
                // 禁用全屏按钮
                if (FullscreenBtn != null)
                {
                    FullscreenBtn.IsEnabled = false;
                }
            });
        }
        
        private void HideTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (!isTimerRunning || isPaused) return;
            
            Application.Current.Dispatcher.Invoke(() =>
            {
                var timeSinceLastActivity = DateTime.Now - lastActivityTime;
                
                if (timeSinceLastActivity.TotalSeconds >= 5)
                {
                    var mainWindow = Application.Current.MainWindow as MainWindow;
                    if (mainWindow != null)
                    {
                        var timerContainer = mainWindow.FindName("TimerContainer") as FrameworkElement;
                        if (timerContainer != null && timerContainer.Visibility == Visibility.Visible)
                        {
                            ShowMinimizedRequested?.Invoke(this, EventArgs.Empty);
                        }
                    }
                }
            });
        }
        
        public void UpdateActivityTime()
        {
            lastActivityTime = DateTime.Now;
            
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                var timerContainer = mainWindow.FindName("TimerContainer") as FrameworkElement;
                var minimizedContainer = mainWindow.FindName("MinimizedTimerContainer") as FrameworkElement;
                
                if (timerContainer != null && minimizedContainer != null)
                {
                    if (timerContainer.Visibility == Visibility.Collapsed && minimizedContainer.Visibility == Visibility.Visible)
                    {
                        HideMinimizedRequested?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
        }

    }
}
