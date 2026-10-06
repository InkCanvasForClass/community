using Dusk.Wpf.Shell;
using Ink_Canvas.Helpers;
using System;
using System.Windows;
using System.Windows.Media;

namespace Ink_Canvas
{
    public partial class MainWindow
    {
        private bool _isDuskInitialized;

        /// <summary>
        /// 初始化并接管 Dusk 墨迹引擎
        /// </summary>
        internal void InitDuskEngine()
        {
            if (_isDuskInitialized || duskCanvas == null) return;
            try
            {
                // 1. 全局日志流保持静默或仅用于开发调试，避免每帧高频写盘拖垮 UI 渲染线程
                WpfNativeInkCanvas.LogAction = null;

                LogHelper.WriteLogToFile("[Dusk.Bridge] 开始初始化 Dusk 墨迹引擎...", LogHelper.LogType.Warning);

                // 2. 验证内核 ABI 版本
                int abiVersion = WpfNativeInkCanvas.KernelAbiVersion;
                LogHelper.WriteLogToFile($"[Dusk.Bridge] 成功加载 Dusk.Native 内核, ABI 版本: {abiVersion}", LogHelper.LogType.Warning);

                // 3. 启用内置撤销/重做栈
                duskCanvas.EnableHistory();

                // 4. 初始同步笔迹外观
                duskCanvas.EditingMode = ShellEditingMode.Ink;
                Color defaultColor = drawingAttributes?.Color ?? Ink_DefaultColor;
                double defaultWidth = drawingAttributes != null && drawingAttributes.Width > 0 ? drawingAttributes.Width : 2.5;
                double defaultEraserRadius = eraserWidth > 0 ? eraserWidth / 2 : 24.0;

                duskCanvas.InkColor = defaultColor;
                duskCanvas.StrokeWidth = defaultWidth;
                duskCanvas.EraserRadius = defaultEraserRadius;

                // 5. 确保 Dusk 画布可见并处于激活状态，同时屏蔽原有 WPF InkCanvas 避免双重渲染与抢占事件
                duskCanvas.Visibility = Visibility.Visible;
                duskCanvas.IsHitTestVisible = true;
                if (inkCanvas != null)
                {
                    inkCanvas.Visibility = Visibility.Hidden;
                    inkCanvas.IsHitTestVisible = false;
                }

                // 6. 挂接激光笔衰减与覆盖层渲染帧循环
                CompositionTarget.Rendering -= DuskCompositionTarget_Rendering;
                CompositionTarget.Rendering += DuskCompositionTarget_Rendering;

                _isDuskInitialized = true;
                LogHelper.WriteLogToFile($"[Dusk.Bridge] Dusk 墨迹引擎初始化成功! 初始颜色=#{defaultColor.A:X2}{defaultColor.R:X2}{defaultColor.G:X2}{defaultColor.B:X2}, 初始粗细={defaultWidth:F1}, 初始橡皮半径={defaultEraserRadius:F1}", LogHelper.LogType.Warning);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"[Dusk.Bridge] 初始化失败: {ex}", LogHelper.LogType.Error);
            }
        }

        private void DuskCompositionTarget_Rendering(object sender, EventArgs e)
        {
            if (_isDuskInitialized && duskCanvas != null)
            {
                long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                duskCanvas.TickOverlays(nowMs);
            }
        }

        /// <summary>
        /// 设置 Dusk 画布激活状态（命中测试与可见性）
        /// </summary>
        internal void SetDuskCanvasActive(bool active)
        {
            if (duskCanvas == null) return;
            LogHelper.WriteLogToFile($"[Dusk.Bridge] SetDuskCanvasActive: active={active}", LogHelper.LogType.Warning);
            if (active)
            {
                DuskSyncSlot();
            }
            duskCanvas.IsHitTestVisible = active;
            duskCanvas.Visibility = active ? Visibility.Visible : Visibility.Hidden;

            // 无论 Dusk 是否激活，旧 inkCanvas 均保持静默以防输入冲突
            if (inkCanvas != null)
            {
                inkCanvas.IsHitTestVisible = false;
                inkCanvas.Visibility = Visibility.Hidden;
            }
        }

        /// <summary>
        /// 同步当前笔触颜色与粗细到 Dusk
        /// </summary>
        internal void SyncDuskPenAttributes(Color color, double width)
        {
            if (!_isDuskInitialized || duskCanvas == null) return;
            LogHelper.WriteLogToFile($"[Dusk.Bridge] SyncDuskPenAttributes: color=#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}, width={width:F1}", LogHelper.LogType.Warning);
            duskCanvas.InkColor = color;
            duskCanvas.StrokeWidth = width > 0 ? width : 2.5;
            duskCanvas.EditingMode = ShellEditingMode.Ink;
        }

