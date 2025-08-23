using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Ink;
using System.Windows.Threading;

namespace InkCanvasForClass.IccInkCanvas {

    /// <summary>
    /// 指示时光机的一个Action的类型
    /// </summary>
    public enum TimeMachineActionType {
        /// <summary>
        /// 用户绘制的墨迹被添加
        /// </summary>
        UserInputStrokeAdded,
        /// <summary>
        /// 墨迹被添加，这可能是由软件添加的
        /// </summary>
        StrokeAdded,
        /// <summary>
        /// 墨迹被移除
        /// </summary>
        StrokeRemoved,
        /// <summary>
        /// 用户使用了面积擦进行擦除
        /// </summary>
        UserGeometryErased,
        /// <summary>
        /// 用户使用了墨迹擦进行擦除
        /// </summary>
        UserStrokeErased,
        /// <summary>
        /// 用户使用了墨迹擦进行擦除
        /// </summary>
        UserAreaErased,
        Test
    }

    public class TimeMachineAction {
        /// <summary>
        /// Action的类型
        /// </summary>
        public TimeMachineActionType ActionType { get; protected set ; }

        private bool _isReversed = false;

        public event EventHandler ActionIsReversedChanged;

        /// <summary>
        /// 指示该Action是否被逆操作了。
        /// 比如现在如果这是一个StrokeAdded的Action，当执行撤销后，墨迹消失，此时IsReversed应该为True。
        /// </summary>
        public bool IsReversed {
            get => _isReversed;
            set {
                if (value == _isReversed) return;
                _isReversed = value;
                ActionIsReversedChanged?.Invoke(this,null);
            }
        }
    }

    /// <summary>
    /// 墨迹操作的Action
    /// </summary>
    public class TimeMachineStrokeAction : TimeMachineAction {

        public event EventHandler AsyncPerformerLocked;
        public event EventHandler AsyncPerformerUnlocked;

        public bool IsLocked { get; protected set; } = false;

        public TimeMachineStrokeAction(TimeMachineActionType type, IccStroke stroke) {
            ActionType = (new TimeMachineActionType[] {
                TimeMachineActionType.UserInputStrokeAdded,
                TimeMachineActionType.StrokeAdded,
                TimeMachineActionType.StrokeRemoved,
            }).Contains(type)
                ? TimeMachineActionType.StrokeAdded
                : type;

            Stroke = stroke;
        }

        public TimeMachineStrokeAction(TimeMachineActionType type, IccStroke stroke, long timeStamp) {
            ActionType = (new TimeMachineActionType[] {
                TimeMachineActionType.UserInputStrokeAdded,
                TimeMachineActionType.StrokeAdded,
                TimeMachineActionType.StrokeRemoved,
            }).Contains(type)
                ? TimeMachineActionType.StrokeAdded
                : type;

            Stroke = stroke;
            TimeStamp = timeStamp;
        }

        /// <summary>
        /// 墨迹。如果为UserInputStrokeAdded或StrokeAdded则为添加的墨迹，否则，StrokeRemoved就是被移除的墨迹。
        /// </summary>
        public IccStroke Stroke { get; private set; }

        /// <summary>
        /// 执行这个操作的时间戳，-1为没有时间。
        /// </summary>
        public long TimeStamp { get; private set; } = -1;

        /// <summary>
        /// 应用该Action所导致的更改到画布上，需要指定Dispatcher，StrokeCollection
        /// </summary>
        public void Perform(Dispatcher dispatcher, StrokeCollection strokes) {
            IsLocked = true;
            AsyncPerformerLocked?.Invoke(this,null);
            var task = dispatcher.InvokeAsync(() => {
                if (IsReversed) {
                    if (ActionType == TimeMachineActionType.StrokeAdded ||
                        ActionType == TimeMachineActionType.UserInputStrokeAdded) strokes.Remove(Stroke);
                    else if (ActionType == TimeMachineActionType.StrokeRemoved)
                        try {
                            strokes.Add(Stroke);
                        }
                        catch { }
                } else {
                    if (ActionType == TimeMachineActionType.StrokeAdded ||
                        ActionType == TimeMachineActionType.UserInputStrokeAdded)
                        try {
                            strokes.Add(Stroke);
                        }
                        catch { }
                    else if (ActionType == TimeMachineActionType.StrokeRemoved) strokes.Remove(Stroke);
                }
            });
            task.Completed += (_, __) => {
                IsLocked = false;
                AsyncPerformerUnlocked?.Invoke(this, null);
            };
        }
    }

    /// <summary>
    /// only internal usage
    /// </summary>
    internal class TimeMachineTestAction : TimeMachineAction {
        private long timeStamp;

        public TimeMachineTestAction() {
            ActionType = TimeMachineActionType.Test;
            timeStamp = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds();
        }

        public void Perform() {
            Trace.WriteLine($"helloworld! reversed:{IsReversed} timestamp: {timeStamp}");
        }
    }

