using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Ink_Canvas.MainWindow_controls
{
    public partial class PenSettingsPanel : UserControl
    {
        public static readonly DependencyProperty PenWidthProperty = DependencyProperty.Register(
            nameof(PenWidth), typeof(double), typeof(PenSettingsPanel), new PropertyMetadata(5.0));

        public static readonly DependencyProperty PenAlphaProperty = DependencyProperty.Register(
            nameof(PenAlpha), typeof(double), typeof(PenSettingsPanel), new PropertyMetadata(255.0));

        public static readonly DependencyProperty HighlighterWidthProperty = DependencyProperty.Register(
            nameof(HighlighterWidth), typeof(double), typeof(PenSettingsPanel), new PropertyMetadata(20.0));

        public static readonly DependencyProperty IsNibModeEnabledProperty = DependencyProperty.Register(
            nameof(IsNibModeEnabled), typeof(bool), typeof(PenSettingsPanel), new PropertyMetadata(true));

        public static readonly DependencyProperty IsInkFadeEnabledProperty = DependencyProperty.Register(
            nameof(IsInkFadeEnabled), typeof(bool), typeof(PenSettingsPanel), new PropertyMetadata(false));

        public static readonly DependencyProperty IsBoardModeProperty = DependencyProperty.Register(
            nameof(IsBoardMode), typeof(bool), typeof(PenSettingsPanel), new PropertyMetadata(false));

        public static readonly DependencyProperty SelectedPenTypeProperty = DependencyProperty.Register(
            nameof(SelectedPenType), typeof(int), typeof(PenSettingsPanel), new PropertyMetadata(0));

        public static readonly RoutedEvent PenTypeChangedEvent = EventManager.RegisterRoutedEvent(
            nameof(PenTypeChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PenSettingsPanel));

        public static readonly RoutedEvent WidthChangedEvent = EventManager.RegisterRoutedEvent(
            nameof(WidthChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PenSettingsPanel));

        public static readonly RoutedEvent AlphaChangedEvent = EventManager.RegisterRoutedEvent(
            nameof(AlphaChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PenSettingsPanel));

        public static readonly RoutedEvent NibModeChangedEvent = EventManager.RegisterRoutedEvent(
            nameof(NibModeChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PenSettingsPanel));

        public static readonly RoutedEvent InkFadeChangedEvent = EventManager.RegisterRoutedEvent(
            nameof(InkFadeChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PenSettingsPanel));

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

        public PenSettingsPanel()
        {
            InitializeComponent();
        }

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
        }

        private void CloseBordertools_MouseUp(object sender, MouseButtonEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(PenTypeChangedEvent, this));
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
            RaiseEvent(new RoutedEventArgs(PenTypeChangedEvent, this));
        }

        private void ToggleSwitchEnableNibMode_Toggled(object sender, RoutedEventArgs e)
        {
            IsNibModeEnabled = ToggleSwitchEnableNibMode.IsOn;
            RaiseEvent(new RoutedEventArgs(NibModeChangedEvent, this));
        }

        private void ToggleSwitchEnableInkToShape_Toggled(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(PenTypeChangedEvent, this));
        }

        private void ToggleSwitchInkFade_Toggled(object sender, RoutedEventArgs e)
        {
            IsInkFadeEnabled = ToggleSwitchInkFade.IsOn;
            RaiseEvent(new RoutedEventArgs(InkFadeChangedEvent, this));
        }

        private void InkWidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            PenWidth = InkWidthSlider.Value;
            RaiseEvent(new RoutedEventArgs(WidthChangedEvent, this));
        }

        private void InkAlphaSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            PenAlpha = InkAlphaSlider.Value;
            RaiseEvent(new RoutedEventArgs(AlphaChangedEvent, this));
        }

        private void HighlighterWidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            HighlighterWidth = HighlighterWidthSlider.Value;
            RaiseEvent(new RoutedEventArgs(WidthChangedEvent, this));
        }

        private void BrushModeButton_Click(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(PenTypeChangedEvent, this));
        }
    }
}
