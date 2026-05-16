using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using iNKORE.UI.WPF.Modern.Controls;
using Ink_Canvas.Properties;

namespace Ink_Canvas.Controls
{
    public partial class PenPalettePopupContent : UserControl
    {
        public static readonly DependencyProperty IsBoardModeProperty = DependencyProperty.Register(
            nameof(IsBoardMode), typeof(bool), typeof(PenPalettePopupContent),
            new PropertyMetadata(false, OnIsBoardModeChanged));

        private static void OnIsBoardModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (PenPalettePopupContent)d;
            control.BoardBrushModeButton.Visibility = (bool)e.NewValue ? Visibility.Collapsed : Visibility.Visible;
        }

        public bool IsBoardMode
        {
            get => (bool)GetValue(IsBoardModeProperty);
            set => SetValue(IsBoardModeProperty, value);
        }

        public PopupTabTitleBar TabBar => TabTitleBar;

        public int SelectedTabIndex
        {
            get => TabTitleBar.SelectedIndex;
            set => TabTitleBar.SelectedIndex = value;
        }

        public FrameworkElement DefaultPenPropsPanel { get; }
        public FrameworkElement HighlighterPenPropsPanel { get; }
        public FrameworkElement LaserPenPropsPanel { get; }
        public FrameworkElement DefaultPenColorsPanel { get; }
        public FrameworkElement HighlighterPenColorsPanel { get; }
        public FrameworkElement LaserPenColorsPanel { get; }

        public ComboBox PenStyleComboBox => ComboBoxPenStyle;
        public ToggleSwitch NibModeToggle => ToggleSwitchEnableNibMode;
        public ToggleSwitch InkToShapeToggle => FloatingBarToggleSwitchEnableInkToShape;
        public Slider InkWidthSlider { get; }
        public Slider InkAlphaSlider { get; }
        public Slider HighlighterWidthSlider { get; }
        public Slider LaserPenWidthSlider { get; }
        public Slider LaserPenAlphaSlider { get; }
        public Slider LaserPenFadeTimeSlider { get; }
        public Button BrushModeBtn => BoardBrushModeButton;
        public Path BrushModeIcon => BoardBrushModeIcon;

        public Border ColorThemeSwitch { get; }
        public Image ColorThemeSwitchIcon { get; }
        public TextBlock ColorThemeSwitchText => ColorThemeSwitchTextBlock;
        public Border LaserPenColorThemeSwitch { get; }
        public Image LaserPenColorThemeSwitchIcon { get; }
        public TextBlock LaserPenColorThemeSwitchText => _LaserPenColorThemeSwitchTextBlock;

        public PenColorButton DefaultPenColorBlack => BorderPenColorBlack;
        public PenColorButton DefaultPenColorWhite => BorderPenColorWhite;
        public PenColorButton DefaultPenColorRed => BorderPenColorRed;
        public PenColorButton DefaultPenColorYellow => BorderPenColorYellow;
        public PenColorButton DefaultPenColorGreen => BorderPenColorGreen;
        public PenColorButton DefaultPenColorBlue => BorderPenColorBlue;
        public PenColorButton DefaultPenColorPink => BorderPenColorPink;
        public PenColorButton DefaultPenColorTeal => BorderPenColorTeal;
        public PenColorButton DefaultPenColorOrange => BorderPenColorOrange;

        public PenColorButton HighlighterPenColorBlack { get; }
        public PenColorButton HighlighterPenColorWhite { get; }
        public PenColorButton HighlighterPenColorRed { get; }
        public PenColorButton HighlighterPenColorYellow { get; }
        public PenColorButton HighlighterPenColorGreen { get; }
        public PenColorButton HighlighterPenColorZinc { get; }
        public PenColorButton HighlighterPenColorBlue { get; }
        public PenColorButton HighlighterPenColorPurple => HighlighterPenPenColorPurple;
        public PenColorButton HighlighterPenColorTeal { get; }
        public PenColorButton HighlighterPenColorOrange { get; }

        public PenColorButton LaserPenColorBlack { get; }
        public PenColorButton LaserPenColorWhite { get; }
        public PenColorButton LaserPenColorRed { get; }
        public PenColorButton LaserPenColorYellow { get; }
        public PenColorButton LaserPenColorGreen { get; }
        public PenColorButton LaserPenColorBlue { get; }
        public PenColorButton LaserPenColorPink { get; }
        public PenColorButton LaserPenColorTeal { get; }
        public PenColorButton LaserPenColorOrange { get; }

        public Button CloseButtonControl => TabTitleBar?.CloseButtonControl;

        public FrameworkElement NibModePanel => NibModeSimpleStackPanel;

