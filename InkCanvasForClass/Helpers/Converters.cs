using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Ink_Canvas.Converter
{
    public class IntNumberToString : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if ((double)value == 0)
            {
                return "无限制";
            }
            else
            {
                return ((double)value).ToString() + "人";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if ((double)value == 0)
            {
                return "无限制";
            }
            else
            {
                return ((double)value).ToString() + "人";
            }
        }
    }

    public class IntNumberToString2 : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if ((double)value == 0)
            {
                return "自动截图";
            }
            else
            {
                return ((double)value).ToString() + "条";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if ((double)value == 0)
            {
                return "自动截图";
            }
            else
            {
                return ((double)value).ToString() + "条";
            }
        }
    }

    public class IsEnabledToOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            bool isChecked = (bool)value;
            if (isChecked == true)
            {
                return 1d;
            }
            else
            {
                return 0.35;
            }
        }
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) { throw new NotImplementedException(); }
    }
}
