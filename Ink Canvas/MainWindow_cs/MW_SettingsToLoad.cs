using Ink_Canvas.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OSVersionExtension;
using System;
using System.Collections.Generic;
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
    public partial class MainWindow : Ink_Canvas.Helpers.PerformanceTransparentWin
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
                        DelAutoSavedFiles.DeleteFilesOlder(Settings.Automation.AutoSavedStrokesLocation,
                            Settings.Automation.AutoDelSavedFilesDaysThreshold);
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
                        _pendingStartupAutoUpdateCheck = true;
                        LogHelper.WriteLogToFile("AutoUpdate | Startup check deferred until UI is stable");
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
                Settings.Startup.IsEnableNibMode = false;
                ToggleSwitchEnableNibMode.IsOn = false;
                BoardToggleSwitchEnableNibMode.IsOn = false;
                BoundsWidth = Settings.Advanced.FingerModeBoundsWidth;
            }

            if (Settings.Startup != null)
            {
                if (Settings.Startup.CrashAction == 0)
                {
                    App.CrashAction = App.CrashActionType.SilentRestart;
                }
                else
                {
                    App.CrashAction = App.CrashActionType.NoAction;
                }
            }

            // Appearance - UI initialization (settings loading moved to AppearancePage)
            if (Settings.Appearance != null)
            {
                if (!Settings.Appearance.IsEnableDisPlayNibModeToggler)
                {
                    NibModeSimpleStackPanel.Visibility = Visibility.Collapsed;
                    BoardNibModeSimpleStackPanel.Visibility = Visibility.Collapsed;
                }

                if (Settings.Appearance.ViewboxFloatingBarScaleTransformValue != 0)
                {
                    double userVal = Settings.Appearance.ViewboxFloatingBarScaleTransformValue;
                    // 限制用户设置值在有效范围内
                    double clampedUserVal = (userVal > 0.5 && userVal < 1.25) ? userVal :
                                               userVal <= 0.5 ? 0.5 :
                                               userVal >= 1.25 ? 1.25 : 1.0;

                    double actualScale = clampedUserVal;

                    // 最终范围限制：0.5x ~ 1.25x
                    ViewboxFloatingBarScaleTransform.ScaleX = actualScale;
                    ViewboxFloatingBarScaleTransform.ScaleY = actualScale;
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

                ViewboxFloatingBar.Opacity = Settings.Appearance.ViewboxFloatingBarOpacityValue;

                ViewboxBlackboardCenterSideScaleTransform.ScaleX = Settings.Appearance.ViewboxBlackBoardScaleTransformValue;
                ViewboxBlackboardCenterSideScaleTransform.ScaleY = Settings.Appearance.ViewboxBlackBoardScaleTransformValue;

                ApplyQuickPanelBottomOffset(Settings.Appearance.QuickPanelBottomOffset);

                if (Settings.Appearance.IsTransparentButtonBackground)
                {
                    { /* Old UI removed */ }
                }
                else
                {
                    { /* Old UI removed */ }
                }

                if (Settings.Appearance.FloatingBarImg >= 12 + Settings.Appearance.CustomFloatingBarImgs.Count)
                {
                    Settings.Appearance.FloatingBarImg = 0;
                }

                UpdateFloatingBarIcon();
                UpdateFloatingBarButtonsVisibility();
                UpdateFloatingBarIcons();

                var _taskbar = Application.Current.Resources["TaskbarTrayIcon"];
                if (_taskbar is FrameworkElement fe)
                    fe.Visibility = Settings.Appearance.EnableTrayIcon ? Visibility.Visible : Visibility.Collapsed;

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

                UpdatePPTBtnSlidersStatus();
                UpdatePPTBtnPreview();
            }
            else
            {
                Settings.PowerPointSettings = new PowerPointSettings();
            }

            // Gesture
            if (Settings.Gesture == null)
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

                // 初始化直线自动拉直相关设置
                // 直线拉直灵敏度也在这里初始化，即使它存储在InkToShape中
                // 初始化高精度直线拉直设置

                // 初始化直线端点吸附相关设置
            }
            else
            {
                Settings.Canvas = new Canvas();
            }

            // Advanced - UI initialization (settings loading moved to AdvancedPage)
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
                FloatingBarToggleSwitchEnableInkToShape.IsOn = Settings.InkToShape.IsInkToShapeEnabled;
                BoardToggleSwitchEnableInkToShape.IsOn = Settings.InkToShape.IsInkToShapeEnabled;
            }
            else
            {
                Settings.InkToShape = new InkToShape();
            }

            // RandSettings - UI initialization (settings loading moved to RandomDrawPage)
            if (Settings.RandSettings != null)
            {
                BoardRandomDrawToolBtn.Visibility = Settings.RandSettings.ShowRandomAndSingleDraw ? Visibility.Visible : Visibility.Collapsed;
                BoardSingleDrawToolBtn.Visibility = Settings.RandSettings.ShowRandomAndSingleDraw ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                Settings.RandSettings = new RandSettings();
            }

            // ModeSettings
            if (Settings.ModeSettings == null)
            {
                Settings.ModeSettings = new ModeSettings();
            }

            if (isStartup && Settings.ModeSettings.IsPPTOnlyMode)
            {
                Hide();
                LogHelper.WriteLogToFile("启动时检测到仅PPT模式，主窗口已隐藏", LogHelper.LogType.Event);
            }

            // Automation
            if (Settings.Automation != null)
            {
                StartOrStoptimerCheckAutoFold();

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
            }
            else
            {
                Settings.Automation = new Automation();
            }

            // auto align
            if (IsInPptPresentationMode)
            {
                ViewboxFloatingBarMarginAnimation(60);
            }
            else
            {
                ViewboxFloatingBarMarginAnimation(100, true);
            }

            RefreshFloatingBarScreenFollowState();

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

                LogHelper.WriteLogToFile("画笔自动恢复设置已加载", LogHelper.LogType.Trace);
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

                LogHelper.WriteLogToFile("墨迹渐隐设置已加载", LogHelper.LogType.Trace);
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

        internal void ApplyQuickPanelBottomOffset(double offset)
        {
            LeftSidePanel.BeginAnimation(FrameworkElement.MarginProperty, null);
            RightSidePanel.BeginAnimation(FrameworkElement.MarginProperty, null);
            LeftUnFoldButtonQuickPanel.BeginAnimation(FrameworkElement.MarginProperty, null);
            RightUnFoldButtonQuickPanel.BeginAnimation(FrameworkElement.MarginProperty, null);

            var leftPanelMargin = LeftSidePanel.Margin;
            LeftSidePanel.Margin = new Thickness(leftPanelMargin.Left, leftPanelMargin.Top, leftPanelMargin.Right, offset);

            var rightPanelMargin = RightSidePanel.Margin;
            RightSidePanel.Margin = new Thickness(rightPanelMargin.Left, rightPanelMargin.Top, rightPanelMargin.Right, offset);

            var leftQuickPanelMargin = LeftUnFoldButtonQuickPanel.Margin;
            LeftUnFoldButtonQuickPanel.Margin = new Thickness(leftQuickPanelMargin.Left, leftQuickPanelMargin.Top, leftQuickPanelMargin.Right, offset);

            var rightQuickPanelMargin = RightUnFoldButtonQuickPanel.Margin;
            RightUnFoldButtonQuickPanel.Margin = new Thickness(rightQuickPanelMargin.Left, rightQuickPanelMargin.Top, rightQuickPanelMargin.Right, offset);
        }
    }
}
