using iNKORE.UI.WPF.Modern.Common.IconKeys;
using System.Windows;
using System.Windows.Input;
using FluentSystemIcons = iNKORE.UI.WPF.Modern.Common.IconKeys.FluentSystemIcons;

namespace Ink_Canvas.Controls.Toolbar.BoardToolbar.Items
{
    /// <summary>
    /// 视频展台：打开/收起展台菜单（设备、分辨率、专业模式、关闭等）。
    /// </summary>
    internal sealed class BoardBoothMenuToolItem : BoardBoothFontIconItemBase
    {
        public override string Id => BoardToolbarRegistry.BoothMenuItemId;
        public override string LocalizationKey => "Booth_ToolbarMenu";
        public override string Description => "打开展台菜单";
        public override ButtonPosition DefaultPosition => ButtonPosition.Single;

        protected override FontIconData IconData => FluentSystemIcons.Options_24_Regular;

        protected override void OnClick(IBoardToolbarHost host, object sender, MouseButtonEventArgs e)
        {
            // 以按钮自身作为菜单的定位目标，避免菜单跑到屏幕顶部
            host.Window.VideoBoothShowMenu(sender as FrameworkElement);
        }
    }
}
