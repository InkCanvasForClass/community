using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Ink_Canvas.Helpers
{
    public class ColorUtilities {

        /// <summary>
        /// 获取一个颜色的人眼感知亮度，并以 0~1 之间的小数表示。
        /// </summary>
        public static double GetGrayLevel(Color color) {
            return (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255;
        }

        /// <summary>
        /// 根据人眼感知亮度返回前景色到底是黑色还是白色
        /// </summary>
        /// <param name="grayLevel"><c>GetGrayLevel</c>返回的人眼感知亮度</param>
        /// <returns>Color</returns>
        public static Color GetReverseForegroundColor(double grayLevel) => grayLevel > 0.5 ? Colors.Black : Colors.White;
    }
}
