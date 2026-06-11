using Ink_Canvas.WorkflowAutomation.Models;
using System.Collections.Generic;

namespace Ink_Canvas.WorkflowAutomation.Abstractions
{
    /// <summary>
    /// 行动服务接口。
    /// 对齐 ClassIsland 的 IActionService。
    /// </summary>
    public interface IActionService
    {
        /// <summary>
        /// 已注册的行动字典
        /// </summary>
        static Dictionary<string, ActionRegistryInfo> Actions { get; } = new();

        /// <summary>
        /// 注册行动处理程序
        /// </summary>
        void RegisterActionHandler(string id, ActionRegistryInfo.HandleDelegate handler);

        /// <summary>
        /// 注册行动恢复处理程序
        /// </summary>
        void RegisterRevertHandler(string id, ActionRegistryInfo.HandleDelegate handler);

        /// <summary>
        /// 触发行动组
        /// </summary>
        void Invoke(ActionSet actionSet);

        /// <summary>
        /// 恢复行动组
        /// </summary>
        void Revert(ActionSet actionSet);

        /// <summary>
        /// 行动是否有内建的恢复
        /// </summary>
        bool ExistRevertHandler(Models.Action action);
    }
}
