using System;
using System.Collections.Generic;
using System.Windows.Controls.Primitives;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 菜单动画专用日志。
    /// </summary>
    /// <remarks>
    /// 与通用的 LogHelper 分两类考虑：
    /// - 通用日志每次会捕调用栈 + 做文件权限操作 + 开关文件，开销很大；
    /// - 动画路径一旦高频写日志，会直接拖垮帧率（此前就发生过）。
    ///
    /// 因此这里做了两层约束：
    /// 1) 只在**状态变化点**记录（打开/收起/完成/打断），不记录逐帧数据；
    /// 2) 每个菜单每秒最多写若干条，超限静默丢弃，避免用户快速连点时爆量。
    ///
    /// 日志开关：<see cref="Enabled"/>，排查完可置 false 停止写入（不影响功能）。
    /// </remarks>
    internal static class AnimationLog
    {
        /// <summary>是否启用动画日志。</summary>
        internal static bool Enabled = true;

        /// <summary>同一个菜单每秒最多写入的条数。</summary>
        private const int MaxLinesPerSecondPerPopup = 8;

        private static readonly Dictionary<string, (long Second, int Count)> RateGuard =
            new Dictionary<string, (long, int)>();

        public static void Log(Popup popup, string phase, string detail = null)
        {
            if (!Enabled) return;

            var name = string.IsNullOrEmpty(popup?.Name) ? "<unnamed>" : popup.Name;

            // 频率闸门：同一菜单同一秒内超限则丢弃
            long nowSecond = Environment.TickCount64 / 1000;
            if (RateGuard.TryGetValue(name, out var state))
            {
                if (state.Second == nowSecond)
                {
                    if (state.Count >= MaxLinesPerSecondPerPopup) return;
                    state.Count++;
                }
                else
                {
                    state = (nowSecond, 1);
                }
            }
            else
            {
                state = (nowSecond, 1);
            }
            RateGuard[name] = state;

            var message = detail == null
                ? $"[PopupAnim] {name} {phase}"
                : $"[PopupAnim] {name} {phase} | {detail}";

            LogHelper.WriteLogToFile(message, LogHelper.LogType.Info);
        }
    }
}
