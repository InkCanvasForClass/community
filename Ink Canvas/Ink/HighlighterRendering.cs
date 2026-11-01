using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.Ink
{
    /// <summary>
    /// Rendering policy independent of WPF's fixed 50% highlighter renderer. The source
    /// strokes and their ARGB attributes remain untouched for editing, history and ISF.
    /// </summary>
    internal static class HighlighterRendering
    {
        internal static readonly Guid ToolGuid = new("F84F2BD6-0D60-47CF-A621-36075F017321");

        internal static void SetTool(DrawingAttributes attributes, bool highlighter)
        {
            if (highlighter) attributes.AddPropertyData(ToolGuid, true);
            else if (attributes.ContainsPropertyData(ToolGuid)) attributes.RemovePropertyData(ToolGuid);
        }

        internal static bool IsHighlighter(Stroke stroke) => stroke.DrawingAttributes.IsHighlighter
            || (stroke.DrawingAttributes.ContainsPropertyData(ToolGuid)
                && stroke.DrawingAttributes.GetPropertyData(ToolGuid) is true);

        internal static double Opacity(byte alpha) => 0.5 * alpha / 255.0;

        internal static StrokeCollection CreatePresentation(StrokeCollection source)
            => new PresentationCache().Create(source);

        internal sealed class PresentationCache
        {
            private readonly Dictionary<Stroke, Geometry> _geometry = new();
            private Dictionary<(Color, Stroke), RenderStroke> _groups = new();
            private Dictionary<Stroke, (Color, Stroke)> _membership = new();
            private readonly HashSet<(Color, Stroke)> _dirtyGroups = new();
            internal void Invalidate(Stroke stroke)
            {
                _geometry.Remove(stroke);
                if (_membership.TryGetValue(stroke, out var key)) _dirtyGroups.Add(key);
            }
            internal void Clear() { _geometry.Clear(); _groups.Clear(); _membership.Clear(); _dirtyGroups.Clear(); }

            internal StrokeCollection Create(StrokeCollection source)
            {
                var result = new StrokeCollection();
                var live = source.ToHashSet();
                foreach (var old in _geometry.Keys.Where(s => !live.Contains(s)).ToArray()) _geometry.Remove(old);
                var next = new Dictionary<(Color, Stroke), RenderStroke>();
                var membership = new Dictionary<Stroke, (Color, Stroke)>();
                RenderStroke Group(Stroke[] strokes, Color color, Stroke individual = null)
                {
                    var geometry = strokes.Select(stroke =>
                    {
                        if (!_geometry.TryGetValue(stroke, out var shape))
                            _geometry[stroke] = shape = stroke.GetGeometry();
                        return shape;
                    }).ToArray();
                    var key = (color, individual);
                    foreach (var stroke in strokes) membership[stroke] = key;
                    if (!_groups.TryGetValue(key, out var cached))
                        cached = new RenderStroke(geometry, color);
                    else cached.Update(geometry, _dirtyGroups.Contains(key));
                    next[key] = cached;
                    return cached;
                }
                // Match WPF's ordering: highlighter groups underneath ordinary ink. Alpha is
                // part of the key so changing the slider never changes existing strokes.
                foreach (var group in source.Where(IsHighlighter).Where(s => s.DrawingAttributes.IsHighlighter)
                             .GroupBy(s => s.DrawingAttributes.Color))
                    result.Add(Group(group.ToArray(), group.Key));
                foreach (var stroke in source.Where(IsHighlighter).Where(s => !s.DrawingAttributes.IsHighlighter))
                    result.Add(Group(new[] { stroke }, stroke.DrawingAttributes.Color, stroke));
                foreach (var stroke in source.Where(s => !IsHighlighter(s))) result.Add(stroke);
                _groups = next;
                _membership = membership;
                _dirtyGroups.Clear();
                return result;
            }
        }

        internal static void Draw(DrawingContext context, StrokeCollection source)
        {
            // Exports draw once: they do not need retained per-canvas drawing nodes.
            foreach (var group in source.Where(IsHighlighter).Where(s => s.DrawingAttributes.IsHighlighter).GroupBy(s => s.DrawingAttributes.Color))
            {
                var brush = new SolidColorBrush(Color.FromRgb(group.Key.R, group.Key.G, group.Key.B)); brush.Freeze();
                context.PushOpacity(Opacity(group.Key.A));
                foreach (var stroke in group) context.DrawGeometry(brush, null, stroke.GetGeometry());
                context.Pop();
            }
            foreach (var stroke in source.Where(IsHighlighter).Where(s => !s.DrawingAttributes.IsHighlighter))
            {
                var color = stroke.DrawingAttributes.Color;
                var brush = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B)); brush.Freeze();
                context.PushOpacity(Opacity(color.A)); context.DrawGeometry(brush, null, stroke.GetGeometry()); context.Pop();
            }
            foreach (var stroke in source.Where(s => !IsHighlighter(s))) stroke.Draw(context);
        }

        // These strokes belong ONLY to the presenter's private collection. They must
        // never enter InkCanvas.Strokes, undo records, eraser hit testing or persistence.
        private sealed class RenderStroke : Stroke
        {
            private Geometry[] _geometry = Array.Empty<Geometry>();
            private readonly SolidColorBrush _brush;
            private readonly DrawingGroup _drawing = new();
            private Rect _bounds;

            internal bool Matches(Geometry[] geometry) => _geometry.SequenceEqual(geometry);

            internal RenderStroke(Geometry[] geometry, Color color)
                : base(new StylusPointCollection { new StylusPoint(0, 0) })
            {
                _brush = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B));
                _brush.Freeze();
                _drawing.Opacity = Opacity(color.A);
                Update(geometry, true);
            }

            internal void Update(Geometry[] geometry, bool force)
            {
                if (!force && Matches(geometry)) return;
                int prefix = 0, suffix = 0;
                while (prefix < _geometry.Length && prefix < geometry.Length && ReferenceEquals(_geometry[prefix], geometry[prefix])) prefix++;
                while (suffix < _geometry.Length - prefix && suffix < geometry.Length - prefix
                    && ReferenceEquals(_geometry[_geometry.Length - 1 - suffix], geometry[geometry.Length - 1 - suffix])) suffix++;
                for (int i = 0; i < _geometry.Length - prefix - suffix; i++) _drawing.Children.RemoveAt(prefix);
                for (int i = prefix; i < geometry.Length - suffix; i++)
                    _drawing.Children.Insert(i, new GeometryDrawing(_brush, null, geometry[i]));
                _geometry = geometry;
                _bounds = Rect.Empty;
                foreach (var shape in geometry) _bounds.Union(shape.Bounds);
                OnInvalidated(EventArgs.Empty);
            }

            public override Rect GetBounds() => _bounds;

            protected override void DrawCore(DrawingContext context, DrawingAttributes attributes)
            {
                // One opacity group for all same-colour non-overlapping highlighters;
                // separate groups for overlap mode. Drawing opaque geometry inside the
                // group avoids dark seams between segments of the same stroke.
                context.DrawDrawing(_drawing);
            }
        }
    }
}
