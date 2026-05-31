using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.Controls
{
    public partial class ToolbarImageButton : UserControl
    {
        private static ToolbarImageButton _lastPressedButton;

        public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
            nameof(Label), typeof(string), typeof(ToolbarImageButton),
            new PropertyMetadata(string.Empty, (d, e) => ((ToolbarImageButton)d).LabelTextBlock.Text = (string)e.NewValue));

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public static readonly DependencyProperty IconGeometryDrawingProperty = DependencyProperty.Register(
            nameof(IconGeometryDrawing), typeof(GeometryDrawing), typeof(ToolbarImageButton),
            new PropertyMetadata(null, OnIconGeometryDrawingChanged));

        private static void OnIconGeometryDrawingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var button = (ToolbarImageButton)d;
            if (e.NewValue is GeometryDrawing newDrawing)
            {
                button.IconGeometryInternal.Geometry = newDrawing.Geometry;
                button.IconGeometryInternal.Brush = newDrawing.Brush;
            }
        }

        public GeometryDrawing IconGeometryDrawing
        {
            get => (GeometryDrawing)GetValue(IconGeometryDrawingProperty);
            set => SetValue(IconGeometryDrawingProperty, value);
        }

        public GeometryDrawing Icon => IconGeometryInternal;

        public GeometryDrawing Badge => BadgeGeometryInternal;

        public GeometryDrawing GeometryDrawing => IconGeometryInternal;

        public static readonly DependencyProperty IconBrushProperty = DependencyProperty.Register(
            nameof(IconBrush), typeof(Brush), typeof(ToolbarImageButton),
            new PropertyMetadata(null, (d, e) => ((ToolbarImageButton)d).IconGeometryInternal.Brush = (Brush)e.NewValue));

        public Brush IconBrush
        {
            get => (Brush)GetValue(IconBrushProperty);
            set => SetValue(IconBrushProperty, value);
        }

        public static readonly DependencyProperty LabelBrushProperty = DependencyProperty.Register(
            nameof(LabelBrush), typeof(Brush), typeof(ToolbarImageButton),
            new PropertyMetadata(null, (d, e) =>
            {
                var b = (ToolbarImageButton)d;
                if (e.NewValue is Brush brush) b.LabelTextBlock.Foreground = brush;
            }));

        public Brush LabelBrush
        {
            get => (Brush)GetValue(LabelBrushProperty);
            set => SetValue(LabelBrushProperty, value);
        }

        public new Brush Background
        {
            get => ButtonBorder.Background;
            set => ButtonBorder.Background = value;
        }

        public double LabelFontSize
        {
            get => LabelTextBlock.FontSize;
            set => LabelTextBlock.FontSize = value;
        }

        public double IconHeight
        {
            get => ButtonImage.Height;
            set => ButtonImage.Height = value;
        }

        public void ApplyOrientation(bool isVertical)
        {
            if (isVertical)
            {
                ButtonPanel.Width = 43;
                ButtonPanel.Height = 44;
                ButtonBorder.Margin = new Thickness(2, 0, 7, 0);
            }
            else
            {
                ButtonPanel.Width = 44;
                ButtonPanel.Height = 43;
                ButtonBorder.Margin = new Thickness(0, 2, 0, 7);
            }
        }

        public event MouseButtonEventHandler ButtonMouseDown;
        public event MouseEventHandler ButtonMouseLeave;
        public event MouseButtonEventHandler ButtonMouseUp;

        public ToolbarImageButton()
        {
            InitializeComponent();
            ButtonPanel.Background = Brushes.Transparent;
            IsEnabledChanged += ToolbarImageButton_IsEnabledChanged;
        }

        private void ToolbarImageButton_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var isEnabled = (bool)e.NewValue;
            ButtonImage.Opacity = isEnabled ? 1.0 : 0.5;
            LabelTextBlock.Opacity = isEnabled ? 1.0 : 0.5;
            ButtonPanel.IsEnabled = isEnabled;
        }

        private void ButtonPanel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsEnabled) return;
            if (_lastPressedButton != null && _lastPressedButton != this)
            {
                _lastPressedButton.ButtonBorder.Background = Brushes.Transparent;
            }
            _lastPressedButton = this;
            ButtonBorder.Background = new SolidColorBrush(Color.FromArgb(28, 24, 24, 27));
            ButtonMouseDown?.Invoke(this, e);
        }

        private void ButtonPanel_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!IsEnabled) return;
            ButtonMouseLeave?.Invoke(this, e);
        }

        private void ButtonPanel_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!IsEnabled) return;
            if (_lastPressedButton == this)
            {
                ButtonBorder.Background = Brushes.Transparent;
                _lastPressedButton = null;
            }
            ButtonMouseUp?.Invoke(this, e);
        }
    }
}
