﻿using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Hardcodet.Wpf.TaskbarNotification;
using Ink_Canvas.Helpers;
using Newtonsoft.Json;
using OSVersionExtension;
using File = System.IO.File;
using OperatingSystem = OSVersionExtension.OperatingSystem;

namespace Ink_Canvas {
    public partial class MainWindow : Window {
        private void LoadSettings(bool isStartup = false) {
            AppVersionTextBlock.Text = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            try {
                if (File.Exists(App.RootPath + settingsFileName)) {
                    try {
                        string text = File.ReadAllText(App.RootPath + settingsFileName);
                        Settings = JsonConvert.DeserializeObject<Settings>(text);
                    }
                    catch { }
                } else {
                    BtnResetToSuggestion_Click(null, null);
                }
            }
            catch (Exception ex) {
                LogHelper.WriteLogToFile(ex.ToString(), LogHelper.LogType.Error);
            }

            // Startup
            if (isStartup) {
                CursorIcon_Click(null, null);
            }

            try {
                if (File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.Startup) +
                                "\\Ink Canvas Annotation.lnk")) {
                    ToggleSwitchRunAtStartup.IsOn = true;
                }
            }
            catch (Exception ex) {
                LogHelper.WriteLogToFile(ex.ToString(), LogHelper.LogType.Error);
            }

            if (Settings.Startup != null) {
                if (isStartup) {
                    if (Settings.Automation.AutoDelSavedFiles) {
                        DelAutoSavedFiles.DeleteFilesOlder(Settings.Automation.AutoSavedStrokesLocation,
                            Settings.Automation.AutoDelSavedFilesDaysThreshold);
                    }

                    if (Settings.Startup.IsFoldAtStartup) {
                        FoldFloatingBar_MouseUp(Fold_Icon, null);
                    }
                }

                if (Settings.Startup.IsEnableNibMode) {
                    ToggleSwitchEnableNibMode.IsOn = true;
                    BoardToggleSwitchEnableNibMode.IsOn = true;
                    BoundsWidth = Settings.Advanced.NibModeBoundsWidth;
                } else {
                    ToggleSwitchEnableNibMode.IsOn = false;
                    BoardToggleSwitchEnableNibMode.IsOn = false;
                    BoundsWidth = Settings.Advanced.FingerModeBoundsWidth;
                }

                // 设置自动更新相关选项
                ToggleSwitchIsAutoUpdate.IsOn = Settings.Startup.IsAutoUpdate;
                
                // 只有在启用了自动更新功能时才检查更新
                if (Settings.Startup.IsAutoUpdate) {
                if (isStartup) {
                    LogHelper.WriteLogToFile("AutoUpdate | Running auto-update check at startup");
                    AutoUpdate();
                }
                    // 当设置被修改时也检查更新（非启动时）
                    else {
                    LogHelper.WriteLogToFile("AutoUpdate | Running auto-update check after settings change");
                    AutoUpdate();
                    }
                }

                // ToggleSwitchIsAutoUpdateWithSilence.Visibility = Settings.Startup.IsAutoUpdate ? Visibility.Visible : Visibility.Collapsed;
                if (Settings.Startup.IsAutoUpdateWithSilence) {
                    ToggleSwitchIsAutoUpdateWithSilence.IsOn = true;
                }

                // 初始化更新通道选择
                foreach (var radioButton in UpdateChannelSelector.Items) {
                    if (radioButton is RadioButton rb) {
                        if (rb.Tag.ToString() == Settings.Startup.UpdateChannel.ToString()) {
                            rb.IsChecked = true;
                            break;
                        }
                    }
                }

                AutoUpdateTimePeriodBlock.Visibility = Settings.Startup.IsAutoUpdateWithSilence
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                AutoUpdateWithSilenceTimeComboBox.InitializeAutoUpdateWithSilenceTimeComboBoxOptions(
                    AutoUpdateWithSilenceStartTimeComboBox, AutoUpdateWithSilenceEndTimeComboBox);
                AutoUpdateWithSilenceStartTimeComboBox.SelectedItem = Settings.Startup.AutoUpdateWithSilenceStartTime;
                AutoUpdateWithSilenceEndTimeComboBox.SelectedItem = Settings.Startup.AutoUpdateWithSilenceEndTime;

                ToggleSwitchFoldAtStartup.IsOn = Settings.Startup.IsFoldAtStartup;
            } else {
                Settings.Startup = new Startup();
            }

            // 恢复崩溃后操作设置
            if (Settings.Startup != null)
            {
                // 恢复崩溃后操作选项
                if (Settings.Startup.CrashAction == 0)
                {
                    App.CrashAction = App.CrashActionType.SilentRestart;
                    if (RadioCrashSilentRestart != null) RadioCrashSilentRestart.IsChecked = true;
                }
                else
                {
                    App.CrashAction = App.CrashActionType.NoAction;
                    if (RadioCrashNoAction != null) RadioCrashNoAction.IsChecked = true;
                }
            }

