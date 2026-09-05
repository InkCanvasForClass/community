using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Windows.UI.Input.Inking;
using Windows.UI.Input.Inking.Core;
using Windows.UI.Core;
using global::Windows.Foundation;

namespace Ink_Canvas.Ink.WinRT
{
    /// <summary>
    /// Owns the OS InkPresenter (created and serviced on the IInkDesktopHost ink thread),
    /// the CoreInkIndependentInputSource pre-input gate, the InkSynchronizer custom-drying
    /// handshake, and the DirectComposition commit-request handler.
    ///
    /// All InkPresenter access is marshaled onto the ink thread through IInkDesktopHost
    /// work items (the presenter is thread-affine to that thread, per the WinUI3 InkCanvas
    /// pattern). StrokeEnded fires on the ink thread; StrokeCollected/StrokeEnded completion is
    /// represented by StrokesCollected, where BeginDry + point snapshot happen there, then the
    /// agile InkPoint snapshot is marshaled to the UI thread where the MainWindow materializes
    /// the WPF Stroke. EndDry is deferred until the WPF stroke is committed.
    /// </summary>
    internal sealed class WinRTInkHost : IDisposable
    {
        private readonly object _startSync = new object();
        private IInkDesktopHost _host;
        private InkPresenter _presenter;
        private IInkPresenterDesktop _presenterDesktop;
        private InkSynchronizer _inkSynchronizer;
        private CoreInkIndependentInputSource _independentInput;
        private readonly object _inkThreadIdSync = new object();
        private int _inkThreadId;

        private readonly WetInkOverlayWindow _overlay;
        private InkCommitRequestHandler _commitHandler;
        private readonly WinRTInkInputGate _inputGate;

        // Keep event handler delegates alive (GC root).
        private TypedEventHandler<CoreInkIndependentInputSource, PointerEventArgs> _onPointerPressing;
        private TypedEventHandler<CoreInkIndependentInputSource, PointerEventArgs> _onPointerMoving;
        private TypedEventHandler<CoreInkIndependentInputSource, PointerEventArgs> _onPointerReleasing;
        private TypedEventHandler<InkPresenter, InkStrokesCollectedEventArgs> _onStrokesCollected;

        private bool _disposed;

        public WinRTInkHost(
            WetInkOverlayWindow overlay,
            WinRTInkInputGate inputGate)
        {
            _overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
            _inputGate = inputGate ?? throw new ArgumentNullException(nameof(inputGate));
        }

        public InkPresenter Presenter => _presenter;

        public bool IsInkThread
        {
            get
            {
                int id;
                lock (_inkThreadIdSync) { id = _inkThreadId; }
                return id != 0 && id == Environment.CurrentManagedThreadId;
            }
        }

        /// <summary>
        /// CoCreates the host and queues the OS presenter creation on the ink thread.
        /// Blocks until the presenter is ready (or fails). UI thread only.
        /// </summary>
        public void Start(IntPtr ownerHwnd, WinRTInkConfig config)
        {
            if (ownerHwnd == IntPtr.Zero)
                throw new ArgumentOutOfRangeException(nameof(ownerHwnd));
            if (config == null)
                throw new ArgumentNullException(nameof(config));
            EnsureNotDisposed();

            lock (_startSync)
            {
                if (_presenter != null)
                    return;

                _overlay.EnsureCreated();
                _commitHandler = new InkCommitRequestHandler(_overlay.CompositionDevice);
                _host = InkDesktopHostInterop.CreateHost();

                using (var ready = new ManualResetEventSlim(false))
                {
                    Exception threadError = null;
                    QueueWorkItemCore(() =>
                    {
                        try
                        {
                            InitializeOnInkThread(config);
                        }
                        catch (Exception ex)
                        {
                            threadError = ex;
                        }
                        finally
                        {
                            ready.Set();
                        }
                    });

                    if (!ready.Wait(TimeSpan.FromSeconds(5)))
                        throw new TimeoutException("WinRT ink presenter failed to initialize on the ink thread.");
                    if (threadError != null)
                        throw new InvalidOperationException(
                            "WinRT ink presenter failed to initialize.",
                            threadError);
                }
            }
        }

