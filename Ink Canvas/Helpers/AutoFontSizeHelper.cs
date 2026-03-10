using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// Automatically shrinks text to fit available width.
    /// Supports TextBlock and Label.
    /// Only shrinks, never enlarges above MaxFontSize.
    /// </summary>
    public static class AutoFontSizeHelper
    {
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(AutoFontSizeHelper),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);
        public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

        public static readonly DependencyProperty MinFontSizeProperty =
            DependencyProperty.RegisterAttached(
                "MinFontSize",
                typeof(double),
                typeof(AutoFontSizeHelper),
                new PropertyMetadata(6d, OnSizingPropertyChanged));

        public static void SetMinFontSize(DependencyObject element, double value) => element.SetValue(MinFontSizeProperty, value);
        public static double GetMinFontSize(DependencyObject element) => (double)element.GetValue(MinFontSizeProperty);

        public static readonly DependencyProperty MaxFontSizeProperty =
            DependencyProperty.RegisterAttached(
                "MaxFontSize",
                typeof(double),
                typeof(AutoFontSizeHelper),
                new PropertyMetadata(double.NaN, OnSizingPropertyChanged));

        public static void SetMaxFontSize(DependencyObject element, double value) => element.SetValue(MaxFontSizeProperty, value);
        public static double GetMaxFontSize(DependencyObject element) => (double)element.GetValue(MaxFontSizeProperty);

        public static readonly DependencyProperty StepProperty =
            DependencyProperty.RegisterAttached(
                "Step",
                typeof(double),
                typeof(AutoFontSizeHelper),
                new PropertyMetadata(0.5d, OnSizingPropertyChanged));

        public static void SetStep(DependencyObject element, double value) => element.SetValue(StepProperty, value);
        public static double GetStep(DependencyObject element) => (double)element.GetValue(StepProperty);

        private static readonly DependencyProperty IsAdjustingProperty =
            DependencyProperty.RegisterAttached(
                "IsAdjusting",
                typeof(bool),
                typeof(AutoFontSizeHelper),
                new PropertyMetadata(false));

        private static void SetIsAdjusting(DependencyObject element, bool value) => element.SetValue(IsAdjustingProperty, value);
        private static bool GetIsAdjusting(DependencyObject element) => (bool)element.GetValue(IsAdjustingProperty);

        private static readonly DependencyProperty OriginalFontSizeProperty =
            DependencyProperty.RegisterAttached(
                "OriginalFontSize",
                typeof(double),
                typeof(AutoFontSizeHelper),
                new PropertyMetadata(double.NaN));

        private static void SetOriginalFontSize(DependencyObject element, double value) => element.SetValue(OriginalFontSizeProperty, value);
        private static double GetOriginalFontSize(DependencyObject element) => (double)element.GetValue(OriginalFontSizeProperty);

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is FrameworkElement fe)) return;
            if (!(fe is TextBlock) && !(fe is Label)) return;

            if ((bool)e.NewValue)
            {
                var originalFontSize = GetElementFontSize(fe);
                if (!double.IsNaN(originalFontSize) && originalFontSize > 0)
                {
                    SetOriginalFontSize(fe, originalFontSize);
                }

                fe.SizeChanged += Element_OnSizeChanged;
                fe.Loaded += Element_OnLoaded;
                fe.Unloaded += Element_OnUnloaded;
                TryHookContentChanged(fe, true);

                fe.Dispatcher.BeginInvoke(new Action(() => TryAdjust(fe)), DispatcherPriority.Loaded);
            }
            else
            {
                fe.SizeChanged -= Element_OnSizeChanged;
                fe.Loaded -= Element_OnLoaded;
                fe.Unloaded -= Element_OnUnloaded;
                TryHookContentChanged(fe, false);
            }
        }

        private static void OnSizingPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement fe && GetIsEnabled(fe))
            {
                fe.Dispatcher.BeginInvoke(new Action(() => TryAdjust(fe)), DispatcherPriority.Loaded);
            }
        }

        private static void Element_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe)
            {
                fe.Dispatcher.BeginInvoke(new Action(() => TryAdjust(fe)), DispatcherPriority.Loaded);
            }
        }

        private static void Element_OnUnloaded(object sender, RoutedEventArgs e)
        {
            // No extra cleanup required here.
        }

        private static void Element_OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is FrameworkElement fe) TryAdjust(fe);
        }

        private static void Element_OnTextChanged(object sender, EventArgs e)
        {
            if (sender is FrameworkElement fe)
            {
                fe.Dispatcher.BeginInvoke(new Action(() => TryAdjust(fe)), DispatcherPriority.Loaded);
            }
        }

        private static void TryHookContentChanged(FrameworkElement fe, bool add)
        {
            try
            {
                DependencyPropertyDescriptor dpd = null;
                if (fe is TextBlock)
                {
                    dpd = DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));
                }
                else if (fe is Label)
                {
                    dpd = DependencyPropertyDescriptor.FromProperty(ContentControl.ContentProperty, typeof(ContentControl));
                }

                if (dpd == null) return;
                if (add) dpd.AddValueChanged(fe, Element_OnTextChanged);
                else dpd.RemoveValueChanged(fe, Element_OnTextChanged);
            }
            catch
            {
                // Ignore descriptor issues in rare runtime cases.
            }
        }

        private static void TryAdjust(FrameworkElement fe)
        {
            if (fe == null) return;
            if (!GetIsEnabled(fe)) return;
            if (GetIsAdjusting(fe)) return;

            var text = GetElementText(fe);
            if (string.IsNullOrEmpty(text)) return;

            if (!ShouldAutoScaleForCurrentCulture(text))
            {
                RestoreOriginalFontSize(fe);
                return;
            }

            var availableWidth = GetAvailableWidth(fe);
            if (double.IsNaN(availableWidth) || availableWidth <= 1) return;

            var min = GetMinFontSize(fe);
            if (double.IsNaN(min) || min <= 0) min = 6d;

            var step = GetStep(fe);
            if (double.IsNaN(step) || step < 0.1) step = 0.5d;

            var current = GetElementFontSize(fe);
            if (double.IsNaN(current) || current <= 0) return;

            var max = GetMaxFontSize(fe);
            if (double.IsNaN(max) || max <= 0) max = current;
            // Never enlarge: auto-fit should only reduce font size when needed.
            if (max > current) max = current;

            var startFont = Math.Min(current, max);
            if (startFont < min) startFont = min;

            SetIsAdjusting(fe, true);
            try
            {
                var font = startFont;
                var desired = MeasureTextWidth(fe, text, font);
                if (desired <= 0) return;

                while (font > min && desired > availableWidth + 0.5)
                {
                    font = Math.Max(min, font - step);
                    desired = MeasureTextWidth(fe, text, font);
                    if (desired <= 0) break;
                }

                // Hard-fit fallback: when very narrow slots (e.g., 28px) still overflow at MinFontSize,
                // keep shrinking proportionally so text always fits in the available width.
                if (desired > availableWidth + 0.5)
                {
                    var hardFont = font;
                    for (var i = 0; i < 6 && desired > availableWidth + 0.5; i++)
                    {
                        var ratio = availableWidth / Math.Max(1.0, desired);
                        hardFont = Math.Max(1.0, hardFont * ratio);
                        desired = MeasureTextWidth(fe, text, hardFont);
                        if (desired <= 0) break;
                    }

                    font = hardFont;
                }

                if (!double.IsNaN(font) && font > 0 && Math.Abs(current - font) > 0.01)
                {
                    SetElementFontSize(fe, font);
                }
            }
            finally
            {
                SetIsAdjusting(fe, false);
            }
        }

        private static string GetElementText(FrameworkElement fe)
        {
            if (fe is TextBlock tb) return tb.Text;
            if (fe is Label label) return label.Content as string ?? label.Content?.ToString();
            return null;
        }

        private static bool ShouldAutoScaleForCurrentCulture(string text)
        {
            // Requirement: auto-scale for English UI only, keep Chinese font size unchanged.
            var culture = CultureInfo.CurrentUICulture;
            var name = culture?.Name ?? string.Empty;
            if (name.StartsWith("en", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Fallback: if actual rendered text is Latin-heavy, still auto-scale.
            // This avoids clipping when culture detection is out of sync.
            if (string.IsNullOrWhiteSpace(text)) return false;
            foreach (var ch in text)
            {
                if ((ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z'))
                {
                    return true;
                }
            }

            return false;
        }

        private static void RestoreOriginalFontSize(FrameworkElement fe)
        {
            var original = GetOriginalFontSize(fe);
            if (double.IsNaN(original) || original <= 0) return;

            var current = GetElementFontSize(fe);
            if (double.IsNaN(current) || current <= 0) return;

            if (Math.Abs(current - original) > 0.01)
            {
                SetElementFontSize(fe, original);
            }
        }

        private static double GetElementFontSize(FrameworkElement fe)
        {
            if (fe is TextBlock tb) return tb.FontSize;
            if (fe is Label label) return label.FontSize;
            return double.NaN;
        }

        private static void SetElementFontSize(FrameworkElement fe, double value)
        {
            if (fe is TextBlock tb) tb.FontSize = value;
            else if (fe is Label label) label.FontSize = value;
        }

        private static double GetAvailableWidth(FrameworkElement fe)
        {
            double width = double.PositiveInfinity;

            // Explicit width on the element itself should be a hard cap.
            if (!double.IsNaN(fe.Width) && !double.IsInfinity(fe.Width) && fe.Width > 1)
            {
                width = Math.Min(width, fe.Width - fe.Margin.Left - fe.Margin.Right);
            }

            if (!double.IsNaN(fe.MaxWidth) && !double.IsInfinity(fe.MaxWidth) && fe.MaxWidth > 1)
            {
                width = Math.Min(width, fe.MaxWidth - fe.Margin.Left - fe.Margin.Right);
            }

            // Prefer the real layout slot first. This is usually the most accurate
            // "space actually assigned by layout" for the element.
            var slot = LayoutInformation.GetLayoutSlot(fe);
            if (!double.IsNaN(slot.Width) && !double.IsInfinity(slot.Width))
            {
                var slotWidth = slot.Width - fe.Margin.Left - fe.Margin.Right;
                if (slotWidth > 1) width = Math.Min(width, slotWidth);
            }

            if (fe.ActualWidth > 1) width = Math.Min(width, fe.ActualWidth);

            // Immediate parent may be a StackPanel that does not constrain width.
            // Walk a few ancestors and take the tightest finite width as fallback.
            DependencyObject ancestor = fe.Parent ?? VisualTreeHelper.GetParent(fe);
            var depth = 0;
            while (ancestor != null && depth < 8)
            {
                if (ancestor is FrameworkElement af && af.ActualWidth > 1)
                {
                    var candidate = af.ActualWidth;

                    // If ancestor sets explicit width, treat it as a stronger cap.
                    if (!double.IsNaN(af.Width) && !double.IsInfinity(af.Width) && af.Width > 1)
                    {
                        candidate = Math.Min(candidate, af.Width);
                    }

                    if (!double.IsNaN(af.MaxWidth) && !double.IsInfinity(af.MaxWidth) && af.MaxWidth > 1)
                    {
                        candidate = Math.Min(candidate, af.MaxWidth);
                    }

                    if (ancestor is Control ac)
                    {
                        candidate -= ac.Padding.Left + ac.Padding.Right;
                        candidate -= ac.BorderThickness.Left + ac.BorderThickness.Right;
                    }
                    else if (ancestor is Border ab)
                    {
                        candidate -= ab.Padding.Left + ab.Padding.Right;
                        candidate -= ab.BorderThickness.Left + ab.BorderThickness.Right;
                    }

                    if (candidate > 1) width = Math.Min(width, candidate);
                }

                ancestor = (ancestor as FrameworkElement)?.Parent ?? VisualTreeHelper.GetParent(ancestor);
                depth++;
            }

            if (double.IsInfinity(width) || double.IsNaN(width) || width <= 1) return -1;

            // Keep width as inner text area.
            if (fe is Control control)
            {
                width -= control.Padding.Left + control.Padding.Right;
                width -= control.BorderThickness.Left + control.BorderThickness.Right;
            }
            else if (fe is Border border)
            {
                width -= border.Padding.Left + border.Padding.Right;
                width -= border.BorderThickness.Left + border.BorderThickness.Right;
            }

            return width;
        }

        private static double MeasureTextWidth(FrameworkElement fe, string text, double fontSize)
        {
            try
            {
                var dpi = VisualTreeHelper.GetDpi(fe);
                var culture = CultureInfo.CurrentUICulture;

                if (fe.Language != null)
                {
                    try
                    {
                        culture = fe.Language.GetEquivalentCulture();
                    }
                    catch
                    {
                    }
                }

                var fontFamily = SystemFonts.MessageFontFamily;
                var fontStyle = FontStyles.Normal;
                var fontWeight = FontWeights.Normal;
                var fontStretch = FontStretches.Normal;
                Brush foreground = Brushes.Black;
                var flowDirection = FlowDirection.LeftToRight;

                if (fe is TextBlock tb)
                {
                    fontFamily = tb.FontFamily;
                    fontStyle = tb.FontStyle;
                    fontWeight = tb.FontWeight;
                    fontStretch = tb.FontStretch;
                    foreground = tb.Foreground;
                    flowDirection = tb.FlowDirection;
                }
                else if (fe is Label label)
                {
                    fontFamily = label.FontFamily;
                    fontStyle = label.FontStyle;
                    fontWeight = label.FontWeight;
                    fontStretch = label.FontStretch;
                    foreground = label.Foreground;
                    flowDirection = label.FlowDirection;
                }

                var typeface = new Typeface(fontFamily, fontStyle, fontWeight, fontStretch);
                var formatted = new FormattedText(
                    text,
                    culture,
                    flowDirection,
                    typeface,
                    fontSize,
                    foreground,
                    dpi.PixelsPerDip);

                return formatted.WidthIncludingTrailingWhitespace;
            }
            catch
            {
                return -1;
            }
        }
    }
}
