using iNKORE.UI.WPF.Modern.Common.IconKeys;
using System.Windows.Input;
using FluentSystemIcons = iNKORE.UI.WPF.Modern.Common.IconKeys.FluentSystemIcons;

namespace Ink_Canvas.Controls.Toolbar.BoardToolbar.Items
{
    /// <summary>
    /// 视频展台：导入图片到展台相册。选择图片文件后按拍照照片同权处理
    /// （进入页码预览、可翻页查看、可独立批注、可删除）。
    /// 仅在展台模式显示（showInBoothOnly）。
    /// </summary>
    internal sealed class BoardBoothImportToolItem : BoardBoothFontIconItemBase
    {
        public override string Id => BoardToolbarRegistry.BoothImportItemId;
        public override string LocalizationKey => "Booth_Import";
        public override string Description => "视频展台导入图片";
        public override ButtonPosition DefaultPosition => ButtonPosition.Single;

        protected override FontIconData IconData => FluentSystemIcons.ImageMultiple_24_Regular;

        protected override void OnClick(IBoardToolbarHost host, object sender, MouseButtonEventArgs e)
        {
            host.Window.VideoBoothImportPhotos();
        }
    }
}
