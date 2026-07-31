using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using Ink_Canvas.Mathematics.Models;
using Ink_Canvas.Mathematics.Services;

namespace Ink_Canvas.Mathematics.Rendering
{
    public sealed class MathStrokeRenderer
    {
        private static readonly object TextGeometryCacheLock = new object();
        private static readonly Dictionary<string, IReadOnlyList<IReadOnlyList<MathPoint>>> TextGeometryCache =
            new Dictionary<string, IReadOnlyList<IReadOnlyList<MathPoint>>>();
        private static readonly ConditionalWeakTable<FunctionObject, FunctionRenderCacheEntry> FunctionRenderCache =
            new ConditionalWeakTable<FunctionObject, FunctionRenderCacheEntry>();
        public static readonly Guid MathObjectIdProperty =
            new Guid("38F2E4F7-1DAA-46AD-9B48-208404DCB1C5");

        public static readonly Guid MathGeneratedStrokeProperty =
            new Guid("4977FD61-2DD6-462E-AB47-DC9A50356555");

        public StrokeCollection Render(MathScene scene, bool showMeasurements = true)
        {
            var strokes = new StrokeCollection();
            if (scene?.Objects == null) return strokes;

            for (var i = 0; i < scene.Objects.Count; i++)
            {
                var mathObject = scene.Objects[i];
                if (mathObject == null || !mathObject.IsVisible) continue;
                RenderObject(strokes, scene, mathObject, showMeasurements);
            }

            return strokes;
        }

        private static void RenderObject(
            StrokeCollection strokes,
            MathScene scene,
            MathObject mathObject,
            bool showMeasurements)
        {
            switch (mathObject)
            {
                case CoordinatePlaneObject coordinatePlane:
                    RenderCoordinatePlane(strokes, coordinatePlane);
                    break;
                case PointObject point:
                    AddEllipse(strokes, mathObject, point.Position, 4, 4);
                    if (showMeasurements)
                    {
                        var pointPlane = FindCoordinatePlane(scene, point.Position);
                        if (pointPlane != null)
                        {
                            var coordinate = ToGridCoordinate(point.Position, pointPlane);
                            AddText(
                                strokes,
                                mathObject,
                                $"({coordinate.X:0.##}, {coordinate.Y:0.##})",
                                new MathPoint(point.Position.X + 6, point.Position.Y + 6));
                        }
                    }
                    break;
                case SegmentObject segment:
                    AddLine(strokes, mathObject, segment.Start, segment.End);
                    if (showMeasurements)
                    {
                        var midpoint = new MathPoint(
                            (segment.Start.X + segment.End.X) / 2,
                            (segment.Start.Y + segment.End.Y) / 2);
                        var segmentPlane = FindCoordinatePlane(scene, midpoint);
                        AddText(
                            strokes,
                            mathObject,
                            MathCoordinateService.ToGridLength(
                                    MathMeasurementService.Distance(segment.Start, segment.End),
                                    segmentPlane?.GridSpacing ?? 40)
                                .ToString("0.##", CultureInfo.CurrentCulture),
                            new MathPoint(midpoint.X + 4, midpoint.Y + 4));
                    }
                    break;
                case TriangleObject triangle:
                    AddLine(strokes, mathObject, triangle.First, triangle.Second);
                    AddLine(strokes, mathObject, triangle.Second, triangle.Third);
                    AddLine(strokes, mathObject, triangle.Third, triangle.First);
                    break;
                case LineObject line:
                    AddExtendedLine(strokes, mathObject, line.Start, line.End, true);
                    break;
                case RayObject ray:
                    AddExtendedLine(strokes, mathObject, ray.Start, ray.Through, false);
                    break;
                case CircleObject circle:
                    AddEllipse(strokes, mathObject, circle.Center, circle.Radius, circle.Radius);
                    if (showMeasurements)
                    {
                        var circlePlane = FindCoordinatePlane(scene, circle.Center);
                        AddText(
                            strokes,
                            mathObject,
                            $"r={MathCoordinateService.ToGridLength(circle.Radius, circlePlane?.GridSpacing ?? 40):0.##}",
                            new MathPoint(circle.Center.X + circle.Radius + 4, circle.Center.Y));
                    }
                    break;
                case TextLabelObject label:
                    AddText(strokes, mathObject, label.Text, label.Position);
                    break;
                case AngleMeasurementObject angle:
                    AddLine(strokes, mathObject, angle.Vertex, angle.First);
                    AddLine(strokes, mathObject, angle.Vertex, angle.Second);
                    if (showMeasurements)
                    {
                        AddText(
                            strokes,
                            mathObject,
                            $"{MathMeasurementService.AngleDegrees(angle.First, angle.Vertex, angle.Second):0.##}°",
                            new MathPoint(angle.Vertex.X + 8, angle.Vertex.Y + 8));
                    }
                    break;
                case FunctionObject function:
                    RenderFunction(strokes, scene, function);
                    break;
                case SolidObject solid:
                    RenderSolid(strokes, solid);
                    break;
            }
        }

