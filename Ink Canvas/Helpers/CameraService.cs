using AForge.Video;
using AForge.Video.DirectShow;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Ink_Canvas.Helpers
{
    public class CameraService : IDisposable
    {
        private VideoCaptureDevice _videoSource;
        private bool _isCapturing;
        private Bitmap _currentFrame;
        private readonly object _frameLock = new object();
        private Dispatcher _dispatcher;

        public event EventHandler<Bitmap> FrameReceived;
        public event EventHandler<string> ErrorOccurred;

        public bool IsCapturing => _isCapturing;
        public List<FilterInfo> AvailableCameras { get; private set; }
        public FilterInfo CurrentCamera { get; private set; }

        public CameraService()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            AvailableCameras = new List<FilterInfo>();
            RefreshCameraList();
        }

        /// <summary>
        /// 刷新可用摄像头列表
        /// </summary>
        public void RefreshCameraList()
        {
            try
            {
                AvailableCameras.Clear();
                var videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                
                foreach (FilterInfo device in videoDevices)
                {
                    AvailableCameras.Add(device);
                }

                LogHelper.WriteLogToFile($"发现 {AvailableCameras.Count} 个摄像头设备");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"刷新摄像头列表失败: {ex.Message}", LogHelper.LogType.Error);
                ErrorOccurred?.Invoke(this, $"刷新摄像头列表失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 开始摄像头预览
        /// </summary>
        /// <param name="cameraIndex">摄像头索引</param>
        public bool StartPreview(int cameraIndex = 0)
        {
            try
            {
                if (AvailableCameras.Count == 0)
                {
                    RefreshCameraList();
                    if (AvailableCameras.Count == 0)
                    {
                        ErrorOccurred?.Invoke(this, "未找到可用的摄像头设备");
                        return false;
                    }
                }

                if (cameraIndex < 0 || cameraIndex >= AvailableCameras.Count)
                {
                    ErrorOccurred?.Invoke(this, "摄像头索引超出范围");
                    return false;
                }

                // 停止当前预览
                StopPreview();

                CurrentCamera = AvailableCameras[cameraIndex];
                _videoSource = new VideoCaptureDevice(CurrentCamera.MonikerString);

                // 设置视频源事件处理
                _videoSource.NewFrame += VideoSource_NewFrame;

                // 启动视频源
                _videoSource.Start();

                _isCapturing = true;
                LogHelper.WriteLogToFile($"开始摄像头预览: {CurrentCamera.Name}");
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"启动摄像头预览失败: {ex.Message}", LogHelper.LogType.Error);
                ErrorOccurred?.Invoke(this, $"启动摄像头预览失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 停止摄像头预览
        /// </summary>
        public void StopPreview()
        {
            try
            {
                if (_videoSource != null && _videoSource.IsRunning)
                {
                    _videoSource.SignalToStop();
                    _videoSource.WaitForStop();
                    _videoSource.NewFrame -= VideoSource_NewFrame;
                    _videoSource = null;
                }

                _isCapturing = false;
                LogHelper.WriteLogToFile("摄像头预览已停止");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"停止摄像头预览失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        /// <summary>
        /// 切换到指定摄像头
        /// </summary>
        /// <param name="cameraIndex">摄像头索引</param>
        public bool SwitchCamera(int cameraIndex)
        {
            try
            {
                if (cameraIndex < 0 || cameraIndex >= AvailableCameras.Count)
                {
                    ErrorOccurred?.Invoke(this, "摄像头索引超出范围");
                    return false;
                }

                return StartPreview(cameraIndex);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"切换摄像头失败: {ex.Message}", LogHelper.LogType.Error);
                ErrorOccurred?.Invoke(this, $"切换摄像头失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取当前帧的Bitmap
        /// </summary>
        public Bitmap GetCurrentFrame()
        {
            lock (_frameLock)
            {
                return _currentFrame?.Clone() as Bitmap;
            }
        }

        /// <summary>
        /// 获取当前帧的BitmapSource（WPF格式）
        /// </summary>
        public BitmapSource GetCurrentFrameAsBitmapSource()
        {
            lock (_frameLock)
            {
                if (_currentFrame == null)
                    return null;

                try
                {
                    using (var memory = new MemoryStream())
                    {
                        _currentFrame.Save(memory, ImageFormat.Png);
                        memory.Position = 0;

                        var bitmapImage = new BitmapImage();
                        bitmapImage.BeginInit();
                        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                        bitmapImage.StreamSource = memory;
                        bitmapImage.EndInit();
                        bitmapImage.Freeze();

                        return bitmapImage;
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"转换帧为BitmapSource失败: {ex.Message}", LogHelper.LogType.Error);
                    return null;
                }
            }
        }

        /// <summary>
        /// 视频源新帧事件处理
        /// </summary>
        private void VideoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            try
            {
                lock (_frameLock)
                {
                    // 释放之前的帧
                    _currentFrame?.Dispose();
                    
                    // 克隆新帧
                    _currentFrame = eventArgs.Frame.Clone() as Bitmap;
                }

                // 在UI线程中触发事件
                _dispatcher.BeginInvoke(new Action(() =>
                {
                    FrameReceived?.Invoke(this, _currentFrame);
                }));
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"处理新帧失败: {ex.Message}", LogHelper.LogType.Error);
                ErrorOccurred?.Invoke(this, $"处理新帧失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取摄像头名称列表
        /// </summary>
        public List<string> GetCameraNames()
        {
            return AvailableCameras.Select(camera => camera.Name).ToList();
        }

        /// <summary>
        /// 检查是否有可用摄像头
        /// </summary>
        public bool HasAvailableCameras()
        {
            if (AvailableCameras.Count == 0)
            {
                RefreshCameraList();
            }
            return AvailableCameras.Count > 0;
        }

        public void Dispose()
        {
            StopPreview();
            
            lock (_frameLock)
            {
                _currentFrame?.Dispose();
            }
        }
    }
}