using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas
{
    public partial class MainWindow : Window
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
                    foreach (var strokes in item.CurrentStroke)
                        if (canvas.Strokes.Contains(strokes))
                            canvas.Strokes.Remove(strokes);

                    foreach (var strokes in item.ReplacedStroke)
                        if (!canvas.Strokes.Contains(strokes))
                            canvas.Strokes.Add(strokes);
                }
                else
                {
                    foreach (var strokes in item.CurrentStroke)
                        if (!canvas.Strokes.Contains(strokes))
                            canvas.Strokes.Add(strokes);

                    foreach (var strokes in item.ReplacedStroke)
                        if (canvas.Strokes.Contains(strokes))
                            canvas.Strokes.Remove(strokes);
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
                        }
                    }
                }
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
                    if (timeMachineHistory.CommitType != TimeMachineHistoryType.ElementInsert)
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

            foreach (var item in history)
                ApplyHistoryToCanvas(item, fakeInkCanv, removed);

            var list = new List<TimeMachineHistory>();
            if (fakeInkCanv.Strokes.Count > 0)
                list.Add(new TimeMachineHistory(fakeInkCanv.Strokes.Clone(), TimeMachineHistoryType.UserInput, false));
            var childrenSnapshot = new List<UIElement>();
            foreach (UIElement c in fakeInkCanv.Children)
                childrenSnapshot.Add(c);
            foreach (UIElement child in childrenSnapshot)
            {
                if (child is Image || child is MediaElement)
                {
                    list.Add(new TimeMachineHistory(child, TimeMachineHistoryType.ElementInsert));
                    fakeInkCanv.Children.Remove(child);
                }
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
            var result = status ? Visibility.Visible : Visibility.Collapsed;
            BtnUndo.Visibility = result;
            BtnUndo.IsEnabled = status;
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
            var result = status ? Visibility.Visible : Visibility.Collapsed;
            BtnRedo.Visibility = result;
            BtnRedo.IsEnabled = status;
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
            if (!isHidingSubPanelsWhenInking)
            {
                isHidingSubPanelsWhenInking = true;
                HideSubPanels(); // 书写时自动隐藏二级菜单
            }

            foreach (var stroke in e?.Removed)
            {
                stroke.StylusPointsChanged -= Stroke_StylusPointsChanged;
                stroke.StylusPointsReplaced -= Stroke_StylusPointsReplaced;
                stroke.DrawingAttributesChanged -= Stroke_DrawingAttributesChanged;
                StrokeInitialHistory.Remove(stroke);
            }

            foreach (var stroke in e?.Added)
            {
                stroke.StylusPointsChanged += Stroke_StylusPointsChanged;
                stroke.StylusPointsReplaced += Stroke_StylusPointsReplaced;
                stroke.DrawingAttributesChanged += Stroke_DrawingAttributesChanged;
                StrokeInitialHistory[stroke] = stroke.StylusPoints.Clone();
            }

            if (_currentCommitType == CommitReason.CodeInput || _currentCommitType == CommitReason.ShapeDrawing) return;

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
            if ((StrokeManipulationHistory.Count == count || sender == null) && dec.Count == 0)
            {
                timeMachine.CommitStrokeManipulationHistory(StrokeManipulationHistory);
                foreach (var item in StrokeManipulationHistory)
                {
                    StrokeInitialHistory[item.Key] = item.Value.Item2;
                }

                StrokeManipulationHistory = null;
            }
        }
    }
}