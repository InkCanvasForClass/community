using Ink_Canvas.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 已注册到宿主的插件摄像头（camera provider）。
    /// 一个 <see cref="IPluginCameraProvider"/> 声明 N 个 <see cref="PluginCameraDescriptor"/>，
    /// 每个描述对应本类型的一个实例。
    /// </summary>
    public sealed class ProviderCameraRegistration
    {
        internal ProviderCameraRegistration(IPluginCameraProvider provider, PluginCameraDescriptor descriptor)
        {
            Provider = provider;
            Descriptor = descriptor;
            Id = descriptor.Id;
            Key = BuildKey(provider.ProviderId, descriptor.Id);
        }

        /// <summary>提供者实现。</summary>
        public IPluginCameraProvider Provider { get; }

        /// <summary>摄像头描述（插件在 GetCameras 中返回的对象）。</summary>
        public PluginCameraDescriptor Descriptor { get; }

        /// <summary>提供者 Id（<see cref="IPluginCameraProvider.ProviderId"/>）。</summary>
        public string ProviderId => Provider?.ProviderId ?? "";

        /// <summary>摄像头 Id（<see cref="PluginCameraDescriptor.Id"/>）。</summary>
        public string Id { get; }

        /// <summary>宿主内部使用的唯一键（用于 CameraInfo.MonikerString 与日志）。</summary>
        public string Key { get; }

        /// <summary>下拉框显示名；插件未指定时回退到摄像头 Id。</summary>
        public string DisplayName =>
            string.IsNullOrWhiteSpace(Descriptor?.DisplayName) ? Id : Descriptor.DisplayName;

        /// <summary>声明的采集能力列表（可能为空）。</summary>
        public IReadOnlyList<PluginCameraCapability> Capabilities =>
            Descriptor?.Capabilities as IReadOnlyList<PluginCameraCapability>
            ?? (IReadOnlyList<PluginCameraCapability>)Array.Empty<PluginCameraCapability>();

        /// <summary>是否支持摄像头属性调节（当前宿主版本对插件摄像头不做属性映射）。</summary>
        public bool SupportsPropertyControl => Descriptor?.SupportsPropertyControl == true;

        /// <summary>拼装宿主内部唯一键。</summary>
        public static string BuildKey(string providerId, string cameraId) =>
            "plugin-camera://" + (providerId ?? "") + "/" + (cameraId ?? "");
    }

    /// <summary>
    /// 插件摄像头提供者注册表（单例）。
    /// <para>职责：</para>
    /// <list type="bullet">
    /// <item>保存插件注册的摄像头，供 <c>DirectShowCameraService.RefreshCameraListAsync</c>
    /// 合并进视频展台下拉框；</item>
    /// <item>作为 <see cref="IPluginCameraFrameSink"/> 接收插件推来的 BGR24 帧，
    /// 维护"最近一帧"并通知订阅者；</item>
    /// <item>管理当前唯一一路活动推流（启动 / 停止插件侧采集、调用 SetCameraFormat）。</item>
    /// </list>
    /// <para>设计取舍：只保留最近一帧、双缓冲交换，避免插件推帧时分配大数组；
    /// 帧数据在锁内拷贝/读取，保证 UI 线程渲染与插件线程提交不会撕裂。</para>
    /// </summary>
    public sealed class CameraProviderRegistry : IPluginCameraFrameSink
    {
        /// <summary>全局单例。宿主各模块（摄像头服务、视频展台、插件服务）共用同一实例。</summary>
        public static CameraProviderRegistry Instance { get; } = new CameraProviderRegistry();

        private readonly object _listLock = new object();
        private readonly object _frameLock = new object();
        private readonly List<ProviderCameraRegistration> _registrations = new List<ProviderCameraRegistration>();

        // 当前活动推流（同一时刻最多一路）
        private ProviderCameraRegistration _activeCamera;
        private bool _activeStarted;

        // 最近一帧（双缓冲：Submit 写 _frameBackBuffer，交换后 _frameFrontBuffer 供读取）
        private byte[] _frameFrontBuffer;
        private byte[] _frameBackBuffer;
        private int _frameWidth;
        private int _frameHeight;
        private int _frameStride;
        private long _frameTimestampMs;
        private long _frameSequence;

        /// <summary>摄像头列表发生变化（注册/注销/插件通知）时触发。可能在任意线程触发，UI 使用需自行切线程。</summary>
        public event EventHandler CamerasChanged;

        /// <summary>收到一帧时触发（在插件推帧线程上同步触发，回调必须快速返回，不要在这里做渲染）。</summary>
        public event EventHandler FrameSubmitted;

        /// <summary>已注册的摄像头快照（线程安全，返回副本）。</summary>
        public IReadOnlyList<ProviderCameraRegistration> Cameras
        {
            get
            {
                lock (_listLock)
                {
                    return _registrations.ToArray();
                }
            }
        }

        /// <summary>当前正在推流的摄像头键；没有则为 null。</summary>
        public string ActiveCameraKey
        {
            get
            {
                lock (_listLock)
                {
                    return _activeCamera?.Key;
                }
            }
        }

        /// <summary>按 (providerId, cameraId) 查找已注册摄像头；找不到返回 null。</summary>
        public ProviderCameraRegistration Find(string providerId, string cameraId)
        {
            if (string.IsNullOrEmpty(providerId) || string.IsNullOrEmpty(cameraId)) return null;
            lock (_listLock)
            {
                return _registrations.FirstOrDefault(r =>
                    string.Equals(r.ProviderId, providerId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(r.Id, cameraId, StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <summary>按内部键查找已注册摄像头；找不到返回 null。</summary>
        public ProviderCameraRegistration FindByKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            lock (_listLock)
            {
                return _registrations.FirstOrDefault(r =>
                    string.Equals(r.Key, key, StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <summary>
        /// 注册/覆盖一个摄像头提供者：移除它此前声明的全部摄像头后，按 GetCameras()
        /// 重新加入，并触发 <see cref="CamerasChanged"/>。
        /// </summary>
        public void Register(IPluginCameraProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            string providerId = provider.ProviderId;
            if (string.IsNullOrWhiteSpace(providerId))
                throw new ArgumentException("IPluginCameraProvider.ProviderId 不能为空", nameof(provider));

            IReadOnlyList<PluginCameraDescriptor> descriptors = null;
            try
            {
                descriptors = provider.GetCameras();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile(
                    $"[CameraProvider] 读取提供者 {providerId} 的摄像头列表失败: {ex.Message}",
                    LogHelper.LogType.Error);
            }

            lock (_listLock)
            {
                _registrations.RemoveAll(r =>
                    string.Equals(r.ProviderId, providerId, StringComparison.OrdinalIgnoreCase));
                if (descriptors != null)
                {
                    foreach (var descriptor in descriptors)
                    {
                        if (descriptor == null || string.IsNullOrWhiteSpace(descriptor.Id))
                        {
                            LogHelper.WriteLogToFile(
                                $"[CameraProvider] 提供者 {providerId} 返回了缺少 Id 的摄像头描述，已忽略",
                                LogHelper.LogType.Warning);
                            continue;
                        }
                        _registrations.Add(new ProviderCameraRegistration(provider, descriptor));
                    }
                }
            }

            LogHelper.WriteLogToFile(
                $"[CameraProvider] 注册提供者 {providerId}，摄像头数={descriptors?.Count ?? 0}",
                LogHelper.LogType.Info);
            RaiseCamerasChanged();
        }

        /// <summary>注销指定提供者：停止其活动推流并移除它声明的全部摄像头。</summary>
        public void Unregister(IPluginCameraProvider provider)
        {
            if (provider == null) return;
            UnregisterByProviderId(provider.ProviderId);
        }

        /// <summary>
        /// 按提供者 Id 注销（宿主在插件卸载时按 manifest.Id 清理，避免插件不调用
        /// <see cref="ICameraProviderService.UnregisterProvider"/> 造成残留）。
        /// </summary>
        public void UnregisterByProviderId(string providerId)
        {
            if (string.IsNullOrWhiteSpace(providerId)) return;

            bool removed;
            lock (_listLock)
            {
                removed = _registrations.RemoveAll(r =>
                    string.Equals(r.ProviderId, providerId, StringComparison.OrdinalIgnoreCase)) > 0;
                // 活动推流属于该提供者时一并停止（StopActiveCamera 内部会调用插件 StopCamera）
                if (_activeCamera != null &&
                    string.Equals(_activeCamera.ProviderId, providerId, StringComparison.OrdinalIgnoreCase))
                {
                    removed = true;
                }
            }

            if (!removed) return;
            StopActiveCamera();
            LogHelper.WriteLogToFile(
                $"[CameraProvider] 注销提供者 {providerId} 的全部摄像头",
                LogHelper.LogType.Info);
            RaiseCamerasChanged();
        }

        /// <summary>插件通知摄像头列表变化：重新从提供者收取列表并触发刷新。</summary>
        public void NotifyCamerasChanged(IPluginCameraProvider provider)
        {
            if (provider == null) return;
            Register(provider);
        }

        /// <summary>
        /// 启动指定摄像头的推流。会先停止上一路活动推流。
        /// 本方法会同步调用插件 <see cref="IPluginCameraProvider.StartCamera"/>，请勿在 UI 线程直接调用
        /// （宿主内部用 Task.Run 包一层）。
        /// </summary>
        /// <param name="camera">已注册摄像头。</param>
        /// <param name="width">期望宽度；&lt;=0 表示不指定。</param>
        /// <param name="height">期望高度；&lt;=0 表示不指定。</param>
        /// <param name="frameRate">期望帧率；&lt;=0 时按 30 传。</param>
        /// <param name="error">失败原因（成功时为 null）。</param>
        public bool TryStartCamera(ProviderCameraRegistration camera, int width, int height, int frameRate, out string error)
        {
            error = null;
            if (camera == null)
            {
                error = "摄像头未注册或已被移除";
                return false;
            }

            // 上一路先停干净（包括旧帧，避免切摄像头瞬间串台）
            StopActiveCamera();

            lock (_listLock)
            {
                _activeCamera = camera;
                _activeStarted = false;
            }

            bool ok;
            try
            {
                ok = camera.Provider.StartCamera(camera.Id, this);
            }
            catch (Exception ex)
            {
                ok = false;
                error = ex.Message;
                LogHelper.WriteLogToFile(
                    $"[CameraProvider] 提供者 {camera.ProviderId} 启动摄像头 {camera.Id} 异常: {ex.Message}",
                    LogHelper.LogType.Error);
            }

            if (!ok)
            {
                lock (_listLock)
                {
                    if (ReferenceEquals(_activeCamera, camera)) _activeCamera = null;
                    _activeStarted = false;
                }
                if (string.IsNullOrEmpty(error)) error = "插件拒绝了该摄像头的启动请求";
                return false;
            }

            lock (_listLock)
            {
                _activeStarted = true;
            }

            // 分辨率：先启动再通知格式，插件既可在启动时使用，也可在运行中即时切换
            if (width > 0 && height > 0)
            {
                TryApplyCameraFormat(camera, width, height, frameRate);
            }

            LogHelper.WriteLogToFile(
                $"[CameraProvider] 已启动插件摄像头 {camera.DisplayName}（{camera.Key}），请求格式={width}×{height}@{frameRate}",
                LogHelper.LogType.Info);
            return true;
        }

        /// <summary>向当前活动摄像头请求切换采集格式；未启动或不支持时返回 false。</summary>
        public bool TryApplyCameraFormat(int width, int height, int frameRate)
        {
            ProviderCameraRegistration camera;
            lock (_listLock)
            {
                camera = _activeStarted ? _activeCamera : null;
            }
            return TryApplyCameraFormat(camera, width, height, frameRate);
        }

        private bool TryApplyCameraFormat(ProviderCameraRegistration camera, int width, int height, int frameRate)
        {
            if (camera == null || width <= 0 || height <= 0) return false;
            try
            {
                return camera.Provider.SetCameraFormat(camera.Id, width, height, frameRate > 0 ? frameRate : 30);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile(
                    $"[CameraProvider] 提供者 {camera.ProviderId} 切换摄像头 {camera.Id} 格式失败: {ex.Message}",
                    LogHelper.LogType.Warning);
                return false;
            }
        }

        /// <summary>
        /// 停止当前活动推流：清空最近帧、通知插件 StopCamera。
        /// 幂等；没有活动推流时为空操作。
        /// </summary>
        public void StopActiveCamera()
        {
            ProviderCameraRegistration camera;
            lock (_listLock)
            {
                camera = _activeCamera;
                _activeCamera = null;
                _activeStarted = false;
            }

            lock (_frameLock)
            {
                _frameFrontBuffer = null;
                _frameBackBuffer = null;
                _frameWidth = 0;
                _frameHeight = 0;
                _frameStride = 0;
                _frameSequence = 0;
            }

            if (camera == null) return;

            try
            {
                camera.Provider.StopCamera(camera.Id);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile(
                    $"[CameraProvider] 提供者 {camera.ProviderId} 停止摄像头 {camera.Id} 异常: {ex.Message}",
                    LogHelper.LogType.Error);
            }

            LogHelper.WriteLogToFile(
                $"[CameraProvider] 已停止插件摄像头 {camera.DisplayName}（{camera.Key}）",
                LogHelper.LogType.Info);
        }

        private void RaiseCamerasChanged()
        {
            try
            {
                CamerasChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile(
                    $"[CameraProvider] CamerasChanged 事件处理异常: {ex.Message}",
                    LogHelper.LogType.Warning);
            }
        }

        // ====================================================================
        // IPluginCameraFrameSink：接收插件推来的帧
        // ====================================================================

        /// <inheritdoc/>
        public bool IsActive
        {
            get
            {
                lock (_listLock)
                {
                    return _activeStarted && _activeCamera != null;
                }
            }
        }

        /// <inheritdoc/>
        public bool Submit(byte[] bgr24Buffer, int width, int height, int stride, long timestampMs = 0L)
        {
            if (bgr24Buffer == null || width <= 0 || height <= 0) return false;
            int minStride = width * 3;
            if (stride < minStride) return false;
            long needed = (long)stride * height;
            if (bgr24Buffer.LongLength < needed) return false;

            lock (_frameLock)
            {
                lock (_listLock)
                {
                    if (!_activeStarted || _activeCamera == null) return false;
                }

                if (_frameBackBuffer == null || _frameBackBuffer.Length < needed)
                    _frameBackBuffer = new byte[(int)needed];

                // 两个缓冲都按当前帧需要的长度使用：更长的旧缓冲只用前 needed 字节
                Buffer.BlockCopy(bgr24Buffer, 0, _frameBackBuffer, 0, (int)needed);
                var swap = _frameFrontBuffer;
                _frameFrontBuffer = _frameBackBuffer;
                _frameBackBuffer = swap;
                _frameWidth = width;
                _frameHeight = height;
                _frameStride = stride;
                _frameTimestampMs = timestampMs;
                _frameSequence++;
            }

            try
            {
                FrameSubmitted?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile(
                    $"[CameraProvider] FrameSubmitted 事件处理异常: {ex.Message}",
                    LogHelper.LogType.Warning);
            }
            return true;
        }

        // ====================================================================
        // 帧读取（消费侧：视频展台 UI / 摄像头服务）
        // ====================================================================

        /// <summary>读取最近一帧的尺寸信息；没有可用帧时返回 false。</summary>
        public bool TryGetLatestFrameInfo(out int width, out int height, out int stride, out long timestampMs)
        {
            lock (_frameLock)
            {
                width = _frameWidth;
                height = _frameHeight;
                stride = _frameStride;
                timestampMs = _frameTimestampMs;
                return _frameFrontBuffer != null && _frameWidth > 0 && _frameHeight > 0;
            }
        }

        /// <summary>
        /// 把最近一帧写入目标 <see cref="WriteableBitmap"/>（必须在创建该位图的 UI 线程调用）。
        /// 尺寸或像素格式不匹配时返回 false。
        /// </summary>
        public bool RenderLatestFrame(WriteableBitmap target)
        {
            if (target == null) return false;
            lock (_frameLock)
            {
                if (_frameFrontBuffer == null || _frameWidth <= 0 || _frameHeight <= 0) return false;
                if (target.PixelWidth != _frameWidth || target.PixelHeight != _frameHeight) return false;
                if (target.Format != PixelFormats.Bgr24) return false;
                int needed = _frameStride * _frameHeight;
                if (_frameFrontBuffer.Length < needed) return false;
                target.WritePixels(
                    new Int32Rect(0, 0, _frameWidth, _frameHeight),
                    _frameFrontBuffer,
                    _frameStride,
                    0);
                return true;
            }
        }

        /// <summary>
        /// 取最近一帧的冻结副本（用于拍照）。返回的位图可跨线程使用；没有可用帧时返回 null。
        /// </summary>
        public BitmapSource GetLatestFrameBitmapSource()
        {
            lock (_frameLock)
            {
                if (_frameFrontBuffer == null || _frameWidth <= 0 || _frameHeight <= 0) return null;
                int needed = _frameStride * _frameHeight;
                if (_frameFrontBuffer.Length < needed) return null;
                try
                {
                    var bitmap = BitmapSource.Create(
                        _frameWidth,
                        _frameHeight,
                        96,
                        96,
                        PixelFormats.Bgr24,
                        null,
                        _frameFrontBuffer,
                        _frameStride);
                    bitmap.Freeze();
                    return bitmap;
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile(
                        $"[CameraProvider] 取最近帧失败: {ex.Message}",
                        LogHelper.LogType.Warning);
                    return null;
                }
            }
        }

        /// <summary>把最近一帧复制为独立的 BGR24 缓冲区（用于非 WPF 调用方）。</summary>
        public bool TryCopyLatestFrame(out byte[] buffer, out int width, out int height, out int stride)
        {
            buffer = null;
            lock (_frameLock)
            {
                width = _frameWidth;
                height = _frameHeight;
                stride = _frameStride;
                if (_frameFrontBuffer == null || _frameWidth <= 0 || _frameHeight <= 0) return false;
                int needed = _frameStride * _frameHeight;
                if (_frameFrontBuffer.Length < needed) return false;
                var copy = new byte[needed];
                Buffer.BlockCopy(_frameFrontBuffer, 0, copy, 0, needed);
                buffer = copy;
                return true;
            }
        }

        /// <summary>最近一帧的序号（每次成功 Submit 自增，可用于诊断帧率）。</summary>
        public long FrameSequence
        {
            get
            {
                lock (_frameLock)
                {
                    return _frameSequence;
                }
            }
        }
    }
}
