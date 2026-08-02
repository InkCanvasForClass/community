using System;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// <see cref="ISystemInfoService"/> 的宿主实现：包装 <see cref="Ink_Canvas.Helpers.DeviceIdentifier"/>。
    /// </summary>
    internal sealed class SystemInfoService : ISystemInfoService
    {
        public string DeviceId
        {
            get
            {
                try { return Ink_Canvas.Helpers.DeviceIdentifier.GetDeviceId(); }
                catch (Exception ex)
                {
                    Helpers.LogHelper.WriteLogToFile($"SystemInfoService.DeviceId failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                    return "";
                }
            }
        }

        public string SystemVersion
        {
            get
            {
                try { return Ink_Canvas.Helpers.DeviceIdentifier.GetSystemVersion(); }
                catch (Exception ex)
                {
                    Helpers.LogHelper.WriteLogToFile($"SystemInfoService.SystemVersion failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                    return "";
                }
            }
        }

        public PluginUsageStats GetUsageStats()
        {
            try
            {
                var (launchCount, totalSeconds, avgSessionSeconds, priority) =
                    Ink_Canvas.Helpers.DeviceIdentifier.GetUsageStats();
                return new PluginUsageStats
                {
                    LaunchCount = launchCount,
                    TotalSeconds = totalSeconds,
                    AvgSessionSeconds = avgSessionSeconds,
                    UpdatePriority = (int)priority,
                };
            }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile($"SystemInfoService.GetUsageStats failed: {ex.Message}", Helpers.LogHelper.LogType.Warning);
                return new PluginUsageStats();
            }
        }
    }
}
