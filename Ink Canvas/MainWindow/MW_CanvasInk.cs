using Ink_Canvas.Controls;
using Ink_Canvas.Helpers;
using Ink_Canvas.Plugins;
using Ink_Canvas.Properties;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Threading;

namespace Ink_Canvas
{
    /// <summary>
    /// 插件画布墨迹服务核心：墨迹读取/插入/清除、工具切换、墨迹冻结。
    /// 对应 <see cref="ICanvasInkService"/>，由 <see cref="Plugins.CanvasInkService"/> 转发。
    /// 所有方法都必须由调用方保证在 UI 线程执行（转发层负责切换线程）。
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>当前页是否已冻结（墨迹锁定）。</summary>
        internal bool IsPluginPageFrozen => IsCurrentPageFrozen;

        /// <summary>当前是否处于画笔/墨迹模式。</summary>
        internal bool IsPluginPenMode => GetPluginCurrentTool() == PluginInkTool.Pen;

        /// <summary>推断当前画布工具。</summary>
        internal PluginInkTool GetPluginCurrentTool()
        {
            if (IsBoardRoamingMode) return PluginInkTool.Roaming;
            if (drawingShapeMode != 0) return PluginInkTool.Shape;

            return inkCanvas.EditingMode switch
            {
                InkCanvasEditingMode.EraseByPoint => PluginInkTool.Eraser,
                InkCanvasEditingMode.EraseByStroke => PluginInkTool.StrokeEraser,
                InkCanvasEditingMode.Select => PluginInkTool.Select,
                // Ink 或 None（原生湿墨迹管线）均为笔。
                _ => PluginInkTool.Pen,
            };
        }

        /// <summary>当前画布上全部墨迹的克隆副本（画布坐标），不共享内部引用。</summary>
        internal StrokeCollection GetPluginCanvasStrokes()
            => ClonePluginStrokes(inkCanvas?.Strokes);

        /// <summary>当前默认笔触属性（克隆副本，修改不影响宿主）。</summary>
        internal DrawingAttributes GetPluginDefaultDrawingAttributes()
            => inkCanvas?.DefaultDrawingAttributes?.Clone() ?? new DrawingAttributes { Color = Colors.Black, Width = 2 };

        /// <summary>主画布实际尺寸（DIP）。</summary>
        internal Size GetPluginCanvasSize()
        {
            if (inkCanvas == null) return new Size(0, 0);
            var w = inkCanvas.ActualWidth;
            var h = inkCanvas.ActualHeight;
            if (double.IsNaN(w) || w <= 0) w = inkCanvas.Width;
            if (double.IsNaN(h) || h <= 0) h = inkCanvas.Height;
            return new Size(w, h);
        }

