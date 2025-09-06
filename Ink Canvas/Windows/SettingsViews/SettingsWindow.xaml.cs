using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Ink_Canvas.Windows.SettingsViews;
using iNKORE.UI.WPF.Helpers;
using iNKORE.UI.WPF.Modern.Controls;
using OSVersionExtension;

namespace Ink_Canvas.Windows {
    public partial class SettingsWindow : Window {

        public SettingsWindow() {
            InitializeComponent();

            // 初始化侧边栏项目
            SidebarItemsControl.ItemsSource = SidebarItems;
            SidebarItems.Add(new SidebarItem() {
                Type = SidebarItemType.Item,
                Title = "启动时行为",
                Name = "StartupItem",
                IconSource = FindResource("StartupIcon") as DrawingImage,
                Selected = false,
            });
            SidebarItems.Add(new SidebarItem() {
                Type = SidebarItemType.Item,
                Title = "画板和墨迹",
                Name = "CanvasAndInkItem",
                IconSource = FindResource("CanvasAndInkIcon") as DrawingImage,
                Selected = false,
            });
            SidebarItems.Add(new SidebarItem() {
                Type = SidebarItemType.Item,
                Title = "手势操作",
                Name = "GesturesItem",
                IconSource = FindResource("GesturesIcon") as DrawingImage,
                Selected = false,
            });
            SidebarItems.Add(new SidebarItem() {
                Type = SidebarItemType.Item,
                Title = "墨迹纠正",
                Name = "InkRecognitionItem",
                IconSource = FindResource("InkRecognitionIcon") as DrawingImage,
                Selected = false,
            });
            SidebarItems.Add(new SidebarItem() {
                Type = SidebarItemType.Separator
            });
            SidebarItems.Add(new SidebarItem() {
                Type = SidebarItemType.Item,
                Title = "个性化设置",
                Name = "ThemeItem",
                IconSource = FindResource("AppearanceIcon") as DrawingImage,
                Selected = false,
            });
            SidebarItems.Add(new SidebarItem() {
                Type = SidebarItemType.Item,
                Title = "快捷键设置",
                Name = "ShortcutsItem",
                IconSource = FindResource("AppearanceIcon") as DrawingImage,
                Selected = false,
            });
            SidebarItems.Add(new SidebarItem() {
                Type = SidebarItemType.Item,
                Title = "崩溃处理",
                Name = "CrashActionItem",
                IconSource = FindResource("AppearanceIcon") as DrawingImage,
                Selected = false,
            });
            SidebarItems.Add(new SidebarItem() {
                Type = SidebarItemType.Separator
            });
            SidebarItems.Add(new SidebarItem() {
                Type = SidebarItemType.Item,
                Title = "PowerPoint 支持",
                Name = "PowerPointItem",
                IconSource = FindResource("PowerPointIcon") as DrawingImage,
                Selected = false,
            });

            SidebarItems.Add(new SidebarItem() {
                Type = SidebarItemType.Item,
                Title = "自动化行为",
                Name = "AutomationItem",
                IconSource = FindResource("AutomationIcon") as DrawingImage,
                Selected = false,
            });
            SidebarItems.Add(new SidebarItem() {
                Type = SidebarItemType.Item,
                Title = "随机点名",
                Name = "LuckyRandomItem",
                IconSource = FindResource("LuckyRandomIcon") as DrawingImage,
                Selected = false,
            });
            SidebarItems.Add(new SidebarItem() {
                Type = SidebarItemType.Separator
            });
            SidebarItems.Add(new SidebarItem() {
                Type = SidebarItemType.Item,
                Title = "存储空间",
                Name = "StorageItem",
                IconSource = FindResource("StorageIcon") as DrawingImage,
                Selected = false,
            });
            SidebarItems.Add(new SidebarItem() {
                Type = SidebarItemType.Item,
                Title = "截图和屏幕捕捉",
                Name = "SnapshotItem",
                IconSource = FindResource("SnapshotIcon") as DrawingImage,
                Selected = false,
            });
            SidebarItems.Add(new SidebarItem() {
                Type = SidebarItemType.Separator
            });
            SidebarItems.Add(new SidebarItem() {
                Type = SidebarItemType.Item,
                Title = "高级选项",
                Name = "AdvancedItem",
                IconSource = FindResource("AdvancedIcon") as DrawingImage,
                Selected = false,
            });
            SidebarItems.Add(new SidebarItem() {
                Type = SidebarItemType.Item,
                Title = "关于 InkCanvasForClass",
                Name = "AboutItem",
                IconSource = FindResource("AboutIcon") as DrawingImage,
                Selected = false,
            });
            SettingsPanes = new Grid[] {
                AboutPane,
                CanvasAndInkPane,
                GesturesPane,
                StartupPane,
                ThemePane,
                ShortcutsPane,
                CrashActionPane,
                InkRecognitionPane,
                AutomationPane,
                PowerPointPane,
                LuckyRandomPane,
                StoragePane,
                SnapshotPane,
                AdvancedPane
            };

            SettingsPaneScrollViewers = new ScrollViewer[] {
                SettingsAboutPanel.AboutScrollViewerEx,
                CanvasAndInkScrollViewerEx,
                GesturesScrollViewerEx,
                StartupScrollViewerEx,
                ThemeScrollViewerEx,
                ShortcutsScrollViewerEx,
                CrashActionScrollViewerEx,
                InkRecognitionScrollViewerEx,
                AutomationScrollViewerEx,
                PowerPointScrollViewerEx,
                LuckyRandomScrollViewerEx,
                StorageScrollViewerEx,
                SnapshotScrollViewerEx,
                AdvancedScrollViewerEx
            };

            SettingsPaneTitles = new string[] {
                "关于",
                "画板和墨迹",
                "手势操作",
                "启动时行为",
                "个性化设置",
                "快捷键设置",
                "崩溃处理",
                "墨迹识别",
                "自动化",
                "PowerPoint",
                "幸运随机",
                "存储",
                "截图",
                "高级"
            };

            SettingsPaneNames = new string[] {
                "AboutItem",
                "CanvasAndInkItem",
                "GesturesItem",
                "StartupItem",
                "ThemeItem",
                "ShortcutsItem",
                "CrashActionItem",
                "InkRecognitionItem",
                "AutomationItem",
                "PowerPointItem",
                "LuckyRandomItem",
                "StorageItem",
                "SnapshotItem",
                "AdvancedItem"
            };

            SettingsAboutPanel.IsTopBarNeedShadowEffect += (o, s) => DropShadowEffectTopBar.Opacity = 0.25;
            SettingsAboutPanel.IsTopBarNeedNoShadowEffect += (o, s) => DropShadowEffectTopBar.Opacity = 0;

            _selectedSidebarItemName = "CanvasAndInkItem";
            UpdateSidebarItemsSelection();
            
            // 为自定义滑块控件添加触摸支持
            AddTouchSupportToCustomSliders();
        }



