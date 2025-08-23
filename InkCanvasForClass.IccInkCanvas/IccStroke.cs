using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace InkCanvasForClass.IccInkCanvas {

    static class ToPointsHelper {
        public static Point[] ToPoints(this IEnumerable<StylusPoint> stylusPoints)
        {
            List<Point> pointList = new List<Point>();
            foreach (StylusPoint stylusPoint in stylusPoints)
                pointList.Add(stylusPoint.ToPoint());
            return pointList.ToArray();
        }
    }

    public enum ShapeDrawingType {
        Line,
        DottedLine,
        DashedLine,
        ArrowOneSide,
        ArrowTwoSide,
        Rectangle,
        Ellipse,
        PieEllipse,
        Triangle,
        RightTriangle,
        Diamond,
        Parallelogram,
        FourLine,
        Staff,
        Axis2D,
        Axis2DA,
        Axis2DB,
        Axis2DC,
        Axis3D,
        Hyperbola,
        HyperbolaF,
        Parabola,
        ParabolaA,
        ParabolaAF,
        Cylinder,
        Cube,
        Cone,
        EllipseC,
        RectangleC
    }

    public static class ShapeDrawingHelper {

        /// <summary>
        /// 根据给定的两个点计算角度
        /// </summary>
        /// <param name="firstPoint"></param>
        /// <param name="lastPoint"></param>
        /// <returns></returns>
        public static double CaculateRotateAngleByGivenTwoPoints(Point firstPoint, Point lastPoint) {
            var vec1 = new double[] {
                lastPoint.X - firstPoint.X ,
                lastPoint.Y - firstPoint.Y
            };
            var vec_base = new double[] { 0, firstPoint.Y };
            var cosine = (vec_base[0] * vec1[0] + vec_base[1] * vec1[1]) /
                         (Math.Sqrt(Math.Pow(vec_base[0],2) + Math.Pow(vec_base[1],2)) *
                          Math.Sqrt(Math.Pow(vec1[0],2) + Math.Pow(vec1[1],2)));
            var angle = Math.Acos(cosine);
            var isIn2And3Quadrant = lastPoint.X <= firstPoint.X;
            var rotateAngle = Math.Round(180 + 180 * (angle / Math.PI) * (isIn2And3Quadrant ? 1 : -1), 0);
            return rotateAngle;
        }


        public static List<Point> DistributePointsOnLine(Point start, Point end, double interval=16) {
            List<Point> points = new List<Point>();

            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            int numPoints = (int)(distance / interval);

            for (int i = 0; i <= numPoints; i++) {
                double ratio = (interval * i) / distance;
                double x = start.X + ratio * dx;
                double y = start.Y + ratio * dy;
                points.Add(new Point(x, y));
            }

            return points;
        }

        public class ArrowLineConfig {
            public int ArrowWidth { get; set; } = 20;
            public int ArrowHeight { get; set; } = 7;
        }

    }

    public class IccStroke : Stroke {

        public IccStroke(StylusPointCollection stylusPoints, DrawingAttributes drawingAttributes)
            : base(stylusPoints, drawingAttributes) { }

        public static Guid StrokeShapeTypeGuid = new Guid("6537b29c-557f-487f-800b-cb30a8f1de78");
        public static Guid StrokeIsShapeGuid = new Guid("40eff5db-9346-4e42-bd46-7b0eb19d0018");

        public StylusPointCollection RawStylusPointCollection { get; set; }

        public ShapeDrawingHelper.ArrowLineConfig ArrowLineConfig { get; set; } =
            new ShapeDrawingHelper.ArrowLineConfig();

        /// <summary>
        /// 根据这个属性判断当前 Stroke 是否是原始输入
        /// </summary>
        public bool IsRawStylusPoints = true;

        /// <summary>
        /// 根据这个属性决定在绘制 Stroke 时是否需要在直线形状中，在两点构成直线上分布点，用于墨迹的范围框选。
        /// </summary>
        public bool IsDistributePointsOnLineShape = true;

        /// <summary>
        /// 指示该墨迹是否来自一个完整墨迹被擦除后的一部分墨迹，仅用于形状墨迹。
        /// </summary>
        public bool IsErasedStrokePart = false;

        // 自定义的墨迹渲染
        protected override void DrawCore(DrawingContext drawingContext,
            DrawingAttributes drawingAttributes) {
            if (!(this.ContainsPropertyData(StrokeIsShapeGuid) &&
                  (bool)this.GetPropertyData(StrokeIsShapeGuid) == true)) {
                base.DrawCore(drawingContext, drawingAttributes);
                return;
            }

            if ((int)this.GetPropertyData(StrokeShapeTypeGuid) == (int)ShapeDrawingType.DashedLine ||
                (int)this.GetPropertyData(StrokeShapeTypeGuid) == (int)ShapeDrawingType.Line ||
                (int)this.GetPropertyData(StrokeShapeTypeGuid) == (int)ShapeDrawingType.DottedLine ||
                (int)this.GetPropertyData(StrokeShapeTypeGuid) == (int)ShapeDrawingType.ArrowOneSide ||
                (int)this.GetPropertyData(StrokeShapeTypeGuid) == (int)ShapeDrawingType.ArrowTwoSide) {
                if (StylusPoints.Count < 2) {
                    base.DrawCore(drawingContext, drawingAttributes);
                    return;
                }

                var pts = new List<Point>(this.StylusPoints.ToPoints());
                if (IsDistributePointsOnLineShape && (
                    (int)this.GetPropertyData(StrokeShapeTypeGuid) == (int)ShapeDrawingType.DashedLine ||
                    (int)this.GetPropertyData(StrokeShapeTypeGuid) == (int)ShapeDrawingType.Line ||
                    (int)this.GetPropertyData(StrokeShapeTypeGuid) == (int)ShapeDrawingType.DottedLine) && IsRawStylusPoints) {
                    IsRawStylusPoints = false;
                    RawStylusPointCollection = StylusPoints.Clone();
                    var pointList = new List<Point> { new Point(StylusPoints[0].X, StylusPoints[0].Y) };
                    pointList.AddRange(ShapeDrawingHelper.DistributePointsOnLine(new Point(StylusPoints[0].X, StylusPoints[0].Y),new Point(StylusPoints[1].X, StylusPoints[1].Y)));
                    pointList.Add(new Point(StylusPoints[1].X, StylusPoints[1].Y));
                    StylusPoints = new StylusPointCollection(pointList);
                }
                StreamGeometry geometry = new StreamGeometry();
                using (StreamGeometryContext ctx = geometry.Open()) {
                    ctx.BeginFigure(pts[0], false , false);
                    pts.RemoveAt(0);
                    ctx.PolyLineTo(pts,true, true);
                }
                var pen = new Pen(new SolidColorBrush(DrawingAttributes.Color),
                    (drawingAttributes.Width + drawingAttributes.Height) / 2) {
                    DashCap = PenLineCap.Round,
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round,
                };
                if ((int)this.GetPropertyData(StrokeShapeTypeGuid) != (int)ShapeDrawingType.Line && 
                    (int)this.GetPropertyData(StrokeShapeTypeGuid) != (int)ShapeDrawingType.ArrowOneSide && 
                    (int)this.GetPropertyData(StrokeShapeTypeGuid) != (int)ShapeDrawingType.ArrowTwoSide)
                    pen.DashStyle = (int)this.GetPropertyData(StrokeShapeTypeGuid) == (int)ShapeDrawingType.DottedLine ? DashStyles.Dot : DashStyles.Dash;
                
                if ((int)this.GetPropertyData(StrokeShapeTypeGuid) == (int)ShapeDrawingType.ArrowOneSide && IsRawStylusPoints) {
                    IsRawStylusPoints = false;
                    pts = new List<Point>(this.StylusPoints.ToPoints());
                    RawStylusPointCollection = StylusPoints.Clone();
                    double w = ArrowLineConfig.ArrowWidth, h = ArrowLineConfig.ArrowHeight;
                    var theta = Math.Atan2(pts[0].Y - pts[1].Y, pts[0].X - pts[1].X);
                    var sint = Math.Sin(theta);
                    var cost = Math.Cos(theta);
                    var pointList = new List<Point> {
                        new Point(pts[0].X, pts[0].Y),
                    };
                    if (IsDistributePointsOnLineShape) pointList.AddRange(ShapeDrawingHelper.DistributePointsOnLine(new Point(pts[0].X, pts[0].Y),new Point(pts[1].X, pts[1].Y)));
                    pointList.AddRange(new List<Point> {
                        new Point(pts[1].X, pts[1].Y),
                        new Point(pts[1].X + (w * cost - h * sint), pts[1].Y + (w * sint + h * cost)),
                        new Point(pts[1].X, pts[1].Y),
                        new Point(pts[1].X + (w * cost + h * sint), pts[1].Y - (h * cost - w * sint)),
                    });
                    StylusPoints = new StylusPointCollection(pointList);
                    var _pts = new List<Point>(this.StylusPoints.ToPoints());
                    using (StreamGeometryContext ctx = geometry.Open()) {
                        ctx.BeginFigure(_pts[0], false , false);
                        _pts.RemoveAt(0);
                        ctx.PolyLineTo(_pts,true, true);
                    }
                    drawingContext.DrawGeometry(new SolidColorBrush(DrawingAttributes.Color),pen, geometry);
                    return;

                }
                drawingContext.DrawGeometry(new SolidColorBrush(Colors.Transparent),pen, geometry);
            }
            
        }
    }
}
