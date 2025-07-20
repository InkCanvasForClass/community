using Ink_Canvas.Helpers;
using iNKORE.UI.WPF.Modern;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using System.Diagnostics;
using File = System.IO.File;
using MessageBox = System.Windows.MessageBox;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Controls.Primitives;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Windows.Input;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media.Animation;
using System.Reflection;
using Brushes = System.Windows.Media.Brushes;
using Point = System.Windows.Point;
using System.Collections.Generic;

namespace Ink_Canvas {
    public partial class MainWindow : Window {
        // 新增：每一页一个Canvas对象
        private List<System.Windows.Controls.Canvas> whiteboardPages = new List<System.Windows.Controls.Canvas>();
        private int currentPageIndex = 0;
        private System.Windows.Controls.Canvas currentCanvas = null;
        private AutoUpdateHelper.UpdateLineGroup AvailableLatestLineGroup = null;

        #region Window Initialization

        public MainWindow() {
            /*
                处于画板模式内：Topmost == false / currentMode != 0
                处于 PPT 放映内：BtnPPTSlideShowEnd.Visibility
            */
            InitializeComponent();

            BlackboardLeftSide.Visibility = Visibility.Collapsed;
            BlackboardCenterSide.Visibility = Visibility.Collapsed;
            BlackboardRightSide.Visibility = Visibility.Collapsed;
            BorderTools.Visibility = Visibility.Collapsed;
            BorderSettings.Visibility = Visibility.Collapsed;
            LeftSidePanelForPPTNavigation.Visibility = Visibility.Collapsed;
            RightSidePanelForPPTNavigation.Visibility = Visibility.Collapsed;
            BorderSettings.Margin = new Thickness(0, 0, 0, 0);
            TwoFingerGestureBorder.Visibility = Visibility.Collapsed;
            BoardTwoFingerGestureBorder.Visibility = Visibility.Collapsed;
            BorderDrawShape.Visibility = Visibility.Collapsed;
            BoardBorderDrawShape.Visibility = Visibility.Collapsed;
            GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;

            //if (!App.StartArgs.Contains("-o"))

            ViewBoxStackPanelMain.Visibility = Visibility.Collapsed;
            ViewBoxStackPanelShapes.Visibility = Visibility.Collapsed;
            var workingArea = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;
            ViewboxFloatingBar.Margin = new Thickness(
                (workingArea.Width - 284) / 2,
                workingArea.Bottom - 60 - workingArea.Top,
                -2000, -200);
            ViewboxFloatingBarMarginAnimation(100, true);

            try {
                if (File.Exists("debug.ini")) Label.Visibility = Visibility.Visible;
            }
            catch (Exception ex) {
                LogHelper.WriteLogToFile(ex.ToString(), LogHelper.LogType.Error);
            }

            try {
                if (File.Exists("Log.txt")) {
                    var fileInfo = new FileInfo("Log.txt");
                    var fileSizeInKB = fileInfo.Length / 1024;
                    if (fileSizeInKB > 512)
                        try {
                            File.Delete("Log.txt");
                            LogHelper.WriteLogToFile(
                                "The Log.txt file has been successfully deleted. Original file size: " + fileSizeInKB +
                                " KB", LogHelper.LogType.Info);
                        }
                        catch (Exception ex) {
                            LogHelper.WriteLogToFile(
                                ex + " | Can not delete the Log.txt file. File size: " + fileSizeInKB + " KB",
                                LogHelper.LogType.Error);
                        }
                }
            }
            catch (Exception ex) {
                LogHelper.WriteLogToFile(ex.ToString(), LogHelper.LogType.Error);
            }

            InitTimers();
            timeMachine.OnRedoStateChanged += TimeMachine_OnRedoStateChanged;
            timeMachine.OnUndoStateChanged += TimeMachine_OnUndoStateChanged;
            inkCanvas.Strokes.StrokesChanged += StrokesOnStrokesChanged;

            Microsoft.Win32.SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
            try {
                if (File.Exists("SpecialVersion.ini")) SpecialVersionResetToSuggestion_Click();
            }
            catch (Exception ex) {
                LogHelper.WriteLogToFile(ex.ToString(), LogHelper.LogType.Error);
            }

            CheckColorTheme(true);
            CheckPenTypeUIState();

            // 注册输入事件
            inkCanvas.PreviewMouseDown += inkCanvas_PreviewMouseDown;
            inkCanvas.StylusDown += inkCanvas_StylusDown;

            // 初始化第一页Canvas
            var firstCanvas = new System.Windows.Controls.Canvas();
            whiteboardPages.Add(firstCanvas);
            InkCanvasGridForInkReplay.Children.Add(firstCanvas);
            currentPageIndex = 0;
            ShowPage(currentPageIndex);
        }

        #endregion

        #region Ink Canvas Functions

        private System.Windows.Media.Color Ink_DefaultColor = Colors.Red;

        private DrawingAttributes drawingAttributes;

        private void loadPenCanvas() {
            try {
                //drawingAttributes = new DrawingAttributes();
                drawingAttributes = inkCanvas.DefaultDrawingAttributes;
                drawingAttributes.Color = Ink_DefaultColor;


                drawingAttributes.Height = 2.5;
                drawingAttributes.Width = 2.5;
                drawingAttributes.IsHighlighter = false;
                // 默认使用高级贝塞尔曲线平滑，如果未启用则使用原来的FitToCurve
                if (Settings.Canvas.UseAdvancedBezierSmoothing)
                {
                    drawingAttributes.FitToCurve = false;
                }
                else
                {
                    drawingAttributes.FitToCurve = Settings.Canvas.FitToCurve;
                }

                inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                inkCanvas.Gesture += InkCanvas_Gesture;
            }
            catch { }
        }

        //ApplicationGesture lastApplicationGesture = ApplicationGesture.AllGestures;
        private DateTime lastGestureTime = DateTime.Now;

