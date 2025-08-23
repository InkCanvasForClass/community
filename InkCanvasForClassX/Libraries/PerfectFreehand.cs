using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using InkCanvasForClassX.Libraries;
using InkCanvasForClassX.Libraries.Stroke;

namespace InkCanvasForClassX.Libraries
{
    /// <summary>
    /// 提供對JS庫<c>steveruizok/perfect-freehand</c>的C#包裝
    /// </summary>
    public class PerfectFreehand {

        private static double Average(double a, double b) {
            return (a + b) / 2;
        }

        public static string ConvertVectorsToSVGPath(Vector[] points, bool closed = true)
        {
            int len = points.Length;

            if (len < 4)
            {
                return string.Empty;
            }

            Vector a = points[0];
            Vector b = points[1];
            Vector c = points[2];

            StringBuilder result = new StringBuilder();
            result.AppendFormat("M{0:F2},{1:F2} Q{2:F2},{3:F2} {4:F2},{5:F2} T",
                a.X, a.Y, b.X, b.Y, Average(b.X, c.X), Average(b.Y, c.Y));

            for (int i = 2, max = len - 1; i < max; i++)
            {
                a = points[i];
                b = points[i + 1];
                result.AppendFormat("{0:F2},{1:F2} ", Average(a.X, b.X), Average(a.Y, b.Y));
            }

            if (closed)
            {
                result.Append("Z");
            }

            return result.ToString();
        }

        /// <summary>
        /// Get an array of points as objects with an adjusted point, pressure, vector, distance, and runningLength.
        /// </summary>
        /// <param name="points">原注釋： An array of points (as `[x, y, pressure]` or `{x, y, pressure}`). Pressure is optional in both cases. 請使用<c>StylusPointCollection</c></param>
        /// <param name="options">An object with options.</param>
        /// <returns></returns>
        public static StrokePoint[] GetStrokePoints(StylusPointCollection points,
            StrokeOptions options) {
            var streamline = options.Streamline ?? 0.5;
            var size = options.Size ?? 16;
            var isComplete = options.Last ?? false;

            // If we don't have any points, return an empty array.
            if (points.Count == 0) return Array.Empty<StrokePoint>();

            // Find the interpolation level between points.
            double t = 0.15 + (1 - streamline) * 0.85;

            // Purify the StylusPointCollection
            var pts = new StylusPointCollection();
            foreach (var stylusPoint in points) {
                pts.Add(new StylusPoint(stylusPoint.X, stylusPoint.Y, stylusPoint.PressureFactor));
            }

            // Add extra points between the two, to help avoid "dash" lines
            // for strokes with tapered start and ends. Don't mutate the
            // input array!
            if (points.Count == 2) {
                var last = pts[1];
                pts.RemoveAt(pts.Count - 1);
                for (var i = 1; i < 5; i++) {
                    var _vec = Vector.InterpolateVectors(
                        new Vector(pts[0].X, pts[0].Y),
                        new Vector(last.X, last.Y),
                        i / 4
                    );
                    pts.Add(new StylusPoint(_vec.X, _vec.Y));
                }
            }

            // If there's only one point, add another point at a 1pt offset.
            // Don't mutate the input array!
            if (pts.Count == 1) {
                var onePt = new Vector(pts[0].X + 1, pts[0].Y + 1);
                pts.Add(new StylusPoint(onePt.X, onePt.Y, pts[0].PressureFactor));
            }

            // The strokePoints array will hold the points for the stroke.
            // Start it out with the first point, which needs no adjustment.
            var strokePoints = new List<StrokePoint>() {
                new StrokePoint() {
                    Point = new Vector(pts[0].X, pts[0].Y),
                    Pressure = pts[0].PressureFactor >= 0 ? pts[0].PressureFactor : 0.25,
                    Vector = new Vector(1, 1),
                    Distance = 0,
                    RunningLength = 0,
                }
            };

            // A flag to see whether we've already reached out minimum length
            var hasReachedMinimumLength = false;

            // We use the runningLength to keep track of the total distance
            double runningLength = 0;

            // We're set this to the latest point, so we can use it to calculate
            // the distance and vector of the next point.
            var prev = strokePoints[0];

            // const max = pts.length - 1
            var max = pts.Count - 1;

            // Iterate through all of the points, creating StrokePoints.
            for (var i = 1; i < pts.Count; i++) {
                var point = isComplete && i == max
                    ? // If we're at the last point, and `options.last` is true,
                    // then add the actual input point.
                    new Vector(pts[i].X, pts[i].Y)
                    : // Otherwise, using the t calculated from the streamline
                    // option, interpolate a new point between the previous
                    // point the current point.
                    Vector.InterpolateVectors(prev.Point, new Vector(pts[i].X, pts[i].Y), t);

                // If the new point is the same as the previous point, skip ahead.
                if (prev.Point.IsEqual(point)) continue;

                // How far is the new point from the previous point?
                var distance = Vector.DistLengthVectors(point, prev.Point);

                // Add this distance to the total "running length" of the line.
                runningLength += distance;

                // At the start of the line, we wait until the new point is a
                // certain distance away from the original point, to avoid noise
                if (i < max && !hasReachedMinimumLength) {
                    if (runningLength < size) continue;
                    hasReachedMinimumLength = true;
                    // TODO: Backfill the missing points so that tapering works correctly.
                }

                // Create a new strokepoint (it will be the new "previous" one).
                prev = new StrokePoint() {
                    // The adjusted point
                    Point = point,
                    // The input pressure (or .5 if not specified)
                    Pressure = pts[i].PressureFactor >= 0 ? pts[i].PressureFactor : 0.5,
                    // The vector from the current point to the previous point
                    Vector = Vector.UnitVector(Vector.SubtractVectors(prev.Point, point)),
                    // The distance between the current point and the previous point
                    Distance = distance,
                    // The total distance so far
                    RunningLength = runningLength,
                };

                // Push it to the strokePoints array.
                strokePoints.Add(prev);
            }

            // Set the vector of the first point to be the same as the second point.
            strokePoints[0].Vector = strokePoints[1]?.Vector ?? new Vector(0, 0);

            return strokePoints.ToArray();
        }

