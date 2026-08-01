using Ink_Canvas.Helpers;
using Ink_Canvas.Plugins;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;

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
    }
}
