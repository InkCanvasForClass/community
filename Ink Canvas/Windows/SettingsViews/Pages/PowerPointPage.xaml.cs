using Ink_Canvas.Helpers;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Windows;
using System.Windows.Controls;
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

        private void LoadSettings()
        {
            _isLoaded = false;
            var ppt = SettingsManager.Settings.PowerPointSettings;

            CardSupportPowerPoint.IsOn = ppt.PowerPointSupport;
            ComboBoxPPTArchitecture.SelectedIndex = (int)ppt.PPTLinkMode;
            CardPowerPointEnhancement.IsOn = ppt.EnablePowerPointEnhancement;
            CardSkipAnimationsWhenGoNext.IsOn = ppt.SkipAnimationsWhenGoNext;
            CardSupportWPS.IsOn = ppt.IsSupportWPS;
            CardEnableWppProcessKill.IsOn = ppt.EnableWppProcessKill;
            UpdatePPTArchitectureDependentCards();



            CardEnablePPTButtonPageClickable.IsOn = ppt.EnablePPTButtonPageClickable;
            ToggleSwitchEnablePPTButtonEnhancedPreview.IsOn = ppt.EnablePPTButtonEnhancedPreview;
            ToggleSwitchPPTEnhancedPreviewLoadingAnimation.IsOn = ppt.ShowPPTEnhancedPreviewLoadingAnimation;
            CardEnablePPTButtonLongPressPageTurn.IsOn = ppt.EnablePPTButtonLongPressPageTurn;

            CardShowCanvasAtNewSlideShow.IsOn = ppt.IsShowCanvasAtNewSlideShow;
            CardEnableSmartMode.IsOn = ppt.EnableSmartMode;

            CardEnableTwoFingerGestureInPresentationMode.IsOn = ppt.IsEnableTwoFingerGestureInPresentationMode;
            CardEnableFingerGestureSlideShowControl.IsOn = ppt.IsEnableFingerGestureSlideShowControl;
            CardEnablePPTTimeCapsule.IsOn = ppt.EnablePPTTimeCapsule;
            ComboBoxPPTTimeCapsulePosition.SelectedIndex = ppt.PPTTimeCapsulePosition;
            CardShowPPTSidebarByDefault.IsOn = ppt.ShowPPTSidebarByDefault;
            CardShowPPTModePrompt.IsOn = ppt.ShowPPTModePrompt;

            CardAutoSaveScreenShotInPowerPoint.IsOn = ppt.IsAutoSaveScreenShotInPowerPoint;
            CardAutoSaveStrokesInPowerPoint.IsOn = ppt.IsAutoSaveStrokesInPowerPoint;

            CardNotifyPreviousPage.IsOn = ppt.IsNotifyPreviousPage;
            CardAlwaysGoToFirstPageOnReenter.IsOn = ppt.IsAlwaysGoToFirstPageOnReenter;
            CardNotifyHiddenPage.IsOn = ppt.IsNotifyHiddenPage;
            CardNotifyAutoPlayPresentation.IsOn = ppt.IsNotifyAutoPlayPresentation;

            _isLoaded = true;
        }

        #region PPT Basic

        private void UpdatePPTArchitectureDependentCards()
        {
            bool isComArchitecture = SettingsManager.Settings.PowerPointSettings.PPTLinkMode == PPTLinkMode.Com;
            var visibility = isComArchitecture ? Visibility.Visible : Visibility.Collapsed;
            CardPowerPointEnhancement.Visibility = visibility;
            CardSupportWPS.Visibility = visibility;
            CardEnableWppProcessKill.Visibility = visibility;
        }

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

        private void ComboBoxPPTArchitecture_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            var ppt = SettingsManager.Settings.PowerPointSettings;
            var selectedMode = (PPTLinkMode)Math.Max(0, ComboBoxPPTArchitecture.SelectedIndex);
            if (ppt.PPTLinkMode == selectedMode) return;

            ppt.PPTLinkMode = selectedMode;
            if (ppt.PPTLinkMode != PPTLinkMode.Com)
            {
                ppt.EnablePowerPointEnhancement = false;
                ppt.IsSupportWPS = false;
                CardPowerPointEnhancement.IsOn = false;
                CardSupportWPS.IsOn = false;
            }
            UpdatePPTArchitectureDependentCards();
            SettingsManager.SaveSettingsToFile();
            try
            {
                SettingsActionHub.OnPPTLinkModeChanged();
            }
            catch (Exception ex) { LogHelper.WriteLogToFile($"切换 PPT 联动架构失败: {ex}", LogHelper.LogType.Error); }
        }

        private void ToggleSwitchSkipAnimationsWhenGoNext_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.PowerPointSettings.SkipAnimationsWhenGoNext = CardSkipAnimationsWhenGoNext.IsOn;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnSkipAnimationsWhenGoNextChanged(CardSkipAnimationsWhenGoNext.IsOn);
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

        private void OnCardClicked(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is SettingsWindow settingsWindow)
            {
                settingsWindow.NavigateToPage("PPTPageFlipPreviewPage");
            }
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
            SettingsManager.Settings.PowerPointSettings.EnablePPTButtonEnhancedPreview = ToggleSwitchEnablePPTButtonEnhancedPreview.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchPPTEnhancedPreviewLoadingAnimation_OnToggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.PowerPointSettings.ShowPPTEnhancedPreviewLoadingAnimation = ToggleSwitchPPTEnhancedPreviewLoadingAnimation.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchEnablePPTButtonLongPressPageTurn_OnToggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.PowerPointSettings.EnablePPTButtonLongPressPageTurn = CardEnablePPTButtonLongPressPageTurn.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        #endregion



        #region PPT SlideShow Entry & Gesture

        private void ToggleSwitchEnableSmartMode_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.PowerPointSettings.EnableSmartMode = CardEnableSmartMode.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

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
        }

        private void ComboBoxPPTTimeCapsulePosition_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || ComboBoxPPTTimeCapsulePosition == null) return;
            SettingsManager.Settings.PowerPointSettings.PPTTimeCapsulePosition = ComboBoxPPTTimeCapsulePosition.SelectedIndex;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnPPTTimeCapsulePositionChanged();
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
        }

        private void ButtonResetPPTTimeCapsulePosition_Click(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsActionHub.OnResetPPTTimeCapsulePosition();
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
