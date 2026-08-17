using System;
using System.Collections.Generic;

namespace Ink_Canvas.Ink.WetInk
{
    /// <summary>
    /// 四边红外触摸设备重点：接触尺寸 → 手指/手掌分类。
    ///
    /// 公式与旧 BuildPalmRoutePolicy / TryGetPalmEraserWidth 完全一致（设置不变）：
    ///   boundWidth = IsQuadIR ? √(w·h) : w
    ///   阈值 = BoundsWidth × ThresholdFactor × SensitivityMultiplier
    ///   手掌宽 = boundWidth × EraserSizeFactor × (IsSpecialScreen ? TouchMultiplier : 1)
    ///
    /// 手指 < 阈值 → 写墨（InkPresenter 处理）；手掌 ≥ 阈值 → 由调用方对干层执行擦除。
    /// 仅在覆盖窗口收到 WM_POINTER 时喂入接触尺寸；墨迹采集/渲染本身交给 InkPresenter。
    /// </summary>
    internal sealed class WetInkTouchClassifier
    {
        private readonly HashSet<uint> _palmContacts = new HashSet<uint>();
        private readonly HashSet<uint> _fingerContacts = new HashSet<uint>();

        /// <summary>当前是否有手掌接触（用于驱动干层擦除）。</summary>
        public bool HasActivePalm => _palmContacts.Count > 0;

        public int PalmContactCount => _palmContacts.Count;

        /// <summary>
        /// 对单个指针接触做分类。deviceIsTouch=false（笔/鼠标）永远不是手掌。
        /// </summary>
        public WetInkContactKind Classify(
            WetInkPalmPolicy policy,
            uint pointerId,
            bool deviceIsTouch,
            double contactWidthDip,
            double contactHeightDip)
        {
            if (!deviceIsTouch)
            {
                _palmContacts.Remove(pointerId);
                return WetInkContactKind.Pen;
            }

            if (!policy.Enabled)
            {
                _fingerContacts.Add(pointerId);
                return WetInkContactKind.Finger;
            }

            // 特殊屏且 TouchMultiplier==0 时禁用手掌擦除（与旧逻辑一致）。
            if (policy.IsSpecialScreen && policy.TouchMultiplier == 0)
            {
                _fingerContacts.Add(pointerId);
                return WetInkContactKind.Finger;
            }

            var boundWidth = policy.IsQuadIr
                ? Math.Sqrt(Math.Max(0, contactWidthDip * contactHeightDip))
                : contactWidthDip;

            var threshold = policy.BoundsWidthDip
                            * policy.ThresholdFactor
                            * policy.SensitivityMultiplier;

            if (boundWidth <= policy.BoundsWidthDip || boundWidth <= threshold)
            {
                _palmContacts.Remove(pointerId);
                _fingerContacts.Add(pointerId);
                return WetInkContactKind.Finger;
            }

            _fingerContacts.Remove(pointerId);
            _palmContacts.Add(pointerId);
            return WetInkContactKind.Palm;
        }

        /// <summary>接触抬起，清理该指针的跟踪状态。</summary>
        public void OnPointerUp(uint pointerId)
        {
            _palmContacts.Remove(pointerId);
            _fingerContacts.Remove(pointerId);
        }

        /// <summary>手掌擦除宽度（DIP），用于干层擦除半径。</summary>
        public double GetPalmEraserWidthDip(
            WetInkPalmPolicy policy,
            double contactWidthDip,
            double contactHeightDip)
        {
            var boundWidth = policy.IsQuadIr
                ? Math.Sqrt(Math.Max(0, contactWidthDip * contactHeightDip))
                : contactWidthDip;
            return boundWidth
                   * policy.EraserSizeFactor
                   * (policy.IsSpecialScreen ? policy.TouchMultiplier : 1);
        }

        /// <summary>清空所有跟踪状态（引擎关闭/失活时调用）。</summary>
        public void Reset()
        {
            _palmContacts.Clear();
            _fingerContacts.Clear();
        }
    }
}
