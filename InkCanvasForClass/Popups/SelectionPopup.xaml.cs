using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static Ink_Canvas.Popups.ColorPalette;

namespace Ink_Canvas.Popups {
    public partial class SelectionPopup : UserControl {
        public SelectionPopup() {
            InitializeComponent();
            SelectionModeTabButtonBorders = new Border[] {
                LassoTabButton, RectangleTabButton
            };
            SelectionModeTabButtonIndicators = new SimpleStackPanel[] {
                LassoTabButtonIndicator, RectangleTabButtonIndicator
            };
            SelectionModeTabButtonIcons = new GeometryDrawing[] {
                LassoTabButtonIcon, RectangleTabButtonIcon
            };
            SelectionModeTabButtonTexts = new TextBlock[] {
                LassoTabButtonText, RectangleTabButtonText
            };
            UpdateSelectionModeButtonsCheckedDisplayStatus();
        }

        private bool isCloseButtonDown = false;
        public event EventHandler<RoutedEventArgs> SelectionPopupShouldCloseEvent;
        public event EventHandler<RoutedEventArgs> SelectAllEvent;
        public event EventHandler<RoutedEventArgs> UnSelectEvent;
        public event EventHandler<RoutedEventArgs> ReverseSelectEvent;
        public event EventHandler<RoutedEventArgs> ApplyScaleToStylusTipChanged;
        public event EventHandler<RoutedEventArgs> OnlyHitTestFullyContainedStrokesChanged;
        public event EventHandler<RoutedEventArgs> AllowClickToSelectLockedStrokeChanged;

        public bool ApplyScaleToStylusTip {
            get => cb1.IsChecked??false;
            set {
                isProgramicallyChangeCheckBox = true;
                cb1.IsChecked = value;
                isProgramicallyChangeCheckBox = false;
            }
        }

        public bool OnlyHitTestFullyContainedStrokes {
            get => cb2.IsChecked ?? false;
            set {
                isProgramicallyChangeCheckBox = true;
                cb2.IsChecked = value;
                isProgramicallyChangeCheckBox = false;
            }
        }

        public bool AllowClickToSelectLockedStroke {
            get => cb3.IsChecked ?? false;
            set {
                isProgramicallyChangeCheckBox = true;
                cb3.IsChecked = value;
                isProgramicallyChangeCheckBox = false;
            }
        }

        public Border[] SelectionModeTabButtonBorders;
        public SimpleStackPanel[] SelectionModeTabButtonIndicators;
        public GeometryDrawing[] SelectionModeTabButtonIcons;
        public TextBlock[] SelectionModeTabButtonTexts;

        public enum SelectionMode {
            LassoMode,
            RectangleMode
        }

        private SelectionMode _selectionModeSelected = SelectionMode.LassoMode;
        public SelectionMode SelectionModeSelected {
            get => _selectionModeSelected;
            set {
                _selectionModeSelected = value;
                UpdateSelectionModeButtonsCheckedDisplayStatus();
            }
        }

        public event EventHandler<SelectionModeChangedEventArgs> SelectionModeChanged;

        public class SelectionModeChangedEventArgs : EventArgs
        {
            public SelectionMode PreviousMode { get; set; }
            public SelectionMode NowMode { get; set; }
        }

        private void UpdateSelectionModeButtonsCheckedDisplayStatus() {
            foreach (var bd in SelectionModeTabButtonBorders) {
                bd.Background = new SolidColorBrush(Colors.Transparent);
            }
            foreach (var indicator in SelectionModeTabButtonIndicators) {
                indicator.Visibility = Visibility.Hidden;
            }
            foreach (var gd in SelectionModeTabButtonIcons) {
                gd.Brush = new SolidColorBrush(Color.FromRgb(63, 63, 70));
            }
            foreach (var text in SelectionModeTabButtonTexts) {
                text.Foreground = new SolidColorBrush(Color.FromRgb(63, 63, 70));
                text.FontWeight = FontWeights.Normal;
            }

            SelectionModeTabButtonBorders[(int)_selectionModeSelected].Background = new SolidColorBrush(Color.FromArgb(34, 59, 130, 246));
            SelectionModeTabButtonIndicators[(int)_selectionModeSelected].Visibility = Visibility.Visible;
            SelectionModeTabButtonIcons[(int)_selectionModeSelected].Brush = new SolidColorBrush(Color.FromRgb(37, 99, 235));
            SelectionModeTabButtonTexts[(int)_selectionModeSelected].Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235));
            SelectionModeTabButtonTexts[(int)_selectionModeSelected].FontWeight = FontWeights.Bold;
        }

        private bool isProgramicallyChangeCheckBox = false;

        private void cb1cked(object sender, RoutedEventArgs e) {
            if (isProgramicallyChangeCheckBox) return;
            ApplyScaleToStylusTipChanged?.Invoke(null,new RoutedEventArgs());
        }

        private void cb2cked(object sender, RoutedEventArgs e) {
            if (isProgramicallyChangeCheckBox) return;
            OnlyHitTestFullyContainedStrokesChanged?.Invoke(null,new RoutedEventArgs());
        }

        private void cb3cked(object sender, RoutedEventArgs e) {
            if (isProgramicallyChangeCheckBox) return;
            AllowClickToSelectLockedStrokeChanged?.Invoke(null,new RoutedEventArgs());
        }

        private void CloseButtonBorder_MouseDown(object sender, MouseButtonEventArgs e) {
            isCloseButtonDown = true;
            CloseButtonBorder.Background = new SolidColorBrush(Color.FromArgb(34, 220, 38, 38));
        }

        private void CloseButtonBorder_MouseLeave(object sender, MouseEventArgs e) {
            isCloseButtonDown = false;
            CloseButtonBorder.Background = new SolidColorBrush(Colors.Transparent);
        }

        private void SelectionModeTabButton_MouseDown(object sender, MouseButtonEventArgs e) {
            var pre = _selectionModeSelected;
            _selectionModeSelected = (SelectionMode)Array.IndexOf(SelectionModeTabButtonBorders, (Border)sender);
            UpdateSelectionModeButtonsCheckedDisplayStatus();

            SelectionModeChanged?.Invoke(this, new SelectionModeChangedEventArgs() {
                PreviousMode = pre,
                NowMode = _selectionModeSelected,
            });
        }

        private void CloseButtonBorder_MouseUp(object sender, MouseButtonEventArgs e) {
            if (!isCloseButtonDown) return;

            CloseButtonBorder_MouseLeave(null, null);
            SelectionPopupShouldCloseEvent?.Invoke(this,new RoutedEventArgs());
        }

        private void SelectAllButtonClicked(object sender, RoutedEventArgs e) {
            SelectAllEvent?.Invoke(this,new RoutedEventArgs());
        }

        private void UnSelectButtonClicked(object sender, RoutedEventArgs e) {
            UnSelectEvent?.Invoke(this,new RoutedEventArgs());
        }

        private void ReverseSelectButtonClicked(object sender, RoutedEventArgs e) {
            ReverseSelectEvent?.Invoke(this,new RoutedEventArgs());
        }
    }
}
