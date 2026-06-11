using Ink_Canvas.Helpers;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class PowerPointPage : Page
    {
        private bool _isLoaded = false;
        private DelayAction _sliderDelayAction = new DelayAction();

        public PowerPointPage()
        {
            InitializeComponent();
            Loaded += PowerPointPage_Loaded;
            Unloaded += PowerPointPage_Unloaded;
        }

        private void PowerPointPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
            _isLoaded = true;
            UpdateAllSliderTexts();
            SliderTouchHelper.AddTouchSupportToAllSliders(this);
        }

        private void UpdateAllSliderTexts()
        {
            UpdateSliderText(PPTButtonLeftPositionValueSlider, PPTButtonLeftPositionText, "{0:F0}");
            UpdateSliderText(PPTButtonRightPositionValueSlider, PPTButtonRightPositionText, "{0:F0}");
            UpdateSliderText(PPTButtonLBPositionValueSlider, PPTButtonLBPositionText, "{0:F0}");
            UpdateSliderText(PPTButtonRBPositionValueSlider, PPTButtonRBPositionText, "{0:F0}");
            UpdateSliderText(PPTLSButtonOpacityValueSlider, PPTLSButtonOpacityText, "{0:P0}");
            UpdateSliderText(PPTRSButtonOpacityValueSlider, PPTRSButtonOpacityText, "{0:P0}");
            UpdateSliderText(PPTLBButtonOpacityValueSlider, PPTLBButtonOpacityText, "{0:P0}");
            UpdateSliderText(PPTRBButtonOpacityValueSlider, PPTRBButtonOpacityText, "{0:P0}");
            UpdateSliderText(PPTNavBarScaleValueSlider, PPTNavBarScaleText, "{0:F2}");
        }

        private void UpdateSliderText(Slider slider, TextBlock textBlock, string format)
        {
            if (slider == null || textBlock == null) return;
            textBlock.Text = string.Format(format, slider.Value);
        }

        private void PowerPointPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = false;
        }

        private void UpdatePreview()
        {
            if (PPTBtnPreviewLS == null) return;

            bool showPPTButton = CardShowPPTButton.IsOn;
            double navBarScale = PPTNavBarScaleValueSlider?.Value ?? 1.0;

            PPTBtnPreviewLS.SetBarScale(navBarScale);
            PPTBtnPreviewRS.SetBarScale(navBarScale);
            PPTBtnPreviewLB.SetBarScale(navBarScale);
            PPTBtnPreviewRB.SetBarScale(navBarScale);

            PPTBtnPreviewLS.Visibility = showPPTButton && CheckboxEnableLSPPTButton.IsOn ? Visibility.Visible : Visibility.Collapsed;
            PPTBtnPreviewLS.Opacity = PPTLSButtonOpacityValueSlider.Value;
            ((TranslateTransform)PPTBtnPreviewLS.RenderTransform).X = PPTButtonLeftPositionValueSlider.Value / 15;

            PPTBtnPreviewRS.Visibility = showPPTButton && CheckboxEnableRSPPTButton.IsOn ? Visibility.Visible : Visibility.Collapsed;
            PPTBtnPreviewRS.Opacity = PPTRSButtonOpacityValueSlider.Value;
            ((TranslateTransform)PPTBtnPreviewRS.RenderTransform).X = -PPTButtonRightPositionValueSlider.Value / 15;

            PPTBtnPreviewLB.Visibility = showPPTButton && CheckboxEnableLBPPTButton.IsOn ? Visibility.Visible : Visibility.Collapsed;
            PPTBtnPreviewLB.Opacity = PPTLBButtonOpacityValueSlider.Value;
            ((TranslateTransform)PPTBtnPreviewLB.RenderTransform).X = PPTButtonLBPositionValueSlider.Value / 15;

            PPTBtnPreviewRB.Visibility = showPPTButton && CheckboxEnableRBPPTButton.IsOn ? Visibility.Visible : Visibility.Collapsed;
            PPTBtnPreviewRB.Opacity = PPTRBButtonOpacityValueSlider.Value;
            ((TranslateTransform)PPTBtnPreviewRB.RenderTransform).X = -PPTButtonRBPositionValueSlider.Value / 15;

            if (PPTTimeCapsulePreviewContainer == null || CardEnablePPTTimeCapsule == null) return;

            bool showTimeCapsule = CardEnablePPTTimeCapsule.IsOn;
            PPTTimeCapsulePreviewContainer.Visibility = showTimeCapsule ? Visibility.Visible : Visibility.Collapsed;
            if (!showTimeCapsule) return;

            int position = ComboBoxPPTTimeCapsulePosition?.SelectedIndex ?? 1;
            if (position < 0 || position > 2) position = 1;

            switch (position)
            {
                case 0:
                    PPTTimeCapsulePreviewContainer.HorizontalAlignment = HorizontalAlignment.Left;
                    PPTTimeCapsulePreviewContainer.VerticalAlignment = VerticalAlignment.Top;
                    PPTTimeCapsulePreviewContainer.Margin = new Thickness(10, 10, 0, 0);
                    PPTTimeCapsulePreviewContainer.RenderTransformOrigin = new Point(0, 0);
                    break;
                case 1:
                    PPTTimeCapsulePreviewContainer.HorizontalAlignment = HorizontalAlignment.Right;
                    PPTTimeCapsulePreviewContainer.VerticalAlignment = VerticalAlignment.Top;
                    PPTTimeCapsulePreviewContainer.Margin = new Thickness(0, 10, 10, 0);
                    PPTTimeCapsulePreviewContainer.RenderTransformOrigin = new Point(1, 0);
                    break;
                default:
                    PPTTimeCapsulePreviewContainer.HorizontalAlignment = HorizontalAlignment.Center;
                    PPTTimeCapsulePreviewContainer.VerticalAlignment = VerticalAlignment.Top;
                    PPTTimeCapsulePreviewContainer.Margin = new Thickness(0, 10, 0, 0);
                    PPTTimeCapsulePreviewContainer.RenderTransformOrigin = new Point(0.5, 0);
                    break;
            }

            if (SliderPPTTimeCapsuleOpacity != null)
                PPTTimeCapsulePreviewContainer.Opacity = SliderPPTTimeCapsuleOpacity.Value;

            if (PPTTimeCapsulePreviewScaleTransform != null && SliderPPTTimeCapsuleScale != null)
            {
                // Preview canvas is much smaller than slideshow, so apply a baseline downscale.
                var scale = SliderPPTTimeCapsuleScale.Value * 0.62;
                PPTTimeCapsulePreviewScaleTransform.ScaleX = scale;
                PPTTimeCapsulePreviewScaleTransform.ScaleY = scale;
            }
        }

        private void LoadSettings()
        {
            _isLoaded = false;
            var ppt = SettingsManager.Settings.PowerPointSettings;

            CardSupportPowerPoint.IsOn = ppt.PowerPointSupport;
            CardPowerPointEnhancement.IsOn = ppt.EnablePowerPointEnhancement;
            CardSkipAnimationsWhenGoNext.IsOn = ppt.SkipAnimationsWhenGoNext;
            CardUseRotPptLink.IsOn = ppt.UseRotPptLink;
            CardSupportWPS.IsOn = ppt.IsSupportWPS;
            CardEnableWppProcessKill.IsOn = ppt.EnableWppProcessKill;

            CardShowPPTButton.IsOn = ppt.ShowPPTButton;
            var displayOpt = ppt.PPTButtonsDisplayOption.ToString();
            CheckboxEnableLBPPTButton.IsOn = displayOpt.Length > 0 && displayOpt[0] == '2';
            CheckboxEnableRBPPTButton.IsOn = displayOpt.Length > 1 && displayOpt[1] == '2';
            CheckboxEnableLSPPTButton.IsOn = displayOpt.Length > 2 && displayOpt[2] == '2';
            CheckboxEnableRSPPTButton.IsOn = displayOpt.Length > 3 && displayOpt[3] == '2';

            PPTButtonLeftPositionValueSlider.Value = ppt.PPTLSButtonPosition;
            PPTButtonRightPositionValueSlider.Value = ppt.PPTRSButtonPosition;
            PPTButtonLBPositionValueSlider.Value = ppt.PPTLBButtonPosition;
            PPTButtonRBPositionValueSlider.Value = ppt.PPTRBButtonPosition;

            PPTLSButtonOpacityValueSlider.Value = ppt.PPTLSButtonOpacity;
            PPTRSButtonOpacityValueSlider.Value = ppt.PPTRSButtonOpacity;
            PPTLBButtonOpacityValueSlider.Value = ppt.PPTLBButtonOpacity;
            PPTRBButtonOpacityValueSlider.Value = ppt.PPTRBButtonOpacity;

            PPTNavBarScaleValueSlider.Value = ppt.PPTNavBarScale;

            var sOpt = ppt.PPTSButtonsOption.ToString();
            CheckboxSPPTDisplayPage.IsChecked = sOpt.Length > 0 && sOpt[0] == '2';
            CheckboxSPPTHalfOpacity.IsChecked = sOpt.Length > 1 && sOpt[1] == '2';
            CheckboxSPPTBlackBackground.IsChecked = sOpt.Length > 2 && sOpt[2] == '2';

            var bOpt = ppt.PPTBButtonsOption.ToString();
            CheckboxBPPTDisplayPage.IsChecked = bOpt.Length > 0 && bOpt[0] == '2';
            CheckboxBPPTHalfOpacity.IsChecked = bOpt.Length > 1 && bOpt[1] == '2';
            CheckboxBPPTBlackBackground.IsChecked = bOpt.Length > 2 && bOpt[2] == '2';

            CardEnablePPTButtonPageClickable.IsOn = ppt.EnablePPTButtonPageClickable;
            CardEnablePPTButtonEnhancedPreview.IsOn = ppt.EnablePPTButtonEnhancedPreview;
            CardEnablePPTButtonLongPressPageTurn.IsOn = ppt.EnablePPTButtonLongPressPageTurn;

            CardShowCanvasAtNewSlideShow.IsOn = ppt.IsShowCanvasAtNewSlideShow;

            CardEnableTwoFingerGestureInPresentationMode.IsOn = ppt.IsEnableTwoFingerGestureInPresentationMode;
            CardEnableFingerGestureSlideShowControl.IsOn = ppt.IsEnableFingerGestureSlideShowControl;
            CardEnablePPTTimeCapsule.IsOn = ppt.EnablePPTTimeCapsule;
            ComboBoxPPTTimeCapsulePosition.SelectedIndex = ppt.PPTTimeCapsulePosition;
            SliderPPTTimeCapsuleOpacity.Value = ppt.PPTTimeCapsuleOpacity;
            SliderPPTTimeCapsuleScale.Value = ppt.PPTTimeCapsuleScale;
            CardShowPPTSidebarByDefault.IsOn = ppt.ShowPPTSidebarByDefault;
            CardShowPPTModePrompt.IsOn = ppt.ShowPPTModePrompt;

            CardAutoSaveScreenShotInPowerPoint.IsOn = ppt.IsAutoSaveScreenShotInPowerPoint;
            CardAutoSaveStrokesInPowerPoint.IsOn = ppt.IsAutoSaveStrokesInPowerPoint;

            CardNotifyPreviousPage.IsOn = ppt.IsNotifyPreviousPage;
            CardAlwaysGoToFirstPageOnReenter.IsOn = ppt.IsAlwaysGoToFirstPageOnReenter;
            CardNotifyHiddenPage.IsOn = ppt.IsNotifyHiddenPage;
            CardNotifyAutoPlayPresentation.IsOn = ppt.IsNotifyAutoPlayPresentation;

            UpdatePreview();
            _isLoaded = true;
        }

        #region PPT Basic

        private void ToggleSwitchSupportPowerPoint_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var ppt = SettingsManager.Settings.PowerPointSettings;
            ppt.PowerPointSupport = CardSupportPowerPoint.IsOn;
            if (!ppt.PowerPointSupport && ppt.IsSupportWPS)
            {
                ppt.IsSupportWPS = false;
                CardSupportWPS.IsOn = false;
            }
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnPPTSupportChanged(CardSupportPowerPoint.IsOn);
        }

        private void ToggleSwitchPowerPointEnhancement_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var ppt = SettingsManager.Settings.PowerPointSettings;
            ppt.EnablePowerPointEnhancement = CardPowerPointEnhancement.IsOn;
            if (ppt.EnablePowerPointEnhancement)
            {
                ppt.IsSupportWPS = false;
                CardSupportWPS.IsOn = false;
            }
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnPPTEnhancementChanged(CardPowerPointEnhancement.IsOn);
        }

        private void ToggleSwitchSkipAnimationsWhenGoNext_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.PowerPointSettings.SkipAnimationsWhenGoNext = CardSkipAnimationsWhenGoNext.IsOn;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnSkipAnimationsWhenGoNextChanged(CardSkipAnimationsWhenGoNext.IsOn);
        }

        private void ToggleSwitchUseRotPptLink_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var ppt = SettingsManager.Settings.PowerPointSettings;
            ppt.UseRotPptLink = CardUseRotPptLink.IsOn;
            SettingsManager.SaveSettingsToFile();
            try
            {
                SettingsActionHub.OnUseRotPptLinkChanged();
            }
            catch (Exception ex) { LogHelper.WriteLogToFile($"切换 PPT 联动架构失败: {ex}", LogHelper.LogType.Error); }
        }

        private void ToggleSwitchSupportWPS_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var ppt = SettingsManager.Settings.PowerPointSettings;
            ppt.IsSupportWPS = CardSupportWPS.IsOn;
            if (ppt.IsSupportWPS)
            {
                if (!ppt.PowerPointSupport)
                {
                    ppt.PowerPointSupport = true;
                    CardSupportPowerPoint.IsOn = true;
                }
                if (ppt.EnablePowerPointEnhancement)
                {
                    ppt.EnablePowerPointEnhancement = false;
                    CardPowerPointEnhancement.IsOn = false;
                }
            }
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnSupportWPSChanged();
        }

        private void ToggleSwitchEnableWppProcessKill_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.PowerPointSettings.EnableWppProcessKill = CardEnableWppProcessKill.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        #endregion

        #region PPT Flip Buttons

        private void ToggleSwitchShowPPTButton_OnToggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.PowerPointSettings.ShowPPTButton = CardShowPPTButton.IsOn;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnShowPPTButtonChanged(CardShowPPTButton.IsOn);
            UpdatePreview();
        }

        private void ToggleSwitchShowPPTSidebarByDefault_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.PowerPointSettings.ShowPPTSidebarByDefault = CardShowPPTSidebarByDefault.IsOn;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnShowPPTSidebarByDefaultChanged();
        }

        private void ToggleSwitchShowPPTModePrompt_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.PowerPointSettings.ShowPPTModePrompt = CardShowPPTModePrompt.IsOn;
            SettingsManager.SaveSettingsToFile();
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

        #region PPT Button Position & Opacity Sliders

        private void PPTButtonLeftPositionValueSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(PPTButtonLeftPositionValueSlider, PPTButtonLeftPositionText, "{0:F0}");
            if (!_isLoaded) return;
            SettingsManager.Settings.PowerPointSettings.PPTLSButtonPosition = (int)PPTButtonLeftPositionValueSlider.Value;
            SettingsActionHub.OnPPTButtonPositionChanged();
            UpdatePreview();
            _sliderDelayAction.DebounceAction(2000, null, () => SettingsManager.SaveSettingsToFile());
        }

        private void PPTButtonRightPositionValueSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(PPTButtonRightPositionValueSlider, PPTButtonRightPositionText, "{0:F0}");
            if (!_isLoaded) return;
            SettingsManager.Settings.PowerPointSettings.PPTRSButtonPosition = (int)PPTButtonRightPositionValueSlider.Value;
            SettingsActionHub.OnPPTButtonPositionChanged();
            UpdatePreview();
            _sliderDelayAction.DebounceAction(2000, null, () => SettingsManager.SaveSettingsToFile());
        }

        private void PPTButtonLBPositionValueSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(PPTButtonLBPositionValueSlider, PPTButtonLBPositionText, "{0:F0}");
            if (!_isLoaded) return;
            SettingsManager.Settings.PowerPointSettings.PPTLBButtonPosition = (int)PPTButtonLBPositionValueSlider.Value;
            SettingsActionHub.OnPPTButtonPositionChanged();
            UpdatePreview();
            _sliderDelayAction.DebounceAction(2000, null, () => SettingsManager.SaveSettingsToFile());
        }

        private void PPTButtonRBPositionValueSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(PPTButtonRBPositionValueSlider, PPTButtonRBPositionText, "{0:F0}");
            if (!_isLoaded) return;
            SettingsManager.Settings.PowerPointSettings.PPTRBButtonPosition = (int)PPTButtonRBPositionValueSlider.Value;
            SettingsActionHub.OnPPTButtonPositionChanged();
            UpdatePreview();
            _sliderDelayAction.DebounceAction(2000, null, () => SettingsManager.SaveSettingsToFile());
        }

        private void PPTLSButtonOpacityValueSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(PPTLSButtonOpacityValueSlider, PPTLSButtonOpacityText, "{0:P0}");
            if (!_isLoaded) return;
            double roundedValue = Math.Round(PPTLSButtonOpacityValueSlider.Value, 1);
            PPTLSButtonOpacityValueSlider.ValueChanged -= PPTLSButtonOpacityValueSlider_ValueChanged;
            PPTLSButtonOpacityValueSlider.Value = roundedValue;
            PPTLSButtonOpacityValueSlider.ValueChanged += PPTLSButtonOpacityValueSlider_ValueChanged;
            SettingsManager.Settings.PowerPointSettings.PPTLSButtonOpacity = roundedValue;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnPPTButtonOpacityChanged("LS", roundedValue);
            UpdatePreview();
        }

        private void PPTRSButtonOpacityValueSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(PPTRSButtonOpacityValueSlider, PPTRSButtonOpacityText, "{0:P0}");
            if (!_isLoaded) return;
            double roundedValue = Math.Round(PPTRSButtonOpacityValueSlider.Value, 1);
            PPTRSButtonOpacityValueSlider.ValueChanged -= PPTRSButtonOpacityValueSlider_ValueChanged;
            PPTRSButtonOpacityValueSlider.Value = roundedValue;
            PPTRSButtonOpacityValueSlider.ValueChanged += PPTRSButtonOpacityValueSlider_ValueChanged;
            SettingsManager.Settings.PowerPointSettings.PPTRSButtonOpacity = roundedValue;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnPPTButtonOpacityChanged("RS", roundedValue);
            UpdatePreview();
        }

        private void PPTLBButtonOpacityValueSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(PPTLBButtonOpacityValueSlider, PPTLBButtonOpacityText, "{0:P0}");
            if (!_isLoaded) return;
            double roundedValue = Math.Round(PPTLBButtonOpacityValueSlider.Value, 1);
            PPTLBButtonOpacityValueSlider.ValueChanged -= PPTLBButtonOpacityValueSlider_ValueChanged;
            PPTLBButtonOpacityValueSlider.Value = roundedValue;
            PPTLBButtonOpacityValueSlider.ValueChanged += PPTLBButtonOpacityValueSlider_ValueChanged;
            SettingsManager.Settings.PowerPointSettings.PPTLBButtonOpacity = roundedValue;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnPPTButtonOpacityChanged("LB", roundedValue);
            UpdatePreview();
        }

        private void PPTRBButtonOpacityValueSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(PPTRBButtonOpacityValueSlider, PPTRBButtonOpacityText, "{0:P0}");
            if (!_isLoaded) return;
            double roundedValue = Math.Round(PPTRBButtonOpacityValueSlider.Value, 1);
            PPTRBButtonOpacityValueSlider.ValueChanged -= PPTRBButtonOpacityValueSlider_ValueChanged;
            PPTRBButtonOpacityValueSlider.Value = roundedValue;
            PPTRBButtonOpacityValueSlider.ValueChanged += PPTRBButtonOpacityValueSlider_ValueChanged;
            SettingsManager.Settings.PowerPointSettings.PPTRBButtonOpacity = roundedValue;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnPPTButtonOpacityChanged("RB", roundedValue);
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

        private void ResetLeftOffset_Click(object sender, RoutedEventArgs e)
        {
            PPTButtonLeftPositionValueSlider.Value = 0;
        }

        private void ResetLeftOpacity_Click(object sender, RoutedEventArgs e)
        {
            PPTLSButtonOpacityValueSlider.Value = 0.5;
        }

        private void ResetRightOffset_Click(object sender, RoutedEventArgs e)
        {
            PPTButtonRightPositionValueSlider.Value = 0;
        }

        private void ResetRightOpacity_Click(object sender, RoutedEventArgs e)
        {
            PPTRSButtonOpacityValueSlider.Value = 0.5;
        }

        private void ResetLeftBottomOffset_Click(object sender, RoutedEventArgs e)
        {
            PPTButtonLBPositionValueSlider.Value = 0;
        }

        private void ResetLeftBottomOpacity_Click(object sender, RoutedEventArgs e)
        {
            PPTLBButtonOpacityValueSlider.Value = 0.5;
        }

        private void ResetRightBottomOffset_Click(object sender, RoutedEventArgs e)
        {
            PPTButtonRBPositionValueSlider.Value = 0;
        }

        private void ResetRightBottomOpacity_Click(object sender, RoutedEventArgs e)
        {
            PPTRBButtonOpacityValueSlider.Value = 0.5;
        }

        #endregion

        #region PPT Button Display Checkboxes

        private void CheckboxEnableLBPPTButton_IsCheckChanged(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var str = SettingsManager.Settings.PowerPointSettings.PPTButtonsDisplayOption.ToString();
            char[] c = str.ToCharArray();
            c[0] = CheckboxEnableLBPPTButton.IsOn ? '2' : '1';
            SettingsManager.Settings.PowerPointSettings.PPTButtonsDisplayOption = int.Parse(new string(c));
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnPPTButtonsDisplayOptionChanged();
            UpdatePreview();
        }

        private void CheckboxEnableRBPPTButton_IsCheckChanged(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var str = SettingsManager.Settings.PowerPointSettings.PPTButtonsDisplayOption.ToString();
            char[] c = str.ToCharArray();
            c[1] = CheckboxEnableRBPPTButton.IsOn ? '2' : '1';
            SettingsManager.Settings.PowerPointSettings.PPTButtonsDisplayOption = int.Parse(new string(c));
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnPPTButtonsDisplayOptionChanged();
            UpdatePreview();
        }

        private void CheckboxEnableLSPPTButton_IsCheckChanged(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var str = SettingsManager.Settings.PowerPointSettings.PPTButtonsDisplayOption.ToString();
            char[] c = str.ToCharArray();
            c[2] = CheckboxEnableLSPPTButton.IsOn ? '2' : '1';
            SettingsManager.Settings.PowerPointSettings.PPTButtonsDisplayOption = int.Parse(new string(c));
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnPPTButtonsDisplayOptionChanged();
            UpdatePreview();
        }

        private void CheckboxEnableRSPPTButton_IsCheckChanged(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var str = SettingsManager.Settings.PowerPointSettings.PPTButtonsDisplayOption.ToString();
            char[] c = str.ToCharArray();
            c[3] = CheckboxEnableRSPPTButton.IsOn ? '2' : '1';
            SettingsManager.Settings.PowerPointSettings.PPTButtonsDisplayOption = int.Parse(new string(c));
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnPPTButtonsDisplayOptionChanged();
            UpdatePreview();
        }

        private void CheckboxSPPTDisplayPage_IsCheckChange(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var str = SettingsManager.Settings.PowerPointSettings.PPTSButtonsOption.ToString();
            char[] c = str.ToCharArray();
            c[0] = CheckboxSPPTDisplayPage.IsChecked == true ? '2' : '1';
            SettingsManager.Settings.PowerPointSettings.PPTSButtonsOption = int.Parse(new string(c));
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnPPTSButtonsOptionChanged();
            UpdatePreview();
        }

        private void CheckboxSPPTHalfOpacity_IsCheckChange(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var ppt = SettingsManager.Settings.PowerPointSettings;
            var str = ppt.PPTSButtonsOption.ToString();
            char[] c = str.ToCharArray();
            bool isHalf = CheckboxSPPTHalfOpacity.IsChecked == true;
            c[1] = isHalf ? '2' : '1';
            ppt.PPTSButtonsOption = int.Parse(new string(c));
            if (isHalf)
            {
                if (ppt.PPTLSButtonOpacity == 1.0) ppt.PPTLSButtonOpacity = 0.5;
                if (ppt.PPTRSButtonOpacity == 1.0) ppt.PPTRSButtonOpacity = 0.5;
                PPTLSButtonOpacityValueSlider.Value = ppt.PPTLSButtonOpacity;
                PPTRSButtonOpacityValueSlider.Value = ppt.PPTRSButtonOpacity;
            }
            else
            {
                if (ppt.PPTLSButtonOpacity == 0.5) ppt.PPTLSButtonOpacity = 1.0;
                if (ppt.PPTRSButtonOpacity == 0.5) ppt.PPTRSButtonOpacity = 1.0;
                PPTLSButtonOpacityValueSlider.Value = ppt.PPTLSButtonOpacity;
                PPTRSButtonOpacityValueSlider.Value = ppt.PPTRSButtonOpacity;
            }
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnPPTSButtonsOptionWithOpacityChanged();
            UpdatePreview();
        }

        private void CheckboxSPPTBlackBackground_IsCheckChange(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var str = SettingsManager.Settings.PowerPointSettings.PPTSButtonsOption.ToString();
            char[] c = str.ToCharArray();
            c[2] = CheckboxSPPTBlackBackground.IsChecked == true ? '2' : '1';
            SettingsManager.Settings.PowerPointSettings.PPTSButtonsOption = int.Parse(new string(c));
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnPPTSButtonsOptionChanged();
            UpdatePreview();
        }

        private void CheckboxBPPTDisplayPage_IsCheckChange(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var str = SettingsManager.Settings.PowerPointSettings.PPTBButtonsOption.ToString();
            char[] c = str.ToCharArray();
            c[0] = CheckboxBPPTDisplayPage.IsChecked == true ? '2' : '1';
            SettingsManager.Settings.PowerPointSettings.PPTBButtonsOption = int.Parse(new string(c));
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnPPTBButtonsOptionChanged();
            UpdatePreview();
        }

        private void CheckboxBPPTHalfOpacity_IsCheckChange(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var ppt = SettingsManager.Settings.PowerPointSettings;
            var str = ppt.PPTBButtonsOption.ToString();
            char[] c = str.ToCharArray();
            bool isHalf = CheckboxBPPTHalfOpacity.IsChecked == true;
            c[1] = isHalf ? '2' : '1';
            ppt.PPTBButtonsOption = int.Parse(new string(c));
            if (isHalf)
            {
                if (ppt.PPTLBButtonOpacity == 1.0) ppt.PPTLBButtonOpacity = 0.5;
                if (ppt.PPTRBButtonOpacity == 1.0) ppt.PPTRBButtonOpacity = 0.5;
                PPTLBButtonOpacityValueSlider.Value = ppt.PPTLBButtonOpacity;
                PPTRBButtonOpacityValueSlider.Value = ppt.PPTRBButtonOpacity;
            }
            else
            {
                if (ppt.PPTLBButtonOpacity == 0.5) ppt.PPTLBButtonOpacity = 1.0;
                if (ppt.PPTRBButtonOpacity == 0.5) ppt.PPTRBButtonOpacity = 1.0;
                PPTLBButtonOpacityValueSlider.Value = ppt.PPTLBButtonOpacity;
                PPTRBButtonOpacityValueSlider.Value = ppt.PPTRBButtonOpacity;
            }
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnPPTBButtonsOptionWithOpacityChanged();
            UpdatePreview();
        }

        private void CheckboxBPPTBlackBackground_IsCheckChange(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var str = SettingsManager.Settings.PowerPointSettings.PPTBButtonsOption.ToString();
            char[] c = str.ToCharArray();
            c[2] = CheckboxBPPTBlackBackground.IsChecked == true ? '2' : '1';
            SettingsManager.Settings.PowerPointSettings.PPTBButtonsOption = int.Parse(new string(c));
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnPPTBButtonsOptionChanged();
            UpdatePreview();
        }

        #endregion

        #region PPT SlideShow Entry & Gesture

        private void ToggleSwitchShowCanvasAtNewSlideShow_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.PowerPointSettings.IsShowCanvasAtNewSlideShow = CardShowCanvasAtNewSlideShow.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchEnableTwoFingerGestureInPresentationMode_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.PowerPointSettings.IsEnableTwoFingerGestureInPresentationMode = CardEnableTwoFingerGestureInPresentationMode.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchEnableFingerGestureSlideShowControl_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.PowerPointSettings.IsEnableFingerGestureSlideShowControl = CardEnableFingerGestureSlideShowControl.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchEnablePPTTimeCapsule_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.PowerPointSettings.EnablePPTTimeCapsule = CardEnablePPTTimeCapsule.IsOn;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnPPTTimeCapsuleChanged();
            UpdatePreview();
        }

        private void ComboBoxPPTTimeCapsulePosition_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || ComboBoxPPTTimeCapsulePosition == null) return;
            SettingsManager.Settings.PowerPointSettings.PPTTimeCapsulePosition = ComboBoxPPTTimeCapsulePosition.SelectedIndex;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnPPTTimeCapsulePositionChanged();
            UpdatePreview();
        }

        private void SliderPPTTimeCapsuleOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isLoaded || SliderPPTTimeCapsuleOpacity == null) return;
            var val = Math.Round(SliderPPTTimeCapsuleOpacity.Value, 2);
            if (SliderPPTTimeCapsuleOpacity.Value != val)
            {
                SliderPPTTimeCapsuleOpacity.Value = val;
                return;
            }
            SettingsManager.Settings.PowerPointSettings.PPTTimeCapsuleOpacity = val;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnPPTTimeCapsuleOpacityChanged();
            UpdatePreview();
        }

        private void SliderPPTTimeCapsuleScale_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isLoaded || SliderPPTTimeCapsuleScale == null) return;
            var val = Math.Round(SliderPPTTimeCapsuleScale.Value, 1);
            if (SliderPPTTimeCapsuleScale.Value != val)
            {
                SliderPPTTimeCapsuleScale.Value = val;
                return;
            }
            SettingsManager.Settings.PowerPointSettings.PPTTimeCapsuleScale = val;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnPPTTimeCapsuleScaleChanged();
            UpdatePreview();
        }

        private void ButtonResetPPTTimeCapsulePosition_Click(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsActionHub.OnResetPPTTimeCapsulePosition();
            UpdatePreview();
        }

        #endregion

        #region PPT Auto Save & Notifications

        private void ToggleSwitchAutoSaveScreenShotInPowerPoint_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.PowerPointSettings.IsAutoSaveScreenShotInPowerPoint = CardAutoSaveScreenShotInPowerPoint.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchAutoSaveStrokesInPowerPoint_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.PowerPointSettings.IsAutoSaveStrokesInPowerPoint = CardAutoSaveStrokesInPowerPoint.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchNotifyPreviousPage_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.PowerPointSettings.IsNotifyPreviousPage = CardNotifyPreviousPage.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchAlwaysGoToFirstPageOnReenter_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.PowerPointSettings.IsAlwaysGoToFirstPageOnReenter = CardAlwaysGoToFirstPageOnReenter.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchNotifyHiddenPage_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.PowerPointSettings.IsNotifyHiddenPage = CardNotifyHiddenPage.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchNotifyAutoPlayPresentation_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.PowerPointSettings.IsNotifyAutoPlayPresentation = CardNotifyAutoPlayPresentation.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        #endregion
    }
}
