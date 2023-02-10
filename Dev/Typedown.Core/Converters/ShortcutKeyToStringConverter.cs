using System;
using Typedown.Core.Models;
using Typedown.Core.Utilities;
using Windows.UI.Xaml.Data;

namespace Typedown.Core.Converters
{
    public class ShortcutKeyToStringConverter: IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value is ShortcutKey key ? Common.GetShortcutKeyText(key) : null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
