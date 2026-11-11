using Ink_Canvas.Helpers;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Ink_Canvas
{
    public partial class MainWindow
    {
        private TouchpadManipulation _boardTouchpad;
        private HwndSource _boardTouchpadSource;
        private bool _isBoardTouchpadGesture;
        private DispatcherTimer _boardTouchpadWheelTimer;

        private void InitializeBoardTouchpadInput()
        {
            SourceInitialized += (s, e) =>
            {
                _boardTouchpadSource = (HwndSource)PresentationSource.FromVisual(this);
                _boardTouchpadSource.AddHook(BoardTouchpadWndProc);
                try
                {
                    _boardTouchpad = new TouchpadManipulation(_boardTouchpadSource,
                        () => currentMode == 1 && IsVisible && !_isVideoPresenterSpecialMode
                            && IsBoardCanvasPoint(Mouse.GetPosition(this)),
                        GetBoardTouchpadGestureOptions, ApplyNativeBoardTouchpadDelta, EndBoardTouchpadGesture);
                }
                catch (Exception ex) when (ex is COMException || ex is UnauthorizedAccessException
                    || ex is System.ComponentModel.Win32Exception)
                {
                    LogHelper.WriteLogToFile($"[Touchpad] Native gestures unavailable; using wheel input: {ex.Message}",
                        LogHelper.LogType.Warning);
                }
            };
            // Commit before another input changes ink, history, tool, or page.
            PreviewMouseDown += (s, e) => EndBoardTouchpadGesture();
            PreviewStylusDown += (s, e) => EndBoardTouchpadGesture();
            PreviewTouchDown += (s, e) => EndBoardTouchpadGesture();
            PreviewKeyDown += (s, e) => EndBoardTouchpadGesture();
            Deactivated += (s, e) => EndBoardTouchpadGesture();
            SizeChanged += (s, e) => EndBoardTouchpadGesture();
            Closed += (s, e) =>
            {
                EndBoardTouchpadGesture();
                _boardTouchpad?.Dispose();
                _boardTouchpad = null;
                _boardTouchpadSource?.RemoveHook(BoardTouchpadWndProc);
            };
        }

        private bool IsBoardCanvasPoint(Point point)
        {
            var hit = InputHitTest(point) as DependencyObject;
            while (hit is Visual)
            {
                if (ReferenceEquals(hit, inkCanvas) || ReferenceEquals(hit, eraserOverlayCanvas)
                    || ReferenceEquals(hit, GridInkCanvasSelectionCover)) return true;
                hit = VisualTreeHelper.GetParent(hit);
            }
            return false;
        }

        private bool HandleOverlayTouchpadMessage(uint message, IntPtr wParam, IntPtr lParam)
        {
            if (currentMode != 1 || _isVideoPresenterSpecialMode || _boardTouchpadSource == null) return false;
            bool handled = false;
            if (message == 0x0250)
                _boardTouchpad?.WndProc(_boardTouchpadSource.Handle, (int)message, wParam, lParam, ref handled);
            else
                BoardTouchpadWndProc(_boardTouchpadSource.Handle, (int)message, wParam, lParam, ref handled);
            return handled;
        }

        private IntPtr BoardTouchpadWndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int wmMouseWheel = 0x020a, wmMouseHWheel = 0x020e;
            if (currentMode != 1 || _isVideoPresenterSpecialMode
                || (message != wmMouseWheel && message != wmMouseHWheel)) return IntPtr.Zero;
            var packed = lParam.ToInt64();
            var screenPoint = new Point(unchecked((short)packed), unchecked((short)(packed >> 16)));
            if (!IsBoardCanvasPoint(PointFromScreen(screenPoint))) return IntPtr.Zero;
            // Pinch compatibility messages carry MK_CONTROL; Keyboard.Modifiers may not reflect it.
            int delta = unchecked((short)(wParam.ToInt64() >> 16));
            bool control = (wParam.ToInt64() & 0x0008) != 0;
            handled = HandleBoardTouchpadWheel(delta, message == wmMouseHWheel, control, inkCanvas.PointFromScreen(screenPoint));
            return IntPtr.Zero;
        }

        private (bool Translate, bool Zoom) GetBoardTouchpadGestureOptions() =>
            (IsBoardRoamingMode || Settings.Gesture.IsEnableTwoFingerTranslateBoard,
                IsBoardRoamingMode ? Settings.Gesture.IsEnableTwoFingerZoomRoaming : Settings.Gesture.IsEnableTwoFingerZoomBoard);

        private bool HandleBoardTouchpadWheel(int delta, bool horizontal, bool control, Point center)
        {
            if (currentMode != 1 || _isVideoPresenterSpecialMode) return false;
            if (delta == 0 || IsCurrentPageFrozen) return true;
            var options = GetBoardTouchpadGestureOptions();
            if (control ? !options.Zoom : !options.Translate) return true;
            var matrix = Matrix.Identity;
            if (control)
            {
                var scale = Math.Pow(1.1, Math.Clamp(delta / 120.0, -4, 4));
                matrix.ScaleAt(scale, scale, center.X, center.Y);
            }
            else
            {
                var lines = SystemParameters.WheelScrollLines;
                var distance = delta / 120.0 * (lines < 0 ? inkCanvas.ActualHeight : lines * 16.0);
                matrix.Translate(horizontal ? -distance : 0, horizontal ? 0 : distance);
            }
            ApplyBoardTouchpadDelta(matrix);
            if (_isBoardTouchpadGesture)
            {
                _boardTouchpadWheelTimer ??= new DispatcherTimer(DispatcherPriority.Input)
                {
                    Interval = TimeSpan.FromMilliseconds(250)
                };
                _boardTouchpadWheelTimer.Stop();
                _boardTouchpadWheelTimer.Tick -= BoardTouchpadWheelCompleted;
                _boardTouchpadWheelTimer.Tick += BoardTouchpadWheelCompleted;
                _boardTouchpadWheelTimer.Start();
            }
            return true;
        }

        private void BoardTouchpadWheelCompleted(object sender, EventArgs e) => EndBoardTouchpadGesture();

        private void ApplyNativeBoardTouchpadDelta(Matrix delta)
        {
            // Native deltas use window DIPs. Conjugate into the canvas coordinate system.
            var transform = inkCanvas.TransformToAncestor(this);
            var origin = transform.Transform(new Point());
            var x = transform.Transform(new Point(1, 0)) - origin;
            var y = transform.Transform(new Point(0, 1)) - origin;
            var toWindow = new Matrix(x.X, x.Y, y.X, y.Y, origin.X, origin.Y);
            if (!toWindow.HasInverse) return;
            var fromWindow = toWindow;
            fromWindow.Invert();
            ApplyBoardTouchpadDelta(toWindow * delta * fromWindow);
        }

        private void ApplyBoardTouchpadDelta(Matrix delta)
        {
            if (currentMode != 1 || IsCurrentPageFrozen || delta.IsIdentity || !IsFinite(delta)
                || delta.M11 <= 0 || delta.M22 <= 0 || _isBoardRoamingPointerDown
                || _boardRoamingContacts.Count > 0 || dec.Count > 0
                || Mouse.LeftButton == MouseButtonState.Pressed || _winRTInkTwoFingerGestureActive) return;
            if (!_isBoardTouchpadGesture)
            {
                if (_isBoardRoamingTwoFingerGesture) return;
                BeginBoardRoamingTwoFingerGesture();
                _isBoardTouchpadGesture = true;
            }
            TransformBoardRoamingContent(delta, new Vector(delta.OffsetX, delta.OffsetY), delta.M11);
            RefreshBoardRoamingPopup(delta.M11 != 1);
        }

        private void EndBoardTouchpadGesture()
        {
            _boardTouchpad?.Stop();
            if (!_isBoardTouchpadGesture) return;
            EndBoardRoamingTwoFingerGesture();
        }

        private void FinishBoardTouchpadInput()
        {
            if (!_isBoardTouchpadGesture) return;
            _isBoardTouchpadGesture = false;
            _boardTouchpadWheelTimer?.Stop();
            _boardTouchpad?.Stop();
        }
    }
}
