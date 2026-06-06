using System;

namespace Ink_Canvas.WorkflowAutomation.Models
{
    /// <summary>
    /// 代表一个行动的注册信息。
    /// 对齐 ClassIsland 的 ActionRegistryInfo，Handle 和 RevertHandle 分离注册。
    /// </summary>
    public class ActionRegistryInfo
    {
        /// <summary>
        /// 行动 ID。
        /// </summary>
        public string Id { get; internal set; }

        /// <summary>
        /// 行动显示图标。
        /// </summary>
        public string IconKind { get; internal set; }

        /// <summary>
        /// 行动显示名称。
        /// </summary>
        public string Name { get; internal set; }

        /// <summary>
        /// 设置控件类型。
        /// </summary>
        public Type SettingsControlType { get; internal set; }

        /// <summary>
        /// 设置类型。
        /// </summary>
        public Type SettingsType { get; internal set; }

        public delegate void HandleDelegate(object settings, string guid);

        /// <summary>
        /// 行动处理程序
        /// </summary>
        public HandleDelegate Handle;

        /// <summary>
        /// 行动恢复处理程序
        /// </summary>
        public HandleDelegate RevertHandle;

        public ActionRegistryInfo(string id, string name = "", string iconKind = "BacteriaOutline")
        {
            Id = id;
            Name = string.IsNullOrEmpty(name) ? id : name;
            IconKind = iconKind;
        }
    }
}