        private void InkCanvas_Gesture(object sender, InkCanvasGestureEventArgs e) {
            var gestures = e.GetGestureRecognitionResults();
            try {
                foreach (var gest in gestures)
                    //Trace.WriteLine(string.Format("Gesture: {0}, Confidence: {1}", gest.ApplicationGesture, gest.RecognitionConfidence));
                    if (StackPanelPPTControls.Visibility == Visibility.Visible) {
                        if (gest.ApplicationGesture == ApplicationGesture.Left)
                            BtnPPTSlidesDown_Click(BtnPPTSlidesDown, null);
                        if (gest.ApplicationGesture == ApplicationGesture.Right)
                            BtnPPTSlidesUp_Click(BtnPPTSlidesUp, null);
                    }
            }
            catch { }
        }

        private void inkCanvas_EditingModeChanged(object sender, RoutedEventArgs e) {
            var inkCanvas1 = sender as InkCanvas;
            if (inkCanvas1 == null) return;

            // 使用辅助方法设置光标
            SetCursorBasedOnEditingMode(inkCanvas1);
            if (Settings.Canvas.IsShowCursor) {
                if (inkCanvas1.EditingMode == InkCanvasEditingMode.Ink ||
                    inkCanvas1.EditingMode == InkCanvasEditingMode.Select ||
                    drawingShapeMode != 0)
                    inkCanvas1.ForceCursor = true;
                else
                    inkCanvas1.ForceCursor = false;
            } else {
                // 套索选择模式下始终强制显示光标，即使用户设置不显示光标
                if (inkCanvas1.EditingMode == InkCanvasEditingMode.Select) {
                    inkCanvas1.ForceCursor = true;
                } else {
                    inkCanvas1.ForceCursor = false;
                }
            }

            if (inkCanvas1.EditingMode == InkCanvasEditingMode.Ink) forcePointEraser = !forcePointEraser;
            
            // 处理高级橡皮擦覆盖层的启用/禁用
            var eraserOverlay = this.FindName("AdvancedEraserOverlay") as Border;
            if (eraserOverlay != null) {
                if (inkCanvas1.EditingMode == InkCanvasEditingMode.EraseByPoint) {
                    // 橡皮擦模式下启用覆盖层
                    eraserOverlay.IsHitTestVisible = true;
                    Trace.WriteLine("Advanced Eraser: Overlay enabled in eraser mode");
                } else {
                    // 其他模式下禁用覆盖层
                    eraserOverlay.IsHitTestVisible = false;
                    // 同时禁用高级橡皮擦系统
                    DisableAdvancedEraserSystem();
                    Trace.WriteLine("Advanced Eraser: Overlay disabled in non-eraser mode");
                }
            }
        }

        #endregion Ink Canvas

        #region Definations and Loading

        public static Settings Settings = new Settings();
        public static string settingsFileName = "Settings.json";
        private bool isLoaded = false;
        private bool forcePointEraser = false;

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            loadPenCanvas();
            //加载设置
            LoadSettings(true);
            
            // 加载自定义背景颜色
            LoadCustomBackgroundColor();
            
            // 注册设置面板滚动事件
            if (SettingsPanelScrollViewer != null)
            {
                SettingsPanelScrollViewer.ScrollChanged += SettingsPanelScrollViewer_ScrollChanged;
            }
            
            // HasNewUpdateWindow hasNewUpdateWindow = new HasNewUpdateWindow();
            if (Environment.Is64BitProcess) GroupBoxInkRecognition.Visibility = Visibility.Collapsed;

            ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;
            SystemEvents_UserPreferenceChanged(null, null);

            //TextBlockVersion.Text = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            LogHelper.WriteLogToFile("Ink Canvas Loaded", LogHelper.LogType.Event);

            isLoaded = true;

            BlackBoardLeftSidePageListView.ItemsSource = blackBoardSidePageListViewObservableCollection;
            BlackBoardRightSidePageListView.ItemsSource = blackBoardSidePageListViewObservableCollection;

            BtnLeftWhiteBoardSwitchPreviousGeometry.Brush =
                new SolidColorBrush(System.Windows.Media.Color.FromArgb(127, 24, 24, 27));
            BtnLeftWhiteBoardSwitchPreviousLabel.Opacity = 0.5;
            BtnRightWhiteBoardSwitchPreviousGeometry.Brush =
                new SolidColorBrush(System.Windows.Media.Color.FromArgb(127, 24, 24, 27));
            BtnRightWhiteBoardSwitchPreviousLabel.Opacity = 0.5;

            // 应用颜色主题，这将考虑自定义背景色
            CheckColorTheme(true);
            
            BtnWhiteBoardSwitchPrevious.IsEnabled = CurrentWhiteboardIndex != 1;
            BorderInkReplayToolBox.Visibility = Visibility.Collapsed;

            // 提前加载IA库，优化第一笔等待时间
            if (Settings.InkToShape.IsInkToShapeEnabled && !Environment.Is64BitProcess) {
                var strokeEmpty = new StrokeCollection();
                InkRecognizeHelper.RecognizeShape(strokeEmpty);
            }

            SystemEvents.DisplaySettingsChanged += SystemEventsOnDisplaySettingsChanged;
            // 自动收纳到侧边栏
            if (Settings.Startup.IsFoldAtStartup)
            {
                FoldFloatingBar_MouseUp(new object(), null);
            }

