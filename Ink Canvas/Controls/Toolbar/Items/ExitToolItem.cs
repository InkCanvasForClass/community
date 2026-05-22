using System.Windows.Input;
using Ink_Canvas.Properties;

namespace Ink_Canvas.Controls.Toolbar.Items
{
    internal sealed class ExitToolItem : ToolbarImageButtonItemBase
    {
        public override string Id => "builtin.exit";
        public override string LocalizationKey => "FloatingBar_ExitButton";
        public override ToolbarRuleset DefaultHidingRuleset => ToolbarRuleset.PptOnly();
        public override bool DefaultShowSeparateBorder => true;
        public override bool DefaultPreventHideOnDragClick => true;
        public override string Description => FloatingBarStrings.ToolbarItem_Desc_Exit;
        protected override string IconGeometry => XamlGraphicsIconGeometries.ExitPresentationIconGeometry;

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.ImagePPTControlEnd_MouseUp(sender, e);

        protected override void AfterBuild(IToolbarHost host, ToolbarImageButton view)
            => host.Window.AttachExitBtn(view);
    }
}
