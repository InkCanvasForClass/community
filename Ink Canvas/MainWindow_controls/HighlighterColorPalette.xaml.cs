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

        public static readonly DependencyProperty SelectedColorCodeProperty = DependencyProperty.Register(
            nameof(SelectedColorCode), typeof(int), typeof(HighlighterColorPalette),
            new PropertyMetadata(100, OnSelectedColorCodeChanged));

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

        public int SelectedColorCode
        {
            get => (int)GetValue(SelectedColorCodeProperty);
            set => SetValue(SelectedColorCodeProperty, value);
        }

        public event RoutedEventHandler ColorSelected
        {
            add => AddHandler(ColorSelectedEvent, value);
            remove => RemoveHandler(ColorSelectedEvent, value);
        }

        public HighlighterColorPalette()
        {
            InitializeComponent();
            UpdateSelectionIndicators(SelectedColorCode);
        }

        private void BtnHighlighterColorBlack_Click(object sender, MouseButtonEventArgs e)
        {
            SelectColor(100, Colors.Black);
        }

        private void BtnHighlighterColorWhite_Click(object sender, MouseButtonEventArgs e)
        {
            SelectColor(101, Colors.White);
        }

        private void BtnHighlighterColorRed_Click(object sender, MouseButtonEventArgs e)
        {
            SelectColor(102, (Color)ColorConverter.ConvertFromString("#ef4444"));
        }

        private void BtnHighlighterColorYellow_Click(object sender, MouseButtonEventArgs e)
        {
            SelectColor(103, (Color)ColorConverter.ConvertFromString("#eab308"));
        }

        private void BtnHighlighterColorGreen_Click(object sender, MouseButtonEventArgs e)
        {
            SelectColor(104, (Color)ColorConverter.ConvertFromString("#22c55e"));
        }

        private void BtnHighlighterColorZinc_Click(object sender, MouseButtonEventArgs e)
        {
            SelectColor(105, (Color)ColorConverter.ConvertFromString("#71717a"));
        }

        private void BtnHighlighterColorBlue_Click(object sender, MouseButtonEventArgs e)
        {
            SelectColor(106, (Color)ColorConverter.ConvertFromString("#3b82f6"));
        }

        private void BtnHighlighterColorPurple_Click(object sender, MouseButtonEventArgs e)
        {
            SelectColor(107, (Color)ColorConverter.ConvertFromString("#a855f7"));
        }

        private void BtnHighlighterColorTeal_Click(object sender, MouseButtonEventArgs e)
        {
            SelectColor(108, (Color)ColorConverter.ConvertFromString("#14b8a6"));
        }

        private void BtnHighlighterColorOrange_Click(object sender, MouseButtonEventArgs e)
        {
            SelectColor(109, (Color)ColorConverter.ConvertFromString("#f97316"));
        }

        public void SetSelectedColorCode(int colorCode)
        {
            SelectedColorCode = colorCode;
        }

        private static void OnSelectedColorCodeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HighlighterColorPalette palette)
            {
                palette.UpdateSelectionIndicators((int)e.NewValue);
            }
        }

        private void SelectColor(int colorCode, Color color)
        {
            SelectedColor = color;
            SelectedColorCode = colorCode;
            RaiseEvent(new RoutedEventArgs(ColorSelectedEvent, this));
        }

        private void UpdateSelectionIndicators(int colorCode)
        {
            foreach (var indicator in GetAllIndicators())
            {
                indicator.Visibility = Visibility.Collapsed;
            }

            switch (colorCode)
            {
                case 100:
                    HighlighterPenViewboxBtnColorBlackContent.Visibility = Visibility.Visible;
                    BoardHighlighterPenViewboxBtnColorBlackContent.Visibility = Visibility.Visible;
                    break;
                case 101:
                    HighlighterPenViewboxBtnColorWhiteContent.Visibility = Visibility.Visible;
                    BoardHighlighterPenViewboxBtnColorWhiteContent.Visibility = Visibility.Visible;
                    break;
                case 102:
                    HighlighterPenViewboxBtnColorRedContent.Visibility = Visibility.Visible;
                    BoardHighlighterPenViewboxBtnColorRedContent.Visibility = Visibility.Visible;
                    break;
                case 103:
                    HighlighterPenViewboxBtnColorYellowContent.Visibility = Visibility.Visible;
                    BoardHighlighterPenViewboxBtnColorYellowContent.Visibility = Visibility.Visible;
                    break;
                case 104:
                    HighlighterPenViewboxBtnColorGreenContent.Visibility = Visibility.Visible;
                    BoardHighlighterPenViewboxBtnColorGreenContent.Visibility = Visibility.Visible;
                    break;
                case 105:
                    HighlighterPenViewboxBtnColorZincContent.Visibility = Visibility.Visible;
                    BoardHighlighterPenViewboxBtnColorZincContent.Visibility = Visibility.Visible;
                    break;
                case 106:
                    HighlighterPenViewboxBtnColorBlueContent.Visibility = Visibility.Visible;
                    BoardHighlighterPenViewboxBtnColorBlueContent.Visibility = Visibility.Visible;
                    break;
                case 107:
                    HighlighterPenViewboxBtnColorPurpleContent.Visibility = Visibility.Visible;
                    BoardHighlighterPenViewboxBtnColorPurpleContent.Visibility = Visibility.Visible;
                    break;
                case 108:
                    HighlighterPenViewboxBtnColorTealContent.Visibility = Visibility.Visible;
                    BoardHighlighterPenViewboxBtnColorTealContent.Visibility = Visibility.Visible;
                    break;
                case 109:
                    HighlighterPenViewboxBtnColorOrangeContent.Visibility = Visibility.Visible;
                    BoardHighlighterPenViewboxBtnColorOrangeContent.Visibility = Visibility.Visible;
                    break;
            }
        }

        private Viewbox[] GetAllIndicators()
        {
            return new[]
            {
                HighlighterPenViewboxBtnColorBlackContent,
                HighlighterPenViewboxBtnColorWhiteContent,
                HighlighterPenViewboxBtnColorRedContent,
                HighlighterPenViewboxBtnColorYellowContent,
                HighlighterPenViewboxBtnColorGreenContent,
                HighlighterPenViewboxBtnColorZincContent,
                HighlighterPenViewboxBtnColorBlueContent,
                HighlighterPenViewboxBtnColorPurpleContent,
                HighlighterPenViewboxBtnColorTealContent,
                HighlighterPenViewboxBtnColorOrangeContent,
                BoardHighlighterPenViewboxBtnColorBlackContent,
                BoardHighlighterPenViewboxBtnColorWhiteContent,
                BoardHighlighterPenViewboxBtnColorRedContent,
                BoardHighlighterPenViewboxBtnColorYellowContent,
                BoardHighlighterPenViewboxBtnColorGreenContent,
                BoardHighlighterPenViewboxBtnColorZincContent,
                BoardHighlighterPenViewboxBtnColorBlueContent,
                BoardHighlighterPenViewboxBtnColorPurpleContent,
                BoardHighlighterPenViewboxBtnColorTealContent,
                BoardHighlighterPenViewboxBtnColorOrangeContent,
            };
        }
    }
}