            // 恢复崩溃后操作设置
            if (App.CrashAction == App.CrashActionType.SilentRestart)
                RadioCrashSilentRestart.IsChecked = true;
            else
                RadioCrashNoAction.IsChecked = true;
            

            
            // 如果当前不是黑板模式，则切换到黑板模式
            if (currentMode == 0)
            {
                // 延迟执行，确保UI已完全加载
                Dispatcher.BeginInvoke(new Action(() => {
                    // 重新加载自定义背景颜色
                    LoadCustomBackgroundColor();
                    
                    // 模拟点击切换按钮进入黑板模式
                    if (GridTransparencyFakeBackground.Background != Brushes.Transparent)
                    {
                        BtnSwitch_Click(BtnSwitch, null);
                    }
                    
                    // 确保背景颜色正确设置为黑板颜色
                    CheckColorTheme(true);
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }

            // 初始化插件系统
            InitializePluginSystem();
        }

        private void SystemEventsOnDisplaySettingsChanged(object sender, EventArgs e) {
            if (!Settings.Advanced.IsEnableResolutionChangeDetection) return;
            ShowNotification($"检测到显示器信息变化，变为{System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width}x{System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height}）");
            new Thread(() => {
                var isFloatingBarOutsideScreen = false;
                var isInPPTPresentationMode = false;
                Dispatcher.Invoke(() => {
                    isFloatingBarOutsideScreen = IsOutsideOfScreenHelper.IsOutsideOfScreen(ViewboxFloatingBar);
                    isInPPTPresentationMode = BtnPPTSlideShowEnd.Visibility == Visibility.Visible;
                });
                if (isFloatingBarOutsideScreen) dpiChangedDelayAction.DebounceAction(3000, null, () => {
                    if (!isFloatingBarFolded)
                    {
                        if (isInPPTPresentationMode) ViewboxFloatingBarMarginAnimation(60);
                        else ViewboxFloatingBarMarginAnimation(100, true);
                    }
                });
            }).Start();
        }

        public DelayAction dpiChangedDelayAction = new DelayAction();

        private void MainWindow_OnDpiChanged(object sender, DpiChangedEventArgs e)
        {
            if (e.OldDpi.DpiScaleX != e.NewDpi.DpiScaleX && e.OldDpi.DpiScaleY != e.NewDpi.DpiScaleY && Settings.Advanced.IsEnableDPIChangeDetection)
            {
                ShowNotification($"系统DPI发生变化，从 {e.OldDpi.DpiScaleX}x{e.OldDpi.DpiScaleY} 变化为 {e.NewDpi.DpiScaleX}x{e.NewDpi.DpiScaleY}");

                new Thread(() => {
                    var isFloatingBarOutsideScreen = false;
                    var isInPPTPresentationMode = false;
                    Dispatcher.Invoke(() => {
                        isFloatingBarOutsideScreen = IsOutsideOfScreenHelper.IsOutsideOfScreen(ViewboxFloatingBar);
                        isInPPTPresentationMode = BtnPPTSlideShowEnd.Visibility == Visibility.Visible;
                    });
                    if (isFloatingBarOutsideScreen) dpiChangedDelayAction.DebounceAction(3000,null, () => {
                        if (!isFloatingBarFolded)
                        {
                            if (isInPPTPresentationMode) ViewboxFloatingBarMarginAnimation(60);
                            else ViewboxFloatingBarMarginAnimation(100, true);
                        }
                    });
                }).Start();
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            LogHelper.WriteLogToFile("Ink Canvas closing", LogHelper.LogType.Event);
            if (!CloseIsFromButton && Settings.Advanced.IsSecondConfirmWhenShutdownApp) {
                e.Cancel = true;
                if (MessageBox.Show("是否继续关闭 InkCanvasForClass，这将丢失当前未保存的墨迹。", "InkCanvasForClass",
                        MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK)
                    if (MessageBox.Show("真的狠心关闭 InkCanvasForClass吗？", "InkCanvasForClass", MessageBoxButton.OKCancel,
                            MessageBoxImage.Error) == MessageBoxResult.OK)
                        if (MessageBox.Show("是否取消关闭 InkCanvasForClass？", "InkCanvasForClass", MessageBoxButton.OKCancel,
                                MessageBoxImage.Error) != MessageBoxResult.OK)
                            e.Cancel = false;
            }

            if (e.Cancel) LogHelper.WriteLogToFile("Ink Canvas closing cancelled", LogHelper.LogType.Event);
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);
        
        private void MainWindow_OnSizeChanged(object sender, SizeChangedEventArgs e) {
            if (Settings.Advanced.IsEnableForceFullScreen) {
                if (isLoaded) ShowNotification(
                    $"检测到窗口大小变化，已自动恢复到全屏：{System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width}x{System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height}（缩放比例为{System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width / SystemParameters.PrimaryScreenWidth}x{System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height / SystemParameters.PrimaryScreenHeight}）");
                WindowState = WindowState.Maximized;
                MoveWindow(new WindowInteropHelper(this).Handle, 0, 0,
                    System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width,
                    System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height, true);
            }
        }


        private void Window_Closed(object sender, EventArgs e) {
            SystemEvents.DisplaySettingsChanged -= SystemEventsOnDisplaySettingsChanged;

            LogHelper.WriteLogToFile("Ink Canvas closed", LogHelper.LogType.Event);
            
            // 检查是否有待安装的更新
            CheckPendingUpdates();
        }
        
        private void CheckPendingUpdates()
        {
            try
            {
                // 如果有可用的更新版本且启用了自动更新
                if (AvailableLatestVersion != null && Settings.Startup.IsAutoUpdate)
                {
                    // 检查更新文件是否已下载
                    string updatesFolderPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "AutoUpdate");
                    string statusFilePath = Path.Combine(updatesFolderPath, $"DownloadV{AvailableLatestVersion}Status.txt");
                    
                    if (File.Exists(statusFilePath) && File.ReadAllText(statusFilePath).Trim().ToLower() == "true")
                    {
                        LogHelper.WriteLogToFile($"AutoUpdate | Installing pending update v{AvailableLatestVersion} on application close");
                        
                        // 设置为用户主动退出，避免被看门狗判定为崩溃
                        App.IsAppExitByUser = true;
                        
                        // 创建批处理脚本并启动，软件关闭后会执行更新操作
                        AutoUpdateHelper.InstallNewVersionApp(AvailableLatestVersion, true);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"AutoUpdate | Error checking pending updates: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        // 辅助方法：使用多线路组下载更新
        private async Task<bool> DownloadUpdateWithFallback(string version, AutoUpdateHelper.UpdateLineGroup primaryGroup, UpdateChannel channel)
        {
            try
            {
                // 如果主要线路组可用，直接使用
                if (primaryGroup != null)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | 使用主要线路组下载: {primaryGroup.GroupName}");
                    return await AutoUpdateHelper.DownloadSetupFile(version, primaryGroup);
                }
                
                // 如果主要线路组不可用，获取所有可用线路组
                LogHelper.WriteLogToFile("AutoUpdate | 主要线路组不可用，获取所有可用线路组");
                var availableGroups = await AutoUpdateHelper.GetAvailableLineGroupsOrdered(channel);
                if (availableGroups.Count == 0)
                {
                    LogHelper.WriteLogToFile("AutoUpdate | 没有可用的线路组", LogHelper.LogType.Error);
                    return false;
                }
                
                LogHelper.WriteLogToFile($"AutoUpdate | 使用 {availableGroups.Count} 个可用线路组进行下载");
                return await AutoUpdateHelper.DownloadSetupFileWithFallback(version, availableGroups);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"AutoUpdate | 下载更新时出错: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
        }

        private async void AutoUpdate() {
            // 清除之前的更新状态，确保使用新通道重新检查
            AvailableLatestVersion = null;
            AvailableLatestLineGroup = null;
            
            // 使用当前选择的更新通道检查更新
            var (remoteVersion, lineGroup) = await AutoUpdateHelper.CheckForUpdates(Settings.Startup.UpdateChannel);
            AvailableLatestVersion = remoteVersion;
            AvailableLatestLineGroup = lineGroup;
            
            // 声明下载状态变量，用于整个方法
            bool isDownloadSuccessful = false;

            if (AvailableLatestVersion != null) {
                // 检测到新版本
                LogHelper.WriteLogToFile($"AutoUpdate | New version available: {AvailableLatestVersion}");
                
                // 检查是否是用户选择跳过的版本
                if (!string.IsNullOrEmpty(Settings.Startup.SkippedVersion) && 
                    Settings.Startup.SkippedVersion == AvailableLatestVersion) {
                    LogHelper.WriteLogToFile($"AutoUpdate | Version {AvailableLatestVersion} was marked to be skipped by the user");
                    return; // 跳过此版本，不执行更新操作
                }
                
                // 如果检测到的版本与跳过的版本不同，则清除跳过版本记录
                // 这确保用户只能跳过当前最新版本，而不是永久跳过所有更新
                if (!string.IsNullOrEmpty(Settings.Startup.SkippedVersion) && 
                    Settings.Startup.SkippedVersion != AvailableLatestVersion) {
                    LogHelper.WriteLogToFile($"AutoUpdate | Detected new version {AvailableLatestVersion} different from skipped version {Settings.Startup.SkippedVersion}, clearing skip record");
                    Settings.Startup.SkippedVersion = "";
                    SaveSettingsToFile();
                }
                
                // 获取当前版本
                string currentVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                
                // 如果启用了静默更新，则自动下载更新而不显示提示
                if (Settings.Startup.IsAutoUpdateWithSilence) {
                    LogHelper.WriteLogToFile("AutoUpdate | Silent update enabled, downloading update automatically without notification");
                    
                    // 静默下载更新，使用多线路组下载功能
                    isDownloadSuccessful = await DownloadUpdateWithFallback(AvailableLatestVersion, AvailableLatestLineGroup, Settings.Startup.UpdateChannel);
                    
                    if (isDownloadSuccessful) {
                        LogHelper.WriteLogToFile("AutoUpdate | Update downloaded successfully, will install when conditions are met");
                        
                        // 启动检查定时器，定期检查是否可以安装
                        timerCheckAutoUpdateWithSilence.Start();
                    } else {
                        LogHelper.WriteLogToFile("AutoUpdate | Silent update download failed", LogHelper.LogType.Error);
                    }
                    
                    return;
                }
                
                // 如果没有启用静默更新，则显示常规更新窗口
                string releaseDate = DateTime.Now.ToString("yyyy年MM月dd日");
                
                // 从服务器获取更新日志
                string releaseNotes = await AutoUpdateHelper.GetUpdateLog(Settings.Startup.UpdateChannel);
                
                // 如果获取失败，使用默认文本
                if (string.IsNullOrEmpty(releaseNotes))
                {
                    releaseNotes = $@"# InkCanvasForClass v{AvailableLatestVersion}更新
                
                    无法获取更新日志，但新版本已准备就绪。";
                }
                
                // 创建并显示更新窗口
                HasNewUpdateWindow updateWindow = new HasNewUpdateWindow(currentVersion, AvailableLatestVersion, releaseDate, releaseNotes);
                bool? dialogResult = updateWindow.ShowDialog();
                
                // 如果窗口被关闭但没有点击按钮，则不执行任何操作
                if (dialogResult != true) {
                    LogHelper.WriteLogToFile("AutoUpdate | Update dialog closed without selection");
                    return;
                }
                
                // 不再从更新窗口获取自动更新设置
                
                // 根据用户选择处理更新
                switch (updateWindow.Result) {
                    case HasNewUpdateWindow.UpdateResult.UpdateNow:
                        // 立即更新：显示下载进度，下载完成后立即安装
                        LogHelper.WriteLogToFile("AutoUpdate | User chose to update now");
                        
                        // 显示下载进度提示
                        MessageBox.Show("开始下载更新，请稍候...", "正在更新", MessageBoxButton.OK, MessageBoxImage.Information);
                        
                        // 下载更新文件，使用多线路组下载功能
                        isDownloadSuccessful = await DownloadUpdateWithFallback(AvailableLatestVersion, AvailableLatestLineGroup, Settings.Startup.UpdateChannel);
                        
                        if (isDownloadSuccessful) {
                            // 下载成功，提示用户准备安装
                            MessageBoxResult result = MessageBox.Show("更新已下载完成，点击确定后将关闭软件并安装新版本！", "安装更新", MessageBoxButton.OKCancel, MessageBoxImage.Information);
                            
                            // 只有当用户点击确定按钮后才关闭软件
                            if (result == MessageBoxResult.OK) {
                                // 设置为用户主动退出，避免被看门狗判定为崩溃
                                App.IsAppExitByUser = true;
                                
                                // 准备批处理脚本
                            AutoUpdateHelper.InstallNewVersionApp(AvailableLatestVersion, false);
                                
                                // 关闭软件，让安装程序接管
                                Application.Current.Shutdown();
                            } else {
                                LogHelper.WriteLogToFile("AutoUpdate | User cancelled update installation");
                            }
                        } else {
                            // 下载失败
                            MessageBox.Show("更新下载失败，请检查网络连接后重试。", "下载失败", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        break;
                        
                    case HasNewUpdateWindow.UpdateResult.UpdateLater:
                        // 稍后更新：静默下载，在软件关闭时自动安装
                        LogHelper.WriteLogToFile("AutoUpdate | User chose to update later");
                        
                        // 不管设置如何，都进行下载，使用多线路组下载功能
                        isDownloadSuccessful = await DownloadUpdateWithFallback(AvailableLatestVersion, AvailableLatestLineGroup, Settings.Startup.UpdateChannel);
                        
                        if (isDownloadSuccessful) {
                            LogHelper.WriteLogToFile("AutoUpdate | Update downloaded successfully, will install when application closes");
                            
                            // 设置标志，在应用程序关闭时安装
                            Settings.Startup.IsAutoUpdate = true;
                            Settings.Startup.IsAutoUpdateWithSilence = true;
                            
                            // 启动检查定时器
                            timerCheckAutoUpdateWithSilence.Start();
                            
                            // 通知用户
                            MessageBox.Show("更新已下载完成，将在软件关闭时自动安装。", "更新已准备就绪", MessageBoxButton.OK, MessageBoxImage.Information);
                        } else {
                            LogHelper.WriteLogToFile("AutoUpdate | Update download failed", LogHelper.LogType.Error);
                            MessageBox.Show("更新下载失败，请检查网络连接后重试。", "下载失败", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        break;
                        
                    case HasNewUpdateWindow.UpdateResult.SkipVersion:
                        // 跳过该版本：记录到设置中
                        LogHelper.WriteLogToFile($"AutoUpdate | User chose to skip version {AvailableLatestVersion}");
                        
                        // 记录要跳过的版本号
                        Settings.Startup.SkippedVersion = AvailableLatestVersion;
                        
                        // 保存设置到文件
                        SaveSettingsToFile();
                        
                        // 通知用户
                        MessageBox.Show($"已设置跳过版本 {AvailableLatestVersion}，在下次发布新版本之前不会再提示更新。", 
                                       "已跳过此版本", 
                                       MessageBoxButton.OK, 
                                       MessageBoxImage.Information);
                        break;
                }
            } else {
                AutoUpdateHelper.DeleteUpdatesFolder();
            }
        }

        // 新增：崩溃后操作设置按钮事件
        private void RadioCrashAction_Checked(object sender, RoutedEventArgs e)
        {
            if (RadioCrashSilentRestart != null && RadioCrashSilentRestart.IsChecked == true)
            {
                App.CrashAction = App.CrashActionType.SilentRestart;
                Settings.Startup.CrashAction = 0;
            }
            else if (RadioCrashNoAction != null && RadioCrashNoAction.IsChecked == true)
            {
                App.CrashAction = App.CrashActionType.NoAction;
                Settings.Startup.CrashAction = 1;
            }
            SaveSettingsToFile();
            // 强制同步全局变量，防止后台逻辑未及时感知
            App.SyncCrashActionFromSettings();
        }

        // 添加一个辅助方法，根据当前编辑模式设置光标
        public void SetCursorBasedOnEditingMode(InkCanvas canvas)
        {
            // 套索选择模式下光标始终显示，无论用户设置如何
            if (canvas.EditingMode == InkCanvasEditingMode.Select) {
                canvas.UseCustomCursor = true;
                canvas.ForceCursor = true;
                canvas.Cursor = Cursors.Cross;
                System.Windows.Forms.Cursor.Show();
                return;
            }

            // 其他模式按照用户设置处理
            if (Settings.Canvas.IsShowCursor) {
                canvas.UseCustomCursor = true;
                canvas.ForceCursor = true;

                // 根据编辑模式设置不同的光标
                if (canvas.EditingMode == InkCanvasEditingMode.EraseByPoint) {
                    canvas.Cursor = Cursors.Cross;
                } else if (canvas.EditingMode == InkCanvasEditingMode.Ink) {
                    var sri = Application.GetResourceStream(new Uri("Resources/Cursors/Pen.cur", UriKind.Relative));
                    if (sri != null)
                        canvas.Cursor = new Cursor(sri.Stream);
                }

                // 确保光标可见，无论是鼠标、触控还是手写笔
                System.Windows.Forms.Cursor.Show();

                // 确保手写笔模式下也能显示光标
                if (Tablet.TabletDevices.Count > 0) {
                    foreach (TabletDevice device in Tablet.TabletDevices) {
                        if (device.Type == TabletDeviceType.Stylus) {
                            // 手写笔设备存在，强制显示光标
                            System.Windows.Forms.Cursor.Show();
                            break;
                        }
                    }
                }
            } else {
                canvas.UseCustomCursor = false;
                canvas.ForceCursor = false;
                System.Windows.Forms.Cursor.Show();
            }
        }

        // 鼠标输入
        private void inkCanvas_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // 使用辅助方法设置光标
            SetCursorBasedOnEditingMode(sender as InkCanvas);
        }

        // 手写笔输入
        private void inkCanvas_StylusDown(object sender, StylusDownEventArgs e)
        {
            // 使用辅助方法设置光标
            SetCursorBasedOnEditingMode(sender as InkCanvas);
        }

        // 触摸结束，恢复光标

        #endregion Definations and Loading

        #region Navigation Sidebar Methods

        // 侧边栏导航按钮事件处理
        private void NavStartup_Click(object sender, RoutedEventArgs e)
        {
            // 切换到启动设置页面
            ShowSettingsSection("startup");
        }

        private void NavCanvas_Click(object sender, RoutedEventArgs e)
        {
            // 切换到画布设置页面
            ShowSettingsSection("canvas");
        }

        private void NavGesture_Click(object sender, RoutedEventArgs e)
        {
            // 切换到手势设置页面
            ShowSettingsSection("gesture");
        }

        private void NavInkRecognition_Click(object sender, RoutedEventArgs e)
        {
            // 切换到墨迹识别设置页面
            ShowSettingsSection("inkrecognition");
        }

        private void NavCrashAction_Click(object sender, RoutedEventArgs e)
        {
            // 切换到崩溃处理设置页面
            ShowSettingsSection("crashaction");
        }

        private void NavPPT_Click(object sender, RoutedEventArgs e)
        {
            // 切换到PPT设置页面
            ShowSettingsSection("ppt");
        }

        private void NavAdvanced_Click(object sender, RoutedEventArgs e)
        {
            // 切换到高级设置页面
            ShowSettingsSection("advanced");
        }

        private void NavAutomation_Click(object sender, RoutedEventArgs e)
        {
            // 切换到自动化设置页面
            ShowSettingsSection("automation");
        }

        private void NavRandomWindow_Click(object sender, RoutedEventArgs e)
        {
            // 切换到随机窗口设置页面
            ShowSettingsSection("randomwindow");
        }

        private void NavAbout_Click(object sender, RoutedEventArgs e)
        {
            // 切换到关于页面
            ShowSettingsSection("about");
        }

        // 新增：个性化设置
        private void NavTheme_Click(object sender, RoutedEventArgs e)
        {
            // 切换到个性化设置页面
            ShowSettingsSection("theme");
        }

        // 新增：快捷键设置
        private void NavShortcuts_Click(object sender, RoutedEventArgs e)
        {
            // 切换到快捷键设置页面
            ShowSettingsSection("shortcuts");
            // 如果设置部分尚未快捷键
            MessageBox.Show("设置功能正在开发中", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnCloseSettings_Click(object sender, RoutedEventArgs e)
        {
            // 关闭设置面板
            BorderSettings.Visibility = Visibility.Collapsed;
            // 设置蒙版为不可点击，并清除背景
            BorderSettingsMask.IsHitTestVisible = false;
            BorderSettingsMask.Background = null; // 确保清除蒙层背景
        }

        // 新增：折叠侧边栏
        private void CollapseNavSidebar_Click(object sender, RoutedEventArgs e)
        {
            // 折叠/展开侧边栏
            var columnDefinitions = ((Grid)BorderSettings.Child).ColumnDefinitions;
            if (columnDefinitions[0].Width.Value == 50)
            {
                // 折叠侧边栏
                columnDefinitions[0].Width = new GridLength(0);
            }
            else
            {
                // 展开侧边栏
                columnDefinitions[0].Width = new GridLength(50);
            }
        }
        
        // 新增：显示侧边栏
        private void ShowNavSidebar_Click(object sender, RoutedEventArgs e)
        {
            // 确保侧边栏展开
            var columnDefinitions = ((Grid)BorderSettings.Child).ColumnDefinitions;
            columnDefinitions[0].Width = new GridLength(50);
        }

        // 辅助方法：显示指定的设置部分
        private async void ShowSettingsSection(string sectionTag)
        {
            // 显示设置面板
            BorderSettings.Visibility = Visibility.Visible;
            // 设置蒙版为可点击，并添加半透明背景
            BorderSettingsMask.IsHitTestVisible = true;
            BorderSettingsMask.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(1, 0, 0, 0));
            
            // 获取SettingsPanelScrollViewer中的所有GroupBox
            var stackPanel = SettingsPanelScrollViewer.Content as StackPanel;
            if (stackPanel == null) return;
            
            // 确保所有GroupBox都是可见的
            foreach (var child in stackPanel.Children)
            {
                if (child is GroupBox groupBox)
                {
                    groupBox.Visibility = Visibility.Visible;
                }
            }
            
            // 确保UI完全更新
            await Dispatcher.InvokeAsync(() => {}, System.Windows.Threading.DispatcherPriority.Render);
            
            // 根据传入的sectionTag滚动到相应的设置部分
            GroupBox targetGroupBox = null;
            
            switch (sectionTag.ToLower())
            {
                case "startup":
                    targetGroupBox = FindGroupBoxByHeader(stackPanel, "启动");
                    break;
                case "canvas":
                    targetGroupBox = FindGroupBoxByHeader(stackPanel, "画板和墨迹");
                    break;
                case "gesture":
                    targetGroupBox = FindGroupBoxByHeader(stackPanel, "手势");
                    break;
                case "inkrecognition":
                    targetGroupBox = GroupBoxInkRecognition;
                    break;
                case "crashaction":
                    targetGroupBox = FindGroupBoxByHeader(stackPanel, "崩溃后操作");
                    break;
                case "ppt":
                    targetGroupBox = FindGroupBoxByHeader(stackPanel, "PPT联动");
                    break;
                case "advanced":
                    targetGroupBox = FindGroupBoxByHeader(stackPanel, "高级设置");
                    break;
                case "automation":
                    targetGroupBox = FindGroupBoxByHeader(stackPanel, "自动化");
                    break;
                case "randomwindow":
                    targetGroupBox = GroupBoxRandWindow;
                    break;
                case "theme":
                    targetGroupBox = GroupBoxAppearanceNewUI;
                    break;
                case "shortcuts":
                    // 快捷键设置部分可能尚未实现
                    targetGroupBox = FindGroupBoxByHeader(stackPanel, "快捷键");
                    break;
                case "about":
                    targetGroupBox = FindGroupBoxByHeader(stackPanel, "关于");
                    break;
                case "plugins":
                    targetGroupBox = GroupBoxPlugins;
                    break;
                default:
                    // 默认滚动到顶部
                    SettingsPanelScrollViewer.ScrollToTop();
                    return;
            }
            
            // 如果找到目标GroupBox，则滚动到它的位置
            if (targetGroupBox != null)
            {
                // 使用动画平滑滚动到目标位置
                ScrollToElement(targetGroupBox);
                
                // 高亮显示当前选中的导航项
                UpdateNavigationButtonState(sectionTag);
            }
            else
            {
                // 如果没有找到目标GroupBox，则滚动到顶部
                SettingsPanelScrollViewer.ScrollToTop();
            }
        }
        
        // 根据Header文本查找GroupBox
        private GroupBox FindGroupBoxByHeader(StackPanel parent, string headerText)
        {
            foreach (var child in parent.Children)
            {
                if (child is GroupBox groupBox)
                {
                    // 查找GroupBox的Header
                    if (groupBox.Header is TextBlock headerTextBlock && 
                        headerTextBlock.Text != null && 
                        headerTextBlock.Text.Contains(headerText))
                    {
                        return groupBox;
                    }
                }
            }
            return null;
        }
        
        // 平滑滚动到指定元素
        private async void ScrollToElement(FrameworkElement element)
        {
            if (element == null || SettingsPanelScrollViewer == null) return;
            
            try
            {
                // 暂时禁用滚动事件处理
                SettingsPanelScrollViewer.ScrollChanged -= SettingsPanelScrollViewer_ScrollChanged;
                
                // 记录当前滚动位置
                double originalOffset = SettingsPanelScrollViewer.VerticalOffset;
                
                // 将ScrollViewer内部的位置信息重置到顶部（不会触发视觉更新）
                SettingsPanelScrollViewer.ScrollToHome();
                
                // 使用Dispatcher进行延迟处理，确保布局更新
                await Dispatcher.InvokeAsync(() => {
                    try
                    {
                        // 强制更新布局
                        SettingsPanelScrollViewer.UpdateLayout();
                        
                        // 获取元素相对于顶部的准确位置
                        Point elementPosition = element.TransformToAncestor(SettingsPanelScrollViewer).Transform(new Point(0, 0));
                        
                        // 计算目标位置，减去一些偏移，使元素不会贴在顶部
                        double targetPosition = elementPosition.Y - 20;
                        
                        // 确保目标位置不小于0
                        targetPosition = Math.Max(0, targetPosition);
                        
                        // 直接设置滚动位置，不使用动画
                        SettingsPanelScrollViewer.ScrollToVerticalOffset(targetPosition);
                    }
                    catch (Exception ex)
                    {
                        // 如果出现异常，恢复到原来的滚动位置
                        SettingsPanelScrollViewer.ScrollToVerticalOffset(originalOffset);
                    }
                    finally
                    {
                        // 重新启用滚动事件处理
                        SettingsPanelScrollViewer.ScrollChanged += SettingsPanelScrollViewer_ScrollChanged;
                    }
                }, System.Windows.Threading.DispatcherPriority.Render);
            }
            catch (Exception)
            {
                // 确保在异常情况下也重新启用滚动事件处理
                SettingsPanelScrollViewer.ScrollChanged += SettingsPanelScrollViewer_ScrollChanged;
            }
        }
        
        // 滚动条变化事件处理
        private void SettingsPanelScrollViewer_ScrollChanged(object sender, System.Windows.Controls.ScrollChangedEventArgs e)
        {
            // 可以在这里添加滚动事件的处理逻辑，如果需要的话
        }
        
        // 更新导航按钮状态
        private void UpdateNavigationButtonState(string activeTag)
        {
            // 清除所有导航按钮的Tag属性
            ClearAllNavButtonTags();
            
            // 设置当前活动按钮的Tag属性
            switch (activeTag.ToLower())
            {
                case "startup":
                    SetNavButtonTag("startup");
                    break;
                case "canvas":
                    SetNavButtonTag("canvas");
                    break;
                case "gesture":
                    SetNavButtonTag("gesture");
                    break;
                case "inkrecognition":
                    SetNavButtonTag("inkrecognition");
                    break;
                case "crashaction":
                    SetNavButtonTag("crashaction");
                    break;
                case "ppt":
                    SetNavButtonTag("ppt");
                    break;
                case "advanced":
                    SetNavButtonTag("advanced");
                    break;
                case "automation":
                    SetNavButtonTag("automation");
                    break;
                case "randomwindow":
                    SetNavButtonTag("randomwindow");
                    break;
                case "theme":
                    SetNavButtonTag("theme");
                    break;
                case "shortcuts":
                    SetNavButtonTag("shortcuts");
                    break;
                case "about":
                    SetNavButtonTag("about");
                    break;
                case "plugins":
                    SetNavButtonTag("plugins");
                    break;
            }
        }
        
        // 清除所有导航按钮的Tag属性
        private void ClearAllNavButtonTags()
        {
            var grid = BorderSettings.Child as Grid;
            if (grid == null) return;
            
            var navSidebar = grid.Children[0] as Border;
            if (navSidebar == null) return;
            
            var navGrid = navSidebar.Child as Grid;
            if (navGrid == null) return;
            
            var scrollViewer = navGrid.Children[1] as ScrollViewer;
            if (scrollViewer == null) return;
            
            var stackPanel = scrollViewer.Content as StackPanel;
            if (stackPanel == null) return;
            
            foreach (var child in stackPanel.Children)
            {
                if (child is Button button)
                {
                    button.Tag = null;
                }
            }
        }
        
        // 设置导航按钮的Tag属性
        private void SetNavButtonTag(string tag)
        {
            var grid = BorderSettings.Child as Grid;
            if (grid == null) return;
            
            var navSidebar = grid.Children[0] as Border;
            if (navSidebar == null) return;
            
            var navGrid = navSidebar.Child as Grid;
            if (navGrid == null) return;
            
            var scrollViewer = navGrid.Children[1] as ScrollViewer;
            if (scrollViewer == null) return;
            
            var stackPanel = scrollViewer.Content as StackPanel;
            if (stackPanel == null) return;
            
            foreach (var child in stackPanel.Children)
            {
                if (child is Button button)
                {
                    // 检查按钮的ToolTip属性，根据tag设置对应的按钮
                    string buttonTag = button.Tag as string;
                    
                    // 如果按钮的Tag与要设置的tag匹配，则设置Tag
                    if (buttonTag != null && buttonTag.ToLower() == tag.ToLower())
                    {
                        button.Tag = tag;
                        return;
                    }
                }
            }
        }
        
        // 根据Header文本查找并显示GroupBox
        private void ShowGroupBoxByHeader(StackPanel parent, string headerText)
        {
            foreach (var child in parent.Children)
            {
                if (child is GroupBox groupBox)
                {
                    // 查找GroupBox的Header
                    if (groupBox.Header is TextBlock headerTextBlock && 
                        headerTextBlock.Text != null && 
                        headerTextBlock.Text.Contains(headerText))
                    {
                        groupBox.Visibility = Visibility.Visible;
                        return;
                    }
                }
            }
        }

        #endregion Navigation Sidebar Methods

        // 添加插件系统初始化方法
        private void InitializePluginSystem()
        {
            try
            {
                // 初始化插件管理器
                Helpers.Plugins.PluginManager.Instance.Initialize();
                LogHelper.WriteLogToFile("插件系统已初始化", LogHelper.LogType.Info);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"初始化插件系统时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        // 添加插件管理导航点击事件处理
        private void NavPlugins_Click(object sender, RoutedEventArgs e)
        {
            ShowSettingsSection("plugins");
        }

        // 添加打开插件管理器按钮点击事件
        private void BtnOpenPluginManager_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 暂时隐藏设置面板
                BorderSettings.Visibility = Visibility.Hidden;
                BorderSettingsMask.Visibility = Visibility.Hidden;
                
                // 创建并显示插件设置窗口
                Windows.PluginSettingsWindow pluginSettingsWindow = new Windows.PluginSettingsWindow();
                
                // 设置窗口关闭事件，用于在插件管理窗口关闭后恢复设置面板
                pluginSettingsWindow.Closed += (s, args) =>
                {
                    // 恢复设置面板显示
                    BorderSettings.Visibility = Visibility.Visible;
                    BorderSettingsMask.Visibility = Visibility.Visible;
                };
                
                // 显示插件设置窗口
                pluginSettingsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                // 确保在发生错误时也恢复设置面板显示
                BorderSettings.Visibility = Visibility.Visible;
                BorderSettingsMask.Visibility = Visibility.Visible;
                
                LogHelper.WriteLogToFile($"打开插件管理器时出错: {ex.Message}", LogHelper.LogType.Error);
                MessageBox.Show($"打开插件管理器时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 在MainWindow类中添加：
        private void ApplyCurrentEraserShape()
        {
            double k = 1;
            switch (Settings.Canvas.EraserSize)
            {
                case 0:
                    k = Settings.Canvas.EraserShapeType == 0 ? 0.5 : 0.7;
                    break;
                case 1:
                    k = Settings.Canvas.EraserShapeType == 0 ? 0.8 : 0.9;
                    break;
                case 3:
                    k = Settings.Canvas.EraserShapeType == 0 ? 1.25 : 1.2;
                    break;
                case 4:
                    k = Settings.Canvas.EraserShapeType == 0 ? 1.5 : 1.3;
                    break;
            }
            if (Settings.Canvas.EraserShapeType == 0)
            {
                inkCanvas.EraserShape = new EllipseStylusShape(k * 90, k * 90);
            }
            else if (Settings.Canvas.EraserShapeType == 1)
            {
                inkCanvas.EraserShape = new RectangleStylusShape(k * 90 * 0.6, k * 90);
            }
        }

        // 显示指定页
        private void ShowPage(int index)
        {
            if (index < 0 || index >= whiteboardPages.Count) return;
            // 只切换可见性
            for (int i = 0; i < whiteboardPages.Count; i++)
            {
                whiteboardPages[i].Visibility = (i == index) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            }
            currentCanvas = whiteboardPages[index];
            currentPageIndex = index;
        }
        // 新建页面
        private void AddNewPage()
        {
            var newCanvas = new System.Windows.Controls.Canvas();
            whiteboardPages.Add(newCanvas);
            InkCanvasGridForInkReplay.Children.Add(newCanvas);
            ShowPage(whiteboardPages.Count - 1);
        }
        // 删除当前页面
        private void DeleteCurrentPage()
        {
            if (whiteboardPages.Count <= 1) return;
            InkCanvasGridForInkReplay.Children.Remove(currentCanvas);
            whiteboardPages.RemoveAt(currentPageIndex);
            if (currentPageIndex >= whiteboardPages.Count)
                currentPageIndex = whiteboardPages.Count - 1;
            ShowPage(currentPageIndex);
        }
    }
}