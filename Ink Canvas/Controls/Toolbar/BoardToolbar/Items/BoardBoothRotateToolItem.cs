using iNKORE.UI.WPF.Modern.Common.IconKeys;
using System.Windows.Input;
using FluentSystemIcons = iNKORE.UI.WPF.Modern.Common.IconKeys.FluentSystemIcons;

namespace Ink_Canvas.Controls.Toolbar.BoardToolbar.Items
{
    /// <summary>视频展台：画面旋转 90°（直播页/照片页自适应）。</summary>
    internal sealed class BoardBoothRotateToolItem : BoardBoothFontIconItemBase
    {
        public override string Id => BoardToolbarRegistry.BoothRotateItemId;
        public override string LocalizationKey => "Booth_Rotate";
        public override string Description => "视频展台旋转 90°";
        public override ButtonPosition DefaultPosition => ButtonPosition.Single;

        protected override FontIconData IconData => FluentSystemIcons.ArrowClockwise_24_Regular;

        protected override void OnClick(IBoardToolbarHost host, object sender, MouseButtonEventArgs e)
        {
            host.Window.VideoBoothRotate90();
        }
    }
}
