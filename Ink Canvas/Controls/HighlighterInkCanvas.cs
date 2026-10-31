using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
        private bool _presentationPaused;
        private double _presentationOpacity;
        private Transform _originalPresentationTransform;
        private MatrixTransform _viewTransform;
        private long _presentationSampleAt;

        internal void SetPresentationTransform(Matrix? matrix)
        {
            if (matrix.HasValue)
            {
                if (_viewTransform == null)
                {
                    _originalPresentationTransform = InkPresenter.RenderTransform;
                    _viewTransform = new MatrixTransform();
                    var transforms = new TransformGroup();
                    transforms.Children.Add(_originalPresentationTransform);
                    transforms.Children.Add(_viewTransform);
                    InkPresenter.RenderTransform = transforms;
                }
                long now = Environment.TickCount64;
                Matrix displayed = _viewTransform.Matrix, target = matrix.Value;
                bool smooth = _presentationSampleAt != 0 && now - _presentationSampleAt is >= 8 and <= 150
                    && displayed.M11 == target.M11 && displayed.M22 == target.M22
                    && displayed.M12 == target.M12 && displayed.M21 == target.M21
                    && Math.Abs(displayed.OffsetY - target.OffsetY) < Math.Max(100, ActualHeight / 2)
                    && Math.Abs(displayed.OffsetX - target.OffsetX) < Math.Max(100, ActualWidth / 2);
                _viewTransform.BeginAnimation(MatrixTransform.MatrixProperty, null);
                _viewTransform.Matrix = target;
                if (smooth && displayed != target)
                {
                    // Bounded interpolation between measured samples; never predict
                    // motion, move editable points, or animate zoom/page jumps.
                    var animation = new TranslationAnimation(displayed, target)
                    { Duration = TimeSpan.FromMilliseconds(Math.Clamp(now - _presentationSampleAt, 8, 32)), FillBehavior = FillBehavior.Stop };
                    _viewTransform.BeginAnimation(MatrixTransform.MatrixProperty, animation);
                }
                _presentationSampleAt = now;
            }
            else if (_viewTransform != null)
            {
                _viewTransform.BeginAnimation(MatrixTransform.MatrixProperty, null);
                InkPresenter.RenderTransform = _originalPresentationTransform;
                _originalPresentationTransform = null;
                _viewTransform = null;
                _presentationSampleAt = 0;
            }
        }

        private sealed class TranslationAnimation(Matrix from, Matrix to) : MatrixAnimationBase
        {
            protected override Freezable CreateInstanceCore() => new TranslationAnimation(from, to);
            protected override Matrix GetCurrentValueCore(Matrix defaultOriginValue, Matrix defaultDestinationValue, AnimationClock clock)
            {
                double progress = clock.CurrentProgress ?? 1;
                return new Matrix(to.M11, to.M12, to.M21, to.M22,
                    from.OffsetX + (to.OffsetX - from.OffsetX) * progress,
                    from.OffsetY + (to.OffsetY - from.OffsetY) * progress);
            }
        }

        internal void PausePresentation(bool paused)
        {
            if (_presentationPaused == paused) return;
            _presentationPaused = paused;
            if (paused) { _presentationOpacity = InkPresenter.Opacity; InkPresenter.Opacity = 0; }
            else InkPresenter.Opacity = _presentationOpacity;
        }

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
