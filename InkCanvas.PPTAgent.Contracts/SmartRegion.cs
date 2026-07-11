using System.Collections.Generic;

namespace InkCanvasPPTAgent.Contracts
{
    /// <summary>
    /// PPT 智慧模式：视频控件区域（坐标为原始 Shape 磅值，由主应用端做屏幕像素转换）
    /// </summary>
    public sealed class SmartRegion
    {
        /// <summary>左上角 X（磅）</summary>
        public double X { get; set; }
        /// <summary>左上角 Y（磅）</summary>
        public double Y { get; set; }
        /// <summary>宽度（磅）</summary>
        public double Width { get; set; }
        /// <summary>高度（磅）</summary>
        public double Height { get; set; }
        /// <summary>Shape 名称（调试用）</summary>
        public string ShapeName { get; set; }
        /// <summary>媒体类型（ppMediaTypeVideo = 13）</summary>
        public int MediaType { get; set; }
    }

    /// <summary>
    /// PPT Agent / COM 返回的智慧模式区域列表及放映窗口信息
    /// </summary>
    public sealed class SmartRegionsResponse
    {
        public List<SmartRegion> Regions { get; set; } = new List<SmartRegion>();
        /// <summary>当前幻灯片索引（1-based）</summary>
        public int SlideIndex { get; set; }
        /// <summary>放映窗口句柄（主应用用于坐标转换）</summary>
        public long SlideShowWindowHandle { get; set; }
        /// <summary>幻灯片宽度（磅）</summary>
        public float SlideWidth { get; set; }
        /// <summary>幻灯片高度（磅）</summary>
        public float SlideHeight { get; set; }
    }
}
