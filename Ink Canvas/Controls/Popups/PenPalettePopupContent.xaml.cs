using Ink_Canvas.Properties;
using iNKORE.UI.WPF.Modern.Controls;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Controls
{
    public partial class PenPalettePopupContent : UserControl
    {
        public PopupTabTitleBar TabBar => Shell.TabBar;

        public int SelectedTabIndex
        {
            get => Shell.SelectedTabIndex;
            set => Shell.SelectedTabIndex = value;
        }

        public FrameworkElement CommonPropsPanel { get; }
        public FrameworkElement LaserPenFadePanel { get; }
        public FrameworkElement LaserPenFadeSpeedPanel { get; }
        public FrameworkElement InkToShapePanel { get; }
        public FrameworkElement HighlighterOverlapPanel { get; }
        public FrameworkElement DefaultPenColorsPanel { get; }
        public FrameworkElement HighlighterPenColorsPanel { get; }
        public FrameworkElement LaserPenColorsPanel { get; }

        public ComboBox PenStyleComboBox => ComboBoxPenStyle;
        public ToggleSwitch NibModeToggle => ToggleSwitchEnableNibMode;
        public ToggleSwitch InkToShapeToggle => FloatingBarToggleSwitchEnableInkToShape;
        public Slider PenWidthSlider { get; }
        public Slider PenAlphaSlider { get; }
        public Slider LaserPenFadeTimeSlider { get; }
        public Slider LaserPenFadeSpeedSlider { get; }
        public TextBlock PenWidthText { get; }
        public TextBlock PenAlphaText { get; }
        public TextBlock LaserPenFadeTimeText { get; }
        public TextBlock LaserPenFadeSpeedText { get; }
        public ToggleSwitch HighlighterOverlapToggle => ToggleSwitchHighlighterOverlap;

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

        public Button CloseButtonControl => Shell?.CloseButtonControl;

        public FrameworkElement NibModePanel => NibModeSimpleStackPanel;

        public PenPalettePopupContent()
        {
            InitializeComponent();

            Shell.InnerContent = InnerContentHost.Content;

            Shell.TabBar.Tabs.Add(new PopupTabItem
            {
                Header = FloatingBarStrings.Board_Pen
            });
            Shell.TabBar.Tabs.Add(new PopupTabItem
            {
                Header = FloatingBarStrings.Board_Highlighter
            });
            Shell.TabBar.Tabs.Add(new PopupTabItem
            {
                Header = FloatingBarStrings.Board_LaserPen
            });
            Shell.TabBar.SelectedIndex = 0;

            Shell.TabBar.SelectedIndexChanged += (s, index) =>
            {
                if (index < 0 || index >= Shell.TabBar.Tabs.Count) return;
                if (index == 0)
                    ShowDefaultPenPanels();
                else if (index == 1)
                    ShowHighlighterPenPanels();
                else if (index == 2)
                    ShowLaserPenPanels();
            };

            CommonPropsPanel = (FrameworkElement)FindName("_CommonPropsPanel");
            LaserPenFadePanel = (FrameworkElement)FindName("_LaserPenFadePanel");
            LaserPenFadeSpeedPanel = (FrameworkElement)FindName("_LaserPenFadeSpeedPanel");
            InkToShapePanel = (FrameworkElement)FindName("_InkToShapePanel");
            HighlighterOverlapPanel = (FrameworkElement)FindName("_HighlighterOverlapPanel");
            DefaultPenColorsPanel = (FrameworkElement)FindName("_DefaultPenColorsPanel");
            HighlighterPenColorsPanel = (FrameworkElement)FindName("_HighlighterPenColorsPanel");
            LaserPenColorsPanel = (FrameworkElement)FindName("_LaserPenColorsPanel");
            PenWidthSlider = (Slider)FindName("_PenWidthSlider");
            PenAlphaSlider = (Slider)FindName("_PenAlphaSlider");
            LaserPenFadeTimeSlider = (Slider)FindName("_LaserPenFadeTimeSlider");
            LaserPenFadeSpeedSlider = (Slider)FindName("_LaserPenFadeSpeedSlider");
            PenWidthText = (TextBlock)FindName("_PenWidthText");
            PenAlphaText = (TextBlock)FindName("_PenAlphaText");
            LaserPenFadeTimeText = (TextBlock)FindName("_LaserPenFadeTimeText");
            LaserPenFadeSpeedText = (TextBlock)FindName("_LaserPenFadeSpeedText");
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
            CommonPropsPanel.Visibility = Visibility.Visible;
            LaserPenFadePanel.Visibility = Visibility.Collapsed;
            LaserPenFadeSpeedPanel.Visibility = Visibility.Collapsed;
            InkToShapePanel.Visibility = Visibility.Visible;
            HighlighterOverlapPanel.Visibility = Visibility.Collapsed;
            DefaultPenColorsPanel.Visibility = Visibility.Visible;
            HighlighterPenColorsPanel.Visibility = Visibility.Collapsed;
            LaserPenColorsPanel.Visibility = Visibility.Collapsed;
        }

        private void ShowHighlighterPenPanels()
        {
            CommonPropsPanel.Visibility = Visibility.Visible;
            LaserPenFadePanel.Visibility = Visibility.Collapsed;
            LaserPenFadeSpeedPanel.Visibility = Visibility.Collapsed;
            InkToShapePanel.Visibility = Visibility.Visible;
            HighlighterOverlapPanel.Visibility = Visibility.Visible;
            DefaultPenColorsPanel.Visibility = Visibility.Collapsed;
            HighlighterPenColorsPanel.Visibility = Visibility.Visible;
            LaserPenColorsPanel.Visibility = Visibility.Collapsed;
        }

        private void ShowLaserPenPanels()
        {
            CommonPropsPanel.Visibility = Visibility.Visible;
            LaserPenFadePanel.Visibility = Visibility.Visible;
            LaserPenFadeSpeedPanel.Visibility = Visibility.Visible;
            InkToShapePanel.Visibility = Visibility.Collapsed;
            HighlighterOverlapPanel.Visibility = Visibility.Collapsed;
            DefaultPenColorsPanel.Visibility = Visibility.Collapsed;
            HighlighterPenColorsPanel.Visibility = Visibility.Collapsed;
            LaserPenColorsPanel.Visibility = Visibility.Visible;
        }

        private void PenWidthPresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string value } &&
                double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var width))
            {
                PenWidthSlider.Value = width;
            }
        }

        public void SwitchToDefaultPen()
        {
            Shell.TabBar.SelectedIndex = 0;
            ShowDefaultPenPanels();
        }

        public void SwitchToHighlighterPen()
        {
            Shell.TabBar.SelectedIndex = 1;
            ShowHighlighterPenPanels();
        }

        public void SwitchToLaserPen()
        {
            Shell.TabBar.SelectedIndex = 2;
            ShowLaserPenPanels();
        }
    }
}
