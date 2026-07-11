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
    }
}
