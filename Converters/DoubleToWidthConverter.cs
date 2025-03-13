using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WpfApp.Converters
{
    /// <summary>
    /// 将滑动条值(0-1)转换为滑动轨道显示宽度
    /// </summary>
    public class DoubleToWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double doubleValue)
            {
                // 获取父级容器的ActualWidth
                double trackWidth = 100; // 默认值，将由实际元素宽度修正

                // 如果提供了参数作为最大宽度
                if (parameter is double maxWidth)
                {
                    trackWidth = maxWidth;
                }

                // 确保值在0-1范围内，并计算对应的像素宽度
                doubleValue = Math.Max(0, Math.Min(1, doubleValue));
                return doubleValue * trackWidth;
            }

            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 