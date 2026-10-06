using System.Windows;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 屏幕像素与 ICC-CE 画布 DIP 坐标之间的转换服务。
    /// </summary>
    public interface ICanvasCoordinateService
    {
        /// <summary>当前画布在虚拟屏幕中的物理像素矩形；画布不可用时为空矩形。</summary>
        Rect CanvasScreenBounds { get; }

        /// <summary>当前画布 DIP 尺寸。</summary>
        Size CanvasSize { get; }

        /// <summary>当前是否能取得画布和 DPI 转换上下文。</summary>
        bool IsAvailable { get; }

        /// <summary>把屏幕物理像素点转换为画布 DIP 点。</summary>
        bool TryScreenToCanvas(Point screenPixelPoint, out Point canvasPoint);

        /// <summary>把屏幕物理像素矩形转换为画布 DIP 矩形。</summary>
        bool TryScreenToCanvas(Rect screenPixelRect, out Rect canvasRect);

        /// <summary>把画布 DIP 点转换为屏幕物理像素点。</summary>
        bool TryCanvasToScreen(Point canvasPoint, out Point screenPixelPoint);

        /// <summary>把画布 DIP 矩形转换为屏幕物理像素矩形。</summary>
        bool TryCanvasToScreen(Rect canvasRect, out Rect screenPixelRect);
    }
}
