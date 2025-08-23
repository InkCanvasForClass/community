using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;

namespace InkCanvasForClass.IccInkCanvas {

    /// <summary>
    /// 时光机代码，基于 Ink Canvas 项目修改
    /// </summary>
    public class TimeMachine
    {
        private readonly List<TimeMachineHistory> _currentStrokeHistory = new List<TimeMachineHistory>();

        private int _currentIndex = -1;

        public delegate void OnUndoRedoStateChanged(TimeMachine timeMachine);

        public event OnUndoRedoStateChanged UndoRedoStateChanged;

        /// <summary>
        /// 提交历史记录的通用方法
        /// </summary>
        /// <param name="history">TimeMachineHistory</param>
        private void CommitHistory(TimeMachineHistory history) {
            Trace.WriteLine("History Commited");
            // 删除当前索引后的所有记录
            if (_currentIndex + 1 < _currentStrokeHistory.Count)
                _currentStrokeHistory.RemoveRange(_currentIndex + 1, (_currentStrokeHistory.Count - 1) - _currentIndex);

            // 添加历史记录
            _currentStrokeHistory.Add(history);
            _currentIndex = _currentStrokeHistory.Count - 1;

            // 通知撤销和重做的状态变化
            NotifyUndoRedoState();
        }

        /// <summary>
        /// 提交由用户绘制的墨迹添加到历史记录
        /// </summary>
        /// <param name="stroke"></param>
        public void CommitStrokeUserInputHistory(StrokeCollection stroke) {
            var history = new TimeMachineHistory(stroke, TimeMachineHistoryType.UserInput, false);
            CommitHistory(history);
        }

        /// <summary>
        /// 提交由墨迹纠正替换的墨迹添加到历史记录
        /// </summary>
        /// <param name="strokeToBeReplaced"></param>
        /// <param name="generatedStroke"></param>
        public void CommitStrokeShapeHistory(StrokeCollection strokeToBeReplaced, StrokeCollection generatedStroke) {
            var history = new TimeMachineHistory(generatedStroke, TimeMachineHistoryType.ShapeRecognition, false, strokeToBeReplaced);
            CommitHistory(history);
        }

        /// <summary>
        /// 提交墨迹点变更到历史记录，Dictionary中每一项的Key对应一条墨迹的引用，Value为元组，第一个是变化前，第二个变化后
        /// </summary>
        /// <param name="stylusPointDictionary"></param>
        public void CommitStylusPointsHistory(Dictionary<Stroke, Tuple<StylusPointCollection, StylusPointCollection>> stylusPointDictionary) {
            var history = new TimeMachineHistory(stylusPointDictionary, TimeMachineHistoryType.StylusPoints);
            CommitHistory(history);
        }

        /// <summary>
        /// 提交墨迹的墨迹属性变更到历史记录
        /// </summary>
        /// <param name="drawingAttributes"></param>
        public void CommitStrokeDrawingAttributesHistory(Dictionary<Stroke, Tuple<DrawingAttributes, DrawingAttributes>> drawingAttributes) {
            var history = new TimeMachineHistory(drawingAttributes, TimeMachineHistoryType.DrawingAttributes);
            CommitHistory(history);
        }

        /// <summary>
        /// 提交墨迹擦被擦除的变更到历史记录中
        /// </summary>
        /// <param name="stroke"></param>
        /// <param name="sourceStroke"></param>
        public void CommitStrokeEraseHistory(StrokeCollection stroke, StrokeCollection sourceStroke = null) {
            var history = new TimeMachineHistory(stroke, TimeMachineHistoryType.Erased, true, sourceStroke);
            CommitHistory(history);
        }

        /// <summary>
        /// 清空所有历史记录
        /// </summary>
        public void ClearHistory()
        {
            _currentStrokeHistory.Clear();
            _currentIndex = -1;
            NotifyUndoRedoState();
        }

        /// <summary>
        /// 撤销
        /// </summary>
        /// <returns></returns>
        public TimeMachineHistory Undo(bool doNotNotifyStateUpdate = false) {
            if (!(_currentIndex > -1)) return null;

            // 如果当前的墨迹是被创建了，就修改为指示被清除
            var item = _currentStrokeHistory[_currentIndex];
            item.StrokeHasBeenCleared = !item.StrokeHasBeenCleared;

            var index = Math.Min(Math.Max(_currentIndex - 1, -1), _currentStrokeHistory.Count - 1);
            _currentIndex = index;
            
            if (!doNotNotifyStateUpdate) NotifyUndoRedoState();
            return item;
        }

        /// <summary>
        /// 撤销，指定步长
        /// </summary>
        /// <param name="steps"></param>
        /// <returns></returns>
        public TimeMachineHistory[] Undo(int steps = 1) {
            var histories = new List<TimeMachineHistory>();

            for (int i = 0; i < steps; i++) {
                var item = Undo(true);
                if (item != null) histories.Add(item);
            }
            NotifyUndoRedoState();
            return histories.ToArray();
        }

        /// <summary>
        /// 重做
        /// </summary>
        /// <returns></returns>
        public TimeMachineHistory Redo(bool doNotNotifyStateUpdate = false) {
            if (!(_currentStrokeHistory.Count - _currentIndex - 1 > 0)) return null;
            var index = Math.Min(Math.Max(_currentIndex + 1, -1), _currentStrokeHistory.Count - 1);

            // 如果当前的墨迹是被清除了，就修改为指示已创建
            var item = _currentStrokeHistory[index];
            item.StrokeHasBeenCleared = !item.StrokeHasBeenCleared;

            _currentIndex = index;

            if (!doNotNotifyStateUpdate) NotifyUndoRedoState();
            return item;
        }

        /// <summary>
        /// 重做，指定步长
        /// </summary>
        /// <param name="steps"></param>
        /// <returns></returns>
        public TimeMachineHistory[] Redo(int steps = 1) {
            var histories = new List<TimeMachineHistory>();

            for (int i = 0; i < steps; i++) {
                var item = Redo(true);
                if (item != null) histories.Add(item);
            }
            NotifyUndoRedoState();
            return histories.ToArray();
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
        private void NotifyUndoRedoState() {
            Application.Current.Dispatcher.InvokeAsync(() => {
                UndoRedoStateChanged?.Invoke(this);
            });
        }

        public bool CanUndo => _currentIndex > -1;
        public bool CanRedo => _currentStrokeHistory.Count - _currentIndex - 1 > 0;
        public int CurrentHistoriesCount => _currentStrokeHistory.Count;
    }

    public class TimeMachineHistory
    {
        public TimeMachineHistoryType CommitType;
        public bool StrokeHasBeenCleared = false;
        public StrokeCollection CurrentStroke;
        public StrokeCollection ReplacedStroke;
        // Tuple的 Value1 是初始值 ; Value 2 是改变值
        public Dictionary<Stroke, Tuple<StylusPointCollection, StylusPointCollection>> StylusPointsDictionary;
        public Dictionary<Stroke, Tuple<DrawingAttributes, DrawingAttributes>> DrawingAttributes;
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
            StylusPointsDictionary = stylusPointDictionary;
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
    }

    public enum TimeMachineHistoryType
    {
        UserInput,
        ShapeRecognition,
        Erased,
        StylusPoints,
        DrawingAttributes
    }
}
