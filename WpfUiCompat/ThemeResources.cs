using System;

namespace WpfUiCompat
{
    /// <summary>
    /// 主题资源字典，兼容 iNKORE ThemeResources。Source 指向 WPF-UI 的亮/暗主题字典，
    /// 因此可被 WPF-UI 的 ApplicationThemeManager 识别并在应用级切换时自动替换。
    /// </summary>
    public class ThemeResources : System.Windows.ResourceDictionary
    {
        internal const string ThemePath = "pack://application:,,,/Wpf.Ui;component/Resources/Theme/";
        internal const string ControlsPath = "pack://application:,,,/Wpf.Ui;component/Resources/Wpf.Ui.xaml";

        private static ThemeResources _current;

        /// <summary>
        /// 获取当前进程内创建的第一个 ThemeResources 实例（与 iNKORE 行为一致）。
        /// </summary>
        public static ThemeResources Current => _current;

        public ThemeResources()
        {
            if (_current == null)
            {
                _current = this;
            }

            Source = new Uri(ThemePath + GetSystemThemeName() + ".xaml", UriKind.Absolute);
        }

        /// <summary>
        /// 获取或设置主题字典集合（兼容 iNKORE 的属性元素语法）。
        /// 兼容层将其中的条目直接并入本字典。
        /// </summary>
        public System.Windows.ResourceDictionary ThemeDictionaries
        {
            get { return _themeDictionaries ??= new System.Windows.ResourceDictionary(); }
            set
            {
                _themeDictionaries = value;
                if (value == null) return;
                foreach (System.Collections.DictionaryEntry entry in value)
                {
                    if (!Contains(entry.Key))
                    {
                        this[entry.Key] = entry.Value;
                    }
                }
            }
        }

        private System.Windows.ResourceDictionary _themeDictionaries;

        /// <summary>
        /// 兼容 iNKORE 的跨线程访问标记。WPF-UI 字典以 DynamicResource 方式被引用，
        /// 此属性仅作 XAML 兼容用途。
        /// </summary>
        public bool CanBeAccessedAcrossThreads
        {
            get { return _canBeAccessedAcrossThreads; }
            set { _canBeAccessedAcrossThreads = value; }
        }

        private bool _canBeAccessedAcrossThreads;

        internal static string GetSystemThemeName()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key?.GetValue("AppsUseLightTheme") is int useLight)
                    {
                        return useLight == 0 ? "Dark" : "Light";
                    }
                }
            }
            catch
            {
            }
            return "Light";
        }
    }

    /// <summary>
    /// 控件资源字典，兼容 iNKORE XamlControlsResources。Source 指向兼容层 CompatStyles.xaml，
    /// 后者合并 WPF-UI 全量控件样式（隐式样式），并补充项目引用的命名样式
    /// （DefaultTabControlStyle / DefaultTabItemStyle / TabControlPivotStyle / TabItemPivotStyle / AccentButtonStyle）。
    /// </summary>
    public class XamlControlsResources : System.Windows.ResourceDictionary
    {
        internal const string CompatStylesPath = "pack://application:,,,/InkCanvas.WpfUiCompat;component/Themes/CompatStyles.xaml";

        public XamlControlsResources()
        {
            Source = new System.Uri(CompatStylesPath, System.UriKind.Absolute);
        }
    }
}
