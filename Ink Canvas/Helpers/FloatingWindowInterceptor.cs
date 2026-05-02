using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Threading;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 悬浮窗拦截器 - 检测和隐藏指定的悬浮窗
    /// </summary>
    public class FloatingWindowInterceptor : IDisposable
    {
        #region Windows API Declarations

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out ForegroundWindowInfo.RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hWnd, int dwAttribute, out ForegroundWindowInfo.RECT pvAttribute, int cbAttribute);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll")]
        private static extern int GetProcessImageFileName(IntPtr hProcess, StringBuilder lpImageFileName, int nSize);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 1;
        private const int SW_SHOWNORMAL = 1;
        private const int SW_MINIMIZE = 6;
        private const int SW_RESTORE = 9;

        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;
        private const uint WS_EX_TOOLWINDOW = 0x00000080;
        private const uint WS_EX_APPWINDOW = 0x00040000;

        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_HIDEWINDOW = 0x0080;
        private const uint SWP_SHOWWINDOW = 0x0040;

        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const uint PROCESS_VM_READ = 0x0010;

        private const uint WM_CLOSE = 0x0010;
        private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
        private const int LOGPIXELSX = 88;
        private const int LOGPIXELSY = 90;


        #endregion

        #region 拦截规则定义

        /// <summary>
        /// 拦截规则类型
        /// </summary>
        public enum InterceptType
        {
            /// <summary>
            /// 希沃白板3 桌面悬浮窗
            /// </summary>
            SeewoWhiteboard3Floating,
            /// <summary>
            /// 希沃白板5 桌面悬浮窗
            /// </summary>
            SeewoWhiteboard5Floating,
            /// <summary>
            /// 希沃白板5C 桌面悬浮窗
            /// </summary>
            SeewoWhiteboard5CFloating,
            /// <summary>
            /// 希沃品课教师端 桌面悬浮窗
            /// </summary>
            SeewoPincoSideBarFloating,
            /// <summary>
            /// 希沃品课教师端 画笔悬浮窗（包括PPT控件）
            /// </summary>
            SeewoPincoDrawingFloating,
            /// <summary>
            /// 希沃品课教师端 桌面画板
            /// </summary>
            SeewoPincoBoardService,
            /// <summary>
            /// 希沃PPT小工具
            /// </summary>
            SeewoPPTFloating,
            /// <summary>
            /// AiClass 桌面悬浮窗
            /// </summary>
            AiClassFloating,
            /// <summary>
            /// 鸿合屏幕书写
            /// </summary>
            HiteAnnotationFloating,
            /// <summary>
            /// 畅言智慧课堂 主栏悬浮窗
            /// </summary>
            ChangYanFloating,
            /// <summary>
            /// 畅言智慧课堂 画笔设置
            /// </summary>
            ChangYanBrushSettings,
            /// <summary>
            /// 畅言智慧课堂 滑动清除
            /// </summary>
            ChangYanSwipeClear,
            /// <summary>
            /// 畅言智慧课堂 互动
            /// </summary>
            ChangYanInteraction,
            /// <summary>
            /// 畅言智慧课堂 学科应用
            /// </summary>
            ChangYanSubjectApp,
            /// <summary>
            /// 畅言智慧课堂 管控
            /// </summary>
            ChangYanControl,
            /// <summary>
            /// 畅言智慧课堂 通用工具
            /// </summary>
            ChangYanCommonTools,
            /// <summary>
            /// 畅言智慧课堂 场景工具栏
            /// </summary>
            ChangYanSceneToolbar,
            /// <summary>
            /// 畅言智慧课堂 绘制窗口
            /// </summary>
            ChangYanDrawWindow,
            /// <summary>
            /// 畅言智慧课堂 PPT悬浮窗
            /// </summary>
            ChangYanPptFloating,
            /// <summary>
            /// 畅言智慧课堂 PPT页面控制
            /// </summary>
            ChangYanPptPageControl,
            /// <summary>
            /// 畅言智慧课堂 PPT返回
            /// </summary>
            ChangYanPptGoBack,
            /// <summary>
            /// 畅言智慧课堂 PPT预览
            /// </summary>
            ChangYanPptPreview,
            /// <summary>
            /// 天喻教育云互动课堂 桌面悬浮窗（包括PPT控件）
            /// </summary>
            IntelligentClassFloating,
            /// <summary>
            /// 天喻教育云互动课堂 PPT悬浮窗
            /// </summary>
            IntelligentClassPptFloating,
            /// <summary>
            /// 希沃桌面 画笔悬浮窗
            /// </summary>
            SeewoDesktopAnnotationFloating,
            /// <summary>
            /// 希沃桌面 侧栏悬浮窗
            /// </summary>
            SeewoDesktopSideBarFloating
        }

        /// <summary>
        /// 拦截规则
        /// </summary>
        public class InterceptRule
        {
            public InterceptType Type { get; set; }
            public string ProcessName { get; set; }
            public string WindowTitlePattern { get; set; }
            public string ClassNamePattern { get; set; }
            public bool IsEnabled { get; set; }
            public bool RequiresAdmin { get; set; }
            public string Description { get; set; }
            public InterceptType? ParentType { get; set; }
            public List<InterceptType> ChildTypes { get; set; } = new List<InterceptType>();

            // 新增的精确匹配字段
            public bool HasWindowStyle { get; set; }
            public uint WindowStyle { get; set; }
            public bool HasWindowSize { get; set; }
            public int WindowWidth { get; set; }
            public int WindowHeight { get; set; }
            public bool ExactTitleMatch { get; set; } = false;
            public bool ExactClassNameMatch { get; set; } = false;

            // 运行时状态字段
            public bool foundHwnd { get; set; } = false;
            public IntPtr outHwnd { get; set; } = IntPtr.Zero;
        }

        #endregion

        #region 私有字段

        private readonly Dictionary<InterceptType, InterceptRule> _interceptRules;
        private readonly Dictionary<IntPtr, InterceptType> _interceptedWindows;
        private readonly Timer _scanTimer;
        private readonly Dispatcher _dispatcher;
        private bool _isRunning;
        private bool _disposed;

        // 简化的性能统计
        private int _consecutiveEmptyScans = 0;
        private DateTime _lastSuccessfulScan = DateTime.Now;

        #endregion

        #region 公共属性

        public bool IsRunning => _isRunning;

        #endregion

        #region 事件

        public event EventHandler<WindowInterceptedEventArgs> WindowIntercepted;
        public event EventHandler<WindowRestoredEventArgs> WindowRestored;

        #endregion

        #region 构造函数

        public FloatingWindowInterceptor()
        {
            _interceptRules = new Dictionary<InterceptType, InterceptRule>();
            _interceptedWindows = new Dictionary<IntPtr, InterceptType>();
            _dispatcher = Dispatcher.CurrentDispatcher;

            InitializeRules();
            _scanTimer = new Timer(ScanForWindows, null, Timeout.Infinite, Timeout.Infinite);
        }

        #endregion

        #region 初始化

        private void InitializeRules()
        {
            // 希沃白板3 桌面悬浮窗
            _interceptRules[InterceptType.SeewoWhiteboard3Floating] = new InterceptRule
            {
                Type = InterceptType.SeewoWhiteboard3Floating,
                ProcessName = "EasiNote",
                WindowTitlePattern = "Note",
                ClassNamePattern = "HwndWrapper[EasiNote.exe;;",
                IsEnabled = true,
                RequiresAdmin = false,
                Description = "希沃白板3 桌面悬浮窗",
                HasWindowStyle = true,
                WindowStyle = 370081792,
                HasWindowSize = true,
                WindowWidth = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width,
                WindowHeight = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height,
                ExactTitleMatch = true,
                ExactClassNameMatch = false
            };

            // 希沃白板5 桌面悬浮窗
            _interceptRules[InterceptType.SeewoWhiteboard5Floating] = new InterceptRule
            {
                Type = InterceptType.SeewoWhiteboard5Floating,
                ProcessName = "EasiNote",
                WindowTitlePattern = "",
                ClassNamePattern = "HwndWrapper[EasiNote;;",
                IsEnabled = true,
                RequiresAdmin = false,
                Description = "希沃白板5 桌面悬浮窗",
                HasWindowStyle = true,
                WindowStyle = 369623040,
                HasWindowSize = true,
                WindowWidth = 550,
                WindowHeight = 200,
                ExactTitleMatch = false,
                ExactClassNameMatch = false
            };

            // 希沃白板5C 桌面悬浮窗
            _interceptRules[InterceptType.SeewoWhiteboard5CFloating] = new InterceptRule
            {
                Type = InterceptType.SeewoWhiteboard5CFloating,
                ProcessName = "EasiNote5C",
                WindowTitlePattern = "",
                ClassNamePattern = "HwndWrapper[EasiNote5C;;",
                IsEnabled = true,
                RequiresAdmin = false,
                Description = "希沃白板5C 桌面悬浮窗",
                HasWindowStyle = true,
                WindowStyle = 369623040,
                HasWindowSize = true,
                WindowWidth = 550,
                WindowHeight = 200,
                ExactTitleMatch = false,
                ExactClassNameMatch = false
            };

            // 希沃品课教师端 桌面悬浮窗（父规则）
            _interceptRules[InterceptType.SeewoPincoSideBarFloating] = new InterceptRule
            {
                Type = InterceptType.SeewoPincoSideBarFloating,
                ProcessName = "ClassIn",
                WindowTitlePattern = "希沃品课——appBar",
                ClassNamePattern = "Chrome_WidgetWin_1",
                IsEnabled = true,
                RequiresAdmin = false,
                Description = "希沃品课教师端 桌面悬浮窗",
                ParentType = null,
                ChildTypes = new List<InterceptType> { InterceptType.SeewoPincoDrawingFloating, InterceptType.SeewoPincoBoardService },
                HasWindowStyle = true,
                WindowStyle = 0x16CF0000,
                ExactTitleMatch = true,
                ExactClassNameMatch = true
            };

            // 希沃品课教师端 画笔悬浮窗（子规则）
            _interceptRules[InterceptType.SeewoPincoDrawingFloating] = new InterceptRule
            {
                Type = InterceptType.SeewoPincoDrawingFloating,
                ProcessName = "ClassIn",
                WindowTitlePattern = "希沃品课——integration",
                ClassNamePattern = "Chrome_WidgetWin_1",
                IsEnabled = true,
                RequiresAdmin = false,
                Description = "希沃品课教师端 画笔悬浮窗（包括PPT控件）",
                ParentType = InterceptType.SeewoPincoSideBarFloating,
                ChildTypes = new List<InterceptType>(),
                HasWindowStyle = true,
                WindowStyle = 335675392,
                ExactTitleMatch = true,
                ExactClassNameMatch = true
            };

            // 希沃品课教师端 桌面画板（子规则）
            _interceptRules[InterceptType.SeewoPincoBoardService] = new InterceptRule
            {
                Type = InterceptType.SeewoPincoBoardService,
                ProcessName = "BoardService",
                WindowTitlePattern = "",
                ClassNamePattern = "HwndWrapper[BoardService;;",
                IsEnabled = true,
                RequiresAdmin = false,
                Description = "希沃品课教师端 桌面画板",
                ParentType = InterceptType.SeewoPincoSideBarFloating,
                ChildTypes = new List<InterceptType>(),
                HasWindowStyle = true,
                WindowStyle = 369623040,
                HasWindowSize = true,
                WindowWidth = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width,
                WindowHeight = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height,
                ExactTitleMatch = false,
                ExactClassNameMatch = false
            };

            // 希沃PPT小工具
            _interceptRules[InterceptType.SeewoPPTFloating] = new InterceptRule
            {
                Type = InterceptType.SeewoPPTFloating,
                ProcessName = "PPTService",
                WindowTitlePattern = "",
                ClassNamePattern = "HwndWrapper[PPTService.exe;;",
                IsEnabled = true,
                RequiresAdmin = false,
                Description = "希沃PPT小工具"
            };

            // AiClass 桌面悬浮窗
            _interceptRules[InterceptType.AiClassFloating] = new InterceptRule
            {
                Type = InterceptType.AiClassFloating,
                ProcessName = "ClassIn",
                WindowTitlePattern = "TransparentWindow",
                ClassNamePattern = "UIWndTransparent",
                IsEnabled = true,
                RequiresAdmin = false,
                Description = "AiClass 桌面悬浮窗"
            };

            // 鸿合屏幕书写
            _interceptRules[InterceptType.HiteAnnotationFloating] = new InterceptRule
            {
                Type = InterceptType.HiteAnnotationFloating,
                ProcessName = "HiteVision",
                WindowTitlePattern = "HiteAnnotation",
                ClassNamePattern = "Qt5QWindowToolSaveBits",
                IsEnabled = true,
                RequiresAdmin = false,
                Description = "鸿合屏幕书写"
            };

            // 畅言智慧课堂 主栏悬浮窗（父规则）
            _interceptRules[InterceptType.ChangYanFloating] = new InterceptRule
            {
                Type = InterceptType.ChangYanFloating,
                ProcessName = "ClassIn",
                WindowTitlePattern = "ifly",
                ClassNamePattern = "Qt5QWindowOwnDCIcon",
                IsEnabled = true,
                RequiresAdmin = true,
                Description = "畅言智慧课堂 主栏悬浮窗",
                ParentType = null,
                ChildTypes = new List<InterceptType>
                {
                    InterceptType.ChangYanBrushSettings,
                    InterceptType.ChangYanSwipeClear,
                    InterceptType.ChangYanInteraction,
                    InterceptType.ChangYanSubjectApp,
                    InterceptType.ChangYanControl,
                    InterceptType.ChangYanCommonTools,
                    InterceptType.ChangYanSceneToolbar,
                    InterceptType.ChangYanDrawWindow
                }
            };

            // 畅言智慧课堂 画笔设置（子规则）
            _interceptRules[InterceptType.ChangYanBrushSettings] = new InterceptRule
            {
                Type = InterceptType.ChangYanBrushSettings,
                ProcessName = "ClassIn",
                WindowTitlePattern = "画笔设置",
                ClassNamePattern = "Qt5QWindowOwnDCIcon",
                IsEnabled = true,
                RequiresAdmin = true,
                Description = "畅言智慧课堂 画笔设置",
                ParentType = InterceptType.ChangYanFloating,
                ChildTypes = new List<InterceptType>()
            };

            // 畅言智慧课堂 滑动清除（子规则）
            _interceptRules[InterceptType.ChangYanSwipeClear] = new InterceptRule
            {
                Type = InterceptType.ChangYanSwipeClear,
                ProcessName = "ClassIn",
                WindowTitlePattern = "滑动清除",
                ClassNamePattern = "Qt5QWindowOwnDCIcon",
                IsEnabled = true,
                RequiresAdmin = true,
                Description = "畅言智慧课堂 滑动清除",
                ParentType = InterceptType.ChangYanFloating,
                ChildTypes = new List<InterceptType>()
            };

            // 畅言智慧课堂 互动（子规则）
            _interceptRules[InterceptType.ChangYanInteraction] = new InterceptRule
            {
                Type = InterceptType.ChangYanInteraction,
                ProcessName = "ClassIn",
                WindowTitlePattern = "互动",
                ClassNamePattern = "Qt5QWindowOwnDCIcon",
                IsEnabled = true,
                RequiresAdmin = true,
                Description = "畅言智慧课堂 互动",
                ParentType = InterceptType.ChangYanFloating,
                ChildTypes = new List<InterceptType>()
            };

            // 畅言智慧课堂 学科应用（子规则）
            _interceptRules[InterceptType.ChangYanSubjectApp] = new InterceptRule
            {
                Type = InterceptType.ChangYanSubjectApp,
                ProcessName = "ClassIn",
                WindowTitlePattern = "学科应用",
                ClassNamePattern = "Qt5QWindowOwnDCIcon",
                IsEnabled = true,
                RequiresAdmin = true,
                Description = "畅言智慧课堂 学科应用",
                ParentType = InterceptType.ChangYanFloating,
                ChildTypes = new List<InterceptType>()
            };

            // 畅言智慧课堂 管控（子规则）
            _interceptRules[InterceptType.ChangYanControl] = new InterceptRule
            {
                Type = InterceptType.ChangYanControl,
                ProcessName = "ClassIn",
                WindowTitlePattern = "管控",
                ClassNamePattern = "Qt5QWindowOwnDCIcon",
                IsEnabled = true,
                RequiresAdmin = true,
                Description = "畅言智慧课堂 管控",
                ParentType = InterceptType.ChangYanFloating,
                ChildTypes = new List<InterceptType>()
            };

            // 畅言智慧课堂 通用工具（子规则）
            _interceptRules[InterceptType.ChangYanCommonTools] = new InterceptRule
            {
                Type = InterceptType.ChangYanCommonTools,
                ProcessName = "ClassIn",
                WindowTitlePattern = "通用工具",
                ClassNamePattern = "Qt5QWindowOwnDCIcon",
                IsEnabled = true,
                RequiresAdmin = true,
                Description = "畅言智慧课堂 通用工具",
                ParentType = InterceptType.ChangYanFloating,
                ChildTypes = new List<InterceptType>()
            };

            // 畅言智慧课堂 场景工具栏（子规则）
            _interceptRules[InterceptType.ChangYanSceneToolbar] = new InterceptRule
            {
                Type = InterceptType.ChangYanSceneToolbar,
                ProcessName = "ClassIn",
                WindowTitlePattern = "SceneToolbar",
                ClassNamePattern = "Qt5QWindowOwnDCIcon",
                IsEnabled = true,
                RequiresAdmin = true,
                Description = "畅言智慧课堂 场景工具栏",
                ParentType = InterceptType.ChangYanFloating,
                ChildTypes = new List<InterceptType>()
            };

            // 畅言智慧课堂 绘制窗口（子规则）
            _interceptRules[InterceptType.ChangYanDrawWindow] = new InterceptRule
            {
                Type = InterceptType.ChangYanDrawWindow,
                ProcessName = "ClassIn",
                WindowTitlePattern = "DrawWindow",
                ClassNamePattern = "Qt5QWindowToolSaveBits",
                IsEnabled = true,
                RequiresAdmin = true,
                Description = "畅言智慧课堂 绘制窗口",
                ParentType = InterceptType.ChangYanFloating,
                ChildTypes = new List<InterceptType>()
            };

            // 畅言智慧课堂 PPT悬浮窗
            _interceptRules[InterceptType.ChangYanPptFloating] = new InterceptRule
            {
                Type = InterceptType.ChangYanPptFloating,
                ProcessName = "ClassIn",
                WindowTitlePattern = "Exch",
                ClassNamePattern = "Qt5QWindowToolSaveBitsOwnDC",
                IsEnabled = true,
                RequiresAdmin = true,
                Description = "畅言智慧课堂 PPT悬浮窗",
                ParentType = null,
                ChildTypes = new List<InterceptType> { InterceptType.ChangYanPptPageControl, InterceptType.ChangYanPptGoBack, InterceptType.ChangYanPptPreview }
            };

            // 畅言智慧课堂 PPT页面控制（子规则）
            _interceptRules[InterceptType.ChangYanPptPageControl] = new InterceptRule
            {
                Type = InterceptType.ChangYanPptPageControl,
                ProcessName = "ClassIn",
                WindowTitlePattern = "PageCtl",
                ClassNamePattern = "Qt5QWindowToolSaveBitsOwnDC",
                IsEnabled = true,
                RequiresAdmin = true,
                Description = "畅言智慧课堂 PPT页面控制",
                ParentType = InterceptType.ChangYanPptFloating,
                ChildTypes = new List<InterceptType>()
            };

            // 畅言智慧课堂 PPT返回（子规则）
            _interceptRules[InterceptType.ChangYanPptGoBack] = new InterceptRule
            {
                Type = InterceptType.ChangYanPptGoBack,
                ProcessName = "ClassIn",
                WindowTitlePattern = "Goback",
                ClassNamePattern = "Qt5QWindowToolSaveBitsOwnDC",
                IsEnabled = true,
                RequiresAdmin = true,
                Description = "畅言智慧课堂 PPT返回",
                ParentType = InterceptType.ChangYanPptFloating,
                ChildTypes = new List<InterceptType>()
            };

            // 畅言智慧课堂 PPT预览（子规则）
            _interceptRules[InterceptType.ChangYanPptPreview] = new InterceptRule
            {
                Type = InterceptType.ChangYanPptPreview,
                ProcessName = "ClassIn",
                WindowTitlePattern = "Preview",
                ClassNamePattern = "Qt5QWindowToolSaveBitsOwnDC",
                IsEnabled = true,
                RequiresAdmin = true,
                Description = "畅言智慧课堂 PPT预览",
                ParentType = InterceptType.ChangYanPptFloating,
                ChildTypes = new List<InterceptType>()
            };

            // 天喻教育云互动课堂 桌面悬浮窗（父规则）
            _interceptRules[InterceptType.IntelligentClassFloating] = new InterceptRule
            {
                Type = InterceptType.IntelligentClassFloating,
                ProcessName = "IntelligentClassApp",
                WindowTitlePattern = "桌面小工具 - 互动课堂",
                ClassNamePattern = "HwndWrapper[IntelligentClassApp.exe;;",
                IsEnabled = true,
                RequiresAdmin = false,
                Description = "天喻教育云互动课堂 桌面悬浮窗（包括PPT控件）",
                ParentType = null,
                ChildTypes = new List<InterceptType> { InterceptType.IntelligentClassPptFloating }
            };

            // 天喻教育云互动课堂 PPT悬浮窗（子规则）
            _interceptRules[InterceptType.IntelligentClassPptFloating] = new InterceptRule
            {
                Type = InterceptType.IntelligentClassPptFloating,
                ProcessName = "IntelligentClass",
                WindowTitlePattern = "",
                ClassNamePattern = "HwndWrapper[IntelligentClass.Office.PowerPoint.vsto|vstolocal;VSTA_Main;",
                IsEnabled = true,
                RequiresAdmin = false,
                Description = "天喻教育云互动课堂 PPT悬浮窗",
                ParentType = InterceptType.IntelligentClassFloating,
                ChildTypes = new List<InterceptType>()
            };

            // 希沃桌面 画笔悬浮窗
            _interceptRules[InterceptType.SeewoDesktopAnnotationFloating] = new InterceptRule
            {
                Type = InterceptType.SeewoDesktopAnnotationFloating,
                ProcessName = "DesktopAnnotation",
                WindowTitlePattern = "",
                ClassNamePattern = "HwndWrapper[DesktopAnnotation.exe;;",
                IsEnabled = true,
                RequiresAdmin = false,
                Description = "希沃桌面 画笔悬浮窗"
            };

            // 希沃桌面 侧栏悬浮窗
            _interceptRules[InterceptType.SeewoDesktopSideBarFloating] = new InterceptRule
            {
                Type = InterceptType.SeewoDesktopSideBarFloating,
                ProcessName = "ResidentSideBar",
                WindowTitlePattern = "ResidentSideBar",
                ClassNamePattern = "HwndWrapper[ResidentSideBar.exe;;",
                IsEnabled = true,
                RequiresAdmin = true,
                Description = "希沃桌面 侧栏悬浮窗"
            };

        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 启动拦截器
        /// </summary>
        public void Start(int scanIntervalMs = 5000)
        {
            if (_isRunning) return;

            _isRunning = true;
            _scanTimer.Change(0, Math.Max(scanIntervalMs, 2000));
        }

        /// <summary>
        /// 停止拦截器
        /// </summary>
        public void Stop()
        {
            if (!_isRunning) return;

            _isRunning = false;
            _scanTimer.Change(Timeout.Infinite, Timeout.Infinite);

            // 恢复所有被拦截的窗口
            RestoreAllWindows();
        }

        /// <summary>
        /// 设置拦截规则
        /// </summary>
        public void SetInterceptRule(InterceptType type, bool enabled)
        {
            if (_interceptRules.ContainsKey(type))
            {
                var rule = _interceptRules[type];
                rule.IsEnabled = enabled;

                // 如果规则被禁用，恢复相关的被拦截窗口
                if (!enabled)
                {
                    RestoreWindowsByType(type);
                }

                // 如果是父规则被禁用，则禁用所有子规则
                if (!enabled && rule.ChildTypes.Count > 0)
                {
                    foreach (var childType in rule.ChildTypes)
                    {
                        if (_interceptRules.ContainsKey(childType))
                        {
                            _interceptRules[childType].IsEnabled = false;
                            RestoreWindowsByType(childType);
                        }
                    }
                }
                // 如果是父规则被启用，则启用所有子规则
                else if (enabled && rule.ChildTypes.Count > 0)
                {
                    foreach (var childType in rule.ChildTypes)
                    {
                        if (_interceptRules.ContainsKey(childType))
                        {
                            _interceptRules[childType].IsEnabled = true;
                        }
                    }
                }
                // 如果是子规则被禁用，检查是否需要禁用父规则
                else if (!enabled && rule.ParentType.HasValue)
                {
                    var parentRule = _interceptRules[rule.ParentType.Value];
                    // 检查是否还有其他启用的子规则
                    bool hasEnabledChildren = parentRule.ChildTypes.Any(childType =>
                        _interceptRules.ContainsKey(childType) && _interceptRules[childType].IsEnabled);

                    // 如果没有启用的子规则，则禁用父规则
                    if (!hasEnabledChildren)
                    {
                        parentRule.IsEnabled = false;
                    }
                }
                // 如果是子规则被启用，则启用父规则
                else if (enabled && rule.ParentType.HasValue)
                {
                    var parentRule = _interceptRules[rule.ParentType.Value];
                    parentRule.IsEnabled = true;
                }
            }
        }

        /// <summary>
        /// 获取拦截规则
        /// </summary>
        public InterceptRule GetInterceptRule(InterceptType type)
        {
            return _interceptRules.ContainsKey(type) ? _interceptRules[type] : null;
        }

        /// <summary>
        /// 获取所有拦截规则
        /// </summary>
        public Dictionary<InterceptType, InterceptRule> GetAllRules()
        {
            return new Dictionary<InterceptType, InterceptRule>(_interceptRules);
        }

        /// <summary>
        /// 获取当前被拦截的窗口数量
        /// </summary>
        public int GetInterceptedWindowsCount()
        {
            return _interceptedWindows.Count;
        }

        /// <summary>
        /// 手动扫描一次
        /// </summary>
        public void ScanOnce()
        {
            ScanForWindows(null);
        }

        /// <summary>
        /// 恢复所有被拦截的窗口
        /// </summary>
        public void RestoreAllWindows()
        {
            var windowsToRestore = new List<IntPtr>(_interceptedWindows.Keys);
            var restoredCount = 0;

            foreach (var hWnd in windowsToRestore)
            {
                if (RestoreWindow(hWnd))
                {
                    restoredCount++;
                }
            }

        }

        /// <summary>
        /// 恢复指定类型的被拦截窗口
        /// </summary>
        public void RestoreWindowsByType(InterceptType type)
        {
            var windowsToRestore = new List<IntPtr>();
            foreach (var kvp in _interceptedWindows)
            {
                if (kvp.Value == type)
                {
                    windowsToRestore.Add(kvp.Key);
                }
            }

            var restoredCount = 0;
            foreach (var hWnd in windowsToRestore)
            {
                if (RestoreWindow(hWnd))
                {
                    restoredCount++;
                }
            }

        }

        /// <summary>
        /// 恢复指定窗口
        /// </summary>
        public bool RestoreWindow(IntPtr hWnd)
        {
            if (!_interceptedWindows.ContainsKey(hWnd)) return false;

            var interceptType = _interceptedWindows[hWnd];

            if (IsWindow(hWnd))
            {
                // 使用多种方法确保窗口恢复显示
                ShowWindow(hWnd, SW_RESTORE);
                ShowWindow(hWnd, SW_SHOW);
                ShowWindow(hWnd, SW_SHOWNORMAL);

                // 将窗口置于前台并显示
                SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_SHOWWINDOW);

                // 强制将窗口带到前台
                BringWindowToTop(hWnd);
                SetForegroundWindow(hWnd);

                _interceptedWindows.Remove(hWnd);

                WindowRestored?.Invoke(this, new WindowRestoredEventArgs
                {
                    WindowHandle = hWnd,
                    InterceptType = interceptType
                });

                return true;
            }

            _interceptedWindows.Remove(hWnd);
            return false;
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 清理无效的窗口句柄
        /// </summary>
        private void CleanupInvalidWindows()
        {
            var invalidWindows = new List<IntPtr>();

            foreach (var kvp in _interceptedWindows)
            {
                var hWnd = kvp.Key;
                if (!IsWindow(hWnd))
                {
                    invalidWindows.Add(hWnd);
                }
            }
            foreach (var hWnd in invalidWindows)
            {
                _interceptedWindows.Remove(hWnd);
            }
        }

        private void ScanForWindows(object state)
        {
            if (!_isRunning) return;

            try
            {
                // 简化的扫描逻辑
                var interceptedCount = 0;
                CleanupInvalidWindows();

                // 重置所有规则的发现状态
                foreach (var rule in _interceptRules.Values)
                {
                    if (rule.IsEnabled)
                    {
                        rule.foundHwnd = false;
                    }
                }

                // 枚举所有窗口
                EnumWindows(EnumWindowsCallback, IntPtr.Zero);

                // 处理找到的窗口
                foreach (var rule in _interceptRules.Values)
                {
                    if (rule.IsEnabled && rule.foundHwnd && rule.outHwnd != IntPtr.Zero)
                    {
                        bool shouldIntercept = !_interceptedWindows.ContainsKey(rule.outHwnd) ||
                                             (_interceptedWindows.ContainsKey(rule.outHwnd) && IsWindowVisible(rule.outHwnd));

                        if (shouldIntercept)
                        {
                            InterceptWindow(rule.outHwnd, rule);
                            interceptedCount++;
                        }
                    }
                }

                // 更新统计
                if (interceptedCount == 0)
                {
                    _consecutiveEmptyScans++;
                }
                else
                {
                    _consecutiveEmptyScans = 0;
                    _lastSuccessfulScan = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"扫描窗口时发生错误: {ex.Message}", LogHelper.LogType.Error);
                _consecutiveEmptyScans++;
            }
        }


        private bool EnumWindowsCallback(IntPtr hWnd, IntPtr lParam)
        {
            try
            {
                // 递归枚举子窗口
                EnumChildWindows(hWnd, EnumWindowsCallback, lParam);

                // 基本检查
                if (!IsWindow(hWnd) || !IsWindowVisible(hWnd)) return true;

                // 检查每个启用的规则
                foreach (var rule in _interceptRules.Values)
                {
                    if (!rule.IsEnabled || rule.foundHwnd) continue;

                    if (MatchesRulePrecise(hWnd, rule))
                    {
                        rule.outHwnd = hWnd;
                        rule.foundHwnd = true;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"枚举窗口回调错误: {ex.Message}", LogHelper.LogType.Error);
                return true;
            }
        }

        private WindowInfo GetWindowInfo(IntPtr hWnd)
        {
            try
            {
                // 获取进程ID
                GetWindowThreadProcessId(hWnd, out uint processId);
                if (processId == 0) return null;

                // 获取进程信息
                var process = Process.GetProcessById((int)processId);
                if (process == null) return null;

                // 获取窗口标题
                var titleBuilder = new StringBuilder(256);
                GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);

                // 获取窗口类名
                var classBuilder = new StringBuilder(256);
                GetClassName(hWnd, classBuilder, classBuilder.Capacity);

                return new WindowInfo
                {
                    Handle = hWnd,
                    ProcessId = processId,
                    ProcessName = process.ProcessName,
                    WindowTitle = titleBuilder.ToString(),
                    ClassName = classBuilder.ToString(),
                    Process = process
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 精确匹配规则
        /// </summary>
        private bool MatchesRulePrecise(IntPtr hWnd, InterceptRule rule)
        {
            try
            {
                // 检查类名
                if (!string.IsNullOrEmpty(rule.ClassNamePattern))
                {
                    var className = new StringBuilder(256);
                    GetClassName(hWnd, className, className.Capacity);
                    var classNameStr = className.ToString();

                    if (rule.ExactClassNameMatch)
                    {
                        if (!classNameStr.Equals(rule.ClassNamePattern, StringComparison.OrdinalIgnoreCase))
                            return false;
                    }
                    else
                    {
                        if (!classNameStr.Contains(rule.ClassNamePattern))
                            return false;
                    }
                }

                // 检查窗口标题
                if (!string.IsNullOrEmpty(rule.WindowTitlePattern))
                {
                    var windowTitle = new StringBuilder(256);
                    GetWindowText(hWnd, windowTitle, windowTitle.Capacity);
                    var titleStr = windowTitle.ToString();

                    if (rule.ExactTitleMatch)
                    {
                        if (!titleStr.Equals(rule.WindowTitlePattern, StringComparison.OrdinalIgnoreCase))
                            return false;
                    }
                    else
                    {
                        if (!titleStr.Contains(rule.WindowTitlePattern))
                            return false;
                    }
                }

                // 检查窗口样式
                if (rule.HasWindowStyle)
                {
                    var style = GetWindowLong(hWnd, GWL_STYLE);
                    if (style != rule.WindowStyle)
                        return false;
                }

                // 检查窗口尺寸
                if (rule.HasWindowSize)
                {
                    var rect = new ForegroundWindowInfo.RECT();
                    if (DwmGetWindowAttribute(hWnd, DWMWA_EXTENDED_FRAME_BOUNDS, out rect, Marshal.SizeOf(rect)) == 0)
                    {
                        var width = rect.Right - rect.Left;
                        var height = rect.Bottom - rect.Top;

                        // 检查精确匹配
                        if (rule.WindowWidth == width && rule.WindowHeight == height)
                            return true;

                        // 检查缩放匹配
                        var hdc = GetDC(IntPtr.Zero);
                        var horizontalDPI = GetDeviceCaps(hdc, LOGPIXELSX);
                        var verticalDPI = GetDeviceCaps(hdc, LOGPIXELSY);
                        ReleaseDC(IntPtr.Zero, hdc);

                        var scale = (horizontalDPI + verticalDPI) / 2.0f / 96.0f;
                        var scaledWidth = (int)(rule.WindowWidth * scale);
                        var scaledHeight = (int)(rule.WindowHeight * scale);

                        if (Math.Abs(scaledWidth - width) <= 1 && Math.Abs(scaledHeight - height) <= 1)
                            return true;

                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"精确匹配规则时发生错误: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
        }

        private bool MatchesRule(WindowInfo windowInfo, InterceptRule rule)
        {
            return MatchesRulePrecise(windowInfo.Handle, rule);
        }

        private void InterceptWindow(IntPtr hWnd, InterceptRule rule)
        {
            try
            {
                if (!IsWindow(hWnd) || !IsWindowVisible(hWnd))
                {
                    if (_interceptedWindows.ContainsKey(hWnd))
                    {
                        _interceptedWindows.Remove(hWnd);
                    }
                    return;
                }

                // 直接隐藏窗口，不发送关闭消息
                ShowWindow(hWnd, SW_HIDE);

                // 记录拦截的窗口
                _interceptedWindows[hWnd] = rule.Type;

                // 触发事件
                WindowIntercepted?.Invoke(this, new WindowInterceptedEventArgs
                {
                    WindowHandle = hWnd,
                    InterceptType = rule.Type,
                    Rule = rule,
                    WindowTitle = GetWindowTitle(hWnd)
                });

            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"拦截窗口时发生错误: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private string GetWindowTitle(IntPtr hWnd)
        {
            try
            {
                var titleBuilder = new StringBuilder(256);
                GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);
                return titleBuilder.ToString();
            }
            catch
            {
                return "Unknown";
            }
        }

        private bool IsMainWindow(IntPtr hWnd)
        {
            try
            {
                // 检查是否有父窗口
                var parent = GetWindow(hWnd, 4); // GW_OWNER
                if (parent != IntPtr.Zero) return false;

                // 检查窗口样式
                var style = GetWindowLong(hWnd, -16); // GWL_STYLE
                var exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);

                // 主窗口通常有 WS_CAPTION 和 WS_SYSMENU
                const uint WS_CAPTION = 0x00C00000;
                const uint WS_SYSMENU = 0x00080000;

                if ((style & WS_CAPTION) != 0 && (style & WS_SYSMENU) != 0)
                {
                    return true; // 这可能是主窗口
                }

                // 检查窗口大小，主窗口通常比较大
                var rect = new ForegroundWindowInfo.RECT();
                GetWindowRect(hWnd, out rect);
                var width = rect.Right - rect.Left;
                var height = rect.Bottom - rect.Top;

                // 如果窗口很大，可能是主窗口
                if (width > 800 && height > 600)
                {
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region 辅助类

        private class WindowInfo
        {
            public IntPtr Handle { get; set; }
            public uint ProcessId { get; set; }
            public string ProcessName { get; set; }
            public string WindowTitle { get; set; }
            public string ClassName { get; set; }
            public Process Process { get; set; }
        }

        #endregion

        #region 事件参数类

        public class WindowInterceptedEventArgs : EventArgs
        {
            public IntPtr WindowHandle { get; set; }
            public InterceptType InterceptType { get; set; }
            public InterceptRule Rule { get; set; }
            public string WindowTitle { get; set; }
        }

        public class WindowRestoredEventArgs : EventArgs
        {
            public IntPtr WindowHandle { get; set; }
            public InterceptType InterceptType { get; set; }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed) return;

            Stop();
            _scanTimer?.Dispose();

            // 恢复所有被拦截的窗口
            RestoreAllWindows();

            _disposed = true;
        }

        #endregion
    }
}