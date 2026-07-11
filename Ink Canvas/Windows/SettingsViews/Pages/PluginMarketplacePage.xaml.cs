using Ink_Canvas.Plugins;
using Ink_Canvas.Properties;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class PluginMarketplacePage : iNKORE.UI.WPF.Modern.Controls.Page
    {
        private readonly PluginMarketService _market = PluginMarketService.Instance;
        private bool _showMarketOnly = true;
        private string _searchText = "";
        private List<MergedPluginInfo> _allPlugins = new List<MergedPluginInfo>();

        public PluginMarketplacePage()
        {
            InitializeComponent();
            _market.PropertyChanged += Market_PropertyChanged;
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            var lastRefresh = _market.GetLastRefreshTime();
            if (lastRefresh == null || (DateTime.Now - lastRefresh.Value).TotalDays >= 7)
            {
                LoadingBar.Visibility = Visibility.Visible;
                LoadingBar.IsIndeterminate = true;
                await _market.RefreshIndexAsync();
                LoadingBar.Visibility = Visibility.Collapsed;
                LoadingBar.IsIndeterminate = false;
            }
            else
            {
                _market.LoadFromCache();
            }
            _allPlugins = _market.MergedPlugins ?? new List<MergedPluginInfo>();
            RefreshList();
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            _market.PropertyChanged -= Market_PropertyChanged;
        }

        private void Market_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (e.PropertyName == nameof(PluginMarketService.MergedPlugins))
                {
                    _allPlugins = _market.MergedPlugins ?? new List<MergedPluginInfo>();
                    RefreshList();
                }
            });
        }

        #region 列表填充

        private void RefreshList()
        {
            var filtered = _allPlugins.Where(p =>
            {
                if (_showMarketOnly && !p.IsOnMarket) return false;
                if (!_showMarketOnly && !p.IsLocal) return false;
                if (!string.IsNullOrWhiteSpace(_searchText))
                {
                    var q = _searchText;
                    return (p.Id?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                        || (p.Name?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                        || (p.Description?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false);
                }
                return true;
            }).ToList();

            PluginListBox.Items.Clear();

            foreach (var p in filtered)
            {
                var item = CreatePluginListItem(p);
                PluginListBox.Items.Add(item);
            }
        }

        private ListBoxItem CreatePluginListItem(MergedPluginInfo p)
        {
            var item = new ListBoxItem
            {
                Padding = new Thickness(10),
                Margin = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Tag = p
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // 图标
            var iconBorder = new Border
            {
                Width = 36, Height = 36,
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new iNKORE.UI.WPF.Modern.Controls.FontIcon
                {
                    Icon = iNKORE.UI.WPF.Modern.Common.IconKeys.SegoeFluentIcons.Puzzle,
                    FontSize = 18,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            iconBorder.SetResourceReference(Border.BackgroundProperty, "ControlFillColorDefaultBrush");
            Grid.SetColumn(iconBorder, 0);
            grid.Children.Add(iconBorder);

            // 名称 + 描述
            var infoPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
            var nameText = new TextBlock
            {
                Text = p.Name,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            var versionText = new TextBlock
            {
                Text = p.VersionText,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            };
            versionText.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorTertiaryBrush");
            titleRow.Children.Add(nameText);
            titleRow.Children.Add(versionText);

            var descText = new TextBlock
            {
                Text = p.Description,
                FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            descText.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");

            infoPanel.Children.Add(titleRow);
            infoPanel.Children.Add(descText);
            Grid.SetColumn(infoPanel, 1);
            grid.Children.Add(infoPanel);

            // 操作按钮
            var actionPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (p.IsOnMarket && !p.IsLocal && !p.RestartRequired)
            {
                var installBtn = new Button { Padding = new Thickness(4), Tag = p.Id, ToolTip = PluginStrings.Market_Install };
                installBtn.Click += InstallButton_Click;
                installBtn.Content = new iNKORE.UI.WPF.Modern.Controls.FontIcon
                {
                    Icon = iNKORE.UI.WPF.Modern.Common.IconKeys.SegoeFluentIcons.Download, FontSize = 14
                };
                actionPanel.Children.Add(installBtn);
            }

            if (p.IsUpdateAvailable)
            {
                var updateBtn = new Button { Padding = new Thickness(4), Tag = p.Id, ToolTip = PluginStrings.Market_Update, Margin = new Thickness(4, 0, 0, 0) };
                updateBtn.Click += InstallButton_Click;
                updateBtn.Content = new iNKORE.UI.WPF.Modern.Controls.FontIcon
                {
                    Icon = iNKORE.UI.WPF.Modern.Common.IconKeys.SegoeFluentIcons.Upload, FontSize = 14
                };
                actionPanel.Children.Add(updateBtn);
            }

            if (p.IsLocal && !p.IsOnMarket)
            {
                var checkIcon = new iNKORE.UI.WPF.Modern.Controls.FontIcon
                {
                    Icon = iNKORE.UI.WPF.Modern.Common.IconKeys.SegoeFluentIcons.Completed,
                    FontSize = 14,
                    Margin = new Thickness(4, 0, 0, 0),
                    Foreground = new SolidColorBrush((Color)Application.Current.FindResource("SystemAccentColor"))
                };
                actionPanel.Children.Add(checkIcon);
            }

            if (!string.IsNullOrEmpty(p.DownloadCountText))
            {
                var countText = new TextBlock
                {
                    Text = p.DownloadCountText,
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6, 0, 0, 0)
                };
                countText.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorTertiaryBrush");
                actionPanel.Children.Add(countText);
            }

            Grid.SetColumn(actionPanel, 2);
            grid.Children.Add(actionPanel);

            item.Content = grid;
            return item;
        }

        #endregion

        #region 事件处理

        private void Tab_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            _showMarketOnly = (sender == TabMarket);
            RefreshList();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchText = (sender as TextBox)?.Text ?? "";
            RefreshList();
        }

        private void SearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
                RefreshList();
        }

        private void PluginListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 详情面板暂不实现
        }

        private async void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as FrameworkElement;
            var id = btn?.Tag as string;
            if (string.IsNullOrEmpty(id)) return;

            var deps = _market.ResolveDependencies(id);
            if (deps.Count > 0)
            {
                var msg = string.Format(PluginStrings.Market_DependencyWarning, string.Join(", ", deps));
                var result = iNKORE.UI.WPF.Modern.Controls.MessageBox.Show(msg, PluginStrings.Market_DependencyTitle, MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes) return;
                foreach (var dep in deps)
                    await _market.RequestDownloadPluginAsync(dep);
            }

            await _market.RequestDownloadPluginAsync(id);
            _allPlugins = _market.MergedPlugins ?? new List<MergedPluginInfo>();
            RefreshList();

            // 更新已安装插件后提示重启
            var updated = _allPlugins.FirstOrDefault(p => p.Id == id);
            if (updated != null && updated.RestartRequired)
            {
                AskRestart();
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadingBar.Visibility = Visibility.Visible;
            LoadingBar.IsIndeterminate = true;
            await _market.RefreshIndexAsync();
            LoadingBar.Visibility = Visibility.Collapsed;
            LoadingBar.IsIndeterminate = false;
            _allPlugins = _market.MergedPlugins ?? new List<MergedPluginInfo>();
            RefreshList();
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }

        private void InstallLocalButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Title = PluginStrings.Market_InstallFromLocal, Filter = "ICC-CE 插件包 (*.icpx)|*.icpx" };
            if (dialog.ShowDialog() != true) return;
            try
            {
                var packagesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PluginPackages");
                if (!Directory.Exists(packagesDir)) Directory.CreateDirectory(packagesDir);
                File.Copy(dialog.FileName, Path.Combine(packagesDir, Path.GetFileName(dialog.FileName)), true);

                // 热加载
                PluginManager.Instance.InstallPendingPackages();
                _allPlugins = _market.MergedPlugins ?? new List<MergedPluginInfo>();
                RefreshList();
            }
            catch (Exception ex)
            {
                iNKORE.UI.WPF.Modern.Controls.MessageBox.Show(string.Format(PluginStrings.Market_InstallLocalFailed, ex.Message), PluginStrings.Market_Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

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
    }
}
