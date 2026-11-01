using System;
using System.Globalization;
using System.Windows.Automation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Ink_Canvas.Properties;

namespace Ink_Canvas.Controls
{
    public partial class WordLectureBar : UserControl
    {
        internal event Action<int> Navigate;
        internal event Action Resume;
        internal event Action<int> Zoom;
        private event Action StateChanged;
        private WordLectureBar _source;
        private bool _showNavigation = true;
        public bool ShowZoom
        {
            get => ZoomControls.Visibility == Visibility.Visible;
            set => ZoomControls.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }
        private (string Status, string Document, int Page, int Total, bool Resume, int Zoom, bool Navigation, string Culture, string NumberCulture)? _state;
        internal bool ShowNavigation
        {
            get => _showNavigation;
            set
            {
                if (_showNavigation == value) return;
                _showNavigation = value;
                if (_state is { } state) SetState(state.Status, state.Document, state.Page, state.Total, state.Resume, state.Zoom);
            }
        }
        public WordLectureBar()
        {
            InitializeComponent();
            // Keep command names in Word's XAML namescope, then host them inside
            // the shared PPT surface so scaling/opacity/shadow apply exactly once.
            Navigation.Children.Remove(ExtraControls);
            PageNavigation.AdditionalContent = ExtraControls;
            PageNavigation.PreviousClick += (_, _) => (_source ?? this).Navigate?.Invoke(-1);
            PageNavigation.NextClick += (_, _) => (_source ?? this).Navigate?.Invoke(1);
        }
        internal void ApplySettings(WordLinkSettings settings, PowerPointSettings ppt, bool right)
        {
            bool dark = settings.DarkBackground, showPage = settings.ShowPageNumber, shadow = settings.NavigationShadow;
            double scale = settings.NavigationScale, opacity = settings.NavigationOpacity, shadowSize = settings.ShadowSize;
            var direction = right ? PPTNavBar.NavDirection.RightBottom : PPTNavBar.NavDirection.LeftBottom;
            if (settings.UsePptAppearance && ppt != null)
            {
                bool global = right ? ppt.PPTRBUseGlobalSettings : ppt.PPTLBUseGlobalSettings;
                dark = global ? ppt.PPTGlobalBlackBackground : right ? ppt.PPTRBBlackBackground : ppt.PPTLBBlackBackground;
                showPage = global ? ppt.PPTGlobalShowPageNumber : right ? ppt.PPTRBShowPageNumber : ppt.PPTLBShowPageNumber;
                scale = global ? ppt.PPTNavBarScale : right ? ppt.PPTRBButtonScale : ppt.PPTLBButtonScale;
                opacity = global ? ppt.PPTGlobalButtonOpacity : right ? ppt.PPTRBButtonOpacity : ppt.PPTLBButtonOpacity;
                (shadow, shadowSize) = ppt.GetPPTNavBarShadow(direction);
            }
            scale = double.IsFinite(scale) ? Math.Clamp(scale, .5, 2) : 1;
            opacity = double.IsFinite(opacity) ? Math.Clamp(opacity, .1, 1) : 1;
            shadowSize = double.IsFinite(shadowSize) ? Math.Clamp(shadowSize, 0, 60) : 16;
            PageNavigation.Direction = direction;
            PageNavigation.ApplyTheme(dark);
            PageNavigation.SetBarScale(scale);
            PageNavigation.SetBarOpacity(opacity);
            PageNavigation.SetShadow(shadow, shadowSize);
            PageNavigation.SetPageButtonVisibility(showPage ? Visibility.Visible : Visibility.Collapsed);
            Resources["WordNavForeground"] = dark ? Brushes.White : new SolidColorBrush(Color.FromRgb(39, 39, 42));
            Resources["WordNavBackground"] = new SolidColorBrush(dark ? Color.FromRgb(39, 39, 42) : Color.FromRgb(244, 244, 245));
            Resources["WordNavBorder"] = new SolidColorBrush(dark ? Color.FromRgb(82, 82, 91) : Color.FromRgb(161, 161, 170));
            Resources["WordNavFeedback"] = dark ? Brushes.White : new SolidColorBrush(Color.FromRgb(24, 24, 27));
            ShowZoom = right && settings.ShowZoom;
        }
        // Peer controls share presentation and route actions to one controller.
        // They never create another Word connection or polling loop.
        internal void Mirror(WordLectureBar source)
        {
            if (source == null || source == this || source._source != null)
                throw new ArgumentException("A mirror requires a primary Word navigation bar.", nameof(source));
            if (_source != null) _source.StateChanged -= RefreshMirror;
            _source = source;
            source.StateChanged += RefreshMirror;
            SetBinding(VisibilityProperty, new Binding(nameof(Visibility)) { Source = source, Mode = BindingMode.OneWay });
            RefreshMirror();
        }
        private void RefreshMirror()
        {
            if (_source._state is not { } state) return;
            _showNavigation = _source.ShowNavigation;
            SetState(state.Status, state.Document, state.Page, state.Total, state.Resume, state.Zoom);
        }
        internal void SetState(string status, string document, int page, int total, bool resume, int zoom = 0)
        {
            var state = (status, document, page, total, resume, zoom, ShowNavigation, CultureInfo.CurrentUICulture.Name, CultureInfo.CurrentCulture.Name);
            var visibility = status == "Ready" && !ShowNavigation ? Visibility.Collapsed : Visibility.Visible;
            if (_state == state && Visibility == visibility) return;
            if (_source == null) Visibility = visibility;
            StatusText.Text = WordStrings.Get(status);
            ToolTip = string.IsNullOrEmpty(document) ? StatusText.Text : document + " — " + StatusText.Text;
            StatusPanel.Visibility = _source != null || (status == "Ready" && !resume) ? Visibility.Collapsed : Visibility.Visible;
            PageNavigation.CurrentSlide = page;
            PageNavigation.TotalSlides = total;
            AutomationProperties.SetName(PageNavigation, page > 0 ? string.Format(WordStrings.PageFormat, page, total) : WordStrings.Get(status));
            PageNavigation.SetNavigationAvailability(status == "Ready" && page > 1, status == "Ready" && page < total, false);
            ZoomText.Text = zoom > 0 ? zoom.ToString(CultureInfo.CurrentCulture) + "%" : "—";
            ZoomOutButton.IsEnabled = status == "Ready" && zoom > 10;
            ZoomInButton.IsEnabled = status == "Ready" && zoom is >= 10 and < 500;
            ResumeButton.Visibility = resume ? Visibility.Visible : Visibility.Collapsed;
            Navigation.Visibility = ShowNavigation ? Visibility.Visible : Visibility.Collapsed;
            _state = state;
            StateChanged?.Invoke();
        }
        private void ResumeClick(object sender, RoutedEventArgs e) => (_source ?? this).Resume?.Invoke();
        private void ZoomOutClick(object sender, RoutedEventArgs e) => (_source ?? this).Zoom?.Invoke(-10);
        private void ZoomInClick(object sender, RoutedEventArgs e) => (_source ?? this).Zoom?.Invoke(10);
    }
}
