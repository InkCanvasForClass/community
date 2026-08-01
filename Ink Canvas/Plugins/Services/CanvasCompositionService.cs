using Ink_Canvas.Helpers;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// <see cref="ICanvasCompositionService"/> 的宿主实现：背景层与墨迹逻辑落在 MainWindow，
    /// 本类负责参数校验、线程转发，以及把逐页合成的图片组装成 PDF。
    /// </summary>
    internal sealed class CanvasCompositionService : ICanvasCompositionService
    {
        /// <summary>PDF 用户单位为 1/72 英寸，WPF 设备无关像素为 1/96 英寸。</summary>
        private const double DipToPoint = 72.0 / 96.0;

        private readonly MainWindow _mainWindow;

        public CanvasCompositionService(MainWindow mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        }

        public bool HasBackgroundLayer => _mainWindow.HasPluginBackgroundLayer;

        public uint PageCount => _mainWindow.PluginPageCount;

        public uint CurrentPageIndex => _mainWindow.PluginCurrentPageIndex;

        public void InjectBackgroundLayer(Func<FrameworkElement> backgroundFactory)
            => _mainWindow.InjectPluginBackgroundLayer(backgroundFactory);

        public void RemoveBackgroundLayer()
            => _mainWindow.RemovePluginBackgroundLayer();

        public void SetPageContentRect(Rect? contentRect)
            => _mainWindow.SetPluginPageContentRect(contentRect);

        public void ConfigurePages(uint pageCount, uint currentPageIndex,
            Func<uint, CancellationToken, Task<BitmapSource>> pageRenderer)
            => _mainWindow.ConfigurePluginPages(pageCount, currentPageIndex, pageRenderer);

        public Task SetCurrentPageAsync(uint pageIndex, CancellationToken cancellationToken = default)
            => _mainWindow.SetPluginCurrentPageAsync(pageIndex, cancellationToken);

        public Task SetVisiblePagesAsync(IReadOnlyList<PluginVisiblePage> visiblePages,
            CancellationToken cancellationToken = default)
            => _mainWindow.SetPluginVisiblePagesAsync(visiblePages, cancellationToken);

        public Task ScrollOffsetAsync(double deltaY, CancellationToken cancellationToken = default)
            => _mainWindow.ScrollPluginOffsetAsync(deltaY, cancellationToken);

        public void SetCanvasGestureHandler(IPluginCanvasGestureHandler handler)
            => _mainWindow.SetPluginCanvasGestureHandler(handler);

        public void SetCanvasContentAnchor(FrameworkElement contentLayer)
            => _mainWindow.SetPluginCanvasContentAnchor(contentLayer);

        public Task TransformInkAsync(Matrix matrix, CancellationToken cancellationToken = default)
            => _mainWindow.TransformPluginInkAsync(matrix, cancellationToken);

        public Task<StrokeCollection> GetStrokesForPageAsync(uint pageIndex,
            CancellationToken cancellationToken = default)
            => _mainWindow.GetPluginPageStrokesAsync(pageIndex, cancellationToken);

        public async Task<string> ExportWithInkAsync(string outputPath, uint pageIndex,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("输出路径不能为空。", nameof(outputPath));

            var fullPath = Path.GetFullPath(outputPath);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var pages = await _mainWindow.GetPluginExportPagesAsync(pageIndex, cancellationToken)
                .ConfigureAwait(false);

            // 逐页「合成 + 编码」彼此独立，可并行；PdfSharp 组装必须串行且按页序，
            // 因此先并行产出各页字节，再按序写入文档。
            // 并行度取 CPU-1（至少 2、最多 4）：合成会短暂回到 UI 线程取状态，
            // 放太开反而加剧 UI 线程争用，且每页位图占内存不小。
            var parallelism = Math.Max(2, Math.Min(4, Environment.ProcessorCount - 1));
            var encoded = new byte[pages.Count][];
            var sizes = new (double Width, double Height)[pages.Count];

            using (var throttle = new SemaphoreSlim(parallelism, parallelism))
            {
                var tasks = new Task[pages.Count];
                for (var i = 0; i < pages.Count; i++)
                {
                    var index = i;
                    var page = pages[index];
                    tasks[index] = Task.Run(async () =>
                    {
                        await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
                        try
                        {
                            var render = await _mainWindow.RenderPluginPageAsync(page, cancellationToken)
                                .ConfigureAwait(false);
                            if (render?.Bitmap == null)
                            {
                                LogHelper.WriteLogToFile($"导出时第 {page} 页合成失败，已跳过。", LogHelper.LogType.Warning);
                                return;
                            }

                            encoded[index] = EncodePage(render.Bitmap);
                            sizes[index] = (render.WidthDip, render.HeightDip);
                        }
                        finally
                        {
                            throttle.Release();
                        }
                    }, cancellationToken);
                }

                await Task.WhenAll(tasks).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();

            using (var document = new PdfDocument())
            {
                for (var i = 0; i < pages.Count; i++)
                {
                    var bytes = encoded[i];
                    if (bytes == null || bytes.Length == 0)
                    {
                        LogHelper.WriteLogToFile($"导出时第 {pages[i]} 页编码失败，已跳过。", LogHelper.LogType.Warning);
                        continue;
                    }

                    AppendPage(document, bytes, sizes[i].Width, sizes[i].Height);
                }

                if (document.PageCount == 0)
                    throw new InvalidOperationException("没有任何页面被成功合成，导出已中止。");

                document.Save(fullPath);
            }

            LogHelper.WriteLogToFile($"插件导出「背景 + 墨迹」PDF 完成: {fullPath}", LogHelper.LogType.Info);
            return fullPath;
        }

        /// <summary>
        /// 把合成结果编码为 JPEG。相比 PNG 的 deflate，JPEG 编码快数倍、体积也小得多，
        /// 而页面已是「PDF 栅格 + 墨迹」的照片型内容，JPEG 的画质损失在 92 质量下不可见。
        /// </summary>
        private static byte[] EncodePage(BitmapSource bitmap)
        {
            var encoder = new JpegBitmapEncoder { QualityLevel = 92 };
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = new MemoryStream())
            {
                encoder.Save(stream);
                return stream.ToArray();
            }
        }

        private static void AppendPage(PdfDocument document, byte[] imageBytes,
            double widthDip, double heightDip)
        {
            // XImage.FromStream 不复制流，必须活到 document.Save 之后，因此不在此处 Dispose。
            // 注意：不能用 new MemoryStream(bytes) —— 该构造函数产生的流 publiclyVisible=false，
            // PDFsharp 内部调用 GetBuffer() 读取原始字节时会抛
            // "MemoryStream's internal buffer cannot be accessed."。
            // 无参构造 + Write 得到的流才允许 GetBuffer()。
            var stream = new MemoryStream(imageBytes.Length);
            stream.Write(imageBytes, 0, imageBytes.Length);
            stream.Position = 0;

            var image = XImage.FromStream(stream);

            var page = document.AddPage();
            page.Width = XUnit.FromPoint(widthDip * DipToPoint);
            page.Height = XUnit.FromPoint(heightDip * DipToPoint);

            using (var gfx = XGraphics.FromPdfPage(page))
            {
                gfx.DrawImage(image, new XRect(0, 0, page.Width.Point, page.Height.Point));
            }
        }
    }
}
