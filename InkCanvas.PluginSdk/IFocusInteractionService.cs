namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 让插件的临时交互界面在宿主无焦点模式下仍可接收输入。
    /// 同一插件重复设置为 active 不会重复计数；关闭或卸载时必须设置为 false。
    /// </summary>
    public interface IFocusInteractionService
    {
        void SetActive(string pluginId, bool active);
    }
}
