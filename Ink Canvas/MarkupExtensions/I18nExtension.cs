using Ink_Canvas.Properties;
using System;
using System.Windows.Markup;

namespace Ink_Canvas.MarkupExtensions
{
    /// <summary>
    /// XAML 中用键名取本地化字符串，无需在 Strings.Designer.cs 中为每个键添加属性。
    /// 用法：xmlns:i18n="clr-namespace:Ink_Canvas.MarkupExtensions" 然后 Text="{i18n:I18n Key=Settings_Title}"
    /// </summary>
    public class I18nExtension : MarkupExtension
    {
        public string Key { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return string.IsNullOrEmpty(Key) ? string.Empty : (Strings.GetString(Key) ?? $"#{Key}");
        }
    }
}
