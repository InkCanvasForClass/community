using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// <see cref="ICanvasElementService"/> 的宿主实现：元素插入/移除核心逻辑落在 MainWindow，
    /// 本类负责线程切换（所有方法可从任意线程调用）与参数校验。
    /// </summary>
    internal sealed class CanvasElementService : ICanvasElementService
    {
        private readonly MainWindow _mainWindow;

        public CanvasElementService(MainWindow mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        }

        private T RunOnUi<T>(Func<T> func)
            => _mainWindow.Dispatcher.CheckAccess() ? func() : _mainWindow.Dispatcher.Invoke(func);

        public bool InsertElement(FrameworkElement element)
            => RunOnUi(() => _mainWindow.InsertPluginCanvasElement(element, null));

        public bool InsertElement(FrameworkElement element, Point position)
            => RunOnUi(() => _mainWindow.InsertPluginCanvasElement(element, position));

        public bool TryRemoveElement(FrameworkElement element)
            => RunOnUi(() => _mainWindow.RemovePluginCanvasElement(element));

        public bool ContainsElement(FrameworkElement element)
            => RunOnUi(() => _mainWindow.ContainsPluginCanvasElement(element));

        public IReadOnlyList<FrameworkElement> GetElements()
            => RunOnUi(() => _mainWindow.GetPluginCanvasElements());
    }
}
