using iNKORE.UI.WPF.Modern.Common.IconKeys;
using Ink_Canvas.Helpers;
using Ink_Canvas.Properties;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FluentSystemIcons = iNKORE.UI.WPF.Modern.Common.IconKeys.FluentSystemIcons;
using FontIcon = iNKORE.UI.WPF.Modern.Controls.FontIcon;

namespace Ink_Canvas.Controls.Toolbar.FloatingToolbar.Items
{
    internal sealed class VideoBoothToolItem : ToolbarImageButtonItemBase
    {
        public override string Id => "builtin.videoBooth";
        public override string LocalizationKey => "Board_VideoBooth";
        public override ToolbarRuleset DefaultHidingRuleset => ToolbarRuleset.AlwaysShow().WithHideOnCollapsed();
        public override string Description => Strings.GetString("Board_VideoBooth") ?? "视频展台";
        public override string IconGeometry => null;

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
        {
            host.Window.Dispatcher.Invoke(() =>
            {
                var mw = host.Window as MainWindow;
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
            view.Loaded += (s, e) =>
            {
                // 在 ToolbarImageButton 的可视化树中找到 ButtonContent Grid，
                // 然后将第一个子元素（Image）替换为 FontIcon
                var buttonContent = FindChildByName<Grid>(view, "ButtonContent");
                if (buttonContent == null || buttonContent.Children.Count == 0)
                    return;

                var oldIcon = buttonContent.Children[0] as Image;
                if (oldIcon == null)
                    return;

                int index = 0;
                buttonContent.Children.RemoveAt(index);
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
                buttonContent.Children.Insert(index, fontIcon);
            };
        }

        private static T FindChildByName<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            if (parent == null) return null;
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T element && element.Name == name)
                    return element;
                var result = FindChildByName<T>(child, name);
                if (result != null) return result;
            }
            return null;
        }
    }
}
