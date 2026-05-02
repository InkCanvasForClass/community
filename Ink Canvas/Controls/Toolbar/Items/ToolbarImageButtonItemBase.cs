using Ink_Canvas.Properties;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.Controls.Toolbar.Items
{
    internal abstract class ToolbarImageButtonItemBase : IToolbarItem
    {
        public abstract string Id { get; }
        public abstract string LocalizationKey { get; }
        public abstract ToolbarSlot DefaultSlot { get; }
        public abstract int DefaultOrder { get; }
        public virtual bool DefaultVisible => true;
        public virtual ToolbarInsertPosition DefaultPosition => ToolbarInsertPosition.Prepend;
        public virtual string DefaultAnchorName => null;

        public string DisplayName => Strings.GetString(LocalizationKey) ?? LocalizationKey;
        public virtual string MenuPanelName => null;

        protected virtual string IconBrushResourceKey => null;
        protected virtual string LabelBrushResourceKey => null;

        protected abstract void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e);

        protected virtual void AfterBuild(IToolbarHost host, ToolbarImageButton view) { }

        public FrameworkElement BuildView(IToolbarHost host)
        {
            var btn = new ToolbarImageButton
            {
                Label = Strings.GetString(LocalizationKey) ?? LocalizationKey,
                Tag = "ToolbarRegistryInjected"
            };
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
    }
}