        public enum SidebarItemType {
            Item,
            Separator
        }

        public class SidebarItem {
            public SidebarItemType Type { get; set; }
            public string Title { get; set; }
            public string Name { get; set; }
            public ImageSource IconSource { get; set; }
            public bool Selected { get; set; }
            public Visibility _spVisibility {
                get => this.Type == SidebarItemType.Separator ? Visibility.Visible : Visibility.Collapsed;
            }
            public Visibility _siVisibility {
                get => this.Type == SidebarItemType.Item ? Visibility.Visible : Visibility.Collapsed;
            }

            public SolidColorBrush _siBackground {
                get => this.Selected
                    ? new SolidColorBrush(Color.FromRgb(217, 217, 217))
                    : new SolidColorBrush(Colors.Transparent);
            }
        }

        public string _selectedSidebarItemName = "";
        public ObservableCollection<SidebarItem> SidebarItems = new ObservableCollection<SidebarItem>();

        public Grid[] SettingsPanes;
        public ScrollViewer[] SettingsPaneScrollViewers;
        public string[] SettingsPaneTitles;
        public string[] SettingsPaneNames;

        public void UpdateSidebarItemsSelection() {
            foreach (var si in SidebarItems) {
                si.Selected = si.Name == _selectedSidebarItemName;
                if (si.Selected && SettingsWindowTitle != null) {
                    SettingsWindowTitle.Text = si.Title;
                }
            }
            CollectionViewSource.GetDefaultView(SidebarItems).Refresh();

            if (AboutPane != null) AboutPane.Visibility = _selectedSidebarItemName == "AboutItem" ? Visibility.Visible : Visibility.Collapsed;
            if (CanvasAndInkPane != null) CanvasAndInkPane.Visibility = _selectedSidebarItemName == "CanvasAndInkItem" ? Visibility.Visible : Visibility.Collapsed;
            if (GesturesPane != null) GesturesPane.Visibility = _selectedSidebarItemName == "GesturesItem" ? Visibility.Visible : Visibility.Collapsed;
            if (StartupPane != null) StartupPane.Visibility = _selectedSidebarItemName == "StartupItem" ? Visibility.Visible : Visibility.Collapsed;
            if (ThemePane != null) ThemePane.Visibility = _selectedSidebarItemName == "ThemeItem" ? Visibility.Visible : Visibility.Collapsed;
            if (ShortcutsPane != null) ShortcutsPane.Visibility = _selectedSidebarItemName == "ShortcutsItem" ? Visibility.Visible : Visibility.Collapsed;
            if (CrashActionPane != null) CrashActionPane.Visibility = _selectedSidebarItemName == "CrashActionItem" ? Visibility.Visible : Visibility.Collapsed;
            if (InkRecognitionPane != null) InkRecognitionPane.Visibility = _selectedSidebarItemName == "InkRecognitionItem" ? Visibility.Visible : Visibility.Collapsed;
            if (AutomationPane != null) AutomationPane.Visibility = _selectedSidebarItemName == "AutomationItem" ? Visibility.Visible : Visibility.Collapsed;
            if (PowerPointPane != null) PowerPointPane.Visibility = _selectedSidebarItemName == "PowerPointItem" ? Visibility.Visible : Visibility.Collapsed;
            if (LuckyRandomPane != null) LuckyRandomPane.Visibility = _selectedSidebarItemName == "LuckyRandomItem" ? Visibility.Visible : Visibility.Collapsed;
            if (StoragePane != null) StoragePane.Visibility = _selectedSidebarItemName == "StorageItem" ? Visibility.Visible : Visibility.Collapsed;
            if (SnapshotPane != null) SnapshotPane.Visibility = _selectedSidebarItemName == "SnapshotItem" ? Visibility.Visible : Visibility.Collapsed;
            if (AdvancedPane != null) AdvancedPane.Visibility = _selectedSidebarItemName == "AdvancedItem" ? Visibility.Visible : Visibility.Collapsed;
            if (SettingsPaneScrollViewers != null) {
                foreach (var sv in SettingsPaneScrollViewers) {
                    if (sv != null) {
                        sv.ScrollToTop();
                    }
                }
            }
        }

