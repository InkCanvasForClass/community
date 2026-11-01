using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Ink_Canvas.Helpers;
using Ink_Canvas.Ink;

namespace Ink_Canvas
{
    /// <summary>
    /// 原始 HID 触摸输入：RawHidTouch 解码 + ContactTracker 合成稳定手指，
    /// 同一批接触同时喂手掌擦引擎和触摸书写。鼠标/笔仍由 PointerWritingSource / RawMouseWritingSource 负责。
    /// </summary>
    public partial class MainWindow
    {
        private const int WmInputMessage = 0x00FF;
        private const int HidTouchExternalIdBase = -200000;

        private HwndSource _hidTouchSource;
        private bool _hidTouchRegistered;
        private ContactTracker _hidTouchTracker;
        private readonly List<ContactTracker.ContactEvent> _hidTouchEvents = new();
        private readonly Dictionary<int, HidTouchState> _hidTouches = new();

        private int _hidGestureA, _hidGestureB;
        private System.Windows.Controls.InkCanvasEditingMode? _hidGestureSavedEditingMode;
        private HashSet<Stroke> _hidStrokeBaseline = new();
        private bool _hidGestureActive;
        private bool _hidGestureSessionActive;
        private Point _hidGestureLastA;
        private Point _hidGestureLastB;

        // 手势期间用一张画布快照做 GPU 变换，避免每帧逐笔改 InkCanvas 触发大量
        // StrokesChanged/重排；抬手时再把累计矩阵一次性写回笔画模型。
        private Image _hidGestureSnapshot;
        private double _hidGestureSnapshotOpacity = 1.0;
        private Matrix _hidGestureTransform = Matrix.Identity;
        private StrokeCollection _hidGestureScaleTargets;
        private double _hidGestureWidthScale = 1.0;

        private sealed class HidTouchState
        {
            public int StableId;
            public Point Dip;
            public double Pressure;
            public int WidthPx;
            public int HeightPx;
            public bool Down;
            public bool IsPalm;
            public long LastTicks;
        }

        private void InitializeHidTouchInput()
        {
            if (_hidTouchSource != null) return;

            IntPtr handle = new WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero)
            {
                SourceInitialized += HidTouchSourceInitialized;
                return;
            }

            HwndSource source = HwndSource.FromHwnd(handle);
            if (source == null)
            {
                return;
            }

            _hidTouchSource = source;
            var screen = System.Windows.Forms.SystemInformation.VirtualScreen;
            _hidTouchTracker = new ContactTracker(screen.Width, screen.Height);
            _hidTouchSource.AddHook(HidTouchWndProc);
            ReloadHidTouchInput();
        }

        private void HidTouchSourceInitialized(object sender, EventArgs e)
        {
            SourceInitialized -= HidTouchSourceInitialized;
            InitializeHidTouchInput();
        }

        /// <summary>按当前设置注册/注销 RawInput。HID 触摸书写与 HID 手掌擦任一开启都需要。</summary>
        private void ReloadHidTouchInput()
        {
            if (_hidTouchSource == null) return;

            bool want = HidInputEnabled;
            if (want && !_hidTouchRegistered)
            {
                try
                {
                    _ = RawHidTouch.Scan();
                    if (RawHidTouch.Register(_hidTouchSource.Handle, out int error))
                    {
                        _hidTouchRegistered = true;
                        _palmHidActive = true;
                        StartPalmHidTimer();
                        LogHelper.WriteLogToFile("[HID] RawInput 触摸已接管");
                    }
                    else
                    {
                        _palmHidActive = false;
                        LogHelper.WriteLogToFile($"[HID] RawInput 注册失败，回退 WPF 触摸，win32={error}",
                            LogHelper.LogType.Error);
                    }
                }
                catch (Exception ex)
                {
                    _hidTouchRegistered = false;
                    _palmHidActive = false;
                    LogHelper.WriteLogToFile($"[HID] 启动失败，回退 WPF 触摸: {ex.Message}", LogHelper.LogType.Error);
                }
            }
            else if (!want && _hidTouchRegistered)
            {
                try { RawHidTouch.Unregister(); } catch { }
                _hidTouchRegistered = false;
                _palmHidActive = false;
                ClearHidTouchState();
                StopPalmHidTimer();
            }
        }

