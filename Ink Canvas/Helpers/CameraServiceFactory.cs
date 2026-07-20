using System;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 摄像头服务工厂：根据操作系统版本选择实现。
    /// - Win10 1607 (14393)+：使用 <see cref="WinRtCameraService"/>（性能更高）
    /// - Win7 SP1+ / Win10 1607-：使用 <see cref="CameraService"/>（AForge DirectShow 兜底）
    /// </summary>
    public static class CameraServiceFactory
    {
        private static readonly Version Win10MinOsVersion = new Version(10, 0, 14393);
        private static bool? _winRtSupportedCache;

        /// <summary>当前系统是否支持 WinRT 摄像头路径。</summary>
        public static bool IsWinRtSupported
        {
            get
            {
                if (_winRtSupportedCache.HasValue) return _winRtSupportedCache.Value;
                try
                {
                    _winRtSupportedCache = Environment.OSVersion.Version >= Win10MinOsVersion;
                }
                catch
                {
                    _winRtSupportedCache = false;
                }
                return _winRtSupportedCache.Value;
            }
        }

        /// <summary>创建一个新的摄像头服务实例。调用方负责 Dispose。</summary>
        public static ICameraService Create()
        {
            if (IsWinRtSupported)
            {
                try
                {
                    return new WinRtCameraService();
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile(
                        $"WinRtCameraService 创建失败，降级到 AForge：{ex.Message}",
                        LogHelper.LogType.Error);
                }
            }
            return new CameraService();
        }

        /// <summary>强制重新检测 OS 支持（用于测试）。</summary>
        internal static void ResetCache()
        {
            _winRtSupportedCache = null;
        }
    }
}
