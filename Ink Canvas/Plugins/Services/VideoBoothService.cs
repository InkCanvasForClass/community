using System;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// <see cref="IVideoBoothService"/> 的宿主实现：包装 MainWindow 视频展台特殊模式的
    /// 开关/拍照/缩放/旋转公开入口。供插件（如视频展台硬件按钮热键适配插件）调用。
    /// </summary>
    internal sealed class VideoBoothService : IVideoBoothService
    {
        private readonly MainWindow _mainWindow;

        public VideoBoothService(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
        }

        public bool IsActive
        {
            get
            {
                try
                {
                    return _mainWindow != null && Invoke(() => _mainWindow.IsVideoBoothActive);
                }
                catch (Exception ex)
                {
                    Helpers.LogHelper.WriteLogToFile($"VideoBoothService.IsActive 异常: {ex.Message}", Helpers.LogHelper.LogType.Error);
                    return false;
                }
            }
        }

        public bool IsPhotoPreviewActive
        {
            get
            {
                try
                {
                    return _mainWindow != null && Invoke(() => _mainWindow.IsVideoBoothPhotoPreviewActive);
                }
                catch (Exception ex)
                {
                    Helpers.LogHelper.WriteLogToFile($"VideoBoothService.IsPhotoPreviewActive 异常: {ex.Message}", Helpers.LogHelper.LogType.Error);
                    return false;
                }
            }
        }

        public void SwitchToLiveView()
        {
            SafeInvoke(() => _mainWindow.VideoBoothSwitchToLiveView(), nameof(SwitchToLiveView));
        }

        public double ZoomScale
        {
            get
            {
                try
                {
                    return _mainWindow == null ? 1.0 : Invoke(() => _mainWindow.VideoBoothZoomScale);
                }
                catch (Exception ex)
                {
                    Helpers.LogHelper.WriteLogToFile($"VideoBoothService.ZoomScale 异常: {ex.Message}", Helpers.LogHelper.LogType.Error);
                    return 1.0;
                }
            }
        }

        public void Toggle()
        {
            SafeInvoke(() => _mainWindow.ToggleVideoBooth(), nameof(Toggle));
        }

        public void CapturePhoto()
        {
            SafeInvoke(() => _mainWindow.VideoBoothCapturePhoto(), nameof(CapturePhoto));
        }

        public void ZoomIn()
        {
            SafeInvoke(() => _mainWindow.VideoBoothZoom(1.1), nameof(ZoomIn));
        }

        public void ZoomOut()
        {
            SafeInvoke(() => _mainWindow.VideoBoothZoom(1.0 / 1.1), nameof(ZoomOut));
        }

        public void ResetZoom()
        {
            SafeInvoke(() => _mainWindow.VideoBoothResetZoom(), nameof(ResetZoom));
        }

        public void Rotate90()
        {
            SafeInvoke(() => _mainWindow.VideoBoothRotate90(), nameof(Rotate90));
        }

        /// <summary>把操作转发到 UI 线程执行并吞掉异常（记录日志），避免热键线程炸掉宿主。</summary>
        private void SafeInvoke(Action action, string operationName)
        {
            try
            {
                if (_mainWindow == null) return;
                Invoke(action);
            }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"VideoBoothService.{operationName} 异常: {ex.Message}", Helpers.LogHelper.LogType.Error);
            }
        }

        /// <summary>热键回调可能在非 UI 线程触发；这里统一调度到宿主 UI 线程。</summary>
        private T Invoke<T>(Func<T> func)
        {
            var dispatcher = _mainWindow?.Dispatcher ?? System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                return func();
            }

            return dispatcher.Invoke(func);
        }

        private void Invoke(Action action)
        {
            var dispatcher = _mainWindow?.Dispatcher ?? System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
                return;
            }

            dispatcher.Invoke(action);
        }
    }
}