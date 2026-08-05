using Ink_Canvas.Models;
using Ink_Canvas.Properties;
using iNKORE.UI.WPF.Modern.Common.IconKeys;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Ink_Canvas.Controls
{
    public partial class DynamicNotificationControl : UserControl
    {
        private readonly Stopwatch countdownStopwatch = new Stopwatch();
        private NotificationMessage currentMessage;
        private TimeSpan countdownDuration;
        private TimeSpan countdownRemaining;
        private bool isExpanded;
        private bool isDarkTheme = true;
        private bool isClosing;
        private bool isCountdownRendering;
        private double countdownProgressLength;

        public event EventHandler Closed;

        public DynamicNotificationControl()
        {
            InitializeComponent();
            RootContainer.SizeChanged += RootContainer_SizeChanged;
        }

        public void Show(NotificationMessage message)
        {
            currentMessage = message;
            isExpanded = message?.ForcePopup == true;
            isClosing = false;

            TitleTextBlock.Text = string.IsNullOrWhiteSpace(message?.Title) ? NotificationStrings.DefaultTitle : message.Title;
            SummaryTextBlock.Text = message?.Summary ?? string.Empty;
            SummaryTextBlock.Visibility = string.IsNullOrWhiteSpace(SummaryTextBlock.Text) ? Visibility.Collapsed : Visibility.Visible;
            ContentTextBlock.Text = string.IsNullOrWhiteSpace(message?.Summary) ? message?.Content ?? string.Empty : message.Summary;
            ActionButton.Content = string.IsNullOrWhiteSpace(message?.ActionText) ? NotificationStrings.ViewDetails : message.ActionText;
            ActionButton.Visibility = message?.Action != null || !string.IsNullOrWhiteSpace(message?.ActionUrl) ? Visibility.Visible : Visibility.Collapsed;
            IconGlyph.Icon = GetIcon(message);
            ExpandedPanel.Visibility = isExpanded ? Visibility.Visible : Visibility.Collapsed;

            Visibility = Visibility.Visible;
            ApplyThemeColors(message);
            BeginShowAnimation();

            StartCountdown(TimeSpan.FromSeconds(Math.Max(1, message?.DisplaySeconds ?? 5)));
        }

        /// <summary>
        /// 刷新通知主题颜色，在全局主题切换时调用
        /// </summary>
        public void RefreshTheme(bool isDark)
        {
            isDarkTheme = isDark;
            if (Visibility == Visibility.Visible && currentMessage != null)
            {
                ApplyThemeColors(currentMessage);
            }
        }

        private FontIconData GetIcon(NotificationMessage message)
        {
            if (message?.Level >= NotificationMessageLevel.High) return SegoeFluentIcons.Warning;

            switch (message?.Type)
            {
                case NotificationMessageType.Urgent:
                    return SegoeFluentIcons.Warning;
                case NotificationMessageType.Important:
                    return SegoeFluentIcons.Important;
                case NotificationMessageType.Update:
                    return SegoeFluentIcons.Sync;
                case NotificationMessageType.Reminder:
                    return SegoeFluentIcons.Stopwatch;
                default:
                    return SegoeFluentIcons.Info;
            }
        }

        private void ApplyThemeColors(NotificationMessage message)
        {
            var (background, border, foreground, secondaryForeground, iconBackground) = GetThemeColors(message);
            RootBorder.Background = new SolidColorBrush(background);
            RootBorder.BorderBrush = new SolidColorBrush(border);
            TitleTextBlock.Foreground = new SolidColorBrush(foreground);
            SummaryTextBlock.Foreground = new SolidColorBrush(secondaryForeground);
            ContentTextBlock.Foreground = new SolidColorBrush(secondaryForeground);
            IconGlyph.Foreground = new SolidColorBrush(foreground);
            IconBackgroundBorder.Background = new SolidColorBrush(iconBackground);
            CloseButtonText.Foreground = new SolidColorBrush(secondaryForeground);
            CountdownProgressPath.Stroke = new SolidColorBrush(border);

            // 操作按钮使用半透明主题色
            if (isDarkTheme)
            {
                ActionButton.Background = new SolidColorBrush(Color.FromArgb(34, 255, 255, 255));
                ActionButton.Foreground = new SolidColorBrush(Colors.White);
                ActionButton.BorderBrush = new SolidColorBrush(Color.FromArgb(51, 255, 255, 255));
            }
            else
            {
                ActionButton.Background = new SolidColorBrush(Color.FromArgb(20, 0, 0, 0));
                ActionButton.Foreground = new SolidColorBrush(Color.FromRgb(24, 24, 27));
                ActionButton.BorderBrush = new SolidColorBrush(Color.FromArgb(34, 0, 0, 0));
            }
        }

        private (Color Background, Color Border, Color Foreground, Color SecondaryForeground, Color IconBackground) GetThemeColors(NotificationMessage message)
        {
            if (isDarkTheme)
                return GetDarkThemeColors(message);
            return GetLightThemeColors(message);
        }

        private static (Color Background, Color Border, Color Foreground, Color SecondaryForeground, Color IconBackground) GetDarkThemeColors(NotificationMessage message)
        {
            if (message?.Level >= NotificationMessageLevel.Critical || message?.Type == NotificationMessageType.Urgent)
                return (Color.FromArgb(238, 91, 30, 33), Color.FromRgb(255, 107, 107), Colors.White, Color.FromArgb(230, 255, 255, 255), Color.FromArgb(38, 255, 255, 255));

            if (message?.Level >= NotificationMessageLevel.High || message?.Type == NotificationMessageType.Important)
                return (Color.FromArgb(238, 112, 72, 18), Color.FromRgb(255, 183, 77), Colors.White, Color.FromArgb(230, 255, 255, 255), Color.FromArgb(38, 255, 255, 255));

            if (message?.Type == NotificationMessageType.Update)
                return (Color.FromArgb(238, 20, 68, 116), Color.FromRgb(66, 165, 245), Colors.White, Color.FromArgb(230, 255, 255, 255), Color.FromArgb(38, 255, 255, 255));

            if (message?.Type == NotificationMessageType.Reminder)
                return (Color.FromArgb(238, 31, 82, 47), Color.FromRgb(102, 187, 106), Colors.White, Color.FromArgb(230, 255, 255, 255), Color.FromArgb(38, 255, 255, 255));

            return (Color.FromArgb(238, 28, 32, 42), Color.FromRgb(66, 165, 245), Colors.White, Color.FromArgb(230, 255, 255, 255), Color.FromArgb(38, 255, 255, 255));
        }

        private static (Color Background, Color Border, Color Foreground, Color SecondaryForeground, Color IconBackground) GetLightThemeColors(NotificationMessage message)
        {
            if (message?.Level >= NotificationMessageLevel.Critical || message?.Type == NotificationMessageType.Urgent)
                return (Color.FromArgb(245, 255, 241, 242), Color.FromRgb(220, 80, 80), Color.FromRgb(153, 27, 27), Color.FromArgb(200, 153, 27, 27), Color.FromArgb(30, 220, 80, 80));

            if (message?.Level >= NotificationMessageLevel.High || message?.Type == NotificationMessageType.Important)
                return (Color.FromArgb(245, 255, 251, 235), Color.FromRgb(217, 153, 43), Color.FromRgb(146, 96, 14), Color.FromArgb(200, 146, 96, 14), Color.FromArgb(30, 217, 153, 43));

            if (message?.Type == NotificationMessageType.Update)
                return (Color.FromArgb(245, 235, 245, 255), Color.FromRgb(59, 130, 246), Color.FromRgb(30, 64, 175), Color.FromArgb(200, 30, 64, 175), Color.FromArgb(30, 59, 130, 246));

            if (message?.Type == NotificationMessageType.Reminder)
                return (Color.FromArgb(245, 240, 253, 244), Color.FromRgb(72, 160, 82), Color.FromRgb(22, 101, 52), Color.FromArgb(200, 22, 101, 52), Color.FromArgb(30, 72, 160, 82));

            return (Color.FromArgb(245, 240, 245, 255), Color.FromRgb(59, 130, 246), Color.FromRgb(30, 64, 175), Color.FromArgb(200, 30, 64, 175), Color.FromArgb(30, 59, 130, 246));
        }

        private void RootBorder_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.OriginalSource is Button) return;
            isExpanded = !isExpanded;
            ExpandedPanel.Visibility = isExpanded ? Visibility.Visible : Visibility.Collapsed;
            Dispatcher.BeginInvoke(new Action(UpdateCountdownProgress), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ActionButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (currentMessage?.Action != null)
                {
                    currentMessage.Action.Invoke();
                }
                else if (!string.IsNullOrWhiteSpace(currentMessage?.ActionUrl))
                {
                    Process.Start(new ProcessStartInfo(currentMessage.ActionUrl) { UseShellExecute = true });
                }
            }
            catch
            {
            }

            Close();
        }

        /// <summary>
        /// 摘除当前显示的插件回调。插件热重载时由 <see cref="Ink_Canvas.Plugins.PluginManager"/>
        /// 调用：<c>currentMessage.Action</c> 直接指向插件 ALC 里的 Action，
        /// 留着会阻止热重载时被回收。
        /// </summary>
        internal void DetachPluginActionIfMatches(string pluginId)
        {
            if (string.IsNullOrEmpty(pluginId)) return;
            if (currentMessage == null || currentMessage.Action == null) return;

            if (pluginId.Equals(currentMessage.Source, StringComparison.OrdinalIgnoreCase)
                || pluginId.Equals(currentMessage.ProviderId, StringComparison.OrdinalIgnoreCase))
            {
                currentMessage.Action = null;
            }
        }

        private void UserControl_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            PauseCountdown();
        }

        private void UserControl_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (currentMessage != null && !isClosing)
            {
                ResumeCountdown();
            }
        }

        private void RootContainer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateCountdownProgressPathGeometry();
            UpdateCountdownProgress();
        }

        private void StartCountdown(TimeSpan duration)
        {
            countdownDuration = duration;
            countdownRemaining = duration;
            countdownStopwatch.Restart();
            CountdownProgressPath.Visibility = Visibility.Visible;
            UpdateCountdownProgressPathGeometry();
            BeginCountdownRendering();
            UpdateCountdownProgress();
        }

        private void PauseCountdown()
        {
            if (!countdownStopwatch.IsRunning) return;

            countdownRemaining = GetCountdownRemaining();
            countdownStopwatch.Reset();
            StopCountdownRendering();
            UpdateCountdownProgress();
        }

        private void ResumeCountdown()
        {
            if (countdownRemaining <= TimeSpan.Zero)
            {
                Close();
                return;
            }

            countdownStopwatch.Restart();
            BeginCountdownRendering();
        }

        private void BeginCountdownRendering()
        {
            if (isCountdownRendering) return;

            CompositionTarget.Rendering += CompositionTarget_Rendering;
            isCountdownRendering = true;
        }

        private void StopCountdownRendering()
        {
            if (!isCountdownRendering) return;

            CompositionTarget.Rendering -= CompositionTarget_Rendering;
            isCountdownRendering = false;
        }

        private void CompositionTarget_Rendering(object sender, EventArgs e)
        {
            UpdateCountdownProgress();
        }

        private TimeSpan GetCountdownRemaining()
        {
            var remaining = countdownRemaining - countdownStopwatch.Elapsed;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        private void UpdateCountdownProgress()
        {
            var remaining = countdownStopwatch.IsRunning ? GetCountdownRemaining() : countdownRemaining;
            if (remaining <= TimeSpan.Zero)
            {
                Close();
                return;
            }

            var progress = countdownDuration.TotalMilliseconds <= 0 ? 0 : remaining.TotalMilliseconds / countdownDuration.TotalMilliseconds;
            UpdateCountdownProgressDash(progress);
        }

        private void UpdateCountdownProgressPathGeometry()
        {
            var width = RootContainer.ActualWidth;
            var height = RootContainer.ActualHeight;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            var thickness = CountdownProgressPath.StrokeThickness;
            var radius = Math.Max(0, RootBorder.CornerRadius.TopLeft - thickness / 2);
            var left = thickness / 2;
            var top = thickness / 2;
            var right = Math.Max(left, width - thickness / 2);
            var bottom = Math.Max(top, height - thickness / 2);
            var horizontalLength = Math.Max(0, right - left - radius * 2);
            var verticalLength = Math.Max(0, bottom - top - radius * 2);
            var arcLength = Math.PI * radius / 2;
            countdownProgressLength = horizontalLength * 2 + verticalLength * 2 + arcLength * 4;
            if (countdownProgressLength <= 0)
            {
                return;
            }

            var start = new Point((left + right) / 2, top);
            var figure = new PathFigure { StartPoint = start, IsClosed = true, IsFilled = false };
            figure.Segments.Add(new LineSegment(new Point(right - radius, top), true));
            figure.Segments.Add(new ArcSegment(new Point(right, top + radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true));
            figure.Segments.Add(new LineSegment(new Point(right, bottom - radius), true));
            figure.Segments.Add(new ArcSegment(new Point(right - radius, bottom), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true));
            figure.Segments.Add(new LineSegment(new Point(left + radius, bottom), true));
            figure.Segments.Add(new ArcSegment(new Point(left, bottom - radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true));
            figure.Segments.Add(new LineSegment(new Point(left, top + radius), true));
            figure.Segments.Add(new ArcSegment(new Point(left + radius, top), new Size(radius, radius), 0, false, SweepDirection.Clockwise, true));
            figure.Segments.Add(new LineSegment(start, true));

            CountdownProgressPath.Data = new PathGeometry(new[] { figure });
        }

        private void UpdateCountdownProgressDash(double progress)
        {
            if (countdownProgressLength <= 0)
            {
                UpdateCountdownProgressPathGeometry();
            }

            if (countdownProgressLength <= 0)
            {
                return;
            }

            progress = Math.Max(0, Math.Min(1, progress));
            var thickness = CountdownProgressPath.StrokeThickness;
            CountdownProgressPath.StrokeDashOffset = 0;
            CountdownProgressPath.StrokeDashArray = new DoubleCollection
            {
                countdownProgressLength * progress / thickness,
                countdownProgressLength / thickness
            };
        }

        private void Close()
        {
            if (isClosing) return;

            isClosing = true;
            StopCountdownRendering();
            countdownStopwatch.Reset();
            CountdownProgressPath.Visibility = Visibility.Collapsed;
            BeginHideAnimation();
        }

        private void BeginShowAnimation()
        {
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
            RootTransform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, new DoubleAnimation(-24, 0, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
        }

        private void BeginHideAnimation()
        {
            var opacityAnimation = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(160));
            opacityAnimation.Completed += (_, __) =>
            {
                Visibility = Visibility.Collapsed;
                currentMessage = null;
                Closed?.Invoke(this, EventArgs.Empty);
            };
            BeginAnimation(OpacityProperty, opacityAnimation);
            RootTransform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, new DoubleAnimation(0, -24, TimeSpan.FromMilliseconds(160)));
        }
    }
}
