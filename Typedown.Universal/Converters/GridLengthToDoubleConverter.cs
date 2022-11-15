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
        public GridUnitType GridUnitType { get; set; } = GridUnitType.Pixel;

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value is GridLength length ? length.Value : 0d;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return new GridLength(value is double length ? length : 0, GridUnitType);
        }
    }
}
