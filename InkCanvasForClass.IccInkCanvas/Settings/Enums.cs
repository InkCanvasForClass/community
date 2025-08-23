using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InkCanvasForClass.IccInkCanvas.Settings {
    public enum BoardPageAppendMode {
        /// <summary>
        /// 添加到所有页面的最后去
        /// </summary>
        AppendToListEnd,
        /// <summary>
        /// 添加为当前页面的下一页
        /// </summary>
        AppendAfterItem,
        /// <summary>
        /// 添加为当前页面的上一页
        /// </summary>
        AppendBeforeItem,
        /// <summary>
        /// 添加到所有页面的最前面去
        /// </summary>
        AppendToListStart
    }

    public enum PageSwitchMode {
        SwitchToPreviousPage,
        SwitchToNextPage,
    }

    public enum InputtingDeviceType {
        None,
        Mouse,
        Touch,
        Stylus
    }

    public enum EraserType {
        Rectangle,
        Ellipse,
    }

    internal enum CommitReason {
        /// <summary>
        /// 由用户输入
        /// </summary>
        UserInput,
        /// <summary>
        /// 由代码手动添加
        /// </summary>
        CodeInput,
        /// <summary>
        /// 形状绘制
        /// </summary>
        ShapeDrawing,
        /// <summary>
        /// 墨迹识别
        /// </summary>
        ShapeRecognition,
        /// <summary>
        /// 清空画布
        /// </summary>
        ClearingCanvas,
        /// <summary>
        /// 已过时，不再直接对Stroke进行Transform来实现Manipulation
        /// </summary>
        [Obsolete]
        Manipulation
    }
}
