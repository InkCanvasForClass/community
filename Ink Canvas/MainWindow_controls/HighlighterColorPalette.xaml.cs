using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.MainWindow_controls
{
    public partial class HighlighterColorPalette : UserControl
    {
        public static readonly DependencyProperty SelectedColorProperty = DependencyProperty.Register(
            nameof(SelectedColor), typeof(Color), typeof(HighlighterColorPalette), new PropertyMetadata(Colors.Black));

        public static readonly DependencyProperty IsBoardModeProperty = DependencyProperty.Register(
            nameof(IsBoardMode), typeof(bool), typeof(HighlighterColorPalette), new PropertyMetadata(false));

        public static readonly RoutedEvent ColorSelectedEvent = EventManager.RegisterRoutedEvent(
            nameof(ColorSelected), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(HighlighterColorPalette));

        public Color SelectedColor
        {
            get => (Color)GetValue(SelectedColorProperty);
            set => SetValue(SelectedColorProperty, value);
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

        public HighlighterColorPalette()
        {
            InitializeComponent();
        }

        private void BtnHighlighterColorBlack_Click(object sender, MouseButtonEventArgs e)
        {
            SelectedColor = Colors.Black;
            RaiseEvent(new RoutedEventArgs(ColorSelectedEvent, this));
        }

        private void BtnHighlighterColorWhite_Click(object sender, MouseButtonEventArgs e)
        {
            SelectedColor = Colors.White;
            RaiseEvent(new RoutedEventArgs(ColorSelectedEvent, this));
        }

        private void BtnHighlighterColorRed_Click(object sender, MouseButtonEventArgs e)
        {
            SelectedColor = (Color)ColorConverter.ConvertFromString("#ef4444");
            RaiseEvent(new RoutedEventArgs(ColorSelectedEvent, this));
        }

        private void BtnHighlighterColorYellow_Click(object sender, MouseButtonEventArgs e)
        {
            SelectedColor = (Color)ColorConverter.ConvertFromString("#eab308");
            RaiseEvent(new RoutedEventArgs(ColorSelectedEvent, this));
        }

        private void BtnHighlighterColorGreen_Click(object sender, MouseButtonEventArgs e)
        {
            SelectedColor = (Color)ColorConverter.ConvertFromString("#22c55e");
            RaiseEvent(new RoutedEventArgs(ColorSelectedEvent, this));
        }

        private void BtnHighlighterColorOrange_Click(object sender, MouseButtonEventArgs e)
        {
            SelectedColor = (Color)ColorConverter.ConvertFromString("#f97316");
            RaiseEvent(new RoutedEventArgs(ColorSelectedEvent, this));
        }
    }
}
