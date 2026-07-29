using Ink_Canvas.Helpers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private class PageListViewItem
        {
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
    }
}