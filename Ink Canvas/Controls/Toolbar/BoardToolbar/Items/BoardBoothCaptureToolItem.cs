using Ink_Canvas.Properties;
using iNKORE.UI.WPF.Modern.Common.IconKeys;
using System.Windows.Input;
using FluentSystemIcons = iNKORE.UI.WPF.Modern.Common.IconKeys.FluentSystemIcons;

namespace Ink_Canvas.Controls.Toolbar.BoardToolbar.Items
{
    /// <summary>
    /// 视频展台：拍照。照片预览页下变为「返回拍照」，点击回到直播画面。
    /// </summary>
    internal sealed class BoardBoothCaptureToolItem : BoardBoothFontIconItemBase
    {
        public override string Id => BoardToolbarRegistry.BoothCaptureItemId;
        public override string LocalizationKey => "Booth_Capture";
        public override string Description => "视频展台拍照";
        public override ButtonPosition DefaultPosition => ButtonPosition.Single;

        protected override FontIconData IconData => FluentSystemIcons.Camera_24_Regular;

        protected override void OnClick(IBoardToolbarHost host, object sender, MouseButtonEventArgs e)
        {
            if (host.Window.IsVideoBoothPhotoPreviewActive)
                host.Window.VideoBoothSwitchToLiveView();
            else
                host.Window.VideoBoothCapturePhoto();

            UpdateLabel(sender as BoardToolbarButton, host.Window);
        }

        protected override void AfterBuild(IBoardToolbarHost host, BoardToolbarButton view)
        {
            base.AfterBuild(host, view);
            UpdateLabel(view, host.Window);
            view.Loaded += (s, e) => UpdateLabel(view, host.Window);
        }

        /// <summary>照片预览页显示「返回拍照」，直播页显示「拍照」。</summary>
        internal static void UpdateLabel(BoardToolbarButton view, MainWindow window)
        {
            if (view == null || window == null) return;
            view.Label = window.IsVideoBoothPhotoPreviewActive
                ? Strings.GetString("Booth_BackToCapture") ?? "返回拍照"
                : Strings.GetString("Booth_Capture") ?? "拍照";
        }
    }
}
