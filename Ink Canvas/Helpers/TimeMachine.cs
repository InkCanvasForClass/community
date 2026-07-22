using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

// Added for UIElement

namespace Ink_Canvas.Helpers
{
    public partial class TimeMachine
    {
        private readonly List<TimeMachineHistory> _currentStrokeHistory = new List<TimeMachineHistory>();

        private int _currentIndex = -1;

        public delegate void OnUndoStateChange(bool status);

        public event OnUndoStateChange OnUndoStateChanged;

        public delegate void OnRedoStateChange(bool status);

        public event OnRedoStateChange OnRedoStateChanged;

        public void CommitStrokeUserInputHistory(StrokeCollection stroke)
        {
            if (_currentIndex + 1 < _currentStrokeHistory.Count)
            {
                _currentStrokeHistory.RemoveRange(_currentIndex + 1, (_currentStrokeHistory.Count - 1) - _currentIndex);
            }
            _currentStrokeHistory.Add(new TimeMachineHistory(stroke, TimeMachineHistoryType.UserInput, false));
            _currentIndex = _currentStrokeHistory.Count - 1;
            NotifyUndoRedoState();
        }

        public bool TryReplaceLastUserInputHistory(StrokeCollection stroke)
        {
            if (stroke == null || _currentIndex < 0 || _currentIndex >= _currentStrokeHistory.Count)
            {
                return false;
            }

            var item = _currentStrokeHistory[_currentIndex];
            if (item.CommitType != TimeMachineHistoryType.UserInput || item.StrokeHasBeenCleared)
            {
                return false;
            }

            item.CurrentStroke = stroke;
            NotifyUndoRedoState();
            return true;
        }

        public void CommitStrokeShapeHistory(StrokeCollection strokeToBeReplaced, StrokeCollection generatedStroke)
        {
            if (_currentIndex + 1 < _currentStrokeHistory.Count)
            {
                _currentStrokeHistory.RemoveRange(_currentIndex + 1, (_currentStrokeHistory.Count - 1) - _currentIndex);
            }
            _currentStrokeHistory.Add(new TimeMachineHistory(generatedStroke,
                TimeMachineHistoryType.ShapeRecognition,
                false,
                strokeToBeReplaced));
            _currentIndex = _currentStrokeHistory.Count - 1;
            NotifyUndoRedoState();
        }

        public void CommitStrokeManipulationHistory(Dictionary<Stroke, Tuple<StylusPointCollection, StylusPointCollection>> stylusPointDictionary)
        {
            if (_currentIndex + 1 < _currentStrokeHistory.Count)
            {
                _currentStrokeHistory.RemoveRange(_currentIndex + 1, (_currentStrokeHistory.Count - 1) - _currentIndex);
            }
            _currentStrokeHistory.Add(
                new TimeMachineHistory(stylusPointDictionary,
                    TimeMachineHistoryType.Manipulation));
            _currentIndex = _currentStrokeHistory.Count - 1;
            NotifyUndoRedoState();
        }
        public void CommitStrokeDrawingAttributesHistory(Dictionary<Stroke, Tuple<DrawingAttributes, DrawingAttributes>> drawingAttributes)
        {
            if (_currentIndex + 1 < _currentStrokeHistory.Count)
            {
                _currentStrokeHistory.RemoveRange(_currentIndex + 1, (_currentStrokeHistory.Count - 1) - _currentIndex);
            }
            _currentStrokeHistory.Add(
                new TimeMachineHistory(drawingAttributes,
                    TimeMachineHistoryType.DrawingAttributes));
            _currentIndex = _currentStrokeHistory.Count - 1;
            NotifyUndoRedoState();
        }

        public void CommitStrokeEraseHistory(StrokeCollection stroke, StrokeCollection sourceStroke = null)
        {
            if (_currentIndex + 1 < _currentStrokeHistory.Count)
            {
                _currentStrokeHistory.RemoveRange(_currentIndex + 1, (_currentStrokeHistory.Count - 1) - _currentIndex);
            }
            _currentStrokeHistory.Add(new TimeMachineHistory(stroke, TimeMachineHistoryType.Clear, true, sourceStroke));
            _currentIndex = _currentStrokeHistory.Count - 1;
            NotifyUndoRedoState();
        }

        public void ClearStrokeHistory()
        {
            _currentStrokeHistory.Clear();
            _currentIndex = -1;
            NotifyUndoRedoState();
        }

        public TimeMachineHistory Undo()
        {
            if (_currentIndex < 0 || _currentIndex >= _currentStrokeHistory.Count)
            {
                return null;
            }

            var item = _currentStrokeHistory[_currentIndex];
            item.StrokeHasBeenCleared = !item.StrokeHasBeenCleared;
            _currentIndex--;
            OnUndoStateChanged?.Invoke(CanUndo);
            OnRedoStateChanged?.Invoke(CanRedo);
            return item;
        }

        public TimeMachineHistory Redo()
        {
            if (_currentStrokeHistory.Count == 0 || _currentIndex >= _currentStrokeHistory.Count - 1)
            {
                return null;
            }

            var item = _currentStrokeHistory[++_currentIndex];
            item.StrokeHasBeenCleared = !item.StrokeHasBeenCleared;
            NotifyUndoRedoState();
            return item;
        }

        public TimeMachineHistory[] ExportTimeMachineHistory()
        {
            if (_currentIndex + 1 < _currentStrokeHistory.Count)
            {
                _currentStrokeHistory.RemoveRange(_currentIndex + 1, (_currentStrokeHistory.Count - 1) - _currentIndex);
            }
            return _currentStrokeHistory.ToArray();
        }

