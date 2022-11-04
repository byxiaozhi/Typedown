using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace Typedown.Universal.Converters
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public bool IsReverse { get; set; }

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var res = (bool)value;
            if (IsReverse) res = !res;
            return res ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            var res = (Visibility)value;
            return IsReverse ? res == Visibility.Collapsed : res == Visibility.Visible;
        }
    }
}
