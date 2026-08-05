using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 画布元素服务：允许插件把任意 WPF 控件作为「元素」插入主画布，
    /// 复用宿主对图片/媒体元素的完整交互（拖动、缩放、旋转、触摸、撤销历史、冻结页保护）。
    /// <para>
    /// 典型用法（如单词卡片、可点击的教具控件）：
    /// <list type="number">
    /// <item>用 XAML/C# 构造一个 <see cref="FrameworkElement"/> 控件（Grid/StackPanel/Border 等）；</item>
    /// <item>调用 <see cref="InsertElement(FrameworkElement)"/> 插入画布（居中、自动尺寸、进撤销历史、切选择模式）；</item>
    /// <item>用户可像拖动图片一样拖动/缩放/旋转该控件，并按 Ctrl+Z 撤销插入；</item>
    /// <item>控件内部的按钮/滑块等交互子元素，在选中模式下可正常点击（宿主已做命中穿透处理）。</item>
    /// </list>
    /// </para>
    /// <para>
    /// 所有方法都可以从任意线程调用，宿主内部会切换到 UI 线程。
    /// 元素只存在于当前会话的画布/页面历史中，不会随 .ink/.elements.json 持久化到磁盘，
    /// 翻页后由页面历史恢复到画布，但交互/事件绑定会随会话重建。
    /// </para>
    /// </summary>
    public interface ICanvasElementService
    {
        /// <summary>
        /// 把控件插入当前画布：居中放置（最大为画布 70%，无显式尺寸时按内容测量），
        /// 写入 TimeMachine 历史（可按 Ctrl+Z 撤销），并切换到选择模式。
        /// 当前页冻结时返回 <c>false</c>；传入 <c>null</c> 或控件已有父容器时返回 <c>false</c>。
        /// </summary>
        bool InsertElement(FrameworkElement element);

        /// <summary>
        /// 把控件插入当前画布，使控件左上角对齐到 <paramref name="position"/>（画布坐标），
        /// 不缩放、不居中。其余行为同 <see cref="InsertElement(FrameworkElement)"/>。
        /// </summary>
        bool InsertElement(FrameworkElement element, Point position);

        /// <summary>
        /// 从当前画布移除指定控件，并写入撤销历史（可按 Ctrl+Z 恢复）。
        /// 控件不在画布上时返回 <c>false</c>。
        /// </summary>
        bool TryRemoveElement(FrameworkElement element);

        /// <summary>指定控件当前是否位于画布上。</summary>
        bool ContainsElement(FrameworkElement element);

        /// <summary>当前画布上全部元素控件的只读快照（元素本身即画布坐标下可直接使用的实例）。</summary>
        IReadOnlyList<FrameworkElement> GetElements();
    }
}
