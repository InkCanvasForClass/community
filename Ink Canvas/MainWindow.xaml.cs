using Ink_Canvas.Helpers;
using Ink_Canvas.Helpers.Plugins;
using Ink_Canvas.Windows;
using iNKORE.UI.WPF.Modern;
using iNKORE.UI.WPF.Modern.Controls;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Application = System.Windows.Application;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using Cursor = System.Windows.Input.Cursor;
using Cursors = System.Windows.Input.Cursors;
using DpiChangedEventArgs = System.Windows.DpiChangedEventArgs;
using File = System.IO.File;
using GroupBox = System.Windows.Controls.GroupBox;
using MessageBox = System.Windows.MessageBox;
using Point = System.Windows.Point;

namespace Ink_Canvas
{
    public partial class MainWindow : Window
    {
        // 新增：每一页一个Canvas对象
        private List<System.Windows.Controls.Canvas> whiteboardPages = new List<System.Windows.Controls.Canvas>();
        private int currentPageIndex;
        private System.Windows.Controls.Canvas currentCanvas;
        private AutoUpdateHelper.UpdateLineGroup AvailableLatestLineGroup;



        #region Window Initialization

        public MainWindow()
        {
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
            var workingArea = Screen.PrimaryScreen.WorkingArea;
            // 考虑快捷调色盘的宽度，确保浮动栏有足够空间
            double floatingBarWidth = 284; // 基础宽度
            if (Settings.Appearance.IsShowQuickColorPalette)
            {
                // 根据显示模式调整宽度
                if (Settings.Appearance.QuickColorPaletteDisplayMode == 0)
                {
                    // 单行显示模式，自适应宽度，但需要足够空间显示6个颜色
                    floatingBarWidth = Math.Max(floatingBarWidth, 120);
                }
                else
                {
                    // 双行显示模式，宽度较大
                    floatingBarWidth = Math.Max(floatingBarWidth, 820);
                }
            }
            ViewboxFloatingBar.Margin = new Thickness(
                (workingArea.Width - floatingBarWidth) / 2,
                workingArea.Bottom - 60 - workingArea.Top,
                -2000, -200);
            ViewboxFloatingBarMarginAnimation(100, true);

            try
            {
                if (File.Exists("debug.ini")) Label.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile(ex.ToString(), LogHelper.LogType.Error);
            }

            try
            {
                if (File.Exists("Log.txt"))
                {
                    var fileInfo = new FileInfo("Log.txt");
                    var fileSizeInKB = fileInfo.Length / 1024;
                    if (fileSizeInKB > 512)
                        try
                        {
                            File.Delete("Log.txt");
                            LogHelper.WriteLogToFile(
                                "The Log.txt file has been successfully deleted. Original file size: " + fileSizeInKB +
                                " KB");
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile(
                                ex + " | Can not delete the Log.txt file. File size: " + fileSizeInKB + " KB",
                                LogHelper.LogType.Error);
                        }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile(ex.ToString(), LogHelper.LogType.Error);
            }

            InitTimers();
            timeMachine.OnRedoStateChanged += TimeMachine_OnRedoStateChanged;
            timeMachine.OnUndoStateChanged += TimeMachine_OnUndoStateChanged;
            inkCanvas.Strokes.StrokesChanged += StrokesOnStrokesChanged;

            SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
            try
            {
                if (File.Exists("SpecialVersion.ini")) SpecialVersionResetToSuggestion_Click();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile(ex.ToString(), LogHelper.LogType.Error);
            }

            CheckColorTheme(true);
            CheckPenTypeUIState();

            // 初始化墨迹平滑管理器
            _inkSmoothingManager = new InkSmoothingManager(Dispatcher);

            // 注册输入事件
            inkCanvas.PreviewMouseDown += inkCanvas_PreviewMouseDown;
            inkCanvas.StylusDown += inkCanvas_StylusDown;
            inkCanvas.MouseRightButtonUp += InkCanvas_MouseRightButtonUp;

            // 初始化第一页Canvas
            var firstCanvas = new System.Windows.Controls.Canvas();
            whiteboardPages.Add(firstCanvas);
            InkCanvasGridForInkReplay.Children.Add(firstCanvas);
            currentPageIndex = 0;
            ShowPage(currentPageIndex);

            // 手动实现触摸滑动
            double leftTouchStartY = 0;
            double leftScrollStartOffset = 0;
            bool leftIsTouching = false;
            BlackBoardLeftSidePageListScrollViewer.TouchDown += (s, e) =>
            {
                leftIsTouching = true;
                leftTouchStartY = e.GetTouchPoint(BlackBoardLeftSidePageListScrollViewer).Position.Y;
                leftScrollStartOffset = BlackBoardLeftSidePageListScrollViewer.VerticalOffset;
                BlackBoardLeftSidePageListScrollViewer.CaptureTouch(e.TouchDevice);
                e.Handled = true;
            };
            BlackBoardLeftSidePageListScrollViewer.TouchMove += (s, e) =>
            {
                if (leftIsTouching)
                {
                    double currentY = e.GetTouchPoint(BlackBoardLeftSidePageListScrollViewer).Position.Y;
                    double delta = leftTouchStartY - currentY;
                    BlackBoardLeftSidePageListScrollViewer.ScrollToVerticalOffset(leftScrollStartOffset + delta);
                    e.Handled = true;
                }
            };
            BlackBoardLeftSidePageListScrollViewer.TouchUp += (s, e) =>
            {
                leftIsTouching = false;
                BlackBoardLeftSidePageListScrollViewer.ReleaseTouchCapture(e.TouchDevice);
                e.Handled = true;
            };
            double rightTouchStartY = 0;
            double rightScrollStartOffset = 0;
            bool rightIsTouching = false;
            BlackBoardRightSidePageListScrollViewer.TouchDown += (s, e) =>
            {
                rightIsTouching = true;
                rightTouchStartY = e.GetTouchPoint(BlackBoardRightSidePageListScrollViewer).Position.Y;
                rightScrollStartOffset = BlackBoardRightSidePageListScrollViewer.VerticalOffset;
                BlackBoardRightSidePageListScrollViewer.CaptureTouch(e.TouchDevice);
                e.Handled = true;
            };
            BlackBoardRightSidePageListScrollViewer.TouchMove += (s, e) =>
            {
                if (rightIsTouching)
                {
                    double currentY = e.GetTouchPoint(BlackBoardRightSidePageListScrollViewer).Position.Y;
                    double delta = rightTouchStartY - currentY;
                    BlackBoardRightSidePageListScrollViewer.ScrollToVerticalOffset(rightScrollStartOffset + delta);
                    e.Handled = true;
                }
            };
            BlackBoardRightSidePageListScrollViewer.TouchUp += (s, e) =>
            {
                rightIsTouching = false;
                BlackBoardRightSidePageListScrollViewer.ReleaseTouchCapture(e.TouchDevice);
                e.Handled = true;
            };
            // 初始化无焦点模式开关
            ToggleSwitchNoFocusMode.IsOn = Settings.Advanced.IsNoFocusMode;
            ApplyNoFocusMode();
            // 初始化窗口置顶开关
            ToggleSwitchAlwaysOnTop.IsOn = Settings.Advanced.IsAlwaysOnTop;
            ApplyAlwaysOnTop();
            
            // 添加窗口激活事件处理，确保置顶状态在窗口重新激活时得到保持
            this.Activated += Window_Activated;
            this.Deactivated += Window_Deactivated;
        }



        #endregion

        #region Ink Canvas Functions

        private Color Ink_DefaultColor = Colors.Red;

        private DrawingAttributes drawingAttributes;
        private InkSmoothingManager _inkSmoothingManager;

        private void loadPenCanvas()
        {
            try
            {
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

        private void InkCanvas_Gesture(object sender, InkCanvasGestureEventArgs e)
        {
            var gestures = e.GetGestureRecognitionResults();
            try
            {
                foreach (var gest in gestures)
                    //Trace.WriteLine(string.Format("Gesture: {0}, Confidence: {1}", gest.ApplicationGesture, gest.RecognitionConfidence));
                    if (StackPanelPPTControls.Visibility == Visibility.Visible)
                    {
                        if (gest.ApplicationGesture == ApplicationGesture.Left)
                            BtnPPTSlidesDown_Click(BtnPPTSlidesDown, null);
                        if (gest.ApplicationGesture == ApplicationGesture.Right)
                            BtnPPTSlidesUp_Click(BtnPPTSlidesUp, null);
                    }
            }
            catch { }
        }

        private void inkCanvas_EditingModeChanged(object sender, RoutedEventArgs e)
        {
            var inkCanvas1 = sender as InkCanvas;
            if (inkCanvas1 == null) return;

            // 使用辅助方法设置光标
            SetCursorBasedOnEditingMode(inkCanvas1);
            if (Settings.Canvas.IsShowCursor)
            {
                if (inkCanvas1.EditingMode == InkCanvasEditingMode.Ink ||
                    inkCanvas1.EditingMode == InkCanvasEditingMode.Select ||
                    drawingShapeMode != 0)
                    inkCanvas1.ForceCursor = true;
                else
                    inkCanvas1.ForceCursor = false;
            }
            else
            {
                // 套索选择模式下始终强制显示光标，即使用户设置不显示光标
                if (inkCanvas1.EditingMode == InkCanvasEditingMode.Select)
                {
                    inkCanvas1.ForceCursor = true;
                }
                else
                {
                    inkCanvas1.ForceCursor = false;
                }
            }

            if (inkCanvas1.EditingMode == InkCanvasEditingMode.Ink) forcePointEraser = !forcePointEraser;

            // 处理高级橡皮擦覆盖层的启用/禁用
            var eraserOverlay = FindName("AdvancedEraserOverlay") as Border;
            if (eraserOverlay != null)
            {
                if (inkCanvas1.EditingMode == InkCanvasEditingMode.EraseByPoint)
                {
                    // 橡皮擦模式下启用覆盖层
                    eraserOverlay.IsHitTestVisible = true;
                    Trace.WriteLine("Advanced Eraser: Overlay enabled in eraser mode");
                }
                else
                {
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
        private bool isLoaded;
        private bool forcePointEraser;

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            loadPenCanvas();
            //加载设置
            LoadSettings(true);
            // 检查保存路径是否可用，不可用则修正
            try
            {
                string savePath = Settings.Automation.AutoSavedStrokesLocation;
                bool needFix = false;
                if (string.IsNullOrWhiteSpace(savePath) || !Directory.Exists(savePath))
                {
                    needFix = true;
                }
                else
                {
                    // 检查是否可写
                    try
                    {
                        string testFile = Path.Combine(savePath, "test.tmp");
                        File.WriteAllText(testFile, "test");
                        File.Delete(testFile);
                    }
                    catch
                    {
                        needFix = true;
                    }
                }
                if (needFix)
                {
                    string newPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "saves");
                    Settings.Automation.AutoSavedStrokesLocation = newPath;
                    if (!Directory.Exists(newPath))
                        Directory.CreateDirectory(newPath);
                    SaveSettingsToFile();
                    LogHelper.WriteLogToFile($"自动修正保存路径为: {newPath}");
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"检测或修正保存路径时出错: {ex.Message}", LogHelper.LogType.Error);
            }

            // 加载自定义背景颜色
            LoadCustomBackgroundColor();

            // 注册设置面板滚动事件
            if (SettingsPanelScrollViewer != null)
            {
                SettingsPanelScrollViewer.ScrollChanged += SettingsPanelScrollViewer_ScrollChanged;
            }

            // 初始化PPT管理器
            InitializePPTManagers();

            // 如果启用PPT支持，开始监控
            if (Settings.PowerPointSettings.PowerPointSupport)
            {
                StartPPTMonitoring();
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
                new SolidColorBrush(Color.FromArgb(127, 24, 24, 27));
            BtnLeftWhiteBoardSwitchPreviousLabel.Opacity = 0.5;
            BtnRightWhiteBoardSwitchPreviousGeometry.Brush =
                new SolidColorBrush(Color.FromArgb(127, 24, 24, 27));
            BtnRightWhiteBoardSwitchPreviousLabel.Opacity = 0.5;

            // 应用颜色主题，这将考虑自定义背景色
            CheckColorTheme(true);

            BtnWhiteBoardSwitchPrevious.IsEnabled = CurrentWhiteboardIndex != 1;
            BorderInkReplayToolBox.Visibility = Visibility.Collapsed;

            // 提前加载IA库，优化第一笔等待时间
            if (Settings.InkToShape.IsInkToShapeEnabled && !Environment.Is64BitProcess)
            {
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
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    // 重新加载自定义背景颜色
                    LoadCustomBackgroundColor();

                    // 模拟点击切换按钮进入黑板模式
                    if (GridTransparencyFakeBackground.Background != Brushes.Transparent)
                    {
                        BtnSwitch_Click(BtnSwitch, null);
                    }

                    // 确保背景颜色正确设置为黑板颜色
                    CheckColorTheme(true);
                }), DispatcherPriority.Loaded);
            }

            // 初始化插件系统
            InitializePluginSystem();
            // 确保开关和设置同步
            ToggleSwitchNoFocusMode.IsOn = Settings.Advanced.IsNoFocusMode;
            ApplyNoFocusMode();
            ToggleSwitchAlwaysOnTop.IsOn = Settings.Advanced.IsAlwaysOnTop;
            ApplyAlwaysOnTop();

            // 初始化UIElement选择系统
            InitializeUIElementSelection();

            // 初始化剪贴板监控
            InitializeClipboardMonitoring();
        }

        private void SystemEventsOnDisplaySettingsChanged(object sender, EventArgs e)
        {
            if (!Settings.Advanced.IsEnableResolutionChangeDetection) return;
            ShowNotification($"检测到显示器信息变化，变为{Screen.PrimaryScreen.Bounds.Width}x{Screen.PrimaryScreen.Bounds.Height}）");
            new Thread(() =>
            {
                var isFloatingBarOutsideScreen = false;
                var isInPPTPresentationMode = false;
                Dispatcher.Invoke(() =>
                {
                    isFloatingBarOutsideScreen = IsOutsideOfScreenHelper.IsOutsideOfScreen(ViewboxFloatingBar);
                    isInPPTPresentationMode = BtnPPTSlideShowEnd.Visibility == Visibility.Visible;
                });
                if (isFloatingBarOutsideScreen) dpiChangedDelayAction.DebounceAction(3000, null, () =>
                {
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

                new Thread(() =>
                {
                    var isFloatingBarOutsideScreen = false;
                    var isInPPTPresentationMode = false;
                    Dispatcher.Invoke(() =>
                    {
                        isFloatingBarOutsideScreen = IsOutsideOfScreenHelper.IsOutsideOfScreen(ViewboxFloatingBar);
                        isInPPTPresentationMode = BtnPPTSlideShowEnd.Visibility == Visibility.Visible;
                    });
                    if (isFloatingBarOutsideScreen) dpiChangedDelayAction.DebounceAction(3000, null, () =>
                    {
                        if (!isFloatingBarFolded)
                        {
                            if (isInPPTPresentationMode) ViewboxFloatingBarMarginAnimation(60);
                            else ViewboxFloatingBarMarginAnimation(100, true);
                        }
                    });
                }).Start();
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            LogHelper.WriteLogToFile("Ink Canvas closing", LogHelper.LogType.Event);
            if (!CloseIsFromButton && Settings.Advanced.IsSecondConfirmWhenShutdownApp)
            {
                // 第一个确认对话框
                var result1 = MessageBox.Show("是否继续关闭 InkCanvasForClass，这将丢失当前未保存的墨迹。", "InkCanvasForClass",
                    MessageBoxButton.OKCancel, MessageBoxImage.Warning);

                if (result1 == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                    LogHelper.WriteLogToFile("Ink Canvas closing cancelled at first confirmation", LogHelper.LogType.Event);
                    return;
                }

                // 第二个确认对话框
                var result2 = MessageBox.Show("真的狠心关闭 InkCanvasForClass吗？", "InkCanvasForClass",
                    MessageBoxButton.OKCancel, MessageBoxImage.Error);

                if (result2 == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                    LogHelper.WriteLogToFile("Ink Canvas closing cancelled at second confirmation", LogHelper.LogType.Event);
                    return;
                }

                // 第三个最终确认对话框
                var result3 = MessageBox.Show("最后确认：确定要关闭 InkCanvasForClass 吗？", "InkCanvasForClass",
                    MessageBoxButton.OKCancel, MessageBoxImage.Question);

                if (result3 == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                    LogHelper.WriteLogToFile("Ink Canvas closing cancelled at final confirmation", LogHelper.LogType.Event);
                    return;
                }

                // 所有确认都通过，允许关闭
                e.Cancel = false;
                LogHelper.WriteLogToFile("Ink Canvas closing confirmed by user", LogHelper.LogType.Event);
            }

            if (e.Cancel) LogHelper.WriteLogToFile("Ink Canvas closing cancelled", LogHelper.LogType.Event);
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        private void MainWindow_OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (Settings.Advanced.IsEnableForceFullScreen)
            {
                if (isLoaded) ShowNotification(
                    $"检测到窗口大小变化，已自动恢复到全屏：{Screen.PrimaryScreen.Bounds.Width}x{Screen.PrimaryScreen.Bounds.Height}（缩放比例为{Screen.PrimaryScreen.Bounds.Width / SystemParameters.PrimaryScreenWidth}x{Screen.PrimaryScreen.Bounds.Height / SystemParameters.PrimaryScreenHeight}）");
                WindowState = WindowState.Maximized;
                MoveWindow(new WindowInteropHelper(this).Handle, 0, 0,
                    Screen.PrimaryScreen.Bounds.Width,
                    Screen.PrimaryScreen.Bounds.Height, true);
            }
        }


        private void Window_Closed(object sender, EventArgs e)
        {
            SystemEvents.DisplaySettingsChanged -= SystemEventsOnDisplaySettingsChanged;

            // 释放PPT管理器资源
            DisposePPTManagers();

            // 清理剪贴板监控
            CleanupClipboardMonitoring();
            ClipboardNotification.Stop();

            // 停止置顶维护定时器
            StopTopmostMaintenance();

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

        private async void AutoUpdate()
        {
            // 清除之前的更新状态，确保使用新通道重新检查
            AvailableLatestVersion = null;
            AvailableLatestLineGroup = null;

            // 使用当前选择的更新通道检查更新
            var (remoteVersion, lineGroup, apiReleaseNotes) = await AutoUpdateHelper.CheckForUpdates(Settings.Startup.UpdateChannel);
            AvailableLatestVersion = remoteVersion;
            AvailableLatestLineGroup = lineGroup;

            // 声明下载状态变量，用于整个方法
            bool isDownloadSuccessful = false;

            if (AvailableLatestVersion != null)
            {
                // 检测到新版本
                LogHelper.WriteLogToFile($"AutoUpdate | New version available: {AvailableLatestVersion}");

                // 检查是否是用户选择跳过的版本
                if (!string.IsNullOrEmpty(Settings.Startup.SkippedVersion) &&
                    Settings.Startup.SkippedVersion == AvailableLatestVersion)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | Version {AvailableLatestVersion} was marked to be skipped by the user");
                    return; // 跳过此版本，不执行更新操作
                }

                // 如果检测到的版本与跳过的版本不同，则清除跳过版本记录
                // 这确保用户只能跳过当前最新版本，而不是永久跳过所有更新
                if (!string.IsNullOrEmpty(Settings.Startup.SkippedVersion) &&
                    Settings.Startup.SkippedVersion != AvailableLatestVersion)
                {
                    LogHelper.WriteLogToFile($"AutoUpdate | Detected new version {AvailableLatestVersion} different from skipped version {Settings.Startup.SkippedVersion}, clearing skip record");
                    Settings.Startup.SkippedVersion = "";
                    SaveSettingsToFile();
                }

                // 获取当前版本
                string currentVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();

                // 如果启用了静默更新，则自动下载更新而不显示提示
                if (Settings.Startup.IsAutoUpdateWithSilence)
                {
                    LogHelper.WriteLogToFile("AutoUpdate | Silent update enabled, downloading update automatically without notification");

                    // 静默下载更新，使用多线路组下载功能
                    isDownloadSuccessful = await DownloadUpdateWithFallback(AvailableLatestVersion, AvailableLatestLineGroup, Settings.Startup.UpdateChannel);

                    if (isDownloadSuccessful)
                    {
                        LogHelper.WriteLogToFile("AutoUpdate | Update downloaded successfully, will install when conditions are met");

                        // 启动检查定时器，定期检查是否可以安装
                        timerCheckAutoUpdateWithSilence.Start();
                    }
                    else
                    {
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
                if (dialogResult != true)
                {
                    LogHelper.WriteLogToFile("AutoUpdate | Update dialog closed without selection");
                    return;
                }

                // 不再从更新窗口获取自动更新设置

                // 根据用户选择处理更新
                switch (updateWindow.Result)
                {
                    case HasNewUpdateWindow.UpdateResult.UpdateNow:
                        // 立即更新：显示下载进度，下载完成后立即安装
                        LogHelper.WriteLogToFile("AutoUpdate | User chose to update now");

                        // 显示下载进度提示
                        MessageBox.Show("开始下载更新，请稍候...", "正在更新", MessageBoxButton.OK, MessageBoxImage.Information);

                        // 下载更新文件，使用多线路组下载功能
                        isDownloadSuccessful = await DownloadUpdateWithFallback(AvailableLatestVersion, AvailableLatestLineGroup, Settings.Startup.UpdateChannel);

                        if (isDownloadSuccessful)
                        {
                            // 下载成功，提示用户准备安装
                            MessageBoxResult result = MessageBox.Show("更新已下载完成，点击确定后将关闭软件并安装新版本！", "安装更新", MessageBoxButton.OKCancel, MessageBoxImage.Information);

                            // 只有当用户点击确定按钮后才关闭软件
                            if (result == MessageBoxResult.OK)
                            {
                                // 设置为用户主动退出，避免被看门狗判定为崩溃
                                App.IsAppExitByUser = true;

                                // 准备批处理脚本
                                AutoUpdateHelper.InstallNewVersionApp(AvailableLatestVersion, false);

                                // 关闭软件，让安装程序接管
                                Application.Current.Shutdown();
                            }
                            else
                            {
                                LogHelper.WriteLogToFile("AutoUpdate | User cancelled update installation");
                            }
                        }
                        else
                        {
                            // 下载失败
                            MessageBox.Show("更新下载失败，请检查网络连接后重试。", "下载失败", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        break;

                    case HasNewUpdateWindow.UpdateResult.UpdateLater:
                        // 稍后更新：静默下载，在软件关闭时自动安装
                        LogHelper.WriteLogToFile("AutoUpdate | User chose to update later");

                        // 不管设置如何，都进行下载，使用多线路组下载功能
                        isDownloadSuccessful = await DownloadUpdateWithFallback(AvailableLatestVersion, AvailableLatestLineGroup, Settings.Startup.UpdateChannel);

                        if (isDownloadSuccessful)
                        {
                            LogHelper.WriteLogToFile("AutoUpdate | Update downloaded successfully, will install when application closes");

                            // 设置标志，在应用程序关闭时安装
                            Settings.Startup.IsAutoUpdate = true;
                            Settings.Startup.IsAutoUpdateWithSilence = true;

                            // 启动检查定时器
                            timerCheckAutoUpdateWithSilence.Start();

                            // 通知用户
                            MessageBox.Show("更新已下载完成，将在软件关闭时自动安装。", "更新已准备就绪", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
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
            }
            else
            {
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
            if (canvas.EditingMode == InkCanvasEditingMode.Select)
            {
                canvas.UseCustomCursor = true;
                canvas.ForceCursor = true;
                canvas.Cursor = Cursors.Cross;
                System.Windows.Forms.Cursor.Show();
                return;
            }

            // 其他模式按照用户设置处理
            if (Settings.Canvas.IsShowCursor)
            {
                canvas.UseCustomCursor = true;
                canvas.ForceCursor = true;

                // 根据编辑模式设置不同的光标
                if (canvas.EditingMode == InkCanvasEditingMode.EraseByPoint)
                {
                    canvas.Cursor = Cursors.Cross;
                }
                else if (canvas.EditingMode == InkCanvasEditingMode.Ink)
                {
                    var sri = Application.GetResourceStream(new Uri("Resources/Cursors/Pen.cur", UriKind.Relative));
                    if (sri != null)
                        canvas.Cursor = new Cursor(sri.Stream);
                }

                // 确保光标可见，无论是鼠标、触控还是手写笔
                System.Windows.Forms.Cursor.Show();

                // 确保手写笔模式下也能显示光标
                if (Tablet.TabletDevices.Count > 0)
                {
                    foreach (TabletDevice device in Tablet.TabletDevices)
                    {
                        if (device.Type == TabletDeviceType.Stylus)
                        {
                            // 手写笔设备存在，强制显示光标
                            System.Windows.Forms.Cursor.Show();
                            break;
                        }
                    }
                }
            }
            else
            {
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

            // 在选择模式下，如果点击的不是UI元素，则取消选择
            if (inkCanvas.EditingMode == InkCanvasEditingMode.Select)
            {
                var hitTest = e.OriginalSource;
                // 如果点击的不是图片或其他UI元素，则取消选择
                if (!(hitTest is Image) && !(hitTest is MediaElement))
                {
                    // 检查是否点击在已选择的UI元素上
                    bool clickedOnSelectedElement = false;
                    if (selectedUIElement != null)
                    {
                        var elementBounds = GetUIElementBounds(selectedUIElement);
                        var clickPoint = e.GetPosition(inkCanvas);
                        clickedOnSelectedElement = elementBounds.Contains(clickPoint);
                    }

                    if (!clickedOnSelectedElement)
                    {
                        DeselectUIElement();
                    }
                }
            }
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
            // 刷新设备信息
            RefreshDeviceInfo();
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

        /// <summary>
        /// 刷新设备信息按钮点击事件
        /// </summary>
        private void RefreshDeviceInfo_Click(object sender, RoutedEventArgs e)
        {
            RefreshDeviceInfo();
        }

        /// <summary>
        /// 刷新设备信息显示
        /// </summary>
        private void RefreshDeviceInfo()
        {
            try
            {
                // 获取设备ID
                string deviceId = DeviceIdentifier.GetDeviceId();
                DeviceIdTextBlock.Text = deviceId;

                // 获取使用频率
                var usageFrequency = DeviceIdentifier.GetUsageFrequency();
                string frequencyText;
                switch (usageFrequency)
                {
                    case DeviceIdentifier.UsageFrequency.High:
                        frequencyText = "高频用户";
                        break;
                    case DeviceIdentifier.UsageFrequency.Medium:
                        frequencyText = "中频用户";
                        break;
                    case DeviceIdentifier.UsageFrequency.Low:
                        frequencyText = "低频用户";
                        break;
                    default:
                        frequencyText = "未知";
                        break;
                }
                UsageFrequencyTextBlock.Text = frequencyText;

                // 获取更新优先级
                var updatePriority = DeviceIdentifier.GetUpdatePriority();
                string priorityText;
                switch (updatePriority)
                {
                    case DeviceIdentifier.UpdatePriority.High:
                        priorityText = "高优先级（优先推送更新）";
                        break;
                    case DeviceIdentifier.UpdatePriority.Medium:
                        priorityText = "中优先级（正常推送更新）";
                        break;
                    case DeviceIdentifier.UpdatePriority.Low:
                        priorityText = "低优先级（延迟推送更新）";
                        break;
                    default:
                        priorityText = "未知";
                        break;
                }
                UpdatePriorityTextBlock.Text = priorityText;

                // 获取使用统计（秒级精度）
                var (launchCount, totalSeconds, avgSessionSeconds, _) = DeviceIdentifier.GetUsageStats();
                LaunchCountTextBlock.Text = launchCount.ToString();

                // 使用新的格式化方法显示秒级精度的使用时长
                string totalUsageText = DeviceIdentifier.FormatDuration(totalSeconds);
                TotalUsageTextBlock.Text = totalUsageText;

                LogHelper.WriteLogToFile($"MainWindow | 设备信息已刷新 - ID: {deviceId}, 频率: {frequencyText}, 优先级: {priorityText}");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"MainWindow | 刷新设备信息失败: {ex.Message}", LogHelper.LogType.Error);

                // 显示错误信息
                DeviceIdTextBlock.Text = "获取失败";
                UsageFrequencyTextBlock.Text = "获取失败";
                UpdatePriorityTextBlock.Text = "获取失败";
                LaunchCountTextBlock.Text = "获取失败";
                TotalUsageTextBlock.Text = "获取失败";
            }
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
            BorderSettingsMask.Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));

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
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);

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
                await Dispatcher.InvokeAsync(() =>
                {
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
                }, DispatcherPriority.Render);
            }
            catch (Exception)
            {
                // 确保在异常情况下也重新启用滚动事件处理
                SettingsPanelScrollViewer.ScrollChanged += SettingsPanelScrollViewer_ScrollChanged;
            }
        }

        // 滚动条变化事件处理
        private void SettingsPanelScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
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

        #region 插件???

        // 添加插件系统初始化方法
        private void InitializePluginSystem()
        {
            try
            {
                // 初始化插件管理器
                PluginManager.Instance.Initialize();
                LogHelper.WriteLogToFile("插件系统已初始化");
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
                PluginSettingsWindow pluginSettingsWindow = new PluginSettingsWindow();

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
        #endregion 插件???

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
                whiteboardPages[i].Visibility = (i == index) ? Visibility.Visible : Visibility.Collapsed;
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
        // 快速面板退出PPT放映按钮事件
        private void ExitPPTSlideShow_MouseUp(object sender, MouseButtonEventArgs e)
        {
            // 直接调用PPT放映结束按钮的逻辑
            BtnPPTSlideShowEnd_Click(BtnPPTSlideShowEnd, null);
        }

        private void HistoryRollbackButton_Click(object sender, RoutedEventArgs e)
        {
            // 收起设置面板（与插件面板一致）
            BorderSettings.Visibility = Visibility.Hidden;
            BorderSettingsMask.Visibility = Visibility.Hidden;
            var win = new HistoryRollbackWindow(Settings.Startup.UpdateChannel);
            win.ShowDialog();
            // 可选：回滚窗口关闭后恢复设置面板显示
            BorderSettings.Visibility = Visibility.Visible;
            BorderSettingsMask.Visibility = Visibility.Visible;
        }

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOPMOST = 0x00000008;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOOWNERZORDER = 0x0200;

        // 添加定时器来维护置顶状态
        private DispatcherTimer topmostMaintenanceTimer;
        private bool isTopmostMaintenanceEnabled = false;

        private void ApplyNoFocusMode()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            if (Settings.Advanced.IsNoFocusMode)
            {
                SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE);
            }
            else
            {
                SetWindowLong(hwnd, GWL_EXSTYLE, exStyle & ~WS_EX_NOACTIVATE);
            }
        }

        private void ApplyAlwaysOnTop()
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (Settings.Advanced.IsAlwaysOnTop)
                {
                    // 先设置WPF的Topmost属性
                    Topmost = true;
                    
                    // 使用更强的Win32 API调用来确保置顶
                    // 1. 设置窗口样式为置顶
                    int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                    SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOPMOST);
                    
                    // 2. 使用SetWindowPos确保窗口在最顶层
                    SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, 
                        SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_NOOWNERZORDER);
                    
                    // 3. 如果启用了无焦点模式，需要特殊处理
                    if (Settings.Advanced.IsNoFocusMode)
                    {
                        // 启动置顶维护定时器
                        StartTopmostMaintenance();
                    }
                    else
                    {
                        // 停止置顶维护定时器
                        StopTopmostMaintenance();
                    }
                }
                else
                {
                    // 取消置顶时
                    // 1. 先使用Win32 API取消置顶
                    SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, 
                        SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_NOOWNERZORDER);
                    
                    // 2. 移除置顶窗口样式
                    int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                    SetWindowLong(hwnd, GWL_EXSTYLE, exStyle & ~WS_EX_TOPMOST);
                    
                    // 3. 停止置顶维护定时器
                    StopTopmostMaintenance();
                    
                    // 注意：这里不直接设置Topmost，让其他代码根据模式决定
                    
                    // 添加调试日志
                    LogHelper.WriteLogToFile($"应用窗口置顶: 取消置顶", LogHelper.LogType.Trace);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用窗口置顶失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 启动置顶维护定时器
        /// </summary>
        private void StartTopmostMaintenance()
        {
            if (isTopmostMaintenanceEnabled) return;
            
            if (topmostMaintenanceTimer == null)
            {
                topmostMaintenanceTimer = new DispatcherTimer();
                topmostMaintenanceTimer.Interval = TimeSpan.FromMilliseconds(500); // 每500ms检查一次
                topmostMaintenanceTimer.Tick += TopmostMaintenanceTimer_Tick;
            }
            
            topmostMaintenanceTimer.Start();
            isTopmostMaintenanceEnabled = true;
            LogHelper.WriteLogToFile("启动置顶维护定时器", LogHelper.LogType.Trace);
        }

        /// <summary>
        /// 停止置顶维护定时器
        /// </summary>
        private void StopTopmostMaintenance()
        {
            if (topmostMaintenanceTimer != null && isTopmostMaintenanceEnabled)
            {
                topmostMaintenanceTimer.Stop();
                isTopmostMaintenanceEnabled = false;
                LogHelper.WriteLogToFile("停止置顶维护定时器", LogHelper.LogType.Trace);
            }
        }

        /// <summary>
        /// 置顶维护定时器事件
        /// </summary>
        private void TopmostMaintenanceTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (!Settings.Advanced.IsAlwaysOnTop || !Settings.Advanced.IsNoFocusMode)
                {
                    StopTopmostMaintenance();
                    return;
                }

                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;

                // 检查窗口是否仍然可见且不是最小化状态
                if (!IsWindow(hwnd) || !IsWindowVisible(hwnd) || IsIconic(hwnd))
                {
                    return;
                }

                // 检查当前窗口是否在最顶层
                var foregroundWindow = GetForegroundWindow();
                if (foregroundWindow != hwnd)
                {
                    // 如果窗口不在最顶层，重新设置置顶
                    SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, 
                        SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_NOOWNERZORDER);
                    
                    // 确保窗口样式正确
                    int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                    if ((exStyle & WS_EX_TOPMOST) == 0)
                    {
                        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOPMOST);
                    }
                    
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"置顶维护定时器出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 根据窗口置顶设置和当前模式设置窗口的Topmost属性
        /// </summary>
        /// <param name="shouldBeTopmost">当前模式是否需要窗口置顶</param>
        public void SetTopmostBasedOnSettings(bool shouldBeTopmost)
        {
            if (Settings.Advanced.IsAlwaysOnTop)
            {
                // 如果启用了窗口置顶设置，则始终置顶
                Topmost = true;
                ApplyAlwaysOnTop();
            }
            else
            {
                // 如果未启用窗口置顶设置，则根据当前模式决定
                Topmost = shouldBeTopmost;
                if (!shouldBeTopmost)
                {
                    ApplyAlwaysOnTop(); // 确保取消置顶
                }
            }
        }

        private void ToggleSwitchNoFocusMode_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            var toggle = sender as ToggleSwitch;
            Settings.Advanced.IsNoFocusMode = toggle != null && toggle.IsOn;
            SaveSettingsToFile();
            ApplyNoFocusMode();
            
            // 如果启用了窗口置顶，需要重新应用置顶设置以处理无焦点模式的变化
            if (Settings.Advanced.IsAlwaysOnTop)
            {
                ApplyAlwaysOnTop();
            }
        }

        private void ToggleSwitchAlwaysOnTop_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            var toggle = sender as ToggleSwitch;
            Settings.Advanced.IsAlwaysOnTop = toggle != null && toggle.IsOn;
            SaveSettingsToFile();
            ApplyAlwaysOnTop();
        }
        
        private void Window_Activated(object sender, EventArgs e)
        {
            // 窗口激活时，如果启用了置顶功能，重新应用置顶设置
            if (Settings.Advanced.IsAlwaysOnTop)
            {
                // 使用Dispatcher.BeginInvoke确保在UI线程上执行
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ApplyAlwaysOnTop();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }
        
        /// <summary>
        /// 窗口失去焦点时的处理
        /// </summary>
        private void Window_Deactivated(object sender, EventArgs e)
        {
            // 窗口失去焦点时，如果启用了置顶功能且处于无焦点模式，重新应用置顶设置
            if (Settings.Advanced.IsAlwaysOnTop && Settings.Advanced.IsNoFocusMode)
            {
                // 使用Dispatcher.BeginInvoke确保在UI线程上执行
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ApplyAlwaysOnTop();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        #region Image Toolbar Event Handlers

        private void BorderImageClone_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (lastBorderMouseDownObject != sender) return;

            if (selectedUIElement is Image image)
            {
                CloneImage(image);
            }
        }

        private void BorderImageCloneToNewBoard_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (lastBorderMouseDownObject != sender) return;

            if (selectedUIElement is Image image)
            {
                CloneImageToNewBoard(image);
            }
        }

        private void BorderImageRotateLeft_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (lastBorderMouseDownObject != sender) return;

            if (selectedUIElement is Image image)
            {
                RotateImage(image, -90);
            }
        }

        private void BorderImageRotateRight_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (lastBorderMouseDownObject != sender) return;

            if (selectedUIElement is Image image)
            {
                RotateImage(image, 90);
            }
        }

        private void GridImageScaleIncrease_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (lastBorderMouseDownObject != sender) return;

            if (selectedUIElement is Image image)
            {
                ScaleImage(image, 1.25);
            }
        }

        private void GridImageScaleDecrease_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (lastBorderMouseDownObject != sender) return;

            if (selectedUIElement is Image image)
            {
                ScaleImage(image, 0.8);
            }
        }

        private void BorderImageDelete_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (lastBorderMouseDownObject != sender) return;

            if (selectedUIElement is Image image)
            {
                DeleteImage(image);
            }
        }

        #endregion
    }
}
