using InkCanvasForClass.IccInkCanvas.Utils.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace InkCanvasForClass.IccInkCanvas {

    /// <summary>
    /// 基于 InkPresenter 优化和魔改的 InkBoard 控件
    /// </summary>
    public class InkBoard : FrameworkElement {
        private readonly VisualCollection _children;
        // Tuple 1为Dispatcher的Guid，2为对应Dispatcher
        private List<Tuple<Guid, Dispatcher>> _asyncDispatchers;

        private const int HalfIntMax = 1073741823;
        private const float HalfFloatMax = 1073741823f;
        private const int QuarterSInt = 536756397;
        private const float QuarterSFloat = 536756397f;

        public InkBoard() {
            VisualCacheMode = new BitmapCache(0);
            ClipToBounds = true;
            _children = new VisualCollection(this);

            test();

        }

        #region Dispatchers 管理

        /// <summary>
        /// 创建一个 Dispatcher
        /// </summary>
        /// <param name="customWorkerDispatcherName">如果可用，会使用这个名字创建Dispatcher（但是不会忽略GUID）</param>
        /// <returns>Tuple，1为Guid，2为Dispatcher</returns>
        private async Task<Tuple<Guid, Dispatcher>> CreateDispathcer(string customWorkerDispatcherName = "IccInkCanvas") {
            var guid = Guid.NewGuid();
            var dispatcher = await UIDispatcher.RunNewAsync($"{customWorkerDispatcherName}_" + guid);
            return new Tuple<Guid, Dispatcher>(guid, dispatcher);
        }

        /// <summary>
        /// 注册一个新的 Dispatcher，这将会把 Dispatcher 给记录到 _asyncDispatchers 中
        /// </summary>
        /// <param name="customWorkerDispatcherName">如果可用，会使用这个名字创建Dispatcher（但是不会忽略GUID）</param>
        private async void RegisterNewDispatcher(string customWorkerDispatcherName = "IccInkCanvas") {
            var dispatcher = await CreateDispathcer(customWorkerDispatcherName);
            _asyncDispatchers.Add(dispatcher);
        }

        /// <summary>
        /// 让一个 Dispatcher 停止工作并销毁
        /// </summary>
        /// <param name="dispatcher"></param>
        private void DisposeDispatcher(Dispatcher dispatcher) {
            dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
            GC.Collect();
        }

        /// <summary>
        /// 判断一个 Dispatcher 是否被挂起
        /// </summary>
        /// <param name="dispatcher"></param>
        /// <returns></returns>
        private static async Task<bool> CheckDispatcherHangAsync(Dispatcher dispatcher)
        {
            var taskCompletionSource = new TaskCompletionSource<bool>();
            _ = dispatcher.InvokeAsync(() => taskCompletionSource.TrySetResult(true));
            await Task.WhenAny(taskCompletionSource.Task, Task.Delay(TimeSpan.FromMilliseconds(1500)));
            return taskCompletionSource.Task.IsCompleted is false;
        }

        #endregion

        /// <summary>
        /// 创建跨线程 InkPresenter，使用传入的 Dispatcher
        /// </summary>
        /// <returns></returns>
        private async Task CreateInkPresenterWithDispatcher() {
            await UIDispatcher.RunNewAsync("IccInkCanvas_" + Guid.NewGuid());
        }

        private async Task test() {
            var di = new IccDispatcherInkCanvasInfo();
            await di.InitDispatcher();
            var control = await di.Dispatcher.InvokeAsync(() => new TextBlock() {
                Text = "Helloworld!",
                Foreground = new SolidColorBrush(Colors.Black),
                FontSize = 24,
            });
            var dc = new DispatcherContainer();
            await dc.SetChildAsync(control);
            _children.Add(dc);
            InvalidateVisual();
        }

        protected override int VisualChildrenCount {
            get { return _children.Count; }
        }

        protected override Visual GetVisualChild(int index) {
            if (index < 0 || index >= _children.Count) new ArgumentOutOfRangeException();
            return _children[index];
        }
    }
}
