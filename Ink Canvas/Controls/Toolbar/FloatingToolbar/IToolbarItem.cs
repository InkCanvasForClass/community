using Ink_Canvas.Plugins;
using iNKORE.UI.WPF.Modern.Common.IconKeys;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Controls.Toolbar.FloatingToolbar
{
    public interface IToolbarItem
    {
        string Id { get; }

        string DisplayName { get; }

        string Description { get; }

        string IconGeometry { get; }

        FontIconData? IconKey { get; }

        ToolbarRuleset DefaultHidingRuleset { get; }

        bool DefaultShowSeparateBorder { get; }

        bool DefaultPreventHideOnDragClick { get; }

        /// <summary>
        /// 组件自定义设置声明。内置组件和插件组件均可通过此属性声明需要在设置页面动态生成的配置项。
        /// 默认为空列表，表示无自定义设置。
        /// </summary>
        IReadOnlyList<PluginToolbarSettingInfo> CustomSettings => System.Array.Empty<PluginToolbarSettingInfo>();

        /// <summary>
        /// 自定义设置面板工厂。若提供此属性，设置页面将使用此工厂返回的 UI 而非通过 CustomSettings 声明式生成。
        /// 适用于需要完全自定义 UI 或读写全局设置（非 per-component 设置）的组件。
        /// </summary>
        Func<FrameworkElement> CustomSettingsPanelFactory => null;

        FrameworkElement BuildView(IToolbarHost host);

        void ApplyOrientation(FrameworkElement view, Orientation orientation) { }
    }
}
