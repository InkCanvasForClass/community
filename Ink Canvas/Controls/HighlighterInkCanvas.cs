using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Threading;
using Ink_Canvas.Ink;

namespace Ink_Canvas.Controls
{
    /// <summary>
    /// Keeps the editing collection intact, supplying rendering-only highlighter groups
    /// to the protected WPF presenter through its public Strokes property.
    /// </summary>
    public sealed class HighlighterInkCanvas : InkCanvas
    {
        private readonly HashSet<Stroke> _observed = new();
        private readonly HashSet<Stroke> _presentedHighlighters = new();
        private DispatcherOperation _refresh;

        public HighlighterInkCanvas()
        {
            Strokes.StrokesChanged += SourceChanged;
            RefreshPresentation();
        }

        protected override void OnStrokesReplaced(InkCanvasStrokesReplacedEventArgs e)
        {
            e.PreviousStrokes.StrokesChanged -= SourceChanged;
            e.NewStrokes.StrokesChanged += SourceChanged;
            RefreshPresentation();
            base.OnStrokesReplaced(e);
        }

        private void SourceChanged(object sender, StrokeCollectionChangedEventArgs e)
        {
            foreach (var stroke in e.Removed)
            {
                stroke.Invalidated -= StrokeInvalidated;
                _observed.Remove(stroke);
            }
            foreach (var stroke in e.Added)
                if (_observed.Add(stroke)) stroke.Invalidated += StrokeInvalidated;
            // The common ordinary-pen path stays on WPF's original collection and
            // does no page scan or geometry work, even on very long pages.
            if (ReferenceEquals(InkPresenter.Strokes, Strokes)
                && !e.Added.Any(HighlighterRendering.IsHighlighter)) return;
            RefreshPresentation(false);
        }

        private void StrokeInvalidated(object sender, EventArgs e)
        {
            // Ordinary strokes are already rendered by WPF using their original
            // identity. Their point/pressure/colour updates need no group rebuild.
            if (sender is Stroke stroke && !HighlighterRendering.IsHighlighter(stroke)
                && !_presentedHighlighters.Contains(stroke)) return;
            if (_refresh?.Status == DispatcherOperationStatus.Pending) return;
            _refresh = Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(RefreshPresentation));
        }

        private void RefreshPresentation() => RefreshPresentation(true);

        private void RefreshPresentation(bool updateSubscriptions)
        {
            if (updateSubscriptions)
            {
                foreach (var stroke in _observed) stroke.Invalidated -= StrokeInvalidated;
                _observed.Clear();
                foreach (var stroke in Strokes)
                {
                    stroke.Invalidated += StrokeInvalidated;
                    _observed.Add(stroke);
                }
            }
            _presentedHighlighters.Clear();
            foreach (var stroke in Strokes.Where(HighlighterRendering.IsHighlighter))
                _presentedHighlighters.Add(stroke);
            if (_presentedHighlighters.Count == 0)
            {
                InkPresenter.Strokes = Strokes;
                return;
            }
            var desired = HighlighterRendering.CreatePresentation(Strokes);
            if (ReferenceEquals(InkPresenter.Strokes, Strokes))
            {
                InkPresenter.Strokes = desired;
                return;
            }
            // Preserve ordinary stroke visuals; replacing the whole presenter collection
            // on every pen-up would redraw long pages on low-powered devices.
            var current = InkPresenter.Strokes;
            var retained = new HashSet<Stroke>(desired);
            foreach (var previous in current.ToArray())
                if (!retained.Contains(previous)) current.Remove(previous);
            for (int i = 0; i < desired.Count; i++)
            {
                if (i < current.Count && ReferenceEquals(current[i], desired[i])) continue;
                if (current.Contains(desired[i])) current.Remove(desired[i]);
                current.Insert(i, desired[i]);
            }
        }
    }
}
