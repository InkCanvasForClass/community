using Ink_Canvas.Helpers;
using System;
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

        /// <summary>
        /// <para> 查找可视树祖先，用于在点击事件中获取 ListViewItem </para>
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

        // 触摸与鼠标双触发抑制支持
        private readonly HashSet<int> _leftActiveTouchIds = new HashSet<int>();
        private DateTime _lastLeftTouchHandled = DateTime.MinValue;

        private readonly HashSet<int> _rightActiveTouchIds = new HashSet<int>();
        private DateTime _lastRightTouchHandled = DateTime.MinValue;


        /// <summary>
        /// <para> 记录触摸起点的字典，用于判断是点击还是滑动 </para>
        private Dictionary<int, Point> _leftTouchStartPoints = new Dictionary<int, Point>();
        private Dictionary<int, Point> _rightTouchStartPoints = new Dictionary<int, Point>();
        
        /// <summary>
        /// <para> 触摸防抖阈值（像素），移动距离小于此值视为点击 </para>
        private const double TouchClickThreshold = 20.0; 

        /// </summary>


        /// <summary>
        /// <para> 处理页面选择跳转逻辑 </para>
        /// </summary>
        private void ProcessPageSelection(ListView listView, int index)
        {
            // 隐藏页面列表
            AnimationsHelper.HideWithSlideAndFade(BoardBorderLeftPageListView);
            AnimationsHelper.HideWithSlideAndFade(BoardBorderRightPageListView);

            if (index < 0) return;

            // 只有当选择的页面与当前页面不同时才进行切换
            if (index + 1 != CurrentWhiteboardIndex)
            {
                // 隐藏图片选择工具栏并退出图片/元素编辑模式
                if (currentSelectedElement != null)
                {
                    // 保存当前编辑模式
                    var previousEditingMode = inkCanvas.EditingMode;
                    UnselectElement(currentSelectedElement);
                    // 恢复编辑模式
                    inkCanvas.EditingMode = previousEditingMode;
                    currentSelectedElement = null;
                }

                // 保存当前页面墨迹
                SaveStrokes();
                // 清空画布
                ClearStrokes(true);
                // 切换索引
                CurrentWhiteboardIndex = index + 1;
                // 恢复新页面的墨迹
                RestoreStrokes();
                // 更新底部页码显示
                UpdateIndexInfoDisplay();
            }

            // 无论是否切换页面，都更新选择索引以保持UI同步
            listView.SelectedIndex = index;
        }

        /// <summary>
        /// <para> 左侧页面列表鼠标松开事件 </para>
        /// </summary>
        private void BlackBoardLeftSidePageListView_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            // 忽略紧随触摸后的鼠标事件以防双触发
            if ((DateTime.UtcNow - _lastLeftTouchHandled) < TimeSpan.FromMilliseconds(500)) return;

            // 尝试根据点击的原始来源查找对应的 ListViewItem 容器（支持点击模板内的缩略图）
            var container = FindVisualAncestor<ListViewItem>(e.OriginalSource as DependencyObject);
            int index = -1;
            if (container != null)
            {
                index = BlackBoardLeftSidePageListView.ItemContainerGenerator.IndexFromContainer(container);
            }

            // 回退到 SelectedIndex（如果无法通过视觉树找到容器）
            if (index < 0) index = BlackBoardLeftSidePageListView.SelectedIndex;

            ProcessPageSelection(BlackBoardLeftSidePageListView, index);
        }

        /// <summary>
        /// <para> 左侧页面列表触摸按下事件 </para>
        /// </summary>
        private void BlackBoardLeftSidePageListView_OnTouchDown(object sender, TouchEventArgs e)
        {
            _leftActiveTouchIds.Add(e.TouchDevice.Id);
            
            // 记录触摸起始点
            var touchPoint = e.GetTouchPoint(BlackBoardLeftSidePageListView);
            if (!_leftTouchStartPoints.ContainsKey(e.TouchDevice.Id))
            {
                _leftTouchStartPoints.Add(e.TouchDevice.Id, touchPoint.Position);
            }
            else
            {
                _leftTouchStartPoints[e.TouchDevice.Id] = touchPoint.Position;
            }
        }

        /// <summary>
        /// <para> 左侧页面列表触摸松开事件 </para>
        /// </summary>
        private void BlackBoardLeftSidePageListView_OnTouchUp(object sender, TouchEventArgs e)
        {
            var id = e.TouchDevice.Id;
            // 仅在整个触摸过程中只存在单个触摸点时触发切换（避免多指滚动触发点击）
            if (_leftActiveTouchIds.Count == 1)
            {
                // 计算移动距离
                bool isClick = true;
                if (_leftTouchStartPoints.ContainsKey(id))
                {
                    Point startPoint = _leftTouchStartPoints[id];
                    Point endPoint = e.GetTouchPoint(BlackBoardLeftSidePageListView).Position;
                    double distance = Math.Sqrt(Math.Pow(endPoint.X - startPoint.X, 2) + Math.Pow(endPoint.Y - startPoint.Y, 2));
                    
                    // 如果移动距离超过阈值，视为滑动操作，不触发点击
                    if (distance > TouchClickThreshold)
                    {
                        isClick = false;
                    }
                    _leftTouchStartPoints.Remove(id);
                }

                if (isClick)
                {
                    var container = FindVisualAncestor<ListViewItem>(e.OriginalSource as DependencyObject);
                    int index = -1;
                    if (container != null)
                    {
                        index = BlackBoardLeftSidePageListView.ItemContainerGenerator.IndexFromContainer(container);
                    }

                    if (index < 0) index = BlackBoardLeftSidePageListView.SelectedIndex;

                    ProcessPageSelection(BlackBoardLeftSidePageListView, index);

                    // 标记触摸已处理以抑制随后鼠标事件
                    _lastLeftTouchHandled = DateTime.UtcNow;
                    e.Handled = true;
                }
            }

            _leftActiveTouchIds.Remove(id);
            if (_leftTouchStartPoints.ContainsKey(id)) _leftTouchStartPoints.Remove(id);
        }


        /// <summary>
        /// <para> 右侧页面列表鼠标松开事件 </para>
        /// </summary>
        private void BlackBoardRightSidePageListView_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            // 忽略紧随触摸后的鼠标事件以防双触发
            if ((DateTime.UtcNow - _lastRightTouchHandled) < TimeSpan.FromMilliseconds(500)) return;

            // 尝试根据点击的原始来源查找对应的 ListViewItem 容器（支持点击模板内的缩略图）
            var container = FindVisualAncestor<ListViewItem>(e.OriginalSource as DependencyObject);
            int index = -1;
            if (container != null)
            {
                index = BlackBoardRightSidePageListView.ItemContainerGenerator.IndexFromContainer(container);
            }

            // 回退到 SelectedIndex（如果无法通过视觉树找到容器）
            if (index < 0) index = BlackBoardRightSidePageListView.SelectedIndex;

            ProcessPageSelection(BlackBoardRightSidePageListView, index);
        }

        /// <summary>
        /// <para> 右侧页面列表触摸按下事件 </para>
        /// </summary>
        private void BlackBoardRightSidePageListView_OnTouchDown(object sender, TouchEventArgs e)
        {
            _rightActiveTouchIds.Add(e.TouchDevice.Id);
        }

        /// <summary>
        /// <para> 右侧页面列表触摸松开事件 </para>
        /// </summary>
        private void BlackBoardRightSidePageListView_OnTouchUp(object sender, TouchEventArgs e)
        {
            var id = e.TouchDevice.Id;
            if (_rightActiveTouchIds.Count == 1)
            {
                // 计算移动距离
                bool isClick = true;
                if (_rightTouchStartPoints.ContainsKey(id))
                {
                    Point startPoint = _rightTouchStartPoints[id];
                    Point endPoint = e.GetTouchPoint(BlackBoardRightSidePageListView).Position;
                    double distance = Math.Sqrt(Math.Pow(endPoint.X - startPoint.X, 2) + Math.Pow(endPoint.Y - startPoint.Y, 2));

                    // 如果移动距离超过阈值，视为滑动操作，不触发点击
                    if (distance > TouchClickThreshold)
                    {
                        isClick = false;
                    }
                    _rightTouchStartPoints.Remove(id);
                }

                if (isClick)
                {
                    var container = FindVisualAncestor<ListViewItem>(e.OriginalSource as DependencyObject);
                    int index = -1;
                    if (container != null)
                    {
                        index = BlackBoardRightSidePageListView.ItemContainerGenerator.IndexFromContainer(container);
                    }

                    if (index < 0) index = BlackBoardRightSidePageListView.SelectedIndex;

                    ProcessPageSelection(BlackBoardRightSidePageListView, index);

                    _lastRightTouchHandled = DateTime.UtcNow;
                    e.Handled = true;
                }
            }

            _rightActiveTouchIds.Remove(id);
            if (_rightTouchStartPoints.ContainsKey(id)) _rightTouchStartPoints.Remove(id);
        }

    }
}