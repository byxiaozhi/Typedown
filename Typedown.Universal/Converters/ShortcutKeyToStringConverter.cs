using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Typedown.Universal.Models;
using Typedown.Universal.ViewModels;
using Typedown.Universal.Utilities;
using Windows.System;
using Windows.UI.Xaml.Data;

namespace Typedown.Universal.Converters
{
    public class ShortcutKeyToStringConverter: IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if(value is ShortcutKey key)
                return Common.GetShortcutKeyText(key);
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
