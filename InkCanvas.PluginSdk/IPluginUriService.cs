using System;
using System.Collections.Generic;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// URI 服务：供插件注册深链接处理程序，或主动打开 <c>icc://</c> 深链接。
    /// <para>
    /// 注册后，宿主会把形如 <c>icc://plugin/&lt;pluginId&gt;/&lt;path&gt;?&lt;query&gt;</c> 的深链接
    /// 派发给对应插件注册的处理器。子路径按「/」分段做最长前缀匹配（忽略大小写），
    /// 注册空字符串 <c>""</c> 表示接收该插件全部子路径。
    /// </para>
    /// <para>
    /// 处理器与 <see cref="OpenUri"/> 均在 UI 线程执行，可安全操作画布/窗口等宿主对象。
    /// </para>
    /// </summary>
    public interface IPluginUriService
    {
        /// <summary>
        /// 注册 URI 处理程序。
        /// <para>
        /// 注册后，<c>icc://plugin/&lt;本插件ID&gt;/&lt;subPath&gt;</c> 会调用 <paramref name="handler"/>。
        /// 应在插件 <c>Initialize</c> 阶段调用（与 <see cref="IPluginHost.RegisterService{T}"/> 约束一致）。
        /// </para>
        /// </summary>
        /// <param name="subPath">子路径（去 <c>plugin/&lt;id&gt;/</c> 前缀），空字符串表示接收全部子路径。</param>
        /// <param name="handler">处理器；返回 <c>true</c> 表示已处理，返回 <c>false</c> 时宿主记录「未处理」日志。</param>
        void RegisterHandler(string subPath, Func<PluginUriRequest, bool> handler);

        /// <summary>
        /// 主动打开一个 <c>icc://</c> 深链接（应用命令或其它插件的 URI）。
        /// <para>受宿主设置「启用 URI 协议」控制：设置关闭时调用不生效。</para>
        /// </summary>
        /// <param name="uri">形如 <c>icc://settings/CanvasPage?key=xxx</c> 或 <c>icc://plugin/&lt;id&gt;/&lt;path&gt;</c>。</param>
        /// <returns>是否已受理（已进入路由，不代表命令一定成功）。</returns>
        bool OpenUri(string uri);
    }

    /// <summary>
    /// 插件 URI 请求。宿主解析 <c>icc://plugin/&lt;pluginId&gt;/&lt;path&gt;</c> 后构造，传递给注册的处理器。
    /// </summary>
    public class PluginUriRequest
    {
        /// <summary>目标插件 ID（来自 URI，忽略大小写匹配）。</summary>
        public string PluginId { get; set; }

        /// <summary>插件子路径（去除 <c>/plugin/&lt;id&gt;/</c> 前缀，小写）。</summary>
        public string Path { get; set; }

        /// <summary>查询参数（键忽略大小写，值已 URL 解码）。</summary>
        public IReadOnlyDictionary<string, string> Query { get; set; }

        /// <summary>原始 URI 字符串。</summary>
        public string RawUri { get; set; }
    }
}
