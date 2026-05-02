using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.Controls
{
    public partial class ToolMenuButton : UserControl
    {
        private Geometry _pendingGeometry;
        private Brush _pendingBrush;
        private static ToolMenuButton _lastPressedButton;

        public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
            nameof(Label), typeof(string), typeof(ToolMenuButton),
            new PropertyMetadata(string.Empty, OnLabelChanged));

        private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var button = (ToolMenuButton)d;
            if (button.LabelControl != null)
                button.LabelControl.Content = (string)e.NewValue;
        }

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public static readonly DependencyProperty IconGeometryProperty = DependencyProperty.Register(
            nameof(IconGeometry), typeof(string), typeof(ToolMenuButton),
            new PropertyMetadata(string.Empty, OnIconGeometryChanged));

        private static void OnIconGeometryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var button = (ToolMenuButton)d;
            if (e.NewValue is string geometry && !string.IsNullOrEmpty(geometry))
            {
                button._pendingGeometry = Geometry.Parse(geometry);
                if (button.IconGeometryInternal != null)
                    button.IconGeometryInternal.Geometry = button._pendingGeometry;
            }
        }

        public string IconGeometry
        {
            get => (string)GetValue(IconGeometryProperty);
            set => SetValue(IconGeometryProperty, value);
        }

        public static readonly DependencyProperty IconBrushProperty = DependencyProperty.Register(
            nameof(IconBrush), typeof(Brush), typeof(ToolMenuButton),
            new PropertyMetadata(null, OnIconBrushChanged));

        private static void OnIconBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var button = (ToolMenuButton)d;
            button._pendingBrush = (Brush)e.NewValue;
            if (button.IconGeometryInternal != null)
                button.IconGeometryInternal.Brush = button._pendingBrush;
        }

        public Brush IconBrush
        {
            get => (Brush)GetValue(IconBrushProperty);
            set => SetValue(IconBrushProperty, value);
        }

        public new Brush Background
        {
            get => ButtonBorder.Background;
            set => ButtonBorder.Background = value;
        }

        public GeometryDrawing Icon
        {
            get
            {
                if (IconGeometryInternal != null)
                    return IconGeometryInternal;
                return new GeometryDrawing(_pendingBrush, null, _pendingGeometry);
            }
        }

        public event MouseButtonEventHandler ButtonMouseDown;
        public event MouseEventHandler ButtonMouseLeave;
        public event MouseButtonEventHandler ButtonMouseUp;

        public ToolMenuButton()
        {
            InitializeComponent();
            Loaded += ToolMenuButton_Loaded;
            ButtonBorder.Background = Brushes.Transparent;
        }

        private void ToolMenuButton_Loaded(object sender, RoutedEventArgs e)
        {
            if (IconGeometryInternal != null)
            {
                if (_pendingGeometry != null)
                    IconGeometryInternal.Geometry = _pendingGeometry;
                if (_pendingBrush != null)
                    IconGeometryInternal.Brush = _pendingBrush;
            }
        }

        private void ButtonPanel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsEnabled) return;
            if (_lastPressedButton != null && _lastPressedButton != this)
            {
                _lastPressedButton.ButtonBorder.Background = Brushes.Transparent;
            }
            _lastPressedButton = this;
            ButtonBorder.Background = new SolidColorBrush(Color.FromArgb(80, 24, 24, 27));
            ButtonMouseDown?.Invoke(this, e);
        }

        private void ButtonPanel_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!IsEnabled) return;
            if (_lastPressedButton == this)
            {
                ButtonBorder.Background = Brushes.Transparent;
                _lastPressedButton = null;
            }
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