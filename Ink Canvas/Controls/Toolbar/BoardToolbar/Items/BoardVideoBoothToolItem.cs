using iNKORE.UI.WPF.Modern.Common.IconKeys;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FluentSystemIcons = iNKORE.UI.WPF.Modern.Common.IconKeys.FluentSystemIcons;
using FontIcon = iNKORE.UI.WPF.Modern.Controls.FontIcon;

namespace Ink_Canvas.Controls.Toolbar.BoardToolbar.Items
{
    internal sealed class BoardVideoBoothToolItem : BoardToolbarImageButtonItemBase
    {
        public override string Id => "board.videoBooth";
        public override string LocalizationKey => "Board_VideoBooth";
        public override string Description => "视频展台";
        public override string IconGeometry => null;
        public override FontIconData? IconKey => FluentSystemIcons.Video_24_Regular;
        public override ButtonPosition DefaultPosition => ButtonPosition.Single;

        protected override void OnClick(IBoardToolbarHost host, object sender, MouseButtonEventArgs e)
        {
            host.Window.Dispatcher.Invoke(() =>
            {
                var mw = host.Window;
                if (mw == null) return;

                // 把按钮自身作为 BoothPopup 的 PlacementTarget，
                // 让 CustomPopupPlacementCallback 中的 targetSize 取按钮尺寸，
                // 菜单才能定位到按钮上方（否则会退化为父级 Grid 屏幕尺寸，菜单跑到屏幕顶部中心上方）
                if (sender is System.Windows.FrameworkElement fe)
                {
                    mw.SetBoothPopupPlacementTarget(fe);
                }

                mw.ToggleVideoPresenterSidebarPublic();
            });
        }

        protected override void AfterBuild(IBoardToolbarHost host, BoardToolbarButton view)
        {
            host.RegisterView(Id, view);
            view.Loaded += (s, e) =>
            {
                var grid = view.ButtonBorderControl.Child as Grid;
                if (grid == null || grid.Children.Count == 0)
                    return;

                var oldIcon = grid.Children[0] as Image;
                if (oldIcon == null)
                    return;

                grid.Children.RemoveAt(0);
                var fontIcon = new FontIcon
                {
                    Icon = FluentSystemIcons.Video_24_Regular,
                    Width = 24,
                    Height = 24,
                    VerticalAlignment = VerticalAlignment.Top,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FontSize = 24,
                    Margin = new Thickness(0, -1, 0, 0)
                };
                grid.Children.Insert(0, fontIcon);
            };
        }
    }
}