        private static void RenderCoordinatePlane(
            StrokeCollection strokes,
            CoordinatePlaneObject coordinatePlane)
        {
            var left = coordinatePlane.Center.X - coordinatePlane.Width / 2;
            var right = coordinatePlane.Center.X + coordinatePlane.Width / 2;
            var top = coordinatePlane.Center.Y - coordinatePlane.Height / 2;
            var bottom = coordinatePlane.Center.Y + coordinatePlane.Height / 2;

            if (coordinatePlane.ShowGrid)
            {
                for (var x = coordinatePlane.Center.X; x <= right; x += coordinatePlane.GridSpacing)
                    AddLine(strokes, coordinatePlane, new MathPoint(x, top), new MathPoint(x, bottom), "#40808080", 1);
                for (var x = coordinatePlane.Center.X - coordinatePlane.GridSpacing; x >= left; x -= coordinatePlane.GridSpacing)
                    AddLine(strokes, coordinatePlane, new MathPoint(x, top), new MathPoint(x, bottom), "#40808080", 1);
                for (var y = coordinatePlane.Center.Y; y <= bottom; y += coordinatePlane.GridSpacing)
                    AddLine(strokes, coordinatePlane, new MathPoint(left, y), new MathPoint(right, y), "#40808080", 1);
                for (var y = coordinatePlane.Center.Y - coordinatePlane.GridSpacing; y >= top; y -= coordinatePlane.GridSpacing)
                    AddLine(strokes, coordinatePlane, new MathPoint(left, y), new MathPoint(right, y), "#40808080", 1);
            }

            if (!coordinatePlane.ShowAxes) return;
            AddArrowLine(
                strokes,
                coordinatePlane,
                new MathPoint(left, coordinatePlane.Center.Y),
                new MathPoint(right, coordinatePlane.Center.Y));
            AddArrowLine(
                strokes,
                coordinatePlane,
                new MathPoint(coordinatePlane.Center.X, bottom),
                new MathPoint(coordinatePlane.Center.X, top));
        }

