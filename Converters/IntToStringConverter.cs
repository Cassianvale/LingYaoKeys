using System;
using System.Globalization;
using System.Windows.Data;

namespace WpfApp.Converters
{
    public class IntToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue)
            {
                return intValue.ToString();
            }
            return "0";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue && int.TryParse(stringValue, out int result))
            {
                return result;
            }
            return 0;
        }
    }
}