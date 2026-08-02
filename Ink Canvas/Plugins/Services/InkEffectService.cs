using System;
using System.Windows;
using System.Windows.Ink;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// <see cref="IInkEffectService"/> 的宿主实现：包装 <see cref="Ink_Canvas.Helpers.InkFadeManager"/>。
    /// 画布未初始化（InkFadeManager 为 null）时，变更类操作安全 no-op。
    /// </summary>
    internal sealed class InkEffectService : IInkEffectService
    {
        private readonly MainWindow _mainWindow;

        public InkEffectService(MainWindow mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        }

        private Ink_Canvas.Helpers.InkFadeManager Manager => _mainWindow.InkFadeManagerInstance;

        public bool IsEnabled
        {
            get { return Manager?.IsEnabled ?? false; }
            set { if (Manager != null) Manager.IsEnabled = value; }
        }

        public int FadeTime
        {
            get { return Manager?.FadeTime ?? 0; }
            set { if (Manager != null) Manager.FadeTime = value; }
        }

        public double FadeSpeedMultiplier
        {
            get { return Manager?.FadeSpeedMultiplier ?? 1.0; }
            set { if (Manager != null) Manager.FadeSpeedMultiplier = value; }
        }

        public void AddFadingStroke(Stroke stroke, Point startPoint, Point endPoint, long strokeDurationMs = 0)
        {
            try { Manager?.AddFadingStroke(stroke, startPoint, endPoint, strokeDurationMs); }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"InkEffectService.AddFadingStroke failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
            }
        }

        public void RemoveStroke(Stroke stroke)
        {
            try { Manager?.RemoveStroke(stroke); }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"InkEffectService.RemoveStroke failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
            }
        }

        public void ClearAllFadingStrokes()
        {
            try { Manager?.ClearAllFadingStrokes(); }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"InkEffectService.ClearAllFadingStrokes failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
            }
        }

        public void UpdateFadeTime(int fadeTime)
        {
            try { Manager?.UpdateFadeTime(fadeTime); }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"InkEffectService.UpdateFadeTime failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
            }
        }

        public void UpdateFadeSpeedMultiplier(double multiplier)
        {
            try { Manager?.UpdateFadeSpeedMultiplier(multiplier); }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"InkEffectService.UpdateFadeSpeedMultiplier failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
            }
        }

        public void Enable()
        {
            try { Manager?.Enable(); }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"InkEffectService.Enable failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
            }
        }

        public void Disable()
        {
            try { Manager?.Disable(); }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"InkEffectService.Disable failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
            }
        }
    }
}
