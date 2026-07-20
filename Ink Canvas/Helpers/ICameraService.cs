using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 视频展台摄像头服务的抽象接口，AForge (Win7+) 和 WinRT (Win10+) 各自实现。
    /// </summary>
    public interface ICameraService : IDisposable
    {
        /// <summary>每收到一帧时触发（参数为已 Freeze 的 BitmapSource，可跨线程）。</summary>
        event EventHandler<FrameEventArgs> FrameReceived;

        /// <summary>发生错误时触发，参数为错误描述。</summary>
        event EventHandler<string> ErrorOccurred;

        bool IsCapturing { get; }
        IReadOnlyList<CameraInfo> AvailableCameras { get; }
        CameraInfo CurrentCamera { get; }

        /// <summary>0=0°, 1=90°, 2=180°, 3=270°。</summary>
        int RotationAngle { get; set; }

        /// <summary>当前摄像头支持的 native 分辨率列表（可能为空，表示设备未提供）。</summary>
        IReadOnlyList<ResolutionInfo> NativeResolutions { get; }

        /// <summary>当前选中的 native 分辨率索引；-1 表示未选中。</summary>
        int SelectedResolutionIndex { get; set; }

        /// <summary>
        /// 刷新可用摄像头列表。返回 Task 以便调用方 await（AForge 同步完成，WinRT 异步完成）。
        /// 调用完成后 <see cref="AvailableCameras"/> 已就绪。
        /// </summary>
        Task RefreshCameraListAsync();

        /// <summary>启动指定摄像头的预览。会刷新 NativeResolutions。返回 Task 以避免阻塞 UI 线程（WinRT 实现是异步的）。</summary>
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

        public string DisplayName =>
            $"{Width}×{Height}" + (FrameRate > 0 ? $" @ {FrameRate}fps" : "");

        // WPF ComboBox 默认调用 ToString()，不重写会显示类型全名 "Ink_Canvas.Helpers.ResolutionInfo"
        public override string ToString() => DisplayName;
    }
}
