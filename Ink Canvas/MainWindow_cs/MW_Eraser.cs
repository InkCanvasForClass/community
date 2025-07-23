using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using Ink_Canvas.Helpers;

namespace Ink_Canvas {
    public partial class MainWindow : Window {

        // 新橡皮擦系统的核心变量
        public bool isUsingAdvancedEraser = false;
        private IncrementalStrokeHitTester advancedHitTester = null;

        // 橡皮擦配置
        public double currentEraserSize = 64;
        public bool isCurrentEraserCircle = false;
        public bool isUsingStrokeEraser = false;

        // 视觉反馈相关
        private Matrix eraserTransformMatrix = new Matrix();
        private Point lastEraserPosition = new Point();
        private bool isEraserVisible = false;

        // 性能优化相关
        private DateTime lastEraserUpdate = DateTime.Now;
        private const double ERASER_UPDATE_INTERVAL = 16.67; // 约60FPS

        // 锁定笔画的GUID（如果不存在则创建一个默认值）
        private static readonly Guid IsLockGuid = new Guid("12345678-1234-1234-1234-123456789ABC");

        // 橡皮擦视觉反馈控件
        private DrawingVisual eraserVisual = new DrawingVisual();
        private VisualCanvas eraserOverlayCanvas = null;
        private Border eraserVisualBorder = null; // 用于显示橡皮擦视觉反馈的Border

        // 兼容性属性：模拟原有的EraserOverlay_DrawingVisual
        private VisualCanvas EraserOverlay_DrawingVisual => eraserOverlayCanvas;

        // 兼容性保持
        [Obsolete("使用 isUsingAdvancedEraser 替代")]
        public bool isUsingGeometryEraser
        {
            get => isUsingAdvancedEraser;
            set => isUsingAdvancedEraser = value;
        }

        [Obsolete("使用 currentEraserSize 替代")]
        public double eraserWidth
        {
            get => currentEraserSize;
            set => currentEraserSize = value;
        }

        [Obsolete("使用 isCurrentEraserCircle 替代")]
        public bool isEraserCircleShape
        {
            get => isCurrentEraserCircle;
            set => isCurrentEraserCircle = value;
        }

        [Obsolete("使用 isUsingStrokeEraser 替代")]
        public bool isUsingStrokesEraser
        {
            get => isUsingStrokeEraser;
            set => isUsingStrokeEraser = value;
        }

        [Obsolete("使用 eraserTransformMatrix 替代")]
        private Matrix scaleMatrix
        {
            get => eraserTransformMatrix;
            set => eraserTransformMatrix = value;
        }

        /// <summary>
        /// 新橡皮擦覆盖层加载事件处理
        /// </summary>
        private void EraserOverlay_Loaded(object sender, RoutedEventArgs e) {
            var border = (Border)sender;
            
            // 初始化覆盖层
            InitializeEraserOverlay(border);
            
            Trace.WriteLine("Advanced Eraser: Overlay loaded and initialized");
        }

        /// <summary>
        /// 开始高级橡皮擦操作
        /// </summary>
        private void StartAdvancedEraserOperation(object sender) {
            if (isUsingAdvancedEraser) return;

            // 设置操作状态
            isUsingAdvancedEraser = true;
            isEraserVisible = true;

            // 更新橡皮擦尺寸
            UpdateEraserSize();

            // 获取inkCanvas引用
            var inkCanvas = this.FindName("inkCanvas") as InkCanvas;
            if (inkCanvas == null) return;

            // 根据橡皮擦形状创建碰撞检测器
            StylusShape eraserShape = CreateEraserShape();
            advancedHitTester = inkCanvas.Strokes.GetIncrementalStrokeHitTester(eraserShape);
            advancedHitTester.StrokeHit += OnAdvancedEraserStrokeHit;

            // 初始化变换矩阵
            InitializeEraserTransform();
        }

        /// <summary>
        /// 创建橡皮擦形状
        /// </summary>
        private StylusShape CreateEraserShape() {
            if (isCurrentEraserCircle) {
                return new EllipseStylusShape(currentEraserSize, currentEraserSize);
            } else {
                // 矩形橡皮擦，使用与原来相同的逻辑
                return new RectangleStylusShape(currentEraserSize, currentEraserSize / 0.6);
            }
        }

