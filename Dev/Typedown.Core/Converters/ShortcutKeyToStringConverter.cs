using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Typedown.Core.Models;
using Typedown.Core.ViewModels;
using Typedown.Core.Utilities;
using Windows.System;
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