        public PenPalettePopupContent()
        {
            InitializeComponent();

            TabTitleBar.Tabs.Add(new PopupTabItem
            {
                Header = Strings.GetString("Board_Pen") ?? "Pen"
            });
            TabTitleBar.Tabs.Add(new PopupTabItem
            {
                Header = Strings.GetString("Board_Highlighter") ?? "Highlighter"
            });
            TabTitleBar.Tabs.Add(new PopupTabItem
            {
                Header = Strings.GetString("Board_LaserPen") ?? "Laser Pen"
            });
            TabTitleBar.SelectedIndex = 0;

            TabTitleBar.SelectedIndexChanged += (s, index) =>
            {
                if (index < 0 || index >= TabTitleBar.Tabs.Count) return;
                if (index == 0)
                    ShowDefaultPenPanels();
                else if (index == 1)
                    ShowHighlighterPenPanels();
                else if (index == 2)
                    ShowLaserPenPanels();
            };

            DefaultPenPropsPanel = (FrameworkElement)FindName("_DefaultPenPropsPanel");
            HighlighterPenPropsPanel = (FrameworkElement)FindName("_HighlighterPenPropsPanel");
            LaserPenPropsPanel = (FrameworkElement)FindName("_LaserPenPropsPanel");
            DefaultPenColorsPanel = (FrameworkElement)FindName("_DefaultPenColorsPanel");
            HighlighterPenColorsPanel = (FrameworkElement)FindName("_HighlighterPenColorsPanel");
            LaserPenColorsPanel = (FrameworkElement)FindName("_LaserPenColorsPanel");
            InkWidthSlider = (Slider)FindName("_InkWidthSlider");
            InkAlphaSlider = (Slider)FindName("_InkAlphaSlider");
            HighlighterWidthSlider = (Slider)FindName("_HighlighterWidthSlider");
            LaserPenWidthSlider = (Slider)FindName("_LaserPenWidthSlider");
            LaserPenAlphaSlider = (Slider)FindName("_LaserPenAlphaSlider");
            LaserPenFadeTimeSlider = (Slider)FindName("_LaserPenFadeTimeSlider");
            ColorThemeSwitch = (Border)FindName("_ColorThemeSwitch");
            ColorThemeSwitchIcon = (Image)FindName("_ColorThemeSwitchIcon");
            LaserPenColorThemeSwitch = (Border)FindName("_LaserPenColorThemeSwitch");
            LaserPenColorThemeSwitchIcon = (Image)FindName("_LaserPenColorThemeSwitchIcon");
            HighlighterPenColorBlack = (PenColorButton)FindName("_HighlighterPenColorBlack");
            HighlighterPenColorWhite = (PenColorButton)FindName("_HighlighterPenColorWhite");
            HighlighterPenColorRed = (PenColorButton)FindName("_HighlighterPenColorRed");
            HighlighterPenColorYellow = (PenColorButton)FindName("_HighlighterPenColorYellow");
            HighlighterPenColorGreen = (PenColorButton)FindName("_HighlighterPenColorGreen");
            HighlighterPenColorZinc = (PenColorButton)FindName("_HighlighterPenColorZinc");
            HighlighterPenColorBlue = (PenColorButton)FindName("_HighlighterPenColorBlue");
            HighlighterPenColorTeal = (PenColorButton)FindName("_HighlighterPenColorTeal");
            HighlighterPenColorOrange = (PenColorButton)FindName("_HighlighterPenColorOrange");
            LaserPenColorBlack = (PenColorButton)FindName("_LaserPenColorBlack");
            LaserPenColorWhite = (PenColorButton)FindName("_LaserPenColorWhite");
            LaserPenColorRed = (PenColorButton)FindName("_LaserPenColorRed");
            LaserPenColorYellow = (PenColorButton)FindName("_LaserPenColorYellow");
            LaserPenColorGreen = (PenColorButton)FindName("_LaserPenColorGreen");
            LaserPenColorBlue = (PenColorButton)FindName("_LaserPenColorBlue");
            LaserPenColorPink = (PenColorButton)FindName("_LaserPenColorPink");
            LaserPenColorTeal = (PenColorButton)FindName("_LaserPenColorTeal");
            LaserPenColorOrange = (PenColorButton)FindName("_LaserPenColorOrange");
        }

        private void ShowDefaultPenPanels()
        {
            DefaultPenPropsPanel.Visibility = Visibility.Visible;
            HighlighterPenPropsPanel.Visibility = Visibility.Collapsed;
            LaserPenPropsPanel.Visibility = Visibility.Collapsed;
            DefaultPenColorsPanel.Visibility = Visibility.Visible;
            HighlighterPenColorsPanel.Visibility = Visibility.Collapsed;
            LaserPenColorsPanel.Visibility = Visibility.Collapsed;
        }

        private void ShowHighlighterPenPanels()
        {
            DefaultPenPropsPanel.Visibility = Visibility.Collapsed;
            HighlighterPenPropsPanel.Visibility = Visibility.Visible;
            LaserPenPropsPanel.Visibility = Visibility.Collapsed;
            DefaultPenColorsPanel.Visibility = Visibility.Collapsed;
            HighlighterPenColorsPanel.Visibility = Visibility.Visible;
            LaserPenColorsPanel.Visibility = Visibility.Collapsed;
        }

        private void ShowLaserPenPanels()
        {
            DefaultPenPropsPanel.Visibility = Visibility.Collapsed;
            HighlighterPenPropsPanel.Visibility = Visibility.Collapsed;
            LaserPenPropsPanel.Visibility = Visibility.Visible;
            DefaultPenColorsPanel.Visibility = Visibility.Collapsed;
            HighlighterPenColorsPanel.Visibility = Visibility.Collapsed;
            LaserPenColorsPanel.Visibility = Visibility.Visible;
        }

        public void SwitchToDefaultPen()
        {
            TabTitleBar.SelectedIndex = 0;
            ShowDefaultPenPanels();
        }

        public void SwitchToHighlighterPen()
        {
            TabTitleBar.SelectedIndex = 1;
            ShowHighlighterPenPanels();
        }

        public void SwitchToLaserPen()
        {
            TabTitleBar.SelectedIndex = 2;
            ShowLaserPenPanels();
        }
    }
}