        /// <summary>
        /// 初始化橡皮擦变换矩阵
        /// </summary>
        private void InitializeEraserTransform() {
            eraserTransformMatrix = new Matrix();

            if (isCurrentEraserCircle) {
                // 圆形橡皮擦：等比例缩放
                var scale = currentEraserSize / 56.0; // 基于56x56的基准尺寸
                eraserTransformMatrix.ScaleAt(scale, scale, 0, 0);
            } else {
                // 矩形橡皮擦：保持传统比例
                var scaleX = currentEraserSize / 38.0;
                var scaleY = (currentEraserSize * 56 / 38) / 56.0;
                eraserTransformMatrix.ScaleAt(scaleX, scaleY, 0, 0);
            }
        }

        /// <summary>
        /// 更新橡皮擦尺寸
        /// </summary>
        private void UpdateEraserSize() {
            // 使用与原来相同的逻辑计算橡皮擦尺寸
            double k = 1.0;
            
            switch (Settings.Canvas.EraserSize) {
                case 0: k = Settings.Canvas.EraserShapeType == 0 ? 0.5 : 0.7; break;
                case 1: k = Settings.Canvas.EraserShapeType == 0 ? 0.8 : 0.9; break;
                case 2: k = 1.0; break;
                case 3: k = Settings.Canvas.EraserShapeType == 0 ? 1.25 : 1.2; break;
                case 4: k = Settings.Canvas.EraserShapeType == 0 ? 1.5 : 1.3; break;
            }

            // 更新形状类型
            isCurrentEraserCircle = (Settings.Canvas.EraserShapeType == 0);
            
            // 根据形状类型设置尺寸
            if (isCurrentEraserCircle) {
                currentEraserSize = k * 90; // 圆形橡皮擦
            } else {
                currentEraserSize = k * 90 * 0.6; // 矩形橡皮擦宽度
            }
        }

        /// <summary>
        /// 结束高级橡皮擦操作
        /// </summary>
        private void EndAdvancedEraserOperation(object sender) {
            if (!isUsingAdvancedEraser) return;

            // 重置操作状态
            isUsingAdvancedEraser = false;
            isEraserVisible = false;

            // 释放鼠标捕获
            if (sender is Border border) {
                border.ReleaseMouseCapture();
            }

            // 隐藏橡皮擦视觉反馈
            HideEraserFeedback();

            // 结束碰撞检测
            if (advancedHitTester != null) {
                advancedHitTester.EndHitTesting();
                advancedHitTester = null;
            }

            // 提交橡皮擦历史记录
            CommitEraserHistory();
        }

        /// <summary>
        /// 隐藏橡皮擦视觉反馈
        /// </summary>
        private void HideEraserFeedback() {
            try {
                if (eraserVisualBorder != null) {
                    eraserVisualBorder.Visibility = Visibility.Collapsed;
                }
            } catch (Exception ex) {
                Trace.WriteLine($"Advanced Eraser: Error hiding feedback - {ex.Message}");
            }
        }

        /// <summary>
        /// 提交橡皮擦历史记录
        /// </summary>
        private void CommitEraserHistory() {
            try {
                if (ReplacedStroke != null || AddedStroke != null) {
                    timeMachine.CommitStrokeEraseHistory(ReplacedStroke, AddedStroke);
                    AddedStroke = null;
                    ReplacedStroke = null;
                }
            } catch (Exception ex) {
                Trace.WriteLine($"Advanced Eraser: Error committing history - {ex.Message}");
            }
        }

        /// <summary>
        /// 高级橡皮擦笔画碰撞事件处理
        /// </summary>
        private void OnAdvancedEraserStrokeHit(object sender, StrokeHitEventArgs args) {
            try {
                var inkCanvas = this.FindName("inkCanvas") as InkCanvas;
                if (inkCanvas == null) return;

                var eraseResult = args.GetPointEraseResults();
                var strokeToReplace = new StrokeCollection { args.HitStroke };

                // 过滤锁定的笔画
                var filteredToReplace = strokeToReplace.Where(stroke => !stroke.ContainsPropertyData(IsLockGuid));
                var filteredToReplaceArray = filteredToReplace as Stroke[] ?? filteredToReplace.ToArray();

                if (!filteredToReplaceArray.Any()) return;

                var filteredResult = eraseResult.Where(stroke => !stroke.ContainsPropertyData(IsLockGuid));
                var filteredResultArray = filteredResult as Stroke[] ?? filteredResult.ToArray();

                // 执行笔画替换或删除
                if (filteredResultArray.Any()) {
                    inkCanvas.Strokes.Replace(
                        new StrokeCollection(filteredToReplaceArray),
                        new StrokeCollection(filteredResultArray)
                    );
                } else {
                    inkCanvas.Strokes.Remove(new StrokeCollection(filteredToReplaceArray));
                }
            } catch (Exception ex) {
                Trace.WriteLine($"Advanced Eraser: Error in stroke hit - {ex.Message}");
            }
        }

