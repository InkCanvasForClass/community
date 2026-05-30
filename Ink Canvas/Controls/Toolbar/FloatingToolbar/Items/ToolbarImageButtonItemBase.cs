using Ink_Canvas.Properties;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.Controls.Toolbar.FloatingToolbar.Items
{
    internal abstract class ToolbarImageButtonItemBase : IToolbarItem
    {
        public abstract string Id { get; }
        public abstract string LocalizationKey { get; }
        public virtual ToolbarRuleset DefaultHidingRuleset => ToolbarRuleset.AlwaysShow().WithHideOnCollapsed();
        public virtual bool DefaultShowSeparateBorder => false;
        public virtual bool DefaultPreventHideOnDragClick => false;
        public virtual string Description => "";

        public string DisplayName => Strings.GetString(LocalizationKey) ?? LocalizationKey;

        protected virtual string IconBrushResourceKey => null;
        protected virtual string LabelBrushResourceKey => null;
        protected virtual string IconGeometry => null;

        protected abstract void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e);

        protected virtual void AfterBuild(IToolbarHost host, ToolbarImageButton view) { }

        public FrameworkElement BuildView(IToolbarHost host)
        {
            var btn = new ToolbarImageButton
            {
                Label = Strings.GetString(LocalizationKey) ?? LocalizationKey,
                Tag = "ToolbarRegistryInjected"
            };
            if (!string.IsNullOrEmpty(IconGeometry))
                btn.Icon.Geometry = Geometry.Parse(IconGeometry);
            if (!string.IsNullOrEmpty(IconBrushResourceKey))
            {
                if (btn.TryFindResource(IconBrushResourceKey) is Brush brush) btn.IconBrush = brush;
                else btn.SetResourceReference(ToolbarImageButton.IconBrushProperty, IconBrushResourceKey);
            }
            if (!string.IsNullOrEmpty(LabelBrushResourceKey))
            {
                if (btn.TryFindResource(LabelBrushResourceKey) is Brush brush) btn.LabelBrush = brush;
                else btn.SetResourceReference(ToolbarImageButton.LabelBrushProperty, LabelBrushResourceKey);
            }
            btn.ButtonMouseUp += (s, e) => OnClick(host, s, e);
            AfterBuild(host, btn);
            return btn;
        }

        public void ApplyOrientation(FrameworkElement view, Orientation orientation)
        {
            if (view is ToolbarImageButton btn)
            {
                btn.ApplyOrientation(orientation == Orientation.Vertical);
            }
        }
    }
}
