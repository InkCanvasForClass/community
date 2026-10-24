using Ink_Canvas.Plugins;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Ink_Canvas
{
    public partial class MainWindow
    {
        private PresentationSourceDescriptor _annotationPresentation;
        private DispatcherTimer _annotationTimer;
        private string _annotationSnapshot;
        private bool _annotationFrozen;
        private bool _annotationCanvasHitTest;
        private Visibility _annotationCanvasVisibility;
        private Brush _annotationBackground;
        private double _annotationOpacity;
        private Visibility _annotationCover;
        private bool _annotationUndo, _annotationRedo;
        private bool IsExternalAnnotationActive => _annotationPresentation != null && !IsWhiteboardMode;

        private void AttachPresentationAnnotation(PresentationSourceDescriptor source)
        {
            if (!source.SupportsExternalAnnotation) return;
            _annotationPresentation = source;
            _annotationCanvasHitTest = inkCanvas.IsHitTestVisible;
            _annotationCanvasVisibility = inkCanvas.Visibility;
            _annotationBackground = GridTransparencyFakeBackground.Background;
            _annotationOpacity = GridTransparencyFakeBackground.Opacity;
            _annotationCover = GridBackgroundCoverHolder.Visibility;
            _annotationSnapshot = null;
            _annotationUndo = IsUndoEnabled;
            _annotationRedo = IsRedoEnabled;
            AbortAllActiveTouchInputs();
            inkCanvas.Select(new System.Windows.Ink.StrokeCollection());
            inkCanvas.ReleaseMouseCapture();
            inkCanvas.ReleaseAllTouchCaptures();
            _annotationTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(50), DispatcherPriority.Input,
                (s, e) => SyncPresentationAnnotation(), Dispatcher);
            SyncPresentationAnnotation();
            SyncWinRTInkPipelineWithLogicalTool();
        }

        private void ApplyPresentationAnnotationRouting()
        {
            if (!IsExternalAnnotationActive) return;
            DisableEraserOverlay();
            inkCanvas.EditingMode = InkCanvasEditingMode.None;
            inkCanvas.IsHitTestVisible = false;
            inkCanvas.Visibility = Visibility.Collapsed;
            GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;
            GridTransparencyFakeBackground.Background = Brushes.Transparent;
            GridTransparencyFakeBackground.Opacity = 0;
            GridBackgroundCoverHolder.Visibility = Visibility.Collapsed;
            SetTransparentHitThrough();
        }

        private void SyncPresentationAnnotation()
        {
            if (!IsExternalAnnotationActive) return;
            ApplyPresentationAnnotationRouting();
            var status = _annotationPresentation.AnnotationStatus;
            var wasFrozen = _annotationFrozen;
            var couldUndo = IsUndoEnabled;
            var couldRedo = IsRedoEnabled;
            if (status != null) _annotationFrozen = status.Frozen;
            IsUndoEnabled = status?.CanUndo == true && !_annotationFrozen;
            IsRedoEnabled = status?.CanRedo == true && !_annotationFrozen;
            if (wasFrozen != _annotationFrozen) UpdateInkFreezeButtonState();
            if (couldUndo != IsUndoEnabled || couldRedo != IsRedoEnabled)
                RaisePluginEvent(PluginUndoRedoStateChanged, IsUndoEnabled, IsRedoEnabled, nameof(PluginUndoRedoStateChanged));
            var attributes = inkCanvas.DefaultDrawingAttributes;
            var color = attributes.Color;
            var state = new PresentationAnnotationState
            {
                Tool = _currentToolMode == "select" ? "select" :
                    _currentToolMode == "eraser" ? "eraser" :
                    _currentToolMode == "eraserByStrokes" ? "stroke-eraser" :
                    _currentToolMode == "pen" || _currentToolMode == "color" ? "pen" : "play",
                Color = $"#{color.R:X2}{color.G:X2}{color.B:X2}",
                Width = attributes.Width,
                Height = attributes.Height,
                Opacity = color.A / 255.0,
                Pressure = !Settings.Canvas.DisablePressure && !attributes.IgnorePressure,
                BrushKind = penType == 2 ? "laser" : penType == 1 ? "highlighter" : "pen",
                Tip = attributes.StylusTip == System.Windows.Ink.StylusTip.Rectangle ? "rectangle" : "ellipse",
                InkStyle = Settings.Canvas.DisablePressure ? 2 : Settings.Canvas.InkStyle,
                VelocityMix = Settings.Canvas.VelocityBrushTipMix,
                MinDistanceScale = Settings.Canvas.RealtimeBrushTipMinDistanceScale,
                HighlighterOverlap = Settings.Canvas.HighlighterOverlapEnabled,
                FadeEnabled = penType == 2 && Settings.Canvas.EnableInkFade,
                FadeDelay = Settings.Canvas.InkFadeTime,
                FadeSpeed = Settings.Canvas.InkFadeSpeedMultiplier,
                Smoothing = Settings.Canvas.UseAdvancedBezierSmoothing,
                SmoothingQuality = Settings.Canvas.InkSmoothingQuality,
                EraserWidth = eraserWidth,
                EraserShape = isEraserCircleShape ? "circle" : "rectangle",
                Frozen = _annotationFrozen
            };
            var snapshot = System.Text.Json.JsonSerializer.Serialize(state);
            if (snapshot == _annotationSnapshot) return;
            try
            {
                _annotationPresentation.AnnotationStateChanged?.Invoke(state);
                _annotationSnapshot = snapshot;
            }
            catch (Exception ex) { Helpers.LogHelper.WriteLogToFile($"外部批注同步失败: {ex}"); }
            SyncWinRTInkPipelineWithLogicalTool();
        }

        private bool TryPresentationAnnotationCommand(string command)
        {
            if (!IsExternalAnnotationActive) return false;
            if (!_annotationFrozen) _annotationPresentation.AnnotationCommand?.Invoke(command);
            return true;
        }

        private void DetachPresentationAnnotation()
        {
            if (_annotationPresentation == null) return;
            _annotationTimer?.Stop();
            _annotationTimer = null;
            var wasOnBoard = IsWhiteboardMode;
            _annotationPresentation = null;
            _annotationSnapshot = null;
            _annotationFrozen = false;
            if (wasOnBoard) return;
            SetTransparentNotHitThrough();
            inkCanvas.IsHitTestVisible = _annotationCanvasHitTest;
            inkCanvas.Visibility = _annotationCanvasVisibility;
            GridTransparencyFakeBackground.Background = _annotationBackground;
            GridTransparencyFakeBackground.Opacity = _annotationOpacity;
            GridBackgroundCoverHolder.Visibility = _annotationCover;
            IsUndoEnabled = _annotationUndo;
            IsRedoEnabled = _annotationRedo;
            UpdateCurrentToolMode("cursor");
            SetCurrentToolMode(InkCanvasEditingMode.None);
            ApplyCursorInputRouting();
        }
    }
}
