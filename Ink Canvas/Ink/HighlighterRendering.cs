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
            internal void Invalidate(Stroke stroke)
            {
                _geometry.Remove(stroke);
                if (_membership.TryGetValue(stroke, out var key)) _groups.Remove(key);
            }
            internal void Clear() { _geometry.Clear(); _groups.Clear(); _membership.Clear(); }

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
                    if (!_groups.TryGetValue(key, out var cached) || !cached.Matches(geometry))
                        cached = new RenderStroke(geometry, color);
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
                return result;
            }
        }

        internal static void Draw(DrawingContext context, StrokeCollection source)
        {
            foreach (var stroke in CreatePresentation(source)) stroke.Draw(context);
        }

        // These strokes belong ONLY to the presenter's private collection. They must
        // never enter InkCanvas.Strokes, undo records, eraser hit testing or persistence.
        private sealed class RenderStroke : Stroke
        {
            private readonly Geometry[] _geometry;
            private readonly SolidColorBrush _brush;
            private readonly double _opacity;
            private readonly Rect _bounds;

            internal bool Matches(Geometry[] geometry) => _geometry.SequenceEqual(geometry);

            internal RenderStroke(Geometry[] geometry, Color color)
                : base(new StylusPointCollection { new StylusPoint(0, 0) })
            {
                _geometry = geometry;
                _bounds = Rect.Empty;
                foreach (var shape in _geometry) _bounds.Union(shape.Bounds);
                _brush = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B));
                _brush.Freeze();
                _opacity = Opacity(color.A);
            }

            public override Rect GetBounds() => _bounds;

            protected override void DrawCore(DrawingContext context, DrawingAttributes attributes)
            {
                // One opacity group for all same-colour non-overlapping highlighters;
                // separate groups for overlap mode. Drawing opaque geometry inside the
                // group avoids dark seams between segments of the same stroke.
                context.PushOpacity(_opacity);
                foreach (var geometry in _geometry) context.DrawGeometry(_brush, null, geometry);
                context.Pop();
            }
        }
    }
}
