using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;

namespace Ink_Canvas.Controls
{
    internal sealed class ShapePreviewSession
    {
        private readonly InkCanvas _owner;
        private readonly HighlighterInkCanvas _preview = new()
        {
            IsHitTestVisible = false, Background = Brushes.Transparent, EditingMode = InkCanvasEditingMode.None
        };
        internal StrokeCollection Strokes => _preview.Strokes;
        internal ShapePreviewSession(InkCanvas owner) => _owner = owner;
        internal void Attach()
        {
            _preview.Width = _owner.ActualWidth;
            _preview.Height = _owner.ActualHeight;
            if (!_owner.Children.Contains(_preview)) _owner.Children.Add(_preview);
        }
        internal void Commit()
        {
            var final = new StrokeCollection(Strokes);
            Clear();
            if (final.Count > 0) _owner.Strokes.Add(final);
        }
        internal void Clear()
        {
            Strokes.Clear();
            _owner.Children.Remove(_preview);
        }
    }
}
