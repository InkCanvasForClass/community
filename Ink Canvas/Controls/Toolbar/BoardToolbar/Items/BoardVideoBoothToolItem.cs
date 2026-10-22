using iNKORE.UI.WPF.Modern.Common.IconKeys;
using System.Windows.Input;
using FluentSystemIcons = iNKORE.UI.WPF.Modern.Common.IconKeys.FluentSystemIcons;

namespace Ink_Canvas.Controls.Toolbar.BoardToolbar.Items
{
    /// <summary>
    /// 视频展台开关：点击进入/退出视频展台；处于展台模式时按钮背景高亮（与选中工具同一套配色）。
    /// </summary>
    internal sealed class BoardVideoBoothToolItem : BoardBoothFontIconItemBase
    {
        public override string Id => "board.videoBooth";
        public override string LocalizationKey => "Board_VideoBooth";
        public override string Description => "视频展台";
        public override ButtonPosition DefaultPosition => ButtonPosition.Single;

        protected override FontIconData IconData => FluentSystemIcons.Video_24_Regular;

        protected override void OnClick(IBoardToolbarHost host, object sender, MouseButtonEventArgs e)
        {
            // 开关语义：未开启时进入展台，已开启时退出展台
            host.Window.ToggleVideoBooth();
            UpdateState(sender as BoardToolbarButton, host.Window);
        }

        protected override void AfterBuild(IBoardToolbarHost host, BoardToolbarButton view)
        {
            base.AfterBuild(host, view);
            UpdateState(view, host.Window);
            view.Loaded += (s, e) => UpdateState(view, host.Window);
        }

        /// <summary>展台开启时按钮高亮，关闭时恢复透明。</summary>
        internal static void UpdateState(BoardToolbarButton view, MainWindow window)
        {
            if (view == null || window == null) return;
            SetActiveAppearance(view, window.IsVideoBoothActive);
        }
    }
}
