using Ink_Canvas.Plugins;
using Ink_Canvas.Properties;
using iNKORE.UI.WPF.Modern.Common.IconKeys;
using System;
using System.Collections.Generic;
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
        public virtual bool DefaultRedStyleIcon => false;
        public virtual bool DefaultRedStyleText => false;
        public virtual string Description => "";

        // 显式声明为 virtual，确保派生类的 override 能正确映射到接口成员
        // （接口默认实现 DIM 在间接继承时不会自动映射到派生类的新声明）
        public virtual IReadOnlyList<PluginToolbarSettingInfo> CustomSettings => null;
        public virtual Func<FrameworkElement> CustomSettingsPanelFactory => null;

        public string DisplayName => Strings.GetString(LocalizationKey) ?? LocalizationKey;

        protected virtual string IconBrushResourceKey => null;
        protected virtual string LabelBrushResourceKey => null;
        public virtual string IconGeometry => null;
        public virtual FontIconData? IconKey => null;

        /// <summary>
        /// 点击后是否要打开菜单/窗口（UI 线程上的重活）。
        /// </summary>
        /// <remarks>
        /// 打开窗口要解析 XAML、创建 HWND；打开菜单要重建菜单内容再挂 Popup；
        /// 都是几十到几百毫秒的同步工作。这类动作正好落在工具栏动画中间时，
        /// UI 线程被占住，动画会整段停住或连掉几帧。
        /// 置 true 的组件统一走 <c>MainWindow.RunToolbarUiAction</c>：
        /// 没有动画时立刻执行，有动画时等它落定再执行。
        /// 换笔、撤销这类轻动作不要置 true，否则会平白多出一段等待。
        /// </remarks>
        protected virtual bool OpensUiOnClick => false;

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
                btn.Icon.Geometry = InkResourceCache.GetGeometry(IconGeometry);
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
            btn.ButtonMouseUp += (s, e) => InvokeClick(host, s, e);
            AfterBuild(host, btn);
            return btn;
        }

        /// <summary>
        /// 点击分派：打开菜单/窗口的动作交给宿主的动画让路机制，其余动作原样立刻执行。
        /// </summary>
        private void InvokeClick(IToolbarHost host, object sender, MouseButtonEventArgs e)
        {
            if (!OpensUiOnClick)
            {
                OnClick(host, sender, e);
                return;
            }

            host.Window.RunToolbarUiAction(Id, () => OnClick(host, sender, e));
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
