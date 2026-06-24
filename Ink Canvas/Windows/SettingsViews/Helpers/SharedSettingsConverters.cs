using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Ink_Canvas.Windows.SettingsViews.Helpers
{
    /// <summary>
    /// 将几何字符串转换为 Geometry 对象。
    /// </summary>
    public class StringToGeometryConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s && !string.IsNullOrWhiteSpace(s))
            {
                return Geometry.Parse(s);
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// null 值转 Visibility（null → Collapsed，非 null → Visible）。
    /// </summary>
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// IdToPathData 转换器基类：将组件 Id 转换为 Path 可用的 Geometry 对象。
    /// 子类只需提供 IdToIconGeometryString 转换逻辑。
    /// </summary>
    public abstract class IdToPathDataConverterBase : IValueConverter
    {
        private static readonly StringToGeometryConverter _strToGeo = new();

        protected abstract string ConvertIdToGeometryString(string id);

        public virtual object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var id = value as string;
            if (id == null) return null;
            var geoString = ConvertIdToGeometryString(id);
            return _strToGeo.Convert(geoString, targetType, parameter, culture);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
