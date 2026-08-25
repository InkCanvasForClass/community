using Ink_Canvas.Helpers;
using Ink_Canvas.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Ink_Canvas.Windows.SettingsViews.Helpers;

namespace Ink_Canvas
{
    public partial class MainWindow
    {
        private readonly Dictionary<string, FrameworkElement> _pluginCanvasLayers =
            new Dictionary<string, FrameworkElement>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _pluginFocusInteractionOwners =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private PluginCanvasToolSession _activePluginCanvasToolSession;
        private string _pluginToolPreviousLogicalMode;
        private bool _pluginCanvasToolActive;

        internal bool IsPluginCanvasToolActive => _pluginCanvasToolActive;

        internal void SetPluginFocusInteraction(string pluginId, bool active)
        {
            ValidatePluginCanvasId(pluginId, nameof(pluginId));
            RunOnUiThread(() =>
            {
                var wasActive = _pluginFocusInteractionOwners.Count > 0;
                if (active) _pluginFocusInteractionOwners.Add(pluginId);
                else _pluginFocusInteractionOwners.Remove(pluginId);
                var isActive = _pluginFocusInteractionOwners.Count > 0;
                if (wasActive != isActive && Settings.Advanced.IsNoFocusMode)
                {
                    WindowSettingsHelper.IsTemporarilyDisablingNoFocusMode = isActive;
                    ApplyNoFocusMode();
                }

                if (!isActive || !Settings.Advanced.IsNoFocusMode) return;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_pluginFocusInteractionOwners.Count == 0 || !IsVisible) return;
                    Activate();
                    Focus();
                }), System.Windows.Threading.DispatcherPriority.Input);
            });
        }

        internal string GetPluginCanvasContrastingForegroundColor()
        {
            if (GridBackgroundCover?.Visibility == Visibility.Visible &&
                GridBackgroundCover.Background is SolidColorBrush background)
                return GetContrastingForegroundColor(background.Color);

            if (CustomBackgroundColor.HasValue)
                return GetContrastingForegroundColor(CustomBackgroundColor.Value);

            return Settings.Canvas.UsingWhiteboard ? "#FF111827" : "#FFF8FAFC";
        }

        private static string GetContrastingForegroundColor(Color color)
        {
            var luminance =
                0.2126 * ToLinearColorComponent(color.R / 255d) +
                0.7152 * ToLinearColorComponent(color.G / 255d) +
                0.0722 * ToLinearColorComponent(color.B / 255d);
            return luminance < 0.45 ? "#FFF8FAFC" : "#FF111827";
        }

        private static double ToLinearColorComponent(double component)
            => component <= 0.04045
                ? component / 12.92
                : Math.Pow((component + 0.055) / 1.055, 2.4);

        internal void RegisterPluginCanvasLayer(
            string pluginId,
            string layerId,
            CanvasLayerPlacement placement,
            Func<FrameworkElement> layerFactory,
            bool isHitTestVisible)
        {
            ValidatePluginCanvasId(pluginId, nameof(pluginId));
            ValidatePluginCanvasId(layerId, nameof(layerId));
            if (layerFactory == null) throw new ArgumentNullException(nameof(layerFactory));

            RunOnUiThread(() =>
            {
                if (InkCanvasGridForInkReplay == null)
                    throw new InvalidOperationException("画布尚未初始化。");

                var key = GetPluginCanvasKey(pluginId, layerId);
                RemovePluginCanvasLayerCore(key);

                var element = layerFactory();
                if (element == null)
                    throw new InvalidOperationException("插件画布图层工厂返回了 null。");

                if (element.Parent != null)
                    throw new InvalidOperationException("插件画布图层已属于其他可视树。");

                element.HorizontalAlignment = HorizontalAlignment.Stretch;
                element.VerticalAlignment = VerticalAlignment.Stretch;
                element.IsHitTestVisible = isHitTestVisible;
                Panel.SetZIndex(element, GetPluginLayerZIndex(placement));

                InkCanvasGridForInkReplay.Children.Add(element);
                _pluginCanvasLayers[key] = element;
            });
        }

        internal bool RemovePluginCanvasLayer(string pluginId, string layerId)
        {
            ValidatePluginCanvasId(pluginId, nameof(pluginId));
            ValidatePluginCanvasId(layerId, nameof(layerId));

            var removed = false;
            RunOnUiThread(() => removed = RemovePluginCanvasLayerCore(GetPluginCanvasKey(pluginId, layerId)));
            return removed;
        }

        internal void RemovePluginCanvasLayers(string pluginId)
        {
            ValidatePluginCanvasId(pluginId, nameof(pluginId));
            RunOnUiThread(() =>
            {
                var prefix = pluginId + ":";
                var keys = _pluginCanvasLayers.Keys
                    .Where(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                foreach (var key in keys) RemovePluginCanvasLayerCore(key);
            });
        }

        internal bool TryActivatePluginCanvasTool(
            string pluginId,
            string toolId,
            out ICanvasToolSession session)
        {
            ValidatePluginCanvasId(pluginId, nameof(pluginId));
            ValidatePluginCanvasId(toolId, nameof(toolId));

            PluginCanvasToolSession created = null;
            RunOnUiThread(() =>
            {
                if (!IsWhiteboardMode) return;

                if (IsCurrentPageFrozen)
                {
                    TryBlockFrozenPageMutation("使用插件画布工具");
                    return;
                }

                if (_activePluginCanvasToolSession != null)
                {
                    if (!string.Equals(
                            _activePluginCanvasToolSession.PluginId,
                            pluginId,
                            StringComparison.OrdinalIgnoreCase))
                        return;

                    EndPluginCanvasTool(_activePluginCanvasToolSession);
                }

                _pluginToolPreviousLogicalMode = _currentToolMode;
                _pluginCanvasToolActive = true;
                // 原生湿墨管线已被 csproj 排除编译（MW_NativeWetInk.cs 未参与编译），
                // 插件画布工具接管输入前以 WPF 实时墨迹的等价取消兜底，避免残留半截笔迹。
                AbortAllActiveTouchInputs();
                SetCurrentToolMode(InkCanvasEditingMode.None);

                created = new PluginCanvasToolSession(
                    pluginId,
                    toolId,
                    CapturePluginPointer,
                    ReleasePluginPointer,
                    EndPluginCanvasTool);
                _activePluginCanvasToolSession = created;
                AttachPluginCanvasToolInput();
            });

            session = created;
            return created != null;
        }

        internal void DeactivatePluginCanvasTools(string pluginId)
        {
            if (string.IsNullOrWhiteSpace(pluginId)) return;
            RunOnUiThread(() =>
            {
                if (_activePluginCanvasToolSession != null &&
                    string.Equals(
                        _activePluginCanvasToolSession.PluginId,
                        pluginId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    CancelAndEndPluginCanvasTool(_activePluginCanvasToolSession);
                }
            });
        }

        private void DeactivateActivePluginCanvasToolForModeChange()
        {
            if (_activePluginCanvasToolSession != null)
                CancelAndEndPluginCanvasTool(_activePluginCanvasToolSession);
        }

        private void CancelAndEndPluginCanvasTool(PluginCanvasToolSession session)
        {
            if (session == null) return;
            if (session.IsActive)
                session.Publish(new CanvasPointerEventArgs { Action = CanvasPointerAction.Cancel });
            EndPluginCanvasTool(session);
        }

        private void EndPluginCanvasTool(PluginCanvasToolSession session)
        {
            if (session == null) return;

            RunOnUiThread(() =>
            {
                if (!ReferenceEquals(session, _activePluginCanvasToolSession))
                {
                    session.MarkInactive();
                    return;
                }

                DetachPluginCanvasToolInput();
                session.ReleaseAllPointers();
                session.MarkInactive();
                _activePluginCanvasToolSession = null;
                _pluginCanvasToolActive = false;

                RestoreToolModeAfterPluginSession(_pluginToolPreviousLogicalMode);
                _pluginToolPreviousLogicalMode = null;
            });
        }

        private void RestoreToolModeAfterPluginSession(string mode)
        {
            var editingMode = InkCanvasEditingMode.None;
            switch (mode)
            {
                case "pen":
                case "color":
                    editingMode = InkCanvasEditingMode.Ink;
                    break;
                case "select":
                    editingMode = InkCanvasEditingMode.Select;
                    break;
                case "eraser":
                    editingMode = InkCanvasEditingMode.EraseByPoint;
                    break;
                case "eraserByStrokes":
                    editingMode = InkCanvasEditingMode.EraseByStroke;
                    break;
            }

            if (!SetCurrentToolMode(editingMode))
            {
                SetCurrentToolMode(InkCanvasEditingMode.None);
                mode = "cursor";
            }

            UpdateCurrentToolMode(string.IsNullOrWhiteSpace(mode) ? "cursor" : mode);
        }

        private void AttachPluginCanvasToolInput()
        {
            if (InkCanvasGridForInkReplay == null) return;
            InkCanvasGridForInkReplay.PreviewMouseDown += PluginCanvasTool_PreviewMouseDown;
            InkCanvasGridForInkReplay.PreviewMouseMove += PluginCanvasTool_PreviewMouseMove;
            InkCanvasGridForInkReplay.PreviewMouseUp += PluginCanvasTool_PreviewMouseUp;
            InkCanvasGridForInkReplay.PreviewMouseWheel += PluginCanvasTool_PreviewMouseWheel;
            InkCanvasGridForInkReplay.PreviewTouchDown += PluginCanvasTool_PreviewTouchDown;
            InkCanvasGridForInkReplay.PreviewTouchMove += PluginCanvasTool_PreviewTouchMove;
            InkCanvasGridForInkReplay.PreviewTouchUp += PluginCanvasTool_PreviewTouchUp;
            InkCanvasGridForInkReplay.PreviewStylusDown += PluginCanvasTool_PreviewStylusDown;
            InkCanvasGridForInkReplay.PreviewStylusMove += PluginCanvasTool_PreviewStylusMove;
            InkCanvasGridForInkReplay.PreviewStylusUp += PluginCanvasTool_PreviewStylusUp;
            PreviewKeyDown += PluginCanvasTool_PreviewKeyDown;
        }

        private void DetachPluginCanvasToolInput()
        {
            if (InkCanvasGridForInkReplay == null) return;
            InkCanvasGridForInkReplay.PreviewMouseDown -= PluginCanvasTool_PreviewMouseDown;
            InkCanvasGridForInkReplay.PreviewMouseMove -= PluginCanvasTool_PreviewMouseMove;
            InkCanvasGridForInkReplay.PreviewMouseUp -= PluginCanvasTool_PreviewMouseUp;
            InkCanvasGridForInkReplay.PreviewMouseWheel -= PluginCanvasTool_PreviewMouseWheel;
            InkCanvasGridForInkReplay.PreviewTouchDown -= PluginCanvasTool_PreviewTouchDown;
            InkCanvasGridForInkReplay.PreviewTouchMove -= PluginCanvasTool_PreviewTouchMove;
            InkCanvasGridForInkReplay.PreviewTouchUp -= PluginCanvasTool_PreviewTouchUp;
            InkCanvasGridForInkReplay.PreviewStylusDown -= PluginCanvasTool_PreviewStylusDown;
            InkCanvasGridForInkReplay.PreviewStylusMove -= PluginCanvasTool_PreviewStylusMove;
            InkCanvasGridForInkReplay.PreviewStylusUp -= PluginCanvasTool_PreviewStylusUp;
            PreviewKeyDown -= PluginCanvasTool_PreviewKeyDown;
        }

        private void PluginCanvasTool_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.OriginalSource is TextBox || e.OriginalSource is PasswordBox) return;
            var session = _activePluginCanvasToolSession;
            if (session == null || !session.IsActive) return;
            if (!IsWhiteboardMode || IsCurrentPageFrozen)
            {
                CancelAndEndPluginCanvasTool(session);
                e.Handled = true;
                return;
            }

            var args = new CanvasKeyEventArgs
            {
                Key = e.Key,
                Modifiers = Keyboard.Modifiers
            };
            session.Publish(args);
            e.Handled = args.Handled;
        }

        private void PluginCanvasTool_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.StylusDevice != null) return;
            PublishPluginPointer(e, CanvasPointerDeviceKind.Mouse, CanvasPointerAction.Down, 0, e.GetPosition(inkCanvas), 0.5f, true);
        }

        private void PluginCanvasTool_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.StylusDevice != null) return;
            PublishPluginPointer(e, CanvasPointerDeviceKind.Mouse, CanvasPointerAction.Move, 0, e.GetPosition(inkCanvas), 0.5f, true);
        }

        private void PluginCanvasTool_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.StylusDevice != null) return;
            PublishPluginPointer(e, CanvasPointerDeviceKind.Mouse, CanvasPointerAction.Up, 0, e.GetPosition(inkCanvas), 0.5f, true);
        }

        private void PluginCanvasTool_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
            => PublishPluginPointer(
                e,
                CanvasPointerDeviceKind.Mouse,
                CanvasPointerAction.Wheel,
                0,
                e.GetPosition(inkCanvas),
                0.5f,
                true,
                wheelDelta: e.Delta,
                modifiers: Keyboard.Modifiers);

        private void PluginCanvasTool_PreviewTouchDown(object sender, TouchEventArgs e)
            => PublishPluginPointer(e, CanvasPointerDeviceKind.Touch, CanvasPointerAction.Down,
                EncodeTouchPointerId(e.TouchDevice.Id), e.GetTouchPoint(inkCanvas).Position, 0.5f,
                e.TouchDevice.Id == 0, e.TouchDevice);

        private void PluginCanvasTool_PreviewTouchMove(object sender, TouchEventArgs e)
            => PublishPluginPointer(e, CanvasPointerDeviceKind.Touch, CanvasPointerAction.Move,
                EncodeTouchPointerId(e.TouchDevice.Id), e.GetTouchPoint(inkCanvas).Position, 0.5f,
                e.TouchDevice.Id == 0, e.TouchDevice);

        private void PluginCanvasTool_PreviewTouchUp(object sender, TouchEventArgs e)
            => PublishPluginPointer(e, CanvasPointerDeviceKind.Touch, CanvasPointerAction.Up,
                EncodeTouchPointerId(e.TouchDevice.Id), e.GetTouchPoint(inkCanvas).Position, 0.5f,
                e.TouchDevice.Id == 0, e.TouchDevice);

        private void PluginCanvasTool_PreviewStylusDown(object sender, StylusDownEventArgs e)
            => PublishStylusPointer(e, CanvasPointerAction.Down);

        private void PluginCanvasTool_PreviewStylusMove(object sender, StylusEventArgs e)
            => PublishStylusPointer(e, CanvasPointerAction.Move);

        private void PluginCanvasTool_PreviewStylusUp(object sender, StylusEventArgs e)
            => PublishStylusPointer(e, CanvasPointerAction.Up);

        private void PublishStylusPointer(StylusEventArgs e, CanvasPointerAction action)
        {
            var points = e.GetStylusPoints(inkCanvas);
            var pressure = points.Count == 0 ? 0.5f : points[points.Count - 1].PressureFactor;
            PublishPluginPointer(
                e,
                CanvasPointerDeviceKind.Pen,
                action,
                EncodePenPointerId(e.StylusDevice.Id),
                e.GetPosition(inkCanvas),
                pressure,
                true,
                e.StylusDevice);
        }

        private void PublishPluginPointer(
            RoutedEventArgs routedEvent,
            CanvasPointerDeviceKind deviceKind,
            CanvasPointerAction action,
            int pointerId,
            Point position,
            float pressure,
            bool isPrimary,
            InputDevice device = null,
            int wheelDelta = 0,
            ModifierKeys modifiers = ModifierKeys.None)
        {
            if (IsPluginCanvasUiSource(routedEvent.OriginalSource as DependencyObject)) return;
            var session = _activePluginCanvasToolSession;
            if (session == null || !session.IsActive) return;
            if (!IsWhiteboardMode || IsCurrentPageFrozen)
            {
                if (IsCurrentPageFrozen && action == CanvasPointerAction.Down)
                    TryBlockFrozenPageMutation("使用插件画布工具");
                CancelAndEndPluginCanvasTool(session);
                routedEvent.Handled = true;
                return;
            }

            if (device != null) session.RememberPointer(pointerId, device);
            var args = new CanvasPointerEventArgs
            {
                DeviceKind = deviceKind,
                Action = action,
                PointerId = pointerId,
                Position = position,
                Pressure = pressure,
                LeftButton = Mouse.LeftButton,
                RightButton = Mouse.RightButton,
                IsPrimary = isPrimary,
                WheelDelta = wheelDelta,
                Modifiers = modifiers
            };
            session.Publish(args);
            routedEvent.Handled = args.Handled;

            if (action == CanvasPointerAction.Up || action == CanvasPointerAction.Cancel)
                session.ForgetPointer(pointerId);
        }

        private bool IsPluginCanvasUiSource(DependencyObject source)
        {
            if (source == null) return false;
            foreach (var layer in _pluginCanvasLayers.Values)
            {
                if (layer.IsHitTestVisible && IsDescendantOf(source, layer)) return true;
            }
            return false;
        }

        private bool CapturePluginPointer(int pointerId)
            => _activePluginCanvasToolSession?.CaptureRememberedPointer(
                pointerId,
                InkCanvasGridForInkReplay) == true;

        private void ReleasePluginPointer(int pointerId)
            => _activePluginCanvasToolSession?.ReleaseRememberedPointer(pointerId);

        private bool RemovePluginCanvasLayerCore(string key)
        {
            if (!_pluginCanvasLayers.TryGetValue(key, out var element)) return false;
            try
            {
                InkCanvasGridForInkReplay?.Children.Remove(element);
            }
            finally
            {
                _pluginCanvasLayers.Remove(key);
            }
            return true;
        }

        private static int GetPluginLayerZIndex(CanvasLayerPlacement placement)
        {
            switch (placement)
            {
                case CanvasLayerPlacement.BelowInk: return -100;
                case CanvasLayerPlacement.AboveInk: return 100;
                case CanvasLayerPlacement.Adorner: return 1000;
                default: throw new ArgumentOutOfRangeException(nameof(placement));
            }
        }

        private static string GetPluginCanvasKey(string pluginId, string itemId)
            => pluginId + ":" + itemId;

        private static void ValidatePluginCanvasId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("插件 ID 和画布项 ID 不能为空。", parameterName);
            if (value.IndexOf(':') >= 0)
                throw new ArgumentException("插件 ID 和画布项 ID 不能包含冒号。", parameterName);
        }

        private static int EncodeTouchPointerId(int id) => 100000 + id;
        private static int EncodePenPointerId(int id) => 200000 + id;
    }

}
