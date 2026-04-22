using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Diagnostics;
using System.Windows;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class PPTPage : iNKORE.UI.WPF.Modern.Controls.Page
    {
        private bool _isLoaded = false;

        public PPTPage()
        {
            InitializeComponent();
            Loaded += PPTPage_Loaded;
        }

        private void PPTPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
            _isLoaded = true;
        }

        private void LoadSettings()
        {
            _isLoaded = false;

            try
            {
                var settings = SettingsManager.Settings;
                if (settings.PowerPointSettings != null)
                {
                    CardSupportPowerPoint.IsOn = settings.PowerPointSettings.PowerPointSupport;
                    CardPowerPointEnhancement.IsOn = settings.PowerPointSettings.EnablePowerPointEnhancement;
                    CardSkipAnimationsWhenGoNext.IsOn = settings.PowerPointSettings.SkipAnimationsWhenGoNext;
                    CardUseRotPptLink.IsOn = settings.PowerPointSettings.UseRotPptLink;
                    CardSupportWPS.IsOn = settings.PowerPointSettings.IsSupportWPS;
                    CardEnableWppProcessKill.IsOn = settings.PowerPointSettings.EnableWppProcessKill;
                    CardShowPPTButton.IsOn = settings.PowerPointSettings.ShowPPTButton;
                    CardEnablePPTButtonPageClickable.IsOn = settings.PowerPointSettings.EnablePPTButtonPageClickable;
                    CardEnablePPTButtonLongPressPageTurn.IsOn = settings.PowerPointSettings.EnablePPTButtonLongPressPageTurn;
                    CardShowPPTSidebarByDefault.IsOn = settings.PowerPointSettings.ShowPPTSidebarByDefault;
                    CardShowCanvasAtNewSlideShow.IsOn = settings.PowerPointSettings.IsShowCanvasAtNewSlideShow;
                    CardEnableTwoFingerGestureInPresentationMode.IsOn = settings.PowerPointSettings.IsEnableTwoFingerGestureInPresentationMode;
                    CardEnableFingerGestureSlideShowControl.IsOn = settings.PowerPointSettings.IsEnableFingerGestureSlideShowControl;
                    CardEnablePPTTimeCapsule.IsOn = settings.PowerPointSettings.EnablePPTTimeCapsule;
                    CardAutoSaveScreenShotInPowerPoint.IsOn = settings.PowerPointSettings.IsAutoSaveScreenShotInPowerPoint;
                    CardAutoSaveStrokesInPowerPoint.IsOn = settings.PowerPointSettings.IsAutoSaveStrokesInPowerPoint;
                    CardNotifyPreviousPage.IsOn = settings.PowerPointSettings.IsNotifyPreviousPage;
                    CardAlwaysGoToFirstPageOnReenter.IsOn = settings.PowerPointSettings.IsAlwaysGoToFirstPageOnReenter;
                    CardNotifyHiddenPage.IsOn = settings.PowerPointSettings.IsNotifyHiddenPage;
                    CardNotifyAutoPlayPresentation.IsOn = settings.PowerPointSettings.IsNotifyAutoPlayPresentation;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载PPT设置时出错: {ex.Message}");
            }

            _isLoaded = true;
        }

        private void ToggleSwitchSupportPowerPoint_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchSupportPowerPoint", CardSupportPowerPoint.IsOn);
        }

        private void ToggleSwitchPowerPointEnhancement_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchPowerPointEnhancement", CardPowerPointEnhancement.IsOn);
        }

        private void ToggleSwitchSkipAnimationsWhenGoNext_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchSkipAnimationsWhenGoNext", CardSkipAnimationsWhenGoNext.IsOn);
        }

        private void ToggleSwitchUseRotPptLink_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchUseRotPptLink", CardUseRotPptLink.IsOn);
        }

        private void ToggleSwitchSupportWPS_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchSupportWPS", CardSupportWPS.IsOn);
        }

        private void ToggleSwitchEnableWppProcessKill_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchEnableWppProcessKill", CardEnableWppProcessKill.IsOn);
        }

        private void ToggleSwitchShowPPTButton_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchShowPPTButton", CardShowPPTButton.IsOn);
        }

        private void ToggleSwitchEnablePPTButtonPageClickable_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchEnablePPTButtonPageClickable", CardEnablePPTButtonPageClickable.IsOn);
        }

        private void ToggleSwitchEnablePPTButtonLongPressPageTurn_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchEnablePPTButtonLongPressPageTurn", CardEnablePPTButtonLongPressPageTurn.IsOn);
        }

        private void ToggleSwitchShowPPTSidebarByDefault_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchShowPPTSidebarByDefault", CardShowPPTSidebarByDefault.IsOn);
        }

        private void ToggleSwitchShowCanvasAtNewSlideShow_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchShowCanvasAtNewSlideShow", CardShowCanvasAtNewSlideShow.IsOn);
        }

        private void ToggleSwitchEnableTwoFingerGestureInPresentationMode_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchEnableTwoFingerGestureInPresentationMode", CardEnableTwoFingerGestureInPresentationMode.IsOn);
        }

        private void ToggleSwitchEnableFingerGestureSlideShowControl_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchEnableFingerGestureSlideShowControl", CardEnableFingerGestureSlideShowControl.IsOn);
        }

        private void ToggleSwitchEnablePPTTimeCapsule_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchEnablePPTTimeCapsule", CardEnablePPTTimeCapsule.IsOn);
        }

        private void ToggleSwitchAutoSaveScreenShotInPowerPoint_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchAutoSaveScreenShotInPowerPoint", CardAutoSaveScreenShotInPowerPoint.IsOn);
        }

        private void ToggleSwitchAutoSaveStrokesInPowerPoint_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchAutoSaveStrokesInPowerPoint", CardAutoSaveStrokesInPowerPoint.IsOn);
        }

        private void ToggleSwitchNotifyPreviousPage_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchNotifyPreviousPage", CardNotifyPreviousPage.IsOn);
        }

        private void ToggleSwitchAlwaysGoToFirstPageOnReenter_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchAlwaysGoToFirstPageOnReenter", CardAlwaysGoToFirstPageOnReenter.IsOn);
        }

        private void ToggleSwitchNotifyHiddenPage_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchNotifyHiddenPage", CardNotifyHiddenPage.IsOn);
        }

        private void ToggleSwitchNotifyAutoPlayPresentation_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            MainWindowSettingsHelper.InvokeToggleSwitchToggled("ToggleSwitchNotifyAutoPlayPresentation", CardNotifyAutoPlayPresentation.IsOn);
        }
    }
}
