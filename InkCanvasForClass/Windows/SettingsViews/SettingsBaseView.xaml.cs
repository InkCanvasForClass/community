using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Ink_Canvas.Components;
using iNKORE.UI.WPF.Helpers;

namespace Ink_Canvas.Windows.SettingsViews {

    public class SettingsViewPanel {
        public string Title { get; set; }
        public Visibility _TitleVisibility => String.IsNullOrWhiteSpace(Title) ? Visibility.Collapsed : Visibility.Visible;
        public Thickness _PanelMargin =>
            String.IsNullOrWhiteSpace(Title) ? new Thickness(0) : new Thickness(0, 12, 0, 0);
        public ObservableCollection<SettingsItem> Items { get; set; } = new ObservableCollection<SettingsItem>() { };
    }

    public enum SettingsItemType {
        Plain, // 只显示Title和Description
        SingleToggleSwtich,
        ToggleSwitchWithArrowButton,
        SelectionButtons,
    }

    public class SettingsItem : INotifyPropertyChanged {
        public string Title { get; set; }
        public string Description { get; set; }
        public SettingsItemType Type { get; set; } = SettingsItemType.Plain;
        public bool IsClickable { get; set; } = false;
        public bool IsSeparatorVisible { get; set; } = true;
        public Visibility _SeparatorVisibility => IsSeparatorVisible ? Visibility.Visible : Visibility.Collapsed;
        public Visibility _ToggleSwitchVisibility =>
            Type == SettingsItemType.SingleToggleSwtich || Type == SettingsItemType.ToggleSwitchWithArrowButton ? Visibility.Visible : Visibility.Collapsed;
        private bool _toggleSwitchToggled;
        public bool ToggleSwitchToggled {
            get => _toggleSwitchToggled;
            set {
                if (_toggleSwitchToggled != value) {
                    _toggleSwitchToggled = value;
                    OnPropertyChanged(nameof(ToggleSwitchToggled)); // 通知绑定控件属性变化
                    OnToggleSwitchToggled?.Invoke(this, EventArgs.Empty); // 触发事件
                }
            }
        }
        public event EventHandler OnToggleSwitchToggled;
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private SolidColorBrush _toggleSwitchBackground = new SolidColorBrush(Color.FromRgb(53, 132, 228));
        public SolidColorBrush ToggleSwitchBackground {
            get => _toggleSwitchBackground;
            set {
                if (_toggleSwitchBackground != value) {
                    _toggleSwitchBackground = value;
                    OnPropertyChanged(nameof(ToggleSwitchBackground)); // 通知绑定控件属性变化
                }
            }
        }

        private bool _toggleSwitchEnabled = true;
        public bool ToggleSwitchEnabled {
            get => _toggleSwitchEnabled;
            set {
                if (_toggleSwitchEnabled != value) {
                    _toggleSwitchEnabled = value;
                    OnPropertyChanged(nameof(ToggleSwitchEnabled)); // 通知绑定控件属性变化
                }
            }
        }
    }
    
    public partial class SettingsBaseView : UserControl {
        public SettingsBaseView() {
            InitializeComponent();
            SettingsViewBaseItemsControl.ItemsSource = SettingsPanels;
        }

        public ObservableCollection<SettingsViewPanel> SettingsPanels { get; set; } =
            new ObservableCollection<SettingsViewPanel>() { };

        public event EventHandler<RoutedEventArgs> IsTopBarNeedShadowEffect; 
        public event EventHandler<RoutedEventArgs> IsTopBarNeedNoShadowEffect;

        private void ScrollViewerEx_ScrollChanged(object sender, ScrollChangedEventArgs e) {
            var scrollViewer = (ScrollViewer)sender;
            if (scrollViewer.VerticalOffset >= 10) {
                IsTopBarNeedShadowEffect?.Invoke(this, new RoutedEventArgs());
            } else {
                IsTopBarNeedNoShadowEffect?.Invoke(this, new RoutedEventArgs());
            }
        }

        private void ScrollBar_Scroll(object sender, RoutedEventArgs e) {
            var scrollbar = (ScrollBar)sender;
            var scrollviewer = scrollbar.FindAscendant<ScrollViewer>();
            if (scrollviewer != null) scrollviewer.ScrollToVerticalOffset(scrollbar.Track.Value);
        }

        private void ScrollBarTrack_MouseEnter(object sender, MouseEventArgs e) {
            var border = (Border)sender;
            if (border.Child is Track track) {
                track.Width = 16;
                track.Margin = new Thickness(0, 0, -2, 0);
                var scrollbar = track.FindAscendant<ScrollBar>();
                if (scrollbar != null) scrollbar.Width = 16;
                var grid = track.FindAscendant<Grid>();
                if (grid.FindDescendantByName("ScrollBarBorderTrackBackground") is Border backgroundBorder) {
                    backgroundBorder.Width = 8;
                    backgroundBorder.CornerRadius = new CornerRadius(4);
                    backgroundBorder.Opacity = 1;
                }
                var thumb = track.Thumb.Template.FindName("ScrollbarThumbEx", track.Thumb) ;
                if (thumb != null) {
                    var _thumb = thumb as Border;
                    _thumb.CornerRadius = new CornerRadius(4);
                    _thumb.Width = 8;
                    _thumb.Margin = new Thickness(-0.75, 0, 1, 0);
                    _thumb.Background = new SolidColorBrush(Color.FromRgb(138, 138, 138));
                }
            }
        }

        private void ToggleSwitch_OnToggled(object sender, RoutedEventArgs e) {
            var toggleswitch = sender as ToggleSwitch;
            var item = toggleswitch.Tag as SettingsItem;
            item.ToggleSwitchToggled = toggleswitch.IsOn;
        }

        private void ScrollBarTrack_MouseLeave(object sender, MouseEventArgs e) {
            var border = (Border)sender;
            border.Background = new SolidColorBrush(Colors.Transparent);
            border.CornerRadius = new CornerRadius(0);
            if (border.Child is Track track) {
                track.Width = 6;
                track.Margin = new Thickness(0, 0, 0, 0);
                var scrollbar = track.FindAscendant<ScrollBar>();
                if (scrollbar != null) scrollbar.Width = 6;
                var grid = track.FindAscendant<Grid>();
                if (grid.FindDescendantByName("ScrollBarBorderTrackBackground") is Border backgroundBorder) {
                    backgroundBorder.Width = 3;
                    backgroundBorder.CornerRadius = new CornerRadius(1.5);
                    backgroundBorder.Opacity = 0;
                }
                var thumb = track.Thumb.Template.FindName("ScrollbarThumbEx", track.Thumb) ;
                if (thumb != null) {
                    var _thumb = thumb as Border;
                    _thumb.CornerRadius = new CornerRadius(1.5);
                    _thumb.Width = 3;
                    _thumb.Margin = new Thickness(0);
                    _thumb.Background = new SolidColorBrush(Color.FromRgb(195, 195, 195));
                }
            }
        }

        private void ScrollbarThumb_MouseDown(object sender, MouseButtonEventArgs e) {
            var thumb = (Thumb)sender;
            var border = thumb.Template.FindName("ScrollbarThumbEx",thumb);
            ((Border)border).Background = new SolidColorBrush(Color.FromRgb(95, 95, 95));
        }

        private void ScrollbarThumb_MouseUp(object sender, MouseButtonEventArgs e) {
            var thumb = (Thumb)sender;
            var border = thumb.Template.FindName("ScrollbarThumbEx",thumb);
            ((Border)border).Background = new SolidColorBrush(Color.FromRgb(138, 138, 138));
        }
    }

}
