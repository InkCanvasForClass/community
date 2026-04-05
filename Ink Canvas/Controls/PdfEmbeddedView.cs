using Ink_Canvas.Helpers;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Ink_Canvas.Controls
{
    /// <summary>
    /// 画布上的多页 PDF：仅显示当前页；翻页与页码由主窗口 PDF 侧栏控制（无 XAML 文件）。
    /// </summary>
    public class PdfEmbeddedView : UserControl
    {
        private readonly Image _pageImage;

        private string _pdfPath;
        private uint _pageCount;
        private uint _currentIndex;
        private bool _compressLargePictures;
        private bool _isPagingBusy;
        private bool _layoutSizeCommitted;

        /// <summary>页码或可翻页状态变化（用于更新侧栏）。</summary>
        public event EventHandler PageNavigationStateChanged;

        public PdfEmbeddedView()
        {
            MinWidth = 80;
            MinHeight = 60;

            var grid = new Grid { ClipToBounds = true };
            _pageImage = new Image
            {
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            grid.Children.Add(_pageImage);
            Content = grid;
        }

        /// <summary>
        /// 初始化并显示指定页；由 MainWindow 在 UI 线程创建后调用。
        /// </summary>
        /// <param name="initialPageIndex">从 0 开始的页码，超出范围时夹紧到合法区间。</param>
        public async Task InitializeAsync(string pdfFilePath, uint pageCount, bool compressLargePictures, uint initialPageIndex = 0)
        {
            _pdfPath = pdfFilePath ?? throw new ArgumentNullException(nameof(pdfFilePath));
            _pageCount = pageCount;
            _compressLargePictures = compressLargePictures;
            if (_pageCount == 0)
                _currentIndex = 0;
            else
                _currentIndex = initialPageIndex >= _pageCount ? _pageCount - 1 : initialPageIndex;

            await ShowPageAsync(_currentIndex);
        }

        public string PdfPath => _pdfPath;

        public uint PageCount => _pageCount;

        public uint CurrentPageIndex => _currentIndex;

        public string PageLabelText => _pageCount == 0 ? "" : $"{_currentIndex + 1} / {_pageCount}";

        public bool CanGoPrevious => !_isPagingBusy && _pageCount > 1 && _currentIndex > 0;

        public bool CanGoNext => !_isPagingBusy && _pageCount > 1 && _currentIndex < _pageCount - 1;

        public async Task GoToPreviousPageAsync()
        {
            await GoRelativeAsync(-1);
        }

        public async Task GoToNextPageAsync()
        {
            await GoRelativeAsync(1);
        }

        private void NotifyPageNavigationStateChanged()
        {
            PageNavigationStateChanged?.Invoke(this, EventArgs.Empty);
        }

        private async Task GoRelativeAsync(int delta)
        {
            if (_isPagingBusy || _pageCount <= 1)
                return;
            int next = (int)_currentIndex + delta;
            if (next < 0 || next >= _pageCount)
                return;
            _currentIndex = (uint)next;
            await ShowPageAsync(_currentIndex);
        }

        private async Task ShowPageAsync(uint pageIndex)
        {
            _isPagingBusy = true;
            NotifyPageNavigationStateChanged();
            try
            {
                BitmapSource raw = await PdfWinRtHelper.RenderPageToBitmapSourceAsync(_pdfPath, pageIndex);
                if (raw == null)
                    return;

                BitmapSource display = ApplyCompressionIfNeeded(raw);
                _pageImage.Source = display;
                if (!_layoutSizeCommitted)
                {
                    bool callerSized = !double.IsNaN(Width) && Width > 0 && !double.IsNaN(Height) && Height > 0;
                    if (!callerSized)
                    {
                        Width = display.PixelWidth;
                        Height = display.PixelHeight;
                    }

                    _layoutSizeCommitted = true;
                }
            }
            finally
            {
                _isPagingBusy = false;
                NotifyPageNavigationStateChanged();
            }
        }

        private BitmapSource ApplyCompressionIfNeeded(BitmapSource rendered)
        {
            int width = rendered.PixelWidth;
            int height = rendered.PixelHeight;
            if (_compressLargePictures && (width > 1920 || height > 1080))
            {
                double scaleX = 1920.0 / width;
                double scaleY = 1080.0 / height;
                double scale = Math.Min(scaleX, scaleY);
                return new TransformedBitmap(rendered, new ScaleTransform(scale, scale));
            }

            return rendered;
        }
    }
}
