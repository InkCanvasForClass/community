using System.Windows.Media;
using Wpf.Ui.Controls;

namespace WpfUiCompat.Common.IconKeys
{
    /// <summary>
    /// Fluent System Icons 字形键集合（兼容 iNKORE 命名：Name_Size_Variant）。
    /// 底层使用 WPF-UI 内嵌的 Fluent System Icons 字体与 SymbolRegular/SymbolFilled 枚举。
    /// 仅包含本项目实际使用的成员。
    /// </summary>
    public static class FluentSystemIcons
    {
        private const string RegularFontSource = "pack://application:,,,/Wpf.Ui;component/Resources/Fonts/#FluentSystemIcons-Regular";
        private const string FilledFontSource = "pack://application:,,,/Wpf.Ui;component/Resources/Fonts/#FluentSystemIcons-Filled";

        public static FontIconData Add_16_Regular => new FontIconData(ToGlyphString(SymbolRegular.Add16), RegularFont);
        public static FontIconData ArrowSync_16_Regular => new FontIconData(ToGlyphString(SymbolRegular.ArrowSync16), RegularFont);
        public static FontIconData Copy_16_Regular => new FontIconData(ToGlyphString(SymbolRegular.Copy16), RegularFont);
        public static FontIconData Delete_16_Regular => new FontIconData(ToGlyphString(SymbolRegular.Delete16), RegularFont);
        public static FontIconData Desktop_24_Regular => new FontIconData(ToGlyphString(SymbolRegular.Desktop24), RegularFont);
        public static FontIconData Dismiss_16_Regular => new FontIconData(ToGlyphString(SymbolRegular.Dismiss16), RegularFont);
        public static FontIconData FolderOpen_16_Regular => new FontIconData(ToGlyphString(SymbolRegular.FolderOpen16), RegularFont);
        public static FontIconData ReOrder_16_Regular => new FontIconData(ToGlyphString(SymbolRegular.ReOrder16), RegularFont);
        public static FontIconData ReOrder_20_Regular => new FontIconData(ToGlyphString(SymbolRegular.ReOrder20), RegularFont);
        public static FontIconData ReOrderDotsVertical_20_Filled => new FontIconData(ToGlyphString(SymbolFilled.ReOrderDotsVertical20), FilledFont);
        public static FontIconData ReOrderDotsVertical_24_Filled => new FontIconData(ToGlyphString(SymbolFilled.ReOrderDotsVertical24), FilledFont);
        public static FontIconData Video_24_Regular => new FontIconData(ToGlyphString(SymbolRegular.Video24), RegularFont);

        private static FontFamily RegularFont => new FontFamily(RegularFontSource);
        private static FontFamily FilledFont => new FontFamily(FilledFontSource);

        private static string ToGlyphString(Wpf.Ui.Controls.SymbolRegular icon)
        {
            return System.Text.Encoding.Unicode.GetString(System.BitConverter.GetBytes((int)icon)).TrimEnd('\0');
        }

        private static string ToGlyphString(Wpf.Ui.Controls.SymbolFilled icon)
        {
            return System.Text.Encoding.Unicode.GetString(System.BitConverter.GetBytes((int)icon)).TrimEnd('\0');
        }    }
}