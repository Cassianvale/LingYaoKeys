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
                return status == "运行中" 
                    ? new SolidColorBrush(System.Windows.Media.Color.FromArgb(204, 76, 175, 80))  // 绿色
                    : new SolidColorBrush(System.Windows.Media.Color.FromArgb(204, 244, 67, 54)); // 红色
            }
            return new SolidColorBrush(System.Windows.Media.Colors.Black);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 