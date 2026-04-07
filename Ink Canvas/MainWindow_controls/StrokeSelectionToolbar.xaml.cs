using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.MainWindow_controls
{
    public partial class StrokeSelectionToolbar : UserControl
    {
        public static readonly DependencyProperty SelectedStrokesProperty = DependencyProperty.Register(
            nameof(SelectedStrokes), typeof(object), typeof(StrokeSelectionToolbar), new PropertyMetadata(null));

        public static readonly DependencyProperty IsVisibleProperty = DependencyProperty.Register(
            nameof(IsVisible), typeof(Visibility), typeof(StrokeSelectionToolbar), new PropertyMetadata(Visibility.Collapsed));

        public static readonly RoutedEvent CloneRequestedEvent = EventManager.RegisterRoutedEvent(
            nameof(CloneRequested), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(StrokeSelectionToolbar));

        public static readonly RoutedEvent CloneToNewBoardRequestedEvent = EventManager.RegisterRoutedEvent(
            nameof(CloneToNewBoardRequested), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(StrokeSelectionToolbar));

        public static readonly RoutedEvent DeleteRequestedEvent = EventManager.RegisterRoutedEvent(
            nameof(DeleteRequested), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(StrokeSelectionToolbar));

        public static readonly RoutedEvent RotateRequestedEvent = EventManager.RegisterRoutedEvent(
            nameof(RotateRequested), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(StrokeSelectionToolbar));

        public static readonly RoutedEvent FlipRequestedEvent = EventManager.RegisterRoutedEvent(
            nameof(FlipRequested), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(StrokeSelectionToolbar));

        public static readonly RoutedEvent WidthChangedEvent = EventManager.RegisterRoutedEvent(
            nameof(WidthChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(StrokeSelectionToolbar));

        public object SelectedStrokes
        {
            get => GetValue(SelectedStrokesProperty);
            set => SetValue(SelectedStrokesProperty, value);
        }

        public new Visibility IsVisible
        {
            get => (Visibility)GetValue(IsVisibleProperty);
            set => SetValue(IsVisibleProperty, value);
        }

        public event RoutedEventHandler CloneRequested
        {
            add => AddHandler(CloneRequestedEvent, value);
            remove => RemoveHandler(CloneRequestedEvent, value);
        }

        public event RoutedEventHandler CloneToNewBoardRequested
        {
            add => AddHandler(CloneToNewBoardRequestedEvent, value);
            remove => RemoveHandler(CloneToNewBoardRequestedEvent, value);
        }

        public event RoutedEventHandler DeleteRequested
        {
            add => AddHandler(DeleteRequestedEvent, value);
            remove => RemoveHandler(DeleteRequestedEvent, value);
        }

        public event RoutedEventHandler RotateRequested
        {
            add => AddHandler(RotateRequestedEvent, value);
            remove => RemoveHandler(RotateRequestedEvent, value);
        }

        public event RoutedEventHandler FlipRequested
        {
            add => AddHandler(FlipRequestedEvent, value);
            remove => RemoveHandler(FlipRequestedEvent, value);
        }

        public event RoutedEventHandler WidthChanged
        {
            add => AddHandler(WidthChangedEvent, value);
            remove => RemoveHandler(WidthChangedEvent, value);
        }

        public StrokeSelectionToolbar()
        {
            InitializeComponent();
        }

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
        }

        private void BorderStrokeSelectionClone_MouseUp(object sender, MouseButtonEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(CloneRequestedEvent, this));
        }

        private void BorderStrokeSelectionCloneToNewBoard_MouseUp(object sender, MouseButtonEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(CloneToNewBoardRequestedEvent, this));
        }

        private void BorderStrokeSelectionDelete_MouseUp(object sender, MouseButtonEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(DeleteRequestedEvent, this));
        }

        private void ImageRotate45_MouseUp(object sender, MouseButtonEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(RotateRequestedEvent, this));
        }

        private void ImageRotate90_MouseUp(object sender, MouseButtonEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(RotateRequestedEvent, this));
        }

        private void ImageFlipHorizontal_MouseUp(object sender, MouseButtonEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(FlipRequestedEvent, this));
        }

        private void ImageFlipVertical_MouseUp(object sender, MouseButtonEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(FlipRequestedEvent, this));
        }

        private void GridPenWidthDecrease_MouseUp(object sender, MouseButtonEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(WidthChangedEvent, this));
        }

        private void GridPenWidthIncrease_MouseUp(object sender, MouseButtonEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(WidthChangedEvent, this));
        }

        private void GridPenWidthRestore_MouseUp(object sender, MouseButtonEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(WidthChangedEvent, this));
        }
    }
}
