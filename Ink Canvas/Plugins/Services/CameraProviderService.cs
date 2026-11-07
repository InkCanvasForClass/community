using System;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// <see cref="ICameraProviderService"/> 的宿主实现：把插件注册的虚拟摄像头交给
    /// <see cref="Ink_Canvas.Helpers.CameraProviderRegistry"/>。
    /// <para>注册表是单例，摄像头服务（<see cref="Ink_Canvas.Helpers.DirectShowCameraService"/>）
    /// 和视频展台 UI（MainWindow）都直接读取同一个实例，因此这里只做参数校验与转发。</para>
    /// </summary>
    internal sealed class CameraProviderService : ICameraProviderService
    {
        /// <inheritdoc/>
        public void RegisterProvider(IPluginCameraProvider provider)
        {
            try
            {
                Helpers.CameraProviderRegistry.Instance.Register(provider);
            }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile(
                    $"[CameraProvider] RegisterProvider 失败: {ex.Message}",
                    Helpers.LogHelper.LogType.Error);
                throw;
            }
        }

        /// <inheritdoc/>
        public void UnregisterProvider(IPluginCameraProvider provider)
        {
            try
            {
                Helpers.CameraProviderRegistry.Instance.Unregister(provider);
            }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile(
                    $"[CameraProvider] UnregisterProvider 失败: {ex.Message}",
                    Helpers.LogHelper.LogType.Error);
            }
        }

        /// <inheritdoc/>
        public void NotifyCamerasChanged(IPluginCameraProvider provider)
        {
            try
            {
                Helpers.CameraProviderRegistry.Instance.NotifyCamerasChanged(provider);
            }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile(
                    $"[CameraProvider] NotifyCamerasChanged 失败: {ex.Message}",
                    Helpers.LogHelper.LogType.Error);
            }
        }
    }
}
