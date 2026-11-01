using Ink_Canvas.Helpers;
using Ink_Canvas.Ink;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Ink;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Ink_Canvas
{
    /// <summary>
    /// 手掌擦适配层。识别与尺寸计算全部交给 <see cref="PalmEraserEngine"/>，
    /// 这里只负责 InkCanvas 的触摸会话、湿墨取消、几何擦除和撤销。
    /// 当前版本只允许在“校准有效且环境匹配”时启用；旧阈值回退留待后续版本。
    /// </summary>
    public partial class MainWindow
    {
        private readonly PalmEraserEngine _palmEngine = new();
        private readonly HashSet<int> _palmSuppressedTouches = new();
        private readonly HashSet<uint> _palmSuppressedPointers = new();
        private TouchDevice _palmTouchDevice;
        private bool _changingPalmMode;
        private bool _palmPreviousManipulation;
        private bool _palmPreviousStrokeEraser;
        private double _palmPreviousWidth;
        private double _palmHeight;
        private Point? _palmLastPoint;
        private int _palmRevision;
        private IDisposable _palmHidSubscription;
        private bool _palmHidActive;
        private PointerWritingSource _palmPointerWritingSource;
        private bool _palmPointerWritingActive;
        private RawMouseWritingSource _palmRawMouseWritingSource;
        private bool _palmRawMouseWritingActive;
        private readonly Dictionary<int, ExternalTouchStroke> _palmExternalStrokes = new();
        private long _palmLastHidContactTicks;
        private int _palmNextExternalVisualId = -100000;
        private DispatcherTimer _palmHidTimer;
        private PalmEraserProfile _palmCachedProfile;
        private long _palmCachedProfileTicks;
        private long _palmCachedProfileStamp;
        private bool _palmInputHooked;
        private bool _palmExternalErasing;
        private bool _palmInputSuspended;

        /// <summary>HID 接触对应的外部墨迹；HID 书写时由这里接管 WPF 触摸不画的笔迹。</summary>
        private sealed class ExternalTouchStroke
        {
            public StrokeVisual Visual;
            public int VisualId;
            public long LastTicks;
        }
        private int _palmInputChannel = -1;
#if ICC_NATIVE_WET_INK
        private uint? _palmNativePointer;
#endif

        private InkCanvasEditingMode GetPalmLogicalEditingMode()
        {
            var mode = inkCanvas?.EditingMode ?? InkCanvasEditingMode.None;
            if (mode != InkCanvasEditingMode.None) return mode;
            return isPalmEraserActive || _changingPalmMode ? palmEraserPreviousEditingMode : mode;
        }

        /// <summary>
        /// 当前构建的手掌擦必须依赖一份有效校准。这里同时做按钮/模式门控，
        /// 校准缺失时返回 false，触摸继续按普通笔处理。
        /// </summary>
        private bool CanStartPalmEraser()
        {
            return Settings.Canvas.EnablePalmEraser && HasValidPalmCalibration()
                && !IsCurrentPageFrozen
                && !_isVideoPresenterSpecialMode && !IsBoardRoamingMode
                && !isSingleFingerDragMode && drawingShapeMode == 0
#if ICC_NATIVE_WET_INK
                && ResolveLogicalInkTool() == LogicalInkTool.Pen
#else
                && (_currentToolMode == "pen" || _currentToolMode == "color"
                    || GetPalmLogicalEditingMode() == InkCanvasEditingMode.Ink)
#endif
                && !isInMultiTouchMode
                && !(currentMode == 1 ? Settings.Gesture.IsEnableMultiTouchModeBoard : Settings.Gesture.IsEnableMultiTouchMode);
        }

        private bool HasValidPalmCalibration()
        {
            var record = Settings.Advanced.TouchCalibration;
            if (record?.IsValid() != true) return false;
            var environment = TouchCalibrationDisplay.Capture(this, record.Environment.Source);
            return record.Environment.Matches(environment);
        }

        /// <summary>
        /// 校准记录（DIP）→ 引擎配置（物理像素）。返回 false 表示当前不可用手掌擦。
        /// 未校准时的旧算法回退属于后续工作：在这里返回 false 即可保留扩展点。
        /// </summary>
        private bool TryCreatePalmProfile(out PalmEraserProfile profile)
        {
            profile = null;
            var record = Settings.Advanced.TouchCalibration;
            var environment = record?.IsValid() == true
                ? TouchCalibrationDisplay.Capture(this, record.Environment.Source)
                : null;
            var options = new PalmEraserOptions
            {
                // 显式来源：手掌擦选 HID 才用原始触摸；否则用 WPF 触摸。
                Source = CurrentPalmEraserChannel == 1 ? PalmSourceMode.RawHid : PalmSourceMode.Software,
                HidDevice = Settings.Advanced.PalmHidDevice ?? "",
                HidScale = Settings.Advanced.PalmHidScale == 1 ? PalmHidScale.Isotropic : PalmHidScale.Stretch,
                // 全局尺寸倍率：在标定轮廓/面积基础上放大或缩小。
                AreaTrim = Math.Clamp(Settings.Advanced.PalmSizeMultiplier, 0.5, 2.0),
                // 点组校准没有面积样本，只能靠多点分组判定；面积校准走面积阈值。
                AreaEnabled = record?.UsePointGrouping != true,
                MinimumContacts = Math.Clamp(record?.MinimumContacts ?? 3, 2, 64),
                GroupWindowMs = Math.Clamp(record?.WindowMilliseconds ?? 200, 0, 1000),
                GroupTolerance = record?.GroupTolerance > 0 ? record.GroupTolerance : 1
            };
            // HID contact extents are device-specific; a record saved with raw HID samples gets
            // its own area pair so switching between WPF and HID does not shift the threshold.
            bool useHidAreas = CurrentPalmEraserChannel == 1 && record?.HasHidAreas == true;
            return PalmEraserProfiles.TryCreate(record, environment, options, isEraserCircleShape, out profile,
                useHidAreas ? record.HidFingerArea : (double?)null,
                useHidAreas ? record.HidPalmArea : (double?)null);
        }

        /// <summary>
        /// HID 每个接触样本都会调用，缓存 500ms，避免反复抓屏幕环境；滑杆/通道变化会在下次刷新生效。
        /// </summary>
        private bool TryGetCachedPalmProfile(out PalmEraserProfile profile)
        {
            profile = null;
            long now = Environment.TickCount64;
            long stamp = HashCode.Combine(
                Settings.Advanced.PalmSizeMultiplier,
                Settings.Advanced.PalmHidDevice,
                Settings.Advanced.PalmHidScale,
                PalmInputStamp);
            if (_palmCachedProfile != null && stamp == _palmCachedProfileStamp
                && now - _palmCachedProfileTicks < 500)
            {
                profile = _palmCachedProfile;
                return true;
            }

            if (!TryCreatePalmProfile(out profile))
            {
                _palmCachedProfile = null;
                return false;
            }
            _palmCachedProfile = profile;
            _palmCachedProfileStamp = stamp;
            _palmCachedProfileTicks = now;
            return true;
        }

        private int CurrentTouchWritingChannel => Math.Clamp(Settings.Advanced.TouchWritingChannel, 0, 1);

        private int CurrentPalmEraserChannel => Math.Clamp(Settings.Advanced.PalmEraserChannel, 0, 1);

        /// <summary>两个通道只要有一个选了 HID，就要启动原始触摸读取。</summary>
        private bool HidInputEnabled => CurrentTouchWritingChannel == 1 || CurrentPalmEraserChannel == 1;

        private int PalmInputStamp => CurrentTouchWritingChannel * 10 + CurrentPalmEraserChannel;

        private int CurrentMouseWritingChannel => Math.Clamp(Settings.Advanced.MouseWritingChannel, 0, 1);

        /// <summary>窗口初始化时调用：按当前设置启动 WPF 之外的接触来源（Pointer/HID）。</summary>
        private void InitializePalmInputSource()
        {
            if (_palmInputHooked) return;
            _palmInputHooked = true;
            Closed += (s, e) => DisposePalmInputSource();
            if (new WindowInteropHelper(this).Handle == IntPtr.Zero)
                SourceInitialized += PalmInputSource_SourceInitialized;
            else
                RebuildPalmInputSource();
        }

        private void PalmInputSource_SourceInitialized(object sender, EventArgs e)
        {
            SourceInitialized -= PalmInputSource_SourceInitialized;
            RebuildPalmInputSource();
        }

        /// <summary>设置页切换通道后调用。</summary>
        internal void ReloadPalmInputSource()
        {
            _palmInputChannel = -1;
            RebuildPalmInputSource();
        }

        private void RebuildPalmInputSource()
        {
            DisposePalmInputSource();
            _palmEngine.Clear();
            _palmSuppressedTouches.Clear();
            _palmCachedProfile = null;
            FinishAllExternalTouchStrokes(false);
            if (isPalmEraserActive) EndPalmEraser();
            _palmExternalErasing = false;
            int stamp = PalmInputStamp;
            _palmInputChannel = stamp;

            IntPtr handle = new WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero)
            {
                SourceInitialized += PalmInputSource_SourceInitialized;
                return;
            }
            HwndSource hwndSource = HwndSource.FromHwnd(handle);
            if (hwndSource == null) return;

            if (HidInputEnabled)
            {
                try
                {
                    // HID 同时接管触摸书写与手掌擦；鼠标/笔仍走 Windows Pointer。
                    _palmHidSubscription = PalmRawHidSource.Subscribe(OnPalmRawContact);
                    _palmHidActive = _palmHidSubscription != null;
                    if (_palmHidActive) StartPalmHidTimer();
                }
                catch (Exception ex)
                {
                    _palmHidActive = false;
                    LogHelper.WriteLogToFile($"[Palm] 启动输入通道失败，回退 WPF 触摸: {ex.Message}", LogHelper.LogType.Error);
                }
            }

            if (CurrentMouseWritingChannel == 1)
            {
                try
                {
                    _palmPointerWritingSource = new PointerWritingSource(hwndSource, OnPointerWritingContact);
                    _palmPointerWritingActive = true;
                }
                catch (Exception ex)
                {
                    _palmPointerWritingActive = false;
                    LogHelper.WriteLogToFile($"[Palm] 启动 Pointer 笔输入失败，回退 WPF 笔: {ex.Message}", LogHelper.LogType.Error);
                }

                try
                {
                    _palmRawMouseWritingSource = new RawMouseWritingSource(hwndSource, OnRawMouseWritingContact);
                    _palmRawMouseWritingActive = _palmRawMouseWritingSource.IsActive;
                }
                catch (Exception ex)
                {
                    _palmRawMouseWritingActive = false;
                    LogHelper.WriteLogToFile($"[Palm] 启动 Raw HID 鼠标输入失败，回退 WPF 鼠标: {ex.Message}", LogHelper.LogType.Error);
                }
            }
        }

        private void DisposePalmInputSource()
        {
            StopPalmHidTimer();
            try { _palmHidSubscription?.Dispose(); } catch { }
            _palmHidSubscription = null;
            _palmHidActive = false;
            try { _palmPointerWritingSource?.Dispose(); } catch { }
            _palmPointerWritingSource = null;
            _palmPointerWritingActive = false;
            try { _palmRawMouseWritingSource?.Dispose(); } catch { }
            _palmRawMouseWritingSource = null;
            _palmRawMouseWritingActive = false;
            _palmCachedProfile = null;
            FinishAllExternalTouchStrokes(false);
            _palmExternalErasing = false;
        }

        private void StartPalmHidTimer()
        {
            if (_palmHidTimer != null) return;
            _palmHidTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _palmHidTimer.Tick += PalmHidTimer_Tick;
            _palmHidTimer.Start();
        }

        private void StopPalmHidTimer()
        {
            if (_palmHidTimer == null) return;
            _palmHidTimer.Stop();
            _palmHidTimer.Tick -= PalmHidTimer_Tick;
            _palmHidTimer = null;
        }

        private void PalmHidTimer_Tick(object sender, EventArgs e)
        {
            _palmEngine.Tick();
            long now = Environment.TickCount64;
            foreach (int id in _palmExternalStrokes.Keys.ToArray())
            {
                if (now - _palmExternalStrokes[id].LastTicks > 1500)
                    FinishExternalTouchStroke(id, true);
            }
            if (isPalmEraserActive && _palmExternalErasing && !_palmEngine.PalmIds.Any())
                FinishExternalPalmIfIdle();
            RestoreTouchChromeIfIdle();
        }

        /// <summary>
        /// 校准页需要独占 HID 触摸采样：挂起时不再把接触送进引擎或绘制外部笔迹。
        /// </summary>
        internal void SuspendPalmInput(bool suspended)
        {
            _palmInputSuspended = suspended;
            if (!suspended) return;
            _palmEngine.Clear();
            if (_palmExternalErasing)
            {
                _palmExternalErasing = false;
                EndPalmEraser();
            }
            RestoreTouchChromeIfIdle();
        }

        private void OnPalmRawContact(RawPalmContact contact)
        {
            if (_palmInputSuspended)
            {
                if (contact.Released) FinishExternalTouchStroke(contact.Id, true);
                return;
            }
            _palmLastHidContactTicks = Environment.TickCount64;
            int id = contact.Id;
            if (contact.Released)
            {
                FinishExternalTouchStroke(id, true);
                if (Settings.Canvas.EnablePalmEraser && HasValidPalmCalibration())
                {
                    _palmEngine.Remove(PalmContactSource.RawHid, id);
                    FinishExternalPalmIfIdle();
                }
                return;
            }

            if (!IsActive) return;

            var screen = System.Windows.Forms.SystemInformation.VirtualScreen;
            if (screen.Width <= 0 || screen.Height <= 0) return;

            Point dip = inkCanvas.PointFromScreen(
                new Point(screen.Left + contact.X * screen.Width, screen.Top + contact.Y * screen.Height));
            if (!IsUsablePalmSize(dip.X, dip.Y)) return;
            if (inkCanvas.ActualWidth > 0 && inkCanvas.ActualHeight > 0
                && !new Rect(0, 0, inkCanvas.ActualWidth, inkCanvas.ActualHeight).Contains(dip))
                return;
            if (IsOverFloatingChrome(dip)) return;

            PalmEraserProfile profile = null;
            bool palmReady = CurrentPalmEraserChannel == 1
                && Settings.Canvas.EnablePalmEraser && CanStartPalmEraser()
                && TryGetCachedPalmProfile(out profile);
            if (palmReady)
            {
                _palmEngine.Configure(profile);
                var dimensions = PalmEraserEngine.HidDimensions(contact.Width, contact.Height,
                    contact.WidthMaximum, contact.HeightMaximum, screen.Width, screen.Height,
                    profile.Options.HidScale);
                if (dimensions.Width > 0 && dimensions.Height > 0)
                {
                    var center = new Point(dip.X * profile.DpiX, dip.Y * profile.DpiY);
                    var rect = new Rect(center.X - dimensions.Width / 2.0,
                        center.Y - dimensions.Height / 2.0, dimensions.Width, dimensions.Height);
                    if (contact.Pressure.HasValue) _palmEngine.NotePressure(contact.Pressure.Value);
                    if (_palmEngine.Submit(PalmContactSource.RawHid, id, rect, true, contact.Device)
                        && _palmEngine.TryGetNearestEraser(center, out var frame))
                    {
                        // 这个接触已被判定为手掌：丢弃此前可能画出的外部预览墨迹。
                        FinishExternalTouchStroke(id, false);
                        ShowExternalPalmFrame(frame, profile);
                        return;
                    }
                }
            }

            if (CurrentTouchWritingChannel != 1) return;

            // WPF 触摸事件可能比 WM_INPUT 先到；发现 HID 样本时把刚落下的 WPF 预览墨迹收掉，
            // 避免同一根手指被画两遍。只在还没有 HID 外部笔迹时做一次。
            if (_palmExternalStrokes.Count == 0) AbortWpfTouchInkForHid();
            AppendExternalTouchPoint(id, dip, contact.Pressure);
        }

        /// <summary>
        /// WM_POINTER 笔输入。返回 true 时吞掉消息，避免 WPF 再画一笔。
        /// 鼠标不经过这里：WPF 鼠标输入保持不变。
        /// </summary>
        private bool OnPointerWritingContact(PointerWritingContact contact)
        {
            int id = -300000 - (int)contact.Id;
            if (contact.Released)
            {
                bool hadStroke = _palmExternalStrokes.ContainsKey(id);
                FinishExternalTouchStroke(id, true);
                return hadStroke;
            }

            if (!CanAcceptExternalWriting()) return false;
            if (inkCanvas == null) return false;

            Point dip = inkCanvas.PointFromScreen(new Point(contact.X, contact.Y));
            if (inkCanvas.ActualWidth > 0 && inkCanvas.ActualHeight > 0
                && !new Rect(0, 0, inkCanvas.ActualWidth, inkCanvas.ActualHeight).Contains(dip))
                return false;
            if (IsOverFloatingChrome(dip)) return false;

            AppendExternalTouchPoint(id, dip, contact.Pressure);
            return _palmExternalStrokes.ContainsKey(id);
        }

        /// <summary>
        /// 物理鼠标的 Raw HID 书写。落笔/抬笔消息由 source 吞掉，WPF InkCanvas 不参与这一笔；
        /// 返回 false 时 WPF 仍按原逻辑处理（例如点在工具栏或非书写模式）。
        /// </summary>
        private bool OnRawMouseWritingContact(RawMouseContact contact)
        {
            const int id = -400001;
            if (contact.Up)
            {
                bool hadStroke = _palmExternalStrokes.ContainsKey(id);
                FinishExternalTouchStroke(id, true);
                return hadStroke;
            }

            if (!_palmRawMouseWritingActive || !CanAcceptExternalWriting()) return false;
            if (inkCanvas == null || !inkCanvas.IsMouseOver) return false;

            Point dip = inkCanvas.PointFromScreen(new Point(contact.X, contact.Y));
            if (inkCanvas.ActualWidth > 0 && inkCanvas.ActualHeight > 0
                && !new Rect(0, 0, inkCanvas.ActualWidth, inkCanvas.ActualHeight).Contains(dip))
            {
                if (_palmExternalStrokes.ContainsKey(id)) FinishExternalTouchStroke(id, true);
                return false;
            }
            if (IsOverFloatingChrome(dip)) return false;

            AppendExternalTouchPoint(id, dip, null);
            return _palmExternalStrokes.ContainsKey(id);
        }

        private void AbortWpfTouchInkForHid()
        {
            foreach (int touchId in _activeRealtimeTouchStrokeIds.ToArray())
            {
                CancelPalmTouchPreview(touchId);
                dec.Remove(touchId);
            }
            foreach (int touchId in _activeTouchStrokeIds.ToArray())
            {
                CancelPalmTouchPreview(touchId);
                dec.Remove(touchId);
            }
        }

        private bool CanAcceptExternalWriting()
        {
            if ((!_palmHidActive && !_palmPointerWritingActive && !_palmRawMouseWritingActive)
                || !IsActive || inkCanvas == null || IsCurrentPageFrozen) return false;
            if (drawingShapeMode != 0) return false;
            var mode = inkCanvas.EditingMode;
            return mode != InkCanvasEditingMode.Select
                && mode != InkCanvasEditingMode.EraseByPoint
                && mode != InkCanvasEditingMode.EraseByStroke;
        }

        /// <summary>HID 输入穿透浮层时（如点击工具栏），不要把触摸当成画布书写。</summary>
        private bool IsOverFloatingChrome(Point dip)
        {
            if (ViewboxFloatingBar == null || !ViewboxFloatingBar.IsVisible) return false;
            try
            {
                Point topLeft = inkCanvas.PointFromScreen(
                    ViewboxFloatingBar.PointToScreen(new Point(0, 0)));
                Point bottomRight = inkCanvas.PointFromScreen(
                    ViewboxFloatingBar.PointToScreen(
                        new Point(ViewboxFloatingBar.ActualWidth, ViewboxFloatingBar.ActualHeight)));
                return new Rect(topLeft, bottomRight).Contains(dip);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private void AppendExternalTouchPoint(int id, Point dip, double? pressure)
        {
            if (!CanAcceptExternalWriting()) return;

            if (!_palmExternalStrokes.TryGetValue(id, out var state))
            {
                int visualId = _palmNextExternalVisualId--;
                var visual = GetStrokeVisual(visualId);
                state = new ExternalTouchStroke { Visual = visual, VisualId = visualId };
                _palmExternalStrokes[id] = state;
                BeginTouchInkInput();
                RealtimeInkPerformanceMonitor.BeginStroke(visual, RealtimeInkInputKind.TouchInterpolated);
            }

            float pressureFactor = pressure.HasValue
                ? (float)Math.Clamp(pressure.Value, 0.01, 1.0)
                : 0.5f;
            var point = new StylusPoint(dip.X, dip.Y) { PressureFactor = pressureFactor };
            state.Visual.Add(point);
            state.LastTicks = Environment.TickCount64;
            RealtimeInkFrameScheduler.RequestRedraw(state.Visual);
        }

        private void FinishExternalTouchStroke(int id, bool commit)
        {
            if (!_palmExternalStrokes.TryGetValue(id, out var state)) return;
            _palmExternalStrokes.Remove(id);
            var visual = state.Visual;
            try
            {
                if (commit)
                {
                    RealtimeInkFrameScheduler.Flush(visual);
                    var stroke = visual.Stroke;
                    if (stroke != null && stroke.StylusPoints.Count > 0)
                    {
                        inkCanvas.Strokes.Add(stroke);
                        inkCanvas_StrokeCollected(inkCanvas, new InkCanvasStrokeCollectedEventArgs(stroke));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                RealtimeInkFrameScheduler.Cancel(visual);
                if (VisualCanvasList.TryGetValue(state.VisualId, out var visualCanvas)
                    && inkCanvas.Children.Contains(visualCanvas))
                    inkCanvas.Children.Remove(visualCanvas);
                StrokeVisualList.Remove(state.VisualId);
                VisualCanvasList.Remove(state.VisualId);
                TouchDownPointsList.Remove(state.VisualId);
                CleanupRealtimeBrushTipState(state.VisualId);
                CancelPauseStraightenTimer(state.VisualId);
                RealtimeInkPerformanceMonitor.EndStroke(visual);
                EndTouchInkInputIfIdle();
                RestoreTouchChromeIfIdle();
            }
        }

        private void FinishAllExternalTouchStrokes(bool commit)
        {
            foreach (int id in _palmExternalStrokes.Keys.ToArray())
                FinishExternalTouchStroke(id, commit);
        }

        /// <summary>
        /// HID 接管触摸书写后，WPF 的触摸事件只用来兜底：HID 源不可用或长时间收不到样本时
        /// 不再抑制 WPF 触摸，避免用户完全写不出字。
        /// </summary>
        private bool ShouldSuppressWpfTouchForHid()
        {
            if (CurrentTouchWritingChannel != 1 || !_palmHidActive) return false;
            if (inkCanvas == null) return false;
            var mode = inkCanvas.EditingMode;
            if (mode == InkCanvasEditingMode.Select
                || mode == InkCanvasEditingMode.EraseByPoint
                || mode == InkCanvasEditingMode.EraseByStroke) return false;
            if (_palmExternalStrokes.Count > 0 || _palmExternalErasing) return true;
            return Environment.TickCount64 - _palmLastHidContactTicks <= 1500;
        }

        private void ShowExternalPalmFrame(PalmEraserFrame frame, PalmEraserProfile profile)
        {
            var position = new Point(frame.CenterPx.X / profile.DpiX, frame.CenterPx.Y / profile.DpiY);
            double width = frame.WidthPx / profile.DpiX;
            double height = frame.HeightPx / profile.DpiY;
            if (!IsUsablePalmSize(width, height)) return;

            if (!isPalmEraserActive)
            {
                _palmExternalErasing = true;
                BeginPalmEraser(position, width, height);
            }
            else
            {
                if (frame.Revision != _palmRevision) BreakPalmEraserPath();
                MovePalmEraser(position, width, height);
            }
            _palmRevision = frame.Revision;
        }

        private void FinishExternalPalmIfIdle()
        {
            if (_palmEngine.PalmIds.Any()) return;
            if (isPalmEraserActive && _palmExternalErasing) EndPalmEraser();
            _palmExternalErasing = false;
            RestoreTouchChromeIfIdle();
        }

        /// <summary>WPF 触摸被 HID 通道抑制时，工具栏/黑板命中层由触摸路径隐藏，空闲后恢复。</summary>
        private void RestoreTouchChromeIfIdle()
        {
            if (dec.Count != 0 || _palmExternalErasing || _palmExternalStrokes.Count != 0) return;
            if (ViewboxFloatingBar != null) ViewboxFloatingBar.IsHitTestVisible = true;
            if (BlackboardUIGridForInkReplay != null) BlackboardUIGridForInkReplay.IsHitTestVisible = true;
        }

        /// <summary>
        /// HID 通道下 WPF 触摸不再画墨迹（由 HID 样本驱动 StrokeVisual）；
        /// HID 源不可用或长时间无样本时返回 false，让 WPF 触摸继续作为回退。
        /// </summary>
        private bool TryHandleExternalPalmTouch(TouchEventArgs e, bool down)
        {
            if (!ShouldSuppressWpfTouchForHid()) return false;
            if (down) _palmSuppressedTouches.Add(e.TouchDevice.Id);
            e.Handled = true;
            return true;
        }

        private bool TryHandlePalmTouch(TouchEventArgs e, bool down)
        {
            int id = e.TouchDevice.Id;
            if (!down && !dec.Contains(id) && !_palmSuppressedTouches.Contains(id)) return false;

            if (CurrentPalmEraserChannel == 0)
            {
                if (CanStartPalmEraser() && TryCreatePalmProfile(out var profile))
                {
                    _palmEngine.Configure(profile);
                    if (down) _palmEngine.Remove(PalmContactSource.Software, id);
                    var touch = e.GetTouchPoint(inkCanvas);
                    _palmEngine.Submit(PalmContactSource.Software, id,
                        new Rect((touch.Position.X - touch.Bounds.Width / 2) * profile.DpiX,
                            (touch.Position.Y - touch.Bounds.Height / 2) * profile.DpiY,
                            touch.Bounds.Width * profile.DpiX, touch.Bounds.Height * profile.DpiY));
                    if (_palmEngine.TryGetEraser(id, out var frame))
                    {
                        var position = new Point(frame.CenterPx.X / profile.DpiX, frame.CenterPx.Y / profile.DpiY);
                        double width = frame.WidthPx / profile.DpiX, height = frame.HeightPx / profile.DpiY;
                        if (!IsUsablePalmSize(width, height)) return false;

                        // 接触由书写升级为手掌时，丢弃尚未提交的预览，不删除已有干墨。
                        foreach (int palmId in _palmEngine.PalmIds)
                        {
                            CancelPalmTouchPreview(palmId);
                            _palmSuppressedTouches.Add(palmId);
                        }
                        _palmSuppressedTouches.Add(id);
                        if (!isPalmEraserActive)
                        {
                            _palmTouchDevice = e.TouchDevice;
                            BeginPalmEraser(position, width, height);
                            inkCanvas.LostTouchCapture += PalmEraser_LostTouchCapture;
                            inkCanvas.CaptureTouch(e.TouchDevice);
                        }
                        else
                        {
                            if (frame.Revision != _palmRevision) BreakPalmEraserPath();
                            MovePalmEraser(position, width, height);
                        }
                        _palmRevision = frame.Revision;
                        e.Handled = true;
                        return true;
                    }

                    if (_palmTouchDevice == e.TouchDevice) EndPalmEraser();
                    if (!isPalmEraserActive && !_palmSuppressedTouches.Contains(id))
                    {
                        // 不是手掌，继续让 WPF/外部书写处理。
                    }
                    else
                    {
                        _palmSuppressedTouches.Add(id);
                        e.Handled = true;
                        return true;
                    }
                }
                else if (isPalmEraserActive)
                {
                    EndPalmEraser();
                }
            }
            else
            {
                // 手掌数据来自 HID；WPF 触摸只用来判断落点是否在已识别的手掌下。
                if (CanStartPalmEraser() && TryCreatePalmProfile(out var profile))
                {
                    _palmEngine.Configure(profile);
                    var touch = e.GetTouchPoint(inkCanvas);
                    var center = new Point(touch.Position.X * profile.DpiX, touch.Position.Y * profile.DpiY);
                    if (_palmEngine.TryGetNearestEraser(center, out var frame))
                    {
                        CancelPalmTouchPreview(id);
                        _palmSuppressedTouches.Add(id);
                        e.Handled = true;
                        ShowExternalPalmFrame(frame, profile);
                        return true;
                    }
                }
                else if (isPalmEraserActive)
                {
                    EndPalmEraser();
                }
            }

            // 书写来源是 HID：HID 样本没到或失效时用 WPF 兜底；样本活跃时禁止 WPF 再画一遍。
            if (CurrentTouchWritingChannel == 1 && ShouldSuppressWpfTouchForHid())
            {
                if (down) _palmSuppressedTouches.Add(id);
                e.Handled = true;
                return true;
            }
            return false;
        }

        private void CancelPalmTouchPreview(int id)
        {
            if (StrokeVisualList.TryGetValue(id, out var visual))
            {
                RealtimeInkFrameScheduler.Cancel(visual);
                RealtimeInkPerformanceMonitor.EndStroke(visual);
            }
            if (VisualCanvasList.TryGetValue(id, out var canvas))
                inkCanvas.Children.Remove(canvas);
            StrokeVisualList.Remove(id);
            VisualCanvasList.Remove(id);
            TouchDownPointsList.Remove(id);
            _activeRealtimeTouchStrokeIds.Remove(id);
            _activeTouchStrokeIds.Remove(id);
            CleanupRealtimeBrushTipState(id);
            CancelPauseStraightenTimer(id);
            EndTouchInkInputIfIdle();
#if ICC_NATIVE_WET_INK
            // WPF fallback 的 pointer namespace 与 WpfPointerInputSource 保持一致。
            uint pointer = 0x80000000 | (unchecked((uint)id) & 0x3FFFFFFF);
            _nativePointerUpdatePump?.DiscardPointer(pointer);
            _nativeInkController?.Cancel(pointer);
            _wetInkWindowHost?.SignalWork();
#endif
        }

        private bool FinishPalmTouch(TouchEventArgs e)
        {
            int id = e.TouchDevice.Id;
            _palmEngine.Remove(PalmContactSource.Software, id);
            bool suppressed = _palmSuppressedTouches.Remove(id);
            if (_palmTouchDevice == e.TouchDevice)
                EndPalmEraser();
            if (!suppressed) return false;
            dec.Remove(id);
            inkCanvas.ReleaseTouchCapture(e.TouchDevice);
            if (dec.Count == 0 && !_palmExternalErasing && !_palmEngine.PalmIds.Any())
            {
                EndPalmEraser();
                ViewboxFloatingBar.IsHitTestVisible = true;
                BlackboardUIGridForInkReplay.IsHitTestVisible = true;
            }
            e.Handled = true;
            return true;
        }

        private void PalmEraser_LostTouchCapture(object sender, TouchEventArgs e)
        {
            var device = e.TouchDevice;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (isPalmEraserActive && _palmTouchDevice == device
                    && device.Captured != inkCanvas)
                {
                    dec.Remove(device.Id);
                    _palmEngine.Remove(PalmContactSource.Software, device.Id);
                    EndPalmEraser();
                    ViewboxFloatingBar.IsHitTestVisible = true;
                    BlackboardUIGridForInkReplay.IsHitTestVisible = true;
                }
            }), DispatcherPriority.Input);
        }

        private void BeginPalmEraser(Point position, double width, double height)
        {
            if (!IsUsablePalmSize(width, height)) return;
            palmEraserPreviousEditingMode = inkCanvas.EditingMode;
            _palmPreviousWidth = eraserWidth;
            _palmPreviousStrokeEraser = isUsingStrokesEraser;
            _palmPreviousManipulation = inkCanvas.IsManipulationEnabled;
            _changingPalmMode = true;
            try
            {
                // 只使用几何擦除器；不能同时让 InkCanvas 内建点擦再次修改笔画。
                inkCanvas.EditingMode = InkCanvasEditingMode.None;
            }
            finally { _changingPalmMode = false; }
            isPalmEraserActive = true;
            inkCanvas.IsManipulationEnabled = false;
            isUsingStrokesEraser = false;
            eraserWidth = width;
            _palmHeight = height;
            _palmLastPoint = null;
            EnableEraserOverlay();
            if (eraserOverlayCanvas != null) eraserOverlayCanvas.IsHitTestVisible = false;
            EraserOverlay_PointerDown(inkCanvas);
            MovePalmEraser(position, width, height);
        }

        private void MovePalmEraser(Point position, double width, double height)
        {
            if (!IsUsablePalmSize(width, height) || !double.IsFinite(position.X) || !double.IsFinite(position.Y)) return;
            if (IsCurrentPageFrozen)
            {
                EndPalmEraser();
                return;
            }
            if (eraserWidth != width || _palmHeight != height)
            {
                eraserWidth = width;
                _palmHeight = height;
                // 尺寸变化只重建命中器，不拆分同一次手掌擦的撤销记录。
                if (hitTester != null)
                {
                    hitTester.StrokeHit -= EraserGeometry_StrokeHit;
                    hitTester.EndHitTesting();
                }
                hitTester = inkCanvas.Strokes.GetIncrementalStrokeHitTester(CreateGeometryEraserShape());
                hitTester.StrokeHit += EraserGeometry_StrokeHit;
                if (_palmLastPoint.HasValue) hitTester.AddPoint(_palmLastPoint.Value);
                if (eraserFeedback != null)
                {
                    eraserFeedback.Width = width;
                    eraserFeedback.Height = height;
                }
            }
            EraserOverlay_PointerMove(inkCanvas, position);
            _palmLastPoint = position;
        }

        private void BreakPalmEraserPath()
        {
            _palmLastPoint = null;
            if (hitTester != null)
            {
                hitTester.StrokeHit -= EraserGeometry_StrokeHit;
                hitTester.EndHitTesting();
            }
            hitTester = inkCanvas.Strokes.GetIncrementalStrokeHitTester(CreateGeometryEraserShape());
            hitTester.StrokeHit += EraserGeometry_StrokeHit;
        }

        private void EndPalmEraser(bool restoreMode = true)
        {
            if (!isPalmEraserActive) return;
            isPalmEraserActive = false;
            inkCanvas.LostTouchCapture -= PalmEraser_LostTouchCapture;
            _palmTouchDevice = null;
#if ICC_NATIVE_WET_INK
            _palmNativePointer = null;
#endif
            _palmLastPoint = null;
            try { DisableEraserOverlay(); }
            finally
            {
                eraserWidth = _palmPreviousWidth;
                isUsingStrokesEraser = _palmPreviousStrokeEraser;
                inkCanvas.IsManipulationEnabled = _palmPreviousManipulation;
                _changingPalmMode = true;
                try
                {
                    if (restoreMode && inkCanvas.EditingMode == InkCanvasEditingMode.None && !IsCurrentPageFrozen)
                        inkCanvas.EditingMode = palmEraserPreviousEditingMode;
                }
                finally { _changingPalmMode = false; }
                SetCursorBasedOnEditingMode(inkCanvas);
            }
        }

        private static bool IsUsablePalmSize(double width, double height) =>
            double.IsFinite(width) && double.IsFinite(height) && width >= 0.003 && height >= 0.003
            && width <= 100000 && height <= 100000;

        internal void ResetTouchCalibrationInput()
        {
            ResetPalmEraser();
        }

        private void ResetPalmEraser()
        {
            EndPalmEraser();
            _palmEngine.Clear();
            _palmSuppressedTouches.Clear();
            _palmSuppressedPointers.Clear();
        }

#if ICC_NATIVE_WET_INK
        private bool TryHandleNativePalm(NativePointerInputBatch batch, NativePointerFacts facts, bool canStart)
        {
            if (batch.InputKind != NativeInkInputKind.Touch) return false;
            // WPF fallback 由 PreviewTouch 管理，不能把同一帧再次送入滤波窗。
            if (batch.IsWpfFallback)
            {
                bool suppressed = isPalmEraserActive
                    || _palmSuppressedTouches.Contains((int)(batch.PointerId & 0x3FFFFFFF));
                if (suppressed) CancelNativePalmInk(batch.PointerId);
                return suppressed;
            }
            if (isPalmEraserActive && !canStart)
            {
                EndPalmEraser();
                _palmSuppressedPointers.Remove(batch.PointerId);
                CancelNativePalmInk(batch.PointerId);
                return false;
            }
            if (!canStart || !CanStartPalmEraser() || HasCanceledFlag(facts)) return false;
            if (!TryCreatePalmProfile(out var profile)) return false;
            _palmEngine.Configure(profile);
            Point position = TranslatePoint(new Point(facts.XDip, facts.YDip), inkCanvas);
            _palmEngine.Submit(PalmContactSource.Software, unchecked((int)batch.PointerId),
                new Rect((position.X - facts.ContactWidthDip / 2) * profile.DpiX,
                    (position.Y - facts.ContactHeightDip / 2) * profile.DpiY,
                    facts.ContactWidthDip * profile.DpiX, facts.ContactHeightDip * profile.DpiY));
            if (!_palmEngine.TryGetEraser(unchecked((int)batch.PointerId), out var frame))
            {
                if (_palmNativePointer == batch.PointerId) EndPalmEraser();
                return _palmSuppressedPointers.Contains(batch.PointerId);
            }
            CancelNativePalmInk(batch.PointerId);
            _palmNativePointer = batch.PointerId;
            _palmSuppressedPointers.Add(batch.PointerId);
            position = new Point(frame.CenterPx.X / profile.DpiX, frame.CenterPx.Y / profile.DpiY);
            if (!isPalmEraserActive) BeginPalmEraser(position, frame.WidthPx / profile.DpiX, frame.HeightPx / profile.DpiY);
            else MovePalmEraser(position, frame.WidthPx / profile.DpiX, frame.HeightPx / profile.DpiY);
            return true;
        }

        private void CancelNativePalmInk(uint pointerId)
        {
            _nativePointerUpdatePump?.DiscardPointer(pointerId);
            _nativeInkController?.Cancel(pointerId);
            StopNativePauseStraightenTimer(pointerId);
            _wetInkWindowHost?.SignalWork();
        }

        private bool FinishNativePalm(NativePointerInputBatch batch)
        {
            if (batch.IsWpfFallback || batch.InputKind != NativeInkInputKind.Touch) return false;
            _palmEngine.Remove(PalmContactSource.Software, unchecked((int)batch.PointerId));
            bool suppressed = _palmSuppressedPointers.Remove(batch.PointerId);
            if (suppressed) CancelNativePalmInk(batch.PointerId);
            if (_palmNativePointer == batch.PointerId) EndPalmEraser();
            return suppressed;
        }
#endif
    }
}
