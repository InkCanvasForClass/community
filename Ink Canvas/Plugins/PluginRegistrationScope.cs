using System;
using System.Collections.Generic;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 单个插件在宿主中留下的注册痕迹。插件每向宿主注册一样东西（工具栏组件、IPC 处理器、
    /// DI 服务、托盘菜单项等），就在这里压入一个撤销动作；卸载时逆序执行，把宿主恢复到
    /// 插件加载前的状态。
    /// <para>
    /// 这是热重载能成立的前提：<see cref="System.Runtime.Loader.AssemblyLoadContext.Unload"/>
    /// 只是"请求"卸载，只要宿主还有任何一个字段指向插件程序集里的对象或委托，ALC 就不会真正释放，
    /// DLL 文件也就一直被占用。撤销注册就是为了断掉这些引用。
    /// </para>
    /// </summary>
    internal sealed class PluginRegistrationScope
    {
        private readonly List<(string Description, Action Undo)> _undoActions
            = new List<(string, Action)>();

        /// <summary>
        /// 插件在 DI 容器中注册过的服务类型。卸载时需要从 ServiceCollection 中剔除并重建容器。
        /// </summary>
        public List<Type> RegisteredServiceTypes { get; } = new List<Type>();

        public string PluginId { get; }

        public PluginRegistrationScope(string pluginId)
        {
            PluginId = pluginId;
        }

        /// <summary>
        /// 记录一个撤销动作。<paramref name="description"/> 仅用于卸载失败时的日志定位。
        /// </summary>
        public void Track(string description, Action undo)
        {
            if (undo == null) return;
            _undoActions.Add((description ?? "(unnamed)", undo));
        }

        /// <summary>
        /// 逆序执行所有撤销动作。单个动作抛异常不会中断其余撤销——少撤一个就少断一条引用，
        /// 会直接导致 ALC 卸载失败，所以必须尽最大努力全部执行完。
        /// </summary>
        /// <param name="onError">撤销失败时的回调，用于写日志。</param>
        public void UndoAll(Action<string, Exception> onError)
        {
            for (var i = _undoActions.Count - 1; i >= 0; i--)
            {
                var (description, undo) = _undoActions[i];
                try
                {
                    undo();
                }
                catch (Exception ex)
                {
                    onError?.Invoke(description, ex);
                }
            }

            _undoActions.Clear();
        }
    }
}
