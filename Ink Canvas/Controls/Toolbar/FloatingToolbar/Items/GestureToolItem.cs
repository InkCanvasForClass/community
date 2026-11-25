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
        public override string IconGeometry => XamlGraphicsIconGeometries.DisabledGestureIcon;

        // 点击打开手势面板：等工具栏动画落定再执行，避免挂 Popup 的重活把动画拖住。
        protected override bool OpensUiOnClick => true;

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.TwoFingerGestureBorder_MouseUp(sender, e);

        protected override void AfterBuild(IToolbarHost host, ToolbarImageButton view)
            => host.Window.AttachGestureBtn(view);
    }
}
