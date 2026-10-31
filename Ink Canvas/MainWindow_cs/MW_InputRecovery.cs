using System;
using System.Windows;
using System.Windows.Input;

namespace Ink_Canvas
{
    public partial class MainWindow
    {
        private Helpers.UiResponsivenessMonitor _uiResponsivenessMonitor;
        private void InitializeCanvasInputRecovery()
        {
            inkCanvas.AddHandler(UIElement.LostMouseCaptureEvent, new MouseEventHandler(CanvasLostMouseCapture), true);
            inkCanvas.AddHandler(UIElement.LostStylusCaptureEvent, new StylusEventHandler(CanvasLostStylusCapture), true);
            inkCanvas.AddHandler(UIElement.LostTouchCaptureEvent, new EventHandler<TouchEventArgs>(CanvasLostTouchCapture), true);
        }

        private void CancelAbandonedMouseInk()
        {
            if (_isMouseRealtimeInking) CancelPalmTouchPreview(MouseRealtimeStrokeId);
            _isMouseRealtimeInking = false;
            isMouseDown = false;
            CancelPauseStraightenTimer(MouseRealtimeStrokeId);
        }

        private void CanvasLostMouseCapture(object sender, MouseEventArgs e)
        {
            if (Mouse.Captured == inkCanvas) return;
            CancelAbandonedMouseInk();
        }

        private void CanvasLostStylusCapture(object sender, StylusEventArgs e)
        {
            var device = e.StylusDevice;
            if (device == null || device.Captured == inkCanvas || IsTouchStylusDevice(device)) return;
            _activeRealtimeStylusStrokeIds.Remove(device.Id);
            CancelPalmTouchPreview(device.Id);
            EndTouchInkInputIfIdle();
            _pauseStraightenInkModeTracking = false;
            CancelPauseStraightenTimer(-200001);
        }

        private void CanvasLostTouchCapture(object sender, TouchEventArgs e)
        {
            var device = e.TouchDevice;
            if (device == null || device.Captured == inkCanvas) return;
            // This contact alone lost ownership. Do not cancel other fingers or commit an
            // unfinished preview just because capture moved to another element/window.
            CancelPalmTouchPreview(device.Id);
            dec.Remove(device.Id);
        }
    }
}
