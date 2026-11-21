using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

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
    /// - 再把返回的点整体减去留白量，抵消内容在容器内的内缩。
    /// 两者合起来，菜单的**视觉位置与未留白时完全一致**。
    ///
    /// 留白量按内容尺寸与过冲峰值动态计算（大菜单需要更多空间），
    /// 不能写死一个固定像素——否则宽菜单仍会被裁。
    /// </remarks>
    internal static class PopupOverflowRoom
    {
        private sealed class Room
        {
            public Border Host;
            public CustomPopupPlacementCallback Original;
            public Size PaddingValue;
        }

        private static readonly ConditionalWeakTable<Popup, Room> Rooms = new ConditionalWeakTable<Popup, Room>();

        /// <summary>容器标记，避免重复包装。</summary>
        private const string HostTag = "PopupOverflowRoomHost";

        /// <summary>
        /// 预留空间并返回应当施加动画的内容元素。
        /// </summary>
        /// <param name="popup">目标菜单。</param>
        /// <param name="peakScale">动画峰值相对内容尺寸的倍数（≥1，例如 1.0367）。</param>
        /// <returns>需要施加变换与动画的元素（内容本身，不是包装容器）。</returns>
        public static FrameworkElement Ensure(Popup popup, double peakScale)
        {
            if (popup?.Child is not FrameworkElement content) return null;

            // 已包装过：按当前内容尺寸刷新留白
            if (Rooms.TryGetValue(popup, out var existing) && content == existing.Host)
            {
                var inner = (FrameworkElement)existing.Host.Child;
                UpdatePadding(existing, inner, peakScale);
                return inner;
            }

            // 先量一次，拿到内容的自然尺寸。
            // 注意：Popup 打开前内容可能尚未完成布局，DesiredSize 会是 0，
            // 此时若按 0 计算留白就等于没留，峰值时四边照样被裁。
            // 因此这里先触发一次布局，仍拿不到就退回到打开后由 UpdatePadding 补算。
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

            var room = new Room
            {
                Original = popup.CustomPopupPlacementCallback,
                PaddingValue = ComputePadding(size, peakScale)
            };

            var host = new Border
            {
                Tag = HostTag,
                Padding = new Thickness(room.PaddingValue.Width, room.PaddingValue.Height,
                    room.PaddingValue.Width, room.PaddingValue.Height)
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
        /// 生成带留白补偿的定位回调。
        /// </summary>
        private static CustomPopupPlacementCallback CreateCompensated(Room room)
        {
            return (popupSize, targetSize, offset) =>
            {
                var pad = room.PaddingValue;
                double padSumX = pad.Width * 2;
                double padSumY = pad.Height * 2;

                // 补偿恒定成立，不需要"判断 popupSize 是否含留白"。
                //
                // 原因很直接：内容在容器里，四周永远内缩了一个 padding
                // （内容左上角在容器坐标系中位于 (padX, padY)），这一点与
                // popupSize 报的是内容尺寸还是容器尺寸无关。
                //
                // 所以只要按"内容尺寸"去问原回调，再把结果整体减去 padding，
                // 内容就必然落在未包装时的位置：
                //   容器位置 P + 内容内缩 pad = 内容视觉位置 = P - pad + pad = P。
                //
                // 之前按 popupSize 是否含留白分两支，走"不补偿"那支时会把
                // 这个内缩直接暴露出来，表现为菜单偏右偏下（实测正好是 (padX, padY)）。
                var contentSize = new Size(
                    Math.Max(0, popupSize.Width - padSumX),
                    Math.Max(0, popupSize.Height - padSumY));

                // popupSize 若其实是内容尺寸，上面这一步会把尺寸算小、居中略有偏差；
                // 用容器实测尺寸对齐一次，确保喂给原回调的始终是真实内容尺寸。
                if (room.Host != null)
                {
                    double hostW = room.Host.ActualWidth > 0 ? room.Host.ActualWidth : room.Host.DesiredSize.Width;
                    double hostH = room.Host.ActualHeight > 0 ? room.Host.ActualHeight : room.Host.DesiredSize.Height;

                    if (hostW > padSumX && hostH > padSumY)
                    {
                        var fromHost = new Size(hostW - padSumX, hostH - padSumY);
                        // 与容器推算出的内容尺寸更可信
                        contentSize = fromHost;
                    }
                }

                var placements = room.Original(contentSize, targetSize, offset);
                if (placements == null) return null;

                return placements
                    .Select(p => new CustomPopupPlacement(
                        new Point(p.Point.X - pad.Width, p.Point.Y - pad.Height),
                        p.PrimaryAxis))
                    .ToArray();
            };
        }

        /// <summary>
        /// 打开后按真实尺寸刷新留白。
        /// Popup 打开前内容可能尚未布局、DesiredSize 为 0，先算出来的留白会偏小；
        /// 这里在内容拿到真实尺寸后补一次，确保峰值时真的不会被裁。
        /// </summary>
        public static void RefreshPadding(Popup popup, double peakScale)
        {
            if (popup == null) return;
            if (!Rooms.TryGetValue(popup, out var room)) return;
            if (room.Host?.Child is not FrameworkElement inner) return;

            UpdatePadding(room, inner, peakScale);
        }

        private static void UpdatePadding(Room room, FrameworkElement content, double peakScale)
        {
            // 优先用布局完成后的真实尺寸，其次才 measure
            var size = new Size(content.ActualWidth, content.ActualHeight);
            if (size.Width <= 0 || size.Height <= 0)
            {
                content.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                size = content.DesiredSize;
            }
            if (size.Width <= 0 || size.Height <= 0) return;

            var padding = ComputePadding(size, peakScale);

            if (Math.Abs(padding.Width - room.PaddingValue.Width) < 0.5 &&
                Math.Abs(padding.Height - room.PaddingValue.Height) < 0.5)
            {
                return;
            }

            room.PaddingValue = padding;
            room.Host.Padding = new Thickness(padding.Width, padding.Height, padding.Width, padding.Height);
        }

        /// <summary>
        /// 留白 = 内容尺寸 × 单边溢出比例，向上取整再留 1px 余量。
        /// 峰值 → 仅一半溢出在单侧（原点在中心，四周对称出界）。
        /// </summary>
        private static Size ComputePadding(Size contentSize, double peakScale)
        {
            // 防御：峰值非法时按常见的 4% 估算，保证仍能显示完整
            if (double.IsNaN(peakScale) || double.IsInfinity(peakScale) || peakScale < 1.0)
            {
                peakScale = 1.04;
            }

            double perSide = (peakScale - 1.0) / 2.0;
            double padX = Math.Ceiling(contentSize.Width * perSide) + 1;
            double padY = Math.Ceiling(contentSize.Height * perSide) + 1;

            // 上限保护：极端尺寸下不要把窗口撑得离谱
            padX = Math.Min(padX, 48);
            padY = Math.Min(padY, 48);

            return new Size(padX, padY);
        }
    }
}