        /// <summary>
        /// 更新高级橡皮擦位置
        /// </summary>
        private void UpdateAdvancedEraserPosition(object sender, Point position) {
            // 移除isUsingAdvancedEraser检查，让视觉反馈始终更新
            // if (!isUsingAdvancedEraser) return;

            // 性能优化：限制更新频率
            var now = DateTime.Now;
            if ((now - lastEraserUpdate).TotalMilliseconds < ERASER_UPDATE_INTERVAL) {
                return;
            }
            lastEraserUpdate = now;

            // 更新位置
            lastEraserPosition = position;

            // 更新视觉反馈（始终执行）
            UpdateEraserVisualFeedback(position);

            // 只有在实际使用橡皮擦时才处理擦除
            if (isUsingAdvancedEraser) {
                // 处理不同的橡皮擦模式
                if (isUsingStrokeEraser) {
                    ProcessStrokeEraserAtPosition(position);
                } else {
                    ProcessGeometryEraserAtPosition(position);
                }
            }
        }

        /// <summary>
        /// 在指定位置处理笔画橡皮擦
        /// </summary>
        private void ProcessStrokeEraserAtPosition(Point position) {
            try {
                var inkCanvas = this.FindName("inkCanvas") as InkCanvas;
                if (inkCanvas == null) return;

                var hitStrokes = inkCanvas.Strokes.HitTest(position)
                    .Where(stroke => !stroke.ContainsPropertyData(IsLockGuid));
                var strokesArray = hitStrokes as Stroke[] ?? hitStrokes.ToArray();

                if (strokesArray.Any()) {
                    inkCanvas.Strokes.Remove(new StrokeCollection(strokesArray));
                }
            } catch (Exception ex) {
                Trace.WriteLine($"Advanced Eraser: Error in stroke eraser - {ex.Message}");
            }
        }

        /// <summary>
        /// 在指定位置处理几何橡皮擦
        /// </summary>
        private void ProcessGeometryEraserAtPosition(Point position) {
            try {
                if (advancedHitTester != null) {
                    advancedHitTester.AddPoint(position);
                }
            } catch (Exception ex) {
                Trace.WriteLine($"Advanced Eraser: Error in geometry eraser - {ex.Message}");
            }
        }

        /// <summary>
        /// 更新橡皮擦视觉反馈
        /// </summary>
        private void UpdateEraserVisualFeedback(Point position) {
            try {
                // 获取或创建橡皮擦视觉反馈Border
                if (eraserVisualBorder == null) {
                    eraserVisualBorder = new Border {
                        Background = new SolidColorBrush(Colors.Transparent),
                        BorderBrush = new SolidColorBrush(Colors.Transparent),
                        BorderThickness = new Thickness(0),
                        IsHitTestVisible = false,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top,
                        Opacity = 1
                    };
                    Panel.SetZIndex(eraserVisualBorder, 1001);
                    
                    // 将Border添加到InkCanvasGridForInkReplay中
                    var inkCanvasGrid = this.FindName("InkCanvasGridForInkReplay") as Grid;
                    if (inkCanvasGrid != null) {
                        inkCanvasGrid.Children.Add(eraserVisualBorder);
                        Trace.WriteLine("Advanced Eraser: Visual feedback border added to grid");
                    } else {
                        Trace.WriteLine("Advanced Eraser: Failed to find InkCanvasGridForInkReplay");
                        return; // 如果找不到Grid，直接返回
                    }
                }

                if (eraserVisualBorder != null) {
                    // 创建橡皮擦视觉反馈
                    var eraserImage = CreateEraserVisualImage();
                    
                    // 清除Border的内容并添加新的图像
                    eraserVisualBorder.Child = eraserImage;
                    
                    // 更新橡皮擦位置和大小
                    if (isCurrentEraserCircle) {
                        var radius = currentEraserSize / 2;
                        eraserVisualBorder.Width = currentEraserSize;
                        eraserVisualBorder.Height = currentEraserSize;
                        
                        // 使用Margin来定位，因为Border在Grid中
                        eraserVisualBorder.Margin = new Thickness(
                            position.X - radius, 
                            position.Y - radius, 
                            0, 0);
                    } else {
                        // 矩形橡皮擦，使用与原来相同的逻辑
                        var height = currentEraserSize / 0.6;
                        eraserVisualBorder.Width = currentEraserSize;
                        eraserVisualBorder.Height = height;
                        
                        // 使用Margin来定位，因为Border在Grid中
                        eraserVisualBorder.Margin = new Thickness(
                            position.X - currentEraserSize / 2, 
                            position.Y - height / 2, 
                            0, 0);
                    }
                    
                    eraserVisualBorder.Visibility = Visibility.Visible;
                    Trace.WriteLine($"Advanced Eraser: Visual feedback updated to ({position.X:F1}, {position.Y:F1})");
                }
            } catch (Exception ex) {
                Trace.WriteLine($"Advanced Eraser: Error updating visual feedback - {ex.Message}");
            }
        }

