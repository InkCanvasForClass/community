using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Ink_Canvas.Helpers.Motion;

namespace Ink_Canvas.Helpers
{
    /// <summary>
    /// 为 Popup 的缩放动画预留溢出空间，并补偿因预留而改变的定位。
    /// </summary>
    /// <remarks>
    /// 问题：Popup 的窗口尺寸恒等于内容的**布局**尺寸（RenderTransform 不参与布局），
    /// 因此任何大于 1.0 的缩放都会让渲染画面超出窗口矩形，超出部分直接被裁掉。
    ///
    /// 解法：在内容外面包一层带 <see cref="Border.Padding"/> 的容器，
    /// 让窗口比内容大一圈，缩放就有活动空间。
    ///
    /// 但窗口变大后，<c>CustomPopupPlacementCallback</c> 收到的 <c>popupSize</c> 也变大了，
    /// 而宿主里 19 处定位都按这个尺寸计算（居中、贴边、留间距），直接用会把菜单位置整体带偏。
    /// 所以这里给回调套一层补偿：
    /// - 用「扣掉留白后的内容尺寸」调用原回调，拿到内容本该所处的位置；
    /// - 再把返回的点整体减去左上留白，抵消内容在容器内的内缩。
    /// 两者合起来，菜单的**视觉位置与未留白时完全一致**。
    ///
    /// 留白量由 <see cref="PopupCurveShape"/> 决定：横向按缩放外扩、纵向还要算上
    /// "收起时整体下滑"那段位移（见 <see cref="ComputePadding"/>）。
    /// </remarks>
    internal static class PopupOverflowRoom
    {
        private sealed class Room
        {
            public Border Host;
            public CustomPopupPlacementCallback Original;
            public Thickness PaddingValue;
        }

        private static readonly ConditionalWeakTable<Popup, Room> Rooms = new ConditionalWeakTable<Popup, Room>();

        /// <summary>容器标记，避免重复包装。</summary>
        private const string HostTag = "PopupOverflowRoomHost";

        /// <summary>单边留白上限（DIP），防止极端尺寸把窗口撑得离谱。</summary>
        private const double MaxPaddingPerSide = 96.0;

        /// <summary>
        /// 预留空间并返回应当施加动画的内容元素，同时给出这条菜单的曲线。
        /// </summary>
        /// <param name="popup">目标菜单。</param>
        /// <param name="shape">这条菜单的曲线（起点缩放由内容宽度导出）。</param>
        /// <returns>需要施加变换与动画的元素（内容本身，不是包装容器）。</returns>
        public static FrameworkElement Ensure(Popup popup, out PopupCurveShape shape)
        {
            // 拿不到内容时也给一条可用的曲线（起点取上限），
            // 免得调用方拿到 default(0) 再去做除法。
            shape = PopupCurveShape.ForStartScale(PopupCurve.MaxStartScale);
            if (popup?.Child is not FrameworkElement content) return null;

            // 已包装过：按当前内容尺寸刷新留白
            if (Rooms.TryGetValue(popup, out var existing) && content == existing.Host)
            {
                var inner = (FrameworkElement)existing.Host.Child;
                shape = PopupCurveShape.ForContentWidth(NaturalWidth(inner));
                UpdatePadding(existing, inner, shape);
                return inner;
            }

            // 先量一次，拿到内容的自然尺寸。
            // 注意：Popup 打开前内容可能尚未完成布局，DesiredSize 会是 0，
            // 此时若按 0 计算留白就等于没留，峰值时四边照样被裁。
            // 因此这里先触发一次布局，仍拿不到就退回到打开后由 UpdatePadding 补算。
            var size = Measure(content);
            shape = PopupCurveShape.ForContentWidth(size.Width);
            var padding = ComputePadding(size, shape);

            var host = new Border
            {
                Tag = HostTag,
                Padding = padding
            };

            var room = new Room
            {
                Original = popup.CustomPopupPlacementCallback,
                PaddingValue = padding
            };

            // 内容当前是 Popup.Child，不先解绑就挂到新容器会抛
            // "元素已有一个逻辑父级"。所以先摘下来，再放进容器，最后交给 Popup。
            // 这三步都在同一次同步调用内完成，中间没有布局，不会出现可见闪烁。
            popup.Child = null;
            host.Child = content;
            popup.Child = host;
            room.Host = host;

            // 给定位回调套上补偿（原回调为 null 时说明该菜单不用自定义定位，无需补偿）
            if (room.Original != null)
            {
                popup.CustomPopupPlacementCallback = CreateCompensated(room);
            }

            // 已存在旧记录（内容被外部替换过）时先移除，避免 Add 抛重复键
            Rooms.Remove(popup);
            Rooms.Add(popup, room);
            return content;
        }

        /// <summary>
        /// 打开后按真实尺寸刷新留白。
        /// Popup 打开前内容可能尚未布局、DesiredSize 为 0，先算出来的留白会偏小；
        /// 这里在内容拿到真实尺寸后补一次，确保峰值时真的不会被裁。
        /// </summary>
        public static void RefreshPadding(Popup popup, PopupCurveShape shape)
        {
            if (popup == null) return;
            if (!Rooms.TryGetValue(popup, out var room)) return;
            if (room.Host?.Child is not FrameworkElement inner) return;

            UpdatePadding(room, inner, shape);
        }

