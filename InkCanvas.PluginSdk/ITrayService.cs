using System;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 系统托盘服务：允许插件控制宿主托盘图标的显隐、主窗口的显隐、
    /// 打开托盘右键菜单，以及向托盘右键菜单注入/移除自己的菜单项。
    /// <para>
    /// 所有方法都可以从任意线程调用，宿主内部会切换到 UI 线程。
    /// 注入的菜单项会插入到宿主固定菜单区（隐藏窗口/重启/关闭等）之间，
    /// 不会破坏宿主菜单的动态状态更新。
    /// </para>
    /// </summary>
    public interface ITrayService
    {
        /// <summary>
        /// 托盘图标是否可见。写入时直接控制宿主托盘图标显隐；
        /// 注意宿主自身有「启用托盘图标」设置（<c>Settings.Appearance.EnableTrayIcon</c>），
        /// 宿主设置关闭时会再次隐藏图标，插件显隐只作为叠加控制。
        /// </summary>
        bool IsIconVisible { get; set; }

        /// <summary>
        /// 主窗口是否可见（写入时同步托盘菜单里「隐藏主窗口」的勾选状态）。
        /// </summary>
        bool IsMainWindowVisible { get; set; }

        /// <summary>打开托盘右键菜单。</summary>
        void ShowContextMenu();

        /// <summary>
        /// 向托盘右键菜单注入一个菜单项，插入到宿主固定菜单区之间。
        /// </summary>
        /// <param name="id">菜单项唯一标识，用于后续移除/查重。</param>
        /// <param name="text">菜单项显示文本。</param>
        /// <param name="onClicked">点击回调（在 UI 线程触发）。</param>
        /// <returns>是否成功；<paramref name="id"/> 已存在或参数无效时返回 false。</returns>
        bool AddMenuItem(string id, string text, Action onClicked);

        /// <summary>移除之前注入的托盘菜单项。</summary>
        /// <returns>是否移除成功。</returns>
        bool RemoveMenuItem(string id);

        /// <summary>检查指定 id 的托盘菜单项是否存在。</summary>
        bool HasMenuItem(string id);

        /// <summary>托盘图标左键按下时触发（宿主按用户设置的左键行为执行默认动作后触发）。</summary>
        event Action LeftClicked;

        /// <summary>托盘图标右键按下时触发（宿主按用户设置的右键行为执行默认动作后触发）。</summary>
        event Action RightClicked;
    }
}