            // Appearance
            if (Settings.Appearance != null) {
                if (!Settings.Appearance.IsEnableDisPlayNibModeToggler) {
                    NibModeSimpleStackPanel.Visibility = Visibility.Collapsed;
                    BoardNibModeSimpleStackPanel.Visibility = Visibility.Collapsed;
                } else {
                    NibModeSimpleStackPanel.Visibility = Visibility.Visible;
                    BoardNibModeSimpleStackPanel.Visibility = Visibility.Visible;
                }

                //if (Settings.Appearance.IsColorfulViewboxFloatingBar) // 浮动工具栏背景色
                //{
                //    LinearGradientBrush gradientBrush = new LinearGradientBrush();
                //    gradientBrush.StartPoint = new Point(0, 0);
                //    gradientBrush.EndPoint = new Point(1, 1);
                //    GradientStop blueStop = new GradientStop(Color.FromArgb(0x95, 0x80, 0xB0, 0xFF), 0);
                //    GradientStop greenStop = new GradientStop(Color.FromArgb(0x95, 0xC0, 0xFF, 0xC0), 1);
                //    gradientBrush.GradientStops.Add(blueStop);
                //    gradientBrush.GradientStops.Add(greenStop);
                //    EnableTwoFingerGestureBorder.Background = gradientBrush;
                //    BorderFloatingBarMainControls.Background = gradientBrush;
                //    BorderFloatingBarMoveControls.Background = gradientBrush;
                //    BorderFloatingBarExitPPTBtn.Background = gradientBrush;

                //    ToggleSwitchColorfulViewboxFloatingBar.IsOn = true;
                //} else {
                //    EnableTwoFingerGestureBorder.Background = (Brush)FindResource("FloatBarBackground");
                //    BorderFloatingBarMainControls.Background = (Brush)FindResource("FloatBarBackground");
                //    BorderFloatingBarMoveControls.Background = (Brush)FindResource("FloatBarBackground");
                //    BorderFloatingBarExitPPTBtn.Background = (Brush)FindResource("FloatBarBackground");

                //    ToggleSwitchColorfulViewboxFloatingBar.IsOn = false;
                //}

                if (Settings.Appearance.ViewboxFloatingBarScaleTransformValue != 0) // 浮动工具栏 UI 缩放 85%
                {
                    double val = Settings.Appearance.ViewboxFloatingBarScaleTransformValue;
                    ViewboxFloatingBarScaleTransform.ScaleX =
                        (val > 0.5 && val < 1.25) ? val : val <= 0.5 ? 0.5 : val >= 1.25 ? 1.25 : 1;
                    ViewboxFloatingBarScaleTransform.ScaleY =
                        (val > 0.5 && val < 1.25) ? val : val <= 0.5 ? 0.5 : val >= 1.25 ? 1.25 : 1;
                    ViewboxFloatingBarScaleTransformValueSlider.Value = val;
                }

                ComboBoxUnFoldBtnImg.SelectedIndex = Settings.Appearance.UnFoldButtonImageType;
                switch (Settings.Appearance.UnFoldButtonImageType) {
                    case 0:
                        RightUnFoldBtnImgChevron.Source =
                            new BitmapImage(new Uri("pack://application:,,,/Resources/new-icons/unfold-chevron.png"));
                        RightUnFoldBtnImgChevron.Width = 14;
                        RightUnFoldBtnImgChevron.Height = 14;
                        RightUnFoldBtnImgChevron.RenderTransform = new RotateTransform(180);
                        LeftUnFoldBtnImgChevron.Source =
                            new BitmapImage(new Uri("pack://application:,,,/Resources/new-icons/unfold-chevron.png"));
                        LeftUnFoldBtnImgChevron.Width = 14;
                        LeftUnFoldBtnImgChevron.Height = 14;
                        LeftUnFoldBtnImgChevron.RenderTransform = null;
                        break;
                    case 1:
                        RightUnFoldBtnImgChevron.Source =
                            new BitmapImage(new Uri("pack://application:,,,/Resources/new-icons/pen-white.png"));
                        RightUnFoldBtnImgChevron.Width = 18;
                        RightUnFoldBtnImgChevron.Height = 18;
                        RightUnFoldBtnImgChevron.RenderTransform = null;
                        LeftUnFoldBtnImgChevron.Source =
                            new BitmapImage(new Uri("pack://application:,,,/Resources/new-icons/pen-white.png"));
                        LeftUnFoldBtnImgChevron.Width = 18;
                        LeftUnFoldBtnImgChevron.Height = 18;
                        LeftUnFoldBtnImgChevron.RenderTransform = null;
                        break;
                }

                ComboBoxChickenSoupSource.SelectedIndex = Settings.Appearance.ChickenSoupSource;

                ToggleSwitchEnableQuickPanel.IsOn = Settings.Appearance.IsShowQuickPanel;

                ToggleSwitchEnableTrayIcon.IsOn = Settings.Appearance.EnableTrayIcon;
                ICCTrayIconExampleImage.Visibility =
                    Settings.Appearance.EnableTrayIcon ? Visibility.Visible : Visibility.Collapsed;
                var _taskbar = (TaskbarIcon)Application.Current.Resources["TaskbarTrayIcon"];
                _taskbar.Visibility = Settings.Appearance.EnableTrayIcon ? Visibility.Visible : Visibility.Collapsed;

                ViewboxFloatingBar.Opacity = Settings.Appearance.ViewboxFloatingBarOpacityValue;

                if (Settings.Appearance.EnableViewboxBlackBoardScaleTransform) // 画板 UI 缩放 80%
                {
                    //ViewboxBlackboardLeftSideScaleTransform.ScaleX = 0.8;
                    //ViewboxBlackboardLeftSideScaleTransform.ScaleY = 0.8;
                    ViewboxBlackboardCenterSideScaleTransform.ScaleX = 0.8;
                    ViewboxBlackboardCenterSideScaleTransform.ScaleY = 0.8;
                    //ViewboxBlackboardRightSideScaleTransform.ScaleX = 0.8;
                    //ViewboxBlackboardRightSideScaleTransform.ScaleY = 0.8;

                    ToggleSwitchEnableViewboxBlackBoardScaleTransform.IsOn = true;
                } else {
                    //ViewboxBlackboardLeftSideScaleTransform.ScaleX = 1;
                    //ViewboxBlackboardLeftSideScaleTransform.ScaleY = 1;
                    ViewboxBlackboardCenterSideScaleTransform.ScaleX = 1;
                    ViewboxBlackboardCenterSideScaleTransform.ScaleY = 1;
                    //ViewboxBlackboardRightSideScaleTransform.ScaleX = 1;
                    //ViewboxBlackboardRightSideScaleTransform.ScaleY = 1;

                    ToggleSwitchEnableViewboxBlackBoardScaleTransform.IsOn = false;
                }

                if (Settings.Appearance.IsTransparentButtonBackground) {
                    BtnExit.Background = new SolidColorBrush(StringToColor("#7F909090"));
                } else {
                    //Light
                    BtnExit.Background = BtnSwitchTheme.Content.ToString() == "深色"
                        ? new SolidColorBrush(StringToColor("#FFCCCCCC"))
                        :
                        //Dark
                        new SolidColorBrush(StringToColor("#FF555555"));
                }

                // 更新自定义图标下拉列表
                UpdateCustomIconsInComboBox();
                
                // 设置选中的图标索引
                // 如果索引超出范围(自定义图标可能已删除)，使用默认图标
                if (Settings.Appearance.FloatingBarImg >= ComboBoxFloatingBarImg.Items.Count)
                {
                    Settings.Appearance.FloatingBarImg = 0;
                }
                
                ComboBoxFloatingBarImg.SelectedIndex = Settings.Appearance.FloatingBarImg;
                
                // 更新浮动栏图标
                UpdateFloatingBarIcon();

                ToggleSwitchEnableTimeDisplayInWhiteboardMode.IsOn =
                    Settings.Appearance.EnableTimeDisplayInWhiteboardMode;

                ToggleSwitchEnableChickenSoupInWhiteboardMode.IsOn =
                    Settings.Appearance.EnableChickenSoupInWhiteboardMode;

                SystemEvents_UserPreferenceChanged(null, null);
            } else {
                Settings.Appearance = new Appearance();
            }

