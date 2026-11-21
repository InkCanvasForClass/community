using System;

namespace Ink_Canvas.Helpers.Motion
{
    /// <summary>
    /// 菜单动画的时钟规则：每帧最多推进多少、被打断时用哪个时长。
    /// </summary>
    /// <remarks>
    /// 曲线（<see cref="PopupCurve"/>）只回答"progress 对应多大的缩放"，
    /// 而"progress 每帧走多少"是另一件事，规则独立、且同样容易出错：
    ///
    ///   1) 单帧时长必须夹住上限，否则一次卡顿就能让 progress 一步跨过整个过冲区；
    ///   2) 打断时不能换时长，否则反向的那一瞬间速度突变 —— 用户描述打断是
    ///      "1-2-3-4 打断 4-3-2-1"，每一步等长，也就是"只翻方向、不改速度"。
    ///
    /// 这两条都是纯数值规则，与 WPF 无关，因此放在这里并做单测（同 SpringSolver）。
    /// </remarks>
    public static class PopupTiming
    {
        /// <summary>
        /// 单帧推进时长的上限（秒）。
        /// </summary>
        /// <remarks>
        /// 窗口被拖动、系统卡顿、调试器断点都会让一帧的 dt 变成上百毫秒。
        /// 不夹住的话 progress 一步跨过整个过冲区，曲线被"跳"过去，
        /// 观感是"过冲时有时无"——实测收起时就曾整段丢掉 1.05 的峰值。
        /// 40ms 相当于最低 25fps 的表现：宁可短暂整体变慢，也不丢曲线形状。
        /// </remarks>
        public const double MaxFrameSeconds = 0.04;

        /// <summary>
        /// 决定本次播放使用的时长。
        /// </summary>
        /// <param name="nominalSeconds">按方向定下的名义时长（打开 0.20s、收起 0.15s）。</param>
        /// <param name="currentSeconds">当前正在使用的时长。</param>
        /// <param name="isRunning">调用本方法时动画是否仍在跑（即这次是否属于打断）。</param>
        /// <returns>
        /// 打断时沿用当前时长（方向翻转、速度不变）；从静止起播时采用名义时长。
        /// </returns>
        public static double ResolveDuration(double nominalSeconds, double currentSeconds, bool isRunning)
        {
            if (!(nominalSeconds > 0.0)) return currentSeconds > 0.0 ? currentSeconds : MaxFrameSeconds;
            return isRunning ? currentSeconds : nominalSeconds;
        }
    }
}
