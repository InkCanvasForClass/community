using Ink_Canvas.Models;
using Ink_Canvas.Properties;
using iNKORE.UI.WPF.Modern.Common.IconKeys;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Ink_Canvas.Controls
{
    public partial class DynamicNotificationControl : UserControl
    {
        private readonly DispatcherTimer autoCloseTimer = new DispatcherTimer();
        private NotificationMessage currentMessage;
        private bool isExpanded;

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
            BeginShowAnimation();

            autoCloseTimer.Stop();
            autoCloseTimer.Interval = TimeSpan.FromSeconds(Math.Max(1, message?.DisplaySeconds ?? 5));
            autoCloseTimer.Start();
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
