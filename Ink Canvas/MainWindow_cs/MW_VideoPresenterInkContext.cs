using System.Windows.Ink;
using Ink_Canvas.Helpers;

namespace Ink_Canvas
{
    public partial class MainWindow
    {
        private StrokeCollection _inkBeforeVideoBooth;
        private TimeMachine.Context _historyBeforeVideoBooth;
        private int _boardColorBeforeVideoBooth;
        private int _desktopColorBeforeVideoBooth;

        private void SuspendInkForVideoBooth()
        {
            if (_inkBeforeVideoBooth != null) return;
            ClearAllSelection();
            _inkBeforeVideoBooth = new StrokeCollection(inkCanvas.Strokes);
            _historyBeforeVideoBooth = timeMachine.CaptureContext();
            _boardColorBeforeVideoBooth = lastBoardInkColor;
            _desktopColorBeforeVideoBooth = lastDesktopInkColor;
            var previous = _currentCommitType;
            try
            {
                _currentCommitType = CommitReason.CodeInput;
                inkCanvas.Strokes.Clear();
                timeMachine.ClearStrokeHistory();
            }
            finally { _currentCommitType = previous; }
        }

        private void RestoreInkAfterVideoBooth()
        {
            if (_inkBeforeVideoBooth == null) return;
            var previous = _currentCommitType;
            try
            {
                _currentCommitType = CommitReason.CodeInput;
                inkCanvas.Strokes.Clear();
                inkCanvas.Strokes.Add(_inkBeforeVideoBooth);
                timeMachine.RestoreContext(_historyBeforeVideoBooth);
                lastBoardInkColor = _boardColorBeforeVideoBooth;
                lastDesktopInkColor = _desktopColorBeforeVideoBooth;
                CheckColorTheme();
                UpdateBoardPenIconColor();
                _inkBeforeVideoBooth = null;
                _historyBeforeVideoBooth = null;
            }
            finally { _currentCommitType = previous; }
        }
    }
}
