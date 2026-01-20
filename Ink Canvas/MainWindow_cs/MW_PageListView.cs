using Ink_Canvas.Helpers;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas
{
    public partial class MainWindow : Window
    {
        private class PageListViewItem
        {
            public int Index { get; set; }
            public StrokeCollection Strokes { get; set; }
        }

        ObservableCollection<PageListViewItem> blackBoardSidePageListViewObservableCollection = new ObservableCollection<PageListViewItem>();

        /// <summary>
        /// <para>刷新白板的缩略图页面列表。</para>
        /// </summary>
        private void RefreshBlackBoardSidePageListView()
        {
            if (blackBoardSidePageListViewObservableCollection.Count == WhiteboardTotalCount)
            {
                foreach (int index in Enumerable.Range(1, WhiteboardTotalCount))
                {
                    var st = ApplyHistoriesToNewStrokeCollection(TimeMachineHistories[index]);
                    st.Clip(new Rect(0, 0, (int)inkCanvas.ActualWidth, (int)inkCanvas.ActualHeight));
                    var pitem = new PageListViewItem
                    {
                        Index = index,
                        Strokes = st,
                    };
                    blackBoardSidePageListViewObservableCollection[index - 1] = pitem;
                }
            }
            else
            {
                blackBoardSidePageListViewObservableCollection.Clear();
                foreach (int index in Enumerable.Range(1, WhiteboardTotalCount))
                {
                    var st = ApplyHistoriesToNewStrokeCollection(TimeMachineHistories[index]);
                    st.Clip(new Rect(0, 0, (int)inkCanvas.ActualWidth, (int)inkCanvas.ActualHeight));
                    var pitem = new PageListViewItem
                    {
                        Index = index,
                        Strokes = st,
                    };
                    blackBoardSidePageListViewObservableCollection.Add(pitem);
                }
            }

            var _st = inkCanvas.Strokes.Clone();
            _st.Clip(new Rect(0, 0, (int)inkCanvas.ActualWidth, (int)inkCanvas.ActualHeight));
            var _pitem = new PageListViewItem
            {
                Index = CurrentWhiteboardIndex,
                Strokes = _st,
            };
            blackBoardSidePageListViewObservableCollection[CurrentWhiteboardIndex - 1] = _pitem;

            BlackBoardLeftSidePageListView.SelectedIndex = CurrentWhiteboardIndex - 1;
            BlackBoardRightSidePageListView.SelectedIndex = CurrentWhiteboardIndex - 1;
        }

        public static void ScrollViewToVerticalTop(FrameworkElement element, ScrollViewer scrollViewer)
        {
            if (element == null || scrollViewer == null)
            {
                return;
            }

            var scrollViewerOffset = scrollViewer.VerticalOffset;
            var point = new Point(0, scrollViewerOffset);
            var transform = element.TransformToVisual(scrollViewer);
            if (transform == null)
            {
                return;
            }

            var tarPos = transform.Transform(point);
            scrollViewer.ScrollToVerticalOffset(tarPos.Y);
        }

        private T FindVisualAncestor<T>(DependencyObject d) where T : DependencyObject
        {
            var current = d;
            while (current != null)
            {
                if (current is T) return (T)current;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }


        private void BlackBoardLeftSidePageListView_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            AnimationsHelper.HideWithSlideAndFade(BoardBorderLeftPageListView);
            AnimationsHelper.HideWithSlideAndFade(BoardBorderRightPageListView);

            // 尝试根据点击的原始来源查找对应的 ListViewItem 容器（支持点击模板内的缩略图）
            var container = FindVisualAncestor<ListViewItem>(e.OriginalSource as DependencyObject);
            int index = -1;
            if (container != null)
            {
                index = BlackBoardLeftSidePageListView.ItemContainerGenerator.IndexFromContainer(container);
            }

            // 回退到 SelectedIndex（如果无法通过视觉树找到容器）
            if (index < 0) index = BlackBoardLeftSidePageListView.SelectedIndex;

            var item = index >= 0 ? BlackBoardLeftSidePageListView.Items[index] : BlackBoardLeftSidePageListView.SelectedItem;

            if (index >= 0)
            {
                // 只有当选择的页面与当前页面不同时才进行切换
                if (index + 1 != CurrentWhiteboardIndex)
                {
                    // 隐藏图片选择工具栏
                    if (currentSelectedElement != null)
                    {
                        // 保存当前编辑模式
                        var previousEditingMode = inkCanvas.EditingMode;
                        UnselectElement(currentSelectedElement);
                        // 恢复编辑模式
                        inkCanvas.EditingMode = previousEditingMode;
                        currentSelectedElement = null;
                    }

                    SaveStrokes();
                    ClearStrokes(true);
                    CurrentWhiteboardIndex = index + 1;
                    RestoreStrokes();
                    UpdateIndexInfoDisplay();
                }
                // 无论是否切换页面，都更新选择索引
                BlackBoardLeftSidePageListView.SelectedIndex = index;
            }
        }

        private void BlackBoardRightSidePageListView_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            AnimationsHelper.HideWithSlideAndFade(BoardBorderLeftPageListView);
            AnimationsHelper.HideWithSlideAndFade(BoardBorderRightPageListView);

            // 尝试根据点击的原始来源查找对应的 ListViewItem 容器（支持点击模板内的缩略图）
            var container = FindVisualAncestor<ListViewItem>(e.OriginalSource as DependencyObject);
            int index = -1;
            if (container != null)
            {
                index = BlackBoardRightSidePageListView.ItemContainerGenerator.IndexFromContainer(container);
            }

            // 回退到 SelectedIndex（如果无法通过视觉树找到容器）
            if (index < 0) index = BlackBoardRightSidePageListView.SelectedIndex;

            var item = index >= 0 ? BlackBoardRightSidePageListView.Items[index] : BlackBoardRightSidePageListView.SelectedItem;

            if (index >= 0)
            {
                // 只有当选择的页面与当前页面不同时才进行切换
                if (index + 1 != CurrentWhiteboardIndex)
                {
                    // 隐藏图片选择工具栏
                    if (currentSelectedElement != null)
                    {
                        // 保存当前编辑模式
                        var previousEditingMode = inkCanvas.EditingMode;
                        UnselectElement(currentSelectedElement);
                        // 恢复编辑模式
                        inkCanvas.EditingMode = previousEditingMode;
                        currentSelectedElement = null;
                    }

                    SaveStrokes();
                    ClearStrokes(true);
                    CurrentWhiteboardIndex = index + 1;
                    RestoreStrokes();
                    UpdateIndexInfoDisplay();
                }
                // 无论是否切换页面，都更新选择索引
                BlackBoardRightSidePageListView.SelectedIndex = index;
            }
        }

    }
}
