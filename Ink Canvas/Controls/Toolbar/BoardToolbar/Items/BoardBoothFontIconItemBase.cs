using iNKORE.UI.WPF.Modern.Common.IconKeys;
using iNKORE.UI.WPF.Modern.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Ink_Canvas.Controls.Toolbar.BoardToolbar.Items
{
    /// <summary>
    /// 视频展台专用工具栏组件的公共基类：把 <see cref="BoardToolbarButton"/> 里默认的
    /// 位图图标替换为字体图标（与 board.videoBooth 的做法一致）。
    /// 这些组件只会在视频展台模式下显示（由组件的 showInBoothOnly 规则控制）。
    /// </summary>
    internal abstract class BoardBoothFontIconItemBase : BoardToolbarImageButtonItemBase
    {
        protected abstract FontIconData IconData { get; }

        /// <summary>供设置页组件库显示字体图标（与工具栏实际使用的图标一致）。</summary>
        public override FontIconData? IconKey => IconData;

        protected override void AfterBuild(IBoardToolbarHost host, BoardToolbarButton view)
        {
            host.RegisterView(Id, view);
            view.Loaded += (s, e) => ApplyFontIcon(view, IconData);
        }

        private static void ApplyFontIcon(BoardToolbarButton view, FontIconData icon)
        {
            if (view?.ButtonBorderControl?.Child is not Grid grid || grid.Children.Count == 0) return;
            // 已替换过就不重复替换（Loaded 可能多次触发）
            if (grid.Children[0] is FontIcon) return;
            if (grid.Children[0] is not Image) return;

            grid.Children.RemoveAt(0);
            grid.Children.Insert(0, new FontIcon
            {
                Icon = icon,
                Width = 24,
                Height = 24,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = 24,
                Margin = new Thickness(0, -1, 0, 0)
            });
        }

        /// <summary>
        /// 开关类组件的选中外观：背景用与「漫游/笔/橡皮」等选中组件相同的
        /// <c>FloatingBarAccentBrush</c>，文字与图标转为白色；未选中时恢复透明与常规前景色。
        /// </summary>
        protected static void SetActiveAppearance(BoardToolbarButton view, bool active)
        {
            if (view == null) return;

            var accent = Application.Current?.TryFindResource("FloatingBarAccentBrush") as Brush
                         ?? new SolidColorBrush(Color.FromRgb(37, 99, 235));
            var normal = Application.Current?.TryFindResource("FloatingBarForegroundBrush") as Brush
                         ?? Brushes.White;

            view.Background = active ? accent : Brushes.Transparent;
            view.Foreground = active ? Brushes.White : normal;

            if (view.ButtonBorderControl?.Child is Grid grid)
            {
                foreach (var child in grid.Children)
                {
                    if (child is FontIcon fontIcon)
                        fontIcon.Foreground = active ? Brushes.White : normal;
                }
            }
        }
    }
}
