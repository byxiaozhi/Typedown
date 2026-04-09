using System;
using System.Collections.ObjectModel;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Metadata;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Controls;
using Avalonia.Controls;

namespace Typedown.Core.Converters
{
    public class BoolToObjectConverter : IValueConverter
    {
        public object TrueValue { get; set; }

        public object FalseValue { get; set; }

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var res = (bool)value ? TrueValue : FalseValue;
            if (res is not string || targetType == typeof(string))
                return res;
            return XamlBindingHelper.ConvertValue(targetType, res);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
