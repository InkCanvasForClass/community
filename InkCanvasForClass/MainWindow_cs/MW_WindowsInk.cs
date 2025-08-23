using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Ink_Canvas
{
    public partial class MainWindow : PerformanceTransparentWin {

        private bool _stylusInverted = false;
        private int _stylusInvertedInit = 0;

        private bool IsStylusInverted {
            get => _stylusInverted;
            set {
                if (value && !_stylusInverted) {
                    StylusInverted?.Invoke(this,new RoutedEventArgs());
                } else if (!value && _stylusInverted) {
                    StylusUnInverted?.Invoke(this,new RoutedEventArgs());
                }
                _stylusInverted = value;
            }
        }

        public event EventHandler<RoutedEventArgs> StylusInverted;
        public event EventHandler<RoutedEventArgs> StylusUnInverted;

        public void UpdateStylusPenInvertedStatus(bool isInverted) {
            if (_stylusInvertedInit == 0) {
                _stylusInverted = isInverted;
                _stylusInvertedInit = 1;
            } else {
                IsStylusInverted = isInverted;
            }
        }

        #region 托管StylusInAirMove和StylusMove事件

        public void mainWin_StylusInAirMove(object sender, StylusEventArgs e) {
            UpdateStylusPenInvertedStatus(e.Inverted);
        }

        public void mainWin_StylusMove(object sender, StylusEventArgs e) {
            UpdateStylusPenInvertedStatus(e.Inverted);
        }

        #endregion

        #region Windows Ink 橡皮按钮自定义适配

        public void StylusInvertedListenerInit() {
            StylusInverted += StylusInvertedEvent;
            StylusUnInverted += StylusUnInvertedEvent;
        }

        private void StylusInvertedEvent(object sender, RoutedEventArgs e) {
            if (Settings.Gesture.WindowsInkEraserButtonAction != 0) {
                if (SelectedMode != ICCToolsEnum.EraseByGeometryMode &&
                    SelectedMode != ICCToolsEnum.EraseByStrokeMode) {
                    GridEraserOverlay.Visibility = Visibility.Visible;
                    isUsingStrokesEraser = Settings.Gesture.WindowsInkEraserButtonAction == 1;
                } else if (SelectedMode == (Settings.Gesture.WindowsInkEraserButtonAction == 2
                               ? ICCToolsEnum.EraseByStrokeMode
                               : ICCToolsEnum.EraseByGeometryMode)) {
                    isUsingStrokesEraser = Settings.Gesture.WindowsInkEraserButtonAction == 1;
                }
                ForceUpdateToolSelection((Settings.Gesture.WindowsInkEraserButtonAction == 2
                    ? ICCToolsEnum.EraseByGeometryMode
                    : ICCToolsEnum.EraseByStrokeMode));
            }
        }

        private void StylusUnInvertedEvent(object sender, RoutedEventArgs e) {
            if (Settings.Gesture.WindowsInkEraserButtonAction != 0) {
                if (SelectedMode != ICCToolsEnum.EraseByGeometryMode &&
                    SelectedMode != ICCToolsEnum.EraseByStrokeMode) {
                    GridEraserOverlay.Visibility = Visibility.Collapsed;
                } else if (SelectedMode == (Settings.Gesture.WindowsInkEraserButtonAction == 2
                               ? ICCToolsEnum.EraseByStrokeMode
                               : ICCToolsEnum.EraseByGeometryMode)) {
                    isUsingStrokesEraser = Settings.Gesture.WindowsInkEraserButtonAction == 2;
                }
            }
            ForceUpdateToolSelection(null);
        }

        #endregion

        #region Windows Ink 筒形按钮自定义适配

        private void mainWin_StylusButtonUp(object sender, StylusButtonEventArgs e) {
            if (e.StylusButton.Guid == StylusPointProperties.BarrelButton.Id) {
                if (Settings.Gesture.WindowsInkBarrelButtonAction == 0) return;
                if (Settings.Gesture.WindowsInkBarrelButtonAction == 1) SelectIcon_MouseUp(null,null);
                else if (Settings.Gesture.WindowsInkBarrelButtonAction == 2) {
                    SymbolIconSelect_MouseUp(null,null);
                    inkCanvas.Select(inkCanvas.Strokes);
                }
                else if (Settings.Gesture.WindowsInkBarrelButtonAction == 3) SymbolIconUndo_MouseUp(null,null);
            }
        }

        private void mainWin_StylusButtonDown(object sender, StylusButtonEventArgs e) {
            if (e.StylusButton.Guid == StylusPointProperties.BarrelButton.Id) {
                
            }
        }

        #endregion
    }
}
