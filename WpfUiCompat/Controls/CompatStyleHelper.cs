using System;
using System.Windows;

namespace WpfUiCompat.Controls
{
    /// <summary>
    /// 兼容控件样式挂接助手。
    /// WPF 隐式样式查找使用元素的实际类型（GetType）精确匹配：继承 WPF-UI 控件后，
    /// 基类字典中 TargetType=基类类型 的隐式样式不会应用到派生类。
    /// 兼容控件在构造器中调用 <see cref="AttachBaseStyle"/> 显式挂接基类样式；
    /// 若 XAML 后续显式设置 Style，将自然覆盖此处的本地值。
    /// </summary>
    internal static class CompatStyleHelper
    {
        /// <summary>
        /// 将基类类型的隐式样式（位于应用级合并字典）赋给元素 Style（若当前未设置）。
        /// </summary>
        /// <param name="element">兼容控件实例。</param>
        /// <param name="baseStyleType">提供隐式样式的基类类型。</param>
        internal static void AttachBaseStyle(FrameworkElement element, Type baseStyleType)
        {
            try
            {
                if (element == null || element.Style != null) return;
                if (element.TryFindResource(baseStyleType) is Style baseStyle)
                {
                    element.Style = baseStyle;
                }
            }
            catch
            {
            }
        }
    }
}