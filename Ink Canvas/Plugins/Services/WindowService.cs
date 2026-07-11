using Ink_Canvas.Helpers;
using System;
using System.Windows;

namespace Ink_Canvas.Plugins
{
    internal class WindowService : IWindowService
    {
        private readonly MainWindow _mainWindow;

        public WindowService(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
        }

        public bool IsTopMost
        {
            get
            {
                try { return _mainWindow?.Topmost ?? false; }
                catch { return false; }
            }
        }

        public bool IsFullscreen
        {
            get
            {
                try { return _mainWindow?.isFullScreenApplied ?? false; }
                catch { return false; }
            }
        }

        public bool IsCollapsed { get; private set; }

        public event Action<bool> TopMostChanged;
        public event Action<bool> CollapseChanged;

        public void SetTopMost(bool topMost)
        {
            try
            {
                _mainWindow?.Dispatcher.Invoke(() =>
                {
                    _mainWindow.Topmost = topMost;
                    TopMostChanged?.Invoke(topMost);
                });
            }
            catch (Exception ex) { LogHelper.WriteLogToFile($"WindowService.SetTopMost failed: {ex.Message}", LogHelper.LogType.Warning); }
        }

        public void ToggleTopMost()
        {
            SetTopMost(!IsTopMost);
        }

        public void Collapse()
        {
            try
            {
                _mainWindow?.Dispatcher.Invoke(() =>
                {
                    _mainWindow.FoldFloatingBar_MouseUp(null, null);
                    IsCollapsed = true;
                    CollapseChanged?.Invoke(true);
                });
            }
            catch (Exception ex) { LogHelper.WriteLogToFile($"WindowService.Collapse failed: {ex.Message}", LogHelper.LogType.Warning); }
        }

        public void Expand()
        {
            try
            {
                _mainWindow?.Dispatcher.Invoke(() =>
                {
                    _mainWindow.UnFoldFloatingBar(null);
                    IsCollapsed = false;
                    CollapseChanged?.Invoke(false);
                });
            }
            catch (Exception ex) { LogHelper.WriteLogToFile($"WindowService.Expand failed: {ex.Message}", LogHelper.LogType.Warning); }
        }

        public void ToggleCollapse()
        {
            if (IsCollapsed) Expand();
            else Collapse();
        }

        public void EnterWhiteboard()
        {
            try
            {
                _mainWindow?.Dispatcher.Invoke(() =>
                {
                    _mainWindow.SwitchToBoardMode();
                });
            }
            catch (Exception ex) { LogHelper.WriteLogToFile($"WindowService.EnterWhiteboard failed: {ex.Message}", LogHelper.LogType.Warning); }
        }

        public void ExitWhiteboard()
        {
            try
            {
                _mainWindow?.Dispatcher.Invoke(() =>
                {
                    _mainWindow.FoldFloatingBar_MouseUp(null, null);
                });
            }
            catch (Exception ex) { LogHelper.WriteLogToFile($"WindowService.ExitWhiteboard failed: {ex.Message}", LogHelper.LogType.Warning); }
        }
    }
}
