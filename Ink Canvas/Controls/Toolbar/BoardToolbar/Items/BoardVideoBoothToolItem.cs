// 只别名导入 WPF-UI 专属控件，避免与 System.Windows.Controls 同名类型冲突。
using SymbolIcon = Wpf.Ui.Controls.SymbolIcon;
using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.BoardToolbar.Items
{
    internal sealed class BoardVideoBoothToolItem : BoardToolbarImageButtonItemBase
    {
        public override string Id => "board.videoBooth";
        public override string LocalizationKey => "Board_VideoBooth";
        public override string Description => "视频展台";
        public override string IconGeometry => null;
        public override SymbolRegular? IconKey => SymbolRegular.Video24;
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
                var fontIcon = new SymbolIcon
                {
                    Symbol = SymbolRegular.Video24,
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
