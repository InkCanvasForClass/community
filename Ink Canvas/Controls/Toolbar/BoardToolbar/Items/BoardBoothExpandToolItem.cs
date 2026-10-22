using iNKORE.UI.WPF.Modern.Common.IconKeys;
using System.Windows.Input;
using FluentSystemIcons = iNKORE.UI.WPF.Modern.Common.IconKeys.FluentSystemIcons;

namespace Ink_Canvas.Controls.Toolbar.BoardToolbar.Items
{
    /// <summary>
    /// 视频展台：展开/收起右侧页码预览面板（等同点击右侧页码按钮）。
    /// 只在展台模式显示，默认播种在右侧区域"新页面"按钮左侧（展台模式下"新页面"被隐藏，正好由它占位）。
    /// </summary>
    internal sealed class BoardBoothExpandToolItem : BoardBoothFontIconItemBase
    {
        public override string Id => BoardToolbarRegistry.BoothExpandItemId;
        public override string LocalizationKey => "Booth_ExpandAlbum";
        public override string Description => "展开页码预览";
        public override ButtonPosition DefaultPosition => ButtonPosition.Single;

        protected override FontIconData IconData => FluentSystemIcons.ChevronUp_24_Regular;

        protected override void OnClick(IBoardToolbarHost host, object sender, MouseButtonEventArgs e)
        {
            host.Window.ToggleBoothPageListPanel();
        }
    }
}
