using Ink_Canvas.Helpers;
using Ink_Canvas.Plugins;
using Ink_Canvas.Properties;
using iNKORE.UI.WPF.Modern.Common.IconKeys;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class PluginPage : iNKORE.UI.WPF.Modern.Controls.Page
    {
        private readonly PluginMarketService _market = PluginMarketService.Instance;

        public PluginPage()
        {
            InitializeComponent();
            Loaded += PluginPage_Loaded;
        }

        private async void PluginPage_Loaded(object sender, RoutedEventArgs e)
        {
            // 确保市场索引已加载（用于检查更新）
            if (_market.MergedPlugins == null || _market.MergedPlugins.Count == 0)
            {
                _market.LoadFromCache();
            }
            LoadPlugins();
        }

        public void LoadPlugins()
        {
            try
            {
                var pluginManager = PluginManager.Instance;
                var plugins = pluginManager.Plugins;

                PluginCountText.Text = string.Format(PluginStrings.Plugin_LoadedCount, plugins.Count);

                if (plugins.Count == 0)
                {
                    PluginContainer.Children.Clear();
                    var emptyPanel = new StackPanel
                    {
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 40, 0, 0)
                    };
                    var icon = new iNKORE.UI.WPF.Modern.Controls.FontIcon
                    {
                        Icon = SegoeFluentIcons.Puzzle,
                        FontSize = 48,
                        Opacity = 0.4,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 8)
                    };
                    var noPluginText = new TextBlock
                    {
                        Text = PluginStrings.Plugin_NoPlugins,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = TryFindResource("TextFillColorTertiaryBrush") as SolidColorBrush ?? Brushes.Gray,
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    emptyPanel.Children.Add(icon);
                    emptyPanel.Children.Add(noPluginText);
                    PluginContainer.Children.Add(emptyPanel);
                    return;
                }

                PluginContainer.Children.Clear();

                foreach (var pluginInfo in plugins)
                {
                    var pluginCard = CreatePluginCard(pluginInfo);
                    PluginContainer.Children.Add(pluginCard);
                }
            }
            catch (Exception ex)
            {
                PluginCountText.Text = string.Format(PluginStrings.Plugin_LoadError, ex.Message);
            }
        }

        private Border CreatePluginCard(PluginInfo pluginInfo)
        {
            var card = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(16)
            };
            card.SetResourceReference(Border.BackgroundProperty, "CardBackgroundFillColorDefaultBrush");
            card.SetResourceReference(Border.BorderBrushProperty, "CardStrokeColorDefaultBrush");

            var mainGrid = new Grid();
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // 图标
            var iconBorder = new Border
            {
                Width = 40, Height = 40,
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new iNKORE.UI.WPF.Modern.Controls.FontIcon
                {
                    Icon = SegoeFluentIcons.Puzzle, FontSize = 20,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            iconBorder.SetResourceReference(Border.BackgroundProperty, "ControlFillColorDefaultBrush");
            Grid.SetColumn(iconBorder, 0);
            mainGrid.Children.Add(iconBorder);

            // 信息面板
            var infoPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            var titlePanel = new StackPanel { Orientation = Orientation.Horizontal };
            var nameText = new TextBlock
            {
                Text = pluginInfo.Name, FontSize = 15, FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 8, 0)
            };
            nameText.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");

            var versionText = new TextBlock
            {
                Text = string.Format("v{0}", pluginInfo.Version), FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            versionText.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorTertiaryBrush");

            titlePanel.Children.Add(nameText);
            titlePanel.Children.Add(versionText);

            // 检查是否有更新
            MergedPluginInfo marketInfo = null;
            if (_market.MergedPlugins != null)
            {
                marketInfo = _market.MergedPlugins.FirstOrDefault(m => m.Id == pluginInfo.Id && m.IsUpdateAvailable);
            }
            if (marketInfo != null)
            {
                var updateTag = new TextBlock
                {
                    Text = string.Format(PluginStrings.Plugin_UpdateAvailable, marketInfo.MarketVersion),
                    FontSize = 11, VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0),
                    Foreground = new SolidColorBrush((Color)Application.Current.FindResource("SystemAccentColor"))
                };
                titlePanel.Children.Add(updateTag);
            }

            var descriptionText = new TextBlock
            {
                Text = pluginInfo.Description, FontSize = 12,
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2)
            };
            descriptionText.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");

            var authorText = new TextBlock { Text = string.Format(PluginStrings.Plugin_Author, pluginInfo.Author), FontSize = 11 };
            authorText.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorTertiaryBrush");

            infoPanel.Children.Add(titlePanel);
            infoPanel.Children.Add(descriptionText);
            infoPanel.Children.Add(authorText);
            Grid.SetColumn(infoPanel, 1);
            mainGrid.Children.Add(infoPanel);

            // 操作按钮
            var actionPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            // 打开文件夹按钮
            var folderBtn = new Button
            {
                Padding = new Thickness(6), Margin = new Thickness(0, 0, 4, 0),
                ToolTip = PluginStrings.Market_OpenPluginsFolder, Tag = pluginInfo
            };
            folderBtn.Click += OpenFolder_Click;
            folderBtn.Content = new iNKORE.UI.WPF.Modern.Controls.FontIcon
            {
                Icon = SegoeFluentIcons.FolderOpen, FontSize = 14
            };
            actionPanel.Children.Add(folderBtn);

            // 更新按钮
            if (marketInfo != null)
            {
                var updateBtn = new Button
                {
                    Padding = new Thickness(6), Margin = new Thickness(0, 0, 4, 0),
                    ToolTip = PluginStrings.Plugin_Update, Tag = marketInfo
                };
                updateBtn.Click += UpdatePlugin_Click;
                updateBtn.Content = new iNKORE.UI.WPF.Modern.Controls.FontIcon
                {
                    Icon = SegoeFluentIcons.Upload, FontSize = 14
                };
                actionPanel.Children.Add(updateBtn);
            }

            // 删除按钮
            var deleteBtn = new Button
            {
                Padding = new Thickness(6), Margin = new Thickness(0, 0, 4, 0),
                ToolTip = PluginStrings.Plugin_Delete, Tag = pluginInfo
            };
            deleteBtn.Click += DeletePlugin_Click;
            deleteBtn.Content = new iNKORE.UI.WPF.Modern.Controls.FontIcon
            {
                Icon = SegoeFluentIcons.Delete, FontSize = 14
            };
            actionPanel.Children.Add(deleteBtn);

            Grid.SetColumn(actionPanel, 2);
            mainGrid.Children.Add(actionPanel);

            card.Child = mainGrid;
            return card;
        }

        #region 操作事件

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as FrameworkElement;
            var info = btn?.Tag as PluginInfo;
            if (info?.PluginFolderPath == null) return;

            try
            {
                if (Directory.Exists(info.PluginFolderPath))
                    Process.Start(new ProcessStartInfo { FileName = info.PluginFolderPath, UseShellExecute = true });
            }
            catch { }
        }

        private async void UpdatePlugin_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as FrameworkElement;
            var marketInfo = btn?.Tag as MergedPluginInfo;
            if (marketInfo == null) return;

            var result = iNKORE.UI.WPF.Modern.Controls.MessageBox.Show(
                string.Format(PluginStrings.Plugin_UpdateAvailable, marketInfo.MarketVersion) + "\n\n" + PluginStrings.Market_RestartMessage,
                PluginStrings.Plugin_Update,
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            // 解析依赖
            var deps = _market.ResolveDependencies(marketInfo.Id);
            foreach (var dep in deps)
                await _market.RequestDownloadPluginAsync(dep);

            await _market.RequestDownloadPluginAsync(marketInfo.Id);

            AskRestart();
        }

        private void DeletePlugin_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as FrameworkElement;
            var info = btn?.Tag as PluginInfo;
            if (info == null) return;

            var result = iNKORE.UI.WPF.Modern.Controls.MessageBox.Show(
                string.Format(PluginStrings.Plugin_DeleteConfirm, info.Name),
                PluginStrings.Plugin_DeleteTitle,
                MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                // 1. 卸载插件实例，释放 AssemblyLoadContext
                var loaded = PluginManager.Instance.Plugins.FirstOrDefault(p => p.Id == info.Id);
                if (loaded != null)
                    PluginManager.Instance.UnloadPlugin(loaded);

                // 2. 写入 .uninstall 标记
                var uninstallMarker = Path.Combine(info.PluginFolderPath, ".uninstall");
                File.WriteAllText(uninstallMarker, "");

                // 3. 标记为待卸载状态
                info.LoadStatus = PluginLoadStatus.Disabled;

                LoadPlugins();
                AskRestart();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"Plugin | 删除插件失败: {info.Id} - {ex.Message}", LogHelper.LogType.Error);
                iNKORE.UI.WPF.Modern.Controls.MessageBox.Show(
                    string.Format(PluginStrings.Market_InstallLocalFailed, ex.Message),
                    PluginStrings.Plugin_DeleteTitle,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AskRestart()
        {
            var result = iNKORE.UI.WPF.Modern.Controls.MessageBox.Show(
                PluginStrings.Market_RestartMessage,
                PluginStrings.Market_RestartTitle,
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var exePath = Process.GetCurrentProcess().MainModule.FileName;
                    Process.Start(exePath);
                    Application.Current.Shutdown();
                }
                catch (Exception ex)
                {
                    iNKORE.UI.WPF.Modern.Controls.MessageBox.Show(
                        string.Format(PluginStrings.Market_RestartFailed, ex.Message),
                        PluginStrings.Market_RestartTitle,
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion
    }
}
