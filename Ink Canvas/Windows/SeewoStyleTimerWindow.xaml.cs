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
            
            // 监听窗口大小变化
            this.SizeChanged += SeewoStyleTimerWindow_SizeChanged;
        }

        private void SeewoStyleTimerWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // 根据窗口大小动态调整控件
            if (WindowState == WindowState.Maximized)
            {
                AdjustFontSizesForFullscreen(true);
            }
            else
            {
                AdjustFontSizesForFullscreen(false);
            }
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
                Digit1Display.Text = (leftTimeSpan.Hours / 10).ToString();
                Digit2Display.Text = (leftTimeSpan.Hours % 10).ToString();
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
                    StartPauseIcon.Text = "▶";
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
            minute += 10;
            if (minute >= 60) minute = 0;
            UpdateDigitDisplays();
        }

        private void Digit3Minus_Click(object sender, RoutedEventArgs e)
        {
            if (isTimerRunning) return;
            minute -= 10;
            if (minute < 0) minute = 50;
            UpdateDigitDisplays();
        }

        // 第4位数字（分钟个位）
        private void Digit4Plus_Click(object sender, RoutedEventArgs e)
        {
            if (isTimerRunning) return;
            minute++;
            if (minute >= 60) minute = 0;
            UpdateDigitDisplays();
        }

        private void Digit4Minus_Click(object sender, RoutedEventArgs e)
        {
            if (isTimerRunning) return;
            minute--;
            if (minute < 0) minute = 59;
            UpdateDigitDisplays();
        }

        // 第5位数字（秒十位）
        private void Digit5Plus_Click(object sender, RoutedEventArgs e)
        {
            if (isTimerRunning) return;
            second += 10;
            if (second >= 60) second = 0;
            UpdateDigitDisplays();
        }

        private void Digit5Minus_Click(object sender, RoutedEventArgs e)
        {
            if (isTimerRunning) return;
            second -= 10;
            if (second < 0) second = 50;
            UpdateDigitDisplays();
        }

        // 第6位数字（秒个位）
        private void Digit6Plus_Click(object sender, RoutedEventArgs e)
        {
            if (isTimerRunning) return;
            second++;
            if (second >= 60) second = 0;
            UpdateDigitDisplays();
        }

        private void Digit6Minus_Click(object sender, RoutedEventArgs e)
        {
            if (isTimerRunning) return;
            second--;
            if (second < 0) second = 59;
            UpdateDigitDisplays();
        }

        private void StartPause_Click(object sender, RoutedEventArgs e)
        {
            if (isPaused && isTimerRunning)
            {
                // 继续计时
                startTime += DateTime.Now - pauseTime;
                StartPauseIcon.Text = "⏸";
                isPaused = false;
                timer.Start();
            }
            else if (isTimerRunning)
            {
                // 暂停计时
                pauseTime = DateTime.Now;
                StartPauseIcon.Text = "▶";
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
                StartPauseIcon.Text = "⏸";
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
                StartPauseIcon.Text = "▶";
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
                // 全屏时调整字体大小
                AdjustFontSizesForFullscreen(true);
            }
            else
            {
                WindowState = WindowState.Normal;
                // 恢复普通窗口时的字体大小
                AdjustFontSizesForFullscreen(false);
            }
        }

        private void AdjustFontSizesForFullscreen(bool isFullscreen)
        {
            if (isFullscreen)
            {
                // 全屏时增大字体
                Digit1Display.FontSize = 120;
                Digit2Display.FontSize = 120;
                Digit3Display.FontSize = 120;
                Digit4Display.FontSize = 120;
                Digit5Display.FontSize = 120;
                Digit6Display.FontSize = 120;
                
                // 增大按钮尺寸
                Digit1PlusBtn.Width = 50;
                Digit1PlusBtn.Height = 50;
                Digit1MinusBtn.Width = 50;
                Digit1MinusBtn.Height = 50;
                
                Digit2PlusBtn.Width = 50;
                Digit2PlusBtn.Height = 50;
                Digit2MinusBtn.Width = 50;
                Digit2MinusBtn.Height = 50;
                
                Digit3PlusBtn.Width = 50;
                Digit3PlusBtn.Height = 50;
                Digit3MinusBtn.Width = 50;
                Digit3MinusBtn.Height = 50;
                
                Digit4PlusBtn.Width = 50;
                Digit4PlusBtn.Height = 50;
                Digit4MinusBtn.Width = 50;
                Digit4MinusBtn.Height = 50;
                
                Digit5PlusBtn.Width = 50;
                Digit5PlusBtn.Height = 50;
                Digit5MinusBtn.Width = 50;
                Digit5MinusBtn.Height = 50;
                
                Digit6PlusBtn.Width = 50;
                Digit6PlusBtn.Height = 50;
                Digit6MinusBtn.Width = 50;
                Digit6MinusBtn.Height = 50;
                
                // 增大控制按钮
                StartPauseBtn.Width = 150;
                StartPauseBtn.Height = 150;
                FullscreenBtn.Width = 150;
                FullscreenBtn.Height = 60;
                ResetBtn.Width = 150;
                ResetBtn.Height = 60;
                
                // 调整按钮字体大小
                StartPauseIcon.FontSize = 48;
                
                // 调整标题栏和关闭按钮在全屏时的位置
                TitleBar.Height = 80;
                TitleText.FontSize = 32;
                BtnClose.Width = 50;
                BtnClose.Height = 50;
                
                // 调整数字显示区域的间距
                MainDisplayGrid.Margin = new Thickness(0, 20, 0, 20);
                
                // 调整时间单位标签的字体大小（现在每个时间组都有自己的标签）
                // 这些标签现在直接在每个时间组内，不需要单独处理
            }
            else
            {
                // 恢复普通窗口时的尺寸
                Digit1Display.FontSize = 64;
                Digit2Display.FontSize = 64;
                Digit3Display.FontSize = 64;
                Digit4Display.FontSize = 64;
                Digit5Display.FontSize = 64;
                Digit6Display.FontSize = 64;
                
                // 恢复按钮尺寸
                Digit1PlusBtn.Width = 30;
                Digit1PlusBtn.Height = 30;
                Digit1MinusBtn.Width = 30;
                Digit1MinusBtn.Height = 30;
                
                Digit2PlusBtn.Width = 30;
                Digit2PlusBtn.Height = 30;
                Digit2MinusBtn.Width = 30;
                Digit2MinusBtn.Height = 30;
                
                Digit3PlusBtn.Width = 30;
                Digit3PlusBtn.Height = 30;
                Digit3MinusBtn.Width = 30;
                Digit3MinusBtn.Height = 30;
                
                Digit4PlusBtn.Width = 30;
                Digit4PlusBtn.Height = 30;
                Digit4MinusBtn.Width = 30;
                Digit4MinusBtn.Height = 30;
                
                Digit5PlusBtn.Width = 30;
                Digit5PlusBtn.Height = 30;
                Digit5MinusBtn.Width = 30;
                Digit5MinusBtn.Height = 30;
                
                Digit6PlusBtn.Width = 30;
                Digit6PlusBtn.Height = 30;
                Digit6MinusBtn.Width = 30;
                Digit6MinusBtn.Height = 30;
                
                // 恢复控制按钮尺寸
                StartPauseBtn.Width = 80;
                StartPauseBtn.Height = 80;
                FullscreenBtn.Width = 80;
                FullscreenBtn.Height = 40;
                ResetBtn.Width = 80;
                ResetBtn.Height = 40;
                
                // 恢复按钮字体大小
                StartPauseIcon.FontSize = 32;
                
                // 恢复标题栏和关闭按钮的普通尺寸
                TitleBar.Height = 50;
                TitleText.FontSize = 20;
                BtnClose.Width = 30;
                BtnClose.Height = 30;
                
                // 恢复数字显示区域的间距
                MainDisplayGrid.Margin = new Thickness(0, 0, 0, 0);
                
                // 恢复时间单位标签的字体大小（现在每个时间组都有自己的标签）
                // 这些标签现在直接在每个时间组内，不需要单独处理
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
