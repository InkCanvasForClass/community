using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using InkCanvasWordLink;

namespace Ink_Canvas.WordLink
{
    internal sealed class WordInkProjection
    {
        private readonly InkCanvas _canvas;
        private readonly Dictionary<Stroke, (int Page, Stroke Canonical, Matrix Transform)> _sources = new();
        private Dictionary<(int Page, Stroke Canonical), Stroke> _projected = new();
        private readonly HashSet<Stroke> _dirty = new();
        private readonly Action<Matrix?> _previewTransform;
        private readonly Dictionary<int, Matrix> _displayTransforms = new();
        private readonly Dictionary<int, Rect> _displayBounds = new();
        private Dictionary<int, StrokeCollection> _displayPages;
        private long _displayWindow;
        private Geometry _lastClip;
        private Rect[] _clipRects = Array.Empty<Rect>();
        internal bool IsPreviewing { get; private set; }
        internal bool Updating { get; private set; }
        internal WordInkProjection(InkCanvas canvas, Action<Matrix?> previewTransform = null)
        { _canvas = canvas; _previewTransform = previewTransform; }

        internal bool TryPreview(WordFrame frame, WordInkDocument document)
        {
            if (_previewTransform == null || HasChanges || !ReferenceEquals(_displayPages, document.Pages)
                || _displayWindow != frame.WindowHandle || _displayTransforms.Count != frame.Pages.Count) return false;
            Matrix? delta = null;
            foreach (var page in frame.Pages)
            {
                if (!_displayTransforms.TryGetValue(page.Number, out var previous)) return false;
                previous.Invert(); previous.Append(Transform(page));
                if (delta.HasValue && (Math.Abs(previous.M11 - delta.Value.M11) > .0001
                    || Math.Abs(previous.M22 - delta.Value.M22) > .0001
                    || Math.Abs(previous.OffsetX - delta.Value.OffsetX) > .5
                    || Math.Abs(previous.OffsetY - delta.Value.OffsetY) > .5)) return false;
                delta = previous;
            }
            if (!delta.HasValue) return false;
            ClearSelection(_canvas);
            _previewTransform(delta.Value);
            IsPreviewing = true;
            UpdateClip(frame);
            return true;
        }

        internal Dictionary<int, StrokeCollection> Capture(WordFrame frame, WordInkDocument document)
        {
            var result = new Dictionary<int, StrokeCollection>(document.Pages);
            foreach (var page in frame.Pages)
            {
                var captured = new StrokeCollection();
                // Toolbar edits can still arrive while the render-only transform
                // is active. Their source points remain in the settled coordinate
                // system until Show commits the new view.
                Matrix inverse = IsPreviewing ? _displayTransforms[page.Number] : Transform(page); inverse.Invert();
                Rect bounds = IsPreviewing ? _displayBounds[page.Number] : AnnotationBounds(frame, page);
                foreach (var stroke in _canvas.Strokes)
                {
                    if (Helpers.InkFadeManager.IsLaserStroke(stroke)) continue;
                    if (_sources.TryGetValue(stroke, out var source) && !_dirty.Contains(stroke))
                    {
                        if (source.Page == page.Number) captured.Add(source.Canonical);
                        continue;
                    }
                    if (bounds.IsEmpty) continue;
                    foreach (var clipped in stroke.GetClipResult(bounds))
                    {
                        clipped.Transform(inverse, true);
                        captured.Add(clipped);
                    }
                }
                if (captured.Count == 0) result.Remove(page.Number);
                else if (!document.Pages.TryGetValue(page.Number, out var previous)
                    || !previous.SequenceEqual(captured)) result[page.Number] = captured;
            }
            return result;
        }

        internal void Show(WordFrame frame, WordInkDocument document)
        {
            Updating = true;
            try
            {
                EndPreview();
                _displayTransforms.Clear();
                _displayBounds.Clear();
                var existing = _canvas.Strokes.ToHashSet();
                var projected = new List<Stroke>();
                var next = new Dictionary<(int Page, Stroke Canonical), Stroke>();
                foreach (var page in frame.Pages)
                {
                    Matrix transform = Transform(page);
                    _displayTransforms[page.Number] = transform;
                    _displayBounds[page.Number] = AnnotationBounds(frame, page);
                    if (document.Pages.TryGetValue(page.Number, out var strokes))
                        foreach (var canonical in strokes)
                        {
                            var key = (page.Number, canonical);
                            if (_projected.TryGetValue(key, out var visible) && existing.Contains(visible)
                                && !_dirty.Contains(visible))
                            {
                                Matrix previous = _sources[visible].Transform;
                                if (previous != transform)
                                {
                                    if (previous.M11 == transform.M11 && previous.M22 == transform.M22
                                        && previous.M12 == transform.M12 && previous.M21 == transform.M21)
                                        visible.Transform(new Matrix(1, 0, 0, 1,
                                            transform.OffsetX - previous.OffsetX, transform.OffsetY - previous.OffsetY), false);
                                    else
                                    {
                                        // Zoom from the canonical data, avoiding accumulated
                                        // pen-width and point rounding errors across zooms.
                                        visible.StylusPoints = canonical.StylusPoints.Clone();
                                        visible.DrawingAttributes = canonical.DrawingAttributes.Clone();
                                        visible.Transform(transform, true);
                                    }
                                }
                            }
                            else
                            {
                                visible = canonical.Clone(); visible.Transform(transform, true);
                                visible.Invalidated += Invalidated;
                            }
                            _sources[visible] = (page.Number, canonical, transform);
                            next.Add(key, visible);
                            projected.Add(visible);
                        }
                }
                var keep = projected.ToHashSet();
                foreach (var obsolete in _sources.Keys.Where(s => !keep.Contains(s)).ToArray())
                {
                    obsolete.Invalidated -= Invalidated;
                    _sources.Remove(obsolete);
                }
                _projected = next; _dirty.Clear();
                ClearSelection(_canvas);
                foreach (var obsolete in existing.Where(s => !keep.Contains(s))) _canvas.Strokes.Remove(obsolete);
                for (int i = 0; i < projected.Count; i++)
                {
                    if (i < _canvas.Strokes.Count && ReferenceEquals(_canvas.Strokes[i], projected[i])) continue;
                    if (existing.Contains(projected[i])) _canvas.Strokes.Remove(projected[i]);
                    _canvas.Strokes.Insert(i, projected[i]);
                }
                UpdateClip(frame);
                _displayPages = document.Pages;
                _displayWindow = frame.WindowHandle;
                HasChanges = false;
            }
            finally { Updating = false; }
        }