        private void ScrollViewerEx_ScrollChanged(object sender, ScrollChangedEventArgs e) {
            var scrollViewer = (ScrollViewer)sender;
            if (scrollViewer.VerticalOffset >= 10) {
                DropShadowEffectTopBar.Opacity = 0.25;
            } else {
                DropShadowEffectTopBar.Opacity = 0;
            }
        }

        private void ScrollBar_Scroll(object sender, RoutedEventArgs e) {
            var scrollbar = (ScrollBar)sender;
            var scrollviewer = scrollbar.FindAscendant<ScrollViewer>();
            if (scrollviewer != null) scrollviewer.ScrollToVerticalOffset(scrollbar.Track.Value);
        }

        private void ScrollBarTrack_MouseEnter(object sender, MouseEventArgs e) {
            var border = (Border)sender;
            if (border.Child is Track track) {
                track.Width = 16;
                track.Margin = new Thickness(0, 0, -2, 0);
                var scrollbar = track.FindAscendant<ScrollBar>();
                if (scrollbar != null) scrollbar.Width = 16;
                var grid = track.FindAscendant<Grid>();
                if (grid.FindDescendantByName("ScrollBarBorderTrackBackground") is Border backgroundBorder) {
                    backgroundBorder.Width = 8;
                    backgroundBorder.CornerRadius = new CornerRadius(4);
                    backgroundBorder.Opacity = 1;
                }
                var thumb = track.Thumb.Template.FindName("ScrollbarThumbEx", track.Thumb) ;
                if (thumb != null) {
                    var _thumb = thumb as Border;
                    _thumb.CornerRadius = new CornerRadius(4);
                    _thumb.Width = 8;
                    _thumb.Margin = new Thickness(-0.75, 0, 1, 0);
                    _thumb.Background = new SolidColorBrush(Color.FromRgb(138, 138, 138));
                }
            }
        }

