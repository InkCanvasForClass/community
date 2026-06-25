using Ink_Canvas.Controls;
using Ink_Canvas.Controls.Toolbar.FloatingToolbar;
using Ink_Canvas.Helpers;
using Ink_Canvas.Models;
using Ink_Canvas.Properties;
using Ink_Canvas.Windows;
using Ink_Canvas.Windows.SettingsViews;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using Ink_Canvas.WorkflowAutomation;
using iNKORE.UI.WPF.Modern;
using iNKORE.UI.WPF.Modern.Controls;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;
using Point = System.Windows.Point;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace Ink_Canvas
{
    public partial class MainWindow : Ink_Canvas.Helpers.PerformanceTransparentWin
    {
        // 每一页一个Canvas对象
        private List<System.Windows.Controls.Canvas> whiteboardPages = new List<System.Windows.Controls.Canvas>();
        private int currentPageIndex;
        private System.Windows.Controls.Canvas currentCanvas;
        internal AutoUpdateHelper.UpdateLineGroup AvailableLatestLineGroup;

        // 全局快捷键管理器
        private GlobalHotkeyManager _globalHotkeyManager;

        // 墨迹渐隐管理器
        private InkFadeManager _inkFadeManager;
        private readonly CancellationTokenSource _notificationProviderCancellation = new CancellationTokenSource();
        private AnnouncementService _announcementService;

        // 悬浮窗拦截管理器
        public FloatingWindowInterceptorManager _floatingWindowInterceptorManager;

        // 窗口概览模型
        private WindowOverviewModel _windowOverviewModel;

        // 设置面板相关状态
        // _isApplyingLanguageFromSettings migrated to AppearancePage
        internal bool _isReloadingForLanguageChange;

        // 全屏处理状态标志
        public bool isFullScreenApplied = false;

        private int _boothResolutionWidth = 1920;
        private int _boothResolutionHeight = 1080;
        public int BoothResolutionWidth => _boothResolutionWidth;
        public int BoothResolutionHeight => _boothResolutionHeight;

        private static Cursor _cachedPenCursor = null;
        private static readonly object _cursorLock = new object();
        private static Cursor _cachedCustomCursor = null;
        private static string _cachedCustomCursorPath = null;

        public static void ClearCustomCursorCache()
        {
            lock (_cursorLock)
            {
                _cachedCustomCursor = null;
                _cachedCustomCursorPath = null;
            }
        }

        internal static DateTime? TrayTemporaryShowUntilUtc;

        // Phase 1: Cursor_Icon / Pen_Icon 原为 XAML 自动生成字段，迁移到 ToolbarRegistry 动态注入后
        // 由 ToolbarHost 在 Window_Loaded 中回填。外部代码 (MW_AutoTheme / MW_FloatingBarIcons 等)
        // 以原字段名继续访问，无需修改。
        internal ToolbarImageButton Cursor_Icon { get; private set; }
        internal ToolbarImageButton Pen_Icon { get; private set; }

        internal ToolbarHost ToolbarHost { get; private set; }

        // Board-prefixed buttons: originally XAML auto-generated fields, now delegated to BoardToolsPopupContent
        internal ToolMenuButton BoardTimerToolBtn => BoardToolsPopupContent?.TimerBtn;
        internal ToolMenuButton BoardRandomDrawToolBtn => BoardToolsPopupContent?.RandomDrawBtn;
        internal ToolMenuButton BoardSingleDrawToolBtn => BoardToolsPopupContent?.SingleDrawBtn;
        internal ToolMenuButton BoardSaveToolBtn => BoardToolsPopupContent?.SaveBtn;
        internal ToolMenuButton BoardOpenToolBtn => BoardToolsPopupContent?.OpenBtn;
        internal ToolMenuButton BoardReplayToolBtn => BoardToolsPopupContent?.ReplayBtn;
        internal ToolMenuButton BoardScreenshotToolBtn => BoardToolsPopupContent?.ScreenshotBtn;
        internal ToolMenuButton BoardShapeDrawToolBtn => BoardToolsPopupContent?.ShapeDrawBtn;
        internal ToolMenuButton BoardRedoToolBtn => BoardToolsPopupContent?.RedoBtn;
        internal ToolMenuButton BoardManualToolBtn => BoardToolsPopupContent?.ManualBtn;
        internal ToolMenuButton BoardSettingsToolBtn => BoardToolsPopupContent?.SettingsBtn;

        // Non-Board buttons: originally XAML auto-generated fields, now delegated to MainToolsPopupContent
        internal ToolMenuButton TimerToolBtn => MainToolsPopupContent?.TimerBtn;
        internal ToolMenuButton RandomDrawToolBtn => MainToolsPopupContent?.RandomDrawBtn;
        internal ToolMenuButton SingleDrawToolBtn => MainToolsPopupContent?.SingleDrawBtn;
        internal ToolMenuButton SaveToolBtn => MainToolsPopupContent?.SaveBtn;
        internal ToolMenuButton OpenToolBtn => MainToolsPopupContent?.OpenBtn;
        internal ToolMenuButton ReplayToolBtn => MainToolsPopupContent?.ReplayBtn;
        internal ToolMenuButton ScreenshotToolBtn => MainToolsPopupContent?.ScreenshotBtn;
        internal ToolMenuButton ShapeDrawToolBtn => MainToolsPopupContent?.ShapeDrawBtn;
        internal ToolMenuButton RedoToolBtn => MainToolsPopupContent?.RedoBtn;
        internal ToolMenuButton ManualToolBtn => MainToolsPopupContent?.ManualBtn;
        internal ToolMenuButton SettingsToolBtn => MainToolsPopupContent?.SettingsBtn;

        internal Image LeftUnFoldBtnImgChevron => LeftSidePanel?.ChevronIcon;
        internal Image RightUnFoldBtnImgChevron => RightSidePanel?.ChevronIcon;

        internal bool IsInPPTPresentationMode { get; set; }
        internal bool ArePPTControlsVisible { get; set; }

        internal static readonly DependencyProperty IsUndoEnabledProperty =
            DependencyProperty.Register(nameof(IsUndoEnabled), typeof(bool), typeof(MainWindow),
                new PropertyMetadata(false));
        internal bool IsUndoEnabled
        {
            get => (bool)GetValue(IsUndoEnabledProperty);
            set => SetValue(IsUndoEnabledProperty, value);
        }

        internal static readonly DependencyProperty IsRedoEnabledProperty =
            DependencyProperty.Register(nameof(IsRedoEnabled), typeof(bool), typeof(MainWindow),
                new PropertyMetadata(false));
        internal bool IsRedoEnabled
        {
            get => (bool)GetValue(IsRedoEnabledProperty);
            set => SetValue(IsRedoEnabledProperty, value);
        }

        #region Window Initialization

        private bool _toolsPopupEventsWired;
        private bool _backgroundPaletteEventsWired;

        private void WireUpToolsPopupContentEvents()
        {
            if (_toolsPopupEventsWired) return;
            _toolsPopupEventsWired = true;

            WireUpSingleToolsPopupContent(BoardToolsPopupContent);
            WireUpSingleToolsPopupContent(MainToolsPopupContent);
        }

        private void WireUpSingleToolsPopupContent(ToolsPopupContent content)
        {
            if (content == null) return;

            if (content.TimerBtn != null)
                content.TimerBtn.ButtonMouseUp += ImageCountdownTimer_MouseUp;
            if (content.RandomDrawBtn != null)
                content.RandomDrawBtn.ButtonMouseUp += SymbolIconRand_MouseUp;
            if (content.SingleDrawBtn != null)
                content.SingleDrawBtn.ButtonMouseUp += SymbolIconRandOne_MouseUp;
            if (content.SaveBtn != null)
            {
                content.SaveBtn.ButtonMouseDown += Border_MouseDown;
                content.SaveBtn.ButtonMouseUp += SymbolIconSaveStrokes_MouseUp;
            }
            if (content.OpenBtn != null)
            {
                content.OpenBtn.ButtonMouseDown += Border_MouseDown;
                content.OpenBtn.ButtonMouseUp += SymbolIconOpenStrokes_MouseUp;
            }
            if (content.ReplayBtn != null)
                content.ReplayBtn.ButtonMouseUp += GridInkReplayButton_MouseUp;
            if (content.ScreenshotBtn != null)
                content.ScreenshotBtn.ButtonMouseUp += SymbolIconScreenshot_MouseUp;
            if (content.ShapeDrawBtn != null)
                content.ShapeDrawBtn.ButtonMouseUp += ImageDrawShape_MouseUp;
            if (content.RedoBtn != null)
                content.RedoBtn.ButtonMouseUp += SymbolIconRedo_MouseUp;
            if (content.ManualBtn != null)
                content.ManualBtn.ButtonMouseUp += OperatingGuideWindowIcon_MouseUp;
            if (content.SettingsBtn != null)
                content.SettingsBtn.ButtonMouseUp += SymbolIconSettings_Click;
            content.CloseButtonControl.Click += CloseBordertools_Click;
        }

        private void WireUpBackgroundPaletteEvents()
        {
            if (_backgroundPaletteEventsWired) return;
            _backgroundPaletteEventsWired = true;

            if (BackgroundPalettePopupContent == null) return;

            var content = BackgroundPalettePopupContent;
            content.WhiteboardBtn.MouseUp += WhiteboardModeBtn_MouseUp;
            content.BlackboardBtn.MouseUp += BlackboardModeBtn_MouseUp;
            content.DarkModeBtnControl.MouseUp += DarkModeBtn_MouseUp;
            content.RSlider.ValueChanged += BackgroundRSlider_ValueChanged;
            content.GSlider.ValueChanged += BackgroundGSlider_ValueChanged;
            content.BSlider.ValueChanged += BackgroundBSlider_ValueChanged;
            content.ApplyBtn.Click += ApplyBackgroundColorBtn_Click;
            content.CloseButtonControl.Click += CloseBordertools_Click;
        }

        private void WireUpBoardShapeDrawPopupContentEvents()
        {
            if (_boardShapeDrawPopupEventsWired) return;
            _boardShapeDrawPopupEventsWired = true;

            var content = BoardShapeDrawPopupContent;
            if (content == null) return;

            content.DrawLineBtn.ButtonMouseDown += Image_MouseDown;
            content.DrawLineBtn.ButtonMouseUp += BtnDrawLine_Click;
            content.DrawDashedLineBtn.ButtonMouseDown += Image_MouseDown;
            content.DrawDashedLineBtn.ButtonMouseUp += BtnDrawDashedLine_Click;
            content.DrawDotLineBtn.ButtonMouseDown += Image_MouseDown;
            content.DrawDotLineBtn.ButtonMouseUp += BtnDrawDotLine_Click;
            content.DrawArrowBtn.ButtonMouseDown += Image_MouseDown;
            content.DrawArrowBtn.ButtonMouseUp += BtnDrawArrow_Click;
            content.DrawParallelLineBtn.ButtonMouseDown += Image_MouseDown;
            content.DrawParallelLineBtn.ButtonMouseUp += BtnDrawParallelLine_Click;
            content.DrawRectangleCenterBtn.ButtonMouseUp += BtnDrawRectangleCenter_Click;
            content.DrawCircleBtn.ButtonMouseUp += BtnDrawCircle_Click;
            content.DrawDashedCircleBtn.ButtonMouseUp += BtnDrawDashedCircle_Click;
            content.DrawEllipseCenterBtn.ButtonMouseUp += BtnDrawCenterEllipse_Click;
            content.DrawEllipseCenterWithFocalPointBtn.ButtonMouseUp += BtnDrawCenterEllipseWithFocalPoint_Click;
            content.DrawCuboidBtn.ButtonMouseUp += BtnDrawCuboid_Click;
            content.DrawRectangleBtn.ButtonMouseUp += BtnDrawRectangle_Click;
            content.DrawCylinderBtn.ButtonMouseUp += BtnDrawCylinder_Click;
            content.DrawConeBtn.ButtonMouseUp += BtnDrawCone_Click;
            content.DrawCoordinate1Btn.ButtonMouseUp += BtnDrawCoordinate1_Click;
            content.DrawCoordinate2Btn.ButtonMouseUp += BtnDrawCoordinate2_Click;
            content.DrawCoordinate3Btn.ButtonMouseUp += BtnDrawCoordinate3_Click;
            content.DrawCoordinate4Btn.ButtonMouseUp += BtnDrawCoordinate4_Click;
            content.DrawCoordinate5Btn.ButtonMouseUp += BtnDrawCoordinate5_Click;
            content.DrawHyperbolaBtn.ButtonMouseUp += BtnDrawHyperbola_Click;
            content.DrawHyperbolaWithFocalPointBtn.ButtonMouseUp += BtnDrawHyperbolaWithFocalPoint_Click;
            content.DrawParabola1Btn.ButtonMouseUp += BtnDrawParabola1_Click;
            content.DrawParabolaWithFocalPointBtn.ButtonMouseUp += BtnDrawParabolaWithFocalPoint_Click;
            content.DrawParabola2Btn.ButtonMouseUp += BtnDrawParabola2_Click;
            content.CloseButtonControl.Click += CloseBordertools_Click;
            content.ShowCircleCenterToggle.Toggled += ToggleSwitchShowCircleCenter_Toggled;
        }

        private bool _penPaletteEventsWired;
        private bool _eraserPopupEventsWired;
        private bool _gesturePopupEventsWired;
        private bool _isUpdatingSliders;

        private void WireUpPenPaletteEvents()
        {
            if (_penPaletteEventsWired) return;
            _penPaletteEventsWired = true;

            WireUpSinglePenPaletteEvents(PenPalettePopupContent);
            WireUpSinglePenPaletteEvents(BoardPenPalettePopupContent);
        }

        private void WireUpSinglePenPaletteEvents(PenPalettePopupContent content)
        {
            if (content == null) return;

            content.PenStyleComboBox.SelectionChanged += ComboBoxPenStyle_SelectionChanged;
            content.NibModeToggle.Toggled += ToggleSwitchEnableNibMode_Toggled;
            content.InkToShapeToggle.Toggled += ToggleSwitchEnableInkToShape_Toggled;
            content.PenWidthSlider.ValueChanged += PenWidthSlider_ValueChanged;
            content.PenAlphaSlider.ValueChanged += PenAlphaSlider_ValueChanged;
            content.LaserPenFadeTimeSlider.ValueChanged += LaserPenFadeTimeSlider_ValueChanged;
            content.LaserPenFadeSpeedSlider.ValueChanged += LaserPenFadeSpeedSlider_ValueChanged;
            content.HighlighterOverlapToggle.Toggled += HighlighterOverlapToggle_Toggled;

            content.TabBar.SelectedIndexChanged += (s, idx) =>
            {
                if (idx == 0) SwitchToDefaultPen(s, null);
                else if (idx == 1) SwitchToHighlighterPen(s, null);
                else if (idx == 2) SwitchToLaserPen(s, null);
            };

            content.DefaultPenColorBlack.ButtonMouseUp += BtnColorBlack_Click;
            content.DefaultPenColorWhite.ButtonMouseUp += BtnColorWhite_Click;
            content.DefaultPenColorRed.ButtonMouseUp += BtnColorRed_Click;
            content.DefaultPenColorYellow.ButtonMouseUp += BtnColorYellow_Click;
            content.DefaultPenColorGreen.ButtonMouseUp += BtnColorGreen_Click;
            content.DefaultPenColorBlue.ButtonMouseUp += BtnColorBlue_Click;
            content.DefaultPenColorPink.ButtonMouseUp += BtnColorPink_Click;
            content.DefaultPenColorTeal.ButtonMouseUp += BtnColorTeal_Click;
            content.DefaultPenColorOrange.ButtonMouseUp += BtnColorOrange_Click;

            content.HighlighterPenColorBlack.ButtonMouseUp += BtnHighlighterColorBlack_Click;
            content.HighlighterPenColorWhite.ButtonMouseUp += BtnHighlighterColorWhite_Click;
            content.HighlighterPenColorRed.ButtonMouseUp += BtnHighlighterColorRed_Click;
            content.HighlighterPenColorYellow.ButtonMouseUp += BtnHighlighterColorYellow_Click;
            content.HighlighterPenColorGreen.ButtonMouseUp += BtnHighlighterColorGreen_Click;
            content.HighlighterPenColorZinc.ButtonMouseUp += BtnHighlighterColorZinc_Click;
            content.HighlighterPenColorBlue.ButtonMouseUp += BtnHighlighterColorBlue_Click;
            content.HighlighterPenPenColorPurple.ButtonMouseUp += BtnHighlighterColorPurple_Click;
            content.HighlighterPenColorTeal.ButtonMouseUp += BtnHighlighterColorTeal_Click;
            content.HighlighterPenColorOrange.ButtonMouseUp += BtnHighlighterColorOrange_Click;

            content.LaserPenColorBlack.ButtonMouseUp += BtnLaserPenColorBlack_Click;
            content.LaserPenColorWhite.ButtonMouseUp += BtnLaserPenColorWhite_Click;
            content.LaserPenColorRed.ButtonMouseUp += BtnLaserPenColorRed_Click;
            content.LaserPenColorYellow.ButtonMouseUp += BtnLaserPenColorYellow_Click;
            content.LaserPenColorGreen.ButtonMouseUp += BtnLaserPenColorGreen_Click;
            content.LaserPenColorBlue.ButtonMouseUp += BtnLaserPenColorBlue_Click;
            content.LaserPenColorPink.ButtonMouseUp += BtnLaserPenColorPink_Click;
            content.LaserPenColorTeal.ButtonMouseUp += BtnLaserPenColorTeal_Click;
            content.LaserPenColorOrange.ButtonMouseUp += BtnLaserPenColorOrange_Click;

            content.ColorThemeSwitch.MouseUp += ColorThemeSwitch_MouseUp;
            content.LaserPenColorThemeSwitch.MouseUp += ColorThemeSwitch_MouseUp;
            content.CloseButtonControl.Click += CloseBordertools_Click;
        }

        private void WireUpEraserPopupContentEvents()
        {
            if (_eraserPopupEventsWired) return;
            _eraserPopupEventsWired = true;

            WireUpSingleEraserPopupContentEvents(EraserPopupContent);
            WireUpSingleEraserPopupContentEvents(BoardEraserPopupContent);
        }

        private void WireUpSingleEraserPopupContentEvents(EraserPopupContent content)
        {
            if (content == null) return;

            content.EraserSizeComboBox.SelectionChanged += ComboBoxEraserSizeFloatingBar_SelectionChanged;
            content.EraserTypeTab.SelectionChanged += EraserTypeTab_SelectionChanged;
            content.ClearInkBtn.Click += EraserPanelSymbolIconDelete_MouseUp;
            content.ClearInkAndHistoryBtn.Click += BoardSymbolIconDeleteInkAndHistories_MouseUp;
            content.CloseButtonControl.Click += CloseBordertools_Click;
        }

        private void WireUpGesturePopupContentEvents()
        {
            if (_gesturePopupEventsWired) return;
            _gesturePopupEventsWired = true;

            WireUpSingleGesturePopupContentEvents(FloatingBarGesturePopupContent);
            WireUpSingleGesturePopupContentEvents(BoardGesturePopupContent);
        }

        private void WireUpSingleGesturePopupContentEvents(GesturePopupContent content)
        {
            if (content == null) return;

            content.MultiTouchToggle.Toggled += ToggleSwitchEnableMultiTouchMode_Toggled;
            content.TwoFingerTranslateToggle.Toggled += ToggleSwitchEnableTwoFingerTranslate_Toggled;
            content.TwoFingerZoomToggle.Toggled += ToggleSwitchEnableTwoFingerZoom_Toggled;
            content.TwoFingerRotationToggle.Toggled += ToggleSwitchEnableTwoFingerRotation_Toggled;
            content.CloseButtonControl.Click += CloseBordertools_Click;
        }

        private bool _imageOptionsPopupEventsWired;

        private void WireUpImageOptionsPopupContentEvents()
        {
            if (_imageOptionsPopupEventsWired) return;
            _imageOptionsPopupEventsWired = true;

            var content = BoardImageOptionsPopupContent;
            if (content == null) return;

            content.ScreenshotOption.MouseUp += ImageOptionScreenshot_MouseUp;
            content.SelectFileOption.MouseUp += ImageOptionSelectFile_MouseUp;
            content.CloseButtonControl.Click += CloseBordertools_Click;
        }

        private bool _shapeDrawPopupEventsWired;
        private bool _boardShapeDrawPopupEventsWired;

        private void WireUpShapeDrawPopupContentEvents()
        {
            if (_shapeDrawPopupEventsWired) return;
            _shapeDrawPopupEventsWired = true;

            var content = ShapeDrawPopupContent;
            if (content == null) return;

            content.DrawLineBtn.ButtonMouseDown += Image_MouseDown;
            content.DrawLineBtn.ButtonMouseUp += BtnDrawLine_Click;
            content.DrawDashedLineBtn.ButtonMouseDown += Image_MouseDown;
            content.DrawDashedLineBtn.ButtonMouseUp += BtnDrawDashedLine_Click;
            content.DrawDotLineBtn.ButtonMouseDown += Image_MouseDown;
            content.DrawDotLineBtn.ButtonMouseUp += BtnDrawDotLine_Click;
            content.DrawArrowBtn.ButtonMouseDown += Image_MouseDown;
            content.DrawArrowBtn.ButtonMouseUp += BtnDrawArrow_Click;
            content.DrawParallelLineBtn.ButtonMouseDown += Image_MouseDown;
            content.DrawParallelLineBtn.ButtonMouseUp += BtnDrawParallelLine_Click;
            content.DrawRectangleCenterBtn.ButtonMouseUp += BtnDrawRectangleCenter_Click;
            content.DrawCircleBtn.ButtonMouseUp += BtnDrawCircle_Click;
            content.DrawDashedCircleBtn.ButtonMouseUp += BtnDrawDashedCircle_Click;
            content.DrawEllipseCenterBtn.ButtonMouseUp += BtnDrawCenterEllipse_Click;
            content.DrawEllipseCenterWithFocalPointBtn.ButtonMouseUp += BtnDrawCenterEllipseWithFocalPoint_Click;
            content.DrawCuboidBtn.ButtonMouseUp += BtnDrawCuboid_Click;
            content.DrawRectangleBtn.ButtonMouseUp += BtnDrawRectangle_Click;
            content.DrawCylinderBtn.ButtonMouseUp += BtnDrawCylinder_Click;
            content.DrawConeBtn.ButtonMouseUp += BtnDrawCone_Click;
            content.DrawCoordinate1Btn.ButtonMouseUp += BtnDrawCoordinate1_Click;
            content.DrawCoordinate2Btn.ButtonMouseUp += BtnDrawCoordinate2_Click;
            content.DrawCoordinate3Btn.ButtonMouseUp += BtnDrawCoordinate3_Click;
            content.DrawCoordinate4Btn.ButtonMouseUp += BtnDrawCoordinate4_Click;
            content.DrawCoordinate5Btn.ButtonMouseUp += BtnDrawCoordinate5_Click;
            content.DrawHyperbolaBtn.ButtonMouseUp += BtnDrawHyperbola_Click;
            content.DrawHyperbolaWithFocalPointBtn.ButtonMouseUp += BtnDrawHyperbolaWithFocalPoint_Click;
            content.DrawParabola1Btn.ButtonMouseUp += BtnDrawParabola1_Click;
            content.DrawParabolaWithFocalPointBtn.ButtonMouseUp += BtnDrawParabolaWithFocalPoint_Click;
            content.DrawParabola2Btn.ButtonMouseUp += BtnDrawParabola2_Click;
            content.CloseButtonControl.Click += CloseBordertools_Click;
            content.ShowCircleCenterToggle.Toggled += ToggleSwitchShowCircleCenter_Toggled;
        }

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
            InitializeComponent();

            if (BorderTools.Child is FrameworkElement btChild) btChild.Visibility = Visibility.Collapsed;
            if (BorderDrawShape.Child is FrameworkElement bdsChild) bdsChild.Visibility = Visibility.Collapsed;
            if (BoardBorderToolsPopup.Child is FrameworkElement bbtpChild) bbtpChild.Visibility = Visibility.Collapsed;
            if (BoardBorderDrawShape.Child is FrameworkElement bbdsChild) bbdsChild.Visibility = Visibility.Collapsed;

            WireUpToolsPopupContentEvents();
            WireUpShapeDrawPopupContentEvents();
            WireUpBoardShapeDrawPopupContentEvents();
            WireUpBackgroundPaletteEvents();
            WireUpPenPaletteEvents();
            WireUpEraserPopupContentEvents();
            WireUpGesturePopupContentEvents();
            WireUpImageOptionsPopupContentEvents();
            WireUpWhiteboardModeSelectionEvents();
            BoardBorderToolsPopup.CustomPopupPlacementCallback =
                (popupSize, targetSize, offset) => new[]
                {
                    new CustomPopupPlacement(
                        new Point((targetSize.Width - popupSize.Width) / 2, -popupSize.Height - 5),
                        PopupPrimaryAxis.Vertical)
                };

            BorderTools.CustomPopupPlacementCallback =
                (popupSize, targetSize, offset) => new[]
                {
                    IsVerticalToolbar
                        ? new CustomPopupPlacement(
                            new Point(-popupSize.Width - 8, (targetSize.Height - popupSize.Height) / 2),
                            PopupPrimaryAxis.Horizontal)
                        : new CustomPopupPlacement(
                            new Point(targetSize.Width / 2 - popupSize.Width / 2, -popupSize.Height - 8),
                            PopupPrimaryAxis.Vertical)
                };

            BorderDrawShape.CustomPopupPlacementCallback =
                (popupSize, targetSize, offset) => new[]
                {
                    IsVerticalToolbar
                        ? new CustomPopupPlacement(
                            new Point(-popupSize.Width - 8, (targetSize.Height - popupSize.Height) / 2),
                            PopupPrimaryAxis.Horizontal)
                        : new CustomPopupPlacement(
                            new Point(targetSize.Width / 2 - popupSize.Width / 2, -popupSize.Height - 8),
                            PopupPrimaryAxis.Vertical)
                };

            BoardBorderDrawShape.CustomPopupPlacementCallback =
                (popupSize, targetSize, offset) => new[]
                {
                    new CustomPopupPlacement(
                        new Point((targetSize.Width - popupSize.Width) / 2, -popupSize.Height - 5),
                        PopupPrimaryAxis.Vertical)
                };

            PenPalette.CustomPopupPlacementCallback =
                (popupSize, targetSize, offset) => new[]
                {
                    IsVerticalToolbar
                        ? new CustomPopupPlacement(
                            new Point(-popupSize.Width - 8, (targetSize.Height - popupSize.Height) / 2),
                            PopupPrimaryAxis.Horizontal)
                        : new CustomPopupPlacement(
                            new Point(targetSize.Width / 2 - popupSize.Width / 2, -popupSize.Height - 8),
                            PopupPrimaryAxis.Vertical)
                };

            BoardPenPalette.CustomPopupPlacementCallback =
                (popupSize, targetSize, offset) => new[]
                {
                    new CustomPopupPlacement(
                        new Point((targetSize.Width - popupSize.Width) / 2, -popupSize.Height - 5),
                        PopupPrimaryAxis.Vertical)
                };

            EraserSizePanel.CustomPopupPlacementCallback =
                (popupSize, targetSize, offset) => new[]
                {
                    IsVerticalToolbar
                        ? new CustomPopupPlacement(
                            new Point(-popupSize.Width - 8, (targetSize.Height - popupSize.Height) / 2),
                            PopupPrimaryAxis.Horizontal)
                        : new CustomPopupPlacement(
                            new Point(targetSize.Width / 2 - popupSize.Width / 2, -popupSize.Height - 8),
                            PopupPrimaryAxis.Vertical)
                };

            BoardEraserSizePanel.CustomPopupPlacementCallback =
                (popupSize, targetSize, offset) => new[]
                {
                    new CustomPopupPlacement(
                        new Point((targetSize.Width - popupSize.Width) / 2, -popupSize.Height - 5),
                        PopupPrimaryAxis.Vertical)
                };

            BoardImageOptionsPanel.CustomPopupPlacementCallback =
                (popupSize, targetSize, offset) => new[]
                {
                    new CustomPopupPlacement(
                        new Point((targetSize.Width - popupSize.Width) / 2, -popupSize.Height - 5),
                        PopupPrimaryAxis.Vertical)
                };

            TwoFingerGestureBorder.CustomPopupPlacementCallback =
                (popupSize, targetSize, offset) => new[]
                {
                    IsVerticalToolbar
                        ? new CustomPopupPlacement(
                            new Point(-popupSize.Width - 8, (targetSize.Height - popupSize.Height) / 2),
                            PopupPrimaryAxis.Horizontal)
                        : new CustomPopupPlacement(
                            new Point(targetSize.Width / 2 - popupSize.Width / 2, -popupSize.Height - 8),
                            PopupPrimaryAxis.Vertical)
                };

            BoardTwoFingerGestureBorder.CustomPopupPlacementCallback =
                (popupSize, targetSize, offset) => new[]
                {
                    new CustomPopupPlacement(
                        new Point((targetSize.Width - popupSize.Width) / 2, -popupSize.Height - 5),
                        PopupPrimaryAxis.Vertical)
                };

            BackgroundPalette.CustomPopupPlacementCallback =
                (popupSize, targetSize, offset) => new[]
                {
                    new CustomPopupPlacement(
                        new Point((targetSize.Width - popupSize.Width) / 2, -popupSize.Height - 5),
                        PopupPrimaryAxis.Vertical)
                };

            BlackboardLeftSide.Visibility = Visibility.Collapsed;
            BlackboardCenterSide.Visibility = Visibility.Collapsed;
            BlackboardRightSide.Visibility = Visibility.Collapsed;
            BorderTools.IsOpen = false;
            LeftSidePanelForPPTNavigation.Visibility = Visibility.Collapsed;
            RightSidePanelForPPTNavigation.Visibility = Visibility.Collapsed;
            TwoFingerGestureBorder.IsOpen = false;
            BoardTwoFingerGestureBorder.IsOpen = false;
            BorderDrawShape.IsOpen = false;
            BoardBorderDrawShape.IsOpen = false;
            GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;

            //if (!App.StartArgs.Contains("-o"))

            // Old UI removed: ViewBoxStackPanelMain.Visibility = Visibility.Collapsed;
            // Old UI removed: ViewBoxStackPanelShapes.Visibility = Visibility.Collapsed;
            var workingArea = Screen.PrimaryScreen.WorkingArea;

            double dpiScaleX = 1, dpiScaleY = 1;
            var source = PresentationSource.FromVisual(this);
            if (source != null)
            {
                dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
                dpiScaleY = source.CompositionTarget.TransformToDevice.M22;
            }

            double logicalScreenWidth = workingArea.Width / dpiScaleX;
            double logicalScreenBottom = workingArea.Bottom / dpiScaleY;
            double logicalScreenTop = workingArea.Top / dpiScaleY;

            double floatingBarWidth = 284;
            if (Settings.Appearance.IsShowQuickColorPalette)
            {
                if (Settings.Appearance.QuickColorPaletteDisplayMode == 0)
                {
                    floatingBarWidth = Math.Max(floatingBarWidth, 120);
                }
                else
                {
                    floatingBarWidth = Math.Max(floatingBarWidth, 820);
                }
            }
            ViewboxFloatingBar.Margin = new Thickness(
                (logicalScreenWidth - floatingBarWidth) / 2,
                logicalScreenBottom - 60 - logicalScreenTop,
                -2000, -200);

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

            WindowSettingsHelper.OnStopKillProcessTimer = () => timerKillProcess.Stop();
            WindowSettingsHelper.OnStartKillProcessTimer = () => timerKillProcess.Start();
            WindowSettingsHelper.OnPPTOnlyModeChanged = (enabled) => CheckMainWindowVisibility();
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

            // 应用无焦点模式设置
            ApplyNoFocusMode();
            // 应用窗口置顶设置
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ApplyAlwaysOnTop();
            }), DispatcherPriority.ApplicationIdle);

            // 添加窗口激活事件处理，确保置顶状态在窗口重新激活时得到保持
            Activated += Window_Activated;
            Deactivated += Window_Deactivated;

            Dispatcher.BeginInvoke(new Action(CheckAndShowOobe), DispatcherPriority.Loaded);
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
        private void ApplyLanguageFromSettings()
        {
            try
            {
                if (Settings?.Appearance == null) return;

                var preferredLanguage = Settings.Appearance.Language ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(preferredLanguage))
                {
                    LocalizationHelper.TrySetCulture(preferredLanguage);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"初始化语言选项失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }


        #endregion

        #region Ink Canvas Functions

        private Color Ink_DefaultColor = Colors.Red;

        private DrawingAttributes drawingAttributes;
        private InkSmoothingManager _inkSmoothingManager;

        /// <summary>
        /// 墨迹平滑管理器实例（供性能页面读取统计）
        /// </summary>
        public InkSmoothingManager InkSmoothingManagerInstance => _inkSmoothingManager;

        private DispatcherTimer _brushAutoRestoreTimer;

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
                    if (penType == 0)
                    {
                        Settings.Canvas.InkWidth = width;
                        Settings.Canvas.InkAlpha = (int)color.A;
                    }
                    else if (penType == 1)
                    {
                        Settings.Canvas.HighlighterWidth = width;
                        Settings.Canvas.HighlighterAlpha = (int)color.A;
                    }
                    else if (penType == 2)
                    {
                        Settings.Canvas.LaserPenWidth = width;
                        Settings.Canvas.LaserPenAlpha = (int)color.A;
                    }
                }

                _isUpdatingSliders = true;
                if (PenWidthSlider != null) PenWidthSlider.Value = penType == 0 ? width * 2 : width;
                if (PenAlphaSlider != null) PenAlphaSlider.Value = color.A;
                if (BoardPenWidthSlider != null) BoardPenWidthSlider.Value = penType == 0 ? width * 2 : width;
                if (BoardPenAlphaSlider != null) BoardPenAlphaSlider.Value = color.A;
                _isUpdatingSliders = false;

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

        private void HighlighterOverlapToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!isLoaded) return;
            var toggle = (iNKORE.UI.WPF.Modern.Controls.ToggleSwitch)sender;
            Settings.Canvas.HighlighterOverlapEnabled = toggle.IsOn;
            if (penType == 1)
            {
                drawingAttributes.IsHighlighter = !toggle.IsOn;
                inkCanvas.DefaultDrawingAttributes.IsHighlighter = !toggle.IsOn;
            }
            SaveSettingsToFile();
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
                    if (ArePPTControlsVisible &&
                        IsInPPTPresentationMode &&
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
            if (IsCurrentPageFrozen && IsFreezeMutatingMode(inkCanvas1.EditingMode))
            {
                TryBlockFrozenPageMutation("修改冻结页面");
                inkCanvas1.EditingMode = InkCanvasEditingMode.None;
                return;
            }

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

        public static Settings Settings { get => SettingsManager.Settings; set => SettingsManager.Settings = value; }
        public static string settingsFileName => SettingsManager.SettingsFileName;

        public void UpdateInkSmoothingConfig()
        {
            _inkSmoothingManager?.UpdateConfig();
        }

        public void UpdateInkFadeManager(bool isEnabled, int fadeTime = 0)
        {
            if (_inkFadeManager != null)
            {
                _inkFadeManager.IsEnabled = isEnabled;
                if (fadeTime > 0)
                    _inkFadeManager.UpdateFadeTime(fadeTime);
            }
        }

        public void UpdatePickNameBackgroundsInComboBox()
        {
        }

        public void UpdatePickNameBackgroundDisplay()
        {
        }

        public string _lastAppliedProfileName;
        private bool isLoaded;
        private bool forcePointEraser;
        private bool _pendingStartupAutoUpdateCheck;
        private bool _sliderTouchSupportInitialized;
        private bool _deferredPhaseBCompleted;

        /// <summary>
        /// 在窗口加载完成后初始化应用的核心子系统、UI 状态和运行时监控组件。
        /// </summary>
        /// <remarks>
        /// 执行设置加载与修复、主题与背景应用、PPT 与插件相关管理器初始化、全局功能（剪贴板监控、全局快捷键、墨迹渐隐等）初始化，恢复启动参数相关状态（白板/显示模式、崩溃后动作等），注册必要的系统与控件事件，并为计时器、滑块触摸与画笔性能（如 IA 加载、画笔恢复等）做好预热与绑定。该方法为窗口呈现后的完整准备流程，不包含具体 UI 交互逻辑的实现细节描述。
        /// </remarks>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            loadPenCanvas();
            // 工具栏插件化按钮先注入到容器，确保 LoadSettings 内部对 Cursor_Icon / Pen_Icon 等的访问非空。
            // Settings.Toolbar 此时尚为默认值（全部可见），与旧 XAML 行为一致。
            InitializeToolbarPlugins();
            // 初始化 Popup 管理器（置顶 + 拖动跟随）
            InitializePopupManager();
            //加载设置
            LoadSettings(true);
            // 启动性能监测（如果已启用）
            PerformanceMonitorHelper.StartIfEnabled();
            // 根据ToolbarPosition设置更新工具栏结构和位置
            UpdateToolbarPosition();
            // 启动时直接设置浮动栏位置，跳过动画
            if (currentMode == 0)
            {
                if (IsInPPTPresentationMode) ViewboxFloatingBarMarginAnimation(60, skipAnimation: true);
                else ViewboxFloatingBarMarginAnimation(100, true, skipAnimation: true);
            }
            ApplyLanguageFromSettings();
            Helpers.LocalizationHelper.SyncCommonResources();
            InitializeNotificationProviders();
            AutomationBootstrap.Initialize();

            // 启动时根据设置恢复调试控制台显示状态
            if (Settings?.Advanced != null && Settings.Advanced.IsDebugConsoleEnabled)
            {
                Helpers.DebugConsoleManager.Show();
            }

            LoadCustomBackgroundColor();
            SetWindowMode();

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
                    if (ThemeHelper.IsSystemThemeLight())
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
            EnsureRealtimeStylusPipelineBinding();
            var leftPageListView = FindView("board.pageList.left") as System.Windows.Controls.ListView;
            var rightPageListView = FindView("board.pageList.right") as System.Windows.Controls.ListView;
            if (leftPageListView != null) leftPageListView.ItemsSource = blackBoardSidePageListViewObservableCollection;
            if (rightPageListView != null) rightPageListView.ItemsSource = blackBoardSidePageListViewObservableCollection;

            InitializeBoardToolbar();

            var boardInkFreezeBtn = FindView("board.inkFreeze") as BoardToolbarButton;
            if (boardInkFreezeBtn != null) AttachBoardInkFreezeBtn(boardInkFreezeBtn);

            var leftPreviousBtn = FindView("board.previousPage.left") as BoardToolbarButton;
            var rightPreviousBtn = FindView("board.previousPage.right") as BoardToolbarButton;
            if (leftPreviousBtn != null)
            {
                leftPreviousBtn.IconGeometryDrawing.Brush =
                    new SolidColorBrush(Color.FromArgb(127, 24, 24, 27));
                leftPreviousBtn.LabelTextBlockControl.Opacity = 0.5;
            }
            if (rightPreviousBtn != null)
            {
                rightPreviousBtn.IconGeometryDrawing.Brush =
                    new SolidColorBrush(Color.FromArgb(127, 24, 24, 27));
                rightPreviousBtn.LabelTextBlockControl.Opacity = 0.5;
            }

            // 应用颜色主题，这将考虑自定义背景色
            CheckColorTheme(true);

            BtnWhiteBoardSwitchPrevious.IsEnabled = CurrentWhiteboardIndex != 1;
            BorderInkReplayToolBox.Visibility = Visibility.Collapsed;

            // 识别后端预热改为后台低优先级执行，避免启动主线程被 WinRT 初始化拖慢。
            if (ShapeRecognitionRouter.ShouldRunShapeRecognition(
                    Settings.InkToShape.IsInkToShapeEnabled,
                    ShapeRecognitionRouter.FromSettingsInt(Settings.InkToShape.ShapeRecognitionEngine)))
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    Task.Run(() =>
                    {
                        InkRecognizeHelper.WarmupShapeRecognition(
                            ShapeRecognitionRouter.FromSettingsInt(Settings.InkToShape.ShapeRecognitionEngine));
                    });
                }), DispatcherPriority.ContextIdle);
            }

            SystemEvents.DisplaySettingsChanged += SystemEventsOnDisplaySettingsChanged;
            // 自动收纳到侧边栏（若通过 --board 进入白板模式或 --show 参数则跳过收纳）
            if (Settings.Startup.IsFoldAtStartup && !App.StartWithBoardMode && !App.StartWithShowMode)
            {
                FoldFloatingBar_MouseUp(new object(), null);
                ScheduleStartupFoldAbsenceVerification();
            }
            else
            {
                UnFoldFloatingBar_MouseUp(new object(), null);
            }

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
                        SwitchBackground(null, null);
                    }

                    // 确保背景颜色正确设置为黑板颜色
                    CheckColorTheme(true);
                }), DispatcherPriority.Loaded);
            }

            // 应用无焦点模式设置
            ApplyNoFocusMode();

            // 设置UIA置顶状态
            App.IsUIAccessTopMostEnabled = Settings.Advanced.EnableUIAccessTopMost;
            if (Settings.Advanced.EnableUIAccessTopMost && Settings.Advanced.IsAlwaysOnTop)
            {
                ApplyUIAccessTopMost();
            }

            _ = RunDeferredStartupPhaseBAsync();

            // 处理命令行参数中的文件路径
            HandleCommandLineFileOpen();

            // 初始化文件关联状态显示
            InitializeFileAssociationStatus();

            // 检查模式设置并应用
            CheckMainWindowVisibility();
            EnsurePPTOnlyVisibilityProbeTimer();

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
        }



        /// <summary>
        /// 响应显示器/分辨率配置变化：在检测启用时显示分辨率变更通知，并在后台检查悬浮工具栏是否位于屏幕之外，若是则在延迟后尝试将其通过动画恢复到可见区域（在演示模式下使用不同的动画偏移）。 
        /// </summary>
        /// <param name="sender">触发事件的源对象（通常由系统事件触发）。</param>
        /// <param name="e">事件参数（未使用）。</param>
        public DelayAction dpiChangedDelayAction = new DelayAction();

        private void SystemEventsOnDisplaySettingsChanged(object sender, EventArgs e)
        {
            if (!Settings.Advanced.IsEnableResolutionChangeDetection) return;
            ShowNotification(string.Format(Properties.MainWindowStrings.Main_DisplayChanged, Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height));
            HandleFloatingBarRecovery();
        }

        private void MainWindow_OnDpiChanged(object sender, DpiChangedEventArgs e)
        {
            if (e.OldDpi.DpiScaleX != e.NewDpi.DpiScaleX && e.OldDpi.DpiScaleY != e.NewDpi.DpiScaleY && Settings.Advanced.IsEnableDPIChangeDetection)
            {
                ShowNotification(string.Format(Properties.MainWindowStrings.Main_DPIChanged, e.OldDpi.DpiScaleX, e.OldDpi.DpiScaleY, e.NewDpi.DpiScaleX, e.NewDpi.DpiScaleY));

                HandleFloatingBarRecovery();
            }
        }

        private void HandleFloatingBarRecovery()
        {
            new Thread(() =>
            {
                try
                {
                    var isFloatingBarOutsideScreen = false;
                    var isInPPTPresentationMode = false;
                    Dispatcher.Invoke(() =>
                    {
                        isFloatingBarOutsideScreen = IsOutsideOfScreenHelper.IsOutsideOfScreen(ViewboxFloatingBar);
                        isInPPTPresentationMode = IsInPPTPresentationMode;
                    });
                    if (isFloatingBarOutsideScreen) dpiChangedDelayAction.DebounceAction(3000, null, () =>
                    {
                        if (!isFloatingBarFolded)
                        {
                            if (isInPPTPresentationMode) ViewboxFloatingBarMarginAnimation(60);
                            else ViewboxFloatingBarMarginAnimation(100, true);
                        }
                    });
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"浮动工具栏恢复失败: {ex.Message}", LogHelper.LogType.Warning);
                }
            }).Start();
        }

        /// <summary>
        /// 根据 Settings.Advanced.WindowMode 切换窗口显示模式。
        /// </summary>
        /// <remarks>
        /// 如果该设置为 true，将窗口置为普通状态并调整到主屏幕的左上角(0,0)及主屏幕分辨率的宽高，使窗口覆盖整个主屏幕；
        /// 否则将窗口设为最大化状态。
        /// </remarks>
        public void SetWindowMode()
        {
            WindowSettingsHelper.SetWindowMode(this);
        }

        private bool _allowCloseAfterExitVerification;
        private bool _isExitVerificationInProgress;
        private bool _forceCloseFromExitOrRestartButton;

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
        private async void Window_Closing(object sender, CancelEventArgs e)
        {
            try
            {
                if (_isReloadingForLanguageChange)
                    return;

                LogHelper.WriteLogToFile("Ink Canvas closing", LogHelper.LogType.Event);

                if (_allowCloseAfterExitVerification)
                {
                    e.Cancel = true;
                    if (_isExitVerificationInProgress) return;

                    _isExitVerificationInProgress = true;
                    await Dispatcher.BeginInvoke(new Action(async () =>
                    {
                        try
                        {
                            bool ok = await SecurityManager.PromptAndVerifyPasswordOrTotpAsync(Settings, this, Properties.MainWindowStrings.Main_ExitVerify, Properties.MainWindowStrings.Main_ExitVerifyWithTotp);
                            if (!ok)
                            {
                                _forceCloseFromExitOrRestartButton = false;
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

                if (!_forceCloseFromExitOrRestartButton &&
                    IsInPPTPresentationMode)
                {
                    e.Cancel = true;
                    await ExitPPTPresentation();
                    LogHelper.WriteLogToFile("Ink Canvas closing converted to exit PPT", LogHelper.LogType.Event);
                    return;
                }
                if (!_forceCloseFromExitOrRestartButton && currentMode != 0)
                {
                    e.Cancel = true;
                    CloseWhiteboardImmediately();
                    LogHelper.WriteLogToFile("Ink Canvas closing converted to exit whiteboard", LogHelper.LogType.Event);
                    return;
                }

                try
                {
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
                        await Dispatcher.BeginInvoke(new Action(async () =>
                        {
                            try
                            {
                                bool ok = await SecurityManager.PromptAndVerifyAsync(Settings, this, Properties.MainWindowStrings.Main_ExitVerify, Properties.MainWindowStrings.Main_ExitVerifyPasswordOnly);
                                if (!ok)
                                {
                                    _forceCloseFromExitOrRestartButton = false;
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
                    var result1 = MessageBox.Show(Properties.MainWindowStrings.Main_CloseConfirm_Level1, "InkCanvasForClass",
                        MessageBoxButton.OKCancel, MessageBoxImage.Warning);

                    if (result1 == MessageBoxResult.Cancel)
                    {
                        _forceCloseFromExitOrRestartButton = false;
                        e.Cancel = true;
                        LogHelper.WriteLogToFile("Ink Canvas closing cancelled at first confirmation", LogHelper.LogType.Event);
                        return;
                    }

                    var result2 = MessageBox.Show(Properties.MainWindowStrings.Main_CloseConfirm_Level2, "InkCanvasForClass",
                        MessageBoxButton.OKCancel, MessageBoxImage.Error);

                    if (result2 == MessageBoxResult.Cancel)
                    {
                        _forceCloseFromExitOrRestartButton = false;
                        e.Cancel = true;
                        LogHelper.WriteLogToFile("Ink Canvas closing cancelled at second confirmation", LogHelper.LogType.Event);
                        return;
                    }

                    var result3 = MessageBox.Show(Properties.MainWindowStrings.Main_CloseConfirm_Level3, "InkCanvasForClass",
                        MessageBoxButton.OKCancel, MessageBoxImage.Question);

                    if (result3 == MessageBoxResult.Cancel)
                    {
                        _forceCloseFromExitOrRestartButton = false;
                        e.Cancel = true;
                        LogHelper.WriteLogToFile("Ink Canvas closing cancelled at final confirmation", LogHelper.LogType.Event);
                        return;
                    }

                    e.Cancel = false;
                    LogHelper.WriteLogToFile("Ink Canvas closing confirmed by user", LogHelper.LogType.Event);
                }

                if (e.Cancel) LogHelper.WriteLogToFile("Ink Canvas closing cancelled", LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"关闭异常: {ex}", LogHelper.LogType.Error);
            }
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
                PPTTimeCapsule?.Dispose();

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

            // 清理统一窗口置顶管理器
            WindowTopmostManager.Shutdown();

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
        internal async Task<bool> DownloadUpdateWithFallback(string version, AutoUpdateHelper.UpdateLineGroup primaryGroup, UpdateChannel channel)
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

        public async void AutoUpdate()
        {
            try
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
                            try { await Dispatcher.InvokeAsync(() => SaveSettingsToFile()); } catch (TaskCanceledException) { } catch (ObjectDisposedException) { }
                        }
                    }
                }

                // 清除之前的更新状态，确保使用新通道重新检查
                AvailableLatestVersion = null;
                AvailableLatestLineGroup = null;
                AvailableLatestReleaseNotes = null;

                // 使用当前选择的更新通道检查更新
                var (remoteVersion, lineGroup, apiReleaseNotes) = await AutoUpdateHelper.CheckForUpdates(Settings.Startup.UpdateChannel);
                AvailableLatestVersion = remoteVersion;
                AvailableLatestLineGroup = lineGroup;
                AvailableLatestReleaseNotes = apiReleaseNotes;

                // 声明下载状态变量，用于整个方法
                bool isDownloadSuccessful = false;

                bool hasValidLineGroup = lineGroup != null;

                if (AvailableLatestVersion != null)
                {
                    try
                    {
                        await Dispatcher.InvokeAsync(() =>
                        {
                            timerCheckAutoUpdateRetry.Stop();
                            updateCheckRetryCount = 0;
                        });
                    }
                    catch (TaskCanceledException) { }
                    catch (ObjectDisposedException) { }

                    // 检测到新版本
                    LogHelper.WriteLogToFile($"AutoUpdate | New version available: {AvailableLatestVersion}");

                    var updateMessage = new NotificationMessage
                    {
                        Id = "update-" + AvailableLatestVersion,
                        Type = NotificationMessageType.Update,
                        Level = NotificationMessageLevel.Normal,
                        Title = NotificationStrings.UpdateTitle,
                        Summary = string.Format(NotificationStrings.NewVersion, AvailableLatestVersion),
                        Content = AvailableLatestReleaseNotes ?? string.Empty,
                        Icon = "Update",
                        ActionText = NotificationStrings.ViewDetails,
                        DisplaySeconds = Settings?.Notification?.UpdateDurationSeconds > 0 ? Settings.Notification.UpdateDurationSeconds : 3,
                        Source = "update",
                        Action = () =>
                        {
                            try
                            {
                                var settingsWindow = new SettingsWindow();
                                settingsWindow.Show();
                                settingsWindow.NavigateToPage("UpdatePage");
                            }
                            catch (Exception ex)
                            {
                                LogHelper.WriteLogToFile($"打开更新设置页失败: {ex.Message}", LogHelper.LogType.Warning);
                            }
                        }
                    };

                    NotificationCenterService.Enqueue(updateMessage);

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
                        try { await Dispatcher.InvokeAsync(() => SaveSettingsToFile()); } catch (TaskCanceledException) { } catch (ObjectDisposedException) { }
                    }

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
                            try { await Dispatcher.InvokeAsync(() => timerCheckAutoUpdateWithSilence.Start()); } catch (TaskCanceledException) { } catch (ObjectDisposedException) { }
                        }
                        else
                        {
                            LogHelper.WriteLogToFile("AutoUpdate | Silent update download failed", LogHelper.LogType.Error);
                        }

                        return;
                    }

                    // 如果没有启用静默更新，则记录日志并依赖 Toast 通知用户。
                    // 用户可在 设置 → 更新 中查看版本说明并选择更新方式。
                    LogHelper.WriteLogToFile(
                        $"AutoUpdate | New version {AvailableLatestVersion} available; user notified via toast, will act from settings page.");
                }
                else if (hasValidLineGroup)
                {
                    LogHelper.WriteLogToFile("AutoUpdate | Current version is already the latest, no retry needed");

                    try
                    {
                        await Dispatcher.InvokeAsync(() =>
                        {
                            timerCheckAutoUpdateRetry.Stop();
                            updateCheckRetryCount = 0;
                        });
                    }
                    catch (TaskCanceledException) { }
                    catch (ObjectDisposedException) { }
                }
                else
                {
                    // 检查更新失败，启动重试定时器
                    LogHelper.WriteLogToFile("AutoUpdate | Update check failed, starting retry timer");

                    // 重置重试计数
                    updateCheckRetryCount = 0;

                    // 启动重试定时器，10分钟后重新检查
                    try { await Dispatcher.InvokeAsync(() => timerCheckAutoUpdateRetry.Start()); } catch (TaskCanceledException) { } catch (ObjectDisposedException) { }

                    // 清理更新文件夹
                    AutoUpdateHelper.DeleteUpdatesFolder();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"AutoUpdate | Error in AutoUpdate: {ex.Message}", LogHelper.LogType.Error);
            }
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

            if (canvas.EditingMode == InkCanvasEditingMode.EraseByPoint)
            {
                canvas.UseCustomCursor = true;
                canvas.ForceCursor = true;
                canvas.Cursor = Cursors.None;
                return;
            }

            // 其他模式按照用户设置处理
            if (Settings.Canvas.IsShowCursor)
            {
                canvas.UseCustomCursor = true;
                canvas.ForceCursor = true;

                // 根据编辑模式和光标类型设置不同的光标
                if (canvas.EditingMode == InkCanvasEditingMode.Ink)
                {
                    int cursorType = Settings.Canvas.PenCursorType;
                    Cursor targetCursor = null;

                    switch (cursorType)
                    {
                        case 0: // 系统光标
                            targetCursor = Cursors.Arrow;
                            break;

                        case 2: // 用户自定义光标
                            targetCursor = LoadCustomCursor(Settings.Canvas.CustomPenCursorPath);
                            break;

                        default: // 1 - 软件内置光标（默认）
                            targetCursor = LoadBuiltInPenCursor();
                            break;
                    }

                    canvas.Cursor = targetCursor ?? LoadBuiltInPenCursor();
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

        private static Cursor LoadBuiltInPenCursor()
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
            return _cachedPenCursor;
        }

        private static Cursor LoadCustomCursor(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            lock (_cursorLock)
            {
                if (string.Equals(_cachedCustomCursorPath, path, StringComparison.OrdinalIgnoreCase) && _cachedCustomCursor != null)
                    return _cachedCustomCursor;

                try
                {
                    _cachedCustomCursor = new Cursor(path);
                    _cachedCustomCursorPath = path;
                    return _cachedCustomCursor;
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"加载自定义光标失败 ({path}): {ex.Message}", LogHelper.LogType.Error);
                    _cachedCustomCursor = null;
                    _cachedCustomCursorPath = null;
                    return null;
                }
            }
        }

        // 鼠标输入
        private void inkCanvas_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (IsCurrentPageFrozen && IsFreezeMutatingMode(inkCanvas.EditingMode))
            {
                TryBlockFrozenPageMutation("修改冻结页面");
                e.Handled = true;
                return;
            }

            // 使用辅助方法设置光标
            SetCursorBasedOnEditingMode(sender as InkCanvas);

            // 检查是否点击了空白区域或其他非图片元素
            var hitTest = e.OriginalSource;
            var dependencyObject = hitTest as DependencyObject;
            bool clickedMediaControl = false;
            while (dependencyObject != null)
            {
                if (dependencyObject is CanvasMediaControl)
                {
                    clickedMediaControl = true;
                    break;
                }
                dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
            }
            if (!(hitTest is Image) && !(hitTest is MediaElement) && !(hitTest is CanvasMediaControl) && !clickedMediaControl)
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
            _stylusDownTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            if (IsCurrentPageFrozen && IsFreezeMutatingMode(inkCanvas.EditingMode))
            {
                TryBlockFrozenPageMutation("修改冻结页面");
                e.Handled = true;
                return;
            }

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
        private async void ExitPPTSlideShow_MouseUp(object sender, MouseButtonEventArgs e)
        {
            await ExitPPTPresentation();
        }

        private void HistoryRollbackButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new Windows.SettingsViews.SettingsWindow();
            settingsWindow.Owner = this;
            settingsWindow.Show();
            settingsWindow.NavigateToPage("UpdatePage");
        }

        private DispatcherTimer autoSaveStrokesTimer;

        private void InstallKeyboardHook()
        {
            WindowSettingsHelper.InstallKeyboardHook();
        }

        private void UninstallKeyboardHook()
        {
            WindowSettingsHelper.UninstallKeyboardHook();
        }

        public void ApplyNoFocusMode()
        {
            WindowSettingsHelper.ApplyNoFocusMode(this);
        }

        public void ApplyAlwaysOnTop()
        {
            WindowSettingsHelper.ApplyAlwaysOnTop(this);
            _popupManager?.OnTopmostSettingChanged();
        }

        private void StartTopmostMaintenance()
        {
            WindowSettingsHelper.PauseTopmostMaintenance();
        }

        private void StopTopmostMaintenance()
        {
            WindowSettingsHelper.PauseTopmostMaintenance();
        }

        public void PauseTopmostMaintenance()
        {
            WindowSettingsHelper.PauseTopmostMaintenance();
        }

        public void ResumeTopmostMaintenance()
        {
            WindowSettingsHelper.ResumeTopmostMaintenance(this);
        }



        /// <summary>
        /// 根据窗口置顶设置和当前模式设置窗口的Topmost属性
        /// </summary>
        /// <param name="shouldBeTopmost">当前模式是否需要窗口置顶</param>
        public void SetTopmostBasedOnSettings(bool shouldBeTopmost)
        {
            WindowSettingsHelper.SetTopmostBasedOnSettings(this, shouldBeTopmost);
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            // WindowTopmostManager.Window_Activated 已负责重新强制 Z 序，无需重复调用 ApplyAlwaysOnTop()
            _popupManager?.OnOwnerActivated();
        }

        private async Task RunDeferredStartupPhaseBAsync()
        {
            if (_deferredPhaseBCompleted) return;
            _deferredPhaseBCompleted = true;

            await Task.Delay(600);

            try
            {
                AutoBackupManager.Initialize(Settings);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[MainWindow] 初始化自动备份管理器时出错: {ex.Message}", LogHelper.LogType.Error);
            }

            try
            {
                UploadQueueHelper.InitializeAllQueues();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[MainWindow] 初始化上传队列时出错: {ex.Message}", LogHelper.LogType.Error);
            }

            InitializeClipboardMonitoring();
            InitializeFloatingWindowInterceptor();
            InitializeGlobalHotkeyManager();

            _ = TelemetryUploader.UploadTelemetryIfNeededAsync();

            _ = Dispatcher.BeginInvoke(new Action(() =>
            {
                ApplyAlwaysOnTop();
            }), DispatcherPriority.ApplicationIdle);

            _ = Dispatcher.BeginInvoke(new Action(() =>
            {
                LoadInkFadeSettings();
                LoadBrushAutoRestoreSettings();
                InitializeInkFadeManager();
            }), DispatcherPriority.ApplicationIdle);

            _ = Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_sliderTouchSupportInitialized) return;
                AddTouchSupportToSliders();
                _sliderTouchSupportInitialized = true;
            }), DispatcherPriority.ApplicationIdle);

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

            InitializePPTManagers();
            if (Settings.PowerPointSettings.PowerPointSupport)
            {
                StartPPTMonitoring();
            }

            try
            {
                _windowOverviewModel = new WindowOverviewModel();
                LogHelper.WriteLogToFile("窗口概览模型已初始化", LogHelper.LogType.Event);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"初始化窗口概览模型失败: {ex.Message}", LogHelper.LogType.Error);
            }

            if (Settings.PowerPointSettings.EnablePowerPointEnhancement)
            {
                StartPowerPointProcessMonitoring();
            }

            if (_pendingStartupAutoUpdateCheck && Settings.Startup?.IsAutoUpdate == true)
            {
                _pendingStartupAutoUpdateCheck = false;
                await Task.Delay(8000);
                _ = Dispatcher.BeginInvoke(new Action(() =>
                {
                    LogHelper.WriteLogToFile("AutoUpdate | Running deferred auto-update check at UI idle");
                    _ = Task.Run(() => AutoUpdate());
                }), DispatcherPriority.ApplicationIdle);
            }
        }

        /// <summary>
        /// 窗口失去焦点时的处理
        /// </summary>
        private void Window_Deactivated(object sender, EventArgs e)
        {
            // 500ms 维护计时器会在下一个 tick 重新强制置顶，无需在此重复调用
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
                _inkFadeManager.IsEnabled = penType == 2 && Settings.Canvas.EnableInkFade;
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
        /// 应用多屏设置到全局热键管理器。
        /// </summary>
        public void ApplyMultiScreenSettings()
        {
            try
            {
                _globalHotkeyManager?.RefreshMultiScreenSettings();
                RefreshFloatingBarScreenFollowState();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用多屏设置时出错: {ex.Message}", LogHelper.LogType.Error);
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


        private void ToggleSwitchEnableInkToShape_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
                var toggle = sender as ToggleSwitch;
                if (toggle == null) return;

                if (sender == FloatingBarToggleSwitchEnableInkToShape)
                    BoardToggleSwitchEnableInkToShape.IsOn = FloatingBarToggleSwitchEnableInkToShape.IsOn;
                else
                    FloatingBarToggleSwitchEnableInkToShape.IsOn = BoardToggleSwitchEnableInkToShape.IsOn;

                Settings.InkToShape.IsInkToShapeEnabled = FloatingBarToggleSwitchEnableInkToShape.IsOn;
                SaveSettingsToFile();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"切换墨迹纠正功能时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void ToggleSwitchShowCircleCenter_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
                var toggle = sender as ToggleSwitch;
                if (toggle == null) return;
                Settings.Canvas.ShowCircleCenter = toggle.IsOn;
                SaveSettingsToFile();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"切换圆心显示时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void ComboBoxEraserSizeFloatingBar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (!isLoaded) return;
                var comboBox = sender as System.Windows.Controls.ComboBox;
                if (comboBox == null) return;

                Settings.Canvas.EraserSize = comboBox.SelectedIndex;
                SaveSettingsToFile();

                if (comboBox.Name == "ComboBoxEraserSizeFloatingBar" && BoardComboBoxEraserSize != null)
                {
                    BoardComboBoxEraserSize.SelectedIndex = comboBox.SelectedIndex;
                }
                else if (comboBox.Name == "BoardComboBoxEraserSize" && ComboBoxEraserSizeFloatingBar != null)
                {
                    ComboBoxEraserSizeFloatingBar.SelectedIndex = comboBox.SelectedIndex;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"切换橡皮擦大小时出错: {ex.Message}", LogHelper.LogType.Error);
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
                if (IsInPPTPresentationMode)
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
                var comboBox = sender as System.Windows.Controls.ComboBox;
                if (comboBox != null)
                {
                    Settings.PowerPointSettings.PPTTimeCapsulePosition = comboBox.SelectedIndex;
                    SaveSettingsToFile();

                    if (IsInPPTPresentationMode)
                    {
                        UpdatePPTTimeCapsulePosition();
                    }

                    LogHelper.WriteLogToFile($"PPT时间胶囊位置已更改为: {comboBox.SelectedIndex}", LogHelper.LogType.Event);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更改PPT时间胶囊位置时出错: {ex.Message}", LogHelper.LogType.Error);
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
                    IsInPPTPresentationMode)
                {
                    PPTTimeCapsuleContainer.Visibility = Visibility.Visible;
                    UpdatePPTTimeCapsulePosition();
                    UpdatePPTTimeCapsuleOpacity();
                    UpdatePPTTimeCapsuleScale();
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
                bool inSlideShow = IsInPPTPresentationMode;
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
        public void UpdatePPTTimeCapsulePosition()
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
                        PPTTimeCapsuleContainer.RenderTransformOrigin = new Point(0, 0);
                        break;
                    case 1: // 右上角
                        PPTTimeCapsuleContainer.HorizontalAlignment = HorizontalAlignment.Right;
                        PPTTimeCapsuleContainer.VerticalAlignment = VerticalAlignment.Top;
                        PPTTimeCapsuleContainer.Margin = new Thickness(0, 20, 20, 0);
                        PPTTimeCapsuleContainer.RenderTransformOrigin = new Point(1, 0);
                        break;
                    case 2: // 顶部居中
                        PPTTimeCapsuleContainer.HorizontalAlignment = HorizontalAlignment.Center;
                        PPTTimeCapsuleContainer.VerticalAlignment = VerticalAlignment.Top;
                        PPTTimeCapsuleContainer.Margin = new Thickness(0, 20, 0, 0);
                        PPTTimeCapsuleContainer.RenderTransformOrigin = new Point(0.5, 0);
                        break;
                }

                // 应用拖拽偏移
                if (PPTTimeCapsule != null)
                {
                    PPTTimeCapsule.ApplyDragOffset(
                        Settings.PowerPointSettings.PPTTimeCapsuleOffsetX,
                        Settings.PowerPointSettings.PPTTimeCapsuleOffsetY);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更新PPT时间胶囊位置时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 更新PPT时间胶囊的透明度
        /// </summary>
        public void UpdatePPTTimeCapsuleOpacity()
        {
            try
            {
                if (PPTTimeCapsuleContainer == null) return;
                PPTTimeCapsuleContainer.Opacity = Settings.PowerPointSettings.PPTTimeCapsuleOpacity;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更新PPT时间胶囊透明度时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 更新PPT时间胶囊的大小
        /// </summary>
        public void UpdatePPTTimeCapsuleScale()
        {
            try
            {
                if (PPTTimeCapsuleScaleTransform == null) return;
                double scale = Settings.PowerPointSettings.PPTTimeCapsuleScale;
                PPTTimeCapsuleScaleTransform.ScaleX = scale;
                PPTTimeCapsuleScaleTransform.ScaleY = scale;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"更新PPT时间胶囊大小时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 保存PPT时间胶囊拖拽偏移量
        /// </summary>
        public void SavePPTTimeCapsuleOffset(double offsetX, double offsetY)
        {
            try
            {
                Settings.PowerPointSettings.PPTTimeCapsuleOffsetX = offsetX;
                Settings.PowerPointSettings.PPTTimeCapsuleOffsetY = offsetY;
                SaveSettingsToFile();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"保存PPT时间胶囊位置偏移时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 重置PPT时间胶囊拖拽偏移量
        /// </summary>
        public void ResetPPTTimeCapsuleOffset()
        {
            try
            {
                Settings.PowerPointSettings.PPTTimeCapsuleOffsetX = 0;
                Settings.PowerPointSettings.PPTTimeCapsuleOffsetY = 0;
                PPTTimeCapsule?.ResetDragOffset();
                SaveSettingsToFile();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"重置PPT时间胶囊位置时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }


        /// <summary>
        /// 初始化文件关联状态显示
        /// </summary>
        private void InitializeFileAssociationStatus()
        {
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
                            ShowNotification(string.Format(Properties.MainWindowStrings.Main_StrokesFileLoaded, Path.GetFileName(icstkFile)));
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLogToFile($"打开命令行参数中的文件失败: {ex.Message}", LogHelper.LogType.Error);
                            ShowNotification(Properties.MainWindowStrings.Main_StrokesFileOpenFailed);
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
        internal bool SetCurrentToolMode(InkCanvasEditingMode newMode, Action additionalActions = null)
        {
            try
            {
                if (IsCurrentPageFrozen && IsFreezeMutatingMode(newMode))
                {
                    TryBlockFrozenPageMutation("切换到编辑工具");
                    return false;
                }

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

                // 在PPT放映模式下，工具模式切换时需要更新工具栏组件的显示状态
                if (IsInPPTPresentationMode)
                {
                    UpdateToolbarComponentVisibility();
                }

                // 执行额外的操作（如果有）
                additionalActions?.Invoke();
                return true;

            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"设置工具模式时出错: {ex.Message}", LogHelper.LogType.Error);
                return false;
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
                    BoardPenWidthSlider,
                    BoardPenAlphaSlider,
                    PenWidthSlider,
                    PenAlphaSlider
                };

                foreach (var slider in sliders)
                {
                    if (slider != null)
                    {
                        Helpers.SliderTouchHelper.AddTouchSupport(slider);
                    }
                }

                LogHelper.WriteLogToFile("已为所有滑块控件添加触摸支持", LogHelper.LogType.Trace);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"添加滑块触摸支持时出错: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        #endregion

        #region 模式切换相关



        /// <summary>
        /// 检查是否应该显示主窗口（基于PPT模式和PPT放映状态）
        /// </summary>
        internal void CheckMainWindowVisibility()
        {
            try
            {
                if (!IsLoaded)
                    return;

                if (Settings.ModeSettings.IsPPTOnlyMode)
                {
                    if (TrayTemporaryShowUntilUtc.HasValue && DateTime.UtcNow < TrayTemporaryShowUntilUtc.Value)
                    {
                        if (!IsVisible)
                            Show();
                        return;
                    }

                    // 仅PPT模式：以 COM/UI 状态为主，Win32 检测全屏放映窗口（screenClass）作兜底，避免 COM 异常时无法唤出
                    bool comUiSlideShow = IsInPPTPresentationMode;
                    bool win32SlideShow = IsPowerPointSlideshowSurfacePresentWin32();
                    bool isInSlideShow = comUiSlideShow || win32SlideShow;
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
            catch (InvalidOperationException ex) when (ex.Message.Contains("Close") || ex.Message.Contains(NotificationStrings.AnimationOff) || ex.Message.Contains("Show") || ex.Message.Contains("Visibility"))
            {
                // 窗口已关闭，忽略此异常
                LogHelper.WriteLogToFile($"检查主窗口可见性时发现窗口已关闭，忽略异常。", LogHelper.LogType.Trace);
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

        // ComboBoxTheme_SelectionChanged and ComboBoxLanguage_SelectionChanged migrated to AppearancePage


        /// <summary>
        /// 应用指定主题
        /// </summary>
        /// <param name="themeIndex">主题索引：0-浅色，1-深色，2-跟随系统</param>
        internal void ApplyTheme(int themeIndex)
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
                        if (ThemeHelper.IsSystemThemeLight())
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
        /// 应用UIA置顶功能
        /// </summary>
        public void ApplyUIAccessTopMost()
        {
            WindowSettingsHelper.ApplyUIAccessTopMost(this);
        }

        internal void OpenQuickDrawFromHotkey()
        {
            try
            {
                if (Settings?.RandSettings?.EnableQuickDraw != true)
                    return;

                var quickDrawWindow = new QuickDrawWindow();
                quickDrawWindow.Owner = this;
                quickDrawWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"打开快抽窗口失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 显示快抽悬浮按钮
        /// </summary>
        public void ShowQuickDrawFloatingButton()
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
