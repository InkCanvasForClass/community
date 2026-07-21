namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 摄像头服务工厂。
    /// 统一返回 <see cref="DirectShowCameraService"/>（基于 DirectShowLib + SampleGrabber），
    /// 不再依赖 AForge.Video / WinRT MediaFrameReader。
    /// 视频展台特殊模式（全屏预览）走 MainWindow.VideoPresenterFullCanvasImage（WPFMediaKit VideoCaptureElement）。
    /// </summary>
    public static class CameraServiceFactory
    {
        /// <summary>创建一个新的摄像头服务实例。调用方负责 Dispose。</summary>
        public static ICameraService Create()
        {
            return new DirectShowCameraService();
        }
    }
}
