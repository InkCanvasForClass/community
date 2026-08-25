using Ink_Canvas.Controls;
using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas
{
    public partial class MainWindow : Ink_Canvas.Helpers.PerformanceTransparentWin
    {
        /// <summary>
        /// 提交原因枚举，用于标识不同类型的操作
        /// </summary>
        private enum CommitReason
        {
            /// <summary>用户输入操作</summary>
            UserInput,
            /// <summary>代码输入操作</summary>
            CodeInput,
            /// <summary>形状绘制操作</summary>
            ShapeDrawing,
            /// <summary>形状识别操作</summary>
            ShapeRecognition,
            /// <summary>清除画布操作</summary>
            ClearingCanvas,
            /// <summary>笔画操作操作</summary>
            Manipulation
        }

        /// <summary>
        /// 当前提交类型
        /// </summary>
        private CommitReason _currentCommitType = CommitReason.UserInput;

        /// <summary>
        /// 是否为点橡皮擦模式
        /// </summary>
        private bool IsEraseByPoint => inkCanvas.EditingMode == InkCanvasEditingMode.EraseByPoint;

        /// <summary>
        /// 替换的笔画集合
        /// </summary>
        private StrokeCollection ReplacedStroke;

        /// <summary>
        /// 添加的笔画集合
        /// </summary>
        private StrokeCollection AddedStroke;

        /// <summary>
        /// 长方体笔画集合
        /// </summary>
        private StrokeCollection CuboidStrokeCollection;

        /// <summary>
        /// 笔画操作历史记录
        /// </summary>
        private Dictionary<Stroke, Tuple<StylusPointCollection, StylusPointCollection>> StrokeManipulationHistory;

        /// <summary>
        /// 笔画初始状态历史记录
        /// </summary>
        private Dictionary<Stroke, StylusPointCollection> StrokeInitialHistory =
            new Dictionary<Stroke, StylusPointCollection>();

        /// <summary>
        /// 绘制属性历史记录
        /// </summary>
        private Dictionary<Stroke, Tuple<DrawingAttributes, DrawingAttributes>> DrawingAttributesHistory =
            new Dictionary<Stroke, Tuple<DrawingAttributes, DrawingAttributes>>();

        /// <summary>
        /// 绘制属性历史记录标志
        /// </summary>
        private Dictionary<Guid, List<Stroke>> DrawingAttributesHistoryFlag = new Dictionary<Guid, List<Stroke>> {
            { DrawingAttributeIds.Color, new List<Stroke>() },
            { DrawingAttributeIds.DrawingFlags, new List<Stroke>() },
            { DrawingAttributeIds.IsHighlighter, new List<Stroke>() },
            { DrawingAttributeIds.StylusHeight, new List<Stroke>() },
            { DrawingAttributeIds.StylusTip, new List<Stroke>() },
            { DrawingAttributeIds.StylusTipTransform, new List<Stroke>() },
            { DrawingAttributeIds.StylusWidth, new List<Stroke>() }
        };

        /// <summary>
        /// 时间机器实例，用于撤销/重做操作
        /// </summary>
        private TimeMachine timeMachine = new TimeMachine();

        /// <summary>
        /// 将历史记录应用到画布
        /// </summary>
        /// <param name="item">时间机器历史记录项</param>
        /// <param name="applyCanvas">要应用的画布，默认为null（使用主画布）</param>
        /// <remarks>
        /// 根据历史记录类型执行不同的操作：
        /// 1. UserInput: 处理用户输入的笔画
        /// 2. ShapeRecognition: 处理形状识别的笔画
        /// 3. Manipulation: 处理笔画操作
        /// 4. DrawingAttributes: 处理绘制属性变化
        /// 5. Clear: 处理清除画布操作
        /// 6. ElementInsert: 处理元素插入操作
        /// </remarks>
        /// <param name="elementsRemovedInThisPage"></param>
        private void ApplyHistoryToCanvas(TimeMachineHistory item, InkCanvas applyCanvas = null, HashSet<UIElement> elementsRemovedInThisPage = null)
        {
            _currentCommitType = CommitReason.CodeInput;
            var canvas = inkCanvas;
            if (applyCanvas != null && applyCanvas is InkCanvas)
            {
                canvas = applyCanvas;
            }

            if (item.CommitType == TimeMachineHistoryType.UserInput)
            {
                if (!item.StrokeHasBeenCleared)
                {
                    foreach (var strokes in item.CurrentStroke)
                        if (!canvas.Strokes.Contains(strokes))
                            canvas.Strokes.Add(strokes);
                }
                else
                {
                    foreach (var strokes in item.CurrentStroke)
                        if (canvas.Strokes.Contains(strokes))
                            canvas.Strokes.Remove(strokes);
                }
            }
            else if (item.CommitType == TimeMachineHistoryType.ShapeRecognition)
            {
                if (item.StrokeHasBeenCleared)
                {
                    if (item.CurrentStroke != null)
                    {
                        foreach (var strokes in item.CurrentStroke)
                            if (canvas.Strokes.Contains(strokes))
                                canvas.Strokes.Remove(strokes);
                    }

                    if (item.ReplacedStroke != null)
                    {
                        foreach (var strokes in item.ReplacedStroke)
                            if (!canvas.Strokes.Contains(strokes))
                                canvas.Strokes.Add(strokes);
                    }
                }
                else
                {
                    if (item.CurrentStroke != null)
                    {
                        foreach (var strokes in item.CurrentStroke)
                            if (!canvas.Strokes.Contains(strokes))
                                canvas.Strokes.Add(strokes);
                    }

                    if (item.ReplacedStroke != null)
                    {
                        foreach (var strokes in item.ReplacedStroke)
                            if (canvas.Strokes.Contains(strokes))
                                canvas.Strokes.Remove(strokes);
                    }
                }
            }
            else if (item.CommitType == TimeMachineHistoryType.Manipulation)
            {
                if (!item.StrokeHasBeenCleared)
                {
                    foreach (var currentStroke in item.StylusPointDictionary)
                    {
                        if (canvas.Strokes.Contains(currentStroke.Key))
                        {
                            currentStroke.Key.StylusPoints = currentStroke.Value.Item2;
                        }
                    }
                }
                else
                {
                    foreach (var currentStroke in item.StylusPointDictionary)
                    {
                        if (canvas.Strokes.Contains(currentStroke.Key))
                        {
                            currentStroke.Key.StylusPoints = currentStroke.Value.Item1;
                        }
                    }
                }
            }
            else if (item.CommitType == TimeMachineHistoryType.DrawingAttributes)
            {
                if (!item.StrokeHasBeenCleared)
                {
                    foreach (var currentStroke in item.DrawingAttributes)
                    {
                        if (canvas.Strokes.Contains(currentStroke.Key))
                        {
                            currentStroke.Key.DrawingAttributes = currentStroke.Value.Item2;
                        }
                    }
                }
                else
                {
                    foreach (var currentStroke in item.DrawingAttributes)
                    {
                        if (canvas.Strokes.Contains(currentStroke.Key))
                        {
                            currentStroke.Key.DrawingAttributes = currentStroke.Value.Item1;
                        }
                    }
                }
            }
            else if (item.CommitType == TimeMachineHistoryType.Clear)
            {
                if (!item.StrokeHasBeenCleared)
                {
                    if (item.CurrentStroke != null)
                        foreach (var currentStroke in item.CurrentStroke)
                            if (!canvas.Strokes.Contains(currentStroke))
                                canvas.Strokes.Add(currentStroke);

                    if (item.ReplacedStroke != null)
                        foreach (var replacedStroke in item.ReplacedStroke)
                            if (canvas.Strokes.Contains(replacedStroke))
                                canvas.Strokes.Remove(replacedStroke);
                }
                else
                {
                    if (item.ReplacedStroke != null)
                        foreach (var replacedStroke in item.ReplacedStroke)
                            if (!canvas.Strokes.Contains(replacedStroke))
                                canvas.Strokes.Add(replacedStroke);

                    if (item.CurrentStroke != null)
                        foreach (var currentStroke in item.CurrentStroke)
                            if (canvas.Strokes.Contains(currentStroke))
                                canvas.Strokes.Remove(currentStroke);
                }
            }
            else if (item.CommitType == TimeMachineHistoryType.ElementEdit)
            {
                var state = item.StrokeHasBeenCleared ? item.PreviousElementState : item.CurrentElementState;
                var replacement = RestoreEditedElement(item.EditedElement, state, canvas);
                if (replacement != null)
                    item.EditedElement = replacement;
            }
            else if (item.CommitType == TimeMachineHistoryType.ElementInsert)
            {
                var targetCanvas = canvas ?? inkCanvas;

                if (item.StrokeHasBeenCleared)
                {
                    if (elementsRemovedInThisPage != null)
                        return;
                    if (item.InsertedElement != null && targetCanvas.Children.Contains(item.InsertedElement))
                        targetCanvas.Children.Remove(item.InsertedElement);
                }
                else
                {
                    if (elementsRemovedInThisPage != null && item.InsertedElement != null && elementsRemovedInThisPage.Contains(item.InsertedElement))
                        return;
                    if (item.InsertedElement != null && !targetCanvas.Children.Contains(item.InsertedElement))
                    {
                        targetCanvas.Children.Add(item.InsertedElement);

                        if (targetCanvas != inkCanvas)
                        {
                            if (item.InsertedElement is Image img)
                            {
                                double left = InkCanvas.GetLeft(img);
                                double top = InkCanvas.GetTop(img);
                                if (double.IsNaN(left) || double.IsNaN(top))
                                {
                                    CenterAndScaleElement(img);
                                }
                            }
                            else if (item.InsertedElement is MediaElement media)
                            {
                                double left = InkCanvas.GetLeft(media);
                                double top = InkCanvas.GetTop(media);
                                if (double.IsNaN(left) || double.IsNaN(top))
                                {
                                    CenterAndScaleElement(media);
                                }
                            }
                            else if (item.InsertedElement is CanvasMediaControl mediaControl)
                            {
                                double left = InkCanvas.GetLeft(mediaControl);
                                double top = InkCanvas.GetTop(mediaControl);
                                if (double.IsNaN(left) || double.IsNaN(top))
                                {
                                    CenterAndScaleElement(mediaControl);
                                }
                            }
                            else if (item.InsertedElement is FrameworkElement genericElement
                                     && !(genericElement is Image)
                                     && !(genericElement is MediaElement))
                            {
                                // 插件插入的自定义控件（见 ICanvasElementService）：
                                // 撤销重做重插后若位置缺失则居中，并补齐 TransformGroup。
                                double left = InkCanvas.GetLeft(genericElement);
                                double top = InkCanvas.GetTop(genericElement);
                                if (double.IsNaN(left) || double.IsNaN(top))
                                {
                                    CenterAndScaleElement(genericElement);
                                }
                                if (genericElement.RenderTransform == null
                                    || genericElement.RenderTransform == Transform.Identity)
                                {
                                    InitializeElementTransform(genericElement);
                                }
                            }
                        }
                    }
                }
            }
            else if (item.CommitType == TimeMachineHistoryType.PluginStateChange && applyCanvas == null)
            {
                ApplyPluginUndoState(
                    item.PluginId,
                    item.StrokeHasBeenCleared ? item.PluginStateBefore : item.PluginStateAfter);
            }
            else if (item.CommitType == TimeMachineHistoryType.PluginInkConversion)
            {
                if (item.StrokeHasBeenCleared)
                {
                    if (item.CurrentStroke != null)
                        foreach (var stroke in item.CurrentStroke)
                            if (canvas.Strokes.Contains(stroke))
                                canvas.Strokes.Remove(stroke);
                }
                else
                {
                    if (item.CurrentStroke != null)
                        foreach (var stroke in item.CurrentStroke)
                            if (!canvas.Strokes.Contains(stroke))
                                canvas.Strokes.Add(stroke);
                }

                if (applyCanvas == null)
                    ApplyPluginUndoState(
                        item.PluginId,
                        item.StrokeHasBeenCleared ? item.PluginStateAfter : item.PluginStateBefore);
            }

            _currentCommitType = CommitReason.UserInput;
        }

        /// <summary>
        /// 将历史记录应用到新的笔画集合
        /// </summary>
        /// <param name="items">时间机器历史记录数组</param>
        /// <returns>返回应用历史记录后的笔画集合</returns>
        /// <remarks>
        /// 创建一个临时画布，应用历史记录，然后返回画布中的笔画集合
        /// 只处理笔画历史，不处理图片元素历史
        /// </remarks>
        private FrameworkElement RestoreEditedElement(UIElement current, string serializedState, InkCanvas canvas)
        {
            if (current is not FrameworkElement currentElement || string.IsNullOrWhiteSpace(serializedState) || canvas == null)
                return current as FrameworkElement;

            var parent = currentElement.Parent as Panel;
            var directCanvas = currentElement.Parent as InkCanvas;
            var index = parent?.Children.IndexOf(currentElement)
                ?? directCanvas?.Children.IndexOf(currentElement)
                ?? canvas.Children.Count;
            var left = InkCanvas.GetLeft(currentElement);
            var top = InkCanvas.GetTop(currentElement);
            var transform = currentElement.RenderTransform?.Clone();

            if (IsEmptyEditableState(serializedState))
            {
                if (ReferenceEquals(currentSelectedElement, currentElement))
                {
                    UnselectElement(currentElement);
                    currentSelectedElement = null;
                }
                if (parent != null && parent.Children.Contains(currentElement))
                    parent.Children.Remove(currentElement);
                else if (directCanvas != null && directCanvas.Children.Contains(currentElement))
                    directCanvas.Children.Remove(currentElement);
                return currentElement;
            }

            try
            {
                var type = currentElement.GetType();
                var method = type.GetMethod("FromSerializedScene", BindingFlags.Public | BindingFlags.Static)
                    ?? type.GetMethod("FromSerializedElement", BindingFlags.Public | BindingFlags.Static);
                if (method == null) return currentElement;
                var parameters = method.GetParameters().Length == 1
                    ? new object[] { serializedState }
                    : new object[] { serializedState, 1d };
                if (method.Invoke(null, parameters) is not FrameworkElement replacement)
                    return currentElement;

                if (ReferenceEquals(currentSelectedElement, currentElement))
                {
                    UnselectElement(currentElement);
                    currentSelectedElement = null;
                }
                if (parent != null && parent.Children.Contains(currentElement))
                    parent.Children.Remove(currentElement);
                else if (directCanvas != null && directCanvas.Children.Contains(currentElement))
                    directCanvas.Children.Remove(currentElement);
                if (!double.IsNaN(left)) InkCanvas.SetLeft(replacement, left);
                if (!double.IsNaN(top)) InkCanvas.SetTop(replacement, top);
                replacement.Name = currentElement.Name;
                replacement.RenderTransform = transform;
                if (parent != null)
                    parent.Children.Insert(Math.Min(index, parent.Children.Count), replacement);
                else if (directCanvas != null)
                    directCanvas.Children.Insert(Math.Min(index, directCanvas.Children.Count), replacement);
                else
                    canvas.Children.Insert(Math.Min(index, canvas.Children.Count), replacement);
                InitializeElementTransformIfMissing(replacement);
                BindElementEvents(replacement);
                return replacement;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RestoreEditableElement failed: {ex.Message}");
                return currentElement;
            }
        }

        private static bool IsEmptyEditableState(string serializedState)
        {
            try
            {
                using var document = JsonDocument.Parse(serializedState);
                var root = document.RootElement;
                return root.TryGetProperty("elements", out var elements)
                    && elements.ValueKind == JsonValueKind.Array
                    && elements.GetArrayLength() == 0;
            }
            catch
            {
                return false;
            }
        }

        private void InitializeElementTransformIfMissing(FrameworkElement element)
        {
            if (element.RenderTransform == null || element.RenderTransform.Value.IsIdentity)
                InitializeElementTransform(element);
        }

        private StrokeCollection ApplyHistoriesToNewStrokeCollection(TimeMachineHistory[] items)
        {
            InkCanvas fakeInkCanv = new InkCanvas
            {
                Width = inkCanvas.ActualWidth,
                Height = inkCanvas.ActualHeight,
                EditingMode = InkCanvasEditingMode.None,
            };

            if (items != null && items.Length > 0)
            {
                foreach (var timeMachineHistory in items)
                {
                    // 只处理笔画历史，不处理图片元素历史
                    // 因为页面预览只需要显示笔画，图片元素会影响主画布
                    if (timeMachineHistory.CommitType != TimeMachineHistoryType.ElementInsert &&
                        timeMachineHistory.CommitType != TimeMachineHistoryType.PluginStateChange)
                    {
                        ApplyHistoryToCanvas(timeMachineHistory, fakeInkCanv);
                    }
                }
            }

            return fakeInkCanv.Strokes;
        }

        /// <summary>
        /// 将一页的完整历史扁平化为“仅最终状态”：在临时画布上重放该页历史，再导出为最少条目的新历史（一笔画集合 + 若干元素插入）。
        /// 用于删除页面前移后，避免移入槽位保留冗长历史导致翻到该页码时卡顿。
        /// </summary>
        /// <param name="history">该页的 TimeMachineHistory 数组，可为 null 或空</param>
        /// <returns>扁平化后的新历史数组；若输入为 null 或空则返回 null</returns>
        private TimeMachineHistory[] FlattenPageHistory(TimeMachineHistory[] history)
        {
            if (history == null || history.Length == 0) return null;

            var removed = CollectRemovedElementsFromHistory(history);
            var fakeInkCanv = new InkCanvas
            {
                Width = inkCanvas.ActualWidth > 0 ? inkCanvas.ActualWidth : 1920,
                Height = inkCanvas.ActualHeight > 0 ? inkCanvas.ActualHeight : 1080,
                EditingMode = InkCanvasEditingMode.None,
            };

            var latestPluginStates = new Dictionary<string, TimeMachineHistory>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in history)
            {
                if (item.CommitType == TimeMachineHistoryType.PluginStateChange ||
                    item.CommitType == TimeMachineHistoryType.PluginInkConversion)
                {
                    if (!string.IsNullOrWhiteSpace(item.PluginId)) latestPluginStates[item.PluginId] = item;
                    if (item.CommitType == TimeMachineHistoryType.PluginStateChange) continue;
                }

                ApplyHistoryToCanvas(item, fakeInkCanv, removed);
            }

            var list = new List<TimeMachineHistory>();
            if (fakeInkCanv.Strokes.Count > 0)
                list.Add(new TimeMachineHistory(fakeInkCanv.Strokes.Clone(), TimeMachineHistoryType.UserInput, false));
            var childrenSnapshot = new List<UIElement>();
            foreach (UIElement c in fakeInkCanv.Children)
                childrenSnapshot.Add(c);
            foreach (UIElement child in childrenSnapshot)
            {
                if (child is Image || child is MediaElement || child is CanvasMediaControl)
                {
                    list.Add(new TimeMachineHistory(child, TimeMachineHistoryType.ElementInsert));
                    fakeInkCanv.Children.Remove(child);
                }
            }
            foreach (var item in latestPluginStates.Values)
            {
                var finalState = item.StrokeHasBeenCleared
                    ? item.PluginStateBefore
                    : item.PluginStateAfter;
                list.Add(new TimeMachineHistory(item.PluginId, string.Empty, finalState));
            }
            return list.Count == 0 ? null : list.ToArray();
        }

        /// <summary>
        /// 获取页面的所有图片元素
        /// </summary>
        /// <param name="items">时间机器历史记录数组</param>
        /// <returns>返回页面的图片元素列表</returns>
        /// <remarks>
        /// 遍历历史记录，收集所有插入的图片元素
        /// </remarks>
        private List<UIElement> GetPageImageElements(TimeMachineHistory[] items)
        {
            var imageElements = new List<UIElement>();

            if (items != null && items.Length > 0)
            {
                foreach (var timeMachineHistory in items)
                {
                    if (timeMachineHistory.CommitType == TimeMachineHistoryType.ElementInsert &&
                        timeMachineHistory.InsertedElement != null &&
                        !timeMachineHistory.StrokeHasBeenCleared)
                    {
                        imageElements.Add(timeMachineHistory.InsertedElement);
                    }
                }
            }

            return imageElements;
        }

        /// <summary>
        /// 处理撤销状态变化事件
        /// </summary>
        /// <param name="status">撤销状态</param>
        /// <remarks>
        /// 根据撤销状态更新撤销按钮的可见性和启用状态
        /// </remarks>
        private void TimeMachine_OnUndoStateChanged(bool status)
        {
            IsUndoEnabled = status;
            RaisePluginEvent(PluginUndoRedoStateChanged, IsUndoEnabled, IsRedoEnabled, nameof(PluginUndoRedoStateChanged));
        }

        /// <summary>
        /// 处理重做状态变化事件
        /// </summary>
        /// <param name="status">重做状态</param>
        /// <remarks>
        /// 根据重做状态更新重做按钮的可见性和启用状态
        /// </remarks>
        private void TimeMachine_OnRedoStateChanged(bool status)
        {
            IsRedoEnabled = status;
            RaisePluginEvent(PluginUndoRedoStateChanged, IsUndoEnabled, IsRedoEnabled, nameof(PluginUndoRedoStateChanged));
        }

        /// <summary>
        /// 处理笔画集合变化事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">笔画集合变化事件参数</param>
        /// <remarks>
        /// 当笔画集合发生变化时：
        /// 1. 书写时自动隐藏二级菜单
        /// 2. 处理移除的笔画：移除事件处理器，从历史记录中移除
        /// 3. 处理添加的笔画：添加事件处理器，记录初始状态
        /// 4. 根据不同的提交类型处理历史记录
        /// </remarks>
        private void StrokesOnStrokesChanged(object sender, StrokeCollectionChangedEventArgs e)
        {
            // 视频展台旋转时程序替换墨迹，不重置基准
            if (_isVideoPresenterSpecialMode && !_isApplyingRotationToStrokes)
            {
                // 用户手动绘制/擦除墨迹，重置旋转基准，下次旋转重新保存基准
                ResetRotationBaseline();
            }

            if (IsCurrentPageFrozen && _currentCommitType != CommitReason.CodeInput)
            {
                var previousCommitType = _currentCommitType;
                _currentCommitType = CommitReason.CodeInput;
                try
                {
                    if (e?.Added != null && e.Added.Count > 0)
                        inkCanvas.Strokes.Remove(e.Added);
                    if (e?.Removed != null)
                    {
                        foreach (Stroke stroke in e.Removed)
                            if (!inkCanvas.Strokes.Contains(stroke))
                                inkCanvas.Strokes.Add(stroke);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"冻结页面回滚笔迹变化失败: {ex.Message}", LogHelper.LogType.Warning);
                }
                finally
                {
                    _currentCommitType = previousCommitType;
                }
                TryBlockFrozenPageMutation("修改冻结页面");
                return;
            }

            // 通知插件墨迹集合变化。冻结页回滚已在上面 return，不会误报；
            // 程序性 CodeInput 变化（含插件自身经 ICanvasInkService 的插入/清除）也会触发，
            // 插件需避免在处理器内再次写入造成循环。
            RaisePluginEvent(PluginStrokesChanged, e?.Added, e?.Removed, nameof(PluginStrokesChanged));

            if (!isHidingSubPanelsWhenInking)
            {
                isHidingSubPanelsWhenInking = true;
                HideSubPanels(); // 书写时自动隐藏二级菜单
            }

            // 视频展台特殊模式下，用户新增墨迹意味着基准快照已过期，下次旋转重新保存。
            // 程序自身在旋转墨迹（_isApplyingRotationToStrokes）时不重置。
            if (_isVideoPresenterSpecialMode && e?.Added != null && e.Added.Count > 0
                && !_isApplyingRotationToStrokes)
            {
                ResetRotationBaseline();
            }

            foreach (var stroke in e?.Removed)
            {
                stroke.StylusPointsChanged -= Stroke_StylusPointsChanged;
                stroke.StylusPointsReplaced -= Stroke_StylusPointsReplaced;
                stroke.DrawingAttributesChanged -= Stroke_DrawingAttributesChanged;
                StrokeInitialHistory.Remove(stroke);

                // 清理绘制属性历史记录中的已移除笔画，防止内存泄漏
                DrawingAttributesHistory.Remove(stroke);
                foreach (var flagList in DrawingAttributesHistoryFlag.Values)
                {
                    flagList.Remove(stroke);
                }
            }

            foreach (var stroke in e?.Added)
            {
                stroke.StylusPointsChanged += Stroke_StylusPointsChanged;
                stroke.StylusPointsReplaced += Stroke_StylusPointsReplaced;
                stroke.DrawingAttributesChanged += Stroke_DrawingAttributesChanged;
                StrokeInitialHistory[stroke] = stroke.StylusPoints.Clone();
            }

            if (_currentCommitType == CommitReason.CodeInput || _currentCommitType == CommitReason.ShapeDrawing) return;

            if (e.Added.Count != 0 || e.Removed.Count != 0)
                MarkCurrentPageInkChanged();

            // 长按撤销清屏：清空前已显式提交可撤销历史，这里吞掉事件路径的自动重复提交
            // （含点擦分支的批量收集），避免产生重复历史或污染橡皮擦批处理。
            if (_suppressClearHistoryCommit && _currentCommitType == CommitReason.ClearingCanvas)
                return;

            if ((e.Added.Count != 0 || e.Removed.Count != 0) && IsEraseByPoint)
            {
                if (AddedStroke == null) AddedStroke = new StrokeCollection();
                if (ReplacedStroke == null) ReplacedStroke = new StrokeCollection();
                AddedStroke.Add(e.Added);
                ReplacedStroke.Add(e.Removed);
                return;
            }

            if (e.Added.Count != 0)
            {
                if (_currentCommitType == CommitReason.ShapeRecognition)
                {
                    timeMachine.CommitStrokeShapeHistory(ReplacedStroke, e.Added);
                    ReplacedStroke = null;
                    return;
                }

                timeMachine.CommitStrokeUserInputHistory(e.Added);
                return;
            }

            if (e.Removed.Count != 0)
            {
                if (_currentCommitType == CommitReason.ShapeRecognition)
                {
                    ReplacedStroke = e.Removed;
                }
                else if (!IsEraseByPoint || _currentCommitType == CommitReason.ClearingCanvas)
                {
                    timeMachine.CommitStrokeEraseHistory(e.Removed);
                }
            }
        }

        /// <summary>
        /// 处理笔画绘制属性变化事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">属性数据变化事件参数</param>
        /// <remarks>
        /// 当笔画的绘制属性发生变化时，记录变化历史
        /// </remarks>
        private void Stroke_DrawingAttributesChanged(object sender, PropertyDataChangedEventArgs e)
        {
            if (IsCurrentPageFrozen && _currentCommitType != CommitReason.CodeInput)
            {
                TryBlockFrozenPageMutation("修改冻结页面");
                return;
            }

            var key = sender as Stroke;
            var currentValue = key.DrawingAttributes.Clone();
            DrawingAttributesHistory.TryGetValue(key, out var previousTuple);
            var previousValue = previousTuple?.Item1 ?? currentValue.Clone();
            var needUpdateValue = !DrawingAttributesHistoryFlag[e.PropertyGuid].Contains(key);
            if (needUpdateValue)
            {
                DrawingAttributesHistoryFlag[e.PropertyGuid].Add(key);
                Debug.Write(e.PreviousValue.ToString());
            }

            if (e.PropertyGuid == DrawingAttributeIds.Color && needUpdateValue)
            {
                previousValue.Color = (Color)e.PreviousValue;
            }

            if (e.PropertyGuid == DrawingAttributeIds.IsHighlighter && needUpdateValue)
            {
                previousValue.IsHighlighter = (bool)e.PreviousValue;
            }

            if (e.PropertyGuid == DrawingAttributeIds.StylusHeight && needUpdateValue)
            {
                previousValue.Height = (double)e.PreviousValue;
            }

            if (e.PropertyGuid == DrawingAttributeIds.StylusWidth && needUpdateValue)
            {
                previousValue.Width = (double)e.PreviousValue;
            }

            if (e.PropertyGuid == DrawingAttributeIds.StylusTip && needUpdateValue)
            {
                previousValue.StylusTip = (StylusTip)e.PreviousValue;
            }

            if (e.PropertyGuid == DrawingAttributeIds.StylusTipTransform && needUpdateValue)
            {
                previousValue.StylusTipTransform = (Matrix)e.PreviousValue;
            }

            if (e.PropertyGuid == DrawingAttributeIds.DrawingFlags && needUpdateValue)
            {
                previousValue.IgnorePressure = (bool)e.PreviousValue;
            }

            DrawingAttributesHistory[key] =
                new Tuple<DrawingAttributes, DrawingAttributes>(previousValue, currentValue);
        }

        /// <summary>
        /// 处理笔画触笔点替换事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">触笔点替换事件参数</param>
        /// <remarks>
        /// 当笔画的触笔点被替换时，更新初始状态历史
        /// </remarks>
        private void Stroke_StylusPointsReplaced(object sender, StylusPointsReplacedEventArgs e)
        {
            StrokeInitialHistory[sender as Stroke] = e.NewStylusPoints.Clone();
        }

        /// <summary>
        /// 处理笔画触笔点变化事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        /// <remarks>
        /// 当笔画的触笔点发生变化时：
        /// 1. 获取选中的笔画数量
        /// 2. 初始化笔画操作历史记录
        /// 3. 记录笔画的初始状态和当前状态
        /// 4. 当所有选中的笔画都已处理时，提交操作历史
        /// </remarks>
        private void Stroke_StylusPointsChanged(object sender, EventArgs e)
        {
            if (IsCurrentPageFrozen && _currentCommitType != CommitReason.CodeInput)
            {
                TryBlockFrozenPageMutation("修改冻结页面");
                return;
            }

            // 视频展台旋转墨迹时（_isApplyingRotationToStrokes=true），不提交操作历史，
            // 否则撤销/重做会尝试恢复旋转前的墨迹位置，与已旋转的视频画面不同步。
            // 旋转后 StrokeInitialHistory 已在 RotateBoothStrokesFromBaseline 中更新。
            if (_isApplyingRotationToStrokes)
                return;

            // 视频展台模式：拖动/缩放摄像头预览导致的墨迹变换是视图操作，不是墨迹编辑，
            // 不提交到 TimeMachine。撤销应直接撤销绘制（在当前位置删除墨迹），
            // 而不是先回退移动/旋转再撤销绘制。
            // 只更新 StrokeInitialHistory 为当前位置，避免后续退出展台后操作初始值错误。
            if (_isVideoPresenterSpecialMode)
            {
                StrokeInitialHistory[sender as Stroke] = (sender as Stroke).StylusPoints.Clone();
                return;
            }

            var selectedStrokes = inkCanvas.GetSelectedStrokes();
            var count = selectedStrokes.Count;
            if (count == 0) count = inkCanvas.Strokes.Count;
            if (StrokeManipulationHistory == null)
            {
                StrokeManipulationHistory =
                    new Dictionary<Stroke, Tuple<StylusPointCollection, StylusPointCollection>>();
            }

            StrokeManipulationHistory[sender as Stroke] =
                new Tuple<StylusPointCollection, StylusPointCollection>(StrokeInitialHistory[sender as Stroke],
                    (sender as Stroke).StylusPoints.Clone());
            if ((StrokeManipulationHistory.Count == count || sender == null)
                && dec.Count == 0
                && !_isStrokeRotationOverlayActive)
            {
                CommitPendingStrokeManipulationHistory();
            }
        }

        /// <summary>
        /// 提交待处理的墨迹操作历史（拖动/缩放结束后调用）。
        /// 将 StrokeManipulationHistory 一次性提交到 TimeMachine，避免逐帧提交产生大量历史条目。
        /// </summary>
        private void CommitPendingStrokeManipulationHistory()
        {
            if (StrokeManipulationHistory == null || StrokeManipulationHistory.Count == 0) return;

            timeMachine.CommitStrokeManipulationHistory(StrokeManipulationHistory);
            foreach (var item in StrokeManipulationHistory)
            {
                StrokeInitialHistory[item.Key] = item.Value.Item2;
            }

            StrokeManipulationHistory = null;

            // 视频展台模式：墨迹被移动/缩放后，旋转基准已过期，重置以便下次旋转重新保存
            if (_isVideoPresenterSpecialMode && !_isApplyingRotationToStrokes)
                ResetRotationBaseline();
        }
    }
}
