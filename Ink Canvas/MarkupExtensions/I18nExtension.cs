using Ink_Canvas.Properties;
using System;
using System.Windows.Markup;

namespace Ink_Canvas.MarkupExtensions
{
    public class I18nExtension : MarkupExtension
    {
        public string Key { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return string.IsNullOrEmpty(Key) ? string.Empty : (Strings.GetString(Key) ?? ("#" + Key));
        }
    }
}