            // PowerPointSettings
            if (Settings.PowerPointSettings != null) {
                
                
                if (Settings.PowerPointSettings.PowerPointSupport) {
                    ToggleSwitchSupportPowerPoint.IsOn = true;
                    // PPT监控将在Window_Loaded中启动
                } else {
                    ToggleSwitchSupportPowerPoint.IsOn = false;
                    // PPT监控将保持停止状态
                }

                ToggleSwitchShowCanvasAtNewSlideShow.IsOn = Settings.PowerPointSettings.IsShowCanvasAtNewSlideShow;

                ToggleSwitchEnableTwoFingerGestureInPresentationMode.IsOn =
                    Settings.PowerPointSettings.IsEnableTwoFingerGestureInPresentationMode;

                ToggleSwitchEnableFingerGestureSlideShowControl.IsOn =
                    Settings.PowerPointSettings.IsEnableFingerGestureSlideShowControl;

                ToggleSwitchAutoSaveStrokesInPowerPoint.IsOn =
                    Settings.PowerPointSettings.IsAutoSaveStrokesInPowerPoint;

                ToggleSwitchNotifyPreviousPage.IsOn = Settings.PowerPointSettings.IsNotifyPreviousPage;

                // -- new --
                ToggleSwitchShowPPTButton.IsOn = Settings.PowerPointSettings.ShowPPTButton;

                ToggleSwitchEnablePPTButtonPageClickable.IsOn =
                    Settings.PowerPointSettings.EnablePPTButtonPageClickable;

                var dops = Settings.PowerPointSettings.PPTButtonsDisplayOption.ToString();
                var dopsc = dops.ToCharArray();
                if ((dopsc[0] == '1' || dopsc[0] == '2') && (dopsc[1] == '1' || dopsc[1] == '2') &&
                    (dopsc[2] == '1' || dopsc[2] == '2') && (dopsc[3] == '1' || dopsc[3] == '2')) {
                    CheckboxEnableLBPPTButton.IsChecked = dopsc[0] == '2';
                    CheckboxEnableRBPPTButton.IsChecked = dopsc[1] == '2';
                    CheckboxEnableLSPPTButton.IsChecked = dopsc[2] == '2';
                    CheckboxEnableRSPPTButton.IsChecked = dopsc[3] == '2';
                } else {
                    Settings.PowerPointSettings.PPTButtonsDisplayOption = 2222;
                    CheckboxEnableLBPPTButton.IsChecked = true;
                    CheckboxEnableRBPPTButton.IsChecked = true;
                    CheckboxEnableLSPPTButton.IsChecked = true;
                    CheckboxEnableRSPPTButton.IsChecked = true;
                    SaveSettingsToFile();
                }

                var sops = Settings.PowerPointSettings.PPTSButtonsOption.ToString();
                var sopsc = sops.ToCharArray();
                if ((sopsc[0] == '1' || sopsc[0] == '2') && (sopsc[1] == '1' || sopsc[1] == '2') &&
                    (sopsc[2] == '1' || sopsc[2] == '2'))
                {
                    CheckboxSPPTDisplayPage.IsChecked = sopsc[0] == '2';
                    CheckboxSPPTHalfOpacity.IsChecked = sopsc[1] == '2';
                    CheckboxSPPTBlackBackground.IsChecked = sopsc[2] == '2';
                }
                else
                {
                    Settings.PowerPointSettings.PPTSButtonsOption = 221;
                    CheckboxSPPTDisplayPage.IsChecked = true;
                    CheckboxSPPTHalfOpacity.IsChecked = true;
                    CheckboxSPPTBlackBackground.IsChecked = false;
                    SaveSettingsToFile();
                }

                var bops = Settings.PowerPointSettings.PPTBButtonsOption.ToString();
                var bopsc = bops.ToCharArray();
                if ((bopsc[0] == '1' || bopsc[0] == '2') && (bopsc[1] == '1' || bopsc[1] == '2') &&
                    (bopsc[2] == '1' || bopsc[2] == '2'))
                {
                    CheckboxBPPTDisplayPage.IsChecked = bopsc[0] == '2';
                    CheckboxBPPTHalfOpacity.IsChecked = bopsc[1] == '2';
                    CheckboxBPPTBlackBackground.IsChecked = bopsc[2] == '2';
                }
                else
                {
                    Settings.PowerPointSettings.PPTBButtonsOption = 121;
                    CheckboxBPPTDisplayPage.IsChecked = false;
                    CheckboxBPPTHalfOpacity.IsChecked = true;
                    CheckboxBPPTBlackBackground.IsChecked = false;
                    SaveSettingsToFile();
                }

                PPTButtonLeftPositionValueSlider.Value = Settings.PowerPointSettings.PPTLSButtonPosition;

                PPTButtonRightPositionValueSlider.Value = Settings.PowerPointSettings.PPTRSButtonPosition;

                UpdatePPTBtnSlidersStatus();

                UpdatePPTBtnPreview();

                // -- new --

                ToggleSwitchNotifyHiddenPage.IsOn = Settings.PowerPointSettings.IsNotifyHiddenPage;

                ToggleSwitchNotifyAutoPlayPresentation.IsOn = Settings.PowerPointSettings.IsNotifyAutoPlayPresentation;

                ToggleSwitchSupportWPS.IsOn = Settings.PowerPointSettings.IsSupportWPS;

                ToggleSwitchAutoSaveScreenShotInPowerPoint.IsOn =
                    Settings.PowerPointSettings.IsAutoSaveScreenShotInPowerPoint;
                ToggleSwitchEnableWppProcessKill.IsOn = Settings.PowerPointSettings.EnableWppProcessKill;
                ToggleSwitchAlwaysGoToFirstPageOnReenter.IsOn = Settings.PowerPointSettings.IsAlwaysGoToFirstPageOnReenter;
            } else {
                Settings.PowerPointSettings = new PowerPointSettings();
            }

