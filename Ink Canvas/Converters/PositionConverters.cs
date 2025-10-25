using System;
using System.Globalization;
using System.Windows.Data;

namespace Ink_Canvas.Converters
{
    /// <summary>
    /// 位置计算转换器
    /// </summary>
    public class PositionConverters
    {
        /// <summary>
        /// 减法转换器
        /// </summary>
        public class SubtractConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                if (value is double baseValue && parameter is string paramStr)
                {
                    if (double.TryParse(paramStr, out double subtractValue))
                    {
                        return baseValue - subtractValue;
                    }
                }
                return value;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                throw new NotImplementedException();
            }
        }
    }
}
