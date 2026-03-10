using Microsoft.International.Converters.PinYinConverter;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas.Windows.SettingsViews
{
    /// <summary>
    /// SearchPanel.xaml 的交互逻辑
    /// </summary>
    public partial class SearchPanel : UserControl
    {
        public event EventHandler<string> NavigateToItem;
        public event EventHandler CloseSearch;

        private ObservableCollection<SearchResultItem> _exactMatches = new ObservableCollection<SearchResultItem>();
        private ObservableCollection<SearchResultItem> _fuzzyMatches = new ObservableCollection<SearchResultItem>();
        private ObservableCollection<SearchResultItem> _relatedItems = new ObservableCollection<SearchResultItem>();

        private List<SettingItem> _allSettings = new List<SettingItem>();

        public SearchPanel()
        {
            InitializeComponent();
            InitializeSettings();
            ExactMatchItemsControl.ItemsSource = _exactMatches;
            FuzzyMatchItemsControl.ItemsSource = _fuzzyMatches;
            RelatedItemsControl.ItemsSource = _relatedItems;
        }

        private void InitializeSettings()
        {
            // 初始化所有设置项数据
            _allSettings = new List<SettingItem>
            {
                // 启动时行为
                new SettingItem { Title = "启动时行为", Category = "启动时行为", ItemName = "StartupItem", Type = SettingItemType.Category },
                
                // 画板和墨迹
                new SettingItem { Title = "画板和墨迹", Category = "画板和墨迹", ItemName = "CanvasAndInkItem", Type = SettingItemType.Category },
                
                // 手势操作
                new SettingItem { Title = "手势操作", Category = "手势操作", ItemName = "GesturesItem", Type = SettingItemType.Category },
                
                // 墨迹纠正
                new SettingItem { Title = "墨迹纠正", Category = "墨迹纠正", ItemName = "InkRecognitionItem", Type = SettingItemType.Category },
                
                // 个性化设置
                new SettingItem { Title = "个性化设置", Category = "个性化设置", ItemName = "ThemeItem", Type = SettingItemType.Category },
                
                // 快捷键设置
                new SettingItem { Title = "快捷键设置", Category = "快捷键设置", ItemName = "ShortcutsItem", Type = SettingItemType.Category },
                
                // 崩溃处理
                new SettingItem { Title = "崩溃处理", Category = "崩溃处理", ItemName = "CrashActionItem", Type = SettingItemType.Category },
                
                // PowerPoint 支持
                new SettingItem { Title = "PowerPoint 支持", Category = "PowerPoint 支持", ItemName = "PowerPointItem", Type = SettingItemType.Category },
                
                // 自动化行为
                new SettingItem { Title = "自动化行为", Category = "自动化行为", ItemName = "AutomationItem", Type = SettingItemType.Category },
                
                // 随机点名
                new SettingItem { Title = "随机点名", Category = "随机点名", ItemName = "LuckyRandomItem", Type = SettingItemType.Category },
                
                // 高级选项
                new SettingItem { Title = "高级选项", Category = "高级选项", ItemName = "AdvancedItem", Type = SettingItemType.Category },
                
                // 截图和屏幕捕捉
                new SettingItem { Title = "截图和屏幕捕捉", Category = "截图和屏幕捕捉", ItemName = "SnapshotItem", Type = SettingItemType.Category },
                
                // 更新中心
                new SettingItem { Title = "更新中心", Category = "更新中心", ItemName = "UpdateCenterItem", Type = SettingItemType.Category },
                
                // 关于
                new SettingItem { Title = "关于 InkCanvasForClass", Category = "关于", ItemName = "AboutItem", Type = SettingItemType.Category },
            };
        }

        public void PerformSearch(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                ClearResults();
                return;
            }

            _exactMatches.Clear();
            _fuzzyMatches.Clear();
            _relatedItems.Clear();

            var searchLower = searchText.ToLower();
            var exactMatchSet = new HashSet<string>();
            var fuzzyMatchSet = new HashSet<string>();

            // 精准匹配和拼音匹配
            foreach (var setting in _allSettings)
            {
                var titleLower = setting.Title.ToLower();
                var categoryLower = setting.Category.ToLower();

                // 精准匹配
                if (titleLower.Contains(searchLower) || categoryLower.Contains(searchLower))
                {
                    if (!exactMatchSet.Contains(setting.ItemName))
                    {
                        _exactMatches.Add(new SearchResultItem
                        {
                            Title = setting.Title,
                            Category = setting.Category,
                            ItemName = setting.ItemName,
                            Type = setting.Type,
                            MatchType = MatchType.Exact
                        });
                        exactMatchSet.Add(setting.ItemName);
                    }
                }
                // 拼音匹配
                else if (ContainsPinyinMatch(setting.Title, searchText) || ContainsPinyinMatch(setting.Category, searchText))
                {
                    if (!exactMatchSet.Contains(setting.ItemName))
                    {
                        _exactMatches.Add(new SearchResultItem
                        {
                            Title = setting.Title,
                            Category = setting.Category,
                            ItemName = setting.ItemName,
                            Type = setting.Type,
                            MatchType = MatchType.Pinyin
                        });
                        exactMatchSet.Add(setting.ItemName);
                    }
                }
            }

            // 模糊匹配
            foreach (var setting in _allSettings)
            {
                if (exactMatchSet.Contains(setting.ItemName))
                    continue;

                var searchableText = $"{setting.Title} {setting.Category} {setting.Description}".ToLower();
                if (FuzzyMatch(searchableText, searchLower))
                {
                    if (!fuzzyMatchSet.Contains(setting.ItemName))
                    {
                        _fuzzyMatches.Add(new SearchResultItem
                        {
                            Title = setting.Title,
                            Category = setting.Category,
                            ItemName = setting.ItemName,
                            Type = setting.Type,
                            MatchType = MatchType.Fuzzy
                        });
                        fuzzyMatchSet.Add(setting.ItemName);
                    }
                }
            }

            // 相关项
            var allMatched = new HashSet<string>(exactMatchSet.Concat(fuzzyMatchSet));
            foreach (var setting in _allSettings)
            {
                if (!allMatched.Contains(setting.ItemName))
                {
                    // 简单的相关性判断
                    if (IsRelated(setting, searchText))
                    {
                        _relatedItems.Add(new SearchResultItem
                        {
                            Title = setting.Title,
                            Category = setting.Category,
                            ItemName = setting.ItemName,
                            Type = setting.Type,
                            MatchType = MatchType.Related
                        });
                    }
                }
            }

            UpdateResultsVisibility();
        }

        private bool ContainsPinyinMatch(string text, string search)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(search))
                return false;

            try
            {
                // 将搜索词转换为小写
                var searchLower = search.ToLower();

                // 获取文本的拼音首字母和全拼
                var pinyinInitials = GetPinyinInitials(text);
                var pinyinFull = GetPinyinFull(text);

                // 检查搜索词是否匹配拼音首字母或全拼
                if (pinyinInitials.ToLower().Contains(searchLower) ||
                    pinyinFull.ToLower().Contains(searchLower))
                {
                    return true;
                }
            }
            catch
            {
                // 如果拼音转换失败，返回false
                return false;
            }

            return false;
        }

        private string GetPinyinInitials(string text)
        {
            var sb = new StringBuilder();
            foreach (char c in text)
            {
                if (IsChinese(c))
                {
                    try
                    {
                        var chineseChar = new ChineseChar(c);
                        if (chineseChar.PinyinCount > 0)
                        {
                            var pinyin = chineseChar.Pinyins[0];
                            if (!string.IsNullOrEmpty(pinyin) && pinyin.Length > 0)
                            {
                                // 获取首字母（移除音调数字后取第一个字母）
                                var firstChar = Regex.Replace(pinyin, @"\d", "")[0];
                                sb.Append(firstChar);
                            }
                        }
                    }
                    catch
                    {
                        sb.Append(c);
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private string GetPinyinFull(string text)
        {
            var sb = new StringBuilder();
            foreach (char c in text)
            {
                if (IsChinese(c))
                {
                    try
                    {
                        var chineseChar = new ChineseChar(c);
                        if (chineseChar.PinyinCount > 0)
                        {
                            var pinyin = chineseChar.Pinyins[0];
                            // 移除音调数字
                            pinyin = Regex.Replace(pinyin, @"\d", "");
                            sb.Append(pinyin);
                        }
                    }
                    catch
                    {
                        sb.Append(c);
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private bool IsChinese(char c)
        {
            return c >= 0x4e00 && c <= 0x9fbb;
        }

        private bool FuzzyMatch(string text, string search)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(search))
                return false;

            // 简单的模糊匹配：检查搜索词的字符是否按顺序出现在文本中
            int searchIndex = 0;
            foreach (char c in text)
            {
                if (searchIndex < search.Length && c == search[searchIndex])
                {
                    searchIndex++;
                    if (searchIndex == search.Length)
                        return true;
                }
            }
            return false;
        }

        private bool IsRelated(SettingItem setting, string search)
        {
            // 简单的相关性判断，可以根据需要改进
            // 例如：检查是否有共同的关键词等
            return false; // 暂时禁用相关项功能
        }

        private void UpdateResultsVisibility()
        {
            ExactMatchPanel.Visibility = _exactMatches.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            FuzzyMatchPanel.Visibility = _fuzzyMatches.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            RelatedItemsPanel.Visibility = _relatedItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            bool hasResults = _exactMatches.Count > 0 || _fuzzyMatches.Count > 0 || _relatedItems.Count > 0;
            NoResultsText.Visibility = hasResults ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ClearResults()
        {
            _exactMatches.Clear();
            _fuzzyMatches.Clear();
            _relatedItems.Clear();
            UpdateResultsVisibility();
        }

        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                CloseSearch?.Invoke(this, EventArgs.Empty);
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox != null)
            {
                PerformSearch(textBox.Text);
            }
        }

        private void SearchTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            textBox?.SelectAll();
        }

        private void CloseSearchButton_Click(object sender, MouseButtonEventArgs e)
        {
            CloseSearch?.Invoke(this, EventArgs.Empty);
        }

        private void SearchResultItem_Click(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            if (border?.Tag is SearchResultItem item)
            {
                NavigateToItem?.Invoke(this, item.ItemName);
            }
        }

        public void FocusSearchBox()
        {
            SearchTextBox.Focus();
        }

        public void SetSearchText(string text)
        {
            SearchTextBox.Text = text;
            PerformSearch(text);
        }

        /// <summary>
        /// 应用主题
        /// </summary>
        public void ApplyTheme()
        {
            try
            {
                bool isDarkTheme = ThemeHelper.IsDarkTheme;

                // 更新主背景
                if (SearchPanelMainGrid != null)
                {
                    SearchPanelMainGrid.Background = ThemeHelper.GetBackgroundPrimaryBrush();
                }

                // 更新顶部栏背景
                if (SearchPanelTopBarBorder != null)
                {
                    SearchPanelTopBarBorder.Background = ThemeHelper.GetBackgroundPrimaryBrush();
                }

                // 更新搜索输入框
                if (SearchInputBorder != null)
                {
                    SearchInputBorder.Background = ThemeHelper.GetTextBoxBackgroundBrush();
                    SearchInputBorder.BorderBrush = ThemeHelper.GetTextBoxBorderBrush();
                }

                // 更新搜索框文字颜色
                if (SearchTextBox != null)
                {
                    SearchTextBox.Foreground = ThemeHelper.GetTextPrimaryBrush();
                }

                // 更新标题文字颜色
                if (ExactMatchTitle != null)
                {
                    ExactMatchTitle.Foreground = ThemeHelper.GetTextPrimaryBrush();
                }
                if (FuzzyMatchTitle != null)
                {
                    FuzzyMatchTitle.Foreground = ThemeHelper.GetTextPrimaryBrush();
                }
                if (RelatedItemsTitle != null)
                {
                    RelatedItemsTitle.Foreground = ThemeHelper.GetTextPrimaryBrush();
                }

                // 更新无结果提示文字颜色
                if (NoResultsText != null)
                {
                    NoResultsText.Foreground = ThemeHelper.GetTextSecondaryBrush();
                }

                // 更新搜索框中的图标颜色
                UpdateSearchIconColor(isDarkTheme);

                // 使用 ThemeHelper 递归更新其他元素（如搜索结果项）
                ThemeHelper.ApplyThemeToControl(this);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SearchPanel 应用主题时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新搜索框中的图标颜色
        /// </summary>
        private void UpdateSearchIconColor(bool isDarkTheme)
        {
            try
            {
                Color iconColor = isDarkTheme
                    ? Color.FromRgb(243, 243, 243) // 深色主题使用浅色图标 #F3F3F3
                    : Color.FromRgb(34, 34, 34);   // 浅色主题使用深色图标 #222222

                // 查找搜索输入框中的图标
                if (SearchInputBorder != null)
                {
                    var image = FindVisualChild<Image>(SearchInputBorder);
                    if (image != null && image.Source is DrawingImage drawingImage)
                    {
                        if (drawingImage.Drawing is DrawingGroup drawingGroup)
                        {
                            var clonedDrawing = CloneDrawingGroup(drawingGroup, iconColor);
                            image.Source = new DrawingImage { Drawing = clonedDrawing };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新搜索图标颜色时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 克隆 DrawingGroup 并更新颜色
        /// </summary>
        private DrawingGroup CloneDrawingGroup(DrawingGroup source, Color newColor)
        {
            var cloned = new DrawingGroup();
            cloned.ClipGeometry = source.ClipGeometry?.Clone();
            cloned.Opacity = source.Opacity;
            cloned.Transform = source.Transform?.Clone();

            foreach (var drawing in source.Children)
            {
                if (drawing is GeometryDrawing geometryDrawing)
                {
                    var clonedGeometry = geometryDrawing.Geometry?.Clone();
                    var clonedBrush = CloneBrush(geometryDrawing.Brush, newColor);
                    var clonedPen = geometryDrawing.Pen != null
                        ? ClonePen(geometryDrawing.Pen, newColor)
                        : null;

                    cloned.Children.Add(new GeometryDrawing(clonedBrush, clonedPen, clonedGeometry));
                }
                else if (drawing is DrawingGroup subGroup)
                {
                    cloned.Children.Add(CloneDrawingGroup(subGroup, newColor));
                }
                else
                {
                    cloned.Children.Add(drawing);
                }
            }

            return cloned;
        }

        /// <summary>
        /// 克隆 Brush 并更新颜色
        /// </summary>
        private Brush CloneBrush(Brush source, Color newColor)
        {
            if (source is SolidColorBrush solidBrush)
            {
                var originalColor = solidBrush.Color;
                if (originalColor.R == 34 && originalColor.G == 34 && originalColor.B == 34) // #222222
                {
                    return new SolidColorBrush(newColor) { Opacity = solidBrush.Opacity };
                }
                else if (originalColor.A > 0 && originalColor != Colors.Transparent &&
                         originalColor.R < 50 && originalColor.G < 50 && originalColor.B < 50) // 深色
                {
                    return new SolidColorBrush(newColor) { Opacity = solidBrush.Opacity };
                }
                return new SolidColorBrush(originalColor) { Opacity = solidBrush.Opacity };
            }
            return source?.Clone();
        }

        /// <summary>
        /// 克隆 Pen 并更新颜色
        /// </summary>
        private Pen ClonePen(Pen source, Color newColor)
        {
            var clonedBrush = CloneBrush(source.Brush, newColor);
            return new Pen(clonedBrush, source.Thickness)
            {
                StartLineCap = source.StartLineCap,
                EndLineCap = source.EndLineCap,
                LineJoin = source.LineJoin,
                MiterLimit = source.MiterLimit
            };
        }

        /// <summary>
        /// 在视觉树中查找指定类型的子元素
        /// </summary>
        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                {
                    return result;
                }
                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }
            return null;
        }
    }

    public class SettingItem
    {
        public string Title { get; set; }
        public string Category { get; set; }
        public string ItemName { get; set; }
        public string Description { get; set; } = "";
        public SettingItemType Type { get; set; }
    }

    public class SearchResultItem
    {
        public string Title { get; set; }
        public string Category { get; set; }
        public string ItemName { get; set; }
        public SettingItemType Type { get; set; }
        public MatchType MatchType { get; set; }
        public string Description { get; set; } = "";
    }

    public enum SettingItemType
    {
        Category,
        Setting
    }

    public enum MatchType
    {
        Exact,
        Pinyin,
        Fuzzy,
        Related
    }
}

