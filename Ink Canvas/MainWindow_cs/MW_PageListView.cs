using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace Ink_Canvas
{
    public partial class MainWindow : Ink_Canvas.Helpers.PerformanceTransparentWin
    {
        private class PageListViewItem : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;

            private bool _isDragging;
            private bool _isSelected;

            /// <summary>该项是否正被拖拽（用于缩略图列表的"抬起"样式）。</summary>
            public bool IsDragging
            {
                get => _isDragging;
                set
                {
                    if (_isDragging == value) return;
                    _isDragging = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDragging)));
                }
            }

            /// <summary>
            /// 该项是否为当前页。选中态由模板中的小蓝条呈现；
            /// 左侧列表的蓝条在条目右侧、右侧列表的蓝条在条目左侧（均朝向屏幕中央），
            /// 左右列表共用同一数据集合，标记天然同步。
            /// </summary>
            public bool IsSelected
            {
                get => _isSelected;
                set
                {
                    if (_isSelected == value) return;
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }

            public int Index { get; set; }
            public StrokeCollection Strokes { get; set; }

            // 视频展台特殊模式专用字段（非特殊模式时均为 null/false，DataTemplate 用这些字段决定显示 InkCanvas/Image/TextBlock）
            /// <summary>视频展台照片缩略图源；非 null 时 DataTemplate 显示 Image 元素而非 InkCanvas。</summary>
            public ImageSource BoothImage { get; set; }
            /// <summary>视频展台提示文字（如"再次点击返回直播画面"）；非空时 DataTemplate 显示 TextBlock 而非 InkCanvas。</summary>
            public string BoothText { get; set; }
            /// <summary>是否为视频展台特殊模式项（非普通白板页）。</summary>
            public bool IsBoothItem => BoothImage != null || !string.IsNullOrEmpty(BoothText);
            /// <summary>DataTemplate 中 InkCanvas 是否可见（普通白板页可见，视频展台项不可见）。</summary>
            public bool ShowInk => !IsBoothItem;
            /// <summary>DataTemplate 中 Image 是否可见（仅视频展台照片项可见）。</summary>
            public bool ShowImage => BoothImage != null;
            /// <summary>DataTemplate 中 TextBlock 是否可见（仅视频展台文字项可见）。</summary>
            public bool ShowText => !string.IsNullOrEmpty(BoothText);
            /// <summary>删除按钮是否可见。直播页（纯文字项）不显示删除，其余（照片项/普通白板页）显示。</summary>
            public bool ShowDeleteButton => !(BoothImage == null && !string.IsNullOrEmpty(BoothText));
        }

        ObservableCollection<PageListViewItem> blackBoardSidePageListViewObservableCollection = new ObservableCollection<PageListViewItem>();

        /// <summary>
        /// 刷新白板的缩略图页面列表，更新左右侧缩略页列表，使其与当前白板页及历史快照一致，并将左右列表的选中项同步到当前白板页。
        /// </summary>
        /// <remarks>
        /// 为每页生成或更新对应的 PageListViewItem（通过应用时间线历史并裁剪到画布边界），用当前画布的笔迹替换当前页的条目，并将两个侧边 ListView 的 SelectedIndex 设置为当前白板索引 - 1。
        /// </remarks>
        private void RefreshBlackBoardSidePageListView()
        {
            var leftPageListView = FindView("board.pageList.left") as ListView;
            var rightPageListView = FindView("board.pageList.right") as ListView;

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

            if (leftPageListView != null) leftPageListView.SelectedIndex = CurrentWhiteboardIndex - 1;
            if (rightPageListView != null) rightPageListView.SelectedIndex = CurrentWhiteboardIndex - 1;
        }

        /// <summary>
        /// 视频展台特殊模式专用：刷新页码列表，按虚拟分页状态显示"直播页 + N 张照片"。
        /// 第 0 项=直播页（文字"再次点击，返回直播"），第 1..N 项=各照片缩略图。
        /// </summary>
        private void RefreshBoothPageListView()
        {
            var leftPageListView = FindView("board.pageList.left") as ListView;
            var rightPageListView = FindView("board.pageList.right") as ListView;

            blackBoardSidePageListViewObservableCollection.Clear();

            // 第 0 项：直播页，显示提示文字（点击会切换回直播画面）
            blackBoardSidePageListViewObservableCollection.Add(new PageListViewItem
            {
                Index = 0,
                Strokes = null,
                BoothText = "再次点击，返回直播",
            });

            // 第 1..N 项：各照片缩略图
            for (int i = 0; i < _capturedPhotos.Count; i++)
            {
                var img = _capturedPhotos[i]?.Image;
                if (img == null) continue;
                blackBoardSidePageListViewObservableCollection.Add(new PageListViewItem
                {
                    Index = i + 1,
                    Strokes = null,
                    BoothImage = img,
                });
            }

            // 同步左右两侧 SelectedIndex：直播页=0，照片页=index+1
            int selectedIndex = _boothCurrentPhotoIndex + 1;
            if (selectedIndex < 0) selectedIndex = 0;
            if (selectedIndex >= blackBoardSidePageListViewObservableCollection.Count)
                selectedIndex = blackBoardSidePageListViewObservableCollection.Count - 1;
            if (leftPageListView != null) leftPageListView.SelectedIndex = selectedIndex;
            if (rightPageListView != null) rightPageListView.SelectedIndex = selectedIndex;
        }

        /// <summary>
        /// 视频展台特殊模式：处理页码列表项点击。
        /// - index=0（直播页项）：切回直播页。
        /// - index>=1（照片项）：切到对应照片预览页。
        /// </summary>
        private void HandleBoothPageListClick(int index, ListView leftPageListView, ListView rightPageListView)
        {
            if (index < 0 || index >= blackBoardSidePageListViewObservableCollection.Count) return;

            if (index == 0)
            {
                // 点击直播页项 -> 切回直播页
                if (_boothCurrentPhotoIndex >= 0)
                {
                    SwitchBoothToLivePage();
                }
                else
                {
                    // 已在直播页，仅刷新页码与按钮状态
                    if (BtnCapturePhoto != null && _cameraService != null)
                        BtnCapturePhoto.IsEnabled = true;
                    UpdateBoothPageInfoDisplay();
                }
            }
            else
            {
                // 点击照片项 -> 切到对应照片预览页（index-1 = _capturedPhotos 索引）
                int photoIndex = index - 1;
                if (_boothCurrentPhotoIndex != photoIndex)
                {
                    SwitchBoothToPhotoPage(photoIndex);
                }
                else
                {
                    // 已在该照片页，仅刷新状态
                    if (BtnCapturePhoto != null)
                        BtnCapturePhoto.IsEnabled = false;
                    UpdateBoothPageInfoDisplay();
                }
            }

            // 同步左右两侧 SelectedIndex
            int selectedIndex = _boothCurrentPhotoIndex + 1;
            if (selectedIndex < 0) selectedIndex = 0;
            if (selectedIndex >= blackBoardSidePageListViewObservableCollection.Count)
                selectedIndex = blackBoardSidePageListViewObservableCollection.Count - 1;
            if (leftPageListView != null) leftPageListView.SelectedIndex = selectedIndex;
            if (rightPageListView != null) rightPageListView.SelectedIndex = selectedIndex;
        }

        /// <summary>
        /// 根据传入相对于 <paramref name="scrollViewer"/> 的点，查找并选中列表中对应的缩略图项；在需要时切换当前白板页并更新画布状态与左右侧缩略图选择状态。
        /// </summary>
        /// <param name="listView">承载页面缩略图的 ListView。</param>
        /// <param name="scrollViewer">包含该 ListView 的 ScrollViewer，用于将触点坐标从滚动视图坐标系转换到 ListView。</param>
        /// <param name="pointInScrollViewer">相对于 <paramref name="scrollViewer"/> 的触点坐标（用于命中测试）。</param>
        /// <remarks>
        /// - 如果命中到 ListViewItem，会隐藏左右侧页面边框、在必要时保存/清空/恢复画笔笔迹并更新 CurrentWhiteboardIndex 与显示信息；还会将左右两侧 ListView 的 SelectedIndex 同步为命中项索引。 
        /// - 在查找命中或切换过程中发生的异常将被捕获并忽略，不会向上抛出。
        /// - 视频展台特殊模式下：不走普通白板分页切换逻辑，转走 <see cref="HandleBoothPageListClick"/>。
        /// </remarks>
        private void TrySwitchWhiteboardPageByTouchPoint(ListView listView, ScrollViewer scrollViewer, Point pointInScrollViewer)
        {
            if (listView == null || scrollViewer == null) return;
            var leftBorder = FindView("board.pageList.leftBorder") as Border;
            var rightBorder = FindView("board.pageList.rightBorder") as Border;
            var leftPageListView = FindView("board.pageList.left") as ListView;
            var rightPageListView = FindView("board.pageList.right") as ListView;
            try
            {
                var transform = scrollViewer.TransformToVisual(listView);
                if (transform == null) return;
                var pointInListView = transform.Transform(pointInScrollViewer);
                var hit = VisualTreeHelper.HitTest(listView, pointInListView);
                if (hit?.VisualHit == null) return;
                var container = FindAncestorOfType<ListViewItem>(hit.VisualHit);
                if (container == null) return;
                int index = listView.ItemContainerGenerator.IndexFromContainer(container);
                if (index < 0 || index >= blackBoardSidePageListViewObservableCollection.Count) return;
                var item = blackBoardSidePageListViewObservableCollection[index];
                if (item == null) return;
                if (leftBorder != null) AnimationsHelper.HideWithSlideAndFade(leftBorder);
                if (rightBorder != null) AnimationsHelper.HideWithSlideAndFade(rightBorder);

                // 视频展台特殊模式：走虚拟分页点击切换，不走普通白板分页逻辑
                if (_isVideoPresenterSpecialMode)
                {
                    HandleBoothPageListClick(index, leftPageListView, rightPageListView);
                    return;
                }

                if (index + 1 != CurrentWhiteboardIndex)
                {
                    if (currentSelectedElement != null)
                    {
                        var previousEditingMode = inkCanvas.EditingMode;
                        UnselectElement(currentSelectedElement);
                        inkCanvas.EditingMode = previousEditingMode;
                        currentSelectedElement = null;
                    }
                    SaveStrokes();
                    ClearStrokes(true);
                    CurrentWhiteboardIndex = index + 1;
                    RestoreStrokes();
                    UpdateIndexInfoDisplay();
                }
                if (leftPageListView != null) leftPageListView.SelectedIndex = index;
                if (rightPageListView != null) rightPageListView.SelectedIndex = index;
            }
            catch
            {
                // 忽略命中测试或切换过程中的异常
            }
        }

        /// <summary>
        /// 在视觉树中自下而上查找并返回第一个匹配指定类型的祖先元素。
        /// </summary>
        /// <typeparam name="T">要查找的祖先类型，必须继承自 <see cref="DependencyObject"/>。</typeparam>
        /// <param name="current">起始节点；从此节点开始向上遍历视觉树。</param>
        /// <returns>找到的第一个类型为 <typeparamref name="T"/> 的祖先元素，未找到时返回 <c>null</c>。</returns>
        private static T FindAncestorOfType<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T found) return found;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        /// <summary>
        /// 将指定元素在给定 ScrollViewer 中滚动，使该元素与可视区域的顶部对齐。
        /// </summary>
        /// <param name="element">要对齐到顶部的元素。</param>
        /// <param name="scrollViewer">包含该元素的目标 ScrollViewer。</param>
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


        /// <summary>
        /// 左侧页面列表视图的鼠标释放事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 该方法会：
        /// 1. 隐藏左右侧页面边框
        /// 2. 获取选中的项目和索引
        /// 3. 只有当选择的页面与当前页面不同时才进行切换
        /// 4. 如果有选中的元素，先取消选择
        /// 5. 保存当前页面的笔画
        /// 6. 清空画布
        /// 7. 更新当前白板索引
        /// 8. 恢复新页面的笔画
        /// 9. 更新索引信息显示
        /// 10. 更新选择索引
        /// </remarks>
        private void BlackBoardLeftSidePageListView_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            var leftBorder = FindView("board.pageList.leftBorder") as Border;
            var rightBorder = FindView("board.pageList.rightBorder") as Border;
            var leftPageListView = FindView("board.pageList.left") as ListView;
            var rightPageListView = FindView("board.pageList.right") as ListView;
            if (leftPageListView == null) return;

            if (leftBorder != null) AnimationsHelper.HideWithSlideAndFade(leftBorder);
            if (rightBorder != null) AnimationsHelper.HideWithSlideAndFade(rightBorder);
            var item = leftPageListView.SelectedItem;
            var index = leftPageListView.SelectedIndex;
            if (item != null)
            {
                // 视频展台特殊模式：走虚拟分页点击切换，不走普通白板分页逻辑
                if (_isVideoPresenterSpecialMode)
                {
                    HandleBoothPageListClick(index, leftPageListView, rightPageListView);
                    return;
                }

                if (index + 1 != CurrentWhiteboardIndex)
                {
                    if (currentSelectedElement != null)
                    {
                        var previousEditingMode = inkCanvas.EditingMode;
                        UnselectElement(currentSelectedElement);
                        inkCanvas.EditingMode = previousEditingMode;
                        currentSelectedElement = null;
                    }

                    SaveStrokes();
                    ClearStrokes(true);
                    CurrentWhiteboardIndex = index + 1;
                    RestoreStrokes();
                    UpdateIndexInfoDisplay();
                }
                leftPageListView.SelectedIndex = index;
            }
        }

        /// <summary>
        /// 右侧页面列表视图的鼠标释放事件处理
        /// </summary>
        /// <param name="sender">发送者</param>
        /// <param name="e">鼠标按钮事件参数</param>
        /// <remarks>
        /// 该方法会：
        /// 1. 隐藏左右侧页面边框
        /// 2. 获取选中的项目和索引
        /// 3. 只有当选择的页面与当前页面不同时才进行切换
        /// 4. 如果有选中的元素，先取消选择
        /// 5. 保存当前页面的笔画
        /// 6. 清空画布
        /// 7. 更新当前白板索引
        /// 8. 恢复新页面的笔画
        /// 9. 更新索引信息显示
        /// 10. 更新选择索引
        /// </remarks>
        private void BlackBoardRightSidePageListView_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            var leftBorder = FindView("board.pageList.leftBorder") as Border;
            var rightBorder = FindView("board.pageList.rightBorder") as Border;
            var leftPageListView = FindView("board.pageList.left") as ListView;
            var rightPageListView = FindView("board.pageList.right") as ListView;
            if (rightPageListView == null) return;

            if (leftBorder != null) AnimationsHelper.HideWithSlideAndFade(leftBorder);
            if (rightBorder != null) AnimationsHelper.HideWithSlideAndFade(rightBorder);
            var item = rightPageListView.SelectedItem;
            var index = rightPageListView.SelectedIndex;
            if (item != null)
            {
                // 视频展台特殊模式：走虚拟分页点击切换，不走普通白板分页逻辑
                if (_isVideoPresenterSpecialMode)
                {
                    HandleBoothPageListClick(index, leftPageListView, rightPageListView);
                    return;
                }

                if (index + 1 != CurrentWhiteboardIndex)
                {
                    if (currentSelectedElement != null)
                    {
                        var previousEditingMode = inkCanvas.EditingMode;
                        UnselectElement(currentSelectedElement);
                        inkCanvas.EditingMode = previousEditingMode;
                        currentSelectedElement = null;
                    }

                    SaveStrokes();
                    ClearStrokes(true);
                    CurrentWhiteboardIndex = index + 1;
                    RestoreStrokes();
                    UpdateIndexInfoDisplay();
                }
                rightPageListView.SelectedIndex = index;
            }
        }

        /// <summary>
        /// 预览列表中某页的"删除"按钮点击：删除该页，并阻止事件继续冒泡（避免触发选中/切页）。
        /// 视频展台特殊模式下：删除对应照片（index>=1），而非白板页。
        /// </summary>
        private void WhiteBoardPageListItem_DeleteClick(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (!(sender is FrameworkElement fe && fe.DataContext is PageListViewItem item))
                return;

            if (_isVideoPresenterSpecialMode)
            {
                // 特殊模式：item.Index>=1 对应 _capturedPhotos[item.Index-1]
                int photoIndex = item.Index - 1;
                if (photoIndex < 0 || photoIndex >= _capturedPhotos.Count) return;

                // 若当前正在看被删照片，必须先切回直播页再 RemoveAt：
                // SwitchBoothToLivePage → SaveCurrentBoothStrokesToSlot 会把画布墨迹
                // 保存到 _capturedPhotos[photoIndex].Strokes（即将随照片 GC），
                // 并从 _liveStrokesSnapshot 恢复直播页墨迹。
                // 若先 RemoveAt 再切页，SaveCurrentBoothStrokesToSlot 会因列表已缩短
                // 走错分支：删最后一张时把被删照片墨迹覆盖到 _liveStrokesSnapshot（污染直播页），
                // 删中间张时覆盖到补位后的下一张照片 Strokes（破坏其他照片墨迹）。
                if (_boothCurrentPhotoIndex == photoIndex)
                {
                    SwitchBoothToLivePage();
                }

                _capturedPhotos.RemoveAt(photoIndex);

                // 当前在看被删照片之后的照片，索引前移（此时 _boothCurrentPhotoIndex 仍指向原照片）
                if (_boothCurrentPhotoIndex > photoIndex)
                {
                    _boothCurrentPhotoIndex--;
                }

                UpdateBoothPageInfoDisplay();
                RefreshBoothPageListView();
                return;
            }

            DeleteWhiteBoardPageByIndex(item.Index);
        }

        #region 页面缩略图拖拽排序

        /// <summary>拖拽排序是否已越过阈值真正开始。</summary>
        private bool _pageListDragActive;

        /// <summary>是否处于拖拽候选（已按下，等待越过阈值/长按确认）。</summary>
        private bool _pageListDragCandidate;

        private ListView _pageListDragListView;
        private ScrollViewer _pageListDragScrollViewer;

        /// <summary>拖拽起始项在集合中的索引（0-based，拖拽过程中保持不变）。</summary>
        private int _pageListDragFromIndex = -1;

        /// <summary>被拖项当前的集合索引（随实时移动更新）。</summary>
        private int _pageListDragCurrentIndex = -1;

        /// <summary>被拖项的实例（高亮状态跟随实例迁移，容器重建不丢失）。</summary>
        private PageListViewItem _pageListDragItem;

        /// <summary>被拖项的容器与其内容（拖拽期间跟随指针平移的是内容，容器布局保持不动）。</summary>
        private ListViewItem _pageListDragContainer;
        private UIElement _pageListDragContainerContent;
        private TranslateTransform _pageListDragTranslate;

        /// <summary>被拖项在列表坐标系中的布局中心 Y（拖拽期间集合不动，此值稳定）。</summary>
        private double _pageListDragContainerCenterY;

        /// <summary>插入位置指示线（仅活动列表显示）。</summary>
        private Border _pageListDragIndicator;

        private Point _pageListDragStartPosition;
        private long _pageListDragCandidateStartTicks;
        private bool _pageListDragRequiresHold;

        /// <summary>鼠标/笔按下后移动超过该距离即开始拖拽。</summary>
        private const double PageListDragThresholdPx = 8;

        /// <summary>触摸需按住该时长确认拖拽（避免与列表滚动冲突）。</summary>
        private const long PageListDragTouchHoldTicks = 450 * TimeSpan.TicksPerMillisecond;

        /// <summary>
        /// 在页面缩略图项上按下时记录拖拽候选。
        /// 命中删除按钮、视频展台虚拟分页或仅一页时不进入候选。
        /// </summary>
        private bool TryBeginPageListDragCandidate(ListView listView, Point positionInList, object originalSource, bool requireHold)
        {
            if (_pageListDragActive || _isVideoPresenterSpecialMode || WhiteboardTotalCount < 2)
                return false;

            // 删除按钮上的按下不进入拖拽，保留按钮点击语义
            if (FindAncestorOfType<Button>(originalSource as DependencyObject) != null)
                return false;

            var container = HitTestPageListViewItem(listView, positionInList);
            if (container == null) return false;
            var index = listView.ItemContainerGenerator.IndexFromContainer(container);
            if (index < 0) return false;
            if (!(listView.ItemContainerGenerator.ItemFromContainer(container) is PageListViewItem item)) return false;

            _pageListDragCandidate = true;
            _pageListDragListView = listView;
            _pageListDragScrollViewer = FindAncestorOfType<ScrollViewer>(listView);
            _pageListDragFromIndex = index;
            _pageListDragCurrentIndex = index;
            _pageListDragItem = item;
            _pageListDragStartPosition = positionInList;
            _pageListDragCandidateStartTicks = Environment.TickCount64;
            _pageListDragRequiresHold = requireHold;
            return true;
        }

        private static ListViewItem HitTestPageListViewItem(ListView listView, Point positionInList)
        {
            var hit = VisualTreeHelper.HitTest(listView, positionInList);
            if (hit?.VisualHit == null) return null;
            return FindAncestorOfType<ListViewItem>(hit.VisualHit);
        }

        /// <summary>
        /// 移动事件驱动：等待越过阈值/长按确认后进入拖拽；拖拽中被拖项跟随指针、
        /// 插入指示线标记落点（集合保持不动，松手时一次性移动）。
        /// 返回 true 表示本次事件已被拖拽流程消费（触摸调用方应跳过滚动逻辑）。
        /// </summary>
        private bool UpdatePageListDrag(ListView listView, Point positionInList)
        {
            if (!_pageListDragCandidate || _pageListDragListView != listView) return false;

            if (!_pageListDragActive)
            {
                var withinThreshold = Math.Abs(positionInList.Y - _pageListDragStartPosition.Y) <= PageListDragThresholdPx;

                if (_pageListDragRequiresHold)
                {
                    // 长按确认前移动过大：取消候选，交还滚动
                    if (!withinThreshold)
                    {
                        CancelPageListDragCandidate();
                        return false;
                    }
                    if (Environment.TickCount64 - _pageListDragCandidateStartTicks < PageListDragTouchHoldTicks)
                        return true;
                }
                else if (withinThreshold)
                {
                    return true;
                }

                _pageListDragActive = true;
                BeginPageListDragVisual(listView);
            }

            AutoScrollPageListDuringDrag(positionInList);
            UpdatePageListDragVisual(listView, positionInList);
            return true;
        }

        /// <summary>进入拖拽：点亮被拖项"抬起"样式并让它从此刻起跟随指针。</summary>
        private void BeginPageListDragVisual(ListView listView)
        {
            SetPageListDragItemHighlight(true);

            _pageListDragContainer = listView.ItemContainerGenerator.ContainerFromIndex(_pageListDragFromIndex) as ListViewItem;
            if (_pageListDragContainer != null)
            {
                _pageListDragContainerCenterY = _pageListDragContainer.TransformToVisual(listView)
                    .Transform(new Point(0, _pageListDragContainer.ActualHeight / 2.0)).Y;

                // 平移施加在容器的内容上（容器自身布局不动），滚动/坐标换算保持稳定
                _pageListDragContainerContent = VisualTreeHelper.GetChildrenCount(_pageListDragContainer) > 0
                    ? VisualTreeHelper.GetChild(_pageListDragContainer, 0) as UIElement
                    : null;
                if (_pageListDragContainerContent != null)
                {
                    _pageListDragTranslate = new TranslateTransform();
                    var group = new TransformGroup();
                    group.Children.Add(new ScaleTransform(1.03, 1.03));
                    group.Children.Add(_pageListDragTranslate);
                    _pageListDragContainerContent.RenderTransformOrigin = new Point(0.5, 0.5);
                    _pageListDragContainerContent.RenderTransform = group;
                }

                Panel.SetZIndex(_pageListDragContainer, 10);
            }

            // 指示线只在用户拖拽的这一侧列表显示
            _pageListDragIndicator = FindView("board.pageList.left") == listView
                ? FindView("board.pageList.left.dragIndicator") as Border
                : FindView("board.pageList.right.dragIndicator") as Border;
        }

        /// <summary>拖拽移动：被拖项跟随指针；指示线标记落点（k&lt;起点在其上方，k&gt;起点在其下方）。</summary>
        private void UpdatePageListDragVisual(ListView listView, Point positionInList)
        {
            if (_pageListDragTranslate != null)
                _pageListDragTranslate.Y = positionInList.Y - _pageListDragContainerCenterY;

            _pageListDragCurrentIndex = ComputePageListDragTargetIndex(listView, positionInList);

            if (_pageListDragIndicator == null) return;

            // 落点未变时隐藏指示线
            if (_pageListDragCurrentIndex == _pageListDragFromIndex)
            {
                _pageListDragIndicator.Visibility = Visibility.Collapsed;
                return;
            }

            var anchorContainer = listView.ItemContainerGenerator.ContainerFromIndex(_pageListDragCurrentIndex) as ListViewItem;
            if (anchorContainer == null)
            {
                _pageListDragIndicator.Visibility = Visibility.Collapsed;
                return;
            }

            // 向上拖：线画在目标项上边缘；向下拖：画在目标项下边缘（含"放最后一位"）
            var anchorPoint = new Point(0, _pageListDragCurrentIndex < _pageListDragFromIndex
                ? 0
                : anchorContainer.ActualHeight);
            var y = anchorContainer.TransformToVisual(_pageListDragIndicator.Parent as Visual)
                .Transform(anchorPoint).Y;

            _pageListDragIndicator.Margin = new Thickness(8, y - 1.5, 8, 0);
            _pageListDragIndicator.Visibility = Visibility.Visible;
        }

        /// <summary>拖拽接近列表上下边缘时自动滚动。</summary>
        private void AutoScrollPageListDuringDrag(Point positionInList)
        {
            var scrollViewer = _pageListDragScrollViewer;
            if (scrollViewer == null) return;

            var posInScroll = _pageListDragListView.TransformToVisual(scrollViewer).Transform(positionInList);
            const double edge = 24;
            if (posInScroll.Y < edge) scrollViewer.LineUp();
            else if (posInScroll.Y > scrollViewer.ActualHeight - edge) scrollViewer.LineDown();
        }

        /// <summary>
        /// 根据指针位置计算被拖项的插入位置（0-based）。
        /// 统计中点位于指针上方的其余项数量即为最终落点：被拖项自身被排除在计数外，
        /// 无需再作偏移修正（额外减一会导致任何页都无法落到最后一位）。
        /// </summary>
        private int ComputePageListDragTargetIndex(ListView listView, Point positionInList)
        {
            var count = blackBoardSidePageListViewObservableCollection.Count;
            var target = 0;
            for (var i = 0; i < count; i++)
            {
                if (i == _pageListDragCurrentIndex) continue;
                var container = listView.ItemContainerGenerator.ContainerFromIndex(i) as ListViewItem;
                if (container == null) continue;
                var midY = container.TransformToVisual(listView)
                    .Transform(new Point(0, container.ActualHeight / 2.0)).Y;
                if (positionInList.Y > midY) target++;
            }
            return Math.Max(0, Math.Min(count - 1, target));
        }

        /// <summary>
        /// 结束拖拽：按落点一次性移动集合，把第 fromPage 页的数据搬移到第 toPage 页，
        /// 页号重排并刷新列表。被拖页是当前页时先保存未提交墨迹，恢复后页号跟随；
        /// 否则仅搬移快照数组、画布不动。
        /// </summary>
        private void EndPageListDrag()
        {
            if (!_pageListDragActive)
            {
                CancelPageListDragCandidate();
                return;
            }

            _pageListDragActive = false;
            _pageListDragCandidate = false;

            var fromPage = _pageListDragFromIndex + 1;
            var toPage = _pageListDragCurrentIndex + 1;

            try
            {
                if (fromPage != toPage
                    && fromPage >= 1 && fromPage <= WhiteboardTotalCount
                    && toPage >= 1 && toPage <= WhiteboardTotalCount)
                {
                    // 集合按最终落点一次性移动（拖拽期间集合未动）
                    blackBoardSidePageListViewObservableCollection.Move(_pageListDragFromIndex, _pageListDragCurrentIndex);

                    if (CurrentWhiteboardIndex == fromPage)
                    {
                        // 当前页参与搬移：先落盘未保存的墨迹，页号跟随被拖页
                        VideoPresenter_BeforePageLeave();
                        PauseAllCanvasMediaPlayback();
                        SaveStrokes();
                        ClearStrokes(true);

                        MoveWhiteboardPageData(fromPage, toPage);

                        CurrentWhiteboardIndex = toPage;

                        RestoreStrokes();
                        VideoPresenter_OnPageChanged();
                    }
                    else
                    {
                        // 其他页搬移：当前画布不动，仅重排快照与页号
                        MoveWhiteboardPageData(fromPage, toPage);

                        if (fromPage < toPage && CurrentWhiteboardIndex > fromPage && CurrentWhiteboardIndex <= toPage)
                            CurrentWhiteboardIndex--;
                        else if (toPage < fromPage && CurrentWhiteboardIndex >= toPage && CurrentWhiteboardIndex < fromPage)
                            CurrentWhiteboardIndex++;
                    }

                    UpdateIndexInfoDisplay();
                    RefreshBlackBoardSidePageListView();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"页面拖拽排序失败: {ex.Message}", LogHelper.LogType.Error);
            }
            finally
            {
                CancelPageListDragCandidate();
            }
        }

        /// <summary>取消拖拽候选并清空状态（含被拖项视觉与插入指示线的还原）。</summary>
        private void CancelPageListDragCandidate()
        {
            SetPageListDragItemHighlight(false);

            if (_pageListDragContainerContent != null)
            {
                _pageListDragContainerContent.RenderTransform = null;
                _pageListDragContainerContent.RenderTransformOrigin = new Point(0, 0);
            }
            if (_pageListDragContainer != null)
                _pageListDragContainer.ClearValue(Panel.ZIndexProperty);
            if (_pageListDragIndicator != null)
                _pageListDragIndicator.Visibility = Visibility.Collapsed;

            _pageListDragCandidate = false;
            _pageListDragActive = false;
            _pageListDragListView = null;
            _pageListDragScrollViewer = null;
            _pageListDragFromIndex = -1;
            _pageListDragCurrentIndex = -1;
            _pageListDragItem = null;
            _pageListDragContainer = null;
            _pageListDragContainerContent = null;
            _pageListDragTranslate = null;
            _pageListDragIndicator = null;
        }

        /// <summary>开启/关闭被拖项的"抬起"视觉（半透明+阴影）。</summary>
        private void SetPageListDragItemHighlight(bool isDragging)
        {
            if (_pageListDragItem != null)
                _pageListDragItem.IsDragging = isDragging;
        }

        /// <summary>
        /// 将第 fromPage 页的快照数据移动到第 toPage 页（1-based），区间内其余页顺移。
        /// 搬移字段与新增/删除页保持一致：历史、多指状态、冻结状态、最后变更时间。
        /// </summary>
        private void MoveWhiteboardPageData(int fromPage, int toPage)
        {
            if (fromPage == toPage) return;

            var history = TimeMachineHistories[fromPage];
            var multiState = savedMultiTouchModeStates[fromPage];
            var frozen = frozenPages[fromPage];
            var lastUtc = pageLastUserInkMutationUtc[fromPage];

            if (fromPage < toPage)
            {
                for (var i = fromPage; i < toPage; i++)
                {
                    TimeMachineHistories[i] = TimeMachineHistories[i + 1];
                    savedMultiTouchModeStates[i] = savedMultiTouchModeStates[i + 1];
                    frozenPages[i] = frozenPages[i + 1];
                    pageLastUserInkMutationUtc[i] = pageLastUserInkMutationUtc[i + 1];
                }
            }
            else
            {
                for (var i = fromPage; i > toPage; i--)
                {
                    TimeMachineHistories[i] = TimeMachineHistories[i - 1];
                    savedMultiTouchModeStates[i] = savedMultiTouchModeStates[i - 1];
                    frozenPages[i] = frozenPages[i - 1];
                    pageLastUserInkMutationUtc[i] = pageLastUserInkMutationUtc[i - 1];
                }
            }

            TimeMachineHistories[toPage] = history;
            savedMultiTouchModeStates[toPage] = multiState;
            frozenPages[toPage] = frozen;
            pageLastUserInkMutationUtc[toPage] = lastUtc;
        }

        #region 鼠标/笔拖拽入口

        /// <summary>
        /// 列表选中变化时同步各数据项的 IsSelected 标记。
        /// 选中样式由数据驱动（卡片描边），左右两个列表共享同一集合，天然对称。
        /// </summary>
        private void PageList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(sender is ListView listView)) return;
            for (var i = 0; i < blackBoardSidePageListViewObservableCollection.Count; i++)
            {
                blackBoardSidePageListViewObservableCollection[i].IsSelected = i == listView.SelectedIndex;
            }
        }

        private void PageList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is ListView listView)) return;
            // 仅记录候选，不在此捕获鼠标——否则普通点击的 MouseUp 会改道到列表，
            // 缩略图切页将失效；捕获推迟到真正越过阈值开始拖拽时
            TryBeginPageListDragCandidate(listView, e.GetPosition(listView), e.OriginalSource, requireHold: false);
        }

        private void PageList_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!(sender is ListView listView)) return;
            if (!_pageListDragCandidate || _pageListDragListView != listView) return;

            var wasActive = _pageListDragActive;
            UpdatePageListDrag(listView, e.GetPosition(listView));
            // 刚进入拖拽时捕获鼠标，保证拖动期间（含越出列表边缘）事件连续
            if (!wasActive && _pageListDragActive)
                listView.CaptureMouse();
            e.Handled = true;
        }

        private void PageList_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!(_pageListDragListView == sender)) return;
            var wasDragging = _pageListDragActive;
            EndPageListDrag();
            // 拖拽结束时吞掉本次点击，避免触发缩略图切页
            if (wasDragging) e.Handled = true;
        }

        private void PageList_LostMouseCapture(object sender, MouseEventArgs e)
        {
            // 捕获意外丢失（如窗口失活）时收尾，避免集合已移动而页数据未同步
            if (_pageListDragListView == sender && _pageListDragActive)
                EndPageListDrag();
        }

        #endregion

        #endregion
    }
}