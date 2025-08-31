using System;
using System.Diagnostics;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 墨迹平滑配置类
    /// </summary>
    public class InkSmoothingConfig
    {
        // 基本平滑参数
        public double SmoothingStrength { get; set; } = 0.4;
        public double ResampleInterval { get; set; } = 2.5;
        public int InterpolationSteps { get; set; } = 12;
        
        // 贝塞尔曲线参数
        public bool UseAdaptiveInterpolation { get; set; } = true;
        public double CurveTension { get; set; } = 0.3;
        public double MinCurvatureThreshold { get; set; } = 0.1;
        public double MaxCurvatureThreshold { get; set; } = 0.8;
        
        // 性能参数
        public bool UseHardwareAcceleration { get; set; } = true;
        public bool UseAsyncProcessing { get; set; } = true;
        public int MaxConcurrentTasks { get; set; } = Environment.ProcessorCount;
        public int MaxPointsPerStroke { get; set; } = 10000;
        
        // 质量设置
        public SmoothingQuality Quality { get; set; } = SmoothingQuality.Balanced;
        
        public enum SmoothingQuality
        {
            Performance,    // 性能优先
            Balanced,       // 平衡
            Quality        // 质量优先
        }

        // 兼容性枚举
        public enum InkSmoothingQuality
        {
            HighPerformance = 0,  // 高性能低质量
            Balanced = 1,         // 平衡
            HighQuality = 2       // 高质量低性能
        }

        /// <summary>
        /// 从设置中加载配置
        /// </summary>
        public static InkSmoothingConfig FromSettings()
        {
            var config = new InkSmoothingConfig();
            
            try
            {
                // 尝试从MainWindow.Settings加载配置（兼容性）
                if (MainWindow.Settings?.Canvas != null)
                {
                    config.Quality = (SmoothingQuality)MainWindow.Settings.Canvas.InkSmoothingQuality;
                    config.UseHardwareAcceleration = MainWindow.Settings.Canvas.UseHardwareAcceleration;
                    config.UseAsyncProcessing = MainWindow.Settings.Canvas.UseAsyncInkSmoothing;
                    config.MaxConcurrentTasks = MainWindow.Settings.Canvas.MaxConcurrentSmoothingTasks > 0 ?
                        MainWindow.Settings.Canvas.MaxConcurrentSmoothingTasks : Environment.ProcessorCount;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载平滑配置失败: {ex.Message}");
            }
            
            return config;
        }

        /// <summary>
        /// 应用质量设置
        /// </summary>
        public void ApplyQualitySettings()
        {
            switch (Quality)
            {
                case SmoothingQuality.Performance:
                    SmoothingStrength = 0.2;
                    ResampleInterval = 4.0;
                    InterpolationSteps = 6;
                    UseAdaptiveInterpolation = false;
                    CurveTension = 0.2;
                    MaxConcurrentTasks = Math.Max(1, Environment.ProcessorCount / 2);
                    break;
                    
                case SmoothingQuality.Balanced:
                    SmoothingStrength = 0.4;
                    ResampleInterval = 2.5;
                    InterpolationSteps = 12;
                    UseAdaptiveInterpolation = true;
                    CurveTension = 0.3;
                    MaxConcurrentTasks = Environment.ProcessorCount;
                    break;
                    
                case SmoothingQuality.Quality:
                    SmoothingStrength = 0.6;
                    ResampleInterval = 1.5;
                    InterpolationSteps = 20;
                    UseAdaptiveInterpolation = true;
                    CurveTension = 0.4;
                    MaxConcurrentTasks = Environment.ProcessorCount;
                    break;
            }
        }

        /// <summary>
        /// 保存配置到设置
        /// </summary>
        public void SaveToSettings()
        {
            try
            {
                // 尝试保存到MainWindow.Settings（兼容性）
                if (MainWindow.Settings?.Canvas != null)
                {
                    MainWindow.Settings.Canvas.InkSmoothingQuality = (int)Quality;
                    MainWindow.Settings.Canvas.UseHardwareAcceleration = UseHardwareAcceleration;
                    MainWindow.Settings.Canvas.UseAsyncInkSmoothing = UseAsyncProcessing;
                    MainWindow.Settings.Canvas.MaxConcurrentSmoothingTasks = MaxConcurrentTasks;
                    
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存平滑配置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 验证配置参数
        /// </summary>
        public bool Validate()
        {
            return SmoothingStrength >= 0.0 && SmoothingStrength <= 1.0 &&
                   ResampleInterval > 0.0 &&
                   InterpolationSteps > 0 && InterpolationSteps <= 50 &&
                   CurveTension >= 0.0 && CurveTension <= 1.0 &&
                   MaxConcurrentTasks > 0 &&
                   MaxPointsPerStroke > 0;
        }

        /// <summary>
        /// 获取配置摘要
        /// </summary>
        public string GetSummary()
        {
            return $"质量: {Quality}, 强度: {SmoothingStrength:F2}, 间隔: {ResampleInterval:F1}, " +
                   $"步数: {InterpolationSteps}, 自适应: {UseAdaptiveInterpolation}, " +
                   $"张力: {CurveTension:F2}, 硬件加速: {UseHardwareAcceleration}";
        }
    }
} 