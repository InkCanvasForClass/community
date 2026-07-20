using Ink_Canvas.Helpers;
using Ink_Canvas.Properties;
using iNKORE.UI.WPF.Modern.Common.IconKeys;
using System.Windows.Input;
using FluentSystemIcons = iNKORE.UI.WPF.Modern.Common.IconKeys.FluentSystemIcons;

namespace Ink_Canvas.Controls.Toolbar.FloatingToolbar.Items
{
    internal sealed class VideoBoothToolItem : ToolbarImageButtonItemBase
    {
        public override string Id => "builtin.videoBooth";
        public override string LocalizationKey => "Board_VideoBooth";
        public override ToolbarRuleset DefaultHidingRuleset => ToolbarRuleset.AlwaysShow().WithHideOnCollapsed();
        public override string Description => Strings.GetString("Board_VideoBooth") ?? "视频展台";
        public override string IconGeometry => null;
        public override FontIconData? IconKey => FluentSystemIcons.Video_24_Regular;

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
        {
            host.Window.Dispatcher.Invoke(() =>
            {
                var mw = host.Window;
                if (mw == null) return;

                if (MainWindow.Settings?.Canvas?.LaunchSeewoVideoShowcaseForWhiteboardBooth == true)
                {
                    // 开启希沃视频展台设置时：直接启动希沃视频展台
                    SoftwareLauncher.LaunchEasiCamera("希沃视频展台");
                }
                else
                {
                    // 正常模式：先打开白板，再打开内置视频展台
                    mw.ImageBlackboard_MouseUp(null, null);
                    mw.ToggleVideoPresenterSidebarPublic();
                }
            });
        }

        protected override void AfterBuild(IToolbarHost host, ToolbarImageButton view)
        {
            host.RegisterView(Id, view);
        }
    }
}
