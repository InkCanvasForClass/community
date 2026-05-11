using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.Controls.Toolbar.Items
{
    internal sealed class DragHandleToolItem : IToolbarItem
    {
        public string Id => "builtin.dragHandle";
        public string DisplayName => "拖动";
        public string Description => "拖动浮动工具栏";
        public ToolbarRuleset DefaultHidingRuleset => ToolbarRuleset.AlwaysShow();
        public bool DefaultShowSeparateBorder => true;

        public FrameworkElement BuildView(IToolbarHost host)
        {
            var image = new Image
            {
                Margin = new Thickness(0.5),
                SnapsToDevicePixels = true,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = ToolbarRegistry.InjectedTag
            };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            panel.Children.Add(image);

            panel.MouseDown += (s, e) => host.Window.DragHandleMouseDown(s, e);
            panel.MouseUp += (s, e) => host.Window.SymbolIconEmoji_MouseUp(s, e);

            host.Window.AttachDragHandleView(image);

            return panel;
        }
    }
}
