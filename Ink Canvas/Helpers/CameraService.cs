using AForge.Video;
using AForge.Video.DirectShow;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Ink_Canvas.Helpers
{
    public class CameraService : IDisposable
    {
        private VideoCaptureDevice _videoSource;
        private bool _isCapturing;
        private Bitmap _currentFrame;
        private readonly object _frameLock = new object();
        private Dispatcher _dispatcher;

        // 新增属性
        private int _rotationAngle = 0; // 0=0度，1=90度，2=180度，3=270度
        private int _resolutionWidth = 640;
        private int _resolutionHeight = 480;

        public event EventHandler<Bitmap> FrameReceived;
        public event EventHandler<string> ErrorOccurred;

        public bool IsCapturing => _isCapturing;
        public List<FilterInfo> AvailableCameras { get; private set; }
        public FilterInfo CurrentCamera { get; private set; }

        // 新增属性
        public int RotationAngle
        {
            get => _rotationAngle;
            set => _rotationAngle = Math.Max(0, Math.Min(3, value));
        }

        public int ResolutionWidth
        {
            get => _resolutionWidth;
            set => _resolutionWidth = Math.Max(320, Math.Min(1920, value));
        }

        public int ResolutionHeight
        {
            get => _resolutionHeight;
            set => _resolutionHeight = Math.Max(240, Math.Min(1080, value));
        }

        public CameraService()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            AvailableCameras = new List<FilterInfo>();
            RefreshCameraList();
        }

        public CameraService(int rotationAngle, int resolutionWidth, int resolutionHeight)
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            AvailableCameras = new List<FilterInfo>();
            _rotationAngle = rotationAngle;
            _resolutionWidth = resolutionWidth;
            _resolutionHeight = resolutionHeight;
            RefreshCameraList();
        }

        /// <summary>
        /// 刷新可用摄像头列表
        /// </summary>
        public void RefreshCameraList()
        {
            try
            {
                AvailableCameras.Clear();
                var videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);

                foreach (FilterInfo device in videoDevices)
                {
                    AvailableCameras.Add(device);
                }

            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"刷新摄像头列表失败: {ex.Message}", LogHelper.LogType.Error);
                ErrorOccurred?.Invoke(this, $"刷新摄像头列表失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 开始摄像头预览
        /// </summary>
        /// <param name="cameraIndex">摄像头索引</param>
        public bool StartPreview(int cameraIndex = 0)
        {
            try
            {
                if (AvailableCameras.Count == 0)
                {
                    RefreshCameraList();
                    if (AvailableCameras.Count == 0)
                    {
                        ErrorOccurred?.Invoke(this, "未找到可用的摄像头设备");
                        return false;
                    }
                }

                if (cameraIndex < 0 || cameraIndex >= AvailableCameras.Count)
                {
                    ErrorOccurred?.Invoke(this, "摄像头索引超出范围");
                    return false;
                }

                // 停止当前预览
                StopPreview();

                CurrentCamera = AvailableCameras[cameraIndex];
                _videoSource = new VideoCaptureDevice(CurrentCamera.MonikerString);

                // 设置视频源事件处理
                _videoSource.NewFrame += VideoSource_NewFrame;

                // 启动视频源
                _videoSource.Start();

                _isCapturing = true;
                LogHelper.WriteLogToFile($"开始摄像头预览: {CurrentCamera.Name}");
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"启动摄像头预览失败: {ex.Message}", LogHelper.LogType.Error);
                ErrorOccurred?.Invoke(this, $"启动摄像头预览失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 停止摄像头预览
        /// </summary>
        public void StopPreview()
        {
            try
            {
                if (_videoSource != null && _videoSource.IsRunning)
                {
                    _videoSource.SignalToStop();
                    _videoSource.WaitForStop();
                    _videoSource.NewFrame -= VideoSource_NewFrame;
                    _videoSource = null;
                }

                _isCapturing = false;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"停止摄像头预览失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 切换到指定摄像头
        /// </summary>
        /// <param name="cameraIndex">摄像头索引</param>
        public bool SwitchCamera(int cameraIndex)
        {
            try
            {
                if (cameraIndex < 0 || cameraIndex >= AvailableCameras.Count)
                {
                    ErrorOccurred?.Invoke(this, "摄像头索引超出范围");
                    return false;
                }

                return StartPreview(cameraIndex);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"切换摄像头失败: {ex.Message}", LogHelper.LogType.Error);
                ErrorOccurred?.Invoke(this, $"切换摄像头失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取当前帧的BitmapSource（WPF格式），直接返回可用的WPF位图
        /// </summary>
        public BitmapSource GetCurrentFrameAsBitmapSource()
        {
            lock (_frameLock)
            {
                if (_currentFrame == null)
                    return null;

                try
                {
                    // 验证位图有效性
                    if (_currentFrame.Width <= 0 || _currentFrame.Height <= 0)
                        return null;

                    // 使用更安全的方法转换位图
                    var bitmapData = _currentFrame.LockBits(
                        new Rectangle(0, 0, _currentFrame.Width, _currentFrame.Height),
                        ImageLockMode.ReadOnly,
                        _currentFrame.PixelFormat);

                    try
                    {
                        // 根据像素格式选择合适的WPF像素格式
                        System.Windows.Media.PixelFormat wpfPixelFormat;
                        switch (_currentFrame.PixelFormat)
                        {
                            case PixelFormat.Format24bppRgb:
                                wpfPixelFormat = System.Windows.Media.PixelFormats.Bgr24;
                                break;
                            case PixelFormat.Format32bppArgb:
                                wpfPixelFormat = System.Windows.Media.PixelFormats.Bgra32;
                                break;
                            case PixelFormat.Format32bppRgb:
                                wpfPixelFormat = System.Windows.Media.PixelFormats.Bgr32;
                                break;
                            default:
                                wpfPixelFormat = System.Windows.Media.PixelFormats.Bgr24;
                                break;
                        }

                        var bitmapSource = BitmapSource.Create(
                            bitmapData.Width,
                            bitmapData.Height,
                            _currentFrame.HorizontalResolution,
                            _currentFrame.VerticalResolution,
                            wpfPixelFormat,
                            null,
                            bitmapData.Scan0,
                            bitmapData.Stride * bitmapData.Height,
                            bitmapData.Stride);

                        bitmapSource.Freeze();
                        return bitmapSource;
                    }
                    finally
                    {
                        _currentFrame.UnlockBits(bitmapData);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"转换帧为BitmapSource失败: {ex.Message}", LogHelper.LogType.Error);
                    return null;
                }
            }
        }


        /// <summary>
        /// 视频源新帧事件处理
        /// </summary>
        private void VideoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            try
            {
                lock (_frameLock)
                {
                    // 释放之前的帧
                    _currentFrame?.Dispose();

                    // 创建新的位图，避免Clone的问题
                    var sourceFrame = eventArgs.Frame;

                    if (sourceFrame != null)
                    {
                        try
                        {
                            var width = sourceFrame.Width;
                            var height = sourceFrame.Height;

                            if (width > 0 && height > 0)
                            {
                                // 应用旋转
                                Bitmap rotatedFrame = ApplyRotation(sourceFrame);

                                // 应用分辨率调整
                                _currentFrame = ResizeImage(rotatedFrame, _resolutionWidth, _resolutionHeight);

                                rotatedFrame?.Dispose();
                            }
                            else
                            {
                                _currentFrame = null;
                            }
                        }
                        catch (Exception frameEx)
                        {
                            LogHelper.WriteLogToFile($"处理源帧失败: {frameEx.Message}", LogHelper.LogType.Error);
                            _currentFrame = null;
                        }
                    }
                    else
                    {
                        _currentFrame = null;
                    }
                }

                // 在UI线程中触发事件
                _dispatcher.BeginInvoke(new Action(() =>
                {
                    FrameReceived?.Invoke(this, _currentFrame);
                }));
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理新帧失败: {ex.Message}", LogHelper.LogType.Error);
                ErrorOccurred?.Invoke(this, $"处理新帧失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取摄像头名称列表
        /// </summary>
        public List<string> GetCameraNames()
        {
            return AvailableCameras.Select(camera => camera.Name).ToList();
        }

        /// <summary>
        /// 检查是否有可用摄像头
        /// </summary>
        public bool HasAvailableCameras()
        {
            if (AvailableCameras.Count == 0)
            {
                RefreshCameraList();
            }
            return AvailableCameras.Count > 0;
        }

        /// <summary>
        /// 应用旋转到图像
        /// </summary>
        private Bitmap ApplyRotation(Bitmap source)
        {
            if (_rotationAngle == 0)
                return new Bitmap(source);

            var rotationType = RotateFlipType.RotateNoneFlipNone;
            switch (_rotationAngle)
            {
                case 1: rotationType = RotateFlipType.Rotate90FlipNone; break;
                case 2: rotationType = RotateFlipType.Rotate180FlipNone; break;
                case 3: rotationType = RotateFlipType.Rotate270FlipNone; break;
            }

            var rotated = new Bitmap(source);
            rotated.RotateFlip(rotationType);
            return rotated;
        }

        /// <summary>
        /// 调整图像大小
        /// </summary>
        private Bitmap ResizeImage(Bitmap source, int width, int height)
        {
            if (source.Width == width && source.Height == height)
                return new Bitmap(source);

            var resized = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            using (var graphics = Graphics.FromImage(resized))
            {
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                graphics.DrawImage(source, 0, 0, width, height);
            }
            return resized;
        }

        public void Dispose()
        {
            StopPreview();

            lock (_frameLock)
            {
                _currentFrame?.Dispose();
            }
        }
    }
}