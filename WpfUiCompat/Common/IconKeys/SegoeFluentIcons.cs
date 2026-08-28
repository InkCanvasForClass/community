using System.Windows.Media;

namespace WpfUiCompat.Common.IconKeys
{
    /// <summary>
    /// Segoe Fluent Icons 字形键集合。移植自 iNKORE.UI.WPF.Modern（MIT License）。
    /// </summary>
    public static partial class SegoeFluentIcons
    {
        public static FontFamily FontFamily => FontDictionary.SegoeFluentIcons;

        public static FontIconData CreateIcon(string glyph, bool forceFluent = false)
        {
            return new FontIconData(glyph, forceFluent ? FontFamily : new FontFamily(Controls.FontIcon.SegoeIconsFontFamilyName));
        }

        public static FontIconData CreateIcon(int chara, bool forceFluent = false)
        {
            return CreateIcon(FontIconData.ToGlyph(chara), forceFluent);
        }
    }
}