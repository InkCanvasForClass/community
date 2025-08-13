using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Threading;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 统一的墨迹平滑管理器，整合异步处理和硬件加速
    /// </summary>
    public class InkSmoothingManager : IDisposable
    {
        private readonly AsyncAdvancedBezierSmoothing _asyncSmoothing;
        private readonly HardwareAcceleratedInkProcessor _hardwareProcessor;
        private readonly InkSmoothingPerformanceMonitor _performanceMonitor;
        private readonly InkSmoothingConfig _config;
        private readonly Dispatcher _uiDispatcher;
        private bool _disposed;

        public InkSmoothingManager(Dispatcher uiDispatcher)
        {
            _uiDispatcher = uiDispatcher;
            _config = InkSmoothingConfig.FromSettings();
            _config.ApplyQualitySettings();

            _asyncSmoothing = new AsyncAdvancedBezierSmoothing(uiDispatcher)
            {
                SmoothingStrength = _config.SmoothingStrength,
                ResampleInterval = _config.ResampleInterval,
                InterpolationSteps = _config.InterpolationSteps,
                UseHardwareAcceleration = _config.UseHardwareAcceleration,
                MaxConcurrentTasks = _config.MaxConcurrentTasks
            };

            _hardwareProcessor = new HardwareAcceleratedInkProcessor();
            _performanceMonitor = new InkSmoothingPerformanceMonitor();
        }

        /// <summary>
        /// 平滑笔画（自动选择最佳方法）
        /// </summary>
        public async Task<Stroke> SmoothStrokeAsync(Stroke originalStroke,
            Action<Stroke, Stroke> onCompleted = null,
            CancellationToken cancellationToken = default)
        {
            if (originalStroke == null || originalStroke.StylusPoints.Count < 2)
                return originalStroke;

            var stopwatch = Stopwatch.StartNew();
            Stroke result = originalStroke;

            try
            {
                if (_config.UseAsyncProcessing)
                {
                    // 使用异步处理
                    result = await _asyncSmoothing.SmoothStrokeAsync(originalStroke, onCompleted, cancellationToken);
                }
                else if (_config.UseHardwareAcceleration)
                {
                    // 使用硬件加速但同步处理
                    result = await _hardwareProcessor.SmoothStrokeWithGPU(originalStroke);
                    onCompleted?.Invoke(originalStroke, result);
                }
                else
                {
                    // 回退到传统同步处理
                    result = await Task.Run(() =>
                    {
                        var traditionalSmoothing = new AdvancedBezierSmoothing();
                        return traditionalSmoothing.SmoothStroke(originalStroke);
                    }, cancellationToken);
                    onCompleted?.Invoke(originalStroke, result);
                }
            }
            catch (OperationCanceledException)
            {
                result = originalStroke;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"墨迹平滑失败: {ex.Message}");
                result = originalStroke;
            }
            finally
            {
                stopwatch.Stop();
                _performanceMonitor.RecordProcessingTime(stopwatch.Elapsed);
            }

            return result;
        }

        /// <summary>
        /// 同步平滑笔画（用于向后兼容）
        /// </summary>
        public Stroke SmoothStroke(Stroke originalStroke)
        {
            if (originalStroke == null || originalStroke.StylusPoints.Count < 2)
                return originalStroke;

            var stopwatch = Stopwatch.StartNew();
            Stroke result;

            try
            {
                if (_config.UseHardwareAcceleration)
                {
                    // 使用硬件加速的同步版本
                    var task = _hardwareProcessor.SmoothStrokeWithGPU(originalStroke);
                    task.Wait(5000); // 5秒超时
                    result = task.Status == TaskStatus.RanToCompletion ? task.Result : originalStroke;
                }
                else
                {
                    // 传统同步处理
                    var traditionalSmoothing = new AdvancedBezierSmoothing();
                    result = traditionalSmoothing.SmoothStroke(originalStroke);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"同步墨迹平滑失败: {ex.Message}");
                result = originalStroke;
            }
            finally
            {
                stopwatch.Stop();
                _performanceMonitor.RecordProcessingTime(stopwatch.Elapsed);
            }

            return result;
        }

        /// <summary>
        /// 更新配置
        /// </summary>
        public void UpdateConfig()
        {
            var newConfig = InkSmoothingConfig.FromSettings();
            newConfig.ApplyQualitySettings();

            _asyncSmoothing.SmoothingStrength = newConfig.SmoothingStrength;
            _asyncSmoothing.ResampleInterval = newConfig.ResampleInterval;
            _asyncSmoothing.InterpolationSteps = newConfig.InterpolationSteps;
            _asyncSmoothing.UseHardwareAcceleration = newConfig.UseHardwareAcceleration;
            _asyncSmoothing.MaxConcurrentTasks = newConfig.MaxConcurrentTasks;
        }

        /// <summary>
        /// 获取性能统计信息
        /// </summary>
        public string GetPerformanceStats()
        {
            return $"平均处理时间: {_performanceMonitor.GetAverageProcessingTimeMs():F2}ms, " +
                   $"最大处理时间: {_performanceMonitor.GetMaxProcessingTimeMs():F2}ms, " +
                   $"样本数: {_performanceMonitor.GetSampleCount()}";
        }

        /// <summary>
        /// 取消所有正在进行的任务
        /// </summary>
        public void CancelAllTasks()
        {
            _asyncSmoothing?.CancelAllTasks();
        }

        /// <summary>
        /// 检查系统是否支持硬件加速
        /// </summary>
        public static bool IsHardwareAccelerationSupported()
        {
            try
            {
                return RenderCapability.Tier >= 0x00020000;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取推荐的配置
        /// </summary>
        public static InkSmoothingConfig GetRecommendedConfig()
        {
            var config = new InkSmoothingConfig();

            // 根据系统性能调整配置
            var processorCount = Environment.ProcessorCount;
            var isHardwareAccelerated = IsHardwareAccelerationSupported();

            if (processorCount >= 4 && isHardwareAccelerated)
            {
                // 降低高质量模式的门槛，4核以上且支持硬件加速就使用高质量
                config.Quality = InkSmoothingQuality.HighQuality;
                config.UseHardwareAcceleration = true;
                config.UseAsyncProcessing = true;
                config.MaxConcurrentTasks = Math.Min(processorCount, 8);
            }
            else if (processorCount >= 2)
            {
                // 2核以上使用平衡模式
                config.Quality = InkSmoothingQuality.Balanced;
                config.UseHardwareAcceleration = isHardwareAccelerated;
                config.UseAsyncProcessing = true;
                config.MaxConcurrentTasks = Math.Min(processorCount, 4);
            }
            else
            {
                // 单核或性能较低的设备使用高性能模式
                config.Quality = InkSmoothingQuality.HighPerformance;
                config.UseHardwareAcceleration = false;
                config.UseAsyncProcessing = false;
                config.MaxConcurrentTasks = 1;
            }

            config.ApplyQualitySettings();
            return config;
        }

        /// <summary>
        /// 应用推荐配置到设置
        /// </summary>
        public static void ApplyRecommendedSettings()
        {
            var config = GetRecommendedConfig();

            MainWindow.Settings.Canvas.InkSmoothingQuality = (int)config.Quality;
            MainWindow.Settings.Canvas.UseHardwareAcceleration = config.UseHardwareAcceleration;
            MainWindow.Settings.Canvas.UseAsyncInkSmoothing = config.UseAsyncProcessing;
            MainWindow.Settings.Canvas.MaxConcurrentSmoothingTasks = config.MaxConcurrentTasks;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                CancelAllTasks();
                _asyncSmoothing?.Dispose();
                _hardwareProcessor?.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// 墨迹平滑事件参数
    /// </summary>
    public class InkSmoothingEventArgs : EventArgs
    {
        public Stroke OriginalStroke { get; set; }
        public Stroke SmoothedStroke { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public bool WasAsync { get; set; }
        public bool UsedHardwareAcceleration { get; set; }
    }
}
