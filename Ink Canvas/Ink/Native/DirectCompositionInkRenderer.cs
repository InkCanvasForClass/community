using System;
using System.Collections.Generic;
using System.Numerics;
using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DirectComposition;
using Vortice.DXGI;
using Vortice.Mathematics;
using D2DAlphaMode = Vortice.DCommon.AlphaMode;
using D3DFeatureLevel = Vortice.Direct3D.FeatureLevel;
using DXGIAlphaMode = Vortice.DXGI.AlphaMode;

namespace Ink_Canvas.Ink.Native
{
    internal sealed class DirectCompositionInkRenderer : IWetInkBatchRenderer
    {
        private static readonly D3DFeatureLevel[] FeatureLevels =
        {
            D3DFeatureLevel.Level_11_1,
            D3DFeatureLevel.Level_11_0,
            D3DFeatureLevel.Level_10_1,
            D3DFeatureLevel.Level_10_0
        };

        private readonly WetInkGeometryBuilder _geometryBuilder = new WetInkGeometryBuilder();
        private readonly Dictionary<long, SessionResources> _sessions =
            new Dictionary<long, SessionResources>();
        private readonly List<WetInkRetirementAck> _pendingRetirementAcks =
            new List<WetInkRetirementAck>();

        private ID3D11Device _d3dDevice;
        private ID3D11DeviceContext _d3dContext;
        private IDXGIDevice _dxgiDevice;
        private IDXGIDevice1 _dxgiDevice1;
        private IDXGIAdapter _adapter;
        private IDXGIFactory2 _dxgiFactory;
        private ID2D1Factory1 _d2dFactory;
        private ID2D1Device _d2dDevice;
        private ID2D1DeviceContext _d2dContext;
        private IDCompositionDevice _compositionDevice;
        private IDCompositionTarget _compositionTarget;
        private IDCompositionVisual _compositionVisual;
        private IDXGISwapChain1 _swapChain;
        private ID2D1Bitmap1 _targetBitmap;
        private ID2D1SolidColorBrush _brush;
        private ID2D1StrokeStyle _laserStrokeStyle;
        private IntPtr _hwnd;
        private WetInkTargetSnapshot _target;
        private bool _deviceReady;
        private bool _needsPresent;
        private bool _disposed;

        public void BindTarget(IntPtr hwnd, WetInkTargetSnapshot target)
        {
            EnsureNotDisposed();
            if (hwnd == IntPtr.Zero)
                throw new ArgumentOutOfRangeException(nameof(hwnd));
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            ReleaseCompositionTree();
            ReleaseSwapChainResources();
            ReleaseDeviceResources();

            _hwnd = hwnd;
            _target = target;
            CreateDeviceResources();
            CreateCompositionTree();
            CreateSwapChainResources();
            _deviceReady = true;
            _needsPresent = true;
            PresentFrame(forceClear: true);
        }

        public void UpdateTarget(WetInkTargetSnapshot target)
        {
            EnsureNotDisposed();
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (!_deviceReady)
            {
                _target = target;
                return;
            }

            var previous = _target;
            _target = target;
            if (previous == null
                || previous.ScreenBounds.Width != target.ScreenBounds.Width
                || previous.ScreenBounds.Height != target.ScreenBounds.Height
                || Math.Abs(previous.DpiX - target.DpiX) > 0.01f
                || Math.Abs(previous.DpiY - target.DpiY) > 0.01f)
            {
                ResizeSwapChain();
            }

            _needsPresent = true;
            PresentFrame(forceClear: false);
        }

        public WetInkApplyResult Apply(WetInkMailboxBatch batch)
        {
            EnsureNotDisposed();
            if (!_deviceReady || _target == null || _target.ScreenBounds.IsEmpty)
                return WetInkApplyResult.NoWork();

            _pendingRetirementAcks.Clear();
            var hadWork = false;
            var snapshotUpdateTicks = 0L;
            try
            {
                if (batch != null)
                {
                    for (var i = 0; i < batch.OrderedItems.Count; i++)
                    {
                        var item = batch.OrderedItems[i];
                        if (item.Kind == WetInkMailboxItemKind.Boundary)
                        {
                            if (ApplyBoundary(item.BoundaryCommand))
                                hadWork = true;
                        }
                        else if (ApplySnapshotTimed(item.RenderSnapshot, ref snapshotUpdateTicks))
                        {
                            hadWork = true;
                        }
                    }
                }

                if (!hadWork && !_needsPresent)
                    return WetInkApplyResult.NoWork();

                var drawStart = System.Diagnostics.Stopwatch.GetTimestamp();
                var drawMs = PresentFrameTimed(forceClear: false, out var presentMs);
                var freq = System.Diagnostics.Stopwatch.Frequency;
                NativeInkPerfProbe.RecordApplySegments(
                    snapshotUpdateTicks * 1000.0 / freq,
                    drawMs,
                    presentMs);

                var acks = _pendingRetirementAcks.Count == 0
                    ? Array.Empty<WetInkRetirementAck>()
                    : _pendingRetirementAcks.ToArray();
                _pendingRetirementAcks.Clear();
                return WetInkApplyResult.Success(acks);
            }
            catch (Exception ex) when (IsDeviceLost(ex))
            {
                _deviceReady = false;
                return WetInkApplyResult.DeviceLost(ex);
            }
            catch (Exception ex)
            {
                return WetInkApplyResult.Failed(ex);
            }
        }

