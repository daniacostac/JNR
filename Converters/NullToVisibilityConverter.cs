using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace JNR.Converters // <--- MAKE SURE THIS IS CORRECT
{
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool inverse = parameter != null && parameter.ToString().ToLower() == "inverse";
            bool isNull = value == null;

            if (inverse)
            {
                isNull = !isNull;
            }

            return isNull ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}