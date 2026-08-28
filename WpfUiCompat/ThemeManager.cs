using System;
using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Appearance;

namespace WpfUiCompat
{
    /// <summary>
    /// 主题管理器，兼容 iNKORE ThemeManager API（含 Current 单例与附加属性）。
    /// 底层通过 WPF-UI 的 ApplicationThemeManager / ApplicationAccentColorManager 实现应用级主题，
    /// 并通过替换元素级主题字典实现逐窗口主题。
    /// </summary>
    public class ThemeManager
    {
        private static ThemeManager _current;

        /// <summary>获取进程级单例。</summary>
        public static ThemeManager Current => _current ??= new ThemeManager();

        /// <summary>当前应用主题变化时触发。</summary>
        public static event EventHandler ActualThemeChanged;

        private ThemeManager()
        {
        }

        /// <summary>
        /// 获取或设置应用主题（经由 WPF-UI ApplicationThemeManager 应用到全局）。
        /// </summary>
        public ApplicationTheme ApplicationTheme
        {
            get
            {
                var theme = ApplicationThemeManager.GetAppTheme();
                return theme switch
                {
                    Wpf.Ui.Appearance.ApplicationTheme.Dark => ApplicationTheme.Dark,
                    Wpf.Ui.Appearance.ApplicationTheme.HighContrast => ApplicationTheme.HighContrast,
                    _ => ApplicationTheme.Light,
                };
            }
            set
            {
                var mapped = value switch
                {
                    ApplicationTheme.Dark => Wpf.Ui.Appearance.ApplicationTheme.Dark,
                    ApplicationTheme.HighContrast => Wpf.Ui.Appearance.ApplicationTheme.HighContrast,
                    _ => Wpf.Ui.Appearance.ApplicationTheme.Light,
                };
                ApplicationThemeManager.Apply(mapped, Wpf.Ui.Controls.WindowBackdropType.None, updateAccent: false);
                ActualThemeChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 获取或设置系统强调色（经由 WPF-UI ApplicationAccentColorManager 应用）。
        /// </summary>
        public Color? AccentColor
        {
            get
            {
                try
                {
                    return ApplicationAccentColorManager.SystemAccent;
                }
                catch
                {
                    return null;
                }
            }
            set
            {
                if (value is Color color)
                {
                    var current = ApplicationThemeManager.GetAppTheme();
                    ApplicationAccentColorManager.Apply(color, current, false);
                }
            }
        }

        #region 附加属性 RequestedTheme

        public static readonly DependencyProperty RequestedThemeProperty =
            DependencyProperty.RegisterAttached(
                "RequestedTheme",
                typeof(ElementTheme),
                typeof(ThemeManager),
                new PropertyMetadata(ElementTheme.Default, OnRequestedThemeChanged));

        public static void SetRequestedTheme(DependencyObject element, ElementTheme value)
        {
            element?.SetValue(RequestedThemeProperty, value);
        }

        public static ElementTheme GetRequestedTheme(DependencyObject element)
        {
            return element == null ? ElementTheme.Default : (ElementTheme)element.GetValue(RequestedThemeProperty);
        }

        private static void OnRequestedThemeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement fe)
            {
                ApplyElementTheme(fe, (ElementTheme)e.NewValue);
            }
        }

        #endregion

        #region 附加属性 IsThemeAware

        public static readonly DependencyProperty IsThemeAwareProperty =
            DependencyProperty.RegisterAttached(
                "IsThemeAware",
                typeof(bool),
                typeof(ThemeManager),
                new PropertyMetadata(false, OnIsThemeAwareChanged));

        public static void SetIsThemeAware(DependencyObject element, bool value)
        {
            element?.SetValue(IsThemeAwareProperty, value);
        }

        public static bool GetIsThemeAware(DependencyObject element)
        {
            return element != null && (bool)element.GetValue(IsThemeAwareProperty);
        }

        private static void OnIsThemeAwareChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Window window && (bool)e.NewValue)
            {
                // 随应用主题联动：清空本地覆盖，回退到应用级字典
                window.Loaded += (s, args) => ApplyElementTheme(window, ElementTheme.Default);
            }
        }

        #endregion

        /// <summary>
        /// 获取元素的实际主题（本地覆盖优先，其次应用主题）。
        /// </summary>
        public static ElementTheme GetActualTheme(DependencyObject element)
        {
            var local = FindLocalThemeDictionary(element as FrameworkElement);
            if (local?.Source != null && local.Source.ToString().Contains("Dark", StringComparison.OrdinalIgnoreCase))
            {
                return ElementTheme.Dark;
            }
            return Current.ApplicationTheme == ApplicationTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
        }

        internal static void ApplyElementTheme(FrameworkElement element, ElementTheme theme)
        {
            if (element == null) return;

            if (!element.IsLoaded && element is not Window)
            {
                element.Loaded += (s, e) => ApplyElementThemeCore(element, theme);
                return;
            }
            ApplyElementThemeCore(element, theme);
        }

        private static void ApplyElementThemeCore(FrameworkElement element, ElementTheme theme)
        {
            try
            {
                if (theme == ElementTheme.Default)
                {
                    RemoveLocalThemeDictionary(element);
                }
                else
                {
                    var source = new Uri(ThemeResources.ThemePath + (theme == ElementTheme.Dark ? "Dark" : "Light") + ".xaml", UriKind.Absolute);
                    ReplaceLocalThemeDictionary(element, new ResourceDictionary { Source = source });
                }

                if (element is Window window)
                {
                    var dark = theme == ElementTheme.Dark || (theme == ElementTheme.Default && Current.ApplicationTheme == ApplicationTheme.Dark);
                    Helpers.WindowHelper.ApplyImmersiveDarkMode(window, dark);
                }
            }
            catch
            {
            }
        }

        internal static ResourceDictionary FindLocalThemeDictionary(FrameworkElement element)
        {
            if (element?.Resources?.MergedDictionaries == null) return null;
            foreach (var dict in element.Resources.MergedDictionaries)
            {
                if (dict is ThemeResources) return dict;
                if (dict.Source != null && dict.Source.ToString().Contains("Wpf.Ui;component/Resources/Theme/", StringComparison.OrdinalIgnoreCase))
                {
                    return dict;
                }
            }
            return null;
        }

        private static void ReplaceLocalThemeDictionary(FrameworkElement element, ResourceDictionary newDictionary)
        {
            var dicts = element.Resources.MergedDictionaries;
            for (int i = 0; i < dicts.Count; i++)
            {
                if (dicts[i] is ThemeResources || (dicts[i].Source != null && dicts[i].Source.ToString().Contains("Wpf.Ui;component/Resources/Theme/", StringComparison.OrdinalIgnoreCase)))
                {
                    dicts[i] = newDictionary;
                    return;
                }
            }
            // 没有本地主题字典时插入到最前（优先于应用级）
            if (dicts.Count == 0) dicts.Add(newDictionary);
            else dicts.Insert(0, newDictionary);
        }

        private static void RemoveLocalThemeDictionary(FrameworkElement element)
        {
            var dicts = element.Resources.MergedDictionaries;
            for (int i = dicts.Count - 1; i >= 0; i--)
            {
                if (dicts[i] is ThemeResources || (dicts[i].Source != null && dicts[i].Source.ToString().Contains("Wpf.Ui;component/Resources/Theme/", StringComparison.OrdinalIgnoreCase)))
                {
                    dicts.RemoveAt(i);
                }
            }
        }
    }
}