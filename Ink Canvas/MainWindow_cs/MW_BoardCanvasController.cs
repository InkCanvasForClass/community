using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;

namespace Ink_Canvas
{
    public partial class MainWindow
    {
        private IBoardCanvasController _boardCanvasController;

        public IBoardCanvasController BoardCanvasController => _boardCanvasController;

        private void InitializeBoardCanvasController()
        {
            if (_boardCanvasController == null)
            {
                _boardCanvasController = new MainWindowBoardCanvasController(this);
            }
        }
    }

    internal sealed class MainWindowBoardCanvasController : IBoardCanvasController
    {
        private readonly MainWindow _window;
        private InkCanvasEditingMode _lastEditingMode;

        public MainWindowBoardCanvasController(MainWindow window)
        {
            _window = window;
            _lastEditingMode = _window.inkCanvas.EditingMode;
            _window.inkCanvas.EditingModeChanged += InkCanvasOnEditingModeChanged;
            _window.inkCanvas.StrokeCollected += InkCanvasOnStrokeCollected;
        }

        public StrokeCollection Strokes => _window.inkCanvas.Strokes;

        public System.Windows.Visibility Visibility
        {
            get => _window.inkCanvas.Visibility;
            set => _window.inkCanvas.Visibility = value;
        }

        public bool IsHitTestVisible
        {
            get => _window.inkCanvas.IsHitTestVisible;
            set => _window.inkCanvas.IsHitTestVisible = value;
        }

        public bool IsManipulationEnabled
        {
            get => _window.inkCanvas.IsManipulationEnabled;
            set => _window.inkCanvas.IsManipulationEnabled = value;
        }

        public InkCanvasEditingMode EditingMode
        {
            get => _window.inkCanvas.EditingMode;
            set
            {
                if (_window.inkCanvas.EditingMode == value)
                {
                    return;
                }

                var oldMode = _window.inkCanvas.EditingMode;
                _window.inkCanvas.EditingMode = value;
                EditingModeChanged?.Invoke(this, new EditingModeChangedEventArgs { OldMode = oldMode, NewMode = value });
            }
        }

        public void SetInkMode() => EditingMode = InkCanvasEditingMode.Ink;

        public void SetEraserByPointMode() => EditingMode = InkCanvasEditingMode.EraseByPoint;

        public void SetEraserByStrokeMode() => EditingMode = InkCanvasEditingMode.EraseByStroke;

        public void SetSelectMode() => EditingMode = InkCanvasEditingMode.Select;

        public Color StrokeColor
        {
            get => _window.inkCanvas.DefaultDrawingAttributes.Color;
            set
            {
                if (_window.inkCanvas.DefaultDrawingAttributes.Color == value)
                {
                    return;
                }

                var oldColor = _window.inkCanvas.DefaultDrawingAttributes.Color;
                _window.inkCanvas.DefaultDrawingAttributes.Color = value;
                StrokeColorChanged?.Invoke(this, new StrokeColorChangedEventArgs { OldColor = oldColor, NewColor = value });
            }
        }

        public double StrokeWidth
        {
            get => _window.inkCanvas.DefaultDrawingAttributes.Width;
            set => _window.inkCanvas.DefaultDrawingAttributes.Width = value;
        }

        public double HighlighterWidth
        {
            get => _window.inkCanvas.DefaultDrawingAttributes.Height;
            set => _window.inkCanvas.DefaultDrawingAttributes.Height = value;
        }

        public bool IsHighlighterMode
        {
            get => _window.inkCanvas.DefaultDrawingAttributes.IsHighlighter;
            set => _window.inkCanvas.DefaultDrawingAttributes.IsHighlighter = value;
        }

        public StylusShape EraserShape
        {
            get => _window.inkCanvas.EraserShape;
            set => _window.inkCanvas.EraserShape = value;
        }

        public StrokeCollection GetSelectedStrokes() => _window.inkCanvas.GetSelectedStrokes();

        public void Select(StrokeCollection strokes) => _window.inkCanvas.Select(strokes);

        public void ClearSelection() => _window.inkCanvas.Select(new StrokeCollection());

        public event EventHandler<EditingModeChangedEventArgs> EditingModeChanged;

        public event EventHandler<StrokeColorChangedEventArgs> StrokeColorChanged;

        public event EventHandler<StrokeEventArgs> StrokeCollected;

        private void InkCanvasOnEditingModeChanged(object sender, RoutedEventArgs e)
        {
            var newMode = _window.inkCanvas.EditingMode;
            var oldMode = _lastEditingMode;
            _lastEditingMode = newMode;
            EditingModeChanged?.Invoke(this, new EditingModeChangedEventArgs { OldMode = oldMode, NewMode = newMode });
        }

        private void InkCanvasOnStrokeCollected(object sender, InkCanvasStrokeCollectedEventArgs e)
        {
            if (e?.Stroke == null)
            {
                return;
            }

            StrokeCollected?.Invoke(this, new StrokeEventArgs { Strokes = new StrokeCollection { e.Stroke } });
        }
    }
}
