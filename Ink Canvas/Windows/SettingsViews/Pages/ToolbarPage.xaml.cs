using GongSolutions.Wpf.DragDrop;
using Ink_Canvas.Controls.Toolbar;
using Ink_Canvas.Helpers;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace Ink_Canvas.Windows.SettingsViews.Pages
{
    public partial class ToolbarPage : Page, IDropTarget
    {
        public class ToolbarItemViewModel : INotifyPropertyChanged
        {
            public string Id { get; }
            public string DisplayName { get; }

            private int _order;
            public int Order { get => _order; set { _order = value; OnPropertyChanged(nameof(Order)); } }

            private bool _isVisible = true;
            public bool IsVisible { get => _isVisible; set { _isVisible = value; OnPropertyChanged(nameof(IsVisible)); } }

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

            public ToolbarItemViewModel(string id, string displayName, int order, bool isVisible)
            {
                Id = id; DisplayName = displayName; _order = order; _isVisible = isVisible;
            }
        }

        private static readonly string LogTag = "ToolbarPage";
        private bool _isLoaded;

        public ObservableCollection<ToolbarItemViewModel> MainItems { get; } = new();
        public ObservableCollection<ToolbarItemViewModel> CanvasItems { get; } = new();
        public ObservableCollection<ToolbarItemViewModel> EndItems { get; } = new();

        public ToolbarPage()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += OnPageLoaded;
        }

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            try { LoadSettings(); }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"{LogTag}: LoadSettings 异常: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}", LogHelper.LogType.Error);
            }
            _isLoaded = true;
        }

        private void LoadSettings()
        {
            LogHelper.WriteLogToFile($"{LogTag}: LoadSettings 开始", LogHelper.LogType.Info);
            MainItems.Clear(); CanvasItems.Clear(); EndItems.Clear();

            var layout = SettingsManager.Settings?.Toolbar ?? new ToolbarLayoutSettings();
            IReadOnlyList<IToolbarItem> discoveredItems;
            try { discoveredItems = ToolbarRegistry.Discover(); }
            catch (Exception ex) { LogHelper.WriteLogToFile($"{LogTag}: Discover 失败: {ex.Message}", LogHelper.LogType.Error); return; }

            foreach (var item in discoveredItems)
            {
                try
                {
                    if (!layout.Items.TryGetValue(item.Id, out var cfg))
                    {
                        cfg = new ToolbarItemConfig
                        {
                            Visible = item.DefaultVisible,
                            Order = item.DefaultOrder,
                            Slot = item.DefaultSlot,
                            Position = item.DefaultPosition,
                            AnchorName = item.DefaultAnchorName
                        };
                    }
                    string displayName;
                    try { displayName = item.DisplayName ?? item.Id; }
                    catch { displayName = item.Id; }

                    var vm = new ToolbarItemViewModel(item.Id, displayName, cfg.Order, cfg.Visible);
                    switch (cfg.Slot)
                    {
                        case ToolbarSlot.FloatingBarMain: MainItems.Add(vm); break;
                        case ToolbarSlot.FloatingBarCanvasControls: CanvasItems.Add(vm); break;
                        case ToolbarSlot.FloatingBarEnd: EndItems.Add(vm); break;
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"{LogTag}: 处理条目失败 [{item?.Id}]: {ex.Message}", LogHelper.LogType.Warning);
                }
            }

            ReorderCollections();
            LogHelper.WriteLogToFile($"{LogTag}: LoadSettings 完成 Main={MainItems.Count} Canvas={CanvasItems.Count} End={EndItems.Count}", LogHelper.LogType.Info);
        }

        private void ReorderCollections()
        {
            SortCollection(MainItems);
            SortCollection(CanvasItems);
            SortCollection(EndItems);
        }

        private static void SortCollection(ObservableCollection<ToolbarItemViewModel> collection)
        {
            if (collection == null) return;
            var sorted = collection.OrderBy(x => x.Order).ToList();
            for (int i = 0; i < sorted.Count; i++)
            {
                var oldIndex = collection.IndexOf(sorted[i]);
                if (oldIndex != -1 && oldIndex != i)
                    collection.Move(oldIndex, i);
            }
        }

        public new void DragOver(IDropInfo dropInfo)
        {
            if (dropInfo.Data is not ToolbarItemViewModel) return;
            dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
            dropInfo.Effects = DragDropEffects.Move;
        }

        public new void Drop(IDropInfo dropInfo)
        {
            if (dropInfo.Data is not ToolbarItemViewModel vm) return;
            if (dropInfo.TargetCollection is not ObservableCollection<ToolbarItemViewModel> target) return;

            var oldIndex = target.IndexOf(vm);
            var newIndex = oldIndex < dropInfo.UnfilteredInsertIndex ? dropInfo.UnfilteredInsertIndex - 1 : dropInfo.UnfilteredInsertIndex;
            var finalIndex = Math.Min(newIndex >= target.Count ? target.Count - 1 : newIndex, target.Count);

            if (!target.Contains(vm))
            {
                if (dropInfo.DragInfo.SourceCollection is ObservableCollection<ToolbarItemViewModel> source)
                    source.Remove(vm);
                target.Insert(dropInfo.UnfilteredInsertIndex, vm);
            }
            else if (oldIndex != -1 && oldIndex != finalIndex)
            {
                target.Move(oldIndex, finalIndex);
            }

            UpdateOrdersFromCollection(target);
            SaveSettings();
        }

        private static void UpdateOrdersFromCollection(ObservableCollection<ToolbarItemViewModel> collection)
        {
            for (int i = 0; i < collection.Count; i++)
                collection[i].Order = (i + 1) * 10;
        }

        private void SaveSettings()
        {
            if (!_isLoaded) return;
            try
            {
                var settings = SettingsManager.Settings;
                if (settings == null) return;
                if (settings.Toolbar == null) settings.Toolbar = new ToolbarLayoutSettings();
                var layout = settings.Toolbar;

                foreach (var vm in MainItems.Concat(CanvasItems).Concat(EndItems))
                {
                    if (!layout.Items.TryGetValue(vm.Id, out var cfg))
                    {
                        var item = ToolbarRegistry.Discover().FirstOrDefault(i => i.Id == vm.Id);
                        cfg = new ToolbarItemConfig
                        {
                            Visible = item?.DefaultVisible ?? true,
                            Order = item?.DefaultOrder ?? 0,
                            Slot = item?.DefaultSlot ?? ToolbarSlot.FloatingBarMain,
                            Position = item?.DefaultPosition ?? ToolbarInsertPosition.Prepend,
                            AnchorName = item?.DefaultAnchorName
                        };
                        layout.Items[vm.Id] = cfg;
                    }
                    cfg.Visible = vm.IsVisible;
                    cfg.Order = vm.Order;
                }

                SettingsManager.SaveSettingsToFile();
                LogHelper.WriteLogToFile($"{LogTag}: 设置已保存", LogHelper.LogType.Info);

                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                        mainWindow?.RebuildToolbar();
                    }
                    catch (Exception ex) { LogHelper.WriteLogToFile($"{LogTag}: RebuildToolbar 异常: {ex.Message}", LogHelper.LogType.Error); }
                }));
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"{LogTag}: SaveSettings 异常: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}", LogHelper.LogType.Error);
            }
        }

        private void ButtonReset_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SettingsManager.Settings?.Toolbar?.Items.Clear();
                SettingsManager.SaveSettingsToFile();
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try { Application.Current.Windows.OfType<MainWindow>().FirstOrDefault()?.RebuildToolbar(); }
                    catch (Exception ex) { LogHelper.WriteLogToFile($"{LogTag}: Reset Rebuild 异常: {ex.Message}", LogHelper.LogType.Error); }
                }));
                LoadSettings();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"{LogTag}: ButtonReset 异常: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}", LogHelper.LogType.Error);
            }
        }
    }
}
