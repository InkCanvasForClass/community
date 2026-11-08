using System;
using System.Windows.Ink;
using System.Windows.Media.Imaging;

namespace Ink_Canvas.Models
{
    public class CapturedImage
    {
        public BitmapImage Image { get; }
        public BitmapImage PreviewImage { get; }
        public BitmapImage Thumbnail { get; }
        public StrokeCollection Strokes { get; }
        public string Timestamp { get; }
        public string FilePath { get; }

        /// <summary>
        /// 使用指定的位图创建一个 CapturedImage 实例，并为其生成缩略图、空白笔划集合和时间戳。
        /// </summary>
        /// <param name="image">用于初始化的位图；不能为空。传入的图像将在内部确保为冻结状态以便安全跨线程使用。</param>
        /// <exception cref="System.ArgumentNullException">当 <paramref name="image"/> 为 null 时抛出。</exception>
        public CapturedImage(BitmapImage image)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image), "图像不能为空");

            // 确保 Image 被冻结，避免跨线程访问风险
            Image = EnsureFrozen(image);
            PreviewImage = CreatePreviewImage(Image);
            Thumbnail = CreateThumbnail(Image);
            Strokes = new StrokeCollection();
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            FilePath = null;
        }

        /// <summary>
        /// 初始化 CapturedImage 实例：将指定图像冻结用于线程安全、生成缩略图并初始化空的笔迹集合，同时设置文件路径和时间戳（尝试从文件名提取时间戳，失败则使用当前时间）。
        /// </summary>
        /// <param name="image">源图像，不能为空。</param>
        /// <param name="filePath">关联文件的路径，可能为 null。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="image"/> 为 null 时抛出。</exception>
        public CapturedImage(BitmapImage image, string filePath)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image), "图像不能为空");

            // 确保 Image 被冻结，避免跨线程访问风险
            Image = EnsureFrozen(image);
            PreviewImage = CreatePreviewImage(Image);
            Thumbnail = CreateThumbnail(Image);
            Strokes = new StrokeCollection();
            FilePath = filePath;
            Timestamp = TryExtractTimestampFromFilePath(filePath) ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        }

        /// <summary>
        /// 尝试从给定文件路径的文件名中解析并返回规范化的时间戳。
        /// </summary>
        /// <param name="filePath">要从其文件名中解析时间戳的文件路径；可以为 null 或空字符串。</param>
        /// <returns>解析得到的时间戳，格式为 "yyyy-MM-dd HH:mm:ss.fff"；无法解析时返回 null。</returns>
        private static string TryExtractTimestampFromFilePath(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath)) return null;
                var name = System.IO.Path.GetFileNameWithoutExtension(filePath);
                if (DateTime.TryParseExact(
                        name,
                        "yyyy-MM-dd HH-mm-ss-fff",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None,
                        out var dt))
                {
                    return dt.ToString("yyyy-MM-dd HH:mm:ss.fff");
                }
                if (name.Length >= 23)
                {
                    var tail = name.Substring(name.Length - 23);
                    if (DateTime.TryParseExact(
                            tail,
                            "yyyy-MM-dd HH-mm-ss-fff",
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None,
                            out var dt2))
                    {
                        return dt2.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 确保并返回一个已冻结的 BitmapImage 副本，以便在跨线程场景中安全使用。
        /// </summary>
        /// <param name="image">要确保为冻结状态的源 BitmapImage。</param>
        /// <returns>与输入图像内容一致且已调用 Freeze 的 BitmapImage 实例。</returns>
        /// <exception cref="ArgumentNullException">在 <paramref name="image"/> 为 null 时抛出。</exception>
        private static BitmapImage EnsureFrozen(BitmapImage image)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));

            if (image.IsFrozen)
                return image;

            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));

            var stream = new System.IO.MemoryStream();
            encoder.Save(stream);
            stream.Position = 0;

            var frozenCopy = new BitmapImage();
            frozenCopy.BeginInit();
            frozenCopy.CacheOption = BitmapCacheOption.OnLoad;
            frozenCopy.StreamSource = stream;
            frozenCopy.EndInit();
            frozenCopy.Freeze();

            return frozenCopy;
        }

        private const double PreviewMaxSide = 1920.0;

        /// <summary>
        /// 生成并返回缩小查看时使用的降低分辨率预览图（最大边长限制在 1920 内）。
        /// 原图本身不超过 1920 时直接复用原图，避免重复内存开销。
        /// </summary>
        private static BitmapImage CreatePreviewImage(BitmapImage original)
        {
            if (original == null) return null;
            if (original.PixelWidth <= 0 || original.PixelHeight <= 0) return original;

            double maxSide = Math.Max(original.PixelWidth, original.PixelHeight);
            if (maxSide <= PreviewMaxSide)
            {
                return original;
            }

            try
            {
                double scale = PreviewMaxSide / maxSide;
                var scaled = new TransformedBitmap(original, new System.Windows.Media.ScaleTransform(scale, scale));
                var encoder = new JpegBitmapEncoder { QualityLevel = 85 };
                encoder.Frames.Add(BitmapFrame.Create(scaled));

                using (var stream = new System.IO.MemoryStream())
                {
                    encoder.Save(stream);
                    stream.Seek(0, System.IO.SeekOrigin.Begin);

                    var result = new BitmapImage();
                    result.BeginInit();
                    result.CacheOption = BitmapCacheOption.OnLoad;
                    result.StreamSource = stream;
                    result.EndInit();
                    result.Freeze();
                    return result;
                }
            }
            catch
            {
                return original;
            }
        }

        private static BitmapImage CreateThumbnail(BitmapImage original)
        {
            if (original == null)
                throw new ArgumentNullException(nameof(original));

            if (original.PixelWidth <= 0 || original.PixelHeight <= 0)
            {
                throw new ArgumentException(
                    $"图像尺寸无效：宽度={original.PixelWidth}, 高度={original.PixelHeight}。图像必须具有有效的像素尺寸。",
                    nameof(original));
            }

            double targetWidth = 290.0;
            double targetHeight = 180.0;
            double scale = Math.Min(targetWidth / original.PixelWidth, targetHeight / original.PixelHeight);

            if (double.IsInfinity(scale) || double.IsNaN(scale) || scale <= 0)
            {
                throw new InvalidOperationException(
                    $"无法计算有效的缩放比例：scale={scale}, 图像尺寸={original.PixelWidth}x{original.PixelHeight}");
            }

            var thumbnail = new TransformedBitmap(original, new System.Windows.Media.ScaleTransform(scale, scale));

            var bmp = new JpegBitmapEncoder { QualityLevel = 85 };
            bmp.Frames.Add(BitmapFrame.Create(thumbnail));

            using (var stream = new System.IO.MemoryStream())
            {
                bmp.Save(stream);
                stream.Seek(0, System.IO.SeekOrigin.Begin);

                var result = new BitmapImage();
                result.BeginInit();
                result.CacheOption = BitmapCacheOption.OnLoad;
                result.StreamSource = stream;
                result.EndInit();
                result.Freeze();

                return result;
            }
        }
    }
}
