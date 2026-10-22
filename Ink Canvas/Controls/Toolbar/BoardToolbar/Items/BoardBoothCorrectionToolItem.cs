using iNKORE.UI.WPF.Modern.Common.IconKeys;
using System.Windows.Input;
using FluentSystemIcons = iNKORE.UI.WPF.Modern.Common.IconKeys.FluentSystemIcons;

namespace Ink_Canvas.Controls.Toolbar.BoardToolbar.Items
{
    /// <summary>
    /// 视频展台：照片矫正开关。开启时按钮背景显示为强调色（蓝色）。
    /// </summary>
    internal sealed class BoardBoothCorrectionToolItem : BoardBoothFontIconItemBase
    {
        public override string Id => BoardToolbarRegistry.BoothCorrectionItemId;
        public override string LocalizationKey => "Booth_Correction";
        public override string Description => "照片矫正开关";
        public override ButtonPosition DefaultPosition => ButtonPosition.Single;

        protected override FontIconData IconData => FluentSystemIcons.Crop_24_Regular;

        protected override void OnClick(IBoardToolbarHost host, object sender, MouseButtonEventArgs e)
        {
            host.Window.VideoBoothTogglePhotoCorrection();
            UpdateState(sender as BoardToolbarButton, host.Window);
        }

        protected override void AfterBuild(IBoardToolbarHost host, BoardToolbarButton view)
        {
            base.AfterBuild(host, view);
            UpdateState(view, host.Window);
            view.Loaded += (s, e) => UpdateState(view, host.Window);
        }

        /// <summary>开启时背景为强调蓝（与选中工具一致），关闭时恢复透明。</summary>
        internal static void UpdateState(BoardToolbarButton view, MainWindow window)
        {
            if (view == null || window == null) return;
            SetActiveAppearance(view, window.IsBoothPhotoCorrectionEnabled);
        }
    }
}
