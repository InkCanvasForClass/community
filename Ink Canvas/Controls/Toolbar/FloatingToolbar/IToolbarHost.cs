using System.Windows;

namespace Ink_Canvas.Controls.Toolbar.FloatingToolbar
{
    /// <summary>
    /// 工具栏按钮插件与宿主之间的桥梁。Phase 1 粗粒度暴露 MainWindow，后续收窄。
    /// </summary>
    public interface IToolbarHost
    {
        MainWindow Window { get; }

        /// <summary>按 id 登记按钮的 view 实例（供 MainWindow 字段回填和互相查找）。</summary>
        void RegisterView(string id, FrameworkElement view);

        /// <summary>按 id 获取之前注册的 view。不存在返回 null。</summary>
        FrameworkElement FindView(string id);
    }
}