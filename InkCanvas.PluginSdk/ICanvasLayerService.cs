using System;
using System.Windows;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 插件画布图层相对于普通墨迹层的位置。
    /// </summary>
    public enum CanvasLayerPlacement
    {
        BelowInk = 0,
        AboveInk = 1,
        Adorner = 2
    }

    /// <summary>
    /// 插件画布图层服务。图层 ID 在同一插件内必须唯一；插件卸载时宿主会统一清理。
    /// </summary>
    public interface ICanvasLayerService
    {
        /// <summary>
        /// 注册或替换插件图层。工厂始终在 UI 线程调用。
        /// </summary>
        void RegisterLayer(
            string pluginId,
            string layerId,
            CanvasLayerPlacement placement,
            Func<FrameworkElement> layerFactory,
            bool isHitTestVisible = false);

        /// <summary>移除插件的指定图层。</summary>
        bool RemoveLayer(string pluginId, string layerId);

        /// <summary>移除插件注册的全部图层。</summary>
        void RemoveLayers(string pluginId);
    }
}
