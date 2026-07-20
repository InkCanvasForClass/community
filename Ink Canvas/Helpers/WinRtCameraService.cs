using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Devices.Enumeration;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 基于 WinRT MediaFrameReader 的摄像头服务实现，Win10 1607+ 可用。
    /// 相比 AForge DirectShow 性能更高（SoftwareBitmap 路径，无 GDI+ 中转）。
    /// </summary>
    public sealed class WinRtCameraService : ICameraService
    {
        private MediaCapture _mediaCapture;
        private MediaFrameReader _reader;
        private MediaFrameSource _colorSource;
        private SoftwareBitmap _lastBitmap;
        private readonly object _frameLock = new object();
        private int _rotationAngle = 0;
        private Dispatcher _dispatcher;
        private bool _camerasInitialized;

        private readonly List<CameraInfo> _cameras = new List<CameraInfo>();
        private readonly List<ResolutionInfo> _nativeResolutions = new List<ResolutionInfo>();
        private int _selectedResolutionIndex = -1;

        public event EventHandler<FrameEventArgs> FrameReceived;
        public event EventHandler<string> ErrorOccurred;

        public bool IsCapturing => _reader != null;

        public IReadOnlyList<CameraInfo> AvailableCameras => _cameras;
        public CameraInfo CurrentCamera { get; private set; }
        public IReadOnlyList<ResolutionInfo> NativeResolutions => _nativeResolutions;

        public int RotationAngle
        {
            get => _rotationAngle;
            set => _rotationAngle = Math.Max(0, Math.Min(3, value));
        }

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
                    // 通过 SetFormatAsync 把所选 MediaFrameFormat 应用到底层帧源
                    _ = ApplyNativeResolutionAsync();
                }
            }
        }

        public WinRtCameraService()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            // 不在构造函数里同步等待异步枚举，避免 UI 线程死锁
            // 首次 RefreshCameraList 调用或 StartPreview 时会自动初始化
        }

        /// <summary>刷新可用摄像头列表（WinRT 异步实现，调用方应 await）。</summary>
        public async Task RefreshCameraListAsync()
        {
            try
            {
                LogHelper.WriteLogToFile("[WinRT] RefreshCameraListAsync 开始", LogHelper.LogType.Info);
                _cameras.Clear();

                var devices = await DeviceInformation.FindAllAsync(
                    DeviceClass.VideoCapture);

                foreach (var dev in devices)
                {
                    // 仅保留颜色摄像头（避免 IR / Depth 等）
                    _cameras.Add(new CameraInfo
                    {
                        Name = string.IsNullOrWhiteSpace(dev.Name) ? "Camera" : dev.Name,
                        MonikerString = dev.Id
                    });
                }
                _camerasInitialized = true;
                LogHelper.WriteLogToFile(
                    $"[WinRT] RefreshCameraListAsync 完成，共 {_cameras.Count} 个摄像头",
                    LogHelper.LogType.Info);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[WinRT] 刷新摄像头列表失败: {ex.Message}", LogHelper.LogType.Error);
                ErrorOccurred?.Invoke(this, $"WinRT 刷新摄像头列表失败: {ex.Message}");
            }
        }

        public async Task<bool> StartPreviewAsync(int cameraIndex = 0)
        {
            try
            {
                LogHelper.WriteLogToFile(
                    $"[WinRT] StartPreviewAsync 开始，cameraIndex={cameraIndex}，_camerasInitialized={_camerasInitialized}，_cameras.Count={_cameras.Count}",
                    LogHelper.LogType.Info);

                if (!_camerasInitialized || _cameras.Count == 0)
                {
                    await RefreshCameraListAsync();
                    if (_cameras.Count == 0)
                    {
                        LogHelper.WriteLogToFile("[WinRT] StartPreview: 未找到可用摄像头", LogHelper.LogType.Warning);
                        ErrorOccurred?.Invoke(this, "未找到可用的摄像头设备");
                        return false;
                    }
                }

                if (cameraIndex < 0 || cameraIndex >= _cameras.Count)
                {
                    ErrorOccurred?.Invoke(this, "摄像头索引超出范围");
                    return false;
                }

                StopPreview();

                CurrentCamera = _cameras[cameraIndex];
                LogHelper.WriteLogToFile(
                    $"[WinRT] StartPreview: 选中 {CurrentCamera.Name}（id={CurrentCamera.MonikerString}）",
                    LogHelper.LogType.Info);

                var sourceGroup = await FindSourceGroupForDeviceAsync(CurrentCamera.MonikerString);
                if (sourceGroup == null)
                {
                    LogHelper.WriteLogToFile(
                        $"[WinRT] StartPreview: 找不到 {CurrentCamera.Name} 对应的 MediaFrameSourceGroup",
                        LogHelper.LogType.Error);
                    ErrorOccurred?.Invoke(this, $"找不到设备对应的 MediaFrameSourceGroup: {CurrentCamera.Name}");
                    return false;
                }

                _mediaCapture = new MediaCapture();
                await _mediaCapture.InitializeAsync(new MediaCaptureInitializationSettings
                {
                    SourceGroup = sourceGroup,
                    SharingMode = MediaCaptureSharingMode.ExclusiveControl,
                    MemoryPreference = MediaCaptureMemoryPreference.Cpu,  // 取 SoftwareBitmap
                    StreamingCaptureMode = StreamingCaptureMode.Video
                });

                // 选择颜色帧源
                _colorSource = _mediaCapture.FrameSources.Values.FirstOrDefault(s =>
                    s.Info.SourceKind == MediaFrameSourceKind.Color
                    && s.Info.MediaStreamType == MediaStreamType.VideoPreview)
                    ?? _mediaCapture.FrameSources.Values.FirstOrDefault(s =>
                        s.Info.SourceKind == MediaFrameSourceKind.Color)
                    ?? _mediaCapture.FrameSources.Values.First();

                // 枚举 native 分辨率并应用当前选择
                RefreshNativeResolutions();
                await ApplyNativeResolutionAsync();

                _reader = await _mediaCapture.CreateFrameReaderAsync(_colorSource, MediaEncodingSubtypes.Bgra8);
                _reader.FrameArrived += OnFrameArrived;
                var status = await _reader.StartAsync();
                if (status != MediaFrameReaderStartStatus.Success)
                {
                    LogHelper.WriteLogToFile(
                        $"[WinRT] StartPreview: FrameReader 启动失败 status={status}",
                        LogHelper.LogType.Error);
                    ErrorOccurred?.Invoke(this, $"FrameReader 启动失败: {status}");
                    StopPreview();
                    return false;
                }

                LogHelper.WriteLogToFile(
                    $"[WinRT] StartPreview 成功: {CurrentCamera.Name}，native 分辨率数: {_nativeResolutions.Count}，选中索引: {_selectedResolutionIndex}",
                    LogHelper.LogType.Info);
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[WinRT] 启动摄像头预览失败: {ex.Message}", LogHelper.LogType.Error);
                ErrorOccurred?.Invoke(this, $"WinRT 启动摄像头预览失败: {ex.Message}");
                return false;
            }
        }

        private async Task<MediaFrameSourceGroup> FindSourceGroupForDeviceAsync(string deviceId)
        {
            var groups = await MediaFrameSourceGroup.FindAllAsync();
            foreach (var g in groups)
            {
                var info = g.SourceInfos.FirstOrDefault(s => s.DeviceInformation?.Id == deviceId);
                if (info != null) return g;
            }
            return null;
        }

        /// <summary>从 MediaFrameSource.SupportedFormats 枚举分辨率列表。</summary>
        private void RefreshNativeResolutions()
        {
            _nativeResolutions.Clear();
            _selectedResolutionIndex = -1;

            try
            {
                if (_colorSource?.SupportedFormats == null) return;

                int bestIndex = -1;
                int bestDiff = int.MaxValue;
                // 默认偏好：1920×1080
                const int preferredW = 1920;
                const int preferredH = 1080;

                var formats = _colorSource.SupportedFormats.ToList();
                for (int i = 0; i < formats.Count; i++)
                {
                    var fmt = formats[i];
                    var info = new ResolutionInfo
                    {
                        Width = (int)(fmt.VideoFormat?.Width ?? 0),
                        Height = (int)(fmt.VideoFormat?.Height ?? 0),
                        FrameRate = fmt.FrameRate?.Numerator > 0 && fmt.FrameRate.Denominator > 0
                            ? (int)(fmt.FrameRate.Numerator / (double)fmt.FrameRate.Denominator)
                            : 0
                    };
                    if (info.Width <= 0 || info.Height <= 0) continue;

                    _nativeResolutions.Add(info);

                    int diff = Math.Abs(info.Width - preferredW) + Math.Abs(info.Height - preferredH);
                    if (diff < bestDiff)
                    {
                        bestDiff = diff;
                        bestIndex = _nativeResolutions.Count - 1;
                    }
                }

                if (bestIndex >= 0)
                {
                    _selectedResolutionIndex = bestIndex;
                }
                else if (_nativeResolutions.Count > 0)
                {
                    _selectedResolutionIndex = 0;
                }

                LogHelper.WriteLogToFile(
                    $"[WinRT] RefreshNativeResolutions 完成，共 {_nativeResolutions.Count} 项，选中: {_selectedResolutionIndex}",
                    LogHelper.LogType.Info);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[WinRT] 枚举 native 分辨率失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private async Task ApplyNativeResolutionAsync()
        {
            try
            {
                if (_colorSource == null) return;
                if (_selectedResolutionIndex < 0 || _selectedResolutionIndex >= _nativeResolutions.Count) return;

                // 用 SupportedFormats 与 NativeResolutions 的索引对应关系查找 MediaFrameFormat
                var formats = _colorSource.SupportedFormats.ToList();
                var target = _nativeResolutions[_selectedResolutionIndex];

                // 找到第一个匹配的 MediaFrameFormat
                MediaFrameFormat matched = null;
                int matchedIndex = -1;
                int walkIndex = -1;
                foreach (var fmt in formats)
                {
                    walkIndex++;
                    int w = (int)(fmt.VideoFormat?.Width ?? 0);
                    int h = (int)(fmt.VideoFormat?.Height ?? 0);
                    if (w == target.Width && h == target.Height)
                    {
                        matched = fmt;
                        matchedIndex = walkIndex;
                        break;
                    }
                }

                if (matched == null)
                {
                    LogHelper.WriteLogToFile(
                        $"WinRT 找不到匹配的 MediaFrameFormat: {target.Width}x{target.Height}",
                        LogHelper.LogType.Warning);
                    return;
                }

                await _colorSource.SetFormatAsync(matched);
                // 同步 _selectedResolutionIndex 与 NativeResolutions 列表的索引
                // （NativeResolutions 已过滤掉无效项，与 formats 不一定一一对应，但已用匹配的 width/height 标识）
                _ = matchedIndex; // 仅用于调试
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"WinRT 应用 native 分辨率失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void OnFrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
        {
            try
            {
                using var frame = sender.TryAcquireLatestFrame();
                if (frame == null) return;

                var srcBitmap = frame.VideoMediaFrame?.SoftwareBitmap;
                if (srcBitmap == null) return;

                // 必须复制一份：frame 被 dispose 后，原始 SoftwareBitmap 会失效
                SoftwareBitmap copy = srcBitmap.BitmapPixelFormat == BitmapPixelFormat.Bgra8
                    ? SoftwareBitmap.Copy(srcBitmap)
                    : SoftwareBitmap.Convert(srcBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore);

                lock (_frameLock)
                {
                    _lastBitmap?.Dispose();
                    _lastBitmap = copy;
                }

                var previewSource = GetCurrentFrameAsBitmapSource();
                if (previewSource == null) return;

                // 与 AForge 实现保持一致：在 UI 线程上触发事件
                _dispatcher.BeginInvoke(new Action(() =>
                {
                    FrameReceived?.Invoke(this, new FrameEventArgs { Frame = previewSource });
                }));
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"WinRT 帧处理失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        public BitmapSource GetCurrentFrameAsBitmapSource()
        {
            lock (_frameLock)
            {
                if (_lastBitmap == null) return null;
                return ConvertSoftwareBitmapToBitmapSource(_lastBitmap);
            }
        }

        /// <summary>
        /// SoftwareBitmap → WPF BitmapSource（应用旋转）。
        /// 用 BGRA → byte[] → BitmapSource.Create 路径，避免依赖 WriteableBitmap.PixelBuffer（WPF 不存在该属性）。
        /// 旋转统一走 GDI+ 处理后的 Bitmap，避免依赖 SoftwareBitmap.Transform（部分 SDK 投射下不存在）。
        /// </summary>
        private BitmapSource ConvertSoftwareBitmapToBitmapSource(SoftwareBitmap sb)
        {
            try
            {
                if (sb == null) return null;
                int width = sb.PixelWidth;
                int height = sb.PixelHeight;
                if (width <= 0 || height <= 0) return null;

                // 旋转角度为 0 时走快路径：SoftwareBitmap → byte[] → BitmapSource.Create
                if (_rotationAngle == 0)
                {
                    return SoftwareBitmapToBitmapSourceFast(sb);
                }

                // 需要旋转：先转 Bitmap，再用 GDI+ RotateFlip，再转回 BitmapSource
                using var bmp = SoftwareBitmapToBitmap(sb);
                if (bmp == null) return null;

                var rotationType = _rotationAngle switch
                {
                    1 => RotateFlipType.Rotate90FlipNone,
                    2 => RotateFlipType.Rotate180FlipNone,
                    3 => RotateFlipType.Rotate270FlipNone,
                    _ => RotateFlipType.RotateNoneFlipNone
                };
                bmp.RotateFlip(rotationType);

                return BitmapToBitmapSource(bmp);
            }
            catch
            {
                return null;
            }
        }

        private static BitmapSource SoftwareBitmapToBitmapSourceFast(SoftwareBitmap sb)
        {
            try
            {
                int width = sb.PixelWidth;
                int height = sb.PixelHeight;

                var bgra = sb.BitmapPixelFormat == BitmapPixelFormat.Bgra8
                    ? sb
                    : SoftwareBitmap.Convert(sb, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore);

                int bufferSize = width * height * 4;
                byte[] buffer = new byte[bufferSize];
                bgra.CopyToBuffer(buffer.AsBuffer());

                var bitmapSource = BitmapSource.Create(
                    width, height,
                    96.0, 96.0,
                    System.Windows.Media.PixelFormats.Bgra32,
                    null,
                    buffer,
                    width * 4);

                bitmapSource.Freeze();

                if (!ReferenceEquals(bgra, sb)) bgra.Dispose();
                return bitmapSource;
            }
            catch
            {
                return null;
            }
        }

        private static BitmapSource BitmapToBitmapSource(Bitmap bitmap)
        {
            try
            {
                if (bitmap == null || bitmap.Width <= 0 || bitmap.Height <= 0) return null;

                var bitmapData = bitmap.LockBits(
                    new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    ImageLockMode.ReadOnly,
                    bitmap.PixelFormat);

                try
                {
                    System.Windows.Media.PixelFormat wpfPixelFormat = bitmap.PixelFormat switch
                    {
                        PixelFormat.Format24bppRgb => System.Windows.Media.PixelFormats.Bgr24,
                        PixelFormat.Format32bppArgb => System.Windows.Media.PixelFormats.Bgra32,
                        PixelFormat.Format32bppRgb => System.Windows.Media.PixelFormats.Bgr32,
                        _ => System.Windows.Media.PixelFormats.Bgr24,
                    };

                    var bitmapSource = BitmapSource.Create(
                        bitmapData.Width,
                        bitmapData.Height,
                        bitmap.HorizontalResolution,
                        bitmap.VerticalResolution,
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
                    bitmap.UnlockBits(bitmapData);
                }
            }
            catch
            {
                return null;
            }
        }

        public Bitmap GetCurrentFrameAsBitmap()
        {
            lock (_frameLock)
            {
                if (_lastBitmap == null) return null;
                try
                {
                    var bmp = SoftwareBitmapToBitmap(_lastBitmap);
                    if (bmp == null) return null;

                    if (_rotationAngle != 0)
                    {
                        var rotationType = _rotationAngle switch
                        {
                            1 => RotateFlipType.Rotate90FlipNone,
                            2 => RotateFlipType.Rotate180FlipNone,
                            3 => RotateFlipType.Rotate270FlipNone,
                            _ => RotateFlipType.RotateNoneFlipNone
                        };
                        bmp.RotateFlip(rotationType);
                    }
                    return bmp;
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"WinRT SoftwareBitmap→Bitmap 转换失败: {ex.Message}", LogHelper.LogType.Error);
                    return null;
                }
            }
        }

        /// <summary>SoftwareBitmap 转 GDI+ Bitmap（用于拍照后的 AForge 图像处理路径）。</summary>
        private static Bitmap SoftwareBitmapToBitmap(SoftwareBitmap softwareBitmap)
        {
            if (softwareBitmap == null) return null;

            int width = softwareBitmap.PixelWidth;
            int height = softwareBitmap.PixelHeight;

            // 强制为 BGRA8 以便直接拷贝到 System.Drawing.Bitmap (32bppArgb)
            var bgra = softwareBitmap.BitmapPixelFormat == BitmapPixelFormat.Bgra8
                ? softwareBitmap
                : SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore);

            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            var data = bitmap.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);
            try
            {
                // SoftwareBitmap (BGRA8) 行 stride = width * 4，与 32bppArgb 一致
                int bufferSize = width * height * 4;
                byte[] buffer = new byte[bufferSize];
                bgra.CopyToBuffer(buffer.AsBuffer());
                int bytesToCopy = Math.Min(buffer.Length, data.Stride * height);
                Marshal.Copy(buffer, 0, data.Scan0, bytesToCopy);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            if (!ReferenceEquals(bgra, softwareBitmap))
            {
                bgra.Dispose();
            }
            return bitmap;
        }

        /// <summary>
        /// 同步停止预览：立即清理本地字段（避免阻塞 UI 线程），异步等待 reader.StopAsync 在后台进行。
        /// </summary>
        public void StopPreview()
        {
            var reader = _reader;
            var capture = _mediaCapture;

            // 立即清空字段，防止 OnFrameArrived 再次访问
            if (reader != null)
            {
                _reader = null;
                try { reader.FrameArrived -= OnFrameArrived; } catch { }
            }
            _mediaCapture = null;
            _colorSource = null;

            lock (_frameLock)
            {
                _lastBitmap?.Dispose();
                _lastBitmap = null;
            }

            // 异步释放 WinRT 资源，不阻塞调用方
            _ = Task.Run(async () =>
            {
                try
                {
                    if (reader != null)
                    {
                        try { await reader.StopAsync(); } catch { }
                        reader.Dispose();
                    }
                    capture?.Dispose();
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"[WinRT] StopPreview 异步释放失败: {ex.Message}", LogHelper.LogType.Error);
                }
            });
        }

        public void Dispose()
        {
            StopPreview();
        }
    }
}
