using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using Ink_Canvas.Helpers;

namespace Ink_Canvas
{
    public partial class MainWindow
    {
        private TouchDebugOverlay _touchDebugOverlay;

        /// <summary>
        /// 右上角触摸调试浮窗。由「触摸输入」页的开关控制，默认关闭。
        /// </summary>
        private void InitializeTouchDebugOverlay()
        {
            Closed += (s, e) =>
            {
                _touchDebugOverlay?.Dispose();
                _touchDebugOverlay = null;
            };

            Loaded += (s, e) => UpdateTouchDebugOverlayVisibility();
            if (IsLoaded) UpdateTouchDebugOverlayVisibility();
        }

        internal void UpdateTouchDebugOverlayVisibility()
        {
            bool enabled = Settings?.Advanced?.ShowTouchDebugOverlay == true;
            if (!enabled)
            {
                _touchDebugOverlay?.Hide();
                return;
            }

            if (_touchDebugOverlay == null)
            {
                _touchDebugOverlay = new TouchDebugOverlay(BuildTouchDebugText);
            }

            _touchDebugOverlay.Show(this);
        }

        private string BuildTouchDebugText()
        {
            var sb = new StringBuilder(512);
            string hidState = _hidTouchRegistered ? "注册" : "未注册";
            string gestureState = _hidGestureSessionActive
                ? (_hidGestureActive ? "会话/活动" : "会话")
                : "无";
            sb.AppendLine($"HID {hidState}  触点={_hidTouches.Count}  手势={gestureState}  pair={_hidGestureA}/{_hidGestureB}");
            sb.AppendLine($"通道  书写={TouchChannelName(CurrentTouchWritingChannel)}  手掌={TouchChannelName(CurrentPalmEraserChannel)}  鼠标={MouseChannelName(CurrentMouseWritingChannel)}");
            sb.AppendLine($"WPF   dec={dec.Count}  模式={inkCanvas.EditingMode}  预览={StrokeVisualList.Count}  外部={_palmExternalStrokes.Count}");
            sb.AppendLine($"鼠标  Pointer={_palmPointerWritingActive}  RawHid={_palmRawMouseWritingActive}");
            sb.AppendLine($"手掌  活动={isPalmEraserActive}  引擎={_palmEngine.PalmIds.Count()}  多点={isInMultiTouchMode}");

            if (_hidTouches.Count == 0)
            {
                sb.Append("触点  -");
                return sb.ToString();
            }

            sb.AppendLine("触点");
            foreach (KeyValuePair<int, HidTouchState> pair in _hidTouches.OrderBy(item => item.Key))
            {
                HidTouchState touch = pair.Value;
                string role = touch.IsPalm ? "掌" : "写";
                string down = touch.Down ? "下" : "抬";
                sb.AppendLine($"  #{touch.StableId} ({touch.Dip.X:F1},{touch.Dip.Y:F1})  {touch.WidthPx:F0}x{touch.HeightPx:F0}  p={touch.Pressure:F2}  {role}/{down}");
            }
            return sb.ToString();
        }

        private static string TouchChannelName(int channel)
        {
            return channel == 1 ? "HID" : "WPF";
        }

        private static string MouseChannelName(int channel)
        {
            return channel == 1 ? "Pointer" : "WPF";
        }
    }
}
