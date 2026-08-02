using System;
using System.Windows.Media.Imaging;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 剪贴板服务：允许插件读取/写入系统剪贴板文本与图像，并订阅剪贴板变化事件。
    /// <para>宿主已挂接系统剪贴板监听（AddClipboardFormatListener），
    /// <see cref="ClipboardUpdate"/> 在剪贴板文本/图像变化时触发。</para>
    /// <para>所有方法都应在 UI 线程调用（WPF Clipboard 依赖 STA 线程）。</para>
    /// </summary>
    public interface IClipboardService
    {
        /// <summary>系统剪贴板内容（文本/图像）变化时触发。</summary>
        event Action ClipboardUpdate;

        /// <summary>读取剪贴板文本；剪贴板不含文本时返回空字符串。</summary>
        string GetText();

        /// <summary>写入文本到剪贴板。返回是否成功。</summary>
        bool SetText(string text);

        /// <summary>读取剪贴板图像；剪贴板不含图像时返回 null。</summary>
        BitmapSource GetImage();

        /// <summary>把图像写入剪贴板。返回是否成功。</summary>
        bool SetImage(BitmapSource image);

        /// <summary>剪贴板当前是否包含图像。</summary>
        bool ContainsImage();
    }
}