        private void ScrollBarTrack_MouseLeave(object sender, MouseEventArgs e) {
            var border = (Border)sender;
            border.Background = new SolidColorBrush(Colors.Transparent);
            border.CornerRadius = new CornerRadius(0);
            if (border.Child is Track track) {
                track.Width = 6;
                track.Margin = new Thickness(0, 0, 0, 0);
                var scrollbar = track.FindAscendant<ScrollBar>();
                if (scrollbar != null) scrollbar.Width = 6;
                var grid = track.FindAscendant<Grid>();
                if (grid.FindDescendantByName("ScrollBarBorderTrackBackground") is Border backgroundBorder) {
                    backgroundBorder.Width = 3;
                    backgroundBorder.CornerRadius = new CornerRadius(1.5);
                    backgroundBorder.Opacity = 0;
                }
                var thumb = track.Thumb.Template.FindName("ScrollbarThumbEx", track.Thumb) ;
                if (thumb != null) {
                    var _thumb = thumb as Border;
                    _thumb.CornerRadius = new CornerRadius(1.5);
                    _thumb.Width = 3;
                    _thumb.Margin = new Thickness(0);
                    _thumb.Background = new SolidColorBrush(Color.FromRgb(195, 195, 195));
                }
            }
        }

        private void ScrollbarThumb_MouseDown(object sender, MouseButtonEventArgs e) {
            var thumb = (Thumb)sender;
            var border = thumb.Template.FindName("ScrollbarThumbEx",thumb);
            ((Border)border).Background = new SolidColorBrush(Color.FromRgb(95, 95, 95));
        }

        private void ScrollbarThumb_MouseUp(object sender, MouseButtonEventArgs e) {
            var thumb = (Thumb)sender;
            var border = thumb.Template.FindName("ScrollbarThumbEx",thumb);
            ((Border)border).Background = new SolidColorBrush(Color.FromRgb(138, 138, 138));
        }

        private Border _sidebarItemMouseDownBorder = null;

        private void SidebarItem_MouseDown(object sender, MouseButtonEventArgs e) {
            if (_sidebarItemMouseDownBorder != null || _sidebarItemMouseDownBorder == sender) return;
            _sidebarItemMouseDownBorder = (Border)sender;
            var bd = sender as Border;
            if (bd.FindDescendantByName("MouseFeedbackBorder") is Border feedbackBd) feedbackBd.Opacity = 0.12;
        }

        private void SidebarItem_MouseUp(object sender, MouseButtonEventArgs e) {
            if (_sidebarItemMouseDownBorder == null || _sidebarItemMouseDownBorder != sender) return;
            if (_sidebarItemMouseDownBorder.Tag is SidebarItem data) _selectedSidebarItemName = data.Name;
            SidebarItem_MouseLeave(sender, null);
            UpdateSidebarItemsSelection();
        }

        private void SidebarItem_MouseLeave(object sender, MouseEventArgs e) {
            if (_sidebarItemMouseDownBorder == null || _sidebarItemMouseDownBorder != sender) return;
            if (_sidebarItemMouseDownBorder.FindDescendantByName("MouseFeedbackBorder") is Border feedbackBd) feedbackBd.Opacity = 0;
            _sidebarItemMouseDownBorder = null;
        }

        private void CloseButton_Click(object sender, MouseButtonEventArgs e) {
            Close();
        }

        private void SearchButton_Click(object sender, MouseButtonEventArgs e) {
            // 搜索功能 - 可以显示搜索框或搜索对话框
        }

        private void MenuButton_Click(object sender, MouseButtonEventArgs e) {
            // 菜单功能 - 可以显示上下文菜单或选项菜单
        }

        private void ToggleSwitch_Click(object sender, MouseButtonEventArgs e) {
            var border = sender as Border;
            if (border != null) {
                // 切换开关状态
                bool isOn = border.Background.ToString() == "#FF3584E4";
                border.Background = isOn ? new SolidColorBrush(Color.FromRgb(225, 225, 225)) : new SolidColorBrush(Color.FromRgb(53, 132, 228));
                
                // 切换内部圆点的位置
                var innerBorder = border.Child as Border;
                if (innerBorder != null) {
                    innerBorder.HorizontalAlignment = isOn ? HorizontalAlignment.Left : HorizontalAlignment.Right;
                }

                // 根据Tag处理不同的设置项
                string tag = border.Tag?.ToString();
                if (!string.IsNullOrEmpty(tag)) {
                    HandleSettingChange(tag, !isOn);
                }
            }
        }