        private static void UpdatePadding(Room room, FrameworkElement content, PopupCurveShape shape)
        {
            var size = new Size(content.ActualWidth, content.ActualHeight);
            if (size.Width <= 0 || size.Height <= 0) size = Measure(content);
            if (size.Width <= 0 || size.Height <= 0) return;

            var padding = ComputePadding(size, shape);

            if (Math.Abs(padding.Left - room.PaddingValue.Left) < 0.5 &&
                Math.Abs(padding.Top - room.PaddingValue.Top) < 0.5 &&
                Math.Abs(padding.Right - room.PaddingValue.Right) < 0.5 &&
                Math.Abs(padding.Bottom - room.PaddingValue.Bottom) < 0.5)
            {
                return;
            }

            room.PaddingValue = padding;
            room.Host.Padding = padding;
        }

        private static Size Measure(FrameworkElement content)
        {
            content.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var size = content.DesiredSize;
            if (size.Width <= 0 || size.Height <= 0)
            {
                size = new Size(content.ActualWidth, content.ActualHeight);
            }

            // 仍然无效：给一个保守的通用留白，至少让常见菜单不被裁
            if (size.Width <= 0 || size.Height <= 0)
            {
                size = new Size(320, 200);
            }

            return size;
        }

        /// <summary>内容自然宽度：优先取布局结果，尚未布局时才去测。</summary>
        private static double NaturalWidth(FrameworkElement content)
        {
            if (content == null) return 0.0;
            if (content.ActualWidth > 0.0) return content.ActualWidth;
            return Measure(content).Width;
        }

        /// <summary>
        /// 留白：横向按缩放外扩算，纵向还要算上收起时的下滑位移。
        /// </summary>
        /// <remarks>
        /// 早期版本按"原点在正中心、四边各溢出半份"给，结果底部工具栏的菜单
        /// （原点实测 (0.496, 1.0)，即底边中点）纵向溢出全堆在顶边，峰值时顶部被裁。
        /// 后来的版本干脆四边都给满一份，安全但窗口白白胖了一圈。
        ///
        /// 现在按**每条边的最坏情况**分别给：
        ///
        ///   横向：原点若贴着左/右边，另一侧就要承受整份宽度外扩 → 每边 W·(peak-1)；
        ///   顶边：原点贴着底边（就是底部工具栏的实际情形），顶部上探 H·(peak-1)，
        ///         再加上峰值处那段额外上移（<see cref="PopupCurveShape.PeakRise"/>）；
        ///   底边：原点贴着顶边时下探 H·(peak-1)；而收起态整体下滑 SlideDistance，
        ///         两者取大——起点越小滑动越远，这一段才是底边真正需要的量。
        ///
        /// 不去精确跟踪当前原点，是因为 Padding 会影响布局尺寸、再反过来改窗口矩形，
        /// 形成反馈环；而按边界情形取大值只让窗口大几像素，且窗口全透明、没有视觉成本。
        /// </remarks>
        private static Thickness ComputePadding(Size contentSize, PopupCurveShape shape)
        {
            var peak = shape.PeakScale;
            if (double.IsNaN(peak) || double.IsInfinity(peak) || peak < 1.0)
            {
                peak = 1.04;
            }

            // 防御：曲线未初始化（StartScale 为 0）时按"无滑动"处理，只留缩放外扩。
            var slide = double.IsNaN(shape.SlideDistance) ? 0.0 : shape.SlideDistance;
            var rise = double.IsNaN(shape.PeakRise) ? 0.0 : shape.PeakRise;

            double overflowX = contentSize.Width * (peak - 1.0);
            double overflowY = contentSize.Height * (peak - 1.0);

            double padX = Cap(Math.Ceiling(overflowX) + 1.0);
            double padTop = Cap(Math.Ceiling(overflowY + rise) + 1.0);
            double padBottom = Cap(Math.Ceiling(Math.Max(overflowY, slide)) + 1.0);

            return new Thickness(padX, padTop, padX, padBottom);
        }

        private static double Cap(double value)
        {
            return value < 0.0 ? 0.0 : value > MaxPaddingPerSide ? MaxPaddingPerSide : value;
        }

        /// <summary>
        /// 生成带留白补偿的定位回调。
        /// </summary>
        private static CustomPopupPlacementCallback CreateCompensated(Room room)
        {
            return (popupSize, targetSize, offset) =>
            {
                var pad = room.PaddingValue;

                // popupSize 是**容器**尺寸，已经包含两侧留白。
                // 原回调的语义基于内容尺寸，所以这里换算成内容尺寸再交给它。
                //
                // 注意不要再额外扣减第二次：早先实测（padding=6 时 popupSize=252，
                // 内容 240）已确认 popupSize 含留白。之前的实现减了两次，
                // 内容高度被算小，垂直方向误差最大 —— 表现就是"菜单偏下"。
                var contentSize = new Size(
                    Math.Max(0, popupSize.Width - (pad.Left + pad.Right)),
                    Math.Max(0, popupSize.Height - (pad.Top + pad.Bottom)));

                var placements = room.Original(contentSize, targetSize, offset);
                if (placements == null) return null;

                // 原回调返回的是"内容左上角应所在的位置"。
                // 内容在容器内向右下各内缩了一个留白，要让内容落在原回调算出的位置，
                // 容器需要再向左上移同样的量。
                return placements
                    .Select(p => new CustomPopupPlacement(
                        new Point(p.Point.X - pad.Left, p.Point.Y - pad.Top),
                        p.PrimaryAxis))
                    .ToArray();
            };
        }
    }
}