        /// <summary>
        /// 把墨迹插入当前画布。可选把墨迹包围盒中心平移到 <paramref name="center"/>（画布坐标）。
        /// 写入 TimeMachine 历史（可按 Ctrl+Z 撤销）；冻结页拒绝变更返回 false。
        /// </summary>
        internal bool TryAddPluginStrokes(StrokeCollection strokes, Point? center)
        {
            if (strokes == null || strokes.Count == 0) return false;
            if (inkCanvas == null) return false;
            if (IsCurrentPageFrozen)
            {
                TryBlockFrozenPageMutation("插入墨迹到白板");
                return false;
            }

            // 克隆后操作：既避免平移污染调用方持有的墨迹，也避免画布与插件共享同一对象。
            var toAdd = ClonePluginStrokes(strokes);
            if (center.HasValue && !double.IsNaN(center.Value.X) && !double.IsNaN(center.Value.Y))
            {
                var bounds = toAdd.GetBounds();
                if (!bounds.IsEmpty)
                {
                    var matrix = Matrix.Identity;
                    matrix.Translate(center.Value.X - (bounds.Left + bounds.Width / 2),
                                     center.Value.Y - (bounds.Top + bounds.Height / 2));
                    foreach (Stroke s in toAdd) s.Transform(matrix, false);
                }
            }

            var previousCommitType = _currentCommitType;
            _currentCommitType = CommitReason.CodeInput;
            try
            {
                inkCanvas.Strokes.Add(toAdd);
                // CodeInput 下 StrokesOnStrokesChanged 会提前返回，不会二次提交，
                // 因此这里手动提交一次历史，保证 Ctrl+Z 可整体撤销本次插入。
                timeMachine.CommitStrokeUserInputHistory(toAdd);
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"插件插入墨迹失败: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
            finally
            {
                _currentCommitType = previousCommitType;
            }
        }

        /// <summary>
        /// 清空当前画布墨迹，写入 TimeMachine 历史（可按 Ctrl+Z 撤销）。冻结页拒绝变更返回 false。
        /// </summary>
        internal bool TryClearPluginStrokes()
        {
            if (inkCanvas == null) return false;
            if (inkCanvas.Strokes.Count == 0) return false;
            if (IsCurrentPageFrozen)
            {
                TryBlockFrozenPageMutation("书写或擦除");
                return false;
            }

            var previousCommitType = _currentCommitType;
            _currentCommitType = CommitReason.ClearingCanvas;
            try
            {
                inkCanvas.Strokes.Clear();
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"插件清除墨迹失败: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
            finally
            {
                _currentCommitType = previousCommitType;
            }
        }

        /// <summary>
        /// 切换画布工具。编辑类工具在冻结页会被拒绝并返回 false。
        /// </summary>
        internal bool SelectPluginTool(PluginInkTool tool)
        {
            switch (tool)
            {
                case PluginInkTool.Select:
                    return SetCurrentToolMode(InkCanvasEditingMode.Select, () =>
                    {
                        forceEraser = false;
                        forcePointEraser = false;
                        drawingShapeMode = 0;
                        inkCanvas.IsManipulationEnabled = true;
                        SetCursorBasedOnEditingMode(inkCanvas);
                    });

                case PluginInkTool.Pen:
                    return SetCurrentToolMode(InkCanvasEditingMode.Ink, () =>
                    {
                        forceEraser = false;
                        forcePointEraser = false;
                        drawingShapeMode = 0;
                    });

                case PluginInkTool.Eraser:
                    return SetCurrentToolMode(InkCanvasEditingMode.EraseByPoint);

                case PluginInkTool.StrokeEraser:
                    return SetCurrentToolMode(InkCanvasEditingMode.EraseByStroke);

                case PluginInkTool.Shape:
                    if (IsCurrentPageFrozen)
                    {
                        TryBlockFrozenPageMutation("绘制几何图形");
                        return false;
                    }
                    drawingShapeMode = 1; // 矩形
                    return SetCurrentToolMode(InkCanvasEditingMode.Ink);

                case PluginInkTool.Roaming:
                    if (currentMode != 1) return false;
                    ActivateBoardRoamingMode();
                    return true;

                default:
                    return false;
            }
        }

        private static StrokeCollection ClonePluginStrokes(StrokeCollection source)
        {
            var result = new StrokeCollection();
            if (source == null) return result;
            foreach (Stroke s in source)
            {
                result.Add(s.Clone());
            }
            return result;
        }

        /// <summary>
        /// 触发「从文件插入图片」流程（文件对话框 + 插入画布）。冻结页拒绝返回 false。
        /// </summary>
        internal bool InsertPluginImage()
        {
            if (IsCurrentPageFrozen)
            {
                TryBlockFrozenPageMutation(FloatingBarStrings.Board_InsertImage);
                return false;
            }
            if (inkCanvas == null) return false;

            ImageOptionSelectFile_MouseUp(null, null);
            return true;
        }

        /// <summary>更换当前画布背景色（打开颜色选择）。</summary>
        internal void ChangePluginBackgroundColor()
            => BoardChangeBackgroundColorBtn_MouseUp(null, null);

        /// <summary>切换双指手势（画布平移/缩放）开关。</summary>
        internal void TogglePluginGesture()
            => TwoFingerGestureBorder_MouseUp(null, null);

        /// <summary>退出白板模式（回到浮动栏）。</summary>
        internal void ExitPluginWhiteboard()
            => ImageBlackboard_MouseUp(null, null);

        /// <summary>
        /// 把插件提供的 WPF 控件作为元素插入当前画布（复用图片/媒体元素完整交互：居中缩放、撤销历史、选择模式）。
        /// <paramref name="position"/> 为 null 时居中（最大为画布 70%）；否则控件左上角对齐到该画布坐标、不缩放。
        /// 冻结页拒绝变更返回 false。
        /// </summary>
        internal bool InsertPluginCanvasElement(FrameworkElement element, Point? position)
        {
            if (element == null || inkCanvas == null) return false;
            if (IsCurrentPageFrozen)
            {
                TryBlockFrozenPageMutation("插入控件");
                return false;
            }

            // 不允许插入已挂到其他容器的控件（同一控件二次插入会被拒），避免逻辑树错乱。
            if (LogicalTreeHelper.GetParent(element) != null)
            {
                LogHelper.WriteLogToFile("插件插入控件失败：元素已属于其他容器", LogHelper.LogType.Warning);
                return false;
            }

            try
            {
                // 设置元素属性，避免被 InkCanvas 选择系统处理（与图片插入保持一致）。
                element.IsHitTestVisible = true;
                element.Focusable = false;

                InitializeInkCanvasSelectionSettings();

                // 先添加到画布
                inkCanvas.Children.Add(element);

                element.Loaded += (s, args) =>
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        // 初始化 TransformGroup（滚轮缩放/旋转/拖动依赖）
                        InitializeElementTransform(element);

                        if (position.HasValue)
                        {
                            InkCanvas.SetLeft(element, position.Value.X);
                            InkCanvas.SetTop(element, position.Value.Y);
                        }
                        else
                        {
                            CenterAndScaleElement(element);
                        }

                        // 最后绑定事件处理器（拖动/缩放/旋转/触摸）
                        BindElementEvents(element);
                    }), DispatcherPriority.Loaded);
                };

