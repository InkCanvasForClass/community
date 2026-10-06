using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace Ink_Canvas.Plugins
{
    internal sealed class PluginCanvasToolSession : ICanvasToolSession
    {
        private readonly Func<int, bool> _capturePointer;
        private readonly Action<int> _releasePointer;
        private readonly Action<PluginCanvasToolSession> _dispose;
        private readonly Dictionary<int, InputDevice> _pointerDevices = new Dictionary<int, InputDevice>();

        internal PluginCanvasToolSession(
            string pluginId,
            string toolId,
            Func<int, bool> capturePointer,
            Action<int> releasePointer,
            Action<PluginCanvasToolSession> dispose)
        {
            PluginId = pluginId;
            ToolId = toolId;
            _capturePointer = capturePointer;
            _releasePointer = releasePointer;
            _dispose = dispose;
            IsActive = true;
        }

        public string PluginId { get; }
        public string ToolId { get; }
        public bool IsActive { get; private set; }
        public event EventHandler<CanvasPointerEventArgs> Pointer;
        public event EventHandler<CanvasKeyEventArgs> KeyDown;

        public bool CapturePointer(int pointerId)
            => IsActive && _capturePointer(pointerId);

        public void ReleasePointer(int pointerId)
        {
            if (IsActive) _releasePointer(pointerId);
        }

        public void Dispose() => _dispose(this);

        internal void Publish(CanvasPointerEventArgs args) => Pointer?.Invoke(this, args);

        internal void Publish(CanvasKeyEventArgs args) => KeyDown?.Invoke(this, args);

        internal void RememberPointer(int pointerId, InputDevice device)
            => _pointerDevices[pointerId] = device;

        internal void ForgetPointer(int pointerId) => _pointerDevices.Remove(pointerId);

        internal bool CaptureRememberedPointer(int pointerId, IInputElement relativeTo)
        {
            if (pointerId == 0) return Mouse.Capture(relativeTo);
            if (!_pointerDevices.TryGetValue(pointerId, out var device)) return false;
            if (device is TouchDevice touch) return touch.Capture(relativeTo);
            if (device is StylusDevice stylus) return stylus.Capture(relativeTo);
            return false;
        }

        internal void ReleaseRememberedPointer(int pointerId)
        {
            if (pointerId == 0)
            {
                if (Mouse.Captured != null) Mouse.Capture(null);
                return;
            }

            if (!_pointerDevices.TryGetValue(pointerId, out var device)) return;
            if (device is TouchDevice touch) touch.Capture(null);
            else if (device is StylusDevice stylus) stylus.Capture(null);
        }

        internal void ReleaseAllPointers()
        {
            if (Mouse.Captured != null) Mouse.Capture(null);
            foreach (var pointerId in _pointerDevices.Keys.ToArray()) ReleaseRememberedPointer(pointerId);
            _pointerDevices.Clear();
        }

        internal void MarkInactive()
        {
            IsActive = false;
            Pointer = null;
            KeyDown = null;
        }
    }
}
