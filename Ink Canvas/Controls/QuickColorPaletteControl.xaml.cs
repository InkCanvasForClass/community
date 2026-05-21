using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Windows;
using System.Windows.Media;
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
            if (QuickColorPalettePanel == null || QuickColorPaletteSingleRowPanel == null || QuickColorPaletteContainer == null) return;

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
            
            QuickColorPaletteContainer.Visibility = Visibility.Visible;
        }

        public void SyncFromSettings()
        {
            var settings = SettingsManager.Settings;
            if (settings?.Appearance == null) return;
            DisplayMode = settings.Appearance.QuickColorPaletteDisplayMode;
        }

        /// <summary>
        /// 强制应用显示模式，确保即使在控件初始化期间也能正确显示
        /// </summary>
        public void ForceApplyDisplayMode()
        {
            if (IsLoaded)
            {
                ApplyDisplayMode();
            }
            else
            {
                void handler(object s, RoutedEventArgs args)
                {
                    Loaded -= handler;
                    ApplyDisplayMode();
                }
                Loaded += handler;
            }
        }

        public void ClearAllChecked()
        {
            QuickColorWhite.IsChecked = false;
            QuickColorOrange.IsChecked = false;
            QuickColorYellow.IsChecked = false;
            QuickColorBlack.IsChecked = false;
            QuickColorBlue.IsChecked = false;
            QuickColorRed.IsChecked = false;
            QuickColorGreen.IsChecked = false;
            QuickColorPurple.IsChecked = false;

            QuickColorWhiteSingle.IsChecked = false;
            QuickColorOrangeSingle.IsChecked = false;
            QuickColorYellowSingle.IsChecked = false;
            QuickColorBlackSingle.IsChecked = false;
            QuickColorRedSingle.IsChecked = false;
            QuickColorGreenSingle.IsChecked = false;
        }

        public void SetCheckedByColor(Color color, int tolerance = 15)
        {
            if (IsColorSimilar(color, Colors.White, tolerance) || IsColorSimilar(color, Color.FromRgb(250, 250, 250), tolerance))
            {
                QuickColorWhite.IsChecked = true;
                QuickColorWhiteSingle.IsChecked = true;
            }
            else if (IsColorSimilar(color, Colors.Black, tolerance))
            {
                QuickColorBlack.IsChecked = true;
                QuickColorBlackSingle.IsChecked = true;
            }
            else if (IsColorSimilar(color, Colors.Yellow, tolerance) ||
                     IsColorSimilar(color, Color.FromRgb(234, 179, 8), tolerance) ||
                     IsColorSimilar(color, Color.FromRgb(250, 204, 21), tolerance) ||
                     IsColorSimilar(color, Color.FromRgb(253, 224, 71), tolerance))
            {
                QuickColorYellow.IsChecked = true;
                QuickColorYellowSingle.IsChecked = true;
            }
            else if (IsColorSimilar(color, Color.FromRgb(255, 165, 0), tolerance) ||
                     IsColorSimilar(color, Color.FromRgb(251, 150, 80), tolerance) ||
                     IsColorSimilar(color, Color.FromRgb(249, 115, 22), tolerance) ||
                     IsColorSimilar(color, Color.FromRgb(234, 88, 12), tolerance) ||
                     IsColorSimilar(color, Color.FromRgb(251, 146, 60), tolerance) ||
                     IsColorSimilar(color, Color.FromRgb(253, 126, 20), tolerance))
            {
                QuickColorOrange.IsChecked = true;
                QuickColorOrangeSingle.IsChecked = true;
            }
            else if (IsColorSimilar(color, Color.FromRgb(37, 99, 235), tolerance))
            {
                QuickColorBlue.IsChecked = true;
            }
            else if (IsColorSimilar(color, Colors.Red, tolerance) ||
                     IsColorSimilar(color, Color.FromRgb(220, 38, 38), tolerance) ||
                     IsColorSimilar(color, Color.FromRgb(239, 68, 68), tolerance))
            {
                QuickColorRed.IsChecked = true;
                QuickColorRedSingle.IsChecked = true;
            }
            else if (IsColorSimilar(color, Color.FromRgb(22, 163, 74), tolerance))
            {
                QuickColorGreen.IsChecked = true;
                QuickColorGreenSingle.IsChecked = true;
            }
            else if (IsColorSimilar(color, Color.FromRgb(147, 51, 234), tolerance))
            {
                QuickColorPurple.IsChecked = true;
            }
        }

        private static bool IsColorSimilar(Color c1, Color c2, int tolerance)
        {
            return Math.Abs(c1.R - c2.R) <= tolerance &&
                   Math.Abs(c1.G - c2.G) <= tolerance &&
                   Math.Abs(c1.B - c2.B) <= tolerance;
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