            // Gesture
            if (Settings.Gesture != null) {
                ToggleSwitchEnableMultiTouchMode.IsOn = Settings.Gesture.IsEnableMultiTouchMode;

                ToggleSwitchEnableTwoFingerZoom.IsOn = Settings.Gesture.IsEnableTwoFingerZoom;
                BoardToggleSwitchEnableTwoFingerZoom.IsOn = Settings.Gesture.IsEnableTwoFingerZoom;

                ToggleSwitchEnableTwoFingerTranslate.IsOn = Settings.Gesture.IsEnableTwoFingerTranslate;
                BoardToggleSwitchEnableTwoFingerTranslate.IsOn = Settings.Gesture.IsEnableTwoFingerTranslate;

                ToggleSwitchEnableTwoFingerRotation.IsOn = Settings.Gesture.IsEnableTwoFingerRotation;
                BoardToggleSwitchEnableTwoFingerRotation.IsOn = Settings.Gesture.IsEnableTwoFingerRotation;

                ToggleSwitchAutoSwitchTwoFingerGesture.IsOn = Settings.Gesture.AutoSwitchTwoFingerGesture;

                ToggleSwitchEnableTwoFingerRotation.IsOn = Settings.Gesture.IsEnableTwoFingerRotation;

                ToggleSwitchEnableTwoFingerRotationOnSelection.IsOn =
                    Settings.Gesture.IsEnableTwoFingerRotationOnSelection;

                if (Settings.Gesture.AutoSwitchTwoFingerGesture) {
                    if (Topmost) {
                        ToggleSwitchEnableTwoFingerTranslate.IsOn = false;
                        BoardToggleSwitchEnableTwoFingerTranslate.IsOn = false;
                        Settings.Gesture.IsEnableTwoFingerTranslate = false;
                        if (!isInMultiTouchMode) ToggleSwitchEnableMultiTouchMode.IsOn = true;
                    } else {
                        ToggleSwitchEnableTwoFingerTranslate.IsOn = true;
                        BoardToggleSwitchEnableTwoFingerTranslate.IsOn = true;
                        Settings.Gesture.IsEnableTwoFingerTranslate = true;
                        if (isInMultiTouchMode) ToggleSwitchEnableMultiTouchMode.IsOn = false;
                    }
                }

                CheckEnableTwoFingerGestureBtnColorPrompt();
            } else {
                Settings.Gesture = new Gesture();
            }

