using System;
using System.Collections.ObjectModel;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Metadata;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Typedown.Core.Models;
using Typedown.Core.Utilities;
using Avalonia.Controls;

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
