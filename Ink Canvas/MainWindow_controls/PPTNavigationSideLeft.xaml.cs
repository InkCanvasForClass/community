using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.MainWindow_controls
{
    public partial class PPTNavigationSideLeft : UserControl
    {
        public static readonly DependencyProperty CurrentPageProperty = DependencyProperty.Register(
            nameof(CurrentPage), typeof(int), typeof(PPTNavigationSideLeft), new PropertyMetadata(1));

        public static readonly DependencyProperty TotalPagesProperty = DependencyProperty.Register(
            nameof(TotalPages), typeof(int), typeof(PPTNavigationSideLeft), new PropertyMetadata(1));

        public static readonly DependencyProperty IsVisibleProperty = DependencyProperty.Register(
            nameof(IsVisible), typeof(bool), typeof(PPTNavigationSideLeft), new PropertyMetadata(true));

        public static readonly RoutedEvent PreviousClickedEvent = EventManager.RegisterRoutedEvent(
            nameof(PreviousClicked), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PPTNavigationSideLeft));

        public static readonly RoutedEvent NextClickedEvent = EventManager.RegisterRoutedEvent(
            nameof(NextClicked), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PPTNavigationSideLeft));

        public static readonly RoutedEvent PageClickedEvent = EventManager.RegisterRoutedEvent(
            nameof(PageClicked), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PPTNavigationSideLeft));

        public int CurrentPage
        {
            get => (int)GetValue(CurrentPageProperty);
            set => SetValue(CurrentPageProperty, value);
        }

        public int TotalPages
        {
            get => (int)GetValue(TotalPagesProperty);
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

        public PPTNavigationSideLeft()
        {
            InitializeComponent();
        }

        private Border ContainerBorderElement => (Border)FindName("PPTBtnLSBorder");
        private Border PreviousButtonFeedbackBorderElement => (Border)FindName("PPTLSPreviousButtonFeedbackBorder");
        private GeometryDrawing PreviousButtonGeometryElement => (GeometryDrawing)FindName("PPTLSPreviousButtonGeometry");
        private Border PageButtonElement => (Border)FindName("PPTLSPageButton");
        private Border PageButtonFeedbackBorderElement => (Border)FindName("PPTLSPageButtonFeedbackBorder");
        private Border NextButtonFeedbackBorderElement => (Border)FindName("PPTLSNextButtonFeedbackBorder");
        private GeometryDrawing NextButtonGeometryElement => (GeometryDrawing)FindName("PPTLSNextButtonGeometry");
        private TextBlock CurrentPageTextElement => (TextBlock)FindName("PPTBtnPageNow");
        private TextBlock TotalPagesTextElement => (TextBlock)FindName("PPTBtnPageTotal");

        public Thickness ControlMargin
        {
            get => Margin;
            set => Margin = value;
        }

        public void SetPageDisplay(string currentPage, string totalPages)
        {
            CurrentPageTextElement.Text = currentPage;
            TotalPagesTextElement.Text = $"/ {totalPages}";
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

        private void GridPPTControlPrevious_MouseDown(object sender, MouseButtonEventArgs e)
        {
            PPTLSPreviousButtonFeedbackBorder.Opacity = 1;
        }

        private void GridPPTControlPrevious_MouseLeave(object sender, MouseEventArgs e)
        {
            PPTLSPreviousButtonFeedbackBorder.Opacity = 0;
        }

        private void GridPPTControlPrevious_MouseUp(object sender, MouseButtonEventArgs e)
        {
            PPTLSPreviousButtonFeedbackBorder.Opacity = 0;
            RaiseEvent(new RoutedEventArgs(PreviousClickedEvent, this));
        }

        private void PPTNavigationBtn_MouseDown(object sender, MouseButtonEventArgs e)
        {
            PPTLSPageButtonFeedbackBorder.Opacity = 1;
        }

        private void PPTNavigationBtn_MouseLeave(object sender, MouseEventArgs e)
        {
            PPTLSPageButtonFeedbackBorder.Opacity = 0;
        }

        private void PPTNavigationBtn_MouseUp(object sender, MouseButtonEventArgs e)
        {
            PPTLSPageButtonFeedbackBorder.Opacity = 0;
            RaiseEvent(new RoutedEventArgs(PageClickedEvent, this));
        }

        private void GridPPTControlNext_MouseDown(object sender, MouseButtonEventArgs e)
        {
            PPTLSNextButtonFeedbackBorder.Opacity = 1;
        }

        private void GridPPTControlNext_MouseLeave(object sender, MouseEventArgs e)
        {
            PPTLSNextButtonFeedbackBorder.Opacity = 0;
        }

        private void GridPPTControlNext_MouseUp(object sender, MouseButtonEventArgs e)
        {
            PPTLSNextButtonFeedbackBorder.Opacity = 0;
            RaiseEvent(new RoutedEventArgs(NextClickedEvent, this));
        }
    }
}
