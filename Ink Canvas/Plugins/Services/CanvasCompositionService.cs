using Ink_Canvas.Helpers;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Ink;
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

        public void ConfigurePages(uint pageCount, uint currentPageIndex,
            Func<uint, CancellationToken, Task<BitmapSource>> pageRenderer)
            => _mainWindow.ConfigurePluginPages(pageCount, currentPageIndex, pageRenderer);

        public Task SetCurrentPageAsync(uint pageIndex, CancellationToken cancellationToken = default)
            => _mainWindow.SetPluginCurrentPageAsync(pageIndex, cancellationToken);

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

            using (var document = new PdfDocument())
            {
                foreach (var page in pages)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var render = await _mainWindow.RenderPluginPageAsync(page, cancellationToken)
                        .ConfigureAwait(false);
                    if (render?.PngBytes == null || render.PngBytes.Length == 0)
                    {
                        LogHelper.WriteLogToFile($"导出时第 {page} 页合成失败，已跳过。", LogHelper.LogType.Warning);
                        continue;
                    }

                    AppendPage(document, render);
                }

                if (document.PageCount == 0)
                    throw new InvalidOperationException("没有任何页面被成功合成，导出已中止。");

                document.Save(fullPath);
            }

            LogHelper.WriteLogToFile($"插件导出「背景 + 墨迹」PDF 完成: {fullPath}", LogHelper.LogType.Info);
            return fullPath;
        }

        private static void AppendPage(PdfDocument document, PluginPageRender render)
        {
            // XImage.FromStream 不复制流，必须活到 document.Save 之后，因此不在此处 Dispose。
            // 注意：不能用 new MemoryStream(bytes) —— 该构造函数产生的流 publiclyVisible=false，
            // PDFsharp 内部调用 GetBuffer() 读取原始字节时会抛
            // "MemoryStream's internal buffer cannot be accessed."。
            // 无参构造 + Write 得到的流才允许 GetBuffer()。
            var stream = new MemoryStream(render.PngBytes.Length);
            stream.Write(render.PngBytes, 0, render.PngBytes.Length);
            stream.Position = 0;

            var image = XImage.FromStream(stream);

            var page = document.AddPage();
            page.Width = XUnit.FromPoint(render.WidthDip * DipToPoint);
            page.Height = XUnit.FromPoint(render.HeightDip * DipToPoint);

            using (var gfx = XGraphics.FromPdfPage(page))
            {
                gfx.DrawImage(image, new XRect(0, 0, page.Width.Point, page.Height.Point));
            }
        }
    }
}
