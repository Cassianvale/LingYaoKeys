using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WpfApp.Converters
{
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                switch (status)
                {
                    case "运行中":
                        return new SolidColorBrush(System.Windows.Media.Color.FromArgb(204, 76, 175, 80));  // 绿色
                    case "已禁用":
                        return new SolidColorBrush(System.Windows.Media.Color.FromArgb(204, 158, 158, 158));  // 灰色
                    default:
                        return new SolidColorBrush(System.Windows.Media.Color.FromArgb(204, 244, 67, 54)); // 红色（已停止）
                }
            }
            return new SolidColorBrush(System.Windows.Media.Color.FromArgb(204, 0, 0, 0));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 