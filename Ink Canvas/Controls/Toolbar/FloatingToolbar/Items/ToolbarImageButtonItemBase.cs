using Ink_Canvas.Properties;
using iNKORE.UI.WPF.Modern.Common.IconKeys;
using System;
using System.Linq;
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
        public virtual string IconGeometry => null;
        public virtual FontIconData? IconKey => null;

        protected abstract void OnClick(IToolbarHost host, object sender, MouseButtonEventArgs e);

        protected virtual void AfterBuild(IToolbarHost host, ToolbarImageButton view) { }

        public FrameworkElement BuildView(IToolbarHost host)
        {
            var btn = new ToolbarImageButton
            {
                Label = Strings.GetString(LocalizationKey) ?? LocalizationKey,
                Tag = "ToolbarRegistryInjected"
            };

            if (IconKey != null)
            {
                // FontIcon 模式：在 Loaded 后替换 Image 为 FontIcon
                btn.Loaded += (s, e) => btn.Dispatcher.BeginInvoke(new Action(() => ReplaceImageWithFontIcon(btn)));
            }
            else if (!string.IsNullOrEmpty(IconGeometry))
            {
                btn.Icon.Geometry = Geometry.Parse(IconGeometry);
            }

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

        private void ReplaceImageWithFontIcon(ToolbarImageButton btn)
        {
            var buttonContent = FindChildByName<Grid>(btn, "ButtonContent");
            if (buttonContent == null || buttonContent.Children.Count == 0) return;

            var oldIcon = buttonContent.Children.OfType<Image>().FirstOrDefault();
            if (oldIcon == null) return;

            var fontIcon = new iNKORE.UI.WPF.Modern.Controls.FontIcon
            {
                Icon = IconKey.Value,
                Width = 24,
                Height = 24,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = 24,
                Margin = new Thickness(0, -1, 0, 0)
            };

            int index = buttonContent.Children.IndexOf(oldIcon);
            if (index < 0) return;
            buttonContent.Children.RemoveAt(index);
            buttonContent.Children.Insert(index, fontIcon);
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

        public void ApplyOrientation(FrameworkElement view, Orientation orientation)
        {
            if (view is ToolbarImageButton btn)
            {
                btn.ApplyOrientation(orientation == Orientation.Vertical);
            }
        }
    }
}
