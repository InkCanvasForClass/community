using System;

namespace WpfUiCompat
{
    /// <summary>
    /// 主题资源键（兼容 iNKORE ThemeKeys），映射到 WPF-UI 的资源键名。
    /// </summary>
    public static class ThemeKeys
    {
        private const string Prefix = "";

        public static object ButtonBackgroundKey => "ButtonBackground";
        public static object ButtonForegroundKey => "ButtonForeground";
        public static object ButtonBackgroundPointerOverKey => "ButtonBackgroundPointerOver";
        public static object ButtonBackgroundPressedKey => "ButtonBackgroundPressed";
        public static object ButtonBackgroundDisabledKey => "ButtonBackgroundDisabled";
        public static object AccentTextFillColorPrimaryBrushKey => "AccentTextFillColorPrimaryBrush";
        public static object AccentTextFillColorSecondaryBrushKey => "AccentTextFillColorSecondaryBrush";
        public static object AccentFillColorDefaultBrushKey => "AccentFillColorDefaultBrush";
        public static object AccentFillColorSecondaryBrushKey => "AccentFillColorSecondaryBrush";
        public static object AccentFillColorTertiaryBrushKey => "AccentFillColorTertiaryBrush";
        public static object TextFillColorPrimaryBrushKey => "TextFillColorPrimaryBrush";
        public static object TextFillColorSecondaryBrushKey => "TextFillColorSecondaryBrush";
        public static object TextFillColorTertiaryBrushKey => "TextFillColorTertiaryBrush";
        public static object ApplicationBackgroundBrushKey => "ApplicationBackgroundBrush";
        public static object ApplicationForegroundColorKey => "TextFillColorPrimary";
        public static object CardBackgroundFillColorDefaultBrushKey => "CardBackgroundFillColorDefaultBrush";
        public static object CardBackgroundFillColorSecondaryBrushKey => "CardBackgroundFillColorSecondaryBrush";
        public static object CardStrokeColorDefaultBrushKey => "CardStrokeColorDefaultBrush";
        public static object ControlFillColorDefaultBrushKey => "ControlFillColorDefaultBrush";
        public static object ControlFillColorSecondaryBrushKey => "ControlFillColorSecondaryBrush";
        public static object ControlStrokeColorDefaultBrushKey => "ControlStrokeColorDefaultBrush";
        public static object ControlContentFillColorDefaultBrushKey => "ControlContentFillColorDefaultBrush";

        public static object AccentFillColorDefaultKey => "AccentFillColorDefault";
        public static object AccentFillColorSecondaryKey => "AccentFillColorSecondary";
        public static object SystemAccentColorKey => "SystemAccentColor";
        public static object SystemAccentColorLight1Key => "SystemAccentColorLight1";
        public static object SystemAccentColorLight2Key => "SystemAccentColorLight2";
        public static object SystemAccentColorLight3Key => "SystemAccentColorLight3";
        public static object SystemAccentColorDark1Key => "SystemAccentColorDark1";
        public static object SystemAccentColorDark2Key => "SystemAccentColorDark2";
        public static object SystemAccentColorDark3Key => "SystemAccentColorDark3";

        // 字体键（FontDictionary 兼容）
        public static object AccentButtonStyleKey => "DefaultButtonStyle";
        public static object DefaultButtonStyleKey => "DefaultButtonStyle";
        public static object TabControlPivotStyleKey => "DefaultTabControlStyle";
        public static object TabItemPivotStyleKey => "DefaultTabItemStyle";
        public static object DividerStrokeColorDefaultBrushKey => "DividerStrokeColorDefaultBrush";
        public static object LayerFillColorDefaultBrushKey => "LayerFillColorDefaultBrush";
        public static object SegoeUISymbolKey => "Segoe UI Symbol";
        public static object SegoeMDL2AssetsKey => "Segoe MDL2 Assets";
        public static object SegoeFluentIconsKey => "Segoe Fluent Icons";
        public static object FluentSystemIconsKey => "FluentSystemIcons";
    }
}