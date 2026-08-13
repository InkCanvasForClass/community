using System;
using System.Collections.Generic;
using System.Linq;
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
        // 方案B：两阶段退休的阶段1缓存（RetireStroke→标记PendingRetire）。
        // Present 完成后把里面的 acks 搬到 _pendingRetirementAcks，这样 "OnNativeWetInkRetired"
        // 收到 ack 时，湿墨层实际上已经把最后一帧画出来了，不会出现空帧闪烘。
        private readonly List<WetInkRetirementAck> _pendingRetirementCommands =
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
        // 烘干统一清预测层：渲染线程每 Apply 一次就重算一次快照，UI 线程只读这个 volatile bool。
        // 不用 lock：volatile 读写本身原子；最多延迟一帧读到旧值，清 SwapChain 是幂等的完全安全。
        private volatile bool _hasActiveWetInkSnapshot;
        // 烘干统一清预测层：UI 线程设 flag，渲染线程在下次 Apply 时执行 D2D Clear+Present。
        // 避免从 UI 线程直接调 D2D/SwapChain 和渲染线程竞争。
        private volatile bool _pendingIdleClear;

        /// <summary>方案G：渲染线程判断是否仍存在需要上屏的湿墨视觉（活 session/待退休/强制 Present）。
        /// batch 为空时若返回 true，仍触发一次 Apply+Present 避免尾帧丢帧/残留。</summary>
        public bool HasPendingVisualWork
        {
            get
            {
                if (_disposed || !_deviceReady) return false;
                if (_needsPresent) return true;
                if (_pendingIdleClear) return true;
                if (_pendingRetirementCommands.Count > 0) return true;
                if (_sessions.Count > 0) return true;
                return false;
            }
        }

        /// <summary>烘干统一清预测层：是否存在任何「还在写」的活跃 session。
        /// 返回的是渲染线程最近一次 Apply 时的快照（可能最多延迟一帧），
        /// 但清 SwapChain 透明是幂等操作，完全安全。</summary>
        public bool HasActiveWetInkSessions
        {
            get
            {
                if (_disposed || !_deviceReady) return false;
                return _hasActiveWetInkSnapshot;
            }
        }

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

            // 方案B：把上一轮 Present 后真正要删除的 PendingRetire session 在这里处理掉，
            // 再执行本轮命令。这样 session 的生命周期是：
            //   RetireStroke 指令到 → MarkPendingRetire → 本轮 Present 仍然画最后一帧
            //   → 下次 Apply 开头才真正 Dispose/Remove，湿墨几何至少保留 1 帧 → 无空帧闪烘。
            CollectPendingRetirements();

            // 烘干统一清预测层：在本轮 session 状态稳定后，重算一次「是否存在正在写的笔」快照。
            // 注意在 ApplyBoundary（可能 AddSession/MarkEnded/MarkPendingRetire）之后再算一次，
            // 保证 UI 线程读到的值尽量新。
            UpdateActiveWetInkSnapshot();

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

                // 边界命令可能新增/结束 session 或标记退休，再刷一次。
                UpdateActiveWetInkSnapshot();

                if (!hadWork && !_needsPresent && !_pendingIdleClear)
                    return WetInkApplyResult.NoWork();

                var drawStart = System.Diagnostics.Stopwatch.GetTimestamp();
                var drawMs = PresentFrameTimed(forceClear: false, out var presentMs);
                var freq = System.Diagnostics.Stopwatch.Frequency;
                NativeInkPerfProbe.RecordApplySegments(
                    snapshotUpdateTicks * 1000.0 / freq,
                    drawMs,
                    presentMs);

                // 方案D：烘干前额外补偿帧。
                // 只要本轮有 RetireStroke 标记（_pendingRetirementCommands 非空）
                // 或任何 session 还处于 PendingRetire/Ended 状态，就再画一次并同步 Present(1)，
                // 用「完全不带预测的最终裁剪帧」强制覆盖 SwapChain，消除部分触摸屏上
                // 「前一帧预测尾没被完全刷新掉 → 湿墨与干墨之间残留一条预测残影」的双线/烘干现象。
                var needsCompensationFrame = _pendingRetirementCommands.Count > 0
                                              || _sessions.Values.Any(s => s.NeedsReliablePresent);
                if (needsCompensationFrame)
                {
                    try
                    {
                        // reliable=true：StillDrawing 时同步等 vsync；
                        // 用完全相同的几何再 Present 一次，确保最终帧稳定落在屏幕上。
                        PresentFrameTimed(forceClear: false, out _);
                    }
                    catch
                    {
                        // best-effort：补偿帧失败不影响主流程成功
                    }
                }

                // 烘干统一清预测层：如果有 pending 的 idle clear 请求（来自 ForceClearIdle），
                // 在正常绘制 + 补偿帧之后执行一次 forceClear Present，把 SwapChain 表面整体清透明。
                // 必须放在最后，确保 clear 帧是最终帧，覆盖一切残留预测像素。
                if (_pendingIdleClear)
                {
                    _pendingIdleClear = false;
                    PresentFrameTimed(forceClear: true, out _);
                }

                // 方案B：Present 结束之后，把本轮标记的待退休 acks 搬给调用者。
                // 此刻几何已上 SwapChain 合成到屏幕，再触发 OnNativeWetInkRetired
                // → RemoveSession（真正删） 就不会"提前清空"了。
                for (var i = 0; i < _pendingRetirementCommands.Count; i++)
                    _pendingRetirementAcks.Add(_pendingRetirementCommands[i]);
                _pendingRetirementCommands.Clear();

                // 修复：不能在这里 Clear _pendingRetirementAcks！
                // CollectPendingRetirements 在下一次 Apply 开头需要用它来真正 Dispose+Remove session。
                // 之前在这里 Clear 导致 session 永远不从 _sessions 移除 → 每帧都在画已退休的
                // session 几何（含预测残影）→ ForceClearIdle 清了表面后下一帧又画回来。
                // 只复制返回，保留原始 list 给下一帧 CollectPendingRetirements 消费。
                var acks = _pendingRetirementAcks.Count == 0
                    ? Array.Empty<WetInkRetirementAck>()
                    : _pendingRetirementAcks.ToArray();
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

        private void CollectPendingRetirements()
        {
            if (_pendingRetirementAcks.Count == 0 && _pendingRetirementCommands.Count == 0)
                return;
            // 这里只把"上一轮已经 Present 过、并回了 acks"的 session 真正移除。
            // _pendingRetirementAcks 里存的是「上一次 Apply 已经给调用方 ack 过」的 session，
            // 此时湿墨层早已带着该 session 的最后一帧显示完了，安全删除。
            if (_pendingRetirementAcks.Count > 0)
            {
                for (var i = 0; i < _pendingRetirementAcks.Count; i++)
                {
                    var ack = _pendingRetirementAcks[i];
                    if (_sessions.TryGetValue(ack.SessionId, out var s))
                    {
                        try { s.Dispose(); } catch { /* best-effort */ }
                        _sessions.Remove(ack.SessionId);
                    }
                }
                _pendingRetirementAcks.Clear();
            }
        }

        /// <summary>烘干统一清预测层：基于渲染线程当前最新的 _sessions 状态重算
        /// 「是否存在任何正在写（Active）的笔」快照，UI 线程只读这个 volatile 结果。</summary>
        private void UpdateActiveWetInkSnapshot()
        {
            if (_disposed || !_deviceReady)
            {
                _hasActiveWetInkSnapshot = false;
                return;
            }
            // _sessions 只在渲染线程增删改，这里遍历没有竞争。
            foreach (var s in _sessions.Values)
            {
                if (s != null && s.IsActive)
                {
                    _hasActiveWetInkSnapshot = true;
                    return;
                }
            }
            _hasActiveWetInkSnapshot = false;
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
            // 烘干统一清预测层：不直接调 D2D（会和渲染线程竞争），只设 flag。
            // 渲染线程在下次 Apply 时检查此 flag 并执行 forceClear Present。
            _pendingIdleClear = true;
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
                    // 方案C：标记该 session 已抬笔（后续的 StillDrawing 要同步等待 Present，
                    // 否则裁剪尾帧被 GPU 丢弃 → 屏幕残留预测尾 → 烘干重影。
                    if (_sessions.TryGetValue(command.SessionId, out var endRes))
                    {
                        endRes.MarkEnded();
                        _needsPresent = true;
                    }
                    return false;
                case WetInkBoundaryCommandKind.CancelStroke:
                    return RemoveSession(command.SessionId);
                case WetInkBoundaryCommandKind.RetireStroke:
                    // 方案B：两阶段退休，避免湿墨空帧闪烘：
                    //   阶段1（本次）：标记 PendingRetire，几何仍然保留在 _sessions 里继续参与绘制
                    //                 → Present 结束后再真正 Dispose/Remove
                    //   阶段2（下次 ProcessCommands 之前）：CollectPendingRetirements 真正删除，
                    //                 并写回 RetirementAck（延迟一帧 ack，保证渲染线程已带着湿墨
                    //                 画过了最后一帧）。
                    if (_sessions.TryGetValue(command.SessionId, out var res)
                        && !res.IsPendingRetire)
                    {
                        res.MarkPendingRetire();
                        _pendingRetirementCommands.Add(
                            new WetInkRetirementAck(command.SessionId, command.Version));
                        _needsPresent = true;

                        // 烘干统一清预测层（借鉴 WinRT InkSynchronizer 自动清湿墨的思路）：
                        // RetireStroke 到达时，干墨已经通过 WPF 渲染栅栏确认上屏，
                        // 此时如果没有其他正在写的笔，直接在同一帧设置 _pendingIdleClear。
                        // Apply 末尾会 forceClear 把 SwapChain 表面整体清透明，
                        // 不再依赖 ack→UI线程→ForceClearIdle 的长链条（7-8帧延迟，易断）。
                        var hasOtherActive = false;
                        foreach (var pair in _sessions)
                        {
                            if (pair.Key != command.SessionId
                                && pair.Value != null
                                && pair.Value.IsActive)
                            {
                                hasOtherActive = true;
                                break;
                            }
                        }
                        if (!hasOtherActive)
                            _pendingIdleClear = true;

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
            // 方案C：抬笔/待退休/裁剪尾帧必须保证上屏，不能被 StillDrawing 静默丢弃；
            // 仍在书写中的帧可以丢（DoNotWait 追求低延迟）。
            // 烘干统一清预测层：forceClear=true 时必须 reliable——这是清 SwapChain 残留
            // 预测像素的帧，丢了旧像素就留着了（识别图形后湿墨不清的根因）。
            var needsReliablePresent = forceClear || _sessions.Values.Any(s => s.NeedsReliablePresent);
            PresentWithoutThrow(needsReliablePresent);
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

        private void PresentWithoutThrow(bool reliable = false)
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
                    // 方案C：抬笔裁剪尾帧 / 退休帧 / 任何已标记 End/PendingRetire 的帧
                    // 不能丢——丢了就会出现"预测尾残留在 SwapChain 上"的现象，看起来像烘干重影。
                    // 回退为 Present(1, None)：同步等 1 个 vsync，GPU 保证上屏。
                    // 落笔过程中仍然直接跳过（保持 DoNotWait 的低延迟）。
                    if (reliable)
                    {
                        try
                        {
                            var waitHr = _swapChain.Present(1, PresentFlags.None);
                            if (waitHr.Failure)
                            {
                                var waitCode = (uint)waitHr.Code;
                                if (IsDeviceLostResult(waitCode))
                                    _deviceReady = false;
                            }
                        }
                        catch
                        {
                            // best-effort：vsync 同步失败也不抛异常
                        }
                    }
                    // 记录频次与耗时供诊断。
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
            // 方案B：待退休标记。RetireStroke 收到指令时先 true（保留几何多画一帧），
            // 下一帧再真正 Dispose + Remove，避免"湿墨空帧 + WPF 干墨还没合成"造成的烘干闪白。
            private bool _pendingRetire;
            // 方案C：抬笔标记（EndStroke 发生过）→ StillDrawing 时不能丢裁剪尾帧
            private bool _ended;

            public SessionResources(WetInkGeometryBuilder builder)
            {
                _geometryState = new WetInkStrokeGeometryState(builder);
            }

        /// <summary>方案C：是否已经 EndStroke → Present 必须保底成功</summary>
            public bool NeedsReliablePresent => _ended || _pendingRetire;

            /// <summary>方案B：是否已标记待退休，下一帧结束真正删除</summary>
            public bool IsPendingRetire => _pendingRetire;

            /// <summary>烘干统一清预测层：是否仍处于"正在写"的活跃状态（没 End 也没待退休）。
            /// 如果所有 session 都 Ended/PendingRetire，说明用户已经抬笔/正在烘干，
            /// 此时 SwapChain 表面残留的预测像素都可以被清零，而不会误擦正在写的真实湿墨。</summary>
            public bool IsActive => !_ended && !_pendingRetire;

            public void MarkEnded() => _ended = true;

            public void MarkPendingRetire() => _pendingRetire = true;

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
