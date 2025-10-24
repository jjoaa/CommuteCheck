using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Kiosk.Converters
{
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isSelected)
            {
                return isSelected ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFA900")) 
                                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC"));
            }
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 