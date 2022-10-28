using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;

namespace Typedown.Universal.Resources.Converters
{
    public class BoolReverseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value is not bool val || !val;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value is not bool val || !val;
        }
    }
}
