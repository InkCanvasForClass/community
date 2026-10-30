using Ink_Canvas.Helpers;
using Ink_Canvas.Models;
using Ink_Canvas.Services.Classroom;
using iNKORE.UI.WPF.Modern.Common.IconKeys;
using System;
using System.Media;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace Ink_Canvas
{
    /// <summary>
    /// Interaction logic for StopwatchWindow.xaml
    /// </summary>
    public partial class CountdownTimerWindow : Window
    {
        public CountdownTimerWindow()
        {
            InitializeComponent();
            AnimationsHelper.ShowWithSlideFromBottomAndFade(this, 0.25);

            _timerService = new TimerService(
                TimerService.LegacyIntervalForTotal,
                () => MainWindow.Settings.RandSettings?.EnableOvertimeCountUp == true,
                () => false);
            // 原字段初始值：hour=0, minute=1, second=0
            _timerService.Minute = 1;
            _timerService.StateChanged += OnTimerStateChanged;

            InitializeUI();

            // 应用主题
            ApplyTheme();
        }

        public static Window CreateTimerWindow()
        {
            return new CountdownTimerWindow();
        }

        // 计时状态机已提取至 TimerService（M14）；本窗口仅订阅状态并渲染。
        private readonly TimerService _timerService;

        private void OnTimerStateChanged(TimerState state)
        {
            Application.Current.Dispatcher.Invoke(() => RenderTick(state));
        }

        private void RenderTick(TimerState state)
        {
            if (!state.IsOvertimeMode || state.JustEnteredOvertime)
            {
                TimeSpan leftTimeSpan = state.Remaining;

                ProcessBarTime.CurrentValue = 1 - state.SpentPercent;
                TextBlockHour.Text = leftTimeSpan.Hours.ToString("00");
                TextBlockMinute.Text = leftTimeSpan.Minutes.ToString("00");
                TextBlockSecond.Text = leftTimeSpan.Seconds.ToString("00");
                TbCurrentTime.Text = leftTimeSpan.ToString(@"hh\:mm\:ss");

                if (state.JustEnteredOvertime)
                {
                    ProcessBarTime.CurrentValue = 0;
                    ProcessBarTime.Visibility = Visibility.Collapsed;
                    BorderStopTime.Visibility = Visibility.Collapsed;

                    // 播放提醒音
                    PlayTimerSound();
                }
                else if (state.JustCompleted)
                {
                    ProcessBarTime.CurrentValue = 0;
                    TextBlockHour.Text = "00";
                    TextBlockMinute.Text = "00";
                    TextBlockSecond.Text = "00";
                    FontIconStart.Icon = SegoeFluentIcons.Play;
                    BtnStartCover.Visibility = Visibility.Visible;
                    var textForeground = Application.Current.FindResource("TimerWindowTextForeground") as SolidColorBrush;
                    if (textForeground != null)
                    {
                        TextBlockHour.Foreground = textForeground;
                    }
                    else
                    {
                        TextBlockHour.Foreground = new SolidColorBrush(StringToColor("#FF5B5D5F"));
                    }
                    BorderStopTime.Visibility = Visibility.Collapsed;

                    // 播放提醒音
                    PlayTimerSound();
                }
            }
            else
            {
                TimeSpan overtimeSpan = state.Overtime;
                TextBlockHour.Text = overtimeSpan.Hours.ToString("00");
                TextBlockMinute.Text = overtimeSpan.Minutes.ToString("00");
                TextBlockSecond.Text = overtimeSpan.Seconds.ToString("00");
                TbCurrentTime.Text = overtimeSpan.ToString(@"hh\:mm\:ss");

                if (MainWindow.Settings.RandSettings?.EnableOvertimeRedText == true)
                {
                    TextBlockHour.Foreground = Brushes.Red;
                    TextBlockMinute.Foreground = Brushes.Red;
                    TextBlockSecond.Foreground = Brushes.Red;
                }
            }
        }

        SoundPlayer player = new SoundPlayer();
        MediaPlayer mediaPlayer = new MediaPlayer();

        bool useLegacyUI = false;

        private void Grid_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_timerService.IsRunning) return;

            var textForeground = Application.Current.FindResource("TimerWindowTextForeground") as SolidColorBrush;

            if (ProcessBarTime.Visibility == Visibility.Visible && _timerService.IsRunning == false)
            {
                ProcessBarTime.Visibility = Visibility.Collapsed;
                GridAdjustHour.Visibility = Visibility.Visible;
                if (textForeground != null)
                {
                    TextBlockHour.Foreground = textForeground;
                }
                else
                {
                    TextBlockHour.Foreground = Brushes.Black;
                }
            }
            else
            {
                ProcessBarTime.Visibility = Visibility.Visible;
                GridAdjustHour.Visibility = Visibility.Collapsed;
                if (textForeground != null)
                {
                    TextBlockHour.Foreground = textForeground;
                }
                else
                {
                    TextBlockHour.Foreground = new SolidColorBrush(StringToColor("#FF5B5D5F"));
                }

                if (_timerService.Hour == 0 && _timerService.Minute == 0 && _timerService.Second == 0)
                {
                    _timerService.Second = 1;
                    TextBlockSecond.Text = _timerService.Second.ToString("00");
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            _timerService.Hour++;
            if (_timerService.Hour >= 100) _timerService.Hour = 0;
            TextBlockHour.Text = _timerService.Hour.ToString("00");
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            _timerService.Hour += 5;
            if (_timerService.Hour >= 100) _timerService.Hour = 0;
            TextBlockHour.Text = _timerService.Hour.ToString("00");
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            _timerService.Hour--;
            if (_timerService.Hour < 0) _timerService.Hour = 99;
            TextBlockHour.Text = _timerService.Hour.ToString("00");
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            _timerService.Hour -= 5;
            if (_timerService.Hour < 0) _timerService.Hour = 99;
            TextBlockHour.Text = _timerService.Hour.ToString("00");
        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            _timerService.Minute++;
            if (_timerService.Minute >= 60) _timerService.Minute = 0;
            TextBlockMinute.Text = _timerService.Minute.ToString("00");
        }

        private void Button_Click_5(object sender, RoutedEventArgs e)
        {
            _timerService.Minute += 5;
            if (_timerService.Minute >= 60) _timerService.Minute = 0;
            TextBlockMinute.Text = _timerService.Minute.ToString("00");
        }

        private void Button_Click_6(object sender, RoutedEventArgs e)
        {
            _timerService.Minute--;
            if (_timerService.Minute < 0) _timerService.Minute = 59;
            TextBlockMinute.Text = _timerService.Minute.ToString("00");
        }

        private void Button_Click_7(object sender, RoutedEventArgs e)
        {
            _timerService.Minute -= 5;
            if (_timerService.Minute < 0) _timerService.Minute = 59;
            TextBlockMinute.Text = _timerService.Minute.ToString("00");
        }

        private void Button_Click_8(object sender, RoutedEventArgs e)
        {
            _timerService.Second += 5;
            if (_timerService.Second >= 60) _timerService.Second = 0;
            TextBlockSecond.Text = _timerService.Second.ToString("00");
        }

        private void Button_Click_9(object sender, RoutedEventArgs e)
        {
            _timerService.Second++;
            if (_timerService.Second >= 60) _timerService.Second = 0;
            TextBlockSecond.Text = _timerService.Second.ToString("00");
        }

        private void Button_Click_10(object sender, RoutedEventArgs e)
        {
            _timerService.Second--;
            if (_timerService.Second < 0) _timerService.Second = 59;
            TextBlockSecond.Text = _timerService.Second.ToString("00");
        }

        private void Button_Click_11(object sender, RoutedEventArgs e)
        {
            _timerService.Second -= 5;
            if (_timerService.Second < 0) _timerService.Second = 59;
            TextBlockSecond.Text = _timerService.Second.ToString("00");
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ProcessBarTime.Visibility = Visibility.Visible;
            GridAdjustHour.Visibility = Visibility.Collapsed;
            BorderStopTime.Visibility = Visibility.Collapsed;
        }

        private void BtnFullscreen_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (WindowState == WindowState.Normal)
            {
                WindowState = WindowState.Maximized;
                FontIconFullscreen.Icon = SegoeFluentIcons.BackToWindow;
            }
            else
            {
                WindowState = WindowState.Normal;
                FontIconFullscreen.Icon = SegoeFluentIcons.FullScreen;
            }
        }

        private void BtnReset_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_timerService.IsRunning)
            {
                TextBlockHour.Text = _timerService.Hour.ToString("00");
                TextBlockMinute.Text = _timerService.Minute.ToString("00");
                TextBlockSecond.Text = _timerService.Second.ToString("00");
                BtnResetCover.Visibility = Visibility.Visible;
                BtnStartCover.Visibility = Visibility.Collapsed;
                BorderStopTime.Visibility = Visibility.Collapsed;
                var textForeground3 = Application.Current.FindResource("TimerWindowTextForeground") as SolidColorBrush;
                if (textForeground3 != null)
                    TextBlockHour.Foreground = textForeground3;
                else
                    TextBlockHour.Foreground = new SolidColorBrush(StringToColor("#FF5B5D5F"));

                _timerService.Reset();
                ProcessBarTime.Visibility = Visibility.Visible;
            }
            else if (_timerService.IsRunning && _timerService.IsPaused)
            {
                TextBlockHour.Text = _timerService.Hour.ToString("00");
                TextBlockMinute.Text = _timerService.Minute.ToString("00");
                TextBlockSecond.Text = _timerService.Second.ToString("00");
                BtnResetCover.Visibility = Visibility.Visible;
                BtnStartCover.Visibility = Visibility.Collapsed;
                BorderStopTime.Visibility = Visibility.Collapsed;
                var textForeground3 = Application.Current.FindResource("TimerWindowTextForeground") as SolidColorBrush;
                if (textForeground3 != null)
                    TextBlockHour.Foreground = textForeground3;
                else
                    TextBlockHour.Foreground = new SolidColorBrush(StringToColor("#FF5B5D5F"));
                FontIconStart.Icon = SegoeFluentIcons.Play;
                _timerService.Reset();
                ProcessBarTime.CurrentValue = 0;
                ProcessBarTime.IsPaused = false;

                ProcessBarTime.Visibility = Visibility.Visible;
            }
            else
            {
                UpdateStopTime();
                _timerService.Restart();
            }
        }

        void UpdateStopTime()
        {
            TextBlockStopTime.Text = (_timerService.StartTime + _timerService.GetTotalTimeSpan()).ToString("t");
        }

        private Color StringToColor(string colorStr)
        {
            Byte[] argb = new Byte[4];
            for (int i = 0; i < 4; i++)
            {
                char[] charArray = colorStr.Substring(i * 2 + 1, 2).ToCharArray();
                //string str = "11";
                Byte b1 = toByte(charArray[0]);
                Byte b2 = toByte(charArray[1]);
                argb[i] = (Byte)(b2 | (b1 << 4));
            }

            return Color.FromArgb(argb[0], argb[1], argb[2], argb[3]); //#FFFFFFFF
        }

        private static byte toByte(char c)
        {
            byte b = (byte)"0123456789ABCDEF".IndexOf(c);
            return b;
        }

        private void BtnStart_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_timerService.IsPaused && _timerService.IsRunning)
            {
                //继续
                _timerService.Resume();
                ProcessBarTime.IsPaused = false;
                var textForeground1 = Application.Current.FindResource("TimerWindowTextForeground") as SolidColorBrush;
                if (textForeground1 != null)
                    TextBlockHour.Foreground = textForeground1;
                else
                    TextBlockHour.Foreground = Brushes.Black;
                FontIconStart.Icon = SegoeFluentIcons.Pause;
                UpdateStopTime();
                BorderStopTime.Visibility = Visibility.Visible;
            }
            else if (_timerService.IsRunning)
            {
                //暂停
                _timerService.Pause();
                ProcessBarTime.IsPaused = true;
                var textForeground3 = Application.Current.FindResource("TimerWindowTextForeground") as SolidColorBrush;
                if (textForeground3 != null)
                    TextBlockHour.Foreground = textForeground3;
                else
                    TextBlockHour.Foreground = new SolidColorBrush(StringToColor("#FF5B5D5F"));
                FontIconStart.Icon = SegoeFluentIcons.Play;
                BorderStopTime.Visibility = Visibility.Collapsed;
            }
            else
            {
                //从头开始
                _timerService.Start();
                ProcessBarTime.IsPaused = false;
                var textForeground2 = Application.Current.FindResource("TimerWindowTextForeground") as SolidColorBrush;
                if (textForeground2 != null)
                    TextBlockHour.Foreground = textForeground2;
                else
                    TextBlockHour.Foreground = Brushes.Black;
                FontIconStart.Icon = SegoeFluentIcons.Pause;
                BtnResetCover.Visibility = Visibility.Collapsed;
                ProcessBarTime.Visibility = Visibility.Visible;
                UpdateStopTime();
                BorderStopTime.Visibility = Visibility.Visible;
            }
        }

        private void InitializeUI()
        {
            // 从设置中读取配置
            if (MainWindow.Settings.RandSettings != null)
            {
                useLegacyUI = MainWindow.Settings.RandSettings.UseLegacyTimerUI;
                UpdateButtonTexts();
            }
        }

        private void ApplyTheme()
        {
            try
            {
                // 根据主题设置文本颜色
                var textForeground = Application.Current.FindResource("TimerWindowTextForeground") as SolidColorBrush;
                if (textForeground != null)
                {
                    TextBlockHour.Foreground = textForeground;
                    TextBlockMinute.Foreground = textForeground;
                    TextBlockSecond.Foreground = textForeground;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用倒计时窗口主题出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        public void RefreshUI()
        {
            InitializeUI();
        }

        /// <summary>
        /// 刷新主题，当主窗口主题切换时调用
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

        private void UpdateButtonTexts()
        {
            if (useLegacyUI)
            {
                // 老版UI：使用+5, +1, -1, -5
                HourPlus5Text.Text = "+5";
                HourPlus1Text.Text = "+1";
                HourMinus1Text.Text = "-1";
                HourMinus5Text.Text = "-5";

                MinutePlus5Text.Text = "+5";
                MinutePlus1Text.Text = "+1";
                MinuteMinus1Text.Text = "-1";
                MinuteMinus5Text.Text = "-5";

                SecondPlus5Text.Text = "+5";
                SecondPlus1Text.Text = "+1";
                SecondMinus1Text.Text = "-1";
                SecondMinus5Text.Text = "-5";
            }
            else
            {
                // 新版UI：使用箭头符号
                HourPlus5Text.Text = "∧∧";
                HourPlus1Text.Text = "∧";
                HourMinus1Text.Text = "∨";
                HourMinus5Text.Text = "∨∨";

                MinutePlus5Text.Text = "∧∧";
                MinutePlus1Text.Text = "∧";
                MinuteMinus1Text.Text = "∨";
                MinuteMinus5Text.Text = "∨∨";

                SecondPlus5Text.Text = "∧∧";
                SecondPlus1Text.Text = "∧";
                SecondMinus1Text.Text = "∨";
                SecondMinus5Text.Text = "∨∨";
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

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _timerService.MarkStopped();
        }

        private void BtnClose_MouseUp(object sender, MouseButtonEventArgs e)
        {
            Close();
        }

        private bool _isInCompact = false;

        private void BtnMinimal_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isInCompact)
            {
                Width = 1100;
                Height = 700;
                BigViewController.Visibility = Visibility.Visible;
                TbCurrentTime.Visibility = Visibility.Collapsed;

                // Set to center
                double dpiScaleX = 1, dpiScaleY = 1;
                PresentationSource source = PresentationSource.FromVisual(this);
                if (source != null)
                {
                    dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
                    dpiScaleY = source.CompositionTarget.TransformToDevice.M22;
                }
                IntPtr windowHandle = new WindowInteropHelper(this).Handle;
                System.Windows.Forms.Screen screen = System.Windows.Forms.Screen.FromHandle(windowHandle);
                double screenWidth = screen.Bounds.Width / dpiScaleX, screenHeight = screen.Bounds.Height / dpiScaleY;
                Left = (screenWidth / 2) - (Width / 2);
                Top = (screenHeight / 2) - (Height / 2);
                Left = (screenWidth / 2) - (Width / 2);
                Top = (screenHeight / 2) - (Height / 2);
            }
            else
            {
                if (WindowState == WindowState.Maximized) WindowState = WindowState.Normal;
                Width = 400;
                Height = 250;
                BigViewController.Visibility = Visibility.Collapsed;
                TbCurrentTime.Visibility = Visibility.Visible;
            }

            _isInCompact = !_isInCompact;
        }

        private void WindowDragMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }
    }
}
