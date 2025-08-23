using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Resources;

namespace Ink_Canvas
{
    public partial class MainWindow : PerformanceTransparentWin {

        private bool isMouseGesturing = false;
        private Point startPoint;

        public void InkCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e) {
            if (e.StylusDevice != null) return;
            isMouseGesturing = true;
            MouseRightButtonGestureTipPopup.Visibility = Visibility.Visible;
            startPoint = e.GetPosition(inkCanvas);
            inkCanvas.CaptureMouse();
            inkCanvas.ForceCursor = true;
            StreamResourceInfo sri = Application.GetResourceStream(
                new Uri("Resources/Cursors/close-hand-cursor.cur", UriKind.Relative));
            inkCanvas.Cursor = new Cursor(sri.Stream);
        }

        public void InkCanvas_MouseRightButtonUp(object sender, MouseButtonEventArgs e) {
            if (e.StylusDevice != null) return;
            isMouseGesturing = false;
            inkCanvas.ReleaseMouseCapture();
            inkCanvas.ForceCursor = false;
            MouseRightButtonGestureTipPopup.Visibility = Visibility.Collapsed;
        }

        private void InkCanvas_MouseGesture_MouseMove(object sender, MouseEventArgs e) {
            if (!isMouseGesturing || e.RightButton != MouseButtonState.Pressed || e.StylusDevice != null) return;
            Trace.WriteLine(e.StylusDevice == null);
            Point currentPoint = e.GetPosition(inkCanvas);
            System.Windows.Vector delta = currentPoint - startPoint;

            foreach (Stroke stroke in inkCanvas.Strokes) {
                stroke.Transform(new Matrix(1, 0, 0, 1, delta.X, delta.Y), false);
            }
                
            startPoint = currentPoint;
        }

    }
}
