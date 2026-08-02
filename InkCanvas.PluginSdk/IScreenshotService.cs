using System.Windows.Media.Imaging;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 截图服务：允许插件捕获全屏或指定区域的屏幕内容，返回位图或保存为文件。
    /// <para>所有方法都应在 UI 线程调用（宿主内部不做线程切换）。</para>
    /// </summary>
    public interface IScreenshotService
    {
        /// <summary>
        /// 捕获整个虚拟屏幕（所有显示器）为位图。
        /// </summary>
        /// <returns>已 Freeze 的 <see cref="BitmapSource"/>，可直接用于 WPF 绑定/绘制。</returns>
        BitmapSource CaptureFullScreen();

        /// <summary>
        /// 捕获屏幕指定区域为位图。
        /// </summary>
        /// <param name="x">区域左上角 X（屏幕坐标）。</param>
        /// <param name="y">区域左上角 Y（屏幕坐标）。</param>
        /// <param name="width">区域宽度。</param>
        /// <param name="height">区域高度。</param>
        /// <returns>已 Freeze 的 <see cref="BitmapSource"/>。</returns>
        BitmapSource CaptureScreenArea(int x, int y, int width, int height);

        /// <summary>
        /// 捕获整个虚拟屏幕并保存为 PNG 文件。
        /// </summary>
        /// <param name="filePath">输出 PNG 路径（目录需已存在）。</param>
        /// <returns>是否保存成功。</returns>
        bool SaveFullScreenToFile(string filePath);
    }
}
