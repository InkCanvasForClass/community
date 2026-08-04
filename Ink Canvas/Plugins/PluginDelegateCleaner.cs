using Ink_Canvas.Helpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Loader;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 从宿主对象上摘除某个插件 ALC 提供的委托。
    /// <para>
    /// 插件通过 <c>+=</c> 订阅宿主服务的事件（<see cref="IEventService"/> 等），或把回调塞进
    /// 宿主的字典（热键、托盘菜单）。这些订阅没有插件身份信息，宿主无从按插件精确退订，
    /// 而只要留下一个，可回收 ALC 就不会释放、热重载即告失败。
    /// </para>
    /// <para>
    /// 这里按「委托的实现方法定义在哪个程序集」来判定归属：属于正在卸载的 ALC 就摘掉，
    /// 其它插件和宿主自己的订阅原样保留。
    /// </para>
    /// </summary>
    internal static class PluginDelegateCleaner
    {
        private const BindingFlags AllInstance =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>
        /// 判断委托是否由 <paramref name="context"/> 中的程序集提供。
        /// 多播委托只要有任一分支属于该 ALC 即视为属于它。
        /// </summary>
        public static bool IsOwnedBy(Delegate handler, AssemblyLoadContext context)
        {
            if (handler == null || context == null) return false;

            foreach (var branch in handler.GetInvocationList())
            {
                var declaringType = branch.Method?.DeclaringType;
                if (declaringType == null) continue;

                // 闭包/lambda 的 DeclaringType 是编译器生成的类型，同样落在插件程序集里，
                // 因此这一判定对 `() => ...` 形式的回调同样成立。
                if (AssemblyLoadContext.GetLoadContext(declaringType.Assembly) == context)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 扫描 <paramref name="target"/> 的所有实例字段，摘除属于 <paramref name="context"/> 的委托：
        /// 委托字段（含 event 的后备字段）整体重建为只保留非插件分支；
        /// 字典/列表中的元素若含插件委托则整条移除。
        /// </summary>
        public static int Sweep(object target, AssemblyLoadContext context)
        {
            if (target == null || context == null) return 0;

            var removed = 0;
            var type = target.GetType();

            while (type != null && type != typeof(object))
            {
                foreach (var field in type.GetFields(AllInstance))
                {
                    if (field.IsStatic) continue;

                    try
                    {
                        if (typeof(Delegate).IsAssignableFrom(field.FieldType))
                        {
                            removed += SweepDelegateField(target, field, context);
                        }
                        else if (typeof(IDictionary).IsAssignableFrom(field.FieldType))
                        {
                            removed += SweepDictionaryField(target, field, context);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLogToFile(
                            $"PluginDelegateCleaner: 清理 {type.Name}.{field.Name} 失败: {ex.Message}",
                            LogHelper.LogType.Warning);
                    }
                }

                type = type.BaseType;
            }

            return removed;
        }

        /// <summary>
        /// 重建委托字段，仅保留不属于该 ALC 的调用分支。
        /// </summary>
        private static int SweepDelegateField(object target, FieldInfo field, AssemblyLoadContext context)
        {
            if (!(field.GetValue(target) is Delegate current)) return 0;

            var survivors = new List<Delegate>();
            var removed = 0;

            foreach (var branch in current.GetInvocationList())
            {
                if (IsOwnedBy(branch, context)) removed++;
                else survivors.Add(branch);
            }

            if (removed == 0) return 0;

            field.SetValue(target, survivors.Count == 0 ? null : Delegate.Combine(survivors.ToArray()));
            return removed;
        }

        /// <summary>
        /// 移除字典中值（或值元组的任一字段）含插件委托的条目。
        /// 覆盖热键表 <c>Dictionary&lt;string, (uint, uint, Action)&gt;</c> 这类结构。
        /// </summary>
        private static int SweepDictionaryField(object target, FieldInfo field, AssemblyLoadContext context)
        {
            if (!(field.GetValue(target) is IDictionary dictionary) || dictionary.Count == 0) return 0;

            var doomedKeys = new List<object>();
            foreach (DictionaryEntry entry in dictionary)
            {
                if (ValueHoldsPluginDelegate(entry.Value, context))
                    doomedKeys.Add(entry.Key);
            }

            foreach (var key in doomedKeys)
                dictionary.Remove(key);

            return doomedKeys.Count;
        }

        /// <summary>
        /// 判断一个字典值是否携带插件委托：值本身是委托，或值是含委托字段的结构体/对象（如值元组）。
        /// </summary>
        private static bool ValueHoldsPluginDelegate(object value, AssemblyLoadContext context)
        {
            if (value == null) return false;

            if (value is Delegate direct) return IsOwnedBy(direct, context);

            var valueType = value.GetType();
            // 只下探一层：值元组与简单记录已足够覆盖宿主现有的回调表，避免深度递归带来的意外开销。
            if (valueType.IsPrimitive || valueType == typeof(string)) return false;

            foreach (var field in valueType.GetFields(AllInstance))
            {
                if (!typeof(Delegate).IsAssignableFrom(field.FieldType)) continue;
                if (field.GetValue(value) is Delegate nested && IsOwnedBy(nested, context))
                    return true;
            }

            return false;
        }
    }
}
