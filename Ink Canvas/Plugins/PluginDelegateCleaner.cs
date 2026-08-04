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
        /// <param name="depth">
        /// 允许继续下探的层数。宿主的回调往往不直接挂在服务对象上，而是转交给内部管理器
        /// （如 <c>HotkeyService → GlobalHotkeyManager._registeredHotkeys</c>），
        /// 因此需要向内递归若干层才能摘干净。
        /// </param>
        public static int Sweep(object target, AssemblyLoadContext context, int depth = 3)
        {
            return Sweep(target, context, depth, new HashSet<object>(ReferenceEqualityComparer.Instance));
        }

        private static int Sweep(object target, AssemblyLoadContext context, int depth, HashSet<object> visited)
        {
            if (target == null || context == null || depth < 0) return 0;
            // 对象图存在环（服务持有 MainWindow、MainWindow 又持有服务），必须去重防止无限递归。
            if (!visited.Add(target)) return 0;

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
                        else if (typeof(IList).IsAssignableFrom(field.FieldType))
                        {
                            removed += SweepListField(target, field, context);
                        }
                        else if (depth > 0 && ShouldDescendInto(field.FieldType))
                        {
                            removed += Sweep(field.GetValue(target), context, depth - 1, visited);
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
        /// 是否值得向该字段内部递归。只下探宿主自己的引用类型：
        /// 系统类型（string/集合基元等）与 WPF 可视化树不会存放插件回调的宿主注册表，
        /// 盲目深挖只会拖慢卸载并可能触碰到有副作用的属性。
        /// </summary>
        private static bool ShouldDescendInto(Type fieldType)
        {
            if (fieldType.IsPrimitive || fieldType.IsEnum || fieldType.IsValueType) return false;
            if (fieldType == typeof(string) || fieldType == typeof(object)) return false;

            var ns = fieldType.Namespace;
            if (string.IsNullOrEmpty(ns)) return false;

            // 只走宿主与 SDK 自己的类型；System.*/Microsoft.* 等一律跳过。
            return ns.StartsWith("Ink_Canvas", StringComparison.Ordinal)
                || ns.StartsWith("InkCanvas", StringComparison.Ordinal);
        }

        /// <summary>
        /// 移除列表中携带插件委托的元素。覆盖通知历史 <c>List&lt;NotificationMessage&gt;</c>
        /// 这类「数据对象里挂着 Action 回调」的结构。
        /// </summary>
        private static int SweepListField(object target, FieldInfo field, AssemblyLoadContext context)
        {
            if (!(field.GetValue(target) is IList list) || list.Count == 0) return 0;
            if (list.IsReadOnly || list.IsFixedSize) return 0;

            var removed = 0;
            for (var i = list.Count - 1; i >= 0; i--)
            {
                if (!ValueHoldsPluginDelegate(list[i], context)) continue;
                list.RemoveAt(i);
                removed++;
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

        /// <summary>
        /// 摘除静态事件上的插件订阅。宿主有若干 <c>static event</c>
        /// （如 <see cref="Ink_Canvas.Helpers.NotificationCenterService.NotificationRequested"/>、
        /// <c>ClipboardNotification.ClipboardUpdate</c>），插件经服务包装订阅后同样会钉住 ALC，
        /// 而静态字段不属于任何实例，实例扫描覆盖不到。
        /// </summary>
        public static int SweepStaticEvents(Type type, AssemblyLoadContext context)
        {
            if (type == null || context == null) return 0;

            var removed = 0;
            foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!typeof(Delegate).IsAssignableFrom(field.FieldType)) continue;

                try
                {
                    removed += SweepDelegateField(null, field, context);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile(
                        $"PluginDelegateCleaner: 清理静态事件 {type.Name}.{field.Name} 失败: {ex.Message}",
                        LogHelper.LogType.Warning);
                }
            }

            return removed;
        }
    }
}
