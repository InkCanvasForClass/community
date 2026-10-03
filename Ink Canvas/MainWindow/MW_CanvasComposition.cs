using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Ink_Canvas
{
    /// <summary>
    /// 单页「背景 + 墨迹」合成结果，交给 <see cref="Plugins.CanvasCompositionService"/> 组装成 PDF。
    /// </summary>
    internal sealed class PluginPageRender
    {
        /// <summary>已 Freeze 的合成结果。编码放到后台线程做，避免 PNG 压缩阻塞 UI。</summary>
        public BitmapSource Bitmap { get; set; }

        /// <summary>页面宽度（设备无关像素，即页面坐标系尺度）。</summary>
        public double WidthDip { get; set; }

        /// <summary>页面高度（设备无关像素，即页面坐标系尺度）。</summary>
        public double HeightDip { get; set; }
    }

    /// <summary>
    /// 插件画布合成：背景层注入 + 按页墨迹缓存 + 「背景 + 墨迹」逐页渲染。
    /// 对应 <see cref="Plugins.ICanvasCompositionService"/>，由 <see cref="Plugins.CanvasCompositionService"/> 转发。
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>注入的背景层在 InkCanvasGridForInkReplay 中的索引（0 = InkCanvas 下方）。</summary>
        private const int PluginBackgroundLayerIndex = 0;

        /// <summary>无法从背景位图推断分辨率时的默认渲染倍率。</summary>
        private const double PluginDefaultRenderScale = 2.0;

        private FrameworkElement _pluginBackgroundLayer;
        private Rect? _pluginPageContentRect;
        private uint _pluginPageCount;
        private uint _pluginCurrentPageIndex;
        private Func<uint, CancellationToken, Task<BitmapSource>> _pluginPageRenderer;

        /// <summary>
        /// 连续滚动模式下的滚动偏移（DIP，长条内容向上滚为正）。
        /// 墨迹以「长条坐标」存取（含页偏移），滚动时宿主实时平移画布墨迹保持对齐。
        /// </summary>
        private double _pluginScrollOffsetY;

        /// <summary>累计的墨迹平移量，用于消除增量平移的浮点累积误差。</summary>
        private double _pluginInkTranslateY;

        /// <summary>
        /// 当前可见页列表（页索引 + 内容矩形）。空 = 单页模式（用 <see cref="SetPluginCurrentPageAsync"/>）。
        /// 双页模式一次显示两页，墨迹按矩形切分到各物理页。
        /// </summary>
        private List<(uint PageIndex, Rect ContentRect)> _pluginVisiblePages = new();

        /// <summary>按页缓存的墨迹，坐标已绑定到背景层页面坐标系。</summary>
        private readonly Dictionary<uint, StrokeCollection> _pluginPageInk = new Dictionary<uint, StrokeCollection>();

        /// <summary>
        /// 每页最近一次可见时的内容矩形。双页模式下导出非可见页时需要它来还原墨迹的
        /// 页面局部坐标（originX），否则会误用单页的整页矩形导致墨迹偏移。
        /// </summary>
        private readonly Dictionary<uint, Rect> _pluginPageRects = new Dictionary<uint, Rect>();

        /// <summary>
        /// 插件注册的画布双指手势处理器（如 PDF 阅读器）。非 null 时，宿主把画布上的
        /// 双指操作转发给它；它返回 true 表示接管，宿主跳过默认的墨迹/画布变换。
        /// </summary>
        private Plugins.IPluginCanvasGestureHandler _pluginCanvasGestureHandler;

        /// <summary>
        /// 插件背景层内的「内容锚点」：墨迹换算（TransformToVisual）的目标元素。
        /// 插件把页面内容放在会缩放/平移的容器、容器外还有固定背景时，必须指向该容器，
        /// 宿主才能把缩放正确纳入墨迹的按页存取换算。null = 使用背景层根节点。
        /// </summary>
        private FrameworkElement _pluginContentAnchor;

        internal bool HasPluginBackgroundLayer => _pluginBackgroundLayer != null;

        internal uint PluginPageCount => _pluginPageCount;

        internal uint PluginCurrentPageIndex => _pluginCurrentPageIndex;

        /// <summary>未配置分页时按单页处理（当前画布即第 0 页）。</summary>
        private uint EffectivePluginPageCount => _pluginPageCount == 0 ? 1u : _pluginPageCount;

        #region 背景层

        internal void InjectPluginBackgroundLayer(Func<FrameworkElement> backgroundFactory)
        {
            if (backgroundFactory == null)
            {
                RemovePluginBackgroundLayer();
                return;
            }

            RunOnUiThread(() =>
            {
                if (InkCanvasGridForInkReplay == null) return;

                DetachPluginBackgroundLayer();

                var element = backgroundFactory();
                if (element == null) return;

                // 铺满画布且不参与命中测试，书写事件仍然全部落到 InkCanvas 上。
                element.HorizontalAlignment = HorizontalAlignment.Stretch;
                element.VerticalAlignment = VerticalAlignment.Stretch;
                element.IsHitTestVisible = false;
                Panel.SetZIndex(element, 0);

                // 插到索引 0：Grid 在 ZIndex 相同时按文档顺序绘制，因此排在 inkCanvas 之前即在其下方。
                InkCanvasGridForInkReplay.Children.Insert(PluginBackgroundLayerIndex, element);
                _pluginBackgroundLayer = element;
            });
        }

        internal void RemovePluginBackgroundLayer()
        {
            RunOnUiThread(() =>
            {
                DetachPluginBackgroundLayer();
                _pluginCanvasGestureHandler = null;
                _pluginContentAnchor = null;
                _pluginPageInk.Clear();
                _pluginPageRects.Clear();
                _pluginPageCount = 0;
                _pluginCurrentPageIndex = 0;
                _pluginPageRenderer = null;
                _pluginPageContentRect = null;
                _pluginVisiblePages.Clear();
                _pluginScrollOffsetY = 0;
                _pluginInkTranslateY = 0;

                // 背景层被移除（外部演示源关闭）后，画布上残留的墨迹也随之清空：
                // 那些笔迹是画在 PDF 页面上的，桌面模式下继续显示会造成"墨迹飘在空画布上"。
                // 用 CodeInput 提交类型，避免污染时间机器历史。
                var previousCommitType = _currentCommitType;
                _currentCommitType = CommitReason.CodeInput;
                try
                {
                    inkCanvas?.Strokes.Clear();
                }
                finally
                {
                    _currentCommitType = previousCommitType;
                }
            });
        }

        /// <summary>
        /// 设置背景层内真正承载页面内容的矩形（背景元素坐标系，DIP）。
        /// 背景以 Uniform 居中留边时，导出需要据此裁出页面区域，否则页面会被拉伸成画布比例。
        /// </summary>
        internal void SetPluginPageContentRect(Rect? contentRect)
        {
            RunOnUiThread(() =>
            {
                if (contentRect.HasValue)
                {
                    var rect = contentRect.Value;
                    if (rect.Width <= 0 || rect.Height <= 0 ||
                        double.IsNaN(rect.Width) || double.IsNaN(rect.Height) ||
                        double.IsInfinity(rect.Width) || double.IsInfinity(rect.Height))
                    {
                        _pluginPageContentRect = null;
                        return;
                    }
                }

                _pluginPageContentRect = contentRect;
            });
        }

        private void DetachPluginBackgroundLayer()
        {
            if (_pluginBackgroundLayer == null) return;

            try
            {
                InkCanvasGridForInkReplay?.Children.Remove(_pluginBackgroundLayer);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"移除插件背景层失败: {ex.Message}", LogHelper.LogType.Warning);
            }

            _pluginBackgroundLayer = null;
        }

        #endregion

        #region 分页

        internal void ConfigurePluginPages(uint pageCount, uint currentPageIndex,
            Func<uint, CancellationToken, Task<BitmapSource>> pageRenderer)
        {
            RunOnUiThread(() =>
            {
                _pluginPageCount = pageCount;
                _pluginCurrentPageIndex = pageCount == 0 ? 0 : Math.Min(currentPageIndex, pageCount - 1);
                _pluginPageRenderer = pageRenderer;

                // 打开新文档时重置可见页列表，避免残留上个文档的双页墨迹缓存。
                _pluginVisiblePages.Clear();
                _pluginPageRects.Clear();

                // 页数收缩时丢掉越界页的墨迹缓存，避免导出时读到不存在的页。
                if (pageCount == 0)
                {
                    _pluginPageInk.Clear();
                    return;
                }

                var stale = new List<uint>();
                foreach (var page in _pluginPageInk.Keys)
                {
                    if (page >= pageCount) stale.Add(page);
                }
                foreach (var page in stale) _pluginPageInk.Remove(page);
            });
        }

        internal Task SetPluginCurrentPageAsync(uint pageIndex, CancellationToken cancellationToken)
        {
            return RunOnUiThreadAsync(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                ValidatePluginPageIndex(pageIndex);
                if (pageIndex == _pluginCurrentPageIndex) return;

                // 先把画布上的墨迹存回原页（转页面局部坐标），再换成目标页的墨迹。
                var canvasInk = CaptureCanvasStrokesInPageSpace();
                var rect = _pluginPageContentRect;
                _pluginPageInk[_pluginCurrentPageIndex] = rect.HasValue
                    ? TranslateStrokes(canvasInk, -rect.Value.X, -rect.Value.Y)
                    : canvasInk;
                if (rect.HasValue)
                    _pluginPageRects[_pluginCurrentPageIndex] = rect.Value;
                _pluginCurrentPageIndex = pageIndex;

                _pluginPageInk.TryGetValue(pageIndex, out var target);
                ReplaceCanvasStrokesFromPageSpace(target);
            });
        }

        /// <summary>
        /// 以「多可见页」方式切换：先把画布墨迹按各可见页矩形切分存回对应物理页，
        /// 清空画布，再恢复新可见页各自的墨迹。双页模式用此方法，墨迹严格按页归属。
        /// </summary>
        internal Task SetPluginVisiblePagesAsync(IReadOnlyList<Plugins.PluginVisiblePage> visiblePages,
            CancellationToken cancellationToken)
        {
            return RunOnUiThreadAsync(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                // 双指手势/缩放在两次同步之间可能通过 TransformPluginInkAsync 移动过墨迹，
                // 导致 _pluginScrollOffsetY/_pluginInkTranslateY 与墨迹实际位置失步。
                // 这里归零：SaveVisiblePagesInk 会用当前的 TransformToVisual 矩阵换算墨迹，
                // 之后的 ScrollPluginOffsetAsync 以同步后的墨迹为基准算增量，位移不受污染。
                _pluginScrollOffsetY = 0;
                _pluginInkTranslateY = 0;

                // 先把当前画布墨迹按旧的可见页矩形切分存回各页，再切换。
                SaveVisiblePagesInk();

                var oldList = new List<(uint PageIndex, Rect ContentRect)>(_pluginVisiblePages);
                _pluginVisiblePages = (visiblePages ?? Array.Empty<Plugins.PluginVisiblePage>())
                    .Select(p => (p.PageIndex, p.ContentRect))
                    .Where(t => t.ContentRect.Width > 0 && t.ContentRect.Height > 0)
                    .ToList();

                // 记录每个可见页的内容矩形，供导出非可见页时还原墨迹的页面局部坐标。
                foreach (var (pageIndex, contentRect) in _pluginVisiblePages)
                    _pluginPageRects[pageIndex] = contentRect;

                if (_pluginVisiblePages.Count > 0)
                    _pluginCurrentPageIndex = _pluginVisiblePages[0].PageIndex;

                // 清空画布，恢复新可见页墨迹。
                ReplaceVisiblePagesInk();

                // 诊断：记录切换前后各页的墨迹条数，便于排查「右页墨迹不切换」。
                LogHelper.WriteLogToFile(
                    "SetVisiblePagesInk 旧页=[" + string.Join(",", oldList.Select(p => p.PageIndex)) + "]" +
                    " 新页=[" + string.Join(",", _pluginVisiblePages.Select(p => p.PageIndex)) + "]" +
                    " 各页条数=[" + string.Join(",", _pluginVisiblePages.Select(p =>
                        (p.PageIndex) + ":" + (_pluginPageInk.TryGetValue(p.PageIndex, out var ink) ? ink.Count : -1))) + "]",
                    LogHelper.LogType.Info);
            });
        }

        /// <summary>
        /// 连续滚动：把当前画布墨迹整体平移 <paramref name="deltaY"/>（DIP），与背景长条滚动保持一致。
        /// 用绝对偏移消除增量平移的浮点累积误差。可见页矩形同步平移，保证任意时刻
        /// 墨迹与矩形在同一坐标下（settle 同步按矩形裁剪墨迹才不会错位到别的页）。
        /// </summary>
        internal Task ScrollPluginOffsetAsync(double deltaY, CancellationToken cancellationToken)
        {
            return RunOnUiThreadAsync(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (inkCanvas == null || deltaY == 0) return;

                // 更新绝对偏移。
                _pluginScrollOffsetY += deltaY;

                // 墨迹当前在「绝对长条坐标」下的偏移，减去新滚动偏移得到需平移量。
                double targetInkOffset = -_pluginScrollOffsetY;
                double shift = targetInkOffset - _pluginInkTranslateY;
                if (shift == 0) return;

                var matrix = new Matrix(1, 0, 0, 1, 0, shift);

                var previousCommitType = _currentCommitType;
                _currentCommitType = CommitReason.CodeInput;
                try
                {
                    foreach (Stroke stroke in inkCanvas.Strokes)
                        stroke.Transform(matrix, false);
                }
                finally
                {
                    _currentCommitType = previousCommitType;
                }

                _pluginInkTranslateY = targetInkOffset;

                // 可见页矩形与墨迹同平移：否则矩形停在旧坐标，settle 同步裁剪时
                // 与已平移的墨迹错位，把墨迹误存到相邻页。
                if (shift != 0)
                {
                    // 墨迹在画布坐标平移 shift；ContentRect 在内容锚点坐标（内容层带缩放时，
                    // 内容坐标 ↔ 画布坐标差 scale 倍），因此矩形平移量 = shift / scale。
                    // scale 取「画布 → 内容」矩阵的 M11（锚点缩放 s 时该矩阵含 1/s）。
                    double toContent = 1.0;
                    var canvasToContent = GetCanvasToPageMatrix();
                    if (canvasToContent.M11 > 0
                        && !double.IsNaN(canvasToContent.M11)
                        && !double.IsInfinity(canvasToContent.M11))
                    {
                        toContent = canvasToContent.M11;
                    }

                    double rectShift = shift * toContent;
                    var updated = new List<(uint PageIndex, Rect ContentRect)>(_pluginVisiblePages.Count);
                    foreach (var (pageIndex, rect) in _pluginVisiblePages)
                        updated.Add((pageIndex, new Rect(rect.X, rect.Y + rectShift, rect.Width, rect.Height)));
                    _pluginVisiblePages = updated;
                }
            });
        }

        /// <summary>注册/注销插件画布双指手势处理器（线程安全，任意线程可调用）。</summary>
        internal void SetPluginCanvasGestureHandler(Plugins.IPluginCanvasGestureHandler handler)
        {
            RunOnUiThread(() => { _pluginCanvasGestureHandler = handler; });
        }

        /// <summary>设置插件背景层内的内容锚点（墨迹换算目标，见 <see cref="_pluginContentAnchor"/>）。</summary>
        internal void SetPluginCanvasContentAnchor(FrameworkElement contentLayer)
        {
            RunOnUiThread(() => { _pluginContentAnchor = contentLayer; });
        }

        /// <summary>
        /// 按矩阵变换当前画布上的全部墨迹（仅笔画坐标，保留笔尖宽度）。
        /// 供插件双指缩放/平移时让墨迹与背景层 RenderTransform 实时同步。
        /// 与 <see cref="ScrollPluginOffsetAsync"/> 的区别：这是通用矩阵（缩放+平移），
        /// 且不改动可见页矩形——背景层 RenderTransform 的缩放已包含在
        /// <see cref="GetCanvasToPageMatrix"/> 的 <c>TransformToVisual</c> 里，
        /// 墨迹的保存/恢复按该矩阵自动对齐，无需在矩形上同步。
        /// </summary>
        internal Task TransformPluginInkAsync(Matrix matrix, CancellationToken cancellationToken)
        {
            return RunOnUiThreadAsync(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (inkCanvas == null || matrix.IsIdentity) return;

                var previousCommitType = _currentCommitType;
                _currentCommitType = CommitReason.CodeInput;
                try
                {
                    foreach (Stroke stroke in inkCanvas.Strokes)
                        stroke.Transform(matrix, false);
                }
                finally
                {
                    _currentCommitType = previousCommitType;
                }
            });
        }

        /// <summary>
        /// 把画布墨迹按当前可见页矩形切分，转成「页面局部坐标」（减去页矩形原点）存入各物理页。
        /// 页面局部坐标与展示模式无关：单页、双页、连续滚动下，同页墨迹始终在同一局部位置，
        /// 恢复时按该页当前显示矩形原点映射回画布，从而保证模式切换不错位。
        /// </summary>
        private void SaveVisiblePagesInk()
        {
            if (inkCanvas == null) return;

            // 当前画布上的全部墨迹（画布坐标）。
            var canvasInk = CaptureCanvasStrokesInPageSpace();

            // 没有可见页列表（单页模式）时，整块画布墨迹归属当前页。
            // 也要转页面局部坐标（减内容矩形原点），与双页/长条模式一致，
            // 否则单页墨迹是画布坐标，切到其它模式时坐标系不匹配而错位。
            if (_pluginVisiblePages.Count == 0)
            {
                var rect = _pluginPageContentRect;
                if (rect.HasValue)
                    _pluginPageInk[_pluginCurrentPageIndex] = TranslateStrokes(canvasInk, -rect.Value.X, -rect.Value.Y);
                else
                    _pluginPageInk[_pluginCurrentPageIndex] = canvasInk;
                return;
            }

            foreach (var (pageIndex, rect) in _pluginVisiblePages)
            {
                var cropped = CropStrokesToRect(canvasInk, rect);
                // 转页面局部坐标：墨迹减去页矩形原点。
                _pluginPageInk[pageIndex] = TranslateStrokes(cropped, -rect.X, -rect.Y);
            }
        }

        /// <summary>
        /// 清空画布并恢复各可见页墨迹：从页面局部坐标（加页矩形原点）映射回画布坐标。
        /// </summary>
        private void ReplaceVisiblePagesInk()
        {
            if (inkCanvas == null) return;

            var matrix = GetCanvasToPageMatrix();
            if (matrix.HasInverse) matrix.Invert();
            else matrix = Matrix.Identity;

            var previousCommitType = _currentCommitType;
            _currentCommitType = CommitReason.CodeInput;
            try
            {
                inkCanvas.Strokes.Clear();
                foreach (var (pageIndex, rect) in _pluginVisiblePages)
                {
                    if (!_pluginPageInk.TryGetValue(pageIndex, out var pageInk)) continue;

                    // 页面局部坐标 → 画布坐标：加页矩形原点，再应用画布矩阵。
                    var inCanvas = TranslateStrokes(pageInk, rect.X, rect.Y);
                    var restored = CloneStrokes(inCanvas, matrix);
                    if (restored.Count > 0) inkCanvas.Strokes.Add(restored);
                }
                HideEdgeExpandHint();
            }
            finally
            {
                _currentCommitType = previousCommitType;
            }
        }

        /// <summary>克隆墨迹并整体平移。</summary>
        private static StrokeCollection TranslateStrokes(StrokeCollection source, double dx, double dy)
        {
            var result = new StrokeCollection();
            if (source == null || (dx == 0 && dy == 0))
            {
                if (source != null) result.Add(source);
                return result;
            }

            var matrix = new Matrix(1, 0, 0, 1, dx, dy);
            foreach (Stroke stroke in source)
            {
                var clone = stroke.Clone();
                clone.Transform(matrix, false);
                result.Add(clone);
            }

            return result;
        }

        /// <summary>把墨迹裁剪到矩形内：保留落在矩形内的点，形成连续段笔画。</summary>
        private static StrokeCollection CropStrokesToRect(StrokeCollection source, Rect rect)
        {
            var result = new StrokeCollection();
            if (source == null || rect.IsEmpty) return result;

            foreach (Stroke stroke in source)
            {
                if (stroke.GetBounds().IntersectsWith(rect))
                {
                    CropSingleStroke(result, stroke, rect);
                }
            }

            return result;
        }

        /// <summary>把单条笔画按矩形切分成若干落在矩形内的子笔画。</summary>
        private static void CropSingleStroke(StrokeCollection target, Stroke stroke, Rect rect)
        {
            List<StylusPoint> segment = null;

            foreach (StylusPoint point in stroke.StylusPoints)
            {
                if (rect.Contains(point.X, point.Y))
                {
                    if (segment == null) segment = new List<StylusPoint>();
                    segment.Add(point);
                }
                else
                {
                    FlushSegment(target, segment, stroke);
                    segment = null;
                }
            }
            FlushSegment(target, segment, stroke);
        }

        private static void FlushSegment(StrokeCollection target, List<StylusPoint> segment, Stroke source)
        {
            if (segment == null || segment.Count == 0) return;

            var stroke = new Stroke(new StylusPointCollection(segment), source.DrawingAttributes.Clone());
            target.Add(stroke);
            segment.Clear();
        }

        private void ValidatePluginPageIndex(uint pageIndex)
        {
            if (pageIndex >= EffectivePluginPageCount)
            {
                throw new ArgumentOutOfRangeException(nameof(pageIndex),
                    $"页索引 {pageIndex} 超出范围，总页数为 {EffectivePluginPageCount}。");
            }
        }

        #endregion

        #region 墨迹 / 页面坐标

        /// <summary>
        /// 画布坐标 → 页面坐标的变换。背景层铺满画布时两者原点重合，此处仍显式计算以兼容带边距的背景元素。
        /// 目标优先用插件声明的内容锚点（<see cref="_pluginContentAnchor"/>）：内容锚点带缩放/平移变换时，
        /// TransformToVisual 会把该变换正确纳入墨迹换算；未声明时回退到背景层根节点。
        /// </summary>
        private Matrix GetCanvasToPageMatrix()
        {
            FrameworkElement anchor = _pluginContentAnchor ?? _pluginBackgroundLayer;
            if (anchor == null || inkCanvas == null) return Matrix.Identity;

            try
            {
                var transform = inkCanvas.TransformToVisual(anchor);
                if (transform is MatrixTransform matrixTransform) return matrixTransform.Matrix;

                var origin = transform.Transform(new Point(0, 0));
                var matrix = Matrix.Identity;
                matrix.Translate(origin.X, origin.Y);
                return matrix;
            }
            catch (InvalidOperationException)
            {
                // 锚点尚未接入可视化树时按重合处理。
                return Matrix.Identity;
            }
        }

        private static StrokeCollection CloneStrokes(StrokeCollection source, Matrix matrix)
        {
            var result = new StrokeCollection();
            if (source == null) return result;

            foreach (Stroke stroke in source)
            {
                var clone = stroke.Clone();
                if (!matrix.IsIdentity) clone.Transform(matrix, false);
                result.Add(clone);
            }

            return result;
        }

        /// <summary>把当前画布上的墨迹复制一份并换算到页面坐标系。</summary>
        private StrokeCollection CaptureCanvasStrokesInPageSpace()
            => CloneStrokes(inkCanvas?.Strokes, GetCanvasToPageMatrix());

        /// <summary>用页面局部坐标的墨迹替换画布内容（加内容矩形原点映射回画布），不写入时间机器历史。</summary>
        private void ReplaceCanvasStrokesFromPageSpace(StrokeCollection pageStrokes)
        {
            if (inkCanvas == null) return;

            var matrix = GetCanvasToPageMatrix();
            if (matrix.HasInverse) matrix.Invert();
            else matrix = Matrix.Identity;

            var previousCommitType = _currentCommitType;
            _currentCommitType = CommitReason.CodeInput;
            try
            {
                inkCanvas.Strokes.Clear();

                // 页面局部坐标 → 画布坐标：加内容矩形原点，再应用画布矩阵。
                var rect = _pluginPageContentRect;
                var inCanvas = rect.HasValue
                    ? TranslateStrokes(pageStrokes, rect.Value.X, rect.Value.Y)
                    : pageStrokes;
                var restored = CloneStrokes(inCanvas, matrix);
                if (restored.Count > 0) inkCanvas.Strokes.Add(restored);
                HideEdgeExpandHint();
            }
            finally
            {
                _currentCommitType = previousCommitType;
            }
        }

        internal Task<StrokeCollection> GetPluginPageStrokesAsync(uint pageIndex, CancellationToken cancellationToken)
        {
            return RunOnUiThreadAsync(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                ValidatePluginPageIndex(pageIndex);
                return GetPluginPageStrokesCore(pageIndex);
            });
        }

        private StrokeCollection GetPluginPageStrokesCore(uint pageIndex)
        {
            // 多可见页模式：当前画布同时显示多页，按矩形实时裁剪该页的墨迹并转页面局部坐标，
            // 保证导出包含刚画下的内容（尚未翻页保存），且与缓存坐标一致。
            if (_pluginVisiblePages.Count > 0)
            {
                foreach (var (page, rect) in _pluginVisiblePages)
                {
                    if (page == pageIndex)
                    {
                        var cropped = CropStrokesToRect(CaptureCanvasStrokesInPageSpace(), rect);
                        return TranslateStrokes(cropped, -rect.X, -rect.Y);
                    }
                }
            }

            // 单页模式：当前页以画布上的实时墨迹为准，转页面局部坐标（减内容矩形原点）。
            if (pageIndex == _pluginCurrentPageIndex)
            {
                var rect = _pluginPageContentRect;
                if (rect.HasValue)
                    return TranslateStrokes(CaptureCanvasStrokesInPageSpace(), -rect.Value.X, -rect.Value.Y);
                return CaptureCanvasStrokesInPageSpace();
            }

            return _pluginPageInk.TryGetValue(pageIndex, out var cached)
                ? CloneStrokes(cached, Matrix.Identity)
                : new StrokeCollection();
        }

        #endregion

        #region 导出渲染

        /// <summary>
        /// 计算 <paramref name="startPageIndex"/> 起需要导出的页序列。
        /// 未提供离屏渲染回调时只能合成当前页。
        /// </summary>
        internal Task<List<uint>> GetPluginExportPagesAsync(uint startPageIndex, CancellationToken cancellationToken)
        {
            return RunOnUiThreadAsync(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                ValidatePluginPageIndex(startPageIndex);

                var pages = new List<uint>();
                if (_pluginPageRenderer == null)
                {
                    if (startPageIndex != _pluginCurrentPageIndex)
                    {
                        throw new InvalidOperationException(
                            "未提供离屏渲染回调（ConfigurePages 的 pageRenderer 为 null），只能导出当前页。");
                    }

                    pages.Add(startPageIndex);
                    LogHelper.WriteLogToFile(
                        "插件未提供离屏渲染回调，导出降级为仅当前页。", LogHelper.LogType.Warning);
                    return pages;
                }

                for (var page = startPageIndex; page < EffectivePluginPageCount; page++) pages.Add(page);
                return pages;
            });
        }

        /// <summary>把指定页的「背景 + 墨迹」合成为一张位图。</summary>
        internal async Task<PluginPageRender> RenderPluginPageAsync(uint pageIndex, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 先在 UI 线程取齐所有需要的状态与数据，之后的重活全部离开 UI 线程。
            var plan = await RunOnUiThreadAsync(() =>
            {
                ValidatePluginPageIndex(pageIndex);

                // 导出某物理页时，优先用该页在可见页列表里的内容矩形尺寸，
                // 双页模式下这样才能按单页导出（否则会取整块画布的 16:9 尺寸）。
                var pageRect = GetPluginPageRect(pageIndex);
                GetPluginPageSize(pageRect, out var widthDip, out var heightDip);

                // 墨迹必须在 UI 线程读取。GetPluginPageStrokesCore 返回的是克隆副本，
                // 只归本次合成任务独占使用，因此可以安全地交给后台线程绘制。
                // 注意 Stroke 不是 Freezable，无法冻结，线程安全完全依赖「不共享」这一点。
                var strokes = GetPluginPageStrokesCore(pageIndex);

                // 导出诊断：记录每页读到的墨迹条数与是否来自缓存。
                LogHelper.WriteLogToFile(
                    $"导出页 {pageIndex} 墨迹条数={strokes.Count} " +
                    $"可见页=[{string.Join(",", _pluginVisiblePages.Select(p => p.PageIndex))}] " +
                    $"当前页={_pluginCurrentPageIndex}",
                    LogHelper.LogType.Info);

                return new PluginPagePlan
                {
                    Renderer = _pluginPageRenderer,
                    WidthDip = widthDip,
                    HeightDip = heightDip,
                    ContentRect = pageRect,
                    Strokes = strokes,
                    IsCurrentPage = pageIndex == _pluginCurrentPageIndex
                };
            }).ConfigureAwait(false);

            BitmapSource background = null;
            if (plan.Renderer != null)
            {
                background = await plan.Renderer(pageIndex, cancellationToken).ConfigureAwait(false);
                if (background != null && background.CanFreeze && !background.IsFrozen) background.Freeze();
            }

            cancellationToken.ThrowIfCancellationRequested();

            // 没有离屏位图时只能抓实时视觉树，那必须回到 UI 线程；
            // 其余情况（导出的正常路径）在线程池上合成，不占用 UI。
            if (background == null && plan.IsCurrentPage && _pluginBackgroundLayer != null)
            {
                return await RunOnUiThreadAsync(
                    () => ComposePluginPage(plan, null, cancellationToken)).ConfigureAwait(false);
            }

            return await Task.Run(
                () => ComposePluginPage(plan, background, cancellationToken), cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>单页合成所需的全部输入，在 UI 线程一次性取齐后交给后台线程。</summary>
        private sealed class PluginPagePlan
        {
            public Func<uint, CancellationToken, Task<BitmapSource>> Renderer;
            public double WidthDip;
            public double HeightDip;
            public Rect? ContentRect;
            public StrokeCollection Strokes;
            public bool IsCurrentPage;
        }

        private PluginPageRender ComposePluginPage(PluginPagePlan plan, BitmapSource background,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 页面区域：插件声明了内容矩形就用它（背景 Uniform 居中时的实际页面范围），
            // 否则回落到整个背景层/画布。
            var widthDip = plan.WidthDip;
            var heightDip = plan.HeightDip;
            var contentRect = plan.ContentRect;
            var originX = contentRect?.X ?? 0;
            var originY = contentRect?.Y ?? 0;

            var scale = GetPluginRenderScale(background, widthDip);
            var pixelWidth = Math.Max(1, (int)Math.Ceiling(widthDip * scale));
            var pixelHeight = Math.Max(1, (int)Math.Ceiling(heightDip * scale));
            var strokes = plan.Strokes;
            var useLiveVisual = background == null && plan.IsCurrentPage;

            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                var fullRect = new Rect(0, 0, pixelWidth, pixelHeight);
                context.DrawRectangle(Brushes.White, null, fullRect);

                if (background != null)
                {
                    // 背景位图是整页渲染，按内容矩形等比（Uniform）铺入。
                    var bgRect = UniformRect(background, fullRect);
                    context.DrawImage(background, bgRect);

                    // 墨迹已是「页面局部坐标」（页矩形原点为 0，与展示模式无关），
                    // 只需按 DIP→像素 缩放，不再平移。
                    double bgScale = bgRect.Width / widthDip;

                    // 诊断：记录墨迹变换前的原始边界，区分「墨迹本身宽」与「变换后宽」。
                    Rect rawBounds = new Rect();
                    foreach (Stroke s in plan.Strokes)
                    {
                        var b = s.GetBounds();
                        if (b.IsEmpty) continue;
                        if (rawBounds.IsEmpty) rawBounds = b;
                        else rawBounds.Union(b);
                    }

                    strokes = CloneStrokesScaled(strokes, bgScale, 0, 0);

                    Rect inkBounds = new Rect();
                    foreach (Stroke s in strokes)
                    {
                        var b = s.GetBounds();
                        if (b.IsEmpty) continue;
                        if (inkBounds.IsEmpty) inkBounds = b;
                        else inkBounds.Union(b);
                    }
                    LogHelper.WriteLogToFile(
                        $"导出几何 widthDip={widthDip:F1} heightDip={heightDip:F1} " +
                        $"origin=({originX:F1},{originY:F1}) bgRect=({bgRect.X:F1},{bgRect.Y:F1},{bgRect.Width:F1},{bgRect.Height:F1}) " +
                        $"bgScale={bgScale:F3} 原始墨迹=({rawBounds.X:F1},{rawBounds.Y:F1},{rawBounds.Width:F1},{rawBounds.Height:F1}) " +
                        $"变换后=({inkBounds.X:F1},{inkBounds.Y:F1},{inkBounds.Width:F1},{inkBounds.Height:F1}) " +
                        $"fullRect=({fullRect.X:F0},{fullRect.Y:F0},{fullRect.Width:F0},{fullRect.Height:F0})",
                        LogHelper.LogType.Info);
                }
                else if (useLiveVisual && _pluginBackgroundLayer != null)
                {
                    // 没有离屏回调时直接抓当前背景层的实时呈现；有内容矩形则只取该区域。
                    var brush = new VisualBrush(_pluginBackgroundLayer) { Stretch = Stretch.None };
                    if (contentRect.HasValue)
                    {
                        brush.ViewboxUnits = BrushMappingMode.Absolute;
                        brush.Viewbox = contentRect.Value;
                    }
                    else
                    {
                        brush.Stretch = Stretch.Fill;
                    }
                    context.DrawRectangle(brush, null, fullRect);
                }

                // 墨迹已按背景缩放并平移到位，直接绘制。
                foreach (Stroke stroke in strokes) stroke.Draw(context);
            }

            var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();

            return new PluginPageRender
            {
                Bitmap = bitmap,
                WidthDip = widthDip,
                HeightDip = heightDip
            };
        }

        /// <summary>计算图片 Uniform（等比）缩放后铺入目标矩形的实际区域。</summary>
        private static Rect UniformRect(BitmapSource image, Rect target)
        {
            if (image == null || image.PixelWidth <= 0 || image.PixelHeight <= 0) return target;
            double scale = Math.Min(target.Width / image.PixelWidth, target.Height / image.PixelHeight);
            double w = image.PixelWidth * scale;
            double h = image.PixelHeight * scale;
            return new Rect(
                target.X + (target.Width - w) / 2,
                target.Y + (target.Height - h) / 2,
                w, h);
        }

        /// <summary>
        /// 克隆墨迹并应用「先平移、再缩放」：把页面局部坐标（内容矩形原点为 0）
        /// 映射到导出位图坐标。返回新集合，不改原墨迹。
        /// </summary>
        private static StrokeCollection CloneStrokesScaled(StrokeCollection source,
            double scale, double offsetX, double offsetY)
        {
            var result = new StrokeCollection();
            if (source == null) return result;

            foreach (Stroke stroke in source)
            {
                var clone = stroke.Clone();
                clone.Transform(new Matrix(scale, 0, 0, scale, offsetX * scale, offsetY * scale), false);
                result.Add(clone);
            }

            return result;
        }

        /// <summary>
        /// 页面尺寸即页面坐标系尺度：插件声明的内容矩形优先（保持页面原始宽高比），
        /// 其次背景层，再次画布，最后回落到 1920x1080。
        /// <summary>
        /// 获取指定物理页的内容矩形：优先用可见页列表里该页的矩形（双页模式），
        /// 否则用单页内容矩形，都没有则回落到 null。
        /// </summary>
        private Rect? GetPluginPageRect(uint pageIndex)
        {
            // 优先用当前可见页列表里的矩形（双页模式）。
            foreach (var (page, rect) in _pluginVisiblePages)
            {
                if (page == pageIndex) return rect;
            }

            // 再查历史矩形（该页曾可见时记录），导出非可见页时需要它还原墨迹的页面局部坐标。
            if (_pluginPageRects.TryGetValue(pageIndex, out var historical))
                return historical;

            return _pluginPageContentRect;
        }

        /// <summary>
        /// 页面尺寸即页面坐标系尺度：优先取传入的内容矩形，
        /// 其次背景层 ActualWidth/Height，再其次画布，最后回落到 1920x1080。
        /// </summary>
        private void GetPluginPageSize(Rect? contentRect, out double widthDip, out double heightDip)
        {
            if (contentRect.HasValue && contentRect.Value.Width > 0 && contentRect.Value.Height > 0)
            {
                widthDip = contentRect.Value.Width;
                heightDip = contentRect.Value.Height;
                return;
            }

            widthDip = _pluginBackgroundLayer?.ActualWidth ?? 0;
            heightDip = _pluginBackgroundLayer?.ActualHeight ?? 0;

            if (widthDip <= 0 || heightDip <= 0)
            {
                widthDip = inkCanvas?.ActualWidth ?? 0;
                heightDip = inkCanvas?.ActualHeight ?? 0;
            }

            if (widthDip <= 0 || heightDip <= 0)
            {
                widthDip = 1920;
                heightDip = 1080;
            }
        }

        private static double GetPluginRenderScale(BitmapSource background, double widthDip)
        {
            if (background == null || widthDip <= 0) return PluginDefaultRenderScale;

            // 跟随插件给出的位图分辨率，避免把高清页面渲染糊掉或把大图无谓放大。
            return Math.Max(1.0, Math.Min(4.0, background.PixelWidth / widthDip));
        }

        #endregion

        #region 线程调度

        private void RunOnUiThread(Action action)
        {
            if (Dispatcher.CheckAccess()) action();
            else Dispatcher.Invoke(action);
        }

        private Task RunOnUiThreadAsync(Action action)
        {
            if (Dispatcher.CheckAccess())
            {
                action();
                return Task.CompletedTask;
            }

            return Dispatcher.InvokeAsync(action).Task;
        }

        private Task<T> RunOnUiThreadAsync<T>(Func<T> func)
        {
            return Dispatcher.CheckAccess()
                ? Task.FromResult(func())
                : Dispatcher.InvokeAsync(func).Task;
        }

        #endregion
    }
}
