namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 文件对话框服务：供插件弹出标准的 Windows 打开/保存文件对话框。
    /// <para>宿主内部切到 UI 线程展示对话框，以宿主主窗口为所有者。</para>
    /// </summary>
    public interface IFileDialogService
    {
        /// <summary>
        /// 弹出「打开文件」对话框。
        /// </summary>
        /// <param name="title">对话框标题。</param>
        /// <param name="filter">文件过滤器，如 "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*"；null 用默认。</param>
        /// <param name="initialDirectory">初始目录；null 用上次目录。</param>
        /// <returns>选中的文件路径；用户取消返回 null。</returns>
        string OpenFile(string title, string filter = null, string initialDirectory = null);

        /// <summary>
        /// 弹出「打开文件」对话框，允许多选。
        /// </summary>
        /// <returns>选中的文件路径列表；用户取消返回空数组。</returns>
        string[] OpenFiles(string title, string filter = null, string initialDirectory = null);

        /// <summary>
        /// 弹出「另存为」对话框。
        /// </summary>
        /// <param name="defaultFileName">默认文件名。</param>
        /// <returns>选中的保存路径；用户取消返回 null。</returns>
        string SaveFile(string title, string filter = null, string defaultFileName = null, string initialDirectory = null);
    }
}
