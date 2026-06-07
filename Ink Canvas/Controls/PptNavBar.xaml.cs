using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Ink_Canvas.Controls
{
    /// <summary>
    /// PPT 翻页 + 增强预览一体化控件。
    /// 通过 <see cref="Direction"/> 切换底部条 (LB/RB) 与侧边条 (LS/RS) 布局,
    /// 预览列表内嵌于同一个 Border,展开时占据按钮组之外的剩余空间。
    /// </summary>
    public partial class PptNavBar : UserControl
    {
        public sealed class PreviewItem
        {
            public int SlideNumber { get; set; }
            public BitmapImage Thumbnail { get; set; }
        }

        public enum NavDirection
        {
            LeftBottom,
            RightBottom,
            LeftSide,
            RightSide
        }

        public static readonly DependencyProperty DirectionProperty = DependencyProperty.Register(
            nameof(Direction), typeof(NavDirection), typeof(PptNavBar),
            new PropertyMetadata(NavDirection.LeftBottom, OnDirectionChanged));

        public static readonly DependencyProperty CurrentSlideProperty = DependencyProperty.Register(
            nameof(CurrentSlide), typeof(int), typeof(PptNavBar),
            new PropertyMetadata(0, OnPageChanged));

        public static readonly DependencyProperty TotalSlidesProperty = DependencyProperty.Register(
            nameof(TotalSlides), typeof(int), typeof(PptNavBar),
            new PropertyMetadata(0, OnPageChanged));

        public static readonly DependencyProperty PreviewItemsProperty = DependencyProperty.Register(
            nameof(PreviewItems), typeof(IList<PreviewItem>), typeof(PptNavBar),
            new PropertyMetadata(null, OnPreviewItemsChanged));

        public static readonly DependencyProperty IsPreviewExpandedProperty = DependencyProperty.Register(
            nameof(IsPreviewExpanded), typeof(bool), typeof(PptNavBar),
            new PropertyMetadata(false, OnIsPreviewExpandedChanged));

        public NavDirection Direction
        {
            get => (NavDirection)GetValue(DirectionProperty);
            set => SetValue(DirectionProperty, value);
        }

        public int CurrentSlide
        {
            get => (int)GetValue(CurrentSlideProperty);
            set => SetValue(CurrentSlideProperty, value);
        }

        public int TotalSlides
        {
            get => (int)GetValue(TotalSlidesProperty);
            set => SetValue(TotalSlidesProperty, value);
        }

        public IList<PreviewItem> PreviewItems
        {
            get => (IList<PreviewItem>)GetValue(PreviewItemsProperty);
            set => SetValue(PreviewItemsProperty, value);
        }

        public bool IsPreviewExpanded
        {
            get => (bool)GetValue(IsPreviewExpandedProperty);
            set => SetValue(IsPreviewExpandedProperty, value);
        }

        public event EventHandler PreviousClick;
        public event EventHandler NextClick;
        public event EventHandler PageClick;
        public event EventHandler<int> SlideSelected;
        public event EventHandler PreviousPressedDown;
        public event EventHandler NextPressedDown;
        public event EventHandler PressEnded;
        public event EventHandler<bool> PreviewExpandedChanged;

        // 静态几何（左下/右下：水平箭头；左侧/右侧：垂直箭头）
        private static readonly Geometry HArrowLeft = Geometry.Parse("F0 M24,24z M0,0z M3.3994,12.9642C2.86687,12.4317,2.86687,11.5683,3.3994,11.0358L9.94485,4.49031C10.4774,3.95777 11.3408,3.95777 11.8733,4.49031 12.4059,5.02284 12.4059,5.88625 11.8733,6.41878L7.65575,10.6364 19.6364,10.6364C20.3895,10.6364 21,11.2469 21,12 21,12.7531 20.3895,13.3636 19.6364,13.3636L7.65575,13.3636 11.8733,17.5812C12.4059,18.1137 12.4059,18.9772 11.8733,19.5097 11.3408,20.0422 10.4774,20.0422 9.94485,19.5097L3.3994,12.9642z");
        private static readonly Geometry HArrowRight = Geometry.Parse("F0 M24,24z M0,0z M20.6006,12.9642C21.1331,12.4317,21.1331,11.5683,20.6006,11.0358L14.0551,4.49031C13.5226,3.95777 12.6592,3.95777 12.1267,4.49031 11.5941,5.02284 11.5941,5.88625 12.1267,6.41878L16.3443,10.6364 4.36364,10.6364C3.61052,10.6364 3,11.2469 3,12 3,12.7531 3.61052,13.3636 4.36364,13.3636L16.3443,13.3636 12.1267,17.5812C11.5941,18.1137 11.5941,18.9772 12.1267,19.5097 12.6592,20.0422 13.5226,20.0422 14.0551,19.5097L20.6006,12.9642z");
        private static readonly Geometry VArrowUp = Geometry.Parse("F0 M24,24z M0,0z M11.0357,3.3994C11.5682,2.86687,12.4316,2.86687,12.9641,3.3994L19.5096,9.94485C20.0421,10.4774 20.0421,11.3408 19.5096,11.8733 18.9771,12.4059 18.1137,12.4059 17.5811,11.8733L13.3635,7.65575 13.3635,19.6364C13.3635,20.3895 12.753,21 11.9999,21 11.2468,21 10.6363,20.3895 10.6363,19.6364L10.6363,7.65575 6.41869,11.8733C5.88616,12.4059 5.02275,12.4059 4.49022,11.8733 3.95769,11.3408 3.95769,10.4774 4.49022,9.94485L11.0357,3.3994z");
        private static readonly Geometry VArrowDown = Geometry.Parse("F0 M24,24z M0,0z M11.0357,20.6006C11.5682,21.1331,12.4316,21.1331,12.9641,20.6006L19.5096,14.0551C20.0421,13.5226 20.0421,12.6592 19.5096,12.1267 18.9771,11.5941 18.1137,11.5941 17.5811,12.1267L13.3635,16.3443 13.3635,4.36364C13.3635,3.61052 12.753,3 11.9999,3 11.2468,3 10.6363,3.61052 10.6363,4.36364L10.6363,16.3443 6.41869,12.1267C5.88616,11.5941 5.02275,11.5941 4.49022,12.1267 3.95769,12.6592 3.95769,13.5226 4.49022,14.0551L11.0357,20.6006z");

        public PptNavBar()
        {
            InitializeComponent();
            ApplyDirection(Direction);
        }

        private static void OnDirectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PptNavBar bar) bar.ApplyDirection((NavDirection)e.NewValue);
        }

        private static void OnPageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PptNavBar bar) bar.RefreshPageText();
        }

        private static void OnPreviewItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PptNavBar bar)
            {
                bar.PreviewList.ItemsSource = e.NewValue as IList<PreviewItem>;
                bar.SyncPreviewSelection();
            }
        }

        private static void OnIsPreviewExpandedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PptNavBar bar)
            {
                bool expanded = (bool)e.NewValue;
                bar.PreviewList.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
                bar.ApplyLayout();
                if (expanded)
                {
                    bar.SyncPreviewSelection();
                    bar.HookOutsideClick();
                }
                else
                {
                    bar.UnhookOutsideClick();
                }
                bar.PreviewExpandedChanged?.Invoke(bar, expanded);
            }
        }

        private Window _hookedWindow;
        private void HookOutsideClick()
        {
            if (_hookedWindow != null) return;
            _hookedWindow = Window.GetWindow(this);
            if (_hookedWindow != null)
            {
                _hookedWindow.PreviewMouseDown += OnWindowPreviewMouseDown;
            }
        }
        private void UnhookOutsideClick()
        {
            if (_hookedWindow != null)
            {
                _hookedWindow.PreviewMouseDown -= OnWindowPreviewMouseDown;
                _hookedWindow = null;
            }
        }
        private void OnWindowPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject d && !IsDescendantOf(d, this))
            {
                IsPreviewExpanded = false;
            }
        }
        private static bool IsDescendantOf(DependencyObject child, DependencyObject ancestor)
        {
            while (child != null)
            {
                if (ReferenceEquals(child, ancestor)) return true;
                child = System.Windows.Media.VisualTreeHelper.GetParent(child)
                        ?? System.Windows.LogicalTreeHelper.GetParent(child);
            }
            return false;
        }

        private void ApplyDirection(NavDirection dir) => ApplyLayout();

        private void ApplyLayout()
        {
            var dir = Direction;
            bool expanded = IsPreviewExpanded;

            // 重置可能在不同状态下被设置的属性
            ButtonRow.ClearValue(WidthProperty);
            ButtonRow.ClearValue(HeightProperty);
            ButtonRow.ClearValue(HorizontalAlignmentProperty);
            PreviewList.ClearValue(WidthProperty);
            PreviewList.ClearValue(HeightProperty);
            PreviewList.ClearValue(MaxHeightProperty);
            PreviewList.ClearValue(MaxWidthProperty);
            PreviewList.ClearValue(HorizontalAlignmentProperty);
            ClearValue(HeightProperty);
            ClearValue(MaxHeightProperty);

            double availableHeight = ComputeAvailableHeight();
            double scale = _currentScale;

            switch (dir)
            {
                case NavDirection.LeftBottom:
                case NavDirection.RightBottom:
                    DockPanel.SetDock(PreviewList, Dock.Top);
                    DockPanel.SetDock(ButtonRow, Dock.Bottom);
                    ButtonRow.Orientation = Orientation.Horizontal;
                    ButtonRow.Height = 50;
                    if (expanded)
                    {
                        // 预览面板拉宽到 280,贴向同侧角落
                        PreviewList.Width = 280;
                        PreviewList.MaxHeight = Math.Max(200, availableHeight - 50 * scale) / scale;
                        PreviewList.HorizontalAlignment = dir == NavDirection.LeftBottom
                            ? HorizontalAlignment.Left
                            : HorizontalAlignment.Right;
                        // 按钮组宽度限制为原始内容宽度,并贴向同侧,保持按钮位置不变
                        ButtonRow.HorizontalAlignment = dir == NavDirection.LeftBottom
                            ? HorizontalAlignment.Left
                            : HorizontalAlignment.Right;
                    }
                    else
                    {
                        PreviewList.SetBinding(WidthProperty, new System.Windows.Data.Binding(nameof(ButtonRow.ActualWidth)) { Source = ButtonRow });
                        PreviewList.MaxHeight = 380 / scale;
                    }
                    PreviousButtonGeometry.Geometry = HArrowLeft;
                    NextButtonGeometry.Geometry = HArrowRight;
                    break;

                case NavDirection.LeftSide:
                    DockPanel.SetDock(PreviewList, Dock.Right);
                    DockPanel.SetDock(ButtonRow, Dock.Left);
                    ButtonRow.Orientation = Orientation.Vertical;
                    ButtonRow.Width = 50;
                    PreviewList.Width = 240;
                    PreviewList.MaxHeight = 480 / scale;
                    PreviousButtonGeometry.Geometry = VArrowUp;
                    NextButtonGeometry.Geometry = VArrowDown;
                    break;
                case NavDirection.RightSide:
                    DockPanel.SetDock(PreviewList, Dock.Left);
                    DockPanel.SetDock(ButtonRow, Dock.Right);
                    ButtonRow.Orientation = Orientation.Vertical;
                    ButtonRow.Width = 50;
                    PreviewList.Width = 240;
                    PreviewList.MaxHeight = 480 / scale;
                    PreviousButtonGeometry.Geometry = VArrowUp;
                    NextButtonGeometry.Geometry = VArrowDown;
                    break;
            }
        }

        private double ComputeAvailableHeight()
        {
            var window = Window.GetWindow(this);
            double h = window != null ? window.ActualHeight : SystemParameters.PrimaryScreenHeight;
            return Math.Max(240, h - 12);
        }

        private void RefreshPageText()
        {
            if (CurrentSlide > 0 && TotalSlides > 0)
            {
                PageNowText.Text = CurrentSlide.ToString();
                PageTotalText.Text = $"/ {TotalSlides}";
            }
            else
            {
                PageNowText.Text = "?";
                PageTotalText.Text = "/ ?";
            }
            SyncPreviewSelection();
        }

        private void SyncPreviewSelection()
        {
            if (PreviewItems == null || CurrentSlide <= 0) return;
            foreach (var item in PreviewItems)
            {
                if (item.SlideNumber == CurrentSlide)
                {
                    PreviewList.SelectedItem = item;
                    PreviewList.ScrollIntoView(item);
                    return;
                }
            }
        }

        private void SetFeedback(Border feedback, double opacity) => feedback.Opacity = opacity;

        private object _lastDown;

        private void PreviousButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _lastDown = sender;
            SetFeedback(PreviousButtonFeedbackBorder, 0.15);
            PreviousPressedDown?.Invoke(this, EventArgs.Empty);
        }

        private void PreviousButton_MouseUp(object sender, MouseButtonEventArgs e)
        {
            SetFeedback(PreviousButtonFeedbackBorder, 0);
            PressEnded?.Invoke(this, EventArgs.Empty);
            if (_lastDown != sender) return;
            _lastDown = null;
            PreviousClick?.Invoke(this, EventArgs.Empty);
        }

        private void PreviousButton_MouseLeave(object sender, MouseEventArgs e)
        {
            SetFeedback(PreviousButtonFeedbackBorder, 0);
            _lastDown = null;
            PressEnded?.Invoke(this, EventArgs.Empty);
        }

        private void NextButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _lastDown = sender;
            SetFeedback(NextButtonFeedbackBorder, 0.15);
            NextPressedDown?.Invoke(this, EventArgs.Empty);
        }

        private void NextButton_MouseUp(object sender, MouseButtonEventArgs e)
        {
            SetFeedback(NextButtonFeedbackBorder, 0);
            PressEnded?.Invoke(this, EventArgs.Empty);
            if (_lastDown != sender) return;
            _lastDown = null;
            NextClick?.Invoke(this, EventArgs.Empty);
        }

        private void NextButton_MouseLeave(object sender, MouseEventArgs e)
        {
            SetFeedback(NextButtonFeedbackBorder, 0);
            _lastDown = null;
            PressEnded?.Invoke(this, EventArgs.Empty);
        }

        private void PageButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _lastDown = sender;
            SetFeedback(PageButtonFeedbackBorder, 0.15);
        }

        private void PageButton_MouseUp(object sender, MouseButtonEventArgs e)
        {
            SetFeedback(PageButtonFeedbackBorder, 0);
            if (_lastDown != sender) return;
            _lastDown = null;
            PageClick?.Invoke(this, EventArgs.Empty);
        }

        private void PageButton_MouseLeave(object sender, MouseEventArgs e)
        {
            SetFeedback(PageButtonFeedbackBorder, 0);
            _lastDown = null;
        }

        private void PreviewList_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (PreviewList.SelectedItem is PreviewItem item)
            {
                SlideSelected?.Invoke(this, item.SlideNumber);
            }
        }

        public void ApplyTheme(bool isDark)
        {
            var fgBrush = isDark ? Brushes.White : new SolidColorBrush(Color.FromRgb(39, 39, 42));
            var feedbackBrush = isDark ? Brushes.White : new SolidColorBrush(Color.FromRgb(24, 24, 27));
            var bgBrush = isDark
                ? new SolidColorBrush(Color.FromRgb(39, 39, 42))
                : new SolidColorBrush(Color.FromRgb(244, 244, 245));
            var borderBrush = isDark
                ? new SolidColorBrush(Color.FromRgb(82, 82, 91))
                : new SolidColorBrush(Color.FromRgb(161, 161, 170));

            PreviousButtonGeometry.Brush = fgBrush;
            NextButtonGeometry.Brush = fgBrush;
            PreviousButtonFeedbackBorder.Background = feedbackBrush;
            NextButtonFeedbackBorder.Background = feedbackBrush;
            PageButtonFeedbackBorder.Background = feedbackBrush;
            PageNowText.Foreground = fgBrush;
            PageTotalText.Foreground = fgBrush;
            RootBorder.Background = bgBrush;
            RootBorder.BorderBrush = borderBrush;
            Resources["PptNavBarItemForeground"] = fgBrush;
        }

        public void SetPageButtonVisibility(Visibility v) => PageButtonBorder.Visibility = v;
        public void SetBarOpacity(double opacity) => RootBorder.Opacity = opacity;

        public void SetBarScale(double scale)
        {
            _currentScale = scale;
            NavBarScaleTransform.ScaleX = scale;
            NavBarScaleTransform.ScaleY = scale;
        }

        private double _currentScale = 1.0;
    }
}