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

        public static readonly DependencyProperty SelectedColorCodeProperty = DependencyProperty.Register(
            nameof(SelectedColorCode), typeof(int), typeof(PenColorPalette), new PropertyMetadata(0, OnSelectedColorCodeChanged));

        public static readonly DependencyProperty ColorPaletteProperty = DependencyProperty.Register(
            nameof(ColorPalette), typeof(ObservableCollection<Color>), typeof(PenColorPalette), 
            new PropertyMetadata(new ObservableCollection<Color>()));

        public static readonly DependencyProperty IsBoardModeProperty = DependencyProperty.Register(
            nameof(IsBoardMode), typeof(bool), typeof(PenColorPalette), new PropertyMetadata(false));

        public static readonly DependencyProperty UseLightThemeColorsProperty = DependencyProperty.Register(
            nameof(UseLightThemeColors), typeof(bool), typeof(PenColorPalette), new PropertyMetadata(false, OnUseLightThemeColorsChanged));

        public static new readonly DependencyProperty VisibilityProperty = DependencyProperty.Register(
            nameof(Visibility), typeof(Visibility), typeof(PenColorPalette),
            new FrameworkPropertyMetadata(Visibility.Visible, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVisibilityChanged));

        public static readonly RoutedEvent ColorSelectedEvent = EventManager.RegisterRoutedEvent(
            nameof(ColorSelected), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PenColorPalette));

        public static readonly RoutedEvent ThemeSwitchClickedEvent = EventManager.RegisterRoutedEvent(
            nameof(ThemeSwitchClicked), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PenColorPalette));

        public Color SelectedColor
        {
            get => (Color)GetValue(SelectedColorProperty);
            set => SetValue(SelectedColorProperty, value);
        }

        public int SelectedColorCode
        {
            get => (int)GetValue(SelectedColorCodeProperty);
            set => SetValue(SelectedColorCodeProperty, value);
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

        public bool UseLightThemeColors
        {
            get => (bool)GetValue(UseLightThemeColorsProperty);
            set => SetValue(UseLightThemeColorsProperty, value);
        }

        public Border BorderSelectionControl => BorderPenColorPaletteSelectionControl;

        public Thickness ControlMargin
        {
            get => BorderPenColorPaletteSelectionControl.Margin;
            set => BorderPenColorPaletteSelectionControl.Margin = value;
        }

        public UIElement ControlChild => BorderPenColorPaletteSelectionControl.Child;

        public new Visibility Visibility
        {
            get => (Visibility)GetValue(VisibilityProperty);
            set => SetValue(VisibilityProperty, value);
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
            UpdateThemeVisuals(UseLightThemeColors);
            UpdateSelectionIndicators(SelectedColorCode);
        }

        public void InvalidateVisualOnControl()
        {
            BorderPenColorPaletteSelectionControl.InvalidateVisual();
        }

        public void SetSelectedColorCode(int colorCode)
        {
            SelectedColorCode = colorCode;
        }

        public void SetUseLightThemeColors(bool useLightThemeColors)
        {
            UseLightThemeColors = useLightThemeColors;
        }

        private static void OnSelectedColorCodeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PenColorPalette palette)
            {
                palette.UpdateSelectionIndicators((int)e.NewValue);
            }
        }

        private static void OnUseLightThemeColorsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PenColorPalette palette)
            {
                palette.UpdateThemeVisuals((bool)e.NewValue);
            }
        }

        private static void OnVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PenColorPalette palette)
            {
                palette.BorderPenColorPaletteSelectionControl.Visibility = (Visibility)e.NewValue;
            }
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
            SelectColor(0, Colors.Black);
        }

        private void BtnColorWhite_Click(object sender, MouseButtonEventArgs e)
        {
            SelectColor(5, Colors.White);
        }

        private void BtnColorRed_Click(object sender, MouseButtonEventArgs e)
        {
            SelectColor(1, GetCurrentPaletteColor(PaletteColor.Red));
        }

        private void BtnColorYellow_Click(object sender, MouseButtonEventArgs e)
        {
            SelectColor(4, GetCurrentPaletteColor(PaletteColor.Yellow));
        }

        private void BtnColorGreen_Click(object sender, MouseButtonEventArgs e)
        {
            SelectColor(2, GetCurrentPaletteColor(PaletteColor.Green));
        }

        private void BtnColorBlue_Click(object sender, MouseButtonEventArgs e)
        {
            SelectColor(3, GetCurrentPaletteColor(PaletteColor.Blue));
        }

        private void BtnColorPink_Click(object sender, MouseButtonEventArgs e)
        {
            SelectColor(6, GetCurrentPaletteColor(PaletteColor.Pink));
        }

        private void BtnColorTeal_Click(object sender, MouseButtonEventArgs e)
        {
            SelectColor(7, GetCurrentPaletteColor(PaletteColor.Teal));
        }

        private void BtnColorOrange_Click(object sender, MouseButtonEventArgs e)
        {
            SelectColor(8, GetCurrentPaletteColor(PaletteColor.Orange));
        }

        private void SelectColor(int colorCode, Color color)
        {
            SelectedColor = color;
            SelectedColorCode = colorCode;
            RaiseEvent(new RoutedEventArgs(ColorSelectedEvent, this));
        }

        private void UpdateThemeVisuals(bool useLightThemeColors)
        {
            ColorThemeSwitchIcon.Source = CreateThemeIcon(useLightThemeColors);
            BoardColorThemeSwitchIcon.Source = CreateThemeIcon(useLightThemeColors);

            string switchText = useLightThemeColors ? "暗系" : "亮系";
            ColorThemeSwitchTextBlock.Text = switchText;
            BoardColorThemeSwitchTextBlock.Text = switchText;

            ApplyPaletteColor(BorderPenColorRed, BoardBorderPenColorRed, PaletteColor.Red, useLightThemeColors);
            ApplyPaletteColor(BorderPenColorGreen, BoardBorderPenColorGreen, PaletteColor.Green, useLightThemeColors);
            ApplyPaletteColor(BorderPenColorBlue, BoardBorderPenColorBlue, PaletteColor.Blue, useLightThemeColors);
            ApplyPaletteColor(BorderPenColorYellow, BoardBorderPenColorYellow, PaletteColor.Yellow, useLightThemeColors);
            ApplyPaletteColor(BorderPenColorPink, BoardBorderPenColorPink, PaletteColor.Pink, useLightThemeColors);
            ApplyPaletteColor(BorderPenColorTeal, BoardBorderPenColorTeal, PaletteColor.Teal, useLightThemeColors);
            ApplyPaletteColor(BorderPenColorOrange, BoardBorderPenColorOrange, PaletteColor.Orange, useLightThemeColors);
        }

        private void UpdateSelectionIndicators(int colorCode)
        {
            foreach (var indicator in GetAllIndicators())
            {
                indicator.Visibility = Visibility.Collapsed;
            }

            switch (colorCode)
            {
                case 0:
                    ViewboxBtnColorBlackContent.Visibility = Visibility.Visible;
                    BoardViewboxBtnColorBlackContent.Visibility = Visibility.Visible;
                    break;
                case 1:
                    ViewboxBtnColorRedContent.Visibility = Visibility.Visible;
                    BoardViewboxBtnColorRedContent.Visibility = Visibility.Visible;
                    break;
                case 2:
                    ViewboxBtnColorGreenContent.Visibility = Visibility.Visible;
                    BoardViewboxBtnColorGreenContent.Visibility = Visibility.Visible;
                    break;
                case 3:
                    ViewboxBtnColorBlueContent.Visibility = Visibility.Visible;
                    BoardViewboxBtnColorBlueContent.Visibility = Visibility.Visible;
                    break;
                case 4:
                    ViewboxBtnColorYellowContent.Visibility = Visibility.Visible;
                    BoardViewboxBtnColorYellowContent.Visibility = Visibility.Visible;
                    break;
                case 5:
                    ViewboxBtnColorWhiteContent.Visibility = Visibility.Visible;
                    BoardViewboxBtnColorWhiteContent.Visibility = Visibility.Visible;
                    break;
                case 6:
                    ViewboxBtnColorPinkContent.Visibility = Visibility.Visible;
                    BoardViewboxBtnColorPinkContent.Visibility = Visibility.Visible;
                    break;
                case 7:
                    ViewboxBtnColorTealContent.Visibility = Visibility.Visible;
                    BoardViewboxBtnColorTealContent.Visibility = Visibility.Visible;
                    break;
                case 8:
                    ViewboxBtnColorOrangeContent.Visibility = Visibility.Visible;
                    BoardViewboxBtnColorOrangeContent.Visibility = Visibility.Visible;
                    break;
            }
        }

        private Viewbox[] GetAllIndicators()
        {
            return new[]
            {
                ViewboxBtnColorBlackContent,
                ViewboxBtnColorWhiteContent,
                ViewboxBtnColorRedContent,
                ViewboxBtnColorYellowContent,
                ViewboxBtnColorGreenContent,
                ViewboxBtnColorBlueContent,
                ViewboxBtnColorPinkContent,
                ViewboxBtnColorTealContent,
                ViewboxBtnColorOrangeContent,
                BoardViewboxBtnColorBlackContent,
                BoardViewboxBtnColorWhiteContent,
                BoardViewboxBtnColorRedContent,
                BoardViewboxBtnColorYellowContent,
                BoardViewboxBtnColorGreenContent,
                BoardViewboxBtnColorBlueContent,
                BoardViewboxBtnColorPinkContent,
                BoardViewboxBtnColorTealContent,
                BoardViewboxBtnColorOrangeContent,
            };
        }

        private static ImageSource CreateThemeIcon(bool useLightThemeColors)
        {
            string uri = useLightThemeColors
                ? "/Resources/Icons-Fluent/ic_fluent_weather_moon_24_regular.png"
                : "/Resources/Icons-Fluent/ic_fluent_weather_sunny_24_regular.png";

            var imageSource = new System.Windows.Media.Imaging.BitmapImage();
            imageSource.BeginInit();
            imageSource.UriSource = new Uri(uri, UriKind.RelativeOrAbsolute);
            imageSource.EndInit();
            return imageSource;
        }

        private void ApplyPaletteColor(Border desktopBorder, Border boardBorder, PaletteColor paletteColor, bool useLightThemeColors)
        {
            Color color = GetPaletteColor(paletteColor, useLightThemeColors);
            var brush = new SolidColorBrush(color);
            desktopBorder.Background = brush;
            boardBorder.Background = brush.Clone();
        }

        private Color GetCurrentPaletteColor(PaletteColor paletteColor)
        {
            return GetPaletteColor(paletteColor, UseLightThemeColors);
        }

        private static Color GetPaletteColor(PaletteColor paletteColor, bool useLightThemeColors)
        {
            switch (paletteColor)
            {
                case PaletteColor.Red:
                    return useLightThemeColors ? Color.FromRgb(239, 68, 68) : Color.FromRgb(220, 38, 38);
                case PaletteColor.Green:
                    return useLightThemeColors ? Color.FromRgb(34, 197, 94) : Color.FromRgb(22, 163, 74);
                case PaletteColor.Blue:
                    return useLightThemeColors ? Color.FromRgb(59, 130, 246) : Color.FromRgb(37, 99, 235);
                case PaletteColor.Yellow:
                    return useLightThemeColors ? Color.FromRgb(250, 204, 21) : Color.FromRgb(234, 179, 8);
                case PaletteColor.Pink:
                    return useLightThemeColors ? Color.FromRgb(236, 72, 153) : Color.FromRgb(147, 51, 234);
                case PaletteColor.Teal:
                    return useLightThemeColors ? Color.FromRgb(20, 184, 166) : Color.FromRgb(13, 148, 136);
                case PaletteColor.Orange:
                    return useLightThemeColors ? Color.FromRgb(249, 115, 22) : Color.FromRgb(234, 88, 12);
                default:
                    return Colors.Black;
            }
        }

        private enum PaletteColor
        {
            Red,
            Green,
            Blue,
            Yellow,
            Pink,
            Teal,
            Orange,
        }
    }
}