        private static void RenderFunction(
            StrokeCollection strokes,
            MathScene scene,
            FunctionObject function)
        {
            var signature = string.Join("|",
                CultureInfo.CurrentUICulture.Name,
                function.Expression,
                function.DomainMin.ToString("R", CultureInfo.InvariantCulture),
                function.DomainMax.ToString("R", CultureInfo.InvariantCulture),
                function.Origin.X.ToString("R", CultureInfo.InvariantCulture),
                function.Origin.Y.ToString("R", CultureInfo.InvariantCulture),
                function.PixelsPerUnit.ToString("R", CultureInfo.InvariantCulture),
                function.RotationDegrees.ToString("R", CultureInfo.InvariantCulture),
                function.SampleQuality,
                function.ShowZeros,
                function.ShowExtrema,
                function.ShowIntersections,
                function.StrokeColor,
                function.StrokeWidth.ToString("R", CultureInfo.InvariantCulture));
            if (function.ShowIntersections)
            {
                var functionIndex = scene.Objects.IndexOf(function);
                for (var i = functionIndex + 1; i < scene.Objects.Count; i++)
                {
                    if (scene.Objects[i] is not FunctionObject other) continue;
                    signature += string.Join("|next:",
                        other.Expression,
                        other.DomainMin.ToString("R", CultureInfo.InvariantCulture),
                        other.DomainMax.ToString("R", CultureInfo.InvariantCulture),
                        other.Origin.X.ToString("R", CultureInfo.InvariantCulture),
                        other.Origin.Y.ToString("R", CultureInfo.InvariantCulture),
                        other.PixelsPerUnit.ToString("R", CultureInfo.InvariantCulture),
                        other.RotationDegrees.ToString("R", CultureInfo.InvariantCulture),
                        other.IsVisible,
                        other.ShowIntersections);
                }
            }
            var cache = FunctionRenderCache.GetOrCreateValue(function);
            lock (cache)
            {
                if (!string.Equals(cache.Signature, signature, StringComparison.Ordinal))
                {
                    var rendered = new StrokeCollection();
                    RenderFunctionCore(rendered, scene, function);
                    cache.Signature = signature;
                    cache.Strokes = rendered;
                }
                foreach (var stroke in cache.Strokes)
                    strokes.Add(stroke);
            }
        }

        private static void RenderFunctionCore(
            StrokeCollection strokes,
            MathScene scene,
            FunctionObject function)
        {
            var sample = FunctionSamplingService.Sample(function);
            for (var segmentIndex = 0; segmentIndex < sample.Segments.Count; segmentIndex++)
            {
                var segment = sample.Segments[segmentIndex];
                var points = new List<MathPoint>(segment.Count);
                for (var i = 0; i < segment.Count; i++)
                {
                    var screenPoint = new MathPoint(
                        function.Origin.X + segment[i].X * function.PixelsPerUnit,
                        function.Origin.Y - segment[i].Y * function.PixelsPerUnit);
                    points.Add(RotateAround(screenPoint, function.Origin, function.RotationDegrees));
                }
                AddStroke(strokes, function, points);
            }

            if (function.ShowZeros)
            {
                for (var i = 0; i < sample.Zeros.Count; i++)
                    AddFunctionMarker(strokes, function, sample.Zeros[i]);
            }
            if (function.ShowExtrema)
            {
                for (var i = 0; i < sample.Extrema.Count; i++)
                    AddFunctionMarker(strokes, function, sample.Extrema[i]);
            }
            if (function.ShowIntersections)
            {
                var functionIndex = scene.Objects.IndexOf(function);
                for (var i = functionIndex + 1; i < scene.Objects.Count; i++)
                {
                    if (scene.Objects[i] is not FunctionObject other ||
                        !other.IsVisible ||
                        !other.ShowIntersections ||
                        !FunctionAnalysisService.ShareCoordinateFrame(function, other))
                        continue;
                    var intersections = FunctionAnalysisService.FindIntersections(function, other);
                    for (var pointIndex = 0; pointIndex < intersections.Count; pointIndex++)
                        AddFunctionMarker(strokes, function, intersections[pointIndex]);
                }
            }
        }

        private sealed class FunctionRenderCacheEntry
        {
            public string Signature { get; set; }

            public StrokeCollection Strokes { get; set; } = new StrokeCollection();
        }

        private static void AddFunctionMarker(
            StrokeCollection strokes,
            FunctionObject function,
            MathPoint point)
        {
            var screenPoint = new MathPoint(
                function.Origin.X + point.X * function.PixelsPerUnit,
                function.Origin.Y - point.Y * function.PixelsPerUnit);
            var rotated = RotateAround(screenPoint, function.Origin, function.RotationDegrees);
            AddEllipse(
                strokes,
                function,
                rotated,
                4,
                4);
        }

