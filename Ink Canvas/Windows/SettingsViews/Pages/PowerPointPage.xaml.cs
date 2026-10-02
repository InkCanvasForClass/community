using Ink_Canvas.Helpers;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Windows;
using System.Windows.Controls;
using Page = System.Windows.Controls.Page;

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

            CardSupportPowerPoint.IsChecked = ppt.PowerPointSupport;
            ComboBoxPPTArchitecture.SelectedIndex = (int)ppt.PPTLinkMode;
            CardPowerPointEnhancement.IsChecked = ppt.EnablePowerPointEnhancement;
            CardSkipAnimationsWhenGoNext.IsChecked = ppt.SkipAnimationsWhenGoNext;
            CardSupportWPS.IsChecked = ppt.IsSupportWPS;
            CardEnableWppProcessKill.IsChecked = ppt.EnableWppProcessKill;
            UpdatePPTArchitectureDependentCards();



            CardEnablePPTButtonPageClickable.IsChecked = ppt.EnablePPTButtonPageClickable;
            ToggleSwitchEnablePPTButtonEnhancedPreview.IsChecked = ppt.EnablePPTButtonEnhancedPreview;
            ToggleSwitchPPTEnhancedPreviewLoadingAnimation.IsChecked = ppt.ShowPPTEnhancedPreviewLoadingAnimation;
            CardEnablePPTButtonLongPressPageTurn.IsChecked = ppt.EnablePPTButtonLongPressPageTurn;

            CardShowCanvasAtNewSlideShow.IsChecked = ppt.IsShowCanvasAtNewSlideShow;
            CardEnableSmartMode.IsChecked = ppt.EnableSmartMode;

            CardEnableTwoFingerGestureInPresentationMode.IsChecked = ppt.IsEnableTwoFingerGestureInPresentationMode;
            CardEnableFingerGestureSlideShowControl.IsChecked = ppt.IsEnableFingerGestureSlideShowControl;
            CardEnablePPTTimeCapsule.IsChecked = ppt.EnablePPTTimeCapsule;
            ComboBoxPPTTimeCapsulePosition.SelectedIndex = ppt.PPTTimeCapsulePosition;
            CardShowPPTSidebarByDefault.IsChecked = ppt.ShowPPTSidebarByDefault;
            CardShowPPTModePrompt.IsChecked = ppt.ShowPPTModePrompt;

            CardAutoSaveScreenShotInPowerPoint.IsChecked = ppt.IsAutoSaveScreenShotInPowerPoint;
            CardAutoSaveStrokesInPowerPoint.IsChecked = ppt.IsAutoSaveStrokesInPowerPoint;

            CardNotifyPreviousPage.IsChecked = ppt.IsNotifyPreviousPage;
            CardAlwaysGoToFirstPageOnReenter.IsChecked = ppt.IsAlwaysGoToFirstPageOnReenter;
            CardNotifyHiddenPage.IsChecked = ppt.IsNotifyHiddenPage;
            CardNotifyAutoPlayPresentation.IsChecked = ppt.IsNotifyAutoPlayPresentation;

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
            ppt.PowerPointSupport = ((CardSupportPowerPoint.IsChecked) == true);
            if (!ppt.PowerPointSupport && ppt.IsSupportWPS)
            {
                ppt.IsSupportWPS = false;
                CardSupportWPS.IsChecked = false;
            }
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnPPTSupportChanged(((CardSupportPowerPoint.IsChecked) == true));
        }

        private void ToggleSwitchPowerPointEnhancement_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var ppt = SettingsManager.Settings.PowerPointSettings;
            ppt.EnablePowerPointEnhancement = ((CardPowerPointEnhancement.IsChecked) == true);
            if (ppt.EnablePowerPointEnhancement)
            {
                ppt.IsSupportWPS = false;
                CardSupportWPS.IsChecked = false;
            }
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnPPTEnhancementChanged(((CardPowerPointEnhancement.IsChecked) == true));
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
                CardPowerPointEnhancement.IsChecked = false;
                CardSupportWPS.IsChecked = false;
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
            SettingsManager.Settings.PowerPointSettings.SkipAnimationsWhenGoNext = ((CardSkipAnimationsWhenGoNext.IsChecked) == true);
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnSkipAnimationsWhenGoNextChanged(((CardSkipAnimationsWhenGoNext.IsChecked) == true));
        }

        private void ToggleSwitchSupportWPS_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var ppt = SettingsManager.Settings.PowerPointSettings;
            ppt.IsSupportWPS = ((CardSupportWPS.IsChecked) == true);
            if (ppt.IsSupportWPS)
            {
                if (!ppt.PowerPointSupport)
                {
                    ppt.PowerPointSupport = true;
                    CardSupportPowerPoint.IsChecked = true;
                }
                if (ppt.EnablePowerPointEnhancement)
                {
                    ppt.EnablePowerPointEnhancement = false;
                    CardPowerPointEnhancement.IsChecked = false;
                }
            }
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnSupportWPSChanged();
        }

        private void ToggleSwitchEnableWppProcessKill_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.PowerPointSettings.EnableWppProcessKill = ((CardEnableWppProcessKill.IsChecked) == true);
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
            SettingsManager.Settings.PowerPointSettings.ShowPPTSidebarByDefault = ((CardShowPPTSidebarByDefault.IsChecked) == true);
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnShowPPTSidebarByDefaultChanged();
        }

        private void ToggleSwitchShowPPTModePrompt_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.PowerPointSettings.ShowPPTModePrompt = ((CardShowPPTModePrompt.IsChecked) == true);
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchEnablePPTButtonPageClickable_OnToggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.PowerPointSettings.EnablePPTButtonPageClickable = ((CardEnablePPTButtonPageClickable.IsChecked) == true);
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchEnablePPTButtonEnhancedPreview_OnToggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.PowerPointSettings.EnablePPTButtonEnhancedPreview = ((ToggleSwitchEnablePPTButtonEnhancedPreview.IsChecked) == true);
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchPPTEnhancedPreviewLoadingAnimation_OnToggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.PowerPointSettings.ShowPPTEnhancedPreviewLoadingAnimation = ((ToggleSwitchPPTEnhancedPreviewLoadingAnimation.IsChecked) == true);
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchEnablePPTButtonLongPressPageTurn_OnToggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.PowerPointSettings.EnablePPTButtonLongPressPageTurn = ((CardEnablePPTButtonLongPressPageTurn.IsChecked) == true);
            SettingsManager.SaveSettingsToFile();
        }

        #endregion



        #region PPT SlideShow Entry & Gesture

        private void ToggleSwitchEnableSmartMode_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.PowerPointSettings.EnableSmartMode = ((CardEnableSmartMode.IsChecked) == true);
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchShowCanvasAtNewSlideShow_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.PowerPointSettings.IsShowCanvasAtNewSlideShow = ((CardShowCanvasAtNewSlideShow.IsChecked) == true);
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchEnableTwoFingerGestureInPresentationMode_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.PowerPointSettings.IsEnableTwoFingerGestureInPresentationMode = ((CardEnableTwoFingerGestureInPresentationMode.IsChecked) == true);
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchEnableFingerGestureSlideShowControl_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.PowerPointSettings.IsEnableFingerGestureSlideShowControl = ((CardEnableFingerGestureSlideShowControl.IsChecked) == true);
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchEnablePPTTimeCapsule_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.PowerPointSettings.EnablePPTTimeCapsule = ((CardEnablePPTTimeCapsule.IsChecked) == true);
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
            SettingsManager.Settings.PowerPointSettings.IsAutoSaveScreenShotInPowerPoint = ((CardAutoSaveScreenShotInPowerPoint.IsChecked) == true);
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchAutoSaveStrokesInPowerPoint_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.PowerPointSettings.IsAutoSaveStrokesInPowerPoint = ((CardAutoSaveStrokesInPowerPoint.IsChecked) == true);
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchNotifyPreviousPage_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.PowerPointSettings.IsNotifyPreviousPage = ((CardNotifyPreviousPage.IsChecked) == true);
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchAlwaysGoToFirstPageOnReenter_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.PowerPointSettings.IsAlwaysGoToFirstPageOnReenter = ((CardAlwaysGoToFirstPageOnReenter.IsChecked) == true);
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchNotifyHiddenPage_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.PowerPointSettings.IsNotifyHiddenPage = ((CardNotifyHiddenPage.IsChecked) == true);
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchNotifyAutoPlayPresentation_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.PowerPointSettings.IsNotifyAutoPlayPresentation = ((CardNotifyAutoPlayPresentation.IsChecked) == true);
            SettingsManager.SaveSettingsToFile();
        }

        #endregion
    }
}
