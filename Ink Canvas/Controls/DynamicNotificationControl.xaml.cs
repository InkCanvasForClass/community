using Ink_Canvas.Models;
using Ink_Canvas.Properties;
using iNKORE.UI.WPF.Modern.Common.IconKeys;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Ink_Canvas.Controls
{
    public partial class DynamicNotificationControl : UserControl
    {
        private readonly DispatcherTimer autoCloseTimer = new DispatcherTimer();
        private NotificationMessage currentMessage;
        private bool isExpanded;
        private bool isDarkTheme = true;

        public event EventHandler Closed;

        public DynamicNotificationControl()
        {
            InitializeComponent();
            autoCloseTimer.Tick += AutoCloseTimer_Tick;
        }

        public void Show(NotificationMessage message)
        {
            currentMessage = message;
            isExpanded = message?.ForcePopup == true;

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

            autoCloseTimer.Stop();
            autoCloseTimer.Interval = TimeSpan.FromSeconds(Math.Max(1, message?.DisplaySeconds ?? 5));
            autoCloseTimer.Start();
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

        private void UserControl_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            autoCloseTimer.Stop();
        }

        private void UserControl_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (currentMessage != null)
            {
                autoCloseTimer.Start();
            }
        }

        private void AutoCloseTimer_Tick(object sender, EventArgs e)
        {
            Close();
        }

        private void Close()
        {
            autoCloseTimer.Stop();
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
