using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using InkCanvasForClass.IccInkCanvas.Settings;
using InkCanvasForClass.IccInkCanvas.Utils.Threading;
using JetBrains.Annotations;
using MatrixTransform = System.Windows.Media.MatrixTransform;

namespace InkCanvasForClass.IccInkCanvas {

    public class IccInnerInkCanvasInfo {
        public Guid GUID { get; set; }
        public Dispatcher Dispatcher { get; set; }
        public DispatcherContainer Container { get; set; }
    }

    public class TimeMachineStatusUpdatedEventArgs : EventArgs {
        public TimeMachineStatusUpdatedEventArgs(IccBoardPage page) {
            Page = page;
            CanUndo = page.TimeMachine.CanUndo;
            CanRedo = page.TimeMachine.CanRedo;
            HistoriesCount = page.TimeMachine.CurrentHistoriesCount;
        }

        public IccBoardPage Page { get; private set; }
        public bool CanUndo { get; private set; }
        public bool CanRedo { get; private set; }
        public int HistoriesCount { get; private set; }
    }

    /// <summary>
    /// 基于InkCanvas封装的墨迹书写控件
    /// </summary>
    public partial class IccBoard : UserControl {

        public BoardSettings BoardSettings { get; private set; } = new BoardSettings();

        private List<IccInnerInkCanvasInfo> DispatcherInkCanvasList { get; set; } =
            new List<IccInnerInkCanvasInfo>();

        private List<IccBoardPage> BoardPages { get; set; } = new List<IccBoardPage>();

        private int CurrentPageIndex;

        #region Properties

        private bool _isEditingModePropertyAccessdByCodeBehind = false;
        public EditingMode EditingMode {
            get => (EditingMode)GetValue(EditingModeProperty);
            set {
                if (value == EditingMode.ShapeDrawing) throw new Exception("EditingMode.ShapeDrawing 不能被用户手动设定");
                _isEditingModePropertyAccessdByCodeBehind = true;
                SetValue(EditingModeProperty, value);
                _isEditingModePropertyAccessdByCodeBehind = false;
                UpdateEditingMode();
            }
        }
        public static readonly System.Windows.DependencyProperty EditingModeProperty =
            System.Windows.DependencyProperty.Register(
                nameof(EditingMode),
                typeof(EditingMode),
                typeof(IccBoard),
                new FrameworkPropertyMetadata(EditingMode.Writing,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    propertyChangedCallback: OnEditingModePropertyChanged));
        private static void OnEditingModePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var iccboard = d as IccBoard;
            if (iccboard != null && iccboard._isEditingModePropertyAccessdByCodeBehind) return;
            iccboard?.UpdateEditingMode();
        }

        #endregion

        #region EditingMode

        private EditingMode _edittingMode;