        private static void RenderSolid(StrokeCollection strokes, SolidObject solid)
        {
            if (solid.SolidType == SolidType.Sphere)
            {
                RenderSphere(strokes, solid);
                return;
            }
            if (solid.SolidType == SolidType.Cone)
            {
                RenderCone(strokes, solid);
                return;
            }

            var projection = SolidProjectionService.Project(solid);
            for (var i = 0; i < projection.Edges.Count; i++)
            {
                var edge = projection.Edges[i];
                if (Math.Abs(edge.Start.X - edge.End.X) < 0.001 &&
                    Math.Abs(edge.Start.Y - edge.End.Y) < 0.001)
                    continue;
                if (edge.IsHidden && !solid.ShowHiddenEdges) continue;
                if (edge.IsHidden)
                    AddDashedLine(strokes, solid, edge.Start, edge.End);
                else
                    AddLine(strokes, solid, edge.Start, edge.End);
            }
        }

        private static void RenderSphere(StrokeCollection strokes, SolidObject solid)
        {
            const int Steps = 64;
            var viewDirection = solid.ViewMode == SolidViewMode.Front
                ? new MathPoint3D(0, 0, 1)
                : Normalize(new MathPoint3D(-0.55, -0.35, 1));
            var firstBasis = Normalize(Cross(new MathPoint3D(0, 1, 0), viewDirection));
            var secondBasis = Normalize(Cross(viewDirection, firstBasis));
            var outline = new List<MathPoint>(Steps + 1);
            var equator = new List<MathPoint>(Steps + 1);
            var equatorDepths = new List<double>(Steps + 1);

            for (var i = 0; i <= Steps; i++)
            {
                var angle = Math.PI * 2 * i / Steps;
                var cosine = Math.Cos(angle) * solid.Radius;
                var sine = Math.Sin(angle) * solid.Radius;
                outline.Add(SolidProjectionService.ProjectWorldPoint(
                    solid,
                    new MathPoint3D(
                        firstBasis.X * cosine + secondBasis.X * sine,
                        firstBasis.Y * cosine + secondBasis.Y * sine,
                        firstBasis.Z * cosine + secondBasis.Z * sine)));
                var modelPoint = new MathPoint3D(cosine, 0, sine);
                equator.Add(SolidProjectionService.ProjectModelPoint(solid, modelPoint));
                equatorDepths.Add(SolidProjectionService.TransformModelPoint(solid, modelPoint).Z);
            }

            AddStroke(strokes, solid, outline);
            if (solid.ViewMode != SolidViewMode.Front)
                AddDepthSplitCurve(strokes, solid, equator, equatorDepths, 0);
        }

        private static void RenderCone(StrokeCollection strokes, SolidObject solid)
        {
            const int Steps = 64;
            var halfHeight = solid.Height / 2;
            var basePoints = new List<MathPoint>(Steps + 1);
            var baseDepths = new List<double>(Steps + 1);
            for (var i = 0; i <= Steps; i++)
            {
                var angle = Math.PI * 2 * i / Steps;
                var modelPoint = new MathPoint3D(
                    solid.Radius * Math.Cos(angle),
                    -halfHeight,
                    solid.Radius * Math.Sin(angle));
                basePoints.Add(SolidProjectionService.ProjectModelPoint(solid, modelPoint));
                baseDepths.Add(SolidProjectionService.TransformModelPoint(solid, modelPoint).Z);
            }

            var apex = SolidProjectionService.ProjectModelPoint(
                solid,
                new MathPoint3D(0, halfHeight, 0));
            var tangents = FindTangentIndices(apex, basePoints, Steps);
            AddLine(strokes, solid, apex, basePoints[tangents.Item1]);
            AddLine(strokes, solid, apex, basePoints[tangents.Item2]);

            var baseCenterDepth = SolidProjectionService.TransformModelPoint(
                solid,
                new MathPoint3D(0, -halfHeight, 0)).Z;
            AddDepthSplitCurve(strokes, solid, basePoints, baseDepths, baseCenterDepth);
        }

