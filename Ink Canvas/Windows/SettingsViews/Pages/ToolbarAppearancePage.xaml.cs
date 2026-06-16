using Ink_Canvas.Helpers;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class ToolbarAppearancePage : Page
    {
        private bool _isLoaded = false;

        public ToolbarAppearancePage()
        {
            InitializeComponent();
            Loaded += Page_Loaded;
            Unloaded += Page_Unloaded;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            var mw = Application.Current.MainWindow as MainWindow;
            mw?.UpdateCustomIconsInComboBox();
            LoadSettings();
            _isLoaded = true;
            UpdateAllSliderTexts();
            SliderTouchHelper.AddTouchSupportToAllSliders(this);
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = false;
        }

        private void LoadSettings()
        {
            var settings = SettingsManager.Settings;
            if (settings?.Appearance == null) return;

            if (settings.Appearance.FloatingBarImg >= ComboBoxFloatingBarImg.Items.Count)
                settings.Appearance.FloatingBarImg = 0;
            ComboBoxFloatingBarImg.SelectedIndex = settings.Appearance.FloatingBarImg;

            if (settings.Appearance.ViewboxFloatingBarScaleTransformValue != 0)
                ViewboxFloatingBarScaleTransformValueSlider.Value = settings.Appearance.ViewboxFloatingBarScaleTransformValue;

            ViewboxFloatingBarOpacityValueSlider.Value = settings.Appearance.ViewboxFloatingBarOpacityValue;
            ViewboxFloatingBarOpacityInPPTValueSlider.Value = settings.Appearance.ViewboxFloatingBarOpacityInPPTValue;
            FloatingBarMenuOpacitySlider.Value = settings.Appearance.FloatingBarMenuOpacity;
            FloatingBarMenuOpacityInPPTSlider.Value = settings.Appearance.FloatingBarMenuOpacityInPPT;

            // 加载工具栏位置
            string positionTag = settings.Appearance.ToolbarPosition.ToString();
            foreach (ComboBoxItem item in ToolbarPositionComboBox.Items)
            {
                if ((string)item.Tag == positionTag)
                {
                    ToolbarPositionComboBox.SelectedItem = item;
                    break;
                }
            }

            // 加载翻转内容设置
            if (CardReverseToolbarContent != null)
            {
                CardReverseToolbarContent.IsOn = settings.Appearance.ReverseToolbarContent;
            }

            // 加载自动翻转设置
            if (ToggleSwitchAutoFlipWhenSpaceInsufficient != null)
            {
                ToggleSwitchAutoFlipWhenSpaceInsufficient.IsOn = settings.Appearance.AutoFlipWhenSpaceInsufficient;
            }

            // 加载自动翻转后翻转组件内容设置
            if (ToggleSwitchFlipContentOnAutoFlip != null)
            {
                ToggleSwitchFlipContentOnAutoFlip.IsOn = settings.Appearance.FlipContentOnAutoFlip;
            }

            // 加载禁止工具栏动画设置
            if (CardDisableToolbarAnimation != null)
            {
                CardDisableToolbarAnimation.IsOn = settings.Appearance.DisableToolbarAnimation;
            }
        }

        private void UpdateAllSliderTexts()
        {
            UpdateSliderText(ViewboxFloatingBarScaleTransformValueSlider, ViewboxFloatingBarScaleSliderText, "{0:F2}x");
            UpdateSliderText(ViewboxFloatingBarOpacityValueSlider, ViewboxFloatingBarOpacityText, "{0:F2}");
            UpdateSliderText(ViewboxFloatingBarOpacityInPPTValueSlider, ViewboxFloatingBarOpacityInPPTText, "{0:F2}");
            UpdateSliderText(FloatingBarMenuOpacitySlider, FloatingBarMenuOpacityText, "{0:F2}");
            UpdateSliderText(FloatingBarMenuOpacityInPPTSlider, FloatingBarMenuOpacityInPPTText, "{0:F2}");
            UpdateFloatingBarActualScaleText();
        }

        private void UpdateFloatingBarActualScaleText()
        {
            if (ViewboxFloatingBarScaleTransformValueSlider == null || ViewboxFloatingBarActualScaleText == null) return;
            double val = ViewboxFloatingBarScaleTransformValueSlider.Value;
            double clampedVal = (val > 0.5 && val < 1.25) ? val : val <= 0.5 ? 0.5 : val >= 1.25 ? 1.25 : 1.0;
            double actualScale = clampedVal;
            ViewboxFloatingBarActualScaleText.Text = $"{actualScale:F2}x";
        }

        private void UpdateSliderText(Slider slider, TextBlock textBlock, string format)
        {
            if (slider == null || textBlock == null) return;
            textBlock.Text = string.Format(format, slider.Value);
        }

        private void ViewboxFloatingBarScaleTransformValueSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(ViewboxFloatingBarScaleTransformValueSlider, ViewboxFloatingBarScaleSliderText, "{0:F2}x");
            if (!_isLoaded) return;
            var slider = ViewboxFloatingBarScaleTransformValueSlider;
            var val = Math.Round(slider.Value, 2);
            if (slider.Value != val)
            {
                slider.Value = val;
                return;
            }
            SettingsManager.Settings.Appearance.ViewboxFloatingBarScaleTransformValue = val;
            SettingsManager.SaveSettingsToFile();

            double clampedVal = (val > 0.5 && val < 1.25) ? val : val <= 0.5 ? 0.5 : val >= 1.25 ? 1.25 : 1.0;
            double actualScale = clampedVal;
            UpdateFloatingBarActualScaleText();

            SettingsActionHub.OnFloatingBarScaleChanged(actualScale);
        }

        private void ViewboxFloatingBarOpacityValueSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(ViewboxFloatingBarOpacityValueSlider, ViewboxFloatingBarOpacityText, "{0:F2}");
            if (!_isLoaded) return;
            var slider = ViewboxFloatingBarOpacityValueSlider;
            var val = Math.Round(slider.Value, 2);
            if (slider.Value != val)
            {
                slider.Value = val;
                return;
            }
            SettingsManager.Settings.Appearance.ViewboxFloatingBarOpacityValue = val;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnFloatingBarOpacityChanged(val);
        }

        private void ViewboxFloatingBarOpacityInPPTValueSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(ViewboxFloatingBarOpacityInPPTValueSlider, ViewboxFloatingBarOpacityInPPTText, "{0:F2}");
            if (!_isLoaded) return;
            var slider = ViewboxFloatingBarOpacityInPPTValueSlider;
            var val = Math.Round(slider.Value, 2);
            if (slider.Value != val)
            {
                slider.Value = val;
                return;
            }
            SettingsManager.Settings.Appearance.ViewboxFloatingBarOpacityInPPTValue = val;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnFloatingBarOpacityInPPTChanged(val);
        }

        private void ToolbarPositionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            if (ToolbarPositionComboBox.SelectedItem is not ComboBoxItem selectedItem) return;

            if (Enum.TryParse<ToolbarPosition>((string)selectedItem.Tag, out var position))
            {
                SettingsManager.Settings.Appearance.ToolbarPosition = position;
                SettingsManager.SaveSettingsToFile();
                SettingsActionHub.OnToolbarPositionChanged(position);
            }
        }

        private void ReverseToolbarContentToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            if (CardReverseToolbarContent == null) return;

            SettingsManager.Settings.Appearance.ReverseToolbarContent = CardReverseToolbarContent.IsOn;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnReverseToolbarContentChanged(CardReverseToolbarContent.IsOn);
        }

        private void AutoFlipWhenSpaceInsufficientToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            if (ToggleSwitchAutoFlipWhenSpaceInsufficient == null) return;

            SettingsManager.Settings.Appearance.AutoFlipWhenSpaceInsufficient = ToggleSwitchAutoFlipWhenSpaceInsufficient.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void FlipContentOnAutoFlipToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            if (ToggleSwitchFlipContentOnAutoFlip == null) return;

            SettingsManager.Settings.Appearance.FlipContentOnAutoFlip = ToggleSwitchFlipContentOnAutoFlip.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void DisableToolbarAnimationToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            if (CardDisableToolbarAnimation == null) return;

            SettingsManager.Settings.Appearance.DisableToolbarAnimation = CardDisableToolbarAnimation.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void FloatingBarMenuOpacitySlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(FloatingBarMenuOpacitySlider, FloatingBarMenuOpacityText, "{0:F2}");
            if (!_isLoaded) return;
            var slider = FloatingBarMenuOpacitySlider;
            var val = Math.Round(slider.Value, 2);
            if (slider.Value != val)
            {
                slider.Value = val;
                return;
            }
            SettingsManager.Settings.Appearance.FloatingBarMenuOpacity = val;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnFloatingBarMenuOpacityChanged(val);
        }

        private void FloatingBarMenuOpacityInPPTSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(FloatingBarMenuOpacityInPPTSlider, FloatingBarMenuOpacityInPPTText, "{0:F2}");
            if (!_isLoaded) return;
            var slider = FloatingBarMenuOpacityInPPTSlider;
            var val = Math.Round(slider.Value, 2);
            if (slider.Value != val)
            {
                slider.Value = val;
                return;
            }
            SettingsManager.Settings.Appearance.FloatingBarMenuOpacityInPPT = val;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnFloatingBarMenuOpacityInPPTChanged(val);
        }

        #region Floating Bar Icon

        private void ComboBoxFloatingBarImg_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Appearance.FloatingBarImg = ComboBoxFloatingBarImg.SelectedIndex;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnFloatingBarImgChanged();
        }

        private void ButtonAddCustomIcon_Click(object sender, RoutedEventArgs e)
        {
            var mw = Application.Current.MainWindow as MainWindow;
            if (mw == null) return;
            AddCustomIconWindow dialog = new AddCustomIconWindow(mw);
            dialog.Owner = mw;
            dialog.ShowDialog();
            if (dialog.IsSuccess)
            {
                ComboBoxFloatingBarImg.SelectedIndex = ComboBoxFloatingBarImg.Items.Count - 1;
            }
        }

        private void ButtonManageCustomIcons_Click(object sender, RoutedEventArgs e)
        {
            var mw = Application.Current.MainWindow as MainWindow;
            if (mw == null) return;
            CustomIconWindow dialog = new CustomIconWindow(mw);
            dialog.Owner = mw;
            dialog.ShowDialog();
        }

        #endregion
    }
}
