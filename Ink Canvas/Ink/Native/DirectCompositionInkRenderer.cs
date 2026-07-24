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
                        else if (ApplySnapshot(item.RenderSnapshot))
                        {
                            hadWork = true;
                        }
                    }
                }

                if (!hadWork && !_needsPresent)
                    return WetInkApplyResult.NoWork();

                PresentFrame(forceClear: false);
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
            if (_d2dContext == null || _swapChain == null || _target == null)
                return;

            _d2dContext.BeginDraw();
            _d2dContext.Clear(new Color4(0f, 0f, 0f, 0f));

            if (!forceClear && _target.IsVisible)
            {
                ApplyExclusionClips();
                foreach (var pair in _sessions)
                    pair.Value.Draw(_d2dContext, _brush);
            }

            _d2dContext.EndDraw().CheckError();
            _swapChain.Present(0, PresentFlags.None).CheckError();
            _compositionDevice?.Commit().CheckError();
            _needsPresent = false;
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
            _adapter = _dxgiDevice.GetAdapter();
            _dxgiFactory = _adapter.GetParent<IDXGIFactory2>();
            _d2dFactory = Vortice.Direct2D1.D2D1.D2D1CreateFactory<ID2D1Factory1>(
                FactoryType.SingleThreaded,
                DebugLevel.None);
            _d2dDevice = _d2dFactory.CreateDevice(_dxgiDevice);
            _d2dContext = _d2dDevice.CreateDeviceContext(DeviceContextOptions.None);
            _d2dContext.UnitMode = UnitMode.Dips;
            _brush = _d2dContext.CreateSolidColorBrush(new Color4(1f, 1f, 1f, 1f), null);
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
                SwapEffect = SwapEffect.FlipSequential,
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
            DisposeAndNull(ref _brush);
            DisposeAndNull(ref _d2dContext);
            DisposeAndNull(ref _d2dDevice);
            DisposeAndNull(ref _d2dFactory);
            DisposeAndNull(ref _dxgiFactory);
            DisposeAndNull(ref _adapter);
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
            private readonly List<WetInkTip> _fixedStartTips = new List<WetInkTip>();
            private readonly List<WetInkTip> _fixedEndTips = new List<WetInkTip>();
            private ID2D1PathGeometry _dynamicPath;
            private WetInkTip _dynamicStartTip;
            private WetInkTip _dynamicEndTip;
            private bool _dynamicIsSinglePoint;
            private bool _hasDynamic;
            private uint _colorArgb;
            private int _publishedFixedCount;

            public SessionResources(WetInkGeometryBuilder builder)
            {
                _geometryState = new WetInkStrokeGeometryState(builder);
            }

            public void Update(WetInkRenderSnapshot snapshot, ID2D1Factory1 factory)
            {
                var update = _geometryState.Update(snapshot);
                _colorArgb = snapshot.Style.ColorArgb;

                if (update.Reset)
                {
                    // Baseline was replaced mid-stroke (e.g. pause straightening):
                    // discard accumulated fixed geometry and rebuild from scratch.
                    for (var i = 0; i < _fixedPaths.Count; i++)
                    {
                        var path = _fixedPaths[i];
                        if (path != null)
                            path.Dispose();
                    }
                    _fixedPaths.Clear();
                    _fixedStartTips.Clear();
                    _fixedEndTips.Clear();
                    _publishedFixedCount = 0;
                }

                while (_publishedFixedCount < update.FixedSegments.Count)
                {
                    var segment = update.FixedSegments[_publishedFixedCount];
                    _fixedPaths.Add(CreatePath(factory, segment));
                    _fixedStartTips.Add(segment.StartTip);
                    _fixedEndTips.Add(segment.EndTip);
                    _publishedFixedCount++;
                }

                DisposePath(ref _dynamicPath);
                _hasDynamic = false;
                _dynamicIsSinglePoint = false;
                if (update.DynamicTail != null)
                {
                    _dynamicIsSinglePoint = update.DynamicTail.IsSinglePoint;
                    if (_dynamicIsSinglePoint
                        || (update.DynamicTail.Outline != null
                            && update.DynamicTail.Outline.Count >= 3))
                    {
                        if (!_dynamicIsSinglePoint)
                            _dynamicPath = CreatePath(factory, update.DynamicTail);
                        _dynamicStartTip = update.DynamicTail.StartTip;
                        _dynamicEndTip = update.DynamicTail.EndTip;
                        _hasDynamic = true;
                    }
                }
            }

            public void Draw(ID2D1DeviceContext context, ID2D1SolidColorBrush brush)
            {
                brush.Color = ToPremultipliedColor(_colorArgb);

                for (var i = 0; i < _fixedPaths.Count; i++)
                {
                    var path = _fixedPaths[i];
                    if (path != null)
                        context.FillGeometry(path, brush);

                    // Avoid translucent overblend at fixed-segment junctions:
                    // only the first segment draws its start tip; intermediate
                    // end tips are omitted because the next segment continues.
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

            public void Dispose()
            {
                for (var i = 0; i < _fixedPaths.Count; i++)
                {
                    var path = _fixedPaths[i];
                    if (path != null)
                        path.Dispose();
                }

                _fixedPaths.Clear();
                _fixedStartTips.Clear();
                _fixedEndTips.Clear();
                DisposePath(ref _dynamicPath);
            }

            private static ID2D1PathGeometry CreatePath(
                ID2D1Factory1 factory,
                WetInkRibbonGeometry geometry)
            {
                if (geometry == null
                    || geometry.Outline == null
                    || geometry.Outline.Count < 3)
                {
                    return null;
                }

                var path = factory.CreatePathGeometry();
                using (var sink = path.Open())
                {
                    // Ribbon outlines self-intersect on sharp turns / segment overlaps.
                    // Alternate (even-odd) fill punches transparent holes there; winding
                    // keeps the union of the stroke solid like WPF ink.
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

                return path;
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

            private static Color4 ToPremultipliedColor(uint colorArgb)
            {
                var a = ((colorArgb >> 24) & 0xFF) / 255f;
                var r = ((colorArgb >> 16) & 0xFF) / 255f;
                var g = ((colorArgb >> 8) & 0xFF) / 255f;
                var b = (colorArgb & 0xFF) / 255f;
                return new Color4(r * a, g * a, b * a, a);
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
