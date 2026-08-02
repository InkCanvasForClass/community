using System;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// <see cref="IClipboardService"/> 的宿主实现：包装宿主已挂接的系统剪贴板监听
    /// （<see cref="MainWindow.ClipboardNotification.ClipboardUpdate"/>）与 WPF Clipboard。
    /// </summary>
    internal sealed class ClipboardService : IClipboardService
    {
        public ClipboardService()
        {
        }

        public event Action ClipboardUpdate
        {
            add => Ink_Canvas.ClipboardNotification.ClipboardUpdate += value;
            remove => Ink_Canvas.ClipboardNotification.ClipboardUpdate -= value;
        }

        public string GetText()
        {
            try
            {
                return Clipboard.ContainsText() ? Clipboard.GetText() : "";
            }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"ClipboardService.GetText failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                return "";
            }
        }

        public bool SetText(string text)
        {
            try
            {
                Clipboard.SetText(text ?? "");
                return true;
            }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"ClipboardService.SetText failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                return false;
            }
        }

        public BitmapSource GetImage()
        {
            try
            {
                return Clipboard.ContainsImage() ? Clipboard.GetImage() : null;
            }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"ClipboardService.GetImage failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                return null;
            }
        }

        public bool SetImage(BitmapSource image)
        {
            try
            {
                if (image == null) return false;
                Clipboard.SetImage(image);
                return true;
            }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"ClipboardService.SetImage failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                return false;
            }
        }

        public bool ContainsImage()
        {
            try
            {
                return Clipboard.ContainsImage();
            }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"ClipboardService.ContainsImage failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                return false;
            }
        }
    }
}
