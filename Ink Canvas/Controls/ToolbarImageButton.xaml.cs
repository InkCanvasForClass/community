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

        public GeometryDrawing GeometryDrawing => IconGeometryInternal;

        public new Brush Background
        {
            get => ButtonPanel.Background;
            set => ButtonPanel.Background = value;
        }

        public event MouseButtonEventHandler ButtonMouseDown;
        public event MouseEventHandler ButtonMouseLeave;
        public event RoutedEventHandler ButtonMouseUp;

        public ToolbarImageButton()
        {
            InitializeComponent();
            ButtonPanel.Background = Brushes.Transparent;
        }

        private void ButtonPanel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_lastPressedButton != null && _lastPressedButton != this)
            {
                _lastPressedButton.Background = Brushes.Transparent;
            }
            _lastPressedButton = this;
            ButtonPanel.Background = new SolidColorBrush(Color.FromArgb(28, 24, 24, 27));
            ButtonMouseDown?.Invoke(this, e);
        }

        private void ButtonPanel_MouseLeave(object sender, MouseEventArgs e)
        {
            ButtonMouseLeave?.Invoke(this, e);
        }

        private void ButtonPanel_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_lastPressedButton == this)
            {
                ButtonPanel.Background = Brushes.Transparent;
                _lastPressedButton = null;
            }
            ButtonMouseUp?.Invoke(this, new RoutedEventArgs(e.RoutedEvent, this));
        }
    }
}