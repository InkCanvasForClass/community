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
        private string _searchText = "";
        private List<MergedPluginInfo> _allPlugins = new List<MergedPluginInfo>();
        private MergedPluginInfo _selectedPlugin;

        public PluginMarketplacePage()
        {
            InitializeComponent();
            _market.PropertyChanged += Market_PropertyChanged;
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                InitSourceMirrorSelectors();

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
                RefreshMirrorSelector();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PluginMarketplacePage | Page_Loaded: {ex}");
            }
        }

        #region 源/镜像管理

        private void InitSourceMirrorSelectors()
        {
            SourceCombo.Items.Clear();
            var active = _market.Sources.GetActiveSource();
            int idx = 0, selected = 0;
            foreach (var src in _market.Sources.Sources)
            {
                SourceCombo.Items.Add(PluginMarketSourcesService.DisplayNameOf(src));
                if (src.Id == active.Id) selected = idx;
                idx++;
            }
            // 官方源始终可见在最前
            SourceCombo.Items.Insert(0, PluginMarketSourcesService.DisplayNameOf(PluginMarketSourcesService.OfficialSource));
            if (string.Equals(active.Id, PluginMarketSourcesService.OfficialSource.Id, StringComparison.OrdinalIgnoreCase))
                selected = 0;
            SourceCombo.SelectedIndex = selected;
        }

        private void RefreshMirrorSelector()
        {
            MirrorCombo.Items.Clear();
            var mirrors = _market.AvailableMirrors;
            if (mirrors == null || mirrors.Count == 0)
            {
                MirrorCombo.IsEnabled = false;
                MirrorCombo.Items.Add(PluginStrings.Market_NoMirrors);
                MirrorCombo.SelectedIndex = 0;
                return;
            }

            MirrorCombo.IsEnabled = true;
            MirrorCombo.Items.Add(PluginStrings.Market_MirrorAuto);
            int selected = 0;
            var activeMirror = _market.Sources.GetActiveSource()?.SelectedMirror ?? "";
            int i = 1;
            foreach (var kv in mirrors)
            {
                MirrorCombo.Items.Add(kv.Key);
                if (string.Equals(kv.Key, activeMirror, StringComparison.OrdinalIgnoreCase))
                    selected = i;
                i++;
            }
            MirrorCombo.SelectedIndex = selected;
        }

        private async void SourceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SourceCombo.SelectedIndex < 0) return;
            try
            {
                var sources = _market.Sources.Sources;
                // 索引 0 是官方源
                string id = SourceCombo.SelectedIndex == 0
                    ? PluginMarketSourcesService.OfficialSource.Id
                    : sources[SourceCombo.SelectedIndex - 1].Id;
                var current = _market.Sources.GetActiveSource();
                if (string.Equals(current.Id, id, StringComparison.OrdinalIgnoreCase)) return;

                LoadingBar.Visibility = Visibility.Visible;
                LoadingBar.IsIndeterminate = true;
                await _market.SwitchSourceAsync(id);
                LoadingBar.Visibility = Visibility.Collapsed;
                LoadingBar.IsIndeterminate = false;

                _allPlugins = _market.MergedPlugins ?? new List<MergedPluginInfo>();
                RefreshList();
                RefreshMirrorSelector();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Switch source error: {ex.Message}");
            }
        }

        private async void MirrorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MirrorCombo.SelectedIndex < 0 || !MirrorCombo.IsEnabled) return;
            try
            {
                string key = null;
                if (MirrorCombo.SelectedIndex > 0)
                {
                    key = MirrorCombo.SelectedItem as string;
                }
                await _market.SelectMirrorAsync(key ?? "");
                _allPlugins = _market.MergedPlugins ?? new List<MergedPluginInfo>();
                RefreshList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Mirror select error: {ex.Message}");
            }
        }

        private async void ManageSourcesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var content = new PluginMarketSourcesWindow(_market.Sources);
                var dialog = new iNKORE.UI.WPF.Modern.Controls.ContentDialog
                {
                    Title = PluginStrings.Market_ManageSources,
                    Content = content,
                    CloseButtonText = Properties.NotificationStrings.AnimationOff,
                    Owner = Window.GetWindow(this) ?? Application.Current?.MainWindow,
                    DefaultButton = iNKORE.UI.WPF.Modern.Controls.ContentDialogButton.Close,
                    Resources =
                    {
                        ["ContentDialogMaxWidth"] = 860d,
                        ["ContentDialogMaxHeight"] = 620d
                    }
                };
                await dialog.ShowAsync();

                if (content.HasChanges)
                {
                    InitSourceMirrorSelectors();
                    RefreshMirrorSelector();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ManageSourcesButton_Click: {ex.Message}");
            }
        }

        #endregion

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
                // 只显示市场插件
                if (!p.IsOnMarket) return false;
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
                PluginListBox.Items.Add(CreatePluginListItem(p));
            }

            // 如果当前选中的插件不在列表中，清空详情
            if (_selectedPlugin != null && !filtered.Contains(_selectedPlugin))
            {
                ShowEmptyState();
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
                Text = p.Name, FontSize = 14, FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0)
            };
            var versionText = new TextBlock
            {
                Text = p.VersionText, FontSize = 11, VerticalAlignment = VerticalAlignment.Center
            };
            versionText.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorTertiaryBrush");
            titleRow.Children.Add(nameText);
            titleRow.Children.Add(versionText);

            var descText = new TextBlock
            {
                Text = p.Description, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis
            };
            descText.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");

            infoPanel.Children.Add(titleRow);
            infoPanel.Children.Add(descText);
            Grid.SetColumn(infoPanel, 1);
            grid.Children.Add(infoPanel);

            // 右侧状态
            var actionPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center
            };

            if (p.IsOnMarket && !p.IsLocal && !p.RestartRequired)
            {
                var btn = new Button { Padding = new Thickness(4), Tag = p.Id, ToolTip = PluginStrings.Market_Install };
                btn.Click += InstallButton_Click;
                btn.Content = new iNKORE.UI.WPF.Modern.Controls.FontIcon
                {
                    Icon = iNKORE.UI.WPF.Modern.Common.IconKeys.SegoeFluentIcons.Download, FontSize = 14
                };
                actionPanel.Children.Add(btn);
            }
            if (p.IsUpdateAvailable)
            {
                var btn = new Button { Padding = new Thickness(4), Tag = p.Id, ToolTip = PluginStrings.Market_Update, Margin = new Thickness(4, 0, 0, 0) };
                btn.Click += InstallButton_Click;
                btn.Content = new iNKORE.UI.WPF.Modern.Controls.FontIcon
                {
                    Icon = iNKORE.UI.WPF.Modern.Common.IconKeys.SegoeFluentIcons.Upload, FontSize = 14
                };
                actionPanel.Children.Add(btn);
            }
            if (p.IsLocal)
            {
                var checkIcon = new iNKORE.UI.WPF.Modern.Controls.FontIcon
                {
                    Icon = iNKORE.UI.WPF.Modern.Common.IconKeys.SegoeFluentIcons.Completed,
                    FontSize = 14, Margin = new Thickness(4, 0, 0, 0),
                    Foreground = new SolidColorBrush((Color)Application.Current.FindResource("SystemAccentColor"))
                };
                actionPanel.Children.Add(checkIcon);
            }
            if (!string.IsNullOrEmpty(p.DownloadCountText))
            {
                var countText = new TextBlock
                {
                    Text = p.DownloadCountText, FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0)
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

        #region 详情面板

        private void ShowEmptyState()
        {
            _selectedPlugin = null;
            EmptyStatePanel.Visibility = Visibility.Visible;
            DetailPanel.Visibility = Visibility.Collapsed;
        }

        private void ShowDetail(MergedPluginInfo p)
        {
            _selectedPlugin = p;
            EmptyStatePanel.Visibility = Visibility.Collapsed;
            DetailPanel.Visibility = Visibility.Visible;

            DetailName.Text = p.Name;
            DetailVersion.Text = $"v{p.Version}";
            DetailAuthor.Text = $"by {p.Author}";
            DetailDescription.Text = p.Description;
            DetailDownloadCount.Text = p.DownloadCount > 0 ? p.DownloadCount.ToString("N0") : "-";
            DetailStarsCount.Text = p.StarsCount > 0 ? p.StarsCount.ToString("N0") : "-";

            var projectUrl = p.MarketEntry?.Manifest?.Url;
            if (string.IsNullOrWhiteSpace(projectUrl))
                projectUrl = p.LocalInfo?.Manifest?.Url;
            DetailUrl.NavigateUri = TryGetWebUri(projectUrl, out var homepage) ? homepage : null;
            DetailUrl.Visibility = DetailUrl.NavigateUri != null ? Visibility.Visible : Visibility.Collapsed;

            // 按钮状态
            var canInstall = p.IsOnMarket && !p.IsLocal && !p.RestartRequired;
            var canUpdate = p.IsUpdateAvailable && !p.RestartRequired;
            DetailInstallBtn.Visibility = canInstall ? Visibility.Visible : Visibility.Collapsed;
            DetailUpdateBtn.Visibility = canUpdate ? Visibility.Visible : Visibility.Collapsed;
            DetailRestartBtn.Visibility = p.RestartRequired ? Visibility.Visible : Visibility.Collapsed;
            DetailProgress.Value = p.DownloadTask?.Progress ?? 0;
            DetailProgress.Visibility = p.IsDownloading ? Visibility.Visible : Visibility.Collapsed;

            // 依赖
            var deps = p.MarketEntry?.Manifest?.Dependencies;
            if (deps != null && deps.Count > 0)
            {
                DetailDepsHeader.Visibility = Visibility.Visible;
                DetailDependencies.ItemsSource = deps;
            }
            else
            {
                DetailDepsHeader.Visibility = Visibility.Collapsed;
                DetailDependencies.ItemsSource = null;
            }

            // 说明文档
            DetailReadmeFallback.Visibility = Visibility.Collapsed;
            DetailReadmeViewer.Document = null;
            if (!string.IsNullOrEmpty(p.ReadmeUrl))
            {
                DetailReadmeViewer.Document = new System.Windows.Documents.FlowDocument
                {
                    Background = System.Windows.Media.Brushes.Transparent
                };
                DetailReadmeViewer.Document.Blocks.Add(new System.Windows.Documents.Paragraph(
                    new System.Windows.Documents.Run(PluginStrings.Market_ReadmeLoading)));
                _ = LoadReadmeAsync(p.ReadmeUrl);
            }
            else
            {
                DetailReadmeFallback.Visibility = Visibility.Visible;
            }
        }

        private async System.Threading.Tasks.Task LoadReadmeAsync(string url)
        {
            try
            {
                string text;
                using (var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(10) })
                {
                    text = await http.GetStringAsync(url);
                }

                Dispatcher.Invoke(() =>
                {
                    var renderer = new Ink_Canvas.Plugins.PluginReadmeRenderer();
                    var doc = renderer.Render(text);
                    DetailReadmeViewer.Document = doc;
                });
            }
            catch
            {
                Dispatcher.Invoke(() =>
                {
                    DetailReadmeFallback.Text = PluginStrings.Market_ReadmeLoadFailed;
                    DetailReadmeFallback.Visibility = Visibility.Visible;
                    DetailReadmeViewer.Document = null;
                });
            }
        }

        #endregion

        #region 事件处理

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchText = (sender as TextBox)?.Text ?? "";
            RefreshList();
        }

        private void SearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter) RefreshList();
        }

        private void PluginListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = PluginListBox.SelectedItem as ListBoxItem;
            var plugin = selectedItem?.Tag as MergedPluginInfo;
            if (plugin != null)
                ShowDetail(plugin);
            else
                ShowEmptyState();
        }

        private static bool TryGetWebUri(string value, out Uri uri)
        {
            if (Uri.TryCreate(value, UriKind.Absolute, out uri) &&
                (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
            {
                return true;
            }

            uri = null;
            return false;
        }

        private async void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as FrameworkElement;
            var id = btn?.Tag as string;
            if (string.IsNullOrEmpty(id)) return;
            await InstallPluginAsync(id);
        }

        private async void DetailInstall_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPlugin == null) return;
            await InstallPluginAsync(_selectedPlugin.Id);
        }

        private void DetailRestart_Click(object sender, RoutedEventArgs e)
        {
            AskRestart();
        }

        private async System.Threading.Tasks.Task InstallPluginAsync(string id)
        {
            var merged = _market.MergedPlugins.FirstOrDefault(p => p.Id == id);
            if (merged == null) return;

            var deps = _market.ResolveDependencies(id);
            if (deps.Count > 0)
            {
                var msg = string.Format(PluginStrings.Market_DependencyWarning, string.Join(", ", deps));
                var result = iNKORE.UI.WPF.Modern.Controls.MessageBox.Show(msg, PluginStrings.Market_DependencyTitle, MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes) return;
                foreach (var dep in deps)
                    await _market.RequestDownloadPluginAsync(dep);
            }

            // 安全检查：在写入下载前评估。对于已经位于市场索引（理论上已被评估过）的条目仍然把它跑一次以防镜像替换。
            try
            {
                var verdict = PluginManager.Instance.EvaluateTrust(null, merged.MarketEntry?.DownloadSha256, id);
                if (verdict.TrustLevel == PluginTrustLevel.Unknown && verdict.Reasons.Count > 0)
                {
                    var confirmMsg = PluginStrings.Market_SecurityWarning + Environment.NewLine + string.Join(Environment.NewLine, verdict.Reasons);
                    var securityResult = iNKORE.UI.WPF.Modern.Controls.MessageBox.Show(confirmMsg,
                        PluginStrings.Market_SecurityTitle,
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                    if (securityResult != MessageBoxResult.Yes) return;
                }
            }
            catch
            {
                // 安全检查失败时继续（不应阻断正常流程）
            }

            await _market.RequestDownloadPluginAsync(id);
            _allPlugins = _market.MergedPlugins ?? new List<MergedPluginInfo>();
            RefreshList();

            var updated = _allPlugins.FirstOrDefault(p => p.Id == id);
            if (updated != null && updated.RestartRequired)
                AskRestart();
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
                PluginStrings.Market_RestartMessage, PluginStrings.Market_RestartTitle,
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    Process.Start(Process.GetCurrentProcess().MainModule.FileName);
                    Application.Current.Shutdown();
                }
                catch (Exception ex)
                {
                    iNKORE.UI.WPF.Modern.Controls.MessageBox.Show(
                        string.Format(PluginStrings.Market_RestartFailed, ex.Message),
                        PluginStrings.Market_RestartTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
