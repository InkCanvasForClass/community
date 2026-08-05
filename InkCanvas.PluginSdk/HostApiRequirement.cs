namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 宿主的 API 兼容性要求，由 <see cref="PluginManager"/> 在加载前对所有插件生效。
    /// </summary>
    public static class HostApiRequirement
    {
        /// <summary>
        /// 当前宿主提供的插件 API 版本。插件在 manifest 中声明 <c>ApiVersion</c>，
        /// 主版本相同且不高于此值即为兼容。
        /// <para>
        /// 维护规则：<b>每次向 SDK 新增对外接口或在既有接口上追加成员，都必须抬升次版本号</b>
        /// （1.9.0 → 1.10.0），插件才有办法声明自己需要的能力下限。
        /// 发生破坏性变更（删除/改签名）时抬主版本号，这会使所有声明旧主版本的插件停止加载。
        /// 仅修复实现、不动接口时不需要改动。
        /// </para>
        /// </summary>
        public static readonly string CurrentApiVersion = "1.10.0";

        /// <summary>
        /// 当前宿主编译版本号，由 Nerdbank.GitVersioning 依据 <c>version.json</c> 与 git 状态自动生成，
        /// 随构建自动更新，不再手动维护。
        /// <para>
        /// 必须是 <c>static readonly</c> 而非 <c>const</c>：<c>const</c> 会在引用方编译期内联成字面量，
        /// 导致通过 NuGet 引用本 SDK 的插件/宿主在只更新 SDK 程序集、未重新编译时仍读到旧版本号。
        /// </para>
        /// </summary>
        public static readonly string HostVersion = ThisAssembly.AssemblyFileVersion;
    }
}