        /// <summary>
        /// 创建橡皮擦视觉图像
        /// </summary>
        private Image CreateEraserVisualImage() {
            try {
                // 根据橡皮擦形状选择对应的DrawingGroup资源
                string resourceKey = isCurrentEraserCircle ? "EraserCircleDrawingGroup" : "EraserDrawingGroup";
                
                // 尝试从资源字典中获取DrawingGroup
                var drawingGroup = this.TryFindResource(resourceKey) as DrawingGroup;
                if (drawingGroup == null) {
                    // 如果找不到资源，创建默认的橡皮擦图像
                    return CreateDefaultEraserImage();
                }

                // 创建变换后的DrawingGroup
                var transformedGroup = new DrawingGroup();
                transformedGroup.Children.Add(drawingGroup);
                
                // 应用缩放变换
                var transform = new ScaleTransform();
                if (isCurrentEraserCircle) {
                    var scale = currentEraserSize / 56.0; // 基于56x56的基准尺寸
                    transform.ScaleX = scale;
                    transform.ScaleY = scale;
                } else {
                    var scaleX = currentEraserSize / 38.0;
                    var scaleY = (currentEraserSize / 0.6) / 56.0;
                    transform.ScaleX = scaleX;
                    transform.ScaleY = scaleY;
                }
                transformedGroup.Transform = transform;

                // 创建DrawingImage
                var drawingImage = new DrawingImage(transformedGroup);
                
                // 创建Image控件
                var image = new Image {
                    Source = drawingImage,
                    Stretch = Stretch.None
                };
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

                return image;
            } catch (Exception ex) {
                Trace.WriteLine($"Advanced Eraser: Error creating eraser visual image - {ex.Message}");
                return CreateDefaultEraserImage();
            }
        }

