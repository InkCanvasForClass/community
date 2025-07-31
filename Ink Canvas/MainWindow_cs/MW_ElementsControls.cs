using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Ink_Canvas.Helpers;
using Microsoft.Win32;

namespace Ink_Canvas
{
    public partial class MainWindow : Window
    {
        #region Image
        private async void BtnImageInsert_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Image files (*.jpg; *.jpeg; *.png; *.bmp)|*.jpg;*.jpeg;*.png;*.bmp";

            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;

                Image image = await CreateAndCompressImageAsync(filePath);

                if (image != null)
                {
                    string timestamp = "img_" + DateTime.Now.ToString("yyyyMMdd_HH_mm_ss_fff");
                    image.Name = timestamp;

                    CenterAndScaleElement(image);
                    inkCanvas.Children.Add(image);

                    // 添加鼠标事件处理，使图片可以被选择
                    image.MouseDown += UIElement_MouseDown;
                    image.IsManipulationEnabled = true;

                    timeMachine.CommitElementInsertHistory(image);
                }
            }
        }

        private async Task<Image> CreateAndCompressImageAsync(string filePath)
        {
            string savePath = Path.Combine(Settings.Automation.AutoSavedStrokesLocation, "File Dependency");
            if (!Directory.Exists(savePath))
            {
                Directory.CreateDirectory(savePath);
            }

            string fileExtension = Path.GetExtension(filePath);
            string timestamp = "img_" + DateTime.Now.ToString("yyyyMMdd_HH_mm_ss_fff");
            string newFilePath = Path.Combine(savePath, timestamp + fileExtension);

            await Task.Run(() => File.Copy(filePath, newFilePath, true));

            return await Dispatcher.InvokeAsync(() =>
            {
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.UriSource = new Uri(newFilePath);
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();

                int width = bitmapImage.PixelWidth;
                int height = bitmapImage.PixelHeight;

                Image image = new Image();
                // 设置拉伸模式为Fill，支持任意比例缩放
                image.Stretch = Stretch.Fill;

                if (isLoaded && Settings.Canvas.IsCompressPicturesUploaded && (width > 1920 || height > 1080))
                {
                    double scaleX = 1920.0 / width;
                    double scaleY = 1080.0 / height;
                    double scale = Math.Min(scaleX, scaleY);

                    TransformedBitmap transformedBitmap = new TransformedBitmap(bitmapImage, new ScaleTransform(scale, scale));

                    image.Source = transformedBitmap;
                    image.Width = transformedBitmap.PixelWidth;
                    image.Height = transformedBitmap.PixelHeight;
                }
                else
                {
                    image.Source = bitmapImage;
                    image.Width = width;
                    image.Height = height;
                }

                return image;
            });
        }
        #endregion

        #region Media
        private async void BtnMediaInsert_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Media files (*.mp4; *.avi; *.wmv)|*.mp4;*.avi;*.wmv";

            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;

                byte[] mediaBytes = await Task.Run(() => File.ReadAllBytes(filePath));

                MediaElement mediaElement = await CreateMediaElementAsync(filePath);

                if (mediaElement != null)
                {
                    CenterAndScaleElement(mediaElement);

                    InkCanvas.SetLeft(mediaElement, 0);
                    InkCanvas.SetTop(mediaElement, 0);
                    inkCanvas.Children.Add(mediaElement);

                    // 添加鼠标事件处理，使媒体元素可以被选择
                    mediaElement.MouseDown += UIElement_MouseDown;
                    mediaElement.IsManipulationEnabled = true;

                    mediaElement.LoadedBehavior = MediaState.Manual;
                    mediaElement.UnloadedBehavior = MediaState.Manual;
                    mediaElement.Loaded += async (_, args) =>
                    {
                        mediaElement.Play();
                        await Task.Delay(100);
                        mediaElement.Pause();
                    };

                    timeMachine.CommitElementInsertHistory(mediaElement);
                }
            }
        }

        private async Task<MediaElement> CreateMediaElementAsync(string filePath)
        {
            string savePath = Path.Combine(Settings.Automation.AutoSavedStrokesLocation, "File Dependency");
            if (!Directory.Exists(savePath))
            {
                Directory.CreateDirectory(savePath);
            }
            return await Dispatcher.InvokeAsync(() =>
            {
                MediaElement mediaElement = new MediaElement();
                mediaElement.Source = new Uri(filePath);
                string timestamp = "media_" + DateTime.Now.ToString("yyyyMMdd_HH_mm_ss_fff");
                mediaElement.Name = timestamp;
                mediaElement.LoadedBehavior = MediaState.Manual;
                mediaElement.UnloadedBehavior = MediaState.Manual;

                mediaElement.Width = 256;
                mediaElement.Height = 256;

                string fileExtension = Path.GetExtension(filePath);
                string newFilePath = Path.Combine(savePath, mediaElement.Name + fileExtension);

                File.Copy(filePath, newFilePath, true);

                mediaElement.Source = new Uri(newFilePath);

                return mediaElement;
            });
        }
        #endregion

        #region Image Operations

        /// <summary>
        /// 旋转图片
        /// </summary>
        /// <param name="image">要旋转的图片</param>
        /// <param name="angle">旋转角度（正数为顺时针，负数为逆时针）</param>
        private void RotateImage(Image image, double angle)
        {
            if (image == null) return;

            try
            {
                // 获取当前的变换
                var transformGroup = image.RenderTransform as TransformGroup ?? new TransformGroup();

                // 查找现有的旋转变换
                RotateTransform rotateTransform = null;
                foreach (Transform transform in transformGroup.Children)
                {
                    if (transform is RotateTransform rt)
                    {
                        rotateTransform = rt;
                        break;
                    }
                }

                // 如果没有旋转变换，创建一个新的
                if (rotateTransform == null)
                {
                    rotateTransform = new RotateTransform();
                    transformGroup.Children.Add(rotateTransform);
                }

                // 设置旋转中心为图片中心
                rotateTransform.CenterX = image.ActualWidth / 2;
                rotateTransform.CenterY = image.ActualHeight / 2;

                // 累加旋转角度
                rotateTransform.Angle = (rotateTransform.Angle + angle) % 360;

                // 应用变换
                image.RenderTransform = transformGroup;

                // 提交到时间机器以支持撤销
                // 注意：旋转操作目前不支持撤销，因为需要更复杂的历史记录机制
            }
            catch (Exception ex)
            {
                // 记录错误但不中断程序
                System.Diagnostics.Debug.WriteLine($"旋转图片时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 克隆图片
        /// </summary>
        /// <param name="image">要克隆的图片</param>
        private void CloneImage(Image image)
        {
            if (image == null) return;

            try
            {
                // 创建图片的副本
                var clonedImage = new Image
                {
                    Source = image.Source,
                    Width = image.Width,
                    Height = image.Height,
                    Stretch = image.Stretch,
                    RenderTransform = image.RenderTransform?.Clone() as Transform
                };

                // 设置位置，稍微偏移以避免重叠
                InkCanvas.SetLeft(clonedImage, InkCanvas.GetLeft(image) + 20);
                InkCanvas.SetTop(clonedImage, InkCanvas.GetTop(image) + 20);

                // 添加鼠标事件处理，使图片可以被选择
                clonedImage.MouseDown += UIElement_MouseDown;
                clonedImage.IsManipulationEnabled = true;

                // 添加到画布
                inkCanvas.Children.Add(clonedImage);

                // 选择新克隆的图片
                DeselectUIElement();
                SelectUIElement(clonedImage);

                // 提交到时间机器以支持撤销
                timeMachine.CommitElementInsertHistory(clonedImage);
            }
            catch (Exception ex)
            {
                // 记录错误但不中断程序
                System.Diagnostics.Debug.WriteLine($"克隆图片时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 克隆图片到新页面
        /// </summary>
        /// <param name="image">要克隆的图片</param>
        private void CloneImageToNewBoard(Image image)
        {
            if (image == null) return;

            try
            {
                // 创建图片的副本
                var clonedImage = new Image
                {
                    Source = image.Source,
                    Width = image.Width,
                    Height = image.Height,
                    Stretch = image.Stretch,
                    RenderTransform = image.RenderTransform?.Clone() as Transform
                };

                // 设置位置，稍微偏移以避免重叠
                InkCanvas.SetLeft(clonedImage, InkCanvas.GetLeft(image) + 20);
                InkCanvas.SetTop(clonedImage, InkCanvas.GetTop(image) + 20);

                // 添加鼠标事件处理，使图片可以被选择
                clonedImage.MouseDown += UIElement_MouseDown;
                clonedImage.IsManipulationEnabled = true;

                // 创建新页面
                BtnWhiteBoardAdd_Click(null, null);

                // 添加到新页面的画布
                inkCanvas.Children.Add(clonedImage);

                // 选择新克隆的图片
                DeselectUIElement();
                SelectUIElement(clonedImage);

                // 提交到时间机器以支持撤销
                timeMachine.CommitElementInsertHistory(clonedImage);
            }
            catch (Exception ex)
            {
                // 记录错误但不中断程序
                System.Diagnostics.Debug.WriteLine($"克隆图片到新页面时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 缩放图片
        /// </summary>
        /// <param name="image">要缩放的图片</param>
        /// <param name="scaleFactor">缩放因子（大于1为放大，小于1为缩小）</param>
        private void ScaleImage(Image image, double scaleFactor)
        {
            if (image == null) return;

            try
            {
                // 获取当前的变换
                var transformGroup = image.RenderTransform as TransformGroup ?? new TransformGroup();

                // 查找现有的缩放变换
                ScaleTransform scaleTransform = null;
                foreach (Transform transform in transformGroup.Children)
                {
                    if (transform is ScaleTransform st)
                    {
                        scaleTransform = st;
                        break;
                    }
                }

                // 如果没有缩放变换，创建一个新的
                if (scaleTransform == null)
                {
                    scaleTransform = new ScaleTransform();
                    transformGroup.Children.Add(scaleTransform);
                }

                // 设置缩放中心为图片中心
                scaleTransform.CenterX = image.ActualWidth / 2;
                scaleTransform.CenterY = image.ActualHeight / 2;

                // 应用缩放因子
                scaleTransform.ScaleX *= scaleFactor;
                scaleTransform.ScaleY *= scaleFactor;

                // 应用变换
                image.RenderTransform = transformGroup;

                // 提交到时间机器以支持撤销
                // 注意：缩放操作目前不支持撤销，因为需要更复杂的历史记录机制
            }
            catch (Exception ex)
            {
                // 记录错误但不中断程序
                System.Diagnostics.Debug.WriteLine($"缩放图片时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 删除图片
        /// </summary>
        /// <param name="image">要删除的图片</param>
        private void DeleteImage(Image image)
        {
            if (image == null) return;

            try
            {
                // 从画布中移除图片
                if (inkCanvas.Children.Contains(image))
                {
                    inkCanvas.Children.Remove(image);

                    // 取消选择
                    DeselectUIElement();

                    // 提交到时间机器以支持撤销
                    timeMachine.CommitElementRemoveHistory(image);
                }
            }
            catch (Exception ex)
            {
                // 记录错误但不中断程序
                System.Diagnostics.Debug.WriteLine($"删除图片时发生错误: {ex.Message}");
            }
        }

        #endregion

        private void CenterAndScaleElement(FrameworkElement element)
        {
            double maxWidth = SystemParameters.PrimaryScreenWidth / 2;
            double maxHeight = SystemParameters.PrimaryScreenHeight / 2;

            double scaleX = maxWidth / element.Width;
            double scaleY = maxHeight / element.Height;
            double scale = Math.Min(scaleX, scaleY);

            // 直接设置元素的大小，而不使用RenderTransform
            double newWidth = element.Width * scale;
            double newHeight = element.Height * scale;

            element.Width = newWidth;
            element.Height = newHeight;

            // 计算居中位置
            double canvasWidth = inkCanvas.ActualWidth;
            double canvasHeight = inkCanvas.ActualHeight;
            double centerX = (canvasWidth - newWidth) / 2;
            double centerY = (canvasHeight - newHeight) / 2;

            // 直接设置位置，而不使用RenderTransform
            InkCanvas.SetLeft(element, centerX);
            InkCanvas.SetTop(element, centerY);

            // 清除任何现有的RenderTransform
            element.RenderTransform = Transform.Identity;
        }
    }
}
