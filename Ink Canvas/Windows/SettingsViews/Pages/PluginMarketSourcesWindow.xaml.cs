using Ink_Canvas.Helpers;
using Ink_Canvas.Plugins;
using Ink_Canvas.Properties;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    /// <summary>
    /// 插件源管理内容。由插件市场页面置于 ContentDialog 中显示。
    /// </summary>
    public partial class PluginMarketSourcesWindow : UserControl
    {
        private readonly PluginMarketSourcesService _service;
        private PluginMarketSourceInfo _current;
        private bool _isNew;

        /// <summary>
        /// 指示本次对话中是否已保存或删除插件源，供调用方刷新源选择器。
        /// </summary>
        public bool HasChanges { get; private set; }

        public PluginMarketSourcesWindow(PluginMarketSourcesService service)
        {
            InitializeComponent();
            _service = service ?? throw new ArgumentNullException(nameof(service));
            ReloadList();
            SourcesList.SelectedIndex = 0;
        }

        private void ReloadList()
        {
            SourcesList.Items.Clear();
            SourcesList.Items.Add(PluginMarketSourcesService.OfficialSource);
            foreach (var source in _service.Sources)
            {
                SourcesList.Items.Add(source);
            }
        }

        private void ShowSource(PluginMarketSourceInfo source, bool readOnly)
        {
            _current = source;
            _isNew = false;
            IdBox.Text = readOnly ? source.Display : source.Id ?? string.Empty;
            UrlBox.Text = source.Url ?? string.Empty;

            // 保持控件可用以避免禁用态文本过浅；只读控件仍可复制 URL 供用户查看。
            IdBox.IsReadOnly = readOnly || !_isNew;
            UrlBox.IsReadOnly = readOnly;
            RemoveBtn.IsEnabled = !readOnly;
            SaveBtn.IsEnabled = !readOnly;
            HintText.Text = readOnly ? PluginStrings.Market_SourceOfficial : string.Empty;
        }

        private void SourcesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SourcesList.SelectedItem is not PluginMarketSourceInfo source)
            {
                return;
            }

            bool isOfficial = string.Equals(
                source.Id,
                PluginMarketSourcesService.OfficialSource.Id,
                StringComparison.OrdinalIgnoreCase);
            ShowSource(source, isOfficial);
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            _isNew = true;
            _current = null;
            SourcesList.SelectedItem = null;
            IdBox.Text = string.Empty;
            UrlBox.Text = string.Empty;
            IdBox.IsReadOnly = false;
            UrlBox.IsReadOnly = false;
            RemoveBtn.IsEnabled = false;
            SaveBtn.IsEnabled = true;
            HintText.Text = string.Empty;
            IdBox.Focus();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string id = (IdBox.Text ?? string.Empty).Trim();
            string url = (UrlBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                HintText.Text = PluginStrings.Market_SourceNameRequired;
                IdBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(url)
                || (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                    && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            {
                HintText.Text = PluginStrings.Market_SourceInvalidUrl;
                UrlBox.Focus();
                return;
            }

            var source = new PluginMarketSourceInfo
            {
                Id = id,
                Url = url,
                SelectedMirror = _isNew ? string.Empty : _current?.SelectedMirror ?? string.Empty
            };
            bool succeeded;
            string error;
            if (_isNew)
            {
                succeeded = _service.TryAdd(source, out error);
            }
            else
            {
                succeeded = _service.Update(source, out error);
            }

            if (!succeeded)
            {
                HintText.Text = error ?? PluginStrings.Market_SourceDuplicate;
                return;
            }

            HasChanges = true;
            HintText.Text = PluginStrings.Market_SourceSaved;
            ReloadList();
            SourcesList.SelectedItem = SourcesList.Items
                .OfType<PluginMarketSourceInfo>()
                .FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_current == null
                || string.Equals(_current.Id, PluginMarketSourcesService.OfficialSource.Id, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var result = MessageBoxHelper.Show(this,
                string.Format(PluginStrings.Market_RemoveSourceConfirmation, _current.Id),
                PluginStrings.Market_RemoveSource,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            if (!_service.Remove(_current.Id))
            {
                return;
            }

            HasChanges = true;
            ReloadList();
            SourcesList.SelectedIndex = 0;
            HintText.Text = PluginStrings.Market_SourceRemoved;
        }
    }
}
