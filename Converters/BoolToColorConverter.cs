using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WpfApp.Converters
{
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isEnabled)
            {
                return isEnabled ? 
                    new SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80)) :  // 启用时的绿色
                    new SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 67, 54));   // 禁用时的红色
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 