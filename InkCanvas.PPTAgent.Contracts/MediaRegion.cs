using System.Collections.Generic;

namespace InkCanvasPPTAgent.Contracts
{
    /// <summary>
    /// PPT 幻灯片中的媒体控件区域（屏幕坐标，像素）
    /// </summary>
    public sealed class MediaRegion
    {
        /// <summary>屏幕坐标 X（像素）</summary>
        public double ScreenX { get; set; }
        /// <summary>屏幕坐标 Y（像素）</summary>
        public double ScreenY { get; set; }
        /// <summary>屏幕宽度（像素）</summary>
        public double ScreenWidth { get; set; }
        /// <summary>屏幕高度（像素）</summary>
        public double ScreenHeight { get; set; }
        /// <summary>Shape 名称（调试用）</summary>
        public string ShapeName { get; set; }
        /// <summary>媒体类型</summary>
        public int MediaType { get; set; }
    }

    /// <summary>
    /// VSTO 端返回的媒体区域列表及当前放映窗口句柄
    /// </summary>
    public sealed class MediaRegionsResponse
    {
        public List<MediaRegion> Regions { get; set; } = new List<MediaRegion>();
        public int SlideIndex { get; set; }
        /// <summary>放映窗口句柄（主应用用于直接转发点击）</summary>
        public long SlideShowWindowHandle { get; set; }
        /// <summary>幻灯片宽度（磅）</summary>
        public float SlideWidth { get; set; }
        /// <summary>幻灯片高度（磅）</summary>
        public float SlideHeight { get; set; }
    }
}
