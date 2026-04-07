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
            nameof(CurrentPage), typeof(string), typeof(PPTNavigationBottomLeft), new PropertyMetadata("1"));

        public static readonly DependencyProperty TotalPagesProperty = DependencyProperty.Register(
            nameof(TotalPages), typeof(string), typeof(PPTNavigationBottomLeft), new PropertyMetadata("1"));

        public static readonly DependencyProperty IsVisibleProperty = DependencyProperty.Register(
            nameof(IsVisible), typeof(bool), typeof(PPTNavigationBottomLeft), new PropertyMetadata(true));

        public static readonly RoutedEvent PreviousClickedEvent = EventManager.RegisterRoutedEvent(
            nameof(PreviousClicked), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PPTNavigationBottomLeft));

        public static readonly RoutedEvent NextClickedEvent = EventManager.RegisterRoutedEvent(
            nameof(NextClicked), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PPTNavigationBottomLeft));

        public static readonly RoutedEvent PageClickedEvent = EventManager.RegisterRoutedEvent(
            nameof(PageClicked), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PPTNavigationBottomLeft));

        public Border BorderSelectionControl => PPTBtnLBBorderElement;

        public Thickness ControlMargin
        {
            get => Margin;
            set => Margin = value;
        }

        public UIElement ControlChild => ViewboxPPTNavigationBottomLeft;

        public void InvalidateVisualOnControl()
        {
            PPTBtnLBBorderElement.InvalidateVisual();
        }

        public new Visibility Visibility
        {
            get => base.Visibility;
            set => base.Visibility = value;
        }

        public Border PPTBtnLBBorder => PPTBtnLBBorderElement;
        public Border PPTLBPreviousButtonBorder => PPTLBPreviousButtonBorderElement;
        public Border PPTLBPreviousButtonFeedbackBorder => PPTLBPreviousButtonFeedbackBorderElement;
        public GeometryDrawing PPTLBPreviousButtonGeometry => PPTLBPreviousButtonGeometryElement;
        public Border PPTLBPageButton => PPTLBPageButtonElement;
        public Border PPTLBPageButtonFeedbackBorder => PPTLBPageButtonFeedbackBorderElement;
        public Border PPTLBNextButtonBorder => PPTLBNextButtonBorderElement;
        public Border PPTLBNextButtonFeedbackBorder => PPTLBNextButtonFeedbackBorderElement;
        public GeometryDrawing PPTLBNextButtonGeometry => PPTLBNextButtonGeometryElement;

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

        public PPTNavigationBottomLeft()
        {
            InitializeComponent();
        }

        private void GridPPTControlPrevious_MouseDown(object sender, MouseButtonEventArgs e)
        {
            PPTLBPreviousButtonFeedbackBorderElement.Opacity = 1;
        }

        private void GridPPTControlPrevious_MouseLeave(object sender, MouseEventArgs e)
        {
            PPTLBPreviousButtonFeedbackBorderElement.Opacity = 0;
        }

        private void GridPPTControlPrevious_MouseUp(object sender, MouseButtonEventArgs e)
        {
            PPTLBPreviousButtonFeedbackBorderElement.Opacity = 0;
            RaiseEvent(new RoutedEventArgs(PreviousClickedEvent, this));
        }

        private void PPTNavigationBtn_MouseDown(object sender, MouseButtonEventArgs e)
        {
            PPTLBPageButtonFeedbackBorderElement.Opacity = 1;
        }

        private void PPTNavigationBtn_MouseLeave(object sender, MouseEventArgs e)
        {
            PPTLBPageButtonFeedbackBorderElement.Opacity = 0;
        }

        private void PPTNavigationBtn_MouseUp(object sender, MouseButtonEventArgs e)
        {
            PPTLBPageButtonFeedbackBorderElement.Opacity = 0;
            RaiseEvent(new RoutedEventArgs(PageClickedEvent, this));
        }

        private void GridPPTControlNext_MouseDown(object sender, MouseButtonEventArgs e)
        {
            PPTLBNextButtonFeedbackBorderElement.Opacity = 1;
        }

        private void GridPPTControlNext_MouseLeave(object sender, MouseEventArgs e)
        {
            PPTLBNextButtonFeedbackBorderElement.Opacity = 0;
        }

        private void GridPPTControlNext_MouseUp(object sender, MouseButtonEventArgs e)
        {
            PPTLBNextButtonFeedbackBorderElement.Opacity = 0;
            RaiseEvent(new RoutedEventArgs(NextClickedEvent, this));
        }
    }
}