        private bool ApplySnapshotTimed(WetInkRenderSnapshot snapshot, ref long snapshotUpdateTicks)
        {
            var start = System.Diagnostics.Stopwatch.GetTimestamp();
            var result = ApplySnapshot(snapshot);
            snapshotUpdateTicks += System.Diagnostics.Stopwatch.GetTimestamp() - start;
            return result;
        }

        public void PresentIdleClear()
        {
            EnsureNotDisposed();
            if (!_deviceReady)
                return;
            PresentFrame(forceClear: true);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            ClearSessions();
            ReleaseCompositionTree();
            ReleaseSwapChainResources();
            ReleaseDeviceResources();
        }

        private bool ApplyBoundary(WetInkBoundaryCommand command)
        {
            switch (command.Kind)
            {
                case WetInkBoundaryCommandKind.BeginStroke:
                    if (!_sessions.ContainsKey(command.SessionId))
                    {
                        _sessions.Add(
                            command.SessionId,
                            new SessionResources(_geometryBuilder));
                    }
                    return false;
                case WetInkBoundaryCommandKind.EndStroke:
                    return false;
                case WetInkBoundaryCommandKind.CancelStroke:
                    return RemoveSession(command.SessionId);
                case WetInkBoundaryCommandKind.RetireStroke:
                    if (RemoveSession(command.SessionId))
                    {
                        _pendingRetirementAcks.Add(
                            new WetInkRetirementAck(command.SessionId, command.Version));
                        return true;
                    }
                    return false;
                case WetInkBoundaryCommandKind.Reset:
                case WetInkBoundaryCommandKind.Shutdown:
                    return ClearSessions();
                case WetInkBoundaryCommandKind.Resize:
                    return false;
                default:
                    return false;
            }
        }

        private bool ApplySnapshot(WetInkRenderSnapshot snapshot)
        {
            if (snapshot == null)
                return false;
            if (!_sessions.TryGetValue(snapshot.SessionId, out var resources))
            {
                resources = new SessionResources(_geometryBuilder);
                _sessions.Add(snapshot.SessionId, resources);
            }

            resources.Update(snapshot, _d2dFactory);
            _needsPresent = true;
            return true;
        }

        private void PresentFrame(bool forceClear)
        {
            PresentFrameTimed(forceClear, out _);
        }

        /// <summary>
        /// 与 PresentFrame 相同，但把「绘制几何」与「Present+Commit」耗时分开返回。
        /// drawMs = BeginDraw..EndDraw（D2D 绘制）；presentMs = Present + DComp Commit（同步等待主导）。
        /// </summary>
        private double PresentFrameTimed(bool forceClear, out double presentMs)
        {
            var drawMs = 0.0;
            presentMs = 0.0;
            if (_d2dContext == null || _swapChain == null || _target == null)
                return drawMs;

            var freq = System.Diagnostics.Stopwatch.Frequency;
            var drawStart = System.Diagnostics.Stopwatch.GetTimestamp();

            _d2dContext.BeginDraw();
            // FlipDiscard 下 back buffer 内容在下一次 Present 后即失效（未定义），
            // 必须每次 Clear，否则新帧会叠加上上帧的残留像素导致"重复+抖动"。
            _d2dContext.Clear(new Color4(0f, 0f, 0f, 0f));

            if (!forceClear && _target.IsVisible)
            {
                ApplyExclusionClips();
                foreach (var pair in _sessions)
                    pair.Value.Draw(_d2dContext, _brush, _laserStrokeStyle);
            }

            drawMs = (System.Diagnostics.Stopwatch.GetTimestamp() - drawStart) * 1000.0 / freq;

            var presentStart = System.Diagnostics.Stopwatch.GetTimestamp();
            var endDrawResult = _d2dContext.EndDraw();
            if (endDrawResult.Failure)
            {
                var code = (uint)endDrawResult.Code;
                if (IsDeviceLostResult(code))
                {
                    _deviceReady = false;
                    return drawMs;
                }
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[WetInk] D2D EndDraw failed: 0x{code:X8}");
                }
                catch
                {
                    // never throw from the render hot path
                }
            }
            PresentWithoutThrow();
            try { _compositionDevice?.Commit(); }
            catch (Exception ex) { LogWithoutThrow("DComp Commit", ex); }
            _needsPresent = false;
            presentMs = (System.Diagnostics.Stopwatch.GetTimestamp() - presentStart) * 1000.0 / freq;
            return drawMs;
        }

