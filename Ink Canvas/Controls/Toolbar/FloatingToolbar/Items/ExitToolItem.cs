using Ink_Canvas.Properties;
using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.FloatingToolbar.Items
{
    internal sealed class ExitToolItem : ToolbarImageButtonItemBase
    {
        public override string Id => "builtin.exit";
        public override string LocalizationKey => "FloatingBar_ExitButton";
        public override ToolbarRuleset DefaultHidingRuleset => ToolbarRuleset.PPTOnly();
        public override bool DefaultShowSeparateBorder => true;
        public override bool DefaultPreventHideOnDragClick => true;
        public override string Description => FloatingBarStrings.ToolbarItem_Desc_Exit;
        public override string IconGeometry => XamlGraphicsIconGeometries.ExitPresentationIconGeometry;

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.ImagePPTControlEnd_MouseUp(sender, e);

        protected override void AfterBuild(IToolbarHost host, ToolbarImageButton view)
            => host.Window.AttachExitBtn(view);
    }
}
