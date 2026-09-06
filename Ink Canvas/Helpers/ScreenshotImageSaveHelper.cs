using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Ink_Canvas.Windows.SettingsViews.Helpers;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 截图落盘统一入口：按设置选择格式（PNG/JPG）、JPG 质量、缩放档位（像素倍率 <=1）。
    /// 手动截图（存桌面）不走本类，保持原 PNG 原尺寸行为。
    /// </summary>
    internal static class ScreenshotImageSaveHelper
    {
        /// <summary>格式枚举值：0 = PNG（无损），1 = JPG（有损）。</summary>
        public const int FormatPng = 0;
        public const int FormatJpeg = 1;

        /// <summary>缩放档位：0=100% 1=75% 2=50% 3=25%。</summary>
        private static readonly double[] ScaleSteps = { 1.0, 0.75, 0.5, 0.25 };

        /// <summary>
        /// 根据当前设置返回保存文件应使用的扩展名（小写，含点）。
        /// </summary>
        internal static string GetExtension()
        {
            return GetExtension(SettingsManager.Settings.Automation.ScreenshotSaveFormat);
        }

        internal static string GetExtension(int format)
        {
            return format == FormatJpeg ? ".jpg" : ".png";
        }

        /// <summary>
        /// 按设置把位图保存到 path（扩展名由调用方用 <see cref="GetExtension"/> 生成）。
        /// JPG 质量低于 100 时走指定编码器，其余直接 ImageFormat 保存。
        /// 调用方负责 Dispose bitmap。
        /// </summary>
        internal static void Save(Bitmap bitmap, string path)
        {
            if (bitmap == null) throw new ArgumentNullException(nameof(bitmap));

            var settings = SettingsManager.Settings.Automation;
            var scaled = ScaleImage(bitmap, settings.ScreenshotScaleMode);
            try
            {
                if (settings.ScreenshotSaveFormat == FormatJpeg)
                {
                    SaveJpeg(scaled, path, settings.ScreenshotJpegQuality);
                }
                else
                {
                    scaled.Save(path, ImageFormat.Png);
                }
            }
            finally
            {
                if (!ReferenceEquals(scaled, bitmap)) scaled.Dispose();
            }
        }

        private static void SaveJpeg(Bitmap bitmap, string path, long quality)
        {
            // 质量钳制到 [1, 100]；100 交给编码器等价无损参数，也走编码器保证一致性
            quality = Math.Max(1, Math.Min(100, quality));
            var jpegEncoder = GetJpegEncoder();
            if (jpegEncoder == null)
            {
                // 系统找不到 JPEG 编码器（几乎不可能），退回默认参数
                bitmap.Save(path, ImageFormat.Jpeg);
                return;
            }

            using (var encoderParams = new EncoderParameters(1))
            using (var qualityParam = new EncoderParameter(Encoder.Quality, quality))
            {
                encoderParams.Param[0] = qualityParam;
                bitmap.Save(path, jpegEncoder, encoderParams);
            }
        }

        private static ImageCodecInfo GetJpegEncoder()
        {
            return ImageCodecInfo.GetImageEncoders()
                .FirstOrDefault(codec => codec.FormatID == ImageFormat.Jpeg.Guid);
        }

        /// <summary>
        /// 按缩放档位缩小图像；1.0 档或目标尺寸 <=1px 时原样返回（不复制）。
        /// 使用高质量双三次插值，符合既有截图渲染的质量设置。
        /// </summary>
        private static Bitmap ScaleImage(Bitmap source, int scaleMode)
        {
            var factor = scaleMode >= 0 && scaleMode < ScaleSteps.Length
                ? ScaleSteps[scaleMode]
                : 1.0;
            if (factor >= 1.0) return source;

            int width = Math.Max(1, (int)Math.Round(source.Width * factor));
            int height = Math.Max(1, (int)Math.Round(source.Height * factor));
            if (width == source.Width && height == source.Height) return source;

            var dest = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(dest))
            {
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.CompositingMode = CompositingMode.SourceOver;
                graphics.DrawImage(source, 0, 0, width, height);
            }
            return dest;
        }
    }
}
