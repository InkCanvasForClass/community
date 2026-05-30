using System.Collections.Generic;
using System.Windows;

namespace Ink_Canvas.Controls.Toolbar.FloatingToolbar
{
    /// <summary>
    /// MainWindow 版的 IToolbarHost 实现。Phase 1 直接把 MainWindow 引用暴露给插件，
    /// 插件可通过 host.Window 访问私有/内部成员（partial class 扩展或 internal 字段）。
    /// 后续阶段逐步把具体行为抽成 Host 上的方法/事件，收窄这个接口。
    /// </summary>
    public sealed class ToolbarHost : IToolbarHost
    {
        private readonly Dictionary<string, FrameworkElement> _views = new Dictionary<string, FrameworkElement>();

        public ToolbarHost(MainWindow window)
        {
            Window = window;
        }

        public MainWindow Window { get; }

        public void RegisterView(string id, FrameworkElement view)
        {
            if (string.IsNullOrEmpty(id) || view == null) return;
            _views[id] = view;
        }

        public FrameworkElement FindView(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return _views.TryGetValue(id, out var v) ? v : null;
        }
    }
}