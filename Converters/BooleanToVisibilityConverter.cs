using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace JNR.Converters // <--- MAKE SURE THIS IS CORRECT
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool boolValue = false;
            if (value is bool)
            {
                boolValue = (bool)value;
            }

            bool inverse = parameter != null && parameter.ToString().ToLower() == "inverse";

            if (inverse) boolValue = !boolValue;

            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}