        private void InitializeOnInkThread(WinRTInkConfig config)
        {
            lock (_inkThreadIdSync) { _inkThreadId = Environment.CurrentManagedThreadId; }

            var riid = InkDesktopHostInterop.IidInkPresenterDesktop;
            var hr = _host.CreateInkPresenter(ref riid, out var presenterDesktopObject);
            hr.ThrowIfFailed();
            var presenterDesktop = (IInkPresenterDesktop)presenterDesktopObject;
            // Wrap the raw IUnknown into the projected InkPresenter (CsWinRT canonical path:
            // MarshalInspectable<T>.FromAbi creates a projected RCW from the ABI pointer;
            // release the temporary GetIUnknownForObject reference after wrapping). The
            // projected type exposes the WinRT surface (attributes, custom drying, StrokeInput);
            // IInkPresenterDesktop stays for desktop-only calls.
            var presenterIUnknown = Marshal.GetIUnknownForObject(presenterDesktopObject);
            try
            {
                _presenter = global::WinRT.MarshalInspectable<InkPresenter>.FromAbi(presenterIUnknown);
            }
            finally
            {
                Marshal.Release(presenterIUnknown);
            }
            _presenterDesktop = presenterDesktop;
            // Supply the IDCompositionDevice3 required for custom drying; the commit handler below
            // commits this same device when the presenter requests a wet-ink composition update.
            presenterDesktop.SetRootVisual(
                _overlay.RootVisual,
                _overlay.CompositionDevice3).ThrowIfFailed();

            _presenter.InputDeviceTypes = CoreInputDeviceTypes.Mouse | CoreInputDeviceTypes.Pen | CoreInputDeviceTypes.Touch;
            _presenter.InputProcessingConfiguration.Mode = InkInputProcessingMode.Inking;
            _presenter.InputProcessingConfiguration.RightDragAction = InkInputRightDragAction.LeaveUnprocessed;
            _presenter.UpdateDefaultDrawingAttributes(config.ToInkDrawingAttributes());

            _inkSynchronizer = _presenter.ActivateCustomDrying();
            presenterDesktop.SetCommitRequestHandler(_commitHandler).ThrowIfFailed();

            _independentInput = CoreInkIndependentInputSource.Create(_presenter);
            _onPointerPressing = (s, e) => _inputGate.OnPointerPressing(s, e);
            _onPointerMoving = (s, e) => _inputGate.OnPointerMoving(s, e);
            _onPointerReleasing = (s, e) => _inputGate.OnPointerReleasing(s, e);
            _independentInput.PointerPressing += _onPointerPressing;
            _independentInput.PointerMoving += _onPointerMoving;
            _independentInput.PointerReleasing += _onPointerReleasing;

            _onStrokesCollected = (s, e) =>
            {
                // Stroke collection is finalized before this event. BeginDry must run on the
                // presenter thread and must happen before EndDry; InkStroke itself is agile, so
                // snapshot only the points before returning to the UI thread.
                try
                {
                    var strokes = BeginDry();
                    var snapshot = SnapshotStrokes(strokes);
                    _overlay.InvokeOnUiThread(() =>
                    {
                        try { OnDryAvailable?.Invoke(snapshot); }
                        catch (Exception ex)
                        {
                            try { OnDryFailed?.Invoke(ex); } catch { /* best-effort */ }
                        }
                    });
                }
                catch (Exception ex)
                {
                    // BeginDry may have succeeded while point snapshot failed. This handler is
                    // still on the ink thread, so end the batch synchronously before reporting
                    // the failure to the UI.
                    try { _inkSynchronizer?.EndDry(); } catch { /* best-effort */ }
                    _overlay.InvokeOnUiThread(() =>
                    {
                        try { OnDryFailed?.Invoke(ex); } catch { /* best-effort */ }
                    });
                }
            };
            _presenter.StrokesCollected += _onStrokesCollected;

            presenterDesktop.SetSize(
                Math.Max(1, config.WidthPx),
                Math.Max(1, config.HeightPx)).ThrowIfFailed();

            // Commit the root visual + initial presenter size so the first wet stroke has a target.
            _overlay.CompositionDevice?.Commit();
        }

        /// <summary>
        /// Queue a fire-and-forget work item on the ink thread. The callback receives the
        /// OS presenter once it exists; null-safe before/after lifecycle.
        /// </summary>
        public void RunOnInkThread(Action<InkPresenter> action)
        {
            EnsureNotDisposed();
            if (_host == null)
                return;
            var presenter = _presenter;
            if (presenter == null)
                return;

            QueueWorkItemCore(() =>
            {
                // Re-read inside the ink thread: Start may have completed by now.
                var local = _presenter;
                if (local != null)
                    action(local);
            });
        }

        public void UpdateDrawingAttributes(InkDrawingAttributes attributes)
        {
            if (attributes == null)
                throw new ArgumentNullException(nameof(attributes));
            RunOnInkThread(p => p.UpdateDefaultDrawingAttributes(attributes));
        }

