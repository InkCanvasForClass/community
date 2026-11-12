using System;
using System.Collections.Generic;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 打开格式服务：供插件向宿主工具栏「打开」按钮的文件对话框注册额外的可打开格式。
    /// <para>
    /// 注册后，宿主对话框的「所有支持的文件」筛选项会包含插件扩展名，并为每组追加一个独立筛选项；
    /// 用户选中这些扩展名的文件时，宿主在 UI 线程调用对应 <c>handler</c>。
    /// </para>
    /// <para>
    /// 必须在插件 <c>Initialize</c> 阶段调用（与 <see cref="IPluginHost.RegisterService{T}"/> 约束一致），
    /// 插件卸载时宿主自动注销其全部格式。
    /// </para>
    /// <para>
    /// 宿主内置格式（.icstk/.xml/.zip/.uink）不能被覆盖；多个插件注册同一扩展名时先注册者生效，
    /// 后注册者的该扩展名被忽略并写警告日志。
    /// </para>
    /// </summary>
    public interface IOpenFileFormatService
    {
        /// <summary>
        /// 注册一组可打开格式。一个插件可多次调用以注册多组，例如「希沃白板课件」*.enb;*.enbx、「文档」*.docx;*.pdf。
        /// </summary>
        /// <param name="groupName">组显示名称，用作对话框筛选项名称。</param>
        /// <param name="extensions">扩展名列表（可带或不带点、忽略大小写），如 ".enb"、"enbx"。</param>
        /// <param name="handler">打开回调，参数为用户选中的文件完整路径；返回 <c>true</c> 表示已处理，
        /// 返回 <c>false</c> 或抛异常时宿主提示打开失败。在 UI 线程执行。</param>
        /// <returns>至少有一个扩展名注册成功时返回 <c>true</c>；不在 Initialize 阶段、参数无效或扩展名全部被拒绝时返回 <c>false</c>。</returns>
        bool RegisterFormat(string groupName, IEnumerable<string> extensions, Func<string, bool> handler);
    }
}