            // Canvas
            if (Settings.Canvas != null) {
                drawingAttributes.Height = Settings.Canvas.InkWidth;
                drawingAttributes.Width = Settings.Canvas.InkWidth;

                InkWidthSlider.Value = Settings.Canvas.InkWidth * 2;
                HighlighterWidthSlider.Value = Settings.Canvas.HighlighterWidth;

                ComboBoxHyperbolaAsymptoteOption.SelectedIndex = (int)Settings.Canvas.HyperbolaAsymptoteOption;

                if (Settings.Canvas.UsingWhiteboard) {
                    GridBackgroundCover.Background = new SolidColorBrush(Color.FromRgb(234, 235, 237));
                    WaterMarkTime.Foreground = new SolidColorBrush(Color.FromRgb(22, 41, 36));
                    WaterMarkDate.Foreground = new SolidColorBrush(Color.FromRgb(22, 41, 36));
                    BlackBoardWaterMark.Foreground = new SolidColorBrush(Color.FromRgb(22, 41, 36));
                    isUselightThemeColor = false;
                } else {
                    GridBackgroundCover.Background = new SolidColorBrush(Color.FromRgb(22, 41, 36));
                    WaterMarkTime.Foreground = new SolidColorBrush(Color.FromRgb(234, 235, 237));
                    WaterMarkDate.Foreground = new SolidColorBrush(Color.FromRgb(234, 235, 237));
                    BlackBoardWaterMark.Foreground = new SolidColorBrush(Color.FromRgb(234, 235, 237));
                    isUselightThemeColor = true;
                }

                if (Settings.Canvas.IsShowCursor) {
                    ToggleSwitchShowCursor.IsOn = true;
                    inkCanvas.ForceCursor = true;
                } else {
                    ToggleSwitchShowCursor.IsOn = false;
                    inkCanvas.ForceCursor = false;
                }

                // 初始化压感触屏模式开关状态
                ToggleSwitchEnablePressureTouchMode.IsOn = Settings.Canvas.EnablePressureTouchMode;
                
                // 初始化屏蔽压感开关状态
                ToggleSwitchDisablePressure.IsOn = Settings.Canvas.DisablePressure;

                ComboBoxPenStyle.SelectedIndex = Settings.Canvas.InkStyle;
                BoardComboBoxPenStyle.SelectedIndex = Settings.Canvas.InkStyle;

                ComboBoxEraserSize.SelectedIndex = Settings.Canvas.EraserSize;
                ComboBoxEraserSizeFloatingBar.SelectedIndex = Settings.Canvas.EraserSize;
                BoardComboBoxEraserSize.SelectedIndex = Settings.Canvas.EraserSize;

                ToggleSwitchClearCanvasAndClearTimeMachine.IsOn =
                    Settings.Canvas.ClearCanvasAndClearTimeMachine;
                ToggleSwitchClearCanvasAlsoClearImages.IsOn = Settings.Canvas.ClearCanvasAlsoClearImages;

                switch (Settings.Canvas.EraserShapeType) {
                    case 0: {
                        double k = 1;
                        switch (Settings.Canvas.EraserSize) {
                            case 0:
                                k = 0.5;
                                break;
                            case 1:
                                k = 0.8;
                                break;
                            case 3:
                                k = 1.25;
                                break;
                            case 4:
                                k = 1.5;
                                break;
                        }

                        inkCanvas.EraserShape = new EllipseStylusShape(k * 90, k * 90);
                        inkCanvas.EditingMode = InkCanvasEditingMode.None;
                        break;
                    }
                    case 1: {
                        double k = 1;
                        switch (Settings.Canvas.EraserSize) {
                            case 0:
                                k = 0.7;
                                break;
                            case 1:
                                k = 0.9;
                                break;
                            case 3:
                                k = 1.2;
                                break;
                            case 4:
                                k = 1.5;
                                break;
                        }

                        inkCanvas.EraserShape = new RectangleStylusShape(k * 90 * 0.6, k * 90);
                        inkCanvas.EditingMode = InkCanvasEditingMode.None;
                        break;
                    }
                }

                CheckEraserTypeTab();

                ToggleSwitchHideStrokeWhenSelecting.IsOn = Settings.Canvas.HideStrokeWhenSelecting;

                // 初始化贝塞尔曲线平滑设置
                if (Settings.Canvas.UseAdvancedBezierSmoothing)
                {
                    // 如果启用高级贝塞尔平滑，则禁用原来的FitToCurve
                    ToggleSwitchAdvancedBezierSmoothing.IsOn = true;
                    ToggleSwitchFitToCurve.IsOn = false;
                    drawingAttributes.FitToCurve = false;
                }
                else if (Settings.Canvas.FitToCurve)
                {
                    // 如果启用原来的FitToCurve，则禁用高级贝塞尔平滑
                    ToggleSwitchFitToCurve.IsOn = true;
                    ToggleSwitchAdvancedBezierSmoothing.IsOn = false;
                    drawingAttributes.FitToCurve = true;
                }
                else
                {
                    // 两者都禁用
                    ToggleSwitchFitToCurve.IsOn = false;
                    ToggleSwitchAdvancedBezierSmoothing.IsOn = false;
                    drawingAttributes.FitToCurve = false;
                }

                // 注释掉新的墨迹平滑性能设置，因为UI控件还没有定义
                /*
                // 初始化新的墨迹平滑性能设置
                ToggleSwitchAsyncInkSmoothing.IsOn = Settings.Canvas.UseAsyncInkSmoothing;
                ToggleSwitchHardwareAcceleration.IsOn = Settings.Canvas.UseHardwareAcceleration;
                ComboBoxInkSmoothingQuality.SelectedIndex = Settings.Canvas.InkSmoothingQuality;
                SliderMaxConcurrentTasks.Value = Settings.Canvas.MaxConcurrentSmoothingTasks > 0 ?
                    Settings.Canvas.MaxConcurrentSmoothingTasks : Environment.ProcessorCount;

                // 检查硬件加速支持
                if (!Helpers.InkSmoothingManager.IsHardwareAccelerationSupported())
                {
                    ToggleSwitchHardwareAcceleration.IsEnabled = false;
                    // 可以添加提示文本说明硬件加速不可用
                }
                */
                
                // 初始化直线自动拉直相关设置
                ToggleSwitchAutoStraightenLine.IsOn = Settings.Canvas.AutoStraightenLine;
                AutoStraightenLineThresholdSlider.Value = Settings.Canvas.AutoStraightenLineThreshold;
                // 直线拉直灵敏度也在这里初始化，即使它存储在InkToShape中
                LineStraightenSensitivitySlider.Value = Settings.InkToShape.LineStraightenSensitivity;
                // 初始化高精度直线拉直设置
                ToggleSwitchHighPrecisionLineStraighten.IsOn = Settings.Canvas.HighPrecisionLineStraighten;
                
                // 初始化直线端点吸附相关设置
                ToggleSwitchLineEndpointSnapping.IsOn = Settings.Canvas.LineEndpointSnapping;
                ToggleSwitchCompressPicturesUploaded.IsOn = Settings.Canvas.IsCompressPicturesUploaded;
            } else {
                Settings.Canvas = new Canvas();
            }

