using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Ink_Canvas.MainWindow_controls
{
    public partial class ImageSelectionToolbar : UserControl
    {
        public static readonly DependencyProperty SelectedImageProperty = DependencyProperty.Register(
            nameof(SelectedImage), typeof(object), typeof(ImageSelectionToolbar), new PropertyMetadata(null));

        public static readonly DependencyProperty IsVisibleProperty = DependencyProperty.Register(
            nameof(IsVisible), typeof(Visibility), typeof(ImageSelectionToolbar), new PropertyMetadata(Visibility.Collapsed));

        public static readonly RoutedEvent CloneRequestedEvent = EventManager.RegisterRoutedEvent(
            nameof(CloneRequested), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ImageSelectionToolbar));

        public static readonly RoutedEvent CloneToNewBoardRequestedEvent = EventManager.RegisterRoutedEvent(
            nameof(CloneToNewBoardRequested), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ImageSelectionToolbar));

        public static readonly RoutedEvent DeleteRequestedEvent = EventManager.RegisterRoutedEvent(
            nameof(DeleteRequested), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ImageSelectionToolbar));

        public static readonly RoutedEvent RotateRequestedEvent = EventManager.RegisterRoutedEvent(
            nameof(RotateRequested), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ImageSelectionToolbar));

        public static readonly RoutedEvent ScaleChangedEvent = EventManager.RegisterRoutedEvent(
            nameof(ScaleChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ImageSelectionToolbar));

        public object SelectedImage
        {
            get => GetValue(SelectedImageProperty);
            set => SetValue(SelectedImageProperty, value);
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

        public event RoutedEventHandler ScaleChanged
        {
            add => AddHandler(ScaleChangedEvent, value);
            remove => RemoveHandler(ScaleChangedEvent, value);
        }

        public ImageSelectionToolbar()
        {
            InitializeComponent();
        }

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
        }

        private void BorderImageClone_MouseUp(object sender, MouseButtonEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(CloneRequestedEvent, this));
        }

        private void BorderImageCloneToNewBoard_MouseUp(object sender, MouseButtonEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(CloneToNewBoardRequestedEvent, this));
        }

        private void BorderImageDelete_MouseUp(object sender, MouseButtonEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(DeleteRequestedEvent, this));
        }

        private void BorderImageRotateLeft_MouseUp(object sender, MouseButtonEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(RotateRequestedEvent, this));
        }

        private void BorderImageRotateRight_MouseUp(object sender, MouseButtonEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(RotateRequestedEvent, this));
        }

        private void GridImageScaleDecrease_MouseUp(object sender, MouseButtonEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(ScaleChangedEvent, this));
        }

        private void GridImageScaleIncrease_MouseUp(object sender, MouseButtonEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(ScaleChangedEvent, this));
        }
    }
}