    /// <summary>
    /// 基于 icc 修改的 时光机 V2 版本，提供了更精简的设计
    /// </summary>
    public class TimeMachineV2 {

        /// <summary>
        /// 当前的Index
        /// </summary>
        private int _currentIndex = 0;

        public int CurrentIndex => _currentIndex;

        private List<TimeMachineAction> _actions = new List<TimeMachineAction>();

        public TimeMachineAction[] Actions => _actions.ToArray();

        public int ActionsCount => _actions.Count;

        /// <summary>
        /// 在撤销重做状态发生改变时触发
        /// </summary>
        public event EventHandler UndoRedoStateChanged;

        /// <summary>
        /// 执行action的perform的时候用于锁住时光机
        /// </summary>
        private bool _asyncPerformerLocker = false;

        private void asyncPerformerLockedOrUnLocked(object sender, EventArgs e) {
            if (sender is TimeMachineStrokeAction) {
                _asyncPerformerLocker = (sender as TimeMachineStrokeAction).IsLocked;
            }
        }

        /// <summary>
        /// 丢弃指针Index后的所有Actions并销毁锁事件
        /// </summary>
        private void DropActionsAfterCursorIndex() {
            var range = _actions.GetRange(_currentIndex, _actions.Count - _currentIndex);
            foreach (var a in range) {
                if (a is TimeMachineStrokeAction) {
                    (a as TimeMachineStrokeAction).AsyncPerformerUnlocked -= asyncPerformerLockedOrUnLocked;
                    (a as TimeMachineStrokeAction).AsyncPerformerLocked -= asyncPerformerLockedOrUnLocked;
                }
            }
            _actions.RemoveRange(_currentIndex, _actions.Count - _currentIndex);
        }

        public void PushStrokeAction(TimeMachineActionType type, IccStroke stroke, long timeStamp) {
            var action = new TimeMachineStrokeAction(type, stroke, timeStamp);
            _actions.RemoveRange(_currentIndex, _actions.Count - _currentIndex);
            _actions.Add(action);
            _currentIndex++;
            UndoRedoStateChanged?.Invoke(this,null);
            
        }

        /// <summary>
        /// only internal usage
        /// </summary>
        public void PushTestAction() {
            var action = new TimeMachineTestAction();
            _actions.RemoveRange(_currentIndex, _actions.Count - _currentIndex);
            _actions.Add(action);
            _currentIndex++;
            UndoRedoStateChanged?.Invoke(this,null);
        }

        public void Undo() {
            Undo(false);
        }

        /// <summary>
        /// 撤销
        /// </summary>
        public void Undo(bool doNotNotifyStateUpdate = false) {
            if (!(_currentIndex > 0)) return;
            if (_asyncPerformerLocker) return;

            var item = _actions[Math.Max(_currentIndex-1,0)];
            if ((new TimeMachineActionType[] {
                    TimeMachineActionType.Test,
                }).Contains(item.ActionType)) {
                (item as TimeMachineTestAction).IsReversed = true;
                (item as TimeMachineTestAction).Perform();
            }

            var index = Math.Min(Math.Max(_currentIndex - 1, 0), _actions.Count);
            _currentIndex = index;
            
            if (!doNotNotifyStateUpdate) UndoRedoStateChanged?.Invoke(this,null);
        }

        /// <summary>
        /// 多步撤销
        /// </summary>
        /// <param name="steps">步长</param>
        public void Undo(bool doNotNotifyStateUpdate = false, int steps = 1) {
            for (int i = 0; i < steps; i++) {
                Undo(true);
            }

            if (!doNotNotifyStateUpdate) UndoRedoStateChanged?.Invoke(this,null);
        }

        /// <summary>
        /// 多步重做
        /// </summary>
        /// <param name="steps">步长</param>
        public void Redo(bool doNotNotifyStateUpdate = false, int steps = 1) {
            for (int i = 0; i < steps; i++) {
                Redo(true);
            }

            if (!doNotNotifyStateUpdate) UndoRedoStateChanged?.Invoke(this,null);
        }

        public void Redo() {
            Redo(false);
        }

        /// <summary>
        /// 重做
        /// </summary>
        public void Redo(bool doNotNotifyStateUpdate = false) {
            if (!(_actions.Count - _currentIndex > 0)) return;
            if (_asyncPerformerLocker) return;

            var item = _actions[Math.Min(_currentIndex, _actions.Count - 1)];
            if ((new TimeMachineActionType[] {
                    TimeMachineActionType.Test,
                }).Contains(item.ActionType)) {
                (item as TimeMachineTestAction).IsReversed = false;
                (item as TimeMachineTestAction).Perform();
            }

            var index = Math.Min(Math.Max(_currentIndex + 1, 0), _actions.Count);
            _currentIndex = index;
            
            if (!doNotNotifyStateUpdate) UndoRedoStateChanged?.Invoke(this,null);
        }
    }
}
