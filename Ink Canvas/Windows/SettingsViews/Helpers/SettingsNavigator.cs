using System.Windows;

namespace Ink_Canvas.Windows.SettingsViews.Helpers
{
    /// <summary>
    /// 附加属性：用于在设置页面中标记控件对应的 Settings.json 键名，
    /// 配合 icc://settings/&lt;Page&gt;?key=&lt;JsonKey&gt; 深链接实现定位与高亮。
    /// </summary>
    public static class SettingsNavigator
    {
        public static readonly DependencyProperty SettingsKeyProperty = DependencyProperty.RegisterAttached(
            "SettingsKey", typeof(string), typeof(SettingsNavigator), new PropertyMetadata(null));

        public static void SetSettingsKey(DependencyObject obj, string value)
        {
            obj.SetValue(SettingsKeyProperty, value);
        }

        public static string GetSettingsKey(DependencyObject obj)
        {
            return (string)obj.GetValue(SettingsKeyProperty);
        }
    }
}