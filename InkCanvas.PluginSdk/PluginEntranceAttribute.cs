using System;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 标记插件入口类。PluginManager 会优先查找带有此特性的类作为插件入口。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class PluginEntranceAttribute : Attribute
    {
    }
}
