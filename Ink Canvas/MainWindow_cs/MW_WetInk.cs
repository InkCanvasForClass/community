using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Ink;
using System.Windows.Interop;
using System.Windows.Media;
using Ink_Canvas.Helpers;
using Ink_Canvas.Ink.WetInk;
using Ink_Canvas.Properties;
using global::Windows.UI.Input.Inking;

namespace Ink_Canvas
{
    /// <summary>
    /// 新墨迹引擎（WinRT InkPresenter）集成。
    ///
    /// 分层：WetInkHostWindow（覆盖窗口）→ WetInkPresenterBridge（IInkDesktopHost + DComp）
    /// → WetInkTouchClassifier（四边红外手掌分类）→ WetInkCommitSink（湿→干提交）
    /// → WetInkRouter（chrome 排除矩形）。
    ///
    /// 引擎只在「逻辑笔工具（pen/color）」激活时接管输入：把覆盖窗口定位到画布上方、
    /// InkPresenter 处理笔/触摸/鼠标，物理 EditingMode 保持 None（防 WPF 内置重复捕获）；
    /// 其它工具（橡皮/选择/漫游/光标/形状）时覆盖窗口停靠离屏，全部交回 WPF。
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>新墨迹引擎是否已成功初始化并接管笔输入。</summary>
        internal static bool IsWetInkEngineActive { get; private set; }

        private WetInkHostWindow _wetInkHostWindow;
        private WetInkPresenterBridge _wetInkPresenterBridge;
        private WetInkTouchClassifier _wetInkTouchClassifier;
        private WetInkCommitSink _wetInkCommitSink;
        private WetInkRouter _wetInkRouter;
        private WetInkPredictionSession _wetInkPredictionSession;
        private WetInkPredictionOverlayWindow _wetInkPredictionWindow;
        private readonly DelayAction _wetInkRefreshDelayAction = new DelayAction();
        private readonly DelayAction _wetInkStyleRefreshDelayAction = new DelayAction();
        private long _wetInkPenSampleCount;
        private IntPtr _mainWindowHwnd;
        private bool _wetInkStarted;
        private bool _wetInkPenToolActive;
        private bool _wetInkIsShuttingDown;

        // ---------------- 生命周期 ----------------

