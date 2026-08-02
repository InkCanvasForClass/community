using System.Windows;
using System.Windows.Ink;

namespace Ink_Canvas.Plugins
{
    /// <summary>
    /// 墨迹特效服务：供插件控制宿主画布的墨迹渐变消隐动画（InkFade）。
    /// <para>宿主画布上的墨迹按时间渐隐消失，用于演示/答题场景的自动擦除效果。</para>
    /// <para>底层复用宿主 <c>InkFadeManager</c>；画布未初始化时调用方法可能无效，
    /// 但不会抛出异常。</para>
    /// </summary>
    public interface IInkEffectService
    {
        /// <summary>墨迹渐隐是否启用。写入即生效。</summary>
        bool IsEnabled { get; set; }

        /// <summary>渐隐时长（毫秒）。</summary>
        int FadeTime { get; set; }

        /// <summary>渐隐速度倍率。</summary>
        double FadeSpeedMultiplier { get; set; }

        /// <summary>
        /// 把一条墨迹加入渐隐队列（从 <paramref name="startPoint"/> 画到 <paramref name="endPoint"/>，
        /// 持续 <paramref name="strokeDurationMs"/> 毫秒后渐隐消失）。
        /// </summary>
        void AddFadingStroke(Stroke stroke, Point startPoint, Point endPoint, long strokeDurationMs = 0);

        /// <summary>从渐隐队列移除指定墨迹（立即停止其渐隐动画）。</summary>
        void RemoveStroke(Stroke stroke);

        /// <summary>清空全部渐隐墨迹。</summary>
        void ClearAllFadingStrokes();

        /// <summary>更新渐隐时长（毫秒）。</summary>
        void UpdateFadeTime(int fadeTime);

        /// <summary>更新渐隐速度倍率。</summary>
        void UpdateFadeSpeedMultiplier(double multiplier);

        /// <summary>启用墨迹渐隐。</summary>
        void Enable();

        /// <summary>停用墨迹渐隐。</summary>
        void Disable();
    }
}
