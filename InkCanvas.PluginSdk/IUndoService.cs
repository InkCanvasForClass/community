using System;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 将插件的结构化状态快照接入宿主统一撤销/重做链路。
    /// </summary>
    public interface IUndoService
    {
        /// <summary>注册插件状态恢复处理器。每个插件只能注册一个处理器。</summary>
        void RegisterStateHandler(string pluginId, Action<string> restoreState);

        /// <summary>移除插件状态恢复处理器。</summary>
        void UnregisterStateHandler(string pluginId);

        /// <summary>
        /// 提交一次结构化状态变化。冻结页面会拒绝提交；相同快照不会生成历史项。
        /// </summary>
        bool CommitState(string pluginId, string beforeState, string afterState);
    }
}
