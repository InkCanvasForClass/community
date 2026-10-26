using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// Windows UI Automation 的只读宿主实现。
    /// </summary>
    internal sealed class ScreenElementService : IScreenElementService
    {
        private sealed class TraversalBudget
        {
            public int Remaining;
            public TraversalBudget(int maxElements) => Remaining = Math.Max(1, maxElements);
        }

        public PluginScreenElementSnapshot GetElementAtPoint(
            Point screenPixelPoint, int maxAncestorDepth = 8,
            int maxChildDepth = 1, int maxElements = 128)
        {
            if (double.IsNaN(screenPixelPoint.X) || double.IsNaN(screenPixelPoint.Y)
                || double.IsInfinity(screenPixelPoint.X) || double.IsInfinity(screenPixelPoint.Y))
                return null;

            try
            {
                var element = AutomationElement.FromPoint(screenPixelPoint);
                if (element == null) return null;

                var budget = new TraversalBudget(maxElements);
                var result = new PluginScreenElementSnapshot
                {
                    Element = ReadElement(element, 0, maxChildDepth, budget)
                };

                var parent = TreeWalker.ControlViewWalker.GetParent(element);
                var depth = 0;
                while (parent != null && depth < ClampDepth(maxAncestorDepth, 32))
                {
                    result.Ancestors.Add(ReadElement(parent, depth + 1, 0, budget));
                    parent = TreeWalker.ControlViewWalker.GetParent(parent);
                    depth++;
                }

                return result;
            }
            catch (ElementNotAvailableException)
            {
                return null;
            }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile(
                    $"ScreenElementService.GetElementAtPoint failed: {ex.Message}",
                    Helpers.LogHelper.LogType.Warning);
                return null;
            }
        }

        public PluginScreenElementInfo GetWindowTree(
            IntPtr windowHandle = default, int maxDepth = 3, int maxElements = 256)
        {
            try
            {
                if (windowHandle == IntPtr.Zero)
                    windowHandle = GetForegroundWindow();
                if (windowHandle == IntPtr.Zero) return null;

                var element = AutomationElement.FromHandle(windowHandle);
                if (element == null) return null;

                var budget = new TraversalBudget(maxElements);
                return ReadElement(element, 0, ClampDepth(maxDepth, 8), budget);
            }
            catch (ElementNotAvailableException)
            {
                return null;
            }
            catch (Exception ex)
            {
                Helpers.LogHelper.WriteLogToFile(
                    $"ScreenElementService.GetWindowTree failed: {ex.Message}",
                    Helpers.LogHelper.LogType.Warning);
                return null;
            }
        }

        private static PluginScreenElementInfo ReadElement(
            AutomationElement element, int depth, int maxChildDepth, TraversalBudget budget)
        {
            if (element == null || budget.Remaining <= 0) return null;
            budget.Remaining--;

            var info = new PluginScreenElementInfo
            {
                Depth = depth,
                Name = Read(() => element.Current.Name),
                AutomationId = Read(() => element.Current.AutomationId),
                ClassName = Read(() => element.Current.ClassName),
                FrameworkId = Read(() => element.Current.FrameworkId),
                LocalizedControlType = Read(() => element.Current.LocalizedControlType),
                IsEnabled = Read(() => element.Current.IsEnabled),
                IsOffscreen = Read(() => element.Current.IsOffscreen),
                ProcessId = Read(() => element.Current.ProcessId),
                BoundingRectangle = Read(() => element.Current.BoundingRectangle),
                WindowHandle = new IntPtr(Read(() => element.Current.NativeWindowHandle))
            };

            try
            {
                var type = element.Current.ControlType;
                info.ControlType = type?.ProgrammaticName ?? "";
            }
            catch
            {
                info.ControlType = "";
            }

            try
            {
                if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var raw)
                    && raw is ValuePattern valuePattern)
                {
                    info.Value = valuePattern.Current.Value ?? "";
                }
            }
            catch
            {
                info.Value = "";
            }

            if (info.BoundingRectangle.IsEmpty
                || double.IsNaN(info.BoundingRectangle.X)
                || double.IsNaN(info.BoundingRectangle.Y))
            {
                info.BoundingRectangle = Rect.Empty;
            }

            if (maxChildDepth <= 0 || budget.Remaining <= 0) return info;

            try
            {
                var children = element.FindAll(TreeScope.Children,
                    System.Windows.Automation.Condition.TrueCondition);
                for (var i = 0; i < children.Count && budget.Remaining > 0; i++)
                {
                    var child = ReadElement(children[i], depth + 1, maxChildDepth - 1, budget);
                    if (child != null) info.Children.Add(child);
                }
            }
            catch (ElementNotAvailableException)
            {
                // UIA 元素可能在枚举期间销毁，保留已读取的父元素。
            }
            catch
            {
                // 单个 provider 失败不应影响整个屏幕上下文。
            }

            return info;
        }

        private static T Read<T>(Func<T> getter)
        {
            try { return getter(); }
            catch { return default; }
        }

        private static int ClampDepth(int value, int max)
            => Math.Max(0, Math.Min(max, value));

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
    }
}
