using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 摄像头服务：供插件枚举摄像头、启动预览、接收帧回调与拍照。
    /// <para>底层复用宿主视频展台的 DirectShow 采集（<c>CameraServiceFactory.Create()</c>），
    /// 与展台共用摄像头设备——插件启动预览可能抢占展台正在使用的设备。</para>
    /// <para>帧回调在后台线程触发，返回的 <see cref="BitmapSource"/> 已 Freeze。</para>
    /// </summary>
    public interface ICameraService
    {
        /// <summary>每收到一帧时触发（参数为已 Freeze 的位图）。</summary>
        event Action<BitmapSource> FrameReceived;

        /// <summary>当前是否正在预览。</summary>
        bool IsCapturing { get; }

        /// <summary>可用摄像头列表。</summary>
        IReadOnlyList<PluginCameraInfo> AvailableCameras { get; }

        /// <summary>旋转角度（0=0°, 1=90°, 2=180°, 3=270°）。</summary>
        int RotationAngle { get; set; }

        /// <summary>当前摄像头支持的 native 分辨率列表。</summary>
        IReadOnlyList<PluginResolutionInfo> NativeResolutions { get; }

        /// <summary>当前选中的 native 分辨率索引（<see cref="NativeResolutions"/> 的索引）；-1 未选中。</summary>
        int SelectedResolutionIndex { get; set; }

        /// <summary>刷新可用摄像头列表。</summary>
        Task RefreshCameraListAsync();

        /// <summary>启动指定摄像头（索引来自 <see cref="AvailableCameras"/>）的预览。返回是否成功。</summary>
        Task<bool> StartPreviewAsync(int cameraIndex);

        /// <summary>停止预览。</summary>
        void StopPreview();

        /// <summary>获取当前帧位图（已 Freeze），用于拍照。</summary>
        BitmapSource GetCurrentFrame();
    }

    /// <summary>
    /// 摄像头信息。
    /// </summary>
    public sealed class PluginCameraInfo
    {
        /// <summary>摄像头名称。</summary>
        public string Name { get; set; } = "";

        /// <summary>设备 Moniker 标识（用于 <see cref="ICameraService.StartPreviewAsync"/>）。</summary>
        public string MonikerString { get; set; } = "";
    }

    /// <summary>
    /// 摄像头分辨率（宽×高×帧率）。
    /// </summary>
    public sealed class PluginResolutionInfo
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int FrameRate { get; set; }
    }
}
