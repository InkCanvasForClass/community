using Ink_Canvas.Controls;
using Ink_Canvas.Helpers;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class PPTFlipButtonSettingsPage : Page
    {
        private bool _isLoaded = false;
        private DelayAction _sliderDelayAction = new DelayAction();
        private PPTNavBar.NavDirection _selectedDirection = PPTNavBar.NavDirection.LeftSide;

        public PPTFlipButtonSettingsPage()
        {
            InitializeComponent();
            Loaded += PPTFlipButtonSettingsPage_Loaded;
            Unloaded += PPTFlipButtonSettingsPage_Unloaded;
        }

        private void PPTFlipButtonSettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
            _isLoaded = true;
            UpdateAllSliderTexts();
            UpdatePreview();
            SelectPosition(_selectedDirection);
            SliderTouchHelper.AddTouchSupportToAllSliders(this);
        }

        private void PPTFlipButtonSettingsPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = false;
        }

        private void PPTFlipButtonSettingsPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
        }

        private void LoadSettings()
        {
            _isLoaded = false;
            var ppt = SettingsManager.Settings.PowerPointSettings;

            CardShowPPTButton.IsOn = ppt.ShowPPTButton;
            PPTNavBarScaleValueSlider.Value = ppt.PPTNavBarScale;
            CardEnablePPTButtonPageClickable.IsOn = ppt.EnablePPTButtonPageClickable;
            CardEnablePPTButtonEnhancedPreview.IsOn = ppt.EnablePPTButtonEnhancedPreview;
            CardEnablePPTButtonLongPressPageTurn.IsOn = ppt.EnablePPTButtonLongPressPageTurn;

            UpdatePositionCheckboxesFromSettings();

            _isLoaded = true;
        }

        private void UpdatePositionCheckboxesFromSettings()
        {
            var ppt = SettingsManager.Settings.PowerPointSettings;
            var displayOpt = ppt.PPTButtonsDisplayOption.ToString();
            if (displayOpt.Length < 4) return;

            CheckboxPosLeftBottom.IsChecked = displayOpt[0] == '2';
            CheckboxPosRightBottom.IsChecked = displayOpt[1] == '2';
            CheckboxPosLeft.IsChecked = displayOpt[2] == '2';
            CheckboxPosRight.IsChecked = displayOpt[3] == '2';
        }

        private void UpdateAllSliderTexts()
        {
            UpdateSliderText(OffsetSlider, OffsetText, "{0:F0}");
            UpdateSliderText(OpacitySlider, OpacityText, "{0:P0}");
            UpdateSliderText(PPTNavBarScaleValueSlider, PPTNavBarScaleText, "{0:F2}");
        }

        private void UpdateSliderText(Slider slider, TextBlock textBlock, string format)
        {
            if (slider == null || textBlock == null) return;
            textBlock.Text = string.Format(format, slider.Value);
        }

        #region Preview & Selection

        public void UpdatePreview()
        {
            var ppt = SettingsManager.Settings.PowerPointSettings;
            var displayOpt = ppt.PPTButtonsDisplayOption.ToString();

            UpdatePreviewNavBar(PreviewLS, PreviewLSBorder, displayOpt, 2, ppt.PPTLSButtonPosition, ppt.PPTLSButtonOpacity, ppt.PPTSButtonsOption.ToString());
            UpdatePreviewNavBar(PreviewRS, PreviewRSBorder, displayOpt, 3, ppt.PPTRSButtonPosition, ppt.PPTRSButtonOpacity, ppt.PPTSButtonsOption.ToString());
            UpdatePreviewNavBar(PreviewLB, PreviewLBBorder, displayOpt, 0, ppt.PPTLBButtonPosition, ppt.PPTLBButtonOpacity, ppt.PPTBButtonsOption.ToString());
            UpdatePreviewNavBar(PreviewRB, PreviewRBBorder, displayOpt, 1, ppt.PPTRBButtonPosition, ppt.PPTRBButtonOpacity, ppt.PPTBButtonsOption.ToString());
        }

        private void UpdatePreviewNavBar(PPTNavBar navBar, Border border, string displayOpt, int index, int offset, double opacity, string groupOpt)
        {
            bool isEnabled = displayOpt.Length > index && displayOpt[index] == '2';
            bool showTotal = CardShowPPTButton.IsOn && isEnabled;
            border.Visibility = showTotal ? Visibility.Visible : Visibility.Collapsed;

            if (!showTotal) return;

            double scale = SettingsManager.Settings.PowerPointSettings.PPTNavBarScale;
            navBar.LayoutTransform = new ScaleTransform(scale, scale);

            navBar.SetBarOpacity(opacity);

            var direction = navBar.Direction;
            if (direction == PPTNavBar.NavDirection.LeftSide)
            {
                border.Margin = new Thickness(6, 0, 0, offset * 2);
            }
            else if (direction == PPTNavBar.NavDirection.RightSide)
            {
                border.Margin = new Thickness(0, 0, 6, offset * 2);
            }
            else if (direction == PPTNavBar.NavDirection.LeftBottom)
            {
                border.Margin = new Thickness(6 + offset, 0, 0, 6);
            }
            else if (direction == PPTNavBar.NavDirection.RightBottom)
            {
                border.Margin = new Thickness(0, 0, 6 + offset, 6);
            }

            bool showPage = groupOpt.Length > 0 && groupOpt[0] == '2';
            navBar.PageButtonBorder.Visibility = showPage ? Visibility.Visible : Visibility.Collapsed;

            bool isDark = groupOpt.Length > 2 && groupOpt[2] == '2';
            navBar.ApplyTheme(isDark);
        }

        private void SelectPosition(PPTNavBar.NavDirection direction)
        {
            _selectedDirection = direction;

            string title = direction switch
            {
                PPTNavBar.NavDirection.LeftSide => Properties.PPTStrings.Position_Left,
                PPTNavBar.NavDirection.RightSide => Properties.PPTStrings.Position_Right,
                PPTNavBar.NavDirection.LeftBottom => Properties.PPTStrings.Position_LeftBottom,
                PPTNavBar.NavDirection.RightBottom => Properties.PPTStrings.Position_RightBottom,
                _ => Properties.PPTStrings.Position_Left
            };
            SelectedPositionTitle.Text = title;
            SelectedPositionTitle.Visibility = Visibility.Visible;

            CardEnablePositionButton.Visibility = Visibility.Visible;
            CardOffset.Visibility = Visibility.Visible;
            CardOpacity.Visibility = Visibility.Visible;
            CardShowPageNumber.Visibility = Visibility.Visible;
            CardBlackBackground.Visibility = Visibility.Visible;

            // Update card headers
            CardEnablePositionButton.Header = string.Format(Properties.PPTStrings.EnablePositionButton, title);
            CardOffset.Header = direction switch
            {
                PPTNavBar.NavDirection.LeftSide => Properties.PPTStrings.LeftOffset,
                PPTNavBar.NavDirection.RightSide => Properties.PPTStrings.RightOffset,
                PPTNavBar.NavDirection.LeftBottom => Properties.PPTStrings.LeftBottomOffset,
                PPTNavBar.NavDirection.RightBottom => Properties.PPTStrings.RightBottomOffset,
                _ => Properties.PPTStrings.LeftOffset
            };
            CardOpacity.Header = direction switch
            {
                PPTNavBar.NavDirection.LeftSide => Properties.PPTStrings.LeftOpacity,
                PPTNavBar.NavDirection.RightSide => Properties.PPTStrings.RightOpacity,
                PPTNavBar.NavDirection.LeftBottom => Properties.PPTStrings.LeftBottomOpacity,
                PPTNavBar.NavDirection.RightBottom => Properties.PPTStrings.RightBottomOpacity,
                _ => Properties.PPTStrings.LeftOpacity
            };

            // Adjust slider ranges for bottom buttons
            if (direction == PPTNavBar.NavDirection.LeftBottom || direction == PPTNavBar.NavDirection.RightBottom)
            {
                OffsetSlider.Minimum = -100;
            }
            else
            {
                OffsetSlider.Minimum = -500;
            }

            // Load current values
            _isLoaded = false;
            var ppt = SettingsManager.Settings.PowerPointSettings;
            var displayOpt = ppt.PPTButtonsDisplayOption.ToString();
            int idx = GetDisplayOptionIndex(direction);
            CardEnablePositionButton.IsOn = displayOpt.Length > idx && displayOpt[idx] == '2';

            OffsetSlider.Value = direction switch
            {
                PPTNavBar.NavDirection.LeftSide => ppt.PPTLSButtonPosition,
                PPTNavBar.NavDirection.RightSide => ppt.PPTRSButtonPosition,
                PPTNavBar.NavDirection.LeftBottom => ppt.PPTLBButtonPosition,
                PPTNavBar.NavDirection.RightBottom => ppt.PPTRBButtonPosition,
                _ => 0
            };

            OpacitySlider.Value = direction switch
            {
                PPTNavBar.NavDirection.LeftSide => ppt.PPTLSButtonOpacity,
                PPTNavBar.NavDirection.RightSide => ppt.PPTRSButtonOpacity,
                PPTNavBar.NavDirection.LeftBottom => ppt.PPTLBButtonOpacity,
                PPTNavBar.NavDirection.RightBottom => ppt.PPTRBButtonOpacity,
                _ => 0.5
            };

            string groupOpt;
            if (IsSideButton(direction))
                groupOpt = ppt.PPTSButtonsOption.ToString();
            else
                groupOpt = ppt.PPTBButtonsOption.ToString();

            CheckboxShowPageNumber.IsChecked = groupOpt.Length > 0 && groupOpt[0] == '2';
            CheckboxBlackBackground.IsChecked = groupOpt.Length > 2 && groupOpt[2] == '2';

            UpdateAllSliderTexts();
            _isLoaded = true;
        }

        private int GetDisplayOptionIndex(PPTNavBar.NavDirection dir)
        {
            return dir switch
            {
                PPTNavBar.NavDirection.LeftBottom => 0,
                PPTNavBar.NavDirection.RightBottom => 1,
                PPTNavBar.NavDirection.LeftSide => 2,
                PPTNavBar.NavDirection.RightSide => 3,
                _ => 2
            };
        }

        private bool IsSideButton(PPTNavBar.NavDirection dir)
        {
            return dir == PPTNavBar.NavDirection.LeftSide || dir == PPTNavBar.NavDirection.RightSide;
        }

        private void PreviewLS_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            SelectPosition(PPTNavBar.NavDirection.LeftSide);
        }

        private void PreviewRS_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            SelectPosition(PPTNavBar.NavDirection.RightSide);
        }

        private void PreviewLB_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            SelectPosition(PPTNavBar.NavDirection.LeftBottom);
        }

        private void PreviewRB_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            SelectPosition(PPTNavBar.NavDirection.RightBottom);
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = Window.GetWindow(this) as SettingsViews.SettingsWindow;
            settingsWindow?.NavigateToPage("PowerPointPage");
        }

        #endregion

        #region Position & Opacity Sliders

        private void OffsetSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(OffsetSlider, OffsetText, "{0:F0}");
            if (!_isLoaded) return;
            var ppt = SettingsManager.Settings.PowerPointSettings;
            int val = (int)OffsetSlider.Value;
            switch (_selectedDirection)
            {
                case PPTNavBar.NavDirection.LeftSide: ppt.PPTLSButtonPosition = val; break;
                case PPTNavBar.NavDirection.RightSide: ppt.PPTRSButtonPosition = val; break;
                case PPTNavBar.NavDirection.LeftBottom: ppt.PPTLBButtonPosition = val; break;
                case PPTNavBar.NavDirection.RightBottom: ppt.PPTRBButtonPosition = val; break;
            }
            SettingsActionHub.OnPPTButtonPositionChanged();
            _sliderDelayAction.DebounceAction(2000, null, () => SettingsManager.SaveSettingsToFile());
            UpdatePreview();
        }

        private void OpacitySlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(OpacitySlider, OpacityText, "{0:P0}");
            if (!_isLoaded) return;
            double roundedValue = Math.Round(OpacitySlider.Value, 1);
            OpacitySlider.ValueChanged -= OpacitySlider_ValueChanged;
            OpacitySlider.Value = roundedValue;
            OpacitySlider.ValueChanged += OpacitySlider_ValueChanged;

            var ppt = SettingsManager.Settings.PowerPointSettings;
            switch (_selectedDirection)
            {
                case PPTNavBar.NavDirection.LeftSide: ppt.PPTLSButtonOpacity = roundedValue; break;
                case PPTNavBar.NavDirection.RightSide: ppt.PPTRSButtonOpacity = roundedValue; break;
                case PPTNavBar.NavDirection.LeftBottom: ppt.PPTLBButtonOpacity = roundedValue; break;
                case PPTNavBar.NavDirection.RightBottom: ppt.PPTRBButtonOpacity = roundedValue; break;
            }
            SettingsManager.SaveSettingsToFile();
            string key = _selectedDirection switch
            {
                PPTNavBar.NavDirection.LeftSide => "LS",
                PPTNavBar.NavDirection.RightSide => "RS",
                PPTNavBar.NavDirection.LeftBottom => "LB",
                PPTNavBar.NavDirection.RightBottom => "RB",
                _ => "LS"
            };
            SettingsActionHub.OnPPTButtonOpacityChanged(key, roundedValue);
            UpdatePreview();
        }

        private void ButtonResetOffset_Click(object sender, RoutedEventArgs e)
        {
            OffsetSlider.Value = 0;
        }

        private void ButtonResetOpacity_Click(object sender, RoutedEventArgs e)
        {
            OpacitySlider.Value = 0.5;
        }

        #endregion

        #region Toggle Switches & Checkboxes

        private void ToggleSwitchShowPPTButton_OnToggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.PowerPointSettings.ShowPPTButton = CardShowPPTButton.IsOn;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnShowPPTButtonChanged(CardShowPPTButton.IsOn);
            UpdatePreview();
        }

        private void ToggleSwitchEnablePositionButton_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var ppt = SettingsManager.Settings.PowerPointSettings;
            var str = ppt.PPTButtonsDisplayOption.ToString();
            char[] c = str.ToCharArray();
            int idx = GetDisplayOptionIndex(_selectedDirection);
            if (idx < c.Length)
            {
                c[idx] = CardEnablePositionButton.IsOn ? '2' : '1';
                ppt.PPTButtonsDisplayOption = int.Parse(new string(c));
                SettingsManager.SaveSettingsToFile();
                SettingsActionHub.OnPPTButtonsDisplayOptionChanged();
                UpdatePositionCheckboxesFromSettings();
                UpdatePreview();
            }
        }

        private void CheckboxPosition_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var checkbox = sender as CheckBox;
            if (checkbox?.Tag == null) return;

            PPTNavBar.NavDirection dir;
            if (!Enum.TryParse<PPTNavBar.NavDirection>(checkbox.Tag.ToString(), out dir)) return;

            var ppt = SettingsManager.Settings.PowerPointSettings;
            var str = ppt.PPTButtonsDisplayOption.ToString();
            char[] c = str.ToCharArray();
            int idx = GetDisplayOptionIndex(dir);
            if (idx < c.Length)
            {
                c[idx] = checkbox.IsChecked == true ? '2' : '1';
                ppt.PPTButtonsDisplayOption = int.Parse(new string(c));
                SettingsManager.SaveSettingsToFile();
                SettingsActionHub.OnPPTButtonsDisplayOptionChanged();

                if (_selectedDirection == dir)
                {
                    _isLoaded = false;
                    CardEnablePositionButton.IsOn = checkbox.IsChecked == true;
                    _isLoaded = true;
                }
                UpdatePreview();
            }
        }

        private void CheckboxShowPageNumber_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var ppt = SettingsManager.Settings.PowerPointSettings;
            if (IsSideButton(_selectedDirection))
            {
                var str = ppt.PPTSButtonsOption.ToString();
                char[] c = str.ToCharArray();
                c[0] = CheckboxShowPageNumber.IsChecked == true ? '2' : '1';
                ppt.PPTSButtonsOption = int.Parse(new string(c));
                SettingsManager.SaveSettingsToFile();
                SettingsActionHub.OnPPTSButtonsOptionChanged();
            }
            else
            {
                var str = ppt.PPTBButtonsOption.ToString();
                char[] c = str.ToCharArray();
                c[0] = CheckboxShowPageNumber.IsChecked == true ? '2' : '1';
                ppt.PPTBButtonsOption = int.Parse(new string(c));
                SettingsManager.SaveSettingsToFile();
                SettingsActionHub.OnPPTBButtonsOptionChanged();
            }
            UpdatePreview();
        }

        private void CheckboxBlackBackground_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var ppt = SettingsManager.Settings.PowerPointSettings;
            if (IsSideButton(_selectedDirection))
            {
                var str = ppt.PPTSButtonsOption.ToString();
                char[] c = str.ToCharArray();
                c[2] = CheckboxBlackBackground.IsChecked == true ? '2' : '1';
                ppt.PPTSButtonsOption = int.Parse(new string(c));
                SettingsManager.SaveSettingsToFile();
                SettingsActionHub.OnPPTSButtonsOptionChanged();
            }
            else
            {
                var str = ppt.PPTBButtonsOption.ToString();
                char[] c = str.ToCharArray();
                c[2] = CheckboxBlackBackground.IsChecked == true ? '2' : '1';
                ppt.PPTBButtonsOption = int.Parse(new string(c));
                SettingsManager.SaveSettingsToFile();
                SettingsActionHub.OnPPTBButtonsOptionChanged();
            }
            UpdatePreview();
        }

        private void PPTNavBarScaleValueSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(PPTNavBarScaleValueSlider, PPTNavBarScaleText, "{0:F2}");
            if (!_isLoaded) return;
            double roundedValue = Math.Round(PPTNavBarScaleValueSlider.Value, 2);
            PPTNavBarScaleValueSlider.ValueChanged -= PPTNavBarScaleValueSlider_ValueChanged;
            PPTNavBarScaleValueSlider.Value = roundedValue;
            PPTNavBarScaleValueSlider.ValueChanged += PPTNavBarScaleValueSlider_ValueChanged;
            SettingsManager.Settings.PowerPointSettings.PPTNavBarScale = roundedValue;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnPPTNavBarScaleChanged(roundedValue);
            UpdatePreview();
        }

        private void ToggleSwitchEnablePPTButtonPageClickable_OnToggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.PowerPointSettings.EnablePPTButtonPageClickable = CardEnablePPTButtonPageClickable.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchEnablePPTButtonEnhancedPreview_OnToggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.PowerPointSettings.EnablePPTButtonEnhancedPreview = CardEnablePPTButtonEnhancedPreview.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchEnablePPTButtonLongPressPageTurn_OnToggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.PowerPointSettings.EnablePPTButtonLongPressPageTurn = CardEnablePPTButtonLongPressPageTurn.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        #endregion
    }
}
