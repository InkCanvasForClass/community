using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.MainWindow_controls
{
    public partial class PPTNavigationBottomRight : UserControl
    {
        public static readonly DependencyProperty CurrentPageProperty = DependencyProperty.Register(
            nameof(CurrentPage), typeof(string), typeof(PPTNavigationBottomRight), new PropertyMetadata("1"));

        public static readonly DependencyProperty TotalPagesProperty = DependencyProperty.Register(
            nameof(TotalPages), typeof(string), typeof(PPTNavigationBottomRight), new PropertyMetadata("1"));

        public static readonly DependencyProperty IsVisibleProperty = DependencyProperty.Register(
            nameof(IsVisible), typeof(bool), typeof(PPTNavigationBottomRight), new PropertyMetadata(true));

        public static readonly RoutedEvent PreviousClickedEvent = EventManager.RegisterRoutedEvent(
            nameof(PreviousClicked), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PPTNavigationBottomRight));

        public static readonly RoutedEvent NextClickedEvent = EventManager.RegisterRoutedEvent(
            nameof(NextClicked), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PPTNavigationBottomRight));

        public static readonly RoutedEvent PageClickedEvent = EventManager.RegisterRoutedEvent(
            nameof(PageClicked), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PPTNavigationBottomRight));

        public Border BorderSelectionControl => PPTBtnRBBorderElement;

        public Thickness ControlMargin
        {
            get => Margin;
            set => Margin = value;
        }

        public UIElement ControlChild => ViewboxPPTNavigationBottomRight;

        public void InvalidateVisualOnControl()
        {
            PPTBtnRBBorderElement.InvalidateVisual();
        }

        public new Visibility Visibility
        {
            get => base.Visibility;
            set => base.Visibility = value;
        }

        public Border PPTBtnRBBorder => PPTBtnRBBorderElement;
        public Border PPTRBPreviousButtonBorder => PPTRBPreviousButtonBorderElement;
        public Border PPTRBPreviousButtonFeedbackBorder => PPTRBPreviousButtonFeedbackBorderElement;
        public GeometryDrawing PPTRBPreviousButtonGeometry => PPTRBPreviousButtonGeometryElement;
        public Border PPTRBPageButton => PPTRBPageButtonElement;
        public Border PPTRBPageButtonFeedbackBorder => PPTRBPageButtonFeedbackBorderElement;
        public Border PPTRBNextButtonBorder => PPTRBNextButtonBorderElement;
        public Border PPTRBNextButtonFeedbackBorder => PPTRBNextButtonFeedbackBorderElement;
        public GeometryDrawing PPTRBNextButtonGeometry => PPTRBNextButtonGeometryElement;

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

        public PPTNavigationBottomRight()
        {
            InitializeComponent();
        }

        private void GridPPTControlPrevious_MouseDown(object sender, MouseButtonEventArgs e)
        {
            PPTRBPreviousButtonFeedbackBorderElement.Opacity = 1;
        }

        private void GridPPTControlPrevious_MouseLeave(object sender, MouseEventArgs e)
        {
            PPTRBPreviousButtonFeedbackBorderElement.Opacity = 0;
        }

        private void GridPPTControlPrevious_MouseUp(object sender, MouseButtonEventArgs e)
        {
            PPTRBPreviousButtonFeedbackBorderElement.Opacity = 0;
            RaiseEvent(new RoutedEventArgs(PreviousClickedEvent, this));
        }

        private void PPTNavigationBtn_MouseDown(object sender, MouseButtonEventArgs e)
        {
            PPTRBPageButtonFeedbackBorderElement.Opacity = 1;
        }

        private void PPTNavigationBtn_MouseLeave(object sender, MouseEventArgs e)
        {
            PPTRBPageButtonFeedbackBorderElement.Opacity = 0;
        }

        private void PPTNavigationBtn_MouseUp(object sender, MouseButtonEventArgs e)
        {
            PPTRBPageButtonFeedbackBorderElement.Opacity = 0;
            RaiseEvent(new RoutedEventArgs(PageClickedEvent, this));
        }

        private void GridPPTControlNext_MouseDown(object sender, MouseButtonEventArgs e)
        {
            PPTRBNextButtonFeedbackBorderElement.Opacity = 1;
        }

        private void GridPPTControlNext_MouseLeave(object sender, MouseEventArgs e)
        {
            PPTRBNextButtonFeedbackBorderElement.Opacity = 0;
        }

        private void GridPPTControlNext_MouseUp(object sender, MouseButtonEventArgs e)
        {
            PPTRBNextButtonFeedbackBorderElement.Opacity = 0;
            RaiseEvent(new RoutedEventArgs(NextClickedEvent, this));
        }
    }
}
