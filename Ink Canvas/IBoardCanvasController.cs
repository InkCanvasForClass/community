using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;

namespace Ink_Canvas
{
    public interface IBoardCanvasController
    {
        StrokeCollection Strokes { get; }
        System.Windows.Visibility Visibility { get; set; }
        bool IsHitTestVisible { get; set; }
        bool IsManipulationEnabled { get; set; }
        InkCanvasEditingMode EditingMode { get; set; }
        void SetInkMode();
        void SetEraserByPointMode();
        void SetEraserByStrokeMode();
        void SetSelectMode();
        Color StrokeColor { get; set; }
        double StrokeWidth { get; set; }
        double HighlighterWidth { get; set; }
        bool IsHighlighterMode { get; set; }
        EllipseStylusShape EraserShape { get; set; }
        StrokeCollection GetSelectedStrokes();
        void Select(StrokeCollection strokes);
        void ClearSelection();
        event System.EventHandler<EditingModeChangedEventArgs> EditingModeChanged;
        event System.EventHandler<StrokeColorChangedEventArgs> StrokeColorChanged;
        event System.EventHandler<StrokeEventArgs> StrokeCollected;
    }

    public class EditingModeChangedEventArgs : System.EventArgs
    {
        public InkCanvasEditingMode OldMode { get; set; }
        public InkCanvasEditingMode NewMode { get; set; }
    }
    
    public class StrokeColorChangedEventArgs : System.EventArgs
    {
        public Color OldColor { get; set; }
        public Color NewColor { get; set; }
    }
    
    public class StrokeEventArgs : System.EventArgs
    {
        public StrokeCollection Strokes { get; set; }
    }
}
