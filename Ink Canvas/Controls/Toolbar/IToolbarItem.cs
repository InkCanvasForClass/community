using System.Windows;

namespace Ink_Canvas.Controls.Toolbar
{
    /// <summary>
    /// 一个工具栏按钮（或任意浮动栏/白板栏条目）的插件化契约。
    /// 实现类必须有无参构造函数，启动时会被 ToolbarRegistry 反射实例化。
    /// </summary>
    public interface IToolbarItem
    {
        /// <summary>稳定、唯一的 id，用于持久化用户配置。不要随便改。</summary>
        string Id { get; }

        ToolbarSlot DefaultSlot { get; }

        /// <summary>同一 slot 内的默认顺序，小的在前。</summary>
        int DefaultOrder { get; }

        bool DefaultVisible { get; }

        ToolbarInsertPosition DefaultPosition { get; }

        /// <summary>仅当 Position 为 BeforeAnchor/AfterAnchor 时有意义，对应 XAML 里 x:Name。</summary>
        string DefaultAnchorName { get; }

        /// <summary>构造 UI 元素并接线所有行为。</summary>
        FrameworkElement BuildView(IToolbarHost host);
    }
}