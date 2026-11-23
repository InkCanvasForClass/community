using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Media;

namespace Ink_Canvas.Controls
{
    /// <summary>
    /// 工具栏图标几何与画刷的静态缓存。
    /// 图标路径字符串、颜色都是不可变输入，解析/创建一次后的产物可以全局共享：
    /// 缓存对象一律冻结（Freezable.Freeze），既保证跨线程安全，也避免每次工具切换
    /// 或主题刷新时重复解析 Path 数据、重复实例化画刷。
    /// </summary>
    public static class InkResourceCache
    {
        private static readonly object Gate = new object();

        private static readonly Dictionary<string, Geometry> Geometries =
            new Dictionary<string, Geometry>(StringComparer.Ordinal);

        private static readonly Dictionary<Color, Brush> Brushes = new Dictionary<Color, Brush>();

        private static long _geometryParseCount;
        private static long _geometryCacheHits;
        private static long _brushCreateCount;
        private static long _brushCacheHits;

        /// <summary>累计 <see cref="Geometry.Parse(string)"/> 次数，用于校验缓存命中（防回归）。</summary>
        public static long GeometryParseCount => Interlocked.Read(ref _geometryParseCount);

        /// <summary>累计几何缓存命中次数。</summary>
        public static long GeometryCacheHits => Interlocked.Read(ref _geometryCacheHits);

        /// <summary>累计画刷创建次数，用于校验缓存命中（防回归）。</summary>
        public static long BrushCreateCount => Interlocked.Read(ref _brushCreateCount);

        /// <summary>累计画刷缓存命中次数。</summary>
        public static long BrushCacheHits => Interlocked.Read(ref _brushCacheHits);

        /// <summary>已缓存的不同图标路径数量。</summary>
        public static int CachedGeometryCount
        {
            get { lock (Gate) return Geometries.Count; }
        }

        /// <summary>已缓存的不同画刷颜色数量。</summary>
        public static int CachedBrushCount
        {
            get { lock (Gate) return Brushes.Count; }
        }

        /// <summary>
        /// 获取图标路径对应的几何对象。相同路径始终返回同一个已冻结实例。
        /// </summary>
        /// <param name="path">XAML 几何路径字符串。</param>
        public static Geometry GetGeometry(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            lock (Gate)
            {
                if (Geometries.TryGetValue(path, out var cached))
                {
                    Interlocked.Increment(ref _geometryCacheHits);
                    // 防回归：命中必须是已冻结的共享实例，否则说明有人往缓存里塞了可变的独立副本。
#if DEBUG
                    if (!cached.IsFrozen)
                        throw new InvalidOperationException("图标几何缓存被破坏（命中实例未冻结）：" + path);
#endif
                    return cached;
                }

                var geometry = Geometry.Parse(path);
                if (geometry.CanFreeze) geometry.Freeze();
                Geometries[path] = geometry;
                Interlocked.Increment(ref _geometryParseCount);
                return geometry;
            }
        }

        /// <summary>
        /// 获取颜色对应的纯色画刷。相同颜色始终返回同一个已冻结实例。
        /// </summary>
        public static Brush GetBrush(Color color)
        {
            lock (Gate)
            {
                if (Brushes.TryGetValue(color, out var cached))
                {
                    Interlocked.Increment(ref _brushCacheHits);
#if DEBUG
                    if (!cached.IsFrozen)
                        throw new InvalidOperationException("画刷缓存被破坏（命中实例未冻结）：" + color);
#endif
                    return cached;
                }

                var brush = new SolidColorBrush(color);
                if (brush.CanFreeze) brush.Freeze();
                Brushes[color] = brush;
                Interlocked.Increment(ref _brushCreateCount);
                return brush;
            }
        }

        /// <summary>
        /// 仅在几何实例发生变化时才写入 <see cref="GeometryDrawing.Geometry"/>，返回是否真正发生了更新。
        /// </summary>
        public static bool SetGeometry(GeometryDrawing drawing, string path)
        {
            return SetGeometry(drawing, GetGeometry(path));
        }

        /// <summary>
        /// 仅在几何实例发生变化时才写入 <see cref="GeometryDrawing.Geometry"/>，返回是否真正发生了更新。
        /// </summary>
        public static bool SetGeometry(GeometryDrawing drawing, Geometry geometry)
        {
            if (drawing == null) return false;
            if (ReferenceEquals(drawing.Geometry, geometry)) return false;
            drawing.Geometry = geometry;
            return true;
        }

        /// <summary>
        /// 仅在画刷实例发生变化时才写入 <see cref="GeometryDrawing.Brush"/>，返回是否真正发生了更新。
        /// </summary>
        public static bool SetBrush(GeometryDrawing drawing, Color color)
        {
            return SetBrush(drawing, GetBrush(color));
        }

        /// <summary>
        /// 仅在画刷实例发生变化时才写入 <see cref="GeometryDrawing.Brush"/>，返回是否真正发生了更新。
        /// </summary>
        public static bool SetBrush(GeometryDrawing drawing, Brush brush)
        {
            if (drawing == null) return false;
            if (ReferenceEquals(drawing.Brush, brush)) return false;
            drawing.Brush = brush;
            return true;
        }

        /// <summary>
        /// 仅在画刷实例发生变化时才写入目标画刷属性的当前值，返回是否真正发生了更新。
        /// </summary>
        public static bool SetBrush(ref Brush target, Color color)
        {
            return SetBrush(ref target, GetBrush(color));
        }

        /// <summary>
        /// 仅在画刷实例发生变化时才写入目标画刷属性的当前值，返回是否真正发生了更新。
        /// </summary>
        public static bool SetBrush(ref Brush target, Brush brush)
        {
            if (ReferenceEquals(target, brush)) return false;
            target = brush;
            return true;
        }
    }
}