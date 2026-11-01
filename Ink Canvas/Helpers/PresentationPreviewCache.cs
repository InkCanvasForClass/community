using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Windows.Media;
using Ink_Canvas.Controls;

namespace Ink_Canvas.Helpers
{
    // UI-thread owned. Only realized list items request native renders; pending work
    // is cancelled when recycled and resident thumbnails have a bounded LRU.
    internal sealed class PresentationPreviewCache : IDisposable
    {
        private readonly Func<int, CancellationToken, Task<BitmapSource>> _render;
        private readonly Action<Exception> _error;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly Dictionary<int, int> _visible = new();
        private readonly Dictionary<int, CancellationTokenSource> _pending = new();
        private readonly LinkedList<int> _lru = new();
        private bool _disposed;
        internal IList<PPTNavBar.PreviewItem> Items { get; }
        internal int ResidentCount => _lru.Count;
        internal bool Owns(PPTNavBar.PreviewItem item) => item != null && item.SlideNumber >= 1
            && item.SlideNumber <= Items.Count && ReferenceEquals(Items[item.SlideNumber - 1], item);

        internal PresentationPreviewCache(int pages, Func<int, CancellationToken, Task<BitmapSource>> render, Action<Exception> error = null)
        {
            _render = render ?? throw new ArgumentNullException(nameof(render)); _error = error;
            Items = Enumerable.Range(1, pages).Select(page => new PPTNavBar.PreviewItem { SlideNumber = page }).ToArray();
        }

        internal void Retain(int page)
        {
            if (_disposed || page < 1 || page > Items.Count) return;
            _visible[page] = _visible.GetValueOrDefault(page) + 1;
            if (Items[page - 1].Thumbnail != null) { _lru.Remove(page); _lru.AddLast(page); return; }
            if (_pending.ContainsKey(page)) return;
            var request = new CancellationTokenSource();
            _pending.Add(page, request);
            _ = LoadAsync(page, request);
        }

        internal void Release(int page)
        {
            if (!_visible.TryGetValue(page, out int count)) return;
            if (count > 1) { _visible[page] = count - 1; return; }
            _visible.Remove(page);
            if (_pending.Remove(page, out var request)) request.Cancel();
            Trim();
        }

        private async Task LoadAsync(int page, CancellationTokenSource request)
        {
            var token = request.Token;
            bool entered = false;
            try
            {
                await _gate.WaitAsync(token); entered = true;
                var bitmap = await _render(page, token);
                if (_disposed || token.IsCancellationRequested || !_visible.ContainsKey(page)) return;
                if (bitmap != null && Math.Max(bitmap.PixelWidth, bitmap.PixelHeight) > 256)
                {
                    double ratio = 256.0 / Math.Max(bitmap.PixelWidth, bitmap.PixelHeight);
                    int width = Math.Max(1, (int)(bitmap.PixelWidth * ratio)), height = Math.Max(1, (int)(bitmap.PixelHeight * ratio));
                    var visual = new DrawingVisual();
                    using (var drawing = visual.RenderOpen()) drawing.DrawImage(bitmap, new Rect(0, 0, width, height));
                    var bounded = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                    bounded.Render(visual); bounded.Freeze(); bitmap = bounded;
                }
                if (bitmap != null && !bitmap.IsFrozen) bitmap.Freeze();
                Items[page - 1].Thumbnail = bitmap;
                if (bitmap != null) { _lru.Remove(page); _lru.AddLast(page); Trim(); }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (!_disposed && !token.IsCancellationRequested) _error?.Invoke(ex); }
            finally
            {
                if (_pending.TryGetValue(page, out var current) && ReferenceEquals(current, request)) _pending.Remove(page);
                if (entered) _gate.Release();
                request.Dispose();
            }
        }

        private void Trim()
        {
            while (_lru.Count > Math.Max(16, _visible.Count))
            {
                var old = _lru.First;
                while (old != null && _visible.ContainsKey(old.Value)) old = old.Next;
                if (old == null) break;
                Items[old.Value - 1].Thumbnail = null; _lru.Remove(old);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var request in _pending.Values.ToArray()) request.Cancel();
            _pending.Clear(); _visible.Clear();
            foreach (int page in _lru) Items[page - 1].Thumbnail = null;
            _lru.Clear();
        }
    }
}
