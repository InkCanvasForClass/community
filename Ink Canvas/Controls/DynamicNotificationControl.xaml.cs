using Ink_Canvas.Models;
using Ink_Canvas.Properties;
using iNKORE.UI.WPF.Modern.Common.IconKeys;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Ink_Canvas.Controls
{
    public partial class DynamicNotificationControl : UserControl
    {
        private DateTime autoCloseStartedAt;
        private TimeSpan autoCloseDuration;
        private bool isCountdownRendering;
        private NotificationMessage currentMessage;
        private bool isExpanded;

        public event EventHandler Closed;

        public DynamicNotificationControl()
        {
            InitializeComponent();
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

            StopCountdownRendering();
            autoCloseDuration = TimeSpan.FromSeconds(Math.Max(1, message?.DisplaySeconds ?? 5));
            autoCloseStartedAt = DateTime.Now;
            UpdateCountdownProgress(1);
            StartCountdownRendering();
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
        }

        private void UserControl_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
        }

        private void StartCountdownRendering()
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
            RenderCountdownFrame();
        }

        private void RenderCountdownFrame()
        {
            if (currentMessage == null || autoCloseDuration <= TimeSpan.Zero)
            {
                Close();
                return;
            }

            var remaining = 1 - (DateTime.Now - autoCloseStartedAt).TotalMilliseconds / autoCloseDuration.TotalMilliseconds;
            UpdateCountdownProgress(Math.Max(0, remaining));
            if (remaining <= 0)
            {
                Close();
            }
        }

        private void RootContainer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateCountdownProgress(CountdownProgressPath?.Tag is double progress ? progress : 1);
        }

        private void UpdateCountdownProgress(double progress)
        {
            if (CountdownProgressPath == null || RootContainer.ActualWidth <= 0 || RootContainer.ActualHeight <= 0)
            {
                return;
            }

            progress = Math.Max(0, Math.Min(1, progress));
            CountdownProgressPath.Tag = progress;

            double width = RootContainer.ActualWidth;
            double height = RootContainer.ActualHeight;
            double radius = Math.Min(24, Math.Min(width, height) / 2);
            double centerX = width / 2;
            double topRightLine = width - radius;
            double topLeftLine = radius;
            double rightLine = height - radius;
            double bottomLine = width - radius;
            double leftLine = height - radius;

            double arc = Math.PI * radius / 2;
            double perimeter = (topRightLine - centerX) + arc + rightLine + arc + bottomLine + arc + leftLine + arc + (centerX - topLeftLine);
            double length = perimeter * progress;
            var figure = new PathFigure { StartPoint = new Point(centerX, 1.5), IsClosed = false, IsFilled = false };

            AddLineSegment(figure, new Point(topRightLine, 1.5), ref length);
            AddArcSegment(figure, new Point(width - 1.5, radius), new Size(radius - 1.5, radius - 1.5), SweepDirection.Clockwise, ref length, arc);
            AddLineSegment(figure, new Point(width - 1.5, rightLine), ref length);
            AddArcSegment(figure, new Point(bottomLine, height - 1.5), new Size(radius - 1.5, radius - 1.5), SweepDirection.Clockwise, ref length, arc);
            AddLineSegment(figure, new Point(topLeftLine, height - 1.5), ref length);
            AddArcSegment(figure, new Point(1.5, leftLine), new Size(radius - 1.5, radius - 1.5), SweepDirection.Clockwise, ref length, arc);
            AddLineSegment(figure, new Point(1.5, radius), ref length);
            AddArcSegment(figure, new Point(topLeftLine, 1.5), new Size(radius - 1.5, radius - 1.5), SweepDirection.Clockwise, ref length, arc);
            AddLineSegment(figure, new Point(centerX, 1.5), ref length);

            CountdownProgressPath.Data = new PathGeometry(new[] { figure });
        }

        private static void AddLineSegment(PathFigure figure, Point endPoint, ref double length)
        {
            if (length <= 0) return;

            Point startPoint = figure.Segments.Count == 0 ? figure.StartPoint : GetLastPoint(figure);
            double segmentLength = (endPoint - startPoint).Length;
            if (length >= segmentLength)
            {
                figure.Segments.Add(new LineSegment(endPoint, true));
                length -= segmentLength;
                return;
            }

            double ratio = segmentLength <= 0 ? 0 : length / segmentLength;
            figure.Segments.Add(new LineSegment(new Point(startPoint.X + (endPoint.X - startPoint.X) * ratio, startPoint.Y + (endPoint.Y - startPoint.Y) * ratio), true));
            length = 0;
        }

        private static void AddArcSegment(PathFigure figure, Point endPoint, Size size, SweepDirection sweepDirection, ref double length, double segmentLength)
        {
            if (length <= 0) return;
            if (length >= segmentLength)
            {
                figure.Segments.Add(new ArcSegment(endPoint, size, 0, false, sweepDirection, true));
                length -= segmentLength;
                return;
            }

            figure.Segments.Add(new ArcSegment(endPoint, size, 0, false, sweepDirection, true));
            length = 0;
        }

        private static Point GetLastPoint(PathFigure figure)
        {
            if (figure.Segments.Count == 0) return figure.StartPoint;
            var segment = figure.Segments[figure.Segments.Count - 1];
            if (segment is LineSegment line) return line.Point;
            if (segment is ArcSegment arc) return arc.Point;
            return figure.StartPoint;
        }

        private void Close()
        {
            StopCountdownRendering();
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
