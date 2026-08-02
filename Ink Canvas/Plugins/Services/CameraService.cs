using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// <see cref="ICameraService"/> 的宿主实现：包装 <see cref="Ink_Canvas.Helpers.CameraServiceFactory"/>
    /// 创建的 DirectShow 摄像头采集，把宿主帧事件桥接到 SDK 事件。
    /// <para>与宿主视频展台共用摄像头设备——插件启动预览可能抢占展台正在使用的设备。</para>
    /// </summary>
    internal sealed class CameraService : ICameraService
    {
        private Ink_Canvas.Helpers.ICameraService _inner;

        private Ink_Canvas.Helpers.ICameraService Inner
        {
            get
            {
                if (_inner == null)
                {
                    _inner = Ink_Canvas.Helpers.CameraServiceFactory.Create();
                    _inner.FrameReceived += OnInnerFrameReceived;
                }
                return _inner;
            }
        }

        public event Action<BitmapSource> FrameReceived;

        private void OnInnerFrameReceived(object sender, Ink_Canvas.Helpers.FrameEventArgs e)
        {
            FrameReceived?.Invoke(e.Frame);
        }

        public bool IsCapturing
        {
            get
            {
                try { return _inner?.IsCapturing ?? false; }
                catch (Exception ex)
                {
                    Helpers.LogHelper.WriteLogToFile($"CameraService.IsCapturing failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                    return false;
                }
            }
        }

        public IReadOnlyList<PluginCameraInfo> AvailableCameras
        {
            get
            {
                try
                {
                    var list = _inner?.AvailableCameras;
                    if (list == null || list.Count == 0) return Array.Empty<PluginCameraInfo>();
                    return list.Select(c => new PluginCameraInfo
                    {
                        Name = c.Name ?? "",
                        MonikerString = c.MonikerString ?? "",
                    }).ToList();
                }
                catch (Exception ex)
                {
                    Helpers.LogHelper.WriteLogToFile($"CameraService.AvailableCameras failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                    return Array.Empty<PluginCameraInfo>();
                }
            }
        }

        public int RotationAngle
        {
            get
            {
                try { return _inner?.RotationAngle ?? 0; }
                catch (Exception ex)
                {
                    Helpers.LogHelper.WriteLogToFile($"CameraService.RotationAngle failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                    return 0;
                }
            }
            set
            {
                try { if (_inner != null) _inner.RotationAngle = value; }
                catch (Exception ex)
                {
                    Helpers.LogHelper.WriteLogToFile($"CameraService.RotationAngle set failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                }
            }
        }

        public IReadOnlyList<PluginResolutionInfo> NativeResolutions
        {
            get
            {
                try
                {
                    var list = _inner?.NativeResolutions;
                    if (list == null || list.Count == 0) return Array.Empty<PluginResolutionInfo>();
                    return list.Select(r => new PluginResolutionInfo
                    {
                        Width = r.Width,
                        Height = r.Height,
                        FrameRate = r.FrameRate,
                    }).ToList();
                }
                catch (Exception ex)
                {
                    Helpers.LogHelper.WriteLogToFile($"CameraService.NativeResolutions failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                    return Array.Empty<PluginResolutionInfo>();
                }
            }
        }

        public int SelectedResolutionIndex
        {
            get
            {
                try { return _inner?.SelectedResolutionIndex ?? -1; }
                catch (Exception ex)
                {
                    Helpers.LogHelper.WriteLogToFile($"CameraService.SelectedResolutionIndex failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                    return -1;
                }
            }
            set
            {
                try { if (_inner != null) _inner.SelectedResolutionIndex = value; }
                catch (Exception ex)
                {
                    Helpers.LogHelper.WriteLogToFile($"CameraService.SelectedResolutionIndex set failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                }
            }
        }

        public async Task RefreshCameraListAsync()
        {
            try { await Inner.RefreshCameraListAsync(); }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"CameraService.RefreshCameraListAsync failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
            }
        }

        public async Task<bool> StartPreviewAsync(int cameraIndex)
        {
            try { return await Inner.StartPreviewAsync(cameraIndex); }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"CameraService.StartPreviewAsync failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                return false;
            }
        }

        public void StopPreview()
        {
            try { _inner?.StopPreview(); }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"CameraService.StopPreview failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
            }
        }

        public BitmapSource GetCurrentFrame()
        {
            try { return _inner?.GetCurrentFrameAsBitmapSource(); }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"CameraService.GetCurrentFrame failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                return null;
            }
        }
    }
}