            // Palm Eraser
            if (Settings.Canvas != null) {
                ToggleSwitchEnablePalmEraser.IsOn = Settings.Canvas.EnablePalmEraser;
            }

            // Advanced
            if (Settings.Advanced != null) {
                TouchMultiplierSlider.Value = Settings.Advanced.TouchMultiplier;
                FingerModeBoundsWidthSlider.Value = Settings.Advanced.FingerModeBoundsWidth;
                NibModeBoundsWidthSlider.Value = Settings.Advanced.NibModeBoundsWidth;
                ToggleSwitchIsLogEnabled.IsOn = Settings.Advanced.IsLogEnabled;
                ToggleSwitchIsSaveLogByDate.IsOn = Settings.Advanced.IsSaveLogByDate;
                ToggleSwitchIsSecondConfimeWhenShutdownApp.IsOn = Settings.Advanced.IsSecondConfirmWhenShutdownApp;
                ToggleSwitchIsSpecialScreen.IsOn = Settings.Advanced.IsSpecialScreen;
                ToggleSwitchIsQuadIR.IsOn = Settings.Advanced.IsQuadIR;
                ToggleSwitchEraserBindTouchMultiplier.IsOn = Settings.Advanced.EraserBindTouchMultiplier;
                ToggleSwitchIsEnableFullScreenHelper.IsOn = Settings.Advanced.IsEnableFullScreenHelper;
                ToggleSwitchIsEnableEdgeGestureUtil.IsOn = Settings.Advanced.IsEnableEdgeGestureUtil;
                ToggleSwitchIsEnableForceFullScreen.IsOn = Settings.Advanced.IsEnableForceFullScreen;
                ToggleSwitchIsEnableResolutionChangeDetection.IsOn = Settings.Advanced.IsEnableResolutionChangeDetection;
                ToggleSwitchIsEnableDPIChangeDetection.IsOn = Settings.Advanced.IsEnableDPIChangeDetection;
                ToggleSwitchIsEnableAvoidFullScreenHelper.IsOn = Settings.Advanced.IsEnableAvoidFullScreenHelper;
                ToggleSwitchIsAutoBackupBeforeUpdate.IsOn = Settings.Advanced.IsAutoBackupBeforeUpdate;
                if (Settings.Advanced.IsEnableFullScreenHelper) {
                    FullScreenHelper.MarkFullscreenWindowTaskbarList(new WindowInteropHelper(this).Handle, true);
                }
                if (Settings.Advanced.IsEnableAvoidFullScreenHelper)
                {
                    AvoidFullScreenHelper.StartAvoidFullScreen(this);
                }
                if (Settings.Advanced.IsEnableEdgeGestureUtil) {
                    if (OSVersion.GetOperatingSystem() >= OperatingSystem.Windows10)
                        EdgeGestureUtil.DisableEdgeGestures(new WindowInteropHelper(this).Handle, true);
                }
                TouchMultiplierSlider.Visibility =
                    ToggleSwitchIsSpecialScreen.IsOn ? Visibility.Visible : Visibility.Collapsed;
            } else {
                Settings.Advanced = new Advanced();
            }

