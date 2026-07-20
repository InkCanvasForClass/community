using Ink_Canvas.Controls;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas
{
    public partial class MainWindow
    {
        private bool _isBoardRoamingPointerDown;
        private Point _boardRoamingLastPoint;
        private Dictionary<Stroke, StylusPointCollection> _boardRoamingStrokeHistory;

        internal void ActivateBoardRoamingMode()
        {
            if (currentMode != 1) return;
            if (IsCurrentPageFrozen)
            {
                TryBlockFrozenPageMutation();
                return;
            }

            ResetTouchStates();
            CancelSingleFingerDragMode();
            drawingShapeMode = 0;
            forceEraser = false;
            forcePointEraser = false;
            GridInkCanvasSelectionCover.Visibility = Visibility.Collapsed;
            inkCanvas.Select(new StrokeCollection());

            if (!SetCurrentToolMode(InkCanvasEditingMode.None)) return;

            UpdateCurrentToolMode("roaming");
            HideSubPanels("roaming");
            UpdateBoardRoamingButtonState();
            SetCursorBasedOnEditingMode(inkCanvas);
        }

        private bool IsBoardRoamingMode
            => currentMode == 1 && string.Equals(_currentToolMode, "roaming", StringComparison.Ordinal);

        private void UpdateBoardRoamingButtonState()
        {
            if (FindView("board.roaming") is not BoardToolbarButton roamingButton) return;

            var foreground = Application.Current.TryFindResource("FloatingBarForegroundBrush") as Brush
                ?? Brushes.White;
            var accent = Application.Current.TryFindResource("FloatingBarAccentBrush") as Brush
                ?? new SolidColorBrush(Color.FromRgb(37, 99, 235));
            var isSelected = IsBoardRoamingMode;

            roamingButton.Background = isSelected ? accent : Brushes.Transparent;
            roamingButton.IconGeometryDrawing.Brush = isSelected ? Brushes.White : foreground;
            roamingButton.Foreground = isSelected ? Brushes.White : foreground;
        }

        private void BeginBoardRoaming(Point point)
        {
            if (!IsBoardRoamingMode || _isBoardRoamingPointerDown || IsCurrentPageFrozen) return;

            _isBoardRoamingPointerDown = true;
            _boardRoamingLastPoint = point;
            _boardRoamingStrokeHistory = new Dictionary<Stroke, StylusPointCollection>();
            foreach (var stroke in inkCanvas.Strokes)
                _boardRoamingStrokeHistory[stroke] = stroke.StylusPoints.Clone();

            inkCanvas.Cursor = Cursors.Hand;
        }

        private void MoveBoardRoaming(Point point)
        {
            if (!_isBoardRoamingPointerDown || !IsBoardRoamingMode) return;

            var delta = point - _boardRoamingLastPoint;
            if (delta.X == 0 && delta.Y == 0) return;

            var matrix = Matrix.Identity;
            matrix.Translate(delta.X, delta.Y);

            var previousCommitType = _currentCommitType;
            _currentCommitType = CommitReason.CodeInput;
            try
            {
                foreach (var stroke in inkCanvas.Strokes)
                    stroke.Transform(matrix, false);
                TransformCanvasImages(matrix);
            }
            finally
            {
                _currentCommitType = previousCommitType;
            }

            _boardRoamingLastPoint = point;
        }

        private void EndBoardRoaming()
        {
            if (!_isBoardRoamingPointerDown) return;

            _isBoardRoamingPointerDown = false;
            CommitBoardRoamingHistory();
            inkCanvas.Cursor = IsBoardRoamingMode ? Cursors.Hand : Cursors.Arrow;
        }

        private void CommitBoardRoamingHistory()
        {
            if (_boardRoamingStrokeHistory == null) return;

            var history = new Dictionary<Stroke, Tuple<StylusPointCollection, StylusPointCollection>>();
            foreach (var item in _boardRoamingStrokeHistory)
            {
                if (!inkCanvas.Strokes.Contains(item.Key)) continue;

                var current = item.Key.StylusPoints.Clone();
                if (!AreStylusPointsEqual(item.Value, current))
                    history[item.Key] = Tuple.Create(item.Value, current);
            }

            if (history.Count > 0)
            {
                timeMachine.CommitStrokeManipulationHistory(history);
                foreach (var item in history)
                    StrokeInitialHistory[item.Key] = item.Value.Item2;
            }

            if (history.Count > 0 || inkCanvas.Children.Count > 0)
                MarkCurrentPageInkChanged();

            _boardRoamingStrokeHistory = null;
        }

        private static bool AreStylusPointsEqual(StylusPointCollection first, StylusPointCollection second)
        {
            if (first.Count != second.Count) return false;
            for (var i = 0; i < first.Count; i++)
            {
                if (first[i].X != second[i].X || first[i].Y != second[i].Y)
                    return false;
            }
            return true;
        }
    }
}