        private void LogWithoutThrow(string context, Exception ex)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[WetInk] {context} failed: {ex.Message}");
            }
            catch
            {
                // never throw from the render hot path
            }
        }

        private void PresentWithoutThrow()
        {
            // DO_NOT_WAIT：GPU 无可用帧时直接返回 DXGI_ERROR_WAS_STILL_DRAWING，
            // 避免把渲染线程卡在 vsync 上阻塞下一帧输入。
            var presentStart = System.Diagnostics.Stopwatch.GetTimestamp();
            var hr = _swapChain.Present(0, PresentFlags.DoNotWait);
            var presentElapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - presentStart;
            if (hr.Failure)
            {
                var code = (uint)hr.Code;
                if (code == (uint)Vortice.DXGI.ResultCode.WasStillDrawing)
                {
                    // 上一帧 GPU 还在画，静默丢弃本帧；记录频次与耗时供诊断。
                    NativeInkPerfProbe.RecordPresentWasStillDrawing(
                        presentElapsedTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
                    return;
                }
                if (IsDeviceLostResult(code))
                {
                    _deviceReady = false;
                    return;
                }
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[WetInk] Present failed: 0x{code:X8}");
                }
                catch
                {
                    // never throw from the render hot path
                }
            }
        }

        private static bool IsDeviceLostResult(uint code)
        {
            return code == (uint)Vortice.DXGI.ResultCode.DeviceRemoved
                   || code == (uint)Vortice.DXGI.ResultCode.DeviceReset;
        }

        private void ApplyExclusionClips()
        {
            // Exclusion rectangles are logical holes for toolbar UI. Direct2D
            // does not support subtractive clips natively; the host keeps the
            // overlay input-transparent and WPF popups stay above by Z-order.
            // Pixel exclusion is applied by skipping draws entirely when the
            // target is hidden. Per-rect geometric clipping can be layered on
            // later without changing the mailbox contract.
        }

        private void CreateDeviceResources()
        {
            Vortice.Direct3D11.D3D11.D3D11CreateDevice(
                IntPtr.Zero,
                DriverType.Hardware,
                DeviceCreationFlags.BgraSupport,
                FeatureLevels,
                out _d3dDevice,
                out _,
                out _d3dContext).CheckError();

            _dxgiDevice = _d3dDevice.QueryInterface<IDXGIDevice>();
            try
            {
                _dxgiDevice1 = _dxgiDevice.QueryInterface<IDXGIDevice1>();
                // 驱动预渲染队列只留 1 帧，避免 Present 排队缓冲进一步增大输入到光子延迟。
                _dxgiDevice1.SetMaximumFrameLatency(1);
            }
            catch
            {
                _dxgiDevice1 = null;
                // 部分老驱动不支持；回退到默认帧延迟。
            }
            _adapter = _dxgiDevice.GetAdapter();
            _dxgiFactory = _adapter.GetParent<IDXGIFactory2>();
            _d2dFactory = Vortice.Direct2D1.D2D1.D2D1CreateFactory<ID2D1Factory1>(
                FactoryType.SingleThreaded,
                DebugLevel.None);
            _d2dDevice = _d2dFactory.CreateDevice(_dxgiDevice);
            _d2dContext = _d2dDevice.CreateDeviceContext(DeviceContextOptions.None);
            _d2dContext.UnitMode = UnitMode.Dips;
            _brush = _d2dContext.CreateSolidColorBrush(new Color4(1f, 1f, 1f, 1f), null);
            _laserStrokeStyle = _d2dFactory.CreateStrokeStyle(new StrokeStyleProperties
            {
                StartCap = CapStyle.Flat,
                EndCap = CapStyle.Flat,
                DashCap = CapStyle.Flat,
                LineJoin = LineJoin.Round,
                DashStyle = DashStyle.Solid,
                MiterLimit = 1f,
                DashOffset = 0f
            });
            _compositionDevice =
                DComp.DCompositionCreateDevice<IDCompositionDevice>(_dxgiDevice);
        }

        private void CreateCompositionTree()
        {
            _compositionDevice.CreateTargetForHwnd(_hwnd, true, out _compositionTarget)
                .CheckError();
            _compositionDevice.CreateVisual(out _compositionVisual).CheckError();
            _compositionTarget.SetRoot(_compositionVisual).CheckError();
            _compositionDevice.Commit().CheckError();
        }

        private void CreateSwapChainResources()
        {
            var width = Math.Max(1, _target.ScreenBounds.Width);
            var height = Math.Max(1, _target.ScreenBounds.Height);
            var description = new SwapChainDescription1
            {
                Width = width,
                Height = height,
                Format = Format.B8G8R8A8_UNorm,
                Stereo = false,
                SampleDescription = new SampleDescription(1, 0),
                BufferUsage = Usage.RenderTargetOutput,
                BufferCount = 2,
                Scaling = Scaling.Stretch,
                // FlipDiscard：与 FlipSequential 相同的低延迟 flip 语义，
                // 但缓冲内容在下一次 present 后即失效，允许 D2D 不再先 Clear 再画。
                SwapEffect = SwapEffect.FlipDiscard,
                AlphaMode = DXGIAlphaMode.Premultiplied,
                Flags = SwapChainFlags.None
            };

            _swapChain = _dxgiFactory.CreateSwapChainForComposition(
                _d3dDevice,
                description,
                null);
            _compositionVisual.SetContent(_swapChain).CheckError();
            _compositionDevice.Commit().CheckError();
            CreateTargetBitmap();
        }

        private void ResizeSwapChain()
        {
            if (_swapChain == null || _target == null)
                return;

            _d2dContext.Target = null;
            if (_targetBitmap != null)
            {
                _targetBitmap.Dispose();
                _targetBitmap = null;
            }

            var width = Math.Max(1, _target.ScreenBounds.Width);
            var height = Math.Max(1, _target.ScreenBounds.Height);
            _swapChain.ResizeBuffers(
                2,
                width,
                height,
                Format.B8G8R8A8_UNorm,
                SwapChainFlags.None).CheckError();
            CreateTargetBitmap();
        }

        private void CreateTargetBitmap()
        {
            using (var surface = _swapChain.GetBuffer<IDXGISurface>(0))
            {
                var properties = new BitmapProperties1(
                    new PixelFormat(Format.B8G8R8A8_UNorm, D2DAlphaMode.Premultiplied),
                    _target.DpiX,
                    _target.DpiY,
                    BitmapOptions.Target | BitmapOptions.CannotDraw);
                _targetBitmap = _d2dContext.CreateBitmapFromDxgiSurface(surface, properties);
            }

            _d2dContext.Target = _targetBitmap;
            _d2dContext.Dpi = new System.Drawing.SizeF(_target.DpiX, _target.DpiY);
        }

        private bool RemoveSession(long sessionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var resources))
                return false;
            resources.Dispose();
            _sessions.Remove(sessionId);
            _needsPresent = true;
            return true;
        }

        private bool ClearSessions()
        {
            if (_sessions.Count == 0)
                return false;
            foreach (var pair in _sessions)
                pair.Value.Dispose();
            _sessions.Clear();
            _needsPresent = true;
            return true;
        }

        private void ReleaseCompositionTree()
        {
            if (_compositionTarget != null)
            {
                try { _compositionTarget.SetRoot(null); }
                catch { /* best-effort shutdown */ }
            }

            if (_compositionVisual != null)
            {
                try { _compositionVisual.SetContent(null); }
                catch { /* best-effort shutdown */ }
            }

            if (_compositionDevice != null)
            {
                try { _compositionDevice.Commit(); }
                catch { /* best-effort shutdown */ }
            }

            DisposeAndNull(ref _compositionVisual);
            DisposeAndNull(ref _compositionTarget);
            DisposeAndNull(ref _compositionDevice);
        }

        private void ReleaseSwapChainResources()
        {
            if (_d2dContext != null)
                _d2dContext.Target = null;
            DisposeAndNull(ref _targetBitmap);
            DisposeAndNull(ref _swapChain);
        }

        private void ReleaseDeviceResources()
        {
            DisposeAndNull(ref _laserStrokeStyle);
            DisposeAndNull(ref _brush);
            DisposeAndNull(ref _d2dContext);
            DisposeAndNull(ref _d2dDevice);
            DisposeAndNull(ref _d2dFactory);
            DisposeAndNull(ref _dxgiFactory);
            DisposeAndNull(ref _adapter);
            DisposeAndNull(ref _dxgiDevice1);
            DisposeAndNull(ref _dxgiDevice);
            DisposeAndNull(ref _d3dContext);
            DisposeAndNull(ref _d3dDevice);
            _deviceReady = false;
        }

        private static bool IsDeviceLost(Exception ex)
        {
            var text = ex.ToString();
            return text.IndexOf("DXGI_ERROR_DEVICE_REMOVED", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("DXGI_ERROR_DEVICE_RESET", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("D2DERR_RECREATE_TARGET", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("Device removed", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DirectCompositionInkRenderer));
        }

        private static void DisposeAndNull<T>(ref T resource) where T : class, IDisposable
        {
            if (resource == null)
                return;
            try { resource.Dispose(); }
            catch { /* best-effort shutdown */ }
            resource = null;
        }

        private sealed class SessionResources : IDisposable
        {
            private readonly WetInkStrokeGeometryState _geometryState;
            private readonly List<ID2D1PathGeometry> _fixedPaths = new List<ID2D1PathGeometry>();
            private readonly List<ID2D1PathGeometry> _fixedLaserCenterPaths = new List<ID2D1PathGeometry>();
            private readonly List<WetInkTip> _fixedStartTips = new List<WetInkTip>();
            private readonly List<WetInkTip> _fixedEndTips = new List<WetInkTip>();
            private ID2D1PathGeometry _dynamicPath;
            private ID2D1PathGeometry _dynamicLaserCenterPath;
            private WetInkTip _dynamicStartTip;
            private WetInkTip _dynamicEndTip;
            private bool _dynamicIsSinglePoint;
            private bool _hasDynamic;
            private uint _colorArgb;
            private InkRenderMode _renderMode;
            private float _strokeWidth;
            private float _strokeHeight;
            private int _publishedFixedCount;
            // B11: 颜色每帧只算一次，Draw 时直接复用，避免每帧 3 次 Color4 构造 + GPU 状态同步。
            private Color4 _premultipliedColor;
            private Color4 _premultipliedLaserGlow;
            private Color4 _premultipliedLaserBody;
            private Color4 _premultipliedLaserCore;

            public SessionResources(WetInkGeometryBuilder builder)
            {
                _geometryState = new WetInkStrokeGeometryState(builder);
            }

            public void Update(WetInkRenderSnapshot snapshot, ID2D1Factory1 factory)
            {
                var update = _geometryState.Update(snapshot);
                if (_colorArgb != snapshot.Style.ColorArgb)
                {
                    _colorArgb = snapshot.Style.ColorArgb;
                    // B11: 颜色变了才重算，颜色不变直接复用上次缓存。
                    _premultipliedColor = ToPremultipliedColor(_colorArgb);
                    _premultipliedLaserGlow = ResolveLaserGlowColor(_colorArgb);
                    _premultipliedLaserBody = ResolveLaserBodyColor(_colorArgb);
                    _premultipliedLaserCore = ResolveLaserCoreColor(_colorArgb);
                }
                _renderMode = snapshot.Style.RenderMode;
                _strokeWidth = (float)Math.Max(0.1, snapshot.Style.Width);
                _strokeHeight = (float)Math.Max(0.1, snapshot.Style.Height);

                if (update.Reset)
                {
                    DisposePaths(_fixedPaths);
                    DisposePaths(_fixedLaserCenterPaths);
                    _fixedStartTips.Clear();
                    _fixedEndTips.Clear();
                    _publishedFixedCount = 0;
                }

                while (_publishedFixedCount < update.FixedSegments.Count)
                {
                    var segment = update.FixedSegments[_publishedFixedCount];
                    _fixedPaths.Add(CreatePath(factory, segment));
                    _fixedLaserCenterPaths.Add(CreateLaserCenterPath(factory, segment));
                    _fixedStartTips.Add(segment.StartTip);
                    _fixedEndTips.Add(segment.EndTip);
                    _publishedFixedCount++;
                }

                DisposePath(ref _dynamicPath);
                DisposePath(ref _dynamicLaserCenterPath);
                _hasDynamic = false;
                _dynamicIsSinglePoint = false;
                if (update.DynamicTail != null)
                {
                    _dynamicIsSinglePoint = update.DynamicTail.IsSinglePoint;
                    if (_dynamicIsSinglePoint)
                    {
                        // 单点笔尾不创建 path，Draw 走 DrawTip 分支。
                        _dynamicStartTip = update.DynamicTail.StartTip;
                        _dynamicEndTip = update.DynamicTail.EndTip;
                        _hasDynamic = true;
                    }
                    else if (update.DynamicTail.Outline != null
                        && update.DynamicTail.Outline.Count >= 3)
                    {
                        // 几何有效时才复用/新建 path；无效几何保持 null 让 Draw 跳过。
                        _dynamicPath = RewritePath(factory, _dynamicPath, update.DynamicTail);
                        _dynamicLaserCenterPath = RewriteLaserCenterPath(factory, _dynamicLaserCenterPath, update.DynamicTail);
                        _dynamicStartTip = update.DynamicTail.StartTip;
                        _dynamicEndTip = update.DynamicTail.EndTip;
                        _hasDynamic = true;
                    }
                    // 几何无效：_hasDynamic 保持 false，Draw 跳过动态笔尾。
                }
            }

            public void Draw(ID2D1DeviceContext context, ID2D1SolidColorBrush brush, ID2D1StrokeStyle laserStrokeStyle)
            {
                if (_renderMode == InkRenderMode.Laser)
                {
                    DrawLaser(context, brush, laserStrokeStyle);
                    return;
                }

                DrawStandard(context, brush);
            }

            private void DrawStandard(ID2D1DeviceContext context, ID2D1SolidColorBrush brush)
            {
                brush.Color = _premultipliedColor;

                for (var i = 0; i < _fixedPaths.Count; i++)
                {
                    var path = _fixedPaths[i];
                    if (path != null)
                        context.FillGeometry(path, brush);

                    if (i == 0)
                        DrawTip(context, brush, _fixedStartTips[i]);
                    if (i == _fixedPaths.Count - 1 && !_hasDynamic)
                        DrawTip(context, brush, _fixedEndTips[i]);
                }

                if (!_hasDynamic)
                    return;

                if (_dynamicIsSinglePoint)
                {
                    DrawTip(context, brush, _dynamicStartTip);
                    return;
                }

                if (_dynamicPath != null)
                    context.FillGeometry(_dynamicPath, brush);
                if (_fixedPaths.Count == 0)
                    DrawTip(context, brush, _dynamicStartTip);
                DrawTip(context, brush, _dynamicEndTip);
            }

            private void DrawLaser(ID2D1DeviceContext context, ID2D1SolidColorBrush brush, ID2D1StrokeStyle laserStrokeStyle)
            {
                var bodyThickness = Math.Max(0.8f, Math.Max(_strokeWidth, _strokeHeight));
                var glowThickness = Math.Max(bodyThickness * 2.2f, bodyThickness + 4f);
                var coreThickness = Math.Max(0.75f, bodyThickness * 0.38f);

                for (var i = 0; i < _fixedLaserCenterPaths.Count; i++)
                {
                    var path = _fixedLaserCenterPaths[i];
                    if (path != null)
                    {
                        DrawLaserGeometry(context, brush, laserStrokeStyle, path, _premultipliedLaserGlow, _premultipliedLaserBody, _premultipliedLaserCore, glowThickness, bodyThickness, coreThickness);
                    }

                    if (i == 0)
                        DrawLaserTip(context, brush, _fixedStartTips[i], glowThickness, bodyThickness, coreThickness);
                    if (i == _fixedLaserCenterPaths.Count - 1 && !_hasDynamic)
                        DrawLaserTip(context, brush, _fixedEndTips[i], glowThickness, bodyThickness, coreThickness);
                }

                if (!_hasDynamic)
                    return;

                if (_dynamicIsSinglePoint)
                {
                    DrawLaserTip(context, brush, _dynamicStartTip, glowThickness, bodyThickness, coreThickness);
                    return;
                }

                if (_dynamicLaserCenterPath != null)
                    DrawLaserGeometry(context, brush, laserStrokeStyle, _dynamicLaserCenterPath, _premultipliedLaserGlow, _premultipliedLaserBody, _premultipliedLaserCore, glowThickness, bodyThickness, coreThickness);
                if (_fixedLaserCenterPaths.Count == 0)
                    DrawLaserTip(context, brush, _dynamicStartTip, glowThickness, bodyThickness, coreThickness);
                DrawLaserTip(context, brush, _dynamicEndTip, glowThickness, bodyThickness, coreThickness);
            }

            public void Dispose()
            {
                DisposePaths(_fixedPaths);
                DisposePaths(_fixedLaserCenterPaths);
                _fixedStartTips.Clear();
                _fixedEndTips.Clear();
                DisposePath(ref _dynamicPath);
                DisposePath(ref _dynamicLaserCenterPath);
            }

            private static ID2D1PathGeometry CreatePath(
                ID2D1Factory1 factory,
                WetInkRibbonGeometry geometry)
            {
                var path = factory.CreatePathGeometry();
                WritePathGeometry(path, geometry);
                return path;
            }

            /// <summary>
            /// 复用已有 path 的 geometry sink 重画，避免每帧 Dispose+Create 一个
            /// ID2D1PathGeometry 资源。path 为 null 时新建。
            /// </summary>
            private static ID2D1PathGeometry RewritePath(
                ID2D1Factory1 factory,
                ID2D1PathGeometry existing,
                WetInkRibbonGeometry geometry)
            {
                var path = existing ?? factory.CreatePathGeometry();
                try
                {
                    WritePathGeometry(path, geometry);
                    return path;
                }
                catch
                {
                    // geometry 写入失败时重建（如设备状态异常）。
                    try { path.Dispose(); }
                    catch { /* best-effort */ }
                    path = factory.CreatePathGeometry();
                    WritePathGeometry(path, geometry);
                    return path;
                }
            }

            private static void WritePathGeometry(
                ID2D1PathGeometry path,
                WetInkRibbonGeometry geometry)
            {
                if (geometry == null
                    || geometry.Outline == null
                    || geometry.Outline.Count < 3)
                {
                    // 无有效轮廓：清空 path 的可见几何（不调用 EndFigure，
                    // 避免在未 BeginFigure 时进入 D2D sink 错误状态——
                    // 历史上这里会让 FillGeometry 抛错导致 EndDraw 失败、
                    // 后续帧不 Present，画面"重复+抖动"）。
                    using (var clear = path.Open())
                    {
                        clear.SetFillMode(Vortice.Direct2D1.FillMode.Winding);
                        clear.Close();
                    }
                    return;
                }

                using (var sink = path.Open())
                {
                    sink.SetFillMode(Vortice.Direct2D1.FillMode.Winding);
                    var first = geometry.Outline[0];
                    sink.BeginFigure(new Vector2(first.X, first.Y), FigureBegin.Filled);
                    for (var i = 1; i < geometry.Outline.Count; i++)
                    {
                        var point = geometry.Outline[i];
                        sink.AddLine(new Vector2(point.X, point.Y));
                    }

                    sink.EndFigure(FigureEnd.Closed);
                    sink.Close();
                }
            }

            private static ID2D1PathGeometry CreateLaserCenterPath(
                ID2D1Factory1 factory,
                WetInkRibbonGeometry geometry)
            {
                var path = factory.CreatePathGeometry();
                WriteLaserCenterPathGeometry(path, geometry);
                return path;
            }

            private static ID2D1PathGeometry RewriteLaserCenterPath(
                ID2D1Factory1 factory,
                ID2D1PathGeometry existing,
                WetInkRibbonGeometry geometry)
            {
                var path = existing ?? factory.CreatePathGeometry();
                try
                {
                    WriteLaserCenterPathGeometry(path, geometry);
                    return path;
                }
                catch
                {
                    try { path.Dispose(); }
                    catch { /* best-effort */ }
                    path = factory.CreatePathGeometry();
                    WriteLaserCenterPathGeometry(path, geometry);
                    return path;
                }
            }

            private static void WriteLaserCenterPathGeometry(
                ID2D1PathGeometry path,
                WetInkRibbonGeometry geometry)
            {
                if (geometry == null
                    || geometry.Outline == null
                    || geometry.Outline.Count < 4
                    || geometry.Outline.Count % 2 != 0)
                {
                    using (var clear = path.Open())
                    {
                        clear.EndFigure(FigureEnd.Open);
                        clear.Close();
                    }
                    return;
                }

                var centerCount = geometry.Outline.Count / 2;
                if (centerCount < 2)
                {
                    using (var clear = path.Open())
                    {
                        clear.EndFigure(FigureEnd.Open);
                        clear.Close();
                    }
                    return;
                }

                using (var sink = path.Open())
                {
                    var first = CenterPoint(geometry.Outline, 0);
                    sink.BeginFigure(new Vector2(first.X, first.Y), FigureBegin.Hollow);
                    for (var i = 1; i < centerCount; i++)
                    {
                        var point = CenterPoint(geometry.Outline, i);
                        sink.AddLine(new Vector2(point.X, point.Y));
                    }

                    sink.EndFigure(FigureEnd.Open);
                    sink.Close();
                }
            }

            private static WetInkVertex CenterPoint(IReadOnlyList<WetInkVertex> outline, int index)
            {
                var mirrored = outline.Count - 1 - index;
                var left = outline[index];
                var right = outline[mirrored];
                return new WetInkVertex(
                    (left.X + right.X) * 0.5f,
                    (left.Y + right.Y) * 0.5f);
            }

            private static void DrawLaserGeometry(
                ID2D1DeviceContext context,
                ID2D1SolidColorBrush brush,
                ID2D1StrokeStyle laserStrokeStyle,
                ID2D1PathGeometry path,
                Color4 glowColor,
                Color4 bodyColor,
                Color4 coreColor,
                float glowThickness,
                float bodyThickness,
                float coreThickness)
            {
                if (path == null)
                    return;

                brush.Color = glowColor;
                context.DrawGeometry(path, brush, glowThickness, laserStrokeStyle);
                brush.Color = bodyColor;
                context.DrawGeometry(path, brush, bodyThickness, laserStrokeStyle);
                brush.Color = coreColor;
                context.DrawGeometry(path, brush, coreThickness, laserStrokeStyle);
            }

            private void DrawLaserTip(
                ID2D1DeviceContext context,
                ID2D1SolidColorBrush brush,
                WetInkTip tip,
                float glowThickness,
                float bodyThickness,
                float coreThickness)
            {
                var bodyRadius = Math.Max(tip.RadiusX, tip.RadiusY);
                if (bodyRadius <= 0f)
                    return;

                var bodyScale = Math.Max(1f, bodyThickness / Math.Max(0.01f, Math.Max(_strokeWidth, _strokeHeight)));
                var glowScale = Math.Max(bodyScale * 1.6f, glowThickness / Math.Max(0.01f, bodyThickness));
                var coreScale = Math.Min(1f, coreThickness / Math.Max(0.01f, bodyThickness));

                brush.Color = _premultipliedLaserGlow;
                DrawScaledTip(context, brush, tip, glowScale);
                brush.Color = _premultipliedLaserBody;
                DrawScaledTip(context, brush, tip, bodyScale);
                brush.Color = _premultipliedLaserCore;
                DrawScaledTip(context, brush, tip, coreScale);
            }

            private static void DrawScaledTip(
                ID2D1DeviceContext context,
                ID2D1SolidColorBrush brush,
                WetInkTip tip,
                float scale)
            {
                if (scale <= 0f)
                    return;

                var radiusX = Math.Max(0.35f, tip.RadiusX * scale);
                var radiusY = Math.Max(0.35f, tip.RadiusY * scale);
                if (tip.Shape == InkStylusTipShape.Rectangle)
                {
                    var rect = new System.Drawing.RectangleF(
                        tip.Center.X - radiusX,
                        tip.Center.Y - radiusY,
                        radiusX * 2f,
                        radiusY * 2f);
                    context.FillRectangle(rect, brush);
                    return;
                }

                context.FillEllipse(
                    new Ellipse(
                        new Vector2(tip.Center.X, tip.Center.Y),
                        radiusX,
                        radiusY),
                    brush);
            }

            private static void DrawTip(
                ID2D1DeviceContext context,
                ID2D1SolidColorBrush brush,
                WetInkTip tip)
            {
                if (tip.RadiusX <= 0f && tip.RadiusY <= 0f)
                    return;

                if (tip.Shape == InkStylusTipShape.Rectangle)
                {
                    var rect = new System.Drawing.RectangleF(
                        tip.Center.X - tip.RadiusX,
                        tip.Center.Y - tip.RadiusY,
                        tip.RadiusX * 2f,
                        tip.RadiusY * 2f);
                    context.FillRectangle(rect, brush);
                    return;
                }

                context.FillEllipse(
                    new Ellipse(
                        new Vector2(tip.Center.X, tip.Center.Y),
                        tip.RadiusX,
                        tip.RadiusY),
                    brush);
            }

            private static Color4 ResolveLaserGlowColor(uint colorArgb) =>
                ToPremultipliedColor(colorArgb, 0.22f);

            private static Color4 ResolveLaserBodyColor(uint colorArgb) =>
                ToPremultipliedColor(BlendTowardWhite(colorArgb, 0.08f), 0.88f);

            private static Color4 ResolveLaserCoreColor(uint colorArgb) =>
                ToPremultipliedColor(BlendTowardWhite(colorArgb, 0.94f), 0.98f);

            private static uint BlendTowardWhite(uint colorArgb, float amount)
            {
                amount = Math.Max(0f, Math.Min(1f, amount));
                var a = (byte)((colorArgb >> 24) & 0xFF);
                var r = (byte)((colorArgb >> 16) & 0xFF);
                var g = (byte)((colorArgb >> 8) & 0xFF);
                var b = (byte)(colorArgb & 0xFF);
                var mixedR = (byte)Math.Round(r + ((255 - r) * amount));
                var mixedG = (byte)Math.Round(g + ((255 - g) * amount));
                var mixedB = (byte)Math.Round(b + ((255 - b) * amount));
                return ((uint)a << 24)
                       | ((uint)mixedR << 16)
                       | ((uint)mixedG << 8)
                       | mixedB;
            }

            private static Color4 ToPremultipliedColor(uint colorArgb, float alphaMultiplier)
            {
                var a = ((colorArgb >> 24) & 0xFF) / 255f;
                var r = ((colorArgb >> 16) & 0xFF) / 255f;
                var g = ((colorArgb >> 8) & 0xFF) / 255f;
                var b = (colorArgb & 0xFF) / 255f;
                var effectiveAlpha = Math.Max(0f, Math.Min(1f, a * alphaMultiplier));
                return new Color4(r * effectiveAlpha, g * effectiveAlpha, b * effectiveAlpha, effectiveAlpha);
            }

            private static Color4 ToPremultipliedColor(uint colorArgb)
            {
                var a = ((colorArgb >> 24) & 0xFF) / 255f;
                var r = ((colorArgb >> 16) & 0xFF) / 255f;
                var g = ((colorArgb >> 8) & 0xFF) / 255f;
                var b = (colorArgb & 0xFF) / 255f;
                return new Color4(r * a, g * a, b * a, a);
            }

            private static void DisposePaths(List<ID2D1PathGeometry> paths)
            {
                for (var i = 0; i < paths.Count; i++)
                {
                    var path = paths[i];
                    if (path != null)
                        path.Dispose();
                }

                paths.Clear();
            }

            private static void DisposePath(ref ID2D1PathGeometry path)
            {
                if (path == null)
                    return;
                path.Dispose();
                path = null;
            }
        }
    }
}