            // InkToShape
            if (Settings.InkToShape != null) {
                ToggleSwitchEnableInkToShape.IsOn = Settings.InkToShape.IsInkToShapeEnabled;

                ToggleSwitchEnableInkToShapeNoFakePressureRectangle.IsOn =
                    Settings.InkToShape.IsInkToShapeNoFakePressureRectangle;

                ToggleSwitchEnableInkToShapeNoFakePressureTriangle.IsOn =
                    Settings.InkToShape.IsInkToShapeNoFakePressureTriangle;

                ToggleCheckboxEnableInkToShapeTriangle.IsChecked = Settings.InkToShape.IsInkToShapeTriangle;

                ToggleCheckboxEnableInkToShapeRectangle.IsChecked = Settings.InkToShape.IsInkToShapeRectangle;

                ToggleCheckboxEnableInkToShapeRounded.IsChecked = Settings.InkToShape.IsInkToShapeRounded;
                
                // 直线拉直灵敏度在Canvas部分已经初始化，这里不再重复
            } else {
                Settings.InkToShape = new InkToShape();
            }

            // RandSettings
            if (Settings.RandSettings != null) {
                ToggleSwitchDisplayRandWindowNamesInputBtn.IsOn = Settings.RandSettings.DisplayRandWindowNamesInputBtn;
                RandWindowOnceCloseLatencySlider.Value = Settings.RandSettings.RandWindowOnceCloseLatency;
                RandWindowOnceMaxStudentsSlider.Value = Settings.RandSettings.RandWindowOnceMaxStudents;
                ToggleSwitchShowRandomAndSingleDraw.IsOn = Settings.RandSettings.ShowRandomAndSingleDraw;
                ToggleSwitchDirectCallCiRand.IsOn = Settings.RandSettings.DirectCallCiRand;
                RandomDrawPanel.Visibility = Settings.RandSettings.ShowRandomAndSingleDraw ? Visibility.Visible : Visibility.Collapsed;
                SingleDrawPanel.Visibility = Settings.RandSettings.ShowRandomAndSingleDraw ? Visibility.Visible : Visibility.Collapsed;
                
                // 加载自定义点名背景
                UpdatePickNameBackgroundsInComboBox();
                
                // 设置选择的背景索引
                if (Settings.RandSettings.SelectedBackgroundIndex >= ComboBoxPickNameBackground.Items.Count)
                {
                    Settings.RandSettings.SelectedBackgroundIndex = 0;
                }
                ComboBoxPickNameBackground.SelectedIndex = Settings.RandSettings.SelectedBackgroundIndex;
            } else {
                Settings.RandSettings = new RandSettings();
                ToggleSwitchDisplayRandWindowNamesInputBtn.IsOn = Settings.RandSettings.DisplayRandWindowNamesInputBtn;
                RandWindowOnceCloseLatencySlider.Value = Settings.RandSettings.RandWindowOnceCloseLatency;
                RandWindowOnceMaxStudentsSlider.Value = Settings.RandSettings.RandWindowOnceMaxStudents;
                ToggleSwitchDirectCallCiRand.IsOn = Settings.RandSettings.DirectCallCiRand;
            }

