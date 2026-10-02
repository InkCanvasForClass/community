using System.Windows;

// 声明自定义控件（SettingsCard）的默认样式位于 Themes/Generic.xaml。
// 没有这个特性，DefaultStyleKeyProperty 找不到样式，所有 SettingsCard 都会渲染成空白。
[assembly: ThemeInfo(
    ResourceDictionaryLocation.None,
    ResourceDictionaryLocation.SourceAssembly)]