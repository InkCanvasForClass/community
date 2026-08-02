using System.Collections.Generic;
using System.Windows.Input;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 快捷键服务，供插件注册自定义全局热键。
    /// </summary>
    public interface IHotkeyService
    {
        /// <summary>
        /// 注册全局热键。
        /// </summary>
        /// <param name="id">热键唯一标识</param>
        /// <param name="modifiers">修饰键组合（Ctrl=2, Alt=1, Shift=4, Win=8）</param>
        /// <param name="key">虚拟键码（如 0x42 = B）</param>
        /// <param name="callback">按下时的回调</param>
        /// <returns>是否注册成功</returns>
        bool Register(string id, uint modifiers, uint key, System.Action callback);

        /// <summary>
        /// 注销全局热键。
        /// </summary>
        bool Unregister(string id);

        /// <summary>
        /// 检查热键是否已注册。
        /// </summary>
        bool IsRegistered(string id);

        /// <summary>
        /// 获取宿主当前已注册的全部热键（含内置热键）的只读描述。
        /// </summary>
        IReadOnlyList<PluginHotkeyInfo> GetRegisteredHotkeys();

        /// <summary>
        /// 更新宿主内置热键的按键组合（按热键名称，如 "Undo"、"Redo"）。
        /// </summary>
        /// <returns>是否更新成功。</returns>
        bool UpdateHotkey(string hotkeyName, Key key, ModifierKeys modifiers);

        /// <summary>启用宿主热键注册（恢复响应）。</summary>
        void EnableRegistration();

        /// <summary>停用宿主热键注册（所有热键暂停响应）。</summary>
        void DisableRegistration();
    }

    /// <summary>
    /// 热键信息（只读描述，不含回调）。
    /// </summary>
    public sealed class PluginHotkeyInfo
    {
        /// <summary>热键名称（如 "Undo"）。</summary>
        public string Name { get; set; } = "";

        /// <summary>主键。</summary>
        public Key Key { get; set; }

        /// <summary>修饰键组合。</summary>
        public ModifierKeys Modifiers { get; set; }
    }
}
