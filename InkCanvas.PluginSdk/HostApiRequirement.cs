namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 宿主的 API 兼容性要求，由 <see cref="PluginManager"/> 在加载前对所有插件生效。
    /// </summary>
    public static class HostApiRequirement
    {
        /// <summary>
        /// 当前宿主支持的最大 API 版本。主版本相同即为兼容。
        /// </summary>
        public static readonly string CurrentApiVersion = "1.0.0";

        /// <summary>
        /// 当前宿主最小受支持的插件宿主版本（例如 "1.7.18"）。低于该版本的插件会被拒绝。
        /// </summary>
        public static readonly string MinSupportedHostVersion = "1.7.18";

        /// <summary>
        /// 当前宿主编译版本号，与 <c>version.json</c> 一致。
        /// </summary>
        public const string HostVersion = "1.7.18.7";
    }
}
