using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.MainWindow_controls
{
    public partial class PenColorPalette : UserControl
    {
        public static readonly DependencyProperty SelectedColorProperty = DependencyProperty.Register(
            nameof(SelectedColor), typeof(Color), typeof(PenColorPalette), new PropertyMetadata(Colors.Black));

        public static readonly DependencyProperty ColorPaletteProperty = DependencyProperty.Register(
            nameof(ColorPalette), typeof(ObservableCollection<Color>), typeof(PenColorPalette), 
            new PropertyMetadata(new ObservableCollection<Color>()));

        public static readonly DependencyProperty IsBoardModeProperty = DependencyProperty.Register(
            nameof(IsBoardMode), typeof(bool), typeof(PenColorPalette), new PropertyMetadata(false));

        public static readonly RoutedEvent ColorSelectedEvent = EventManager.RegisterRoutedEvent(
            nameof(ColorSelected), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PenColorPalette));

        public static readonly RoutedEvent ThemeSwitchClickedEvent = EventManager.RegisterRoutedEvent(
            nameof(ThemeSwitchClicked), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PenColorPalette));

        public Color SelectedColor
        {
            get => (Color)GetValue(SelectedColorProperty);
            set => SetValue(SelectedColorProperty, value);
        }

        public ObservableCollection<Color> ColorPalette
        {
            get => (ObservableCollection<Color>)GetValue(ColorPaletteProperty);
            set => SetValue(ColorPaletteProperty, value);
        }

        public bool IsBoardMode
        {
            get => (bool)GetValue(IsBoardModeProperty);
            set => SetValue(IsBoardModeProperty, value);
        }

        public event RoutedEventHandler ColorSelected
        {
            add => AddHandler(ColorSelectedEvent, value);
            remove => RemoveHandler(ColorSelectedEvent, value);
        }

        public event RoutedEventHandler ThemeSwitchClicked
        {
            add => AddHandler(ThemeSwitchClickedEvent, value);
            remove => RemoveHandler(ThemeSwitchClickedEvent, value);
        }

        public PenColorPalette()
        {
            InitializeComponent();
        }

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
        }

        private void ColorThemeSwitch_MouseUp(object sender, MouseButtonEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(ThemeSwitchClickedEvent, this));
        }

        private void BtnColorBlack_Click(object sender, MouseButtonEventArgs e)
        {
            SelectedColor = Colors.Black;
            RaiseEvent(new RoutedEventArgs(ColorSelectedEvent, this));
        }

        private void BtnColorWhite_Click(object sender, MouseButtonEventArgs e)
        {
            SelectedColor = Colors.White;
            RaiseEvent(new RoutedEventArgs(ColorSelectedEvent, this));
        }

        private void BtnColorRed_Click(object sender, MouseButtonEventArgs e)
        {
            SelectedColor = (Color)ColorConverter.ConvertFromString("#dc2626");
            RaiseEvent(new RoutedEventArgs(ColorSelectedEvent, this));
        }

        private void BtnColorYellow_Click(object sender, MouseButtonEventArgs e)
        {
            SelectedColor = (Color)ColorConverter.ConvertFromString("#eab308");
            RaiseEvent(new RoutedEventArgs(ColorSelectedEvent, this));
        }

        private void BtnColorGreen_Click(object sender, MouseButtonEventArgs e)
        {
            SelectedColor = (Color)ColorConverter.ConvertFromString("#16a34a");
            RaiseEvent(new RoutedEventArgs(ColorSelectedEvent, this));
        }

        private void BtnColorBlue_Click(object sender, MouseButtonEventArgs e)
        {
            SelectedColor = (Color)ColorConverter.ConvertFromString("#2563eb");
            RaiseEvent(new RoutedEventArgs(ColorSelectedEvent, this));
        }

        private void BtnColorPink_Click(object sender, MouseButtonEventArgs e)
        {
            SelectedColor = (Color)ColorConverter.ConvertFromString("#db2777");
            RaiseEvent(new RoutedEventArgs(ColorSelectedEvent, this));
        }

        private void BtnColorTeal_Click(object sender, MouseButtonEventArgs e)
        {
            SelectedColor = (Color)ColorConverter.ConvertFromString("#0d9488");
            RaiseEvent(new RoutedEventArgs(ColorSelectedEvent, this));
        }

        private void BtnColorOrange_Click(object sender, MouseButtonEventArgs e)
        {
            SelectedColor = (Color)ColorConverter.ConvertFromString("#ea580c");
            RaiseEvent(new RoutedEventArgs(ColorSelectedEvent, this));
        }
    }
}
