using Ink_Canvas.Helpers;
using Ink_Canvas.Helpers.Plugins;
using Ink_Canvas.Windows;
using iNKORE.UI.WPF.Modern;
using iNKORE.UI.WPF.Modern.Controls;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;
using Point = System.Windows.Point;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace Ink_Canvas
{
    public partial class MainWindow : Window
    {
        [DllImport("UIAccessDLL_x86.dll", EntryPoint = "PrepareUIAccess", CallingConvention = CallingConvention.Cdecl)]
        public static extern Int32 PrepareUIAccessX86();

        [DllImport("UIAccessDLL_x64.dll", EntryPoint = "PrepareUIAccess", CallingConvention = CallingConvention.Cdecl)]
        public static extern Int32 PrepareUIAccessX64();

        // 每一页一个Canvas对象
        private List<System.Windows.Controls.Canvas> whiteboardPages = new List<System.Windows.Controls.Canvas>();
        private int currentPageIndex;
        private System.Windows.Controls.Canvas currentCanvas;
        private AutoUpdateHelper.UpdateLineGroup AvailableLatestLineGroup;

        // 全局快捷键管理器
        private GlobalHotkeyManager _globalHotkeyManager;

        // 墨迹渐隐管理器
        private InkFadeManager _inkFadeManager;

        // 悬浮窗拦截管理器
        private FloatingWindowInterceptorManager _floatingWindowInterceptorManager;

        // 窗口概览模型
        private WindowOverviewModel _windowOverviewModel;

        // 设置面板相关状态
        private bool isTemporarilyDisablingNoFocusMode = false;

        private bool _isApplyingLanguageFromSettings;
        private bool _isReloadingForLanguageChange;

        // 全屏处理状态标志
        public bool isFullScreenApplied = false;

        private int _boothResolutionWidth = 1920;
        private int _boothResolutionHeight = 1080;
        public int BoothResolutionWidth => _boothResolutionWidth;
        public int BoothResolutionHeight => _boothResolutionHeight;

        private static Cursor _cachedPenCursor = null;
        private static readonly object _cursorLock = new object();

        #region Window Initialization

        /// <summary>
        /// 初始化主窗口实例，构建并配置界面元素、初始页面和应用程序运行时状态。
        /// </summary>
        /// <remarks>
        /// 执行 UI 可见性与布局初始设置、浮动栏位置计算与动画、日志文件清理与调试标记、定时器与撤销/重做绑定、输入事件与墨迹管理器初始化、
        /// 首页画布创建、左右侧面板的触摸滑动与点击分页交互绑定、无焦点与置顶模式应用、滑块触摸支持以及延迟的首-run OOBE 检查等启动工作。
        /// </remarks>
        public MainWindow()
        {
            /*
                处于画板模式内：Topmost == false / currentMode != 0
                处于 PPT 放映内：BtnPPTSlideShowEnd.Visibility
            */
            try
            {
                var path = App.RootPath + settingsFileName;
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var loadedSettings = JsonConvert.DeserializeObject<Settings>(json);
                    var preferredLanguage = loadedSettings?.Appearance?.Language;
                    if (!string.IsNullOrWhiteSpace(preferredLanguage))
                    {
                        LocalizationHelper.TrySetCulture(preferredLanguage);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"启动时预加载语言失败: {ex.Message}", LogHelper.LogType.Error);
            }

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
            // 新增：只在屏幕模式下初始化浮动栏动画
            if (currentMode == 0)
            {
                ViewboxFloatingBarMarginAnimation(100, true);
            }

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

            // 初始化墨迹渐隐管理器
            _inkFadeManager = new InkFadeManager(this);

            // 注册输入事件
            inkCanvas.PreviewMouseDown += inkCanvas_PreviewMouseDown;
            inkCanvas.StylusDown += inkCanvas_StylusDown;
            inkCanvas.MouseRightButtonUp += InkCanvas_MouseRightButtonUp;
            // 注册橡皮擦操作结束事件
            inkCanvas.StylusUp += inkCanvas_StylusUp;

            // 初始化第一页Canvas
            var firstCanvas = new System.Windows.Controls.Canvas();
            whiteboardPages.Add(firstCanvas);
            InkCanvasGridForInkReplay.Children.Add(firstCanvas);
            currentPageIndex = 0;
            ShowPage(currentPageIndex);

            // 手动实现触摸滑动
            const double TouchTapMovementThreshold = 15.0;
            double leftTouchStartY = 0;
            double leftTouchStartX = 0;
            double leftScrollStartOffset = 0;
            bool leftIsTouching = false;
            bool leftTouchDidScroll = false;
            BlackBoardLeftSidePageListScrollViewer.TouchDown += (s, e) =>
            {
                leftIsTouching = true;
                leftTouchDidScroll = false;
                var pt = e.GetTouchPoint(BlackBoardLeftSidePageListScrollViewer).Position;
                leftTouchStartX = pt.X;
                leftTouchStartY = pt.Y;
                leftScrollStartOffset = BlackBoardLeftSidePageListScrollViewer.VerticalOffset;
                BlackBoardLeftSidePageListScrollViewer.CaptureTouch(e.TouchDevice);
                e.Handled = true;
            };
            BlackBoardLeftSidePageListScrollViewer.TouchMove += (s, e) =>
            {
                if (leftIsTouching)
                {
                    var pt = e.GetTouchPoint(BlackBoardLeftSidePageListScrollViewer).Position;
                    double deltaY = leftTouchStartY - pt.Y;
                    double deltaX = pt.X - leftTouchStartX;
                    if (!leftTouchDidScroll && (Math.Abs(deltaY) > TouchTapMovementThreshold || Math.Abs(deltaX) > TouchTapMovementThreshold))
                        leftTouchDidScroll = true;
                    if (leftTouchDidScroll)
                        BlackBoardLeftSidePageListScrollViewer.ScrollToVerticalOffset(leftScrollStartOffset + deltaY);
                    e.Handled = true;
                }
            };
            BlackBoardLeftSidePageListScrollViewer.TouchUp += (s, e) =>
            {
                if (leftIsTouching && !leftTouchDidScroll)
                {
                    var pt = e.GetTouchPoint(BlackBoardLeftSidePageListScrollViewer).Position;
                    double dx = pt.X - leftTouchStartX, dy = pt.Y - leftTouchStartY;
                    if (dx * dx + dy * dy <= TouchTapMovementThreshold * TouchTapMovementThreshold)
                        TrySwitchWhiteboardPageByTouchPoint(BlackBoardLeftSidePageListView, BlackBoardLeftSidePageListScrollViewer, pt);
                }
                leftIsTouching = false;
                leftTouchDidScroll = false;
                BlackBoardLeftSidePageListScrollViewer.ReleaseTouchCapture(e.TouchDevice);
                e.Handled = true;
            };
            double rightTouchStartY = 0;
            double rightTouchStartX = 0;
            double rightScrollStartOffset = 0;
            bool rightIsTouching = false;
            bool rightTouchDidScroll = false;
            BlackBoardRightSidePageListScrollViewer.TouchDown += (s, e) =>
            {
                rightIsTouching = true;
                rightTouchDidScroll = false;
                var pt = e.GetTouchPoint(BlackBoardRightSidePageListScrollViewer).Position;
                rightTouchStartX = pt.X;
                rightTouchStartY = pt.Y;
                rightScrollStartOffset = BlackBoardRightSidePageListScrollViewer.VerticalOffset;
                BlackBoardRightSidePageListScrollViewer.CaptureTouch(e.TouchDevice);
                e.Handled = true;
            };
            BlackBoardRightSidePageListScrollViewer.TouchMove += (s, e) =>
            {
                if (rightIsTouching)
                {
                    var pt = e.GetTouchPoint(BlackBoardRightSidePageListScrollViewer).Position;
                    double deltaY = rightTouchStartY - pt.Y;
                    double deltaX = pt.X - rightTouchStartX;
                    if (!rightTouchDidScroll && (Math.Abs(deltaY) > TouchTapMovementThreshold || Math.Abs(deltaX) > TouchTapMovementThreshold))
                        rightTouchDidScroll = true;
                    if (rightTouchDidScroll)
                        BlackBoardRightSidePageListScrollViewer.ScrollToVerticalOffset(rightScrollStartOffset + deltaY);
                    e.Handled = true;
                }
            };
            BlackBoardRightSidePageListScrollViewer.TouchUp += (s, e) =>
            {
                if (rightIsTouching && !rightTouchDidScroll)
                {
                    var pt = e.GetTouchPoint(BlackBoardRightSidePageListScrollViewer).Position;
                    double dx = pt.X - rightTouchStartX, dy = pt.Y - rightTouchStartY;
                    if (dx * dx + dy * dy <= TouchTapMovementThreshold * TouchTapMovementThreshold)
                        TrySwitchWhiteboardPageByTouchPoint(BlackBoardRightSidePageListView, BlackBoardRightSidePageListScrollViewer, pt);
                }
                rightIsTouching = false;
                rightTouchDidScroll = false;
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
            Activated += Window_Activated;
            Deactivated += Window_Deactivated;

            // 为滑块控件添加触摸事件支持
            AddTouchSupportToSliders();

            // 初始化计时器控件事件
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (TimerControl != null)
                {
                    TimerControl.ShowMinimizedRequested += TimerControl_ShowMinimizedRequested;
                    TimerControl.HideMinimizedRequested += TimerControl_HideMinimizedRequested;
                }

                if (MinimizedTimerControl != null && TimerControl != null)
                {
                    MinimizedTimerControl.SetParentControl(TimerControl);
                }
                CheckAndShowOobe();
            }), DispatcherPriority.Loaded);
        }

        /// <summary>
        /// 在应用启动时检查是否需要展示首次运行引导（OOBE）；如果尚未显示，则延迟触发 OOBE 窗口并在完成后调用 OnOobeCompleted。
        /// </summary>
        /// <remarks>
        /// 在显示 OOBE 时会临时隐藏浮动工具栏（ViewboxFloatingBar）；若显示过程中发生错误，会记录日志并恢复浮动工具栏的可见性。
        /// 该方法捕获内部异常并将错误写入日志，不会向上抛出异常。
        /// </remarks>
        private void CheckAndShowOobe()
        {
            try
            {
                if (Settings?.Startup?.HasShownOobe == false)
                {
                    var oobeTimer = new DispatcherTimer(DispatcherPriority.Loaded, Dispatcher)
                    {
                        Interval = TimeSpan.FromMilliseconds(500)
                    };
                    oobeTimer.Tick += (s, e) =>
                    {
                        oobeTimer.Stop();
                        oobeTimer = null;
                        try
                        {
                            if (ViewboxFloatingBar != null)
                            {
                                ViewboxFloatingBar.Visibility = Visibility.Collapsed;
                            }

                            var oobeWindow = new OobeWindow(Settings);
                            oobeWindow.Owner = this;
                            try
                            {
                                App.IsOobeShowing = true;
                                oobeWindow.ShowDialog();
                            }
                            finally
                            {
                                App.IsOobeShowing = false;
                            }

                            OnOobeCompleted();
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"显示 OOBE 时出错: {ex.Message}", LogHelper.LogType.Error);
                            if (ViewboxFloatingBar != null)
                            {
                                ViewboxFloatingBar.Visibility = Visibility.Visible;
                            }
                        }
                    };
                    oobeTimer.Start();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"检查 OOBE 时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 处理完成首次引导（OOBE）后的状态更新与界面恢复。
        /// </summary>
        /// <remarks>
        /// 将启动配置标记为已显示 OOBE 并持久化；在常规模式（currentMode == 0）下恢复并显示浮动工具栏（并触发边距动画）；记录完成事件或在出错时记录错误信息。
        /// </remarks>
        private void OnOobeCompleted()
        {
            try
            {
                if (Settings?.Startup != null)
                {
                    Settings.Startup.HasShownOobe = true;
                    SaveSettingsToFile();
                }

                LoadSettings(false, skipAutoUpdateCheck: true);

                if (ViewboxFloatingBar != null && currentMode == 0)
                {
                    ViewboxFloatingBar.Visibility = Visibility.Visible;
                    ViewboxFloatingBarMarginAnimation(100, true);
                }

                LogHelper.WriteLogToFile("OOBE 已完成", LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"完成 OOBE 时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 将计时器切换为最小化视图并把最小化容器定位到当前计时器的位置。
        /// </summary>
        /// <remarks>
        /// 计算原计时器容器在窗口中的位置（若容器居中则使用 TransformToAncestor 获取实际坐标，否则使用 Margin 的 Left/Top），
        /// 将该位置应用到最小化容器并把两者的对齐方式设为左上，然后隐藏原计时器并显示最小化容器。
        /// </remarks>
        private void TimerControl_ShowMinimizedRequested(object sender, EventArgs e)
        {
            var timerContainer = FindName("TimerContainer") as FrameworkElement;
            var minimizedContainer = FindName("MinimizedTimerContainer") as FrameworkElement;

            if (timerContainer != null && minimizedContainer != null)
            {
                double x = 0, y = 0;

                if (timerContainer.HorizontalAlignment == HorizontalAlignment.Center &&
                    timerContainer.VerticalAlignment == VerticalAlignment.Center)
                {
                    var timerPoint = timerContainer.TransformToAncestor(this).Transform(new Point(0, 0));
                    x = timerPoint.X;
                    y = timerPoint.Y;
                }
                else
                {
                    var timerMargin = timerContainer.Margin;
                    x = double.IsNaN(timerMargin.Left) ? 0 : timerMargin.Left;
                    y = double.IsNaN(timerMargin.Top) ? 0 : timerMargin.Top;
                }

                minimizedContainer.Margin = new Thickness(x, y, 0, 0);
                minimizedContainer.HorizontalAlignment = HorizontalAlignment.Left;
                minimizedContainer.VerticalAlignment = VerticalAlignment.Top;

                timerContainer.Margin = new Thickness(x, y, 0, 0);
                timerContainer.HorizontalAlignment = HorizontalAlignment.Left;
                timerContainer.VerticalAlignment = VerticalAlignment.Top;

                timerContainer.Visibility = Visibility.Collapsed;
                minimizedContainer.Visibility = Visibility.Visible;
            }
        }

        private void TimerControl_HideMinimizedRequested(object sender, EventArgs e)
        {
            var timerContainer = FindName("TimerContainer") as FrameworkElement;
            var minimizedContainer = FindName("MinimizedTimerContainer") as FrameworkElement;

            if (timerContainer != null && minimizedContainer != null)
            {
                minimizedContainer.Visibility = Visibility.Collapsed;
                timerContainer.Visibility = Visibility.Visible;

                if (TimerControl != null)
                {
                    TimerControl.UpdateActivityTime();
                }
            }
        }

        /// <summary>
        /// 根据DPI缩放因子调整TimerContainer的尺寸
        /// </summary>
        public void AdjustTimerContainerSize()
        {
            try
            {
                var timerContainer = FindName("TimerContainer") as FrameworkElement;
                if (timerContainer == null) return;

                var source = System.Windows.PresentationSource.FromVisual(this);
                if (source != null)
                {
                    var dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
                    var dpiScaleY = source.CompositionTarget.TransformToDevice.M22;

                    // 如果DPI缩放因子大于1.25，则适当缩小容器尺寸
                    // 这样可以确保在高DPI屏幕上，计时器窗口的物理像素大小不会过大
                    if (dpiScaleX > 1.25 || dpiScaleY > 1.25)
                    {
                        // 使用较小的缩放因子来限制最大尺寸
                        double scaleFactor = Math.Min(dpiScaleX, dpiScaleY);

                        // 计算目标物理像素大小（约1350x750物理像素）
                        // 然后转换为逻辑像素
                        double targetPhysicalWidth = 1350;
                        double targetPhysicalHeight = 750;

                        // 转换为逻辑像素
                        double maxWidth = targetPhysicalWidth / scaleFactor;
                        double maxHeight = targetPhysicalHeight / scaleFactor;

                        // 确保不会小于原始尺寸的70%
                        maxWidth = Math.Max(maxWidth, 900 * 0.7);
                        maxHeight = Math.Max(maxHeight, 500 * 0.7);

                        // 应用调整后的尺寸
                        timerContainer.Width = maxWidth;
                        timerContainer.Height = maxHeight;
                    }
                    else
                    {
                        // 标准DPI，使用原始尺寸
                        timerContainer.Width = 900;
                        timerContainer.Height = 500;
                    }
                }
                else
                {
                    // 无法获取DPI信息，使用原始尺寸
                    timerContainer.Width = 900;
                    timerContainer.Height = 500;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"调整TimerContainer尺寸失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }



        #endregion

        #region Ink Canvas Functions

        private Color Ink_DefaultColor = Colors.Red;

        private DrawingAttributes drawingAttributes;
        private InkSmoothingManager _inkSmoothingManager;

        private DispatcherTimer _brushAutoRestoreTimer;

        private bool _isBoardBrushMode;
        private double _savedInkWidthBeforeBoardBrush = 5;

        /// <summary>
        /// 初始化并配置画笔绘制属性并将手势事件处理器附加到 inkCanvas。
        /// </summary>
        /// <remarks>
        /// 根据应用设置（例如高级贝塞尔平滑或 FitToCurve）设置 drawingAttributes 的颜色、宽高及高亮模式；
        /// 最后订阅 inkCanvas 的 Gesture 事件以处理手势交互。
        /// </remarks>
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

                inkCanvas.Gesture += InkCanvas_Gesture;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }


        /// <summary>
        /// 将给定的十六进制颜色字符串规范化为一个带指定不透明度的 Color 值。
        /// </summary>
        /// <param name="hex">颜色字符串（支持 "#RRGGBB", "#AARRGGBB", "RRGGBB" 等形式）；为空或无效时会使用默认值。</param>
        /// <param name="alpha">用于输出颜色的 alpha 通道（0-255）。</param>
        /// <returns>`Color`：返回与输入对应的颜色并应用给定的 alpha；对于若干常用调色板色值会做规范化映射；解析失败时返回带指定 alpha 的纯红色。</returns>
        private static Color GetCanonicalPaletteColorFromHex(string hex, byte alpha)
        {
            if (string.IsNullOrWhiteSpace(hex)) return Color.FromArgb(alpha, 255, 0, 0);

            string n = hex.Trim().ToLowerInvariant();
            if (n.StartsWith("#")) n = n.Substring(1);
            if (n.Length == 8) n = n.Substring(2, 6); // 去掉 AA
            else if (n.Length != 6) n = "";

            if (n.Length == 6)
            {
                if (n == "ffffff") return Color.FromArgb(alpha, 255, 255, 255);
                if (n == "fb9650") return Color.FromArgb(alpha, 251, 150, 80);   // 251,150,80 橙
                if (n == "ffff00") return Color.FromArgb(alpha, 255, 255, 0);
                if (n == "000000") return Color.FromArgb(alpha, 0, 0, 0);
                if (n == "2563eb") return Color.FromArgb(alpha, 37, 99, 235);    // 37,99,235 蓝
                if (n == "ff0000") return Color.FromArgb(alpha, 255, 0, 0);
                if (n == "16a34a") return Color.FromArgb(alpha, 22, 163, 74);    // 22,163,74 绿
                if (n == "9333ea") return Color.FromArgb(alpha, 147, 51, 234);    // 147,51,234 紫
            }

            try
            {
                var converted = ColorConverter.ConvertFromString(hex);
                if (converted is Color parsed)
                {
                    byte r = parsed.R, g = parsed.G, b = parsed.B;
                    if (r == 255 && g == 255 && b == 255) return Color.FromArgb(alpha, 255, 255, 255);
                    if (r == 251 && g == 150 && b == 80) return Color.FromArgb(alpha, 251, 150, 80);
                    if (r == 255 && g == 255 && b == 0) return Color.FromArgb(alpha, 255, 255, 0);
                    if (r == 0 && g == 0 && b == 0) return Color.FromArgb(alpha, 0, 0, 0);
                    if (r == 37 && g == 99 && b == 235) return Color.FromArgb(alpha, 37, 99, 235);
                    if (r == 255 && g == 0 && b == 0) return Color.FromArgb(alpha, 255, 0, 0);
                    if (r == 22 && g == 163 && b == 74) return Color.FromArgb(alpha, 22, 163, 74);
                    if (r == 147 && g == 51 && b == 234) return Color.FromArgb(alpha, 147, 51, 234);
                    return Color.FromArgb(alpha, r, g, b);
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
            return Color.FromArgb(alpha, 255, 0, 0);
        }

        /// <summary>
        /// 立即应用画笔颜色、粗细与高度到当前画布并同步相关状态与 UI 元素。
        /// </summary>
        /// <param name="color">要设置的画笔颜色（包含 alpha 通道）。</param>
        /// <param name="width">要设置的画笔宽度（绘制时使用的逻辑宽度）。</param>
        /// <param name="height">要设置的画笔高度（绘制时使用的逻辑高度）。</param>
        /// <remarks>
        /// 此方法会：
        /// - 更新当前绘图属性和 inkCanvas 的默认绘图属性的颜色与尺寸（在 penType != 1 时更新宽高）。
        /// - 根据当前模式（桌面或白板）记录最近使用的颜色索引用于后续恢复或 UI 显示。
        /// - 同步 Settings.Canvas 中的 InkWidth 与 InkAlpha 值（如果 Settings 可用）。
        /// - 更新相关的宽度与透明度滑块值（若对应控件已初始化）。
        /// - 调用主题检查以确保颜色主题一致性并更新内部的 Ink_DefaultColor 状态。
        /// </remarks>
        private void SetBrushAttributesDirectly(Color color, double width, double height)
        {
            try
            {
                if (!Dispatcher.CheckAccess())
                {
                    Dispatcher.Invoke(() => SetBrushAttributesDirectly(color, width, height));
                    return;
                }

                if (drawingAttributes == null)
                {
                    drawingAttributes = inkCanvas.DefaultDrawingAttributes;
                }

                Color rgbColor = Color.FromRgb(color.R, color.G, color.B);
                if (currentMode == 0)
                {
                    if (rgbColor == Colors.White) lastDesktopInkColor = 5;
                    else if (rgbColor == Color.FromRgb(251, 150, 80)) lastDesktopInkColor = 8;
                    else if (rgbColor == Colors.Yellow) lastDesktopInkColor = 4;
                    else if (rgbColor == Colors.Black) lastDesktopInkColor = 0;
                    else if (rgbColor == Color.FromRgb(37, 99, 235)) lastDesktopInkColor = 3;
                    else if (rgbColor == Colors.Red) lastDesktopInkColor = 1;
                    else if (rgbColor == Colors.Green || rgbColor == Color.FromRgb(22, 163, 74)) lastDesktopInkColor = 2;
                    else if (rgbColor == Color.FromRgb(147, 51, 234)) lastDesktopInkColor = 6;
                }
                else
                {
                    if (rgbColor == Colors.White) lastBoardInkColor = 5;
                    else if (rgbColor == Color.FromRgb(251, 150, 80)) lastBoardInkColor = 8;
                    else if (rgbColor == Colors.Yellow) lastBoardInkColor = 4;
                    else if (rgbColor == Colors.Black) lastBoardInkColor = 0;
                    else if (rgbColor == Color.FromRgb(37, 99, 235)) lastBoardInkColor = 3;
                    else if (rgbColor == Colors.Red) lastBoardInkColor = 1;
                    else if (rgbColor == Colors.Green || rgbColor == Color.FromRgb(22, 163, 74)) lastBoardInkColor = 2;
                    else if (rgbColor == Color.FromRgb(147, 51, 234)) lastBoardInkColor = 6;
                }

                var colorWithAlpha = Color.FromArgb(color.A, color.R, color.G, color.B);
                drawingAttributes.Color = colorWithAlpha;
                inkCanvas.DefaultDrawingAttributes.Color = colorWithAlpha;

                CheckColorTheme();

                Ink_DefaultColor = inkCanvas.DefaultDrawingAttributes.Color;

                // 粗细与透明度
                if (penType != 1)
                {
                    drawingAttributes.Width = width;
                    drawingAttributes.Height = height;
                    inkCanvas.DefaultDrawingAttributes.Width = width;
                    inkCanvas.DefaultDrawingAttributes.Height = height;
                }

                if (Settings?.Canvas != null)
                {
                    Settings.Canvas.InkWidth = width;
                    Settings.Canvas.InkAlpha = (int)color.A;
                }

                if (InkWidthSlider != null) InkWidthSlider.Value = width * 2;
                if (InkAlphaSlider != null) InkAlphaSlider.Value = color.A;
                if (BoardInkWidthSlider != null) BoardInkWidthSlider.Value = width * 2;
                if (BoardInkAlphaSlider != null) BoardInkAlphaSlider.Value = color.A;

                if (penType != 1)
                {
                    drawingAttributes.Width = width;
                    drawingAttributes.Height = height;
                    inkCanvas.DefaultDrawingAttributes.Width = width;
                    inkCanvas.DefaultDrawingAttributes.Height = height;
                }

            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"SetBrushAttributesDirectly: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private const double BoardBrushInkWidth = 16;
        private const double BoardBrushInkHeight = 50;

        /// <summary>
        /// 切换“板刷”模式：在板刷与普通画笔间切换，保存/恢复画笔宽度，更新 InkCanvas 的 DrawingAttributes（宽度、高度、笔尖形状、是否忽略压力等），并同步相关 UI 状态（按钮背景、滑块值）与 Settings.Canvas.InkWidth。
        /// </summary>
        private void BoardBrushModeButton_Click(object sender, RoutedEventArgs e)
        {
            _isBoardBrushMode = !_isBoardBrushMode;

            try
            {
                if (drawingAttributes == null)
                    drawingAttributes = inkCanvas.DefaultDrawingAttributes;

                if (penType == 1) return;

                if (_isBoardBrushMode)
                {
                    _savedInkWidthBeforeBoardBrush = InkWidthSlider != null ? InkWidthSlider.Value / 2.0 : drawingAttributes.Width;
                    if (_savedInkWidthBeforeBoardBrush < 0.5) _savedInkWidthBeforeBoardBrush = 2.5;

                    drawingAttributes.Width = BoardBrushInkWidth;
                    drawingAttributes.Height = BoardBrushInkHeight;
                    inkCanvas.DefaultDrawingAttributes.Width = BoardBrushInkWidth;
                    inkCanvas.DefaultDrawingAttributes.Height = BoardBrushInkHeight;
                    drawingAttributes.StylusTip = StylusTip.Rectangle;
                    inkCanvas.DefaultDrawingAttributes.StylusTip = StylusTip.Rectangle;
                    drawingAttributes.IgnorePressure = true;
                    inkCanvas.DefaultDrawingAttributes.IgnorePressure = true;

                    if (BoardBrushModeButton != null)
                        BoardBrushModeButton.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                }
                else
                {
                    double w = InkWidthSlider != null ? InkWidthSlider.Value / 2.0 : _savedInkWidthBeforeBoardBrush;
                    if (w < 0.5) w = 2.5;

                    drawingAttributes.Width = w;
                    drawingAttributes.Height = w;
                    inkCanvas.DefaultDrawingAttributes.Width = w;
                    inkCanvas.DefaultDrawingAttributes.Height = w;
                    drawingAttributes.StylusTip = StylusTip.Ellipse;
                    inkCanvas.DefaultDrawingAttributes.StylusTip = StylusTip.Ellipse;
                    drawingAttributes.IgnorePressure = Settings.Canvas.DisablePressure;
                    inkCanvas.DefaultDrawingAttributes.IgnorePressure = Settings.Canvas.DisablePressure;

                    if (BoardInkWidthSlider != null) BoardInkWidthSlider.Value = w * 2;
                    if (Settings?.Canvas != null) Settings.Canvas.InkWidth = w;

                    if (BoardBrushModeButton != null)
                        BoardBrushModeButton.ClearValue(BackgroundProperty);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"BoardBrushModeButton_Click: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 切换“画板画笔”（Board brush）模式，并将画笔属性与相关 UI 状态同步为画板或普通画笔配置。
        /// </summary>
        /// <remarks>
        /// - 在点击事件的发送者不是 BoardBrushModeButton 或最后一次按下的对象不匹配时不会执行任何操作。  
        /// - 切换为画板模式时会保存当前宽度、设置矩形笔尖、禁用压力感应并将画笔宽高调整为画板预设值，同时将按钮背景置为激活色。  
        /// - 取消画板模式时会恢复之前保存的宽度（并更新滑块与 Settings.Canvas.InkWidth）、恢复椭圆笔尖和压力感应设置，并清除按钮的自定义背景。  
        /// - 如果当前 penType 等于 1，则在切换内部模式标志后不会修改画笔属性或 UI。  
        /// - 内部异常会被捕获并记录，但不会向调用者抛出异常。
        /// </remarks>
        private void BoardBrushModeButton_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender != BoardBrushModeButton) return;
            if (lastBorderMouseDownObject != BoardBrushModeButton) return;

            _isBoardBrushMode = !_isBoardBrushMode;

            try
            {
                if (drawingAttributes == null)
                    drawingAttributes = inkCanvas.DefaultDrawingAttributes;

                if (penType == 1) return;

                if (_isBoardBrushMode)
                {
                    _savedInkWidthBeforeBoardBrush = InkWidthSlider != null ? InkWidthSlider.Value / 2.0 : drawingAttributes.Width;
                    if (_savedInkWidthBeforeBoardBrush < 0.5) _savedInkWidthBeforeBoardBrush = 2.5;

                    drawingAttributes.Width = BoardBrushInkWidth;
                    drawingAttributes.Height = BoardBrushInkHeight;
                    inkCanvas.DefaultDrawingAttributes.Width = BoardBrushInkWidth;
                    inkCanvas.DefaultDrawingAttributes.Height = BoardBrushInkHeight;
                    drawingAttributes.StylusTip = StylusTip.Rectangle;
                    inkCanvas.DefaultDrawingAttributes.StylusTip = StylusTip.Rectangle;
                    drawingAttributes.IgnorePressure = true;
                    inkCanvas.DefaultDrawingAttributes.IgnorePressure = true;

                    if (BoardBrushModeButton != null)
                        BoardBrushModeButton.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                }
                else
                {
                    double w = InkWidthSlider != null ? InkWidthSlider.Value / 2.0 : _savedInkWidthBeforeBoardBrush;
                    if (w < 0.5) w = 2.5;

                    drawingAttributes.Width = w;
                    drawingAttributes.Height = w;
                    inkCanvas.DefaultDrawingAttributes.Width = w;
                    inkCanvas.DefaultDrawingAttributes.Height = w;
                    drawingAttributes.StylusTip = StylusTip.Ellipse;
                    inkCanvas.DefaultDrawingAttributes.StylusTip = StylusTip.Ellipse;
                    drawingAttributes.IgnorePressure = Settings.Canvas.DisablePressure;
                    inkCanvas.DefaultDrawingAttributes.IgnorePressure = Settings.Canvas.DisablePressure;

                    if (BoardInkWidthSlider != null) BoardInkWidthSlider.Value = w * 2;
                    if (Settings?.Canvas != null) Settings.Canvas.InkWidth = w;

                    if (BoardBrushModeButton != null)
                        BoardBrushModeButton.ClearValue(BackgroundProperty);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"BoardBrushModeButton_MouseUp: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 初始化用于自动恢复画笔属性的计时器并应用当前的时间间隔设置。
        /// </summary>
        private void InitBrushAutoRestoreTimer()
        {
            if (_brushAutoRestoreTimer == null)
            {
                _brushAutoRestoreTimer = new DispatcherTimer();
                _brushAutoRestoreTimer.Tick += BrushAutoRestoreTimer_Tick;
            }

            UpdateBrushAutoRestoreTimerInterval();
        }

        /// <summary>
        /// — 根据配置计算并设置画笔自动恢复计时器的下次间隔。
        /// </summary>
        /// <remarks>
        /// 优先尝试从 Settings.Canvas.BrushAutoRestoreTimes 解析一组时间点（支持 ';', '；', ',', '，' 分隔），
        /// 并选择距离当前时间的下一个时间点来计算间隔（若当天无剩余时间点则选择下一天的最早时间点）。
        /// 若未提供有效时间点或解析失败，则使用 Settings.Canvas.BrushAutoRestoreDelaySeconds（最小为 1 秒）作为间隔。
        /// 计算得到的间隔最终赋值给 _brushAutoRestoreTimer.Interval。
        /// </remarks>
        private void UpdateBrushAutoRestoreTimerInterval()
        {
            if (_brushAutoRestoreTimer == null) return;

            TimeSpan? nextInterval = null;
            try
            {
                var timesConfig = Settings?.Canvas?.BrushAutoRestoreTimes;
                if (!string.IsNullOrWhiteSpace(timesConfig))
                {
                    var parts = timesConfig
                        .Split(new[] { ';', '；', ',', '，' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => p.Trim())
                        .ToList();

                    var validTimes = new List<TimeSpan>();
                    foreach (var part in parts)
                    {
                        if (TimeSpan.TryParse(part, out var ts) &&
                            ts >= TimeSpan.Zero &&
                            ts < TimeSpan.FromDays(1))
                        {
                            validTimes.Add(ts);
                        }
                    }

                    if (validTimes.Count > 0)
                    {
                        var now = DateTime.Now;
                        var today = now.Date;
                        var nowTod = now.TimeOfDay;

                        TimeSpan? todayNext = null;
                        foreach (var t in validTimes)
                        {
                            if (t >= nowTod)
                            {
                                if (todayNext == null || t < todayNext.Value)
                                {
                                    todayNext = t;
                                }
                            }
                        }

                        DateTime target;
                        if (todayNext.HasValue)
                        {
                            target = today + todayNext.Value;
                        }
                        else
                        {
                            var firstTime = validTimes.OrderBy(t => t).First();
                            target = today.AddDays(1) + firstTime;
                        }

                        var interval = target - now;
                        if (interval < TimeSpan.FromSeconds(1))
                        {
                            interval = TimeSpan.FromSeconds(1);
                        }
                        nextInterval = interval;
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            if (!nextInterval.HasValue)
            {
                int seconds = Settings?.Canvas?.BrushAutoRestoreDelaySeconds ?? 0;
                if (seconds < 1) seconds = 1;
                nextInterval = TimeSpan.FromSeconds(seconds);
            }

            _brushAutoRestoreTimer.Interval = nextInterval.Value;
        }

        /// <summary>
        /// 安排（初始化并启动或重启）画笔自动恢复计时器，以便在计时器到期时恢复画笔的预设属性。
        /// </summary>
        /// <remarks>
        /// 如果全局设置或画布设置为空，或未启用画笔自动恢复，则不会进行任何操作。
        /// 在需要时会初始化计时器或更新其间隔，然后停止并重新启动计时器以重置计时周期。
        /// 方法内部捕获并记录异常，不会将异常向上传播。
        /// </remarks>
        internal void ScheduleBrushAutoRestore()
        {
            try
            {
                if (Settings == null || Settings.Canvas == null || !Settings.Canvas.EnableBrushAutoRestore)
                {
                    return;
                }

                if (_brushAutoRestoreTimer == null)
                {
                    InitBrushAutoRestoreTimer();
                }
                else
                {
                    UpdateBrushAutoRestoreTimerInterval();
                }

                _brushAutoRestoreTimer.Stop();
                _brushAutoRestoreTimer.Start();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"ScheduleBrushAutoRestore: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 在自动还原画笔定时器触发时，将画笔属性恢复为用户设置的颜色、不透明度和宽度，并重置定时器间隔以继续周期性还原。
        /// </summary>
        /// <remarks>
        /// 如果设置未启用或缺失则不会进行任何操作。透明度会限定在 0 到 255 之间；当配置宽度无效时使用当前画笔宽度或默认值作为回退值。
        /// </remarks>
        private void BrushAutoRestoreTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                _brushAutoRestoreTimer.Stop();

                if (Settings == null || Settings.Canvas == null || !Settings.Canvas.EnableBrushAutoRestore)
                {
                    return;
                }

                if (drawingAttributes == null)
                {
                    drawingAttributes = inkCanvas.DefaultDrawingAttributes;
                }

                int alphaConfig = Settings.Canvas.BrushAutoRestoreAlpha;
                if (alphaConfig < 0) alphaConfig = 0;
                if (alphaConfig > 255) alphaConfig = 255;
                byte alpha = (byte)alphaConfig;

                Color targetColor = GetCanonicalPaletteColorFromHex(Settings.Canvas.BrushAutoRestoreColor ?? "", alpha);

                double sliderValue = Settings.Canvas.BrushAutoRestoreWidth;
                double width;
                if (sliderValue <= 0)
                {
                    width = Settings.Canvas.InkWidth > 0 ? Settings.Canvas.InkWidth : 2.5;
                }
                else
                {
                    width = sliderValue / 2.0;
                }

                SetBrushAttributesDirectly(targetColor, width, width);

                UpdateBrushAutoRestoreTimerInterval();
                _brushAutoRestoreTimer.Start();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"BrushAutoRestoreTimer_Tick: {ex.Message}", LogHelper.LogType.Error);
            }
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
                    // 只有在PPT放映模式下才响应翻页手势
                    if (StackPanelPPTControls.Visibility == Visibility.Visible &&
                        BtnPPTSlideShowEnd.Visibility == Visibility.Visible &&
                        PPTManager?.IsInSlideShow == true)
                    {
                        if (gest.ApplicationGesture == ApplicationGesture.Left)
                        {
                            BtnPPTSlidesDown_Click(null, null); // 下一页
                        }
                        if (gest.ApplicationGesture == ApplicationGesture.Right)
                        {
                            BtnPPTSlidesUp_Click(null, null); // 上一页
                        }
                    }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
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

            // 处理橡皮擦覆盖层的启用/禁用
            var eraserOverlay = FindName("EraserOverlayCanvas") as Canvas;
            if (eraserOverlay != null)
            {
                if (inkCanvas1.EditingMode == InkCanvasEditingMode.EraseByPoint)
                {
                    // 橡皮擦模式下启用覆盖层
                    EnableEraserOverlay();
                    Trace.WriteLine("Eraser: Overlay enabled in eraser mode");
                }
                else
                {
                    // 其他模式下禁用覆盖层
                    DisableEraserOverlay();
                    Trace.WriteLine("Eraser: Overlay disabled in non-eraser mode");
                }
            }
        }

        #endregion Ink Canvas

        #region Definations and Loading

        public static Settings Settings = new Settings();
        public static string settingsFileName = Path.Combine("Configs", "Settings.json");
        private bool isLoaded;
        private bool forcePointEraser;

        /// <summary>
        /// 在窗口加载完成后初始化应用的核心子系统、UI 状态和运行时监控组件。
        /// </summary>
        /// <remarks>
        /// 执行设置加载与修复、主题与背景应用、PPT 与插件相关管理器初始化、全局功能（剪贴板监控、全局快捷键、墨迹渐隐等）初始化，恢复启动参数相关状态（白板/显示模式、崩溃后动作等），注册必要的系统与控件事件，并为计时器、滑块触摸与画笔性能（如 IA 加载、画笔恢复等）做好预热与绑定。该方法为窗口呈现后的完整准备流程，不包含具体 UI 交互逻辑的实现细节描述。
        /// </remarks>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            loadPenCanvas();
            //加载设置
            LoadSettings(true);
            ApplyLanguageFromSettings();
            AutoBackupManager.Initialize(Settings);
            CheckUpdateChannelAndTelemetryConsistency();

            // 初始化上传队列（恢复上次的上传队列）
            try
            {
                UploadQueueHelper.InitializeAllQueues();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[MainWindow] 初始化上传队列时出错: {ex.Message}", LogHelper.LogType.Error);
                // 继续执行其他初始化操作，不中断整个加载过程
            }

            _ = TelemetryUploader.UploadTelemetryIfNeededAsync();

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
                    string newPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Saves");
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

            // 设置窗口模式
            SetWindowMode();

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

            // 初始化窗口概览模型
            try
            {
                _windowOverviewModel = new WindowOverviewModel();
                LogHelper.WriteLogToFile("窗口概览模型已初始化", LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"初始化窗口概览模型失败: {ex.Message}", LogHelper.LogType.Error);
            }

            // 如果启用PowerPoint联动增强功能，启动进程守护
            if (Settings.PowerPointSettings.EnablePowerPointEnhancement)
            {
                StartPowerPointProcessMonitoring();
            }

            // HasNewUpdateWindow hasNewUpdateWindow = new HasNewUpdateWindow();
            if (Environment.Is64BitProcess) GroupBoxInkRecognition.Visibility = Visibility.Collapsed;

            // 根据设置应用主题
            switch (Settings.Appearance.Theme)
            {
                case 0: // 浅色主题
                    ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;
                    SetTheme("Light");
                    break;
                case 1: // 深色主题
                    ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
                    SetTheme("Dark");
                    break;
                case 2: // 跟随系统
                    if (IsSystemThemeLight())
                    {
                        ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;
                        SetTheme("Light");
                    }
                    else
                    {
                        ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
                        SetTheme("Dark");
                    }
                    break;
            }

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

            // 提前加载识别后端，优化第一笔等待时间
            if (ShapeRecognitionRouter.ShouldRunShapeRecognition(
                    Settings.InkToShape.IsInkToShapeEnabled,
                    ShapeRecognitionRouter.FromSettingsInt(Settings.InkToShape.ShapeRecognitionEngine)))
            {
                InkRecognizeHelper.WarmupShapeRecognition(
                    ShapeRecognitionRouter.FromSettingsInt(Settings.InkToShape.ShapeRecognitionEngine));
            }

            SystemEvents.DisplaySettingsChanged += SystemEventsOnDisplaySettingsChanged;
            // 自动收纳到侧边栏（若通过 --board 进入白板模式或 --show 参数则跳过收纳）
            if (Settings.Startup.IsFoldAtStartup && !App.StartWithBoardMode && !App.StartWithShowMode)
            {
                FoldFloatingBar_MouseUp(new object(), null);
                ScheduleStartupFoldAbsenceVerification();
            }

            // 恢复崩溃后操作设置
            if (App.CrashAction == App.CrashActionType.SilentRestart)
                RadioCrashSilentRestart.IsChecked = true;
            else
                RadioCrashNoAction.IsChecked = true;

            // 显示快抽悬浮按钮
            ShowQuickDrawFloatingButton();

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

            // 初始化UIA置顶开关
            ToggleSwitchUIAccessTopMost.IsOn = Settings.Advanced.EnableUIAccessTopMost;
            UpdateUIAccessTopMostVisibility();

            App.IsUIAccessTopMostEnabled = Settings.Advanced.EnableUIAccessTopMost;

            // 初始化橡皮擦自动切换回批注模式开关
            if (ToggleSwitchEnableEraserAutoSwitchBack != null)
            {
                ToggleSwitchEnableEraserAutoSwitchBack.IsOn = Settings.Canvas.EnableEraserAutoSwitchBack;
            }
            if (EraserAutoSwitchBackDelaySlider != null)
            {
                EraserAutoSwitchBackDelaySlider.Value = Settings.Canvas.EraserAutoSwitchBackDelaySeconds;
            }

            // 初始化剪贴板监控
            InitializeClipboardMonitoring();

            // 初始化悬浮窗拦截管理器
            InitializeFloatingWindowInterceptor();

            // 初始化全局快捷键管理器
            InitializeGlobalHotkeyManager();

            // 初始化墨迹渐隐管理器
            InitializeInkFadeManager();

            // 处理命令行参数中的文件路径
            HandleCommandLineFileOpen();

            // 初始化文件关联状态显示
            InitializeFileAssociationStatus();

            // 检查模式设置并应用
            CheckMainWindowVisibility();

            // 检查是否通过--board参数启动，如果是则自动切换到白板模式
            if (App.StartWithBoardMode)
            {
                LogHelper.WriteLogToFile("检测到--board参数，自动切换到白板模式", LogHelper.LogType.Event);
                // 延迟执行，确保UI已完全加载
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    SwitchToBoardMode();
                }), DispatcherPriority.Loaded);
            }

            // 检查是否通过--show参数启动，如果是则确保退出收纳模式并恢复浮动栏
            if (App.StartWithShowMode)
            {
                LogHelper.WriteLogToFile("检测到--show参数，退出收纳模式并恢复浮动栏", LogHelper.LogType.Event);
                // 延迟执行，确保UI已完全加载
                Dispatcher.BeginInvoke(new Action(async () =>
                {
                    // 如果当前处于收纳模式，则展开浮动栏
                    if (isFloatingBarFolded)
                    {
                        await UnFoldFloatingBar(new object());
                    }
                }), DispatcherPriority.Loaded);
            }

            // 初始化计时器控件关联
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (TimerControl != null && MinimizedTimerControl != null)
                {
                    MinimizedTimerControl.SetParentControl(TimerControl);

                    // 设置PPT时间胶囊的父控件
                    if (PPTTimeCapsule != null)
                    {
                        PPTTimeCapsule.SetParentControl(TimerControl);
                    }

                    TimerControl.ShowMinimizedRequested += (s, args) =>
                    {
                        if (TimerContainer != null && MinimizedTimerContainer != null && MinimizedTimerControl != null)
                        {
                            TimerContainer.Visibility = Visibility.Collapsed;

                            if (Settings.PowerPointSettings.EnablePPTTimeCapsule &&
                                BtnPPTSlideShowEnd.Visibility == Visibility.Visible &&
                                PPTTimeCapsule != null)
                            {
                                MinimizedTimerContainer.Visibility = Visibility.Collapsed;
                            }
                            else
                            {
                                MinimizedTimerContainer.Visibility = Visibility.Visible;
                                MinimizedTimerControl.Visibility = Visibility.Visible;
                            }
                        }
                    };

                    TimerControl.HideMinimizedRequested += (s, args) =>
                    {
                        if (MinimizedTimerContainer != null && MinimizedTimerControl != null)
                        {
                            MinimizedTimerContainer.Visibility = Visibility.Collapsed;
                            MinimizedTimerControl.Visibility = Visibility.Collapsed;
                        }

                        // 如果启用了PPT时间胶囊，停止倒计时显示
                        if (Settings.PowerPointSettings.EnablePPTTimeCapsule && PPTTimeCapsule != null)
                        {
                            PPTTimeCapsule.StopCountdown();
                        }
                    };

                    // 监听计时器完成事件
                    TimerControl.TimerCompleted += (s, args) =>
                    {
                        // 如果启用了PPT时间胶囊且在PPT模式下，触发完成动画
                        if (Settings.PowerPointSettings.EnablePPTTimeCapsule &&
                            BtnPPTSlideShowEnd.Visibility == Visibility.Visible &&
                            PPTTimeCapsule != null)
                        {
                            PPTTimeCapsule.OnTimerCompleted();
                        }
                    };
                }
            }), DispatcherPriority.Loaded);
            AddTouchSupportToSliders();
        }

        private void ApplyLanguageFromSettings()
        {
            try
            {
                if (ComboBoxLanguage == null || Settings?.Appearance == null) return;

                var preferredLanguage = Settings.Appearance.Language ?? string.Empty;
                int index;

                if (string.IsNullOrWhiteSpace(preferredLanguage))
                {
                    index = 0;
                }
                else if (string.Equals(preferredLanguage, "zh-CN", StringComparison.OrdinalIgnoreCase))
                {
                    index = 1;
                }
                else if (string.Equals(preferredLanguage, "en-US", StringComparison.OrdinalIgnoreCase))
                {
                    index = 2;
                }
                else
                {
                    index = 0;
                }

                _isApplyingLanguageFromSettings = true;
                try
                {
                    ComboBoxLanguage.SelectedIndex = index;
                }
                finally
                {
                    _isApplyingLanguageFromSettings = false;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"初始化语言选项失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }


        /// <summary>
        /// 响应显示器/分辨率配置变化：在检测启用时显示分辨率变更通知，并在后台检查悬浮工具栏是否位于屏幕之外，若是则在延迟后尝试将其通过动画恢复到可见区域（在演示模式下使用不同的动画偏移）。 
        /// </summary>
        /// <param name="sender">触发事件的源对象（通常由系统事件触发）。</param>
        /// <param name="e">事件参数（未使用）。</param>
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

                // 如果TimerContainer可见，调整其尺寸
                Dispatcher.Invoke(() =>
                {
                    var timerContainer = FindName("TimerContainer") as FrameworkElement;
                    if (timerContainer != null && timerContainer.Visibility == Visibility.Visible)
                    {
                        AdjustTimerContainerSize();
                    }
                });

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

        /// <summary>
        /// 根据 Settings.Advanced.WindowMode 切换窗口显示模式。
        /// </summary>
        /// <remarks>
        /// 如果该设置为 true，将窗口置为普通状态并调整到主屏幕的左上角(0,0)及主屏幕分辨率的宽高，使窗口覆盖整个主屏幕；
        /// 否则将窗口设为最大化状态。
        /// </remarks>
        private void SetWindowMode()
        {
            if (Settings.Advanced.WindowMode)
            {
                WindowState = WindowState.Normal;
                Left = 0.0;
                Top = 0.0;
                Height = SystemParameters.PrimaryScreenHeight;
                Width = SystemParameters.PrimaryScreenWidth;
            }
            else // 全屏
            {
                WindowState = WindowState.Maximized;
            }
        }

        private bool _allowCloseAfterExitVerification;
        private bool _isExitVerificationInProgress;

        /// <summary>
        /// 处理主窗口的关闭流程：记录关闭事件，按需进行退出密码验证或多次确认并据此取消或允许关闭。
        /// </summary>
        /// <remarks>
        /// - 会首先写入关闭日志。 
        /// - 如果启用了退出密码验证，事件会被取消并异步弹出密码验证对话；验证通过后会再次触发关闭。 
        /// - 如果设置了“关闭时二次确认”，会依次弹出最多三个确认对话框，任一对话被取消则终止关闭。 
        /// - 在任何取消关闭的情况下都会写入相应的日志记录。 
        /// </remarks>
        /// <param name="sender">触发关闭事件的源对象（通常为窗口本身）。</param>
        /// <param name="e">关闭事件参数；方法会在需要中止关闭时将 <c>e.Cancel</c> 设为 <c>true</c>。</param>
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            LogHelper.WriteLogToFile("Ink Canvas closing", LogHelper.LogType.Event);

            if (_allowCloseAfterExitVerification)
            {
                _allowCloseAfterExitVerification = false;
                return;
            }

            if (BtnPPTSlideShowEnd != null && BtnPPTSlideShowEnd.Visibility == Visibility.Visible)
            {
                e.Cancel = true;
                BtnPPTSlideShowEnd_Click(BtnPPTSlideShowEnd, null);
                LogHelper.WriteLogToFile("Ink Canvas closing converted to exit PPT", LogHelper.LogType.Event);
                return;
            }
            if (currentMode != 0)
            {
                e.Cancel = true;
                CloseWhiteboardImmediately();
                LogHelper.WriteLogToFile("Ink Canvas closing converted to exit whiteboard", LogHelper.LogType.Event);
                return;
            }

            try
            {
                // 快抽按钮现在集成在主窗口中，不需要单独关闭
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"关闭快抽悬浮按钮时出错: {ex.Message}", LogHelper.LogType.Error);
            }

            try
            {
                if (!App.IsUpdateInstalling && SecurityManager.IsPasswordRequiredForExit(Settings))
                {
                    e.Cancel = true;
                    if (_isExitVerificationInProgress) return;

                    _isExitVerificationInProgress = true;
                    Dispatcher.BeginInvoke(new Action(async () =>
                    {
                        try
                        {
                            bool ok = await SecurityManager.PromptAndVerifyAsync(Settings, this, "退出验证", "请输入安全密码以退出软件。");
                            if (!ok)
                            {
                                LogHelper.WriteLogToFile("Ink Canvas closing cancelled by security password", LogHelper.LogType.Event);
                                return;
                            }

                            _allowCloseAfterExitVerification = true;
                            Close();
                        }
                        catch
                        {
                        }
                        finally
                        {
                            _isExitVerificationInProgress = false;
                        }
                    }), DispatcherPriority.Normal);
                    return;
                }
            }
            catch
            {
            }

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


        /// <summary>
        /// 在窗口关闭时释放和清理所有相关资源并执行退出流程。
        /// </summary>
        /// <param name="sender">触发关闭事件的对象（通常为主窗口）。</param>
        /// <param name="e">关闭事件的参数（未使用）。</param>
        private void Window_Closed(object sender, EventArgs e)
        {
            SystemEvents.DisplaySettingsChanged -= SystemEventsOnDisplaySettingsChanged;

            try
            {
                // 清理视频展台资源
                if (_cameraService != null)
                {
                    _cameraService.FrameReceived -= CameraService_FrameReceived;
                    _cameraService.ErrorOccurred -= CameraService_ErrorOccurred;
                    _cameraService.Dispose();
                    _cameraService = null;
                }
                lock (_videoPresenterFrameLock)
                {
                    _lastFrame?.Dispose();
                    _lastFrame = null;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }

            // 释放PPT管理器资源
            DisposePPTManagers();

            // 清理剪贴板监控
            CleanupClipboardMonitoring();
            ClipboardNotification.Stop();

            // 清理全局快捷键管理器
            if (_globalHotkeyManager != null)
            {
                _globalHotkeyManager.Dispose();
                _globalHotkeyManager = null;
            }

            // 清理墨迹渐隐管理器
            if (_inkFadeManager != null)
            {
                _inkFadeManager.ClearAllFadingStrokes();
                _inkFadeManager = null;
            }

            // 清理悬浮窗拦截管理器
            if (_floatingWindowInterceptorManager != null)
            {
                _floatingWindowInterceptorManager.Dispose();
                _floatingWindowInterceptorManager = null;
            }

            // 清理窗口概览模型
            if (_windowOverviewModel != null)
            {
                _windowOverviewModel.Dispose();
                _windowOverviewModel = null;
            }

            // 停止置顶维护定时器
            StopTopmostMaintenance();

            UninstallKeyboardHook();

            // 从Z-Order管理器中移除主窗口
            WindowZOrderManager.UnregisterWindow(this);

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
                    string statusFilePath = AutoUpdateHelper.GetUpdateDownloadStatusFilePath(AvailableLatestVersion);

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

        // 使用多线路组下载更新
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
            if (!string.IsNullOrEmpty(Settings.Startup.AutoUpdatePauseUntilDate))
            {
                if (DateTime.TryParse(Settings.Startup.AutoUpdatePauseUntilDate, out DateTime pauseUntilDate))
                {
                    if (DateTime.Now < pauseUntilDate)
                    {
                        LogHelper.WriteLogToFile($"AutoUpdate | 自动更新已暂停，直到 {pauseUntilDate:yyyy-MM-dd}");
                        return;
                    }
                    else
                    {
                        LogHelper.WriteLogToFile($"AutoUpdate | 暂停期已过，恢复自动更新检查");
                        Settings.Startup.AutoUpdatePauseUntilDate = "";
                        SaveSettingsToFile();
                    }
                }
            }

            // 清除之前的更新状态，确保使用新通道重新检查
            AvailableLatestVersion = null;
            AvailableLatestLineGroup = null;

            // 使用当前选择的更新通道检查更新
            var (remoteVersion, lineGroup, apiReleaseNotes) = await AutoUpdateHelper.CheckForUpdates(Settings.Startup.UpdateChannel);
            AvailableLatestVersion = remoteVersion;
            AvailableLatestLineGroup = lineGroup;

            // 声明下载状态变量，用于整个方法
            bool isDownloadSuccessful = false;

            bool hasValidLineGroup = lineGroup != null;

            if (AvailableLatestVersion != null)
            {
                // 检测到新版本，停止重试定时器
                timerCheckAutoUpdateRetry.Stop();
                updateCheckRetryCount = 0;

                // 检测到新版本
                LogHelper.WriteLogToFile($"AutoUpdate | New version available: {AvailableLatestVersion}");

                // 通过 Windows 系统通知提示有新版本
                WindowsNotificationHelper.ShowNewVersionToast(AvailableLatestVersion);

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
                updateWindow.Owner = this;
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
                                AutoUpdateHelper.InstallNewVersionApp(AvailableLatestVersion, true);  // 修改为静默模式，避免重复启动进程

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
            else if (hasValidLineGroup)
            {
                LogHelper.WriteLogToFile("AutoUpdate | Current version is already the latest, no retry needed");

                // 停止重试定时器
                timerCheckAutoUpdateRetry.Stop();
                updateCheckRetryCount = 0;
            }
            else
            {
                // 检查更新失败，启动重试定时器
                LogHelper.WriteLogToFile("AutoUpdate | Update check failed, starting retry timer");

                // 重置重试计数
                updateCheckRetryCount = 0;

                // 启动重试定时器，10分钟后重新检查
                timerCheckAutoUpdateRetry.Start();

                // 清理更新文件夹
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
                    if (_cachedPenCursor == null)
                    {
                        lock (_cursorLock)
                        {
                            if (_cachedPenCursor == null)
                            {
                                try
                                {
                                    var sri = Application.GetResourceStream(new Uri("Resources/Cursors/Pen.cur", UriKind.Relative));
                                    if (sri != null)
                                    {
                                        _cachedPenCursor = new Cursor(sri.Stream);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogHelper.WriteLogToFile($"加载 Pen 光标资源失败: {ex.Message}", LogHelper.LogType.Error);
                                }
                            }
                        }
                    }
                    if (_cachedPenCursor != null)
                    {
                        canvas.Cursor = _cachedPenCursor;
                    }
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

            // 检查是否点击了空白区域或其他非图片元素
            var hitTest = e.OriginalSource;
            if (!(hitTest is Image) && !(hitTest is MediaElement))
            {
                // 如果当前有选中的元素，取消选中状态
                if (currentSelectedElement != null)
                {
                    // 取消选中元素
                    UnselectElement(currentSelectedElement);
                    currentSelectedElement = null;

                    // 重置为选择模式，确保用户可以继续选择其他元素
                    SetCurrentToolMode(InkCanvasEditingMode.Select);
                    // 更新模式缓存
                    UpdateCurrentToolMode("select");
                    // 刷新浮动栏高光显示
                    SetFloatingBarHighlightPosition("select");
                }
            }

        }

        // 手写笔输入
        private void inkCanvas_StylusDown(object sender, StylusDownEventArgs e)
        {
            // 使用辅助方法设置光标
            SetCursorBasedOnEditingMode(sender as InkCanvas);
        }

        // 手写笔抬起事件（用于橡皮擦自动切换）
        private void inkCanvas_StylusUp(object sender, StylusEventArgs e)
        {
            HandleEraserOperationEnded();
        }

        /// <summary>
        /// 处理橡皮擦操作结束事件
        /// </summary>
        private void HandleEraserOperationEnded()
        {
            try
            {
                // 检查是否在橡皮擦模式且启用了自动切换功能
                if ((inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint ||
                     inkCanvas.EditingMode == InkCanvasEditingMode.EraseByStroke) &&
                    Settings.Canvas.EnableEraserAutoSwitchBack)
                {
                    // 启动或重启计时器
                    StartEraserAutoSwitchBackTimer();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理橡皮擦操作结束事件失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 注册橡皮擦操作监听器（在切换到橡皮擦模式时调用）
        /// </summary>
        private void RegisterEraserOperationListeners()
        {
            // 事件已经在构造函数中注册，这里只需要确保计时器在操作结束时启动
            // 实际的启动逻辑在HandleEraserOperationEnded中处理
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

        // 个性化设置
        private void NavTheme_Click(object sender, RoutedEventArgs e)
        {
            // 切换到个性化设置页面
            ShowSettingsSection("theme");
        }

        // 快捷键设置
        private void NavShortcuts_Click(object sender, RoutedEventArgs e)
        {
            OpenHotkeySettingsWindow();
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

        // 折叠侧边栏
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

        // 显示侧边栏
        private void ShowNavSidebar_Click(object sender, RoutedEventArgs e)
        {
            // 确保侧边栏展开
            var columnDefinitions = ((Grid)BorderSettings.Child).ColumnDefinitions;
            columnDefinitions[0].Width = new GridLength(50);
        }

        // 显示指定的设置部分
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
                    targetGroupBox = GroupBoxStartup;
                    break;
                case "canvas":
                    targetGroupBox = GroupBoxCanvas;
                    break;
                case "gesture":
                    targetGroupBox = GroupBoxGesture;
                    break;
                case "inkrecognition":
                    targetGroupBox = GroupBoxInkRecognition;
                    break;
                case "crashaction":
                    targetGroupBox = GroupBoxCrashAction;
                    break;
                case "ppt":
                    targetGroupBox = GroupBoxPPT;
                    break;
                case "advanced":
                    targetGroupBox = GroupBoxAdvanced;
                    break;
                case "automation":
                    targetGroupBox = GroupBoxAutomation;
                    break;
                case "randomwindow":
                    targetGroupBox = GroupBoxRandWindow;
                    break;
                case "theme":
                    targetGroupBox = GroupBoxAppearanceNewUI;
                    break;
                case "shortcuts":
                    // 快捷键设置部分可能尚未实现
                    targetGroupBox = null;
                    break;
                case "about":
                    targetGroupBox = GroupBoxAbout;
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
                    catch (Exception)
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

        #region 新设置窗口

        /// <summary>
        /// 在隐藏子面板后打开新的设置窗口；若需要则先提示并验证安全密码，并在正在打开或隐藏设置面板时不执行任何操作。
        /// </summary>
        /// <remarks>
        /// 在验证密码失败或发生异常时会中止操作。成功通过验证后以模式窗口方式显示设置窗口并将当前窗口设为其所有者。
        /// </remarks>
        private async void BtnOpenNewSettings_Click(object sender, RoutedEventArgs e)
        {
            if (isOpeningOrHidingSettingsPane) return;
            HideSubPanels();
            {
                try
                {
                    if (SecurityManager.IsPasswordRequiredForEnterSettings(Settings))
                    {
                        bool ok = await SecurityManager.PromptAndVerifyAsync(Settings, this, "进入设置", "请输入安全密码以进入设置。");
                        if (!ok) return;
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"安全密码校验失败: {ex}", LogHelper.LogType.Error);
                    return;
                }

                var settingsWindow = new SettingsWindow();
                settingsWindow.Owner = this;
                settingsWindow.ShowDialog();
            }
        }

        #endregion 新设置窗口

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
        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentProcessId();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private LowLevelKeyboardProc _keyboardProc;
        private IntPtr _keyboardHookId = IntPtr.Zero;

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
        private DispatcherTimer autoSaveStrokesTimer;
        private bool isTopmostMaintenanceEnabled;

        private IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
        }

        private void InstallKeyboardHook()
        {
            if (_keyboardHookId == IntPtr.Zero)
            {
                _keyboardProc = KeyboardHookProc;
                _keyboardHookId = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc,
                    GetModuleHandle(null), 0);
                if (_keyboardHookId == IntPtr.Zero)
                {
                    LogHelper.WriteLogToFile("安装低级键盘钩子失败", LogHelper.LogType.Error);
                }
            }
        }

        private void UninstallKeyboardHook()
        {
            if (_keyboardHookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_keyboardHookId);
                _keyboardHookId = IntPtr.Zero;
                _keyboardProc = null;
            }
        }

        private void ApplyNoFocusMode()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

            bool shouldBeNoFocus = isTemporarilyDisablingNoFocusMode ?
                false : Settings.Advanced.IsNoFocusMode;

            if (shouldBeNoFocus)
            {
                SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE);
                InstallKeyboardHook();
            }
            else
            {
                SetWindowLong(hwnd, GWL_EXSTYLE, exStyle & ~WS_EX_NOACTIVATE);
                UninstallKeyboardHook();
            }
        }

        private void ApplyAlwaysOnTop()
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (Settings.Advanced.IsAlwaysOnTop)
                {
                    Topmost = true;

                    // 1. 设置窗口样式为置顶
                    int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                    SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOPMOST);

                    // 2. 使用SetWindowPos确保窗口在最顶层
                    SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                        SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_NOOWNERZORDER);

                    // 3. 如果启用了无焦点模式且未启用UIA置顶，需要特殊处理
                    if (Settings.Advanced.IsNoFocusMode && !Settings.Advanced.EnableUIAccessTopMost)
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
            if (Settings.Advanced.EnableUIAccessTopMost)
            {
                return;
            }

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

        public void PauseTopmostMaintenance()
        {
            if (topmostMaintenanceTimer != null && isTopmostMaintenanceEnabled)
            {
                topmostMaintenanceTimer.Stop();
            }
        }

        public void ResumeTopmostMaintenance()
        {
            if (Settings.Advanced.IsAlwaysOnTop &&
                Settings.Advanced.IsNoFocusMode &&
                !Settings.Advanced.EnableUIAccessTopMost)
            {
                if (topmostMaintenanceTimer != null && !isTopmostMaintenanceEnabled)
                {
                    topmostMaintenanceTimer.Start();
                    isTopmostMaintenanceEnabled = true;
                }
            }
        }

        /// <summary>
        /// 置顶维护定时器事件
        /// </summary>
        private void TopmostMaintenanceTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (Settings.Advanced.EnableUIAccessTopMost)
                {
                    StopTopmostMaintenance();
                    return;
                }

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

                // 检查是否有子窗口在前景
                var foregroundWindow = GetForegroundWindow();
                if (foregroundWindow != hwnd)
                {
                    // 检查前景窗口是否是当前应用程序的子窗口
                    var foregroundWindowProcessId = GetWindowThreadProcessId(foregroundWindow, out uint processId);
                    var currentProcessId = GetCurrentProcessId();

                    if (processId == currentProcessId)
                    {
                        // 如果有子窗口在前景，暂停置顶维护
                        return;
                    }

                    // 如果窗口不在最顶层且没有子窗口，重新设置置顶
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

            if (isTemporarilyDisablingNoFocusMode)
            {
                isTemporarilyDisablingNoFocusMode = false;
            }

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
            UpdateUIAccessTopMostVisibility();
        }

        private void ToggleSwitchUIAccessTopMost_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            var toggle = sender as ToggleSwitch;
            bool newValue = toggle != null && toggle.IsOn;

            Settings.Advanced.EnableUIAccessTopMost = newValue;
            SaveSettingsToFile();
            ApplyUIAccessTopMost();

            App.IsUIAccessTopMostEnabled = newValue;

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
                }), DispatcherPriority.Loaded);
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
                }), DispatcherPriority.Loaded);
            }
        }



        #region 全局快捷键管理
        /// <summary>
        /// 初始化墨迹渐隐管理器
        /// </summary>
        private void InitializeInkFadeManager()
        {
            try
            {
                // 确保墨迹渐隐管理器已初始化
                if (_inkFadeManager == null)
                {
                    _inkFadeManager = new InkFadeManager(this);
                }

                // 同步设置状态
                _inkFadeManager.IsEnabled = Settings.Canvas.EnableInkFade;
                _inkFadeManager.UpdateFadeTime(Settings.Canvas.InkFadeTime);

                LogHelper.WriteLogToFile("墨迹渐隐管理器已初始化", LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"初始化墨迹渐隐管理器时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 初始化全局快捷键管理器
        /// </summary>
        private void InitializeGlobalHotkeyManager()
        {
            try
            {
                _globalHotkeyManager = new GlobalHotkeyManager(this);
                // 启动时加载快捷键，但默认为鼠标模式，禁用快捷键以放行键盘操作
                _globalHotkeyManager.EnableHotkeyRegistration();
                // 启动时默认为鼠标模式，禁用快捷键
                _globalHotkeyManager.UpdateHotkeyStateForToolMode(true);
                LogHelper.WriteLogToFile("全局快捷键管理器已初始化，启动时默认为鼠标模式并禁用快捷键", LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"初始化全局快捷键管理器时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 打开快捷键设置窗口
        /// </summary>
        private void OpenHotkeySettingsWindow()
        {
            try
            {
                if (_globalHotkeyManager == null)
                {
                    MessageBox.Show("快捷键管理器尚未初始化，请稍后重试。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 暂时隐藏设置面板
                BorderSettings.Visibility = Visibility.Hidden;
                BorderSettingsMask.Visibility = Visibility.Hidden;

                // 创建快捷键设置窗口
                var hotkeySettingsWindow = new HotkeySettingsWindow(this, _globalHotkeyManager);

                // 设置窗口关闭事件，用于在快捷键设置窗口关闭后恢复设置面板
                hotkeySettingsWindow.Closed += (s, e) =>
                {
                    // 恢复设置面板显示
                    BorderSettings.Visibility = Visibility.Visible;
                    BorderSettingsMask.Visibility = Visibility.Visible;
                };

                // 显示快捷键设置窗口
                hotkeySettingsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                // 确保在发生错误时也恢复设置面板显示
                BorderSettings.Visibility = Visibility.Visible;
                BorderSettingsMask.Visibility = Visibility.Visible;

                LogHelper.WriteLogToFile($"打开快捷键设置窗口时出错: {ex.Message}", LogHelper.LogType.Error);
                MessageBox.Show($"打开快捷键设置窗口时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region 展台/白板分辨率切换
        private const int BoothResolutionTabCount = 4;
        private static readonly (int w, int h)[] BoothResolutionValues = { (1280, 720), (1920, 1080), (2560, 1440), (3840, 2160) };

        private void BoothResolutionTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
            {
                var parts = tag.Split(',');
                if (parts.Length == 2 && int.TryParse(parts[0].Trim(), out int w) && int.TryParse(parts[1].Trim(), out int h) && w > 0 && h > 0)
                {
                    _boothResolutionWidth = w;
                    _boothResolutionHeight = h;
                    UpdateBoothResolutionTabState();
                    SyncBoothResolutionToCameraService();
                }
            }
        }

        private void UpdateBoothResolutionTabState()
        {
            int index = 0;
            for (int i = 0; i < BoothResolutionValues.Length; i++)
            {
                if (BoothResolutionValues[i].w == _boothResolutionWidth && BoothResolutionValues[i].h == _boothResolutionHeight)
                {
                    index = i;
                    break;
                }
            }

            if (BoothResolutionTabIndicator != null)
            {
                BoothResolutionTabIndicator.Margin = new Thickness(index * 70, 0, 0, 0);
            }

            var texts = new[] { BtnBoothResolution720?.Content as TextBlock, BtnBoothResolution1080?.Content as TextBlock, BtnBoothResolution2K?.Content as TextBlock, BtnBoothResolution4K?.Content as TextBlock };
            for (int i = 0; i < texts.Length && i < 4; i++)
            {
                if (texts[i] == null) continue;
                if (i == index)
                {
                    texts[i].FontWeight = FontWeights.Bold;
                    texts[i].Foreground = new SolidColorBrush(Colors.White);
                    texts[i].Opacity = 1.0;
                }
                else
                {
                    texts[i].FontWeight = FontWeights.SemiBold;
                    texts[i].SetResourceReference(TextBlock.ForegroundProperty, "FloatBarForeground");
                    texts[i].Opacity = 0.7;
                }
            }
        }
        #endregion

        #region 墨迹渐隐功能
        /// <summary>
        /// 墨迹渐隐开关切换事件处理
        /// </summary>
        private void ToggleSwitchEnableInkFade_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
                Settings.Canvas.EnableInkFade = ToggleSwitchEnableInkFade.IsOn;
                _inkFadeManager.IsEnabled = Settings.Canvas.EnableInkFade;

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

            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"切换墨迹渐隐功能时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 墨迹渐隐时间滑块值改变事件处理
        /// </summary>
        private void InkFadeTimeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                Settings.Canvas.InkFadeTime = (int)e.NewValue;
                if (_inkFadeManager != null)
                {
                    _inkFadeManager.UpdateFadeTime(Settings.Canvas.InkFadeTime);
                }
                LogHelper.WriteLogToFile($"墨迹渐隐时间已更新为 {Settings.Canvas.InkFadeTime}ms", LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更新墨迹渐隐时间时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }



        /// <summary>
        /// 批注子面板中墨迹渐隐开关切换事件处理
        /// </summary>
        private void ToggleSwitchInkFadeInPanel_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
                Settings.Canvas.EnableInkFade = ToggleSwitchInkFadeInPanel.IsOn;
                _inkFadeManager.IsEnabled = Settings.Canvas.EnableInkFade;

                // 同步设置面板中的开关状态
                if (ToggleSwitchEnableInkFade != null)
                {
                    ToggleSwitchEnableInkFade.IsOn = Settings.Canvas.EnableInkFade;
                }

                // 同步普通画笔面板中的开关状态
                if (ToggleSwitchInkFadeInPanel2 != null)
                {
                    ToggleSwitchInkFadeInPanel2.IsOn = Settings.Canvas.EnableInkFade;
                }

            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"批注子面板中切换墨迹渐隐功能时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 在笔工具菜单中隐藏墨迹渐隐控制开关切换事件处理
        /// <summary>
        /// 切换“在笔工具菜单中隐藏墨迹渐隐控制开关”设置并立即应用该更改。
        /// </summary>
        /// <remarks>
        /// 当控件切换时，方法会更新 Settings.Canvas.HideInkFadeControlInPenMenu 的值、将设置写回配置文件、刷新墨迹渐隐控件的可见性，并记录事件日志或错误日志。
        /// </remarks>
        private void ToggleSwitchHideInkFadeControlInPenMenu_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
                if (isLoaded)
                {
                    Settings.Canvas.HideInkFadeControlInPenMenu = ToggleSwitchHideInkFadeControlInPenMenu.IsOn;
                    SaveSettingsToFile();
                }

                // 立即更新墨迹渐隐控制开关的可见性
                UpdateInkFadeControlVisibility();

                LogHelper.WriteLogToFile($"在笔工具菜单中隐藏墨迹渐隐控制开关已{(Settings.Canvas.HideInkFadeControlInPenMenu ? "启用" : "禁用")}", LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"切换在笔工具菜单中隐藏墨迹渐隐控制开关时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 橡皮擦自动切换回批注模式开关切换事件处理
        /// </summary>
        private void ToggleSwitchEnableEraserAutoSwitchBack_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!isLoaded) return;
                Settings.Canvas.EnableEraserAutoSwitchBack = ToggleSwitchEnableEraserAutoSwitchBack.IsOn;
                SaveSettingsToFile();

                // 如果禁用，停止计时器
                if (!Settings.Canvas.EnableEraserAutoSwitchBack)
                {
                    StopEraserAutoSwitchBackTimer();
                }

                LogHelper.WriteLogToFile($"橡皮擦自动切换回批注模式已{(Settings.Canvas.EnableEraserAutoSwitchBack ? "启用" : "禁用")}", LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"切换橡皮擦自动切换回批注模式时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 橡皮擦自动切换延迟时间滑块值改变事件处理
        /// </summary>
        private void EraserAutoSwitchBackDelaySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                if (!isLoaded) return;
                Settings.Canvas.EraserAutoSwitchBackDelaySeconds = (int)e.NewValue;
                SaveSettingsToFile();

                // 如果计时器正在运行，重新启动以应用新的延迟时间
                if (_eraserAutoSwitchBackTimer != null && _eraserAutoSwitchBackTimer.IsEnabled)
                {
                    StartEraserAutoSwitchBackTimer();
                }

                LogHelper.WriteLogToFile($"橡皮擦自动切换延迟时间已更新为 {Settings.Canvas.EraserAutoSwitchBackDelaySeconds} 秒", LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更新橡皮擦自动切换延迟时间时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 根据开关状态启用或禁用画笔自动恢复：更新设置并保存，启用时初始化并安排恢复定时器，禁用时停止计时器。
        /// </summary>
        private void ToggleSwitchBrushAutoRestore_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!isLoaded) return;
                Settings.Canvas.EnableBrushAutoRestore = ToggleSwitchBrushAutoRestore.IsOn;
                SaveSettingsToFile();

                if (Settings.Canvas.EnableBrushAutoRestore)
                {
                    InitBrushAutoRestoreTimer();
                    ScheduleBrushAutoRestore();
                }
                else
                {
                    if (_brushAutoRestoreTimer != null)
                    {
                        _brushAutoRestoreTimer.Stop();
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"切换画笔自动恢复功能时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 当“画笔自动恢复次数”文本改变时更新并保存设置；若启用画笔自动恢复，则重新调度自动恢复定时器。
        /// </summary>
        /// <remarks>
        /// 在窗口未完成加载或 Settings.Canvas 为 null 时不执行任何操作；方法内部会捕获并记录异常，不向调用方抛出异常。
        /// </remarks>
        private void BrushAutoRestoreTimesTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (!isLoaded) return;
                if (Settings?.Canvas == null) return;

                Settings.Canvas.BrushAutoRestoreTimes = BrushAutoRestoreTimesTextBox.Text ?? string.Empty;
                SaveSettingsToFile();
                if (Settings.Canvas.EnableBrushAutoRestore)
                {
                    ScheduleBrushAutoRestore();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"BrushAutoRestoreTimes: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 响应画笔自动恢复颜色下拉框的选择变更并将选中项保存为设置中的目标颜色。
        /// </summary>
        /// <remarks>
        /// 当窗口已加载且 Settings.Canvas 可用时，将选中 ComboBoxItem 的 Tag（十六进制颜色字符串）写入 Settings.Canvas.BrushAutoRestoreColor 并持久化到设置文件；若发生异常则记录错误日志。
        /// </remarks>
        private void ComboBoxBrushAutoRestoreColor_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (!isLoaded) return;
                if (Settings?.Canvas == null) return;

                if (ComboBoxBrushAutoRestoreColor.SelectedItem is ComboBoxItem item)
                {
                    string hex = item.Tag as string ?? string.Empty;
                    Settings.Canvas.BrushAutoRestoreColor = hex;
                    SaveSettingsToFile();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更新画笔自动恢复目标颜色时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 将画笔自动恢复的目标粗细设置为滑块的新值并将更改保存到设置文件。
        /// </summary>
        /// <param name="sender">触发事件的滑块控件（通常为 BrushAutoRestoreWidthSlider）。</param>
        /// <param name="e">包含滑块的新值的事件参数；使用 <c>e.NewValue</c> 作为目标粗细。</param>
        /// <remarks>
        /// 如果窗口尚未完成加载或 Settings.Canvas 为 null，则不执行任何操作。
        /// </remarks>
        private void BrushAutoRestoreWidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                if (!isLoaded) return;
                if (Settings?.Canvas == null) return;

                Settings.Canvas.BrushAutoRestoreWidth = e.NewValue;
                SaveSettingsToFile();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更新画笔自动恢复目标粗细时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 在画笔自动恢复透明度滑块的值发生变化时，将新的透明度值保存到 Settings.Canvas.BrushAutoRestoreAlpha 并持久化到设置文件。
        /// </summary>
        /// <param name="e">来自滑块的事件参数；使用 <c>e.NewValue</c> 的整数值作为新的透明度目标。</param>
        private void BrushAutoRestoreAlphaSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                if (!isLoaded) return;
                if (Settings?.Canvas == null) return;

                Settings.Canvas.BrushAutoRestoreAlpha = (int)e.NewValue;
                SaveSettingsToFile();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更新画笔自动恢复目标透明度时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 更新墨迹渐隐控制开关的可见性
        /// </summary>
        private void UpdateInkFadeControlVisibility()
        {
            try
            {
                bool isHidden = Settings.Canvas.HideInkFadeControlInPenMenu;

                // 控制 InkFadeControlPanel1（批注子面板中）的可见性
                if (InkFadeControlPanel1 != null)
                {
                    InkFadeControlPanel1.Visibility = isHidden ? Visibility.Collapsed : Visibility.Visible;
                }

                // 控制 InkFadeControlPanel2（普通画笔面板中）的可见性
                if (InkFadeControlPanel2 != null)
                {
                    InkFadeControlPanel2.Visibility = isHidden ? Visibility.Collapsed : Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更新墨迹渐隐控制面板可见性时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// PPT放映模式显示手势按钮开关切换事件处理
        /// </summary>
        private void ToggleSwitchShowGestureButtonInSlideShow_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!isLoaded) return;
                var toggle = sender as ToggleSwitch;
                Settings.PowerPointSettings.ShowGestureButtonInSlideShow = toggle != null && toggle.IsOn;
                SaveSettingsToFile();

                // 如果当前在PPT放映模式，需要立即更新手势按钮的显示状态
                if (BtnPPTSlideShowEnd.Visibility == Visibility.Visible)
                {
                    UpdateGestureButtonVisibilityInPPTMode();
                }

                LogHelper.WriteLogToFile($"PPT放映模式显示手势按钮已{(Settings.PowerPointSettings.ShowGestureButtonInSlideShow ? "启用" : "禁用")}", LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"切换PPT放映模式显示手势按钮时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void ToggleSwitchEnablePPTTimeCapsule_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!isLoaded) return;
                var toggle = sender as ToggleSwitch;
                Settings.PowerPointSettings.EnablePPTTimeCapsule = toggle != null && toggle.IsOn;
                SaveSettingsToFile();

                // 如果当前在PPT放映模式，需要立即更新时间胶囊和快捷面板的显示状态
                if (BtnPPTSlideShowEnd.Visibility == Visibility.Visible)
                {
                    UpdatePPTTimeCapsuleVisibility();
                    UpdatePPTQuickPanelVisibility();
                }

                LogHelper.WriteLogToFile($"PPT时间显示胶囊已{(Settings.PowerPointSettings.EnablePPTTimeCapsule ? "启用" : "禁用")}", LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"切换PPT时间显示胶囊时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void ComboBoxPPTTimeCapsulePosition_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (!isLoaded) return;
                if (ComboBoxPPTTimeCapsulePosition != null)
                {
                    Settings.PowerPointSettings.PPTTimeCapsulePosition = ComboBoxPPTTimeCapsulePosition.SelectedIndex;
                    SaveSettingsToFile();

                    // 如果当前在PPT放映模式，需要立即更新时间胶囊的位置
                    if (BtnPPTSlideShowEnd.Visibility == Visibility.Visible)
                    {
                        UpdatePPTTimeCapsulePosition();
                    }

                    LogHelper.WriteLogToFile($"PPT时间胶囊位置已更改为: {ComboBoxPPTTimeCapsulePosition.SelectedIndex}", LogHelper.LogType.Event);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更改PPT时间胶囊位置时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 更新PPT模式下手势按钮的显示状态
        /// </summary>
        private void UpdateGestureButtonVisibilityInPPTMode()
        {
            try
            {
                if (Settings.PowerPointSettings.ShowGestureButtonInSlideShow)
                {
                    // 如果启用了PPT放映模式显示手势按钮，则检查是否在批注模式下显示手势按钮
                    CheckEnableTwoFingerGestureBtnVisibility(true);
                }
                else
                {
                    // 如果禁用了PPT放映模式显示手势按钮，则隐藏手势按钮
                    EnableTwoFingerGestureBorder.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更新PPT模式下手势按钮显示状态时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 更新PPT时间胶囊的显示状态
        /// </summary>
        public void UpdatePPTTimeCapsuleVisibility()
        {
            try
            {
                if (PPTTimeCapsuleContainer == null || PPTTimeCapsule == null) return;

                if (Settings.PowerPointSettings.EnablePPTTimeCapsule &&
                    BtnPPTSlideShowEnd.Visibility == Visibility.Visible)
                {
                    PPTTimeCapsuleContainer.Visibility = Visibility.Visible;
                    UpdatePPTTimeCapsulePosition();
                }
                else
                {
                    PPTTimeCapsuleContainer.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更新PPT时间胶囊显示状态时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 更新PPT快捷面板的显示状态
        /// </summary>
        public void UpdatePPTQuickPanelVisibility()
        {
            try
            {
                if (PPTQuickPanelContainer == null || PPTQuickPanel == null) return;

                // 仅在 PPT 模式下且用户开启“PPT 放映时显示快速面板”时显示
                bool inSlideShow = BtnPPTSlideShowEnd.Visibility == Visibility.Visible;
                bool showQuickPanel = Settings.PowerPointSettings.ShowPPTSidebarByDefault;
                if (inSlideShow && showQuickPanel)
                {
                    PPTQuickPanelContainer.Visibility = Visibility.Visible;
                    PPTQuickPanel?.UpdateVisibility(true);
                }
                else
                {
                    PPTQuickPanelContainer.Visibility = Visibility.Collapsed;
                    PPTQuickPanel?.UpdateVisibility(false);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更新PPT快捷面板显示状态时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 更新PPT时间胶囊的位置
        /// </summary>
        private void UpdatePPTTimeCapsulePosition()
        {
            try
            {
                if (PPTTimeCapsuleContainer == null) return;

                int position = Settings.PowerPointSettings.PPTTimeCapsulePosition;
                // 0-左上角, 1-右上角, 2-顶部居中
                switch (position)
                {
                    case 0: // 左上角
                        PPTTimeCapsuleContainer.HorizontalAlignment = HorizontalAlignment.Left;
                        PPTTimeCapsuleContainer.VerticalAlignment = VerticalAlignment.Top;
                        PPTTimeCapsuleContainer.Margin = new Thickness(20, 20, 0, 0);
                        break;
                    case 1: // 右上角
                        PPTTimeCapsuleContainer.HorizontalAlignment = HorizontalAlignment.Right;
                        PPTTimeCapsuleContainer.VerticalAlignment = VerticalAlignment.Top;
                        PPTTimeCapsuleContainer.Margin = new Thickness(0, 20, 20, 0);
                        break;
                    case 2: // 顶部居中
                        PPTTimeCapsuleContainer.HorizontalAlignment = HorizontalAlignment.Center;
                        PPTTimeCapsuleContainer.VerticalAlignment = VerticalAlignment.Top;
                        PPTTimeCapsuleContainer.Margin = new Thickness(0, 20, 0, 0);
                        break;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更新PPT时间胶囊位置时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        #endregion


        /// <summary>
        /// 初始化文件关联状态显示
        /// </summary>
        private void InitializeFileAssociationStatus()
        {
            try
            {
                bool isRegistered = FileAssociationManager.IsFileAssociationRegistered();
                if (isRegistered)
                {
                    TextBlockFileAssociationStatus.Text = "✓ .icstk文件关联已注册";
                    TextBlockFileAssociationStatus.Foreground = new SolidColorBrush(Colors.LightGreen);
                }
                else
                {
                    TextBlockFileAssociationStatus.Text = "✗ .icstk文件关联未注册";
                    TextBlockFileAssociationStatus.Foreground = new SolidColorBrush(Colors.LightCoral);
                }
            }
            catch (Exception ex)
            {
                TextBlockFileAssociationStatus.Text = "✗ 检查文件关联状态时出错";
                TextBlockFileAssociationStatus.Foreground = new SolidColorBrush(Colors.LightCoral);
                LogHelper.WriteLogToFile($"初始化文件关联状态显示时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 处理命令行参数中的文件路径
        /// </summary>
        private void HandleCommandLineFileOpen()
        {
            try
            {
                // 检查启动参数中是否有.icstk文件
                string icstkFile = FileAssociationManager.GetIcstkFileFromArgs(App.StartArgs);

                if (!string.IsNullOrEmpty(icstkFile))
                {
                    LogHelper.WriteLogToFile($"检测到命令行参数中的.icstk文件: {icstkFile}", LogHelper.LogType.Event);

                    // 延迟执行，确保UI已完全加载
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            // 打开文件
                            OpenSingleStrokeFile(icstkFile);
                            ShowNotification($"已加载墨迹文件: {Path.GetFileName(icstkFile)}");
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"打开命令行参数中的文件失败: {ex.Message}", LogHelper.LogType.Error);
                            ShowNotification("打开墨迹文件失败");
                        }
                    }), DispatcherPriority.Loaded);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理命令行文件打开时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 集中管理工具模式切换和快捷键状态更新
        /// 避免在每个工具按钮点击时重复刷新快捷键状态
        /// </summary>
        /// <param name="newMode">新的编辑模式</param>
        /// <param name="additionalActions">可选的额外操作委托</param>
        internal void SetCurrentToolMode(InkCanvasEditingMode newMode, Action additionalActions = null)
        {
            try
            {
                // 如果切换到非橡皮擦模式，禁用橡皮擦覆盖层并重置橡皮擦状态
                if (newMode != InkCanvasEditingMode.EraseByPoint && newMode != InkCanvasEditingMode.EraseByStroke)
                {
                    DisableEraserOverlay();
                }

                // 执行模式切换
                inkCanvas.EditingMode = newMode;

                // 根据模式确定是否为鼠标模式（无工具模式）
                bool isMouseMode = newMode == InkCanvasEditingMode.None;

                // 更新快捷键状态
                if (_globalHotkeyManager != null)
                {
                    _globalHotkeyManager.UpdateHotkeyStateForToolMode(isMouseMode);
                }

                // 在PPT放映模式下，工具模式切换时需要更新手势按钮的显示状态
                if (BtnPPTSlideShowEnd.Visibility == Visibility.Visible)
                {
                    UpdateGestureButtonVisibilityInPPTMode();
                }

                // 执行额外的操作（如果有）
                additionalActions?.Invoke();

            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"设置工具模式时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        #region 滑块触摸支持

        /// <summary>
        /// 为所有滑块控件添加触摸和手写笔事件支持
        /// <summary>
        /// 为窗口中预定义的一组滑块控件注册触摸交互支持并记录操作结果。
        /// </summary>
        /// <remarks>
        /// 如果在添加触摸支持过程中发生错误，会捕获异常并将错误信息记录到日志中。
        /// </remarks>
        private void AddTouchSupportToSliders()
        {
            try
            {
                // 获取所有滑块控件并添加触摸支持
                var sliders = new List<Slider>
                {
                    InkFadeTimeSlider,
                    AutoStraightenLineThresholdSlider,
                    LineStraightenSensitivitySlider,
                    LineEndpointSnappingThresholdSlider,
                    ViewboxFloatingBarScaleTransformValueSlider,
                    ViewboxFloatingBarOpacityValueSlider,
                    ViewboxFloatingBarOpacityInPPTValueSlider,
                    PPTButtonLeftPositionValueSlider,
                    PPTButtonRightPositionValueSlider,
                    PPTButtonLBPositionValueSlider,
                    PPTButtonRBPositionValueSlider,
                    PPTLSButtonOpacityValueSlider,
                    PPTRSButtonOpacityValueSlider,
                    PPTLBButtonOpacityValueSlider,
                    PPTRBButtonOpacityValueSlider,
                    TouchMultiplierSlider,
                    NibModeBoundsWidthSlider,
                    FingerModeBoundsWidthSlider,
                    SideControlMinimumAutomationSlider,
                    RandWindowOnceCloseLatencySlider,
                    RandWindowOnceMaxStudentsSlider,
                    TimerVolumeSlider,
                    ProgressiveReminderVolumeSlider,
                    BoardInkWidthSlider,
                    BoardInkAlphaSlider,
                    BoardHighlighterWidthSlider,
                    InkWidthSlider,
                    InkAlphaSlider,
                    HighlighterWidthSlider,
                    MLAvoidanceHistorySlider,
                    MLAvoidanceWeightSlider,
                    BrushAutoRestoreWidthSlider,
                    BrushAutoRestoreAlphaSlider
                };

                foreach (var slider in sliders)
                {
                    if (slider != null)
                    {
                        AddTouchSupportToSlider(slider);
                    }
                }

                LogHelper.WriteLogToFile("已为所有滑块控件添加触摸支持", LogHelper.LogType.Trace);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"添加滑块触摸支持时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 为单个滑块控件添加触摸和手写笔事件支持
        /// </summary>
        /// <param name="slider">要添加触摸支持的滑块控件</param>
        private void AddTouchSupportToSlider(Slider slider)
        {
            if (slider == null) return;

            // 启用触摸和手写笔支持
            slider.IsManipulationEnabled = true;

            // 添加触摸事件 - 使用更简单直接的方法
            slider.TouchDown += (s, e) => HandleSliderTouch(s, e, slider);
            slider.TouchMove += (s, e) => HandleSliderTouch(s, e, slider);
            slider.TouchUp += (s, e) => HandleSliderTouchEnd(s, e, slider);

            // 添加手写笔事件
            slider.StylusDown += (s, e) => HandleSliderStylus(s, e, slider);
            slider.StylusMove += (s, e) => HandleSliderStylus(s, e, slider);
            slider.StylusUp += (s, e) => HandleSliderStylusEnd(s, e, slider);
        }

        /// <summary>
        /// 处理滑块触摸事件（按下和移动）
        /// </summary>
        private void HandleSliderTouch(object sender, TouchEventArgs e, Slider slider)
        {
            if (slider == null) return;

            // 捕获触摸设备
            if (e.RoutedEvent == TouchDownEvent)
            {
                slider.CaptureTouch(e.TouchDevice);
            }

            // 计算触摸位置对应的滑块值
            var touchPoint = e.GetTouchPoint(slider);

            // 使用更精确的位置计算方法
            UpdateSliderValueFromPositionImproved(slider, touchPoint.Position);

            e.Handled = true;
        }

        /// <summary>
        /// 处理滑块触摸结束事件
        /// </summary>
        private void HandleSliderTouchEnd(object sender, TouchEventArgs e, Slider slider)
        {
            if (slider == null) return;

            // 释放触摸捕获
            slider.ReleaseTouchCapture(e.TouchDevice);

            e.Handled = true;
        }

        /// <summary>
        /// 处理滑块手写笔事件（按下和移动）
        /// </summary>
        private void HandleSliderStylus(object sender, StylusEventArgs e, Slider slider)
        {
            if (slider == null) return;

            // 捕获手写笔设备
            if (e.RoutedEvent == StylusDownEvent)
            {
                slider.CaptureStylus();
            }

            // 计算手写笔位置对应的滑块值
            var stylusPoint = e.GetStylusPoints(slider);
            if (stylusPoint.Count > 0)
            {
                UpdateSliderValueFromPositionImproved(slider, stylusPoint[0].ToPoint());
            }

            e.Handled = true;
        }

        /// <summary>
        /// 处理滑块手写笔结束事件
        /// </summary>
        private void HandleSliderStylusEnd(object sender, StylusEventArgs e, Slider slider)
        {
            if (slider == null) return;

            // 释放手写笔捕获
            slider.ReleaseStylusCapture();

            e.Handled = true;
        }

        /// <summary>
        /// 根据触摸/手写笔位置更新滑块值（改进版本）
        /// </summary>
        /// <param name="slider">滑块控件</param>
        /// <param name="position">触摸/手写笔位置</param>
        private void UpdateSliderValueFromPositionImproved(Slider slider, Point position)
        {
            if (slider == null) return;

            try
            {
                // 获取滑块的轨道元素
                var track = slider.Template.FindName("PART_Track", slider) as Track;
                if (track == null)
                {
                    // 如果找不到轨道，使用简单方法
                    UpdateSliderValueFromPosition(slider, position);
                    return;
                }

                // 获取轨道的实际边界
                var trackBounds = track.TransformToAncestor(slider).TransformBounds(new Rect(0, 0, track.ActualWidth, track.ActualHeight));

                double relativePosition = 0;

                if (slider.Orientation == System.Windows.Controls.Orientation.Horizontal)
                {
                    // 水平滑块
                    if (trackBounds.Width > 0)
                    {
                        // 计算相对于轨道的相对位置
                        var relativeX = position.X - trackBounds.X;
                        relativePosition = Math.Max(0, Math.Min(1, relativeX / trackBounds.Width));
                    }
                }
                else
                {
                    // 垂直滑块
                    if (trackBounds.Height > 0)
                    {
                        // 计算相对于轨道的相对位置
                        var relativeY = position.Y - trackBounds.Y;
                        relativePosition = Math.Max(0, Math.Min(1, relativeY / trackBounds.Height));
                    }
                }

                // 计算新的滑块值
                var newValue = slider.Minimum + relativePosition * (slider.Maximum - slider.Minimum);

                // 如果启用了吸附到刻度，则调整到最近的刻度
                if (slider.IsSnapToTickEnabled && slider.TickFrequency > 0)
                {
                    var tickCount = (int)((slider.Maximum - slider.Minimum) / slider.TickFrequency);
                    var tickIndex = (int)Math.Round(relativePosition * tickCount);
                    newValue = slider.Minimum + tickIndex * slider.TickFrequency;
                }

                // 更新滑块值
                slider.Value = Math.Max(slider.Minimum, Math.Min(slider.Maximum, newValue));
            }
            catch (Exception ex)
            {
                // 如果改进方法失败，回退到简单方法
                UpdateSliderValueFromPosition(slider, position);
                LogHelper.WriteLogToFile($"更新滑块值时出错，使用回退方法: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 根据触摸/手写笔位置更新滑块值（简单版本）
        /// </summary>
        /// <param name="slider">滑块控件</param>
        /// <param name="position">触摸/手写笔位置</param>
        private void UpdateSliderValueFromPosition(Slider slider, Point position)
        {
            if (slider == null) return;

            try
            {
                // 使用更简单直接的方法计算滑块值
                double relativePosition = 0;

                if (slider.Orientation == System.Windows.Controls.Orientation.Horizontal)
                {
                    // 水平滑块 - 使用滑块的实际宽度
                    var sliderWidth = slider.ActualWidth;
                    if (sliderWidth > 0)
                    {
                        // 考虑滑块的边距和拇指大小
                        var thumbSize = 20; // 假设拇指大小约为20像素
                        var effectiveWidth = sliderWidth - thumbSize;
                        var adjustedX = position.X - thumbSize / 2;
                        relativePosition = Math.Max(0, Math.Min(1, adjustedX / effectiveWidth));
                    }
                }
                else
                {
                    // 垂直滑块 - 使用滑块的实际高度
                    var sliderHeight = slider.ActualHeight;
                    if (sliderHeight > 0)
                    {
                        // 考虑滑块的边距和拇指大小
                        var thumbSize = 20; // 假设拇指大小约为20像素
                        var effectiveHeight = sliderHeight - thumbSize;
                        var adjustedY = position.Y - thumbSize / 2;
                        relativePosition = Math.Max(0, Math.Min(1, adjustedY / effectiveHeight));
                    }
                }

                // 计算新的滑块值
                var newValue = slider.Minimum + relativePosition * (slider.Maximum - slider.Minimum);

                // 如果启用了吸附到刻度，则调整到最近的刻度
                if (slider.IsSnapToTickEnabled && slider.TickFrequency > 0)
                {
                    var tickCount = (int)((slider.Maximum - slider.Minimum) / slider.TickFrequency);
                    var tickIndex = (int)Math.Round(relativePosition * tickCount);
                    newValue = slider.Minimum + tickIndex * slider.TickFrequency;
                }

                // 更新滑块值
                slider.Value = Math.Max(slider.Minimum, Math.Min(slider.Maximum, newValue));
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更新滑块值时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        #endregion

        #region 模式切换相关

        /// <summary>
        /// 模式切换开关事件处理
        /// </summary>
        private void ToggleSwitchMode_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
                var toggle = sender as ToggleSwitch;
                if (toggle != null)
                {
                    Settings.ModeSettings.IsPPTOnlyMode = toggle.IsOn;

                    // 保存设置到文件
                    SaveSettingsToFile();

                    // 如果切换到仅PPT模式，立即隐藏主窗口
                    if (Settings.ModeSettings.IsPPTOnlyMode)
                    {
                        Hide();
                        LogHelper.WriteLogToFile("已切换到仅PPT模式，主窗口已隐藏", LogHelper.LogType.Event);
                    }
                    else
                    {
                        // 如果切换到正常模式，显示主窗口
                        Show();
                        LogHelper.WriteLogToFile("已切换到正常模式，主窗口已显示", LogHelper.LogType.Event);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"切换模式时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 检查是否应该显示主窗口（基于PPT模式和PPT放映状态）
        /// </summary>
        private void CheckMainWindowVisibility()
        {
            try
            {
                if (Settings.ModeSettings.IsPPTOnlyMode)
                {
                    // 仅PPT模式下，只有在PPT放映时才显示
                    bool isInSlideShow = BtnPPTSlideShowEnd.Visibility == Visibility.Visible;
                    if (isInSlideShow && !IsVisible)
                    {
                        Show();
                        LogHelper.WriteLogToFile("PPT放映开始，显示主窗口（仅PPT模式）", LogHelper.LogType.Trace);
                    }
                    else if (!isInSlideShow && IsVisible)
                    {
                        Hide();
                    }
                }
                else
                {
                    // 正常模式下，确保主窗口可见
                    if (!IsVisible)
                    {
                        Show();
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"检查主窗口可见性时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 切换到白板模式（用于--board参数和IPC命令）
        /// 调用浮动栏上的白板功能
        /// </summary>
        public void SwitchToBoardMode()
        {
            try
            {
                LogHelper.WriteLogToFile("开始切换到白板模式", LogHelper.LogType.Event);

                // 调用浮动栏上的白板功能
                ImageBlackboard_MouseUp(null, null);

                LogHelper.WriteLogToFile("已成功切换到白板模式", LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"切换到白板模式时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        #endregion

        #region Theme Toggle

        /// <summary>
        /// 主题下拉框选择变化事件
        /// </summary>
        private void ComboBoxTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isLoaded) return;

            try
            {
                System.Windows.Controls.ComboBox comboBox = sender as System.Windows.Controls.ComboBox;
                if (comboBox != null)
                {
                    Settings.Appearance.Theme = comboBox.SelectedIndex;

                    // 应用新主题
                    ApplyTheme(comboBox.SelectedIndex);

                    // 保存设置
                    SaveSettingsToFile();

                    // 显示通知
                    string themeName;
                    switch (comboBox.SelectedIndex)
                    {
                        case 0:
                            themeName = "浅色主题";
                            break;
                        case 1:
                            themeName = "深色主题";
                            break;
                        case 2:
                            themeName = "跟随系统";
                            break;
                        default:
                            themeName = "未知主题";
                            break;
                    }

                    ShowNotification($"已切换到{themeName}");
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"切换主题时出错: {ex.Message}", LogHelper.LogType.Error);
                ShowNotification("主题切换失败");
            }
        }


        private void ComboBoxLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (!isLoaded) return;
                if (_isApplyingLanguageFromSettings) return;
                if (_isReloadingForLanguageChange) return;
                if (Settings?.Appearance == null) return;
                if (ComboBoxLanguage == null) return;

                var index = ComboBoxLanguage.SelectedIndex;
                string language;

                switch (index)
                {
                    case 1:
                        language = "zh-CN";
                        break;
                    case 2:
                        language = "en-US";
                        break;
                    case 0:
                    default:
                        language = string.Empty;
                        break;
                }

                Settings.Appearance.Language = language;
                SaveSettingsToFile();

                LocalizationHelper.TrySetCulture(language);

                _isReloadingForLanguageChange = true;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        var newWindow = new MainWindow
                        {
                            WindowState = WindowState,
                            Left = Left,
                            Top = Top
                        };
                        newWindow.Show();
                        Close();
                    }
                    catch (Exception ex2)
                    {
                        LogHelper.WriteLogToFile($"重建主窗口以应用语言时出错: {ex2.Message}", LogHelper.LogType.Error);
                        ShowNotification("已更新界面语言设置，重启应用后可完全生效。");
                        _isReloadingForLanguageChange = false;
                    }
                }), DispatcherPriority.ApplicationIdle);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"切换界面语言时出错: {ex.Message}", LogHelper.LogType.Error);
                ShowNotification("切换界面语言失败。");
                _isReloadingForLanguageChange = false;
            }
        }

        /// <summary>
        /// 应用指定主题
        /// </summary>
        /// <param name="themeIndex">主题索引：0-浅色，1-深色，2-跟随系统</param>
        private void ApplyTheme(int themeIndex)
        {
            try
            {
                switch (themeIndex)
                {
                    case 0: // 浅色主题
                        SetTheme("Light", true);
                        // 浅色主题下设置浮动栏为完全不透明
                        ViewboxFloatingBar.Opacity = 1.0;
                        break;
                    case 1: // 深色主题
                        SetTheme("Dark", true);
                        // 深色主题下设置浮动栏为完全不透明
                        ViewboxFloatingBar.Opacity = 1.0;
                        break;
                    case 2: // 跟随系统
                        if (IsSystemThemeLight())
                        {
                            SetTheme("Light", true);
                            ViewboxFloatingBar.Opacity = 1.0;
                        }
                        else
                        {
                            SetTheme("Dark", true);
                            ViewboxFloatingBar.Opacity = 1.0;
                        }
                        break;
                }

                // 强制刷新通知框的颜色资源
                RefreshNotificationColors();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用主题时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 刷新通知框的颜色资源
        /// </summary>
        private void RefreshNotificationColors()
        {
            try
            {
                // 强制刷新通知框的背景和前景色
                var border = GridNotifications.Children.OfType<Border>().FirstOrDefault();
                if (border != null)
                {
                    border.Background = (Brush)Application.Current.FindResource("SettingsPageBackground");
                    border.BorderBrush = new SolidColorBrush(Color.FromRgb(185, 28, 28)); // 保持红色边框
                }

                TextBlockNotice.Foreground = (Brush)Application.Current.FindResource("SettingsPageForeground");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"刷新通知框颜色时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        #endregion

        #region UIA置顶功能

        /// <summary>
        /// 更新UIA置顶开关的可见性
        /// </summary>
        private void UpdateUIAccessTopMostVisibility()
        {
            try
            {
                var visibility = Settings.Advanced.IsAlwaysOnTop ? Visibility.Visible : Visibility.Collapsed;

                if (UIAccessTopMostPanel != null)
                {
                    UIAccessTopMostPanel.Visibility = visibility;
                }

                if (UIAccessTopMostDescription != null)
                {
                    UIAccessTopMostDescription.Visibility = visibility;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更新UIA置顶开关可见性时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 应用UIA置顶功能
        /// </summary>
        private void ApplyUIAccessTopMost()
        {
            try
            {
                if (Settings.Advanced.EnableUIAccessTopMost && Settings.Advanced.IsAlwaysOnTop)
                {
                    // 检查是否以管理员权限运行
                    var identity = WindowsIdentity.GetCurrent();
                    var principal = new WindowsPrincipal(identity);

                    if (principal.IsInRole(WindowsBuiltInRole.Administrator))
                    {
                        try
                        {
                            timerKillProcess.Stop();
                            if (App.watchdogProcess != null && !App.watchdogProcess.HasExited)
                            {
                                App.watchdogProcess.Kill();
                                App.watchdogProcess = null;
                            }


                            // 调用UIAccess DLL
                            if (Environment.Is64BitProcess)
                            {
                                PrepareUIAccessX64();
                            }
                            else
                            {
                                PrepareUIAccessX86();
                            }

                            App.StartWatchdogIfNeeded();
                            timerKillProcess.Start();
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"启用UIA置顶功能时出错: {ex.Message}", LogHelper.LogType.Error);
                        }
                    }
                    else
                    {
                        LogHelper.WriteLogToFile("UIA置顶功能需要管理员权限", LogHelper.LogType.Warning);
                    }
                }
                else
                {
                    LogHelper.WriteLogToFile("UIA置顶功能已禁用", LogHelper.LogType.Trace);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用UIA置顶功能时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 显示快抽悬浮按钮
        /// </summary>
        private void ShowQuickDrawFloatingButton()
        {
            try
            {
                var quickDrawButton = FindName("QuickDrawFloatingButton") as Controls.QuickDrawFloatingButtonControl;
                if (quickDrawButton == null) return;

                // 检查设置是否启用快抽功能
                if (Settings?.RandSettings?.EnableQuickDraw == true)
                {
                    quickDrawButton.Visibility = Visibility.Visible;
                }
                else
                {
                    quickDrawButton.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"显示快抽悬浮按钮失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }


        #endregion
    }
}
