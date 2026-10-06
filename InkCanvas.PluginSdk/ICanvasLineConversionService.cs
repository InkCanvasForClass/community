using System;
using System.Windows;

namespace Ink_Canvas.Plugins
{
    public enum CanvasLineSource
    {
        GeometryLine = 0,
        AutoStraightenedInk = 1
    }

    /// <summary>
    /// 宿主完成全部笔迹后处理后发布的直线候选。候选令牌仅能成功消费一次。
    /// </summary>
    public sealed class CanvasLineFinalizedEventArgs : EventArgs
    {
        public string CandidateToken { get; set; }
        public string PageId { get; set; }
        public Point Start { get; set; }
        public Point End { get; set; }
        public CanvasLineSource Source { get; set; }
    }

    /// <summary>
    /// 允许插件把宿主最终直线与自己的结构化状态作为一个撤销项进行原子转换。
    /// </summary>
    public interface ICanvasLineConversionService
    {
        event EventHandler<CanvasLineFinalizedEventArgs> LineFinalized;

        /// <summary>
        /// 消费候选直线并提交一个复合历史项。非白板、冻结页、过期或重复令牌返回 false。
        /// 调用前插件应已应用 afterState；返回 false 时插件负责恢复 beforeState。
        /// </summary>
        bool TryConvertToPluginState(
            string pluginId,
            string candidateToken,
            string beforeState,
            string afterState);
    }
}
