using System;
using System.Globalization;
using System.Windows.Data;

namespace WpfApp.Converters;

/// <summary>
/// 多值转换器，用于计算滑动条进度显示宽度
/// </summary>
public class RatioConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            // 检查参数数量
            if (values.Length < 4)
                return 0.0;

            // 提取参数
            var currentValue = System.Convert.ToDouble(values[0]);
            var minimum = System.Convert.ToDouble(values[1]);
            var maximum = System.Convert.ToDouble(values[2]);
            var trackWidth = System.Convert.ToDouble(values[3]);

            // 计算比例
            if (maximum - minimum == 0)
                return 0.0;

            // 计算比例值并应用到轨道宽度
            var ratio = (currentValue - minimum) / (maximum - minimum);

            // 限制在0-1范围内
            ratio = Math.Max(0, Math.Min(1, ratio));

            return ratio * trackWidth;
        }
        catch (Exception)
        {
            return 0.0;
        }
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}