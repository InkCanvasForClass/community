using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using iNKORE.UI.WPF.Modern.Controls;
using iNKORE.UI.WPF.Controls;

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
            new SolidColorBrush(Color.FromArgb(72, 219, 234, 254));

        private static readonly SolidColorBrush UnselectedBackground =
            new SolidColorBrush(Colors.Transparent);

        public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(
            nameof(SelectedIndex), typeof(int), typeof(PopupTabTitleBar),
            new PropertyMetadata(0, OnSelectedIndexChanged));

        private static void OnSelectedIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (PopupTabTitleBar)d;
            control.UpdateTabVisuals();
            control.SelectedIndexChanged?.Invoke(control, (int)e.NewValue);
        }

        public int SelectedIndex
        {
            get => (int)GetValue(SelectedIndexProperty);
            set => SetValue(SelectedIndexProperty, value);
        }

        public ObservableCollection<PopupTabItem> Tabs { get; }

        public FontIcon CloseFontIcon => CloseIcon;

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
                Height = 20,
                CornerRadius = new CornerRadius(3),
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
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0)
            };

            if (!string.IsNullOrEmpty(tabItem.IconSource))
            {
                var icon = new Image
                {
                    Source = new BitmapImage(new Uri(tabItem.IconSource, UriKind.RelativeOrAbsolute)),
                    Height = 13,
                    Width = 13
                };
                RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.HighQuality);
                contentPanel.Children.Add(icon);
            }

            var text = new TextBlock
            {
                Foreground = Brushes.White,
                FontWeight = FontWeights.Medium,
                FontSize = 9,
                TextAlignment = TextAlignment.Center,
                Text = tabItem.Header ?? "",
                Margin = new Thickness(2, 1, 0, 0)
            };
            contentPanel.Children.Add(text);

            Grid.SetRow(contentPanel, 0);
            grid.Children.Add(contentPanel);

            var indicator = new Border
            {
                Height = 2,
                CornerRadius = new CornerRadius(1),
                HorizontalAlignment = HorizontalAlignment.Center,
                Visibility = Visibility.Collapsed
            };

            var indicatorBrush = TryFindResource("FloatBarBackground") as Brush;
            indicator.Background = indicatorBrush ?? Brushes.White;

            Grid.SetRow(indicator, 1);
            grid.Children.Add(indicator);

            border.Child = grid;
            border.Padding = new Thickness(6, 0, 6, 0);

            return border;
        }

        private void UpdateTabVisuals()
        {
            for (int i = 0; i < TabsPanel.Children.Count; i++)
            {
                if (!(TabsPanel.Children[i] is Border border)) continue;
                if (!(border.Child is Grid grid)) continue;

                bool isSelected = (i == SelectedIndex);

                border.Background = isSelected ? SelectedBackground : UnselectedBackground;
                border.Opacity = isSelected ? 1 : 0.9;

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
                                textBlock.FontSize = isSelected ? 9.5 : 9;
                                textBlock.Margin = isSelected
                                    ? new Thickness(2, 0.5, 0, 0)
                                    : new Thickness(2, 1, 0, 0);
                            }
                        }
                    }
                }
            }
        }
    }
}