        /// <summary>
        /// Compute a radius based on the pressure.
        /// </summary>
        public static double GetStrokeRadius(
            double size,
            double thinning,
            double pressure,
            Func<double, double> easing = null)
        {
            if (easing == null) {
                easing = t => t; // 默認的 easing 函數
            }
            return size * easing(0.5 - thinning * (0.5 - pressure));
        }

        // This is the rate of change for simulated pressure. It could be an option.
        private const double RATE_OF_PRESSURE_CHANGE = 0.275;

        private const double PI = Math.PI;
        private const double FIXED_PI = PI + 0.0001;

        /// <summary>
        /// Get an array of points (as `[x, y]`) representing the outline of a stroke.
        /// </summary>
        /// <param name="points">An array of StrokePoints as returned from `getStrokePoints`.</param>
        /// <param name="options">An object with options.</param>
        /// <returns></returns>
        public static Vector[] GetStrokeOutlinePointsVectors(StrokePoint[] points, StrokeOptions options) {
            var strokeOptions_Size = options.Size ?? 16;
            var strokeOptions_Thinning = options.Thinning ?? 0.5;
            var strokeOptions_Smoothing = options.Smoothing ?? 0.5;
            var strokeOptions_SimulatePressure = options.SimulatePressure ?? true;
            Func<double,double> strokeOptions_Easing = (t) => t;
            var strokeOptions_Start = options.Start;
            var strokeOptions_End = options.End;
            var isComplete = options.Last ?? false;

            var capStart = strokeOptions_Start != null ? strokeOptions_Start.Cap : true;
            Func<double, double> taperStartEase = strokeOptions_Start != null
                ? strokeOptions_Start.Easing
                : (s) => s * (2 - s);

            var capEnd = strokeOptions_End != null ? strokeOptions_End.Cap : true;
            Func<double, double> taperEndEase = strokeOptions_End != null
                ? strokeOptions_End.Easing
                : (t) => --t * t * t + 1;

            // We can't do anything with an empty array or a stroke with negative size.
            if (points.Length == 0 || strokeOptions_Size <= 0) {
                return new Vector[] { };
            }

            // The total length of the line
            var totalLength = points[points.Length - 1].RunningLength;

            var taperStart = strokeOptions_Start != null ? strokeOptions_Start.IsTaper == false ? 0 :
                strokeOptions_Start.IsTaper ? Math.Max(strokeOptions_Size, totalLength) :
                strokeOptions_Start.Taper != null ? (double)strokeOptions_Start.Taper : 0 : Double.NaN;

            var taperEnd = strokeOptions_End != null ? strokeOptions_End.IsTaper == false ? 0 :
                strokeOptions_End.IsTaper ? Math.Max(strokeOptions_Size, totalLength) :
                strokeOptions_End.Taper != null ? (double)strokeOptions_End.Taper : 0 : Double.NaN;

            // The minimum allowed distance between points (squared)
            var minDistance = Math.Pow(strokeOptions_Size * strokeOptions_Smoothing, 2);

            // Our collected left and right points
            var leftPts = new List<Vector>();
            var rightPts = new List<Vector>();

            // Previous pressure (start with average of first five pressures,
            // in order to prevent fat starts for every line. Drawn lines
            // almost always start slow!
            var _prevPressure_arrseg = new ArraySegment<StrokePoint>(points, 0, 10);
            var prevPressure = _prevPressure_arrseg.Aggregate(points[0].Pressure,
                (acc, curr) => {
                    var pressure = curr.Pressure;

                    if (strokeOptions_SimulatePressure) {
                        // Speed of change - how fast should the the pressure changing?
                        var sp = Math.Min(1, curr.Distance / strokeOptions_Size);
                        // Rate of change - how much of a change is there?
                        var rp = Math.Min(1, 1 - sp);
                        pressure = Math.Min(1, acc + (rp - acc) * (sp * RATE_OF_PRESSURE_CHANGE));
                    }

                    return (acc + pressure) / 2;
                });

            // The current radius
            var radius = GetStrokeRadius(strokeOptions_Size, strokeOptions_Thinning, points[points.Length - 1].Pressure,
                strokeOptions_Easing);

            // The radius of the first saved point
            double firstRadius = Double.NaN;

            // Previous vector
            var prevVector = points[0].Vector;

            // Previous left and right points
            var pl = points[0].Point;
            var pr = pl;

            // Temporary left and right points
            var tl = pl;
            var tr = pr;

            // Keep track of whether the previous point is a sharp corner
            // ... so that we don't detect the same corner twice
            var isPrevPointSharpCorner = false;

            /*
                Find the outline's left and right points

                Iterating through the points and populate the rightPts and leftPts arrays,
                skipping the first and last pointsm, which will get caps later on.
            */
            foreach (var _sp in points) {
                var pressure = _sp.Pressure;
                var point = _sp.Point;
                var vector = _sp.Vector;
                var distance = _sp.Distance;
                var runningLength = _sp.RunningLength;

                var i = Array.IndexOf<StrokePoint>(points, _sp);

                // Removes noise from the end of the line
                if (i < points.Length - 1 && totalLength - runningLength < 3) {
                    continue;
                }

                /*
                    Calculate the radius

                    If not thinning, the current point's radius will be half the size; or
                    otherwise, the size will be based on the current (real or simulated)
                    pressure.
                */
                if (strokeOptions_Thinning == Double.NaN) {
                    if (strokeOptions_SimulatePressure) {
                        // If we're simulating pressure, then do so based on the distance
                        // between the current point and the previous point, and the size
                        // of the stroke. Otherwise, use the input pressure.
                        var sp = Math.Min(1, distance / strokeOptions_Size);
                        var rp = Math.Min(1, 1 - sp);
                        pressure = Math.Min(1, prevPressure + (rp - prevPressure) * (sp * RATE_OF_PRESSURE_CHANGE));
                    }

                    radius = GetStrokeRadius(strokeOptions_Size, strokeOptions_Thinning, pressure,
                        strokeOptions_Easing);
                } else {
                    radius = strokeOptions_Size / 2;
                }

                if (firstRadius == Double.NaN) firstRadius = radius;

                /*
                    Apply tapering

                    If the current length is within the taper distance at either the
                    start or the end, calculate the taper strengths. Apply the smaller
                    of the two taper strengths to the radius.
                */
                var ts = runningLength < taperStart ? taperStartEase(runningLength / taperStart) : 1;
                var te = runningLength < taperEnd ? taperEndEase(runningLength / taperEnd) : 1;

                radius = Math.Max(0.01, radius * Math.Min(ts, te));

                /* Add points to left and right */

                /*
                    Handle sharp corners

                    Find the difference (dot product) between the current and next vector.
                    If the next vector is at more than a right angle to the current vector,
                    draw a cap at the current point.
                */

                var nextVector = (i < points.Length - 1 ? points[i + 1] : points[i]).Vector;
                var nextDpr = i < points.Length - 1 ? Vector.DotVectors(vector, nextVector) : 1.0;
                var prevDpr = Vector.DotVectors(vector, prevVector);

                var isPointSharpCorner = prevDpr < 0 && !isPrevPointSharpCorner;
                var isNextPointSharpCorner = nextDpr < 0;

                if (isPointSharpCorner || isNextPointSharpCorner) {
                    // It's a sharp corner. Draw a rounded cap and move on to the next point
                    // Considering saving these and drawing them later? So that we can avoid
                    // crossing future points.

                    var offset = Vector.MultiplyVector(Vector.PerpendicularRotationVector(prevVector), radius);

                    double step = 1D / 13D;
                    double t = 0D;
                    for (; t <= 1D; t += step) {
                        tl = Vector.RotateVectors(Vector.SubtractVectors(point, offset), point, FIXED_PI * t);
                        leftPts.Add(tl);

                        tr = Vector.RotateVectors(Vector.AddVectors(point, offset), point, FIXED_PI * -t);
                        rightPts.Add(tr);
                    }

                    pl = tl;
                    pr = tr;

                    if (isNextPointSharpCorner) {
                        isPrevPointSharpCorner = true;
                    }
                    continue;
                }

                isPrevPointSharpCorner = false;

                // Handle the last point
                if (i == points.Length - 1) {
                    var offset = Vector.MultiplyVector(Vector.PerpendicularRotationVector(vector), radius);
                    leftPts.Add(Vector.SubtractVectors(point, offset));
                    rightPts.Add(Vector.AddVectors(point, offset));
                    continue;
                }

                /*
                    Add regular points

                    Project points to either side of the current point, using the
                    calculated size as a distance. If a point's distance to the
                    previous point on that side greater than the minimum distance
                    (or if the corner is kinda sharp), add the points to the side's
                    points array.
                */
                Vector _offset =
                    Vector.MultiplyVector(
                        Vector.PerpendicularRotationVector(Vector.InterpolateVectors(nextVector, vector, nextDpr)),
                        radius);

                tl = Vector.SubtractVectors(point, _offset);

                if (i <= 1 || Vector.DistLengthSquaredVectors(pl, tl) > minDistance) {
                    leftPts.Add(tl);
                    pl = tl;
                }

                tr = Vector.AddVectors(point, _offset);

                if (i <= 1 || Vector.DistLengthSquaredVectors(pr, tr) > minDistance) {
                    rightPts.Add(tr);
                    pr = tr;
                }

                // Set variables for next iteration
                prevPressure = pressure;
                prevVector = vector;
            }

            /*
                Drawing caps

                Now that we have our points on either side of the line, we need to
                draw caps at the start and end. Tapered lines don't have caps, but
                may have dots for very short lines.
            */
            var firstPoint = points[0].Point;

            var lastPoint = points.Length > 1
                ? points[points.Length - 1].Point
                : Vector.AddVectors(points[0].Point, new Vector(1, 1));

            var startCap = new List<Vector>();
            var endCap = new List<Vector>();

            /*
                Draw a dot for very short or completed strokes

                If the line is too short to gather left or right points and if the line is
                not tapered on either side, draw a dot. If the line is tapered, then only
                draw a dot if the line is both very short and complete. If we draw a dot,
                we can just return those points.
            */
            if (points.Length == 1) {
                if (!((strokeOptions_Start != null && (taperStart != 0 && strokeOptions_Start.IsTaper)) || (strokeOptions_End != null &&
                        (taperEnd != 0 && strokeOptions_End.IsTaper))) || isComplete) {
                    var start = Vector.ProjectVectors(firstPoint,
                        Vector.UnitVector(
                            Vector.PerpendicularRotationVector(Vector.SubtractVectors(firstPoint, lastPoint))), !double.IsNaN(firstRadius) ? -firstRadius : -radius);
                    var dotPts = new List<Vector>();
                    double step = 1D / 13D;
                    double t = step;
                    for (; t <= 1D; t += step) {
                        dotPts.Add(Vector.RotateVectors(start, firstPoint, FIXED_PI * 2 * t));
                    }

                    return dotPts.ToArray();
                }
            } else {
                /*
                    Draw a start cap

                    Unless the line has a tapered start, or unless the line has a tapered end
                    and the line is very short, draw a start cap around the first point. Use
                    the distance between the second left and right point for the cap's radius.
                    Finally remove the first left and right points. :psyduck:
                */

                if ((strokeOptions_Start != null && (taperStart != 0 && strokeOptions_Start.IsTaper)) || ((strokeOptions_End != null &&
                        (taperEnd != 0 && strokeOptions_End.IsTaper)) && points.Length == 1)) {
                    // The start point is tapered, noop
                } else if (capStart) {
                    // Draw the round cap - add thirteen points rotating the right point around the start point to the left point
                    double step = 1D / 13D;
                    double t = step;
                    for (; t <= 1D; t += step) {
                        var pt = Vector.RotateVectors(rightPts[0], firstPoint, FIXED_PI * t);
                        startCap.Add(pt);
                    }
                } else {
                    // Draw the flat cap - add a point to the left and right of the start point
                    var cornersVector = Vector.SubtractVectors(leftPts[0], rightPts[0]);
                    var offsetA = Vector.MultiplyVector(cornersVector, 0.5);
                    var offsetB = Vector.MultiplyVector(cornersVector, 0.51);

                    startCap.Add(Vector.SubtractVectors(firstPoint, offsetA));
                    startCap.Add(Vector.SubtractVectors(firstPoint, offsetB));
                    startCap.Add(Vector.AddVectors(firstPoint, offsetA));
                    startCap.Add(Vector.AddVectors(firstPoint, offsetB));
                }

                /*
                    Draw an end cap

                    If the line does not have a tapered end, and unless the line has a tapered
                    start and the line is very short, draw a cap around the last point. Finally,
                    remove the last left and right points. Otherwise, add the last point. Note
                    that This cap is a full-turn-and-a-half: this prevents incorrect caps on
                    sharp end turns.
                */
                var direction =
                    Vector.PerpendicularRotationVector(Vector.NegateVector(points[points.Length - 1].Vector));

                if ((strokeOptions_End != null &&
                     (taperEnd != 0 && strokeOptions_End.IsTaper)) ||
                    ((strokeOptions_Start != null && (taperStart != 0 && strokeOptions_Start.IsTaper)) && points.Length == 1)) {
                    // Tapered end - push the last point to the line
                    endCap.Add(lastPoint);
                } else if (capEnd) {
                    // Draw the round end cap
                    var start = Vector.ProjectVectors(lastPoint, direction, radius);
                    double step = 1D / 29D;
                    double t = step;
                    for (; t < 1D; t += step) {
                        endCap.Add(Libraries.Vector.RotateVectors(start, lastPoint, FIXED_PI * 3 * t));
                    }
                } else {
                    // Draw the flat end cap
                    endCap.Add(Vector.AddVectors(lastPoint, Vector.MultiplyVector(direction, radius)));
                    endCap.Add(Vector.AddVectors(lastPoint, Vector.MultiplyVector(direction, radius * 0.99)));
                    endCap.Add(Vector.SubtractVectors(lastPoint, Vector.MultiplyVector(direction, radius)));
                    endCap.Add(Vector.SubtractVectors(lastPoint, Vector.MultiplyVector(direction, radius * 0.99)));
                }
            }

            /*
                Return the points in the correct winding order: begin on the left side, then
                continue around the end cap, then come back along the right side, and finally
                complete the start cap.
            */
            rightPts.Reverse();
            return leftPts.Concat(endCap).Concat(rightPts).Concat(startCap).ToArray();
        }
    }

