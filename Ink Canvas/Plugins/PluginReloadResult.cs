namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// <see cref="PluginManager.ReloadPlugin"/> 的结果。
    /// </summary>
    public class PluginReloadResult
    {
        /// <summary>
        /// 插件是否已重新加载并进入 Loaded 状态。
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 旧版本的 AssemblyLoadContext 是否已被 GC 真正回收。
        /// <para>
        /// 为 false 时插件仍能正常工作（新程序集已加载），但旧程序集滞留在内存中，
        /// 说明宿主某处还持有插件对象的引用。反复重载会累积内存占用，
        /// 建议提示用户重启以彻底清理。
        /// </para>
        /// </summary>
        public bool FullyUnloaded { get; set; }

        /// <summary>
        /// 插件目录已不存在，本次操作实际是卸载而非重载。
        /// </summary>
        public bool WasRemoved { get; set; }

        /// <summary>
        /// 失败原因，仅在 <see cref="Success"/> 为 false 时有值。
        /// </summary>
        public string ErrorMessage { get; set; }

        public static PluginReloadResult Failed(string message)
        {
            return new PluginReloadResult { Success = false, ErrorMessage = message };
        }
    }
}
