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

namespace Ink_Canvas {
    public partial class MainWindow : Window {
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
            inkCanvas.TouchDown += inkCanvas_TouchDown;
            inkCanvas.TouchUp += inkCanvas_TouchUp;
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
                drawingAttributes.FitToCurve = Settings.Canvas.FitToCurve;

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

            if (inkCanvas1.EditingMode == InkCanvasEditingMode.Ink) forcePointEraser = !forcePointEraser;
        }

        #endregion Ink Canvas

        #region Definations and Loading

        public static Settings Settings = new Settings();
        public static string settingsFileName = "Settings.json";
        private bool isLoaded = false;

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            loadPenCanvas();
            //加载设置
            LoadSettings(true);
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
                FoldFloatingBar_MouseUp(null, null);
            }

            // 恢复崩溃后操作设置
            if (App.CrashAction == App.CrashActionType.SilentRestart)
                RadioCrashSilentRestart.IsChecked = true;
            else
                RadioCrashNoAction.IsChecked = true;
        }

        private void SystemEventsOnDisplaySettingsChanged(object sender, EventArgs e) {
            if (!Settings.Advanced.IsEnableResolutionChangeDetection) return;
            ShowNotification($"检测到显示器信息变化，变为{System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width}x{System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height}");
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

        private async void AutoUpdate() {
            AvailableLatestVersion = await AutoUpdateHelper.CheckForUpdates();

            if (AvailableLatestVersion != null) {
                // 打开更新提示窗口
                LogHelper.WriteLogToFile($"AutoUpdate | New version available: {AvailableLatestVersion}");
                
                // 获取当前版本和发布日期
                string currentVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                string releaseDate = DateTime.Now.ToString("yyyy年MM月dd日");
                
                // 创建更新说明内容（可以从服务器获取或直接在此处设置）
                string releaseNotes = $@"# InkCanvasForClass v{AvailableLatestVersion}更新
                
                你好，此次更新包含了一系列新功能和改进：

                1. 修复了一些已知的bug
                2. 优化了程序性能
                3. 改进了用户界面
                4. 添加了新的功能

                感谢您使用InkCanvasForClass CE！";
                
                // 创建并显示更新窗口
                HasNewUpdateWindow updateWindow = new HasNewUpdateWindow(currentVersion, AvailableLatestVersion, releaseDate, releaseNotes);
                bool? dialogResult = updateWindow.ShowDialog();
                
                // 声明下载结果变量
                bool isDownloadSuccessful;
                
                // 如果窗口被关闭但没有点击按钮，视为"稍后更新"
                if (dialogResult != true) {
                    LogHelper.WriteLogToFile("AutoUpdate | Update dialog closed without selection");
                    
                    // 更新自动更新设置并保存
                    Settings.Startup.IsAutoUpdate = updateWindow.IsAutoUpdateEnabled;
                    Settings.Startup.IsAutoUpdateWithSilence = updateWindow.IsSilentUpdateEnabled;
                    SaveSettingsToFile();
                    
                    // 如果启用了静默更新，则自动下载更新
                    if (Settings.Startup.IsAutoUpdateWithSilence) {
                        LogHelper.WriteLogToFile("AutoUpdate | Silent update enabled, downloading update automatically");
                        
                        // 静默下载更新
                        isDownloadSuccessful = await AutoUpdateHelper.DownloadSetupFileAndSaveStatus(AvailableLatestVersion);
                        
                        if (isDownloadSuccessful) {
                            LogHelper.WriteLogToFile("AutoUpdate | Update downloaded successfully, will install when application closes");
                            
                            // 启动检查定时器
                            timerCheckAutoUpdateWithSilence.Start();
                        } else {
                            LogHelper.WriteLogToFile("AutoUpdate | Silent update download failed", LogHelper.LogType.Error);
                        }
                    }
                    
                    return;
                }
                
                // 更新自动更新设置并保存
                Settings.Startup.IsAutoUpdate = updateWindow.IsAutoUpdateEnabled;
                Settings.Startup.IsAutoUpdateWithSilence = updateWindow.IsSilentUpdateEnabled;
                SaveSettingsToFile();
                
                // 根据用户选择处理更新
                switch (updateWindow.Result) {
                    case HasNewUpdateWindow.UpdateResult.UpdateNow:
                        // 立即更新：显示下载进度，下载完成后立即安装
                        LogHelper.WriteLogToFile("AutoUpdate | User chose to update now");
                        
                        // 显示下载进度提示
                        MessageBox.Show("开始下载更新，请稍候...", "正在更新", MessageBoxButton.OK, MessageBoxImage.Information);
                        
                        // 下载更新文件
                        isDownloadSuccessful = await AutoUpdateHelper.DownloadSetupFileAndSaveStatus(AvailableLatestVersion);
                        
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
                        
                        // 不管设置如何，都进行下载
                        isDownloadSuccessful = await AutoUpdateHelper.DownloadSetupFileAndSaveStatus(AvailableLatestVersion);
                        
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
                        // 可以在设置中添加"已跳过的版本"列表
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
            }
            else if (RadioCrashNoAction != null && RadioCrashNoAction.IsChecked == true)
            {
                App.CrashAction = App.CrashActionType.NoAction;
            }
            SaveSettingsToFile();
        }

        // 添加一个辅助方法，根据当前编辑模式设置光标
        private void SetCursorBasedOnEditingMode(InkCanvas canvas)
        {
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
                } else if (canvas.EditingMode == InkCanvasEditingMode.Select) {
                    canvas.Cursor = Cursors.Cross;
                }
                
                // 确保光标可见，无论是鼠标、触控还是手写笔
                System.Windows.Forms.Cursor.Show();
                
                // 强制应用光标设置
                canvas.ForceCursor = true;
                
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
            SetCursorBasedOnEditingMode(inkCanvas);
        }

        // 手写笔输入
        private void inkCanvas_StylusDown(object sender, StylusDownEventArgs e)
        {
            // 使用辅助方法设置光标
            SetCursorBasedOnEditingMode(inkCanvas);
        }

        // 触摸输入，不隐藏光标
        private void inkCanvas_TouchDown(object sender, TouchEventArgs e)
        {
            // 使用辅助方法设置光标
            SetCursorBasedOnEditingMode(inkCanvas);
        }

        // 触摸结束，恢复光标
        private void inkCanvas_TouchUp(object sender, TouchEventArgs e)
        {
            // 使用辅助方法设置光标
            SetCursorBasedOnEditingMode(inkCanvas);
            
            // 确保光标可见
            if (Settings.Canvas.IsShowCursor) {
                inkCanvas.ForceCursor = true;
                inkCanvas.UseCustomCursor = true;
                System.Windows.Forms.Cursor.Show();
            }
        }

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
        private void ShowSettingsSection(string sectionTag)
        {
            // 显示设置面板
            BorderSettings.Visibility = Visibility.Visible;
            // 设置蒙版为可点击，并添加半透明背景
            BorderSettingsMask.IsHitTestVisible = true;
            BorderSettingsMask.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(1, 0, 0, 0));
            
            // 获取SettingsPanelScrollViewer中的所有GroupBox
            var stackPanel = SettingsPanelScrollViewer.Content as StackPanel;
            if (stackPanel == null) return;
            
            // 首先隐藏所有GroupBox
            foreach (var child in stackPanel.Children)
            {
                if (child is GroupBox groupBox)
                {
                    groupBox.Visibility = Visibility.Collapsed;
                }
            }
            
            // 根据传入的sectionTag显示相应的设置部分
            switch (sectionTag.ToLower())
            {
                case "startup":
                    // 显示启动设置
                    ShowGroupBoxByHeader(stackPanel, "启动");
                    break;
                case "canvas":
                    // 显示画板和墨迹设置
                    ShowGroupBoxByHeader(stackPanel, "画板和墨迹");
                    break;
                case "gesture":
                    // 显示手势设置
                    ShowGroupBoxByHeader(stackPanel, "手势");
                    break;
                case "inkrecognition":
                    // 显示墨迹纠正设置
                    ShowGroupBoxByHeader(stackPanel, "墨迹纠正");
                    if (GroupBoxInkRecognition != null)
                        GroupBoxInkRecognition.Visibility = Visibility.Visible;
                    break;
                case "crashaction":
                    // 显示崩溃后操作设置
                    ShowGroupBoxByHeader(stackPanel, "崩溃后操作");
                    break;
                case "ppt":
                    // 显示PPT联动设置
                    ShowGroupBoxByHeader(stackPanel, "PPT联动");
                    break;
                case "advanced":
                    // 显示高级设置
                    // 这里可能需要根据实际情况调整
                    break;
                case "automation":
                    // 显示自动化设置
                    // 这里可能需要根据实际情况调整
                    break;
                case "randomwindow":
                    // 显示随机窗口设置
                    if (GroupBoxRandWindow != null)
                        GroupBoxRandWindow.Visibility = Visibility.Visible;
                    break;
                case "theme":
                    // 显示主题设置
                    if (GroupBoxAppearanceNewUI != null)
                        GroupBoxAppearanceNewUI.Visibility = Visibility.Visible;
                    break;
                case "shortcuts":
                    // 显示快捷键设置
                    // 快捷键设置部分可能尚未实现
                    break;
                case "about":
                    // 显示关于页面
                    ShowGroupBoxByHeader(stackPanel, "关于");
                    break;
                default:
                    // 默认显示第一个GroupBox
                    if (stackPanel.Children.Count > 0 && stackPanel.Children[0] is GroupBox firstGroupBox)
                    {
                        firstGroupBox.Visibility = Visibility.Visible;
                    }
                    break;
            }
            
            // 滚动到顶部
            SettingsPanelScrollViewer.ScrollToTop();
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
    }
}