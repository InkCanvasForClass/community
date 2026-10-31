using iNKORE.UI.WPF.Modern.Common.IconKeys;
using System;
using System.Windows;

namespace Ink_Canvas.Controls.Toolbar.BoardToolbar
{
    public interface IBoardToolbarItem
    {
        string Id { get; }

        string DisplayName { get; }

        string Description { get; }

        string IconGeometry { get; }

        FontIconData? IconKey { get; }

        ButtonPosition DefaultPosition { get; }

        /// <summary>
        /// 自定义设置面板工厂。若提供此属性，设置页面将在"组件设置"中附加此工厂返回的 UI。
        /// 适用于需要完全自定义 UI 或读写全局设置（非 per-component 设置）的组件。
        /// </summary>
        Func<FrameworkElement> CustomSettingsPanelFactory => null;

        FrameworkElement BuildView(IBoardToolbarHost host);

        void ApplyPosition(FrameworkElement view, ButtonPosition position);
    }
}
