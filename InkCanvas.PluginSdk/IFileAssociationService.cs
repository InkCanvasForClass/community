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
        /// <returns>是否注册成功</returns>
        bool Register(string extension, string progId, string description, string iconPath = null);

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
