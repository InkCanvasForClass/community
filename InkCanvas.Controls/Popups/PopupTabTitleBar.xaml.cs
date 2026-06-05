using iNKORE.UI.WPF.Controls;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Ink_Canvas.Controls
{
    public class PopupTabItem
    {
        public string Header { get; set; }
        public string IconSource { get; set; }
    }

    public partial class PopupTabTitleBar : UserControl
    {
        private static readonly SolidColorBrush SelectedBackground =
            new SolidColorBrush(Color.FromArgb(40, 59, 130, 246));

        private static readonly SolidColorBrush UnselectedBackground =
            new SolidColorBrush(Colors.Transparent);

        public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(
            nameof(SelectedIndex), typeof(int), typeof(PopupTabTitleBar),
            new PropertyMetadata(0, OnSelectedIndexChanged));

        private static void OnSelectedIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (PopupTabTitleBar)d;
            var newIndex = (int)e.NewValue;
            if (newIndex < 0 || newIndex >= control.Tabs.Count)
                return;
            control.UpdateTabVisuals();
            control.SelectedIndexChanged?.Invoke(control, newIndex);
        }

        public int SelectedIndex
        {
            get => (int)GetValue(SelectedIndexProperty);
            set => SetValue(SelectedIndexProperty, value);
        }

        public ObservableCollection<PopupTabItem> Tabs { get; }

        public Button CloseButtonControl => CloseButton;

        public event EventHandler<int> SelectedIndexChanged;

        public PopupTabTitleBar()
        {
            InitializeComponent();
            Tabs = new ObservableCollection<PopupTabItem>();
            Tabs.CollectionChanged += Tabs_CollectionChanged;
        }

        private void Tabs_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RebuildTabs();
        }

        private void RebuildTabs()
        {
            TabsPanel.Children.Clear();
            for (int i = 0; i < Tabs.Count; i++)
            {
                var tabItem = Tabs[i];
                var tabBorder = CreateTabElement(tabItem, i);
                TabsPanel.Children.Add(tabBorder);
            }
            UpdateTabVisuals();
        }

        private Border CreateTabElement(PopupTabItem tabItem, int index)
        {
            var border = new Border
            {
                Height = 28,
                CornerRadius = new CornerRadius(4),
                Background = UnselectedBackground,
                Tag = index,
                Cursor = Cursors.Hand
            };

            border.MouseUp += (s, e) =>
            {
                if (SelectedIndex != index)
                {
                    SelectedIndex = index;
                }
                e.Handled = true;
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var contentPanel = new SimpleStackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (!string.IsNullOrEmpty(tabItem.IconSource))
            {
                var icon = new Image
                {
                    Source = new BitmapImage(new Uri(tabItem.IconSource, UriKind.RelativeOrAbsolute)),
                    Height = 16,
                    Width = 16
                };
                RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.HighQuality);
                contentPanel.Children.Add(icon);
            }

            var text = new TextBlock
            {
                FontWeight = FontWeights.Medium,
                FontSize = 14,
                TextAlignment = TextAlignment.Center,
                Text = tabItem.Header ?? "",
                Margin = new Thickness(4, 0, 4, 0)
            };
            text.SetResourceReference(TextBlock.ForegroundProperty, "FloatBarForeground");
            contentPanel.Children.Add(text);

            Grid.SetRow(contentPanel, 0);
            grid.Children.Add(contentPanel);

            var indicator = new Border
            {
                Height = 3,
                CornerRadius = new CornerRadius(1.5),
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = new SolidColorBrush(Color.FromRgb(0x3b, 0x82, 0xf6)),
                Visibility = Visibility.Collapsed
            };

            Grid.SetRow(indicator, 1);
            grid.Children.Add(indicator);

            border.Child = grid;
            border.Padding = new Thickness(8, 0, 8, 0);

            return border;
        }

        private void UpdateTabVisuals()
        {
            if (SelectedIndex < 0 || SelectedIndex >= TabsPanel.Children.Count)
                return;
            for (int i = 0; i < TabsPanel.Children.Count; i++)
            {
                if (!(TabsPanel.Children[i] is Border border)) continue;
                if (!(border.Child is Grid grid)) continue;

                bool isSelected = (i == SelectedIndex);

                border.Background = isSelected ? SelectedBackground : UnselectedBackground;

                if (grid.Children.Count >= 2)
                {
                    if (grid.Children[1] is Border indicator)
                    {
                        indicator.Visibility = isSelected ? Visibility.Visible : Visibility.Collapsed;
                    }

                    if (grid.Children[0] is SimpleStackPanel contentPanel)
                    {
                        foreach (var child in contentPanel.Children)
                        {
                            if (child is TextBlock textBlock)
                            {
                                textBlock.FontWeight = isSelected ? FontWeights.Bold : FontWeights.Medium;
                            }
                        }
                    }
                }
            }
        }
    }
}
