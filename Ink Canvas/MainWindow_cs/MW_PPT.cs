using Ink_Canvas.Helpers;
using iNKORE.UI.WPF.Modern;
using Microsoft.Office.Core;
using Microsoft.Office.Interop.PowerPoint;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Threading;
using Application = System.Windows.Application;
using File = System.IO.File;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace Ink_Canvas
{
    public partial class MainWindow : Window
    {
        #region Win32 API Declarations
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsZoomed(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out ForegroundWindowInfo.RECT lpRect);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        private const int GWL_STYLE = -16;
        private const int WS_VISIBLE = 0x10000000;
        private const int WS_MINIMIZE = 0x20000000;
        private const uint GW_HWNDNEXT = 2;
        private const uint GW_HWNDPREV = 3;

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        #endregion

        #region PPT Application Variables
        public static Microsoft.Office.Interop.PowerPoint.Application pptApplication;
        public static Presentation presentation;
        public static Slides slides;
        public static Slide slide;
        public static int slidescount;
        #endregion

        #region PPT State Management
        private bool wasFloatingBarFoldedWhenEnterSlideShow;
        private bool isEnteredSlideShowEndEvent; 
        private bool isPresentationHaveBlackSpace;

        // 长按翻页相关字段
        private DispatcherTimer _longPressTimer;
        private bool _isLongPressNext = true; // true为下一页，false为上一页
        private const int LongPressDelay = 500; // 长按延迟时间（毫秒）
        private const int LongPressInterval = 50; // 长按翻页间隔（毫秒）

        // PowerPoint应用程序守护相关字段
        private DispatcherTimer _powerPointProcessMonitorTimer;
        private const int ProcessMonitorInterval = 1000; // 应用程序监控间隔（毫秒）

        // 上次播放位置相关字段
        private int _lastPlaybackPage = 0;
        private bool _shouldNavigateToLastPage = false;

        // 页面切换防抖机制
        private DateTime _lastSlideSwitchTime = DateTime.MinValue;
        private int _pendingSlideIndex = -1;
        private System.Timers.Timer _slideSwitchDebounceTimer;
        private const int SlideSwitchDebounceMs = 150; // 防抖延迟150毫秒
        #endregion

        #region PPT Managers
        private PPTManager _pptManager;
        private MultiPPTInkManager _multiPPTInkManager;
        private PPTInkManager _singlePPTInkManager;
        private PPTUIManager _pptUIManager;

        /// <summary>
        /// 获取PPT管理器实例
        /// </summary>
        public PPTManager PPTManager => _pptManager;
        #endregion

        #region PPT Manager Initialization
        private void InitializePPTManagers()
        {
            try
            {
                // 初始化长按定时器
                InitializeLongPressTimer();

                // 初始化PPT管理器
                _pptManager = new PPTManager();
                _pptManager.IsSupportWPS = Settings.PowerPointSettings.IsSupportWPS;

                // 注册事件
                _pptManager.PPTConnectionChanged += OnPPTConnectionChanged;
                _pptManager.SlideShowBegin += OnPPTSlideShowBegin;
                _pptManager.SlideShowNextSlide += OnPPTSlideShowNextSlide;
                _pptManager.SlideShowEnd += OnPPTSlideShowEnd;
                _pptManager.PresentationOpen += OnPPTPresentationOpen;
                _pptManager.PresentationClose += OnPPTPresentationClose;
                _pptManager.SlideShowStateChanged += OnPPTSlideShowStateChanged;

                if (Settings.PowerPointSettings.IsSupportWPS)
                {
                    _singlePPTInkManager = new PPTInkManager();
                    _singlePPTInkManager.IsAutoSaveEnabled = Settings.PowerPointSettings.IsAutoSaveStrokesInPowerPoint;
                    _singlePPTInkManager.AutoSaveLocation = Settings.Automation.AutoSavedStrokesLocation;
                }
                else
                {
                    _multiPPTInkManager = new MultiPPTInkManager();
                    _multiPPTInkManager.IsAutoSaveEnabled = Settings.PowerPointSettings.IsAutoSaveStrokesInPowerPoint;
                    _multiPPTInkManager.AutoSaveLocation = Settings.Automation.AutoSavedStrokesLocation;
                    _multiPPTInkManager.PPTManager = _pptManager;
                }

                // 初始化UI管理器
                _pptUIManager = new PPTUIManager(this);
                _pptUIManager.ShowPPTButton = Settings.PowerPointSettings.ShowPPTButton;
                _pptUIManager.PPTButtonsDisplayOption = Settings.PowerPointSettings.PPTButtonsDisplayOption;
                _pptUIManager.PPTSButtonsOption = Settings.PowerPointSettings.PPTSButtonsOption;
                _pptUIManager.PPTBButtonsOption = Settings.PowerPointSettings.PPTBButtonsOption;
                _pptUIManager.PPTLSButtonPosition = Settings.PowerPointSettings.PPTLSButtonPosition;
                _pptUIManager.PPTRSButtonPosition = Settings.PowerPointSettings.PPTRSButtonPosition;
                _pptUIManager.PPTLBButtonPosition = Settings.PowerPointSettings.PPTLBButtonPosition;
                _pptUIManager.PPTRBButtonPosition = Settings.PowerPointSettings.PPTRBButtonPosition;
                _pptUIManager.EnablePPTButtonPageClickable = Settings.PowerPointSettings.EnablePPTButtonPageClickable;
                _pptUIManager.EnablePPTButtonLongPressPageTurn = Settings.PowerPointSettings.EnablePPTButtonLongPressPageTurn;

                LogHelper.WriteLogToFile("PPT管理器初始化完成", LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"PPT管理器初始化失败: {ex}", LogHelper.LogType.Error);
            }
        }

        private void StartPPTMonitoring()
        {
            if (Settings.PowerPointSettings.PowerPointSupport)
            {
                _pptManager?.StartMonitoring();
                LogHelper.WriteLogToFile("PPT监控已启动", LogHelper.LogType.Event);
            }
        }

        private void StopPPTMonitoring()
        {
            _pptManager?.StopMonitoring();
            LogHelper.WriteLogToFile("PPT监控已停止", LogHelper.LogType.Event);
        }

        #region PowerPoint Application Management
        /// <summary>
        /// 启动PowerPoint应用程序守护
        /// </summary>
        private void StartPowerPointProcessMonitoring()
        {
            try
            {
                if (!Settings.PowerPointSettings.EnablePowerPointEnhancement) return;

                // 创建PowerPoint应用程序实例
                CreatePowerPointApplication();

                // 启动应用程序监控定时器
                if (_powerPointProcessMonitorTimer == null)
                {
                    _powerPointProcessMonitorTimer = new DispatcherTimer();
                    _powerPointProcessMonitorTimer.Interval = TimeSpan.FromMilliseconds(ProcessMonitorInterval);
                    _powerPointProcessMonitorTimer.Tick += OnPowerPointApplicationMonitorTick;
                }
                _powerPointProcessMonitorTimer.Start();

                LogHelper.WriteLogToFile("PowerPoint应用程序守护已启动", LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"启动PowerPoint应用程序守护失败: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 停止PowerPoint应用程序守护
        /// </summary>
        private void StopPowerPointProcessMonitoring()
        {
            try
            {
                // 停止应用程序监控定时器
                _powerPointProcessMonitorTimer?.Stop();

                // 关闭PowerPoint应用程序
                ClosePowerPointApplication();

                LogHelper.WriteLogToFile("PowerPoint应用程序守护已停止", LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"停止PowerPoint应用程序守护失败: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 创建PowerPoint应用程序实例
        /// </summary>
        private void CreatePowerPointApplication()
        {
            try
            {
                // 如果应用程序已存在且有效，则不重复创建
                if (pptApplication != null && IsPowerPointApplicationValid())
                {
                    return;
                }

                // 创建新的PowerPoint应用程序实例
                pptApplication = new Microsoft.Office.Interop.PowerPoint.Application();

                // 设置为不可见，作为后台进程
                pptApplication.Visible = MsoTriState.msoFalse;

                // 设置应用程序属性
                pptApplication.WindowState = PpWindowState.ppWindowMinimized;

                // 直接设置PPTManager的PPTApplication属性，绕过COM注册问题
                Task.Delay(1000).ContinueWith(_ =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            // 直接设置PPTManager的PowerPoint应用程序实例
                            if (_pptManager != null)
                            {
                                // 使用反射或直接访问来设置PPTManager的PPTApplication
                                SetPPTManagerApplication(pptApplication);
                                LogHelper.WriteLogToFile("已直接设置PPTManager的PowerPoint应用程序实例", LogHelper.LogType.Event);
                            }
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"设置PPTManager的PowerPoint应用程序实例失败: {ex}", LogHelper.LogType.Error);
                        }
                    });
                });

                LogHelper.WriteLogToFile("PowerPoint应用程序实例已创建", LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"创建PowerPoint应用程序实例失败: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 设置PPTManager的PowerPoint应用程序实例
        /// </summary>
        private void SetPPTManagerApplication(Microsoft.Office.Interop.PowerPoint.Application app)
        {
            try
            {
                if (_pptManager == null) return;

                // 使用反射调用PPTManager的ConnectToPPT方法
                var pptManagerType = _pptManager.GetType();
                var connectMethod = pptManagerType.GetMethod("ConnectToPPT",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (connectMethod != null)
                {
                    connectMethod.Invoke(_pptManager, new object[] { app });
                    LogHelper.WriteLogToFile("通过ConnectToPPT方法设置PowerPoint应用程序实例", LogHelper.LogType.Event);
                }
                else
                {
                    // 如果无法通过反射调用，尝试直接设置属性
                    var pptApplicationProperty = pptManagerType.GetProperty("PPTApplication",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                    if (pptApplicationProperty != null && pptApplicationProperty.CanWrite)
                    {
                        pptApplicationProperty.SetValue(_pptManager, app);
                        LogHelper.WriteLogToFile("通过属性设置PPTManager的PowerPoint应用程序实例", LogHelper.LogType.Event);
                    }
                    else
                    {
                        LogHelper.WriteLogToFile("无法设置PPTManager的PowerPoint应用程序实例", LogHelper.LogType.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"设置PPTManager的PowerPoint应用程序实例失败: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 检查PowerPoint应用程序是否有效
        /// </summary>
        private bool IsPowerPointApplicationValid()
        {
            try
            {
                if (pptApplication == null) return false;
                if (!Marshal.IsComObject(pptApplication)) return false;

                // 尝试访问一个简单的属性来验证连接是否有效
                var _ = pptApplication.Name;
                return true;
            }
            catch (COMException comEx)
            {
                var hr = (uint)comEx.HResult;
                // 如果COM对象已失效，返回false
                if (hr == 0x8001010E || hr == 0x80004005 || hr == 0x800706B5)
                {
                    return false;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 关闭PowerPoint应用程序
        /// </summary>
        private void ClosePowerPointApplication()
        {
            try
            {
                if (pptApplication != null)
                {
                    // 关闭所有打开的演示文稿
                    if (pptApplication.Presentations.Count > 0)
                    {
                        for (int i = pptApplication.Presentations.Count; i >= 1; i--)
                        {
                            try
                            {
                                pptApplication.Presentations[i].Close();
                            }
                            catch { }
                        }
                    }

                    // 退出PowerPoint应用程序
                    pptApplication.Quit();

                    // 释放COM对象
                    Marshal.ReleaseComObject(pptApplication);
                    pptApplication = null;
                }

                LogHelper.WriteLogToFile("PowerPoint应用程序已关闭", LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"关闭PowerPoint应用程序失败: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// PowerPoint应用程序监控定时器事件
        /// </summary>
        private void OnPowerPointApplicationMonitorTick(object sender, EventArgs e)
        {
            try
            {
                if (!Settings.PowerPointSettings.EnablePowerPointEnhancement)
                {
                    StopPowerPointProcessMonitoring();
                    return;
                }

                // 检查应用程序是否还在运行
                if (!IsPowerPointApplicationValid())
                {
                    LogHelper.WriteLogToFile("检测到PowerPoint应用程序已失效，重新创建", LogHelper.LogType.Event);
                    CreatePowerPointApplication();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"PowerPoint应用程序监控异常: {ex}", LogHelper.LogType.Error);
            }
        }
        #endregion

        private void DisposePPTManagers()
        {
            try
            {
                _pptManager?.Dispose();
                _multiPPTInkManager?.Dispose();
                _singlePPTInkManager?.Dispose();
                _longPressTimer?.Stop();
                _longPressTimer = null;
                _pptManager = null;
                _multiPPTInkManager = null;
                _singlePPTInkManager = null;
                _pptUIManager = null;

                // 清理PowerPoint进程守护
                StopPowerPointProcessMonitoring();
                _powerPointProcessMonitorTimer = null;

                LogHelper.WriteLogToFile("PPT管理器已释放", LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"释放PPT管理器失败: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 初始化长按定时器
        /// </summary>
        private void InitializeLongPressTimer()
        {
            _longPressTimer = new DispatcherTimer();
            _longPressTimer.Interval = TimeSpan.FromMilliseconds(LongPressDelay);
            _longPressTimer.Tick += OnLongPressTimerTick;
        }

        /// <summary>
        /// 启动长按检测
        /// </summary>
        /// <param name="sender">触发事件的控件</param>
        /// <param name="isNext">是否为下一页按钮</param>
        private void StartLongPressDetection(object sender, bool isNext)
        {
            if (!Settings.PowerPointSettings.EnablePPTButtonLongPressPageTurn) return;

            _isLongPressNext = isNext;
            // 重置定时器间隔为初始延迟时间，确保每次长按检测都从正确的延迟开始
            _longPressTimer.Interval = TimeSpan.FromMilliseconds(LongPressDelay);
            _longPressTimer?.Start();
        }

        /// <summary>
        /// 停止长按检测
        /// </summary>
        private void StopLongPressDetection()
        {
            _longPressTimer?.Stop();
        }

        /// <summary>
        /// 长按定时器事件处理
        /// </summary>
        private void OnLongPressTimerTick(object sender, EventArgs e)
        {
            if (!Settings.PowerPointSettings.EnablePPTButtonLongPressPageTurn) return;

            _longPressTimer.Interval = TimeSpan.FromMilliseconds(LongPressInterval);

            // 执行翻页
            if (_isLongPressNext)
            {
                BtnPPTSlidesDown_Click(BtnPPTSlidesDown, null);
            }
            else
            {
                BtnPPTSlidesUp_Click(BtnPPTSlidesUp, null);
            }
        }
        #endregion

        #region New PPT Event Handlers
        private void OnPPTConnectionChanged(bool isConnected)
        {
            try
            {
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _pptUIManager?.UpdateConnectionStatus(isConnected);

                    if (isConnected)
                    {
                        LogHelper.WriteLogToFile("PPT连接已建立", LogHelper.LogType.Event);
                    }
                    else
                    {
                        LogHelper.WriteLogToFile("PPT连接已断开", LogHelper.LogType.Event);
                        if (Settings.PowerPointSettings.IsSupportWPS)
                        {
                            _singlePPTInkManager?.ClearAllStrokes();
                        }
                        else
                        {
                            _multiPPTInkManager?.ClearAllStrokes();
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理PPT连接状态变化失败: {ex}", LogHelper.LogType.Error);
            }
        }

        private void OnPPTPresentationOpen(Presentation pres)
        {
            try
            {
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    // 在初始化墨迹管理器之前，先清理画布上的所有墨迹
                    ClearStrokes(true);

                    // 清理备份历史记录，防止旧演示文稿的墨迹影响新演示文稿
                    if (TimeMachineHistories != null && TimeMachineHistories.Length > 0)
                    {
                        TimeMachineHistories[0] = null;
                    }

                    if (Settings.PowerPointSettings.IsSupportWPS)
                    {
                        _singlePPTInkManager?.InitializePresentation(pres);
                    }
                    else
                    {
                        _multiPPTInkManager?.InitializePresentation(pres);
                    }

                    // 处理跳转到首页或上次播放页的逻辑
                    HandlePresentationOpenNavigation(pres);

                    // 检查隐藏幻灯片
                    if (Settings.PowerPointSettings.IsNotifyHiddenPage)
                    {
                        CheckAndNotifyHiddenSlides(pres);
                    }

                    // 检查自动播放设置
                    if (Settings.PowerPointSettings.IsNotifyAutoPlayPresentation)
                    {
                        CheckAndNotifyAutoPlaySettings(pres);
                    }

                    _pptUIManager?.UpdateConnectionStatus(true);

                    LogHelper.WriteLogToFile($"已打开新演示文稿: {pres.Name}，墨迹状态已清理", LogHelper.LogType.Event);
                });
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理演示文稿打开事件失败: {ex}", LogHelper.LogType.Error);
            }
        }

        private void OnPPTPresentationClose(Presentation pres)
        {
            try
            {
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (Settings.PowerPointSettings.IsSupportWPS)
                    {
                        _singlePPTInkManager?.SaveAllStrokesToFile(pres);
                    }
                    else
                    {
                        _multiPPTInkManager?.SaveAllStrokesToFile(pres);
                        _multiPPTInkManager?.RemovePresentation(pres);
                    }

                    _pptUIManager?.UpdateConnectionStatus(false);
                });
            }
            catch (COMException comEx)
            {
                // COM对象已失效，这是正常情况，完全静默处理
                var hr = (uint)comEx.HResult;
                if (hr == 0x8001010E || hr == 0x80004005 || hr == 0x800706BA || hr == 0x800706BE || hr == 0x80048010)
                {
                }
            }
            catch (Exception)
            {
            }
        }

        private void OnPPTSlideShowStateChanged(bool isInSlideShow)
        {
            try
            {
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    // 通知UI管理器放映状态变化
                    _pptUIManager?.OnSlideShowStateChanged(isInSlideShow);

                    if (!isInSlideShow)
                    {
                    }

                    // 检查主窗口可见性（用于仅PPT模式）
                    CheckMainWindowVisibility();
                });
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理PPT放映状态变化失败: {ex}", LogHelper.LogType.Error);
            }
        }

        private void OnPPTSlideShowBegin(SlideShowWindow wn)
        {
            try
            {
                // 始终记录进入放映时浮动栏收纳状态，用于退出时恢复
                wasFloatingBarFoldedWhenEnterSlideShow = isFloatingBarFolded;

                if (Settings.Automation.IsAutoFoldInPPTSlideShow)
                {
                    if (!isFloatingBarFolded)
                        FoldFloatingBar_MouseUp(new object(), null);
                }
                else
                {
                    if (isFloatingBarFolded)
                    {
                        UnFoldFloatingBar(new object());
                    }
                }

                isStopInkReplay = true;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var activePresentation = _pptManager?.GetCurrentActivePresentation();
                    if (activePresentation != null)
                    {
                        if (Settings.PowerPointSettings.IsSupportWPS)
                        {
                        }
                        else
                        {
                            _multiPPTInkManager?.SwitchToPresentation(activePresentation);
                        }
                    }

                    // 处理跳转到首页或上次播放位置
                    if (Settings.PowerPointSettings.IsAlwaysGoToFirstPageOnReenter)
                    {
                        _pptManager?.TryNavigateToSlide(1);
                    }
                    else if (_shouldNavigateToLastPage && _lastPlaybackPage > 0)
                    {
                        _pptManager?.TryNavigateToSlide(_lastPlaybackPage);
                        _shouldNavigateToLastPage = false; // 重置标志位
                    }

                    // 更新UI状态
                    var currentSlide = _pptManager?.GetCurrentSlideNumber() ?? 0;
                    var totalSlides = _pptManager?.SlidesCount ?? 0;
                    _pptUIManager?.UpdateSlideShowStatus(true, currentSlide, totalSlides);

                    // 设置浮动栏透明度和边距
                    _pptUIManager?.SetFloatingBarOpacity(Settings.Appearance.ViewboxFloatingBarOpacityInPPTValue);
                    _pptUIManager?.SetMainPanelMargin(new Thickness(10, 10, 10, 10));

                    // 显示侧边栏退出按钮
                    _pptUIManager?.UpdateSidebarExitButtons(true);

                    // 处理画板显示
                    if (Settings.PowerPointSettings.IsShowCanvasAtNewSlideShow &&
                        !Settings.Automation.IsAutoFoldInPPTSlideShow &&
                        GridTransparencyFakeBackground.Background == Brushes.Transparent && !isFloatingBarFolded)
                    {
                        BtnHideInkCanvas_Click(BtnHideInkCanvas, null);
                    }

                    if (currentMode != 0)
                    {
                        ImageBlackboard_MouseUp(null, null);
                        BtnHideInkCanvas_Click(BtnHideInkCanvas, null);
                    }

                    BorderFloatingBarMainControls.Visibility = Visibility.Visible;

                    // 在PPT模式下根据设置决定是否隐藏手势面板和手势按钮
                    AnimationsHelper.HideWithSlideAndFade(TwoFingerGestureBorder);
                    AnimationsHelper.HideWithSlideAndFade(BoardTwoFingerGestureBorder);

                    // 根据设置决定是否在PPT放映模式下显示手势按钮
                    if (Settings.PowerPointSettings.ShowGestureButtonInSlideShow)
                    {
                        // 如果启用了PPT放映模式显示手势按钮，则显示手势按钮
                        if (Settings.Gesture.IsEnableTwoFingerGesture)
                        {
                            CheckEnableTwoFingerGestureBtnVisibility(true);
                        }
                    }
                    else
                    {
                        // 如果禁用了PPT放映模式显示手势按钮，则隐藏手势按钮
                        EnableTwoFingerGestureBorder.Visibility = Visibility.Collapsed;
                    }

                    if (Settings.PowerPointSettings.IsShowCanvasAtNewSlideShow &&
                        !Settings.Automation.IsAutoFoldInPPTSlideShow)
                    {
                        // 先进入批注模式，这会显示调色盘
                        PenIcon_Click(null, null);
                        // 然后设置颜色
                        BtnColorRed_Click(null, null);
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                if (inkCanvas.EditingMode == InkCanvasEditingMode.Ink)
                                {
                                    UpdateCurrentToolMode("pen");
                                    SetFloatingBarHighlightPosition("pen");
                                }
                            }
                            catch (Exception ex)
                            {
                                LogHelper.WriteLogToFile($"PPT进入批注模式后同步浮动栏高光状态失败: {ex.Message}", LogHelper.LogType.Error);
                            }
                        }), DispatcherPriority.Loaded);
                    }

                    isEnteredSlideShowEndEvent = false;

                    // 加载当前页墨迹
                    LoadCurrentSlideInk(currentSlide);
                });

                if (!isFloatingBarFolded)
                {
                    ViewboxFloatingBarMarginAnimation(60);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理幻灯片放映开始事件失败: {ex}", LogHelper.LogType.Error);
            }
        }

        private void OnPPTSlideShowNextSlide(SlideShowWindow wn)
        {
            try
            {
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var activePresentation = _pptManager?.GetCurrentActivePresentation();
                    if (activePresentation != null)
                    {
                        if (Settings.PowerPointSettings.IsSupportWPS)
                        {
                        }
                        else
                        {
                            _multiPPTInkManager?.SwitchToPresentation(activePresentation);
                        }
                    }

                    var currentSlide = _pptManager?.GetCurrentSlideNumber() ?? 0;
                    var totalSlides = _pptManager?.SlidesCount ?? 0;

                    // 使用防抖机制处理页面切换
                    HandleSlideSwitchWithDebounce(currentSlide, totalSlides);

                });
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理幻灯片切换事件失败: {ex}", LogHelper.LogType.Error);
            }
        }

        private async void OnPPTSlideShowEnd(Presentation pres)
        {
            try
            {
                if (Settings.Automation.IsAutoFoldAfterPPTSlideShow)
                {
                    if (wasFloatingBarFoldedWhenEnterSlideShow)
                    {
                        if (!isFloatingBarFolded) FoldFloatingBar_MouseUp(new object(), null);
                    }
                    else
                    {
                        if (isFloatingBarFolded) await UnFoldFloatingBar(new object());
                    }
                }
                else
                {
                    if (Settings.Automation.IsAutoFoldInPPTSlideShow)
                    {
                        if (isFloatingBarFolded)
                        {
                            await UnFoldFloatingBar(new object());
                        }
                    }
                    else
                    {
                        if (isFloatingBarFolded)
                        {
                            await UnFoldFloatingBar(new object());
                        }
                    }
                }

                if (isEnteredSlideShowEndEvent) return;
                isEnteredSlideShowEndEvent = true;

                if (Settings.PowerPointSettings.IsSupportWPS)
                {
                    _singlePPTInkManager?.SaveAllStrokesToFile(pres);
                }
                else
                {
                    _multiPPTInkManager?.SaveAllStrokesToFile(pres);
                }

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        isPresentationHaveBlackSpace = false;

                        // 恢复主题
                        if (BtnSwitchTheme.Content.ToString() == "深色")
                        {
                            BtnExit.Foreground = Brushes.Black;
                            ThemeManager.Current.ApplicationTheme = ApplicationTheme.Light;
                        }

                        // 更新UI状态
                        _pptUIManager?.UpdateSlideShowStatus(false);
                        _pptUIManager?.UpdateSidebarExitButtons(false);
                        _pptUIManager?.SetMainPanelMargin(new Thickness(10, 10, 10, 55));
                        _pptUIManager?.SetFloatingBarOpacity(Settings.Appearance.ViewboxFloatingBarOpacityValue);

                        if (currentMode != 0)
                        {
                            CloseWhiteboardImmediately();
                            currentMode = 0;
                        }

                        ClearStrokes(true);
                        // 清空备份历史记录，防止退出白板时恢复已结束PPT的墨迹
                        // 注意：这里只清空索引0的备份，不影响白板页面的墨迹（索引1及以上）
                        TimeMachineHistories[0] = null;

                        // 重置墨迹管理器的锁定状态，防止下次放映时墨迹显示错误
                        ResetInkManagerLockState();

                        // 退出PPT模式时恢复手势面板和手势按钮的显示状态
                        if (Settings.Gesture.IsEnableTwoFingerGesture && ToggleSwitchEnableMultiTouchMode.IsOn)
                        {
                            // 根据手势设置决定是否显示手势面板和手势按钮
                            CheckEnableTwoFingerGestureBtnVisibility(true);
                        }
                        else
                        {
                            // 如果手势功能未启用，确保手势按钮保持隐藏
                            EnableTwoFingerGestureBorder.Visibility = Visibility.Collapsed;
                        }

                        // 退出PPT模式时隐藏快捷调色盘
                        if (QuickColorPalettePanel != null)
                        {
                            QuickColorPalettePanel.Visibility = Visibility.Collapsed;
                        }
                        if (QuickColorPaletteSingleRowPanel != null)
                        {
                            QuickColorPaletteSingleRowPanel.Visibility = Visibility.Collapsed;
                        }

                        if (GridTransparencyFakeBackground.Background != Brushes.Transparent)
                            BtnHideInkCanvas_Click(BtnHideInkCanvas, null);
                        SetCurrentToolMode(InkCanvasEditingMode.None);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"处理幻灯片放映结束UI更新失败: {ex}", LogHelper.LogType.Error);
                    }
                });

                await Task.Delay(100);
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    PureViewboxFloatingBarMarginAnimationInDesktopMode();
                    ViewboxFloatingBarMarginAnimation(100, true);
                });
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理幻灯片放映结束事件失败: {ex}", LogHelper.LogType.Error);
            }
        }
        #endregion

        #region Helper Methods
        private void HandlePresentationOpenNavigation(Presentation pres)
        {
            try
            {
                if (Settings.PowerPointSettings.IsAlwaysGoToFirstPageOnReenter)
                {
                    _pptManager?.TryNavigateToSlide(1);
                }
                else if (Settings.PowerPointSettings.IsNotifyPreviousPage)
                {
                    ShowPreviousPageNotification(pres);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理演示文稿导航失败: {ex}", LogHelper.LogType.Error);
            }
        }

        private void ShowPreviousPageNotification(Presentation pres)
        {
            try
            {
                if (pres == null) return;

                var presentationPath = pres.FullName;
                var fileHash = GetFileHash(presentationPath);
                var folderName = pres.Name + "_" + pres.Slides.Count + "_" + fileHash;
                var folderPath = Path.Combine(Settings.Automation.AutoSavedStrokesLocation, "Auto Saved - Presentations", folderName);
                var positionFile = Path.Combine(folderPath, "Position");

                if (!File.Exists(positionFile)) return;

                if (int.TryParse(File.ReadAllText(positionFile), out var page) && page > 0)
                {
                    _lastPlaybackPage = page;
                    new YesOrNoNotificationWindow($"上次播放到了第 {page} 页, 是否立即跳转", () =>
                    {
                        try
                        {
                            if (_pptManager?.PPTApplication != null)
                            {
                                if (_pptManager.PPTApplication.SlideShowWindows.Count >= 1)
                                {
                                    pres.SlideShowWindow.View.GotoSlide(page);
                                }
                                else
                                {
                                    pres.Windows[1].View.GotoSlide(page);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"跳转到第{page}页失败: {ex}", LogHelper.LogType.Error);
                        }
                    }).ShowDialog();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"显示上次播放页通知失败: {ex}", LogHelper.LogType.Error);
            }
        }

        private void CheckAndNotifyHiddenSlides(Presentation pres)
        {
            try
            {
                bool hasHiddenSlides = false;
                if (pres?.Slides != null)
                {
                    foreach (Slide slide in pres.Slides)
                    {
                        if (slide.SlideShowTransition.Hidden == MsoTriState.msoTrue)
                        {
                            hasHiddenSlides = true;
                            break;
                        }
                    }
                }

                if (hasHiddenSlides && !IsShowingRestoreHiddenSlidesWindow)
                {
                    IsShowingRestoreHiddenSlidesWindow = true;
                    new YesOrNoNotificationWindow("检测到此演示文档中包含隐藏的幻灯片，是否取消隐藏？",
                        () =>
                        {
                            try
                            {
                                if (pres?.Slides != null)
                                {
                                    foreach (Slide slide in pres.Slides)
                                    {
                                        if (slide.SlideShowTransition.Hidden == MsoTriState.msoTrue)
                                            slide.SlideShowTransition.Hidden = MsoTriState.msoFalse;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                LogHelper.WriteLogToFile($"取消隐藏幻灯片失败: {ex}", LogHelper.LogType.Error);
                            }
                            finally
                            {
                                IsShowingRestoreHiddenSlidesWindow = false;
                            }
                        },
                        () => { IsShowingRestoreHiddenSlidesWindow = false; },
                        () => { IsShowingRestoreHiddenSlidesWindow = false; }).ShowDialog();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"检查隐藏幻灯片失败: {ex}", LogHelper.LogType.Error);
            }
        }

        private void CheckAndNotifyAutoPlaySettings(Presentation pres)
        {
            try
            {
                if (BtnPPTSlideShowEnd.Visibility == Visibility.Visible) return;

                bool hasSlideTimings = false;
                if (pres?.Slides != null)
                {
                    foreach (Slide slide in pres.Slides)
                    {
                        if (slide.SlideShowTransition.AdvanceOnTime == MsoTriState.msoTrue &&
                            slide.SlideShowTransition.AdvanceTime > 0)
                        {
                            hasSlideTimings = true;
                            break;
                        }
                    }
                }

                if (hasSlideTimings && !IsShowingAutoplaySlidesWindow)
                {
                    IsShowingAutoplaySlidesWindow = true;
                    new YesOrNoNotificationWindow("检测到此演示文档中自动播放或排练计时已经启用，可能导致幻灯片自动翻页，是否取消？",
                        () =>
                        {
                            try
                            {
                                if (pres != null)
                                {
                                    pres.SlideShowSettings.AdvanceMode = PpSlideShowAdvanceMode.ppSlideShowManualAdvance;
                                }
                            }
                            catch (Exception ex)
                            {
                                LogHelper.WriteLogToFile($"设置手动播放模式失败: {ex}", LogHelper.LogType.Error);
                            }
                            finally
                            {
                                IsShowingAutoplaySlidesWindow = false;
                            }
                        },
                        () => { IsShowingAutoplaySlidesWindow = false; },
                        () => { IsShowingAutoplaySlidesWindow = false; }).ShowDialog();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"检查自动播放设置失败: {ex}", LogHelper.LogType.Error);
            }
        }

        private void LoadCurrentSlideInk(int slideIndex)
        {
            try
            {
                StrokeCollection strokes = null;
                if (Settings.PowerPointSettings.IsSupportWPS)
                {
                    strokes = _singlePPTInkManager?.LoadSlideStrokes(slideIndex);
                }
                else
                {
                    strokes = _multiPPTInkManager?.LoadSlideStrokes(slideIndex);
                }

                if (strokes != null)
                {
                    inkCanvas.Strokes.Clear();
                    inkCanvas.Strokes.Add(strokes);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"加载当前页墨迹失败: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 重置墨迹管理器的锁定状态，防止墨迹显示错误
        /// </summary>
        private void ResetInkManagerLockState()
        {
            try
            {
                if (Settings.PowerPointSettings.IsSupportWPS)
                {
                    _singlePPTInkManager?.ResetLockState();
                }
                else
                {
                    var activePresentation = _pptManager?.GetCurrentActivePresentation();
                    if (activePresentation != null)
                    {
                        _multiPPTInkManager?.SwitchToPresentation(activePresentation);
                        _multiPPTInkManager?.ResetCurrentPresentationLockState();
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"重置墨迹管理器锁定状态失败: {ex}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 重置PPT相关的状态变量，当PPT自动收纳设置变更时调用
        /// </summary>
        public void ResetPPTStateVariables()
        {
            try
            {
                // 重置进入PPT时的浮动栏收纳状态记录
                wasFloatingBarFoldedWhenEnterSlideShow = false;

                // 重置PPT放映结束事件标志
                isEnteredSlideShowEndEvent = false;

                // 重置演示文稿黑边状态
                isPresentationHaveBlackSpace = false;

                // 重置上次播放位置相关字段
                _lastPlaybackPage = 0;
                _shouldNavigateToLastPage = false;

                // 重置页面切换防抖机制
                _lastSlideSwitchTime = DateTime.MinValue;
                _pendingSlideIndex = -1;

                LogHelper.WriteLogToFile("PPT状态变量已重置", LogHelper.LogType.Trace);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"重置PPT状态变量失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 使用防抖机制处理页面切换
        /// </summary>
        private void HandleSlideSwitchWithDebounce(int currentSlide, int totalSlides)
        {
            try
            {
                var now = DateTime.Now;

                // 如果距离上次切换时间太短，使用防抖机制
                if (now - _lastSlideSwitchTime < TimeSpan.FromMilliseconds(SlideSwitchDebounceMs))
                {
                    _pendingSlideIndex = currentSlide;

                    // 停止之前的定时器
                    _slideSwitchDebounceTimer?.Stop();

                    // 创建新的定时器
                    _slideSwitchDebounceTimer = new System.Timers.Timer(SlideSwitchDebounceMs);
                    _slideSwitchDebounceTimer.Elapsed += (sender, e) =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (_pendingSlideIndex > 0)
                            {
                                SwitchSlideInk(_pendingSlideIndex);
                                _pptUIManager?.UpdateCurrentSlideNumber(_pendingSlideIndex, totalSlides);
                                _pendingSlideIndex = -1;
                            }
                        });
                        _slideSwitchDebounceTimer?.Stop();
                    };
                    _slideSwitchDebounceTimer.Start();
                }
                else
                {
                    // 直接处理页面切换
                    SwitchSlideInk(currentSlide);
                    _pptUIManager?.UpdateCurrentSlideNumber(currentSlide, totalSlides);
                }

                _lastSlideSwitchTime = now;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理页面切换防抖失败: {ex}", LogHelper.LogType.Error);
            }
        }

        private void SwitchSlideInk(int newSlideIndex)
        {
            try
            {
                // 检查PPT连接状态
                if (_pptManager?.IsConnected != true || _pptManager?.IsInSlideShow != true)
                {
                    return;
                }

                // 获取当前页面索引
                var currentSlideIndex = _pptManager?.GetCurrentSlideNumber() ?? 0;


                // 验证页面索引的有效性
                if (newSlideIndex <= 0)
                {
                    LogHelper.WriteLogToFile($"无效的新页面索引: {newSlideIndex}，跳过页面切换", LogHelper.LogType.Warning);
                    return;
                }

                // 如果有当前墨迹且不是第一次切换，先保存到当前页面
                if (inkCanvas.Strokes.Count > 0 && currentSlideIndex > 0 && currentSlideIndex != newSlideIndex)
                {
                    bool canWrite = false;
                    if (Settings.PowerPointSettings.IsSupportWPS)
                    {
                        canWrite = _singlePPTInkManager?.CanWriteInk(currentSlideIndex) == true;
                    }
                    else
                    {
                        canWrite = _multiPPTInkManager?.CanWriteInk(currentSlideIndex) == true;
                    }

                    if (canWrite)
                    {
                        if (Settings.PowerPointSettings.IsSupportWPS)
                        {
                            _singlePPTInkManager?.SaveCurrentSlideStrokes(currentSlideIndex, inkCanvas.Strokes);
                        }
                        else
                        {
                            _multiPPTInkManager?.SaveCurrentSlideStrokes(currentSlideIndex, inkCanvas.Strokes);
                        }
                    }
                }
                else if (inkCanvas.Strokes.Count > 0 && currentSlideIndex <= 0)
                {
                }

                StrokeCollection newStrokes = null;
                if (Settings.PowerPointSettings.IsSupportWPS)
                {
                    newStrokes = _singlePPTInkManager?.SwitchToSlide(newSlideIndex, null);
                }
                else
                {
                    newStrokes = _multiPPTInkManager?.SwitchToSlide(newSlideIndex, null);
                }
                if (newStrokes != null)
                {
                    inkCanvas.Strokes.Clear();
                    inkCanvas.Strokes.Add(newStrokes);
                }

                // 注意：LockInkForSlide已经在SwitchToSlide中调用，这里不需要重复调用
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"切换页面墨迹失败: {ex}", LogHelper.LogType.Error);
            }
        }

        private string GetFileHash(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath)) return "unknown";

                using (var md5 = MD5.Create())
                {
                    byte[] hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(filePath));
                    return BitConverter.ToString(hashBytes).Replace("-", "").Substring(0, 8);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"计算文件哈希值失败: {ex}", LogHelper.LogType.Error);
                return "error";
            }
        }
        #endregion

        private void BtnCheckPPT_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 使用新的PPT管理器进行连接检查
                if (_pptManager == null)
                {
                    InitializePPTManagers();
                }

                // 手动触发一次连接检查
                _pptManager?.StartMonitoring();

                // 等待一小段时间让连接建立
                Task.Delay(500).ContinueWith(_ =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (_pptManager?.IsConnected == true)
                        {
                            LogHelper.WriteLogToFile("手动PPT连接检查成功", LogHelper.LogType.Event);
                        }
                        else
                        {
                            MessageBox.Show("未找到幻灯片");
                            LogHelper.WriteLogToFile("手动PPT连接检查失败", LogHelper.LogType.Warning);
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"手动检查PPT应用程序失败: {ex}", LogHelper.LogType.Error);
                _pptUIManager?.UpdateConnectionStatus(false);
                MessageBox.Show("未找到幻灯片");
            }
        }

        private void ToggleSwitchPowerPointEnhancement_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            Settings.PowerPointSettings.EnablePowerPointEnhancement = ToggleSwitchPowerPointEnhancement.IsOn;

            if (Settings.PowerPointSettings.EnablePowerPointEnhancement)
            {
                Settings.PowerPointSettings.IsSupportWPS = false;
                ToggleSwitchSupportWPS.IsOn = false;

                // 更新PPT管理器的WPS支持设置
                if (_pptManager != null)
                {
                    _pptManager.IsSupportWPS = false;
                }
            }

            SaveSettingsToFile();

            // 启动或停止PowerPoint进程守护
            if (Settings.PowerPointSettings.EnablePowerPointEnhancement)
            {
                StartPowerPointProcessMonitoring();
            }
            else
            {
                StopPowerPointProcessMonitoring();
            }
        }

        private void ToggleSwitchSupportWPS_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;

            Settings.PowerPointSettings.IsSupportWPS = ToggleSwitchSupportWPS.IsOn;

            if (Settings.PowerPointSettings.IsSupportWPS)
            {
                Settings.PowerPointSettings.EnablePowerPointEnhancement = false;
                ToggleSwitchPowerPointEnhancement.IsOn = false;
                StopPowerPointProcessMonitoring();
            }

            // 更新PPT管理器的WPS支持设置
            if (_pptManager != null)
            {
                _pptManager.IsSupportWPS = Settings.PowerPointSettings.IsSupportWPS;
            }

            SaveSettingsToFile();
        }

        private static bool isWPSSupportOn => Settings.PowerPointSettings.IsSupportWPS;

        public static bool IsShowingRestoreHiddenSlidesWindow;
        private static bool IsShowingAutoplaySlidesWindow;

        private void BtnPPTSlidesUp_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    // 保存当前页墨迹
                    var currentSlide = _pptManager?.GetCurrentSlideNumber() ?? 0;
                    if (currentSlide > 0)
                    {
                        if (Settings.PowerPointSettings.IsSupportWPS)
                        {
                            _singlePPTInkManager?.SaveCurrentSlideStrokes(currentSlide, inkCanvas.Strokes);
                        }
                        else
                        {
                            _multiPPTInkManager?.SaveCurrentSlideStrokes(currentSlide, inkCanvas.Strokes);
                        }
                    }

                    // 保存截图（如果启用）
                    if (inkCanvas.Strokes.Count > Settings.Automation.MinimumAutomationStrokeNumber &&
                        Settings.PowerPointSettings.IsAutoSaveScreenShotInPowerPoint)
                    {
                        var presentationName = _pptManager?.GetPresentationName() ?? "";
                        SaveScreenShot(true, $"{presentationName}/{currentSlide}");
                    }

                    // 执行翻页
                    if (_pptManager?.TryNavigatePrevious() == true)
                    {
                        // 翻页成功，等待事件处理墨迹切换
                    }
                    else
                    {
                        LogHelper.WriteLogToFile("切换到上一页失败", LogHelper.LogType.Warning);
                        _pptUIManager?.UpdateConnectionStatus(false);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"PPT上一页操作异常: {ex}", LogHelper.LogType.Error);
                    _pptUIManager?.UpdateConnectionStatus(false);
                }
            });
        }

        private void BtnPPTSlidesDown_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    // 保存当前页墨迹
                    var currentSlide = _pptManager?.GetCurrentSlideNumber() ?? 0;
                    if (currentSlide > 0)
                    {
                        if (Settings.PowerPointSettings.IsSupportWPS)
                        {
                            _singlePPTInkManager?.SaveCurrentSlideStrokes(currentSlide, inkCanvas.Strokes);
                        }
                        else
                        {
                            _multiPPTInkManager?.SaveCurrentSlideStrokes(currentSlide, inkCanvas.Strokes);
                        }
                    }

                    // 保存截图（如果启用）
                    if (inkCanvas.Strokes.Count > Settings.Automation.MinimumAutomationStrokeNumber &&
                        Settings.PowerPointSettings.IsAutoSaveScreenShotInPowerPoint)
                    {
                        var presentationName = _pptManager?.GetPresentationName() ?? "";
                        SaveScreenShot(true, $"{presentationName}/{currentSlide}");
                    }

                    // 执行翻页
                    if (_pptManager?.TryNavigateNext() == true)
                    {
                        // 翻页成功，等待事件处理墨迹切换
                    }
                    else
                    {
                        LogHelper.WriteLogToFile("切换到下一页失败", LogHelper.LogType.Warning);
                        _pptUIManager?.UpdateConnectionStatus(false);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"PPT下一页操作异常: {ex}", LogHelper.LogType.Error);
                    _pptUIManager?.UpdateConnectionStatus(false);
                }
            });
        }

        private void PPTNavigationBtn_MouseDown(object sender, MouseButtonEventArgs e)
        {
            lastBorderMouseDownObject = sender;
            if (!Settings.PowerPointSettings.EnablePPTButtonPageClickable) return;
            if (sender == PPTLSPageButton)
            {
                PPTLSPageButtonFeedbackBorder.Opacity = 0.15;
            }
            else if (sender == PPTRSPageButton)
            {
                PPTRSPageButtonFeedbackBorder.Opacity = 0.15;
            }
            else if (sender == PPTLBPageButton)
            {
                PPTLBPageButtonFeedbackBorder.Opacity = 0.15;
            }
            else if (sender == PPTRBPageButton)
            {
                PPTRBPageButtonFeedbackBorder.Opacity = 0.15;
            }
        }

        private void PPTNavigationBtn_MouseLeave(object sender, MouseEventArgs e)
        {
            lastBorderMouseDownObject = null;
            if (sender == PPTLSPageButton)
            {
                PPTLSPageButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTRSPageButton)
            {
                PPTRSPageButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTLBPageButton)
            {
                PPTLBPageButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTRBPageButton)
            {
                PPTRBPageButtonFeedbackBorder.Opacity = 0;
            }
        }

        private void PPTNavigationBtn_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (lastBorderMouseDownObject != sender) return;

            if (sender == PPTLSPageButton)
            {
                PPTLSPageButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTRSPageButton)
            {
                PPTRSPageButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTLBPageButton)
            {
                PPTLBPageButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTRBPageButton)
            {
                PPTRBPageButtonFeedbackBorder.Opacity = 0;
            }

            if (!Settings.PowerPointSettings.EnablePPTButtonPageClickable) return;

            // 使用新的PPT管理器检查连接状态
            if (_pptManager?.IsConnected != true || _pptManager?.IsInSlideShow != true)
            {
                LogHelper.WriteLogToFile("PPT未连接或未在放映状态，无法执行页码点击操作", LogHelper.LogType.Warning);
                return;
            }

            try
            {
                GridTransparencyFakeBackground.Opacity = 1;
                GridTransparencyFakeBackground.Background = new SolidColorBrush(StringToColor("#01FFFFFF"));
                CursorIcon_Click(null, null);

                // 使用新的PPT管理器显示导航
                if (_pptManager.TryShowSlideNavigation())
                {
                    LogHelper.WriteLogToFile("成功显示PPT幻灯片导航", LogHelper.LogType.Trace);
                }
                else
                {
                    LogHelper.WriteLogToFile("显示PPT幻灯片导航失败", LogHelper.LogType.Warning);
                }

                // 控制居中
                if (!isFloatingBarFolded)
                {
                    ViewboxFloatingBarMarginAnimation(60);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"PPT翻页控件操作失败: {ex}", LogHelper.LogType.Error);
            }
        }

        private void BtnPPTSlideShow_Click(object sender, RoutedEventArgs e)
        {
            new Thread(() =>
            {
                try
                {
                    if (_pptManager?.TryStartSlideShow() != true)
                    {
                        LogHelper.WriteLogToFile("启动幻灯片放映失败", LogHelper.LogType.Warning);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"启动幻灯片放映异常: {ex}", LogHelper.LogType.Error);
                }
            }).Start();
        }

        private async void BtnPPTSlideShowEnd_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 保存当前页墨迹
                var currentSlide = _pptManager?.GetCurrentSlideNumber() ?? 0;
                if (currentSlide > 0)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (Settings.PowerPointSettings.IsSupportWPS)
                        {
                            _singlePPTInkManager?.SaveCurrentSlideStrokes(currentSlide, inkCanvas.Strokes);
                        }
                        else
                        {
                            _multiPPTInkManager?.SaveCurrentSlideStrokes(currentSlide, inkCanvas.Strokes);
                        }
                        timeMachine.ClearStrokeHistory();
                    });
                }

                // 结束放映
                if (_pptManager?.TryEndSlideShow() == true)
                {
                    // 如果成功结束放映，等待OnPPTSlideShowEnd事件处理收纳状态恢复
                }
                else
                {
                    LogHelper.WriteLogToFile("结束幻灯片放映失败", LogHelper.LogType.Warning);

                    // 手动更新UI状态，防止事件未触发
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        _pptUIManager?.UpdateSlideShowStatus(false);
                        _pptUIManager?.UpdateSidebarExitButtons(false);
                        LogHelper.WriteLogToFile("手动更新放映结束UI状态", LogHelper.LogType.Trace);
                    });

                    // 手动处理收纳状态恢复，因为OnPPTSlideShowEnd事件可能未触发
                    await HandleManualSlideShowEnd();
                }

                HideSubPanels("cursor");
                SetCurrentToolMode(InkCanvasEditingMode.None);

                await Task.Delay(150);
                if (Settings.Automation.IsAutoFoldAfterPPTSlideShow)
                {
                    if (wasFloatingBarFoldedWhenEnterSlideShow)
                    {
                        ViewboxFloatingBarMarginAnimation(-60);
                    }
                    else
                    {
                        ViewboxFloatingBarMarginAnimation(100, true);
                    }
                }
                else
                {
                    if (isFloatingBarFolded)
                    {
                        ViewboxFloatingBarMarginAnimation(-60);
                    }
                    else
                    {
                        ViewboxFloatingBarMarginAnimation(100, true);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"结束PPT放映操作异常: {ex}", LogHelper.LogType.Error);

                // 确保UI状态正确
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _pptUIManager?.UpdateSlideShowStatus(false);
                    _pptUIManager?.UpdateSidebarExitButtons(false);
                });

                // 异常情况下也手动处理收纳状态恢复
                await HandleManualSlideShowEnd();

                // 异常情况下也要根据设置决定浮动栏边距
                await Task.Delay(150);
                if (Settings.Automation.IsAutoFoldAfterPPTSlideShow)
                {
                    if (wasFloatingBarFoldedWhenEnterSlideShow)
                    {
                        ViewboxFloatingBarMarginAnimation(-60);
                    }
                    else
                    {
                        ViewboxFloatingBarMarginAnimation(100, true);
                    }
                }
                else
                {
                    if (isFloatingBarFolded)
                    {
                        ViewboxFloatingBarMarginAnimation(-60);
                    }
                    else
                    {
                        ViewboxFloatingBarMarginAnimation(100, true);
                    }
                }
            }
        }

        /// <summary>
        /// 手动处理PPT放映结束时的收纳状态恢复
        /// </summary>
        private async Task HandleManualSlideShowEnd()
        {
            try
            {
                if (Settings.Automation.IsAutoFoldAfterPPTSlideShow)
                {
                    if (wasFloatingBarFoldedWhenEnterSlideShow)
                    {
                        if (!isFloatingBarFolded) FoldFloatingBar_MouseUp(new object(), null);
                    }
                    else
                    {
                        if (isFloatingBarFolded) await UnFoldFloatingBar(new object());
                    }
                }
                else
                {
                    if (Settings.Automation.IsAutoFoldInPPTSlideShow)
                    {
                        if (isFloatingBarFolded)
                        {
                            await UnFoldFloatingBar(new object());
                        }
                    }
                    else
                    {
                        // 如果两个功能都关闭，确保浮动栏展开
                        if (isFloatingBarFolded)
                        {
                            await UnFoldFloatingBar(new object());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"手动处理PPT放映结束收纳状态恢复失败: {ex}", LogHelper.LogType.Error);
            }
        }

        private void GridPPTControlPrevious_MouseDown(object sender, MouseButtonEventArgs e)
        {
            lastBorderMouseDownObject = sender;
            if (sender == PPTLSPreviousButtonBorder)
            {
                PPTLSPreviousButtonFeedbackBorder.Opacity = 0.15;
            }
            else if (sender == PPTRSPreviousButtonBorder)
            {
                PPTRSPreviousButtonFeedbackBorder.Opacity = 0.15;
            }
            else if (sender == PPTLBPreviousButtonBorder)
            {
                PPTLBPreviousButtonFeedbackBorder.Opacity = 0.15;
            }
            else if (sender == PPTRBPreviousButtonBorder)
            {
                PPTRBPreviousButtonFeedbackBorder.Opacity = 0.15;
            }

            // 启动长按检测
            if (Settings.PowerPointSettings.EnablePPTButtonLongPressPageTurn)
            {
                StartLongPressDetection(sender, false);
            }
        }
        private void GridPPTControlPrevious_MouseLeave(object sender, MouseEventArgs e)
        {
            lastBorderMouseDownObject = null;
            if (sender == PPTLSPreviousButtonBorder)
            {
                PPTLSPreviousButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTRSPreviousButtonBorder)
            {
                PPTRSPreviousButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTLBPreviousButtonBorder)
            {
                PPTLBPreviousButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTRBPreviousButtonBorder)
            {
                PPTRBPreviousButtonFeedbackBorder.Opacity = 0;
            }

            // 停止长按检测
            StopLongPressDetection();
        }
        private void GridPPTControlPrevious_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (lastBorderMouseDownObject != sender) return;
            if (sender == PPTLSPreviousButtonBorder)
            {
                PPTLSPreviousButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTRSPreviousButtonBorder)
            {
                PPTRSPreviousButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTLBPreviousButtonBorder)
            {
                PPTLBPreviousButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTRBPreviousButtonBorder)
            {
                PPTRBPreviousButtonFeedbackBorder.Opacity = 0;
            }

            // 停止长按检测
            StopLongPressDetection();

            BtnPPTSlidesUp_Click(BtnPPTSlidesUp, null);
        }


        private void GridPPTControlNext_MouseDown(object sender, MouseButtonEventArgs e)
        {
            lastBorderMouseDownObject = sender;
            if (sender == PPTLSNextButtonBorder)
            {
                PPTLSNextButtonFeedbackBorder.Opacity = 0.15;
            }
            else if (sender == PPTRSNextButtonBorder)
            {
                PPTRSNextButtonFeedbackBorder.Opacity = 0.15;
            }
            else if (sender == PPTLBNextButtonBorder)
            {
                PPTLBNextButtonFeedbackBorder.Opacity = 0.15;
            }
            else if (sender == PPTRBNextButtonBorder)
            {
                PPTRBNextButtonFeedbackBorder.Opacity = 0.15;
            }

            // 启动长按检测
            if (Settings.PowerPointSettings.EnablePPTButtonLongPressPageTurn)
            {
                StartLongPressDetection(sender, true);
            }
        }
        private void GridPPTControlNext_MouseLeave(object sender, MouseEventArgs e)
        {
            lastBorderMouseDownObject = null;
            if (sender == PPTLSNextButtonBorder)
            {
                PPTLSNextButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTRSNextButtonBorder)
            {
                PPTRSNextButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTLBNextButtonBorder)
            {
                PPTLBNextButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTRBNextButtonBorder)
            {
                PPTRBNextButtonFeedbackBorder.Opacity = 0;
            }

            // 停止长按检测
            StopLongPressDetection();
        }
        private void GridPPTControlNext_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (lastBorderMouseDownObject != sender) return;
            if (sender == PPTLSNextButtonBorder)
            {
                PPTLSNextButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTRSNextButtonBorder)
            {
                PPTRSNextButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTLBNextButtonBorder)
            {
                PPTLBNextButtonFeedbackBorder.Opacity = 0;
            }
            else if (sender == PPTRBNextButtonBorder)
            {
                PPTRBNextButtonFeedbackBorder.Opacity = 0;
            }

            // 停止长按检测
            StopLongPressDetection();

            BtnPPTSlidesDown_Click(BtnPPTSlidesDown, null);
        }

        private void ImagePPTControlEnd_MouseUp(object sender, MouseButtonEventArgs e)
        {
            BtnPPTSlideShowEnd_Click(BtnPPTSlideShowEnd, null);
        }
    }
}
