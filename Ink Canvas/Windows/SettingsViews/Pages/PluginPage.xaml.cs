using Ink_Canvas.Plugins;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class PluginPage : iNKORE.UI.WPF.Modern.Controls.Page
    {
        public PluginPage()
        {
            InitializeComponent();
            Loaded += PluginPage_Loaded;
        }

        private void PluginPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadPlugins();
        }

        public void LoadPlugins()
        {
            try
            {
                var pluginManager = PluginManager.Instance;
                var plugins = pluginManager.Plugins;

                PluginCountText.Text = string.Format("已加载 {0} 个插件", plugins.Count);

                if (plugins.Count == 0)
                {
                    PluginContainer.Children.Clear();
                    var noPluginText = new TextBlock
                    {
                        Text = "没有找到插件，请将插件文件放置在 Plugins 目录中",
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = Brushes.Gray,
                        Margin = new Thickness(0, 10, 0, 0)
                    };
                    PluginContainer.Children.Add(noPluginText);
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
                PluginCountText.Text = string.Format("加载插件时出错：{0}", ex.Message);
            }
        }

        private Border CreatePluginCard(PluginInfo pluginInfo)
        {
            var card = new Border
            {
                Background = Brushes.White,
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(15)
            };

            var stackPanel = new StackPanel();

            var titlePanel = new DockPanel { LastChildFill = true };

            var nameText = new TextBlock
            {
                Text = pluginInfo.Name,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Black,
                Margin = new Thickness(0, 0, 10, 0)
            };
            DockPanel.SetDock(nameText, Dock.Left);

            var versionText = new TextBlock
            {
                Text = string.Format("v{0}", pluginInfo.Version),
                FontSize = 12,
                Foreground = Brushes.Gray,
                VerticalAlignment = VerticalAlignment.Center
            };

            titlePanel.Children.Add(nameText);
            titlePanel.Children.Add(versionText);

            var descriptionText = new TextBlock
            {
                Text = pluginInfo.Description,
                FontSize = 12,
                Foreground = Brushes.DarkGray,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 5, 0, 5)
            };

            var authorText = new TextBlock
            {
                Text = string.Format("作者：{0}", pluginInfo.Author),
                FontSize = 11,
                Foreground = Brushes.Gray
            };

            stackPanel.Children.Add(titlePanel);
            stackPanel.Children.Add(descriptionText);
            stackPanel.Children.Add(authorText);

            card.Child = stackPanel;
            return card;
        }
    }
}