        public void SetSize(float widthPx, float heightPx)
        {
            var w = Math.Max(1f, widthPx);
            var h = Math.Max(1f, heightPx);
            RunOnInkThread(p =>
            {
                _presenterDesktop?.SetSize(w, h).ThrowIfFailed();
            });
        }

        public void SetInputEnabled(bool enabled)
        {
            RunOnInkThread(p => p.IsInputEnabled = enabled);
        }

        /// <summary>
        /// Synchronously dry a batch of wet strokes on the ink thread. Must only be called
        /// from the ink thread (StrokeEnded handler / BeginDry callback). Returns null when
        /// not on the ink thread (defensive).
        /// </summary>
        public System.Collections.Generic.IReadOnlyList<InkStroke> BeginDry()
        {
            if (!IsInkThread || _inkSynchronizer == null)
                return null;
            return _inkSynchronizer.BeginDry();
        }

        /// <summary>
        /// Queue EndDry on the ink thread. Must be called exactly once per BeginDry, even
        /// when the dry batch was empty or discarded (frozen page).
        /// </summary>
        public void QueueEndDry(Action completed = null)
        {
            if (_host == null || _presenter == null)
            {
                completed?.Invoke();
                return;
            }

            RunOnInkThread(_ =>
            {
                try
                {
                    _inkSynchronizer?.EndDry();
                }
                finally
                {
                    completed?.Invoke();
                }
            });
        }

        /// <summary>Cancel all currently wet strokes by disengaging the input gate (subsequent
        /// moving/releasing events are marked Handled). The presenter drops them.</summary>
        public void CancelActiveStrokes()
        {
            _inputGate.CancelActiveStrokes();
        }

        /// <summary>
        /// Raised on the UI thread after BeginDry on the ink thread, with the agile InkPoint
        /// snapshot for the strokes that just went dry. Exactly one BeginDry is in flight at a
        /// time (the ink presenter does not raise a new StrokeEnded until EndDry).
        /// </summary>
        public Action<IReadOnlyList<IReadOnlyList<InkPoint>>> OnDryAvailable { get; set; }

        /// <summary>Raised on the UI thread when BeginDry fails; the app should fall back.</summary>
        public Action<Exception> OnDryFailed { get; set; }

        private static IReadOnlyList<IReadOnlyList<InkPoint>> SnapshotStrokes(
            IReadOnlyList<InkStroke> strokes)
        {
            if (strokes == null || strokes.Count == 0)
                return Array.Empty<IReadOnlyList<InkPoint>>();

            var list = new List<IReadOnlyList<InkPoint>>(strokes.Count);
            foreach (var stroke in strokes)
            {
                if (stroke == null)
                    continue;
                // InkStroke is agile; GetInkPoints is safe from the ink thread. Capture into a
                // plain array so the UI thread never touches the thread-affine stroke object.
                var pts = stroke.GetInkPoints();
                var copy = new InkPoint[pts.Count];
                for (var i = 0; i < pts.Count; i++)
                    copy[i] = pts[i];
                list.Add(copy);
            }
            return list;
        }

        private void QueueWorkItemCore(Action action)
        {
            var item = new ManagedInkWorkItem(() =>
            {
                try { action(); }
                catch (Exception ex)
                {
                    return Marshal.GetHRForException(ex);
                }
                return 0;
            });
            _host.QueueWorkItem(item).ThrowIfFailed();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            lock (_startSync)
            {
                if (_independentInput != null)
                {
                    try
                    {
                        _independentInput.PointerPressing -= _onPointerPressing;
                        _independentInput.PointerMoving -= _onPointerMoving;
                        _independentInput.PointerReleasing -= _onPointerReleasing;
                    }
                    catch { }
                    _independentInput = null;
                }

                if (_presenter != null)
                {
                    try { _presenter.StrokesCollected -= _onStrokesCollected; }
                    catch { }
                    _presenter = null;
                }

                _presenterDesktop = null;
                _onPointerPressing = null;
                _onPointerMoving = null;
                _onPointerReleasing = null;
                _onStrokesCollected = null;
                _inkSynchronizer = null;
                _commitHandler = null;

                if (_host != null)
                {
                    try { Marshal.FinalReleaseComObject(_host); }
                    catch { }
                    _host = null;
                }
            }
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(WinRTInkHost));
        }
    }

    internal static class HResultExtensions
    {
        public static void ThrowIfFailed(this int hr)
        {
            Marshal.ThrowExceptionForHR(hr);
        }
    }
}
