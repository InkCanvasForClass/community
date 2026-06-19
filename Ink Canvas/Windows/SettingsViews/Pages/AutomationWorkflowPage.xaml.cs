using Ink_Canvas.Helpers;
using Ink_Canvas.Properties;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using Ink_Canvas.WorkflowAutomation;
using Ink_Canvas.WorkflowAutomation.Enums;
using Ink_Canvas.WorkflowAutomation.Models;
using Ink_Canvas.WorkflowAutomation.Services;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class AutomationWorkflowPage : Page, INotifyPropertyChanged
    {
        private bool _isLoaded = false;
        private bool _isUpdatingEditor = false;
        private AutomationService Service => AutomationBootstrap.Service;

        // 静态属性供 XAML x:Static 绑定
        public static List<TriggerInfo> RegisteredTriggersList => AutomationRegistry.RegisteredTriggers;
        public static List<ActionRegistryInfo> RegisteredActionsList =>
            AutomationRegistry.RegisteredActions.Values.ToList();
        public static List<RuleRegistryInfo> RegisteredRulesList =>
            AutomationRegistry.RegisteredRules.Values.ToList();

        public AutomationWorkflowPage()
        {
            DataContext = this;
            InitializeComponent();
            Loaded += AutomationWorkflowPage_Loaded;
            Unloaded += AutomationWorkflowPage_Unloaded;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void AutomationWorkflowPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadPresetSettings();
            _isLoaded = true;
            UpdateFileAssociationStatus();
            SliderTouchHelper.AddTouchSupportToAllSliders(this);

            RefreshWorkflowList();

            // Default to preset panel
            NavigationListBox.SelectedIndex = 0;
        }

        private void AutomationWorkflowPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = false;
        }

        private MainWindow GetMainWindow() => Application.Current.MainWindow as MainWindow;

        #region Navigation

        private void NavigationListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;

            SelectedWorkflow = NavigationListBox.SelectedItem as Workflow;

            if (NavigationListBox.SelectedItem == NavPresetItem)
            {
                PresetPanel.Visibility = Visibility.Visible;
                WorkflowEditorPanel.Visibility = Visibility.Collapsed;
                EmptyStatePanel.Visibility = Visibility.Collapsed;
            }
            else if (NavigationListBox.SelectedItem is Workflow workflow)
            {
                PresetPanel.Visibility = Visibility.Collapsed;
                WorkflowEditorPanel.Visibility = Visibility.Visible;
                EmptyStatePanel.Visibility = Visibility.Collapsed;
                UpdateEditorBindings(workflow);
            }
            else
            {
                PresetPanel.Visibility = Visibility.Collapsed;
                WorkflowEditorPanel.Visibility = Visibility.Collapsed;
                EmptyStatePanel.Visibility = Visibility.Visible;
            }
        }

        private void RefreshWorkflowList()
        {
            // Remove old workflow items (keep NavPresetItem at index 0)
            while (NavigationListBox.Items.Count > 1)
                NavigationListBox.Items.RemoveAt(1);

            // Add workflow items
            foreach (var workflow in Service.Workflows)
            {
                NavigationListBox.Items.Add(workflow);
            }
        }

        #endregion

        #region Preset Settings

        private void LoadPresetSettings()
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
        { if (!_isLoaded) return; SettingsManager.Settings.Automation.IsAutoFoldInEasiNote = CardAutoFoldInEasiNote.IsOn; SettingsManager.SaveSettingsToFile(); SettingsActionHub.OnAutoFoldChanged(); }
        private void ToggleSwitchAutoFoldInEasiCamera_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsManager.Settings.Automation.IsAutoFoldInEasiCamera = CardAutoFoldInEasiCamera.IsOn; SettingsManager.SaveSettingsToFile(); SettingsActionHub.OnAutoFoldChanged(); }
        private void ToggleSwitchAutoFoldInEasiNote3_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsManager.Settings.Automation.IsAutoFoldInEasiNote3 = CardAutoFoldInEasiNote3.IsOn; SettingsManager.SaveSettingsToFile(); SettingsActionHub.OnAutoFoldChanged(); }
        private void ToggleSwitchAutoFoldInEasiNote3C_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsManager.Settings.Automation.IsAutoFoldInEasiNote3C = CardAutoFoldInEasiNote3C.IsOn; SettingsManager.SaveSettingsToFile(); SettingsActionHub.OnAutoFoldChanged(); }
        private void ToggleSwitchAutoFoldInEasiNote5C_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsManager.Settings.Automation.IsAutoFoldInEasiNote5C = CardAutoFoldInEasiNote5C.IsOn; SettingsManager.SaveSettingsToFile(); SettingsActionHub.OnAutoFoldChanged(); }
        private void ToggleSwitchAutoFoldInSeewoPincoTeacher_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsManager.Settings.Automation.IsAutoFoldInSeewoPincoTeacher = CardAutoFoldInSeewoPincoTeacher.IsOn; SettingsManager.SaveSettingsToFile(); SettingsActionHub.OnAutoFoldChanged(); }
        private void ToggleSwitchAutoFoldInHiteTouchPro_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsManager.Settings.Automation.IsAutoFoldInHiteTouchPro = CardAutoFoldInHiteTouchPro.IsOn; SettingsManager.SaveSettingsToFile(); SettingsActionHub.OnAutoFoldChanged(); }
        private void ToggleSwitchAutoFoldInHiteLightBoard_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsManager.Settings.Automation.IsAutoFoldInHiteLightBoard = CardAutoFoldInHiteLightBoard.IsOn; SettingsManager.SaveSettingsToFile(); SettingsActionHub.OnAutoFoldChanged(); }
        private void ToggleSwitchAutoFoldInHiteCamera_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsManager.Settings.Automation.IsAutoFoldInHiteCamera = CardAutoFoldInHiteCamera.IsOn; SettingsManager.SaveSettingsToFile(); SettingsActionHub.OnAutoFoldChanged(); }
        private void ToggleSwitchAutoFoldInWxBoardMain_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsManager.Settings.Automation.IsAutoFoldInWxBoardMain = CardAutoFoldInWxBoardMain.IsOn; SettingsManager.SaveSettingsToFile(); SettingsActionHub.OnAutoFoldChanged(); }
        private void ToggleSwitchAutoFoldInOldZyBoard_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsManager.Settings.Automation.IsAutoFoldInOldZyBoard = CardAutoFoldInOldZyBoard.IsOn; SettingsManager.SaveSettingsToFile(); SettingsActionHub.OnAutoFoldChanged(); }
        private void ToggleSwitchAutoFoldInMSWhiteboard_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsManager.Settings.Automation.IsAutoFoldInMSWhiteboard = CardAutoFoldInMSWhiteboard.IsOn; SettingsManager.SaveSettingsToFile(); SettingsActionHub.OnAutoFoldChanged(); }
        private void ToggleSwitchAutoFoldInAdmoxWhiteboard_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsManager.Settings.Automation.IsAutoFoldInAdmoxWhiteboard = CardAutoFoldInAdmoxWhiteboard.IsOn; SettingsManager.SaveSettingsToFile(); SettingsActionHub.OnAutoFoldChanged(); }
        private void ToggleSwitchAutoFoldInAdmoxBooth_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsManager.Settings.Automation.IsAutoFoldInAdmoxBooth = CardAutoFoldInAdmoxBooth.IsOn; SettingsManager.SaveSettingsToFile(); SettingsActionHub.OnAutoFoldChanged(); }
        private void ToggleSwitchAutoFoldInQPoint_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsManager.Settings.Automation.IsAutoFoldInQPoint = CardAutoFoldInQPoint.IsOn; SettingsManager.SaveSettingsToFile(); SettingsActionHub.OnAutoFoldChanged(); }
        private void ToggleSwitchAutoFoldInYiYunVisualPresenter_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsManager.Settings.Automation.IsAutoFoldInYiYunVisualPresenter = CardAutoFoldInYiYunVisualPresenter.IsOn; SettingsManager.SaveSettingsToFile(); SettingsActionHub.OnAutoFoldChanged(); }
        private void ToggleSwitchAutoFoldInMaxHubWhiteboard_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsManager.Settings.Automation.IsAutoFoldInMaxHubWhiteboard = CardAutoFoldInMaxHubWhiteboard.IsOn; SettingsManager.SaveSettingsToFile(); SettingsActionHub.OnAutoFoldChanged(); }

        private void ToggleSwitchAutoFoldInPPTSlideShow_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            var auto = SettingsManager.Settings.Automation;
            bool previousState = auto.IsAutoFoldInPPTSlideShow;
            auto.IsAutoFoldInPPTSlideShow = CardAutoFoldInPPTSlideShow.IsOn;
            if (previousState != auto.IsAutoFoldInPPTSlideShow)
                LogHelper.WriteLogToFile($"PPT自动收纳设置已变更: {auto.IsAutoFoldInPPTSlideShow}", LogHelper.LogType.Trace);
            SettingsManager.SaveSettingsToFile();
            SettingsActionHub.OnAutoFoldChanged();
        }

        #endregion

        #region AutoKill

        private void UpdateAutoKillTimer() => SettingsActionHub.OnAutoKillChanged();

        private void ToggleSwitchAutoKillPPTService_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsManager.Settings.Automation.IsAutoKillPPTService = CardAutoKillPPTService.IsOn; SettingsManager.SaveSettingsToFile(); UpdateAutoKillTimer(); }
        private void ToggleSwitchAutoKillEasiNote_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsManager.Settings.Automation.IsAutoKillEasiNote = CardAutoKillEasiNote.IsOn; SettingsManager.SaveSettingsToFile(); UpdateAutoKillTimer(); }
        private void ToggleSwitchAutoKillHiteAnnotation_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsManager.Settings.Automation.IsAutoKillHiteAnnotation = CardAutoKillHiteAnnotation.IsOn; SettingsManager.SaveSettingsToFile(); UpdateAutoKillTimer(); }
        private void ToggleSwitchAutoKillVComYouJiao_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsManager.Settings.Automation.IsAutoKillVComYouJiao = CardAutoKillVComYouJiao.IsOn; SettingsManager.SaveSettingsToFile(); UpdateAutoKillTimer(); }
        private void ToggleSwitchAutoKillSeewoLauncher2DesktopAnnotation_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsManager.Settings.Automation.IsAutoKillSeewoLauncher2DesktopAnnotation = CardAutoKillSeewoLauncher2DesktopAnnotation.IsOn; SettingsManager.SaveSettingsToFile(); UpdateAutoKillTimer(); }
        private void ToggleSwitchAutoKillInkCanvas_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsManager.Settings.Automation.IsAutoKillInkCanvas = CardAutoKillInkCanvas.IsOn; SettingsManager.SaveSettingsToFile(); UpdateAutoKillTimer(); }
        private void ToggleSwitchAutoKillICA_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsManager.Settings.Automation.IsAutoKillICA = CardAutoKillICA.IsOn; SettingsManager.SaveSettingsToFile(); UpdateAutoKillTimer(); }
        private void ToggleSwitchAutoKillIDT_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsManager.Settings.Automation.IsAutoKillIDT = CardAutoKillIDT.IsOn; SettingsManager.SaveSettingsToFile(); UpdateAutoKillTimer(); }
        private void ToggleSwitchAutoEnterAnnotationAfterKillHite_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsManager.Settings.Automation.IsAutoEnterAnnotationAfterKillHite = CardAutoEnterAnnotationAfterKillHite.IsOn; SettingsManager.SaveSettingsToFile(); }

        #endregion

        #region Fold Mode

        private void ToggleSwitchAutoEnterAnnotationModeWhenExitFoldMode_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsManager.Settings.Automation.IsAutoEnterAnnotationModeWhenExitFoldMode = CardAutoEnterAnnotationModeWhenExitFoldMode.IsOn; SettingsManager.SaveSettingsToFile(); }
        private void ToggleSwitchAutoFoldWhenExitWhiteboard_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsManager.Settings.Automation.IsAutoFoldWhenExitWhiteboard = CardAutoFoldWhenExitWhiteboard.IsOn; SettingsManager.SaveSettingsToFile(); }
        private void ToggleSwitchAutoFoldAfterPPTSlideShow_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsManager.Settings.Automation.IsAutoFoldAfterPPTSlideShow = CardAutoFoldAfterPPTSlideShow.IsOn; SettingsManager.SaveSettingsToFile(); }
        private void ToggleSwitchKeepFoldAfterSoftwareExit_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsManager.Settings.Automation.KeepFoldAfterSoftwareExit = CardKeepFoldAfterSoftwareExit.IsOn; SettingsManager.SaveSettingsToFile(); }

        #endregion

        #region Storage & Save

        private void ToggleSwitchSaveScreenshotsInDateFolders_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsManager.Settings.Automation.IsSaveScreenshotsInDateFolders = CardSaveScreenshotsInDateFolders.IsOn; SettingsManager.SaveSettingsToFile(); }
        private void ToggleSwitchAutoSaveStrokesAtScreenshot_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsManager.Settings.Automation.IsAutoSaveStrokesAtScreenshot = CardAutoSaveStrokesAtScreenshot.IsOn; SettingsManager.SaveSettingsToFile(); }
        private void ToggleSwitchAutoSaveStrokesAtClear_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsManager.Settings.Automation.IsAutoSaveScreenshotAtClear = CardAutoSaveStrokesAtClear.IsOn; SettingsManager.SaveSettingsToFile(); }
        private void ToggleSwitchSaveStrokesAsXML_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsManager.Settings.Automation.IsSaveStrokesAsXML = CardSaveStrokesAsXML.IsOn; SettingsManager.SaveSettingsToFile(); }
        private void ToggleSwitchEnableAutoSaveStrokes_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsManager.Settings.Automation.IsEnableAutoSaveStrokes = CardEnableAutoSaveStrokes.IsOn; SettingsManager.SaveSettingsToFile(); SettingsActionHub.OnAutoSaveStrokesChanged(); }

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
        { if (!_isLoaded) return; SettingsManager.Settings.Automation.AutoDelSavedFiles = CardAutoDelSavedFiles.IsOn; SettingsManager.SaveSettingsToFile(); }

        private void ComboBoxAutoDelSavedFilesDaysThreshold_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            SettingsManager.Settings.Automation.AutoDelSavedFilesDaysThreshold =
                int.Parse(((ComboBoxItem)ComboBoxAutoDelSavedFilesDaysThreshold.SelectedItem).Content.ToString());
            SettingsManager.SaveSettingsToFile();
        }

        private void SideControlMinimumAutomationSlider_ValueChanged(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsManager.Settings.Automation.MinimumAutomationStrokeNumber = (int)SideControlMinimumAutomationSlider.Value; SettingsManager.SaveSettingsToFile(); }

        private void ToggleSwitchSaveFullPageStrokes_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsManager.Settings.Automation.IsSaveFullPageStrokes = CardSaveFullPageStrokes.IsOn; SettingsManager.SaveSettingsToFile(); }
        private void ToggleSwitchUseCustomSaveFileName_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsManager.Settings.Automation.IsUseCustomSaveFileName = CardUseCustomSaveFileName.IsOn; SettingsManager.SaveSettingsToFile(); }

        private void TextBoxCustomSaveFileNameTemplate_LostFocus(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsManager.Settings.Automation.CustomSaveFileNameTemplate = TextBoxCustomSaveFileNameTemplate.Text; SettingsManager.SaveSettingsToFile(); }

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
                if (tag == template) { matchedIndex = i; break; }
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
        { if (!_isLoaded) return; SettingsActionHub.OnFloatingWindowInterceptorRuleChanged("SeewoWhiteboard3Floating", ToggleSwitchSeewoWhiteboard3Floating.IsOn); UpdateFloatingWindowInterceptorEnabled(); }
        private void ToggleSwitchSeewoWhiteboard5Floating_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsActionHub.OnFloatingWindowInterceptorRuleChanged("SeewoWhiteboard5Floating", ToggleSwitchSeewoWhiteboard5Floating.IsOn); UpdateFloatingWindowInterceptorEnabled(); }
        private void ToggleSwitchSeewoWhiteboard5CFloating_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsActionHub.OnFloatingWindowInterceptorRuleChanged("SeewoWhiteboard5CFloating", ToggleSwitchSeewoWhiteboard5CFloating.IsOn); UpdateFloatingWindowInterceptorEnabled(); }
        private void ToggleSwitchSeewoPincoSideBarFloating_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsActionHub.OnFloatingWindowInterceptorRuleChanged("SeewoPincoSideBarFloating", ToggleSwitchSeewoPincoSideBarFloating.IsOn); UpdateFloatingWindowInterceptorEnabled(); }
        private void ToggleSwitchSeewoPincoDrawingFloating_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsActionHub.OnFloatingWindowInterceptorRuleChanged("SeewoPincoDrawingFloating", ToggleSwitchSeewoPincoDrawingFloating.IsOn); UpdateFloatingWindowInterceptorEnabled(); }
        private void ToggleSwitchSeewoPPTFloating_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsActionHub.OnFloatingWindowInterceptorRuleChanged("SeewoPPTFloating", ToggleSwitchSeewoPPTFloating.IsOn); UpdateFloatingWindowInterceptorEnabled(); }
        private void ToggleSwitchAiClassFloating_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsActionHub.OnFloatingWindowInterceptorRuleChanged("AiClassFloating", ToggleSwitchAiClassFloating.IsOn); UpdateFloatingWindowInterceptorEnabled(); }
        private void ToggleSwitchHiteAnnotationFloating_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsActionHub.OnFloatingWindowInterceptorRuleChanged("HiteAnnotationFloating", ToggleSwitchHiteAnnotationFloating.IsOn); UpdateFloatingWindowInterceptorEnabled(); }
        private void ToggleSwitchChangYanFloating_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsActionHub.OnFloatingWindowInterceptorRuleChanged("ChangYanFloating", ToggleSwitchChangYanFloating.IsOn); UpdateFloatingWindowInterceptorEnabled(); }
        private void ToggleSwitchChangYanPPTFloating_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsActionHub.OnFloatingWindowInterceptorRuleChanged("ChangYanPPTFloating", ToggleSwitchChangYanPPTFloating.IsOn); UpdateFloatingWindowInterceptorEnabled(); }
        private void ToggleSwitchIntelligentClassFloating_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsActionHub.OnFloatingWindowInterceptorRuleChanged("IntelligentClassFloating", ToggleSwitchIntelligentClassFloating.IsOn); UpdateFloatingWindowInterceptorEnabled(); }
        private void ToggleSwitchSeewoDesktopAnnotationFloating_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsActionHub.OnFloatingWindowInterceptorRuleChanged("SeewoDesktopAnnotationFloating", ToggleSwitchSeewoDesktopAnnotationFloating.IsOn); UpdateFloatingWindowInterceptorEnabled(); }
        private void ToggleSwitchSeewoDesktopSideBarFloating_Toggled(object sender, RoutedEventArgs e)
        { if (!_isLoaded) return; SettingsActionHub.OnFloatingWindowInterceptorRuleChanged("SeewoDesktopSideBarFloating", ToggleSwitchSeewoDesktopSideBarFloating.IsOn); UpdateFloatingWindowInterceptorEnabled(); }

        #endregion

        #region File Association

        private void BtnRegisterFileAssociation_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool success = FileAssociationManager.RegisterFileAssociation();
                UpdateFileAssociationStatus();
                var mw = GetMainWindow();
                if (mw != null) mw.ShowNotification(success ? AutomationStrings.FileAssoc_RegisterSuccess : AutomationStrings.FileAssoc_RegisterFailed);
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
                if (mw != null) mw.ShowNotification(success ? AutomationStrings.FileAssoc_UnregisterSuccess : AutomationStrings.FileAssoc_UnregisterFailed);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"取消文件关联失败: {ex.Message}", LogHelper.LogType.Error);
                UpdateFileAssociationStatus();
            }
        }

        private void BtnCheckFileAssociation_Click(object sender, RoutedEventArgs e) => UpdateFileAssociationStatus();

        private void UpdateFileAssociationStatus()
        {
            try
            {
                bool isRegistered = FileAssociationManager.IsFileAssociationRegistered();
                TextBlockFileAssociationStatus.Text = isRegistered ? AutomationStrings.FileAssoc_Registered : AutomationStrings.FileAssoc_NotRegistered;
                TextBlockFileAssociationStatus.Foreground = isRegistered ? new SolidColorBrush(Colors.LightGreen) : new SolidColorBrush(Colors.LightCoral);
            }
            catch (Exception ex)
            {
                TextBlockFileAssociationStatus.Text = AutomationStrings.FileAssoc_CheckError;
                TextBlockFileAssociationStatus.Foreground = new SolidColorBrush(Colors.LightCoral);
                LogHelper.WriteLogToFile($"检查文件关联状态失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        #endregion

        #endregion

        #region Workflow Editor

        private Workflow _selectedWorkflow;
        public Workflow SelectedWorkflow
        {
            get => _selectedWorkflow;
            private set
            {
                if (ReferenceEquals(value, _selectedWorkflow)) return;
                _selectedWorkflow = value;
                OnPropertyChanged();
            }
        }

        private void BtnAddWorkflow_Click(object sender, RoutedEventArgs e)
        {
            var workflow = new Workflow();
            workflow.ActionSet.Name = $"自定义自动化 {Service.Workflows.Count + 1}";
            // 初始化一个默认规则组
            workflow.Ruleset.Groups.Add(new RuleGroup
            {
                Rules = new ObservableCollection<Rule> { new Rule() }
            });
            Service.Workflows.Add(workflow);
            Service.SaveConfig("AddWorkflow");
            RefreshWorkflowList();
            NavigationListBox.SelectedIndex = NavigationListBox.Items.Count - 1;
        }

        private void BtnRemoveWorkflow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not Workflow workflow) return;
            Service.Workflows.Remove(workflow);
            Service.SaveConfig("RemoveWorkflow");
            RefreshWorkflowList();
            NavigationListBox.SelectedIndex = 0;
        }

        private void BtnDuplicateWorkflow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not Workflow source) return;
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(source);
            var copy = Newtonsoft.Json.JsonConvert.DeserializeObject<Workflow>(json);
            if (copy != null)
            {
                copy.ActionSet.Name += " (副本)";
                Service.Workflows.Add(copy);
                Service.SaveConfig("DuplicateWorkflow");
                RefreshWorkflowList();
                NavigationListBox.SelectedIndex = NavigationListBox.Items.Count - 1;
            }
        }

        private void ToggleWorkflowEnabled_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            Service.SaveConfig("WorkflowEnabledChanged");
        }

        private void UpdateEditorBindings(Workflow workflow)
        {
            _isUpdatingEditor = true;
            try
            {
                TextBoxWorkflowName.TextChanged -= TextBoxWorkflowName_TextChanged;
                TextBoxWorkflowName.Text = workflow.ActionSet.Name;
                TextBoxWorkflowName.TextChanged += TextBoxWorkflowName_TextChanged;

                CheckBoxIsRevertEnabled.IsChecked = workflow.ActionSet.IsRevertEnabled;
                ToggleIsConditionEnabled.IsOn = workflow.IsConditionEnabled;

                // 触发器
                TriggersItemsControl.ItemsSource = workflow.Triggers;

                // 行动
                ActionsItemsControl.ItemsSource = workflow.ActionSet.Actions;

                // 规则集 - 先评估更新 State，再设置 ItemsSource
                ComboBoxRulesetMode.SelectedIndex = workflow.Ruleset.Mode == RulesetLogicalMode.Or ? 0 : 1;
                CheckBoxRulesetReversed.IsChecked = workflow.Ruleset.IsReversed;
                UpdateRulesetStateIndicator(workflow.Ruleset);
                RuleGroupsItemsControl.ItemsSource = workflow.Ruleset.Groups;

                UpdateConditionVisibility(workflow.IsConditionEnabled);
                UpdateRevertHintVisibility(workflow.ActionSet.IsRevertEnabled);
            }
            finally
            {
                _isUpdatingEditor = false;
            }
        }

        private void UpdateRulesetStateIndicator(Ruleset ruleset)
        {
            if (ruleset == null)
            {
                EllipseRulesetState.Fill = Brushes.DarkGray;
                return;
            }

            // 评估规则集（会自动更新所有层级的 State）
            Service.RulesetService.IsRulesetSatisfied(ruleset);

            EllipseRulesetState.Fill = ruleset.State switch
            {
                2 => Brushes.Green,
                1 => Brushes.IndianRed,
                _ => Brushes.DarkGray
            };
        }

        private void TextBoxWorkflowName_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingEditor) return;
            if (SelectedWorkflow is Workflow workflow)
            {
                workflow.ActionSet.Name = TextBoxWorkflowName.Text;
                Service.SaveConfig("NameChanged");
            }
        }

        private void CheckBoxIsRevertEnabled_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded || _isUpdatingEditor) return;
            if (SelectedWorkflow is Workflow workflow)
            {
                workflow.ActionSet.IsRevertEnabled = CheckBoxIsRevertEnabled.IsChecked == true;
                UpdateRevertHintVisibility(workflow.ActionSet.IsRevertEnabled);
                Service.SaveConfig("RevertEnabledChanged");
            }
        }

        private void ToggleIsConditionEnabled_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded || _isUpdatingEditor) return;
            if (SelectedWorkflow is Workflow workflow)
            {
                workflow.IsConditionEnabled = ToggleIsConditionEnabled.IsOn;
                UpdateConditionVisibility(workflow.IsConditionEnabled);
                Service.SaveConfig("ConditionEnabledChanged");
            }
        }

        private void UpdateConditionVisibility(bool enabled)
        {
            ConditionDisabledHint.Visibility = enabled ? Visibility.Collapsed : Visibility.Visible;
            ConditionEditorPanel.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateRevertHintVisibility(bool enabled)
        {
            RevertHintPanel.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        }

        // 触发器操作
        private void BtnAddTrigger_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedWorkflow is not Workflow workflow) return;
            var trigger = new TriggerSettings { Id = AutomationRegistry.RegisteredTriggers.FirstOrDefault()?.Id ?? "" };
            workflow.Triggers.Add(trigger);
            Service.SaveConfig("AddTrigger");
        }

        private void BtnRemoveTrigger_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedWorkflow is not Workflow workflow) return;
            if (sender is not Button btn || btn.Tag is not TriggerSettings trigger) return;
            workflow.Triggers.Remove(trigger);
            Service.SaveConfig("RemoveTrigger");
        }

        private void ComboBoxTriggerType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            if (sender is ComboBox cb && cb.Tag is TriggerSettings trigger)
            {
                trigger.Id = cb.SelectedValue as string ?? "";
                Service.SaveConfig("TriggerTypeChanged");
            }
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T target) return target;

                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }

            return null;
        }

        // 行动操作
        private void BtnAddAction_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedWorkflow is not Workflow workflow) return;
            var firstAction = AutomationRegistry.RegisteredActions.FirstOrDefault();
            var action = new Ink_Canvas.WorkflowAutomation.Models.Action { Id = firstAction.Key ?? "" };
            workflow.ActionSet.Actions.Add(action);
            Service.SaveConfig("AddAction");
        }

        private void BtnRemoveAction_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedWorkflow is not Workflow workflow) return;
            if (sender is not Button btn || btn.Tag is not Ink_Canvas.WorkflowAutomation.Models.Action action) return;
            workflow.ActionSet.Actions.Remove(action);
            Service.SaveConfig("RemoveAction");
        }

        private void ComboBoxActionType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || _isUpdatingEditor) return;
            if (sender is ComboBox cb && cb.Tag is Ink_Canvas.WorkflowAutomation.Models.Action action)
            {
                action.Id = cb.SelectedValue as string ?? "";
                action.Settings = null;

                if (FindVisualChild<AutomationSettingsPresenter>(cb.Parent) is { } presenter)
                    presenter.RefreshContent();

                Service.SaveConfig("ActionTypeChanged");
            }
        }

        // 规则集操作
        private void ComboBoxRulesetMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || _isUpdatingEditor) return;
            if (SelectedWorkflow is Workflow workflow)
            {
                workflow.Ruleset.Mode = ComboBoxRulesetMode.SelectedIndex == 0 ? RulesetLogicalMode.Or : RulesetLogicalMode.And;
                UpdateRulesetStateIndicator(workflow.Ruleset);
                Service.SaveConfig("RulesetModeChanged");
            }
        }

        private void CheckBoxRulesetReversed_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded || _isUpdatingEditor) return;
            if (SelectedWorkflow is Workflow workflow)
            {
                workflow.Ruleset.IsReversed = CheckBoxRulesetReversed.IsChecked == true;
                UpdateRulesetStateIndicator(workflow.Ruleset);
                Service.SaveConfig("RulesetReversedChanged");
            }
        }

        private void BtnAddRuleGroup_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedWorkflow is not Workflow workflow) return;
            workflow.Ruleset.Groups.Add(new RuleGroup
            {
                Rules = new ObservableCollection<Rule> { new Rule() }
            });
            Service.SaveConfig("AddRuleGroup");
        }

        private void BtnDeleteGroup_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedWorkflow is not Workflow workflow) return;
            if (sender is not Button btn || btn.Tag is not RuleGroup group) return;
            workflow.Ruleset.Groups.Remove(group);
            Service.SaveConfig("DeleteGroup");
        }

        private void BtnDuplicateGroup_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedWorkflow is not Workflow workflow) return;
            if (sender is not Button btn || btn.Tag is not RuleGroup source) return;
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(source);
            var copy = Newtonsoft.Json.JsonConvert.DeserializeObject<RuleGroup>(json);
            if (copy != null)
            {
                workflow.Ruleset.Groups.Add(copy);
                Service.SaveConfig("DuplicateGroup");
            }
        }

        private void ComboBoxGroupMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            if (sender is ComboBox cb && cb.Tag is RuleGroup group)
            {
                group.Mode = cb.SelectedIndex == 0 ? RulesetLogicalMode.Or : RulesetLogicalMode.And;
                if (SelectedWorkflow is Workflow workflow)
                {
                    UpdateRulesetStateIndicator(workflow.Ruleset);
                }
                Service.SaveConfig("GroupModeChanged");
            }
        }

        private void CheckBoxGroupReversed_Changed(object sender, RoutedEventArgs e)
        {
            // IsReversed 绑定是 TwoWay，自动更新
            if (!_isLoaded) return;
            if (SelectedWorkflow is Workflow workflow)
            {
                UpdateRulesetStateIndicator(workflow.Ruleset);
            }
            Service.SaveConfig("GroupReversedChanged");
        }

        private void CheckBoxGroupEnabled_Toggled(object sender, RoutedEventArgs e)
        {
            // IsEnabled 绑定是 TwoWay，自动更新
            if (!_isLoaded) return;
            if (SelectedWorkflow is Workflow workflow)
            {
                UpdateRulesetStateIndicator(workflow.Ruleset);
            }
            Service.SaveConfig("GroupEnabledChanged");
        }

        private void BtnAddRuleToGroup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not RuleGroup group) return;
            var firstRule = AutomationRegistry.RegisteredRules.FirstOrDefault();
            var rule = new Rule { Id = firstRule.Key ?? "" };
            group.Rules.Add(rule);
            if (SelectedWorkflow is Workflow workflow)
            {
                UpdateRulesetStateIndicator(workflow.Ruleset);
            }
            Service.SaveConfig("AddRule");
        }

        private void BtnRemoveRule_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not Rule rule) return;
            // 找到包含此规则的 RuleGroup
            if (SelectedWorkflow is Workflow workflow)
            {
                foreach (var group in workflow.Ruleset.Groups)
                {
                    if (group.Rules.Contains(rule))
                    {
                        group.Rules.Remove(rule);
                        UpdateRulesetStateIndicator(workflow.Ruleset);
                        Service.SaveConfig("RemoveRule");
                        break;
                    }
                }
            }
        }

        private void ComboBoxRuleType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            if (sender is ComboBox cb && cb.Tag is Rule rule)
            {
                rule.Id = cb.SelectedValue as string ?? "";
                EnsureRuleSettingsInstance(rule);
                if (SelectedWorkflow is Workflow workflow)
                {
                    UpdateRulesetStateIndicator(workflow.Ruleset);
                }
                Service.SaveConfig("RuleTypeChanged");
            }
        }

        private void CheckBoxRuleReversed_Changed(object sender, RoutedEventArgs e)
        {
            // IsReversed 绑定是 TwoWay，自动更新
            if (!_isLoaded) return;
            if (SelectedWorkflow is Workflow workflow)
            {
                UpdateRulesetStateIndicator(workflow.Ruleset);
            }
            Service.SaveConfig("RuleReversedChanged");
        }

        // 触发/恢复按钮
        private void BtnInvokeAction_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedWorkflow is not Workflow workflow) return;
            Service.ActionService.Invoke(workflow.ActionSet);
            Service.SaveConfig("ManualInvokeAction");
        }

        private void BtnRevertAction_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedWorkflow is not Workflow workflow) return;
            Service.ActionService.Revert(workflow.ActionSet);
            Service.SaveConfig("ManualRevertAction");
        }

        private void RuleSettingsPresenter_SettingsChanged(object sender, EventArgs e)
        {
            if (SelectedWorkflow is Workflow workflow)
            {
                UpdateRulesetStateIndicator(workflow.Ruleset);
            }
            Service.SaveConfig("RuleSettingsChanged");
        }

        private void ActionSettingsPresenter_SettingsChanged(object sender, EventArgs e)
        {
            Service.SaveConfig("ActionSettingsChanged");
        }

        internal static object EnsureSettingsInstance(object settings, Type settingsType)
        {
            if (settingsType == null) return null;
            var actual = settings ?? Activator.CreateInstance(settingsType);

            if (actual is JToken token)
            {
                try
                {
                    actual = token.ToObject(settingsType);
                }
                catch
                {
                    actual = Activator.CreateInstance(settingsType);
                }
            }

            if (actual == null || actual.GetType() != settingsType)
            {
                actual = Activator.CreateInstance(settingsType);
            }

            return actual;
        }

        private static void EnsureRuleSettingsInstance(Rule rule)
        {
            if (!AutomationRegistry.RegisteredRules.TryGetValue(rule.Id, out var info))
                return;

            rule.Settings = EnsureSettingsInstance(rule.Settings, info.SettingsType);
        }

        #endregion
    }

    public class AutomationSettingsPresenter : ContentControl
    {
        public static readonly DependencyProperty ItemProperty = DependencyProperty.Register(
            nameof(Item), typeof(object), typeof(AutomationSettingsPresenter),
            new PropertyMetadata(null, OnItemChanged));

        private INotifyPropertyChanged _currentNotifySource;

        public object Item
        {
            get => GetValue(ItemProperty);
            set => SetValue(ItemProperty, value);
        }

        public event EventHandler SettingsChanged;

        public AutomationSettingsPresenter()
        {
            Loaded += (_, _) => RefreshContent();
            Unloaded += (_, _) => DetachCurrentItem();
        }

        private static void OnItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not AutomationSettingsPresenter presenter) return;
            presenter.DetachCurrentItem();
            presenter.AttachItem(e.NewValue);
            presenter.RefreshContent();
        }

        private void AttachItem(object item)
        {
            if (item is INotifyPropertyChanged notify)
            {
                _currentNotifySource = notify;
                _currentNotifySource.PropertyChanged += CurrentItem_PropertyChanged;
            }
        }

        private void DetachCurrentItem()
        {
            if (_currentNotifySource == null) return;
            _currentNotifySource.PropertyChanged -= CurrentItem_PropertyChanged;
            _currentNotifySource = null;
        }

        private void CurrentItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(Rule.Id) or nameof(Rule.Settings) or nameof(Ink_Canvas.WorkflowAutomation.Models.Action.Id) or nameof(Ink_Canvas.WorkflowAutomation.Models.Action.Settings))
            {
                RefreshContent();
            }
        }

        internal void RefreshContent()
        {
            var context = ResolveSettingsContext();
            if (context == null)
            {
                Visibility = Visibility.Collapsed;
                Content = null;
                return;
            }

            var editableProperties = context.SettingsType
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.CanRead && p.CanWrite)
                .ToArray();

            if (editableProperties.Length == 0)
            {
                Visibility = Visibility.Collapsed;
                Content = null;
                return;
            }

            Visibility = Visibility.Visible;

            var panel = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            foreach (var property in editableProperties)
            {
                var editor = CreateEditor(context.SettingsObject, property);
                if (editor != null)
                {
                    panel.Children.Add(editor);
                }
            }

            Content = panel.Children.Count > 0 ? panel : null;
            Visibility = panel.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private FrameworkElement CreateEditor(object settingsObject, PropertyInfo property)
        {
            var propertyType = property.PropertyType;
            var label = GetPropertyDisplayName(property.Name);
            var value = property.GetValue(settingsObject);

            if (propertyType == typeof(string))
            {
                var container = CreateLabeledContainer(label);
                var textBox = new TextBox
                {
                    Text = value as string ?? "",
                    MinWidth = 180,
                    Padding = new Thickness(8, 4, 8, 4)
                };
                textBox.TextChanged += (_, _) =>
                {
                    property.SetValue(settingsObject, textBox.Text);
                    RaiseSettingsChanged();
                };
                container.Children.Add(textBox);
                return container;
            }

            if (propertyType == typeof(bool))
            {
                var checkBox = new CheckBox
                {
                    Content = label,
                    IsChecked = value as bool? ?? false,
                    Margin = new Thickness(0, 18, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                checkBox.Checked += (_, _) =>
                {
                    property.SetValue(settingsObject, true);
                    RaiseSettingsChanged();
                };
                checkBox.Unchecked += (_, _) =>
                {
                    property.SetValue(settingsObject, false);
                    RaiseSettingsChanged();
                };
                return checkBox;
            }

            if (propertyType == typeof(int) || propertyType == typeof(double))
            {
                var container = CreateLabeledContainer(label);
                var textBox = new TextBox
                {
                    Text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "",
                    MinWidth = 100,
                    Padding = new Thickness(8, 4, 8, 4)
                };
                textBox.LostFocus += (_, _) =>
                {
                    try
                    {
                        var converted = propertyType == typeof(int)
                            ? int.Parse(textBox.Text, CultureInfo.InvariantCulture)
                            : double.Parse(textBox.Text, CultureInfo.InvariantCulture);
                        property.SetValue(settingsObject, converted);
                        RaiseSettingsChanged();
                    }
                    catch
                    {
                        textBox.Text = Convert.ToString(property.GetValue(settingsObject), CultureInfo.InvariantCulture) ?? "";
                    }
                };
                container.Children.Add(textBox);
                return container;
            }

            if (propertyType.IsEnum)
            {
                var container = CreateLabeledContainer(label);
                var comboBox = new ComboBox
                {
                    ItemsSource = Enum.GetValues(propertyType),
                    SelectedItem = value,
                    MinWidth = 120
                };
                comboBox.SelectionChanged += (_, _) =>
                {
                    if (comboBox.SelectedItem == null) return;
                    property.SetValue(settingsObject, comboBox.SelectedItem);
                    RaiseSettingsChanged();
                };
                container.Children.Add(comboBox);
                return container;
            }

            return null;
        }

        private static StackPanel CreateLabeledContainer(string label)
        {
            var container = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            container.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 11,
                Opacity = 0.72,
                Margin = new Thickness(0, 0, 0, 2)
            });
            return container;
        }

        private SettingsContext ResolveSettingsContext()
        {
            switch (Item)
            {
                case Rule rule:
                    if (!AutomationRegistry.RegisteredRules.TryGetValue(rule.Id, out var ruleInfo) || ruleInfo.SettingsType == null)
                        return null;
                    rule.Settings = AutomationWorkflowPage.EnsureSettingsInstance(rule.Settings, ruleInfo.SettingsType);
                    return new SettingsContext(rule.Settings!, ruleInfo.SettingsType);

                case Ink_Canvas.WorkflowAutomation.Models.Action action:
                    if (!AutomationRegistry.RegisteredActions.TryGetValue(action.Id, out var actionInfo) || actionInfo.SettingsType == null)
                        return null;
                    action.Settings = AutomationWorkflowPage.EnsureSettingsInstance(action.Settings, actionInfo.SettingsType);
                    return new SettingsContext(action.Settings!, actionInfo.SettingsType);

                default:
                    return null;
            }
        }

        private static string GetPropertyDisplayName(string propertyName)
        {
            return propertyName switch
            {
                "ProcessName" => "进程名",
                "TitleContains" => "标题包含",
                "IgnoreCase" => "忽略大小写",
                "Type" => "通知类型",
                "Message" => "通知内容",
                "SavePath" => "保存路径",
                "SaveAsXml" => "保存为 XML",
                "Fold" => "折叠",
                "EnterAnnotation" => "进入批注",
                "Topmost" => "置顶",
                _ => propertyName
            };
        }

        private void RaiseSettingsChanged()
        {
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        private sealed record SettingsContext(object SettingsObject, Type SettingsType);
    }

    /// <summary>
    /// RulesetLogicalMode 到 int 的转换器，用于 ComboBox SelectedIndex 绑定
    /// </summary>
    public class RulesetLogicalModeToIntConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is RulesetLogicalMode mode)
                return (int)mode; // Or=0, And=1
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int i)
                return (RulesetLogicalMode)i;
            return RulesetLogicalMode.Or;
        }
    }
}