        internal bool IsHidTouchActive => _hidTouchRegistered;

        internal void SuspendHidTouchInputForCalibration()
        {
            if (!_hidTouchRegistered) return;
            try { RawHidTouch.Unregister(); } catch { }
            _hidTouchRegistered = false;
            _palmHidActive = false;
            ClearHidTouchState();
            StopPalmHidTimer();
        }

        internal void ResumeHidTouchInputAfterCalibration()
        {
            if (!HidInputEnabled) return;
            if (_hidTouchSource == null)
                InitializeHidTouchInput();
            else
                ReloadHidTouchInput();
            _palmHidActive = _hidTouchRegistered;
            if (_palmHidActive) StartPalmHidTimer();
        }

        private void ShutdownHidTouchInput()
        {
            try
            {
                if (_hidTouchRegistered) RawHidTouch.Unregister();
            }
            catch { }

            _hidTouchRegistered = false;
            _palmHidActive = false;
            ClearHidTouchState();
            StopPalmHidTimer();

            if (_hidTouchSource != null)
            {
                try { _hidTouchSource.RemoveHook(HidTouchWndProc); } catch { }
                _hidTouchSource = null;
            }
            _hidTouchTracker = null;
        }

        private IntPtr HidTouchWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg != WmInputMessage || !_hidTouchRegistered) return IntPtr.Zero;

            bool read;
            List<RawHidTouch.RawFrame> frames;
            try
            {
                read = RawHidTouch.TryReadFrames(lParam, out frames);
            }
            catch (Exception ex)
            {
                return IntPtr.Zero;
            }

            if (!read)
            {
                if (RawHidTouch.LastUnknownDevice != IntPtr.Zero)
                {
                    if (RawHidTouch.TryBeginAutoRescan())
                    {
                        try { RawHidTouch.Scan(); } catch { }
                    }
                }
                return IntPtr.Zero;
            }
            // 笔走 PointerWritingSource，避免同一支笔被画两遍。
            if (RawHidTouch.LastFrameUsage == 0x20) return IntPtr.Zero;

            foreach (RawHidTouch.RawFrame frame in frames)
            {
                try { ProcessHidFrame(frame); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
            }
            return IntPtr.Zero;
        }

        private void ProcessHidFrame(RawHidTouch.RawFrame frame)
        {
            var screen = System.Windows.Forms.SystemInformation.VirtualScreen;
            if (screen.Width <= 0 || screen.Height <= 0) return;

            var tracks = new List<ContactTracker.TrackIn>(frame.Contacts.Count);
            foreach (RawHidTouch.RawContact contact in frame.Contacts)
            {
                if (contact.XLogMax <= 0 || contact.YLogMax <= 0) continue;

                float px = (float)(screen.Left + contact.XLogical / (double)contact.XLogMax * screen.Width);
                float py = (float)(screen.Top + contact.YLogical / (double)contact.YLogMax * screen.Height);
                // 接触宽高使用设备上报的原始逻辑值（触摸屏常见 4×4）。不同设备
                // 的宽高量程并不可靠，强行按 LogMax 放大到屏幕尺寸会得到 0 或满屏；
                // 原始值由「墨迹粗细」策略决定最终笔宽。
                float w = contact.WLogical > 0 ? contact.WLogical : 1;
                float h = contact.HLogical > 0 ? contact.HLogical : 1;
                tracks.Add(new ContactTracker.TrackIn(contact.Link, contact.HasCid, contact.ContactId,
                    contact.Down, px, py, w, h, (float)(contact.Pressure01 ?? 0.5)));
            }

            _hidTouchEvents.Clear();
            _hidTouchTracker.Sync(tracks, frame.CountZero, _hidTouchEvents);
            foreach (ContactTracker.ContactEvent ev in _hidTouchEvents) ProcessHidTouchEvent(ev);
            if (_hidGestureSessionActive) ApplyHidGestureFrame();
        }

        private void ProcessHidTouchEvent(ContactTracker.ContactEvent ev)
        {
            int externalId = HidTouchExternalIdBase - ev.StableId;
            if (ev.Kind == ContactTracker.Kind.Up)
            {
                if (!_hidTouches.TryGetValue(ev.StableId, out HidTouchState state)) return;
                _hidTouches.Remove(ev.StableId);
                if (_hidGestureActive && IsHidGesturePair(ev.StableId))
                    EndHidGesture();

                if (state.IsPalm)
                {
                    _palmEngine.Remove(PalmContactSource.RawHid, ev.StableId);
                    FinishExternalPalmIfIdle();
                }
                else
                {
                    FinishExternalTouchStroke(externalId, true);
                }

                // 手势会话必须锁到所有非手掌手指都抬起：手指还在屏幕上移动时绝不落笔，
                // 否则双指转完抬起一根、另一根继续移动就会“边转边画”。
                if (_hidGestureSessionActive
                    && !_hidTouches.Values.Any(t => t.Down && !t.IsPalm))
                    EndHidGestureSession();
                return;
            }

            if (!IsPointOverThisWindow((int)ev.X, (int)ev.Y))
            {
                return;
            }

            Point dip = inkCanvas.PointFromScreen(new Point(ev.X, ev.Y));
            if (!IsUsablePalmSize(dip.X, dip.Y))
            {
                return;
            }

            bool hasState = _hidTouches.TryGetValue(ev.StableId, out HidTouchState st);
            if (!hasState)
            {
                // 新落指必须落在画布内且不在浮动栏上，避免从工具栏/窗口外起笔。
                if (inkCanvas.ActualWidth > 0 && inkCanvas.ActualHeight > 0
                    && !new Rect(0, 0, inkCanvas.ActualWidth, inkCanvas.ActualHeight).Contains(dip))
                {
                    return;
                }
                if (IsOverFloatingChrome(dip))
                {
                    return;
                }
                st = new HidTouchState { StableId = ev.StableId };
                if (_hidTouches.Count == 0 && inkCanvas != null)
                    _hidStrokeBaseline = new HashSet<Stroke>(inkCanvas.Strokes);
                _hidTouches[ev.StableId] = st;
            }

            // 先更新状态再做区域过滤：手指移到工具栏上/画布外时，双指手势仍要跟踪位置，
            // 否则手势会突然“卡住”，只能等手指回到画布内才继续。
            st.Dip = dip;
            st.Pressure = ev.Pressure01;
            st.WidthPx = Math.Max(0, (int)Math.Round(ev.W));
            st.HeightPx = Math.Max(0, (int)Math.Round(ev.H));
            st.LastTicks = Environment.TickCount64;
            if (ev.Kind == ContactTracker.Kind.Down)
            {
                st.Down = true;
            }

            // 手势会话一旦开始，手指只服务手势：不再做画布/工具栏/手掌过滤，
            // 否则手指滑到工具栏或画布外时 Move 被丢弃，手势看起来像卡住。
            if (_hidGestureSessionActive)
            {
                // 手势按“帧”统一应用：这里只更新位置，等本帧所有触点事件处理完
                // 再在 ApplyHidGestureFrame 里算一次矩阵，避免两只手先后到达时
                // 用旧坐标重复平移/缩放造成抖动。
                if (ev.Kind == ContactTracker.Kind.Down)
                    OnHidFingerDown(st);
                return;
            }

            if (hasState
                && ((inkCanvas.ActualWidth > 0 && inkCanvas.ActualHeight > 0
                     && !new Rect(0, 0, inkCanvas.ActualWidth, inkCanvas.ActualHeight).Contains(dip))
                    || IsOverFloatingChrome(dip)))
            {
                return;
            }

            PalmEraserProfile profile = null;
            bool palmReady = CurrentPalmEraserChannel == 1
                && Settings.Canvas.EnablePalmEraser
                && CanStartPalmEraser()
                && TryGetCachedPalmProfile(out profile);
            if (palmReady)
            {
                _palmEngine.Configure(profile);
                var center = new Point(dip.X * profile.DpiX, dip.Y * profile.DpiY);
                double width = Math.Max(1, ev.W);
                double height = Math.Max(1, ev.H);
                var rect = new Rect(center.X - width / 2, center.Y - height / 2, width, height);
                if (ev.Pressure01 > 0) _palmEngine.NotePressure(ev.Pressure01);

                bool submitted = _palmEngine.Submit(PalmContactSource.RawHid, ev.StableId, rect, true, "");
                PalmEraserFrame frame = default;
                bool hasEraser = submitted && _palmEngine.TryGetEraser(ev.StableId, out frame);
                if (hasEraser)
                {
                    if (!st.IsPalm)
                    {
                        st.IsPalm = true;
                        FinishExternalTouchStroke(externalId, false);
                        CancelHidGestureFor(ev.StableId);
                    }
                    ShowExternalPalmFrame(frame, profile);
                    return;
                }
            }

            if (st.IsPalm)
            {
                return;
            }

            // 触摸书写仍选 WPF 时，HID 只负责手掌判定，不能把接触再画成墨迹。
            if (CurrentTouchWritingChannel != 1)
            {
                return;
            }
            // 选择/擦除/几何图形等工具下由 WPF 处理触摸，HID 不抢。
            if (!CanAcceptExternalWriting())
            {
                return;
            }

            // WPF 触摸事件可能比 WM_INPUT 先到；HID 首次落指时把刚落下的 WPF 预览收掉，
            // 避免同一根手指被画两遍。
            if (ev.Kind == ContactTracker.Kind.Down && _palmExternalStrokes.Count == 0)
                AbortWpfTouchInkForHid();

            if (ev.Kind == ContactTracker.Kind.Down)
                OnHidFingerDown(st);
            else
                OnHidFingerMove(st);
        }

        private void OnHidFingerDown(HidTouchState st)
        {
            // 手势会话期间不再起新笔迹，等所有手指抬起后恢复书写。
            if (_hidGestureSessionActive)
            {
                // 兜底：上一轮会话的 pair 已经不在，且没有其他手指还按着，
                // 说明 Up 丢了；这里主动收尾，否则 HID 书写会一直“写不出来”。
                long now = Environment.TickCount64;
                bool pairAlive = _hidGestureActive && _hidTouches.Values.Any(
                    t => t.Down && now - t.LastTicks < 1000
                        && (t.StableId == _hidGestureA || t.StableId == _hidGestureB));
                // 只认“手势对”还活着；不能用“还有别的手指按着”当理由，
                // 否则手势对已经没了、却有两根新手指出于按下状态时，会话永远结束不了。
                if (pairAlive) return;
                EndHidGestureSession();
            }

            // 多指书写与双指手势是互斥的四个开关：
            // 多指书写开启时，第二根手指继续落笔，绝不能启动手势清空已有墨迹；
            // 多指书写关闭时，才按“双指移动/缩放/旋转”里已开启的项进入手势。
            bool multiWriting = isInMultiTouchMode || Settings.Gesture.IsEnableMultiTouchMode;
            if (!multiWriting && Settings.Gesture.IsEnableTwoFingerGesture)
            {
                HidTouchState other = _hidTouches.Values
                    .FirstOrDefault(t => t.Down && !t.IsPalm && t.StableId != st.StableId);
                if (other != null)
                {
                    StartHidGesture(st, other);
                    return;
                }
            }

            AppendExternalTouchPoint(HidTouchExternalIdBase - st.StableId, st.Dip, st.Pressure, st.WidthPx, st.HeightPx);
        }

        private void OnHidFingerMove(HidTouchState st)
        {
            if (_hidGestureSessionActive)
            {
                if (_hidGestureActive && IsHidGesturePair(st.StableId))
                    ApplyHidGesture(st);
                return;
            }

            AppendExternalTouchPoint(HidTouchExternalIdBase - st.StableId, st.Dip, st.Pressure, st.WidthPx, st.HeightPx);
        }

        private bool IsHidGesturePair(int id) => id == _hidGestureA || id == _hidGestureB;

        private void StartHidGesture(HidTouchState a, HidTouchState b)
        {
            _hidGestureSessionActive = true;
            _hidGestureActive = true;
            _hidGestureA = a.StableId;
            _hidGestureB = b.StableId;
            _hidGestureLastA = a.Dip;
            _hidGestureLastB = b.Dip;

            // 视频展台同款做法：手势期间把 InkCanvas 的 EditingMode 压成 None，
            // 阻止 WPF 内置墨迹（触摸提升成 Stylus/Mouse 后）在旋转缩放时继续画。
            if (inkCanvas != null)
            {
                _hidGestureSavedEditingMode = inkCanvas.EditingMode;
                if (inkCanvas.EditingMode != System.Windows.Controls.InkCanvasEditingMode.None)
                {
                    inkCanvas.EditingMode = System.Windows.Controls.InkCanvasEditingMode.None;
                }
                try { inkCanvas.ReleaseAllTouchCaptures(); } catch { }
            }

            // 双指手势优先：清掉所有手指当前画出的预览墨迹（外部笔迹 + WPF 预览），
            // 再进入平移/旋转/缩放。随后保持 IsManipulationEnabled=false，
            // 避免 WPF 自己的操纵逻辑和 HID 手势同时改画布，出现“卡一下动一下”。
            FinishAllExternalTouchStrokes(false);
            AbortWpfTouchInkForHid();
            // 两根手指落下前，第一根手指可能已经把笔迹提交进 InkCanvas；
            // 手势开始时把本次触摸序列里新提交的笔画一并移除，避免“第一指还在画”。
            int removedCommitted = 0;
            if (inkCanvas != null)
            {
                foreach (Stroke stroke in inkCanvas.Strokes.ToArray())
                {
                    if (!_hidStrokeBaseline.Contains(stroke))
                    {
                        inkCanvas.Strokes.Remove(stroke);
                        removedCommitted++;
                    }
                }
            }
            CancelAllHidGesturePreviews();
            BeginTouchInkInput();
            BeginHidGestureSnapshot();
        }

        /// <summary>清掉所有仍然挂在画布上的触摸预览 visual，避免手势开始时残留某一指的墨迹。</summary>
        private void CancelAllHidGesturePreviews()
        {
            foreach (int id in StrokeVisualList.Keys.ToArray())
                CancelPalmTouchPreview(id);
            foreach (int id in VisualCanvasList.Keys.ToArray())
                CancelPalmTouchPreview(id);
        }

        /// <summary>
        /// 手势开始时给 InkCanvas 拍一张快照，手势期间只改这张图的 RenderTransform。
        /// 大量笔画时，逐帧 Transform + 改 DrawingAttributes 会触发成百上千次
        /// StrokesChanged，WPF 的 InkCanvas 事件链会把 UI 线程拖死。
        /// </summary>
        private void BeginHidGestureSnapshot()
        {
            if (inkCanvas == null || _hidGestureSnapshot != null) return;
            if (inkCanvas.Parent is not Panel parent) return;

            double width = inkCanvas.ActualWidth;
            double height = inkCanvas.ActualHeight;
            if (width < 1 || height < 1) return;

            DpiScale dpi = VisualTreeHelper.GetDpi(inkCanvas);
            var bitmap = new RenderTargetBitmap(
                Math.Max(1, (int)Math.Ceiling(width * dpi.DpiScaleX)),
                Math.Max(1, (int)Math.Ceiling(height * dpi.DpiScaleY)),
                dpi.PixelsPerInchX,
                dpi.PixelsPerInchY,
                PixelFormats.Pbgra32);
            bitmap.Render(inkCanvas);

            var image = new Image
            {
                Source = bitmap,
                Width = width,
                Height = height,
                IsHitTestVisible = false,
                Stretch = Stretch.Fill,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

            int index = parent.Children.IndexOf(inkCanvas);
            if (index < 0) return;
            parent.Children.Insert(index + 1, image);

            _hidGestureSnapshot = image;
            _hidGestureSnapshotOpacity = inkCanvas.Opacity;
            _hidGestureTransform = Matrix.Identity;
            _hidGestureWidthScale = 1.0;

            StrokeCollection selected = inkCanvas.GetSelectedStrokes();
            _hidGestureScaleTargets = selected != null && selected.Count > 0
                ? new StrokeCollection(selected.ToArray())
                : null;

            // 快照已经覆盖原墨迹，隐藏 InkCanvas 的渲染；它仍保持命中测试与笔迹模型。
            inkCanvas.Opacity = 0;
        }

        /// <summary>手势结束：移除快照，把累计矩阵与宽度倍率一次性写回笔画模型。</summary>
        private void CommitHidGestureVisual()
        {
            if (_hidGestureSnapshot == null) return;

            if (_hidGestureSnapshot.Parent is Panel parent)
                parent.Children.Remove(_hidGestureSnapshot);
            _hidGestureSnapshot = null;

            if (inkCanvas == null) return;
            inkCanvas.Opacity = _hidGestureSnapshotOpacity;

            Matrix matrix = _hidGestureTransform;
            bool hasMatrix = !matrix.IsIdentity;
            double widthScale = _hidGestureWidthScale;
            StrokeCollection targets = _hidGestureScaleTargets;
            _hidGestureScaleTargets = null;
            _hidGestureTransform = Matrix.Identity;
            _hidGestureWidthScale = 1.0;

            if (targets != null && targets.Count > 0)
            {
                if (hasMatrix) targets.Transform(matrix, false);
                if (widthScale != 1.0)
                    foreach (Stroke stroke in targets)
                        if (inkCanvas.Strokes.Contains(stroke))
                            ScaleStrokeWidth(stroke, widthScale);
            }
            else
            {
                if (hasMatrix)
                {
                    inkCanvas.Strokes.Transform(matrix, false);
                    TransformCanvasImages(matrix);
                }
                if (widthScale != 1.0)
                    foreach (Stroke stroke in inkCanvas.Strokes.ToArray())
                        ScaleStrokeWidth(stroke, widthScale);
            }

            inkCanvas.InvalidateVisual();
        }

        private void EndHidGesture()
        {
            if (_hidGestureActive)
            CommitHidGestureVisual();
            _hidGestureActive = false;
            _hidGestureA = 0;
            _hidGestureB = 0;
        }

        private void EndHidGestureSession()
        {
            if (_hidGestureSessionActive)
            EndHidGesture();
            _hidGestureSessionActive = false;
            // 手势期间由 BeginTouchInkInput 压住 WPF 操纵；会话结束后再交还。
            EndTouchInkInputIfIdle();
            RestoreTouchChromeIfIdle();
            if (inkCanvas != null && _hidGestureSavedEditingMode.HasValue)
            {
                try
                {
                    if (inkCanvas.EditingMode == System.Windows.Controls.InkCanvasEditingMode.None)
                        inkCanvas.EditingMode = _hidGestureSavedEditingMode.Value;
                }
                catch { }
                _hidGestureSavedEditingMode = null;
            }
        }

        private void CancelHidGestureFor(int id)
        {
            if (_hidGestureActive && IsHidGesturePair(id))
            {
                EndHidGestureSession();
            }
        }

        private void ApplyHidGestureFrame()
        {
            if (!_hidGestureActive) return;
            if (!_hidTouches.TryGetValue(_hidGestureA, out HidTouchState a) || !a.Down) return;
            if (!_hidTouches.TryGetValue(_hidGestureB, out HidTouchState b) || !b.Down) return;
            ApplyHidGesture(a);
        }

        private void ApplyHidGesture(HidTouchState st)
        {
            // 手势过程中工具/冻结状态可能变化，但手势本身不应依赖“能否书写”；
            // 只有用户关掉手势或进入多触模式时才结束会话。
            if (!Settings.Gesture.IsEnableTwoFingerGesture || IsCurrentPageFrozen)
            {
                EndHidGestureSession();
                return;
            }

            int otherId = st.StableId == _hidGestureA ? _hidGestureB : _hidGestureA;
            if (!_hidTouches.TryGetValue(otherId, out HidTouchState other) || !other.Down)
            {
                EndHidGesture();
                return;
            }

            Point curA = st.StableId == _hidGestureA ? st.Dip : other.Dip;
            Point curB = st.StableId == _hidGestureB ? st.Dip : other.Dip;
            Point prevA = _hidGestureLastA;
            Point prevB = _hidGestureLastB;

            Point prevCenter = new Point((prevA.X + prevB.X) / 2, (prevA.Y + prevB.Y) / 2);
            Point curCenter = new Point((curA.X + curB.X) / 2, (curA.Y + curB.Y) / 2);
            double prevDistance = (prevB - prevA).Length;
            double curDistance = (curB - curA).Length;
            double prevAngle = Math.Atan2(prevB.Y - prevA.Y, prevB.X - prevA.X);
            double curAngle = Math.Atan2(curB.Y - curA.Y, curB.X - curA.X);
            double angleDegrees = (curAngle - prevAngle) * 180.0 / Math.PI;

            bool isBoard = currentMode == 1;
            bool enableTranslate = IsBoardRoamingMode || (isBoard
                ? Settings.Gesture.IsEnableTwoFingerTranslateBoard
                : Settings.Gesture.IsEnableTwoFingerTranslate);
            bool enableRotate = !IsBoardRoamingMode && (isBoard
                ? Settings.Gesture.IsEnableTwoFingerRotationBoard
                : Settings.Gesture.IsEnableTwoFingerRotation);
            bool enableZoom = !IsBoardRoamingMode && (isBoard
                ? Settings.Gesture.IsEnableTwoFingerZoomBoard
                : Settings.Gesture.IsEnableTwoFingerZoom);

            double scale = prevDistance > 0.01 ? curDistance / prevDistance : 1.0;
            if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0.05 || scale >= 20) scale = 1.0;

            var matrix = new Matrix();
            if (enableTranslate)
                matrix.Translate(curCenter.X - prevCenter.X, curCenter.Y - prevCenter.Y);
            if (enableRotate && Math.Abs(angleDegrees) > 0.01)
                matrix.RotateAt(angleDegrees, curCenter.X, curCenter.Y);
            if (enableZoom && Math.Abs(scale - 1.0) > 0.0001)
                matrix.ScaleAt(scale, scale, curCenter.X, curCenter.Y);

            if (enableTranslate || enableRotate || enableZoom)
                ApplyHidGestureMatrix(matrix, enableZoom ? scale : 1.0);

            _hidGestureLastA = curA;
            _hidGestureLastB = curB;
        }

        private void ApplyHidGestureMatrix(Matrix matrix, double scale)
        {
            // 有快照时，只动叠加层的 RenderTransform：GPU 合成，不触发 InkCanvas 的
            // StrokesChanged/布局。抬手后再把累计矩阵一次性写回笔画模型。
            if (_hidGestureSnapshot != null)
            {
                _hidGestureTransform = Matrix.Multiply(_hidGestureTransform, matrix);
                _hidGestureSnapshot.RenderTransform = new MatrixTransform(_hidGestureTransform);
                _hidGestureWidthScale *= scale;
                return;
            }

            StrokeCollection selected = inkCanvas.GetSelectedStrokes();
            if (selected != null && selected.Count > 0)
            {
                selected.Transform(matrix, false);
                if (scale != 1.0)
                    foreach (Stroke stroke in selected) ScaleStrokeWidth(stroke, scale);
            }
            else
            {
                inkCanvas.Strokes.Transform(matrix, false);
                if (scale != 1.0)
                    foreach (Stroke stroke in inkCanvas.Strokes) ScaleStrokeWidth(stroke, scale);
                TransformCanvasImages(matrix);
            }
            inkCanvas.InvalidateVisual();
        }

        private static void ScaleStrokeWidth(Stroke stroke, double scale)
        {
            try
            {
                stroke.DrawingAttributes.Width *= scale;
                stroke.DrawingAttributes.Height *= scale;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        private void ClearHidTouchState()
        {
            foreach (int id in _hidTouches.Keys.ToArray())
            {
                FinishExternalTouchStroke(HidTouchExternalIdBase - id, false);
                try { _palmEngine.Remove(PalmContactSource.RawHid, id); } catch { }
            }
            _hidTouches.Clear();
            _hidStrokeBaseline.Clear();
            _hidTouchTracker?.Reset();
            EndHidGestureSession();
            FinishExternalPalmIfIdle();
        }
    }
}
