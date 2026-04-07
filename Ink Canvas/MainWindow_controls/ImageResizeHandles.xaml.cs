using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace Ink_Canvas.MainWindow_controls
{
    public partial class ImageResizeHandles : UserControl
    {
        private const double HandleOffset = 4d;

        private FrameworkElement ResizeHandlesCanvasElement => (FrameworkElement)FindName("ImageResizeHandlesCanvas");
        private Ellipse TopLeftHandle => (Ellipse)FindName("ImageTopLeftHandle");
        private Ellipse TopRightHandle => (Ellipse)FindName("ImageTopRightHandle");
        private Ellipse BottomLeftHandle => (Ellipse)FindName("ImageBottomLeftHandle");
        private Ellipse BottomRightHandle => (Ellipse)FindName("ImageBottomRightHandle");
        private Ellipse TopHandle => (Ellipse)FindName("ImageTopHandle");
        private Ellipse BottomHandle => (Ellipse)FindName("ImageBottomHandle");
        private Ellipse LeftHandle => (Ellipse)FindName("ImageLeftHandle");
        private Ellipse RightHandle => (Ellipse)FindName("ImageRightHandle");

        public static new readonly DependencyProperty VisibilityProperty = DependencyProperty.Register(
            nameof(Visibility),
            typeof(Visibility),
            typeof(ImageResizeHandles),
            new FrameworkPropertyMetadata(Visibility.Collapsed, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVisibilityChanged));

        public new Visibility Visibility
        {
            get => (Visibility)GetValue(VisibilityProperty);
            set => SetValue(VisibilityProperty, value);
        }

        public Thickness ControlMargin
        {
            get => ResizeHandlesCanvasElement.Margin;
            set => ResizeHandlesCanvasElement.Margin = value;
        }

        public ImageResizeHandles()
        {
            InitializeComponent();
        }

        public void UpdateHandlePositions(Rect elementBounds)
        {
            ControlMargin = new Thickness(elementBounds.Left, elementBounds.Top, 0, 0);

            System.Windows.Controls.Canvas.SetLeft(TopLeftHandle, -HandleOffset);
            System.Windows.Controls.Canvas.SetTop(TopLeftHandle, -HandleOffset);

            System.Windows.Controls.Canvas.SetLeft(TopRightHandle, elementBounds.Width - HandleOffset);
            System.Windows.Controls.Canvas.SetTop(TopRightHandle, -HandleOffset);

            System.Windows.Controls.Canvas.SetLeft(BottomLeftHandle, -HandleOffset);
            System.Windows.Controls.Canvas.SetTop(BottomLeftHandle, elementBounds.Height - HandleOffset);

            System.Windows.Controls.Canvas.SetLeft(BottomRightHandle, elementBounds.Width - HandleOffset);
            System.Windows.Controls.Canvas.SetTop(BottomRightHandle, elementBounds.Height - HandleOffset);

            System.Windows.Controls.Canvas.SetLeft(TopHandle, elementBounds.Width / 2 - HandleOffset);
            System.Windows.Controls.Canvas.SetTop(TopHandle, -HandleOffset);

            System.Windows.Controls.Canvas.SetLeft(BottomHandle, elementBounds.Width / 2 - HandleOffset);
            System.Windows.Controls.Canvas.SetTop(BottomHandle, elementBounds.Height - HandleOffset);

            System.Windows.Controls.Canvas.SetLeft(LeftHandle, -HandleOffset);
            System.Windows.Controls.Canvas.SetTop(LeftHandle, elementBounds.Height / 2 - HandleOffset);

            System.Windows.Controls.Canvas.SetLeft(RightHandle, elementBounds.Width - HandleOffset);
            System.Windows.Controls.Canvas.SetTop(RightHandle, elementBounds.Height / 2 - HandleOffset);
        }

        private static void OnVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ImageResizeHandles handles)
            {
                var visibility = (Visibility)e.NewValue;
                handles.SetCurrentValue(UIElement.VisibilityProperty, visibility);
                ResizeHandlesVisibility(handles, visibility);
            }
        }

        private static void ResizeHandlesVisibility(ImageResizeHandles handles, Visibility visibility)
        {
            if (handles.ResizeHandlesCanvasElement is UIElement resizeHandlesCanvas)
            {
                resizeHandlesCanvas.Visibility = visibility;
            }
        }
    }
}
