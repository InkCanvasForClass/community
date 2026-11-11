using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.DirectManipulation;
using Windows.Win32.UI.WindowsAndMessaging;
using static Windows.Win32.Graphics.DirectManipulation.DIRECTMANIPULATION_CONFIGURATION;
using static Windows.Win32.Graphics.DirectManipulation.DIRECTMANIPULATION_STATUS;

namespace Ink_Canvas.Helpers
{
    // Only claims precision-touchpad contacts. Touchscreen/pen input stays with WPF/WinRT.
    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    internal sealed class TouchpadManipulation : IDirectManipulationViewportEventHandler, IDisposable
    {
        private readonly HwndSource _source;
        private IntPtr _inputWindow;
        private readonly Func<bool> _canStart;
        private readonly Action<Matrix> _delta;
        private readonly Action _completed;
        private IDirectManipulationManager _manager;
        private IDirectManipulationUpdateManager _updates;
        private IDirectManipulationViewport _viewport;
        private uint _cookie;
        private bool _registered;
        private bool _activated;
        private bool _claimed;
        private bool _rendering;
        private bool _running;
        private bool _disposed;
        private Matrix _previous = Matrix.Identity;
        private readonly float[] _transform = new float[6];
        private RECT _bounds;

        internal TouchpadManipulation(HwndSource source, Func<bool> canStart, Action<Matrix> delta, Action completed)
        {
            _source = source;
            _canStart = canStart;
            _delta = delta;
            _completed = completed;
            try
            {
                // WPF's WISP registration prevents Activate on its HWND (E_ACCESSDENIED).
                // A hidden HWND on the same UI thread hosts the virtual viewport; only
                // touchpad contacts explicitly passed from DM_POINTERHITTEST are assigned.
                _inputWindow = CreateWindowEx(0, "STATIC", "ICC touchpad input", 0x80000000,
                    0, 0, 1, 1, source.Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                if (_inputWindow == IntPtr.Zero) throw new System.ComponentModel.Win32Exception();
                _manager = (IDirectManipulationManager)new DirectManipulationManager();
                _manager.GetUpdateManager(out _updates);
                _manager.CreateViewport(null, (HWND)_inputWindow, out _viewport);
                _viewport.ActivateConfiguration(DIRECTMANIPULATION_CONFIGURATION_INTERACTION
                    | DIRECTMANIPULATION_CONFIGURATION_TRANSLATION_X | DIRECTMANIPULATION_CONFIGURATION_TRANSLATION_Y
                    | DIRECTMANIPULATION_CONFIGURATION_SCALING | DIRECTMANIPULATION_CONFIGURATION_TRANSLATION_INERTIA);
                _viewport.SetViewportOptions(DIRECTMANIPULATION_VIEWPORT_OPTIONS.DIRECTMANIPULATION_VIEWPORT_OPTIONS_MANUALUPDATE);
                Resize();
                _viewport.AddEventHandler((HWND)source.Handle, this, out _cookie);
                _registered = true;
                _manager.Activate((HWND)_inputWindow);
                _activated = true;
                _viewport.Enable();
                source.AddHook(WndProc);
                LogHelper.WriteLogToFile("[Touchpad] 原生触控板视口已初始化", LogHelper.LogType.Trace);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message != PInvoke.DM_POINTERHITTEST || !_canStart()) return IntPtr.Zero;
            var id = (uint)(wParam.ToInt64() & 0xffff);
            if (!PInvoke.GetPointerType(id, out var type) || type != POINTER_INPUT_TYPE.PT_TOUCHPAD)
                return IntPtr.Zero;
            try
            {
                Resize();
                _claimed = true;
                _viewport.SetContact(id);
                LogHelper.WriteLogToFile("[Touchpad] 已接管精确式触控板手势", LogHelper.LogType.Trace);
                // Status notifications are asynchronous; they start frame updates when RUNNING arrives.
                handled = true;
            }
            catch (COMException ex)
            {
                _claimed = false;
                LogHelper.WriteLogToFile($"[Touchpad] SetContact failed: {ex.Message}", LogHelper.LogType.Warning);
            }
            return IntPtr.Zero;
        }

        private void Resize()
        {
            PInvoke.GetClientRect((HWND)_source.Handle, out var bounds);
            if (bounds.right <= 0 || bounds.bottom <= 0) return;
            if (bounds.right == _bounds.right && bounds.bottom == _bounds.bottom) return;
            if (_bounds.right > 0) Stop();
            _bounds = bounds;
            _viewport.SetViewportRect(bounds);
        }

        private void Render(object sender, EventArgs e)
        {
            if (_disposed || !_claimed) return;
            try { _updates.Update(null); }
            catch (COMException ex)
            {
                LogHelper.WriteLogToFile($"[Touchpad] Update failed: {ex.Message}", LogHelper.LogType.Warning);
                Stop();
            }
        }

        void IDirectManipulationViewportEventHandler.OnViewportStatusChanged(
            IDirectManipulationViewport viewport, DIRECTMANIPULATION_STATUS current, DIRECTMANIPULATION_STATUS previous)
        {
            if (_disposed || !_claimed) return;
            if (current == DIRECTMANIPULATION_RUNNING || current == DIRECTMANIPULATION_INERTIA)
            {
                _running = true;
                if (!_rendering)
                {
                    _rendering = true;
                    CompositionTarget.Rendering += Render;
                }
            }
            else if (current == DIRECTMANIPULATION_READY && _running)
            {
                bool changed = !_previous.IsIdentity;
                Finish();
                // Each gesture starts at identity, avoiding the native viewport's cumulative zoom limits.
                if (changed) viewport.ZoomToRect(0, 0, _bounds.right, _bounds.bottom, false);
            }
        }

        void IDirectManipulationViewportEventHandler.OnViewportUpdated(IDirectManipulationViewport viewport) { }

        void IDirectManipulationViewportEventHandler.OnContentUpdated(
            IDirectManipulationViewport viewport, IDirectManipulationContent content)
        {
            if (_disposed || !_running) return;
            content.GetContentTransform(_transform);
            var current = new Matrix(_transform[0], _transform[1], _transform[2], _transform[3], _transform[4], _transform[5]);
            var delta = GetDelta(_previous, current);
            _previous = current;
            if (delta.IsIdentity) return;
            var fromDevice = _source.CompositionTarget.TransformFromDevice;
            var toDevice = _source.CompositionTarget.TransformToDevice;
            _delta(toDevice * delta * fromDevice);
        }

        internal static Matrix GetDelta(Matrix previous, Matrix current)
        {
            if (!previous.HasInverse || !double.IsFinite(current.M11) || current.M11 <= 0
                || !double.IsFinite(current.OffsetX) || !double.IsFinite(current.OffsetY)) return Matrix.Identity;
            previous.Invert();
            return previous * current;
        }

        internal void Stop()
        {
            if (_disposed || _viewport == null || !_claimed) return;
            // Finish first so queued callbacks cannot apply a late delta to a different page/tool.
            Finish();
            try
            {
                _viewport.Stop();
                _viewport.ReleaseAllContacts();
                if (_bounds.right > 0 && _bounds.bottom > 0)
                    _viewport.ZoomToRect(0, 0, _bounds.right, _bounds.bottom, false);
            }
            catch (COMException ex)
            {
                LogHelper.WriteLogToFile($"[Touchpad] Stop failed: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        private void Finish()
        {
            var wasRunning = _running;
            _claimed = false;
            _running = false;
            _previous = Matrix.Identity;
            if (_rendering)
            {
                CompositionTarget.Rendering -= Render;
                _rendering = false;
            }
            if (wasRunning) _completed();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _source.RemoveHook(WndProc);
            Finish();
            try
            {
                if (_registered) _viewport.RemoveEventHandler(_cookie);
                _viewport?.Abandon();
                if (_activated) _manager.Deactivate((HWND)_inputWindow);
            }
            catch (COMException ex)
            {
                LogHelper.WriteLogToFile($"[Touchpad] Dispose failed: {ex.Message}", LogHelper.LogType.Warning);
            }
            finally
            {
                if (_viewport != null) Marshal.ReleaseComObject(_viewport);
                if (_updates != null) Marshal.ReleaseComObject(_updates);
                if (_manager != null) Marshal.ReleaseComObject(_manager);
                if (_inputWindow != IntPtr.Zero) DestroyWindow(_inputWindow);
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateWindowEx(uint extendedStyle, string className, string title, uint style,
            int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyWindow(IntPtr window);
    }
}
