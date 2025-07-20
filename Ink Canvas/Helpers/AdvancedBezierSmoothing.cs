using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using Point = System.Windows.Point;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 高级贝塞尔曲线平滑算法 - 优化版本
    /// 用于解决墨迹闪烁问题，提供更平滑的笔迹效果
    /// 优化特性：
    /// 1. 更平滑的墨迹：改进的贝塞尔曲线算法
    /// 2. 更平滑的拐点：优化的控制点计算
    /// 3. 1像素级别插点：精确到像素级别的曲线生成
    /// </summary>
    public class AdvancedBezierSmoothing
    {
        /// <summary>
        /// 平滑强度 (0.0 - 1.0) - 优化为更平滑的默认值
        /// </summary>
        public double SmoothingStrength { get; set; } = 0.7;

        /// <summary>
        /// 张力参数 (0.0 - 1.0) - 优化为更平滑的默认值
        /// </summary>
        public double Tension { get; set; } = 0.4;

        /// <summary>
        /// 是否启用自适应平滑
        /// </summary>
        public bool EnableAdaptiveSmoothing { get; set; } = true;

        /// <summary>
        /// 最小点间距阈值 - 优化为1像素级别
        /// </summary>
        public double MinPointDistance { get; set; } = 1.0; // 降低到1像素级别

        /// <summary>
        /// 最大点间距阈值 - 优化为更精细的控制
        /// </summary>
        public double MaxPointDistance { get; set; } = 15.0; // 进一步减少最大间距，提高平滑度

        /// <summary>
        /// 手抖修正强度 (0.0 - 1.0) - 优化为更平滑的默认值
        /// </summary>
        public double ShakeCorrectionStrength { get; set; } = 0.8;

        /// <summary>
        /// 速度加权平滑强度 (0.0 - 1.0) - 优化为更平滑的默认值
        /// </summary>
        public double VelocityWeightedSmoothingStrength { get; set; } = 0.8;

        /// <summary>
        /// 时间加权平滑强度 (0.0 - 1.0) - 优化为更平滑的默认值
        /// </summary>
        public double TimeWeightedSmoothingStrength { get; set; } = 0.6;

        /// <summary>
        /// 拐点平滑强度 (0.0 - 1.0) - 新增参数，优化为更平滑的默认值
        /// </summary>
        public double CornerSmoothingStrength { get; set; } = 0.8;

        /// <summary>
        /// 1像素级别插点精度 - 新增参数
        /// </summary>
        public double PixelLevelPrecision { get; set; } = 1.0;

        /// <summary>
        /// 对笔画进行高级贝塞尔曲线平滑处理
        /// </summary>
        /// <param name="stroke">原始笔画</param>
        /// <returns>平滑后的笔画</returns>
        public Stroke SmoothStroke(Stroke stroke)
        {
            if (stroke == null || stroke.StylusPoints.Count < 3)
                return stroke;

            var originalPoints = stroke.StylusPoints.ToList();
            var smoothedPoints = new List<StylusPoint>();

            // 检查采样率，如果点太少则使用不同的处理策略
            bool isLowSamplingRate = originalPoints.Count < 10; // 少于10个点认为是低采样率

            if (isLowSamplingRate)
            {
                // 低采样率情况下的特殊处理
                return HandleLowSamplingRateStroke(stroke);
            }

            // 第一步：手抖修正
            var shakeCorrectedPoints = ApplyShakeCorrection(originalPoints);

            // 第二步：基于速度和时间的加权平滑
            var velocityTimeWeightedPoints = ApplyVelocityTimeWeightedSmoothing(shakeCorrectedPoints);

            // 第三步：拐点检测和平滑
            var cornerSmoothedPoints = ApplyCornerSmoothing(velocityTimeWeightedPoints);

            // 第四步：点过滤和重采样（1像素级别）
            var filteredPoints = FilterAndResamplePoints(cornerSmoothedPoints);

            // 第五步：计算优化的控制点
            var controlPoints = CalculateOptimizedControlPoints(filteredPoints);

            // 第六步：生成1像素级别的平滑曲线点
            var curvePoints = GeneratePixelLevelCurvePoints(filteredPoints, controlPoints);

            // 第七步：修正收尾相连问题
            var fixedStylusPoints = FixEndToEndConnection(new StylusPointCollection(curvePoints));
            
            // 第八步：创建新的笔画
            var smoothedStroke = new Stroke(fixedStylusPoints)
            {
                DrawingAttributes = stroke.DrawingAttributes.Clone()
            };

            return smoothedStroke;
        }

        /// <summary>
        /// 检测并修正收尾相连问题
        /// </summary>
        private StylusPointCollection FixEndToEndConnection(StylusPointCollection points)
        {
            if (points.Count < 3) return points;

            var resultPoints = new StylusPointCollection();
            
            // 复制所有点
            foreach (var point in points)
            {
                resultPoints.Add(point);
            }

            // 检查首尾是否过于接近
            var firstPoint = resultPoints[0];
            var lastPoint = resultPoints[resultPoints.Count - 1];
            double endToEndDistance = GetDistance(firstPoint.ToPoint(), lastPoint.ToPoint());

            // 如果首尾距离太近，可能是收尾相连问题
            if (endToEndDistance < 3.0) // 降低阈值到3像素
            {
                // 移除最后一个点，避免收尾相连
                if (resultPoints.Count > 1)
                {
                    resultPoints.RemoveAt(resultPoints.Count - 1);
                }
            }

            return resultPoints;
        }

        /// <summary>
        /// 过滤和重采样点（1像素级别优化）
        /// </summary>
        private List<StylusPoint> FilterAndResamplePoints(List<StylusPoint> points)
        {
            var filteredPoints = new List<StylusPoint>();
            
            if (points.Count == 0) return filteredPoints;

            // 添加第一个点
            filteredPoints.Add(points[0]);

            // 使用改进的移动平均来减少毛刺
            var smoothedPoints = new List<StylusPoint>();
            int windowSize = 5; // 增加窗口大小以获得更平滑的效果

            for (int i = 0; i < points.Count; i++)
            {
                var currentPoint = points[i];
                var lastPoint = filteredPoints[filteredPoints.Count - 1];

                double distance = GetDistance(lastPoint.ToPoint(), currentPoint.ToPoint());

                // 如果距离太近，跳过（1像素级别）
                if (distance < MinPointDistance)
                    continue;

                // 应用改进的移动平均平滑
                if (i >= windowSize - 1)
                {
                    double avgX = 0, avgY = 0, avgPressure = 0;
                    double totalWeight = 0;
                    
                    // 使用加权移动平均，中心点权重更高
                    for (int j = 0; j < windowSize; j++)
                    {
                        var point = points[i - j];
                        double weight = 1.0 - Math.Abs(j - (windowSize - 1) / 2.0) / (windowSize / 2.0);
                        weight = Math.Max(0.1, weight); // 确保最小权重
                        
                        avgX += point.X * weight;
                        avgY += point.Y * weight;
                        avgPressure += point.PressureFactor * weight;
                        totalWeight += weight;
                    }
                    
                    avgX /= totalWeight;
                    avgY /= totalWeight;
                    avgPressure /= totalWeight;

                    var smoothedPoint = new StylusPoint(avgX, avgY, (float)avgPressure);
                    currentPoint = smoothedPoint;
                }

                // 如果距离太远，插入中间点（1像素级别精度）
                if (distance > MaxPointDistance)
                {
                    int segments = Math.Max(2, (int)(distance / PixelLevelPrecision));
                    for (int j = 1; j < segments; j++)
                    {
                        double ratio = (double)j / segments;
                        var interpolatedPoint = InterpolatePoint(lastPoint, currentPoint, ratio);
                        filteredPoints.Add(interpolatedPoint);
                    }
                }

                filteredPoints.Add(currentPoint);
            }

            return filteredPoints;
        }

        /// <summary>
        /// 计算优化的控制点（改进拐点平滑）
        /// </summary>
        private List<Point> CalculateOptimizedControlPoints(List<StylusPoint> points)
        {
            var controlPoints = new List<Point>();
            
            if (points.Count < 2) return controlPoints;

            // 检查点密度，如果点太稀疏则使用更保守的控制点计算
            bool isSparsePoints = points.Count < 5;
            double tensionMultiplier = isSparsePoints ? 0.05 : 0.2; // 进一步减少张力
            double maxOffset = isSparsePoints ? 3.0 : 8.0; // 减少最大偏移

            for (int i = 0; i < points.Count; i++)
            {
                Point currentPoint = points[i].ToPoint();
                Point controlPoint;

                if (i == 0)
                {
                    // 第一个点的控制点
                    Point nextPoint = points[i + 1].ToPoint();
                    double distance = GetDistance(currentPoint, nextPoint);
                    
                    // 如果距离太远，使用更保守的控制点
                    if (distance > 30.0) // 降低阈值
                    {
                        controlPoint = currentPoint; // 直接使用当前点作为控制点
                    }
                    else
                    {
                        // 使用更平滑的控制点计算
                        double tension = Tension * tensionMultiplier * CornerSmoothingStrength;
                        controlPoint = new Point(
                            currentPoint.X + (nextPoint.X - currentPoint.X) * tension,
                            currentPoint.Y + (nextPoint.Y - currentPoint.Y) * tension
                        );
                    }
                }
                else if (i == points.Count - 1)
                {
                    // 最后一个点的控制点
                    Point prevPoint = points[i - 1].ToPoint();
                    double distance = GetDistance(currentPoint, prevPoint);
                    
                    // 如果距离太远，使用更保守的控制点
                    if (distance > 30.0) // 降低阈值
                    {
                        controlPoint = currentPoint; // 直接使用当前点作为控制点
                    }
                    else
                    {
                        // 使用更平滑的控制点计算
                        double tension = Tension * tensionMultiplier * CornerSmoothingStrength;
                        controlPoint = new Point(
                            currentPoint.X + (currentPoint.X - prevPoint.X) * tension,
                            currentPoint.Y + (currentPoint.Y - prevPoint.Y) * tension
                        );
                    }
                }
                else
                {
                    // 中间点的控制点（改进拐点平滑）
                    Point prevPoint = points[i - 1].ToPoint();
                    Point nextPoint = points[i + 1].ToPoint();

                    // 检查前后点的距离，如果太远则使用更保守的方法
                    double prevDistance = GetDistance(currentPoint, prevPoint);
                    double nextDistance = GetDistance(currentPoint, nextPoint);
                    
                    if (prevDistance > 30.0 || nextDistance > 30.0) // 降低阈值
                    {
                        // 距离太远，使用线性插值作为控制点
                        controlPoint = new Point(
                            (prevPoint.X + nextPoint.X) / 2.0,
                            (prevPoint.Y + nextPoint.Y) / 2.0
                        );
                    }
                    else
                    {
                        // 计算改进的切线方向，使用更平滑的方法
                        double tangentX = (nextPoint.X - prevPoint.X) * tensionMultiplier;
                        double tangentY = (nextPoint.Y - prevPoint.Y) * tensionMultiplier;

                        // 应用张力参数和拐点平滑，但限制最大偏移
                        double offsetX = tangentX * Tension * CornerSmoothingStrength;
                        double offsetY = tangentY * Tension * CornerSmoothingStrength;
                        
                        // 限制偏移距离
                        double offsetDistance = Math.Sqrt(offsetX * offsetX + offsetY * offsetY);
                        if (offsetDistance > maxOffset)
                        {
                            offsetX = offsetX * maxOffset / offsetDistance;
                            offsetY = offsetY * maxOffset / offsetDistance;
                        }

                        controlPoint = new Point(
                            currentPoint.X + offsetX,
                            currentPoint.Y + offsetY
                        );
                    }
                }

                controlPoints.Add(controlPoint);
            }

            return controlPoints;
        }

        /// <summary>
        /// 生成1像素级别的平滑曲线点
        /// </summary>
        private List<StylusPoint> GeneratePixelLevelCurvePoints(List<StylusPoint> points, List<Point> controlPoints)
        {
            var curvePoints = new List<StylusPoint>();
            
            if (points.Count < 2) return curvePoints;

            // 为每个线段生成贝塞尔曲线点
            for (int i = 0; i < points.Count - 1; i++)
            {
                var startPoint = points[i];
                var endPoint = points[i + 1];
                var startControl = controlPoints[i];
                var endControl = controlPoints[i + 1];

                // 计算1像素级别的步长
                double distance = GetDistance(startPoint.ToPoint(), endPoint.ToPoint());
                
                // 根据距离计算精确的步数，确保1像素级别精度
                int steps = Math.Max(1, (int)(distance / PixelLevelPrecision));
                
                // 限制最大步数以避免过度细分
                steps = Math.Min(steps, 50);

                // 生成贝塞尔曲线点
                for (int j = 0; j <= steps; j++)
                {
                    double t = (double)j / steps;
                    var curvePoint = CalculateBezierPoint(startPoint.ToPoint(), startControl, endControl, endPoint.ToPoint(), t);
                    
                    // 插值压感值，使用更平滑的插值
                    float pressure = InterpolatePressure(startPoint.PressureFactor, endPoint.PressureFactor, t);
                    
                    // 对压感值进行额外的平滑处理
                    if (j > 0 && j < steps)
                    {
                        float prevPressure = curvePoints[curvePoints.Count - 1].PressureFactor;
                        pressure = (prevPressure + pressure) * 0.5f; // 简单的移动平均
                    }
                    
                    var stylusPoint = new StylusPoint(curvePoint.X, curvePoint.Y, pressure);
                    curvePoints.Add(stylusPoint);
                }
            }

            return curvePoints;
        }

        /// <summary>
        /// 应用拐点平滑
        /// </summary>
        private List<StylusPoint> ApplyCornerSmoothing(List<StylusPoint> points)
        {
            if (points.Count < 3) return points;

            var smoothedPoints = new List<StylusPoint>();
            smoothedPoints.Add(points[0]); // 添加第一个点

            for (int i = 1; i < points.Count - 1; i++)
            {
                var prev = points[i - 1];
                var curr = points[i];
                var next = points[i + 1];

                // 计算角度变化
                double angle1 = Math.Atan2(curr.Y - prev.Y, curr.X - prev.X);
                double angle2 = Math.Atan2(next.Y - curr.Y, next.X - curr.X);
                double angleDiff = Math.Abs(angle2 - angle1);
                
                // 标准化角度差
                if (angleDiff > Math.PI) angleDiff = 2 * Math.PI - angleDiff;

                // 如果角度变化太大，认为是拐点
                if (angleDiff > Math.PI / 4) // 45度阈值
                {
                    // 应用拐点平滑
                    double smoothingFactor = CornerSmoothingStrength;
                    double smoothedX = curr.X * (1.0 - smoothingFactor) + 
                                     (prev.X + next.X) * 0.5 * smoothingFactor;
                    double smoothedY = curr.Y * (1.0 - smoothingFactor) + 
                                     (prev.Y + next.Y) * 0.5 * smoothingFactor;

                    var smoothedPoint = new StylusPoint(smoothedX, smoothedY, curr.PressureFactor);
                    smoothedPoints.Add(smoothedPoint);
                }
                else
                {
                    smoothedPoints.Add(curr);
                }
            }

            smoothedPoints.Add(points[points.Count - 1]); // 添加最后一个点
            return smoothedPoints;
        }

        /// <summary>
        /// 计算贝塞尔曲线上的点
        /// </summary>
        private Point CalculateBezierPoint(Point p0, Point p1, Point p2, Point p3, double t)
        {
            double u = 1 - t;
            double tt = t * t;
            double uu = u * u;
            double uuu = uu * u;
            double ttt = tt * t;

            Point point = new Point();
            point.X = uuu * p0.X + 3 * uu * t * p1.X + 3 * u * tt * p2.X + ttt * p3.X;
            point.Y = uuu * p0.Y + 3 * uu * t * p1.Y + 3 * u * tt * p2.Y + ttt * p3.Y;

            return point;
        }

        /// <summary>
        /// 计算两点间距离
        /// </summary>
        private double GetDistance(Point p1, Point p2)
        {
            return Math.Sqrt((p1.X - p2.X) * (p1.X - p2.X) + (p1.Y - p2.Y) * (p1.Y - p2.Y));
        }

        /// <summary>
        /// 插值两点间的点
        /// </summary>
        private StylusPoint InterpolatePoint(StylusPoint p1, StylusPoint p2, double ratio)
        {
            return new StylusPoint(
                p1.X + (p2.X - p1.X) * ratio,
                p1.Y + (p2.Y - p1.Y) * ratio,
                InterpolatePressure(p1.PressureFactor, p2.PressureFactor, ratio)
            );
        }

        /// <summary>
        /// 插值压感值
        /// </summary>
        private float InterpolatePressure(float p1, float p2, double ratio)
        {
            // 使用平滑的插值函数来减少毛刺
            double smoothRatio = SmoothStep(ratio);
            return (float)(p1 + (p2 - p1) * smoothRatio);
        }

        /// <summary>
        /// 平滑步进函数，用于减少插值时的毛刺
        /// </summary>
        private double SmoothStep(double t)
        {
            // 使用三次平滑函数
            return t * t * (3.0 - 2.0 * t);
        }

        /// <summary>
        /// 应用手抖修正
        /// </summary>
        private List<StylusPoint> ApplyShakeCorrection(List<StylusPoint> points)
        {
            if (points.Count < 3) return points;

            var correctedPoints = new List<StylusPoint>();
            correctedPoints.Add(points[0]); // 添加第一个点

            for (int i = 1; i < points.Count - 1; i++)
            {
                var prev = points[i - 1];
                var curr = points[i];
                var next = points[i + 1];

                // 计算当前点的预期位置（基于前后点的线性插值）
                double expectedX = (prev.X + next.X) / 2.0;
                double expectedY = (prev.Y + next.Y) / 2.0;

                // 计算当前点与预期位置的偏差
                double deviationX = Math.Abs(curr.X - expectedX);
                double deviationY = Math.Abs(curr.Y - expectedY);
                double deviation = Math.Sqrt(deviationX * deviationX + deviationY * deviationY);

                // 如果偏差超过阈值，认为是手抖
                double shakeThreshold = 3.0; // 降低手抖检测阈值
                if (deviation > shakeThreshold)
                {
                    // 应用手抖修正
                    double correctionFactor = ShakeCorrectionStrength;
                    double correctedX = curr.X + (expectedX - curr.X) * correctionFactor;
                    double correctedY = curr.Y + (expectedY - curr.Y) * correctionFactor;

                    // 保持压感值不变
                    var correctedPoint = new StylusPoint(correctedX, correctedY, curr.PressureFactor);
                    correctedPoints.Add(correctedPoint);
                }
                else
                {
                    correctedPoints.Add(curr);
                }
            }

            correctedPoints.Add(points[points.Count - 1]); // 添加最后一个点
            return correctedPoints;
        }

        /// <summary>
        /// 应用基于速度和时间的加权平滑
        /// </summary>
        private List<StylusPoint> ApplyVelocityTimeWeightedSmoothing(List<StylusPoint> points)
        {
            if (points.Count < 3) return points;

            var smoothedPoints = new List<StylusPoint>();
            smoothedPoints.Add(points[0]); // 添加第一个点

            // 计算每个点的速度和加速度
            var velocities = new List<double>();
            var accelerations = new List<double>();
            var timeWeights = new List<double>();

            for (int i = 1; i < points.Count; i++)
            {
                var prev = points[i - 1];
                var curr = points[i];

                // 计算速度（距离/时间，这里假设时间间隔为1）
                double velocity = GetDistance(prev.ToPoint(), curr.ToPoint());
                velocities.Add(velocity);

                // 计算加速度
                if (i > 1)
                {
                    double prevVelocity = velocities[velocities.Count - 2];
                    double acceleration = velocity - prevVelocity;
                    accelerations.Add(acceleration);
                }

                // 计算时间权重（基于点的密度）
                double timeWeight = 1.0;
                if (i > 1)
                {
                    // 如果点过于密集，增加时间权重
                    double avgDistance = velocity;
                    if (avgDistance < 1.0) // 降低阈值
                    {
                        timeWeight = 1.5; // 增加权重
                    }
                    else if (avgDistance > 15.0) // 降低阈值
                    {
                        timeWeight = 0.5; // 减少权重
                    }
                }
                timeWeights.Add(timeWeight);
            }

            // 应用加权平滑
            for (int i = 1; i < points.Count - 1; i++)
            {
                var prev = points[i - 1];
                var curr = points[i];
                var next = points[i + 1];

                // 计算速度权重
                double velocityWeight = 1.0;
                if (i < velocities.Count)
                {
                    double velocity = velocities[i - 1];
                    // 速度越快，权重越大（更平滑）
                    velocityWeight = Math.Min(2.0, velocity / 8.0 + 0.5); // 调整参数
                }

                // 计算加速度权重
                double accelerationWeight = 1.0;
                if (i < accelerations.Count)
                {
                    double acceleration = Math.Abs(accelerations[i - 1]);
                    // 加速度越大，权重越大（更平滑）
                    accelerationWeight = Math.Min(2.0, acceleration / 3.0 + 0.5); // 调整参数
                }

                // 获取时间权重
                double timeWeight = timeWeights[i];

                // 综合权重
                double totalWeight = (velocityWeight * VelocityWeightedSmoothingStrength + 
                                   accelerationWeight * VelocityWeightedSmoothingStrength + 
                                   timeWeight * TimeWeightedSmoothingStrength) / 3.0;

                // 应用加权平滑
                double smoothedX = curr.X;
                double smoothedY = curr.Y;

                if (totalWeight > 1.0)
                {
                    // 向相邻点加权平均
                    double weight = (totalWeight - 1.0) * 0.2; // 减少最大影响
                    smoothedX = curr.X * (1.0 - weight) + (prev.X + next.X) * 0.5 * weight;
                    smoothedY = curr.Y * (1.0 - weight) + (prev.Y + next.Y) * 0.5 * weight;
                }

                var smoothedPoint = new StylusPoint(smoothedX, smoothedY, curr.PressureFactor);
                smoothedPoints.Add(smoothedPoint);
            }

            smoothedPoints.Add(points[points.Count - 1]); // 添加最后一个点
            return smoothedPoints;
        }

        /// <summary>
        /// 处理低采样率笔画
        /// </summary>
        private Stroke HandleLowSamplingRateStroke(Stroke stroke)
        {
            var points = stroke.StylusPoints.ToList();
            var resultPoints = new List<StylusPoint>();

            // 对于低采样率，使用简单的线性插值而不是贝塞尔曲线
            for (int i = 0; i < points.Count - 1; i++)
            {
                var currentPoint = points[i];
                var nextPoint = points[i + 1];

                // 添加当前点
                resultPoints.Add(currentPoint);

                // 计算两点间距离
                double distance = GetDistance(currentPoint.ToPoint(), nextPoint.ToPoint());

                // 如果距离太远，插入中间点（1像素级别）
                if (distance > 15.0) // 降低阈值
                {
                    int segments = Math.Max(2, Math.Min(8, (int)(distance / PixelLevelPrecision))); // 使用像素级别精度
                    for (int j = 1; j < segments; j++)
                    {
                        double ratio = (double)j / segments;
                        var interpolatedPoint = InterpolatePoint(currentPoint, nextPoint, ratio);
                        resultPoints.Add(interpolatedPoint);
                    }
                }
            }

            // 添加最后一个点
            resultPoints.Add(points[points.Count - 1]);

            // 应用轻微的手抖修正
            var shakeCorrectedPoints = ApplyShakeCorrection(resultPoints);

            // 修正收尾相连问题
            var fixedStylusPoints = FixEndToEndConnection(new StylusPointCollection(shakeCorrectedPoints));

            // 创建新的笔画
            var smoothedStroke = new Stroke(fixedStylusPoints)
            {
                DrawingAttributes = stroke.DrawingAttributes.Clone()
            };

            return smoothedStroke;
        }

        /// <summary>
        /// 应用自适应平滑
        /// </summary>
        private void ApplyAdaptiveSmoothing(List<StylusPoint> points)
        {
            if (!EnableAdaptiveSmoothing || points.Count < 3)
                return;

            // 计算笔迹的速度变化
            var speeds = new List<double>();
            for (int i = 1; i < points.Count - 1; i++)
            {
                var prev = points[i - 1].ToPoint();
                var curr = points[i].ToPoint();
                var next = points[i + 1].ToPoint();

                double speed1 = GetDistance(prev, curr);
                double speed2 = GetDistance(curr, next);
                double avgSpeed = (speed1 + speed2) / 2.0;

                speeds.Add(avgSpeed);
            }

            // 根据速度调整平滑强度
            if (speeds.Count > 0)
            {
                double avgSpeed = speeds.Average();
                double maxSpeed = speeds.Max();
                double minSpeed = speeds.Min();

                // 速度变化越大，平滑强度越小
                double speedVariation = (maxSpeed - minSpeed) / avgSpeed;
                SmoothingStrength = Math.Max(0.1, Math.Min(0.9, SmoothingStrength * (1.0 - speedVariation * 0.3)));
            }
        }
    }
} 