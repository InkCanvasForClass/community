using Ink_Canvas.Helpers;
using Ink_Canvas.Windows.SettingsViews.Helpers;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace Ink_Canvas
{
    /// <summary>
    /// 液态玻璃浮动栏的宿主逻辑：开关、位置持久化、随主窗状态跟随，
    /// 以及把玻璃栏上的工具按钮转发到既有的浮动栏处理器。
    /// 玻璃栏本体见 <see cref="LiquidGlassBarWindow"/>。
    /// </summary>
    public partial class MainWindow
    {
        private LiquidGlassBarWindow _liquidGlassBar;
        private DispatcherTimer _liquidGlassRefreshTimer;

        /// <summary>玻璃背景定期重抓间隔：桌面内容会变（切窗口、播视频），需要周期性跟上。</summary>
        private static readonly TimeSpan LiquidGlassRefreshInterval = TimeSpan.FromSeconds(2);

        // —— 生命周期 ——

        /// <summary>启动时按设置恢复玻璃浮动栏。</summary>
        internal void RestoreLiquidGlassBarOnStartup()
        {
            if (!Settings.Appearance.EnableLiquidGlassBar) return;
            ShowLiquidGlassBar();
        }

        internal void ShowLiquidGlassBar()
        {
            try
            {
                if (_liquidGlassBar == null || !_liquidGlassBar.IsLoaded)
                {
                    _liquidGlassBar = new LiquidGlassBarWindow(this);
                    // 不设 Owner：Owner 会让玻璃栏随主窗 Z 序沉降，全屏批注窗置底时会跟着不可见
                    ApplyLiquidGlassBarSavedPosition(_liquidGlassBar);
                    _liquidGlassBar.Show();
                }
                else
                {
                    _liquidGlassBar.Show();
                }

                _liquidGlassBar.GlassOpacity = Settings.Appearance.LiquidGlassBarOpacity;
                StartLiquidGlassRefreshTimer();
                RefreshLiquidGlassBarActiveState();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"液态玻璃浮动栏显示失败: {ex.Message}", LogHelper.LogType.Error);
            }
        }

        internal void HideLiquidGlassBar()
        {
            StopLiquidGlassRefreshTimer();

            if (_liquidGlassBar == null) return;
            try
            {
                _liquidGlassBar.Close();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"液态玻璃浮动栏关闭失败: {ex.Message}", LogHelper.LogType.Warning);
            }
            finally
            {
                _liquidGlassBar = null;
            }
        }

        /// <summary>把上次保存的位置应用到玻璃栏；无记录时放到屏幕底部居中。</summary>
        private void ApplyLiquidGlassBarSavedPosition(LiquidGlassBarWindow bar)
        {
            double x = Settings.Appearance.LiquidGlassBarPositionX;
            double y = Settings.Appearance.LiquidGlassBarPositionY;

            if (x >= 0 && y >= 0)
            {
                bar.Left = x;
                bar.Top = y;
                // 尺寸要等 SizeToContent 完成才知道，加载后再夹一次
                bar.Loaded += (_, _) => bar.ClampIntoWorkingArea();
                return;
            }

            bar.Loaded += (_, _) =>
            {
                try
                {
                    var area = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;
                    double scale = 1.0;
                    var source = PresentationSource.FromVisual(bar);
                    if (source?.CompositionTarget != null)
                        scale = source.CompositionTarget.TransformToDevice.M11;
                    if (scale <= 0) scale = 1.0;

                    double w = bar.ActualWidth > 0 ? bar.ActualWidth : 640;
                    double h = bar.ActualHeight > 0 ? bar.ActualHeight : 80;

                    bar.Left = (area.Left + (area.Width - w * scale) / 2) / scale;
                    bar.Top = (area.Bottom - h * scale - 24 * scale) / scale;
                    bar.ClampIntoWorkingArea();
                    SaveLiquidGlassBarPosition(bar.Left, bar.Top);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLogToFile($"液态玻璃浮动栏定位失败: {ex.Message}", LogHelper.LogType.Warning);
                }
            };
        }

        internal void SaveLiquidGlassBarPosition(double left, double top)
        {
            Settings.Appearance.LiquidGlassBarPositionX = left;
            Settings.Appearance.LiquidGlassBarPositionY = top;
            SettingsManager.SaveSettingsToFile();
        }

        // —— 背景周期刷新 ——

        private void StartLiquidGlassRefreshTimer()
        {
            StopLiquidGlassRefreshTimer();

            _liquidGlassRefreshTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = LiquidGlassRefreshInterval
            };
            _liquidGlassRefreshTimer.Tick += LiquidGlassRefreshTimer_Tick;
            _liquidGlassRefreshTimer.Start();
        }

        private void StopLiquidGlassRefreshTimer()
        {
            if (_liquidGlassRefreshTimer == null) return;
            _liquidGlassRefreshTimer.Stop();
            _liquidGlassRefreshTimer.Tick -= LiquidGlassRefreshTimer_Tick;
            _liquidGlassRefreshTimer = null;
        }

        private void LiquidGlassRefreshTimer_Tick(object sender, EventArgs e)
        {
            if (_liquidGlassBar == null || !_liquidGlassBar.IsVisible) return;
            // 鼠标在栏上时不重抓：隐藏/显示会打断悬停与点击
            if (_liquidGlassBar.IsMouseOver) return;

            _liquidGlassBar.RefreshBackdrop(recapture: true);
        }

        // —— 设置变更响应 ——

        internal void ApplyLiquidGlassBarEnabled(bool enabled)
        {
            if (enabled) ShowLiquidGlassBar();
            else HideLiquidGlassBar();
        }

        internal void ApplyLiquidGlassBarOpacity(double opacity)
        {
            if (_liquidGlassBar != null) _liquidGlassBar.GlassOpacity = opacity;
        }

        /// <summary>
        /// 把当前工具模式与画笔颜色同步到玻璃栏的选中态。
        /// 由 <c>UpdateCurrentToolMode</c> 与颜色切换后调用。
        /// </summary>
        internal void RefreshLiquidGlassBarActiveState()
        {
            if (_liquidGlassBar == null || !_liquidGlassBar.IsVisible) return;

            try
            {
                _liquidGlassBar.SyncActiveState(_currentToolMode, penType, drawingAttributes?.Color);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLogToFile($"液态玻璃浮动栏状态同步失败: {ex.Message}", LogHelper.LogType.Warning);
            }
        }

        // —— 工具转发：玻璃栏调用这些方法，业务逻辑仍在原处理器里 ——

        internal void LiquidGlassBarSelectPen()
            => PenIcon_Click(null, MakeGlassBarMouseArgs());

        internal void LiquidGlassBarSelectHighlighter()
            => SwitchToHighlighterPen(null, MakeGlassBarMouseArgs());

        internal void LiquidGlassBarSelectEraser()
            => EraserIcon_Click(null, MakeGlassBarMouseArgs());

        internal void LiquidGlassBarSelectLasso()
            => SymbolIconSelect_MouseUp(null, MakeGlassBarMouseArgs());

        internal void LiquidGlassBarUndo()
            => SymbolIconUndo_MouseUp(null, MakeGlassBarMouseArgs());

        internal void LiquidGlassBarRedo()
            => SymbolIconRedo_MouseUp(null, new RoutedEventArgs());

        internal void LiquidGlassBarClear()
            => BtnClear_Click(null, new RoutedEventArgs());

        internal void LiquidGlassBarToggleWhiteboard()
            => ImageBlackboard_MouseUp(null, MakeGlassBarMouseArgs());

        /// <summary>
        /// 打开"更多工具"弹窗。弹窗默认锚定在浮动栏按钮上，玻璃栏是独立窗口、
        /// 与主窗坐标系无关，因此直接沿用浮动栏锚点（弹窗仍显示在主窗内）。
        /// </summary>
        internal void LiquidGlassBarOpenTools()
            => SymbolIconTools_MouseUp(ToolsFloatingBarBtn, MakeGlassBarMouseArgs());

        private static MouseButtonEventArgs MakeGlassBarMouseArgs()
            => new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left);
    }
}