        private void HandleSettingChange(string settingName, bool value) {
            // 根据设置名称处理不同的设置项
            switch (settingName) {
                case "UseObviousCursor":
                    // 处理使用更加明显的画笔光标设置
                    break;
                case "HideInkWhenExitAnnotationMode":
                    // 处理退出批注模式后隐藏墨迹设置
                    break;
                case "DisablePenPressure":
                    // 处理禁用模拟笔锋设置
                    break;
                case "ClearInkWithoutHistory":
                    // 处理清空墨迹时不保留时光机历史记录设置
                    break;
                case "UseBlackBackgroundForTransparency":
                    // 处理AllowTransparency使用黑色背景设置
                    break;
                case "KeepHyperbolaAsymptote":
                    // 处理保留双曲线渐近线设置
                    break;
                case "UseInkEraser":
                    // 处理使用墨迹擦设置
                    break;
                case "UseDefaultBackgroundColorForNewPage":
                    // 处理创建新页时始终使用默认背景色设置
                    break;
                case "DebugMode":
                    // 处理调试模式设置
                    break;
                case "PerformanceMonitoring":
                    // 处理性能监控设置
                    break;
                case "AutoRestartOnCrash":
                    // 处理崩溃时自动重启设置
                    break;
                case "SendCrashReport":
                    // 处理发送崩溃报告设置
                    break;
                case "EnableLuckyRandom":
                    // 处理启用幸运随机功能设置
                    break;
                case "EnableInkToShape":
                    // 处理启用墨迹转形状设置
                    break;
                case "InkRecognitionRange":
                    // 处理墨迹识别范围设置
                    break;
                case "RecognizeQuadrilateral":
                    // 处理识别四边形设置
                    break;
                case "RecognizeTriangle":
                    // 处理识别三角形设置
                    break;
                case "RecognizeCircle":
                    // 处理识别圆形设置
                    break;
                case "RecognizeLine":
                    // 处理识别直线设置
                    break;
                case "EnablePressureSimulation":
                    // 处理启用三角形和矩形每边模拟压力值设置
                    break;
                case "ConcentricCircleCorrection":
                    // 处理同心圆识别矫正设置
                    break;
                case "EnableAutoHide":
                    // 处理启用自动收纳设置
                    break;
                case "EnableAutoKill":
                    // 处理启用自动查杀设置
                    break;
                case "EnableWhiteboardKiller":
                    // 处理启用桌面画板悬浮窗杀手设置
                    break;
                case "EnablePowerPointCom":
                    // 处理启用 PowerPoint COM 支持设置
                    break;
                case "EnableWpsCom":
                    // 处理启用 WPS COM 支持设置
                    break;
                case "EnableVsto":
                    // 处理启用 VSTO 支持设置
                    break;
                default:
                    // 未知设置项
                    break;
            }
        }

        private void OptionButton_Click(object sender, MouseButtonEventArgs e) {
            var border = sender as Border;
            if (border != null) {
                string tag = border.Tag?.ToString();
                if (!string.IsNullOrEmpty(tag)) {
                    // 清除同组其他按钮的选中状态
                    ClearOtherOptionsInGroup(border, tag);
                    
                    // 设置当前按钮为选中状态
                    border.Background = new SolidColorBrush(Color.FromRgb(225, 225, 225));
                    var textBlock = border.Child as TextBlock;
                    if (textBlock != null) {
                        textBlock.FontWeight = FontWeights.Bold;
                    }

                    // 处理选项变化
                    HandleOptionChange(tag);
                }
            }
        }

        private void ClearOtherOptionsInGroup(Border currentBorder, string currentTag) {
            // 获取当前按钮所在的父容器
            var parent = currentBorder.Parent as StackPanel;
            if (parent != null) {
                // 获取组名（Tag中下划线前的部分）
                string groupName = currentTag.Split('_')[0];
                
                // 清除同组其他按钮的选中状态
                foreach (var child in parent.Children) {
                    if (child is Border border && border != currentBorder) {
                        string childTag = border.Tag?.ToString();
                        if (!string.IsNullOrEmpty(childTag) && childTag.StartsWith(groupName + "_")) {
                            border.Background = new SolidColorBrush(Colors.Transparent);
                            var textBlock = border.Child as TextBlock;
                            if (textBlock != null) {
                                textBlock.FontWeight = FontWeights.Normal;
                            }
                        }
                    }
                }
            }
        }