        private static Tuple<int, int> FindTangentIndices(
            MathPoint apex,
            IReadOnlyList<MathPoint> loop,
            int count)
        {
            var candidates = new List<int>();
            const double Epsilon = 0.001;
            for (var i = 0; i < count; i++)
            {
                var directionX = loop[i].X - apex.X;
                var directionY = loop[i].Y - apex.Y;
                var hasPositive = false;
                var hasNegative = false;
                for (var j = 0; j < count; j++)
                {
                    var cross = directionX * (loop[j].Y - apex.Y) -
                                directionY * (loop[j].X - apex.X);
                    if (cross > Epsilon) hasPositive = true;
                    if (cross < -Epsilon) hasNegative = true;
                    if (hasPositive && hasNegative) break;
                }
                if (!hasPositive || !hasNegative)
                    candidates.Add(i);
            }

            if (candidates.Count >= 2)
                return Tuple.Create(candidates[0], candidates[candidates.Count - 1]);

            var left = 0;
            var right = 0;
            for (var i = 1; i < count; i++)
            {
                if (loop[i].X < loop[left].X) left = i;
                if (loop[i].X > loop[right].X) right = i;
            }
            return Tuple.Create(left, right);
        }

        private static void AddDepthSplitCurve(
            StrokeCollection strokes,
            MathObject mathObject,
            IReadOnlyList<MathPoint> points,
            IReadOnlyList<double> depths,
            double centerDepth)
        {
            var visibleRun = new List<MathPoint>();
            for (var i = 0; i < points.Count - 1; i++)
            {
                var hidden = (depths[i] + depths[i + 1]) / 2 > centerDepth;
                if (hidden)
                {
                    if (visibleRun.Count > 1)
                        AddStroke(strokes, mathObject, visibleRun);
                    visibleRun.Clear();
                    AddDashedLine(strokes, mathObject, points[i], points[i + 1]);
                }
                else
                {
                    if (visibleRun.Count == 0)
                        visibleRun.Add(points[i]);
                    visibleRun.Add(points[i + 1]);
                }
            }
            if (visibleRun.Count > 1)
                AddStroke(strokes, mathObject, visibleRun);
        }

        private static MathPoint3D Normalize(MathPoint3D point)
        {
            var length = Math.Sqrt(point.X * point.X + point.Y * point.Y + point.Z * point.Z);
            return length <= double.Epsilon
                ? new MathPoint3D(1, 0, 0)
                : new MathPoint3D(point.X / length, point.Y / length, point.Z / length);
        }

        private static MathPoint3D Cross(MathPoint3D first, MathPoint3D second)
        {
            return new MathPoint3D(
                first.Y * second.Z - first.Z * second.Y,
                first.Z * second.X - first.X * second.Z,
                first.X * second.Y - first.Y * second.X);
        }

        private static void AddArrowLine(
            StrokeCollection strokes,
            MathObject mathObject,
            MathPoint start,
            MathPoint end)
        {
            AddLine(strokes, mathObject, start, end);
            var angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
            const double ArrowLength = 12;
            const double ArrowAngle = Math.PI / 7;
            AddLine(
                strokes,
                mathObject,
                end,
                new MathPoint(
                    end.X - ArrowLength * Math.Cos(angle - ArrowAngle),
                    end.Y - ArrowLength * Math.Sin(angle - ArrowAngle)));
            AddLine(
                strokes,
                mathObject,
                end,
                new MathPoint(
                    end.X - ArrowLength * Math.Cos(angle + ArrowAngle),
                    end.Y - ArrowLength * Math.Sin(angle + ArrowAngle)));
        }

