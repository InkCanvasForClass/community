using Ink_Canvas.Plugins;
using Ink_Canvas.Properties;
using System.Collections.Generic;
using System.Windows.Input;

namespace Ink_Canvas.Controls.Toolbar.FloatingToolbar.Items
{
    internal sealed class CursorWithDelToolItem : ToolbarImageButtonItemBase
    {
        public override string Id => "builtin.cursorWithDel";
        public override string LocalizationKey => "FloatingBar_ClearAndMouse";
        public override ToolbarRuleset DefaultHidingRuleset => ToolbarRuleset.AnnotationOnly().WithHideOnCollapsed();
        public override string Description => FloatingBarStrings.ToolbarItem_Desc_CursorWithDel;
        public override string IconGeometry => XamlGraphicsIconGeometries.CursorWithDelFloatingBarBtnIcon;

        public override IReadOnlyList<PluginToolbarSettingInfo> CustomSettings { get; } = new List<PluginToolbarSettingInfo>
        {
            new PluginToolbarSettingInfo
            {
                Key = ComponentSettingKeys.Label,
                DisplayName = "按钮名称",
                Description = "选择清空并切换到鼠标模式按钮的显示名称",
                Type = PluginToolbarSettingType.ComboBox,
                Options = new List<string>
                {
                    Strings.GetString("FloatingBar_ClearAndMouse"),
                    Strings.GetString("FloatingBar_ClearAndMouseShort")
                },
                OptionValues = new List<string>
                {
                    "FloatingBar_ClearAndMouse",
                    "FloatingBar_ClearAndMouseShort"
                },
                DefaultValue = "FloatingBar_ClearAndMouse"
            }
        };

        protected override void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
            => host.Window.CursorWithDelIcon_Click(sender, e);

        protected override void AfterBuild(IToolbarHost host, ToolbarImageButton view)
            => host.Window.AttachCursorWithDelBtn(view);
    }
}
