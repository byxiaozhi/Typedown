using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace Typedown.Universal.Converters
{
    public class GridLengthToDoubleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is GridLength length)
                return length.Value;
            return 0d;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is double length)
                return new GridLength(length);
            return new GridLength(0);
        }
    }
}
