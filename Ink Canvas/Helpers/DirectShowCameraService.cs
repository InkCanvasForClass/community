using DirectShowLib;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 基于 DirectShow (DirectShowLib) FilterGraph + SampleGrabber 的摄像头服务实现。
    /// 不依赖 AForge.Video / WinRT，纯 DirectShow + GDI+。兼容 Win7 SP1+。
    /// ScreenshotSelectorWindow 使用此实现（不创建 UI 控件，纯帧事件路径）。
    /// 视频展台特殊模式（全屏预览）仍走 MainWindow.VideoPresenterFullCanvasImage (WPFMediaKit VideoCaptureElement)。
    /// </summary>
    public sealed class DirectShowCameraService : ICameraService, ISampleGrabberCB
    {
        // === DirectShow 图组件 ===
        private IFilterGraph2 _filterGraph;
        private IBaseFilter _sourceFilter;
        private IBaseFilter _sampleGrabberFilter;
        private ISampleGrabber _sampleGrabber;
        private IBaseFilter _nullRenderer;
        private IMediaControl _mediaControl;

        // === 状态 ===
        private bool _isCapturing;
        private Bitmap _currentFrame;
        private readonly object _frameLock = new object();
        private Dispatcher _dispatcher;
        private int _rotationAngle = 0;

        private readonly List<CameraInfo> _cameras = new List<CameraInfo>();
        private readonly List<ResolutionInfo> _nativeResolutions = new List<ResolutionInfo>();
        private int _selectedResolutionIndex = -1;

        // === 复用缓冲区：避免每帧新建 BitmapSource 触发 WPF DUCE 纹理堆积 OOM ===
        // 复用的 WriteableBitmap（UI 线程访问，通过 WritePixels 更新内容；Image.Source 持续指向它，
        // WPF 合成器通过 AddDirtyRect 自动重绘，不再每帧重新分配 GPU 纹理）
        private WriteableBitmap _reusableBitmap;
        // 复用的后台 byte 缓冲区：BufferCB 把数据复制到这里（可在 DirectShow 工作线程操作）
        private byte[] _reusableBuffer;
        // 当前协商出的 stride（RGB24 实际行字节数，未必对齐到 4；用于诊断）
        private int _sourceStride;

        public event EventHandler<FrameEventArgs> FrameReceived;
        public event EventHandler<string> ErrorOccurred;

        public bool IsCapturing => _isCapturing;

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
                // 同步派生索引（保留当前帧率选择如果可能）
                SyncDerivedIndicesFromCapability();
                if (value >= 0 && _isCapturing)
                {
                    // 运行时切换分辨率：停图后重新 StartPreviewAsync
                    // （DirectShow 的 SetFormat 在 graph 运行中调用效果不稳，重建更可靠）
                    _ = RestartWithNewResolutionAsync();
                }
            }
        }

        /// <summary>
        /// 静默更新 SelectedResolutionIndex（不触发 RestartWithNewResolutionAsync）。
        /// 用于特殊模式下 VideoCaptureElement 接管预览时，_cameraService 不应抢占摄像头设备，
        /// 调用者（MainWindow）会直接重新启动 VideoCaptureElement 应用新分辨率。
        /// </summary>
        public void SetSelectedResolutionIndexSilent(int value)
        {
            if (value == _selectedResolutionIndex) return;
            if (value < -1 || value >= _nativeResolutions.Count)
                throw new ArgumentOutOfRangeException(nameof(value));
            _selectedResolutionIndex = value;
            SyncDerivedIndicesFromCapability();
        }

        /// <summary>去重后的分辨率列表（按 W,H 分组，FrameRate 取最大值，便于 ComboBox 显示）。</summary>
        public IReadOnlyList<ResolutionInfo> UniqueResolutions { get; } = new List<ResolutionInfo>();

        /// <summary>
        /// 所有有效的 (W, H, FPS) 组合（去重）。
        /// 排序：先按分辨率降序（像素数从大到小），同分辨率内按帧率降序。
        /// 用于单 ComboBox 填充"分辨率@帧数"组合选项。
        /// </summary>
        public IReadOnlyList<ResolutionInfo> AllResolutionFpsCombos { get; } = new List<ResolutionInfo>();

        /// <summary>当前在 AllResolutionFpsCombos 中的选中索引；-1 表示未选中。</summary>
        public int SelectedComboIndex { get; set; } = -1;

        /// <summary>获取指定分辨率下支持的帧率列表（去重、降序）。</summary>
        public IReadOnlyList<int> GetFrameratesFor(int width, int height)
        {
            var result = new List<int>();
            try
            {
                foreach (var r in _nativeResolutions)
                {
                    if (r.Width == width && r.Height == height && r.FrameRate > 0)
                    {
                        if (!result.Contains(r.FrameRate))
                            result.Add(r.FrameRate);
                    }
                }
                result.Sort((a, b) => b.CompareTo(a)); // 降序，常用 60fps 在前
            }
            catch { }
            return result;
        }

        /// <summary>在 NativeResolutions 中查找匹配 (W, H, FPS) 的 capability 索引。</summary>
        public int FindCapabilityIndex(int width, int height, int framerate)
        {
            int fallback = -1;
            int fallbackDiff = int.MaxValue;
            for (int i = 0; i < _nativeResolutions.Count; i++)
            {
                var r = _nativeResolutions[i];
                if (r.Width != width || r.Height != height) continue;
                if (fallback < 0) fallback = i;
                if (framerate <= 0)
                {
                    return i;
                }
                int diff = Math.Abs(r.FrameRate - framerate);
                if (diff < fallbackDiff)
                {
                    fallbackDiff = diff;
                    fallback = i;
                }
                if (diff == 0) return i;
            }
            return fallback;
        }

        /// <summary>当前选中的"去重分辨率索引"；-1 表示未选中。</summary>
        public int SelectedUniqueResolutionIndex { get; set; } = -1;

        /// <summary>当前分辨率下的帧率索引（GetFrameratesFor 返回列表的索引）；-1 表示未选中。</summary>
        public int SelectedFramerateIndex
        {
            get;
            set;
        }

        /// <summary>在 capability index 变更时同步派生索引（SelectedUniqueResolutionIndex / SelectedFramerateIndex）。</summary>
        private void SyncDerivedIndicesFromCapability()
        {
            if (_selectedResolutionIndex < 0 || _selectedResolutionIndex >= _nativeResolutions.Count)
            {
                SelectedUniqueResolutionIndex = -1;
                SelectedFramerateIndex = -1;
                return;
            }

            var current = _nativeResolutions[_selectedResolutionIndex];

            // 找 UniqueResolutions 中对应的索引
            int uIdx = -1;
            var list = (List<ResolutionInfo>)UniqueResolutions;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Width == current.Width && list[i].Height == current.Height)
                {
                    uIdx = i;
                    break;
                }
            }
            SelectedUniqueResolutionIndex = uIdx;

            // 找 GetFrameratesFor(W,H) 中当前帧率的索引
            if (uIdx >= 0 && current.FrameRate > 0)
            {
                var framerates = GetFrameratesFor(current.Width, current.Height);
                int fIdx = -1;
                for (int i = 0; i < framerates.Count; i++)
                {
                    if (framerates[i] == current.FrameRate)
                    {
                        fIdx = i;
                        break;
                    }
                }
                SelectedFramerateIndex = fIdx;
            }
            else
            {
                SelectedFramerateIndex = -1;
            }

            // 同步 SelectedComboIndex：在 AllResolutionFpsCombos 中找匹配的 (W, H, FPS)
            int comboIdx = -1;
            var comboList = (List<ResolutionInfo>)AllResolutionFpsCombos;
            for (int i = 0; i < comboList.Count; i++)
            {
                if (comboList[i].Width == current.Width
                    && comboList[i].Height == current.Height
                    && comboList[i].FrameRate == current.FrameRate)
                {
                    comboIdx = i;
                    break;
                }
            }
            SelectedComboIndex = comboIdx;
        }

        public DirectShowCameraService()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            // 不在构造函数里同步等待，避免 UI 线程死锁
            _ = RefreshCameraListAsync();
        }

        /// <summary>刷新可用摄像头列表（DirectShow 同步完成）。</summary>
        public Task RefreshCameraListAsync()
        {
            try
            {
                _cameras.Clear();
                var devices = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);
                foreach (var dev in devices)
                {
                    _cameras.Add(new CameraInfo
                    {
                        Name = string.IsNullOrWhiteSpace(dev.Name) ? "Camera" : dev.Name,
                        // DevicePath 是设备路径（与 WPFMediaKit MultimediaUtil.VideoInputDevices 一致）；
                        // 内部 CreateSourceFilter 会用 DsDevice.Mon 重新 BindToObject
                        MonikerString = dev.DevicePath ?? dev.Name
                    });
                }

                LogHelper.WriteLogToFile(
                    $"[DirectShow] RefreshCameraList 完成，共 {_cameras.Count} 个摄像头",
                    LogHelper.LogType.Info);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[DirectShow] 刷新摄像头列表失败: {ex.Message}", LogHelper.LogType.Error);
                ErrorOccurred?.Invoke(this, $"刷新摄像头列表失败: {ex.Message}");
            }
            return Task.CompletedTask;
        }

        /// <summary>启动指定摄像头的预览。DirectShow 同步实现，但返回 Task 以保持接口一致。</summary>
        public Task<bool> StartPreviewAsync(int cameraIndex = 0)
        {
            return Task.Run(() => StartPreviewCore(cameraIndex));
        }

        private bool StartPreviewCore(int cameraIndex)
        {
            try
            {
                if (_cameras.Count == 0)
                {
                    RefreshCameraListAsync().GetAwaiter().GetResult();
                    if (_cameras.Count == 0)
                    {
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

                // === 构建 FilterGraph: source -> sample grabber -> null renderer ===
                _filterGraph = (IFilterGraph2)new FilterGraph();

                // 1. 创建 source filter（通过 moniker 重新枚举找到对应 DsDevice）
                _sourceFilter = CreateSourceFilterByDevicePath(CurrentCamera.MonikerString);
                int hr = _filterGraph.AddFilter(_sourceFilter, "Video Source");
                DsError.ThrowExceptionForHR(hr);

                // 2. 枚举 native 分辨率并应用当前选择（必须在连接 pin 之前 SetFormat）
                RefreshNativeResolutions();
                ApplyNativeResolution();

                // 3. 创建 Sample Grabber
                _sampleGrabberFilter = (IBaseFilter)new SampleGrabber();
                _sampleGrabber = (ISampleGrabber)_sampleGrabberFilter;
                hr = _filterGraph.AddFilter(_sampleGrabberFilter, "Sample Grabber");
                DsError.ThrowExceptionForHR(hr);

                // 配置 Sample Grabber 的媒体类型为 RGB24（grabber 会自动做色彩空间转换）
                var mediaType = new AMMediaType
                {
                    majorType = MediaType.Video,
                    subType = MediaSubType.RGB24,
                    formatType = FormatType.VideoInfo
                };
                hr = _sampleGrabber.SetMediaType(mediaType);
                DsUtils.FreeAMMediaType(mediaType);
                DsError.ThrowExceptionForHR(hr);

                _sampleGrabber.SetBufferSamples(true);
                _sampleGrabber.SetOneShot(false);
                // 1 = 使用 BufferCB（不传整块 IMediaSample，性能更好）
                hr = _sampleGrabber.SetCallback(this, 1);
                DsError.ThrowExceptionForHR(hr);

                // 4. 创建 NullRenderer（丢弃已抓取的帧，不需要渲染到窗口）
                _nullRenderer = (IBaseFilter)new NullRenderer();
                hr = _filterGraph.AddFilter(_nullRenderer, "Null Renderer");
                DsError.ThrowExceptionForHR(hr);

                // 5. 手动连接 source capture pin -> grabber input -> grabber output -> null renderer input
                //    （比 RenderEx 更可控，且不依赖 RenderEx 的具体签名）
                var capturePin = DsFindPin.ByCategory(_sourceFilter, PinCategory.Capture, 0);
                var grabberIn = DsFindPin.ByDirection(_sampleGrabberFilter, PinDirection.Input, 0);
                var grabberOut = DsFindPin.ByDirection(_sampleGrabberFilter, PinDirection.Output, 0);
                var nullIn = DsFindPin.ByDirection(_nullRenderer, PinDirection.Input, 0);

                hr = _filterGraph.Connect(capturePin, grabberIn);
                DsError.ThrowExceptionForHR(hr);
                hr = _filterGraph.Connect(grabberOut, nullIn);
                DsError.ThrowExceptionForHR(hr);

                // 6. 启动图
                _mediaControl = (IMediaControl)_filterGraph;
                hr = _mediaControl.Run();
                DsError.ThrowExceptionForHR(hr);

                _isCapturing = true;
                LogHelper.WriteLogToFile(
                    $"[DirectShow] StartPreview 成功: {CurrentCamera.Name}, native 分辨率数: {_nativeResolutions.Count}, 选中: {_selectedResolutionIndex}",
                    LogHelper.LogType.Info);
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[DirectShow] 启动摄像头预览失败: {ex.Message}", LogHelper.LogType.Error);
                ErrorOccurred?.Invoke(this, $"启动摄像头预览失败: {ex.Message}");
                CleanupGraph();
                return false;
            }
        }

        private IBaseFilter CreateSourceFilterByDevicePath(string devicePath)
        {
            var devices = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);
            foreach (var dev in devices)
            {
                if (string.Equals(dev.DevicePath, devicePath, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(dev.Name, devicePath, StringComparison.OrdinalIgnoreCase))
                {
                    object source;
                    Guid iid = typeof(IBaseFilter).GUID;
                    dev.Mon.BindToObject(null, null, ref iid, out source);
                    return (IBaseFilter)source;
                }
            }
            throw new InvalidOperationException($"找不到设备: {devicePath}");
        }

        /// <summary>从已添加到 FilterGraph 的 _sourceFilter 上枚举 native 分辨率列表。</summary>
        private void RefreshNativeResolutions()
        {
            try
            {
                if (_sourceFilter == null) return;

                var capturePin = DsFindPin.ByCategory(_sourceFilter, PinCategory.Capture, 0);
                var config = capturePin as IAMStreamConfig;
                if (config == null) return;

                EnumerateCapabilitiesFromConfig(config);

                LogHelper.WriteLogToFile(
                    $"[DirectShow] RefreshNativeResolutions 完成，共 {_nativeResolutions.Count} 项，选中: {_selectedResolutionIndex}，去重分辨率: {UniqueResolutions.Count}",
                    LogHelper.LogType.Info);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[DirectShow] 枚举 native 分辨率失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 从 IAMStreamConfig.GetStreamCaps 读取 native 分辨率列表，填充
        /// _nativeResolutions / _selectedResolutionIndex / UniqueResolutions / 派生索引。
        /// 抽自 RefreshNativeResolutions，供 RefreshNativeResolutions（StartPreviewCore 内调用）
        /// 与 EnumerateResolutionsAsync（不启动预览的独立枚举）共用。
        /// </summary>
        private void EnumerateCapabilitiesFromConfig(IAMStreamConfig config)
        {
            _nativeResolutions.Clear();
            _selectedResolutionIndex = -1;

            if (config == null) return;

            int hr = config.GetNumberOfCapabilities(out int count, out int size);
            if (hr != 0 || count <= 0 || size <= 0) return;

            // VideoStreamConfigCaps 结构大小
            IntPtr caps = Marshal.AllocHGlobal(size);
            try
            {
                int bestIndex = -1;
                int bestDiff = int.MaxValue;
                const int preferredW = 1920;
                const int preferredH = 1080;

                // 记录每个 (W, H) 已添加的帧率，避免重复
                var addedFramerates = new Dictionary<long, HashSet<int>>();

                for (int i = 0; i < count; i++)
                {
                    AMMediaType mt = null;
                    try
                    {
                        hr = config.GetStreamCaps(i, out mt, caps);
                        if (hr != 0 || mt == null) continue;

                        // 某些摄像头驱动通过 VideoInfo2 暴露 60fps 等高帧率 capability，
                        // 必须同时接受 VideoInfo 和 VideoInfo2，否则会漏掉 60fps（Q3 根因）。
                        bool isVideoInfo2 = mt.formatType == FormatType.VideoInfo2;
                        if (mt.formatType != FormatType.VideoInfo && !isVideoInfo2) continue;
                        if (mt.formatPtr == IntPtr.Zero) continue;

                        int width;
                        int height;
                        long avgTimePerFrame;
                        if (isVideoInfo2)
                        {
                            // VideoInfoHeader2 = VideoInfoHeader 字段 + InterlaceFlags/CopyProtectFlags/
                            //   PictAspectRatioX/PictAspectRatioY/ControlFlags/Reserved2 (4*7=28 字节)
                            //   BmiHeader 在最后，结构布局由 DirectShowLib 的 [StructLayout] 决定。
                            var vih2 = (VideoInfoHeader2)Marshal.PtrToStructure(
                                mt.formatPtr, typeof(VideoInfoHeader2));
                            width = vih2.BmiHeader.Width;
                            height = vih2.BmiHeader.Height;
                            avgTimePerFrame = vih2.AvgTimePerFrame;
                        }
                        else
                        {
                            var vih = (VideoInfoHeader)Marshal.PtrToStructure(
                                mt.formatPtr, typeof(VideoInfoHeader));
                            width = vih.BmiHeader.Width;
                            height = vih.BmiHeader.Height;
                            avgTimePerFrame = vih.AvgTimePerFrame;
                        }

                        if (width <= 0 || height <= 0) continue;

                        long key = ((long)width << 32) | (uint)height;
                        if (!addedFramerates.ContainsKey(key))
                            addedFramerates[key] = new HashSet<int>();

                        int defaultFps = avgTimePerFrame > 0
                            ? (int)Math.Round(10000000.0 / avgTimePerFrame)
                            : 30;

                        // 读取 VideoStreamConfigCaps 的 MinFrameInterval/MaxFrameInterval，
                        // 仅用于诊断和决定是否启用范围枚举。
                        long rangeMinInterval = 0, rangeMaxInterval = 0;
                        bool hasRange = TryReadFrameIntervalRange(caps, size, out rangeMinInterval, out rangeMaxInterval)
                            && rangeMaxInterval > rangeMinInterval && rangeMinInterval > 0;

                        // 诊断日志：记录每个 capability 的关键参数
                        try
                        {
                            LogHelper.WriteLogToFile(
                                $"[DirectShow] cap #{i}: {width}×{height}, avgTimePerFrame={avgTimePerFrame} (≈{defaultFps}fps), " +
                                $"minInterval={rangeMinInterval} (≈{(rangeMinInterval > 0 ? 10000000.0 / rangeMinInterval : 0):F1}fps), " +
                                $"maxInterval={rangeMaxInterval} (≈{(rangeMaxInterval > 0 ? 10000000.0 / rangeMaxInterval : 0):F1}fps), " +
                                $"formatType={mt.formatType}",
                                LogHelper.LogType.Info);
                        }
                        catch { }

                        // === 帧率枚举策略（修复 50fps 误判） ===
                        // 驱动通过两种方式暴露帧率：
                        //   (a) 单一 capability + AvgTimePerFrame 精确指定（如 60fps capability，AvgTimePerFrame=166667）
                        //   (b) 一个 capability 带范围（minInterval/maxInterval），表示该分辨率支持范围内所有帧率
                        // 旧逻辑用 commonFps={60,50,30,25,...} 在范围内"猜测"，会把 50fps 等中间值误判为支持。
                        // 新逻辑：
                        //   - 优先使用 AvgTimePerFrame（驱动明确声明的）
                        //   - 若有范围，再补充范围两端对应的帧率（最高 fps = 10000000/minInterval，最低 fps = 10000000/maxInterval）
                        //   - 不再用 commonFps 硬编码列表猜测中间值
                        var framerates = new List<int>();
                        framerates.Add(defaultFps);

                        if (hasRange)
                        {
                            // 范围上限（minInterval）对应的最高 fps
                            int rangeHighFps = (int)Math.Round(10000000.0 / rangeMinInterval);
                            // 范围下限（maxInterval）对应的最低 fps
                            int rangeLowFps = (int)Math.Round(10000000.0 / rangeMaxInterval);

                            if (rangeHighFps > 0 && rangeHighFps != defaultFps)
                                framerates.Add(rangeHighFps);
                            if (rangeLowFps > 0 && rangeLowFps != defaultFps && rangeLowFps != rangeHighFps)
                                framerates.Add(rangeLowFps);
                        }

                        // 去重
                        framerates = framerates.Distinct().ToList();

                        foreach (int fps in framerates)
                        {
                            if (addedFramerates[key].Contains(fps)) continue;
                            addedFramerates[key].Add(fps);

                            var info = new ResolutionInfo
                            {
                                Width = width,
                                Height = height,
                                FrameRate = fps
                            };
                            _nativeResolutions.Add(info);

                            int diff = Math.Abs(width - preferredW) + Math.Abs(height - preferredH);
                            if (diff < bestDiff)
                            {
                                bestDiff = diff;
                                bestIndex = _nativeResolutions.Count - 1;
                            }
                        }
                    }
                    finally
                    {
                        if (mt != null) DsUtils.FreeAMMediaType(mt);
                    }
                }

                if (bestIndex >= 0)
                    _selectedResolutionIndex = bestIndex;
                else if (_nativeResolutions.Count > 0)
                    _selectedResolutionIndex = 0;
            }
            finally
            {
                Marshal.FreeHGlobal(caps);
            }

            // 重建去重分辨率列表（UniqueResolutions）
            RebuildUniqueResolutions();
            // 同步派生索引
            SyncDerivedIndicesFromCapability();
        }

        /// <summary>
        /// 独立枚举指定摄像头的 native 分辨率（不启动预览，不抢占设备）。
        /// 参考 EasiCamera Cvte.MediaDevice.VideoInputService.GetAllAvailableResolution：
        /// 用 FilterGraphNoThread（无消息泵）+ ICaptureGraphBuilder2 + AddSourceFilterForMoniker
        /// 枚举 IAMStreamConfig.GetStreamCaps，不调用 IMediaControl.Run()，因此不会与
        /// VideoCaptureElement（特殊模式）或另一个 FilterGraph（_cameraService.StartPreviewAsync）抢占设备。
        /// 用于特殊模式下：先用此方法填充分辨率 ComboBox，再启动 VideoCaptureElement 预览。
        /// 调用后 AvailableCameras / NativeResolutions / UniqueResolutions / SelectedResolutionIndex
        /// / SelectedUniqueResolutionIndex / SelectedFramerateIndex / CurrentCamera 均已就绪。
        /// </summary>
        public Task EnumerateResolutionsAsync(int cameraIndex)
        {
            return Task.Run(() =>
            {
                try
                {
                    if (_cameras.Count == 0)
                    {
                        RefreshCameraListAsync().GetAwaiter().GetResult();
                        if (_cameras.Count == 0)
                        {
                            ErrorOccurred?.Invoke(this, "未找到可用的摄像头设备");
                            return;
                        }
                    }

                    if (cameraIndex < 0 || cameraIndex >= _cameras.Count)
                    {
                        ErrorOccurred?.Invoke(this, "摄像头索引超出范围");
                        return;
                    }

                    // 设置 CurrentCamera（即使没有真正启动预览，也记录当前选择；
                    // MainWindow.FindCurrentCameraIndex 依赖此字段）
                    CurrentCamera = _cameras[cameraIndex];

                    // 通过 _cameras 中保存的 MonikerString 重新枚举 DsDevice，
                    // 以便 AddSourceFilterForMoniker 拿到正确的 IMoniker
                    var devices = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);
                    DsDevice target = null;
                    for (int i = 0; i < devices.Length; i++)
                    {
                        if (string.Equals(devices[i].DevicePath, CurrentCamera.MonikerString, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(devices[i].Name, CurrentCamera.MonikerString, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(devices[i].Name, CurrentCamera.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            target = devices[i];
                            break;
                        }
                    }
                    if (target == null)
                    {
                        // 索引兜底
                        if (cameraIndex >= 0 && cameraIndex < devices.Length)
                            target = devices[cameraIndex];
                    }
                    if (target == null)
                    {
                        LogHelper.WriteLogToFile(
                            $"[DirectShow] EnumerateResolutionsAsync: 找不到摄像头 {CurrentCamera.Name}",
                            LogHelper.LogType.Warning);
                        return;
                    }

                    // === 构建 FilterGraphNoThread（无消息泵，不抢占设备）===
                    IGraphBuilder graphBuilder = null;
                    ICaptureGraphBuilder2 captureGraphBuilder = null;
                    IBaseFilter sourceFilter = null;
                    object streamConfigObj = null;
                    try
                    {
                        graphBuilder = (IGraphBuilder)new FilterGraphNoThread();
                        captureGraphBuilder = (ICaptureGraphBuilder2)new CaptureGraphBuilder2();
                        DsError.ThrowExceptionForHR(captureGraphBuilder.SetFiltergraph(graphBuilder));

                        DsError.ThrowExceptionForHR(((IFilterGraph2)graphBuilder).AddSourceFilterForMoniker(
                            target.Mon, null, target.Name, out sourceFilter));

                        DsError.ThrowExceptionForHR(captureGraphBuilder.FindInterface(
                            PinCategory.Capture, MediaType.Video, sourceFilter,
                            typeof(IAMStreamConfig).GUID, out streamConfigObj));

                        var streamConfig = streamConfigObj as IAMStreamConfig;
                        if (streamConfig == null)
                        {
                            LogHelper.WriteLogToFile(
                                "[DirectShow] EnumerateResolutionsAsync: 无法获取 IAMStreamConfig",
                                LogHelper.LogType.Warning);
                            return;
                        }

                        EnumerateCapabilitiesFromConfig(streamConfig);

                        LogHelper.WriteLogToFile(
                            $"[DirectShow] EnumerateResolutionsAsync 完成: {CurrentCamera.Name}, native={_nativeResolutions.Count}, unique={UniqueResolutions.Count}, selected={_selectedResolutionIndex}",
                            LogHelper.LogType.Info);
                    }
                    finally
                    {
                        // 释放临时 graph 的 COM 对象（顺序：接口 -> filter -> graph builder）
                        if (streamConfigObj != null) Marshal.ReleaseComObject(streamConfigObj);
                        if (sourceFilter != null) Marshal.ReleaseComObject(sourceFilter);
                        if (captureGraphBuilder != null) Marshal.ReleaseComObject(captureGraphBuilder);
                        if (graphBuilder != null) Marshal.ReleaseComObject(graphBuilder);
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile(
                        $"[DirectShow] EnumerateResolutionsAsync 异常: {ex.Message}",
                        LogHelper.LogType.Error);
                }
            });
        }

        /// <summary>
        /// 从 VideoStreamConfigCaps 结构读取 MinFrameInterval/MaxFrameInterval。
        /// 参考 EasiCamera VideoInputService.GetAllAvailableResolution：
        /// 用 Marshal.PtrToStructure 直接读 DirectShowLib 的 VideoStreamConfigCaps 结构，
        /// 不依赖硬编码偏移量（结构实际布局由 DirectShowLib 的 [StructLayout(Pack=2)] 决定，
        /// 手算偏移容易因 LONGLONG 对齐差异出错）。
        /// </summary>
        private static bool TryReadFrameIntervalRange(IntPtr caps, int size, out long minInterval, out long maxInterval)
        {
            minInterval = 0;
            maxInterval = 0;
            if (caps == IntPtr.Zero || size <= 0) return false;

            // 帧率合理范围：1fps (10000000) ~ 200fps (50000)
            const long MinReasonable = 10000;       // 1000fps
            const long MaxReasonable = 100000000;   // 0.1fps

            try
            {
                // 用 DirectShowLib 自带的 VideoStreamConfigCaps 结构（Pack=2）解析。
                // 只有 piSize 与结构大小匹配时才安全。
                if (size != Marshal.SizeOf(typeof(VideoStreamConfigCaps)))
                {
                    // 兜底：枚举常见偏移量（4 字节对齐 / 8 字节对齐）
                    // 实际 native AMVideoStreamConfigCaps 布局（无 Pack）：
                    //   guid(16) + VideoStandard(4) + InputSize(8) + MinCroppingSize(8) +
                    //   MaxCroppingSize(8) + CropGranularity(8) + CropAlign(8) +
                    //   MinOutputSize(8) + MaxOutputSize(8) + OutputGranularity(8) +
                    //   StretchTaps(8) + MinFrameInterval(8) + MaxFrameInterval(8) + ...
                    // 4 字节对齐: MinFrameInterval@92, MaxFrameInterval@100
                    // 8 字节对齐: MinFrameInterval@96, MaxFrameInterval@104
                    int[] minOffsets = { 92, 96 };
                    int[] maxOffsets = { 100, 104 };
                    for (int i = 0; i < minOffsets.Length; i++)
                    {
                        int minOff = minOffsets[i];
                        int maxOff = maxOffsets[i];
                        if (minOff + 8 > size || maxOff + 8 > size) continue;

                        long minVal = Marshal.ReadInt64(caps, minOff);
                        long maxVal = Marshal.ReadInt64(caps, maxOff);

                        if (minVal >= MinReasonable && minVal <= MaxReasonable
                            && maxVal >= minVal && maxVal <= MaxReasonable)
                        {
                            minInterval = minVal;
                            maxInterval = maxVal;
                            return true;
                        }
                    }
                    return false;
                }

                var vsc = (VideoStreamConfigCaps)Marshal.PtrToStructure(caps, typeof(VideoStreamConfigCaps));
                long minVal2 = vsc.MinFrameInterval;
                long maxVal2 = vsc.MaxFrameInterval;

                if (minVal2 >= MinReasonable && minVal2 <= MaxReasonable
                    && maxVal2 >= minVal2 && maxVal2 <= MaxReasonable)
                {
                    minInterval = minVal2;
                    maxInterval = maxVal2;
                    return true;
                }
            }
            catch
            {
                // 解析失败时返回 false，调用方会回退到 AvgTimePerFrame
            }
            return false;
        }

        /// <summary>按 (W, H) 去重，构建 UniqueResolutions 列表。FrameRate 取该分辨率下最大值。
        /// 同时构建 AllResolutionFpsCombos：所有 (W, H, FPS) 去重组合（用于单 ComboBox "W×H@FPS"）。</summary>
        private void RebuildUniqueResolutions()
        {
            var list = (List<ResolutionInfo>)UniqueResolutions;
            list.Clear();

            var comboList = (List<ResolutionInfo>)AllResolutionFpsCombos;
            comboList.Clear();
            var comboKeys = new HashSet<string>();

            foreach (var r in _nativeResolutions)
            {
                // UniqueResolutions：按 (W, H) 去重
                int existingIdx = -1;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].Width == r.Width && list[i].Height == r.Height)
                    {
                        existingIdx = i;
                        break;
                    }
                }
                if (existingIdx < 0)
                {
                    list.Add(new ResolutionInfo
                    {
                        Width = r.Width,
                        Height = r.Height,
                        FrameRate = r.FrameRate
                    });
                }
                else if (r.FrameRate > list[existingIdx].FrameRate)
                {
                    list[existingIdx].FrameRate = r.FrameRate;
                }

                // AllResolutionFpsCombos：按 (W, H, FPS) 去重
                if (r.FrameRate > 0)
                {
                    string key = $"{r.Width}x{r.Height}x{r.FrameRate}";
                    if (comboKeys.Add(key))
                    {
                        comboList.Add(new ResolutionInfo
                        {
                            Width = r.Width,
                            Height = r.Height,
                            FrameRate = r.FrameRate
                        });
                    }
                }
            }

            // 排序：分辨率降序（像素数从大到小），同分辨率内按帧率降序
            comboList.Sort((a, b) =>
            {
                long pixelsA = (long)a.Width * a.Height;
                long pixelsB = (long)b.Width * b.Height;
                if (pixelsA != pixelsB) return pixelsB.CompareTo(pixelsA);
                return b.FrameRate.CompareTo(a.FrameRate);
            });
        }

        /// <summary>把当前选中的 native 分辨率应用到 IAMStreamConfig.SetFormat。</summary>
        private void ApplyNativeResolution()
        {
            try
            {
                if (_sourceFilter == null) return;
                if (_selectedResolutionIndex < 0 || _selectedResolutionIndex >= _nativeResolutions.Count) return;

                var capturePin = DsFindPin.ByCategory(_sourceFilter, PinCategory.Capture, 0);
                var config = capturePin as IAMStreamConfig;
                if (config == null) return;

                int hr = config.GetNumberOfCapabilities(out int count, out int size);
                if (hr != 0 || count <= 0 || size <= 0) return;

                IntPtr caps = Marshal.AllocHGlobal(size);
                try
                {
                    // 注意：_nativeResolutions 已过滤无效项，索引可能与原始 GetStreamCaps 的 i 不对应。
                    // 这里用 width/height 反向匹配。
                    var target = _nativeResolutions[_selectedResolutionIndex];
                    int matchedIndex = -1;

                    for (int i = 0; i < count; i++)
                    {
                        AMMediaType mt = null;
                        try
                        {
                            hr = config.GetStreamCaps(i, out mt, caps);
                            if (hr != 0 || mt == null) continue;
                            if (mt.formatType != FormatType.VideoInfo) continue;
                            if (mt.formatPtr == IntPtr.Zero) continue;

                            var vih = (VideoInfoHeader)Marshal.PtrToStructure(
                                mt.formatPtr, typeof(VideoInfoHeader));

                            if (vih.BmiHeader.Width == target.Width
                                && vih.BmiHeader.Height == target.Height)
                            {
                                matchedIndex = i;
                                break;
                            }
                        }
                        finally
                        {
                            if (mt != null) DsUtils.FreeAMMediaType(mt);
                        }
                    }

                    if (matchedIndex < 0)
                    {
                        LogHelper.WriteLogToFile(
                            $"[DirectShow] ApplyNativeResolution 找不到匹配的 {target.Width}x{target.Height}",
                            LogHelper.LogType.Warning);
                        return;
                    }

                    AMMediaType applyMt = null;
                    try
                    {
                        hr = config.GetStreamCaps(matchedIndex, out applyMt, caps);
                        DsError.ThrowExceptionForHR(hr);
                        hr = config.SetFormat(applyMt);
                        DsError.ThrowExceptionForHR(hr);
                    }
                    finally
                    {
                        if (applyMt != null) DsUtils.FreeAMMediaType(applyMt);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(caps);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[DirectShow] 应用 native 分辨率失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private async Task RestartWithNewResolutionAsync()
        {
            try
            {
                var idx = _cameras.IndexOf(CurrentCamera);
                if (idx < 0 && _cameras.Count > 0) idx = 0;
                if (idx < 0) return;

                // 保留 _selectedResolutionIndex，StopPreview 不清空 _nativeResolutions
                var selIdx = _selectedResolutionIndex;
                await StopPreviewAsyncInternal();
                _selectedResolutionIndex = selIdx; // StopPreview 不动这个，但保险起见恢复
                await StartPreviewAsync(idx);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[DirectShow] RestartWithNewResolutionAsync 失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private Task StopPreviewAsyncInternal()
        {
            try
            {
                if (_mediaControl != null)
                {
                    try { _mediaControl.Stop(); } catch { }
                }
                CleanupGraph();
                _isCapturing = false;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[DirectShow] StopPreviewAsyncInternal 失败: {ex.Message}", LogHelper.LogType.Error);
            }
            return Task.CompletedTask;
        }

        /// <summary>停止预览并清理 DirectShow 图。</summary>
        public void StopPreview()
        {
            try
            {
                // 先标记停止，让 BufferCB 立即短路返回，避免访问正在被释放的 COM 对象
                _isCapturing = false;

                if (_mediaControl != null)
                {
                    try { _mediaControl.Stop(); } catch { }
                }
                CleanupGraph();

                lock (_frameLock)
                {
                    _currentFrame?.Dispose();
                    _currentFrame = null;
                }

                // 清理复用缓冲区
                _reusableBitmap = null;
                _reusableBuffer = null;
                _sourceStride = 0;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[DirectShow] 停止摄像头预览失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        private void CleanupGraph()
        {
            // DirectShow COM 对象释放顺序：先停图，再释放 filter 引用
            if (_filterGraph != null)
            {
                try
                {
                    // 注意：_mediaControl 与 _filterGraph 是同一 RCW（不同接口 cast），只释放一次。
                    // 先断开 sample grabber 回调，避免释放过程中触发
                    if (_sampleGrabber != null)
                    {
                        try { _sampleGrabber.SetCallback(null, 0); } catch { }
                    }

                    // 释放子 filter RCW（每个是独立的 RCW）
                    if (_sourceFilter != null)
                    {
                        Marshal.ReleaseComObject(_sourceFilter);
                        _sourceFilter = null;
                    }
                    if (_sampleGrabber != null)
                    {
                        Marshal.ReleaseComObject(_sampleGrabber);
                        _sampleGrabber = null;
                    }
                    if (_sampleGrabberFilter != null)
                    {
                        Marshal.ReleaseComObject(_sampleGrabberFilter);
                        _sampleGrabberFilter = null;
                    }
                    if (_nullRenderer != null)
                    {
                        Marshal.ReleaseComObject(_nullRenderer);
                        _nullRenderer = null;
                    }

                    // _mediaControl 与 _filterGraph 共享 RCW，置 null 避免重复释放
                    _mediaControl = null;
                    Marshal.ReleaseComObject(_filterGraph);
                    _filterGraph = null;
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"[DirectShow] CleanupGraph 异常: {ex.Message}", LogHelper.LogType.Warning);
                }
            }
            else
            {
                // 兜底：万一 _filterGraph 已 null 但其他字段还有引用
                _mediaControl = null;
                _sourceFilter = null;
                _sampleGrabber = null;
                _sampleGrabberFilter = null;
                _nullRenderer = null;
            }
        }

        // ====================================================================
        // ISampleGrabberCB 实现
        // ====================================================================

        /// <summary>SampleCB 不会被调用（SetCallback 用了 1，走 BufferCB 路径）。</summary>
        public int SampleCB(double sampleTime, IMediaSample sample)
        {
            return 0;
        }

        /// <summary>BufferCB：每帧由 DirectShow 在流线程上回调，buffer 指向 RGB24 像素数据。</summary>
        public int BufferCB(double sampleTime, IntPtr buffer, int bufferLen)
        {
            try
            {
                if (!_isCapturing || buffer == IntPtr.Zero || bufferLen <= 0) return 0;

                // 从 sample grabber 拿协商出的媒体类型，得到当前 width/height
                int width, height, stride;
                if (!TryGetNegotiatedVideoFormat(out width, out height, out stride)) return 0;

                bool needRotate = _rotationAngle != 0;
                int srcWidth = width;
                int srcHeight = height;
                // stride 应至少容纳一行 RGB24 数据
                int actualStride = stride > 0 ? stride : (width * 3 + 3) & ~3;
                _sourceStride = actualStride;

                // === 1. 复制到托管缓冲区（DirectShow 工作线程上操作）===
                int requiredSize = actualStride * height;
                if (_reusableBuffer == null || _reusableBuffer.Length < requiredSize)
                {
                    _reusableBuffer = new byte[requiredSize];
                }
                Marshal.Copy(buffer, _reusableBuffer, 0, Math.Min(bufferLen, requiredSize));

                // 拍贝一份给 _currentFrame（用于拍照路径，需可旋转）
                // 注意：每次都 Clone 会产生 GC 压力，但拍照频率较低可接受
                Bitmap snapshot = BuildBitmapFromBuffer(_reusableBuffer, srcWidth, srcHeight, actualStride);

                if (snapshot != null)
                {
                    if (needRotate)
                    {
                        var rotationType = _rotationAngle switch
                        {
                            1 => RotateFlipType.Rotate90FlipNone,
                            2 => RotateFlipType.Rotate180FlipNone,
                            3 => RotateFlipType.Rotate270FlipNone,
                            _ => RotateFlipType.RotateNoneFlipNone
                        };
                        snapshot.RotateFlip(rotationType);
                    }

                    lock (_frameLock)
                    {
                        _currentFrame?.Dispose();
                        _currentFrame = snapshot;
                    }
                }

                // === 2. 通知 UI 线程更新 WriteableBitmap / BitmapSource ===
                byte[] bufferCopy = _reusableBuffer;
                int bw = srcWidth, bh = srcHeight, bstride = actualStride;
                bool rotate = needRotate;
                int rotation = _rotationAngle;

                _dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        BitmapSource frameSource;
                        if (!rotate)
                        {
                            // 快路径：直接用复用的 WriteableBitmap（Bgr24，3 字节/像素）
                            if (_reusableBitmap == null
                                || _reusableBitmap.PixelWidth != bw
                                || _reusableBitmap.PixelHeight != bh)
                            {
                                _reusableBitmap = new WriteableBitmap(
                                    bw, bh,
                                    96.0, 96.0,
                                    System.Windows.Media.PixelFormats.Bgr24,
                                    null);
                            }
                            _reusableBitmap.WritePixels(
                                new System.Windows.Int32Rect(0, 0, bw, bh),
                                bufferCopy,
                                bstride,
                                0);
                            frameSource = _reusableBitmap;
                        }
                        else
                        {
                            // 慢路径：旋转后用 BitmapSource.Create（每帧重建，但仅在用户启用旋转时）
                            int rotW, rotH;
                            if (rotation == 1 || rotation == 3)
                            {
                                rotW = bh; rotH = bw;
                            }
                            else
                            {
                                rotW = bw; rotH = bh;
                            }

                            // 用 BuildBitmapFromBuffer 构建 24bppRgb Bitmap → RotateFlip → LockBits → BitmapSource.Create
                            using (var srcBmp = BuildBitmapFromBuffer(bufferCopy, bw, bh, bstride))
                            {
                                var rotType = rotation switch
                                {
                                    1 => RotateFlipType.Rotate90FlipNone,
                                    2 => RotateFlipType.Rotate180FlipNone,
                                    3 => RotateFlipType.Rotate270FlipNone,
                                    _ => RotateFlipType.RotateNoneFlipNone
                                };
                                srcBmp.RotateFlip(rotType);

                                var data = srcBmp.LockBits(
                                    new Rectangle(0, 0, srcBmp.Width, srcBmp.Height),
                                    ImageLockMode.ReadOnly,
                                    PixelFormat.Format24bppRgb);
                                try
                                {
                                    frameSource = BitmapSource.Create(
                                        rotW, rotH,
                                        96.0, 96.0,
                                        System.Windows.Media.PixelFormats.Bgr24,
                                        null,
                                        data.Scan0,
                                        data.Stride * rotH,
                                        data.Stride);
                                    frameSource.Freeze();
                                }
                                finally
                                {
                                    srcBmp.UnlockBits(data);
                                }
                            }
                        }

                        FrameReceived?.Invoke(this, new FrameEventArgs { Frame = frameSource });
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile($"[DirectShow] FrameReceived 分发失败: {ex.Message}", LogHelper.LogType.Error);
                    }
                }));
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[DirectShow] BufferCB 失败: {ex.Message}", LogHelper.LogType.Error);
            }
            return 0;
        }

        /// <summary>从 byte[] 构建 Bitmap（24bppRgb，按行复制以处理 stride 不一致）。</summary>
        private static Bitmap BuildBitmapFromBuffer(byte[] buffer, int width, int height, int srcStride)
        {
            try
            {
                var bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
                var data = bmp.LockBits(
                    new Rectangle(0, 0, width, height),
                    ImageLockMode.WriteOnly,
                    PixelFormat.Format24bppRgb);
                try
                {
                    int dstStride = data.Stride;
                    int rowBytes = width * 3;
                    if (srcStride == dstStride)
                    {
                        Marshal.Copy(buffer, 0, data.Scan0, srcStride * height);
                    }
                    else
                    {
                        for (int y = 0; y < height; y++)
                        {
                            Marshal.Copy(buffer, y * srcStride,
                                (IntPtr)(data.Scan0.ToInt64() + y * dstStride), rowBytes);
                        }
                    }
                }
                finally
                {
                    bmp.UnlockBits(data);
                }
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>从 SampleGrabber 读取协商出的媒体类型，得到 width/height/stride。</summary>
        private bool TryGetNegotiatedVideoFormat(out int width, out int height, out int stride)
        {
            width = height = stride = 0;
            try
            {
                if (_sampleGrabber == null) return false;

                var mt = new AMMediaType();
                int hr = _sampleGrabber.GetConnectedMediaType(mt);
                if (hr != 0) return false;

                try
                {
                    if (mt.formatType != FormatType.VideoInfo || mt.formatPtr == IntPtr.Zero)
                        return false;

                    var vih = (VideoInfoHeader)Marshal.PtrToStructure(mt.formatPtr, typeof(VideoInfoHeader));
                    width = vih.BmiHeader.Width;
                    height = vih.BmiHeader.Height;
                    // BMIH BiSizeImage 通常是 stride * height；stride 至少是 width * 3，并对齐到 4 字节
                    int computed = (width * 3 + 3) & ~3;
                    stride = computed;
                    if (width <= 0 || height <= 0) return false;
                    return true;
                }
                finally
                {
                    DsUtils.FreeAMMediaType(mt);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[DirectShow] TryGetNegotiatedVideoFormat 失败: {ex.Message}", LogHelper.LogType.Warning);
                return false;
            }
        }

        // ====================================================================
        // 获取当前帧
        // ====================================================================

        /// <summary>获取当前帧的 WPF 位图（已 Freeze，可跨线程）。</summary>
        public BitmapSource GetCurrentFrameAsBitmapSource()
        {
            lock (_frameLock)
            {
                if (_currentFrame == null) return null;
                try
                {
                    return BitmapToBitmapSource(_currentFrame);
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>获取当前帧的 GDI+ Bitmap 副本（调用方负责 Dispose）。</summary>
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

        private static BitmapSource BitmapToBitmapSource(Bitmap bmp)
        {
            if (bmp == null || bmp.Width <= 0 || bmp.Height <= 0) return null;

            var bitmapData = bmp.LockBits(
                new Rectangle(0, 0, bmp.Width, bmp.Height),
                ImageLockMode.ReadOnly,
                bmp.PixelFormat);
            try
            {
                System.Windows.Media.PixelFormat wpfPixelFormat = bmp.PixelFormat switch
                {
                    PixelFormat.Format24bppRgb => System.Windows.Media.PixelFormats.Bgr24,
                    PixelFormat.Format32bppArgb => System.Windows.Media.PixelFormats.Bgra32,
                    PixelFormat.Format32bppRgb => System.Windows.Media.PixelFormats.Bgr32,
                    _ => System.Windows.Media.PixelFormats.Bgr24,
                };

                var bs = BitmapSource.Create(
                    bitmapData.Width,
                    bitmapData.Height,
                    bmp.HorizontalResolution,
                    bmp.VerticalResolution,
                    wpfPixelFormat,
                    null,
                    bitmapData.Scan0,
                    bitmapData.Stride * bitmapData.Height,
                    bitmapData.Stride);
                bs.Freeze();
                return bs;
            }
            finally
            {
                bmp.UnlockBits(bitmapData);
            }
        }

        public void Dispose()
        {
            StopPreview();
        }
    }
}
