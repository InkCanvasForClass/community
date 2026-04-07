using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.MainWindow_controls
{
    public partial class PPTNavigationSideRight : UserControl
    {
        public static readonly DependencyProperty CurrentPageProperty = DependencyProperty.Register(
            nameof(CurrentPage), typeof(string), typeof(PPTNavigationSideRight), new PropertyMetadata("1"));

        public static readonly DependencyProperty TotalPagesProperty = DependencyProperty.Register(
            nameof(TotalPages), typeof(string), typeof(PPTNavigationSideRight), new PropertyMetadata("1"));

        public static readonly DependencyProperty IsVisibleProperty = DependencyProperty.Register(
            nameof(IsVisible), typeof(bool), typeof(PPTNavigationSideRight), new PropertyMetadata(true));

        public static readonly RoutedEvent PreviousClickedEvent = EventManager.RegisterRoutedEvent(
            nameof(PreviousClicked), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PPTNavigationSideRight));

        public static readonly RoutedEvent NextClickedEvent = EventManager.RegisterRoutedEvent(
            nameof(NextClicked), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PPTNavigationSideRight));

        public static readonly RoutedEvent PageClickedEvent = EventManager.RegisterRoutedEvent(
            nameof(PageClicked), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PPTNavigationSideRight));

        private Border ContainerBorderElement => (Border)FindName("PPTBtnRSBorder");
        private Border PreviousButtonFeedbackBorderElement => (Border)FindName("PPTRSPreviousButtonFeedbackBorder");
        private GeometryDrawing PreviousButtonGeometryElement => (GeometryDrawing)FindName("PPTRSPreviousButtonGeometry");
        private Border PageButtonElement => (Border)FindName("PPTRSPageButton");
        private Border PageButtonFeedbackBorderElement => (Border)FindName("PPTRSPageButtonFeedbackBorder");
        private Border NextButtonFeedbackBorderElement => (Border)FindName("PPTRSNextButtonFeedbackBorder");
        private GeometryDrawing NextButtonGeometryElement => (GeometryDrawing)FindName("PPTRSNextButtonGeometry");

        public Border ContainerBorder => ContainerBorderElement;
        public Border PreviousButtonFeedbackBorder => PreviousButtonFeedbackBorderElement;
        public GeometryDrawing PreviousButtonGeometry => PreviousButtonGeometryElement;
        public Border PageButton => PageButtonElement;
        public Border PageButtonFeedbackBorder => PageButtonFeedbackBorderElement;
        public Border NextButtonFeedbackBorder => NextButtonFeedbackBorderElement;
        public GeometryDrawing NextButtonGeometry => NextButtonGeometryElement;

        public Thickness ControlMargin
        {
            get => Margin;
            set => Margin = value;
        }

        public void SetPageDisplay(string currentPage, string totalPages)
        {
            CurrentPage = currentPage;
            TotalPages = totalPages;
        }

        public void SetPageButtonVisibility(Visibility visibility)
        {
            PageButtonElement.Visibility = visibility;
        }

        public void SetContainerOpacity(double opacity)
        {
            ContainerBorderElement.Opacity = opacity;
        }

        public void ApplyTheme(Brush backgroundBrush, Brush borderBrush, Brush foregroundBrush, Brush feedbackBrush)
        {
            ContainerBorderElement.Background = backgroundBrush;
            ContainerBorderElement.BorderBrush = borderBrush;

            PreviousButtonGeometryElement.Brush = foregroundBrush;
            NextButtonGeometryElement.Brush = foregroundBrush;

            PreviousButtonFeedbackBorderElement.Background = feedbackBrush;
            PageButtonFeedbackBorderElement.Background = feedbackBrush;
            NextButtonFeedbackBorderElement.Background = feedbackBrush;

            TextBlock.SetForeground(PageButtonElement, foregroundBrush);
        }

        public string CurrentPage
        {
            get => (string)GetValue(CurrentPageProperty);
            set => SetValue(CurrentPageProperty, value);
        }

        public string TotalPages
        {
            get => (string)GetValue(TotalPagesProperty);
            set => SetValue(TotalPagesProperty, value);
        }

        public bool IsVisible
        {
            get => (bool)GetValue(IsVisibleProperty);
            set => SetValue(IsVisibleProperty, value);
        }

        public event RoutedEventHandler PreviousClicked
        {
            add => AddHandler(PreviousClickedEvent, value);
            remove => RemoveHandler(PreviousClickedEvent, value);
        }

        public event RoutedEventHandler NextClicked
        {
            add => AddHandler(NextClickedEvent, value);
            remove => RemoveHandler(NextClickedEvent, value);
        }

        public event RoutedEventHandler PageClicked
        {
            add => AddHandler(PageClickedEvent, value);
            remove => RemoveHandler(PageClickedEvent, value);
        }

        public PPTNavigationSideRight()
        {
            InitializeComponent();
        }

        private void GridPPTControlPrevious_MouseDown(object sender, MouseButtonEventArgs e)
        {
            PPTRSPreviousButtonFeedbackBorder.Opacity = 1;
        }

        private void GridPPTControlPrevious_MouseLeave(object sender, MouseEventArgs e)
        {
            PPTRSPreviousButtonFeedbackBorder.Opacity = 0;
        }

        private void GridPPTControlPrevious_MouseUp(object sender, MouseButtonEventArgs e)
        {
            PPTRSPreviousButtonFeedbackBorder.Opacity = 0;
            RaiseEvent(new RoutedEventArgs(PreviousClickedEvent, this));
        }

        private void PPTNavigationBtn_MouseDown(object sender, MouseButtonEventArgs e)
        {
            PPTRSPageButtonFeedbackBorder.Opacity = 1;
        }

        private void PPTNavigationBtn_MouseLeave(object sender, MouseEventArgs e)
        {
            PPTRSPageButtonFeedbackBorder.Opacity = 0;
        }

        private void PPTNavigationBtn_MouseUp(object sender, MouseButtonEventArgs e)
        {
            PPTRSPageButtonFeedbackBorder.Opacity = 0;
            RaiseEvent(new RoutedEventArgs(PageClickedEvent, this));
        }

        private void GridPPTControlNext_MouseDown(object sender, MouseButtonEventArgs e)
        {
            PPTRSNextButtonFeedbackBorder.Opacity = 1;
        }

        private void GridPPTControlNext_MouseLeave(object sender, MouseEventArgs e)
        {
            PPTRSNextButtonFeedbackBorder.Opacity = 0;
        }

        private void GridPPTControlNext_MouseUp(object sender, MouseButtonEventArgs e)
        {
            PPTRSNextButtonFeedbackBorder.Opacity = 0;
            RaiseEvent(new RoutedEventArgs(NextClickedEvent, this));
        }
    }
}
