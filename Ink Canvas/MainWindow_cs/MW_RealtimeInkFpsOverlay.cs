using Ink_Canvas.Helpers;

namespace Ink_Canvas
{
    /// <summary>
    /// 实时墨迹 FPS / 提交延迟 HUD 控制器。
    /// </summary>
    public partial class MainWindow
    {
        private RealtimeInkFpsOverlay _realtimeInkFpsOverlay;
        private bool _realtimeInkFpsOverlayHooked;

        private void EnsureRealtimeInkFpsOverlayCleanup()
        {
            if (_realtimeInkFpsOverlayHooked) return;
            _realtimeInkFpsOverlayHooked = true;
            Closed += (_, _) => HideRealtimeInkFpsOverlay();
        }

        /// <summary>
        /// 启动实时墨迹 FPS / 提交延迟 HUD。
        /// 若已存在则先关闭再重建，避免和旧实例的 CompositionTarget 订阅叠加。
        /// </summary>
        public void ShowRealtimeInkFpsOverlay()
        {
            try
            {
                EnsureRealtimeInkFpsOverlayCleanup();
                HideRealtimeInkFpsOverlay();
                _realtimeInkFpsOverlay = new RealtimeInkFpsOverlay();
                _realtimeInkFpsOverlay.Show();
            }
            catch (System.Exception ex)
            {
                LogHelper.WriteLogToFile(
                    $"[RealtimeInkFpsOverlay] Show 失败: {ex.Message}",
                    LogHelper.LogType.Error);
                _realtimeInkFpsOverlay = null;
            }
        }

        /// <summary>
        /// 关闭实时墨迹 FPS / 提交延迟 HUD（无实例时静默 no-op）。
        /// </summary>
        public void HideRealtimeInkFpsOverlay()
        {
            if (_realtimeInkFpsOverlay == null) return;
            try
            {
                _realtimeInkFpsOverlay.Close();
            }
            catch (System.Exception ex)
            {
                LogHelper.WriteLogToFile(
                    $"[RealtimeInkFpsOverlay] Close 失败: {ex.Message}",
                    LogHelper.LogType.Warning);
            }
            _realtimeInkFpsOverlay = null;
        }
    }
}