using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Ink_Canvas.MainWindow_controls
{
    public partial class ImageResizeHandles : UserControl
    {
        public static readonly DependencyProperty TargetBoundsProperty = DependencyProperty.Register(
            nameof(TargetBounds), typeof(Rect), typeof(ImageResizeHandles), new PropertyMetadata(new Rect(0, 0, 0, 0)));

        public static readonly DependencyProperty IsVisibleProperty = DependencyProperty.Register(
            nameof(IsVisible), typeof(Visibility), typeof(ImageResizeHandles), new PropertyMetadata(Visibility.Collapsed));

        public static readonly RoutedEvent ResizeStartedEvent = EventManager.RegisterRoutedEvent(
            nameof(ResizeStarted), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ImageResizeHandles));

        public static readonly RoutedEvent ResizeCompletedEvent = EventManager.RegisterRoutedEvent(
            nameof(ResizeCompleted), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ImageResizeHandles));

        public static readonly RoutedEvent BoundsChangedEvent = EventManager.RegisterRoutedEvent(
            nameof(BoundsChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ImageResizeHandles));

        public Rect TargetBounds
        {
            get => (Rect)GetValue(TargetBoundsProperty);
            set => SetValue(TargetBoundsProperty, value);
        }

        public new Visibility IsVisible
        {
            get => (Visibility)GetValue(IsVisibleProperty);
            set => SetValue(IsVisibleProperty, value);
        }

        public event RoutedEventHandler ResizeStarted
        {
            add => AddHandler(ResizeStartedEvent, value);
            remove => RemoveHandler(ResizeStartedEvent, value);
        }

        public event RoutedEventHandler ResizeCompleted
        {
            add => AddHandler(ResizeCompletedEvent, value);
            remove => RemoveHandler(ResizeCompletedEvent, value);
        }

        public event RoutedEventHandler BoundsChanged
        {
            add => AddHandler(BoundsChangedEvent, value);
            remove => RemoveHandler(BoundsChangedEvent, value);
        }

        public ImageResizeHandles()
        {
            InitializeComponent();
        }

        private void ImageResizeHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(ResizeStartedEvent, this));
            e.Handled = true;
        }

        private void ImageResizeHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(ResizeCompletedEvent, this));
            e.Handled = true;
        }

        private void ImageResizeHandle_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                RaiseEvent(new RoutedEventArgs(BoundsChangedEvent, this));
            }
        }
    }
}
