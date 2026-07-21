using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 视频展台摄像头服务的抽象接口。
    /// 当前实现：<see cref="DirectShowCameraService"/>（基于 DirectShowLib FilterGraph + SampleGrabber）。
    /// </summary>
    public interface ICameraService : IDisposable
    {
        /// <summary>每收到一帧时触发（参数为已 Freeze 的 BitmapSource，或复用的 WriteableBitmap）。</summary>
        event EventHandler<FrameEventArgs> FrameReceived;

        /// <summary>发生错误时触发，参数为错误描述。</summary>
        event EventHandler<string> ErrorOccurred;

        bool IsCapturing { get; }
        IReadOnlyList<CameraInfo> AvailableCameras { get; }
        CameraInfo CurrentCamera { get; }

        /// <summary>0=0°, 1=90°, 2=180°, 3=270°。</summary>
        int RotationAngle { get; set; }

        /// <summary>当前摄像头支持的 native 分辨率列表（W,H,FPS 组合；可能为空）。</summary>
        IReadOnlyList<ResolutionInfo> NativeResolutions { get; }

        /// <summary>当前选中的 native 分辨率索引（NativeResolutions 的索引）；-1 表示未选中。</summary>
        int SelectedResolutionIndex { get; set; }

        /// <summary>
        /// 静默更新 SelectedResolutionIndex（不触发 RestartWithNewResolutionAsync）。
        /// 用于特殊模式下 VideoCaptureElement 接管预览时，_cameraService 不应抢占摄像头设备。
        /// 调用者负责后续重新启动 VideoCaptureElement 预览。
        /// </summary>
        void SetSelectedResolutionIndexSilent(int value);

        /// <summary>
        /// 去重后的分辨率列表（同 W,H 合并；FrameRate 取该分辨率下最大值）。
        /// 用于分辨率 ComboBox 填充。
        /// </summary>
        IReadOnlyList<ResolutionInfo> UniqueResolutions { get; }

        /// <summary>
        /// 所有有效的 (W, H, FPS) 组合（去重）。
        /// 排序：先按分辨率降序（像素数从大到小），同分辨率内按帧率降序。
        /// 用于单 ComboBox 填充"分辨率@帧数"组合选项。
        /// </summary>
        IReadOnlyList<ResolutionInfo> AllResolutionFpsCombos { get; }

        /// <summary>获取指定分辨率下支持的帧率列表（去重、降序）。</summary>
        IReadOnlyList<int> GetFrameratesFor(int width, int height);

        /// <summary>
        /// 在 NativeResolutions 中查找匹配 (W, H, FPS) 的 capability 索引。
        /// 若找不到精确匹配，退回到同 (W, H) 下最接近的 FPS。
        /// </summary>
        int FindCapabilityIndex(int width, int height, int framerate);

        /// <summary>当前选中的"去重分辨率索引"；-1 表示未选中。</summary>
        int SelectedUniqueResolutionIndex { get; set; }

        /// <summary>当前分辨率下的帧率索引（GetFrameratesFor 返回列表的索引）；-1 表示未选中。</summary>
        int SelectedFramerateIndex { get; set; }

        /// <summary>当前在 AllResolutionFpsCombos 中的选中索引；-1 表示未选中。</summary>
        int SelectedComboIndex { get; set; }

        /// <summary>
        /// 刷新可用摄像头列表。返回 Task 以便调用方 await。
        /// 调用完成后 <see cref="AvailableCameras"/> 已就绪。
        /// </summary>
        Task RefreshCameraListAsync();

        /// <summary>
        /// 独立枚举指定摄像头的 native 分辨率（不启动预览，不抢占设备）。
        /// 用 FilterGraphNoThread + ICaptureGraphBuilder2 + AddSourceFilterForMoniker
        /// 枚举 IAMStreamConfig.GetStreamCaps，不调用 IMediaControl.Run()。
        /// 用于特殊模式下：先用此方法填充分辨率 ComboBox，再启动 VideoCaptureElement 预览。
        /// 调用完成后 NativeResolutions / UniqueResolutions / SelectedResolutionIndex /
        /// SelectedUniqueResolutionIndex / SelectedFramerateIndex / CurrentCamera 均已就绪。
        /// </summary>
        Task EnumerateResolutionsAsync(int cameraIndex);

        /// <summary>启动指定摄像头的预览。会刷新 NativeResolutions。</summary>
        Task<bool> StartPreviewAsync(int cameraIndex);

        /// <summary>停止预览。</summary>
        void StopPreview();

        /// <summary>获取当前帧的 WPF 位图（已 Freeze）。</summary>
        BitmapSource GetCurrentFrameAsBitmapSource();

        /// <summary>获取当前帧的 GDI+ Bitmap（用于拍照后的图像处理，调用方负责 Dispose）。</summary>
        Bitmap GetCurrentFrameAsBitmap();
    }

    public class FrameEventArgs : EventArgs
    {
        public BitmapSource Frame { get; set; }
    }

    public class CameraInfo
    {
        public string Name { get; set; }
        public string MonikerString { get; set; }
    }

    public class ResolutionInfo
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int FrameRate { get; set; }

        /// <summary>带帧率的完整显示名（用于日志）。</summary>
        public string DisplayName =>
            $"{Width}×{Height}" + (FrameRate > 0 ? $" @ {FrameRate}fps" : "");

        /// <summary>
        /// WPF ComboBox 默认调用 ToString()。
        /// 单 ComboBox 显示所有有效的 (W, H, FPS) 组合，格式 "1920×1080@60fps"。
        /// 若 FrameRate <= 0（不区分帧率的分辨率），仅显示 "W×H"。
        /// </summary>
        public override string ToString() =>
            $"{Width}×{Height}" + (FrameRate > 0 ? $"@{FrameRate}fps" : "");
    }
}
