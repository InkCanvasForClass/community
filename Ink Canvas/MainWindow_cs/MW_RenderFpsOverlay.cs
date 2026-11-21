using Ink_Canvas.Helpers;

namespace Ink_Canvas
{
    /// <summary>
    /// 渲染帧率 HUD 控制器。
    /// </summary>
    public partial class MainWindow
    {
        private RenderFpsOverlay _renderFpsOverlay;
        private bool _renderFpsOverlayHooked;

        private void EnsureRenderFpsOverlayCleanup()
        {
            if (_renderFpsOverlayHooked) return;
            _renderFpsOverlayHooked = true;
            Closed += (_, _) => HideRenderFpsOverlay();
        }

        /// <summary>
        /// 显示渲染帧率 HUD。已存在时先关闭重建，避免重复实例叠加计时器。
        /// </summary>
        public void ShowRenderFpsOverlay()
        {
            try
            {
                EnsureRenderFpsOverlayCleanup();
                HideRenderFpsOverlay();

                // HUD 需要刷新率作为判读基准，显示前先探测一次
                RenderPacingController.ProbeDisplayRefreshRate();

                _renderFpsOverlay = new RenderFpsOverlay();
                _renderFpsOverlay.Show();
            }
            catch (System.Exception ex)
            {
                LogHelper.WriteLogToFile($"[RenderFpsOverlay] 显示失败: {ex.Message}", LogHelper.LogType.Error);
                _renderFpsOverlay = null;
            }
        }

        /// <summary>
        /// 关闭渲染帧率 HUD（无实例时静默 no-op）。
        /// </summary>
        public void HideRenderFpsOverlay()
        {
            if (_renderFpsOverlay == null) return;
            try
            {
                _renderFpsOverlay.Close();
            }
            catch (System.Exception ex)
            {
                LogHelper.WriteLogToFile($"[RenderFpsOverlay] 关闭失败: {ex.Message}", LogHelper.LogType.Warning);
            }
            _renderFpsOverlay = null;
        }
    }
}