        public bool ImportTimeMachineHistory(TimeMachineHistory[] sourceHistory)
        {
            _currentStrokeHistory.Clear();
            _currentStrokeHistory.AddRange(sourceHistory);
            _currentIndex = _currentStrokeHistory.Count - 1;
            NotifyUndoRedoState();
            return true;
        }
        private void NotifyUndoRedoState()
        {
            OnUndoStateChanged?.Invoke(CanUndo);
            OnRedoStateChanged?.Invoke(CanRedo);
        }

        /// <summary>当前历史是否允许撤销。</summary>
        public bool CanUndo => _currentIndex > -1;

        /// <summary>当前历史是否允许重做。</summary>
        public bool CanRedo => _currentStrokeHistory.Count > 0 && _currentStrokeHistory.Count - _currentIndex - 1 > 0;

        /// <summary>
        /// 对当前页历史中所有不在画布上的 Stroke 应用变换矩阵。
        /// 用于视频展台模式：旋转/移动/缩放摄像头预览时，画布上的笔画已被
        /// inkCanvas.Strokes.Transform 变换，但历史中已移出画布的 Stroke
        /// （如形状识别的 ReplacedStroke、已撤销的 CurrentStroke）不会被变换。
        /// 撤销/重做时这些 Stroke 加回画布会出现在旧位置，因此需要同步变换。
        /// 注意：同一个 Stroke 可能出现在多个历史条目中（如条目1的 CurrentStroke
        /// 和条目2的 ReplacedStroke 是同一引用），用 HashSet 去重避免双重变换。
        /// </summary>
        public void TransformStrokesInHistory(Matrix matrix, StrokeCollection canvasStrokes)
        {
            var transformed = new HashSet<Stroke>();
            foreach (var item in _currentStrokeHistory)
            {
                TransformStrokeCollectionIfNotOnCanvas(item.ReplacedStroke, matrix, canvasStrokes, transformed);
                TransformStrokeCollectionIfNotOnCanvas(item.CurrentStroke, matrix, canvasStrokes, transformed);
            }
        }

        private static void TransformStrokeCollectionIfNotOnCanvas(StrokeCollection strokes, Matrix matrix, StrokeCollection canvasStrokes, HashSet<Stroke> transformed)
        {
            if (strokes == null) return;
            foreach (Stroke stroke in strokes)
            {
                if (!canvasStrokes.Contains(stroke) && transformed.Add(stroke))
                {
                    stroke.Transform(matrix, false);
                }
            }
        }
    }

    public class TimeMachineHistory
    {
        public TimeMachineHistoryType CommitType;
        public bool StrokeHasBeenCleared;
        public StrokeCollection CurrentStroke;
        public StrokeCollection ReplacedStroke;
        //这里说一下 Tuple的 Value1 是初始值 ; Value 2 是改变值
        public Dictionary<Stroke, Tuple<StylusPointCollection, StylusPointCollection>> StylusPointDictionary;
        public Dictionary<Stroke, Tuple<DrawingAttributes, DrawingAttributes>> DrawingAttributes;
        public UIElement InsertedElement; // 新增
        public TimeMachineHistory(StrokeCollection currentStroke, TimeMachineHistoryType commitType, bool strokeHasBeenCleared)
        {
            CommitType = commitType;
            CurrentStroke = currentStroke;
            StrokeHasBeenCleared = strokeHasBeenCleared;
            ReplacedStroke = null;
        }
        public TimeMachineHistory(Dictionary<Stroke, Tuple<StylusPointCollection, StylusPointCollection>> stylusPointDictionary, TimeMachineHistoryType commitType)
        {
            CommitType = commitType;
            StylusPointDictionary = stylusPointDictionary;
        }
        public TimeMachineHistory(Dictionary<Stroke, Tuple<DrawingAttributes, DrawingAttributes>> drawingAttributes, TimeMachineHistoryType commitType)
        {
            CommitType = commitType;
            DrawingAttributes = drawingAttributes;
        }
        public TimeMachineHistory(StrokeCollection currentStroke, TimeMachineHistoryType commitType, bool strokeHasBeenCleared, StrokeCollection replacedStroke)
        {
            CommitType = commitType;
            CurrentStroke = currentStroke;
            StrokeHasBeenCleared = strokeHasBeenCleared;
            ReplacedStroke = replacedStroke;
        }
        public TimeMachineHistory(UIElement element, TimeMachineHistoryType commitType) // 新增
        {
            CommitType = commitType;
            InsertedElement = element;
        }
    }

    public enum TimeMachineHistoryType
    {
        UserInput,
        ShapeRecognition,
        Clear,
        Manipulation,
        DrawingAttributes,
        ElementInsert // 新增
    }

    public partial class TimeMachine // 新增partial，便于扩展
    {
        public void CommitElementInsertHistory(UIElement element)
        {
            if (_currentIndex + 1 < _currentStrokeHistory.Count)
            {
                _currentStrokeHistory.RemoveRange(_currentIndex + 1, (_currentStrokeHistory.Count - 1) - _currentIndex);
            }
            _currentStrokeHistory.Add(new TimeMachineHistory(element, TimeMachineHistoryType.ElementInsert));
            _currentIndex = _currentStrokeHistory.Count - 1;
            NotifyUndoRedoState();
        }

        public void CommitElementRemoveHistory(UIElement element)
        {
            if (_currentIndex + 1 < _currentStrokeHistory.Count)
            {
                _currentStrokeHistory.RemoveRange(_currentIndex + 1, (_currentStrokeHistory.Count - 1) - _currentIndex);
            }
            var history = new TimeMachineHistory(element, TimeMachineHistoryType.ElementInsert);
            history.StrokeHasBeenCleared = true; // 标记为已清除
            _currentStrokeHistory.Add(history);
            _currentIndex = _currentStrokeHistory.Count - 1;
            NotifyUndoRedoState();
        }
    }
}