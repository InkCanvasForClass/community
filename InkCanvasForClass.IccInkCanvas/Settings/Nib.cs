using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InkCanvasForClass.IccInkCanvas.Settings {
    
    /// <summary>
    /// 定义了笔尖类型
    /// </summary>
    public enum NibType {
        /// <summary>
        /// 默认笔
        /// </summary>
        Default,
        /// <summary>
        /// 荧光笔
        /// </summary>
        Highlighter
    }

    /// <summary>
    /// 笔锋样式
    /// </summary>
    public enum StrokeNibStyle {
        /// <summary>
        /// 默认，直接根据输入设备提供的压力值正常渲染
        /// </summary>
        Default,
        /// <summary>
        /// 强制无笔锋
        /// </summary>
        Solid,
        /// <summary>
        /// 有笔锋，基于固定点集算法计算，触笔设备不套用算法
        /// </summary>
        Beautiful
    }
}
