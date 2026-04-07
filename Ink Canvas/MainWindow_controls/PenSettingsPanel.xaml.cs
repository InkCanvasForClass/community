using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.MainWindow_controls
{
    public partial class PenSettingsPanel : UserControl
    {
        private bool _isSynchronizingControls;

        public static readonly DependencyProperty PenWidthProperty = DependencyProperty.Register(
            nameof(PenWidth), typeof(double), typeof(PenSettingsPanel), new PropertyMetadata(5.0, OnPenWidthChanged));

        public static readonly DependencyProperty PenAlphaProperty = DependencyProperty.Register(
            nameof(PenAlpha), typeof(double), typeof(PenSettingsPanel), new PropertyMetadata(255.0, OnPenAlphaChanged));

        public static readonly DependencyProperty HighlighterWidthProperty = DependencyProperty.Register(
            nameof(HighlighterWidth), typeof(double), typeof(PenSettingsPanel), new PropertyMetadata(20.0, OnHighlighterWidthChanged));

        public static readonly DependencyProperty IsNibModeEnabledProperty = DependencyProperty.Register(
            nameof(IsNibModeEnabled), typeof(bool), typeof(PenSettingsPanel), new PropertyMetadata(true, OnIsNibModeEnabledChanged));

        public static readonly DependencyProperty IsInkFadeEnabledProperty = DependencyProperty.Register(
            nameof(IsInkFadeEnabled), typeof(bool), typeof(PenSettingsPanel), new PropertyMetadata(false, OnIsInkFadeEnabledChanged));

        public static readonly DependencyProperty IsBoardModeProperty = DependencyProperty.Register(
            nameof(IsBoardMode), typeof(bool), typeof(PenSettingsPanel), new PropertyMetadata(false));

        public static readonly DependencyProperty SelectedPenTypeProperty = DependencyProperty.Register(
            nameof(SelectedPenType), typeof(int), typeof(PenSettingsPanel), new PropertyMetadata(0, OnSelectedPenTypeChanged));

        public static readonly RoutedEvent PenTypeChangedEvent = EventManager.RegisterRoutedEvent(
            nameof(PenTypeChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PenSettingsPanel));

        public static readonly RoutedEvent PenStyleChangedEvent = EventManager.RegisterRoutedEvent(
            nameof(PenStyleChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PenSettingsPanel));

        public static readonly RoutedEvent WidthChangedEvent = EventManager.RegisterRoutedEvent(
            nameof(WidthChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PenSettingsPanel));

        public static readonly RoutedEvent AlphaChangedEvent = EventManager.RegisterRoutedEvent(
            nameof(AlphaChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PenSettingsPanel));

        public static readonly RoutedEvent NibModeChangedEvent = EventManager.RegisterRoutedEvent(
            nameof(NibModeChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PenSettingsPanel));

        public static readonly RoutedEvent InkFadeChangedEvent = EventManager.RegisterRoutedEvent(
            nameof(InkFadeChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PenSettingsPanel));

        public static readonly RoutedEvent InkToShapeChangedEvent = EventManager.RegisterRoutedEvent(
            nameof(InkToShapeChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PenSettingsPanel));

        public static readonly RoutedEvent BrushModeClickedEvent = EventManager.RegisterRoutedEvent(
            nameof(BrushModeClicked), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PenSettingsPanel));

        public static readonly RoutedEvent CloseRequestedEvent = EventManager.RegisterRoutedEvent(
            nameof(CloseRequested), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PenSettingsPanel));

        public double PenWidth
        {
            get => (double)GetValue(PenWidthProperty);
            set => SetValue(PenWidthProperty, value);
        }

        public double PenAlpha
        {
            get => (double)GetValue(PenAlphaProperty);
            set => SetValue(PenAlphaProperty, value);
        }

        public double HighlighterWidth
        {
            get => (double)GetValue(HighlighterWidthProperty);
            set => SetValue(HighlighterWidthProperty, value);
        }

        public bool IsNibModeEnabled
        {
            get => (bool)GetValue(IsNibModeEnabledProperty);
            set => SetValue(IsNibModeEnabledProperty, value);
        }

        public bool IsInkFadeEnabled
        {
            get => (bool)GetValue(IsInkFadeEnabledProperty);
            set => SetValue(IsInkFadeEnabledProperty, value);
        }

        public bool IsBoardMode
        {
            get => (bool)GetValue(IsBoardModeProperty);
            set => SetValue(IsBoardModeProperty, value);
        }

        public int SelectedPenType
        {
            get => (int)GetValue(SelectedPenTypeProperty);
            set => SetValue(SelectedPenTypeProperty, value);
        }

        public event RoutedEventHandler PenTypeChanged
        {
            add => AddHandler(PenTypeChangedEvent, value);
            remove => RemoveHandler(PenTypeChangedEvent, value);
        }

        public event RoutedEventHandler PenStyleChanged
        {
            add => AddHandler(PenStyleChangedEvent, value);
            remove => RemoveHandler(PenStyleChangedEvent, value);
        }

        public event RoutedEventHandler WidthChanged
        {
            add => AddHandler(WidthChangedEvent, value);
            remove => RemoveHandler(WidthChangedEvent, value);
        }

        public event RoutedEventHandler AlphaChanged
        {
            add => AddHandler(AlphaChangedEvent, value);
            remove => RemoveHandler(AlphaChangedEvent, value);
        }

        public event RoutedEventHandler NibModeChanged
        {
            add => AddHandler(NibModeChangedEvent, value);
            remove => RemoveHandler(NibModeChangedEvent, value);
        }

        public event RoutedEventHandler InkFadeChanged
        {
            add => AddHandler(InkFadeChangedEvent, value);
            remove => RemoveHandler(InkFadeChangedEvent, value);
        }

        public event RoutedEventHandler InkToShapeChanged
        {
            add => AddHandler(InkToShapeChangedEvent, value);
            remove => RemoveHandler(InkToShapeChangedEvent, value);
        }

        public event RoutedEventHandler BrushModeClicked
        {
            add => AddHandler(BrushModeClickedEvent, value);
            remove => RemoveHandler(BrushModeClickedEvent, value);
        }

        public event RoutedEventHandler CloseRequested
        {
            add => AddHandler(CloseRequestedEvent, value);
            remove => RemoveHandler(CloseRequestedEvent, value);
        }

        public Slider InkWidthSliderControl => InkWidthSlider;

        public Slider InkAlphaSliderControl => InkAlphaSlider;

        public Slider HighlighterWidthSliderControl => HighlighterWidthSlider;

        public FrameworkElement NibModePanel => NibModeSimpleStackPanel;

        public FrameworkElement InkFadePanel => InkFadeControlPanel;

        public int PenStyleSelectedIndex
        {
            get => ComboBoxPenStyle?.SelectedIndex ?? -1;
            set
            {
                if (ComboBoxPenStyle != null && ComboBoxPenStyle.SelectedIndex != value)
                {
                    SynchronizeControls(() => ComboBoxPenStyle.SelectedIndex = value);
                }
            }
        }

        public bool IsInkToShapeEnabled
        {
            get => ToggleSwitchEnableInkToShape?.IsOn ?? false;
            set
            {
                if (ToggleSwitchEnableInkToShape != null && ToggleSwitchEnableInkToShape.IsOn != value)
                {
                    SynchronizeControls(() => ToggleSwitchEnableInkToShape.IsOn = value);
                }
            }
        }

        public Visibility NibModePanelVisibility
        {
            get => NibModeSimpleStackPanel.Visibility;
            set => NibModeSimpleStackPanel.Visibility = value;
        }

        public Visibility InkFadePanelVisibility
        {
            get => InkFadeControlPanel.Visibility;
            set => InkFadeControlPanel.Visibility = value;
        }

        public PenSettingsPanel()
        {
            InitializeComponent();
            ApplyStateToControls();
        }

        public void SetPenTypeVisualState(int penType)
        {
            if (SelectedPenType != penType)
            {
                SelectedPenType = penType;
                return;
            }

            UpdatePenTypeVisualState();
        }

        public void SetBrushModeActive(bool isActive)
        {
            if (BrushModeButton == null)
            {
                return;
            }

            if (isActive)
            {
                BrushModeButton.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
            }
            else
            {
                BrushModeButton.ClearValue(BackgroundProperty);
            }
        }

        private static void OnPenWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PenSettingsPanel panel)
            {
                panel.ApplyPenWidthToControl((double)e.NewValue);
            }
        }

        private static void OnPenAlphaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PenSettingsPanel panel)
            {
                panel.ApplyPenAlphaToControl((double)e.NewValue);
            }
        }

        private static void OnHighlighterWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PenSettingsPanel panel)
            {
                panel.ApplyHighlighterWidthToControl((double)e.NewValue);
            }
        }

        private static void OnIsNibModeEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PenSettingsPanel panel)
            {
                panel.ApplyNibModeToControl((bool)e.NewValue);
            }
        }

        private static void OnIsInkFadeEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PenSettingsPanel panel)
            {
                panel.ApplyInkFadeToControl((bool)e.NewValue);
            }
        }

        private static void OnSelectedPenTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PenSettingsPanel panel)
            {
                panel.UpdatePenTypeVisualState();
            }
        }

        private void ApplyStateToControls()
        {
            ApplyPenWidthToControl(PenWidth);
            ApplyPenAlphaToControl(PenAlpha);
            ApplyHighlighterWidthToControl(HighlighterWidth);
            ApplyNibModeToControl(IsNibModeEnabled);
            ApplyInkFadeToControl(IsInkFadeEnabled);
            UpdatePenTypeVisualState();
        }

        private void ApplyPenWidthToControl(double value)
        {
            if (InkWidthSlider == null || AreClose(InkWidthSlider.Value, value))
            {
                return;
            }

            SynchronizeControls(() => InkWidthSlider.Value = value);
        }

        private void ApplyPenAlphaToControl(double value)
        {
            if (InkAlphaSlider == null || AreClose(InkAlphaSlider.Value, value))
            {
                return;
            }

            SynchronizeControls(() => InkAlphaSlider.Value = value);
        }

        private void ApplyHighlighterWidthToControl(double value)
        {
            if (HighlighterWidthSlider == null || AreClose(HighlighterWidthSlider.Value, value))
            {
                return;
            }

            SynchronizeControls(() => HighlighterWidthSlider.Value = value);
        }

        private void ApplyNibModeToControl(bool value)
        {
            if (ToggleSwitchEnableNibMode == null || ToggleSwitchEnableNibMode.IsOn == value)
            {
                return;
            }

            SynchronizeControls(() => ToggleSwitchEnableNibMode.IsOn = value);
        }

        private void ApplyInkFadeToControl(bool value)
        {
            if (ToggleSwitchInkFade == null || ToggleSwitchInkFade.IsOn == value)
            {
                return;
            }

            SynchronizeControls(() => ToggleSwitchInkFade.IsOn = value);
        }

        private void UpdatePenTypeVisualState()
        {
            bool isDefaultPen = SelectedPenType != 1;

            DefaultPenPropsPanel.Visibility = isDefaultPen ? Visibility.Visible : Visibility.Collapsed;
            HighlighterPenPropsPanel.Visibility = isDefaultPen ? Visibility.Collapsed : Visibility.Visible;

            DefaultPenTabButton.Opacity = isDefaultPen ? 1 : 0.9;
            DefaultPenTabButtonText.FontWeight = isDefaultPen ? FontWeights.Bold : FontWeights.Normal;
            DefaultPenTabButtonText.FontSize = isDefaultPen ? 9.5 : 9;
            DefaultPenTabButtonText.Margin = isDefaultPen ? new Thickness(2, 0.5, 0, 0) : new Thickness(2, 1, 0, 0);
            DefaultPenTabButton.Background = isDefaultPen
                ? new SolidColorBrush(Color.FromArgb(72, 219, 234, 254))
                : new SolidColorBrush(Colors.Transparent);
            DefaultPenTabButtonIndicator.Visibility = isDefaultPen ? Visibility.Visible : Visibility.Collapsed;

            HighlightPenTabButton.Opacity = isDefaultPen ? 0.9 : 1;
            HighlightPenTabButtonText.FontWeight = isDefaultPen ? FontWeights.Normal : FontWeights.Bold;
            HighlightPenTabButtonText.FontSize = isDefaultPen ? 9 : 9.5;
            HighlightPenTabButtonText.Margin = isDefaultPen ? new Thickness(2, 1, 0, 0) : new Thickness(2, 0.5, 0, 0);
            HighlightPenTabButton.Background = isDefaultPen
                ? new SolidColorBrush(Colors.Transparent)
                : new SolidColorBrush(Color.FromArgb(72, 219, 234, 254));
            HighlightPenTabButtonIndicator.Visibility = isDefaultPen ? Visibility.Collapsed : Visibility.Visible;
        }

        private void SynchronizeControls(Action action)
        {
            _isSynchronizingControls = true;
            try
            {
                action();
            }
            finally
            {
                _isSynchronizingControls = false;
            }
        }

        private static bool AreClose(double left, double right)
        {
            return Math.Abs(left - right) < 0.001;
        }

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
        }

        private void CloseBordertools_MouseUp(object sender, MouseButtonEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(CloseRequestedEvent, this));
        }

        private void SwitchToDefaultPen(object sender, MouseButtonEventArgs e)
        {
            SelectedPenType = 0;
            RaiseEvent(new RoutedEventArgs(PenTypeChangedEvent, this));
        }

        private void SwitchToHighlighterPen(object sender, MouseButtonEventArgs e)
        {
            SelectedPenType = 1;
            RaiseEvent(new RoutedEventArgs(PenTypeChangedEvent, this));
        }

        private void ComboBoxPenStyle_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSynchronizingControls)
            {
                return;
            }

            RaiseEvent(new RoutedEventArgs(PenStyleChangedEvent, this));
        }

        private void ToggleSwitchEnableNibMode_Toggled(object sender, RoutedEventArgs e)
        {
            IsNibModeEnabled = ToggleSwitchEnableNibMode.IsOn;

            if (_isSynchronizingControls)
            {
                return;
            }

            RaiseEvent(new RoutedEventArgs(NibModeChangedEvent, this));
        }

        private void ToggleSwitchEnableInkToShape_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isSynchronizingControls)
            {
                return;
            }

            RaiseEvent(new RoutedEventArgs(InkToShapeChangedEvent, this));
        }

        private void ToggleSwitchInkFade_Toggled(object sender, RoutedEventArgs e)
        {
            IsInkFadeEnabled = ToggleSwitchInkFade.IsOn;

            if (_isSynchronizingControls)
            {
                return;
            }

            RaiseEvent(new RoutedEventArgs(InkFadeChangedEvent, this));
        }

        private void InkWidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            PenWidth = InkWidthSlider.Value;

            if (_isSynchronizingControls)
            {
                return;
            }

            RaiseEvent(new RoutedEventArgs(WidthChangedEvent, this));
        }

        private void InkAlphaSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            PenAlpha = InkAlphaSlider.Value;

            if (_isSynchronizingControls)
            {
                return;
            }

            RaiseEvent(new RoutedEventArgs(AlphaChangedEvent, this));
        }

        private void HighlighterWidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            HighlighterWidth = HighlighterWidthSlider.Value;

            if (_isSynchronizingControls)
            {
                return;
            }

            RaiseEvent(new RoutedEventArgs(WidthChangedEvent, this));
        }

        private void BrushModeButton_Click(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(BrushModeClickedEvent, this));
        }
    }
}
