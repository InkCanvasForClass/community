using Ink_Canvas.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ink_Canvas.Plugins
{
    internal sealed class WindowOverviewService : IWindowOverviewService, IDisposable
    {
        private readonly WindowOverviewModel _model;
        private readonly object _lock = new object();
        private IReadOnlyList<PluginWindowInfo> _windows = Array.Empty<PluginWindowInfo>();
        private PluginWindowInfo _foregroundWindow;

        public WindowOverviewService(WindowOverviewModel model)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _model.WindowsUpdated += OnWindowsUpdated;
            Refresh(_model.Windows);
        }

        public IReadOnlyList<PluginWindowInfo> Windows
        {
            get { lock (_lock) return _windows; }
        }

        public PluginWindowInfo ForegroundWindow
        {
            get { lock (_lock) return _foregroundWindow; }
        }

        public event Action WindowsChanged;

        public void Refresh() => _model.UpdateWindows();

        private void OnWindowsUpdated(object sender, List<WindowInfo> windows) => Refresh(windows);

        private void Refresh(IReadOnlyList<WindowInfo> windows)
        {
            var snapshot = (windows ?? Array.Empty<WindowInfo>()).Select(window => new PluginWindowInfo
            {
                Handle = window.Handle,
                Title = window.Title ?? "",
                ClassName = window.ClassName ?? "",
                ProcessName = window.ProcessName ?? "",
                ProcessPath = window.ProcessPath ?? "",
                IsVisible = window.IsVisible,
                IsMinimized = window.IsMinimized,
                ProcessId = window.ProcessId
            }).ToList().AsReadOnly();
            var foregroundHandle = ForegroundWindowInfo.GetForegroundWindowHandle();
            var foreground = snapshot.FirstOrDefault(window => window.Handle == foregroundHandle);
            lock (_lock)
            {
                _windows = snapshot;
                _foregroundWindow = foreground;
            }
            WindowsChanged?.Invoke();
        }

        public void Dispose()
        {
            _model.WindowsUpdated -= OnWindowsUpdated;
        }
    }
}
