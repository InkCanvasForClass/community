using Ink_Canvas.Helpers;
using Ink_Canvas.Properties;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Windows;
using System.Windows.Controls;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class AutomationPage : Page
    {
        private bool _isLoaded = false;

        public AutomationPage()
        {
            InitializeComponent();
            Loaded += AutomationPage_Loaded;
            Unloaded += AutomationPage_Unloaded;
        }

        private void AutomationPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
            _isLoaded = true;
            UpdateFileAssociationStatus();
            SliderTouchHelper.AddTouchSupportToAllSliders(this);
        }

        private void AutomationPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = false;
        }

        private MainWindow GetMainWindow() => Application.Current.MainWindow as MainWindow;

        private void LoadSettings()
        {
            _isLoaded = false;
            var auto = SettingsManager.Settings.Automation;

            CardAutoFoldInEasiNote.IsOn = auto.IsAutoFoldInEasiNote;
            CardAutoFoldInEasiCamera.IsOn = auto.IsAutoFoldInEasiCamera;
            CardAutoFoldInEasiNote3.IsOn = auto.IsAutoFoldInEasiNote3;
            CardAutoFoldInEasiNote3C.IsOn = auto.IsAutoFoldInEasiNote3C;
            CardAutoFoldInEasiNote5C.IsOn = auto.IsAutoFoldInEasiNote5C;
            CardAutoFoldInSeewoPincoTeacher.IsOn = auto.IsAutoFoldInSeewoPincoTeacher;
            CardAutoFoldInHiteTouchPro.IsOn = auto.IsAutoFoldInHiteTouchPro;
            CardAutoFoldInHiteLightBoard.IsOn = auto.IsAutoFoldInHiteLightBoard;
            CardAutoFoldInHiteCamera.IsOn = auto.IsAutoFoldInHiteCamera;
            CardAutoFoldInWxBoardMain.IsOn = auto.IsAutoFoldInWxBoardMain;
            CardAutoFoldInOldZyBoard.IsOn = auto.IsAutoFoldInOldZyBoard;
            CardAutoFoldInMSWhiteboard.IsOn = auto.IsAutoFoldInMSWhiteboard;
            CardAutoFoldInAdmoxWhiteboard.IsOn = auto.IsAutoFoldInAdmoxWhiteboard;
            CardAutoFoldInAdmoxBooth.IsOn = auto.IsAutoFoldInAdmoxBooth;
            CardAutoFoldInQPoint.IsOn = auto.IsAutoFoldInQPoint;
            CardAutoFoldInYiYunVisualPresenter.IsOn = auto.IsAutoFoldInYiYunVisualPresenter;
            CardAutoFoldInMaxHubWhiteboard.IsOn = auto.IsAutoFoldInMaxHubWhiteboard;
            CardAutoFoldInPPTSlideShow.IsOn = auto.IsAutoFoldInPPTSlideShow;

            CardAutoKillPPTService.IsOn = auto.IsAutoKillPPTService;
            CardAutoKillEasiNote.IsOn = auto.IsAutoKillEasiNote;
            CardAutoKillHiteAnnotation.IsOn = auto.IsAutoKillHiteAnnotation;
            CardAutoKillVComYouJiao.IsOn = auto.IsAutoKillVComYouJiao;
            CardAutoKillSeewoLauncher2DesktopAnnotation.IsOn = auto.IsAutoKillSeewoLauncher2DesktopAnnotation;
            CardAutoKillInkCanvas.IsOn = auto.IsAutoKillInkCanvas;
            CardAutoKillICA.IsOn = auto.IsAutoKillICA;
            CardAutoKillIDT.IsOn = auto.IsAutoKillIDT;
            CardAutoEnterAnnotationAfterKillHite.IsOn = auto.IsAutoEnterAnnotationAfterKillHite;

            CardAutoEnterAnnotationModeWhenExitFoldMode.IsOn = auto.IsAutoEnterAnnotationModeWhenExitFoldMode;
            CardAutoFoldWhenExitWhiteboard.IsOn = auto.IsAutoFoldWhenExitWhiteboard;
            CardAutoFoldAfterPPTSlideShow.IsOn = auto.IsAutoFoldAfterPPTSlideShow;
            CardKeepFoldAfterSoftwareExit.IsOn = auto.KeepFoldAfterSoftwareExit;

            CardSaveScreenshotsInDateFolders.IsOn = auto.IsSaveScreenshotsInDateFolders;
            CardAutoSaveStrokesAtScreenshot.IsOn = auto.IsAutoSaveStrokesAtScreenshot;
            CardAutoSaveStrokesAtClear.IsOn = auto.IsAutoSaveScreenshotAtClear;
            CardSaveStrokesAsXML.IsOn = auto.IsSaveStrokesAsXML;
            CardSaveStrokesAsUInk.IsOn = auto.IsSaveStrokesAsUInK;
            CardEnableAutoSaveStrokes.IsOn = auto.IsEnableAutoSaveStrokes;

            var interval = auto.AutoSaveStrokesIntervalMinutes;
            foreach (ComboBoxItem item in ComboBoxAutoSaveStrokesInterval.Items)
            {
                if (item.Tag != null && int.TryParse(item.Tag.ToString(), out int tagVal) && tagVal == interval)
                {
                    ComboBoxAutoSaveStrokesInterval.SelectedItem = item;
                    break;
                }
            }

            CardAutoDelSavedFiles.IsOn = auto.AutoDelSavedFiles;
            ComboBoxAutoDelSavedFilesDaysThreshold.SelectedIndex = auto.AutoDelSavedFilesDaysThreshold switch
            {
                7 => 0,
                14 => 1,
                30 => 2,
                60 => 3,
                90 => 4,
                _ => 2
            };

            SideControlMinimumAutomationSlider.Value = auto.MinimumAutomationStrokeNumber;
            CardSaveFullPageStrokes.IsOn = auto.IsSaveFullPageStrokes;

            CardUseCustomSaveFileName.IsOn = auto.IsUseCustomSaveFileName;
            TextBoxCustomSaveFileNameTemplate.Text = auto.CustomSaveFileNameTemplate;
            SyncSaveFileNamePresetSelection(auto.CustomSaveFileNameTemplate);

            if (auto.FloatingWindowInterceptor.InterceptRules != null)
            {
                ToggleSwitchSeewoWhiteboard3Floating.IsOn = auto.FloatingWindowInterceptor.InterceptRules.ContainsKey("SeewoWhiteboard3Floating") && auto.FloatingWindowInterceptor.InterceptRules["SeewoWhiteboard3Floating"];
                ToggleSwitchSeewoWhiteboard5Floating.IsOn = auto.FloatingWindowInterceptor.InterceptRules.ContainsKey("SeewoWhiteboard5Floating") && auto.FloatingWindowInterceptor.InterceptRules["SeewoWhiteboard5Floating"];
                ToggleSwitchSeewoWhiteboard5CFloating.IsOn = auto.FloatingWindowInterceptor.InterceptRules.ContainsKey("SeewoWhiteboard5CFloating") && auto.FloatingWindowInterceptor.InterceptRules["SeewoWhiteboard5CFloating"];
                ToggleSwitchSeewoPincoSideBarFloating.IsOn = auto.FloatingWindowInterceptor.InterceptRules.ContainsKey("SeewoPincoSideBarFloating") && auto.FloatingWindowInterceptor.InterceptRules["SeewoPincoSideBarFloating"];
                ToggleSwitchSeewoPincoDrawingFloating.IsOn = auto.FloatingWindowInterceptor.InterceptRules.ContainsKey("SeewoPincoDrawingFloating") && auto.FloatingWindowInterceptor.InterceptRules["SeewoPincoDrawingFloating"];
                ToggleSwitchSeewoPPTFloating.IsOn = auto.FloatingWindowInterceptor.InterceptRules.ContainsKey("SeewoPPTFloating") && auto.FloatingWindowInterceptor.InterceptRules["SeewoPPTFloating"];
                ToggleSwitchAiClassFloating.IsOn = auto.FloatingWindowInterceptor.InterceptRules.ContainsKey("AiClassFloating") && auto.FloatingWindowInterceptor.InterceptRules["AiClassFloating"];
                ToggleSwitchHiteAnnotationFloating.IsOn = auto.FloatingWindowInterceptor.InterceptRules.ContainsKey("HiteAnnotationFloating") && auto.FloatingWindowInterceptor.InterceptRules["HiteAnnotationFloating"];
                ToggleSwitchChangYanFloating.IsOn = auto.FloatingWindowInterceptor.InterceptRules.ContainsKey("ChangYanFloating") && auto.FloatingWindowInterceptor.InterceptRules["ChangYanFloating"];
                ToggleSwitchChangYanPPTFloating.IsOn = auto.FloatingWindowInterceptor.InterceptRules.ContainsKey("ChangYanPPTFloating") && auto.FloatingWindowInterceptor.InterceptRules["ChangYanPPTFloating"];
                ToggleSwitchIntelligentClassFloating.IsOn = auto.FloatingWindowInterceptor.InterceptRules.ContainsKey("IntelligentClassFloating") && auto.FloatingWindowInterceptor.InterceptRules["IntelligentClassFloating"];
                ToggleSwitchSeewoDesktopAnnotationFloating.IsOn = auto.FloatingWindowInterceptor.InterceptRules.ContainsKey("SeewoDesktopAnnotationFloating") && auto.FloatingWindowInterceptor.InterceptRules["SeewoDesktopAnnotationFloating"];
                ToggleSwitchSeewoDesktopSideBarFloating.IsOn = auto.FloatingWindowInterceptor.InterceptRules.ContainsKey("SeewoDesktopSideBarFloating") && auto.FloatingWindowInterceptor.InterceptRules["SeewoDesktopSideBarFloating"];
            }

            UpdateFloatingWindowInterceptorEnabled();

            _isLoaded = true;
        }

        #region AutoFold

        private void ToggleSwitchAutoFoldInEasiNote_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Automation.IsAutoFoldInEasiNote = CardAutoFoldInEasiNote.IsOn;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnAutoFoldChanged();
        }

        private void ToggleSwitchAutoFoldInEasiCamera_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Automation.IsAutoFoldInEasiCamera = CardAutoFoldInEasiCamera.IsOn;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnAutoFoldChanged();
        }

        private void ToggleSwitchAutoFoldInEasiNote3_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Automation.IsAutoFoldInEasiNote3 = CardAutoFoldInEasiNote3.IsOn;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnAutoFoldChanged();
        }

        private void ToggleSwitchAutoFoldInEasiNote3C_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Automation.IsAutoFoldInEasiNote3C = CardAutoFoldInEasiNote3C.IsOn;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnAutoFoldChanged();
        }

        private void ToggleSwitchAutoFoldInEasiNote5C_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Automation.IsAutoFoldInEasiNote5C = CardAutoFoldInEasiNote5C.IsOn;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnAutoFoldChanged();
        }

        private void ToggleSwitchAutoFoldInSeewoPincoTeacher_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Automation.IsAutoFoldInSeewoPincoTeacher = CardAutoFoldInSeewoPincoTeacher.IsOn;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnAutoFoldChanged();
        }

        private void ToggleSwitchAutoFoldInHiteTouchPro_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Automation.IsAutoFoldInHiteTouchPro = CardAutoFoldInHiteTouchPro.IsOn;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnAutoFoldChanged();
        }

        private void ToggleSwitchAutoFoldInHiteLightBoard_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Automation.IsAutoFoldInHiteLightBoard = CardAutoFoldInHiteLightBoard.IsOn;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnAutoFoldChanged();
        }

        private void ToggleSwitchAutoFoldInHiteCamera_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Automation.IsAutoFoldInHiteCamera = CardAutoFoldInHiteCamera.IsOn;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnAutoFoldChanged();
        }

        private void ToggleSwitchAutoFoldInWxBoardMain_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Automation.IsAutoFoldInWxBoardMain = CardAutoFoldInWxBoardMain.IsOn;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnAutoFoldChanged();
        }

        private void ToggleSwitchAutoFoldInOldZyBoard_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Automation.IsAutoFoldInOldZyBoard = CardAutoFoldInOldZyBoard.IsOn;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnAutoFoldChanged();
        }

        private void ToggleSwitchAutoFoldInMSWhiteboard_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Automation.IsAutoFoldInMSWhiteboard = CardAutoFoldInMSWhiteboard.IsOn;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnAutoFoldChanged();
        }

        private void ToggleSwitchAutoFoldInAdmoxWhiteboard_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Automation.IsAutoFoldInAdmoxWhiteboard = CardAutoFoldInAdmoxWhiteboard.IsOn;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnAutoFoldChanged();
        }

        private void ToggleSwitchAutoFoldInAdmoxBooth_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Automation.IsAutoFoldInAdmoxBooth = CardAutoFoldInAdmoxBooth.IsOn;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnAutoFoldChanged();
        }

        private void ToggleSwitchAutoFoldInQPoint_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Automation.IsAutoFoldInQPoint = CardAutoFoldInQPoint.IsOn;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnAutoFoldChanged();
        }

        private void ToggleSwitchAutoFoldInYiYunVisualPresenter_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Automation.IsAutoFoldInYiYunVisualPresenter = CardAutoFoldInYiYunVisualPresenter.IsOn;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnAutoFoldChanged();
        }

        private void ToggleSwitchAutoFoldInMaxHubWhiteboard_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Automation.IsAutoFoldInMaxHubWhiteboard = CardAutoFoldInMaxHubWhiteboard.IsOn;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnAutoFoldChanged();
        }

        private void ToggleSwitchAutoFoldInPPTSlideShow_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var auto = SettingsManager.Settings.Automation;
            bool previousState = auto.IsAutoFoldInPPTSlideShow;
            auto.IsAutoFoldInPPTSlideShow = CardAutoFoldInPPTSlideShow.IsOn;
            if (previousState != auto.IsAutoFoldInPPTSlideShow)
            {
                LogHelper.WriteLogToFile($"PPT自动收纳设置已变更: {auto.IsAutoFoldInPPTSlideShow}", LogHelper.LogType.Trace);
            }
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnAutoFoldChanged();
        }

        #endregion

        #region AutoKill

        private void UpdateAutoKillTimer()
        {
            SettingsActionHub.OnAutoKillChanged();
        }

        private void ToggleSwitchAutoKillPPTService_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Automation.IsAutoKillPPTService = CardAutoKillPPTService.IsOn;
            SettingsManager.SaveSettingsToFile();
            UpdateAutoKillTimer();
        }

        private void ToggleSwitchAutoKillEasiNote_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Automation.IsAutoKillEasiNote = CardAutoKillEasiNote.IsOn;
            SettingsManager.SaveSettingsToFile();
            UpdateAutoKillTimer();
        }

        private void ToggleSwitchAutoKillHiteAnnotation_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Automation.IsAutoKillHiteAnnotation = CardAutoKillHiteAnnotation.IsOn;
            SettingsManager.SaveSettingsToFile();
            UpdateAutoKillTimer();
        }

        private void ToggleSwitchAutoKillVComYouJiao_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Automation.IsAutoKillVComYouJiao = CardAutoKillVComYouJiao.IsOn;
            SettingsManager.SaveSettingsToFile();
            UpdateAutoKillTimer();
        }

        private void ToggleSwitchAutoKillSeewoLauncher2DesktopAnnotation_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Automation.IsAutoKillSeewoLauncher2DesktopAnnotation = CardAutoKillSeewoLauncher2DesktopAnnotation.IsOn;
            SettingsManager.SaveSettingsToFile();
            UpdateAutoKillTimer();
        }

        private void ToggleSwitchAutoKillInkCanvas_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Automation.IsAutoKillInkCanvas = CardAutoKillInkCanvas.IsOn;
            SettingsManager.SaveSettingsToFile();
            UpdateAutoKillTimer();
        }

        private void ToggleSwitchAutoKillICA_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Automation.IsAutoKillICA = CardAutoKillICA.IsOn;
            SettingsManager.SaveSettingsToFile();
            UpdateAutoKillTimer();
        }

        private void ToggleSwitchAutoKillIDT_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Automation.IsAutoKillIDT = CardAutoKillIDT.IsOn;
            SettingsManager.SaveSettingsToFile();
            UpdateAutoKillTimer();
        }

        private void ToggleSwitchAutoEnterAnnotationAfterKillHite_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Automation.IsAutoEnterAnnotationAfterKillHite = CardAutoEnterAnnotationAfterKillHite.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        #endregion

        #region Fold Mode

        private void ToggleSwitchAutoEnterAnnotationModeWhenExitFoldMode_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Automation.IsAutoEnterAnnotationModeWhenExitFoldMode = CardAutoEnterAnnotationModeWhenExitFoldMode.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchAutoFoldWhenExitWhiteboard_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Automation.IsAutoFoldWhenExitWhiteboard = CardAutoFoldWhenExitWhiteboard.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchAutoFoldAfterPPTSlideShow_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Automation.IsAutoFoldAfterPPTSlideShow = CardAutoFoldAfterPPTSlideShow.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchKeepFoldAfterSoftwareExit_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Automation.KeepFoldAfterSoftwareExit = CardKeepFoldAfterSoftwareExit.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        #endregion

        #region Storage & Save

        private void ToggleSwitchSaveScreenshotsInDateFolders_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Automation.IsSaveScreenshotsInDateFolders = CardSaveScreenshotsInDateFolders.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchAutoSaveStrokesAtScreenshot_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Automation.IsAutoSaveStrokesAtScreenshot = CardAutoSaveStrokesAtScreenshot.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchAutoSaveStrokesAtClear_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Automation.IsAutoSaveScreenshotAtClear = CardAutoSaveStrokesAtClear.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchSaveStrokesAsXML_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Automation.IsSaveStrokesAsXML = CardSaveStrokesAsXML.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchSaveStrokesAsUInK_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Automation.IsSaveStrokesAsUInK = CardSaveStrokesAsUInk.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchEnableAutoSaveStrokes_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Automation.IsEnableAutoSaveStrokes = CardEnableAutoSaveStrokes.IsOn;
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnAutoSaveStrokesChanged();
        }

        private void ComboBoxAutoSaveStrokesInterval_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || ComboBoxAutoSaveStrokesInterval.SelectedItem == null) return;
            var selectedItem = ComboBoxAutoSaveStrokesInterval.SelectedItem as ComboBoxItem;
            if (selectedItem?.Tag != null && int.TryParse(selectedItem.Tag.ToString(), out int intervalMinutes))
            {
                SettingsManager.Settings.Automation.AutoSaveStrokesIntervalMinutes = intervalMinutes;
                SettingsManager.SaveSettingsToFile();
                SettingsActionHub.OnAutoSaveStrokesChanged();
            }
        }

        private void ToggleSwitchAutoDelSavedFiles_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Automation.AutoDelSavedFiles = CardAutoDelSavedFiles.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ComboBoxAutoDelSavedFilesDaysThreshold_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Automation.AutoDelSavedFilesDaysThreshold =
                int.Parse(((ComboBoxItem)ComboBoxAutoDelSavedFilesDaysThreshold.SelectedItem).Content.ToString());
            SettingsManager.SaveSettingsToFile();
        }

        private void SideControlMinimumAutomationSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Automation.MinimumAutomationStrokeNumber = (int)SideControlMinimumAutomationSlider.Value;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchSaveFullPageStrokes_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Automation.IsSaveFullPageStrokes = CardSaveFullPageStrokes.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchUseCustomSaveFileName_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Automation.IsUseCustomSaveFileName = CardUseCustomSaveFileName.IsOn;
            SettingsManager.SaveSettingsToFile();
        }

        private void TextBoxCustomSaveFileNameTemplate_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Automation.CustomSaveFileNameTemplate = TextBoxCustomSaveFileNameTemplate.Text;
            SettingsManager.SaveSettingsToFile();
        }

        private void ComboBoxSaveFileNamePreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || ComboBoxSaveFileNamePreset.SelectedItem == null) return;
            var item = ComboBoxSaveFileNamePreset.SelectedItem as ComboBoxItem;
            var tag = item?.Tag?.ToString();
            if (string.IsNullOrEmpty(tag)) return;

            if (tag == "__custom__")
            {
                CardCustomSaveFileNameTemplate.Visibility = Visibility.Visible;
                return;
            }

            CardCustomSaveFileNameTemplate.Visibility = Visibility.Collapsed;
            SettingsManager.Settings.Automation.CustomSaveFileNameTemplate = tag;
            TextBoxCustomSaveFileNameTemplate.Text = tag;
            SettingsManager.SaveSettingsToFile();
        }

        private void SyncSaveFileNamePresetSelection(string template)
        {
            int matchedIndex = -1;
            for (int i = 0; i < ComboBoxSaveFileNamePreset.Items.Count; i++)
            {
                var item = ComboBoxSaveFileNamePreset.Items[i] as ComboBoxItem;
                var tag = item?.Tag?.ToString();
                if (tag == template)
                {
                    matchedIndex = i;
                    break;
                }
            }

            if (matchedIndex >= 0)
            {
                ComboBoxSaveFileNamePreset.SelectedIndex = matchedIndex;
                CardCustomSaveFileNameTemplate.Visibility = Visibility.Collapsed;
            }
            else
            {
                ComboBoxSaveFileNamePreset.SelectedIndex = ComboBoxSaveFileNamePreset.Items.Count - 1;
                CardCustomSaveFileNameTemplate.Visibility = Visibility.Visible;
            }
        }

        #endregion

        #region Floating Window Interceptor

        private void UpdateFloatingWindowInterceptorEnabled()
        {
            var auto = SettingsManager.Settings.Automation;
            bool anyOn = ToggleSwitchSeewoWhiteboard3Floating.IsOn
                || ToggleSwitchSeewoWhiteboard5Floating.IsOn
                || ToggleSwitchSeewoWhiteboard5CFloating.IsOn
                || ToggleSwitchSeewoPincoSideBarFloating.IsOn
                || ToggleSwitchSeewoPincoDrawingFloating.IsOn
                || ToggleSwitchSeewoPPTFloating.IsOn
                || ToggleSwitchAiClassFloating.IsOn
                || ToggleSwitchHiteAnnotationFloating.IsOn
                || ToggleSwitchChangYanFloating.IsOn
                || ToggleSwitchChangYanPPTFloating.IsOn
                || ToggleSwitchIntelligentClassFloating.IsOn
                || ToggleSwitchSeewoDesktopAnnotationFloating.IsOn
                || ToggleSwitchSeewoDesktopSideBarFloating.IsOn;
            auto.FloatingWindowInterceptor.IsEnabled = anyOn;
            SettingsActionHub.OnFloatingWindowInterceptorEnabledCheck(anyOn);
            SettingsManager.SaveSettingsToFile();
        }

        private void ToggleSwitchSeewoWhiteboard3Floating_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsActionHub.OnFloatingWindowInterceptorRuleChanged("SeewoWhiteboard3Floating", ToggleSwitchSeewoWhiteboard3Floating.IsOn);
            UpdateFloatingWindowInterceptorEnabled();
        }

        private void ToggleSwitchSeewoWhiteboard5Floating_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsActionHub.OnFloatingWindowInterceptorRuleChanged("SeewoWhiteboard5Floating", ToggleSwitchSeewoWhiteboard5Floating.IsOn);
            UpdateFloatingWindowInterceptorEnabled();
        }

        private void ToggleSwitchSeewoWhiteboard5CFloating_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsActionHub.OnFloatingWindowInterceptorRuleChanged("SeewoWhiteboard5CFloating", ToggleSwitchSeewoWhiteboard5CFloating.IsOn);
            UpdateFloatingWindowInterceptorEnabled();
        }

        private void ToggleSwitchSeewoPincoSideBarFloating_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsActionHub.OnFloatingWindowInterceptorRuleChanged("SeewoPincoSideBarFloating", ToggleSwitchSeewoPincoSideBarFloating.IsOn);
            UpdateFloatingWindowInterceptorEnabled();
        }

        private void ToggleSwitchSeewoPincoDrawingFloating_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsActionHub.OnFloatingWindowInterceptorRuleChanged("SeewoPincoDrawingFloating", ToggleSwitchSeewoPincoDrawingFloating.IsOn);
            UpdateFloatingWindowInterceptorEnabled();
        }

        private void ToggleSwitchSeewoPPTFloating_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsActionHub.OnFloatingWindowInterceptorRuleChanged("SeewoPPTFloating", ToggleSwitchSeewoPPTFloating.IsOn);
            UpdateFloatingWindowInterceptorEnabled();
        }

        private void ToggleSwitchAiClassFloating_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsActionHub.OnFloatingWindowInterceptorRuleChanged("AiClassFloating", ToggleSwitchAiClassFloating.IsOn);
            UpdateFloatingWindowInterceptorEnabled();
        }

        private void ToggleSwitchHiteAnnotationFloating_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsActionHub.OnFloatingWindowInterceptorRuleChanged("HiteAnnotationFloating", ToggleSwitchHiteAnnotationFloating.IsOn);
            UpdateFloatingWindowInterceptorEnabled();
        }

        private void ToggleSwitchChangYanFloating_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsActionHub.OnFloatingWindowInterceptorRuleChanged("ChangYanFloating", ToggleSwitchChangYanFloating.IsOn);
            UpdateFloatingWindowInterceptorEnabled();
        }

        private void ToggleSwitchChangYanPPTFloating_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsActionHub.OnFloatingWindowInterceptorRuleChanged("ChangYanPPTFloating", ToggleSwitchChangYanPPTFloating.IsOn);
            UpdateFloatingWindowInterceptorEnabled();
        }

        private void ToggleSwitchIntelligentClassFloating_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsActionHub.OnFloatingWindowInterceptorRuleChanged("IntelligentClassFloating", ToggleSwitchIntelligentClassFloating.IsOn);
            UpdateFloatingWindowInterceptorEnabled();
        }

        private void ToggleSwitchSeewoDesktopAnnotationFloating_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsActionHub.OnFloatingWindowInterceptorRuleChanged("SeewoDesktopAnnotationFloating", ToggleSwitchSeewoDesktopAnnotationFloating.IsOn);
            UpdateFloatingWindowInterceptorEnabled();
        }

        private void ToggleSwitchSeewoDesktopSideBarFloating_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsActionHub.OnFloatingWindowInterceptorRuleChanged("SeewoDesktopSideBarFloating", ToggleSwitchSeewoDesktopSideBarFloating.IsOn);
            UpdateFloatingWindowInterceptorEnabled();
        }

        #endregion

        #region File Association

        private void BtnRegisterFileAssociation_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool success = FileAssociationManager.RegisterFileAssociation();
                UpdateFileAssociationStatus();
                var mw = GetMainWindow();
                if (mw != null)
                    mw.ShowNotification(success ? AutomationStrings.FileAssoc_RegisterSuccess : AutomationStrings.FileAssoc_RegisterFailed);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"注册文件关联失败: {ex.Message}", LogHelper.LogType.Error);
                UpdateFileAssociationStatus();
            }
        }

        private void BtnUnregisterFileAssociation_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool success = FileAssociationManager.UnregisterFileAssociation();
                UpdateFileAssociationStatus();
                var mw = GetMainWindow();
                if (mw != null)
                    mw.ShowNotification(success ? AutomationStrings.FileAssoc_UnregisterSuccess : AutomationStrings.FileAssoc_UnregisterFailed);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"取消文件关联失败: {ex.Message}", LogHelper.LogType.Error);
                UpdateFileAssociationStatus();
            }
        }

        private void BtnCheckFileAssociation_Click(object sender, RoutedEventArgs e)
        {
            UpdateFileAssociationStatus();
        }

        private void UpdateFileAssociationStatus()
        {
            try
            {
                bool isRegistered = FileAssociationManager.IsFileAssociationRegistered();
                if (isRegistered)
                {
                    TextBlockFileAssociationStatus.Text = AutomationStrings.FileAssoc_Registered;
                    TextBlockFileAssociationStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightGreen);
                }
                else
                {
                    TextBlockFileAssociationStatus.Text = AutomationStrings.FileAssoc_NotRegistered;
                    TextBlockFileAssociationStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightCoral);
                }
            }
            catch (Exception ex)
            {
                TextBlockFileAssociationStatus.Text = AutomationStrings.FileAssoc_CheckError;
                TextBlockFileAssociationStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightCoral);
                LogHelper.WriteLogToFile($"检查文件关联状态失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        #endregion

        #region Custom Automation

        private void CardCustomAutomation_Click(object sender, RoutedEventArgs e)
        {
            // 导航到自定义自动化页面
            var settingsWindow = Window.GetWindow(this) as Windows.SettingsViews.SettingsWindow;
            if (settingsWindow != null)
            {
                settingsWindow.NavigateToPage("AutomationWorkflowPage");
            }
        }

        #endregion
    }
}
