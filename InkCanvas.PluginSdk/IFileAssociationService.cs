namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 文件关联服务，供插件注册自定义文件类型关联。
    /// </summary>
    public interface IFileAssociationService
    {
        /// <summary>
        /// 注册文件关联（需要管理员权限）。
        /// </summary>
        /// <param name="extension">文件扩展名，如 ".icstk"</param>
        /// <param name="progId">程序标识符，如 "InkCanvasForClass.CE.icstk"</param>
        /// <param name="description">文件类型描述</param>
        /// <param name="iconPath">图标路径（可选）</param>
        /// <param name="pluginId">归属插件 ID（可选）。调用发生在 <c>Initialize</c> 之外（如设置页）时宿主无法自动识别调用方，
        /// 必须显式传插件自身 ID，否则双击打开对应扩展名文件时宿主不派发任何插件；建议传插件的 <c>Manifest.Id</c>。</param>
        /// <returns>是否注册成功</returns>
        bool Register(string extension, string progId, string description, string iconPath = null, string pluginId = null);

        /// <summary>
        /// 注销文件关联。
        /// </summary>
        bool Unregister(string extension);

        /// <summary>
        /// 检查文件关联是否已注册。
        /// </summary>
        bool IsRegistered(string extension);
    }
}
