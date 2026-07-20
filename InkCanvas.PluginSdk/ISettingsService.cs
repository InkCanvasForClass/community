namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 设置服务，供插件读写主程序设置。
    /// </summary>
    public interface ISettingsService
    {
        /// <summary>
        /// 获取设置值。
        /// </summary>
        /// <param name="key">设置键，使用 "." 分隔层级，如 "appearance.theme"</param>
        /// <returns>设置值，不存在返回 default</returns>
        T Get<T>(string key);

        /// <summary>
        /// 设置值。
        /// </summary>
        /// <param name="key">设置键</param>
        /// <param name="value">值</param>
        void Set<T>(string key, T value);

        /// <summary>
        /// 检查设置键是否存在。
        /// </summary>
        bool Has(string key);

        /// <summary>
        /// 设置变更时触发。
        /// </summary>
        event System.Action<string, object> SettingChanged;
    }
}