    namespace Stroke {
        public class StrokeOptions
        {
            /// <summary>
            /// The base size (diameter) of the stroke.
            /// </summary>
            public double? Size { get; set; }

            /// <summary>
            /// The effect of pressure on the stroke's size.
            /// </summary>
            public double? Thinning { get; set; }

            /// <summary>
            /// How much to soften the stroke's edges.
            /// </summary>
            public double? Smoothing { get; set; }
            public double? Streamline { get; set; }

            /// <summary>
            /// An easing function to apply to each point's pressure.
            /// </summary>
            public Func<double, double> Easing { get; set; }

            /// <summary>
            /// Whether to simulate pressure based on velocity.
            /// </summary>
            public bool? SimulatePressure { get; set; }

            /// <summary>
            /// Cap, taper and easing for the start of the line.
            /// </summary>
            public StrokeCapOptions Start { get; set; }

            /// <summary>
            /// Cap, taper and easing for the end of the line.
            /// </summary>
            public StrokeCapOptions End { get; set; }

            /// <summary>
            /// Whether to handle the points as a completed stroke.
            /// </summary>
            public bool? Last { get; set; }
        }

        public class StrokeCapOptions
        {
            /// <summary>
            /// Whether to apply a cap at the start/end of the line.
            /// </summary>
            public bool Cap { get; set; }

            /// <summary>
            /// The taper value at the start/end of the line.
            /// </summary>
            public double Taper { get; set; }

            public bool IsTaper { get; set; }

            /// <summary>
            /// An easing function to apply to the taper.
            /// </summary>
            public Func<double, double> Easing { get; set; }
        }

        public class StrokePoint
        {
            /// <summary>
            /// The point coordinates as [x, y].
            /// </summary>
            public Vector Point { get; set; }

            /// <summary>
            /// The pressure at the point.
            /// </summary>
            public double Pressure { get; set; }

            /// <summary>
            /// The distance from the previous point.
            /// </summary>
            public double Distance { get; set; }

            /// <summary>
            /// The vector at the point.
            /// </summary>
            public Vector Vector { get; set; }

            /// <summary>
            /// The running length of the stroke.
            /// </summary>
            public double RunningLength { get; set; }
        }
    }
}
