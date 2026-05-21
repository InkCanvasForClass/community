using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Ink_Canvas.Controls
{
    public partial class SidePanelToggle : UserControl
    {
        public static readonly DependencyProperty IsRightSideProperty = DependencyProperty.Register(
            nameof(IsRightSide), typeof(bool), typeof(SidePanelToggle),
            new PropertyMetadata(false, OnIsRightSideChanged));

        private static void OnIsRightSideChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (SidePanelToggle)d;
            if ((bool)e.NewValue)
            {
                control.PanelBorder.CornerRadius = new CornerRadius(25, 0, 0, 25);
                control.ChevronImage.Margin = new Thickness(0, 0, 10, 0);
                control.ChevronImage.RenderTransformOrigin = new Point(0.5, 0.5);
                control.ChevronImage.RenderTransform = new RotateTransform(180);
            }
            else
            {
                control.PanelBorder.CornerRadius = new CornerRadius(0, 25, 25, 0);
                control.ChevronImage.Margin = new Thickness(10, 0, 0, 0);
                control.ChevronImage.RenderTransform = null;
            }
        }

        public bool IsRightSide
        {
            get => (bool)GetValue(IsRightSideProperty);
            set => SetValue(IsRightSideProperty, value);
        }

        public Image ChevronIcon => ChevronImage;

        public SidePanelToggle()
        {
            InitializeComponent();
        }
    }
}