        private void UpdateEditingMode() {
            _edittingMode = EditingMode;
            if (EditingMode == EditingMode.None || EditingMode == EditingMode.NoneWithHitTest) 
                WrapperInkCanvas.EditingMode = InkCanvasEditingMode.None;
            IsHitTestVisible = EditingMode != EditingMode.None;
            if (EditingMode == EditingMode.Writing) WrapperInkCanvas.EditingMode = InkCanvasEditingMode.Ink;
            EraserCanvas.Visibility = EditingMode == EditingMode.GeometryErasing ? Visibility.Visible : Visibility.Collapsed;
            RectangleAreaEraserCanvas.Visibility =
                EditingMode == EditingMode.AreaErasing ? Visibility.Visible : Visibility.Collapsed;
            RoamingHitTestBorder.Visibility = EditingMode == EditingMode.RoamingMode ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion

        #region Events

        private static readonly System.Windows.RoutedEvent EditingModeChangedEvent = EventManager.RegisterRoutedEvent(
            name: "EditingModeChanged",
            routingStrategy: RoutingStrategy.Bubble,
            handlerType: typeof(System.Windows.RoutedEventHandler),
            ownerType: typeof(IccBoard));
        private static readonly System.Windows.RoutedEvent ActiveEditingModeChangedEvent = EventManager.RegisterRoutedEvent(
            name: "ActiveEditingModeChanged",
            routingStrategy: RoutingStrategy.Bubble,
            handlerType: typeof(System.Windows.RoutedEventHandler),
            ownerType: typeof(IccBoard));
        private static readonly System.Windows.RoutedEvent CurrentPageChangedEvent = EventManager.RegisterRoutedEvent(
            name: "CurrentPageChanged",
            routingStrategy: RoutingStrategy.Bubble,
            handlerType: typeof(System.Windows.RoutedEventHandler),
            ownerType: typeof(IccBoard));
        private static readonly System.Windows.RoutedEvent UndoRedoStateChangedEvent = EventManager.RegisterRoutedEvent(
            name: "UndoRedoStateChanged",
            routingStrategy: RoutingStrategy.Bubble,
            handlerType: typeof(System.Windows.RoutedEventHandler),
            ownerType: typeof(IccBoard));

        public event System.Windows.RoutedEventHandler EditingModeChanged {
            add => AddHandler(EditingModeChangedEvent, value);
            remove => RemoveHandler(EditingModeChangedEvent, value);
        }
        public event System.Windows.RoutedEventHandler ActiveEditingModeChanged {
            add => AddHandler(ActiveEditingModeChangedEvent, value);
            remove => RemoveHandler(ActiveEditingModeChangedEvent, value);
        }
        public event System.Windows.RoutedEventHandler CurrentPageChanged {
            add => AddHandler(CurrentPageChangedEvent, value);
            remove => RemoveHandler(CurrentPageChangedEvent, value);
        }
        /// <summary>
        /// 该事件接收撤销和重做状态变更的事件（注意，当前页面变更也会触发该事件。该事件接收每一页撤销和重做状态变更的事件，但是不提供具体是哪一页的时光机发生了状态变更，可以用这个事件来更新当前页面的时光机UI状态）
        /// </summary>
        public event System.Windows.RoutedEventHandler UndoRedoStateChanged {
            add => AddHandler(UndoRedoStateChangedEvent, value);
            remove => RemoveHandler(UndoRedoStateChangedEvent, value);
        }

        private bool isIgnoreRaiseCurrentPageChangedEvent = false;

        private void RaiseEditingModeChangedEvent() {
            RoutedEventArgs routedEventArgs = new RoutedEventArgs(routedEvent: EditingModeChangedEvent);
            RaiseEvent(routedEventArgs);
        }
        private void RaiseActiveEditingModeChangedEvent() {
            RoutedEventArgs routedEventArgs = new RoutedEventArgs(routedEvent: ActiveEditingModeChangedEvent);
            RaiseEvent(routedEventArgs);
        }
        private void RaiseCurrentPageChangedEvent() {
            if (isIgnoreRaiseCurrentPageChangedEvent) return;
            RoutedEventArgs routedEventArgs = new RoutedEventArgs(routedEvent: CurrentPageChangedEvent);
            RaiseEvent(routedEventArgs);
            RaiseUndoRedoStateChangedEvent(null);
        }
        private void RaiseUndoRedoStateChangedEvent(TimeMachine t) {
            RoutedEventArgs routedEventArgs = new RoutedEventArgs(routedEvent: UndoRedoStateChangedEvent);
            RaiseEvent(routedEventArgs);
            if (t != null) TimeMachineStatusUpdated?.Invoke(this, new TimeMachineStatusUpdatedEventArgs(BoardPages.Find(page=>page.TimeMachine.Equals(t))));
        }

        /// <summary>
        /// 该事件用于接收时光机状态变更的事件（接收所有页面的时光机状态变化）
        /// </summary>
        public event EventHandler<TimeMachineStatusUpdatedEventArgs> TimeMachineStatusUpdated;

        #endregion

        #region BoardSettings

        private void RegisterEventsForBoardSettings() {
            BoardSettings.NibWidthChanged += (s,e)=>WrapperInkCanvas.DefaultDrawingAttributes.Width = BoardSettings.NibWidth;
            BoardSettings.NibHeightChanged += (s,e)=>WrapperInkCanvas.DefaultDrawingAttributes.Height = BoardSettings.NibHeight;
            BoardSettings.NibColorChanged += (s,e)=>WrapperInkCanvas.DefaultDrawingAttributes.Color = BoardSettings.NibColor;
        }

        #endregion

        #region InkCanvas Manager

        /// <summary>
        /// 记录所有已注册了Strokes_OnStrokesChanged事件的InkCanvas的GUID
        /// </summary>
        private Dictionary<Guid, StrokeCollectionChangedEventHandler>
            _innerInkCanvas_Strokes_OnStrokesChanged_wrappers = new Dictionary<Guid, StrokeCollectionChangedEventHandler>();

        /// <summary>
        /// 记录所有已登记要触发Stroke_OnDrawingAttributesChanged事件的InkCanvas的GUID
        /// （为什么不是已注册？因为这个事件是针对一条Stroke而言的，这里只是记录这个InkCanvas将会调用这个事件）
        /// </summary>
        private Dictionary<Guid, PropertyDataChangedEventHandler>
            _innerInkCanvas_Stroke_OnDrawingAttributesChanged_wrappers = new Dictionary<Guid, PropertyDataChangedEventHandler>();

        /// <summary>
        /// 记录所有已登记要触发Stroke_OnStylusPointsChanged事件的InkCanvas的GUID
        /// （为什么不是已注册？因为这个事件是针对一条Stroke而言的，这里只是记录这个InkCanvas将会调用这个事件）
        /// </summary>
        private Dictionary<Guid, EventHandler>
            _innerInkCanvas_Stroke_OnStylusPointsChanged_wrappers = new Dictionary<Guid, EventHandler>();

        /// <summary>
        /// 记录所有已登记要触发Stroke_OnStylusPointsChanged事件的InkCanvas的GUID
        /// （为什么不是已注册？因为这个事件是针对一条Stroke而言的，这里只是记录这个InkCanvas将会调用这个事件）
        /// </summary>
        private Dictionary<Guid, StylusPointsReplacedEventHandler>
            _innerInkCanvas_Stroke_OnStylusPointsReplaced_wrappers = new Dictionary<Guid, StylusPointsReplacedEventHandler>();

        private async Task<IccInnerInkCanvasInfo> AddIccInkCanvas() {
            var screenW = SystemParameters.PrimaryScreenWidth;
            var screenH = SystemParameters.PrimaryScreenHeight;

            var fullWidth = screenW * 257;
            var fullHeight = screenH * 417;

            var left = 0 - screenW * 128;
            var top = 0 - screenH * 208;

            var di = new IccDispatcherInkCanvasInfo();
            await di.InitDispatcher();
            var control = await di.Dispatcher.InvokeAsync(() => new InkCanvas() {
                Background = new SolidColorBrush(Colors.White),
                Width = fullWidth,
                Height = fullHeight,
            });
            var dc = new DispatcherContainer() {
                Width = fullWidth,
                Height = fullHeight,
            };
            await dc.SetChildAsync(control);
            InkCanvasHostCanvas.Children.Add(dc);
            Canvas.SetTop(dc, top);
            Canvas.SetLeft(dc, left);
            var info = new IccInnerInkCanvasInfo() {
                GUID = di.GUID,
                Dispatcher = di.Dispatcher,
                Container = dc
            };
            DispatcherInkCanvasList.Add(info);

            // 注册事件
            RegisterTimeMachineEventsToInkCanvas(info.Dispatcher, info.Container, info.GUID);

            return info;
        }

        private void RemoveIccInkCanvas(IccInnerInkCanvasInfo item) {
            item.Dispatcher.BeginInvokeShutdown(DispatcherPriority.Render);
            InkCanvasHostCanvas.Children.Remove(item.Container);
            DispatcherInkCanvasList.Remove(item);
        }

        private void UpdateInnerInkCanvasVisibility(IccBoardPage item) {
            WrapperInkCanvas.Strokes.Clear();
            foreach (UIElement child in InkCanvasHostCanvas.Children) {
                child.Visibility = Visibility.Collapsed;
                if (child.Equals(item.Container)) child.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// 添加时光机支持到DispatcherInkCanvas中（这里是直接以GUID为标识符的）
        /// </summary>
        /// <param name="dispatcher"></param>
        /// <param name="container"></param>
        /// <param name="GUID"></param>
        private void RegisterTimeMachineEventsToInkCanvas(Dispatcher dispatcher, DispatcherContainer container, Guid GUID) {
            Task.Run(() => {
                dispatcher.Invoke(() => {
                    var ic = container.Child as InkCanvas;
                    // Strokes_OnStrokesChanged
                    if (!_innerInkCanvas_Strokes_OnStrokesChanged_wrappers.ContainsKey(GUID)) {
                        _innerInkCanvas_Strokes_OnStrokesChanged_wrappers.Add(GUID, (o, args) => {
                            InnerInkCanvas_Strokes_OnStrokesChanged(o, dispatcher, container, GUID, args);
                        });
                        ic.Strokes.StrokesChanged += _innerInkCanvas_Strokes_OnStrokesChanged_wrappers[GUID];
                    }
                    // Stroke_OnDrawingAttributesChanged
                    if (!_innerInkCanvas_Stroke_OnDrawingAttributesChanged_wrappers.ContainsKey(GUID)) {
                        _innerInkCanvas_Stroke_OnDrawingAttributesChanged_wrappers.Add(GUID, (o, args) => {
                            InnerInkCanvas_Stroke_OnDrawingAttributesChanged(o, dispatcher, container, GUID, args);
                        });
                    }
                    // Stroke_OnStylusPointsChanged
                    if (!_innerInkCanvas_Stroke_OnStylusPointsChanged_wrappers.ContainsKey(GUID)) {
                        _innerInkCanvas_Stroke_OnStylusPointsChanged_wrappers.Add(GUID, (o, args) => {
                            InnerInkCanvas_Stroke_OnStylusPointsChanged(o, dispatcher, container, GUID, args);
                        });
                    }
                    // Stroke_OnStylusPointsReplaced
                    if (!_innerInkCanvas_Stroke_OnStylusPointsReplaced_wrappers.ContainsKey(GUID)) {
                        _innerInkCanvas_Stroke_OnStylusPointsReplaced_wrappers.Add(GUID, (o, args) => {
                            InnerInkCanvas_Stroke_OnStylusPointsReplaced(o, dispatcher, container, GUID, args);
                        });
                    }
                });
            });
        }

        /// <summary>
        /// 取消注册时光机支持
        /// </summary>
        /// <param name="dispatcher"></param>
        /// <param name="container"></param>
        /// <param name="GUID"></param>
        private void UnRegisterTimeMachineEventsToInkCanvas(Dispatcher dispatcher, DispatcherContainer container, Guid GUID) {
            Task.Run(() => {
                dispatcher.Invoke(() => {
                    var ic = container.Child as InkCanvas;
                    // Strokes_OnStrokesChanged
                    if (_innerInkCanvas_Strokes_OnStrokesChanged_wrappers.ContainsKey(GUID)) {
                        _innerInkCanvas_Strokes_OnStrokesChanged_wrappers.Remove(GUID);
                        ic.Strokes.StrokesChanged -= _innerInkCanvas_Strokes_OnStrokesChanged_wrappers[GUID];
                    }
                    // Stroke_OnDrawingAttributesChanged
                    if (_innerInkCanvas_Stroke_OnDrawingAttributesChanged_wrappers.ContainsKey(GUID)) {
                        _innerInkCanvas_Stroke_OnDrawingAttributesChanged_wrappers.Remove(GUID);
                    }
                    // Stroke_OnStylusPointsChanged
                    if (_innerInkCanvas_Stroke_OnStylusPointsChanged_wrappers.ContainsKey(GUID)) {
                        _innerInkCanvas_Stroke_OnStylusPointsChanged_wrappers.Remove(GUID);
                    }
                    // Stroke_OnStylusPointsReplaced
                    if (_innerInkCanvas_Stroke_OnStylusPointsReplaced_wrappers.ContainsKey(GUID)) {
                        _innerInkCanvas_Stroke_OnStylusPointsReplaced_wrappers.Remove(GUID);
                    }
                });
            });
        }

        #endregion

        #region Multi-Pages

        /// <summary>
        /// 根据索引值获取页面
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public IccBoardPage this[int index] => BoardPages[index];

        /// <summary>
        /// 根据InkCanvas的GUID获取页面
        /// </summary>
        /// <param name="GUID"></param>
        /// <returns></returns>
        public IccBoardPage this[Guid GUID] => BoardPages.Find(page => page.GUID == GUID);

        /// <summary>
        /// 获取总页数
        /// </summary>
        public int PagesCount {
            get => BoardPages.Count;
        }

        /// <summary>
        /// 获取当前页面的索引值（如果需要展示页数的话，获取到的还需要加1）
        /// </summary>
        public int CurrentPage {
            get => CurrentPageIndex;
        }

        /// <summary>
        /// 获取到当前页面的 IccBoardPage 对象
        /// </summary>
        public IccBoardPage CurrentPageItem {
            get => BoardPages[CurrentPageIndex];
        }

        /// <summary>
        /// 添加新的白板页面（同时会创建新的DispatcherInkCanvas，如果想用现有的DispatcherInkCanvas插入为新页面的话，请使用 <see cref="AddPageFromExistedDispatcherInkCanvas"/> ）
        /// </summary>
        /// <param name="boardPageAppendMode">选择页面插入的模式</param>
        /// <param name="jumpToPageWhenAdded">指定是否在插入页面后跳转到该页面</param>
        /// <returns></returns>
        public async Task<IccBoardPage> AddPage(BoardPageAppendMode? boardPageAppendMode = BoardPageAppendMode.AppendAfterItem, bool? jumpToPageWhenAdded = true) {
            var info = await AddIccInkCanvas();
            
            var page = new IccBoardPage() {
                Container = info.Container,
                GUID = info.GUID,
                Dispatcher = info.Dispatcher,
                InkCanvas = await info.Dispatcher.InvokeAsync(() => info.Container.Child) as InkCanvas
            };

            page.TimeMachine.UndoRedoStateChanged += RaiseUndoRedoStateChangedEvent;

            var appendMode = boardPageAppendMode ?? BoardPageAppendMode.AppendAfterItem;
            var insertP = 0;

            if (appendMode == BoardPageAppendMode.AppendAfterItem) {
                insertP = Math.Max(Math.Min(CurrentPageIndex + 1, BoardPages.Count),0);
            } else if (appendMode == BoardPageAppendMode.AppendBeforeItem) {
                insertP = Math.Max(Math.Min(CurrentPageIndex - 1, BoardPages.Count),0);
            } else if (appendMode == BoardPageAppendMode.AppendToListStart) {
                insertP = 0;
            } else if (appendMode == BoardPageAppendMode.AppendToListEnd) {
                insertP = BoardPages.Count;
            }
            BoardPages.Insert(insertP, page);

            isIgnoreRaiseCurrentPageChangedEvent = false;

            if (jumpToPageWhenAdded??true) {
                UpdateInnerInkCanvasVisibility(page);
                CurrentPageIndex = BoardPages.IndexOf(page);
                RaiseCurrentPageChangedEvent();
            }
            
            return page;
        }

        /// <summary>
        /// 从现有的DispatcherInkCanvas中添加页面（和AddPage的区别就是不会调用AddIccInkCanvas，不会注册事件，也不会对DispatcherInkCanvas做任何初始化操作）
        /// （注意：如果使用该方法，需要自行处理TimeMachine相关的东西！）
        /// </summary>
        /// <param name="container"></param>
        /// <param name="dispatcher"></param>
        /// <param name="GUID"></param>
        /// <param name="boardPageAppendMode"></param>
        /// <param name="jumpToPageWhenAdded"></param>
        /// <returns></returns>
        public IccBoardPage AddPageFromExistedDispatcherInkCanvas(DispatcherContainer container, Dispatcher dispatcher, Guid? GUID, 
            BoardPageAppendMode? boardPageAppendMode = BoardPageAppendMode.AppendAfterItem, bool? jumpToPageWhenAdded = true) {
            var page = new IccBoardPage() {
                Container = container,
                GUID = GUID??Guid.NewGuid(),
                Dispatcher = dispatcher,
                InkCanvas = dispatcher.Invoke(() => container.Child) as InkCanvas
            };

            var appendMode = boardPageAppendMode ?? BoardPageAppendMode.AppendAfterItem;
            var insertP = 0;

            if (appendMode == BoardPageAppendMode.AppendAfterItem) {
                insertP = Math.Max(Math.Min(CurrentPageIndex + 1, BoardPages.Count),0);
            } else if (appendMode == BoardPageAppendMode.AppendBeforeItem) {
                insertP = Math.Max(Math.Min(CurrentPageIndex - 1, BoardPages.Count),0);
            } else if (appendMode == BoardPageAppendMode.AppendToListStart) {
                insertP = 0;
            } else if (appendMode == BoardPageAppendMode.AppendToListEnd) {
                insertP = BoardPages.Count;
            }
            BoardPages.Insert(insertP, page);

            isIgnoreRaiseCurrentPageChangedEvent = false;

            if (jumpToPageWhenAdded??true) {
                UpdateInnerInkCanvasVisibility(page);
                CurrentPageIndex = BoardPages.IndexOf(page);
                RaiseCurrentPageChangedEvent();
            }
            
            return page;
        }

        /// <summary>
        /// 切换到上一页
        /// </summary>
        /// <param name="step">跳转的页数，默认为1</param>
        public void GoToPreviousPage(int? step = 1) {
            var ci = CurrentPageIndex - step??1;
            if (ci < 0) ci = 0;

            var page = BoardPages[ci];
            UpdateInnerInkCanvasVisibility(page);

            if (!ci.Equals(CurrentPageIndex)) {
                CurrentPageIndex = ci;
                RaiseCurrentPageChangedEvent();
            }
        }

        /// <summary>
        /// 切换到下一页
        /// </summary>
        /// <param name="step">跳转的页数，默认为1</param>
        public void GoToNextPage(int? step = 1) {
            var ci = CurrentPageIndex + step??1;
            if (ci > BoardPages.Count - 1) ci = BoardPages.Count - 1;

            var page = BoardPages[ci];
            UpdateInnerInkCanvasVisibility(page);

            if (!ci.Equals(CurrentPageIndex)) {
                CurrentPageIndex = ci;
                RaiseCurrentPageChangedEvent();
            }
        }

        /// <summary>
        /// 跳转到指定索引的页面
        /// </summary>
        /// <param name="index">索引</param>
        public void GotoPage(int index) {
            UpdateInnerInkCanvasVisibility(BoardPages[index]);

            if (!index.Equals(CurrentPageIndex)) {
                CurrentPageIndex = index;
                RaiseCurrentPageChangedEvent();
            }
        }

        /// <summary>
        /// 跳转到指定页面
        /// </summary>
        /// <param name="page">页面</param>
        public void GotoPage(IccBoardPage page) {
            var index = BoardPages.IndexOf(page);
            UpdateInnerInkCanvasVisibility(page);

            if (!index.Equals(CurrentPageIndex)) {
                CurrentPageIndex = index;
                RaiseCurrentPageChangedEvent();
            }
        }

        /// <summary>
        /// 指示当前页面是否是最后一页，如果只有一页会返回true
        /// </summary>
        public bool IsCurrentLastPage => CurrentPageIndex == BoardPages.Count - 1;

        /// <summary>
        /// 指示当前页面是否是第一页，如果只有一页会返回true
        /// </summary>
        public bool IsCurrentFirstPage => CurrentPageIndex == 0;

        private async Task _RemovePage(IccBoardPage page, bool? isDisposeInstance = true, PageSwitchMode? pageSwitchMode = PageSwitchMode.SwitchToPreviousPage) {
            var isCurrent = CurrentPageItem == page;
            isIgnoreRaiseCurrentPageChangedEvent = true;
            if (isDisposeInstance??true) RemoveIccInkCanvas(DispatcherInkCanvasList.Find(info => info.GUID.Equals(page.GUID)));
            if (isCurrent) {
                var switchMode = pageSwitchMode ?? PageSwitchMode.SwitchToPreviousPage;
                if (IsCurrentFirstPage) GoToNextPage();
                else if (IsCurrentLastPage) GoToPreviousPage();
                else if (!IsCurrentFirstPage && !IsCurrentLastPage) {
                    if (switchMode == PageSwitchMode.SwitchToPreviousPage) GoToPreviousPage();
                    else if (switchMode == PageSwitchMode.SwitchToNextPage) GoToNextPage();
                }
            }
            page.TimeMachine.UndoRedoStateChanged -= RaiseUndoRedoStateChangedEvent;
            BoardPages.Remove(page);
            isIgnoreRaiseCurrentPageChangedEvent = false;
            if (BoardPages.Count == 0) await AddPage(BoardPageAppendMode.AppendToListEnd);
            else RaiseCurrentPageChangedEvent();
        }

        /// <summary>
        /// 移除指定索引的页面，如果只有1页，执行该方法会删除该页面并新建一个页面。
        /// </summary>
        /// <param name="index">索引</param>
        /// <param name="isDisposeInstance">指定是否销毁该页面的Dispatcher，销毁后无法再使用该实例添加到Board！</param>
        /// <param name="pageSwitchMode">删除当前激活页面后切换到临近页面的行为，如果删除的页面不是当前页面则不会生效</param>
        public async Task RemovePageAt(int index, bool? isDisposeInstance = true, PageSwitchMode? pageSwitchMode = PageSwitchMode.SwitchToPreviousPage) {
            var page = BoardPages[index];
            await _RemovePage(page, isDisposeInstance, pageSwitchMode);
        }

        /// <summary>
        /// 移除当前页面，如果只有1页，执行该方法会删除当前页面并新建一个页面。
        /// </summary>
        /// <param name="isDisposeInstance">指定是否销毁该页面的Dispatcher，销毁后无法再使用该实例添加到Board！</param>
        /// <param name="pageSwitchMode">删除当前激活页面后切换到临近页面的行为，如果删除的页面不是当前页面则不会生效</param>
        public async Task RemovePage(bool? isDisposeInstance = true, PageSwitchMode? pageSwitchMode = PageSwitchMode.SwitchToPreviousPage) {
            var page = CurrentPageItem;
            await _RemovePage(page, isDisposeInstance, pageSwitchMode);
        }

        /// <summary>
        /// 移除指定的页面，如果只有1页，执行该方法会删除该页面并新建一个页面。
        /// </summary>
        /// <param name="page">页面</param>
        /// <param name="isDisposeInstance">指定是否销毁该页面的Dispatcher，销毁后无法再使用该实例添加到Board！</param>
        /// <param name="pageSwitchMode">删除当前激活页面后切换到临近页面的行为，如果删除的页面不是当前页面则不会生效</param>
        public async Task RemovePage(IccBoardPage page, bool? isDisposeInstance = true, PageSwitchMode? pageSwitchMode = PageSwitchMode.SwitchToPreviousPage) {
            await _RemovePage(page, isDisposeInstance, pageSwitchMode);
        }

        #endregion

        #region Dynamic Renderer

        public static readonly Guid StrokeUniqueIdKeyGuid = Guid.Parse("71600a68-0a93-4e71-8120-5a7fad5de7e2");

        private async void WrapperInkCanvas_Loaded(object sender, RoutedEventArgs e) {
            var ic = (IccInkCanvas)sender;

            // 启动时自动修改 InkCanvas 的大小
            var screenW = SystemParameters.PrimaryScreenWidth;
            var screenH = SystemParameters.PrimaryScreenHeight;

            var fullWidth = screenW * 257;
            var fullHeight = screenH * 417;

            var left = 0 - screenW * 128;
            var top = 0 - screenH * 208;

            ic.Width = fullWidth;
            ic.Height = fullHeight;
            Canvas.SetLeft(ic, left);
            Canvas.SetTop(ic, top);

            ic.DefaultDrawingAttributes.Width = BoardSettings.NibWidth;
            ic.DefaultDrawingAttributes.Height = BoardSettings.NibHeight;
            ic.DefaultDrawingAttributes.Color = BoardSettings.NibColor;

            ic.StrokeCollected += IccWrapperInkCanvas_StrokeCollected;

            ic.BoardSettings = BoardSettings;

            // BoardSettings 事件注册
            RegisterEventsForBoardSettings();

            if (BoardPages.Count == 0) {
                await AddPage();
            }
        }

        private void IccWrapperInkCanvas_StrokeCollected(object sender, InkCanvasStrokeCollectedEventArgs e) {
            var ic = (IccInkCanvas)sender;

            // add unique id to stroke
            e.Stroke.AddPropertyData(StrokeUniqueIdKeyGuid, BoardPages[CurrentPageIndex].LastStrokeID);
            BoardPages[CurrentPageIndex].LastStrokeID += 1L;

            Task.Run(() => {
                BoardPages[CurrentPageIndex].Dispatcher.Invoke(() => {
                    var _c = (InkCanvas)BoardPages[CurrentPageIndex].Container.Child;
                    _c.Strokes.Add(e.Stroke);
                });
                Task.Delay(100);
                Dispatcher.InvokeAsync(()=>ic.Strokes.Remove(e.Stroke));
            });
        }

        #endregion

        #region Eraser Overlay

        private IncrementalStrokeHitTester eraserStrokeHitTester;
        private bool isEraserOverlayPointerDown = false;

        private void EraserOverlayCanvas_Loaded(object sender, RoutedEventArgs e) {
            var bd = (Canvas)sender;
            bd.StylusDown += ((o, args) => {
                e.Handled = true;
                if (args.StylusDevice.TabletDevice.Type == TabletDeviceType.Stylus) ((Canvas)o).CaptureStylus();
                EraserOverlay_PointerDown(sender);
            });
            bd.StylusUp += ((o, args) => {
                e.Handled = true;
                if (args.StylusDevice.TabletDevice.Type == TabletDeviceType.Stylus) ((Canvas)o).ReleaseStylusCapture();
                EraserOverlay_PointerUp(sender);
            });
            bd.StylusMove += ((o, args) => {
                e.Handled = true;
                EraserOverlay_PointerMove(sender, args.GetPosition(WrapperInkCanvas), args.GetPosition(this));
            });
            bd.MouseDown += ((o, args) => {
                ((Canvas)o).CaptureMouse();
                EraserOverlay_PointerDown(sender);
            });
            bd.MouseUp += ((o, args) => {
                ((Canvas)o).ReleaseMouseCapture();
                EraserOverlay_PointerUp(sender);
            });
            bd.MouseMove += ((o, args) => {
                EraserOverlay_PointerMove(sender, args.GetPosition(WrapperInkCanvas), args.GetPosition(this));
            });
            BoardSettings.EraserTypeChanged += (o, args) => {
                if (BoardSettings.EraserType == EraserType.Rectangle)
                    EraserFeedback.Source = FindResource("RectangleEraserImageSource") as DrawingImage;
                else if (BoardSettings.EraserType == EraserType.Ellipse)
                    EraserFeedback.Source = FindResource("EllipseEraserImageSource") as DrawingImage;
            };
            EraserFeedback.Source = FindResource("RectangleEraserImageSource") as DrawingImage;
        }

        private void EraserOverlay_PointerDown(object sender) {
            if (isEraserOverlayPointerDown) return;
            if (CurrentPageItem.Dispatcher.Invoke(() =>
                    ((InkCanvas)CurrentPageItem.Container.Child).Strokes.Count) == 0) return;
            isEraserOverlayPointerDown = true;
            EraserFeedback.Width = Math.Max(BoardSettings.EraserSize,10);
            EraserFeedback.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
            EraserFeedback.Visibility = Visibility.Collapsed;


            StylusShape stylusTipShape;
            if (BoardSettings.EraserType == EraserType.Ellipse)
                stylusTipShape = new EllipseStylusShape(Math.Max(BoardSettings.EraserSize-4, 10),
                    Math.Max(BoardSettings.EraserSize-4, 10));
            else stylusTipShape = new RectangleStylusShape(BoardSettings.EraserSize - 4, (BoardSettings.EraserSize-4) * 56 / 38);

            // init hittester
            Task.Run(() => {
                eraserStrokeHitTester = CurrentPageItem.Dispatcher.Invoke(() =>
                    ((InkCanvas)CurrentPageItem.Container.Child).Strokes.GetIncrementalStrokeHitTester(stylusTipShape));
                CurrentPageItem.Dispatcher.Invoke(() => {
                    eraserStrokeHitTester.StrokeHit += (obj, e) => {
                        var stks = ((InkCanvas)CurrentPageItem.Container.Child).Strokes;
                        StrokeCollection eraseResult = e.GetPointEraseResults();
                        StrokeCollection strokesToReplace = new StrokeCollection { e.HitStroke };
                        if (eraseResult.Any()) {
                            stks.Replace(strokesToReplace, eraseResult);
                        } else {
                            stks.Remove(strokesToReplace);
                        }
                    };
                });
            });
        }

        private void EraserOverlay_PointerUp(object sender) {
            if (!isEraserOverlayPointerDown) return;
            isEraserOverlayPointerDown = false;
            EraserFeedback.Visibility = Visibility.Collapsed;

            if (ReplacedStroke != null || AddedStroke != null) {
                CurrentPageItem.TimeMachine.CommitStrokeEraseHistory(ReplacedStroke, AddedStroke);
                AddedStroke = null;
                ReplacedStroke = null;
            }

            Task.Run(() => {
                CurrentPageItem.Dispatcher.Invoke(() => {
                    eraserStrokeHitTester.EndHitTesting();
                });
                eraserStrokeHitTester = null;
            });
        }

        private void EraserOverlay_PointerMove(object sender, Point ptInInkCanvas, Point ptInEraserOverlay) {
            if (!isEraserOverlayPointerDown) return;
            if (EraserFeedback.Visibility == Visibility.Collapsed) EraserFeedback.Visibility = Visibility.Visible;
            EraserFeedbackTranslateTransform.X = ptInEraserOverlay.X - EraserFeedback.ActualWidth /2;
            EraserFeedbackTranslateTransform.Y = ptInEraserOverlay.Y  - EraserFeedback.ActualHeight /2;

            // erase stroke
            try {
                CurrentPageItem.Dispatcher.Invoke(() => {
                    eraserStrokeHitTester.AddPoint(ptInInkCanvas);
                });
            }
            catch{}
        }

        #endregion

        #region Rectangle Area Eraser

        private bool isRectangleAreaEraserCanvasPointerDown = false;
        private Point? rectangleAreaEraserCanvas_firstPt;
        private Point? rectangleAreaEraserCanvas_firstPtInIC;
        private Point? rectangleAreaEraserCanvas_lastPt;
        private Point? rectangleAreaEraserCanvas_lastPtInIC;

        private void HostCanvas_Loaded(object sender, RoutedEventArgs e) {
            var ca = (Canvas)sender;
            HostCanvasClipGeometry1.Rect = new Rect(new Size(ca.Width, ca.Height));
        }

        private void HostCanvas_SizeChanged(object sender, SizeChangedEventArgs e) {
            HostCanvasClipGeometry1.Rect = new Rect(e.NewSize);
        }

        private void RectangleAreaEraserCanvas_Loaded(object sender, RoutedEventArgs e) {
            var ca = (Canvas)sender;

            ca.StylusDown += ((o, args) => {
                e.Handled = true;
                if (args.StylusDevice.TabletDevice.Type == TabletDeviceType.Stylus) ((Canvas)o).CaptureStylus();
                RectangleAreaEraserCanvas_PointerDown(sender);
            });
            ca.StylusUp += ((o, args) => {
                e.Handled = true;
                if (args.StylusDevice.TabletDevice.Type == TabletDeviceType.Stylus) ((Canvas)o).ReleaseStylusCapture();
                RectangleAreaEraserCanvas_PointerUp(sender);
            });
            ca.StylusMove += ((o, args) => {
                e.Handled = true;
                RectangleAreaEraserCanvas_PointerMove(sender, args.GetPosition(WrapperInkCanvas), args.GetPosition(this));
            });
            ca.MouseDown += ((o, args) => {
                ((Canvas)o).CaptureMouse();
                RectangleAreaEraserCanvas_PointerDown(sender);
            });
            ca.MouseUp += ((o, args) => {
                ((Canvas)o).ReleaseMouseCapture();
                RectangleAreaEraserCanvas_PointerUp(sender);
            });
            ca.MouseMove += ((o, args) => {
                RectangleAreaEraserCanvas_PointerMove(sender, args.GetPosition(WrapperInkCanvas), args.GetPosition(this));
            });

            AreaErasingFeedback.Visibility = Visibility.Collapsed;
        }

        private void RectangleAreaEraserCanvas_PointerDown(object sender) {
            if (isRectangleAreaEraserCanvasPointerDown) return;
            isRectangleAreaEraserCanvasPointerDown = true;

            HostCanvasClipGeometry.Geometry2 = new RectangleGeometry();
            rectangleAreaEraserCanvas_firstPt = null;
            rectangleAreaEraserCanvas_lastPt = null;
            rectangleAreaEraserCanvas_firstPtInIC = null;
            rectangleAreaEraserCanvas_lastPtInIC = null;
        }

        private void RectangleAreaEraserCanvas_PointerUp(object sender) {
            if (!isRectangleAreaEraserCanvasPointerDown) return;
            isRectangleAreaEraserCanvasPointerDown = false;

            HostCanvasClipGeometry.Geometry2 = Geometry.Empty;

            var rect = new Rect(rectangleAreaEraserCanvas_firstPtInIC ?? new Point(0, 0),
                rectangleAreaEraserCanvas_lastPtInIC ?? new Point(0, 0));

            var stylusShape = new RectangleStylusShape(rect.Width, rect.Height);
            Task.Run(() => {
                CurrentPageItem.Dispatcher.Invoke(() => {
                    ((InkCanvas)CurrentPageItem.Container.Child).Strokes.Erase(
                        new Point[] { new Point(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2) }, stylusShape);
                });
                if (ReplacedStroke != null || AddedStroke != null) {
                    CurrentPageItem.TimeMachine.CommitStrokeEraseHistory(ReplacedStroke, AddedStroke);
                    AddedStroke = null;
                    ReplacedStroke = null;
                }
            });

            rectangleAreaEraserCanvas_firstPt = null;
            rectangleAreaEraserCanvas_lastPt = null;
            rectangleAreaEraserCanvas_firstPtInIC = null;
            rectangleAreaEraserCanvas_lastPtInIC = null;

            AreaErasingFeedback.Visibility = Visibility.Collapsed;

        }

        private void RectangleAreaEraserCanvas_PointerMove(object sender, Point ptInInkCanvas, Point ptInEraserOverlay) {
            if (!isRectangleAreaEraserCanvasPointerDown) return;

            if (rectangleAreaEraserCanvas_firstPt == null) {
                rectangleAreaEraserCanvas_firstPt = ptInEraserOverlay;
                rectangleAreaEraserCanvas_firstPtInIC = ptInInkCanvas;
            }
            rectangleAreaEraserCanvas_lastPt = ptInEraserOverlay;
            rectangleAreaEraserCanvas_lastPtInIC = ptInInkCanvas;

            // update geometry clip
            ((RectangleGeometry)HostCanvasClipGeometry.Geometry2).Rect = new Rect(
                rectangleAreaEraserCanvas_firstPt ?? new Point(0, 0),
                rectangleAreaEraserCanvas_lastPt ?? new Point(0, 0));

            // update fedback
            if (AreaErasingFeedback.Visibility == Visibility.Collapsed)
                AreaErasingFeedback.Visibility = Visibility.Visible;
            Canvas.SetTop(AreaErasingFeedback,Math.Min(((Point)rectangleAreaEraserCanvas_firstPt).Y,((Point)rectangleAreaEraserCanvas_lastPt).Y));
            Canvas.SetLeft(AreaErasingFeedback,Math.Min(((Point)rectangleAreaEraserCanvas_firstPt).X,((Point)rectangleAreaEraserCanvas_lastPt).X));
            AreaErasingFeedback.Width = Math.Abs(((Point)rectangleAreaEraserCanvas_firstPt).X - ((Point)rectangleAreaEraserCanvas_lastPt).X);
            AreaErasingFeedback.Height = Math.Abs(((Point)rectangleAreaEraserCanvas_firstPt).Y - ((Point)rectangleAreaEraserCanvas_lastPt).Y);
        }

        #endregion

        #region TimeMachine

        private bool _isTimeMachineThreadTaskDone = true;

        // 指示时光机该怎么判定本次历史记录提交
        private CommitReason _currentCommitType = CommitReason.UserInput;

        private StrokeCollection ReplacedStroke;
        private StrokeCollection AddedStroke;

        private Dictionary<Stroke, Tuple<StylusPointCollection, StylusPointCollection>> StylusPointsChangedHistory;

        private Dictionary<Stroke, StylusPointCollection> StrokeInitialHistory =
            new Dictionary<Stroke, StylusPointCollection>();

        private Dictionary<Stroke, Tuple<DrawingAttributes, DrawingAttributes>> DrawingAttributesHistory =
            new Dictionary<Stroke, Tuple<DrawingAttributes, DrawingAttributes>>();

        private Dictionary<Guid, List<Stroke>> DrawingAttributesHistoryFlag = new Dictionary<Guid, List<Stroke>>() {
            { DrawingAttributeIds.Color, new List<Stroke>() },
            { DrawingAttributeIds.DrawingFlags, new List<Stroke>() },
            { DrawingAttributeIds.IsHighlighter, new List<Stroke>() },
            { DrawingAttributeIds.StylusHeight, new List<Stroke>() },
            { DrawingAttributeIds.StylusTip, new List<Stroke>() },
            { DrawingAttributeIds.StylusTipTransform, new List<Stroke>() },
            { DrawingAttributeIds.StylusWidth, new List<Stroke>() }
        };

        /// <summary>
        /// 根据传入的History项目，将历史记录应用到指定页面上去
        /// </summary>
        /// <param name="page"></param>
        /// <param name="history"></param>
        private void ApplyHistoryToPage(IccBoardPage page, TimeMachineHistory history) {
            _currentCommitType = CommitReason.CodeInput;

            page.Dispatcher.InvokeAsync(() => {
                var inkcanvas = page.Container.Child as InkCanvas;

                while (!_isTimeMachineThreadTaskDone) {
                    Thread.Sleep(1);
                }

                _isTimeMachineThreadTaskDone = false;

                if (history.CommitType == TimeMachineHistoryType.UserInput) {
                    foreach (var strokes in history.CurrentStroke) {
                        Trace.WriteLine(strokes);
                        if (!inkcanvas.Strokes.Contains(strokes) && !history.StrokeHasBeenCleared) inkcanvas.Strokes.Add(strokes);
                        else if (inkcanvas.Strokes.Contains(strokes) && history.StrokeHasBeenCleared) {
                            foreach (var _stroke in inkcanvas.Strokes.Where(s =>
                                         s.ContainsPropertyData(StrokeUniqueIdKeyGuid) &&
                                         s.GetPropertyData(StrokeUniqueIdKeyGuid) ==
                                         strokes.GetPropertyData(StrokeUniqueIdKeyGuid)).ToArray()) inkcanvas.Strokes.Remove(_stroke);
                        }
                    }
                } else if (history.CommitType == TimeMachineHistoryType.ShapeRecognition) {
                    foreach (var strokes in history.CurrentStroke) {
                        if (inkcanvas.Strokes.Contains(strokes) && history.StrokeHasBeenCleared) 
                            foreach (var _stroke in inkcanvas.Strokes.Where(s =>
                                         s.ContainsPropertyData(StrokeUniqueIdKeyGuid) &&
                                         s.GetPropertyData(StrokeUniqueIdKeyGuid) ==
                                         strokes.GetPropertyData(StrokeUniqueIdKeyGuid)).ToArray()) inkcanvas.Strokes.Remove(_stroke);
                        else if (!inkcanvas.Strokes.Contains(strokes) && !history.StrokeHasBeenCleared) inkcanvas.Strokes.Add(strokes);
                    }
                    foreach (var strokes in history.ReplacedStroke) {
                        if (!inkcanvas.Strokes.Contains(strokes) && !history.StrokeHasBeenCleared) 
                            foreach (var _stroke in inkcanvas.Strokes.Where(s =>
                                         s.ContainsPropertyData(StrokeUniqueIdKeyGuid) &&
                                         s.GetPropertyData(StrokeUniqueIdKeyGuid) ==
                                         strokes.GetPropertyData(StrokeUniqueIdKeyGuid)).ToArray()) inkcanvas.Strokes.Remove(_stroke);
                        else if (inkcanvas.Strokes.Contains(strokes) && history.StrokeHasBeenCleared) inkcanvas.Strokes.Add(strokes);
                    }
                } else if (history.CommitType == TimeMachineHistoryType.StylusPoints) {
                    foreach (var currentStroke in history.StylusPointsDictionary) {
                        if (inkcanvas.Strokes.Contains(currentStroke.Key)) 
                            currentStroke.Key.StylusPoints = history.StrokeHasBeenCleared ? currentStroke.Value.Item1 : currentStroke.Value.Item2;
                    }
                } else if (history.CommitType == TimeMachineHistoryType.DrawingAttributes) {
                    foreach (var currentStroke in history.DrawingAttributes) {
                        if (inkcanvas.Strokes.Contains(currentStroke.Key))
                            currentStroke.Key.DrawingAttributes = history.StrokeHasBeenCleared ? currentStroke.Value.Item1 : currentStroke.Value.Item2;
                    }
                } else if (history.CommitType == TimeMachineHistoryType.Erased) {
                    if (!history.StrokeHasBeenCleared) {
                        if (history.CurrentStroke != null)
                            foreach (var currentStroke in history.CurrentStroke)
                                if (!inkcanvas.Strokes.Contains(currentStroke))
                                    inkcanvas.Strokes.Add(currentStroke);

                        if (history.ReplacedStroke != null)
                            foreach (var replacedStroke in history.ReplacedStroke)
                                if (inkcanvas.Strokes.Contains(replacedStroke))
                                    inkcanvas.Strokes.Remove(replacedStroke);
                    } else {
                        if (history.ReplacedStroke != null)
                            foreach (var replacedStroke in history.ReplacedStroke)
                                if (!inkcanvas.Strokes.Contains(replacedStroke))
                                    inkcanvas.Strokes.Add(replacedStroke);

                        if (history.CurrentStroke != null)
                            foreach (var currentStroke in history.CurrentStroke)
                                if (inkcanvas.Strokes.Contains(currentStroke))
                                    inkcanvas.Strokes.Remove(currentStroke);
                    }
                }

                _currentCommitType = CommitReason.UserInput;
                _isTimeMachineThreadTaskDone = true;
            });
        }

        /// <summary>
        /// 批量应用多个历史记录到页面上
        /// </summary>
        /// <param name="page"></param>
        /// <param name="histories"></param>
        private void ApplyHistoriesToPage(IccBoardPage page, TimeMachineHistory[] histories) {
            foreach (var timeMachineHistory in histories) {
                ApplyHistoryToPage(page,timeMachineHistory);
            }
        }

        /// <summary>
        /// 内部的InkCanvas触发了Strokes.StrokesChanged事件
        /// </summary>
        /// <param name="sender">发送事件的InkCanvas（需要用传入的Dispatcher操作）</param>
        /// <param name="dispatcher">这个InkCanvas所在的Dispatcher对象</param>
        /// <param name="container">这个InkCanvas所存放的DispatcherContainer</param>
        /// <param name="GUID">InkCanvas（也是页面的）GUID</param>
        /// <param name="eventArgs">StrokeCollectionChangedEventArgs事件</param>
        private void InnerInkCanvas_Strokes_OnStrokesChanged(object sender, Dispatcher dispatcher,
            DispatcherContainer container, Guid GUID, StrokeCollectionChangedEventArgs eventArgs) {
            Trace.WriteLine($"GUID：{GUID} 触发了Strokes_OnStrokesChanged，第{BoardPages.IndexOf(BoardPages.Find(page=>page.GUID.Equals(GUID)))+1}页");

            foreach (var stroke in eventArgs?.Removed) {
                stroke.DrawingAttributesChanged -=
                    _innerInkCanvas_Stroke_OnDrawingAttributesChanged_wrappers[GUID];
                stroke.StylusPointsReplaced -=
                    _innerInkCanvas_Stroke_OnStylusPointsReplaced_wrappers[GUID];
                stroke.StylusPointsChanged -=
                    _innerInkCanvas_Stroke_OnStylusPointsChanged_wrappers[GUID];
            }
            foreach (var stroke in eventArgs?.Added) {
                stroke.DrawingAttributesChanged +=
                    _innerInkCanvas_Stroke_OnDrawingAttributesChanged_wrappers[GUID];
                stroke.StylusPointsReplaced +=
                    _innerInkCanvas_Stroke_OnStylusPointsReplaced_wrappers[GUID];
                stroke.StylusPointsChanged +=
                    _innerInkCanvas_Stroke_OnStylusPointsChanged_wrappers[GUID];
            }

            if (_currentCommitType == CommitReason.CodeInput || _currentCommitType == CommitReason.ShapeDrawing) return;

            // 橡皮擦擦除墨迹和时光机对接
            if ((eventArgs?.Added.Count != 0 || eventArgs?.Removed.Count != 0) &&
                (_edittingMode == EditingMode.GeometryErasing || _edittingMode == EditingMode.AreaErasing)) {
                if (AddedStroke == null) AddedStroke = new StrokeCollection();
                if (ReplacedStroke == null) ReplacedStroke = new StrokeCollection();
                try {
                    AddedStroke.Add(eventArgs.Added);
                    ReplacedStroke.Add(eventArgs.Removed);
                }
                catch {}
                Trace.WriteLine(ReplacedStroke.Count);
                return;
            }

            // 有新墨迹
            if (eventArgs.Added.Count != 0) {
                // 判断是否是形状识别新增的墨迹
                if (_currentCommitType == CommitReason.ShapeRecognition) {
                    BoardPages.Find(page=>page.GUID==GUID).TimeMachine.CommitStrokeShapeHistory(ReplacedStroke, eventArgs.Added);
                    ReplacedStroke = null;
                    return;
                } else {
                    BoardPages.Find(page=>page.GUID==GUID).TimeMachine.CommitStrokeUserInputHistory(eventArgs.Added);
                    return;
                }
            }

            // 有被删除的墨迹
            if (eventArgs.Removed.Count != 0) {
                if (_currentCommitType == CommitReason.ShapeRecognition) {
                    ReplacedStroke = eventArgs.Removed;
                    return;
                } else if ((_edittingMode == EditingMode.GeometryErasing || _edittingMode == EditingMode.AreaErasing) || _currentCommitType == CommitReason.ClearingCanvas) {
                    BoardPages.Find(page=>page.GUID==GUID).TimeMachine.CommitStrokeEraseHistory(eventArgs.Removed);
                    return;
                }
            }
        }

        /// <summary>
        /// 内部的InkCanvas的Stroke触发了Stroke.DrawingAttributesChanged事件
        /// </summary>
        /// <param name="sender">发送事件的Stroke（需要用传入的Dispatcher操作）</param>
        /// <param name="dispatcher">这个InkCanvas所在的Dispatcher对象</param>
        /// <param name="container">这个InkCanvas所存放的DispatcherContainer</param>
        /// <param name="GUID">InkCanvas（也是页面的）GUID</param>
        /// <param name="eventArgs">PropertyDataChangedEventArgs事件</param>
        private void InnerInkCanvas_Stroke_OnDrawingAttributesChanged(object sender, Dispatcher dispatcher,
            DispatcherContainer container, Guid GUID, PropertyDataChangedEventArgs eventArgs) {
            Trace.WriteLine($"GUID：{GUID} 触发了Stroke_OnDrawingAttributesChanged，第{BoardPages.IndexOf(BoardPages.Find(page=>page.GUID.Equals(GUID)))+1}页");
        }

        /// <summary>
        /// 内部的InkCanvas的Stroke触发了Stroke.StylusPointsChanged事件
        /// </summary>
        /// <param name="sender">发送事件的Stroke（需要用传入的Dispatcher操作）</param>
        /// <param name="dispatcher">这个InkCanvas所在的Dispatcher对象</param>
        /// <param name="container">这个InkCanvas所存放的DispatcherContainer</param>
        /// <param name="GUID">InkCanvas（也是页面的）GUID</param>
        /// <param name="eventArgs">EventArgs事件</param>
        private void InnerInkCanvas_Stroke_OnStylusPointsChanged(object sender, Dispatcher dispatcher,
            DispatcherContainer container, Guid GUID, EventArgs eventArgs) {
            Trace.WriteLine($"GUID：{GUID} 触发了Stroke_OnStylusPointsChanged，第{BoardPages.IndexOf(BoardPages.Find(page=>page.GUID.Equals(GUID)))+1}页");
        }

        /// <summary>
        /// 内部的InkCanvas的Stroke触发了Stroke.StylusPointsReplaced事件
        /// </summary>
        /// <param name="sender">发送事件的Stroke（需要用传入的Dispatcher操作）</param>
        /// <param name="dispatcher">这个InkCanvas所在的Dispatcher对象</param>
        /// <param name="container">这个InkCanvas所存放的DispatcherContainer</param>
        /// <param name="GUID">InkCanvas（也是页面的）GUID</param>
        /// <param name="eventArgs">StylusPointsReplacedEventArgs事件</param>
        private void InnerInkCanvas_Stroke_OnStylusPointsReplaced(object sender, Dispatcher dispatcher,
            DispatcherContainer container, Guid GUID, StylusPointsReplacedEventArgs eventArgs) {
            Trace.WriteLine($"GUID：{GUID} 触发了Stroke_OnStylusPointsReplaced，第{BoardPages.IndexOf(BoardPages.Find(page=>page.GUID.Equals(GUID)))+1}页");
        }

        #endregion

        #region TimeMachine Public APIs

        /// <summary>
        /// 当前页面的历史记录数量
        /// </summary>
        public int CurrentPageHistoriesCount => CurrentPageItem.TimeMachine.CurrentHistoriesCount;

        /// <summary>
        /// 获取当前页面能否撤销
        /// </summary>
        public bool CanUndo => CurrentPageItem.TimeMachine.CanUndo;

        /// <summary>
        /// 获取当前页面能否重做
        /// </summary>
        public bool CanRedo => CurrentPageItem.TimeMachine.CanRedo;

        /// <summary>
        /// 撤销（仅针对当前激活页面）
        /// </summary>
        public void Undo() {
            if (!_isTimeMachineThreadTaskDone) return;
            var history = CurrentPageItem.TimeMachine.Undo(false);
            if (history != null) ApplyHistoryToPage(CurrentPageItem,history);
        }

        /// <summary>
        /// 多步撤销（仅针对当前激活页面）
        /// </summary>
        public void Undo(int steps = 1) {
            if (!_isTimeMachineThreadTaskDone) return;
            var histories = CurrentPageItem.TimeMachine.Undo(steps);
            if (histories.Length <= 0) return;
            foreach (var history in histories) {
                ApplyHistoryToPage(CurrentPageItem,history);
            }
        }

        /// <summary>
        /// 重做（仅针对当前激活页面）
        /// </summary>
        public void Redo() {
            if (!_isTimeMachineThreadTaskDone) return;
            var history = CurrentPageItem.TimeMachine.Redo(false);
            if (history != null) ApplyHistoryToPage(CurrentPageItem,history);
        }

        /// <summary>
        /// 多步撤销（仅针对当前激活页面）
        /// </summary>
        public void Redo(int steps = 1) {
            if (!_isTimeMachineThreadTaskDone) return;
            var histories = CurrentPageItem.TimeMachine.Redo(steps);
            if (histories.Length <= 0) return;
            foreach (var history in histories) {
                ApplyHistoryToPage(CurrentPageItem,history);
            }
        }

        #endregion

        #region 漫游相关代码 Roaming

        private bool isRoamingHitTestBorderPointerDown = false;

        private Point? roamingFirstPoint;
        private Point? roamingLastPoint;
        private Matrix? roamingMatrix = null;

        private void RoamingHitTestBorder_Loaded(object sender, RoutedEventArgs e) {
            var rh = (Border)sender;

            rh.StylusDown += ((o, args) => {
                e.Handled = true;
                if (args.StylusDevice.TabletDevice.Type == TabletDeviceType.Stylus) ((Border)o).CaptureStylus();
                RoamingHitTestBorder_PointerDown(sender);
            });
            rh.StylusUp += ((o, args) => {
                e.Handled = true;
                if (args.StylusDevice.TabletDevice.Type == TabletDeviceType.Stylus) ((Border)o).ReleaseStylusCapture();
                RoamingHitTestBorder_PointerUp(sender);
            });
            rh.StylusMove += ((o, args) => {
                e.Handled = true;
                RoamingHitTestBorder_PointerMove(sender, args.GetPosition(WrapperInkCanvas), args.GetPosition(null));
            });
            rh.MouseDown += ((o, args) => {
                ((Border)o).CaptureMouse();
                RoamingHitTestBorder_PointerDown(sender);
            });
            rh.MouseUp += ((o, args) => {
                ((Border)o).ReleaseMouseCapture();
                RoamingHitTestBorder_PointerUp(sender);
            });
            rh.MouseMove += ((o, args) => {
                RoamingHitTestBorder_PointerMove(sender, args.GetPosition(WrapperInkCanvas), args.GetPosition(null));
            });
        }

        /// <summary>
        /// TODO: 设置页面的MatrixTransform（还没有正式完工）
        /// </summary>
        /// <param name="page"></param>
        /// <param name="matrix"></param>
        private void _SetPageMatrixTransform(IccBoardPage page, Matrix matrix) {
            if (page.Container.RenderTransformOrigin != new Point(0, 0))
                page.Container.RenderTransformOrigin = new Point(0, 0);
            page.Container.RenderTransform = new MatrixTransform(matrix);
            WrapperInkCanvasMatrixTransform.Matrix = matrix;
        }

        private void RoamingHitTestBorder_PointerDown(object sender) {
            if (isRoamingHitTestBorderPointerDown) return;
            isRoamingHitTestBorderPointerDown = true;

            roamingFirstPoint = null;
            roamingLastPoint = null;
            roamingMatrix = null;
        }

        private void RoamingHitTestBorder_PointerUp(object sender) {
            if (!isRoamingHitTestBorderPointerDown) return;
            isRoamingHitTestBorderPointerDown = false;

            CurrentPageItem.TransformMatrix = (CurrentPageItem.Container.RenderTransform as MatrixTransform).Value;

            roamingFirstPoint = null;
            roamingLastPoint = null;
            roamingMatrix = null;
        }

        private void RoamingHitTestBorder_PointerMove(object sender, Point ptInInkCanvas, Point ptAbsolute) {
            if (!isRoamingHitTestBorderPointerDown) return;

            if (roamingFirstPoint == null) roamingFirstPoint = ptAbsolute;
            roamingLastPoint = ptAbsolute;

            if (roamingMatrix == null) roamingMatrix = CurrentPageItem.TransformMatrix;
            
            var deltaX = (roamingLastPoint ?? new Point(0, 0)).X - (roamingFirstPoint ?? new Point(0, 0)).X;
            var deltaY = (roamingLastPoint ?? new Point(0, 0)).Y - (roamingFirstPoint ?? new Point(0, 0)).Y;

            var mt = roamingMatrix??new Matrix();
            mt.Translate(deltaX,deltaY);
            
            _SetPageMatrixTransform(CurrentPageItem,mt);
        }

        #endregion

        public IccBoard() {
            InitializeComponent();
        }

    }
}
