namespace Ink_Canvas.Helpers
{
    public partial class TimeMachine
    {
        internal sealed record Context(TimeMachineHistory[] History, int Index);
        internal Context CaptureContext() => new(_currentStrokeHistory.ToArray(), _currentIndex);
        internal void RestoreContext(Context context)
        {
            _currentStrokeHistory.Clear();
            _currentStrokeHistory.AddRange(context.History);
            _currentIndex = context.Index;
            NotifyUndoRedoState();
        }
    }
}