        private static void AddExtendedLine(
            StrokeCollection strokes,
            MathObject mathObject,
            MathPoint start,
            MathPoint through,
            bool extendBackward)
        {
            var deltaX = through.X - start.X;
            var deltaY = through.Y - start.Y;
            var length = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            if (length <= double.Epsilon) return;

            const double Extension = 10000;
            var unitX = deltaX / length;
            var unitY = deltaY / length;
            var from = extendBackward
                ? new MathPoint(start.X - unitX * Extension, start.Y - unitY * Extension)
                : start;
            var to = new MathPoint(start.X + unitX * Extension, start.Y + unitY * Extension);
            AddLine(strokes, mathObject, from, to);
        }

        private static void AddDashedLine(
            StrokeCollection strokes,
            MathObject mathObject,
            MathPoint start,
            MathPoint end)
        {
            var deltaX = end.X - start.X;
            var deltaY = end.Y - start.Y;
            var length = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            if (length <= double.Epsilon) return;

            const double DashLength = 7;
            const double GapLength = 5;
            for (var offset = 0d; offset < length; offset += DashLength + GapLength)
            {
                var dashEnd = Math.Min(length, offset + DashLength);
                AddLine(
                    strokes,
                    mathObject,
                    new MathPoint(start.X + deltaX * offset / length, start.Y + deltaY * offset / length),
                    new MathPoint(start.X + deltaX * dashEnd / length, start.Y + deltaY * dashEnd / length));
            }
        }

        private static void AddEllipse(
            StrokeCollection strokes,
            MathObject mathObject,
            MathPoint center,
            double radiusX,
            double radiusY)
        {
            const int Steps = 48;
            var points = new List<MathPoint>(Steps + 1);
            for (var i = 0; i <= Steps; i++)
            {
                var angle = Math.PI * 2 * i / Steps;
                points.Add(new MathPoint(
                    center.X + radiusX * Math.Cos(angle),
                    center.Y + radiusY * Math.Sin(angle)));
            }
            AddStroke(strokes, mathObject, points);
        }

        private static void AddText(
            StrokeCollection strokes,
            MathObject mathObject,
            string text,
            MathPoint position)
        {
            if (string.IsNullOrEmpty(text)) return;
            var figures = GetTextGeometry(text);
            for (var figureIndex = 0; figureIndex < figures.Count; figureIndex++)
            {
                var source = figures[figureIndex];
                var points = new List<MathPoint>(source.Count);
                for (var pointIndex = 0; pointIndex < source.Count; pointIndex++)
                    points.Add(new MathPoint(
                        source[pointIndex].X + position.X,
                        source[pointIndex].Y + position.Y));
                AddStroke(strokes, mathObject, points);
            }
        }

        private static IReadOnlyList<IReadOnlyList<MathPoint>> GetTextGeometry(string text)
        {
            var key = CultureInfo.CurrentUICulture.Name + "\n" + text;
            lock (TextGeometryCacheLock)
            {
                if (TextGeometryCache.TryGetValue(key, out var cached)) return cached;
                var formattedText = new FormattedText(
                    text,
                    CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Microsoft YaHei UI"),
                    16,
                    Brushes.Black,
                    1);
                var geometry = formattedText
                    .BuildGeometry(new Point(0, 0))
                    .GetFlattenedPathGeometry(0.8, ToleranceType.Absolute);
                var result = new List<IReadOnlyList<MathPoint>>(geometry.Figures.Count);
                for (var figureIndex = 0; figureIndex < geometry.Figures.Count; figureIndex++)
                {
                    var figure = geometry.Figures[figureIndex];
                    var points = new List<MathPoint> { new MathPoint(figure.StartPoint.X, figure.StartPoint.Y) };
                    for (var segmentIndex = 0; segmentIndex < figure.Segments.Count; segmentIndex++)
                    {
                        if (figure.Segments[segmentIndex] is PolyLineSegment polyLine)
                        {
                            for (var pointIndex = 0; pointIndex < polyLine.Points.Count; pointIndex++)
                                points.Add(new MathPoint(polyLine.Points[pointIndex].X, polyLine.Points[pointIndex].Y));
                        }
                        else if (figure.Segments[segmentIndex] is LineSegment line)
                        {
                            points.Add(new MathPoint(line.Point.X, line.Point.Y));
                        }
                    }
                    if (figure.IsClosed && points.Count > 1) points.Add(points[0]);
                    result.Add(points);
                }
                if (TextGeometryCache.Count >= 512) TextGeometryCache.Clear();
                TextGeometryCache[key] = result;
                return result;
            }
        }

