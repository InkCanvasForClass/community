using System;
using System.Windows;
using System.Windows.Media;

namespace WpfUiCompat.Common.IconKeys
{
    /// <summary>
    /// 图标字体字典。兼容层不内嵌字体文件，直接引用系统图标字体。
    /// </summary>
    public static class FontDictionary
    {
        public const string SegoeFluentIconsName = "Segoe Fluent Icons";
        public const string SegoeMDL2AssetsName = "Segoe MDL2 Assets";
        public const string SegoeUISymbolName = "Segoe UI Symbol";

        public static FontFamily SegoeUISymbol => new FontFamily(SegoeUISymbolName);
        public static FontFamily SegoeMDL2Assets => new FontFamily(SegoeMDL2AssetsName);
        public static FontFamily SegoeFluentIcons => new FontFamily(SegoeFluentIconsName);
    }

    /// <summary>
    /// 图标数据：字形 + 字体。移植自 iNKORE.UI.WPF.Modern（MIT License）。
    /// </summary>
    public struct FontIconData
    {
        private FontFamily _fontFamily;
        public FontFamily FontFamily => _fontFamily;

        private string _glyph;
        public string Glyph => _glyph;

        public FontIconData(string glyph, FontFamily family = null)
        {
            _glyph = glyph;
            _fontFamily = family;
        }

        public static string ToGlyph(int chara)
        {
            return char.ConvertFromUtf32(chara);
        }

        public static int ToUtf32(string glyph)
        {
            if (string.IsNullOrEmpty(glyph))
                throw new ArgumentException("Input glyph cannot be null or empty.");

            if (glyph.Length == 1)
            {
                return char.ConvertToUtf32(glyph, 0);
            }
            else if (glyph.Length == 2 && char.IsSurrogatePair(glyph[0], glyph[1]))
            {
                return char.ConvertToUtf32(glyph, 0);
            }
            else
            {
                throw new ArgumentException("Input glyph must be a single character or a valid surrogate pair.");
            }
        }
    }
}
