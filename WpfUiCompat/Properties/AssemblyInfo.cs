using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;

// 声明主题资源位置：WPF 据此从 Themes/Generic.xaml 加载自定义控件隐式样式
[assembly: ThemeInfo(
    ResourceDictionaryLocation.None,
    ResourceDictionaryLocation.SourceAssembly
)]

// 将原 iNKORE.UI.WPF.Modern 的 XML 命名空间映射到本兼容层，
// 这样现有 XAML（xmlns:ui="http://schemas.inkore.net/lib/ui/wpf/modern"）无需修改。
// 兼容层底层由 WPF-UI (lepoco/wpfui) 实现，iNKORE 程序集已被彻底移除。
[assembly: XmlnsDefinition("http://schemas.inkore.net/lib/ui/wpf/modern", "WpfUiCompat")]
[assembly: XmlnsDefinition("http://schemas.inkore.net/lib/ui/wpf/modern", "WpfUiCompat.Controls")]
[assembly: XmlnsDefinition("http://schemas.inkore.net/lib/ui/wpf/modern", "WpfUiCompat.Common.IconKeys")]
[assembly: XmlnsDefinition("http://schemas.inkore.net/lib/ui/wpf/modern", "WpfUiCompat.Helpers")]
[assembly: XmlnsDefinition("http://schemas.inkore.net/lib/ui/wpf", "WpfUiCompat.Controls")]
[assembly: XmlnsDefinition("http://schemas.inkore.net/lib/ui/wpf", "WpfUiCompat")]
[assembly: XmlnsPrefix("http://schemas.inkore.net/lib/ui/wpf/modern", "ui")]
