using Ink_Canvas.Properties;
using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.FloatingToolbar.Items
{
    internal sealed class GestureToolItem : ToolbarImageButtonItemBase
    {
        public override string Id => "builtin.gesture";
        public override string LocalizationKey => "FloatingBar_GestureButton";
        public override ToolbarRuleset DefaultHidingRuleset => ToolbarRuleset.AnnotationOnly();
        public override bool DefaultShowSeparateBorder => true;
        public override bool DefaultPreventHideOnDragClick => true;
        public override string Description => FloatingBarStrings.ToolbarItem_Desc_Gesture;
        protected override string IconGeometry => XamlGraphicsIconGeometries.DisabledGestureIcon;

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.TwoFingerGestureBorder_MouseUp(sender, e);

        protected override void AfterBuild(IToolbarHost host, ToolbarImageButton view)
            => host.Window.AttachGestureBtn(view);
    }
}
