using System.Windows;
using System.Windows.Input;

namespace Ink_Canvas.Controls
{
    public partial class QuickColorPaletteControl : System.Windows.Controls.UserControl
    {
        public static readonly RoutedEvent ColorClickedEvent = EventManager.RegisterRoutedEvent(
            nameof(ColorClicked), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(QuickColorPaletteControl));

        public event RoutedEventHandler ColorClicked
        {
            add => AddHandler(ColorClickedEvent, value);
            remove => RemoveHandler(ColorClickedEvent, value);
        }

        public QuickColorPaletteControl()
        {
            InitializeComponent();
        }

        private void ColorButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
        }

        private void ColorButton_MouseLeave(object sender, MouseEventArgs e)
        {
        }

        private void QuickColorBlack_Click(object sender, RoutedEventArgs e)
            => RaiseEvent(new RoutedEventArgs(ColorClickedEvent, "Black"));

        private void QuickColorWhite_Click(object sender, RoutedEventArgs e)
            => RaiseEvent(new RoutedEventArgs(ColorClickedEvent, "White"));

        private void QuickColorRed_Click(object sender, RoutedEventArgs e)
            => RaiseEvent(new RoutedEventArgs(ColorClickedEvent, "Red"));

        private void QuickColorOrange_Click(object sender, RoutedEventArgs e)
            => RaiseEvent(new RoutedEventArgs(ColorClickedEvent, "Orange"));

        private void QuickColorYellow_Click(object sender, RoutedEventArgs e)
            => RaiseEvent(new RoutedEventArgs(ColorClickedEvent, "Yellow"));

        private void QuickColorGreen_Click(object sender, RoutedEventArgs e)
            => RaiseEvent(new RoutedEventArgs(ColorClickedEvent, "Green"));

        private void QuickColorBlue_Click(object sender, RoutedEventArgs e)
            => RaiseEvent(new RoutedEventArgs(ColorClickedEvent, "Blue"));

        private void QuickColorPurple_Click(object sender, RoutedEventArgs e)
            => RaiseEvent(new RoutedEventArgs(ColorClickedEvent, "Purple"));
    }
}
