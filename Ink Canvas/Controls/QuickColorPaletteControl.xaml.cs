using Ink_Canvas.Windows.SettingsViews.Helpers;
using System.Windows;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Ink_Canvas.Controls
{
    public partial class QuickColorPaletteControl : System.Windows.Controls.UserControl, INotifyPropertyChanged
    {
        public static readonly RoutedEvent ColorClickedEvent = EventManager.RegisterRoutedEvent(
            nameof(ColorClicked), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(QuickColorPaletteControl));

        public event RoutedEventHandler ColorClicked
        {
            add => AddHandler(ColorClickedEvent, value);
            remove => RemoveHandler(ColorClickedEvent, value);
        }

        public static readonly DependencyProperty DisplayModeProperty =
            DependencyProperty.Register(nameof(DisplayMode), typeof(int), typeof(QuickColorPaletteControl),
                new PropertyMetadata(1, OnDisplayModeChanged));

        public int DisplayMode
        {
            get => (int)GetValue(DisplayModeProperty);
            set => SetValue(DisplayModeProperty, value);
        }

        private static void OnDisplayModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (QuickColorPaletteControl)d;
            control.ApplyDisplayMode();
        }

        public QuickColorPaletteControl()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplyDisplayMode();
        }

        private void ApplyDisplayMode()
        {
            if (QuickColorPalettePanel == null || QuickColorPaletteSingleRowPanel == null) return;

            if (DisplayMode == 0)
            {
                QuickColorPalettePanel.Visibility = Visibility.Collapsed;
                QuickColorPaletteSingleRowPanel.Visibility = Visibility.Visible;
            }
            else
            {
                QuickColorPalettePanel.Visibility = Visibility.Visible;
                QuickColorPaletteSingleRowPanel.Visibility = Visibility.Collapsed;
            }
        }

        public void SyncFromSettings()
        {
            var settings = SettingsManager.Settings;
            if (settings?.Appearance == null) return;
            DisplayMode = settings.Appearance.QuickColorPaletteDisplayMode;
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

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
