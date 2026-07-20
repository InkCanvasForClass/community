using AForge.Video;
using AForge.Video.DirectShow;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 基于 AForge.NET DirectShow 的摄像头服务实现，兼容 Win7 SP1+。
    /// </summary>
    public class CameraService : ICameraService
    {
        private VideoCaptureDevice _videoSource;
        private bool _isCapturing;
        private Bitmap _currentFrame;
        private readonly object _frameLock = new object();
        private Dispatcher _dispatcher;

        private int _rotationAngle = 0;
        private int _resolutionWidth = 640;
        private int _resolutionHeight = 480;

        private readonly List<ResolutionInfo> _nativeResolutions = new List<ResolutionInfo>();
        private int _selectedResolutionIndex = -1;

        public event EventHandler<FrameEventArgs> FrameReceived;
        public event EventHandler<string> ErrorOccurred;

        public bool IsCapturing => _isCapturing;

        public IReadOnlyList<CameraInfo> AvailableCameras { get; private set; }
            = new List<CameraInfo>();

        public CameraInfo CurrentCamera { get; private set; }

        public int RotationAngle
        {
            get => _rotationAngle;
            set => _rotationAngle = Math.Max(0, Math.Min(3, value));
        }

        public int ResolutionWidth
        {
            get => _resolutionWidth;
            set => _resolutionWidth = Math.Max(320, Math.Min(3840, value));
        }

        public int ResolutionHeight
        {
            get => _resolutionHeight;
            set => _resolutionHeight = Math.Max(240, Math.Min(2160, value));
        }

        public IReadOnlyList<ResolutionInfo> NativeResolutions => _nativeResolutions;

        public int SelectedResolutionIndex
        {
            get => _selectedResolutionIndex;
            set
            {
                if (value == _selectedResolutionIndex) return;
                if (value < -1 || value >= _nativeResolutions.Count)
                    throw new ArgumentOutOfRangeException(nameof(value));

                _selectedResolutionIndex = value;
                if (value >= 0)
                {
                    var r = _nativeResolutions[value];
                    _resolutionWidth = r.Width;
                    _resolutionHeight = r.Height;

                    // 把分辨率同步到底层 VideoCaptureDevice（需要重启预览才生效）
                    try
                    {
                        ApplyVideoResolutionToVideoSource();
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"切换 AForge native 分辨率失败: {ex.Message}", LogHelper.LogType.Error);
                    }
                }
            }
        }

        public CameraService()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            RefreshCameraListAsync().GetAwaiter().GetResult();
        }

        public CameraService(int rotationAngle, int resolutionWidth, int resolutionHeight)
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            _rotationAngle = rotationAngle;
            _resolutionWidth = resolutionWidth;
            _resolutionHeight = resolutionHeight;
            RefreshCameraListAsync().GetAwaiter().GetResult();
        }

        /// <summary>刷新可用摄像头列表（AForge 同步完成）。</summary>
        public Task RefreshCameraListAsync()
        {
            try
            {
                var list = new List<CameraInfo>();
                var videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);

                foreach (FilterInfo device in videoDevices)
                {
                    list.Add(new CameraInfo
                    {
                        Name = device.Name,
                        MonikerString = device.MonikerString
                    });
                }

                AvailableCameras = list;
                LogHelper.WriteLogToFile(
                    $"[AForge] RefreshCameraList 完成，共 {list.Count} 个摄像头",
                    LogHelper.LogType.Info);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[AForge] 刷新摄像头列表失败: {ex.Message}", LogHelper.LogType.Error);
                ErrorOccurred?.Invoke(this, $"刷新摄像头列表失败: {ex.Message}");
            }
            return Task.CompletedTask;
        }

        /// <summary>启动摄像头预览（AForge 同步实现，返回 Task.FromResult）。</summary>
        public Task<bool> StartPreviewAsync(int cameraIndex = 0)
        {
            return Task.FromResult(StartPreviewCore(cameraIndex));
        }

        private bool StartPreviewCore(int cameraIndex)
        {
            try
            {
                var cameras = AvailableCameras.ToList();
                if (cameras.Count == 0)
                {
                    RefreshCameraListAsync().GetAwaiter().GetResult();
                    cameras = AvailableCameras.ToList();
                    if (cameras.Count == 0)
                    {
                        ErrorOccurred?.Invoke(this, "未找到可用的摄像头设备");
                        return false;
                    }
                }

                if (cameraIndex < 0 || cameraIndex >= cameras.Count)
                {
                    ErrorOccurred?.Invoke(this, "摄像头索引超出范围");
                    return false;
                }

                StopPreview();

                CurrentCamera = cameras[cameraIndex];
                _videoSource = new VideoCaptureDevice(CurrentCamera.MonikerString);

                // 枚举 native 分辨率并应用当前选择
                RefreshNativeResolutions();
                ApplyVideoResolutionToVideoSource();

                _videoSource.NewFrame += VideoSource_NewFrame;
                _videoSource.Start();

                _isCapturing = true;
                LogHelper.WriteLogToFile(
                    $"[AForge] 开始摄像头预览: {CurrentCamera.Name}，native 分辨率数: {_nativeResolutions.Count}，选中索引: {_selectedResolutionIndex}",
                    LogHelper.LogType.Info);
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"启动摄像头预览失败: {ex.Message}", LogHelper.LogType.Error);
                ErrorOccurred?.Invoke(this, $"启动摄像头预览失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>从 VideoCaptureDevice.VideoCapabilities 读取 native 分辨率列表。</summary>
        private void RefreshNativeResolutions()
        {
            _nativeResolutions.Clear();
            _selectedResolutionIndex = -1;

            if (_videoSource == null) return;

            try
            {
                var caps = _videoSource.VideoCapabilities;
                if (caps == null || caps.Length == 0) return;

                int bestMatch = -1;
                int bestMatchDiff = int.MaxValue;

                for (int i = 0; i < caps.Length; i++)
                {
                    var cap = caps[i];
                    var info = new ResolutionInfo
                    {
                        Width = cap.FrameSize.Width,
                        Height = cap.FrameSize.Height,
                        FrameRate = cap.AverageFrameRate
                    };
                    _nativeResolutions.Add(info);

                    // 选择与当前 _resolutionWidth/Height 最接近的 native 模式
                    int diff = Math.Abs(info.Width - _resolutionWidth)
                             + Math.Abs(info.Height - _resolutionHeight);
                    if (diff < bestMatchDiff)
                    {
                        bestMatchDiff = diff;
                        bestMatch = i;
                    }
                }

                if (bestMatch >= 0)
                {
                    _selectedResolutionIndex = bestMatch;
                    var r = _nativeResolutions[bestMatch];
                    _resolutionWidth = r.Width;
                    _resolutionHeight = r.Height;
                }

                LogHelper.WriteLogToFile(
                    $"[AForge] RefreshNativeResolutions 完成，共 {_nativeResolutions.Count} 项，选中: {_selectedResolutionIndex}",
                    LogHelper.LogType.Info);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[AForge] 枚举 native 分辨率失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>把当前选中的 native 分辨率应用到 VideoCaptureDevice。</summary>
        private void ApplyVideoResolutionToVideoSource()
        {
            if (_videoSource == null) return;
            if (_selectedResolutionIndex < 0 || _selectedResolutionIndex >= _nativeResolutions.Count) return;

            try
            {
                var caps = _videoSource.VideoCapabilities;
                if (caps == null || _selectedResolutionIndex >= caps.Length) return;
                _videoSource.VideoResolution = caps[_selectedResolutionIndex];
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"应用 native 分辨率失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>停止摄像头预览。</summary>
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

        /// <summary>获取当前帧的 BitmapSource（WPF 格式）。</summary>
        public BitmapSource GetCurrentFrameAsBitmapSource()
        {
            lock (_frameLock)
            {
                if (_currentFrame == null)
                    return null;

                try
                {
                    if (_currentFrame.Width <= 0 || _currentFrame.Height <= 0)
                        return null;

                    var bitmapData = _currentFrame.LockBits(
                        new Rectangle(0, 0, _currentFrame.Width, _currentFrame.Height),
                        ImageLockMode.ReadOnly,
                        _currentFrame.PixelFormat);

                    try
                    {
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

        /// <summary>获取当前帧的 Bitmap 副本（调用方负责 Dispose）。</summary>
        public Bitmap GetCurrentFrameAsBitmap()
        {
            lock (_frameLock)
            {
                if (_currentFrame == null) return null;
                try
                {
                    return (Bitmap)_currentFrame.Clone();
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>视频源新帧事件处理。</summary>
        private void VideoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            try
            {
                lock (_frameLock)
                {
                    _currentFrame?.Dispose();

                    var sourceFrame = eventArgs.Frame;
                    if (sourceFrame != null && sourceFrame.Width > 0 && sourceFrame.Height > 0)
                    {
                        try
                        {
                            Bitmap rotatedFrame = ApplyRotation(sourceFrame);

                            int targetWidth = _resolutionWidth;
                            int targetHeight = _resolutionHeight;

                            if (_rotationAngle == 1 || _rotationAngle == 3)
                            {
                                targetWidth = _resolutionHeight;
                                targetHeight = _resolutionWidth;
                            }

                            _currentFrame = ResizeImageWithAspectRatio(rotatedFrame, targetWidth, targetHeight);
                            rotatedFrame?.Dispose();
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

                var previewSource = GetCurrentFrameAsBitmapSource();

                _dispatcher.BeginInvoke(new Action(() =>
                {
                    FrameReceived?.Invoke(this, new FrameEventArgs { Frame = previewSource });
                }));
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理新帧失败: {ex.Message}", LogHelper.LogType.Error);
                ErrorOccurred?.Invoke(this, $"处理新帧失败: {ex.Message}");
            }
        }

        /// <summary>获取摄像头名称列表。</summary>
        public List<string> GetCameraNames()
        {
            return AvailableCameras.Select(c => c.Name).ToList();
        }

        /// <summary>检查是否有可用摄像头。</summary>
        public bool HasAvailableCameras()
        {
            if (AvailableCameras.Count == 0)
            {
                RefreshCameraListAsync().GetAwaiter().GetResult();
            }
            return AvailableCameras.Count > 0;
        }

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

        private Bitmap ResizeImageWithAspectRatio(Bitmap source, int targetWidth, int targetHeight)
        {
            if (source.Width == targetWidth && source.Height == targetHeight)
                return new Bitmap(source);

            double scaleX = (double)targetWidth / source.Width;
            double scaleY = (double)targetHeight / source.Height;
            double scale = Math.Min(scaleX, scaleY);

            int actualWidth = (int)(source.Width * scale);
            int actualHeight = (int)(source.Height * scale);

            var resized = new Bitmap(actualWidth, actualHeight, PixelFormat.Format24bppRgb);
            using (var graphics = Graphics.FromImage(resized))
            {
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                graphics.DrawImage(source, 0, 0, actualWidth, actualHeight);
            }
            return resized;
        }

        public void Dispose()
        {
            StopPreview();

            lock (_frameLock)
            {
                _currentFrame?.Dispose();
                _currentFrame = null;
            }
        }
    }
}
