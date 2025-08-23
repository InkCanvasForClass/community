using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InkCanvasForClass.IccInkCanvas.Settings {

    /// <summary>
    /// IccBoard的编辑模式
    /// </summary>
    public enum EditingMode {
        /// <summary>
        /// 仅显示墨迹，不会接收任何输入事件，没有HitTest
        /// </summary>
        None,
        /// <summary>
        /// 仅显示墨迹，不会接收任何输入事件，有HitTest
        /// </summary>
        NoneWithHitTest,
        /// <summary>
        /// 书写模式，该模式下允许临时切换到橡皮擦模式
        /// </summary>
        Writing,
        /// <summary>
        /// 墨迹擦模式
        /// </summary>
        StrokeErasing,
        /// <summary>
        /// 板擦模式
        /// </summary>
        GeometryErasing,
        /// <summary>
        /// 区域擦除模式
        /// </summary>
        AreaErasing,
        /// <summary>
        /// 墨迹选择模式
        /// </summary>
        Select,
        /// <summary>
        /// 仅显示墨迹，仅接受手势输入
        /// </summary>
        Gestures,
        /// <summary>
        /// 形状绘制模式，该模式不能被用户直接设置
        /// </summary>
        ShapeDrawing,
        /// <summary>
        /// 漫游模式
        /// </summary>
        RoamingMode
    }
}
