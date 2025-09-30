using Ink_Canvas.Helpers;
using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas
{
    public partial class MainWindow : Window
    {
        // 橡皮擦系统核心变量
        public bool isUsingGeometryEraser = false;
        private IncrementalStrokeHitTester hitTester = null;

        public double eraserWidth = 64;
        public bool isEraserCircleShape = false;
        public bool isUsingStrokesEraser = false;

        private Matrix scaleMatrix = new Matrix();

        // 橡皮擦覆盖层相关控件
        private System.Windows.Controls.Canvas eraserOverlayCanvas;
        private Image eraserFeedback;
        private TranslateTransform eraserFeedbackTranslateTransform;

        // 锁定笔画的GUID
        private static readonly Guid IsLockGuid = new Guid("12345678-1234-1234-1234-123456789ABC");

        /// <summary>
        /// 橡皮擦覆盖层加载事件处理
        /// </summary>
        private void EraserOverlayCanvas_Loaded(object sender, RoutedEventArgs e)
        {
            var canvas = (System.Windows.Controls.Canvas)sender;
            eraserOverlayCanvas = canvas;

            // 获取橡皮擦反馈控件
            eraserFeedback = FindName("EraserFeedback") as Image;
            if (eraserFeedback != null)
            {
                eraserFeedbackTranslateTransform = eraserFeedback.RenderTransform as TranslateTransform;
            }

            // 绑定事件处理
            canvas.StylusDown += ((o, args) => {
                e.Handled = true;
                if (args.StylusDevice.TabletDevice.Type == TabletDeviceType.Stylus) canvas.CaptureStylus();
                EraserOverlay_PointerDown(sender);
            });
            canvas.StylusUp += ((o, args) => {
                e.Handled = true;
                if (args.StylusDevice.TabletDevice.Type == TabletDeviceType.Stylus) canvas.ReleaseStylusCapture();
                EraserOverlay_PointerUp(sender);
            });
            canvas.StylusMove += ((o, args) => {
                e.Handled = true;
                EraserOverlay_PointerMove(sender, args.GetPosition(inkCanvas));
            });
            canvas.MouseDown += ((o, args) => {
                canvas.CaptureMouse();
                EraserOverlay_PointerDown(sender);
            });
            canvas.MouseUp += ((o, args) => {
                canvas.ReleaseMouseCapture();
                EraserOverlay_PointerUp(sender);
            });
            canvas.MouseMove += ((o, args) => {
                EraserOverlay_PointerMove(sender, args.GetPosition(inkCanvas));
            });

            // 设置橡皮擦样式
            UpdateEraserStyle();
        }

        /// <summary>
        /// 更新橡皮擦样式
        /// </summary>
        private void UpdateEraserStyle()
        {
            if (eraserFeedback == null) return;

            // 根据橡皮擦形状选择对应的图像资源
            string resourceKey = isEraserCircleShape ? "EllipseEraserImageSource" : "RectangleEraserImageSource";

            if (TryFindResource(resourceKey) is DrawingImage imageSource)
            {
                eraserFeedback.Source = imageSource;
            }
        }

        /// <summary>
        /// 橡皮擦按下事件处理
        /// </summary>
        private void EraserOverlay_PointerDown(object sender)
        {
            if (isUsingGeometryEraser) return;

            // 锁定
            isUsingGeometryEraser = true;

            // 计算高度
            var _h = eraserWidth * 56 / 38;

            // 初始化碰撞检测器
            StylusShape eraserShape;
            if (isEraserCircleShape)
            {
                eraserShape = new EllipseStylusShape(eraserWidth, eraserWidth);
            }
            else
            {
                eraserShape = new RectangleStylusShape(eraserWidth, _h);
            }

            hitTester = inkCanvas.Strokes.GetIncrementalStrokeHitTester(eraserShape);
            hitTester.StrokeHit += EraserGeometry_StrokeHit;

            // 计算缩放矩阵
            var scaleX = eraserWidth / 38;
            var scaleY = _h / 56;
            scaleMatrix = new Matrix();
            scaleMatrix.ScaleAt(scaleX, scaleY, 0, 0);

            // 设置橡皮擦反馈大小
            if (eraserFeedback != null)
            {
                eraserFeedback.Width = Math.Max(eraserWidth, 10);
                eraserFeedback.Height = isEraserCircleShape ? eraserFeedback.Width : _h;
                eraserFeedback.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
                eraserFeedback.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// 橡皮擦抬起事件处理
        /// </summary>
        private void EraserOverlay_PointerUp(object sender)
        {
            if (!isUsingGeometryEraser) return;

            // 解锁
            isUsingGeometryEraser = false;

            // 释放捕获
            ((UIElement)sender).ReleaseMouseCapture();

            // 隐藏橡皮擦反馈
            if (eraserFeedback != null)
            {
                eraserFeedback.Visibility = Visibility.Collapsed;
            }

            // 结束碰撞检测
            if (hitTester != null)
            {
                hitTester.EndHitTesting();
                hitTester = null;
            }

            // 提交橡皮擦历史记录
            if (ReplacedStroke != null || AddedStroke != null)
            {
                timeMachine.CommitStrokeEraseHistory(ReplacedStroke, AddedStroke);
                AddedStroke = null;
                ReplacedStroke = null;
            }
        }

        /// <summary>
        /// 橡皮擦移动事件处理
        /// </summary>
        private void EraserOverlay_PointerMove(object sender, Point pt)
        {
            if (!isUsingGeometryEraser) return;

            if (isUsingStrokesEraser)
            {
                // 笔画橡皮擦模式
                var _filtered = inkCanvas.Strokes.HitTest(pt).Where(stroke => !stroke.ContainsPropertyData(IsLockGuid));
                var filtered = _filtered as Stroke[] ?? _filtered.ToArray();
                if (!filtered.Any()) return;
                inkCanvas.Strokes.Remove(new StrokeCollection(filtered));
            }
            else
            {
                // 几何橡皮擦模式
                // 显示橡皮擦反馈
                if (eraserFeedback != null && eraserFeedback.Visibility == Visibility.Collapsed)
                {
                    eraserFeedback.Visibility = Visibility.Visible;
                }

                // 更新橡皮擦位置
                if (eraserFeedbackTranslateTransform != null)
                {
                    eraserFeedbackTranslateTransform.X = pt.X - eraserFeedback.ActualWidth / 2;
                    eraserFeedbackTranslateTransform.Y = pt.Y - eraserFeedback.ActualHeight / 2;
                }

                // 添加点到碰撞检测器
                if (hitTester != null)
                {
                    hitTester.AddPoint(pt);
                }
            }
        }

        /// <summary>
        /// 橡皮擦几何碰撞事件处理
        /// </summary>
        private void EraserGeometry_StrokeHit(object sender, StrokeHitEventArgs args)
        {
            StrokeCollection eraseResult = args.GetPointEraseResults();
            StrokeCollection strokesToReplace = new StrokeCollection { args.HitStroke };

            // 过滤锁定的笔画
            var filtered_2replace = strokesToReplace.Where(stroke => !stroke.ContainsPropertyData(IsLockGuid));
            var filtered2Replace = filtered_2replace as Stroke[] ?? filtered_2replace.ToArray();
            if (!filtered2Replace.Any()) return;

            var filtered_result = eraseResult.Where(stroke => !stroke.ContainsPropertyData(IsLockGuid));
            var filteredResult = filtered_result as Stroke[] ?? filtered_result.ToArray();

            // 替换或删除笔画
            if (filteredResult.Any())
            {
                inkCanvas.Strokes.Replace(new StrokeCollection(filtered2Replace), new StrokeCollection(filteredResult));
            }
            else
            {
                inkCanvas.Strokes.Remove(new StrokeCollection(filtered2Replace));
            }
        }

        /// <summary>
        /// 启用橡皮擦覆盖层
        /// </summary>
        public void EnableEraserOverlay()
        {
            if (eraserOverlayCanvas != null)
            {
                eraserOverlayCanvas.IsHitTestVisible = true;
                eraserOverlayCanvas.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// 禁用橡皮擦覆盖层
        /// </summary>
        public void DisableEraserOverlay()
        {
            if (eraserOverlayCanvas != null)
            {
                eraserOverlayCanvas.IsHitTestVisible = false;
                eraserOverlayCanvas.Visibility = Visibility.Collapsed;
            }

            // 重置橡皮擦状态
            if (isUsingGeometryEraser)
            {
                isUsingGeometryEraser = false;
                if (hitTester != null)
                {
                    hitTester.EndHitTesting();
                    hitTester = null;
                }
            }

            // 隐藏橡皮擦反馈
            if (eraserFeedback != null)
            {
                eraserFeedback.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// 更新橡皮擦尺寸
        /// </summary>
        public void UpdateEraserSize()
        {
            double k = 1.0;

            switch (Settings.Canvas.EraserSize)
            {
                case 0: k = Settings.Canvas.EraserShapeType == 0 ? 0.5 : 0.7; break;
                case 1: k = Settings.Canvas.EraserShapeType == 0 ? 0.8 : 0.9; break;
                case 2: k = 1.0; break;
                case 3: k = Settings.Canvas.EraserShapeType == 0 ? 1.25 : 1.2; break;
                case 4: k = Settings.Canvas.EraserShapeType == 0 ? 1.5 : 1.3; break;
            }

            // 更新形状类型
            isEraserCircleShape = (Settings.Canvas.EraserShapeType == 0);

            // 根据形状类型设置尺寸
            if (isEraserCircleShape)
            {
                eraserWidth = k * 90; // 圆形橡皮擦
                }
                else
            {
                eraserWidth = k * 90 * 0.6; // 矩形橡皮擦宽度
            }

            // 更新橡皮擦样式
            UpdateEraserStyle();
        }

        /// <summary>
        /// 切换橡皮擦形状
        /// </summary>
        public void ToggleEraserShape()
        {
            isEraserCircleShape = !isEraserCircleShape;
            Settings.Canvas.EraserShapeType = isEraserCircleShape ? 0 : 1;
            UpdateEraserStyle();
        }

        /// <summary>
        /// 切换橡皮擦模式
        /// </summary>
        public void ToggleEraserMode()
        {
            isUsingStrokesEraser = !isUsingStrokesEraser;
        }

        /// <summary>
        /// 应用橡皮擦形状到InkCanvas
        /// </summary>
        public void ApplyAdvancedEraserShape()
        {
            try
            {
                // 更新橡皮擦尺寸
                UpdateEraserSize();

                // 创建橡皮擦形状
                StylusShape eraserShape;
                if (isEraserCircleShape)
                {
                    eraserShape = new EllipseStylusShape(eraserWidth, eraserWidth);
                }
                else
                {
                    var height = eraserWidth * 56 / 38;
                    eraserShape = new RectangleStylusShape(eraserWidth, height);
                }

                // 应用到InkCanvas
                inkCanvas.EraserShape = eraserShape;

                Trace.WriteLine($"Eraser: Applied shape - Size: {eraserWidth}, Circle: {isEraserCircleShape}");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Eraser: Error applying shape - {ex.Message}");
            }
        }

        /// <summary>
        /// 获取橡皮擦状态信息
        /// </summary>
        public string GetEraserStatusInfo()
        {
            return $"橡皮擦状态:\n" +
                   $"- 激活: {isUsingGeometryEraser}\n" +
                   $"- 尺寸: {eraserWidth:F1}\n" +
                   $"- 形状: {(isEraserCircleShape ? "圆形" : "矩形")}\n" +
                   $"- 模式: {(isUsingStrokesEraser ? "笔画" : "几何")}";
        }
    }
}