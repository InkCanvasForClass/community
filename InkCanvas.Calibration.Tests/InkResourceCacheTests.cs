using System;
using System.Windows.Media;
using Ink_Canvas.Controls;

namespace InkCanvas.Calibration.Tests
{
    /// <summary>
    /// InkResourceCache 的防回归断言。
    /// 一旦有人绕过缓存（重新 Geometry.Parse / new SolidColorBrush），
    /// 或让同一输入返回不同实例，这些用例会立刻失败，避免性能静默回退。
    /// </summary>
    internal static class InkResourceCacheTests
    {
        private const string PathA = "M0,0 L10,10 L20,0 Z";
        private const string PathB = "M0,0 H24 V24 H0 Z";

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        public static void RunAll()
        {
            GeometryLookupIsCached();
            GeometryParseIsSkippedOnCacheHit();
            GeometryLookupHandlesMissingInput();
            BrushLookupIsCached();
            BrushCreateIsSkippedOnCacheHit();
            SetGeometrySkipsRedundantWrite();
            SetBrushSkipsRedundantWrite();
            SetBrushRefSkipsRedundantWrite();
        }

        private static void GeometryLookupIsCached()
        {
            var first = InkResourceCache.GetGeometry(PathA);
            var second = InkResourceCache.GetGeometry(PathA);

            Check(first != null, "合法图标路径应解析出几何对象");
            Check(ReferenceEquals(first, second), "同一图标路径多次获取必须返回同一实例（缓存未命中）");
            Check(first.IsFrozen, "缓存的几何对象必须已冻结，避免被就地修改污染全局共享实例");
            Check(!ReferenceEquals(InkResourceCache.GetGeometry(PathB), first), "不同图标路径不应共享几何实例");
        }

        private static void GeometryParseIsSkippedOnCacheHit()
        {
            InkResourceCache.GetGeometry(PathA);
            long parses = InkResourceCache.GeometryParseCount;
            int cached = InkResourceCache.CachedGeometryCount;

            for (int i = 0; i < 50; i++)
            {
                InkResourceCache.GetGeometry(PathA);
            }

            Check(InkResourceCache.GeometryParseCount == parses,
                "重复获取同一图标几何时不应再次解析 Path 数据（Geometry.Parse 次数增长）");
            Check(InkResourceCache.CachedGeometryCount == cached,
                "重复获取同一图标几何不应新增缓存条目");
        }

        private static void GeometryLookupHandlesMissingInput()
        {
            Check(InkResourceCache.GetGeometry(null) == null, "null 路径应返回 null");
            Check(InkResourceCache.GetGeometry(string.Empty) == null, "空路径应返回 null");
        }

        private static void BrushLookupIsCached()
        {
            Color color = Color.FromRgb(17, 34, 51);
            var first = InkResourceCache.GetBrush(color);
            var second = InkResourceCache.GetBrush(color);

            Check(first != null, "颜色应能获取到画刷");
            Check(ReferenceEquals(first, second), "同一颜色多次获取必须返回同一画刷实例（缓存未命中）");
            Check(first.IsFrozen, "缓存的画刷必须已冻结，避免被就地改色污染全局共享实例");
            Check(!ReferenceEquals(InkResourceCache.GetBrush(Color.FromRgb(17, 34, 52)), first),
                "不同颜色不应共享画刷实例");
        }

        private static void BrushCreateIsSkippedOnCacheHit()
        {
            Color color = Color.FromRgb(61, 62, 63);
            InkResourceCache.GetBrush(color);
            long creates = InkResourceCache.BrushCreateCount;
            int cached = InkResourceCache.CachedBrushCount;

            for (int i = 0; i < 50; i++)
            {
                InkResourceCache.GetBrush(color);
            }

            Check(InkResourceCache.BrushCreateCount == creates,
                "重复获取同一颜色时不应重复实例化画刷（SolidColorBrush 创建次数增长）");
            Check(InkResourceCache.CachedBrushCount == cached,
                "重复获取同一颜色不应新增缓存条目");
        }

        private static void SetGeometrySkipsRedundantWrite()
        {
            var drawing = new GeometryDrawing();
            var geometry = InkResourceCache.GetGeometry(PathA);

            Check(InkResourceCache.SetGeometry(drawing, PathA), "首次写入几何应返回 true");
            Check(ReferenceEquals(drawing.Geometry, geometry), "写入后应持有缓存中的同一几何实例");

            long parses = InkResourceCache.GeometryParseCount;
            for (int i = 0; i < 20; i++)
            {
                Check(!InkResourceCache.SetGeometry(drawing, PathA), "几何未变化时应跳过写入并返回 false");
            }
            Check(InkResourceCache.GeometryParseCount == parses, "几何未变化时不应产生新的 Path 解析");
            Check(ReferenceEquals(drawing.Geometry, geometry), "跳过写入后仍应保持原几何实例");

            Check(InkResourceCache.SetGeometry(drawing, PathB), "几何变化时应返回 true");
            Check(ReferenceEquals(drawing.Geometry, InkResourceCache.GetGeometry(PathB)),
                "几何变化后应切换到新路径对应的缓存实例");
            Check(!InkResourceCache.SetGeometry(null, PathB), "空目标不应抛异常或视为更新");
        }

        private static void SetBrushSkipsRedundantWrite()
        {
            var drawing = new GeometryDrawing();
            var brush = InkResourceCache.GetBrush(Colors.Red);

            Check(InkResourceCache.SetBrush(drawing, Colors.Red), "首次写入画刷应返回 true");
            Check(ReferenceEquals(drawing.Brush, brush), "写入后应持有缓存中的同一画刷实例");

            long creates = InkResourceCache.BrushCreateCount;
            for (int i = 0; i < 20; i++)
            {
                Check(!InkResourceCache.SetBrush(drawing, Colors.Red), "画刷未变化时应跳过写入并返回 false");
            }
            Check(InkResourceCache.BrushCreateCount == creates, "画刷未变化时不应产生新的画刷实例");

            Check(InkResourceCache.SetBrush(drawing, Colors.Blue), "画刷变化时应返回 true");
            Check(ReferenceEquals(drawing.Brush, InkResourceCache.GetBrush(Colors.Blue)),
                "画刷变化后应切换到新颜色对应的缓存实例");
            Check(!InkResourceCache.SetBrush(null, Colors.Blue), "空目标不应抛异常或视为更新");
        }

        private static void SetBrushRefSkipsRedundantWrite()
        {
            Brush target = null;

            Check(InkResourceCache.SetBrush(ref target, Colors.Green), "首次写入目标画刷应返回 true");
            Check(ReferenceEquals(target, InkResourceCache.GetBrush(Colors.Green)),
                "目标画刷应指向缓存实例");
            Check(!InkResourceCache.SetBrush(ref target, Colors.Green), "画刷未变化时应返回 false");
        }
    }
}
