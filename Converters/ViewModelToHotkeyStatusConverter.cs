using System.Globalization;
using System.Windows.Data;
using WpfApp.ViewModels;

namespace WpfApp.Converters
{
    public class ViewModelToHotkeyStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is KeyMappingViewModel keyMappingVM)
            {
                return keyMappingVM.HotkeyStatus;
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 