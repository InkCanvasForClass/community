using H.NotifyIcon;
using Ink_Canvas.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OSVersionExtension;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using File = System.IO.File;
using OperatingSystem = OSVersionExtension.OperatingSystem;
using WinForms = System.Windows.Forms;

namespace Ink_Canvas
{
    public partial class MainWindow : Window
    {
        /// <summary>
        /// 从配置文件加载用户设置并将其应用到主窗口和相关控件的状态（包括启动、外观、画布、手势、PPT、自动化等各项配置）。
        /// </summary>
        /// <param name="isStartup">指示当前为应用启动阶段；为 true 时按启动流程应用启动相关设置（例如触发启动专用动作和启动时的行为）。</param>
        /// <summary>
        /// 从当前配置文件重新加载设置并应用到界面（热重载），不触发启动逻辑与自动更新检查。
        /// 用于配置文件切换后立即生效。
        /// </summary>
        public void ReloadSettingsFromFile()
        {
            LoadSettings(false, skipAutoUpdateCheck: true);
        }

        /// <param name="skipAutoUpdateCheck">指示是否跳过自动更新检查；为 true 时不会在加载设置后执行自动更新检测。</param>
        private void LoadSettings(bool isStartup = false, bool skipAutoUpdateCheck = false)
        {
            try
            {
                if (File.Exists(App.RootPath + settingsFileName))
                {
                    try
                    {
                        string text = File.ReadAllText(App.RootPath + settingsFileName);
                        Settings = JsonConvert.DeserializeObject<Settings>(text);

                        if (Settings != null)
                        {
                            CleanupObsoleteSettings(text);
                        }

                        // 验证设置是否成功加载
                        if (Settings == null)
                        {
                            LogHelper.WriteLogToFile("配置文件解析失败，尝试从备份恢复", LogHelper.LogType.Warning);
                            if (AutoBackupManager.TryRestoreFromBackup())
                            {
                                // 重新尝试加载
                                text = File.ReadAllText(App.RootPath + settingsFileName);
                                Settings = JsonConvert.DeserializeObject<Settings>(text);
                                if (Settings != null)
                                {
                                    // 清理过期配置项
                                    CleanupObsoleteSettings(text);
                                }
                            }

                            // 如果仍然失败，使用默认设置
                            if (Settings == null)
                            {
                                LogHelper.WriteLogToFile("从备份恢复失败，使用默认设置", LogHelper.LogType.Warning);
                                BtnResetToSuggestion_Click(null, null);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"配置文件加载失败: {ex.Message}", LogHelper.LogType.Error);

                        // 尝试从备份恢复
                        LogHelper.WriteLogToFile("尝试从备份恢复配置文件", LogHelper.LogType.Warning);
                        if (AutoBackupManager.TryRestoreFromBackup())
                        {
                            try
                            {
                                string text = File.ReadAllText(App.RootPath + settingsFileName);
                                Settings = JsonConvert.DeserializeObject<Settings>(text);
                                if (Settings != null)
                                {
                                    // 清理过期配置项
                                    CleanupObsoleteSettings(text);
                                }
                            }
                            catch (Exception restoreEx)
                            {
                                LogHelper.WriteLogToFile($"从备份恢复后重新加载失败: {restoreEx.Message}", LogHelper.LogType.Error);
                                BtnResetToSuggestion_Click(null, null);
                            }
                        }

                        // 如果仍然失败，使用默认设置
                        if (Settings == null)
                        {
                            LogHelper.WriteLogToFile("从备份恢复失败，使用默认设置", LogHelper.LogType.Warning);
                            BtnResetToSuggestion_Click(null, null);
                        }
                    }
                }
                else
                {
                    LogHelper.WriteLogToFile("配置文件不存在，尝试从备份恢复", LogHelper.LogType.Warning);
                    if (AutoBackupManager.TryRestoreFromBackup())
                    {
                        try
                        {
                            string text = File.ReadAllText(App.RootPath + settingsFileName);
                            Settings = JsonConvert.DeserializeObject<Settings>(text);
                            if (Settings != null)
                            {
                                // 清理过期配置项
                                CleanupObsoleteSettings(text);
                            }
                        }
                        catch (Exception restoreEx)
                        {
                            LogHelper.WriteLogToFile($"从备份恢复后加载失败: {restoreEx.Message}", LogHelper.LogType.Error);
                            BtnResetToSuggestion_Click(null, null);
                        }
                    }
                    else
                    {
                        // 备份恢复失败（备份目录不存在等），使用默认设置
                        LogHelper.WriteLogToFile("备份恢复失败，使用默认设置", LogHelper.LogType.Warning);
                        BtnResetToSuggestion_Click(null, null);
                    }

                    // 如果仍然失败，使用默认设置
                    if (Settings == null)
                    {
                        LogHelper.WriteLogToFile("从备份恢复失败，使用默认设置", LogHelper.LogType.Warning);
                        BtnResetToSuggestion_Click(null, null);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile(ex.ToString(), LogHelper.LogType.Error);
            }

            try
            {
                if (Settings?.Appearance != null)
                {
                    var preferredLanguage = Settings.Appearance.Language ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(preferredLanguage))
                    {
                        LocalizationHelper.TrySetCulture(preferredLanguage);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"从配置应用界面语言失败: {ex.Message}", LogHelper.LogType.Error);
            }

            try
            {
                ProcessProtectionManager.ApplyFromSettings();
            }
            catch
            {
            }

            // Startup
            if (isStartup)
            {
                CursorIcon_Click(null, null);
            }

            try
            {
                if (Settings?.Startup != null)
                {
                }
            }
            catch
            {
            }

            if (Settings.Startup != null)
            {
                if (isStartup)
                {
                    if (Settings.Automation.AutoDelSavedFiles)
                    {
                        DelAutoSavedFiles.DeleteFilesOlder(Settings.Automation.AutoSavedStrokesLocation, Settings.Automation.AutoDelSavedFilesDaysThreshold);
                    }
                }

                if (Settings.Startup.IsEnableNibMode)
                {
                    ToggleSwitchEnableNibMode.IsOn = true;
                    BoardToggleSwitchEnableNibMode.IsOn = true;
                    BoundsWidth = Settings.Advanced.NibModeBoundsWidth;
                }
                else
                {
                    ToggleSwitchEnableNibMode.IsOn = false;
                    BoardToggleSwitchEnableNibMode.IsOn = false;
                    BoundsWidth = Settings.Advanced.FingerModeBoundsWidth;
                }

                // 设置自动更新相关选项
                if (Settings.Startup.IsAutoUpdate && !skipAutoUpdateCheck)
                {
                    if (isStartup)
                    {
                        LogHelper.WriteLogToFile("AutoUpdate | Running auto-update check at startup");
                        AutoUpdate();
                    }
                    else
                    {
                        LogHelper.WriteLogToFile("AutoUpdate | Running auto-update check after settings change");
                        AutoUpdate();
                    }
                }
            }
            else
            {
                Settings.Startup = new Startup();
                Settings.Startup.IsEnableNibMode = false; // 默认关闭笔尖模式
                ToggleSwitchEnableNibMode.IsOn = false; // 默认关闭笔尖模式
                BoardToggleSwitchEnableNibMode.IsOn = false; // 默认关闭笔尖模式
                BoundsWidth = Settings.Advanced.FingerModeBoundsWidth; // 使用手指模式边界宽度
            }

            // 恢复崩溃后操作设置
            if (Settings.Startup != null)
            {
                // 恢复崩溃后操作选项
                if (Settings.Startup.CrashAction == 0)
                {
                    App.CrashAction = App.CrashActionType.SilentRestart;
                }
                else
                {
                    App.CrashAction = App.CrashActionType.NoAction;
                }
            }

            // Appearance
            if (Settings.Appearance != null)
            {
                if (!Settings.Appearance.IsEnableDisPlayNibModeToggler)
                {
                    NibModeSimpleStackPanel.Visibility = Visibility.Collapsed;
                    BoardNibModeSimpleStackPanel.Visibility = Visibility.Collapsed;
                }
                else
                {
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
                }

                switch (Settings.Appearance.UnFoldButtonImageType)
                {
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

                // 设置主题下拉框

                _suppressChickenSoupSourceSelectionChanged = true;
                try
                {
                }
                finally
                {
                    Dispatcher.BeginInvoke(
                        (Action)(() => { _suppressChickenSoupSourceSelectionChanged = false; }),
                        DispatcherPriority.ContextIdle);
                }

                var _taskbar = (TaskbarIcon)Application.Current.Resources["TaskbarTrayIcon"];
                _taskbar.Visibility = Settings.Appearance.EnableTrayIcon ? Visibility.Visible : Visibility.Collapsed;

                ViewboxFloatingBar.Opacity = Settings.Appearance.ViewboxFloatingBarOpacityValue;

                // 初始化浮动栏透明度滑块值

                if (Settings.Appearance.EnableViewboxBlackBoardScaleTransform) // 画板 UI 缩放 80%
                {
                    //ViewboxBlackboardLeftSideScaleTransform.ScaleX = 0.8;
                    //ViewboxBlackboardLeftSideScaleTransform.ScaleY = 0.8;
                    ViewboxBlackboardCenterSideScaleTransform.ScaleX = 0.8;
                    ViewboxBlackboardCenterSideScaleTransform.ScaleY = 0.8;
                    //ViewboxBlackboardRightSideScaleTransform.ScaleX = 0.8;
                    //ViewboxBlackboardRightSideScaleTransform.ScaleY = 0.8;

                }
                else
                {
                    //ViewboxBlackboardLeftSideScaleTransform.ScaleX = 1;
                    //ViewboxBlackboardLeftSideScaleTransform.ScaleY = 1;
                    ViewboxBlackboardCenterSideScaleTransform.ScaleX = 1;
                    ViewboxBlackboardCenterSideScaleTransform.ScaleY = 1;
                    //ViewboxBlackboardRightSideScaleTransform.ScaleX = 1;
                    //ViewboxBlackboardRightSideScaleTransform.ScaleY = 1;

                }

                if (Settings.Appearance.IsTransparentButtonBackground)
                {
                    BtnExit.Background = new SolidColorBrush(StringToColor("#7F909090"));
                }
                else
                {
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
                {
                    Settings.Appearance.FloatingBarImg = 0;
                }


                // 更新浮动栏图标
                UpdateFloatingBarIcon();

                // 浮动栏按钮显示控制开关初始化

                // 初始化快捷调色盘指示器
                UpdateQuickColorPaletteIndicator(inkCanvas.DefaultDrawingAttributes.Color);

                // 应用浮动栏按钮可见性设置
                UpdateFloatingBarButtonsVisibility();

                // 更新浮动栏图标
                UpdateFloatingBarIcons();

                SystemEvents_UserPreferenceChanged(null, null);
            }
            else
            {
                Settings.Appearance = new Appearance();
            }

            // PowerPointSettings
            if (Settings.PowerPointSettings != null)
            {


                if (Settings.PowerPointSettings.PowerPointSupport)
                {
                    // PPT监控将在Window_Loaded中启动
                }
                else
                {
                    // PPT监控将保持停止状态
                }

                // PPT时间显示胶囊设置
                {
                    int position = Settings.PowerPointSettings.PPTTimeCapsulePosition;
                    if (position < 0 || position > 2)
                    {
                        position = 1;
                    }
                }

                // -- new --

                var dops = Settings.PowerPointSettings.PPTButtonsDisplayOption.ToString();
                var dopsc = dops.ToCharArray();
                if ((dopsc[0] == '1' || dopsc[0] == '2') && (dopsc[1] == '1' || dopsc[1] == '2') &&
                    (dopsc[2] == '1' || dopsc[2] == '2') && (dopsc[3] == '1' || dopsc[3] == '2'))
                {
                }
                else
                {
                    Settings.PowerPointSettings.PPTButtonsDisplayOption = 2222;
                    SaveSettingsToFile();
                }

                var sops = Settings.PowerPointSettings.PPTSButtonsOption.ToString();
                var sopsc = sops.ToCharArray();
                if ((sopsc[0] == '1' || sopsc[0] == '2') && (sopsc[1] == '1' || sopsc[1] == '2') &&
                    (sopsc[2] == '1' || sopsc[2] == '2'))
                {
                }
                else
                {
                    Settings.PowerPointSettings.PPTSButtonsOption = 221;
                    SaveSettingsToFile();
                }

                var bops = Settings.PowerPointSettings.PPTBButtonsOption.ToString();
                var bopsc = bops.ToCharArray();
                if ((bopsc[0] == '1' || bopsc[0] == '2') && (bopsc[1] == '1' || bopsc[1] == '2') &&
                    (bopsc[2] == '1' || bopsc[2] == '2'))
                {
                }
                else
                {
                    Settings.PowerPointSettings.PPTBButtonsOption = 121;
                    SaveSettingsToFile();
                }





                // 初始化PPT翻页按钮透明度滑块值，根据半透明选项设置默认值
                // 重用之前定义的sopsc和bopsc变量
                bool isSideHalfOpacity = sopsc.Length >= 2 && sopsc[1] == '2';
                // 如果透明度为0或未设置，根据半透明选项设置默认值
                if (Settings.PowerPointSettings.PPTLSButtonOpacity == 0.0 ||
                    (Settings.PowerPointSettings.PPTLSButtonOpacity == 1.0 && isSideHalfOpacity))
                {
                    Settings.PowerPointSettings.PPTLSButtonOpacity = isSideHalfOpacity ? 0.5 : 1.0;
                }
                if (Settings.PowerPointSettings.PPTRSButtonOpacity == 0.0 ||
                    (Settings.PowerPointSettings.PPTRSButtonOpacity == 1.0 && isSideHalfOpacity))
                {
                    Settings.PowerPointSettings.PPTRSButtonOpacity = isSideHalfOpacity ? 0.5 : 1.0;
                }

                bool isBottomHalfOpacity = bopsc.Length >= 2 && bopsc[1] == '2';
                // 如果透明度为0或未设置，根据半透明选项设置默认值
                if (Settings.PowerPointSettings.PPTLBButtonOpacity == 0.0 ||
                    (Settings.PowerPointSettings.PPTLBButtonOpacity == 1.0 && isBottomHalfOpacity))
                {
                    Settings.PowerPointSettings.PPTLBButtonOpacity = isBottomHalfOpacity ? 0.5 : 1.0;
                }
                if (Settings.PowerPointSettings.PPTRBButtonOpacity == 0.0 ||
                    (Settings.PowerPointSettings.PPTRBButtonOpacity == 1.0 && isBottomHalfOpacity))
                {
                    Settings.PowerPointSettings.PPTRBButtonOpacity = isBottomHalfOpacity ? 0.5 : 1.0;
                }

                UpdatePPTBtnSlidersStatus();

                UpdatePPTBtnPreview();

                // -- new --

            }
            else
            {
                Settings.PowerPointSettings = new PowerPointSettings();
            }

            // Gesture
            if (Settings.Gesture != null)
            {
                if (Settings.Gesture.AutoSwitchTwoFingerGesture)
                {
                    if (Topmost)
                    {
                        Settings.Gesture.IsEnableTwoFingerTranslate = false;
                    }
                    else
                    {
                        Settings.Gesture.IsEnableTwoFingerTranslate = true;
                    }
                }

                CheckEnableTwoFingerGestureBtnColorPrompt();
            }
            else
            {
                Settings.Gesture = new Gesture();
            }

            // Canvas
            if (Settings.Canvas != null)
            {
                drawingAttributes.Height = Settings.Canvas.InkWidth;
                drawingAttributes.Width = Settings.Canvas.InkWidth;

                InkWidthSlider.Value = Settings.Canvas.InkWidth * 2;
                HighlighterWidthSlider.Value = Settings.Canvas.HighlighterWidth;

                int alpha = (int)Settings.Canvas.InkAlpha;
                if (alpha < 0) alpha = 0; if (alpha > 255) alpha = 255;
                var inkColor = drawingAttributes.Color;
                drawingAttributes.Color = Color.FromArgb((byte)alpha, inkColor.R, inkColor.G, inkColor.B);
                inkCanvas.DefaultDrawingAttributes.Color = drawingAttributes.Color;
                if (InkAlphaSlider != null) InkAlphaSlider.Value = alpha;
                if (BoardInkAlphaSlider != null) BoardInkAlphaSlider.Value = alpha;



                if (Settings.Canvas.UsingWhiteboard)
                {
                    GridBackgroundCover.Background = new SolidColorBrush(Color.FromRgb(234, 235, 237));
                    WaterMarkTime.Foreground = new SolidColorBrush(Color.FromRgb(22, 41, 36));
                    WaterMarkDate.Foreground = new SolidColorBrush(Color.FromRgb(22, 41, 36));
                    BlackBoardWaterMark.Foreground = new SolidColorBrush(Color.FromRgb(22, 41, 36));
                    isUselightThemeColor = false;
                }
                else
                {
                    GridBackgroundCover.Background = new SolidColorBrush(Color.FromRgb(22, 41, 36));
                    WaterMarkTime.Foreground = new SolidColorBrush(Color.FromRgb(234, 235, 237));
                    WaterMarkDate.Foreground = new SolidColorBrush(Color.FromRgb(234, 235, 237));
                    BlackBoardWaterMark.Foreground = new SolidColorBrush(Color.FromRgb(234, 235, 237));
                    isUselightThemeColor = true;
                }

                if (Settings.Canvas.IsShowCursor)
                {
                    inkCanvas.ForceCursor = true;
                }
                else
                {
                    inkCanvas.ForceCursor = false;
                }


                // 初始化屏蔽压感开关状态
                inkCanvas.DefaultDrawingAttributes.IgnorePressure = Settings.Canvas.DisablePressure;


                if (Settings.Canvas.EnableVelocityBrushTip)
                {
                    Settings.Canvas.InkStyle = 3;
                    Settings.Canvas.EnableVelocityBrushTip = false;
                }

                if (Settings.Canvas.InkStyle < 0 || Settings.Canvas.InkStyle > 3)
                    Settings.Canvas.InkStyle = 0;

                int penStyleUi = PenStyleUiIndexFromInkStyle(Settings.Canvas.InkStyle);
                ComboBoxPenStyle.SelectedIndex = penStyleUi;
                BoardComboBoxPenStyle.SelectedIndex = penStyleUi;

                ComboBoxEraserSizeFloatingBar.SelectedIndex = Settings.Canvas.EraserSize;
                BoardComboBoxEraserSize.SelectedIndex = Settings.Canvas.EraserSize;


                switch (Settings.Canvas.EraserShapeType)
                {
                    case 0:
                        {
                            double k = 1;
                            switch (Settings.Canvas.EraserSize)
                            {
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
                    case 1:
                        {
                            double k = 1;
                            switch (Settings.Canvas.EraserSize)
                            {
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


                // 初始化贝塞尔曲线平滑设置
                if (Settings.Canvas.UseAdvancedBezierSmoothing)
                {
                    // 如果启用高级贝塞尔平滑，则禁用原来的FitToCurve
                    drawingAttributes.FitToCurve = false;
                }
                else if (Settings.Canvas.FitToCurve)
                {
                    // 如果启用原来的FitToCurve，则禁用高级贝塞尔平滑
                    drawingAttributes.FitToCurve = true;
                }
                else
                {
                    // 两者都禁用
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
                // 直线拉直灵敏度也在这里初始化，即使它存储在InkToShape中
                // 初始化高精度直线拉直设置

                // 初始化直线端点吸附相关设置
            }
            else
            {
                Settings.Canvas = new Canvas();
            }

            // Palm Eraser
            if (Settings.Canvas != null)
            {
            }

            // Advanced
            if (Settings.Advanced != null)
            {
                if (Settings.Advanced.IsEnableFullScreenHelper)
                {
                    FullScreenHelper.MarkFullscreenWindowTaskbarList(new WindowInteropHelper(this).Handle, true);
                }
                if (Settings.Advanced.IsEnableAvoidFullScreenHelper)
                {
                    AvoidFullScreenHelper.StartAvoidFullScreen(this);
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (isLoaded)
                        {
                            MoveWindow(new WindowInteropHelper(this).Handle, 0, 0,
                                WinForms.Screen.PrimaryScreen.Bounds.Width, WinForms.Screen.PrimaryScreen.Bounds.Height, true);
                        }
                    }), DispatcherPriority.ApplicationIdle);
                }
                if (Settings.Advanced.IsEnableEdgeGestureUtil)
                {
                    if (OSVersion.GetOperatingSystem() >= OperatingSystem.Windows10)
                        EdgeGestureUtil.DisableEdgeGestures(new WindowInteropHelper(this).Handle, true);
                }
                
            }
            else
            {
                Settings.Advanced = new Advanced();
            }

            // InkToShape
            if (Settings.InkToShape != null)
            {








                // 直线拉直灵敏度在Canvas部分已经初始化，这里不再重复
            }
            else
            {
                Settings.InkToShape = new InkToShape();
            }

            // RandSettings
            if (Settings.RandSettings != null)
            {
                ToggleSwitchDisplayRandWindowNamesInputBtn.IsOn = Settings.RandSettings.DisplayRandWindowNamesInputBtn;
                RandWindowOnceCloseLatencySlider.Value = Settings.RandSettings.RandWindowOnceCloseLatency;
                RandWindowOnceMaxStudentsSlider.Value = Settings.RandSettings.RandWindowOnceMaxStudents;
                ToggleSwitchShowRandomAndSingleDraw.IsOn = Settings.RandSettings.ShowRandomAndSingleDraw;
                ToggleSwitchEnableQuickDraw.IsOn = Settings.RandSettings.EnableQuickDraw;
                ToggleSwitchExternalCaller.IsOn = Settings.RandSettings.DirectCallCiRand;
                ComboBoxExternalCallerType.SelectedIndex = Settings.RandSettings.ExternalCallerType;
                BoardRandomDrawToolBtn.Visibility = Settings.RandSettings.ShowRandomAndSingleDraw ? Visibility.Visible : Visibility.Collapsed;
                BoardSingleDrawToolBtn.Visibility = Settings.RandSettings.ShowRandomAndSingleDraw ? Visibility.Visible : Visibility.Collapsed;

                // 计时器设置
                ToggleSwitchUseLegacyTimerUI.IsOn = Settings.RandSettings.UseLegacyTimerUI;
                ToggleSwitchUseNewStyleUI.IsOn = Settings.RandSettings.UseNewStyleUI;
                ToggleSwitchEnableOvertimeCountUp.IsOn = Settings.RandSettings.EnableOvertimeCountUp;

                // 新点名UI设置
                ToggleSwitchUseNewRollCallUI.IsOn = Settings.RandSettings.UseNewRollCallUI;
                ToggleSwitchEnableMLAvoidance.IsOn = Settings.RandSettings.EnableMLAvoidance;
                MLAvoidanceHistorySlider.Value = Settings.RandSettings.MLAvoidanceHistoryCount;
                MLAvoidanceWeightSlider.Value = Settings.RandSettings.MLAvoidanceWeight;

                bool canEnableRedText = Settings.RandSettings.EnableOvertimeCountUp && Settings.RandSettings.EnableOvertimeRedText;
                ToggleSwitchEnableOvertimeRedText.IsOn = canEnableRedText;
                if (!canEnableRedText)
                {
                    Settings.RandSettings.EnableOvertimeRedText = false;
                }

                TimerVolumeSlider.Value = Settings.RandSettings.TimerVolume;

                // 渐进提醒设置
                ToggleSwitchEnableProgressiveReminder.IsOn = Settings.RandSettings.EnableProgressiveReminder;
                ProgressiveReminderVolumeSlider.Value = Settings.RandSettings.ProgressiveReminderVolume;

                // 加载自定义点名背景
                UpdatePickNameBackgroundsInComboBox();

                // 设置选择的背景索引
                if (Settings.RandSettings.SelectedBackgroundIndex >= ComboBoxPickNameBackground.Items.Count)
                {
                    Settings.RandSettings.SelectedBackgroundIndex = 0;
                }
                ComboBoxPickNameBackground.SelectedIndex = Settings.RandSettings.SelectedBackgroundIndex;
            }
            else
            {
                Settings.RandSettings = new RandSettings();
                ToggleSwitchDisplayRandWindowNamesInputBtn.IsOn = Settings.RandSettings.DisplayRandWindowNamesInputBtn;
                RandWindowOnceCloseLatencySlider.Value = Settings.RandSettings.RandWindowOnceCloseLatency;
                RandWindowOnceMaxStudentsSlider.Value = Settings.RandSettings.RandWindowOnceMaxStudents;
                ToggleSwitchEnableQuickDraw.IsOn = Settings.RandSettings.EnableQuickDraw;
                ToggleSwitchExternalCaller.IsOn = Settings.RandSettings.DirectCallCiRand;
                ComboBoxExternalCallerType.SelectedIndex = Settings.RandSettings.ExternalCallerType;
                ToggleSwitchUseLegacyTimerUI.IsOn = Settings.RandSettings.UseLegacyTimerUI;
                ToggleSwitchUseNewStyleUI.IsOn = Settings.RandSettings.UseNewStyleUI;
                ToggleSwitchEnableOvertimeCountUp.IsOn = Settings.RandSettings.EnableOvertimeCountUp;

                bool canEnableRedText = Settings.RandSettings.EnableOvertimeCountUp && Settings.RandSettings.EnableOvertimeRedText;
                ToggleSwitchEnableOvertimeRedText.IsOn = canEnableRedText;
                if (!canEnableRedText)
                {
                    Settings.RandSettings.EnableOvertimeRedText = false;
                }

                TimerVolumeSlider.Value = Settings.RandSettings.TimerVolume;

                // 渐进提醒设置
                ToggleSwitchEnableProgressiveReminder.IsOn = Settings.RandSettings.EnableProgressiveReminder;
                ProgressiveReminderVolumeSlider.Value = Settings.RandSettings.ProgressiveReminderVolume;
            }

            // ModeSettings
            if (Settings.ModeSettings != null)
            {
                ToggleSwitchMode.IsOn = Settings.ModeSettings.IsPPTOnlyMode;

                // 根据加载的配置状态执行相应的窗口显示/隐藏逻辑
                if (isStartup && Settings.ModeSettings.IsPPTOnlyMode)
                {
                    // 启动时如果是仅PPT模式，隐藏主窗口
                    Hide();
                    LogHelper.WriteLogToFile("启动时检测到仅PPT模式，主窗口已隐藏", LogHelper.LogType.Event);
                }
            }
            else
            {
                Settings.ModeSettings = new ModeSettings();
                ToggleSwitchMode.IsOn = false;
            }

            // Automation
            if (Settings.Automation != null)
            {
                StartOrStoptimerCheckAutoFold();

















                if (Settings.Automation.IsAutoFoldInPPTSlideShow)
                {
                }





                if (Settings.Automation.IsAutoKillEasiNote || Settings.Automation.IsAutoKillPptService ||
                    Settings.Automation.IsAutoKillHiteAnnotation || Settings.Automation.IsAutoKillInkCanvas
                    || Settings.Automation.IsAutoKillICA || Settings.Automation.IsAutoKillIDT ||
                    Settings.Automation.IsAutoKillVComYouJiao
                    || Settings.Automation.IsAutoKillSeewoLauncher2DesktopAnnotation)
                {
                    timerKillProcess.Start();
                }
                else
                {
                    timerKillProcess.Stop();
                }

                // 加载定时保存墨迹设置
                if (Settings.Automation.AutoDelSavedFiles)
                {
                    DelAutoSavedFiles.DeleteFilesOlder(Settings.Automation.AutoSavedStrokesLocation, Settings.Automation.AutoDelSavedFilesDaysThreshold);
                }

                // 加载退出收纳模式自动切换至批注模式设置

                // 加载退出白板时自动收纳设置
            }
            else
            {
                Settings.Automation = new Automation();
            }

            // auto align
            if (BtnPPTSlideShowEnd.Visibility == Visibility.Visible)
            {
                ViewboxFloatingBarMarginAnimation(60);
            }
            else
            {
                ViewboxFloatingBarMarginAnimation(100, true);
            }

            // 加载墨迹渐隐设置
            LoadInkFadeSettings();

            // 加载画笔自动恢复设置
            LoadBrushAutoRestoreSettings();

            // 刷新配置文件列表
            try { RefreshConfigProfileList(); } catch (Exception ex) { LogHelper.WriteLogToFile($"刷新配置文件列表失败: {ex.Message}", LogHelper.LogType.Warning); }
        }

        /// <summary>
        /// 将画笔自动恢复相关的设置应用到界面控件并在启用时初始化自动恢复定时器。
        /// </summary>
        /// <remarks>
        /// 会将 Settings.Canvas 中的 BrushAutoRestore 配置同步到对应的切换开关、时间文本框、颜色下拉框、宽度和透明度滑块；当颜色缺失时会使用默认值 `#FFFF0000`，当宽度无效时使用默认值 `5`。若功能被启用，会初始化并启动定时器以执行自动恢复任务。方法执行过程中会记录加载结果或错误信息到日志。
        /// </remarks>
        private void LoadBrushAutoRestoreSettings()
        {
            try
            {







                // 如果功能已启用，初始化并启动定时器
                if (Settings.Canvas.EnableBrushAutoRestore)
                {
                    InitBrushAutoRestoreTimer();
                    ScheduleBrushAutoRestore();
                }

                LogHelper.WriteLogToFile("画笔自动恢复设置已加载", LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"加载画笔自动恢复设置时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 加载墨迹渐隐设置
        /// </summary>
        private void LoadInkFadeSettings()
        {
            try
            {

                // 同步批注子面板中的开关状态
                if (ToggleSwitchInkFadeInPanel != null)
                {
                    ToggleSwitchInkFadeInPanel.IsOn = Settings.Canvas.EnableInkFade;
                }

                // 同步普通画笔面板中的开关状态
                if (ToggleSwitchInkFadeInPanel2 != null)
                {
                    ToggleSwitchInkFadeInPanel2.IsOn = Settings.Canvas.EnableInkFade;
                }




                // 同步墨迹渐隐管理器的状态
                if (_inkFadeManager != null)
                {
                    _inkFadeManager.IsEnabled = Settings.Canvas.EnableInkFade;
                    _inkFadeManager.UpdateFadeTime(Settings.Canvas.InkFadeTime);
                }


                // 根据设置更新墨迹渐隐控制开关的可见性
                UpdateInkFadeControlVisibility();

                LogHelper.WriteLogToFile("墨迹渐隐设置已加载", LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"加载墨迹渐隐设置时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 清理配置文件中的过期设置
        /// </summary>
        /// <param name="userConfigJson">用户配置的JSON字符串</param>
        /// <remarks>
        /// 清理过期设置时：
        /// 1. 创建默认配置对象
        /// 2. 将默认配置和用户配置都序列化为JObject
        /// 3. 递归比较并删除用户配置中多余的键
        /// 4. 如果有清理操作，重新反序列化并保存
        /// 5. 记录清理结果到日志
        /// </remarks>
        private void CleanupObsoleteSettings(string userConfigJson)
        {
            try
            {
                // 创建默认配置对象
                Settings defaultSettings = new Settings();

                // 将默认配置和用户配置都序列化为JObject
                JObject defaultConfigObj = JObject.FromObject(defaultSettings); EnsureDefaultConfigSchemaIncludesIgnoredNullKeys(defaultConfigObj);
                JObject userConfigObj = JObject.Parse(userConfigJson);

                // 记录是否有清理操作
                bool hasChanges = false;

                // 递归比较并删除用户配置中多余的键
                RemoveObsoleteProperties(userConfigObj, defaultConfigObj, ref hasChanges);

                // 如果有清理操作，重新反序列化并保存
                if (hasChanges)
                {
                    string cleanedJson = userConfigObj.ToString(Formatting.Indented);
                    Settings = JsonConvert.DeserializeObject<Settings>(cleanedJson);
                    SaveSettingsToFile();
                    LogHelper.WriteLogToFile("已清理过期配置项", LogHelper.LogType.Event);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"清理过期配置时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 递归删除用户配置中多余的属性
        /// </summary>
        /// <param name="userObj">用户配置的JObject</param>
        /// <param name="defaultObj">默认配置的JObject</param>
        /// <param name="hasChanges">是否有变更的引用标志</param>
        /// <remarks>
        /// 递归删除多余属性时：
        /// 1. 检查用户配置和默认配置是否为空
        /// 2. 获取需要删除的键列表
        /// 3. 遍历用户配置的所有属性
        /// 4. 如果默认配置中不存在该属性，标记为删除
        /// 5. 如果两个属性都是对象类型，递归比较
        /// 6. 处理数组中的对象（如自定义图标列表等）
        /// 7. 删除标记的键
        /// 8. 设置变更标志
        /// </remarks>
        private static void EnsureDefaultConfigSchemaIncludesIgnoredNullKeys(JObject defaultConfigObj)
        {
            if (defaultConfigObj == null) return;
            if (defaultConfigObj["appearance"] is JObject appearance && !appearance.ContainsKey("hitokotoCategories"))
                appearance["hitokotoCategories"] = JValue.CreateNull();
        }

        private void RemoveObsoleteProperties(JObject userObj, JObject defaultObj, ref bool hasChanges)
        {
            if (userObj == null || defaultObj == null)
                return;

            // 获取需要删除的键列表（避免在遍历时修改集合）
            List<string> keysToRemove = new List<string>();

            foreach (var property in userObj.Properties())
            {
                string propertyName = property.Name;

                // 如果默认配置中不存在该属性，标记为删除
                if (!defaultObj.ContainsKey(propertyName))
                {
                    keysToRemove.Add(propertyName);
                    continue;
                }

                // 如果两个属性都是对象类型，递归比较
                JToken userValue = property.Value;
                JToken defaultValue = defaultObj[propertyName];

                if (userValue != null && defaultValue != null)
                {
                    if (userValue.Type == JTokenType.Object && defaultValue.Type == JTokenType.Object)
                    {
                        RemoveObsoleteProperties(userValue as JObject, defaultValue as JObject, ref hasChanges);
                    }
                    // 处理数组中的对象（如自定义图标列表等）
                    else if (userValue.Type == JTokenType.Array && defaultValue.Type == JTokenType.Array)
                    {
                        JArray userArray = userValue as JArray;
                        JArray defaultArray = defaultValue as JArray;

                        if (userArray != null && defaultArray != null && userArray.Count > 0 && defaultArray.Count > 0)
                        {
                            // 如果数组元素是对象，比较第一个元素的属性结构
                            if (userArray[0].Type == JTokenType.Object && defaultArray[0].Type == JTokenType.Object)
                            {
                                for (int i = 0; i < userArray.Count; i++)
                                {
                                    if (userArray[i] is JObject userItemObj && defaultArray[0] is JObject defaultItemObj)
                                    {
                                        RemoveObsoleteProperties(userItemObj, defaultItemObj, ref hasChanges);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // 删除标记的键
            foreach (string key in keysToRemove)
            {
                userObj.Remove(key);
                hasChanges = true;
            }
        }
    }
}