                timeMachine.CommitElementInsertHistory(element);

                // 插入后切换到选择模式并刷新浮动栏高光显示
                SetCurrentToolMode(InkCanvasEditingMode.Select);
                UpdateCurrentToolMode("select");
                HideSubPanels("select");
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"插件插入控件失败: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
        }

        /// <summary>
        /// 从当前画布移除插件插入的控件，写入撤销历史（可按 Ctrl+Z 恢复）。冻结页拒绝变更返回 false。
        /// </summary>
        internal bool RemovePluginCanvasElement(FrameworkElement element)
        {
            if (element == null || inkCanvas == null) return false;
            if (!inkCanvas.Children.Contains(element)) return false;
            if (IsCurrentPageFrozen)
            {
                TryBlockFrozenPageMutation("删除控件");
                return false;
            }

            try
            {
                // 与图片删除路径保持一致：媒体控件先暂停播放、记录删除历史、清理选中态。
                if (element is CanvasMediaControl mediaControl)
                {
                    mediaControl.PausePlayback();
                }

                if (ReferenceEquals(currentSelectedElement, element))
                {
                    UnselectElement(element);
                    currentSelectedElement = null;
                }

                timeMachine.CommitElementRemoveHistory(element);
                inkCanvas.Children.Remove(element);
                SyncPdfPageSidebarWithCanvas();
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"插件移除控件失败: {ex.Message}", LogHelper.LogType.Error);
                return false;
            }
        }

        /// <summary>指定控件当前是否位于画布上。</summary>
        internal bool ContainsPluginCanvasElement(FrameworkElement element)
            => element != null && inkCanvas != null && inkCanvas.Children.Contains(element);

        /// <summary>当前画布上全部元素控件的快照。</summary>
        internal IReadOnlyList<FrameworkElement> GetPluginCanvasElements()
        {
            var list = new List<FrameworkElement>();
            if (inkCanvas == null) return list;
            foreach (var child in inkCanvas.Children)
            {
                if (child is FrameworkElement fe) list.Add(fe);
            }
            return list;
        }
    }
}
