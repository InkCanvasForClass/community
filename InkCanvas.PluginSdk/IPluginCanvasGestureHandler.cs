using System.Windows.Input;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 画布双指手势处理器：宿主把 InkCanvas 上的操作（Manipulation）事件转发给插件，
    /// 用于实现插件背景层的双指缩放/平移，并让墨迹与背景同步。
    /// <para>
    /// 宿主在以下时机回调（均发生在 UI 线程）：
    /// <list type="number">
    /// <item><see cref="OnCanvasGestureStarting"/> — 操作即将开始，返回 <c>true</c> 表示插件接管，
    ///     此时插件应在 <see cref="ManipulationStartingEventArgs.Mode"/> 里声明需要的手势类型
    ///     （如 <see cref="ManipulationModes.Scale"/> | <see cref="ManipulationModes.Translate"/>）；</item>
    /// <item><see cref="OnCanvasGestureDelta"/> — 操作增量，返回 <c>true</c> 表示插件已处理，
    ///     宿主将跳过默认的墨迹/画布变换；</item>
    /// <item><see cref="OnCanvasGestureCompleted"/> — 操作结束，宿主的编辑模式恢复由宿主照常处理。</item>
    /// </list>
    /// 不参与手势时应返回 <c>false</c>，让宿主走默认行为（书写/选择/橡皮擦等）。
    /// </para>
    /// </summary>
    public interface IPluginCanvasGestureHandler
    {
        /// <summary>操作即将开始。返回 <c>true</c> 表示插件接管该次操作。</summary>
        bool OnCanvasGestureStarting(ManipulationStartingEventArgs e);

        /// <summary>操作增量。返回 <c>true</c> 表示插件已处理，宿主跳过默认变换。</summary>
        bool OnCanvasGestureDelta(ManipulationDeltaEventArgs e);

        /// <summary>操作结束。宿主清理照常进行，这里只做插件自身的收尾。</summary>
        void OnCanvasGestureCompleted(ManipulationCompletedEventArgs e);
    }
}
