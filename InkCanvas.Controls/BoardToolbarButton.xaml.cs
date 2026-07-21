using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.Controls
{
    public enum ButtonPosition
    {
        First,
        Middle,
        Last,
        Single
    }

    public partial class BoardToolbarButton : UserControl
    {
        public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
            nameof(Label), typeof(string), typeof(BoardToolbarButton),
            new PropertyMetadata(string.Empty, OnLabelChanged));

        private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var button = (BoardToolbarButton)d;
            if (button.LabelTextBlock != null)
                button.LabelTextBlock.Text = (string)e.NewValue;
        }

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public static readonly DependencyProperty IconGeometryProperty = DependencyProperty.Register(
            nameof(IconGeometry), typeof(string), typeof(BoardToolbarButton),
            new PropertyMetadata(string.Empty, OnIconGeometryChanged));

        private static void OnIconGeometryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var button = (BoardToolbarButton)d;
            if (button.IconGeometryInternal != null && e.NewValue is string geometry && !string.IsNullOrEmpty(geometry))
            {
                button.IconGeometryInternal.Geometry = Geometry.Parse(geometry);
            }
        }

        public string IconGeometry
        {
            get => (string)GetValue(IconGeometryProperty);
            set => SetValue(IconGeometryProperty, value);
        }

        public static readonly DependencyProperty PositionProperty = DependencyProperty.Register(
            nameof(Position), typeof(ButtonPosition), typeof(BoardToolbarButton),
            new PropertyMetadata(ButtonPosition.Middle, OnPositionChanged));

        private static void OnPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var button = (BoardToolbarButton)d;
            button.UpdateCornerRadius((ButtonPosition)e.NewValue);
        }

        public ButtonPosition Position
        {
            get => (ButtonPosition)GetValue(PositionProperty);
            set => SetValue(PositionProperty, value);
        }

        public static readonly DependencyProperty IconBrushProperty = DependencyProperty.Register(
            nameof(IconBrush), typeof(Brush), typeof(BoardToolbarButton),
            new PropertyMetadata(null, OnIconBrushChanged));

        private static void OnIconBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var button = (BoardToolbarButton)d;
            if (button.IconGeometryInternal != null && e.NewValue is Brush brush)
            {
                button.IconGeometryInternal.Brush = brush;
            }
        }

        public Brush IconBrush
        {
            get => (Brush)GetValue(IconBrushProperty);
            set => SetValue(IconBrushProperty, value);
        }



        public event MouseButtonEventHandler ButtonMouseDown;
        public event MouseButtonEventHandler ButtonMouseUp;

        public GeometryDrawing IconGeometryDrawing => IconGeometryInternal;
        public GeometryDrawing IconGeometryDrawing2 => IconGeometryInternal2;

        public Border ButtonBorderControl => ButtonBorder;

        public TextBlock LabelTextBlockControl => LabelTextBlock;

        public new Brush Background
        {
            get => ButtonBorder.Background;
            set => ButtonBorder.Background = value;
        }

        public new Brush BorderBrush
        {
            get => ButtonBorder.BorderBrush;
            set => ButtonBorder.BorderBrush = value;
        }

        public new Brush Foreground
        {
            get => LabelTextBlock.Foreground;
            set => LabelTextBlock.Foreground = value;
        }

        public static readonly DependencyProperty IsEnabledBindingProperty = DependencyProperty.Register(
            nameof(IsEnabledBinding), typeof(bool?), typeof(BoardToolbarButton),
            new PropertyMetadata(null, OnIsEnabledBindingChanged));

        private static void OnIsEnabledBindingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var button = (BoardToolbarButton)d;
            if (e.NewValue is bool isEnabled)
            {
                button.IsEnabled = isEnabled;
                button.UpdateIconOpacity(isEnabled);
            }
        }

        public bool? IsEnabledBinding
        {
            get => (bool?)GetValue(IsEnabledBindingProperty);
            set => SetValue(IsEnabledBindingProperty, value);
        }

        private void UpdateIconOpacity(bool isEnabled)
        {
            double opacity = isEnabled ? 1.0 : 0.4;
            if (ButtonImage != null)
            {
                ButtonImage.Opacity = opacity;
            }
            if (LabelTextBlock != null)
            {
                LabelTextBlock.Opacity = opacity;
            }
        }

        public BoardToolbarButton()
        {
            InitializeComponent();
            Loaded += BoardToolbarButton_Loaded;
        }

        private void BoardToolbarButton_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateCornerRadius(Position);
            if (!string.IsNullOrEmpty(IconGeometry))
            {
                IconGeometryInternal.Geometry = Geometry.Parse(IconGeometry);
            }
        }

        private void UpdateCornerRadius(ButtonPosition position)
        {
            if (ButtonBorder == null) return;

            switch (position)
            {
                case ButtonPosition.First:
                    ButtonBorder.CornerRadius = new CornerRadius(5, 0, 0, 5);
                    break;
                case ButtonPosition.Middle:
                    ButtonBorder.CornerRadius = new CornerRadius(0);
                    break;
                case ButtonPosition.Last:
                    ButtonBorder.CornerRadius = new CornerRadius(0, 5, 5, 0);
                    break;
                case ButtonPosition.Single:
                    ButtonBorder.CornerRadius = new CornerRadius(5);
                    break;
            }
        }

        private void ButtonBorder_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ButtonMouseDown?.Invoke(this, e);
        }

        private void ButtonBorder_MouseUp(object sender, MouseButtonEventArgs e)
        {
            ButtonMouseUp?.Invoke(this, e);
        }
    }
}
