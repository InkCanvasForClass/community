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

        public static new readonly DependencyProperty VisibilityProperty = DependencyProperty.Register(
            nameof(Visibility), typeof(Visibility), typeof(ImageSelectionToolbar), 
            new FrameworkPropertyMetadata(Visibility.Collapsed, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVisibilityChanged));

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

        // 用于向后兼容 - 暴露内部 Border 以便 partial class 访问
        public Border BorderSelectionControl => BorderImageSelectionControl;

        // 暴露 Margin 属性以便 MW_ElementsControls.cs 设置位置
        public Thickness ControlMargin
        {
            get => BorderImageSelectionControl.Margin;
            set => BorderImageSelectionControl.Margin = value;
        }

        // 暴露 Child 属性以便 MW_AutoTheme.cs 访问
        public UIElement ControlChild
        {
            get => BorderImageSelectionControl.Child;
        }

        // 暴露 InvalidateVisual 方法以便 MW_AutoTheme.cs 调用
        public void InvalidateVisualOnControl()
        {
            BorderImageSelectionControl.InvalidateVisual();
        }

        public object SelectedImage
        {
            get => GetValue(SelectedImageProperty);
            set => SetValue(SelectedImageProperty, value);
        }

        public new Visibility Visibility
        {
            get => (Visibility)GetValue(VisibilityProperty);
            set => SetValue(VisibilityProperty, value);
        }

        private static void OnVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ImageSelectionToolbar toolbar)
            {
                toolbar.BorderImageSelectionControl.Visibility = (Visibility)e.NewValue;
            }
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
