using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WpfApp.Converters
{
    public class BoolToColorConverter : IValueConverter
    {
        public Brush TrueValue { get; set; } = new SolidColorBrush(Color.FromRgb(76, 175, 80));  // #4CAF50
        public Brush FalseValue { get; set; } = new SolidColorBrush(Color.FromRgb(117, 117, 117));  // #757575

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? TrueValue : FalseValue;
            }
            return FalseValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 