using System.Windows;
using System.Windows.Media;
using WpfUiCompat.Common.IconKeys;

namespace WpfUiCompat.Controls
{
    /// <summary>
    /// 兼容 iNKORE FontIcon API 的图标控件，基于 WPF-UI FontIcon 实现。
    /// 支持通过 <see cref="Icon"/> 属性直接绑定 <see cref="SegoeFluentIcons"/> 的字形键。
    /// </summary>
    public class FontIcon : Wpf.Ui.Controls.FontIcon
    {
        public const string SegoeIconsFontFamilyName = "Segoe Fluent Icons,Segoe MDL2 Assets,Segoe UI Symbol";

        public FontIcon()
        {
            CompatStyleHelper.AttachBaseStyle(this, typeof(Wpf.Ui.Controls.FontIcon));
            SetCurrentValue(FontFamilyProperty, new FontFamily(SegoeIconsFontFamilyName));
            SetCurrentValue(FontSizeProperty, 16d);
        }

        public FontIcon(FontIconData icon) : this()
        {
            Icon = icon;
        }

        public FontIcon(string glyph, FontFamily fontFamily) : this()
        {
            Glyph = glyph;
            if (fontFamily != null)
            {
                FontFamily = fontFamily;
            }
        }

        /// <summary>
        /// 标识 <see cref="Icon"/> 依赖属性。
        /// </summary>
        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register(
                nameof(Icon),
                typeof(FontIconData?),
                typeof(FontIcon),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure, OnIconChanged));

        /// <summary>
        /// 获取或设置包装的图标（包含 Glyph 与 FontFamily）。
        /// </summary>
        public FontIconData? Icon
        {
            get => (FontIconData?)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        private static void OnIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FontIcon fontIcon && e.NewValue is FontIconData data)
            {
                fontIcon.Glyph = data.Glyph ?? string.Empty;
                if (data.FontFamily != null)
                {
                    fontIcon.FontFamily = data.FontFamily;
                }
            }
        }
    }
}