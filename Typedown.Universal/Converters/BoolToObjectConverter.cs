using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Markup;

namespace Typedown.Universal.Converters
{
    public class BoolToObjectConverter : IValueConverter
    {
        public object TrueValue { get; set; }

        public object FalseValue { get; set; }

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var res = (bool)value ? TrueValue : FalseValue;
            return XamlBindingHelper.ConvertValue(targetType, res);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
