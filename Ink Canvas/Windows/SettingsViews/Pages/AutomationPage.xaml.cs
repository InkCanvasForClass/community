using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class AutomationPage : iNKORE.UI.WPF.Modern.Controls.Page
    {
        private bool _isLoaded = false;

        public AutomationPage()
        {
            InitializeComponent();
            Loaded += AutomationPage_Loaded;
        }

        private void AutomationPage_Loaded(object sender, RoutedEventArgs e)
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
                if (settings.Automation != null)
                {
                    CardAutoFoldInEasiNote.IsOn = settings.Automation.IsAutoFoldInEasiNote;
                    CardAutoFoldInEasiNote3.IsOn = settings.Automation.IsAutoFoldInEasiNote3;
                    CardAutoFoldInEasiNote3C.IsOn = settings.Automation.IsAutoFoldInEasiNote3C;
                    CardAutoFoldInEasiNote5C.IsOn = settings.Automation.IsAutoFoldInEasiNote5C;
                    CardAutoFoldInSeewoPincoTeacher.IsOn = settings.Automation.IsAutoFoldInSeewoPincoTeacher;
                    CardAutoFoldInEasiCamera.IsOn = settings.Automation.IsAutoFoldInEasiCamera;
                    CardAutoFoldInHiteTouchPro.IsOn = settings.Automation.IsAutoFoldInHiteTouchPro;
                    CardAutoFoldInHiteCamera.IsOn = settings.Automation.IsAutoFoldInHiteCamera;
                    CardAutoFoldInHiteLightBoard.IsOn = settings.Automation.IsAutoFoldInHiteLightBoard;
                    CardAutoFoldInWxBoardMain.IsOn = settings.Automation.IsAutoFoldInWxBoardMain;
                    CardAutoFoldInMSWhiteboard.IsOn = settings.Automation.IsAutoFoldInMSWhiteboard;
                    CardAutoFoldInAdmoxWhiteboard.IsOn = settings.Automation.IsAutoFoldInAdmoxWhiteboard;
                    CardAutoFoldInAdmoxBooth.IsOn = settings.Automation.IsAutoFoldInAdmoxBooth;
                    CardAutoFoldInQPoint.IsOn = settings.Automation.IsAutoFoldInQPoint;
                    CardAutoFoldInYiYunVisualPresenter.IsOn = settings.Automation.IsAutoFoldInYiYunVisualPresenter;
                    CardAutoFoldInMaxHubWhiteboard.IsOn = settings.Automation.IsAutoFoldInMaxHubWhiteboard;
                    CardAutoFoldInOldZyBoard.IsOn = settings.Automation.IsAutoFoldInOldZyBoard;
                    CardAutoFoldInPPTSlideShow.IsOn = settings.Automation.IsAutoFoldInPPTSlideShow;
                    CardAutoFoldAfterPPTSlideShow.IsOn = settings.Automation.IsAutoFoldAfterPPTSlideShow;
                    CardKeepFoldAfterSoftwareExit.IsOn = settings.Automation.KeepFoldAfterSoftwareExit;
                    CardAutoEnterAnnotationModeWhenExitFoldMode.IsOn = settings.Automation.IsAutoEnterAnnotationModeWhenExitFoldMode;
                    CardAutoFoldWhenExitWhiteboard.IsOn = settings.Automation.IsAutoFoldWhenExitWhiteboard;
                    CardAutoSaveStrokesAtClear.IsOn = settings.Automation.IsAutoSaveStrokesAtClear;
                    CardSaveScreenshotsInDateFolders.IsOn = settings.Automation.IsSaveScreenshotsInDateFolders;
                    CardAutoSaveStrokesAtScreenshot.IsOn = settings.Automation.IsAutoSaveStrokesAtScreenshot;
                    CardEnableAutoSaveStrokes.IsOn = settings.Automation.IsEnableAutoSaveStrokes;
                    CardAutoDelSavedFiles.IsOn = settings.Automation.AutoDelSavedFiles;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载自动化设置时出错: {ex.Message}");
            }

            _isLoaded = true;
        }

        private void AutoFold_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            try
            {
                var settings = SettingsManager.Settings.Automation;
                settings.IsAutoFoldInEasiNote = CardAutoFoldInEasiNote.IsOn;
                settings.IsAutoFoldInEasiNote3 = CardAutoFoldInEasiNote3.IsOn;
                settings.IsAutoFoldInEasiNote3C = CardAutoFoldInEasiNote3C.IsOn;
                settings.IsAutoFoldInEasiNote5C = CardAutoFoldInEasiNote5C.IsOn;
                settings.IsAutoFoldInSeewoPincoTeacher = CardAutoFoldInSeewoPincoTeacher.IsOn;
                settings.IsAutoFoldInEasiCamera = CardAutoFoldInEasiCamera.IsOn;
                settings.IsAutoFoldInHiteTouchPro = CardAutoFoldInHiteTouchPro.IsOn;
                settings.IsAutoFoldInHiteCamera = CardAutoFoldInHiteCamera.IsOn;
                settings.IsAutoFoldInHiteLightBoard = CardAutoFoldInHiteLightBoard.IsOn;
                settings.IsAutoFoldInWxBoardMain = CardAutoFoldInWxBoardMain.IsOn;
                settings.IsAutoFoldInMSWhiteboard = CardAutoFoldInMSWhiteboard.IsOn;
                settings.IsAutoFoldInAdmoxWhiteboard = CardAutoFoldInAdmoxWhiteboard.IsOn;
                settings.IsAutoFoldInAdmoxBooth = CardAutoFoldInAdmoxBooth.IsOn;
                settings.IsAutoFoldInQPoint = CardAutoFoldInQPoint.IsOn;
                settings.IsAutoFoldInYiYunVisualPresenter = CardAutoFoldInYiYunVisualPresenter.IsOn;
                settings.IsAutoFoldInMaxHubWhiteboard = CardAutoFoldInMaxHubWhiteboard.IsOn;
                settings.IsAutoFoldInOldZyBoard = CardAutoFoldInOldZyBoard.IsOn;
                settings.IsAutoFoldInPPTSlideShow = CardAutoFoldInPPTSlideShow.IsOn;
                settings.IsAutoFoldAfterPPTSlideShow = CardAutoFoldAfterPPTSlideShow.IsOn;
                settings.KeepFoldAfterSoftwareExit = CardKeepFoldAfterSoftwareExit.IsOn;
                settings.IsAutoEnterAnnotationModeWhenExitFoldMode = CardAutoEnterAnnotationModeWhenExitFoldMode.IsOn;
                settings.IsAutoFoldWhenExitWhiteboard = CardAutoFoldWhenExitWhiteboard.IsOn;
                SettingsManager.SaveSettingsToFile();
                MainWindowSettingsHelper.InvokeMainWindowMethod("StartOrStoptimerCheckAutoFold");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置自动收纳时出错: {ex.Message}");
            }
        }

        private void AutoSave_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            try
            {
                var settings = SettingsManager.Settings.Automation;
                settings.IsAutoSaveStrokesAtClear = CardAutoSaveStrokesAtClear.IsOn;
                settings.IsSaveScreenshotsInDateFolders = CardSaveScreenshotsInDateFolders.IsOn;
                settings.IsAutoSaveStrokesAtScreenshot = CardAutoSaveStrokesAtScreenshot.IsOn;
                settings.IsEnableAutoSaveStrokes = CardEnableAutoSaveStrokes.IsOn;
                settings.AutoDelSavedFiles = CardAutoDelSavedFiles.IsOn;
                SettingsManager.SaveSettingsToFile();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置自动保存时出错: {ex.Message}");
            }
        }
    }
}