        /// <summary>
        /// 创建默认的橡皮擦图像（当资源不可用时）
        /// </summary>
        private Image CreateDefaultEraserImage() {
            try {
                // 创建一个简单的几何图形作为默认橡皮擦
                Geometry geometry;
                if (isCurrentEraserCircle) {
                    geometry = new EllipseGeometry(new Point(28, 28), 28, 28);
                } else {
                    geometry = new RectangleGeometry(new Rect(0, 0, 38, 56));
                }

                var brush = new SolidColorBrush(Colors.LightGray);
                var pen = new Pen(new SolidColorBrush(Colors.DarkGray), 1);

                var geometryDrawing = new GeometryDrawing(brush, pen, geometry);
                var drawingGroup = new DrawingGroup();
                drawingGroup.Children.Add(geometryDrawing);

                // 应用缩放变换
                var transform = new ScaleTransform();
                if (isCurrentEraserCircle) {
                    var scale = currentEraserSize / 56.0;
                    transform.ScaleX = scale;
                    transform.ScaleY = scale;
                } else {
                    var scaleX = currentEraserSize / 38.0;
                    var scaleY = (currentEraserSize / 0.6) / 56.0;
                    transform.ScaleX = scaleX;
                    transform.ScaleY = scaleY;
                }
                drawingGroup.Transform = transform;

                var drawingImage = new DrawingImage(drawingGroup);
                var image = new Image {
                    Source = drawingImage,
                    Stretch = Stretch.None
                };

                return image;
            } catch (Exception ex) {
                Trace.WriteLine($"Advanced Eraser: Error creating default eraser image - {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 兼容性方法：旧版橡皮擦几何碰撞处理
        /// </summary>
        [Obsolete("使用 OnAdvancedEraserStrokeHit 替代")]
        private void EraserGeometry_StrokeHit(object sender, StrokeHitEventArgs args) {
            OnAdvancedEraserStrokeHit(sender, args);
        }

        /// <summary>
        /// 兼容性方法：旧版橡皮擦移动处理
        /// </summary>
        [Obsolete("使用 UpdateAdvancedEraserPosition 替代")]
        private void EraserOverlay_PointerMove(object sender, Point pt) {
            UpdateAdvancedEraserPosition(sender, pt);
        }

        /// <summary>
        /// 兼容性方法：旧版橡皮擦按下处理
        /// </summary>
        [Obsolete("使用 StartAdvancedEraserOperation 替代")]
        private void EraserOverlay_PointerDown(object sender) {
            StartAdvancedEraserOperation(sender);
        }

        /// <summary>
        /// 兼容性方法：旧版橡皮擦抬起处理
        /// </summary>
        [Obsolete("使用 EndAdvancedEraserOperation 替代")]
        private void EraserOverlay_PointerUp(object sender) {
            EndAdvancedEraserOperation(sender);
        }

        /// <summary>
        /// 获取当前橡皮擦状态信息（用于调试）
        /// </summary>
        public string GetEraserStatusInfo() {
            return $"Advanced Eraser Status:\n" +
                   $"- Active: {isUsingAdvancedEraser}\n" +
                   $"- Size: {currentEraserSize:F1}\n" +
                   $"- Shape: {(isCurrentEraserCircle ? "Circle" : "Rectangle")}\n" +
                   $"- Mode: {(isUsingStrokeEraser ? "Stroke" : "Geometry")}\n" +
                   $"- Visible: {isEraserVisible}\n" +
                   $"- Last Position: ({lastEraserPosition.X:F1}, {lastEraserPosition.Y:F1})";
        }

        /// <summary>
        /// 重置橡皮擦状态
        /// </summary>
        public void ResetEraserState() {
            isUsingAdvancedEraser = false;
            isEraserVisible = false;
            lastEraserPosition = new Point();

            if (advancedHitTester != null) {
                advancedHitTester.EndHitTesting();
                advancedHitTester = null;
            }

            HideEraserFeedback();
            
            // 清理视觉反馈Border
            if (eraserVisualBorder != null) {
                var inkCanvasGrid = this.FindName("InkCanvasGridForInkReplay") as Grid;
                if (inkCanvasGrid != null) {
                    inkCanvasGrid.Children.Remove(eraserVisualBorder);
                }
                eraserVisualBorder = null;
            }
        }

        /// <summary>
        /// 应用高级橡皮擦形状到InkCanvas
        /// </summary>
        public void ApplyAdvancedEraserShape() {
            try {
                var inkCanvas = this.FindName("inkCanvas") as InkCanvas;
                if (inkCanvas == null) return;

                // 更新橡皮擦尺寸和形状
                UpdateEraserSize();

                // 创建橡皮擦形状
                StylusShape eraserShape = CreateEraserShape();

                // 应用到InkCanvas
                inkCanvas.EraserShape = eraserShape;

                Trace.WriteLine($"Advanced Eraser: Applied shape - Size: {currentEraserSize}, Circle: {isCurrentEraserCircle}");
            } catch (Exception ex) {
                Trace.WriteLine($"Advanced Eraser: Error applying shape - {ex.Message}");

                // 回退到传统方法
                try {
                    ApplyCurrentEraserShape();
                } catch (Exception fallbackEx) {
                    Trace.WriteLine($"Advanced Eraser: Fallback also failed - {fallbackEx.Message}");
                }
            }
        }

        /// <summary>
        /// 启用高级橡皮擦系统
        /// </summary>
        public void EnableAdvancedEraserSystem() {
            try {
                // 获取橡皮擦覆盖层
                var eraserOverlay = this.FindName("AdvancedEraserOverlay") as Border;
                if (eraserOverlay != null) {
                    // 启用覆盖层的交互
                    eraserOverlay.IsHitTestVisible = true;
                    
                    // 确保覆盖层在橡皮擦模式下启用
                    if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint) {
                        eraserOverlay.IsHitTestVisible = true;
                        Trace.WriteLine("Advanced Eraser: Overlay enabled for eraser mode");
                    }
                    
                    // 设置覆盖层的大小以覆盖整个InkCanvas
                    var inkCanvasControl = this.FindName("inkCanvas") as InkCanvas;
                    if (inkCanvasControl != null) {
                        eraserOverlay.Width = inkCanvasControl.ActualWidth;
                        eraserOverlay.Height = inkCanvasControl.ActualHeight;
                        Trace.WriteLine($"Advanced Eraser: Overlay size set to {eraserOverlay.Width}x{eraserOverlay.Height}");
                    }
                    
                    Trace.WriteLine("Advanced Eraser: System enabled successfully");
                } else {
                    Trace.WriteLine("Advanced Eraser: Failed to find eraser overlay");
                }
            } catch (Exception ex) {
                Trace.WriteLine($"Advanced Eraser: Error enabling system - {ex.Message}");
            }
        }

        /// <summary>
        /// 初始化橡皮擦覆盖层
        /// </summary>
        private void InitializeEraserOverlay(Border overlay) {
            try {
                // 设置覆盖层的基本属性
                overlay.Background = new SolidColorBrush(Colors.Transparent);
                overlay.IsHitTestVisible = false; // 默认禁用，只在橡皮擦模式下启用
                
                // 绑定事件处理
                overlay.MouseDown += (sender, e) => {
                    if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint) {
                        overlay.CaptureMouse();
                        StartAdvancedEraserOperation(sender);
                    }
                };
                
                overlay.MouseUp += (sender, e) => {
                    if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint) {
                        overlay.ReleaseMouseCapture();
                        EndAdvancedEraserOperation(sender);
                    }
                };
                
                overlay.MouseMove += (sender, e) => {
                    if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint) {
                        var position = e.GetPosition((UIElement)this.FindName("inkCanvas"));
                        Trace.WriteLine($"Advanced Eraser: Mouse move event triggered at ({position.X:F1}, {position.Y:F1})");
                        UpdateAdvancedEraserPosition(sender, position);
                    } else {
                        Trace.WriteLine($"Advanced Eraser: Mouse move ignored - not in eraser mode, current mode: {inkCanvas.EditingMode}");
                    }
                };
                
                // 触控笔事件
                overlay.StylusDown += (sender, e) => {
                    if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint) {
                        e.Handled = true;
                        if (e.StylusDevice.TabletDevice.Type == TabletDeviceType.Stylus) {
                            overlay.CaptureStylus();
                        }
                        StartAdvancedEraserOperation(sender);
                    }
                };
                
