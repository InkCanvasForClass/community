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

        private MainWindow GetMainWindow() => Application.Current.MainWindow as MainWindow;

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
            CheckboxEnableLBPPTButton.IsChecked = displayOpt.Length > 0 && displayOpt[0] == '2';
            CheckboxEnableRBPPTButton.IsChecked = displayOpt.Length > 1 && displayOpt[1] == '2';
            CheckboxEnableLSPPTButton.IsChecked = displayOpt.Length > 2 && displayOpt[2] == '2';
            CheckboxEnableRSPPTButton.IsChecked = displayOpt.Length > 3 && displayOpt[3] == '2';

            PPTButtonLeftPositionValueSlider.Value = ppt.PPTLSButtonPosition;
            PPTButtonRightPositionValueSlider.Value = ppt.PPTRSButtonPosition;
            PPTButtonLBPositionValueSlider.Value = ppt.PPTLBButtonPosition;
            PPTButtonRBPositionValueSlider.Value = ppt.PPTRBButtonPosition;

            PPTLSButtonOpacityValueSlider.Value = ppt.PPTLSButtonOpacity;
            PPTRSButtonOpacityValueSlider.Value = ppt.PPTRSButtonOpacity;
            PPTLBButtonOpacityValueSlider.Value = ppt.PPTLBButtonOpacity;
            PPTRBButtonOpacityValueSlider.Value = ppt.PPTRBButtonOpacity;

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
            CardShowGestureButtonInSlideShow.IsOn = ppt.ShowGestureButtonInSlideShow;
            CardEnablePPTTimeCapsule.IsOn = ppt.EnablePPTTimeCapsule;
            ComboBoxPPTTimeCapsulePosition.SelectedIndex = ppt.PPTTimeCapsulePosition;
            CardShowPPTSidebarByDefault.IsOn = ppt.ShowPPTSidebarByDefault;

            CardAutoSaveScreenShotInPowerPoint.IsOn = ppt.IsAutoSaveScreenShotInPowerPoint;
            CardAutoSaveStrokesInPowerPoint.IsOn = ppt.IsAutoSaveStrokesInPowerPoint;

            CardNotifyPreviousPage.IsOn = ppt.IsNotifyPreviousPage;
            CardAlwaysGoToFirstPageOnReenter.IsOn = ppt.IsAlwaysGoToFirstPageOnReenter;
            CardNotifyHiddenPage.IsOn = ppt.IsNotifyHiddenPage;
            CardNotifyAutoPlayPresentation.IsOn = ppt.IsNotifyAutoPlayPresentation;

            _isLoaded = true;
        }

        #region PPT Basic

        private void ToggleSwitchSupportPowerPoint_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var mw = GetMainWindow();
            var ppt = SettingsManager.Settings.PowerPointSettings;
            ppt.PowerPointSupport = CardSupportPowerPoint.IsOn;
            if (!ppt.PowerPointSupport && ppt.IsSupportWPS)
            {
                ppt.IsSupportWPS = false;
                CardSupportWPS.IsOn = false;
                if (mw?.PPTManager != null) mw.PPTManager.IsSupportWPS = false;
            }
            SettingsManager.SaveSettingsToFile();
            if (mw != null)
            {
                if (ppt.PowerPointSupport)
                {
                    if (mw.PPTManager == null) mw.InitializePPTManagers();
                    mw.StartPPTMonitoring();
                }
                else mw.StopPPTMonitoring();
            }
        }

        private void ToggleSwitchPowerPointEnhancement_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var mw = GetMainWindow();
            var ppt = SettingsManager.Settings.PowerPointSettings;
            ppt.EnablePowerPointEnhancement = CardPowerPointEnhancement.IsOn;
            if (ppt.EnablePowerPointEnhancement)
            {
                ppt.IsSupportWPS = false;
                CardSupportWPS.IsOn = false;
                if (mw?.PPTManager != null) mw.PPTManager.IsSupportWPS = false;
            }
            SettingsManager.SaveSettingsToFile();
            if (mw != null)
            {
                if (ppt.EnablePowerPointEnhancement)
                    mw.StartPowerPointProcessMonitoring();
                else
                    mw.StopPowerPointProcessMonitoring();
            }
        }

        private void ToggleSwitchSkipAnimationsWhenGoNext_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var mw = GetMainWindow();
            SettingsManager.Settings.PowerPointSettings.SkipAnimationsWhenGoNext = CardSkipAnimationsWhenGoNext.IsOn;
            if (mw?.PPTManager != null)
                mw.PPTManager.SkipAnimationsWhenNavigating = CardSkipAnimationsWhenGoNext.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchUseRotPptLink_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var mw = GetMainWindow();
            var ppt = SettingsManager.Settings.PowerPointSettings;
            ppt.UseRotPptLink = CardUseRotPptLink.IsOn;
            SettingsManager.SaveSettingsToFile();
            try
            {
                if (mw != null)
                {
                    mw.StopPPTMonitoring();
                    if (ppt.UseRotPptLink && ppt.EnablePowerPointEnhancement)
                    {
                        ppt.EnablePowerPointEnhancement = false;
                        CardPowerPointEnhancement.IsOn = false;
                        mw.StopPowerPointProcessMonitoring();
                        SettingsManager.SaveSettingsToFile();
                    }
                    mw.InitializePPTManagers();
                    if (ppt.PowerPointSupport) mw.StartPPTMonitoring();
                    LogHelper.WriteLogToFile($"已切换 PPT 联动架构为 {(ppt.UseRotPptLink ? "ROT" : "COM")}", LogHelper.LogType.Event);
                }
            }
            catch (Exception ex) { LogHelper.WriteLogToFile($"切换 PPT 联动架构失败: {ex}", LogHelper.LogType.Error); }
        }

        private void ToggleSwitchSupportWPS_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var mw = GetMainWindow();
            var ppt = SettingsManager.Settings.PowerPointSettings;
            ppt.IsSupportWPS = CardSupportWPS.IsOn;
            if (ppt.IsSupportWPS)
            {
                if (!ppt.PowerPointSupport)
                {
                    ppt.PowerPointSupport = true;
                    CardSupportPowerPoint.IsOn = true;
                    if (mw != null)
                    {
                        if (mw.PPTManager == null) mw.InitializePPTManagers();
                        mw.StartPPTMonitoring();
                    }
                }
                if (ppt.EnablePowerPointEnhancement)
                {
                    ppt.EnablePowerPointEnhancement = false;
                    CardPowerPointEnhancement.IsOn = false;
                    mw?.StopPowerPointProcessMonitoring();
                }
            }
            if (mw?.PPTManager != null)
            {
                mw.PPTManager.IsSupportWPS = ppt.IsSupportWPS;
                mw.PPTManager.SkipAnimationsWhenNavigating = ppt.SkipAnimationsWhenGoNext;
            }
            SettingsManager.SaveSettingsToFile();
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
            var mw = GetMainWindow();
            SettingsManager.Settings.PowerPointSettings.ShowPPTButton = CardShowPPTButton.IsOn;
            SettingsManager.SaveSettingsToFile();
            if (mw?.PPTUIManager != null)
            {
                mw.PPTUIManager.ShowPPTButton = CardShowPPTButton.IsOn;
                mw.PPTUIManager.UpdateNavigationPanelsVisibility();
            }
            mw?.UpdatePPTBtnPreview();
        }

        private void ToggleSwitchShowPPTSidebarByDefault_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var mw = GetMainWindow();
            SettingsManager.Settings.PowerPointSettings.ShowPPTSidebarByDefault = CardShowPPTSidebarByDefault.IsOn;
            SettingsManager.SaveSettingsToFile();
            if (mw != null && mw.IsInPptPresentationMode)
                mw.UpdatePPTQuickPanelVisibility();
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
            var mw = GetMainWindow();
            SettingsManager.Settings.PowerPointSettings.PPTLSButtonPosition = (int)PPTButtonLeftPositionValueSlider.Value;
            mw?.UpdatePPTBtnSlidersStatus();
            mw?.UpdatePPTUIManagerSettings();
            _sliderDelayAction.DebounceAction(2000, null, () => SettingsManager.SaveSettingsToFile());
            mw?.UpdatePPTBtnPreview();
        }

        private void PPTButtonRightPositionValueSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(PPTButtonRightPositionValueSlider, PPTButtonRightPositionText, "{0:F0}");
            if (!_isLoaded) return;
            var mw = GetMainWindow();
            SettingsManager.Settings.PowerPointSettings.PPTRSButtonPosition = (int)PPTButtonRightPositionValueSlider.Value;
            mw?.UpdatePPTBtnSlidersStatus();
            mw?.UpdatePPTUIManagerSettings();
            _sliderDelayAction.DebounceAction(2000, null, () => SettingsManager.SaveSettingsToFile());
            mw?.UpdatePPTBtnPreview();
        }

        private void PPTButtonLBPositionValueSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(PPTButtonLBPositionValueSlider, PPTButtonLBPositionText, "{0:F0}");
            if (!_isLoaded) return;
            var mw = GetMainWindow();
            SettingsManager.Settings.PowerPointSettings.PPTLBButtonPosition = (int)PPTButtonLBPositionValueSlider.Value;
            mw?.UpdatePPTBtnSlidersStatus();
            mw?.UpdatePPTUIManagerSettings();
            _sliderDelayAction.DebounceAction(2000, null, () => SettingsManager.SaveSettingsToFile());
            mw?.UpdatePPTBtnPreview();
        }

        private void PPTButtonRBPositionValueSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateSliderText(PPTButtonRBPositionValueSlider, PPTButtonRBPositionText, "{0:F0}");
            if (!_isLoaded) return;
            var mw = GetMainWindow();
            SettingsManager.Settings.PowerPointSettings.PPTRBButtonPosition = (int)PPTButtonRBPositionValueSlider.Value;
            mw?.UpdatePPTBtnSlidersStatus();
            mw?.UpdatePPTUIManagerSettings();
            _sliderDelayAction.DebounceAction(2000, null, () => SettingsManager.SaveSettingsToFile());
            mw?.UpdatePPTBtnPreview();
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
            var mw = GetMainWindow();
            if (mw?.PPTUIManager != null)
            {
                mw.PPTUIManager.PPTLSButtonOpacity = roundedValue;
                mw.PPTUIManager.UpdateNavigationButtonStyles();
            }
            mw?.UpdatePPTBtnPreview();
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
            var mw = GetMainWindow();
            if (mw?.PPTUIManager != null)
            {
                mw.PPTUIManager.PPTRSButtonOpacity = roundedValue;
                mw.PPTUIManager.UpdateNavigationButtonStyles();
            }
            mw?.UpdatePPTBtnPreview();
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
            var mw = GetMainWindow();
            if (mw?.PPTUIManager != null)
            {
                mw.PPTUIManager.PPTLBButtonOpacity = roundedValue;
                mw.PPTUIManager.UpdateNavigationButtonStyles();
            }
            mw?.UpdatePPTBtnPreview();
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
            var mw = GetMainWindow();
            if (mw?.PPTUIManager != null)
            {
                mw.PPTUIManager.PPTRBButtonOpacity = roundedValue;
                mw.PPTUIManager.UpdateNavigationButtonStyles();
            }
            mw?.UpdatePPTBtnPreview();
        }

        #endregion

        #region PPT Button Display Checkboxes

        private void CheckboxEnableLBPPTButton_IsCheckChanged(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var mw = GetMainWindow();
            var str = SettingsManager.Settings.PowerPointSettings.PPTButtonsDisplayOption.ToString();
            char[] c = str.ToCharArray();
            c[0] = CheckboxEnableLBPPTButton.IsChecked == true ? '2' : '1';
            SettingsManager.Settings.PowerPointSettings.PPTButtonsDisplayOption = int.Parse(new string(c));
            SettingsManager.SaveSettingsToFile();
            if (mw?.PPTUIManager != null && mw.IsInPptPresentationMode)
            {
                mw.PPTUIManager.PPTButtonsDisplayOption = SettingsManager.Settings.PowerPointSettings.PPTButtonsDisplayOption;
                mw.PPTUIManager.UpdateNavigationPanelsVisibility();
            }
            mw?.UpdatePPTBtnPreview();
        }

        private void CheckboxEnableRBPPTButton_IsCheckChanged(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var mw = GetMainWindow();
            var str = SettingsManager.Settings.PowerPointSettings.PPTButtonsDisplayOption.ToString();
            char[] c = str.ToCharArray();
            c[1] = CheckboxEnableRBPPTButton.IsChecked == true ? '2' : '1';
            SettingsManager.Settings.PowerPointSettings.PPTButtonsDisplayOption = int.Parse(new string(c));
            SettingsManager.SaveSettingsToFile();
            if (mw?.PPTUIManager != null && mw.IsInPptPresentationMode)
            {
                mw.PPTUIManager.PPTButtonsDisplayOption = SettingsManager.Settings.PowerPointSettings.PPTButtonsDisplayOption;
                mw.PPTUIManager.UpdateNavigationPanelsVisibility();
            }
            mw?.UpdatePPTBtnPreview();
        }

        private void CheckboxEnableLSPPTButton_IsCheckChanged(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var mw = GetMainWindow();
            var str = SettingsManager.Settings.PowerPointSettings.PPTButtonsDisplayOption.ToString();
            char[] c = str.ToCharArray();
            c[2] = CheckboxEnableLSPPTButton.IsChecked == true ? '2' : '1';
            SettingsManager.Settings.PowerPointSettings.PPTButtonsDisplayOption = int.Parse(new string(c));
            SettingsManager.SaveSettingsToFile();
            if (mw?.PPTUIManager != null && mw.IsInPptPresentationMode)
            {
                mw.PPTUIManager.PPTButtonsDisplayOption = SettingsManager.Settings.PowerPointSettings.PPTButtonsDisplayOption;
                mw.PPTUIManager.UpdateNavigationPanelsVisibility();
            }
            mw?.UpdatePPTBtnPreview();
        }

        private void CheckboxEnableRSPPTButton_IsCheckChanged(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var mw = GetMainWindow();
            var str = SettingsManager.Settings.PowerPointSettings.PPTButtonsDisplayOption.ToString();
            char[] c = str.ToCharArray();
            c[3] = CheckboxEnableRSPPTButton.IsChecked == true ? '2' : '1';
            SettingsManager.Settings.PowerPointSettings.PPTButtonsDisplayOption = int.Parse(new string(c));
            SettingsManager.SaveSettingsToFile();
            if (mw?.PPTUIManager != null && mw.IsInPptPresentationMode)
            {
                mw.PPTUIManager.PPTButtonsDisplayOption = SettingsManager.Settings.PowerPointSettings.PPTButtonsDisplayOption;
                mw.PPTUIManager.UpdateNavigationPanelsVisibility();
            }
            mw?.UpdatePPTBtnPreview();
        }

        private void CheckboxSPPTDisplayPage_IsCheckChange(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var mw = GetMainWindow();
            var str = SettingsManager.Settings.PowerPointSettings.PPTSButtonsOption.ToString();
            char[] c = str.ToCharArray();
            c[0] = CheckboxSPPTDisplayPage.IsChecked == true ? '2' : '1';
            SettingsManager.Settings.PowerPointSettings.PPTSButtonsOption = int.Parse(new string(c));
            SettingsManager.SaveSettingsToFile();
            if (mw?.PPTUIManager != null && mw.IsInPptPresentationMode)
            {
                mw.PPTUIManager.PPTSButtonsOption = SettingsManager.Settings.PowerPointSettings.PPTSButtonsOption;
                mw.PPTUIManager.UpdateNavigationButtonStyles();
            }
            mw?.UpdatePPTBtnPreview();
        }

        private void CheckboxSPPTHalfOpacity_IsCheckChange(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var mw = GetMainWindow();
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
            if (mw?.PPTUIManager != null && mw.IsInPptPresentationMode)
            {
                mw.PPTUIManager.PPTSButtonsOption = ppt.PPTSButtonsOption;
                mw.PPTUIManager.PPTLSButtonOpacity = ppt.PPTLSButtonOpacity;
                mw.PPTUIManager.PPTRSButtonOpacity = ppt.PPTRSButtonOpacity;
                mw.PPTUIManager.UpdateNavigationButtonStyles();
            }
            mw?.UpdatePPTBtnPreview();
        }

        private void CheckboxSPPTBlackBackground_IsCheckChange(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var mw = GetMainWindow();
            var str = SettingsManager.Settings.PowerPointSettings.PPTSButtonsOption.ToString();
            char[] c = str.ToCharArray();
            c[2] = CheckboxSPPTBlackBackground.IsChecked == true ? '2' : '1';
            SettingsManager.Settings.PowerPointSettings.PPTSButtonsOption = int.Parse(new string(c));
            SettingsManager.SaveSettingsToFile();
            if (mw?.PPTUIManager != null && mw.IsInPptPresentationMode)
            {
                mw.PPTUIManager.PPTSButtonsOption = SettingsManager.Settings.PowerPointSettings.PPTSButtonsOption;
                mw.PPTUIManager.UpdateNavigationButtonStyles();
            }
            mw?.UpdatePPTBtnPreview();
        }

        private void CheckboxBPPTDisplayPage_IsCheckChange(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var mw = GetMainWindow();
            var str = SettingsManager.Settings.PowerPointSettings.PPTBButtonsOption.ToString();
            char[] c = str.ToCharArray();
            c[0] = CheckboxBPPTDisplayPage.IsChecked == true ? '2' : '1';
            SettingsManager.Settings.PowerPointSettings.PPTBButtonsOption = int.Parse(new string(c));
            SettingsManager.SaveSettingsToFile();
            if (mw?.PPTUIManager != null && mw.IsInPptPresentationMode)
            {
                mw.PPTUIManager.PPTBButtonsOption = SettingsManager.Settings.PowerPointSettings.PPTBButtonsOption;
                mw.PPTUIManager.UpdateNavigationButtonStyles();
            }
            mw?.UpdatePPTBtnPreview();
        }

        private void CheckboxBPPTHalfOpacity_IsCheckChange(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var mw = GetMainWindow();
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
            if (mw?.PPTUIManager != null && mw.IsInPptPresentationMode)
            {
                mw.PPTUIManager.PPTBButtonsOption = ppt.PPTBButtonsOption;
                mw.PPTUIManager.PPTLBButtonOpacity = ppt.PPTLBButtonOpacity;
                mw.PPTUIManager.PPTRBButtonOpacity = ppt.PPTRBButtonOpacity;
                mw.PPTUIManager.UpdateNavigationButtonStyles();
            }
            mw?.UpdatePPTBtnPreview();
        }

        private void CheckboxBPPTBlackBackground_IsCheckChange(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var mw = GetMainWindow();
            var str = SettingsManager.Settings.PowerPointSettings.PPTBButtonsOption.ToString();
            char[] c = str.ToCharArray();
            c[2] = CheckboxBPPTBlackBackground.IsChecked == true ? '2' : '1';
            SettingsManager.Settings.PowerPointSettings.PPTBButtonsOption = int.Parse(new string(c));
            SettingsManager.SaveSettingsToFile();
            if (mw?.PPTUIManager != null && mw.IsInPptPresentationMode)
            {
                mw.PPTUIManager.PPTBButtonsOption = SettingsManager.Settings.PowerPointSettings.PPTBButtonsOption;
                mw.PPTUIManager.UpdateNavigationButtonStyles();
            }
            mw?.UpdatePPTBtnPreview();
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

        private void ToggleSwitchShowGestureButtonInSlideShow_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var mw = GetMainWindow();
            SettingsManager.Settings.PowerPointSettings.ShowGestureButtonInSlideShow = CardShowGestureButtonInSlideShow.IsOn;
            SettingsManager.SaveSettingsToFile();
            if (mw != null && mw.IsInPptPresentationMode)
                mw.UpdateGestureButtonVisibilityInPPTMode();
        }

        private void ToggleSwitchEnablePPTTimeCapsule_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var mw = GetMainWindow();
            SettingsManager.Settings.PowerPointSettings.EnablePPTTimeCapsule = CardEnablePPTTimeCapsule.IsOn;
            SettingsManager.SaveSettingsToFile();
            if (mw != null && mw.IsInPptPresentationMode)
            {
                mw.UpdatePPTTimeCapsuleVisibility();
                mw.UpdatePPTQuickPanelVisibility();
            }
        }

        private void ComboBoxPPTTimeCapsulePosition_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || ComboBoxPPTTimeCapsulePosition == null) return;
            var mw = GetMainWindow();
            SettingsManager.Settings.PowerPointSettings.PPTTimeCapsulePosition = ComboBoxPPTTimeCapsulePosition.SelectedIndex;
            SettingsManager.SaveSettingsToFile();
            if (mw != null && mw.IsInPptPresentationMode)
                mw.UpdatePPTTimeCapsulePosition();
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
            var mw = GetMainWindow();
            if (mw != null && mw.IsInPptPresentationMode)
                mw.UpdatePPTTimeCapsuleOpacity();
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
            var mw = GetMainWindow();
            if (mw != null && mw.IsInPptPresentationMode)
                mw.UpdatePPTTimeCapsuleScale();
        }

        private void ButtonResetPPTTimeCapsulePosition_Click(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var mw = GetMainWindow();
            mw?.ResetPPTTimeCapsuleOffset();
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
