using System.Windows.Ink;

namespace Ink_Canvas.Ink
{
    internal static class PersistentStrokePolicy
    {
        // Transient laser visuals must not be restored as permanent ink by undo/redo.
        internal static StrokeCollection Filter(StrokeCollection strokes)
        {
            var result = new StrokeCollection();
            foreach (Stroke stroke in strokes)
                if (!Helpers.InkFadeManager.IsLaserStroke(stroke)) result.Add(stroke);
            return result;
        }
    }
}