        private static void AddLine(
            StrokeCollection strokes,
            MathObject mathObject,
            MathPoint start,
            MathPoint end,
            string color = null,
            double? width = null)
        {
            AddStroke(strokes, mathObject, new[] { start, end }, color, width);
        }

        private static void AddStroke(
            StrokeCollection strokes,
            MathObject mathObject,
            IReadOnlyList<MathPoint> points,
            string color = null,
            double? width = null)
        {
            if (points == null || points.Count < 2) return;
            var stylusPoints = new StylusPointCollection(points.Count);
            for (var i = 0; i < points.Count; i++)
            {
                if (!double.IsFinite(points[i].X) || !double.IsFinite(points[i].Y)) continue;
                stylusPoints.Add(new StylusPoint(points[i].X, points[i].Y));
            }
            if (stylusPoints.Count < 2) return;

            var stroke = new Stroke(stylusPoints)
            {
                DrawingAttributes = CreateDrawingAttributes(
                    color ?? mathObject.StrokeColor,
                    width ?? mathObject.StrokeWidth)
            };
            stroke.AddPropertyData(MathGeneratedStrokeProperty, true);
            stroke.AddPropertyData(MathObjectIdProperty, mathObject.Id.ToString("D"));
            strokes.Add(stroke);
        }

        private static DrawingAttributes CreateDrawingAttributes(string value, double width)
        {
            var color = Colors.Black;
            try
            {
                if (ColorConverter.ConvertFromString(value) is Color parsed)
                    color = parsed;
            }
            catch (FormatException)
            {
            }

            return new DrawingAttributes
            {
                Color = color,
                Width = Math.Max(1, width),
                Height = Math.Max(1, width),
                IgnorePressure = true,
                FitToCurve = false,
                StylusTip = StylusTip.Ellipse
            };
        }

        private static CoordinatePlaneObject FindCoordinatePlane(MathScene scene, MathPoint point)
        {
            if (scene?.Objects == null) return null;
            for (var i = scene.Objects.Count - 1; i >= 0; i--)
            {
                if (scene.Objects[i] is not CoordinatePlaneObject plane) continue;
                if (Math.Abs(point.X - plane.Center.X) <= plane.Width / 2 &&
                    Math.Abs(point.Y - plane.Center.Y) <= plane.Height / 2)
                    return plane;
            }
            return null;
        }

        private static MathPoint ToGridCoordinate(MathPoint point, CoordinatePlaneObject plane)
        {
            return new MathPoint(
                (point.X - plane.Center.X) / plane.GridSpacing,
                (plane.Center.Y - point.Y) / plane.GridSpacing);
        }

        private static MathPoint RotateAround(MathPoint point, MathPoint center, double degrees)
        {
            if (Math.Abs(degrees) <= double.Epsilon) return point;
            var radians = degrees * Math.PI / 180;
            var cosine = Math.Cos(radians);
            var sine = Math.Sin(radians);
            var deltaX = point.X - center.X;
            var deltaY = point.Y - center.Y;
            return new MathPoint(
                center.X + deltaX * cosine - deltaY * sine,
                center.Y + deltaX * sine + deltaY * cosine);
        }
    }
}
