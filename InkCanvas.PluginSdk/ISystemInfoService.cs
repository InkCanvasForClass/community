namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 系统信息服务：供插件读取宿主设备与系统信息。
    /// </summary>
    public interface ISystemInfoService
    {
        /// <summary>设备唯一 ID（用于诊断/上报）。</summary>
        string DeviceId { get; }

        /// <summary>Windows 系统版本描述。</summary>
        string SystemVersion { get; }

        /// <summary>宿主使用统计（启动次数、累计时长等）。</summary>
        PluginUsageStats GetUsageStats();
    }

    /// <summary>
    /// 宿主使用统计。
    /// </summary>
    public sealed class PluginUsageStats
    {
        /// <summary>累计启动次数。</summary>
        public int LaunchCount { get; set; }

        /// <summary>累计使用时长（秒）。</summary>
        public long TotalSeconds { get; set; }

        /// <summary>平均单次会话时长（秒）。</summary>
        public double AvgSessionSeconds { get; set; }

        /// <summary>更新优先级（int 形式的宿主枚举）。</summary>
        public int UpdatePriority { get; set; }
    }
}