        /// <summary>
        /// 切换 Dusk 编辑模式（笔 / 点橡皮 / 笔段橡皮 / 禁用）
        /// </summary>
        internal void SetDuskEditingMode(ShellEditingMode mode)
        {
            if (!_isDuskInitialized || duskCanvas == null) return;
            LogHelper.WriteLogToFile($"[Dusk.Bridge] SetDuskEditingMode: mode={mode}", LogHelper.LogType.Warning);
            duskCanvas.EditingMode = mode;
        }

        /// <summary>
        /// 设置 Dusk 橡皮擦半径
        /// </summary>
        internal void SetDuskEraserRadius(double radius)
        {
            if (!_isDuskInitialized || duskCanvas == null) return;
            LogHelper.WriteLogToFile($"[Dusk.Bridge] SetDuskEraserRadius: radius={radius:F1}", LogHelper.LogType.Warning);
            duskCanvas.EraserRadius = radius > 0 ? radius : 24.0;
        }

        /// <summary>
        /// 执行 Dusk 撤销
        /// </summary>
        internal bool DuskUndo()
        {
            if (!_isDuskInitialized || duskCanvas == null) return false;
            LogHelper.WriteLogToFile("[Dusk.Bridge] DuskUndo()", LogHelper.LogType.Warning);
            return duskCanvas.Undo();
        }

        /// <summary>
        /// 执行 Dusk 重做
        /// </summary>
        internal bool DuskRedo()
        {
            if (!_isDuskInitialized || duskCanvas == null) return false;
            LogHelper.WriteLogToFile("[Dusk.Bridge] DuskRedo()", LogHelper.LogType.Warning);
            return duskCanvas.Redo();
        }

        /// <summary>
        /// 同步当前模式对应的 Dusk 槽位（白板模式下使用当前白板页码，非白板/桌面批注模式固定使用 Slot 0）
        /// </summary>
        internal void DuskSyncSlot()
        {
            if (!_isDuskInitialized || duskCanvas == null) return;
            int targetSlot = (currentMode == 1) ? Math.Max(1, CurrentWhiteboardIndex) : 0;
            if (duskCanvas.CurrentSlot != targetSlot)
            {
                LogHelper.WriteLogToFile($"[Dusk.Bridge] DuskSyncSlot: 依据 currentMode={currentMode} 切换槽位 -> Slot {targetSlot}", LogHelper.LogType.Warning);
                duskCanvas.SwitchSlot(targetSlot);
            }
        }

        /// <summary>
        /// 切换 Dusk 墨迹槽位（Slot 0 为桌面批注，Slot 1..N 为白板各页）
        /// </summary>
        internal void DuskSwitchSlot(int slotIndex)
        {
            if (!_isDuskInitialized || duskCanvas == null) return;
            LogHelper.WriteLogToFile($"[Dusk.Bridge] DuskSwitchSlot: slotIndex={slotIndex}", LogHelper.LogType.Warning);
            duskCanvas.SwitchSlot(slotIndex);
        }

        /// <summary>
        /// 删除指定白板页对应的 Dusk 槽位
        /// </summary>
        internal void DuskDeleteSlot(int slotIndex)
        {
            if (!_isDuskInitialized || duskCanvas == null) return;
            LogHelper.WriteLogToFile($"[Dusk.Bridge] DuskDeleteSlot: slotIndex={slotIndex}", LogHelper.LogType.Warning);
            duskCanvas.DeleteSlot(slotIndex);
        }

        /// <summary>
        /// 清空 Dusk 画布（isErasedByCode 为 true 表示切页/暂存过渡，保留当前槽位数据）
        /// </summary>
        internal void DuskClear(bool isErasedByCode = false)
        {
            if (!_isDuskInitialized || duskCanvas == null) return;
            if (isErasedByCode)
            {
                // 代码内部切页/暂存过渡，不销毁当前槽位笔迹
                return;
            }
            LogHelper.WriteLogToFile($"[Dusk.Bridge] DuskClear() 用户显式清空当前槽位画布 (Slot {duskCanvas.CurrentSlot})", LogHelper.LogType.Warning);
            duskCanvas.Clear();
        }

        /// <summary>
        /// 释放 Dusk 原生资源
        /// </summary>
        internal void DisposeDuskEngine()
        {
            LogHelper.WriteLogToFile("[Dusk.Bridge] DisposeDuskEngine()", LogHelper.LogType.Warning);
            CompositionTarget.Rendering -= DuskCompositionTarget_Rendering;
            if (duskCanvas != null)
            {
                duskCanvas.Dispose();
            }
            _isDuskInitialized = false;
        }
    }
}
