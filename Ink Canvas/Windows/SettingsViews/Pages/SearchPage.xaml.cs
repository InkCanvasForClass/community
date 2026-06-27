using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using iNKORE.UI.WPF.Modern.Controls;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class SearchPage
    {
        /// <summary>
        /// 选中搜索结果时触发，参数为 PageTag；"__back__" 表示返回
        /// </summary>
        public static event EventHandler<string> ResultSelected;

        // 由 SettingsWindow 设置的静态搜索数据
        internal static List<(string Text, string PageTag)> SearchData { get; set; }

        public SearchPage()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                Dispatcher.BeginInvoke(new Action(() => SearchBox.Focus()),
                    System.Windows.Threading.DispatcherPriority.Input);
            };
        }

        private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (SearchData == null) return;

            string raw = (args.ChosenSuggestion as string) ?? args.QueryText;
            if (string.IsNullOrWhiteSpace(raw)) return;

            string query = raw.Trim();
            var match = SearchData.Where(e => e.Text.Equals(query, StringComparison.OrdinalIgnoreCase))
                     .Concat(SearchData.Where(e => e.Text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0))
                     .FirstOrDefault();

            if (match.Text != null)
            {
                ResultSelected?.Invoke(this, match.PageTag);
            }
        }

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
            if (SearchData == null) return;

            string query = sender.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(query))
            {
                sender.ItemsSource = null;
                HintText.Visibility = Visibility.Visible;
                return;
            }

            HintText.Visibility = Visibility.Collapsed;

            var suggestions = SearchData
                .Where(e => e.Text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(e => e.Text)
                .Distinct()
                .Take(50)
                .ToList();

            sender.ItemsSource = suggestions;
            sender.IsSuggestionListOpen = suggestions.Count > 0;
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(SearchBox.Text))
            {
                SearchBox.IsSuggestionListOpen = true;
            }
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                ResultSelected?.Invoke(this, "__back__");
            }
        }
    }
}
