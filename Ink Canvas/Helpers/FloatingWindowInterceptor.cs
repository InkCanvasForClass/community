using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
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
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hWnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern bool QueryFullProcessImageNameW(IntPtr hProcess, uint dwFlags, StringBuilder lpExeName, ref uint lpdwSize);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private const int SW_HIDE = 0;
        private const int SW_SHOWNOACTIVATE = 4;

        private const int GWL_STYLE = -16;

        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;

        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        private const uint WM_CLOSE = 0x0010;
        private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
        private const int LOGPIXELSX = 88;
        private const int LOGPIXELSY = 90;
        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        #endregion

        #region 拦截规则定义

        public enum InterceptType
        {
            // 希沃系列
            SeewoWhiteboard3Floating,
            SeewoWhiteboard5Floating,
            SeewoWhiteboard5CFloating,
            SeewoPincoSideBarFloating,
            SeewoPincoDrawingFloating,
            SeewoPincoBoardService,
            SeewoPPTFloating,
            SeewoIwbAssistantFloating,
            SeewoDesktopSideBarFloating,
            SeewoDesktopDrawingFloating,
            // 欧帝
            YiouBoardFloating,
            // AiClass
            AiClassFloating,
            // ClassIn X
            ClassInXFloating,
            // 鸿合
            HiteAnnotationFloating,
            // 畅言4.0
            ChangYanFloating,
            ChangYanBrushSettings,
            ChangYanSwipeClear,
            ChangYanInteraction,
            ChangYanSubjectApp,
            ChangYanControl,
            ChangYanCommonTools,
            ChangYanSceneToolbar,
            ChangYanDrawWindow,
            ChangYanPptFloating,
            ChangYanPptPageControl,
            ChangYanPptGoBack,
            ChangYanPptPreview,
            // 畅言5.0
            ChangYan5Floating,
            // 天喻
            IntelligentClassFloating,
            IntelligentClassPptFloating,
            // C30智能教学
            Iclass30SidebarFloating,
            Iclass30Floating,
            // 保留（已被SeewoDesktopDrawingFloating替代，但保持向后兼容）
            SeewoDesktopAnnotationFloating,
        }

        public enum StyleMatchType { Exact, Subset }

        public enum SizeMatchType { None, Exact, DPIScale, Scale, FullScreen, FullHeight, FullWidth }

        public class InterceptRule
        {
            public InterceptType Type { get; set; }
            public string ProcessName { get; set; }
            public string WindowTitlePattern { get; set; }
            public bool TitleIsRegex { get; set; } = false;
            public string ClassNamePattern { get; set; }
            public bool ClassNameIsRegex { get; set; } = true;
            public bool IsEnabled { get; set; }
            public bool RequiresAdmin { get; set; }
            public string Description { get; set; }
            public InterceptType? ParentType { get; set; }
            public List<InterceptType> ChildTypes { get; set; } = new List<InterceptType>();

            public bool HasWindowStyle { get; set; }
            public uint WindowStyle { get; set; }
            public StyleMatchType StyleMatchType { get; set; } = StyleMatchType.Exact;

            public SizeMatchType SizeMatchType { get; set; } = SizeMatchType.None;
            public int WindowWidth { get; set; }
            public int WindowHeight { get; set; }

            // Action type (Close vs Hide vs Move)
            public InterceptActionType ActionType { get; set; } = InterceptActionType.Hide;

            // 运行时扫描状态（每次扫描重置）
            public bool foundHwnd { get; set; } = false;
            public IntPtr outHwnd { get; set; } = IntPtr.Zero;
        }

        public enum InterceptActionType { Hide, Close, Move }

        #endregion

        #region 私有字段

        private readonly Dictionary<InterceptType, InterceptRule> _interceptRules;
        private readonly Dictionary<IntPtr, InterceptType> _interceptedWindows;
        private readonly Dictionary<IntPtr, (int x, int y)> _movedWindowPositions;
        private readonly Timer _scanTimer;
        private readonly Dispatcher _dispatcher;
        private bool _isRunning;
        private bool _disposed;

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
            _movedWindowPositions = new Dictionary<IntPtr, (int, int)>();
            _dispatcher = Dispatcher.CurrentDispatcher;

            InitializeRules();
            _scanTimer = new Timer(ScanForWindows, null, Timeout.Infinite, Timeout.Infinite);
        }

        #endregion

        #region 初始化规则

        private void InitializeRules()
        {
            // ─── 希沃白板3 ───────────────────────────────────────────────────────────
            AddRule(new InterceptRule
            {
                Type = InterceptType.SeewoWhiteboard3Floating,
                ProcessName = "EasiNote.exe",
                WindowTitlePattern = "^Note$",
                TitleIsRegex = true,
                ClassNamePattern = @"HwndWrapper\[EasiNote\.exe;;[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\]",
                ClassNameIsRegex = true,
                HasWindowStyle = true,
                WindowStyle = 0x16CF0000, // WS_CLIPSIBLINGS|WS_CLIPCHILDREN|WS_SYSMENU|WS_THICKFRAME|WS_GROUP|WS_TABSTOP|WS_MINIMIZEBOX|WS_MAXIMIZEBOX
                StyleMatchType = StyleMatchType.Exact,
                SizeMatchType = SizeMatchType.FullScreen,
                IsEnabled = true,
                Description = "希沃白板3 桌面悬浮窗"
            });

            // ─── 希沃白板5 ───────────────────────────────────────────────────────────
            AddRule(new InterceptRule
            {
                Type = InterceptType.SeewoWhiteboard5Floating,
                ProcessName = "EasiNote.exe",
                WindowTitlePattern = "",
                ClassNamePattern = @"HwndWrapper\[EasiNote;;[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\]",
                ClassNameIsRegex = true,
                HasWindowStyle = true,
                WindowStyle = 0x16080000, // WS_CLIPSIBLINGS|WS_CLIPCHILDREN|WS_SYSMENU
                StyleMatchType = StyleMatchType.Exact,
                SizeMatchType = SizeMatchType.FullScreen,
                IsEnabled = true,
                Description = "希沃白板5 桌面悬浮窗"
            });

            // ─── 希沃白板5C ──────────────────────────────────────────────────────────
            AddRule(new InterceptRule
            {
                Type = InterceptType.SeewoWhiteboard5CFloating,
                ProcessName = "EasiNote5C.exe",
                WindowTitlePattern = "",
                ClassNamePattern = @"HwndWrapper\[EasiNote5C;;[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\]",
                ClassNameIsRegex = true,
                HasWindowStyle = true,
                WindowStyle = 0x16080000,
                StyleMatchType = StyleMatchType.Subset,
                SizeMatchType = SizeMatchType.FullScreen,
                IsEnabled = true,
                Description = "希沃白板5C 桌面悬浮窗"
            });

            // ─── 希沃品课 侧栏（父规则）────────────────────────────────────────────
            AddRule(new InterceptRule
            {
                Type = InterceptType.SeewoPincoSideBarFloating,
                ProcessName = "seewoPincoTeacher.exe",
                WindowTitlePattern = "^希沃品课——appBar$",
                TitleIsRegex = true,
                ClassNamePattern = "^Chrome_WidgetWin_1$",
                ClassNameIsRegex = true,
                HasWindowStyle = true,
                WindowStyle = 0x04440000, // WS_CLIPSIBLINGS|WS_GROUP|WS_MINIMIZEBOX
                StyleMatchType = StyleMatchType.Exact,
                SizeMatchType = SizeMatchType.None,
                ActionType = InterceptActionType.Close,
                IsEnabled = true,
                Description = "希沃品课教师端 侧栏悬浮窗",
                ChildTypes = new List<InterceptType> { InterceptType.SeewoPincoDrawingFloating, InterceptType.SeewoPincoBoardService }
            });

            // ─── 希沃品课 画笔（子规则）────────────────────────────────────────────
            AddRule(new InterceptRule
            {
                Type = InterceptType.SeewoPincoDrawingFloating,
                ProcessName = "seewoPincoTeacher.exe",
                WindowTitlePattern = "^希沃品课——integration$",
                TitleIsRegex = true,
                ClassNamePattern = "^Chrome_WidgetWin_1$",
                ClassNameIsRegex = true,
                HasWindowStyle = true,
                WindowStyle = 0x04440000,
                StyleMatchType = StyleMatchType.Exact,
                SizeMatchType = SizeMatchType.FullScreen,
                ActionType = InterceptActionType.Close,
                IsEnabled = true,
                Description = "希沃品课教师端 画笔悬浮窗（包括PPT控件）",
                ParentType = InterceptType.SeewoPincoSideBarFloating
            });

            // ─── 希沃品课 桌面画板（子规则）─────────────────────────────────────────
            AddRule(new InterceptRule
            {
                Type = InterceptType.SeewoPincoBoardService,
                ProcessName = "BoardService.exe",
                WindowTitlePattern = "",
                ClassNamePattern = @"HwndWrapper\[BoardService;;[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\]",
                ClassNameIsRegex = true,
                HasWindowStyle = true,
                WindowStyle = 0x16080000,
                StyleMatchType = StyleMatchType.Exact,
                SizeMatchType = SizeMatchType.FullScreen,
                ActionType = InterceptActionType.Close,
                IsEnabled = true,
                Description = "希沃品课教师端 桌面画板",
                ParentType = InterceptType.SeewoPincoSideBarFloating
            });

            // ─── 希沃PPT小工具 ───────────────────────────────────────────────────────
            AddRule(new InterceptRule
            {
                Type = InterceptType.SeewoPPTFloating,
                ProcessName = "PPTService.exe",
                WindowTitlePattern = "",
                ClassNamePattern = @"HwndWrapper\[PPTService\.exe;;[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\]",
                ClassNameIsRegex = true,
                HasWindowStyle = true,
                WindowStyle = 0x16080000,
                StyleMatchType = StyleMatchType.Subset,
                SizeMatchType = SizeMatchType.FullScreen,
                IsEnabled = true,
                Description = "希沃PPT小工具"
            });

            // ─── 希沃课堂助手 ────────────────────────────────────────────────────────
            AddRule(new InterceptRule
            {
                Type = InterceptType.SeewoIwbAssistantFloating,
                ProcessName = "SeewoIwbAssistant.exe",
                WindowTitlePattern = "",
                ClassNamePattern = @"HwndWrapper\[SeewoIwbAssistant;;[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\]",
                ClassNameIsRegex = true,
                HasWindowStyle = true,
                WindowStyle = 0x16080000,
                StyleMatchType = StyleMatchType.Subset,
                SizeMatchType = SizeMatchType.FullScreen,
                IsEnabled = true,
                Description = "希沃课堂助手"
            });

            // ─── 希沃桌面 侧栏 ───────────────────────────────────────────────────────
            AddRule(new InterceptRule
            {
                Type = InterceptType.SeewoDesktopSideBarFloating,
                ProcessName = "ResidentSideBar.exe",
                WindowTitlePattern = "^ResidentSideBar$",
                TitleIsRegex = true,
                ClassNamePattern = @"HwndWrapper\[ResidentSideBar\.exe;;[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\]",
                ClassNameIsRegex = true,
                HasWindowStyle = true,
                WindowStyle = 0x17080000, // WS_CLIPSIBLINGS|WS_CLIPCHILDREN|WS_MAXIMIZE|WS_SYSMENU
                StyleMatchType = StyleMatchType.Exact,
                SizeMatchType = SizeMatchType.FullScreen,
                IsEnabled = true,
                RequiresAdmin = true,
                Description = "希沃桌面 侧栏悬浮窗"
            });

            // ─── 希沃桌面 画笔 ───────────────────────────────────────────────────────
            // (旧名 SeewoDesktopAnnotationFloating 已合并到此条，保留向后兼容枚举)
            AddRule(new InterceptRule
            {
                Type = InterceptType.SeewoDesktopDrawingFloating,
                ProcessName = "DesktopAnnotation.exe",
                WindowTitlePattern = "",
                ClassNamePattern = @"HwndWrapper\[DesktopAnnotation(\.exe)?;;[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\]",
                ClassNameIsRegex = true,
                HasWindowStyle = true,
                WindowStyle = 0x16080000,
                StyleMatchType = StyleMatchType.Exact,
                SizeMatchType = SizeMatchType.FullScreen,
                IsEnabled = true,
                Description = "希沃桌面 画笔悬浮窗"
            });

            // 向后兼容：SeewoDesktopAnnotationFloating 指向相同规则逻辑，默认禁用（使用新条目）
            AddRule(new InterceptRule
            {
                Type = InterceptType.SeewoDesktopAnnotationFloating,
                ProcessName = "DesktopAnnotation.exe",
                WindowTitlePattern = "",
                ClassNamePattern = @"HwndWrapper\[DesktopAnnotation(\.exe)?;;[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\]",
                ClassNameIsRegex = true,
                HasWindowStyle = true,
                WindowStyle = 0x16080000,
                StyleMatchType = StyleMatchType.Exact,
                SizeMatchType = SizeMatchType.FullScreen,
                IsEnabled = false,
                Description = "希沃桌面 画笔悬浮窗（旧）"
            });

            // ─── 欧帝白板 ─────────────────────────────────────────────────────────────
            AddRule(new InterceptRule
            {
                Type = InterceptType.YiouBoardFloating,
                ProcessName = "WriteBoard.exe",
                WindowTitlePattern = "^WinYODesktop$",
                TitleIsRegex = true,
                ClassNamePattern = @"HwndWrapper\[WriteBoard\.exe;;[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\]",
                ClassNameIsRegex = true,
                HasWindowStyle = true,
                WindowStyle = 0x16080000,
                StyleMatchType = StyleMatchType.Subset,
                SizeMatchType = SizeMatchType.DPIScale,
                WindowWidth = 780,
                WindowHeight = 60,
                IsEnabled = true,
                RequiresAdmin = true,
                Description = "欧帝白板 悬浮窗"
            });

            // ─── AiClass ──────────────────────────────────────────────────────────────
            AddRule(new InterceptRule
            {
                Type = InterceptType.AiClassFloating,
                ProcessName = "wisdom_class.exe",
                WindowTitlePattern = "^TransparentWindow$",
                TitleIsRegex = true,
                ClassNamePattern = "^UIWndTransparent$",
                ClassNameIsRegex = true,
                HasWindowStyle = true,
                WindowStyle = 0x82000000, // WS_POPUP|WS_CLIPSIBLINGS
                StyleMatchType = StyleMatchType.Subset,
                SizeMatchType = SizeMatchType.FullScreen,
                IsEnabled = true,
                Description = "AiClass 桌面悬浮窗"
            });

            // ─── ClassIn X ────────────────────────────────────────────────────────────
            AddRule(new InterceptRule
            {
                Type = InterceptType.ClassInXFloating,
                ProcessName = "ClassIn X.exe",
                WindowTitlePattern = "^ClassIn X$",
                TitleIsRegex = true,
                ClassNamePattern = "^Qt5151QWindowIcon$",
                ClassNameIsRegex = true,
                HasWindowStyle = true,
                WindowStyle = 0x86000000, // WS_POPUP|WS_CLIPSIBLINGS|WS_CLIPCHILDREN
                StyleMatchType = StyleMatchType.Subset,
                SizeMatchType = SizeMatchType.FullScreen,
                IsEnabled = true,
                Description = "ClassIn X 桌面悬浮窗"
            });

            // ─── 鸿合屏幕书写 ────────────────────────────────────────────────────────
            AddRule(new InterceptRule
            {
                Type = InterceptType.HiteAnnotationFloating,
                ProcessName = "HiteVision.exe",
                WindowTitlePattern = "^HiteAnnotation$",
                TitleIsRegex = true,
                ClassNamePattern = "^Qt5QWindowToolSaveBits$",
                ClassNameIsRegex = true,
                IsEnabled = true,
                Description = "鸿合屏幕书写"
            });

            // ─── 畅言4.0 父规则 ──────────────────────────────────────────────────────
            AddRule(new InterceptRule
            {
                Type = InterceptType.ChangYanFloating,
                ProcessName = "ifly.qzk.Toolbar.exe",
                WindowTitlePattern = "^DrawWindow$",
                TitleIsRegex = true,
                ClassNamePattern = "^Qt5QWindowToolSaveBits$",
                ClassNameIsRegex = true,
                HasWindowStyle = true,
                WindowStyle = 0x86000000,
                StyleMatchType = StyleMatchType.Subset,
                SizeMatchType = SizeMatchType.FullScreen,
                IsEnabled = true,
                RequiresAdmin = true,
                Description = "畅言智慧课堂4.0 绘制窗口",
                ChildTypes = new List<InterceptType>
                {
                    InterceptType.ChangYanBrushSettings,
                    InterceptType.ChangYanSwipeClear,
                    InterceptType.ChangYanInteraction,
                    InterceptType.ChangYanSubjectApp,
                    InterceptType.ChangYanControl,
                    InterceptType.ChangYanCommonTools,
                    InterceptType.ChangYanSceneToolbar,
                    InterceptType.ChangYanDrawWindow,
                }
            });

            // 畅言4.0 子规则
            AddChangYan4ChildRule(InterceptType.ChangYanBrushSettings, "^画笔设置$", "^Qt5QWindowOwnDCIcon$", "畅言4.0 画笔设置");
            AddChangYan4ChildRule(InterceptType.ChangYanSwipeClear, "^滑动清除$", "^Qt5QWindowOwnDCIcon$", "畅言4.0 滑动清除");
            AddChangYan4ChildRule(InterceptType.ChangYanInteraction, "^互动$", "^Qt5QWindowOwnDCIcon$", "畅言4.0 互动");
            AddChangYan4ChildRule(InterceptType.ChangYanSubjectApp, "^学科应用$", "^Qt5QWindowOwnDCIcon$", "畅言4.0 学科应用");
            AddChangYan4ChildRule(InterceptType.ChangYanControl, "^管控$", "^Qt5QWindowOwnDCIcon$", "畅言4.0 管控");
            AddChangYan4ChildRule(InterceptType.ChangYanCommonTools, "^通用工具$", "^Qt5QWindowOwnDCIcon$", "畅言4.0 通用工具");
            AddChangYan4ChildRule(InterceptType.ChangYanSceneToolbar, "^SceneToolbar$", "^Qt5QWindowOwnDCIcon$", "畅言4.0 场景工具栏");
            AddChangYan4ChildRule(InterceptType.ChangYanDrawWindow, "^DrawWindow$", "^Qt5QWindowToolSaveBits$", "畅言4.0 绘制窗口（子）");

            // ─── 畅言4.0 PPT 悬浮窗 ─────────────────────────────────────────────────
            AddRule(new InterceptRule
            {
                Type = InterceptType.ChangYanPptFloating,
                ProcessName = "ifly.qzk.Toolbar.exe",
                WindowTitlePattern = "Exch",
                ClassNamePattern = "^Qt5QWindowToolSaveBitsOwnDC$",
                ClassNameIsRegex = true,
                IsEnabled = true,
                RequiresAdmin = true,
                Description = "畅言4.0 PPT悬浮窗",
                ChildTypes = new List<InterceptType> { InterceptType.ChangYanPptPageControl, InterceptType.ChangYanPptGoBack, InterceptType.ChangYanPptPreview }
            });
            AddChangYan4PptChildRule(InterceptType.ChangYanPptPageControl, "PageCtl", "畅言4.0 PPT页面控制");
            AddChangYan4PptChildRule(InterceptType.ChangYanPptGoBack, "Goback", "畅言4.0 PPT返回");
            AddChangYan4PptChildRule(InterceptType.ChangYanPptPreview, "Preview", "畅言4.0 PPT预览");

            // ─── 畅言5.0 ─────────────────────────────────────────────────────────────
            AddRule(new InterceptRule
            {
                Type = InterceptType.ChangYan5Floating,
                ProcessName = "ifly.qzk.Toolbar.exe",
                WindowTitlePattern = "^DrawWindow$",
                TitleIsRegex = true,
                ClassNamePattern = "^Qt5152QWindowIcon$",
                ClassNameIsRegex = true,
                HasWindowStyle = true,
                WindowStyle = 0x86000000,
                StyleMatchType = StyleMatchType.Subset,
                SizeMatchType = SizeMatchType.FullScreen,
                IsEnabled = true,
                RequiresAdmin = true,
                Description = "畅言智慧课堂5.0 绘制窗口"
            });

            // ─── 天喻互动课堂 父规则 ──────────────────────────────────────────────────
            AddRule(new InterceptRule
            {
                Type = InterceptType.IntelligentClassFloating,
                ProcessName = "IntelligentClassApp.exe",
                WindowTitlePattern = "^桌面小工具 - 互动课堂$",
                TitleIsRegex = true,
                ClassNamePattern = @"HwndWrapper\[IntelligentClassApp\.exe;;[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\]",
                ClassNameIsRegex = true,
                HasWindowStyle = true,
                WindowStyle = 0x17080000,
                StyleMatchType = StyleMatchType.Subset,
                SizeMatchType = SizeMatchType.FullScreen,
                IsEnabled = true,
                Description = "天喻互动课堂 桌面悬浮窗",
                ChildTypes = new List<InterceptType> { InterceptType.IntelligentClassPptFloating }
            });

            // ─── 天喻互动课堂 PPT 子规则 ──────────────────────────────────────────────
            AddRule(new InterceptRule
            {
                Type = InterceptType.IntelligentClassPptFloating,
                ProcessName = "IntelligentClass.Office.PowerPoint.vsto|vstolocal",
                WindowTitlePattern = "",
                ClassNamePattern = @"HwndWrapper\[IntelligentClass\.Office\.PowerPoint\.vsto\|vstolocal;VSTA_Main;[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\]",
                ClassNameIsRegex = true,
                HasWindowStyle = true,
                WindowStyle = 0x86000000,
                StyleMatchType = StyleMatchType.Subset,
                SizeMatchType = SizeMatchType.FullScreen,
                IsEnabled = true,
                Description = "天喻互动课堂 PPT悬浮窗",
                ParentType = InterceptType.IntelligentClassFloating
            });

            // ─── C30 侧栏（默认关闭）────────────────────────────────────────────────
            AddRule(new InterceptRule
            {
                Type = InterceptType.Iclass30SidebarFloating,
                ProcessName = "teach.exe",
                WindowTitlePattern = "^控制工具条$",
                TitleIsRegex = true,
                ClassNamePattern = "^ctrltool$",
                ClassNameIsRegex = true,
                HasWindowStyle = true,
                WindowStyle = 0x86880000, // WS_POPUP|WS_CLIPSIBLINGS|WS_CLIPCHILDREN|WS_SYSMENU
                StyleMatchType = StyleMatchType.Subset,
                ActionType = InterceptActionType.Move,
                IsEnabled = false,
                Description = "C30智能教学 侧栏悬浮窗"
            });

            // ─── C30 画笔工具 ─────────────────────────────────────────────────────────
            AddRule(new InterceptRule
            {
                Type = InterceptType.Iclass30Floating,
                ProcessName = "teach.exe",
                WindowTitlePattern = "^工具栏$",
                TitleIsRegex = true,
                ClassNamePattern = "^toolview$",
                ClassNameIsRegex = true,
                HasWindowStyle = true,
                WindowStyle = 0x86880000,
                StyleMatchType = StyleMatchType.Subset,
                ActionType = InterceptActionType.Move,
                IsEnabled = true,
                Description = "C30智能教学 画笔工具栏"
            });
        }

        private void AddRule(InterceptRule rule)
        {
            _interceptRules[rule.Type] = rule;
        }

        private void AddChangYan4ChildRule(InterceptType type, string titlePattern, string classPattern, string desc)
        {
            AddRule(new InterceptRule
            {
                Type = type,
                ProcessName = "ifly.qzk.Toolbar.exe",
                WindowTitlePattern = titlePattern,
                TitleIsRegex = true,
                ClassNamePattern = classPattern,
                ClassNameIsRegex = true,
                IsEnabled = true,
                RequiresAdmin = true,
                Description = desc,
                ParentType = InterceptType.ChangYanFloating
            });
        }

        private void AddChangYan4PptChildRule(InterceptType type, string titlePattern, string desc)
        {
            AddRule(new InterceptRule
            {
                Type = type,
                ProcessName = "ifly.qzk.Toolbar.exe",
                WindowTitlePattern = titlePattern,
                ClassNamePattern = "^Qt5QWindowToolSaveBitsOwnDC$",
                ClassNameIsRegex = true,
                IsEnabled = true,
                RequiresAdmin = true,
                Description = desc,
                ParentType = InterceptType.ChangYanPptFloating
            });
        }

        #endregion

        #region 公共方法

        public void Start(int scanIntervalMs = 5000)
        {
            if (_isRunning) return;
            _isRunning = true;
            _scanTimer.Change(0, Math.Max(scanIntervalMs, 2000));
        }

        public void Stop()
        {
            if (!_isRunning) return;
            _isRunning = false;
            _scanTimer.Change(Timeout.Infinite, Timeout.Infinite);
            RestoreAllWindows();
        }

        public void SetInterceptRule(InterceptType type, bool enabled)
        {
            if (!_interceptRules.ContainsKey(type)) return;

            var rule = _interceptRules[type];
            rule.IsEnabled = enabled;

            if (!enabled) RestoreWindowsByType(type);

            if (!enabled && rule.ChildTypes.Count > 0)
            {
                foreach (var childType in rule.ChildTypes)
                    if (_interceptRules.ContainsKey(childType))
                    {
                        _interceptRules[childType].IsEnabled = false;
                        RestoreWindowsByType(childType);
                    }
            }
            else if (enabled && rule.ChildTypes.Count > 0)
            {
                foreach (var childType in rule.ChildTypes)
                    if (_interceptRules.ContainsKey(childType))
                        _interceptRules[childType].IsEnabled = true;
            }
            else if (!enabled && rule.ParentType.HasValue)
            {
                var parentRule = _interceptRules[rule.ParentType.Value];
                bool hasEnabledChildren = parentRule.ChildTypes.Any(ct =>
                    _interceptRules.ContainsKey(ct) && _interceptRules[ct].IsEnabled);
                if (!hasEnabledChildren)
                    parentRule.IsEnabled = false;
            }
            else if (enabled && rule.ParentType.HasValue)
            {
                _interceptRules[rule.ParentType.Value].IsEnabled = true;
            }
        }

        public InterceptRule GetInterceptRule(InterceptType type)
        {
            return _interceptRules.ContainsKey(type) ? _interceptRules[type] : null;
        }

        public Dictionary<InterceptType, InterceptRule> GetAllRules()
        {
            return new Dictionary<InterceptType, InterceptRule>(_interceptRules);
        }

        public int GetInterceptedWindowsCount() => _interceptedWindows.Count;

        public void ScanOnce() => ScanForWindows(null);

        public void RestoreAllWindows()
        {
            foreach (var hWnd in new List<IntPtr>(_interceptedWindows.Keys))
                RestoreWindow(hWnd);
        }

        public void RestoreWindowsByType(InterceptType type)
        {
            foreach (var hWnd in _interceptedWindows.Where(kvp => kvp.Value == type).Select(kvp => kvp.Key).ToList())
                RestoreWindow(hWnd);
        }

        public bool RestoreWindow(IntPtr hWnd)
        {
            if (!_interceptedWindows.ContainsKey(hWnd)) return false;

            var interceptType = _interceptedWindows[hWnd];

            if (IsWindow(hWnd))
            {
                if (_movedWindowPositions.TryGetValue(hWnd, out var pos))
                {
                    SetWindowPos(hWnd, IntPtr.Zero, pos.x, pos.y, 0, 0,
                        SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
                    _movedWindowPositions.Remove(hWnd);
                }
                else
                {
                    ShowWindow(hWnd, SW_SHOWNOACTIVATE);
                }

                _interceptedWindows.Remove(hWnd);
                WindowRestored?.Invoke(this, new WindowRestoredEventArgs
                {
                    WindowHandle = hWnd,
                    InterceptType = interceptType
                });
                return true;
            }

            _interceptedWindows.Remove(hWnd);
            _movedWindowPositions.Remove(hWnd);
            return false;
        }

        #endregion

        #region 私有方法

        private void CleanupInvalidWindows()
        {
            foreach (var hWnd in _interceptedWindows.Keys.Where(h => !IsWindow(h)).ToList())
            {
                _interceptedWindows.Remove(hWnd);
                _movedWindowPositions.Remove(hWnd);
            }
        }

        private void ScanForWindows(object state)
        {
            if (!_isRunning) return;

            try
            {
                CleanupInvalidWindows();

                var foundWindows = new List<(IntPtr hwnd, InterceptRule rule)>();

                EnumWindows((hWnd, lParam) =>
                {
                    EnumChildWindows(hWnd, (childHwnd, _) =>
                    {
                        CheckWindow(childHwnd, foundWindows);
                        return true;
                    }, IntPtr.Zero);
                    CheckWindow(hWnd, foundWindows);
                    return true;
                }, IntPtr.Zero);

                var interceptedCount = 0;
                foreach (var (hWnd, rule) in foundWindows)
                {
                    // 已被隐藏且仍不可见：已拦截，无需重复操作
                    if (_interceptedWindows.ContainsKey(hWnd) && !IsWindowVisible(hWnd))
                        continue;
                    ApplyIntercept(hWnd, rule);
                    interceptedCount++;
                }

                if (interceptedCount == 0) _consecutiveEmptyScans++;
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

        private void CheckWindow(IntPtr hWnd, List<(IntPtr, InterceptRule)> found)
        {
            // 必须是合法窗口句柄，但不要求可见（被我们隐藏的窗口也需要被发现以维持拦截状态）
            if (!IsWindow(hWnd)) return;

            foreach (var rule in _interceptRules.Values)
            {
                if (!rule.IsEnabled) continue;
                if (MatchesRule(hWnd, rule))
                {
                    found.Add((hWnd, rule));
                    break; // 一个窗口只匹配第一条命中的规则
                }
            }
        }
<<<END_W2P:old_string:h4e39a0fd>>>
END_CALL

        private bool MatchesRule(IntPtr hWnd, InterceptRule rule)
        {
            try
            {
                // 检查类名
                if (!string.IsNullOrEmpty(rule.ClassNamePattern))
                {
                    var sb = new StringBuilder(512);
                    GetClassName(hWnd, sb, sb.Capacity);
                    var className = sb.ToString();
                    if (rule.ClassNameIsRegex)
                    {
                        if (!Regex.IsMatch(className, rule.ClassNamePattern)) return false;
                    }
                    else
                    {
                        if (!className.Contains(rule.ClassNamePattern)) return false;
                    }
                }

                // 检查窗口标题
                if (!string.IsNullOrEmpty(rule.WindowTitlePattern))
                {
                    var sb = new StringBuilder(512);
                    GetWindowText(hWnd, sb, sb.Capacity);
                    var title = sb.ToString();
                    if (rule.TitleIsRegex)
                    {
                        if (!Regex.IsMatch(title, rule.WindowTitlePattern)) return false;
                    }
                    else
                    {
                        if (!title.Contains(rule.WindowTitlePattern)) return false;
                    }
                }

                // 检查进程名
                if (!string.IsNullOrEmpty(rule.ProcessName))
                {
                    GetWindowThreadProcessId(hWnd, out uint processId);
                    if (processId == 0) return false;

                    var processName = GetProcessFileName(processId);
                    if (processName == null) return false;

                    // ProcessName 可能是不含路径的文件名（如 "EasiNote.exe"）
                    if (!processName.Equals(rule.ProcessName, StringComparison.OrdinalIgnoreCase))
                        return false;
                }

                // 检查窗口样式
                if (rule.HasWindowStyle)
                {
                    var style = GetWindowLong(hWnd, GWL_STYLE);
                    if (rule.StyleMatchType == StyleMatchType.Exact)
                    {
                        if (style != rule.WindowStyle) return false;
                    }
                    else // Subset
                    {
                        if ((style & rule.WindowStyle) != rule.WindowStyle) return false;
                    }
                }

                // 检查窗口尺寸
                if (rule.SizeMatchType != SizeMatchType.None)
                {
                    RECT rect;
                    if (DwmGetWindowAttribute(hWnd, DWMWA_EXTENDED_FRAME_BOUNDS, out rect, Marshal.SizeOf(typeof(RECT))) != 0)
                        GetWindowRect(hWnd, out rect);

                    int w = rect.Right - rect.Left;
                    int h = rect.Bottom - rect.Top;
                    if (w == 0 && h == 0) return false;

                    // DWM 返回物理像素，GetSystemMetrics 返回逻辑像素，需换算
                    var hdc = GetDC(IntPtr.Zero);
                    float scaleX = GetDeviceCaps(hdc, LOGPIXELSX) / 96.0f;
                    float scaleY = GetDeviceCaps(hdc, LOGPIXELSY) / 96.0f;
                    ReleaseDC(IntPtr.Zero, hdc);
                    int screenW = (int)(GetSystemMetrics(SM_CXSCREEN) * scaleX);
                    int screenH = (int)(GetSystemMetrics(SM_CYSCREEN) * scaleY);

                    const int tolerance = 2;

                    switch (rule.SizeMatchType)
                    {
                        case SizeMatchType.FullScreen:
                            if (Math.Abs(w - screenW) > tolerance || Math.Abs(h - screenH) > tolerance)
                                return false;
                            break;
                        case SizeMatchType.FullHeight:
                            if (Math.Abs(h - screenH) > tolerance) return false;
                            break;
                        case SizeMatchType.FullWidth:
                            if (Math.Abs(w - screenW) > tolerance) return false;
                            break;
                        case SizeMatchType.Exact:
                            if (w != rule.WindowWidth || h != rule.WindowHeight) return false;
                            break;
                        case SizeMatchType.DPIScale:
                            if (Math.Abs(rule.WindowWidth * scaleX - w) > tolerance ||
                                Math.Abs(rule.WindowHeight * scaleY - h) > tolerance)
                                return false;
                            break;
                        case SizeMatchType.Scale:
                        {
                            if (rule.WindowWidth == 0 || rule.WindowHeight == 0) return false;
                            float wr = (float)w / rule.WindowWidth;
                            float hr = (float)h / rule.WindowHeight;
                            if (Math.Abs(wr - hr) > 0.03f) return false;
                            break;
                        }
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private string GetProcessFileName(uint processId)
        {
            try
            {
                var hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
                if (hProcess == IntPtr.Zero) return null;
                try
                {
                    var sb = new StringBuilder(1024);
                    uint size = (uint)sb.Capacity;
                    if (QueryFullProcessImageNameW(hProcess, 0, sb, ref size))
                    {
                        var fullPath = sb.ToString(0, (int)size);
                        return System.IO.Path.GetFileName(fullPath);
                    }
                    return null;
                }
                finally
                {
                    CloseHandle(hProcess);
                }
            }
            catch
            {
                return null;
            }
        }

        private void ApplyIntercept(IntPtr hWnd, InterceptRule rule)
        {
            try
            {
                if (!IsWindow(hWnd) || !IsWindowVisible(hWnd))
                {
                    _interceptedWindows.Remove(hWnd);
                    return;
                }

                switch (rule.ActionType)
                {
                    case InterceptActionType.Hide:
                        ShowWindow(hWnd, SW_HIDE);
                        _interceptedWindows[hWnd] = rule.Type;
                        break;

                    case InterceptActionType.Close:
                        PostMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                        break;

                    case InterceptActionType.Move:
                        GetWindowRect(hWnd, out var rect);
                        int x = rect.Left, y = rect.Top;
                        if (x != -32000 || y != -32000)
                        {
                            _movedWindowPositions[hWnd] = (x, y);
                            SetWindowPos(hWnd, IntPtr.Zero, -32000, -32000, 0, 0,
                                SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
                            _interceptedWindows[hWnd] = rule.Type;
                        }
                        break;
                }

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
                var sb = new StringBuilder(256);
                GetWindowText(hWnd, sb, sb.Capacity);
                return sb.ToString();
            }
            catch { return "Unknown"; }
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
            RestoreAllWindows();
            _disposed = true;
        }

        #endregion
    }
}