        private void HandleOptionChange(string optionTag) {
            // 根据选项标签处理不同的选项变化
            string[] parts = optionTag.Split('_');
            if (parts.Length >= 2) {
                string group = parts[0];
                string value = parts[1];
                
                switch (group) {
                    case "EraserSize":
                        // 处理板擦橡皮大小设置
                        break;
                    case "DefaultBackgroundColor":
                        // 处理默认背景色设置
                        break;
                    case "DefaultPaperFormat":
                        // 处理默认稿纸格式设置
                        break;
                    case "AutoSaveInterval":
                        // 处理自动保存间隔设置
                        break;
                    case "ScreenshotQuality":
                        // 处理截图质量设置
                        break;
                    case "ScreenshotFormat":
                        // 处理截图格式设置
                        break;
                    case "InkRecognitionBehavior":
                        // 处理墨迹识别后转换行为设置
                        break;
                    case "Theme":
                        // 处理主题设置
                        break;
                    case "Language":
                        // 处理语言设置
                        break;
                    default:
                        // 未知选项组
                        break;
                }
            }
        }

        #region 自定义滑块触摸支持

        /// <summary>
        /// 为自定义滑块控件添加触摸和手写笔事件支持
        /// </summary>
        private void AddTouchSupportToCustomSliders()
        {
            try
            {
                // 延迟执行，确保UI元素已加载
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    // 查找所有自定义滑块控件并添加触摸支持
                    AddTouchSupportToCustomSliderInPane(CanvasAndInkPane);
                    AddTouchSupportToCustomSliderInPane(ThemePane);
                    AddTouchSupportToCustomSliderInPane(PowerPointPane);
                    AddTouchSupportToCustomSliderInPane(AutomationPane);
                    AddTouchSupportToCustomSliderInPane(LuckyRandomPane);
                    AddTouchSupportToCustomSliderInPane(AdvancedPane);
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                // 记录错误但不影响程序运行
                System.Diagnostics.Debug.WriteLine($"添加自定义滑块触摸支持时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 为指定面板中的自定义滑块控件添加触摸支持
        /// </summary>
        /// <param name="pane">面板控件</param>
        private void AddTouchSupportToCustomSliderInPane(Grid pane)
        {
            if (pane == null) return;

            // 查找面板中的所有自定义滑块控件
            var customSliders = FindCustomSlidersInPanel(pane);
            
            foreach (var slider in customSliders)
            {
                AddTouchSupportToCustomSlider(slider);
            }
        }

        /// <summary>
        /// 在面板中查找自定义滑块控件
        /// </summary>
        /// <param name="panel">面板控件</param>
        /// <returns>自定义滑块控件列表</returns>
        private List<CustomSliderInfo> FindCustomSlidersInPanel(DependencyObject panel)
        {
            var customSliders = new List<CustomSliderInfo>();
            
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(panel); i++)
            {
                var child = VisualTreeHelper.GetChild(panel, i);
                
                // 检查是否是自定义滑块控件（包含GnomeSliderThumb图片的Grid）
                if (child is Grid grid)
                {
                    var customSlider = FindCustomSliderInGrid(grid);
                    if (customSlider != null)
                    {
                        customSliders.Add(customSlider);
                    }
                }
                
                // 递归查找子元素
                customSliders.AddRange(FindCustomSlidersInPanel(child));
            }
            
            return customSliders;
        }

        /// <summary>
        /// 在Grid中查找自定义滑块控件
        /// </summary>
        /// <param name="grid">Grid控件</param>
        /// <returns>自定义滑块信息</returns>
        private CustomSliderInfo FindCustomSliderInGrid(Grid grid)
        {
            // 查找包含GnomeSliderThumb图片的Grid
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(grid); i++)
            {
                var child = VisualTreeHelper.GetChild(grid, i);
                
                if (child is Image image && image.Source != null)
                {
                    var sourceName = image.Source.ToString();
                    if (sourceName.Contains("GnomeSliderThumb"))
                    {
                        // 找到滑块控件，创建自定义滑块信息
                        var customSlider = new CustomSliderInfo
                        {
                            Container = grid,
                            ThumbImage = image,
                            TrackBorder = FindTrackBorderInGrid(grid),
                            ValueBorder = FindValueBorderInGrid(grid)
                        };
                        
                        return customSlider;
                    }
                }
            }
            
            return null;
        }

        /// <summary>
        /// 在Grid中查找轨道Border
        /// </summary>
        /// <param name="grid">Grid控件</param>
        /// <returns>轨道Border</returns>
        private Border FindTrackBorderInGrid(Grid grid)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(grid); i++)
            {
                var child = VisualTreeHelper.GetChild(grid, i);
                
                if (child is Border border && border.Background != null)
                {
                    var brush = border.Background as SolidColorBrush;
                    if (brush != null && brush.Color.ToString() == "#FFDEDEDE")
                    {
                        return border;
                    }
                }
            }
            
