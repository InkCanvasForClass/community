using Ink_Canvas.Helpers;
using Microsoft.Win32;
using System;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Ink_Canvas.Windows
{
    /// <summary>
    /// PPT时间显示胶囊控件
    /// </summary>
    public partial class PPTTimeCapsule : UserControl, IDisposable
    {
        private System.Timers.Timer timeUpdateTimer;
        private System.Timers.Timer countdownUpdateTimer;
        private DateTime lastTime = DateTime.MinValue;
        private TimerControl parentControl;
        private bool wasTimerRunning = false;
        private bool isOvertime = false;
        private Storyboard capsuleExpandStoryboard;
        private Storyboard capsuleShrinkStoryboard;
        private Storyboard colonBlinkStoryboard;
        private double originalCapsuleWidth = 0;
        private string lastCountdownText = ""; // 上次的倒计时文本，用于检测文本变化
        private Storyboard currentWidthAnimation; // 当前正在运行的宽度动画

        public PPTTimeCapsule()
        {
            InitializeComponent();
            InitializeTimers();
            ApplyTheme();

            // 监听主题变化
            SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;

            Loaded += PPTTimeCapsule_Loaded;
            Unloaded += PPTTimeCapsule_Unloaded;
            IsVisibleChanged += PPTTimeCapsule_IsVisibleChanged;
        }

        private void PPTTimeCapsule_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (Visibility == Visibility.Visible)
            {
                ApplyTheme();
            }
        }

        private void PPTTimeCapsule_Loaded(object sender, RoutedEventArgs e)
        {
            // 记录初始宽度
            if (MainCapsule != null && originalCapsuleWidth == 0)
            {
                originalCapsuleWidth = MainCapsule.ActualWidth > 0 ? MainCapsule.ActualWidth : 120;
            }
            UpdateTimeDisplay();
            StartTimeUpdate();
        }

        private void PPTTimeCapsule_Unloaded(object sender, RoutedEventArgs e)
        {
            Dispose();
        }

        /// <summary>
        /// 实现 IDisposable
        /// </summary>
        public void Dispose()
        {
            StopTimeUpdate();
            SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
            timeUpdateTimer?.Dispose();
            countdownUpdateTimer?.Dispose();
        }

        private void InitializeTimers()
        {
            // 时间更新定时器（每秒更新）
            timeUpdateTimer = new System.Timers.Timer(1000);
            timeUpdateTimer.Elapsed += TimeUpdateTimer_Elapsed;

            // 倒计时更新定时器
            countdownUpdateTimer = new System.Timers.Timer(250);
            countdownUpdateTimer.Elapsed += CountdownUpdateTimer_Elapsed;
        }

        private void TimeUpdateTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateTimeDisplay();
            }), DispatcherPriority.Normal);
        }

        private void CountdownUpdateTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (this.Visibility != Visibility.Visible)
                {
                    return;
                }

                UpdateCountdownDisplay();
            }), DispatcherPriority.Normal);
        }

        private void StartTimeUpdate()
        {
            if (timeUpdateTimer != null && !timeUpdateTimer.Enabled)
            {
                timeUpdateTimer.Start();
            }
            if (countdownUpdateTimer != null && !countdownUpdateTimer.Enabled)
            {
                countdownUpdateTimer.Start();
            }
            // 启动冒号闪动动画
            StartColonBlinkAnimation();
        }

        private void StopTimeUpdate()
        {
            if (timeUpdateTimer != null)
            {
                timeUpdateTimer.Stop();
            }
            if (countdownUpdateTimer != null)
            {
                countdownUpdateTimer.Stop();
            }
            // 停止冒号闪动动画
            StopColonBlinkAnimation();
        }

        /// <summary>
        /// 设置父计时器控件
        /// </summary>
        public void SetParentControl(TimerControl parent)
        {
            parentControl = parent;
            if (parentControl != null)
            {
                UpdateCountdownDisplay();
            }
        }

        private void UpdateTimeDisplay()
        {
            DateTime now = DateTime.Now;

            // 检查小时是否改变
            if (lastTime != DateTime.MinValue && lastTime.Hour != now.Hour)
            {
                // 先更新数字内容
                SetDigitDisplay("Hour1Display", now.Hour / 10);
                SetDigitDisplay("Hour2Display", now.Hour % 10);

                // 重置Transform位置到上方
                HourContentTransform.Y = -40;
                HourPanel.Opacity = 0;

                // 播放小时滚动动画：从上方滚入
                PlayHourScrollAnimation();
            }
            else if (lastTime == DateTime.MinValue)
            {
                // 首次加载，直接更新显示
                SetDigitDisplay("Hour1Display", now.Hour / 10);
                SetDigitDisplay("Hour2Display", now.Hour % 10);
            }

            // 检查分钟是否改变
            if (lastTime != DateTime.MinValue && lastTime.Minute != now.Minute)
            {
                // 先更新数字内容
                SetDigitDisplay("Minute1Display", now.Minute / 10);
                SetDigitDisplay("Minute2Display", now.Minute % 10);

                // 重置Transform位置到上方
                MinuteContentTransform.Y = -40;
                MinutePanel.Opacity = 0;

                // 播放分钟滚动动画：从上方滚入
                PlayMinuteScrollAnimation();
            }
            else if (lastTime == DateTime.MinValue)
            {
                // 首次加载，直接更新显示
                SetDigitDisplay("Minute1Display", now.Minute / 10);
                SetDigitDisplay("Minute2Display", now.Minute % 10);
            }

            lastTime = now;
        }

        private void PlayHourScrollAnimation()
        {
            // 新时间从上方滚入（-25到0）
            var scrollAnimation = (Storyboard)Resources["HourScrollAnimation"];
            scrollAnimation.Begin();
        }

        private void PlayMinuteScrollAnimation()
        {
            // 新时间从上方滚入（-25到0）
            var scrollAnimation = (Storyboard)Resources["MinuteScrollAnimation"];
            scrollAnimation.Begin();
        }

        /// <summary>
        /// 根据数字值设置SVG数字显示
        /// </summary>
        /// <param name="pathName">Path控件的名称</param>
        /// <param name="digit">要显示的数字(0-9)</param>
        private void SetDigitDisplay(string pathName, int digit)
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
            }
        }

        /// <summary>
        /// 设置SVG数字的填充颜色
        /// </summary>
        /// <param name="pathName">Path控件的名称</param>
        /// <param name="color">填充颜色</param>
        private void SetDigitFill(string pathName, Color color)
        {
            var path = this.FindName(pathName) as System.Windows.Shapes.Path;
            if (path != null)
            {
                path.Fill = new SolidColorBrush(color);
            }
        }

        private void StartColonBlinkAnimation()
        {
            try
            {
                if (colonBlinkStoryboard == null)
                {
                    colonBlinkStoryboard = (Storyboard)Resources["ColonBlinkAnimation"];
                }
                if (colonBlinkStoryboard != null)
                {
                    colonBlinkStoryboard.Begin(ColonDisplay, true); // true表示HandoffBehavior.SnapshotAndReplace
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"启动冒号闪动动画失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void StopColonBlinkAnimation()
        {
            try
            {
                if (colonBlinkStoryboard != null)
                {
                    colonBlinkStoryboard.Stop(ColonDisplay);
                    // 恢复冒号透明度
                    if (ColonDisplay != null)
                    {
                        ColonDisplay.Opacity = 1.0;
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"停止冒号闪动动画失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }


        /// <summary>
        /// 停止倒计时
        /// </summary>
        public void StopCountdown()
        {
            bool wasRunning = wasTimerRunning;
            wasTimerRunning = false;
            CountdownPanel.Visibility = Visibility.Collapsed;

            // 重置超时状态
            isOvertime = false;
            // 根据主题恢复倒计时文本颜色
            ApplyTheme();

            // 播放胶囊缩短动画
            if (wasRunning)
            {
                PlayCapsuleShrinkAnimation();
            }
        }

        private void UpdateCountdownDisplay()
        {
            if (parentControl == null) return;

            // 检查计时器是否正在运行
            bool isRunning = parentControl.IsTimerRunning;

            // 如果状态改变，更新UI
            if (isRunning != wasTimerRunning)
            {
                wasTimerRunning = isRunning;
                if (isRunning)
                {
                    // 计时器开始运行，显示倒计时面板并播放伸长动画
                    CountdownPanel.Visibility = Visibility.Visible;
                    // 确保倒计时文本使用主题颜色
                    ApplyTheme();
                    UpdateCountdownText();
                    PlayCapsuleExpandAnimation();
                }
                else
                {
                    // 计时器停止，隐藏倒计时面板并播放缩短动画
                    CountdownPanel.Visibility = Visibility.Collapsed;
                    PlayCapsuleShrinkAnimation();
                    isOvertime = false;
                    // 根据主题恢复倒计时文本颜色
                    ApplyTheme();
                    return;
                }
            }

            // 如果计时器未运行，不更新显示
            if (!isRunning)
            {
                return;
            }

            // 直接从parentControl获取剩余时间
            var remainingTime = parentControl.GetRemainingTime();
            if (!remainingTime.HasValue)
            {
                // 如果无法获取剩余时间（可能是暂停状态），不更新显示
                return;
            }

            var timeSpan = remainingTime.Value;
            bool isOvertimeMode = timeSpan.TotalSeconds < 0;

            // 更新倒计时文本
            UpdateCountdownText(timeSpan, isOvertimeMode);

            // 根据文本内容动态调整胶囊宽度
            AdjustCapsuleWidthToContent();
        }

        /// <summary>
        /// 更新倒计时文本内容
        /// </summary>
        private void UpdateCountdownText(TimeSpan? timeSpan = null, bool? isOvertimeMode = null)
        {
            if (CountdownText == null || parentControl == null) return;

            if (!timeSpan.HasValue)
            {
                var remainingTime = parentControl.GetRemainingTime();
                if (!remainingTime.HasValue) return;
                timeSpan = remainingTime.Value;
            }

            if (!isOvertimeMode.HasValue)
            {
                isOvertimeMode = timeSpan.Value.TotalSeconds < 0;
            }

            // 处理超时状态
            if (isOvertimeMode.Value)
            {
                if (!isOvertime)
                {
                    isOvertime = true;
                    OnTimerOvertime();
                }

                // 确保倒计时文本为红色（如果启用了超时红色文本设置）
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow != null && MainWindow.Settings.RandSettings?.EnableOvertimeRedText == true)
                {
                    CountdownText.Foreground = new SolidColorBrush(Colors.Red);
                }

                // 显示超时时间
                var overtimeSpan = -timeSpan.Value;
                if (overtimeSpan.TotalHours >= 1)
                {
                    int hours = (int)overtimeSpan.TotalHours;
                    CountdownText.Text = $"{hours:D2}:{overtimeSpan.Minutes:D2}:{overtimeSpan.Seconds:D2}";
                }
                else
                {
                    CountdownText.Text = $"{overtimeSpan.Minutes:D2}:{overtimeSpan.Seconds:D2}";
                }
            }
            else
            {
                // 正常倒计时
                if (isOvertime)
                {
                    // 从超时状态恢复
                    isOvertime = false;
                    // 根据主题恢复倒计时文本颜色（如果未启用超时红色文本）
                    var mainWindow = Application.Current.MainWindow as MainWindow;
                    if (mainWindow == null || MainWindow.Settings.RandSettings?.EnableOvertimeRedText != true)
                    {
                        ApplyTheme();
                    }
                }
                else
                {
                    // 确保正常倒计时时使用主题颜色（如果未启用超时红色文本）
                    var mainWindow = Application.Current.MainWindow as MainWindow;
                    if (mainWindow == null || MainWindow.Settings.RandSettings?.EnableOvertimeRedText != true)
                    {
                        // 检查当前颜色是否是红色，如果不是红色，则应用主题
                        if (CountdownText.Foreground is SolidColorBrush brush && brush.Color != Colors.Red)
                        {
                            ApplyTheme();
                        }
                    }
                }

                if (timeSpan.Value.TotalHours >= 1)
                {
                    int hours = (int)timeSpan.Value.TotalHours;
                    CountdownText.Text = $"{hours:D2}:{timeSpan.Value.Minutes:D2}:{timeSpan.Value.Seconds:D2}";
                }
                else
                {
                    CountdownText.Text = $"{timeSpan.Value.Minutes:D2}:{timeSpan.Value.Seconds:D2}";
                }
            }
        }

        /// <summary>
        /// 根据内容动态调整胶囊宽度
        /// </summary>
        private void AdjustCapsuleWidthToContent()
        {
            if (MainCapsule == null || !wasTimerRunning || CountdownText == null) return;

            try
            {
                // 检查文本是否改变（格式变化可能导致宽度变化）
                string currentText = CountdownText.Text;
                if (currentText == lastCountdownText)
                {
                    // 文本未改变，不需要调整
                    return;
                }

                lastCountdownText = currentText;

                double currentWidth = MainCapsule.ActualWidth;
                double targetWidth = CalculateTargetWidth();

                // 如果当前宽度与目标宽度差异较大（超过2像素），则调整
                if (Math.Abs(currentWidth - targetWidth) > 2)
                {
                    // 停止当前动画
                    if (currentWidthAnimation != null)
                    {
                        currentWidthAnimation.Stop();
                        currentWidthAnimation = null;
                    }
                    if (capsuleExpandStoryboard != null)
                    {
                        capsuleExpandStoryboard.Stop();
                    }
                    if (capsuleShrinkStoryboard != null)
                    {
                        capsuleShrinkStoryboard.Stop();
                    }

                    // 创建新的动画
                    var animation = new DoubleAnimation
                    {
                        From = currentWidth,
                        To = targetWidth,
                        Duration = new Duration(TimeSpan.FromMilliseconds(targetWidth > currentWidth ? 600 : 800)),
                        EasingFunction = targetWidth > currentWidth
                            ? new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 }
                            : new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.2 }
                    };

                    var storyboard = new Storyboard();
                    Storyboard.SetTarget(animation, MainCapsule);
                    Storyboard.SetTargetProperty(animation, new PropertyPath(FrameworkElement.WidthProperty));
                    storyboard.Children.Add(animation);
                    currentWidthAnimation = storyboard;
                    storyboard.Begin();

                    storyboard.Completed += (o, args) =>
                    {
                        storyboard.Remove();
                        if (currentWidthAnimation == storyboard)
                        {
                            currentWidthAnimation = null;
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"调整胶囊宽度失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void PlayCapsuleExpandAnimation()
        {
            try
            {
                if (MainCapsule != null)
                {
                    // 记录原始宽度
                    if (originalCapsuleWidth == 0)
                    {
                        originalCapsuleWidth = MainCapsule.ActualWidth > 0 ? MainCapsule.ActualWidth : 120;
                    }

                    // 停止当前动画
                    if (currentWidthAnimation != null)
                    {
                        currentWidthAnimation.Stop();
                        currentWidthAnimation = null;
                    }

                    // 根据倒计时文本的实际宽度动态计算目标宽度
                    double targetWidth = CalculateTargetWidth();

                    if (capsuleExpandStoryboard == null)
                    {
                        capsuleExpandStoryboard = (Storyboard)Resources["CapsuleExpandAnimation"];
                    }

                    if (capsuleExpandStoryboard != null)
                    {
                        // 设置动画的目标值
                        var animation = capsuleExpandStoryboard.Children[0] as DoubleAnimation;
                        if (animation != null)
                        {
                            animation.From = MainCapsule.ActualWidth;
                            animation.To = targetWidth;
                        }

                        capsuleExpandStoryboard.Begin();
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"播放胶囊伸长动画失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 根据倒计时文本的实际宽度计算目标宽度
        /// </summary>
        private double CalculateTargetWidth()
        {
            if (CountdownText == null || TimeDisplayPanel == null)
            {
                // 如果倒计时文本不可用，使用估算值
                return originalCapsuleWidth + 80;
            }

            // 测量倒计时文本的实际宽度
            CountdownText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double countdownTextWidth = CountdownText.DesiredSize.Width;

            // 测量时间显示面板的宽度
            TimeDisplayPanel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double timeDisplayWidth = TimeDisplayPanel.DesiredSize.Width;

            double targetWidth = timeDisplayWidth + countdownTextWidth + 8 + 32;

            // 确保最小宽度不小于原始宽度
            if (targetWidth < originalCapsuleWidth)
            {
                targetWidth = originalCapsuleWidth;
            }

            return targetWidth;
        }

        private void PlayCapsuleShrinkAnimation()
        {
            try
            {
                if (MainCapsule != null && originalCapsuleWidth > 0)
                {
                    // 停止当前动画
                    if (currentWidthAnimation != null)
                    {
                        currentWidthAnimation.Stop();
                        currentWidthAnimation = null;
                    }

                    // 重置文本跟踪
                    lastCountdownText = "";

                    if (capsuleShrinkStoryboard == null)
                    {
                        capsuleShrinkStoryboard = (Storyboard)Resources["CapsuleShrinkAnimation"];
                    }

                    if (capsuleShrinkStoryboard != null)
                    {
                        // 设置动画的目标值
                        var animation = capsuleShrinkStoryboard.Children[0] as DoubleAnimation;
                        if (animation != null)
                        {
                            animation.From = MainCapsule.ActualWidth;
                            animation.To = originalCapsuleWidth;
                        }

                        capsuleShrinkStoryboard.Begin();
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"播放胶囊缩短动画失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void OnTimerOvertime()
        {
            // 改变倒计时文字颜色为红色
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow != null && MainWindow.Settings.RandSettings?.EnableOvertimeRedText == true)
            {
                CountdownText.Foreground = new SolidColorBrush(Colors.Red);
            }
        }

        /// <summary>
        /// 处理计时器完成事件
        /// </summary>
        public void OnTimerCompleted()
        {
            // 确保在UI线程上执行
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => OnTimerCompleted()), DispatcherPriority.Normal);
                return;
            }

            // 停止倒计时
            StopCountdown();
        }

        private void MainCapsule_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 点击恢复主计时器窗口（不重置计时器）
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                mainWindow.AdjustTimerContainerSize();

                // 显示主计时器窗口
                var timerContainer = mainWindow.FindName("TimerContainer") as FrameworkElement;
                if (timerContainer != null)
                {
                    timerContainer.Visibility = Visibility.Visible;
                }

                // 隐藏最小化计时器容器
                var minimizedContainer = mainWindow.FindName("MinimizedTimerContainer") as FrameworkElement;
                if (minimizedContainer != null)
                {
                    minimizedContainer.Visibility = Visibility.Collapsed;
                }

                if (mainWindow.TimerControl != null)
                {
                    mainWindow.TimerControl.UpdateActivityTime();

                    mainWindow.TimerControl.CloseRequested -= TimerControl_CloseRequested;
                    mainWindow.TimerControl.CloseRequested += TimerControl_CloseRequested;
                }
            }
        }

        private void TimerControl_CloseRequested(object sender, EventArgs e)
        {
            // 当计时器窗口关闭时，隐藏TimerContainer
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                var timerContainer = mainWindow.FindName("TimerContainer") as FrameworkElement;
                if (timerContainer != null)
                {
                    timerContainer.Visibility = Visibility.Collapsed;
                }

                var minimizedContainer = mainWindow.FindName("MinimizedTimerContainer") as FrameworkElement;
                if (minimizedContainer != null)
                {
                    minimizedContainer.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                ApplyTheme();
            }), DispatcherPriority.Normal);
        }

        private void ApplyTheme()
        {
            try
            {
                if (MainWindow.Settings != null)
                {
                    ApplyTheme(MainWindow.Settings);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用PPT时间胶囊主题失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void ApplyTheme(Settings settings)
        {
            try
            {
                bool isDarkTheme = false;

                if (settings.Appearance.Theme == 0) // 浅色主题
                {
                    isDarkTheme = false;
                }
                else if (settings.Appearance.Theme == 1) // 深色主题
                {
                    isDarkTheme = true;
                }
                else // 跟随系统主题
                {
                    bool isSystemLight = IsSystemThemeLight();
                    isDarkTheme = !isSystemLight;
                }

                if (isDarkTheme)
                {
                    // 深色主题：使用80%不透明度的深色背景
                    CapsuleBackgroundBrush.Color = Color.FromArgb(204, 32, 32, 32); // #CC202020，约80%不透明度
                    SetDigitFill("Hour1Display", Colors.White);
                    SetDigitFill("Hour2Display", Colors.White);
                    SetDigitFill("Minute1Display", Colors.White);
                    SetDigitFill("Minute2Display", Colors.White);
                    ColonDisplay.Foreground = new SolidColorBrush(Colors.White);
                    CountdownText.Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255));
                }
                else
                {
                    // 浅色主题：使用80%不透明度的白色背景
                    CapsuleBackgroundBrush.Color = Color.FromArgb(204, 255, 255, 255); // #CCFFFFFF，约80%不透明度
                    SetDigitFill("Hour1Display", Colors.Black);
                    SetDigitFill("Hour2Display", Colors.Black);
                    SetDigitFill("Minute1Display", Colors.Black);
                    SetDigitFill("Minute2Display", Colors.Black);
                    ColonDisplay.Foreground = new SolidColorBrush(Colors.Black);
                    CountdownText.Foreground = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0));
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用PPT时间胶囊主题失败: {ex.Message}", LogHelper.LogType.Error);
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

        /// <summary>
        /// 获取当前倒计时状态
        /// </summary>
        public bool IsCountdownRunning => parentControl != null && parentControl.IsTimerRunning;

        /// <summary>
        /// 获取是否超时
        /// </summary>
        public bool IsOvertime => isOvertime;
    }
}

