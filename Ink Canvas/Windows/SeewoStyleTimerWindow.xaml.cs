using Ink_Canvas.Helpers;
using System;
using System.Media;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace Ink_Canvas
{
    /// <summary>
    /// 仿希沃风格的倒计时器窗口
    /// </summary>
    public partial class SeewoStyleTimerWindow : Window
    {
        public SeewoStyleTimerWindow()
        {
            InitializeComponent();
            AnimationsHelper.ShowWithSlideFromBottomAndFade(this, 0.25);

            timer.Elapsed += Timer_Elapsed;
            timer.Interval = 50;
            InitializeUI();
            
            // 应用主题
            ApplyTheme();
        }


        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (!isTimerRunning || isPaused)
            {
                timer.Stop();
                return;
            }

            TimeSpan timeSpan = DateTime.Now - startTime;
            TimeSpan totalTimeSpan = new TimeSpan(hour, minute, second);
            TimeSpan leftTimeSpan = totalTimeSpan - timeSpan;
            if (leftTimeSpan.Milliseconds > 0) leftTimeSpan += new TimeSpan(0, 0, 1);

            Application.Current.Dispatcher.Invoke(() =>
            {
                int totalHours = (int)leftTimeSpan.TotalHours;
                int displayHours = totalHours;
                
                if (displayHours > 99) displayHours = 99;
                
                Digit1Display.Text = (displayHours / 10).ToString();
                Digit2Display.Text = (displayHours % 10).ToString();
                Digit3Display.Text = (leftTimeSpan.Minutes / 10).ToString();
                Digit4Display.Text = (leftTimeSpan.Minutes % 10).ToString();
                Digit5Display.Text = (leftTimeSpan.Seconds / 10).ToString();
                Digit6Display.Text = (leftTimeSpan.Seconds % 10).ToString();

                if (leftTimeSpan.TotalSeconds <= 0)
                {
                    Digit1Display.Text = "0";
                    Digit2Display.Text = "0";
                    Digit3Display.Text = "0";
                    Digit4Display.Text = "0";
                    Digit5Display.Text = "0";
                    Digit6Display.Text = "0";
                    timer.Stop();
                    isTimerRunning = false;
                    StartPauseIcon.Data = Geometry.Parse(PlayIconData);
                    PlayTimerSound();
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

        Timer timer = new Timer();

        private void InitializeUI()
        {
            UpdateDigitDisplays();
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
                LogHelper.WriteLogToFile($"应用仿希沃倒计时窗口主题出错: {ex.Message}", LogHelper.LogType.Error);
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
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用仿希沃倒计时窗口主题出错: {ex.Message}", LogHelper.LogType.Error);
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
            Digit1Display.Text = (hour / 10).ToString();
            Digit2Display.Text = (hour % 10).ToString();
            Digit3Display.Text = (minute / 10).ToString();
            Digit4Display.Text = (minute % 10).ToString();
            Digit5Display.Text = (second / 10).ToString();
            Digit6Display.Text = (second % 10).ToString();
        }

        // 第1位数字（小时十位）
        private void Digit1Plus_Click(object sender, RoutedEventArgs e)
        {
            if (isTimerRunning) return;
            hour += 10;
            if (hour >= 100) hour = 0;
            UpdateDigitDisplays();
        }

        private void Digit1Minus_Click(object sender, RoutedEventArgs e)
        {
            if (isTimerRunning) return;
            hour -= 10;
            if (hour < 0) hour = 90;
            UpdateDigitDisplays();
        }

        // 第2位数字（小时个位）
        private void Digit2Plus_Click(object sender, RoutedEventArgs e)
        {
            if (isTimerRunning) return;
            hour++;
            if (hour >= 100) hour = 0;
            UpdateDigitDisplays();
        }

        private void Digit2Minus_Click(object sender, RoutedEventArgs e)
        {
            if (isTimerRunning) return;
            hour--;
            if (hour < 0) hour = 99;
            UpdateDigitDisplays();
        }

        // 第3位数字（分钟十位）
        private void Digit3Plus_Click(object sender, RoutedEventArgs e)
        {
            if (isTimerRunning) return;
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
                timer.Start();
            }
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            if (!isTimerRunning)
            {
                UpdateDigitDisplays();
            }
            else if (isTimerRunning && isPaused)
            {
                UpdateDigitDisplays();
                StartPauseIcon.Data = Geometry.Parse(PlayIconData);
                isTimerRunning = false;
                timer.Stop();
                isPaused = false;
            }
            else
            {
                startTime = DateTime.Now;
                Timer_Elapsed(timer, null);
            }
        }

        private void Fullscreen_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Normal)
            {
                WindowState = WindowState.Maximized;
            }
            else
            {
                WindowState = WindowState.Normal;
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
                    // 播放自定义铃声
                    mediaPlayer.Open(new Uri(MainWindow.Settings.RandSettings.CustomTimerSoundPath));
                }
                else
                {
                    // 播放默认铃声
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
                // 如果播放失败，静默处理
                System.Diagnostics.Debug.WriteLine($"播放计时器铃声失败: {ex.Message}");
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 窗口加载时的初始化
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            isTimerRunning = false;
            
            if (MainWindow.Settings != null)
            {
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    try
                    {
                        var currentModeField = mainWindow.GetType().GetField("currentMode", 
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (currentModeField != null)
                        {
                            int currentMode = (int)currentModeField.GetValue(mainWindow);
                            if (currentMode == 1) // 白板模式
                            {
                                mainWindow.Topmost = false; // 保持白板模式下的非置顶状态
                            }
                            else
                            {
                                mainWindow.Topmost = true; // 其他模式恢复置顶
                            }
                        }
                    }
                    catch
                    {
                        // 如果反射失败，使用默认行为
                        mainWindow.Topmost = true;
                    }
                }
            }
        }

        private void BtnClose_MouseUp(object sender, MouseButtonEventArgs e)
        {
            Close();
        }

        private void WindowDragMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }
    }
}
