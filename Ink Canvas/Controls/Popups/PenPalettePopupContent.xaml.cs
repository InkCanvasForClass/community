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
        public FrameworkElement DefaultPenColorsPanel { get; }
        public FrameworkElement HighlighterPenColorsPanel { get; }

        public ComboBox PenStyleComboBox => ComboBoxPenStyle;
        public ToggleSwitch NibModeToggle => ToggleSwitchEnableNibMode;
        public ToggleSwitch InkToShapeToggle => FloatingBarToggleSwitchEnableInkToShape;
        public Slider InkWidthSlider { get; }
        public Slider InkAlphaSlider { get; }
        public Slider HighlighterWidthSlider { get; }
        public Button BrushModeBtn => BoardBrushModeButton;
        public Path BrushModeIcon => BoardBrushModeIcon;

        public ToggleSwitch InkFadeToggle => ToggleSwitchInkFadeInPanel;
        public ToggleSwitch InkFadeToggle2 => ToggleSwitchInkFadeInPanel;

        public Border ColorThemeSwitch { get; }
        public Image ColorThemeSwitchIcon { get; }
        public TextBlock ColorThemeSwitchText => ColorThemeSwitchTextBlock;

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

        public FontIcon CloseFontIcon => TabTitleBar?.CloseFontIcon;

        public FrameworkElement NibModePanel => NibModeSimpleStackPanel;
        public FrameworkElement InkFadeControlPanel => InkFadeControlPanel1;
        public FrameworkElement InkFadeControlPanel2 => InkFadeControlPanel1;

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
            TabTitleBar.SelectedIndex = 0;

            TabTitleBar.SelectedIndexChanged += (s, index) =>
            {
                if (index < 0 || index >= TabTitleBar.Tabs.Count) return;
                if (index == 0)
                    ShowDefaultPenPanels();
                else if (index == 1)
                    ShowHighlighterPenPanels();
            };

            DefaultPenPropsPanel = (FrameworkElement)FindName("_DefaultPenPropsPanel");
            HighlighterPenPropsPanel = (FrameworkElement)FindName("_HighlighterPenPropsPanel");
            DefaultPenColorsPanel = (FrameworkElement)FindName("_DefaultPenColorsPanel");
            HighlighterPenColorsPanel = (FrameworkElement)FindName("_HighlighterPenColorsPanel");
            InkWidthSlider = (Slider)FindName("_InkWidthSlider");
            InkAlphaSlider = (Slider)FindName("_InkAlphaSlider");
            HighlighterWidthSlider = (Slider)FindName("_HighlighterWidthSlider");
            ColorThemeSwitch = (Border)FindName("_ColorThemeSwitch");
            ColorThemeSwitchIcon = (Image)FindName("_ColorThemeSwitchIcon");
            HighlighterPenColorBlack = (PenColorButton)FindName("_HighlighterPenColorBlack");
            HighlighterPenColorWhite = (PenColorButton)FindName("_HighlighterPenColorWhite");
            HighlighterPenColorRed = (PenColorButton)FindName("_HighlighterPenColorRed");
            HighlighterPenColorYellow = (PenColorButton)FindName("_HighlighterPenColorYellow");
            HighlighterPenColorGreen = (PenColorButton)FindName("_HighlighterPenColorGreen");
            HighlighterPenColorZinc = (PenColorButton)FindName("_HighlighterPenColorZinc");
            HighlighterPenColorBlue = (PenColorButton)FindName("_HighlighterPenColorBlue");
            HighlighterPenColorTeal = (PenColorButton)FindName("_HighlighterPenColorTeal");
            HighlighterPenColorOrange = (PenColorButton)FindName("_HighlighterPenColorOrange");
        }

        private void ShowDefaultPenPanels()
        {
            DefaultPenPropsPanel.Visibility = Visibility.Visible;
            HighlighterPenPropsPanel.Visibility = Visibility.Collapsed;
            DefaultPenColorsPanel.Visibility = Visibility.Visible;
            HighlighterPenColorsPanel.Visibility = Visibility.Collapsed;
        }

        private void ShowHighlighterPenPanels()
        {
            DefaultPenPropsPanel.Visibility = Visibility.Collapsed;
            HighlighterPenPropsPanel.Visibility = Visibility.Visible;
            DefaultPenColorsPanel.Visibility = Visibility.Collapsed;
            HighlighterPenColorsPanel.Visibility = Visibility.Visible;
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
    }
}
