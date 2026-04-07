using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 使用 Windows.Data.Pdf（WinRT）将 PDF 页渲染为 WPF 可用的位图。
    /// </summary>
    internal static class PdfWinRtHelper
    {
        public static async Task<uint> GetPageCountAsync(string pdfPath)
        {
            if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
                return 0;

            var file = await StorageFile.GetFileFromPathAsync(pdfPath).AsTask();
            var doc = await PdfDocument.LoadFromFileAsync(file).AsTask();
            if (doc.IsPasswordProtected)
                return 0;
            return doc.PageCount;
        }

        public static async Task<BitmapSource> RenderPageToBitmapSourceAsync(string pdfPath, uint pageIndex)
        {
            if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
                return null;

            var file = await StorageFile.GetFileFromPathAsync(pdfPath).AsTask();
            var doc = await PdfDocument.LoadFromFileAsync(file).AsTask();
            if (doc.IsPasswordProtected)
                return null;
            if (pageIndex >= doc.PageCount)
                return null;

            var page = doc.GetPage(pageIndex);
            try
            {
                using (var ras = new InMemoryRandomAccessStream())
                {
                    await page.RenderToStreamAsync(ras).AsTask();
                    ras.Seek(0);

                    var ms = new MemoryStream();
                    using (var netStream = ras.AsStreamForRead())
                        netStream.CopyTo(ms);
                    ms.Position = 0;

                    try
                    {
                        return await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            var bi = new BitmapImage();
                            bi.BeginInit();
                            bi.StreamSource = ms;
                            bi.CacheOption = BitmapCacheOption.OnLoad;
                            bi.EndInit();
                            bi.Freeze();
                            return (BitmapSource)bi;
                        });
                    }
                    finally
                    {
                        ms.Dispose();
                    }
                }
            }
            finally
            {
                (page as IDisposable)?.Dispose();
            }
        }
    }
}
