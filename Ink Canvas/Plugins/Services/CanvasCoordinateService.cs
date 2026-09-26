using System;
using System.Windows;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// <see cref="ICanvasCoordinateService"/> 的宿主实现。
    /// </summary>
    internal sealed class CanvasCoordinateService : ICanvasCoordinateService
    {
        private readonly MainWindow _mainWindow;

        public CanvasCoordinateService(MainWindow mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        }

        private T RunOnUi<T>(Func<T> func)
            => _mainWindow.Dispatcher.CheckAccess() ? func() : _mainWindow.Dispatcher.Invoke(func);

        public Rect CanvasScreenBounds
            => RunOnUi(() => _mainWindow.TryGetPluginCanvasScreenPixels(out var bounds)
                ? bounds : Rect.Empty);

        public Size CanvasSize
            => RunOnUi(() => _mainWindow.GetPluginCanvasSize());

        public bool IsAvailable
            => RunOnUi(() => _mainWindow.TryGetPluginCanvasScreenPixels(out _));

        public bool TryScreenToCanvas(Point screenPixelPoint, out Point canvasPoint)
        {
            Point result = default;
            var success = RunOnUi(() => _mainWindow.TryConvertPluginScreenPixelsToCanvas(
                screenPixelPoint, out result));
            canvasPoint = result;
            return success;
        }

        public bool TryScreenToCanvas(Rect screenPixelRect, out Rect canvasRect)
        {
            Rect result = Rect.Empty;
            var success = RunOnUi(() => TryMapRect(screenPixelRect,
                _mainWindow.TryConvertPluginScreenPixelsToCanvas, out result));
            canvasRect = result;
            return success;
        }

        public bool TryCanvasToScreen(Point canvasPoint, out Point screenPixelPoint)
        {
            Point result = default;
            var success = RunOnUi(() => _mainWindow.TryConvertPluginCanvasToScreenPixels(
                canvasPoint, out result));
            screenPixelPoint = result;
            return success;
        }

        public bool TryCanvasToScreen(Rect canvasRect, out Rect screenPixelRect)
        {
            Rect result = Rect.Empty;
            var success = RunOnUi(() => TryMapRect(canvasRect,
                _mainWindow.TryConvertPluginCanvasToScreenPixels, out result));
            screenPixelRect = result;
            return success;
        }

        private delegate bool PointMapper(Point source, out Point target);

        private static bool TryMapRect(Rect source,
            PointMapper mapper, out Rect result)
        {
            result = Rect.Empty;
            if (source.IsEmpty || double.IsNaN(source.Width) || double.IsNaN(source.Height))
                return false;

            if (!mapper(new Point(source.Left, source.Top), out var topLeft)
                || !mapper(new Point(source.Right, source.Top), out var topRight)
                || !mapper(new Point(source.Left, source.Bottom), out var bottomLeft)
                || !mapper(new Point(source.Right, source.Bottom), out var bottomRight))
                return false;

            result = new Rect(topLeft, topRight);
            result.Union(bottomLeft);
            result.Union(bottomRight);
            return !result.IsEmpty;
        }
    }
}
