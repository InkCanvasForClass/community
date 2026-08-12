using Ink_Canvas.Properties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas
{
    public partial class MainWindow : Ink_Canvas.Helpers.PerformanceTransparentWin
    {
        // 橡皮擦系统核心变量
        public bool isUsingGeometryEraser = false;
        private IncrementalStrokeHitTester hitTester = null;

        public double eraserWidth = 64;
        public bool isEraserCircleShape = false;
        public bool isUsingStrokesEraser = false;
        private bool _secAgentStrokeEraseActive;
        private readonly Dictionary<FrameworkElement, string> _secAgentEraseInitialStates = new();

        private Matrix scaleMatrix = new Matrix();

        // 橡皮擦覆盖层相关控件
        private System.Windows.Controls.Canvas eraserOverlayCanvas;
        private Image eraserFeedback;
        private TranslateTransform eraserFeedbackTranslateTransform;

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
            canvas.StylusDown += ((o, args) =>
            {
                args.Handled = true;
                if (args.StylusDevice.TabletDevice.Type == TabletDeviceType.Stylus) canvas.CaptureStylus();
                EraserOverlay_PointerDown(sender);
            });
            canvas.StylusUp += ((o, args) =>
            {
                args.Handled = true;
                if (args.StylusDevice.TabletDevice.Type == TabletDeviceType.Stylus) canvas.ReleaseStylusCapture();
                EraserOverlay_PointerUp(sender);
            });
            canvas.StylusMove += ((o, args) =>
            {
                args.Handled = true;
                EraserOverlay_PointerMove(sender, args.GetPosition(inkCanvas));
            });
            canvas.MouseDown += ((o, args) =>
            {
                args.Handled = true;
                canvas.CaptureMouse();
                EraserOverlay_PointerDown(sender);
            });
            canvas.MouseUp += ((o, args) =>
            {
                args.Handled = true;
                canvas.ReleaseMouseCapture();
                EraserOverlay_PointerUp(sender);
            });
            canvas.MouseMove += ((o, args) =>
            {
                args.Handled = true;
                EraserOverlay_PointerMove(sender, args.GetPosition(inkCanvas));
            });
            // Touch is not guaranteed to promote to the overlay's stylus events on
            // every tablet driver. Handle it directly so the area eraser uses the same
            // scene geometry and history path for finger/touch input as for mouse/pen.
            canvas.TouchDown += ((o, args) =>
            {
                args.Handled = true;
                canvas.CaptureTouch(args.TouchDevice);
                EraserOverlay_PointerDown(sender);
            });
            canvas.TouchMove += ((o, args) =>
            {
                args.Handled = true;
                EraserOverlay_PointerMove(sender, args.GetTouchPoint(inkCanvas).Position);
            });
            canvas.TouchUp += ((o, args) =>
            {
                args.Handled = true;
                canvas.ReleaseTouchCapture(args.TouchDevice);
                EraserOverlay_PointerUp(sender);
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
            var imageSource = TryFindResource(resourceKey) as DrawingImage;

            if (imageSource != null)
            {
                eraserFeedback.Source = imageSource;
            }
        }

        /// <summary>
        /// 橡皮擦按下事件处理
        /// </summary>
        private void EraserOverlay_PointerDown(object sender)
        {
            _secAgentEraseInitialStates.Clear();
            if (currentSelectedElement != null)
            {
                currentSelectedElement.ReleaseMouseCapture();
                UnselectElement(currentSelectedElement);
                currentSelectedElement = null;
            }
            if (TryBlockFrozenPageMutation("擦除冻结页面")) return;
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
                CommitPendingSecAgentEraseHistory();
            }

            // 提交橡皮擦历史记录
            CommitPendingSecAgentEraseHistory();
            CommitPendingGeometryEraseHistory();

            // 橡皮擦自动切换回批注
            HandleEraserOperationEnded();
        }

        private void CommitPendingGeometryEraseHistory()
        {
            if (ReplacedStroke == null && AddedStroke == null) return;

            timeMachine.CommitStrokeEraseHistory(ReplacedStroke, AddedStroke);
            MarkCurrentPageInkChanged();
            AddedStroke = null;
            ReplacedStroke = null;
        }

        /// <summary>
        /// 橡皮擦移动事件处理
        /// </summary>
        private void EraserOverlay_PointerMove(object sender, Point pt)
        {
            if (TryBlockFrozenPageMutation("擦除冻结页面")) return;
            if (!isUsingGeometryEraser) return;

            EraseSecAgentSceneElementsAt(pt);

            if (isUsingStrokesEraser)
            {
                // 笔画橡皮擦模式
                var _filtered = inkCanvas.Strokes.HitTest(pt).Where(stroke => !stroke.ContainsPropertyData(FrozenStrokePropertyGuid));
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
        /// Editable SVG scenes are represented as ordinary InkCanvas children rather than ink strokes.
        /// Apply the same eraser footprint to them, deleting only the independently inserted row/line/shape hit.
        /// </summary>
        private void EraseSecAgentSceneElementsAt(Point point)
        {
            if (inkCanvas == null) return;
            var halfWidth = Math.Max(5, eraserWidth / 2);
            var halfHeight = isEraserCircleShape ? halfWidth : Math.Max(5, eraserWidth * 56 / 38 / 2);
            var eraserBounds = new Rect(point.X - halfWidth, point.Y - halfHeight, halfWidth * 2, halfHeight * 2);
            var candidates = EnumerateSecAgentEditableSceneElements()
                .Where(element => IsSecAgentSceneHit(element, point, eraserBounds))
                .ToArray();
            if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint)
            {
                foreach (var candidate in candidates)
                    EraseSecAgentSceneArea(candidate, eraserBounds);
                return;
            }

            RemoveSecAgentSceneElements(candidates, true);
        }

        private bool EraseSecAgentSceneArea(FrameworkElement element, Rect canvasRectangle)
        {
            try
            {
                var owner = GetSecAgentHistoryOwner(element);
                var before = ReadSecAgentState(owner);
                if (owner != null && !string.IsNullOrWhiteSpace(before) && !_secAgentEraseInitialStates.ContainsKey(owner))
                    _secAgentEraseInitialStates[owner] = before;
                var inverse = element.TransformToAncestor(inkCanvas).Inverse;
                if (inverse is null) return false;
                var localRectangle = inverse.TransformBounds(canvasRectangle);
                var method = element.GetType().GetMethod("EraseLocalRect", new[] { typeof(Rect), typeof(double) });
                if (method?.Invoke(element, new object[] { localRectangle, 4d }) is not bool changed || !changed)
                    return false;

                var hasContent = element.GetType().GetProperty("HasVisualContent")?.GetValue(element) is bool value && value;
                if (!hasContent)
                    RemoveSecAgentSceneElements(new[] { element }, false);
                else
                    MarkCurrentPageInkChanged();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void CommitPendingSecAgentEraseHistory()
        {
            foreach (var pair in _secAgentEraseInitialStates.ToArray())
            {
                var after = ReadSecAgentState(pair.Key);
                if (!string.IsNullOrWhiteSpace(after) && !string.Equals(pair.Value, after, StringComparison.Ordinal))
                    timeMachine.CommitElementEditHistory(pair.Key, pair.Value, after);
            }
            _secAgentEraseInitialStates.Clear();
        }

        private FrameworkElement GetSecAgentHistoryOwner(FrameworkElement element)
        {
            DependencyObject current = element;
            while (current != null)
            {
                if (current is FrameworkElement framework && IsSecAgentEditableSceneGroup(framework))
                    return framework;
                current = VisualTreeHelper.GetParent(current);
            }
            return element;
        }

        private static string ReadSecAgentState(FrameworkElement element)
        {
            if (element == null) return null;
            var property = element.GetType().GetProperty("SerializedScene")
                ?? element.GetType().GetProperty("SerializedElement");
            return property?.GetValue(element) as string;
        }

        /// <summary>
        /// The built-in EraseByStroke path only knows about InkCanvas.Strokes and never sees
        /// SvgSceneElement children. These hooks mirror that mode for inserted SVG paths.
        /// </summary>
        internal bool BeginSecAgentStrokeErase(Point point)
        {
            if (inkCanvas?.EditingMode != InkCanvasEditingMode.EraseByStroke) return false;
            _secAgentStrokeEraseActive = true;
            return EraseSecAgentSceneElementAtPoint(point);
        }

        internal bool MoveSecAgentStrokeErase(Point point)
        {
            if (!_secAgentStrokeEraseActive) return false;
            return EraseSecAgentSceneElementAtPoint(point);
        }

        internal void EndSecAgentStrokeErase()
        {
            _secAgentStrokeEraseActive = false;
        }

        private bool EraseSecAgentSceneElementAtPoint(Point point)
        {
            var targets = inkCanvas is null ? null : EnumerateSecAgentEditableSceneElements()
                .Where(element => IsSecAgentScenePointHit(element, point))
                .ToArray();
            if (targets is null || targets.Length == 0) return false;

            // Keep grouped SVG items as one serialized edit. Recording a child removal as a
            // normal top-level ElementInsert history would restore that child outside its
            // SvgSceneGroup on undo, especially when the last child also removes the group.
            var groupedTargets = targets
                .Select(element => new { Element = element, Group = FindSecAgentSceneGroup(element) })
                .Where(item => item.Group != null)
                .GroupBy(item => item.Group)
                .ToArray();
            var groupedStates = groupedTargets
                .Select(group => new
                {
                    Group = group.Key,
                    Before = ReadSecAgentState(group.Key),
                    Elements = group.Select(item => item.Element).ToArray()
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.Before))
                .ToArray();

            var directTargets = targets
                .Where(element => FindSecAgentSceneGroup(element) == null)
                .ToArray();
            RemoveSecAgentSceneElements(directTargets, true);
            foreach (var groupedState in groupedStates)
                RemoveSecAgentSceneElements(groupedState.Elements, false);

            foreach (var groupedState in groupedStates)
            {
                var after = ReadSecAgentState(groupedState.Group);
                if (!string.IsNullOrWhiteSpace(after))
                    timeMachine.CommitElementEditHistory(groupedState.Group, groupedState.Before, after);
            }
            return true;
        }

        private bool IsSecAgentScenePointHit(FrameworkElement element, Point canvasPoint)
        {
            try
            {
                var inverse = element.TransformToAncestor(inkCanvas).Inverse;
                if (inverse is not null)
                    return InvokeSceneHitTest(element, "HitTestLocalPoint", inverse.Transform(canvasPoint), 4d);
            }
            catch
            {
                // Use the conservative bounds fallback while an element is being laid out.
            }
            return GetSceneElementBounds(element).Contains(canvasPoint);
        }

        private static bool IsSecAgentEditableSceneElement(FrameworkElement element)
        {
            var type = element?.GetType();
            return string.Equals(type?.FullName, "Ink_Canvas.SecAgent.Plugin.SvgSceneElement", StringComparison.Ordinal)
                || string.Equals(type?.Name, "SvgSceneElement", StringComparison.Ordinal)
                || element?.Name?.StartsWith("svgscene_", StringComparison.OrdinalIgnoreCase) == true;
        }

        private static bool IsSecAgentEditableSceneGroup(FrameworkElement element)
        {
            var type = element?.GetType();
            return string.Equals(type?.FullName, "Ink_Canvas.SecAgent.Plugin.SvgSceneGroup", StringComparison.Ordinal)
                || string.Equals(type?.Name, "SvgSceneGroup", StringComparison.Ordinal)
                || element?.Name?.StartsWith("svgscene_group_", StringComparison.OrdinalIgnoreCase) == true;
        }

        internal bool HasSecAgentSceneElementsOnCanvas()
        {
            return inkCanvas != null && inkCanvas.Children.OfType<FrameworkElement>()
                .Any(element => IsSecAgentEditableSceneElement(element) || IsSecAgentEditableSceneGroup(element));
        }

        private static FrameworkElement FindSecAgentSceneGroup(FrameworkElement element)
        {
            DependencyObject current = element;
            while (current != null)
            {
                if (current is FrameworkElement framework && IsSecAgentEditableSceneGroup(framework))
                    return framework;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private static bool HasSecAgentSceneElements(FrameworkElement group)
        {
            if (group == null) return false;
            var method = group.GetType().GetMethod("GetSceneElements", Type.EmptyTypes);
            if (method?.Invoke(group, null) is not System.Collections.IEnumerable children)
                return false;

            foreach (var child in children)
                if (child is FrameworkElement framework && IsSecAgentEditableSceneElement(framework))
                    return true;
            return false;
        }

        private IEnumerable<FrameworkElement> EnumerateSecAgentEditableSceneElements()
        {
            if (inkCanvas == null) yield break;
            foreach (var child in inkCanvas.Children.OfType<FrameworkElement>())
            {
                if (IsSecAgentEditableSceneElement(child))
                {
                    yield return child;
                    continue;
                }

                if (!IsSecAgentEditableSceneGroup(child)) continue;
                var method = child.GetType().GetMethod("GetSceneElements", Type.EmptyTypes);
                if (method?.Invoke(child, null) is not System.Collections.IEnumerable nested) continue;
                foreach (var item in nested)
                    if (item is FrameworkElement element && IsSecAgentEditableSceneElement(element))
                        yield return element;
            }
        }

        private bool IsSecAgentSceneHit(FrameworkElement element, Point canvasPoint, Rect canvasRect)
        {
            try
            {
                var toCanvas = element.TransformToAncestor(inkCanvas);
                var inverse = toCanvas.Inverse;
                if (inverse is not null)
                {
                    var localPoint = inverse.Transform(canvasPoint);
                    if (inkCanvas.EditingMode == InkCanvasEditingMode.EraseByStroke)
                        return InvokeSceneHitTest(element, "HitTestLocalPoint", localPoint, 4d);

                    var localRect = inverse.TransformBounds(canvasRect);
                    return InvokeSceneRectTest(element, "IntersectsLocalRect", localRect, 4d);
                }
            }
            catch
            {
                // Fall through to the conservative layout-bounds check for elements that are
                // not measured yet or have an unavailable transform during page transitions.
            }
            return canvasRect.IntersectsWith(GetSceneElementBounds(element));
        }

        private static bool InvokeSceneHitTest(FrameworkElement element, string methodName, Point point, double tolerance)
        {
            var method = element.GetType().GetMethod(methodName, new[] { typeof(Point), typeof(double) });
            return method?.Invoke(element, new object[] { point, tolerance }) is bool result && result;
        }

        private static bool InvokeSceneRectTest(FrameworkElement element, string methodName, Rect rectangle, double tolerance)
        {
            var method = element.GetType().GetMethod(methodName, new[] { typeof(Rect), typeof(double) });
            return method?.Invoke(element, new object[] { rectangle, tolerance }) is bool result && result;
        }

        private void RemoveSecAgentSceneElements(IEnumerable<FrameworkElement> elements, bool recordHistory)
        {
            var targets = elements?.Where(element => element != null).Distinct().ToArray();
            if (targets is null || targets.Length == 0) return;
            foreach (var target in targets)
            {
                var isDirectChild = inkCanvas.Children.Contains(target);
                var parentPanel = target.Parent as Panel;
                var ownerGroup = FindSecAgentSceneGroup(target);
                if (!isDirectChild && parentPanel is null) continue;
                if (ReferenceEquals(currentSelectedElement, target))
                {
                    UnselectElement(currentSelectedElement);
                    currentSelectedElement = null;
                }
                if (recordHistory) timeMachine.CommitElementRemoveHistory(target);
                if (isDirectChild)
                    inkCanvas.Children.Remove(target);
                else
                    parentPanel.Children.Remove(target);

                // A scene element normally lives inside the group's private Canvas, so the
                // immediate parent is not the group itself. Remove the outer group as soon as
                // its last visible child is gone; otherwise an empty transparent hit box would
                // remain on the whiteboard and continue intercepting selection/eraser input.
                if (ownerGroup is not null && !HasSecAgentSceneElements(ownerGroup))
                {
                    if (ReferenceEquals(currentSelectedElement, ownerGroup))
                    {
                        UnselectElement(currentSelectedElement);
                        currentSelectedElement = null;
                    }
                    if (recordHistory) timeMachine.CommitElementRemoveHistory(ownerGroup);
                    if (inkCanvas.Children.Contains(ownerGroup))
                        inkCanvas.Children.Remove(ownerGroup);
                }
            }
            MarkCurrentPageInkChanged();
        }

        /// <summary>
        /// Removes editable SecAgent scene items when the canvas is cleared. These items live in
        /// InkCanvas.Children, so clearing InkCanvas.Strokes alone cannot remove them.
        /// </summary>
        private void ClearSecAgentSceneElements()
        {
            if (inkCanvas == null) return;
            var targets = inkCanvas.Children.OfType<FrameworkElement>()
                .Where(element => IsSecAgentEditableSceneElement(element) || IsSecAgentEditableSceneGroup(element))
                .ToArray();
            Debug.WriteLine($"ClearSecAgentSceneElements: found {targets.Length} direct scene element(s).");
            RemoveSecAgentSceneElements(targets, false);
            // A selection/host integration can temporarily reparent an inserted item. Remove
            // any remaining direct scene child as a final invariant for toolbar clear actions.
            foreach (var child in inkCanvas.Children.OfType<FrameworkElement>().ToArray())
            {
                if (IsSecAgentEditableSceneElement(child) || IsSecAgentEditableSceneGroup(child))
                    inkCanvas.Children.Remove(child);
            }
        }

        private Rect GetSceneElementBounds(FrameworkElement element)
        {
            var width = !double.IsNaN(element.ActualWidth) && element.ActualWidth > 0 ? element.ActualWidth : element.Width;
            var height = !double.IsNaN(element.ActualHeight) && element.ActualHeight > 0 ? element.ActualHeight : element.Height;
            var localBounds = new Rect(0, 0, Math.Max(1, width), Math.Max(1, height));
            try { return element.TransformToAncestor(inkCanvas).TransformBounds(localBounds); }
            catch
            {
                var left = InkCanvas.GetLeft(element);
                var top = InkCanvas.GetTop(element);
                return new Rect(double.IsNaN(left) ? 0 : left, double.IsNaN(top) ? 0 : top, localBounds.Width, localBounds.Height);
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
            var filtered_2replace = strokesToReplace.Where(stroke => !stroke.ContainsPropertyData(FrozenStrokePropertyGuid));
            var filtered2Replace = filtered_2replace as Stroke[] ?? filtered_2replace.ToArray();
            if (!filtered2Replace.Any()) return;

            var filtered_result = eraseResult.Where(stroke => !stroke.ContainsPropertyData(FrozenStrokePropertyGuid));
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
            // An inserted SVG is selected immediately after insertion. Its image-style
            // selection overlay is a sibling above EraserOverlayCanvas and would otherwise
            // consume the pointer before the area eraser can receive it.
            if (currentSelectedElement != null)
            {
                currentSelectedElement.ReleaseMouseCapture();
                UnselectElement(currentSelectedElement);
                currentSelectedElement = null;
            }

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

            CommitPendingGeometryEraseHistory();
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
                   $"- 模式: {(isUsingStrokesEraser ? "笔画" : FloatingBarStrings.FloatingBar_Geometry)}";
        }
    }
}