        /// <summary>启动引擎（应用 Loaded 后调用）。任何初始化失败都回退旧墨迹路径。</summary>
        internal void StartWetInkEngine()
        {
            if (_wetInkStarted || IsWetInkEngineActive) return;
            // UseLegacyInkSystem=true → 旧 WPF InkCanvas 路径（重启生效后由用户选择）。
            if (Settings?.Canvas?.UseLegacyInkSystem == true)
            {
                LogHelper.WriteLogToFile("配置了 UseLegacyInkSystem=true，保持使用传统 WPF InkCanvas 墨迹系统", LogHelper.LogType.Event);
                return;
            }
            try
            {
                LogHelper.WriteLogToFile("正在初始化新墨迹引擎（WinRT InkPresenter）...", LogHelper.LogType.Event);
                _mainWindowHwnd = new WindowInteropHelper(this).Handle;
                if (_mainWindowHwnd == IntPtr.Zero)
                    throw new InvalidOperationException("主窗口句柄无效");

                _wetInkHostWindow = new WetInkHostWindow(_mainWindowHwnd);

                _wetInkPresenterBridge = new WetInkPresenterBridge();
                var dpiScale = GetDpiScale();
                var initWidthPx = Math.Max(1, (float)(ActualWidth * dpiScale));
                var initHeightPx = Math.Max(1, (float)(ActualHeight * dpiScale));
                LogHelper.WriteLogToFile($"新墨迹覆盖窗口初始尺寸: {ActualWidth}x{ActualHeight} Dip, DPI Scale={dpiScale:F2}", LogHelper.LogType.Event);
                if (!_wetInkPresenterBridge.Initialize(_wetInkHostWindow.Hwnd, initWidthPx, initHeightPx))
                    throw new InvalidOperationException("InkPresenter 初始化失败");

                _wetInkTouchClassifier = new WetInkTouchClassifier();
                _wetInkHostWindow.ContactSample += OnWetInkContactSample;
                _wetInkHostWindow.PenSample += OnWetInkPenSample;
                _wetInkHostWindow.ContactUp += OnWetInkContactUp;
                _wetInkPresenterBridge.StrokesCollected += OnWetInkStrokesCollected;

                _wetInkRouter = new WetInkRouter();

                _wetInkCommitSink = new WetInkCommitSink(
                    Dispatcher, _wetInkPresenterBridge, CommitWetInkStrokeToDryLayer);
                _wetInkPredictionSession = new WetInkPredictionSession();
                _wetInkPredictionWindow = new WetInkPredictionOverlayWindow();

                LocationChanged -= OnWetInkWindowLocationChanged;
                LocationChanged += OnWetInkWindowLocationChanged;

                _wetInkStarted = true;
                IsWetInkEngineActive = true;

                LogHelper.WriteLogToFile("新墨迹引擎（WinRT InkPresenter）启动成功，已就绪", LogHelper.LogType.Event);
                SyncWetInkEngineWithLogicalTool();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"新墨迹引擎启动失败，触发回退保护退回旧墨迹路径: {ex}", LogHelper.LogType.Error);
                ShutdownWetInkEngineCore();
            }
        }

        /// <summary>关闭引擎并释放资源（窗口关闭时调用）。</summary>
        internal void ShutdownWetInkEngine()
        {
            LogHelper.WriteLogToFile("请求关闭新墨迹引擎...", LogHelper.LogType.Event);
            _wetInkIsShuttingDown = true;
            ShutdownWetInkEngineCore();
        }

        private void ShutdownWetInkEngineCore()
        {
            if (_wetInkHostWindow != null)
            {
                _wetInkHostWindow.ContactSample -= OnWetInkContactSample;
                _wetInkHostWindow.PenSample -= OnWetInkPenSample;
                _wetInkHostWindow.ContactUp -= OnWetInkContactUp;
                _wetInkHostWindow.ParkOffscreen();
                _wetInkHostWindow.Dispose();
                _wetInkHostWindow = null;
            }

            LocationChanged -= OnWetInkWindowLocationChanged;

            if (_wetInkPredictionWindow != null)
            {
                _wetInkPredictionWindow.Dispose();
                _wetInkPredictionWindow = null;
            }
            _wetInkPredictionSession = null;

            if (_wetInkPresenterBridge != null)
            {
                _wetInkPresenterBridge.StrokesCollected -= OnWetInkStrokesCollected;
                _wetInkPresenterBridge.SetInputEnabled(false);
                _wetInkPresenterBridge.Dispose();
                _wetInkPresenterBridge = null;
            }

            _wetInkCommitSink = null;
            _wetInkRouter = null;
            _wetInkTouchClassifier = null;
            _wetInkStarted = false;
            _wetInkPenToolActive = false;
            IsWetInkEngineActive = false;

            if (inkCanvas != null && (_currentToolMode == "pen" || _currentToolMode == "color"))
            {
                inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
                LogHelper.WriteLogToFile("新墨迹引擎已关闭，恢复 WPF InkCanvas EditingMode=Ink", LogHelper.LogType.Event);
            }
        }

        /// <summary>按逻辑工具同步引擎：笔工具激活接管，其它工具停靠覆盖窗口。</summary>
        internal void SyncWetInkEngineWithLogicalTool()
        {
            if (!_wetInkStarted || _wetInkIsShuttingDown) return;
            if (!IsWetInkEngineActive || _wetInkPresenterBridge == null || _wetInkHostWindow == null)
                return;

            var tool = ResolveWetInkLogicalTool();
            var penTool = WetInkEnginePolicy.IsPenToolActive(tool, IsCurrentPageFrozen);

            if (penTool == _wetInkPenToolActive) return;

            _wetInkPenToolActive = penTool;
            LogHelper.WriteLogToFile(
                $"新墨迹引擎同步工具: {(penTool ? "笔激活" : "笔释放")} mode={_currentToolMode}",
                LogHelper.LogType.Trace);

            if (penTool)
            {
                try
                {
                    var style = BuildWetInkStyleSnapshot();
                    if (_wetInkCommitSink != null)
                        _wetInkCommitSink.CurrentStyle = style;
                    _wetInkPresenterBridge.UpdateDrawingAttributes(style);
                    UpdateWetInkTarget();
                    _wetInkHostWindow.BringToFront();
                    _wetInkPresenterBridge.SetInputEnabled(true);

                    if (inkCanvas.EditingMode != InkCanvasEditingMode.None)
                        inkCanvas.EditingMode = InkCanvasEditingMode.None;
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"新墨迹引擎激活失败: {ex}", LogHelper.LogType.Error);
                }
            }
            else
            {
                _wetInkPredictionSession?.EndStroke();
                _wetInkPredictionWindow?.Clear();
                _wetInkPresenterBridge.SetInputEnabled(false);
                _wetInkTouchClassifier.Reset();
                _wetInkHostWindow.ParkOffscreen();
            }
        }

        /// <summary>延迟重建覆盖层位置与 chrome 排除区（工具栏/弹窗/窗口移动后调用）。</summary>
        private void RefreshWetInkTargetSoon()
        {
            if (!_wetInkStarted) return;
            _wetInkRefreshDelayAction.DebounceAction(80, null,
                () => Dispatcher.BeginInvoke(UpdateWetInkTarget));
        }

        /// <summary>延迟同步笔样式（颜色/粗细/荧光笔/激光笔切换后调用）。</summary>
        private void RefreshWetInkStyleSoon()
        {
            if (!_wetInkStarted) return;
            _wetInkStyleRefreshDelayAction.DebounceAction(80, null,
                () => Dispatcher.BeginInvoke(UpdateWetInkStyle));
        }

        private void UpdateWetInkStyle()
        {
            if (!_wetInkStarted || !_wetInkPenToolActive || _wetInkPresenterBridge == null) return;
            var style = BuildWetInkStyleSnapshot();
            if (_wetInkCommitSink != null)
                _wetInkCommitSink.CurrentStyle = style;
            _wetInkPresenterBridge.UpdateDrawingAttributes(style);
        }

        private void OnWetInkPopupCollectionChanged()
        {
            RefreshWetInkTargetSoon();
        }

        private void OnWetInkWindowLocationChanged(object sender, EventArgs e)
        {
            RefreshWetInkTargetSoon();
        }

        /// <summary>重定位覆盖窗口 + 重建 chrome 排除区域（尺寸/DPI/换屏时调用）。</summary>
        internal void UpdateWetInkTarget()
        {
            if (!_wetInkStarted || _wetInkIsShuttingDown) return;
            if (!IsWetInkEngineActive || _wetInkHostWindow == null || _wetInkRouter == null)
                return;

            // 非笔工具时覆盖窗口必须保持停靠离屏，否则透明窗口会挡住主窗口全部点击。
            if (!_wetInkPenToolActive)
            {
                _wetInkHostWindow.ParkOffscreen();
                return;
            }

            try
            {
                var dpiScale = GetDpiScale();
                var clientOrigin = PointToScreen(new Point(0, 0));
                // 视觉树收集 chrome（屏幕 DIP，不能再乘 dpiScale）；Popup 用真实 HWND 矩形。
                var popupRects = new List<Rect>();
                foreach (var popup in GetPopupManager()?.GetOpenPopupsSnapshot() ?? Array.Empty<Popup>())
                {
                    if (popup?.Child == null) continue;
                    try
                    {
                        var source = PresentationSource.FromVisual(popup.Child) as HwndSource;
                        if (source?.Handle == IntPtr.Zero) continue;
                        if (!GetWindowRect(source.Handle, out var r)) continue;
                        if (r.Right > r.Left && r.Bottom > r.Top)
                            popupRects.Add(new Rect(
                                r.Left / dpiScale, r.Top / dpiScale,
                                (r.Right - r.Left) / dpiScale,
                                (r.Bottom - r.Top) / dpiScale));
                    }
                    catch { }
                }

                var exclusionRects = _wetInkRouter.BuildAllChromeRects(
                    this,
                    clientOrigin,
                    new[] { InkCanvasGridForInkReplay, GridBackgroundCoverHolder },
                    popupRects);
                foreach (Window w in System.Windows.Application.Current.Windows)
                {
                    if (w == this || w.Visibility != Visibility.Visible) continue;
                    try
                    {
                        var hwnd = new WindowInteropHelper(w).Handle;
                        if (hwnd == IntPtr.Zero) continue;
                        if (!GetWindowRect(hwnd, out var r)) continue;
                        if (r.Right > r.Left && r.Bottom > r.Top)
                            // GetWindowRect 返回物理像素 → 屏幕 DIP
                            exclusionRects.Add(new Rect(
                                r.Left / dpiScale, r.Top / dpiScale,
                                (r.Right - r.Left) / dpiScale, (r.Bottom - r.Top) / dpiScale));
                    }
                    catch { }
                }

                _wetInkHostWindow.UpdateTarget(
                    dpiScale,
                    clientOrigin.X,
                    clientOrigin.Y,
                    ActualWidth,
                    ActualHeight,
                    exclusionRects);

                var rectDetails = exclusionRects.Count == 0 ? "" :
                    " [" + string.Join(" | ", exclusionRects.Select(r =>
                        $"({r.X:0},{r.Y:0},{r.Width:0}x{r.Height:0})")) + "]";
                LogHelper.WriteLogToFile(
                    $"新墨迹引擎覆盖窗口: origin=({clientOrigin.X:0},{clientOrigin.Y:0}) size=({ActualWidth:0}x{ActualHeight:0}) " +
                    $"排除区={exclusionRects.Count}个{rectDetails}",
                    LogHelper.LogType.Trace);

                _wetInkPresenterBridge?.UpdateTargetSize(
                    Math.Max(1, (float)(ActualWidth * dpiScale)),
                    Math.Max(1, (float)(ActualHeight * dpiScale)));

                var inkOrigin = GetInkCanvasOriginInWindowDip();
                if (_wetInkCommitSink != null)
                {
                    _wetInkCommitSink.InkCanvasOffsetXDip = inkOrigin.X;
                    _wetInkCommitSink.InkCanvasOffsetYDip = inkOrigin.Y;
                    // InkPresenter 坐标是物理像素(SetSize 用物理 px),干层用 DIP
                    _wetInkCommitSink.PointsToDipScale = dpiScale > 0 ? 1.0 / dpiScale : 1.0;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"新墨迹引擎更新目标失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        // ---------------- 逻辑工具 ----------------

        private WetInkLogicalTool ResolveWetInkLogicalTool()
        {
            if (IsBoardRoamingMode)
                return WetInkLogicalTool.BoardRoam;
            if (drawingShapeMode != 0
                || string.Equals(_currentToolMode, "shape", StringComparison.OrdinalIgnoreCase))
                return WetInkLogicalTool.Shape;

            switch (_currentToolMode)
            {
                case "pen":
                case "color":
                    return WetInkLogicalTool.Pen;
                case "eraser":
                    return WetInkLogicalTool.PointEraser;
                case "eraserByStrokes":
                    return WetInkLogicalTool.StrokeEraser;
                case "select":
                    return WetInkLogicalTool.Select;
                case "roaming":
                    return WetInkLogicalTool.BoardRoam;
                case "cursor":
                default:
                    return WetInkLogicalTool.Cursor;
            }
        }

        // ---------------- 输入分类 / 手掌擦除 ----------------

        private void OnWetInkContactSample(
            uint pointerId, bool isTouch, double contactWidthDip, double contactHeightDip, double xDip, double yDip)
        {
            if (!IsWetInkEngineActive || !_wetInkPenToolActive) return;
            if (_wetInkTouchClassifier == null) return;

            try
            {
                var policy = BuildWetInkPalmPolicy();
                var kind = _wetInkTouchClassifier.Classify(
                    policy, pointerId, isTouch, contactWidthDip, contactHeightDip);

                if (kind == WetInkContactKind.Palm)
                {
                    var eraserWidth = _wetInkTouchClassifier.GetPalmEraserWidthDip(
                        policy, contactWidthDip, contactHeightDip);
                    EraseDryStrokesUnderPalm(xDip, yDip, eraserWidth);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"手掌分类失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        private void OnWetInkContactUp(uint pointerId)
        {
            _wetInkTouchClassifier?.OnPointerUp(pointerId);
            _wetInkPredictionSession?.EndStroke();
            _wetInkPredictionWindow?.Clear();
        }

        /// <summary>把覆盖窗口的实时笔采样喂给预测会话并刷新预测尾预览。</summary>
        private void OnWetInkPenSample(
            uint pointerId, double xDip, double yDip, float pressure, long timestampMicroseconds)
        {
            if (!IsWetInkEngineActive || !_wetInkPenToolActive) return;
            if (_wetInkPredictionSession == null || _wetInkPredictionWindow == null) return;

            try
            {
                var inkOrigin = GetInkCanvasOriginInWindowDip();
                _wetInkPredictionSession.OnRealSample(
                    xDip - inkOrigin.X,
                    yDip - inkOrigin.Y,
                    pressure,
                    timestampMicroseconds);
                UpdateWetInkPredictionOverlay();

                _wetInkPenSampleCount++;
                if ((_wetInkPenSampleCount & 0x1FF) == 0)
                {
                    LogHelper.WriteLogToFile(
                        $"新墨迹预测: 真实采样={_wetInkPredictionSession.RealPointCount} " +
                        $"预测点={_wetInkPredictionSession.PredictedPoints.Count}",
                        LogHelper.LogType.Trace);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"墨迹预测会话失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        private void UpdateWetInkPredictionOverlay()
        {
            if (_wetInkPredictionSession == null || _wetInkPredictionWindow == null) return;

            var realPoints = _wetInkPredictionSession.GetRecentRealPoints();
            var predictedPoints = _wetInkPredictionSession.PredictedPoints;
            if (realPoints.Count == 0 && predictedPoints.Count == 0)
            {
                _wetInkPredictionWindow.Clear();
                return;
            }

            var inkOrigin = GetInkCanvasOriginInWindowDip();
            var screenOrigin = PointToScreen(new Point(0, 0));
            var da = inkCanvas?.DefaultDrawingAttributes;
            var color = da?.Color ?? Colors.Black;
            var width = da?.Width ?? 2.0;
            var highlighter = da?.IsHighlighter == true;
            var laser = penType == 2;

            _wetInkPredictionWindow.UpdatePrediction(
                realPoints,
                predictedPoints,
                inkOrigin.X,
                inkOrigin.Y,
                screenOrigin.X,
                screenOrigin.Y,
                ActualWidth,
                ActualHeight,
                width,
                color,
                highlighter,
                laser);
        }

        /// <summary>手掌擦除：移除与手掌接触矩形相交的干层笔画（TimeMachine 走 StrokesChanged 记录撤销）。</summary>
        private void EraseDryStrokesUnderPalm(double xDip, double yDip, double eraserWidthDip)
        {
            if (inkCanvas == null || inkCanvas.Strokes.Count == 0) return;

            try
            {
                var inkOrigin = GetInkCanvasOriginInWindowDip();
                var center = new Point(xDip - inkOrigin.X, yDip - inkOrigin.Y);
                var rect = new Rect(
                    center.X - eraserWidthDip,
                    center.Y - eraserWidthDip,
                    eraserWidthDip * 2,
                    eraserWidthDip * 2);

                var toRemove = new StrokeCollection();
                foreach (Stroke stroke in inkCanvas.Strokes)
                {
                    try
                    {
                        if (stroke.GetBounds().IntersectsWith(rect))
                            toRemove.Add(stroke);
                    }
                    catch
                    {
                        // 单条笔画边界异常不影响其余笔画。
                    }
                }

                if (toRemove.Count > 0)
                    inkCanvas.Strokes.Remove(toRemove);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"手掌擦除失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        // ---------------- 湿→干提交 ----------------

        private void OnWetInkStrokesCollected(object sender, IReadOnlyList<InkStroke> strokes)
        {
            if (!IsWetInkEngineActive) return;
            LogHelper.WriteLogToFile($"新墨迹引擎收集到湿墨笔画: {strokes?.Count ?? 0} 条", LogHelper.LogType.Trace);
            _wetInkCommitSink?.OnStrokesCollected(sender, strokes);
        }

        /// <summary>把 InkPresenter 转好的 WPF Stroke 进干层并复用既有后处理（撤销走 StrokesChanged）。</summary>
        private void CommitWetInkStrokeToDryLayer(Stroke stroke)
        {
            if (inkCanvas == null || stroke == null) return;

            var bounds = stroke.GetBounds();
            LogHelper.WriteLogToFile($"湿墨烘干提交到干层: 点数={stroke.StylusPoints.Count}, 边界=({bounds.X:F0},{bounds.Y:F0},{bounds.Width:F0}x{bounds.Height:F0}), 粗细={stroke.DrawingAttributes.Width:F1}", LogHelper.LogType.Trace);

            inkCanvas.Strokes.Add(stroke);
            ProcessCommittedStroke(stroke);
        }

        // ---------------- 样式 / 手掌策略 / 坐标 ----------------

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out Win32RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct Win32RECT { public int Left, Top, Right, Bottom; }

        /// <summary>当前笔属性（inkCanvas.DefaultDrawingAttributes）→ 引擎样式快照。</summary>
        private WetInkStyleSnapshot BuildWetInkStyleSnapshot()
        {
            var da = inkCanvas.DefaultDrawingAttributes;
            var winColor = global::Windows.UI.Color.FromArgb(da.Color.A, da.Color.R, da.Color.G, da.Color.B);
            var penTip = da.StylusTip == StylusTip.Rectangle
                ? PenTipShape.Rectangle
                : PenTipShape.Circle;
            var dpiScale = GetDpiScale();

            return new WetInkStyleSnapshot(
                winColor,
                Math.Max(0.1, da.Width * dpiScale),
                Math.Max(0.1, da.Height * dpiScale),
                da.FitToCurve,
                da.IgnorePressure,
                da.IsHighlighter,
                penTip,
                isLaser: penType == 2);
        }

        /// <summary>手掌判定策略（与旧 BuildPalmRoutePolicy 公式一致，设置不变）。</summary>
        private WetInkPalmPolicy BuildWetInkPalmPolicy()
        {
            var canvas = Settings.Canvas;
            var advanced = Settings.Advanced;
            var isNib = Settings.Startup.IsEnableNibMode;

            double sensitivity;
            switch (canvas.PalmEraserSensitivity)
            {
                case 0:
                    sensitivity = 3.0;
                    break;
                case 1:
                    sensitivity = 2.5;
                    break;
                default:
                    sensitivity = 2.0;
                    break;
            }

            return new WetInkPalmPolicy(
                enabled: canvas.EnablePalmEraser,
                isQuadIr: advanced.IsQuadIR,
                isSpecialScreen: advanced.IsSpecialScreen,
                boundsWidthDip: BoundsWidth,
                thresholdFactor: isNib
                    ? advanced.NibModeBoundsWidthThresholdValue
                    : advanced.FingerModeBoundsWidthThresholdValue,
                sensitivityMultiplier: sensitivity,
                eraserSizeFactor: isNib
                    ? advanced.NibModeBoundsWidthEraserSize
                    : advanced.FingerModeBoundsWidthEraserSize,
                touchMultiplier: advanced.TouchMultiplier);
        }

        private Point GetInkCanvasOriginInWindowDip()
        {
            if (inkCanvas == null) return new Point(0, 0);
            try
            {
                return inkCanvas.TransformToAncestor(this).Transform(new Point(0, 0));
            }
            catch
            {
                return new Point(0, 0);
            }
        }
    }
}
