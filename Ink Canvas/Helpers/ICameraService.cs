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

        // === 摄像头属性控制（手机专业模式）===
        // 通过 DirectShow IAMVideoProcAmp / IAMCameraControl 写入摄像头硬件。
        // 视频展台特殊模式下 VideoCaptureElement 占用设备流式预览，这里用一个
        // "不抢占设备"的常驻 FilterGraphNoThread + source filter 同时持有两个接口，
        // 属性写入驱动 KSPROPERTY 后全局生效，VideoCaptureElement 的画面随之改变。

        /// <summary>
        /// 亮度（曝光度）归一化值，范围 -100..100，0 表示摄像头默认值。
        /// 等价于 <see cref="GetCameraPropertyValue"/>(<see cref="BoothCameraProperty.Brightness"/>)，
        /// 保留是为了与早期代码兼容。setter 会异步把值应用到硬件。
        /// 摄像头不支持时 setter 静默忽略。
        /// </summary>
        int Brightness { get; set; }

        /// <summary>当前摄像头是否支持亮度（曝光度）调节。等价于 IsCameraPropertySupported(Brightness)。</summary>
        bool BrightnessSupported { get; }

        /// <summary>
        /// 探测当前摄像头支持的全部属性（亮度/对比度/饱和度/色温/增益/焦距/快门），
        /// 并构建常驻"不抢占设备"的图同时持有 IAMVideoProcAmp 与 IAMCameraControl。
        /// 探测后 <see cref="CameraProperties"/> 已就绪，并会立即把当前所有值应用一次。
        /// 等价于 <see cref="ProbeCameraPropertiesAsync"/>，保留是为了兼容。
        /// </summary>
        Task ProbeBrightnessSupportAsync();

        /// <summary>立即把当前 Brightness 应用到摄像头硬件。等价于 ApplyCameraPropertyAsync(Brightness)。</summary>
        Task<bool> ApplyBrightnessAsync();

        /// <summary>释放属性控制占用的常驻 FilterGraphNoThread 资源。等价于 ReleaseCameraPropertyControl()。</summary>
        void ReleaseBrightnessControl();

        // --- 统一属性 API（推荐新代码使用）---

        /// <summary>当前摄像头所有属性的支持状态与归一化值（-100..100，0=默认）。需先 ProbeCameraPropertiesAsync。</summary>
        IReadOnlyDictionary<BoothCameraProperty, CameraPropState> CameraProperties { get; }

        /// <summary>读取指定属性的归一化值（-100..100，0=默认）。未支持/未探测时返回 0。</summary>
        int GetCameraPropertyValue(BoothCameraProperty prop);

        /// <summary>
        /// 设置指定属性的归一化值（-100..100，自动 clamp）。setter 会异步应用到硬件，不阻塞 UI。
        /// 摄像头不支持时静默忽略。
        /// </summary>
        void SetCameraPropertyValue(BoothCameraProperty prop, int value);

        /// <summary>当前摄像头是否支持指定属性。需先 ProbeCameraPropertiesAsync。</summary>
        bool IsCameraPropertySupported(BoothCameraProperty prop);

        /// <summary>
        /// 探测当前摄像头支持的全部属性，构建常驻"不抢占设备"的图同时持有 IAMVideoProcAmp 与 IAMCameraControl。
        /// 内部用 FilterGraphNoThread + AddSourceFilterForMoniker，不调用 Run()，可与 VideoCaptureElement 流式预览共存。
        /// 探测后 <see cref="CameraProperties"/> 已就绪，并会立即把当前所有属性值应用一次。
        /// </summary>
        Task ProbeCameraPropertiesAsync();

        /// <summary>立即把指定属性的当前值应用到摄像头硬件。通常由 SetCameraPropertyValue 自动触发。</summary>
        Task<bool> ApplyCameraPropertyAsync(BoothCameraProperty prop);

        /// <summary>释放属性控制占用的常驻 FilterGraphNoThread 资源（切换摄像头/停止预览/退出展台时调用）。</summary>
        void ReleaseCameraPropertyControl();
    }

    /// <summary>
    /// 视频展台可调摄像头属性枚举。映射到 DirectShow IAMVideoProcAmp / IAMCameraControl 的对应 property。
    /// </summary>
    public enum BoothCameraProperty
    {
        /// <summary>亮度（曝光度）- IAMVideoProcAmp.Brightness。绝大多数摄像头支持。</summary>
        Brightness,
        /// <summary>对比度 - IAMVideoProcAmp.Contrast。</summary>
        Contrast,
        /// <summary>饱和度 - IAMVideoProcAmp.Saturation。</summary>
        Saturation,
        /// <summary>色温（白平衡）- IAMVideoProcAmp.WhiteBalance。需摄像头支持 Manual，否则只能 Auto。</summary>
        WhiteBalance,
        /// <summary>增益（最接近手机 ISO 的概念）- IAMVideoProcAmp.Gain。DirectShow 无 ISO 概念。</summary>
        Gain,
        /// <summary>焦距（手动对焦）- IAMCameraControl.Focus。需有马达的镜头；定焦摄像头不支持。</summary>
        Focus,
        /// <summary>快门（曝光时间）- IAMCameraControl.Exposure。多数 USB 摄像头仅 Auto；非手机绝对快门速度。</summary>
        Exposure,
    }

    /// <summary>单个摄像头属性的支持状态与归一化值。</summary>
    public class CameraPropState
    {
        /// <summary>摄像头是否支持手动调节该属性。</summary>
        public bool Supported;
        /// <summary>硬件范围最小值（IAMVideoProcAmp/IAMCameraControl.GetRange 返回）。</summary>
        public int HwMin;
        /// <summary>硬件范围最大值。</summary>
        public int HwMax;
        /// <summary>硬件默认值。</summary>
        public int HwDefault;
        /// <summary>归一化值 -100..100，0=默认。+100=max，-100=min。</summary>
        public int NormalizedValue;
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