            // Automation
            if (Settings.Automation != null) {
                StartOrStoptimerCheckAutoFold();
                ToggleSwitchAutoFoldInEasiNote.IsOn = Settings.Automation.IsAutoFoldInEasiNote;

                ToggleSwitchAutoFoldInEasiCamera.IsOn = Settings.Automation.IsAutoFoldInEasiCamera;

                ToggleSwitchAutoFoldInEasiNote3C.IsOn = Settings.Automation.IsAutoFoldInEasiNote3C;

                ToggleSwitchAutoFoldInEasiNote3.IsOn = Settings.Automation.IsAutoFoldInEasiNote3;

                ToggleSwitchAutoFoldInEasiNote5C.IsOn = Settings.Automation.IsAutoFoldInEasiNote5C;

                ToggleSwitchAutoFoldInSeewoPincoTeacher.IsOn = Settings.Automation.IsAutoFoldInSeewoPincoTeacher;

                ToggleSwitchAutoFoldInHiteTouchPro.IsOn = Settings.Automation.IsAutoFoldInHiteTouchPro;

                ToggleSwitchAutoFoldInHiteLightBoard.IsOn = Settings.Automation.IsAutoFoldInHiteLightBoard;

                ToggleSwitchAutoFoldInHiteCamera.IsOn = Settings.Automation.IsAutoFoldInHiteCamera;

                ToggleSwitchAutoFoldInWxBoardMain.IsOn = Settings.Automation.IsAutoFoldInWxBoardMain;

                ToggleSwitchAutoFoldInOldZyBoard.IsOn = Settings.Automation.IsAutoFoldInOldZyBoard;

                ToggleSwitchAutoFoldInMSWhiteboard.IsOn = Settings.Automation.IsAutoFoldInMSWhiteboard;

                ToggleSwitchAutoFoldInAdmoxWhiteboard.IsOn = Settings.Automation.IsAutoFoldInAdmoxWhiteboard;

                ToggleSwitchAutoFoldInAdmoxBooth.IsOn = Settings.Automation.IsAutoFoldInAdmoxBooth;

                ToggleSwitchAutoFoldInQPoint.IsOn = Settings.Automation.IsAutoFoldInQPoint;

                ToggleSwitchAutoFoldInYiYunVisualPresenter.IsOn = Settings.Automation.IsAutoFoldInYiYunVisualPresenter;

                ToggleSwitchAutoFoldInMaxHubWhiteboard.IsOn = Settings.Automation.IsAutoFoldInMaxHubWhiteboard;

                SettingsPPTInkingAndAutoFoldExplictBorder.Visibility = Visibility.Collapsed;
                if (Settings.Automation.IsAutoFoldInPPTSlideShow) {
                    SettingsPPTInkingAndAutoFoldExplictBorder.Visibility = Visibility.Visible;
                    SettingsShowCanvasAtNewSlideShowStackPanel.Opacity = 0.5;
                    SettingsShowCanvasAtNewSlideShowStackPanel.IsHitTestVisible = false;
                }
                

                ToggleSwitchAutoFoldInPPTSlideShow.IsOn = Settings.Automation.IsAutoFoldInPPTSlideShow;

                ToggleSwitchAutoFoldAfterPPTSlideShow.IsOn = Settings.Automation.IsAutoFoldAfterPPTSlideShow;

                if (Settings.Automation.IsAutoKillEasiNote || Settings.Automation.IsAutoKillPptService ||
                    Settings.Automation.IsAutoKillHiteAnnotation || Settings.Automation.IsAutoKillInkCanvas
                    || Settings.Automation.IsAutoKillICA || Settings.Automation.IsAutoKillIDT ||
                    Settings.Automation.IsAutoKillVComYouJiao
                    || Settings.Automation.IsAutoKillSeewoLauncher2DesktopAnnotation) {
                    timerKillProcess.Start();
                } else {
                    timerKillProcess.Stop();
                }

                ToggleSwitchAutoKillEasiNote.IsOn = Settings.Automation.IsAutoKillEasiNote;

                ToggleSwitchAutoKillHiteAnnotation.IsOn = Settings.Automation.IsAutoKillHiteAnnotation;

                ToggleSwitchAutoKillPptService.IsOn = Settings.Automation.IsAutoKillPptService;

                ToggleSwitchAutoKillVComYouJiao.IsOn = Settings.Automation.IsAutoKillVComYouJiao;

                ToggleSwitchAutoKillInkCanvas.IsOn = Settings.Automation.IsAutoKillInkCanvas;

                ToggleSwitchAutoKillICA.IsOn = Settings.Automation.IsAutoKillICA;

                ToggleSwitchAutoKillIDT.IsOn = Settings.Automation.IsAutoKillIDT;

                ToggleSwitchAutoKillSeewoLauncher2DesktopAnnotation.IsOn =
                    Settings.Automation.IsAutoKillSeewoLauncher2DesktopAnnotation;

                ToggleSwitchAutoSaveStrokesAtClear.IsOn = Settings.Automation.IsAutoSaveStrokesAtClear;

                ToggleSwitchSaveScreenshotsInDateFolders.IsOn = Settings.Automation.IsSaveScreenshotsInDateFolders;

                ToggleSwitchAutoSaveStrokesAtScreenshot.IsOn = Settings.Automation.IsAutoSaveStrokesAtScreenshot;
                
                ToggleSwitchSaveFullPageStrokes.IsOn = Settings.Automation.IsSaveFullPageStrokes;

                SideControlMinimumAutomationSlider.Value = Settings.Automation.MinimumAutomationStrokeNumber;

                AutoSavedStrokesLocation.Text = Settings.Automation.AutoSavedStrokesLocation;
                ToggleSwitchAutoDelSavedFiles.IsOn = Settings.Automation.AutoDelSavedFiles;
                ComboBoxAutoDelSavedFilesDaysThreshold.Text =
                    Settings.Automation.AutoDelSavedFilesDaysThreshold.ToString();
                    
                // 加载退出收纳模式自动切换至批注模式设置
                ToggleSwitchAutoEnterAnnotationModeWhenExitFoldMode.IsOn = Settings.Automation.IsAutoEnterAnnotationModeWhenExitFoldMode;
            } else {
                Settings.Automation = new Automation();
            }

            // auto align
            if (BtnPPTSlideShowEnd.Visibility == Visibility.Visible) {
                ViewboxFloatingBarMarginAnimation(60);
            } else {
                ViewboxFloatingBarMarginAnimation(100, true);
            }
        }
    }
}