        internal bool HasChanges { get; private set; }
        internal static void ClearSelection(InkCanvas canvas)
        {
            // Select(empty) also changes EditingMode to Select. Never invoke it
            // from a page refresh while a pen, eraser or cursor is active.
            if (canvas.EditingMode == InkCanvasEditingMode.Select
                && (canvas.GetSelectedStrokes().Count > 0 || canvas.GetSelectedElements().Count > 0))
                canvas.Select(new StrokeCollection());
        }
        internal void Changed() { if (!Updating) HasChanges = true; }
        private void Invalidated(object sender, EventArgs e) { if (!Updating) { _dirty.Add((Stroke)sender); HasChanges = true; } }
        internal void Accept() { HasChanges = false; }
        internal void Reset()
        {
            EndPreview();
            _displayTransforms.Clear(); _displayBounds.Clear(); _displayPages = null;
            _lastClip = null; _clipRects = Array.Empty<Rect>();
            foreach (var stroke in _sources.Keys) stroke.Invalidated -= Invalidated;
            _sources.Clear(); _projected.Clear(); _dirty.Clear(); HasChanges = false;
        }

        private void EndPreview()
        {
            if (!IsPreviewing) return;
            _previewTransform(null);
            IsPreviewing = false;
        }

        private void UpdateClip(WordFrame frame)
        {
            Rect viewport = RectFor(frame.Viewport);
            var rects = frame.Pages.Select(page => Rect.Intersect(AnnotationBounds(frame, page), viewport))
                .Where(rect => !rect.IsEmpty).ToArray();
            if (ReferenceEquals(_canvas.Clip, _lastClip) && rects.SequenceEqual(_clipRects)) return;
            var clip = new GeometryGroup();
            foreach (var rect in rects) clip.Children.Add(new RectangleGeometry(rect));
            clip.Freeze(); _canvas.Clip = clip;
            _lastClip = clip; _clipRects = rects;
        }

        internal bool EqualsContent(Dictionary<int, StrokeCollection> first, Dictionary<int, StrokeCollection> second)
            => first.Count == second.Count && first.All(p => second.TryGetValue(p.Key, out var value)
                && p.Value.Count == value.Count && p.Value.Zip(value).All(pair => ReferenceEquals(pair.First, pair.Second)));

        private Matrix Transform(WordPageFrame page)
        {
            Rect rect = RectFor(page.Bounds);
            return new Matrix(rect.Width / page.WidthPoints, 0, 0, rect.Height / page.HeightPoints, rect.X, rect.Y);
        }

        private Rect AnnotationBounds(WordFrame frame, WordPageFrame page)
        {
            Rect paper = RectFor(page.Bounds), viewport = RectFor(frame.Viewport);
            double left = viewport.Left, right = viewport.Right;
            double center = paper.Left + paper.Width / 2;
            foreach (var other in frame.Pages)
            {
                if (other.Number == page.Number) continue;
                Rect neighbor = RectFor(other.Bounds);
                if (neighbor.Bottom <= paper.Top || neighbor.Top >= paper.Bottom) continue;
                // Facing pages share the gutter at its midpoint. Outer margins
                // belong to the adjacent page, including negative page-space X.
                if (neighbor.Left + neighbor.Width / 2 < center)
                    left = Math.Max(left, (neighbor.Right + paper.Left) / 2);
                else right = Math.Min(right, (paper.Right + neighbor.Left) / 2);
            }
            return right > left ? new Rect(left, paper.Top, right - left, paper.Height) : Rect.Empty;
        }
        private Rect RectFor(WordRect rect)
        {
            Point origin = _canvas.PointFromScreen(new Point(rect.X, rect.Y));
            Point corner = _canvas.PointFromScreen(new Point(rect.X + rect.Width, rect.Y + rect.Height));
            return new Rect(origin, corner);
        }
    }
}
