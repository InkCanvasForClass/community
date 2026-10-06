namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 视频展台服务：供插件驱动宿主内置视频展台（特殊模式全屏预览）的
    /// 开关、拍照、缩放与旋转，行为与展台菜单内对应按钮一致。
    /// <para>典型场景：第三方视频展台硬件把物理按键模拟为全局热键
    /// （如 Ctrl+Alt+Shift+M/O/I/U/P），插件经 <see cref="IHotkeyService"/>
    /// 接收后转发到本服务，即可用内置展台替代厂商软件。</para>
    /// <para>所有方法均可从任意线程调用，实现内部会切换到 UI 线程执行；
    /// 展台未激活时除 <see cref="Toggle"/> 与 <see cref="IsActive"/> 外均为空操作。</para>
    /// </summary>
    public interface IVideoBoothService
    {
        /// <summary>当前是否处于视频展台特殊模式（全屏预览激活）。</summary>
        bool IsActive { get; }

        /// <summary>
        /// 当前是否处于照片预览页（展台特殊模式下正在查看某张已拍照片，
        /// 而非摄像头直播画面）。拍照按钮在照片预览页会被宿主置灰，
        /// 硬件拍照键可据此给出「返回摄像头画面」的引导提示。
        /// </summary>
        bool IsPhotoPreviewActive { get; }

        /// <summary>当前预览缩放倍率（未激活时恒为 1.0）。</summary>
        double ZoomScale { get; }

        /// <summary>
        /// 从照片预览页返回直播（摄像头）画面。未处于照片预览页时为空操作。
        /// </summary>
        void SwitchToLiveView();

        /// <summary>
        /// 开关视频展台：未激活时进入白板并打开展台；已激活时完全退出
        /// （等同展台菜单「关闭」按钮）。
        /// </summary>
        void Toggle();

        /// <summary>拍照（等同展台菜单拍照按钮：含冷却、照片矫正与冻结帧兜底逻辑）。</summary>
        void CapturePhoto();

        /// <summary>以画面中心为锚点放大一档（与滚轮向上同倍率 1.1）。</summary>
        void ZoomIn();

        /// <summary>以画面中心为锚点缩小一档（与滚轮向下同倍率 1/1.1）。</summary>
        void ZoomOut();

        /// <summary>重置缩放与平移到默认状态（1.0 倍、居中）。</summary>
        void ResetZoom();

        /// <summary>预览画面顺时针旋转 90°（直播页与照片预览页均可）。</summary>
        void Rotate90();
    }
}