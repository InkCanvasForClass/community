using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.MainWindow_controls
{
    public partial class PPTNavigationBottomLeft : UserControl
    {
        public static readonly DependencyProperty CurrentPageProperty = DependencyProperty.Register(
            nameof(CurrentPage), typeof(int), typeof(PPTNavigationBottomLeft), new PropertyMetadata(1));

        public static readonly DependencyProperty TotalPagesProperty = DependencyProperty.Register(
            nameof(TotalPages), typeof(int), typeof(PPTNavigationBottomLeft), new PropertyMetadata(1));

        public static readonly DependencyProperty IsVisibleProperty = DependencyProperty.Register(
            nameof(IsVisible), typeof(bool), typeof(PPTNavigationBottomLeft), new PropertyMetadata(true));

        public static readonly RoutedEvent PreviousClickedEvent = EventManager.RegisterRoutedEvent(
            nameof(PreviousClicked), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PPTNavigationBottomLeft));

        public static readonly RoutedEvent NextClickedEvent = EventManager.RegisterRoutedEvent(
            nameof(NextClicked), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PPTNavigationBottomLeft));

        public static readonly RoutedEvent PageClickedEvent = EventManager.RegisterRoutedEvent(
            nameof(PageClicked), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PPTNavigationBottomLeft));

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

        public PPTNavigationBottomLeft()
        {
            InitializeComponent();
        }

        private void GridPPTControlPrevious_MouseDown(object sender, MouseButtonEventArgs e)
        {
            PPTLBPreviousButtonFeedbackBorder.Opacity = 1;
        }

        private void GridPPTControlPrevious_MouseLeave(object sender, MouseEventArgs e)
        {
            PPTLBPreviousButtonFeedbackBorder.Opacity = 0;
        }

        private void GridPPTControlPrevious_MouseUp(object sender, MouseButtonEventArgs e)
        {
            PPTLBPreviousButtonFeedbackBorder.Opacity = 0;
            RaiseEvent(new RoutedEventArgs(PreviousClickedEvent, this));
        }

        private void PPTNavigationBtn_MouseDown(object sender, MouseButtonEventArgs e)
        {
            PPTLBPageButtonFeedbackBorder.Opacity = 1;
        }

        private void PPTNavigationBtn_MouseLeave(object sender, MouseEventArgs e)
        {
            PPTLBPageButtonFeedbackBorder.Opacity = 0;
        }

        private void PPTNavigationBtn_MouseUp(object sender, MouseButtonEventArgs e)
        {
            PPTLBPageButtonFeedbackBorder.Opacity = 0;
            RaiseEvent(new RoutedEventArgs(PageClickedEvent, this));
        }

        private void GridPPTControlNext_MouseDown(object sender, MouseButtonEventArgs e)
        {
            PPTLBNextButtonFeedbackBorder.Opacity = 1;
        }

        private void GridPPTControlNext_MouseLeave(object sender, MouseEventArgs e)
        {
            PPTLBNextButtonFeedbackBorder.Opacity = 0;
        }

        private void GridPPTControlNext_MouseUp(object sender, MouseButtonEventArgs e)
        {
            PPTLBNextButtonFeedbackBorder.Opacity = 0;
            RaiseEvent(new RoutedEventArgs(NextClickedEvent, this));
        }
    }
}