            return null;
        }

        /// <summary>
        /// 在Grid中查找值显示Border
        /// </summary>
        /// <param name="grid">Grid控件</param>
        /// <returns>值显示Border</returns>
        private Border FindValueBorderInGrid(Grid grid)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(grid); i++)
            {
                var child = VisualTreeHelper.GetChild(grid, i);
                
                if (child is Border border && border.Background != null)
                {
                    var brush = border.Background as SolidColorBrush;
                    if (brush != null && brush.Color.ToString() == "#FF3584E4")
                    {
                        return border;
                    }
                }
            }
            
            return null;
        }

        /// <summary>
        /// 为自定义滑块控件添加触摸支持
        /// </summary>
        /// <param name="customSlider">自定义滑块信息</param>
        private void AddTouchSupportToCustomSlider(CustomSliderInfo customSlider)
        {
            if (customSlider?.Container == null) return;

            // 启用触摸和手写笔支持
            customSlider.Container.IsManipulationEnabled = true;

            // 添加触摸事件
            customSlider.Container.TouchDown += (s, e) => CustomSlider_TouchDown(s, e, customSlider);
            customSlider.Container.TouchMove += (s, e) => CustomSlider_TouchMove(s, e, customSlider);
            customSlider.Container.TouchUp += (s, e) => CustomSlider_TouchUp(s, e, customSlider);

            // 添加手写笔事件
            customSlider.Container.StylusDown += (s, e) => CustomSlider_StylusDown(s, e, customSlider);
            customSlider.Container.StylusMove += (s, e) => CustomSlider_StylusMove(s, e, customSlider);
            customSlider.Container.StylusUp += (s, e) => CustomSlider_StylusUp(s, e, customSlider);

            // 添加操作事件
            customSlider.Container.ManipulationStarted += (s, e) => CustomSlider_ManipulationStarted(s, e, customSlider);
            customSlider.Container.ManipulationDelta += (s, e) => CustomSlider_ManipulationDelta(s, e, customSlider);
            customSlider.Container.ManipulationCompleted += (s, e) => CustomSlider_ManipulationCompleted(s, e, customSlider);
        }

        /// <summary>
        /// 自定义滑块触摸按下事件处理
        /// </summary>
        private void CustomSlider_TouchDown(object sender, TouchEventArgs e, CustomSliderInfo customSlider)
        {
            customSlider.Container.CaptureTouch(e.TouchDevice);
            customSlider.IsTouchCaptured = true;
            var touchPoint = e.GetTouchPoint(customSlider.Container);
            UpdateCustomSliderValueFromPosition(customSlider, touchPoint.Position);
            e.Handled = true;
        }

        /// <summary>
        /// 自定义滑块触摸移动事件处理
        /// </summary>
        private void CustomSlider_TouchMove(object sender, TouchEventArgs e, CustomSliderInfo customSlider)
        {
            // 检查是否有触摸捕获
            if (!customSlider.IsTouchCaptured) return;
            var touchPoint = e.GetTouchPoint(customSlider.Container);
            UpdateCustomSliderValueFromPosition(customSlider, touchPoint.Position);
            e.Handled = true;
        }

        /// <summary>
        /// 自定义滑块触摸释放事件处理
        /// </summary>
        private void CustomSlider_TouchUp(object sender, TouchEventArgs e, CustomSliderInfo customSlider)
        {
            customSlider.Container.ReleaseTouchCapture(e.TouchDevice);
            customSlider.IsTouchCaptured = false;
            e.Handled = true;
        }

        /// <summary>
        /// 自定义滑块手写笔按下事件处理
        /// </summary>
        private void CustomSlider_StylusDown(object sender, StylusDownEventArgs e, CustomSliderInfo customSlider)
        {
            customSlider.Container.CaptureStylus();
            var stylusPoint = e.GetStylusPoints(customSlider.Container);
            if (stylusPoint.Count > 0)
            {
                UpdateCustomSliderValueFromPosition(customSlider, stylusPoint[0].ToPoint());
            }
            e.Handled = true;
        }

        /// <summary>
        /// 自定义滑块手写笔移动事件处理
        /// </summary>
        private void CustomSlider_StylusMove(object sender, StylusEventArgs e, CustomSliderInfo customSlider)
        {
            if (!customSlider.Container.IsStylusCaptured) return;
            var stylusPoint = e.GetStylusPoints(customSlider.Container);
            if (stylusPoint.Count > 0)
            {
                UpdateCustomSliderValueFromPosition(customSlider, stylusPoint[0].ToPoint());
            }
            e.Handled = true;
        }

        /// <summary>
        /// 自定义滑块手写笔释放事件处理
        /// </summary>
        private void CustomSlider_StylusUp(object sender, StylusEventArgs e, CustomSliderInfo customSlider)
        {
            customSlider.Container.ReleaseStylusCapture();
            e.Handled = true;
        }

        /// <summary>
        /// 自定义滑块操作开始事件处理
        /// </summary>
        private void CustomSlider_ManipulationStarted(object sender, ManipulationStartedEventArgs e, CustomSliderInfo customSlider)
        {
            e.Handled = true;
        }

        /// <summary>
        /// 自定义滑块操作变化事件处理
        /// </summary>
        private void CustomSlider_ManipulationDelta(object sender, ManipulationDeltaEventArgs e, CustomSliderInfo customSlider)
        {
            var manipulationOrigin = e.ManipulationOrigin;
            UpdateCustomSliderValueFromPosition(customSlider, manipulationOrigin);
            e.Handled = true;
        }

        /// <summary>
        /// 自定义滑块操作完成事件处理
        /// </summary>
        private void CustomSlider_ManipulationCompleted(object sender, ManipulationCompletedEventArgs e, CustomSliderInfo customSlider)
        {
            e.Handled = true;
        }

        /// <summary>
        /// 根据触摸/手写笔位置更新自定义滑块值
        /// </summary>
        /// <param name="customSlider">自定义滑块信息</param>
        /// <param name="position">触摸/手写笔位置</param>
        private void UpdateCustomSliderValueFromPosition(CustomSliderInfo customSlider, Point position)
        {
            if (customSlider?.TrackBorder == null || customSlider.ThumbImage == null) return;

            try
            {
                // 计算滑块轨道的实际位置和长度
                var trackWidth = customSlider.TrackBorder.ActualWidth;
                if (trackWidth <= 0) return;

                // 计算相对位置（0-1之间）
                var relativePosition = Math.Max(0, Math.Min(1, position.X / trackWidth));

                // 更新滑块位置
                var thumbTransform = customSlider.ThumbImage.RenderTransform as TranslateTransform;
                if (thumbTransform == null)
                {
                    thumbTransform = new TranslateTransform();
                    customSlider.ThumbImage.RenderTransform = thumbTransform;
                }

                // 计算新的滑块位置
                var newX = relativePosition * trackWidth;
                thumbTransform.X = newX;

                // 更新值显示Border的宽度
                if (customSlider.ValueBorder != null)
                {
                    var valueWidth = relativePosition * trackWidth;
                    customSlider.ValueBorder.Width = Math.Max(0, valueWidth);
                    
                    // 调整值显示Border的位置
                    var valueMargin = customSlider.ValueBorder.Margin;
                    customSlider.ValueBorder.Margin = new Thickness(0, valueMargin.Top, trackWidth - valueWidth, valueMargin.Bottom);
                }

                // 这里可以根据需要添加值变化事件处理
                // 例如：OnCustomSliderValueChanged(customSlider, relativePosition);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新自定义滑块值时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 自定义滑块信息类
        /// </summary>
        private class CustomSliderInfo
        {
            public Grid Container { get; set; }
            public Image ThumbImage { get; set; }
            public Border TrackBorder { get; set; }
            public Border ValueBorder { get; set; }
            public bool IsTouchCaptured { get; set; } = false;
        }

        #endregion
    }
}