                overlay.StylusUp += (sender, e) => {
                    if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint) {
                        e.Handled = true;
                        if (e.StylusDevice.TabletDevice.Type == TabletDeviceType.Stylus) {
                            overlay.ReleaseStylusCapture();
                        }
                        EndAdvancedEraserOperation(sender);
                    }
                };
                
                overlay.StylusMove += (sender, e) => {
                    if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint) {
                        e.Handled = true;
                        var position = e.GetPosition((UIElement)this.FindName("inkCanvas"));
                        UpdateAdvancedEraserPosition(sender, position);
                        Trace.WriteLine($"Advanced Eraser: Stylus move at ({position.X:F1}, {position.Y:F1})");
                    }
                };
                
                Trace.WriteLine("Advanced Eraser: Overlay initialized successfully");
            } catch (Exception ex) {
                Trace.WriteLine($"Advanced Eraser: Error initializing overlay - {ex.Message}");
            }
        }

        /// <summary>
        /// 禁用高级橡皮擦系统
        /// </summary>
        public void DisableAdvancedEraserSystem() {
            try {
                // 重置橡皮擦状态
                ResetEraserState();
                
                // 获取橡皮擦覆盖层并禁用
                var eraserOverlay = this.FindName("AdvancedEraserOverlay") as Border;
                if (eraserOverlay != null) {
                    eraserOverlay.IsHitTestVisible = false;
                }
                
                // 确保视觉反馈被隐藏
                HideEraserFeedback();
                
                Trace.WriteLine("Advanced Eraser: System disabled successfully");
            } catch (Exception ex) {
                Trace.WriteLine($"Advanced Eraser: Error disabling system - {ex.Message}");
            }
        }

        /// <summary>
        /// 切换橡皮擦形状（圆形/矩形）
        /// </summary>
        public void ToggleEraserShape() {
            isCurrentEraserCircle = !isCurrentEraserCircle;

            // 更新设置
            Settings.Canvas.EraserShapeType = isCurrentEraserCircle ? 0 : 1;

            // 应用新形状
            ApplyAdvancedEraserShape();

            Trace.WriteLine($"Advanced Eraser: Toggled to {(isCurrentEraserCircle ? "Circle" : "Rectangle")}");
        }       
    }
}
