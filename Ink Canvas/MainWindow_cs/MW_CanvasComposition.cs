using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
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

        /// <summary>按页缓存的墨迹，坐标已绑定到背景层页面坐标系。</summary>
        private readonly Dictionary<uint, StrokeCollection> _pluginPageInk = new Dictionary<uint, StrokeCollection>();

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
                _pluginPageInk.Clear();
                _pluginPageCount = 0;
                _pluginCurrentPageIndex = 0;
                _pluginPageRenderer = null;
                _pluginPageContentRect = null;
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

                // 先把画布上的墨迹按页面坐标存回原页，再换成目标页的墨迹。
                _pluginPageInk[_pluginCurrentPageIndex] = CaptureCanvasStrokesInPageSpace();
                _pluginCurrentPageIndex = pageIndex;

                _pluginPageInk.TryGetValue(pageIndex, out var target);
                ReplaceCanvasStrokesFromPageSpace(target);
            });
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
        /// </summary>
        private Matrix GetCanvasToPageMatrix()
        {
            if (_pluginBackgroundLayer == null || inkCanvas == null) return Matrix.Identity;

            try
            {
                var transform = inkCanvas.TransformToVisual(_pluginBackgroundLayer);
                if (transform is MatrixTransform matrixTransform) return matrixTransform.Matrix;

                var origin = transform.Transform(new Point(0, 0));
                var matrix = Matrix.Identity;
                matrix.Translate(origin.X, origin.Y);
                return matrix;
            }
            catch (InvalidOperationException)
            {
                // 背景层尚未接入可视化树时按重合处理。
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

        /// <summary>用页面坐标系的墨迹替换画布内容，不写入时间机器历史。</summary>
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
                var restored = CloneStrokes(pageStrokes, matrix);
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
            // 当前页以画布上的实时墨迹为准，其余页读缓存。
            if (pageIndex == _pluginCurrentPageIndex) return CaptureCanvasStrokesInPageSpace();

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
                GetPluginPageSize(out var widthDip, out var heightDip);

                // 墨迹必须在 UI 线程读取并克隆；Stroke 不是线程安全的，
                // 但 Freeze 后的克隆可以安全地在后台线程绘制。
                var strokes = GetPluginPageStrokesCore(pageIndex);
                foreach (Stroke stroke in strokes)
                {
                    if (stroke.CanFreeze && !stroke.IsFrozen) stroke.Freeze();
                }

                return new PluginPagePlan
                {
                    Renderer = _pluginPageRenderer,
                    WidthDip = widthDip,
                    HeightDip = heightDip,
                    ContentRect = _pluginPageContentRect,
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
                    // 页面区域已按内容矩形裁定，此处铺满即为 1:1 还原，不会改变宽高比。
                    context.DrawImage(background, fullRect);
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

                // 墨迹在背景元素坐标系里，先平移掉内容矩形原点，再按渲染倍率缩放。
                context.PushTransform(new ScaleTransform(scale, scale));
                if (originX != 0 || originY != 0)
                    context.PushTransform(new TranslateTransform(-originX, -originY));
                try
                {
                    foreach (Stroke stroke in strokes) stroke.Draw(context);
                }
                finally
                {
                    if (originX != 0 || originY != 0) context.Pop();
                    context.Pop();
                }
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

        /// <summary>
        /// 页面尺寸即页面坐标系尺度：插件声明的内容矩形优先（保持页面原始宽高比），
        /// 其次背景层，再次画布，最后回落到 1920x1080。
        /// </summary>
        private void GetPluginPageSize(out double widthDip, out double heightDip)
        {
            var contentRect = _pluginPageContentRect;
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
