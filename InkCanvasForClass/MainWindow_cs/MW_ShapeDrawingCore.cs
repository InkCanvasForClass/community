using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas {

    public partial class MainWindow : PerformanceTransparentWin {

        public StrokeCollection DrawShapeCore(PointCollection pts, ShapeDrawingType type, bool doNotDisturbutePoints, bool isPreview) {
            // 线
            if (type == MainWindow.ShapeDrawingType.Line || 
                type == MainWindow.ShapeDrawingType.DashedLine || 
                type == MainWindow.ShapeDrawingType.DottedLine ||
                type == MainWindow.ShapeDrawingType.ArrowOneSide ||
                type == MainWindow.ShapeDrawingType.ArrowTwoSide) {
                if (pts.Count != 2) throw new Exception("传入的点个数不是2个");
                var stk = new IccStroke(new StylusPointCollection() {
                    new StylusPoint(pts[0].X, pts[0].Y),
                    new StylusPoint(pts[1].X, pts[1].Y),
                }, inkCanvas.DefaultDrawingAttributes.Clone()) {
                    IsDistributePointsOnLineShape = !doNotDisturbutePoints,
                };
                stk.AddPropertyData(IccStroke.StrokeIsShapeGuid, true);
                stk.AddPropertyData(IccStroke.StrokeShapeTypeGuid, (int)type);
                return new StrokeCollection() { stk };
            }

            return new StrokeCollection();
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
    }
}
